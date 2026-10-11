using System.Text.RegularExpressions;

namespace PageCompose.Core.Visuals;

/// <summary>
/// Fronts and backs told apart by their file names, among files added together: "card-recto.pdf" and
/// "card-verso.pdf", "Flyer front.jpg" and "Flyer back.jpg". The word stands on its own (between separators or at an
/// end), in any case; the rest of the name and the folder must be the same, the extension may differ. A name that
/// matches several files of the same side is left alone.
/// </summary>
public static partial class BackPairs
{
    [GeneratedRegex(@"(?<![\p{L}\p{N}])(?:(?<front>recto|front)|(?<back>verso|back))(?![\p{L}\p{N}])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SideWord();

    /// <summary>The pairs found, in the order of their fronts; the other files are not in it.</summary>
    public static IReadOnlyList<(string Front, string Back)> Find(IEnumerable<string> paths)
    {
        var fronts = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var backs = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        foreach (var path in paths)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            var matches = SideWord().Matches(name);
            if (matches.Count != 1) continue; // "recto-verso.pdf" is both: neither
            var m = matches[0];
            string key = Path.Combine(Path.GetDirectoryName(path) ?? "", name[..m.Index] + "\0" + name[(m.Index + m.Length)..]);
            var side = m.Groups["front"].Success ? fronts : backs;
            if (!side.TryGetValue(key, out var list))
            {
                side[key] = list = [];
                if (side == fronts) order.Add(key);
            }
            list.Add(path);
        }

        return [.. order
            .Where(key => fronts[key].Count == 1 && backs.TryGetValue(key, out var b) && b.Count == 1)
            .Select(key => (fronts[key][0], backs[key][0]))];
    }
}
