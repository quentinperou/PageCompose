using PageCompose.Core.Layout;

namespace PageCompose.Core.Pdf;

/// <summary>
/// The mark of a sheet (the job, the date, "sheet 2/5": a text given by the app), to sort the sheets at the
/// guillotine: small grey text on the front, in a band outside the block and its crop marks, inside the printable
/// area, so that the cuts take it away with the waste. Below the block, else above it, else along its left or right
/// side; nowhere when the block leaves no room (a lab print filled to its edges).
/// </summary>
public static class SheetMark
{
    public const double FontSizePt = 6;

    /// <summary>The smallest font, when the text is longer than its band.</summary>
    public const double MinFontSizePt = 4;

    /// <summary>Between the mark and the block or its crop marks.</summary>
    public const double GapMm = 1;

    /// <summary>Thickness of the band: a line of text at <see cref="FontSizePt"/>.</summary>
    public const double BandMm = 2.6;

    public enum Side { Bottom, Top, Left, Right }

    /// <summary>
    /// The band the mark goes in, along the block, across the printable area; null when no side has room for it.
    /// On the <see cref="Side.Left"/> and <see cref="Side.Right"/> bands, the text reads from the bottom up.
    /// </summary>
    public static (RectMm Band, Side Side)? Place(SheetLayout layout)
    {
        if (layout.Block is not { } block) return null;

        // The crop marks may reach further than the block.
        double left = block.X, top = block.Y, right = block.Right, bottom = block.Bottom;
        foreach (var m in layout.CropMarks.Concat(layout.FoldMarks))
        {
            left = Math.Min(left, Math.Min(m.X1, m.X2));
            top = Math.Min(top, Math.Min(m.Y1, m.Y2));
            right = Math.Max(right, Math.Max(m.X1, m.X2));
            bottom = Math.Max(bottom, Math.Max(m.Y1, m.Y2));
        }

        var p = layout.PrintableArea;
        const double room = BandMm + GapMm - 1e-9;
        if (p.Bottom - bottom >= room)
            return (new RectMm(p.X, bottom + GapMm, p.Width, BandMm), Side.Bottom);
        if (top - p.Y >= room)
            return (new RectMm(p.X, top - GapMm - BandMm, p.Width, BandMm), Side.Top);
        if (left - p.X >= room)
            return (new RectMm(left - GapMm - BandMm, p.Y, BandMm, p.Height), Side.Left);
        if (p.Right - right >= room)
            return (new RectMm(right + GapMm, p.Y, BandMm, p.Height), Side.Right);
        return null;
    }
}
