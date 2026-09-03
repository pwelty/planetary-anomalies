<#
.SYNOPSIS
    Launches DSP with a Gale profile's BepInEx, without going through Gale.

.DESCRIPTION
    The game folder holds Unity Doorstop (winhttp.dll) but no BepInEx, so launching DSP from
    Steam loads no mods. Gale works around that by pointing Doorstop at the profile's own
    BepInEx through an environment variable; this does the same thing, so the test loop is
    repeatable from the terminal.

    Doorstop 3 is what is installed here (confirmed from the strings in winhttp.dll), so the
    variable is DOORSTOP_INVOKE_DLL_PATH. Doorstop 4 renamed it to DOORSTOP_TARGET_ASSEMBLY --
    if a future BepInEx update changes this, that is the thing to re-check.

    Steam should already be running; DSP uses Steamworks and will complain otherwise.

.EXAMPLE
    .\scripts\launch.ps1
.EXAMPLE
    .\scripts\launch.ps1 -ProfileName 'gs run' -Tail
#>
[CmdletBinding()]
param(
    [string]$GameDir,
    [string]$BepInExDir,
    [string]$ProfileName,

    # Follow the BepInEx log after launching instead of returning immediately.
    [switch]$Tail
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$GameDir = Get-DspDir -Override $GameDir
$BepInExDir = Get-BepInExDir -Override $BepInExDir -ProfileName $ProfileName -GameDir $GameDir

$exe = Join-Path $GameDir 'DSPGAME.exe'
if (-not (Test-Path $exe)) { throw "DSPGAME.exe not found in '$GameDir'." }

$preloader = Join-Path $BepInExDir 'core\BepInEx.Preloader.dll'
if (-not (Test-Path $preloader)) { throw "No BepInEx preloader at '$preloader'." }

$plugin = Join-Path $BepInExDir 'plugins\PlanetaryAnomalies\PlanetaryAnomalies.dll'
if (-not (Test-Path $plugin)) {
    Write-Warning "PlanetaryAnomalies.dll is not installed in this profile. Run .\scripts\install.ps1 first."
}

if (-not (Get-Process -Name steam -ErrorAction SilentlyContinue)) {
    Write-Warning "Steam does not appear to be running. DSP uses Steamworks and may refuse to start."
}

# Start fresh so a previous run's lines are not mistaken for this one's.
$log = Join-Path $BepInExDir 'LogOutput.log'
if (Test-Path $log) {
    $archived = Join-Path $BepInExDir ('LogOutput.previous.log')
    Move-Item $log $archived -Force
}

Write-Host "Game:    $exe"
Write-Host "BepInEx: $BepInExDir"
Write-Host "Log:     $log"
Write-Host ""

$env:DOORSTOP_INVOKE_DLL_PATH = $preloader
# Remove rather than blank it: Doorstop treats the variable being present as meaningful, so an
# empty string could silently disable modding.
Remove-Item Env:\DOORSTOP_DISABLE -ErrorAction SilentlyContinue

Start-Process -FilePath $exe -WorkingDirectory $GameDir

Write-Host "Launched. Watch for these lines in the log:" -ForegroundColor Green
Write-Host "  Planetary Anomalies v0.4.0 loaded"
Write-Host "  ANOMALY   (once a save is loaded)"
Write-Host "  Anomaly attached to assembler #N   (once a matching smelter exists)"

if ($Tail) {
    Write-Host ""
    Write-Host "Tailing the log; Ctrl+C to stop." -ForegroundColor Cyan
    while (-not (Test-Path $log)) { Start-Sleep -Milliseconds 500 }
    Get-Content $log -Wait -Tail 0 | Where-Object { $_ -match 'Planetary Anomalies|ANOMALY|Anomaly attached|Error' }
}
