using System;
using System.Globalization;

namespace FocusBear.Core.Schedule
{
    /// <summary>Field readers for the raw <c>/to-do</c> JSON the Day Plan imports.</summary>
    public static class TodoFieldParsing
    {
        /// <summary>The Day Plan's duration for a to-do that carries none.</summary>
        public const int FallbackMinutes = 15;

        /// <summary>
        /// A to-do's estimated duration in seconds.
        ///
        /// The API returns <c>duration</c> as a STRING ("300", "3600", "0") — a Postgres numeric
        /// rendered as text — so a plain integer cast always fails and every imported to-do silently
        /// fell back to the 15-minute default, inflating the Day Plan's task totals. Accept both shapes.
        /// </summary>
        public static int DurationSeconds(object raw)
        {
            if (raw == null) return 0;

            if (raw is string text)
            {
                int parsed;
                return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                    ? parsed
                    : 0;
            }

            // Newtonsoft hands back JValue, the WPF app hands back boxed primitives; both are IConvertible.
            if (raw is IConvertible convertible)
            {
                try
                {
                    return (int)convertible.ToDouble(CultureInfo.InvariantCulture);
                }
                catch (Exception e) when (e is FormatException || e is InvalidCastException || e is OverflowException)
                {
                    return 0;
                }
            }

            return DurationSeconds(raw.ToString());
        }

        /// <summary>
        /// The Day Plan duration (minutes) for a to-do, defaulting to <paramref name="fallbackMinutes"/>
        /// when unset. A sub-minute duration still reads as at least a minute.
        /// </summary>
        public static int DurationMinutes(object raw, int fallbackMinutes = FallbackMinutes)
        {
            int seconds = DurationSeconds(raw);
            return seconds > 0
                ? Math.Max(1, seconds / HabitDurationText.SecondsPerMinute)
                : fallbackMinutes;
        }
    }
}
