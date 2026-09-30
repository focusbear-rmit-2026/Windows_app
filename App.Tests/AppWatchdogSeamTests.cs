using Focus_Bear.Models;
using Focus_Bear.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The watchdog probe brings Focus Bear back after a kill but must leave a deliberate quit alone, and it must
    /// never start a second copy. The marker round trip runs against the test host's own registry root.
    /// </summary>
    [TestClass]
    public class AppWatchdogSeamTests
    {
        [TestMethod]
        public void OnlyAProbeIsEverTurnedAway()
        {
            Assert.AreEqual(AppWatchdog.LaunchDecision.Run, AppWatchdog.Decide(false, true, true), "a normal launch keeps its single-instance path");
            Assert.AreEqual(AppWatchdog.LaunchDecision.Run, AppWatchdog.Decide(false, false, false));
        }

        [TestMethod]
        public void AProbeStartsTheAppOnlyAfterAKill()
        {
            Assert.AreEqual(AppWatchdog.LaunchDecision.Run, AppWatchdog.Decide(true, false, false), "killed: not running, exit not clean");
            Assert.AreEqual(AppWatchdog.LaunchDecision.ExitSilently, AppWatchdog.Decide(true, false, true), "quit on purpose");
            Assert.AreEqual(AppWatchdog.LaunchDecision.ExitSilently, AppWatchdog.Decide(true, true, false), "already running");
            Assert.AreEqual(AppWatchdog.LaunchDecision.ExitSilently, AppWatchdog.Decide(true, true, true));
        }

        [TestMethod]
        public void TheProbeArgument()
        {
            Assert.IsTrue(AppWatchdog.IsWatchdogLaunch(new[] { "/WATCHDOG" }));
            Assert.IsTrue(AppWatchdog.IsWatchdogLaunch(new[] { "focusbear://x", "/watchdog" }));
            Assert.IsFalse(AppWatchdog.IsWatchdogLaunch(new[] { "/watchdogx" }));
            Assert.IsFalse(AppWatchdog.IsWatchdogLaunch(new string[0]));
            Assert.IsFalse(AppWatchdog.IsWatchdogLaunch(null));
        }

        [TestMethod]
        public void TheCleanExitMarker_RoundTrips()
        {
            Assert.IsTrue(FocusBear.Core.AppInstance.IsTestHost, "never against the live registry");
            StringAssert.EndsWith(E2EBuild.RegistryRoot, "Focus Bear Tests");

            AppWatchdog.MarkRunning();
            Assert.IsFalse(AppWatchdog.LastExitWasClean(), "running: a kill now leaves this for the probe");
            AppWatchdog.MarkCleanExit();
            Assert.IsTrue(AppWatchdog.LastExitWasClean());
        }
    }
}
