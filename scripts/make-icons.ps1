<#
.SYNOPSIS
    Generates the application icon (.ico), its large in-app image, the README icon (.png), the icons of the
    preset files (.PageComposePreset) and of the project files (.PageComposeProject), and SVG copies of the designs
    (docs\icons).
.DESCRIPTION
    The generated files are committed: run this script again only to change the design.
    Design: a white 2 x 3 grid of visuals (one sheet, step and repeat) on a rounded square with a
    turquoise-to-blue gradient. A preset file is the same grid on a page of that gradient, with a folded corner; an
    project file, that page in front of a paler one.
    Small sizes are drawn on whole pixels to stay sharp.
    Requires Windows PowerShell 5.1 (System.Drawing).
#>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot

$gradientStart = [System.Drawing.Color]::FromArgb(255, 0x2C, 0xC5, 0xB5)
$gradientEnd = [System.Drawing.Color]::FromArgb(255, 0x16, 0x60, 0xC9)

function New-RoundedRectPath([System.Drawing.RectangleF]$r, [float]$radius) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    if ($radius -le 0) { $p.AddRectangle($r); return $p }
    $d = [Math]::Min($radius * 2, [Math]::Min($r.Width, $r.Height))
    $p.AddArc($r.X, $r.Y, $d, $d, 180, 90)
    $p.AddArc($r.Right - $d, $r.Y, $d, $d, 270, 90)
    $p.AddArc($r.Right - $d, $r.Bottom - $d, $d, $d, 0, 90)
    $p.AddArc($r.X, $r.Bottom - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-Canvas([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    return @{ Bitmap = $bmp; Graphics = $g }
}

function Fill-Tiles([System.Drawing.Graphics]$g, $tiles, [float]$radius) {
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    foreach ($t in $tiles) {
        $path = New-RoundedRectPath $t $radius
        $g.FillPath($white, $path)
        $path.Dispose()
    }
    $white.Dispose()
}

# ---------------------------------------------------------------- App icon

# The 256 px design: tiles 60 x 48, 12 px apart, background radius 23 % of its side, inside a 4 % transparent margin.
$appDesign = @{ Xs = @(62, 134); Ys = @(44, 104, 164); W = 60; H = 48; R = 9; Bg = 0.23; Margin = 0.04 }

# Small sizes, tuned by hand on whole pixels: background radius, column x, tile width, row y, tile height, tile radius.
$hinted = @{
    16 = @{ Bg = 3.5; Xs = @(3, 9); W = 4; Ys = @(2, 6, 10); H = 3; R = 0.5 }
    20 = @{ Bg = 4.5; Xs = @(4, 11); W = 5; Ys = @(3, 8, 13); H = 4; R = 0.5 }
    24 = @{ Bg = 5.5; Xs = @(5, 13); W = 6; Ys = @(4, 10, 16); H = 4; R = 1 }
    32 = @{ Bg = 7.5; Xs = @(7, 17); W = 8; Ys = @(5, 13, 21); H = 6; R = 1.5 }
    40 = @{ Bg = 9.5; Xs = @(9, 21); W = 10; Ys = @(6, 16, 26); H = 8; R = 1.5 }
}

function New-IconBitmap([int]$size) {
    $canvas = New-Canvas $size
    $g = $canvas.Graphics

    if ($hinted.ContainsKey($size)) {
        $spec = $hinted[$size]
        $m = 0.0
        $s = [float]$size
        $bgRadius = [float]$spec.Bg
        $tiles = foreach ($y in $spec.Ys) { foreach ($x in $spec.Xs) { New-Object System.Drawing.RectangleF $x, $y, $spec.W, $spec.H } }
        $tileRadius = [float]$spec.R
    }
    else {
        $m = [float]($size * $appDesign.Margin)
        $s = [float]($size - 2 * $m)
        $bgRadius = [float]($s * $appDesign.Bg)
        $k = $s / 256
        $tiles = foreach ($y in $appDesign.Ys) { foreach ($x in $appDesign.Xs) { New-Object System.Drawing.RectangleF ($m + $x * $k), ($m + $y * $k), ($appDesign.W * $k), ($appDesign.H * $k) } }
        $tileRadius = [float]($appDesign.R * $k)
    }

    $rect = New-Object System.Drawing.RectangleF $m, $m, $s, $s
    $bg = New-RoundedRectPath $rect $bgRadius
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $gradientStart, $gradientEnd, ([float]45)
    $g.FillPath($brush, $bg)
    Fill-Tiles $g $tiles $tileRadius

    $brush.Dispose(); $bg.Dispose(); $g.Dispose()
    return $canvas.Bitmap
}

# ---------------------------------------------------------------- Preset file icon

# By size, on whole pixels: page (x, y, width, height), folded corner, page corner radius, column x, tile width,
# row y, tile height, tile radius. The 256 px design: tiles 60 x 40, 8 px apart across, 12 px down.
$fileSpecs = @{
    16 = @{ Page = @(1, 0, 14, 16); Fold = 4; R = 0; Xs = @(3, 9); W = 4; Ys = @(4, 8, 12); H = 3; TR = 0 }
    20 = @{ Page = @(2, 0, 16, 20); Fold = 5; R = 1; Xs = @(4, 11); W = 5; Ys = @(6, 11, 16); H = 3; TR = 0 }
    24 = @{ Page = @(2, 0, 20, 24); Fold = 6; R = 1.5; Xs = @(5, 13); W = 6; Ys = @(7, 13, 19); H = 4; TR = 0.5 }
    32 = @{ Page = @(3, 0, 26, 32); Fold = 8; R = 2; Xs = @(7, 17); W = 8; Ys = @(11, 18, 25); H = 5; TR = 1 }
    40 = @{ Page = @(4, 0, 32, 40); Fold = 10; R = 2.5; Xs = @(9, 21); W = 10; Ys = @(13, 22, 31); H = 7; TR = 1 }
    48 = @{ Page = @(5, 0, 38, 48); Fold = 12; R = 3; Xs = @(11, 25); W = 12; Ys = @(16, 27, 38); H = 8; TR = 1.5 }
    64 = @{ Page = @(6, 0, 52, 64); Fold = 16; R = 4; Xs = @(15, 33); W = 16; Ys = @(22, 36, 50); H = 11; TR = 2 }
    256 = @{ Page = @(36, 8, 184, 240); Fold = 60; R = 14; Xs = @(64, 132); W = 60; Ys = @(92, 144, 196); H = 40; TR = 8 }
}

# A page of the gradient (alpha: 255 opaque), with rounded corners and its top right one folded; returns its outline
# (only the outline, drawing nothing, when there is no graphics).
function Add-Page([System.Drawing.Graphics]$g, $rect, [float]$f, [float]$r, [int]$alpha = 255) {
    $x, $y, $w, $h = [float[]]$rect
    $d = [float](2 * $r)
    $x1 = $x + $w; $y1 = $y + $h

    # The page: rounded corners, the top right one cut by the fold.
    $page = New-Object System.Drawing.Drawing2D.GraphicsPath
    if ($d -gt 0) { $page.AddArc($x, $y, $d, $d, 180, 90) }
    $page.AddLine(($x + $d / 2), $y, ($x1 - $f), $y)
    $page.AddLine(($x1 - $f), $y, $x1, ($y + $f))
    $page.AddLine($x1, ($y + $f), $x1, ($y1 - $d / 2))
    if ($d -gt 0) { $page.AddArc(($x1 - $d), ($y1 - $d), $d, $d, 0, 90) }
    $page.AddLine(($x1 - $d / 2), $y1, ($x + $d / 2), $y1)
    if ($d -gt 0) { $page.AddArc($x, ($y1 - $d), $d, $d, 90, 90) }
    $page.CloseFigure()

    # The fold, lighter; its inner corner is rounded like the page corners.
    $fold = New-Object System.Drawing.Drawing2D.GraphicsPath
    $fr = [float][Math]::Min($r * 0.75, $f / 4)
    $fold.AddLine(($x1 - $f), $y, ($x1 - $f), ($y + $f - $fr))
    if ($fr -gt 0) { $fold.AddArc(($x1 - $f), ($y + $f - 2 * $fr), (2 * $fr), (2 * $fr), 180, -90) }
    $fold.AddLine(($x1 - $f + $fr), ($y + $f), $x1, ($y + $f))
    $fold.CloseFigure()

    if ($null -eq $g) { $fold.Dispose(); return $page }
    $pageRect = New-Object System.Drawing.RectangleF $x, $y, $w, $h
    $from = [System.Drawing.Color]::FromArgb($alpha, $gradientStart); $to = [System.Drawing.Color]::FromArgb($alpha, $gradientEnd)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $pageRect, $from, $to, ([float]60)
    $light = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(110, 255, 255, 255))
    $g.FillPath($brush, $page)
    $g.FillPath($light, $fold)
    $light.Dispose(); $brush.Dispose(); $fold.Dispose()
    return $page
}

function New-FileIconBitmap([int]$size) {
    $spec = $fileSpecs[$size]
    $canvas = New-Canvas $size
    $g = $canvas.Graphics
    $page = Add-Page $g $spec.Page ([float]$spec.Fold) ([float]$spec.R)

    $tiles = foreach ($ty in $spec.Ys) { foreach ($tx in $spec.Xs) { New-Object System.Drawing.RectangleF $tx, $ty, $spec.W, $spec.H } }
    Fill-Tiles $g $tiles ([float]$spec.TR)

    $page.Dispose(); $g.Dispose()
    return $canvas.Bitmap
}

# ---------------------------------------------------------------- Project file icon

# A project (.PageComposeProject) is a pile: the preset page, smaller, in front of a paler one, up and to the right (chosen
# by the author among four proposals). By size, on whole pixels: back page and front page (x, y, width, height),
# folded corner, page corner radius, gap between the pages, then the grid of the front page as for the presets.
$projectSpecs = @{
    16 = @{ Back = @(6, 0, 10, 13); Front = @(1, 3, 11, 13); Fold = 3; R = 0; Gap = 1; Xs = @(3, 7); W = 3; Ys = @(7, 10, 13); H = 2; TR = 0 }
    20 = @{ Back = @(7, 0, 12, 16); Front = @(1, 4, 14, 16); Fold = 4; R = 0; Gap = 1; Xs = @(3, 9); W = 4; Ys = @(9, 12, 15); H = 2; TR = 0 }
    24 = @{ Back = @(8, 0, 15, 19); Front = @(1, 5, 17, 19); Fold = 5; R = 1; Gap = 1; Xs = @(3, 10); W = 5; Ys = @(11, 15, 19); H = 3; TR = 0.5 }
    32 = @{ Back = @(9, 0, 22, 27); Front = @(2, 5, 22, 27); Fold = 7; R = 2; Gap = 1; Xs = @(5, 14); W = 7; Ys = @(14, 20, 26); H = 4; TR = 1 }
    40 = @{ Back = @(11, 0, 27, 34); Front = @(2, 6, 28, 34); Fold = 9; R = 2; Gap = 2; Xs = @(6, 17); W = 9; Ys = @(17, 24, 31); H = 5; TR = 1 }
    48 = @{ Back = @(13, 0, 33, 40); Front = @(2, 8, 34, 40); Fold = 11; R = 2.5; Gap = 2; Xs = @(7, 20); W = 11; Ys = @(20, 28, 36); H = 6; TR = 1.5 }
    64 = @{ Back = @(18, 0, 44, 54); Front = @(3, 10, 45, 54); Fold = 14; R = 3.5; Gap = 3; Xs = @(9, 26); W = 14; Ys = @(27, 38, 49); H = 8; TR = 2 }
    256 = @{ Back = @(68, 8, 164, 216); Front = @(28, 36, 164, 216); Fold = 52; R = 13; Gap = 8; Xs = @(53, 115); W = 52; Ys = @(112, 158, 204); H = 36; TR = 7 }
}

# Opacity of the page behind.
$projectBackAlpha = 130

function New-ProjectIconBitmap([int]$size) {
    $spec = $projectSpecs[$size]
    $canvas = New-Canvas $size
    $g = $canvas.Graphics
    $back = Add-Page $g $spec.Back ([float]$spec.Fold) ([float]$spec.R) $projectBackAlpha

    # A clear gap around the front page, so that both pages stand apart on a light or a dark background.
    $outline = Add-Page $null $spec.Front ([float]$spec.Fold) ([float]$spec.R)
    $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $clear = New-Object System.Drawing.Pen ([System.Drawing.Color]::Transparent), ([float](2 * $spec.Gap))
    $g.DrawPath($clear, $outline)
    $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver

    $front = Add-Page $g $spec.Front ([float]$spec.Fold) ([float]$spec.R)
    $tiles = foreach ($ty in $spec.Ys) { foreach ($tx in $spec.Xs) { New-Object System.Drawing.RectangleF $tx, $ty, $spec.W, $spec.H } }
    Fill-Tiles $g $tiles ([float]$spec.TR)

    $clear.Dispose(); $outline.Dispose(); $front.Dispose(); $back.Dispose(); $g.Dispose()
    return $canvas.Bitmap
}

# ---------------------------------------------------------------- Files

function Get-PngBytes([System.Drawing.Bitmap]$bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    return $ms.ToArray()
}

function Save-Png([int]$size, [string]$path) {
    New-Item -ItemType Directory -Force (Split-Path -Parent $path) | Out-Null
    $bmp = New-IconBitmap $size
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "  $path"
}

# ICO file made of PNG-compressed entries (supported since Windows Vista).
function Save-Ico([int[]]$sizes, [string]$path, [scriptblock]$draw = { param($size) New-IconBitmap $size }) {
    $images = foreach ($size in $sizes) { $b = & $draw $size; , (Get-PngBytes $b); $b.Dispose() }
    $fs = [System.IO.File]::Create($path)
    $bw = New-Object System.IO.BinaryWriter $fs
    $bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
        $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([UInt16]1); $bw.Write([UInt16]32)
        $bw.Write([UInt32]$images[$i].Length); $bw.Write([UInt32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($img in $images) { $bw.Write([byte[]]$img) }
    $bw.Dispose()
    Write-Host "  $path"
}

# ---------------------------------------------------------------- SVG copies

# The 256 px designs as SVG (docs\icons), to rework the icons in any vector editor. The app does not use them, and
# the small sizes stay drawn on whole pixels by this script.
function Get-Hex([System.Drawing.Color]$c) { '#' + $c.R.ToString('X2') + $c.G.ToString('X2') + $c.B.ToString('X2') }

function Get-TilesSvg($xs, $ys, $w, $h, $r) {
    $rects = foreach ($y in $ys) { foreach ($x in $xs) { "    <rect x=`"$x`" y=`"$y`" width=`"$w`" height=`"$h`" rx=`"$r`" />" } }
    $rects -join "`n"
}

function Get-AppSvg {
    $m = 256 * $appDesign.Margin
    $k = (256 - 2 * $m) / 256
    $tiles = (Get-TilesSvg $appDesign.Xs $appDesign.Ys $appDesign.W $appDesign.H $appDesign.R) -replace '(?m)^', '  '
    @"
<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
  <!-- PageCompose app icon, 256 px design. Written by scripts/make-icons.ps1: change the design there. -->
  <defs>
    <linearGradient id="gradient" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="$(Get-Hex $gradientStart)" />
      <stop offset="1" stop-color="$(Get-Hex $gradientEnd)" />
    </linearGradient>
  </defs>
  <g transform="translate($m $m) scale($k)">
    <rect width="256" height="256" rx="$(256 * $appDesign.Bg)" fill="url(#gradient)" />
    <g fill="#FFFFFF">
$tiles
    </g>
  </g>
</svg>
"@
}

function Get-FileSvg {
    $spec = $fileSpecs[256]
    $x, $y, $w, $h = $spec.Page
    $f = $spec.Fold; $r = $spec.R
    $x1 = $x + $w; $y1 = $y + $h
    $fr = [Math]::Min($r * 0.75, $f / 4)
    # GDI+ spreads a 60 degree gradient from the top left corner of the page to the line through the opposite one.
    $a = 60 * [Math]::PI / 180
    $length = $w * [Math]::Cos($a) + $h * [Math]::Sin($a)
    $gx = [Math]::Round($x + $length * [Math]::Cos($a), 2)
    $gy = [Math]::Round($y + $length * [Math]::Sin($a), 2)
    $tiles = Get-TilesSvg $spec.Xs $spec.Ys $spec.W $spec.H $spec.TR
    @"
<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
  <!-- Icon of the PageCompose preset files (.PageComposePreset), 256 px design. Written by scripts/make-icons.ps1: change the design there. -->
  <defs>
    <linearGradient id="gradient" gradientUnits="userSpaceOnUse" x1="$x" y1="$y" x2="$gx" y2="$gy">
      <stop offset="0" stop-color="$(Get-Hex $gradientStart)" />
      <stop offset="1" stop-color="$(Get-Hex $gradientEnd)" />
    </linearGradient>
  </defs>
  <path d="M$x,$($y + $r) A$r,$r 0 0 1 $($x + $r),$y L$($x1 - $f),$y L$x1,$($y + $f) L$x1,$($y1 - $r) A$r,$r 0 0 1 $($x1 - $r),$y1 L$($x + $r),$y1 A$r,$r 0 0 1 $x,$($y1 - $r) Z" fill="url(#gradient)" />
  <path d="M$($x1 - $f),$y L$($x1 - $f),$($y + $f - $fr) A$fr,$fr 0 0 0 $($x1 - $f + $fr),$($y + $f) L$x1,$($y + $f) Z" fill="#FFFFFF" fill-opacity="0.43" />
  <g fill="#FFFFFF">
$tiles
  </g>
</svg>
"@
}

# The path of a page with its folded corner, and its fold, for the SVG copies.
function Get-PageSvgPaths($rect, $f, $r) {
    $x, $y, $w, $h = $rect
    $x1 = $x + $w; $y1 = $y + $h
    $fr = [Math]::Min($r * 0.75, $f / 4)
    @{
        Page = "M$x,$($y + $r) A$r,$r 0 0 1 $($x + $r),$y L$($x1 - $f),$y L$x1,$($y + $f) L$x1,$($y1 - $r) A$r,$r 0 0 1 $($x1 - $r),$y1 L$($x + $r),$y1 A$r,$r 0 0 1 $x,$($y1 - $r) Z"
        Fold = "M$($x1 - $f),$y L$($x1 - $f),$($y + $f - $fr) A$fr,$fr 0 0 0 $($x1 - $f + $fr),$($y + $f) L$x1,$($y + $f) Z"
    }
}

function Get-GradientSvg([string]$id, $rect) {
    $x, $y, $w, $h = $rect
    $a = 60 * [Math]::PI / 180
    $length = $w * [Math]::Cos($a) + $h * [Math]::Sin($a)
    $gx = [Math]::Round($x + $length * [Math]::Cos($a), 2)
    $gy = [Math]::Round($y + $length * [Math]::Sin($a), 2)
    @"
    <linearGradient id="$id" gradientUnits="userSpaceOnUse" x1="$x" y1="$y" x2="$gx" y2="$gy">
      <stop offset="0" stop-color="$(Get-Hex $gradientStart)" />
      <stop offset="1" stop-color="$(Get-Hex $gradientEnd)" />
    </linearGradient>
"@
}

function Get-ProjectSvg {
    $spec = $projectSpecs[256]
    $back = Get-PageSvgPaths $spec.Back $spec.Fold $spec.R
    $front = Get-PageSvgPaths $spec.Front $spec.Fold $spec.R
    $tiles = Get-TilesSvg $spec.Xs $spec.Ys $spec.W $spec.H $spec.TR
    $opacity = [Math]::Round($projectBackAlpha / 255, 2)
    @"
<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
  <!-- Icon of the PageCompose project files (.PageComposeProject), 256 px design. Written by scripts/make-icons.ps1: change the design there. -->
  <defs>
$(Get-GradientSvg 'back' $spec.Back)
$(Get-GradientSvg 'front' $spec.Front)
    <mask id="gap">
      <rect width="256" height="256" fill="#FFFFFF" />
      <path d="$($front.Page)" fill="#000000" stroke="#000000" stroke-width="$(2 * $spec.Gap)" />
    </mask>
  </defs>
  <g mask="url(#gap)" opacity="$opacity">
    <path d="$($back.Page)" fill="url(#back)" />
    <path d="$($back.Fold)" fill="#FFFFFF" fill-opacity="0.43" />
  </g>
  <path d="$($front.Page)" fill="url(#front)" />
  <path d="$($front.Fold)" fill="#FFFFFF" fill-opacity="0.43" />
  <g fill="#FFFFFF">
$tiles
  </g>
</svg>
"@
}

function Save-Svg([string]$path, [string]$svg) {
    New-Item -ItemType Directory -Force (Split-Path -Parent $path) | Out-Null
    [System.IO.File]::WriteAllText($path, $svg.Replace("`r`n", "`n") + "`n", (New-Object System.Text.UTF8Encoding $false))
    Write-Host "  $path"
}

Write-Host 'Generated icons:'
Save-Ico @(16, 20, 24, 32, 40, 48, 64, 256) (Join-Path $root 'src\PageCompose.App\Assets\PageCompose.ico')
Save-Png 256 (Join-Path $root 'docs\icon-256.png')
# Large image shown inside the app (About card): an .ico would display its first, 16 px, frame.
Save-Png 256 (Join-Path $root 'src\PageCompose.App\Assets\PageCompose.png')
# Icon of the preset files, installed next to the app and registered by the installer.
Save-Ico @(16, 20, 24, 32, 40, 48, 64, 256) (Join-Path $root 'src\PageCompose.App\Assets\PresetFile.ico') { param($size) New-FileIconBitmap $size }
Save-Svg (Join-Path $root 'docs\icons\PageCompose.svg') (Get-AppSvg)
Save-Svg (Join-Path $root 'docs\icons\PresetFile.svg') (Get-FileSvg)
# Icon of the project files (.PageComposeProject), installed and registered in the same way.
Save-Ico @(16, 20, 24, 32, 40, 48, 64, 256) (Join-Path $root 'src\PageCompose.App\Assets\ProjectFile.ico') { param($size) New-ProjectIconBitmap $size }
Save-Svg (Join-Path $root 'docs\icons\ProjectFile.svg') (Get-ProjectSvg)
