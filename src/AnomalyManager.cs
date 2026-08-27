namespace PlanetaryAnomalies
{
    /// <summary>
    /// Works out the single Stage 0 anomaly once per galaxy and hands it to the patch.
    ///
    /// The anomaly is built as a private <see cref="RecipeExecuteData"/> instance rather than by
    /// editing anything shared. See docs/inspection.md for why that is the seam.
    /// </summary>
    internal static class AnomalyManager
    {
        /// <summary>Stage 0 multiplier. Nothing depends on the exact value; 10 is just unmistakable.</summary>
        internal const int OutputMultiplier = 10;

        /// <summary>
        /// The recipe we expect to be the starting iron-ore-to-iron-ingot smelt.
        ///
        /// Recipe ids cannot be read off disk (DSP keeps its proto database inside Unity assets),
        /// so this hard-coded id is treated as a guess and verified against the loaded database
        /// before use. If it does not hold in this build we fall back to the lowest-id
        /// single-input/single-output smelting recipe and say so loudly in the log.
        /// </summary>
        internal const int ExpectedIronIngotRecipeId = 1;

        private static PlanetAnomaly _current;

        // Which galaxy we last resolved for, so a new game or a different save re-resolves.
        private static int _resolvedSeed;
        private static int _resolvedPlanetId = -1;
        private static bool _resolveFailed;
        private static bool _applicationLogged;

        /// <summary>Drops all state. Called when the plugin unloads.</summary>
        internal static void Reset()
        {
            _current = null;
            _resolvedSeed = 0;
            _resolvedPlanetId = -1;
            _resolveFailed = false;
            _applicationLogged = false;
        }

        /// <summary>
        /// Returns the anomaly for the running galaxy, or null if there is no game loaded yet or
        /// the game data needed to build it is not ready. Cheap enough for the per-tick patch:
        /// after the first success it is a handful of field reads and two integer compares.
        /// </summary>
        internal static PlanetAnomaly Resolve()
        {
            GameData data = GameMain.data;
            if (data == null)
            {
                return null;
            }

            GalaxyData galaxy = data.galaxy;
            if (galaxy == null)
            {
                return null;
            }

            int birthPlanetId = galaxy.birthPlanetId;
            if (birthPlanetId <= 0)
            {
                return null;
            }

            int seed = data.gameDesc != null ? data.gameDesc.galaxySeed : 0;

            if (_resolvedPlanetId == birthPlanetId && _resolvedSeed == seed)
            {
                // Already settled for this galaxy, one way or the other.
                return _resolveFailed ? null : _current;
            }

            bool permanentFailure;
            PlanetAnomaly anomaly = Establish(galaxy, birthPlanetId, seed, out permanentFailure);

            if (anomaly != null)
            {
                _current = anomaly;
                _resolvedSeed = seed;
                _resolvedPlanetId = birthPlanetId;
                _resolveFailed = false;
                _applicationLogged = false;
                return anomaly;
            }

            if (permanentFailure)
            {
                // Remember the failure so we complain once rather than every tick.
                _current = null;
                _resolvedSeed = seed;
                _resolvedPlanetId = birthPlanetId;
                _resolveFailed = true;
            }

            // Otherwise the proto database simply is not loaded yet; try again next tick.
            return null;
        }

        /// <summary>
        /// Logs, once per galaxy, that the anomaly has actually been attached to a real machine.
        /// This is the line that proves the guard fired on the intended planet.
        /// </summary>
        internal static void NoteApplied(PlanetData planet, int assemblerId)
        {
            if (_applicationLogged)
            {
                return;
            }

            _applicationLogged = true;

            string planetName = planet != null ? planet.displayName : "<unknown>";
            int planetId = planet != null ? planet.id : -1;

            Plugin.Log.LogInfo(
                "Anomaly attached to assembler #" + assemblerId +
                " on " + planetName + " (planet id " + planetId + "). " +
                "This machine's output is now x" + OutputMultiplier + ".");
        }

        private static PlanetAnomaly Establish(GalaxyData galaxy, int birthPlanetId, int seed, out bool permanentFailure)
        {
            permanentFailure = false;

            RecipeProtoSet recipes = LDB.recipes;
            if (recipes == null || recipes.dataArray == null || recipes.dataArray.Length == 0)
            {
                // Proto database not loaded yet. Not a failure.
                return null;
            }

            RecipeProto recipe = SelectSmelterRecipe(recipes);
            if (recipe == null)
            {
                Plugin.Log.LogError(
                    "No single-input/single-output smelting recipe exists in this build. " +
                    "Stage 0 cannot proceed; no machine will be modified.");
                permanentFailure = true;
                return null;
            }

            if (RecipeProto.recipeExecuteData == null)
            {
                return null;
            }

            RecipeExecuteData shared;
            if (!RecipeProto.recipeExecuteData.TryGetValue(recipe.ID, out shared) || shared == null)
            {
                // RecipeProto.InitRecipeItems has not run yet.
                return null;
            }

            if (shared.products == null || shared.productCounts == null ||
                shared.requires == null || shared.requireCounts == null)
            {
                Plugin.Log.LogError(
                    "Recipe " + recipe.ID + " has incomplete execute data; refusing to modify it.");
                permanentFailure = true;
                return null;
            }

            RecipeExecuteData anomalousData = BuildAnomalousExecuteData(shared);

            PlanetData planet = galaxy.PlanetById(birthPlanetId);
            string planetName = planet != null ? planet.displayName : "<not generated yet>";

            LogAnomaly(recipe, shared, anomalousData, birthPlanetId, planetName, seed);

            return new PlanetAnomaly(birthPlanetId, recipe.ID, OutputMultiplier, anomalousData);
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
        /// Returns the hard-coded recipe if the installed build agrees it is a simple smelt,
        /// otherwise the lowest-id recipe that is.
        /// </summary>
        private static RecipeProto SelectSmelterRecipe(RecipeProtoSet recipes)
        {
            if (recipes.Exist(ExpectedIronIngotRecipeId))
            {
                RecipeProto expected = recipes.Select(ExpectedIronIngotRecipeId);
                if (IsSingleOutputSmelt(expected))
                {
                    return expected;
                }
            }

            Plugin.Log.LogWarning(
                "Recipe id " + ExpectedIronIngotRecipeId + " is not a single-input/single-output " +
                "smelting recipe in this build. Falling back to the lowest-id recipe that is.");

            RecipeProto best = null;
            RecipeProto[] all = recipes.dataArray;
            for (int i = 0; i < all.Length; i++)
            {
                RecipeProto candidate = all[i];
                if (!IsSingleOutputSmelt(candidate))
                {
                    continue;
                }

                if (best == null || candidate.ID < best.ID)
                {
                    best = candidate;
                }
            }

            return best;
        }

        private static bool IsSingleOutputSmelt(RecipeProto recipe)
        {
            return recipe != null
                && recipe.Type == ERecipeType.Smelt
                && recipe.Items != null && recipe.Items.Length == 1
                && recipe.ItemCounts != null && recipe.ItemCounts.Length == 1
                && recipe.Results != null && recipe.Results.Length == 1
                && recipe.ResultCounts != null && recipe.ResultCounts.Length == 1;
        }

        private static void LogAnomaly(
            RecipeProto recipe,
            RecipeExecuteData vanilla,
            RecipeExecuteData anomalous,
            int planetId,
            string planetName,
            int seed)
        {
            Plugin.Log.LogInfo("ANOMALY");
            Plugin.Log.LogInfo("  Galaxy seed:  " + seed);
            Plugin.Log.LogInfo("  Planet:       " + planetName + " (home planet, id " + planetId + ")");
            Plugin.Log.LogInfo("  Recipe:       " + DescribeProto(recipe.name, recipe.Name, recipe.ID));
            Plugin.Log.LogInfo("  Recipe type:  " + recipe.Type);
            Plugin.Log.LogInfo("  Normally:     " + DescribeSide(vanilla.requires, vanilla.requireCounts) +
                               " -> " + DescribeSide(vanilla.products, vanilla.productCounts));
            Plugin.Log.LogInfo("  Here:         " + DescribeSide(anomalous.requires, anomalous.requireCounts) +
                               " -> " + DescribeSide(anomalous.products, anomalous.productCounts));
            Plugin.Log.LogInfo("  Effect:       output x" + OutputMultiplier + " on this planet only");
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
