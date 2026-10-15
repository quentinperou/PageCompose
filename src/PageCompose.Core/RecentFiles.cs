namespace PageCompose.Core;

/// <summary>A project opened or saved, and when.</summary>
public sealed record RecentFile(string Path, DateTime Time);

/// <summary>
/// The recent projects of the "File" menu: the last one first, each file once (its path in any case), at most
/// <see cref="MaxCount"/>.
/// </summary>
public static class RecentFiles
{
    public const int MaxCount = 10;

    /// <summary>The file put first (again, if it was already there).</summary>
    public static List<RecentFile> Add(IEnumerable<RecentFile> list, string path, DateTime time) =>
        [new RecentFile(path, time), .. list.Where(r => !Same(r.Path, path)).Take(MaxCount - 1)];

    public static List<RecentFile> Remove(IEnumerable<RecentFile> list, string path) => [.. list.Where(r => !Same(r.Path, path))];

    /// <summary>The same file: Windows paths ignore case, and "a\..\b" is "b".</summary>
    public static bool Same(string a, string b) => string.Equals(Full(a), Full(b), StringComparison.OrdinalIgnoreCase);

    private static string Full(string path)
    {
        try
        {
            return System.IO.Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or System.IO.PathTooLongException)
        {
            return path;
        }
    }
}
