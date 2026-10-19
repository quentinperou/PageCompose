using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PageCompose.Core;

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

    /// <summary>The common text in the band of framed sizes.</summary>
    public string CaptionText { get; set; } = "";
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

    /// <summary>Its own text in the band of its frame; empty: the common one.</summary>
    public string Caption { get; set; } = "";

    /// <summary>The photo enlarged in its cell, 1 to 3 (projects of 0.11.0 and before: 1).</summary>
    public double Zoom { get; set; } = 1;

    /// <summary>
    /// The turn and mirror of the picture that <see cref="CropX"/> and <see cref="CropY"/> are given in: a photo may be
    /// converted when the project is opened again (Windows stores it upright), or no longer be. Null in older projects.
    /// </summary>
    public int? CropTurn { get; set; }

    public bool CropMirror { get; set; }

    public IEnumerable<ProjectLine> WithBack() => Back is null ? [this] : [this, Back];
}

/// <summary>
/// The .PageComposeProject files (asked by the author): a zip under another name, holding "project.json", a copy of each
/// file of the project ("files/1/IMG_0042.HEIC"…), so that it opens on any PC, even when the originals have moved, and
/// "preview.png", the first sheet in small for the recent projects (since 0.11.0).
/// </summary>
public static class ProjectStore
{
    /// <summary>Registered by the installer: a double click opens the project.</summary>
    public const string FileExtension = ".PageComposeProject";

    private const string ProjectEntry = "project.json";
    private const string PreviewEntry = "preview.png";
    private const string FilesFolder = "files/";

    private static readonly string OpenedFolder = Path.Combine(Path.GetTempPath(), "PageCompose", "Projects");
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    // Already compressed: stored as they are, which is much faster.
    private static readonly string[] Compressed = [".jpg", ".jpeg", ".png", ".heic", ".heif", ".webp"];

    public static bool IsProjectFile(string path) => Path.GetExtension(path).Equals(FileExtension, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Writes the project and a copy of each of its files (once each, however many lines use it), with
    /// <paramref name="preview"/>, a small PNG of its first sheet, shown in the recent projects.
    /// </summary>
    public static void Save(string path, ProjectFile job, byte[]? preview = null)
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

        // Never a half-written project, and a save that failed (a photo no longer there) leaves nothing that would stop
        // the next one.
        SafeFile.Write(path, output =>
        {
            using var zip = new ZipArchive(output, ZipArchiveMode.Create);
            using (var writer = new StreamWriter(zip.CreateEntry(ProjectEntry).Open(), new UTF8Encoding(false)))
                writer.Write(JsonSerializer.Serialize(saved, Json));
            if (preview is not null)
            {
                using var stream = zip.CreateEntry(PreviewEntry, CompressionLevel.NoCompression).Open();
                stream.Write(preview);
            }
            foreach (var (file, entry) in entries)
            {
                var level = Compressed.Contains(Path.GetExtension(file).ToLowerInvariant()) ? CompressionLevel.NoCompression : CompressionLevel.Optimal;
                zip.CreateEntryFromFile(file, entry, level);
            }
        });
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
                    // Under a name of its own, then given its name: a file cut short by a run stopped meanwhile is never
                    // taken for whole when the project is opened again.
                    string part = $"{target}.{Guid.NewGuid():N}.part";
                    file.ExtractToFile(part);
                    try { File.Move(part, target); }
                    catch (IOException) when (File.Exists(target)) { File.Delete(part); } // another window took it out meanwhile
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

    /// <summary>The small picture of the first sheet; null for a project saved before 0.11.0, or a file unreadable.</summary>
    public static byte[]? ReadPreview(string path)
    {
        try
        {
            using var zip = ZipFile.OpenRead(path);
            if (zip.GetEntry(PreviewEntry) is not { Length: > 0 and < 4_000_000 } entry) return null;
            using var stream = entry.Open();
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return null;
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
