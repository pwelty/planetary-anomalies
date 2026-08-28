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

# --- the generator is a compatibility contract, so prove it has not moved -----------------------
#
# A galaxy's identity is whatever AnomalyMath returns. Change any of it and every existing galaxy
# silently becomes a different galaxy -- which happened once, when recipe selection moved from list
# indexing to rendezvous hashing. The rule since: a mod update must never move an existing galaxy.
#
# AnomalyMath deliberately touches no game types, so it compiles and runs on its own here.

$goldenFile = Join-Path $repoRoot 'tests\golden-generator.txt'
$runnerFile = Join-Path $repoRoot 'tests\GoldenRunner.cs'
$mathFile = Join-Path $repoRoot 'src\AnomalyMath.cs'

if (-not (Test-Path $goldenFile)) {
    $failures.Add("tests\golden-generator.txt is missing; the generator is unprotected.")
} elseif ((Test-Path $runnerFile) -and (Test-Path $mathFile)) {
    $goldenExe = Join-Path ([System.IO.Path]::GetTempPath()) ("pa-golden-" + [System.Guid]::NewGuid().ToString('N') + ".exe")
    $csc = Get-CscPath
    $cscOutput = & $csc /nologo /optimize+ /out:$goldenExe $mathFile $runnerFile 2>&1

    if ($LASTEXITCODE -ne 0) {
        $failures.Add("Could not compile the generator check: $cscOutput")
    } else {
        # Compare only data lines. Comments are documentation, not part of the contract, and
        # treating them as contract produces false alarms that teach people to ignore this check.
        $actual = @(& $goldenExe | Where-Object { $_ -and -not $_.StartsWith('#') })
        $expected = @(Get-Content $goldenFile | Where-Object { $_ -and -not $_.StartsWith('#') })
        Remove-Item $goldenExe -Force -ErrorAction SilentlyContinue

        $diffs = New-Object System.Collections.Generic.List[string]
        $max = [Math]::Max($actual.Count, $expected.Count)
        for ($i = 0; $i -lt $max; $i++) {
            $a = if ($i -lt $actual.Count) { $actual[$i] } else { '<missing>' }
            $e = if ($i -lt $expected.Count) { $expected[$i] } else { '<missing>' }
            if ($a -ne $e) { $diffs.Add("      line $($i + 1): expected '$e' but got '$a'") }
        }

        if ($diffs.Count -gt 0) {
            $shown = ($diffs | Select-Object -First 5) -join "`n"
            $more = if ($diffs.Count -gt 5) { "`n      ... and $($diffs.Count - 5) more" } else { '' }
            $failures.Add(@"
THE GENERATOR HAS CHANGED. $($diffs.Count) of $max results moved.

$shown$more

    Every existing galaxy would silently become a different galaxy: planets would gain or lose
    anomalies, or keep an anomaly on a different recipe. A mod update must never do this.

    If this was accidental, revert the change to src\AnomalyMath.cs.
    If it was deliberate, it needs an AnomalySystemVersion bump and a changelog entry saying
    existing galaxies will be re-rolled -- then regenerate tests\golden-generator.txt.
    See ROADMAP.md on version pinning.
"@)
        } else {
            Write-Host "OK  Generator unchanged ($max results match tests\golden-generator.txt)"
        }
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

# The planet panel disclosure depends on three public members and one method. Assert them, since
# a rename would otherwise surface as a silently missing line in the UI rather than an error.
$planetDetail = $gameAsm.MainModule.GetType('UIPlanetDetail')
if ($null -eq $planetDetail) {
    $failures.Add("Harmony target type 'UIPlanetDetail' does not exist in the installed game.")
} else {
    $onSet = $planetDetail.Methods | Where-Object { $_.Name -eq 'OnPlanetDataSet' -and $_.Parameters.Count -eq 0 }
    if (-not $onSet) {
        $failures.Add("Harmony target 'UIPlanetDetail.OnPlanetDataSet()' does not exist in the installed game.")
    } else {
        Write-Host "OK  Harmony target: UIPlanetDetail.OnPlanetDataSet()"
    }

    foreach ($needed in @('planetBrief', 'briefContentRect')) {
        $fld = $planetDetail.Fields | Where-Object { $_.Name -eq $needed }
        if (-not $fld -or -not $fld.IsPublic) {
            $failures.Add("UIPlanetDetail.$needed is missing or no longer public.")
        } else {
            Write-Host "OK  UIPlanetDetail.$needed is public"
        }
    }

    $planetProp = $planetDetail.Properties | Where-Object { $_.Name -eq 'planet' }
    if (-not $planetProp -or -not $planetProp.GetMethod -or -not $planetProp.GetMethod.IsPublic) {
        $failures.Add("UIPlanetDetail.planet has no public getter.")
    } else {
        Write-Host "OK  UIPlanetDetail.planet getter is public"
    }
}

# The machine-level marker. _assemblerId is private and reached by field reference, so a rename
# there would throw at runtime rather than failing to compile -- assert it here instead.
$asmWindow = $gameAsm.MainModule.GetType('UIAssemblerWindow')
if ($null -eq $asmWindow) {
    $failures.Add("Harmony target type 'UIAssemblerWindow' does not exist in the installed game.")
} else {
    $onUpdate = $asmWindow.Methods | Where-Object { $_.Name -eq '_OnUpdate' -and $_.Parameters.Count -eq 0 }
    if (-not $onUpdate) {
        $failures.Add("Harmony target 'UIAssemblerWindow._OnUpdate()' does not exist in the installed game.")
    } else {
        Write-Host "OK  Harmony target: UIAssemblerWindow._OnUpdate()"
    }

    foreach ($needed in @('stateText', 'factory', 'factorySystem')) {
        $fld = $asmWindow.Fields | Where-Object { $_.Name -eq $needed }
        if (-not $fld -or -not $fld.IsPublic) {
            $failures.Add("UIAssemblerWindow.$needed is missing or no longer public.")
        } else {
            Write-Host "OK  UIAssemblerWindow.$needed is public"
        }
    }

    $idField = $asmWindow.Fields | Where-Object { $_.Name -eq '_assemblerId' }
    if (-not $idField) {
        $failures.Add("UIAssemblerWindow._assemblerId is gone; the machine marker reaches it by field reference and would throw.")
    } else {
        Write-Host "OK  UIAssemblerWindow._assemblerId exists (reached by field reference)"
    }
}

# The star map marker. Its correctness rests on planet being written ONLY in _OnInit/_OnFree --
# if a label could be rebound to another planet without _OnInit running, a marker could survive
# onto the wrong world. Assert that, because it is an assumption about behaviour, not just names.
$starmapPlanet = $gameAsm.MainModule.GetType('UIStarmapPlanet')
if ($null -eq $starmapPlanet) {
    $failures.Add("Harmony target type 'UIStarmapPlanet' does not exist in the installed game.")
} else {
    foreach ($needed in @('_OnInit', 'OnPlanetDisplayNameChange')) {
        if (-not ($starmapPlanet.Methods | Where-Object { $_.Name -eq $needed })) {
            $failures.Add("Harmony target 'UIStarmapPlanet.$needed' does not exist in the installed game.")
        } else {
            Write-Host "OK  Harmony target: UIStarmapPlanet.$needed"
        }
    }

    foreach ($needed in @('nameText', 'planet')) {
        $fld = $starmapPlanet.Fields | Where-Object { $_.Name -eq $needed }
        if (-not $fld -or -not $fld.IsPublic) {
            $failures.Add("UIStarmapPlanet.$needed is missing or no longer public.")
        } else {
            Write-Host "OK  UIStarmapPlanet.$needed is public"
        }
    }

    $writers = @()
    foreach ($m in $starmapPlanet.Methods) {
        if (-not $m.HasBody) { continue }
        foreach ($ins in $m.Body.Instructions) {
            $op = $ins.Operand
            if ($ins.OpCode.Name -eq 'stfld' -and $op -is [Mono.Cecil.FieldReference] -and
                $op.Name -eq 'planet' -and $op.DeclaringType.Name -eq 'UIStarmapPlanet') {
                if ($writers -notcontains $m.Name) { $writers += $m.Name }
            }
        }
    }
    $unexpected = $writers | Where-Object { $_ -notin @('_OnInit', '_OnFree') }
    if ($unexpected) {
        $failures.Add("UIStarmapPlanet.planet is now also written in: $($unexpected -join ', '). A label may be rebound to a different planet without _OnInit running, so the star map marker could survive onto the wrong world. Re-check StarmapPlanetPatch.")
    } else {
        Write-Host "OK  UIStarmapPlanet.planet still written only in _OnInit/_OnFree"
    }
}

# The star-level count, same shape and same assumption as the planet label.
$starmapStar = $gameAsm.MainModule.GetType('UIStarmapStar')
if ($null -eq $starmapStar) {
    $failures.Add("Harmony target type 'UIStarmapStar' does not exist in the installed game.")
} else {
    foreach ($needed in @('_OnInit', 'OnStarDisplayNameChange')) {
        if (-not ($starmapStar.Methods | Where-Object { $_.Name -eq $needed })) {
            $failures.Add("Harmony target 'UIStarmapStar.$needed' does not exist in the installed game.")
        } else {
            Write-Host "OK  Harmony target: UIStarmapStar.$needed"
        }
    }

    foreach ($needed in @('nameText', 'star')) {
        $fld = $starmapStar.Fields | Where-Object { $_.Name -eq $needed }
        if (-not $fld -or -not $fld.IsPublic) {
            $failures.Add("UIStarmapStar.$needed is missing or no longer public.")
        } else {
            Write-Host "OK  UIStarmapStar.$needed is public"
        }
    }

    $starWriters = @()
    foreach ($m in $starmapStar.Methods) {
        if (-not $m.HasBody) { continue }
        foreach ($ins in $m.Body.Instructions) {
            $op = $ins.Operand
            if ($ins.OpCode.Name -eq 'stfld' -and $op -is [Mono.Cecil.FieldReference] -and
                $op.Name -eq 'star' -and $op.DeclaringType.Name -eq 'UIStarmapStar') {
                if ($starWriters -notcontains $m.Name) { $starWriters += $m.Name }
            }
        }
    }
    $unexpectedStar = $starWriters | Where-Object { $_ -notin @('_OnInit', '_OnFree') }
    if ($unexpectedStar) {
        $failures.Add("UIStarmapStar.star is now also written in: $($unexpectedStar -join ', '). A label may be rebound to a different star without _OnInit running, so the count could show against the wrong system.")
    } else {
        Write-Host "OK  UIStarmapStar.star still written only in _OnInit/_OnFree"
    }

    $planetsField = $gameAsm.MainModule.GetType('StarData').Fields | Where-Object { $_.Name -eq 'planets' }
    if (-not $planetsField -or -not $planetsField.IsPublic) {
        $failures.Add("StarData.planets is missing or no longer public; the star count cannot enumerate a system.")
    } else {
        Write-Host "OK  StarData.planets is public"
    }
}

# Gas giants are excluded because they cannot host assemblers. If this enum member is renamed the
# comparison would stop matching and gas giants would quietly become anomalous again -- visible
# only as a marker on a planet nobody can build on.
$planetType = $gameAsm.MainModule.GetType('EPlanetType')
if ($null -eq $planetType) {
    $failures.Add("EPlanetType does not exist; the gas giant exclusion cannot compile.")
} elseif (-not ($planetType.Fields | Where-Object { $_.Name -eq 'Gas' })) {
    $failures.Add("EPlanetType.Gas is gone; gas giants would silently become eligible for anomalies again.")
} else {
    Write-Host "OK  EPlanetType.Gas exists (gas giant exclusion)"
}

$typeField = $gameAsm.MainModule.GetType('PlanetData').Fields | Where-Object { $_.Name -eq 'type' }
if (-not $typeField -or -not $typeField.IsPublic) {
    $failures.Add("PlanetData.type is missing or no longer public; gas giants cannot be identified.")
} else {
    Write-Host "OK  PlanetData.type is public"
}

# The discovery gate. PlanetData.scanned is DSP's own record of whether the player has learned
# about a planet; if it disappears, the disclosure rule needs rethinking, not patching.
$planetData = $gameAsm.MainModule.GetType('PlanetData')
$scanned = $planetData.Fields | Where-Object { $_.Name -eq 'scanned' }
if (-not $scanned -or -not $scanned.IsPublic) {
    $failures.Add("PlanetData.scanned is missing or no longer public; the discovery gate is gone.")
} else {
    Write-Host "OK  PlanetData.scanned is public (discovery gate)"
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
