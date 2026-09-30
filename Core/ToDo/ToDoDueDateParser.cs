using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace FocusBear.Core.ToDo
{
    /// <summary>
    /// Turns typed due dates into a local calendar day. A superset of the web's `parseSnoozeInput`
    /// (<c>utils/snooze-date.util.ts</c>). Grammar (case-insensitive, English keywords plus the
    /// culture's month / weekday names):
    ///
    /// <code>
    ///   today . tomorrow (hoy . manana)  -> that day
    ///   Nd / Nw / Nm / Ny . in N days.   -> now + N days / weeks / months / years. `m` is MONTHS,
    ///                                       never minutes - this grammar has no times in it at all.
    ///                                       The longer spellings parseSnoozeDate takes are accepted
    ///                                       too (mo, wk, yr).
    ///   -1d / -2w / -1m / -1y            -> back the same way, for a date that has already passed
    ///   fri . friday . next fri          -> the next occurrence of that weekday, never today
    ///   28 . 28th                        -> that day of this month if still ahead, else next month
    ///   28 sep . sep 28 . 28th sep       -> that date this year if not already past, else next year
    ///   25/9 . 25/9/2027                 -> day/month[/year] (month/day accepted when the day cannot
    ///                                       be a month)
    ///   2026-09-25                       -> that exact day
    /// </code>
    ///
    /// Anything else is null so the caller can leave the date untouched.
    /// </summary>
    public static class ToDoDueDateParser
    {
        private static readonly ISet<string> TodayKeywords =
            new HashSet<string>(StringComparer.Ordinal) { "today", "tod", "hoy" };

        private static readonly ISet<string> TomorrowKeywords =
            new HashSet<string>(StringComparer.Ordinal) { "tomorrow", "tom", "tmr", "mañana", "manana" };

        private static readonly IDictionary<string, int> EnglishWeekdays = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "sun", 1 }, { "sunday", 1 }, { "mon", 2 }, { "monday", 2 },
            { "tue", 3 }, { "tues", 3 }, { "tuesday", 3 },
            { "wed", 4 }, { "weds", 4 }, { "wednesday", 4 },
            { "thu", 5 }, { "thur", 5 }, { "thurs", 5 }, { "thursday", 5 },
            { "fri", 6 }, { "friday", 6 }, { "sat", 7 }, { "saturday", 7 },
        };

        private static readonly IDictionary<string, int> EnglishMonths = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "jan", 1 }, { "january", 1 }, { "feb", 2 }, { "february", 2 },
            { "mar", 3 }, { "march", 3 }, { "apr", 4 }, { "april", 4 },
            { "may", 5 }, { "jun", 6 }, { "june", 6 }, { "jul", 7 }, { "july", 7 },
            { "aug", 8 }, { "august", 8 }, { "sep", 9 }, { "sept", 9 }, { "september", 9 },
            { "oct", 10 }, { "october", 10 }, { "nov", 11 }, { "november", 11 },
            { "dec", 12 }, { "december", 12 },
        };

        private const string OrdinalSuffix = "(?:st|nd|rd|th)?";

        /// <summary>
        /// The sign is optional so a due date can be moved backwards: "-1d" is yesterday, "-2w" a
        /// fortnight ago. Backdating is how an overdue task gets marked overdue on the day it was
        /// actually due.
        /// </summary>
        private static readonly Regex RelativePattern = Compile(
            @"^(?:in\s+)?([+-]?\d+)\s*(days?|d|weeks?|wks?|w|months?|mo|m|years?|yrs?|y)$");

        private static readonly Regex WeekdayPattern = Compile(@"^(?:next\s+)?([\p{L}.]+)$");
        private static readonly Regex DayOfMonthPattern = Compile(@"^(\d{1,2})" + OrdinalSuffix + "$");
        private static readonly Regex IsoPattern = Compile(@"^(\d{4})-(\d{1,2})-(\d{1,2})$");
        private static readonly Regex NumericDatePattern = Compile(@"^(\d{1,2})[/.-](\d{1,2})(?:[/.-](\d{2,4}))?$");
        private static readonly Regex DayMonthPattern = Compile(@"^(\d{1,2})" + OrdinalSuffix + @"\s+([\p{L}.]+)(?:\s+(\d{4}))?$");
        private static readonly Regex MonthDayPattern = Compile("^([\\p{L}.]+)\\s+(\\d{1,2})" + OrdinalSuffix + @"(?:,?\s+(\d{4}))?$");

        private const int MonthsPerYear = 12;
        private const int LongestMonthDays = 31;
        private const int DaysPerWeek = 7;
        private const int TwoDigitYearBase = 2000;
        private const int TwoDigitYearLength = 2;
        private const int MinYear = 1;
        private const int MaxYear = 9999;

        public static DateTime? Parse(string raw, DateTime now, CultureInfo culture = null)
        {
            // A step past the last representable day ("tomorrow" on 9999-12-31) is not a date (found by fuzzing).
            try { return ParseDate(raw, now, culture); }
            catch (ArgumentOutOfRangeException) { return null; }
            catch (OverflowException) { return null; }
        }

        private static DateTime? ParseDate(string raw, DateTime now, CultureInfo culture)
        {
            string input = Normalize(raw);
            if (input.Length == 0) return null;

            DateTime today = now.Date;
            if (TodayKeywords.Contains(input)) return today;
            if (TomorrowKeywords.Contains(input)) return today.AddDays(1);

            var names = LocalizedNames.For(culture ?? CultureInfo.CurrentCulture);
            return ParseRelative(input, today)
                ?? ParseWeekday(input, today, names)
                ?? ParseDayOfMonth(input, today)
                ?? ParseIsoDay(input)
                ?? ParseNumericDate(input, today)
                ?? ParseDayAndMonthName(input, today, names);
        }

        /// <summary>
        /// Lower-cased with runs of whitespace collapsed, so "in  3   weeks" reads as "in 3 weeks", and every
        /// decimal digit folded to ASCII. The patterns' <c>\d</c> matches any script's digits, and int.Parse
        /// then threw on the full-width "２５" a Japanese IME types — on every keystroke of the preview.
        /// </summary>
        private static string Normalize(string raw)
        {
            if (raw == null) return string.Empty;
            var folded = new System.Text.StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                folded.Append(c > '\u007f' && char.IsDigit(c) ? (char)('0' + (int)char.GetNumericValue(c)) : c);
            }
            string[] words = folded.ToString().ToLowerInvariant().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", words);
        }

        /// <summary>"2d", "3 weeks", "in 2 days", "1m", and backwards: "-1d" (yesterday), "-2w".</summary>
        private static DateTime? ParseRelative(string input, DateTime today)
        {
            Match match = RelativePattern.Match(input);
            if (!match.Success) return null;
            if (!int.TryParse(match.Groups[1].Value, NumberStyles.AllowLeadingSign,
                              CultureInfo.InvariantCulture, out int amount))
            {
                return null;
            }

            // The unit's first letter is enough to tell the four apart: d(ay), w(eek), y(ear), and
            // everything else in the alternation starts with m, for month. An offset past year 9999 (or
            // before year 1) is not a day at all — "99999999d" used to throw out of the preview.
            try
            {
                switch (match.Groups[2].Value[0])
                {
                    case 'd': return today.AddDays(amount);
                    case 'w': return today.AddDays(checked(amount * DaysPerWeek));
                    case 'y': return today.AddYears(amount);
                    default: return today.AddMonths(amount);
                }
            }
            catch (ArgumentOutOfRangeException) { return null; }
            catch (OverflowException) { return null; }
        }

        /// <summary>
        /// "fri", "friday", "next fri" — the next occurrence, never today ("next" reads the same
        /// because a bare weekday already means the coming one).
        /// </summary>
        private static DateTime? ParseWeekday(string input, DateTime today, LocalizedNames names)
        {
            Match match = WeekdayPattern.Match(input);
            if (!match.Success) return null;
            if (!names.Weekdays.TryGetValue(LocalizedNames.Key(match.Groups[1].Value), out int weekday)) return null;

            int current = (int)today.DayOfWeek + 1;
            int delta = (weekday - current + DaysPerWeek) % DaysPerWeek;
            if (delta == 0) delta = DaysPerWeek;
            return today.AddDays(delta);
        }

        /// <summary>
        /// "28", "28th" — this month while the day is still ahead, else next month; clamped to the
        /// target month's length.
        /// </summary>
        private static DateTime? ParseDayOfMonth(string input, DateTime today)
        {
            Match match = DayOfMonthPattern.Match(input);
            if (!match.Success) return null;
            int day = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (day < 1 || day > LongestMonthDays) return null;

            DateTime monthStart = new DateTime(today.Year, today.Month, 1);
            int daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
            if (day <= today.Day || day > daysInMonth)
            {
                monthStart = monthStart.AddMonths(1);
                daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
            }
            return monthStart.AddDays(Math.Min(day, daysInMonth) - 1);
        }

        private static DateTime? ParseIsoDay(string input)
        {
            Match match = IsoPattern.Match(input);
            if (!match.Success) return null;
            return ExactDay(Number(match, 1), Number(match, 2), Number(match, 3));
        }

        /// <summary>"25/9", "25.9.2027", "9/25" (swapped when only that reading is a valid date).</summary>
        private static DateTime? ParseNumericDate(string input, DateTime today)
        {
            Match match = NumericDatePattern.Match(input);
            if (!match.Success) return null;

            int day = Number(match, 1).Value;
            int month = Number(match, 2).Value;
            if (month > MonthsPerYear && day <= MonthsPerYear)
            {
                int swapped = day;
                day = month;
                month = swapped;
            }

            string yearText = match.Groups[3].Success ? match.Groups[3].Value : null;
            if (yearText != null)
            {
                int year = int.Parse(yearText, CultureInfo.InvariantCulture);
                return ExactDay(yearText.Length == TwoDigitYearLength ? TwoDigitYearBase + year : year, month, day);
            }
            return UpcomingDay(month, day, today);
        }

        /// <summary>"28 sep", "28th september", "sep 28", "sept 28, 2027".</summary>
        private static DateTime? ParseDayAndMonthName(string input, DateTime today, LocalizedNames names)
        {
            int? day = null;
            int? month = null;
            int? year = null;

            Match dayFirst = DayMonthPattern.Match(input);
            if (dayFirst.Success)
            {
                day = Number(dayFirst, 1);
                month = names.Month(dayFirst.Groups[2].Value);
                year = Number(dayFirst, 3);
            }
            else
            {
                Match monthFirst = MonthDayPattern.Match(input);
                if (!monthFirst.Success) return null;
                month = names.Month(monthFirst.Groups[1].Value);
                day = Number(monthFirst, 2);
                year = Number(monthFirst, 3);
            }

            if (!day.HasValue || !month.HasValue) return null;
            if (year.HasValue) return ExactDay(year, month, day);
            return UpcomingDay(month.Value, day.Value, today);
        }

        /// <summary>A month/day without a year: this year unless that day has already passed, then next year.</summary>
        private static DateTime? UpcomingDay(int month, int day, DateTime today)
        {
            DateTime? thisYear = ExactDay(today.Year, month, day);
            if (!thisYear.HasValue) return null;
            if (thisYear.Value >= today) return thisYear;
            return ExactDay(today.Year + 1, month, day);
        }

        /// <summary>Null for impossible dates (31 Feb, month 13) rather than a silent roll-over.</summary>
        private static DateTime? ExactDay(int? year, int? month, int? day)
        {
            if (!year.HasValue || !month.HasValue || !day.HasValue) return null;
            if (year.Value < MinYear || year.Value > MaxYear) return null;
            if (month.Value < 1 || month.Value > MonthsPerYear) return null;
            if (day.Value < 1 || day.Value > DateTime.DaysInMonth(year.Value, month.Value)) return null;
            return new DateTime(year.Value, month.Value, day.Value);
        }

        private static int? Number(Match match, int group)
        {
            Group captured = match.Groups[group];
            return captured.Success ? int.Parse(captured.Value, CultureInfo.InvariantCulture) : (int?)null;
        }

        private static Regex Compile(string pattern) => new Regex(pattern, RegexOptions.CultureInvariant);

        /// <summary>
        /// Month / weekday names of the app's culture, so "viernes" or "sept" work alongside the
        /// English ones. English is seeded first and the culture's symbols overwrite on collision, so
        /// English always parses whatever the culture is.
        /// </summary>
        private sealed class LocalizedNames
        {
            private static readonly object CacheLock = new object();
            private static readonly IDictionary<string, LocalizedNames> Cache =
                new Dictionary<string, LocalizedNames>(StringComparer.Ordinal);

            private LocalizedNames(CultureInfo culture)
            {
                DateTimeFormatInfo format = culture.DateTimeFormat;

                var weekdays = new Dictionary<string, int>(EnglishWeekdays, StringComparer.Ordinal);
                foreach (string[] symbols in new[] { format.DayNames, format.AbbreviatedDayNames })
                {
                    // .NET indexes weekday symbols from Sunday, as Foundation numbers them from 1.
                    for (int index = 0; index < symbols.Length; index++) Add(weekdays, symbols[index], index + 1);
                }

                var months = new Dictionary<string, int>(EnglishMonths, StringComparer.Ordinal);
                foreach (string[] symbols in new[]
                         {
                             format.MonthNames, format.AbbreviatedMonthNames,
                             format.MonthGenitiveNames, format.AbbreviatedMonthGenitiveNames,
                         })
                {
                    // These arrays carry a thirteenth, empty entry for the leap month of non-Gregorian
                    // calendars; Add skips it.
                    for (int index = 0; index < symbols.Length; index++) Add(months, symbols[index], index + 1);
                }

                Weekdays = weekdays;
                Months = months;
            }

            public IDictionary<string, int> Weekdays { get; }
            public IDictionary<string, int> Months { get; }

            public int? Month(string name)
            {
                return Months.TryGetValue(Key(name), out int month) ? month : (int?)null;
            }

            public static LocalizedNames For(CultureInfo culture)
            {
                lock (CacheLock)
                {
                    if (!Cache.TryGetValue(culture.Name, out LocalizedNames names))
                    {
                        names = new LocalizedNames(culture);
                        Cache[culture.Name] = names;
                    }
                    return names;
                }
            }

            /// <summary>Cultures abbreviate with a trailing dot ("sept."); typed input rarely includes it.</summary>
            public static string Key(string symbol) => symbol.ToLowerInvariant().Replace(".", string.Empty);

            private static void Add(IDictionary<string, int> names, string symbol, int value)
            {
                if (string.IsNullOrEmpty(symbol)) return;
                names[Key(symbol)] = value;
            }
        }
    }
}
