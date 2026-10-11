using PageCompose.Core.Layout;

namespace PageCompose.Core.Pdf;

/// <summary>Colours offered for the captions: dark tones, as written by hand on a Polaroid.</summary>
public enum CaptionColor { Black, DarkGrey, Grey, Navy, Burgundy }

/// <summary>
/// How the captions of the framed visuals are written: the family name of a font of the PC, its TrueType file
/// (embedded in the PDF, so that the text stays sharp and needs no font on the printer), and the colour.
/// </summary>
public sealed record CaptionStyle(string Family, string FontFile, CaptionColor Color);

/// <summary>
/// The text written in the thick white band of a framed visual (the bottom of a Polaroid), asked by the author: centred
/// in it, as large as the band allows, turned with the frame.
/// </summary>
public static class Caption
{
    /// <summary>A white margin at least this thick holds a text (Polaroid: 22 mm, Instax: 18 mm; 10 x 10 border: 5 mm).</summary>
    public const double MinBandMm = 8;

    /// <summary>The share of the thickness of the band the font takes (its em), and of its length the text may fill.</summary>
    internal const double HeightShare = 0.36, LengthShare = 0.88;

    public enum Side { Bottom, Left, Top, Right }

    public static (byte R, byte G, byte B) Rgb(this CaptionColor color) => color switch
    {
        CaptionColor.Black => (0, 0, 0),
        CaptionColor.Grey => (110, 110, 110),
        CaptionColor.Navy => (24, 40, 92),
        CaptionColor.Burgundy => (110, 22, 40),
        _ => (55, 55, 55),
    };

    /// <summary>
    /// The band of a cell: its thickest white margin, when it is thick enough (the bottom first, as a Polaroid stands).
    /// A frame laid a quarter turn has its margins turned clockwise: its bottom band is on the left.
    /// </summary>
    public static (RectMm Band, Side Side)? BandOf(LayoutCell cell)
    {
        var (m, t) = (cell.Margins, cell.Trim);
        double thickest = Math.Max(Math.Max(m.Top, m.Bottom), Math.Max(m.Left, m.Right));
        if (thickest < MinBandMm) return null;
        if (m.Bottom == thickest) return (new RectMm(t.X, t.Bottom - m.Bottom, t.Width, m.Bottom), Side.Bottom);
        if (m.Left == thickest) return (new RectMm(t.X, t.Y, m.Left, t.Height), Side.Left);
        if (m.Top == thickest) return (new RectMm(t.X, t.Y, t.Width, m.Top), Side.Top);
        return (new RectMm(t.Right - m.Right, t.Y, m.Right, t.Height), Side.Right);
    }

    /// <summary>How far the text turns (clockwise) to read along its band, as the frame stands.</summary>
    internal static double AngleOf(Side side) => side switch { Side.Left => 90, Side.Top => 180, Side.Right => -90, _ => 0 };
}
