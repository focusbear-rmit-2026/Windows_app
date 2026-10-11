using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Blocking;

namespace Focus_Bear.Utils
{
    /// <summary>
    /// The allowed apps and sites of the routine habit or break activity that is running. The blocking paths indexed
    /// the routine's list with <c>nHabitIndex</c> directly; an index out of range threw, the handler caught it, and
    /// nothing was blocked for the rest of the habit. Now an index that doesn't point into the list falls back to the
    /// habit being played (what the Mac reads, <c>currentHabitPlaying</c>), and with neither there are no rules, so
    /// only the always-allowed apps and sites stay: the safe side while an allow-list habit is on.
    /// </summary>
    internal static class RunningActivityRules
    {
        internal sealed class Rules
        {
            public List<string> Apps = new List<string>();
            public List<string> Urls = new List<string>();
            public bool IsRelax;
            /// <summary>False when the running activity couldn't be found (the lists are then empty).</summary>
            public bool Known;
            public string Label = "";
        }

        internal static Rules For(AppSettings s)
        {
            var rules = new Rules();
            switch (s.nCurrentActivityType)
            {
                case CurrentActivityType.MORNING_HABITS:
                    rules.Label = "MORNING_HABITS";
                    FromHabit(rules, s.arrAllMorningHabits, s);
                    break;
                case CurrentActivityType.EVENING_HABITS:
                    rules.Label = "EVENING_HABITS";
                    FromHabit(rules, s.arrAllEveningHabits, s);
                    break;
                case CurrentActivityType.BREAK_ACTIVITY:
                    rules.Label = "BREAK_ACTIVITY";
                    var list = s.isMandatoryActivityGoing ? s.arrActivitiesIncludeInEveryBreak : s.arrActivitiesExcludeInEveryBreak;
                    if (list != null && ActivityAllowList.InRange(s.nHabitIndex, list.Count))
                        Take(rules, list[s.nHabitIndex].allowedApps, list[s.nHabitIndex].allowedUrls);
                    break;
            }
            return rules;
        }

        private static void FromHabit(Rules rules, List<habit> habits, AppSettings s)
        {
            if (habits != null && ActivityAllowList.InRange(s.nHabitIndex, habits.Count))
            {
                Take(rules, habits[s.nHabitIndex].allowedApps, habits[s.nHabitIndex].allowedUrls);
                rules.IsRelax = habits[s.nHabitIndex].show_saved_distracting_websites;
            }
            else if (!string.IsNullOrEmpty(s.currentHabitPlaying.id))
            {
                Take(rules, s.currentHabitPlaying.allowedApps, s.currentHabitPlaying.allowedUrls);
                rules.IsRelax = s.currentHabitPlaying.show_saved_distracting_websites;
            }
        }

        private static void Take(Rules rules, List<string> apps, List<string> urls)
        {
            rules.Known = true;
            rules.Apps = ActivityAllowList.AppNames(apps).ToList();
            // A blank row (or a bare "https://") would match every page: it is not an entry.
            rules.Urls = (urls ?? new List<string>()).Where(u => u != null && BrowserUrlEntries.ToNeedle(u).Trim().Length > 0).ToList();
        }

        /// <summary>
        /// Clears the allowed-apps and use-focus-mode habit flags when they are stale (Core
        /// <see cref="ActivityAllowList.IsStaleActivityFlag"/>): set, in a habit or break activity, yet no running habit
        /// resolves. Logged when it happens, which is once: the flags stay clear until a habit sets them again.
        /// Returns true when it cleared them.
        /// </summary>
        internal static bool ClearIfStale(AppSettings s)
        {
            bool flagSet = s.bHabitwithAllowedAppsRunning || s.bHabitwithUseFocusModeRunning;
            if (!flagSet) return false;
            var rules = For(s);
            if (!ActivityAllowList.IsStaleActivityFlag(flagSet, rules.Label.Length > 0, rules.Known)) return false;
            LogWriter.LogWrite("Blocking - the " + rules.Label + " activity flags are set but no habit is running (index " + s.nHabitIndex
                + "): clearing them (allowedApps=" + s.bHabitwithAllowedAppsRunning + ", useFocusMode=" + s.bHabitwithUseFocusModeRunning + ")");
            s.bHabitwithAllowedAppsRunning = false;
            s.bHabitwithUseFocusModeRunning = false;
            return true;
        }

        /// <summary>
        /// The evening relax habit ("guilt-free play") at <c>nHabitIndex</c> is running: nothing is blocked. By index only,
        /// never the last played habit, so a stale relax habit can't switch blocking off.
        /// </summary>
        internal static bool IsRelaxHabitRunning(AppSettings s) =>
            s.nCurrentActivityType == CurrentActivityType.EVENING_HABITS && s.arrAllEveningHabits != null
            && ActivityAllowList.InRange(s.nHabitIndex, s.arrAllEveningHabits.Count)
            && s.arrAllEveningHabits[s.nHabitIndex].show_saved_distracting_websites;
    }
}
