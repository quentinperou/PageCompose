using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace PageCompose.App.Services;

/// <summary>
/// Windows 11 Mica behind a window: the material of its title bar, so that the title bar and the window read as one
/// surface, as in the Windows Settings app. It follows the light/dark mode set by WPF on the window. On Windows 10 and
/// early Windows 11, the window keeps the plain background set in its XAML.
/// </summary>
internal static class MicaBackdrop
{
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmsbtMainWindow = 2; // Mica

    /// <summary>The system backdrop attribute exists from Windows 11 22H2 (build 22621).</summary>
    public static bool IsSupported => Environment.OSVersion.Version.Build >= 22621;

    public static void Apply(Window window)
    {
        if (!IsSupported) return;

        window.Background = Brushes.Transparent;
        window.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (HwndSource.FromHwnd(hwnd)?.CompositionTarget is { } target)
                target.BackgroundColor = Colors.Transparent;

            // The whole client area shows the backdrop, then Mica is chosen as that backdrop.
            var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref margins);
            int type = DwmsbtMainWindow;
            DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref type, sizeof(int));
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left, Right, Top, Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
