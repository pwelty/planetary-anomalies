Planetary Anomalies — Codex Implementation Spec

Goal

Build a very small *Dyson Sphere Program* mod that proves one core mechanic:

> A standard DSP recipe can behave differently depending on the planet where production occurs.
For the first version, the player's *home planet* receives exactly one production anomaly:

> One randomly selected standard recipe produces *2× its normal output* on the home planet only.
This is an experimental spike, not the finished mod. Do not build scanning, discovery, UI, multiple anomaly types, or galaxy-wide generation yet.

The purpose of v0.0.1 is to prove that *planet-local recipe warping is technically possible*.

Product Concept

The eventual mod is inspired by *Stellaris anomalies*, except anomalies modify local industrial physics.

A planet might eventually have effects such as:
 • Gears produce ×2 output.
 • Processors require half as much silicon.
 • Titanium alloy does not require sulfuric acid.
 • Graphene produces ×3 output.
 • A recipe runs ×5 faster.
 • An ingredient is replaced by another ingredient.
 • A recipe produces an unusual byproduct.
These effects are:
 • procedurally assigned,
 • stable for a given galaxy,
 • attached to individual planets,
 • hidden until discovered,
 • intentionally not balanced.
The goal is to make exploration meaningful by occasionally discovering planets with unusually valuable industrial properties.

Do *not* implement this full system yet.

v0.0.1 Scope

Implement exactly one anomaly.

When a new game is running and the player's birth/home planet is available:
 1. Identify the home planet.
 2. Select one eligible standard production recipe at random.
 3. Record an anomaly:
`planet = home planet
recipe = randomly selected recipe
effect = output ×2` 1. Write the anomaly to the BepInEx log.
 2. Whenever that recipe executes on the home planet, it must produce twice the normal quantity.
 3. The same recipe on any other planet must behave normally.
Example:

`ANOMALY
Planet: Mediterranean
Recipe: Circuit Board
Effect: Output ×2`
Normally:

`2 Iron + 1 Copper
→ 2 Circuit Boards`
On the anomalous home planet:

`2 Iron + 1 Copper
→ 4 Circuit Boards`
Everywhere else:

`2 Iron + 1 Copper
→ 2 Circuit Boards`
Critical Technical Requirement

Do not modify the global recipe definition

Do *not* implement the anomaly by changing something equivalent to:

`RecipeProto.ResultCounts`
or any other globally shared recipe data.

That would cause the altered recipe to apply throughout the galaxy.

The effect must occur *during execution of the recipe inside a particular PlanetFactory / FactorySystem / assembler context*.

The implementation must know:

`Which planet is this machine on?
Which recipe is this machine executing?
Did this production cycle complete?
How much output should this completed cycle create?`
The modifier must only alter the final output quantity when:

`factory planet ID == anomaly planet ID

AND

assembler recipe ID == anomaly recipe ID`
Development Approach

Use the normal DSP mod stack:
 • C#
 • BepInEx
 • Harmony
 • Dyson Sphere Program game assemblies
Do not introduce CommonAPI, GalacticScale, or other mod dependencies for v0.0.1 unless they prove absolutely necessary.

Prefer the smallest possible dependency surface.

Environment

Assume development is being done on *Windows* by someone who is an experienced developer but is not familiar with:
 • C#
 • .NET game modding
 • Unity internals
 • BepInEx
 • Harmony
 • Dyson Sphere Program internals
Therefore:
 • keep project setup explicit,
 • avoid unexplained C# conventions,
 • automate build/copy steps where practical,
 • provide useful logs,
 • document exactly where generated files go.
Do not assume Visual Studio is required.

The project should be comfortable to edit/build using:
 • Codex
 • terminal / PowerShell
 • VS Code or another editor
Use the simplest currently supported .NET/C# project configuration compatible with DSP and BepInEx.

First Task: Inspect the Current Game

Before implementing the production patch, inspect the locally installed DSP assemblies.

Locate the game's managed assemblies, particularly:

`Assembly-CSharp.dll
UnityEngine*.dll`
and the BepInEx assemblies after BepInEx is installed.

Determine the current definitions and relevant call paths for at least:

`AssemblerComponent
FactorySystem
PlanetFactory
RecipeProto
GameMain
GameData`
Specifically inspect:

`AssemblerComponent.InternalUpdate
FactorySystem.GameTick`
or their current equivalents.

Do not assume field names or method signatures from old GitHub mods are still correct.

The source of truth is the user's currently installed version of DSP.

Reverse-Engineering Objective

Find the exact code path where a completed production cycle:
 1. determines that enough ingredients exist,
 2. consumes ingredients,
 3. completes the recipe timer,
 4. adds recipe results to the assembler output buffer,
 5. updates production statistics.
We need to intercept or alter *step 4*.

Ideal conceptual location:

`normalOutputAmount = recipe output amount

if anomalous planet + anomalous recipe:
    outputAmount *= 2

add outputAmount to assembler output`
Prefer a Harmony prefix/postfix if possible.

Use a Harmony transpiler only if there is no reasonably safe higher-level hook.

Avoid modifying globally shared structures temporarily unless there is no better solution.

DSP may update multiple factories or machines in ways that make temporary mutation of shared recipe state unsafe.

Project Structure

Use something roughly like:

`PlanetaryAnomalies/
│
├─ README.md
├─ PlanetaryAnomalies.csproj
│
├─ src/
│  ├─ Plugin.cs
│  ├─ Anomaly.cs
│  ├─ AnomalyManager.cs
│  │
│  └─ Patches/
│     └─ AssemblerProductionPatch.cs
│
└─ scripts/
   ├─ build.ps1
   └─ install.ps1`
Adjust if another structure is more idiomatic.

Do not over-engineer.

Plugin

Create a normal BepInEx plugin.

Conceptually:

`[BepInPlugin(
    "com.planetaryanomalies.dsp",
    "Planetary Anomalies",
    "0.0.1")]
public class Plugin : BaseUnityPlugin
{
    private void Awake()
    {
        // initialize Harmony
        // register patches
        // log plugin startup
    }
}`
Expected log message:

`[Planetary Anomalies] v0.0.1 loaded`
Anomaly Model

For v0.0.1, keep the data structure deliberately tiny.

Something like:

`public sealed class PlanetAnomaly
{
    public int PlanetId { get; init; }
    public int RecipeId { get; init; }
    public int OutputMultiplier { get; init; }
}`
There is only one anomaly.

No generalized inheritance hierarchy.

No effect scripting system.

No JSON schema.

No database.

No configuration framework.

We can generalize later.

Selecting the Recipe

Once the home planet and recipe database are initialized, construct a list of eligible recipes.

Initially restrict eligibility to normal machine production types such as:

`Smelting
Assembling
Chemical production`
Do not initially include unusual production systems unless testing shows they use the same execution path.

Exclude recipes that are likely to complicate the proof-of-concept, such as:
 • fractionation,
 • research,
 • ray receivers,
 • mining,
 • orbital collectors,
 • proliferator-specific behavior,
 • special Dark Fog systems,
 • recipes with unusual execution machinery.
If unsure, begin even more narrowly:

> Assembler recipes only.
That is acceptable.

The goal is proof, not coverage.

Random Selection

For v0.0.1, ordinary random selection is acceptable.

Example conceptual code:

`eligibleRecipes[randomIndex]`
Log enough information to reproduce what was chosen manually:

`[Planetary Anomalies]
Home planet ID: 101
Home planet: Mediterranean
Recipe ID: 31
Recipe: Circuit Board
Effect: output ×2`
Persisting the random selection across save/load is *not required in the first spike*.

If save/load makes testing frustrating, use a deterministic choice temporarily.

For example:

`first eligible recipe whose ID hashes with planet ID`
Technical simplicity is more important than randomness for the first successful test.

Applying the Modifier

The central behavior should conceptually be:

`if (
    currentFactoryPlanetId == anomaly.PlanetId &&
    assembler.recipeId == anomaly.RecipeId
)
{
    producedOutput *= anomaly.OutputMultiplier;
}`
The implementation must alter the actual machine output.

It is not enough to:
 • change UI text,
 • alter statistics only,
 • change the recipe prototype,
 • simulate bonus items elsewhere,
 • add items directly to player inventory.
The assembler's output storage/buffer should contain the doubled result.

Inserters should then be able to remove those items normally.

Multi-Output Recipes

For v0.0.1, either:

Option A — preferred

Restrict random selection to recipes with exactly one output.

or:

Option B

Multiply all outputs.

Do not spend significant time solving multi-output semantics yet.

Proliferator

Do not attempt sophisticated proliferator integration in v0.0.1.

However, the mod must not crash when proliferator is used.

If DSP calculates proliferator bonus before the patched output stage, the anomaly should ideally multiply whatever result DSP would ordinarily produce.

Conceptually:

`normal DSP production result
× planetary anomaly`
rather than attempting to independently recreate DSP's proliferator formula.

If this is difficult, document the issue and exclude proliferated test cases from v0.0.1.

Production Statistics

Correct physical production is the priority.

If DSP production statistics report the original amount rather than doubled output, note this as a known issue.

Do not delay the proof-of-concept solely to fix production statistics.

Later versions should report actual anomalous production accurately.

UI

No custom UI for v0.0.1.

Use logs.

Optional: a temporary in-game notification is acceptable if trivial, but it is not required.

Do not build:
 • anomaly screens,
 • planet panels,
 • icons,
 • localization,
 • scanning UI,
 • discovery popups.
Discovery

Not part of v0.0.1.

The anomaly is known immediately through the log.

Later versions may make anomalies hidden until:

`planet scanned
OR
player lands
OR
affected recipe is executed`
But not now.

Save Persistence

Not required for the first proof-of-concept.

It is acceptable if restarting DSP causes the home planet to receive another random anomaly.

Once production modification works reliably, persistence will become the next milestone.

Build Workflow

Create PowerShell scripts so the mod can be built and installed without understanding the C# toolchain.

Desired workflow:

`.\scripts\build.ps1`
produces:

`PlanetaryAnomalies.dll`
Then:

`.\scripts\install.ps1`
copies the mod into the correct BepInEx plugin directory.

If practical, support:

`.\scripts\build.ps1 -Install`
Do not hardcode the user's Steam path if it can easily be detected or configured.

If the DSP installation path must be configured, put it in one obvious location.

Example:

`$DspPath = "C:\Program Files (x86)\Steam\steamapps\common\Dyson Sphere Program"`
Document it.

README

Write a README aimed at a developer who has never made a DSP mod.

Include:

Requirements

What must be installed.

DSP path

Where the game is normally installed.

BepInEx setup

Where BepInEx lives.

Build

Exact command.

Install

Exact command.

Run

How to start DSP and verify the plugin loaded.

Logs

Where to find the BepInEx log.

Testing

Exactly how to determine which recipe received the anomaly.

Test Procedure

A successful v0.0.1 test looks like this.

1. Start DSP

Verify log:

`Planetary Anomalies v0.0.1 loaded`
2. Start or load a suitable test game

The mod identifies the home planet.

3. Read the log

Example:

`ANOMALY FOUND
Planet: Mediterranean
Recipe: Circuit Board
Effect: Output ×2`
4. Build a machine for that recipe

Use a normal assembler/smelter as appropriate.

5. Supply exact ingredients for one production cycle

For example:

`2 iron
1 copper`
6. Allow one cycle to complete

Expected:

`4 circuit boards`
instead of:

`2 circuit boards`
7. Verify normal mechanics

An inserter should remove the resulting items normally.

The machine should continue running normally.

No duplication loop or deadlock should occur.

Secondary Planet Test

Once interplanetary travel is available—or with a suitable existing save—build the same recipe on another planet.

Expected:

`normal recipe output`
Only the anomalous planet should receive the ×2 modifier.

This is the critical acceptance test for planet-local behavior.

Acceptance Criteria

v0.0.1 is complete when all of the following are true:
 • DSP launches successfully with the mod installed.
 • BepInEx loads the plugin.
 • The plugin identifies the home planet.
 • Exactly one eligible recipe is selected.
 • The selected recipe and planet are written clearly to the log.
 • The selected recipe produces ×2 output on the home planet.
 • Inputs are consumed normally.
 • Inserters can move the anomalous output normally.
 • The recipe behaves normally on another planet.
 • The global `RecipeProto` is not permanently mutated.
 • No custom UI is required.
 • No anomaly persistence is required.
 • No scanning/discovery mechanics are required.
Important Engineering Principle

Do *not* begin by building the full anomaly framework.

The first unknown is:

> Can DSP production output be safely altered per planet?
Resolve that first.

If necessary, temporarily hard-code:

`Planet ID = home planet
Recipe ID = Circuit Board
Multiplier = 2`
That is a perfectly valid intermediate milestone.

The progression should be:

`1. Hard-coded recipe, hard-coded home planet
2. Hard-coded recipe, dynamically identified home planet
3. Random recipe, home planet
4. Persist anomaly
5. Deterministic galaxy-generated anomalies
6. Multiple anomaly types
7. Discovery mechanics
8. UI`
Do not skip directly to step 5.

After v0.0.1

Once the spike works, the intended next architecture is:

`Galaxy Seed
    ↓
Planet ID
    ↓
Deterministic anomaly roll
    ↓
optional PlanetAnomaly
    ↓
recipe ID
    ↓
recipe mutation`
Anomalies should ultimately be reproducible from something like:

`hash(galaxy seed, planet ID, anomaly-system version)`
That allows the same galaxy seed to produce the same anomalous planets.

The mod should eventually support sparse distribution.

Most planets should have no anomaly.

Some should have useful anomalies.

Very rarely, a planet should have an absurdly powerful anomaly.

Do not automatically compensate powerful anomalies with drawbacks.

The point is discovery, not balance.

Long-Term Design Principle

The intended player experience is:

`travel to system
↓
inspect planets
↓
discover anomaly
↓
realize a normal recipe behaves strangely here
↓
decide whether the anomaly is strategically useful
↓
possibly reorganize interstellar manufacturing around that planet`
The mod should create planets that players remember as things like:

`Gear World
Processor World
Graphene World
Motor World`
These identities should emerge from procedural generation rather than being predefined planet classes.

The finished mod should make the player think:

> “I wonder whether there is something weird in that system.”
rather than merely:

> “Does that system have the rare resource I need?”
But first:

*Make one recipe produce twice as much on the home planet.*
