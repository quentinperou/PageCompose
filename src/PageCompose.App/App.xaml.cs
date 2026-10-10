using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using PageCompose.App.Services;
using PageCompose.App.ViewModels;

namespace PageCompose.App;

public partial class App : Application
{
    // Windows display language, kept to come back to it when the user picks "Windows language".
    private static readonly CultureInfo SystemUICulture = CultureInfo.CurrentUICulture;

    /// <summary>Settings shared by every window (one instance, saved on change and on exit).</summary>
    public static AppSettings Settings { get; } = AppSettings.Load();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // UI language: the one chosen in Settings, else the Windows one (French, otherwise English).
        // PAGECOMPOSE_LANG=en|fr forces one, handy to test both.
        ApplyLanguage(Environment.GetEnvironmentVariable("PAGECOMPOSE_LANG") is { Length: > 0 } forced
            ? forced
            : Settings.Language);

        // Light or dark: the theme chosen in Settings (by default the Windows one).
        // PAGECOMPOSE_THEME=Light|Dark forces one, handy to test both.
        ApplyTheme(Enum.TryParse<AppTheme>(Environment.GetEnvironmentVariable("PAGECOMPOSE_THEME"), ignoreCase: true, out var forcedTheme)
            ? forcedTheme
            : Settings.Theme);

        // WPF bindings parse numbers as en-US by default: use the regional format of the PC (decimal comma in France).
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

        // The wheel over a closed drop-down list scrolls the settings instead of changing the list: the author changed
        // settings by accident while scrolling. A list changes only once opened.
        EventManager.RegisterClassHandler(typeof(ComboBox), UIElement.PreviewMouseWheelEvent,
            new MouseWheelEventHandler(ComboBox_PreviewMouseWheel));

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(Strings.Format("UnexpectedError", args.Exception.Message), Strings.Get("AppTitle"),
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        PdfRasterizer.DeleteOldFiles();
        var window = new MainWindow(new MainViewModel(Settings));
        MainWindow = window;
        window.Show();

        // Files or folders given on the command line (dropped on the app icon) become visuals; a preset file
        // (double click on a .PageCompose file) is imported.
        _ = window.AddPathsAsync(e.Args);

        // Once per run (not when the window is rebuilt for a language change); the window shows the offer.
        _ = Updater.CheckAtStartupAsync();
    }

    private static void ComboBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ComboBox { IsDropDownOpen: false } combo) return;
        e.Handled = true;

        DependencyObject? parent = combo;
        while (parent is not null and not ScrollViewer)
            parent = VisualTreeHelper.GetParent(parent);
        (parent as ScrollViewer)?.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = combo,
        });
    }

    /// <summary>Switches every window to the light or dark Fluent theme, or back to the Windows one.</summary>
    public static void ApplyTheme(AppTheme theme) =>
        Current.ThemeMode = theme switch
        {
            AppTheme.Light => ThemeMode.Light,
            AppTheme.Dark => ThemeMode.Dark,
            _ => ThemeMode.System,
        };

    /// <summary>Sets the UI language: <see cref="AppSettings.SystemLanguage"/> or a culture name ("en", "fr").</summary>
    public static void ApplyLanguage(string language)
    {
        var culture = language == AppSettings.SystemLanguage ? SystemUICulture : CultureInfo.GetCultureInfo(language);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    /// <summary>
    /// Texts are read once, when a window is built: to switch language, the main window is replaced by a new one
    /// at the same place, on the same view model (the work in progress is kept), with the Settings window open again.
    /// </summary>
    public void ReloadMainWindow(MainWindow old)
    {
        ApplyLanguage(Settings.Language);

        var bounds = old.WindowState == WindowState.Normal ? new Rect(old.Left, old.Top, old.Width, old.Height) : old.RestoreBounds;
        var window = new MainWindow(old.ViewModel)
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            WindowState = old.WindowState == WindowState.Maximized ? WindowState.Maximized : WindowState.Normal,
        };

        MainWindow = window;
        window.Show();
        old.Close();
        old.ViewModel.RefreshTexts();
        window.Dispatcher.BeginInvoke(window.OpenSettings, DispatcherPriority.ApplicationIdle);
    }
}
