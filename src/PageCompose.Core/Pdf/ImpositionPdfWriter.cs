using PageCompose.Core.Layout;
using PageCompose.Core.Visuals;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace PageCompose.Core.Pdf;

/// <param name="Layout">Computed layout.</param>
/// <param name="Sheets">Visual of each cell, sheet by sheet (see <see cref="SheetFiller"/>); null = empty cell.</param>
/// <param name="Verso">Back shared by every filled cell (null = single-sided).</param>
/// <param name="CutLines">Thin lines along every cut, across the whole block, on the front (crop marks or not).</param>
/// <param name="VersoMargins">The back keeps the white margins of the front (mirrored with the flip).</param>
public sealed record ImpositionJob(
    SheetLayout Layout,
    IReadOnlyList<VisualInfo?[]> Sheets,
    VisualInfo? Verso = null,
    FitMode Fit = FitMode.Fill,
    DuplexFlip Flip = DuplexFlip.LongEdge,
    bool RotateVerso180 = false,
    string? Title = null,
    bool CutLines = false,
    bool VersoMargins = false)
{
    /// <summary>The same visual in every cell of <paramref name="sheets"/> sheets.</summary>
    public static ImpositionJob Repeat(SheetLayout layout, VisualInfo visual, int sheets, VisualInfo? verso = null,
        FitMode fit = FitMode.Fill) =>
        new(layout,
            Enumerable.Range(0, sheets).Select(_ => Enumerable.Repeat<VisualInfo?>(visual, layout.PerSheet).ToArray()).ToList(),
            verso, fit);
}

/// <summary>
/// Writes the layout PDF: one page per sheet (front then back, alternating, when there is a back).
/// Pages are exactly the size of the sheet. Each visual is stored once in the file, and JPEG files are
/// embedded as they are (no recompression).
/// </summary>
public static class ImpositionPdfWriter
{
    /// <summary>Crop mark line width, in points.</summary>
    public const double CropMarkWidthPt = 0.3;

    public static void Write(ImpositionJob job, string path)
    {
        using var fs = File.Create(path);
        Write(job, fs);
    }

    public static void Write(ImpositionJob job, Stream output)
    {
        ArgumentNullException.ThrowIfNull(job);
        var layout = job.Layout;
        if (layout.IsEmpty)
            throw new InvalidOperationException("The visual size does not fit on this sheet.");
        if (job.Sheets.Count == 0)
            throw new ArgumentException("No sheet to write.", nameof(job));
        if (job.Sheets.Any(s => s.Length != layout.PerSheet))
            throw new ArgumentException("Each sheet must have as many cells as the layout.", nameof(job));

        using var doc = new PdfDocument();
        var first = job.Sheets.SelectMany(s => s).FirstOrDefault(v => v is not null);
        doc.Info.Title = job.Title ?? first?.FileName ?? "Layout";
        doc.Info.Creator = "PageCompose";
        doc.Options.CompressContentStreams = true;

        // One XImage per visual: PDFsharp then stores the image only once.
        var images = new Dictionary<VisualInfo, XImage>();
        try
        {
            XImage ImageOf(VisualInfo v)
            {
                if (!images.TryGetValue(v, out var image))
                    images[v] = image = Open(v);
                return image;
            }

            var versoSide = new SideOptions(true, job.Flip, job.RotateVerso180, job.VersoMargins);
            foreach (var sheet in job.Sheets)
            {
                var recto = layout.Cells.Select((cell, i) => (cell, visual: sheet[i])).ToList();
                DrawSide(doc, layout, recto, ImageOf, job.Fit, SideOptions.Recto, job.CutLines);

                if (job.Verso is { } verso)
                {
                    // The back is only printed behind cells that have a front.
                    var back = recto.Select(r => (r.cell, visual: r.visual is null ? null : verso)).ToList();
                    DrawSide(doc, layout, back, ImageOf, job.Fit, versoSide, cutLines: false);
                }
            }

            doc.Save(output, false);
        }
        finally
        {
            foreach (var image in images.Values) image.Dispose();
        }
    }

    private static XImage Open(VisualInfo visual)
    {
        if (visual.Kind == VisualKind.Pdf)
        {
            var form = XPdfForm.FromFile(visual.Path);
            form.PageNumber = visual.PageNumber;
            return form;
        }

        return XImage.FromFile(visual.Path);
    }

    private static void DrawSide(PdfDocument doc, SheetLayout layout, List<(LayoutCell cell, VisualInfo? visual)> cells,
        Func<VisualInfo, XImage> imageOf, FitMode fit, SideOptions side, bool cutLines)
    {
        var page = doc.AddPage();
        page.Width = XUnit.FromMillimeter(layout.Sheet.Width);
        page.Height = XUnit.FromMillimeter(layout.Sheet.Height);

        using var gfx = XGraphics.FromPdfPage(page);
        foreach (var (layoutCell, visual) in cells)
        {
            if (visual is null) continue;
            var cell = side.IsVerso ? Placement.MirrorForVerso(layoutCell, layout.Sheet, side.Flip) : layoutCell;
            var p = Placement.Compute(visual, cell, fit, side);

            var state = gfx.Save();
            gfx.IntersectClip(ToPt(p.Clip));
            gfx.TranslateTransform(Units.MmToPt(p.CenterX), Units.MmToPt(p.CenterY));
            if (p.AngleDeg != 0) gfx.RotateTransform(p.AngleDeg);
            if (p.Mirror) gfx.ScaleTransform(-1, 1);
            gfx.DrawImage(imageOf(visual), ToPt(p.DrawRect));
            gfx.Restore(state);
        }

        // A space wider than the bleed leaves a white band between two visuals. Painting it white changes nothing on
        // paper, but Windows (preview, JPG export, flattened PDF) rounds clipping paths to whole pixels: a band of one
        // or two pixels (0.1 mm at 300 dpi) would vanish between some columns or rows and not others.
        if (layout.Options.SpacingMm > 2 * layout.Options.BleedMm)
            PaintSpaces(gfx, side.IsVerso
                ? [.. layout.Cells.Select(c => Placement.MirrorForVerso(c, layout.Sheet, side.Flip))]
                : layout.Cells);

        // Crop marks on the front only: that is the side facing up on the guillotine.
        var pen = new XPen(XColors.Black, CropMarkWidthPt);
        if (!side.IsVerso && layout.CropMarks.Count > 0)
        {
            foreach (var m in layout.CropMarks)
                gfx.DrawLine(pen, Units.MmToPt(m.X1), Units.MmToPt(m.Y1), Units.MmToPt(m.X2), Units.MmToPt(m.Y2));
        }

        // Continuous cut lines over the visuals, from one side of the block of trims to the other. The cells form a
        // grid: a line along the edge of one cell runs along the edges of its whole row or column.
        if (cutLines && layout.Cells.Count > 0)
        {
            double top = layout.Cells[0].Trim.Y, bottom = layout.Cells[^1].Trim.Bottom;
            double left = layout.Cells[0].Trim.X, right = layout.Cells[^1].Trim.Right;
            foreach (var x in layout.CutsX)
                gfx.DrawLine(pen, Units.MmToPt(x), Units.MmToPt(top), Units.MmToPt(x), Units.MmToPt(bottom));
            foreach (var y in layout.CutsY)
                gfx.DrawLine(pen, Units.MmToPt(left), Units.MmToPt(y), Units.MmToPt(right), Units.MmToPt(y));
        }
    }

    /// <summary>Fills the bands between the columns and between the rows of cells (bleed included) in white.</summary>
    private static void PaintSpaces(XGraphics gfx, IReadOnlyList<LayoutCell> cells)
    {
        if (cells.Count == 0) return;
        var columns = cells.GroupBy(c => c.Column)
            .Select(g => (Start: g.Min(c => c.Bleed.X), End: g.Max(c => c.Bleed.Right))).OrderBy(c => c.Start).ToList();
        var rows = cells.GroupBy(c => c.Row)
            .Select(g => (Start: g.Min(c => c.Bleed.Y), End: g.Max(c => c.Bleed.Bottom))).OrderBy(r => r.Start).ToList();
        double left = columns[0].Start, right = columns[^1].End, top = rows[0].Start, bottom = rows[^1].End;

        for (int i = 1; i < columns.Count; i++)
            if (columns[i].Start > columns[i - 1].End)
                gfx.DrawRectangle(XBrushes.White, ToPt(new RectMm(columns[i - 1].End, top, columns[i].Start - columns[i - 1].End, bottom - top)));
        for (int i = 1; i < rows.Count; i++)
            if (rows[i].Start > rows[i - 1].End)
                gfx.DrawRectangle(XBrushes.White, ToPt(new RectMm(left, rows[i - 1].End, right - left, rows[i].Start - rows[i - 1].End)));
    }

    private static XRect ToPt(RectMm r) =>
        new(Units.MmToPt(r.X), Units.MmToPt(r.Y), Units.MmToPt(r.Width), Units.MmToPt(r.Height));
}
