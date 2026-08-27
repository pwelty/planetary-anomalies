<#
.SYNOPSIS
    Builds PlanetaryAnomalies.dll.

.DESCRIPTION
    Compiles with the C# compiler that ships with the .NET Framework -- already on every
    Windows machine -- so there is no SDK to install and no NuGet restore.

    It compiles against the game's own assemblies (/nostdlib+ plus an explicit reference to
    the game's mscorlib), which is what a Unity mod should bind to anyway. That does mean
    C# 5 language features only: no string interpolation, no ?., no expression-bodied members.

.EXAMPLE
    .\scripts\build.ps1
.EXAMPLE
    .\scripts\build.ps1 -Install
#>
[CmdletBinding()]
param(
    # DSP install folder. Discovered from Steam if omitted.
    [string]$GameDir,

    # BepInEx folder to reference. Discovered from the Gale profiles if omitted.
    [string]$BepInExDir,

    # Gale profile name, when more than one profile has BepInEx installed.
    [string]$ProfileName,

    # Copy the result into the BepInEx plugins folder afterwards.
    [switch]$Install
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$repoRoot = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $repoRoot 'build'
$outDll = Join-Path $outDir 'PlanetaryAnomalies.dll'

$GameDir = Get-DspDir -Override $GameDir
$managed = Get-ManagedDir -GameDir $GameDir
$BepInExDir = Get-BepInExDir -Override $BepInExDir -ProfileName $ProfileName -GameDir $GameDir
$csc = Get-CscPath

Write-Host "Game:    $GameDir"
Write-Host "BepInEx: $BepInExDir"
Write-Host "Compiler: $csc"

$references = @(
    (Join-Path $managed 'mscorlib.dll')
    (Join-Path $managed 'System.dll')
    (Join-Path $managed 'System.Core.dll')
    # BepInEx and UnityEngine.CoreModule both surface netstandard-typed members.
    (Join-Path $managed 'netstandard.dll')
    (Join-Path $managed 'Assembly-CSharp.dll')
    (Join-Path $managed 'UnityEngine.dll')
    (Join-Path $managed 'UnityEngine.CoreModule.dll')
    # UnityEngine.UI: the planet detail panel's brief is a UnityEngine.UI.Text.
    (Join-Path $managed 'UnityEngine.UI.dll')
    (Join-Path $BepInExDir 'core\BepInEx.dll')
    (Join-Path $BepInExDir 'core\0Harmony.dll')
)

foreach ($reference in $references) {
    if (-not (Test-Path $reference)) { throw "Missing reference assembly: $reference" }
}

$sources = @(Get-ChildItem (Join-Path $repoRoot 'src') -Filter *.cs -Recurse | ForEach-Object { $_.FullName })
if ($sources.Count -eq 0) { throw "No .cs files found under $repoRoot\src." }

if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

Write-Host "Compiling $($sources.Count) source file(s)..."

$cscArgs = @(
    '/nologo'
    # /noconfig: without it csc.rsp adds the machine's own System.dll and System.Core.dll,
    # which collide with the game's copies of the same assembly identities.
    '/noconfig'
    '/target:library'
    '/platform:x64'
    '/optimize+'
    '/debug:pdbonly'
    '/warnaserror-'
    '/nostdlib+'
    "/out:$outDll"
)
$cscArgs += ($references | ForEach-Object { "/reference:$_" })
$cscArgs += $sources

& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { throw "Compilation failed (csc exit code $LASTEXITCODE)." }

$built = Get-Item $outDll
Write-Host "Built $($built.FullName) ($($built.Length) bytes)" -ForegroundColor Green

if ($Install) {
    & (Join-Path $PSScriptRoot 'install.ps1') -GameDir $GameDir -BepInExDir $BepInExDir
}
