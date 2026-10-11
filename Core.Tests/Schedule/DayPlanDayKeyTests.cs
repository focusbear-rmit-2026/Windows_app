using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Schedule
{
    [TestClass]
    public class DayPlanDayKeyTests
    {
        [TestMethod]
        public void TheKeyIsTheGregorianDayWhateverTheThreadCulture()
        {
            // The key is also the backend's day (GET /day-plan/day/{day}, dismissed events). A Thai thread would have
            // spelled 2026 as 2569, and the Day Plan's stores keyed by one form would miss what was saved under the other.
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("th-TH");
                Assert.AreEqual("2026-09-26", DayPlanDayKey.For(new DateTime(2026, 9, 26, 23, 30, 0)));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [TestMethod]
        public void AMeetingIsFiledUnderEveryDayItCovers()
        {
            DateTime night = new DateTime(2026, 9, 26, 23, 30, 0);
            CollectionAssert.AreEqual(new[] { "2026-09-26", "2026-09-27" }, DayPlanDayKey.DaysCovered(night, night.AddHours(1)).ToArray(),
                "23:30-00:30 also blocks the next morning's first half hour");
            CollectionAssert.AreEqual(new[] { "2026-09-26" }, DayPlanDayKey.DaysCovered(night, night.AddMinutes(30)).ToArray(), "ending at midnight is not the next day");
            CollectionAssert.AreEqual(new[] { "2026-09-26" }, DayPlanDayKey.DaysCovered(night, night).ToArray(), "no length: its start day");
            Assert.AreEqual(7, DayPlanDayKey.DaysCovered(night, night.AddDays(30)).Count, "capped");
        }
    }
}
