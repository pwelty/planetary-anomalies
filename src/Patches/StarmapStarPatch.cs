using System;
using HarmonyLib;
using UnityEngine.UI;

namespace PlanetaryAnomalies
{
    /// <summary>
    /// Marks a star when any of its known planets is anomalous.
    ///
    /// The planet labels alone turned out to sit at the wrong altitude: the galaxy view shows
    /// stars, and planet names only appear once you have clicked into a system. So marking planets
    /// helps once you have arrived somewhere and does nothing for the decision the release thread
    /// was actually complaining about -- where to go next.
    ///
    /// A count rather than recipe names. At galaxy scale the useful question is "is there anything
    /// here", not "what exactly"; a view carrying a recipe name per planet would be unreadable, and
    /// the detail is one click away on the planets themselves.
    ///
    /// Gated on <c>PlanetData.scanned</c> per planet, like every other surface. A system the player
    /// knows nothing about stays blank, so this reports on what has been explored rather than
    /// pointing at what has not.
    ///
    /// Same hook shape as the planet label, and safe for the same reason:
    /// <c>UIStarmapStar.star</c> is written only in <c>_OnInit</c> and <c>_OnFree</c>, so a label
    /// is never rebound to a different star without <c>_OnInit</c> running again.
    /// </summary>
    internal static class UIStarmapStarPatch
    {
        private static bool _errorLogged;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIStarmapStar), "_OnInit")]
        internal static void AfterInit(UIStarmapStar __instance)
        {
            Apply(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIStarmapStar), "OnStarDisplayNameChange")]
        internal static void AfterRename(UIStarmapStar __instance)
        {
            Apply(__instance);
        }

        private static void Apply(UIStarmapStar instance)
        {
            try
            {
                if (instance == null)
                {
                    return;
                }

                Text label = instance.nameText;
                StarData star = instance.star;
                if (label == null || star == null || star.planets == null)
                {
                    return;
                }

                if (Plugin.StarmapLabel != null && Plugin.StarmapLabel.Value == StarmapLabelMode.Off)
                {
                    return;
                }

                int anomalous = 0;
                for (int i = 0; i < star.planets.Length; i++)
                {
                    PlanetData planet = star.planets[i];
                    if (planet == null || !planet.scanned)
                    {
                        continue;
                    }

                    if (AnomalyManager.AnomalyFor(planet.id) != null)
                    {
                        anomalous++;
                    }
                }

                if (anomalous == 0)
                {
                    return;
                }

                // "ANOMALY" / "3 ANOMALIES" -- the word is always present, per PRODUCT.md, so
                // anything the mod says is recognisable as the mod's doing.
                string body = anomalous == 1 ? "ANOMALY" : anomalous + " ANOMALIES";

                if (!label.supportRichText)
                {
                    label.supportRichText = true;
                }

                string suffix = "  <color=#FFC454>" + body + "</color>";

                if (label.text != null && label.text.EndsWith(suffix, StringComparison.Ordinal))
                {
                    return;
                }

                label.text = label.text + suffix;
            }
            catch (Exception e)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    Plugin.Log.LogError("Failed to mark a star map star: " + e);
                }
            }
        }
    }
}
