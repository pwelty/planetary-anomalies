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

        // Recipe names or ids the player has ruled out in config, lower-cased, and which of them
        // actually matched something -- so a typo can be reported rather than silently doing nothing.
        private static HashSet<string> _exclusions;
        private static HashSet<string> _exclusionsMatched;

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
            _exclusions = null;
            _exclusionsMatched = null;
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
        /// Whether a planet's anomaly should be shown to the player at all. Every display surface
        /// asks this; production never does.
        ///
        /// An anomaly on a recipe the player has not researched is not information being withheld
        /// -- the recipe is already unavailable, so the label names something they cannot build and
        /// may not recognise. Twenty hours before particle broadband exists, "Particle Broadband
        /// x10" is noise, and noise teaches players to stop reading labels. Hiding it until the
        /// research lands makes the star map fill in as the game opens up, which is the shape the
        /// information actually has.
        ///
        /// The rule is deliberately the same on every surface rather than split between ambient
        /// ones and deliberate ones, so it can be stated in a line: knowing a planet means knowing
        /// the anomalies you can act on. The machine window needs no special case, since running a
        /// recipe implies having researched it.
        /// </summary>
        internal static bool IsDisclosed(int planetId)
        {
            PlanetAnomaly anomaly = AnomalyFor(planetId);
            if (anomaly == null)
            {
                return false;
            }

            return IsRecipeKnown(anomaly.RecipeId);
        }

        /// <summary>
        /// Whether the player has researched a recipe, using the game's own record of it --
        /// GameHistoryData.RecipeUnlocked is what DSP itself asks before offering a recipe.
        ///
        /// Fails open. If the config is off, or history is not available yet, the answer is "show
        /// it": a null reference during loading should not silently blank the whole feature.
        /// </summary>
        private static bool IsRecipeKnown(int recipeId)
        {
            if (Plugin.HideUnresearched != null && !Plugin.HideUnresearched.Value)
            {
                return true;
            }

            GameHistoryData history = GameMain.history;
            if (history == null)
            {
                return true;
            }

            return history.RecipeUnlocked(recipeId);
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

            LoadExclusions();
            _eligible = BuildEligibleRecipes(recipes);
            ReportExclusions();

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

            if (Plugin.LogEveryAnomaly != null && Plugin.LogEveryAnomaly.Value)
            {
                SurveyGalaxy(galaxy);
            }

            return true;
        }

        /// <summary>
        /// Logs every anomaly in the galaxy, star by star, ignoring whether the player has scanned
        /// anything.
        ///
        /// This deliberately bypasses discovery, so it is off by default and gated behind a config
        /// entry whose description says as much. It exists because "does this galaxy contain X
        /// anywhere?" is a real question during development -- checking a distribution, hunting a
        /// specific recipe, confirming a fix across a whole galaxy rather than the handful of
        /// planets that happen to have factories.
        ///
        /// A player who turns this on is choosing to spoil their own galaxy. That is their call to
        /// make, but it should be a deliberate one, which is why it is not a keybind.
        /// </summary>
        private static void SurveyGalaxy(GalaxyData galaxy)
        {
            if (galaxy.stars == null)
            {
                return;
            }

            int planets = 0;
            int anomalous = 0;

            Plugin.Log.LogInfo("=== GALAXY SURVEY (spoils discovery; LogEveryAnomaly is on) ===");

            // The eligible pool itself, because "no planet has X" has two very different causes:
            // X is not eligible, or X is eligible and simply was not drawn. Without this the two
            // are indistinguishable, and distinguishing them by inference wasted three loads.
            LogEligiblePool();


            for (int s = 0; s < galaxy.stars.Length; s++)
            {
                StarData star = galaxy.stars[s];
                if (star == null || star.planets == null)
                {
                    continue;
                }

                for (int p = 0; p < star.planets.Length; p++)
                {
                    PlanetData planet = star.planets[p];
                    if (planet == null)
                    {
                        continue;
                    }

                    planets++;

                    PlanetAnomaly anomaly = AnomalyFor(planet.id);
                    if (anomaly != null)
                    {
                        anomalous++;
                        Plugin.Log.LogInfo(
                            "  SURVEY  " + star.displayName + " / " + planet.displayName +
                            " (planet id " + planet.id + "): " + ShortDescribeForPlanet(planet.id));
                        continue;
                    }

                    // Planets that are not anomalous still *have* a recipe waiting for them: the
                    // recipe is chosen from (seed, planet, version, pool) and never consults
                    // density. Only whether the planet passes the presence roll depends on it.
                    //
                    // So reporting the would-be recipe alongside the roll answers a question that
                    // is otherwise unanswerable: "is there a planet in this galaxy that would carry
                    // X, and what density would surface it?" The roll is the threshold that planet
                    // needs -- set AnomalyChancePercent above it and the planet becomes anomalous.
                    string wouldBe = WouldBeRecipeName(planet.id);
                    if (wouldBe != null)
                    {
                        Plugin.Log.LogInfo(
                            "  LATENT  " + star.displayName + " / " + planet.displayName +
                            " (planet id " + planet.id + "): " + wouldBe +
                            "  [needs density > " + PresenceRoll(planet.id) + "%]");
                    }
                }
            }

            Plugin.Log.LogInfo(
                "=== SURVEY END: " + anomalous + " anomalous of " + planets + " planets (" +
                (planets > 0 ? (anomalous * 100 / planets) : 0) + "%) ===");
        }

        /// <summary>
        /// Lists every recipe an anomaly could land on, so "is X eligible?" is answerable from the
        /// log instead of by inference.
        /// </summary>
        private static void LogEligiblePool()
        {
            if (_eligible == null)
            {
                return;
            }

            string line = "";
            int onLine = 0;

            for (int i = 0; i < _eligible.Length; i++)
            {
                RecipeProto r = _eligible[i];
                string name = (r.Results != null && r.Results.Length > 0)
                    ? PlayerFacingItemName(r.Results[0])
                    : r.name;

                line += (onLine > 0 ? ", " : "") + name + " [" + r.Type + "]";
                onLine++;

                if (onLine == 6)
                {
                    Plugin.Log.LogInfo("  POOL  " + line);
                    line = "";
                    onLine = 0;
                }
            }

            if (onLine > 0)
            {
                Plugin.Log.LogInfo("  POOL  " + line);
            }

            Plugin.Log.LogInfo("  POOL  (" + _eligible.Length + " eligible recipes)");
        }

        /// <summary>
        /// The recipe a planet would carry if it were anomalous, ignoring the presence roll.
        /// Null for planets that can never be anomalous at any density -- the home planet and gas
        /// giants -- since reporting a latent recipe for those would be misleading.
        /// </summary>
        private static string WouldBeRecipeName(int planetId)
        {
            if (planetId == _birthPlanetId || IsUnbuildable(planetId) || _eligible == null)
            {
                return null;
            }

            int[] ids = new int[_eligible.Length];
            for (int i = 0; i < _eligible.Length; i++)
            {
                ids[i] = _eligible[i].ID;
            }

            int chosen = AnomalyMath.ChooseRecipeId(_galaxySeed, planetId, AnomalySystemVersion, ids);
            for (int i = 0; i < _eligible.Length; i++)
            {
                if (_eligible[i].ID != chosen || _eligible[i].Results == null || _eligible[i].Results.Length == 0)
                {
                    continue;
                }

                return PlayerFacingItemName(_eligible[i].Results[0]);
            }

            return null;
        }

        /// <summary>
        /// The planet's presence roll, 0-99. It is anomalous when this is below the galaxy's
        /// density, so the roll is exactly the density threshold that planet needs.
        /// </summary>
        private static int PresenceRoll(int planetId)
        {
            return (int)(AnomalyMath.Hash(_galaxySeed, planetId, AnomalySystemVersion, AnomalyMath.SaltPresence) % 100u);
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
        /// Types are restricted to those that actually run through AssemblerComponent:
        /// Smelt, Assemble, Chemical, Refine and Particle. Fractionators, research, ray receivers
        /// and the rest use different machinery, so this seam would not apply to them.
        ///
        /// Refine and Particle were excluded at first, on the assumption that only the three
        /// obvious types used the assembler path. That was caution rather than evidence, and the
        /// evidence was already in docs/inspection.md: `InternalUpdate`'s own output-cap logic has
        /// a dedicated branch for `Particle` and a general branch covering `Refine`, which it could
        /// not have if those machines ran elsewhere.
        ///
        /// The cost of excluding them was not neutral. Strange Matter is a Particle recipe, so no
        /// galaxy could ever produce a Strange Matter anomaly -- one of the more interesting
        /// late-game items, ruled out by an unexamined assumption rather than a decision.
        ///
        /// Widening the pool shifts roughly one planet in N per recipe added, which is the
        /// accepted cost of a pool change. It does not affect the generator's arithmetic, so the
        /// golden test correctly stays quiet.
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
                recipe.Type != ERecipeType.Chemical &&
                recipe.Type != ERecipeType.Refine &&
                recipe.Type != ERecipeType.Particle)
            {
                return false;
            }

            if (IsExcludedByPlayer(recipe))
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
        /// Whether the player has ruled this recipe out in config.
        ///
        /// The alternative was for the mod to decide which anomalies are worthless -- and it
        /// cannot. Which recipes matter depends on how someone plays: belts, sorters, wind
        /// turbines and solar panels are placed by the thousand, while a whole game needs a
        /// handful of water pumps, but that ranking shifts with play style and stage. Deciding
        /// centrally would mean exactly the universal ranking that ROADMAP.md's feature test
        /// rejects.
        ///
        /// So the mod holds no opinion and the player states theirs. Empty by default.
        /// </summary>
        private static bool IsExcludedByPlayer(RecipeProto recipe)
        {
            if (_exclusions == null || _exclusions.Count == 0)
            {
                return false;
            }

            if (_exclusions.Contains(recipe.ID.ToString()))
            {
                _exclusionsMatched.Add(recipe.ID.ToString());
                return true;
            }

            // Match on anything a player might reasonably type: the recipe's name, its raw proto
            // name, or the item it produces -- which is what the star map labels actually show, so
            // it is the name most likely to be copied from the screen.
            string[] candidates = new string[]
            {
                recipe.name,
                recipe.Name,
                PlayerFacingRecipeName(recipe)
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (string.IsNullOrEmpty(candidates[i]))
                {
                    continue;
                }

                string key = candidates[i].Trim().ToLowerInvariant();
                if (_exclusions.Contains(key))
                {
                    _exclusionsMatched.Add(key);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Parses the config exclusion list once per galaxy, and reports entries that matched
        /// nothing. A silent typo would look identical to a working exclusion, which is the kind
        /// of thing that costs someone an evening.
        /// </summary>
        private static void LoadExclusions()
        {
            _exclusions = new HashSet<string>();
            _exclusionsMatched = new HashSet<string>();

            string raw = Plugin.ExcludedRecipes != null ? Plugin.ExcludedRecipes.Value : null;
            if (string.IsNullOrEmpty(raw))
            {
                return;
            }

            string[] parts = raw.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string entry = parts[i].Trim();
                if (entry.Length > 0)
                {
                    _exclusions.Add(entry.ToLowerInvariant());
                }
            }
        }

        private static void ReportExclusions()
        {
            if (_exclusions == null || _exclusions.Count == 0)
            {
                return;
            }

            Plugin.Log.LogInfo(
                "Excluded by config: " + _exclusionsMatched.Count + " of " + _exclusions.Count + " entries matched a recipe.");

            foreach (string entry in _exclusions)
            {
                if (!_exclusionsMatched.Contains(entry))
                {
                    Plugin.Log.LogWarning(
                        "ExcludedRecipes entry \"" + entry + "\" matched no recipe. Check the spelling: " +
                        "use the item name as it appears in game, or the recipe's numeric id.");
                }
            }
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
                body += PlayerFacingRecipeName(recipe) + ": " +
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
            return PlayerFacingRecipeName(recipe) + " ×" + anomaly.OutputMultiplier;
        }

        /// <summary>
        /// Just the affected item's name, with no multiplier -- for star labels, which list several
        /// and have no room to repeat "×10" for each. Null when the planet has no anomaly.
        /// </summary>
        internal static string AnomalousItemName(int planetId)
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

            return PlayerFacingRecipeName(recipe);
        }

        /// <summary>
        /// The recipe's own player-facing name, falling back to its output item.
        ///
        /// Naming the *recipe* rather than its output matters more than it looks. DSP has several
        /// cases where two recipes produce the same item -- "Space Warper" from Graviton Lens and
        /// "Space Warper (advanced)" from Gravity Matrix, for instance. Labelling an anomaly by its
        /// output item told a player their planet boosted "Space Warper", so they built the recipe
        /// they knew, got no boost, and reasonably reported a bug. The mod was right and its label
        /// was wrong.
        ///
        /// The recipe name disambiguates because that is exactly its job in the game's own UI. For
        /// the large majority of recipes it reads identically to the item name, so this changes
        /// nothing visible except in the cases where it matters.
        /// </summary>
        private static string PlayerFacingRecipeName(RecipeProto recipe)
        {
            if (recipe == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(recipe.name))
            {
                return recipe.name;
            }

            if (recipe.Results != null && recipe.Results.Length > 0)
            {
                return PlayerFacingItemName(recipe.Results[0]);
            }

            return null;
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
