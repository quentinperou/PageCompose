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

    /// <summary>A font of the PC, asked for by its family name: the TrueType file that holds it.</summary>
    public static void Register(string family, string file) => Files[family] = file;

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) =>
        Files.TryGetValue(familyName, out var file) ? new(file) : new(bold ? "segoeuib" : "segoeui");

    // A registered font is named by its full path; Segoe UI by its file name in the fonts folder.
    public byte[]? GetFont(string faceName) => File.ReadAllBytes(Path.IsPathRooted(faceName)
        ? faceName
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), faceName + ".ttf"));
}
