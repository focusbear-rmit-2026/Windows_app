using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Schedule
{
    /// <summary>
    /// Report 2026-09-25_09-01-38: "it should interleave my morning routine habits throughout my day
    /// plan. I have gaps between meetings where I can do parts of morning routine. Use same logic as
    /// mac". The Windows Day Plan now places habits with <see cref="ScheduleBuilder.PlaceHabitsInGaps"/>;
    /// these pin the behaviour on a day shaped like the one in the report.
    /// </summary>
    [TestClass]
    public class ScheduleBuilderInterleaveTests
    {
        private static readonly TimeSpan Offset = TimeSpan.FromHours(10);

        private static DateTimeOffset T(int hour, int minute = 0)
        {
            return new DateTimeOffset(2026, 9, 25, hour, minute, 0, Offset);
        }

        private static ScheduleHabitInput Habit(int index, string title, int minutes)
        {
            return new ScheduleHabitInput(index, title, minutes * 60, id: "h" + index);
        }

        private static ScheduleMeetingInput Meeting(string title, DateTimeOffset start, DateTimeOffset end)
        {
            return new ScheduleMeetingInput(title, title, start, end);
        }

        /// <summary>A packed day: short gaps between meetings from 10am, a free stretch late afternoon.</summary>
        private static List<ScheduleMeetingInput> PackedDay()
        {
            return new List<ScheduleMeetingInput>
            {
                Meeting("Time tracker", T(9, 20), T(10, 20)),
                Meeting("Focus and chill", T(11, 0), T(12, 0)),
                Meeting("Standup", T(12, 0), T(12, 15)),
                Meeting("Leadership", T(12, 30), T(13, 0)),
                Meeting("UX", T(13, 0), T(13, 20)),
                Meeting("Lunch", T(13, 20), T(13, 50)),
                Meeting("Afternoon block", T(14, 0), T(16, 0)),
            };
        }

        private static List<ScheduleHabitInput> Routine()
        {
            return new List<ScheduleHabitInput>
            {
                Habit(0, "Record energy", 1),
                Habit(1, "Yoga", 15),
                Habit(2, "Sinus rinse", 3),
                Habit(3, "Deep journaling", 40),
                Habit(4, "Stretch", 10),
                Habit(5, "Meditate", 20),
            };
        }

        private static IReadOnlyList<ScheduleBlock> Build(DateTimeOffset now, DateTimeOffset windowEnd)
        {
            var segs = new[] { new ScheduleRoutineSegment("Morning", Routine(), now, windowEnd, true) };
            return ScheduleBuilder.Build(segs, PackedDay(), now, T(23, 59), 0);
        }

        [TestMethod]
        public void HabitsFillTheGapsBetweenMeetings()
        {
            IReadOnlyList<ScheduleBlock> blocks = Build(T(9, 4), T(21));
            List<ScheduleBlock> habits = blocks.Where(b => b.Kind == ScheduleBlockKind.Habit).ToList();

            Assert.IsTrue(habits.All(h => !h.DidOverflow), "every habit fits somewhere before the evening");
            foreach (ScheduleBlock h in habits)
            {
                foreach (ScheduleMeetingInput m in PackedDay())
                {
                    Assert.IsFalse(h.Start < m.End && h.End > m.Start,
                                   h.Title + " overlaps " + m.Title);
                }
            }
            // The 16-minute gap before the first meeting takes the first habits in order.
            Assert.AreEqual(T(9, 4), habits.Single(h => h.Title == "Record energy").Start);
            Assert.AreEqual(T(9, 5), habits.Single(h => h.Title == "Yoga").Start);
            // Sinus rinse no longer fits before 9:20 and goes into the next gap.
            Assert.AreEqual(T(10, 20), habits.Single(h => h.Title == "Sinus rinse").Start);
            // The long habit waits for a gap that holds it (10:20–11:00 after the rinse would be 40 min).
            Assert.IsTrue(habits.Single(h => h.Title == "Deep journaling").Start >= T(10, 20));
        }

        [TestMethod]
        public void RoutineIsSplitIntoRunsSeparatedByMeetings()
        {
            IReadOnlyList<ScheduleBlock> blocks = Build(T(9, 4), T(21));
            // Count the runs of consecutive habit blocks: more than one means the routine is interleaved
            // with the day rather than stacked in one lump.
            int runs = 0;
            bool inRun = false;
            foreach (ScheduleBlock b in blocks)
            {
                bool isHabit = b.Kind == ScheduleBlockKind.Habit;
                if (isHabit && !inRun) runs++;
                inRun = isHabit;
            }
            Assert.IsTrue(runs >= 2, "expected the routine to be spread across gaps, got " + runs + " run(s)");
        }

        [TestMethod]
        public void OnlyTheHabitThatFitsNowhereOverflows()
        {
            // The evening starts at 14:00, so only the gaps up to then count; the 40-minute habit fits
            // in 10:20–11:00 but the 20-minute one then has nowhere left.
            IReadOnlyList<ScheduleBlock> blocks = Build(T(9, 4), T(14));
            List<ScheduleBlock> overflow = blocks.Where(b => b.Kind == ScheduleBlockKind.Habit && b.DidOverflow).ToList();
            List<ScheduleBlock> placed = blocks.Where(b => b.Kind == ScheduleBlockKind.Habit && !b.DidOverflow).ToList();

            Assert.IsTrue(placed.Count >= 4, "most of the routine still gets real slots");
            Assert.IsTrue(overflow.Count >= 1, "something is reported as not fitting");
            Assert.IsTrue(overflow.All(o => o.Start == o.End), "overflow habits are zero-length anchors");
            Assert.IsTrue(overflow.All(o => o.OverflowDurationSeconds.HasValue && o.OverflowDurationSeconds > 0));
        }
    }
}
