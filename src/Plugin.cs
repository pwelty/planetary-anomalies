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
        public const string PluginVersion = "0.2.0";

        internal static ManualLogSource Log;

        // Playtesting knobs. BepInEx writes these to
        // BepInEx/config/com.planetaryanomalies.dsp.cfg on first run; edit that file and relaunch
        // rather than rebuilding. They are read when a galaxy is first seen, so a change takes
        // effect on the next load rather than mid-session.
        internal static ConfigEntry<int> AnomalyChancePercent;
        internal static ConfigEntry<int> OutputMultiplier;
        internal static ConfigEntry<StarmapLabelMode> StarmapLabel;
        internal static ConfigEntry<bool> LogEveryAnomaly;

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
            _harmony.PatchAll(typeof(UIStarmapPlanetPatch));
            _harmony.PatchAll(typeof(UIStarmapStarPatch));

            // The production hook only fires once a planet has a factory to tick, which does not
            // happen until something is built there -- not merely when a save is loaded.
            Log.LogInfo("Patched PlanetFactory.BeforeGameTick() for production, and " +
                        "UIPlanetDetail, UIAssemblerWindow and the star map to disclose " +
                        "anomalies in the planet panel, on the machine, and on star map planet and star labels. " +
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

            StarmapLabel = Config.Bind(
                "Display",
                "StarmapLabel",
                StarmapLabelMode.Detail,
                "What star map labels show, for planets you have scanned.\n" +
                "Detail: a star lists the affected items in its system, and a planet names its item\n" +
                "  and multiplier, e.g. \"Titanium Crystal x10\".\n" +
                "Marker: a star shows how many anomalous planets it has, and a planet shows just a\n" +
                "  symbol -- a less crowded galaxy view.\n" +
                "Off: no star map labels at all; anomalies remain visible in the planet panel.\n" +
                "Unscanned planets never show anything, whichever setting is used.");

            LogEveryAnomaly = Config.Bind(
                "Diagnostics",
                "LogEveryAnomaly",
                false,
                "Writes every anomaly in the galaxy to the BepInEx log when a save is loaded, " +
                "including planets you have never scanned. This spoils discovery on purpose. It " +
                "exists for development and for answering \"does this galaxy contain X anywhere?\". " +
                "It changes nothing in game and shows nothing on screen -- it only writes to the log.");
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
