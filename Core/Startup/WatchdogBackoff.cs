using System;

namespace FocusBear.Core.Startup
{
    /// <summary>
    /// When the crash watchdog stops relaunching a build that dies during start-up. Each watchdog relaunch counts as a
    /// failed start until that run reaches the tray icon (the app's "started" point, from where it can be quit); a
    /// run that gets there resets the count. After <see cref="FreeRelaunches"/> failed starts in a row the watchdog
    /// tries once every <see cref="Delay"/> instead of every probe, so a broken build is not started every minute
    /// with no tray icon to quit it from. An app killed after its tray icon appeared is relaunched at once, as before.
    /// </summary>
    public static class WatchdogBackoff
    {
        public const int FreeRelaunches = 3;
        public static readonly TimeSpan Delay = TimeSpan.FromMinutes(15);

        public static bool ShouldRelaunch(int failedStarts, DateTime? lastRelaunchUtc, DateTime nowUtc)
        {
            if (failedStarts < FreeRelaunches || !lastRelaunchUtc.HasValue) return true;
            TimeSpan since = nowUtc - lastRelaunchUtc.Value;
            // A clock that went back must not leave the app down until it catches up.
            return since < TimeSpan.Zero || since >= Delay;
        }
    }
}
