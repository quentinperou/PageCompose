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

    /// <summary>The page on white paper, <paramref name="widthPx"/> wide (the height follows the proportions).</summary>
    public static async Task<BitmapSource> RenderPageAsync(PdfDocument document, uint pageIndex, uint widthPx)
    {
        using var page = document.GetPage(pageIndex);
        using var output = new InMemoryRandomAccessStream();
        await page.RenderToStreamAsync(output, new PdfPageRenderOptions { DestinationWidth = widthPx });
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
