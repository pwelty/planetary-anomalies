using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace PlanetaryAnomalies
{
    /// <summary>
    /// Discloses a planet's anomaly in the star map's planet detail panel, under the existing
    /// "planet description" tab.
    ///
    /// The discovery rule this implements is in PRODUCT.md: knowing the planet means knowing its
    /// anomaly. The gate is DSP's own <c>PlanetData.scanned</c> flag rather than anything this mod
    /// invents -- it is set by landing or by scanning from the star map, it is persisted by the
    /// game through GalaxyData.Export/ImportScannedDatas, and UIPlanetDetail already re-runs
    /// OnPlanetDataSet when it flips. An unscanned planet therefore reveals nothing, which is what
    /// keeps unexplored space worth exploring.
    ///
    /// Appending is safe against duplication because the game rewrites planetBrief from scratch on
    /// every OnPlanetDataSet call (it even re-picks the flavour text at random), so our line never
    /// accumulates across refreshes.
    /// </summary>
    [HarmonyPatch(typeof(UIPlanetDetail), "OnPlanetDataSet")]
    internal static class UIPlanetDetailPatch
    {
        // UI code runs every frame a panel is open. If something here throws, log it once rather
        // than filling the log with one copy per frame.
        private static bool _errorLogged;

        [HarmonyPostfix]
        internal static void Postfix(UIPlanetDetail __instance)
        {
            try
            {
                PlanetData planet = __instance.planet;
                if (planet == null || !planet.scanned)
                {
                    return;
                }

                Text brief = __instance.planetBrief;
                if (brief == null)
                {
                    return;
                }

                AnomalyVisibility visibility = AnomalyManager.VisibilityFor(planet.id);
                if (visibility == AnomalyVisibility.None)
                {
                    return;
                }

                // The panel has room to be a sentence rather than a symbol, so Marker mode says
                // plainly what it is withholding and why. "Something is here, and you cannot use it
                // yet" is a reason to come back; a bare mark in a paragraph of text is a puzzle.
                string description = visibility == AnomalyVisibility.Full
                    ? AnomalyManager.DescribeForPlanet(planet.id)
                    : "ANOMALY\nOn a recipe you have not researched yet.";
                if (string.IsNullOrEmpty(description))
                {
                    return;
                }

                brief.text = brief.text + "\n\n" + description;

                // The game sizes the brief's container from Text.preferredHeight immediately after
                // setting the text:
                //
                //     briefContentRect.sizeDelta =
                //         new Vector2(round(sizeDelta.x), round(planetBrief.preferredHeight))
                //
                // That measurement predates our appended line, so without redoing it the added
                // text is drawn into a box too short to hold it. Same computation, run again.
                RectTransform content = __instance.briefContentRect;
                if (content != null)
                {
                    content.sizeDelta = new Vector2(
                        Mathf.Round(content.sizeDelta.x),
                        Mathf.Round(brief.preferredHeight));
                }
            }
            catch (Exception e)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    Plugin.Log.LogError("Failed to show the anomaly in the planet panel: " + e);
                }
            }
        }
    }
}
