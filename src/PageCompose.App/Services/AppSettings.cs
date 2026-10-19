using System.Text.Json;
using System.Text.Json.Serialization;
using PageCompose.App.ViewModels;
using PageCompose.Core;
using PageCompose.Core.Layout;
using PageCompose.Core.Pdf;

namespace PageCompose.App.Services;

/// <summary>Light or dark appearance of the app.</summary>
public enum AppTheme
{
    /// <summary>Follow the Windows setting ("Choose your mode").</summary>
    System,
    Light,
    Dark,
}

/// <summary>User preferences, saved in %LOCALAPPDATA%\PageCompose\settings.json.</summary>
public sealed class AppSettings
{
    /// <summary>Follow the Windows display language.</summary>
    public const string SystemLanguage = "system";

    /// <summary>UI languages with a translation (Properties/Strings*.resx). English is the default.</summary>
    public static readonly string[] SupportedLanguages = ["en", "fr"];

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PageCompose", "settings.json");

    /// <summary><see cref="SystemLanguage"/> or one of <see cref="SupportedLanguages"/>.</summary>
    public string Language { get; set; } = SystemLanguage;

    [JsonConverter(typeof(JsonStringEnumConverter<AppTheme>))]
    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>Ask GitHub for a newer version when the app starts.</summary>
    public bool CheckForUpdatesAtStartup { get; set; } = true;

    /// <summary>
    /// A photo whose colour profile is not sRGB (Adobe RGB, Display P3…) is converted to sRGB when it is added: else
    /// its colours are wrong in the PDF, the preview and the JPG export (on by default, the author's choice).
    /// </summary>
    public bool ConvertToSrgb { get; set; } = true;

    /// <summary>
    /// The preview draws lighter copies of the photos, of the size the screen shows (<see cref="PreviewImages"/>): about
    /// ten times faster with photos from a camera. Exports always take the original files (on by default, asked by the
    /// author).
    /// </summary>
    public bool LightPreview { get; set; } = true;

    /// <summary>Releases offered as updates: stable only, or betas too.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<UpdateChannel>))]
    public UpdateChannel UpdateChannel { get; set; } = UpdateChannel.Stable;

    [JsonConverter(typeof(JsonStringEnumConverter<LayoutMode>))]
    public LayoutMode Mode { get; set; } = LayoutMode.Repeat;

    /// <summary><see cref="FormatId"/> of a custom size (width and height below).</summary>
    public const string CustomFormatId = "Custom";

    /// <summary><see cref="Core.PaperFormat.Id"/> of the visual size, or <see cref="CustomFormatId"/>.</summary>
    public string? FormatId { get; set; }
    public double CustomWidth { get; set; } = 100;
    public double CustomHeight { get; set; } = 150;
    /// <summary><see cref="Core.PaperFormat.Id"/> of the sheet, or <see cref="CustomFormatId"/>.</summary>
    public string? SheetId { get; set; }
    public double CustomSheetWidth { get; set; } = 320;
    public double CustomSheetHeight { get; set; } = 450;

    /// <summary>Unprintable margin of a custom sheet: 4.1 mm on the ApeosPro, 0 for a lab print.</summary>
    public double CustomSheetMargin { get; set; } = Core.SheetFormats.PrinterMarginMm;
    public bool CropMarks { get; set; } = true;

    /// <summary>Folded cards: dashed marks in line with the folds, outside the block.</summary>
    public bool FoldMarks { get; set; } = true;
    public bool CutLines { get; set; }

    /// <summary>Cut lines along each cut in the Mixed mode (apart from those of the grids: on by default).</summary>
    public bool MixedCutLines { get; set; } = true;
    public bool BleedEnabled { get; set; }
    public double BleedMm { get; set; } = 2;

    /// <summary>The bleed of a photo takes its edges, mirrored, instead of enlarging it (off by default: the author's choice).</summary>
    public bool MirrorBleed { get; set; }

    /// <summary>Captions in the band of framed sizes: the font (a family name of the PC) and the colour. The text belongs to the job, not here.</summary>
    public string CaptionFont { get; set; } = FontList.DefaultFont;

    [JsonConverter(typeof(JsonStringEnumConverter<CaptionColor>))]
    public CaptionColor CaptionColor { get; set; } = CaptionColor.Black; // black by default (the author's choice)
    public bool SpacingEnabled { get; set; }
    public double SpacingMm { get; set; } = 5;

    /// <summary>White margins inside each visual (front only), in millimetres.</summary>
    public bool MarginsEnabled { get; set; }
    public double MarginTop { get; set; }
    public double MarginRight { get; set; }
    public double MarginBottom { get; set; }
    public double MarginLeft { get; set; }

    /// <summary>The back keeps the white margins of the front.</summary>
    public bool VersoMargins { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<FitMode>))]
    public FitMode Fit { get; set; } = FitMode.Fill;

    public bool CompleteLastSheet { get; set; }

    /// <summary>The quantity is typed in visuals or in sheets (Repeat mode). Sheets by default (asked by the author).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<QuantityUnit>))]
    public QuantityUnit Unit { get; set; } = QuantityUnit.Sheets;

    public string? LastExportFolder { get; set; }

    /// <summary>Export each sheet side as a 300 dpi picture in the PDF. On by default (asked by the author).</summary>
    public bool FlattenPdf { get; set; } = true;

    /// <summary>Mixed mode: the cutting plan (A4 PDF) is written next to each export.</summary>
    public bool CuttingPlan { get; set; } = true;

    /// <summary>The mark of each sheet (job, date, sheet number) outside the block.</summary>
    public bool SheetMark { get; set; }

    /// <summary>The project (.PageComposeProject: files and settings) is written next to each export.</summary>
    public bool SaveProject { get; set; }

    /// <summary>Colour of the cut lines drawn over the visuals: grey by default, seen on light and dark photos alike (the author's choice).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<CutLineColor>))]
    public CutLineColor CutLineColor { get; set; } = CutLineColor.Grey;

    /// <summary>The numbering of the visuals (off by default).</summary>
    public NumberingSettings Numbering { get; set; } = new();

    // A missing or damaged file just means default settings: preferences are never worth an error message.
    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath) && JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) is { } settings)
            {
                if (!SupportedLanguages.Contains(settings.Language))
                    settings.Language = SystemLanguage;
                return settings;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
