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
public sealed record SideOptions(bool IsVerso = false, DuplexFlip Flip = DuplexFlip.LongEdge, bool Rotate180 = false,
    bool VersoMargins = false)
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
    RectMm DrawRect, double EffectiveDpi);

public static class Placement
{
    /// <summary>An area whose sides differ by less than this (a micrometre) is square.</summary>
    private const double SquareToleranceMm = 1e-3;

    /// <summary>The cell as it lies on the back of the sheet (mirrored according to the flip).</summary>
    public static LayoutCell MirrorForVerso(LayoutCell cell, SizeMm sheet, DuplexFlip flip) => flip == DuplexFlip.LongEdge
        ? cell with { Trim = cell.Trim.MirrorX(sheet.Width), Bleed = cell.Bleed.MirrorX(sheet.Width), Margins = cell.Margins.MirrorX }
        : cell with { Trim = cell.Trim.MirrorY(sheet.Height), Bleed = cell.Bleed.MirrorY(sheet.Height), Margins = cell.Margins.MirrorY };

    public static VisualPlacement Compute(VisualInfo visual, LayoutCell cell, FitMode fit, SideOptions? side = null)
    {
        side ??= SideOptions.Recto;

        // White margins are on the front; the back covers the whole cell unless asked to keep them. On the back, the
        // cell and its margins are mirrored (MirrorForVerso): the thick border of a Polaroid stays under the visual.
        bool framed = side.HasMargins && !cell.Margins.IsEmpty;
        var area = framed ? cell.ImageBleed : cell.Bleed;

        // A PDF that already has its bleed is aligned on the trim; anything else fills the bleed area.
        var target = visual.HasOwnBleed ? (framed ? cell.ImageTrim : cell.Trim) : area;

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

        double sx = target.Width / cw, sy = target.Height / ch;
        double k = fit == FitMode.Fill ? Math.Max(sx, sy) : Math.Min(sx, sy); // mm per raw unit

        // The part kept: the visual moved in its own axes (DrawRect is drawn before the turn), by the share of what
        // overflows its area (or of what it leaves free, in Fit mode) given by CropX / CropY.
        var (overX, overY) = Overflow(visual, target, angle, k);
        var draw = new RectMm(-content.CenterX * k + (0.5 - visual.CropX) * overX, -content.CenterY * k + (0.5 - visual.CropY) * overY,
            visual.FullWidth * k, visual.FullHeight * k);
        double dpi = visual.Kind == VisualKind.Image ? Units.MmPerInch / k : double.NaN;

        return new VisualPlacement(area, target.CenterX, target.CenterY, angle, visual.BaseMirror, draw, dpi);
    }

    /// <summary>
    /// Where the part kept goes when the visual is dragged by (<paramref name="dxMm"/>, <paramref name="dyMm"/>) on the
    /// sheet, in this cell: the drag turned back into the axes of the visual (its turn and mirror undone), then into
    /// a share of what overflows. Kept between its edges.
    /// </summary>
    public static (double CropX, double CropY) Dragged(VisualInfo visual, LayoutCell cell, FitMode fit, SideOptions side,
        double dxMm, double dyMm)
    {
        var p = Compute(visual, cell, fit, side);
        var content = visual.Content;
        var (cw, ch) = p.AngleDeg % 180 == 90 ? (content.Height, content.Width) : (content.Width, content.Height);
        var target = Target(visual, cell, side);
        double k = fit == FitMode.Fill ? Math.Max(target.Width / cw, target.Height / ch) : Math.Min(target.Width / cw, target.Height / ch);
        var (overX, overY) = Overflow(visual, target, (int)p.AngleDeg, k);

        double a = -p.AngleDeg * Math.PI / 180;
        double rx = dxMm * Math.Cos(a) - dyMm * Math.Sin(a), ry = dxMm * Math.Sin(a) + dyMm * Math.Cos(a);
        if (p.Mirror) rx = -rx;
        double x = Math.Abs(overX) > 1e-9 ? Math.Clamp(visual.CropX - rx / overX, 0, 1) : visual.CropX;
        double y = Math.Abs(overY) > 1e-9 ? Math.Clamp(visual.CropY - ry / overY, 0, 1) : visual.CropY;
        return (x, y);
    }

    // The area the visual is fitted to: its trim (a PDF with its own bleed), else what it fills (bleed or image area).
    private static RectMm Target(VisualInfo visual, LayoutCell cell, SideOptions side)
    {
        bool framed = side.HasMargins && !cell.Margins.IsEmpty;
        var area = framed ? cell.ImageBleed : cell.Bleed;
        return visual.HasOwnBleed ? (framed ? cell.ImageTrim : cell.Trim) : area;
    }

    // What the visual overflows its area by, along its own axes (negative: what it leaves free), in millimetres.
    private static (double X, double Y) Overflow(VisualInfo visual, RectMm target, int angle, double k)
    {
        var (width, height) = angle % 180 == 90 ? (target.Height, target.Width) : (target.Width, target.Height);
        return (visual.Content.Width * k - width, visual.Content.Height * k - height);
    }

    private static int Normalize(int angle) => ((angle % 360) + 360) % 360;
}
