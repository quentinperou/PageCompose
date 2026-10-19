using System.Globalization;
using PageCompose.Core.Layout;
using PageCompose.Core.Visuals;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace PageCompose.Core.Pdf;

/// <summary>Texts of the cutting plan, in the language of the app.</summary>
/// <param name="Title">"Cutting plan".</param>
/// <param name="SheetLine">Under the title: "Sheet {0} / {1} · {2} · front side up" (number, count, name of the sheet).</param>
/// <param name="Hint">At the foot of each page: "Cut in the order of the numbers."</param>
/// <param name="FromTop">A measurement from the top edge of the piece: "{0} mm from the top" (also bottom, left, right).</param>
/// <param name="MeasureHint">Under the measurements: what they are measured from.</param>
public sealed record CuttingPlanTexts(string Title, string SheetLine, string Hint,
    string FromTop, string FromBottom, string FromLeft, string FromRight, string MeasureHint);

/// <summary>
/// The cutting plan of a mixed layout, to print on office paper and keep next to the guillotine: one A4 page per
/// sheet, the sheet drawn smaller with its visuals faded and the name of their size, its cuts in red with their
/// numbers in cutting order, and beside it the measurement of each cut (chosen by the author among plans with and
/// without them). At the foot of each page, the icon and the name of the app with its GitHub link (asked by the
/// author). It is a file of its own: never part of the PDF printed on the sheets.
/// </summary>
public static class CuttingPlanWriter
{
    private const double PageWidth = 210, PageHeight = 297, Margin = 12;

    /// <summary>Diameter of a cut number on paper, in millimetres.</summary>
    private const double Disc = 4.6;

    /// <summary>Width of the column of measurements, in millimetres.</summary>
    private const double TableWidth = 58;

    /// <summary>
    /// Height of a measurement line, in millimetres: 5.4 when there is room, never under 4.2 (the 8 pt text). Lines
    /// that do not fit beside the sheet go on to pages of their own (a crash above about 575 cuts until 0.12.1, lines
    /// on top of each other from about 80).
    /// </summary>
    private const double Row = 5.4, MinRow = 4.2, HintHeight = 14;

    private const double ContentTop = Margin + 20, ContentBottom = PageHeight - Margin - 9;

    private static readonly XColor Red = XColor.FromArgb(0xC4, 0x2B, 0x1C);
    private static readonly XColor Ink = XColor.FromArgb(0x20, 0x20, 0x20);
    private static readonly XColor Grey = XColor.FromArgb(0x6B, 0x6B, 0x6B);

    /// <param name="itemLabels">Name of the size of each item of the packing (by index), written on its cells.</param>
    /// <param name="sheetName">Name of the sheet ("SRA3 (320 × 450)").</param>
    /// <param name="jobName">Name of the job (the exported PDF), at the top right of each page; null for none.</param>
    /// <param name="logoPng">Icon of the app (PNG), at the foot of each page beside its name and its GitHub link.</param>
    public static void Write(MixedPlan plan, IReadOnlyList<string> itemLabels, string sheetName, string? jobName,
        CuttingPlanTexts texts, Stream output, FitMode fit = FitMode.Fill, byte[]? logoPng = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(texts);
        if (plan.SheetCount == 0)
            throw new ArgumentException("No sheet to write.", nameof(plan));
        GlobalFontSettings.FontResolver ??= new WindowsFonts();

        using var doc = new PdfDocument();
        doc.Info.Title = jobName is null ? texts.Title : $"{texts.Title} · {jobName}";
        doc.Info.Creator = "PageCompose";
        doc.Options.CompressContentStreams = true;

        // One XImage per visual, as in the layout PDF.
        var images = new Dictionary<VisualInfo, XImage>();
        XImage? logo = logoPng is null ? null : XImage.FromStream(new MemoryStream(logoPng));
        try
        {
            XImage ImageOf(VisualInfo v)
            {
                if (!images.TryGetValue(v, out var image))
                    images[v] = image = ImpositionPdfWriter.Open(v);
                return image;
            }

            for (int s = 0; s < plan.SheetCount; s++)
            {
                // The sheet and the measurements that fit beside it, then the others on pages of their own.
                int cuts = plan.Sheets[s].Layout.CutSegments.Count, next = 0;
                bool first = true;
                do
                {
                    var page = doc.AddPage();
                    page.Width = XUnit.FromMillimeter(PageWidth);
                    page.Height = XUnit.FromMillimeter(PageHeight);
                    using var gfx = XGraphics.FromPdfPage(page);
                    next = first
                        ? DrawPage(gfx, plan, s, itemLabels, sheetName, jobName, texts, ImageOf, fit)
                        : DrawMoreMeasurements(gfx, plan, s, sheetName, jobName, texts, next);
                    DrawBrand(gfx, page, logo);
                    first = false;
                }
                while (next < cuts);
            }

            doc.Save(output, false);
        }
        finally
        {
            foreach (var image in images.Values) image.Dispose();
            logo?.Dispose();
        }
    }

    /// <summary>At the foot of the page, on the right: the icon, "PageCompose" and the address of its GitHub page, a link.</summary>
    private static void DrawBrand(XGraphics gfx, PdfPage page, XImage? logo)
    {
        const string name = "PageCompose";
        string url = $"https://github.com/{UpdateFeed.Repository}";
        string shown = $"github.com/{UpdateFeed.Repository}";
        var nameFont = new XFont("Segoe UI", 8, XFontStyleEx.Bold);
        var urlFont = new XFont("Segoe UI", 7.5);
        const double height = 5, icon = 4.4, space = 1.6;

        // From the right margin leftwards: the address, the name, the icon.
        double y = PageHeight - Margin - height, right = PageWidth - Margin;
        double urlWidth = Units.PtToMm(gfx.MeasureString(shown, urlFont).Width);
        double nameWidth = Units.PtToMm(gfx.MeasureString(name, nameFont).Width);
        double urlLeft = right - urlWidth, nameLeft = urlLeft - 2 * space - nameWidth;
        double left = logo is null ? nameLeft : nameLeft - space - icon;

        gfx.DrawString(shown, urlFont, new XSolidBrush(Grey), Box(urlLeft, y, urlWidth, height), XStringFormats.CenterLeft);
        gfx.DrawString(name, nameFont, new XSolidBrush(Ink), Box(nameLeft, y, nameWidth, height), XStringFormats.CenterLeft);
        if (logo is not null)
            gfx.DrawImage(logo, Box(left, y + (height - icon) / 2, icon, icon));

        // The link area, in PDF coordinates (from the bottom of the page).
        page.AddWebLink(new PdfRectangle(new XPoint(Pt(left), Pt(PageHeight - y - height)), new XPoint(Pt(right), Pt(PageHeight - y))), url);
    }

    // Title, sheet, job.
    private static void DrawHeader(XGraphics gfx, MixedPlan plan, int index, string sheetName, string? jobName, CuttingPlanTexts texts)
    {
        var titleFont = new XFont("Segoe UI", 16, XFontStyleEx.Bold);
        var lineFont = new XFont("Segoe UI", 9.5);
        var smallFont = new XFont("Segoe UI", 8);
        gfx.DrawString(texts.Title, titleFont, new XSolidBrush(Ink), Box(Margin, Margin, 120, 8), XStringFormats.TopLeft);
        gfx.DrawString(string.Format(CultureInfo.CurrentCulture, texts.SheetLine, index + 1, plan.SheetCount, sheetName),
            lineFont, new XSolidBrush(Grey), Box(Margin, Margin + 8.5, 186, 5), XStringFormats.TopLeft);
        if (jobName is not null)
            gfx.DrawString(jobName, smallFont, new XSolidBrush(Grey), Box(Margin + 66, Margin + 1.5, 120, 5), XStringFormats.TopRight);
        gfx.DrawLine(new XPen(XColor.FromArgb(0xD0, 0xD0, 0xD0), 0.5), Pt(Margin), Pt(Margin + 16.5), Pt(PageWidth - Margin), Pt(Margin + 16.5));
    }

    /// <summary>The page of a sheet; returns the first cut whose measurement did not fit beside it.</summary>
    private static int DrawPage(XGraphics gfx, MixedPlan plan, int index, IReadOnlyList<string> itemLabels, string sheetName,
        string? jobName, CuttingPlanTexts texts, Func<VisualInfo, XImage> imageOf, FitMode fit)
    {
        var sheet = plan.Sheets[index];
        var layout = sheet.Layout;
        var cuts = layout.CutSegments;
        var smallFont = new XFont("Segoe UI", 8);
        DrawHeader(gfx, plan, index, sheetName, jobName, texts);

        // The sheet, as large as the page allows beside the measurements.
        double top = ContentTop, bottom = ContentBottom;
        double areaWidth = PageWidth - 2 * Margin - TableWidth - 6, areaHeight = bottom - top;
        double scale = Math.Min(areaWidth / layout.Sheet.Width, areaHeight / layout.Sheet.Height);
        double ox = Margin, oy = top;
        double X(double x) => ox + x * scale;
        double Y(double y) => oy + y * scale;

        var paper = Box(ox, oy, layout.Sheet.Width * scale, layout.Sheet.Height * scale);
        gfx.DrawRectangle(new XPen(XColor.FromArgb(0xA0, 0xA0, 0xA0), 0.5), XBrushes.White, paper);

        // The visuals as printed, faded so that the cuts stand out.
        var state = gfx.Save();
        gfx.TranslateTransform(Pt(ox), Pt(oy));
        gfx.ScaleTransform(scale);
        ImpositionPdfWriter.DrawVisuals(gfx, layout, layout.Cells.Select((c, i) => (c, sheet.Visuals[i])), imageOf, fit, SideOptions.Recto);
        gfx.Restore(state);
        if (layout.Block is { } block)
            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(150, 255, 255, 255)),
                Box(X(block.X), Y(block.Y), block.Width * scale, block.Height * scale));

        // The size of each visual on it, small enough to leave room for the numbers on its edges.
        var outline = new XPen(XColor.FromArgb(0x80, 0x80, 0x80), 0.3);
        foreach (var (cell, i) in layout.Cells.Select((c, i) => (c, i)))
        {
            var trim = Box(X(cell.Trim.X), Y(cell.Trim.Y), cell.Trim.Width * scale, cell.Trim.Height * scale);
            gfx.DrawRectangle(outline, trim);
            if (sheet.Items[i] >= itemLabels.Count) continue;
            string label = itemLabels[sheet.Items[i]];
            double room = Pt(cell.Trim.Width * scale - 2.4 * Disc), size = 7.5;
            double width = gfx.MeasureString(label, new XFont("Segoe UI", size, XFontStyleEx.Bold)).Width;
            if (width > room) size = Math.Max(4.5, size * room / width);
            gfx.DrawString(label, new XFont("Segoe UI", size, XFontStyleEx.Bold), new XSolidBrush(Ink), trim, XStringFormats.Center);
        }

        // The cuts, then their numbers in cutting order.
        var cutPen = new XPen(Red, 0.8);
        foreach (var c in cuts)
            gfx.DrawLine(cutPen, Pt(X(c.X1)), Pt(Y(c.Y1)), Pt(X(c.X2)), Pt(Y(c.Y2)));
        var at = MixedPacker.NumberPositions(layout, Disc / scale);
        for (int i = 0; i < cuts.Count; i++)
            DrawNumber(gfx, i + 1, X(at[i].X), Y(at[i].Y), Disc);

        int next = DrawMeasurements(gfx, layout, texts, ox + areaWidth + 6, top, areaHeight, 0, hint: true);

        gfx.DrawString(texts.Hint, smallFont, new XSolidBrush(Grey), Box(Margin, PageHeight - Margin - 5, 120, 5), XStringFormats.CenterLeft);
        return next;
    }

    /// <summary>
    /// The measurements of a sheet that did not fit beside it, from cut <paramref name="first"/>, in columns across
    /// the page; returns the first one left for the next page.
    /// </summary>
    private static int DrawMoreMeasurements(XGraphics gfx, MixedPlan plan, int index, string sheetName, string? jobName,
        CuttingPlanTexts texts, int first)
    {
        var layout = plan.Sheets[index].Layout;
        DrawHeader(gfx, plan, index, sheetName, jobName, texts);
        int next = first;
        for (double left = Margin; left + TableWidth <= PageWidth - Margin + 1e-9 && next < layout.CutSegments.Count; left += TableWidth + 6)
            next = DrawMeasurements(gfx, layout, texts, left, ContentTop, ContentBottom - ContentTop, next, hint: false);
        return next;
    }

    // One line per cut, from cut first: its number, then where it falls in its piece; as many as fit in the height
    // (with the hint under them on the page of the sheet). Returns the first cut left.
    private static int DrawMeasurements(XGraphics gfx, SheetLayout layout, CuttingPlanTexts texts, double left, double top, double height,
        int first, bool hint)
    {
        var font = new XFont("Segoe UI", 8);
        var hintFont = new XFont("Segoe UI", 7);
        int remaining = layout.CutSegments.Count - first;
        double usable = height - (hint ? HintHeight : 0);
        double row = remaining <= 0 ? 0 : Math.Max(MinRow, Math.Min(Row, usable / remaining));
        int count = row == 0 ? 0 : Math.Min(remaining, (int)Math.Floor(usable / row + 1e-9));
        double disc = Math.Min(Disc, row - 0.4);

        for (int k = 0; k < count; k++)
        {
            int i = first + k;
            double y = top + k * row;
            var (mm, edge) = layout.CutMeasurement(i);
            DrawNumber(gfx, i + 1, left + Disc / 2, y + row / 2, disc);
            string format = edge switch
            {
                CutEdge.Top => texts.FromTop,
                CutEdge.Bottom => texts.FromBottom,
                CutEdge.Left => texts.FromLeft,
                _ => texts.FromRight,
            };
            string text = string.Format(CultureInfo.CurrentCulture, format, mm.ToString("0.0", CultureInfo.CurrentCulture));
            gfx.DrawString(text, font, new XSolidBrush(Ink), Box(left + Disc + 2.5, y, TableWidth - Disc - 2.5, row), XStringFormats.CenterLeft);
        }

        if (hint)
        {
            var formatter = new XTextFormatter(gfx);
            formatter.DrawString(texts.MeasureHint, hintFont, new XSolidBrush(Grey),
                Box(left, top + count * row + 2, TableWidth, HintHeight), XStringFormats.TopLeft);
        }
        return first + count;
    }

    // A white disc ringed in red, with the number in red, as on the preview of the app.
    private static void DrawNumber(XGraphics gfx, int number, double x, double y, double diameter)
    {
        var disc = Box(x - diameter / 2, y - diameter / 2, diameter, diameter);
        gfx.DrawEllipse(new XPen(Red, 0.6), XBrushes.White, disc);
        var font = new XFont("Segoe UI", diameter * (number < 10 ? 1.45 : 1.2), XFontStyleEx.Bold);
        gfx.DrawString(number.ToString(CultureInfo.InvariantCulture), font, new XSolidBrush(Red), disc, XStringFormats.Center);
    }

    private static double Pt(double mm) => Units.MmToPt(mm);

    private static XRect Box(double x, double y, double width, double height) => new(Pt(x), Pt(y), Pt(width), Pt(height));
}
