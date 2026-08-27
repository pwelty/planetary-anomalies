# Shared path discovery for build.ps1 and install.ps1.
#
# Nothing here is hard-coded to one machine if it can be found instead. If discovery fails,
# every function says exactly what to pass or set.
#
# Overrides, in order of precedence:
#   1. the -GameDir / -BepInExDir parameters on build.ps1 and install.ps1
#   2. the DSP_DIR and DSP_BEPINEX_DIR environment variables
#   3. scripts\local.paths.ps1  (gitignored; see local.paths.example.ps1)
#   4. automatic discovery

$ErrorActionPreference = 'Stop'

$script:DspAppId = '1366540'

$LocalPaths = Join-Path $PSScriptRoot 'local.paths.ps1'
if (Test-Path $LocalPaths) { . $LocalPaths }

function Get-DspDir {
    param([string]$Override)

    foreach ($candidate in @($Override, $env:DSP_DIR, $script:LocalDspDir)) {
        if ($candidate -and (Test-Path $candidate)) { return (Resolve-Path $candidate).Path }
    }

    # Walk every Steam library recorded in libraryfolders.vdf, not just the default one.
    $roots = @()
    foreach ($steam in @("${env:ProgramFiles(x86)}\Steam", "$env:ProgramFiles\Steam")) {
        $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
        if (-not (Test-Path $vdf)) { continue }
        $content = Get-Content $vdf -Raw
        foreach ($m in [regex]::Matches($content, '"path"\s+"([^"]+)"')) {
            $roots += $m.Groups[1].Value -replace '\\\\', '\'
        }
    }
    $roots += "${env:ProgramFiles(x86)}\Steam"

    foreach ($root in ($roots | Select-Object -Unique)) {
        $dir = Join-Path $root 'steamapps\common\Dyson Sphere Program'
        if (Test-Path (Join-Path $dir 'DSPGAME.exe')) { return (Resolve-Path $dir).Path }
    }

    throw "Could not find the Dyson Sphere Program install. Pass -GameDir '<path>', set DSP_DIR, or create scripts\local.paths.ps1 (copy local.paths.example.ps1)."
}

function Get-ManagedDir {
    param([Parameter(Mandatory)][string]$GameDir)

    $managed = Join-Path $GameDir 'DSPGAME_Data\Managed'
    if (-not (Test-Path (Join-Path $managed 'Assembly-CSharp.dll'))) {
        throw "No Assembly-CSharp.dll under '$managed'. Is '$GameDir' really the DSP install?"
    }
    return $managed
}

# The BepInEx tree to build against and install into. Mods here are managed by Gale, which
# keeps a separate BepInEx per profile, so this prefers a Gale profile and falls back to a
# plain in-game-directory BepInEx install.
function Get-BepInExDir {
    param([string]$Override, [string]$ProfileName, [string]$GameDir)

    foreach ($candidate in @($Override, $env:DSP_BEPINEX_DIR, $script:LocalBepInExDir)) {
        if ($candidate -and (Test-Path (Join-Path $candidate 'core\BepInEx.dll'))) {
            return (Resolve-Path $candidate).Path
        }
        if ($candidate) { throw "'$candidate' does not look like a BepInEx folder (no core\BepInEx.dll)." }
    }

    $profileRoot = Join-Path $env:APPDATA 'com.kesomannen.gale\dyson-sphere-program\profiles'
    if (Test-Path $profileRoot) {
        if ($ProfileName) {
            $dir = Join-Path $profileRoot "$ProfileName\BepInEx"
            if (-not (Test-Path (Join-Path $dir 'core\BepInEx.dll'))) {
                throw "Gale profile '$ProfileName' has no BepInEx installed. Install a mod into it in Gale first."
            }
            return (Resolve-Path $dir).Path
        }

        $found = @(Get-ChildItem $profileRoot -Directory | ForEach-Object {
            $dir = Join-Path $_.FullName 'BepInEx'
            if (Test-Path (Join-Path $dir 'core\BepInEx.dll')) {
                [pscustomobject]@{ Name = $_.Name; Path = $dir }
            }
        })

        if ($found.Count -eq 1) { return $found[0].Path }
        if ($found.Count -gt 1) {
            $names = ($found | ForEach-Object { "'$($_.Name)'" }) -join ', '
            throw "Several Gale profiles have BepInEx ($names). Pass -ProfileName <name> to pick one."
        }
    }

    if ($GameDir) {
        $dir = Join-Path $GameDir 'BepInEx'
        if (Test-Path (Join-Path $dir 'core\BepInEx.dll')) { return (Resolve-Path $dir).Path }
    }

    throw "Could not find an installed BepInEx. Install one via Gale (or into the game folder), then re-run. You can also pass -BepInExDir '<path>'."
}

function Get-CscPath {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (-not (Test-Path $csc)) {
        throw "The in-box C# compiler was not found at '$csc'. It ships with the .NET Framework and is normally present on Windows."
    }
    return $csc
}
