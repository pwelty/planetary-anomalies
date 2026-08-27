# Roadmap

This is the canonical design roadmap for Planetary Anomalies. It records directions, questions,
and sequencing; it is not a promise that every idea below will ship.

The working release is **v0.1.0**: most non-home planets deterministically receive one ordinary,
single-output recipe whose output is multiplied by ten on that planet. Anomalies derive from the
galaxy seed, appear when the planet is known, and change real machine output without modifying
shared recipe prototypes or writing anomaly data to saves.

## The design thesis

Planetary Anomalies is **Fischer Random for Dyson Sphere Program**.

Fischer Random preserves chess skill while disrupting memorized opening theory. Planetary
Anomalies should preserve DSP's recipes, machines, ratios, and logistics while disrupting the idea
that one memorized factory doctrine is correct in every galaxy. Expertise should remain valuable,
but it should answer a situated question:

> Given the peculiar industrial geography of this galaxy, where should production happen?

The mod exists to restore two things:

1. **Exploration as looking, not merely scanning.** In ordinary play, scanning often means finding
   a known resource required by an existing plan. Anomalies should make it rational to explore
   because an unknown opportunity may change the plan.
2. **Contingency inside optimization.** Players should still optimize, but the optimum should depend
   on this seed, these planets, these distances, and these discoveries. The galaxy is strange; the
   factory remains trustworthy.

The clearest success case is intentionally extravagant: a player willingly builds a supply chain
20 light-years away because that world produces ten times as many electromagnetic turbines. The
anomaly does not remove logistics. It gives the player a compelling reason to build more of them.

## Principles to protect

### Location must matter

An anomaly succeeds when it changes where production belongs, reroutes an established supply
chain, or creates a specialized industrial world. A modifier that merely improves a number without
changing a decision is thin.

### Randomize the strategic situation, not machine reliability

Anomalies are determined before the player's decision and remain stable afterward. Avoid per-cycle
casino randomness as the default. Surprise belongs in discovery; mastery belongs in exploitation.

### Uneven value is the point

Some anomalies may be marginal, some timely only in the early or late game, and some absurdly
valuable. Do not normalize every effect into a polite percentage bonus. Balance should keep the
mechanic legible and prevent collapse, not prevent legendary industrial places from existing.

Do not assign universal quality labels such as Common/Epic/Legendary. The fact can remain stable
while its significance changes with technology, bottlenecks, transport capacity, nearby resources,
and the player's existing infrastructure.

### Create another logistics problem, not free items

A strong anomaly should still require inputs, transport, power, buffers, vessels, warpers, or a
commitment to inconvenient geography. The reward may be outrageous; realizing it should engage
DSP's systems rather than bypass them.

### Make places tell industrial stories

Future anomaly effects should feel like properties or histories of worlds, not spreadsheet rows
wearing random hats. Planet type may weight plausible anomaly families without determining them so
completely that players merely memorize a new lookup table. Coherence provides meaning; exceptions
preserve curiosity.

### Protect curiosity from chores

Once a planet is known, the player must not be forced to test the planet × recipe cross-product.
Discovery mechanics may require travel, scanning, probes, investment, or progressive disclosure,
but they should reveal actionable information rather than demand brute force.

### Add one variable at a time

v0.1.0 works because one extreme effect exposed the design quickly. Keep using vertical slices:
implement one coherent new behavior, play it, observe whether it changes decisions, and only then
build a framework around repeated needs.

## Evidence so far

v0.1.0 has been witnessed end to end in game:

- seed-derived galaxy density;
- deterministic planet and recipe selection;
- home-planet exclusion;
- anomaly disclosure in the planet panel;
- anomaly marker on a real machine;
- normal input consumption;
- multiplied output in the real machine buffer;
- normal inserter removal and output-cap behavior;
- planet locality without shared recipe mutation.

Paul's first developed-save example was a titanium-rich planet producing ten times as much Titanium
Crystal. Rather than export titanium for processing elsewhere, it made sense to import Organic
Crystal, manufacture locally, and export the finished product. That is the intended behavioral
change.

The first Reddit response understood the broader exploration problem and proposed a beacon launched
toward another system to learn what is there. Treat this as evidence that the premise invites
world-native design thinking, not as an immediate feature commitment.

## Roadmap now

### Phase A — Learn from v0.1.x

Do not outrun the first playable idea.

Collect stories and failures:

- Which anomalies actually cause players to relocate production?
- Which are useful early, mid, or late, and which become useful only after circumstances change?
- How far will players travel for a sufficiently valuable anomaly?
- Do players revisit known planets when their bottlenecks change?
- Does 25–75% per-galaxy density sustain curiosity or make anomalies feel routine?
- Does exact disclosure create good decisions, or would partial disclosure produce better play?
- Does the current machine buffer cap make ×10 feel powerful or merely stalled?
- What happens with proliferator and production statistics?
- Which other mods alter recipes or factory behavior in ways that matter?

Near-term hardening belongs here:

- verify production statistics;
- test proliferator deliberately;
- test a clean Gale profile;
- test uninstall/reinstall and game-update behavior;
- document multiplayer as unsupported until actually tested;
- pin the anomaly-system version when generator changes become plausible;
- improve compatibility without adding dependencies unless evidence requires one.

Success criterion: real players report changed factory placement or logistics, not merely larger
numbers.

### Phase B — A second static effect

Choose the second effect to test the design grammar, not to fill a catalog. It should differ in kind
from output multiplication and answer a specific question about play.

Candidate families:

- **Input efficiency:** consume less of one ingredient.
- **Speed:** complete the same recipe faster, with output and inputs unchanged.
- **Power:** alter the energy cost of a recipe or machine class.
- **Substitution:** replace one ingredient with another local or cheaper material.
- **Byproduct:** add a useful or troublesome secondary output.
- **Constraint/nerf:** require more input, more power, or more time in a specific and legible way.

A negative anomaly is worthwhile only when it creates planning, tradeoffs, or character. “Everything
is 15% slower” is punishment confetti. “Electronics consume twice the copper here, but turbines
produce at ×10” describes an industrial place.

Do not build a generalized effect framework before the second effect demonstrates what abstractions
are actually shared. After two or three real effects, extract the smallest common contract.

### Phase C — Planet-conditioned industrial personalities

Explore weighted relationships between planet type and anomaly family:

- lava worlds leaning toward heat-intensive or metallurgical effects;
- frozen worlds leaning toward cryogenic, chemical, or cooling effects;
- ocean worlds leaning toward chemical or organic effects;
- desert worlds leaning toward silicon or solar-related effects;
- high-wind worlds leaning toward power or lightweight-component effects.

These are thematic weights, not deterministic rules and not claims of scientific simulation. The
goal is for a player to say, “Of course that furnace world makes titanium alloy absurdly well,”
while still occasionally finding a surprising exception.

Questions:

- How much predictability creates story rather than a new memorized table?
- Should one planet have one coherent personality containing a buff and a constraint?
- Can local resources and anomaly effects combine into emergent industrial regions?
- Should neighboring systems ever form complementary anomaly corridors?

### Phase D — Discovery as infrastructure

The current rule is intentionally cheap: scan or visit a planet and its anomaly is disclosed. Any
new discovery system must preserve actionability and avoid recipe × planet brute force.

Candidate mechanisms:

- system- or galaxy-level signals that something unusual exists without revealing details;
- launched beacons or probes that report on another system;
- progressive disclosure: signal → affected recipe/family → exact effect;
- travel revealing more than remote scanning;
- consultable records of every anomaly already learned.

The beacon proposal is especially consonant with DSP: manufacture knowledge infrastructure, launch
it, wait, then decide whether the result merits an expedition. But it must add a meaningful
exploration decision rather than merely delay information behind a compulsory timer.

Possible disclosure ladder:

1. Remote observation: a system may contain an anomaly.
2. Beacon/probe: affected recipe or anomaly family.
3. Close scan or visit: exact magnitude and constraints.

This remains a research track until v0.1 play shows that the current disclosure model is too cheap.

### Phase E — Stateful anomalies and industrial history

Static anomalies give planets properties. Stateful anomalies give them histories.

Candidate forms:

- **Awakening:** after producing 1,000 units locally, a dormant effect activates.
- **Maturation:** sustained production changes the effect rather than merely increasing it.
- **Commitment:** the first eligible recipe to cross a threshold becomes the planet's specialization.
- **Branching:** player activity selects one of several possible industrial paths.
- **Coupling:** producing one item awakens an effect on another recipe.
- **Exhaustion:** an extraordinary boom settles into a smaller permanent advantage.
- **Mutation:** an anomaly changes after a documented condition.

Every threshold must create an investment, commitment, risk, or revelation. Avoid a universal grind
bar whose solved answer is always “produce 1,000 junk units before building the real factory.”

This phase changes the ontology and the implementation. Current anomalies are deterministic facts
recomputed from seed and planet ID. Stateful anomalies are dispositions whose manifestations depend
on player activity. They require deliberate decisions about:

- what state is saved;
- migration and anomaly-system versions;
- uninstall behavior;
- whether vanilla production statistics can be authoritative;
- UI for progress and state transitions;
- multiplayer authority and synchronization.

Do not enter this phase casually; it is a separate architectural milestone.

### Phase F — Mature procedural economic geography

Only after multiple effect and discovery types survive playtesting, consider combinations that make
galaxies develop recognizable economic geography:

- rare industrial capitals worth serving across extreme distance;
- ordinary worlds whose value changes with technology;
- complementary planets that form production corridors;
- boom worlds, constrained worlds, and latent opportunities;
- anomaly interactions with local resources and other discovered anomalies;
- tools for remembering and comparing known opportunities without ranking them for the player.

The aim is not more content for its own sake. It is a galaxy where players tell stories about places
because those places changed what they built.

## Candidate release sequence

This is a sequencing hypothesis, not a commitment:

- **v0.1.x — Observe and harden:** current ×10 static anomaly; statistics, proliferator, compatibility,
  documentation, and player stories.
- **v0.2 — Second effect vertical slice:** one effect of a different kind, selected from play evidence.
- **v0.3 — Industrial personalities:** planet-type weighting and perhaps one coherent buff/constraint
  pairing, if it remains legible.
- **v0.4 — Discovery experiment:** beacon/probe or progressive disclosure, only if current scanning
  proves too passive.
- **v0.5+ — Stateful experiment:** one awakening or commitment mechanic with explicit save semantics.

Version numbers may change. The dependency order matters more than the labels.

## Explicitly not on the immediate roadmap

- balancing every anomaly to equal economic value;
- universal quality tiers that tell the player what is “best”;
- per-cycle random output as the default model;
- a giant modifier framework before several effects exist;
- custom UI merely to make the mod look larger;
- mandatory dependencies without a demonstrated need;
- changing many variables simultaneously before their behavioral effects can be observed;
- treating every good community suggestion as the next sprint.

## The test for every proposed feature

A proposed feature earns its place when it passes most of these questions:

1. Does it make exploration, place, or logistics matter more?
2. Does it create a decision rather than merely add a bonus or delay?
3. Does it preserve deterministic, trustworthy machine behavior after discovery?
4. Can a player understand why their machine behaves differently?
5. Can its value change with context rather than being universally ranked?
6. Does it produce a story someone would tell about a particular planet?
7. Can it be tested as one bounded vertical slice?

If not, it may be an attractive modifier without belonging in Planetary Anomalies.
