using System;

namespace FocusBear.Core.Schedule
{
    /// <summary>
    /// Whether the motivation / visualise screen has already been shown for a routine today.
    ///
    /// It was gated only by an in-memory flag, so restarting a routine — or the app — showed the star
    /// rating and "what's motivating you today?" again, several times in one evening
    /// (report 2026-09-13_20-30-50). The answer is per routine per day, so that is what gets stored.
    /// </summary>
    public static class RoutineVisualizePrompt
    {
        private const string KeyPrefix = "routineVisualizeShownDay.";
        private const string MorningKey = KeyPrefix + "morning";
        private const string EveningKey = KeyPrefix + "evening";
        private const string CustomKeyPrefix = KeyPrefix + "custom.";

        /// <summary>
        /// One routine's prompt. Custom routines are keyed individually so answering one does not
        /// silence another.
        /// </summary>
        public static class StorageKey
        {
            public static string Morning
            {
                get { return MorningKey; }
            }

            public static string Evening
            {
                get { return EveningKey; }
            }

            public static string Custom(string routineId)
            {
                return CustomKeyPrefix + routineId;
            }
        }

        /// <summary>Show it unless it has already been shown on this day.</summary>
        public static bool ShouldShow(string lastShownDay, string today)
        {
            if (string.IsNullOrEmpty(lastShownDay)) return true;
            return !string.Equals(lastShownDay, today, StringComparison.Ordinal);
        }

        public static string DayKey(DateTimeOffset date)
        {
            return DayPlanDayKey.For(date);
        }
    }
}
