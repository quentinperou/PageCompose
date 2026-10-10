using System.Windows.Input;
using System.Windows.Media;
using PageCompose.Core;
using PageCompose.Core.Visuals;

namespace PageCompose.App.ViewModels;

/// <summary>Entry of the page list of a PDF: "Page 2 / 3".</summary>
public sealed record PageOption(int Number, string Name)
{
    public override string ToString() => Name;
}

/// <summary>A visual of the list, with its number of copies, and its page when it is a PDF of several pages.</summary>
public sealed class VisualItem : ObservableObject
{
    private readonly Action _changed;
    private readonly Func<VisualItem, int, Task>? _choosePage;

    /// <param name="choosePage">Loads another page of the PDF into this item (see <see cref="Replace"/>).</param>
    public VisualItem(VisualInfo info, Action changed, Action<VisualItem> remove, Func<VisualItem, int, Task>? choosePage = null)
    {
        _info = info;
        _changed = changed;
        _choosePage = choosePage;
        Pages = [.. Enumerable.Range(1, info.PageCount).Select(n => new PageOption(n, Strings.Format("PageOf", n, info.PageCount)))];
        _selectedPage = Pages[Math.Clamp(info.PageNumber, 1, Pages.Count) - 1];
        IncrementCommand = new RelayCommand(() => Copies++);
        DecrementCommand = new RelayCommand(() => Copies--);
        RemoveCommand = new RelayCommand(() => remove(this));
        RotateRightCommand = new RelayCommand(() => Rotate(90));
        RotateLeftCommand = new RelayCommand(() => Rotate(-90));
        RotateHalfCommand = new RelayCommand(() => Rotate(180));
    }

    private VisualInfo _info;

    /// <summary>The visual, with the turn chosen by the user (applied on the sheet after the automatic one).</summary>
    public VisualInfo Info => _rotation == 0 ? _info : _info with { UserRotation = _rotation };

    /// <summary>The visual as read from the file, before the user's turn (the thumbnail turns on screen).</summary>
    public VisualInfo SourceInfo => _info;

    private int _rotation;

    /// <summary>
    /// Clockwise turn chosen by the user (0, 90, 180, 270), from the context menu of the file line: it turns the
    /// visual as the sheet shows it, after the automatic quarter turn (<see cref="VisualInfo.UserRotation"/>); the
    /// preview, the PDF, the resolution and the cropping follow. Not part of the presets (it belongs to the file).
    /// </summary>
    public int Rotation => _rotation;

    /// <summary>The same turn as <paramref name="other"/> (the visual carried over to the other mode).</summary>
    public void RotateLike(VisualItem other) => Rotate(other.Rotation - _rotation);

    private void Rotate(int degrees)
    {
        if (degrees % 360 == 0) return;
        _rotation = ((_rotation + degrees) % 360 + 360) % 360;
        OnPropertyChanged(nameof(Rotation));
        OnPropertyChanged(nameof(Info));
        OnPropertyChanged(nameof(Details));
        _changed();
    }
    public string FileName => Info.FileName;
    public string Details => Describe(Info);

    /// <summary>Pages of the file: the page list shows when there are several.</summary>
    public IReadOnlyList<PageOption> Pages { get; }
    public bool HasPages => Pages.Count > 1;

    private PageOption _selectedPage;
    public PageOption SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (value is null || !Set(ref _selectedPage, value) || _choosePage is null) return;
            _ = _choosePage(this, value.Number);
        }
    }

    /// <summary>Another page of the same file, read by the view model.</summary>
    public void Replace(VisualInfo info)
    {
        _info = info;
        OnPropertyChanged(nameof(Info));
        OnPropertyChanged(nameof(Details));
        _changed();
    }

    private int _copies = 1;
    public int Copies
    {
        get => _copies;
        set { if (Set(ref _copies, Math.Clamp(value, 1, 100_000))) _changed(); }
    }

    private ImageSource? _thumbnail;
    public ImageSource? Thumbnail { get => _thumbnail; set => Set(ref _thumbnail, value); }

    /// <summary>Effective resolution at the chosen size ("300 dpi"), or "PDF".</summary>
    private string? _quality;
    public string? Quality { get => _quality; set => Set(ref _quality, value); }

    private bool _isLowQuality;
    public bool IsLowQuality { get => _isLowQuality; set => Set(ref _isLowQuality, value); }

    public ICommand IncrementCommand { get; }
    public ICommand DecrementCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand RotateRightCommand { get; }
    public ICommand RotateLeftCommand { get; }
    public ICommand RotateHalfCommand { get; }

    public static string Describe(VisualInfo v)
    {
        if (v.Kind == VisualKind.Image)
        {
            var (w, h) = v.TurnedContentSize;
            // A PDF converted to an image reads "PDF · 1075 × 721 px".
            return Strings.Format("DescribeImage", Path.GetExtension(v.OriginalPath).TrimStart('.').ToUpperInvariant(), $"{w:0}", $"{h:0}");
        }

        string pdf = Strings.Format("DescribePdf", $"{Units.PtToMm(v.FullWidth):0.#}", $"{Units.PtToMm(v.FullHeight):0.#}");
        return v.HasOwnBleed
            ? pdf + Strings.Format("DescribeTrim", $"{Units.PtToMm(v.Content.Width):0.#}", $"{Units.PtToMm(v.Content.Height):0.#}")
            : pdf;
    }
}
