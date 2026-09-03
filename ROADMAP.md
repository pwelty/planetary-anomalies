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

The replies beneath it are stronger evidence than the suggestion itself. Three separate players
described the same complaint without prompting: the star map gives no indication of what has been
scanned, the 6 ly boundary is invisible in a 3D view, and exploration ends abruptly — "bam, done
exploring." The mod currently discloses anomalies only after selecting a planet and opening its
description tab, which means the screen people actually explore from shows nothing. This converges
with the premise rather than merely sitting near it.

The release thread also exposed a description failure rather than a balance one. Readers could not
determine whether the effect altered output or speed, whether it applied to one recipe or to a
production chain, or that it was configurable at all. "10x is insane" and "only 1 like ingots would
be OK, but a full chain is really insane" are both answered by facts that were never stated: one
recipe per planet, output only, inputs and cycle time unchanged, one planet's anomaly independent of
its neighbours, and both multiplier and frequency configurable. Treat unclear communication as a
defect with the same standing as a code defect; it produced objections to a mod that does not do
what the objections describe.

Two community suggestions were declined and the reasons are worth keeping. Adding drawbacks to
compensate powerful anomalies — more aggressive dark fog on anomalous worlds, resource costs to
discover — contradicts *Uneven value is the point*: an anomaly with a price becomes a trade to
evaluate rather than something to find. Making anomalous planets carry only the resource they favour
would cross an architectural boundary rather than a design one; veins are real save data, so writing
them would end the property that the mod can be removed leaving an ordinary save. That property is
why players could try v0.1.0 on established saves at all.

Star map disclosure was built during Phase A and settled three questions by play rather than
argument, each reversing a prior guess:

- **Planet labels beat the description tab.** "Much quicker than using the descr text." Reading a
  system at a glance is the behaviour worth supporting.
- **A count is not enough.** Stars first showed "3 ANOMALIES", on the theory that at galaxy scale
  the question is "is there anything here". Wrong: "I keep forgetting what is in that system." The
  question is what, not whether.
- **Truncation is worse than length.** Names were then capped at three with a "+2" tail, which
  reintroduced the same failure in miniature. Uncapped: "yes it's long and they overlap, but it's
  nice to see at a glance."

Label overlap in dense regions is therefore a known and accepted cosmetic cost, not an open bug.
If it is ever addressed, the fix is wrapping, a smaller font, or hiding labels below a zoom
threshold -- not truncation, which has now failed twice.

The pattern across all three: guesses about legibility were wrong in the same direction every time,
consistently underestimating how much information the player wants on screen.

### Phase A success criterion: met, on the author

A galaxy-wide survey of two saves (a diagnostic that ignores discovery, off by default) produced
the clearest evidence yet.

Seed 40078654: 146 anomalous of 252 planets. Paul went looking for a Quantum Chip anomaly and there
was none -- in either surveyed galaxy. What that galaxy had instead was `Plane Filter ×10` on two
worlds, one step upstream: `1 Casimir Crystal + 2 Titanium Glass -> 10 Plane Filter`, where Plane
Filter is the expensive half of a Quantum Chip.

**He relocated Plane Filter production.** Unprompted, on a galaxy he did not design, because the
anomaly was not where he wanted it and building around where it actually was made more sense than
not. That is the Phase A success criterion -- "changed factory placement or logistics, not merely
larger numbers" -- met by the mechanic rather than by argument.

The shape of it matters as much as the fact. A galaxy that granted the requested anomaly would have
taught nothing; one that offered a neighbouring opportunity forced a decision. Design consequence:
**do not make anomalies more grantable.** The temptation will be to raise density, or to weight
selection toward what a player needs. Both would remove exactly the friction that produced this.

Distribution notes from the same survey, useful for the duplicates question:

- 147 eligible recipes, 145 anomalous planets, **91 distinct recipes used**. Expected distinct under
  uniform independent draws is ~92.5, so selection is behaving correctly.
- ~56 recipes appear nowhere in a given galaxy. That is a feature: a galaxy has things it simply
  does not do, and Quantum Chip being one of them is what sent Paul upstream.
- Any specific recipe has roughly a 37% chance of being absent from a galaxy this size. Absence is
  ordinary, not a bug -- worth remembering when someone reports "my galaxy has no X".

### Player feedback after 0.1.x

A player who actually installed it reported three things worth separating.

**They run ×3, not ×10.** The configurable multiplier earned its place. It also suggests ×10 may be
the wrong default rather than merely a bold one; two people have now called it too high.

**They mostly do not use anomalies, "as I would have to wrap my head around the blueprints I used
to play with."** This is the most important sentence anyone has said about the mod, and it is the
thesis meeting reality. Planetary Anomalies asks players to abandon memorised layouts; blueprints
are precisely the tool that makes memorised layouts free. A player with a blueprint library has
already paid for a factory doctrine, and an anomaly asks them to write it off.

That is not obviously a problem to fix -- it is the friction the mod exists to create -- but it is
the difference between a mechanic people admire and one they use. Worth watching for whether
anomalies get used by players *without* established blueprint libraries, and whether the ones who
do use them are reacting to unusually valuable anomalies rather than ordinary ones.

**"Can you add a rule that e.g. lava planets don't add water pump effect?"** Declined, and the
reason is worth keeping because the request will recur.

It rests on a misreading. The anomaly makes the *item* cheaper to manufacture; where that item gets
used is a separate question. A lava world producing water pumps ten at a time is no stranger than
producing them anywhere else -- you export the pumps to wherever water needs pumping. Manufacturing
location and use location are already decoupled in DSP, and that decoupling is the game.

A plausibility rule would therefore constrain nothing real while making generation planet-dependent
and harder to reason about. Note also that DSP's own `ItemProto.productionMask` would not have
helped: it records how an item can be produced -- recipe type, mining, gas collection -- not where.

**The complaint did surface something underneath it, but it is narrower than it first looked.**

The initial reading was that building recipes are duds because nobody mass-produces buildings.
Paul corrected that from actual play: the buildings placed in bulk are belts, sorters, wind
turbines, solar panels and gas transport stations, and a ×10 on any of those genuinely changes a
build. Wind Turbine was wrongly called a dud by someone reasoning from "it is a building" without
knowing which buildings get placed by the thousand.

What survives is a much shorter list -- `Water Pump ×10` really is close to worthless, because a
whole game needs very few -- and even that is shakier than it looks, because Paul's own phrasing
was "for me anyway". Which buildings a player mass-produces depends on their style and stage: a
Dyson swarm build places different things than a solar-tiling build, and an early game places
different things than a late one.

So filtering duds requires ranking recipes by value, and *the test for every proposed feature*
asks whether a thing's value can change with context rather than being universally ranked. A dud
filter is a universal ranking by construction. It would also need hand-curating, and the list would
rot as DSP changes.

Current position: **do not filter.** The dud problem is real but small, and the cure asks the design
to do something it explicitly refuses. Revisit only if players report duds often enough that the
noise outweighs the principle -- and if so, prefer a rule grounded in something the game already
knows over a hand-written list.

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

- state plainly what the mod does, in the listing and the README: one recipe per planet, output
  only, inputs and cycle time unchanged, neighbours independent, multiplier and frequency
  configurable. This is the cheapest change available and it answers most of the balance objection
  without touching balance;
- verify production statistics;
- test proliferator deliberately;
- test a clean Gale profile;
- test uninstall/reinstall and game-update behavior;
- document multiplayer as unsupported until actually tested;
- pin the anomaly-system version when generator changes become plausible;
- improve compatibility without adding dependencies unless evidence requires one;
- replace the placeholder icon shipped with v0.1.0;
- make the mod appear as a real package in Gale. Deferred: installing the Thunderstore release is
  the obvious route, but it cannot coexist with a local build in the same profile, and a separate
  dev profile proved unworkable in practice. Revisit when iteration slows.

One visibility question belongs here rather than in a later phase, because three players raised it
on release day and the mechanism is already understood. Should an anomalous planet be marked in the
star map without being selected?

`docs/inspection.md` records the surface: `UIStarmapPlanet` exposes a public `nameText` label and a
public `planet`, and that text is assigned in `_OnInit` and `OnPlanetDisplayNameChange` rather than
`_OnUpdate` — so an appended marker persists with no per-frame cost, unlike the assembler window,
which must re-append every frame. Cheaper than either surface already built.

The open questions are about restraint, not feasibility:

- a marker only, or the affected recipe? A galaxy view carrying dozens of recipe names is noise;
- it must respect `PlanetData.scanned`, or it reveals the existence of something the discovery rule
  says the player has to earn;
- whether a star system should also read as worth visiting before its planets are legible.

### Appearing as a real package in Gale

`scripts/install.ps1` copies the built DLL into a profile's `BepInEx/plugins/PlanetaryAnomalies/`.
BepInEx loads it, but Gale never lists it, because Gale's mod list comes from its own
`data.sqlite3` rather than from scanning the folder. A hand-copied plugin is invisible to the UI,
cannot be toggled there, and could be removed by a profile repair or re-sync.

Now that v0.1.0 is on Thunderstore this mostly stops being a packaging problem and becomes a
workflow one: installing it through Gale from Thunderstore makes it a first-class package with no
code change at all.

The wrinkle is that development then collides with it. A Gale-installed `pwelty-PlanetaryAnomalies`
and a hand-installed `PlanetaryAnomalies` both declare the same `BepInPlugin` GUID, so BepInEx will
load one and refuse the other — and which one it refuses is not something to leave to chance while
testing.

**Two profiles was tried on 2026-08-28 and abandoned.** A clean dev profile holding only BepInEx
failed to run: launching it outside Gale skips setup Gale performs, and the other mods in the real
profile -- ModFixerOne in particular -- are not optional in practice. Developing against the
profile actually played is the working arrangement, with `scripts/local.paths.ps1` pinning it so no
flag is needed.

Remaining options:
- **Have `install.ps1` refuse to install** alongside a Gale-managed copy of the same GUID, rather
  than producing a silent conflict. Worth doing regardless of which option is chosen.
- Overwriting the DLL inside Gale's own package folder. It keeps one profile, but Gale then
  displays the Thunderstore version number for a build that is not it, which is exactly the kind of
  quiet inaccuracy that wastes an hour later.

Success criterion: real players report changed factory placement or logistics, not merely larger
numbers.

### No duplicate recipes (under consideration)

Two planets can currently receive the same recipe, which weakens the identity the mod trades on --
"the sorter world" means less when there are three of them.

Two things constrain it.

**Galaxy-wide uniqueness may be impossible.** There are ~147 eligible recipes; a default galaxy has
roughly 200-300 planets, and at 25-75% density that is ~50-200 anomalous ones. In the upper half of
that range the recipes simply run out, so the rule can only ever be "unique where possible", and the
exhaustion case needs a defined behaviour rather than an accident.

**It is a generation change.** Uniqueness cannot be a per-planet decision -- choosing a planet's
recipe requires knowing what others took -- so it becomes a galaxy-wide assignment. That moves
recipes, trips the golden test, and needs an `AnomalySystemVersion` bump. Not a total scramble: with
a deterministic assignment order a planet keeps its first choice unless an earlier planet took it,
so perhaps 20-30% move. But every existing galaxy shifts.

Options:

- **Per-system uniqueness.** No two planets around the same star share a recipe. Always achievable,
  and arguably where duplication actually grates, since a system is what you see on one screen.
- **Galaxy-wide, best effort.** Strongest identity, largest reshuffle, needs the exhaustion rule.
- **Defer to 1.0**, where a re-roll is already accepted, and bundle it with variable multipliers so
  players pay one re-roll rather than two.

The third is the current leaning, for that last reason alone.

### Icons instead of names (investigated; viable in the panel, not the star map)

Paul: "I don't know the names of a lot of things, just the images." That is not a small point. If
players recognise items by icon, a name is not merely more verbose than an icon -- it is harder to
read. It moves showing icons from polish toward correctness.

**Star map labels: not feasible cheaply.** `ItemProto.iconTagString` exists -- `\` + IconTag + `;`
-- but it is only written during `Preload` and never read; nothing in the game assembly parses that
syntax. There are no `UnityEngine.UI.Text` subclasses and no TextMeshPro. DSP composes icons and
text as *separate* `Image` and `Text` components. So an inline glyph in a star map label means
creating an `Image` per label, positioning it against variable-width text, and managing its lifetime
with the label's pooling -- the "custom UI objects" category `PRODUCT.md` keeps out of scope.

**The planet panel: viable, using the game's own components.** It already instantiates icon rows for
the resource list, and the API is public:

~~~
UIResAmountEntry.SetInfo(int index, string label, Sprite icon, string tip,
                         bool highlightLabel, bool highlightValue, string strBuilderFormat)
UIPlanetDetail.GetEntry()      // pooled, or instantiated from entryPrafab
ItemProto.iconSprite
~~~

An anomaly could appear as an icon row in the same visual language as the resources beneath it,
reusing DSP's prefab and pool rather than inventing a component. That sits on the right side of the
line the mod has held: borrowing existing UI, not building bespoke UI.

The risk is layout bookkeeping. `UIPlanetDetail` counts entries and computes the resources tab
height (`SetResCount`, `resourcesTabHeight`), so injecting a row needs care or it will misplace
things. Fiddlier than the text appends done so far, and it should be judged on screen rather than
in the abstract.

Cheaper in the meantime: the existing `Marker` mode, or a Unicode symbol prefix, with the caveat
that the label font may not carry the glyph.


### Shipped in 0.4: hide labels for recipes not yet researched

Paul's idea, from starting a fresh run. A star map label reading `Particle Broadband ×10` twenty
hours before you can make particle broadband is noise, and noise that teaches players to stop
reading labels. Showing it only once the recipe is researched turns the star map into progressive
disclosure: information appears when it becomes actionable.

Directly implementable. `GameHistoryData.RecipeUnlocked(int recipeId)` is public and is what the
game itself uses for the same question; `GameHistoryData.recipeUnlocked` is the underlying set.

**The obvious risk, and the feature hiding inside it.** If a label stays hidden until research
lands, a player who sweeps a region early sees little and may never look again -- so a world that
became relevant goes unnoticed. That would be worse than the noise it replaced.

But `GameHistoryData` exposes an `onTechUnlocked` event, and `UIPlanetDetail` already refreshes on
it. So the fix is better than the problem: when a recipe is researched, the galaxy can say it
already knows where that recipe is unreasonably cheap. "You have researched Sorter Mk.III. You
scanned a world three jumps away that makes them ten at a time." That is the mod telling a player
something genuinely useful at the exact moment they can use it, which is a better moment than
discovery.

**Decided: hide it everywhere, not only on star map labels.** Paul's reasoning is that there is
nothing to "disable" -- an unresearched recipe is already unavailable to the player, so an anomaly
on it is not information being withheld, it is information that does not yet mean anything.

That gives one rule across every surface instead of a split between ambient and deliberate ones,
and it restates the discovery rule cleanly:

> Knowing a planet means knowing the anomalies you can act on.

It is self-consistent without special cases. The machine window needs none, since running a recipe
implies having researched it. Star counts naturally count only what the player can use. And the
planet panel showing nothing for an unresearched anomaly matches the star map showing nothing.

Remaining open questions:
- Does this make the early game feel *emptier* than it already does? The home planet is never
  anomalous and scanning range is short, so the first hours are already vanilla. This removes more.
  It may be right anyway -- an empty early star map that fills as you research is a better shape
  than a full one you learn to ignore -- but it is a real cost and should be judged in play, which
  is why a fresh run matters.
- Should already-known-but-newly-relevant anomalies be announced once, or quietly appear? Announcing
  risks nagging; appearing quietly risks going unseen.

Not a generation change: this is display only, so it cannot move anyone's galaxy.

**Shipped in 0.4**, gated on `GameHistoryData.RecipeUnlocked` as anticipated, applied at the three
display surfaces rather than inside the describe methods so that `LogEveryAnomaly` keeps dumping the
whole galaxy -- a diagnostic that deliberately ignores every disclosure rule should keep ignoring
this one. `HideUnresearchedAnomalies` defaults to true; false restores 0.3 behaviour, because this
changes what an existing player sees and that is their call to reverse.

The two open questions above are now play questions, not design questions:
- whether the early game feels emptier. Judge on a fresh run, not a mature save, where almost
  everything is researched and the change is nearly invisible.
- the `onTechUnlocked` announcement is **not** built. Hiding had to come first: there is nothing to
  announce until something was being withheld. It is the obvious next move, and it is what turns
  this from a subtraction into an addition -- "you have researched Sorter Mk.III, and you scanned a
  world three jumps away that makes them ten at a time."

### Candidate: export the galaxy index as a single web page

Paul's idea, after I generated one by hand for his seed so he could plan around it: the artifact
turned out to be more fun than a planning aid. A self-contained HTML file listing every anomaly you
know about, grouped by system, that you can open in a browser, keep on a second monitor, or paste
to a friend playing the same seed.

It costs almost nothing to build. The mod already derives every anomaly, already knows which
planets are scanned, and already renders these strings for the panel and star map. An export is
that same data written to a file with a `<style>` block around it. No new game hooks.

**What makes it interesting is what it refuses to include.** The obvious version dumps the whole
galaxy, which is the answer key -- it is `LogEveryAnomaly` with better typography, and it deletes
the exploration the mod exists to create. The version worth building exports only what the player
has actually discovered: scanned planets, and -- following the 0.4 rule -- only the recipes they have
researched. Then it is a field notebook rather than a solution file, it grows as the galaxy opens
up, and re-exporting after a long survey run is itself a small reward.

That also makes it honest about its own limits. A page that says "seventeen anomalies known, of a
galaxy you have not finished looking at" is a truer artifact than one claiming completeness.

Open questions:
- How is it triggered? A config flag that writes on save is invisible and cheap; a keybind is
  discoverable but needs UI. Probably a config flag first, since the audience for this is small and
  already reads the config file.
- Where does it go? Next to the save, or `BepInEx/` -- the former is more findable, the latter less
  intrusive.
- Does it become stale immediately? Yes, and that is fine: it is a snapshot, dated in the header.

Low priority, high delight-per-line. A good thing to build on a day when the generator should not
be touched.
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

#### Candidate: does Icarus's replicator obey the anomaly?

Paul's question. Today: no. The seam is `AssemblerComponent.recipeExecuteData`, which is factory
machines only; the mecha replicator is `MechaForge`, an unrelated system with its own task queue.

Technically cheap. `ForgeTask` exposes `recipeId`, `productCounts` and `produced`;
`MechaForge.TaskDeliver` hands `produced` to the player via `Player.TryAddItemToPackage`; and
`GameMain.localPlanet` says where Icarus is standing. Perhaps 40 lines on a separate seam.

The argument for is consistency: an anomaly is a property of the *place*, not of a machine type. If
local industrial physics are strange here, the replicator standing in that place should be strange
too. A player who hand-crafts on an anomalous world and sees nothing has found an inconsistency.

The argument against is stronger than it first looks. This is the application that least serves the
thesis -- see *Create another logistics problem, not free items*. Hand-crafting creates no logistics
at all: inputs are already in your pocket and the output goes straight back there. It is the closest
this mod could come to simply granting items, and the version most likely to read as an exploit
rather than a discovery. Home planets never being anomalous limits it, since you must travel to
benefit, but that is a mitigation rather than an answer.

If built, multiply when the task is created rather than at delivery, so the replicator window shows
the real count. Patching `TaskDeliver` alone would show 1 and hand over 10, which is the same
"UI lies" defect already fixed twice on other surfaces.

Scheduled for consideration, not for the next release.

#### Candidate: alternative production paths (Against the Storm)

Paul's framing: some items should have more than one recipe, the way Against the Storm gives a
settlement a particular subset of production paths and makes "which route is available here" the
interesting question.

This fits the thesis better than most modifier ideas. It is not "more stuff" -- it changes *where
production belongs* by changing what a place can do, which is the mod's stated purpose. It also
differs in kind from output multiplication, which is what Phase B asks for. And DSP already
establishes the concept: graphene from fire ice, diamonds from kimberlite.

There are two versions, and they are very different in cost.

**Substitution — reachable with the seam already built.** Same recipe, different inputs on that
planet. Inspection confirms `AssemblerComponent.UpdateNeeds` computes `needs` from
`recipeExecuteData.requires` and `requireCounts`, and `InternalUpdate` consumes from the same
arrays (IL_0394, IL_03a1). We already hand affected machines a private `RecipeExecuteData`, so
changing `requires` there would make the machine consume something else -- and because `needs`
derives from it, inserters and logistics would fetch the substituted ingredient on their own. No
new machinery.

**Additional recipes in the picker — much harder.** Recipes are global protos and the picker lists
what is unlocked; making a recipe available on one planet only is not something this seam touches.
Do not assume the cheap version generalises to this.

The design cost is UI honesty, and it is worse for inputs than for outputs. The machine panel reads
the prototype, so it would list the vanilla ingredients while the machine wants different ones. A
player feeding copper into something that silently ignores it reads as a bug, not a discovery.
Output multiplication is forgiving here -- you get more than promised -- but substitution must be
stated plainly on the machine before it ships, not merely in the planet panel.

Worth prototyping as the Phase B effect if it survives that constraint.

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
- **v0.4 — Discovery experiment:** progressive disclosure. Shipped, in the form of hiding anomalies
  until their recipe is researched; the beacon/probe half remains unbuilt and unneeded so far.
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
