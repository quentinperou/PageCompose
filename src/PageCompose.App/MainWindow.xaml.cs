using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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
        ShowTitleIcon();
        StateChanged += (_, _) => FitMaximized();
        ApplyZoom();
        viewModel.AskPdfPages = pdfs =>
        {
            var dialog = new PdfPagesWindow(pdfs) { Owner = this };
            return dialog.ShowDialog() == true ? dialog.Choice : null;
        };

        viewModel.ConfirmReplace = AskReplace;
        viewModel.ConfirmDropMissing = count => MessageBox.Show(this, Strings.Count("ProjectDropsMissing", count), Strings.Get("AppTitle"),
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;

        Updater.Checked += Updater_Checked;
        ShowUpdateBar();
        ViewModel.Notices.CollectionChanged += Notices_CollectionChanged;
    }

    // The view model outlives the window (a language change builds another on it): unsubscribed in OnClosed.
    private void Notices_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
        MeasureSingleNotice();

    // Files of an export already in its folder, besides the one chosen in the dialog: replaced only if the user agrees.
    private bool AskReplace(IReadOnlyList<string> files)
    {
        string text = files.Count == 1
            ? Strings.Format("ExportReplaceOne", Path.GetFileName(files[0]))
            : Strings.Format("ExportReplaceMany", files.Count, Path.GetFileName(files[0]));
        return MessageBox.Show(this, text, Strings.Get("AppTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public MainViewModel ViewModel { get; }

    /// <summary>
    /// Adds files and folders (command line, drag and drop) as visuals; a preset file (.PageComposePreset, opened by a
    /// double click) is imported instead, and a project (.PageComposeProject) opened.
    /// </summary>
    public async Task AddPathsAsync(IEnumerable<string> paths)
    {
        var all = paths.ToList();
        foreach (var preset in all.Where(p => PresetStore.IsPresetFile(p) && File.Exists(p)))
            ImportPresets(preset);
        if (all.FirstOrDefault(p => ProjectStore.IsProjectFile(p) && File.Exists(p)) is { } job)
            await OpenProjectAsync(job);

        var files = ExpandPaths(all);
        if (files.Count > 0) await ViewModel.AddFilesAsync(files);
    }

    // In a window that already holds work, the project takes its place or opens in a new window, as asked; the changes
    // of the project it replaces are saved first if wanted.
    private async Task OpenProjectAsync(string path)
    {
        if (ViewModel.HasAnyVisual)
        {
            var ask = new OpenProjectWindow(Path.GetFileName(path)) { Owner = this };
            if (ask.ShowDialog() != true) return;
            if (!ask.Replace)
            {
                StartWindow(path); // as a double click on the file gives
                return;
            }
        }
        if (!await ConfirmLeaveProjectAsync()) return;

        string? error = null;
        try
        {
            await ViewModel.OpenProjectAsync(path);
        }
        catch (InvalidDataException)
        {
            error = Strings.Get("ProjectNotAFile");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = Strings.Format("ProjectOpenFailed", ex.Message);
        }

        if (error is not null)
            MessageBox.Show(this, error, Strings.Get("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
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
            Title = Strings.Get(ViewModel.HasVisualList ? "OpenVisualsTitle" : "OpenVisualTitle"),
            Filter = Strings.Get("VisualsFilter"),
            Multiselect = ViewModel.HasVisualList,
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
        DroppedPaths(e).Any(p => Directory.Exists(p) || VisualLoader.IsSupported(p) || PresetStore.IsPresetFile(p) || ProjectStore.IsProjectFile(p));

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
        var paths = DroppedPaths(e);
        // The drop is answered first: a question asked meanwhile (open a project, the pages of a PDF) would hold the
        // window it comes from, Explorer, until it is answered.
        await Dispatcher.Yield(DispatcherPriority.Background);
        await AddPathsAsync(paths);
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
        {
            // A preset or a project: answered first, as in Window_Drop (a question would hold Explorer).
            await Dispatcher.Yield(DispatcherPriority.Background);
            await AddPathsAsync(paths);
        }
    }

    // The "Back" slot of a line of the list: its own back, dropped or chosen (a file dropped on its back replaces it).
    private async void LineBack_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not VisualItem line) return;
        var dialog = new OpenFileDialog { Title = Strings.Format("OpenLineBackTitle", line.FileName), Filter = Strings.Get("VisualsFilter") };
        if (dialog.ShowDialog(this) == true) await ViewModel.SetLineBackAsync(line, dialog.FileName);
    }

    private async void LineBack_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        DropOverlay.Visibility = Visibility.Collapsed;
        if ((sender as FrameworkElement)?.DataContext is VisualItem line && ExpandPaths(DroppedPaths(e)) is [var first, ..])
            await ViewModel.SetLineBackAsync(line, first);
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

    // A number being typed reaches the job a moment later (its box waits): taken now, before saving or exporting.
    private static void CommitTyping() =>
        (Keyboard.FocusedElement as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

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
    // larger inside the scroll viewer: drag with the middle button, or with Alt and the left button (the left button
    // alone drags the photos, "Reframing" below), or from a place without photo, to move; Ctrl + wheel zooms around
    // the mouse, Ctrl + / - / 0 from the keyboard, a double click zooms in or fits again. The view model renders the
    // preview sharper when it is shown larger (MainViewModel.SetPreviewDisplayWidth).

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
        PreviewSidesView.Cursor = zoomed ? Cursors.Hand : null; // a photo shows the move cursor (it can be dragged)
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
        // Here: Alt held, or a place without photo (a photo takes the left button first, Sheet_MouseLeftButtonDown).
        StartPan(e);
    }

    // The middle button moves the zoomed preview anywhere, photos included.
    private void Preview_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle) StartPan(e);
    }

    private void Preview_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle) EndPan();
    }

    private void StartPan(MouseButtonEventArgs e)
    {
        if (_zoom <= 1) return;
        _panStart = e.GetPosition(PreviewScroll);
        _panOffset = new Vector(PreviewScroll.HorizontalOffset, PreviewScroll.VerticalOffset);
        PreviewSidesView.CaptureMouse();
        e.Handled = true;
    }

    private void EndPan()
    {
        if (_panStart is null) return;
        _panStart = null;
        PreviewSidesView.ReleaseMouseCapture();
    }

    private void Preview_MouseMove(object sender, MouseEventArgs e)
    {
        if (_panStart is not { } start || (e.LeftButton != MouseButtonState.Pressed && e.MiddleButton != MouseButtonState.Pressed)) return;
        var moved = e.GetPosition(PreviewScroll) - start;
        PreviewScroll.ScrollToHorizontalOffset(_panOffset.X - moved.X);
        PreviewScroll.ScrollToVerticalOffset(_panOffset.Y - moved.Y);
    }

    private void Preview_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => EndPan();

    private void Preview_LostMouseCapture(object sender, MouseEventArgs e) => _panStart = null;

    // ---- Reframing: a photo dragged on the preview keeps another part of it ----
    // The left button on a photo of the sheet drags it in its cell (every copy of its line follows, drawn over the
    // preview by the view model until it is rendered again); a double click still zooms, Alt still moves the view.
    // A right click on a photo opens the menu of its line (duplicate, turn, centre the photo…).

    private FrameworkElement? _dragSheet;

    // The sheet under the mouse, and where: Alt pressed or released changes its cursor without a move.
    private FrameworkElement? _hoverSheet;
    private Point _hoverAt;

    private static (bool Front, Point At) SheetPoint(object sender, MouseEventArgs e)
    {
        var sheet = (FrameworkElement)sender;
        return (((PreviewSide)sheet.DataContext).IsFront, e.GetPosition(sheet)); // the sheet is laid out in millimetres
    }

    // Alt held: the hand that moves the zoomed view (nothing to move at 100 %); else the move cursor over a photo.
    private void UpdateSheetCursor(bool alt)
    {
        if (_hoverSheet is not { DataContext: PreviewSide side } sheet || _dragSheet is not null || _panStart is not null) return;
        sheet.Cursor = alt
            ? _zoom > 1 ? Cursors.Hand : null
            : ViewModel.LineAt(side.IsFront, _hoverAt.X, _hoverAt.Y) is null ? null : Cursors.SizeAll;
    }

    private void Sheet_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_hoverSheet == sender) _hoverSheet = null;
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.System && e.SystemKey is Key.LeftAlt or Key.RightAlt) UpdateSheetCursor(alt: false);
    }

    private void Sheet_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount > 1 || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) return;
        var (front, at) = SheetPoint(sender, e);
        if (!ViewModel.BeginCropDrag(front, at.X, at.Y)) return;
        _dragSheet = (FrameworkElement)sender;
        _dragSheet.CaptureMouse();
        e.Handled = true;
    }

    private void Sheet_MouseMove(object sender, MouseEventArgs e)
    {
        var (_, at) = SheetPoint(sender, e);
        if (_dragSheet == sender)
        {
            if (e.LeftButton == MouseButtonState.Pressed) ViewModel.CropDragTo(at.X, at.Y);
            return;
        }
        _hoverSheet = (FrameworkElement)sender;
        _hoverAt = at;
        UpdateSheetCursor(Keyboard.Modifiers.HasFlag(ModifierKeys.Alt));
    }

    private void Sheet_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragSheet != sender) return;
        _dragSheet = null;
        ((FrameworkElement)sender).ReleaseMouseCapture();
        ViewModel.EndCropDrag();
        e.Handled = true;
    }

    private void Sheet_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_dragSheet != sender) return;
        _dragSheet = null;
        ViewModel.EndCropDrag();
    }

    private void Sheet_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var (front, at) = SheetPoint(sender, e);
        if (ViewModel.LineAt(front, at.X, at.Y) is not { } line) return;
        var menu = (ContextMenu)FindResource("VisualMenu"); // not shared: a new menu each time
        menu.DataContext = line;
        menu.PlacementTarget = (UIElement)sender;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
        e.Handled = true;
    }

    // ---- The line of the notices ----

    private void NoticeBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => ViewModel.NoticesOpen = !ViewModel.NoticesOpen;

    private void NoticeBar_SizeChanged(object sender, SizeChangedEventArgs e) => MeasureSingleNotice();

    // Does a notice alone fit its line? Its whole text, in the font of the line, against the width of the text of the
    // line (which fills what the icon leaves; the chevron, shown while summed up, would leave it the room it takes).
    private void MeasureSingleNotice()
    {
        if (ViewModel.Notices.Count != 1 || NoticeText.ActualWidth <= 0) return;
        var typeface = new Typeface(NoticeText.FontFamily, NoticeText.FontStyle, NoticeText.FontWeight, NoticeText.FontStretch);
        var text = new FormattedText(ViewModel.Notices[0].Text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            typeface, NoticeText.FontSize, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        double room = NoticeText.ActualWidth + (ViewModel.CanOpenNotices ? NoticeChevron.ActualWidth + NoticeChevron.Margin.Left : 0);
        ViewModel.SingleNoticeFits = text.WidthIncludingTrailingWhitespace <= room - 2; // a hair kept: rounding to pixels
    }

    // A click anywhere else closes the notices opened over the preview (the click still does what it does).
    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel.NoticesOpen && e.OriginalSource is DependencyObject source && !IsWithin(source, NoticePanel) && !IsWithin(source, NoticeBar))
            ViewModel.NoticesOpen = false;
    }

    private static bool IsWithin(DependencyObject element, DependencyObject ancestor)
    {
        for (var e = element; e is not null; e = e is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(e) : LogicalTreeHelper.GetParent(e))
            if (e == ancestor) return true;
        return false;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && ViewModel.NoticesOpen)
        {
            ViewModel.NoticesOpen = false;
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape && FilePanel.IsOpen) // opened with the mouse: the keyboard is still on the window
        {
            FilePanel.IsOpen = false;
            e.Handled = true;
            return;
        }
        if (e.Key == Key.System && e.SystemKey is Key.LeftAlt or Key.RightAlt)
        {
            UpdateSheetCursor(alt: true); // not handled: Alt keeps its usual part in Windows
            return;
        }
        // Undo, redo: the job (settings and lines), except in a text box that has its own typing to undo.
        if (e.Key is Key.Z or Key.Y && (Keyboard.Modifiers & ModifierKeys.Control) != 0 &&
            !(Keyboard.FocusedElement is TextBox { CanUndo: true } && e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control))
        {
            bool redo = e.Key == Key.Y || (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            if (redo ? ViewModel.Redo() : ViewModel.Undo()) ViewModel.Toast.Hide();
            e.Handled = true;
            return;
        }
        // The File menu: Alt+F opens it, and its commands have their usual shortcuts.
        if (e.Key == Key.System && e.SystemKey == Key.F && Keyboard.Modifiers == ModifierKeys.Alt)
        {
            OpenFilePanel(keyboard: true);
            e.Handled = true;
            return;
        }
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            switch (e.Key)
            {
                case Key.N: StartWindow(); break;
                case Key.S: _ = SaveProjectAsync(saveAs: true); break;
                default: return;
            }
            e.Handled = true;
            return;
        }
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        switch (e.Key)
        {
            case Key.Add or Key.OemPlus: SetZoom(_zoom * ZoomStep); break;
            case Key.Subtract or Key.OemMinus: SetZoom(_zoom / ZoomStep); break;
            case Key.D0 or Key.NumPad0: SetZoom(1); break;
            case Key.N: _ = NewProjectAsync(); break;
            case Key.O: _ = ChooseProjectAsync(); break;
            case Key.S: _ = SaveProjectAsync(saveAs: false); break;
            case Key.E:
                CommitTyping();
                if (ViewModel.CanExport) Export_Click(this, e);
                break;
            case Key.OemComma: OpenSettings(); break;
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
        // The installer closes the app: the changes of the project first, if wanted.
        if (!await ConfirmLeaveProjectAsync())
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

    /// <summary>
    /// An update installed from Settings: the changes of the project first, if wanted, as from the bar; then the window
    /// closes with the app without asking again. False: no update.
    /// </summary>
    public async Task<bool> BeginUpdateAsync()
    {
        if (!await ConfirmLeaveProjectAsync()) return false;
        _installingUpdate = true;
        return true;
    }

    /// <summary>The update from Settings did not happen: the window asks again on closing.</summary>
    public void EndUpdate()
    {
        _installingUpdate = false;
        ShowUpdateBar();
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
        ViewModel.RedrawPreview(); // "Fast preview" may have changed
        if (settings.LanguageChanged)
            ((App)Application.Current).ReloadMainWindow(this);
    }

    // A double click on the zoom slider of a line gives 100 % back (asked by the author).
    private void ZoomSlider_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not VisualItem line) return;
        line.ZoomPercent = 100;
        e.Handled = true;
    }

    // ---- Title bar, File menu (asked by the author) ----
    // The job is a document: "Untitled" until it is saved, then its .PageComposeProject file. Only a project saved or
    // opened asks to save its changes (on closing, "New", another project); a job never saved closes as it always did.

    // The icon of the title bar: the frame of the .ico drawn for the scale of the screen (the small sizes are
    // hand-fitted to whole pixels; the 256 px PNG would be blurry at 16 px).
    private void ShowTitleIcon()
    {
        double size = 16 * VisualTreeHelper.GetDpi(this).DpiScaleX;
        var frames = new IconBitmapDecoder(new Uri("pack://application:,,,/Assets/PageCompose.ico"), BitmapCreateOptions.None,
            BitmapCacheOption.OnLoad).Frames;
        TitleIcon.Source = frames.Where(f => f.PixelWidth >= size - 0.5).MinBy(f => f.PixelWidth) ?? frames.MaxBy(f => f.PixelWidth);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        ShowTitleIcon();
    }

    // Maximized, a window without the Windows title bar reaches beyond the screen by its resize border: its content
    // is moved back in.
    private void FitMaximized()
    {
        var border = SystemParameters.WindowResizeBorderThickness;
        WindowRoot.Margin = WindowState == WindowState.Maximized
            ? new Thickness(border.Left + 4, border.Top + 4, border.Right + 4, border.Bottom + 4)
            : new Thickness(0);
    }

    // The width Windows keeps for its buttons on the right of the title bar (minimize, maximize, close).
    private const double CaptionButtonsWidth = 140;

    private void TitleBar_SizeChanged(object sender, SizeChangedEventArgs e) => PlaceProjectName();

    private void ProjectName_TargetUpdated(object? sender, System.Windows.Data.DataTransferEventArgs e) => PlaceProjectName();

    // The name of the project in the middle of the title bar when it fits between "File" and the buttons of Windows,
    // else right after "File" (asked by the author), shortened with "…" when even that is too narrow.
    private void PlaceProjectName()
    {
        const double gap = 12;
        double width = TitleBar.ActualWidth, after = TitleLeft.ActualWidth + 10, end = width - CaptionButtonsWidth;
        (ProjectName.Margin, ProjectName.MaxWidth) = (new Thickness(0), double.PositiveInfinity); // measured alone
        ProjectName.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double text = ProjectName.DesiredSize.Width, middle = (width - text) / 2;
        double left = middle >= after + gap && middle + text <= end - gap ? middle : after;
        ProjectName.Margin = new Thickness(left, 0, 0, 0);
        ProjectName.MaxWidth = Math.Max(0, end - gap - left);
    }

    private void FileButton_Click(object sender, RoutedEventArgs e) => OpenFilePanel(keyboard: false);

    private void OpenFilePanel(bool keyboard)
    {
        if (FilePanel.IsOpen) return;
        CommitTyping();
        ViewModel.ReloadRecentProjects();
        FilePanel.IsOpen = true;
        if (keyboard) Dispatcher.BeginInvoke(() => FileNewButton.Focus(), DispatcherPriority.Input);
    }

    private void FilePanel_Opened(object? sender, EventArgs e) => FileButton.Tag = "Open";

    private void FilePanel_Closed(object? sender, EventArgs e) => FileButton.Tag = null;

    private void FilePanel_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        FilePanel.IsOpen = false;
        FileButton.Focus();
        e.Handled = true;
    }

    private async void FileNew_Click(object sender, RoutedEventArgs e)
    {
        FilePanel.IsOpen = false;
        await NewProjectAsync();
    }

    private async Task NewProjectAsync()
    {
        if (await ConfirmLeaveProjectAsync()) ViewModel.NewProject();
    }

    private void FileNewWindow_Click(object sender, RoutedEventArgs e)
    {
        FilePanel.IsOpen = false;
        StartWindow();
    }

    /// <summary>Another window of the app (a process of its own), empty or with a project.</summary>
    private static void StartWindow(string? project = null)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
        if (project is not null) start.ArgumentList.Add(project);
        Process.Start(start);
    }

    private async void FileOpen_Click(object sender, RoutedEventArgs e)
    {
        FilePanel.IsOpen = false;
        await ChooseProjectAsync();
    }

    private async Task ChooseProjectAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.Get("OpenProjectDialogTitle"),
            Filter = Strings.Get("ProjectFilter"),
            InitialDirectory = ViewModel.ProjectFolder ?? (ViewModel.LastExportFolder is { } folder && Directory.Exists(folder) ? folder : ""),
        };
        if (dialog.ShowDialog(this) == true) await OpenProjectAsync(dialog.FileName);
    }

    private async void RecentProject_Click(object sender, RoutedEventArgs e)
    {
        FilePanel.IsOpen = false;
        if ((sender as FrameworkElement)?.DataContext is not RecentProjectItem recent) return;
        if (!File.Exists(recent.Path))
        {
            ViewModel.ForgetRecentProject(recent.Path);
            MessageBox.Show(this, Strings.Format("ProjectMissing", recent.Name), Strings.Get("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        await OpenProjectAsync(recent.Path);
    }

    private void ClearRecentProjects_Click(object sender, RoutedEventArgs e) => ViewModel.ClearRecentProjects();

    private async void FileSave_Click(object sender, RoutedEventArgs e)
    {
        FilePanel.IsOpen = false;
        await SaveProjectAsync(saveAs: false);
    }

    private async void FileSaveAs_Click(object sender, RoutedEventArgs e)
    {
        FilePanel.IsOpen = false;
        await SaveProjectAsync(saveAs: true);
    }

    /// <summary>
    /// "Save" (Ctrl+S): into the file of the project, asked the first time; "Save as": always asked. True once saved.
    /// </summary>
    private async Task<bool> SaveProjectAsync(bool saveAs)
    {
        CommitTyping();
        if (!ViewModel.CanSaveProject) return false;
        string? path = saveAs ? null : ViewModel.ProjectPath;
        if (path is null)
        {
            var dialog = new SaveFileDialog
            {
                Title = Strings.Get("SaveProjectDialogTitle"),
                Filter = Strings.Get("ProjectFilter"),
                FileName = ViewModel.ProjectPath is { } current
                    ? Path.GetFileName(current)
                    : Path.ChangeExtension(ViewModel.SuggestedFileName(), ProjectStore.FileExtension),
                InitialDirectory = ViewModel.ProjectFolder
                                   ?? (ViewModel.LastExportFolder is { } folder && Directory.Exists(folder) ? folder : ViewModel.FirstVisualFolder ?? ""),
            };
            if (dialog.ShowDialog(this) != true) return false;
            path = dialog.FileName;
        }

        try
        {
            return await ViewModel.SaveProjectAsync(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ViewModel.Toast.Hide();
            MessageBox.Show(this, Strings.Format("ProjectSaveFailed", ex.Message), Strings.Get("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    /// <summary>
    /// Before another job takes the place of a project saved or opened, or the window closes: its changes saved first if
    /// wanted. False: stay as it is (cancelled, or the save did not happen).
    /// </summary>
    private async Task<bool> ConfirmLeaveProjectAsync()
    {
        CommitTyping();
        if (!ViewModel.HasUnsavedChanges()) return true;
        var ask = new SaveChangesWindow(ViewModel.ProjectTitle) { Owner = this };
        if (ask.ShowDialog() != true) return false;
        return !ask.Save || await SaveProjectAsync(saveAs: false);
    }

    private void FileExportPdf_Click(object sender, RoutedEventArgs e)
    {
        FilePanel.IsOpen = false;
        Export_Click(sender, e);
    }

    private void FileExportJpeg_Click(object sender, RoutedEventArgs e)
    {
        FilePanel.IsOpen = false;
        ExportJpeg_Click(sender, e);
    }

    private void FileSettings_Click(object sender, RoutedEventArgs e)
    {
        FilePanel.IsOpen = false;
        OpenSettings();
    }

    private void FileQuit_Click(object sender, RoutedEventArgs e)
    {
        FilePanel.IsOpen = false;
        Close();
    }

    // Asked once (a project with changes) before closing; the language change closes it without asking (the job goes on
    // in the new window), and so does an update being installed (asked before it starts).
    private bool _closeAsked;

    /// <summary>For a language change: the window is replaced by another on the same job.</summary>
    public void CloseForReload()
    {
        _closeAsked = true;
        Close();
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_closeAsked && !_installingUpdate && ViewModel.HasUnsavedChanges())
        {
            e.Cancel = true;
            if (!await ConfirmLeaveProjectAsync()) return;
            _closeAsked = true;
            _ = Dispatcher.BeginInvoke(Close);
            return;
        }
        ViewModel.SaveSettings();
    }

    protected override void OnClosed(EventArgs e)
    {
        Updater.Checked -= Updater_Checked;
        ViewModel.Notices.CollectionChanged -= Notices_CollectionChanged;
        base.OnClosed(e);
    }
}
