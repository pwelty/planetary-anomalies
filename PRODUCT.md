# Product

Planetary Anomalies should make exploration industrially surprising: occasionally a normal recipe behaves abnormally on one planet, creating memorable production worlds and reasons to reorganize interstellar manufacturing.

## Current product question

Can Dyson Sphere Program's real machine output be modified safely at execution time, on one planet only, without permanently mutating global recipe definitions?

## Stage progression

1. Hard-coded smelter recipe produces ×10 on the dynamically identified home planet only, proving the output seam and planet-locality together.
2. Random eligible single-output recipe on the home planet.
3. Persist the anomaly.
4. Derive sparse anomalies deterministically from galaxy seed and planet ID.
5. Add effect varieties, discovery, and UI only after the mechanism is stable.

## Non-goals before Stage 0 passes

No UI, persistence, scanning, random recipe selection, generalized effect framework, CommonAPI, GalacticScale, balance system, or content expansion. The home-planet guard is in scope; nothing beyond it is.
