using System;
using FocusBear.Core.Stats;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Stats
{
    [TestClass]
    public class StatsTimeZoneTests
    {
        [TestMethod]
        public void AHalfHourZone_IsItsFixedOffset_NotUtc()
        {
            Assert.AreEqual("UTC+05:30", StatsTimeZone.ForOffset(new TimeSpan(5, 30, 0)));
            Assert.AreEqual("UTC+05:45", StatsTimeZone.ForOffset(new TimeSpan(5, 45, 0)));
            Assert.AreEqual("UTC-03:30", StatsTimeZone.ForOffset(new TimeSpan(-3, -30, 0)));
        }

        [TestMethod]
        public void WholeHourZones_AreEtcGmt_SignInverted()
        {
            Assert.AreEqual("Etc/GMT-10", StatsTimeZone.ForOffset(TimeSpan.FromHours(10)));
            Assert.AreEqual("Etc/GMT+5", StatsTimeZone.ForOffset(TimeSpan.FromHours(-5)));
            Assert.AreEqual("UTC", StatsTimeZone.ForOffset(TimeSpan.Zero));
        }
    }
}
