using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PageCompose.Core.Tests;

/// <summary>Keeps the English and French UI strings (src/PageCompose.App/Properties/*.resx) in step with each other and with the code.</summary>
public class LocalizationTests
{
    private static readonly string AppDir = Path.Combine(FindRepositoryRoot(), "src", "PageCompose.App");

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PageCompose.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root (PageCompose.slnx) not found.");
    }

    private static Dictionary<string, string> ReadStrings(string fileName) =>
        XDocument.Load(Path.Combine(AppDir, "Properties", fileName)).Root!
            .Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string)d.Element("value")!);

    private static HashSet<string> Placeholders(string text) =>
        Regex.Matches(text, @"\{\d+\}").Select(m => m.Value).ToHashSet();

    /// <summary>Keys referenced by the app: {local:Tr Key}, Strings.Get/Format("Key"), Strings.Count("Key") and preview sides.</summary>
    private static List<string> UsedKeys()
    {
        var keys = new List<string>();
        foreach (var file in Directory.EnumerateFiles(AppDir, "*.*", SearchOption.AllDirectories)
                     .Where(f => (f.EndsWith(".cs") || f.EndsWith(".xaml")) && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            string text = File.ReadAllText(file);
            keys.AddRange(Regex.Matches(text, @"\{local:Tr (\w+)\}").Select(m => m.Groups[1].Value));

            // First argument of Strings.Get/Format/Count, ternaries included: Strings.Get(a ? "KeyA" : "KeyB").
            foreach (Match call in Regex.Matches(text, @"Strings\.(Get|Format|Count)\(([^,)]*)"))
            {
                // A literal followed by "+" is a prefix ("Format_" + id), checked by EveryPresetSize_HasALabel.
                var literals = Regex.Matches(call.Groups[2].Value, "\"(\\w+)\"(?!\\s*\\+)").Select(m => m.Groups[1].Value);
                keys.AddRange(call.Groups[1].Value == "Count" ? literals.SelectMany(k => new[] { k + "One", k + "Many" }) : literals);
            }

            keys.AddRange(Regex.Matches(text, @"PreviewSide \w+ \{ get; \} = new\(""(\w+)""\)").Select(m => m.Groups[1].Value));
        }

        return keys;
    }

    [Fact]
    public void FrenchTranslation_HasExactlyTheEnglishKeys()
    {
        Assert.Equal(
            ReadStrings("Strings.resx").Keys.Order(),
            ReadStrings("Strings.fr.resx").Keys.Order());
    }

    [Fact]
    public void Translations_UseTheSamePlaceholders()
    {
        var french = ReadStrings("Strings.fr.resx");
        foreach (var (key, english) in ReadStrings("Strings.resx"))
            Assert.True(Placeholders(english).SetEquals(Placeholders(french[key])), $"Placeholders differ for \"{key}\".");
    }

    [Fact]
    public void EveryKeyUsedByTheApp_Exists()
    {
        var keys = ReadStrings("Strings.resx").Keys.ToHashSet();
        var used = UsedKeys();

        Assert.True(used.Count > 50, $"Only {used.Count} keys found: the scan of the sources is broken.");
        Assert.All(used, key => Assert.Contains(key, keys));
    }

    [Fact]
    public void EveryPresetSize_HasALabel()
    {
        var keys = ReadStrings("Strings.resx").Keys.ToHashSet();
        Assert.All(ItemFormats.All, f => Assert.Contains("Format_" + f.Id, keys));
        Assert.All(SheetFormats.All, s => Assert.Contains("Sheet_" + s.Id, keys));
    }

    [Fact]
    public void EveryCutLineColour_HasAName()
    {
        var keys = ReadStrings("Strings.resx").Keys.ToHashSet();
        Assert.All(Enum.GetValues<Pdf.CutLineColor>(), c => Assert.Contains("CutColor_" + c, keys));
    }
}
