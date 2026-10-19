namespace PageCompose.Core;

/// <summary>
/// A file written whole or not at all: into a temporary file beside it, which then takes its place. A write that
/// fails (a photo no longer there, a full disk) leaves the file that was there as it was, and nothing else behind.
/// </summary>
public static class SafeFile
{
    public static void Write(string path, Action<Stream> write)
    {
        // A name of its own: two writes of the same file, or a temporary file left by a run that was stopped, never
        // stand in each other's way.
        string temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write))
                write(stream);
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            try { File.Delete(temp); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            throw;
        }
    }
}
