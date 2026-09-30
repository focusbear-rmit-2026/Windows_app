using System;
using System.Xml;

namespace FocusBear.Core.Startup
{
    /// <summary>
    /// How the Windows scheduled tasks that start Focus Bear are set up (report 2026-09-28_21-45-38: "Focus Bear takes
    /// a long time to start up when I restart my computer. There would be ample time for someone to uninstall it").
    /// </summary>
    public static class StartupTaskPolicy
    {
        /// <summary>
        /// The logon task starts the app at once. It used to wait four minutes after sign-in (a delay from the
        /// original import with no recorded reason), which with the start-up itself left Focus Bear off for about
        /// five minutes after every restart. Task Scheduler reads an empty delay as none.
        /// </summary>
        public const string LogonDelay = "";

        /// <summary>
        /// How often the watchdog checks that a killed Focus Bear is running again: the longest it can stay down after
        /// "End task". One minute is the shortest repetition Task Scheduler accepts; a probe that finds the app
        /// running leaves before loading anything.
        /// </summary>
        public static readonly TimeSpan WatchdogProbeInterval = TimeSpan.FromMinutes(1);

        /// <summary>The watchdog trigger's repetition, as Task Scheduler writes it ("PT1M").</summary>
        public static string WatchdogRepetitionInterval => XmlConvert.ToString(WatchdogProbeInterval);

        /// <summary>
        /// True when a logon trigger's delay (ISO 8601, as Task Scheduler stores it) holds the app back, so an existing
        /// task registered with the old four-minute wait gets repaired. Empty or zero is no delay; anything unreadable
        /// counts as a delay, so it is rewritten to a known value.
        /// </summary>
        public static bool DelaysStart(string delay)
        {
            if (string.IsNullOrWhiteSpace(delay)) return false;
            try { return XmlConvert.ToTimeSpan(delay.Trim()) > TimeSpan.Zero; }
            catch (FormatException) { return true; }
        }

        /// <summary>
        /// True when a watchdog task's repetition (ISO 8601) is slower than <see cref="WatchdogProbeInterval"/>, or
        /// unreadable, so the task is worth re-registering.
        /// </summary>
        public static bool WatchdogIntervalIsStale(string interval)
        {
            if (string.IsNullOrWhiteSpace(interval)) return true;
            try { return XmlConvert.ToTimeSpan(interval.Trim()) != WatchdogProbeInterval; }
            catch (FormatException) { return true; }
        }
    }
}
