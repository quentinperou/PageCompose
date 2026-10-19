using System.Globalization;
using PageCompose.Core.Visuals;

namespace PageCompose.Core.Pdf;

/// <summary>
/// Order of the numbers: down the sheets cell by cell, so that the sheets stacked and cut give piles already in order
/// (cell 1 of every sheet, then cell 2…), or sheet after sheet in reading order.
/// </summary>
public enum NumberingOrder { Pile, BySheet }

/// <summary>Which way a number reads: across, up (from the bottom, as the spine of a book), upside down, down.</summary>
public enum NumberDirection { Horizontal, Vertical, HorizontalInverted, VerticalInverted }

/// <summary>Colours of the numbers and of the background behind them.</summary>
public enum NumberColor { Black, White, Grey, Red, Blue }

/// <summary>
/// A place of the number in a visual: the top left corner of the number as it lies, from the top left corner of the
/// visual (millimetres), and the way it reads.
/// </summary>
public sealed record NumberPlace(double X, double Y, NumberDirection Direction = NumberDirection.Horizontal);

/// <summary>
/// How the numbers of the visuals are written (asked by the author: raffle tickets, numbered cards…): the text around
/// the number, its places in the visual, a TrueType font of the PC (its file, embedded in the PDF), its height and
/// colour, and a background behind it if asked (to keep it readable on a busy visual).
/// </summary>
public sealed record NumberingStyle(string Prefix, int Digits, string Suffix, IReadOnlyList<NumberPlace> Places,
    string Family, string FontFile, double SizeMm, NumberColor Color, NumberColor? Background = null)
{
    /// <summary>White space around the number on its background, in millimetres.</summary>
    public const double BackgroundPaddingMm = 0.8;

    /// <summary>"N° 007": the number with at least <see cref="Digits"/> digits, between its prefix and its suffix.</summary>
    public string Text(int number) =>
        Prefix + number.ToString("D" + Math.Clamp(Digits, 0, 9), CultureInfo.InvariantCulture) + Suffix;
}

public static class Numbering
{
    /// <summary>
    /// The number of each visual laid, sheet by sheet as in <paramref name="sheets"/> (null: an empty cell), from
    /// <paramref name="first"/>. In a pile, the numbers go down the sheets in each cell, cell after cell: with the
    /// last sheet only partly filled, the piles of its empty cells are one shorter, and the numbers still follow on.
    /// </summary>
    public static int?[][] Assign(IReadOnlyList<IReadOnlyList<VisualInfo?>> sheets, NumberingOrder order, int first)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        var numbers = sheets.Select(s => new int?[s.Count]).ToArray();
        int next = Math.Max(0, first);
        if (order == NumberingOrder.BySheet)
        {
            for (int s = 0; s < sheets.Count; s++)
                for (int c = 0; c < sheets[s].Count; c++)
                    if (sheets[s][c] is not null) numbers[s][c] = next++;
            return numbers;
        }

        int cells = sheets.Count == 0 ? 0 : sheets.Max(s => s.Count);
        for (int c = 0; c < cells; c++)
            for (int s = 0; s < sheets.Count; s++)
                if (c < sheets[s].Count && sheets[s][c] is not null) numbers[s][c] = next++;
        return numbers;
    }

    public static (byte R, byte G, byte B) Rgb(this NumberColor color) => color switch
    {
        NumberColor.White => (255, 255, 255),
        NumberColor.Grey => (110, 110, 110),
        NumberColor.Red => (200, 30, 30),
        NumberColor.Blue => (25, 60, 160),
        _ => (0, 0, 0),
    };
}
