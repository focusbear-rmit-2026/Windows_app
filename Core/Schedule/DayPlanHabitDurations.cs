using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.Schedule
{
    /// <summary>
    /// A habit trimmed for one specific day, without changing the habit itself.
    ///
    /// "Can we add a shorten option that makes the habit shorter just for today (only persists
    /// locally). Available both for current day and future days" (report 2026-09-15_20-29-02). A
    /// habit's real duration is a server setting shared with every other day and every other device, so
    /// a one-day trim has nowhere to live but this machine — it is kept here and applied when that day
    /// is built, exactly like <see cref="DayPlanHabitSkips"/>. Nothing here is ever sent to the server,
    /// so Settings › Edit habits keeps showing the true duration.
    ///
    /// As with the skips, the Mac keeps the map in <c>UserDefaults</c>; here it is a plain dictionary
    /// (<c>day → habit id → seconds</c>) the host app persists however it likes.
    /// </summary>
    public sealed class DayPlanHabitDurations
    {
        /// <summary>Days kept. Older entries are dropped so the map cannot grow without bound.</summary>
        public const int RetainedDays = 60;

        /// <summary>The shortest trim offered or accepted — below a minute a habit block stops being a
        /// usable slot.</summary>
        public const int MinimumSeconds = 60;

        /// <summary>The lengths offered in the sheet, longest first once filtered against the habit's
        /// real length.</summary>
        public static readonly IReadOnlyList<int> PresetMinutes = new[] { 30, 20, 15, 10, 5 };

        private readonly Dictionary<string, Dictionary<string, int>> _overridesByDay;

        public DayPlanHabitDurations(IDictionary<string, Dictionary<string, int>> stored = null)
        {
            _overridesByDay = stored == null
                ? new Dictionary<string, Dictionary<string, int>>()
                : stored.ToDictionary(e => e.Key, e => new Dictionary<string, int>(e.Value));
        }

        /// <summary>The map as it should be persisted.</summary>
        public IReadOnlyDictionary<string, Dictionary<string, int>> OverridesByDay
        {
            get { return _overridesByDay; }
        }

        /// <summary>The trim recorded for this habit on this day, or null when it runs its real length.</summary>
        public int? OverrideSeconds(string habitId, DateTimeOffset day)
        {
            if (string.IsNullOrEmpty(habitId)) return null;
            Dictionary<string, int> dayMap;
            int seconds;
            if (!_overridesByDay.TryGetValue(DayPlanDayKey.For(day), out dayMap)) return null;
            if (!dayMap.TryGetValue(habitId, out seconds) || seconds <= 0) return null;
            return seconds;
        }

        /// <summary>
        /// How long to schedule (or run) this habit on this day: the trim when there is one, never
        /// longer than the habit's real length — this only ever shortens. A stale trim (the habit was
        /// shortened in settings since) therefore loses to the habit's own length.
        /// </summary>
        public int EffectiveSeconds(string habitId, DateTimeOffset day, int actual)
        {
            int? trimmed = OverrideSeconds(habitId, day);
            return trimmed.HasValue ? Math.Min(trimmed.Value, actual) : actual;
        }

        /// <summary>
        /// Records a trim, clamped to at least <see cref="MinimumSeconds"/>. Asking for the real length
        /// (or longer) clears the trim instead of storing one. Returns the trim now in force, null when
        /// cleared.
        /// </summary>
        public int? SetOverride(int seconds, string habitId, DateTimeOffset day, int actual)
        {
            if (string.IsNullOrEmpty(habitId)) return null;
            int wanted = Math.Max(seconds, MinimumSeconds);
            if (wanted >= actual)
            {
                ClearOverride(habitId, day);
                return null;
            }

            string key = DayPlanDayKey.For(day);
            Dictionary<string, int> dayMap;
            if (!_overridesByDay.TryGetValue(key, out dayMap))
            {
                dayMap = new Dictionary<string, int>();
                _overridesByDay[key] = dayMap;
            }
            dayMap[habitId] = wanted;
            DayPlanDayKey.PruneOldestDays(_overridesByDay, RetainedDays);
            return wanted;
        }

        /// <summary>Back to the habit's real length on this day.</summary>
        public void ClearOverride(string habitId, DateTimeOffset day)
        {
            string key = DayPlanDayKey.For(day);
            Dictionary<string, int> dayMap;
            if (!_overridesByDay.TryGetValue(key, out dayMap)) return;
            if (!dayMap.Remove(habitId)) return;
            if (dayMap.Count == 0) _overridesByDay.Remove(key);
        }

        public void Clear(DateTimeOffset day)
        {
            _overridesByDay.Remove(DayPlanDayKey.For(day));
        }

        /// <summary>
        /// The trims worth offering for a habit of this real length: the presets that are genuinely
        /// shorter, longest first. A habit already near the floor gets the one-minute trim, and a habit
        /// at or under the floor gets nothing to choose from.
        /// </summary>
        public static IReadOnlyList<int> Options(int actualSeconds)
        {
            List<int> fromPresets = PresetMinutes
                .Select(m => m * HabitDurationText.SecondsPerMinute)
                .Where(s => s >= MinimumSeconds && s < actualSeconds)
                .ToList();
            if (fromPresets.Count > 0) return fromPresets;
            return actualSeconds > MinimumSeconds ? new List<int> { MinimumSeconds } : new List<int>();
        }

        /// <summary>What a typed length means for a habit of <paramref name="actualSeconds"/>.</summary>
        public enum TypedTrimOutcome
        {
            Apply,
            /// <summary>Nothing in the text reads as a length.</summary>
            NotADuration,
            /// <summary>A real length, but not shorter than the habit — refused rather than applied,
            /// because this only ever shortens. "Full length" is how you clear a trim.</summary>
            NotShorter,
        }

        public struct TypedTrim : IEquatable<TypedTrim>
        {
            private TypedTrim(TypedTrimOutcome outcome, int seconds)
            {
                Outcome = outcome;
                Seconds = seconds;
            }

            public TypedTrimOutcome Outcome { get; }
            /// <summary>Only meaningful for <see cref="TypedTrimOutcome.Apply"/>.</summary>
            public int Seconds { get; }

            public static TypedTrim Apply(int seconds)
            {
                return new TypedTrim(TypedTrimOutcome.Apply, seconds);
            }

            public static readonly TypedTrim NotADuration = new TypedTrim(TypedTrimOutcome.NotADuration, 0);
            public static readonly TypedTrim NotShorter = new TypedTrim(TypedTrimOutcome.NotShorter, 0);

            public bool Equals(TypedTrim other)
            {
                return Outcome == other.Outcome && Seconds == other.Seconds;
            }

            public override bool Equals(object obj)
            {
                return obj is TypedTrim && Equals((TypedTrim)obj);
            }

            public override int GetHashCode()
            {
                unchecked { return ((int)Outcome * 397) ^ Seconds; }
            }

            public override string ToString()
            {
                return Outcome == TypedTrimOutcome.Apply ? "Apply(" + Seconds + "s)" : Outcome.ToString();
            }
        }

        /// <summary>
        /// Kept here, pure, so the field's grammar and the "only ever shortens" rule are unit-tested
        /// rather than living in the sheet. The grammar itself is <see cref="DurationFieldParser"/> —
        /// the same parser the Day Plan's other duration fields use.
        /// </summary>
        public static TypedTrim TypedTrimFor(string text, int actualSeconds)
        {
            int? minutes = DurationFieldParser.DurationMinutesFromFieldText(text);
            if (!minutes.HasValue) return TypedTrim.NotADuration;
            int seconds = Math.Max(minutes.Value * HabitDurationText.SecondsPerMinute, MinimumSeconds);
            return seconds < actualSeconds ? TypedTrim.Apply(seconds) : TypedTrim.NotShorter;
        }

        /// <summary>Whole minutes to show for a length — a 90-second habit reads "2 min", never "0 min".
        /// Away-from-zero rounding, because .NET's default midpoint rule is banker's rounding and would
        /// report 2.5 minutes as 2 where Swift's <c>rounded()</c> says 3.</summary>
        public static int MinutesLabel(int seconds)
        {
            double minutes = (double)seconds / HabitDurationText.SecondsPerMinute;
            return Math.Max(1, (int)Math.Round(minutes, MidpointRounding.AwayFromZero));
        }
    }
}
