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
    /// What the label says is configurable. Naming the affected item is the point -- reading a
    /// galaxy for what it is good at beats reading it for where something merely exists -- but at
    /// high anomaly density a view full of recipe names may be noise, in which case a bare marker
    /// or nothing at all reads better. That is a judgement only play can settle, so it is a
    /// setting rather than a decision baked into the code.
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

        /// <summary>
        /// Scanning a planet and researching a recipe both change what this label should say, and
        /// neither rewrites the label. Without this the text is frozen at whatever was true when
        /// the star map was built. See <see cref="StarmapLabel"/>.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIStarmapPlanet), "_OnLateUpdate")]
        internal static void AfterLateUpdate(UIStarmapPlanet __instance)
        {
            if (StarmapLabel.DueForRefresh())
            {
                Apply(__instance);
            }
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

                StarmapLabelMode mode = Plugin.StarmapLabel != null
                    ? Plugin.StarmapLabel.Value
                    : StarmapLabelMode.Detail;

                // Every reason to say nothing runs through the same path as saying something, so
                // that a label which used to carry a marker loses it when the answer changes. An
                // early return here would leave stale text on screen -- which is exactly the bug
                // this shape exists to prevent.
                string body = null;

                // The two gates: has the player found this planet, and is its anomaly on something
                // they could actually build. An unresearched recipe names something they cannot
                // make and may not recognise.
                if (planet.scanned && mode != StarmapLabelMode.Off && AnomalyManager.IsDisclosed(planet.id))
                {
                    body = "Å";
                    if (mode == StarmapLabelMode.Detail)
                    {
                        string what = AnomalyManager.ShortDescribeForPlanet(planet.id);
                        if (!string.IsNullOrEmpty(what))
                        {
                            body = "Å " + what;
                        }
                    }
                }

                StarmapLabel.Set(label, body);
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
    }

    /// <summary>How much an anomalous planet says about itself on its star map label.</summary>
    internal enum StarmapLabelMode
    {
        /// <summary>No star map label. Anomalies stay visible in the planet panel.</summary>
        Off,

        /// <summary>Just the word ANOMALY, for a less crowded galaxy view.</summary>
        Marker,

        /// <summary>The affected item and multiplier, e.g. "Titanium Crystal x10".</summary>
        Detail
    }
}
