# Changelog

## 0.5.0 (in development)

- When you finish a technology, the mod says whether a world you have already scanned makes one of
  its recipes ten times over. Research Antimatter Capsule and it tells you Iclarkrav I has been
  making them ×10 the whole time. Shown as the game's own brief tip and written to the log.
  `AnnounceOnResearch = false` turns it off.

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
  behaviour and remains the default; `Show` names everything, as in 0.3 and earlier.

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
