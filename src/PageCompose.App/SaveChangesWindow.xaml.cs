using System.Windows;
using PageCompose.App.Services;

namespace PageCompose.App;

/// <summary>
/// A project saved or opened has changes not saved, and the window closes, or another job takes its place ("New",
/// another project): save them first, or not. Cancel, or the window closed: nothing happens.
/// </summary>
public partial class SaveChangesWindow : Window
{
    public SaveChangesWindow(string projectName)
    {
        InitializeComponent();
        MicaBackdrop.Apply(this);
        MessageText.Text = Strings.Format("SaveChangesMessage", projectName);
        Loaded += (_, _) => SaveButton.Focus();
    }

    /// <summary>True: save first; false: leave the changes. Read once the window closes with OK.</summary>
    public bool Save { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Save = true;
        DialogResult = true;
    }

    private void Discard_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
