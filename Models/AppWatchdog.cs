using Focus_Bear.Utils;
using Microsoft.Win32;
using System;
using System.Linq;
using System.Threading;

namespace Focus_Bear.Models
{
    /// <summary>
    /// Brings Focus Bear back after it is killed — ended from Task Scheduler, "End task" in Task Manager, or a
    /// crash — without undoing a deliberate quit.
    ///
    /// A scheduled task (<see cref="TaskName"/>) launches the app with <see cref="LaunchArgument"/> every
    /// <see cref="ProbeInterval"/>. That launch is a probe: it exits at once, before any app state is loaded,
    /// unless the app is not running AND its last exit was not a clean one. Every graceful exit (quit, restart,
    /// update, sign-out, Windows shutting down) goes through Application_Exit, which marks the exit clean; a kill
    /// never reaches it, so the marker still says "running" and the probe starts the app for real.
    ///
    /// A build that keeps dying before its tray icon appears is retried less often (Core <c>WatchdogBackoff</c>), and
    /// support can switch the watchdog off for one Windows account with the <see cref="DisabledValue"/> registry value.
    /// Who gets it and how to remove it: docs/crash-watchdog.md.
    /// </summary>
    internal static class AppWatchdog
    {
        internal const string TaskName = "Focus Bear Watchdog";
        internal const string LaunchArgument = "/watchdog";

        /// <summary>
        /// How long Focus Bear can stay down after a kill. A probe that finds it running costs a fraction of a second.
        /// One minute (was two): the shortest repetition Task Scheduler allows (report 2026-09-28_21-45-38).
        /// </summary>
        internal static readonly TimeSpan ProbeInterval = FocusBear.Core.Startup.StartupTaskPolicy.WatchdogProbeInterval;

        /// <summary>
        /// Under the global key: the marker must be readable before anyone has signed in to the app. The E2E test
        /// build has its own global key (<see cref="E2EBuild"/>), so a killed test run can't tell the real app's
        /// watchdog that Focus Bear was killed and start a deliberately quit Focus Bear again.
        /// </summary>
        private static string RegistryPath => E2EBuild.RegistryRoot;
        private const string CleanExitValue = "WATCHDOG_CLEAN_EXIT";
        private const string Clean = "1";
        private const string Running = "0";

        /// <summary>The hidden off switch: "1" under the app's registry key stops the watchdog for this Windows account.</summary>
        internal const string DisabledValue = "WATCHDOG_DISABLED";
        private const string Disabled = "1";

        /// <summary>Watchdog relaunches in a row that have not reached the tray icon yet.</summary>
        private const string FailedStartsValue = "WATCHDOG_FAILED_STARTS";
        /// <summary>When the watchdog last relaunched the app, UTC ticks.</summary>
        private const string LastRelaunchValue = "WATCHDOG_LAST_RELAUNCH";

        internal enum LaunchDecision
        {
            Run,
            ExitSilently,
        }

        /// <summary>
        /// Whether a launch should go ahead. Only a watchdog probe is ever turned away: an ordinary launch (the
        /// logon task, a shortcut) keeps its existing single-instance behaviour.
        /// </summary>
        internal static LaunchDecision Decide(bool launchedByWatchdog, bool anotherInstanceRunning, bool lastExitWasClean)
        {
            if (!launchedByWatchdog) return LaunchDecision.Run;
            return anotherInstanceRunning || lastExitWasClean ? LaunchDecision.ExitSilently : LaunchDecision.Run;
        }

        internal static bool IsWatchdogLaunch(string[] args) =>
            args != null && args.Any(arg => string.Equals(arg, LaunchArgument, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Called from the App's static constructor, so a probe that isn't needed leaves before the app's settings,
        /// log files, telemetry or windows are touched — and before its exit handler could write anything.
        /// </summary>
        internal static void ExitEarlyIfProbeNotNeeded(string instanceMutexName)
        {
            try
            {
                if (!IsWatchdogLaunch(Environment.GetCommandLineArgs().Skip(1).ToArray())) return;
#if DEBUG
                // A developer's build is never brought back: stopping the debugger is a kill, so the watchdog
                // restarted the Debug exe within two minutes, which also locked bin\Debug against the next build.
                // This also quietens a task an earlier Debug run registered (see EnsureWatchdogTask).
                Environment.Exit(0);
#endif
                if (IsDisabled()) Environment.Exit(0);
                if (Decide(true, IsInstanceRunning(instanceMutexName), LastExitWasClean()) == LaunchDecision.ExitSilently)
                {
                    Environment.Exit(0);
                }
                if (!TryRecordRelaunch()) Environment.Exit(0);
            }
            catch (Exception)
            {
                // A probe that can't decide must not start a second copy of the app on top of a running one.
                Environment.Exit(0);
            }
        }

        private static bool IsInstanceRunning(string mutexName)
        {
            try
            {
                if (Mutex.TryOpenExisting(mutexName, out Mutex existing))
                {
                    existing.Dispose();
                    return true;
                }
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                // It exists; we just may not open it.
                return true;
            }
        }

        internal static bool LastExitWasClean() => Clean.Equals(ReadValue(CleanExitValue));

        /// <summary>Support's off switch for this Windows account (docs/crash-watchdog.md).</summary>
        internal static bool IsDisabled() => Disabled.Equals(ReadValue(DisabledValue));

        /// <summary>
        /// Counts this relaunch as a failed start until the app reaches its tray icon (<see cref="MarkStartedUp"/>).
        /// False when the back-off says to leave the app down for now.
        /// </summary>
        private static bool TryRecordRelaunch()
        {
            int.TryParse(ReadValue(FailedStartsValue), out int failedStarts);
            DateTime? lastRelaunch = long.TryParse(ReadValue(LastRelaunchValue), out long ticks) && ticks > 0
                ? new DateTime(ticks, DateTimeKind.Utc)
                : (DateTime?)null;
            DateTime now = DateTime.UtcNow;
            if (!FocusBear.Core.Startup.WatchdogBackoff.ShouldRelaunch(failedStarts, lastRelaunch, now)) return false;
            WriteValue(FailedStartsValue, (failedStarts + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
            WriteValue(LastRelaunchValue, now.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return true;
        }

        /// <summary>The app reached its tray icon: whatever happens next, this start did not fail.</summary>
        internal static void MarkStartedUp() => WriteValue(FailedStartsValue, "0");

        private static string ReadValue(string name)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
            {
                return key?.GetValue(name) as string;
            }
        }

        /// <summary>The primary instance has started: from now on only a graceful exit counts as clean.</summary>
        internal static void MarkRunning() => WriteMarker(Running);

        /// <summary>A graceful exit: the next probe leaves the app closed.</summary>
        internal static void MarkCleanExit() => WriteMarker(Clean);

        private static void WriteMarker(string value) => WriteValue(CleanExitValue, value);

        private static void WriteValue(string name, string value)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    key.SetValue(name, value, RegistryValueKind.String);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AppWatchdog marker write ex - " + ex.Message);
            }
        }
    }
}
