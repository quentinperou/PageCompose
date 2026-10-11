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
    /// <param name="format">Its size in the Mixed mode (<see cref="Format"/>).</param>
    /// <param name="duplicate">Adds a copy of the line below it (lines of a list only: none for a single visual).</param>
    public VisualItem(VisualInfo info, Action changed, Action<VisualItem> remove, Func<VisualItem, int, Task>? choosePage = null,
        FormatOption? format = null, Action<VisualItem>? duplicate = null)
    {
        _info = info;
        _format = format;
        _changed = changed;
        _choosePage = choosePage;
        DuplicateCommand = duplicate is null ? null : new RelayCommand(() => duplicate(this));
        Pages = [.. Enumerable.Range(1, info.PageCount).Select(n => new PageOption(n, Strings.Format("PageOf", n, info.PageCount)))];
        _selectedPage = Pages[Math.Clamp(info.PageNumber, 1, Pages.Count) - 1];
        IncrementCommand = new RelayCommand(() => Copies++);
        DecrementCommand = new RelayCommand(() => Copies--);
        RemoveCommand = new RelayCommand(() => remove(this));
        RotateRightCommand = new RelayCommand(() => Rotate(90));
        RotateLeftCommand = new RelayCommand(() => Rotate(-90));
        RotateHalfCommand = new RelayCommand(() => Rotate(180));
        RotateAutoCommand = new RelayCommand(() => Rotate(-_rotation));
        RecenterCommand = new RelayCommand(() => SetCrop(0.5, 0.5));
    }

    private VisualInfo _info;

    /// <summary>
    /// The visual, with the turn chosen by the user (applied on the sheet after the automatic one) and the part of it
    /// kept, moved on the preview.
    /// </summary>
    public VisualInfo Info => _rotation == 0 && !IsCropMoved ? _info : _info with { UserRotation = _rotation, CropX = _cropX, CropY = _cropY };

    private double _cropX = 0.5, _cropY = 0.5;

    /// <summary>
    /// Part of the visual kept when it overflows its cell (<see cref="VisualInfo.CropX"/>), chosen by dragging it on the
    /// preview: every copy of the line follows. Like the turn, it belongs to the line, not to the presets.
    /// </summary>
    public (double X, double Y) Crop => (_cropX, _cropY);

    public bool IsCropMoved => Math.Abs(_cropX - 0.5) > 1e-9 || Math.Abs(_cropY - 0.5) > 1e-9;

    public void SetCrop(double x, double y)
    {
        if (Math.Abs(x - _cropX) < 1e-9 && Math.Abs(y - _cropY) < 1e-9) return;
        (_cropX, _cropY) = (x, y);
        OnPropertyChanged(nameof(Info));
        OnPropertyChanged(nameof(IsCropMoved));
        _changed();
    }

    /// <summary>The visual as stored in its file (no turn), for the preview of a drag; loaded at the first drag.</summary>
    public ImageSource? DragImage { get; set; }

    /// <summary>The visual as read from the file, before the user's turn (the thumbnail turns on screen).</summary>
    public VisualInfo SourceInfo => _info;

    private int _rotation;

    /// <summary>
    /// Clockwise turn chosen by the user (0, 90, 180, 270), from the context menu of the file line: it turns the
    /// visual as the sheet shows it, after the automatic quarter turn (<see cref="VisualInfo.UserRotation"/>); the
    /// preview, the PDF, the resolution and the cropping follow. Not part of the presets (it belongs to the file).
    /// </summary>
    public int Rotation => _rotation;

    /// <summary>Turned by hand: "Automatic rotation" in the context menu gives the automatic turn alone back.</summary>
    public bool IsTurnedByHand => _rotation != 0;

    /// <summary>
    /// The same turn and size as <paramref name="other"/>: the visual carried over to the other mode, or a duplicated
    /// line.
    /// </summary>
    public void CopyChoicesOf(VisualItem other)
    {
        Rotate(other.Rotation - _rotation);
        if (other.Format is not null) _format = other.Format;
        (_customWidth, _customHeight) = (other.CustomWidth, other.CustomHeight);
        (_cropX, _cropY) = other.Crop;
    }

    private void Rotate(int degrees)
    {
        if (degrees % 360 == 0) return;
        _rotation = ((_rotation + degrees) % 360 + 360) % 360;
        OnPropertyChanged(nameof(Rotation));
        OnPropertyChanged(nameof(IsTurnedByHand));
        OnPropertyChanged(nameof(Info));
        OnPropertyChanged(nameof(Details));
        _changed();
    }
    public string FileName => Info.FileName;
    public string Details => Describe(Info);

    /// <summary>The file name, with the page of a PDF of several pages: "card.pdf · page 2" (a back, on its line).</summary>
    public string NameWithPage => Info.PageCount > 1 ? Strings.Format("NameWithPage", FileName, Info.PageNumber) : FileName;

    private VisualItem? _back;

    /// <summary>
    /// Its own back when the layout is double-sided (lines of a list); none: the shared back of the "Double-sided"
    /// card. Like the turn, it belongs to the file line, not to the presets.
    /// </summary>
    public VisualItem? Back
    {
        get => _back;
        set
        {
            if (!Set(ref _back, value)) return;
            IsBackAuto = false;
            OnPropertyChanged(nameof(HasBack));
            _changed();
        }
    }

    public bool HasBack => _back is not null;

    /// <summary>A back found by the app (page 2 of the PDF, a "-verso" file added with it), without recomputing anything.</summary>
    public void InitBack(VisualItem back, bool auto)
    {
        _back = back;
        IsBackAuto = auto;
        OnPropertyChanged(nameof(Back));
        OnPropertyChanged(nameof(HasBack));
    }

    private bool _isBackAuto;

    /// <summary>The back was found by the app: an "auto" badge says so on the line.</summary>
    public bool IsBackAuto { get => _isBackAuto; private set => Set(ref _isBackAuto, value); }

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
        OnPropertyChanged(nameof(NameWithPage));
        _changed();
    }

    private int _copies = 1;
    public int Copies
    {
        get => _copies;
        set { if (Set(ref _copies, Math.Clamp(value, 1, 100_000))) _changed(); }
    }

    private FormatOption? _format;

    /// <summary>
    /// Size of this visual in the Mixed mode, chosen on its line (preset sizes only); null until the line is shown in
    /// that mode. Like the turn by hand, it belongs to the file line, not to the presets.
    /// </summary>
    public FormatOption? Format
    {
        get => _format;
        set
        {
            if (value is null || !Set(ref _format, value)) return;
            OnPropertyChanged(nameof(IsCustomFormat));
            _changed();
        }
    }

    /// <summary>A size of its own ("Custom" in the list of the line): its width and height show beside the list.</summary>
    public bool IsCustomFormat => _format?.IsCustom == true;

    private double _customWidth = 100, _customHeight = 150;

    /// <summary>Width of the custom size of the line, in millimetres.</summary>
    public double CustomWidth { get => _customWidth; set { if (Set(ref _customWidth, value)) _changed(); } }

    /// <summary>Height of the custom size of the line, in millimetres.</summary>
    public double CustomHeight { get => _customHeight; set { if (Set(ref _customHeight, value)) _changed(); } }

    /// <summary>The custom size a new line starts with (that of the size section), without recomputing anything.</summary>
    public void InitCustomSize(double width, double height) => (_customWidth, _customHeight) = (width, height);

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
    public ICommand RotateAutoCommand { get; }

    /// <summary>"Centre the photo": the part kept back to the middle, as before it was dragged.</summary>
    public ICommand RecenterCommand { get; }

    /// <summary>"Duplicate the line": the same visual, page, turn and size, once, on a new line below.</summary>
    public ICommand? DuplicateCommand { get; }

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
