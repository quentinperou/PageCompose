using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
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

    public SettingsWindow()
    {
        InitializeComponent();
        MicaBackdrop.Apply(this);

        LanguageBox.Items.Add(new LanguageOption(AppSettings.SystemLanguage, Strings.Get("LanguageSystem")));
        foreach (string code in AppSettings.SupportedLanguages)
            LanguageBox.Items.Add(new LanguageOption(code, NativeName(code)));
        LanguageBox.SelectedItem = LanguageBox.Items.Cast<LanguageOption>().First(o => o.Code == _settings.Language);

        ThemeBox.Items.Add(new ThemeOption(AppTheme.System, Strings.Get("ThemeSystem")));
        ThemeBox.Items.Add(new ThemeOption(AppTheme.Light, Strings.Get("ThemeLight")));
        ThemeBox.Items.Add(new ThemeOption(AppTheme.Dark, Strings.Get("ThemeDark")));
        ThemeBox.SelectedItem = ThemeBox.Items.Cast<ThemeOption>().First(o => o.Theme == _settings.Theme);

        ChannelBox.Items.Add(new ChannelOption(UpdateChannel.Stable, Strings.Get("UpdateChannelStable")));
        ChannelBox.Items.Add(new ChannelOption(UpdateChannel.Beta, Strings.Get("UpdateChannelBeta")));
        ChannelBox.SelectedItem = ChannelBox.Items.Cast<ChannelOption>().First(o => o.Channel == _settings.UpdateChannel);
        CheckAtStartupBox.IsChecked = _settings.CheckForUpdatesAtStartup;
        ShowUpdateState();
        Updater.Checked += Updater_Checked;
        Closed += (_, _) => Updater.Checked -= Updater_Checked;

        VersionText.Text = Strings.Format("AboutVersion", Updater.CurrentVersion);
        ShowPresetCount();
        _initialized = true;
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

    // ---------------------------------------------------------------- Updates

    private void Updater_Checked(object? sender, EventArgs e) => ShowUpdateState();

    /// <summary>Shows what the last check found, or <paramref name="status"/> (progress, error) instead.</summary>
    private void ShowUpdateState(string? status = null)
    {
        var available = Updater.Available;
        UpdateStatusText.Text = status
            ?? (available is not null ? Strings.Format("UpdateStatusAvailable", Updater.CurrentVersion, available.Version)
                : Updater.HasChecked ? Strings.Format("UpdateStatusUpToDate", Updater.CurrentVersion)
                : Strings.Format("UpdateStatusIdle", Updater.CurrentVersion));
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
            error = Strings.Format("UpdateStatusFailed", Updater.CurrentVersion, Updater.Describe(ex));
        }
        _updating = false;
        ShowUpdateState(error);
    }

    private async Task InstallUpdateAsync(ReleaseInfo release)
    {
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
            UpdateStatusText.Text = Strings.Get("UpdateStarting");
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

    // The count, and what can be exported: all the presets first, then each one (the choice is kept when the list
    // changes).
    private void ShowPresetCount(string? result = null)
    {
        var presets = PresetStore.Load();
        PresetsStatus.Text = result ?? (presets.Count == 0 ? Strings.Get("PresetNone") : Strings.Count("PresetsCount", presets.Count));

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
        // Files exported before version 0.4.4 end in .json.
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

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

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
