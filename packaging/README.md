# Planetary Anomalies

Most planets in your galaxy have an **industrial anomaly**: one ordinary recipe produces ten times its normal output, but only there.

A world where sorters come out ten at a time. A world that turns coal into energetic graphite by the stack. A world that assembles mini fusion power plants almost for free. Which world does what is drawn from your galaxy seed, so every galaxy is different — and the same galaxy always produces the same anomalies.

The point is to make exploration industrially interesting. A planet stops being "does it have titanium?" and starts being "what is this place unreasonably good at, and is that worth building around?"

## Why this exists

I wanted more to do.

Dyson Sphere Program already rewards optimisation beautifully, but by the mid-game exploration flattens out: you go looking for resources you already know you need, and one temperate world is much like another. I wanted a reason to be curious about a system beyond what ore it has.

So: give planets industrial personalities, and let that ripple outward. Finding an anomaly is a small reward for exploring. Working out whether it is worth using is a reward for thinking. And actually using it means moving production somewhere inconvenient and hauling the results home, which puts a new wrinkle in logistics — the part of the game I enjoy most anyway.

It is meant to add a decision, not a difficulty.

## How you find them

Scan or visit a planet, then open its **description tab** in the star map. If it has an anomaly, it says so:

```
ANOMALY
Sorter Mk.III: 2 → 20
```

That is the whole discovery mechanic — no hunting, no guessing. Knowing the planet means knowing its anomaly. A machine actually running an anomalous recipe also marks itself in its own window, so you are never left wondering why a number looks wrong.

Your **home planet never has an anomaly**. The starting world stays ordinary, deliberately: anomalies are a reason to look outward.

## What this means in practice

An anomaly does nothing until you build the affected recipe on that planet. Most of them will sit unused — that is the point. The interesting decision is whether a particular world is worth reorganising production around, not whether you can be bothered to find it.

Anomalies are **not balanced**, on purpose. A ×10 on iron ingots is a convenience. A ×10 on something late-game and expensive is a windfall. Finding one of those should feel like a discovery, not like a reward that has been carefully measured out for you.

## Configuration

Settings live in `BepInEx/config/com.planetaryanomalies.dsp.cfg` after the first run.

| Setting | Default | What it does |
| --- | --- | --- |
| `AnomalyChancePercent` | `-1` | How many non-home planets are anomalous. `-1` derives it from the galaxy seed, between 25% and 75%, so galaxies differ from one another. Any value from 0 to 100 forces that density instead. |
| `OutputMultiplier` | `10` | How much more an anomalous recipe produces. |

Changes take effect when a save is next loaded.

## Compatibility

- Built and tested against **DSP 0.10.34**.
- **No mod dependencies** beyond BepInEx.
- **Nothing is written to your saves.** Anomalies are recomputed from the galaxy seed every time you load, so removing this mod leaves a completely ordinary save behind. Recipe prototypes are never modified, so other mods reading them see vanilla values.
- Mods that **add recipes** may shift the anomalies on a small number of planets, since new recipes join the pool that anomalies are drawn from. The rest of your galaxy is unaffected.
- Multiplayer is untested.

## Known limitations

- Only one kind of anomaly exists so far: increased output.
- Only recipes with a single output item are eligible, so nothing that produces two different things at once.
- Proliferator interaction is untested beyond not crashing.
- English only.

## Roadmap

Rough intentions, not promises, roughly in the order they are being thought about.

**More kinds of anomaly.** Output multipliers are the simplest possible effect and the only one implemented. The interesting ones are different in kind: a recipe that needs half as much of an ingredient, one that runs several times faster, one that swaps an ingredient for something cheaper, one that produces an unexpected byproduct. A galaxy where every anomaly is "more stuff" is a thinner galaxy than one where planets are strange in different ways.

**Anomalies worth remembering.** Most should be useful; a few should be absurd. Rarity tiers, so that occasionally you find a world that genuinely changes your plans rather than mildly improving them.

**Softer descriptions.** Right now the panel tells you exactly what an anomaly does. There is an argument for saying only *what* is affected — "improved sorter output" — and letting you find out how much by building it. Existence stays free; magnitude becomes something you discover.

**Better ways to notice.** The planet description tab works, but it is somewhere you have to already be looking. Some signal at the system or galaxy level would make an unexplored region feel like it might be hiding something.

**Version pinning.** Anomalies are derived from the galaxy seed and the version of the generator. Right now, if the generator changes, existing galaxies change with it. Recording the version a galaxy was created under would let it keep its anomalies across mod updates — the one thing that genuinely needs saving.

**Multi-output recipes**, and a considered answer to proliferator, rather than the current "does not crash".

If you have opinions about any of this, or a galaxy that produced something memorable, the GitHub issues page is open.
