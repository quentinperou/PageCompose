using System.Security.Cryptography;
using System.Text;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace PageCompose.App.Services;

/// <summary>
/// Photos the app does not read itself (iPhone HEIC, WebP, TIFF; <see cref="Core.Visuals.VisualLoader.ConvertedExtensions"/>):
/// Windows decodes them, upright and in sRGB (an iPhone HEIC is in Display P3), and they are saved as a JPEG of
/// quality <see cref="Quality"/>, full size, in the folder of the converted files. Transparent parts become white,
/// as the paper. The JPEG is then read as any photo.
/// </summary>
internal static class PhotoConverter
{
    public const double Quality = 0.95;

    /// <summary>The JPEG made from <paramref name="path"/>, once per version of the file (kept for a day).</summary>
    /// <exception cref="Exception">Windows cannot decode it: a codec missing (HEIC without its extensions) or a damaged file.</exception>
    public static async Task<string> ConvertAsync(string path)
    {
        var source = new FileInfo(path);
        string key = $"{source.FullName}|{source.Length}|{source.LastWriteTimeUtc.Ticks}";
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..12];
        string output = Path.Combine(PdfRasterizer.Folder, $"{Path.GetFileNameWithoutExtension(path)}-{hash}.jpg");
        if (File.Exists(output))
            return output;

        var file = await StorageFile.GetFileFromPathAsync(source.FullName);
        using var input = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(input);
        var pixels = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, new BitmapTransform(),
            ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb);
        byte[] bgra = pixels.DetachPixelData();

        // Over white paper: with premultiplied alpha, each colour plus what the pixel lets through.
        for (int i = 0; i < bgra.Length; i += 4)
        {
            int clear = 255 - bgra[i + 3];
            if (clear == 0) continue;
            bgra[i] = (byte)(bgra[i] + clear);
            bgra[i + 1] = (byte)(bgra[i + 1] + clear);
            bgra[i + 2] = (byte)(bgra[i + 2] + clear);
            bgra[i + 3] = 255;
        }

        using var jpeg = new InMemoryRandomAccessStream();
        var options = new BitmapPropertySet { ["ImageQuality"] = new BitmapTypedValue(Quality, Windows.Foundation.PropertyType.Single) };
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, jpeg, options);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, decoder.OrientedPixelWidth, decoder.OrientedPixelHeight,
            decoder.DpiX, decoder.DpiY, bgra);
        await encoder.FlushAsync();
        jpeg.Seek(0);

        Directory.CreateDirectory(PdfRasterizer.Folder);
        string temp = output + ".tmp";
        using (var fs = File.Create(temp))
            await jpeg.AsStreamForRead().CopyToAsync(fs);
        File.Move(temp, output, overwrite: true);
        return output;
    }
}
