using System;
using HarmonyLib;
using UnityEngine.UI;

namespace PlanetaryAnomalies
{
    /// <summary>
    /// Marks anomalous planets in the star map, on the floating label, so a galaxy can be read
    /// without selecting planets one at a time.
    ///
    /// Three players raised this independently on release day: the star map shows nothing about
    /// what has been scanned, and exploration ends abruptly. Disclosure previously required
    /// selecting a planet and opening its description tab, which meant the screen people actually
    /// explore from said nothing.
    ///
    /// Deliberately a marker and not the recipe. A galaxy view carrying dozens of recipe names
    /// would be noise; the detail stays one click away in the description tab.
    ///
    /// Gated on <c>PlanetData.scanned</c>, exactly like the planet panel. An unscanned planet must
    /// reveal nothing at all -- otherwise the marker gives away the existence of something the
    /// discovery rule says the player has to earn, which would be worse than showing nothing.
    ///
    /// Hooked at <c>_OnInit</c> rather than <c>_OnUpdate</c>: the label's text is assigned there
    /// (and in <c>OnPlanetDisplayNameChange</c>), so an appended marker persists and costs nothing
    /// per frame. That is safe because <c>UIStarmapPlanet.planet</c> is written only in
    /// <c>_OnInit</c> and <c>_OnFree</c> -- a label is never rebound to a different planet without
    /// <c>_OnInit</c> running again, so a marker cannot survive onto the wrong world.
    /// </summary>
    internal static class UIStarmapPlanetPatch
    {
        private static bool _errorLogged;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIStarmapPlanet), "_OnInit")]
        internal static void AfterInit(UIStarmapPlanet __instance)
        {
            Apply(__instance);
        }

        /// <summary>
        /// The game rewrites the label when a planet is renamed, which would drop the marker.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIStarmapPlanet), "OnPlanetDisplayNameChange")]
        internal static void AfterRename(UIStarmapPlanet __instance)
        {
            Apply(__instance);
        }

        private static void Apply(UIStarmapPlanet instance)
        {
            try
            {
                if (instance == null)
                {
                    return;
                }

                PlanetData planet = instance.planet;
                Text label = instance.nameText;
                if (planet == null || label == null)
                {
                    return;
                }

                // The discovery gate. Nothing at all for a planet the player has not learned about.
                if (!planet.scanned)
                {
                    return;
                }

                if (AnomalyManager.AnomalyFor(planet.id) == null)
                {
                    return;
                }

                // Guards the rename path, and any future call that re-applies without the game
                // having rewritten the label first.
                if (label.text != null && label.text.EndsWith(Marker, StringComparison.Ordinal))
                {
                    return;
                }

                label.text = label.text + Marker;
            }
            catch (Exception e)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    Plugin.Log.LogError("Failed to mark a star map planet: " + e);
                }
            }
        }

        // Rich text: DSP's own UI uses <color> tags in these labels, so this renders rather than
        // showing as literal markup. The wording follows PRODUCT.md -- always the word "anomaly".
        private const string Marker = "  <color=#FFC454>ANOMALY</color>";
    }
}
