# Planetary Anomalies

Most planets in your galaxy have an **industrial anomaly**: one ordinary recipe produces ten times its normal output, but only there.

A world where sorters come out ten at a time. A world that turns coal into energetic graphite by the stack. A world that assembles mini fusion power plants almost for free. Which world does what is drawn from your galaxy seed, so every galaxy is different — and the same galaxy always produces the same anomalies.

The point is to make exploration industrially interesting. A planet stops being "does it have titanium?" and starts being "what is this place unreasonably good at, and is that worth building around?"

## Before 1.0: your galaxy will keep its rules

This is the last release before 1.0, and 1.0 will change how anomalies are drawn -- some
combination of unique recipes, varied multipliers, and research cubes. Every 0.x release has used
the same rules, which is why your anomalies have never moved. Under new rules they would.

So this release adds the one thing that lets an update change the rules without changing your
galaxy: a setting that names the rules version, `AnomalyRules = 1`. It is written into your config
once, and the mod never changes it. When 1.0 arrives with rules version 2, your config still says
`1`, and every galaxy on this install keeps rolling exactly as it does today. Your Kaus III stays
your Kaus III. A fresh install of 1.0 gets `2` as its default and the new rules from the start.

You do not have to do anything. When you *want* the new rules, set `AnomalyRules = Latest` -- or
the new number -- and they apply to every galaxy on this install, old and new alike. That is the
one thing to know: the setting is per install, not per galaxy. If you keep it at `1` and start a
brand-new game after 1.0, that game rolls the old rules too, until you change it. The log says
which rules are in force on every load.

Two honest notes. The setting lives in your config on your machine, not in the save, so it does
not travel: someone you hand a save to will see that galaxy under whatever their own config says.
And it covers the *rules*, not the recipe list -- if 1.0 adds recipes to the pool, a few planets
shift the way a handful did when refining and particle recipes joined in 0.2. Everything else
stays put.
## What's new in 0.5

**Finish a technology and the galaxy tells you where it is already cheap.** Research Antimatter
Capsule, and the mod mentions that Iclarkrav I -- a world you scanned twenty hours ago and forgot --
makes them ten at a time.

*Why:* 0.4 hid anomalies until you researched their recipe, and hiding on its own is a subtraction.
This is what makes the trade honest: the mod stays quiet while a recipe means nothing to you, and
speaks at the moment it starts to. That moment is a better one than discovery, because it is when
you can act.

Only worlds you have already scanned are named. If none of them has it but the galaxy does, you are
told that much and no more: *Antimatter Capsule ×10 exists on a world you have not found.* Knowing
something is out there is a reason to go looking; where it is stays the part worth earning.

One consequence, since it is easy to miss: this makes silence meaningful. If a technology completes
and the mod says nothing, your galaxy has no anomaly on that recipe anywhere. About one recipe in
three is absent from any given galaxy, so that will happen often, and it is worth knowing rather
than searching for something that was never there.

`AnnounceOnResearch = false` turns all of it off.

**An anomaly you cannot use yet can now show a marker instead of vanishing.** `UnresearchedAnomalies
= Marker` puts the symbol on the planet without naming the recipe: you know something is there and
worth coming back for, but not yet what.

*Why:* 0.4 hid anomalies until you researched their recipe, which removed a lot of noise and one
useful thing along with it. In the first galaxy to run it, 31 anomalies went dark -- and they were
overwhelmingly military, because that is where DSP's tech tree puts the research you have not done.
A player fighting to hold a distant world could not see that the galaxy contained
`Superalloy Ammo Box ×10` three jumps away. Hiding the name is right; hiding the *place* removed a
reason to explore.

Existence turns out to be cheap information. The name is the part that means nothing before you can
build it.

`Hide` remains the default and is the 0.4 behaviour. `Show` names everything, as in 0.3.
If you set `HideUnresearchedAnomalies = false` in 0.4, it is migrated to `Show` and reported in
the log rather than quietly ignored.

## Previously, in 0.4

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

That is the whole discovery mechanic -- no hunting, no guessing. A machine actually running an
anomalous recipe also marks itself in its own window, so you are never left wondering why a number
looks wrong.

**What you see depends on whether you have researched the recipe.** Knowing a planet means knowing
the anomalies you can act on. An anomaly on a recipe you have researched is shown in full, as above.
One on a recipe you have *not* researched yet is handled by `UnresearchedAnomalies`:

- `Hide` (the default): nothing at all. The planet reads as ordinary until the research lands,
  so the map fills in as the game opens up rather than naming things you cannot build.
- `Marker`: the planet gets the bare symbol -- `Å` with no name -- its star counts it as `+1`
  after the names it can show, and the description tab says only *"On a recipe you have not
  researched yet."* You know something is there and worth coming back for, but not what. This is
  the mode for a fresh start: the map is full of places to remember long before you can read them.
- `Show`: everything, always, as the mod behaved before 0.4.

(Not to be confused with `StarmapLabel = Marker`, which is about *how much* every label says --
symbols and counts instead of names -- regardless of research. The two combine sensibly.)

**And when the research does land, the galaxy tells you.** Finish a technology and, if a world you
have already scanned makes one of its recipes ×10, you get a brief tip naming it. If only worlds
you have not found have it, you are told that much and no more. If the mod says nothing, your
galaxy has no anomaly on that recipe anywhere. `AnnounceOnResearch = false` turns this off.

Your **home planet never has an anomaly**. The starting world stays ordinary, deliberately: anomalies are a reason to look outward, and one at home would arrive before the star map exists to explain it. If you would rather start with something -- the early game is a long walk before the first anomaly is reachable -- set `HomePlanetNeverAnomalous = false` and home takes the same chance as anywhere else. Most of the time that still means nothing; occasionally it means a reason to stay a while.

## What this means in practice

An anomaly does nothing until you build the affected recipe on that planet. Most of them will sit unused — that is the point. The interesting decision is whether a particular world is worth reorganising production around, not whether you can be bothered to find it.

Anomalies are **not balanced**, on purpose. A ×10 on iron ingots is a convenience. A ×10 on something late-game and expensive is a windfall. Finding one of those should feel like a discovery, not like a reward that has been carefully measured out for you.

**And yes, ten is a lot.** It is a default, not a verdict. Using an anomaly means building somewhere
you did not choose, hauling the output home, and -- in a hostile galaxy -- clearing the Dark Fog off
a world and then holding it while it keeps trying to take your far-flung industry apart. That is a
real price, paid in belts, vessels, turrets, ammunition and attention, and the reward has to be
worth it. How big it needs to be for that is a matter of taste.

So the exact multiplier is yours. `OutputMultiplier` takes whatever makes the trade interesting to
you: some players run three, the default is ten, and nothing else in the mod cares which. There is
also an experimental setting, below, meant to scale the reward to how hostile your galaxy is -- today
it simply uses a smaller number when the Dark Fog is off or passive, so a peaceful game does not get
paid for a war it never has to fight.

Even without the fog, the trade is not free. A peaceful galaxy still makes you wrestle with
distance, logistics, and what the anomalous world actually has to offer -- and, most of all, with
building there without the layouts and blueprints you already know, which is the part most players
find hardest. The war is the largest cost. It is not the only one.

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
| `HomePlanetNeverAnomalous` | `true` | Keeps the world you start on ordinary. On by default: an anomaly at home arrives before the star map exists to explain it, and anomalies are meant to send you outward. Turn it off and home is drawn like anywhere else -- usually nothing, occasionally a reason to stay a while. Affects only the home planet. |
| `AnomalyChancePercent` | `Seed` | How many non-home planets are anomalous. `Seed` derives it from the galaxy seed, between 25% and 75%, so galaxies differ from one another. A number from 0 to 100 forces that density instead -- useful when a galaxy rolls sparser than you want to play, and free on a galaxy nobody has built on. Changing it re-rolls which planets are anomalous, not which recipe each one carries. |
| `OutputMultiplier` | `10` | How much more an anomalous recipe produces. |
| `StarmapLabel` | `Detail` | What star map labels show. `Detail` names the affected items, `Marker` shows counts and a symbol, `Off` hides them. Unscanned planets show nothing either way. |
| `AnomalyRules` | `1` | The version of the anomaly rules this install uses: a number, or `Latest`. Written once and never changed by the mod, so an upgraded install keeps rolling its galaxies as before when 1.0 introduces version 2. Set `Latest` or the new number to take the new rules, for every galaxy on this install. See *Before 1.0* above. |
| `ExcludedRecipes` | empty | Recipes that should never receive an anomaly, comma separated — by item name as it appears in game, or numeric id. The mod holds no opinion about which anomalies are worth having, because that depends entirely on how you play; this is where you state yours. Entries matching nothing are reported in the log rather than ignored. |
| `AnnounceOnResearch` | `true` | When a technology completes, names any world you have already scanned that makes one of its recipes ten times over. Shown as the game's own brief tip, and always written to the log. Never names a planet you have not visited. |
| `UnresearchedAnomalies` | `Hide` | What an anomaly says about itself before you have researched its recipe, everywhere it would appear. `Hide` says nothing. `Marker` shows the symbol without the name — somewhere to come back to. `Show` names everything, as in 0.3. Display only: generation is unchanged. |
| `LogEveryAnomaly` | `false` | Writes every anomaly in the galaxy to the log, including planets you have never scanned. Spoils discovery on purpose; for troubleshooting. |

Changes take effect when a save is next loaded.

### Experimental

Off by default, and may change or go away.

| Setting | Default | What it does |
| --- | --- | --- |
| `MultiplierFromCombatSettings` | `false` | When on, a galaxy with the Dark Fog disabled or set to passive uses `PeacefulOutputMultiplier` instead of `OutputMultiplier`. Hostile galaxies are unchanged. The multiplier is a return on the cost of using an anomaly, and the largest cost is holding a world against the fog; without that cost the same ×10 is a giveaway. Resolved once per save load, and the choice is written to the log. |
| `PeacefulOutputMultiplier` | `3` | The multiplier a peaceful galaxy gets when the setting above is on. 3 is what players who found ×10 too high have settled on. |

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

## What is (tentatively) coming in 1.0

Tentative, and in no promised order. What these have in common is that every one of them changes
how anomalies are drawn, which is why they are waiting for each other: they ship together as a
single change to the rules, and your existing galaxies keep the old rules unless you opt in. See
*Before 1.0* above.

**No duplicate anomalies within a system.** Three sorter worlds around one star dilute what an
anomaly means. Within a system, each recipe would appear at most once. Galaxy-wide uniqueness is
being resisted on purpose -- if one Crystal Silicon world exists and it is twenty light years out
through hostile space, you have no choice; two or three is a decision about which one you can hold.

**Varied multipliers.** Not every anomaly at the same number. Most modest, a few absurd, so that
occasionally you find a world that genuinely changes your plans rather than mildly improving them.

**Research cubes.** Matrices are not eligible today. They are the one thing a player would restructure
a galaxy around, and they are consumed forever rather than built once -- so a cube world is a
permanent commitment, which is exactly the trade this mod is about. It waits for varied multipliers,
because a flat ×10 on Universe Matrix is a different order of thing from ×10 on iron.

**Recipes with a by-product.** Antimatter and plasma refining are excluded only because hydrogen
falls out alongside. The single-output rule is standing in for "one clear product", and those two
are where it comes apart.

## Further out

**More kinds of anomaly.** Output multipliers are the simplest possible effect and the only one
implemented. The interesting ones are different in kind: a recipe that needs half as much of an
ingredient, one that runs faster, one that swaps an ingredient for something cheaper, one with an
unexpected by-product. A galaxy where every anomaly is "more stuff" is thinner than one where
planets are strange in different ways.

**Softer descriptions.** The panel tells you exactly what an anomaly does. There is an argument for
saying only *what* is affected -- "improved sorter output" -- and letting you find out how much by
building it. Existence stays free; magnitude becomes something you discover.

**A page for your galaxy.** A single file listing every anomaly you have found, grouped by system,
to keep on a second monitor or hand to someone playing the same seed. Only what you have
discovered -- a field notebook, not the answer key.

**A considered answer to proliferator**, rather than the current "does not crash".

## Feedback, ideas, bugs -- all welcome

Every change since 0.1 came from someone playing it and saying what happened -- the star map
labels, the recipe-name fix, the hiding, the markers, the research announcement. None of them were
on a list beforehand. Bug reports, ideas, disagreements with anything above, a galaxy that
produced something memorable, a setting you changed and why -- I would like to hear all of it:

- GitHub issues: https://github.com/pwelty/planetary-anomalies/issues
- Email: ponch@paulwelty.com

Even "I installed it and never used an anomaly" is useful. Especially that one.
