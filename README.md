# Planetary Anomalies

A Dyson Sphere Program mod exploring planets whose local industrial physics differs from the rest of the galaxy.

**Released on Thunderstore:** https://thunderstore.io/c/dyson-sphere-program/p/pwelty/PlanetaryAnomalies/

Install it with a mod manager (Gale, r2modman) rather than by hand. The rest of this file is for
working on the mod; the player-facing description is [`packaging/README.md`](packaging/README.md).

## What it does

Most non-home planets carry an **anomaly**: one ordinary recipe produces ten times its normal
output there. Which planets, and which recipe on each, is derived from the galaxy seed, so a
galaxy always regenerates the same anomalies and nothing is written to saves.

Anomalies are shown in a planet's description tab once scanned, and machines running an
anomalous recipe are marked in their own window. The player-facing description is
[`packaging/README.md`](packaging/README.md).

Design intent and decisions are in [`PRODUCT.md`](PRODUCT.md). Future directions, sequencing,
and the tests proposed features must pass are in [`ROADMAP.md`](ROADMAP.md). [`SPEC.md`](SPEC.md)
is the original brief and is now partly superseded -- where they disagree, `PRODUCT.md`,
`ROADMAP.md`, and `LOG.md` are current.

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

## The working loop

Gale launches the game; the scripts build into the profile Gale launches.

```bash
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1 -Install
```

then launch from Gale as normal. Quit the game before installing -- DSP holds the plugin DLL open
while it runs, and `install.ps1` will tell you so rather than failing obscurely.

`scripts\local.paths.ps1` (gitignored) pins which profile the scripts target, so no `-ProfileName`
flag is needed. Copy [`scripts\local.paths.example.ps1`](scripts/local.paths.example.ps1) if you
need to change it.

A separate clean `dev` profile was tried and abandoned: launching it outside Gale skips setup that
Gale does, and the other mods this profile carries -- ModFixerOne in particular -- are not
optional in practice. Develop against the profile you actually play.

**Do not install the Thunderstore release into the same profile as a local build.** Both declare
the same `BepInPlugin` GUID, so BepInEx loads one and refuses the other. `install.ps1` checks for
this and refuses rather than producing a profile whose behaviour depends on load order.

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
[Info   :Planetary Anomalies] Planetary Anomalies v0.3.0 loaded
[Info   :Planetary Anomalies] Anomalies derived from the galaxy seed; output x10. Density drawn per galaxy, 25-75%.
[Info   :Planetary Anomalies] Patched PlanetFactory.BeforeGameTick() for production, and UIPlanetDetail, UIAssemblerWindow and the star map to disclose anomalies in the planet panel, on the machine, and on star map planet and star labels. Idle until a planet has a factory (i.e. until something is built).
```

Once a save is loaded, the galaxy is characterised and each planet is decided as its factory
first ticks:

```
[Info   :Planetary Anomalies] Galaxy seed 40078654: 147 eligible recipes, 65% of non-home planets anomalous (derived from the seed), anomaly system v1.
[Info   :Planetary Anomalies] No anomaly: Alrami III (planet id 103) (home planet -- never anomalous).
[Info   :Planetary Anomalies] ANOMALY
[Info   :Planetary Anomalies]   Planet:       Zeta Piscium I (id 1201)
[Info   :Planetary Anomalies]   Recipe:       Sorter Mk.III [极速分拣器, id 90]
[Info   :Planetary Anomalies]   Recipe type:  Assemble
[Info   :Planetary Anomalies]   Normally:     2 x Sorter Mk.II + 1 x Electromagnetic Turbine -> 2 x Sorter Mk.III
[Info   :Planetary Anomalies]   Here:         2 x Sorter Mk.II + 1 x Electromagnetic Turbine -> 20 x Sorter Mk.III
[Info   :Planetary Anomalies]   Effect:       output x10 on this planet only
```

And, the first time real machines are actually modified:

```
[Info   :Planetary Anomalies] Anomaly attached to 1 machine on Alrami I (planet id 101). Its output is now x10.
```

That last line is the one that matters. It only prints when the swap really happened, so if it
is missing, nothing was modified.

## Which planets are anomalous, and which recipe

Everything is derived, nothing is stored:

- **Density.** `hash(seed, 0, version, saltDensity)` maps to 25-75%, so galaxies differ from one
  another. `AnomalyChancePercent` in the config overrides it for playtesting.
- **Presence.** A planet is anomalous when `hash(seed, planetId, version, saltPresence) % 100`
  falls under that density. Home planets are excluded outright.
- **Recipe.** Chosen by *rendezvous hashing*: every eligible recipe is weighted for the planet by
  `hash(seed, planetId, version, saltRecipe, recipeId)` and the heaviest wins.

That last one matters. Indexing into a sorted list would tie the choice to the list's length and
order, so adding one recipe -- which `LDBTool` and `CommonAPI` exist to do -- would shift every
planet in the galaxy. Measured over 4000 planets, adding one recipe to a list of 147 changed
99.3% of planets under indexing and 0.9% under rendezvous weighting.

Eligible recipes are `Smelt`, `Assemble` and `Chemical` with exactly one output *item*. A recipe
producing 2 of something is eligible and becomes 20; recipes producing two different items are
excluded.

The hash is FNV-1a plus an avalanche step, deliberately not `String.GetHashCode` or `Random` --
neither is guaranteed stable across runtimes, and this has to reproduce the same galaxy forever.
The avalanche matters because planets in one system have consecutive ids.

## Test procedure

1. Launch from Gale and confirm the startup lines above.
2. Load a **copy** of a developed save -- never your only copy. A fresh game only has the home
   planet, which is never anomalous, so nothing will be visible.
3. Read the log for the density line and the per-planet verdicts.
4. Open an anomalous planet in the star map (`V`), select it, and open its **description tab**.
   The anomaly should be stated there.
5. Build a machine for that planet's own anomalous recipe. `Anomaly attached to N machine(s)`
   should appear in the log as soon as the machine is placed, and the machine's own window should
   show the `ANOMALY` marker.
6. Supply ingredients and watch one cycle complete.

Expected: the output slot gains the multiplied amount while consuming normal inputs, inserters
remove it normally, and the machine keeps cycling -- no deadlock, no runaway duplication. It will
pause when its output buffer cannot take another full batch and resume once something drains it;
that is the vanilla cap scaling with the anomaly, not a bug.

Non-anomalous planets, and anomalous planets running any other recipe, must be untouched.

Production statistics are expected to match the anomalous amount, because DSP feeds the
statistics register from the same counts as the output buffer. Confirm rather than assume.

## Configuration

BepInEx writes `BepInEx/config/com.planetaryanomalies.dsp.cfg` on first run. Edit and relaunch;
values are read when a galaxy is first seen, so a change applies on the next load.

| Setting | Default | Meaning |
| --- | --- | --- |
| `AnomalyChancePercent` | `-1` | `-1` derives density from the seed (25-75%). `0`-`100` forces a density, for playtesting. |
| `OutputMultiplier` | `10` | Output multiplier for an anomalous recipe. |

## Project layout

```
src/
  Plugin.cs                          BepInEx entry point, Harmony setup, config binding
  Anomaly.cs                         the tiny PlanetAnomaly record
  AnomalyManager.cs                  derives anomalies from the seed; per-planet cache
  Patches/
    AssemblerProductionPatch.cs      attaches anomalies to machines (PlanetFactory.BeforeGameTick)
    PlanetDetailPatch.cs             discloses the anomaly in the planet description tab
    AssemblerWindowPatch.cs          marks a machine running its planet's anomalous recipe
scripts/
  common.ps1                         path discovery shared by the scripts
  build.ps1                          compile
  install.ps1                        copy into BepInEx plugins
  launch.ps1                         start DSP against a Gale profile without Gale
  verify.ps1                         re-check references and Harmony targets against the game
  package.ps1                        build the Thunderstore zip (does not upload)
packaging/                           what ships: manifest, player README, changelog, icon
docs/
  inspection.md                      what was read out of the installed assemblies, and why
build/                               build output (gitignored)
dist/                                packaged releases (gitignored)
```

## How the anomaly works

The short version; the reasoning and IL are in [`docs/inspection.md`](docs/inspection.md).

DSP adds a completed cycle's output from `AssemblerComponent.recipeExecuteData.productCounts`.
That field is a per-component **reference** into a static dictionary shared by every assembler
in the galaxy — so editing the array in place would change the recipe everywhere, which is
exactly what this project forbids.

Instead, the mod builds one private `RecipeExecuteData` per anomalous planet (all arrays copied,
product counts multiplied) and assigns it to just those assemblers on that planet running that
planet's anomalous recipe. The game's own production code then produces the anomalous amount by
itself, and nothing shared is ever modified.

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
