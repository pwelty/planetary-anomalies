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
    /// there anything here". Play disagreed twice over.
    ///
    /// First: a count tells you a system has anomalies but not whether it is worth the trip, and
    /// which system held what is exactly the thing players forget. So Detail names them.
    ///
    /// Then the names were capped, with a "+2" tail for the remainder -- which reintroduced the
    /// original problem in miniature, since "+2" says there is more without saying what. Every
    /// anomalous planet in the system is now listed. A long label is a readable problem; a truncated
    /// one is a confusing one. Marker mode remains for anyone who wants the galaxy view quieter.
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
        /// The star map marker. A symbol rather than the word, because a star label sits beside
        /// every other star label and the word costs more room than it earns there.
        ///
        /// This is a deliberate exception to the "always say anomaly" rule in PRODUCT.md, which
        /// still holds everywhere with room for it -- the planet panel, the machine window, the log.
        /// A player meets the word first in those places, so the symbol reads as shorthand rather
        /// than as something unexplained.
        ///
        /// U+00C5 is Latin-1, so the UI font almost certainly carries it. More decorative glyphs
        /// risk rendering as an empty box, which would be worse than the word.
        /// </summary>
        private const string Symbol = "Å";

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

                    string item = AnomalyManager.AnomalousItemName(planet.id);
                    if (!string.IsNullOrEmpty(item))
                    {
                        names += (named > 0 ? ", " : "") + item;
                        named++;
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
                    body = Symbol + " " + names;
                }
                else
                {
                    body = anomalous == 1 ? Symbol : Symbol + " " + anomalous;
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
