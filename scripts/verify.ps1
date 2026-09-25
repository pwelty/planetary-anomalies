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
    foreach ($needed in @('_OnInit', 'OnPlanetDisplayNameChange', '_OnLateUpdate')) {
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
    foreach ($needed in @('_OnInit', 'OnStarDisplayNameChange', '_OnLateUpdate')) {
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

# The second half of the disclosure rule: an anomaly is hidden until its recipe is researched.
# RecipeUnlocked is what the game itself asks, so if it moves, the mod should stop rather than
# quietly fall back to showing everything.
$history = $gameAsm.MainModule.GetType('GameHistoryData')
if (-not $history) {
    $failures.Add("GameHistoryData is gone; the research gate cannot be evaluated.")
} else {
    $recipeUnlocked = $history.Methods | Where-Object {
        $_.Name -eq 'RecipeUnlocked' -and $_.IsPublic -and $_.Parameters.Count -eq 1 -and
        $_.Parameters[0].ParameterType.FullName -eq 'System.Int32' -and
        $_.ReturnType.FullName -eq 'System.Boolean'
    }
    if (-not $recipeUnlocked) {
        $failures.Add("GameHistoryData.RecipeUnlocked(int) -> bool is missing; the research gate is gone.")
    } else {
        Write-Host "OK  GameHistoryData.RecipeUnlocked(int) is public (research gate)"
    }
}

# RecipeProto.preTech is how the log names the technology a hidden anomaly is waiting on.
$recipeProto = $gameAsm.MainModule.GetType('RecipeProto')
$preTech = $recipeProto.Fields | Where-Object { $_.Name -eq 'preTech' -and $_.IsPublic }
if (-not $preTech) {
    $failures.Add("RecipeProto.preTech is missing or no longer public; hidden anomalies cannot name their technology.")
} else {
    Write-Host "OK  RecipeProto.preTech is public (names the blocking technology)"
}

# --- the research announcement -----------------------------------------------------------------

$notify = $history.Methods | Where-Object {
    $_.Name -eq 'NotifyTechUnlock' -and $_.Parameters.Count -eq 3 -and
    $_.Parameters[0].ParameterType.FullName -eq 'System.Int32'
}
if (-not $notify) {
    $failures.Add("Harmony target 'GameHistoryData.NotifyTechUnlock(int, int, bool)' is gone; research announcements would silently stop.")
} else {
    Write-Host "OK  Harmony target: GameHistoryData.NotifyTechUnlock(int, int, bool)"

    # The postfix takes the technology id BY NAME. A rename compiles, then fails to patch at load.
    if ($notify.Parameters[0].Name -ne '_techId') {
        $failures.Add("GameHistoryData.NotifyTechUnlock's first parameter is now '$($notify.Parameters[0].Name)', not '_techId'; the postfix binds it by name and would fail to patch, turning research announcements off.")
    } else {
        Write-Host "OK  NotifyTechUnlock's technology id is still named _techId (bound by name)"
    }
}

# --- ReplicatorFollowsPlanet ------------------------------------------------------------------------
# The postfix reads the ForgeTask that AddTaskIterate returns and rewrites its productCounts; where
# Icarus stands comes from GameMain.localPlanet.
$forge = $gameAsm.MainModule.GetType('MechaForge')
$addIterate = $forge.Methods | Where-Object { $_.Name -eq 'AddTaskIterate' -and $_.ReturnType.Name -eq 'ForgeTask' } | Select-Object -First 1
if (-not $addIterate) {
    $failures.Add("MechaForge.AddTaskIterate no longer returns a ForgeTask; ReplicatorFollowsPlanet would fail to patch.")
} else {
    Write-Host "OK  Harmony target: MechaForge.AddTaskIterate returns ForgeTask"
}
$forgeTask = $gameAsm.MainModule.GetType('ForgeTask')
foreach ($needed in @('recipeId', 'productCounts', 'count')) {
    $f = $forgeTask.Fields | Where-Object { $_.Name -eq $needed -and $_.IsPublic }
    if (-not $f) {
        $failures.Add("ForgeTask.$needed is missing or no longer public; ReplicatorFollowsPlanet cannot read or change the job.")
    } else {
        Write-Host "OK  ForgeTask.$needed is public"
    }
}
$localPlanet = $gameAsm.MainModule.GetType('GameMain').Properties | Where-Object { $_.Name -eq 'localPlanet' -and $_.GetMethod -and $_.GetMethod.IsStatic -and $_.GetMethod.IsPublic }
if (-not $localPlanet) {
    $failures.Add("GameMain.localPlanet is missing or no longer public static; the replicator cannot tell where Icarus is.")
} else {
    Write-Host "OK  GameMain.localPlanet is public static"
}
# The queue count is rewritten after UIReplicatorWindow.ActiveQueueText(int index); the postfix binds the
# index and two private fields by name.
$repWin = $gameAsm.MainModule.GetType('UIReplicatorWindow')
$aqt = $repWin.Methods | Where-Object { $_.Name -eq 'ActiveQueueText' -and $_.Parameters.Count -eq 1 -and $_.Parameters[0].Name -eq 'index' } | Select-Object -First 1
$tq = $repWin.Fields | Where-Object { $_.Name -eq 'taskQueue' }
$qt = $repWin.Fields | Where-Object { $_.Name -eq 'queueNumTexts' }
if (-not $aqt -or -not $tq -or -not $qt) {
    $failures.Add("UIReplicatorWindow.ActiveQueueText(index), taskQueue or queueNumTexts has changed; the replicator queue would show the unmultiplied count.")
} else {
    Write-Host "OK  Harmony target: UIReplicatorWindow.ActiveQueueText(index), with taskQueue and queueNumTexts by name"
}

$techProto = $gameAsm.MainModule.GetType('TechProto')
$unlockRecipes = $techProto.Fields | Where-Object { $_.Name -eq 'UnlockRecipes' -and $_.IsPublic }
if (-not $unlockRecipes) {
    $failures.Add("TechProto.UnlockRecipes is missing or no longer public; a technology cannot be mapped to its recipes.")
} else {
    Write-Host "OK  TechProto.UnlockRecipes is public"
}

# Announcements are raised through the positioned overload, so they can sit under the game's own
# "Research complete" notice instead of at the cursor, and the notice's text is what they anchor to.
$tipsType = $gameAsm.MainModule.GetType('UIGeneralTips')
$positioned = $tipsType.Methods | Where-Object {
    $_.Name -eq 'InvokeRealtimeTip' -and $_.IsPublic -and $_.Parameters.Count -eq 4 -and
    $_.Parameters[0].ParameterType.FullName -eq 'System.String' -and
    $_.Parameters[1].ParameterType.Name -eq 'Vector2' -and
    $_.Parameters[2].ParameterType.FullName -eq 'System.Single' -and
    $_.Parameters[3].ParameterType.FullName -eq 'System.Single'
}
if (-not $positioned) {
    $failures.Add("UIGeneralTips.InvokeRealtimeTip(string, Vector2, float, float) is gone; announcements could not be placed on screen.")
} else {
    Write-Host "OK  UIGeneralTips.InvokeRealtimeTip(string, Vector2, float, float) is public (positioned announcements)"
}
foreach ($needed in @('researchCompleteText', 'tipPanelRect', 'realtimeTipPrefab')) {
    $tf = $tipsType.Fields | Where-Object { $_.Name -eq $needed -and $_.IsPublic }
    if (-not $tf) {
        $failures.Add("UIGeneralTips.$needed is missing or no longer public; announcements could not be placed under the research notice.")
    } else {
        Write-Host "OK  UIGeneralTips.$needed is public"
    }
}
$rectField = $gameAsm.MainModule.GetType('UIRealtimeTip').Fields | Where-Object { $_.Name -eq 'rectTrans' -and $_.IsPublic }
if (-not $rectField) {
    $failures.Add("UIRealtimeTip.rectTrans is missing or no longer public; announcements could not be centred.")
} else {
    Write-Host "OK  UIRealtimeTip.rectTrans is public"
}

# The assumption that makes announcing safe. If Import ever starts unlocking technologies, loading
# a mature save would fire one announcement per researched tech -- a wall of tips at the worst
# possible moment. This is behaviour rather than shape, so nothing else would catch it.
$import = ($history.Methods | Where-Object { $_.Name -eq 'Import' })[0]
$unlocksOnImport = $import.Body.Instructions | Where-Object {
    $_.Operand -and ($_.Operand.Name -eq 'NotifyTechUnlock' -or $_.Operand.Name -eq 'UnlockTech' -or $_.Operand.Name -eq 'AddTechHash')
}
if ($unlocksOnImport) {
    $failures.Add("GameHistoryData.Import now calls the technology unlock path; loading a save would announce every researched technology at once.")
} else {
    Write-Host "OK  GameHistoryData.Import still does not unlock technologies (announcements stay quiet on load)"
}

# --- the experimental combat-settings multiplier ------------------------------------------------
$gameDesc = $gameAsm.MainModule.GetType('GameDesc')
foreach ($needed in @('isPeaceMode', 'combatSettings')) {
    $f = $gameDesc.Fields | Where-Object { $_.Name -eq $needed -and $_.IsPublic }
    if (-not $f) {
        $failures.Add("GameDesc.$needed is missing or no longer public; the combat-settings multiplier cannot read it.")
    } else {
        Write-Host "OK  GameDesc.$needed is public"
    }
}
$combat = $gameAsm.MainModule.GetType('CombatSettings')
$passive = $combat.Properties | Where-Object { $_.Name -eq 'isEnemyPassive' -and $_.GetMethod -and $_.GetMethod.IsPublic }
if (-not $passive) {
    $failures.Add("CombatSettings.isEnemyPassive is missing or no longer public; passive enemies cannot be detected.")
} else {
    Write-Host "OK  CombatSettings.isEnemyPassive getter is public"
}

# --- the menu demo galaxy must stay untouched --------------------------------------------------
$dspGame = $gameAsm.MainModule.GetType('DSPGame')
$menuDemo = $dspGame.Fields | Where-Object { $_.Name -eq 'IsMenuDemo' -and $_.IsPublic -and $_.IsStatic }
if (-not $menuDemo) {
    $failures.Add("DSPGame.IsMenuDemo is missing or no longer public static; the mod would run against the galaxy behind the main menu.")
} else {
    Write-Host "OK  DSPGame.IsMenuDemo is public static (menu demo excluded)"
}
# --- the anomaly line in the item tooltip ------------------------------------------------------
# Harmony binds the postfix's parameters by NAME, so SetTip must still have an int named itemId and a
# bool named isRecipe. A rename would not fail to compile; it would fail to patch, at load, silently.
$itemTip = $gameAsm.MainModule.GetType('UIItemTip')
$setTip = $itemTip.Methods | Where-Object { $_.Name -eq 'SetTip' -and $_.IsPublic } | Select-Object -First 1
if (-not $setTip) {
    $failures.Add("Harmony target 'UIItemTip.SetTip' is gone; the tooltip anomaly line would never appear.")
} else {
    $pItem = $setTip.Parameters | Where-Object { $_.Name -eq 'itemId' -and $_.ParameterType.FullName -eq 'System.Int32' }
    $pRecipe = $setTip.Parameters | Where-Object { $_.Name -eq 'isRecipe' -and $_.ParameterType.FullName -eq 'System.Boolean' }
    if (-not $pItem -or -not $pRecipe) {
        $failures.Add("UIItemTip.SetTip no longer has parameters named 'itemId' (int) and 'isRecipe' (bool); the postfix would fail to bind.")
    } else {
        Write-Host "OK  Harmony target: UIItemTip.SetTip, with itemId and isRecipe bound by name"
    }

    # The replicator grid passes a recipe as a NEGATIVE itemId, and the postfix relies on that: a
    # negative id is looked up as a recipe. SetTip's own prologue is the proof -- it negates the
    # argument (ldarg.1; neg) to recover the recipe id. If that ever goes, the grid would silently
    # lose its anomaly line again, which is how this was found in the first place.
    $ins = $setTip.Body.Instructions
    $negatesArg = $false
    for ($i = 1; $i -lt [Math]::Min($ins.Count, 40); $i++) {
        if ($ins[$i].OpCode.Name -eq 'neg' -and $ins[$i - 1].OpCode.Name -eq 'ldarg.1') { $negatesArg = $true; break }
    }
    if (-not $negatesArg) {
        $failures.Add("UIItemTip.SetTip no longer negates a negative itemId in its prologue; the replicator grid may no longer pass recipe ids as negative numbers, and the tooltip line would vanish from the grid.")
    } else {
        Write-Host "OK  UIItemTip.SetTip treats a negative itemId as a recipe id (the replicator grid's convention)"
    }
}
foreach ($needed in @('trans', 'descText')) {
    $f = $itemTip.Fields | Where-Object { $_.Name -eq $needed -and $_.IsPublic }
    if (-not $f) {
        $failures.Add("UIItemTip.$needed is missing or no longer public; the tooltip anomaly line could not be placed.")
    } else {
        Write-Host "OK  UIItemTip.$needed is public"
    }
}
$itemProto = $gameAsm.MainModule.GetType('ItemProto')
$recipesField = $itemProto.Fields | Where-Object { $_.Name -eq 'recipes' -and $_.IsPublic }
if (-not $recipesField) {
    $failures.Add("ItemProto.recipes is missing or no longer public; an item could not be mapped to the recipes that make it.")
} else {
    Write-Host "OK  ItemProto.recipes is public (item -> recipes)"
}
$uiRootType = $gameAsm.MainModule.GetType('UIRoot')
$tipArea = $uiRootType.Fields | Where-Object { $_.Name -eq 'itemTipTransform' -and $_.IsPublic }
if (-not $tipArea) {
    $failures.Add("UIRoot.itemTipTransform is missing or no longer public; a grown tooltip could not be kept on screen.")
} else {
    Write-Host "OK  UIRoot.itemTipTransform is public (tooltip clamp)"
}

# --- announcements wait for a UI that can show them --------------------------------------------
# UIRealtimeTip.Popup begins "if (!UIRoot.instance.uiGame.active) return", so a tip raised during a
# load is silently discarded. That happened in play: eight announcements fired while a save was
# loading and none of them ever reached the screen. The queue now drains from UIGeneralTips._OnUpdate
# only while the game is running and the UI is up, so these three must exist.
$generalTipsForPump = $gameAsm.MainModule.GetType('UIGeneralTips')
$tipsUpdate = $generalTipsForPump.Methods | Where-Object { $_.Name -eq '_OnUpdate' -and -not $_.IsStatic }
if (-not $tipsUpdate) {
    $failures.Add("Harmony target 'UIGeneralTips._OnUpdate' is gone; queued announcements would never be shown.")
} else {
    Write-Host "OK  Harmony target: UIGeneralTips._OnUpdate (announcement queue)"
}
$manual = $gameAsm.MainModule.GetType('ManualBehaviour')
$activeProp = $manual.Properties | Where-Object { $_.Name -eq 'active' -and $_.GetMethod -and $_.GetMethod.IsPublic }
if (-not $activeProp) {
    $failures.Add("ManualBehaviour.active is missing or no longer public; the mod could not tell whether the UI can show a tip.")
} else {
    Write-Host "OK  ManualBehaviour.active is public (UI-ready gate)"
}
foreach ($needed in @('isRunning', 'isPaused', 'isLoading')) {
    $gp = $gameAsm.MainModule.GetType('GameMain').Properties | Where-Object { $_.Name -eq $needed -and $_.GetMethod -and $_.GetMethod.IsPublic -and $_.GetMethod.IsStatic }
    if (-not $gp) {
        $failures.Add("GameMain.$needed is missing or no longer a public static property; announcements could fire during a load again.")
    } else {
        Write-Host "OK  GameMain.$needed is a public static property"
    }
}

# The queue is drained from UIGeneralTips._OnUpdate, and that only runs because UIGame._OnUpdate calls
# generalTips._Update() and ManualBehaviour._Update calls _OnUpdate. Behaviour, not shape: if either
# link breaks, announcements queue forever and nothing else would notice.
$uiGameType = $gameAsm.MainModule.GetType('UIGame')
$uiGameUpdate = $uiGameType.Methods | Where-Object { $_.Name -eq '_OnUpdate' } | Select-Object -First 1
$drivesTips = $false
if ($uiGameUpdate -and $uiGameUpdate.HasBody) {
    $ins = $uiGameUpdate.Body.Instructions
    for ($k = 0; $k -lt $ins.Count - 1; $k++) {
        if ($ins[$k].Operand -and $ins[$k].Operand.Name -eq 'generalTips') {
            for ($j = $k + 1; $j -le [Math]::Min($k + 3, $ins.Count - 1); $j++) {
                if ($ins[$j].Operand -and $ins[$j].Operand.Name -eq '_Update') { $drivesTips = $true }
            }
        }
    }
}
if (-not $drivesTips) {
    $failures.Add("UIGame._OnUpdate no longer calls generalTips._Update(); queued announcements would never drain.")
} else {
    Write-Host "OK  UIGame._OnUpdate still calls generalTips._Update() (announcement queue is driven)"
}
$manualUpdate = $gameAsm.MainModule.GetType('ManualBehaviour').Methods | Where-Object { $_.Name -eq '_Update' } | Select-Object -First 1
$callsOnUpdate = $manualUpdate -and $manualUpdate.HasBody -and ($manualUpdate.Body.Instructions | Where-Object { $_.Operand -and $_.Operand.Name -eq '_OnUpdate' })
if (-not $callsOnUpdate) {
    $failures.Add("ManualBehaviour._Update no longer calls _OnUpdate; the announcement queue hook would never run.")
} else {
    Write-Host "OK  ManualBehaviour._Update still calls _OnUpdate"
}

# --- announcement tips stay long enough to read ------------------------------------------------
# TechUnlockPatch stretches its own tips after the game creates them. Two of the fields it needs are
# private and reached by name, which the compiler cannot check -- so they are asserted here. If
# either goes, tips silently fall back to the game's 1.5 seconds, which play showed is unreadable.
$generalTipsType = $gameAsm.MainModule.GetType('UIGeneralTips')
$tipsField = $generalTipsType.Fields | Where-Object { $_.Name -eq 'realtimeTips' -and $_.FieldType.FullName -match 'List`1<UIRealtimeTip>' }
if (-not $tipsField) {
    $failures.Add("UIGeneralTips.realtimeTips (List<UIRealtimeTip>) is gone; announcement tips would fall back to the game's unreadable 1.5 seconds.")
} else {
    Write-Host "OK  UIGeneralTips.realtimeTips : List<UIRealtimeTip> (reached by name)"
}
$realtimeTipType = $gameAsm.MainModule.GetType('UIRealtimeTip')
$lifeField = $realtimeTipType.Fields | Where-Object { $_.Name -eq 'lifeTime' -and $_.FieldType.FullName -eq 'System.Single' }
if (-not $lifeField) {
    $failures.Add("UIRealtimeTip.lifeTime (float) is gone; announcement tips could not be kept on screen long enough to read.")
} else {
    Write-Host "OK  UIRealtimeTip.lifeTime : float (reached by name)"
}
foreach ($needed in @('upSpeed', 'delayTime', 'textComp')) {
    $pf = $realtimeTipType.Fields | Where-Object { $_.Name -eq $needed -and $_.IsPublic }
    if (-not $pf) {
        $failures.Add("UIRealtimeTip.$needed is missing or no longer public; announcement tips cannot be slowed and queued.")
    } else {
        Write-Host "OK  UIRealtimeTip.$needed is public"
    }
}
# The six seconds are computed from the rate Update burns lifeTime at. Behaviour, not shape: if the
# rate changes, the duration silently changes with it.
$tipUpdate = $realtimeTipType.Methods | Where-Object { $_.Name -eq 'Update' } | Select-Object -First 1
$decay = $null
if ($tipUpdate -and $tipUpdate.HasBody) {
    $decay = $tipUpdate.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'ldc.r4' -and [Math]::Abs([double]$_.Operand - 0.6666666) -lt 0.00001 }
}
if (-not $decay) {
    $failures.Add("UIRealtimeTip.Update no longer burns lifeTime at 0.6666666 per second; the announcement duration in TechUnlockPatch would be wrong.")
} else {
    Write-Host "OK  UIRealtimeTip.Update still burns lifeTime at 2/3 per second (announcement duration)"
}

# The announcement pins lifeTime at 0.8, which is only right while Update derives a tip's width from
# lifeTime as sqrt(clamp01(0.2 + 7 x (1 - lifeTime))): full width at 0.886 or below, zero above ~1.03.
# Extending a tip by raising lifeTime instead made it invisible for four and a half seconds and went
# unnoticed for a week, because the log said it was shown. If the game changes this, 0.8 must be
# re-derived, and it is worth knowing before a player finds out.
$hasScale = $false
if ($tipUpdate -and $tipUpdate.HasBody) {
    $consts = @($tipUpdate.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'ldc.r4' } | ForEach-Object { [double]$_.Operand })
    $callsScale = @($tipUpdate.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'set_localScale' }).Count -gt 0
    $callsSqrt = @($tipUpdate.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'Sqrt' }).Count -gt 0
    $has7 = @($consts | Where-Object { [Math]::Abs($_ - 7.0) -lt 0.0001 }).Count -gt 0
    $has02 = @($consts | Where-Object { [Math]::Abs($_ - 0.2) -lt 0.0001 }).Count -gt 0
    $hasScale = $callsScale -and $callsSqrt -and $has7 -and $has02
}
if (-not $hasScale) {
    $failures.Add("UIRealtimeTip.Update no longer sets its width as sqrt(clamp01(0.2 + 7 x (1 - lifeTime))); the announcement's HoldLifetime of 0.8 was derived from that and must be re-checked.")
} else {
    Write-Host "OK  UIRealtimeTip.Update still derives width from lifeTime (announcement holds it at 0.8 for full width)"
}

$gameMain = $gameAsm.MainModule.GetType('GameMain')
$historyProp = $gameMain.Properties | Where-Object {
    $_.Name -eq 'history' -and $_.GetMethod -and $_.GetMethod.IsStatic -and $_.GetMethod.IsPublic
}
if (-not $historyProp) {
    $failures.Add("GameMain.history is missing or no longer a public static property; the research gate is unreachable.")
} else {
    Write-Host "OK  GameMain.history is a public static property"
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
