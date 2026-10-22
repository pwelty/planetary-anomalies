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
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log;

        // Playtesting knobs. BepInEx writes these to
        // BepInEx/config/com.planetaryanomalies.dsp.cfg on first run; edit that file and relaunch
        // rather than rebuilding. They are read when a galaxy is first seen, so a change takes
        // effect on the next load rather than mid-session.
        internal static ConfigEntry<string> AnomalyChancePercent;
        internal static ConfigEntry<bool> HomePlanetNeverAnomalous;
        internal static ConfigEntry<bool> AnomalousHome;
        internal static ConfigEntry<bool> HomeSkipCombat;
        internal static ConfigEntry<bool> NearbyFavorsEarlyRecipes;
        internal static ConfigEntry<bool> NoDuplicates;
        internal static ConfigEntry<bool> NoRepeatsInGalaxy;
        internal static ConfigEntry<bool> ResearchCubes;
        internal static ConfigEntry<bool> MultiOutputRecipes;
        internal static ConfigEntry<bool> Storm;
        internal static ConfigEntry<bool> ReplicatorFollowsPlanet;
        internal static ConfigEntry<int> OutputMultiplier;
        internal static ConfigEntry<bool> MultiplierFromDifficulty;
        internal static ConfigEntry<bool> MultiplierFromDanger;
        internal static ConfigEntry<float> DifficultyStrength;
        internal static ConfigEntry<float> DangerStrength;
        internal static ConfigEntry<float> DistanceStrength;
        internal static ConfigEntry<bool> MultiplierFromDistance;
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
        internal static ConfigEntry<bool> ExcludeBuildings;
        internal static ConfigEntry<string> RecipeWeights;
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
                typeof(UIStarmapStarPatch), typeof(ItemTipPatch), typeof(TechUnlockPatch), typeof(UILabWindowPatch),
                typeof(UIPlanetGlobePatch)
            };
            string[] surfaceNames =
            {
                "the planet panel", "the machine window", "star map planet labels",
                "star map star labels", "item tooltips", "research announcements", "the lab window",
                "the corner globe (Storm)"
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

            // Not a disclosure surface: an effect, on its own seam, and inert unless the setting is on.
            TryPatch(typeof(ReplicatorPatch), "the replicator (ReplicatorFollowsPlanet)");
            TryPatch(typeof(LabOutputCapPatch), "the lab output limit (ResearchCubes)");
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
                "Rulesets 1 and 2 only; ruleset 3 uses AnomalousHome instead.\n" +
                "Keeps the world you start on ordinary.\n" +
                "On by default, deliberately: you would meet an anomaly before the star map exists to\n" +
                "explain it, and anomalies are meant to be a reason to look outward rather than a\n" +
                "property of the ground you are standing on.\n" +
                "Set to false and your home planet is drawn like any other -- most of the time it will\n" +
                "still have nothing, since it takes the same chance as everywhere else.\n" +
                "This affects only the home planet. Every other world keeps exactly the anomaly it\n" +
                "already had, because presence is an independent draw per planet.");

            AnomalousHome = Config.Bind(
                "Generation",
                "AnomalousHome",
                true,
                "Ruleset 3 only. The world you start on always carries an anomaly, drawn only from\n" +
                "recipes you can build early -- smelting, belts, sorters, assemblers and the like --\n" +
                "so there is something to use in the first hours, before any other world is in reach.\n" +
                "false: the home planet is never anomalous.\n" +
                "Rulesets 1 and 2 ignore this and use HomePlanetNeverAnomalous.");

            HomeSkipCombat = Config.Bind(
                "Generation",
                "HomeSkipCombat",
                true,
                "Ruleset 3 only, with AnomalousHome on. The home planet's anomaly is never a weapon,\n" +
                "ammunition or defensive building -- the early tech tree includes the first turrets and\n" +
                "ammo, and a starting world making Gauss Turrets ten at a time is not much of a start.\n" +
                "false: any early recipe can land on the home planet, combat included.\n" +
                "Only the home planet is affected; other worlds draw combat recipes as usual.");

            NearbyFavorsEarlyRecipes = Config.Bind(
                "Generation",
                "NearbyFavorsEarlyRecipes",
                true,
                "Ruleset 3 only. Worlds in star systems within 6 light years of home lean toward\n" +
                "recipes you can build early, so what you can reach first is more often something you\n" +
                "can use first. Worlds further out draw as usual.\n" +
                "false: every world draws from the whole recipe list alike.\n" +
                "Changes which recipe some nearby worlds carry, never which worlds are anomalous.");

            NoDuplicates = Config.Bind(
                "Generation",
                "NoDuplicates",
                true,
                "Ruleset 3 only. No recipe is an anomaly twice in the same star system, and none twice\n" +
                "among the star systems within 6 light years of home -- the worlds you reach first,\n" +
                "where a repeat wastes the most.\n" +
                "false: every world draws on its own, and repeats happen.\n" +
                "Changes which recipe some worlds carry, never which worlds are anomalous.");

            NoRepeatsInGalaxy = Config.Bind(
                "Generation",
                "NoRepeatsInGalaxy",
                false,
                "Ruleset 3 only. Every recipe is an anomaly at most once in the whole galaxy. Takes over\n" +
                "from NoDuplicates.\n" +
                "There are only about 150 recipes, so at high density a galaxy has more anomalous worlds\n" +
                "than recipes. They are handed out nearest first -- home, then outward -- and once every\n" +
                "recipe is taken, the worlds still waiting, the furthest ones, stay ordinary. The log says\n" +
                "how many.\n" +
                "Off by default: a recipe on two or three worlds is a choice of which one you can hold.");

            ResearchCubes = Config.Bind(
                "Generation",
                "ResearchCubes",
                true,
                "Ruleset 3 only. Research cubes -- the matrices labs make -- can be anomalies too, so a\n" +
                "world may make electromagnetic or universe matrices ten at a time in its labs.\n" +
                "A cube world is the one place a player would reorganise a galaxy around, and the cubes\n" +
                "are consumed forever. For now every cube anomaly is the same multiple as the rest;\n" +
                "varied multipliers are planned to make the rarer cubes rarer.\n" +
                "false: no matrix is ever an anomaly.");

            MultiOutputRecipes = Config.Bind(
                "Generation",
                "MultiOutputRecipes",
                true,
                "Ruleset 3 only. Recipes with more than one output can be anomalies -- plasma refining,\n" +
                "X-ray cracking, graphene from fire ice, antimatter. Every output is multiplied, the\n" +
                "by-product included: an anomalous refinery makes ten times the hydrogen as well as ten\n" +
                "times the oil, which is more to use and more to get rid of.\n" +
                "false: only recipes with a single output are drawn, as in rulesets 1 and 2.");

            Storm = Config.Bind(
                "Generation",
                "Storm",
                true,
                "Ruleset 3 only. Early in a new game -- between 10 and 20 minutes of game time -- an\n" +
                "anomalous storm from the nearest world whose anomaly needs no research reaches your home\n" +
                "planet. For 30 minutes your machines there make that recipe at the anomaly's multiple; then\n" +
                "it passes, and you are told the world that has it for good. Announced when it arrives and\n" +
                "when it ends; the home planet's panel and the machine window show it while it lasts.\n" +
                "Nothing is saved: the storm follows the save's own clock.\n" +
                "false: no storm.");

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

            MultiplierFromDifficulty = Config.Bind(
                "Effect",
                "MultiplierFromDifficulty",
                false,
                "Scales OutputMultiplier by the galaxy's Dark Fog difficulty -- the score the game shows when\n" +
                "you create a galaxy, against 1.0 for the default settings -- between 0.3 and 3 times: x3 to\n" +
                "x30 at the default 10. Peace mode counts as the floor, x3. Dial: DifficultyStrength. Change the Dark Fog's\n" +
                "aggressiveness mid-game and the multiple follows within a few seconds, announced. Applies to\n" +
                "every anomaly, home included, and combines with MultiplierFromDanger and\n" +
                "MultiplierFromDistance by multiplying. Supersedes the experimental\n" +
                "MultiplierFromCombatSettings. Any ruleset; switching it on or off takes effect on the next load.\n" +
                "false: OutputMultiplier as configured.");

            MultiplierFromDanger = Config.Bind(
                "Effect",
                "MultiplierFromDanger",
                false,
                "Anomalies in star systems that start with Dark Fog hives make more: one more step of\n" +
                "OutputMultiplier per hive, up to three -- x20 with one hive, x30 with two, x40 with three or\n" +
                "more, at the default 10 (dial: DangerStrength). The count is the\n" +
                "one the system started with, fixed when the galaxy was made, so hives spreading later never\n" +
                "change a number. No effect in peace mode. The home planet keeps OutputMultiplier. Combines\n" +
                "with MultiplierFromDistance by multiplying.\n" +
                "Moves no planet and changes no recipe, so it can be switched in a running game; it takes\n" +
                "effect on the next load. Any ruleset.\n" +
                "false: every anomaly makes OutputMultiplier.");

            MultiplierFromDistance = Config.Bind(
                "Effect",
                "MultiplierFromDistance",
                false,
                "Anomalies further from home make more. Star systems are ranked by distance from home: the\n" +
                "nearest third keep OutputMultiplier, the middle third make one and a half times it, the\n" +
                "farthest third twice -- x10, x15, x20 at the default (dial:\n" +
                "DistanceStrength). Systems within 6 light years of home always count as near. The home\n" +
                "planet keeps OutputMultiplier. Combines with\n" +
                "MultiplierFromDanger by multiplying. Moves no planet and changes no recipe, so it can be\n" +
                "switched in a running game; it takes effect on the next load. Any ruleset.\n" +
                "false: every anomaly makes OutputMultiplier.");

            DifficultyStrength = Config.Bind(
                "Multiplier tuning",
                "DifficultyStrength",
                1.0f,
                new ConfigDescription(
                "How strongly the Dark Fog difficulty moves the base multiple. Only matters when\n" +
                "MultiplierFromDifficulty is on. Examples at OutputMultiplier 10:\n" +
                "  1   -- as the game's score says: a galaxy scoring 1.5 gives x15, one scoring 0.5 gives x5.\n" +
                "  2   -- twice as far from x10: the same galaxies give x20 and x1 (never below x1).\n" +
                "  0.5 -- half as far: x13 and x8.\n" +
                "  0   -- difficulty changes nothing; every galaxy gets x10.\n" +
                "Read when a save loads.",
                new AcceptableValueRange<float>(0f, 10f)));

            DangerStrength = Config.Bind(
                "Multiplier tuning",
                "DangerStrength",
                1.0f,
                new ConfigDescription(
                "How much each Dark Fog hive a star system started with adds. Only matters when\n" +
                "MultiplierFromDanger is on. At most three hives count. Examples at OutputMultiplier 10:\n" +
                "  1   -- each hive adds x10: one hive x20, three hives x40.\n" +
                "  1.5 -- each adds x15: one hive x25, three x55.\n" +
                "  0.5 -- each adds x5: one hive x15, three x25.\n" +
                "  0   -- hives add nothing.\n" +
                "Read when a save loads.",
                new AcceptableValueRange<float>(0f, 10f)));

            DistanceStrength = Config.Bind(
                "Multiplier tuning",
                "DistanceStrength",
                1.0f,
                new ConfigDescription(
                "How much being far from home adds. Only matters when MultiplierFromDistance is on.\n" +
                "Examples at OutputMultiplier 10, for the middle and farthest thirds of star systems:\n" +
                "  1   -- x15 and x20.\n" +
                "  1.5 -- x18 and x25.\n" +
                "  0.5 -- x13 and x15.\n" +
                "  0   -- distance adds nothing.\n" +
                "Read when a save loads.",
                new AcceptableValueRange<float>(0f, 10f)));

            ReplicatorFollowsPlanet = Config.Bind(
                "Effect",
                "ReplicatorFollowsPlanet",
                true,
                "When Icarus stands on an anomalous world, hand-crafting that world's anomalous recipe\n" +
                "in the replicator makes the same multiple the machines there do.\n" +
                "A job keeps its multiple if you fly away before it finishes, and a job still queued\n" +
                "when you save is saved with it.\n" +
                "false: the replicator ignores anomalies, as before 1.0.");

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
                "Which ruleset this install uses: 1, 2, 3, or Latest.\n" +
                "1 -- the pre-1.0 ruleset. Every 0.x release uses it. It draws from a fixed list of\n" +
                "  150 recipes, so a game update that adds recipes never moves a planet.\n" +
                "2 -- ruleset 1 plus every recipe the game has gained since: Dark Fog Lens from game\n" +
                "  version 0.10.35, and whatever comes next. Switching from 1 moves only the few planets\n" +
                "  those recipes take -- about one anomalous planet in 150 per recipe -- and a later game\n" +
                "  update can move a planet or two again. Recipes added by other mods count here too.\n" +
                "3 -- the 1.0 ruleset, in development, and the default for a fresh install. It draws\n" +
                "  anomalies differently, so a galaxy under 3 is a different galaxy: see AnomalousHome\n" +
                "  and NearbyFavorsEarlyRecipes. Its recipe list follows the game, like ruleset 2.\n" +
                "Latest -- the newest ruleset this version of the mod knows (3).\n" +
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

            ExcludeBuildings = Config.Bind(
                "Generation",
                "ExcludeBuildings",
                false,
                "No anomaly is ever on a recipe that makes a building -- power, belts, sorters, storage,\n" +
                "logistics, miners, smelters, assemblers, labs, turrets and the rest -- so every anomaly is on\n" +
                "something you make to use or ship rather than to place. Read from the game, so it also covers\n" +
                "buildings a game update or another mod adds. Asked for on Reddit, where players had been\n" +
                "pasting every building's name into ExcludedRecipes.\n" +
                "Like ExcludedRecipes, switching it changes only the planets that carry a building recipe; the\n" +
                "rest of your galaxy is untouched. Any ruleset. The log names what was left out.\n" +
                "false: buildings can be anomalies.");

            RecipeWeights = Config.Bind(
                "Generation",
                "RecipeWeights",
                "",
                "For players who want to tune rarity; everyone else can leave it empty. How likely particular\n" +
                "recipes are to be drawn, comma separated, as name: weight. Use the name as it appears in game,\n" +
                "or a recipe's numeric id:\n" +
                "  RecipeWeights = Crystal Silicon: 0.1, Gear: 3, Sorter Mk.I: 0\n" +
                "  1   -- normal, the same as leaving a recipe out of the list.\n" +
                "  3   -- three times as likely. Whole numbers above 1; 2.5 counts as 3.\n" +
                "  0.5 -- half as likely; 0.1 a tenth; 0.3 a third (one over a whole number).\n" +
                "  0   -- never, the same as ExcludedRecipes.\n" +
                "Changing a weight moves only the planets that recipe wins or loses; the rest of your galaxy\n" +
                "is untouched. Your galaxy then differs from another player's with the same seed. Entries that\n" +
                "match no recipe are named in the log. Any ruleset.");

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
