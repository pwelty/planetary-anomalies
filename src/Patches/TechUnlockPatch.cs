using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using HarmonyLib;

namespace PlanetaryAnomalies
{
    /// <summary>
    /// When a technology completes, says whether the galaxy already contains a world that makes one
    /// of its recipes ten times over.
    ///
    /// This is the other half of hiding, and the reason hiding is defensible at all. 0.4 withheld
    /// anomalies whose recipe was unresearched, which removed real noise -- and, it turned out,
    /// removed anomalies as a *reason to research*. The first galaxy to run it hid 31, overwhelmingly
    /// military, from a player who had just discovered that holding a distant world costs a war.
    ///
    /// Without this, hiding is a subtraction: information a player had in 0.3 and lost in 0.4.
    /// With it, the trade is honest -- the mod stays quiet while a recipe means nothing to you, and
    /// speaks at the exact moment it starts to.
    ///
    /// The mechanics are shaped by three findings from play, each of which first looked like "I
    /// never saw any announcements":
    ///
    /// Research completion is not assumed to arrive on the main thread. NotifyTechUnlock is called
    /// from FactorySystem.GameTickLabResearchMode under GameHistoryData.techLock; in play it has
    /// been observed on the main thread (lab research has no _Parallel twin, unlike lab produce),
    /// but the lock exists for a reason and this mod's caches are not thread-safe. So the patch on
    /// NotifyTechUnlock records the technology id and does nothing else; everything real happens on
    /// the main thread, and the log line for each completed technology says which thread reported
    /// it, so the assumption is checked on every load rather than trusted.
    ///
    /// Tips raised while the game UI is down are discarded without a word: UIRealtimeTip.Popup opens
    /// with "if (!UIRoot.instance.uiGame.active) return". So announcements wait in a queue drained
    /// from UIGeneralTips._OnUpdate, which UIGame drives every frame the tip layer is open. While the
    /// star map is up the tip layer is closed, and announcements simply wait until you leave it.
    ///
    /// The game's tip lasts 1.5 seconds, unreadable for a sentence, so each announcement is
    /// lengthened after the game creates it.
    ///
    /// Only planets already scanned are named. Announcing anomalies on worlds the player has never
    /// visited would be the answer key with extra steps.
    /// </summary>
    internal static class TechUnlockPatch
    {
        private static bool _errorLogged;
        private static bool _handoffErrorLogged;

        // ---------------------------------------------------------------------------------------
        // The handoff. Written from whichever thread finished the research; read on the main thread.
        // ---------------------------------------------------------------------------------------

        private struct Completed
        {
            internal int TechId;
            internal bool OnMainThread;
        }

        private static readonly object _handoffLock = new object();
        private static readonly List<Completed> _completed = new List<Completed>();
        private static int _mainThreadId = -1;

        /// <summary>Called from Plugin.Awake, which Unity runs on the main thread.</summary>
        internal static void CaptureMainThread()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        // ---------------------------------------------------------------------------------------
        // Main-thread state.
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Technologies already handled this session. NotifyTechUnlock fires per level, and
        /// multi-level technologies would otherwise repeat themselves for as long as you research.
        /// </summary>
        private static readonly HashSet<int> _handled = new HashSet<int>();

        private static readonly Queue<string> _pending = new Queue<string>();

        /// <summary>
        /// How many announcements wait in line before the rest become a single count. A burst of
        /// finished research should not drip for minutes; the log always has every one.
        /// </summary>
        private const int MaxQueued = 5;

        /// <summary>How many worlds to name before summarising the rest.</summary>
        private const int MaxNamed = 3;

        private static int _overflow;
        private static float _nextShowTime;
        private static bool _testQueued;

        internal static void Reset()
        {
            lock (_handoffLock)
            {
                _completed.Clear();
            }

            _handled.Clear();
            _pending.Clear();
            _overflow = 0;
            _nextShowTime = 0f;
            _testQueued = false;
        }

        /// <summary>
        /// Records that a technology finished, and nothing more.
        ///
        /// This runs while the game holds its tech write lock, on whichever thread ticked the lab --
        /// observed to be the main thread, not guaranteed to be -- so it must be trivial: no galaxy
        /// sweep, no anomaly cache, no Unity. Postfix rather than the onTechUnlocked event, so the
        /// subscription cannot outlive the plugin.
        ///
        /// Safe against load: GameHistoryData.Import does not call the unlock path, so loading a
        /// save with two hundred technologies already researched records nothing.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameHistoryData), "NotifyTechUnlock")]
        internal static void AfterTechUnlock(int _techId)
        {
            try
            {
                Completed entry = new Completed();
                entry.TechId = _techId;
                entry.OnMainThread = Thread.CurrentThread.ManagedThreadId == _mainThreadId;

                lock (_handoffLock)
                {
                    _completed.Add(entry);
                }
            }
            catch (Exception e)
            {
                // ManualLogSource is safe to call from any thread. Never throw back into the game's
                // lab tick while it holds a lock.
                if (!_handoffErrorLogged)
                {
                    _handoffErrorLogged = true;
                    Plugin.Log.LogError("Failed to record a completed technology: " + e);
                }
            }
        }

        /// <summary>
        /// Main thread, every frame the tip layer is open. Turns recorded technologies into
        /// announcements, then releases one waiting announcement when there is room on screen.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIGeneralTips), "_OnUpdate")]
        internal static void AfterTipsUpdate()
        {
            try
            {
                if (_mainThreadId < 0)
                {
                    _mainThreadId = Thread.CurrentThread.ManagedThreadId;
                }

                DrainCompleted();
                QueueTestIfAsked();

                if (_pending.Count == 0 && _overflow == 0)
                {
                    return;
                }

                if (!CanShow())
                {
                    return;
                }

                float now = UnityEngine.Time.realtimeSinceStartup;
                if (now < _nextShowTime)
                {
                    return;
                }

                string message;
                if (_pending.Count > 0)
                {
                    message = _pending.Dequeue();
                }
                else
                {
                    message = "ANOMALY: " + _overflow + " more you can now use -- see the log.";
                    _overflow = 0;
                }

                Show(message);
                _nextShowTime = now + ReadSeconds + GapSeconds;
            }
            catch (Exception e)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    Plugin.Log.LogError("Failed to process research announcements: " + e);
                }
            }
        }

        private static void DrainCompleted()
        {
            Completed[] batch;
            lock (_handoffLock)
            {
                if (_completed.Count == 0)
                {
                    return;
                }

                batch = _completed.ToArray();
                _completed.Clear();
            }

            for (int i = 0; i < batch.Length; i++)
            {
                Process(batch[i]);
            }
        }

        private static void Process(Completed entry)
        {
            if (_handled.Contains(entry.TechId))
            {
                return;
            }
            _handled.Add(entry.TechId);

            TechProtoSet techs = LDB.techs;
            if (techs == null || !techs.Exist(entry.TechId))
            {
                return;
            }

            TechProto tech = techs.Select(entry.TechId);
            if (tech == null)
            {
                return;
            }

            string techName = string.IsNullOrEmpty(tech.name) ? "technology " + entry.TechId : tech.name;
            int recipeCount = tech.UnlockRecipes != null ? tech.UnlockRecipes.Length : 0;
            bool enabled = Plugin.AnnounceOnResearch == null || Plugin.AnnounceOnResearch.Value;

            int announced = 0;
            if (enabled && tech.UnlockRecipes != null)
            {
                for (int i = 0; i < tech.UnlockRecipes.Length; i++)
                {
                    string message = MessageFor(tech.UnlockRecipes[i]);
                    if (message != null)
                    {
                        Plugin.Log.LogInfo("Announced on research: " + message);
                        Enqueue(message);
                        announced++;
                    }
                }
            }

            // Every completed technology gets a line, including the ones with nothing to say.
            // Without it, a session where research produced no anomalies looked identical in the
            // log to one where announcements were broken -- and three rounds of guessing followed.
            Plugin.Log.LogInfo(
                "Research completed: " + techName + " -- " + recipeCount + " recipe(s), " +
                (enabled ? announced + " announced" : "announcements off") +
                " (reported on the " + (entry.OnMainThread ? "main" : "a worker") + " thread).");
        }

        /// <summary>The announcement for one recipe, or null if there is nothing to say.</summary>
        private static string MessageFor(int recipeId)
        {
            string what = AnomalyManager.RecipeLabel(recipeId);
            if (string.IsNullOrEmpty(what))
            {
                return null;
            }

            int total;
            string where = AnomalyManager.KnownPlanetsWithRecipe(recipeId, MaxNamed, out total);
            if (where != null)
            {
                return "ANOMALY: " + what + " on " + where;
            }

            if (AnomalyManager.AnyUnknownPlanetWithRecipe(recipeId))
            {
                // Existence without location. The same trade Marker mode makes about a place, made
                // one level up about a recipe: knowing it is out there is a reason to go looking,
                // and finding it is still the part worth earning.
                //
                // Paul's phrasing, and deliberately the plain one rather than the joke he offered
                // alongside it. Every other line this mod writes is plain; one that is not would
                // read as a different mod talking.
                return "ANOMALY: " + what + " exists on a world you have not found.";
            }

            return null;
        }

        private static void Enqueue(string message)
        {
            if (_pending.Count < MaxQueued)
            {
                _pending.Enqueue(message);
            }
            else
            {
                _overflow++;
            }
        }

        /// <summary>
        /// With TestAnnouncement on, queues one announcement once the game is up, so the whole
        /// display path can be checked in seconds instead of waiting for research to finish.
        /// </summary>
        private static void QueueTestIfAsked()
        {
            if (_testQueued || Plugin.TestAnnouncement == null || !Plugin.TestAnnouncement.Value)
            {
                return;
            }

            if (!CanShow())
            {
                return;
            }

            _testQueued = true;
            Enqueue("ANOMALY: test announcement. If you can read this, announcements work.");
            Plugin.Log.LogInfo("TestAnnouncement is on: queued a test announcement.");
        }

        /// <summary>
        /// Whether a tip raised right now would actually be drawn. Popup discards anything raised
        /// while the game UI is down and reports nothing, so this has to be asked first.
        /// </summary>
        private static bool CanShow()
        {
            if (!GameMain.isRunning || GameMain.isPaused || GameMain.isLoading)
            {
                return false;
            }

            UIRoot root = UIRoot.instance;
            return root != null && root.uiGame != null && root.uiGame.active && root.uiGame.generalTips != null;
        }

        // ---------------------------------------------------------------------------------------
        // Drawing.
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Seconds an announcement stays fully readable.
        ///
        /// Paul's verdict on the game's own duration: "cool but REALLY fast -- I couldn't read it
        /// all". SetText gives a tip lifeTime 1 and UIRealtimeTip.Update burns that at two thirds
        /// per second, so it is gone in 1.5 seconds -- right for "can't build here", wrong for a
        /// sentence naming a recipe and two planets.
        /// </summary>
        private const float ReadSeconds = 6f;

        /// <summary>Pause between queued announcements, so one clearly ends before the next begins.</summary>
        private const float GapSeconds = 0.5f;

        /// <summary>
        /// Upward drift in pixels per second. The game's 40 suits a tip that lives 1.5 seconds; over
        /// six it would carry the text 240 pixels up the screen while you were reading it.
        /// </summary>
        private const float DriftPixelsPerSecond = 6f;

        /// <summary>The rate UIRealtimeTip.Update burns lifeTime at. verify.ps1 asserts it still is.</summary>
        private const float LifeDecayPerSecond = 0.6666666f;

        private static FieldInfo _realtimeTipsField;
        private static FieldInfo _lifeTimeField;

        /// <summary>
        /// Still the game's own realtime tip, not a message box: this is worth noticing, not worth
        /// interrupting for. Logs what actually happened on screen, not what was intended.
        /// </summary>
        private static void Show(string message)
        {
            try
            {
                UIRealtimeTip.Popup(message, true, 0);

                if (Linger(message))
                {
                    Plugin.Log.LogInfo("Shown on screen: " + message);
                }
                else
                {
                    // The game declined to draw it, or its tip list could not be read. Either way
                    // this is the line that answers "why did I not see it".
                    Plugin.Log.LogWarning("Raised a tip, but the game did not draw one: " + message);
                }
            }
            catch (Exception e)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    Plugin.Log.LogError("Could not show a tip; the announcement is in the log only: " + e);
                }
            }
        }

        /// <summary>
        /// Finds the tip the game just created for this message and stretches it. Returns whether
        /// the tip was found. Only this mod's tips are touched; the game's own keep their duration.
        ///
        /// Two of the fields involved are not public and are reached by name, which the compiler
        /// cannot check, so verify.ps1 asserts both.
        /// </summary>
        private static bool Linger(string message)
        {
            UIRoot root = UIRoot.instance;
            if (root == null || root.uiGame == null || root.uiGame.generalTips == null)
            {
                return false;
            }

            if (_realtimeTipsField == null)
            {
                _realtimeTipsField = AccessTools.Field(typeof(UIGeneralTips), "realtimeTips");
            }

            if (_lifeTimeField == null)
            {
                _lifeTimeField = AccessTools.Field(typeof(UIRealtimeTip), "lifeTime");
            }

            if (_realtimeTipsField == null || _lifeTimeField == null)
            {
                return false;
            }

            List<UIRealtimeTip> tips = _realtimeTipsField.GetValue(root.uiGame.generalTips) as List<UIRealtimeTip>;
            if (tips == null)
            {
                return false;
            }

            for (int i = tips.Count - 1; i >= 0; i--)
            {
                UIRealtimeTip tip = tips[i];
                if (tip == null || tip.textComp == null || !tip.gameObject.activeSelf || tip.textComp.text != message)
                {
                    continue;
                }

                _lifeTimeField.SetValue(tip, ReadSeconds * LifeDecayPerSecond);
                tip.upSpeed = DriftPixelsPerSecond;

                // Spacing is the queue's job, so this one shows immediately. The game may have set
                // a delay of its own if it raised a tip in the same frame.
                tip.delayTime = 0f;
                return true;
            }

            return false;
        }
    }
}
