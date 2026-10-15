using System.Windows.Input;
using System.Windows.Media;
using PageCompose.Core;
using PageCompose.Core.Layout;
using PageCompose.Core.Visuals;

namespace PageCompose.App.ViewModels;

/// <summary>Entry of the page list of a PDF: "Page 2 / 3".</summary>
public sealed record PageOption(int Number, string Name)
{
    public override string ToString() => Name;
}

/// <summary>
/// What was chosen on a line at one moment: to undo a change (the same <see cref="Item"/> gets it back) or to save the
/// project. Its back is captured with it.
/// </summary>
public sealed record LineState(VisualItem Item, VisualInfo Info, int Copies, int Rotation, FormatOption? Format,
    double CustomWidth, double CustomHeight, double CropX, double CropY, LineState? Back, bool BackAuto, FitMode? Fit,
    string Caption = "", double Zoom = 1);

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
        ResetFramingCommand = new RelayCommand(ResetFraming);
        FitDefaultCommand = new RelayCommand(() => SetFit(null));
        OwnFillCommand = new RelayCommand(() => SetFit(FitMode.Fill));
        OwnFitCommand = new RelayCommand(() => SetFit(FitMode.Fit));
    }

    private VisualInfo _info;

    /// <summary>
    /// The visual, with the turn chosen by the user (applied on the sheet after the automatic one), the part of it
    /// kept, moved on the preview, and its zoom.
    /// </summary>
    public VisualInfo Info => _rotation == 0 && !IsFramed && _fit is null
        ? _info
        : _info with { UserRotation = _rotation, CropX = _cropX, CropY = _cropY, Fit = _fit, Zoom = _zoom };

    private FitMode? _fit;

    /// <summary>
    /// How it lies in its cell, chosen for it alone (right click on its line): filled or whole; null: as the general
    /// setting says. Like the turn, it belongs to the line, not to the presets.
    /// </summary>
    public FitMode? Fit => _fit;

    private string _caption = "";

    /// <summary>
    /// The text of this photo in the band of its frame; empty: the common text of the "Text in the band" card. Like its
    /// turn, it belongs to the line.
    /// </summary>
    public string Caption { get => _caption; set { if (Set(ref _caption, value ?? "")) _changed(); } }

    private bool _hasCaptionBand;

    /// <summary>Its size has a band to write in (set by the view model): its caption field shows.</summary>
    public bool HasCaptionBand { get => _hasCaptionBand; set => Set(ref _hasCaptionBand, value); }

    public bool IsFitDefault => _fit is null;
    public bool IsOwnFill => _fit == FitMode.Fill;
    public bool IsOwnFit => _fit == FitMode.Fit;

    private void SetFit(FitMode? fit)
    {
        if (_fit == fit) return;
        _fit = fit;
        foreach (var name in new[] { nameof(Fit), nameof(IsFitDefault), nameof(IsOwnFill), nameof(IsOwnFit), nameof(Info) })
            OnPropertyChanged(name);
        _changed();
    }

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
        FramingChanged();
    }

    private double _zoom = 1;

    /// <summary>
    /// The photo enlarged in its cell (<see cref="VisualInfo.Zoom"/>), with the slider of the menu of its line (asked by
    /// the author): 1 to 3; dragged on the preview, it shows another part. Like the part kept, it belongs to the line.
    /// </summary>
    public double Zoom => _zoom;

    /// <summary>The zoom in %, for the slider (100 to 300) and the value beside it.</summary>
    public double ZoomPercent
    {
        get => Math.Round(_zoom * 100);
        set
        {
            double zoom = Math.Clamp(Math.Round(value) / 100, 1, VisualInfo.MaxZoom);
            if (Math.Abs(zoom - _zoom) < 1e-9) return;
            _zoom = zoom;
            FramingChanged();
        }
    }

    public bool IsZoomed => _zoom > 1 + 1e-9;

    /// <summary>Moved or zoomed: "Reset the framing" gives it back centred, at 100 %.</summary>
    public bool IsFramed => IsCropMoved || IsZoomed;

    private void ResetFraming()
    {
        if (!IsFramed) return;
        (_cropX, _cropY, _zoom) = (0.5, 0.5, 1);
        FramingChanged();
    }

    private void FramingChanged()
    {
        foreach (var name in new[] { nameof(Info), nameof(IsCropMoved), nameof(Zoom), nameof(ZoomPercent), nameof(IsZoomed), nameof(IsFramed) })
            OnPropertyChanged(name);
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
        _zoom = other.Zoom;
        _fit = other.Fit;
        _caption = other.Caption;
    }

    public LineState Capture() => new(this, _info, _copies, _rotation, _format, _customWidth, _customHeight, _cropX, _cropY,
        _back?.Capture(), _isBackAuto, _fit, _caption, _zoom);

    /// <summary>
    /// Takes back what <see cref="Capture"/> saw (or a project opened), its back included, without recomputing
    /// anything: the view model does it once.
    /// </summary>
    public void Restore(LineState s)
    {
        if (s.Info.PageNumber != _info.PageNumber) (ThumbnailOutdated, DragImage) = (true, null);
        _info = s.Info;
        (_copies, _rotation, _format, _customWidth, _customHeight, _cropX, _cropY, _fit, _caption, _zoom) =
            (s.Copies, s.Rotation, s.Format, s.CustomWidth, s.CustomHeight, s.CropX, s.CropY, s.Fit, s.Caption, s.Zoom);
        _back = s.Back?.Item;
        if (s.Back is { } back) back.Item.Restore(back);
        _isBackAuto = s.BackAuto;
        _selectedPage = Pages[Math.Clamp(_info.PageNumber, 1, Pages.Count) - 1];
        OnPropertyChanged(string.Empty); // every property
    }

    /// <summary>Another page of its file came back with <see cref="Restore"/>: the view model reads its thumbnail again.</summary>
    public bool ThumbnailOutdated { get; set; }

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
        DragImage = null; // the picture of the other page
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
    /// <summary>"Reset the framing" (was "Recenter the photo" until 0.11.0): centred again, at 100 %.</summary>
    public ICommand ResetFramingCommand { get; }

    /// <summary>Back to the general setting "The visual in its cell".</summary>
    public ICommand FitDefaultCommand { get; }

    /// <summary>This visual fills its cell (cropped if need be), whatever the general setting.</summary>
    public ICommand OwnFillCommand { get; }

    /// <summary>This visual lies whole in its cell, whatever the general setting.</summary>
    public ICommand OwnFitCommand { get; }

    /// <summary>"Duplicate the line": the same visual, page, turn and size, once, on a new line below.</summary>
    public ICommand? DuplicateCommand { get; }

    public static string Describe(VisualInfo v)
    {
        if (v.Kind == VisualKind.Image)
        {
            var (w, h) = v.TurnedContentSize;
            // A PDF converted to an image reads "PDF · 1075 × 721 px"; a photo of another profile than sRGB says it,
            // "· Adobe RGB (1998) → sRGB" once converted at import.
            string image = Strings.Format("DescribeImage", Path.GetExtension(v.OriginalPath).TrimStart('.').ToUpperInvariant(), $"{w:0}", $"{h:0}");
            return v.Profile is { IsSrgb: false } profile
                ? image + Strings.Format(v.ProfileConverted ? "DescribeProfileConverted" : "DescribeProfile", profile.Name)
                : image;
        }

        string pdf = Strings.Format("DescribePdf", $"{Units.PtToMm(v.FullWidth):0.#}", $"{Units.PtToMm(v.FullHeight):0.#}");
        return v.HasOwnBleed
            ? pdf + Strings.Format("DescribeTrim", $"{Units.PtToMm(v.Content.Width):0.#}", $"{Units.PtToMm(v.Content.Height):0.#}")
            : pdf;
    }
}
