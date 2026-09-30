using System;
using System.Globalization;

namespace FocusBear.Core.Schedule
{
    /// <summary>
    /// The block-key vocabulary — which block of the day a time-boxed task is pinned to.
    ///
    /// On the Mac these prefixes were written out as literals in the schedule view controller, again in
    /// the habit checklist, and again in every capacity path that asked "is this a habit key?". That
    /// last repetition is how the capacity model came to absorb task work into habits but not into
    /// calendar events: two of the three places tested for "habit:" and nobody noticed the third kind
    /// of key existed (report 2026-09-17_09-08-25). One vocabulary, one place.
    /// </summary>
    public static class DayPlanBlockKey
    {
        public const string HabitPrefix = "habit:";
        public const string EventPrefix = "event:";
        public const string DeepWorkPrefix = "deepwork:";

        /// <summary>
        /// Habits are keyed by NAME, not by server id — the Day Plan builds its rows from the routine's
        /// habit titles, so renaming a habit detaches its tasks.
        /// </summary>
        public static string Habit(string name)
        {
            return HabitPrefix + name;
        }

        public static string Event(string id)
        {
            return EventPrefix + id;
        }

        /// <summary>
        /// Deep-work sprints drift as the day repacks, so their key is the start time they were pinned
        /// at (Unix seconds) and the timeline re-matches it to the nearest current sprint.
        /// </summary>
        public static string DeepWork(DateTimeOffset start)
        {
            return DeepWorkPrefix + start.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Whether a task pinned to this block is worked inside time the day has ALREADY committed, so
        /// its minutes must not be charged against the day's free time a second time.
        ///
        /// Habits and calendar events qualify — "if I drag a task to an event, it should be the same
        /// impact on day plan free time as if I drag it to a habit" (report 2026-09-17_09-08-25). Deep
        /// work does not: a deep-work block is not committed time, it IS the day's free work time, so
        /// absorbing into it would refund the very minutes it represents.
        /// </summary>
        public static bool Absorbs(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return key.StartsWith(HabitPrefix, StringComparison.Ordinal)
                   || key.StartsWith(EventPrefix, StringComparison.Ordinal);
        }
    }
}
