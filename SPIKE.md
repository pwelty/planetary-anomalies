# Stage 0 — Ten plates from one smelter cycle

## Aim

Prove the smallest mechanical claim: a normal DSP smelter completes a vanilla recipe cycle but places ten units in its actual output buffer instead of one.

Use the simplest ordinary single-output smelting recipe available in the installed game—prefer the starting iron-ore → iron-ingot recipe (Paul may casually call these plates). Hard-code the recipe ID after inspecting the installed game data; do not rely on a historical numeric ID.

## Required order

1. Inspect Dolphin's installed game version and managed assemblies.
2. Record exact signatures/call paths for `AssemblerComponent.InternalUpdate`, its `FactorySystem.GameTick` caller, and any parallel assembler path in the current build.
3. Install/verify BepInEx and Harmony against that build.
4. Load a minimal plugin and emit a versioned startup log.
5. Patch the real production-output path for the one hard-coded smelter recipe.
6. Test in a copied/new save immediately after basic smelting is available.

## Acceptance

- DSP launches and BepInEx loads the plugin.
- The plugin logs its version and the hard-coded recipe selected.
- One normal recipe cycle consumes the normal input quantity.
- The smelter's real output storage gains 10 units rather than 1.
- Inserters can remove the output normally.
- The machine continues cycling without deadlock or duplication loops.
- Production statistics behavior is observed and recorded, but statistics mismatch does not block Stage 0.
- No `RecipeProto` or shared recipe definition is permanently modified.

## Out of scope

- Planet checks.
- Randomness.
- Save persistence.
- Proliferator correctness beyond “does not crash.”
- Other recipe or machine types.
- Multiplayer.
- UI.

## Stop condition

Once this works, stop and preserve the receipt: game version, assembly hashes, patch seam, BepInEx log, recipe, input/output observation, and known issues. Stage 1 is a separate change adding the home-planet guard.
