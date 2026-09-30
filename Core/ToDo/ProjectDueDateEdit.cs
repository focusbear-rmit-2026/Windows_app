using System;
using System.Collections.Generic;

namespace FocusBear.Core.ToDo
{
    /// <summary>The one-click choices under the due-date field, in the Mac's order.</summary>
    public enum DueDateQuickOption { Today, Tomorrow, NextWeek, Clear }

    /// <summary>
    /// The arithmetic behind the Mac's <c>ToDoDueDatePopover</c> as the project cards use it: which presets
    /// it offers, the day each one means, what goes on the wire as <c>due_date</c>, and whether a pick is
    /// a change at all (an unchanged pick costs no request).
    /// </summary>
    public static class ProjectDueDateEdit
    {
        /// <summary>Today · Tomorrow · Next week, and Clear only when there is a date to clear.</summary>
        public static IList<DueDateQuickOption> QuickOptions(string currentDue)
        {
            var options = new List<DueDateQuickOption> { DueDateQuickOption.Today, DueDateQuickOption.Tomorrow, DueDateQuickOption.NextWeek };
            if (!string.IsNullOrEmpty(currentDue)) options.Add(DueDateQuickOption.Clear);
            return options;
        }

        /// <summary>The local day a preset means; null for Clear. "Next week" is seven days on, as on the Mac.</summary>
        public static DateTime? DateFor(DueDateQuickOption option, DateTime now)
        {
            switch (option)
            {
                case DueDateQuickOption.Today: return now.Date;
                case DueDateQuickOption.Tomorrow: return now.Date.AddDays(1);
                case DueDateQuickOption.NextWeek: return now.Date.AddDays(7);
                default: return null;
            }
        }

        /// <summary><c>PUT /projects/:id { due_date }</c>: the local day as yyyy-MM-dd, or null to clear it.</summary>
        public static string WireValue(DateTime? day) => day.HasValue ? ToDoDates.Ymd(day.Value.Date) : null;

        /// <summary>Whether picking <paramref name="day"/> leaves the project's due date as it was.</summary>
        public static bool IsUnchanged(string currentDue, DateTime? day) =>
            ToDoDates.YmdFromIso(currentDue) == WireValue(day);

        /// <summary>The month and day the calendar opens on: the current due date, else today.</summary>
        public static DateTime CalendarStart(string currentDue, DateTime now) =>
            ToDoDates.Parse(currentDue)?.Date ?? now.Date;

        /// <summary>The portfolio row's <c>days_until_due</c> for a picked day, so the card is right before the refresh.</summary>
        public static int? DaysUntilDue(DateTime? day, DateTime now) =>
            day.HasValue ? ToDoDates.DaysBetween(now, day.Value) : (int?)null;
    }
}
