using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.Stats
{
    /// <summary>A habit and how often it was completed inside its scoring window.</summary>
    public sealed class HabitConsistencyItem
    {
        public HabitConsistencyItem(string activityId, int completedDays, int windowDays, int percentage)
        {
            ActivityId = activityId;
            CompletedDays = completedDays;
            WindowDays = windowDays;
            Percentage = percentage;
        }

        public string ActivityId { get; }
        public int CompletedDays { get; }
        public int WindowDays { get; }

        /// <summary>0–100, rounded.</summary>
        public int Percentage { get; }
    }

    /// <summary>What the caller knows about one habit; the ranking needs no more than this.</summary>
    public sealed class ConsistencyInput
    {
        public string ActivityId { get; set; }

        /// <summary>When the habit was created. Null means "assume it has existed the whole window".</summary>
        public DateTime? CreatedAt { get; set; }
    }

    /// <summary>
    /// The "most / least consistent habits" ranking. Port of the Mac's
    /// <c>HabitConsistencyRanking</c>.
    /// </summary>
    public static class HabitConsistencyRanking
    {
        public const int WindowMaxDays = 90;
        public const int TopVisible = 5;

        /// <summary>The "least consistent" list only appears once there is something beyond the top five.</summary>
        public const int MinimumForLeastSection = TopVisible + 1;

        public sealed class Split
        {
            public Split(IReadOnlyList<HabitConsistencyItem> mostConsistent,
                         IReadOnlyList<HabitConsistencyItem> leastConsistent)
            {
                MostConsistent = mostConsistent;
                LeastConsistent = leastConsistent;
            }

            public IReadOnlyList<HabitConsistencyItem> MostConsistent { get; }
            public IReadOnlyList<HabitConsistencyItem> LeastConsistent { get; }
            public bool ShowsLeastSection { get { return LeastConsistent.Count > 0; } }
        }

        /// <summary>
        /// A habit created less than 90 days ago is scored against the days it has existed, not
        /// against 90 — otherwise a habit added yesterday reads as 1% consistent.
        /// </summary>
        public static int WindowDays(DateTime? createdAt, DateTime today)
        {
            if (createdAt == null) return WindowMaxDays;
            int elapsed = StatsCalendar.DaysBetween(createdAt.Value, today) + 1;
            return Math.Min(Math.Max(elapsed, 1), WindowMaxDays);
        }

        /// <summary>
        /// Ranks the habits the stats payload has a completion count for. A habit with no entry is
        /// dropped rather than shown as 0% — the Mac does the same, because "no data" and "never
        /// done it" are different claims.
        /// </summary>
        public static IReadOnlyList<HabitConsistencyItem> Build(IEnumerable<ConsistencyInput> activities,
                                                               IDictionary<string, int> completedDaysByActivityId,
                                                               DateTime today)
        {
            if (activities == null) return new List<HabitConsistencyItem>();
            var completed = completedDaysByActivityId ?? new Dictionary<string, int>();

            return activities
                .Where(activity => activity != null && activity.ActivityId != null
                                   && completed.ContainsKey(activity.ActivityId))
                .Select(activity =>
                {
                    int days = completed[activity.ActivityId];
                    int window = WindowDays(activity.CreatedAt, today);
                    int percentage = SwiftRounding.RoundedToInt((double)days / window * 100);
                    percentage = Math.Min(Math.Max(percentage, 0), 100);
                    return new HabitConsistencyItem(activity.ActivityId, days, window, percentage);
                })
                // OrderByDescending is stable in LINQ-to-objects, which matches the Swift sort's
                // behaviour for equal percentages closely enough to keep the two lists in step.
                .OrderByDescending(item => item.Percentage)
                .ToList();
        }

        /// <summary>
        /// Top five, then the rest **reversed** so the least consistent habit is first in its own
        /// section — that reversal is the whole point of the second list.
        /// </summary>
        public static Split SplitRanked(IReadOnlyList<HabitConsistencyItem> ranked)
        {
            if (ranked == null || ranked.Count == 0)
            {
                return new Split(new List<HabitConsistencyItem>(), new List<HabitConsistencyItem>());
            }
            var most = ranked.Take(TopVisible).ToList();
            var least = ranked.Count >= MinimumForLeastSection
                ? ranked.Skip(TopVisible).Reverse().ToList()
                : new List<HabitConsistencyItem>();
            return new Split(most, least);
        }
    }
}
