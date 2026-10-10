using System.Windows.Threading;

namespace PageCompose.App.ViewModels;

/// <summary>
/// Notification over the preview (as the toasts of Windows 11): the progress of an export, then the result with the
/// files written, or a short message (presets imported); hidden after a few seconds unless the mouse is over it.
/// </summary>
public sealed class ExportToast : ObservableObject
{
    private readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromSeconds(8) };

    public ExportToast() => _hideTimer.Tick += (_, _) => Hide();

    private bool _isVisible;
    public bool IsVisible { get => _isVisible; private set => Set(ref _isVisible, value); }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!Set(ref _isBusy, value)) return;
            OnPropertyChanged(nameof(IsDone));
        }
    }

    /// <summary>The export is over: the result is shown.</summary>
    public bool IsDone => !IsBusy;

    private string _title = "";
    public string Title { get => _title; private set => Set(ref _title, value); }

    private string _detail = "";
    public string Detail { get => _detail; set => Set(ref _detail, value); }

    private IReadOnlyList<string> _files = [];

    /// <summary>Files written by the last export, in order.</summary>
    public IReadOnlyList<string> Files
    {
        get => _files;
        private set
        {
            _files = value;
            OnPropertyChanged(nameof(HasFiles));
        }
    }

    /// <summary>Shows the buttons that open the files.</summary>
    public bool HasFiles => Files.Count > 0;

    public void ShowProgress(string title, string detail)
    {
        _hideTimer.Stop();
        Files = [];
        (Title, Detail, IsBusy, IsVisible) = (title, detail, true, true);
    }

    public void ShowDone(string title, string detail, IReadOnlyList<string> files)
    {
        Files = files;
        (Title, Detail, IsBusy, IsVisible) = (title, detail, false, true);
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    public void ShowMessage(string title, string detail) => ShowDone(title, detail, []);

    public void Hide()
    {
        _hideTimer.Stop();
        IsVisible = false;
    }

    /// <summary>The mouse is over it: it stays until the mouse leaves.</summary>
    public void Hold() => _hideTimer.Stop();

    public void Release()
    {
        if (IsVisible && !IsBusy) _hideTimer.Start();
    }
}
