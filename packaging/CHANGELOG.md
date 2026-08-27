# Changelog

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
