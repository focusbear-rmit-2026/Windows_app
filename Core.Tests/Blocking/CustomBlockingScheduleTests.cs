using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Blocking
{
    /// <summary>
    /// Ported from the Mac app's <c>CustomBlockingScheduleTests.swift</c>.
    ///
    /// The Swift anchors its clock-relative cases to "today at 12:00" so an offset can never roll
    /// across midnight at run time and flip a same-day window into the cross-midnight branch. Here the
    /// reference is a FIXED date instead, which gives the same protection and also fixes the weekday,
    /// so the cases read the same on every day of the week.
    /// </summary>
    [TestClass]
    public class CustomBlockingScheduleTests
    {
        /// <summary>Wednesday 16 September 2026, midday.</summary>
        private static readonly DateTime FixedNow = new DateTime(2026, 9, 16, 12, 0, 0);

        private static readonly int[] EveryDay = { 1, 2, 3, 4, 5, 6, 7 };

        private static CustomBlockingSchedule MakeSchedule(int startOffsetSeconds, int endOffsetSeconds,
                                                           IReadOnlyList<int> daysOfWeek = null)
        {
            return new CustomBlockingSchedule
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Test Schedule",
                StartTime = FixedNow.AddSeconds(startOffsetSeconds),
                EndTime = FixedNow.AddSeconds(endOffsetSeconds),
                DaysOfWeek = daysOfWeek ?? new[] { CalendarWeekday.For(FixedNow) },
            };
        }

        /// <summary>Absolute hour/minute on the reference date; a window that ends earlier runs to tomorrow.</summary>
        private static CustomBlockingSchedule MakeScheduleAbsolute(int startHour, int startMinute,
                                                                   int endHour, int endMinute,
                                                                   IReadOnlyList<int> daysOfWeek)
        {
            DateTime start = FixedNow.Date.AddHours(startHour).AddMinutes(startMinute);
            DateTime end = FixedNow.Date.AddHours(endHour).AddMinutes(endMinute);
            if (end <= start) end = end.AddDays(1);
            return new CustomBlockingSchedule
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Test Schedule",
                StartTime = start,
                EndTime = end,
                DaysOfWeek = daysOfWeek,
            };
        }

        private static DateTime At(int hour, int minute = 0)
        {
            return FixedNow.Date.AddHours(hour).AddMinutes(minute);
        }

        // MARK: - IsBlockingActive, same day

        [TestMethod]
        public void ActiveWhenNowIsInsideTheWindow()
        {
            Assert.IsTrue(MakeSchedule(-1800, 1800).IsBlockingActive(FixedNow));
        }

        [TestMethod]
        public void InactiveBeforeTheWindowStartsAndAfterItEnds()
        {
            Assert.IsFalse(MakeSchedule(1800, 7200).IsBlockingActive(FixedNow));
            Assert.IsFalse(MakeSchedule(-7200, -1800).IsBlockingActive(FixedNow));
        }

        [TestMethod]
        public void TheStartBoundaryIsInclusive()
        {
            Assert.IsTrue(MakeSchedule(0, 3600).IsBlockingActive(FixedNow));
        }

        [TestMethod]
        public void InactiveWhenTodayIsNotOneOfTheScheduledDays()
        {
            int today = CalendarWeekday.For(FixedNow);
            int[] otherDays = EveryDay.Where(day => day != today).ToArray();
            Assert.IsFalse(MakeSchedule(-1800, 1800, otherDays).IsBlockingActive(FixedNow));
        }

        [TestMethod]
        public void InactiveWhenTheScheduleHasNoDaysAtAll()
        {
            Assert.IsFalse(MakeSchedule(-1800, 1800, new int[0]).IsBlockingActive(FixedNow));
        }

        [TestMethod]
        public void EveryDayStillRespectsTheWindow()
        {
            Assert.IsTrue(MakeSchedule(-3600, 3600, EveryDay).IsBlockingActive(FixedNow));
            Assert.IsFalse(MakeSchedule(-7200, -3600, EveryDay).IsBlockingActive(FixedNow));
        }

        // MARK: - IsBlockingActive, across midnight (a 22:00–02:00 window)

        [TestMethod]
        public void ActiveOnTheEveningSideOfACrossMidnightWindow()
        {
            DateTime now = At(23);
            CustomBlockingSchedule schedule = MakeScheduleAbsolute(22, 0, 2, 0, new[] { CalendarWeekday.For(now) });
            Assert.IsTrue(schedule.IsBlockingActive(now));
        }

        [TestMethod]
        public void ActiveOnTheMorningSideBecauseTheWindowBelongsToTheDayItStartedOn()
        {
            // Now is 01:00, so the day that owns the schedule is YESTERDAY.
            DateTime now = At(1);
            int yesterdayWeekday = CalendarWeekday.For(now.AddDays(-1));
            CustomBlockingSchedule schedule = MakeScheduleAbsolute(22, 0, 2, 0, new[] { yesterdayWeekday });
            Assert.IsTrue(schedule.IsBlockingActive(now));
        }

        [TestMethod]
        public void InactiveAfterACrossMidnightWindowHasEnded()
        {
            // Now is 03:00, past the end. The scheduled day is deliberately neither today nor
            // yesterday: were it today, 03:00 >= 22:00 would (by design) read as an evening window
            // that has just begun, which is a different case.
            DateTime now = At(3);
            int today = CalendarWeekday.For(now);
            int yesterday = CalendarWeekday.For(now.AddDays(-1));
            int unrelated = EveryDay.First(day => day != today && day != yesterday);
            Assert.IsFalse(MakeScheduleAbsolute(22, 0, 2, 0, new[] { unrelated }).IsBlockingActive(now));
        }

        // MARK: - RemainingBlockingDuration

        [TestMethod]
        public void RemainingIsTheTimeLeftInTheWindow()
        {
            Assert.AreEqual(1800, MakeSchedule(-1800, 1800).RemainingBlockingDuration(FixedNow).TotalSeconds, 1);
            Assert.AreEqual(600, MakeSchedule(-600, 600).RemainingBlockingDuration(FixedNow).TotalSeconds, 1);
            Assert.AreEqual(25200, MakeSchedule(-3600, 25200).RemainingBlockingDuration(FixedNow).TotalSeconds, 1);
            Assert.AreEqual(1800, MakeSchedule(0, 1800).RemainingBlockingDuration(FixedNow).TotalSeconds, 1);
        }

        [TestMethod]
        public void RemainingIsTheOneSecondSentinelWhenNothingIsRunning()
        {
            Assert.AreEqual(CustomBlockingSchedule.NotRunningRemaining,
                MakeSchedule(-7200, -3600).RemainingBlockingDuration(FixedNow));
        }

        [TestMethod]
        public void RemainingDoesNotConsultTheDaysOfWeek()
        {
            // The Mac's does not either, so a schedule with NO days still reports a remaining time
            // while the clock is inside its window. Whether that should short-circuit is a product
            // decision; pinned here so a port never silently makes it one.
            CustomBlockingSchedule schedule = MakeSchedule(-1800, 1800, new int[0]);
            Assert.IsFalse(schedule.IsBlockingActive(FixedNow), "it is definitely not active");
            Assert.AreEqual(1800, schedule.RemainingBlockingDuration(FixedNow).TotalSeconds, 1,
                "yet it still reports the window's remainder");
        }

        [TestMethod]
        public void RemainingOnTheMorningSideCountsDownYesterdaysWindow()
        {
            DateTime now = At(1);
            CustomBlockingSchedule schedule = MakeScheduleAbsolute(22, 0, 2, 0, new[] { CalendarWeekday.For(now.AddDays(-1)) });
            Assert.AreEqual(3600, schedule.RemainingBlockingDuration(now).TotalSeconds, 1);
        }

        // MARK: - Overlap

        [TestMethod]
        public void OverlappingSchedulesOnASharedDayAreDetected()
        {
            int today = CalendarWeekday.For(FixedNow);
            CustomBlockingSchedule existing = MakeScheduleAbsolute(9, 0, 12, 0, new[] { today });
            CustomBlockingSchedule overlapping = MakeScheduleAbsolute(10, 0, 13, 0, new[] { today });

            Assert.IsTrue(BlockingScheduleOverlap.IsBlockingScheduleOverlapping(overlapping, new[] { existing }));
        }

        [TestMethod]
        public void AdjacentSchedulesDoNotOverlap()
        {
            int today = CalendarWeekday.For(FixedNow);
            CustomBlockingSchedule existing = MakeScheduleAbsolute(9, 0, 12, 0, new[] { today });
            CustomBlockingSchedule adjacent = MakeScheduleAbsolute(12, 0, 15, 0, new[] { today });

            Assert.IsFalse(BlockingScheduleOverlap.IsBlockingScheduleOverlapping(adjacent, new[] { existing }),
                "starting exactly when the other ends is not an overlap");
        }

        [TestMethod]
        public void TheSameHoursOnDifferentDaysDoNotOverlap()
        {
            int today = CalendarWeekday.For(FixedNow);
            int otherDay = today == CalendarWeekday.Saturday ? CalendarWeekday.Sunday : today + 1;
            CustomBlockingSchedule existing = MakeScheduleAbsolute(9, 0, 12, 0, new[] { today });
            CustomBlockingSchedule differentDay = MakeScheduleAbsolute(9, 0, 12, 0, new[] { otherDay });

            Assert.IsFalse(BlockingScheduleOverlap.IsBlockingScheduleOverlapping(differentDay, new[] { existing }));
        }

        [TestMethod]
        public void AScheduleDoesNotOverlapItselfWhenExcludedById()
        {
            CustomBlockingSchedule schedule = MakeScheduleAbsolute(9, 0, 12, 0, new[] { CalendarWeekday.For(FixedNow) });

            Assert.IsTrue(BlockingScheduleOverlap.IsBlockingScheduleOverlapping(schedule, new[] { schedule }),
                "without the exclusion, editing a schedule collides with its own saved copy");
            Assert.IsFalse(BlockingScheduleOverlap.IsBlockingScheduleOverlapping(
                schedule, new[] { schedule }, excludingId: schedule.Id));
        }
    }
}
