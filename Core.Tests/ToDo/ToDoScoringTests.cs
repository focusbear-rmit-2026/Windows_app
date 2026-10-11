using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>
    /// TOP score (T x O / P), ported case-for-case from the Mac's <c>ToDoLogicTests</c>. The constants
    /// are the web's and the backend's, so a drift here is a drift between clients.
    /// </summary>
    [TestClass]
    public class ToDoScoringTests
    {
        private const double Tolerance = 0.0001;

        private static readonly DateTime Now = new DateTime(2026, 9, 7, 10, 0, 0);

        private static DateTime Day(int offset) => Now.Date.AddDays(offset);

        private static ToDoTask Task(string id, int? dueOffset = null, int? outcome = null,
                                     int? perspiration = null, double? topScore = null,
                                     ToDoStatus status = ToDoStatus.NotStarted)
        {
            return new ToDoTask
            {
                Id = id,
                Title = id,
                Status = ToDoStatusWire.Of(status),
                DueDate = dueOffset.HasValue ? ToDoDates.Ymd(Day(dueOffset.Value)) : null,
                Outcome = outcome,
                PerspirationLevel = perspiration,
                TopScore = topScore,
            };
        }

        [TestMethod]
        public void TimePressureMatchesWebConstants()
        {
            Assert.AreEqual(0.1, ToDoScoring.TimePressure(null, 15, Now), Tolerance);
            Assert.AreEqual(9, ToDoScoring.TimePressure(Day(0), 15, Now), Tolerance);
            Assert.AreEqual(13, ToDoScoring.TimePressure(Day(-3), 15, Now), Tolerance, "an overdue day adds a point each");

            double future = ToDoScoring.TimePressure(Day(10), 60, Now);
            Assert.IsTrue(future > 0.1, "a dated task beats an undated one");
            Assert.IsTrue(future < 9, "but never a task due today");
        }

        [TestMethod]
        public void TopScoreIsTimesOutcomeOverPerspiration()
        {
            // Due today: T = 9, O = 5, P = 2 -> 22.5
            Assert.AreEqual(22.5, ToDoScoring.TopScore(5, 2, Day(0), Now), Tolerance);

            // Perspiration is clamped into 1...10.
            Assert.AreEqual(45, ToDoScoring.TopScore(5, 0, Day(0), Now), Tolerance);
        }

        [TestMethod]
        public void PerspirationForMinutesIsTheInverseOfTheWorkTable()
        {
            // The lowest level whose minutes cover the typed value; beyond the table the top level.
            Assert.AreEqual(1, ToDoScoring.PerspirationForMinutes(5));
            Assert.AreEqual(2, ToDoScoring.PerspirationForMinutes(15));
            Assert.AreEqual(3, ToDoScoring.PerspirationForMinutes(16));
            Assert.AreEqual(4, ToDoScoring.PerspirationForMinutes(45));
            Assert.AreEqual(5, ToDoScoring.PerspirationForMinutes(240));
            Assert.AreEqual(10, ToDoScoring.PerspirationForMinutes(10000));

            // A level that already matches the minutes is kept, so the slider does not snap 6 -> 5 / 8 -> 7.
            Assert.AreEqual(6, ToDoScoring.PerspirationForMinutes(240, 6));
            Assert.AreEqual(8, ToDoScoring.PerspirationForMinutes(480, 8));
            Assert.AreEqual(7, ToDoScoring.PerspirationForMinutes(480, 2));

            for (int level = ToDoScoring.MinLevel; level <= ToDoScoring.MaxLevel; level++)
            {
                Assert.AreEqual(level, ToDoScoring.PerspirationForMinutes(ToDoScoring.MinutesForPerspiration(level), level));
            }
        }

        /// <summary>
        /// Due 21 days ago, stored 0.2 while it was still weeks away: the server never re-scores, so
        /// the list must show the live overdue value the editor shows (T = 10 + 21, O = 6, P = 3 -> 62).
        /// </summary>
        [TestMethod]
        public void StaleStoredScoreLosesToLocalFormula()
        {
            ToDoTask overdue = Task("a", dueOffset: -21, outcome: 6, perspiration: 3, topScore: 0.2);

            Assert.AreEqual(62, ToDoScoring.TopScoreOf(overdue, Now), Tolerance);
            Assert.AreEqual(ToDoScoring.TopScore(6, 3, Day(-21), Now), ToDoScoring.TopScoreOf(overdue, Now), Tolerance,
                            "list and editor agree");
            Assert.AreEqual(22.5, ToDoScoring.TopScoreOf(Task("b", dueOffset: 0, outcome: 5, perspiration: 2), Now), Tolerance);

            // Only a row with none of the inputs falls back to what the server stored.
            Assert.AreEqual(7, ToDoScoring.TopScoreOf(Task("c", topScore: 7), Now), Tolerance);
            Assert.AreEqual(ToDoScoring.TopScore(1, 1, Day(3), Now),
                            ToDoScoring.TopScoreOf(Task("d", dueOffset: 3, topScore: 7), Now), Tolerance);
        }

        [TestMethod]
        public void SmartSortUsesLiveScores()
        {
            // Stored scores say "later" wins; the live formula says the overdue one does.
            IList<ToDoTask> sorted = ToDoScoring.SortBySmart(new[]
            {
                Task("later", dueOffset: 10, outcome: 5, perspiration: 2, topScore: 50),
                Task("overdue", dueOffset: -2, outcome: 5, perspiration: 2, topScore: 0.5),
            }, Now);

            CollectionAssert.AreEqual(new[] { "overdue", "later" }, sorted.Select(task => task.Id).ToArray());
        }

        /// <summary>
        /// Web `getTOPScore`: the server keeps the score a task had on completion, so an overdue
        /// "TOP 115" would outrank every open task forever.
        /// </summary>
        [TestMethod]
        public void CompletedTasksScoreZeroSoTheySortLast()
        {
            ToDoTask done = Task("done", dueOffset: -30, outcome: 10, perspiration: 1,
                                 topScore: 115, status: ToDoStatus.Completed);

            Assert.AreEqual(ToDoScoring.CompletedScore, ToDoScoring.TopScoreOf(done, Now), Tolerance);

            IList<ToDoTask> sorted = ToDoScoring.SortBySmart(
                new[] { done, Task("open", dueOffset: 5, outcome: 1, perspiration: 5) }, Now);
            CollectionAssert.AreEqual(new[] { "open", "done" }, sorted.Select(task => task.Id).ToArray());
        }

        [TestMethod]
        public void DurationFallsBackToPerspirationMinutes()
        {
            Assert.AreEqual(600, ToDoScoring.DurationSeconds(600, 2));
            Assert.AreEqual(15 * 60, ToDoScoring.DurationSeconds(0, 2));
            Assert.AreEqual(3360 * 60, ToDoScoring.DurationSeconds(null, 9));
        }

        [TestMethod]
        public void RoundedDropsATrailingZeroDecimal()
        {
            Assert.AreEqual("22.5", ToDoScoring.Rounded(22.5));
            Assert.AreEqual("62", ToDoScoring.Rounded(62.0));
            Assert.AreEqual("0.1", ToDoScoring.Rounded(0.1));
            Assert.AreEqual("9", ToDoScoring.Rounded(8.96));
        }

        /// <summary>
        /// Ties keep the order they arrived in, so a re-sort of an unchanged list never reshuffles the
        /// rows under the user.
        /// </summary>
        [TestMethod]
        public void SmartSortIsStableAcrossEqualScores()
        {
            IList<ToDoTask> sorted = ToDoScoring.SortBySmart(new[]
            {
                Task("first", dueOffset: 0, outcome: 5, perspiration: 2),
                Task("second", dueOffset: 0, outcome: 5, perspiration: 2),
            }, Now);

            CollectionAssert.AreEqual(new[] { "first", "second" }, sorted.Select(task => task.Id).ToArray());
        }
    }
}
