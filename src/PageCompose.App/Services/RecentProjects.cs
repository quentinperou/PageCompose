using System.Text.Json;
using PageCompose.Core;

namespace PageCompose.App.Services;

/// <summary>
/// The recent projects of the "File" menu (opened or saved), in their own file of %LOCALAPPDATA%\PageCompose, shared
/// by every window: read again before each change, as the presets. A project that is no longer on the disk drops out.
/// </summary>
public static class RecentProjects
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PageCompose", "recent.json");

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>The recent projects still on the disk, the last one first.</summary>
    public static List<RecentFile> Load() => [.. Read().Where(r => File.Exists(r.Path))];

    public static void Add(string path) => Write(RecentFiles.Add(Read(), Path.GetFullPath(path), DateTime.Now));

    public static void Remove(string path) => Write(RecentFiles.Remove(Read(), path));

    public static void Clear() => Write([]);

    private static List<RecentFile> Read()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<List<RecentFile>>(File.ReadAllText(FilePath))?.Where(r => !string.IsNullOrEmpty(r.Path)).ToList() ?? [];
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // A damaged list starts again: it only points at files.
        }

        return [];
    }

    private static void Write(List<RecentFile> list)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(list, Json));
            File.Move(temp, FilePath, overwrite: true); // never a half-written file
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Another window writing at the same time: the list is only a convenience.
        }
    }
}
