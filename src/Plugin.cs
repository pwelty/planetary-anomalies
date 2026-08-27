using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace PlanetaryAnomalies
{
    /// <summary>
    /// Stage 0 of Planetary Anomalies: one hard-coded smelter recipe produces ten units instead
    /// of one, on the home planet only. No UI, no persistence, no randomness -- see SPIKE.md.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.planetaryanomalies.dsp";
        public const string PluginName = "Planetary Anomalies";
        public const string PluginVersion = "0.0.1";

        internal static ManualLogSource Log;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            Log.LogInfo(PluginName + " v" + PluginVersion + " loaded");
            Log.LogInfo("Stage 0: one hard-coded smelting recipe produces x" +
                        AnomalyManager.OutputMultiplier + " output on the home planet only.");
            LogGameVersion();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(FactorySystemGameTickPatch));

            Log.LogInfo("Patched FactorySystem.GameTick(long, bool). Waiting for a game to load.");
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            AnomalyManager.Reset();
        }

        /// <summary>
        /// Records the build this run actually executed against, so a log can be matched back to
        /// the signatures in docs/inspection.md.
        /// </summary>
        private static void LogGameVersion()
        {
            try
            {
                Version version = GameConfig.gameVersion;
                Log.LogInfo("Game version: " + version.ToFullString() + " (build " + GameConfig.build + ")");
            }
            catch (System.Exception e)
            {
                Log.LogWarning("Could not read the game version: " + e.Message);
            }
        }
    }
}
