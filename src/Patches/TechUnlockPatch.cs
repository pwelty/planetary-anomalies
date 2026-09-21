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
    /// The game's tip lasts 1.5 seconds, unreadable for a sentence, so each announcement is held on
    /// screen after the game creates it. Held, not given a longer lifetime: see HoldLifetime for why
    /// the obvious way makes it invisible.
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

            /// <summary>The game this research belongs to. See <see cref="NoticeNewGame"/>.</summary>
            internal GameHistoryData History;
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
        /// Technologies already handled in the loaded game. NotifyTechUnlock fires per level, and
        /// multi-level technologies would otherwise repeat themselves for as long as you research.
        /// </summary>
        private static readonly HashSet<int> _handled = new HashSet<int>();

        /// <summary>The research history of the game all of the state below belongs to.</summary>
        private static GameHistoryData _history;

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

        /// <summary>When the test announcement may be queued; negative until the UI first can draw.</summary>
        private static float _testReadyAt = -1f;

        /// <summary>
        /// How long after the game first becomes ready the test announcement waits.
        ///
        /// The first version fired on the first frame a tip could be drawn, which is the frame the
        /// load finishes -- while the player is still watching the fade-in, not the corner of the
        /// screen. Paul launched to check it and did not see it, and the log showed it "shown on
        /// screen" at the very end of the load burst. A feature whose documented job is "check that
        /// announcements work" cannot fire while nobody is looking.
        /// </summary>
        private const float TestDelaySeconds = 8f;

        internal static void Reset()
        {
            lock (_handoffLock)
            {
                _completed.Clear();
            }

            _history = null;
            _geometryLogged = false;
            ForgetGame();
        }

        private static void ForgetGame()
        {
            _handled.Clear();
            _pending.Clear();
            _overflow = 0;
            _nextShowTime = 0f;
            _testQueued = false;
            _testReadyAt = -1f;
        }

        /// <summary>
        /// Everything remembered about research belongs to one loaded game, and a session can hold
        /// several: load a save, play, reload an earlier one. Found on the last read-through before
        /// 0.5 shipped -- the set of handled technologies lasted the whole session, so a technology
        /// finished, then finished again after reloading the save from before it, was announced the
        /// first time and met with silence the second. Reloading is ordinary in a game where a
        /// distant base can be lost while you are away.
        ///
        /// The game builds a new GameHistoryData for every load, so its identity is the signal: no
        /// hook on loading, no guess about which screen is showing. Research recorded against any
        /// other history -- the previous game's last frames, never drained because its UI had
        /// closed -- is dropped rather than announced in a galaxy it did not happen in.
        /// </summary>
        private static void NoticeNewGame()
        {
            GameHistoryData current = GameMain.history;
            if (ReferenceEquals(current, _history))
            {
                return;
            }

            _history = current;
            ForgetGame();
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
        internal static void AfterTechUnlock(GameHistoryData __instance, int _techId)
        {
            try
            {
                // The galaxy behind the main menu has labs, and they research. Not the player's.
                if (DSPGame.IsMenuDemo)
                {
                    return;
                }

                Completed entry = new Completed();
                entry.TechId = _techId;
                entry.History = __instance;
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

                RestoreReusedTips();
                HoldTips();
                NoticeNewGame();
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
                // Research from a game that is no longer the loaded one. See NoticeNewGame. Said
                // out loud, because a rule that drops announcements must never be able to do it
                // silently -- if this line ever appears for research you just watched finish, the
                // identity check is wrong, not the research.
                if (!ReferenceEquals(batch[i].History, _history))
                {
                    Plugin.Log.LogInfo("Ignored a completed technology (id " + batch[i].TechId +
                                       ") recorded against a game that is no longer the loaded one.");
                    continue;
                }

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
                "Research completed: " + techName + " (tech " + entry.TechId + ") -- " + recipeCount + " recipe(s), " +
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
        /// With TestAnnouncement on, queues one announcement a few seconds after the game is up, so
        /// the whole display path can be checked in seconds instead of waiting for research to
        /// finish. The wait is <see cref="TestDelaySeconds"/>, counted from the first moment a tip
        /// could be drawn -- see there for why it is not immediate.
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

            float now = UnityEngine.Time.realtimeSinceStartup;
            if (_testReadyAt < 0f)
            {
                _testReadyAt = now + TestDelaySeconds;
                return;
            }

            if (now < _testReadyAt)
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

        /// <summary>
        /// The lifeTime an announcement is held at while it is being read.
        ///
        /// The obvious way to make a tip last longer is a bigger lifeTime, and that was the first
        /// version here: 4.0, for six seconds. It made the tip invisible for its first four and a
        /// half. UIRealtimeTip.Update sets a tip's width to sqrt(clamp01(0.2 + 7 x (1 - lifeTime))),
        /// a pop-in that expects lifeTime to start at 1, so anything above about 1.03 is a width of
        /// zero. Paul saw only the last second and a half and called it "really hard to see" -- and
        /// once the drift below was also wrong, saw nothing at all, twice, while the log said
        /// "shown on screen" both times.
        ///
        /// So lifeTime is pinned to a value that is already full width (0.886 or less) and fully
        /// opaque (above 1/9) for as long as the announcement is being read, then released to burn
        /// down and fade the way the game intends.
        /// </summary>
        private const float HoldLifetime = 0.8f;

        /// <summary>Below this lifeTime the text starts to fade: alpha is sqrt(lifeTime) x 3, clamped.</summary>
        private const float FullAlphaFloor = 1f / 9f;

        /// <summary>The rate UIRealtimeTip.Update burns lifeTime at. verify.ps1 asserts it still is.</summary>
        private const float LifeDecayPerSecond = 0.6666666f;

        /// <summary>
        /// How long lifeTime is pinned, so that the pinned time plus the natural burn-down from
        /// <see cref="HoldLifetime"/> to where the fade begins adds up to <see cref="ReadSeconds"/>.
        /// </summary>
        private const float HoldSeconds = ReadSeconds - (HoldLifetime - FullAlphaFloor) / LifeDecayPerSecond;

        /// <summary>Pause between queued announcements, so one clearly ends before the next begins.</summary>
        private const float GapSeconds = 0.5f;

        /// <summary>
        /// The drift argument of InvokeRealtimeTip, which the game multiplies by 40 to get pixels per
        /// second. Zero: an announcement being read should stay where it was put. This was 6, on the
        /// belief that it was already pixels per second, so tips flew up the screen at 240 a second.
        /// </summary>
        private const float Drift = 0f;

        /// <summary>
        /// Where the announcement sits relative to the game's own "Research complete" notice, in
        /// panel pixels below it. That notice is where the eye already is at the moment research
        /// finishes; a tip at the cursor, which is where the game puts them, was "really hard to
        /// see" even at six seconds.
        /// </summary>
        private const float BelowNoticePixels = 44f;

        /// <summary>If the notice cannot be located, top-centre of the panel, this fraction up.</summary>
        private const float FallbackHeightFraction = 0.82f;

        /// <summary>Points added to the prefab's font size. Added, not multiplied: tips are pooled.</summary>
        private const int FontBoost = 8;

        /// <summary>The mod's colour everywhere else it writes on the star map.</summary>
        private static readonly UnityEngine.Color AnnouncementColour = new UnityEngine.Color(1f, 0.769f, 0.329f, 1f);

        private static FieldInfo _realtimeTipsField;
        private static FieldInfo _lifeTimeField;

        /// <summary>
        /// What a pooled tip looked like before this mod styled it, so it can be put back.
        ///
        /// UIGeneralTips keeps its tips in a pool and reuses inactive ones for the game's own
        /// messages, and SetText resets only the text, position and lifetime. Anything else this
        /// mod changes -- size, colour, pivot -- would otherwise turn up on the next "can't build
        /// here". So every styled tip is remembered here and restored the moment it stops showing
        /// one of ours.
        /// </summary>
        private sealed class TipStyle
        {
            internal int FontSize;
            internal UnityEngine.Color Colour;
            internal UnityEngine.TextAnchor Alignment;
            internal UnityEngine.Vector2 Pivot;
            internal string Message;

            /// <summary>Where the announcement was asked to appear, for the geometry log.</summary>
            internal UnityEngine.Vector2 Requested;

            /// <summary>realtimeSinceStartup when it was raised, and when to stop pinning its lifeTime.</summary>
            internal float ShownAt;
            internal float HoldUntil;
            internal bool GeometryLogged;
        }

        private static readonly Dictionary<UIRealtimeTip, TipStyle> _styled = new Dictionary<UIRealtimeTip, TipStyle>();
        private static readonly List<UIRealtimeTip> _restoreScratch = new List<UIRealtimeTip>();

        /// <summary>
        /// Still the game's own realtime tip, not a message box: this is worth noticing, not worth
        /// interrupting for. Placed under the research notice rather than at the cursor, and given
        /// no sound of its own -- the game has just played its research chime. Logs what actually
        /// happened on screen, not what was intended.
        /// </summary>
        private static void Show(string message)
        {
            try
            {
                UIRoot root = UIRoot.instance;
                UIGeneralTips tips = (root != null && root.uiGame != null) ? root.uiGame.generalTips : null;
                if (tips == null)
                {
                    Plugin.Log.LogWarning("No tip layer to show an announcement on: " + message);
                    return;
                }

                UnityEngine.Vector2 position = AnnouncementPosition(tips);
                tips.InvokeRealtimeTip(message, position, Drift, 0f);

                if (Linger(tips, message, position))
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
        /// Directly beneath the game's "Research complete" notice, in the coordinates the game uses
        /// for its tips: the panel's own rect, origin bottom-left. (For the cursor the game computes
        /// mouse / screen * panel size; this is the same space, taken from the notice instead.)
        /// </summary>
        private static UnityEngine.Vector2 AnnouncementPosition(UIGeneralTips tips)
        {
            UnityEngine.RectTransform panel = tips.tipPanelRect;
            if (panel == null)
            {
                return UnityEngine.Vector2.zero;
            }

            UnityEngine.Rect rect = panel.rect;

            UnityEngine.UI.Text notice = tips.researchCompleteText;
            if (notice != null && notice.rectTransform != null)
            {
                UnityEngine.Vector3 local = panel.InverseTransformPoint(notice.rectTransform.position);
                float x = local.x - rect.xMin;
                float y = local.y - rect.yMin - BelowNoticePixels;
                if (x > 0f && x < rect.width && y > 0f && y < rect.height)
                {
                    return new UnityEngine.Vector2(x, y);
                }
            }

            return new UnityEngine.Vector2(rect.width * 0.5f, rect.height * FallbackHeightFraction);
        }

        /// <summary>
        /// Finds the tip the game just created for this message, stretches it and styles it.
        /// Returns whether the tip was found. Only this mod's tips are touched, and only while they
        /// show this mod's text -- see <see cref="RestoreReusedTips"/>.
        ///
        /// lifeTime and the tip list are not public and are reached by name, which the compiler
        /// cannot check, so verify.ps1 asserts both.
        /// </summary>
        private static bool Linger(UIGeneralTips tips, string message, UnityEngine.Vector2 requested)
        {
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

            List<UIRealtimeTip> list = _realtimeTipsField.GetValue(tips) as List<UIRealtimeTip>;
            if (list == null)
            {
                return false;
            }

            for (int i = list.Count - 1; i >= 0; i--)
            {
                UIRealtimeTip tip = list[i];
                if (tip == null || tip.textComp == null || !tip.gameObject.activeSelf || tip.textComp.text != message)
                {
                    continue;
                }

                _lifeTimeField.SetValue(tip, HoldLifetime);
                Style(tip, tips.realtimeTipPrefab, message, requested);
                return true;
            }

            return false;
        }

        private static void Style(UIRealtimeTip tip, UIRealtimeTip prefab, string message, UnityEngine.Vector2 requested)
        {
            TipStyle original;
            if (!_styled.TryGetValue(tip, out original))
            {
                original = new TipStyle();
                original.FontSize = tip.textComp.fontSize;
                original.Colour = tip.textComp.color;
                original.Alignment = tip.textComp.alignment;
                original.Pivot = tip.rectTrans != null ? tip.rectTrans.pivot : new UnityEngine.Vector2(0.5f, 0.5f);
                _styled[tip] = original;
            }
            original.Message = message;

            float now = UnityEngine.Time.realtimeSinceStartup;
            original.Requested = requested;
            original.ShownAt = now;
            original.HoldUntil = now + HoldSeconds;
            original.GeometryLogged = false;

            // Sized from the prefab, never from the tip's current value: a reused tip may already
            // carry a previous boost, and boosts must not stack.
            int baseSize = prefab != null && prefab.textComp != null ? prefab.textComp.fontSize : original.FontSize;
            tip.textComp.fontSize = baseSize + FontBoost;

            // Update rewrites only the alpha each frame, so the colour survives the fade.
            UnityEngine.Color c = AnnouncementColour;
            c.a = tip.textComp.color.a;
            tip.textComp.color = c;

            // Centred on the position given, so the line sits under the notice rather than
            // starting at it and running off to the right.
            tip.textComp.alignment = UnityEngine.TextAnchor.MiddleCenter;
            if (tip.rectTrans != null)
            {
                tip.rectTrans.pivot = new UnityEngine.Vector2(0.5f, 0.5f);
            }
        }

        private static bool _geometryLogged;

        /// <summary>
        /// Every frame, for each of this mod's tips still being read: pin lifeTime at
        /// <see cref="HoldLifetime"/> until the hold is over, and once, half a second in, write down
        /// where the tip actually is on the screen.
        ///
        /// The second half exists because "shown on screen" in this log has only ever meant "the
        /// game created a tip and it is active". It said so twice while nothing could be seen. What
        /// the player sees is a rectangle in screen pixels, so that is what gets logged.
        /// </summary>
        private static void HoldTips()
        {
            if (_styled.Count == 0 || _lifeTimeField == null)
            {
                return;
            }

            float now = UnityEngine.Time.realtimeSinceStartup;
            foreach (KeyValuePair<UIRealtimeTip, TipStyle> entry in _styled)
            {
                UIRealtimeTip tip = entry.Key;
                TipStyle style = entry.Value;
                if (tip == null || tip.textComp == null || !tip.gameObject.activeSelf)
                {
                    continue;
                }

                if (now < style.HoldUntil)
                {
                    _lifeTimeField.SetValue(tip, HoldLifetime);
                }

                if (!style.GeometryLogged && !_geometryLogged && now >= style.ShownAt + 0.5f)
                {
                    style.GeometryLogged = true;
                    _geometryLogged = true;
                    LogGeometry(tip, style);
                }
            }
        }

        /// <summary>Where an announcement really is, in screen pixels. Once per session.</summary>
        private static void LogGeometry(UIRealtimeTip tip, TipStyle style)
        {
            try
            {
                UIRoot root = UIRoot.instance;
                UIGeneralTips tips = (root != null && root.uiGame != null) ? root.uiGame.generalTips : null;

                UnityEngine.RectTransform rect = tip.rectTrans;
                UnityEngine.Canvas canvas = tip.GetComponentInParent<UnityEngine.Canvas>();
                UnityEngine.Camera camera = (canvas != null && canvas.renderMode != UnityEngine.RenderMode.ScreenSpaceOverlay)
                    ? canvas.worldCamera
                    : null;

                UnityEngine.Vector3[] corners = new UnityEngine.Vector3[4];
                rect.GetWorldCorners(corners);
                UnityEngine.Vector2 low = UnityEngine.RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                UnityEngine.Vector2 high = UnityEngine.RectTransformUtility.WorldToScreenPoint(camera, corners[2]);

                int width = UnityEngine.Screen.width;
                int height = UnityEngine.Screen.height;
                bool onScreen = high.x > 0f && low.x < width && high.y > 0f && low.y < height;

                string notice = "no notice";
                string panel = "no panel";
                bool inPanel = false;
                if (tips != null)
                {
                    if (tips.researchCompleteText != null)
                    {
                        UnityEngine.Vector2 at = UnityEngine.RectTransformUtility.WorldToScreenPoint(
                            camera, tips.researchCompleteText.rectTransform.position);
                        notice = "notice at " + at + (tips.researchCompleteText.gameObject.activeInHierarchy ? " (showing)" : " (not showing)");
                    }

                    if (tips.tipPanelRect != null)
                    {
                        panel = "tip panel " + tips.tipPanelRect.rect.size;
                        inPanel = rect.parent == tips.tipPanelRect;
                    }
                }

                Plugin.Log.LogInfo(
                    "Announcement geometry (once): asked for " + style.Requested + ", tip is at " + rect.anchoredPosition +
                    ", scale " + rect.localScale + ", size " + rect.rect.size + ", pivot " + rect.pivot +
                    ", anchors " + rect.anchorMin + " to " + rect.anchorMax + "; on screen from " + low + " to " + high +
                    " of " + width + "x" + height + (onScreen ? " -- INSIDE the screen" : " -- OUTSIDE the screen") +
                    "; " + notice + "; " + panel + "; parent is the tip panel: " + inPanel +
                    "; text alpha " + tip.textComp.color.a + ", size " + tip.textComp.fontSize + ".");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not measure the announcement on screen: " + e.Message);
            }
        }

        /// <summary>
        /// Puts back any styled tip that is no longer showing one of this mod's messages -- because
        /// it faded out, or because the game reused it for a message of its own.
        /// </summary>
        private static void RestoreReusedTips()
        {
            if (_styled.Count == 0)
            {
                return;
            }

            _restoreScratch.Clear();
            foreach (KeyValuePair<UIRealtimeTip, TipStyle> entry in _styled)
            {
                UIRealtimeTip tip = entry.Key;
                bool gone = tip == null || tip.textComp == null;
                bool ours = !gone && tip.gameObject.activeSelf && tip.textComp.text == entry.Value.Message;
                if (!ours)
                {
                    _restoreScratch.Add(tip);
                }
            }

            for (int i = 0; i < _restoreScratch.Count; i++)
            {
                UIRealtimeTip tip = _restoreScratch[i];
                TipStyle original = _styled[tip];
                _styled.Remove(tip);

                if (tip == null || tip.textComp == null)
                {
                    continue;
                }

                tip.textComp.fontSize = original.FontSize;
                UnityEngine.Color c = original.Colour;
                c.a = tip.textComp.color.a;
                tip.textComp.color = c;
                tip.textComp.alignment = original.Alignment;
                if (tip.rectTrans != null)
                {
                    tip.rectTrans.pivot = original.Pivot;
                }
            }
        }
    }
}