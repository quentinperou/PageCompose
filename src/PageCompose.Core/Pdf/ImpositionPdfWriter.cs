using PageCompose.Core.Layout;
using PageCompose.Core.Visuals;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace PageCompose.Core.Pdf;

/// <summary>
/// Colours offered for the cut lines: black, grey, white (no ink: it shows on dark photos), or a bright one that stands
/// out on any photo.
/// </summary>
public enum CutLineColor { Black, Grey, White, Red, Cyan, Magenta }

public static class CutLineColors
{
    /// <summary>The colour as printed (and shown in the list of the app).</summary>
    public static (byte R, byte G, byte B) Rgb(this CutLineColor color) => color switch
    {
        CutLineColor.Grey => (128, 128, 128),
        CutLineColor.White => (255, 255, 255),
        CutLineColor.Red => (228, 0, 43),
        CutLineColor.Cyan => (0, 174, 239),
        CutLineColor.Magenta => (236, 0, 140),
        _ => (0, 0, 0),
    };
}

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
/// <param name="CutLineColor">Colour of the cut lines, drawn over the visuals (the crop marks stay black).</param>
/// <param name="MirrorBleed">The bleed takes the edges of the visuals, mirrored, instead of enlarging them (see <see cref="SideOptions.MirrorBleed"/>).</param>
/// <param name="Caption">How the captions of the visuals (<see cref="VisualInfo.Caption"/>) are written in the band of their frame; null: none.</param>
/// <param name="Numbering">How the numbers of the visuals are written, on the front; null: none.</param>
/// <param name="Numbers">The number of each visual, sheet by sheet as <paramref name="Sheets"/> (see <see cref="Pdf.Numbering.Assign"/>).</param>
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
    IReadOnlyList<string>? Marks = null,
    CutLineColor CutLineColor = CutLineColor.Black,
    bool MirrorBleed = false,
    CaptionStyle? Caption = null,
    NumberingStyle? Numbering = null,
    IReadOnlyList<int?[]>? Numbers = null)
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

    /// <summary>How far the mirrored copies reach under the visual, so that renderers leave no hairline between them.</summary>
    private const double MirrorOverlapMm = 0.05;

    /// <summary>Written whole or not at all: a failed export leaves the file that was there as it was.</summary>
    public static void Write(ImpositionJob job, string path) => SafeFile.Write(path, stream => Write(job, stream));

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

            var rectoSide = SideOptions.Recto with { MirrorBleed = job.MirrorBleed };
            var versoSide = new SideOptions(true, job.Flip, job.RotateVerso180, job.VersoMargins, job.MirrorBleed);
            for (int s = 0; s < job.Sheets.Count; s++)
            {
                var (layout, sheet) = (job.LayoutOf(s), job.Sheets[s]);
                var recto = layout.Cells.Select((cell, i) => (cell, visual: sheet[i])).ToList();
                DrawSide(doc, layout, recto, ImageOf, job.Fit, rectoSide, job.CutLines ? job.CutLineColor : null, job.Marks?[s], job.Caption,
                    job.Numbering is null ? null : job.Numbers?[s], job.Numbering);

                if (job.HasBack)
                {
                    // The back is only printed behind cells that have a front: their own back, else the shared one.
                    var back = recto.Select((r, i) => (r.cell, visual: r.visual is null ? null : job.BackOf(s, i))).ToList();
                    DrawSide(doc, layout, back, ImageOf, job.Fit, versoSide, cutLines: null);
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
            // PDFsharp draws a page that has a /Rotate already turned, in a box the size of the page before the turn:
            // turned again by the placement (BaseRotation), it came out the wrong way, and off its cell at a quarter
            // turn. The placement turns the page as it is stored: such a page is drawn from a copy without its /Rotate.
            if (visual.BaseRotation != 0)
                return XPdfForm.FromStream(Unrotated(visual.Path, visual.PageNumber));
            var form = XPdfForm.FromFile(visual.Path);
            form.PageNumber = visual.PageNumber;
            return form;
        }

        return XImage.FromFile(visual.Path);
    }

    // The page alone, in a PDF of its own, with no /Rotate.
    private static MemoryStream Unrotated(string path, int pageNumber)
    {
        using var source = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        using var copy = new PdfDocument();
        var page = copy.AddPage(source.Pages[pageNumber - 1]);
        page.Rotate = 0;
        var stream = new MemoryStream();
        copy.Save(stream, false);
        stream.Position = 0;
        return stream;
    }

    private static void DrawSide(PdfDocument doc, SheetLayout layout, List<(LayoutCell cell, VisualInfo? visual)> cells,
        Func<VisualInfo, XImage> imageOf, FitMode fit, SideOptions side, CutLineColor? cutLines, string? mark = null,
        CaptionStyle? caption = null, int?[]? numbers = null, NumberingStyle? numbering = null)
    {
        var page = doc.AddPage();
        page.Width = XUnit.FromMillimeter(layout.Sheet.Width);
        page.Height = XUnit.FromMillimeter(layout.Sheet.Height);

        using var gfx = XGraphics.FromPdfPage(page);
        DrawVisuals(gfx, layout, cells, imageOf, fit, side);

        // The captions, in the bands of the frames, on the front.
        if (!side.IsVerso && caption is not null)
        {
            foreach (var (cell, visual) in cells)
                if (visual?.Caption is { Length: > 0 } text && !string.IsNullOrWhiteSpace(text))
                    DrawCaption(gfx, cell, text.Trim(), caption);
        }

        // The number of each visual, on the front.
        if (!side.IsVerso && numbering is not null && numbers is not null)
        {
            for (int i = 0; i < cells.Count && i < numbers.Length; i++)
                if (cells[i].visual is not null && numbers[i] is { } number)
                    DrawNumber(gfx, cells[i].cell, numbering.Text(number), numbering);
        }

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

        // Crop marks on the front only: that is the side facing up on the guillotine. Fold marks dashed beside them.
        var pen = new XPen(XColors.Black, CropMarkWidthPt);
        if (!side.IsVerso && layout.CropMarks.Count > 0)
        {
            foreach (var m in layout.CropMarks)
                gfx.DrawLine(pen, Units.MmToPt(m.X1), Units.MmToPt(m.Y1), Units.MmToPt(m.X2), Units.MmToPt(m.Y2));
        }
        if (!side.IsVerso && layout.FoldMarks.Count > 0)
        {
            // Dashes of 0.8 mm, 0.6 mm apart (the pattern counts in line widths): seen at a glance on paper.
            var dashed = new XPen(XColors.Black, CropMarkWidthPt)
            {
                DashPattern = [Units.MmToPt(0.8) / CropMarkWidthPt, Units.MmToPt(0.6) / CropMarkWidthPt],
            };
            foreach (var m in layout.FoldMarks)
                gfx.DrawLine(dashed, Units.MmToPt(m.X1), Units.MmToPt(m.Y1), Units.MmToPt(m.X2), Units.MmToPt(m.Y2));
        }

        // A mixed sheet: its own cuts, each across the piece it cuts (the numbers of the cutting order are only on screen:
        // printed, they would land on the visuals).
        var linePen = cutLines is { } color ? new XPen(ColorOf(color), CropMarkWidthPt) : null;
        if (linePen is not null && layout.IsMixed)
        {
            foreach (var c in layout.CutSegments)
                gfx.DrawLine(linePen, Units.MmToPt(c.X1), Units.MmToPt(c.Y1), Units.MmToPt(c.X2), Units.MmToPt(c.Y2));
        }
        // Continuous cut lines over the visuals, from one side of the block of trims to the other. The cells form a
        // grid: a line along the edge of one cell runs along the edges of its whole row or column.
        else if (linePen is not null && layout.Cells.Count > 0)
        {
            double top = layout.Cells[0].Trim.Y, bottom = layout.Cells[^1].Trim.Bottom;
            double left = layout.Cells[0].Trim.X, right = layout.Cells[^1].Trim.Right;
            foreach (var x in layout.CutsX)
                gfx.DrawLine(linePen, Units.MmToPt(x), Units.MmToPt(top), Units.MmToPt(x), Units.MmToPt(bottom));
            foreach (var y in layout.CutsY)
                gfx.DrawLine(linePen, Units.MmToPt(left), Units.MmToPt(y), Units.MmToPt(right), Units.MmToPt(y));
        }

        if (!side.IsVerso && !string.IsNullOrWhiteSpace(mark) && SheetMark.Place(layout) is { } at)
            DrawMark(gfx, mark, at.Band, at.Side);
    }

    private static XColor ColorOf(CutLineColor color) => color.Rgb() is var (r, g, b) ? XColor.FromArgb(r, g, b) : XColors.Black;

    // Centred in the band, as large as its thickness allows (shrunk to its length), turned with the frame.
    private static void DrawCaption(XGraphics gfx, LayoutCell cell, string text, CaptionStyle style)
    {
        if (Caption.BandOf(cell) is not { } at) return;
        GlobalFontSettings.FontResolver ??= new WindowsFonts();
        string family = WindowsFonts.Register(style.Family, style.FontFile);

        var band = at.Band;
        bool across = at.Side is Caption.Side.Bottom or Caption.Side.Top;
        double thick = across ? band.Height : band.Width, length = across ? band.Width : band.Height;
        double size = Units.MmToPt(thick * Caption.HeightShare);
        var font = new XFont(family, size);
        double width = gfx.MeasureString(text, font).Width, room = Units.MmToPt(length * Caption.LengthShare);
        if (width > room) font = new XFont(family, size * room / width);

        var (r, g, b) = style.Color.Rgb();
        double cx = Units.MmToPt(band.CenterX), cy = Units.MmToPt(band.CenterY), w = Units.MmToPt(length), h = Units.MmToPt(thick);
        var state = gfx.Save();
        if (Caption.AngleOf(cell) is var angle and not 0) gfx.RotateAtTransform(angle, new XPoint(cx, cy));
        gfx.DrawString(text, font, new XSolidBrush(XColor.FromArgb(r, g, b)), new XRect(cx - w / 2, cy - h / 2, w, h), XStringFormats.Center);
        gfx.Restore(state);
    }

    // The number of a visual at each of its places, from the top left corner of the visual as it stands (a size laid a
    // quarter turn is turned clockwise, as its margins), clipped to its trim. The corner given is that of the number as
    // it lies: turned about it, then moved back into that corner.
    private static void DrawNumber(XGraphics gfx, LayoutCell cell, string text, NumberingStyle style)
    {
        GlobalFontSettings.FontResolver ??= new WindowsFonts();
        var font = new XFont(WindowsFonts.Register(style.Family, style.FontFile), Units.MmToPt(Math.Max(0.5, style.SizeMm)));
        var (r, g, b) = style.Color.Rgb();
        var brush = new XSolidBrush(XColor.FromArgb(r, g, b));
        var size = gfx.MeasureString(text, font);
        double pad = style.Background is null ? 0 : Units.MmToPt(NumberingStyle.BackgroundPaddingMm);
        double w = size.Width + 2 * pad, h = size.Height + 2 * pad;

        var t = cell.Trim;
        var state = gfx.Save();
        gfx.IntersectClip(ToPt(t));
        gfx.TranslateTransform(Units.MmToPt(cell.Rotated ? t.Right : t.X), Units.MmToPt(t.Y));
        if (cell.Rotated) gfx.RotateTransform(90);
        foreach (var place in style.Places)
        {
            var at = gfx.Save();
            gfx.TranslateTransform(Units.MmToPt(place.X), Units.MmToPt(place.Y));
            switch (place.Direction)
            {
                case NumberDirection.Vertical: gfx.TranslateTransform(0, w); gfx.RotateTransform(-90); break;
                case NumberDirection.HorizontalInverted: gfx.TranslateTransform(w, h); gfx.RotateTransform(180); break;
                case NumberDirection.VerticalInverted: gfx.TranslateTransform(h, 0); gfx.RotateTransform(90); break;
            }
            if (style.Background is { } background)
            {
                var (br, bg, bb) = background.Rgb();
                gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(br, bg, bb)), new XRect(0, 0, w, h));
            }
            gfx.DrawString(text, font, brush, new XRect(pad, pad, size.Width, size.Height), XStringFormats.TopLeft);
            gfx.Restore(at);
        }
        gfx.Restore(state);
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
            var image = imageOf(visual);

            // Mirrored bleed: the copies first, a hair larger, under the visual: no seam shows between them.
            var r = p.DrawRect;
            foreach (var (dx, dy) in p.MirrorCopies)
            {
                var copy = gfx.Save();
                gfx.TranslateTransform(Units.MmToPt(r.CenterX + dx * r.Width), Units.MmToPt(r.CenterY + dy * r.Height));
                gfx.ScaleTransform(dx != 0 ? -1 : 1, dy != 0 ? -1 : 1);
                gfx.DrawImage(image, ToPt(new RectMm(-r.Width / 2, -r.Height / 2, r.Width, r.Height).Inflate(MirrorOverlapMm)));
                gfx.Restore(copy);
            }
            gfx.DrawImage(image, ToPt(r));
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
