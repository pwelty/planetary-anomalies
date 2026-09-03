# Planetary Anomalies

Most planets in your galaxy have an **industrial anomaly**: one ordinary recipe produces ten times its normal output, but only there.

A world where sorters come out ten at a time. A world that turns coal into energetic graphite by the stack. A world that assembles mini fusion power plants almost for free. Which world does what is drawn from your galaxy seed, so every galaxy is different — and the same galaxy always produces the same anomalies.

The point is to make exploration industrially interesting. A planet stops being "does it have titanium?" and starts being "what is this place unreasonably good at, and is that worth building around?"

## What's new in 0.4

**Anomalies stay hidden until you have researched the recipe.** Every surface follows the same
rule now — the planet panel, planet and star labels, and the system counts.

*Why:* a star map label reading `Particle Broadband ×10` twenty hours before you can make particle
broadband is not a secret being kept from you; the recipe is already unavailable, so the label
names something you cannot build and may not recognise. It is noise, and noise teaches you to stop
reading the labels. The rule is one line: **knowing a planet means knowing the anomalies you can
act on.** In practice the star map now fills in as your research opens up, which is the shape the
information actually has.

Set `HideUnresearchedAnomalies = false` if you preferred seeing everything. Nothing about
generation changes either way — the same planets are anomalous, you just get told later.

**Fixed: anomalies are named by their recipe, not by the item they make.** DSP has ten pairs of
recipes that produce the same item — Space Warper from Graviton Lens and *Space Warper
(advanced)* from Gravity Matrix, and nine more. The label named the item, so a player on a
"Space Warper" planet built the recipe they knew, got no boost, and reasonably reported a bug. The
mod was right and its label was wrong. Labels now name the recipe, which is exactly how the game
itself tells them apart. About one anomaly in fifteen reads differently; none of them moved.

## Previously, in 0.3

**You can now exclude recipes you do not want anomalies on.** A new `ExcludedRecipes` setting takes
a comma-separated list, by item name as it appears in game or by numeric id:

```
ExcludedRecipes = Water Pump, Assembling Machine Mk.I
```

*Why:* a player pointed out that some anomalies are simply not worth having -- their example was a
water pump anomaly, which is close to useless because a whole game needs very few water pumps. The
obvious fix would be for the mod to filter such recipes out itself, and that turns out to be the
wrong answer: which recipes matter depends entirely on how you play. Wind turbines, belts, sorters
and solar panels get placed by the thousand; someone else's dead weight is your bottleneck. So the
mod holds no opinion, and you state yours.

Empty by default, so nothing changes unless you want it to. Entries that match nothing are reported
in the log rather than silently ignored.

*What it does and does not affect:* nothing is written to your saves, and nothing about your factory
is altered. What changes is which anomaly a planet has — and only for planets carrying the recipe
you excluded. Every other planet keeps exactly what it had. Those planets stay anomalous and get
their next-best recipe instead.

The one thing to be aware of: if you had built production on a planet to exploit recipe X and then
exclude X, that planet's anomaly becomes something else and those machines return to normal output.
In practice this is unlikely, since you would be excluding a recipe precisely because you do not
build with it. Machines never get stuck part-way either — a machine's output data is reset from the
game's own recipe on load and whenever its recipe changes, so it cannot keep producing an anomaly
that no longer exists.

It does mean your galaxy differs from another player's with the same seed.

## And in 0.2

**You can see anomalies from the star map now.** Previously you had to select a planet and open its
description tab, one planet at a time — which meant the screen you actually explore from told you
nothing. Now a scanned anomalous planet is labelled with what it makes, and its star lists what the
system contains. You can read a region at a glance instead of clicking through it.

*Why:* several people said the same thing after the first release — that exploration goes quiet
once you've found the resources you need, partly because the star map shows so little. Putting the
anomalies there is the smallest thing that helps, and it turns out to be the difference between a
mechanic you remember and one you forget you installed.

**Gas giants no longer get anomalies.** They can't host assemblers, so an anomaly there could never
be used — it just advertised something impossible. If a gas giant in your galaxy had one, it
doesn't now; nothing else changes.

**Oil refinery and particle collider recipes can be anomalous.** Previously only smelters,
assemblers and chemical plants were eligible, which quietly ruled out Strange Matter, Deuterium and
refined oil entirely — no galaxy could ever have them. That was an oversight, not a decision.

*Effect on an existing save:* this adds three recipes to the pool, so a small number of planets
change what they produce — around four out of a hundred and fifty in testing. Everything else stays
exactly as it was. Anomalies are still derived from your galaxy seed, so nothing is random and
nothing is lost.

**Also:** the machine window now marks a machine running its planet's anomalous recipe, so the
panel no longer shows the normal recipe while the machine visibly does something else.

### A note on updates

Anomalies are generated from your galaxy seed, which means a mod update could in principle
rewrite your galaxy. It shouldn't, and from this release the build refuses to ship if the
generator changes without a deliberate version bump — so your worlds stay your worlds across
updates. The exceptions are called out above, and both are corrections rather than reshuffles.

## Why this exists

I wanted more to do.

Dyson Sphere Program already rewards optimisation beautifully, but by the mid-game exploration flattens out: you go looking for resources you already know you need, and one temperate world is much like another. I wanted a reason to be curious about a system beyond what ore it has.

So: give planets industrial personalities, and let that ripple outward. Finding an anomaly is a small reward for exploring. Working out whether it is worth using is a reward for thinking. And actually using it means moving production somewhere inconvenient and hauling the results home, which puts a new wrinkle in logistics — the part of the game I enjoy most anyway.

It is meant to add a decision, not a difficulty.

## How you find them

Scan or visit a planet and the star map tells you. An anomalous planet is labelled with what it makes, and its star lists what the system contains, so you can read a region without clicking into it. Selecting a planet and opening its **description tab** gives the full detail:

```
ANOMALY
Sorter Mk.III: 2 → 20
```

That is the whole discovery mechanic — no hunting, no guessing. Knowing a planet means knowing the anomalies you can act on: an anomaly on a recipe you have not researched yet stays quiet until the research lands, so the map fills in as the game opens up rather than naming things you cannot build. A machine actually running an anomalous recipe also marks itself in its own window, so you are never left wondering why a number looks wrong.

Your **home planet never has an anomaly**. The starting world stays ordinary, deliberately: anomalies are a reason to look outward.

## What this means in practice

An anomaly does nothing until you build the affected recipe on that planet. Most of them will sit unused — that is the point. The interesting decision is whether a particular world is worth reorganising production around, not whether you can be bothered to find it.

Anomalies are **not balanced**, on purpose. A ×10 on iron ingots is a convenience. A ×10 on something late-game and expensive is a windfall. Finding one of those should feel like a discovery, not like a reward that has been carefully measured out for you.

### The galaxy does the balancing

Anomalies are not balanced against each other. Using one is balanced anyway, and not by anything
this mod invented.

An anomaly is somewhere you did not choose. Reaching it costs logistics. Building on it costs a
factory you have to think about rather than paste. And on most worlds worth having it costs a
fight: you clear the dark fog, and then you *hold* it, because ground bases get re-seeded from
orbit. There is no trip that ends the problem. So a remote anomaly is never just a ×10 -- it is a
×10 plus a garrison, plus the power to run it, plus everything you shipped out to build it.

That price scales with exactly what the mod is asking of you. The further out you are willing to
go, the more the galaxy charges, and it charges in a currency -- ground you have to keep -- that no
output multiplier can pay off. Nothing in this mod knows the dark fog exists. DSP has been pricing
distance the whole time; the mod only had to give you a reason to go.

Which means a good anomaly never asks whether ×10 is worth having. It asks whether this one is worth
holding ground for, twenty light years from home. That is a better question, and the mod is a good
deal worse at asking it than the game is.

## Configuration

Settings live in `BepInEx/config/com.planetaryanomalies.dsp.cfg` after the first run.

| Setting | Default | What it does |
| --- | --- | --- |
| `AnomalyChancePercent` | `-1` | How many non-home planets are anomalous. `-1` derives it from the galaxy seed, between 25% and 75%, so galaxies differ from one another. Any value from 0 to 100 forces that density instead. |
| `OutputMultiplier` | `10` | How much more an anomalous recipe produces. |
| `StarmapLabel` | `Detail` | What star map labels show. `Detail` names the affected items, `Marker` shows counts and a symbol, `Off` hides them. Unscanned planets show nothing either way. |
| `ExcludedRecipes` | empty | Recipes that should never receive an anomaly, comma separated — by item name as it appears in game, or numeric id. The mod holds no opinion about which anomalies are worth having, because that depends entirely on how you play; this is where you state yours. Entries matching nothing are reported in the log rather than ignored. |
| `HideUnresearchedAnomalies` | `true` | Hides an anomaly until you have researched the recipe it affects, everywhere it would otherwise appear. `false` shows every anomaly on any planet you have scanned, as in 0.3. Display only: generation is unchanged. |
| `LogEveryAnomaly` | `false` | Writes every anomaly in the galaxy to the log, including planets you have never scanned. Spoils discovery on purpose; for troubleshooting. |

Changes take effect when a save is next loaded.

## Compatibility

- Built and tested against **DSP 0.10.34**.
- **No mod dependencies** beyond BepInEx.
- **Nothing is written to your saves.** Anomalies are recomputed from the galaxy seed every time you load, so removing this mod leaves a completely ordinary save behind. Recipe prototypes are never modified, so other mods reading them see vanilla values.
- Mods that **add recipes** may shift the anomalies on a small number of planets, since new recipes join the pool that anomalies are drawn from. The rest of your galaxy is unaffected.
- Multiplayer is untested.

## Known limitations

- Only one kind of anomaly exists so far: increased output.
- Only recipes with a single output item are eligible, so nothing that produces two different things at once — which still rules out plasma refining and antimatter, since both produce hydrogen alongside their main output.
- Proliferator interaction is untested beyond not crashing.
- English only.

## Roadmap

Rough intentions, not promises, roughly in the order they are being thought about.

**More kinds of anomaly.** Output multipliers are the simplest possible effect and the only one implemented. The interesting ones are different in kind: a recipe that needs half as much of an ingredient, one that runs several times faster, one that swaps an ingredient for something cheaper, one that produces an unexpected byproduct. A galaxy where every anomaly is "more stuff" is a thinner galaxy than one where planets are strange in different ways.

**Anomalies worth remembering.** Most should be useful; a few should be absurd. Rarity tiers, so that occasionally you find a world that genuinely changes your plans rather than mildly improving them.

**Softer descriptions.** Right now the panel tells you exactly what an anomaly does. There is an argument for saying only *what* is affected — "improved sorter output" — and letting you find out how much by building it. Existence stays free; magnitude becomes something you discover.

**Version pinning.** The build now refuses to ship if the generator changes by accident, so updates do not quietly rewrite your galaxy. What is still missing is recording which generator version a galaxy was created under, which would let existing galaxies keep their anomalies even through a *deliberate* change. That single number is the one thing that genuinely needs saving.

**Multi-output recipes**, and a considered answer to proliferator, rather than the current "does not crash".

If you have opinions about any of this, or a galaxy that produced something memorable, the GitHub issues page is open.
