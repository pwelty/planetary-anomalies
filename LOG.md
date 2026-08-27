# Session log

Newest first. One entry per working session: what changed, what is proven, what is not, and
what the next session should pick up. Facts that outlive a session belong in
`docs/inspection.md`; this file is the narrative around them.

---

## 2026-08-27 — Stage 0 works in game

**State: Stage 0 confirmed by Paul in the running game. Ten plates per cycle on the home
planet.** The off-planet negative case is still deferred, as `SPIKE.md` always intended.

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

### The bug that cost us the first in-game run — worth not repeating

The first build hooked `FactorySystem.GameTick`. It loaded cleanly, logged nothing, and changed
nothing. Cause: `GameLogic.OnGameLogicFrame` dispatches most factory phases as a *pair* — a
sequential method and a `_Parallel` twin — chosen on thread count, and
`FactorySystemFacilityGameTick` (the only caller of `FactorySystem.GameTick`) runs **only when
threadCount <= 1**. Multithreading is the default, so the hook never fired.

The irony is that the inspection had already identified this exact trap one level down, for
`InternalUpdate`, and the design was built around it — then the *attachment hook* was chosen
without applying the same test. **Lesson: for any per-tick hook in DSP, check whether the method
has a `_Parallel` sibling before trusting it.**

Fix: hook `PlanetFactory.BeforeGameTick()` instead. `GameLogic.FactoryBeforeGameTick` has no
`_Parallel` twin, is gated only on "am I the main thread", and walks every factory — so it runs
in both modes, and earlier in the frame than the facility phase.

`scripts/verify.ps1` now asserts this specific property, so the mistake cannot recur silently.

### Confirmed in game

Home planet `Theta Phoenicis III` (id 103), galaxy seed 3664027, recipe `Iron Ingot` (id 1):

- Output slot rises in steps of **10** per cycle.
- Machine pauses at 95 and resumes at 90 — the vanilla cap
  (`produced[0] + productCounts[0] > 100`) scaling with the anomalous count instead of being
  bypassed. No deadlock, no duplication, no overflow.
- Inserters drain the anomalous output normally.
- Input consumption unchanged: 1 ore per cycle.
- Real game version captured: **`0.10.34.28529`**.

**Planet locality confirmed** on a copy of an established multi-planet save (seed 40078654,
home planet `Alrami III` id 103). The anomaly attached on the home planet while the guard left
four other planets — 52 machines running the same recipe — on vanilla output, including
`Alrami IV` in the *same star system*. Paul confirmed normal output in game there. That closes
the criterion `SPIKE.md` deferred, and incidentally proves the shared `RecipeExecuteData` was
never mutated: had it been, all 52 would have gone anomalous.

**Stage 0 acceptance is therefore complete**, with one non-blocking exception below.

### Still open

- **Production statistics** have not been read. `SPIKE.md` asks for them to be observed and
  recorded but explicitly does not let a mismatch block Stage 0. Expectation from the IL is that
  they *match* the anomalous amount, since `productRegister` is fed from the same `productCounts`
  as the output buffer. Worth a glance at the statistics panel next time a save is open.

### Discovery decided (recorded, not built)

Paul settled the discovery model this session, and it **supersedes `SPEC.md`'s** staged
hidden-until-triggered sketch:

> When the player learns about a planet — by landing or by scanning it remotely — they are told
> its anomaly. No separate hunt.

His reasoning: hidden-until-you-happen-to-build-it degenerates into searching the recipe ×
planet cross-product. The interesting decision is what to *do* about an anomalous planet, not
whether the player can be bothered to find it.

Description fidelity: **precise for now** ("Iron Ingot: 1 → 10"), with room to soften later to
"Improved iron ingot output" — naming what is affected without the number, so existence is free
but magnitude is still worth discovering.

Written up in `PRODUCT.md`. Two supporting facts already in hand:

- `docs/inspection.md` now records DSP's own scanning API (`GalaxyData.StartAutoScanning`,
  `UpdateScanningProcedure`, `Export/ImportScannedDatas`, `unscannedStarCount`). Scan state is
  persisted by the game, so "has the player learned about this planet" does not need inventing —
  though the exact semantics are unverified.
- Displaying an anomaly only becomes meaningful once it survives a restart, so **persistence
  (stage 3) should land before any discovery UI**, or the panel would show a different anomaly
  every launch.

Recorded, not designed. Building it now would be the "don't skip ahead" failure the docs guard
against — but it is no longer an open question, just unbuilt.

### Known rough edges, deliberately accepted for Stage 0

- The patch rescans the home planet's assembler pool every tick. Cheap (two integer compares
  per slot, no allocation) but not what a finished mod should do; a later stage should react to
  `SetRecipe`/`Import` instead.
- The recipe id is hard-coded to `1` and *verified at runtime* rather than trusted, because DSP
  keeps its proto database inside Unity assets and recipe ids cannot be read off disk. If the
  id turns out to be something else, the plugin falls back and says so in the log.
- `gs run` has other mods, one of which (`Common API Nebula Compatibility`) fails to load on this
  build for reasons unrelated to us. A dedicated clean Gale profile would be a better test bed.
- The plugin is copied into the Gale profile by `install.ps1` rather than installed through Gale,
  so **it does not appear in Gale's mod list** (Gale reads its own `data.sqlite3`). It still
  loads. A Gale repair/re-sync of the profile could remove it; re-run `install.ps1` if the
  startup lines vanish.
- The log line still prints `(build 0)`. `GameConfig.build` is not the build number —
  `gameVersion.ToFullString()` already carries `28529`. Cosmetic; drop the suffix.
- A large multiplier makes a machine spend most of its time stalled on its own output cap, so
  ×10 output is not ×10 throughput unless the buffer is drained fast. Relevant to later balance
  thinking, though `SPEC.md` explicitly does not want balance yet.

### Next session

1. Answer the two open observations above — production statistics, and input consumed per cycle.
2. First off-planet test once travel is available: same recipe, another planet, normal output.
   That closes the last Stage 0 acceptance item.
3. Then Stage 1 (random eligible single-output recipe on the home planet) is a separate change.
4. Consider replacing the per-tick pool sweep with a reaction to `SetRecipe`/`Import` before the
   anomaly count grows beyond one.
