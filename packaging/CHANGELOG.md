# Changelog

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

---

## Listing notes (not part of the changelog)

Thunderstore community: **Dyson Sphere Program**.

Categories chosen for 0.1.0: **Assembling Machines**, **Logistics**, **Resources**.

*Resources* is the loosest of the three -- in DSP it usually signals ore and veins (PlanetFinder
is tagged that way for vein search), so browsers there are often after extraction tooling. Kept
because the mod does change what a planet is worth travelling to, which is the same question.
Categories are editable after publishing, so this is cheap to revisit.

Deliberately not chosen:

- *Nebula Compatible* -- multiplayer is untested and the README says so; claiming it would invite
  bug reports that cannot be answered.
- *Libraries* -- that is for APIs other mods depend on.
- *Quality of Life* -- the busiest category and tempting for reach, but this adds a mechanic
  rather than smoothing friction, and QoL users are not necessarily looking for a gameplay change.

The category list is not fully discoverable from cached package data; "Assembling Machines" does
not appear in any installed mod's metadata but exists on the upload page. Check the page rather
than inferring the list.
