using System;
using System.IO;
using HarmonyLib;

namespace PlanetaryAnomalies
{
    /// <summary>
    /// Tells the manager how a galaxy arrived: created in this session, or loaded from a save.
    ///
    /// Pinning needs that one fact for a galaxy it has never seen, and it needs it from the game
    /// rather than inferred. GameMain.Start always calls GameData.NewGame first, then -- only for
    /// a load -- GameSave.LoadCurrentGame, which calls GameData.Import over the fresh data. So by
    /// the time GameMain.Begin runs, "NewGame fired and Import did not" is exactly "this is new".
    ///
    /// The first version inferred age from GameMain.gameTick and got 515,221 on a brand-new game.
    /// verify.ps1 now asserts both halves of the call order above, because this is a behavioural
    /// assumption and nothing else would notice it changing.
    /// </summary>
    internal static class GameDataPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameData), "NewGame")]
        internal static void AfterNewGame()
        {
            AnomalyManager.NoteGalaxyCreated();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameData), "Import")]
        internal static void AfterImport()
        {
            AnomalyManager.NoteGalaxyLoaded();
        }
    }
}