using System.Globalization;

namespace FocusBear.Core.Schedule
{
    /// <summary>
    /// Compact habit-duration labels for the Day Plan rows and the habit editor: "45s", "1m 30s", "5m",
    /// "1h 5m". Seconds matter — a 1-second "log it" habit used to read as "0m".
    /// </summary>
    public static class HabitDurationText
    {
        public const int SecondsPerMinute = 60;
        public const int SecondsPerHour = 3600;

        private const string HourUnit = "h";
        private const string MinuteUnit = "m";
        private const string SecondUnit = "s";

        public static string Short(int seconds)
        {
            int hours = seconds / SecondsPerHour;
            int minutes = (seconds % SecondsPerHour) / SecondsPerMinute;
            int remainder = seconds % SecondsPerMinute;

            if (hours > 0) return Number(hours) + HourUnit + " " + Number(minutes) + MinuteUnit;
            if (minutes > 0 && remainder > 0) return Number(minutes) + MinuteUnit + " " + Number(remainder) + SecondUnit;
            if (minutes > 0) return Number(minutes) + MinuteUnit;
            return Number(remainder) + SecondUnit;
        }

        /// <summary>Digits only, never the machine's own numeral shapes — this is a compact token, not prose.</summary>
        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
