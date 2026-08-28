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
    /// It started as a bare count, on the theory that at galaxy scale the useful question is "is
    /// there anything here". Play disagreed: a count tells you a system has anomalies but not
    /// whether it is worth the trip, and remembering which system held what is exactly the thing
    /// players forget. So Detail lists the affected items, capped at MaxNamed with a "+N" tail, and
    /// Marker keeps the count for when the galaxy view gets crowded.
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
        /// <summary>
        /// How many item names a star label will list before falling back to "+N". A system with
        /// several anomalies would otherwise produce a label long enough to collide with its
        /// neighbours in the galaxy view.
        /// </summary>
        private const int MaxNamed = 3;

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

                StarmapLabelMode mode = Plugin.StarmapLabel != null
                    ? Plugin.StarmapLabel.Value
                    : StarmapLabelMode.Detail;

                if (mode == StarmapLabelMode.Off)
                {
                    return;
                }

                int anomalous = 0;
                string names = "";
                int named = 0;

                for (int i = 0; i < star.planets.Length; i++)
                {
                    PlanetData planet = star.planets[i];
                    if (planet == null || !planet.scanned)
                    {
                        continue;
                    }

                    if (AnomalyManager.AnomalyFor(planet.id) == null)
                    {
                        continue;
                    }

                    anomalous++;

                    if (named < MaxNamed)
                    {
                        string item = AnomalyManager.AnomalousItemName(planet.id);
                        if (!string.IsNullOrEmpty(item))
                        {
                            names += (named > 0 ? ", " : "") + item;
                            named++;
                        }
                    }
                }

                if (anomalous == 0)
                {
                    return;
                }

                // A bare count says something is here but not whether it is worth the trip, which
                // in play turned out to be the more common question -- you remember that a system
                // has anomalies and forget which. So Detail names them; Marker keeps the count for
                // when the galaxy view gets crowded.
                //
                // The word "anomaly" always appears, per PRODUCT.md, so anything the mod says is
                // recognisable as the mod's doing rather than the game's.
                string body;
                if (mode == StarmapLabelMode.Detail && named > 0)
                {
                    body = "ANOMALY: " + names;
                    if (anomalous > named)
                    {
                        body += " +" + (anomalous - named);
                    }
                }
                else
                {
                    body = anomalous == 1 ? "ANOMALY" : anomalous + " ANOMALIES";
                }

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
