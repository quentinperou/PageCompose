using PageCompose.Core.Layout;
using PageCompose.Core.Visuals;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace PageCompose.Core.Pdf;

/// <param name="Layout">Computed layout.</param>
/// <param name="Sheets">Visual of each cell, sheet by sheet (see <see cref="SheetFiller"/>); null = empty cell.</param>
/// <param name="Verso">Back shared by every filled cell (null = single-sided).</param>
/// <param name="CutLines">
/// Thin lines along every cut, on the front (crop marks or not): across the whole block of a grid, along each cut of
/// a mixed sheet.
/// </param>
/// <param name="VersoMargins">The back keeps the white margins of the front (mirrored with the flip).</param>
/// <param name="SheetLayouts">Layout of each sheet, when each has its own (mixed sizes); else every sheet uses <paramref name="Layout"/>.</param>
/// <param name="Backs">
/// The back of each cell, sheet by sheet, when visuals have their own (null in a cell: <paramref name="Verso"/>, the
/// shared back, if any).
/// </param>
/// <param name="Marks">The mark of each sheet, on its front outside the block (see <see cref="SheetMark"/>); null: none.</param>
public sealed record ImpositionJob(
    SheetLayout Layout,
    IReadOnlyList<VisualInfo?[]> Sheets,
    VisualInfo? Verso = null,
    FitMode Fit = FitMode.Fill,
    DuplexFlip Flip = DuplexFlip.LongEdge,
    bool RotateVerso180 = false,
    string? Title = null,
    bool CutLines = false,
    bool VersoMargins = false,
    IReadOnlyList<SheetLayout>? SheetLayouts = null,
    IReadOnlyList<VisualInfo?[]>? Backs = null,
    IReadOnlyList<string>? Marks = null)
{
    /// <summary>Layout of sheet <paramref name="index"/> (from 0).</summary>
    public SheetLayout LayoutOf(int index) => SheetLayouts?[index] ?? Layout;

    /// <summary>The sheets have a back: a shared one, or the own back of some visuals.</summary>
    public bool HasBack => Verso is not null || (Backs?.Any(sheet => sheet.Any(v => v is not null)) ?? false);

    /// <summary>The back of a cell: its own, else the shared one (null: nothing behind it).</summary>
    public VisualInfo? BackOf(int sheet, int cell) =>
        Backs is { } backs && sheet < backs.Count && cell < backs[sheet].Length && backs[sheet][cell] is { } own ? own : Verso;

    /// <summary>The sheets of a mixed layout, as they are or only some of them (indices, from 0).</summary>
    public static ImpositionJob Mixed(MixedPlan plan, IEnumerable<int>? sheets = null)
    {
        var chosen = (sheets ?? Enumerable.Range(0, plan.SheetCount)).Select(i => plan.Sheets[i]).ToList();
        if (chosen.Count == 0)
            throw new ArgumentException("No sheet to write.", nameof(plan));
        return new ImpositionJob(chosen[0].Layout, chosen.Select(s => s.Visuals).ToList(), CutLines: true,
            SheetLayouts: chosen.Select(s => s.Layout).ToList());
    }

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
        if (job.Layout.IsEmpty)
            throw new InvalidOperationException("The visual size does not fit on this sheet.");
        if (job.Sheets.Count == 0)
            throw new ArgumentException("No sheet to write.", nameof(job));
        if (job.SheetLayouts is { } own && (own.Count != job.Sheets.Count || own.Any(l => l.IsEmpty)))
            throw new ArgumentException("Each sheet needs its own layout, with cells.", nameof(job));
        if (job.Sheets.Select((s, i) => s.Length != job.LayoutOf(i).PerSheet).Any(wrong => wrong))
            throw new ArgumentException("Each sheet must have as many cells as the layout.", nameof(job));
        if (job.Backs is { } backs && (backs.Count != job.Sheets.Count || backs.Where((b, i) => b.Length != job.Sheets[i].Length).Any()))
            throw new ArgumentException("The backs must match the sheets, cell by cell.", nameof(job));
        if (job.Marks is { } marks && marks.Count != job.Sheets.Count)
            throw new ArgumentException("One mark per sheet.", nameof(job));

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
            for (int s = 0; s < job.Sheets.Count; s++)
            {
                var (layout, sheet) = (job.LayoutOf(s), job.Sheets[s]);
                var recto = layout.Cells.Select((cell, i) => (cell, visual: sheet[i])).ToList();
                DrawSide(doc, layout, recto, ImageOf, job.Fit, SideOptions.Recto, job.CutLines, job.Marks?[s]);

                if (job.HasBack)
                {
                    // The back is only printed behind cells that have a front: their own back, else the shared one.
                    var back = recto.Select((r, i) => (r.cell, visual: r.visual is null ? null : job.BackOf(s, i))).ToList();
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

    internal static XImage Open(VisualInfo visual)
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
        Func<VisualInfo, XImage> imageOf, FitMode fit, SideOptions side, bool cutLines, string? mark = null)
    {
        var page = doc.AddPage();
        page.Width = XUnit.FromMillimeter(layout.Sheet.Width);
        page.Height = XUnit.FromMillimeter(layout.Sheet.Height);

        using var gfx = XGraphics.FromPdfPage(page);
        DrawVisuals(gfx, layout, cells, imageOf, fit, side);

        // A space wider than the bleed leaves a white band between two visuals. Painting it white changes nothing on
        // paper, but Windows (preview, JPG export, flattened PDF) rounds clipping paths to whole pixels: a band of one
        // or two pixels (0.1 mm at 300 dpi) would vanish between some columns or rows and not others.
        if (layout.IsMixed)
        {
            foreach (var space in layout.Spaces)
                gfx.DrawRectangle(XBrushes.White, ToPt(side.IsVerso ? MirrorForVerso(space, layout.Sheet, side.Flip) : space));
        }
        else if (layout.Options.SpacingMm > 2 * layout.Options.BleedMm)
        {
            PaintSpaces(gfx, side.IsVerso
                ? [.. layout.Cells.Select(c => Placement.MirrorForVerso(c, layout.Sheet, side.Flip))]
                : layout.Cells);
        }

        // Crop marks on the front only: that is the side facing up on the guillotine.
        var pen = new XPen(XColors.Black, CropMarkWidthPt);
        if (!side.IsVerso && layout.CropMarks.Count > 0)
        {
            foreach (var m in layout.CropMarks)
                gfx.DrawLine(pen, Units.MmToPt(m.X1), Units.MmToPt(m.Y1), Units.MmToPt(m.X2), Units.MmToPt(m.Y2));
        }

        // A mixed sheet: its own cuts, each across the piece it cuts (the numbers of the cutting order are only on screen:
        // printed, they would land on the visuals).
        if (cutLines && layout.IsMixed)
        {
            foreach (var c in layout.CutSegments)
                gfx.DrawLine(pen, Units.MmToPt(c.X1), Units.MmToPt(c.Y1), Units.MmToPt(c.X2), Units.MmToPt(c.Y2));
        }
        // Continuous cut lines over the visuals, from one side of the block of trims to the other. The cells form a
        // grid: a line along the edge of one cell runs along the edges of its whole row or column.
        else if (cutLines && layout.Cells.Count > 0)
        {
            double top = layout.Cells[0].Trim.Y, bottom = layout.Cells[^1].Trim.Bottom;
            double left = layout.Cells[0].Trim.X, right = layout.Cells[^1].Trim.Right;
            foreach (var x in layout.CutsX)
                gfx.DrawLine(pen, Units.MmToPt(x), Units.MmToPt(top), Units.MmToPt(x), Units.MmToPt(bottom));
            foreach (var y in layout.CutsY)
                gfx.DrawLine(pen, Units.MmToPt(left), Units.MmToPt(y), Units.MmToPt(right), Units.MmToPt(y));
        }

        if (!side.IsVerso && !string.IsNullOrWhiteSpace(mark) && SheetMark.Place(layout) is { } at)
            DrawMark(gfx, mark, at.Band, at.Side);
    }

    // Small grey text, centred along its band; shrunk down to SheetMark.MinFontSizePt when longer than the band.
    private static void DrawMark(XGraphics gfx, string text, RectMm band, SheetMark.Side side)
    {
        GlobalFontSettings.FontResolver ??= new WindowsFonts();
        bool upright = side is SheetMark.Side.Bottom or SheetMark.Side.Top;
        double length = Units.MmToPt(upright ? band.Width : band.Height) - Units.MmToPt(2);
        double size = SheetMark.FontSizePt;
        double width = gfx.MeasureString(text, new XFont("Segoe UI", size)).Width;
        if (width > length) size = Math.Max(SheetMark.MinFontSizePt, size * length / width);
        var font = new XFont("Segoe UI", size);

        double cx = Units.MmToPt(band.X + band.Width / 2), cy = Units.MmToPt(band.Y + band.Height / 2);
        double w = Units.MmToPt(upright ? band.Width : band.Height), h = Units.MmToPt(upright ? band.Height : band.Width);
        var state = gfx.Save();
        if (!upright) gfx.RotateAtTransform(-90, new XPoint(cx, cy)); // read from the bottom up
        gfx.DrawString(text, font, XBrushes.Gray, new XRect(cx - w / 2, cy - h / 2, w, h), XStringFormats.Center);
        gfx.Restore(state);
    }

    /// <summary>The visual of each cell, in the coordinates of the sheet (points); the cutting plan draws them smaller.</summary>
    internal static void DrawVisuals(XGraphics gfx, SheetLayout layout, IEnumerable<(LayoutCell cell, VisualInfo? visual)> cells,
        Func<VisualInfo, XImage> imageOf, FitMode fit, SideOptions side)
    {
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

    // As the cells of the back (Placement.MirrorForVerso).
    private static RectMm MirrorForVerso(RectMm r, SizeMm sheet, DuplexFlip flip) =>
        flip == DuplexFlip.LongEdge ? r.MirrorX(sheet.Width) : r.MirrorY(sheet.Height);

    internal static XRect ToPt(RectMm r) =>
        new(Units.MmToPt(r.X), Units.MmToPt(r.Y), Units.MmToPt(r.Width), Units.MmToPt(r.Height));
}
