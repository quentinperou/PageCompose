<p align="center">
  <img src="docs/icon-256.png" width="96" alt="PageCompose icon">
</p>

<h1 align="center">PageCompose</h1>

<p align="center">
  Lay out business cards, photo prints and flyers on a sheet, ready to print, in a few clicks.
</p>

<p align="center">
  <b>English</b> · <a href="README.fr.md">Français</a>
</p>

<p align="center">
  <img src=".github/screenshot.png" width="900" alt="PageCompose: three photos laid out on an SRA3 sheet, front and back side by side">
</p>

---

## Features

PageCompose turns your visuals (JPG, PNG, HEIC, WebP, TIFF or PDF) into a **print-ready PDF**: the sheet is filled with as many
copies as fit, with optional crop marks and bleed, ready to cut on a guillotine. Its defaults match the
**FUJIFILM ApeosPro C810 for Photo** (SRA3, A3 and A4 sheets, 4.1 mm unprintable margin), but the PDF works
with any printer that prints at actual size.

- **Three modes**:
  - **Repeat**: one visual repeated over the whole sheet (business cards, flyers…). Enter the quantity in visuals
    or in sheets; the last sheet can be filled, or hold just the visuals asked for.
  - **Same size**: several different visuals of one size on the same sheet (the 8 photos of an order, for instance), each with
    its own number of copies ("For all lines" sets them all at once). The last sheet can be filled by repeating the
    visuals, or keep empty cells.
  - **Mixed sizes**: several sizes on the same sheet (10 × 15, 13 × 18 and 20 × 30 prints of one order, business cards…),
    each visual with its own size (a preset one, or a custom size typed on its line) and copies; **"Duplicate the
    line"** (right click) gives the same photo in another size. They are packed on **as few sheets as possible**, each sheet its own
    arrangement, with straight cuts only (as on a guillotine). The preview **numbers the cuts in cutting order**; the
    numbers are only on screen, the PDF gets thin cut lines (can be turned off) and, if you like, crop marks for the
    cuts that reach the edge of the block. **"Add the cutting plan"** writes, next to each export,
    an A4 PDF to print on plain paper and keep by the guillotine: each sheet with its numbered cuts and the
    measurement of each cut ("101.6 mm from the top": that edge against the back gauge).
- **Drag and drop** files or a whole folder anywhere in the window (or click to choose). Dropping several files
  in Repeat mode switches to Same size.
- **Presets**: save the settings of a usual job under a name (mode, size and margins, sheet, cutting, quantity in
  visuals or in sheets, double-sided, export) and get them back in one click. Files, the number of copies and the
  size of each line in Mixed sizes mode are not part of them (a project keeps them, see below). **Settings → Presets** exports
  them to a `.PageComposePreset` file (all of them in `all-preset.PageComposePreset`, or a single one under its own name) and
  imports them, to copy them to another PC or keep them safe. A **double click** on
  a `.PageComposePreset` file (or dropping it on the window) imports them too; a file of a single preset applies it at once.
  The file is still JSON, readable in Notepad.
- **iPhone photos (HEIC), WebP and TIFF**: Windows converts them when they are added (upright, colours in sRGB) into a
  JPEG of top quality, used as any photo. HEIC needs the "HEIF Image Extensions" and "HEVC Video Extensions" of the
  Microsoft Store, usually already installed; the app says so when they are missing.
- **PDF of several pages**: a page list on the line of the file chooses the page used. Added to a list of visuals,
  the app asks whether to take all its pages (one line each) or one page, chosen in a list. A PDF of two pages (a
  business card) gives its front and its back at once: in Repeat mode, or in a list when Double-sided is ticked.
- **Preset sizes**: business card 85 × 55, ID photo 35 × 45 (eight on a 10 × 15 print), photos 9 × 13 / 10 × 15 / 13 × 18 / 15 × 20 / 20 × 30 (lab sizes, in
  inches: 10 × 15 = 4 × 6 in = 101.6 × 152.4 mm), squares (also 10 × 10 with a 5 mm white border), **instant photo frames** (classic
  Polaroid, Instax Mini, Square and Wide, with their thick border at the bottom), A6 postcard, DL and A5 flyers, A4,
  **folded cards** laid open (A6 folded / A5 open, 10 × 15 folded, square 14 × 14 folded, trifold A4 leaflet) with dashed
  fold marks outside the visuals, or any custom size.
- **White margins**: a white band inside each visual, set side by side (top, bottom, left, right). The framed sizes
  set it themselves; it applies to the front, and to the back too if you tick it. A **text in the band** of a Polaroid
  or an Instax frame (a name, a date): common to the photos or line by line, in a font of the PC (the handwriting ones
  of Windows first) and a dark colour, sized to the band; the font is embedded in the PDF.
- **Sheets**: SRA3 (320 × 450), A3 and A4 for the ApeosPro (4.1 mm unprintable margin), **lab prints** 10 × 15,
  20 × 30 and 30 × 45 (printed to the edge: four 10 × 15 on a 20 × 30), or a **custom sheet** with its own
  unprintable margin.
- **Best fit, automatically**: the layout that holds the most visuals, turning the size a quarter when it fits better
  (a Polaroid frame then takes its photo along, so the thick border stays under it). Each photo is turned a quarter
  when its proportions call for it; a **right click** on its line turns it by hand (right, left, 180°), for a photo
  taken sideways. **Drag a photo on the preview** to choose which part of it is kept when it is cropped (every copy
  follows; "Centre the photo" in its right click menu). The same menu makes one visual **fill its cell or lie whole**
  in it, whatever the general setting ("As the general setting" gives it back).
- **Cutting**: edge to edge (a single cut between two visuals), short **crop marks**, **bleed** (2 mm by default: the
  photo is enlarged to cover it, or keeps its size with its edges **mirrored** into the bleed),
  an optional **space between visuals** (5 mm for instance), and **continuous cut lines** across the whole block, in
  the colour of your choice (black, grey, white for dark photos, red, cyan or magenta).
- **Double-sided**: each back is placed so that it lands behind its front (long or short edge flip, optional 180°
  turn). In a list, **each visual may have its own back**, on its line (drop it on its "Back" slot); the others get
  the **shared back**. The app finds them itself (an "auto" badge says so): page 2 of a PDF of two pages, or
  `-recto` / `-verso` (`front` / `back`) files added together. The preview shows **both sides next to each other**.
- **True preview**: what you see is the exported PDF, rendered by Windows, with the unprintable area shown in red.
  **Zoom** with the magnifier buttons, Ctrl + wheel or a double click (move around with the middle button, or Alt +
  click and drag), shown in % of the real size; the fit button brings the whole sheet back in view.
- **Export as PDF or as JPG**: the PDF keeps the photos as they are (no recompression) and the designer PDFs as
  vectors; **"Flatten the PDF"** turns each sheet side into a single 300 dpi picture, leaving nothing for the printer
  to interpret. **"Export as JPG"** writes one 300 dpi picture per sheet side, handy for a photo lab. A notification
  then offers to open the file (the first picture of a JPG export) or show it in its folder.
- **Sheet mark** (optional): the name of the file, the date and "sheet 2/5" in small print outside the visuals, cut off
  with the waste, to sort the sheets at the guillotine.
- **Projects saved to print again**: "Save the project" writes a `.PageComposeProject` file next to each export, holding a
  copy of its files and every setting (copies, sizes, backs, reframing, turns); **"Export the project"** writes it without
  exporting the sheets. A double click on it opens the project as it was, on any PC, in a window of its own; dropped
  on a window that already holds work, it asks whether to open it in a new window or in place of that work (Ctrl+Z
  gives it back).
- **Undo, redo**: Ctrl+Z and Ctrl+Y (or Ctrl+Shift+Z) undo and redo the changes of the settings and of the list.
- **Number fields**: the Up and Down arrows change the value (Shift: by 10); "0.1" and "0,1" both work.
- **Checks**: effective resolution of each visual at the chosen size (warning below 200 dpi), cropping when the
  proportions differ from the size. The notices fit on one line under the preview, which keeps its size: the worst
  one gives its colour, and a click shows them all.
- **Smart with files**: phone photos are turned upright (EXIF), a designer PDF with built-in bleed is placed at
  100 % on its trim box, JPEG files are embedded without recompression and stored only once in the PDF (a JPEG from
  the web that the PDF library refuses is converted by Windows instead). A
  non-standard PDF that cannot be imported (some billing software makes them) is converted to a 300 dpi image,
  with a note saying so.
- **English and French**: the app and the installer follow the Windows display language; the app language can
  also be chosen in **Settings** (gear icon).
- **Windows 11 look** (Fluent, Mica), **light or dark**: it follows the Windows mode, or the theme chosen in Settings.
- **Updates in one click**: the app tells you when a new version is published on GitHub and installs it for you
  (stable or beta channel, check at startup can be turned off).

Examples on an SRA3 sheet:

| Size | Edge to edge | With 2 mm bleed |
|------|:---:|:---:|
| Business card 85 × 55 | 25 | 21 |
| ID photo 35 × 45 | 72 | 66 |
| Photo 10 × 15 | 8 | 4 |
| Photo 9 × 13 | 9 | 9 |
| Photo 13 × 18 | 4 | 4 |
| Classic Polaroid 88 × 107 | 12 | 9 |
| Instax Mini 54 × 86 | 25 | 21 |
| A6 postcard | 8 | 6 |

<p align="center">
  <img src=".github/screenshot-polaroid.png" width="900" alt="Twelve classic Polaroid frames on an SRA3 sheet, with the white margins of the frame">
</p>

In Mixed sizes mode, an order of four 10 × 15, two 13 × 18 and one 20 × 30 takes 2 SRA3 sheets, where one sheet per size
takes 3:

<p align="center">
  <img src=".github/screenshot-mixed.png" width="900" alt="Mixed sizes mode: 10 x 15 and 20 x 30 prints packed on an SRA3 sheet, the cuts numbered in cutting order">
</p>

### Printing the PDF

The PDF is exactly the size of the sheet and already holds the layout: print it at **actual size (100 %)**, without
any scaling or imposition from the printer driver or the print server. With a FUJIFILM GP Controller, drop it in a
hot folder set to the real size and no imposition. A lab print sheet goes to the lab at its size, borderless.

## Installation

1. Download `PageCompose-Setup-x.y.z.exe` from the [Releases](../../releases) page.
2. Run it. As the installer is not signed, Windows SmartScreen may show “Windows protected your PC”:
   click **More info → Run anyway**.
3. Accept the administrator prompt (UAC).

The installer:

- copies the app to `C:\Program Files\PageCompose` and adds it to the Start menu (desktop shortcut optional);
- associates the `.PageComposePreset` preset files and the `.PageComposeProject` projects with PageCompose (their icons, and
  the double click that imports or opens them);
- offers to install the Microsoft **.NET 10 Desktop Runtime** if it is missing.

Later versions install from the app: when one is available, a bar offers to **Update** (or use **Settings →
Updates**). The new installer is downloaded from GitHub, checked against the SHA-256 published by GitHub, installed
silently, and PageCompose starts again.

To uninstall: **Settings → Apps → Installed apps → PageCompose**. The uninstaller removes the app, its temporary
files and its file associations, and asks whether to delete your settings and presets too (No by default, to find them again after a
reinstall).

**Requirements:** Windows 10 2004 or later, x64 (made for Windows 11).

## Development

### Prerequisites

Everything installs with `winget` (built into Windows 11), from a terminal:

| Tool | Purpose | Install |
|------|---------|---------|
| **Git** | Get the code | `winget install Git.Git` |
| **.NET 10 SDK** | Build the app (C# / WPF) and run the tests | `winget install Microsoft.DotNet.SDK.10` |
| **Inno Setup 6** | Build the installer | `winget install JRSoftware.InnoSetup` |

Any editor (Visual Studio, VS Code, Rider) is fine for the C# code.

### Build

```powershell
.\scripts\build.ps1
```

The script runs the tests, then produces in `dist\`:

| File | Content |
|------|---------|
| `app\` | the app (`PageCompose.exe`…), framework-dependent |
| `PageCompose-Setup-<version>.exe` | the installer |

Options: `-SkipTests`, `-NoInstaller`.

### While developing

```powershell
dotnet test tests\PageCompose.Core.Tests                            # unit tests
dotnet run --project src\PageCompose.App -- "C:\path\to\photos"     # run the app with files or a folder
```

Set the `PAGECOMPOSE_LANG` environment variable to `en` or `fr` before starting the app to check the other
language without changing any setting, and `PAGECOMPOSE_THEME` to `Light` or `Dark` for the other theme.

### Code layout

```
src/
  PageCompose.Core/   Logic without UI: layout computation, packing of mixed sizes (guillotine cuts),
                      placement, distribution over the sheets, reading the visuals (size, EXIF, PDF
                      TrimBox), writing the PDF (PDFsharp), list of the GitHub releases (updates)
  PageCompose.App/    WPF app: settings panel, preview (Windows.Data.Pdf), drag and drop, HEIC / WebP / TIFF
                      conversion, projects, undo, settings, updates, UI strings
installer/            Inno Setup script of the installer
scripts/              build.ps1 (full build), make-icons.ps1 (regenerates the icons)
tests/                Unit tests (xUnit)
docs/                 README icon, SVG icons (docs/icons)
.github/workflows/    Automated build on GitHub (and release publishing)
```

### Translations

| What | Where |
|------|-------|
| App texts | `src/PageCompose.App/Properties/Strings.resx` (English, the default) and `Strings.fr.resx` (French). The tests check that both have the same keys and placeholders, and that every key used by the code exists. |
| Installer messages | `[Languages]` and `[CustomMessages]` in `installer/PageCompose.iss` |
| Languages offered in Settings | `AppSettings.SupportedLanguages` in `src/PageCompose.App/Services/AppSettings.cs` |

Adding a language means updating these three places, plus a `README.<language>.md` if you like.

### Icons

`scripts\make-icons.ps1` draws the icon (`src/PageCompose.App/Assets/PageCompose.ico`, its 256 px image for the app,
and `docs/icon-256.png`), the icons of the preset files and of the projects
(`src/PageCompose.App/Assets/PresetFile.ico`, `ProjectFile.ico`, installed next to the app) and SVG copies of them
(`docs/icons/PageCompose.svg`, `PresetFile.svg`, `ProjectFile.svg`) to rework the designs in a vector editor; the small sizes of the `.ico` files are fitted to whole pixels by the script. The generated files are
committed: run it again only to change the design.

### Releasing a version

1. Update `<Version>` in `Directory.Build.props`.
2. Commit, then create and push a tag: `git tag v1.0.0`, then `git push origin v1.0.0`.
3. The GitHub Actions workflow builds, tests and publishes the installer in a Release.

A tag with a suffix (`v1.1.0-beta.1`) is published as a *pre-release*: only the apps set to the **Beta** update channel
offer it. Installed apps find a new version at their next start.

## Roadmap

- [x] Several sizes on the same sheet (Mixed sizes mode, 0.5.0).
- [ ] **Free signing** of the installer through [SignPath Foundation](https://signpath.org) (for open source projects): no more SmartScreen warning.

## Privacy policy

Your visuals and PDF files never leave your computer.

The only network access is the **update check**: when the app starts (if *Check for updates at startup* is on, the
default) and when you click *Check for updates*, it sends one HTTPS request to the GitHub API (`api.github.com`) to
read the list of PageCompose releases. No file, visual or personal data is sent; like any web request, GitHub sees
your IP address, and the app version in the User-Agent. When you accept an update, its installer is downloaded from
GitHub. Turn off the check at startup in **Settings → Updates** and the app makes no request unless you ask for one.

The links in Settings (*QuentinPerou*, *Source code on GitHub*, *What's new*) open in your browser only when you
click them.

## License

[MIT](LICENSE.md). Uses [PDFsharp](https://github.com/empira/PDFsharp) (MIT).

## Credits

Developed by [QuentinPerou](https://github.com/quentinperou), with [Claude Code](https://claude.com/claude-code) (Anthropic).
