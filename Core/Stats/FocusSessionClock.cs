using System;
using System.Globalization;

namespace FocusBear.Core.Stats
{
    /// <summary>
    /// The words the countdown headline is built from. Config-driven on the Mac so Spanish reads
    /// correctly; the same applies here, so they are never hard-coded into the formatter.
    /// </summary>
    public sealed class FocusSessionDurationUnits
    {
        public FocusSessionDurationUnits(string hour, string minute, string second)
        {
            Hour = hour;
            Minute = minute;
            Second = second;
        }

        public string Hour { get; }
        public string Minute { get; }
        public string Second { get; }
    }

    /// <summary>
    /// Countdown and progress arithmetic for the focus-session timer. Port of the Mac's
    /// <c>FocusSessionClock</c>.
    /// </summary>
    public static class FocusSessionClock
    {
        public const int SecondsPerMinute = 60;
        public const int SecondsPerHour = 3600;

        /// <summary>Never negative — an overrun session reads "0 sec" rather than counting backwards.</summary>
        public static int RemainingSeconds(DateTime end, DateTime now)
        {
            return Math.Max(0, (int)Math.Floor((end - now).TotalSeconds));
        }

        /// <summary>The design's headline format: "24 min 36 sec", growing to "1 hr 24 min" past the hour.</summary>
        public static string Headline(int remainingSeconds, FocusSessionDurationUnits units)
        {
            int clamped = Math.Max(0, remainingSeconds);
            int hours = clamped / SecondsPerHour;
            int minutes = clamped % SecondsPerHour / SecondsPerMinute;
            int seconds = clamped % SecondsPerMinute;

            if (hours > 0)
            {
                return hours + " " + units.Hour + " " + minutes + " " + units.Minute;
            }

            if (minutes > 0)
            {
                return minutes + " " + units.Minute + " " + seconds + " " + units.Second;
            }

            return seconds + " " + units.Second;
        }

        /// <summary>
        /// Whole minutes left, rounded UP so a session with twenty seconds to go still reads "1m"
        /// rather than "0m" in the left menu's "Focusing (Nm left)" row.
        /// </summary>
        public static int MinutesRemaining(DateTime end, DateTime now)
        {
            int seconds = RemainingSeconds(end, now);
            return (int)Math.Ceiling((double)seconds / SecondsPerMinute);
        }

        /// <summary>
        /// How long the session runs in total, for the progress bar: the largest of the planned
        /// length, the live length, and the time actually remaining.
        ///
        /// That last term is what makes it safe. The live length can still hold a PREVIOUS session's
        /// value when a new one starts, and a total below the time remaining makes elapsed clamp to
        /// zero — the reported bug was a bar sitting at 00:00 while the total contradicted the
        /// countdown beside it.
        /// </summary>
        public static int SessionTotalSeconds(int plannedMinutes, int liveSeconds, int remainingSeconds)
        {
            return Math.Max(Math.Max(plannedMinutes * SecondsPerMinute, liveSeconds), remainingSeconds);
        }

        /// <summary>
        /// A short elapsed reading for the active-task card: seconds in the first minute, then whole
        /// minutes, then hours and minutes. Deliberately coarse so the card is not ticking every second.
        /// </summary>
        public static string CompactElapsed(int seconds, FocusSessionDurationUnits units)
        {
            int clamped = Math.Max(0, seconds);
            int hours = clamped / SecondsPerHour;
            int minutes = clamped % SecondsPerHour / SecondsPerMinute;

            if (hours > 0)
            {
                return hours + " " + units.Hour + " " + minutes + " " + units.Minute;
            }

            if (clamped >= SecondsPerMinute)
            {
                return minutes + " " + units.Minute;
            }

            return clamped + " " + units.Second;
        }

        /// <summary>Zero-padded clock text — "07:12" under an hour, "01:07:12" over it.</summary>
        public static string ClockText(int seconds)
        {
            int clamped = Math.Max(0, seconds);
            int hours = clamped / SecondsPerHour;
            int minutes = clamped % SecondsPerHour / SecondsPerMinute;
            int remainder = clamped % SecondsPerMinute;

            if (hours > 0)
            {
                return Padded(hours) + ":" + Padded(minutes) + ":" + Padded(remainder);
            }

            return Padded(minutes) + ":" + Padded(remainder);
        }

        /// <summary>Fraction of the session already spent, clamped to 0…1 for the progress bar.</summary>
        public static double Progress(int elapsedSeconds, int totalSeconds)
        {
            if (totalSeconds <= 0)
            {
                return 0;
            }

            return Math.Min(1, Math.Max(0, (double)elapsedSeconds / totalSeconds));
        }

        private static string Padded(int value)
        {
            return value.ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
