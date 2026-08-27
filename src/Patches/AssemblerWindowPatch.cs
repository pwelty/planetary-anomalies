using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine.UI;

namespace PlanetaryAnomalies
{
    /// <summary>
    /// Marks the assembler window when the machine in front of the player is running its planet's
    /// anomalous recipe.
    ///
    /// Without this the panel actively contradicts the machine: it reads the recipe out of
    /// <c>RecipeProto</c>, which this mod deliberately never touches, so it shows the vanilla
    /// output while the machine visibly produces ten times it. That is worse than simply omitting
    /// the information.
    ///
    /// The marker is appended to <c>stateText</c> -- the "Working" / "Lack of ingredients" line --
    /// rather than correcting the displayed recipe. Correcting the recipe numbers would blur the
    /// line the design rests on: the prototype really is unchanged, and the anomaly really is a
    /// property of the planet rather than of this machine. A short note says so without pretending
    /// otherwise. The planet panel remains the place with the detail.
    /// </summary>
    [HarmonyPatch(typeof(UIAssemblerWindow), "_OnUpdate")]
    internal static class UIAssemblerWindowPatch
    {
        // UI code runs every frame. If something here throws, log it once rather than once per
        // frame for as long as the window is open.
        private static bool _errorLogged;

        // _assemblerId is private. AccessTools.FieldRef would be the fast way, but its delegate
        // returns by reference, which the C# 5 compiler this project builds with cannot express --
        // so plain reflection, with the FieldInfo cached since this runs every frame.
        private static FieldInfo _assemblerIdField;

        [HarmonyPostfix]
        internal static void Postfix(UIAssemblerWindow __instance)
        {
            try
            {
                Text state = __instance.stateText;
                if (state == null)
                {
                    return;
                }

                PlanetFactory factory = __instance.factory;
                if (factory == null || factory.planet == null)
                {
                    return;
                }

                PlanetAnomaly anomaly = AnomalyManager.AnomalyFor(factory.planet.id);
                if (anomaly == null)
                {
                    return;
                }

                FactorySystem system = __instance.factorySystem;
                if (system == null || system.assemblerPool == null)
                {
                    return;
                }

                if (_assemblerIdField == null)
                {
                    _assemblerIdField = AccessTools.Field(typeof(UIAssemblerWindow), "_assemblerId");
                    if (_assemblerIdField == null)
                    {
                        return;
                    }
                }

                int id = (int)_assemblerIdField.GetValue(__instance);
                if (id <= 0 || id >= system.assemblerPool.Length)
                {
                    return;
                }

                // Live pool slots store their own index back into id; recycled ones do not.
                if (system.assemblerPool[id].id != id ||
                    system.assemblerPool[id].recipeId != anomaly.RecipeId)
                {
                    return;
                }

                string marker = "   ·   ANOMALY ×" + anomaly.OutputMultiplier;

                // The game rewrites stateText on every update, in every branch, so appending does
                // not normally accumulate. This guards the case where some path returns without
                // rewriting it and last frame's text is still in place.
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
                    Plugin.Log.LogError("Failed to mark the assembler window: " + e);
                }
            }
        }
    }
}
