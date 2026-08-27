<#
.SYNOPSIS
    Checks the built plugin against the installed game assemblies.

.DESCRIPTION
    Compiling proves the plugin agrees with the assemblies it was compiled against. This goes
    one step further and re-resolves every type, field and method the built DLL references
    against the assemblies that are actually installed right now, so a game update that moves
    or renames something is caught here instead of as a silent no-op in game.

    It also confirms the Harmony target method exists with the exact signature the patch
    attribute names.

    This is a static check. It is not evidence that the anomaly works in game -- only a manual
    in-game test is that.

.EXAMPLE
    .\scripts\verify.ps1
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
$outDll = Join-Path $repoRoot 'build\PlanetaryAnomalies.dll'
if (-not (Test-Path $outDll)) { throw "No build output at '$outDll'. Run .\scripts\build.ps1 first." }

$GameDir = Get-DspDir -Override $GameDir
$managed = Get-ManagedDir -GameDir $GameDir
$BepInExDir = Get-BepInExDir -Override $BepInExDir -ProfileName $ProfileName -GameDir $GameDir

Add-Type -Path (Join-Path $BepInExDir 'core\Mono.Cecil.dll')

$resolver = New-Object Mono.Cecil.DefaultAssemblyResolver
$resolver.AddSearchDirectory($managed)
$resolver.AddSearchDirectory((Join-Path $BepInExDir 'core'))

$readerParams = New-Object Mono.Cecil.ReaderParameters
$readerParams.AssemblyResolver = $resolver

$plugin = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($outDll, $readerParams)
$module = $plugin.MainModule

$failures = New-Object System.Collections.Generic.List[string]
$checked = 0

# Only check references into assemblies we control the version of. mscorlib/System/Unity are
# stable enough that noise there would drown the signal.
$interesting = @('Assembly-CSharp', 'BepInEx', '0Harmony')

function Test-Interesting($scopeName) { return $interesting -contains $scopeName }

foreach ($typeRef in $module.GetTypeReferences()) {
    if (-not (Test-Interesting $typeRef.Scope.Name)) { continue }
    $checked++
    if ($null -eq $typeRef.Resolve()) {
        $failures.Add("type not found: $($typeRef.FullName)  (expected in $($typeRef.Scope.Name))")
    }
}

foreach ($memberRef in $module.GetMemberReferences()) {
    $scope = $memberRef.DeclaringType.Scope.Name
    if (-not (Test-Interesting $scope)) { continue }
    $checked++
    $resolved = $null
    try { $resolved = $memberRef.Resolve() } catch { $resolved = $null }
    if ($null -eq $resolved) {
        $failures.Add("member not found: $($memberRef.DeclaringType.FullName)::$($memberRef.Name)  (expected in $scope)")
    }
}

# The Harmony target is named in an attribute, so the compiler cannot check it. Do it here.
$gameAsm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $managed 'Assembly-CSharp.dll'), $readerParams)
$planetFactory = $gameAsm.MainModule.GetType('PlanetFactory')
if ($null -eq $planetFactory) {
    $failures.Add("Harmony target type 'PlanetFactory' does not exist in the installed game.")
} else {
    $beforeTick = $planetFactory.Methods | Where-Object { $_.Name -eq 'BeforeGameTick' -and $_.Parameters.Count -eq 0 }
    if (-not $beforeTick) {
        $failures.Add("Harmony target 'PlanetFactory.BeforeGameTick()' does not exist in the installed game.")
    } else {
        Write-Host "OK  Harmony target: PlanetFactory.BeforeGameTick()"
    }

    $fsField = $planetFactory.Fields | Where-Object { $_.Name -eq 'factorySystem' }
    if (-not $fsField -or -not $fsField.IsPublic) {
        $failures.Add("PlanetFactory.factorySystem is missing or no longer public.")
    } else {
        Write-Host "OK  PlanetFactory.factorySystem is public"
    }
}

# The hook must run in BOTH the sequential and multithreaded dispatch paths. GameLogic pairs most
# factory phases with a _Parallel twin and picks between them on thread count, so a phase that has
# a twin is only half the story. FactoryBeforeGameTick having no twin is what makes it safe --
# assert that, because a future update adding one would silently break multithreaded games.
$gameLogic = $gameAsm.MainModule.GetType('GameLogic')
if ($null -eq $gameLogic) {
    $failures.Add("GameLogic does not exist in the installed game.")
} else {
    $twin = $gameLogic.Methods | Where-Object { $_.Name -eq 'FactoryBeforeGameTick_Parallel' }
    if ($twin) {
        $failures.Add("GameLogic.FactoryBeforeGameTick_Parallel now exists, so FactoryBeforeGameTick is probably no longer run in multithreaded games. The hook needs re-checking against OnGameLogicFrame.")
    } else {
        Write-Host "OK  GameLogic.FactoryBeforeGameTick still has no _Parallel twin"
    }

    $callsBefore = $false
    $fbgt = $gameLogic.Methods | Where-Object { $_.Name -eq 'FactoryBeforeGameTick' }
    if ($fbgt -and $fbgt.HasBody) {
        foreach ($ins in $fbgt.Body.Instructions) {
            $op = $ins.Operand
            if ($op -is [Mono.Cecil.MethodReference] -and $op.Name -eq 'BeforeGameTick' -and $op.DeclaringType.Name -eq 'PlanetFactory') { $callsBefore = $true }
        }
    }
    if (-not $callsBefore) {
        $failures.Add("GameLogic.FactoryBeforeGameTick no longer calls PlanetFactory.BeforeGameTick; the hook would never fire.")
    } else {
        Write-Host "OK  GameLogic.FactoryBeforeGameTick still calls PlanetFactory.BeforeGameTick"
    }
}

# The whole design rests on this field being a per-component reference into shared data. If a
# game update ever turns it into a value type or removes it, the approach is invalid.
$assembler = $gameAsm.MainModule.GetType('AssemblerComponent')
if ($null -eq $assembler -or -not $assembler.IsValueType) {
    $failures.Add("AssemblerComponent is missing or is no longer a struct; the in-place pool write in the patch would silently do nothing.")
} else {
    Write-Host "OK  AssemblerComponent is still a struct (pool writes go through)"
}

$red = $assembler.Fields | Where-Object { $_.Name -eq 'recipeExecuteData' }
if (-not $red -or $red.FieldType.FullName -ne 'RecipeExecuteData') {
    $failures.Add("AssemblerComponent.recipeExecuteData is missing or changed type; the anomaly has nowhere to attach.")
} else {
    Write-Host "OK  AssemblerComponent.recipeExecuteData : RecipeExecuteData"
}

Write-Host ""
Write-Host "Resolved $checked reference(s) into $($interesting -join ', ')."

if ($failures.Count -gt 0) {
    Write-Host ""
    foreach ($failure in $failures) { Write-Host "FAIL  $failure" -ForegroundColor Red }
    throw "$($failures.Count) verification failure(s) against the installed game."
}

Write-Host "All references resolve against the installed assemblies." -ForegroundColor Green
