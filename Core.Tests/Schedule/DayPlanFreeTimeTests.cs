using System;
using System.Collections.Generic;
using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Schedule
{
    /// <summary>
    /// Ported from <c>ScheduleKitTests.swift</c>: free time vs. time-boxed task load
    /// (report 2026-09-10_07-53-47), and the block-key vocabulary that decides which blocks absorb
    /// their tasks' time (report 2026-09-17_09-08-25).
    /// </summary>
    [TestClass]
    public class DayPlanFreeTimeTests
    {
        /// <summary>The user's own example: 20m of tasks on a 10m block costs the day 10 extra minutes,
        /// not 30.</summary>
        [TestMethod]
        public void ExcessIsTaskTimeBeyondTheBlock()
        {
            Assert.AreEqual(10, DayPlanFreeTime.ExcessMinutes(20, 10));
        }

        /// <summary>Tasks that fit inside the block are already paid for by the block itself.</summary>
        [TestMethod]
        public void TasksUnderOrEqualToTheBlockDurationCostNothing()
        {
            Assert.AreEqual(0, DayPlanFreeTime.ExcessMinutes(5, 10));
            Assert.AreEqual(0, DayPlanFreeTime.ExcessMinutes(10, 10));
        }

        /// <summary>An empty block never hands time back — free time must not go UP.</summary>
        [TestMethod]
        public void NoTasksLeavesFreeTimeUntouched()
        {
            Assert.AreEqual(0, DayPlanFreeTime.ExcessMinutes(0, 10));
            Assert.AreEqual(120, DayPlanFreeTime.AdjustedFreeMinutes(
                120, new[] { new DayPlanFreeTime.BlockLoad(10, 0) }));
        }

        /// <summary>A task with no linked block contributes no block load at all.</summary>
        [TestMethod]
        public void NoLinkedBlocksLeavesFreeTimeUntouched()
        {
            Assert.AreEqual(120, DayPlanFreeTime.AdjustedFreeMinutes(120, new List<DayPlanFreeTime.BlockLoad>()));
        }

        /// <summary>Every block's overflow adds up, and only the overflow.</summary>
        [TestMethod]
        public void AdjustedFreeTimeDeductsOnlyTheOverflow()
        {
            var blocks = new[]
            {
                new DayPlanFreeTime.BlockLoad(10, 20),   // 10 over
                new DayPlanFreeTime.BlockLoad(30, 15),   // fits — costs nothing
                new DayPlanFreeTime.BlockLoad(25, 40),   // 15 over
            };

            Assert.AreEqual(95, DayPlanFreeTime.AdjustedFreeMinutes(120, blocks));
        }

        /// <summary>An over-committed day reports no free time rather than a negative one.</summary>
        [TestMethod]
        public void AdjustedFreeTimeClampsAtZero()
        {
            Assert.AreEqual(0, DayPlanFreeTime.AdjustedFreeMinutes(
                10, new[] { new DayPlanFreeTime.BlockLoad(5, 90) }));
        }

        /// <summary>A zero-length (or nonsense) block cannot absorb anything, but must not amplify either.</summary>
        [TestMethod]
        public void ZeroLengthBlockChargesTheFullTaskTime()
        {
            Assert.AreEqual(20, DayPlanFreeTime.ExcessMinutes(20, 0));
            Assert.AreEqual(20, DayPlanFreeTime.ExcessMinutes(20, -5));
        }
    }

    /// <summary>
    /// The block-key vocabulary. Two of the three places that asked "is this a habit key?" tested for
    /// the prefix inline and nobody noticed a third kind of key existed, so the capacity model absorbed
    /// task work into habits but not into calendar events (report 2026-09-17_09-08-25).
    /// </summary>
    [TestClass]
    public class DayPlanBlockKeyTests
    {
        [TestMethod]
        public void HabitsAreKeyedByNameAndEventsById()
        {
            Assert.AreEqual("habit:📞 Cold calls", DayPlanBlockKey.Habit("📞 Cold calls"));
            Assert.AreEqual("event:ABC:DEF", DayPlanBlockKey.Event("ABC:DEF"),
                            "a calendar id containing a colon survives");
        }

        [TestMethod]
        public void ADeepWorkKeyIsTheStartTimeItWasPinnedAt()
        {
            DateTimeOffset start = DateTimeOffset.FromUnixTimeSeconds(1_789_000_000);
            Assert.AreEqual("deepwork:1789000000", DayPlanBlockKey.DeepWork(start));
        }

        [TestMethod]
        public void OnlyCommittedTimeAbsorbsItsTasks()
        {
            Assert.IsTrue(DayPlanBlockKey.Absorbs(DayPlanBlockKey.Habit("Run")));
            Assert.IsTrue(DayPlanBlockKey.Absorbs(DayPlanBlockKey.Event("evt")),
                          "dragging a task onto an event must cost the day what dragging it onto a habit does");
            Assert.IsFalse(DayPlanBlockKey.Absorbs(DayPlanBlockKey.DeepWork(DateTimeOffset.Now)),
                           "deep work IS the day's free work time — absorbing into it would refund the very "
                           + "minutes it represents");
            Assert.IsFalse(DayPlanBlockKey.Absorbs(null), "an unassigned task is pinned to nothing");
            Assert.IsFalse(DayPlanBlockKey.Absorbs(string.Empty));
        }

        [TestMethod]
        public void APlannedTaskCarriesTheKeyOfTheBlockItWasDroppedOn()
        {
            DayPlanTask onAHabit = new DayPlanTask("Review ISO docs", durationMinutes: 15,
                                                   blockKey: DayPlanBlockKey.Habit("📞 Cold calls"));

            Assert.IsTrue(DayPlanBlockKey.Absorbs(onAHabit.BlockKey),
                          "its 15 minutes are already inside the habit's block");
            Assert.IsFalse(DayPlanBlockKey.Absorbs(new DayPlanTask("Unplanned").BlockKey));
        }
    }
}
