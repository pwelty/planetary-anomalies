# Changelog

## 1.0.0 (in development)

- Ruleset 3 begins, with the early game. The home planet always carries an anomaly drawn from
  recipes you can build early (`AnomalousHome`), and worlds within 6 light years of home lean
  toward those recipes (`NearbyFavorsEarlyRecipes`). Both on by default. Ruleset 3 is the default
  for a fresh install; an existing config keeps its ruleset.
- `HomeSkipCombat` (ruleset 3, on by default): the home planet's anomaly is never a weapon,
  ammunition or defensive building. Other worlds are unaffected.
- `NoDuplicates` (ruleset 3, on by default): no recipe twice in one star system, and none twice
  among the systems within 6 light years of home.
- `NoRepeatsInGalaxy` (ruleset 3, off by default): every recipe at most once in the whole galaxy,
  handed out nearest first. With about 150 recipes, a dense galaxy runs out; the furthest worlds
  then stay ordinary, and the log says how many.
- `ResearchCubes` (ruleset 3, on by default): matrices can be anomalies, made ten at a time by the
  labs on that world. A flat x10 for now; varied multipliers are still planned for the rarer cubes.
  Labs keep at most 10 finished matrices before they stop, which a x10 craft overflows -- a
  Gravity Matrix world never started, and single-matrix worlds ran a batch at a time. Anomalous
  labs now have room for two batches; ordinary labs are unchanged.
  The lab window says ANOMALY ×10 on a lab making that world's matrix, as the assembler window
  already does for machines.
- `MultiOutputRecipes` (ruleset 3, on by default): recipes with more than one output -- plasma
  refining, X-ray cracking, graphene from fire ice, antimatter -- can be anomalies. Every output
  is multiplied, by-product included, and the planet panel lists each product on its own line.
- The storm (`Storm`, ruleset 3, on by default): between 10 and 20 minutes into a new game, an
  anomalous storm from the nearest world whose anomaly needs no research reaches your home planet.
  For 30 minutes your machines there make that recipe at its multiple; then it passes, and you are
  told which world has it for good. Announced at both ends, and shown on the home planet's panel, in
  the machine window, and under the small globe in the top-right corner, with the minutes left, while
  it lasts. With `ReplicatorFollowsPlanet` on, the replicator at home makes it at the same multiple.
  Nothing is saved; it follows the save's own clock, so a reload mid-storm finds it still blowing.
- Varied multipliers, two settings, both off by default so the flat x10 stays unless you choose
  otherwise. `MultiplierFromDanger`: a world in a star system that started with Dark Fog hives makes
  more -- x20 with one hive, x30 with two, x40 with three or more. `MultiplierFromDistance`: the
  farther from home, the more -- the nearest third of star systems x10, the middle third x15, the
  farthest third x20. Together they multiply, so a far system thick with hives reaches x80. The home
  planet keeps x10. The planet panel says why each number is what it is, and the star map labels,
  tooltips, announcements and the storm use each world's own number. Neither moves any planet or
  changes any recipe, so either can be switched in a running game, on any ruleset.
  `MultiplierFromDifficulty` (off by default) scales the base by the galaxy's Dark Fog difficulty --
  the score the game shows at creation, against 1.0 for the default settings -- from x3 to x30, with
  peace mode at x3. Change the Dark Fog's aggressiveness mid-game and the multiple follows within a few
  seconds, with an announcement. It applies everywhere, home included,
  and supersedes the experimental `MultiplierFromCombatSettings`.
  Each has a dial under *Multiplier tuning* -- `DifficultyStrength`, `DangerStrength`,
  `DistanceStrength`, 1 by default -- to turn its effect up or down in play: 1.5 half as strong again,
  0.5 half as strong, 0 none.
- `ExcludeBuildings` (off by default, any ruleset): no anomaly on a recipe that makes a building --
  power, belts, sorters, storage, logistics, miners, factories, labs, turrets. Asked for on Reddit, where
  players had been pasting every building's name into `ExcludedRecipes`. Read from the game, so new
  buildings from updates or other mods are covered too. Only the planets that carried a building
  recipe change.
- `ReplicatorFollowsPlanet` (on by default, for now): standing on an anomalous world, Icarus's
  replicator makes that world's anomalous recipe at the same multiple its machines do, and the
  replicator queue shows the multiplied count.
- Names from other mods' recipes no longer show in Chinese. A recipe added by another mod -- More Mega
  Structures' star cannon parts, reported by a player -- could appear on star map labels, the planet
  panel and announcements as its untranslated Chinese name in an English game. Names are now
  translated as they are shown, falling back to the item's name when the recipe has no translation.
- The survey log (`LogEveryAnomaly`) now says MARKER for an anomaly Marker mode is showing, instead
  of HIDDEN.

## 0.5.2

**Recipe names from other mods now show in your language.** With a mod that adds recipes -- More
Mega Structures' star cannon parts, reported by a player -- an anomaly on one of them could be labelled
with its untranslated Chinese name in an English game. Nothing else changes: every planet keeps the
anomaly it had.

- Names are translated when they are shown rather than read from the game's cache, which another mod's
  recipe can leave holding its Chinese key. A recipe with no translation at all falls back to its
  item's name. Star map labels, the planet panel, tooltips and announcements all use the same names.

## 0.5.1

**Handles Dark Fog Lens, the recipe Dyson Sphere Program 0.10.35 just added.** By default it stays
out of your galaxy, so no planet moves on its account; set `AnomalyRules = 2` if you want it in.
Nothing else changes.

- Your galaxy stays exactly as it was through Dyson Sphere Program 0.10.35. The game update adds a
  recipe, Dark Fog Lens, and under 0.5.0 it took about one anomalous planet in 150 -- one of 127 in
  the first galaxy checked, where a Crystal Shell Set world became a Dark Fog Lens one. 0.5.1 puts
  them back.

  The fix is to say what ruleset 1 is. `AnomalyRules = 1` -- the pre-1.0 ruleset, and still the
  default -- now fixes the recipe list as well as the rules: it draws from the same 150 recipes it
  always has, and nothing a game update or another mod adds can move one of its planets. The log
  names any recipe it leaves out.
- New: **ruleset 2**, for players who want the new recipes. `AnomalyRules = 2` (or `Latest`) is
  ruleset 1 drawing from every recipe the game has -- Dark Fog Lens now, and whatever arrives next.
  Switching moves only the few planets those recipes take; density and which planets are anomalous
  stay exactly as they are. It is opt-in because a later game update can move a planet or two
  again, which is the thing ruleset 1 exists to prevent. It is also exactly what 0.5.0 did.
- Coming in 1.0: **ruleset 3**, with more options. That is where the rest of the list lands --
  research cubes, recipes with a by-product, varied multipliers, no duplicate anomalies within a
  system -- none of which rulesets 1 and 2 will ever draw. Opt-in for existing installs, like
  ruleset 2.
- If you run a mod that adds recipes: those are drawn under ruleset 2, and no longer under ruleset
  1, so a ruleset-1 planet that carried one moves once, back to a recipe from the list.
- Checked against Dyson Sphere Program 0.10.35: every hook the mod uses is where it was. The log
  now lists the recipe pool by id on every load, for bug reports.

## 0.5.0

- Hover an item -- in the replicator, your inventory, the build menu, anywhere -- and its tooltip
  now names the worlds you know of that make it ten times over: *Anomaly: Copper Ingot ×10 on
  BatenKaitos I*. One line per recipe where an item has several. Same rules as the star map:
  worlds you have scanned, recipes you have researched, and unresearched ones as
  `UnresearchedAnomalies` allows. It is what the star map already knows, sorted by the thing you
  are holding instead of by where it is. In the replicator's recipe grid, where you hover a
  recipe rather than an item, the line is for that recipe alone.
- When you finish a technology, the mod says whether a world you have already scanned makes one of
  its recipes ten times over. Research Antimatter Capsule and it tells you Iclarkrav I has been
  making them ×10 the whole time. Shown in gold just under the game's own "Research complete"
  notice for about six seconds, and written to the log. `AnnounceOnResearch = false` turns it
  off, and `TestAnnouncement = true` shows one about eight seconds after a save loads -- long
  enough to be looking at the screen -- so you can check they work on your setup.

  This is the other half of hiding unresearched anomalies. On its own, hiding is a subtraction --
  information you had in 0.3 and lost in 0.4. With this, the mod stays quiet while a recipe means
  nothing to you and speaks the moment it starts to, which is a better moment than discovery
  because it is when you can act on it. Only planets you have already found are named; it will not
  point at worlds you have not visited. If none of your scanned worlds has it but the galaxy does,
  it says only that -- that it exists somewhere you have not been, without saying where. Knowing
  something is out there is a reason to go looking; finding it is still the part worth earning.
- Your galaxies keep their rules through updates. A new setting, `AnomalyRules`, names the version
  of the anomaly rules this install uses; it is written once, as `1`, and the mod never changes
  it. Every 0.x release has used rules version 1, so nothing changes today. It matters because 1.0
  will introduce version 2, which draws anomalies differently: an install upgraded from 0.5 keeps
  its `1`, and every galaxy on it stays exactly as it is through 1.0 and beyond. Set
  `AnomalyRules = Latest` (or the new number) when you want the new rules. Nothing is written to
  your saves, or anywhere else -- that one line in the config is the whole record.
- `HomePlanetNeverAnomalous`, on by default, is the rule that has always been there -- your starting
  world stays ordinary -- now written down where you can turn it off. Off, the home planet takes the
  same chance as everywhere else, which most of the time still means nothing. Only the home planet
  is affected either way; every other world keeps exactly what it had.
- `AnomalyChancePercent` now reads `Seed` by default rather than `-1`, and says what it means.
  A number from 0 to 100 still forces a density; `-1` still works for configs written by earlier
  versions. Nothing about generation changes.
- Experimental, off by default: `MultiplierFromCombatSettings`. When on, a galaxy with the Dark Fog
  disabled or passive uses `PeacefulOutputMultiplier` (default 3) instead of `OutputMultiplier`;
  hostile galaxies are unchanged. The multiplier is a return on the cost of using an anomaly, and
  the largest cost is clearing a world and then holding it -- without the Dark Fog only hauling
  remains, so the same ×10 that is fair against a garrison is a giveaway without one. Resolved once
  when a save loads and written to the log. Changes neither which planets are anomalous nor which
  recipe each carries.
- Anomalies on recipes you have not researched can now show a marker without naming what they are.
  `UnresearchedAnomalies = Marker` marks the planet so you know something is there and worth coming
  back for, while withholding the name until the recipe means something to you. `Hide` is the 0.4
  behaviour and remains the default; `Show` names everything, as in 0.3 and earlier. A 0.4 config
  with `HideUnresearchedAnomalies = false` is migrated to `Show`, and the log says so.
- If a game update breaks one part of the mod -- a tooltip, a label -- the rest keeps working and
  the log names what is off. If production itself cannot be patched, the mod switches off entirely
  rather than label anomalies that do nothing.
- The mod now leaves the galaxy behind the main menu alone. DSP runs that demo like a real game,
  and earlier versions quietly treated it as one. Harmless, but a mod should not be doing
  anything in a menu.

## 0.4.0

- Anomalies stay hidden until you have researched the recipe they affect -- on the planet panel,
  on planet and star labels, and in the system counts. An unresearched recipe is already
  unavailable to you, so an anomaly on it named something you could not build and might not
  recognise. "Particle Broadband x10" twenty hours before particle broadband exists is noise, and
  noise teaches you to stop reading the labels. Now the star map fills in as your research opens
  up, and anything on it is something you can act on. Set `HideUnresearchedAnomalies = false` for
  the old behaviour. Display only: no planet changes what it produces.
- Fixed: anomalies were labelled by the item they produce rather than by the recipe. Where DSP has
  two recipes for the same item -- "Space Warper" from Graviton Lens and "Space Warper (advanced)"
  from Gravity Matrix, and nine other pairs -- the label named the item, so building the recipe you
  already knew produced no boost and looked like a broken mod. Labels now name the recipe, which
  is how the game itself distinguishes them. Affects roughly one anomaly in fifteen.

  Display only: no planet changes what it produces, and no galaxy is regenerated. If a planet
  seemed to have a broken anomaly, it was correct all along and now says so.

- Fixed: star map labels never updated. They were written once, when the star map was built, and
  nothing rewrote them afterwards -- so scanning a planet or finishing a technology did not make
  its anomaly appear until you reloaded the save. Reopening the map did not help. This was mostly
  invisible before now, but combined with hiding unresearched anomalies it meant the mod could
  hide something and then never un-hide it, which is the worst failure that feature could have.
  Labels now re-examine themselves about twice a second and rewrite only when the answer changed.
- Fixed: a stray dash in one label. DSP's own English name for the Thruster recipe is literally
  "- Thruster", so naming anomalies by recipe in this release exposed it and one planet advertised
  "- Thruster x10". Leading punctuation is now stripped from recipe names; hyphens inside a name
  are left alone, so EM-Rail Ejector and High-purity Silicon still read correctly.
- The log now says when an anomaly is being withheld, which technology would reveal it, and the
  galaxy survey reports both gates per planet -- whether you have scanned it, whether you have researched the recipe, and
  which way that came out. A feature that hides things has to be able to say what it hid, or every
  question about a missing label turns into guesswork.

## 0.3.0

- New `ExcludedRecipes` setting: name recipes that should never receive an anomaly, comma
  separated, by item name as shown in game or by numeric id. Useful if your galaxy keeps handing
  you anomalies on things you never mass-produce. Empty by default, because which recipes are
  worth having depends entirely on how you play -- one player's useless anomaly is what another
  builds by the thousand. Excluding a recipe only moves the planets that currently carry it.

## 0.2.0

- Anomalies are shown in the star map. A star lists the affected items in its system, and each
  planet names its item and multiplier, marked with a compact "Å", so a system can be read
  without opening every planet's description tab. `StarmapLabel` chooses between that (Detail), bare counts and markers (Marker),
  or nothing (Off).
- Refine and Particle recipes are now eligible for anomalies, alongside Smelt, Assemble and
  Chemical. Particle recipes were excluded on an unexamined assumption, which meant Strange Matter
  and other collider outputs could never be anomalous in any galaxy. This adds recipes to the pool,
  so a small number of planets change which recipe they carry; most are unaffected.
- Fixed: gas giants could receive an anomaly. They cannot host assemblers, so the anomaly could
  never be used and the star map advertised something unusable. Gas giants are now skipped.
  This removes anomalies from gas giants in existing galaxies; every other planet is unchanged.


## 0.1.0

First release.

- Most non-home planets carry an anomaly: one ordinary recipe produces ten times its normal
  output there.
- Which planets are anomalous, and which recipe each one affects, is derived from the galaxy
  seed. The same galaxy always produces the same anomalies, and nothing is written to saves.
- Anomaly density is itself drawn from the seed, between 25% and 75%, so galaxies differ from
  one another.
- Home planets never have an anomaly.
- Anomalies are shown in a planet's description tab once it has been scanned or visited, and
  machines running an anomalous recipe are marked in their own window.
- Configurable anomaly density and output multiplier.
