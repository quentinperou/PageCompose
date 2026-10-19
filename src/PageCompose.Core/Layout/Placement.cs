using PageCompose.Core.Visuals;

namespace PageCompose.Core.Layout;

public enum FitMode
{
    /// <summary>The visual covers the whole cell; what overflows is cropped.</summary>
    Fill,

    /// <summary>The whole visual fits in the cell (white bands possible).</summary>
    Fit,
}

/// <summary>How the sheet is flipped for double-sided printing (portrait sheet).</summary>
public enum DuplexFlip
{
    /// <summary>Flip on the long edge (the usual one).</summary>
    LongEdge,

    /// <summary>Flip on the short edge.</summary>
    ShortEdge,
}

/// <summary>Side of the sheet, and flip rules for the back.</summary>
/// <param name="VersoMargins">The back keeps the white margins of the cell too (by default it covers the whole cell).</param>
/// <param name="MirrorBleed">
/// A visual without its own bleed keeps its size in its trim, and the bleed takes its edges, mirrored (by default it is
/// enlarged to cover the bleed, and its edges go with the cut).
/// </param>
public sealed record SideOptions(bool IsVerso = false, DuplexFlip Flip = DuplexFlip.LongEdge, bool Rotate180 = false,
    bool VersoMargins = false, bool MirrorBleed = false)
{
    /// <summary>The white margins of the cell apply on this side.</summary>
    public bool HasMargins => !IsVerso || VersoMargins;

    public static SideOptions Recto { get; } = new();
}

/// <summary>
/// How to draw a visual in a cell: move to (CenterX, CenterY), turn by AngleDeg (clockwise, y down),
/// apply the mirror if any, then draw the raw visual in DrawRect, clipped by Clip. All values in millimetres.
/// </summary>
public sealed record VisualPlacement(RectMm Clip, double CenterX, double CenterY, double AngleDeg, bool Mirror,
    RectMm DrawRect, double EffectiveDpi)
{
    /// <summary>
    /// Mirrored bleed: the copies of the visual drawn around it, flipped (-1 or 1: beside DrawRect on that side, 0:
    /// in line with it), in the axes of the drawing; only those the clip shows.
    /// </summary>
    public IReadOnlyList<(int Dx, int Dy)> MirrorCopies { get; init; } = [];
}

public static class Placement
{
    /// <summary>An area whose sides differ by less than this (a micrometre) is square.</summary>
    private const double SquareToleranceMm = 1e-3;

    /// <summary>The cell as it lies on the back of the sheet (mirrored according to the flip).</summary>
    public static LayoutCell MirrorForVerso(LayoutCell cell, SizeMm sheet, DuplexFlip flip) => flip == DuplexFlip.LongEdge
        ? cell with { Trim = cell.Trim.MirrorX(sheet.Width), Bleed = cell.Bleed.MirrorX(sheet.Width), Margins = cell.Margins.MirrorX }
        : cell with { Trim = cell.Trim.MirrorY(sheet.Height), Bleed = cell.Bleed.MirrorY(sheet.Height), Margins = cell.Margins.MirrorY };

    /// <param name="fit">How the visuals lie in their cells, unless the visual has its own (<see cref="VisualInfo.Fit"/>).</param>
    public static VisualPlacement Compute(VisualInfo visual, LayoutCell cell, FitMode fit, SideOptions? side = null)
    {
        side ??= SideOptions.Recto;
        fit = visual.Fit ?? fit;

        // White margins are on the front; the back covers the whole cell unless asked to keep them. On the back, the
        // cell and its margins are mirrored (MirrorForVerso): the thick border of a Polaroid stays under the visual.
        bool framed = side.HasMargins && !cell.Margins.IsEmpty;
        var area = framed ? cell.ImageBleed : cell.Bleed;

        // A PDF that already has its bleed is aligned on the trim; anything else fills the bleed area, unless its edges
        // are mirrored into the bleed (then it fills its trim).
        bool mirrored = Mirrored(visual, side, fit);
        var target = Target(visual, cell, side, fit);

        var (uw, uh) = visual.UprightContentSize;
        // The edges of a cell are sums: a square image area (Polaroid, 79 x 79) comes out a hair wider or taller
        // depending on where the cell lies. Within a micrometre it is square, and turns nothing.
        bool tallArea = target.Height - target.Width > SquareToleranceMm, wideArea = target.Width - target.Height > SquareToleranceMm;
        bool needsQuarterTurn = (uw > uh && tallArea) || (uh > uw && wideArea);

        // A frame with a thicker side (Polaroid) has an up side. Laid a quarter turn, it takes along a visual that
        // fits either way (square visual or square image area), so that the visual stays upright in its frame.
        // Otherwise a visual turns by its proportions everywhere, frames included (the author wants no exception):
        // a photo the wrong way is turned by hand, from the context menu of its line.
        bool fitsEitherWay = Math.Abs(uw - uh) < 1e-9 || (!tallArea && !wideArea);
        if (framed && cell.Rotated && cell.Margins.IsAsymmetric && fitsEitherWay)
            needsQuarterTurn = true;
        int sheetAngle = needsQuarterTurn ? 90 : 0;

        if (side.IsVerso)
        {
            // On the back, the top of the visual must end up on the same physical edge as on the front.
            sheetAngle = side.Flip == DuplexFlip.LongEdge ? -sheetAngle : 180 - sheetAngle;
            if (side.Rotate180) sheetAngle += 180;
        }

        // The user's turn comes last: it turns the visual as it is shown in its cell.
        int angle = Normalize(visual.BaseRotation + sheetAngle + visual.UserRotation);
        var content = visual.Content;
        var (cw, ch) = angle % 180 == 90 ? (content.Height, content.Width) : (content.Width, content.Height);

        double k = Scale(visual, target, cw, ch, fit); // mm per raw unit

        // The part kept: the visual moved in its own axes (DrawRect is drawn before the turn), by the share of what
        // overflows its area (or of what it leaves free, in Fit mode) given by CropX / CropY.
        var (overX, overY) = Overflow(visual, target, angle, k);
        var draw = new RectMm(-content.CenterX * k + (0.5 - visual.CropX) * overX, -content.CenterY * k + (0.5 - visual.CropY) * overY,
            visual.FullWidth * k, visual.FullHeight * k);
        double dpi = visual.Kind == VisualKind.Image ? Units.MmPerInch / k : double.NaN;

        var placement = new VisualPlacement(area, target.CenterX, target.CenterY, angle, visual.BaseMirror, draw, dpi);
        return mirrored ? placement with { MirrorCopies = MirrorCopiesOf(placement) } : placement;
    }

    private static bool Mirrored(VisualInfo visual, SideOptions side, FitMode fit) =>
        side.MirrorBleed && !visual.HasOwnBleed && fit == FitMode.Fill;

    // The copies the clip shows: the clip turned back into the axes of the drawing (the turn, then the mirror, undone:
    // quarter turns keep it a rectangle), compared with the visual drawn there.
    private static List<(int Dx, int Dy)> MirrorCopiesOf(VisualPlacement p)
    {
        double a = -p.AngleDeg * Math.PI / 180, cos = Math.Round(Math.Cos(a)), sin = Math.Round(Math.Sin(a));
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var (x, y) in new[] { (p.Clip.X, p.Clip.Y), (p.Clip.Right, p.Clip.Y), (p.Clip.X, p.Clip.Bottom), (p.Clip.Right, p.Clip.Bottom) })
        {
            double dx = x - p.CenterX, dy = y - p.CenterY;
            double lx = dx * cos - dy * sin, ly = dx * sin + dy * cos;
            if (p.Mirror) lx = -lx;
            (minX, maxX, minY, maxY) = (Math.Min(minX, lx), Math.Max(maxX, lx), Math.Min(minY, ly), Math.Max(maxY, ly));
        }

        const double eps = 1e-6;
        var r = p.DrawRect;
        bool left = minX < r.X - eps, right = maxX > r.Right + eps, top = minY < r.Y - eps, bottom = maxY > r.Bottom + eps;
        var copies = new List<(int, int)>();
        foreach (int dy in new[] { -1, 0, 1 })
        foreach (int dx in new[] { -1, 0, 1 })
        {
            if ((dx, dy) == (0, 0) || (dx == -1 && !left) || (dx == 1 && !right) || (dy == -1 && !top) || (dy == 1 && !bottom)) continue;
            copies.Add((dx, dy));
        }
        return copies;
    }

    /// <summary>
    /// Where the part kept goes when the visual is dragged by (<paramref name="dxMm"/>, <paramref name="dyMm"/>) on the
    /// sheet, in this cell: the drag turned back into the axes of the visual (its turn and mirror undone), then into
    /// a share of what overflows. Kept between its edges.
    /// </summary>
    public static (double CropX, double CropY) Dragged(VisualInfo visual, LayoutCell cell, FitMode fit, SideOptions side,
        double dxMm, double dyMm)
    {
        fit = visual.Fit ?? fit;
        var p = Compute(visual, cell, fit, side);
        var content = visual.Content;
        var (cw, ch) = p.AngleDeg % 180 == 90 ? (content.Height, content.Width) : (content.Width, content.Height);
        var target = Target(visual, cell, side, fit);
        double k = Scale(visual, target, cw, ch, fit);
        var (overX, overY) = Overflow(visual, target, (int)p.AngleDeg, k);

        double a = -p.AngleDeg * Math.PI / 180;
        double rx = dxMm * Math.Cos(a) - dyMm * Math.Sin(a), ry = dxMm * Math.Sin(a) + dyMm * Math.Cos(a);
        if (p.Mirror) rx = -rx;
        double x = Math.Abs(overX) > 1e-9 ? Math.Clamp(visual.CropX - rx / overX, 0, 1) : visual.CropX;
        double y = Math.Abs(overY) > 1e-9 ? Math.Clamp(visual.CropY - ry / overY, 0, 1) : visual.CropY;
        return (x, y);
    }

    /// <summary>
    /// The part kept (<see cref="VisualInfo.CropX"/>, <see cref="VisualInfo.CropY"/>), given in the axes of a picture
    /// stored with one turn and mirror, in the axes of the same picture stored with others: a photo converted by Windows
    /// is stored upright (its EXIF turn applied), so that a project saved before it was converted kept another part.
    /// </summary>
    public static (double X, double Y) CropInOtherAxes(double x, double y, int fromRotation, bool fromMirror, int toRotation, bool toMirror)
    {
        // To the picture upright, as it is drawn: the mirror, then the turn; then back: the other turn undone, its mirror.
        if (fromMirror) x = 1 - x;
        (x, y) = Turned(x, y, fromRotation);
        (x, y) = Turned(x, y, 360 - toRotation);
        if (toMirror) x = 1 - x;
        return (x, y);
    }

    // A point given in shares of the picture (0 to 1, y down), where it goes when the picture turns clockwise.
    private static (double X, double Y) Turned(double x, double y, int degrees) => Normalize(degrees) switch
    {
        90 => (1 - y, x),
        180 => (1 - x, 1 - y),
        270 => (y, 1 - x),
        _ => (x, y),
    };

    // The area the visual is fitted to: its trim (a PDF with its own bleed, or edges mirrored into the bleed), else what
    // it fills (bleed or image area).
    private static RectMm Target(VisualInfo visual, LayoutCell cell, SideOptions side, FitMode fit)
    {
        bool framed = side.HasMargins && !cell.Margins.IsEmpty;
        var area = framed ? cell.ImageBleed : cell.Bleed;
        return visual.HasOwnBleed || Mirrored(visual, side, fit) ? (framed ? cell.ImageTrim : cell.Trim) : area;
    }

    // Millimetres per raw unit: the visual covering its area (Fill) or whole in it (Fit), then enlarged by its zoom.
    private static double Scale(VisualInfo visual, RectMm target, double cw, double ch, FitMode fit)
    {
        double sx = target.Width / cw, sy = target.Height / ch;
        return (fit == FitMode.Fill ? Math.Max(sx, sy) : Math.Min(sx, sy)) * Math.Clamp(visual.Zoom, 1, VisualInfo.MaxZoom);
    }

    // What the visual overflows its area by, along its own axes (negative: what it leaves free), in millimetres.
    private static (double X, double Y) Overflow(VisualInfo visual, RectMm target, int angle, double k)
    {
        var (width, height) = angle % 180 == 90 ? (target.Height, target.Width) : (target.Width, target.Height);
        return (visual.Content.Width * k - width, visual.Content.Height * k - height);
    }

    private static int Normalize(int angle) => ((angle % 360) + 360) % 360;
}
