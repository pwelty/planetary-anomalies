# Resident agent instructions

## Aim

Maintain the accepted, published release and work only toward the milestone Paul explicitly selects.
`ROADMAP.md` records directions and sequencing; it is not blanket permission to build ahead.
`SPIKE.md` and `SPEC.md` are design history unless a current task points back to them.

## Hard boundaries

- Dolphin's installed DSP assemblies are the source of truth. Inspect before using method names, signatures, local-variable positions, recipe IDs, or offsets.
- Never commit game DLLs, BepInEx binaries, saves, Steam files, generated decompilation trees, credentials, or user paths.
- Never permanently mutate global `RecipeProto` data to simulate local output.
- Do not infer the next feature from the roadmap. The default after v0.1.0 is observation and bounded hardening until Paul selects another vertical slice.
- Do not build a generalized effect framework before multiple implemented effects demonstrate a shared contract.
- Use a copied/new test save. Never claim manual game acceptance from compilation or unit tests.
- Prefer a small Harmony prefix/postfix/wrapper. Use a transpiler only when inspection proves no safer seam.
- Keep build/install steps explicit for a developer new to C# modding.

## Workflow

1. Read `README.md`, `PRODUCT.md`, `ROADMAP.md`, and `LOG.md`. Read `SPIKE.md` and `SPEC.md` when historical rationale or a current task requires them.
2. Inspect current repository state and public precedent.
3. Produce the exact Dolphin assembly-access requirement if the assemblies are unavailable; do not code against guesses.
4. When assemblies are available, record game version, file hashes, and relevant signatures in `docs/inspection.md`.
5. Implement the smallest testable patch.
6. Add build/install scripts and focused automated checks where meaningful.
7. Stop for Paul's manual in-game test when the artifact is ready.
8. Append a session entry to `LOG.md` before finishing: what changed, what is actually proven,
   what is not, and where the next session should start. Durable facts about the game belong in
   `docs/inspection.md`, not in `LOG.md`.

Commit coherent progress and keep the working tree clean. Do not deploy, publish to Thunderstore, or create a release without explicit approval.
