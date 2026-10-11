using System.Windows;
using System.Windows.Controls;
using PageCompose.App.Services;
using PageCompose.App.ViewModels;

namespace PageCompose.App;

/// <summary>
/// Asks, when PDFs of several pages are added to a list, which pages to add: all of them (one line each), or one page
/// (chosen here for a single PDF; the first of each for several, changed afterwards on its line). Asked once for all
/// the PDFs dropped together. Cancel, or the window closed: they are not added.
/// </summary>
public partial class PdfPagesWindow : Window
{
    /// <param name="pdfs">The PDFs of several pages added, with their number of pages.</param>
    public PdfPagesWindow(IReadOnlyList<(string FileName, int Pages)> pdfs)
    {
        InitializeComponent();
        MicaBackdrop.Apply(this);
        if (pdfs is [var single])
        {
            MessageText.Text = Strings.Format("PdfPagesOne", single.FileName, single.Pages);
            PageList.ItemsSource = Enumerable.Range(1, single.Pages).Select(n => new PageOption(n, Strings.Format("PageOf", n, single.Pages))).ToList();
            PageList.SelectedIndex = 0;
        }
        else
        {
            MessageText.Text = Strings.Format("PdfPagesMany", pdfs.Count, pdfs.Sum(p => p.Pages));
            OnePagePanel.Visibility = Visibility.Collapsed;
            FirstRadio.Visibility = Visibility.Visible;
        }
        Loaded += (_, _) => AddButton.Focus();
    }

    /// <summary>The pages chosen, once the window closes with Add.</summary>
    public PdfPagesChoice Choice => AllRadio.IsChecked == true
        ? new PdfPagesChoice(true)
        : new PdfPagesChoice(false, OneRadio.IsChecked == true && PageList.SelectedItem is PageOption page ? page.Number : 1);

    // Choosing a page means one page.
    private void PageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) OneRadio.IsChecked = true;
    }

    private void Add_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
