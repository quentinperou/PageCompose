using PageCompose.Core.Layout;
using PageCompose.Core.Visuals;

namespace PageCompose.Core.Tests;

public class SheetFillerTests
{
    private static VisualInfo V(string name) => new()
    {
        Path = name, Kind = VisualKind.Image, FullWidth = 100, FullHeight = 100, Content = new RawBox(0, 0, 100, 100),
    };

    private static readonly VisualInfo A = V("a.jpg"), B = V("b.jpg"), C = V("c.jpg");

    [Fact]
    public void Single_visual_fills_full_sheets_when_completing()
    {
        var plan = SheetFiller.Fill([new(A, 20)], 8, completeLastSheet: true);

        Assert.Equal(3, plan.SheetCount);
        Assert.Equal((20, 24, 4, 0), (plan.Requested, plan.Printed, plan.Extra, plan.EmptyCells));
        Assert.All(plan.Sheets.SelectMany(s => s), v => Assert.Same(A, v));
    }

    [Fact]
    public void Last_sheet_keeps_empty_cells_when_not_completing()
    {
        var plan = SheetFiller.Fill([new(A, 20)], 8, completeLastSheet: false);

        Assert.Equal(3, plan.SheetCount);
        Assert.Equal((20, 0, 4), (plan.Printed, plan.Extra, plan.EmptyCells));
        Assert.Equal(4, plan.Sheets[2].Count(v => v is null));
        Assert.Null(plan.Sheets[2][^1]);
    }

    [Fact]
    public void Visuals_are_placed_in_list_order_and_extras_cycle_through_the_list()
    {
        var plan = SheetFiller.Fill([new(A, 3), new(B, 2), new(C, 0)], 4, completeLastSheet: true);

        Assert.Equal("a a a b", Names(plan.Sheets[0]));
        Assert.Equal("b a b a", Names(plan.Sheets[1])); // C (0 copies) is skipped
        Assert.Equal(3, plan.Extra);
    }

    private static string Names(VisualInfo?[] cells) => string.Join(" ", cells.Select(v => v is null ? "-" : v.Path[..1]));

    [Theory]
    [InlineData(0, 5)]
    [InlineData(8, 0)]
    public void Nothing_to_print_gives_an_empty_plan(int perSheet, int copies)
    {
        var plan = SheetFiller.Fill([new(A, copies)], perSheet, completeLastSheet: true);

        Assert.Equal(0, plan.SheetCount);
        Assert.Equal(0, plan.Printed);
    }
}
