using System.Windows.Media.Imaging;
using PageCompose.App.Services;
using PageCompose.Core;

namespace PageCompose.App.ViewModels;

/// <summary>
/// A recent project of the "File" menu: its first sheet in small, its name, when and where it was saved. Or, at the
/// top, the last job never saved (<see cref="IsLastJob"/>).
/// </summary>
public sealed class RecentProjectItem : ObservableObject
{
    private readonly RecentFile _file;
    private readonly string? _name, _detail;

    public RecentProjectItem(RecentFile file) => _file = file;

    private RecentProjectItem(RecentFile file, string name, string detail)
    {
        _file = file;
        (_name, _detail) = (name, detail);
        IsLastJob = true;
    }

    /// <summary>"Last job not saved", "Today 17:42 · 8 visuals".</summary>
    public static RecentProjectItem ForLastJob(DateTime time, int visuals) =>
        new(new RecentFile("", time), Strings.Get("LastJobName"), Strings.Format("LastJobDetail", When(time), Strings.Count("Visuals", visuals)));

    /// <summary>The last job never saved (Services/LastJob), not a project file.</summary>
    public bool IsLastJob { get; }

    public string Path => _file.Path;

    public string Name => _name ?? System.IO.Path.GetFileNameWithoutExtension(_file.Path);

    /// <summary>"Today 11:42 · Orders\Martin": the time, and the last two folders of its place.</summary>
    public string Detail => _detail ?? Strings.Format("RecentProjectDetail", When(_file.Time), Folder(_file.Path));

    private BitmapSource? _preview;

    /// <summary>Null for a project saved before 0.11.0: the menu shows the icon of a project instead.</summary>
    public BitmapSource? Preview { get => _preview; private set => Set(ref _preview, value); }

    public async Task LoadPreviewAsync()
    {
        var png = await Task.Run(() => IsLastJob ? LastJob.ReadPreview() : ProjectStore.ReadPreview(_file.Path));
        if (png is null) return;
        try
        {
            var picture = new BitmapImage();
            picture.BeginInit();
            picture.StreamSource = new MemoryStream(png);
            picture.CacheOption = BitmapCacheOption.OnLoad;
            picture.EndInit();
            picture.Freeze();
            Preview = picture;
        }
        catch (Exception ex) when (ex is NotSupportedException or IOException or InvalidOperationException)
        {
        }
    }

    private static string When(DateTime time)
    {
        var day = time.Date;
        if (day == DateTime.Today) return Strings.Format("RecentToday", time.ToString("t"));
        if (day == DateTime.Today.AddDays(-1)) return Strings.Format("RecentYesterday", time.ToString("t"));
        return time.ToString("d");
    }

    private static string Folder(string path)
    {
        var folder = new DirectoryInfo(System.IO.Path.GetDirectoryName(path) ?? "");
        return folder.Parent is { Parent: not null } parent ? System.IO.Path.Combine(parent.Name, folder.Name) : folder.FullName;
    }
}
