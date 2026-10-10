using PageCompose.Core.Visuals;

namespace PageCompose.Core.Layout;

/// <summary>A visual and the number of copies wanted.</summary>
public sealed record VisualCopies(VisualInfo Visual, int Copies);

/// <summary>
/// Visuals assigned to the cells, sheet by sheet. Each sheet is an array as long as the layout, in cell order
/// (row by row); null = empty cell.
/// </summary>
public sealed record SheetPlan(IReadOnlyList<VisualInfo?[]> Sheets, int Requested, int Printed, int PerSheet)
{
    public static SheetPlan Empty { get; } = new([], 0, 0, 0);

    public int SheetCount => Sheets.Count;

    /// <summary>Copies added to fill the last sheet.</summary>
    public int Extra => Printed - Requested;

    /// <summary>Cells left empty (last sheet not filled).</summary>
    public int EmptyCells => SheetCount * PerSheet - Printed;
}

public static class SheetFiller
{
    /// <summary>
    /// Places the copies in list order. When <paramref name="completeLastSheet"/> is true, the remaining cells of
    /// the last sheet are filled by going through the visuals again, in order.
    /// </summary>
    public static SheetPlan Fill(IReadOnlyList<VisualCopies> items, int perSheet, bool completeLastSheet)
    {
        var active = items.Where(i => i.Copies > 0).ToList();
        int requested = active.Sum(i => i.Copies);
        if (perSheet <= 0 || requested == 0)
            return SheetPlan.Empty with { PerSheet = Math.Max(perSheet, 0) };

        int sheetCount = (requested + perSheet - 1) / perSheet;
        var sequence = active.SelectMany(i => Enumerable.Repeat(i.Visual, i.Copies)).ToList();
        if (completeLastSheet)
            for (int k = 0; sequence.Count < sheetCount * perSheet; k++)
                sequence.Add(active[k % active.Count].Visual);

        var sheets = new List<VisualInfo?[]>(sheetCount);
        for (int s = 0; s < sheetCount; s++)
        {
            var cells = new VisualInfo?[perSheet];
            for (int c = 0; c < perSheet; c++)
            {
                int index = s * perSheet + c;
                cells[c] = index < sequence.Count ? sequence[index] : null;
            }

            sheets.Add(cells);
        }

        return new SheetPlan(sheets, requested, sequence.Count, perSheet);
    }
}
