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

## Known design tension: discovery must not become brute force

Anomalies are meant to be hidden until discovered, but with many recipes across many planets a
naive "hidden until you happen to build the right recipe here" rule degenerates into searching
the whole cross-product. That is a chore, and it punishes the curiosity the mod exists to
reward.

The working constraint: the player should learn **that** a planet is anomalous cheaply, and
**what** the anomaly is with effort. A planet-level signal — from scanning, landing, or a survey
— narrows the search to one world and ideally to a category, leaving the specific recipe as the
thing worth investigating. Whatever the eventual mechanism, no design should require trying
every recipe on a planet, or visiting every planet with the same recipe, to find anything.

Documentation of a discovered anomaly matters as much as the discovery: once found, it should
stay legible somewhere the player can consult, not live only in their memory or notes.

Not to be built until the mechanism is stable; recorded so the eventual design starts here.

## Non-goals before Stage 0 passes

No UI, persistence, scanning, random recipe selection, generalized effect framework, CommonAPI, GalacticScale, balance system, or content expansion. The home-planet guard is in scope; nothing beyond it is.
