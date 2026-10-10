using System;
using HarmonyLib;
using UnityEngine.UI;

namespace PlanetaryAnomalies
{
    /// <summary>
    /// Marks the lab window when the lab in front of the player is making its planet's anomalous
    /// matrix -- the lab twin of <see cref="UIAssemblerWindowPatch"/>, for ResearchCubes.
    ///
    /// Same reasoning, same surface: the window reads the recipe out of <c>RecipeProto</c>, so it
    /// shows one matrix per cycle while the lab makes ten. The marker goes on <c>stateText</c>,
    /// which <c>_OnUpdate</c> rewrites in every produce-mode branch. Labs in research mode make no
    /// items, so they are never marked.
    ///
    /// Unlike the assembler window, <c>factory</c> and <c>factorySystem</c> are private here, so
    /// they come in through Harmony's field injection; <c>labId</c> has a public getter.
    /// </summary>
    [HarmonyPatch(typeof(UILabWindow), "_OnUpdate")]
    internal static class UILabWindowPatch
    {
        // UI code runs every frame; log a failure once, not once per frame.
        private static bool _errorLogged;

        [HarmonyPostfix]
        internal static void Postfix(UILabWindow __instance, PlanetFactory ___factory, FactorySystem ___factorySystem)
        {
            try
            {
                Text state = __instance.stateText;
                if (state == null || ___factory == null || ___factory.planet == null)
                {
                    return;
                }

                PlanetAnomaly anomaly = AnomalyManager.AnomalyFor(___factory.planet.id);
                if (anomaly == null)
                {
                    return;
                }

                if (___factorySystem == null || ___factorySystem.labPool == null)
                {
                    return;
                }

                int id = __instance.labId;
                LabComponent[] labs = ___factorySystem.labPool;
                if (id <= 0 || id >= labs.Length)
                {
                    return;
                }

                // Live pool slots store their own index back into id; recycled ones do not.
                if (labs[id].id != id || labs[id].researchMode || labs[id].recipeId != anomaly.RecipeId)
                {
                    return;
                }

                string marker = "   ·   ANOMALY ×" + anomaly.OutputMultiplier;

                // Guards a path that returns without rewriting stateText, leaving last frame's
                // marker in place.
                if (state.text != null && state.text.EndsWith(marker, StringComparison.Ordinal))
                {
                    return;
                }

                state.text = state.text + marker;
            }
            catch (Exception e)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    Plugin.Log.LogError("Failed to mark the lab window: " + e);
                }
            }
        }
    }
}
