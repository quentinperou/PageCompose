using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace PageCompose.Core.Pdf;

/// <summary>
/// A flattened PDF: each page is one picture of a sheet side (rendered beforehand from the layout PDF), so that the
/// printer driver or the RIP has nothing left to interpret: no transparency, font, overprint or colour space of an
/// imported PDF. The JPEG pictures are embedded as they are.
/// </summary>
public static class FlatPdfWriter
{
    /// <param name="pageImages">JPEG file of each page, in order (front, back, front…).</param>
    /// <param name="sheet">Size of every page: the pictures cover it entirely.</param>
    public static void Write(IReadOnlyList<string> pageImages, SizeMm sheet, Stream output, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(pageImages);
        if (pageImages.Count == 0)
            throw new ArgumentException("No page to write.", nameof(pageImages));

        using var doc = new PdfDocument();
        doc.Info.Title = title ?? "Layout";
        doc.Info.Creator = "PageCompose";

        var images = new List<XImage>(pageImages.Count);
        try
        {
            foreach (var file in pageImages)
            {
                var page = doc.AddPage();
                page.Width = XUnit.FromMillimeter(sheet.Width);
                page.Height = XUnit.FromMillimeter(sheet.Height);

                var image = XImage.FromFile(file);
                images.Add(image);
                using var gfx = XGraphics.FromPdfPage(page);
                gfx.DrawImage(image, 0, 0, page.Width.Point, page.Height.Point);
            }

            doc.Save(output, false);
        }
        finally
        {
            foreach (var image in images) image.Dispose();
        }
    }
}
