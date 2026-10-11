using System.Collections.Generic;

namespace FocusBear.Core.Schedule
{
    /// <summary>
    /// Pure ordering rule for switching the live routine to another habit mid-flight.
    ///
    /// The habit player's "Select another habit" sheet moves the chosen habit to the position of the
    /// habit that is currently playing, which leaves that habit directly behind it — so it is not
    /// skipped or completed, it just comes up next. The Day Plan's Play button does the same thing, but
    /// it can offer any habit in the routine, including one *before* the current position.
    ///
    /// Regression it pins: with a habit paused, tapping Play on a different habit in the Day Plan did
    /// nothing at all.
    /// </summary>
    public static class RoutineReorder
    {
        /// <summary>
        /// <paramref name="habits"/> with the item at <paramref name="from"/> moved to
        /// <paramref name="anchor"/> (the currently-playing position). Returns null when there is
        /// nothing to do — the index is out of range, or the item already sits where it would land.
        /// </summary>
        public static IReadOnlyList<T> Moving<T>(IReadOnlyList<T> habits, int from, int anchor)
        {
            if (habits == null || from < 0 || from >= habits.Count) return null;

            int target = anchor < 0 ? 0 : (anchor > habits.Count - 1 ? habits.Count - 1 : anchor);
            if (from == target) return null;

            List<T> result = new List<T>(habits);
            T moved = result[from];
            result.RemoveAt(from);
            // Removing an item that sat *before* the anchor shifts the anchor down one place.
            int insertAt = from < target ? target - 1 : target;
            if (insertAt > result.Count) insertAt = result.Count;
            result.Insert(insertAt, moved);
            return result;
        }
    }
}
