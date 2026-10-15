using System.Windows.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage.Streams;

namespace PageCompose.App.Services;

/// <summary>
/// Renders a PDF page to an image with the Windows PDF engine: the preview shows the actual PDF that will be
/// exported, not a simulation. The JPEG export and the flattened PDF use the same rendering.
/// </summary>
internal static class PdfPreviewRenderer
{
    public static async Task<BitmapSource> RenderAsync(byte[] pdf, uint pageIndex, uint widthPx)
    {
        var document = await LoadAsync(pdf);
        return await RenderPageAsync(document, Math.Min(pageIndex, document.PageCount - 1), widthPx);
    }

    public static async Task<PdfDocument> LoadAsync(byte[] pdf)
    {
        var input = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(input.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(pdf);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }

        return await PdfDocument.LoadFromStreamAsync(input);
    }

    /// <summary>
    /// The page on white paper, <paramref name="widthPx"/> wide (the height follows the proportions). For the screen,
    /// it is handed over as a BMP rather than a PNG: the same pixels, without compressing them to read them back at
    /// once (half the time of a render at 3600 px, measured on October 15, 2026); the exports keep the PNG, as always.
    /// </summary>
    public static async Task<BitmapSource> RenderPageAsync(PdfDocument document, uint pageIndex, uint widthPx, bool forScreen = false)
    {
        using var page = document.GetPage(pageIndex);
        using var output = new InMemoryRandomAccessStream();
        var options = new PdfPageRenderOptions { DestinationWidth = widthPx };
        if (forScreen) options.BitmapEncoderId = Windows.Graphics.Imaging.BitmapEncoder.BmpEncoderId;
        await page.RenderToStreamAsync(output, options);
        output.Seek(0);

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = output.AsStreamForRead();
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
