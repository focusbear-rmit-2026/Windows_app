using System;
using FocusBear.Core.Startup;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Startup
{
    /// <summary>#1588 review: a build that dies during start-up was relaunched every probe, with no tray icon to quit from.</summary>
    [TestClass]
    public class WatchdogBackoffTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void TheFirstFailedStartsAreRelaunchedAtOnce()
        {
            for (int failed = 0; failed < WatchdogBackoff.FreeRelaunches; failed++)
                Assert.IsTrue(WatchdogBackoff.ShouldRelaunch(failed, T0, T0.AddSeconds(60)), failed + " failed starts");
        }

        [TestMethod]
        public void AfterThatItWaitsTheDelayBetweenTries()
        {
            int failed = WatchdogBackoff.FreeRelaunches;
            Assert.IsFalse(WatchdogBackoff.ShouldRelaunch(failed, T0, T0.AddMinutes(1)));
            Assert.IsFalse(WatchdogBackoff.ShouldRelaunch(failed + 5, T0, T0 + WatchdogBackoff.Delay - TimeSpan.FromSeconds(1)));
            Assert.IsTrue(WatchdogBackoff.ShouldRelaunch(failed, T0, T0 + WatchdogBackoff.Delay));
        }

        [TestMethod]
        public void NoRecordedRelaunchOrAClockThatWentBackRelaunches()
        {
            Assert.IsTrue(WatchdogBackoff.ShouldRelaunch(10, null, T0));
            Assert.IsTrue(WatchdogBackoff.ShouldRelaunch(10, T0, T0.AddMinutes(-5)));
        }
    }
}
