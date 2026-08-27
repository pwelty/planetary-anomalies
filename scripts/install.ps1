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

$target = Join-Path $BepInExDir 'plugins\PlanetaryAnomalies'
if (-not (Test-Path $target)) { New-Item -ItemType Directory -Path $target | Out-Null }

Copy-Item $outDll $target -Force

$pdb = [IO.Path]::ChangeExtension($outDll, '.pdb')
if (Test-Path $pdb) { Copy-Item $pdb $target -Force }

Write-Host "Installed to $target" -ForegroundColor Green
Write-Host "Log after launching: $BepInExDir\LogOutput.log"
