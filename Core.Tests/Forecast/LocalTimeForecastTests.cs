using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Forecast;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using F = FocusBear.Core.Forecast.LocalTimeForecast;

namespace FocusBear.Core.Tests.Forecast
{
    /// <summary>
    /// Ported from the Mac's LocalTimeForecastTests / ForecastReschedulingTests (report 2026-09-25_14-33-34:
    /// "missing the reprioritise tasks feature that we have on Mac").
    /// </summary>
    [TestClass]
    public class LocalTimeForecastTests
    {
        private static readonly DateTime Start = new DateTime(2026, 9, 7);

        private static DateTime Date(int day = 7, int hour = 0, int minute = 0)
        {
            return new DateTime(2026, 9, day, hour, minute, 0);
        }

        private static F.Interval Span(int from, int to, int day = 7)
        {
            return new F.Interval(Date(day, from), to == 24 ? Date(day + 1) : Date(day, to));
        }

        /// <summary>A day with default sleep (22:00–06:00) + 2h self-care: 14h of work, less what a meeting takes.</summary>
        private static F.DayInput Day(int day = 7, double? workMinutes = null)
        {
            var meetings = new List<F.Interval>();
            if (workMinutes.HasValue)
            {
                // Work window is 06:00–22:00 less 2h care = 14h; a meeting from 06:00 takes the rest.
                double busy = 14 * 60 - workMinutes.Value;
                if (busy > 0) meetings.Add(new F.Interval(Date(day, 6), Date(day, 6).AddMinutes(busy)));
            }
            return F.DayInput.FromIntervals(Date(day), meetings, null, new F.CapacitySettings { SelfCareMinutes = 120 });
        }

        private static F.Result Calc(IEnumerable<F.DayInput> days, IEnumerable<F.ProjectInput> projects = null,
                                     IEnumerable<F.TaskInput> tasks = null)
        {
            return F.Calculate(days, projects ?? new F.ProjectInput[0], tasks ?? new F.TaskInput[0]);
        }

        // MARK: - Capacity

        [TestMethod]
        public void SleepAndSelfCareLeaveFourteenHours()
        {
            F.Result r = Calc(new[] { Day() });
            Assert.AreEqual(14 * 60, r.Days[0].Capacity.AvailableWorkMinutes, 0.001);
            Assert.AreEqual(10 * 60, r.Days[0].Capacity.SleepAndSelfCareMinutes, 0.001);
        }

        [TestMethod]
        public void MeetingUnionClipsAcrossMidnightAndDoesNotDoubleCountHabits()
        {
            var input = F.DayInput.FromIntervals(Date(), new[]
            {
                new F.Interval(Date(6, 23), Date(7, 1)), Span(9, 11), Span(10, 12)
            }, new[]
            {
                F.PlacedBlock.Habit("one", "habit:one", Span(11, 13)),
                F.PlacedBlock.Habit("two", "habit:two", Span(12, 14)),
            });
            F.Capacity c = Calc(new[] { input }).Days[0].Capacity;
            Assert.AreEqual(180, c.MeetingMinutes, 0.001, "0–1 overlaps sleep; 9–12 is 180");
            Assert.AreEqual(120, c.HabitMinutes, 0.001);
            Assert.AreEqual(660, c.AvailableWorkMinutes, 0.001);
        }

        [TestMethod]
        public void SliversBetweenMeetingsAreNotWorkTime()
        {
            // 06:00–22:00 packed except for two 5-minute gaps.
            var input = F.DayInput.FromIntervals(Date(), new[]
            {
                new F.Interval(Date(7, 6), Date(7, 12)), new F.Interval(Date(7, 12, 5), Date(7, 18)), new F.Interval(Date(7, 18, 5), Date(7, 22)),
            }, null);
            F.Capacity c = Calc(new[] { input }).Days[0].Capacity;
            Assert.AreEqual(10, c.RawWorkMinutes, 0.001);
            Assert.AreEqual(0, c.AvailableWorkMinutes, 0.001);
        }

        [TestMethod]
        public void VeryBusyDayExposesDeficitAndNeverAllocatesNegativeTime()
        {
            var input = F.DayInput.FromIntervals(Date(), new[] { Span(0, 24) }, null, new F.CapacitySettings { SelfCareMinutes = 120 });
            F.Result r = Calc(new[] { input }, tasks: new[] { new F.TaskInput("a", estimatedMinutes: 20) });
            Assert.AreEqual(120, r.Days[0].Capacity.OvercommittedMinutes, 0.001);
            Assert.AreEqual(20, r.Tasks[0].UnallocatedMinutes, 0.001);
            Assert.AreEqual(0, r.Days[0].AllocatedWorkMinutes, 0.001);
        }

        [TestMethod]
        public void TodayCannotReuseElapsedTime()
        {
            var input = new F.DayInput(Date(), new F.PlacedBlock[0], new F.CapacitySettings(), availableFrom: Date(7, 20));
            F.Capacity c = Calc(new[] { input }).Days[0].Capacity;
            Assert.AreEqual(120, c.AvailableWorkMinutes, 0.001, "20:00–22:00 is all that's left");
        }

        // MARK: - Allocation order

        [TestMethod]
        public void AnEarlierProjectDeadlineBeatsAHigherRating()
        {
            var projects = new[] { new F.ProjectInput("low", 4, Date()), new F.ProjectInput("high", 10, Date(8)) };
            F.Result r = Calc(new[] { Day(workMinutes: 60) }, projects, new[]
            {
                new F.TaskInput("low-task", "low", 60, outcome: 10),
                new F.TaskInput("high-task", "high", 60, outcome: 2),
            });
            Assert.AreEqual("low-task", r.Tasks[0].Task.Id);
            Assert.AreEqual(0, r.Tasks[0].UnallocatedMinutes, 0.001);
            Assert.AreEqual(60, r.Tasks[1].UnallocatedMinutes, 0.001);
        }

        [TestMethod]
        public void TaskOrderingUsesDueThenOutcomeThenPerspirationAndStableId()
        {
            var project = new F.ProjectInput("p");
            var inputs = new[]
            {
                new F.TaskInput("b", "p", 10, 6, 2, Date()),
                new F.TaskInput("a", "p", 10, 6, 2, Date()),
                new F.TaskInput("harder", "p", 10, 6, 8, Date()),
                new F.TaskInput("later", "p", 10, 6, 1, Date(8)),
                new F.TaskInput("important", "p", 10, 10, 1, Date(9)),
            };
            F.Result r = Calc(new[] { Day() }, new[] { project }, inputs);
            CollectionAssert.AreEqual(new[] { "a", "b", "harder", "later", "important" }, r.Tasks.Select(t => t.Task.Id).ToList());
            F.Result shuffled = Calc(new[] { Day() }, new[] { project }, inputs.Reverse());
            CollectionAssert.AreEqual(r.Tasks.Select(t => t.Task.Id).ToList(), shuffled.Tasks.Select(t => t.Task.Id).ToList());
        }

        [TestMethod]
        public void WorkSpillsOntoLaterDays()
        {
            F.Result r = Calc(new[] { Day(7, 60), Day(8, 60) }, tasks: new[] { new F.TaskInput("t", estimatedMinutes: 90) });
            Assert.AreEqual(2, r.Tasks[0].Allocations.Count);
            Assert.AreEqual(Date(8), r.Tasks[0].CompletionDate);
        }

        [TestMethod]
        public void TaskPinnedToAHabitIsAbsorbedByIt()
        {
            var input = F.DayInput.FromIntervals(Date(), null, new[] { F.PlacedBlock.Habit("h", "habit:Read", Span(8, 9)) },
                                                 new F.CapacitySettings { SelfCareMinutes = 120 });
            var task = new F.TaskInput("t", estimatedMinutes: 45, blockAssignment: new F.BlockAssignment(Date(), "habit:Read"));
            F.Result r = Calc(new[] { input }, tasks: new[] { task });
            Assert.AreEqual(45, r.Tasks[0].Allocations[0].AbsorbedBlockMinutes, 0.001);
            Assert.AreEqual(0, r.Tasks[0].Allocations[0].WorkMinutes, 0.001);
            Assert.AreEqual(13 * 60, r.Days[0].RemainingWorkMinutes, 0.001, "the habit's hour was already off the day");
        }

        [TestMethod]
        public void HardDeadlineIsReservedOnItsOwnDayFirst()
        {
            var tasks = new[]
            {
                new F.TaskInput("flexible", estimatedMinutes: 60, outcome: 2, dueDate: Date(7)),
                new F.TaskInput("hard", estimatedMinutes: 60, outcome: 10, dueDate: Date(8)),
            };
            F.Result r = Calc(new[] { Day(7, 60), Day(8, 60) }, tasks: tasks);
            F.TaskResult hard = r.Tasks.Single(t => t.Task.Id == "hard");
            Assert.AreEqual(Date(8), hard.CompletionDate);
            Assert.AreEqual(Date(7), r.Tasks.Single(t => t.Task.Id == "flexible").CompletionDate);
        }

        [TestMethod]
        public void LockedDayTakesOnlyWorkPinnedToIt()
        {
            var locked = Day(7, 60); locked.IsLocked = true;
            var tasks = new[]
            {
                new F.TaskInput("pinned", estimatedMinutes: 30, notBefore: Date(7)),
                new F.TaskInput("loose", estimatedMinutes: 30),
            };
            F.Result r = Calc(new[] { locked, Day(8, 60) }, tasks: tasks);
            Assert.AreEqual(Date(7), r.Tasks.Single(t => t.Task.Id == "pinned").CompletionDate);
            Assert.AreEqual(Date(8), r.Tasks.Single(t => t.Task.Id == "loose").CompletionDate);
        }

        [TestMethod]
        public void LikelyCompletionCombinesFinishedWorkWithWhatStillFits()
        {
            var project = new F.ProjectInput("p", dueDate: Date(), totalTaskCount: 4, uncompletedTaskCount: 2);
            F.Result r = Calc(new[] { Day(7, 60), Day(8) }, new[] { project }, new[]
            {
                new F.TaskInput("a", "p", 60), new F.TaskInput("b", "p", 60),
            });
            Assert.AreEqual(75, r.Projects[0].LikelyCompletionPercent);
        }

        // MARK: - Day load (the Day Plan's buffer)

        [TestMethod]
        public void BufferIsCapacityLessCommittedAndGoesNegative()
        {
            F.DayLoad ok = F.DayLoadFor(Date(), 120, 90);
            Assert.AreEqual(30, ok.BufferMinutes, 0.001);
            Assert.IsFalse(ok.IsOvercommitted);
            F.DayLoad over = F.DayLoadFor(Date(), 120, 200, absorbedMinutes: 20);
            Assert.AreEqual(-60, over.BufferMinutes, 0.001);
            Assert.AreEqual(60, over.OvercommittedMinutes, 0.001);
            Assert.IsTrue(over.IsOvercommitted);
            Assert.AreEqual(0, over.FreeMinutes, 0.001);
        }

        [TestMethod]
        public void HorizonDayLoadCountsWorkPinnedToTheDay()
        {
            var tasks = new[] { new F.TaskInput("t", estimatedMinutes: 120, notBefore: Date(7)) };
            F.Result r = Calc(new[] { Day(7, 60), Day(8) }, tasks: tasks);
            F.DayLoad? load = F.DayLoadOn(Date(7), r);
            Assert.IsTrue(load.HasValue);
            Assert.AreEqual(60, load.Value.OvercommittedMinutes, 0.001);
        }

        // MARK: - Work plan

        [TestMethod]
        public void PostponementBandsFollowTheWording()
        {
            Assert.AreEqual(0, F.PostponementDays(10));
            Assert.AreEqual(7, F.PostponementDays(8));
            Assert.AreEqual(21, F.PostponementDays(6));
            Assert.AreEqual(90, F.PostponementDays(4));
            Assert.IsNull(F.PostponementDays(2));
            Assert.IsFalse(F.AllowsPostponement(10));
            Assert.IsTrue(F.AllowsPostponement(2));
        }

        [TestMethod]
        public void AnUnratedProjectNeverTightensItsTasks()
        {
            var task = new F.TaskInput("t", "p", 10, outcome: 4);
            Assert.AreEqual(4, F.EffectiveOutcome(task, new F.ProjectInput("p", 0)));
            Assert.AreEqual(10, F.EffectiveOutcome(task, new F.ProjectInput("p", 10)));
            Assert.AreEqual(8, F.EffectiveOutcome(task.With(outcome: 8), new F.ProjectInput("p", 4)), "a stricter task keeps its own rating");
            Assert.IsTrue(F.InheritsProjectOutcome(task, new F.ProjectInput("p", 10)));
        }

        [TestMethod]
        public void OverdueWorkIsPushedToWhenItCanBeDone()
        {
            // Due on the 6th (already gone), no room on the 7th: the plan proposes the 8th.
            var tasks = new[] { new F.TaskInput("t", estimatedMinutes: 60, outcome: 2, dueDate: Date(6)) };
            F.Result r = Calc(new[] { Day(7, 0), Day(8) }, tasks: tasks);
            F.WorkPlan plan = F.ProposeWorkPlan(r);
            Assert.AreEqual(1, plan.Changes.Count);
            Assert.AreEqual("task:t", plan.Changes[0].Id);
            Assert.AreEqual(Date(8), plan.Changes[0].ProposedDate);
            Assert.AreEqual(Date(6), plan.Changes[0].PreviousDate);
        }

        [TestMethod]
        public void HardDeadlineIsNeverMovedEitherWay()
        {
            // Rated 10 and pinned by a Day Plan placement on the 8th, but the 7th has room: still not moved.
            var tasks = new[] { new F.TaskInput("t", estimatedMinutes: 60, outcome: 10, dueDate: Date(9), notBefore: Date(8)) };
            F.Result r = Calc(new[] { Day(7), Day(8), Day(9) }, tasks: tasks);
            F.WorkPlan plan = F.ProposeWorkPlan(r);
            Assert.AreEqual(0, plan.Changes.Count);
            Assert.AreEqual(F.ConflictReason.HardDeadline, plan.Conflicts.Single().Reason);
            Assert.AreEqual(0, plan.ReviewableConflicts.Count, "a hard deadline is not something to review");
        }

        [TestMethod]
        public void PostponementPastTheBandIsAConflict()
        {
            // Rated 8 (a few days = 7): due the 7th, no room until the 16th.
            var days = new List<F.DayInput> { Day(7, 0) };
            for (int d = 8; d <= 15; d++) days.Add(Day(d, 0));
            days.Add(Day(16));
            var tasks = new[] { new F.TaskInput("t", estimatedMinutes: 60, outcome: 8, dueDate: Date(7)) };
            F.WorkPlan plan = F.ProposeWorkPlan(Calc(days, tasks: tasks));
            Assert.AreEqual(F.ConflictReason.PostponementLimit, plan.Conflicts.Single().Reason);
            Assert.AreEqual(Date(16), plan.Conflicts.Single().EarliestCompletionDate);
        }

        [TestMethod]
        public void MissingEstimateAndOutsideHorizonAreConflicts()
        {
            var tasks = new[]
            {
                new F.TaskInput("unknown", dueDate: Date(7)),
                new F.TaskInput("huge", estimatedMinutes: 10000, dueDate: Date(7)),
            };
            F.WorkPlan plan = F.ProposeWorkPlan(Calc(new[] { Day(7) }, tasks: tasks));
            Assert.AreEqual(F.ConflictReason.MissingEstimate, plan.Conflicts.Single(c => c.ItemId == "unknown").Reason);
            Assert.AreEqual(F.ConflictReason.OutsideHorizon, plan.Conflicts.Single(c => c.ItemId == "huge").Reason);
        }

        [TestMethod]
        public void TasksAreHeldToTheirProjectsEndDate()
        {
            var project = new F.ProjectInput("p", 2, Date(8));
            var tasks = new[] { new F.TaskInput("t", "p", 60, outcome: 2, dueDate: Date(7)) };
            F.WorkPlan plan = F.ProposeWorkPlan(Calc(new[] { Day(7, 0), Day(8, 0), Day(9) }, new[] { project }, tasks));
            Assert.AreEqual(F.ConflictReason.ProjectEndDate, plan.Conflicts.Single(c => c.Kind == F.ChangeKind.Task).Reason);
        }

        [TestMethod]
        public void NoProposalLandsOnALockedDay()
        {
            var locked = Day(7); locked.IsLocked = true;
            var tasks = new[] { new F.TaskInput("t", estimatedMinutes: 30, outcome: 2, dueDate: Date(9), notBefore: Date(7)) };
            F.WorkPlan plan = F.ProposeWorkPlan(Calc(new[] { locked, Day(8), Day(9) }, tasks: tasks));
            Assert.AreEqual(0, plan.Changes.Count, "work already planned on a locked day is not re-dated to it");
        }

        [TestMethod]
        public void ATypedDateOverridesAProposalAndAnswersAConflict()
        {
            var tasks = new[]
            {
                new F.TaskInput("t", estimatedMinutes: 60, outcome: 2, dueDate: Date(6)),
                new F.TaskInput("u", estimatedMinutes: 60, outcome: 8, dueDate: Date(7)),
            };
            var days = new List<F.DayInput> { Day(7, 60) };
            for (int d = 8; d <= 20; d++) days.Add(Day(d, d < 18 ? 0 : 600));
            F.WorkPlan plan = F.ProposeWorkPlan(Calc(days, tasks: tasks));
            var typed = new Dictionary<string, DateTime> { { "task:t", Date(12, 15) }, { "task:u", Date(19) } };
            F.WorkPlan applied = plan.ApplyingManualDates(typed)
                .AdoptingManualDates(typed, (kind, id) => id == "u" ? Date(7) : Date(6));
            Assert.AreEqual(Date(12), applied.Changes.Single(c => c.ItemId == "t").ProposedDate, "a typed date is normalised to its day");
            Assert.AreEqual(Date(19), applied.Changes.Single(c => c.ItemId == "u").ProposedDate, "the refused row became a change");
            Assert.IsFalse(applied.Conflicts.Any(c => c.ItemId == "u"));
        }

        [TestMethod]
        public void TodayAndTomorrowAreLockedByDefault()
        {
            Assert.IsTrue(F.IsPlanLocked(Date(7, 15), Start, true));
            Assert.IsTrue(F.IsPlanLocked(Date(8), Start, true));
            Assert.IsFalse(F.IsPlanLocked(Date(9), Start, true));
            Assert.IsFalse(F.IsPlanLocked(Date(7), Start, false));
            Assert.IsFalse(F.IsPlanLocked(Date(6), Start, true));
        }

        // MARK: - Suggestion / grouping / day focus / repositioning

        [TestMethod]
        public void SuggestionFollowsThePosRule()
        {
            Assert.IsNull(ReprioritiseSuggestion.For(4, 119, null, Start));
            Assert.AreEqual(ReprioritiseSuggestion.ReasonKind.Overdue, ReprioritiseSuggestion.For(5, 0, null, Start).Value.Kind);
            Assert.AreEqual(ReprioritiseSuggestion.ReasonKind.DayOverCapacity, ReprioritiseSuggestion.For(0, 120, null, Start).Value.Kind);
            Assert.IsNull(ReprioritiseSuggestion.For(9, 500, Start.AddDays(-6), Start), "dismissed within the week");
            Assert.IsNotNull(ReprioritiseSuggestion.For(9, 500, Start.AddDays(-7), Start), "a week later it may speak again");
        }

        [TestMethod]
        public void WorstDayLooksOnlyAtTheNextFiveDays()
        {
            var tasks = new[] { new F.TaskInput("far", estimatedMinutes: 300, notBefore: Date(13)) };
            var days = new List<F.DayInput>();
            for (int d = 7; d <= 13; d++) days.Add(Day(d, d == 13 ? 0 : 600));
            F.Result r = Calc(days, tasks: tasks);
            Assert.AreEqual(0, ReprioritiseSuggestion.WorstDayOvercommitMinutes(r, Date(7)), 0.001, "the 13th is outside the window");
            Assert.AreEqual(300, ReprioritiseSuggestion.WorstDayOvercommitMinutes(r, Date(9)), 0.001);
        }

        [TestMethod]
        public void MajorIsFiveDaysOrMoreAndNoPreviousDate()
        {
            Assert.IsTrue(ForecastGrouping.IsMajor(new F.DueDateChange(F.ChangeKind.Task, "a", Date(7), Date(12))));
            Assert.IsFalse(ForecastGrouping.IsMajor(new F.DueDateChange(F.ChangeKind.Task, "a", Date(7), Date(11))));
            Assert.IsTrue(ForecastGrouping.IsMajor(new F.DueDateChange(F.ChangeKind.Task, "a", Date(12), Date(7))), "earlier counts too");
            Assert.IsTrue(ForecastGrouping.IsMajor(new F.DueDateChange(F.ChangeKind.Task, "a", null, Date(7))));
        }

        [TestMethod]
        public void GroupsKeepFirstSeenOrderAndBulkSelectSkipsProjects()
        {
            var changes = new[]
            {
                new F.DueDateChange(F.ChangeKind.Task, "t1", Date(7), Date(9)),
                new F.DueDateChange(F.ChangeKind.Task, "t2", Date(7), Date(9)),
                new F.DueDateChange(F.ChangeKind.Project, "p2", Date(7), Date(9)),
                new F.DueDateChange(F.ChangeKind.Task, "t3", Date(7), Date(9)),
            };
            var project = new Dictionary<string, string> { { "t1", "p2" }, { "t2", null }, { "t3", "p2" } };
            var groups = ForecastGrouping.Groups(changes, id => project[id]);
            CollectionAssert.AreEqual(new[] { "p2", null }, groups.Select(g => g.ProjectId).ToList());
            Assert.AreEqual(3, groups[0].Rows.Count);
            var bulk = ForecastGrouping.BulkSelectableIds(changes, c => true);
            CollectionAssert.AreEqual(new[] { "task:t1", "task:t2", "task:t3" }, bulk);
            var some = new HashSet<string> { "task:t1" };
            Assert.AreEqual(ForecastGrouping.SelectionState.Some, ForecastGrouping.State(bulk, some));
            Assert.AreEqual(3, ForecastGrouping.Toggled(bulk, some).Count);
            Assert.AreEqual(0, ForecastGrouping.Toggled(bulk, new HashSet<string>(bulk)).Count);
        }

        [TestMethod]
        public void DayFocusCountsOnlyWorkMovedOffThatDay()
        {
            var changes = new[]
            {
                new F.DueDateChange(F.ChangeKind.Task, "off", Date(7), Date(9)),
                new F.DueDateChange(F.ChangeKind.Task, "elsewhere", Date(8), Date(9)),
            };
            var planned = new Dictionary<string, DateTime?> { { "off", Date(7) }, { "elsewhere", Date(8) } };
            var moving = ForecastDayFocus.ChangesMovingWorkOff(Date(7), changes, id => planned[id]);
            Assert.AreEqual("off", moving.Single().ItemId);
            var relief = ForecastDayFocus.ReliefFor(moving, new ForecastDayFocus.Target(Date(7), 45), id => 30, new[] { 15.0 });
            Assert.AreEqual(15, relief.RemainingMinutes, 0.001);
            Assert.IsTrue(relief.RemainderIsPinned);
        }

        [TestMethod]
        public void RepositioningFollowsTheNewDateButNeverOntoALockedDayOrThePast()
        {
            Assert.AreEqual(Date(10), DayPlanRepositioning.For("t", Date(8), Date(10), Date(7), new[] { Date(7), Date(8) }).Value.To);
            Assert.IsNull(DayPlanRepositioning.For("t", Date(9), Date(8), Date(7), new[] { Date(7), Date(8) }));
            Assert.IsNull(DayPlanRepositioning.For("t", Date(5), Date(9), Date(7), null));
            Assert.IsNull(DayPlanRepositioning.For("t", null, Date(9), Date(7), null));
        }
    }
}
