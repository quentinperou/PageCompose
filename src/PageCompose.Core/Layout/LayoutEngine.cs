namespace PageCompose.Core.Layout;

/// <summary>Settings of a "step &amp; repeat" layout of visuals of one size on a sheet.</summary>
/// <param name="Sheet">Printing sheet; its <see cref="PaperFormat.Margins"/> are the unprintable edges.</param>
/// <param name="Item">Finished (trimmed) size of a visual.</param>
/// <param name="BleedMm">Bleed on each side (0 = edge to edge: neighbouring visuals share one cut).</param>
/// <param name="CropMarks">Adds crop marks around the block.</param>
/// <param name="SpacingMm">
/// Space between two neighbouring visuals, from trim to trim. The bleed always fits: the space is at least twice the
/// bleed.
/// </param>
/// <param name="Margins">White margins inside each visual, for <paramref name="Item"/> as requested (front only).</param>
/// <param name="Folds">Where the size folds (a folded card); with <paramref name="FoldMarks"/>, dashed marks in line
/// with each fold outside the block, in the room of the crop marks.</param>
public sealed record LayoutOptions(PaperFormat Sheet, SizeMm Item, double BleedMm = 0, bool CropMarks = false,
    double SpacingMm = 0, MarginsMm Margins = default, FoldsMm? Folds = null, bool FoldMarks = false)
{
    /// <summary>Marks are drawn outside the block, crop marks or fold marks: the room is kept for them.</summary>
    public bool HasMarks => CropMarks || (FoldMarks && Folds is { IsEmpty: false });

    /// <summary>Distance between the trims of two neighbouring cells.</summary>
    public double Gap => Math.Max(SpacingMm, 2 * BleedMm);

    /// <summary>Minimum room kept for crop marks outside the block (short marks: 5 mm would cost visuals).</summary>
    public const double CropMarkReserveMm = 3.0;

    /// <summary>Gap between the edge of the block (bleed included) and the start of a mark.</summary>
    public const double CropMarkOffsetMm = 1.0;

    /// <summary>Longest mark, when there is room for it.</summary>
    public const double CropMarkMaxLengthMm = 5.0;
}

/// <param name="Margins">White margins as laid on the sheet (turned with the visual when <paramref name="Rotated"/>).</param>
/// <param name="Rotated">The visual is laid a quarter turn clockwise from the requested size.</param>
public sealed record LayoutCell(int Column, int Row, RectMm Trim, RectMm Bleed, MarginsMm Margins = default, bool Rotated = false)
{
    /// <summary>Finished image area: the trim inside the white margins.</summary>
    public RectMm ImageTrim => Trim.Deflate(Margins);

    /// <summary>Where the image is drawn: the bleed only reaches the sides without a white margin.</summary>
    public RectMm ImageBleed
    {
        get
        {
            double left = Margins.Left > 0 ? Trim.X + Margins.Left : Bleed.X;
            double top = Margins.Top > 0 ? Trim.Y + Margins.Top : Bleed.Y;
            double right = Margins.Right > 0 ? Trim.Right - Margins.Right : Bleed.Right;
            double bottom = Margins.Bottom > 0 ? Trim.Bottom - Margins.Bottom : Bleed.Bottom;
            return new RectMm(left, top, right - left, bottom - top);
        }
    }
}

/// <summary>Edge of a piece of paper put against the back gauge of the guillotine for a cut.</summary>
public enum CutEdge { Top, Left, Bottom, Right }

/// <summary>
/// Result of the layout computation, in millimetres: a grid of one size (<see cref="LayoutEngine"/>), or one sheet of
/// several sizes (<see cref="MixedPacker"/>, <see cref="IsMixed"/>).
/// </summary>
public sealed class SheetLayout
{
    internal SheetLayout(LayoutOptions options, RectMm printableArea, bool itemRotated, int columns, int rows,
        IReadOnlyList<LayoutCell> cells, IReadOnlyList<double> cutsX, IReadOnlyList<double> cutsY,
        IReadOnlyList<LineMm> cropMarks, IReadOnlyList<LineMm>? foldMarks = null)
    {
        FoldMarks = foldMarks ?? [];
        Options = options;
        PrintableArea = printableArea;
        ItemRotated = itemRotated;
        Columns = columns;
        Rows = rows;
        Cells = cells;
        CutsX = cutsX;
        CutsY = cutsY;
        CropMarks = cropMarks;
    }

    /// <summary>A sheet of several sizes: no grid, its own cuts in cutting order, and the spaces to paint white.</summary>
    internal SheetLayout(LayoutOptions options, RectMm printableArea, IReadOnlyList<LayoutCell> cells,
        IReadOnlyList<LineMm> cutSegments, IReadOnlyList<RectMm> cutPieces, IReadOnlyList<RectMm> spaces,
        IReadOnlyList<LineMm> cropMarks)
        : this(options, printableArea, false, 0, 0, cells, [], [], cropMarks)
    {
        IsMixed = true;
        CutSegments = cutSegments;
        CutPieces = cutPieces;
        Spaces = spaces;
    }

    /// <summary>
    /// Several sizes on the sheet, each cell its own size: there are no columns nor rows, <see cref="LayoutOptions.Item"/>
    /// is not used (zero), and the cuts are <see cref="CutSegments"/>.
    /// </summary>
    public bool IsMixed { get; }

    /// <summary>
    /// Cuts of a mixed sheet, in cutting order (guillotine: each one across the piece it cuts). Those along the edges
    /// of the block are drawn along the block, though they go across the sheet.
    /// </summary>
    public IReadOnlyList<LineMm> CutSegments { get; } = [];

    /// <summary>The piece of paper each cut of <see cref="CutSegments"/> cuts, as it is at that moment.</summary>
    public IReadOnlyList<RectMm> CutPieces { get; } = [];

    /// <summary>
    /// The clamp of a guillotine needs room: a part of the piece narrower than this is measured from the other edge.
    /// </summary>
    public const double MinHeldMm = 30;

    /// <summary>
    /// Where cut <paramref name="index"/> falls in its piece: the measurement to set on the guillotine, with
    /// <c>Edge</c> of the piece against the back gauge. From the top (a cut across) or the left (a cut down), so that
    /// it reads as the size of a visual, unless that part is too narrow to be held (a space, a strip of waste): then
    /// from the bottom or the right.
    /// </summary>
    public (double Mm, CutEdge Edge) CutMeasurement(int index)
    {
        var (cut, piece) = (CutSegments[index], CutPieces[index]);
        bool across = Math.Abs(cut.Y1 - cut.Y2) < 1e-9;
        double first = across ? cut.Y1 - piece.Y : cut.X1 - piece.X;
        double second = (across ? piece.Height : piece.Width) - first;
        bool fromFirst = first >= MinHeldMm || first >= second;
        return across
            ? (fromFirst ? first : second, fromFirst ? CutEdge.Top : CutEdge.Bottom)
            : (fromFirst ? first : second, fromFirst ? CutEdge.Left : CutEdge.Right);
    }

    /// <summary>What no bleed covers between the visuals of a mixed sheet, painted white.</summary>
    public IReadOnlyList<RectMm> Spaces { get; } = [];

    public LayoutOptions Options { get; }
    public SizeMm Sheet => Options.Sheet.Size;
    public RectMm PrintableArea { get; }

    /// <summary>True when the visuals are laid a quarter turn from the requested size.</summary>
    public bool ItemRotated { get; }

    /// <summary>Finished size as laid on the sheet.</summary>
    public SizeMm PlacedItem => ItemRotated ? Options.Item.Rotated : Options.Item;

    public int Columns { get; }
    public int Rows { get; }
    public int PerSheet => Cells.Count;
    public bool IsEmpty => PerSheet == 0;

    /// <summary>Cells in reading order (row by row).</summary>
    public IReadOnlyList<LayoutCell> Cells { get; }

    /// <summary>Positions of the vertical (x) and horizontal (y) cut lines.</summary>
    public IReadOnlyList<double> CutsX { get; }
    public IReadOnlyList<double> CutsY { get; }

    public IReadOnlyList<LineMm> CropMarks { get; }

    /// <summary>Marks in line with the folds of folded cards, outside the block (drawn dashed).</summary>
    public IReadOnlyList<LineMm> FoldMarks { get; }

    /// <summary>Bounds of all cells, bleed included.</summary>
    public RectMm? Block => IsEmpty ? null : Bounds(Cells.Select(c => c.Bleed));

    /// <summary>The smallest rectangle around <paramref name="rects"/> (at least one).</summary>
    internal static RectMm Bounds(IEnumerable<RectMm> rects)
    {
        double left = double.MaxValue, top = double.MaxValue, right = double.MinValue, bottom = double.MinValue;
        foreach (var r in rects)
            (left, top, right, bottom) = (Math.Min(left, r.X), Math.Min(top, r.Y), Math.Max(right, r.Right), Math.Max(bottom, r.Bottom));
        return new RectMm(left, top, right - left, bottom - top);
    }
}

public static class LayoutEngine
{
    public static SheetLayout Compute(LayoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Item.Width <= 0 || options.Item.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "The visual size must be positive.");
        if (options.BleedMm < 0)
            throw new ArgumentOutOfRangeException(nameof(options), "The bleed cannot be negative.");
        if (options.SpacingMm < 0)
            throw new ArgumentOutOfRangeException(nameof(options), "The spacing cannot be negative.");
        var margins = options.Margins;
        if (margins.Top < 0 || margins.Right < 0 || margins.Bottom < 0 || margins.Left < 0)
            throw new ArgumentOutOfRangeException(nameof(options), "The margins cannot be negative.");
        if (margins.Left + margins.Right >= options.Item.Width || margins.Top + margins.Bottom >= options.Item.Height)
            throw new ArgumentOutOfRangeException(nameof(options), "The margins leave no room for the image.");

        var edges = options.Sheet.Margins;
        if (edges.Top < 0 || edges.Right < 0 || edges.Bottom < 0 || edges.Left < 0)
            throw new ArgumentOutOfRangeException(nameof(options), "The unprintable margins cannot be negative.");

        var sheet = options.Sheet.Size;
        var printable = new RectMm(0, 0, sheet.Width, sheet.Height).Deflate(edges);
        if (printable.Width <= 0 || printable.Height <= 0)
            return new SheetLayout(options, new RectMm(sheet.Width / 2, sheet.Height / 2, 0, 0), false, 0, 0, [], [], [], []);

        // Around the block: the bleed of the outer cells, plus the room for the crop marks.
        double reserve = options.BleedMm + (options.HasMarks ? LayoutOptions.CropMarkReserveMm : 0);
        double gap = options.Gap;
        double availW = printable.Width - 2 * reserve;
        double availH = printable.Height - 2 * reserve;

        var item = options.Item;
        var (cols, rows) = (Fit(availW, item.Width, gap), Fit(availH, item.Height, gap));
        var (colsR, rowsR) = (Fit(availW, item.Height, gap), Fit(availH, item.Width, gap));

        // On a tie, keep the requested orientation.
        bool rotated = colsR * rowsR > cols * rows;
        if (rotated)
        {
            (cols, rows) = (colsR, rowsR);
            item = item.Rotated;
            margins = margins.RotatedClockwise;
        }

        if (cols * rows == 0)
            return new SheetLayout(options, printable, false, 0, 0, [], [], [], []);

        double blockW = cols * item.Width + (cols - 1) * gap;
        double blockH = rows * item.Height + (rows - 1) * gap;
        double x0 = (sheet.Width - blockW) / 2;
        double y0 = (sheet.Height - blockH) / 2;

        var cells = new List<LayoutCell>(cols * rows);
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            var trim = new RectMm(x0 + c * (item.Width + gap), y0 + r * (item.Height + gap), item.Width, item.Height);
            cells.Add(new LayoutCell(c, r, trim, trim.Inflate(options.BleedMm), margins, rotated));
        }

        var cutsX = Distinct(Enumerable.Range(0, cols).SelectMany(c => new[] { x0 + c * (item.Width + gap), x0 + c * (item.Width + gap) + item.Width }));
        var cutsY = Distinct(Enumerable.Range(0, rows).SelectMany(r => new[] { y0 + r * (item.Height + gap), y0 + r * (item.Height + gap) + item.Height }));

        var marked = new RectMm(x0, y0, blockW, blockH).Inflate(options.BleedMm);
        var marks = options.CropMarks ? BuildCropMarks(marked, printable, cutsX, cutsY) : [];

        // The folds of each column and row (turned with the size), marked the same way as the cuts.
        List<LineMm> foldMarks = [];
        if (options.FoldMarks && options.Folds is { IsEmpty: false } folds)
        {
            var laid = rotated ? folds.RotatedClockwise(options.Item.Height) : folds;
            var foldsX = Enumerable.Range(0, cols).SelectMany(c => laid.X.Select(x => x0 + c * (item.Width + gap) + x)).ToList();
            var foldsY = Enumerable.Range(0, rows).SelectMany(r => laid.Y.Select(y => y0 + r * (item.Height + gap) + y)).ToList();
            foldMarks = BuildCropMarks(marked, printable, foldsX, foldsY);
        }

        return new SheetLayout(options, printable, rotated, cols, rows, cells, cutsX, cutsY, marks, foldMarks);
    }

    /// <summary>Largest n such that n * size + (n - 1) * gap fits in avail.</summary>
    internal static int Fit(double avail, double size, double gap)
    {
        if (size <= 0 || avail + 1e-9 < size) return 0;
        return (int)Math.Floor((avail + gap) / (size + gap) + 1e-9);
    }

    private static List<double> Distinct(IEnumerable<double> values)
    {
        var result = new List<double>();
        foreach (var v in values.Order())
            if (result.Count == 0 || v - result[^1] > 1e-6)
                result.Add(v);
        return result;
    }

    private static List<LineMm> BuildCropMarks(RectMm block, RectMm printable, List<double> cutsX, List<double> cutsY)
    {
        const double offset = LayoutOptions.CropMarkOffsetMm;
        const double maxLen = LayoutOptions.CropMarkMaxLengthMm;
        var marks = new List<LineMm>();

        foreach (var x in cutsX)
        {
            double top = block.Y - offset;
            double bottom = block.Bottom + offset;
            marks.Add(new LineMm(x, top, x, Math.Max(printable.Y, top - maxLen)));
            marks.Add(new LineMm(x, bottom, x, Math.Min(printable.Bottom, bottom + maxLen)));
        }

        foreach (var y in cutsY)
        {
            double left = block.X - offset;
            double right = block.Right + offset;
            marks.Add(new LineMm(left, y, Math.Max(printable.X, left - maxLen), y));
            marks.Add(new LineMm(right, y, Math.Min(printable.Right, right + maxLen), y));
        }

        return marks;
    }
}
