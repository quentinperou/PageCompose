using System.Windows.Media;
using System.Windows.Media.Imaging;
using PageCompose.Core;

namespace PageCompose.App.Services;

/// <summary>
/// Pictures of the sheets, for the JPEG export and the flattened PDF: each page of the layout PDF rendered by Windows
/// at <see cref="Dpi"/> on white paper, saved as a JPEG (quality <see cref="JpegQuality"/>, <see cref="Dpi"/> written
/// in the file so that labs print it at its size).
/// </summary>
internal static class SheetExporter
{
    public const int Dpi = 300;
    public const int JpegQuality = 95;

    /// <summary>Writes page i of <paramref name="pdf"/> to <paramref name="outputs"/>[i], reporting each page done.</summary>
    public static async Task RenderPagesAsync(byte[] pdf, SizeMm sheet, IReadOnlyList<string> outputs, IProgress<int>? progress)
    {
        var document = await PdfPreviewRenderer.LoadAsync(pdf);
        if (document.PageCount != outputs.Count)
            throw new InvalidOperationException($"{document.PageCount} pages for {outputs.Count} files.");

        uint width = (uint)Math.Round(sheet.Width / Units.MmPerInch * Dpi);
        for (int i = 0; i < outputs.Count; i++)
        {
            var bitmap = await PdfPreviewRenderer.RenderPageAsync(document, (uint)i, width);
            string path = outputs[i];
            await Task.Run(() => SaveJpeg(bitmap, path)); // a 300 dpi SRA3 page is 20 Mpx: encoded off the UI thread
            progress?.Report(i + 1);
        }
    }

    private static void SaveJpeg(BitmapSource bitmap, string path)
    {
        // 24-bit (no alpha in a JPEG), with the print resolution in its header.
        var rgb = new FormatConvertedBitmap(bitmap, PixelFormats.Bgr24, null, 0);
        int stride = rgb.PixelWidth * 3;
        var pixels = new byte[stride * rgb.PixelHeight];
        rgb.CopyPixels(pixels, stride, 0);
        var atDpi = BitmapSource.Create(rgb.PixelWidth, rgb.PixelHeight, Dpi, Dpi, PixelFormats.Bgr24, null, pixels, stride);

        var encoder = new JpegBitmapEncoder { QualityLevel = JpegQuality };
        encoder.Frames.Add(BitmapFrame.Create(atDpi));
        using var fs = File.Create(path);
        encoder.Save(fs);
    }
}
