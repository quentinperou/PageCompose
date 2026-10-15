# CLAUDE.md

Guidance for Claude Code and contributors working on this repository. `README.md` (English) and
`README.fr.md` (French) describe the project for users.

## The project

**PageCompose**: a small open-source (MIT) Windows 11 app that lays out visuals (JPG, PNG, PDF) on a sheet and
exports a **print-ready PDF** (step and repeat, crop marks, bleed, double-sided). Made for a photo shop printing on a
**FUJIFILM ApeosPro C810 for Photo** (SRA3 / A3 / A4 sheets, 4.1 mm unprintable margin). UI in English and French.
Author: [QuentinPerou](https://github.com/quentinperou). Repository: https://github.com/quentinperou/PageCompose
(public since October 11, 2026; the author commits and publishes himself). Credits wording: "Developed by
QuentinPerou, with Claude Code (Anthropic)".

**Talk to the author in French.** Code, comments, commit messages and these docs are in English.

## Project status (October 15, 2026)

Version **0.12.0** (October 15, 2026, made on the shop PC, committed there as QuentinPerou with the tag `v0.12.0`,
**not pushed**) makes the preview about ten times faster
with photos from a camera: lighter copies of the photos on screen ("Fast preview", an option, on by default; the
exports always take the originals), one render at a time, shorter waits; and the zoom into a photo, with a slider in
the right click menu of its line.
Version **0.11.0** (October 15, 2026, made on the shop PC, committed there as QuentinPerou with the tag `v0.11.0`,
**not pushed**: to push from the home PC, after 0.10.0) makes the project a document, as in Word
or Paint: a title bar drawn by the app with a "File" button (New, New window, Open, Save, Save as, the exports,
Settings, Quit, with their shortcuts) and the recent projects with their first sheet, the name of the project in the
title bar with a dot while changes are not saved, and "Save the changes?" on closing a project saved or opened.
Version **0.10.0** (October 15, 2026, made on the shop PC, committed there as QuentinPerou with the tag `v0.10.0`,
**not pushed**: to push from the home PC) converts the photos whose colour profile is not sRGB (Adobe RGB, Display P3…) to sRGB when they are
added (an option of Settings → General, on by default), adds a size for every line at once in Mixed sizes, with an
"Apply" button, in an "Apply to all" box with the copies (issue #1), and pins
a fixed `System.Security.Cryptography.Pkcs` (the 8.0.1 brought by PDFsharp has known vulnerabilities).
Version **0.9.0** (October 11, 2026: commit "PageCompose 0.9.0", tag `v0.9.0`, GitHub release with its patch note)
adds the copies of every line at once, a text in the band of the Polaroid and Instax frames (font and colour of the
PC, embedded in the PDF), the folded cards (four sizes laid open, dashed fold marks), the mirrored bleed (an option),
a single notice too long for its line summed up as several, and the Settings window on pages with a menu on the left.
Version **0.8.0** (October 11, 2026: commit "PageCompose 0.8.0", tag `v0.8.0`, GitHub release with its patch note)
adds fill or fit per visual (right click on its line), the colour of the cut lines (grey by default), the notices on
one line under the preview (the preview no longer changes size), and converts the JPEG files from the web that
PDFsharp refused ("Unsupported image format").
Version **0.7.0** (October 11, 2026: commit "PageCompose 0.7.0", tag `v0.7.0`, GitHub release with its patch note)
adds iPhone HEIC, WebP and TIFF photos (converted by Windows), the ID photo size 35 × 45, the mark of each sheet,
the projects (`.PageComposeProject`: files and settings, saved with an export or alone, opened again by a double click
or a drop), undo / redo (Ctrl+Z, Ctrl+Y), and renames the preset files `.PageComposePreset`.
Version **0.6.0** (published on October 11, 2026: tag `v0.6.0` on the commit "PageCompose 0.6.0", GitHub release
with its patch note) adds a back per visual in the lists (found alone from a 2-page PDF or a `-recto` / `-verso`
pair), reframing by dragging a photo on the preview, "Duplicate the line", a custom size per line in Mixed sizes and
the window that asks for the pages of a PDF.
Version **0.5.0** (published on October 11, 2026: commit "PageCompose 0.5.0: Mixed sizes mode and cutting plan",
tag `v0.5.0`, GitHub release with its patch note) adds the third mode, **Mixed** (several print sizes on a sheet,
packed with guillotine cuts, the cutting order numbered on the preview, the cutting plan), and widens the settings
column from 430 to 470 px. Version **0.4.9** works
end to end, developed and tested on Windows 11 (build 26200), and is installed on the shop PC. 0.4.9 drops two notices that repeated the settings column (empty cells, "Fit" mode). 0.4.8 stops photos from turning in some Polaroid frames and not others (a square image area within 1 µm turns
nothing), turns a visual by hand from the right click menu of its line, keeps the title, the Settings button and
the presets fixed above the settings that scroll, and lets the mouse wheel over a closed drop-down list scroll the
settings instead of changing the list (`App.ComboBox_PreviewMouseWheel`, a class handler for every ComboBox). 0.4.7 keeps the unit of the quantity (visuals or sheets) in the presets and the settings. 0.4.6 adds the preview zoom and paints the space between visuals white, so that a 0.1 mm space shows in
both directions in the preview, the JPG export and the flattened PDF. 0.4.5 exports either all the presets or a single one. 0.4.4 gave the preset files their own extension, `.PageCompose` (icon, double click imports), and dropped the
"Open the file after export" box (the toast offers it; "Open" opens the first picture of a JPEG export). 0.4.3 added the presets (with export / import in Settings), the page of a PDF (two pages: front and back), "0.1" /
"0,1" with the digits of narrow number boxes visible, and the clean-up at uninstall. 0.4.1 added the Mica window background, the white margins on the back as an option, and "Export PDF" without an
ellipsis. 0.4.2 added the JPEG export, the flattened PDF, the export toast, and fixed a second window deleting the
converted PDF pictures of the first one. 0.3.0 added white margins, the framed sizes (classic Polaroid, Instax Mini / Square / Wide, 10 × 10 with a 5 mm
border) and the space between visuals. 0.4.0 added the lab print sheets (10 × 15, 20 × 30, 30 × 45, borderless), a
custom sheet with its own unprintable margin, the optional filling of the last sheet in Repeat mode too, the
Up / Down arrows in number fields, continuous cut lines, and the image fallback for PDFs that PDFsharp cannot read;
the photo sizes moved to exact inches.

- Three modes: **Repeat** (one visual over the whole sheet, quantity in visuals or sheets), **Compose** (several
  visuals of one size, each with its own copies) and **Mixed** (several visuals, each with its own size and copies,
  packed on as few sheets as possible). Those are their names in the code and these notes (`LayoutMode`, settings,
  presets); the UI calls them **Repeat · Same size · Mixed sizes**, in French **Répétition · Même format ·
  Multi-formats** (renamed in 0.5.0 at the author's request: "Composition" sounded like the mode of several sizes). In Repeat and Compose, "Fill the last sheet" is optional (off: exactly the
  quantity asked); Mixed lays exactly the copies asked.
- Drag and drop of files and folders, double-sided (a shared back, and in the lists a back per visual) with the
  front and back previews side by side, sheet navigation, resolution and cropping checks, export.
- English/French UI with live switching from Settings (the main window is rebuilt on the same view model).
- Windows 11 Fluent theme, light or dark: follows Windows, or forced from Settings (applied live).
- Updates from GitHub Releases, as in CropPdf: check at startup (can be turned off), Stable/Beta channel, bar in the
  main window, download checked with SHA-256, silent install and relaunch. The Settings window has CropPdf's cards
  (language, theme, presets, updates, about), on pages chosen from a menu on the left (see below).
- 1344 unit tests pass. `scripts\build.ps1` builds `dist\PageCompose-Setup-0.12.0.exe` locally (Inno Setup 6).
- The installer is not signed: the UAC prompt shows "Unknown publisher" (QuentinPerou appears in the file details
  and in Installed apps). Left as is for now (October 10, 2026). A paid certificate would show the author's legal
  name, SignPath Foundation its own name; only a self-signed certificate trusted by hand on each PC shows "QuentinPerou".
- First commit on October 10, 2026 (branch `main`), made by Claude at the author's request, as QuentinPerou with his
  GitHub noreply address, set in the repository's own config, and tagged `v0.4.9`. Published on October 11, 2026:
  public repository https://github.com/quentinperou/PageCompose, `main` and `v0.4.9` pushed, the workflow built and
  published the release `v0.4.9` with `PageCompose-Setup-0.4.9.exe` (the update check now finds it). The project
  started as "Apeos Print" (layout +
  direct printing to the ApeosPro); direct printing was dropped on October 10, 2026: nothing is ever sent to a printer.

## Architecture

- `src/PageCompose.Core` (net10.0, no UI, **all the logic, unit-tested**):
  - `Layout/LayoutEngine`: step and repeat layout (both orientations tried, block centred, crop marks);
  - `Layout/Placement`: a visual in its cell (fill / fit, auto quarter turn, EXIF, back orientation, PDF on its TrimBox);
  - `Layout/SheetFiller`: visuals assigned to the cells, sheet by sheet;
  - `Layout/MixedPacker`: the Mixed mode. `MixedItem`s (visual, size, copies, margins of its size) → `MixedPlan`
    (one `MixedSheet` per sheet: its own `SheetLayout` with `IsMixed`, `CutSegments` in cutting order and white
    `Spaces`, and the item of each cell; items too large left out). A guillotine tree per sheet, many heuristics
    tried (fewer above 150 copies), `NumberPositions` places the cut numbers on screen;
  - `Visuals/VisualLoader`: JPEG/PNG headers read without decoding, EXIF orientation, PDF through PDFsharp;
    errors are `VisualLoadException` with a `VisualLoadError` code that the UI translates. `ConvertedExtensions`
    (HEIC / HEIF, WebP, TIFF): taken by `IsSupported`, but converted by the app first (`NeedsConversion`);
  - `Visuals/ColorProfile`: the ICC profile of a JPEG (APP2 "ICC_PROFILE" segments, joined in order) or a PNG (iCCP,
    zlib; or an sRGB chunk, the profile winning when both are there), read by `ImageHeaderReader` without decoding:
    its name ("desc", ICC v2 text or v4 "mluc") and whether it is sRGB, **by its primaries** (rXYZ / gXYZ / bXYZ within
    0.01 of sRGB in D50: the small "c2" sRGB profiles of the web count), by its name without primaries.
    `VisualInfo.Profile`, `ProfileConverted`, `HasForeignProfile`. Why: PDFsharp declares a JPEG `/DeviceRGB`, so a RIP
    and Windows (preview, JPG, flattened PDF) ignore the profile inside it, and an Adobe RGB photo printed dull
    (checked October 15: Adobe RGB 60,160,90 should be sRGB 0,162,84; the render gave 60,160,90);
  - `Visuals/BackPairs`: fronts and backs told apart by their names among files added together (`recto` / `verso`,
    `front` / `back`, a word of its own, any case; same folder and rest of the name, the extension may differ);
  - `Pdf/ImpositionPdfWriter`: writes the PDF (PDFsharp 6.2.4, MIT); `ImpositionJob.SheetLayouts` gives each sheet
    its own layout (`ImpositionJob.Mixed`); `ImpositionJob.Backs` the own back of each cell (null: the shared
    `Verso`), `HasBack` / `BackOf`. `Pdf/FlatPdfWriter`: the flattened PDF, one JPEG picture per page,
    embedded as it is.
  - `Pdf/CuttingPlanWriter`: the cutting plan of the Mixed mode, an A4 PDF to print on office paper, never with the
    sheets: one page per sheet, the sheet drawn smaller (its visuals faded, through `ImpositionPdfWriter.DrawVisuals`)
    with the name of each size, the cuts in red, their numbers, and a column of measurements
    (`SheetLayout.CutMeasurement`: from the top or left edge of the piece cut, `CutPieces`; a part narrower than
    `MinHeldMm` = 30 mm, which the clamp cannot hold, from the bottom or right). At the foot of each page, on the
    right, the icon of the app (its 256 px PNG, passed by the app), "PageCompose" and its GitHub address, a link
    (asked by the author). The only text PDFsharp draws in the app: Segoe UI read from the Windows fonts folder
    (`WindowsFonts`, the core build of PDFsharp finds no font).
  - `UpdateFeed` + `SemanticVersion`: GitHub "list releases" API of `quentinperou/PageCompose`, asset
    `PageCompose-Setup-*.exe` and its `digest` (SHA-256), SemVer 2.0 ordering, never offers a downgrade
    (ported from CropPdf).
  - Preset sizes (`ItemFormats`, `SheetFormats`) have a stable `Id`; their labels live in the UI strings
    (`Format_<Id>`, `Sheet_<Id>`). `PaperFormat.Margins` is where nothing is printed: the white border of a framed
    size, the unprintable edges of a sheet. Choosing a framed size in the app fills the margin fields, leaving it
    gives the user's own margins back.
- `src/PageCompose.App` (WPF, net10.0-windows10.0.19041.0), a thin layer:
  - **Windows 11 theme, light and dark**: `Fluent.xaml` merged in `App.xaml`, then `App.ApplyTheme` sets
    `Application.ThemeMode` (System / Light / Dark; experimental API, `WPF0001` is suppressed in the csproj).
    **Colours are always `{DynamicResource <Fluent brush>}`** (`TextFillColorPrimaryBrush`,
    `CardBackgroundFillColorDefaultBrush`, `SystemFillColorCautionBrush`…), never hard-coded, or they stay light in
    dark mode. Only the sheet preview is white (paper). App styles must be `BasedOn` the Fluent style **by its name**
    (`DefaultCheckBoxStyle`, `AccentButtonStyle`…): `{StaticResource {x:Type CheckBox}}` finds the classic Windows
    style. Icons use the "Segoe Fluent Icons" font.
  - **Mica** (`Services/MicaBackdrop`, Windows 11 22H2+): the windows get the material of their title bar
    (DWM system backdrop; the main window draws its own title bar since 0.11.0, on that material, see "Title bar and
    File menu"); their XAML `ApplicationBackgroundBrush` stays as the fallback elsewhere. The settings
    column is transparent on it (only its cards stand out), the preview sits on `LayerFillColorDefaultBrush` with a
    rounded top-left corner, like the content of Windows 11 apps. Screen copies are needed to see Mica (PrintWindow
    does not draw it): take them only when PageCompose is in the foreground, never another application.
  - `Services/Updater`: check at startup, download to `%TEMP%\PageCompose-Update`, run the installer with
    `/SP- /SILENT /NORESTART /CLOSEAPPLICATIONS /FORCECLOSEAPPLICATIONS /RELAUNCH=1` (the `.iss` relaunches the app).
  - `ViewModels/MainViewModel`: modes, `VisualItem` list (thumbnail, copies, dpi), recomputed on every change. Over
    the lines, a box "Appliquer à tous" ("Apply to all", `ShowCopiesForAll`; in the Same size and Mixed sizes lists
    alike): in Mixed sizes first "Format : [list] [Appliquer]" (`FormatForAll` / `FormatsForAll`, the preset sizes,
    empty while the lines differ, a custom size staying typed per line; choosing in the list changes nothing until
    "Appliquer", `ApplyFormatForAllCommand`: asked by the author, GitHub issue #1), then "Exemplaires : [−] [n] [+]"
    (`CopiesForAll`, every line at once, one recomputation; the copies never change the sizes). The box is filled
    with `SubtleFillColorTertiaryBrush` and outlined with `ControlStrokeColorSecondaryBrush` (the card stroke alone
    vanished in the dark theme; the stronger ones were too dark in the light one);
    the preview is regenerated 200 ms after the last change, for the displayed sheet only.
  - **Mixed mode** (`RecomputeMixed`): uses the `Visuals` list of Compose; each line has its own size
    (`VisualItem.Format`, `MixedFormats`: the preset sizes and "Custom", whose width and height are typed on the line,
    `VisualItem.CustomWidth/Height`; a custom size under 5 mm leaves the line out, which says so), on a row of its own
    across the whole line (the author chose it among three places: under the name cut long size names, on the right
    left no room for the file name). A new line takes the size of the size section (custom size included).
    "Duplicate the line" (right click, lines of a list only: `VisualItem.DuplicateCommand`) adds the same visual,
    page, turn and size once below: the same photo in another size (a file already in the list is not added again
    by a drop, which is kept as a guard against a drop made twice). That section is hidden, as "Fill the
    last sheet" and the margins on the back (the back covers each cell). The crop marks are offered (shared with the
    grids: those of the cuts that reach the edge of the block, with a notice when fewer sheets fit without them), and
    the cut lines have a setting of their own, `MixedCutLines` ("along each cut", on by default, in the settings and
    the presets), since those of the grids are often off (asked by the author: both were first hidden). `Layout` is the shown sheet's; `CutNumbers` draws the cutting order over the front of the preview only
    (never in the PDF or JPG: the numbers would land on the photos). `MixedPacker.NumberPositions(layout, size)`
    places them, for the screen and the cutting plan alike: the numbers of the edges of the block just outside it,
    beside its bleed (easier to read, asked by the author; kept on the sheet when the margin is too narrow), the
    others along their cut, apart from each other. The last packing is kept (`_lastPacking`) while
    sheet, bleed, spacing, sizes and copies do not change. Notices: each line checked in its first cell, the back in
    the largest; too large lines say so on their line and in a notice. "Add the cutting plan (A4)" (`CuttingPlan`,
    on by default, in the settings and the presets, shown in the Mixed mode only) writes `<name>-cutting-plan.pdf`
    (`-plan-de-coupe` in French) next to each export, PDF or JPG; the toast says "+ cutting plan".
  - **The preview is the real PDF**, rendered by `Windows.Data.Pdf` (`Services/PdfPreviewRenderer`): no drawing
    logic is duplicated in the UI. **Fast preview** (0.12.0, the author's audit request; "Aperçu rapide", Settings →
    General, `AppSettings.LightPreview`, on by default): the photos of the preview are lighter copies
    (`Services/PreviewImages`: long side 1024 to 6144 px, the size each cell needs at the width rendered, × 1.5 for a
    filled photo; decoded straight to that size by WIC, colours as stored, JPEG 90 or PNG; none for a PDF, a CMYK JPEG
    or a photo hardly larger), made once in `%TEMP%\PageCompose\Preview` and named by a fingerprint of the content of
    the file (path, size, date, first and last 64 KB: another photo under the same name gets its own copy, asked by
    the author). `MainViewModel.LightenAsync` swaps them into the preview job only (and the picture of the recent
    projects): **the exports, the cutting plan and the projects never use them** (the author's first concern). Measured
    with 8 photos of 12 MP: about 1 s per refresh before, 0.1 s after (2.1 s → 0.2 s zoomed). Also in 0.12.0: the
    screen gets a BMP from the renderer (`forScreen`; the exports keep the PNG), one render at a time (the latest
    wins), the PDF loaded once for both sides, 100 ms before a refresh (was 200) and 250 ms for the number boxes
    (`Delay`, was 400). `.local\cs\preview-bench.cs` measures the steps.
  - **Preview zoom** (`MainWindow.xaml.cs`, "Preview zoom"): zoom 1 = the sheets fit the area (Viewbox, no scroll
    bars); above 1 the sides are laid out that many times larger in `PreviewScroll`. Buttons, Ctrl + wheel around the
    mouse, Ctrl + / - / 0, double click; to move: the middle button, or Alt + left button, anywhere, or the left
    button from a place without photo (the left button on a photo reframes it; the author chose middle and Alt).
    Cursor: a hand when zoomed, the move cursor over a photo, the hand again over a photo while Alt is held (updated
    when Alt is pressed or released, asked by the author).
  - **Reframing on the preview** (`MainWindow.xaml.cs`, "Reframing"; `MainViewModel.BeginCropDrag` / `CropDragTo` /
    `EndCropDrag`): the left button drags a photo in its cell (front, or its back), every copy of its line on
    the sheet following; meanwhile the copies are drawn over the rendered preview (`PreviewSide.Overlays`,
    `CropOverlay`: the photo as stored, `ThumbnailLoader.LoadRawAsync`, faded around its cell, sharp inside it) until
    the preview is rendered again with the new part kept. A double click still zooms; "Recentrer la photo" in the
    menu of the line, which a right click on the photo in the preview opens too. The grids give each line its own
    `VisualInfo` object (`_lineOfInfo`), so that a cell finds its line back. The label is in % of the real size. The view tells the view
    model how wide the sheet is on screen (`SetPreviewDisplayWidth`), which renders the preview from 1400 up to
    3600 px wide, in steps of 400.
  - Dialogs, drag and drop and the update bar in `MainWindow.xaml.cs`; `SettingsWindow`, reopened after a language
    change. **Settings on pages** (asked by the author, variant A of two mock-ups, `.local\settings-mockups.cs`): a menu
    on the left as in the Windows 11 Settings app (`PageList`, an accent bar on the chosen entry), General (language,
    theme), Presets, Updates, About, the page title from the chosen entry. The four pages share one cell, the hidden
    ones keeping their room, so the window takes the height of the tallest and does not jump; each page scrolls once
    the window reaches the height of the screen (`MaxHeight` = work area). No Close button (the cross of the title
    bar, as in Windows 11); Escape closes. No "Settings" heading over the menu (the title bar says it, as in Windows 11;
    removed at the author's request). No title repeats its page: the Presets card is titled by the count, the
    Updates card by the installed version ("Version 0.9.0", what the check found under it), the About card by the
    app's name and its icon (the PNG). CropPdf still has the single page.
  - `Services/PdfRasterizer`: a PDF that PDFsharp cannot read (non-standard files, e.g. an encrypted invoice
    whose Info strings have unescaped parentheses) is rendered by `Windows.Data.Pdf` to a 300 dpi PNG in
    `%TEMP%\PageCompose\Converted` (files older than a day deleted at startup: another window may be using its
    own) and used as an image; `VisualInfo.SourcePath` keeps the
    original file for the name, the folder and the "PDF · … px" line.
  - `Services/PhotoConverter`: an iPhone HEIC, a WebP or a TIFF, or a JPEG that PDFsharp refuses (one that does not
    start with a JFIF, Exif or ICC segment, as some web images: `VisualLoadError.NeedsConversion`, found at loading,
    else the whole preview failed with "Unsupported image format"), is decoded by Windows (WinRT
    `BitmapDecoder`: EXIF orientation applied, colours managed to sRGB, since an iPhone HEIC is in Display P3),
    flattened on white where transparent, and saved full size as a JPEG of quality 95 in the same `Converted`
    folder (once per file version); `SourcePath` keeps the original ("HEIC · 4032 × 3024 px"). HEIC needs the
    "HEIF Image Extensions" and "HEVC Video Extensions" of the Store: without them, the line is refused with
    `LoadHeicCodec`, which names them. **A photo whose profile is not sRGB** (`HasForeignProfile`: Adobe RGB, Display
    P3, ProPhoto, CMYK…) goes the same way when `AppSettings.ConvertToSrgb` is on ("Convertir les photos en sRGB à
    l'import", Settings → General, on by default, asked by the author on October 15; the photos already in the list
    stay as they were read); its line says "· Adobe RGB (1998) → sRGB" (off: "· Adobe RGB (1998)"), with the whole
    details line as tooltip. sRGB photos and photos without a profile are never touched.
  - Exports ("Export as PDF" / "Export as JPG": same "as" on both buttons, "JPG" everywhere in the UI): the PDF as
    it is; "Flatten the PDF" and "Export as JPG" render each page of that PDF with
    `Windows.Data.Pdf` at 300 dpi (`Services/SheetExporter`: JPEG quality 95, 300 dpi in the file), then
    `FlatPdfWriter` gathers them for the flattened PDF. JPEG files: the chosen name for a single picture, else
    `name-f1.jpg`, `name-f1-verso.jpg`… The result shows in a toast over the preview (`ViewModels/ExportToast`,
    open the first file / show in folder, hidden after 8 s), never as a long status text (the author found it ugly).
    There is no "open after export" option: the toast offers it (removed at the author's request).
  - **Presets** (`Services/LayoutPreset` + `PresetStore`): the settings of a job under a name, never the files nor
    the number of copies (decided with the author); the unit of the quantity, visuals or sheets, is kept
    (`LayoutPreset.Unit`, asked by the author; older presets read as visuals). The unit is also kept in the settings,
    **sheets by default** (asked by the author in 0.5.0). Stored in their own `%LOCALAPPDATA%\PageCompose\presets.json`, read
    again before each change (several windows), damaged copy kept aside. The list shows a preset only while the
    settings match it: a manual change clears the selection, so picking it again applies it again. Settings →
    Presets exports them (`{"Format": "PageCompose presets", "Version": 1, "Presets": [...]}`) and imports them
    (a copy of presets.json works too; the same name replaces). The export takes all of them
    (`all-preset.PageComposePreset`, the author's name) or the one chosen in a list (`<preset name>.PageComposePreset`,
    `PresetStore.ExportFileName`). Exported files end in **`.PageComposePreset`** (`PresetStore.FileExtension`,
    capitals wanted by the author; still plain JSON; `.PageCompose` until 0.6.0, renamed at his request with no
    compatibility kept: the installer deletes the old association). The installer associates the
    extension (`[Registry]`, ProgID `PageCompose.Presets`, icon `{app}\PresetFile.ico`): a double click starts the
    app with the file, and `MainWindow.AddPathsAsync` imports it (also when dropped on the window); a file of one
    preset applies it, a toast says what came in.
  - **Sheet mark** ("Mark each sheet", `SheetMark`, in the Export section, the settings and the presets; asked by the
    author): `ImpositionJob.Marks`, one text per sheet, "name · date · sheet 2/5", the name being that of the exported
    file (the one offered, in the preview). `Core/Pdf/SheetMark.Place` finds a band of 2.6 mm outside the block and its
    crop marks, 1 mm away, inside the printable area: below, else above, else along the left or right side (read from
    the bottom up); none on a lab print filled to its edges, which a notice says. Grey Segoe UI 6 pt (down to 4 pt
    when too long), on the front only.
  - **Projects** (`Services/ProjectStore`, `.PageComposeProject`, asked by the author; first named `.PageComposeJob` and
    "commande", renamed before any release at his request: "Job" read as a print job sent to a printer): "Save the
    project" (`SaveProject`, Export section, settings and presets, off by default) writes `<name>.PageComposeProject`
    next to each export, PDF or JPG: a zip renamed, `project.json` (`ProjectFile`: the settings as a `LayoutPreset`, the quantity, the Repeat visual, the shared back,
    the lines with copies, page, turn, part kept, size, own back and its "auto") and a copy of each file once
    (`files/1/IMG_0042.HEIC`: a folder each, so that the names stay), and since 0.11.0 `preview.png`, the front of the
    first sheet 120 px wide (`ProjectStore.ReadPreview`, for the recent projects). The project written next to an
    export becomes the file of the job (see "File menu" below); the "Export the project" button of 0.7.0 to 0.10.0 is
    gone (File > Save replaces it, the author's remark). A double click (ProgID `PageCompose.Project`, icon
    `{app}\ProjectFile.ico`, chosen by the author: the preset page in front of a paler one) or a drop opens it: extracted
    to `%TEMP%\PageCompose\Projects\<name>-<hash>` (folders older than a day deleted at startup), it takes the place of
    the job in progress (Ctrl+Z gives it back); the export then offers its folder and name. A double click starts a
    window of its own; dropped on a window that holds work, `OpenProjectWindow` asks (asked by the author): "In a new
    window" (the default: another process of the app with the file, as a double click) or "Replace". `Window_Drop`
    answers the drop before any question (`Dispatcher.Yield`): a dialog shown inside the drop held Explorer until it
    was answered.
  - **Title bar and File menu** (0.11.0, asked by the author, variant A + C (1) of his mock-ups,
    `.local\maquettes\recents-*.png`): the window draws its own title bar (`WindowChrome`, `CaptionHeight` 32,
    `GlassFrameThickness` -1, `UseAeroCaptionButtons`: Windows keeps drawing its buttons, snap layouts included, and
    Mica stays behind the whole window): the logo (the frame of the .ico for the scale of the screen, `ShowTitleIcon`),
    "PageCompose", the "File" button (a real button with the arrow of a menu, variant C of three: as plain text it did
    not read as a button), then the name of the project, in the middle of the bar when it fits between "File" and the
    buttons of Windows, else right after "File" (`PlaceProjectName`, asked by the author). Maximized, the content is
    moved in by the resize border (`FitMaximized`). "File" opens one panel (`FilePanel`, a Popup): the commands on the
    left (New Ctrl+N, New window Ctrl+Shift+N, Open… Ctrl+O, Save Ctrl+S, Save as… Ctrl+Shift+S, Export as PDF Ctrl+E,
    Export as JPG, Settings Ctrl+,, Quit Alt+F4; Alt+F opens it), the recent projects on the right with the first sheet
    of each (`RecentProjectItem`; the 10 last opened or saved, `Core/RecentFiles`, stored in
    `%LOCALAPPDATA%\PageCompose\recent.json` by `Services/RecentProjects`, read again each time the panel opens, a file
    no longer there dropped; "Clear the list").
  - **The project as a document** (0.11.0, the author's decisions): a job is "Untitled" until it is saved or opened,
    then its `.PageComposeProject` file (`MainViewModel.ProjectDocument`: its path and its `JobState` when saved; each
    undo step keeps its document, so Ctrl+Z after "New" or after opening gives the job back with its file). The window
    starts on an untitled job (the last settings, no visual). Save writes into its file (asked the first time), Save
    as always asks; the box "Save the project" of the Export section stays and its project becomes the file of the job.
    The name has a dot (●) while a project saved or opened has changes (`IsModified`, the state compared with the one
    saved, counted 600 ms after a change as undo); closing, "New", another project in its place or an update ask
    "Save the changes to “name”?" (`SaveChangesWindow`: Save / Don't save / Cancel) only for such a project: a job
    never saved closes as it always did (the author's choice). The language change closes the old window without
    asking (`CloseForReload`). "New" empties the window and keeps the settings (offered when the author hesitated, a job
    often looks like the one before; confirmed by him after trying it); "New window" starts another process. A number being typed is taken before saving
    (`CommitTyping`). "Remove all" keeps the project (the lines only).
  - **Undo, redo** (Ctrl+Z, Ctrl+Y or Ctrl+Shift+Z; shortcuts only, the author's choice): `MainViewModel` keeps the
    whole job (`JobState`: the settings through `CapturePreset`, the quantity, `VisualItem.Capture()` of each line,
    the same objects) 600 ms after the last change, up to 100 steps; `RestoreState` puts the same lines back
    (`VisualItem.Restore`) with `_restoring` set, so that nothing is recomputed until the end. In a text box that can
    undo its own typing, Ctrl+Z is left to it.
  - **Uninstall** (`installer/PageCompose.iss`): removes the file associations, `%TEMP%\PageCompose` and
    `%TEMP%\PageCompose-Update`, then asks whether to delete `%LOCALAPPDATA%\PageCompose` (settings, presets), No by
    default; a silent uninstall keeps them. The .NET runtime stays (shared).
  - **PDF pages**: `VisualInfo.PageNumber` / `PageCount`; a page list on the file line (`VisualItem.Pages`) when
    there are several. A PDF of exactly two pages in Repeat mode sets page 2 as the back; in a list with
    Double-sided ticked, page 2 becomes the back of its line. Otherwise, added to a list (Same size,
    Mixed sizes), PDFs of several pages make the app ask once (`PdfPagesWindow`, through
    `MainViewModel.AskPdfPages`): all their pages, one line each, or one page (chosen in a list for a single PDF;
    the first of each for several, changed afterwards on its line). Cancel or the window closed: those PDFs are not
    added, the other files are (the author's requests after a first version that took the first page then).
  - Every text box holds a number: an implicit style gives them `NumericInput` (Up / Down arrows, Shift: by 10,
    `NumericInput.Minimum`; "." and "," both become the decimal separator of the PC) and `TextWrapping="Wrap"`, which
    hides the Fluent clear button (it hid the digits of narrow boxes). A text box for text sets `NumericInput.Step="0"`. The settings column's `ScrollViewer` is not focusable, or a keyboard focus frame shows
    around the whole column.
  - Settings column: the sections have no number in their title (asked by the author in 0.5.0: some are hidden in
    some modes, the numbers then jumped). The title, the Settings button and the presets are fixed at the top (asked by the author), the
    two export buttons at the bottom, each with a divider; only the settings between them scroll. The export options
    ("Flatten the PDF", "Add the cutting plan" in the Mixed mode, "Mark each sheet", "Save the project") are in the last
    section, "Export", at the end of the scroll (asked by the author in 0.5.0); all are saved in the settings and in the
    presets; the first two are on by default (the author's choice), the last two off.
  - **Notices under the preview** (`MainViewModel.AddVisualNotices`), on **one line of a fixed height** (asked by the
    author: their list changed the height of the preview with each change; variant A of his mock-ups): the icon and
    colour of the worst, "3 alertes" and their key words (`Notice.Key`, "Recadrage · Verso"), or the notice itself when
    alone and short enough (then nothing to unfold, its whole text as tooltip: the author's remark; too long for the
    line, measured by the view, `SingleNoticeFits`, it is summed up as several: "1 alerte · Recadrage"); a click shows them all
    (`NoticesOpen`, `NoticePanel`) over the bottom of the preview, which does not move;
    a click elsewhere or Escape closes them; nothing opens by itself, even for an error (his choice). With no notice the
    line is hidden, its room kept. Only what the settings column does not already
    show (resolution, cropping, own bleed, missing back, how many fit without crop marks). The author removed those
    that repeated it: the empty cells (in the quantity summary) and the "Fit" mode (in its own label).
  - Settings in `%LOCALAPPDATA%\PageCompose\settings.json` (`Services/AppSettings`, one shared instance `App.Settings`).
  - **Several windows of the app may run at once** (decided with the author: two jobs in parallel). Each saves its
    settings when it closes (the last one wins); nothing shared may be deleted at startup if another window can be
    using it.
- `tests/PageCompose.Core.Tests` (xUnit, net10.0-windows so WPF can create test JPEG files): reference table of
  layouts, geometry, EXIF, placement, distribution, PDF content (image stored once, JPEG not recompressed),
  localization (`LocalizationTests`).
- WPF projects do not import `System.IO` implicitly: it is added with `<Using Include="System.IO" />`.

## Layout rules

- **Internal unit: millimetres.** PDF points: `pt = mm × 72 / 25.4`.
- Sheets: SRA3 320 × 450, A3 297 × 420, A4 210 × 297 (ApeosPro); lab prints 10 × 15, 20 × 30, 30 × 45; custom
  (laid portrait, 20 to 3000 mm). The PDF page is **exactly** the sheet, portrait, block centred.
- **Unprintable margin per sheet** (`PaperFormat.Margins` of the sheet): 4.1 mm on every edge for the ApeosPro sheets
  (IPP `media-*-margin-supported` = 410, read from the printer), 0 for lab prints (printed to the edge), set by hand
  for a custom sheet. Nothing, crop marks included, may fall in it. The preview shows it as a red band.
- Cutting: edge to edge = adjoining visuals, shared cut; crop marks = **3 mm reserved** outside the block, 1 mm
  offset, up to 5 mm long (longer marks would cost visuals: 8 → 4 photos 10 × 15 on SRA3); bleed = 2 mm by default,
  4 mm between two visuals.
- Photo and lab print sizes are **in inches**, as labs print them: 9 × 13 = 3.5 × 5 in (88.9 × 127),
  10 × 15 = 4 × 6 in (101.6 × 152.4), 13 × 18 = 5 × 7 in, 15 × 20 = 6 × 8 in, 20 × 30 = 8 × 12 in (203.2 × 304.8),
  30 × 45 = 12 × 18 in. So four 10 × 15 fill a 20 × 30 exactly (rounded to 102 × 152 they did not). The SRA3 / A3 / A4
  reference table did not change. Business card: 85 × 55. ID photo: 35 × 45 (`IdPhoto`, eight on a 10 × 15 print).
- Crop marks need 3 mm around the block: on a lab print, where the visuals reach the edges, they cost visuals; the
  app then says how many fit without them.
- Both orientations are tried; on a tie the requested one is kept.
- A visual turns a quarter when its proportions oppose those of its area (`Placement`); an area square within 1 µm
  turns nothing: cell edges are sums, and rounding used to make the 79 × 79 Polaroid image area "taller" in some
  cells only, so one photo turned when the space between visuals changed.
- Images: embedded **without recompression**, once per PDF (except the copies converted by Windows: HEIC, WebP, TIFF,
  JPEG refused by PDFsharp, and photos of another profile than sRGB, JPEG 95); EXIF orientation applied; warning
  below 200 dpi, error below 150 dpi. **All the colours that leave the app are sRGB** (designer PDFs aside: kept as
  they are, their own colour spaces left to the RIP).
- A PDF with a TrimBox is placed at 100 % on the trim and clipped to the layout bleed.
- Back: cells mirrored according to the flip (long edge: vertical axis; short edge: horizontal axis). Crop marks
  and continuous cut lines (`ImpositionJob.CutLines`: every cut, across the block of trims) on the front only.
- **A back per visual** (lists only, `VisualItem.Back`, chosen by the author from a combined mock-up): with
  Double-sided ticked, each line shows a "Verso" slot (drop or click; × gives the shared back again; right click turns
  or centres it). Behind a line without its own back: the shared back of the "Recto verso" card, else nothing (a
  notice counts those visuals). Found by the app, with an "auto" badge (`IsBackAuto`): page 2 of a PDF of two pages,
  and `BackPairs` among the files added together. In Repeat mode, one such pair is the visual and the shared back.
  Duplicating a line copies its back; it belongs to the line, not to the presets.
- **Space between visuals** (`LayoutOptions.SpacingMm`): trim to trim; the gap is `max(spacing, 2 × bleed)`, so the
  bleed always fits. Spacing is only between visuals, not around the block. When it is wider than the bleeds, the
  band left between them is **painted white** (`ImpositionPdfWriter.PaintSpaces`, both sides): nothing changes on
  paper, but Windows rounds clipping paths to whole pixels, and a 0.1 mm band (1.2 px at 300 dpi) used to vanish
  between some rows or columns and not others.
- **White margins** (`MarginsMm`, top / right / bottom / left of the visual as requested): inside the finished size,
  on the front; the back covers the whole cell (it hides the front/back offset of duplex printing) unless
  `ImpositionJob.VersoMargins`, where it keeps the margins mirrored with the cell (`MirrorForVerso`), so that the
  thick border of a Polaroid stays under the visual on both faces. No crop marks on the back (decided with the author). The image fills `LayoutCell.ImageBleed`: the trim inside the
  margins, plus the bleed only on the sides without a margin. Resolution and cropping checks use `ImageTrim`.
- When the layout turns the size a quarter (`LayoutCell.Rotated`), the margins turn **clockwise** with it
  (`RotatedClockwise`: the top goes to the right). With asymmetric margins (a Polaroid) and a visual that fits either
  way (square image or square image area), the visual turns with the frame so that it stays upright in it; otherwise
  the usual orientation rule applies, **frames included** (the author wants no exception).
- **Part kept** (`VisualInfo.CropX` / `CropY`, 0 = one edge, 0.5 = centre, 1 = the other edge): which part of a
  visual stays when it overflows its area (Fill), or where it lies when it leaves room (Fit). In the axes of the
  visual as stored (before any turn), so that every copy keeps the same part however it is laid; `Placement` shifts
  the draw rectangle by that share of the overflow, and `Placement.Dragged` turns a drag on the sheet into it (the
  turn and mirror undone, kept between the edges). Chosen by dragging the photo on the preview (the author's choice
  among three mock-ups); it belongs to the line (`VisualItem.Crop`), not to the presets.
- **Zoom into a photo** (0.12.0, `VisualInfo.Zoom`, 1 to `MaxZoom` = 3; asked by the author, variant C of three: in the
  right click menu of the line, with a slider, the value beside it, a double click on the slider giving 100 % back,
  the menu staying open while it moves; mock-up `.local\maquettes\zoom-photo-menu.png`): `Placement.Scale` multiplies
  the Fill or Fit scale by it, around the part kept, so a drag shows another part; the clip stays the cell, the
  effective dpi goes down with it (the resolution notice follows), and the line says " · Zoom 150 %". "Reset the
  framing" (was "Recenter the photo") gives 100 % and the centre back. It belongs to the line (`VisualItem.Zoom`,
  undo, duplicate, projects: `ProjectLine.Zoom`), not to the presets.
- **Mirrored bleed** (`SideOptions.MirrorBleed`, `ImpositionJob.MirrorBleed`, a choice under the bleed, **off by
  default**: the author keeps "Agrandir la photo"): a visual without its own bleed, filled, keeps its size in its trim
  (target = trim) and the bleed takes its edges: `VisualPlacement.MirrorCopies` lists the flipped copies around it that
  the clip shows (the clip turned back into the axes of the drawing), drawn first, 0.05 mm larger, so that no seam shows.
- **Folded cards** (`PaperFormat.Folds`, `FoldsMm`, turned clockwise with the size; asked by the author): A6 folded
  (A5 open), 10 x 15 folded (10 x 30 open), square 14 x 14 folded, trifold A4 (folds at 97 and 197 mm: the flap
  folded in is narrower); `LayoutOptions.FoldMarks` ("Traits de pliage", in the size section, settings and presets,
  on by default) adds `SheetLayout.FoldMarks`, built as the crop marks in line with each fold and drawn dashed
  (0.8 / 0.6 mm), in the room kept for marks. Not in the Mixed mode (packed as their open size).
- **Text in the band** (`VisualInfo.Caption`, `ImpositionJob.Caption` = `CaptionStyle`, `Core/Pdf/Caption`; asked by the
  author, mock-up accepted): in the thickest white margin of a frame when 8 mm or more (Polaroid 22, Instax 18), centred,
  em = 36 % of the band, shrunk to 88 % of its length, turned with the frame (a frame laid a quarter turn has its band
  on the left). A common text (card "Texte dans la bande", belongs to the job: undo and projects, not the presets) and a
  text per line (`VisualItem.Caption`, empty: the common one). The font: a TrueType font of the PC (`FontList`: the
  registry, .ttf only, bold / italic faces and symbols left out, Segoe Print, Segoe Script, Ink Free, Gabriola, Comic
  Sans first), registered by its file in `WindowsFonts` and embedded by PDFsharp (the text stays vector; a picture only
  when flattened or in JPG); a dark colour (`CaptionColor`, **black by default**: the author's choice). Font and colour are in the settings and the presets.
- **Fill or fit, per visual** (`VisualInfo.Fit`, asked by the author): right click on a line, "As the general setting" /
  "Fill the cell" / "Fit (whole image)" (checked in the menu); `Placement.Compute` and `Dragged` take it before the
  layout's. The line says " · Fitted" or " · Filled" after its resolution; the cropping check follows it. It belongs
  to the line (undo, projects), not to the presets.
- **Colour of the cut lines** (`ImpositionJob.CutLineColor`, `CutLineColors.Rgb`; asked by the author): black, grey,
  white, red, cyan or magenta, from a list under the cut lines box (shown while they are on), in the settings and the
  presets; **grey by default** (the author's choice: seen on light and dark photos). The crop marks stay black (they are on the white paper; the author's choice).
- **Turn by hand** (`VisualInfo.UserRotation`, right click on a file line: right, left, 180°): applied on the sheet
  after the automatic quarter turn, so it turns the visual as shown and the automatic turn never undoes it (a photo
  taken sideways). The thumbnail turns with it, the resolution and cropping checks follow (they read the final angle).
  It belongs to the file line, not to the presets, and stays through every change of the settings; "Automatic
  rotation" (last item of the menu, enabled once turned by hand) gives the automatic turn alone back (0.5.0).
- **Mixed mode** (`MixedPacker`): fewest sheets first (each sheet its own arrangement), then fewest cuts, then the
  smallest block on the last sheet; quarter turns allowed; the current bleed and space apply (area = printable area
  less the bleed, each visual takes its size plus the gap); block centred. Cuts are guillotine cuts, each across the
  piece it cuts: the four edges of the block first (not those on the edge of the sheet: a full lab print needs
  none), then piece by piece, each part trimmed to its visuals; with a space between two parts, one cut on each side
  of it. Numbers on screen only, thin cut lines in the PDF (decided with the author; can be turned off). Crop marks,
  when asked, only in line with the cuts that reach the edge of the block (its edges included: a mark at each
  corner), with the same 3 mm kept around the block as in the grids; a cut inside a piece gets none (the cutting
  plan gives it). The spaces wider than the bleeds are painted white, as in the other modes.
- Instant frames: overall and image sizes are the published ones (Polaroid 88 × 107, image 79 × 79; Instax Mini
  54 × 86 / 46 × 62; Square 72 × 86 / 62 × 62; Wide 108 × 86 / 99 × 62). The top/bottom split of the border is not
  published: it is an estimate (Polaroid 6 / 22 mm, Instax 6 / 18 mm), adjustable in the app.

## Localization

- Every UI text is in `src/PageCompose.App/Properties/Strings.resx` (English) **and** `Strings.fr.resx` (French).
- XAML: `{local:Tr Key}`; code: `Strings.Get("Key")`, `Strings.Format("Key", …)`, `Strings.Count("Key", n)`
  (uses `KeyOne` / `KeyMany`).
- `LocalizationTests` check same keys, same `{0}` placeholders, and that every key used by the code exists:
  keep the call patterns above so the scan finds them.
- `PAGECOMPOSE_LANG=en|fr` forces a language for a test run, `PAGECOMPOSE_THEME=Light|Dark` a theme.

## Precautions

- The only network access is the update check (`api.github.com`, then the installer download from GitHub), described
  in the README privacy policy. The app sends nothing to a printer. Do not add any other access without an explicit
  request.
- Never drop a file into the hot folders of the shop's GP Controller: that prints at once, on expensive paper.
- Shop details (IP addresses, hot folder names) stay out of committed files: they belong in `CLAUDE.local.md`.
- Manual tests of the app open windows and dialogs on the author's screen: keep them short, close the app, and do
  not leave test settings in `%LOCALAPPDATA%\PageCompose\settings.json`, test presets in `presets.json` or test
  projects in `recent.json` (back them up and put them back, as the `.local` scripts do).
- **Never send keys (SendKeys, keyboard events) in a new test**: Windows may refuse to bring the app to the front
  while the author works in another one, and the keys go there (October 15, 2026: a Ctrl+S then Escape went to another
  app). Drive the app with UI Automation only (Invoke, Value, Toggle), which reaches the app and nothing else; open
  a project by the command line rather than through the new Windows file dialog, whose name box has no Value pattern.
  The older `.local` scripts that type into a dialog check that it is in the foreground first (`Wait-Dialog`).
  Never cut the output of a test script (`Select-Object -First`): that stops it before it closes its window and puts
  the author's settings back (October 15: done by hand afterwards).
- Do not commit or push unless the author asks. Never push from the shop PC: no GitHub account may be signed in
  there (the author's wish); commits need no account.
- Commit messages: **no `Co-Authored-By` line for Claude** (the author's choice), whatever a default says. The
  credits of the README stay as they are.

## Commands

- Build: `dotnet build PageCompose.slnx` (no warning expected, the NuGet audit included: a vulnerable package brought by
  another is pinned to a fixed version in the csproj, as `System.Security.Cryptography.Pkcs` 10.0.13 for PDFsharp).
- Tests: `dotnet test tests/PageCompose.Core.Tests`.
- Run: `dotnet run --project src/PageCompose.App` (files or folders given as arguments are added as visuals).
- Full build with installer: `.\scripts\build.ps1` (options `-SkipTests`, `-NoInstaller`), output in `dist\`.
- Icons: `.\scripts\make-icons.ps1` (Windows PowerShell 5.1, System.Drawing); generated files are committed: the app
  icon, the preset file icon (`Assets/PresetFile.ico`: the app's grid on a page of its gradient, chosen by the author
  among four proposals), the project file icon (`Assets/ProjectFile.ico`: that page in front of a paler one, proposal A of
  four) and SVG copies of the 256 px designs in `docs/icons` (not used by the build). Small sizes are
  hand-fitted to whole pixels in the script's tables.
- Check a PDF visually: `gswin64c -q -dNOPAUSE -dBATCH -sDEVICE=png16m -r45 -o page-%d.png file.pdf`
  (needs Ghostscript, installed on the shop PC).
- NuGet source: `nuget.config` (the shop PC has no global NuGet source).
- README screenshots, in English and French (`.fr.png`), the main window at its default size (1360 × 900), light
  theme: `.github/screenshot.png` (Compose mode, three photos 10 × 15 on SRA3, the back shown),
  `.github/screenshot-polaroid.png` (Repeat mode, classic Polaroid on SRA3, margin fields shown) and
  `.github/screenshot-mixed.png` (Mixed mode, 10 × 15, 13 × 18 and 20 × 30 on SRA3, cut numbers shown). Synthetic
  photos only (gradient, sun and hill, in the proportions of their sizes: the author liked them). The script that
  takes them is in `.local\` (see below), from the default settings; `-Preview` saves window pictures to check first.

## Working on several PCs

The project folder lives on a USB drive, so that work can go on at the shop and at home.

- **Claude Code memory is per PC and per path**: what was learnt on one PC is not on the other. Everything needed to
  resume is in this file and in `CLAUDE.local.md` (git-ignored, at the root of the folder, so it travels on the
  drive): where the work stands, the next steps, the decisions taken with the author and how he likes to work.
  Keep both up to date at the end of a session.
- `.local\` (git-ignored) holds the local tools: README screenshots, UI test of the preset import, synthetic test
  pictures, small C# programs on `PageCompose.Core` (`.local\README.md` lists them).
- The drive letter can change (`F:\PageCompose` at the shop): use paths relative to the repository root.
- **Git on the USB drive**: the drive records no file owner, so Git refuses the repository ("dubious ownership")
  until it is allowed, on each PC: `git config --global --add safe.directory <path of the folder>` (the path follows
  the drive letter), or `git -c safe.directory=<path> …` for a single command (used on the shop PC, so as to change
  nothing there). The identity is in the repository's `.git/config` (`user.name` QuentinPerou, his GitHub noreply
  address): it travels with the drive, nothing to set per PC.
- A new PC needs the prerequisites of the README (`winget install Microsoft.DotNet.SDK.10`, Inno Setup 6 for the
  installer). `bin\`, `obj\` and `dist\` are rebuilt on each PC; delete them if a build behaves strangely after a move.

## Releasing

1. Bump `<Version>` in `Directory.Build.props`.
2. Commit, push `main`, tag `vX.Y.Z`, push the tag (from the home PC only, never from the shop PC).
3. `.github/workflows/build.yml` checks that the tag matches the version, builds, tests and publishes
   `PageCompose-Setup-X.Y.Z.exe` in a GitHub Release (a `-suffix` tag is a pre-release).
4. Replace the generated "Full Changelog" notes with a short patch note in English, in the style of CropPdf's
   releases (bold summary line, New / Fixes, Installation with the SmartScreen line, SHA-256 of the installer):
   `gh release edit vX.Y.Z --notes-file <file>`.
