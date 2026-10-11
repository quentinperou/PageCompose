using PageCompose.Core.Layout;
using PageCompose.Core.Pdf;
using PageCompose.Core.Visuals;

namespace PageCompose.Core.Tests;

public class MixedPackerTests
{
    private const double Eps = 1e-6;

    private static readonly SizeMm Photo20x25 = new(8 * 25.4, 10 * 25.4);

    private static VisualInfo Image(string name, double w = 1200, double h = 1800) => new()
    {
        Path = name + ".jpg",
        Kind = VisualKind.Image,
        FullWidth = w,
        FullHeight = h,
        Content = new RawBox(0, 0, w, h),
    };

    private static MixedItem Item(string name, SizeMm size, int copies, MarginsMm margins = default) =>
        new(Image(name), size, copies, margins);

    // The three orders of the simulations shown to the author (October 10, 2026).
    public static TheoryData<string, double, int> Orders => new()
    {
        { "mixed", 0, 2 }, { "mixed", 3, 2 },
        { "cards", 0, 2 }, { "cards", 3, 2 },
        { "large", 0, 4 }, { "large", 3, 4 },
    };

    private static List<MixedItem> Order(string name) => name switch
    {
        "mixed" => [Item("a", ItemFormats.Photo10x15.Size, 4), Item("b", ItemFormats.Photo13x18.Size, 2), Item("c", Photo20x25, 1)],
        "cards" => [Item("a", ItemFormats.Photo15x20.Size, 2), Item("b", ItemFormats.Photo10x15.Size, 3), Item("c", ItemFormats.BusinessCard.Size, 10)],
        _ => [Item("a", ItemFormats.Photo10x15.Size, 12), Item("b", ItemFormats.Photo13x18.Size, 6), Item("c", ItemFormats.Photo20x30.Size, 2)],
    };

    [Theory]
    [MemberData(nameof(Orders))]
    public void Orders_of_the_simulations_save_a_sheet(string order, double spacing, int sheets)
    {
        var items = Order(order);

        var plan = MixedPacker.Pack(SheetFormats.Sra3, items, spacingMm: spacing);

        // One layout per size (Compose mode) takes one sheet more each time.
        Assert.Equal(sheets, plan.SheetCount);
        Assert.Equal(items.Sum(i => i.Copies), plan.Printed);
        Assert.Empty(plan.TooLarge);
    }

    [Theory]
    [MemberData(nameof(Orders))]
    public void Every_sheet_can_be_cut_on_a_guillotine(string order, double spacing, int _)
    {
        foreach (double bleed in new[] { 0.0, 2.0 })
        {
            var plan = MixedPacker.Pack(SheetFormats.Sra3, Order(order), bleed, spacing);
            foreach (var sheet in plan.Sheets)
                AssertCuttable(sheet.Layout);
        }
    }

    [Fact]
    public void Every_item_is_laid_as_many_times_as_its_copies()
    {
        var items = Order("cards");

        var plan = MixedPacker.Pack(SheetFormats.Sra3, items);

        for (int i = 0; i < items.Count; i++)
            Assert.Equal(items[i].Copies, plan.Sheets.Sum(s => s.Items.Count(k => k == i)));
        foreach (var sheet in plan.Sheets)
        {
            Assert.Equal(sheet.Layout.PerSheet, sheet.Items.Count);
            Assert.Equal(sheet.Items.Select(i => items[i].Visual), sheet.Visuals);
            // Each cell has the size of its item, turned or not.
            foreach (var (cell, item) in sheet.Layout.Cells.Zip(sheet.Items))
                Assert.Equal(cell.Rotated ? items[item].Size.Rotated : items[item].Size, cell.Trim.Size);
        }
    }

    [Fact]
    public void The_block_is_centred_on_the_sheet()
    {
        var plan = MixedPacker.Pack(SheetFormats.Sra3, Order("mixed"));

        foreach (var sheet in plan.Sheets)
        {
            var block = sheet.Layout.Block!.Value;
            Assert.Equal(160, block.CenterX, 6);
            Assert.Equal(225, block.CenterY, 6);
            Assert.True(sheet.Layout.IsMixed);
        }
    }

    [Fact]
    public void A_full_lab_print_needs_no_cut_on_its_edges()
    {
        // Four 10 x 15 fill a 20 x 30 exactly: three cuts make four pieces, none along the edges of the print.
        var plan = MixedPacker.Pack(SheetFormats.Lab20x30, [Item("a", ItemFormats.Photo10x15.Size, 4)]);

        var sheet = Assert.Single(plan.Sheets);
        Assert.Equal(4, sheet.Layout.PerSheet);
        Assert.Equal(3, sheet.Layout.CutSegments.Count);
        AssertCuttable(sheet.Layout);
    }

    [Fact]
    public void An_item_too_large_for_the_sheet_is_left_out()
    {
        var items = new List<MixedItem> { Item("poster", new SizeMm(300, 400), 1), Item("a", ItemFormats.Photo10x15.Size, 2) };

        var plan = MixedPacker.Pack(SheetFormats.A4, items);

        Assert.Equal([0], plan.TooLarge);
        Assert.Equal(2, plan.Printed);
        Assert.All(plan.Sheets.SelectMany(s => s.Items), i => Assert.Equal(1, i));
    }

    [Fact]
    public void Nothing_to_lay_gives_no_sheet()
    {
        Assert.Equal(0, MixedPacker.Pack(SheetFormats.Sra3, []).SheetCount);
        Assert.Equal(0, MixedPacker.Pack(SheetFormats.Sra3, [Item("a", ItemFormats.Photo10x15.Size, 0)]).SheetCount);
    }

    [Fact]
    public void Margins_of_a_turned_frame_turn_clockwise()
    {
        var polaroid = ItemFormats.Polaroid;
        var items = new List<MixedItem>
        {
            Item("p", polaroid.Size, 7, polaroid.Margins),
            Item("a", ItemFormats.Photo13x18.Size, 3),
        };

        var plan = MixedPacker.Pack(SheetFormats.Sra3, items);

        var cells = plan.Sheets.SelectMany(s => s.Layout.Cells.Zip(s.Items)).Where(c => c.Second == 0).Select(c => c.First).ToList();
        Assert.Equal(7, cells.Count);
        Assert.All(cells, c => Assert.Equal(c.Rotated ? polaroid.Margins.RotatedClockwise : polaroid.Margins, c.Margins));
        Assert.All(cells, c => Assert.Equal(79, c.ImageTrim.Width, 6));
    }

    [Fact]
    public void A_space_wider_than_the_bleeds_is_painted_white_and_only_there()
    {
        var spaced = MixedPacker.Pack(SheetFormats.Sra3, Order("mixed"), 0, 3);
        var bled = MixedPacker.Pack(SheetFormats.Sra3, Order("mixed"), 2, 3); // gap of 4 mm, filled by the bleeds

        foreach (var sheet in spaced.Sheets)
        {
            var layout = sheet.Layout;
            var block = layout.Block!.Value;
            // The spaces and the visuals share the block exactly, without overlapping.
            double covered = layout.Spaces.Sum(s => s.Width * s.Height) + layout.Cells.Sum(c => c.Trim.Width * c.Trim.Height);
            Assert.Equal(block.Width * block.Height, covered, 3);
            Assert.All(layout.Spaces, s => Assert.True(block.Contains(s)));
            Assert.DoesNotContain(layout.Spaces, s => layout.Cells.Any(c => Overlap(s, c.Trim)));
        }
        Assert.All(bled.Sheets, s => Assert.Empty(s.Layout.Spaces));
    }

    [Fact]
    public void Each_cut_is_measured_from_an_edge_the_clamp_can_hold()
    {
        var plan = MixedPacker.Pack(SheetFormats.Sra3, Order("cards"), 0, 3);
        var edges = new List<CutEdge>();

        foreach (var layout in plan.Sheets.Select(s => s.Layout))
        {
            Assert.Equal(layout.CutSegments.Count, layout.CutPieces.Count);
            for (int i = 0; i < layout.CutSegments.Count; i++)
            {
                var (cut, piece) = (layout.CutSegments[i], layout.CutPieces[i]);
                var (mm, edge) = layout.CutMeasurement(i);
                bool across = edge is CutEdge.Top or CutEdge.Bottom;
                Assert.Equal(Math.Abs(cut.Y1 - cut.Y2) < Eps, across);
                double size = across ? piece.Height : piece.Width;
                double fromFirst = across ? cut.Y1 - piece.Y : cut.X1 - piece.X;

                // Inside its piece, measured from the edge named, and that part is wide enough to be held.
                Assert.InRange(fromFirst, Eps, size - Eps);
                Assert.Equal(edge is CutEdge.Top or CutEdge.Left ? fromFirst : size - fromFirst, mm, 6);
                Assert.True(mm >= SheetLayout.MinHeldMm || mm >= size - mm, $"Cut {i + 1}: {mm} mm held, out of {size}");
                edges.Add(edge);
            }
        }

        // The 3 mm spaces are measured from the far edge.
        Assert.Contains(edges, e => e is CutEdge.Bottom or CutEdge.Right);
    }

    [Fact]
    public void Cuts_between_photos_measure_the_size_of_the_photos()
    {
        var plan = MixedPacker.Pack(SheetFormats.Lab20x30, [Item("a", ItemFormats.Photo10x15.Size, 4)]);
        var layout = Assert.Single(plan.Sheets).Layout;

        Assert.Equal(3, layout.CutSegments.Count);
        for (int i = 0; i < layout.CutSegments.Count; i++)
        {
            double mm = layout.CutMeasurement(i).Mm;
            Assert.True(Math.Abs(mm - 101.6) < 1e-6 || Math.Abs(mm - 152.4) < 1e-6, $"Cut {i + 1}: {mm} mm");
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 0)]
    [InlineData(0, 3)]
    public void Crop_marks_stand_outside_the_block_in_line_with_the_cuts_reaching_its_edge(double bleed, double spacing)
    {
        var plan = MixedPacker.Pack(SheetFormats.Sra3, Order("cards"), bleed, spacing, cropMarks: true);

        foreach (var layout in plan.Sheets.Select(s => s.Layout))
        {
            var block = SheetLayout.Bounds(layout.Cells.Select(c => c.Trim));
            var outer = block.Inflate(bleed);
            // Room kept for them, as in the grid layouts.
            Assert.True(layout.PrintableArea.Contains(outer.Inflate(LayoutOptions.CropMarkReserveMm)));
            Assert.True(layout.CropMarks.Count >= 8, $"{layout.CropMarks.Count} marks"); // two at each corner at least
            foreach (var m in layout.CropMarks)
            {
                // Outside the block and its bleed, inside the printable area.
                Assert.True(layout.PrintableArea.Contains(new RectMm(Math.Min(m.X1, m.X2), Math.Min(m.Y1, m.Y2),
                    Math.Abs(m.X2 - m.X1), Math.Abs(m.Y2 - m.Y1))));
                Assert.False(m.X1 > outer.X && m.X1 < outer.Right && m.Y1 > outer.Y && m.Y1 < outer.Bottom);
                // In line with a cut that ends on the edge of the block.
                bool down = Math.Abs(m.X1 - m.X2) < Eps;
                Assert.Contains(layout.CutSegments, c => down
                    ? Math.Abs(c.X1 - m.X1) < Eps && Math.Abs(c.X2 - m.X1) < Eps &&
                      (Math.Abs(Math.Min(c.Y1, c.Y2) - block.Y) < Eps || Math.Abs(Math.Max(c.Y1, c.Y2) - block.Bottom) < Eps)
                    : Math.Abs(c.Y1 - m.Y1) < Eps && Math.Abs(c.Y2 - m.Y1) < Eps &&
                      (Math.Abs(Math.Min(c.X1, c.X2) - block.X) < Eps || Math.Abs(Math.Max(c.X1, c.X2) - block.Right) < Eps));
            }
            AssertCuttable(layout);
        }

        Assert.All(MixedPacker.Pack(SheetFormats.Sra3, Order("cards")).Sheets, s => Assert.Empty(s.Layout.CropMarks));
    }

    [Fact]
    public void Crop_marks_cost_room_on_a_print_without_margins()
    {
        // Four 10 x 15 fill a 20 x 30 to its edges: with the room of the crop marks, they no longer fit on one.
        var items = new List<MixedItem> { Item("a", ItemFormats.Photo10x15.Size, 4) };

        Assert.Equal(1, MixedPacker.Pack(SheetFormats.Lab20x30, items).SheetCount);
        Assert.True(MixedPacker.Pack(SheetFormats.Lab20x30, items, cropMarks: true).SheetCount > 1);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(2, true)]
    public void Numbers_of_the_edges_of_the_block_stand_outside_it(double bleed, bool cropMarks)
    {
        const double size = 12;
        var layout = MixedPacker.Pack(SheetFormats.Sra3, Order("mixed"), bleed, 0, cropMarks).Sheets[0].Layout;
        var block = SheetLayout.Bounds(layout.Cells.Select(c => c.Trim));
        var outer = block.Inflate(bleed);

        var at = MixedPacker.NumberPositions(layout, size);

        Assert.Equal(layout.CutSegments.Count, at.Count);
        int outside = 0;
        for (int i = 0; i < at.Count; i++)
        {
            var (x, y) = at[i];
            if (Edges(block).Any(e => Covers(e, layout.CutSegments[i])))
            {
                // Beside the block and its bleed, the whole number on the sheet.
                Assert.False(x > outer.X - Eps && x < outer.Right + Eps && y > outer.Y - Eps && y < outer.Bottom + Eps);
                Assert.True(new RectMm(0, 0, 320, 450).Contains(new RectMm(x - size / 2, y - size / 2, size, size)));
                outside++;
            }
            else
            {
                Assert.True(OnSegment(x, y, layout.CutSegments[i]), $"Number {i + 1} is not on its cut");
            }
        }
        Assert.Equal(4, outside);
    }

    [Fact]
    public void Two_close_cuts_get_their_numbers_apart()
    {
        // Both sides of a 3 mm space: the numbers must not sit on top of each other.
        LineMm[] cuts = [new(100, 0, 100, 200), new(103, 0, 103, 200), new(0, 50, 100, 50)];

        var at = MixedPacker.NumberPositions(cuts, 10);

        Assert.Equal(3, at.Count);
        for (int i = 0; i < at.Count; i++)
        for (int j = i + 1; j < at.Count; j++)
            Assert.True(Math.Sqrt(Math.Pow(at[i].X - at[j].X, 2) + Math.Pow(at[i].Y - at[j].Y, 2)) >= 10 - Eps);
        Assert.Equal((100, 100), at[0]); // the middle of the cut when there is room
    }

    [Fact]
    public void The_same_order_always_gives_the_same_sheets()
    {
        var a = MixedPacker.Pack(SheetFormats.Sra3, Order("large"), 0, 3);
        var b = MixedPacker.Pack(SheetFormats.Sra3, Order("large"), 0, 3);

        Assert.Equal(a.Sheets.Select(s => s.Layout.Cells.Select(c => c.Trim)).SelectMany(t => t),
            b.Sheets.Select(s => s.Layout.Cells.Select(c => c.Trim)).SelectMany(t => t));
        Assert.Equal(a.Sheets.SelectMany(s => s.Layout.CutSegments), b.Sheets.SelectMany(s => s.Layout.CutSegments));
    }

    [Fact]
    public void A_large_order_is_laid_quickly()
    {
        var items = new List<MixedItem>
        {
            Item("a", ItemFormats.Photo10x15.Size, 500), Item("b", ItemFormats.Photo13x18.Size, 200),
            Item("c", ItemFormats.BusinessCard.Size, 200), Item("d", ItemFormats.Photo20x30.Size, 100),
        };

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var plan = MixedPacker.Pack(SheetFormats.Sra3, items);

        Assert.Equal(1000, plan.Printed);
        Assert.True(watch.ElapsedMilliseconds < 2000, $"{watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Wrong_settings_are_rejected()
    {
        var ok = Item("a", ItemFormats.Photo10x15.Size, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => MixedPacker.Pack(SheetFormats.Sra3, [ok], bleedMm: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MixedPacker.Pack(SheetFormats.Sra3, [ok], spacingMm: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MixedPacker.Pack(SheetFormats.Sra3, [Item("z", new SizeMm(0, 10), 1)]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MixedPacker.Pack(SheetFormats.Sra3, [Item("m", ItemFormats.BusinessCard.Size, 1, new MarginsMm(30, 0, 30, 0))]));
    }

    // ---- What a guillotine needs ----

    private static void AssertCuttable(SheetLayout layout)
    {
        var cells = layout.Cells;
        var cuts = layout.CutSegments;
        var gap = layout.Options.Gap;

        // Inside the printable area, bleed included, and apart by the gap at least.
        Assert.All(cells, c => Assert.True(layout.PrintableArea.Contains(c.Bleed), $"{c.Bleed} outside {layout.PrintableArea}"));
        for (int i = 0; i < cells.Count; i++)
        for (int j = i + 1; j < cells.Count; j++)
            Assert.False(Overlap(cells[i].Trim.Inflate(gap / 2 - Eps), cells[j].Trim.Inflate(gap / 2 - Eps)),
                $"{cells[i].Trim} and {cells[j].Trim} are closer than {gap} mm");

        // Every edge of every visual is cut (or is the edge of the sheet), and no cut goes through a visual.
        var sheet = new RectMm(0, 0, layout.Sheet.Width, layout.Sheet.Height);
        foreach (var cell in cells)
        foreach (var edge in Edges(cell.Trim))
            Assert.True(OnSheetEdge(edge, sheet) || cuts.Any(c => Covers(c, edge)), $"Edge {edge} of {cell.Trim} is not cut");
        foreach (var cut in cuts)
            Assert.DoesNotContain(cells, c => Crosses(cut, c.Trim));

        // Guillotine order: each cut ends on the edge of the block or on an earlier cut.
        var block = SheetLayout.Bounds(cells.Select(c => c.Trim));
        for (int i = 0; i < cuts.Count; i++)
            foreach (var (x, y) in new[] { (cuts[i].X1, cuts[i].Y1), (cuts[i].X2, cuts[i].Y2) })
                Assert.True(OnRectEdge(x, y, block) || cuts.Take(i).Any(c => OnSegment(x, y, c)),
                    $"Cut {i + 1} {cuts[i]} ends at ({x}, {y}), on nothing cut before");
    }

    private static bool Overlap(RectMm a, RectMm b) =>
        a.X < b.Right - Eps && b.X < a.Right - Eps && a.Y < b.Bottom - Eps && b.Y < a.Bottom - Eps;

    private static IEnumerable<LineMm> Edges(RectMm r) =>
    [
        new(r.X, r.Y, r.Right, r.Y), new(r.X, r.Bottom, r.Right, r.Bottom),
        new(r.X, r.Y, r.X, r.Bottom), new(r.Right, r.Y, r.Right, r.Bottom),
    ];

    private static bool Covers(LineMm cut, LineMm edge) => OnSegment(edge.X1, edge.Y1, cut) && OnSegment(edge.X2, edge.Y2, cut);

    private static bool OnSegment(double x, double y, LineMm c) =>
        Math.Abs(c.X1 - c.X2) < Eps
            ? Math.Abs(x - c.X1) < Eps && y >= Math.Min(c.Y1, c.Y2) - Eps && y <= Math.Max(c.Y1, c.Y2) + Eps
            : Math.Abs(y - c.Y1) < Eps && x >= Math.Min(c.X1, c.X2) - Eps && x <= Math.Max(c.X1, c.X2) + Eps;

    private static bool OnRectEdge(double x, double y, RectMm r) =>
        ((Math.Abs(x - r.X) < Eps || Math.Abs(x - r.Right) < Eps) && y >= r.Y - Eps && y <= r.Bottom + Eps) ||
        ((Math.Abs(y - r.Y) < Eps || Math.Abs(y - r.Bottom) < Eps) && x >= r.X - Eps && x <= r.Right + Eps);

    private static bool OnSheetEdge(LineMm edge, RectMm sheet) =>
        (Math.Abs(edge.X1 - edge.X2) < Eps && (Math.Abs(edge.X1 - sheet.X) < Eps || Math.Abs(edge.X1 - sheet.Right) < Eps)) ||
        (Math.Abs(edge.Y1 - edge.Y2) < Eps && (Math.Abs(edge.Y1 - sheet.Y) < Eps || Math.Abs(edge.Y1 - sheet.Bottom) < Eps));

    private static bool Crosses(LineMm cut, RectMm r) =>
        Math.Abs(cut.X1 - cut.X2) < Eps
            ? cut.X1 > r.X + Eps && cut.X1 < r.Right - Eps && Math.Max(cut.Y1, cut.Y2) > r.Y + Eps && Math.Min(cut.Y1, cut.Y2) < r.Bottom - Eps
            : cut.Y1 > r.Y + Eps && cut.Y1 < r.Bottom - Eps && Math.Max(cut.X1, cut.X2) > r.X + Eps && Math.Min(cut.X1, cut.X2) < r.Right - Eps;
}
