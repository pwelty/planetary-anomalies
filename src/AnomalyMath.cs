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
    /// - the eligible recipe pool gaining or losing entries, whether from a DSP update or another
    ///   mod. Rendezvous selection keeps that to roughly one planet in N per recipe added, rather
    ///   than nearly all of them;
    /// - an explicit <c>AnomalySystemVersion</c> bump, which is what that constant is for.
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
