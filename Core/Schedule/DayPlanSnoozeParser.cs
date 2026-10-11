using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FocusBear.Core.ToDo;

namespace FocusBear.Core.Schedule
{
    /// <summary>
    /// The Day Plan's "move this task to…" grammar — the Mac's <c>parseSnoozeDate</c> (DayPlanTasks.swift),
    /// which the Windows Day Plan had an older copy of:
    ///
    /// <code>
    ///   today . tod . tom . tmr . tomorrow . yesterday . yest . yd
    ///   tue . tuesday                -> its next occurrence, strictly after today
    ///   fri 18th . Friday 18 . fri, 18 -> that date this month, only if it IS a Friday (can be earlier)
    ///   2d . 2w . 3mo . 1y . -1d     -> relative; a leading "-" back-dates the task
    ///   23 . 23rd                    -> that day of the month, this month or the next one that has it
    ///   1 Jul . Jul 1 . 1/7 . 2026-07-01 -> an absolute day, rolled a year forward if already past
    /// </code>
    ///
    /// Also, so nothing a Windows user typed before stops working: a leading "next " or "this " before a
    /// weekday ("next tue", which the help text advertised), and "next week" / "next month" / "next year".
    /// </summary>
    public static class DayPlanSnoozeParser
    {
        private const int HighestDayOfMonth = 31;
        private const int MonthsToSearchForDayNumber = 12;
        private static readonly string[] OrdinalSuffixes = { "st", "nd", "rd", "th" };

        private static readonly IDictionary<string, DayOfWeek> Weekdays = new Dictionary<string, DayOfWeek>(StringComparer.Ordinal)
        {
            { "sun", DayOfWeek.Sunday }, { "sunday", DayOfWeek.Sunday },
            { "mon", DayOfWeek.Monday }, { "monday", DayOfWeek.Monday },
            { "tue", DayOfWeek.Tuesday }, { "tues", DayOfWeek.Tuesday }, { "tuesday", DayOfWeek.Tuesday },
            { "wed", DayOfWeek.Wednesday }, { "weds", DayOfWeek.Wednesday }, { "wednesday", DayOfWeek.Wednesday },
            { "thu", DayOfWeek.Thursday }, { "thur", DayOfWeek.Thursday }, { "thurs", DayOfWeek.Thursday }, { "thursday", DayOfWeek.Thursday },
            { "fri", DayOfWeek.Friday }, { "friday", DayOfWeek.Friday },
            { "sat", DayOfWeek.Saturday }, { "saturday", DayOfWeek.Saturday },
        };

        /// <param name="baseDate">The real today — not the viewed day: on a future day "today" means the actual today.</param>
        public static DateTime? Parse(string input, DateTime baseDate, CultureInfo culture = null)
        {
            // An offset past year 9999 or before year 1 ("+9999y", "99999d"), or a step past the last day, is not a
            // date: it threw ArgumentOutOfRange out of the snooze box (found by fuzzing).
            try { return ParseDate(input, baseDate, culture); }
            catch (ArgumentOutOfRangeException) { return null; }
            catch (OverflowException) { return null; }
        }

        private static DateTime? ParseDate(string input, DateTime baseDate, CultureInfo culture)
        {
            string s = (input ?? "").Trim().ToLowerInvariant();
            if (s.Length == 0) return null;
            DateTime today = baseDate.Date;

            bool qualified = false;
            if (s.StartsWith("next ", StringComparison.Ordinal) || s.StartsWith("this ", StringComparison.Ordinal))
            {
                qualified = s.StartsWith("next ", StringComparison.Ordinal);
                s = s.Substring(5).Trim();
            }

            if (s == "today" || s == "tod") return today;
            if (s == "tom" || s == "tmr" || s == "tomorrow") return today.AddDays(1);
            if (s == "yesterday" || s == "yest" || s == "yd") return today.AddDays(-1);

            if (qualified)
            {
                switch (s)
                {
                    case "day": return today.AddDays(1);
                    case "week": case "wk": return today.AddDays(7);
                    case "month": case "mo": return today.AddMonths(1);
                    case "year": case "yr": return today.AddYears(1);
                }
            }

            // An explicit day number wins over a weekday shortcut: "fri 18th" can mean today, or an
            // earlier day of this month — moving Saturday's task back to "fri 18th" must not jump a month.
            var words = s.Replace(",", " ").Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 2 && Weekdays.TryGetValue(words[0], out var namedDay) && BareDayOfMonth(words[1]) is int number)
            {
                if (number > DateTime.DaysInMonth(today.Year, today.Month)) return null;
                var target = new DateTime(today.Year, today.Month, number);
                return target.DayOfWeek == namedDay ? target : (DateTime?)null;
            }

            // A weekday without a date is its next occurrence, strictly after today.
            if (Weekdays.TryGetValue(s, out var weekday))
            {
                var day = today.AddDays(1);
                while (day.DayOfWeek != weekday) day = day.AddDays(1);
                return day;
            }

            // [+-]<number><unit>: d / w / mo / y.
            string body = s;
            int sign = 1;
            if (body.StartsWith("-", StringComparison.Ordinal)) { sign = -1; body = body.Substring(1); }
            else if (body.StartsWith("+", StringComparison.Ordinal)) body = body.Substring(1);
            int digits = body.TakeWhile(char.IsDigit).Count();
            if (digits > 0 && int.TryParse(body.Substring(0, digits), NumberStyles.None, CultureInfo.InvariantCulture, out int n) && n > 0)
            {
                int m = sign * n;
                switch (body.Substring(digits).Trim())
                {
                    case "d": case "day": case "days": return today.AddDays(m);
                    case "w": case "wk": case "wks": case "week": case "weeks": return today.AddDays(checked(m * 7));
                    case "mo": case "month": case "months": return today.AddMonths(m);
                    case "y": case "yr": case "yrs": case "year": case "years": return today.AddYears(m);
                }
            }

            // A bare day number is that day of the MONTH, not a count of days, and it resolves forward.
            if (BareDayOfMonth(s) is int dayOfMonth) return NextOccurrence(dayOfMonth, today);

            // An absolute day; a date already past this year (no year given) rolls forward a year, so a
            // move always lands in the future.
            var absolute = ToDoDueDateParser.Parse(input, today, culture);
            if (!absolute.HasValue) return null;
            var date = absolute.Value.Date;
            if (date < today) date = date.AddYears(1);
            return date >= today ? date : (DateTime?)null;
        }

        /// <summary>"23", "23rd" — the day a bare number names, or null for anything else.</summary>
        private static int? BareDayOfMonth(string text)
        {
            int digits = text.TakeWhile(char.IsDigit).Count();
            if (digits == 0 || !int.TryParse(text.Substring(0, digits), NumberStyles.None, CultureInfo.InvariantCulture, out int day)) return null;
            if (day < 1 || day > HighestDayOfMonth) return null;
            string suffix = text.Substring(digits);
            return suffix.Length == 0 || OrdinalSuffixes.Contains(suffix) ? day : (int?)null;
        }

        /// <summary>This month when the day hasn't passed, else the next month that HAS it: 31 in February is 31 March.</summary>
        private static DateTime? NextOccurrence(int day, DateTime today)
        {
            var month = new DateTime(today.Year, today.Month, 1);
            for (int i = 0; i <= MonthsToSearchForDayNumber; i++, month = month.AddMonths(1))
            {
                if (day > DateTime.DaysInMonth(month.Year, month.Month)) continue;
                var candidate = new DateTime(month.Year, month.Month, day);
                if (candidate >= today) return candidate;
            }
            return null;
        }
    }
}
