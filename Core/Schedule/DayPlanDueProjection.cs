using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.Schedule
{
    /// <summary>
    /// Synced unfinished work has one canonical home: its due date, or today if overdue. Future days
    /// contain only work due on that day; viewing a future date does not age today's work.
    /// </summary>
    public static class DayPlanDueProjection
    {
        /// <summary>One incoming row: the day a to-do is due, and the planned task built from it.</summary>
        public struct DueRow
        {
            public DueRow(string due, DayPlanTask task)
            {
                Due = due;
                Task = task;
            }

            public string Due { get; }
            public DayPlanTask Task { get; }
        }

        /// <summary>The day a to-do belongs on: its due day, or today when that day has passed.</summary>
        public static string CanonicalDay(string dueDate, string today)
        {
            DateTimeOffset? date = ToDoDates.Parse(dueDate);
            if (!date.HasValue) return null;
            string due = ToDoDates.Ymd(date.Value);
            return string.CompareOrdinal(today, due) >= 0 ? today : due;
        }

        /// <summary>
        /// Re-files the stored plan against the current due dates.
        ///
        /// Everything the backend owns (a task with a <c>SourceTaskId</c> that is not finished) is
        /// dropped and re-added from <paramref name="rows"/>, so a to-do whose due date moved leaves the
        /// day it used to sit on. Local tasks and finished history stay exactly where they are, and a
        /// task completed today is not resurfaced by a row that has not caught up yet.
        /// </summary>
        public static IReadOnlyDictionary<string, IReadOnlyList<DayPlanTask>> Reconcile(
            IReadOnlyDictionary<string, IReadOnlyList<DayPlanTask>> stored,
            IEnumerable<DueRow> rows, string today)
        {
            return Reconcile(stored, rows.Select(r => new KeyValuePair<string, DayPlanTask>(r.Due, r.Task)), today,
                             t => t.SourceTaskId, t => t.Done, t => t.Id);
        }

        /// <summary>
        /// The same rule over any task type — the Windows app keeps its own Day Plan task class, so the rule
        /// takes the three fields it reads rather than a type. Rows are (due day, task).
        /// </summary>
        public static IReadOnlyDictionary<string, IReadOnlyList<T>> Reconcile<T>(
            IReadOnlyDictionary<string, IReadOnlyList<T>> stored,
            IEnumerable<KeyValuePair<string, T>> rows, string today,
            Func<T, string> sourceTaskId, Func<T, bool> done, Func<T, string> id)
        {
            Dictionary<string, List<T>> result = stored.ToDictionary(
                day => day.Key,
                day => day.Value.Where(t => sourceTaskId(t) == null || done(t)).ToList());

            IReadOnlyList<T> todaysTasks;
            HashSet<string> completedToday = new HashSet<string>(
                stored.TryGetValue(today, out todaysTasks)
                    ? todaysTasks.Where(done).Select(sourceTaskId).Where(s => s != null)
                    : Enumerable.Empty<string>());

            HashSet<string> seen = new HashSet<string>();
            foreach (KeyValuePair<string, T> row in rows)
            {
                string source = sourceTaskId(row.Value);
                if (source == null || !seen.Add(source) || completedToday.Contains(source)) continue;
                List<T> day;
                if (!result.TryGetValue(row.Key, out day))
                {
                    day = new List<T>();
                    result[row.Key] = day;
                }
                day.Add(row.Value);
            }

            // Preserve the user's row order when a refresh leaves tasks on the same due day. OrderBy is
            // a stable sort, so rows the stored order says nothing about keep the order they arrived in.
            foreach (string day in result.Keys.ToList())
            {
                Dictionary<string, int> order = StoredOrder(stored, day, id);
                result[day] = result[day]
                    .OrderBy(task => order.TryGetValue(id(task), out int at) ? at : int.MaxValue)
                    .ToList();
            }

            return result.Where(day => day.Value.Count > 0)
                         .ToDictionary(day => day.Key, day => (IReadOnlyList<T>)day.Value);
        }

        private static Dictionary<string, int> StoredOrder<T>(
            IReadOnlyDictionary<string, IReadOnlyList<T>> stored, string day, Func<T, string> id)
        {
            Dictionary<string, int> order = new Dictionary<string, int>();
            IReadOnlyList<T> tasks;
            if (!stored.TryGetValue(day, out tasks)) return order;
            for (int i = 0; i < tasks.Count; i++)
            {
                // First occurrence wins, matching the Swift dictionary's uniquing rule.
                if (!order.ContainsKey(id(tasks[i]))) order[id(tasks[i])] = i;
            }
            return order;
        }
    }
}
