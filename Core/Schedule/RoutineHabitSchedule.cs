using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FocusBear.Core.Schedule
{
    /// <summary>
    /// The product-wide days-of-week vocabulary: uppercase three-letter English codes, plus the three
    /// group tokens the server also sends. The Mac derives a day's code with a fixed
    /// <c>en_US_POSIX</c> "EEE" formatter precisely so a localized day name (Spanish "SÁB") can never
    /// be compared against these English server values.
    /// </summary>
    public static class WeekdayCode
    {
        public const string Monday = "MON";
        public const string Tuesday = "TUE";
        public const string Wednesday = "WED";
        public const string Thursday = "THU";
        public const string Friday = "FRI";
        public const string Saturday = "SAT";
        public const string Sunday = "SUN";

        /// <summary>The seven codes in the web dashboard's display order (its <c>DaysOfWeek</c> enum).</summary>
        public static readonly IReadOnlyList<string> All =
            new[] { Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday };

        /// <summary>Server group token: every day.</summary>
        public const string EveryDay = "all";
        /// <summary>Server group token: Monday to Friday.</summary>
        public const string Weekdays = "weekdays";
        /// <summary>Server group token: Saturday and Sunday.</summary>
        public const string Weekend = "weekend";

        /// <summary>
        /// The code for a day, from the day's own offset. A switch rather than a format string so no
        /// culture can ever get between a date and the English code the server matches it against.
        /// </summary>
        public static string For(DateTimeOffset day)
        {
            switch (day.DayOfWeek)
            {
                case DayOfWeek.Monday: return Monday;
                case DayOfWeek.Tuesday: return Tuesday;
                case DayOfWeek.Wednesday: return Wednesday;
                case DayOfWeek.Thursday: return Thursday;
                case DayOfWeek.Friday: return Friday;
                case DayOfWeek.Saturday: return Saturday;
                default: return Sunday;
            }
        }
    }

    /// <summary>
    /// Whether a server days-of-week list ("all" / "weekdays" / "weekend" / "MON"…"SUN") includes a
    /// given weekday. Ported from the Mac's <c>isScheduledForWeekday</c> in <c>routineData.swift</c>.
    ///
    /// The bug it was extracted to fix: the checks were written inline and tested "sat" twice, so
    /// Sunday counted as a weekday and "weekend" never matched Sunday.
    /// </summary>
    public static class RoutineDaysOfWeek
    {
        public static bool IsScheduledForWeekday(IEnumerable<string> daysOfWeek, string currentWeekDay)
        {
            List<string> days = daysOfWeek == null ? new List<string>() : daysOfWeek.ToList();
            bool isWeekend = ContainsIgnoringCase(currentWeekDay, WeekdayCode.Saturday)
                             || ContainsIgnoringCase(currentWeekDay, WeekdayCode.Sunday);

            if (days.Any(d => EqualsIgnoringCase(d, WeekdayCode.EveryDay))) return true;
            if (days.Any(d => EqualsIgnoringCase(d, WeekdayCode.Weekdays))) return !isWeekend;
            if (days.Any(d => EqualsIgnoringCase(d, WeekdayCode.Weekend))) return isWeekend;
            return days.Any(d => EqualsIgnoringCase(d, currentWeekDay));
        }

        /// <summary>
        /// A CONTAINS, not an equality — so a full day name ("SATURDAY") is recognised as the weekend
        /// too, exactly as the Swift <c>localizedCaseInsensitiveContains</c> does. Ordinal rather than
        /// localized comparison: every value compared here is an ASCII English code, where the two
        /// agree, and an ordinal comparison cannot be changed by the user's locale.
        /// </summary>
        private static bool ContainsIgnoringCase(string haystack, string needle)
        {
            return haystack != null
                   && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool EqualsIgnoringCase(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// The <c>yyyy-MM-dd</c> key every per-day local store is filed under. One implementation, because
    /// the keys have to agree: a habit trimmed for a day and a habit skipped for that day are the same
    /// day. Invariant culture and the Gregorian calendar, so the key cannot change with the machine's
    /// regional settings — the Mac's <c>getStringFromDate</c> uses a locale-dependent
    /// <c>DateFormatter</c> here, which would render a different key under a non-Gregorian calendar.
    /// </summary>
    public static class DayPlanDayKey
    {
        public const string Format = "yyyy-MM-dd";

        public static string For(DateTimeOffset day)
        {
            return day.ToString(Format, CultureInfo.InvariantCulture);
        }

        /// <summary>The day of a local <see cref="DateTime"/>, in the Gregorian calendar whatever the thread's culture.</summary>
        public static string For(DateTime day)
        {
            return day.ToString(Format, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Every day a meeting covers, as keys: its start day and each day it runs into. 23:30-00:30 is filed under
        /// both days, so the next day's first half hour is busy too; one ending exactly at midnight is not. A
        /// meeting with no length is its start day's. Capped at <paramref name="maxDays"/>, so a stray week-long
        /// event can't fill the cache.
        /// </summary>
        public static IList<string> DaysCovered(DateTime start, DateTime end, int maxDays = 7)
        {
            var days = new List<string>();
            DateTime last = end > start ? end.AddTicks(-1).Date : start.Date;
            for (DateTime d = start.Date; d <= last && days.Count < Math.Max(1, maxDays); d = d.AddDays(1))
            {
                days.Add(For(d));
                if (d == DateTime.MaxValue.Date) break;   // AddDays past 9999-12-31 throws
            }
            return days;
        }

        /// <summary>
        /// Drops all but the most recent <paramref name="retainedDays"/> days of a day-keyed map, so a
        /// local store cannot grow without bound. <c>yyyy-MM-dd</c> sorts chronologically as a string,
        /// which is the whole reason for that key format.
        /// </summary>
        public static void PruneOldestDays<TValue>(IDictionary<string, TValue> map, int retainedDays)
        {
            if (map.Count <= retainedDays) return;
            HashSet<string> keep = new HashSet<string>(
                map.Keys.OrderBy(key => key, StringComparer.Ordinal).Skip(map.Count - retainedDays));
            foreach (string key in map.Keys.Where(key => !keep.Contains(key)).ToList()) map.Remove(key);
        }
    }

    /// <summary>
    /// Habits the user has skipped for a specific day, before that day arrives.
    ///
    /// "Allow me to skip certain habits in advance so if I know I don't have much time for habits for
    /// tomorrow I can" (report 2026-09-13_20-13-58). Today's skips are a server concern
    /// (<c>skipped_habit_ids</c> on the routine), but a skip for a future day has nowhere to live yet,
    /// so it is kept on this machine and applied when that day is built.
    ///
    /// The Mac stores the map in <c>UserDefaults</c>; here it is an ordinary dictionary the host app
    /// persists however it likes (it is deliberately plain JSON-shaped, <c>day → habit ids</c>).
    /// </summary>
    public sealed class DayPlanHabitSkips
    {
        /// <summary>Days kept. Older entries are dropped so the map cannot grow without bound.</summary>
        public const int RetainedDays = 60;

        private readonly Dictionary<string, List<string>> _skippedIdsByDay;

        public DayPlanHabitSkips(IDictionary<string, List<string>> stored = null)
        {
            _skippedIdsByDay = stored == null
                ? new Dictionary<string, List<string>>()
                : stored.ToDictionary(e => e.Key, e => new List<string>(e.Value));
        }

        /// <summary>The map as it should be persisted, oldest days already pruned away.</summary>
        public IReadOnlyDictionary<string, List<string>> SkippedIdsByDay
        {
            get { return _skippedIdsByDay; }
        }

        public ISet<string> SkippedIds(DateTimeOffset day)
        {
            List<string> ids;
            return _skippedIdsByDay.TryGetValue(DayPlanDayKey.For(day), out ids)
                ? new HashSet<string>(ids)
                : new HashSet<string>();
        }

        public bool IsSkipped(string habitId, DateTimeOffset day)
        {
            if (string.IsNullOrEmpty(habitId)) return false;
            return SkippedIds(day).Contains(habitId);
        }

        /// <summary>Returns the new state, so the caller can report what it did.</summary>
        public bool Toggle(string habitId, DateTimeOffset day)
        {
            string key = DayPlanDayKey.For(day);
            List<string> stored;
            HashSet<string> ids = _skippedIdsByDay.TryGetValue(key, out stored)
                ? new HashSet<string>(stored)
                : new HashSet<string>();

            bool nowSkipped = ids.Add(habitId);
            if (!nowSkipped) ids.Remove(habitId);

            if (ids.Count == 0) _skippedIdsByDay.Remove(key);
            else _skippedIdsByDay[key] = ids.OrderBy(id => id, StringComparer.Ordinal).ToList();

            DayPlanDayKey.PruneOldestDays(_skippedIdsByDay, RetainedDays);
            return nowSkipped;
        }

        public void Clear(DateTimeOffset day)
        {
            _skippedIdsByDay.Remove(DayPlanDayKey.For(day));
        }
    }

    /// <summary>
    /// Whether a habit belongs in a given day's routine.
    ///
    /// One rule, asked by both the Day Plan and the routine player. The skip store used to be read only
    /// when drawing the plan, so a habit skipped ahead for tomorrow disappeared from tomorrow's plan and
    /// then played anyway when tomorrow's routine started (report 2026-09-16_15-14-58 — a habit skipped
    /// the night before was offered the next morning). Anything deciding which habits run goes through
    /// here, so the two answers cannot drift apart again.
    /// </summary>
    public static class RoutineHabitSchedule
    {
        public static bool ShouldPlay(string habitId, IEnumerable<string> daysOfWeek, DateTimeOffset day,
                                      DayPlanHabitSkips skips = null)
        {
            return RoutineDaysOfWeek.IsScheduledForWeekday(daysOfWeek, WeekdayCode.For(day))
                   && !(skips != null && skips.IsSkipped(habitId, day));
        }
    }
}
