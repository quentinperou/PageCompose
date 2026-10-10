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

    /// <summary>Size of the trim area once the visual is upright, in raw units.</summary>
    public (double Width, double Height) UprightContentSize =>
        BaseRotation % 180 == 90 ? (Content.Height, Content.Width) : (Content.Width, Content.Height);

    /// <summary>Size of the trim area upright, then turned by the user (as the list shows it).</summary>
    public (double Width, double Height) TurnedContentSize =>
        (BaseRotation + UserRotation) % 180 == 90 ? (Content.Height, Content.Width) : (Content.Width, Content.Height);

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
