using System;
using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Schedule
{
    [TestClass]
    public class ShutOffClockTests
    {
        private static DateTime At(int h, int m) => new DateTime(2026, 9, 27, h, m, 0);

        [TestMethod]
        public void UnknownTimesAreNeverShutOffTimeAndNeverThrow()
        {
            // The start-up smoke test (27 Sep): no settings yet, IsShutOffTime threw out of StartWorkFlow.
            Assert.IsFalse(ShutOffClock.IsShutOffTime(null, null, At(23, 0)));
            Assert.IsFalse(ShutOffClock.IsShutOffTime("07:00", null, At(23, 0)));
            Assert.IsFalse(ShutOffClock.IsShutOffTime("", "22:00", At(23, 0)));
            Assert.IsFalse(ShutOffClock.IsShutOffTime("7am", "22:00", At(23, 0)));
        }

        [TestMethod]
        public void OutsideTheDayIsShutOffTime()
        {
            Assert.IsTrue(ShutOffClock.IsShutOffTime("07:00", "22:00", At(22, 30)));
            Assert.IsTrue(ShutOffClock.IsShutOffTime("07:00", "22:00", At(6, 30)));
            Assert.IsFalse(ShutOffClock.IsShutOffTime("07:00", "22:00", At(12, 0)));
            Assert.IsFalse(ShutOffClock.IsShutOffTime("07:00:00", "22:00:00", At(22, 0)), "the shut-off minute itself is still the day");
        }

        [TestMethod]
        public void ADayThatRunsPastMidnightWraps()
        {
            // Start 10:00, shut-off 01:00: shut-off time is 01:00-10:00.
            Assert.IsTrue(ShutOffClock.IsShutOffTime("10:00", "01:00", At(3, 0)));
            Assert.IsFalse(ShutOffClock.IsShutOffTime("10:00", "01:00", At(23, 0)));
            Assert.IsFalse(ShutOffClock.IsShutOffTime("10:00", "01:00", At(0, 30)));
        }
    }
}
