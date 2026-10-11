using PageCompose.Core.Visuals;

namespace PageCompose.Core.Layout;

/// <summary>A visual of a mixed layout: its finished size, its number of copies and the white margins of its size.</summary>
public sealed record MixedItem(VisualInfo Visual, SizeMm Size, int Copies, MarginsMm Margins = default);

/// <summary>
/// One sheet of a mixed layout: its own cells and cuts, and the item of each cell (index in the list given to
/// <see cref="MixedPacker.Pack"/>) with its visual, in cell order.
/// </summary>
public sealed record MixedSheet(SheetLayout Layout, IReadOnlyList<int> Items, VisualInfo?[] Visuals);

/// <summary>The sheets of a mixed layout. Items too large for the sheet (indices) are left out.</summary>
public sealed record MixedPlan(IReadOnlyList<MixedSheet> Sheets, IReadOnlyList<int> TooLarge)
{
    public static MixedPlan Empty { get; } = new([], []);

    public int SheetCount => Sheets.Count;

    /// <summary>Copies laid on the sheets.</summary>
    public int Printed => Sheets.Sum(s => s.Items.Count);
}

/// <summary>
/// Lays visuals of several sizes on as few sheets as possible, each sheet its own arrangement, with guillotine cuts
/// only: each cut goes from one side of the piece it cuts to the other, as on a guillotine. Visuals may turn a quarter.
/// Many simple rules are tried (sort order × choice of the free piece × split of what is left); the best layout
/// wins: fewest sheets, then fewest cuts, then the smallest block on the last sheet.
/// </summary>
public static class MixedPacker
{
    private const double Eps = 1e-6;

    /// <summary>Above this many copies, fewer rules are tried, so that the layout is still computed at once.</summary>
    private const int ManyCopies = 150;

    /// <param name="bleedMm">Bleed on each side of every visual.</param>
    /// <param name="spacingMm">Space between two neighbouring visuals, from trim to trim (at least twice the bleed).</param>
    /// <param name="cropMarks">
    /// Crop marks outside the block, in line with the cuts that reach its edges; they keep the same room around the
    /// block as in the grid layouts.
    /// </param>
    public static MixedPlan Pack(PaperFormat sheet, IReadOnlyList<MixedItem> items, double bleedMm = 0, double spacingMm = 0,
        bool cropMarks = false)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(items);
        if (bleedMm < 0)
            throw new ArgumentOutOfRangeException(nameof(bleedMm), "The bleed cannot be negative.");
        if (spacingMm < 0)
            throw new ArgumentOutOfRangeException(nameof(spacingMm), "The spacing cannot be negative.");
        var edges = sheet.Margins;
        if (edges.Top < 0 || edges.Right < 0 || edges.Bottom < 0 || edges.Left < 0)
            throw new ArgumentOutOfRangeException(nameof(sheet), "The unprintable margins cannot be negative.");
        foreach (var item in items)
        {
            if (item.Size.Width <= 0 || item.Size.Height <= 0)
                throw new ArgumentOutOfRangeException(nameof(items), "The visual size must be positive.");
            var m = item.Margins;
            if (m.Top < 0 || m.Right < 0 || m.Bottom < 0 || m.Left < 0 ||
                m.Left + m.Right >= item.Size.Width || m.Top + m.Bottom >= item.Size.Height)
                throw new ArgumentOutOfRangeException(nameof(items), "The margins leave no room for the image.");
        }

        var options = new LayoutOptions(sheet, default, bleedMm, cropMarks, spacingMm);
        double gap = options.Gap;
        var printable = new RectMm(0, 0, sheet.Width, sheet.Height).Deflate(edges);
        // The outer bleeds stay printable, and so do the crop marks around them. Each visual takes its size plus the
        // gap after it (right and bottom): the area is widened by one gap too.
        var usable = printable.Inflate(-(bleedMm + (cropMarks ? LayoutOptions.CropMarkReserveMm : 0)));
        var area = new RectMm(usable.X, usable.Y, usable.Width + gap, usable.Height + gap);

        bool Fits(SizeMm s) => (s.Width <= usable.Width + Eps && s.Height <= usable.Height + Eps) ||
                               (s.Height <= usable.Width + Eps && s.Width <= usable.Height + Eps);
        var tooLarge = Enumerable.Range(0, items.Count).Where(i => items[i].Copies > 0 && !Fits(items[i].Size)).ToList();
        var queue = Enumerable.Range(0, items.Count).Where(i => !tooLarge.Contains(i))
            .SelectMany(i => Enumerable.Repeat(i, Math.Max(0, items[i].Copies))).ToList();
        if (queue.Count == 0)
            return new MixedPlan([], tooLarge);

        var sizes = items.Select(i => i.Size).ToList();
        int[] sorts = queue.Count > ManyCopies ? [0, 1, 3] : [0, 1, 2, 3, 4];
        int[] fits = queue.Count > ManyCopies ? [0, 1] : [0, 1, 2];
        int[] splits = queue.Count > ManyCopies ? [0, 1, 4] : [0, 1, 2, 3, 4, 5];

        List<Packed>? best = null;
        (int, int, double) bestScore = default;
        foreach (int sort in sorts)
        foreach (int fit in fits)
        foreach (int split in splits)
        {
            var sheets = PackOnce(Sorted(queue, sizes, sort), sizes, area, gap, fit, split)
                .Select(s => Finish(s, sheet, printable, bleedMm, gap, cropMarks)).ToList();
            var last = SheetLayout.Bounds(sheets[^1].Placed.Select(p => p.Trim));
            var score = (sheets.Count, sheets.Sum(s => s.Cuts.Count), last.Width * last.Height);
            if (best is null || score.CompareTo(bestScore) < 0)
                (best, bestScore) = (sheets, score);
        }

        return new MixedPlan(best!.Select(p => ToSheet(p, items, options, printable)).ToList(), tooLarge);
    }

    /// <summary>
    /// Where to write the number of each cut of a mixed sheet (on screen and on the cutting plan): those of the edges
    /// of the block just outside it, beside its bleed, where they are easier to read than on the edge of the photos
    /// (asked by the author); the others along their cut, away from the numbers already placed.
    /// </summary>
    /// <param name="sizeMm">Diameter of a number, in millimetres on the sheet.</param>
    public static IReadOnlyList<(double X, double Y)> NumberPositions(SheetLayout layout, double sizeMm)
    {
        var cuts = layout.CutSegments;
        if (cuts.Count == 0 || layout.IsEmpty) return [];
        var block = SheetLayout.Bounds(layout.Cells.Select(c => c.Trim));
        double outside = layout.Options.BleedMm + LayoutOptions.CropMarkOffsetMm + sizeMm / 2;
        double near = sizeMm / 2 + 0.5; // the whole number stays on the sheet
        double width = layout.Sheet.Width, height = layout.Sheet.Height;

        var fixedAt = new (double X, double Y)?[cuts.Count];
        for (int i = 0; i < cuts.Count; i++)
        {
            var c = cuts[i];
            double midX = (c.X1 + c.X2) / 2, midY = (c.Y1 + c.Y2) / 2;
            bool across = Math.Abs(c.Y1 - c.Y2) < Eps;
            if (across && Math.Abs(c.Y1 - block.Y) < Eps) fixedAt[i] = (midX, Math.Max(near, block.Y - outside));
            else if (across && Math.Abs(c.Y1 - block.Bottom) < Eps) fixedAt[i] = (midX, Math.Min(height - near, block.Bottom + outside));
            else if (!across && Math.Abs(c.X1 - block.X) < Eps) fixedAt[i] = (Math.Max(near, block.X - outside), midY);
            else if (!across && Math.Abs(c.X1 - block.Right) < Eps) fixedAt[i] = (Math.Min(width - near, block.Right + outside), midY);
        }

        return Along(cuts, sizeMm * 1.3, fixedAt);
    }

    /// <summary>
    /// Where to write the number of each cut: along the cut, away from the numbers already placed (two cuts on each
    /// side of a narrow space would put them on top of each other).
    /// </summary>
    /// <param name="distanceMm">Distance wanted between two numbers.</param>
    public static IReadOnlyList<(double X, double Y)> NumberPositions(IReadOnlyList<LineMm> cuts, double distanceMm) =>
        Along(cuts, distanceMm, new (double X, double Y)?[cuts.Count]);

    // The numbers already placed (fixedAt) stay; the others go along their cut, as far as they can from all of them.
    private static List<(double X, double Y)> Along(IReadOnlyList<LineMm> cuts, double distanceMm, (double X, double Y)?[] fixedAt)
    {
        double[] along = [0.5, 0.3, 0.7, 0.15, 0.85, 0.4, 0.6, 0.22, 0.78];
        var placed = fixedAt.Where(p => p is not null).Select(p => p!.Value).ToList();
        var result = new List<(double X, double Y)>(cuts.Count);
        for (int i = 0; i < cuts.Count; i++)
        {
            if (fixedAt[i] is { } given)
            {
                result.Add(given);
                continue;
            }

            var c = cuts[i];
            (double X, double Y) best = default;
            double bestRoom = -1;
            foreach (double t in along)
            {
                var p = (X: c.X1 + (c.X2 - c.X1) * t, Y: c.Y1 + (c.Y2 - c.Y1) * t);
                double room = placed.Count == 0 ? double.MaxValue : placed.Min(q => Math.Sqrt((q.X - p.X) * (q.X - p.X) + (q.Y - p.Y) * (q.Y - p.Y)));
                if (room >= distanceMm) { best = p; break; }
                if (room > bestRoom) (best, bestRoom) = (p, room);
            }

            placed.Add(best);
            result.Add(best);
        }

        return result;
    }

    // ---- Packing ----

    /// <summary>A piece of the sheet, in packing coordinates: its footprint includes the gap after it (right and bottom).</summary>
    private sealed class Node(RectMm footprint)
    {
        public readonly RectMm Footprint = footprint;
        public Node? First, Second; // a split: First is on the left (vertical cut) or at the top (horizontal cut)
        public bool VerticalCut;
        public int Placed = -1;     // a visual: index in the sheet's placed list
    }

    /// <summary>A visual on a sheet: its item, its finished size as laid, and whether it is turned a quarter.</summary>
    private sealed record Placed(int Item, RectMm Trim, bool Rotated);

    private sealed class Sheet
    {
        public Sheet(RectMm area) => Free.Add(Root = new Node(area));

        public readonly Node Root;
        public readonly List<Node> Free = [];
        public readonly List<Placed> Placed = [];

        /// <summary>Sizes (short side, long side) that found no room here: a larger one will not either.</summary>
        public readonly List<(double Short, double Long)> Failed = [];
    }

    /// <summary>
    /// A sheet once packed: its visuals in reading order, its cuts in cutting order with the piece each one cuts, its
    /// white spaces and its crop marks.
    /// </summary>
    private sealed record Packed(List<Placed> Placed, List<LineMm> Cuts, List<RectMm> Pieces, List<RectMm> Spaces, List<LineMm> Marks);

    // Copies of the same visual stay together: the sorts are stable.
    private static List<int> Sorted(List<int> queue, List<SizeMm> sizes, int rule) => rule switch
    {
        0 => [.. queue.OrderByDescending(i => sizes[i].Width * sizes[i].Height)],
        1 => [.. queue.OrderByDescending(i => Math.Max(sizes[i].Width, sizes[i].Height))],
        2 => [.. queue.OrderByDescending(i => sizes[i].Width + sizes[i].Height)],
        3 => [.. queue.OrderByDescending(i => Math.Min(sizes[i].Width, sizes[i].Height))],
        _ => [.. queue.OrderByDescending(i => Math.Max(sizes[i].Width, sizes[i].Height) / Math.Min(sizes[i].Width, sizes[i].Height))],
    };

    private static List<Sheet> PackOnce(List<int> queue, List<SizeMm> sizes, RectMm area, double gap, int fitRule, int splitRule)
    {
        var sheets = new List<Sheet>();
        foreach (int item in queue)
        {
            var size = sizes[item];
            var choice = Choose(sheets, size, gap, fitRule);
            if (choice is null)
            {
                sheets.Add(new Sheet(area));
                choice = Choose([sheets[^1]], size, gap, fitRule)
                         ?? throw new InvalidOperationException("A visual larger than the sheet was not set aside.");
            }

            var (target, node, rotated) = choice.Value;
            Place(target, node, item, rotated ? size.Rotated : size, rotated, gap, splitRule);
        }

        return sheets;
    }

    /// <summary>The first sheet where the visual fits, and its best free piece there. On a tie, the visual is not turned.</summary>
    private static (Sheet, Node, bool Rotated)? Choose(List<Sheet> sheets, SizeMm size, double gap, int fitRule)
    {
        var key = (Short: Math.Min(size.Width, size.Height), Long: Math.Max(size.Width, size.Height));
        foreach (var sheet in sheets)
        {
            if (sheet.Failed.Any(f => f.Short <= key.Short + Eps && f.Long <= key.Long + Eps)) continue;

            (Sheet, Node, bool)? best = null;
            double bestScore = double.MaxValue;
            foreach (var node in sheet.Free)
            foreach (bool rotated in (ReadOnlySpan<bool>)[false, true])
            {
                var (w, h) = rotated ? (size.Height + gap, size.Width + gap) : (size.Width + gap, size.Height + gap);
                var f = node.Footprint;
                if (w > f.Width + Eps || h > f.Height + Eps) continue;
                double score = fitRule switch
                {
                    0 => f.Width * f.Height - w * h,             // best area fit
                    1 => Math.Min(f.Width - w, f.Height - h),     // best short side fit
                    _ => Math.Max(f.Width - w, f.Height - h),     // best long side fit
                };
                if (score < bestScore) (bestScore, best) = (score, (sheet, node, rotated));
            }

            if (best is not null) return best;
            sheet.Failed.Add(key);
        }

        return null;
    }

    // The visual goes in the top-left corner of the piece; what is left is split in two (on its right and below it).
    private static void Place(Sheet sheet, Node node, int item, SizeMm size, bool rotated, double gap, int splitRule)
    {
        sheet.Free.Remove(node);
        var f = node.Footprint;
        double w = size.Width + gap, h = size.Height + gap, right = f.Width - w, below = f.Height - h;
        bool acrossFirst = splitRule switch
        {
            0 => right <= below,                  // the shorter leftover first
            1 => right > below,
            2 => f.Width <= f.Height,             // across the shorter side of the piece
            3 => f.Width > f.Height,
            4 => w * below > right * h,           // one large leftover rather than two average ones
            _ => w * below <= right * h,
        };

        Node cell;
        if (acrossFirst)
        {
            // Across the whole piece under the visual, then the visual apart from what is on its right.
            var row = below > Eps ? Split(sheet, node, vertical: false, f.Y + h) : node;
            cell = right > Eps ? Split(sheet, row, vertical: true, f.X + w) : row;
        }
        else
        {
            var column = right > Eps ? Split(sheet, node, vertical: true, f.X + w) : node;
            cell = below > Eps ? Split(sheet, column, vertical: false, f.Y + h) : column;
        }

        cell.Placed = sheet.Placed.Count;
        sheet.Placed.Add(new Placed(item, new RectMm(f.X, f.Y, size.Width, size.Height), rotated));
    }

    // Splits a piece in two: the second part becomes free, the first is returned.
    private static Node Split(Sheet sheet, Node node, bool vertical, double at)
    {
        var f = node.Footprint;
        node.VerticalCut = vertical;
        node.First = new Node(vertical ? f with { Width = at - f.X } : f with { Height = at - f.Y });
        node.Second = new Node(vertical ? new RectMm(at, f.Y, f.Right - at, f.Height) : new RectMm(f.X, at, f.Width, f.Bottom - at));
        sheet.Free.Add(node.Second);
        return node.First;
    }

    // The block centred on the printable area, then its cuts, its white spaces and its crop marks.
    private static Packed Finish(Sheet sheet, PaperFormat format, RectMm printable, double bleed, double gap, bool cropMarks)
    {
        var block = SheetLayout.Bounds(sheet.Placed.Select(p => p.Trim));
        double dx = printable.CenterX - block.CenterX, dy = printable.CenterY - block.CenterY;
        var placed = sheet.Placed.Select(p => p with { Trim = p.Trim with { X = p.Trim.X + dx, Y = p.Trim.Y + dy } }).ToList();
        block = block with { X = block.X + dx, Y = block.Y + dy };

        var (cuts, pieces) = Cuts(sheet.Root, placed, new RectMm(0, 0, format.Width, format.Height));
        var spaces = gap > 2 * bleed + Eps ? Spaces([.. placed.Select(p => p.Trim.Inflate(bleed))]) : [];
        var marks = cropMarks ? CropMarks(cuts, block, bleed, printable) : [];
        var reading = placed.OrderBy(p => Math.Round(p.Trim.Y, 3)).ThenBy(p => p.Trim.X).ToList();
        return new Packed(reading, cuts, pieces, spaces, marks);
    }

    /// <summary>
    /// Crop marks of a mixed sheet: outside the block, in line with each cut that reaches its edge, the edges of the
    /// block included (a mark at each corner). A cut inside a piece reaches no edge: the cutting plan gives it. As in
    /// the grid layouts: 1 mm from the bleed, up to 5 mm long, never in the unprintable margin.
    /// </summary>
    private static List<LineMm> CropMarks(List<LineMm> cuts, RectMm block, double bleed, RectMm printable)
    {
        const double offset = LayoutOptions.CropMarkOffsetMm, length = LayoutOptions.CropMarkMaxLengthMm;
        var outer = block.Inflate(bleed);
        var marks = new List<LineMm>();
        void Add(LineMm m)
        {
            if (!marks.Any(k => Math.Abs(k.X1 - m.X1) < Eps && Math.Abs(k.Y1 - m.Y1) < Eps && Math.Abs(k.X2 - m.X2) < Eps && Math.Abs(k.Y2 - m.Y2) < Eps))
                marks.Add(m);
        }

        foreach (var c in cuts)
        {
            if (Math.Abs(c.X1 - c.X2) < Eps)
            {
                double x = c.X1, top = Math.Min(c.Y1, c.Y2), bottom = Math.Max(c.Y1, c.Y2);
                if (Math.Abs(top - block.Y) < Eps)
                    Add(new LineMm(x, outer.Y - offset, x, Math.Max(printable.Y, outer.Y - offset - length)));
                if (Math.Abs(bottom - block.Bottom) < Eps)
                    Add(new LineMm(x, outer.Bottom + offset, x, Math.Min(printable.Bottom, outer.Bottom + offset + length)));
            }
            else
            {
                double y = c.Y1, left = Math.Min(c.X1, c.X2), right = Math.Max(c.X1, c.X2);
                if (Math.Abs(left - block.X) < Eps)
                    Add(new LineMm(outer.X - offset, y, Math.Max(printable.X, outer.X - offset - length), y));
                if (Math.Abs(right - block.Right) < Eps)
                    Add(new LineMm(outer.Right + offset, y, Math.Min(printable.Right, outer.Right + offset + length), y));
            }
        }

        return marks;
    }

    private static MixedSheet ToSheet(Packed packed, IReadOnlyList<MixedItem> items, LayoutOptions options, RectMm printable)
    {
        var cells = packed.Placed.Select((p, i) =>
        {
            var margins = items[p.Item].Margins;
            return new LayoutCell(i, 0, p.Trim, p.Trim.Inflate(options.BleedMm), p.Rotated ? margins.RotatedClockwise : margins, p.Rotated);
        }).ToList();
        var layout = new SheetLayout(options, printable, cells, packed.Cuts, packed.Pieces, packed.Spaces, packed.Marks);
        return new MixedSheet(layout, [.. packed.Placed.Select(p => p.Item)], [.. packed.Placed.Select(p => (VisualInfo?)items[p.Item].Visual)]);
    }

    // ---- Cuts ----

    /// <summary>
    /// Cuts in cutting order: the edges of the block first (not those on the edge of the sheet), then piece by piece,
    /// each cut across the whole piece: the cut between the two parts of a split, then each part, trimmed to its
    /// visuals first (with a space between the parts, its far side is cut there). With each cut, the piece of paper it
    /// cuts (for its measurement on the cutting plan).
    /// </summary>
    private static (List<LineMm> Cuts, List<RectMm> Pieces) Cuts(Node root, List<Placed> placed, RectMm sheet)
    {
        var bounds = new Dictionary<Node, RectMm?>();
        RectMm? BoundsOf(Node n)
        {
            if (bounds.TryGetValue(n, out var b)) return b;
            if (n.Placed >= 0) b = placed[n.Placed].Trim;
            else if (n.First is not null)
            {
                var (first, second) = (BoundsOf(n.First), BoundsOf(n.Second!));
                b = first is null ? second : second is null ? first : SheetLayout.Bounds([first.Value, second.Value]);
            }
            return bounds[n] = b;
        }

        var cuts = new List<LineMm>();
        var pieces = new List<RectMm>();
        void Add(LineMm cut, RectMm piece) { cuts.Add(cut); pieces.Add(piece); }

        // The edges of the block, drawn along the block, each across what is left of the sheet.
        var block = BoundsOf(root)!.Value;
        var rest = sheet;
        if (block.Y > sheet.Y + Eps) { Add(new LineMm(block.X, block.Y, block.Right, block.Y), rest); rest = Below(rest, block.Y); }
        if (block.X > sheet.X + Eps) { Add(new LineMm(block.X, block.Y, block.X, block.Bottom), rest); rest = RightOf(rest, block.X); }
        if (block.Right < sheet.Right - Eps) { Add(new LineMm(block.Right, block.Y, block.Right, block.Bottom), rest); rest = LeftOf(rest, block.Right); }
        if (block.Bottom < sheet.Bottom - Eps) Add(new LineMm(block.X, block.Bottom, block.Right, block.Bottom), rest);

        void Walk(Node node, RectMm piece)
        {
            if (BoundsOf(node) is not { } b) return;
            // Trimmed to its visuals, one side after the other, each cut across what is left of the piece.
            if (b.Y > piece.Y + Eps) { Add(new LineMm(piece.X, b.Y, piece.Right, b.Y), piece); piece = Below(piece, b.Y); }
            if (b.X > piece.X + Eps) { Add(new LineMm(b.X, piece.Y, b.X, piece.Bottom), piece); piece = RightOf(piece, b.X); }
            if (b.Bottom < piece.Bottom - Eps) { Add(new LineMm(piece.X, b.Bottom, piece.Right, b.Bottom), piece); piece = Above(piece, b.Bottom); }
            if (b.Right < piece.Right - Eps) { Add(new LineMm(b.Right, piece.Y, b.Right, piece.Bottom), piece); piece = LeftOf(piece, b.Right); }
            if (node.First is null) return;

            if (BoundsOf(node.First) is not { } first) { Walk(node.Second!, piece); return; }
            if (BoundsOf(node.Second!) is null) { Walk(node.First, piece); return; }
            if (node.VerticalCut)
            {
                Add(new LineMm(first.Right, piece.Y, first.Right, piece.Bottom), piece);
                Walk(node.First, LeftOf(piece, first.Right));
                Walk(node.Second!, RightOf(piece, first.Right));
            }
            else
            {
                Add(new LineMm(piece.X, first.Bottom, piece.Right, first.Bottom), piece);
                Walk(node.First, Above(piece, first.Bottom));
                Walk(node.Second!, Below(piece, first.Bottom));
            }
        }

        Walk(root, block);
        return (cuts, pieces);
    }

    // What is left of a piece on one side of a cut.
    private static RectMm Above(RectMm r, double y) => r with { Height = y - r.Y };
    private static RectMm Below(RectMm r, double y) => new(r.X, y, r.Width, r.Bottom - y);
    private static RectMm LeftOf(RectMm r, double x) => r with { Width = x - r.X };
    private static RectMm RightOf(RectMm r, double x) => new(x, r.Y, r.Right - x, r.Height);

    // ---- White spaces ----

    /// <summary>
    /// What the bleeds leave uncovered inside the block, as rectangles: a grid on every edge of a bleed, its free cells
    /// gathered in runs along each band, then down the bands. A space wider than the bleeds is painted white, as in
    /// the other modes (Windows rounds clipping paths to whole pixels: a thin space would vanish here and there).
    /// </summary>
    private static List<RectMm> Spaces(List<RectMm> bleeds)
    {
        var block = SheetLayout.Bounds(bleeds);
        var xs = Distinct(bleeds.SelectMany(r => new[] { r.X, r.Right }).Append(block.X).Append(block.Right));
        var ys = Distinct(bleeds.SelectMany(r => new[] { r.Y, r.Bottom }).Append(block.Y).Append(block.Bottom));

        var spaces = new List<RectMm>();
        var open = new Dictionary<(double, double), int>(); // runs of the band above: index in spaces
        for (int j = 0; j + 1 < ys.Count; j++)
        {
            double y0 = ys[j], y1 = ys[j + 1], cy = (y0 + y1) / 2;
            bool Covered(int i)
            {
                double cx = (xs[i] + xs[i + 1]) / 2;
                return bleeds.Any(r => cx > r.X && cx < r.Right && cy > r.Y && cy < r.Bottom);
            }

            var runs = new Dictionary<(double, double), int>();
            for (int i = 0; i + 1 < xs.Count; i++)
            {
                if (Covered(i)) continue;
                int start = i;
                while (i + 2 < xs.Count && !Covered(i + 1)) i++;
                var run = (xs[start], xs[i + 1]);
                if (open.TryGetValue(run, out int k))
                {
                    spaces[k] = spaces[k] with { Height = y1 - spaces[k].Y };
                    runs[run] = k;
                }
                else
                {
                    runs[run] = spaces.Count;
                    spaces.Add(new RectMm(run.Item1, y0, run.Item2 - run.Item1, y1 - y0));
                }
            }

            open = runs;
        }

        return spaces;
    }

    private static List<double> Distinct(IEnumerable<double> values)
    {
        var result = new List<double>();
        foreach (var v in values.Order())
            if (result.Count == 0 || v - result[^1] > Eps)
                result.Add(v);
        return result;
    }
}
