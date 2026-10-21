# Session log

Newest first. One entry per working session: what changed, what is proven, what is not, and
what the next session should pick up. Facts that outlive a session belong in
`docs/inspection.md`; this file is the narrative around them.

---

## 2026-10-11 to 2026-10-20 — by-products, the lab limit, Dark Fog from play, the storm

The same play-and-tweak loop on seed 84201727 until Paul had exhausted the run, then the first feature
meant for a fresh one. Ruleset 3 only, every behaviour a config setting, changelog in the same commit.

**What changed.**

- `MultiOutputRecipes` (`6f813bd`, on): recipes with a by-product join the pool; every product is
  multiplied, and the planet panel lists each on its own line.
- Labs on cube worlds can start (`3cc78ab`). Paul's Green Matrix lab on Sadira I would not run: a lab
  holds at most 10 finished matrices, and a x10 craft of a two-matrix recipe overflows that before it
  begins. `LabOutputCapPatch`, a transpiler -- the limit is a constant mid-method, so no safer seam --
  gives anomalous labs room for two batches. The seam is in `docs/inspection.md`.
- Dark Fog, from Paul's play (`a050a9d` to `09bd366`, roadmap only): the base-level ladder (Matrix
  and the Lens tech at 12, a second hidden tech at 15, Core Element at 24, a farmed base reaching 30),
  the Lens research costing 1,000 matrices, and each drop's x10 world in that galaxy. This is
  evidence for the Dark Fog disclosure candidate, not a decision to build it.
- Downloads as of 19 Oct (`6a3bfa1`): 695, 0.5.1 at 199, and a correction -- superseded versions keep
  gaining about half a download a day, so 0.5.0 did not "stop dead".
- **The storm** (`b535282`, setting `Storm`, on). Between minutes 10 and 20 of game time (the minute
  from the seed), the nearest anomalous non-home world whose recipe needs no research lends its anomaly
  to home for 30 minutes. It is announced on arrival and when it passes, with a STORM line and minutes
  left on home's planet panel and STORM xN in the machine window. The clock is `GameMain.gameTick`,
  restored from the save, so nothing is stored. It is the only anomaly that is taken away again, so
  its sweep alone restores machines to the shared execute data.
- The game updated itself on 15 Oct to 0.10.35.29151. Build and verify pass against it; hash and the
  clock facts are in `docs/inspection.md`.

**What is actually proven.**

- In game, by Paul, informally: by-product worlds "I THINK IT'S working"; the Dark Fog ladder and
  drops as listed.
- Statically: a new game's clock starts at 0. `timei` is an instance field with no initializer on a
  per-game component, written only by its own tick and by the save import (Cecil, 20 Oct).
- Build and verify.ps1 pass; the golden test's 202 results are unchanged.

**What is not.**

- ~~The storm in game~~ -- run on 20 Oct, new game, seed 12261908: "Storm: from DenebKaitos I (Gear
  x10), reaching γ Ursae Majoris III at minute 11 ... This save is at minute 0". Paul saw the arrival
  announcement, STORM x10 in the assembler window, the STORM line in the planet description, and the
  end announcement. The log shows three assemblers switched to x10 as he built them and all three
  "back to normal" when it passed. Paul's verdict: "it was cool." A reload after it (a save at
  minute 43) logged "it has already passed" and announced nothing. Two additions made during that
  run are installed but unseen, since they need a storm in progress: the replicator following the
  storm (`5d550a8`) and a storm line under the corner globe (`010a8be`), added after Paul asked
  whether it could be seen without opening the planet panel. A reload *during* a storm is unseen too.
- ~~The lab limit fix in play~~ -- confirmed by Paul on 20 Oct: the Green Matrix lab on Sadira I
  "was fine" after the fix.
- Which tech appeared at Dark Fog level 15.

**Where the next session starts.** The storm is tested (above). On the next new game -- seed 12261908
brings Gear at minute 11 -- check the corner-globe line, the replicator during the storm, and a reload
mid-storm; the load log's "This save is at minute M" says whether a save falls inside it. Paul
expected a second storm; that is a roadmap candidate (*more than one storm*), not a decision. Beyond
that, the remaining 1.0 list is Paul's to choose from (ROADMAP, "1.0 in development").

---

## 2026-10-10 — the lab window marker; first cube world reached

Paul reached Graffias IV, this galaxy's Universe Matrix world (seed 84201727), and set up a lab.

**What changed.** `UILabWindowPatch` (`src/Patches/LabWindowPatch.cs`): a postfix on
`UILabWindow._OnUpdate` appends `ANOMALY ×10` to `stateText` for a produce-mode lab making the
planet's anomalous recipe. It mirrors the assembler marker. The difference is that `factory` and
`factorySystem` are private on the lab window, so they come in by Harmony field injection.
Registered as its own display surface ("the lab window"). verify.ps1 checks the target and the
fields it uses. Signatures are in `docs/inspection.md` under "Lab window", read against 0.10.35.29104.

**What is actually proven.**

- From the game log: the production sweep attached the anomaly to one lab on Graffias IV ("Anomaly
  attached to 1 machine on Graffias IV ... output is now x10"). The replicator showed ×10 there too.
- In game, by Paul, informally: the lab's matrices are "coming out pretty fast". That is consistent
  with ×10, but nobody counted, so it does not yet prove a lab makes ten per cycle.
- Build and verify.ps1 pass.

**What is not.**

- The lab marker has not been seen in game. It needs a game restart to load.
- A counted ×10 from a lab: watch the output count step by 10, or compare Universe Matrix made
  against Antimatter used in the planet's production stats.
- Whether production statistics count lab output at ×10, and whether the lab's output cap behaves
  well at ×10. Neither is inspected for labs.

**Where the next session starts.** Ask Paul whether the lab window shows the marker after a restart
and whether the count steps by 10. Then carry on with the ruleset 3 list from the entry below.

---

## 2026-09-24 to 2026-09-28 — ruleset 3 begins: the early game, uniqueness, the replicator, cubes

Development of 1.0, played and tuned in a loop: Paul played a fresh run (seed 84201727, ruleset 3,
AnomalyChancePercent 75), reported, reloaded; each change was committed, pushed and installed between
reloads, with its changelog line in the same commit. All of it is ruleset 3 only unless noted, and every
new behaviour is a config setting.

**What changed** (commits `c9e09b1` to `db3180c`).

- *Ruleset 3 exists* (`c9e09b1`): generator version 2, recipe list follows the game, the default for a
  fresh install; Latest means 3. `AnomalousHome` (home always carries an early recipe) and
  `NearbyFavorsEarlyRecipes` (stars within 6 ly of home -- 2,400,000 units per ly, as the star map uses
  -- draw early recipes at x8). "Early" is read from the game: no unlocking tech, or a tech costing only
  the first matrix and plain items; never a hidden tech. The weighted draw is integer-only rendezvous (a
  recipe of weight w enters w times) because Math.Log is not guaranteed to round alike everywhere.
- *Round 2* (`b749b43`): the first matrix was sought as the one made without other matrices -- wrong,
  every matrix below Universe is made from ordinary items -- so the fallback left 17 early recipes and no
  blue tier. Now it is the matrix whose own research needs no matrix: 52 early. `NoDuplicates`: no recipe
  twice in a system or among the systems within 6 ly of home, decided in planet-id order within the
  group. `ReplicatorFollowsPlanet`: postfix on `MechaForge.AddTaskIterate` multiplies the new job's
  `productCounts` when Icarus stands on a world with that anomaly. The survey log's Marker label.
- `NoRepeatsInGalaxy` (`758bed6`, off by default): every recipe once in the galaxy, handed out nearest
  first in one ordered pass; once the pool is spent the furthest worlds stay ordinary.
- `HomeSkipCombat` (`027e3cb`): the early tier includes the first combat techs, and the home planet came
  up Battlefield Analysis Base. Combat is read from the game's fields (item type Turret/Defense, ammo and
  bomb types, fighter ids, turret/battle-base/beacon/field-generator/combat-module buildings).
- The replicator queue showed "1" while delivering ten (`0325b83`): `UIReplicatorWindow.ActiveQueueText`
  multiplies crafts by the *global* recipe's output. A postfix rewrites the number from the job's own
  `productCounts`, binding the private `taskQueue` and `queueNumTexts` by name.
- `ResearchCubes` (`db3180c`, flat x10 for now): matrix recipes join the pool. Labs in produce mode run
  the same per-machine `RecipeExecuteData` as assemblers (`LabComponent.InternalUpdateAssemble` reads the
  same fields), so the production sweep now swaps it on labs too.

**What is actually proven.**

- In game, by Paul: a *real* research announcement, naming planets, drawn on screen and seen ("yeah 2
  wind turbines on 1 system") -- the item still open from the previous entry; the replicator delivering
  ten engines on Castor III.
- From the log of the run, per round: the nearby early share went 50% with 17 early recipes (the
  arithmetic for that list: 17x8 against 134) to 75% with 52; no repeats within any system or near home
  under `NoDuplicates`; 151 recipes on 151 worlds, 7 far worlds ordinary, under `NoRepeatsInGalaxy`; with
  cubes, 157 recipes on 157 worlds, 1 ordinary, all six matrices placed (none within 6 ly), and no recipe
  missing its execute data. Home: Magnetic Coil, then Battlefield Analysis Base, then Engine.
- Offline, with the mod's own generator: the weighted draw gives 66.3% against 66.7% expected over 13,000
  draws, deterministically. verify.ps1 passes against 0.10.35, the golden test's 201 original results
  unchanged; new checks cover the replicator, its queue and the lab seam.

**What is not.**

- ~~The replicator queue now reading ten~~ -- confirmed by Paul in play after the power-cut restart
  ("yes that works").
- A lab producing a matrix at x10: every cube world in this galaxy is far from home.
- Ruleset 3's draw is not locked: it changes whenever a setting or the pool does, by design until 1.0
  ships, when its golden output is recorded.
- Defaults for release are undecided -- `ReplicatorFollowsPlanet` is on "for now", against the roadmap's
  earlier argument for off; whether the x8 lean and 75% nearby share are right; whether the home draw
  should skip cubes as well as combat.
- The lab window has no anomaly marker. The README says nothing about any of this yet; that is the
  release docs pass.

**Where the next session starts.** Keep the play-and-tweak loop going on seed 84201727 if Paul wants;
otherwise the next ruleset 3 candidates by his list: varied multipliers (which cubes are waiting on),
by-product recipes, then the idea-list items. Check the replicator queue reads ten on the next load.

---

## 2026-09-19 to 2026-09-24 — 0.5.0 and 0.5.1 released; ruleset 1 fixed; 1.0 ideas

**A gap first.** No entries were written between 27 Aug and this one, across 0.2 to 0.5. That history
is in `packaging/CHANGELOG.md` (what shipped), `ROADMAP.md` (why, and what was learned) and the git
log; it is not reconstructed here. AGENTS.md asks for an entry per session, and this resumes it.

**What changed.**

- *0.5.0 finished and released* (22 Sep, tag `v0.5.0`). Last fixes before release: the item tooltip in
  the replicator grid (the grid passes a recipe id *negated* as `itemId`); the tooltip line drawn in the
  game's own text colour with only "Anomaly:" in gold; a read-through that found research state kept
  per session rather than per game, menu-demo research leaking into the first real game, one broken
  patch able to take every later patch with it (each surface now patches on its own), a tooltip
  dictionary that leaked destroyed tooltips, and "ten to one" hard-coded in the Marker line.
- *Research announcements were invisible, and had been since 0.5 began.* Two causes, both found by
  reading `UIRealtimeTip.Update`: a tip's width is `sqrt(clamp01(0.2 + 7 x (1 - lifeTime)))`, so
  lengthening a tip by raising `lifeTime` made it zero-width for most of its life; and
  `InvokeRealtimeTip` multiplies its drift argument by 40, so 6 meant 240 px/s. Now `lifeTime` is pinned
  at 0.8 while the announcement is read, drift is 0, and the landing position is logged in screen
  pixels. verify.ps1 asserts the width formula.
- *0.5.1 released* (23 Sep, tag `v0.5.1`), for Dyson Sphere Program 0.10.35, which added Dark Fog Lens
  (recipe 162). Under 0.5.0 it took one of 127 anomalous planets in Paul's galaxy (Zavijava IV,
  Crystal Shell Set until then). Ruleset 1 now draws only from a fixed list of 150 recipe ids
  (`AnomalyMath.RulesetOnePool`, locked by the golden test); ruleset 2, opt-in, is the same arithmetic
  on the game's full pool; 1.0's rules become ruleset 3. The pool is logged by id on every load.
- *1.0.0 started* as a version bump only. No 1.0 development yet, at Paul's instruction.
- *Roadmap:* 0.5 and 0.5.1 retrospectives; a new first principle, "everything circles around
  recipes"; and a run of 1.0 candidates, recorded as ideas, not decisions -- the monolith (and a
  supervein, marked off-concept), four suggestions (scope, priced multipliers, planet types, upgrade
  notice), Dark Fog disclosure, Krell worlds and their natural-cause version, debuffs that bite, linked
  anomalies, finite anomalies with tranches, chained anomalies. The seed searcher and galaxy page moved
  onto the README's 1.0 list.

**What is actually proven.**

- In game, by Paul: announcements are seen (test tip on screen, geometry logged inside the screen at
  full scale and opacity); the tooltip line shows in the replicator grid; under 0.5.1 on game 0.10.35,
  Zavijava IV is Crystal Shell Set again and the log names Dark Fog Lens as not drawn.
- Offline, with the mod's own generator: ruleset 1's list reproduces all 148 anomalous planets in the
  Anomaly Catalogue for seed 30853643 (built on 0.10.34, 3 Sep) and all 127 in Paul's galaxy. That is
  the evidence the recovered list matches 0.10.34's pool; the baseline could not be taken live because
  Steam updated the game a minute before launch.
- verify.ps1: 68 checks, including the golden test (201 results), pass against 0.10.35.

**What is not.**

- A *real* research announcement -- one naming a planet -- has not been seen since the display fix;
  only the test tip has. Same code path, not the same proof.
- Ruleset 2 has not been run in game. It is the 0.5.0 behaviour, so low risk, but unobserved.
- The Dark Fog facts in the roadmap (hidden techs, level-gated ground drops, base levels, settings
  fixed at creation) come from the game's code; the per-item drop levels and which item reveals Dark
  Fog Lens come from its data and have not been read. Player reports of levels 12/15/18/21/24 are
  unconfirmed.
- The survey log labels Marker-mode anomalies `HIDDEN`. Known, log only, not fixed.

**Where the next session starts.** 1.0 is Paul's to scope. The two decisions that shape it: which
candidates ruleset 3 carries, and whether the mod starts writing to saves (finite and chained anomalies
both need it). Nothing is built ahead of that. Downloads as of 24 Sep: 462 in total; 0.5.1 at 45 in its
first day.

---

## 2026-08-27 (later) — anomalies derived across the galaxy from the seed

**Released as v0.1.0 on Thunderstore:**
https://thunderstore.io/c/dyson-sphere-program/p/pwelty/PlanetaryAnomalies/ --
categories Assembling Machines, Logistics, Resources. Icon is still the generated placeholder.

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
- ~~Machine marker not yet witnessed.~~ **Confirmed.** The assembler window shows the marker on a
  machine running its planet's anomalous recipe, so the panel no longer contradicts the machine.

- **Determinism is only as stable as the eligible recipe list.** Re-running without saving gives
  the identical galaxy, which is the point. But the recipe is chosen by *indexing into the sorted
  list of eligible recipes*, so if that list gains or loses an entry, every planet's recipe shifts.

  A DSP update could do that. So could another mod: `LDBTool` and `CommonAPI` are installed in the
  `gs run` profile and exist precisely to add protos. Installing a recipe-adding mod would
  silently reshuffle an existing galaxy's anomalies.

  Worth fixing before anyone else plays it. Selecting by something intrinsic to the recipe --
  hashing its proto id rather than its position -- would make an added recipe affect only the
  planets that newly select it, instead of shifting everything. Not urgent while the only player
  is Paul, but it undermines the "same galaxy forever" promise the design rests on.

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
5. ~~Coach Paul through uploading the mod.~~ **Done** -- published 2026-08-27 as
   https://thunderstore.io/c/dyson-sphere-program/p/pwelty/PlanetaryAnomalies/ (v0.1.0).
   Original note: He has not published a DSP mod before, so this needs
   to be explicit: what Thunderstore expects (manifest, icon, README, version), how the package is
   assembled, what the DSP community conventions are, and what to check before a first release.
   `AGENTS.md` requires explicit approval before any publish, so this is preparation, not release.
6. ~~Write the mod description for him~~ **Done** -- `packaging/README.md` is the listing page.
   Original note: -- the Thunderstore listing and README aimed at players
   rather than developers: what the mod does, what it feels like to play with, the config options,
   and honest notes on what it does not do yet.

