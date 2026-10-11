using System.Windows.Media;
using System.Windows.Media.Imaging;
using PageCompose.Core.Visuals;

namespace PageCompose.App.Services;

/// <summary>Thumbnails of the visuals for the list (decoded small, turned upright according to EXIF).</summary>
internal static class ThumbnailLoader
{
    public static async Task<ImageSource?> LoadAsync(VisualInfo visual, int sizePx)
    {
        try
        {
            if (visual.Kind == VisualKind.Pdf)
            {
                var bytes = await File.ReadAllBytesAsync(visual.Path);
                return await PdfPreviewRenderer.RenderAsync(bytes, (uint)(visual.PageNumber - 1), (uint)sizePx);
            }

            return await Task.Run(() => LoadImage(visual, sizePx));
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException or UnauthorizedAccessException
                                   or System.Runtime.InteropServices.COMException or FileFormatException)
        {
            return null; // a thumbnail is a nicety: nothing stops when it fails
        }
    }

    /// <summary>
    /// The visual as stored in its file, before any turn (EXIF, PDF /Rotate), <paramref name="sizePx"/> wide: the axes
    /// Placement draws it in, for the preview of a drag on the sheet. Null when it cannot be read.
    /// </summary>
    public static async Task<ImageSource?> LoadRawAsync(VisualInfo visual, int sizePx)
    {
        try
        {
            if (visual.Kind == VisualKind.Pdf)
            {
                // The Windows engine renders the page turned as displayed: turned back by its /Rotate.
                var bytes = await File.ReadAllBytesAsync(visual.Path);
                var page = await PdfPreviewRenderer.RenderAsync(bytes, (uint)(visual.PageNumber - 1), (uint)sizePx);
                if (visual.BaseRotation == 0) return page;
                var stored = new TransformedBitmap(page, new RotateTransform(-visual.BaseRotation));
                stored.Freeze();
                return stored;
            }

            return await Task.Run(() =>
            {
                var bitmap = new BitmapImage();
                using (var stream = File.OpenRead(visual.Path))
                {
                    bitmap.BeginInit();
                    bitmap.StreamSource = stream;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = sizePx; // WPF does not apply the EXIF orientation: as stored
                    bitmap.EndInit();
                }
                bitmap.Freeze();
                return (ImageSource)bitmap;
            });
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException or UnauthorizedAccessException
                                   or System.Runtime.InteropServices.COMException or FileFormatException)
        {
            return null;
        }
    }

    private static ImageSource LoadImage(VisualInfo visual, int sizePx)
    {
        var bitmap = new BitmapImage();
        using (var stream = File.OpenRead(visual.Path))
        {
            bitmap.BeginInit();
            bitmap.StreamSource = stream;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = sizePx;
            bitmap.EndInit();
        }

        bitmap.Freeze();
        if (visual.BaseRotation == 0 && !visual.BaseMirror) return bitmap;

        var transform = new TransformGroup();
        if (visual.BaseMirror) transform.Children.Add(new ScaleTransform(-1, 1));
        if (visual.BaseRotation != 0) transform.Children.Add(new RotateTransform(visual.BaseRotation));
        var upright = new TransformedBitmap(bitmap, transform);
        upright.Freeze();
        return upright;
    }
}
