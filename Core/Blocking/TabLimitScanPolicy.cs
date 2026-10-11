using System;

namespace FocusBear.Core.Blocking
{
    /// <summary>What a tab-limit check should do this time.</summary>
    public enum TabScan
    {
        /// <summary>Nothing needs the browsers read.</summary>
        None,
        /// <summary>Count the tabs only to keep the focus session's productivity stats current.</summary>
        StatsOnly,
        /// <summary>Count the tabs and enforce the limit.</summary>
        Enforce,
    }

    /// <summary>
    /// When the tab limiter reads the browsers. A read is a cross-process UI Automation walk of every browser
    /// window, and it ran every 3 s (plus on browser events) whenever a focus session was on, limit or not, and
    /// overlapped itself when a read took longer than the timer. Now:
    /// <list type="bullet">
    /// <item>only one read at a time;</item>
    /// <item>enforcement only with a limit actually configured (on, at least one tab, not expired) and during a
    /// focus session or blocking schedule, as the setting says;</item>
    /// <item>without a limit, a focus session's stats are refreshed at most every <see cref="StatsInterval"/>;</item>
    /// <item>the fallback timer backs off for <see cref="NoBrowserBackoff"/> after a read found no browser
    /// window (a browser event still reads at once: it proves there is one).</item>
    /// </list>
    /// </summary>
    public static class TabLimitScanPolicy
    {
        public static readonly TimeSpan StatsInterval = TimeSpan.FromSeconds(60);
        public static readonly TimeSpan NoBrowserBackoff = TimeSpan.FromSeconds(30);

        public static bool IsLimitConfigured(bool limiterEnabled, int maxTabs, bool isExpired) =>
            limiterEnabled && maxTabs >= 1 && !isExpired;

        /// <param name="fromBrowserEvent">True when a browser window event asked (false: the fallback timer).</param>
        /// <param name="lastNoBrowserUtc">When the last read found no browser window, if it did.</param>
        /// <param name="lastScanUtc">When the last read finished.</param>
        public static TabScan Decide(bool limiterEnabled, int maxTabs, bool isExpired, bool focusRunning, bool scheduleRunning,
            bool inFlight, bool fromBrowserEvent, DateTime? lastNoBrowserUtc, DateTime? lastScanUtc, DateTime nowUtc)
        {
            if (inFlight) return TabScan.None;
            if (!fromBrowserEvent && lastNoBrowserUtc.HasValue && nowUtc - lastNoBrowserUtc.Value < NoBrowserBackoff) return TabScan.None;
            if (IsLimitConfigured(limiterEnabled, maxTabs, isExpired) && (focusRunning || scheduleRunning)) return TabScan.Enforce;
            if (!focusRunning) return TabScan.None;
            return !lastScanUtc.HasValue || nowUtc - lastScanUtc.Value >= StatsInterval ? TabScan.StatsOnly : TabScan.None;
        }
    }
}
