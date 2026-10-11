using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PageCompose.App.Services;

/// <summary>
/// A project saved to be printed again: its settings (as a preset), the quantity, and each line with what was chosen on
/// it. <see cref="ProjectLine.File"/> is the full path of the file in the app, its entry in the saved file.
/// </summary>
public sealed class ProjectFile
{
    public const string Signature = "PageCompose project";

    public string Format { get; set; } = Signature;
    public int Version { get; set; } = 1;
    public LayoutPreset Settings { get; set; } = new();
    public int Quantity { get; set; } = 1;

    /// <summary>The visual of the Repeat mode.</summary>
    public ProjectLine? Repeat { get; set; }

    /// <summary>The shared back of the "Double-sided" card.</summary>
    public ProjectLine? SharedBack { get; set; }

    /// <summary>The lines of the list (Same size, Mixed sizes).</summary>
    public List<ProjectLine> Visuals { get; set; } = [];
}

public sealed class ProjectLine
{
    public string File { get; set; } = "";
    public int Page { get; set; } = 1;
    public int Copies { get; set; } = 1;
    public int Rotation { get; set; }
    public double CropX { get; set; } = 0.5;
    public double CropY { get; set; } = 0.5;

    /// <summary>Its size in the Mixed mode: the Id of a preset size, "Custom" (the size below), or none.</summary>
    public string? FormatId { get; set; }
    public double CustomWidth { get; set; } = 100;
    public double CustomHeight { get; set; } = 150;
    public ProjectLine? Back { get; set; }
    public bool BackAuto { get; set; }

    /// <summary>"Fill" or "Fit" when the line lies in its cell its own way; none: as the settings say.</summary>
    public string? Fit { get; set; }

    public IEnumerable<ProjectLine> WithBack() => Back is null ? [this] : [this, Back];
}

/// <summary>
/// The .PageComposeProject files (asked by the author): a zip under another name, holding "project.json" and a copy of each
/// file of the project ("files/1/IMG_0042.HEIC"…), so that it opens on any PC, even when the originals have moved.
/// </summary>
public static class ProjectStore
{
    /// <summary>Registered by the installer: a double click opens the project.</summary>
    public const string FileExtension = ".PageComposeProject";

    private const string ProjectEntry = "project.json";
    private const string FilesFolder = "files/";

    private static readonly string OpenedFolder = Path.Combine(Path.GetTempPath(), "PageCompose", "Projects");
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    // Already compressed: stored as they are, which is much faster.
    private static readonly string[] Compressed = [".jpg", ".jpeg", ".png", ".heic", ".heif", ".webp"];

    public static bool IsProjectFile(string path) => Path.GetExtension(path).Equals(FileExtension, StringComparison.OrdinalIgnoreCase);

    /// <summary>Writes the project and a copy of each of its files (once each, however many lines use it).</summary>
    public static void Save(string path, ProjectFile job)
    {
        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // file → entry
        var lines = new[] { job.Repeat, job.SharedBack }.OfType<ProjectLine>().Concat(job.Visuals).SelectMany(l => l.WithBack()).ToList();
        foreach (var line in lines)
        {
            // A folder each: two files of the same name may come from two folders, and the lines keep their names.
            if (!entries.ContainsKey(line.File))
                entries[line.File] = $"{FilesFolder}{entries.Count + 1}/{Path.GetFileName(line.File)}";
        }

        // The JSON names each file by its entry; the job given is left as it was.
        var saved = JsonSerializer.Deserialize<ProjectFile>(JsonSerializer.Serialize(job))!;
        foreach (var line in new[] { saved.Repeat, saved.SharedBack }.OfType<ProjectLine>().Concat(saved.Visuals).SelectMany(l => l.WithBack()))
            line.File = entries[line.File];

        string temp = path + ".tmp";
        using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
        {
            using (var writer = new StreamWriter(zip.CreateEntry(ProjectEntry).Open(), new UTF8Encoding(false)))
                writer.Write(JsonSerializer.Serialize(saved, Json));
            foreach (var (file, entry) in entries)
            {
                var level = Compressed.Contains(Path.GetExtension(file).ToLowerInvariant()) ? CompressionLevel.NoCompression : CompressionLevel.Optimal;
                zip.CreateEntryFromFile(file, entry, level);
            }
        }
        File.Move(temp, path, overwrite: true); // never a half-written project
    }

    /// <summary>
    /// Opens a project: its files are taken out to a folder of their own (kept for a day), and the lines get their
    /// full paths.
    /// </summary>
    /// <exception cref="InvalidDataException">Not a PageCompose project, or a damaged one.</exception>
    public static ProjectFile Open(string path)
    {
        var source = new FileInfo(path);
        string key = $"{source.FullName}|{source.Length}|{source.LastWriteTimeUtc.Ticks}";
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..12];
        string folder = Path.Combine(OpenedFolder, $"{Path.GetFileNameWithoutExtension(path)}-{hash}");

        try
        {
            using var zip = ZipFile.OpenRead(path);
            var entry = zip.GetEntry(ProjectEntry) ?? throw new InvalidDataException("No project.json in this file.");
            ProjectFile? job;
            using (var reader = new StreamReader(entry.Open()))
                job = JsonSerializer.Deserialize<ProjectFile>(reader.ReadToEnd());
            if (job?.Format != ProjectFile.Signature)
                throw new InvalidDataException("Not a PageCompose project.");

            string root = Path.GetFullPath(folder) + Path.DirectorySeparatorChar;
            foreach (var line in new[] { job.Repeat, job.SharedBack }.OfType<ProjectLine>().Concat(job.Visuals).SelectMany(l => l.WithBack()))
            {
                // Only the files of the project, inside its folder (never a path that climbs out of it).
                string target = Path.GetFullPath(Path.Combine(folder, line.File));
                if (!line.File.StartsWith(FilesFolder, StringComparison.Ordinal) || !target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Unexpected entry {line.File}.");
                if (!File.Exists(target))
                {
                    var file = zip.GetEntry(line.File) ?? throw new InvalidDataException($"Missing entry {line.File}.");
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    file.ExtractToFile(target, overwrite: true);
                }
                line.File = target;
            }
            if (Directory.Exists(folder)) Directory.SetLastWriteTimeUtc(folder, DateTime.UtcNow); // opened again: kept a day more
            return job;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException)
        {
            throw new InvalidDataException("Damaged PageCompose project.", ex);
        }
    }

    /// <summary>The projects opened more than a day ago (another window may still use a recent one).</summary>
    public static void DeleteOldFolders()
    {
        if (!Directory.Exists(OpenedFolder)) return;
        var limit = DateTime.UtcNow.AddDays(-1);
        foreach (var dir in Directory.EnumerateDirectories(OpenedFolder))
        {
            try
            {
                if (Directory.GetLastWriteTimeUtc(dir) < limit)
                    Directory.Delete(dir, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // In use: next time.
            }
        }
    }
}
