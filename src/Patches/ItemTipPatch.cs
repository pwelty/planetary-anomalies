using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace PlanetaryAnomalies
{
    /// <summary>
    /// Adds a line to the item tooltip -- replicator, inventory, build menu, anywhere the game
    /// shows one -- naming the worlds you know of that make this item ten times over.
    ///
    /// Paul's ask, and the right place for it: every other surface tells you about a *place*; this
    /// is the first that answers a question about a *thing*, at the moment you are asking it. You
    /// hover Copper Ingot deciding where to smelt, and the tooltip says BatenKaitos I makes these
    /// ten to one. It is the notebook where you would actually consult it.
    ///
    /// Same disclosure rules as everywhere else: planets you have scanned, recipes you have
    /// researched, unresearched ones by the UnresearchedAnomalies setting. Nothing the star map
    /// would not already show, sorted by item instead of by system.
    ///
    /// UIItemTip.SetTip lays the panel out top to bottom and sizes it after the recipe entries, so
    /// a line appended to the description would sit under everything below it. The line is instead
    /// a text element of the mod's own, cloned from the description for font and style, placed
    /// under the last thing the game laid out, and the panel is grown to hold it and put back on
    /// screen the way SetTip does.
    /// </summary>
    internal static class ItemTipPatch
    {
        private static bool _errorLogged;
        private static bool _geometryLogged;
        private static bool _recipeEntryLogged;
        private static bool _styleLogged;

        /// <summary>Gap between the game's last element and the anomaly line.</summary>
        private const float Spacing = 6f;

        /// <summary>
        /// How much padding the game leaves under its last element. The line's top goes where that
        /// padding begins, and the same padding is kept under the line.
        /// </summary>
        private const float BottomPadding = 10f;

        /// <summary>
        /// The label's colour, as a rich-text tag: the star map's gold, turned down. Only the word
        /// "Anomaly:" wears it. The rest of the line keeps the description's own colour.
        ///
        /// Two versions coloured the whole line. Full gold was, in Paul's words, "really bright";
        /// this muted gold was still "glowy" and hard to read at times. DSP's UI blooms bright text,
        /// and a whole line of it fights the panel. The game's description colour is the one colour
        /// proven legible on this panel over every background the tooltip lands on, so the line
        /// borrows it and marks itself with the label alone.
        /// </summary>
        private const string LabelColour = "#DBBA75";

        /// <summary>The word every line opens with; the only part drawn in gold.</summary>
        private const string Label = "Anomaly:";

        /// <summary>One line per tooltip instance; the game keeps very few.</summary>
        private static readonly Dictionary<UIItemTip, Text> _lines = new Dictionary<UIItemTip, Text>();

        /// <summary>
        /// Harmony binds parameters by name: itemId and isRecipe are two of SetTip's eleven, and
        /// verify.ps1 asserts both names are still there.
        ///
        /// The replicator grid hovers recipes, not items, and passes the recipe id NEGATED as
        /// itemId -- the game's own convention, resolved at the top of SetTip. It took two wrong
        /// readings to find that: first "isRecipe means itemId is a recipe id" (it is not the flag
        /// that says so), then "itemId is always an item id" (it is not, when negative). Both
        /// showed nothing, silently, for every icon in the grid. The once-only log below is what
        /// finally printed "item -53" and settled it. A negative id names one recipe; a positive
        /// id names an item and every recipe that makes it.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIItemTip), "SetTip")]
        internal static void AfterSetTip(UIItemTip __instance, int itemId, bool isRecipe)
        {
            try
            {
                if (__instance == null || __instance.trans == null || __instance.descText == null)
                {
                    return;
                }

                string body = itemId < 0
                    ? AnomalyManager.TooltipLinesForRecipe(-itemId)
                    : AnomalyManager.TooltipLinesForItem(itemId);

                if (itemId < 0 && !_recipeEntryLogged)
                {
                    _recipeEntryLogged = true;
                    Plugin.Log.LogInfo("Recipe-entry tooltip (once): recipe " + (-itemId) + " (passed as " + itemId + "), " +
                                       (string.IsNullOrEmpty(body) ? "no anomaly line" : "anomaly line shown") + ".");
                }

                Text line = LineFor(__instance);

                if (string.IsNullOrEmpty(body))
                {
                    if (line != null)
                    {
                        line.gameObject.SetActive(false);
                    }
                    return;
                }

                if (line == null)
                {
                    return;
                }

                RectTransform panel = __instance.trans;
                RectTransform rect = line.rectTransform;
                RectTransform desc = __instance.descText.rectTransform;

                line.text = Decorate(body);
                line.color = __instance.descText.color;
                line.gameObject.SetActive(true);

                float panelHeight = panel.sizeDelta.y;
                float lineHeight = line.preferredHeight;

                // Under the last thing the game placed: the panel's bottom edge is at -height (the
                // panel hangs from its top), the game's content ends where its bottom padding
                // begins, and the line goes one gap below that. Same horizontal position as the
                // description. The first run left the gap out of the position and put it only in
                // the height, so the line sat flush against the recipe entries.
                Vector2 pos = rect.anchoredPosition;
                pos.x = desc.anchoredPosition.x;
                pos.y = -panelHeight + BottomPadding - Spacing;
                rect.anchoredPosition = pos;

                Vector2 size = panel.sizeDelta;
                size.y = panelHeight + Spacing + lineHeight;
                panel.sizeDelta = size;

                Clamp(panel);

                if (!_geometryLogged)
                {
                    _geometryLogged = true;
                    Plugin.Log.LogInfo(
                        "Tooltip geometry (once): panel " + panel.sizeDelta.x + "x" + panelHeight + " -> " + size.y +
                        "; desc anchorMin " + desc.anchorMin + " pivot " + desc.pivot +
                        " pos " + desc.anchoredPosition + " size " + desc.sizeDelta +
                        "; line pos " + rect.anchoredPosition + " height " + lineHeight + ".");
                }
            }
            catch (Exception e)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    Plugin.Log.LogError("Failed to add the anomaly line to an item tooltip: " + e);
                }
            }
        }

        /// <summary>
        /// The mod's text element for this tooltip, made on first use by cloning the description
        /// so the font, size, colour, wrapping width and -- importantly -- outline or shadow match. The
        /// first version stripped every component but the text, which threw away the outline DSP
        /// puts on tooltip text for legibility; only a localiser is removed now, since that would
        /// fight over the text.
        /// </summary>
        private static Text LineFor(UIItemTip tip)
        {
            Text line;
            if (_lines.TryGetValue(tip, out line) && line != null)
            {
                return line;
            }

            Text source = tip.descText;
            GameObject clone = UnityEngine.Object.Instantiate(source.gameObject, tip.trans);
            clone.name = "PlanetaryAnomalies-AnomalyLine";

            line = clone.GetComponent<Text>();
            if (line == null)
            {
                UnityEngine.Object.Destroy(clone);
                return null;
            }

            Component[] components = clone.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component c = components[i];
                if (c != null && c.GetType().Name.IndexOf("Locali", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    UnityEngine.Object.Destroy(c);
                }
            }

            line.supportRichText = true;
            line.raycastTarget = false;

            LogStyleOnce(source);

            _lines[tip] = line;
            return line;
        }

        /// <summary>Colours the leading label of each line; the rest stays as the description draws it.</summary>
        private static string Decorate(string body)
        {
            return body.Replace(Label, "<color=" + LabelColour + ">" + Label + "</color>");
        }

        /// <summary>
        /// What the game's description text actually is -- colour, size, font, material, and any
        /// outline or shadow on it -- written once, so the next pass at legibility starts from what
        /// the game draws rather than another guess at what "glowy" means.
        /// </summary>
        private static void LogStyleOnce(Text source)
        {
            if (_styleLogged)
            {
                return;
            }
            _styleLogged = true;

            string effects = "";
            Shadow[] shadows = source.GetComponents<Shadow>();
            for (int i = 0; i < shadows.Length; i++)
            {
                effects += "; " + shadows[i].GetType().Name + " colour " + shadows[i].effectColor +
                           " distance " + shadows[i].effectDistance;
            }

            Plugin.Log.LogInfo(
                "Tooltip description style (once): colour " + source.color + ", size " + source.fontSize + " " + source.fontStyle +
                ", font " + (source.font != null ? source.font.name : "?") +
                ", material " + (source.material != null ? source.material.name : "?") + effects + ".");
        }

        /// <summary>
        /// SetTip's own screen clamp, repeated after the panel has grown: the tooltip lives inside
        /// UIRoot.itemTipTransform, and any edge past that rect pulls the panel back by the overlap.
        /// </summary>
        private static void Clamp(RectTransform panel)
        {
            UIRoot root = UIRoot.instance;
            if (root == null || root.itemTipTransform == null)
            {
                return;
            }

            Rect bounds = root.itemTipTransform.rect;
            float width = Mathf.RoundToInt(bounds.width);
            float height = Mathf.RoundToInt(bounds.height);

            Rect r = panel.rect;
            r.x += panel.anchorMin.x * width + panel.anchoredPosition.x;
            r.y += panel.anchorMin.y * height + panel.anchoredPosition.y;

            Vector2 shift = Vector2.zero;
            if (r.xMin < 0f) { shift.x = -r.xMin; }
            if (r.yMin < 0f) { shift.y = -r.yMin; }
            if (r.xMax > width) { shift.x = width - r.xMax; }
            if (r.yMax > height) { shift.y = height - r.yMax; }

            if (shift != Vector2.zero)
            {
                panel.anchoredPosition = panel.anchoredPosition + shift;
            }
        }
    }
}
