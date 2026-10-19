using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PageCompose.Core.Visuals;

namespace PageCompose.App.Services;

/// <summary>
/// "Fast preview" (a setting, on by default, asked by the author): lighter copies of the photos, for the preview only.
/// The Windows PDF engine decodes every photo of the sheet at its full size at each refresh (about a second for 8
/// photos of 12 MP, measured on October 15, 2026); a copy of the size the screen shows takes a tenth of it. The exports,
/// the cutting plan and the projects never come here: they always take the original files.
/// <para>
/// A copy is named by a fingerprint of the content of its file (its path, size and date, its first and last 64 KB), not
/// by its name: another photo saved under the same name gets its own copy. In %TEMP%\PageCompose\Preview, deleted at
/// startup once a day old (another window may be using its own).
/// </para>
/// </summary>
internal static class PreviewImages
{
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), "PageCompose", "Preview");

    /// <summary>The long sides made, in pixels: a few sizes only, so that a copy serves at several zooms.</summary>
    private static readonly int[] Sizes = [1024, 1536, 2048, 3072, 4096, 6144];

    private const int JpegQuality = 90;
    private const int SampleBytes = 64 * 1024;

    // A copy being made is not made twice (two lines of the same photo, a refresh while it is made).
    private static readonly ConcurrentDictionary<string, Lazy<Task<string?>>> Making = new();

    /// <summary>
    /// A copy of the photo with at least <paramref name="neededPx"/> pixels on its long side, made the first time; null
    /// when the photo is not much larger itself, for a PDF, or when no copy can be made: the original is drawn then.
    /// </summary>
    public static async Task<string?> GetAsync(VisualInfo visual, double neededPx)
    {
        if (visual.Kind != VisualKind.Image) return null; // a PDF stays vector
        int size = Sizes.FirstOrDefault(s => s >= neededPx);
        if (size == 0 || size >= 0.8 * Math.Max(visual.FullWidth, visual.FullHeight)) return null;

        bool png = Path.GetExtension(visual.Path).Equals(".png", StringComparison.OrdinalIgnoreCase); // its transparency kept
        if (await Task.Run(() => Fingerprint(visual.Path, size)) is not { } key) return null;
        string target = Path.Combine(Folder, key + (png ? ".png" : ".jpg"));
        var making = Making.GetOrAdd(target, _ => new Lazy<Task<string?>>(() => Task.Run(() => Make(visual, target, size, png))));
        return await making.Value;
    }

    private static string? Fingerprint(string path, int size)
    {
        try
        {
            var file = new FileInfo(path);
            using var stream = file.OpenRead();
            var head = new byte[Math.Min(SampleBytes, file.Length)];
            stream.ReadExactly(head);
            var tail = new byte[Math.Min(SampleBytes, file.Length)];
            stream.Seek(-tail.Length, SeekOrigin.End);
            stream.ReadExactly(tail);

            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            sha.AppendData(Encoding.UTF8.GetBytes($"{file.FullName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}|{size}"));
            sha.AppendData(head);
            sha.AppendData(tail);
            return Convert.ToHexString(sha.GetHashAndReset())[..32];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? Make(VisualInfo visual, string target, int size, bool png)
    {
        try
        {
            if (File.Exists(target))
            {
                File.SetLastWriteTimeUtc(target, DateTime.UtcNow); // used again: kept a day more
                return target;
            }

            // Decoded straight to the size wanted (the JPEG decoder skips most of the work), as stored: the turn of
            // its EXIF is applied by the layout, as for the original. Its colours as they are in the file, as the PDF
            // engine reads them in the original.
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(visual.Path);
            image.CacheOption = BitmapCacheOption.OnLoad;
            // IgnoreImageCache: a photo retouched and saved over itself under the same name is read again.
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.IgnoreImageCache;
            if (visual.FullWidth >= visual.FullHeight) image.DecodePixelWidth = size;
            else image.DecodePixelHeight = size;
            image.EndInit();
            image.Freeze();
            if (image.Format == PixelFormats.Cmyk32) return null; // a CMYK JPEG written again could change its colours

            BitmapEncoder encoder = png ? new PngBitmapEncoder() : new JpegBitmapEncoder { QualityLevel = JpegQuality };
            encoder.Frames.Add(BitmapFrame.Create(image));
            Directory.CreateDirectory(Folder);
            string temp = target + ".tmp";
            using (var fs = File.Create(temp))
                encoder.Save(fs);
            File.Move(temp, target, overwrite: true); // never a half-written copy
            return target;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null; // the original is drawn: slower, never wrong
        }
    }

    /// <summary>At startup, the copies not used for a day (another window of the app may be using its own).</summary>
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
