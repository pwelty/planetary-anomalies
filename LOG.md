# Session log

Newest first. One entry per working session: what changed, what is proven, what is not, and
what the next session should pick up. Facts that outlive a session belong in
`docs/inspection.md`; this file is the narrative around them.

---

## 2026-08-27 (later) — anomalies derived across the galaxy from the seed

**State: confirmed in game, end to end.**

Replaced the single hard-coded home-planet anomaly with per-planet generation from
`hash(galaxy seed, planet id, anomaly-system version)`. Effect stays at output ×10; effect
variety is a later step, one new variable at a time.

Stages 2 and 3 as written in `PRODUCT.md` are now dead: stage 2 was "random recipe on the home
planet", which contradicts home planets never being anomalous, and stage 3 (persist) is dissolved
by determinism. This is effectively stage 4.

### Decisions taken this session

- **Density is itself drawn from the seed, 25-75%**, rather than being a fixed rate. Paul's idea,
  and better than a constant: galaxies differ from one another, not just planets within a galaxy.
  Some are anomaly-rich, some sparse, and that is one more thing a seed means.
- **Most planets are anomalous**, superseding SPEC's "sparse distribution". Paul's reasoning: rare
  anomalies are rarely worth reorganising production around, so the mechanic would barely touch
  how the game is played. Common ones make most worlds a candidate for specialising something,
  which is what makes exploration and interstellar logistics matter.
- **Config overrides for playtesting**, not for design. `AnomalyChancePercent` (-1 derives from
  the seed; 0-100 forces a density) and `OutputMultiplier`. BepInEx writes them to
  `BepInEx/config/com.planetaryanomalies.dsp.cfg`; edit and relaunch rather than rebuilding.

### Verified before running in game

The generator was checked by compiling the same hash with the same compiler as the plugin, so
`unchecked` arithmetic matched exactly:

- Density spans 25-75 with mean 50.0 over 200k seeds.
- Adjacent planet ids are uncorrelated -- the main risk, since planets in one system have
  consecutive ids and a weak mix would make a whole system share a verdict.
- 64.33% actual against a 65% target over 4000 planets.
- All 120 recipe slots used; presence does not predict recipe (mean index 59.49 anomalous vs
  59.42 overall, expected 59.5), so the two salts are genuinely independent.

That produced a falsifiable prediction for the late-game save before the code had ever run.

### Confirmed in game

Two independently written implementations agreed on density for both saves: seed 22135963 gave
58%, seed 40078654 gave 65%, matching the offline harness exactly. 147 eligible recipes.

Every prediction held:

~~~
Zeta Piscium I   (1201)  predicted ANOMALOUS   -> Sorter Mk.III 2 -> 20
Theta Scorpii VI (5406)  predicted ANOMALOUS   -> Mini Fusion Power Plant 1 -> 10
73 Velorum IV    (1704)  predicted no anomaly  -> No anomaly
Alrami IV        (104)   predicted no anomaly  -> No anomaly
Alrami III       (103)   home                  -> No anomaly (home planet)
~~~

Observed rate across the save: 11 anomalous of 17 planets with factories = 64.7%, against the
65% target. Paul confirmed the planet panel shows the correct anomaly on Theta Scorpii VI.

Multi-count outputs are handled: `Conveyor Belt Mk.II 3 -> 30`, `Sorter Mk.III 2 -> 20`, not
flattened to 10.

### The finding that matters most: anomalies are latent

**Zero machines were attached.** Not a bug -- the anomalies landed on recipes those planets do not
currently build. Under the old build the anomaly was always iron ingot on the home planet, where
machines obviously existed; now it is a random eligible recipe, so an anomaly only does anything
if the player chooses to build that recipe there.

That is the intended loop -- learn a world is good at something, then decide whether to move
production -- but it changes what the mod feels like: the effect is invisible until acted upon,
and the planet panel is the only thing making it actionable. It also means the disclosure work
was not a nice-to-have; without it, most anomalies would be undiscoverable in practice.

### Still open

- ~~Attachment under the new build has not been witnessed.~~ **Confirmed.** Paul built an
  assembler for Titanium Crystal on `Alrami I`, that planet's own anomalous recipe. The swap fired
  as soon as the machine was placed -- `Anomaly attached to 1 machine on Alrami I (planet id 101)`
  -- and the output slot took 10 Titanium Crystal per cycle instead of 1.

  The chain is now witnessed end to end: seed -> per-galaxy density -> per-planet anomaly ->
  recipe selection -> disclosure in the planet panel -> the swap onto a real machine -> anomalous
  output in game.
- **The machine's own panel does not say anything is different.** Paul's observation, standing at
  the anomalous assembler: the recipe shown is the normal one -- 1 Organic Crystal + 3 Titanium
  Ingot -> 1 Titanium Crystal -- while the machine visibly produces 10. This is worse than the
  `M` view omission recorded in the previous session. There the information was merely absent;
  here the UI contradicts what the machine is doing, right where the player is looking.

  It follows directly from the design: we never touch `RecipeProto`, and the machine panel reads
  the prototype, so it necessarily shows vanilla numbers. Correct mechanism, misleading display.

  A surface exists. `UIAssemblerWindow` has public `titleText` and `stateText`, refreshed in
  `_OnUpdate()`, so the same append-to-text-the-game-already-draws approach used for the planet
  panel would apply. Not built yet; it is a decision about how loud the machine should be, not a
  technical problem.
- **Balance.** Some anomalies are enormous: Mini Fusion Power Plant ×10, Annihilation Constraint
  Sphere ×10. SPEC says explicitly not to balance, but at 65% density with a flat ×10, late-game
  recipes may swing harder than intended. The config overrides exist for exactly this.
- **Production statistics** still unread, from the previous session.

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
- **What actually needs saving is narrower than it first looks.** Stage 0's anomaly is
  hard-coded, so it is identical on every launch and nothing needs recording; a save touched by
  the mod today is unchanged by it. The problem appears at stage 2, where a *random* pick would
  differ every launch. But stage 4 — deriving anomalies deterministically from
  `hash(galaxy seed, planet id, anomaly-system version)` — dissolves it again: the anomaly is
  reproducible from data the save already contains, so the anomalies themselves never need
  storing. Persistence as a stage may largely evaporate if stage 4 lands before any UI does.

  What genuinely must be recorded even then is the **anomaly-system version**. If the generator
  ever changes and a galaxy is not pinned to the version it was generated under, every existing
  save silently re-rolls: someone's Graphene World quietly becomes something else. That is the
  one thing that has to survive in the save, and it is cheap — a single integer. `SPEC.md`
  already anticipates it by putting the version inside the hash.

  Discovery state may not need storing either, since DSP persists its own scan data — unverified.

  Practical note: `DSPModSave` is already installed in the `gs run` profile and is the
  conventional way to attach mod data to a DSP save. `AGENTS.md` bars CommonAPI, but that
  constraint is explicitly scoped to Stage 0.

Recorded, not designed. Building it now would be the "don't skip ahead" failure the docs guard
against — but it is no longer an open question, just unbuilt.

### Anomaly disclosed in the planet panel (confirmed in game)

Implements the discovery rule decided above. A postfix on `UIPlanetDetail.OnPlanetDataSet`
appends to `planetBrief`, gated on `PlanetData.scanned`.

Confirmed by Paul: **galaxy view -> click the planet -> description tab** shows the anomaly.

Two things the inspection caught that would otherwise have been bugs:

- The game sizes the brief's container from `Text.preferredHeight` *immediately after* setting
  the text, so that measurement predates the appended line. The postfix redoes the same
  computation, or the added text would render into a box too short for it.
- The game rewrites `planetBrief` from scratch on every call — it even re-picks the flavour text
  with `Random.Range` — so appending cannot accumulate across refreshes.

Using DSP's own `PlanetData.scanned` as the gate meant "landed or scanned remotely" needed no new
machinery: the game sets it, persists it, and `UIPlanetDetail` already re-runs `OnPlanetDataSet`
when it flips. Note it is set *on demand* — `OnPlanetDataSet` calls `RunScanThread()` for an
unscanned planet — so opening the panel may itself trigger the scan. Whether that is the right
discovery feel is a play question, not a code one.

**Not shown in the `M` view, and that is correct.** Standing on a planet, the anomaly is not visible without going out to the galaxy view. But neither is the planet description, nor the memo field: DSP keeps all of that behind `V`. Putting the anomaly with the description therefore follows the game's own information architecture rather than cutting across it, which is a better outcome than surfacing it in a view the game deliberately keeps sparse.

If that ever needs revisiting, the cheap option found while looking is `UIPlanetGlobe.geoInfoText`, the local planet info text in the `M` view. It is the same kind of surface as `planetBrief`, so the same append-and-remeasure approach would apply. Recorded so it need not be rediscovered.

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

1. **Production statistics** — the one unobserved Stage 0 item. Non-blocking.
2. **Decide the stage order: 2 then 3, or straight to 4.** Deriving anomalies from
   `hash(galaxy seed, planet id, anomaly-system version)` makes them stable *and* removes the need
   for a save subsystem, leaving only the version integer to persist. Going straight there may be
   cleaner than doing random-then-persist first.
3. **Stage 2 is independently testable before that decision.** Per-launch variation only harms a
   *player* across sessions; inside one test session it is harmless. And the planet panel now
   doubles as the test instrument: read the randomly chosen recipe off the planet's description
   tab, then go check a machine running it. That verifies the way a player would, rather than by
   reading the log.

   Constraint Paul noted: this needs a **late-game save**, since the star map (`V`) is not
   available early, and the panel is only reachable through it. Paul has one: the developed
   multi-planet save already used for the planet-locality test (galaxy seed 40078654, home
   planet `Alrami III` id 103).

   That save is already a known-good test bed, and the guard log from this session says exactly
   where. These non-home planets run the iron ingot recipe:

   ~~~
   Alrami IV          id 104    39 machines   (same system as home)
   Theta Scorpii VI   id 5406    7 machines
   Zeta Piscium I     id 1201    4 machines
   73 Velorum IV      id 1704    2 machines
   ~~~

   Once "home planets never have anomalies" lands, testing moves onto these. `Alrami IV` is the
   obvious first target: same star system as home, so it is quick to reach, and with 39 machines
   an anomaly there is impossible to miss.
4. Replace the per-tick pool sweep with a reaction to `SetRecipe`/`Import` before the anomaly count
   grows beyond one.

