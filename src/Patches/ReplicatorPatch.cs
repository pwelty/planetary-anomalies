using System;
using HarmonyLib;

namespace PlanetaryAnomalies
{
    /// <summary>
    /// With ReplicatorFollowsPlanet on, Icarus's replicator obeys the anomaly of the world Icarus is
    /// standing on: hand-crafting that world's anomalous recipe there makes the same multiple its
    /// machines do.
    ///
    /// Long on the roadmap as a question, with a real objection: hand-crafting skips the factory, which
    /// is the part the mod exists to relocate. Paul's call for 1.0 was to make it a setting and test it
    /// in play -- an anomaly is a property of the place, and a replicator standing in that place and
    /// ignoring it is an inconsistency a player notices.
    ///
    /// The seam: MechaForge.AddTaskIterate builds every replicator job, and calls itself for the
    /// intermediates a job needs, returning each ForgeTask it creates. ForgeTask is a class with its
    /// own productCounts array, copied from the recipe, so multiplying it here changes this one job and
    /// nothing shared -- and it is multiplied when the job is created, not when it is delivered, so the
    /// queue shows the real count rather than promising one and handing over ten.
    ///
    /// Two consequences to know. A job keeps its multiple if Icarus flies away before it finishes,
    /// because the place that counts is where it was queued. And the game saves queued jobs, so a job
    /// still in the queue at save time is saved with its multiple -- a small exception to "nothing is
    /// written to your saves", and it disappears when the job completes.
    /// </summary>
    internal static class ReplicatorPatch
    {
        private static bool _errorLogged;
        private static bool _firstLogged;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MechaForge), "AddTaskIterate")]
        internal static void AfterAddTaskIterate(ForgeTask __result)
        {
            try
            {
                if (__result == null || __result.productCounts == null)
                {
                    return;
                }

                if (Plugin.ReplicatorFollowsPlanet == null || !Plugin.ReplicatorFollowsPlanet.Value)
                {
                    return;
                }

                PlanetData planet = GameMain.localPlanet;
                if (planet == null)
                {
                    return;
                }

                PlanetAnomaly anomaly = AnomalyManager.AnomalyFor(planet.id);
                if (anomaly == null || anomaly.RecipeId != __result.recipeId)
                {
                    return;
                }

                for (int i = 0; i < __result.productCounts.Length; i++)
                {
                    __result.productCounts[i] *= anomaly.OutputMultiplier;
                }

                if (!_firstLogged)
                {
                    _firstLogged = true;
                    Plugin.Log.LogInfo("Replicator follows the planet (once): recipe " + __result.recipeId + " on " +
                                       planet.displayName + " makes x" + anomaly.OutputMultiplier + " per craft, " +
                                       __result.count + " craft(s) queued.");
                }
            }
            catch (Exception e)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    Plugin.Log.LogError("Failed to apply an anomaly to the replicator: " + e);
                }
            }
        }
    }
}
