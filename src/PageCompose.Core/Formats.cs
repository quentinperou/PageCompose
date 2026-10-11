namespace PageCompose.Core;

/// <summary>Named size (sheet or visual), in millimetres. <see cref="Id"/> is stable: settings and UI texts key on it.</summary>
/// <param name="Margins">
/// Where nothing is printed: for a visual, the white border built into the size (instant photo frames); for a sheet,
/// the unprintable margin of the printer. None for most sizes.
/// </param>
/// <param name="Folds">Where a folded card folds, on its open size; none for a flat one.</param>
public sealed record PaperFormat(string Id, double Width, double Height, MarginsMm Margins = default, FoldsMm? Folds = null)
{
    public SizeMm Size => new(Width, Height);

    public override string ToString() => Id;
}

/// <summary>
/// The folds of a card, on its open size as defined: <see cref="X"/> the vertical folds (from its left edge),
/// <see cref="Y"/> the horizontal ones (from its top edge), in millimetres.
/// </summary>
public sealed record FoldsMm(IReadOnlyList<double> X, IReadOnlyList<double> Y)
{
    public static FoldsMm None { get; } = new([], []);

    public bool IsEmpty => X.Count == 0 && Y.Count == 0;

    /// <summary>
    /// The folds of the size laid a quarter turn clockwise (as its margins, <see cref="MarginsMm.RotatedClockwise"/>): a
    /// horizontal fold at y becomes a vertical one at <paramref name="height"/> − y, a vertical one at x a horizontal one at x.
    /// </summary>
    public FoldsMm RotatedClockwise(double height) => new([.. Y.Select(y => height - y)], [.. X]);
}

/// <summary>Photo lab sizes are in inches: 10 x 15 is 4 x 6 in, so that four of them make exactly one 20 x 30.</summary>
internal static class Inches
{
    public static double Mm(double inches) => inches * Units.MmPerInch;
}

/// <summary>
/// Printing sheets: those of the FUJIFILM ApeosPro C810, which cannot print its 4.1 mm edges, and photo lab prints,
/// printed to the edge.
/// </summary>
public static class SheetFormats
{
    /// <summary>Unprintable margin of the ApeosPro C810 (IPP media-*-margin-supported = 410).</summary>
    public const double PrinterMarginMm = 4.1;

    private static readonly MarginsMm Printer = new(PrinterMarginMm, PrinterMarginMm, PrinterMarginMm, PrinterMarginMm);

    public static readonly PaperFormat Sra3 = new("SRA3", 320, 450, Printer);
    public static readonly PaperFormat A3 = new("A3", 297, 420, Printer);
    public static readonly PaperFormat A4 = new("A4", 210, 297, Printer);
    public static readonly PaperFormat Lab10x15 = new("Lab10x15", Inches.Mm(4), Inches.Mm(6));
    public static readonly PaperFormat Lab20x30 = new("Lab20x30", Inches.Mm(8), Inches.Mm(12));
    public static readonly PaperFormat Lab30x45 = new("Lab30x45", Inches.Mm(12), Inches.Mm(18));

    public static IReadOnlyList<PaperFormat> All { get; } = [Sra3, A3, A4, Lab10x15, Lab20x30, Lab30x45];
}

/// <summary>Preset visual sizes. Photo prints use the lab sizes, in inches (10 x 15 = 4 x 6 in = 101.6 x 152.4 mm).</summary>
public static class ItemFormats
{
    public static readonly PaperFormat BusinessCard = new("BusinessCard", 85, 55);
    public static readonly PaperFormat IdPhoto = new("IdPhoto", 35, 45); // eight on a 10 x 15 print
    public static readonly PaperFormat Photo9x13 = new("Photo9x13", Inches.Mm(3.5), Inches.Mm(5));
    public static readonly PaperFormat Photo10x15 = new("Photo10x15", Inches.Mm(4), Inches.Mm(6));
    public static readonly PaperFormat Photo13x18 = new("Photo13x18", Inches.Mm(5), Inches.Mm(7));
    public static readonly PaperFormat Photo15x20 = new("Photo15x20", Inches.Mm(6), Inches.Mm(8));
    public static readonly PaperFormat Photo20x30 = new("Photo20x30", Inches.Mm(8), Inches.Mm(12));
    public static readonly PaperFormat Square10 = new("Square10", 100, 100);
    public static readonly PaperFormat Square15 = new("Square15", 150, 150);
    public static readonly PaperFormat Square10Border = new("Square10Border", 100, 100, new MarginsMm(5, 5, 5, 5)); // image 90 x 90

    // Instant photo frames: published film and image sizes; the split of the border between top and bottom is not
    // published, so it is an estimate (thick border at the bottom), adjustable in the app.
    public static readonly PaperFormat Polaroid = new("Polaroid", 88, 107, new MarginsMm(6, 4.5, 22, 4.5));        // image 79 x 79
    public static readonly PaperFormat InstaxMini = new("InstaxMini", 54, 86, new MarginsMm(6, 4, 18, 4));         // image 46 x 62
    public static readonly PaperFormat InstaxSquare = new("InstaxSquare", 72, 86, new MarginsMm(6, 5, 18, 5));     // image 62 x 62
    public static readonly PaperFormat InstaxWide = new("InstaxWide", 108, 86, new MarginsMm(6, 4.5, 18, 4.5));    // image 99 x 62

    public static readonly PaperFormat PostcardA6 = new("PostcardA6", 105, 148);
    public static readonly PaperFormat FlyerDl = new("FlyerDL", 99, 210);
    public static readonly PaperFormat FlyerA5 = new("FlyerA5", 148, 210);
    public static readonly PaperFormat A4 = new("A4", 210, 297);

    // Folded cards, laid open (asked by the author): the fold in the middle of the long side; the leaflet in three has its
    // folded-in flap 3 mm narrower, so that it closes (outside face: flap 97 | back 100 | front 100).
    public static readonly PaperFormat FoldedA6 = new("FoldedA6", 148, 210, Folds: new([], [105]));                          // A5 open
    public static readonly PaperFormat Folded10x15 = new("Folded10x15", Inches.Mm(4), Inches.Mm(12), Folds: new([], [Inches.Mm(6)]));
    public static readonly PaperFormat FoldedSquare14 = new("FoldedSquare14", 140, 280, Folds: new([], [140]));
    public static readonly PaperFormat TrifoldA4 = new("TrifoldA4", 297, 210, Folds: new([97, 197], []));

    public static IReadOnlyList<PaperFormat> All { get; } =
    [
        BusinessCard, IdPhoto, Photo9x13, Photo10x15, Photo13x18, Photo15x20, Photo20x30,
        Square10, Square15, Square10Border, Polaroid, InstaxMini, InstaxSquare, InstaxWide,
        PostcardA6, FlyerDl, FlyerA5, A4,
        FoldedA6, Folded10x15, FoldedSquare14, TrifoldA4,
    ];
}
