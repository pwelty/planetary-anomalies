using System;
using System.Collections.Generic;
using HarmonyLib;

namespace PlanetaryAnomalies
{
    /// <summary>
    /// When a technology completes, says whether the galaxy already contains a world that makes one
    /// of its recipes ten times over.
    ///
    /// This is the other half of hiding, and the reason hiding is defensible at all. 0.4 withheld
    /// anomalies whose recipe was unresearched, which removed real noise -- and, it turned out,
    /// removed anomalies as a *reason to research*. The first galaxy to run it hid 31, overwhelmingly
    /// military, from a player who had just discovered that holding a distant world costs a war.
    ///
    /// Without this, hiding is a subtraction: information a player had in 0.3 and lost in 0.4.
    /// With it, the trade is honest -- the mod stays quiet while a recipe means nothing to you, and
    /// speaks at the exact moment it starts to. That moment is a better one than discovery, because
    /// it is when you can act.
    ///
    /// Only planets already scanned are named. Announcing anomalies on worlds the player has never
    /// visited would be the answer key with extra steps.
    /// </summary>
    internal static class TechUnlockPatch
    {
        private static bool _errorLogged;

        /// <summary>
        /// Technologies already announced this session. NotifyTechUnlock fires per level, and
        /// multi-level technologies would otherwise repeat themselves for as long as you research.
        /// </summary>
        private static readonly HashSet<int> _announced = new HashSet<int>();

        /// <summary>How many worlds to name before summarising the rest.</summary>
        private const int MaxNamed = 3;

        internal static void Reset()
        {
            _announced.Clear();
        }

        /// <summary>
        /// Postfix rather than the onTechUnlocked event, so the subscription cannot outlive the
        /// plugin: Harmony removes this on unpatch, where a stray event handler would keep firing
        /// against a manager that had been reset.
        ///
        /// Safe against load: GameHistoryData.Import does not call the unlock path, so loading a
        /// save with two hundred technologies already researched announces nothing.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameHistoryData), "NotifyTechUnlock")]
        internal static void AfterTechUnlock(int _techId)
        {
            try
            {
                if (Plugin.AnnounceOnResearch != null && !Plugin.AnnounceOnResearch.Value)
                {
                    return;
                }

                if (_announced.Contains(_techId))
                {
                    return;
                }
                _announced.Add(_techId);

                TechProtoSet techs = LDB.techs;
                if (techs == null || !techs.Exist(_techId))
                {
                    return;
                }

                TechProto tech = techs.Select(_techId);
                if (tech == null || tech.UnlockRecipes == null)
                {
                    return;
                }

                for (int i = 0; i < tech.UnlockRecipes.Length; i++)
                {
                    Announce(tech.UnlockRecipes[i]);
                }
            }
            catch (Exception e)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    Plugin.Log.LogError("Failed to announce anomalies for a technology: " + e);
                }
            }
        }

        private static void Announce(int recipeId)
        {
            int total;
            string where = AnomalyManager.KnownPlanetsWithRecipe(recipeId, MaxNamed, out total);
            if (where == null)
            {
                return;
            }

            string what = AnomalyManager.RecipeLabel(recipeId);
            if (string.IsNullOrEmpty(what))
            {
                return;
            }

            string message = "ANOMALY: " + what + " on " + where;

            Plugin.Log.LogInfo("Announced on research: " + message);

            // The game's own transient tip, used here the way DSP uses it elsewhere. Deliberately
            // not a message box: this is worth noticing, not worth interrupting for.
            try
            {
                UIRealtimeTip.Popup(message, true, 0);
            }
            catch (Exception e)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    Plugin.Log.LogError("Could not show a tip; the announcement is in the log only: " + e);
                }
            }
        }
    }
}
