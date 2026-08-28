<#
.SYNOPSIS
    Copies the built PlanetaryAnomalies.dll into the BepInEx plugins folder.

.DESCRIPTION
    Mods on this machine are managed by Gale, which keeps a separate BepInEx tree per profile.
    The plugin is installed into its own folder inside that profile's plugins directory, which
    is the layout Gale and Thunderstore mods already use.

.EXAMPLE
    .\scripts\install.ps1
.EXAMPLE
    .\scripts\install.ps1 -ProfileName 'anomalies test'
#>
[CmdletBinding()]
param(
    [string]$GameDir,
    [string]$BepInExDir,

    # Gale profile name, when more than one profile has BepInEx installed.
    [string]$ProfileName
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$repoRoot = Split-Path -Parent $PSScriptRoot
$outDll = Join-Path $repoRoot 'build\PlanetaryAnomalies.dll'

if (-not (Test-Path $outDll)) {
    throw "No build output at '$outDll'. Run .\scripts\build.ps1 first."
}

if (-not $GameDir) { $GameDir = Get-DspDir }
$BepInExDir = Get-BepInExDir -Override $BepInExDir -ProfileName $ProfileName -GameDir $GameDir

$pluginsDir = Join-Path $BepInExDir 'plugins'
$target = Join-Path $pluginsDir 'PlanetaryAnomalies'

# Refuse to install beside a copy of this same plugin -- most likely the Thunderstore release
# installed through Gale. Two folders declaring the same BepInPlugin GUID means BepInEx loads one
# and refuses the other, and which one it refuses is not worth guessing while testing. Better to
# stop here than to produce a profile whose behaviour depends on load order.
if (Test-Path $pluginsDir) {
    $conflicts = @()
    foreach ($dir in (Get-ChildItem $pluginsDir -Directory -ErrorAction SilentlyContinue)) {
        if ($dir.FullName -eq $target) { continue }

        $hasOurDll = Test-Path (Join-Path $dir.FullName 'PlanetaryAnomalies.dll')

        $manifestSaysUs = $false
        $manifest = Join-Path $dir.FullName 'manifest.json'
        if (Test-Path $manifest) {
            try {
                $manifestSaysUs = ((Get-Content $manifest -Raw | ConvertFrom-Json).name -eq 'PlanetaryAnomalies')
            } catch { }
        }

        if ($hasOurDll -or $manifestSaysUs) { $conflicts += $dir.FullName }
    }

    if ($conflicts.Count -gt 0) {
        $list = ($conflicts | ForEach-Object { "  $_" }) -join "`n"
        throw @"
Another copy of Planetary Anomalies is already in this profile:

$list

Both declare the same BepInPlugin GUID, so BepInEx would load one and refuse the other.

Either install into a different profile:

    .\scripts\install.ps1 -ProfileName 'dev'

or remove the other copy from this profile first. If it was installed through Gale, remove it
in Gale rather than by deleting the folder, so Gale's own record stays consistent.
"@
    }
}

if (-not (Test-Path $target)) { New-Item -ItemType Directory -Path $target -Force | Out-Null }

Copy-Item $outDll $target -Force

$pdb = [IO.Path]::ChangeExtension($outDll, '.pdb')
if (Test-Path $pdb) { Copy-Item $pdb $target -Force }

Write-Host "Installed to $target" -ForegroundColor Green
Write-Host "Log after launching: $BepInExDir\LogOutput.log"
