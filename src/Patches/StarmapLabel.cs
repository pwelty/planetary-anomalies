using System;
using UnityEngine;
using UnityEngine.UI;

namespace PlanetaryAnomalies
{
    /// <summary>
    /// Writing and rewriting the mod's suffix on a star map label.
    ///
    /// This exists because of a bug worth remembering. The star map labels were originally written
    /// once, by appending to whatever the game had put there, on <c>_OnInit</c> and on the rename
    /// event. That is every place DSP itself writes <c>nameText.text</c>, so it looked complete.
    ///
    /// It was not, because what the suffix *says* depends on state that changes while you play:
    /// whether a planet has been scanned, and -- since 0.4 -- whether its recipe has been
    /// researched. A label composed before you finished a technology kept the answer that was true
    /// when it was built, for the rest of the session. Reopening the star map did not help; only
    /// reloading the save did. So a feature whose entire job is hiding things could hide one and
    /// never un-hide it, which is the worst possible failure for it to have.
    ///
    /// Two consequences for the code here. Applying a suffix must be able to *replace* one, not
    /// merely append when absent -- so the old text is cut at the marker and rebuilt. And an empty
    /// body must clear an existing suffix rather than returning early, because "nothing to say" is
    /// a real answer that has to be able to overwrite a previous one.
    /// </summary>
    internal static class StarmapLabel
    {
        /// <summary>
        /// Opens the mod's suffix, and doubles as the cut point when replacing it. Two leading
        /// spaces separate it from the game's own text.
        ///
        /// Star map labels have rich text off by default, so a colour tag would otherwise render
        /// as the literal characters. UIPlanetDetail does support it, which is what misled the
        /// first attempt -- they are different components with different settings.
        /// </summary>
        private const string Open = "  <color=#FFC454>";

        private const string Close = "</color>";

        /// <summary>
        /// How often a visible label re-examines itself, in frames. Roughly twice a second at 60fps.
        ///
        /// The alternative was to drive refreshes from events -- <c>onTechUnlocked</c> exists -- but
        /// there is no matching event for a planet becoming scanned, so events would fix half the
        /// problem and leave the other half looking identical. A poll needs to know nothing about
        /// why the answer changed, which is the property worth having here.
        ///
        /// The work per label is small: one dictionary lookup per planet in the system, and a
        /// string comparison that almost always matches and writes nothing.
        /// </summary>
        private const int RefreshIntervalFrames = 30;

        /// <summary>
        /// Whether this frame is one of the periodic re-examination frames.
        ///
        /// Deliberately derived from the frame number rather than from "time since last refresh":
        /// every label in the same frame must agree, and a shared last-refreshed timestamp would
        /// let the first label consume the interval and starve the rest.
        /// </summary>
        internal static bool DueForRefresh()
        {
            return (Time.frameCount % RefreshIntervalFrames) == 0;
        }

        /// <summary>
        /// Gives a label the mod's suffix, replacing any suffix already there. A null or empty
        /// body removes it. Writes nothing when the text is already correct, which is the common
        /// case on a refresh frame.
        /// </summary>
        internal static void Set(Text label, string body)
        {
            if (label == null)
            {
                return;
            }

            string current = label.text != null ? label.text : "";

            int at = current.IndexOf(Open, StringComparison.Ordinal);
            string bare = at >= 0 ? current.Substring(0, at) : current;

            string wanted = string.IsNullOrEmpty(body) ? bare : bare + Open + body + Close;

            if (string.Equals(current, wanted, StringComparison.Ordinal))
            {
                return;
            }

            if (!string.IsNullOrEmpty(body) && !label.supportRichText)
            {
                label.supportRichText = true;
            }

            label.text = wanted;
        }
    }
}
