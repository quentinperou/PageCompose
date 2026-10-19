using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using Microsoft.Win32;
using PageCompose.App.Services;
using PageCompose.Core;

namespace PageCompose.App;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings = App.Settings;
    private readonly bool _initialized;
    private bool _updating; // an update check or download is running
    private readonly ScrollViewer[] _pages;

    public SettingsWindow()
    {
        InitializeComponent();
        MicaBackdrop.Apply(this);
        _pages = [GeneralPage, PresetsPage, UpdatesPage, AboutPage];
        // The pages scroll rather than the window going past the screen.
        MaxHeight = SystemParameters.WorkArea.Height;

        LanguageBox.Items.Add(new LanguageOption(AppSettings.SystemLanguage, Strings.Get("LanguageSystem")));
        foreach (string code in AppSettings.SupportedLanguages)
            LanguageBox.Items.Add(new LanguageOption(code, NativeName(code)));
        LanguageBox.SelectedItem = LanguageBox.Items.Cast<LanguageOption>().First(o => o.Code == _settings.Language);

        ThemeBox.Items.Add(new ThemeOption(AppTheme.System, Strings.Get("ThemeSystem")));
        ThemeBox.Items.Add(new ThemeOption(AppTheme.Light, Strings.Get("ThemeLight")));
        ThemeBox.Items.Add(new ThemeOption(AppTheme.Dark, Strings.Get("ThemeDark")));
        ThemeBox.SelectedItem = ThemeBox.Items.Cast<ThemeOption>().First(o => o.Theme == _settings.Theme);

        StartupBox.Items.Add(Strings.Get("StartupNewJob"));
        StartupBox.Items.Add(Strings.Get("StartupLastJob"));
        StartupBox.SelectedIndex = _settings.ResumeLastJob ? 1 : 0;

        ChannelBox.Items.Add(new ChannelOption(UpdateChannel.Stable, Strings.Get("UpdateChannelStable")));
        ChannelBox.Items.Add(new ChannelOption(UpdateChannel.Beta, Strings.Get("UpdateChannelBeta")));
        ChannelBox.SelectedItem = ChannelBox.Items.Cast<ChannelOption>().First(o => o.Channel == _settings.UpdateChannel);
        CheckAtStartupBox.IsChecked = _settings.CheckForUpdatesAtStartup;
        ConvertToSrgbBox.IsChecked = _settings.ConvertToSrgb;
        LightPreviewBox.IsChecked = _settings.LightPreview;
        ShowUpdateState();
        Updater.Checked += Updater_Checked;
        Closed += (_, _) => Updater.Checked -= Updater_Checked;

        VersionText.Text = Strings.Format("AboutVersion", Updater.CurrentVersion);
        ShowPresetCount();
        _initialized = true;
    }

    // The page chosen in the menu; the others stay in place, hidden, so that the window keeps its height.
    private void PageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_pages is null) return; // the first page is chosen while the window is built
        for (int i = 0; i < _pages.Length; i++)
            _pages[i].Visibility = i == PageList.SelectedIndex ? Visibility.Visible : Visibility.Hidden;
    }

    /// <summary>True when the user picked another language: the main window must be rebuilt.</summary>
    public bool LanguageChanged { get; private set; }

    // Languages are listed in their own language ("English", "Français"), whatever the current UI language.
    private static string NativeName(string code)
    {
        var culture = CultureInfo.GetCultureInfo(code);
        return culture.TextInfo.ToUpper(culture.NativeName[0]) + culture.NativeName[1..];
    }

    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || LanguageBox.SelectedItem is not LanguageOption option || option.Code == _settings.Language)
            return;

        _settings.Language = option.Code;
        _settings.Save();
        LanguageChanged = true;
        // The main window reloads in the new language and reopens this page.
        Close();
    }

    // The theme applies at once to every window (their colours are dynamic resources).
    private void ThemeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || ThemeBox.SelectedItem is not ThemeOption option || option.Theme == _settings.Theme)
            return;

        _settings.Theme = option.Theme;
        _settings.Save();
        App.ApplyTheme(option.Theme);
    }

    private void StartupBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;
        _settings.ResumeLastJob = StartupBox.SelectedIndex == 1;
        _settings.Save();
    }

    // ---------------------------------------------------------------- Updates

    private void Updater_Checked(object? sender, EventArgs e) => ShowUpdateState();

    /// <summary>
    /// The installed version, then what the last check found (nothing before a check), or <paramref name="status"/>
    /// (progress, error) instead.
    /// </summary>
    private void ShowUpdateState(string? status = null)
    {
        var available = Updater.Available;
        InstalledVersionText.Text = Strings.Format("UpdateStatusIdle", Updater.CurrentVersion);
        UpdateStatusText.Text = status
            ?? (available is not null ? Strings.Format("UpdateAvailableBar", available.Version)
                : Updater.HasChecked ? Strings.Get("UpdateUpToDate")
                : "");
        UpdateStatusText.Visibility = UpdateStatusText.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateNotesLink.Visibility = available is not null && status is null ? Visibility.Visible : Visibility.Collapsed;

        UpdateButton.Content = available is null ? Strings.Get("UpdateCheck") : Strings.Get("UpdateInstall");
        if (available is null)
            UpdateButton.ClearValue(StyleProperty);
        else
            UpdateButton.SetResourceReference(StyleProperty, "AccentButtonStyle");
        UpdateButton.IsEnabled = !_updating;
    }

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (Updater.Available is { } release)
            await InstallUpdateAsync(release);
        else
            await CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        _updating = true;
        ShowUpdateState(Strings.Get("UpdateStatusChecking"));
        string? error = null;
        try
        {
            await Updater.CheckAsync(_settings.UpdateChannel);
        }
        catch (Exception ex)
        {
            error = Strings.Format("UpdateCheckFailed", Updater.Describe(ex));
        }
        _updating = false;
        ShowUpdateState(error);
    }

    private async Task InstallUpdateAsync(ReleaseInfo release)
    {
        // The installer closes the app: the changes of the project first, if wanted (as from the bar of the window).
        var main = Owner as MainWindow;
        if (main is not null && !await main.BeginUpdateAsync()) return;

        _updating = true;
        ShowUpdateState(Strings.Get("UpdateDownloading"));
        UpdateProgress.Value = 0;
        UpdateProgress.IsIndeterminate = false;
        UpdateProgress.Visibility = Visibility.Visible;
        var progress = new Progress<double>(p =>
        {
            UpdateProgress.Value = p;
            if (p < 1)
                return;
            // Downloaded: the installer now waits for the UAC prompt, then installs.
            UpdateProgress.IsIndeterminate = true;
            ShowUpdateState(Strings.Get("UpdateStarting"));
        });

        string status;
        try
        {
            if (await Updater.InstallAsync(release, progress))
                return; // the app is closing, the installer takes over
            status = Strings.Get("UpdateCancelled");
        }
        catch (Exception ex)
        {
            status = Strings.Format("ErrorUpdate", Updater.Describe(ex));
        }
        _updating = false;
        main?.EndUpdate();
        UpdateProgress.Visibility = Visibility.Collapsed;
        ShowUpdateState(status);
    }

    private void UpdateNotes_Click(object sender, RoutedEventArgs e)
    {
        if (Updater.Available is { } release)
            Updater.OpenReleasePage(release);
    }

    private void CheckAtStartup_Click(object sender, RoutedEventArgs e)
    {
        _settings.CheckForUpdatesAtStartup = CheckAtStartupBox.IsChecked == true;
        _settings.Save();
    }

    // For the photos added from now on: those already in the list stay as they were read.
    private void ConvertToSrgb_Click(object sender, RoutedEventArgs e)
    {
        _settings.ConvertToSrgb = ConvertToSrgbBox.IsChecked == true;
        _settings.Save();
    }

    // The preview is drawn again when the window closes (MainWindow.OpenSettings).
    private void LightPreview_Click(object sender, RoutedEventArgs e)
    {
        _settings.LightPreview = LightPreviewBox.IsChecked == true;
        _settings.Save();
    }

    private async void ChannelBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || ChannelBox.SelectedItem is not ChannelOption option || option.Channel == _settings.UpdateChannel)
            return;

        _settings.UpdateChannel = option.Channel;
        _settings.Save();
        // What is offered depends on the channel: look again.
        if (!_updating)
            await CheckForUpdatesAsync();
    }

    // ---------------------------------------------------------------- Presets

    // The count, the result of the last import or export under it, and what can be exported: all the presets first,
    // then each one (the choice is kept when the list changes).
    private void ShowPresetCount(string? result = null)
    {
        var presets = PresetStore.Load();
        PresetsCountText.Text = presets.Count == 0 ? Strings.Get("PresetNone") : Strings.Count("PresetsCount", presets.Count);
        PresetsStatus.Text = result ?? "";
        PresetsStatus.Visibility = result is null ? Visibility.Collapsed : Visibility.Visible;

        var chosen = ExportPresetBox.SelectedItem as ExportChoice;
        ExportPresetBox.Items.Clear();
        if (presets.Count > 0)
            ExportPresetBox.Items.Add(new ExportChoice(null, Strings.Format("PresetsExportAll", presets.Count)));
        foreach (var preset in presets)
            ExportPresetBox.Items.Add(new ExportChoice(preset.Name, preset.Name));
        var choices = ExportPresetBox.Items.Cast<ExportChoice>().ToList();
        ExportPresetBox.SelectedItem = choices.FirstOrDefault(c => c.Name is { } name && chosen?.Name is { } old && PresetStore.SameName(name, old))
            ?? choices.FirstOrDefault();
        ExportPresetBox.IsEnabled = ExportPresetsButton.IsEnabled = presets.Count > 0;
    }

    private void ExportPresets_Click(object sender, RoutedEventArgs e)
    {
        if (ExportPresetBox.SelectedItem is not ExportChoice choice) return;
        var dialog = new SaveFileDialog
        {
            Title = Strings.Get("PresetsExportTitle"),
            Filter = Strings.Get("PresetsFilter"),
            FileName = PresetStore.ExportFileName(choice.Name),
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            int count = PresetStore.Export(dialog.FileName, choice.Name);
            string file = Path.GetFileName(dialog.FileName);
            ShowPresetCount(count == 0 ? Strings.Get("PresetMissing") // deleted from another window of the app
                : choice.Name is null ? Strings.Format("PresetsExported", count, file)
                : Strings.Format("PresetExported", choice.Name, file));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowPresetCount(Strings.Format("PresetsFileFailed", ex.Message));
        }
    }

    private void ImportPresets_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = Strings.Get("PresetsImportTitle"), Filter = Strings.Get("PresetsOpenFilter") };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var (imported, replaced) = PresetStore.Import(dialog.FileName);
            ShowPresetCount(Strings.Count("PresetsImported", imported) +
                (replaced > 0 ? Strings.Count("PresetsReplaced", replaced) : ""));
        }
        catch (InvalidDataException)
        {
            ShowPresetCount(Strings.Get("PresetsNotAFile"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowPresetCount(Strings.Format("PresetsFileFailed", ex.Message));
        }
    }

    // ---------------------------------------------------------------- Other

    // No Close button (the cross of the title bar, as in Windows 11): Escape still closes. An open list closes first.
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        Close();
    }

    private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private sealed record LanguageOption(string Code, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed record ThemeOption(AppTheme Theme, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed record ChannelOption(UpdateChannel Channel, string Name)
    {
        public override string ToString() => Name;
    }

    /// <summary>What to export: every preset (<paramref name="Name"/> null) or the one of that name.</summary>
    private sealed record ExportChoice(string? Name, string Label)
    {
        public override string ToString() => Label;
    }
}
