using System;
using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Blocking
{
    [TestClass]
    public class TabLimitScanPolicyTests
    {
        private static readonly DateTime Now = new DateTime(2026, 9, 27, 1, 0, 0, DateTimeKind.Utc);

        private static TabScan Decide(bool enabled = true, int max = 10, bool expired = false, bool focus = true, bool schedule = false,
            bool inFlight = false, bool fromEvent = false, DateTime? noBrowser = null, DateTime? lastScan = null) =>
            TabLimitScanPolicy.Decide(enabled, max, expired, focus, schedule, inFlight, fromEvent, noBrowser, lastScan, Now);

        [TestMethod]
        public void AReadStillRunningIsNeverStartedAgain()
        {
            Assert.AreEqual(TabScan.None, Decide(inFlight: true));
            Assert.AreEqual(TabScan.None, Decide(inFlight: true, fromEvent: true));
        }

        [TestMethod]
        public void TheLimitIsEnforcedOnlyWhenConfiguredAndDuringFocusOrASchedule()
        {
            Assert.AreEqual(TabScan.Enforce, Decide(focus: true));
            Assert.AreEqual(TabScan.Enforce, Decide(focus: false, schedule: true));
            Assert.AreEqual(TabScan.None, Decide(focus: false, schedule: false));
            Assert.AreEqual(TabScan.None, Decide(enabled: false, focus: false, schedule: true), "no limit, no reason to read");
            Assert.AreEqual(TabScan.None, Decide(max: 0, focus: false, schedule: true), "on with no number is not a limit");
            Assert.AreEqual(TabScan.None, Decide(expired: true, focus: false, schedule: true));
        }

        [TestMethod]
        public void WithoutALimitAFocusSessionsStatsAreReadAMinuteApartNotEveryThreeSeconds()
        {
            Assert.AreEqual(TabScan.StatsOnly, Decide(enabled: false, lastScan: null));
            Assert.AreEqual(TabScan.None, Decide(enabled: false, lastScan: Now.AddSeconds(-3)));
            Assert.AreEqual(TabScan.StatsOnly, Decide(enabled: false, lastScan: Now - TabLimitScanPolicy.StatsInterval));
        }

        [TestMethod]
        public void TheTimerBacksOffWhenNoBrowserIsOpenButABrowserEventStillReads()
        {
            DateTime found = Now.AddSeconds(-5);
            Assert.AreEqual(TabScan.None, Decide(noBrowser: found));
            Assert.AreEqual(TabScan.Enforce, Decide(noBrowser: found, fromEvent: true));
            Assert.AreEqual(TabScan.Enforce, Decide(noBrowser: Now - TabLimitScanPolicy.NoBrowserBackoff));
        }
    }
}
