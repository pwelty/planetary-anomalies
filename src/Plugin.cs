using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace PlanetaryAnomalies
{
    /// <summary>
    /// Planetary Anomalies: on most planets, one ordinary recipe produces ten times its normal
    /// output. Which planets, and which recipe on each, is derived from the galaxy seed, so a
    /// given galaxy always has the same anomalies. See PRODUCT.md and LOG.md.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.planetaryanomalies.dsp";
        public const string PluginName = "Planetary Anomalies";
        public const string PluginVersion = "0.0.1";

        internal static ManualLogSource Log;

        // Playtesting knobs. BepInEx writes these to
        // BepInEx/config/com.planetaryanomalies.dsp.cfg on first run; edit that file and relaunch
        // rather than rebuilding. They are read when a galaxy is first seen, so a change takes
        // effect on the next load rather than mid-session.
        internal static ConfigEntry<int> AnomalyChancePercent;
        internal static ConfigEntry<int> OutputMultiplier;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            BindConfig();

            Log.LogInfo(PluginName + " v" + PluginVersion + " loaded");
            Log.LogInfo("Anomalies derived from the galaxy seed; output x" + OutputMultiplier.Value +
                        (AnomalyChancePercent.Value >= 0
                            ? ". Density forced to " + AnomalyChancePercent.Value + "% by config."
                            : ". Density drawn per galaxy, 25-75%."));

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(PlanetFactoryBeforeGameTickPatch));
            _harmony.PatchAll(typeof(UIPlanetDetailPatch));
            _harmony.PatchAll(typeof(UIAssemblerWindowPatch));

            // The production hook only fires once a planet has a factory to tick, which does not
            // happen until something is built there -- not merely when a save is loaded.
            Log.LogInfo("Patched PlanetFactory.BeforeGameTick() for production, and " +
                        "UIPlanetDetail.OnPlanetDataSet() and UIAssemblerWindow._OnUpdate() to disclose " +
                        "anomalies in the planet panel and on the machine. " +
                        "Idle until a planet has a factory (i.e. until something is built).");
        }

        private void BindConfig()
        {
            AnomalyChancePercent = Config.Bind(
                "Generation",
                "AnomalyChancePercent",
                -1,
                new ConfigDescription(
                    "Playtesting override for how many non-home planets carry an anomaly. " +
                    "-1, the default, derives the density from the galaxy seed, between 25% and " +
                    "75%, so galaxies differ from one another: some are anomaly-rich, some sparse. " +
                    "That is the intended behaviour. Any value from 0 to 100 forces that " +
                    "percentage instead, which is useful for testing but makes every galaxy the " +
                    "same density. Changing this re-rolls which planets are anomalous, though not " +
                    "which recipe each anomalous planet gets.",
                    new AcceptableValueRange<int>(-1, 100)));

            OutputMultiplier = Config.Bind(
                "Effect",
                "OutputMultiplier",
                10,
                new ConfigDescription(
                    "How much more an anomalous recipe produces. 10 is deliberately unmistakable " +
                    "at a glance. Changing this affects neither which planets are anomalous nor " +
                    "which recipe each one affects.",
                    new AcceptableValueRange<int>(2, 1000)));
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            AnomalyManager.Reset();
            PlanetFactoryBeforeGameTickPatch.Reset();
        }
    }
}
