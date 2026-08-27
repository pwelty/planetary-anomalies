using System;
using HarmonyLib;

namespace PlanetaryAnomalies
{
    /// <summary>
    /// Attaches the anomaly to the machines it applies to.
    ///
    /// This does not patch production. DSP adds a completed cycle's output from
    /// <c>AssemblerComponent.recipeExecuteData.productCounts</c>, and that field is a per-component
    /// reference into a static, shared dictionary. So instead of intercepting the output code --
    /// which runs on two different paths, single-threaded and parallel, neither of which can see
    /// the planet -- we hand the affected machines a private copy of that data with the counts
    /// already multiplied. Both execution paths then produce the anomalous amount on their own,
    /// and nothing shared is touched.
    ///
    /// <c>FactorySystem.GameTick</c> is the hook because it is the one place that runs once per
    /// planet per tick, knows its own planet, and is called for every factory regardless of the
    /// multithreading setting. See docs/inspection.md.
    /// </summary>
    [HarmonyPatch(typeof(FactorySystem), "GameTick", new Type[] { typeof(long), typeof(bool) })]
    internal static class FactorySystemGameTickPatch
    {
        [HarmonyPrefix]
        internal static void Prefix(FactorySystem __instance)
        {
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

            AssemblerComponent[] pool = __instance.assemblerPool;
            if (pool == null)
            {
                return;
            }

            int cursor = __instance.assemblerCursor;
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
