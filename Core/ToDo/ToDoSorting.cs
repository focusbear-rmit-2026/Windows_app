using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>
    /// Web `sortTasksBySortMode`. The API's own order is never trusted once rows have been merged
    /// client-side, and its SMART order is itself ranked on stored scores that go stale the moment a
    /// due date passes (see <see cref="ToDoScoring.TopScoreOf"/>).
    ///
    /// Every mode breaks ties on the caller's original order. Swift's <c>sorted</c> is not a stable
    /// sort, so the Mac carries an explicit index alongside each row; LINQ's <c>OrderBy</c> IS stable,
    /// but the tie-break is kept explicit anyway so the rule reads the same in both ports and does not
    /// depend on a property of the sort that no test would catch changing.
    /// </summary>
    public static class ToDoSorting
    {
        public static IList<ToDoTask> Sort(IEnumerable<ToDoTask> tasks, ToDoSortMode mode, DateTime now)
        {
            IList<ToDoTask> rows = (tasks ?? Enumerable.Empty<ToDoTask>()).ToList();
            switch (mode)
            {
                case ToDoSortMode.DueDate: return ByDueDate(rows);
                case ToDoSortMode.Chronological: return ByCreatedNewestFirst(rows);
                case ToDoSortMode.Smart: return ToDoScoring.SortBySmart(rows, now);
                default: throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        /// <summary>
        /// Soonest due first, undated last.
        ///
        /// The Mac sorts an absent due date as <c>+infinity</c>; <see cref="DateTime.MaxValue"/> is the
        /// same idea in a type that has no infinity, and no real due date can reach it.
        /// </summary>
        private static IList<ToDoTask> ByDueDate(IList<ToDoTask> rows)
        {
            return rows
                .Select((task, index) => new { task, index })
                .OrderBy(entry => ToDoDates.Parse(entry.task.DueDate) ?? DateTime.MaxValue)
                .ThenBy(entry => entry.index)
                .Select(entry => entry.task)
                .ToList();
        }

        /// <summary>
        /// Newest first. A row with no readable `created_at` — the API has answered a create without
        /// one — sorts as the oldest rather than jumping to the top of the list.
        /// </summary>
        private static IList<ToDoTask> ByCreatedNewestFirst(IList<ToDoTask> rows)
        {
            return rows
                .Select((task, index) => new { task, index })
                .OrderByDescending(entry => ToDoDates.Parse(entry.task.CreatedAt) ?? DateTime.MinValue)
                .ThenBy(entry => entry.index)
                .Select(entry => entry.task)
                .ToList();
        }
    }
}
