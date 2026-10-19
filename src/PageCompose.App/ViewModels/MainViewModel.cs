using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
/// <summary>A colour of the captions, with its name and its swatch in the list.</summary>
public sealed class CaptionColorOption(CaptionColor value)
{
    public CaptionColor Value { get; } = value;
    public string Name => Strings.Get("CaptionColor_" + Value);
    public Brush Swatch { get; } = Freeze(value.Rgb());

    public override string ToString() => Name;

    private static SolidColorBrush Freeze((byte R, byte G, byte B) c)
    {
        var brush = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B)); // the colour printed, not a theme colour
        brush.Freeze();
        return brush;
    }
}

/// <summary>A colour of the numbers or of their background (null: no background), with its name and swatch.</summary>
public sealed class NumberColorOption(NumberColor? value)
{
    public NumberColor? Value { get; } = value;
    public string Name => Value is { } color ? Strings.Get("NumberColor_" + color) : Strings.Get("NumberBackgroundNone");
    public Brush Swatch { get; } = Freeze(value);

    public override string ToString() => Name;

    private static Brush Freeze(NumberColor? value)
    {
        if (value is not { } color) return Brushes.Transparent;
        var (r, g, b) = color.Rgb();
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b)); // the colour printed, not a theme colour
        brush.Freeze();
        return brush;
    }
}

/// <summary>The characters of the codes made by the app, with their name.</summary>
public sealed class CodeAlphabetOption(CodeAlphabet value)
{
    public CodeAlphabet Value { get; } = value;
    public string Name => Strings.Get("CodeAlphabet_" + Value);

    public override string ToString() => Name;
}

/// <summary>A way a number reads, with its name.</summary>
public sealed class NumberDirectionOption(NumberDirection value)
{
    public NumberDirection Value { get; } = value;
    public string Name => Strings.Get("NumberDirection_" + Value);

    public override string ToString() => Name;
}

/// <summary>A colour of the cut lines, with its name and its swatch in the list.</summary>
public sealed class CutLineColorOption(CutLineColor value)
{
    public CutLineColor Value { get; } = value;
    public string Name => Strings.Get("CutColor_" + Value);

    public Brush Swatch { get; } = Freeze(value.Rgb());

    public override string ToString() => Name; // what screen readers say

    private static SolidColorBrush Freeze((byte R, byte G, byte B) c)
    {
        var brush = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B)); // the colour printed, not a theme colour
        brush.Freeze();
        return brush;
    }
}

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

/// <param name="Key">One or two words for the line that sums the notices up ("Cropping").</param>
public sealed record Notice(NoticeLevel Level, string Text, string Key)
{
    /// <summary>Segoe Fluent Icons glyph, as in the Windows 11 InfoBar.</summary>
    public string Icon => IconOf(Level);

    public static string IconOf(NoticeLevel level) => level switch { NoticeLevel.Error => "", NoticeLevel.Warning => "", _ => "" };
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
    private const uint ProjectPreviewPx = 120; // the first sheet in the recent projects (about 31 × 44 at 200 % zoom)

    private readonly AppSettings _settings;
    private readonly DispatcherTimer _previewDebounce;
    private int _previewVersion;

    public MainViewModel(AppSettings settings)
    {
        _settings = settings;
        Formats = [.. ItemFormats.All.Select(f => new FormatOption(f)), new FormatOption(null)];
        MixedFormats = Formats;
        FormatsForAll = [.. Formats.Where(f => !f.IsCustom)];
        Sheets = [.. SheetFormats.All.Select(s => new SheetOption(s)), new SheetOption(null)];

        _mode = settings.Mode;
        _selectedFormat = Formats.FirstOrDefault(f => (f.Format?.Id ?? AppSettings.CustomFormatId) == settings.FormatId) ?? Formats[0];
        _customWidth = settings.CustomWidth;
        _customHeight = settings.CustomHeight;
        _selectedSheet = Sheets.FirstOrDefault(s => (s.Sheet?.Id ?? AppSettings.CustomFormatId) == settings.SheetId) ?? Sheets[0];
        (_customSheetWidth, _customSheetHeight, _customSheetMargin) =
            (settings.CustomSheetWidth, settings.CustomSheetHeight, settings.CustomSheetMargin);
        _cropMarks = settings.CropMarks;
        _foldMarks = settings.FoldMarks;
        _cutLines = settings.CutLines;
        _mixedCutLines = settings.MixedCutLines;
        _bleedEnabled = settings.BleedEnabled;
        _mirrorBleed = settings.MirrorBleed;
        CaptionFonts = FontList.Load();
        _captionFont = FontOf(settings.CaptionFont);
        _captionColor = settings.CaptionColor;
        CaptionColors = [.. Enum.GetValues<CaptionColor>().Select(c => new CaptionColorOption(c))];
        _numbering = settings.Numbering?.Copy() ?? new();
        NumberColors = [.. Enum.GetValues<NumberColor>().Select(c => new NumberColorOption(c))];
        NumberBackgrounds = [new NumberColorOption(null), .. NumberColors];
        NumberDirections = [.. Enum.GetValues<NumberDirection>().Select(d => new NumberDirectionOption(d))];
        CodeAlphabets = [.. Enum.GetValues<CodeAlphabet>().Select(a => new CodeAlphabetOption(a))];
        _bleedMm = settings.BleedMm;
        _spacingEnabled = settings.SpacingEnabled;
        _spacingMm = settings.SpacingMm;
        _marginsEnabled = settings.MarginsEnabled;
        _versoMargins = settings.VersoMargins;
        (_marginTop, _marginRight, _marginBottom, _marginLeft) =
            (settings.MarginTop, settings.MarginRight, settings.MarginBottom, settings.MarginLeft);
        TakeMarginsOfSize();
        _fit = settings.Fit;
        _completeLastSheet = settings.CompleteLastSheet;
        _unit = settings.Unit;
        _flattenPdf = settings.FlattenPdf;
        _cuttingPlan = settings.CuttingPlan;
        _sheetMark = settings.SheetMark;
        _saveProject = settings.SaveProject;
        _cutLineColor = settings.CutLineColor;
        CutLineColors = [.. Enum.GetValues<CutLineColor>().Select(c => new CutLineColorOption(c))];

        ClearVisualsCommand = new RelayCommand(ClearVisuals);
        MoreForAllCommand = new RelayCommand(() => AllLines(line => line.Copies++));
        LessForAllCommand = new RelayCommand(() => AllLines(line => line.Copies--));
        ApplyFormatForAllCommand = new RelayCommand(ApplyFormatForAll);
        DrawNewCodesCommand = new RelayCommand(DrawNewCodes);
        PreviousSheetCommand = new RelayCommand(() => PreviewSheetIndex--);
        NextSheetCommand = new RelayCommand(() => PreviewSheetIndex++);
        Notices.CollectionChanged += (_, _) =>
        {
            foreach (var name in new[]
                     {
                         nameof(HasNotices), nameof(WorstNoticeLevel), nameof(WorstNoticeIcon), nameof(NoticeTitle), nameof(NoticeDetail),
                         nameof(CanOpenNotices), nameof(NoticeBarTooltip),
                     })
                OnPropertyChanged(name);
            if (!CanOpenNotices) NoticesOpen = false;
        };
        Visuals.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasVisuals));
            OnPropertyChanged(nameof(VisualsDropHint));
        };

        _previewDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) }; // 200 until 0.11.0, when a render took a second
        _previewDebounce.Tick += async (_, _) =>
        {
            _previewDebounce.Stop();
            await RefreshPreviewAsync();
        };
        _historyDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(HistoryDelayMs) };
        _historyDebounce.Tick += (_, _) => Remember();
        _lastJobDebounce = new DispatcherTimer { Interval = TimeSpan.FromSeconds(LastJobDelaySeconds) };
        _lastJobDebounce.Tick += async (_, _) =>
        {
            _lastJobDebounce.Stop();
            await WriteLastJobAsync();
        };

        Recompute();
        _current = CaptureState();
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
    /// Imports a preset file (double click on a .PageComposePreset file, or dropped on the window). A file of one preset
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
        FoldMarks = FoldMarks,
        CutLines = CutLines,
        MixedCutLines = MixedCutLines,
        BleedEnabled = BleedEnabled,
        MirrorBleed = MirrorBleed,
        CaptionFont = SelectedCaptionFont?.Name ?? FontList.DefaultFont,
        CaptionColor = _captionColor,
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
        SheetMark = SheetMark,
        SaveProject = SaveProject,
        CutLineColor = _cutLineColor,
        Numbering = _numbering.Copy(),
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
        TakeMarginsOfSize();
        VersoMargins = p.VersoMargins;
        SelectedSheet = Sheets.FirstOrDefault(s => (s.Sheet?.Id ?? AppSettings.CustomFormatId) == p.SheetId) ?? SelectedSheet;
        CustomSheetWidth = p.CustomSheetWidth;
        CustomSheetHeight = p.CustomSheetHeight;
        CustomSheetMargin = p.CustomSheetMargin;
        CropMarks = p.CropMarks;
        FoldMarks = p.FoldMarks;
        CutLines = p.CutLines;
        MixedCutLines = p.MixedCutLines;
        BleedEnabled = p.BleedEnabled;
        MirrorBleed = p.MirrorBleed;
        SelectedCaptionFont = FontOf(p.CaptionFont) ?? SelectedCaptionFont;
        SelectedCaptionColor = CaptionColors.First(c => c.Value == p.CaptionColor);
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
        SheetMark = p.SheetMark;
        SaveProject = p.SaveProject;
        SelectedCutLineColor = CutLineColors.First(c => c.Value == p.CutLineColor);
        SetNumberingSettings(p.Numbering ?? new());
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

    // ---- The copies of every line at once (asked by the author: 2 prints of each of 40 photos) ----

    public bool ShowCopiesForAll => Visuals.Count > 1;

    /// <summary>The copies of every line when they all have the same; null (an empty box) while they differ. Typed: every line takes it.</summary>
    public int? CopiesForAll
    {
        get => Visuals.Count > 0 && Visuals.All(v => v.Copies == Visuals[0].Copies) ? Visuals[0].Copies : null;
        set
        {
            if (value is int n) AllLines(line => line.Copies = n);
        }
    }

    public ICommand MoreForAllCommand { get; }
    public ICommand LessForAllCommand { get; }

    // ---- The size of every line at once, in Mixed sizes (issue #1 of the author) ----
    // On its own row above the copies, and applied by a button only: choosing in the list changes nothing (his
    // choice, after a first version that applied the size at once and overwrote the sizes chosen line by line).

    public bool ShowFormatForAll => _mode == LayoutMode.Mixed && Visuals.Count > 1;

    /// <summary>The preset sizes: a custom size stays typed line by line.</summary>
    public IReadOnlyList<FormatOption> FormatsForAll { get; }

    private FormatOption? _formatToApply;

    /// <summary>
    /// The size chosen in the list, applied by <see cref="ApplyFormatForAllCommand"/>. Before any choice: the size of
    /// every line when they all have the same preset size, else nothing.
    /// </summary>
    public FormatOption? FormatForAll
    {
        get => _formatToApply ?? (Visuals.Count > 0 && Visuals[0].Format is { IsCustom: false } first && Visuals.All(v => v.Format == first) ? first : null);
        set
        {
            if (value is not { IsCustom: false } format || format == _formatToApply) return;
            _formatToApply = format;
            OnPropertyChanged(nameof(FormatForAll));
            OnPropertyChanged(nameof(CanApplyFormatForAll));
        }
    }

    /// <summary>A size is chosen, and some line has another one.</summary>
    public bool CanApplyFormatForAll => FormatForAll is { } f && Visuals.Any(v => v.Format != f);

    public ICommand ApplyFormatForAllCommand { get; }

    private void ApplyFormatForAll()
    {
        if (FormatForAll is not { } format) return;
        _formatToApply = null; // the list then shows the size of every line
        AllLines(line => line.Format = format);
    }

    // Every line changed, then one recomputation (not one per line: packing mixed sizes is the costly part).
    private void AllLines(Action<VisualItem> change)
    {
        _restoring = true;
        try { foreach (var line in Visuals) change(line); }
        finally { _restoring = false; }
        Changed();
    }

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

    /// <summary>The colours offered for the cut lines (the crop marks stay black, on the white paper).</summary>
    public IReadOnlyList<CutLineColorOption> CutLineColors { get; }

    private CutLineColor _cutLineColor;
    public CutLineColorOption SelectedCutLineColor
    {
        get => CutLineColors.First(c => c.Value == _cutLineColor);
        set
        {
            if (value is null || value.Value == _cutLineColor) return;
            _cutLineColor = value.Value;
            OnPropertyChanged();
            Changed();
        }
    }

    /// <summary>The colour list shows while the cut lines of the mode are on.</summary>
    public bool ShowCutLineColor => _mode == LayoutMode.Mixed ? MixedCutLines : CutLines;

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

    private bool _foldMarks;

    /// <summary>Folded cards: dashed marks in line with each fold, outside the block, as the crop marks (asked by the author).</summary>
    public bool FoldMarks { get => _foldMarks; set { if (Set(ref _foldMarks, value)) Changed(); } }

    /// <summary>The size chosen folds (a folded card): its marks can be drawn. Not in the Mixed mode, where each line has its size.</summary>
    public bool ShowFoldMarks => _mode != LayoutMode.Mixed && SelectedFormat.Format?.Folds is { IsEmpty: false };

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

    private bool _mirrorBleed;

    /// <summary>
    /// The bleed of a photo takes its edges, mirrored: the photo keeps its size in its trim (asked by the author; the
    /// default enlarges it to cover the bleed, its edges going with the cut).
    /// </summary>
    public bool MirrorBleed
    {
        get => _mirrorBleed;
        set
        {
            if (!Set(ref _mirrorBleed, value)) return;
            OnPropertyChanged(nameof(EnlargeForBleed));
            Changed();
        }
    }

    public bool EnlargeForBleed { get => !_mirrorBleed; set { if (value) MirrorBleed = false; } }

    // ---- Numbering (asked by the author: raffle tickets, numbered cards, unique promo codes…) ----
    // Each visual laid gets its own number, or its own code, written on the front at one or two places of the visual,
    // with a QR code if asked. By sheet (the default, the author's choice), or in a pile: down the sheets in each cell,
    // so that the sheets stacked and cut give piles already in order. The codes are made by the app or read from a list,
    // and written next to each export ("name-codes.csv").

    private NumberingSettings _numbering;

    private void SetNumbering<T>(T value, Func<NumberingSettings, T> get, Action<NumberingSettings, T> set,
        [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(get(_numbering), value)) return;
        set(_numbering, value);
        OnPropertyChanged(name);
        Changed();
    }

    // A preset, an undo step, a project: every setting of the numbering at once.
    private void SetNumberingSettings(NumberingSettings numbering)
    {
        _numbering = numbering.Copy();
        foreach (var name in new[]
                 {
                     nameof(NumberingEnabled), nameof(NumberingNumbers), nameof(NumberingGeneratedCodes), nameof(NumberingCodeList),
                     nameof(CodeLength), nameof(CodeGroup), nameof(SelectedCodeAlphabet),
                     nameof(NumberingQr), nameof(QrContentIndex), nameof(QrAddressMode), nameof(QrAddress), nameof(QrX), nameof(QrY), nameof(QrSizeMm),
                     nameof(NumberingFirst), nameof(NumberingDigits), nameof(NumberingPrefix),
                     nameof(NumberingSuffix), nameof(NumberingInPile), nameof(NumberingBySheet), nameof(NumberX1), nameof(NumberY1),
                     nameof(NumberDirection1), nameof(NumberingSecond), nameof(NumberX2), nameof(NumberY2), nameof(NumberDirection2),
                     nameof(SelectedNumberFont), nameof(NumberSizeMm), nameof(SelectedNumberColor), nameof(SelectedNumberBackground),
                     nameof(NumberingSummary),
                 })
            OnPropertyChanged(name);
        Changed();
    }

    public bool ShowNumbering => _mode != LayoutMode.Mixed;

    public bool NumberingEnabled { get => _numbering.Enabled; set => SetNumbering(value, n => n.Enabled, (n, v) => n.Enabled = v); }

    // What each visual gets: chosen at the top of the card, as the modes (the author's mock-up).
    public bool NumberingNumbers { get => _numbering.Content == NumberingContent.Numbers; set { if (value) SetContent(NumberingContent.Numbers); } }
    public bool NumberingGeneratedCodes { get => _numbering.Content == NumberingContent.GeneratedCodes; set { if (value) SetContent(NumberingContent.GeneratedCodes); } }
    public bool NumberingCodeList { get => _numbering.Content == NumberingContent.CodeList; set { if (value) SetContent(NumberingContent.CodeList); } }

    private void SetContent(NumberingContent content)
    {
        if (_numbering.Content == content) return;
        // The text before a number ("N° ") does not suit a code ("Code : "), and back: changed while it is the default.
        string numberPrefix = new NumberingSettings().Prefix, codePrefix = Strings.Get("NumberingCodePrefix");
        if (content == NumberingContent.Numbers && _numbering.Prefix == codePrefix) _numbering.Prefix = numberPrefix;
        else if (content != NumberingContent.Numbers && _numbering.Prefix == numberPrefix) _numbering.Prefix = codePrefix;
        _numbering.Content = content;
        foreach (var name in new[] { nameof(NumberingNumbers), nameof(NumberingGeneratedCodes), nameof(NumberingCodeList), nameof(NumberingPrefix) })
            OnPropertyChanged(name);
        Changed();
    }

    /// <summary>Characters of the codes made by the app (3 to 32), and their groups (0: none).</summary>
    public int CodeLength { get => _numbering.CodeLength; set => SetNumbering(Math.Clamp(value, 3, 32), n => n.CodeLength, (n, v) => n.CodeLength = v); }
    public int CodeGroup { get => _numbering.CodeGroup; set => SetNumbering(Math.Clamp(value, 0, 32), n => n.CodeGroup, (n, v) => n.CodeGroup = v); }

    public IReadOnlyList<CodeAlphabetOption> CodeAlphabets { get; }

    public CodeAlphabetOption SelectedCodeAlphabet
    {
        get => CodeAlphabets.First(a => a.Value == _numbering.CodeAlphabet);
        set { if (value is not null) SetNumbering(value.Value, n => n.CodeAlphabet, (n, v) => n.CodeAlphabet = v); }
    }

    // The codes of the job: made by the app (and how, "8/4/LettersAndDigits"), or read from a list (and the name of its
    // file). They belong to the job as its visuals do (undo, projects, the last job), never to the settings: the same
    // project prints the same codes again, and two jobs never share theirs.
    private sealed record JobCodes(IReadOnlyList<string> Generated, string GeneratedFormat, IReadOnlyList<string> List, string ListName,
        int ListDuplicates = 0)
    {
        public static readonly JobCodes None = new([], "", [], "");

        public bool SameAs(JobCodes other) => GeneratedFormat == other.GeneratedFormat && ListName == other.ListName &&
                                              (ReferenceEquals(Generated, other.Generated) || Generated.SequenceEqual(other.Generated)) &&
                                              (ReferenceEquals(List, other.List) || List.SequenceEqual(other.List));
    }

    private JobCodes _codes = JobCodes.None;

    // More visuals than codes of that length and those characters.
    private bool _codesTooFew;

    private string CodeFormat() => FormattableString.Invariant($"{_numbering.CodeLength}/{_numbering.CodeGroup}/{_numbering.CodeAlphabet}");

    // The visuals laid, each taking a number or a code.
    private int NumberedVisuals() => Plan.Sheets.Sum(s => s.Count(v => v is not null));

    // The codes made by the app: as many as the visuals laid, made once and kept with the job, so that the preview, the
    // export and its list of codes always agree. Visuals added get new codes, those given never change; codes of
    // another length or other characters are made again.
    private void EnsureCodes()
    {
        _codesTooFew = false;
        if (!_numbering.Enabled || _mode == LayoutMode.Mixed || _numbering.Content != NumberingContent.GeneratedCodes) return;
        string format = CodeFormat();
        var codes = _codes.GeneratedFormat == format ? _codes.Generated : [];
        int more = NumberedVisuals() - codes.Count;
        if (more <= 0 && _codes.GeneratedFormat == format) return;
        try
        {
            var added = more > 0 ? Numbering.NewCodes(more, _numbering.CodeLength, _numbering.CodeGroup, _numbering.CodeAlphabet, codes) : [];
            _codes = _codes with { Generated = [.. codes, .. added], GeneratedFormat = format };
        }
        catch (InvalidOperationException)
        {
            _codesTooFew = true;
        }
    }

    /// <summary>"Draw new codes": every code made again, none of those given before (a batch already printed).</summary>
    public ICommand DrawNewCodesCommand { get; }

    private void DrawNewCodes()
    {
        List<string> codes;
        try
        {
            codes = Numbering.NewCodes(NumberedVisuals(), _numbering.CodeLength, _numbering.CodeGroup, _numbering.CodeAlphabet, _codes.Generated);
        }
        catch (InvalidOperationException)
        {
            codes = []; // made again without avoiding the old ones, or said too few
        }
        _codes = _codes with { Generated = codes, GeneratedFormat = codes.Count > 0 ? CodeFormat() : "" };
        Changed();
    }

    /// <summary>"7F3K-9QXA": the first code made, or one of the same kind.</summary>
    public string CodeExample
    {
        get
        {
            if (_codes.GeneratedFormat == CodeFormat() && _codes.Generated.Count > 0) return _codes.Generated[0];
            if (_codeExample.Format != CodeFormat())
            {
                try { _codeExample = (CodeFormat(), Numbering.NewCodes(1, _numbering.CodeLength, _numbering.CodeGroup, _numbering.CodeAlphabet)[0]); }
                catch (InvalidOperationException) { _codeExample = (CodeFormat(), ""); }
            }
            return _codeExample.Code;
        }
    }

    private (string Format, string Code) _codeExample = ("", "");

    /// <summary>The name of the file of the list of codes, or "No list chosen".</summary>
    public string CodeListName => HasCodeList ? _codes.ListName : Strings.Get("CodeListNoFile");

    public bool HasCodeList => _codes.ListName.Length > 0;

    /// <summary>"480 codes · 2 duplicates left out".</summary>
    public string? CodeListStatus => !HasCodeList ? null
        : _codes.List.Count == 0 ? Strings.Get("CodeListEmpty")
        : Strings.Count("CodesInList", _codes.List.Count) + (_codes.ListDuplicates > 0 ? " · " + Strings.Count("CodeDuplicates", _codes.ListDuplicates) : "");

    /// <summary>
    /// "Choose…": the codes of a text or CSV file, in its order (the first column). In the Repeat mode the quantity
    /// follows the list, in visuals.
    /// </summary>
    /// <exception cref="IOException">The file cannot be read.</exception>
    public async Task LoadCodeListAsync(string path)
    {
        string text = await Task.Run(() => File.ReadAllText(path));
        var read = Numbering.ReadCodes(text);
        _codes = _codes with { List = read.Codes, ListName = Path.GetFileName(path), ListDuplicates = read.Duplicates };
        if (_mode == LayoutMode.Repeat && read.Codes.Count > 0)
        {
            _unit = QuantityUnit.Items;
            _quantity = Math.Min(read.Codes.Count, 100_000);
            foreach (var name in new[] { nameof(UnitItems), nameof(UnitSheets), nameof(CanCompleteLastSheet), nameof(Quantity) })
                OnPropertyChanged(name);
        }
        Changed();
    }

    // ---- The QR code of each number or code (asked by the author with the codes) ----

    public bool NumberingQr { get => _numbering.Qr; set => SetNumbering(value, n => n.Qr, (n, v) => n.Qr = v); }

    /// <summary>0: the code alone; 1: an address with the code in it.</summary>
    public int QrContentIndex
    {
        get => _numbering.QrAddressMode ? 1 : 0;
        set
        {
            SetNumbering(value == 1, n => n.QrAddressMode, (n, v) => n.QrAddressMode = v);
            OnPropertyChanged(nameof(QrAddressMode));
        }
    }

    public bool QrAddressMode => _numbering.QrAddressMode;
    public string QrAddress { get => _numbering.QrAddress; set => SetNumbering(value ?? "", n => n.QrAddress, (n, v) => n.QrAddress = v); }
    public double QrX { get => _numbering.QrX; set => SetNumbering(Math.Clamp(value, 0, 3000), n => n.QrX, (n, v) => n.QrX = v); }
    public double QrY { get => _numbering.QrY; set => SetNumbering(Math.Clamp(value, 0, 3000), n => n.QrY, (n, v) => n.QrY = v); }
    public double QrSizeMm { get => _numbering.QrSizeMm; set => SetNumbering(Math.Clamp(value, 5, 500), n => n.QrSizeMm, (n, v) => n.QrSizeMm = v); }
    public int NumberingFirst { get => _numbering.First; set => SetNumbering(Math.Clamp(value, 0, 999_999_999), n => n.First, (n, v) => n.First = v); }
    public int NumberingDigits { get => _numbering.Digits; set => SetNumbering(Math.Clamp(value, 0, 9), n => n.Digits, (n, v) => n.Digits = v); }
    public string NumberingPrefix { get => _numbering.Prefix; set => SetNumbering(value ?? "", n => n.Prefix, (n, v) => n.Prefix = v); }
    public string NumberingSuffix { get => _numbering.Suffix; set => SetNumbering(value ?? "", n => n.Suffix, (n, v) => n.Suffix = v); }

    public bool NumberingInPile
    {
        get => _numbering.Order == NumberingOrder.Pile;
        set
        {
            if (!value) return; // the other button of the pair is chosen
            SetNumbering(NumberingOrder.Pile, n => n.Order, (n, v) => n.Order = v);
            OnPropertyChanged(nameof(NumberingBySheet));
        }
    }

    public bool NumberingBySheet
    {
        get => _numbering.Order == NumberingOrder.BySheet;
        set
        {
            if (!value) return;
            SetNumbering(NumberingOrder.BySheet, n => n.Order, (n, v) => n.Order = v);
            OnPropertyChanged(nameof(NumberingInPile));
        }
    }

    public double NumberX1 { get => _numbering.X1; set => SetNumbering(Math.Clamp(value, 0, 3000), n => n.X1, (n, v) => n.X1 = v); }
    public double NumberY1 { get => _numbering.Y1; set => SetNumbering(Math.Clamp(value, 0, 3000), n => n.Y1, (n, v) => n.Y1 = v); }
    public bool NumberingSecond { get => _numbering.Second; set => SetNumbering(value, n => n.Second, (n, v) => n.Second = v); }
    public double NumberX2 { get => _numbering.X2; set => SetNumbering(Math.Clamp(value, 0, 3000), n => n.X2, (n, v) => n.X2 = v); }
    public double NumberY2 { get => _numbering.Y2; set => SetNumbering(Math.Clamp(value, 0, 3000), n => n.Y2, (n, v) => n.Y2 = v); }
    public double NumberSizeMm { get => _numbering.SizeMm; set => SetNumbering(Math.Clamp(value, 0.5, 200), n => n.SizeMm, (n, v) => n.SizeMm = v); }

    /// <summary>Across, up, upside down, down.</summary>
    public IReadOnlyList<NumberDirectionOption> NumberDirections { get; }

    public NumberDirectionOption NumberDirection1
    {
        get => NumberDirections.First(d => d.Value == _numbering.Direction1);
        set { if (value is not null) SetNumbering(value.Value, n => n.Direction1, (n, v) => n.Direction1 = v); }
    }

    public NumberDirectionOption NumberDirection2
    {
        get => NumberDirections.First(d => d.Value == _numbering.Direction2);
        set { if (value is not null) SetNumbering(value.Value, n => n.Direction2, (n, v) => n.Direction2 = v); }
    }

    /// <summary>The font of the numbers, among the TrueType fonts of the PC (those of the captions); Segoe UI by default.</summary>
    public FontChoice? SelectedNumberFont
    {
        get => CaptionFonts.FirstOrDefault(f => string.Equals(f.Name, _numbering.Font, StringComparison.OrdinalIgnoreCase))
               ?? CaptionFonts.FirstOrDefault(f => f.Name == NumberingSettings.DefaultFont) ?? CaptionFonts.FirstOrDefault();
        set { if (value is not null) SetNumbering(value.Name, n => n.Font, (n, v) => n.Font = v); }
    }

    public IReadOnlyList<NumberColorOption> NumberColors { get; }

    public NumberColorOption SelectedNumberColor
    {
        get => NumberColors.First(c => c.Value == _numbering.Color);
        set { if (value?.Value is { } color) SetNumbering(color, n => n.Color, (n, v) => n.Color = v); }
    }

    /// <summary>None, then the colours.</summary>
    public IReadOnlyList<NumberColorOption> NumberBackgrounds { get; }

    public NumberColorOption SelectedNumberBackground
    {
        get => _numbering.HasBackground ? NumberBackgrounds.First(c => c.Value == _numbering.Background) : NumberBackgrounds[0];
        set
        {
            if (value is null) return;
            bool has = value.Value is not null;
            var color = value.Value ?? _numbering.Background;
            if (has == _numbering.HasBackground && color == _numbering.Background) return;
            (_numbering.HasBackground, _numbering.Background) = (has, color);
            OnPropertyChanged();
            Changed();
        }
    }

    // How the numbers are written now.
    private NumberingStyle? NumberingStyleNow()
    {
        if (!_numbering.Enabled || _mode == LayoutMode.Mixed || SelectedNumberFont is not { } font) return null;
        List<NumberPlace> places = [new(_numbering.X1, _numbering.Y1, _numbering.Direction1)];
        if (_numbering.Second) places.Add(new(_numbering.X2, _numbering.Y2, _numbering.Direction2));
        var qr = _numbering.Qr ? new QrStyle(_numbering.QrX, _numbering.QrY, _numbering.QrSizeMm, _numbering.QrAddressMode ? _numbering.QrAddress : null) : null;
        return new NumberingStyle(_numbering.Prefix, _numbering.Suffix, places, font.Name, font.File,
            _numbering.SizeMm, _numbering.Color, _numbering.HasBackground ? _numbering.Background : null, qr);
    }

    // The codes the visuals take, in order: those made for the settings as they are, or those of the list.
    private IReadOnlyList<string> CodesNow() => _numbering.Content switch
    {
        NumberingContent.GeneratedCodes when _codes.GeneratedFormat == CodeFormat() => _codes.Generated,
        NumberingContent.CodeList => _codes.List,
        _ => [],
    };

    // The rank and the number or code of each visual of every sheet laid: in a pile those of a sheet depend on all the
    // others (a sheet shown alone, too). Codes are taken from the first one; a visual past the last code has none.
    private (NumberingStyle Style, int?[][] Ranks, string?[][] Values)? NumbersOfPlan()
    {
        if (NumberingStyleNow() is not { } style) return null;
        bool numbers = _numbering.Content == NumberingContent.Numbers;
        var ranks = Numbering.Assign(Plan.Sheets, _numbering.Order, numbers ? _numbering.First : 0);
        var codes = CodesNow();
        int digits = _numbering.Digits;
        return (style, ranks, Numbering.Values(ranks, r => numbers ? Numbering.Number(r, digits) : r < codes.Count ? codes[r] : null));
    }

    // Visuals that get no code: more of them than codes in the list.
    private int VisualsWithoutCode() =>
        _numbering.Enabled && _mode != LayoutMode.Mixed && _numbering.Content == NumberingContent.CodeList && _codes.List.Count > 0
            ? Math.Max(0, NumberedVisuals() - _codes.List.Count)
            : 0;

    // "12 visuals without a code: the list has only 468."
    private static string WithoutCode(int visuals, int codes) => Strings.Count("VisualsWithoutCode", visuals) + Strings.Format("CodeListOnly", codes);

    /// <summary>"From N° 001 to N° 480 · piles of 30 sheets", "25 unique codes, from … to …".</summary>
    public string? NumberingSummary
    {
        get
        {
            if (NumberingStyleNow() is not { } style) return null;
            int count = NumberedVisuals();
            if (count == 0) return null;
            var codes = CodesNow();
            string? text = _numbering.Content switch
            {
                NumberingContent.Numbers => Strings.Format("NumberingRange", style.Text(Numbering.Number(_numbering.First, _numbering.Digits)),
                    style.Text(Numbering.Number(_numbering.First + count - 1, _numbering.Digits))),
                NumberingContent.GeneratedCodes when _codesTooFew => Strings.Format("CodesTooFew", _numbering.CodeLength),
                NumberingContent.GeneratedCodes when codes.Count >= count =>
                    Strings.Count("UniqueCodes", count) + Strings.Format("CodesFromTo", style.Text(codes[0]), style.Text(codes[count - 1])),
                NumberingContent.CodeList when !HasCodeList || codes.Count == 0 => Strings.Get("CodeListNone"),
                NumberingContent.CodeList when codes.Count < count => WithoutCode(count - codes.Count, codes.Count),
                NumberingContent.CodeList => Strings.Format("CodeListRange", style.Text(codes[0]), style.Text(codes[count - 1])) +
                                             (codes.Count > count ? Strings.Count("CodeListLeft", codes.Count - count) : ""),
                _ => null,
            };
            if (text is not null && _numbering.Order == NumberingOrder.Pile && Plan.SheetCount > 1)
                text += Strings.Format("NumberingPiles", Plan.SheetCount);
            // An address without the code: the same QR code on every visual.
            if (_numbering.Qr && _numbering.QrAddressMode && !_numbering.QrAddress.Contains(QrStyle.CodeField, StringComparison.Ordinal))
                text = (text is null ? "" : text + "\n") + Strings.Get("QrAddressWithoutCode");
            return text;
        }
    }

    // ---- The text in the band of framed sizes (a Polaroid, an Instax), asked by the author ----

    private string _captionText = "";

    /// <summary>The text common to every photo; a line may have its own. It belongs to the job (undo, projects), not to the settings.</summary>
    public string CaptionText { get => _captionText; set { if (Set(ref _captionText, value ?? "")) Changed(); } }

    /// <summary>The TrueType fonts of the PC, the handwriting ones of Windows first.</summary>
    public IReadOnlyList<FontChoice> CaptionFonts { get; }

    private FontChoice? _captionFont;
    public FontChoice? SelectedCaptionFont { get => _captionFont; set { if (value is not null && Set(ref _captionFont, value)) Changed(); } }

    public IReadOnlyList<CaptionColorOption> CaptionColors { get; }

    private CaptionColor _captionColor;
    public CaptionColorOption SelectedCaptionColor
    {
        get => CaptionColors.First(c => c.Value == _captionColor);
        set
        {
            if (value is null || value.Value == _captionColor) return;
            _captionColor = value.Value;
            OnPropertyChanged();
            Changed();
        }
    }

    /// <summary>A size laid has a band to write in: the "Text in the band" card shows.</summary>
    public bool ShowCaption => _mode == LayoutMode.Mixed
        ? Visuals.Any(v => MixedSizeOf(v) is { } size && HasBand(size.Margins))
        : HasBand(CurrentMargins());

    private static bool HasBand(MarginsMm m) => Math.Max(Math.Max(m.Top, m.Bottom), Math.Max(m.Left, m.Right)) >= Core.Pdf.Caption.MinBandMm;

    private FontChoice? FontOf(string? name) =>
        CaptionFonts.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) ?? CaptionFonts.FirstOrDefault();

    // The text written in the band of a line: its own, else the common one; none without a band.
    private string? CaptionOf(VisualItem line, bool band)
    {
        if (!band) return null;
        // The text of a line, else the common one. In the Repeat mode only the common one: a line carried over from a
        // list keeps its own text for when it goes back, but the Repeat mode has no field to show or change it.
        string text = _mode == LayoutMode.Repeat || string.IsNullOrWhiteSpace(line.Caption) ? CaptionText : line.Caption;
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    // The sides as the PDF draws them: the dpi, the cropping and the drags on the preview follow the mirrored bleed.
    private SideOptions FrontSide => SideOptions.Recto with { MirrorBleed = BleedEnabled && MirrorBleed };
    private SideOptions BackSide => new(true, _flip, RotateVerso180, BackHasMargins, BleedEnabled && MirrorBleed);

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

    // ---- The line of the notices under the preview (asked by the author: a list of notices changed the height of the
    // preview with each change). One line of a fixed height: the icon of the worst, how many and their key words, or the
    // notice itself when alone; a click shows them all over the bottom of the preview.

    public bool HasNotices => Notices.Count > 0;
    public NoticeLevel WorstNoticeLevel => Notices.Count == 0 ? NoticeLevel.Info : Notices.Max(n => n.Level);
    public string WorstNoticeIcon => Notice.IconOf(WorstNoticeLevel);

    private bool _singleNoticeFits = true;

    /// <summary>
    /// A notice alone fits its line (measured by the view, which knows the width): it is then the line itself; too
    /// long, it is summed up as several are (the author's remark).
    /// </summary>
    public bool SingleNoticeFits
    {
        get => _singleNoticeFits;
        set
        {
            if (!Set(ref _singleNoticeFits, value)) return;
            foreach (var name in new[] { nameof(NoticeTitle), nameof(NoticeDetail), nameof(CanOpenNotices), nameof(NoticeBarTooltip) })
                OnPropertyChanged(name);
            if (!CanOpenNotices) NoticesOpen = false;
        }
    }

    // Summed up: "2 notices" and their key words, the details opened from the line.
    private bool NoticesSummed => Notices.Count > 1 || (Notices.Count == 1 && !_singleNoticeFits);

    /// <summary>"3 notices" and the space before their key words, or nothing when one fits the line (<see cref="NoticeDetail"/> is the notice).</summary>
    public string NoticeTitle => NoticesSummed ? Strings.Count("NoticesCount", Notices.Count) + "  " : "";

    /// <summary>Their key words ("Cropping · Back"), or the notice itself when alone and short enough.</summary>
    public string NoticeDetail => NoticesSummed
        ? string.Join(" · ", Notices.Select(n => n.Key).Distinct())
        : Notices.Count == 1 ? Notices[0].Text : "";

    /// <summary>
    /// Several notices open over the preview; a single one is the line itself, nothing to unfold (the author's remark):
    /// its whole text is its tooltip.
    /// </summary>
    public bool CanOpenNotices => NoticesSummed;

    public string NoticeBarTooltip => CanOpenNotices ? Strings.Get("NoticeBarTooltip") : NoticeDetail;

    private bool _noticesOpen;

    /// <summary>The notices shown in full, over the bottom of the preview.</summary>
    public bool NoticesOpen { get => _noticesOpen; set => Set(ref _noticesOpen, value && CanOpenNotices); }

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
        var side = front ? FrontSide : BackSide;
        for (int i = 0; i < layout.Cells.Count && i < visuals.Length; i++)
        {
            if (visuals[i] is null) continue;
            var cell = front ? layout.Cells[i] : Placement.MirrorForVerso(layout.Cells[i], layout.Sheet, _flip);
            var b = cell.Bleed;
            if (x < b.X || x > b.Right || y < b.Y || y > b.Bottom) continue;
            var line = front ? LineOfCell(PreviewSheetIndex, i) : BackOf(LineOfCell(PreviewSheetIndex, i));
            return line is null ? null : (line, cell, side);
        }
        return null;
    }

    // Every cell of the sheet shown holding this line, on the front, or this back, on the back.
    private List<LayoutCell> CellsOf(VisualItem line, bool front)
    {
        var cells = new List<LayoutCell>();
        if (Layout is not { } layout || PreviewSheetIndex >= Plan.SheetCount) return cells;
        var visuals = Plan.Sheets[PreviewSheetIndex];
        for (int i = 0; i < layout.Cells.Count && i < visuals.Length; i++)
        {
            if (visuals[i] is null) continue;
            if (front && LineOfCell(PreviewSheetIndex, i) == line) cells.Add(layout.Cells[i]);
            else if (!front && BackOf(LineOfCell(PreviewSheetIndex, i)) == line)
                cells.Add(Placement.MirrorForVerso(layout.Cells[i], layout.Sheet, _flip));
        }
        return cells;
    }

    /// <summary>
    /// What is printed behind a line when the layout is double-sided: its own back (lines of a list), else the shared
    /// one; null when single-sided.
    /// </summary>
    private VisualItem? BackOf(VisualItem? line) =>
        !VersoEnabled ? null : (_mode != LayoutMode.Repeat ? line?.Back : null) ?? Verso;

    private VisualItem? LineOfCell(int sheet, int cell)
    {
        if (_mode == LayoutMode.Mixed)
            return sheet < _mixed.SheetCount && _mixed.Sheets[sheet].Items[cell] is var item && item < _mixedLines.Count ? _mixedLines[item] : null;
        return sheet < Plan.SheetCount && Plan.Sheets[sheet][cell] is { } info && _lineOfInfo.TryGetValue(info, out var line) ? line : null;
    }

    private static async Task LoadDragImageAsync(VisualItem line, PreviewSide view)
    {
        // The copies of this drag: another photo dragged before the picture is read has copies of its own.
        var overlays = view.Overlays.ToList();
        line.DragImage = await ThumbnailLoader.LoadRawAsync(line.SourceInfo, 1400);
        foreach (var overlay in overlays.Where(view.Overlays.Contains))
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
    public bool FlattenPdf { get => _flattenPdf; set { if (Set(ref _flattenPdf, value)) OptionChanged(); } }

    private bool _cuttingPlan;

    /// <summary>Mixed mode: writes the cutting plan (A4 PDF, to print on office paper) next to each export.</summary>
    public bool CuttingPlan { get => _cuttingPlan; set { if (Set(ref _cuttingPlan, value)) OptionChanged(); } }

    private bool _saveProject;

    /// <summary>
    /// Writes the project (its files and settings, a .PageComposeProject file) next to each export, to print it again later
    /// as it was: a double click opens it (asked by the author).
    /// </summary>
    public bool SaveProject { get => _saveProject; set { if (Set(ref _saveProject, value)) OptionChanged(); } }

    // An option of the export: nothing to lay out again.
    private void OptionChanged()
    {
        if (_restoring) return;
        ForgetPresetIfChanged();
        RememberLater();
    }

    private bool _sheetMark;

    /// <summary>
    /// The mark of each sheet, outside the block: the name of the exported file (the one offered, in the preview), the
    /// date and "sheet 2/5", to sort the sheets at the guillotine (asked by the author).
    /// </summary>
    public bool SheetMark { get => _sheetMark; set { if (Set(ref _sheetMark, value)) Changed(); } }

    /// <summary>Progress and result of the exports, shown over the preview.</summary>
    public ExportToast Toast { get; } = new();

    public string? LastExportFolder => _settings.LastExportFolder;

    /// <summary>Folder of the first visual, offered by default for the export.</summary>
    public string? FirstVisualFolder => ProjectFolder ?? Path.GetDirectoryName(ActiveVisuals().FirstOrDefault()?.Info.OriginalPath);

    /// <summary>Folder of the project file, offered first for its exports and for "Save as".</summary>
    public string? ProjectFolder => _document.Path is { } path ? Path.GetDirectoryName(path) : null;

    // ---- Undo, redo (Ctrl+Z, Ctrl+Y; asked by the author) ----
    // The whole job is taken HistoryDelayMs after the last change, so that typing a number is one step, and at most
    // HistoryLength steps are kept. A step taken back puts the same lines back, their thumbnails with them.

    // The project of each state goes with it: Ctrl+Z after "New" or after opening a project gives the job back with its file.
    private sealed record JobState(string SettingsJson, LayoutPreset Settings, int Quantity, LineState? Repeat, LineState? SharedBack,
        IReadOnlyList<LineState> Lines, string CaptionText, JobCodes Codes, ProjectDocument Doc)
    {
        public bool SameAs(JobState other) => SettingsJson == other.SettingsJson && Quantity == other.Quantity && CaptionText == other.CaptionText &&
                                              Repeat == other.Repeat && SharedBack == other.SharedBack && Lines.SequenceEqual(other.Lines) &&
                                              Codes.SameAs(other.Codes) && ReferenceEquals(Doc, other.Doc);
    }

    private const int HistoryDelayMs = 600, HistoryLength = 100;
    private readonly DispatcherTimer _historyDebounce;
    private readonly List<JobState> _undo = [], _redo = [];
    private JobState _current;
    private bool _restoring;

    private JobState CaptureState()
    {
        var settings = CapturePreset("");
        return new JobState(System.Text.Json.JsonSerializer.Serialize(settings), settings, Quantity, RepeatVisual?.Capture(),
            Verso?.Capture(), [.. Visuals.Select(v => v.Capture())], CaptionText, _codes, _document);
    }

    private void RememberLater()
    {
        _historyDebounce.Stop();
        _historyDebounce.Start();
    }

    private void Remember()
    {
        _historyDebounce.Stop();
        var now = CaptureState();
        if (now.SameAs(_current)) return;
        _undo.Add(_current);
        if (_undo.Count > HistoryLength) _undo.RemoveAt(0);
        _redo.Clear();
        _current = now;
        DocumentChanged();
    }

    /// <summary>Ctrl+Z: the job as it was before the last change; false when there is none.</summary>
    public bool Undo() => Step(_undo, _redo);

    /// <summary>Ctrl+Y: the change undone last, made again.</summary>
    public bool Redo() => Step(_redo, _undo);

    private bool Step(List<JobState> from, List<JobState> to)
    {
        if (_historyDebounce.IsEnabled) Remember(); // a change still waiting is a step of its own
        if (from.Count == 0) return false;
        to.Add(_current);
        _current = from[^1];
        from.RemoveAt(from.Count - 1);
        RestoreState(_current);
        return true;
    }

    private void RestoreState(JobState state)
    {
        _restoring = true;
        try
        {
            ApplyPresetValues(state.Settings);
            _quantity = state.Quantity;
            _captionText = state.CaptionText;
            _codes = state.Codes;
            Visuals.Clear();
            foreach (var line in state.Lines)
            {
                line.Item.Restore(line);
                Visuals.Add(line.Item);
            }
            state.Repeat?.Item.Restore(state.Repeat);
            state.SharedBack?.Item.Restore(state.SharedBack);
            (_repeatVisual, _verso) = (state.Repeat?.Item, state.SharedBack?.Item);
            _document = state.Doc;
        }
        finally
        {
            _restoring = false;
        }
        DocumentChanged();

        foreach (var name in new[] { nameof(Quantity), nameof(RepeatVisual), nameof(HasRepeatVisual), nameof(Verso), nameof(HasVerso), nameof(CaptionText) })
            OnPropertyChanged(name);
        foreach (var item in AllItems().Where(i => i.ThumbnailOutdated))
        {
            item.ThumbnailOutdated = false;
            _ = LoadThumbnailAsync(item);
        }
        ForgetPresetIfChanged();
        Recompute();
        SchedulePreview();
    }

    // Every visual of the job, backs included.
    private IEnumerable<VisualItem> AllItems() => new[] { RepeatVisual, Verso }.Concat(Visuals).OfType<VisualItem>()
        .SelectMany(i => i.Back is { } back ? new[] { i, back } : [i]);

    // ---- Projects (.PageComposeProject) ----
    // The job is a document, as in Word or Paint (asked by the author): "Untitled" until it is saved or opened, then its
    // file, saved again by Ctrl+S. Its name is in the title bar, with a dot while changes are not saved.

    /// <summary>The project file of the job, and its state when it was saved or opened (shared by its undo steps).</summary>
    private sealed class ProjectDocument
    {
        public string? Path;
        public JobState? Saved;

        /// <summary>Files of the project that could not be opened (a HEIC without its codec, a damaged file): still in it.</summary>
        public int Missing;

        /// <summary>Read back from the last job with changes not saved in its project (its saved state is not known then).</summary>
        public bool Dirty;
    }

    /// <summary>
    /// Asked before the job is saved into the file of its project while files of it could not be opened (their number):
    /// it would be written without them. False: not saved.
    /// </summary>
    public Func<int, bool>? ConfirmDropMissing { get; set; }

    private ProjectDocument _document = new();

    /// <summary>The file of the job; null while it has never been saved nor opened.</summary>
    public string? ProjectPath => _document.Path;

    public string ProjectTitle => _document.Path is { } path ? Path.GetFileNameWithoutExtension(path) : Strings.Get("UntitledProject");

    /// <summary>
    /// A project saved or opened has changed since: the dot in the title bar, and the question on closing. A job never
    /// saved has no file to save into: it closes as it always did (the author's choice).
    /// </summary>
    public bool IsModified => _document.Path is not null && (_document.Saved is { } saved ? !_current.SameAs(saved) : _document.Dirty);

    /// <summary>In the title bar of the window (drawn by the app) and the taskbar.</summary>
    public string ProjectCaption => ProjectTitle + (IsModified ? " ●" : "");

    public string WindowTitle => Strings.Format("WindowTitle", ProjectCaption);

    /// <summary>"Save" has something to write: a visual, or a project already saved (emptied since).</summary>
    public bool CanSaveProject => HasAnyVisual || _document.Path is not null;

    private void DocumentChanged()
    {
        foreach (var name in new[] { nameof(ProjectPath), nameof(ProjectTitle), nameof(IsModified), nameof(ProjectCaption), nameof(WindowTitle), nameof(CanSaveProject) })
            OnPropertyChanged(name);
        // The job, or its document, has changed: the last job is written a moment later.
        _lastJobDebounce?.Stop();
        _lastJobDebounce?.Start();
    }

    /// <summary>The changes of the last moment counted (they are taken a moment after the last one): before closing, opening, saving.</summary>
    public bool HasUnsavedChanges()
    {
        if (_historyDebounce.IsEnabled) Remember();
        return IsModified;
    }

    private ProjectFile ToProjectFile(JobState state)
    {
        static ProjectLine Line(LineState s) => new()
        {
            File = s.Info.OriginalPath,
            Page = s.Info.PageNumber,
            Copies = s.Copies,
            Rotation = s.Rotation,
            CropX = s.CropX,
            CropY = s.CropY,
            FormatId = s.Format is null ? null : s.Format.Format?.Id ?? AppSettings.CustomFormatId,
            CustomWidth = s.CustomWidth,
            CustomHeight = s.CustomHeight,
            Back = s.Back is null ? null : Line(s.Back),
            BackAuto = s.BackAuto,
            Fit = s.Fit?.ToString(),
            Caption = s.Caption,
            Zoom = s.Zoom,
            CropTurn = s.Info.BaseRotation,
            CropMirror = s.Info.BaseMirror,
        };

        return new ProjectFile
        {
            Settings = state.Settings,
            Quantity = state.Quantity,
            Repeat = state.Repeat is null ? null : Line(state.Repeat),
            SharedBack = state.SharedBack is null ? null : Line(state.SharedBack),
            Visuals = [.. state.Lines.Select(Line)],
            CaptionText = state.CaptionText,
            Codes = [.. state.Codes.Generated],
            CodesFormat = state.Codes.GeneratedFormat,
            CodeList = [.. state.Codes.List],
            CodeListName = state.Codes.ListName,
        };
    }

    /// <summary>
    /// Opens a project (double click on a .PageComposeProject file, dropped on the window, File menu): it becomes the job
    /// of the window, in place of the one in progress, which Ctrl+Z gives back. Its files that cannot be read are left
    /// out, and said so.
    /// </summary>
    /// <exception cref="InvalidDataException">Not a PageCompose project, or a damaged one.</exception>
    public async Task OpenProjectAsync(string path)
    {
        var job = await Task.Run(() => ProjectStore.Open(path));
        int visuals = await LoadJobAsync(job, new ProjectDocument { Path = Path.GetFullPath(path) }, saved: true);
        RecentProjects.Add(path);
        Toast.ShowMessage(Strings.Get("ProjectOpened"), Strings.Format("ProjectOpenedDetail", Path.GetFileName(path), Strings.Count("Visuals", visuals)));
    }

    // A job read back (a project, the last job): its lines loaded (those that cannot be read left out, and said so),
    // then in place of the job in progress, which Ctrl+Z gives back. Saved: its document holds it as saved. Returns
    // its visuals.
    private async Task<int> LoadJobAsync(ProjectFile job, ProjectDocument document, bool saved)
    {
        var errors = new List<string>();
        int missing = 0;

        async Task<LineState?> Line(ProjectLine l, Func<VisualInfo, VisualItem> create)
        {
            var (info, _, error) = await LoadAsync(l.File, l.Page);
            if (error is not null) errors.Add(error);
            if (info is null)
            {
                missing++;
                return null;
            }
            var item = create(info);
            var back = l.Back is { } b ? await Line(b, i => CreateBackItem(item, i)) : null;
            var format = l.FormatId is null ? null : MixedFormats.FirstOrDefault(f => (f.Format?.Id ?? AppSettings.CustomFormatId) == l.FormatId);
            // The part kept, in the axes of the picture as it is now stored (converted or no longer, since it was saved).
            var (cropX, cropY) = l.CropTurn is { } turn && (turn != info.BaseRotation || l.CropMirror != info.BaseMirror)
                ? Placement.CropInOtherAxes(l.CropX, l.CropY, turn, l.CropMirror, info.BaseRotation, info.BaseMirror)
                : (l.CropX, l.CropY);
            return new LineState(item, info, Math.Clamp(l.Copies, 1, 100_000), (l.Rotation % 360 + 360) % 360 / 90 * 90, format,
                l.CustomWidth, l.CustomHeight, Math.Clamp(cropX, 0, 1), Math.Clamp(cropY, 0, 1), back, back is not null && l.BackAuto,
                Enum.TryParse<FitMode>(l.Fit, out var fit) ? fit : null, l.Caption ?? "", Math.Clamp(l.Zoom, 1, VisualInfo.MaxZoom));
        }

        var lines = new List<LineState>();
        foreach (var l in job.Visuals)
            if (await Line(l, i => CreateItem(i)) is { } line) lines.Add(line);
        var repeat = job.Repeat is { } r ? await Line(r, i => CreateItem(i, _ => RepeatVisual = null)) : null;
        var shared = job.SharedBack is { } sb ? await Line(sb, i => CreateItem(i, _ => Verso = null)) : null;

        if (_historyDebounce.IsEnabled) Remember();
        document.Missing = missing;
        var codes = new JobCodes(job.Codes ?? [], job.CodesFormat ?? "", job.CodeList ?? [], job.CodeListName ?? "");
        RestoreState(new JobState("", job.Settings, Math.Clamp(job.Quantity, 0, 100_000), repeat, shared, lines, job.CaptionText ?? "", codes, document));
        Remember(); // one step: Ctrl+Z gives the job before it back
        if (saved) document.Saved = _current;
        DocumentChanged();
        FilesMessage = errors.Count > 0 ? string.Join(" ", errors.Take(3)) : null;
        return lines.Count + (repeat is null ? 0 : 1);
    }

    // ---- The last job (asked by the author, as the session of Notepad) ----
    // Written a few seconds after each change and on closing (Services/LastJob). Reopened at start when Settings ->
    // General asks for it; a job never saved also shows at the top of the recent projects. An empty job never saved
    // writes nothing: a new window, or New, does not wipe the last job.

    private const int LastJobDelaySeconds = 3;
    private readonly DispatcherTimer _lastJobDebounce;
    private bool _writingLastJob;

    private LastJob.Saved? LastJobNow()
    {
        if (_historyDebounce.IsEnabled) Remember();
        if (!HasAnyVisual && _document.Path is null) return null;
        return new LastJob.Saved { Time = DateTime.Now, ProjectPath = _document.Path, Modified = IsModified, Job = ToProjectFile(_current) };
    }

    /// <summary>
    /// On closing (and at the end of the Windows session): the job as it is, at once. Changes not kept (the user answered
    /// "Don't save"): its project as saved.
    /// </summary>
    public void WriteLastJob(bool changesKept = true)
    {
        _lastJobDebounce.Stop();
        if (LastJobNow() is not { } job) return;
        if (!changesKept && job.ProjectPath is not null) job.Modified = false;
        LastJob.Write(job);
    }

    // A moment after a change: the job, and the picture of its first sheet for the recent projects (a job never saved).
    private async Task WriteLastJobAsync()
    {
        if (_writingLastJob)
        {
            _lastJobDebounce.Start(); // once this one is written
            return;
        }
        if (LastJobNow() is not { } job) return;
        _writingLastJob = true;
        try
        {
            ImpositionJob? first = null;
            if (job.ProjectPath is null && CanExport)
            {
                try { first = BuildJob([0]); }
                catch (Exception ex) when (ex is not OutOfMemoryException) { }
            }
            var preview = first is null ? null : await FirstSheetPngAsync(first);
            await Task.Run(() =>
            {
                LastJob.Write(job);
                if (job.ProjectPath is null) LastJob.WritePreview(preview);
            });
        }
        finally
        {
            _writingLastJob = false;
        }
    }

    /// <summary>
    /// The last job in place of the one in progress (Ctrl+Z gives it back): its project as saved, or the job with its
    /// changes, the project file left as it is until saved. False: there is none.
    /// </summary>
    public async Task<bool> RestoreLastJobAsync()
    {
        if (await Task.Run(LastJob.Read) is not { } last) return false;
        string? project = last.ProjectPath is { } path && File.Exists(path) ? path : null;
        if (project is not null && !last.Modified)
        {
            await OpenProjectAsync(project);
            return true;
        }
        if (project is not null)
        {
            // Its files taken out again where the job names them (they are kept a day).
            try { await Task.Run(() => ProjectStore.Open(project)); }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { }
        }
        int visuals = await LoadJobAsync(last.Job, new ProjectDocument { Path = project, Dirty = project is not null }, saved: false);
        Toast.ShowMessage(Strings.Get("LastJobResumed"), Strings.Count("Visuals", visuals));
        return true;
    }

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

        // A front and its back added together ("card-recto.pdf" and "card-verso.pdf", "front" / "back"): in the lists,
        // when the layout is double-sided, the back goes on the line of its front; one such pair in the Repeat mode is
        // its visual and the back, as a PDF of two pages.
        var pairs = BackPairs.Find(loaded.Select(v => v.OriginalPath))
            .Select(p => (Front: loaded.First(v => SamePath(v.OriginalPath, p.Front)), Back: loaded.First(v => SamePath(v.OriginalPath, p.Back))))
            .Where(p => p.Front.PageCount <= 2) // a back for each page of a longer PDF: not guessed
            .ToList();

        var messages = new List<string>(notes);
        if (_mode == LayoutMode.Repeat && loaded.Count == 2 && pairs.Count == 1)
        {
            RepeatVisual = CreateItem(pairs[0].Front, _ => RepeatVisual = null);
            Verso = CreateItem(pairs[0].Back, _ => Verso = null);
            VersoError = null;
            VersoEnabled = true;
            FilesMessage = notes.Count > 0 ? string.Join(" ", notes) : null;
            return;
        }

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

            // Double-sided: the backs added with their fronts go on their lines, and a PDF of two pages is a front and its
            // back; both show an "auto" badge. Otherwise they are visuals like the others.
            var backOf = new Dictionary<VisualInfo, VisualInfo>(ReferenceEqualityComparer.Instance);
            if (VersoEnabled)
            {
                foreach (var (front, back) in pairs) backOf[front] = back;
                loaded.RemoveAll(v => backOf.Values.Any(b => ReferenceEquals(b, v)));
            }
            bool TwoSided(VisualInfo v) => VersoEnabled && v.PageCount == 2 && !backOf.ContainsKey(v);

            // PDFs of several pages: all their pages (one line each) or one page, asked once for all of them; cancelled,
            // they are left out (the other files are added).
            var multiPage = loaded.Where(v => v.PageCount > 1 && !TwoSided(v) && !backOf.ContainsKey(v) && !InList(v)).ToList();
            var choice = multiPage.Count > 0 && AskPdfPages is { } ask
                ? ask([.. multiPage.Select(v => (v.FileName, v.PageCount))])
                : new PdfPagesChoice(false);

            int duplicates = 0;
            foreach (var v in loaded)
            {
                if (InList(v))
                {
                    duplicates++;
                    // Its back added again: on the line already there, if it has none.
                    if (backOf.TryGetValue(v, out var again) && Visuals.FirstOrDefault(i => SamePath(i.Info.OriginalPath, v.OriginalPath) &&
                            i.Info.PageNumber == v.PageNumber && !i.HasBack) is { } existing)
                        existing.InitBack(CreateBackItem(existing, again), auto: true);
                    continue;
                }
                if (v.PageCount < 2 || TwoSided(v) || backOf.ContainsKey(v))
                {
                    var line = CreateItem(v);
                    if (backOf.TryGetValue(v, out var back))
                        line.InitBack(CreateBackItem(line, back), auto: true);
                    else if (TwoSided(v))
                    {
                        var (page2, _, error) = await LoadAsync(v.OriginalPath, 2);
                        if (page2 is not null) line.InitBack(CreateBackItem(line, page2), auto: true);
                        if (error is not null) errors.Add(error);
                    }
                    Visuals.Add(line);
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

    /// <summary>The own back of a line of the list, dropped on its "Back" slot or chosen with it.</summary>
    public async Task SetLineBackAsync(VisualItem line, string path)
    {
        var (info, note, error) = await LoadAsync(path);
        FilesMessage = error ?? note;
        if (info is null || !Visuals.Contains(line)) return;
        line.Back = CreateBackItem(line, info);
    }

    // A back on its line: its close button takes it off the line (the shared back is then printed behind it).
    private VisualItem CreateBackItem(VisualItem line, VisualInfo info) => CreateItem(info, _ => line.Back = null);

    private static bool SamePath(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads a visual (a page of a PDF, from 1). A PDF that PDFsharp cannot read becomes an image rendered by Windows,
    /// with a note saying so.
    /// </summary>
    private static async Task<(VisualInfo? Info, string? Note, string? Error)> LoadAsync(string path, int pageNumber = 1)
    {
        if (VisualLoader.NeedsConversion(path))
            return await ConvertAsync(path);
        try
        {
            var info = VisualLoader.Load(path, pageNumber);
            // A photo in Adobe RGB, Display P3…: the PDF, the preview and the JPG export would ignore its profile and
            // show it dull, so Windows converts it to sRGB (an option of the settings, on by default; the setting is
            // the shared App.Settings, as this method is static).
            if (info.HasForeignProfile && App.Settings.ConvertToSrgb)
                return await ConvertAsync(path, info.Profile);
            return (info, null, null);
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
        catch (VisualLoadException ex) when (ex.Error == VisualLoadError.NeedsConversion)
        {
            return await ConvertAsync(path); // a JPEG the PDF cannot take as it is: decoded and saved again by Windows
        }
        catch (VisualLoadException ex)
        {
            return (null, null, LoadErrorMessage(ex));
        }
    }

    /// <summary>
    /// An iPhone HEIC, a WebP, a TIFF, or a photo with a colour <paramref name="profile"/> other than sRGB: decoded by
    /// Windows into a JPEG in sRGB, then read as any photo; the line keeps the name and the type of the file
    /// ("HEIC · 4032 × 3024 px"), and says from which profile it was converted.
    /// </summary>
    private static async Task<(VisualInfo? Info, string? Note, string? Error)> ConvertAsync(string path, ColorProfile? profile = null)
    {
        string name = Path.GetFileName(path);
        if (!File.Exists(path)) return (null, null, Strings.Format("LoadNotFound", path));
        try
        {
            string jpeg = await PhotoConverter.ConvertAsync(path);
            var info = VisualLoader.Load(jpeg) with { SourcePath = path };
            return (profile is null ? info : info with { Profile = profile, ProfileConverted = true }, null, null);
        }
        catch (VisualLoadException ex)
        {
            return (null, null, Strings.Format("LoadUnreadable", name, ex.Detail ?? ""));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Without its extensions (HEIF, and HEVC for the photos themselves), Windows cannot decode a HEIC at all.
            bool heic = Path.GetExtension(path).ToLowerInvariant() is ".heic" or ".heif";
            return (null, null, heic ? Strings.Format("LoadHeicCodec", name) : Strings.Format("LoadUnreadable", name, ex.Message.Trim()));
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
        if (_document.Path is { } project) return Path.GetFileNameWithoutExtension(project) + ".pdf"; // the name of its project
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

    /// <summary>
    /// Asked before an export replaces files that are already there besides the one chosen in the dialog (which Windows
    /// asked about): the pictures of a JPG export of several sheets, the cutting plan, the project. Given their paths;
    /// false: nothing is exported.
    /// </summary>
    public Func<IReadOnlyList<string>, bool>? ConfirmReplace { get; set; }

    // A file an export writes after its sheets, prepared when the export starts.
    private sealed record PendingFile(string Path, Func<Task> Write);

    private bool MayReplace(IEnumerable<string?> files, string chosen)
    {
        // The file chosen was there and Windows asked: replacing it replaces what goes with it (an export made again).
        if (File.Exists(chosen)) return true;
        var existing = files.OfType<string>()
            .Where(f => !string.Equals(Path.GetFullPath(f), Path.GetFullPath(chosen), StringComparison.OrdinalIgnoreCase) && File.Exists(f))
            .ToList();
        return existing.Count == 0 || ConfirmReplace?.Invoke(existing) != false;
    }

    /// <summary>
    /// The PDF of every sheet. Everything it writes is taken when it starts: a flattened export can take half a minute,
    /// and a change made meanwhile (a setting, New, another project opened) belongs to the next one. False: not done
    /// (files the user did not want replaced).
    /// </summary>
    public async Task<bool> ExportAsync(string path)
    {
        if (!CanExport) return false;
        var job = BuildJob(AllSheets(), Path.GetFileNameWithoutExtension(path));
        var sheetSize = Sheet.Size;
        int sheets = Plan.SheetCount;
        var plan = CuttingPlanFor(path);
        var codes = CodesFor(path);
        var project = SaveProject ? ProjectWriterFor(ProjectPathNextTo(path)) : null;
        if (!MayReplace([plan?.Path, codes?.Path, project?.Path], path)) return false;

        Toast.ShowProgress(Strings.Get("Exporting"), Path.GetFileName(path));
        if (FlattenPdf)
        {
            // Each side rendered as a 300 dpi picture, then gathered into the PDF: nothing left for the RIP to interpret.
            string folder = Path.Combine(Path.GetTempPath(), "PageCompose", $"Flat-{Guid.NewGuid():N}");
            Directory.CreateDirectory(folder);
            try
            {
                var pages = Enumerable.Range(1, PageCount(job)).Select(i => Path.Combine(folder, $"{i}.jpg")).ToList();
                await RenderPagesAsync(job, sheetSize, pages);
                await Task.Run(() => SafeFile.Write(path, fs => FlatPdfWriter.Write(pages, sheetSize, fs, job.Title)));
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

        if (plan is not null) await plan.Write();
        if (codes is not null) await codes.Write();
        if (project is not null) await project.Write();
        Toast.ShowDone(Strings.Get("ToastPdfSaved"),
            Strings.Format("ToastPdfDetail", Path.GetFileName(path), Strings.Count("Sheets", sheets)) +
            (plan is null ? "" : Strings.Get("ToastWithPlan")) + (codes is null ? "" : Strings.Get("ToastWithCodes")) +
            (project is null ? "" : Strings.Get("ToastWithProject")),
            plan is null ? [path] : [path, plan.Path]);
        _settings.LastExportFolder = Path.GetDirectoryName(path);
        SaveSettings();
        return true;
    }

    /// <summary>There is something to save as a project: a visual, in the list or in the Repeat mode.</summary>
    public bool HasAnyVisual => ActiveVisuals().Any();

    /// <summary>
    /// "Save", "Save as" (File menu): the project to <paramref name="path"/>, which becomes the file of the job. False:
    /// not saved (files of the project that could not be opened, kept at the user's request).
    /// </summary>
    public async Task<bool> SaveProjectAsync(string path)
    {
        if (ProjectWriterFor(path) is not { } project) return false;
        Toast.ShowProgress(Strings.Get("SavingProject"), Path.GetFileName(path));
        await project.Write();
        Toast.ShowDone(Strings.Get("ToastProjectSaved"), Path.GetFileName(path), [path]);
        return true;
    }

    // The project written next to an export when asked: "name.PageComposeProject".
    private static string ProjectPathNextTo(string exportPath) =>
        Path.Combine(Path.GetDirectoryName(exportPath) ?? "", Path.GetFileNameWithoutExtension(exportPath) + ProjectStore.FileExtension);

    // The job as it is now, to be written into a project, which then becomes its file. The state and its document are
    // taken at once: a save that ends after New or after another project was opened marks the project it wrote, never
    // the one open by then. A change made while it is written is a change still to save. Null: the project has files
    // that could not be opened, and the user keeps its file as it is rather than write it without them.
    private PendingFile? ProjectWriterFor(string path)
    {
        if (_historyDebounce.IsEnabled) Remember();
        var state = _current;
        var doc = state.Doc;
        if (doc.Missing > 0 && doc.Path is { } own && string.Equals(Path.GetFullPath(path), own, StringComparison.OrdinalIgnoreCase)
            && ConfirmDropMissing?.Invoke(doc.Missing) == false)
            return null;
        var file = ToProjectFile(state);
        ImpositionJob? first = null;
        try { if (CanExport) first = BuildJob([0]); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { } // no picture of the first sheet, the project all the same
        return new PendingFile(path, async () =>
        {
            var preview = await FirstSheetPngAsync(first);
            await Task.Run(() => ProjectStore.Save(path, file, preview));
            state.Doc.Path = Path.GetFullPath(path);
            state.Doc.Saved = state;
            state.Doc.Missing = 0; // its file is now the job as it is
            state.Doc.Dirty = false;
            DocumentChanged();
            RecentProjects.Add(path);
        });
    }

    // The first sheet in small (its front), for the recent projects; none when nothing is laid or it cannot be drawn.
    private async Task<byte[]?> FirstSheetPngAsync(ImpositionJob? first)
    {
        if (first is null) return null;
        try
        {
            var job = await LightenAsync(first, PreviewWidthPx); // the copies of the preview, already made
            var pdf = await Task.Run(() =>
            {
                using var ms = new MemoryStream();
                ImpositionPdfWriter.Write(job, ms);
                return ms.ToArray();
            });
            var picture = await PdfPreviewRenderer.RenderAsync(pdf, 0, ProjectPreviewPx);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(picture));
            using var png = new MemoryStream();
            encoder.Save(png);
            return png.ToArray();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null; // whatever went wrong with the picture, the project is saved all the same
        }
    }

    /// <summary>
    /// "New" (Ctrl+N): an empty job in this window, "Untitled", with the same settings: a job often looks like the one
    /// before (the author's choice). Ctrl+Z gives the job back, with its file.
    /// </summary>
    public void NewProject()
    {
        if (_historyDebounce.IsEnabled) Remember();
        RestoreState(_current with { Repeat = null, SharedBack = null, Lines = [], CaptionText = "", Codes = JobCodes.None, Doc = new ProjectDocument() });
        Remember();
        FilesMessage = null;
        VersoError = null;
    }

    // ---- Recent projects (File menu) ----

    public ObservableCollection<RecentProjectItem> RecentProjectList { get; } = [];

    public bool HasRecentProjects => RecentProjectList.Count > 0;

    /// <summary>Read again each time the menu opens: another window may have saved a project meanwhile.</summary>
    public void ReloadRecentProjects()
    {
        RecentProjectList.Clear();
        // The last job never saved, unless it is the one in this window (a job never saved holding visuals).
        if (LastJob.Read() is { ProjectPath: null, Visuals: > 0 } last && !(_document.Path is null && HasAnyVisual))
        {
            var item = RecentProjectItem.ForLastJob(last.Time, last.Visuals);
            RecentProjectList.Add(item);
            _ = item.LoadPreviewAsync();
        }
        foreach (var file in RecentProjects.Load())
        {
            var item = new RecentProjectItem(file);
            RecentProjectList.Add(item);
            _ = item.LoadPreviewAsync();
        }
        OnPropertyChanged(nameof(HasRecentProjects));
        _ = DropMissingRecentProjectsAsync([.. RecentProjectList.Where(i => !i.IsLastJob)]);
    }

    // A project no longer there drops out of the list. Looked for away from the window: a share that is off makes
    // Windows wait for its time-out, and the File menu used to wait with it.
    private async Task DropMissingRecentProjectsAsync(List<RecentProjectItem> items)
    {
        var missing = await Task.Run(() => items.Where(i => !File.Exists(i.Path)).ToList());
        foreach (var item in missing)
            RecentProjectList.Remove(item);
        if (missing.Count > 0) OnPropertyChanged(nameof(HasRecentProjects));
    }

    public void ForgetRecentProject(string path)
    {
        RecentProjects.Remove(path);
        ReloadRecentProjects();
    }

    public void ClearRecentProjects()
    {
        RecentProjects.Clear();
        LastJob.Clear();
        ReloadRecentProjects();
    }

    /// <summary>
    /// In the Mixed mode, when asked: the cutting plan next to the export ("name-cutting-plan.pdf"), to print on
    /// office paper and keep by the guillotine; null when there is none. Taken when the export starts, as its sheets.
    /// </summary>
    private PendingFile? CuttingPlanFor(string exportPath)
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
        return new PendingFile(path, () => Task.Run(() =>
            SafeFile.Write(path, fs => CuttingPlanWriter.Write(plan, labels, sheet, stem, texts, fs, fit, logo))));
    }

    /// <summary>
    /// With codes on the visuals: their list next to the export ("name-codes.csv"), for the till or the website that
    /// takes them, in the separator of the PC's lists (that of Excel); null otherwise. Taken when the export starts.
    /// </summary>
    private PendingFile? CodesFor(string exportPath)
    {
        if (_numbering.Content == NumberingContent.Numbers || NumbersOfPlan() is not { } numbering) return null;
        if (!numbering.Values.Any(sheet => sheet.Any(v => v is not null))) return null; // no list chosen yet
        string stem = Path.GetFileNameWithoutExtension(exportPath);
        string path = Path.Combine(Path.GetDirectoryName(exportPath) ?? "", Strings.Format("CodesFileName", stem) + ".csv");
        string csv = Numbering.CodesCsv(numbering.Ranks, numbering.Values,
            [Strings.Get("CodesColumnSheet"), Strings.Get("CodesColumnCell"), Strings.Get("CodesColumnCode")],
            System.Globalization.CultureInfo.CurrentCulture.TextInfo.ListSeparator);
        return new PendingFile(path, () => Task.Run(() => SafeFile.Write(path, fs =>
        {
            using var writer = new StreamWriter(fs, new System.Text.UTF8Encoding(true), leaveOpen: true); // with its BOM, for Excel
            writer.Write(csv);
        })));
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
        var job = BuildJob(AllSheets(), Path.GetFileNameWithoutExtension(path));
        var sheetSize = Sheet.Size;
        int count = PageCount(job);
        string folder = Path.GetDirectoryName(path) ?? "", stem = Path.GetFileNameWithoutExtension(path);
        List<string> files = count == 1
            ? [path]
            : Enumerable.Range(0, count).Select(i =>
            {
                int sheet = (job.HasBack ? i / 2 : i) + 1;
                bool back = job.HasBack && i % 2 == 1;
                return Path.Combine(folder, Strings.Format(back ? "JpegPageBack" : "JpegPageFront", stem, sheet) + ".jpg");
            }).ToList();

        // As for the PDF, everything taken now (see ExportAsync).
        var plan = CuttingPlanFor(path);
        var codes = CodesFor(path);
        var project = SaveProject ? ProjectWriterFor(ProjectPathNextTo(path)) : null;
        if (!MayReplace([.. files, plan?.Path, codes?.Path, project?.Path], path)) return [];

        Toast.ShowProgress(Strings.Get("Exporting"), Path.GetFileName(path));
        await RenderPagesAsync(job, sheetSize, files);
        if (plan is not null) await plan.Write();
        if (codes is not null) await codes.Write();
        if (project is not null) await project.Write();
        Toast.ShowDone(Strings.Count("JpegSaved", files.Count),
            (files.Count == 1 ? Path.GetFileName(path) : Strings.Format("ToastInFolder", Path.GetFileName(folder))) +
            (plan is null ? "" : Strings.Get("ToastWithPlan")) + (codes is null ? "" : Strings.Get("ToastWithCodes")) +
            (project is null ? "" : Strings.Get("ToastWithProject")),
            plan is null ? files : [.. files, plan.Path]);
        _settings.LastExportFolder = folder;
        SaveSettings();
        return files;
    }

    // Front, then back when there is one, for every sheet: as in the PDF.
    private static int PageCount(ImpositionJob job) => job.Sheets.Count * (job.HasBack ? 2 : 1);

    private async Task RenderPagesAsync(ImpositionJob job, SizeMm sheetSize, IReadOnlyList<string> files)
    {
        var pdf = await Task.Run(() =>
        {
            using var ms = new MemoryStream();
            ImpositionPdfWriter.Write(job, ms);
            return ms.ToArray();
        });
        var progress = new Progress<int>(done => Toast.Detail = Strings.Format("ExportingPage", Math.Min(done + 1, files.Count), files.Count));
        Toast.Detail = Strings.Format("ExportingPage", 1, files.Count);
        await SheetExporter.RenderPagesAsync(pdf, sheetSize, files, progress);
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
        _settings.FoldMarks = FoldMarks;
        _settings.CutLines = CutLines;
        _settings.MixedCutLines = MixedCutLines;
        _settings.BleedEnabled = BleedEnabled;
        _settings.MirrorBleed = MirrorBleed;
        _settings.CaptionFont = SelectedCaptionFont?.Name ?? FontList.DefaultFont;
        _settings.CaptionColor = _captionColor;
        _settings.Numbering = _numbering.Copy();
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
        _settings.SheetMark = SheetMark;
        _settings.SaveProject = SaveProject;
        _settings.CutLineColor = _cutLineColor;
        _settings.Save();
    }

    /// <summary>Recomputes every text after a language change (the new window reads the static ones itself).</summary>
    public void RefreshTexts()
    {
        FilesMessage = null;
        VersoError = null;
        OnPropertyChanged(nameof(VisualsDropHint));
        DocumentChanged(); // "Untitled" in the new language
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
        if (item.Back is { } back)
        {
            var copyBack = CreateBackItem(copy, back.SourceInfo);
            copyBack.CopyChoicesOf(back);
            copy.InitBack(copyBack, item.IsBackAuto);
        }
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

    // "Remove all": the lines only, the job keeps its project ("New" starts another one).
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
        carryOver &= !_restoring; // a state taken back has its own lists
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
            // Its own back is the back of the Repeat mode, when there was no shared one.
            if (_verso is null && Visuals[0].Back is { } back)
            {
                _verso = CreateItem(back.SourceInfo, _ => Verso = null);
                _verso.CopyChoicesOf(back);
                OnPropertyChanged(nameof(Verso));
                OnPropertyChanged(nameof(HasVerso));
            }
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

    // Margins read back (settings, a preset, an undo step, a project): those of the framed size chosen are its own, and
    // leaving that size gives no margins; any others are the user's, and stay when the size changes.
    private void TakeMarginsOfSize() =>
        _userMargins = _selectedFormat.Format is { Margins: { IsEmpty: false } preset } && _marginsEnabled && TypedMargins() == preset
            ? (false, MarginsMm.None)
            : null;

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
        if (_restoring) return; // recomputed once, at the end
        ForgetPresetIfChanged();
        Recompute();
        SchedulePreview();
        RememberLater();
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

    /// <summary>A setting of the preview changed in the Settings window ("Fast preview"): drawn again.</summary>
    public void RedrawPreview() => SchedulePreview();

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

        EnsureCodes();
        if (VisualsWithoutCode() is > 0 and var without)
            Notices.Add(new Notice(NoticeLevel.Warning, WithoutCode(without, _codes.List.Count), Strings.Get("NoticeKey_Codes")));
        else if (_codesTooFew)
            Notices.Add(new Notice(NoticeLevel.Warning, Strings.Format("CodesTooFew", _numbering.CodeLength), Strings.Get("NoticeKey_Codes")));
        PlanText = BuildPlanText();
        if (MarkHasNoRoom() && Plan.SheetCount > 0)
            Notices.Add(new Notice(NoticeLevel.Warning, Strings.Get("NoticeNoRoomForMark"), Strings.Get("NoticeKey_Mark")));
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
                     nameof(HasAnyVisual), nameof(CanSaveProject), nameof(ShowCutLineColor), nameof(ShowCopiesForAll), nameof(CopiesForAll), nameof(ShowFoldMarks),
                     nameof(ShowFormatForAll), nameof(FormatForAll), nameof(CanApplyFormatForAll),
                     nameof(ShowCaption), nameof(NumberingSummary), nameof(ShowNumbering), nameof(CodeExample), nameof(CodeListName),
                     nameof(HasCodeList), nameof(CodeListStatus),
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
            ? LayoutEngine.Compute(new LayoutOptions(Sheet, size, bleed, CropMarks, spacing, margins, SelectedFormat.Format?.Folds, FoldMarks))
            : null;
        int perSheet = Layout?.PerSheet ?? 0;

        // Each line gives its own VisualInfo object, so that a cell of the preview finds its line back (two lines may
        // hold equal ones: the same photo duplicated).
        _lineOfInfo.Clear();
        bool band = HasBand(margins);
        VisualInfo Own(VisualItem line)
        {
            line.HasCaptionBand = band && _mode != LayoutMode.Repeat;
            var info = line.Info with { Caption = CaptionOf(line, band) };
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
            Notices.Add(new Notice(NoticeLevel.Error, Details, Strings.Get("NoticeKey_Size")));
        }
        else if (!marginsFit)
        {
            Headline = Strings.Get("MarginsTooLarge");
            Details = Strings.Format("MarginsTooLargeDetails", MinSizeMm);
            Notices.Add(new Notice(NoticeLevel.Error, Details, Strings.Get("NoticeKey_Margins")));
        }
        else if (!sheetValid)
        {
            Headline = Strings.Get("InvalidSheet");
            Details = Strings.Format("InvalidSheetDetails", MinSheetMm, MaxSheetMm);
            Notices.Add(new Notice(NoticeLevel.Error, Details, Strings.Get("NoticeKey_Sheet")));
        }
        else if (Layout!.IsEmpty)
        {
            Headline = Strings.Get("DoesNotFit");
            Details = Strings.Format("DoesNotFitDetails", SelectedFormat.Name, SheetLabel);
            Notices.Add(new Notice(NoticeLevel.Error, Strings.Get("DoesNotFitNotice"), Strings.Get("NoticeKey_Size")));
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
                      (Layout.FoldMarks.Count > 0 ? Strings.Get("DetailsFoldMarks") : "") +
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
            v.HasCaptionBand = MixedSizeOf(v) is { } sized && HasBand(sized.Margins);
            if (MixedSizeOf(v) is { } size)
            {
                lines.Add(v);
                items.Add(new MixedItem(v.Info with { Caption = CaptionOf(v, v.HasCaptionBand) }, size.Size, v.Copies, size.Margins));
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
            Notices.Add(new Notice(NoticeLevel.Error, Details, Strings.Get("NoticeKey_Sheet")));
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
            Notices.Add(new Notice(NoticeLevel.Info, Strings.Format("NoticeWithoutCropMarksSheets", Strings.Count("Sheets", without)), Strings.Get("NoticeKey_CropMarks")));

        foreach (int i in _mixed.TooLarge)
        {
            lines[i].Quality = Strings.Get("QualityTooLarge");
            lines[i].IsLowQuality = true;
        }
        if (_mixed.TooLarge.Count > 0)
            Notices.Add(new Notice(NoticeLevel.Error, Strings.Format("NoticeTooLarge", List(_mixed.TooLarge.Select(i => lines[i].FileName)), SheetLabel), Strings.Get("NoticeKey_TooLarge")));

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
            Notices.Add(new Notice(NoticeLevel.Info, Strings.Get(_mode == LayoutMode.Repeat ? "NoticeAddVisual" : "NoticeAddVisuals"), Strings.Get("NoticeKey_Visuals")));
            return;
        }

        var lowDpi = new List<(string Name, int Dpi)>();
        var cropped = new List<(string Name, double Ratio)>();
        var ownBleed = new List<(string Name, double Mm)>();
        // Each back in the cell of its front; the shared one, when some visual has no back of its own, in the cell given.
        var versoSide = BackSide;
        bool ownBacks = VersoEnabled && _mode != LayoutMode.Repeat;
        foreach (var (item, cell) in fronts)
        {
            Assess(item, cell, FrontSide, lowDpi, cropped, ownBleed);
            if (ownBacks && item.Back is { } own)
                Assess(own, cell, versoSide, lowDpi, cropped, ownBleed);
        }
        int withoutBack = ownBacks ? ActiveVisuals().Count(v => !v.HasBack) : ActiveVisuals().Count();
        if (VersoEnabled && Verso is not null && back is not null && withoutBack > 0)
            Assess(Verso, back, versoSide, lowDpi, cropped, ownBleed);

        var critical = lowDpi.Where(d => d.Dpi < CriticalDpi).ToList();
        var low = lowDpi.Where(d => d.Dpi >= CriticalDpi).ToList();
        if (critical.Count > 0)
            Notices.Add(new Notice(NoticeLevel.Error, Strings.Format("NoticeDpiCritical", List(critical.Select(d => $"{d.Name} ({d.Dpi} dpi)")), MinDpi), Strings.Get("NoticeKey_Resolution")));
        if (low.Count > 0)
            Notices.Add(new Notice(NoticeLevel.Warning, Strings.Format("NoticeDpiLow", List(low.Select(d => $"{d.Name} ({d.Dpi} dpi)")), MinDpi), Strings.Get("NoticeKey_Resolution")));
        if (cropped.Count > 0)
            Notices.Add(new Notice(NoticeLevel.Warning, Strings.Format("NoticeCropped", List(cropped.Select(c => $"{c.Name} ({c.Ratio:P0})"))), Strings.Get("NoticeKey_Cropping")));
        foreach (var (name, mm) in ownBleed.Distinct())
        {
            Notices.Add(new Notice(NoticeLevel.Info, Strings.Format("NoticeOwnBleed", name, mm.ToString("0.#")), Strings.Get("NoticeKey_Bleed")));
            if (bleed > mm + 0.05)
                Notices.Add(new Notice(NoticeLevel.Warning,
                    Strings.Format("NoticeOwnBleedTooSmall", name, mm.ToString("0.#"), bleed.ToString("0.#")), Strings.Get("NoticeKey_Bleed")));
        }

        // No notice for what the settings column already says (asked by the author): the empty cells are in the
        // quantity summary, the "Fit" mode in its own label.
        if (VersoEnabled && Verso is null && withoutBack == ActiveVisuals().Count())
            Notices.Add(new Notice(NoticeLevel.Warning, Strings.Get("NoticeNoBack"), Strings.Get("NoticeKey_Back")));
        else if (VersoEnabled && Verso is null && withoutBack > 0)
            Notices.Add(new Notice(NoticeLevel.Warning, Strings.Count("NoticeWithoutBack", withoutBack), Strings.Get("NoticeKey_Back")));
    }

    private void Assess(VisualItem item, LayoutCell cell, SideOptions side,
        List<(string, int)> lowDpi, List<(string, double)> cropped, List<(string, double)> ownBleed)
    {
        var v = item.Info;
        var p = Placement.Compute(v, cell, _fit, side);

        if (double.IsNaN(p.EffectiveDpi))
        {
            item.Quality = Strings.Get("QualityPdf") + OwnFitText(item);
            item.IsLowQuality = false;
        }
        else
        {
            int dpi = (int)Math.Round(p.EffectiveDpi);
            item.Quality = Strings.Format("QualityDpi", dpi) + OwnFitText(item);
            item.IsLowQuality = dpi < MinDpi;
            if (dpi < MinDpi) lowDpi.Add((v.FileName, dpi));
        }

        if ((v.Fit ?? _fit) == FitMode.Fill)
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

    // " · Fitted": the line lies in its cell its own way (right click on the line).
    private static string OwnFitText(VisualItem item) => (item.IsZoomed ? Strings.Format("QualityZoom", item.ZoomPercent) : "") + item.Fit switch
    {
        FitMode.Fill => Strings.Get("QualityOwnFill"),
        FitMode.Fit => Strings.Get("QualityOwnFit"),
        _ => "",
    };

    // Crop marks need room around the block (none on a lab print): say how many fit without them, when that is more.
    private void AddCropMarksNotice(SheetLayout layout)
    {
        if (!layout.Options.CropMarks) return;
        int without = LayoutEngine.Compute(layout.Options with { CropMarks = false }).PerSheet;
        if (without > layout.PerSheet)
            Notices.Add(new Notice(NoticeLevel.Info, Strings.Format("NoticeWithoutCropMarks", without), Strings.Get("NoticeKey_CropMarks")));
    }

    // Each file once: the same file can be a front and the back, or be added twice.
    private static string List(IEnumerable<string> items)
    {
        var all = items.Distinct().ToList();
        return all.Count <= 3 ? string.Join(", ", all) : Strings.Format("ListAndMore", string.Join(", ", all.Take(3)), all.Count - 3);
    }

    private bool _previewRunning, _previewAgain;

    // A change was made while a render ran: its picture (or its error) is not shown, the next render comes at once. It
    // used to clear the copies drawn over a photo being dragged, which jumped back to its old part for a moment.
    private bool PreviewOutdated => _previewAgain || _previewDebounce.IsEnabled;

    // One render at a time (renders side by side slowed each other down): the changes made meanwhile are drawn once it
    // is over, in their last state only.
    private async Task RefreshPreviewAsync()
    {
        if (_previewRunning)
        {
            _previewAgain = true;
            return;
        }

        _previewRunning = true;
        try
        {
            do
            {
                _previewAgain = false;
                await RenderPreviewAsync();
            }
            while (_previewAgain);
        }
        finally
        {
            _previewRunning = false;
        }
    }

    private async Task RenderPreviewAsync()
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
        uint width = _previewWidthPx;
        IsPreviewBusy = true;
        try
        {
            job = await LightenAsync(job, width);
            var pdf = await Task.Run(() =>
            {
                using var ms = new MemoryStream();
                ImpositionPdfWriter.Write(job, ms);
                return ms.ToArray();
            });
            var document = await PdfPreviewRenderer.LoadAsync(pdf); // once for both sides
            var recto = await PdfPreviewRenderer.RenderPageAsync(document, 0, width, forScreen: true);
            var verso = job.HasBack ? await PdfPreviewRenderer.RenderPageAsync(document, 1, width, forScreen: true) : null;
            if (version == _previewVersion && !PreviewOutdated)
            {
                RectoSide.Image = recto;
                VersoSide.Image = verso;
                ClearOverlays();
            }
        }
        catch (Exception ex)
        {
            if (version == _previewVersion && !PreviewOutdated)
            {
                RectoSide.Image = VersoSide.Image = null;
                ClearOverlays();
                Notices.Add(new Notice(NoticeLevel.Error, Strings.Format("NoticePreviewFailed", ex.Message), Strings.Get("NoticeKey_Preview")));
            }
        }
        finally
        {
            if (version == _previewVersion && !PreviewOutdated) IsPreviewBusy = false;
        }
    }

    /// <summary>
    /// "Fast preview" (a setting, on by default): each photo of a job drawn on the screen replaced by a lighter copy of
    /// the size it is shown at <paramref name="widthPx"/> (<see cref="PreviewImages"/>). Only the preview and the
    /// picture of the recent projects come here: the exports take the job of <see cref="BuildJob"/> as it is, with the
    /// original files.
    /// </summary>
    private async Task<ImpositionJob> LightenAsync(ImpositionJob job, uint widthPx)
    {
        if (!_settings.LightPreview) return job;

        // Pixels needed on the long side of each photo: its cell at the scale of the rendering, half as much again for
        // a photo filled in a cell of other proportions (cropped, it is larger than its cell).
        double pxPerMm = widthPx / job.Layout.Sheet.Width;
        var needed = new Dictionary<VisualInfo, double>();
        void Need(VisualInfo? visual, LayoutCell cell)
        {
            if (visual is null) return;
            double px = Math.Max(cell.Bleed.Width, cell.Bleed.Height) * pxPerMm * 1.5 * Math.Clamp(visual.Zoom, 1, VisualInfo.MaxZoom);
            needed[visual] = Math.Max(needed.GetValueOrDefault(visual), px);
        }
        for (int s = 0; s < job.Sheets.Count; s++)
        {
            var cells = job.LayoutOf(s).Cells;
            for (int c = 0; c < cells.Count; c++)
            {
                if (job.Sheets[s][c] is not { } front) continue;
                Need(front, cells[c]);
                Need(job.BackOf(s, c), cells[c]);
            }
        }

        var copies = await Task.WhenAll(needed.Select(async n => (Visual: n.Key, Copy: await PreviewImages.GetAsync(n.Key, n.Value))));
        var light = copies.Where(c => c.Copy is not null).ToDictionary(c => c.Visual, c => c.Visual with { Path = c.Copy! });
        if (light.Count == 0) return job;
        VisualInfo? Light(VisualInfo? v) => v is not null && light.TryGetValue(v, out var copy) ? copy : v;
        return job with
        {
            Sheets = [.. job.Sheets.Select(sheet => sheet.Select(Light).ToArray())],
            Backs = job.Backs is null ? null : [.. job.Backs.Select(sheet => sheet.Select(Light).ToArray())],
            Verso = Light(job.Verso),
        };
    }

    /// <param name="sheets">Indices of the sheets, from 0.</param>
    /// <param name="jobName">Name of the exported file, in the marks of the sheets; the one offered for the preview.</param>
    private ImpositionJob BuildJob(IReadOnlyList<int> sheets, string? jobName = null)
    {
        var numbering = NumbersOfPlan();
        // Mixed sheets each have their own layout, and their own cut lines setting (on by default).
        var job = _mode == LayoutMode.Mixed
            ? ImpositionJob.Mixed(_mixed, sheets) with { Title = Strings.Get("PdfTitleMixed"), CutLines = MixedCutLines }
            : new ImpositionJob(Layout!, [.. sheets.Select(i => Plan.Sheets[i])], CutLines: CutLines,
                Title: _mode == LayoutMode.Repeat ? RepeatVisual?.Info.FileName : Strings.Get("PdfTitleCompose"));
        // The own backs of the lines, cell by cell (the others get the shared back).
        bool ownBacks = VersoEnabled && _mode != LayoutMode.Repeat && Visuals.Any(v => v.HasBack);
        return job with
        {
            Verso = VersoEnabled ? Verso?.Info : null,
            Backs = ownBacks
                ? [.. sheets.Select((s, k) => Enumerable.Range(0, job.Sheets[k].Length).Select(c => LineOfCell(s, c)?.Back?.Info).ToArray())]
                : null,
            Marks = SheetMark ? MarksOf(sheets, jobName ?? Path.GetFileNameWithoutExtension(SuggestedFileName())) : null,
            CutLineColor = _cutLineColor,
            MirrorBleed = BleedEnabled && MirrorBleed,
            Caption = SelectedCaptionFont is { } font ? new CaptionStyle(font.Name, font.File, _captionColor) : null,
            Numbering = numbering?.Style,
            Numbers = numbering is { } n ? [.. sheets.Select(i => n.Values[i])] : null,
            Fit = _fit,
            Flip = _flip,
            RotateVerso180 = RotateVerso180,
            VersoMargins = BackHasMargins,
        };
    }

    private List<int> AllSheets() => [.. Enumerable.Range(0, Plan.SheetCount)];

    // "dupont-cards · 11/10/2026 · sheet 2/5"
    private List<string> MarksOf(IReadOnlyList<int> sheets, string jobName)
    {
        string date = DateTime.Now.ToString("d", System.Globalization.CultureInfo.CurrentCulture);
        return [.. sheets.Select(i => Strings.Format("SheetMarkText", jobName, date, i + 1, Plan.SheetCount))];
    }

    /// <summary>The mark is asked, but a sheet leaves no room for it outside the block (a lab print filled to its edges).</summary>
    private bool MarkHasNoRoom() => SheetMark && (_mode == LayoutMode.Mixed
        ? _mixed.Sheets.Any(s => Core.Pdf.SheetMark.Place(s.Layout) is null)
        : Layout is { IsEmpty: false } layout && Core.Pdf.SheetMark.Place(layout) is null);
}
