using HarmonyLib;

namespace PlanetaryAnomalies
{
    /// <summary>
    /// Attaches the anomaly to the machines it applies to.
    ///
    /// This does not patch production. DSP adds a completed cycle's output from
    /// <c>AssemblerComponent.recipeExecuteData.productCounts</c>, and that field is a per-component
    /// reference into a static, shared dictionary. So instead of intercepting the output code --
    /// which runs on two different paths, neither of which can see the planet -- we hand the
    /// affected machines a private copy of that data with the counts already multiplied. Both
    /// execution paths then produce the anomalous amount on their own, and nothing shared is
    /// touched.
    ///
    /// The hook is <c>PlanetFactory.BeforeGameTick</c>, called once per factory per tick from
    /// <c>GameLogic.FactoryBeforeGameTick</c>.
    ///
    /// Choosing this hook matters more than it looks. <c>GameLogic.OnGameLogicFrame</c> dispatches
    /// most factory work through paired methods -- a sequential one and a <c>_Parallel</c> one --
    /// and picks between them on thread count:
    ///
    ///     V_6 = threadCount &lt;= 1      -> calls FactorySystemFacilityGameTick
    ///     V_7 = threadCount &gt;= 2      -> calls FactorySystemFacilityGameTick_Parallel
    ///
    /// So a hook on <c>FactorySystem.GameTick</c> silently never fires on a multithreaded game,
    /// which is the default. <c>FactoryBeforeGameTick</c> has no <c>_Parallel</c> twin and is
    /// guarded only by "is this the main thread", so it runs in both modes. It also runs earlier
    /// in the frame than the facility phase, so the swap is in place before production for that
    /// tick. See docs/inspection.md.
    /// </summary>
    [HarmonyPatch(typeof(PlanetFactory), "BeforeGameTick")]
    internal static class PlanetFactoryBeforeGameTickPatch
    {
        // Proves the hook itself fires. Without this, "nothing happened" cannot be told apart from
        // "the patch never ran", which are very different bugs -- and were, once.
        private static bool _hookProven;

        [HarmonyPrefix]
        internal static void Prefix(PlanetFactory __instance)
        {
            if (!_hookProven)
            {
                _hookProven = true;
                PlanetData first = __instance.planet;
                Plugin.Log.LogInfo(
                    "PlanetFactory.BeforeGameTick prefix is running (first seen on " +
                    (first != null ? first.displayName + ", planet id " + first.id : "<no planet>") + ").");
            }

            PlanetAnomaly anomaly = AnomalyManager.Resolve();
            if (anomaly == null)
            {
                return;
            }

            // The planet guard. Stage 0 cannot fully witness the negative case yet -- early in a
            // new game the player is only ever on the home planet -- but the guard is live from
            // the first build, and this is where it fires.
            PlanetData planet = __instance.planet;
            if (planet == null || planet.id != anomaly.PlanetId)
            {
                return;
            }

            FactorySystem system = __instance.factorySystem;
            if (system == null)
            {
                return;
            }

            AssemblerComponent[] pool = system.assemblerPool;
            if (pool == null)
            {
                return;
            }

            int cursor = system.assemblerCursor;
            if (cursor > pool.Length)
            {
                cursor = pool.Length;
            }

            RecipeExecuteData anomalousData = anomaly.AnomalousExecuteData;
            int recipeId = anomaly.RecipeId;

            for (int i = 1; i < cursor; i++)
            {
                // DSP marks live pool slots by storing the index back into id; recycled slots do not.
                if (pool[i].id != i)
                {
                    continue;
                }

                if (pool[i].recipeId != recipeId)
                {
                    continue;
                }

                // Already ours. This is the case on almost every tick, so it is the cheap path.
                if (ReferenceEquals(pool[i].recipeExecuteData, anomalousData))
                {
                    continue;
                }

                // AssemblerComponent is a struct in an array, so this writes through to the pool.
                pool[i].recipeExecuteData = anomalousData;

                AnomalyManager.NoteApplied(planet, i);
            }
        }
    }
}
