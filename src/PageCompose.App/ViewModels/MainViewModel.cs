using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PageCompose.App.Services;
using PageCompose.Core;
using PageCompose.Core.Layout;
using PageCompose.Core.Pdf;
using PageCompose.Core.Visuals;

namespace PageCompose.App.ViewModels;

/// <summary>
/// Repeat: one visual over the whole sheet. Compose: several visuals of one size, each with its own copies. Mixed:
/// several visuals, each with its own size and copies, packed on as few sheets as possible.
/// </summary>
public enum LayoutMode { Repeat, Compose, Mixed }

public enum QuantityUnit { Items, Sheets }

/// <summary>Entry of the visual size list; <see cref="Format"/> is null for "Custom".</summary>
public sealed class FormatOption(PaperFormat? format)
{
    public PaperFormat? Format { get; } = format;
    public bool IsCustom => Format is null;
    public string Name => Format is null ? Strings.Get("FormatCustom") : Strings.Get("Format_" + Format.Id);

    public override string ToString() => Name;
}

/// <summary>Entry of the sheet list; <see cref="Sheet"/> is null for "Custom".</summary>
public sealed class SheetOption(PaperFormat? sheet)
{
    public PaperFormat? Sheet { get; } = sheet;
    public bool IsCustom => Sheet is null;
    public string Name => Sheet is null ? Strings.Get("SheetCustom") : Strings.Get("Sheet_" + Sheet.Id);

    public override string ToString() => Name;
}

public enum NoticeLevel { Info, Warning, Error }

public sealed record Notice(NoticeLevel Level, string Text)
{
    /// <summary>Segoe Fluent Icons glyph, as in the Windows 11 InfoBar.</summary>
    public string Icon => Level switch { NoticeLevel.Error => "", NoticeLevel.Warning => "", _ => "" };
}

/// <summary>Pages to add of PDFs of several pages: all of them, or one (from 1).</summary>
public sealed record PdfPagesChoice(bool AllPages, int Page = 1);

/// <summary>Number of a cut on the preview of a mixed sheet (cutting order), in millimetres on the sheet.</summary>
public sealed record CutNumber(double Left, double Top, double Size, double FontSize, string Text);

/// <summary>A side shown in the preview (front, and the back next to it).</summary>
public sealed class PreviewSide(string labelKey) : ObservableObject
{
    public string Label => Strings.Get(labelKey);

    /// <summary>The front: the side facing up on the guillotine, where the cut numbers are shown.</summary>
    public bool IsFront { get; init; }

    private ImageSource? _image;
    public ImageSource? Image { get => _image; set => Set(ref _image, value); }

    private bool _showLabel;
    public bool ShowLabel { get => _showLabel; set => Set(ref _showLabel, value); }

    /// <summary>The copies of a visual being dragged on this side, drawn over the preview until it is rendered again.</summary>
    public ObservableCollection<CropOverlay> Overlays { get; } = [];
}

/// <summary>
/// A copy of a visual dragged on the preview (to choose the part kept), drawn by the window over the rendered sheet:
/// the part kept in its cell, the rest of it faded around. In millimetres on the sheet.
/// </summary>
public sealed class CropOverlay(RectMm clip) : ObservableObject
{
    public Geometry Clip { get; } = Frozen(new RectangleGeometry(new Rect(clip.X, clip.Y, clip.Width, clip.Height)));

    /// <summary>The cell, outlined while it is dragged.</summary>
    public Rect Cell { get; } = new(clip.X, clip.Y, clip.Width, clip.Height);

    private ImageSource? _image;
    public ImageSource? Image { get => _image; set => Set(ref _image, value); }

    private double _width, _height;
    public double Width { get => _width; private set => Set(ref _width, value); }
    public double Height { get => _height; private set => Set(ref _height, value); }

    private Transform _transform = Transform.Identity;
    public Transform Transform { get => _transform; private set => Set(ref _transform, value); }

    /// <summary>Draws it as Placement places it: its rectangle mirrored, turned, then moved to the centre of its area.</summary>
    public void Show(VisualPlacement p)
    {
        Width = p.DrawRect.Width;
        Height = p.DrawRect.Height;
        var m = Matrix.Identity;
        m.Translate(p.DrawRect.X, p.DrawRect.Y);
        if (p.Mirror) m.Scale(-1, 1);
        m.Rotate(p.AngleDeg);
        m.Translate(p.CenterX, p.CenterY);
        Transform = Frozen(new MatrixTransform(m));
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}

public sealed class MainViewModel : ObservableObject
{
    /// <summary>Smallest visual size, and smallest image left inside the white margins.</summary>
    private const double MinSizeMm = 5;
    private const double MinSheetMm = 20;
    private const double MaxSheetMm = 3000;
    private const int MinDpi = 200;
    private const int CriticalDpi = 150;
    private const uint PreviewWidthPx = 1400;
    private const uint MaxPreviewWidthPx = 3600; // about 285 dpi on SRA3: sharp at high zoom, about 50 MB per side
    private const int ThumbnailPx = 160;

    private readonly AppSettings _settings;
    private readonly DispatcherTimer _previewDebounce;
    private int _previewVersion;

    public MainViewModel(AppSettings settings)
    {
        _settings = settings;
        Formats = [.. ItemFormats.All.Select(f => new FormatOption(f)), new FormatOption(null)];
        MixedFormats = Formats;
        Sheets = [.. SheetFormats.All.Select(s => new SheetOption(s)), new SheetOption(null)];

        _mode = settings.Mode;
        _selectedFormat = Formats.FirstOrDefault(f => (f.Format?.Id ?? AppSettings.CustomFormatId) == settings.FormatId) ?? Formats[0];
        _customWidth = settings.CustomWidth;
        _customHeight = settings.CustomHeight;
        _selectedSheet = Sheets.FirstOrDefault(s => (s.Sheet?.Id ?? AppSettings.CustomFormatId) == settings.SheetId) ?? Sheets[0];
        (_customSheetWidth, _customSheetHeight, _customSheetMargin) =
            (settings.CustomSheetWidth, settings.CustomSheetHeight, settings.CustomSheetMargin);
        _cropMarks = settings.CropMarks;
        _cutLines = settings.CutLines;
        _mixedCutLines = settings.MixedCutLines;
        _bleedEnabled = settings.BleedEnabled;
        _bleedMm = settings.BleedMm;
        _spacingEnabled = settings.SpacingEnabled;
        _spacingMm = settings.SpacingMm;
        _marginsEnabled = settings.MarginsEnabled;
        _versoMargins = settings.VersoMargins;
        (_marginTop, _marginRight, _marginBottom, _marginLeft) =
            (settings.MarginTop, settings.MarginRight, settings.MarginBottom, settings.MarginLeft);
        // Saved on a framed size with its own margins: leaving that size gives no margins.
        if (_selectedFormat.Format is { Margins: { IsEmpty: false } preset } && _marginsEnabled && TypedMargins() == preset)
            _userMargins = (false, MarginsMm.None);
        _fit = settings.Fit;
        _completeLastSheet = settings.CompleteLastSheet;
        _unit = settings.Unit;
        _flattenPdf = settings.FlattenPdf;
        _cuttingPlan = settings.CuttingPlan;

        ClearVisualsCommand = new RelayCommand(ClearVisuals);
        PreviousSheetCommand = new RelayCommand(() => PreviewSheetIndex--);
        NextSheetCommand = new RelayCommand(() => PreviewSheetIndex++);
        Visuals.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasVisuals));
            OnPropertyChanged(nameof(VisualsDropHint));
        };

        _previewDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _previewDebounce.Tick += async (_, _) =>
        {
            _previewDebounce.Stop();
            await RefreshPreviewAsync();
        };

        Recompute();
    }

    // ---- Lists ----

    public IReadOnlyList<FormatOption> Formats { get; }
    public IReadOnlyList<SheetOption> Sheets { get; }

    /// <summary>
    /// Sizes offered on each line in the Mixed mode: the preset sizes, and "Custom", whose width and height are typed on
    /// the line (<see cref="VisualItem.CustomWidth"/>).
    /// </summary>
    public IReadOnlyList<FormatOption> MixedFormats { get; }

    /// <summary>The lines laid in the Mixed mode, by index as packed (a line with a custom size too small is not).</summary>
    private List<VisualItem> _mixedLines = [];

    /// <summary>The line of each VisualInfo laid by the grids (Repeat, Compose): each line gives its own object.</summary>
    private readonly Dictionary<VisualInfo, VisualItem> _lineOfInfo = new(ReferenceEqualityComparer.Instance);

    // ---- Presets: the settings of a usual job (no files, no quantity) ----

    public ObservableCollection<LayoutPreset> Presets { get; } = [.. PresetStore.Load()];

    private LayoutPreset? _selectedPreset;
    public LayoutPreset? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (!Set(ref _selectedPreset, value)) return;
            OnPropertyChanged(nameof(HasPreset));
            if (value is not null) ApplyPreset(value);
        }
    }

    public bool HasPreset => SelectedPreset is not null;

    /// <summary>Saves the current settings under <paramref name="name"/> (replacing a preset of that name) and selects it.</summary>
    public void SavePreset(string name)
    {
        var preset = CapturePreset(name.Trim());
        ShowPresets(PresetStore.Save(preset), preset.Name);
    }

    public void DeleteSelectedPreset()
    {
        if (SelectedPreset is { } preset)
            ShowPresets(PresetStore.Delete(preset.Name), null);
    }

    /// <summary>Reads the presets again (another window may have changed them), keeping the selection.</summary>
    public void ReloadPresets() => ShowPresets(PresetStore.Load(), SelectedPreset?.Name);

    /// <summary>
    /// Imports a preset file (double click on a .PageCompose file, or dropped on the window). A file of one preset
    /// applies it at once; the notification tells what came in.
    /// </summary>
    /// <exception cref="InvalidDataException">The file holds no PageCompose presets.</exception>
    public void ImportPresets(string path)
    {
        var (imported, _, all) = PresetStore.ImportPresets(path);
        if (imported is [var single])
        {
            ShowPresets(all, null); // selecting it then applies it, even when it replaced the selected one
            SelectedPreset = Presets.FirstOrDefault(p => PresetStore.SameName(p.Name, single.Name));
            Toast.ShowMessage(Strings.Count("PresetsImported", 1), Strings.Format("PresetApplied", single.Name));
        }
        else
        {
            // The selected preset may have been replaced by one with other settings.
            ShowPresets(all, SelectedPreset?.Name);
            ForgetPresetIfChanged();
            Toast.ShowMessage(Strings.Count("PresetsImported", imported.Count), Strings.Format("PresetsImportedFrom", Path.GetFileName(path)));
        }
    }

    // Refills the list without applying the selected preset again (the settings are already its own).
    private void ShowPresets(List<LayoutPreset> presets, string? selectedName)
    {
        _selectedPreset = null;
        Presets.Clear();
        foreach (var p in presets) Presets.Add(p);
        _selectedPreset = selectedName is null ? null : Presets.FirstOrDefault(p => PresetStore.SameName(p.Name, selectedName));
        OnPropertyChanged(nameof(SelectedPreset));
        OnPropertyChanged(nameof(HasPreset));
    }

    private LayoutPreset CapturePreset(string name) => new()
    {
        Name = name,
        Mode = _mode,
        FormatId = SelectedFormat.Format?.Id ?? AppSettings.CustomFormatId,
        CustomWidth = CustomWidth,
        CustomHeight = CustomHeight,
        MarginsEnabled = MarginsEnabled,
        MarginTop = MarginTop,
        MarginRight = MarginRight,
        MarginBottom = MarginBottom,
        MarginLeft = MarginLeft,
        VersoMargins = VersoMargins,
        SheetId = SelectedSheet.Sheet?.Id ?? AppSettings.CustomFormatId,
        CustomSheetWidth = CustomSheetWidth,
        CustomSheetHeight = CustomSheetHeight,
        CustomSheetMargin = CustomSheetMargin,
        CropMarks = CropMarks,
        CutLines = CutLines,
        MixedCutLines = MixedCutLines,
        BleedEnabled = BleedEnabled,
        BleedMm = BleedMm,
        SpacingEnabled = SpacingEnabled,
        SpacingMm = SpacingMm,
        Fit = _fit,
        CompleteLastSheet = CompleteLastSheet,
        Unit = _unit,
        VersoEnabled = VersoEnabled,
        Flip = _flip,
        RotateVerso180 = RotateVerso180,
        FlattenPdf = FlattenPdf,
        CuttingPlan = CuttingPlan,
    };

    private bool _applyingPreset;

    // The list shows a preset only while the settings are its own: changed by hand, they no longer are, and picking
    // the preset again applies it again.
    private void ForgetPresetIfChanged()
    {
        if (_applyingPreset || _selectedPreset is not { } preset) return;
        var options = new System.Text.Json.JsonSerializerOptions();
        if (System.Text.Json.JsonSerializer.Serialize(CapturePreset(preset.Name), options) ==
            System.Text.Json.JsonSerializer.Serialize(preset, options)) return;
        _selectedPreset = null;
        OnPropertyChanged(nameof(SelectedPreset));
        OnPropertyChanged(nameof(HasPreset));
    }

    // The visuals, the back and the quantity stay: only the settings change (the unit of the quantity included).
    private void ApplyPreset(LayoutPreset p)
    {
        _applyingPreset = true;
        try { ApplyPresetValues(p); }
        finally { _applyingPreset = false; }
        Changed();
    }

    private void ApplyPresetValues(LayoutPreset p)
    {
        SetMode(p.Mode);
        SelectedFormat = Formats.FirstOrDefault(f => (f.Format?.Id ?? AppSettings.CustomFormatId) == p.FormatId) ?? SelectedFormat;
        CustomWidth = p.CustomWidth;
        CustomHeight = p.CustomHeight;
        // The margins of the preset, as the user's own (choosing the size above may have set those of a frame).
        SetMargins(p.MarginsEnabled, new MarginsMm(p.MarginTop, p.MarginRight, p.MarginBottom, p.MarginLeft));
        _userMargins = null;
        VersoMargins = p.VersoMargins;
        SelectedSheet = Sheets.FirstOrDefault(s => (s.Sheet?.Id ?? AppSettings.CustomFormatId) == p.SheetId) ?? SelectedSheet;
        CustomSheetWidth = p.CustomSheetWidth;
        CustomSheetHeight = p.CustomSheetHeight;
        CustomSheetMargin = p.CustomSheetMargin;
        CropMarks = p.CropMarks;
        CutLines = p.CutLines;
        MixedCutLines = p.MixedCutLines;
        BleedEnabled = p.BleedEnabled;
        BleedMm = p.BleedMm;
        SpacingEnabled = p.SpacingEnabled;
        SpacingMm = p.SpacingMm;
        SetFit(p.Fit);
        CompleteLastSheet = p.CompleteLastSheet;
        SetUnit(p.Unit);
        VersoEnabled = p.VersoEnabled;
        SetFlip(p.Flip);
        RotateVerso180 = p.RotateVerso180;
        FlattenPdf = p.FlattenPdf;
        CuttingPlan = p.CuttingPlan;
    }

    // ---- Mode ----

    private LayoutMode _mode;
    public bool IsRepeatMode { get => _mode == LayoutMode.Repeat; set { if (value) SetMode(LayoutMode.Repeat); } }
    public bool IsComposeMode { get => _mode == LayoutMode.Compose; set { if (value) SetMode(LayoutMode.Compose); } }
    public bool IsMixedMode { get => _mode == LayoutMode.Mixed; set { if (value) SetMode(LayoutMode.Mixed); } }

    /// <summary>Several visuals in a list (Compose and Mixed), rather than a single one.</summary>
    public bool HasVisualList => _mode != LayoutMode.Repeat;

    // ---- Repeat: one visual ----

    private VisualItem? _repeatVisual;
    public VisualItem? RepeatVisual
    {
        get => _repeatVisual;
        private set
        {
            if (!Set(ref _repeatVisual, value)) return;
            OnPropertyChanged(nameof(HasRepeatVisual));
            Changed();
        }
    }

    public bool HasRepeatVisual => RepeatVisual is not null;

    private int _quantity = 1;
    public int Quantity { get => _quantity; set { if (Set(ref _quantity, Math.Clamp(value, 0, 100_000))) Changed(); } }

    private QuantityUnit _unit = QuantityUnit.Sheets;
    public bool UnitItems { get => _unit == QuantityUnit.Items; set { if (value) SetUnit(QuantityUnit.Items); } }
    public bool UnitSheets { get => _unit == QuantityUnit.Sheets; set { if (value) SetUnit(QuantityUnit.Sheets); } }

    // ---- Compose: several visuals ----

    public ObservableCollection<VisualItem> Visuals { get; } = [];
    public bool HasVisuals => Visuals.Count > 0;
    public string VisualsDropHint => Strings.Get(HasVisuals ? "DropVisualsAdd" : "DropVisualsEmpty");

    private bool _completeLastSheet;
    public bool CompleteLastSheet { get => _completeLastSheet; set { if (Set(ref _completeLastSheet, value)) Changed(); } }

    /// <summary>
    /// A quantity counted in sheets always fills them: the option only matters for a number of visuals. Mixed sheets
    /// are packed with the copies asked, nothing more.
    /// </summary>
    public bool CanCompleteLastSheet => _mode == LayoutMode.Compose || (_mode == LayoutMode.Repeat && _unit == QuantityUnit.Items);

    public ICommand ClearVisualsCommand { get; }

    /// <summary>
    /// Asks which pages to add of the PDFs of several pages added to the list (file names and page counts); null when
    /// cancelled: they are not added. Set by the window; none: their first page.
    /// </summary>
    public Func<IReadOnlyList<(string FileName, int Pages)>, PdfPagesChoice?>? AskPdfPages { get; set; }

    private string? _filesMessage;
    public string? FilesMessage { get => _filesMessage; private set => Set(ref _filesMessage, value); }

    // ---- Back (shared by every cell) ----

    private bool _versoEnabled;
    public bool VersoEnabled
    {
        get => _versoEnabled;
        set
        {
            if (!Set(ref _versoEnabled, value)) return;
            VersoSide.ShowLabel = RectoSide.ShowLabel = value;
            UpdatePreviewSides();
            Changed();
        }
    }

    private VisualItem? _verso;
    public VisualItem? Verso
    {
        get => _verso;
        private set
        {
            if (!Set(ref _verso, value)) return;
            OnPropertyChanged(nameof(HasVerso));
            Changed();
        }
    }

    public bool HasVerso => Verso is not null;

    private string? _versoError;
    public string? VersoError { get => _versoError; private set => Set(ref _versoError, value); }

    private DuplexFlip _flip = DuplexFlip.LongEdge;
    public bool FlipLongEdge { get => _flip == DuplexFlip.LongEdge; set { if (value) SetFlip(DuplexFlip.LongEdge); } }
    public bool FlipShortEdge { get => _flip == DuplexFlip.ShortEdge; set { if (value) SetFlip(DuplexFlip.ShortEdge); } }

    private bool _rotateVerso180;
    public bool RotateVerso180 { get => _rotateVerso180; set { if (Set(ref _rotateVerso180, value)) Changed(); } }

    // ---- Size, sheet, cutting ----

    private FormatOption _selectedFormat;
    public FormatOption SelectedFormat
    {
        get => _selectedFormat;
        set
        {
            if (!Set(ref _selectedFormat, value)) return;
            OnPropertyChanged(nameof(IsCustomFormat));
            ApplyFormatMargins(value.Format?.Margins ?? MarginsMm.None);
            Changed();
        }
    }

    public bool IsCustomFormat => SelectedFormat.IsCustom;

    // White margins inside each visual (front only). Framed sizes (Polaroid, Instax…) set them; the user's own
    // margins are kept aside meanwhile and come back when leaving such a size, unless they were edited.
    private bool _marginsEnabled;
    private double _marginTop, _marginRight, _marginBottom, _marginLeft;
    private (bool Enabled, MarginsMm Margins)? _userMargins;

    public bool MarginsEnabled { get => _marginsEnabled; set { if (Set(ref _marginsEnabled, value)) MarginsEdited(); } }
    public double MarginTop { get => _marginTop; set { if (Set(ref _marginTop, value)) MarginsEdited(); } }
    public double MarginRight { get => _marginRight; set { if (Set(ref _marginRight, value)) MarginsEdited(); } }
    public double MarginBottom { get => _marginBottom; set { if (Set(ref _marginBottom, value)) MarginsEdited(); } }
    public double MarginLeft { get => _marginLeft; set { if (Set(ref _marginLeft, value)) MarginsEdited(); } }

    private bool _versoMargins;
    public bool VersoMargins { get => _versoMargins; set { if (Set(ref _versoMargins, value)) Changed(); } }

    /// <summary>
    /// The back keeps the white margins: asked, and there are margins and a back. Not in the Mixed mode, where the
    /// option is hidden with the size section: the back covers each cell.
    /// </summary>
    private bool BackHasMargins => VersoMargins && MarginsEnabled && VersoEnabled && _mode != LayoutMode.Mixed;

    private double _customWidth;
    public double CustomWidth { get => _customWidth; set { if (Set(ref _customWidth, value)) Changed(); } }

    private double _customHeight;
    public double CustomHeight { get => _customHeight; set { if (Set(ref _customHeight, value)) Changed(); } }

    private SheetOption _selectedSheet;
    public SheetOption SelectedSheet
    {
        get => _selectedSheet;
        set
        {
            if (!Set(ref _selectedSheet, value)) return;
            OnPropertyChanged(nameof(IsCustomSheet));
            Changed();
        }
    }

    public bool IsCustomSheet => SelectedSheet.IsCustom;

    // Custom sheet: any paper, laid portrait, with the unprintable margin of the printer used (0 for a lab print).
    private double _customSheetWidth;
    public double CustomSheetWidth { get => _customSheetWidth; set { if (Set(ref _customSheetWidth, value)) Changed(); } }

    private double _customSheetHeight;
    public double CustomSheetHeight { get => _customSheetHeight; set { if (Set(ref _customSheetHeight, value)) Changed(); } }

    private double _customSheetMargin;
    public double CustomSheetMargin { get => _customSheetMargin; set { if (Set(ref _customSheetMargin, value)) Changed(); } }

    private bool _cropMarks;
    public bool CropMarks { get => _cropMarks; set { if (Set(ref _cropMarks, value)) Changed(); } }

    private bool _cutLines;
    public bool CutLines { get => _cutLines; set { if (Set(ref _cutLines, value)) Changed(); } }

    private bool _mixedCutLines;

    /// <summary>
    /// The cut lines of the Mixed mode, apart from those of the grids (<see cref="CutLines"/>, often off): there they
    /// go along each cut, and are on by default.
    /// </summary>
    public bool MixedCutLines { get => _mixedCutLines; set { if (Set(ref _mixedCutLines, value)) Changed(); } }

    private bool _bleedEnabled;
    public bool BleedEnabled { get => _bleedEnabled; set { if (Set(ref _bleedEnabled, value)) Changed(); } }

    private double _bleedMm;
    public double BleedMm { get => _bleedMm; set { if (Set(ref _bleedMm, value)) Changed(); } }

    private bool _spacingEnabled;
    public bool SpacingEnabled { get => _spacingEnabled; set { if (Set(ref _spacingEnabled, value)) Changed(); } }

    private double _spacingMm;
    public double SpacingMm { get => _spacingMm; set { if (Set(ref _spacingMm, value)) Changed(); } }

    private FitMode _fit;
    public bool IsFill { get => _fit == FitMode.Fill; set { if (value) SetFit(FitMode.Fill); } }
    public bool IsFit { get => _fit == FitMode.Fit; set { if (value) SetFit(FitMode.Fit); } }

    // ---- Result ----

    /// <summary>The layout of the sheets; in the Mixed mode, that of the sheet shown (each has its own).</summary>
    public SheetLayout? Layout { get; private set; }
    public SheetPlan Plan { get; private set; } = SheetPlan.Empty;

    /// <summary>The sheets of the Mixed mode (empty in the other modes).</summary>
    private MixedPlan _mixed = MixedPlan.Empty;

    /// <summary>
    /// The Mixed mode's last packings, by what they were computed from: settings that change nothing keep them, and
    /// the packing without crop marks (for its notice) is not done again at each change.
    /// </summary>
    private readonly Dictionary<string, MixedPlan> _packings = [];

    /// <summary>Cutting order on the sheet shown in the Mixed mode, over the front of the preview (never printed).</summary>
    public IReadOnlyList<CutNumber> CutNumbers { get; private set; } = [];

    public string Headline { get; private set; } = "";
    public string Details { get; private set; } = "";
    public string PlanText { get; private set; } = "";

    public ObservableCollection<Notice> Notices { get; } = [];

    /// <summary>Sheet of the layout. A custom sheet out of range falls back to SRA3 for the drawing (an error is shown).</summary>
    private PaperFormat Sheet => SelectedSheet.Sheet ?? CustomSheet() ?? SheetFormats.Sra3;

    /// <summary>Sheet name in messages: the list entry, or the size of a custom sheet.</summary>
    private string SheetLabel => SelectedSheet.IsCustom ? $"{Sheet.Width:0.#} × {Sheet.Height:0.#} mm" : SelectedSheet.Name;

    /// <summary>Under the preview: the red band is only there when the sheet has unprintable edges.</summary>
    public string PreviewHint { get; private set; } = "";
    public double SheetWidth => Sheet.Width;
    public double SheetHeight => Sheet.Height;

    /// <summary>The 4.1 mm unprintable band, drawn over the preview.</summary>
    public Geometry MarginGeometry { get; private set; } = Geometry.Empty;

    /// <summary>Cell outlines, shown until the real preview is ready.</summary>
    public IReadOnlyList<RectMm> PlaceholderCells { get; private set; } = [];

    public bool CanExport => Layout is { IsEmpty: false } && Plan.SheetCount > 0;

    // ---- Preview ----

    public PreviewSide RectoSide { get; } = new("SideFront") { IsFront = true };
    public PreviewSide VersoSide { get; } = new("SideBack");
    public ObservableCollection<PreviewSide> PreviewSides { get; } = [];

    private int _previewSheetIndex;
    public int PreviewSheetIndex
    {
        get => _previewSheetIndex;
        set
        {
            int clamped = Math.Clamp(value, 0, Math.Max(0, Plan.SheetCount - 1));
            if (!Set(ref _previewSheetIndex, clamped)) return;
            OnPropertyChanged(nameof(PreviewSheetLabel));
            if (_mode == LayoutMode.Mixed) ShowMixedSheet(); // each sheet has its own layout
            SchedulePreview();
        }
    }

    public string PreviewSheetLabel => Strings.Format("SheetPosition", PreviewSheetIndex + 1, Math.Max(1, Plan.SheetCount));
    public bool HasSeveralSheets => Plan.SheetCount > 1;
    public ICommand PreviousSheetCommand { get; }
    public ICommand NextSheetCommand { get; }

    private bool _isPreviewBusy;
    public bool IsPreviewBusy { get => _isPreviewBusy; private set => Set(ref _isPreviewBusy, value); }

    // ---- The part kept, dragged on the preview ----
    // A photo dragged in its cell keeps another part of it (Placement.Dragged); every copy of its line follows. While
    // the mouse moves, the window draws the copies over the preview (CropOverlay); the line takes the new part when
    // the mouse is released, and the preview is rendered again.

    private sealed class CropDrag(VisualItem line, VisualInfo start, LayoutCell cell, SideOptions side, PreviewSide view,
        List<LayoutCell> cells, double x, double y)
    {
        public VisualItem Line { get; } = line;
        public VisualInfo Start { get; } = start;
        public LayoutCell Cell { get; } = cell;
        public SideOptions Side { get; } = side;
        public PreviewSide View { get; } = view;
        public List<LayoutCell> Cells { get; } = cells;
        public double X { get; } = x;
        public double Y { get; } = y;
        public (double X, double Y) Crop { get; set; } = (start.CropX, start.CropY);
    }

    private CropDrag? _drag;

    /// <summary>The line of the visual under a point of the sheet shown (millimetres), on the front or the back.</summary>
    public VisualItem? LineAt(bool front, double x, double y) => FindCell(front, x, y)?.Line;

    /// <summary>Starts dragging the visual under the point; false when there is none.</summary>
    public bool BeginCropDrag(bool front, double x, double y)
    {
        if (FindCell(front, x, y) is not { } hit) return false;
        var view = front ? RectoSide : VersoSide;
        var start = hit.Line.Info;
        var cells = CellsOf(hit.Line, front);
        _drag = new CropDrag(hit.Line, start, hit.Cell, hit.Side, view, cells, x, y);

        view.Overlays.Clear();
        foreach (var cell in cells)
        {
            var p = Placement.Compute(start, cell, _fit, hit.Side);
            var overlay = new CropOverlay(p.Clip) { Image = hit.Line.DragImage };
            overlay.Show(p);
            view.Overlays.Add(overlay);
        }
        if (hit.Line.DragImage is null)
            _ = LoadDragImageAsync(hit.Line, view);
        return true;
    }

    public void CropDragTo(double x, double y)
    {
        if (_drag is not { } d) return;
        d.Crop = Placement.Dragged(d.Start, d.Cell, _fit, d.Side, x - d.X, y - d.Y);
        var moved = d.Start with { CropX = d.Crop.X, CropY = d.Crop.Y };
        for (int i = 0; i < d.Cells.Count && i < d.View.Overlays.Count; i++)
            d.View.Overlays[i].Show(Placement.Compute(moved, d.Cells[i], _fit, d.Side));
    }

    /// <summary>The line takes the part kept; the copies drawn over the preview stay until it is rendered again.</summary>
    public void EndCropDrag()
    {
        if (_drag is not { } d) return;
        _drag = null;
        if (Math.Abs(d.Crop.X - d.Start.CropX) < 1e-9 && Math.Abs(d.Crop.Y - d.Start.CropY) < 1e-9)
            d.View.Overlays.Clear(); // a click, not a drag
        else
            d.Line.SetCrop(d.Crop.X, d.Crop.Y);
    }

    // The cell under a point, in the frame of its side (the back mirrored), with the line of its visual.
    private (VisualItem Line, LayoutCell Cell, SideOptions Side)? FindCell(bool front, double x, double y)
    {
        if (Layout is not { IsEmpty: false } layout || PreviewSheetIndex >= Plan.SheetCount) return null;
        var visuals = Plan.Sheets[PreviewSheetIndex];
        var side = front ? SideOptions.Recto : new SideOptions(true, _flip, RotateVerso180, BackHasMargins);
        for (int i = 0; i < layout.Cells.Count && i < visuals.Length; i++)
        {
            if (visuals[i] is null) continue;
            var cell = front ? layout.Cells[i] : Placement.MirrorForVerso(layout.Cells[i], layout.Sheet, _flip);
            var b = cell.Bleed;
            if (x < b.X || x > b.Right || y < b.Y || y > b.Bottom) continue;
            var line = front ? LineOfCell(PreviewSheetIndex, i) : VersoEnabled ? Verso : null;
            return line is null ? null : (line, cell, side);
        }
        return null;
    }

    // Every cell of the sheet shown holding this line (on the back: the back is behind every front).
    private List<LayoutCell> CellsOf(VisualItem line, bool front)
    {
        var cells = new List<LayoutCell>();
        if (Layout is not { } layout || PreviewSheetIndex >= Plan.SheetCount) return cells;
        var visuals = Plan.Sheets[PreviewSheetIndex];
        for (int i = 0; i < layout.Cells.Count && i < visuals.Length; i++)
        {
            if (visuals[i] is null) continue;
            if (front && LineOfCell(PreviewSheetIndex, i) == line) cells.Add(layout.Cells[i]);
            else if (!front) cells.Add(Placement.MirrorForVerso(layout.Cells[i], layout.Sheet, _flip));
        }
        return cells;
    }

    private VisualItem? LineOfCell(int sheet, int cell)
    {
        if (_mode == LayoutMode.Mixed)
            return sheet < _mixed.SheetCount && _mixed.Sheets[sheet].Items[cell] is var item && item < _mixedLines.Count ? _mixedLines[item] : null;
        return sheet < Plan.SheetCount && Plan.Sheets[sheet][cell] is { } info && _lineOfInfo.TryGetValue(info, out var line) ? line : null;
    }

    private static async Task LoadDragImageAsync(VisualItem line, PreviewSide view)
    {
        line.DragImage = await ThumbnailLoader.LoadRawAsync(line.SourceInfo, 1400);
        foreach (var overlay in view.Overlays)
            overlay.Image ??= line.DragImage;
    }

    // Once the preview shows the part kept, the copies drawn over it go.
    private void ClearOverlays()
    {
        if (_drag is not null) return;
        RectoSide.Overlays.Clear();
        VersoSide.Overlays.Clear();
    }

    // ---- Export ----

    private bool _flattenPdf;
    public bool FlattenPdf { get => _flattenPdf; set { if (Set(ref _flattenPdf, value)) ForgetPresetIfChanged(); } }

    private bool _cuttingPlan;

    /// <summary>Mixed mode: writes the cutting plan (A4 PDF, to print on office paper) next to each export.</summary>
    public bool CuttingPlan { get => _cuttingPlan; set { if (Set(ref _cuttingPlan, value)) ForgetPresetIfChanged(); } }

    /// <summary>Progress and result of the exports, shown over the preview.</summary>
    public ExportToast Toast { get; } = new();

    public string? LastExportFolder => _settings.LastExportFolder;

    /// <summary>Folder of the first visual, offered by default for the export.</summary>
    public string? FirstVisualFolder => Path.GetDirectoryName(ActiveVisuals().FirstOrDefault()?.Info.OriginalPath);

    // ---- Files ----

    /// <summary>
    /// Adds files to the layout. In Repeat mode one file replaces the visual; several files at once switch to Compose.
    /// </summary>
    public async Task AddFilesAsync(IReadOnlyList<string> paths)
    {
        var loaded = new List<VisualInfo>();
        var errors = new List<string>();
        var notes = new List<string>();
        foreach (var path in paths)
        {
            var (info, note, error) = await LoadAsync(path);
            if (info is not null) loaded.Add(info);
            if (note is not null) notes.Add(note);
            if (error is not null) errors.Add(error);
        }

        var messages = new List<string>(notes);
        if (_mode == LayoutMode.Repeat && loaded.Count > 1)
        {
            SetMode(LayoutMode.Compose, carryOver: false);
            messages.Add(Strings.Format("FilesSwitchedToCompose", loaded.Count));
        }

        if (_mode == LayoutMode.Repeat)
        {
            if (loaded.Count == 1)
            {
                RepeatVisual = CreateItem(loaded[0], _ => RepeatVisual = null);

                // A PDF of two pages is a front and its back (business cards): page 2 becomes the back.
                if (loaded[0].PageCount == 2)
                {
                    var (back, _, _) = await LoadAsync(loaded[0].OriginalPath, 2);
                    if (back is not null)
                    {
                        Verso = CreateItem(back, _ => Verso = null);
                        VersoError = null;
                        VersoEnabled = true;
                        messages.Add(Strings.Format("PdfPage2AsBack", loaded[0].FileName));
                    }
                }
            }
        }
        else
        {
            // The same page of the same file only once (another page of it is another visual).
            bool InList(VisualInfo v) => Visuals.Any(i =>
                string.Equals(i.Info.OriginalPath, v.OriginalPath, StringComparison.OrdinalIgnoreCase) && i.Info.PageNumber == v.PageNumber);

            // PDFs of several pages: all their pages (one line each) or one page, asked once for all of them; cancelled,
            // they are left out (the other files are added).
            var multiPage = loaded.Where(v => v.PageCount > 1 && !InList(v)).ToList();
            var choice = multiPage.Count > 0 && AskPdfPages is { } ask
                ? ask([.. multiPage.Select(v => (v.FileName, v.PageCount))])
                : new PdfPagesChoice(false);

            int duplicates = 0;
            foreach (var v in loaded)
            {
                if (InList(v))
                {
                    duplicates++;
                    continue;
                }
                if (v.PageCount < 2)
                {
                    Visuals.Add(CreateItem(v));
                    continue;
                }
                if (choice is null) continue;

                IEnumerable<int> pages = choice.AllPages ? Enumerable.Range(1, v.PageCount) : [Math.Clamp(choice.Page, 1, v.PageCount)];
                foreach (int page in pages)
                {
                    var (info, _, error) = page == v.PageNumber ? (v, null, null) : await LoadAsync(v.OriginalPath, page);
                    if (info is not null && !InList(info)) Visuals.Add(CreateItem(info));
                    if (error is not null) errors.Add(error);
                }
            }

            if (duplicates > 0) messages.Add(Strings.Format("FilesDuplicates", duplicates));
            Changed();
        }

        messages.AddRange(errors.Take(3));
        if (errors.Count > 3) messages.Add(Strings.Format("FilesMoreErrors", errors.Count - 3));
        FilesMessage = messages.Count > 0 ? string.Join(" ", messages) : null;
    }

    public async Task SetVersoAsync(string path)
    {
        var (info, note, error) = await LoadAsync(path);
        VersoError = error;
        if (info is null) return;

        Verso = CreateItem(info, _ => Verso = null);
        VersoEnabled = true;
        if (note is not null) FilesMessage = note;
    }

    /// <summary>
    /// Reads a visual (a page of a PDF, from 1). A PDF that PDFsharp cannot read becomes an image rendered by Windows,
    /// with a note saying so.
    /// </summary>
    private static async Task<(VisualInfo? Info, string? Note, string? Error)> LoadAsync(string path, int pageNumber = 1)
    {
        try
        {
            return (VisualLoader.Load(path, pageNumber), null, null);
        }
        catch (VisualLoadException ex) when (ex.Error == VisualLoadError.Unreadable &&
                                             string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var (png, pageCount) = await PdfRasterizer.RenderPageAsync(path, pageNumber);
                var image = VisualLoader.Load(png) with { SourcePath = path, PageNumber = pageNumber, PageCount = pageCount };
                return (image, Strings.Format("PdfConverted", ex.FileName, PdfRasterizer.Dpi), null);
            }
            catch (Exception inner) when (inner is not OutOfMemoryException)
            {
                // Windows cannot read it either: damaged, or protected by a password.
                return (null, null, Strings.Format("LoadPdfDamaged", ex.FileName));
            }
        }
        catch (VisualLoadException ex)
        {
            return (null, null, LoadErrorMessage(ex));
        }
    }

    public static string LoadErrorMessage(VisualLoadException ex) => ex.Error switch
    {
        VisualLoadError.NotFound => Strings.Format("LoadNotFound", ex.FilePath),
        VisualLoadError.Unsupported => Strings.Format("LoadUnsupported", ex.Detail ?? ""),
        VisualLoadError.NoPages => Strings.Format("LoadNoPages", ex.FileName),
        _ => Strings.Format("LoadUnreadable", ex.FileName, ex.Detail ?? ""),
    };

    /// <summary>File name offered for the export, with the .pdf extension (the JPEG export changes it).</summary>
    public string SuggestedFileName()
    {
        var item = CurrentItemSize() ?? default;
        string size = $"{item.Width:0}x{item.Height:0}"; // whole millimetres: 10 x 15 reads 102x152, not 101,6x152,4
        string name = _mode switch
        {
            LayoutMode.Repeat => Strings.Format("FileNameRepeat", Path.GetFileNameWithoutExtension(RepeatVisual?.Info.OriginalPath ?? "layout"),
                size, Sheet.Id, Layout?.PerSheet ?? 0, Plan.SheetCount),
            LayoutMode.Compose => Strings.Format("FileNameCompose", Visuals.Count, size, Sheet.Id, Plan.SheetCount),
            _ => Strings.Format("FileNameMixed", Visuals.Count, Sheet.Id, Plan.SheetCount),
        };
        return name + ".pdf";
    }

    public async Task ExportAsync(string path)
    {
        if (!CanExport) return;
        var job = BuildJob(AllSheets());
        Toast.ShowProgress(Strings.Get("Exporting"), Path.GetFileName(path));
        if (FlattenPdf)
        {
            // Each side rendered as a 300 dpi picture, then gathered into the PDF: nothing left for the RIP to interpret.
            string folder = Path.Combine(Path.GetTempPath(), "PageCompose", $"Flat-{Guid.NewGuid():N}");
            Directory.CreateDirectory(folder);
            try
            {
                var pages = Enumerable.Range(1, PageCount(job)).Select(i => Path.Combine(folder, $"{i}.jpg")).ToList();
                await RenderPagesAsync(job, pages);
                await Task.Run(() =>
                {
                    using var fs = File.Create(path);
                    FlatPdfWriter.Write(pages, Sheet.Size, fs, job.Title);
                });
            }
            finally
            {
                try { Directory.Delete(folder, recursive: true); }
                catch (IOException) { }
            }
        }
        else
        {
            await Task.Run(() => ImpositionPdfWriter.Write(job, path));
        }

        var cuttingPlan = await WriteCuttingPlanAsync(path);
        Toast.ShowDone(Strings.Get("ToastPdfSaved"),
            Strings.Format("ToastPdfDetail", Path.GetFileName(path), Strings.Count("Sheets", Plan.SheetCount)) +
            (cuttingPlan is null ? "" : Strings.Get("ToastWithPlan")),
            cuttingPlan is null ? [path] : [path, cuttingPlan]);
        _settings.LastExportFolder = Path.GetDirectoryName(path);
        SaveSettings();
    }

    /// <summary>
    /// In the Mixed mode, when asked: the cutting plan next to the export ("name-cutting-plan.pdf"), to print on
    /// office paper and keep by the guillotine. Returns its path, or null when there is none.
    /// </summary>
    private async Task<string?> WriteCuttingPlanAsync(string exportPath)
    {
        if (_mode != LayoutMode.Mixed || !CuttingPlan || _mixed.SheetCount == 0) return null;
        string stem = Path.GetFileNameWithoutExtension(exportPath);
        string path = Path.Combine(Path.GetDirectoryName(exportPath) ?? "", Strings.Format("CuttingPlanFileName", stem) + ".pdf");
        var texts = new CuttingPlanTexts(Strings.Get("CuttingPlanTitle"), Strings.Get("CuttingPlanSheet"), Strings.Get("CuttingPlanHint"),
            Strings.Get("CutFromTop"), Strings.Get("CutFromBottom"), Strings.Get("CutFromLeft"), Strings.Get("CutFromRight"),
            Strings.Get("CuttingPlanMeasureHint"));
        // The size of each line laid, by index as packed.
        var labels = _mixedLines.Select(MixedSizeName).ToList();
        var (plan, sheet, fit, logo) = (_mixed, SheetLabel, _fit, AppIconPng());
        await Task.Run(() =>
        {
            using var fs = File.Create(path);
            CuttingPlanWriter.Write(plan, labels, sheet, stem, texts, fs, fit, logo);
        });
        return path;
    }

    // The icon of the app in high definition, for the foot of the cutting plan (never the .ico: its first frame is 16 px).
    private static byte[]? AppIconPng()
    {
        if (Application.GetResourceStream(new Uri("pack://application:,,,/Assets/PageCompose.png")) is not { } resource)
            return null;
        using var png = new MemoryStream();
        resource.Stream.CopyTo(png);
        return png.ToArray();
    }

    /// <summary>
    /// One 300 dpi JPEG per side of each sheet. A single picture takes <paramref name="path"/> as it is; otherwise
    /// each gets the sheet number, and "back" for the back. Returns the files written, in order.
    /// </summary>
    public async Task<IReadOnlyList<string>> ExportJpegAsync(string path)
    {
        if (!CanExport) return [];
        var job = BuildJob(AllSheets());
        int count = PageCount(job);
        string folder = Path.GetDirectoryName(path) ?? "", stem = Path.GetFileNameWithoutExtension(path);
        List<string> files = count == 1
            ? [path]
            : Enumerable.Range(0, count).Select(i =>
            {
                int sheet = (job.Verso is null ? i : i / 2) + 1;
                bool back = job.Verso is not null && i % 2 == 1;
                return Path.Combine(folder, Strings.Format(back ? "JpegPageBack" : "JpegPageFront", stem, sheet) + ".jpg");
            }).ToList();

        Toast.ShowProgress(Strings.Get("Exporting"), Path.GetFileName(path));
        await RenderPagesAsync(job, files);
        var cuttingPlan = await WriteCuttingPlanAsync(path);
        Toast.ShowDone(Strings.Count("JpegSaved", files.Count),
            (files.Count == 1 ? Path.GetFileName(path) : Strings.Format("ToastInFolder", Path.GetFileName(folder))) +
            (cuttingPlan is null ? "" : Strings.Get("ToastWithPlan")),
            cuttingPlan is null ? files : [.. files, cuttingPlan]);
        _settings.LastExportFolder = folder;
        SaveSettings();
        return files;
    }

    // Front, then back when there is one, for every sheet: as in the PDF.
    private static int PageCount(ImpositionJob job) => job.Sheets.Count * (job.Verso is null ? 1 : 2);

    private async Task RenderPagesAsync(ImpositionJob job, IReadOnlyList<string> files)
    {
        var pdf = await Task.Run(() =>
        {
            using var ms = new MemoryStream();
            ImpositionPdfWriter.Write(job, ms);
            return ms.ToArray();
        });
        var progress = new Progress<int>(done => Toast.Detail = Strings.Format("ExportingPage", Math.Min(done + 1, files.Count), files.Count));
        Toast.Detail = Strings.Format("ExportingPage", 1, files.Count);
        await SheetExporter.RenderPagesAsync(pdf, Sheet.Size, files, progress);
    }

    public void SaveSettings()
    {
        _settings.Mode = _mode;
        _settings.FormatId = SelectedFormat.Format?.Id ?? AppSettings.CustomFormatId;
        _settings.CustomWidth = CustomWidth;
        _settings.CustomHeight = CustomHeight;
        _settings.SheetId = SelectedSheet.Sheet?.Id ?? AppSettings.CustomFormatId;
        (_settings.CustomSheetWidth, _settings.CustomSheetHeight, _settings.CustomSheetMargin) =
            (CustomSheetWidth, CustomSheetHeight, CustomSheetMargin);
        _settings.CropMarks = CropMarks;
        _settings.CutLines = CutLines;
        _settings.MixedCutLines = MixedCutLines;
        _settings.BleedEnabled = BleedEnabled;
        _settings.BleedMm = BleedMm;
        _settings.SpacingEnabled = SpacingEnabled;
        _settings.SpacingMm = SpacingMm;
        _settings.MarginsEnabled = MarginsEnabled;
        _settings.VersoMargins = VersoMargins;
        (_settings.MarginTop, _settings.MarginRight, _settings.MarginBottom, _settings.MarginLeft) =
            (MarginTop, MarginRight, MarginBottom, MarginLeft);
        _settings.Fit = _fit;
        _settings.CompleteLastSheet = CompleteLastSheet;
        _settings.Unit = _unit;
        _settings.FlattenPdf = FlattenPdf;
        _settings.CuttingPlan = CuttingPlan;
        _settings.Save();
    }

    /// <summary>Recomputes every text after a language change (the new window reads the static ones itself).</summary>
    public void RefreshTexts()
    {
        FilesMessage = null;
        VersoError = null;
        OnPropertyChanged(nameof(VisualsDropHint));
        Recompute();
        SchedulePreview();
    }

    // ---- Internals ----

    /// <param name="remove">What the item's close button does (default: remove it from the composition).</param>
    private VisualItem CreateItem(VisualInfo info, Action<VisualItem>? remove = null)
    {
        var item = new VisualItem(info, Changed, remove ?? RemoveItem, ChoosePageAsync,
            _mode == LayoutMode.Mixed ? DefaultMixedFormat() : null, remove is null ? DuplicateItem : null);
        item.InitCustomSize(CustomWidth, CustomHeight);
        _ = LoadThumbnailAsync(item);
        return item;
    }

    /// <summary>Size of a new line in the Mixed mode: the one chosen in the size section, custom size included.</summary>
    private FormatOption DefaultMixedFormat() => SelectedFormat;

    // "Duplicate the line": the same photo in another size, typically (in the Mixed mode, a file already in the list is
    // not added again: it would be a drop made twice). Once, on a new line below; its size is then changed.
    private void DuplicateItem(VisualItem item)
    {
        int index = Visuals.IndexOf(item);
        if (index < 0) return;
        var copy = CreateItem(item.SourceInfo);
        copy.CopyChoicesOf(item);
        Visuals.Insert(index + 1, copy);
        Changed();
    }

    /// <summary>Size and white margins of a line in the Mixed mode; null when its custom size is too small.</summary>
    private (SizeMm Size, MarginsMm Margins)? MixedSizeOf(VisualItem line)
    {
        var format = line.Format ?? DefaultMixedFormat();
        if (format.Format is { } preset) return (preset.Size, preset.Margins);
        return line.CustomWidth >= MinSizeMm && line.CustomHeight >= MinSizeMm
            ? (new SizeMm(line.CustomWidth, line.CustomHeight), MarginsMm.None)
            : null;
    }

    /// <summary>Name of the size of a line, on the cutting plan: that of the list, or the custom size in millimetres.</summary>
    private string MixedSizeName(VisualItem line) => (line.Format ?? DefaultMixedFormat()) is { IsCustom: false } preset
        ? preset.Name
        : $"{line.CustomWidth:0.#} × {line.CustomHeight:0.#} mm";

    // Another page of the PDF of an item, chosen in its page list.
    private async Task ChoosePageAsync(VisualItem item, int pageNumber)
    {
        var (info, _, error) = await LoadAsync(item.Info.OriginalPath, pageNumber);
        if (info is null)
        {
            FilesMessage = error;
            return;
        }

        item.Replace(info);
        _ = LoadThumbnailAsync(item);
    }

    private static async Task LoadThumbnailAsync(VisualItem item) =>
        item.Thumbnail = await ThumbnailLoader.LoadAsync(item.SourceInfo, ThumbnailPx); // the user's turn is drawn on screen

    private void RemoveItem(VisualItem item)
    {
        if (Visuals.Remove(item)) Changed();
    }

    private void ClearVisuals()
    {
        Visuals.Clear();
        FilesMessage = null;
        Changed();
    }

    private IEnumerable<VisualItem> ActiveVisuals() =>
        _mode == LayoutMode.Repeat ? (RepeatVisual is { } r ? [r] : []) : Visuals;

    private void SetMode(LayoutMode mode, bool carryOver = true)
    {
        if (_mode == mode) return;
        _mode = mode;

        // Keep the work in progress when switching modes, turned as it was. Compose and Mixed share their list.
        if (carryOver && mode != LayoutMode.Repeat && Visuals.Count == 0 && RepeatVisual is { } r)
        {
            var copy = CreateItem(r.SourceInfo);
            copy.CopyChoicesOf(r);
            Visuals.Add(copy);
        }
        if (carryOver && mode == LayoutMode.Repeat && RepeatVisual is null && Visuals.Count > 0)
        {
            _repeatVisual = CreateItem(Visuals[0].SourceInfo, _ => RepeatVisual = null);
            _repeatVisual.CopyChoicesOf(Visuals[0]);
        }

        // Lines added in another mode get their size in the Mixed mode now, from the size chosen until then.
        if (mode == LayoutMode.Mixed)
            foreach (var item in Visuals.Where(v => v.Format is null))
                item.Format = DefaultMixedFormat();

        FilesMessage = null;
        OnPropertyChanged(nameof(IsRepeatMode));
        OnPropertyChanged(nameof(IsComposeMode));
        OnPropertyChanged(nameof(IsMixedMode));
        OnPropertyChanged(nameof(HasVisualList));
        OnPropertyChanged(nameof(CanCompleteLastSheet));
        OnPropertyChanged(nameof(RepeatVisual));
        OnPropertyChanged(nameof(HasRepeatVisual));
        Changed();
    }

    private void SetFlip(DuplexFlip flip)
    {
        if (_flip == flip) return;
        _flip = flip;
        OnPropertyChanged(nameof(FlipLongEdge));
        OnPropertyChanged(nameof(FlipShortEdge));
        Changed();
    }

    private void SetFit(FitMode fit)
    {
        if (_fit == fit) return;
        _fit = fit;
        OnPropertyChanged(nameof(IsFill));
        OnPropertyChanged(nameof(IsFit));
        Changed();
    }

    private void SetUnit(QuantityUnit unit)
    {
        if (_unit == unit) return;
        _unit = unit;
        OnPropertyChanged(nameof(UnitItems));
        OnPropertyChanged(nameof(UnitSheets));
        OnPropertyChanged(nameof(CanCompleteLastSheet));
        Changed();
    }

    private SizeMm? CurrentItemSize()
    {
        if (SelectedFormat.Format is { } f) return f.Size;
        return CustomWidth >= MinSizeMm && CustomHeight >= MinSizeMm ? new SizeMm(CustomWidth, CustomHeight) : null;
    }

    /// <summary>The custom sheet, laid portrait, or null when its size is out of range.</summary>
    private PaperFormat? CustomSheet()
    {
        double w = Math.Min(CustomSheetWidth, CustomSheetHeight), h = Math.Max(CustomSheetWidth, CustomSheetHeight);
        if (w < MinSheetMm || h > MaxSheetMm) return null;
        double m = Math.Clamp(CustomSheetMargin, 0, (w - MinSheetMm) / 2); // leaves room to print
        return new PaperFormat($"{w:0.#}x{h:0.#}", w, h, new MarginsMm(m, m, m, m));
    }

    /// <summary>The four margins as typed, whether they are on or not.</summary>
    private MarginsMm TypedMargins() => new(_marginTop, _marginRight, _marginBottom, _marginLeft);

    /// <summary>Margins given to the layout: none when off, negative values counted as 0.</summary>
    private MarginsMm CurrentMargins() => MarginsEnabled
        ? new MarginsMm(Math.Max(0, MarginTop), Math.Max(0, MarginRight), Math.Max(0, MarginBottom), Math.Max(0, MarginLeft))
        : MarginsMm.None;

    private void MarginsEdited()
    {
        _userMargins = null; // edited by hand: they stay when the size changes
        Changed();
    }

    private void ApplyFormatMargins(MarginsMm preset)
    {
        if (!preset.IsEmpty)
        {
            _userMargins ??= (_marginsEnabled, TypedMargins());
            SetMargins(true, preset);
        }
        else if (_userMargins is { } user)
        {
            _userMargins = null;
            SetMargins(user.Enabled, user.Margins);
        }
    }

    private void SetMargins(bool enabled, MarginsMm m)
    {
        (_marginsEnabled, _marginTop, _marginRight, _marginBottom, _marginLeft) = (enabled, m.Top, m.Right, m.Bottom, m.Left);
        foreach (var name in new[] { nameof(MarginsEnabled), nameof(MarginTop), nameof(MarginRight), nameof(MarginBottom), nameof(MarginLeft) })
            OnPropertyChanged(name);
    }

    private void Changed()
    {
        ForgetPresetIfChanged();
        Recompute();
        SchedulePreview();
    }

    /// <summary>Width of the rendered preview, in pixels: more when the preview is zoomed.</summary>
    private uint _previewWidthPx = PreviewWidthPx;

    /// <summary>
    /// The sheet is shown <paramref name="pixels"/> wide on the screen (zoom of the preview): renders it again with
    /// enough pixels, from 1400 to 3600 in steps of 400, so that a small change of zoom renders nothing.
    /// </summary>
    public void SetPreviewDisplayWidth(double pixels)
    {
        uint width = (uint)Math.Clamp(Math.Ceiling(pixels / 400) * 400, PreviewWidthPx, MaxPreviewWidthPx);
        if (width == _previewWidthPx) return;
        _previewWidthPx = width;
        SchedulePreview();
    }

    private void SchedulePreview()
    {
        _previewDebounce.Stop();
        _previewDebounce.Start();
    }

    private void UpdatePreviewSides()
    {
        PreviewSides.Clear();
        PreviewSides.Add(RectoSide);
        if (VersoEnabled) PreviewSides.Add(VersoSide);
    }

    private void Recompute()
    {
        Notices.Clear();
        if (PreviewSides.Count == 0) UpdatePreviewSides();

        bool sheetValid = !SelectedSheet.IsCustom || CustomSheet() is not null;
        double bleed = BleedEnabled ? Math.Clamp(BleedMm, 0, 10) : 0;
        double spacing = SpacingEnabled ? Math.Clamp(SpacingMm, 0, 100) : 0;
        if (_mode == LayoutMode.Mixed)
            RecomputeMixed(sheetValid, bleed, spacing);
        else
            RecomputeGrid(sheetValid, bleed, spacing);

        PlanText = BuildPlanText();
        double edge = Sheet.Margins.Top;
        PreviewHint = edge > 0 ? Strings.Format("PreviewHintMargin", edge.ToString("0.#")) : Strings.Get("PreviewHint");

        var printable = Layout?.PrintableArea ?? new RectMm(0, 0, Sheet.Width, Sheet.Height);
        var margin = new GeometryGroup { FillRule = FillRule.EvenOdd };
        margin.Children.Add(new RectangleGeometry(new Rect(0, 0, Sheet.Width, Sheet.Height)));
        margin.Children.Add(new RectangleGeometry(new Rect(printable.X, printable.Y, printable.Width, printable.Height)));
        margin.Freeze();
        MarginGeometry = margin;
        PlaceholderCells = Layout?.Cells.Select(c => c.Trim).ToList() ?? [];

        foreach (var name in new[]
                 {
                     nameof(Layout), nameof(Plan), nameof(Headline), nameof(Details), nameof(PlanText), nameof(CanExport),
                     nameof(SheetWidth), nameof(SheetHeight), nameof(MarginGeometry), nameof(PlaceholderCells), nameof(PreviewHint),
                     nameof(PreviewSheetIndex), nameof(PreviewSheetLabel), nameof(HasSeveralSheets), nameof(CutNumbers),
                 })
            OnPropertyChanged(name);
    }

    // Repeat and Compose: one size, a grid of cells, the same on every sheet.
    private void RecomputeGrid(bool sheetValid, double bleed, double spacing)
    {
        _mixed = MixedPlan.Empty;
        CutNumbers = [];
        var item = CurrentItemSize();
        var margins = CurrentMargins();
        bool marginsFit = item is not { } s ||
                          (s.Width - margins.Left - margins.Right >= MinSizeMm && s.Height - margins.Top - margins.Bottom >= MinSizeMm);
        Layout = item is { } size && marginsFit && sheetValid
            ? LayoutEngine.Compute(new LayoutOptions(Sheet, size, bleed, CropMarks, spacing, margins))
            : null;
        int perSheet = Layout?.PerSheet ?? 0;

        // Each line gives its own VisualInfo object, so that a cell of the preview finds its line back (two lines may
        // hold equal ones: the same photo duplicated).
        _lineOfInfo.Clear();
        VisualInfo Own(VisualItem line)
        {
            var info = line.Info with { };
            _lineOfInfo[info] = line;
            return info;
        }

        if (_mode == LayoutMode.Repeat)
        {
            // Counted in sheets, the sheets are full; counted in visuals, the last sheet is filled only on request.
            int items = _unit == QuantityUnit.Items ? Quantity : Quantity * perSheet;
            Plan = RepeatVisual is { } r
                ? SheetFiller.Fill([new VisualCopies(Own(r), items)], perSheet, CompleteLastSheet)
                : SheetFiller.Fill([], perSheet, CompleteLastSheet);
        }
        else
        {
            Plan = SheetFiller.Fill(Visuals.Select(v => new VisualCopies(Own(v), v.Copies)).ToList(), perSheet, CompleteLastSheet);
        }

        if (_previewSheetIndex >= Plan.SheetCount) _previewSheetIndex = Math.Max(0, Plan.SheetCount - 1);

        if (item is null)
        {
            Headline = Strings.Get("InvalidSize");
            Details = Strings.Get("InvalidSizeDetails");
            Notices.Add(new Notice(NoticeLevel.Error, Details));
        }
        else if (!marginsFit)
        {
            Headline = Strings.Get("MarginsTooLarge");
            Details = Strings.Format("MarginsTooLargeDetails", MinSizeMm);
            Notices.Add(new Notice(NoticeLevel.Error, Details));
        }
        else if (!sheetValid)
        {
            Headline = Strings.Get("InvalidSheet");
            Details = Strings.Format("InvalidSheetDetails", MinSheetMm, MaxSheetMm);
            Notices.Add(new Notice(NoticeLevel.Error, Details));
        }
        else if (Layout!.IsEmpty)
        {
            Headline = Strings.Get("DoesNotFit");
            Details = Strings.Format("DoesNotFitDetails", SelectedFormat.Name, SheetLabel);
            Notices.Add(new Notice(NoticeLevel.Error, Strings.Get("DoesNotFitNotice")));
            AddCropMarksNotice(Layout);
        }
        else
        {
            Headline = Strings.Format("PerSheet", Layout.PerSheet);
            Details = $"{Layout.Columns} × {Layout.Rows}" +
                      (Layout.ItemRotated ? Strings.Get("DetailsRotated") : "") +
                      (spacing > 0 ? Strings.Format("DetailsSpacing", Layout.Options.Gap.ToString("0.#")) : "") +
                      (bleed > 0 ? Strings.Format("DetailsBleed", bleed.ToString("0.#"))
                          : spacing > 0 ? "" : Strings.Get("DetailsEdgeToEdge")) +
                      (margins.IsEmpty ? "" : Strings.Get("DetailsMargins")) +
                      (CropMarks ? Strings.Get("DetailsCropMarks") : "") +
                      (CutLines ? Strings.Get("DetailsCutLines") : "");
            var first = Layout.Cells[0];
            AddVisualNotices([.. ActiveVisuals().Select(v => (v, first))], first, bleed);
            AddCropMarksNotice(Layout);
        }
    }

    // Mixed: each line its own size, packed on as few sheets as possible, each sheet its own layout.
    private void RecomputeMixed(bool sheetValid, double bleed, double spacing)
    {
        // A line whose custom size is too small is left out, and says so.
        var lines = new List<VisualItem>();
        var items = new List<MixedItem>();
        foreach (var v in Visuals)
        {
            if (MixedSizeOf(v) is { } size)
            {
                lines.Add(v);
                items.Add(new MixedItem(v.Info, size.Size, v.Copies, size.Margins));
            }
            else
            {
                v.Quality = Strings.Format("QualitySizeTooSmall", MinSizeMm);
                v.IsLowQuality = true;
            }
        }
        _mixedLines = lines;
        _mixed = sheetValid ? Pack(items, bleed, spacing, CropMarks) : MixedPlan.Empty;
        Plan = new SheetPlan([.. _mixed.Sheets.Select(s => s.Visuals)], _mixed.Printed, _mixed.Printed, 0);
        if (_previewSheetIndex >= Plan.SheetCount) _previewSheetIndex = Math.Max(0, Plan.SheetCount - 1);
        ShowMixedSheet();

        if (!sheetValid)
        {
            Headline = Strings.Get("InvalidSheet");
            Details = Strings.Format("InvalidSheetDetails", MinSheetMm, MaxSheetMm);
            Notices.Add(new Notice(NoticeLevel.Error, Details));
            return;
        }

        string cutting = (spacing > 0 ? Strings.Format("DetailsSpacing", Math.Max(spacing, 2 * bleed).ToString("0.#")) : "") +
                         (bleed > 0 ? Strings.Format("DetailsBleed", bleed.ToString("0.#")) : spacing > 0 ? "" : Strings.Get("DetailsEdgeToEdge")) +
                         (CropMarks ? Strings.Get("DetailsCropMarks") : "") +
                         (MixedCutLines ? Strings.Get("DetailsCutLines") : "");
        Headline = _mixed.SheetCount > 0 ? Strings.Count("Sheets", _mixed.SheetCount) : Strings.Get("MixedHeadline");
        Details = _mixed.Printed > 0 ? Strings.Count("Prints", _mixed.Printed) + cutting : cutting.TrimStart(' ', '·');

        // Crop marks keep room around the block (none on a lab print): say how many sheets it takes without them,
        // when fewer.
        if (CropMarks && _mixed.SheetCount > 0 && Pack(items, bleed, spacing, false).SheetCount is var without && without < _mixed.SheetCount)
            Notices.Add(new Notice(NoticeLevel.Info, Strings.Format("NoticeWithoutCropMarksSheets", Strings.Count("Sheets", without))));

        foreach (int i in _mixed.TooLarge)
        {
            lines[i].Quality = Strings.Get("QualityTooLarge");
            lines[i].IsLowQuality = true;
        }
        if (_mixed.TooLarge.Count > 0)
            Notices.Add(new Notice(NoticeLevel.Error, Strings.Format("NoticeTooLarge", List(_mixed.TooLarge.Select(i => lines[i].FileName)), SheetLabel)));

        // Each line is checked in its own cell, the back in the largest one (where its resolution is lowest).
        var cells = new SortedDictionary<int, LayoutCell>();
        foreach (var sheet in _mixed.Sheets)
        foreach (var (cell, item) in sheet.Layout.Cells.Zip(sheet.Items))
            cells.TryAdd(item, cell);
        AddVisualNotices([.. cells.Select(c => (lines[c.Key], c.Value))],
            cells.Values.MaxBy(c => c.Trim.Width * c.Trim.Height), bleed);
    }

    /// <summary>
    /// Packs the lines, or gives the last packing again when nothing that matters changed (the packing is the costly
    /// part); the visuals are those of the lines as they are now (turned by hand, another page of a PDF).
    /// </summary>
    private MixedPlan Pack(List<MixedItem> items, double bleed, double spacing, bool cropMarks)
    {
        var sheet = Sheet;
        var key = string.Join(";", new[] { sheet.Width, sheet.Height, sheet.Margins.Top, bleed, spacing, cropMarks ? 1 : 0 }
            .Concat(items.SelectMany(i => new[] { i.Size.Width, i.Size.Height, i.Margins.Top, i.Margins.Right, i.Margins.Bottom, i.Margins.Left, i.Copies }))
            .Select(v => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
        if (!_packings.TryGetValue(key, out var plan))
        {
            if (_packings.Count >= 8) _packings.Clear();
            _packings[key] = plan = MixedPacker.Pack(sheet, items, bleed, spacing, cropMarks);
        }

        return plan with
        {
            Sheets = [.. plan.Sheets.Select(s => s with { Visuals = [.. s.Items.Select(i => (VisualInfo?)items[i].Visual)] })],
        };
    }

    // The sheet shown in the Mixed mode: its layout, its cell outlines and the numbers of its cuts.
    private void ShowMixedSheet()
    {
        Layout = _mixed.SheetCount > 0 ? _mixed.Sheets[_previewSheetIndex].Layout : null;
        PlaceholderCells = Layout?.Cells.Select(c => c.Trim).ToList() ?? [];
        CutNumbers = Layout is { } layout ? NumbersOf(layout) : [];
        OnPropertyChanged(nameof(Layout));
        OnPropertyChanged(nameof(PlaceholderCells));
        OnPropertyChanged(nameof(CutNumbers));
    }

    // In proportion to the sheet, which the preview fits to its area: about 12 mm on SRA3, the same size on screen
    // for every sheet.
    private static List<CutNumber> NumbersOf(SheetLayout layout)
    {
        double size = Math.Max(layout.Sheet.Width, layout.Sheet.Height) * 0.027;
        var at = MixedPacker.NumberPositions(layout, size);
        return [.. at.Select((p, i) => new CutNumber(p.X - size / 2, p.Y - size / 2, size, size * (i < 9 ? 0.62 : 0.5), (i + 1).ToString()))];
    }

    private string BuildPlanText()
    {
        if (Plan.SheetCount == 0) return "";
        string text = "→ " + Strings.Count("Sheets", Plan.SheetCount) + " · " +
                      Strings.Count(_mode == LayoutMode.Repeat ? "Visuals" : "Prints", Plan.Printed);
        if (Plan.Extra > 0) text += Strings.Format("PlanExtra", Plan.Extra);
        if (Plan.EmptyCells > 0) text += " · " + Strings.Count("EmptyCells", Plan.EmptyCells);
        return text;
    }

    /// <param name="fronts">Each visual with the cell it is checked in.</param>
    /// <param name="back">The cell the back is checked in.</param>
    /// <param name="bleed">Bleed of the layout, in millimetres.</param>
    private void AddVisualNotices(IReadOnlyList<(VisualItem Item, LayoutCell Cell)> fronts, LayoutCell? back, double bleed)
    {
        if (!ActiveVisuals().Any())
        {
            Notices.Add(new Notice(NoticeLevel.Info, Strings.Get(_mode == LayoutMode.Repeat ? "NoticeAddVisual" : "NoticeAddVisuals")));
            return;
        }

        var lowDpi = new List<(string Name, int Dpi)>();
        var cropped = new List<(string Name, double Ratio)>();
        var ownBleed = new List<(string Name, double Mm)>();
        foreach (var (item, cell) in fronts)
            Assess(item, cell, SideOptions.Recto, lowDpi, cropped, ownBleed);
        if (VersoEnabled && Verso is not null && back is not null)
            Assess(Verso, back, new SideOptions(true, _flip, RotateVerso180, BackHasMargins), lowDpi, cropped, ownBleed);

        var critical = lowDpi.Where(d => d.Dpi < CriticalDpi).ToList();
        var low = lowDpi.Where(d => d.Dpi >= CriticalDpi).ToList();
        if (critical.Count > 0)
            Notices.Add(new Notice(NoticeLevel.Error, Strings.Format("NoticeDpiCritical", List(critical.Select(d => $"{d.Name} ({d.Dpi} dpi)")), MinDpi)));
        if (low.Count > 0)
            Notices.Add(new Notice(NoticeLevel.Warning, Strings.Format("NoticeDpiLow", List(low.Select(d => $"{d.Name} ({d.Dpi} dpi)")), MinDpi)));
        if (cropped.Count > 0)
            Notices.Add(new Notice(NoticeLevel.Warning, Strings.Format("NoticeCropped", List(cropped.Select(c => $"{c.Name} ({c.Ratio:P0})")))));
        foreach (var (name, mm) in ownBleed.Distinct())
        {
            Notices.Add(new Notice(NoticeLevel.Info, Strings.Format("NoticeOwnBleed", name, mm.ToString("0.#"))));
            if (bleed > mm + 0.05)
                Notices.Add(new Notice(NoticeLevel.Warning,
                    Strings.Format("NoticeOwnBleedTooSmall", name, mm.ToString("0.#"), bleed.ToString("0.#"))));
        }

        // No notice for what the settings column already says (asked by the author): the empty cells are in the
        // quantity summary, the "Fit" mode in its own label.
        if (VersoEnabled && Verso is null)
            Notices.Add(new Notice(NoticeLevel.Warning, Strings.Get("NoticeNoBack")));
    }

    private void Assess(VisualItem item, LayoutCell cell, SideOptions side,
        List<(string, int)> lowDpi, List<(string, double)> cropped, List<(string, double)> ownBleed)
    {
        var v = item.Info;
        var p = Placement.Compute(v, cell, _fit, side);

        if (double.IsNaN(p.EffectiveDpi))
        {
            item.Quality = Strings.Get("QualityPdf");
            item.IsLowQuality = false;
        }
        else
        {
            int dpi = (int)Math.Round(p.EffectiveDpi);
            item.Quality = Strings.Format("QualityDpi", dpi);
            item.IsLowQuality = dpi < MinDpi;
            if (dpi < MinDpi) lowDpi.Add((v.FileName, dpi));
        }

        if (_fit == FitMode.Fill)
        {
            // Only the proportion mismatch with the finished image matters: the bleed is cut off anyway.
            var image = side.HasMargins ? cell.ImageTrim : cell.Trim;
            var (cw, ch) = p.AngleDeg % 180 == 90 ? (v.Content.Height, v.Content.Width) : (v.Content.Width, v.Content.Height);
            double rv = cw / ch, rt = image.Width / image.Height;
            double ratio = 1 - Math.Min(rv, rt) / Math.Max(rv, rt);
            if (ratio > 0.03) cropped.Add((v.FileName, ratio));
        }

        if (v.HasOwnBleed)
            ownBleed.Add((v.FileName, Units.PtToMm(new[]
            {
                v.Content.X, v.Content.Y, v.FullWidth - v.Content.X - v.Content.Width, v.FullHeight - v.Content.Y - v.Content.Height,
            }.Min())));
    }

    // Crop marks need room around the block (none on a lab print): say how many fit without them, when that is more.
    private void AddCropMarksNotice(SheetLayout layout)
    {
        if (!layout.Options.CropMarks) return;
        int without = LayoutEngine.Compute(layout.Options with { CropMarks = false }).PerSheet;
        if (without > layout.PerSheet)
            Notices.Add(new Notice(NoticeLevel.Info, Strings.Format("NoticeWithoutCropMarks", without)));
    }

    // Each file once: the same file can be a front and the back, or be added twice.
    private static string List(IEnumerable<string> items)
    {
        var all = items.Distinct().ToList();
        return all.Count <= 3 ? string.Join(", ", all) : Strings.Format("ListAndMore", string.Join(", ", all.Take(3)), all.Count - 3);
    }

    private async Task RefreshPreviewAsync()
    {
        int version = ++_previewVersion;
        if (!CanExport)
        {
            RectoSide.Image = VersoSide.Image = null;
            IsPreviewBusy = false;
            ClearOverlays();
            return;
        }

        var job = BuildJob([PreviewSheetIndex]);
        IsPreviewBusy = true;
        try
        {
            var pdf = await Task.Run(() =>
            {
                using var ms = new MemoryStream();
                ImpositionPdfWriter.Write(job, ms);
                return ms.ToArray();
            });
            var recto = await PdfPreviewRenderer.RenderAsync(pdf, 0, _previewWidthPx);
            var verso = job.Verso is null ? null : await PdfPreviewRenderer.RenderAsync(pdf, 1, _previewWidthPx);
            if (version == _previewVersion)
            {
                RectoSide.Image = recto;
                VersoSide.Image = verso;
                ClearOverlays();
            }
        }
        catch (Exception ex)
        {
            if (version == _previewVersion)
            {
                RectoSide.Image = VersoSide.Image = null;
                ClearOverlays();
                Notices.Add(new Notice(NoticeLevel.Error, Strings.Format("NoticePreviewFailed", ex.Message)));
            }
        }
        finally
        {
            if (version == _previewVersion) IsPreviewBusy = false;
        }
    }

    /// <param name="sheets">Indices of the sheets, from 0.</param>
    private ImpositionJob BuildJob(IReadOnlyList<int> sheets)
    {
        // Mixed sheets each have their own layout, and their own cut lines setting (on by default).
        var job = _mode == LayoutMode.Mixed
            ? ImpositionJob.Mixed(_mixed, sheets) with { Title = Strings.Get("PdfTitleMixed"), CutLines = MixedCutLines }
            : new ImpositionJob(Layout!, [.. sheets.Select(i => Plan.Sheets[i])], CutLines: CutLines,
                Title: _mode == LayoutMode.Repeat ? RepeatVisual?.Info.FileName : Strings.Get("PdfTitleCompose"));
        return job with
        {
            Verso = VersoEnabled ? Verso?.Info : null,
            Fit = _fit,
            Flip = _flip,
            RotateVerso180 = RotateVerso180,
            VersoMargins = BackHasMargins,
        };
    }

    private List<int> AllSheets() => [.. Enumerable.Range(0, Plan.SheetCount)];
}
