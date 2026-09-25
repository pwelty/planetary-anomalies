using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine.UI;

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
        private static bool _queueErrorLogged;

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

        /// <summary>
        /// The queue's number, told the truth.
        ///
        /// The first test in play: ten engines delivered, and the queue said "1". The window does mean
        /// items -- UIReplicatorWindow.ActiveQueueText shows crafts times output -- but it reads the
        /// output from the global RecipeProto, which this mod never changes, not from the job. So after
        /// it writes the number, this rewrites it from the job's own productCounts: only for a job this
        /// mod multiplied, and keeping the brackets the game puts around a sub-job's count.
        ///
        /// taskQueue and queueNumTexts are private; Harmony reaches them by name, and verify.ps1 checks
        /// that the names are still there.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIReplicatorWindow), "ActiveQueueText")]
        internal static void AfterActiveQueueText(int index, List<ForgeTask> ___taskQueue, Text[] ___queueNumTexts)
        {
            try
            {
                if (___taskQueue == null || ___queueNumTexts == null || index < 0 ||
                    index >= ___taskQueue.Count || index >= ___queueNumTexts.Length || ___queueNumTexts[index] == null)
                {
                    return;
                }

                ForgeTask task = ___taskQueue[index];
                if (task == null || task.productCounts == null || task.productCounts.Length != 1)
                {
                    return;
                }

                RecipeProto recipe = LDB.recipes.Select(task.recipeId);
                if (recipe == null || recipe.ResultCounts == null || recipe.ResultCounts.Length != 1 ||
                    task.productCounts[0] == recipe.ResultCounts[0])
                {
                    return;
                }

                int shown = task.count * task.productCounts[0];
                ___queueNumTexts[index].text = task.parentTaskIndex < 0 ? shown.ToString() : "(" + shown + ")";
            }
            catch (Exception e)
            {
                if (!_queueErrorLogged)
                {
                    _queueErrorLogged = true;
                    Plugin.Log.LogError("Failed to show the replicator queue's multiplied count: " + e);
                }
            }
        }
    }
}
