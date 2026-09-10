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
    /// record. GameDataPatch learns which from the game itself -- NewGame fired, or Import did --
    /// and this hook is what makes the manager read that answer at load, while it is fresh, rather
    /// than at some later moment when a panel first asks.
    ///
    /// GameMain.Begin is called from the loader for new games and loads alike, after the data is
    /// in place. If the manager is not ready for some reason it simply reports why and is asked
    /// again later, as it always was.
    ///
    /// It is also called for the galaxy behind the main menu, which DSP loads from a resource save
    /// and ticks like a real game. Being eager is exactly what made the mod notice that galaxy for
    /// the first time in 0.5 -- and pin it, which is wrong: nobody is playing it. So the demo is
    /// skipped here, and the manager refuses to pin it even if something else asks.
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
                if (DSPGame.IsMenuDemo)
                {
                    return;
                }

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
