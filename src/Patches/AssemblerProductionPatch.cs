using System.Collections.Generic;
using HarmonyLib;

namespace PlanetaryAnomalies
{
    /// <summary>
    /// Attaches each planet's anomaly to the machines it applies to.
    ///
    /// This does not patch production. DSP adds a completed cycle's output from
    /// <c>AssemblerComponent.recipeExecuteData.productCounts</c>, and that field is a per-component
    /// reference into a static, shared dictionary. So instead of intercepting the output code --
    /// which runs on two different paths, neither of which can see the planet -- we hand the
    /// affected machines a private copy of that data with the counts already multiplied. Both
    /// execution paths then produce the anomalous amount on their own, and nothing shared is
    /// touched.
    ///
    /// The hook is <c>PlanetFactory.BeforeGameTick</c>, called once per factory per tick from
    /// <c>GameLogic.FactoryBeforeGameTick</c>.
    ///
    /// Choosing this hook matters more than it looks. <c>GameLogic.OnGameLogicFrame</c> dispatches
    /// most factory work through paired methods -- a sequential one and a <c>_Parallel</c> one --
    /// and picks between them on thread count:
    ///
    ///     V_6 = threadCount &lt;= 1      -> calls FactorySystemFacilityGameTick
    ///     V_7 = threadCount &gt;= 2      -> calls FactorySystemFacilityGameTick_Parallel
    ///
    /// So a hook on <c>FactorySystem.GameTick</c> silently never fires on a multithreaded game,
    /// which is the default. <c>FactoryBeforeGameTick</c> has no <c>_Parallel</c> twin and is
    /// guarded only by "is this the main thread", so it runs in both modes. It also runs earlier
    /// in the frame than the facility phase, so the swap is in place before production for that
    /// tick. See docs/inspection.md.
    /// </summary>
    [HarmonyPatch(typeof(PlanetFactory), "BeforeGameTick")]
    internal static class PlanetFactoryBeforeGameTickPatch
    {
        /// <summary>
        /// Ticks between full sweeps of a planet's assembler pool.
        ///
        /// Most planets are anomalous now, so a sweep every tick would walk every assembler on
        /// every developed planet, sixty times a second, almost always finding nothing to do. A
        /// sweep is also triggered immediately whenever the pool's cursor moves, which is what
        /// happens when a machine is built or removed, so the only case this delays is an existing
        /// machine being switched to the anomalous recipe in place. Half a second late on that is
        /// invisible: the machine has to fill with ingredients before it can produce anything.
        /// </summary>
        private const int SweepIntervalTicks = 30;

        // Proves the hook itself fires. Without this, "nothing happened" cannot be told apart from
        // "the patch never ran", which are very different bugs -- and were, once.
        private static bool _hookProven;

        // Per planet: ticks until the next unconditional sweep, and the pool cursor as of the last
        // sweep, so a change can trigger one immediately.
        private static readonly Dictionary<int, int> _countdown = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> _lastCursor = new Dictionary<int, int>();

        // Planets whose first successful attachment has been logged, so the log carries evidence
        // without one line per machine.
        private static readonly HashSet<int> _attachLogged = new HashSet<int>();

        /// <summary>Drops per-planet state. Called when the plugin unloads.</summary>
        internal static void Reset()
        {
            _hookProven = false;
            _countdown.Clear();
            _lastCursor.Clear();
            _attachLogged.Clear();
        }

        [HarmonyPrefix]
        internal static void Prefix(PlanetFactory __instance)
        {
            PlanetData planet = __instance.planet;
            if (planet == null)
            {
                return;
            }

            if (!_hookProven)
            {
                _hookProven = true;
                Plugin.Log.LogInfo(
                    "PlanetFactory.BeforeGameTick prefix is running (first seen on " +
                    planet.displayName + ", planet id " + planet.id + ").");
            }

            PlanetAnomaly anomaly = AnomalyManager.AnomalyFor(planet.id);
            if (anomaly == null)
            {
                return;
            }

            FactorySystem system = __instance.factorySystem;
            if (system == null || system.assemblerPool == null)
            {
                return;
            }

            int cursor = system.assemblerCursor;
            if (!DueForSweep(planet.id, cursor))
            {
                return;
            }

            AssemblerComponent[] pool = system.assemblerPool;
            if (cursor > pool.Length)
            {
                cursor = pool.Length;
            }

            RecipeExecuteData anomalousData = anomaly.AnomalousExecuteData;
            int recipeId = anomaly.RecipeId;
            int attached = 0;

            for (int i = 1; i < cursor; i++)
            {
                // DSP marks live pool slots by storing the index back into id; recycled slots do not.
                if (pool[i].id != i || pool[i].recipeId != recipeId)
                {
                    continue;
                }

                // Already ours. This is the case on almost every sweep, so it is the cheap path.
                if (ReferenceEquals(pool[i].recipeExecuteData, anomalousData))
                {
                    continue;
                }

                // AssemblerComponent is a struct in an array, so this writes through to the pool.
                pool[i].recipeExecuteData = anomalousData;
                attached++;
            }

            if (attached > 0 && _attachLogged.Add(planet.id))
            {
                AnomalyManager.NoteApplied(planet, attached);
            }
        }

        /// <summary>
        /// True when this planet's pool should be walked: either the cursor moved, meaning
        /// machines were built or removed, or the interval has elapsed.
        /// </summary>
        private static bool DueForSweep(int planetId, int cursor)
        {
            int previousCursor;
            bool cursorChanged = !_lastCursor.TryGetValue(planetId, out previousCursor) || previousCursor != cursor;

            if (cursorChanged)
            {
                _lastCursor[planetId] = cursor;
                _countdown[planetId] = SweepIntervalTicks;
                return true;
            }

            int remaining;
            if (!_countdown.TryGetValue(planetId, out remaining))
            {
                _countdown[planetId] = SweepIntervalTicks;
                return true;
            }

            if (remaining <= 0)
            {
                _countdown[planetId] = SweepIntervalTicks;
                return true;
            }

            _countdown[planetId] = remaining - 1;
            return false;
        }
    }
}
