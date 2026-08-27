# Product

Planetary Anomalies should make exploration industrially surprising: occasionally a normal recipe behaves abnormally on one planet, creating memorable production worlds and reasons to reorganize interstellar manufacturing.

## Current product question

Does stable, place-specific industrial variation make players explore more, reconsider globally
optimal factory doctrine, and build logistics around particular worlds?

## Current state and roadmap

The original staged progression was overtaken during the first working session: v0.1.0 already
derives anomalies deterministically across the galaxy, excludes home planets, discloses anomalies
through existing game surfaces, and has been published to Thunderstore. The obsolete intermediate
stages remain in `SPEC.md` as design history rather than current instructions.

The canonical forward plan is [`ROADMAP.md`](ROADMAP.md). Its immediate phase is observation and
hardening of the one-effect release, followed by bounded experiments rather than a prebuilt
modifier framework.

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

### Home planets never have anomalies

The rule above assumes the player can reach the planet detail panel. Early in a game they cannot: the star map (`V`) is not available yet, and the panel is only reachable through it.

That produces the worst version of the problem the rule exists to prevent. The first anomaly a player ever encounters would be the one they are least equipped to understand -- a machine behaving strangely, with the only explanation locked behind tech they do not have. Unexplained weirdness with no path to an answer is exactly the "suffering" this design is meant to avoid.

**Decided: home planets never have anomalies.** Not "rarely" and not "unless the roll says so" -- never. "Home planet" means the galaxy birth planet, the world the player starts on.

Three things fall out of that, all of them good:

- The early-game hole closes with no new UI. The player cannot encounter an unexplainable anomaly before the star map exists, because the only planet they can reach does not have one.
- It fits the intended experience better. Anomalies should be a reason to look at *other* worlds. Making the starting world special works against the pull outward that the whole mod exists to create.
- It is a principle rather than a tuning knob. No rarity value to balance, no edge case where an unlucky seed produces a confusing first hour.

The original home-planet test rig in `SPIKE.md` is now historical. v0.1.0 excludes the birth
planet, and end-to-end testing uses a developed multi-planet save and whichever non-home worlds the
generator selects.

### Always say "anomaly"

Every player-facing surface uses the word **anomaly**, in that form, so that anything the mod
says is instantly recognisable as coming from the mod rather than from the game. A player who
sees something odd should be able to tell at a glance whether Planetary Anomalies caused it.

Current surfaces, all consistent:

- Planet detail panel: a block headed `ANOMALY`, then the affected recipe.
- Assembler window: `ANOMALY ×10` appended to the machine state line.
- BepInEx log: `ANOMALY` blocks, `No anomaly:`, `Anomaly attached to ...`.

New surfaces must use the same word. Do not introduce synonyms -- no "irregularity", "quirk",
"bonus", or "effect" in player-facing text -- and do not abbreviate it.

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

## Non-goals

Stage 0 has passed, so the original "nothing beyond the home-planet guard" bar has been met and lifted. What remains out of scope, and why:

- **Custom UI objects.** The earlier blanket "no UI" meant: build no anomaly screens, planet panels, icons, localization, scanning UI, or discovery popups. That still holds. Disclosing the anomaly in the planet detail panel does not breach it -- it appends to a `Text` the game already creates and draws, and re-runs the game's own height calculation. No new GameObjects, prefabs, assets, or localization. Bespoke UI remains out of scope; borrowing existing UI does not.
- **Anomaly state persistence.** Current anomalies need none: planet and recipe are derived from
  the seed and stable identifiers. Save integration becomes necessary only for generator-version
  pinning or future stateful anomalies.
- **Generalized effect framework, balance system, content expansion.** Still premature. There is one effect type, and it should stay that way until the shape of several is actually known.
- **CommonAPI, GalacticScale, or any other mod dependency.** The plugin has none, and the smallest possible dependency surface remains the goal. `DSPModSave` becomes worth reconsidering only when something genuinely must be written to a save.
