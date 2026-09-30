using System;
using System.Globalization;

namespace FocusBear.Core.Schedule
{
    /// <summary>
    /// Whether it is the user's shut-off time: outside the day that runs from the morning start to the evening
    /// shut-off. Unknown times (no settings yet: a new account, or a settings read that failed with nothing cached)
    /// are never shut-off time. The app's old check parsed them straight away, and a null threw out of
    /// StartWorkFlow before the background timer started, so the app ran no routine or schedule checks at all.
    /// </summary>
    public static class ShutOffClock
    {
        /// <summary>"HH:mm" or "HH:mm:ss" as a time of day, or null.</summary>
        public static TimeSpan? TimeOfDay(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (!TimeSpan.TryParse(value.Trim(), CultureInfo.InvariantCulture, out TimeSpan t)) return null;
            if (t < TimeSpan.Zero || t >= TimeSpan.FromDays(1)) return null;
            return t;
        }

        public static bool IsShutOffTime(string dayStart, string shutOff, DateTime now)
        {
            TimeSpan? start = TimeOfDay(dayStart), shut = TimeOfDay(shutOff);
            if (!start.HasValue || !shut.HasValue) return false;
            // Whole minutes, as the app always compared.
            DateTime startAt = now.Date.AddHours(start.Value.Hours).AddMinutes(start.Value.Minutes);
            DateTime shutAt = now.Date.AddHours(shut.Value.Hours).AddMinutes(shut.Value.Minutes);
            if (startAt > shutAt) return now < startAt && now > shutAt;
            return now < startAt || now > shutAt;
        }
    }
}
