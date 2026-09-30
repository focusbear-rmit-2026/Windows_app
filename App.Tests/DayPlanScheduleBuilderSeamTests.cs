using System;
using System.Collections.Generic;
using System.Linq;
using Focus_Bear.Habits.DayPlan;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The Day Plan's schedule engine (the app's DayPlanScheduleBuilder, over Core's gap placement). Its port of
    /// the Mac's ScheduleKitTests only ran when someone called it by hand; these run it, plus the edges the
    /// Day Plan hits every day: a meeting already under way, overlapping meetings, a finished meeting.
    /// </summary>
    [TestClass]
    public class DayPlanScheduleBuilderSeamTests
    {
        private static readonly DateTime D = new DateTime(2026, 9, 28);
        private static DateTime T(int h, int m = 0) => D.AddHours(h).AddMinutes(m);

        private static ScheduleHabitInput Habit(int i, int minutes) => new ScheduleHabitInput(i, "H" + i, minutes * 60, id: "h" + i);

        private static List<ScheduleBlock> Build(DateTime now, List<ScheduleHabitInput> habits, params ScheduleMeetingInput[] meetings) =>
            DayPlanScheduleBuilder.Build(new List<ScheduleRoutineSegment> { new ScheduleRoutineSegment("Morning", habits, T(6), T(17), true) },
                meetings.ToList(), now, T(23, 59), 0, false, eveningRoutineStart: T(17), deepWorkCutoff: T(17));

        private static List<ScheduleBlock> Habits(List<ScheduleBlock> b) => b.Where(x => x.Kind == ScheduleBlockKind.Habit).OrderBy(x => x.Start).ToList();

        [TestMethod]
        public void ThePortedMacScheduleKitChecksAllPass()
        {
            var failed = DayPlanScheduleBuilderSelfTest.Run().Where(r => !r.Passed).Select(r => r.Name + ": " + r.Detail).ToList();
            Assert.AreEqual(0, failed.Count, string.Join("\n", failed));
        }

        [TestMethod]
        public void AMeetingAlreadyUnderWayHoldsTheHabitsUntilItEnds()
        {
            var blocks = Build(T(9, 10), new List<ScheduleHabitInput> { Habit(0, 10) }, new ScheduleMeetingInput("m", "Standup", T(9), T(9, 30)));

            Assert.AreEqual(T(9, 30), Habits(blocks)[0].Start);
            Assert.IsTrue(blocks.Any(b => b.Kind == ScheduleBlockKind.Meeting && b.EventID == "m"), "it still shows");
        }

        [TestMethod]
        public void OverlappingMeetingsAreOneBusyStretch()
        {
            var blocks = Build(T(9), new List<ScheduleHabitInput> { Habit(0, 15) },
                new ScheduleMeetingInput("a", "A", T(9), T(10)), new ScheduleMeetingInput("b", "B", T(9, 30), T(11)));

            Assert.AreEqual(T(11), Habits(blocks)[0].Start);
            Assert.IsFalse(blocks.Any(b => b.Kind == ScheduleBlockKind.DeepWork && b.Start < T(11)), "no deep work inside the meetings");
        }

        [TestMethod]
        public void AFinishedMeetingDropsOffTodayUnlessEarlierEventsAreShown()
        {
            var meeting = new ScheduleMeetingInput("m", "Done", T(7), T(8));
            Assert.IsFalse(Build(T(9), new List<ScheduleHabitInput>(), meeting).Any(b => b.Kind == ScheduleBlockKind.Meeting));

            var shown = DayPlanScheduleBuilder.Build(new List<ScheduleRoutineSegment>(), new List<ScheduleMeetingInput> { meeting }, T(9), T(23, 59), 0, false, meetingsFrom: D);
            Assert.IsTrue(shown.Any(b => b.Kind == ScheduleBlockKind.Meeting));
        }

        [TestMethod]
        public void HabitsFillTheGapsBetweenMeetingsInOrderAndOnlyTheActiveRoutineIsCurrent()
        {
            var blocks = Build(T(9), new List<ScheduleHabitInput> { Habit(0, 10), Habit(1, 30), Habit(2, 5) },
                new ScheduleMeetingInput("m", "M", T(9, 20), T(10)));
            var habits = Habits(blocks);

            Assert.AreEqual(T(9), habits[0].Start);
            Assert.AreEqual("H2", habits[1].Title, "the 5-minute habit fits the 10 minutes left before the meeting");
            Assert.AreEqual(T(10), habits.Single(h => h.Title == "H1").Start);
            Assert.AreEqual(1, habits.Count(h => h.IsCurrent));
            Assert.IsTrue(habits[0].IsCurrent);
        }
    }
}
