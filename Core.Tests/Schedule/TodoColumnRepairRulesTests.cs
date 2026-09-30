using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Schedule
{
    /// <summary>
    /// Ported from the Mac app's <c>DayPlanProjectRepairTests.swift</c>, "one-off repair of
    /// already-completed to-dos" (report 2026-09-15_17-41-45): a to-do the backend calls COMPLETED that
    /// is parked in an open kanban column reads as outstanding to every other client — and to the
    /// server's own <c>exclude_completed</c>.
    /// </summary>
    [TestClass]
    public class TodoColumnRepairRulesTests
    {
        private static IReadOnlyList<ProjectCustomStatus> Columns(bool done = true)
        {
            return new List<ProjectCustomStatus>
            {
                new ProjectCustomStatus("todo", "To Do", 0, false),
                new ProjectCustomStatus("doing", "In Progress", 1, false),
                new ProjectCustomStatus("done", "Done", 2, done),
            };
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<ProjectCustomStatus>> Board(
            params KeyValuePair<string, IReadOnlyList<ProjectCustomStatus>>[] projects)
        {
            return projects.ToDictionary(p => p.Key, p => p.Value);
        }

        private static KeyValuePair<string, IReadOnlyList<ProjectCustomStatus>> Project(
            string id, IReadOnlyList<ProjectCustomStatus> columns)
        {
            return new KeyValuePair<string, IReadOnlyList<ProjectCustomStatus>>(id, columns);
        }

        private static ToDoRow Row(string id, string status, string column, string project)
        {
            return new ToDoRow(id, status, project, column, id);
        }

        /// <summary>The one row shape the sweep exists for: the backend says COMPLETED, but the card is
        /// parked in an open column.</summary>
        [TestMethod]
        public void RepairMovesACompletedTaskOutOfAnOpenColumnIntoTheDoneColumn()
        {
            TodoColumnRepairRules.Plan plan = TodoColumnRepairRules.Build(
                new[] { Row("stuck", ToDoStatus.Completed, "todo", "p") },
                Board(Project("p", Columns())));

            CollectionAssert.AreEqual(
                new List<TodoColumnRepairRules.Repair>
                {
                    new TodoColumnRepairRules.Repair("stuck", "p", "todo", "done"),
                },
                plan.Repairs.ToList());
            Assert.AreEqual(0, plan.Skips.Count);
        }

        /// <summary>
        /// Completion is read from <c>status</c> and never from a column — inferring it the other way is
        /// the bug this whole change removes, and doing it here would complete tasks the user had not.
        /// </summary>
        [TestMethod]
        public void RepairNeverMovesATaskThatIsNotCompleted()
        {
            foreach (string status in new[] { ToDoStatus.NotStarted, ToDoStatus.InProgress })
            {
                TodoColumnRepairRules.Plan plan = TodoColumnRepairRules.Build(
                    new[] { Row("open", status, "todo", "p") },
                    Board(Project("p", Columns())));

                Assert.AreEqual(0, plan.Repairs.Count, status + " must never be re-filed as done");
                CollectionAssert.AreEqual(
                    new List<TodoColumnRepairRules.Skip>
                    {
                        new TodoColumnRepairRules.Skip("open", TodoColumnRepairRules.SkipReason.NotCompleted),
                    },
                    plan.Skips.ToList());
            }
        }

        [TestMethod]
        public void RepairLeavesCorrectlyFiledAndUnreasonableRowsAlone()
        {
            var rows = new[]
            {
                Row("already-done", ToDoStatus.Completed, "done", "p"),
                Row("no-column", ToDoStatus.Completed, null, "p"),
                Row("no-project", ToDoStatus.Completed, "todo", null),
                Row("unloaded-project", ToDoStatus.Completed, "todo", "unknown"),
                Row("unknown-column", ToDoStatus.Completed, "ghost", "p"),
                Row("no-done-column", ToDoStatus.Completed, "todo", "q"),
            };

            TodoColumnRepairRules.Plan plan = TodoColumnRepairRules.Build(
                rows, Board(Project("p", Columns()), Project("q", Columns(done: false))));

            Assert.AreEqual(0, plan.Repairs.Count);
            CollectionAssert.AreEqual(
                new List<TodoColumnRepairRules.Skip>
                {
                    new TodoColumnRepairRules.Skip("already-done", TodoColumnRepairRules.SkipReason.ColumnAlreadyCompletes),
                    new TodoColumnRepairRules.Skip("no-column", TodoColumnRepairRules.SkipReason.NoColumn),
                    new TodoColumnRepairRules.Skip("no-project", TodoColumnRepairRules.SkipReason.NoProject),
                    new TodoColumnRepairRules.Skip("unloaded-project", TodoColumnRepairRules.SkipReason.ProjectNotLoaded),
                    new TodoColumnRepairRules.Skip("unknown-column", TodoColumnRepairRules.SkipReason.ColumnUnknown),
                    new TodoColumnRepairRules.Skip("no-done-column", TodoColumnRepairRules.SkipReason.ProjectHasNoDoneColumn),
                },
                plan.Skips.ToList());
            Assert.AreEqual(
                "columnAlreadyCompletes=1 columnUnknown=1 noColumn=1 noProject=1 projectHasNoDoneColumn=1 projectNotLoaded=1",
                TodoColumnRepairRules.Summary(plan.Skips));
        }

        /// <summary>
        /// Idempotence, proved the way it actually happens: apply the plan, feed the result back in, and
        /// the second pass must want to write nothing.
        /// </summary>
        [TestMethod]
        public void RepairRunTwiceOverItsOwnOutputPlansNoFurtherWrites()
        {
            var board = Board(Project("p", Columns()));
            var rows = new List<ToDoRow>
            {
                Row("a", ToDoStatus.Completed, "todo", "p"),
                Row("b", ToDoStatus.Completed, "doing", "p"),
                Row("c", ToDoStatus.Completed, "done", "p"),
                Row("d", ToDoStatus.NotStarted, "todo", "p"),
            };

            TodoColumnRepairRules.Plan first = TodoColumnRepairRules.Build(rows, board);
            CollectionAssert.AreEqual(new List<string> { "a", "b" }, first.Repairs.Select(r => r.TaskId).ToList());

            // Apply exactly what the sweep would have PUT.
            foreach (TodoColumnRepairRules.Repair repair in first.Repairs)
            {
                rows.Single(r => r.Id == repair.TaskId).CustomStatusId = repair.ToColumnId;
            }

            TodoColumnRepairRules.Plan second = TodoColumnRepairRules.Build(rows, board);
            Assert.AreEqual(0, second.Repairs.Count, "A repaired row must never be rewritten");
            Assert.AreEqual(rows.Count, second.Skips.Count);
        }

        [TestMethod]
        public void TheDoneColumnIsTheFirstCompletingOneInBoardOrder()
        {
            var columns = new List<ProjectCustomStatus>
            {
                new ProjectCustomStatus("archive", "Archive", 9, true),
                new ProjectCustomStatus("todo", "To Do", 0, false),
                new ProjectCustomStatus("done", "Done", 2, true),
            };

            TodoColumnRepairRules.Plan plan = TodoColumnRepairRules.Build(
                new[] { Row("stuck", ToDoStatus.Completed, "todo", "p") }, Board(Project("p", columns)));

            Assert.AreEqual("done", plan.Repairs.Single().ToColumnId, "column order decides, not list order");
        }

        /// <summary>
        /// A <c>custom_status_id</c> the response OMITS and one it returns as null are identical once
        /// decoded. The first pass of this sweep reported <c>noColumn=500</c>, zero repairs and stored
        /// its marker on an account with 47 rows to fix, so the two have to be told apart at the JSON
        /// level before a pass is allowed to call itself complete.
        /// </summary>
        [TestMethod]
        public void AMissingColumnFieldIsCountedApartFromANullOne()
        {
            const string rowA = "{\"id\":\"a\",\"title\":\"A\",\"status\":\"COMPLETED\"";
            const string rowB = "{\"id\":\"b\",\"title\":\"B\",\"status\":\"COMPLETED\"";

            string present = Payload(rowA + ",\"custom_status_id\":\"todo\"}," + rowB + ",\"custom_status_id\":null}");
            Assert.AreEqual(2, TodoColumnRepairRules.RowsCarryingColumnField(present),
                            "An explicit null still proves the server sends the field");

            string absent = Payload(rowA + "}," + rowB + "}");
            Assert.AreEqual(0, TodoColumnRepairRules.RowsCarryingColumnField(absent),
                            "No field at all: the pass is blind and must not record success");

            Assert.AreEqual(0, TodoColumnRepairRules.RowsCarryingColumnField(Payload(string.Empty)));
            Assert.AreEqual(0, TodoColumnRepairRules.RowsCarryingColumnField("not json"));
            Assert.AreEqual(0, TodoColumnRepairRules.RowsCarryingColumnField(null));
        }

        private static string Payload(string rows)
        {
            return "{\"data\":[" + rows + "],\"meta\":{\"page\":1}}";
        }

        [TestMethod]
        public void TheSchemaVersionIsWhatDecidesWhetherAnAccountIsSweptAgain()
        {
            Assert.AreEqual(2, TodoColumnRepairRules.SchemaVersion,
                            "v1 recorded a truncated pass as complete and abandoned 47 rows");
        }
    }
}
