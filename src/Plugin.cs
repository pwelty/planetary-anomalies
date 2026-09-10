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
        public const string PluginVersion = "0.5.0";

        internal static ManualLogSource Log;

        // Playtesting knobs. BepInEx writes these to
        // BepInEx/config/com.planetaryanomalies.dsp.cfg on first run; edit that file and relaunch
        // rather than rebuilding. They are read when a galaxy is first seen, so a change takes
        // effect on the next load rather than mid-session.
        internal static ConfigEntry<int> AnomalyChancePercent;
        internal static ConfigEntry<int> OutputMultiplier;
        internal static ConfigEntry<StarmapLabelMode> StarmapLabel;
        internal static ConfigEntry<UnresearchedDisplay> UnresearchedAnomalies;
        internal static ConfigEntry<bool> AnnounceOnResearch;

        // Experimental. Off by default, and everything about them says so.
        internal static ConfigEntry<bool> MultiplierFromCombatSettings;
        internal static ConfigEntry<int> PeacefulOutputMultiplier;

        /// <summary>
        /// Superseded by <see cref="UnresearchedAnomalies"/> in 0.5, and still bound so that a
        /// player who set it in 0.4 does not have their choice silently ignored.
        /// </summary>
        internal static ConfigEntry<bool> LegacyHideUnresearched;
        internal static ConfigEntry<bool> LogEveryAnomaly;
        internal static ConfigEntry<string> ExcludedRecipes;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            BindConfig();

            Log.LogInfo(PluginName + " v" + PluginVersion + " loaded");
            Log.LogInfo("Anomalies derived from the galaxy seed; output x" + OutputMultiplier.Value +
                        (AnomalyChancePercent.Value >= 0
                            ? ". Density forced to " + AnomalyChancePercent.Value + "% by config."
                            : ". Density drawn per galaxy, 25-75%.") +
                        (MultiplierFromCombatSettings.Value
                            ? " EXPERIMENTAL: peaceful galaxies use x" + PeacefulOutputMultiplier.Value +
                              " instead; resolved when a save loads."
                            : ""));

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(PlanetFactoryBeforeGameTickPatch));
            _harmony.PatchAll(typeof(UIPlanetDetailPatch));
            _harmony.PatchAll(typeof(UIAssemblerWindowPatch));
            _harmony.PatchAll(typeof(UIStarmapPlanetPatch));
            _harmony.PatchAll(typeof(UIStarmapStarPatch));
            _harmony.PatchAll(typeof(TechUnlockPatch));

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

            ExcludedRecipes = Config.Bind(
                "Generation",
                "ExcludedRecipes",
                "",
                "Recipes that should never receive an anomaly, comma separated. Empty by default.\n" +
                "Use the item name as it appears in game, or a recipe's numeric id:\n" +
                "  ExcludedRecipes = Water Pump, Wind Turbine, 106\n" +
                "The mod deliberately holds no opinion about which anomalies are worth having --\n" +
                "which recipes matter depends on how you play, and a list that is useless to one\n" +
                "player is exactly what another builds by the thousand. So this is yours to set.\n" +
                "Entries that match no recipe are reported in the log rather than ignored quietly.\n" +
                "Excluding a recipe only changes the planets that currently carry it; the rest of\n" +
                "your galaxy is untouched. It does mean your galaxy differs from another player's\n" +
                "with the same seed.");

            UnresearchedAnomalies = Config.Bind(
                "Display",
                "UnresearchedAnomalies",
                UnresearchedDisplay.Hide,
                "What an anomaly says about itself before you have researched the recipe it\n" +
                "affects. Applies everywhere: the planet panel, planet and star labels, and the\n" +
                "system counts.\n" +
                "Hide:   nothing at all. The planet reads as ordinary until the research lands.\n" +
                "Marker: the symbol without the name -- you know something is there and worth\n" +
                "  coming back for, but not yet what. Existence is cheap information; the name is\n" +
                "  the part that means nothing before you can build it.\n" +
                "Show:   everything, as in 0.3 and earlier.\n" +
                "Display only: this changes nothing about which planets are anomalous.");

            AnnounceOnResearch = Config.Bind(
                "Display",
                "AnnounceOnResearch",
                true,
                "When you finish a technology, says whether a world you have already scanned makes\n" +
                "one of its recipes ten times over. Shown as the game's own brief tip, and always\n" +
                "written to the log.\n" +
                "This is the other half of hiding unresearched anomalies: the mod stays quiet while\n" +
                "a recipe means nothing to you, and speaks at the moment it starts to. Only planets\n" +
                "you have already found are named -- it will not point at worlds you have not\n" +
                "visited, which would be a spoiler rather than a reminder.");

            LegacyHideUnresearched = Config.Bind(
                "Display",
                "HideUnresearchedAnomalies",
                true,
                "Superseded by UnresearchedAnomalies. Kept only so a 0.4 setting is not ignored:\n" +
                "if this is false and UnresearchedAnomalies is still at its default, it is read as\n" +
                "UnresearchedAnomalies = Show, once, and reported in the log.");

            // A player who turned the 0.4 setting off asked to see everything. Renaming the setting
            // must not quietly revoke that -- a config that stops being honoured without saying so
            // is worse than one that never existed.
            if (!LegacyHideUnresearched.Value && UnresearchedAnomalies.Value == UnresearchedDisplay.Hide)
            {
                UnresearchedAnomalies.Value = UnresearchedDisplay.Show;
                Log.LogInfo("HideUnresearchedAnomalies = false is superseded by " +
                            "UnresearchedAnomalies = Show, and has been migrated. You can delete " +
                            "the old setting; Marker is the middle option if you want the symbol " +
                            "without the name.");
            }

            MultiplierFromCombatSettings = Config.Bind(
                "Experimental",
                "MultiplierFromCombatSettings",
                false,
                "EXPERIMENTAL. Off by default.\n" +
                "When on, a galaxy with the Dark Fog disabled or set to passive uses\n" +
                "PeacefulOutputMultiplier instead of OutputMultiplier. Hostile galaxies are unchanged.\n" +
                "The reasoning: the multiplier is a return on the cost of using an anomaly, and the\n" +
                "largest cost is clearing a world and then holding it. Without the Dark Fog that\n" +
                "cost is gone and only hauling remains, so the same x10 that is fair against a\n" +
                "garrison is a giveaway without one.\n" +
                "Resolved once per galaxy when a save is loaded, and the choice is written to the\n" +
                "log. Changes neither which planets are anomalous nor which recipe each carries.");

            PeacefulOutputMultiplier = Config.Bind(
                "Experimental",
                "PeacefulOutputMultiplier",
                3,
                new ConfigDescription(
                    "EXPERIMENTAL. Only used when MultiplierFromCombatSettings is on and the galaxy\n" +
                    "is peaceful or its enemies passive. 3 is what players who found x10 too high\n" +
                    "have settled on.",
                    new AcceptableValueRange<int>(2, 1000)));

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
            TechUnlockPatch.Reset();
        }
    }
}
