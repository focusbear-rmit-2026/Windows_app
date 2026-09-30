using System;
using System.Collections.Generic;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// Weekday numbering as the Mac app holds it IN MEMORY: <c>Calendar.component(.weekday)</c>,
    /// 1 = Sunday … 7 = Saturday.
    ///
    /// Three numberings meet around blocking schedules and none of them may be unified:
    /// <list type="bullet">
    /// <item>THIS one, 1…7, is what <see cref="CustomBlockingSchedule.DaysOfWeek"/> holds.</item>
    /// <item>The WIRE, <c>blocking_schedule.days_of_week</c>, is 0 = Sunday … 6 = Saturday —
    ///       JavaScript's <c>Date.getDay()</c>, with a sibling <c>days_of_week_text</c> field. See
    ///       <see cref="BlockingScheduleDaysOfWeek"/>, which is the only place the +1/-1 shift lives.</item>
    /// <item>Habits and managed policies use "MON".."SUN" STRING codes (see
    ///       <c>FocusBear.Core.Schedule.WeekdayCode</c>). That is a different vocabulary for a
    ///       different feature; blocking schedules never speak it.</item>
    /// </list>
    /// </summary>
    public static class CalendarWeekday
    {
        public const int Sunday = 1;
        public const int Saturday = 7;

        public static int For(DateTime date)
        {
            // .NET counts Sunday as 0; Foundation counts it as 1.
            return (int)date.DayOfWeek + 1;
        }
    }

    /// <summary>Strictness of a running block, the Mac's <c>FOCUSMODESTRICTNESS</c>.</summary>
    public enum FocusModeStrictness
    {
        Normal,
        Strict,
        SuperStrict,
    }

    /// <summary>Whether a schedule is running, the Mac's <c>State</c> enum.</summary>
    public enum BlockingScheduleState
    {
        Off,
        On,
        Pause,
    }

    /// <summary>
    /// A custom blocking schedule and the two questions asked of it every tick: is it blocking right
    /// now, and for how much longer. Ported from the Mac's <c>customBlockingSchedule</c>.
    ///
    /// <see cref="StartTime"/> and <see cref="EndTime"/> carry a date, but ONLY their time of day is
    /// used by <see cref="IsBlockingActive"/> and <see cref="RemainingBlockingDuration"/> — the
    /// window is re-based onto the day being asked about. A window whose end is at or before its
    /// start crosses midnight and belongs to the day it STARTED on, which is why a 22:00–02:00
    /// schedule set for Monday is still blocking at 01:00 on Tuesday.
    /// </summary>
    public sealed class CustomBlockingSchedule
    {
        /// <summary>
        /// What a schedule that is not running reports as its remaining time. Not zero: the Mac's
        /// callers treat this as "nothing to count down", and zero would read as "ends now".
        /// </summary>
        public static readonly TimeSpan NotRunningRemaining = TimeSpan.FromSeconds(1);

        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string StartTimeString { get; set; }
        public string EndTimeString { get; set; }

        /// <summary>Calendar weekdays, 1 = Sunday … 7 = Saturday. See <see cref="CalendarWeekday"/>.</summary>
        public IReadOnlyList<int> DaysOfWeek { get; set; }

        public string FocusModeId { get; set; }
        public FocusModeStrictness BlockLevel { get; set; }
        public bool IsAIBlockingEnabled { get; set; }
        public bool IsMicroBreaksEnabled { get; set; }
        public string Emoji { get; set; }
        public BlockingScheduleState State { get; set; }

        public CustomBlockingSchedule()
        {
            DaysOfWeek = new int[0];
            State = BlockingScheduleState.Off;
        }

        public bool IsBlockingActive(DateTime now)
        {
            if (DaysOfWeek == null || DaysOfWeek.Count == 0) return false;

            int todayWeekday = CalendarWeekday.For(now);
            DateTime todayStart = now.Date + StartTime.TimeOfDay;
            DateTime todayEnd = now.Date + EndTime.TimeOfDay;

            if (todayStart < todayEnd)
            {
                if (!Contains(DaysOfWeek, todayWeekday)) return false;
                return now >= todayStart && now <= todayEnd;
            }

            // Crosses midnight. Either it started today and runs on past midnight …
            if (Contains(DaysOfWeek, todayWeekday) && now >= todayStart) return true;

            // … or it started yesterday and has not yet reached this morning's end.
            int yesterdayWeekday = CalendarWeekday.For(now.AddDays(-1));
            if (Contains(DaysOfWeek, yesterdayWeekday) && now <= todayEnd) return true;

            return false;
        }

        /// <summary>
        /// Time left in the window, or <see cref="NotRunningRemaining"/> when none is running.
        ///
        /// Deliberately DOES NOT consult <see cref="DaysOfWeek"/> — the Mac's does not either, so a
        /// schedule with no days still reports a remaining time when the clock is inside its window.
        /// Whether that should short-circuit is a product decision, not a porting one, so the
        /// behaviour is carried over rather than quietly fixed here.
        /// </summary>
        public TimeSpan RemainingBlockingDuration(DateTime now)
        {
            DateTime todayStart = now.Date + StartTime.TimeOfDay;
            DateTime todayEnd = AdjustedEnd(todayStart, now.Date + EndTime.TimeOfDay);

            DateTime yesterday = now.AddDays(-1);
            DateTime yesterdayStart = yesterday.Date + StartTime.TimeOfDay;
            DateTime yesterdayEnd = AdjustedEnd(yesterdayStart, yesterday.Date + EndTime.TimeOfDay);

            // Yesterday first: a cross-midnight window that started yesterday owns this moment.
            if (now >= yesterdayStart && now < yesterdayEnd) return yesterdayEnd - now;
            if (now >= todayStart && now < todayEnd) return todayEnd - now;
            return NotRunningRemaining;
        }

        /// <summary>An end at or before its start means the window runs into the next day.</summary>
        private static DateTime AdjustedEnd(DateTime start, DateTime rawEnd)
        {
            return rawEnd <= start ? rawEnd.AddDays(1) : rawEnd;
        }

        private static bool Contains(IReadOnlyList<int> days, int weekday)
        {
            for (int index = 0; index < days.Count; index++)
            {
                if (days[index] == weekday) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// The Mac's <c>customBlockingScheduleManager.isBlockingScheduleOverlapping</c> — the check that
    /// stops a user saving two schedules that fight over the same hour.
    /// </summary>
    public static class BlockingScheduleOverlap
    {
        /// <summary>
        /// Whether <paramref name="newSchedule"/> overlaps any of <paramref name="schedules"/> on a
        /// day they share. Touching windows do not overlap: a 12:00 start against a 12:00 end is
        /// adjacent, because the comparison is strict on both ends.
        ///
        /// <paramref name="excludingId"/> is how editing an existing schedule avoids colliding with
        /// itself.
        ///
        /// Note this compares the schedules' absolute start/end instants, NOT their time of day —
        /// faithfully to the Mac, where it holds only because every schedule's times are re-based
        /// onto today when they are parsed and again on each day change. Two schedules whose stored
        /// dates have drifted apart would silently stop overlapping.
        /// </summary>
        public static bool IsBlockingScheduleOverlapping(CustomBlockingSchedule newSchedule,
                                                         IEnumerable<CustomBlockingSchedule> schedules,
                                                         string excludingId = null)
        {
            if (newSchedule == null) throw new ArgumentNullException(nameof(newSchedule));
            if (schedules == null) return false;

            DateTime newStart = newSchedule.StartTime;
            DateTime newEnd = NormalizedEnd(newSchedule);

            foreach (int newDay in newSchedule.DaysOfWeek ?? (IReadOnlyList<int>)new int[0])
            {
                foreach (CustomBlockingSchedule existing in schedules)
                {
                    if (existing == null) continue;
                    if (excludingId != null && existing.Id == excludingId) continue;
                    if (existing.DaysOfWeek == null || !ContainsDay(existing.DaysOfWeek, newDay)) continue;

                    DateTime existingStart = existing.StartTime;
                    DateTime existingEnd = NormalizedEnd(existing);
                    if (newStart < existingEnd && newEnd > existingStart) return true;
                }
            }
            return false;
        }

        private static DateTime NormalizedEnd(CustomBlockingSchedule schedule)
        {
            return schedule.EndTime <= schedule.StartTime ? schedule.EndTime.AddDays(1) : schedule.EndTime;
        }

        private static bool ContainsDay(IReadOnlyList<int> days, int weekday)
        {
            for (int index = 0; index < days.Count; index++)
            {
                if (days[index] == weekday) return true;
            }
            return false;
        }
    }
}
