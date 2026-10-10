using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using PageCompose.App.Services;
using PageCompose.App.ViewModels;
using PageCompose.Core;
using PageCompose.Core.Visuals;

namespace PageCompose.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        MicaBackdrop.Apply(this);
        ApplyZoom();

        Updater.Checked += Updater_Checked;
        ShowUpdateBar();
    }

    public MainViewModel ViewModel { get; }

    /// <summary>
    /// Adds files and folders (command line, drag and drop) as visuals; a preset file (.PageCompose, opened by a
    /// double click) is imported instead.
    /// </summary>
    public async Task AddPathsAsync(IEnumerable<string> paths)
    {
        var all = paths.ToList();
        foreach (var preset in all.Where(p => PresetStore.IsPresetFile(p) && File.Exists(p)))
            ImportPresets(preset);

        var files = ExpandPaths(all);
        if (files.Count > 0) await ViewModel.AddFilesAsync(files);
    }

    private void ImportPresets(string path)
    {
        string? error = null;
        try
        {
            ViewModel.ImportPresets(path);
        }
        catch (InvalidDataException)
        {
            error = Strings.Get("PresetsNotAFile");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = Strings.Format("PresetsFileFailed", ex.Message);
        }

        if (error is not null)
            MessageBox.Show(this, error, Strings.Get("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    // ---- Choosing files ----

    private async void BrowseVisuals_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.Get(ViewModel.IsComposeMode ? "OpenVisualsTitle" : "OpenVisualTitle"),
            Filter = Strings.Get("VisualsFilter"),
            Multiselect = ViewModel.IsComposeMode,
        };
        if (dialog.ShowDialog(this) == true) await ViewModel.AddFilesAsync(dialog.FileNames);
    }

    private async void BrowseVerso_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = Strings.Get("OpenBackTitle"), Filter = Strings.Get("VisualsFilter") };
        if (dialog.ShowDialog(this) == true) await ViewModel.SetVersoAsync(dialog.FileName);
    }

    /// <summary>Replaces each folder by its visuals (first level, sorted by name) and drops unsupported files.</summary>
    private static List<string> ExpandPaths(IEnumerable<string> paths)
    {
        var result = new List<string>();
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
                result.AddRange(Directory.EnumerateFiles(path).Where(VisualLoader.IsSupported).Order(StringComparer.CurrentCultureIgnoreCase));
            else if (VisualLoader.IsSupported(path))
                result.Add(path);
        }

        return result;
    }

    // ---- Drag and drop ----

    private static string[] DroppedPaths(DragEventArgs e) =>
        e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] paths ? paths : [];

    private static bool CanAccept(DragEventArgs e) =>
        DroppedPaths(e).Any(p => Directory.Exists(p) || VisualLoader.IsSupported(p) || PresetStore.IsPresetFile(p));

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        bool ok = CanAccept(e);
        e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        DropOverlay.Visibility = ok ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }

    private void Window_DragLeave(object sender, DragEventArgs e)
    {
        // DragLeave also fires when moving from one element to another: only hide when leaving the window.
        var p = e.GetPosition(this);
        if (p.X <= 0 || p.Y <= 0 || p.X >= ActualWidth || p.Y >= ActualHeight)
            DropOverlay.Visibility = Visibility.Collapsed;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        await AddPathsAsync(DroppedPaths(e));
    }

    private void VersoZone_DragOver(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        e.Effects = CanAccept(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void VersoZone_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        DropOverlay.Visibility = Visibility.Collapsed;
        var paths = DroppedPaths(e);
        if (ExpandPaths(paths) is [var first, ..])
            await ViewModel.SetVersoAsync(first);
        else
            await AddPathsAsync(paths); // a preset file
    }

    // ---- Export ----

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = ExportDialog(Strings.Get("ExportDialogTitle"), Strings.Get("PdfFilter"), ViewModel.SuggestedFileName());
        if (dialog.ShowDialog(this) != true) return;
        await RunExportAsync(async () =>
        {
            await ViewModel.ExportAsync(dialog.FileName);
            return [dialog.FileName];
        });
    }

    private async void ExportJpeg_Click(object sender, RoutedEventArgs e)
    {
        var dialog = ExportDialog(Strings.Get("ExportJpegDialogTitle"), Strings.Get("JpegFilter"),
            Path.ChangeExtension(ViewModel.SuggestedFileName(), ".jpg"));
        if (dialog.ShowDialog(this) != true) return;
        await RunExportAsync(() => ViewModel.ExportJpegAsync(dialog.FileName));
    }

    private SaveFileDialog ExportDialog(string title, string filter, string fileName) => new()
    {
        Title = title,
        Filter = filter,
        FileName = fileName,
        InitialDirectory = ViewModel.LastExportFolder is { } folder && Directory.Exists(folder) ? folder : ViewModel.FirstVisualFolder ?? "",
    };

    // The toast shows the progress, then offers to open the file or show it in its folder.
    private async Task RunExportAsync(Func<Task<IReadOnlyList<string>>> export)
    {
        try
        {
            await export();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException or InvalidOperationException)
        {
            // COMException: the Windows PDF engine could not render a sheet (JPEG export, flattened PDF).
            ViewModel.Toast.Hide();
            MessageBox.Show(this, Strings.Format("ExportFailed", ex.Message), Strings.Get("AppTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // The first file (the first sheet of a JPEG export), in its usual app.
    private static void Open(IReadOnlyList<string> files)
    {
        if (files.Count > 0)
            Process.Start(new ProcessStartInfo(files[0]) { UseShellExecute = true });
    }

    private static void ShowInFolder(IReadOnlyList<string> files)
    {
        if (files.Count > 0)
            Process.Start("explorer.exe", $"/select,\"{files[0]}\"");
    }

    private void ToastOpen_Click(object sender, RoutedEventArgs e)
    {
        Open(ViewModel.Toast.Files);
        ViewModel.Toast.Hide();
    }

    private void ToastShowInFolder_Click(object sender, RoutedEventArgs e)
    {
        ShowInFolder(ViewModel.Toast.Files);
        ViewModel.Toast.Hide();
    }

    private void ToastClose_Click(object sender, RoutedEventArgs e) => ViewModel.Toast.Hide();
    private void Toast_MouseEnter(object sender, MouseEventArgs e) => ViewModel.Toast.Hold();
    private void Toast_MouseLeave(object sender, MouseEventArgs e) => ViewModel.Toast.Release();

    // ---- Preview zoom ----
    // Zoom 1: the sheets fit the preview area, as they always did. Above 1, the sides are laid out that many times
    // larger inside the scroll viewer: drag to move, Ctrl + wheel zooms around the mouse, Ctrl + / - / 0 from the
    // keyboard, a double click zooms in or fits again. The view model renders the preview sharper when it is shown
    // larger (MainViewModel.SetPreviewDisplayWidth).

    private const double MaxZoom = 16, ZoomStep = 1.5, WheelStep = 1.2;
    private double _zoom = 1;
    private Point? _panStart;
    private Vector _panOffset;

    /// <summary>Zooms to <paramref name="zoom"/> times the fitted size, keeping the point under <paramref name="anchor"/> still.</summary>
    private void SetZoom(double zoom, Point? anchor = null)
    {
        zoom = Math.Clamp(zoom, 1, MaxZoom);
        if (Math.Abs(zoom - _zoom) < 0.001) return;

        var a = anchor ?? new Point(PreviewScroll.ViewportWidth / 2, PreviewScroll.ViewportHeight / 2);
        double x = (PreviewScroll.HorizontalOffset + a.X) / _zoom, y = (PreviewScroll.VerticalOffset + a.Y) / _zoom;
        _zoom = zoom;
        ApplyZoom();
        PreviewScroll.UpdateLayout();
        PreviewScroll.ScrollToHorizontalOffset(x * _zoom - a.X);
        PreviewScroll.ScrollToVerticalOffset(y * _zoom - a.Y);
    }

    private void ApplyZoom()
    {
        bool zoomed = _zoom > 1;
        var bars = zoomed ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        PreviewScroll.HorizontalScrollBarVisibility = PreviewScroll.VerticalScrollBarVisibility = bars;
        PreviewSidesView.Width = zoomed ? PreviewScroll.ActualWidth * _zoom : double.NaN;
        PreviewSidesView.Height = zoomed ? PreviewScroll.ActualHeight * _zoom : double.NaN;
        PreviewSidesView.Cursor = zoomed ? Cursors.SizeAll : null;
        ZoomOutButton.IsEnabled = ZoomFitButton.IsEnabled = zoomed;
        ZoomInButton.IsEnabled = _zoom < MaxZoom;
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom * ZoomStep);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom / ZoomStep);
    private void ZoomFit_Click(object sender, RoutedEventArgs e) => SetZoom(1);

    private void PreviewScroll_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_zoom > 1) ApplyZoom(); // the zoomed sides follow the size of the window
    }

    private void PreviewScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            SetZoom(_zoom * (e.Delta > 0 ? WheelStep : 1 / WheelStep), e.GetPosition(PreviewScroll));
            e.Handled = true;
        }
        else if (_zoom > 1 && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            PreviewScroll.ScrollToHorizontalOffset(PreviewScroll.HorizontalOffset - e.Delta);
            e.Handled = true;
        }
    }

    private void Preview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            if (_zoom > 1) SetZoom(1);
            else SetZoom(2.5, e.GetPosition(PreviewScroll));
            e.Handled = true;
            return;
        }
        if (_zoom <= 1) return;
        _panStart = e.GetPosition(PreviewScroll);
        _panOffset = new Vector(PreviewScroll.HorizontalOffset, PreviewScroll.VerticalOffset);
        PreviewSidesView.CaptureMouse();
        e.Handled = true;
    }

    private void Preview_MouseMove(object sender, MouseEventArgs e)
    {
        if (_panStart is not { } start || e.LeftButton != MouseButtonState.Pressed) return;
        var moved = e.GetPosition(PreviewScroll) - start;
        PreviewScroll.ScrollToHorizontalOffset(_panOffset.X - moved.X);
        PreviewScroll.ScrollToVerticalOffset(_panOffset.Y - moved.Y);
    }

    private void Preview_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_panStart is null) return;
        _panStart = null;
        PreviewSidesView.ReleaseMouseCapture();
    }

    private void Preview_LostMouseCapture(object sender, MouseEventArgs e) => _panStart = null;

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        switch (e.Key)
        {
            case Key.Add or Key.OemPlus: SetZoom(_zoom * ZoomStep); break;
            case Key.Subtract or Key.OemMinus: SetZoom(_zoom / ZoomStep); break;
            case Key.D0 or Key.NumPad0: SetZoom(1); break;
            default: return;
        }
        e.Handled = true;
    }

    // After each layout: the zoom in % of the real size, and the size of the preview on the screen, in pixels.
    private void PreviewSidesView_LayoutUpdated(object? sender, EventArgs e)
    {
        if (FindDescendant<Viewbox>(PreviewSidesView)?.Child is not FrameworkElement sheet || sheet.ActualWidth <= 0
            || !sheet.IsVisible)
            return;
        double shown = sheet.TransformToAncestor(this).TransformBounds(new Rect(sheet.RenderSize)).Width; // device-independent units
        string text = Strings.Format("ZoomPercent", Math.Round(shown / ViewModel.SheetWidth * 25.4 / 96 * 100));
        if (ZoomText.Text != text) ZoomText.Text = text;
        ViewModel.SetPreviewDisplayWidth(shown * VisualTreeHelper.GetDpi(this).DpiScaleX);
    }

    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            if (FindDescendant<T>(child) is { } deeper) return deeper;
        }
        return null;
    }

    // ---- Updates ----

    // Closing the bar hides that version for the rest of the run, also after a language change.
    private static SemanticVersion? s_dismissedUpdate;

    private bool _installingUpdate;

    private void Updater_Checked(object? sender, EventArgs e) => ShowUpdateBar();

    private void ShowUpdateBar()
    {
        if (_installingUpdate)
            return;
        if (Updater.Available is not { } release || release.Version == s_dismissedUpdate)
        {
            UpdateBar.Visibility = Visibility.Collapsed;
            return;
        }
        UpdateText.Text = Strings.Format("UpdateAvailableBar", release.Version);
        UpdateActions.Visibility = Visibility.Visible;
        UpdateCloseButton.Visibility = Visibility.Visible;
        UpdateProgress.Visibility = Visibility.Collapsed;
        UpdateBar.Visibility = Visibility.Visible;
    }

    private async void UpdateInstall_Click(object sender, RoutedEventArgs e)
    {
        if (Updater.Available is not { } release)
            return;

        UpdateActions.Visibility = Visibility.Collapsed;
        UpdateCloseButton.Visibility = Visibility.Collapsed;
        UpdateProgress.Value = 0;
        UpdateProgress.IsIndeterminate = false;
        UpdateProgress.Visibility = Visibility.Visible;
        UpdateText.Text = Strings.Get("UpdateDownloading");
        var progress = new Progress<double>(p =>
        {
            UpdateProgress.Value = p;
            if (p < 1)
                return;
            // Downloaded: the installer now waits for the UAC prompt, then installs.
            UpdateProgress.IsIndeterminate = true;
            UpdateText.Text = Strings.Get("UpdateStarting");
        });

        _installingUpdate = true;
        string message;
        try
        {
            if (await Updater.InstallAsync(release, progress))
                return; // the app is closing, the installer takes over
            message = Strings.Get("UpdateCancelled");
        }
        catch (Exception ex)
        {
            message = Strings.Format("ErrorUpdate", Updater.Describe(ex));
        }
        _installingUpdate = false;
        // The bar stays, with the reason, so that the update can be tried again.
        ShowUpdateBar();
        UpdateText.Text = message;
    }

    private void UpdateNotes_Click(object sender, RoutedEventArgs e)
    {
        if (Updater.Available is { } release)
            Updater.OpenReleasePage(release);
    }

    private void CloseUpdate_Click(object sender, RoutedEventArgs e)
    {
        s_dismissedUpdate = Updater.Available?.Version;
        UpdateBar.Visibility = Visibility.Collapsed;
    }

    // ---- Presets ----

    private void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ReloadPresets();
        var dialog = new PresetNameWindow(ViewModel.SelectedPreset?.Name ?? "", [.. ViewModel.Presets.Select(p => p.Name)]) { Owner = this };
        if (dialog.ShowDialog() == true)
            ViewModel.SavePreset(dialog.PresetName);
    }

    private void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedPreset is not { } preset) return;
        if (MessageBox.Show(this, Strings.Format("PresetDeleteConfirm", preset.Name), Strings.Get("AppTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            ViewModel.DeleteSelectedPreset();
    }

    // Another window may have saved presets meanwhile.
    private void Presets_DropDownOpened(object? sender, EventArgs e) => ViewModel.ReloadPresets();

    // ---- Settings ----

    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings();

    public void OpenSettings()
    {
        var settings = new SettingsWindow { Owner = this };
        settings.ShowDialog();
        ViewModel.ReloadPresets(); // presets may have been imported
        if (settings.LanguageChanged)
            ((App)Application.Current).ReloadMainWindow(this);
    }

    private void Window_Closing(object? sender, CancelEventArgs e) => ViewModel.SaveSettings();

    protected override void OnClosed(EventArgs e)
    {
        Updater.Checked -= Updater_Checked;
        base.OnClosed(e);
    }
}
