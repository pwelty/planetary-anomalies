using System;
using HarmonyLib;
using UnityEngine.UI;

namespace PlanetaryAnomalies
{
    /// <summary>
    /// Shows the storm under the small globe in the top-right corner while it is over the planet the
    /// player stands on: "STORM: Gear ×10, 23 min". That globe is always on screen, so the storm can
    /// be seen without opening anything -- the planet panel only refreshes when it is opened.
    ///
    /// The line goes on <c>geoInfoText</c>, the climate line ("Tropics", "Summer hemisphere"), which
    /// <c>UIPlanetGlobe._OnUpdate</c> writes on a planet. Whatever follows the marker is cut before
    /// appending, so a frame the game skips cannot stack lines or keep a stale minute count.
    /// </summary>
    [HarmonyPatch(typeof(UIPlanetGlobe), "_OnUpdate")]
    internal static class UIPlanetGlobePatch
    {
        private const string Marker = "\nSTORM: ";

        private static bool _errorLogged;

        [HarmonyPostfix]
        internal static void Postfix(UIPlanetGlobe __instance)
        {
            try
            {
                Text info = __instance.geoInfoText;
                if (info == null)
                {
                    return;
                }

                string text = info.text ?? "";
                int at = text.IndexOf(Marker, StringComparison.Ordinal);
                string bare = at >= 0 ? text.Substring(0, at) : text;

                PlanetData planet = GameMain.localPlanet;
                string line = planet != null ? AnomalyManager.StormGlobeLine(planet.id) : null;
                string wanted = line != null ? bare + Marker + line : bare;
                if (!string.Equals(wanted, text, StringComparison.Ordinal))
                {
                    info.text = wanted;
                }
            }
            catch (Exception e)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    Plugin.Log.LogError("Corner globe storm line failed (logged once): " + e);
                }
            }
        }
    }
}
