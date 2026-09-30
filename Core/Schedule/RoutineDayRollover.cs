using System;

namespace FocusBear.Core.Schedule
{
    /// <summary>A time of day, with no date attached — the Mac's <c>Time</c> struct.</summary>
    public struct RoutineStartTime
    {
        public RoutineStartTime(int hour, int minute, int second = 0)
        {
            Hour = hour;
            Minute = minute;
            Second = second;
        }

        public int Hour { get; }
        public int Minute { get; }
        public int Second { get; }

        public TimeSpan TimeOfDay
        {
            get { return new TimeSpan(Hour, Minute, Second); }
        }
    }

    /// <summary>
    /// When one routine-day ends and the next begins, ported from the Mac's <c>isHabitAlreadyShown</c>.
    ///
    /// A routine-day does not start at midnight: before the routine's start time the user is still in
    /// YESTERDAY's day, so "now" rolls back a day before the two dates are compared. With no start time
    /// known — settings not loaded yet, or a profile whose startup time is missing or unparseable —
    /// there is nothing to roll back to, so the plain calendar day is used.
    ///
    /// On the Mac this was pulled out of <c>AppDelegate.checkLastHabitShowDate</c>, which force-unwrapped
    /// the start time and took the app down on launch whenever it was nil (EXC_BREAKPOINT in
    /// <c>readSettingsAndLoadApp</c>). "No start time" is an ordinary input here, not a crash.
    /// </summary>
    public static class RoutineDayRollover
    {
        public static bool IsHabitAlreadyShown(DateTimeOffset lastShown, DateTimeOffset now,
                                               RoutineStartTime? routineStart)
        {
            // Day arithmetic on the wall-clock component, the way Swift's Calendar does it: a day back
            // is the same clock time on the previous date, not 24 hours of elapsed time.
            DateTime today = now.DateTime;
            if (routineStart.HasValue && today.TimeOfDay < routineStart.Value.TimeOfDay)
            {
                today = today.AddDays(-1);
            }

            // Shown already unless the routine-day has moved PAST the day it was last shown on. A stored
            // date in the future (a clock change, a restored backup) therefore counts as already shown,
            // rather than re-showing the routine.
            return today.Date <= lastShown.DateTime.Date;
        }
    }
}
