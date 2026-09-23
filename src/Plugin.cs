using System;
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
        public const string PluginVersion = "0.5.1";

        internal static ManualLogSource Log;

        // Playtesting knobs. BepInEx writes these to
        // BepInEx/config/com.planetaryanomalies.dsp.cfg on first run; edit that file and relaunch
        // rather than rebuilding. They are read when a galaxy is first seen, so a change takes
        // effect on the next load rather than mid-session.
        internal static ConfigEntry<string> AnomalyChancePercent;
        internal static ConfigEntry<bool> HomePlanetNeverAnomalous;
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
        internal static ConfigEntry<bool> TestAnnouncement;
        internal static ConfigEntry<string> ExcludedRecipes;
        internal static ConfigEntry<string> AnomalyRules;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            // Before anything can be patched: research finishes on worker threads, and the
            // announcement code needs to know which thread is Unity's.
            TechUnlockPatch.CaptureMainThread();

            BindConfig();

            Log.LogInfo(PluginName + " v" + PluginVersion + " loaded");
            int forcedDensity;
            Log.LogInfo("Anomalies derived from the galaxy seed; output x" + OutputMultiplier.Value +
                        (AnomalyManager.TryForcedDensity(out forcedDensity)
                            ? ". Density forced to " + forcedDensity + "% by config."
                            : ". Density drawn per galaxy, 25-75%.") +
                        (MultiplierFromCombatSettings.Value
                            ? " EXPERIMENTAL: peaceful galaxies use x" + PeacefulOutputMultiplier.Value +
                              " instead; resolved when a save loads."
                            : ""));

            _harmony = new Harmony(PluginGuid);

            // Production first, and alone. If the game has changed under it the mod does nothing to
            // output, and every label would then advertise an anomaly that does not exist -- worse
            // than no mod. So nothing else is patched without it.
            if (!TryPatch(typeof(PlanetFactoryBeforeGameTickPatch), "production (PlanetFactory.BeforeGameTick)"))
            {
                Log.LogError(PluginName + " is off: production could not be patched, so nothing is " +
                             "labelled either. This usually means the game has updated; look for a " +
                             "newer version of the mod.");
                return;
            }

            // Each display surface on its own. They hang off UI methods bound by parameter name,
            // which is the kind of thing a game update moves, and 0.5 added two more of them. One
            // that fails to apply must not take the others with it -- before this, a throw from any
            // PatchAll ended Awake, and every patch after it silently never happened.
            Type[] surfaces =
            {
                typeof(UIPlanetDetailPatch), typeof(UIAssemblerWindowPatch), typeof(UIStarmapPlanetPatch),
                typeof(UIStarmapStarPatch), typeof(ItemTipPatch), typeof(TechUnlockPatch)
            };
            string[] surfaceNames =
            {
                "the planet panel", "the machine window", "star map planet labels",
                "star map star labels", "item tooltips", "research announcements"
            };

            string working = "";
            string broken = "";
            for (int i = 0; i < surfaces.Length; i++)
            {
                if (TryPatch(surfaces[i], surfaceNames[i]))
                {
                    working += (working.Length > 0 ? ", " : "") + surfaceNames[i];
                }
                else
                {
                    broken += (broken.Length > 0 ? ", " : "") + surfaceNames[i];
                }
            }

            // The production hook only fires once a planet has a factory to tick, which does not
            // happen until something is built there -- not merely when a save is loaded.
            Log.LogInfo("Patched PlanetFactory.BeforeGameTick() for production. Anomalies are disclosed in: " +
                        (working.Length > 0 ? working : "nothing") + "." +
                        (broken.Length > 0 ? " NOT working: " + broken + " -- see the errors above." : "") +
                        " Production is idle until a planet has a factory (i.e. until something is built).");
        }

        /// <summary>
        /// Applies one patch class and says whether it took. Harmony throws when a target method or
        /// a by-name parameter is gone, which is what a game update looks like from in here.
        /// </summary>
        private bool TryPatch(Type patchClass, string what)
        {
            try
            {
                _harmony.PatchAll(patchClass);
                return true;
            }
            catch (Exception e)
            {
                Log.LogError("Could not patch " + what + ", so that part of the mod is off. This usually " +
                             "means the game has updated. " + e);
                return false;
            }
        }

        private void BindConfig()
        {
            HomePlanetNeverAnomalous = Config.Bind(
                "Generation",
                "HomePlanetNeverAnomalous",
                true,
                "Keeps the world you start on ordinary.\n" +
                "On by default, deliberately: you would meet an anomaly before the star map exists to\n" +
                "explain it, and anomalies are meant to be a reason to look outward rather than a\n" +
                "property of the ground you are standing on.\n" +
                "Set to false and your home planet is drawn like any other -- most of the time it will\n" +
                "still have nothing, since it takes the same chance as everywhere else.\n" +
                "This affects only the home planet. Every other world keeps exactly the anomaly it\n" +
                "already had, because presence is an independent draw per planet.");

            AnomalyChancePercent = Config.Bind(
                "Generation",
                "AnomalyChancePercent",
                "Seed",
                "How many non-home planets carry an anomaly.\n" +
                "Seed: derived from the galaxy seed, between 25% and 75%, so galaxies differ from one\n" +
                "  another -- some anomaly-rich, some sparse. The default, and the intended behaviour.\n" +
                "A number from 0 to 100 forces that percentage instead. Useful when a galaxy rolls\n" +
                "  sparser than you want to play; it costs nothing on a galaxy nobody has built on.\n" +
                "Changing this re-rolls which planets are anomalous, though not which recipe each\n" +
                "anomalous planet gets. (-1 still means Seed, for configs written by earlier versions.)");
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

            AnomalyRules = Config.Bind(
                "Generation",
                "AnomalyRules",
                AnomalyManager.DefaultRuleset.ToString(),
                "Which ruleset this install uses: 1, 2, or Latest.\n" +
                "1 -- the pre-1.0 ruleset, and the default. Every 0.x release uses it. It draws from a\n" +
                "  fixed list of 150 recipes, so a game update that adds recipes never moves a planet.\n" +
                "2 -- ruleset 1 plus every recipe the game has gained since: Dark Fog Lens from game\n" +
                "  version 0.10.35, and whatever comes next. Switching from 1 moves only the few planets\n" +
                "  those recipes take -- about one anomalous planet in 150 per recipe -- and a later game\n" +
                "  update can move a planet or two again. Recipes added by other mods count here too.\n" +
                "Latest -- the newest ruleset this version of the mod knows. 1.0 will add ruleset 3,\n" +
                "  which draws anomalies differently: a galaxy under 3 is a different galaxy.\n" +
                "This line is written once, when the mod first runs, and the mod never changes it, so an\n" +
                "upgraded install keeps what it has. It applies to every galaxy on this install, old and\n" +
                "new alike. Nothing is written to saves or anywhere else; this is the record.");

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
                "affects. Applies everywhere: the planet panel, planet and star labels, the system\n" +
                "counts, and the line in item tooltips.\n" +
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
                "When you finish a technology, says whether a world you have already scanned has an\n" +
                "anomaly on one of its recipes. Shown in gold just under the game's own\n" +
                "\"Research complete\" notice for about six seconds -- several from one technology\n" +
                "appear in turn -- and always written to the log.\n" +
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

            TestAnnouncement = Config.Bind(
                "Diagnostics",
                "TestAnnouncement",
                false,
                "Shows one test announcement about eight seconds after a save loads, through exactly the path a\n" +
                "real one takes. A quick way to check announcements work on your setup without\n" +
                "waiting for research to finish. The log records whether it reached the screen.\n" +
                "Turn it off again afterwards.");
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
            ItemTipPatch.Reset();
        }
    }
}
