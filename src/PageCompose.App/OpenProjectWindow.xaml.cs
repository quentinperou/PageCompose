using System.Windows;
using PageCompose.App.Services;

namespace PageCompose.App;

/// <summary>
/// A project dropped on a window that already holds work: it takes its place, or opens in a new window (asked by the
/// author). Cancel, or the window closed: it is not opened.
/// </summary>
public partial class OpenProjectWindow : Window
{
    public OpenProjectWindow(string fileName)
    {
        InitializeComponent();
        MicaBackdrop.Apply(this);
        MessageText.Text = Strings.Format("OpenProjectMessage", fileName);
        Loaded += (_, _) => NewWindowButton.Focus();
    }

    /// <summary>True: in place of the work in progress; false: in a new window. Read once the window closes with OK.</summary>
    public bool Replace { get; private set; }

    private void NewWindow_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        Replace = true;
        DialogResult = true;
    }
}
