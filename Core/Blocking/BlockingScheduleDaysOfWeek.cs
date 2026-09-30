using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// Decoding <c>blocking_schedule.days_of_week</c>, and the ONLY place the wire numbering meets
    /// the in-memory one.
    ///
    /// THE WIRE IS NUMBERS, 0 = SUNDAY … 6 = SATURDAY — JavaScript's <c>Date.getDay()</c>, with a
    /// sibling <c>days_of_week_text</c> field carrying the human-readable form. This is NOT the
    /// "MON".."SUN" vocabulary habits and managed policies use, and the two must not be unified: they
    /// are different fields on different resources that happen to describe days.
    ///
    /// The decode is deliberately forgiving because the backend has shipped all of these shapes at
    /// once. The regression behind it: one schedule came back with <c>[0…6]</c> and another with
    /// <c>["ALL"]</c>, the whole-array decode threw on the string element, and EVERY custom blocking
    /// schedule disappeared. So an unrecognised shape falls back to the full week — a schedule that
    /// blocks too often is recoverable; one that silently stopped existing is not.
    /// </summary>
    public static class BlockingScheduleDaysOfWeek
    {
        public const int WireSunday = 0;
        public const int WireSaturday = 6;

        /// <summary>Case-insensitive token the backend uses to mean "every day".</summary>
        public const string AllDaysToken = "all";

        /// <summary>The fallback, and the value for an absent <c>days_of_week</c>.</summary>
        public static readonly IReadOnlyList<int> FullWeekWire =
            new[] { 0, 1, 2, 3, 4, 5, 6 };

        /// <summary>Lower-cased day names (full plus the abbreviations seen on the wire) → wire index.</summary>
        private static readonly IDictionary<string, int> DayNameToWireIndex =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "sunday", 0 }, { "sun", 0 },
                { "monday", 1 }, { "mon", 1 },
                { "tuesday", 2 }, { "tue", 2 }, { "tues", 2 },
                { "wednesday", 3 }, { "wed", 3 },
                { "thursday", 4 }, { "thu", 4 }, { "thur", 4 }, { "thurs", 4 },
                { "friday", 5 }, { "fri", 5 },
                { "saturday", 6 }, { "sat", 6 },
            };

        /// <summary>
        /// Wire days from a <c>days_of_week</c> token. <c>null</c> (the key was absent) and every
        /// unrecognised shape yield the full week.
        ///
        /// A list of day names only survives if EVERY name is recognised — a partly-understood list
        /// is treated as not understood, rather than silently enforcing the subset that happened to
        /// parse.
        /// </summary>
        public static IReadOnlyList<int> ParseWire(JToken daysOfWeek)
        {
            if (daysOfWeek == null || daysOfWeek.Type == JTokenType.Null) return FullWeekWire;
            var array = daysOfWeek as JArray;
            if (array == null) return FullWeekWire;

            var integers = new List<int>(array.Count);
            bool allIntegers = true;
            foreach (JToken element in array)
            {
                if (element.Type == JTokenType.Integer) integers.Add(element.Value<int>());
                else { allIntegers = false; break; }
            }
            if (allIntegers) return integers;

            var names = new List<string>(array.Count);
            foreach (JToken element in array)
            {
                if (element.Type != JTokenType.String) return FullWeekWire;
                names.Add(element.Value<string>().Trim().ToLowerInvariant());
            }

            if (names.Contains(AllDaysToken)) return FullWeekWire;

            var mapped = new List<int>(names.Count);
            foreach (string name in names)
            {
                int index;
                if (DayNameToWireIndex.TryGetValue(name, out index)) mapped.Add(index);
            }
            if (mapped.Count > 0 && mapped.Count == names.Count) return mapped;

            return FullWeekWire;
        }

        /// <summary>Wire (0 = Sunday) → in-memory calendar weekday (1 = Sunday).</summary>
        public static IReadOnlyList<int> ToCalendarWeekdays(IEnumerable<int> wireDays)
        {
            var calendarDays = new List<int>();
            if (wireDays == null) return calendarDays;
            foreach (int day in wireDays) calendarDays.Add(day + 1);
            return calendarDays;
        }

        /// <summary>In-memory calendar weekday (1 = Sunday) → wire (0 = Sunday), for a write back.</summary>
        public static IReadOnlyList<int> ToWireDays(IEnumerable<int> calendarWeekdays)
        {
            var wireDays = new List<int>();
            if (calendarWeekdays == null) return wireDays;
            foreach (int day in calendarWeekdays) wireDays.Add(day - 1);
            return wireDays;
        }
    }
}
