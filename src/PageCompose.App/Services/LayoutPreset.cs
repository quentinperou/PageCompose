using System.Text.Json;
using System.Text.Json.Serialization;
using PageCompose.App.ViewModels;
using PageCompose.Core.Layout;

namespace PageCompose.App.Services;

/// <summary>
/// The settings of a usual job, saved under a name: mode, size and white margins, sheet, cutting, fill, unit of the
/// quantity (visuals or sheets), double-sided and export options. Never the files nor the number of copies (decided
/// with the author: they change with each job).
/// </summary>
public sealed class LayoutPreset
{
    public string Name { get; set; } = "";

    [JsonConverter(typeof(JsonStringEnumConverter<LayoutMode>))]
    public LayoutMode Mode { get; set; }

    public string? FormatId { get; set; }
    public double CustomWidth { get; set; } = 100;
    public double CustomHeight { get; set; } = 150;
    public bool MarginsEnabled { get; set; }
    public double MarginTop { get; set; }
    public double MarginRight { get; set; }
    public double MarginBottom { get; set; }
    public double MarginLeft { get; set; }
    public bool VersoMargins { get; set; }

    public string? SheetId { get; set; }
    public double CustomSheetWidth { get; set; } = 320;
    public double CustomSheetHeight { get; set; } = 450;
    public double CustomSheetMargin { get; set; } = Core.SheetFormats.PrinterMarginMm;

    public bool CropMarks { get; set; } = true;
    public bool CutLines { get; set; }

    /// <summary>Cut lines in the Mixed mode (presets of 0.4.x: yes, as they were then always drawn).</summary>
    public bool MixedCutLines { get; set; } = true;
    public bool BleedEnabled { get; set; }
    public double BleedMm { get; set; } = 2;
    public bool SpacingEnabled { get; set; }
    public double SpacingMm { get; set; } = 5;

    [JsonConverter(typeof(JsonStringEnumConverter<FitMode>))]
    public FitMode Fit { get; set; } = FitMode.Fill;

    public bool CompleteLastSheet { get; set; }

    /// <summary>The quantity is typed in visuals or in sheets (presets of 0.4.6 and before: visuals).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<QuantityUnit>))]
    public QuantityUnit Unit { get; set; } = QuantityUnit.Items;

    public bool VersoEnabled { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<DuplexFlip>))]
    public DuplexFlip Flip { get; set; } = DuplexFlip.LongEdge;

    public bool RotateVerso180 { get; set; }
    public bool FlattenPdf { get; set; }

    /// <summary>Mixed mode: the cutting plan next to each export (presets of 0.4.x: yes).</summary>
    public bool CuttingPlan { get; set; } = true;

    public override string ToString() => Name;
}

/// <summary>
/// The presets, in %LOCALAPPDATA%\PageCompose\presets.json: a file of their own, apart from the settings, so that a
/// damaged settings file or another window of the app saving its settings never loses them. It is read again just
/// before each change, and a damaged copy is kept aside rather than overwritten.
/// </summary>
public static class PresetStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PageCompose", "presets.json");

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>
    /// Extension of the exported files, registered by the installer: a double click imports them. They are still
    /// plain JSON, readable in any text editor.
    /// </summary>
    public const string FileExtension = ".PageCompose";

    public static bool IsPresetFile(string path) => Path.GetExtension(path).Equals(FileExtension, StringComparison.OrdinalIgnoreCase);

    public static List<LayoutPreset> Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return Sorted(JsonSerializer.Deserialize<List<LayoutPreset>>(File.ReadAllText(FilePath)) ?? []);
        }
        catch (JsonException)
        {
            // Kept aside before the next save writes a new file.
            try { File.Copy(FilePath, Path.ChangeExtension(FilePath, ".damaged.json"), overwrite: true); }
            catch (IOException) { }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return [];
    }

    /// <summary>Adds the preset, or replaces the one of the same name. Returns the new list.</summary>
    public static List<LayoutPreset> Save(LayoutPreset preset)
    {
        var all = Load();
        all.RemoveAll(p => SameName(p.Name, preset.Name));
        all.Add(preset);
        return Write(Sorted(all));
    }

    public static List<LayoutPreset> Delete(string name)
    {
        var all = Load();
        all.RemoveAll(p => SameName(p.Name, name));
        return Write(all);
    }

    /// <summary>
    /// Writes every preset, or only the one named <paramref name="name"/>, to a file (to copy them to another PC or
    /// keep them safe). Returns how many were written; nothing is written when that preset no longer exists.
    /// </summary>
    public static int Export(string path, string? name = null)
    {
        var presets = Load();
        if (name is not null)
            presets = presets.Where(p => SameName(p.Name, name)).ToList();
        if (presets.Count == 0)
            return 0;

        File.WriteAllText(path, JsonSerializer.Serialize(new PresetFile { Presets = presets }, Json));
        return presets.Count;
    }

    /// <summary>
    /// Name proposed for an export: "all-preset.PageCompose" for all of them (the author's choice), else the name of
    /// the preset, without the characters a file name cannot hold.
    /// </summary>
    public static string ExportFileName(string? name)
    {
        if (name is null)
            return "all-preset" + FileExtension;
        var invalid = Path.GetInvalidFileNameChars();
        string safe = new string([.. name.Select(c => invalid.Contains(c) ? '-' : c)]).Trim().TrimEnd('.');
        return (safe.Length > 0 ? safe : "preset") + FileExtension;
    }

    /// <summary>
    /// Adds the presets of an exported file (a copy of presets.json works too); one of the same name is replaced.
    /// </summary>
    /// <exception cref="InvalidDataException">The file holds no PageCompose presets.</exception>
    public static (int Imported, int Replaced) Import(string path)
    {
        var (imported, replaced, _) = ImportPresets(path);
        return (imported.Count, replaced);
    }

    /// <summary>As <see cref="Import"/>, with the presets imported and the new list.</summary>
    public static (IReadOnlyList<LayoutPreset> Imported, int Replaced, List<LayoutPreset> All) ImportPresets(string path)
    {
        var incoming = Read(File.ReadAllText(path)).Where(p => !string.IsNullOrWhiteSpace(p.Name)).ToList();
        if (incoming.Count == 0)
            throw new InvalidDataException("No PageCompose preset in this file.");

        var all = Load();
        int replaced = 0;
        foreach (var preset in incoming)
        {
            if (all.RemoveAll(p => SameName(p.Name, preset.Name)) > 0) replaced++;
            all.Add(preset);
        }

        return (incoming, replaced, Write(Sorted(all)));
    }

    private static List<LayoutPreset> Read(string json)
    {
        try
        {
            var trimmed = json.TrimStart();
            if (trimmed.StartsWith('['))
                return JsonSerializer.Deserialize<List<LayoutPreset>>(json) ?? [];
            var file = JsonSerializer.Deserialize<PresetFile>(json);
            return file?.Format == PresetFile.Signature ? file.Presets : [];
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Not a presets file.", ex);
        }
    }

    /// <summary>An exported file: its signature tells it apart from any other JSON file.</summary>
    private sealed class PresetFile
    {
        public const string Signature = "PageCompose presets";

        public string Format { get; set; } = Signature;
        public int Version { get; set; } = 1;
        public List<LayoutPreset> Presets { get; set; } = [];
    }

    public static bool SameName(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.CurrentCultureIgnoreCase);

    private static List<LayoutPreset> Sorted(List<LayoutPreset> presets) =>
        [.. presets.Where(p => !string.IsNullOrWhiteSpace(p.Name)).OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)];

    private static List<LayoutPreset> Write(List<LayoutPreset> presets)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(presets, Json));
        File.Move(temp, FilePath, overwrite: true); // never a half-written file
        return presets;
    }
}
