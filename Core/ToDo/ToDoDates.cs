using System;
using System.Globalization;

namespace FocusBear.Core.ToDo
{
    /// <summary>Web `getDueDateChipVariant`: red when overdue, amber when due today, nothing when completed.</summary>
    public enum ToDoDueState
    {
        None,
        Overdue,
        Today,
    }

    /// <summary>
    /// Reading and writing the dates a to-do carries. Ported from the Mac's <c>ToDoDates</c>.
    ///
    /// Dates are handled as local calendar days: a <see cref="DateTime"/> whose date component is the
    /// day the user means. That is what the Mac does through <c>Calendar.current</c>, and it is why
    /// every rule here compares <c>.Date</c> rather than instants — "overdue" is a question about
    /// days, not about hours.
    /// </summary>
    public static class ToDoDates
    {
        /// <summary>Web `formatToYMD`, and the form `PUT /to-do` accepts for a due date.</summary>
        public const string YmdFormat = "yyyy-MM-dd";

        private const string DayMonthFormat = "d MMM";
        private const string DayMonthYearFormat = "d MMM yyyy";
        private const string WeekdayDayMonthFormat = "ddd d MMM";
        private const string WeekdayDayMonthYearFormat = "ddd d MMM yyyy";

        /// <summary>The character that tells an ISO-8601 instant apart from a plain calendar day.</summary>
        private const char IsoTimeSeparator = 'T';

        private const int MinutesPerHour = 60;

        /// <summary>
        /// Accepts the API's ISO-8601 timestamps (with or without fractional seconds) and plain
        /// <c>yyyy-MM-dd</c>, and nothing else.
        ///
        /// The ISO branch is gated on the date/time separator on purpose: <c>DateTimeOffset.TryParse</c>
        /// would otherwise also accept whatever the running culture calls a date ("9/25/2026"), which
        /// the Mac's ISO8601DateFormatter rejects. A field this lenient would read a user-typed string
        /// as a wire value.
        /// </summary>
        public static DateTime? Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string trimmed = value.Trim();

            if (trimmed.IndexOf(IsoTimeSeparator) >= 0
                && DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture,
                                           DateTimeStyles.RoundtripKind, out DateTimeOffset instant))
            {
                // An instant is stored in UTC but *means* the local day it falls on, which is the day
                // the list, the chips and the forecast all group by.
                return instant.LocalDateTime;
            }

            if (DateTime.TryParseExact(trimmed, YmdFormat, CultureInfo.InvariantCulture,
                                       DateTimeStyles.None, out DateTime day))
            {
                return day;
            }

            return null;
        }

        public static string Ymd(DateTime date) => date.ToString(YmdFormat, CultureInfo.InvariantCulture);

        public static string YmdFromIso(string value)
        {
            DateTime? parsed = Parse(value);
            return parsed.HasValue ? Ymd(parsed.Value) : null;
        }

        /// <summary>Whole calendar days from <paramref name="now"/> to <paramref name="date"/> (negative = past).</summary>
        public static int DaysBetween(DateTime now, DateTime date) => (date.Date - now.Date).Days;

        public static ToDoDueState DueState(string isoString, bool isCompleted, DateTime now)
        {
            if (isCompleted) return ToDoDueState.None;
            DateTime? date = Parse(isoString);
            if (!date.HasValue) return ToDoDueState.None;

            int days = DaysBetween(now, date.Value);
            if (days < 0) return ToDoDueState.Overdue;
            if (days == 0) return ToDoDueState.Today;
            return ToDoDueState.None;
        }

        /// <summary>
        /// "Fri 11 Sep", or "Tue 4 Apr 2028" once the day falls outside the current year — a date years
        /// out read as if it were this one (Mac report 2026-09-15_20-52-05).
        ///
        /// Display only. The grammar cannot read either form back, so this is never a field's own text;
        /// <see cref="ToDoDueDateFieldText"/> uses <see cref="DayMonthYear"/>, which always carries the year.
        /// </summary>
        public static string WeekdayDayMonth(DateTime date, DateTime now, CultureInfo culture = null)
        {
            string format = IsSameYear(date, now) ? WeekdayDayMonthFormat : WeekdayDayMonthYearFormat;
            return date.ToString(format, culture ?? CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// "11 Sep 2026" — a due date as the *text* of the typed field. Unlike
        /// <see cref="WeekdayDayMonth"/> this is a form <see cref="ToDoDueDateParser"/> reads back, so
        /// the value the field shows survives the next save.
        /// </summary>
        public static string DayMonthYear(DateTime date, CultureInfo culture = null)
        {
            return date.ToString(DayMonthYearFormat, culture ?? CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Web `formatDueDate`: Today / Tomorrow / Yesterday, else "18 Jan" this year, "18 Jan 2027"
        /// otherwise. The three words are passed in because this project carries no translations.
        /// </summary>
        public static string DueLabel(string isoString, DateTime now, string today, string tomorrow,
                                      string yesterday, CultureInfo culture = null)
        {
            DateTime? date = Parse(isoString);
            if (!date.HasValue) return string.Empty;

            switch (DaysBetween(now, date.Value))
            {
                case 0: return today;
                case 1: return tomorrow;
                case -1: return yesterday;
                default:
                    return IsSameYear(date.Value, now)
                        ? date.Value.ToString(DayMonthFormat, culture ?? CultureInfo.InvariantCulture)
                        : DayMonthYear(date.Value, culture);
            }
        }

        /// <summary>Web `formatLocalizedDuration` fallback: "1h 30m", "2h", "45m".</summary>
        public static string DurationLabel(int hours, int minutes)
        {
            if (hours > 0 && minutes > 0)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0}h {1}m", hours, minutes);
            }
            if (hours > 0) return string.Format(CultureInfo.InvariantCulture, "{0}h", hours);
            return string.Format(CultureInfo.InvariantCulture, "{0}m", minutes);
        }

        /// <summary>"Task duration" seconds to a "1h 30m"-style label (minutes rounded down).</summary>
        public static string DurationLabelFromSeconds(int seconds)
        {
            int totalMinutes = Math.Max(0, seconds) / 60;
            return DurationLabel(totalMinutes / MinutesPerHour, totalMinutes % MinutesPerHour);
        }

        private static bool IsSameYear(DateTime date, DateTime now) => date.Year == now.Year;
    }
}
