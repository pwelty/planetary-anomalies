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

        /// <summary>
        /// Percentage of non-home planets that carry an anomaly.
        ///
        /// Deliberately high, which supersedes SPEC's "sparse distribution". Rare anomalies are
        /// rarely worth reorganising production around, so the mechanic would barely touch how the
        /// game is played. Common ones make most worlds a candidate for specialising something,
        /// which is what makes galaxy-wide exploration and interstellar logistics matter. Not 100:
        /// ordinary planets are what give anomalous ones contrast.
        /// </summary>
        /// <summary>
        /// The range the per-galaxy anomaly density is drawn from, when it is not overridden.
        ///
        /// Density is itself derived from the seed, so galaxies differ from one another and not
        /// just planet-by-planet: some are anomaly-rich, some sparse. That is one more thing a
        /// seed means. The floor is well above zero because a galaxy with almost no anomalies is
        /// a galaxy where this mod does nothing.
        /// </summary>
        internal const int DensityMinPercent = 25;
        internal const int DensityMaxPercent = 75;

        /// <summary>
        /// Percentage of non-home planets carrying an anomaly in the loaded galaxy. Derived from
        /// the seed, unless the config overrides it for playtesting.
        /// </summary>
        internal static int DensityPercent { get { return _densityPercent; } }

        private static int _densityPercent = DensityMaxPercent;

        // Distinct salts so "is this planet anomalous" and "which recipe" do not correlate. Two
        // draws from one hash would tie the choice of recipe to the fact of having one.
        private const uint SaltPresence = 0x9E3779B9u;
        private const uint SaltRecipe = 0x85EBCA6Bu;
        private const uint SaltDensity = 0xC2B2AE35u;

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
        /// The anomaly density for this galaxy: the config value when it is set to something
        /// other than -1, otherwise a value drawn from the seed within the range above.
        /// </summary>
        private static int ResolveDensity(int seed)
        {
            if (IsDensityOverridden())
            {
                return Plugin.AnomalyChancePercent.Value;
            }

            uint span = (uint)(DensityMaxPercent - DensityMinPercent + 1);

            // Planet id 0 is not a real planet, so it is free to use as the "whole galaxy" key.
            return DensityMinPercent + (int)(Hash(seed, 0, AnomalySystemVersion, SaltDensity) % span);
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

            uint presence = Hash(_galaxySeed, planetId, AnomalySystemVersion, SaltPresence);
            if (presence % 100u >= (uint)_densityPercent)
            {
                return null;
            }

            uint choice = Hash(_galaxySeed, planetId, AnomalySystemVersion, SaltRecipe);
            RecipeProto recipe = _eligible[(int)(choice % (uint)_eligible.Length)];

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
        /// A deterministic hash. Deliberately not String.GetHashCode or Random: neither is
        /// guaranteed stable across runtimes or versions, and this value must produce the same
        /// galaxy forever.
        ///
        /// FNV-1a over the four inputs, then an avalanche step, so that neighbouring planet ids --
        /// which is exactly what planets in one system are -- do not produce related results.
        /// </summary>
        private static uint Hash(int seed, int planetId, int version, uint salt)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = MixBytes(h, (uint)seed);
                h = MixBytes(h, (uint)planetId);
                h = MixBytes(h, (uint)version);
                h = MixBytes(h, salt);

                h ^= h >> 16;
                h *= 2246822507u;
                h ^= h >> 13;
                h *= 3266489909u;
                h ^= h >> 16;
                return h;
            }
        }

        private static uint MixBytes(uint h, uint value)
        {
            unchecked
            {
                for (int i = 0; i < 4; i++)
                {
                    h ^= (value >> (i * 8)) & 0xFFu;
                    h *= 16777619u;
                }

                return h;
            }
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

            // Ascending id, so the list order does not depend on how the game happened to load its
            // protos. The recipe choice indexes into this, so its order is part of the seed
            // contract: reordering it silently changes every galaxy.
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

                // → is a right arrow, written escaped so the source file stays pure ASCII and
                // cannot be mangled by the compiler's source encoding.
                body += PlayerFacingItemName(recipe.Results[i]) + ": " +
                        normal + " → " + (normal * anomaly.OutputMultiplier);
            }

            if (body.Length == 0)
            {
                return null;
            }

            return "ANOMALY\n" + body;
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
            string suffix = planetId == _birthPlanetId ? " (home planet -- never anomalous)" : "";
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
                "Anomaly attached to " + machineCount + " machine(s) on " + planetName +
                " (planet id " + planetId + "). Their output is now x" + OutputMultiplier + ".");
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
