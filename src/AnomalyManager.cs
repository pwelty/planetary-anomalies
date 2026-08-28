using System;
using System.Collections.Generic;

namespace PlanetaryAnomalies
{
    /// <summary>
    /// Decides which planets are anomalous and what each one does, deterministically from the
    /// galaxy seed.
    ///
    /// Nothing is stored in the save. A planet's anomaly is a pure function of
    /// (galaxy seed, planet id, anomaly-system version), so the same galaxy always produces the
    /// same anomalies and re-deriving on load is equivalent to having persisted them. See LOG.md
    /// for why that removes most of the need for a save subsystem.
    ///
    /// The anomaly itself is built as a private <see cref="RecipeExecuteData"/> rather than by
    /// editing anything shared. See docs/inspection.md for why that is the seam.
    /// </summary>
    internal static class AnomalyManager
    {
        /// <summary>
        /// Output multiplier, from config so it can be retuned during playtesting without a
        /// rebuild. Read once per galaxy, not per tick.
        /// </summary>
        internal static int OutputMultiplier
        {
            get { return Plugin.OutputMultiplier != null ? Plugin.OutputMultiplier.Value : 10; }
        }

        /// <summary>
        /// Bumping this re-rolls every galaxy. It is part of the hash so that a future change to
        /// generation can be introduced without silently rewriting anomalies in galaxies that
        /// already exist -- but only once the version in force is recorded per save, which it is
        /// not yet. Until then, changing this changes existing galaxies. See LOG.md.
        /// </summary>
        internal const int AnomalySystemVersion = 1;

        internal const int DensityMinPercent = AnomalyMath.DensityMinPercent;
        internal const int DensityMaxPercent = AnomalyMath.DensityMaxPercent;

        /// <summary>
        /// Percentage of non-home planets carrying an anomaly in the loaded galaxy. Derived from
        /// the seed, unless the config overrides it for playtesting.
        /// </summary>
        internal static int DensityPercent { get { return _densityPercent; } }

        private static int _densityPercent = DensityMaxPercent;


        // Which galaxy the cache below belongs to. A different seed or birth planet means a
        // different game, so everything is recomputed.
        private static int _galaxySeed;
        private static int _birthPlanetId = -1;
        private static bool _galaxyKnown;

        // planet id -> anomaly, or null for "derived, and this planet has none". Absence of the
        // key means "not yet derived".
        private static readonly Dictionary<int, PlanetAnomaly> _byPlanet = new Dictionary<int, PlanetAnomaly>();

        // Recipes an anomaly may land on, in ascending id order so the choice is stable.
        private static RecipeProto[] _eligible;

        private static bool _versionLogged;
        private static string _waitReason;

        /// <summary>Drops all state. Called when the plugin unloads.</summary>
        internal static void Reset()
        {
            _galaxySeed = 0;
            _birthPlanetId = -1;
            _galaxyKnown = false;
            _byPlanet.Clear();
            _eligible = null;
            _versionLogged = false;
            _waitReason = null;
        }

        /// <summary>
        /// Records what the plugin is still waiting for, once per distinct reason. Without this a
        /// silent empty result is indistinguishable from a hook that never fires.
        /// </summary>
        private static void Waiting(string reason)
        {
            if (_waitReason == reason)
            {
                return;
            }

            _waitReason = reason;
            Plugin.Log.LogInfo("Waiting: " + reason);
        }

        /// <summary>
        /// The anomaly on a planet, or null if it has none or the game is not ready yet. Cheap
        /// enough to call every tick: after the first call for a planet it is one dictionary
        /// lookup.
        /// </summary>
        internal static PlanetAnomaly AnomalyFor(int planetId)
        {
            if (!EnsureGalaxy())
            {
                return null;
            }

            PlanetAnomaly cached;
            if (_byPlanet.TryGetValue(planetId, out cached))
            {
                return cached;
            }

            PlanetAnomaly derived = Derive(planetId);

            // Cached either way, including null: "this planet has no anomaly" is an answer worth
            // remembering, not a reason to recompute every tick.
            _byPlanet[planetId] = derived;

            if (derived != null)
            {
                LogAnomaly(derived, planetId);
            }
            else
            {
                LogNoAnomaly(planetId);
            }

            return derived;
        }

        /// <summary>
        /// Establishes which galaxy is loaded and builds the eligible recipe list. Returns false
        /// while the game is still loading.
        /// </summary>
        private static bool EnsureGalaxy()
        {
            GameData data = GameMain.data;
            if (data == null)
            {
                Waiting("no game data yet (no save loaded).");
                return false;
            }

            GalaxyData galaxy = data.galaxy;
            if (galaxy == null)
            {
                Waiting("game data exists but the galaxy is not generated yet.");
                return false;
            }

            int birthPlanetId = galaxy.birthPlanetId;
            if (birthPlanetId <= 0)
            {
                Waiting("galaxy exists but birthPlanetId is not set yet.");
                return false;
            }

            int seed = data.gameDesc != null ? data.gameDesc.galaxySeed : 0;

            if (_galaxyKnown && _galaxySeed == seed && _birthPlanetId == birthPlanetId)
            {
                return _eligible != null;
            }

            // New game, or a different save. Nothing carries over.
            _byPlanet.Clear();
            _eligible = null;
            _galaxySeed = seed;
            _birthPlanetId = birthPlanetId;

            RecipeProtoSet recipes = LDB.recipes;
            if (recipes == null || recipes.dataArray == null || recipes.dataArray.Length == 0)
            {
                Waiting("the recipe database (LDB.recipes) is not loaded yet.");
                return false;
            }

            if (RecipeProto.recipeExecuteData == null)
            {
                Waiting("RecipeProto.recipeExecuteData is null (InitRecipeItems has not run).");
                return false;
            }

            _eligible = BuildEligibleRecipes(recipes);

            if (_eligible.Length == 0)
            {
                Plugin.Log.LogError("No eligible recipes in this build; no planet will be anomalous.");
                return false;
            }

            _densityPercent = ResolveDensity(seed);
            _galaxyKnown = true;

            LogGameVersionOnce();
            Plugin.Log.LogInfo(
                "Galaxy seed " + seed + ": " + _eligible.Length + " eligible recipes, " +
                _densityPercent + "% of non-home planets anomalous" +
                (IsDensityOverridden() ? " (forced by config)" : " (derived from the seed)") +
                ", anomaly system v" + AnomalySystemVersion + ".");

            return true;
        }

        /// <summary>
        /// Whether a planet cannot host the machines an anomaly would apply to.
        ///
        /// Only gas giants today. If the planet cannot be looked up we assume it is buildable:
        /// wrongly skipping a real planet is worse than the marker we are trying to avoid.
        /// </summary>
        private static bool IsUnbuildable(int planetId)
        {
            GameData data = GameMain.data;
            if (data == null || data.galaxy == null)
            {
                return false;
            }

            PlanetData planet = data.galaxy.PlanetById(planetId);
            return planet != null && planet.type == EPlanetType.Gas;
        }

        /// <summary>
        /// The anomaly density for this galaxy: the config value when it is set to something
        /// other than -1, otherwise a value drawn from the seed within the range above.
        /// </summary>
        private static int ResolveDensity(int seed)
        {
            if (IsDensityOverridden())
            {
                return Plugin.AnomalyChancePercent.Value;
            }

            return AnomalyMath.DensityFor(seed, AnomalySystemVersion);
        }

        private static bool IsDensityOverridden()
        {
            return Plugin.AnomalyChancePercent != null && Plugin.AnomalyChancePercent.Value >= 0;
        }

        /// <summary>
        /// Works out a planet's anomaly from the seed. Pure: same inputs, same answer, every load.
        /// </summary>

        private static PlanetAnomaly Derive(int planetId)
        {
            // Home planets never have anomalies. The player would meet one before the star map
            // exists to explain it, and anomalies should be a reason to look at other worlds
            // rather than a property of the world you start on. See PRODUCT.md.
            if (planetId == _birthPlanetId)
            {
                return null;
            }

            // Gas giants cannot host assemblers -- they take orbital collectors and nothing else --
            // so an anomaly there could never be realised. It would burn a slot and, worse, the
            // star map would advertise something the player cannot use.
            //
            // This filter is applied here rather than inside AnomalyMath, and that distinction
            // matters: presence is an independent per-planet draw, so excluding gas giants removes
            // their anomalies without moving any other planet. Every non-gas-giant keeps exactly
            // what it had. The generator's arithmetic is untouched, which is why the golden test
            // correctly does not fire.
            if (IsUnbuildable(planetId))
            {
                return null;
            }

            if (!AnomalyMath.IsAnomalous(_galaxySeed, planetId, AnomalySystemVersion, _densityPercent))
            {
                return null;
            }

            RecipeProto recipe = ChooseRecipe(planetId);
            if (recipe == null)
            {
                return null;
            }

            RecipeExecuteData shared;
            if (!RecipeProto.recipeExecuteData.TryGetValue(recipe.ID, out shared) || shared == null)
            {
                Waiting("no execute data cached for recipe " + recipe.ID + " yet.");
                return null;
            }

            if (shared.products == null || shared.productCounts == null ||
                shared.requires == null || shared.requireCounts == null)
            {
                Plugin.Log.LogError(
                    "Recipe " + recipe.ID + " has incomplete execute data; refusing to modify it.");
                return null;
            }

            return new PlanetAnomaly(
                planetId, recipe.ID, OutputMultiplier, BuildAnomalousExecuteData(shared));
        }

        /// <summary>
        /// Picks the planet's recipe by rendezvous hashing: every eligible recipe is weighted for
        /// this planet, and the heaviest wins.
        ///
        /// The obvious alternative -- hash once and index into the list -- makes the choice depend
        /// on the list's *length and order*. Adding or removing a single recipe then shifts the
        /// index for every planet in the galaxy, silently rewriting anomalies people had already
        /// discovered. That is not hypothetical: LDBTool and CommonAPI exist to add protos, and a
        /// DSP update can too.
        ///
        /// Weighting each recipe independently removes that coupling. A newly added recipe wins
        /// only on the planets where its weight happens to be highest, roughly one in N, and every
        /// other planet keeps exactly what it had. Removing one only affects the planets that had
        /// chosen it.
        ///
        /// O(N) per planet, N being the eligible recipe count, and computed once per planet then
        /// cached -- so it costs nothing that matters.
        /// </summary>
        private static RecipeProto ChooseRecipe(int planetId)
        {
            int[] ids = new int[_eligible.Length];
            for (int i = 0; i < _eligible.Length; i++)
            {
                ids[i] = _eligible[i].ID;
            }

            int chosen = AnomalyMath.ChooseRecipeId(_galaxySeed, planetId, AnomalySystemVersion, ids);
            if (chosen < 0)
            {
                return null;
            }

            for (int i = 0; i < _eligible.Length; i++)
            {
                if (_eligible[i].ID == chosen)
                {
                    return _eligible[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Recipes an anomaly may land on: ordinary machine production with exactly one output
        /// item.
        ///
        /// "One output" means one output *type*, not a count of one -- a recipe producing 2
        /// graphene is eligible and becomes 20. Recipes producing several different items, such as
        /// oil refining, are excluded, which is what SPEC's preferred option asks for: multiplying
        /// a single number is unambiguous, and multi-output semantics are not worth solving yet.
        ///
        /// Types are restricted to those that actually run through AssemblerComponent.
        /// fractionators, research, ray receivers and the rest use different machinery, so the
        /// seam this mod relies on would not apply to them.
        /// </summary>
        private static RecipeProto[] BuildEligibleRecipes(RecipeProtoSet recipes)
        {
            List<RecipeProto> eligible = new List<RecipeProto>();
            RecipeProto[] all = recipes.dataArray;

            for (int i = 0; i < all.Length; i++)
            {
                if (IsEligible(all[i]))
                {
                    eligible.Add(all[i]);
                }
            }

            // Ascending id, so the list does not depend on how the game happened to load its
            // protos. Selection no longer depends on this order -- see ChooseRecipe -- but a
            // stable order keeps logs and diagnostics reproducible.
            eligible.Sort(delegate(RecipeProto a, RecipeProto b) { return a.ID.CompareTo(b.ID); });

            return eligible.ToArray();
        }

        private static bool IsEligible(RecipeProto recipe)
        {
            if (recipe == null)
            {
                return false;
            }

            if (recipe.Type != ERecipeType.Smelt &&
                recipe.Type != ERecipeType.Assemble &&
                recipe.Type != ERecipeType.Chemical)
            {
                return false;
            }

            return recipe.Items != null && recipe.Items.Length >= 1
                && recipe.ItemCounts != null && recipe.ItemCounts.Length == recipe.Items.Length
                && recipe.Results != null && recipe.Results.Length == 1
                && recipe.ResultCounts != null && recipe.ResultCounts.Length == 1
                && recipe.ResultCounts[0] > 0;
        }

        /// <summary>
        /// Copies the shared execute data and multiplies the product counts. Every array is
        /// copied: sharing even one of them with the global data would leak the anomaly to every
        /// planet, which is the exact failure this project forbids.
        /// </summary>
        private static RecipeExecuteData BuildAnomalousExecuteData(RecipeExecuteData shared)
        {
            int[] productCounts = new int[shared.productCounts.Length];
            for (int i = 0; i < productCounts.Length; i++)
            {
                productCounts[i] = shared.productCounts[i] * OutputMultiplier;
            }

            return new RecipeExecuteData(
                (int[])shared.requires.Clone(),
                (int[])shared.requireCounts.Clone(),
                (int[])shared.products.Clone(),
                productCounts,
                shared.timeSpend,
                shared.extraTimeSpend,
                shared.productive);
        }

        /// <summary>
        /// A short, player-facing description of the anomaly on a planet, or null if it has none.
        /// Deliberately not the log format: the log carries raw proto names and numeric ids so it
        /// stays reproducible across languages, and none of that belongs in front of a player.
        ///
        /// Precise for now -- "Graphene: 2 -> 20". PRODUCT.md anticipates softening this to a
        /// qualitative phrase later, so the wording lives in this one place.
        /// </summary>
        internal static string DescribeForPlanet(int planetId)
        {
            PlanetAnomaly anomaly = AnomalyFor(planetId);
            if (anomaly == null)
            {
                return null;
            }

            RecipeProtoSet recipes = LDB.recipes;
            if (recipes == null || !recipes.Exist(anomaly.RecipeId))
            {
                return null;
            }

            RecipeProto recipe = recipes.Select(anomaly.RecipeId);
            if (recipe == null || recipe.Results == null || recipe.ResultCounts == null)
            {
                return null;
            }

            string body = "";
            for (int i = 0; i < recipe.Results.Length && i < recipe.ResultCounts.Length; i++)
            {
                if (i > 0)
                {
                    body += "\n";
                }

                int normal = recipe.ResultCounts[i];

                // The arrow, multiplication sign and middot used in this project are literal UTF-8.
                // csc reads them correctly and they render correctly in game -- both verified. Keep
                // to characters already proven here rather than introducing untested ones.
                body += PlayerFacingItemName(recipe.Results[i]) + ": " +
                        normal + " → " + (normal * anomaly.OutputMultiplier);
            }

            if (body.Length == 0)
            {
                return null;
            }

            return "ANOMALY\n" + body;
        }

        /// <summary>
        /// A one-line description for cramped surfaces such as star map labels: the affected item
        /// and the multiplier, e.g. "Titanium Crystal ×10". Null when the planet has no anomaly.
        ///
        /// Separate from DescribeForPlanet, which is two lines and belongs where there is room.
        /// </summary>
        internal static string ShortDescribeForPlanet(int planetId)
        {
            PlanetAnomaly anomaly = AnomalyFor(planetId);
            if (anomaly == null)
            {
                return null;
            }

            RecipeProtoSet recipes = LDB.recipes;
            if (recipes == null || !recipes.Exist(anomaly.RecipeId))
            {
                return null;
            }

            RecipeProto recipe = recipes.Select(anomaly.RecipeId);
            if (recipe == null || recipe.Results == null || recipe.Results.Length == 0)
            {
                return null;
            }

            // Only the first product is named. Eligible recipes have exactly one, and if that ever
            // changes a star map label is not the place to explain it.
            return PlayerFacingItemName(recipe.Results[0]) + " ×" + anomaly.OutputMultiplier;
        }

        /// <summary>Localized item name with no ids, for display to the player.</summary>

        private static string PlayerFacingItemName(int itemId)
        {
            ItemProtoSet items = LDB.items;
            if (items != null && items.Exist(itemId))
            {
                ItemProto item = items.Select(itemId);
                if (item != null && !string.IsNullOrEmpty(item.name))
                {
                    return item.name;
                }
            }

            return "item " + itemId;
        }

        /// <summary>
        /// Records the build this run actually executed against, so a log can be matched back to
        /// the signatures in docs/inspection.md.
        /// </summary>
        private static void LogGameVersionOnce()
        {
            if (_versionLogged)
            {
                return;
            }

            _versionLogged = true;

            try
            {
                // GameConfig.build is not the build number -- it reads 0 at runtime.
                Version version = GameConfig.gameVersion;
                Plugin.Log.LogInfo("Game version: " + version.ToFullString());
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not read the game version: " + e.Message);
            }
        }

        private static string PlanetName(int planetId)
        {
            GameData data = GameMain.data;
            if (data != null && data.galaxy != null)
            {
                PlanetData planet = data.galaxy.PlanetById(planetId);
                if (planet != null)
                {
                    return planet.displayName;
                }
            }

            return "<unknown>";
        }

        private static void LogNoAnomaly(int planetId)
        {
            string suffix = "";
            if (planetId == _birthPlanetId) { suffix = " (home planet -- never anomalous)"; }
            else if (IsUnbuildable(planetId)) { suffix = " (gas giant -- cannot host machines)"; }
            Plugin.Log.LogInfo(
                "No anomaly: " + PlanetName(planetId) + " (planet id " + planetId + ")" + suffix + ".");
        }

        private static void LogAnomaly(PlanetAnomaly anomaly, int planetId)
        {
            RecipeProto recipe = LDB.recipes.Select(anomaly.RecipeId);
            RecipeExecuteData vanilla;
            RecipeProto.recipeExecuteData.TryGetValue(anomaly.RecipeId, out vanilla);

            Plugin.Log.LogInfo("ANOMALY");
            Plugin.Log.LogInfo("  Planet:       " + PlanetName(planetId) + " (id " + planetId + ")");
            Plugin.Log.LogInfo("  Recipe:       " + DescribeProto(recipe.name, recipe.Name, recipe.ID));
            Plugin.Log.LogInfo("  Recipe type:  " + recipe.Type);

            if (vanilla != null)
            {
                RecipeExecuteData here = anomaly.AnomalousExecuteData;
                Plugin.Log.LogInfo("  Normally:     " + DescribeSide(vanilla.requires, vanilla.requireCounts) +
                                   " -> " + DescribeSide(vanilla.products, vanilla.productCounts));
                Plugin.Log.LogInfo("  Here:         " + DescribeSide(here.requires, here.requireCounts) +
                                   " -> " + DescribeSide(here.products, here.productCounts));
            }

            Plugin.Log.LogInfo("  Effect:       output x" + OutputMultiplier + " on this planet only");
        }

        /// <summary>
        /// Logs, once per planet, that an anomaly was actually attached to real machines. This is
        /// the line that proves the swap happened rather than the anomaly merely being derived.
        /// </summary>
        internal static void NoteApplied(PlanetData planet, int machineCount)
        {
            string planetName = planet != null ? planet.displayName : "<unknown>";
            int planetId = planet != null ? planet.id : -1;

            Plugin.Log.LogInfo(
                "Anomaly attached to " + machineCount + (machineCount == 1 ? " machine on " : " machines on ") +
                planetName + " (planet id " + planetId + "). " +
                (machineCount == 1 ? "Its" : "Their") + " output is now x" + OutputMultiplier + ".");
        }

        private static string DescribeSide(int[] itemIds, int[] counts)
        {
            string result = "";
            for (int i = 0; i < itemIds.Length; i++)
            {
                if (i > 0)
                {
                    result += " + ";
                }

                result += counts[i] + " x " + DescribeItem(itemIds[i]);
            }

            return result;
        }

        private static string DescribeItem(int itemId)
        {
            ItemProtoSet items = LDB.items;
            if (items != null && items.Exist(itemId))
            {
                ItemProto item = items.Select(itemId);
                if (item != null)
                {
                    return DescribeProto(item.name, item.Name, itemId);
                }
            }

            return "item " + itemId;
        }

        /// <summary>
        /// Prefers the localized display name but always carries the raw name and id, so the log
        /// stays reproducible no matter which language the game is running in.
        /// </summary>
        private static string DescribeProto(string localizedName, string rawName, int id)
        {
            string shown = !string.IsNullOrEmpty(localizedName) ? localizedName : rawName;
            if (string.IsNullOrEmpty(shown))
            {
                return "id " + id;
            }

            if (!string.IsNullOrEmpty(rawName) && rawName != shown)
            {
                return shown + " [" + rawName + ", id " + id + "]";
            }

            return shown + " [id " + id + "]";
        }
    }
}
