using PdfSharp.Fonts;

namespace PageCompose.Core.Pdf;

/// <summary>
/// Segoe UI of Windows, as in the app: the core build of PDFsharp finds no font by itself (the cutting plan, the
/// marks of the sheets).
/// </summary>
internal sealed class WindowsFonts : IFontResolver
{
    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) =>
        new(bold ? "segoeuib" : "segoeui");

    public byte[]? GetFont(string faceName) =>
        File.ReadAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), faceName + ".ttf"));
}
