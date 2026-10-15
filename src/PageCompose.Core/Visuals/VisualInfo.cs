namespace PageCompose.Core.Visuals;

public enum VisualKind
{
    Image,
    Pdf,
}

/// <summary>Rectangle in the visual's raw units (pixels for an image, points for a PDF), origin at the top left.</summary>
public readonly record struct RawBox(double X, double Y, double Width, double Height)
{
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
}

/// <summary>
/// A source visual. Sizes are those of the content as stored in the file
/// (before the EXIF orientation or the PDF page rotation is applied).
/// </summary>
public sealed record VisualInfo
{
    public required string Path { get; init; }
    public required VisualKind Kind { get; init; }

    /// <summary>Whole size, in raw units.</summary>
    public required double FullWidth { get; init; }
    public required double FullHeight { get; init; }

    /// <summary>Area that must land on the trim: the whole image, or the TrimBox of a PDF that already has its bleed.</summary>
    public required RawBox Content { get; init; }

    /// <summary>Clockwise turn (0, 90, 180, 270) that puts the visual upright: EXIF orientation or PDF /Rotate.</summary>
    public int BaseRotation { get; init; }

    /// <summary>Horizontal mirror applied before the turn (EXIF orientations 2, 4, 5, 7).</summary>
    public bool BaseMirror { get; init; }

    /// <summary>Page of a PDF used as the visual, from 1 (1 for an image).</summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>Number of pages of the file (1 for an image): with more, the app offers to choose the page.</summary>
    public int PageCount { get; init; } = 1;

    /// <summary>True when the file already has a bleed around its trim area (PDF with a TrimBox).</summary>
    public bool HasOwnBleed =>
        Math.Abs(Content.Width - FullWidth) > 0.5 || Math.Abs(Content.Height - FullHeight) > 0.5;

    /// <summary>
    /// Clockwise turn chosen by the user (0, 90, 180, 270), applied on the sheet after the automatic quarter turn: it
    /// turns the visual as it is shown, for a photo taken sideways. The automatic turn still follows the proportions
    /// of the upright visual (the author wants it everywhere).
    /// </summary>
    public int UserRotation { get; init; }

    /// <summary>
    /// Part of the visual kept when it does not fit its area exactly (moved by dragging it on the preview), in the axes
    /// of the visual as stored (before any turn), so that every copy keeps the same part however it is laid: 0 = its
    /// left edge kept, 0.5 = centred, 1 = its right edge. In Fit mode, where it is smaller than its area, it is aligned
    /// the same way.
    /// </summary>
    public double CropX { get; init; } = 0.5;

    /// <summary>As <see cref="CropX"/>, from its top edge (0) to its bottom edge (1).</summary>
    public double CropY { get; init; } = 0.5;

    /// <summary>
    /// The visual enlarged in its area, around the part kept (<see cref="CropX"/>, <see cref="CropY"/>): 1 as laid,
    /// up to <see cref="MaxZoom"/> (chosen with a slider in the menu of its line, asked by the author). It crops more:
    /// its resolution goes down with it.
    /// </summary>
    public double Zoom { get; init; } = 1;

    public const double MaxZoom = 3;

    /// <summary>
    /// How it lies in its cell when chosen for it alone (right click on its line): filled or whole; null: as the
    /// layout says.
    /// </summary>
    public Layout.FitMode? Fit { get; init; }

    /// <summary>The text written in the thick white band of its frame (a Polaroid), if any (see <see cref="Pdf.Caption"/>).</summary>
    public string? Caption { get; init; }

    /// <summary>Size of the trim area once the visual is upright, in raw units.</summary>
    public (double Width, double Height) UprightContentSize =>
        BaseRotation % 180 == 90 ? (Content.Height, Content.Width) : (Content.Width, Content.Height);

    /// <summary>Size of the trim area upright, then turned by the user (as the list shows it).</summary>
    public (double Width, double Height) TurnedContentSize =>
        (BaseRotation + UserRotation) % 180 == 90 ? (Content.Height, Content.Width) : (Content.Width, Content.Height);

    /// <summary>
    /// The colour profile of a photo as read in the file chosen (null: none, read as sRGB). It stays on the copy
    /// converted to sRGB (<see cref="ProfileConverted"/>), for the line to say from what.
    /// </summary>
    public ColorProfile? Profile { get; init; }

    /// <summary>The photo was converted from <see cref="Profile"/> to sRGB at import (<see cref="Path"/> is the copy).</summary>
    public bool ProfileConverted { get; init; }

    /// <summary>A photo whose colours are only right once converted: a profile that is not sRGB, left as it is.</summary>
    public bool HasForeignProfile => Kind == VisualKind.Image && Profile is { IsSrgb: false } && !ProfileConverted;

    /// <summary>File chosen by the user, when <see cref="Path"/> is a stand-in for it (a PDF converted to an image).</summary>
    public string? SourcePath { get; init; }

    /// <summary>File chosen by the user (shown, and offered as the export folder).</summary>
    public string OriginalPath => SourcePath ?? Path;

    public string FileName => System.IO.Path.GetFileName(OriginalPath);
}

public enum VisualLoadError
{
    NotFound,
    Unsupported,
    NoPages,
    Unreadable,

    /// <summary>
    /// Read, but PDFsharp would refuse to embed it as it is (a JPEG from the web that does not start with a JFIF, Exif
    /// or ICC segment): the app converts it first.
    /// </summary>
    NeedsConversion,
}

/// <summary>A visual that cannot be used. The UI turns <see cref="Error"/> into a message in its own language.</summary>
public sealed class VisualLoadException(VisualLoadError error, string path, string? detail = null, Exception? inner = null)
    : Exception($"{error}: {path}{(detail is null ? "" : " (" + detail + ")")}", inner)
{
    public VisualLoadError Error { get; } = error;
    public string FilePath { get; } = path;
    public string FileName => System.IO.Path.GetFileName(FilePath);

    /// <summary>Technical detail (file extension, parser message), when there is one.</summary>
    public string? Detail { get; } = detail;
}
