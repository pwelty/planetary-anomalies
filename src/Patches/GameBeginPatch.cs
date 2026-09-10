using System;
using HarmonyLib;

namespace PlanetaryAnomalies
{
    /// <summary>
    /// Tells the manager about a galaxy the moment it exists, instead of waiting to be asked.
    ///
    /// Everything else in this mod is lazy: the galaxy is established the first time a panel, a
    /// label or a factory tick needs an answer. That was fine until pinning, which has to decide
    /// whether a galaxy with no recorded rules is brand new or an old save that predates the
    /// record -- and the only honest signal for that is how old the game is when the mod first
    /// sees it. A lazy first sight could be ten minutes into a new game; an eager one is always
    /// at load, where a new galaxy is at tick zero and an old save is at whatever it was saved at.
    ///
    /// GameMain.Begin is called from the loader for new games and loads alike, after the data is
    /// in place. If the manager is not ready for some reason it simply reports why and is asked
    /// again later, as it always was.
    /// </summary>
    internal static class GameBeginPatch
    {
        private static bool _errorLogged;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameMain), "Begin")]
        internal static void AfterBegin()
        {
            try
            {
                AnomalyManager.Touch();
            }
            catch (Exception e)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    Plugin.Log.LogError("Failed to establish the galaxy at game start: " + e);
                }
            }
        }
    }
}
