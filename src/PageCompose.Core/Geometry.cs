namespace PageCompose.Core;

/// <summary>Size in millimetres.</summary>
public readonly record struct SizeMm(double Width, double Height)
{
    public SizeMm Rotated => new(Height, Width);
    public bool IsLandscape => Width > Height;
    public bool IsPortrait => Height > Width;

    public override string ToString() => $"{Width:0.#} x {Height:0.#} mm";
}

/// <summary>Rectangle in millimetres, origin at the top-left corner of the sheet, y pointing down.</summary>
public readonly record struct RectMm(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
    public SizeMm Size => new(Width, Height);

    public RectMm Inflate(double d) => new(X - d, Y - d, Width + 2 * d, Height + 2 * d);

    public RectMm Deflate(MarginsMm m) => new(X + m.Left, Y + m.Top, Width - m.Left - m.Right, Height - m.Top - m.Bottom);

    /// <summary>Mirror image across the vertical centre line of a sheet <paramref name="sheetWidth"/> wide.</summary>
    public RectMm MirrorX(double sheetWidth) => this with { X = sheetWidth - Right };

    /// <summary>Mirror image across the horizontal centre line of a sheet <paramref name="sheetHeight"/> high.</summary>
    public RectMm MirrorY(double sheetHeight) => this with { Y = sheetHeight - Bottom };

    public bool Contains(RectMm other, double tolerance = 1e-6) =>
        other.X >= X - tolerance && other.Y >= Y - tolerance &&
        other.Right <= Right + tolerance && other.Bottom <= Bottom + tolerance;
}

/// <summary>
/// White margins inside a finished visual, in millimetres: the image goes inside them (a Polaroid frame, a white
/// border). Sides are those of the visual as it stands, the thick border of a Polaroid being at the bottom.
/// </summary>
public readonly record struct MarginsMm(double Top, double Right, double Bottom, double Left)
{
    public static MarginsMm None => default;

    public bool IsEmpty => Top == 0 && Right == 0 && Bottom == 0 && Left == 0;

    /// <summary>True when one side differs from its opposite: the visual then has an up side.</summary>
    public bool IsAsymmetric => Math.Abs(Top - Bottom) > 1e-9 || Math.Abs(Left - Right) > 1e-9;

    /// <summary>The margins after a quarter turn clockwise (the top goes to the right).</summary>
    public MarginsMm RotatedClockwise => new(Left, Top, Right, Bottom);

    /// <summary>Left and right swapped (mirror image across a vertical axis).</summary>
    public MarginsMm MirrorX => this with { Left = Right, Right = Left };

    /// <summary>Top and bottom swapped (mirror image across a horizontal axis).</summary>
    public MarginsMm MirrorY => this with { Top = Bottom, Bottom = Top };

    // The generated one would print the turned and mirrored margins, and theirs, endlessly.
    public override string ToString() => $"top {Top:0.#}, right {Right:0.#}, bottom {Bottom:0.#}, left {Left:0.#} mm";
}

/// <summary>Line segment in millimetres (crop marks).</summary>
public readonly record struct LineMm(double X1, double Y1, double X2, double Y2);

public static class Units
{
    public const double MmPerInch = 25.4;
    public const double PointsPerInch = 72.0;

    public static double MmToPt(double mm) => mm * PointsPerInch / MmPerInch;
    public static double PtToMm(double pt) => pt * MmPerInch / PointsPerInch;
}
