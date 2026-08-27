<#
.SYNOPSIS
    Builds the Thunderstore package zip.

.DESCRIPTION
    Produces dist/PlanetaryAnomalies-<version>.zip laid out the way the Dyson Sphere Program
    community expects: everything at the root of the zip, no folders. That layout was taken from
    the mods already installed on this machine (PlanetFinder, UXAssist, DeliverySlotsTweaks),
    not from memory.

        manifest.json
        icon.png
        README.md
        CHANGELOG.md
        PlanetaryAnomalies.dll

    This does NOT upload anything. Publishing is a deliberate, manual step and AGENTS.md requires
    explicit approval for it.

.EXAMPLE
    .\scripts\package.ps1
#>
[CmdletBinding()]
param(
    [string]$GameDir,
    [string]$BepInExDir,
    [string]$ProfileName
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$repoRoot = Split-Path -Parent $PSScriptRoot
$packagingDir = Join-Path $repoRoot 'packaging'
$distDir = Join-Path $repoRoot 'dist'

# --- the version must agree in two places, and Thunderstore will not let you re-upload one ---

$pluginSource = Join-Path $repoRoot 'src\Plugin.cs'
$pluginText = Get-Content $pluginSource -Raw
if ($pluginText -notmatch 'PluginVersion\s*=\s*"([^"]+)"') {
    throw "Could not read PluginVersion from '$pluginSource'."
}
$codeVersion = $Matches[1]

$manifestPath = Join-Path $packagingDir 'manifest.json'
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$manifestVersion = $manifest.version_number

if ($codeVersion -ne $manifestVersion) {
    throw "Version mismatch: Plugin.cs says '$codeVersion', manifest.json says '$manifestVersion'. " +
          "These must agree -- the manifest version is what Thunderstore publishes, and it can never be reused."
}

Write-Host "Version: $codeVersion (Plugin.cs and manifest.json agree)"

# --- Thunderstore's own rules, checked here so a rejected upload is not the first you hear of it ---

$problems = New-Object System.Collections.Generic.List[string]

if ($manifest.name -notmatch '^[A-Za-z0-9_]+$') {
    $problems.Add("manifest name '$($manifest.name)' must be letters, digits and underscores only -- no spaces or hyphens.")
}
if ($manifest.description.Length -gt 250) {
    $problems.Add("manifest description is $($manifest.description.Length) characters; the limit is 250.")
}
if (-not $manifest.website_url) {
    $problems.Add("manifest has no website_url.")
}

$iconPath = Join-Path $packagingDir 'icon.png'
if (-not (Test-Path $iconPath)) {
    $problems.Add("packaging/icon.png is missing; Thunderstore requires one.")
} else {
    Add-Type -AssemblyName System.Drawing
    $icon = [System.Drawing.Image]::FromFile((Resolve-Path $iconPath))
    $w = $icon.Width; $h = $icon.Height
    $icon.Dispose()
    if ($w -ne 256 -or $h -ne 256) {
        $problems.Add("icon.png is ${w}x${h}; Thunderstore requires exactly 256x256.")
    }
}

foreach ($required in @('README.md', 'manifest.json')) {
    if (-not (Test-Path (Join-Path $packagingDir $required))) {
        $problems.Add("packaging/$required is missing.")
    }
}

if ($problems.Count -gt 0) {
    Write-Host ""
    Write-Host "Package would be rejected:" -ForegroundColor Red
    foreach ($p in $problems) { Write-Host "  - $p" -ForegroundColor Red }
    throw "$($problems.Count) problem(s) found."
}

# --- build fresh, so the packaged DLL is never a stale one ---

Write-Host ""
Write-Host "Building..."
& (Join-Path $PSScriptRoot 'build.ps1') -GameDir $GameDir -BepInExDir $BepInExDir -ProfileName $ProfileName | Out-Host

Write-Host "Verifying against the installed game..."
& (Join-Path $PSScriptRoot 'verify.ps1') -GameDir $GameDir -BepInExDir $BepInExDir -ProfileName $ProfileName | Out-Host

$dll = Join-Path $repoRoot 'build\PlanetaryAnomalies.dll'
if (-not (Test-Path $dll)) { throw "No build output at '$dll'." }

# --- assemble ---

$staging = Join-Path ([System.IO.Path]::GetTempPath()) ("pa-package-" + [System.Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $staging -Force | Out-Null

try {
    Copy-Item $dll (Join-Path $staging 'PlanetaryAnomalies.dll')
    foreach ($f in @('manifest.json', 'icon.png', 'README.md', 'CHANGELOG.md')) {
        $src = Join-Path $packagingDir $f
        if (Test-Path $src) { Copy-Item $src (Join-Path $staging $f) }
    }

    if (-not (Test-Path $distDir)) { New-Item -ItemType Directory -Path $distDir -Force | Out-Null }
    $zip = Join-Path $distDir "PlanetaryAnomalies-$codeVersion.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($staging, $zip)

    Write-Host ""
    Write-Host "Package: $zip" -ForegroundColor Green
    [System.IO.Compression.ZipFile]::OpenRead($zip).Entries | ForEach-Object {
        "  {0,-28} {1,8} bytes" -f $_.FullName, $_.Length
    }
    Write-Host ""
    Write-Host "Nothing has been uploaded. To publish, go to https://thunderstore.io/c/dyson-sphere-program/create/"
    Write-Host "and upload this zip. The version number can never be reused, so check it first."
}
finally {
    Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
}
