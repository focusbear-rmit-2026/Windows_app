using System;
using FocusBear.Core.Stats;

namespace FocusBear.Core.Focus
{
    /// <summary>
    /// The left menu's focus-session row (the Mac's <c>leftMenuVc.resetFocusModeTitle</c>). While a session runs
    /// it says what is happening, "Focusing (29m left)", not what clicking it does: it has never stopped the
    /// session, it opens the focus screen, and it used to read "Stop Focus Session".
    /// </summary>
    public static class FocusSessionMenuTitle
    {
        /// <summary>config leftMenuVcInfo.menuFocusingTimeLeft on the Mac; this is its last-resort default.</summary>
        public const string DefaultFocusingTemplate = "Focusing (<MINUTES>m left)";
        public const string MinutesToken = "<MINUTES>";

        /// <summary>
        /// Running → "Focusing (Nm left)", whole minutes rounded up (a minute and a half left reads "2m"; the Mac
        /// never switches to hours, so 65 minutes reads "65m"). Paused → <paramref name="resumeTitle"/>. Between
        /// the sessions of a Super Focus run → <paramref name="stopSuperFocusTitle"/>. Otherwise
        /// <paramref name="startTitle"/>.
        /// </summary>
        public static string For(bool isRunning, bool isPaused, bool isSuperFocus, DateTime? end, DateTime now,
            string focusingTemplate, string resumeTitle, string stopSuperFocusTitle, string startTitle)
        {
            if (isRunning && !isPaused)
            {
                string template = string.IsNullOrWhiteSpace(focusingTemplate) || !focusingTemplate.Contains(MinutesToken) ? DefaultFocusingTemplate : focusingTemplate;
                int minutes = end.HasValue ? FocusSessionClock.MinutesRemaining(end.Value, now) : 0;
                return template.Replace(MinutesToken, minutes.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            if (isRunning) return resumeTitle;
            return isSuperFocus ? stopSuperFocusTitle : startTitle;
        }
    }
}
