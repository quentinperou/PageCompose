using PageCompose.Core.Layout;
using PageCompose.Core.Pdf;

namespace PageCompose.Core.Tests;

public sealed class SheetMarkTests
{
    public static IEnumerable<object[]> AllLayouts() =>
        from sheet in SheetFormats.All
        from item in ItemFormats.All
        from bleed in new[] { 0.0, 2.0 }
        from marks in new[] { false, true }
        select new object[] { sheet.Id, item.Id, bleed, marks };

    [Theory]
    [MemberData(nameof(AllLayouts))]
    public void The_band_is_printable_and_clear_of_the_block_and_its_crop_marks(string sheetId, string itemId, double bleed, bool marks)
    {
        var sheet = SheetFormats.All.Single(s => s.Id == sheetId);
        var layout = LayoutEngine.Compute(new LayoutOptions(sheet, ItemFormats.All.Single(f => f.Id == itemId).Size, bleed, marks));
        if (SheetMark.Place(layout) is not { } at) return;

        var (band, p) = (at.Band, layout.PrintableArea);
        Assert.True(band.X >= p.X - 1e-9 && band.Y >= p.Y - 1e-9 && band.Right <= p.Right + 1e-9 && band.Bottom <= p.Bottom + 1e-9);
        Assert.True(Math.Min(band.Width, band.Height) >= SheetMark.BandMm - 1e-9);
        foreach (var cell in layout.Cells)
            Assert.False(Overlap(band, cell.Bleed));
        foreach (var m in layout.CropMarks)
            Assert.False(Overlap(band, new RectMm(Math.Min(m.X1, m.X2), Math.Min(m.Y1, m.Y2), Math.Abs(m.X2 - m.X1), Math.Abs(m.Y2 - m.Y1))));
    }

    [Fact]
    public void Below_the_block_when_there_is_room()
    {
        // Eight 10 x 15 on SRA3 leave about 20 mm above and below the block.
        var layout = LayoutEngine.Compute(new LayoutOptions(SheetFormats.Sra3, ItemFormats.Photo10x15.Size, 0, true));

        var at = SheetMark.Place(layout);

        Assert.Equal(SheetMark.Side.Bottom, at?.Side);
        Assert.True(at!.Value.Band.Y >= layout.Block!.Value.Bottom);
    }

    [Fact]
    public void Along_a_side_when_the_block_fills_the_height()
    {
        // Business cards on SRA3: 12.5 mm above and below, nearly all of it taken by the crop marks.
        var layout = LayoutEngine.Compute(new LayoutOptions(SheetFormats.Sra3, ItemFormats.BusinessCard.Size, 0, true));

        Assert.Equal(SheetMark.Side.Left, SheetMark.Place(layout)?.Side);
    }

    [Fact]
    public void Nowhere_on_a_lab_print_filled_to_its_edges()
    {
        var layout = LayoutEngine.Compute(new LayoutOptions(SheetFormats.Lab10x15, ItemFormats.Photo10x15.Size));

        Assert.Null(SheetMark.Place(layout));
    }

    // Touching edges do not overlap.
    private static bool Overlap(RectMm a, RectMm b) =>
        a.X < b.Right - 1e-9 && b.X < a.Right - 1e-9 && a.Y < b.Bottom - 1e-9 && b.Y < a.Bottom - 1e-9;
}
