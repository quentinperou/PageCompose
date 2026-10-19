using System.Collections.Concurrent;
using PdfSharp.Fonts;

namespace PageCompose.Core.Pdf;

/// <summary>
/// The fonts of Windows: Segoe UI, as in the app (the cutting plan, the marks of the sheets), and the fonts registered
/// by their file (the captions of the framed visuals). The core build of PDFsharp finds no font by itself.
/// </summary>
internal sealed class WindowsFonts : IFontResolver
{
    private static readonly ConcurrentDictionary<string, string> Files = new(StringComparer.OrdinalIgnoreCase);

    // The captions ask for their font under a name of their own: Segoe UI chosen for them (one file, the regular face)
    // took the place of the regular and bold Segoe UI of the cutting plan.
    private const string CaptionPrefix = "PageCompose caption: ";

    /// <summary>A font of the PC for the captions, by its TrueType file; returns the family name to ask PDFsharp for.</summary>
    public static string Register(string family, string file)
    {
        string name = CaptionPrefix + family;
        Files[name] = file;
        return name;
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) =>
        Files.TryGetValue(familyName, out var file) ? new(file) : new(bold ? "segoeuib" : "segoeui");

    // A registered font is named by its full path; Segoe UI by its file name in the fonts folder.
    public byte[]? GetFont(string faceName) => File.ReadAllBytes(Path.IsPathRooted(faceName)
        ? faceName
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), faceName + ".ttf"));
}
