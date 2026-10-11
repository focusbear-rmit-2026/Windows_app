using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.Stats
{
    /// <summary>
    /// The four numbers above a single habit's stats screen. Port of the Mac's
    /// <c>HabitStatSummary</c>, itself a port of the web dashboard's
    /// <c>calculateTotalCompletions</c> / <c>calculateTotalHoursSpent</c> /
    /// <c>calculateCurrentStreak</c> / <c>calculateLast30DaysPercentage</c>.
    /// </summary>
    public sealed class HabitStatSummary
    {
        public const int RecentWindowDays = 30;

        public int TotalCompletions { get; set; }

        /// <summary>One decimal, like the web.</summary>
        public double HoursSpent { get; set; }

        public int CurrentStreak { get; set; }
        public int Last30DaysPercentage { get; set; }

        /// <summary>
        /// Computes all four.
        /// <para>
        /// The streak is anchored on YESTERDAY, not today, so a habit done only today reads 0. That
        /// looks wrong and is deliberate: it is what the dashboard shows, and a streak that ticked
        /// up the moment you completed something would disagree with the web for the rest of the
        /// day.
        /// </para>
        /// </summary>
        public static HabitStatSummary Compute(IEnumerable<ActivityStatItem> items, DateTime today)
        {
            var summary = new HabitStatSummary();
            var materialised = (items ?? Enumerable.Empty<ActivityStatItem>()).ToList();

            IDictionary<string, int> counts = HeatMapBuilder.CountsByDay(materialised);
            summary.TotalCompletions = counts.Values.Sum();

            double seconds = materialised.Sum(item => item.Summary);
            summary.HoursSpent = SwiftRounding.Rounded(seconds / 3600 * 10) / 10;

            summary.CurrentStreak = StreakEndingYesterday(counts, today);

            DateTime windowStart = StatsCalendar.AddDays(-RecentWindowDays, StatsCalendar.StartOfDay(today));
            // 0...RecentWindowDays inclusive — 31 days examined against a 30-day denominator, which
            // is the web's arithmetic and can therefore exceed 100%. Kept rather than corrected.
            int activeDays = Enumerable.Range(0, RecentWindowDays + 1)
                .Count(offset => CountFor(counts, StatsCalendar.AddDays(offset, windowStart)) > 0);
            summary.Last30DaysPercentage =
                SwiftRounding.RoundedToInt((double)activeDays / RecentWindowDays * 100);

            return summary;
        }

        private static int StreakEndingYesterday(IDictionary<string, int> counts, DateTime today)
        {
            int streak = 0;
            DateTime cursor = StatsCalendar.AddDays(-1, today);
            while (CountFor(counts, cursor) > 0)
            {
                streak++;
                cursor = StatsCalendar.AddDays(-1, cursor);
            }
            return streak;
        }

        private static int CountFor(IDictionary<string, int> counts, DateTime day)
        {
            int count;
            return counts.TryGetValue(StatsDates.DayKey(day), out count) ? count : 0;
        }
    }
}
