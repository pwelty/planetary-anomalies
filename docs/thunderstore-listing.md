# Thunderstore listing notes

Internal notes about how the package is listed. These lived at the bottom of the shipped
CHANGELOG.md from 0.1.0 until 0.5.0, under a heading that said they were not part of the
changelog -- which did not stop Thunderstore rendering them on the public page for four
releases. Moved here, and package.ps1 now refuses a changelog whose headings are not all
version numbers.


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
