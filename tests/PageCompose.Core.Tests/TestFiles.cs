using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace PageCompose.Core.Tests;

/// <summary>Creates test files (JPEG/PNG through the WPF WIC encoder, PDF through PDFsharp).</summary>
internal sealed class TestFiles : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("apeosprint-tests-").FullName;

    public string Path(string name) => System.IO.Path.Combine(_dir, name);

    /// <summary>Flat JPEG with a red top-left corner (orientation mark) and an optional EXIF orientation.</summary>
    public string Jpeg(int width, int height, int exifOrientation = 1, string name = "visuel.jpg")
    {
        var path = Path(name);
        var encoder = new JpegBitmapEncoder { QualityLevel = 90 };
        BitmapMetadata? metadata = null;
        if (exifOrientation != 1)
        {
            metadata = new BitmapMetadata("jpg");
            metadata.SetQuery("/app1/ifd/{ushort=274}", (ushort)exifOrientation);
        }

        encoder.Frames.Add(BitmapFrame.Create(Bitmap(width, height), null, metadata, null));
        using var fs = File.Create(path);
        encoder.Save(fs);
        return path;
    }

    public string Png(int width, int height, string name = "visuel.png")
    {
        var path = Path(name);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(Bitmap(width, height)));
        using var fs = File.Create(path);
        encoder.Save(fs);
        return path;
    }

    /// <summary>One-page PDF of <paramref name="widthMm"/> x <paramref name="heightMm"/>, with a TrimBox inset by <paramref name="trimInsetMm"/>.</summary>
    /// <param name="pages">Number of pages; page n (from 1) is n millimetres wider than the first, to tell them apart.</param>
    public string Pdf(double widthMm, double heightMm, double trimInsetMm = 0, int rotate = 0, string name = "visuel.pdf", int pages = 1)
    {
        var path = Path(name);
        using var doc = new PdfDocument();
        for (int n = 0; n < pages; n++)
        {
            var page = doc.AddPage();
            page.Width = XUnit.FromMillimeter(widthMm + n);
            page.Height = XUnit.FromMillimeter(heightMm);
            if (trimInsetMm > 0)
            {
                double inset = XUnit.FromMillimeter(trimInsetMm).Point;
                page.TrimBox = new PdfRectangle(new XPoint(inset, inset),
                    new XPoint(page.Width.Point - inset, page.Height.Point - inset));
            }

            if (rotate != 0) page.Rotate = rotate;
            using var gfx = XGraphics.FromPdfPage(page);
            gfx.DrawRectangle(n == 0 ? XBrushes.SteelBlue : XBrushes.IndianRed, 0, 0, page.Width.Point, page.Height.Point);
        }

        doc.Save(path);
        return path;
    }

    private static BitmapSource Bitmap(int width, int height)
    {
        int stride = width * 3;
        var pixels = new byte[stride * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int i = y * stride + x * 3;
            bool marker = x < width / 4 && y < height / 4;
            pixels[i] = marker ? (byte)0 : (byte)200;      // B
            pixels[i + 1] = marker ? (byte)0 : (byte)160;  // G
            pixels[i + 2] = marker ? (byte)230 : (byte)60; // R
        }

        return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr24, null, pixels, stride);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }
}
