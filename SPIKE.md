# Stage 0 — Ten plates, home planet only

## Aim

Prove the smallest claim that is actually about *planetary* anomalies: a normal DSP smelter completes a vanilla recipe cycle and places ten units in its real output buffer instead of one—but only on the player's home planet.

Use the simplest ordinary single-output smelting recipe available in the installed game—prefer the starting iron-ore → iron-ingot recipe (Paul may casually call these plates). Hard-code the recipe ID after inspecting the installed game data; do not rely on a historical numeric ID.

×10 is chosen because it is unmistakable at a glance in the machine's output slot. Nothing depends on the exact multiplier.

## The one honest limitation

Early in a new game the player is only ever on the home planet, so the negative case—normal output everywhere else—cannot be observed yet. The planet guard is still implemented and logged from the first build; it is simply not fully witnessed until travel is available. This is a limit on the test, not a reason to defer the guard.

## Required order

1. Inspect Dolphin's installed game version and managed assemblies.
2. Record exact signatures/call paths for `AssemblerComponent.InternalUpdate`, its `FactorySystem.GameTick` caller, and any parallel assembler path in the current build.
3. Record how the executing machine's planet is reached from that call path, and how the home/birth planet is identified.
4. Install/verify BepInEx and Harmony against that build.
5. Load a minimal plugin and emit a versioned startup log.
6. Patch the real production-output path for the one hard-coded smelter recipe, guarded to the home planet.
7. Test in a copied/new save immediately after basic smelting is available.

## Acceptance

- DSP launches and BepInEx loads the plugin.
- The plugin logs its version, the hard-coded recipe, and the identified home planet.
- One normal recipe cycle consumes the normal input quantity.
- On the home planet, the smelter's real output storage gains 10 units rather than 1.
- Inserters can remove the output normally.
- The machine continues cycling without deadlock or duplication loops.
- Production statistics behavior is observed and recorded, but statistics mismatch does not block Stage 0.
- No `RecipeProto` or shared recipe definition is permanently modified.

Deferred to the first off-planet test, once travel is available or a suitable save exists:

- The same recipe on any other planet produces its normal output.

## Out of scope

- Randomness.
- Save persistence.
- Proliferator correctness beyond "does not crash."
- Other recipe or machine types.
- Multiplayer.
- UI.

## Stop condition

Once this works, stop and preserve the receipt: game version, assembly hashes, patch seam, how the planet is identified, BepInEx log, recipe, input/output observation, and known issues. Random recipe selection is a separate change.
