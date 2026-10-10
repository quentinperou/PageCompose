# CLAUDE.md

Guidance for Claude Code and contributors working on this repository. `README.md` (English) and
`README.fr.md` (French) describe the project for users.

## The project

**PageCompose**: a small open-source (MIT) Windows 11 app that lays out visuals (JPG, PNG, PDF) on a sheet and
exports a **print-ready PDF** (step and repeat, crop marks, bleed, double-sided). Made for a photo shop printing on a
**FUJIFILM ApeosPro C810 for Photo** (SRA3 / A3 / A4 sheets, 4.1 mm unprintable margin). UI in English and French.
Author: [QuentinPerou](https://github.com/quentinperou). Planned repository: https://github.com/quentinperou/PageCompose
(not published yet: the author commits and publishes himself). Credits wording: "Developed by QuentinPerou, with
Claude Code (Anthropic)".

**Talk to the author in French.** Code, comments, commit messages and these docs are in English.

## Project status (October 10, 2026)

Version **0.4.9** works end to end, developed and tested on Windows 11 (build 26200), and is installed on the shop
PC. 0.4.9 drops two notices that repeated the settings column (empty cells, "Fit" mode). 0.4.8 stops photos from turning in some Polaroid frames and not others (a square image area within 1 µm turns
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

- Two modes: **Repeat** (one visual over the whole sheet, quantity in visuals or sheets) and **Compose** (several
  visuals, each with its own copies). In both, "Fill the last sheet" is optional (off: exactly the quantity asked).
- Drag and drop of files and folders, shared back (double-sided) with the front and back previews side by side,
  sheet navigation, resolution and cropping checks, export.
- English/French UI with live switching from Settings (the main window is rebuilt on the same view model).
- Windows 11 Fluent theme, light or dark: follows Windows, or forced from Settings (applied live).
- Updates from GitHub Releases, as in CropPdf: check at startup (can be turned off), Stable/Beta channel, bar in the
  main window, download checked with SHA-256, silent install and relaunch. The Settings window copies CropPdf's
  (language, theme, updates, about).
- 585 unit tests pass. `scripts\build.ps1` builds `dist\PageCompose-Setup-0.4.9.exe` locally (Inno Setup 6).
- The installer is not signed: the UAC prompt shows "Unknown publisher" (QuentinPerou appears in the file details
  and in Installed apps). Left as is for now (October 10, 2026). A paid certificate would show the author's legal
  name, SignPath Foundation its own name; only a self-signed certificate trusted by hand on each PC shows "QuentinPerou".
- First commit on October 10, 2026 (branch `main`), made by Claude at the author's request, as QuentinPerou with his
  GitHub noreply address, set in the repository's own config. Not done yet: GitHub repository, push, CI run,
  release. Until the repository exists, the update check gets a 404 and says "no version has been published on
  GitHub yet". The project started as "Apeos Print" (layout +
  direct printing to the ApeosPro); direct printing was dropped on October 10, 2026: nothing is ever sent to a printer.

## Architecture

- `src/PageCompose.Core` (net10.0, no UI, **all the logic, unit-tested**):
  - `Layout/LayoutEngine`: step and repeat layout (both orientations tried, block centred, crop marks);
  - `Layout/Placement`: a visual in its cell (fill / fit, auto quarter turn, EXIF, back orientation, PDF on its TrimBox);
  - `Layout/SheetFiller`: visuals assigned to the cells, sheet by sheet;
  - `Visuals/VisualLoader`: JPEG/PNG headers read without decoding, EXIF orientation, PDF through PDFsharp;
    errors are `VisualLoadException` with a `VisualLoadError` code that the UI translates;
  - `Pdf/ImpositionPdfWriter`: writes the PDF (PDFsharp 6.2.4, MIT). `Pdf/FlatPdfWriter`: the flattened PDF, one
    JPEG picture per page, embedded as it is.
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
    (DWM system backdrop); their XAML `ApplicationBackgroundBrush` stays as the fallback elsewhere. The settings
    column is transparent on it (only its cards stand out), the preview sits on `LayerFillColorDefaultBrush` with a
    rounded top-left corner, like the content of Windows 11 apps. Screen copies are needed to see Mica (PrintWindow
    does not draw it): take them only when PageCompose is in the foreground, never another application.
  - `Services/Updater`: check at startup, download to `%TEMP%\PageCompose-Update`, run the installer with
    `/SP- /SILENT /NORESTART /CLOSEAPPLICATIONS /FORCECLOSEAPPLICATIONS /RELAUNCH=1` (the `.iss` relaunches the app).
  - `ViewModels/MainViewModel`: modes, `VisualItem` list (thumbnail, copies, dpi), recomputed on every change;
    the preview is regenerated 200 ms after the last change, for the displayed sheet only.
  - **The preview is the real PDF**, rendered by `Windows.Data.Pdf` (`Services/PdfPreviewRenderer`): no drawing
    logic is duplicated in the UI.
  - **Preview zoom** (`MainWindow.xaml.cs`, "Preview zoom"): zoom 1 = the sheets fit the area (Viewbox, no scroll
    bars); above 1 the sides are laid out that many times larger in `PreviewScroll`. Buttons, Ctrl + wheel around the
    mouse, Ctrl + / - / 0, double click; drag to move. The label is in % of the real size. The view tells the view
    model how wide the sheet is on screen (`SetPreviewDisplayWidth`), which renders the preview from 1400 up to
    3600 px wide, in steps of 400.
  - Dialogs, drag and drop and the update bar in `MainWindow.xaml.cs`; `SettingsWindow` (language, theme,
    updates, about), reopened after a language change.
  - `Services/PdfRasterizer`: a PDF that PDFsharp cannot read (non-standard files, e.g. an encrypted invoice
    whose Info strings have unescaped parentheses) is rendered by `Windows.Data.Pdf` to a 300 dpi PNG in
    `%TEMP%\PageCompose\Converted` (files older than a day deleted at startup: another window may be using its
    own) and used as an image; `VisualInfo.SourcePath` keeps the
    original file for the name, the folder and the "PDF · … px" line.
  - Exports ("Export as PDF" / "Export as JPG": same "as" on both buttons, "JPG" everywhere in the UI): the PDF as
    it is; "Flatten the PDF" and "Export as JPG" render each page of that PDF with
    `Windows.Data.Pdf` at 300 dpi (`Services/SheetExporter`: JPEG quality 95, 300 dpi in the file), then
    `FlatPdfWriter` gathers them for the flattened PDF. JPEG files: the chosen name for a single picture, else
    `name-f1.jpg`, `name-f1-verso.jpg`… The result shows in a toast over the preview (`ViewModels/ExportToast`,
    open the first file / show in folder, hidden after 8 s), never as a long status text (the author found it ugly).
    There is no "open after export" option: the toast offers it (removed at the author's request).
  - **Presets** (`Services/LayoutPreset` + `PresetStore`): the settings of a job under a name, never the files nor
    the number of copies (decided with the author); the unit of the quantity, visuals or sheets, is kept
    (`LayoutPreset.Unit`, asked by the author; older presets read as visuals). The unit is also kept in the settings. Stored in their own `%LOCALAPPDATA%\PageCompose\presets.json`, read
    again before each change (several windows), damaged copy kept aside. The list shows a preset only while the
    settings match it: a manual change clears the selection, so picking it again applies it again. Settings →
    Presets exports them (`{"Format": "PageCompose presets", "Version": 1, "Presets": [...]}`) and imports them
    (a copy of presets.json works too; the same name replaces). The export takes all of them
    (`all-preset.PageCompose`, the author's name) or the one chosen in a list (`<preset name>.PageCompose`,
    `PresetStore.ExportFileName`). Exported files end in **`.PageCompose`**
    (`PresetStore.FileExtension`, capitals wanted by the author; still plain JSON). The installer associates the
    extension (`[Registry]`, ProgID `PageCompose.Presets`, icon `{app}\PresetFile.ico`): a double click starts the
    app with the file, and `MainWindow.AddPathsAsync` imports it (also when dropped on the window); a file of one
    preset applies it, a toast says what came in. The import dialog still accepts the `.json` files of 0.4.3.
  - **Uninstall** (`installer/PageCompose.iss`): removes the `.PageCompose` association, `%TEMP%\PageCompose` and
    `%TEMP%\PageCompose-Update`, then asks whether to delete `%LOCALAPPDATA%\PageCompose` (settings, presets), No by
    default; a silent uninstall keeps them. The .NET runtime stays (shared).
  - **PDF pages**: `VisualInfo.PageNumber` / `PageCount`; a page list on the file line (`VisualItem.Pages`) when
    there are several. A PDF of exactly two pages in Repeat mode sets page 2 as the back.
  - Every text box holds a number: an implicit style gives them `NumericInput` (Up / Down arrows, Shift: by 10,
    `NumericInput.Minimum`; "." and "," both become the decimal separator of the PC) and `TextWrapping="Wrap"`, which
    hides the Fluent clear button (it hid the digits of narrow boxes). A text box for text sets `NumericInput.Step="0"`. The settings column's `ScrollViewer` is not focusable, or a keyboard focus frame shows
    around the whole column.
  - Settings column: the title, the Settings button and the presets are fixed at the top (asked by the author), the
    export buttons at the bottom, each with a divider; only the settings between them scroll.
  - **Notices under the preview** (`MainViewModel.AddVisualNotices`): only what the settings column does not already
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
  reference table did not change. Business card: 85 × 55.
- Crop marks need 3 mm around the block: on a lab print, where the visuals reach the edges, they cost visuals; the
  app then says how many fit without them.
- Both orientations are tried; on a tie the requested one is kept.
- A visual turns a quarter when its proportions oppose those of its area (`Placement`); an area square within 1 µm
  turns nothing: cell edges are sums, and rounding used to make the 79 × 79 Polaroid image area "taller" in some
  cells only, so one photo turned when the space between visuals changed.
- Images: embedded **without recompression**, once per PDF; EXIF orientation applied; warning below 200 dpi,
  error below 150 dpi.
- A PDF with a TrimBox is placed at 100 % on the trim and clipped to the layout bleed.
- Back: cells mirrored according to the flip (long edge: vertical axis; short edge: horizontal axis). Crop marks
  and continuous cut lines (`ImpositionJob.CutLines`: every cut, across the block of trims) on the front only.
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
- **Turn by hand** (`VisualInfo.UserRotation`, right click on a file line: right, left, 180°): applied on the sheet
  after the automatic quarter turn, so it turns the visual as shown and the automatic turn never undoes it (a photo
  taken sideways). The thumbnail turns with it, the resolution and cropping checks follow (they read the final angle).
  It belongs to the file line, not to the presets.
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
  not leave test settings in `%LOCALAPPDATA%\PageCompose\settings.json` or test presets in `presets.json` (back them
  up and put them back, as the `.local` scripts do).
- Do not commit or push unless the author asks. Never push from the shop PC: no GitHub account may be signed in
  there (the author's wish); commits need no account.
- Commit messages: **no `Co-Authored-By` line for Claude** (the author's choice), whatever a default says. The
  credits of the README stay as they are.

## Commands

- Build: `dotnet build PageCompose.slnx` (no warning expected).
- Tests: `dotnet test tests/PageCompose.Core.Tests`.
- Run: `dotnet run --project src/PageCompose.App` (files or folders given as arguments are added as visuals).
- Full build with installer: `.\scripts\build.ps1` (options `-SkipTests`, `-NoInstaller`), output in `dist\`.
- Icons: `.\scripts\make-icons.ps1` (Windows PowerShell 5.1, System.Drawing); generated files are committed: the app
  icon, the preset file icon (`Assets/PresetFile.ico`: the app's grid on a page of its gradient, chosen by the author
  among four proposals) and SVG copies of the 256 px designs in `docs/icons` (not used by the build). Small sizes are
  hand-fitted to whole pixels in the script's tables.
- Check a PDF visually: `gswin64c -q -dNOPAUSE -dBATCH -sDEVICE=png16m -r45 -o page-%d.png file.pdf`
  (needs Ghostscript, installed on the shop PC).
- NuGet source: `nuget.config` (the shop PC has no global NuGet source).
- README screenshots, in English and French (`.fr.png`), the main window at its default size (1360 × 900), light
  theme: `.github/screenshot.png` (Compose mode, three photos 10 × 15 on SRA3, the back shown) and
  `.github/screenshot-polaroid.png` (Repeat mode, classic Polaroid on SRA3, margin fields shown). The script that
  takes them is in `.local\` (see below), from the default settings; the current ones show version 0.4.4.

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
2. Commit, tag `vX.Y.Z`, push the tag.
3. `.github/workflows/build.yml` checks that the tag matches the version, builds, tests and publishes
   `PageCompose-Setup-X.Y.Z.exe` in a GitHub Release (a `-suffix` tag is a pre-release).
