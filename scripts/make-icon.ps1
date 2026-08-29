<#
.SYNOPSIS
    Draws packaging/icon.png, the 256x256 Thunderstore icon.

.DESCRIPTION
    Kept as a script rather than a checked-in binary alone, so the icon can be adjusted and
    regenerated rather than being an opaque file nobody dares touch.

    Design intent: legible at the ~64px the package list actually renders. That rules out detail.
    One planet, one anomalous ring, high contrast against a dark field. The ring is gold to match
    the in-game markers, so the listing and the game agree visually.

.EXAMPLE
    .\scripts\make-icon.ps1
#>
[CmdletBinding()]
param(
    [string]$OutPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutPath) { $OutPath = Join-Path $repoRoot 'packaging\icon.png' }

$size = 256
$bmp = New-Object System.Drawing.Bitmap $size, $size
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

function RGB([int]$r, [int]$gr, [int]$b, [int]$a = 255) {
    return [System.Drawing.Color]::FromArgb($a, $r, $gr, $b)
}

# --- deep space, slightly lighter at centre so the planet does not sit on flat black ---
$bg = New-Object System.Drawing.Drawing2D.GraphicsPath
$bg.AddEllipse(-60, -60, $size + 120, $size + 120)
$vignette = New-Object System.Drawing.Drawing2D.PathGradientBrush $bg
$vignette.CenterColor = (RGB 26 34 52)
$vignette.SurroundColors = @((RGB 8 10 18))
$g.FillRectangle($vignette, 0, 0, $size, $size)

# --- a few stars, deliberately sparse ---
$starBrush = New-Object System.Drawing.SolidBrush (RGB 150 170 200 170)
$stars = @(@(28,40,2), @(210,34,2), @(60,206,2), @(228,180,3), @(180,64,2), @(36,140,2), @(240,110,2))
foreach ($s in $stars) { $g.FillEllipse($starBrush, $s[0], $s[1], $s[2], $s[2]) }

# --- ring geometry, shared by the back and front halves ---
# The ring is drawn in two passes with the planet between them, which is what makes it read as
# something orbiting rather than a hook laid over the top.
$rx = 22; $ry = 96; $rw = 212; $rh = 76
$gold = RGB 255 196 84

$ringPen = New-Object System.Drawing.Pen $gold, 7
$ringPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$ringPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

$backPen = New-Object System.Drawing.Pen (RGB 168 128 56), 6
$backPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$backPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

$glowPen = New-Object System.Drawing.Pen (RGB 255 196 84 55), 16
$glowPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$glowPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

# Back half: dimmer, as if passing behind.
$g.DrawArc($backPen, $rx, $ry, $rw, $rh, 180, 180)

# --- the planet, drawn over the back half of the ring ---
$px = 62; $py = 58; $pd = 132
$planetPath = New-Object System.Drawing.Drawing2D.GraphicsPath
$planetPath.AddEllipse($px, $py, $pd, $pd)
$planet = New-Object System.Drawing.Drawing2D.PathGradientBrush $planetPath
$planet.CenterPoint = New-Object System.Drawing.PointF (($px + $pd * 0.34), ($py + $pd * 0.30))
$planet.CenterColor = (RGB 104 148 198)
$planet.SurroundColors = @((RGB 16 30 56))
$g.FillEllipse($planet, $px, $py, $pd, $pd)

# Terminator: a soft dark crescent lower-right, so it reads as a sphere rather than a disc.
$shadow = New-Object System.Drawing.Drawing2D.GraphicsPath
$shadow.AddEllipse($px + 28, $py + 24, $pd, $pd)
$shadowBrush = New-Object System.Drawing.Drawing2D.PathGradientBrush $shadow
$shadowBrush.CenterColor = (RGB 0 0 0 170)
$shadowBrush.SurroundColors = @((RGB 0 0 0 0))
$old = $g.Clip
$g.SetClip($planetPath)
$g.FillEllipse($shadowBrush, $px + 28, $py + 24, $pd, $pd)
$g.Clip = $old

# --- front half of the ring, over the planet, with a gap ---
# The gap is the anomaly: an unbroken ring reads as ordinary orbital mechanics. The bright spark
# sits at the break.
$g.DrawArc($glowPen, $rx, $ry, $rw, $rh, 8, 150)
$g.DrawArc($ringPen, $rx, $ry, $rw, $rh, 8, 150)

$sparkGlow = New-Object System.Drawing.SolidBrush (RGB 255 214 130 80)
$spark = New-Object System.Drawing.SolidBrush (RGB 255 236 190)
$g.FillEllipse($sparkGlow, 196, 108, 36, 36)
$g.FillEllipse($spark, 207, 119, 14, 14)

$g.Dispose()
$bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

$check = [System.Drawing.Image]::FromFile((Resolve-Path $OutPath))
$w = $check.Width; $h = $check.Height
$check.Dispose()

Write-Host ("Wrote {0} ({1}x{2}, {3:N0} bytes)" -f $OutPath, $w, $h, (Get-Item $OutPath).Length)
if ($w -ne 256 -or $h -ne 256) { throw "Icon must be exactly 256x256; got ${w}x${h}." }
