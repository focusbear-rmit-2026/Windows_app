using System;
using System.Collections.Generic;
using System.Linq;
using Focus_Bear.Utils;
using FocusBear.Core.Schedule;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// The one answer to "does this habit run on this day?", asked by the Day Plan AND the routine
    /// player. The rule is <see cref="RoutineHabitSchedule"/> in FocusBear.Core, where it is tested;
    /// this file only maps the WPF <see cref="habit"/> and a local <see cref="DateTime"/> onto it.
    /// <para>
    /// Before this there were four copies that disagreed. The Day Plan understood "weekdays" and
    /// "weekend"; the routine player only knew "all" and exact day codes, so a Weekdays habit showed in
    /// the plan every weekday and then never played. A habit with no days showed in the plan and was
    /// skipped (or threw) in the player.
    /// </para>
    /// </summary>
    public static class RoutineHabitDays
    {
        /// <summary>
        /// A habit with no days runs every day. That is the server's own reading — the backend filters a
        /// routine with <c>days_of_week ?? [ALL]</c> and refuses to store an empty list — so the only
        /// way this client sees none is a habit whose days were never set, which the server plays daily.
        /// Core's rule reads an empty list as "no day", so the default is applied here, before it.
        /// </summary>
        private static readonly string[] EveryDay = { WeekdayCode.EveryDay };

        public static bool IsScheduledOn(habit routineHabit, DateTime day)
        {
            IEnumerable<string> days = routineHabit.DaysOfWeek == null || routineHabit.DaysOfWeek.Count == 0
                ? (IEnumerable<string>)EveryDay
                : routineHabit.DaysOfWeek;
            return RoutineHabitSchedule.ShouldPlay(routineHabit.id, days, OnItsOwnClock(day));
        }

        /// <summary>The habits that run on <paramref name="day"/>, in their routine order.</summary>
        public static List<habit> ScheduledOn(IEnumerable<habit> habits, DateTime day)
        {
            return habits == null
                ? new List<habit>()
                : habits.Where(routineHabit => IsScheduledOn(routineHabit, day)).ToList();
        }

        /// <summary>
        /// The weekday is the one on the wall clock the caller passed. Built from ticks with a zero offset
        /// so no time-zone conversion can move it across midnight, whatever <see cref="DateTime.Kind"/> is.
        /// </summary>
        private static DateTimeOffset OnItsOwnClock(DateTime day)
        {
            return new DateTimeOffset(day.Ticks, TimeSpan.Zero);
        }
    }
}
