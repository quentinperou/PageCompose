using Microsoft.Win32;

namespace PageCompose.App.Services;

/// <summary>A font offered for the captions: its family name and its TrueType file.</summary>
public sealed record FontChoice(string Name, string File)
{
    public override string ToString() => Name;
}

/// <summary>
/// The fonts offered for the captions: the TrueType fonts installed on the PC (PDFsharp embeds those), the handwriting
/// ones of Windows first; the bold and italic faces, the symbols and the collections (.ttc) are left out.
/// </summary>
internal static class FontList
{
    public const string DefaultFont = "Segoe Print";

    private static readonly string[] Preferred = [DefaultFont, "Segoe Script", "Ink Free", "Gabriola", "Comic Sans MS"];

    private static readonly string[] Faces =
        ["Bold", "Italic", "Oblique", "Light", "Semibold", "SemiBold", "Semilight", "SemiLight", "Black", "Heavy", "Thin", "Medium", "Condensed", "Narrow", "ExtraBold", "Demibold"];

    private static readonly string[] Symbols = ["Wingdings", "Webdings", "Symbol", "Marlett", "MT Extra", "MDL2", "Fluent Icons", "Emoji", "Holo", "Bookshelf"];

    private const string FontsKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts";

    public static IReadOnlyList<FontChoice> Load()
    {
        var fonts = new Dictionary<string, FontChoice>(StringComparer.OrdinalIgnoreCase);
        string folder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            try
            {
                using var key = root.OpenSubKey(FontsKey);
                if (key is null) continue;
                foreach (var entry in key.GetValueNames())
                {
                    if (key.GetValue(entry) is not string file || !file.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)) continue;
                    string name = entry.Replace("(TrueType)", "").Trim();
                    var words = name.Split(' ');
                    if (words.Any(w => Faces.Contains(w)) || Symbols.Any(s => name.Contains(s, StringComparison.OrdinalIgnoreCase))) continue;
                    string path = Path.IsPathRooted(file) ? file : Path.Combine(folder, file);
                    if (File.Exists(path)) fonts.TryAdd(name, new FontChoice(name, path));
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                // A list without these fonts: the others are offered.
            }
        }

        var first = Preferred.Where(fonts.ContainsKey).Select(n => fonts[n]);
        var others = fonts.Values.Where(f => !Preferred.Contains(f.Name, StringComparer.OrdinalIgnoreCase)).OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase);
        return [.. first, .. others];
    }
}
