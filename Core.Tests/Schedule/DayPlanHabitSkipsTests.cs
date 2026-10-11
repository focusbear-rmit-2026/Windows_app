using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Schedule
{
    /// <summary>
    /// Ported from <c>ScheduleKitTests.swift</c>: habits skipped ahead of time (report
    /// 2026-09-13_20-13-58) and the routine honouring a skip made in advance (report
    /// 2026-09-16_15-14-58 — a habit skipped the night before was offered the next morning anyway).
    /// </summary>
    [TestClass]
    public class DayPlanHabitSkipsTests
    {
        /// <summary>A Monday, at a fixed offset, so "another day" arithmetic never lands on a DST seam.</summary>
        private static readonly DateTimeOffset TestDay =
            new DateTimeOffset(2026, 9, 21, 9, 0, 0, TimeSpan.FromHours(10));

        /// <summary>Scheduled for every day, so only a skip can keep a habit out of the routine.</summary>
        private static readonly string[] EveryDay = { "all" };

        [TestMethod]
        public void SkippingAHabitAheadAppliesToThatDayOnly()
        {
            DayPlanHabitSkips skips = new DayPlanHabitSkips();
            DateTimeOffset tomorrow = TestDay;
            DateTimeOffset anotherDay = tomorrow.AddDays(1);

            Assert.IsTrue(skips.Toggle("meditate", tomorrow));
            Assert.IsTrue(skips.IsSkipped("meditate", tomorrow));
            Assert.IsFalse(skips.IsSkipped("meditate", anotherDay),
                           "Skipping tomorrow leaves every other day alone");
            Assert.IsFalse(skips.IsSkipped("stretch", tomorrow));
        }

        [TestMethod]
        public void SkippingAheadTogglesBackOff()
        {
            DayPlanHabitSkips skips = new DayPlanHabitSkips();

            Assert.IsTrue(skips.Toggle("meditate", TestDay));
            Assert.IsFalse(skips.Toggle("meditate", TestDay), "Second press unskips");
            Assert.IsFalse(skips.IsSkipped("meditate", TestDay));
            Assert.AreEqual(0, skips.SkippedIds(TestDay).Count, "An emptied day leaves nothing behind");
            Assert.IsFalse(skips.SkippedIdsByDay.ContainsKey(DayPlanDayKey.For(TestDay)));
        }

        [TestMethod]
        public void SkipsAreNeverStoredForAnEmptyHabitId()
        {
            DayPlanHabitSkips skips = new DayPlanHabitSkips();

            Assert.IsFalse(skips.IsSkipped(string.Empty, TestDay));
            Assert.IsFalse(skips.IsSkipped(null, TestDay));
        }

        [TestMethod]
        public void OldSkipDaysArePrunedAwayInDateOrder()
        {
            Dictionary<string, List<string>> map = new Dictionary<string, List<string>>();
            for (int day = 1; day <= DayPlanHabitSkips.RetainedDays + 5; day++)
            {
                map[string.Format(CultureInfo.InvariantCulture, "2026-01-{0:00}", day)] = new List<string> { "h" };
            }

            DayPlanDayKey.PruneOldestDays(map, DayPlanHabitSkips.RetainedDays);

            Assert.AreEqual(DayPlanHabitSkips.RetainedDays, map.Count);
            Assert.IsFalse(map.ContainsKey("2026-01-01"), "The oldest days go first");
            Assert.IsTrue(map.ContainsKey(string.Format(CultureInfo.InvariantCulture, "2026-01-{0:00}",
                                                        DayPlanHabitSkips.RetainedDays + 5)));
        }

        [TestMethod]
        public void SkipsSurviveAReloadOfTheStore()
        {
            DayPlanHabitSkips skips = new DayPlanHabitSkips();
            skips.Toggle("read", TestDay);

            // A fresh store over the persisted map is what a relaunch sees.
            DayPlanHabitSkips reopened = new DayPlanHabitSkips(
                skips.SkippedIdsByDay.ToDictionary(day => day.Key, day => day.Value));

            Assert.IsTrue(reopened.IsSkipped("read", TestDay));
        }

        // MARK: - The routine honours a skip made in advance

        [TestMethod]
        public void AHabitSkippedForADayDoesNotPlayThatDay()
        {
            DayPlanHabitSkips skips = new DayPlanHabitSkips();

            Assert.IsTrue(RoutineHabitSchedule.ShouldPlay("read", EveryDay, TestDay, skips),
                          "Nothing is skipped yet, so the habit plays");
            skips.Toggle("read", TestDay);
            Assert.IsFalse(RoutineHabitSchedule.ShouldPlay("read", EveryDay, TestDay, skips),
                           "A habit skipped for this day must not be played by this day's routine");
        }

        [TestMethod]
        public void ASkipMadeTheNightBeforeAppliesToTheNextDayAndNotToToday()
        {
            DayPlanHabitSkips skips = new DayPlanHabitSkips();
            DateTimeOffset tonight = TestDay;
            DateTimeOffset tomorrow = tonight.AddDays(1);

            // What the Day Plan writes when you skip ahead while looking at tomorrow.
            skips.Toggle("read", tomorrow);

            Assert.IsFalse(RoutineHabitSchedule.ShouldPlay("read", EveryDay, tomorrow, skips),
                           "Tomorrow's routine must honour the skip recorded for tomorrow");
            Assert.IsTrue(RoutineHabitSchedule.ShouldPlay("read", EveryDay, tonight, skips),
                          "A skip for tomorrow must not leak into tonight's routine");
        }

        [TestMethod]
        public void SkippingOneHabitLeavesTheRestOfTheRoutineAlone()
        {
            DayPlanHabitSkips skips = new DayPlanHabitSkips();
            skips.Toggle("read", TestDay);

            Assert.IsTrue(RoutineHabitSchedule.ShouldPlay("shower", EveryDay, TestDay, skips));
        }

        [TestMethod]
        public void UnskippingPutsTheHabitBackIntoThatDaysRoutine()
        {
            DayPlanHabitSkips skips = new DayPlanHabitSkips();
            skips.Toggle("read", TestDay);
            skips.Toggle("read", TestDay);

            Assert.IsTrue(RoutineHabitSchedule.ShouldPlay("read", EveryDay, TestDay, skips));
        }

        [TestMethod]
        public void SkippingAheadDoesNotReplaceTheWeekdayRule()
        {
            DayPlanHabitSkips skips = new DayPlanHabitSkips();
            string otherWeekday = WeekdayCode.For(TestDay) == WeekdayCode.Saturday
                ? WeekdayCode.Sunday
                : WeekdayCode.Saturday;

            Assert.IsFalse(RoutineHabitSchedule.ShouldPlay("read", new[] { otherWeekday }, TestDay, skips),
                           "Skipping ahead is an extra rule, not a replacement for the weekday rule");
        }

        [TestMethod]
        public void WithNoSkipStoreAtAllTheWeekdayRuleStillDecides()
        {
            // The routine player asks this before any Day Plan state exists.
            Assert.IsTrue(RoutineHabitSchedule.ShouldPlay("read", EveryDay, TestDay));
            Assert.IsFalse(RoutineHabitSchedule.ShouldPlay("read", new[] { WeekdayCode.Sunday }, TestDay));
        }
    }
}
