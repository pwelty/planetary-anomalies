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

## Discovery: knowing the planet means knowing the anomaly

**When the player learns about a planet, they are told its anomaly.** Landing on it counts;
scanning it remotely counts. There is no separate hunt for the anomaly once the planet is known.

This deliberately supersedes the staged, hidden-until-triggered sketch in `SPEC.md`. That model
degenerates into brute force: with many recipes across many planets, "build the right recipe on
the right world and find out" means searching the recipe × planet cross-product. That is a
chore, and it punishes the curiosity the mod exists to reward.

The interesting decision is meant to be *what to do about* an anomalous planet — whether it is
worth reorganizing production around — not whether the player can be bothered to find it. Making
discovery cheap protects that decision instead of burying it.

### How precisely the anomaly is described

For now, **state it exactly**: "Iron Ingot: 1 → 10". Precision is what makes the thing testable
and legible while the mechanism is young.

Later this can soften to a qualitative description — "Improved iron ingot output" — that names
*what* is affected without giving the number. Existence stays free, magnitude becomes the thing
the player finds out by building it. That keeps a small discovery worth having without
reintroducing the search problem, since the player already knows which planet and which recipe
to look at.

Both forms are the same mechanism with different text, so nothing about the implementation needs
to anticipate the change beyond keeping the wording in one place.

Consequences to respect whenever this is built:

- An anomaly must be legible from the planet's own information, not only from watching a machine
  behave oddly.
- Once known, it stays consultable. The player should never have to keep notes to remember which
  world was the graphene world.
- An unknown planet reveals nothing, so unexplored space keeps its pull.

Not to be built until the mechanism is stable, but the design starts here rather than from
`SPEC.md`'s discovery section.

## Non-goals before Stage 0 passes

No UI, persistence, scanning, random recipe selection, generalized effect framework, CommonAPI, GalacticScale, balance system, or content expansion. The home-planet guard is in scope; nothing beyond it is.
