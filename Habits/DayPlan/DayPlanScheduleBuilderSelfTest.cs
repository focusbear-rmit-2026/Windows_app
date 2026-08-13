using System;
using System.Collections.Generic;
using System.Linq;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// A lightweight, project-embedded port of the macOS ScheduleKitTests
    /// (Mac-App: FocusBearTests/ScheduleKitTests.swift). The Windows solution has no XCTest-style
    /// test target, so these run in-process and report pass/fail — used to verify the engine
    /// matches the macOS placement rules during Milestone 2 before the UI is built on top.
    ///
    /// This is scaffolding for verification; the UI does not depend on it and it can be removed
    /// (or moved to a real test project) once the engine is trusted.
    /// </summary>
    public static class DayPlanScheduleBuilderSelfTest
    {
        public class Result
        {
            public string Name;
            public bool Passed;
            public string Detail;
        }

        // A DateTime at h:m today (the engine packs habits from `now`, all same-day here).
        private static DateTime T(int h, int m = 0)
        {
            DateTime now = DateTime.Now;
            return new DateTime(now.Year, now.Month, now.Day, h, m, 0);
        }

        private static ScheduleHabitInput Habit(int idx, string title, int minutes,
                                                DateTime? cutoff = null, bool playing = false, int elapsed = 0)
        {
            return new ScheduleHabitInput(idx, title, minutes * 60, playing, elapsed, cutoff);
        }

        private static ScheduleRoutineSegment Seg(List<ScheduleHabitInput> habits, DateTime start, DateTime end,
                                                  bool actionable = true, string name = "Morning")
        {
            return new ScheduleRoutineSegment(name, habits, start, end, actionable);
        }

        private static List<ScheduleBlock> Build(List<ScheduleRoutineSegment> segs, DateTime now, DateTime dayEnd,
                                                 List<ScheduleMeetingInput> meetings = null, int buffer = 0)
        {
            return DayPlanScheduleBuilder.Build(segs, meetings ?? new List<ScheduleMeetingInput>(), now, dayEnd,
                                                buffer, outOfOrderAllowed: false);
        }

        private static List<ScheduleBlock> HabitBlocks(List<ScheduleBlock> blocks)
        {
            return blocks.Where(b => b.Kind == ScheduleBlockKind.Habit).OrderBy(b => b.Start).ToList();
        }

        public static List<Result> Run()
        {
            var results = new List<Result>();

            void Check(string name, Func<string> test)
            {
                try
                {
                    string fail = test();
                    results.Add(new Result { Name = name, Passed = fail == null, Detail = fail ?? "OK" });
                }
                catch (Exception ex)
                {
                    results.Add(new Result { Name = name, Passed = false, Detail = "threw: " + ex.Message });
                }
            }

            // 1. Habits packed in order, back-to-back.
            Check("HabitsPackedInOrderBackToBack", () =>
            {
                DateTime now = T(9, 0);
                var segs = new List<ScheduleRoutineSegment> { Seg(new List<ScheduleHabitInput>
                    { Habit(0, "A", 10), Habit(1, "B", 15), Habit(2, "C", 5) }, now, T(12, 0)) };
                var blocks = HabitBlocks(Build(segs, now, T(18, 0)));
                var titles = blocks.Select(b => b.Title).ToList();
                if (!titles.SequenceEqual(new[] { "A", "B", "C" })) return "order was [" + string.Join(",", titles) + "]";
                if (blocks[0].Start != now) return "A did not start at now";
                if (blocks[1].Start != blocks[0].End) return "B not back-to-back with A";
                if (blocks[2].Start != blocks[1].End) return "C not back-to-back with B";
                return null;
            });

            // 2. An ACTIVE routine keeps every habit in list order even when the later ones don't fit the
            // window — the ones that overflow are flagged (not dropped) so the routine stays startable
            // (#1371). Order is still preserved (no leap-frogging to a shorter later habit).
            Check("ActiveRoutineKeepsOverflowHabitsFlaggedInOrder", () =>
            {
                DateTime now = T(9, 0);
                var segs = new List<ScheduleRoutineSegment> { Seg(new List<ScheduleHabitInput>
                    { Habit(0, "A", 10), Habit(1, "B", 30), Habit(2, "C", 5) }, now, T(9, 20)) };
                var blocks = HabitBlocks(Build(segs, now, T(18, 0)));
                var titles = blocks.Select(b => b.Title).ToList();
                if (!titles.SequenceEqual(new[] { "A", "B", "C" })) return "expected [A,B,C], got [" + string.Join(",", titles) + "]";
                if (blocks[0].DidOverflow) return "A fit, should not overflow";
                if (!blocks[1].DidOverflow || !blocks[2].DidOverflow) return "B/C should be flagged overflow";
                return null;
            });

            // 2b. A fully-booked day (a meeting covering the whole window) still lists the active routine's
            // habit — flagged overflow and marked current — so the user is never locked out (#1371).
            Check("FullyBookedDayStillListsActiveHabit", () =>
            {
                DateTime now = T(9, 0);
                var meetings = new List<ScheduleMeetingInput> { new ScheduleMeetingInput("m1", "All day", T(9, 0), T(18, 0)) };
                var segs = new List<ScheduleRoutineSegment> { Seg(new List<ScheduleHabitInput> { Habit(0, "A", 15) }, now, T(17, 0), actionable: true) };
                var blocks = HabitBlocks(Build(segs, now, T(18, 0), meetings));
                if (blocks.Count != 1) return "expected the habit to still be listed, got " + blocks.Count;
                if (!blocks[0].DidOverflow) return "habit should be flagged overflow";
                if (!blocks[0].IsCurrent || !blocks[0].IsActionable) return "habit should be current+actionable (startable)";
                return null;
            });

            // 3. Habit pushed past an overlapping meeting.
            Check("HabitPushedPastOverlappingMeeting", () =>
            {
                DateTime now = T(9, 0);
                var meetings = new List<ScheduleMeetingInput> { new ScheduleMeetingInput("m1", "Standup", T(9, 5), T(9, 35)) };
                var segs = new List<ScheduleRoutineSegment> { Seg(new List<ScheduleHabitInput> { Habit(0, "A", 20) }, now, T(12, 0)) };
                var blocks = HabitBlocks(Build(segs, now, T(18, 0), meetings));
                if (blocks.Count != 1) return "expected 1 habit, got " + blocks.Count;
                return blocks[0].Start >= T(9, 35) ? null : "A started before meeting end (" + blocks[0].Start.ToString("HH:mm") + ")";
            });

            // 4. Meeting appears as its own block.
            Check("MeetingAppearsAsBlock", () =>
            {
                DateTime now = T(9, 0);
                var meetings = new List<ScheduleMeetingInput> { new ScheduleMeetingInput("m1", "Standup", T(10, 0), T(10, 30)) };
                var segs = new List<ScheduleRoutineSegment> { Seg(new List<ScheduleHabitInput> { Habit(0, "A", 10) }, now, T(12, 0)) };
                var blocks = Build(segs, now, T(18, 0), meetings);
                return blocks.Any(b => b.Kind == ScheduleBlockKind.Meeting && b.EventID == "m1") ? null : "meeting block missing";
            });

            // 5. Habits never spill past the window end.
            Check("HabitsNeverSpillPastWindowEnd", () =>
            {
                DateTime now = T(9, 0);
                var segs = new List<ScheduleRoutineSegment> { Seg(new List<ScheduleHabitInput> { Habit(0, "A", 30) }, now, T(9, 45)) };
                var blocks = HabitBlocks(Build(segs, now, T(18, 0)));
                return blocks.All(b => b.End <= T(9, 45)) ? null : "a habit ended past 9:45";
            });

            // 6. Past-cutoff habit dropped, but later habit still shown (continue, not break).
            Check("PastCutoffHabitDroppedButLaterShown", () =>
            {
                DateTime now = T(9, 0);
                var segs = new List<ScheduleRoutineSegment> { Seg(new List<ScheduleHabitInput>
                    { Habit(0, "A", 10, cutoff: T(8, 0)), Habit(1, "B", 10) }, now, T(12, 0)) };
                var titles = HabitBlocks(Build(segs, now, T(18, 0))).Select(b => b.Title).ToList();
                return titles.SequenceEqual(new[] { "B" }) ? null : "expected [B], got [" + string.Join(",", titles) + "]";
            });

            // 7. Only the next habit is "current" in an actionable segment.
            Check("OnlyNextHabitIsCurrentInActionableSegment", () =>
            {
                DateTime now = T(9, 0);
                var segs = new List<ScheduleRoutineSegment> { Seg(new List<ScheduleHabitInput>
                    { Habit(0, "A", 10), Habit(1, "B", 10) }, now, T(12, 0), actionable: true) };
                var blocks = HabitBlocks(Build(segs, now, T(18, 0)));
                if (blocks.Count(b => b.IsCurrent) != 1) return "expected exactly 1 current";
                if (!blocks.First().IsCurrent) return "earliest habit is not the current one";
                if (!blocks.All(b => b.IsActionable)) return "not all habits actionable";
                return null;
            });

            // 8. Preview (non-actionable) segment yields no actionable / current habits.
            Check("PreviewSegmentNotActionable", () =>
            {
                DateTime now = T(9, 0);
                var segs = new List<ScheduleRoutineSegment> { Seg(new List<ScheduleHabitInput> { Habit(0, "A", 10) }, now, T(12, 0), actionable: false) };
                var blocks = HabitBlocks(Build(segs, now, T(18, 0)));
                return blocks.All(b => !b.IsActionable && !b.IsCurrent) ? null : "preview habit was actionable/current";
            });

            return results;
        }
    }
}
