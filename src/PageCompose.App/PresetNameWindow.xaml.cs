using System.Windows;
using System.Windows.Controls;
using PageCompose.App.Services;

namespace PageCompose.App;

/// <summary>Asks the name of a preset; a name already used means replacing that preset, which the window says.</summary>
public partial class PresetNameWindow : Window
{
    private readonly IReadOnlyList<string> _existing;

    public PresetNameWindow(string initialName, IReadOnlyList<string> existing)
    {
        _existing = existing;
        InitializeComponent();
        MicaBackdrop.Apply(this);
        NameBox.Text = initialName;
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    /// <summary>The name typed, once the window closes with Save.</summary>
    public string PresetName => NameBox.Text.Trim();

    private void NameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SaveButton.IsEnabled = PresetName.Length > 0;
        string? replaced = _existing.FirstOrDefault(n => PresetStore.SameName(n, PresetName));
        ReplaceText.Text = replaced is null ? "" : Strings.Format("PresetReplaces", replaced);
        ReplaceText.Visibility = replaced is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
