using System.Text.Json;
using PageCompose.Core;

namespace PageCompose.App.Services;

/// <summary>
/// The last job of the app (asked by the author, as the session of Notepad): written a few seconds after each change and
/// on closing, in %LOCALAPPDATA%\PageCompose, never in %TEMP% (emptied by Windows and by the app). The photos are named
/// by their place, not copied. Reopened at start when Settings → General asks for it (off by default: at the shop, the
/// next customer's job starts empty); a job never saved also shows at the top of the recent projects. Several windows:
/// the last one written wins.
/// </summary>
public static class LastJob
{
    public const string Signature = "PageCompose last job";

    public sealed class Saved
    {
        public string Format { get; set; } = Signature;
        public int Version { get; set; } = 1;
        public DateTime Time { get; set; }

        /// <summary>The project of the job when it was saved or opened; null for a job never saved.</summary>
        public string? ProjectPath { get; set; }

        /// <summary>Changed since its project was saved.</summary>
        public bool Modified { get; set; }

        /// <summary>The job, its files by their full paths.</summary>
        public ProjectFile Job { get; set; } = new();

        /// <summary>The visuals of the job (lines, and the visual of the Repeat mode).</summary>
        public int Visuals => Job.Visuals.Count + (Job.Repeat is null ? 0 : 1);
    }

    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PageCompose");

    private static readonly string FilePath = Path.Combine(Folder, "last-job.json");
    private static readonly string PreviewPath = Path.Combine(Folder, "last-job.png");

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>The last job, or null when there is none (or it cannot be read: it is only a convenience).</summary>
    public static Saved? Read()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(FilePath));
            return saved?.Format == Signature && saved.Job is not null ? saved : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    public static void Write(Saved job)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            SafeFile.Write(FilePath, stream => JsonSerializer.Serialize(stream, job, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Another window writing at the same time: the next change writes it again.
        }
    }

    /// <summary>Its first sheet in small, for the recent projects (null: none).</summary>
    public static void WritePreview(byte[]? png)
    {
        try
        {
            if (png is null) File.Delete(PreviewPath);
            else
            {
                Directory.CreateDirectory(Folder);
                SafeFile.Write(PreviewPath, stream => stream.Write(png));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    public static byte[]? ReadPreview()
    {
        try { return File.Exists(PreviewPath) ? File.ReadAllBytes(PreviewPath) : null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>"Clear the list" of the recent projects: the last job goes too.</summary>
    public static void Clear()
    {
        foreach (var path in new[] { FilePath, PreviewPath })
        {
            try { File.Delete(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
