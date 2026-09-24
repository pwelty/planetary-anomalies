namespace PlanetaryAnomalies
{
    /// <summary>
    /// The generator, as pure arithmetic. Nothing here touches a game type, so it can be compiled
    /// and tested outside Dyson Sphere Program -- which is the point.
    ///
    /// **This is a compatibility contract, not an implementation detail.**
    ///
    /// A galaxy's identity -- which planets are anomalous, and which recipe each one owns -- is
    /// whatever these functions return. Change any of them and every existing galaxy silently
    /// becomes a different galaxy. That happened once, on 2026-08-28, when recipe selection moved
    /// from list indexing to rendezvous hashing: the same planets stayed anomalous but every
    /// recipe changed, and a player's "titanium crystal world" quietly became something else.
    ///
    /// The rule that came out of it: **a mod update must never move an existing galaxy.** Adding
    /// recipes to the eligible pool is a deliberate act with an understood cost; shipping a new
    /// version is not.
    ///
    /// `scripts/verify.ps1` compiles this file on its own and checks its output against
    /// `tests/golden-generator.txt`. Any change that moves a single result fails the build and
    /// names what moved. If you are here to change one of these functions, that failure is the
    /// system working -- go and read `ROADMAP.md` on version pinning before overriding it.
    ///
    /// What may still legitimately change a galaxy:
    ///
    /// - the player choosing another ruleset, which is what <c>AnomalyRules</c> is for;
    /// - a recipe in ruleset 1's list disappearing from the game, or ceasing to be eligible.
    ///   Nothing the mod can do keeps a planet on a recipe the game no longer has; rendezvous
    ///   selection keeps the damage to the planets that carried it, and the log says so.
    ///
    /// What no longer can, under ruleset 1: a game update or another mod *adding* recipes. Until
    /// 0.5.1 that moved roughly one planet in N per recipe added, and Dyson Sphere Program 0.10.35
    /// did exactly that with Dark Fog Lens -- one planet in 127 in the first galaxy checked. So
    /// ruleset 1 draws only from <see cref="RulesetOnePool"/>. Ruleset 2 is the same arithmetic
    /// drawing from whatever the game has, for players who want new recipes and accept that the
    /// next one can move a planet or two.
    /// </summary>
    internal static class AnomalyMath
    {
        internal const int DensityMinPercent = 25;
        internal const int DensityMaxPercent = 75;

        // Distinct salts so "is this planet anomalous", "which recipe", and "how dense is this
        // galaxy" are independent draws. Sharing one would correlate them.
        internal const uint SaltPresence = 0x9E3779B9u;
        internal const uint SaltRecipe = 0x85EBCA6Bu;
        internal const uint SaltDensity = 0xC2B2AE35u;

        /// <summary>
        /// The newest ruleset defined here, which is what AnomalyRules = Latest means. Ruleset 3 is
        /// 1.0's, in development: until 1.0 ships its draw may still change, and it is only locked
        /// by the golden test when it does.
        /// </summary>
        internal const int LatestRuleset = 3;

        /// <summary>
        /// The generator version a ruleset hashes with -- the number every draw below is keyed by.
        ///
        /// Rulesets 1 and 2 share version 1: they differ only in which recipes may be drawn, so
        /// switching between them moves only the planets the extra recipes take, never density or
        /// presence. A ruleset that changes the arithmetic itself -- 1.0's, ruleset 3 -- gets a
        /// version of its own, and every galaxy under it is a different galaxy.
        /// </summary>
        internal static int GeneratorVersionFor(int ruleset)
        {
            return ruleset >= 3 ? 2 : 1;
        }

        /// <summary>Whether a ruleset draws from a fixed list rather than from whatever the game has.</summary>
        internal static bool HasFixedPool(int ruleset)
        {
            return ruleset == 1;
        }

        /// <summary>Whether a recipe may be drawn under a ruleset at all. See <see cref="RulesetOnePool"/>.</summary>
        internal static bool InRulesetPool(int ruleset, int recipeId)
        {
            if (!HasFixedPool(ruleset))
            {
                return true;
            }

            return System.Array.IndexOf(RulesetOnePool, recipeId) >= 0;
        }

        /// <summary>
        /// Ruleset 1's recipes, by id: the 150 every pre-1.0 release drew from, as Dyson Sphere
        /// Program 0.10.34 defined them.
        ///
        /// Fixed in 0.5.1, after 0.10.35 added Dark Fog Lens (recipe 162) and it took a planet in
        /// the first galaxy checked -- Zavijava IV in seed 30085239, a Crystal Shell Set world until
        /// that morning. AnomalyRules = 1 promises a galaxy stays exactly as it is, and a pool that
        /// grows with the game cannot keep that promise.
        ///
        /// Recovered as 0.10.35's eligible pool minus recipe 162, because Steam updated the game a
        /// minute before a baseline could be taken on 0.10.34. Three things agree: every 0.10.34 log
        /// counted 150, 0.10.35 counts 151, and the patch notes name one new recipe.
        ///
        /// Part of the contract, like the arithmetic: the golden test prints it, so changing an id
        /// fails the build. Player exclusions still apply on top of it.
        /// </summary>
        internal static readonly int[] RulesetOnePool =
        {
            1, 2, 3, 4, 5, 6, 7, 8, 10, 11, 12, 13, 14, 15, 17, 19, 20, 21, 22, 23,
            24, 25, 26, 28, 29, 30, 31, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45,
            46, 47, 48, 49, 50, 51, 52, 53, 54, 56, 57, 59, 60, 61, 62, 63, 64, 65, 66, 67,
            68, 69, 70, 71, 72, 73, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89,
            90, 91, 92, 93, 94, 95, 96, 97, 98, 99, 100, 101, 103, 104, 105, 106, 107, 108, 109, 110,
            111, 112, 113, 114, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131,
            132, 133, 134, 135, 136, 137, 138, 139, 140, 141, 142, 143, 144, 145, 146, 147, 148, 149, 150, 151,
            152, 153, 154, 155, 156, 157, 158, 159, 160, 161
        };

        /// <summary>
        /// The percentage of non-home planets carrying an anomaly in a given galaxy, drawn from the
        /// seed so that galaxies differ from one another rather than only planet-by-planet.
        /// </summary>
        internal static int DensityFor(int seed, int version)
        {
            uint span = (uint)(DensityMaxPercent - DensityMinPercent + 1);

            // Planet id 0 is not a real planet, so it is free to use as the "whole galaxy" key.
            return DensityMinPercent + (int)(Hash(seed, 0, version, SaltDensity) % span);
        }

        /// <summary>Whether a planet carries an anomaly, given the galaxy's density.</summary>
        internal static bool IsAnomalous(int seed, int planetId, int version, int densityPercent)
        {
            return Hash(seed, planetId, version, SaltPresence) % 100u < (uint)densityPercent;
        }

        /// <summary>
        /// Picks a planet's recipe from the eligible pool by rendezvous hashing: every candidate is
        /// weighted for this planet and the heaviest wins.
        ///
        /// Selecting by index into the pool would tie the result to the pool's length and order, so
        /// adding one recipe would shift every planet in the galaxy. Weighting each candidate
        /// independently means a new recipe wins only where it is heaviest -- about one planet in N
        /// -- and every other planet keeps what it had.
        ///
        /// Returns -1 for an empty pool.
        /// </summary>
        internal static int ChooseRecipeId(int seed, int planetId, int version, int[] eligibleRecipeIds)
        {
            if (eligibleRecipeIds == null || eligibleRecipeIds.Length == 0)
            {
                return -1;
            }

            int best = -1;
            uint bestWeight = 0;

            for (int i = 0; i < eligibleRecipeIds.Length; i++)
            {
                int candidate = eligibleRecipeIds[i];
                uint weight = RecipeWeight(seed, planetId, version, candidate);

                // Ties broken by lower id, so the result never depends on iteration order.
                if (best < 0 || weight > bestWeight || (weight == bestWeight && candidate < best))
                {
                    best = candidate;
                    bestWeight = weight;
                }
            }

            return best;
        }

        /// <summary>
        /// How much more likely an early recipe is, in ruleset 3, on a world near home. Eight to
        /// one: with roughly a fifth of the pool early, about two nearby anomalies in three land on
        /// something buildable in the first hours. A starting point for play, not a finding.
        /// </summary>
        internal const int EarlyRecipeWeight = 8;

        /// <summary>
        /// What "near home" means in ruleset 3: star systems within this many light years of the
        /// starting star. Six, at Paul's call -- "like the game does".
        /// </summary>
        internal const double NearLightYears = 6.0;

        /// <summary>The game's light year, in its own position units (the star map uses the same).</summary>
        internal const double LightYear = 2400000.0;

        internal const uint SaltEntry = 0x27D4EB2Fu;

        /// <summary>
        /// Rendezvous selection where some candidates count more than others: a recipe with weight
        /// w enters w times, each entry hashed on its own, and the heaviest single entry wins. A
        /// candidate's chance is then its share of all entries.
        ///
        /// Integers on purpose. The textbook weighted rendezvous takes a logarithm of each hash, and
        /// Math.Log is not guaranteed to round the same on every machine; a galaxy that differed in
        /// its last bit between two players' CPUs would not be the same galaxy. Hashes, counts and
        /// comparisons are exact everywhere.
        /// </summary>
        internal static int ChooseRecipeIdWeighted(int seed, int planetId, int version, int[] ids, int[] weights)
        {
            if (ids == null || ids.Length == 0 || weights == null || weights.Length != ids.Length)
            {
                return -1;
            }

            int best = -1;
            uint bestWeight = 0;

            for (int i = 0; i < ids.Length; i++)
            {
                int candidate = ids[i];
                int entries = weights[i] < 1 ? 1 : weights[i];
                for (int k = 0; k < entries; k++)
                {
                    uint weight = EntryWeight(seed, planetId, version, candidate, k);

                    // Ties broken by lower id, as in ChooseRecipeId.
                    if (best < 0 || weight > bestWeight || (weight == bestWeight && candidate < best))
                    {
                        best = candidate;
                        bestWeight = weight;
                    }
                }
            }

            return best;
        }

        /// <summary>One entry's weight for <see cref="ChooseRecipeIdWeighted"/>.</summary>
        internal static uint EntryWeight(int seed, int planetId, int version, int recipeId, int entry)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = MixBytes(h, (uint)seed);
                h = MixBytes(h, (uint)planetId);
                h = MixBytes(h, (uint)version);
                h = MixBytes(h, SaltEntry);
                h = MixBytes(h, (uint)recipeId);
                h = MixBytes(h, (uint)entry);
                return Avalanche(h);
            }
        }

        /// <summary>
        /// How strongly one recipe is drawn to one planet. The weight follows the recipe's own id
        /// rather than its position in a list, which is what makes the pool's order irrelevant.
        /// </summary>
        internal static uint RecipeWeight(int seed, int planetId, int version, int recipeId)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = MixBytes(h, (uint)seed);
                h = MixBytes(h, (uint)planetId);
                h = MixBytes(h, (uint)version);
                h = MixBytes(h, SaltRecipe);
                h = MixBytes(h, (uint)recipeId);
                return Avalanche(h);
            }
        }

        /// <summary>
        /// FNV-1a over the inputs, then an avalanche step.
        ///
        /// Deliberately not <c>String.GetHashCode</c> or <c>Random</c>: neither is guaranteed
        /// stable across runtimes or versions, and this must reproduce the same galaxy forever.
        /// The avalanche matters because planets in one system have consecutive ids, so a weak mix
        /// would make a whole system share a verdict.
        /// </summary>
        internal static uint Hash(int seed, int planetId, int version, uint salt)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = MixBytes(h, (uint)seed);
                h = MixBytes(h, (uint)planetId);
                h = MixBytes(h, (uint)version);
                h = MixBytes(h, salt);
                return Avalanche(h);
            }
        }

        private static uint Avalanche(uint h)
        {
            unchecked
            {
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
    }
}
