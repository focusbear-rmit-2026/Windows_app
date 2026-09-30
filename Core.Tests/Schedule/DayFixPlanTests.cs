using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Schedule
{
    /// <summary>
    /// Ported from <c>DayFixPlanTests.swift</c> (Mac report 2026-09-17_08-15-57): "Fix" — the modal
    /// behind the Day Plan's red over-capacity warning. Which routes it offers for a given day, in
    /// what order, which habits the routine route may touch, and what each meeting is allowed to do.
    /// </summary>
    [TestClass]
    public class DayFixPlanTests
    {
        private static readonly DateTimeOffset Day = new DateTimeOffset(2026, 9, 16, 0, 0, 0, TimeSpan.Zero);

        private static DateTimeOffset At(int hour)
        {
            return Day.AddHours(hour);
        }

        private sealed class Row
        {
            internal Row(int seconds, string name)
            {
                Seconds = seconds;
                Name = name;
            }

            internal int Seconds { get; }
            internal string Name { get; }
        }

        private sealed class Habit
        {
            internal Habit(string name, DateTimeOffset start, string routine)
            {
                Name = name;
                Start = start;
                Routine = routine;
            }

            internal string Name { get; }
            internal DateTimeOffset Start { get; }
            internal string Routine { get; }
        }

        private const string MorningRoutine = "Morning routine habits";
        private const string EveningRoutine = "Evening routine habits";

        private static IList<Habit> Offered(IEnumerable<Habit> habits, DateTimeOffset? eveningStart)
        {
            return DayFixPlan.HabitsInsideWorkingDay(habits, habit => habit.Start,
                                                     habit => habit.Routine == EveningRoutine, eveningStart);
        }

        private static DayFixRoute[] Routes(IEnumerable<DayFixOption> options)
        {
            return options.Select(option => option.Route).ToArray();
        }

        // MARK: - The routes offered on an over-committed day

        /// <summary>
        /// Ordered by capacity freed, largest first. Three equal buttons would make the user do the
        /// arithmetic the app has already done.
        /// </summary>
        [TestMethod]
        public void FixRoutesAreOrderedByHowMuchRoomEachCouldFree()
        {
            IList<DayFixOption> options = DayFixPlan.Options(45, 20, 180);
            CollectionAssert.AreEqual(new[] { DayFixRoute.Tasks, DayFixRoute.Meetings, DayFixRoute.Routine },
                                      Routes(options),
                                      "180 minutes of tasks is the answer before 45 of meetings and 20 of habits");
            Assert.AreEqual(180d, options[0].FreeableMinutes);
        }

        /// <summary>
        /// A route that cannot free a minute is a dead button, and every row in this modal is supposed
        /// to move the number at the top.
        /// </summary>
        [TestMethod]
        public void ARouteWithNothingBehindItIsNotOfferedAtAll()
        {
            Assert.AreEqual(0, DayFixPlan.Options(0, 0, 0).Count);
            CollectionAssert.AreEqual(new[] { DayFixRoute.Tasks }, Routes(DayFixPlan.Options(0, 0, 90)),
                                      "Only what can actually help");
            CollectionAssert.AreEqual(new[] { DayFixRoute.Meetings }, Routes(DayFixPlan.Options(30, 0, 0)),
                                      "A day whose only slack is its meetings gets the meetings route");
        }

        [TestMethod]
        public void EqualRoutesKeepTheOrderTheReportListedThemIn()
        {
            CollectionAssert.AreEqual(
                new[] { DayFixRoute.Meetings, DayFixRoute.Routine, DayFixRoute.Tasks },
                Routes(DayFixPlan.Options(60, 60, 60)),
                "A tie is broken by the report's own order, not arbitrarily");
        }

        /// <summary>A negative figure is not slack either — only a positive one is offered.</summary>
        [TestMethod]
        public void ANegativeFigureIsNotAnOfferableRoute()
        {
            CollectionAssert.AreEqual(new[] { DayFixRoute.Tasks }, Routes(DayFixPlan.Options(-30, 0, 15)));
        }

        // MARK: - The lists inside a route

        /// <summary>"habits sorted by duration descending" — that is where the minutes are.</summary>
        [TestMethod]
        public void TheRoutineListPutsTheLongestHabitFirst()
        {
            IList<Row> sorted = DayFixPlan.LongestFirst(
                new[] { new Row(300, "Stretch"), new Row(1800, "Meditate"), new Row(600, "Journal") },
                row => row.Seconds, row => row.Name);

            CollectionAssert.AreEqual(new[] { "Meditate", "Journal", "Stretch" },
                                      sorted.Select(row => row.Name).ToArray());
        }

        [TestMethod]
        public void HabitsOfEqualLengthDoNotReshuffleBetweenRenders()
        {
            var rows = new[] { new Row(600, "Zebra"), new Row(600, "Apple") };

            string[] once = DayFixPlan.LongestFirst(rows, row => row.Seconds, row => row.Name)
                .Select(row => row.Name).ToArray();
            string[] twice = DayFixPlan.LongestFirst(rows.Reverse(), row => row.Seconds, row => row.Name)
                .Select(row => row.Name).ToArray();

            CollectionAssert.AreEqual(new[] { "Apple", "Zebra" }, once);
            CollectionAssert.AreEqual(once, twice, "Same habits, same order, however they arrive");
        }

        /// <summary>
        /// The tie-break is Swift's, so the Mac and Windows list an accented title in the same place
        /// whichever encoding it arrived in. Ordinal put the decomposed "Étirer" before "Fold" and the
        /// precomposed one after it.
        /// </summary>
        [TestMethod]
        public void AnAccentedTitleTieBreaksAsItDoesOnTheMac()
        {
            var rows = new[] { new Row(600, "Fold"), new Row(600, "E\u0301tirer"), new Row(600, "\u00C9crire") };

            string[] sorted = DayFixPlan.LongestFirst(rows, row => row.Seconds, row => row.Name)
                .Select(row => row.Name).ToArray();

            CollectionAssert.AreEqual(new[] { "Fold", "\u00C9crire", "E\u0301tirer" }, sorted);
        }

        // MARK: - Which habits the routine route may offer

        /// <summary>
        /// "Evening routine isn't relevant as we assume all tasks get completed before evening routine
        /// — just offer to shorten morning routine" (report follow-up). The modal used to list the
        /// whole day's habits, so it claimed hours of habits on a day whose morning routine was minutes.
        /// </summary>
        [TestMethod]
        public void TheEveningRoutineIsNotOfferedAsAWayToFitTodaysTasks()
        {
            IList<Habit> offered = Offered(
                new[]
                {
                    new Habit("Meditate", At(7), MorningRoutine),
                    new Habit("Wind down", At(21), EveningRoutine),
                },
                At(20));

            CollectionAssert.AreEqual(new[] { "Meditate" }, offered.Select(habit => habit.Name).ToArray());
        }

        /// <summary>
        /// The rule is the reason — anything that runs after the tasks were meant to be done frees
        /// nothing for them — so a custom routine parked at the end of the day goes too, while one at
        /// lunchtime, which really does eat into the day's tasks, stays.
        /// </summary>
        [TestMethod]
        public void OnlyHabitsThatRunBeforeTheEveningRoutineCount()
        {
            IList<Habit> offered = Offered(
                new[]
                {
                    new Habit("Lunch reset", At(12), "Midday habits"),
                    new Habit("Tidy desk", At(22), "Night habits"),
                },
                At(20));

            CollectionAssert.AreEqual(new[] { "Lunch reset" }, offered.Select(habit => habit.Name).ToArray());
        }

        /// <summary>
        /// Without a shutdown time there is no evening to be after, so nothing is dropped on a guess —
        /// hiding a habit the user could have shortened is worse than listing one they won't.
        /// </summary>
        [TestMethod]
        public void WithNoEveningRoutineTimeEveryHabitIsStillOffered()
        {
            IList<Habit> offered = Offered(
                new[]
                {
                    new Habit("Meditate", At(7), MorningRoutine),
                    new Habit("Read", At(23), "Night habits"),
                },
                null);

            Assert.AreEqual(2, offered.Count);
        }

        /// <summary>
        /// The evening routine's OWN habits are dropped by name-independent flag, before the time
        /// comparison — so one that somehow starts in the morning is still not offered.
        /// </summary>
        [TestMethod]
        public void AnEveningRoutineHabitIsDroppedWhateverTimeItStarts()
        {
            IList<Habit> offered = Offered(new[] { new Habit("Wind down", At(6), EveningRoutine) }, At(20));
            Assert.AreEqual(0, offered.Count);

            IList<Habit> withoutAnEveningTime =
                Offered(new[] { new Habit("Wind down", At(6), EveningRoutine) }, null);
            Assert.AreEqual(0, withoutAnEveningTime.Count,
                            "The flag is checked before the missing-evening-time fallback");
        }

        /// <summary>A habit starting exactly when the evening routine does is already too late.</summary>
        [TestMethod]
        public void AHabitStartingExactlyAtTheEveningRoutineIsOut()
        {
            Assert.AreEqual(0, Offered(new[] { new Habit("Tidy desk", At(20), "Night habits") }, At(20)).Count);
            Assert.AreEqual(1, Offered(new[] { new Habit("Tidy desk", At(20).AddMinutes(-1), "Night habits") },
                                       At(20)).Count);
        }

        // MARK: - What one meeting is allowed to offer

        /// <summary>
        /// The report's three-way rule: "Cancel (if they own it) / Decline (if there are other
        /// attendees but they don't own it) / Skip (if there are no other attendees)".
        /// </summary>
        [TestMethod]
        public void AMeetingWithNobodyElseOnItIsOnlyEverSkipped()
        {
            foreach (EventOwnership ownership in
                     Enum.GetValues(typeof(EventOwnership)).Cast<EventOwnership>())
            {
                Assert.AreEqual(DayFixMeetingAction.Skip, DayFixPlan.MeetingAction(false, ownership),
                                "There is nobody to notify, whoever owns it");
            }
        }

        [TestMethod]
        public void TheOrganiserOfAMeetingWithAttendeesIsOfferedCancel()
        {
            Assert.AreEqual(DayFixMeetingAction.Cancel,
                            DayFixPlan.MeetingAction(true, EventOwnership.Owned));
        }

        [TestMethod]
        public void AnAttendeeIsOfferedDeclineRatherThanCancel()
        {
            Assert.AreEqual(DayFixMeetingAction.Decline,
                            DayFixPlan.MeetingAction(true, EventOwnership.NotOwned));
        }

        /// <summary>
        /// Cancelling takes the meeting off everyone else's calendar, so it must never happen on a
        /// guess — the same rule the event editor's destructive button follows, stated in one place.
        /// </summary>
        [TestMethod]
        public void UnknownOwnershipNeverOffersToCancelForEveryone()
        {
            Assert.AreEqual(DayFixMeetingAction.Decline,
                            DayFixPlan.MeetingAction(true, EventOwnership.Unknown));
        }
    }
}
