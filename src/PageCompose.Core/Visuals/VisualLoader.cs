using PdfSharp.Drawing;
using PdfSharp.Pdf.IO;

namespace PageCompose.Core.Visuals;

public static class VisualLoader
{
    /// <summary>Files read as they are.</summary>
    public static IReadOnlyList<string> SupportedExtensions { get; } = [".jpg", ".jpeg", ".png", ".pdf"];

    /// <summary>
    /// Photos that Windows decodes (iPhone HEIC, WebP, TIFF): the app turns them into a JPEG first, then reads it
    /// (<see cref="Load"/> does not take them).
    /// </summary>
    public static IReadOnlyList<string> ConvertedExtensions { get; } = [".heic", ".heif", ".webp", ".tif", ".tiff"];

    /// <summary>A file the app takes, as it is or converted.</summary>
    public static bool IsSupported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()) || NeedsConversion(path);

    public static bool NeedsConversion(string path) =>
        ConvertedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <param name="pageNumber">Page of a PDF, from 1 (ignored for an image).</param>
    /// <exception cref="VisualLoadException">Missing, unsupported or unreadable file, or no such page.</exception>
    public static VisualInfo Load(string path, int pageNumber = 1)
    {
        if (!File.Exists(path))
            throw new VisualLoadException(VisualLoadError.NotFound, path);

        string ext = Path.GetExtension(path).ToLowerInvariant();
        try
        {
            return ext switch
            {
                ".jpg" or ".jpeg" => LoadJpeg(path),
                ".png" => LoadImage(path, ImageHeaderReader.ReadPng),
                ".pdf" => LoadPdf(path, pageNumber),
                _ => throw new VisualLoadException(VisualLoadError.Unsupported, path, ext),
            };
        }
        catch (VisualLoadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new VisualLoadException(VisualLoadError.Unreadable, path, ex.Message, ex);
        }
    }

    private static VisualInfo LoadJpeg(string path)
    {
        var info = LoadImage(path, ImageHeaderReader.ReadJpeg);
        if (!PdfSharpTakes(path))
            throw new VisualLoadException(VisualLoadError.NeedsConversion, path);
        return info;
    }

    /// <summary>
    /// PDFsharp embeds a JPEG as it is, but refuses one that does not start with a JFIF, Exif or ICC segment ("Unsupported
    /// image format" when the PDF is written; images saved by some web sites and tools). Those start, the others are
    /// tried with PDFsharp itself.
    /// </summary>
    private static bool PdfSharpTakes(string path)
    {
        var start = new byte[4];
        using (var s = File.OpenRead(path))
            s.ReadExactly(start);
        if (start[0] == 0xFF && start[1] == 0xD8 && start[2] == 0xFF && start[3] is 0xE0 or 0xE1 or 0xE2)
            return true;
        try
        {
            using var image = XImage.FromFile(path);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static VisualInfo LoadImage(string path, Func<Stream, ImageHeaderReader.ImageHeader> read)
    {
        ImageHeaderReader.ImageHeader h;
        using (var s = File.OpenRead(path))
            h = read(s);

        var (rotation, mirror) = FromExif(h.ExifOrientation);
        return new VisualInfo
        {
            Path = path,
            Kind = VisualKind.Image,
            FullWidth = h.Width,
            FullHeight = h.Height,
            Content = new RawBox(0, 0, h.Width, h.Height),
            BaseRotation = rotation,
            BaseMirror = mirror,
        };
    }

    private static VisualInfo LoadPdf(string path, int pageNumber)
    {
        using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        if (doc.PageCount == 0 || pageNumber < 1 || pageNumber > doc.PageCount)
            throw new VisualLoadException(VisualLoadError.NoPages, path);

        var page = doc.Pages[pageNumber - 1];
        var media = page.MediaBox;
        var content = new RawBox(0, 0, media.Width, media.Height);

        // A PDF "with bleed" declares its trim area in the TrimBox.
        if (page.Elements.ContainsKey("/TrimBox"))
        {
            var trim = page.TrimBox;
            if (trim.Width > 1 && trim.Height > 1 &&
                trim.X1 >= media.X1 - 0.5 && trim.Y1 >= media.Y1 - 0.5 &&
                trim.X2 <= media.X2 + 0.5 && trim.Y2 <= media.Y2 + 0.5)
            {
                // From PDF coordinates (origin bottom left) to an origin at the top left.
                content = new RawBox(trim.X1 - media.X1, media.Y2 - trim.Y2, trim.Width, trim.Height);
            }
        }

        return new VisualInfo
        {
            Path = path,
            Kind = VisualKind.Pdf,
            FullWidth = media.Width,
            FullHeight = media.Height,
            Content = content,
            BaseRotation = ((page.Rotate % 360) + 360) % 360,
            PageNumber = pageNumber,
            PageCount = doc.PageCount,
        };
    }

    /// <summary>EXIF orientation to (clockwise turn, horizontal mirror applied before the turn).</summary>
    internal static (int Rotation, bool Mirror) FromExif(int orientation) => orientation switch
    {
        2 => (0, true),
        3 => (180, false),
        4 => (180, true),
        5 => (270, true),
        6 => (90, false),
        7 => (90, true),
        8 => (270, false),
        _ => (0, false),
    };
}
