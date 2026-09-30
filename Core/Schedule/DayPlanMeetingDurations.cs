using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.Schedule
{
    /// <summary>
    /// A meeting trimmed for one specific day, IN THE DAY PLAN ONLY.
    ///
    /// The Fix modal's meetings route offers "shorten — similar to the habit shorten UI", and
    /// "similar to the habit shorten UI" is the whole specification: like
    /// <see cref="DayPlanHabitDurations"/>, this is one day, on this machine, and reversible.
    ///
    /// It is deliberately NOT a write to the calendar. Shortening a meeting from a "make today fit"
    /// modal would push an update to every attendee, and on a repeating event the calendar store
    /// would happily apply it to the wrong occurrence (looking an event up by identifier hands back
    /// the series' first one). Cancelling and declining are the actions that reach other people, and
    /// they keep their confirmation; trimming the slot the day plan reserves does not need one.
    ///
    /// The Mac keeps the map in <c>UserDefaults</c>; here it is a plain dictionary
    /// (<c>day → event id → seconds</c>) the host app persists however it likes.
    /// </summary>
    public sealed class DayPlanMeetingDurations
    {
        /// <summary>Days kept, so the map cannot grow without bound — the same horizon as the habit
        /// trims.</summary>
        public const int RetainedDays = 60;

        private readonly Dictionary<string, Dictionary<string, int>> _overridesByDay;

        public DayPlanMeetingDurations(IDictionary<string, Dictionary<string, int>> stored = null)
        {
            _overridesByDay = stored == null
                ? new Dictionary<string, Dictionary<string, int>>()
                : stored.ToDictionary(entry => entry.Key, entry => new Dictionary<string, int>(entry.Value));
        }

        /// <summary>The map as it should be persisted.</summary>
        public IReadOnlyDictionary<string, Dictionary<string, int>> OverridesByDay
        {
            get { return _overridesByDay; }
        }

        /// <summary>The trim recorded for this meeting on this day, or null when it keeps its calendar
        /// length.</summary>
        public int? OverrideSeconds(string eventId, DateTimeOffset day)
        {
            if (string.IsNullOrEmpty(eventId)) return null;
            Dictionary<string, int> dayMap;
            int seconds;
            if (!_overridesByDay.TryGetValue(DayPlanDayKey.For(day), out dayMap)) return null;
            if (!dayMap.TryGetValue(eventId, out seconds) || seconds <= 0) return null;
            return seconds;
        }

        /// <summary>
        /// The end the Day Plan should use for this occurrence: the trim when there is one, and never
        /// later than the meeting really ends — this only ever SHORTENS.
        ///
        /// A stored trim longer than the meeting (it was extended on the calendar after being trimmed)
        /// therefore loses to the meeting's own end, rather than stretching the reserved slot back out.
        /// </summary>
        public DateTimeOffset TrimmedEnd(string eventId, DateTimeOffset day, DateTimeOffset start,
                                         DateTimeOffset end)
        {
            int? seconds = OverrideSeconds(eventId, day);
            if (!seconds.HasValue) return end;
            DateTimeOffset trimmed = start.AddSeconds(seconds.Value);
            return trimmed < end ? trimmed : end;
        }

        /// <summary>
        /// Records a trim, clamped to at least <see cref="DayPlanHabitDurations.MinimumSeconds"/>.
        /// Asking for the meeting's real length (or longer) CLEARS the trim instead of storing one, so
        /// "full length" is how the user takes a trim back. Returns the trim now in force, null when
        /// cleared.
        /// </summary>
        public int? SetOverride(int seconds, string eventId, DateTimeOffset day, int actual)
        {
            if (string.IsNullOrEmpty(eventId)) return null;
            int wanted = Math.Max(seconds, DayPlanHabitDurations.MinimumSeconds);
            if (wanted >= actual)
            {
                ClearOverride(eventId, day);
                return null;
            }

            string key = DayPlanDayKey.For(day);
            Dictionary<string, int> dayMap;
            if (!_overridesByDay.TryGetValue(key, out dayMap))
            {
                dayMap = new Dictionary<string, int>();
                _overridesByDay[key] = dayMap;
            }
            dayMap[eventId] = wanted;
            DayPlanDayKey.PruneOldestDays(_overridesByDay, RetainedDays);
            return wanted;
        }

        /// <summary>Back to the meeting's real length on this day.</summary>
        public void ClearOverride(string eventId, DateTimeOffset day)
        {
            string key = DayPlanDayKey.For(day);
            Dictionary<string, int> dayMap;
            if (!_overridesByDay.TryGetValue(key, out dayMap)) return;
            if (!dayMap.Remove(eventId)) return;
            if (dayMap.Count == 0) _overridesByDay.Remove(key);
        }
    }

    /// <summary>
    /// Whether a date assignment actually moves the Day Plan panel to another day.
    ///
    /// The panel's date is re-assigned on plenty of refreshes that land on the same day, and
    /// committing an open name editor on every one of those would yank the cursor out mid-word. Only
    /// a real day change should end editing.
    /// </summary>
    public static class DayPlanDateChange
    {
        /// <summary>
        /// Calendar-day based, NOT an elapsed-time threshold. Two seconds either side of midnight is
        /// a day change and twenty-three hours inside one day is not, so any "more than 24 hours"
        /// test would get both cases wrong.
        /// </summary>
        public static bool IsADifferentDay(DateTimeOffset from, DateTimeOffset to)
        {
            return from.Date != to.Date;
        }
    }

    /// <summary>Where "View full day plan" should take the user.</summary>
    public enum DayPlanTarget
    {
        /// <summary>The main window's own Day Plan pane.</summary>
        MainWindowDayPlan,

        /// <summary>The Day Plan tab embedded in the allowed-activity habit player.</summary>
        AllowedActivityDayPlanTab,
    }

    /// <summary>
    /// There are two Day Plans on screen at different times: the main window's own pane, and the copy
    /// embedded as a tab inside the allowed-activity habit player. While an allowed-app habit is
    /// playing, the player owns the habits area and the main window's Day Plan deliberately refuses to
    /// show (a routine is live) — so aiming at it left the user looking at the Habit Player tab and
    /// nothing appeared to happen.
    /// </summary>
    public static class DayPlanDestination
    {
        /// <param name="hasAllowedActivityTabs">Whether the allowed-activity player's
        /// "Habit Player | Day Plan" tab bar is currently on screen.</param>
        public static DayPlanTarget Target(bool hasAllowedActivityTabs)
        {
            return hasAllowedActivityTabs
                ? DayPlanTarget.AllowedActivityDayPlanTab
                : DayPlanTarget.MainWindowDayPlan;
        }
    }
}
