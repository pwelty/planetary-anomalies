# Session log

Newest first. One entry per working session: what changed, what is proven, what is not, and
what the next session should pick up. Facts that outlive a session belong in
`docs/inspection.md`; this file is the narrative around them.

---

## 2026-08-27 — Stage 0 built, awaiting Paul's in-game test

**State: code complete and statically verified. Not yet witnessed in the running game.**

### Done

- **Inspected the installed game** rather than relying on public mods. DSP `0.10.34`,
  Steam build `23109513`, Unity `2022.3.62f3c1`. Read `Assembly-CSharp.dll` with the
  `Mono.Cecil.dll` that already ships inside the installed BepInEx, driven from PowerShell —
  no decompiler or SDK was installed. Full receipt, including hashes, IL excerpts and exact
  signatures, is in [`docs/inspection.md`](docs/inspection.md).
- **Found the production seam.** `AssemblerComponent.InternalUpdate` adds a completed cycle's
  output from `this.recipeExecuteData.productCounts`. That field is a per-component *reference*
  into the static, shared `RecipeProto.recipeExecuteData` dictionary — so editing the array in
  place would change the recipe galaxy-wide, which the project forbids.
- **Chose the seam accordingly: swap the reference, don't patch production.** Each anomalous
  machine gets a private `RecipeExecuteData` whose product counts are already ×10. Every array
  is copied, so nothing shared is touched.
- **Discovered a trap worth remembering:** `InternalUpdate` is called from *two* paths —
  `FactorySystem.GameTick` and `GameLogic._assembler_parallel` (the multithreaded one). A patch
  on either alone would be silently wrong on the other setting. The data-swap approach covers
  both, because neither path re-reads the shared dictionary.
- **Wrote the plugin** (`src/`): `Plugin.cs`, `Anomaly.cs`, `AnomalyManager.cs`, and a Harmony
  prefix on `FactorySystem.GameTick(long, bool)` — the one method that runs once per planet per
  tick, knows its own planet, and runs regardless of the multithreading setting.
- **Built it with zero installs.** No .NET SDK on this machine (`winget` has no user-scope
  installer for it), so `scripts/build.ps1` uses the in-box `csc.exe` from the .NET Framework,
  compiling against the game's own assemblies. That constrains us to **C# 5** syntax.
- **Added `scripts/verify.ps1`**, which re-resolves all 76 of the plugin's references against
  the currently installed assemblies and asserts the Harmony target signature still exists.
  It passes. This is the automated guard against a game update silently breaking the patch.
- **Installed** into the Gale profile `gs run`.

### Not done / not proven

- **No in-game test has been run.** Compilation and reference verification are not evidence
  that ten plates appear in a smelter. Stage 0 is not complete until Paul plays it.
- The off-planet negative case remains deferred, as `SPIKE.md` anticipated — the guard is live
  and logged, but early in a new game there is nowhere else to stand.
- The exact four-part game version is not yet recorded in `docs/inspection.md`; the plugin logs
  it at startup, so fill the table in from the first real run.

### Known rough edges, deliberately accepted for Stage 0

- The patch rescans the home planet's assembler pool every tick. Cheap (two integer compares
  per slot, no allocation) but not what a finished mod should do; a later stage should react to
  `SetRecipe`/`Import` instead.
- The recipe id is hard-coded to `1` and *verified at runtime* rather than trusted, because DSP
  keeps its proto database inside Unity assets and recipe ids cannot be read off disk. If the
  id turns out to be something else, the plugin falls back and says so in the log.
- `gs run` has 11 other mods, one of which (BlueprintTweaks) already fails to load on this
  build for reasons unrelated to us. A dedicated clean Gale profile would be a better test bed.

### Next session

1. Read the BepInEx log from Paul's run and record the real version + chosen recipe.
2. If ten plates appear: preserve the receipt per `SPIKE.md`'s stop condition, then Stage 1
   (random eligible single-output recipe) is a separate change.
3. If they do not: the first thing to check is whether the anomaly was ever attached — the log
   line `Anomaly attached to assembler #N` only prints when the swap actually happened.
