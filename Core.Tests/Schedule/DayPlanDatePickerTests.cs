using System;
using System.Linq;
using FocusBear.Core.Forecast;
using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Level = FocusBear.Core.Schedule.DayPlanFreeLevel.Level;

namespace FocusBear.Core.Tests.Schedule
{
    /// <summary>
    /// The Day Plan date picker: the Mac's DayPlanFreeLevel thresholds and DayPlanDayBadgeTests
    /// (ScheduleKitTests.swift), the month grid layout, and which calendar reads a shown month needs.
    /// </summary>
    [TestClass]
    public class DayPlanDatePickerTests
    {
        [TestMethod]
        public void FreeLevelThresholdsMatchTheMac()
        {
            Assert.AreEqual(Level.None, DayPlanFreeLevel.For(-60), "over-committed stays red however far past zero");
            Assert.AreEqual(Level.None, DayPlanFreeLevel.For(0));
            Assert.AreEqual(Level.Tight, DayPlanFreeLevel.For(1));
            Assert.AreEqual(Level.Tight, DayPlanFreeLevel.For(30));
            Assert.AreEqual(Level.Tight, DayPlanFreeLevel.For(60));
            Assert.AreEqual(Level.Some, DayPlanFreeLevel.For(61));
            Assert.AreEqual(Level.Some, DayPlanFreeLevel.For(90));
            Assert.AreEqual(Level.Some, DayPlanFreeLevel.For(120));
            Assert.AreEqual(Level.Plenty, DayPlanFreeLevel.For(121));
            Assert.AreEqual(Level.Plenty, DayPlanFreeLevel.For(480));
        }

        [TestMethod]
        public void NetFreeChargesOnlyTaskWorkTheBlocksDoNotAbsorb()
        {
            var day = new DateTime(2026, 9, 26);
            Assert.AreEqual(300, DayPlanFreeLevel.NetFreeMinutes(LocalTimeForecast.DayLoadFor(day, 360, 90, 30)));
            Assert.AreEqual(360, DayPlanFreeLevel.NetFreeMinutes(LocalTimeForecast.DayLoadFor(day, 360, 20, 30)), "absorbed time never hands time back");
            Assert.AreEqual(-40, DayPlanFreeLevel.NetFreeMinutes(LocalTimeForecast.DayLoadFor(day, 20, 60)), "an over-committed day goes negative");
            Assert.AreEqual(89, DayPlanFreeLevel.NetFreeMinutes(LocalTimeForecast.DayLoadFor(day, 89.9, 0)), "truncated like Swift's Int()");
            Assert.AreEqual(-89, DayPlanFreeLevel.NetFreeMinutes(LocalTimeForecast.DayLoadFor(day, 0, 89.9)), "toward zero, not down");
        }

        [TestMethod]
        public void AnUnloadedDayGetsNoBadgeAndIsNotMeasured()
        {
            bool measured = false;
            Assert.IsNull(DayPlanDayBadge.For(true, false, () => { measured = true; return 480; }),
                          "an unread day is unknown, not free");
            Assert.IsFalse(measured, "no schedule build for a day whose meetings haven't been read");
        }

        [TestMethod]
        public void ALoadedFutureDayIsBadgedFromItsFreeMinutes()
        {
            Assert.AreEqual(Level.Plenty, DayPlanDayBadge.For(true, true, () => 480));
            Assert.AreEqual(Level.Some, DayPlanDayBadge.For(true, true, () => 90));
            Assert.AreEqual(Level.Tight, DayPlanDayBadge.For(true, true, () => 30));
            Assert.AreEqual(Level.None, DayPlanDayBadge.For(true, true, () => -60));
        }

        [TestMethod]
        public void NoScheduleTodayAndPastDaysAreNeverBadged()
        {
            Assert.IsNull(DayPlanDayBadge.For(true, true, () => null));
            Assert.IsNull(DayPlanDayBadge.For(false, true, () => -60));
        }

        [TestMethod]
        public void MonthGridStartsOnTheFirstWeekday()
        {
            // September 2026 starts on a Tuesday.
            var monday = DayPlanMonthGrid.CellDates(new DateTime(2026, 9, 25), DayOfWeek.Monday);
            Assert.AreEqual(42, monday.Length);
            Assert.IsNull(monday[0]);
            Assert.AreEqual(new DateTime(2026, 9, 1), monday[1]);
            Assert.AreEqual(new DateTime(2026, 9, 30), monday[30]);
            Assert.IsNull(monday[31]);
            Assert.AreEqual(30, monday.Count(d => d.HasValue));

            var sunday = DayPlanMonthGrid.CellDates(new DateTime(2026, 9, 1), DayOfWeek.Sunday);
            Assert.AreEqual(new DateTime(2026, 9, 1), sunday[2]);
            // A month starting on the first weekday has no leading blank (February 2026 starts on a Sunday).
            Assert.AreEqual(0, DayPlanMonthGrid.LeadingBlanks(new DateTime(2026, 2, 1), DayOfWeek.Sunday));
            Assert.AreEqual(6, DayPlanMonthGrid.LeadingBlanks(new DateTime(2026, 2, 1), DayOfWeek.Monday));
            CollectionAssert.AreEqual(new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
                                              DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday },
                                      DayPlanMonthGrid.WeekdayOrder(DayOfWeek.Monday));
        }

        private static DateTime?[] Month(int year, int month) =>
            DayPlanMonthGrid.CellDates(new DateTime(year, month, 1), DayOfWeek.Monday);

        [TestMethod]
        public void TheCurrentMonthNeedsOnlyTheWindow()
        {
            var today = new DateTime(2026, 9, 25);
            var plan = DayPlanMonthLoad.For(Month(2026, 9), today, d => true, 45, 90, 31);
            Assert.AreEqual(45, plan.WindowDays, "never narrower than the Day Plan's own window");
            Assert.AreEqual(0, plan.SingleDays.Count);
        }

        [TestMethod]
        public void AMonthInsideTheWindowWidensItToCoverTheMonth()
        {
            var today = new DateTime(2026, 9, 25);
            var plan = DayPlanMonthLoad.For(Month(2026, 11), today, d => true, 45, 90, 31);
            Assert.AreEqual(67, plan.WindowDays, "30 Nov is today + 66");
            Assert.AreEqual(0, plan.SingleDays.Count);
        }

        [TestMethod]
        public void DaysPastTheWindowAreReadOneAtATimeAndCapped()
        {
            var today = new DateTime(2026, 9, 25);
            // today + 89 = 23 Dec: 1..23 Dec come from the window, 24..31 Dec one at a time.
            var plan = DayPlanMonthLoad.For(Month(2026, 12), today, d => true, 45, 90, 31);
            Assert.AreEqual(90, plan.WindowDays);
            CollectionAssert.AreEqual(Enumerable.Range(24, 8).Select(d => new DateTime(2026, 12, d)).ToList(), plan.SingleDays.ToList());

            var capped = DayPlanMonthLoad.For(Month(2027, 3), today, d => true, 45, 90, 10);
            Assert.IsNull(capped.WindowDays);
            Assert.AreEqual(10, capped.SingleDays.Count);
            Assert.AreEqual(new DateTime(2027, 3, 1), capped.SingleDays[0], "soonest first");
        }

        [TestMethod]
        public void PastMonthsAndLoadedDaysNeedNothing()
        {
            var today = new DateTime(2026, 9, 25);
            Assert.IsTrue(DayPlanMonthLoad.For(Month(2026, 8), today, d => true, 45, 90, 31).IsEmpty, "past days are never badged");
            Assert.IsTrue(DayPlanMonthLoad.For(Month(2026, 10), today, d => false, 45, 90, 31).IsEmpty, "already read");
            var partly = DayPlanMonthLoad.For(Month(2027, 1), today, d => d.Day > 20, 45, 90, 31);
            Assert.AreEqual(11, partly.SingleDays.Count);
        }
    }
}
