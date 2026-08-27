# Planetary Anomalies

A Dyson Sphere Program mod exploring planets whose local industrial physics differs from the rest of the galaxy.

## Current milestone

**Stage 0:** prove the production-output patch seam by making one ordinary smelter recipe produce 10 units instead of 1, on the home planet only. See [`SPIKE.md`](SPIKE.md).

The complete intended v0.0.1 design is in [`SPEC.md`](SPEC.md). Do not begin the generalized anomaly system until Stage 0 works in the installed game.

Session-by-session history is in [`LOG.md`](LOG.md). What was read out of the installed game — versions, hashes, signatures, the chosen seam and why — is in [`docs/inspection.md`](docs/inspection.md).

## Development authority

Dolphin's currently installed Dyson Sphere Program assemblies are authoritative. Public mods are evidence and examples; their method signatures are not authority for the current game build.

---

# Working on the mod

Written for a developer who has never made a DSP mod. Every command is a full command; nothing
assumes Visual Studio or prior C# setup.

## Requirements

Everything needed is already on a normal Windows machine with the game and a mod manager:

| Need | Where it comes from |
| --- | --- |
| Dyson Sphere Program | Steam. Found automatically via Steam's library list. |
| BepInEx 5 + Harmony | Installed by **Gale**, per profile. Found automatically. |
| A C# compiler | `csc.exe`, shipped with the .NET Framework — already on Windows. |

**No .NET SDK, Visual Studio, or NuGet restore is required.** The build compiles against the
game's own assemblies, which is what a Unity mod binds to at runtime anyway.

The one consequence: the in-box compiler is **C# 5**. No string interpolation (`$"..."`), no
`?.`, no `nameof`, no expression-bodied members. Use `string.Format` or `+`.

## Where things live

**The game** is found by reading Steam's `libraryfolders.vdf`, so a non-default library works.
On this machine it resolves to:

```
C:\Program Files (x86)\Steam\steamapps\common\Dyson Sphere Program
```

**BepInEx is not in the game folder.** Gale keeps a complete BepInEx tree per profile:

```
%APPDATA%\com.kesomannen.gale\dyson-sphere-program\profiles\<profile>\BepInEx\
```

The game folder does contain a leftover `winhttp.dll` and `doorstop_config.ini`, but no
`BepInEx` folder — so **launching DSP straight from Steam loads no mods at all**. Launch from
Gale.

If discovery ever fails or picks the wrong thing, override it, in this order of precedence:

1. `-GameDir` / `-BepInExDir` / `-ProfileName` parameters on the scripts
2. `DSP_DIR` and `DSP_BEPINEX_DIR` environment variables
3. `scripts\local.paths.ps1` — copy [`scripts\local.paths.example.ps1`](scripts/local.paths.example.ps1); it is gitignored

## Build

```bash
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

Produces `build\PlanetaryAnomalies.dll`.

## Install

```bash
powershell -ExecutionPolicy Bypass -File .\scripts\install.ps1
```

Copies the DLL to `<profile>\BepInEx\plugins\PlanetaryAnomalies\`. If more than one Gale
profile has BepInEx, the script stops and asks you to pick one with `-ProfileName '<name>'`.

Build and install together:

```bash
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1 -Install
```

## Verify against the installed game

```bash
powershell -ExecutionPolicy Bypass -File .\scripts\verify.ps1
```

Re-resolves every type, field and method the built DLL references against the assemblies
*currently installed*, and asserts that the Harmony target `PlanetFactory.BeforeGameTick()`
still exists, that `AssemblerComponent` is still a struct, that `recipeExecuteData` is still
there, and that `GameLogic.FactoryBeforeGameTick` still calls the hook and still has no
`_Parallel` twin. A game update that moves any of this fails here rather than becoming a silent
no-op in game — which is not hypothetical: an earlier hook did exactly that (see below).

This is a static check. **It is not evidence the anomaly works.** Only playing the game is.

## Run

Launch DSP **from Gale**, using the profile you installed into.

## Logs

```
%APPDATA%\com.kesomannen.gale\dyson-sphere-program\profiles\<profile>\BepInEx\LogOutput.log
```

On startup you should see:

```
[Info   :Planetary Anomalies] Planetary Anomalies v0.0.1 loaded
[Info   :Planetary Anomalies] Stage 0: one hard-coded smelting recipe produces x10 output on the home planet only.
[Info   :Planetary Anomalies] Game version: 0.10.34.xxxxx (build xxxxx)
[Info   :Planetary Anomalies] Patched PlanetFactory.BeforeGameTick(). Idle until a planet has a factory (i.e. until something is built).
```

Once a game is loaded, the anomaly is resolved and logged in full:

```
[Info   :Planetary Anomalies] ANOMALY
[Info   :Planetary Anomalies]   Galaxy seed:  <seed>
[Info   :Planetary Anomalies]   Planet:       <name> (home planet, id <id>)
[Info   :Planetary Anomalies]   Recipe:       Iron Ingot [铁块, id 1]
[Info   :Planetary Anomalies]   Recipe type:  Smelt
[Info   :Planetary Anomalies]   Normally:     1 x Iron Ore -> 1 x Iron Ingot
[Info   :Planetary Anomalies]   Here:         1 x Iron Ore -> 10 x Iron Ingot
[Info   :Planetary Anomalies]   Effect:       output x10 on this planet only
```

And, the first time a real machine is actually modified:

```
[Info   :Planetary Anomalies] Anomaly attached to assembler #3 on <planet> (planet id <id>). This machine's output is now x10.
```

That last line is the one that matters. It only prints when the swap really happened, so if it
is missing, nothing was modified.

## Which recipe gets the anomaly

Stage 0 hard-codes one recipe: the starting iron-ore-to-iron-ingot smelt, expected to be
recipe id `1`.

DSP keeps its proto database inside Unity assets, so recipe ids cannot be read off disk the way
method signatures can. The id is therefore treated as a **guess that is verified at runtime**:
before using it the plugin checks the loaded recipe really is a single-input, single-output
`Smelt` recipe. If it is not, the plugin logs a warning and falls back to the lowest-id recipe
that is. Either way the log states exactly which recipe, and which items, it settled on — so
the run is reproducible without trusting the constant.

## Test procedure

1. Launch from Gale and confirm the four startup lines above.
2. Start a **new game**, or load a **copy** of a save — never your only copy.
3. Read the `ANOMALY` block to see the recipe and home planet.
4. Build a smelter for that recipe on the home planet.
5. Feed it enough ore for one cycle.
6. Watch one cycle complete.

Expected: the smelter's output slot gains **10** ingots, not 1, while consuming the normal
1 ore. An inserter should remove them normally, and the machine should keep cycling — no
deadlock, no runaway duplication.

Production statistics are expected to match the anomalous amount, because DSP feeds the
statistics register from the same counts as the output buffer. Confirm rather than assume; per
[`SPIKE.md`](SPIKE.md) a statistics mismatch is recorded but does not block Stage 0.

The negative case — the same recipe producing normally on another planet — cannot be witnessed
until interplanetary travel is available. The planet guard is implemented and logged from this
first build regardless.

## Project layout

```
src/
  Plugin.cs                          BepInEx entry point, Harmony setup, startup logging
  Anomaly.cs                         the tiny PlanetAnomaly record
  AnomalyManager.cs                  resolves home planet + recipe, builds the private recipe data
  Patches/
    AssemblerProductionPatch.cs      the Harmony prefix that attaches the anomaly
scripts/
  common.ps1                         path discovery shared by the scripts
  build.ps1                          compile
  install.ps1                        copy into BepInEx plugins
  verify.ps1                         re-check references against the installed game
docs/
  inspection.md                      what was read out of the installed assemblies, and why the seam was chosen
build/                               build output (gitignored)
```

## How the anomaly works

The short version; the reasoning and IL are in [`docs/inspection.md`](docs/inspection.md).

DSP adds a completed cycle's output from `AssemblerComponent.recipeExecuteData.productCounts`.
That field is a per-component **reference** into a static dictionary shared by every assembler
in the galaxy — so editing the array in place would change the recipe everywhere, which is
exactly what this project forbids.

Instead, the mod builds one private `RecipeExecuteData` (all arrays copied, product counts
×10) and assigns it to just those assemblers that are on the home planet and running the target
recipe. The game's own production code then produces the anomalous amount by itself.

Two consequences worth knowing:

- It covers **both** execution paths. `InternalUpdate` is called from `FactorySystem.GameTick`
  *and* from `GameLogic._assembler_parallel` when multithreading is on. Because the anomaly is
  data hanging off the component, neither path needs patching.

**A trap worth knowing before you add any per-tick hook here.** `GameLogic.OnGameLogicFrame`
dispatches most factory phases as a *pair* — a sequential method and a `_Parallel` twin — and
picks between them on thread count. Multithreading is the default, so hooking a sequential-only
phase produces a mod that loads cleanly, logs nothing, and does nothing. The first version of
this mod hooked `FactorySystem.GameTick` and did exactly that. Before trusting any per-tick
method, check whether a `_Parallel` sibling exists; `PlanetFactory.BeforeGameTick` is safe
because `GameLogic.FactoryBeforeGameTick` has none and is gated only on being the main thread.
- It **cannot leak into a save**. DSP persists only the recipe id and reassigns the shared
  instance on load, so a save written with the mod installed loads vanilla without it.
