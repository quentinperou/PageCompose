using System.Security.Cryptography;
using System.Text;
using Windows.Data.Pdf;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace PageCompose.App.Services;

/// <summary>
/// Last resort for a PDF that PDFsharp cannot read (non-standard files, often encrypted ones from billing software,
/// that Acrobat and Windows tolerate): the Windows PDF engine renders its first page to a PNG image at
/// <see cref="Dpi"/>, which is then used as an image visual. Vector quality and TrimBox are lost.
/// </summary>
internal static class PdfRasterizer
{
    public const int Dpi = 300;

    // A0 at 300 dpi is about 14000 px: beyond this, the resolution goes down instead.
    private const double MaxSidePx = 12000;

    /// <summary>The converted files of the app (also the photos of <see cref="PhotoConverter"/>).</summary>
    internal static readonly string Folder = Path.Combine(Path.GetTempPath(), "PageCompose", "Converted");

    /// <summary>
    /// Returns the PNG of a page (from 1) and the number of pages of the file. The PNG is made once per file version
    /// and page (kept until the next start).
    /// </summary>
    public static async Task<(string Png, int PageCount)> RenderPageAsync(string pdfPath, int pageNumber)
    {
        var source = new FileInfo(pdfPath);
        var file = await StorageFile.GetFileFromPathAsync(source.FullName);
        var document = await PdfDocument.LoadFromFileAsync(file);
        int count = (int)document.PageCount;
        if (pageNumber < 1 || pageNumber > count)
            throw new InvalidDataException($"The PDF has no page {pageNumber}.");

        string key = $"{source.FullName}|{source.Length}|{source.LastWriteTimeUtc.Ticks}|{pageNumber}";
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..12];
        string output = Path.Combine(Folder, $"{Path.GetFileNameWithoutExtension(pdfPath)}-p{pageNumber}-{hash}.png");
        if (File.Exists(output))
            return (Reused(output), count);

        using var page = document.GetPage((uint)(pageNumber - 1));
        // Size is in DIPs (1/96 in), the page turned as it is displayed. Only the width is given: the engine keeps
        // the proportions.
        double scale = Math.Min(Dpi / 96.0, MaxSidePx / Math.Max(page.Size.Width, page.Size.Height));
        var options = new PdfPageRenderOptions
        {
            DestinationWidth = (uint)Math.Max(1, Math.Round(page.Size.Width * scale)),
            BitmapEncoderId = BitmapEncoder.PngEncoderId,
        };

        using var image = new InMemoryRandomAccessStream();
        await page.RenderToStreamAsync(image, options);
        image.Seek(0);

        Directory.CreateDirectory(Folder);
        string temp = output + ".tmp";
        using (var fs = File.Create(temp))
            await image.AsStreamForRead().CopyToAsync(fs);
        File.Move(temp, output, overwrite: true);
        return (output, count);
    }

    /// <summary>
    /// A converted image used again: kept a day more, since another window starting deletes those older than a day.
    /// </summary>
    internal static string Reused(string path)
    {
        try { File.SetLastWriteTimeUtc(path, DateTime.UtcNow); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return path;
    }

    /// <summary>
    /// Converted images are only used during a run. At startup, those older than a day go: not all of them, since
    /// another window of the app may be using its own.
    /// </summary>
    public static void DeleteOldFiles()
    {
        if (!Directory.Exists(Folder)) return;
        var limit = DateTime.UtcNow.AddDays(-1);
        foreach (var file in Directory.EnumerateFiles(Folder))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < limit)
                    File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // In use: it will go next time.
            }
        }
    }
}
