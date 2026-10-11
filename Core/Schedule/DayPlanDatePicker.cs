using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Forecast;

namespace FocusBear.Core.Schedule
{
    /// <summary>
    /// How much net free time a day has, and the band it falls in. Ported from the Mac's
    /// <c>DayPlanFreeLevel</c> (ScheduleViewController.swift). Net free = the day's free work minutes minus
    /// its tasks still to do (tasks pinned to a habit or meeting only count past what that block absorbs),
    /// so it can be at or below zero on an over-committed day.
    /// </summary>
    public static class DayPlanFreeLevel
    {
        /// <summary>At or below this many minutes free: red ("none left" / over-committed).</summary>
        public const int NoneMaxMinutes = 0;
        /// <summary>At or below this: orange (tight).</summary>
        public const int TightMaxMinutes = 60;
        /// <summary>At or below this: blue (some). More than this: green (plenty).</summary>
        public const int ComfortableMaxMinutes = 120;

        public enum Level { None, Tight, Some, Plenty }

        public static Level For(int freeMinutes)
        {
            if (freeMinutes <= NoneMaxMinutes) return Level.None;
            if (freeMinutes <= TightMaxMinutes) return Level.Tight;
            if (freeMinutes <= ComfortableMaxMinutes) return Level.Some;
            return Level.Plenty;
        }

        /// <summary>
        /// The Mac's <c>netFreeMinutes(on:)</c> from a day's load: available work minutes less the unfinished
        /// task work the day's blocks don't absorb (the tasks panel's "buffer"). Truncated toward zero like
        /// Swift's <c>Int(Double)</c>.
        /// </summary>
        public static int NetFreeMinutes(LocalTimeForecast.DayLoad load)
        {
            return (int)load.BufferMinutes;
        }
    }

    /// <summary>
    /// The badge decision for one day of the date picker's month grid (the Mac's <c>DayPlanDayBadge</c>):
    /// only future days are badged, and a day whose meetings have not been read yet gets no badge (unknown
    /// is never "plenty of room"). The free-minutes function is only called when the answer counts, so an
    /// unknown day costs no schedule build.
    /// </summary>
    public static class DayPlanDayBadge
    {
        public static DayPlanFreeLevel.Level? For(bool isFuture, bool inputsAreLoaded, Func<int?> netFreeMinutes)
        {
            if (!isFuture || !inputsAreLoaded || netFreeMinutes == null) return null;
            int? minutes = netFreeMinutes();
            return minutes.HasValue ? DayPlanFreeLevel.For(minutes.Value) : (DayPlanFreeLevel.Level?)null;
        }
    }

    /// <summary>
    /// The month grid behind the Day Plan's date label: six weeks of seven days starting on the locale's
    /// first weekday, with blank cells before the 1st and after the last day (the Mac's
    /// <c>DayPlanDatePickerController.rebuildGrid</c>).
    /// </summary>
    public static class DayPlanMonthGrid
    {
        public const int Columns = 7;
        public const int Weeks = 6;
        public const int Cells = Columns * Weeks;

        public static DateTime MonthStart(DateTime date) => new DateTime(date.Year, date.Month, 1);

        /// <summary>Blank cells before the 1st, given the first day of the week.</summary>
        public static int LeadingBlanks(DateTime monthStart, DayOfWeek firstDayOfWeek)
        {
            return ((int)monthStart.DayOfWeek - (int)firstDayOfWeek + Columns) % Columns;
        }

        /// <summary>The date shown in each of the 42 cells, or null for a blank padding cell.</summary>
        public static DateTime?[] CellDates(DateTime anyDayInMonth, DayOfWeek firstDayOfWeek)
        {
            DateTime start = MonthStart(anyDayInMonth);
            int blanks = LeadingBlanks(start, firstDayOfWeek);
            int days = DateTime.DaysInMonth(start.Year, start.Month);
            var cells = new DateTime?[Cells];
            for (int i = 0; i < Cells; i++)
            {
                int day = i - blanks + 1;
                cells[i] = day >= 1 && day <= days ? start.AddDays(day - 1) : (DateTime?)null;
            }
            return cells;
        }

        /// <summary>The weekday columns in display order, starting at <paramref name="firstDayOfWeek"/>.</summary>
        public static DayOfWeek[] WeekdayOrder(DayOfWeek firstDayOfWeek)
        {
            return Enumerable.Range(0, Columns).Select(i => (DayOfWeek)(((int)firstDayOfWeek + i) % Columns)).ToArray();
        }
    }

    /// <summary>
    /// Which calendar reads the picker needs so a shown month can be badged. Only future days are badged, so
    /// only they are loaded. Days inside the backend's rolling window (today .. today+maxWindowDays-1) come
    /// from one <c>days=N</c> read wide enough to cover them; days beyond it need a <c>day=</c> read each,
    /// capped per month.
    /// </summary>
    public static class DayPlanMonthLoad
    {
        public sealed class Plan
        {
            public Plan(int? windowDays, IReadOnlyList<DateTime> singleDays)
            {
                WindowDays = windowDays;
                SingleDays = singleDays;
            }

            /// <summary>A <c>days=N</c> read to make, or null when the window already covers the month.</summary>
            public int? WindowDays { get; }
            /// <summary>Days past the window still to read one at a time, soonest first.</summary>
            public IReadOnlyList<DateTime> SingleDays { get; }
            public bool IsEmpty => !WindowDays.HasValue && SingleDays.Count == 0;
        }

        /// <param name="monthDays">The month's days (any order; null entries ignored).</param>
        /// <param name="today">Today's date.</param>
        /// <param name="needsLoad">True when a day has not been read recently enough.</param>
        /// <param name="minWindowDays">The Day Plan's own window: never ask for a narrower one.</param>
        /// <param name="maxWindowDays">The widest <c>days=</c> read the backend allows.</param>
        /// <param name="maxSingleDays">At most this many <c>day=</c> reads.</param>
        public static Plan For(IEnumerable<DateTime?> monthDays, DateTime today, Func<DateTime, bool> needsLoad,
                               int minWindowDays, int maxWindowDays, int maxSingleDays)
        {
            today = today.Date;
            int? window = null;
            var singles = new List<DateTime>();
            foreach (DateTime day in (monthDays ?? Enumerable.Empty<DateTime?>()).Where(d => d.HasValue).Select(d => d.Value.Date).Distinct().OrderBy(d => d))
            {
                if (day <= today) continue;   // today and past days are never badged
                if (needsLoad != null && !needsLoad(day)) continue;
                int offset = (int)(day - today).TotalDays;
                if (offset < maxWindowDays)
                    window = Math.Max(window ?? 0, offset + 1);
                else if (singles.Count < maxSingleDays)
                    singles.Add(day);
            }
            if (window.HasValue) window = Math.Min(maxWindowDays, Math.Max(minWindowDays, window.Value));
            return new Plan(window, singles);
        }
    }
}
