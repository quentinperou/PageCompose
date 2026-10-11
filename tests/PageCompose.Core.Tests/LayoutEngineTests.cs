using PageCompose.Core;
using PageCompose.Core.Layout;

namespace PageCompose.Core.Tests;

public class LayoutEngineTests
{
    private const double Bleed = 2.0;

    /// <summary>
    /// Reference table of layouts on the ApeosPro sheets (4.1 mm margin, 3 mm crop marks, 2 mm bleed), computed
    /// separately at design time; unchanged when the photo sizes moved to exact inches (10 x 15 = 101.6 x 152.4).
    /// Columns: edge to edge, + crop marks, 2 mm bleed + crop marks.
    /// </summary>
    public static TheoryData<string, string, int, int, int> Reference => new()
    {
        { "SRA3", "BusinessCard", 25, 25, 21 },
        { "SRA3", "IdPhoto", 72, 72, 66 },
        { "SRA3", "Photo9x13", 9, 9, 9 },
        { "SRA3", "Photo10x15", 8, 8, 4 },
        { "SRA3", "Photo13x18", 4, 4, 4 },
        { "SRA3", "Photo15x20", 4, 4, 2 },
        { "SRA3", "Photo20x30", 2, 2, 1 },
        { "SRA3", "Square10", 12, 12, 8 },
        { "SRA3", "Square15", 4, 4, 2 },
        { "SRA3", "Square10Border", 12, 12, 8 },
        { "SRA3", "Polaroid", 12, 12, 9 },
        { "SRA3", "InstaxMini", 25, 25, 21 },
        { "SRA3", "InstaxSquare", 20, 20, 16 },
        { "SRA3", "InstaxWide", 12, 12, 9 },
        { "SRA3", "PostcardA6", 8, 8, 6 },
        { "SRA3", "FlyerDL", 6, 6, 4 },
        { "SRA3", "FlyerA5", 4, 4, 4 },
        { "SRA3", "A4", 2, 2, 2 },
        { "A3", "BusinessCard", 21, 21, 18 },
        { "A3", "IdPhoto", 72, 72, 56 },
        { "A3", "Photo9x13", 9, 9, 9 },
        { "A3", "Photo10x15", 4, 4, 4 },
        { "A3", "Photo13x18", 4, 4, 4 },
        { "A3", "Photo15x20", 2, 2, 2 },
        { "A3", "Photo20x30", 1, 1, 1 },
        { "A3", "Square10", 8, 8, 6 },
        { "A3", "Square15", 2, 2, 2 },
        { "A3", "Square10Border", 8, 8, 6 },
        { "A3", "Polaroid", 9, 9, 9 },
        { "A3", "InstaxMini", 21, 21, 18 },
        { "A3", "InstaxSquare", 16, 15, 15 },
        { "A3", "InstaxWide", 9, 9, 9 },
        { "A3", "PostcardA6", 4, 4, 4 },
        { "A3", "FlyerDL", 4, 4, 3 },
        { "A3", "FlyerA5", 2, 2, 2 },
        { "A3", "A4", 1, 1, 1 },
        { "A4", "BusinessCard", 10, 10, 9 },
        { "A4", "IdPhoto", 32, 32, 25 },
        { "A4", "Photo9x13", 4, 4, 4 },
        { "A4", "Photo10x15", 2, 2, 2 },
        { "A4", "Photo13x18", 2, 2, 2 },
        { "A4", "Photo15x20", 1, 1, 1 },
        { "A4", "Photo20x30", 0, 0, 0 },
        { "A4", "Square10", 4, 2, 2 },
        { "A4", "Square15", 1, 1, 1 },
        { "A4", "Square10Border", 4, 2, 2 },
        { "A4", "Polaroid", 4, 4, 4 },
        { "A4", "InstaxMini", 10, 10, 9 },
        { "A4", "InstaxSquare", 8, 6, 6 },
        { "A4", "InstaxWide", 4, 4, 4 },
        { "A4", "PostcardA6", 2, 2, 2 },
        { "A4", "FlyerDL", 2, 1, 1 },
        { "A4", "FlyerA5", 1, 1, 1 },
        { "A4", "A4", 0, 0, 0 },
    };

    [Theory]
    [MemberData(nameof(Reference))]
    public void PerSheet_matches_reference(string sheet, string item, int edgeToEdge, int withMarks, int withBleedAndMarks)
    {
        var s = Sheet(sheet);
        var f = ItemFormats.All.Single(x => x.Id == item);
        var (i, m) = (f.Size, f.Margins); // margins are inside the size: they do not change the count

        Assert.Equal(edgeToEdge, LayoutEngine.Compute(new LayoutOptions(s, i, Margins: m)).PerSheet);
        Assert.Equal(withMarks, LayoutEngine.Compute(new LayoutOptions(s, i, 0, true, Margins: m)).PerSheet);
        Assert.Equal(withBleedAndMarks, LayoutEngine.Compute(new LayoutOptions(s, i, Bleed, true, Margins: m)).PerSheet);
    }

    public static IEnumerable<object[]> AllCombinations() =>
        from sheet in SheetFormats.All
        from item in ItemFormats.All
        from bleed in new[] { 0.0, Bleed }
        from marks in new[] { false, true }
        select new object[] { sheet.Id, item.Id, bleed, marks };

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void Geometry_stays_inside_printable_area(string sheetName, string itemName, double bleed, bool marks)
    {
        var sheet = SheetFormats.All.Single(f => f.Id == sheetName);
        var item = ItemFormats.All.Single(f => f.Id == itemName).Size;
        var layout = LayoutEngine.Compute(new LayoutOptions(sheet, item, bleed, marks));
        if (layout.IsEmpty) return;

        Assert.Equal(layout.Columns * layout.Rows, layout.Cells.Count);
        foreach (var cell in layout.Cells)
        {
            Assert.True(layout.PrintableArea.Contains(cell.Bleed), $"cell outside the printable area: {cell}");
            Assert.Equal(layout.PlacedItem.Width, cell.Trim.Width, 6);
            Assert.Equal(layout.PlacedItem.Height, cell.Trim.Height, 6);
        }

        // Block centred on the sheet.
        var block = layout.Block!.Value;
        Assert.Equal(sheet.Width / 2, block.CenterX, 6);
        Assert.Equal(sheet.Height / 2, block.CenterY, 6);

        // Cells do not overlap, bleed included.
        foreach (var a in layout.Cells)
        foreach (var b in layout.Cells.Where(c => c != a))
            Assert.False(a.Bleed.X < b.Bleed.Right - 1e-6 && b.Bleed.X < a.Bleed.Right - 1e-6 &&
                         a.Bleed.Y < b.Bleed.Bottom - 1e-6 && b.Bleed.Y < a.Bleed.Bottom - 1e-6);

        if (!marks)
        {
            Assert.Empty(layout.CropMarks);
            return;
        }

        Assert.Equal(2 * (layout.CutsX.Count + layout.CutsY.Count), layout.CropMarks.Count);
        foreach (var m in layout.CropMarks)
        {
            foreach (var (x, y) in new[] { (m.X1, m.Y1), (m.X2, m.Y2) })
            {
                Assert.InRange(x, layout.PrintableArea.X - 1e-6, layout.PrintableArea.Right + 1e-6);
                Assert.InRange(y, layout.PrintableArea.Y - 1e-6, layout.PrintableArea.Bottom + 1e-6);
                bool insideBlock = x > block.X + 1e-6 && x < block.Right - 1e-6 && y > block.Y + 1e-6 && y < block.Bottom - 1e-6;
                Assert.False(insideBlock, "a crop mark overlaps the visuals");
            }

            double length = Math.Abs(m.X2 - m.X1) + Math.Abs(m.Y2 - m.Y1);
            Assert.True(length >= LayoutOptions.CropMarkReserveMm - LayoutOptions.CropMarkOffsetMm - 1e-6, $"crop mark too short: {length}");
        }
    }

    [Fact]
    public void Bleed_mode_separates_cells_by_twice_the_bleed()
    {
        var layout = LayoutEngine.Compute(new LayoutOptions(SheetFormats.Sra3, ItemFormats.BusinessCard.Size, Bleed, true));

        var row0 = layout.Cells.Where(c => c.Row == 0).OrderBy(c => c.Column).ToList();
        Assert.Equal(2 * Bleed, row0[1].Trim.X - row0[0].Trim.Right, 6);
        Assert.Equal(2 * layout.Columns, layout.CutsX.Count); // two cuts per cell
    }

    [Fact]
    public void Edge_to_edge_cells_share_cut_lines()
    {
        var layout = LayoutEngine.Compute(new LayoutOptions(SheetFormats.Sra3, ItemFormats.Photo10x15.Size, 0, true));

        Assert.Equal(8, layout.PerSheet);
        Assert.True(layout.ItemRotated);
        Assert.Equal(layout.Columns + 1, layout.CutsX.Count);
        Assert.Equal(layout.Rows + 1, layout.CutsY.Count);
    }

    [Theory]
    [InlineData("Lab20x30", "Photo10x15", 4)]  // 4 x 6 in prints: four of them make one 8 x 12 in
    [InlineData("Lab30x45", "Photo10x15", 9)]
    [InlineData("Lab30x45", "Photo15x20", 4)]
    [InlineData("Lab10x15", "Photo10x15", 1)]
    [InlineData("Lab10x15", "InstaxMini", 2)]
    [InlineData("Lab10x15", "IdPhoto", 8)]     // a sheet of ID photos
    public void Lab_prints_are_printed_to_the_edge(string sheetId, string itemId, int expected)
    {
        var sheet = SheetFormats.All.Single(s => s.Id == sheetId);
        var layout = LayoutEngine.Compute(new LayoutOptions(sheet, ItemFormats.All.Single(f => f.Id == itemId).Size));

        Assert.Equal(new RectMm(0, 0, sheet.Width, sheet.Height), layout.PrintableArea);
        Assert.Equal(expected, layout.PerSheet);
    }

    [Fact]
    public void Printable_area_follows_the_sheet_margins()
    {
        var sra3 = LayoutEngine.Compute(new LayoutOptions(SheetFormats.Sra3, ItemFormats.BusinessCard.Size)).PrintableArea;
        Assert.Equal(4.1, sra3.X, 6);
        Assert.Equal(4.1, sra3.Y, 6);
        Assert.Equal(311.8, sra3.Width, 6);
        Assert.Equal(441.8, sra3.Height, 6);

        var custom = new PaperFormat("Custom", 200, 300, new MarginsMm(10, 0, 5, 2));
        Assert.Equal(new RectMm(2, 10, 198, 285), LayoutEngine.Compute(
            new LayoutOptions(custom, ItemFormats.BusinessCard.Size)).PrintableArea);
    }

    [Fact]
    public void Sheet_margins_that_leave_nothing_give_an_empty_layout()
    {
        var sheet = new PaperFormat("Tiny", 20, 30, new MarginsMm(10, 10, 10, 10));

        Assert.True(LayoutEngine.Compute(new LayoutOptions(sheet, new SizeMm(5, 5))).IsEmpty);
        Assert.Throws<ArgumentOutOfRangeException>(() => LayoutEngine.Compute(
            new LayoutOptions(sheet with { Margins = new MarginsMm(-1, 0, 0, 0) }, new SizeMm(5, 5))));
    }

    [Fact]
    public void Too_large_item_gives_empty_layout()
    {
        var layout = LayoutEngine.Compute(new LayoutOptions(SheetFormats.A4, ItemFormats.A4.Size));

        Assert.True(layout.IsEmpty);
        Assert.Null(layout.Block);
        Assert.Empty(layout.Cells);
    }

    [Theory]
    [InlineData(100, 30, 0, 3)]
    [InlineData(99.9, 30, 0, 3)]
    [InlineData(89.9, 30, 0, 2)]
    [InlineData(100, 30, 4, 3)]
    [InlineData(97.9, 30, 4, 2)]
    [InlineData(10, 30, 0, 0)]
    public void Fit_counts_items_with_gaps(double avail, double size, double gap, int expected) =>
        Assert.Equal(expected, LayoutEngine.Fit(avail, size, gap));

    private static PaperFormat Sheet(string key) => key switch
    {
        "SRA3" => SheetFormats.Sra3,
        "A3" => SheetFormats.A3,
        _ => SheetFormats.A4,
    };
}
