# Installed game inspection

Everything here was read from Dolphin's installed Dyson Sphere Program on 2026-08-27.
No signature below is taken from a public mod or from an older DSP build.

## Method

`Assembly-CSharp.dll` was read with `Mono.Cecil` — the copy that already ships inside the
installed BepInEx (`BepInEx/core/Mono.Cecil.dll`), loaded from PowerShell. No decompiler,
SDK, or extra tooling was installed to produce this document. The scripts used are throwaway
inspection scripts, not part of the build.

Types are reported exactly as Cecil sees them, including whether each field is public,
static, and a value type — because the patch depends on all three.

## Build under inspection

| Item | Value |
| --- | --- |
| Game | Dyson Sphere Program, Steam app `1366540` |
| Install path | `C:\Program Files (x86)\Steam\steamapps\common\Dyson Sphere Program` |
| Game version | `0.10.34` (string embedded in `DSPGAME_Data/globalgamemanagers`) |
| Steam build id | `23109513` |
| Unity | `2022.3.62f3c1` (`DSPGAME.exe` file version `2022.3.62.1451004`) |
| Managed assemblies | `DSPGAME_Data/Managed` |
| CLR runtime target | `Net_4_0` |

The exact four-part game version is logged at runtime by the plugin from
`GameConfig.gameVersion.ToFullString()` and `GameConfig.build`. Record it into this table on
the first successful in-game run — the embedded `0.10.34` string is the major/minor/release
only.

### Assembly hashes (SHA-256)

| File | Size | Modified (UTC) | SHA-256 |
| --- | --- | --- | --- |
| `Managed/Assembly-CSharp.dll` | 7830016 | 2026-05-06T16:23:21Z | `AE0BA95F75BD879A62AA4CE253B2AB78EAA4FB3C7C595F5E1FEE75EBE0E0EF85` |
| `Managed/UnityEngine.CoreModule.dll` | 1395024 | 2026-01-22T13:50:23Z | `E2B5AE2FD12646D03FC3D04D1A37D522572A3B97022FE1B95BBF2A2F2B04853A` |
| `Managed/UnityEngine.dll` | 126288 | 2026-01-22T13:50:23Z | `72CC73EEF0036530ABE21F82971FF06002CEE37EFFDD4DD7D5D4EC8DF3911F8D` |

`Assembly-CSharp.dll` and `globalgamemanagers` share a 2026-05-06 timestamp; the Unity
module assemblies are older because that update did not change the engine.

## Mod loader

BepInEx is **not** installed into the game directory. The game root holds a leftover
Doorstop pair (`winhttp.dll`, `doorstop_config.ini` pointing at `BepInEx\core\BepInEx.Preloader.dll`)
but there is no `BepInEx` folder there, so launching DSP straight from Steam currently loads
no plugins at all. Mods are managed by **Gale**, which keeps a full BepInEx tree per profile:

```
%APPDATA%\com.kesomannen.gale\dyson-sphere-program\profiles\<profile>\BepInEx\
```

| Item | Value |
| --- | --- |
| Profiles present | `Default` (empty), `gs run` (BepInEx + 11 mods) |
| BepInEx | `5.4.17.0` |
| `BepInEx.dll` | 116224 bytes, SHA-256 `DC1CB6B58B962BDA5AAA1D6B5F9AE14EC174F61836A1A1F96C1A040C7E8381F7` |
| `0Harmony.dll` | 189440 bytes, SHA-256 `7BD2BD6F87C1758047DEF40F2F0F024C877456CE7C01D68031358EE0C615D850` (HarmonyX, `HarmonyLib` namespace) |

The existing BepInEx log confirms the loader works on this build. It also shows CommonAPI
warning that it was built for build id `0.10.28.21308` while running `0.10.34.28529` — a
reminder that third-party mods here are behind the installed game, which is exactly why
their signatures are not treated as authority.

## The production path

### `AssemblerComponent` is a struct in a pooled array

```
AssemblerComponent   valuetype=True  sealed=True  public=True
```

It lives in `FactorySystem.assemblerPool` (`AssemblerComponent[]`). Because it is a value
type in an array, `pool[i].field = x` writes through to the stored element — no copy-back
dance is needed, and equally, copying an element to a local would silently discard writes.

Relevant fields, all public instance fields:

```
System.Int32        id
System.Int32        entityId
System.Int32        recipeId
ERecipeType         recipeType
RecipeExecuteData   recipeExecuteData
System.Int32[]      served
System.Int32[]      incServed
System.Int32[]      needs
System.Int32[]      produced
System.Int32        time, extraTime, cycleCount, extraCycleCount, speed, speedOverride
System.Boolean      replicating, forceAccMode, incUsed
```

### The one production method

```
System.UInt32 AssemblerComponent::InternalUpdate(
    System.Single power, System.Int32[] productRegister, System.Int32[] consumeRegister)
```

Note there is **no** `PlanetFactory` or planet argument. The executing machine cannot see its
own planet, so planet locality has to come from the caller side.

### Callers of `InternalUpdate` — there are two paths

```
FactorySystem::GameTick(System.Int64 time, System.Boolean isActive)                     [IL_05e4, IL_073c]
GameLogic::_assembler_parallel(System.Int32 threadOrdinal, System.Int32 workBatchSize,
                               System.Int32 extraProtectedSize, System.Int32 maxRedispatchChance)  [IL_023a, IL_034a]
```

`_assembler_parallel` is the multithreaded path; it reaches the same components via
`GameLogic.factories[...]` → `PlanetFactory.factorySystem.assemblerPool`. **Any patch placed on
only one of these two methods would be silently wrong for players on the other setting.**
This single fact drove the design below.

### Where output is actually added

From the `InternalUpdate` IL, the completed-cycle branch reads its output counts from the
component's own `recipeExecuteData`, not from `RecipeProto`:

```
IL_010d: ldarg.0
IL_010e: ldfld        AssemblerComponent::recipeExecuteData
IL_0113: ldfld        RecipeExecuteData::products          -> local V_8
IL_011a: ldarg.0
IL_011b: ldfld        AssemblerComponent::recipeExecuteData
IL_0120: ldfld        RecipeExecuteData::productCounts     -> local V_9
...
IL_0185: ldarg.0
IL_0186: ldfld        AssemblerComponent::produced
IL_018b: ldc.i4.0
IL_018c: ldelema      System.Int32
IL_0192: ldind.i4
IL_0193: ldloc.s      V_9            // productCounts[0]
IL_0197: add
IL_0198: stind.i4                    // produced[0] += productCounts[0]
...                                  // then the same amount into productRegister, under a lock
```

So the quantity added to the real output buffer — and to the production statistics register —
is `recipeExecuteData.productCounts[i]`. That is the value the anomaly must change.

The proliferator "extra products" branch at the top of the method (`extraTime >= extraTimeSpend`)
adds from the *same* `productCounts` array, so an anomaly applied there scales whatever DSP
would ordinarily produce rather than recreating DSP's own formula.

Output-buffer caps are also expressed in terms of `productCounts`, which is why multiplying it
does not deadlock the machine — the cap scales with it:

| `recipeType` | cap condition (single output) |
| --- | --- |
| `Smelt` (1) | `produced[0] + productCounts[0] > 100` → return |
| `Particle` (5, single-output branch) | `produced[0] > productCounts[0] * 9` → return |
| other | `produced[0] > productCounts[0] * 19` → return |

### `RecipeExecuteData` is shared, and that is the trap

```
RecipeExecuteData  valuetype=False (reference type)
  System.Int32[]  requires, requireCounts, products, productCounts
  System.Int32    timeSpend, extraTimeSpend
  System.Boolean  productive
  .ctor(int[] _requires, int[] _requireCounts, int[] _products, int[] _productCounts,
        int _timeSpend, int _extraTimeSpend, bool _productive)
```

The instances live in a **static** dictionary on `RecipeProto`:

```
public static System.Collections.Generic.Dictionary<System.Int32, RecipeExecuteData>
    RecipeProto::recipeExecuteData        // keyed by recipe ID, populated in RecipeProto::InitRecipeItems
```

and `SetRecipe` hands the assembler a reference straight out of it:

```
IL_0074: ldarg.0
IL_0075: ldsfld       RecipeProto::recipeExecuteData
IL_007a: ldloc.0                            // the RecipeProto
IL_007b: ldfld        Proto::ID
IL_0080: callvirt     Dictionary`2::get_Item
IL_0085: stfld        AssemblerComponent::recipeExecuteData
```

**Therefore: mutating `assembler.recipeExecuteData.productCounts` in place would change every
assembler on every planet running that recipe.** That is the galaxy-wide mutation the project
forbids. The field is a per-component *reference*, though — and that is the opening.

Every writer of `AssemblerComponent.recipeExecuteData`:

```
AssemblerComponent::SetRecipe   [IL_0085, IL_0124]   // player sets/changes recipe, blueprint paste
AssemblerComponent::SetEmpty    [IL_0064]
AssemblerComponent::Import      [IL_0405, IL_06bb]   // save load
```

All three assign the shared instance. `Export` writes only `recipeId`, so nothing anomalous is
ever written into a save file, and a loaded save always comes back with vanilla data.

### Reaching the planet from the tick

`FactorySystem` holds the planet directly — no navigation needed:

```
PlanetData      FactorySystem::planet          // public instance field
PlanetFactory   FactorySystem::factory
AssemblerComponent[]  FactorySystem::assemblerPool
System.Int32          FactorySystem::assemblerCursor
System.Int32          PlanetData::id
System.String         PlanetData::displayName   // property
```

And `FactorySystem.GameTick(long, bool)` has exactly one caller, which runs it for **every**
factory, every tick, with no multithreading guard:

```
GameLogic::FactorySystemFacilityGameTick
    for (i = 0; i < factoryCount; i++)
        factories[i].factorySystem.GameTick(timei, factories[i] == localLoadedFactory)
```

That makes it a dependable per-planet, per-tick hook regardless of the multithreading setting.

### Identifying the home planet

```
System.Int32  GalaxyData::birthPlanetId     // public instance field
System.Int32  GalaxyData::birthStarId
System.Int32  GalaxyData::seed
PlanetData    GalaxyData::PlanetById(System.Int32)
```

reached as `GameMain.data.galaxy.birthPlanetId`. `GameMain.data` is a public static field, and
`GameData.gameDesc.galaxySeed` distinguishes one galaxy from another.

### Recipe lookup

```
RecipeProtoSet LDB::recipes            // RecipeProtoSet : ProtoSet<RecipeProto>
ProtoSet<T>:  T[] dataArray;  T Select(Int32);  Boolean Exist(Int32)
Proto:        System.Int32 ID;  System.String Name;  string name { get; }   // name is the localized one
RecipeProto:  ERecipeType Type;  Int32[] Items, ItemCounts, Results, ResultCounts
ERecipeType:  None=0 Smelt=1 Chemical=2 Refine=3 Assemble=4 Particle=5 Exchange=6
              PhotonStore=7 Fractionate=8 Research=15
```

Recipe *IDs* are not statically inspectable: DSP ships its proto database inside Unity assets
(`Configs/` holds only an empty `path.txt`; `Wiki/` holds tutorial prose). So the recipe cannot
be pinned from disk the way the signatures above can. The plugin therefore hard-codes the
expected id and **verifies its shape at runtime** before using it, logging the recipe and item
names it actually resolved. See "Recipe selection" in the README.

## Chosen seam

**Give the anomalous machines a private `RecipeExecuteData` instead of patching production.**

Once per galaxy the plugin builds one new `RecipeExecuteData` — a copy of the shared one for
the target recipe, with every array copied and `productCounts` multiplied by 10. A Harmony
**prefix on `FactorySystem.GameTick(long, bool)`** checks `planet.id` against the home planet
and, for matching assemblers, swaps that private instance into `pool[i].recipeExecuteData`.

Why this seam and not a patch on `InternalUpdate`:

- **It covers both execution paths.** The anomaly is data hanging off the component, so it
  applies whether `FactorySystem.GameTick` or `GameLogic._assembler_parallel` runs the machine.
  A prefix/postfix on `InternalUpdate` would have to be duplicated and, worse, could not see
  the planet from inside the call.
- **Nothing global is mutated.** The shared dictionary and every array in it are untouched;
  only one component field — a reference — is reassigned, and only on the home planet.
- **No transpiler.** `AGENTS.md` allows one only when inspection proves no safer seam exists.
  Inspection proved the opposite.
- **Nothing hot is patched.** `InternalUpdate` runs per machine per tick; the patched method
  runs per planet per tick.
- **Statistics stay consistent for free**, because `productRegister` is fed from the same
  `productCounts` the buffer is.
- **It cannot leak into a save.** `Export` persists only `recipeId`, and `Import` reassigns the
  shared instance, so a save written with the mod loads vanilla without it.

Known costs, accepted for Stage 0 and recorded rather than hidden:

- The prefix rescans the home planet's assembler pool every tick. It is `O(assemblerCursor)`
  with two integer comparisons per slot and no allocation, which is negligible at spike scale
  but is not what a finished mod should do. A later stage should react to `SetRecipe`/`Import`
  instead of sweeping.
- The swap is a reference assignment performed on the main thread during the facility tick,
  while the parallel assembler work is a separate phase of the same tick. Reference assignment
  is atomic, and the object is fully constructed before publication, so the worst case is a
  machine running vanilla output for one extra tick after its recipe changes.
- Anything else that reassigns `recipeExecuteData` (another mod, a recipe change, a save load)
  simply reverts that machine until the next tick re-applies the swap.
