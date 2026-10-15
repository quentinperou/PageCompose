using System.Windows.Media.Imaging;
using PageCompose.App.Services;
using PageCompose.Core;

namespace PageCompose.App.ViewModels;

/// <summary>A recent project of the "File" menu: its first sheet in small, its name, when and where it was saved.</summary>
public sealed class RecentProjectItem(RecentFile file) : ObservableObject
{
    public string Path => file.Path;

    public string Name => System.IO.Path.GetFileNameWithoutExtension(file.Path);

    /// <summary>"Today 11:42 · Orders\Martin": the time, and the last two folders of its place.</summary>
    public string Detail => Strings.Format("RecentProjectDetail", When(file.Time), Folder(file.Path));

    private BitmapSource? _preview;

    /// <summary>Null for a project saved before 0.11.0: the menu shows the icon of a project instead.</summary>
    public BitmapSource? Preview { get => _preview; private set => Set(ref _preview, value); }

    public async Task LoadPreviewAsync()
    {
        if (await Task.Run(() => ProjectStore.ReadPreview(file.Path)) is not { } png) return;
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
