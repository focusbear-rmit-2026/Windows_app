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
                if (Decide(true, IsInstanceRunning(instanceMutexName), LastExitWasClean()) == LaunchDecision.ExitSilently)
                {
                    Environment.Exit(0);
                }
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

        internal static bool LastExitWasClean()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
            {
                return key != null && Clean.Equals(key.GetValue(CleanExitValue) as string);
            }
        }

        /// <summary>The primary instance has started: from now on only a graceful exit counts as clean.</summary>
        internal static void MarkRunning() => WriteMarker(Running);

        /// <summary>A graceful exit: the next probe leaves the app closed.</summary>
        internal static void MarkCleanExit() => WriteMarker(Clean);

        private static void WriteMarker(string value)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    key.SetValue(CleanExitValue, value, RegistryValueKind.String);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AppWatchdog marker write ex - " + ex.Message);
            }
        }
    }
}
