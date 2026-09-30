using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Schedule
{
    /// <summary>
    /// Ported from the Mac app's <c>ScheduleKitTests.swift</c>. These lock in the placement rules that
    /// have been bug-prone: in-order packing, filling the gap before a meeting with a shorter later
    /// habit, capping at the routine window, surfacing (rather than dropping) what does not fit, and
    /// dropping past-cutoff habits.
    ///
    /// The Swift tests anchor on <c>Date()</c>; these anchor on a fixed day at a fixed offset instead,
    /// so a run at 23:59:59 cannot straddle midnight and a machine's time zone cannot change an answer.
    /// </summary>
    [TestClass]
    public class ScheduleBuilderTests
    {
        private static readonly TimeSpan Offset = TimeSpan.FromHours(10);
        private const int TestYear = 2026, TestMonth = 9, TestDay = 21;

        /// <summary>A time at h:m on the test day — the engine packs habits from `now`, all same-day here.</summary>
        private static DateTimeOffset T(int hour, int minute = 0)
        {
            return Day(0, hour, minute);
        }

        /// <summary>A time at h:m, <paramref name="dayOffset"/> days on — for the future-day plans.</summary>
        private static DateTimeOffset Day(int dayOffset, int hour, int minute = 0)
        {
            return new DateTimeOffset(TestYear, TestMonth, TestDay, hour, minute, 0, Offset).AddDays(dayOffset);
        }

        private static ScheduleHabitInput Habit(int index, string title, int minutes,
                                                DateTimeOffset? cutoff = null, bool playing = false,
                                                int elapsedSeconds = 0, string id = null)
        {
            return new ScheduleHabitInput(index, title, minutes * 60, playing, elapsedSeconds, cutoff, false, id);
        }

        private static ScheduleRoutineSegment Seg(IEnumerable<ScheduleHabitInput> habits, DateTimeOffset start,
                                                  DateTimeOffset end, bool actionable = true,
                                                  string name = "Morning", bool isEarlier = false)
        {
            return new ScheduleRoutineSegment(name, habits.ToList(), start, end, actionable, isEarlier);
        }

        private static IReadOnlyList<ScheduleBlock> Build(IEnumerable<ScheduleRoutineSegment> segments,
                                                          DateTimeOffset now, DateTimeOffset dayEnd,
                                                          IEnumerable<ScheduleMeetingInput> meetings = null,
                                                          int bufferSeconds = 0)
        {
            return ScheduleBuilder.Build(segments, meetings ?? new List<ScheduleMeetingInput>(), now, dayEnd,
                                         bufferSeconds);
        }

        private static List<ScheduleBlock> HabitBlocks(IEnumerable<ScheduleBlock> blocks)
        {
            return blocks.Where(b => b.Kind == ScheduleBlockKind.Habit).OrderBy(b => b.Start).ToList();
        }

        private static List<string> Titles(IEnumerable<ScheduleBlock> blocks)
        {
            return blocks.Select(b => b.Title).ToList();
        }

        // MARK: - Placement order

        [TestMethod]
        public void HabitsPackedInOrderBackToBack()
        {
            DateTimeOffset now = T(9);
            var segments = new[] { Seg(new[] { Habit(0, "A", 10), Habit(1, "B", 15), Habit(2, "C", 5) }, now, T(12)) };

            List<ScheduleBlock> blocks = HabitBlocks(Build(segments, now, T(18)));

            CollectionAssert.AreEqual(new List<string> { "A", "B", "C" }, Titles(blocks));
            Assert.AreEqual(now, blocks[0].Start);
            Assert.AreEqual(blocks[0].End, blocks[1].Start, "back-to-back");
            Assert.AreEqual(blocks[1].End, blocks[2].Start);
        }

        // MARK: - Gap filling: a shorter later habit fills what is left of a gap

        [TestMethod]
        public void ShorterLaterHabitFillsRemainingWindow()
        {
            DateTimeOffset now = T(9);
            // Window 9:00–9:20. A=10m fits; B=30m fits nowhere; C=5m fits in the 10 min left, so it is
            // pulled forward rather than leaving that time as deep work. B is the only overflow.
            var segments = new[] { Seg(new[] { Habit(0, "A", 10), Habit(1, "B", 30), Habit(2, "C", 5) }, now, T(9, 20)) };

            List<ScheduleBlock> blocks = HabitBlocks(Build(segments, now, T(18)));

            List<ScheduleBlock> timed = blocks.Where(b => !b.DidOverflow).ToList();
            CollectionAssert.AreEqual(new List<string> { "A", "C" }, Titles(timed));
            Assert.AreEqual(timed[0].End, timed[1].Start, "C is pulled forward into the time B could not use");

            // The habit that fit nowhere is not dropped: it forms the "not enough time today" group,
            // anchored after the last habit that did fit so it sorts below it.
            List<ScheduleBlock> overflow = blocks.Where(b => b.DidOverflow).ToList();
            CollectionAssert.AreEqual(new List<string> { "B" }, Titles(overflow));
            Assert.IsTrue(overflow.All(b => b.Start >= timed[1].End));
            Assert.IsTrue(overflow.All(b => b.IsActionable));
        }

        // MARK: - Gap before a meeting (the reported morning: 06:30 routine, meetings from 07:00)

        private static ScheduleHabitInput[] ScreenshotHabits()
        {
            return new[]
            {
                Habit(0, "A", 5), Habit(1, "B", 8), Habit(2, "C", 45), Habit(3, "D", 10), Habit(4, "E", 20),
            };
        }

        private static ScheduleMeetingInput[] ScreenshotMeetings()
        {
            return new[]
            {
                new ScheduleMeetingInput("m1", "Help with Sean", T(7), T(8)),
                new ScheduleMeetingInput("m2", "Standup", T(8, 15), T(9)),
            };
        }

        [TestMethod]
        public void GapBeforeMeetingFilledByShorterHabitAndLongHabitMovesPastMeetings()
        {
            DateTimeOffset now = T(6, 30);
            // Window runs well past the meetings (the morning window ends at the shutdown time).
            var segments = new[] { Seg(ScreenshotHabits(), now, T(12)) };

            IReadOnlyList<ScheduleBlock> blocks = Build(segments, now, T(18), ScreenshotMeetings());
            List<ScheduleBlock> habits = HabitBlocks(blocks);

            Assert.IsTrue(habits.All(b => !b.DidOverflow), "everything fits somewhere in the window");
            // A and B fill 06:30–06:43; C (45m) cannot finish before 07:00, so D (10m) is pulled forward
            // into 06:43–06:53. E (20m) fits neither the 7 min left nor the 15 min between meetings.
            // C then lands in the first gap after the meetings that fits it, and E follows in order.
            CollectionAssert.AreEqual(new List<string> { "A", "B", "D", "C", "E" }, Titles(habits));
            Assert.AreEqual(T(6, 43), habits[2].Start);
            Assert.AreEqual(T(6, 53), habits[2].End);
            Assert.AreEqual(T(9), habits[3].Start);
            Assert.AreEqual(T(9, 45), habits[4].Start);

            Assert.IsFalse(blocks.Any(b => b.Kind == ScheduleBlockKind.DeepWork && b.Start < T(7)),
                           "no deep-work block before the meeting while a habit still fits");
            // The 15 min between the meetings fits no remaining habit, so that gap IS deep work.
            Assert.IsTrue(blocks.Any(b => b.Kind == ScheduleBlockKind.DeepWork && b.Start == T(8) && b.End == T(8, 15)));
            CollectionAssert.AreEqual(new List<string> { "A" }, Titles(habits.Where(b => b.IsCurrent)));
        }

        [TestMethod]
        public void HabitThatFitsNowhereInWindowOverflows()
        {
            DateTimeOffset now = T(6, 30);
            // Window ends at 09:30: after the meetings only 30 min remain, so C (45m) fits nowhere and
            // must surface in the "not enough time today" group; E (20m) still takes the 09:00 slot.
            var segments = new[] { Seg(ScreenshotHabits(), now, T(9, 30)) };

            List<ScheduleBlock> habits = HabitBlocks(Build(segments, now, T(18), ScreenshotMeetings()));

            List<ScheduleBlock> timed = habits.Where(b => !b.DidOverflow).ToList();
            CollectionAssert.AreEqual(new List<string> { "A", "B", "D", "E" }, Titles(timed));
            Assert.AreEqual(T(9), timed[3].Start);

            List<ScheduleBlock> overflow = habits.Where(b => b.DidOverflow).ToList();
            CollectionAssert.AreEqual(new List<string> { "C" }, Titles(overflow));
            Assert.IsTrue(overflow.All(b => b.Start >= T(9, 20)), "overflow sorts below the last placed habit");
            Assert.IsTrue(overflow.All(b => b.IsActionable));
        }

        [TestMethod]
        public void LeaveBufferShrinksGapBeforeMeeting()
        {
            DateTimeOffset now = T(6, 30);
            // 5 min buffer: the gap is 06:30–06:43 (not 06:48), so A (15m) cannot be squeezed in and
            // moves past the meeting while B (10m) fills the gap.
            var meeting = new ScheduleMeetingInput("m1", "Standup", T(6, 48), T(7));
            var segments = new[] { Seg(new[] { Habit(0, "A", 15), Habit(1, "B", 10) }, now, T(12)) };

            List<ScheduleBlock> habits = HabitBlocks(Build(segments, now, T(18), new[] { meeting }, 5 * 60));

            CollectionAssert.AreEqual(new List<string> { "B", "A" }, Titles(habits));
            Assert.AreEqual(now, habits[0].Start);
            Assert.AreEqual(T(7), habits[1].Start);
        }

        [TestMethod]
        public void PlayingHabitUsesRemainingDurationToFit()
        {
            DateTimeOffset now = T(6, 30);
            // A is 30m but has 20m elapsed: its remaining 10m fits before the 06:45 meeting.
            var meeting = new ScheduleMeetingInput("m1", "Standup", T(6, 45), T(7));
            var segments = new[] { Seg(new[] { Habit(0, "A", 30, playing: true, elapsedSeconds: 20 * 60) }, now, T(12)) };

            List<ScheduleBlock> habits = HabitBlocks(Build(segments, now, T(18), new[] { meeting }));

            Assert.AreEqual(now, habits.Single().Start);
            Assert.AreEqual(T(6, 40), habits[0].End);
        }

        // MARK: - Not enough time today (overflow group)

        [TestMethod]
        public void WholeDayEventPutsEntireRoutineInOverflowGroup()
        {
            DateTimeOffset now = T(9);
            // A meeting spanning the whole routine window: no habit can be placed at all. The routine
            // must still be visible (and doable) as one collapsed group — not vanish.
            var allDay = new ScheduleMeetingInput("m1", "Offsite", T(8), T(18));
            var segments = new[] { Seg(new[] { Habit(0, "A", 10), Habit(1, "B", 15) }, now, T(12)) };

            List<ScheduleBlock> blocks = HabitBlocks(Build(segments, now, T(18), new[] { allDay }));

            CollectionAssert.AreEqual(new List<string> { "A", "B" }, Titles(blocks));
            Assert.IsTrue(blocks.All(b => b.DidOverflow), "nothing fit, so the whole routine overflows");
            Assert.IsTrue(blocks.All(b => b.IsActionable));
            // With no timed habit, the first overflow habit is the routine's next-up selection.
            CollectionAssert.AreEqual(new List<string> { "A" }, Titles(blocks.Where(b => b.IsCurrent)));
        }

        /// <summary>
        /// A preview (non-actionable) routine used to DROP the habits that did not fit, so the plan
        /// silently claimed the routine fitted. It must surface them as an overflow group too — just not
        /// actionable.
        /// </summary>
        [TestMethod]
        public void PreviewSegmentSurfacesOverflowGroupWithoutMakingItActionable()
        {
            DateTimeOffset now = T(9);
            var allDay = new ScheduleMeetingInput("m1", "Offsite", T(8), T(18));
            var segments = new[] { Seg(new[] { Habit(0, "A", 10) }, now, T(12), actionable: false) };

            List<ScheduleBlock> blocks = HabitBlocks(Build(segments, now, T(18), new[] { allDay }));

            CollectionAssert.AreEqual(new List<string> { "A" }, Titles(blocks));
            Assert.IsTrue(blocks.All(b => b.DidOverflow));
            Assert.IsTrue(blocks.All(b => !b.IsActionable && !b.IsCurrent), "a preview routine stays read-only");
            Assert.IsTrue(blocks.All(b => b.IsEditable));
        }

        /// <summary>
        /// The reported case (Sat 12 Sep): a FUTURE day boxed in by all-day-ish events — sleep until
        /// 6:30, then meetings from 7:00 through past the evening routine start. Only 27 minutes of the
        /// morning routine fit; the rest has nowhere to go anywhere in the routine's window, and the
        /// plan must say so instead of rendering a tidy 6:30–6:57 range.
        /// </summary>
        [TestMethod]
        public void FutureDayWithNoRoomFlagsTheRestOfTheRoutineAsOverflow()
        {
            DateTimeOffset now = Day(2, 5, 0);
            var meetings = new[]
            {
                new ScheduleMeetingInput("sleep", "Sleep", Day(2, 0, 0), Day(2, 6, 30)),
                new ScheduleMeetingInput("m1", "Help with Sean", Day(2, 7, 0), Day(2, 8, 0)),
                new ScheduleMeetingInput("m2", "Look after Sean", Day(2, 8, 0), Day(2, 20, 30)),
            };
            // Morning routine window runs to the evening routine start (8pm) — which the last meeting
            // outlasts, so there is no later gap for the long habit to move into.
            var segments = new[]
            {
                Seg(new[] { Habit(0, "A", 10), Habit(1, "B", 10), Habit(2, "C", 7), Habit(3, "D", 30) },
                    now, Day(2, 20, 0), actionable: false),
            };

            List<ScheduleBlock> blocks = HabitBlocks(Build(segments, now, Day(2, 23, 59), meetings, 2 * 60));

            List<ScheduleBlock> fitted = blocks.Where(b => !b.DidOverflow).ToList();
            CollectionAssert.AreEqual(new List<string> { "A", "B", "C" }, Titles(fitted));
            Assert.AreEqual(Day(2, 6, 30), fitted.First().Start, "the first habit starts when sleep ends");
            Assert.AreEqual(Day(2, 6, 57), fitted.Last().End, "and the last one stops before the 7am meeting");

            List<ScheduleBlock> overflow = blocks.Where(b => b.DidOverflow).ToList();
            CollectionAssert.AreEqual(new List<string> { "D" }, Titles(overflow), "the 30m habit fits nowhere");
            Assert.AreEqual(30 * 60, overflow.First().OverflowDurationSeconds, "its own length drives the row's label");
            Assert.AreEqual(overflow.First().Start, overflow.First().End,
                            "overflow is zero-duration so it does not eat free time");
            Assert.AreEqual(Day(2, 6, 57), overflow.First().Start, "anchored after the last habit that fit");
        }

        /// <summary>
        /// The same future day, but with the routine window reaching past the meetings: the long habit is
        /// MOVED to the free evening slot rather than reported as not fitting (that rule stays intact).
        /// </summary>
        [TestMethod]
        public void FutureDayMovesTheLongHabitLaterWhenThereIsRoom()
        {
            DateTimeOffset now = Day(2, 5, 0);
            var meetings = new[]
            {
                new ScheduleMeetingInput("sleep", "Sleep", Day(2, 0, 0), Day(2, 6, 30)),
                new ScheduleMeetingInput("m1", "Meetings", Day(2, 7, 0), Day(2, 20, 30)),
            };
            var segments = new[]
            {
                Seg(new[] { Habit(0, "D", 30), Habit(1, "A", 10) }, now, Day(2, 21, 30), actionable: false),
            };

            List<ScheduleBlock> blocks = HabitBlocks(Build(segments, now, Day(2, 23, 59), meetings, 2 * 60));

            Assert.IsTrue(blocks.All(b => !b.DidOverflow), "everything fits once there is a later gap");
            // A (10m) is pulled forward into the short morning gap; D (30m) takes the evening slot.
            CollectionAssert.AreEqual(new List<string> { "A", "D" }, Titles(blocks));
            Assert.AreEqual(Day(2, 6, 30), blocks[0].Start);
            Assert.AreEqual(Day(2, 20, 30), blocks[1].Start);
        }

        /// <summary>
        /// "Earlier today" history is laid out with no window cap, so it never produces an overflow group
        /// (which would double up habits already rendered above it).
        /// </summary>
        [TestMethod]
        public void EarlierSectionNeverOverflows()
        {
            DateTimeOffset now = T(12);
            var earlier = Seg(new[] { Habit(0, "A", 45) }, T(7), T(7, 10), actionable: false, name: "Earlier",
                              isEarlier: true);

            List<ScheduleBlock> blocks = HabitBlocks(Build(new[] { earlier }, now, T(23)));

            CollectionAssert.AreEqual(new List<string> { "A" }, Titles(blocks));
            Assert.IsTrue(blocks.All(b => !b.DidOverflow));
            Assert.AreEqual(T(7), blocks[0].Start, "history is laid out at its real window start, not at now");
        }

        // MARK: - Meetings

        [TestMethod]
        public void HabitPushedPastOverlappingMeeting()
        {
            DateTimeOffset now = T(9);
            var meeting = new ScheduleMeetingInput("m1", "Standup", T(9, 5), T(9, 35));
            var segments = new[] { Seg(new[] { Habit(0, "A", 20) }, now, T(12)) };

            List<ScheduleBlock> blocks = HabitBlocks(Build(segments, now, T(18), new[] { meeting }));

            Assert.AreEqual(1, blocks.Count);
            // A (20m) cannot finish before the 9:05 meeting, so it starts no earlier than the meeting end.
            Assert.IsTrue(blocks[0].Start >= T(9, 35));
        }

        [TestMethod]
        public void MeetingAppearsAsBlock()
        {
            DateTimeOffset now = T(9);
            var meeting = new ScheduleMeetingInput("m1", "Standup", T(10), T(10, 30));

            IReadOnlyList<ScheduleBlock> blocks =
                Build(new[] { Seg(new[] { Habit(0, "A", 10) }, now, T(12)) }, now, T(18), new[] { meeting });

            Assert.IsTrue(blocks.Any(b => b.Kind == ScheduleBlockKind.Meeting && b.EventId == "m1"));
        }

        // MARK: - Window capping

        [TestMethod]
        public void HabitsNeverSpillPastWindowEnd()
        {
            DateTimeOffset now = T(9);
            var segments = new[] { Seg(new[] { Habit(0, "A", 30) }, now, T(9, 45)) };

            List<ScheduleBlock> blocks = HabitBlocks(Build(segments, now, T(18)));

            Assert.IsTrue(blocks.All(b => b.End <= T(9, 45)));
        }

        // MARK: - Cutoff

        [TestMethod]
        public void PastCutoffHabitDroppedButLaterShown()
        {
            DateTimeOffset now = T(9);
            // A's cutoff (8:00) has already passed → dropped; B (no cutoff) still shows.
            var segments = new[] { Seg(new[] { Habit(0, "A", 10, cutoff: T(8)), Habit(1, "B", 10) }, now, T(12)) };

            List<ScheduleBlock> blocks = HabitBlocks(Build(segments, now, T(18)));

            CollectionAssert.AreEqual(new List<string> { "B" }, Titles(blocks));
        }

        [TestMethod]
        public void CutoffStillInTheFutureKeepsTheHabitEvenWhenItsSlotLandsLater()
        {
            DateTimeOffset now = T(9);
            // The cutoff has not passed, so the habit stays visible — the user can still do it out of
            // order right up to it, even though its projected slot is after the cutoff.
            var segments = new[] { Seg(new[] { Habit(0, "A", 60), Habit(1, "B", 10, cutoff: T(9, 30)) }, now, T(12)) };

            List<ScheduleBlock> blocks = HabitBlocks(Build(segments, now, T(18)));

            CollectionAssert.AreEqual(new List<string> { "A", "B" }, Titles(blocks));
            Assert.AreEqual(T(10), blocks[1].Start, "projected past its own cutoff, and still shown");
        }

        // MARK: - Actionability

        [TestMethod]
        public void OnlyNextHabitIsCurrentInActionableSegment()
        {
            DateTimeOffset now = T(9);
            var segments = new[] { Seg(new[] { Habit(0, "A", 10), Habit(1, "B", 10) }, now, T(12), actionable: true) };

            List<ScheduleBlock> blocks = HabitBlocks(Build(segments, now, T(18)));

            Assert.AreEqual(1, blocks.Count(b => b.IsCurrent));
            Assert.IsTrue(blocks.First().IsCurrent, "the earliest habit is the current one");
            Assert.IsTrue(blocks.All(b => b.IsActionable));
        }

        [TestMethod]
        public void PreviewSegmentNotActionable()
        {
            DateTimeOffset now = T(9);
            var segments = new[] { Seg(new[] { Habit(0, "A", 10) }, now, T(12), actionable: false) };

            List<ScheduleBlock> blocks = HabitBlocks(Build(segments, now, T(18)));

            Assert.IsTrue(blocks.All(b => !b.IsActionable && !b.IsCurrent));
        }

        /// <summary>
        /// Every habit is editable from the Day Plan — a preview (future) routine's, and "earlier today"
        /// history's too, since the editor changes the habit's settings rather than the day's record.
        /// Only <c>IsActionable</c> (Play/Done/Skip on today's row) distinguishes the active routine.
        /// </summary>
        [TestMethod]
        public void EverySegmentIsEditableIncludingEarlierAndPreview()
        {
            DateTimeOffset now = T(9);
            var active = Seg(new[] { Habit(0, "A", 10) }, now, T(12), actionable: true, name: "Morning");
            var preview = Seg(new[] { Habit(1, "B", 10) }, T(17), T(18), actionable: false, name: "Evening");
            var earlier = Seg(new[] { Habit(2, "C", 10) }, T(7), T(8), actionable: false, name: "Earlier",
                              isEarlier: true);

            List<ScheduleBlock> blocks = HabitBlocks(Build(new[] { earlier, active, preview }, now, T(23)));
            Dictionary<string, ScheduleBlock> byName = blocks.ToDictionary(b => b.RoutineName ?? string.Empty);

            Assert.IsTrue(byName["Morning"].IsActionable);
            Assert.IsTrue(byName["Morning"].IsEditable);
            Assert.IsFalse(byName["Evening"].IsActionable);
            Assert.IsTrue(byName["Evening"].IsEditable);
            Assert.IsFalse(byName["Earlier"].IsActionable);
            Assert.IsTrue(byName["Earlier"].IsEditable, "completed/past habits are edited, not re-run");
        }

        // MARK: - Deep-work sprint splitting
        //
        // The cadence comes from the server's `break_after_minutes`: sprintWorkSeconds is how long you
        // work before a break, sprintBreakSeconds is how long the break lasts.

        private static IReadOnlyList<ScheduleBlock> DeepWorkDay(int workSeconds, int breakSeconds,
                                                                DateTimeOffset from, DateTimeOffset to)
        {
            return ScheduleBuilder.Build(new List<ScheduleRoutineSegment>(), new List<ScheduleMeetingInput>(),
                                         from, to, 0, sprintWorkSeconds: workSeconds,
                                         sprintBreakSeconds: breakSeconds);
        }

        [TestMethod]
        public void WithNoBreakCadenceTheWholeGapIsOneDeepWorkBlock()
        {
            IReadOnlyList<ScheduleBlock> blocks = DeepWorkDay(0, 0, T(9), T(12));

            Assert.AreEqual(1, blocks.Count);
            Assert.AreEqual(ScheduleBlockKind.DeepWork, blocks[0].Kind);
            Assert.AreEqual(T(9), blocks[0].Start);
            Assert.AreEqual(T(12), blocks[0].End);
        }

        [TestMethod]
        public void ANonPositiveWorkOrBreakValueNeverSplits()
        {
            // `breakAfterMinutes` defaults to 0 until the server says otherwise, so both of these are
            // reachable states and neither may produce a split (or an endless loop).
            Assert.AreEqual(1, DeepWorkDay(25 * 60, 0, T(9), T(12)).Count);
            Assert.AreEqual(1, DeepWorkDay(0, 5 * 60, T(9), T(12)).Count);
            Assert.AreEqual(1, DeepWorkDay(-25 * 60, -5 * 60, T(9), T(12)).Count);
        }

        [TestMethod]
        public void ABlockShorterThanTheWorkIntervalStaysOneBlock()
        {
            IReadOnlyList<ScheduleBlock> blocks = DeepWorkDay(50 * 60, 10 * 60, T(9), T(9, 30));

            Assert.AreEqual(1, blocks.Count, "a 30-minute gap under a 50-minute work interval is not split");
            Assert.AreEqual(ScheduleBlockKind.DeepWork, blocks[0].Kind);
            Assert.AreEqual(T(9, 30), blocks[0].End);
        }

        [TestMethod]
        public void SprintsAlternateWorkAndBreakAcrossTheGap()
        {
            IReadOnlyList<ScheduleBlock> blocks = DeepWorkDay(25 * 60, 5 * 60, T(9), T(11));

            CollectionAssert.AreEqual(
                new List<ScheduleBlockKind>
                {
                    ScheduleBlockKind.DeepWork, ScheduleBlockKind.BreakSprint,
                    ScheduleBlockKind.DeepWork, ScheduleBlockKind.BreakSprint,
                    ScheduleBlockKind.DeepWork, ScheduleBlockKind.BreakSprint,
                    ScheduleBlockKind.DeepWork,
                },
                blocks.Select(b => b.Kind).ToList());
            Assert.AreEqual(T(9, 25), blocks[0].End);
            Assert.AreEqual(T(9, 30), blocks[1].End);
            Assert.AreEqual(T(9, 55), blocks[2].End);
            Assert.AreEqual(T(10, 25), blocks[5].Start, "each sprint is 25 minutes");
            Assert.AreEqual(T(10, 30), blocks[5].End, "and each break five");
            Assert.AreEqual(T(11), blocks.Last().End, "the sprints exactly fill the gap");
            Assert.AreEqual(T(10, 30), blocks.Last().Start,
                            "the last five minutes are too short for a break, so the final sprint absorbs them");
        }

        [TestMethod]
        public void ATrailingRemainderTooSmallForABreakIsFoldedIntoTheLastSprint()
        {
            // 9:00–9:53 at 25/5: work, break, work, and then 3 minutes left — too short for a break, so
            // the day must not end on a stray break.
            IReadOnlyList<ScheduleBlock> blocks = DeepWorkDay(25 * 60, 5 * 60, T(9), T(9, 53));

            Assert.AreEqual(ScheduleBlockKind.DeepWork, blocks.Last().Kind, "never ends on a break");
            Assert.AreEqual(T(9, 53), blocks.Last().End);
            Assert.AreEqual(T(9, 30), blocks.Last().Start, "the remainder is absorbed into the final sprint");
        }

        [TestMethod]
        public void AGapShorterThanTheDeepWorkMinimumIsNotFilledAtAll()
        {
            // 9 minutes of free time is below the 10-minute floor, so no deep-work row is offered.
            var meeting = new ScheduleMeetingInput("m1", "Standup", T(9, 9), T(18));
            IReadOnlyList<ScheduleBlock> blocks = ScheduleBuilder.Build(
                new List<ScheduleRoutineSegment>(), new[] { meeting }, T(9), T(18), 0);

            Assert.IsFalse(blocks.Any(b => b.Kind == ScheduleBlockKind.DeepWork));
        }

        [TestMethod]
        public void NoDeepWorkIsSuggestedPastTheUsersCutoff()
        {
            IReadOnlyList<ScheduleBlock> blocks = ScheduleBuilder.Build(
                new List<ScheduleRoutineSegment>(), new List<ScheduleMeetingInput>(), T(9), T(23, 59), 0,
                deepWorkCutoff: T(17));

            Assert.AreEqual(T(17), blocks.Single(b => b.Kind == ScheduleBlockKind.DeepWork).End);
        }

        // MARK: - Ordering

        [TestMethod]
        public void OverflowHabitsStayContiguousAndInRoutineOrderAgainstADeepWorkBlockAtTheSameStart()
        {
            DateTimeOffset now = T(9);
            // Nothing fits before the meeting, so every habit is anchored at `now` — where the deep-work
            // filler for the freed time also starts. A deep-work row sorting BETWEEN two of them is what
            // split one "not enough time today" card into two.
            var meeting = new ScheduleMeetingInput("m1", "Offsite", T(9, 30), T(18));
            var segments = new[]
            {
                Seg(new[] { Habit(0, "A", 45), Habit(1, "B", 50), Habit(2, "C", 55) }, now, T(12)),
            };

            IReadOnlyList<ScheduleBlock> blocks = Build(segments, now, T(18), new[] { meeting });
            List<ScheduleBlock> ordered = blocks.Where(b => b.Kind == ScheduleBlockKind.Habit
                                                            || b.Kind == ScheduleBlockKind.DeepWork).ToList();

            CollectionAssert.AreEqual(new List<string> { "A", "B", "C" },
                                      Titles(ordered.Take(3)), "the overflow group stays together, in routine order");
            Assert.AreEqual(ScheduleBlockKind.DeepWork, ordered[3].Kind, "and deep work sorts after all of it");
        }

        [TestMethod]
        public void TheEveningTransitionMarkerIsDrawnOnlyWhenItIsStillAhead()
        {
            IReadOnlyList<ScheduleBlock> ahead = ScheduleBuilder.Build(
                new List<ScheduleRoutineSegment>(), new List<ScheduleMeetingInput>(), T(9), T(23, 59), 0,
                eveningRoutineStart: T(21));
            Assert.AreEqual(1, ahead.Count(b => b.Kind == ScheduleBlockKind.RoutineTransition));

            IReadOnlyList<ScheduleBlock> past = ScheduleBuilder.Build(
                new List<ScheduleRoutineSegment>(), new List<ScheduleMeetingInput>(), T(22), T(23, 59), 0,
                eveningRoutineStart: T(21));
            Assert.IsFalse(past.Any(b => b.Kind == ScheduleBlockKind.RoutineTransition));
        }

        [TestMethod]
        public void MeetingsAreShownFromNowUnlessAnEarlierFloorIsGiven()
        {
            DateTimeOffset now = T(9);
            var finished = new ScheduleMeetingInput("m1", "Earlier standup", T(7), T(7, 30));

            IReadOnlyList<ScheduleBlock> today = ScheduleBuilder.Build(
                new List<ScheduleRoutineSegment>(), new[] { finished }, now, T(18), 0);
            Assert.IsFalse(today.Any(b => b.Kind == ScheduleBlockKind.Meeting));

            IReadOnlyList<ScheduleBlock> withHistory = ScheduleBuilder.Build(
                new List<ScheduleRoutineSegment>(), new[] { finished }, now, T(18), 0, meetingsFrom: T(0));
            Assert.AreEqual(1, withHistory.Count(b => b.Kind == ScheduleBlockKind.Meeting));
        }

        // MARK: - Free intervals

        [TestMethod]
        public void OverlappingBusySpansAreMergedBeforeGapsAreMeasured()
        {
            var busy = new List<TimeRange>
            {
                new TimeRange(T(10), T(11)),
                new TimeRange(T(10, 30), T(12)),   // overlaps the first
                new TimeRange(T(9), T(9, 30)),     // out of order
            };

            IReadOnlyList<TimeRange> free = ScheduleBuilder.FreeIntervals(T(9), T(13), busy);

            CollectionAssert.AreEqual(
                new List<TimeRange> { new TimeRange(T(9, 30), T(10)), new TimeRange(T(12), T(13)) },
                free.ToList());
        }

        [TestMethod]
        public void AWindowThatEndsBeforeItStartsHasNoFreeTime()
        {
            Assert.AreEqual(0, ScheduleBuilder.FreeIntervals(T(12), T(9), new List<TimeRange>()).Count);
        }

        // MARK: - Time-zone behaviour, where C# and Swift could have parted company

        /// <summary>
        /// Instants, not wall-clock times. A 30-minute habit that starts 15 minutes before a
        /// spring-forward still takes 30 minutes; its END is 03:15 local, not the 02:15 that does not
        /// exist. This is what Swift's <c>Date.addingTimeInterval</c> does, and it is why this port uses
        /// <c>DateTimeOffset</c> — the same code on a local <c>DateTime</c> would have produced the
        /// non-existent 02:15 and then disagreed with the Mac for the rest of the day.
        /// </summary>
        [TestMethod]
        public void HabitCrossingSpringForwardKeepsItsRealLength()
        {
            TimeZoneInfo melbourne = FindTimeZone("Australia/Melbourne", "AUS Eastern Standard Time");
            if (melbourne == null) Assert.Inconclusive("No Melbourne time zone on this machine.");

            // 2026-10-04 02:00 AEST → 03:00 AEDT.
            DateTimeOffset start = new DateTimeOffset(2026, 10, 4, 1, 45, 0, TimeSpan.FromHours(10));
            var segments = new[]
            {
                new ScheduleRoutineSegment("Morning", new[] { Habit(0, "A", 30) }, start,
                                           start.AddHours(6), true),
            };

            IReadOnlyList<ScheduleBlock> blocks = ScheduleBuilder.Build(
                segments, new List<ScheduleMeetingInput>(), start, start.AddHours(6), 0);
            ScheduleBlock habit = blocks.First(b => b.Kind == ScheduleBlockKind.Habit);

            Assert.AreEqual(30 * 60, habit.DurationSeconds, "the habit still takes half an hour");
            DateTimeOffset localEnd = TimeZoneInfo.ConvertTime(habit.End, melbourne);
            Assert.AreEqual(3, localEnd.Hour);
            Assert.AreEqual(15, localEnd.Minute);
        }

        private static TimeZoneInfo FindTimeZone(params string[] ids)
        {
            foreach (string id in ids)
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }
            return null;
        }
    }
}
