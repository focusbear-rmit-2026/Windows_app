using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// A routine habit's (or break activity's) "allowed apps": while it runs, every other app is minimised. This was
    /// four copies of one loop in FocusChangedHandler, and each turned a blank entry into the title pattern "**",
    /// which matches every window, so a habit with an empty row in its allowed-apps list allowed everything.
    /// </summary>
    public static class ActivityAllowList
    {
        /// <summary>The entries as process names: trimmed, lower-case, without ".exe"; blanks and repeats dropped.</summary>
        public static IReadOnlyList<string> AppNames(IEnumerable<string> allowedApps)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<string>();
            foreach (string entry in allowedApps ?? Enumerable.Empty<string>())
            {
                string name = (entry ?? "").Trim().ToLowerInvariant();
                if (name.EndsWith(".exe", StringComparison.Ordinal)) name = name.Substring(0, name.Length - 4).TrimEnd();
                if (name.Trim('*').Length == 0) continue;   // blank, or only wildcards: would allow every app
                if (seen.Add(name)) result.Add(name);
            }
            return result;
        }

        /// <summary>
        /// Whether the app in front may stay: its process name is on the list, or its window title contains an entry
        /// (a web app in its own window, e.g. "Notion" in a browser's app window), as before.
        /// </summary>
        public static bool IsAppAllowed(string processName, string windowTitle, IEnumerable<string> allowedApps)
        {
            string process = (processName ?? "").Trim().ToLowerInvariant();
            string title = (windowTitle ?? "").ToLowerInvariant();
            foreach (string name in AppNames(allowedApps))
            {
                if (name == process) return true;
                if (title.Length > 0 && WildcardPattern.Matches(title, "*" + name + "*")) return true;
            }
            return false;
        }

        /// <summary>Whether <paramref name="index"/> points into a list of <paramref name="count"/> activities.</summary>
        public static bool InRange(int index, int count) => index >= 0 && index < count;

        /// <summary>
        /// A "habit with allowed apps / with a focus mode is running" flag is stale when a routine habit or break activity
        /// is the current activity but neither the habit index nor the habit being played resolves to one: nothing is
        /// running that the flag could be about, so it is cleared instead of minimising every app on the rules of a
        /// habit that isn't there. Outside a habit or break activity the flag is left alone (another path owns it).
        /// </summary>
        public static bool IsStaleActivityFlag(bool flagSet, bool inHabitOrBreakActivity, bool runningActivityResolved) =>
            flagSet && inHabitOrBreakActivity && !runningActivityResolved;
    }
}
