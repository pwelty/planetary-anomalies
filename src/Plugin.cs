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

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(PlanetFactoryBeforeGameTickPatch));
            _harmony.PatchAll(typeof(UIPlanetDetailPatch));

            // The hook only fires once a planet has a factory to tick, which does not happen until
            // something is built on that planet -- not merely when a save is loaded.
            Log.LogInfo("Patched PlanetFactory.BeforeGameTick() for production, and " +
                        "UIPlanetDetail.OnPlanetDataSet() to disclose the anomaly in the planet panel. " +
                        "Idle until a planet has a factory (i.e. until something is built).");
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
    }
}
