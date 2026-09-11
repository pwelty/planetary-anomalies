using System;
using System.Collections.Generic;
using System.Reflection;
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
    /// speaks at the exact moment it starts to. That moment is a better one than discovery, because
    /// it is when you can act.
    ///
    /// Only planets already scanned are named. Announcing anomalies on worlds the player has never
    /// visited would be the answer key with extra steps.
    /// </summary>
    internal static class TechUnlockPatch
    {
        private static bool _errorLogged;

        /// <summary>
        /// Technologies already announced this session. NotifyTechUnlock fires per level, and
        /// multi-level technologies would otherwise repeat themselves for as long as you research.
        /// </summary>
        private static readonly HashSet<int> _announced = new HashSet<int>();

        /// <summary>How many worlds to name before summarising the rest.</summary>
        private const int MaxNamed = 3;

        internal static void Reset()
        {
            _announced.Clear();
        }

        /// <summary>
        /// Postfix rather than the onTechUnlocked event, so the subscription cannot outlive the
        /// plugin: Harmony removes this on unpatch, where a stray event handler would keep firing
        /// against a manager that had been reset.
        ///
        /// Safe against load: GameHistoryData.Import does not call the unlock path, so loading a
        /// save with two hundred technologies already researched announces nothing.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameHistoryData), "NotifyTechUnlock")]
        internal static void AfterTechUnlock(int _techId)
        {
            try
            {
                if (Plugin.AnnounceOnResearch != null && !Plugin.AnnounceOnResearch.Value)
                {
                    return;
                }

                if (_announced.Contains(_techId))
                {
                    return;
                }
                _announced.Add(_techId);

                TechProtoSet techs = LDB.techs;
                if (techs == null || !techs.Exist(_techId))
                {
                    return;
                }

                TechProto tech = techs.Select(_techId);
                if (tech == null || tech.UnlockRecipes == null)
                {
                    return;
                }

                List<string> messages = new List<string>();
                for (int i = 0; i < tech.UnlockRecipes.Length; i++)
                {
                    string message = MessageFor(tech.UnlockRecipes[i]);
                    if (message != null)
                    {
                        Plugin.Log.LogInfo("Announced on research: " + message);
                        messages.Add(message);
                    }
                }

                if (messages.Count > 0)
                {
                    Show(messages);
                }
            }
            catch (Exception e)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    Plugin.Log.LogError("Failed to announce anomalies for a technology: " + e);
                }
            }
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

        /// <summary>
        /// Seconds an announcement stays fully readable.
        ///
        /// The first version used the game's realtime tip exactly as it comes, and Paul's verdict
        /// from play was "cool but REALLY fast -- I couldn't read it all". The game's duration is
        /// right for what the game uses that tip for, a few words like "can't build here" beside
        /// the cursor: SetText gives it a lifeTime of 1 and UIRealtimeTip.Update burns that at two
        /// thirds per second, so it is gone in 1.5 seconds and fully opaque for about 1.3 of them.
        /// A sentence naming a recipe and two planets needs several times that.
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
        /// Shows a technology's announcements one after another rather than all at once. Every tip
        /// appears at the cursor; at the game's fast drift simultaneous tips pull apart on their
        /// own, but slowed down enough to read they would sit on top of each other.
        ///
        /// Still the game's own realtime tip, not a message box: this is worth noticing, not worth
        /// interrupting for.
        /// </summary>
        private static void Show(List<string> messages)
        {
            for (int i = 0; i < messages.Count; i++)
            {
                try
                {
                    // One sound per technology, not one per line.
                    UIRealtimeTip.Popup(messages[i], i == 0, 0);
                    Linger(messages[i], i);
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
        }

        /// <summary>
        /// Stretches the tip the game just created for this message and queues it behind earlier
        /// ones. Only this mod's tips are touched; the game's own keep their normal duration.
        ///
        /// Two of the fields involved are not public and are reached by name, which the compiler
        /// cannot check, so verify.ps1 asserts both. If either ever goes missing this does nothing
        /// and the tip shows for the game's default 1.5 seconds: short again, but not broken.
        /// </summary>
        private static void Linger(string message, int queuePosition)
        {
            UIRoot root = UIRoot.instance;
            if (root == null || root.uiGame == null || root.uiGame.generalTips == null)
            {
                return;
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
                return;
            }

            List<UIRealtimeTip> tips = _realtimeTipsField.GetValue(root.uiGame.generalTips) as List<UIRealtimeTip>;
            if (tips == null)
            {
                return;
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

                // Hidden while delayTime counts down, and its lifetime only starts burning once that
                // reaches zero -- so this queues the tip without shortening it.
                tip.delayTime = queuePosition * (ReadSeconds + GapSeconds);
                return;
            }
        }
    }
}
