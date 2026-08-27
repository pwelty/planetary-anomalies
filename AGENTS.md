# Resident agent instructions

## Aim

Work only toward the current milestone in `SPIKE.md`. `SPEC.md` describes later intent; it is not permission to build ahead.

## Hard boundaries

- Dolphin's installed DSP assemblies are the source of truth. Inspect before using method names, signatures, local-variable positions, recipe IDs, or offsets.
- Never commit game DLLs, BepInEx binaries, saves, Steam files, generated decompilation trees, credentials, or user paths.
- Never permanently mutate global `RecipeProto` data to simulate local output.
- Begin with the hard-coded ×10 smelter proof. No planet logic, random anomaly, UI, persistence, CommonAPI, GalacticScale, or framework generalization.
- Use a copied/new test save. Never claim manual game acceptance from compilation or unit tests.
- Prefer a small Harmony prefix/postfix/wrapper. Use a transpiler only when inspection proves no safer seam.
- Keep build/install steps explicit for a developer new to C# modding.

## Workflow

1. Read `README.md`, `PRODUCT.md`, `SPIKE.md`, and only then `SPEC.md`.
2. Inspect current repository state and public precedent.
3. Produce the exact Dolphin assembly-access requirement if the assemblies are unavailable; do not code against guesses.
4. When assemblies are available, record game version, file hashes, and relevant signatures in `docs/inspection.md`.
5. Implement the smallest testable patch.
6. Add build/install scripts and focused automated checks where meaningful.
7. Stop for Paul's manual in-game test when the artifact is ready.

Commit coherent progress and keep the working tree clean. Do not deploy, publish to Thunderstore, or create a release without explicit approval.
