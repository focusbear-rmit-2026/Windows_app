using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Forecast;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Forecast
{
    /// <summary>The forecast's inputs from the server catalogue (the Mac's LocalForecastSnapshot.projectTasks).</summary>
    [TestClass]
    public class ForecastSnapshotTests
    {
        private static readonly DateTime Today = new DateTime(2026, 9, 25);

        private static readonly Project Book = new Project("p-book", "Book", new[]
        {
            new ProjectCustomStatus("col-done", "Done", 2, true),
            new ProjectCustomStatus("col-doing", "Doing", 1, false),
        }) { Outcome = 8, DueDate = "2026-10-31", TaskCount = 4, UncompletedTaskCount = 3 };

        private static ToDoTask Task(string id, string due = null, int? seconds = null, string project = "p-book",
                                     string column = null, string status = "IN_PROGRESS", int? perspiration = null)
        {
            return new ToDoTask
            {
                Id = id, Title = id, DueDate = due, Duration = seconds, ProjectId = project, CustomStatusId = column,
                Status = status, PerspirationLevel = perspiration,
            };
        }

        [TestMethod]
        public void ProjectsReadUnratedAsZero()
        {
            var inputs = ForecastSnapshot.Projects(new[] { Book, new Project("p2", "Two") });
            Assert.AreEqual(8, inputs[0].Outcome);
            Assert.AreEqual(new DateTime(2026, 10, 31), inputs[0].DueDate);
            Assert.AreEqual(0, inputs[1].Outcome);
            Assert.IsFalse(inputs[1].HasRating);
        }

        [TestMethod]
        public void OverdueWorkRollsOntoTodayAndKeepsItsRealDueDate()
        {
            var t = ForecastSnapshot.ProjectTasks(new[] { Task("a", "2026-09-20", 1800) }, new[] { Book }, null, Today).Single();
            Assert.AreEqual(new DateTime(2026, 9, 20), t.DueDate);
            Assert.AreEqual(Today, t.NotBefore);
            Assert.AreEqual(30, t.EstimatedMinutes);
        }

        [TestMethod]
        public void TasksOutsideAKnownProjectAndFinishedRowsAreDropped()
        {
            var rows = new[]
            {
                Task("keep", "2026-09-26", 600),
                Task("noproject", "2026-09-26", 600, project: null),
                Task("done-column", "2026-09-26", 600, column: "col-done"),
                Task("done-status", "2026-09-26", 600, status: "COMPLETED"),
                Task("keep", "2026-09-26", 600),
            };
            var ids = ForecastSnapshot.ProjectTasks(rows, new[] { Book }, null, Today).Select(t => t.Id).ToList();
            CollectionAssert.AreEqual(new[] { "keep" }, ids);
        }

        [TestMethod]
        public void APlacedCopyPricesTheTaskAndPinsItToItsBlockOnTheDueDay()
        {
            var planned = new[]
            {
                new ForecastSnapshot.PlannedCopy("a", Today.AddDays(1), 45, "habit:🧘 Yoga"),
                new ForecastSnapshot.PlannedCopy("b", Today.AddDays(3), 20, "habit:🧘 Yoga"),
            };
            var tasks = ForecastSnapshot.ProjectTasks(new[] { Task("a", "2026-09-26", 1800), Task("b", "2026-09-26") },
                                                      new[] { Book }, planned, Today);
            var a = tasks.Single(t => t.Id == "a");
            Assert.AreEqual(45, a.EstimatedMinutes, "the Day Plan's duration wins");
            Assert.AreEqual("habit:🧘 Yoga", a.BlockAssignment.Value.BlockKey);
            var b = tasks.Single(t => t.Id == "b");
            Assert.IsNull(b.BlockAssignment, "a placement on another day than the due day pins nothing");
            Assert.AreEqual(20, b.EstimatedMinutes);
        }

        [TestMethod]
        public void NoDurationFallsBackToPerspirationThenUnknown()
        {
            var tasks = ForecastSnapshot.ProjectTasks(new[] { Task("p", perspiration: 3), Task("none") }, new[] { Book }, null, Today);
            Assert.AreEqual(ToDoScoring.MinutesForPerspiration(3), tasks.Single(t => t.Id == "p").EstimatedMinutes);
            Assert.IsNull(tasks.Single(t => t.Id == "none").EstimatedMinutes);
        }

        [TestMethod]
        public void OverdueCountUsesTheListsOwnRule()
        {
            var rows = new[] { Task("a", "2026-09-24"), Task("b", "2026-09-25"), Task("c", "2026-09-01", status: "COMPLETED"), Task("d") };
            Assert.AreEqual(1, ForecastSnapshot.OverdueCount(rows, Today.AddHours(9)));
        }

        [TestMethod]
        public void WorkAssignedToSomebodyElseIsLeftOut()
        {
            // GET /to-do sends the assignee as a relation, with no flat assignee_id.
            var theirs = Task("theirs", "2026-09-20", 1800); theirs.Assignee = new ToDoAssignee("u-sam", "sam");
            var flatTheirs = Task("flat", "2026-09-20", 1800); flatTheirs.AssigneeId = "u-sam";
            var mine = Task("mine", "2026-09-20", 1800); mine.Assignee = new ToDoAssignee("u-me", "me");
            var nobodys = Task("nobody", "2026-09-20", 1800);
            var rows = new[] { theirs, flatTheirs, mine, nobodys };
            var ids = ForecastSnapshot.ProjectTasks(rows, new[] { Book }, null, Today, "u-me").Select(t => t.Id).ToList();
            CollectionAssert.AreEqual(new[] { "mine", "nobody" }, ids);
            Assert.AreEqual(2, ForecastSnapshot.OverdueCount(rows, Today, "u-me"));
            // Signed out: nothing is guessed away.
            Assert.AreEqual(4, ForecastSnapshot.ProjectTasks(rows, new[] { Book }, null, Today).Count);
        }
    }
}
