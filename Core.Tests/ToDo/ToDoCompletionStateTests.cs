using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>
    /// "Is this task done", and where a completion should put its card. Ported case-for-case from the
    /// Mac's <c>ToDoLogicTests</c>; the reports named are the ones each rule pins.
    /// </summary>
    [TestClass]
    public class ToDoCompletionStateTests
    {
        private static ToDoTask Task(string id, ToDoStatus status = ToDoStatus.NotStarted, string customStatusId = null)
        {
            return new ToDoTask
            {
                Id = id,
                Title = id,
                Status = ToDoStatusWire.Of(status),
                CustomStatusId = customStatusId,
            };
        }

        private static ProjectCustomStatus Column(string id, string label, int order, bool completes = false) =>
            new ProjectCustomStatus(id, label, order, completes);

        private static readonly ProjectCustomStatus[] ToDoAndDone =
        {
            Column("todo", "To Do", 0),
            Column("done", "Done", 1, completes: true),
        };

        /// <summary>
        /// Mac report 2026-09-15_17-41-45. A task ticked off from the Day Plan, the to-do list, the
        /// routine player or another device keeps whatever open column it was in — <c>status</c> is the
        /// only thing that changes. The board and the sectioned list both draw those as done, so this
        /// rule, which everything else reads completion from, has to agree with them.
        /// </summary>
        [TestMethod]
        public void CompletionReadsStatusFirstSoWorkTickedOffElsewhereStillCountsAsDone()
        {
            var project = new Project("p", "P", ToDoAndDone);
            ISet<string> completing = ToDoCompletionState.CompletingStatusIds(new[] { project });
            CollectionAssert.AreEquivalent(new[] { "done" }, completing.ToArray());

            ToDoTask tickedElsewhere = Task("a", ToDoStatus.Completed, "todo");
            Assert.IsTrue(ToDoCompletionState.IsFinished(tickedElsewhere, ToDoAndDone));
            Assert.IsTrue(ToDoCompletionState.IsFinished(tickedElsewhere, completing));

            Assert.IsTrue(ToDoCompletionState.IsFinished(Task("b", customStatusId: "done"), ToDoAndDone));
            Assert.IsFalse(ToDoCompletionState.IsFinished(Task("c", customStatusId: "todo"), ToDoAndDone));
            Assert.IsFalse(ToDoCompletionState.IsFinished(Task("d"), ToDoAndDone));
            Assert.IsTrue(ToDoCompletionState.IsFinished(Task("e", ToDoStatus.Completed), new ProjectCustomStatus[0]));
            Assert.IsFalse(ToDoCompletionState.IsFinished(Task("f", customStatusId: "done"), new ProjectCustomStatus[0]),
                           "An unknown column is no evidence either way; the status decides");
        }

        /// <summary>
        /// Mac report 2026-09-15_17-41-45, follow-up: "hang on — why are tasks remaining in ToDo when
        /// they were completed?" Completing outside the project board has to move the card too, or the
        /// task keeps the open column it was in and every other client still reads it as outstanding.
        /// </summary>
        [TestMethod]
        public void CompletingOutsideTheBoardMovesTheTaskIntoTheProjectsCompletingColumn()
        {
            ProjectCustomStatus[] columns =
            {
                Column("todo", "To Do", 0),
                Column("review", "Review", 1),
                Column("done", "Done", 2, completes: true),
            };

            Assert.AreEqual(ToDoColumnWrite.MoveTo("done"),
                            ToDoCompletionState.ResolveColumnWrite(true, "todo", columns));
            Assert.AreEqual(ToDoColumnWrite.MoveTo("done"),
                            ToDoCompletionState.ResolveColumnWrite(true, null, columns));
            Assert.AreEqual(ToDoColumnWrite.LeaveAlone,
                            ToDoCompletionState.ResolveColumnWrite(true, "done", columns),
                            "Already there: nothing to write");

            // Several completing columns: the first by order, matching the kanban checkbox.
            ProjectCustomStatus[] twoDone = columns.Concat(new[] { Column("shipped", "Shipped", 3, completes: true) }).ToArray();
            Assert.AreEqual(ToDoColumnWrite.MoveTo("done"),
                            ToDoCompletionState.ResolveColumnWrite(true, "todo", twoDone));
            Assert.AreEqual(ToDoColumnWrite.MoveTo("done"),
                            // Spelled out rather than `twoDone.Reverse()`: under C# 13 that binds to
                            // the span overload, which returns void and reverses in place. LangVersion
                            // is pinned so it cannot happen here, but the explicit call cannot rot.
                            ToDoCompletionState.ResolveColumnWrite(true, "todo", Enumerable.Reverse(twoDone).ToArray()),
                            "Unordered input must not change the answer");
        }

        /// <summary>
        /// A project with no completing column must not end up worse off than before: parking a
        /// finished task in a column the server reads as open is the bug, not the fix.
        /// </summary>
        [TestMethod]
        public void AProjectWithNoCompletingColumnKeepsTheTaskWhereItIs()
        {
            ProjectCustomStatus[] noDone = { Column("todo", "To Do", 0), Column("later", "Later", 1) };

            Assert.AreEqual(ToDoColumnWrite.LeaveAlone, ToDoCompletionState.ResolveColumnWrite(true, "todo", noDone));
            Assert.AreEqual(ToDoColumnWrite.LeaveAlone, ToDoCompletionState.ResolveColumnWrite(false, "later", noDone));

            // No project, or a project without custom columns at all.
            var none = new ProjectCustomStatus[0];
            Assert.AreEqual(ToDoColumnWrite.LeaveAlone, ToDoCompletionState.ResolveColumnWrite(true, null, none));
            Assert.AreEqual(ToDoColumnWrite.LeaveAlone, ToDoCompletionState.ResolveColumnWrite(true, "stale", none));
        }

        /// <summary>
        /// Re-opening has to undo the move, or the task sits in "Done" with a NOT_STARTED status — the
        /// same inconsistency mirrored, and one the server would read as finished.
        /// </summary>
        [TestMethod]
        public void ReopeningMovesBackOutOfADoneColumnButNeverOutOfAnOpenOne()
        {
            ProjectCustomStatus[] columns =
            {
                Column("todo", "To Do", 0),
                Column("doing", "In Progress", 1),
                Column("done", "Done", 2, completes: true),
            };

            Assert.AreEqual(ToDoColumnWrite.MoveTo("todo"), ToDoCompletionState.ResolveColumnWrite(false, "done", columns));
            Assert.AreEqual(ToDoColumnWrite.LeaveAlone, ToDoCompletionState.ResolveColumnWrite(false, "doing", columns),
                            "An un-tick must not drag a task out of In Progress");
            Assert.AreEqual(ToDoColumnWrite.LeaveAlone, ToDoCompletionState.ResolveColumnWrite(false, "todo", columns));
            Assert.AreEqual(ToDoColumnWrite.LeaveAlone, ToDoCompletionState.ResolveColumnWrite(false, null, columns));
        }

        // MARK: The checkbox

        [TestMethod]
        public void StatusToggleUsesProjectDoneColumn()
        {
            ToDoTask completed = ToDoStatusToggle.Apply(Task("a", customStatusId: "todo"), true, ToDoAndDone);
            Assert.AreEqual(ToDoStatusWire.Completed, completed.Status);
            Assert.AreEqual("done", completed.CustomStatusId);

            ToDoTask reopened = ToDoStatusToggle.Apply(completed, false, ToDoAndDone);
            Assert.AreEqual(ToDoStatusWire.NotStarted, reopened.Status);
            Assert.AreEqual("todo", reopened.CustomStatusId);

            ToDoTask plain = ToDoStatusToggle.Apply(Task("b"), true, new ProjectCustomStatus[0]);
            Assert.AreEqual(ToDoStatusWire.Completed, plain.Status);
            Assert.IsNull(plain.CustomStatusId);

            // Web parity: a task without a column id still moves into the project's done column.
            ToDoTask legacy = ToDoStatusToggle.Apply(Task("c"), true, ToDoAndDone);
            Assert.AreEqual("done", legacy.CustomStatusId);
        }

        /// <summary>
        /// The deliberately surprising half. A project with NO completing column keeps the task where
        /// it is rather than taking web's "else the last column" fallback: that column is one the
        /// server reads as open, so writing it would mark a finished task outstanding
        /// (Mac report 2026-09-15_17-41-45). Nothing is lost, because the board still RENDERS a
        /// COMPLETED task under the done column — which is what the last assertion here proves.
        /// </summary>
        [TestMethod]
        public void CompletingWithoutADoneColumnLeavesTheColumnAloneAndTheCardStillReadsAsDone()
        {
            ProjectCustomStatus[] noDone = ToDoAndDone
                .Select(status => new ProjectCustomStatus(status.Id, status.Label, status.Order))
                .ToArray();

            ToDoTask completed = ToDoStatusToggle.Apply(Task("d", customStatusId: "todo"), true, noDone);
            Assert.AreEqual("todo", completed.CustomStatusId, "the open column is kept rather than a wrong one written");
            Assert.AreEqual(ToDoStatusWire.Completed, completed.Status, "status is the only signal, and it is read correctly");

            var project = new Project("p", "P", noDone);
            IList<ProjectColumn> columns = ProjectKanban.Columns(project, null);
            Assert.AreEqual("done", ProjectKanban.ColumnId(completed, columns, true),
                            "Still rendered under the done column, so the card does not move either way");

            Assert.IsNull(ToDoStatusToggle.Apply(Task("e"), true, noDone).CustomStatusId);
            Assert.AreEqual("todo",
                            ToDoStatusToggle.Apply(Task("d", ToDoStatus.Completed, "done"), false, noDone).CustomStatusId);
        }

        /// <summary>
        /// Completing is NOT the mirror of re-opening: completing never falls back to the project's
        /// last column, while re-opening always goes to the first. The two paths are pinned together
        /// here so a later "tidy-up" cannot make them symmetric again.
        /// </summary>
        [TestMethod]
        public void CompletingHasNoLastColumnFallbackWhileReopeningAlwaysTakesTheFirst()
        {
            ProjectCustomStatus[] noDone = { Column("todo", "To Do", 0), Column("later", "Later", 1) };

            Assert.AreEqual("todo", ToDoStatusToggle.Apply(Task("a", customStatusId: "todo"), true, noDone).CustomStatusId);
            Assert.AreEqual("later", ToDoStatusToggle.Apply(Task("b", customStatusId: "later"), true, noDone).CustomStatusId,
                            "completing writes no column of its own here");
            Assert.AreEqual("todo", ToDoStatusToggle.Apply(Task("c", customStatusId: "later"), false, noDone).CustomStatusId,
                            "re-opening always lands in the first column");
        }

        [TestMethod]
        public void ApplyDoesNotMutateTheTaskItWasGiven()
        {
            ToDoTask original = Task("a", customStatusId: "todo");

            ToDoStatusToggle.Apply(original, true, ToDoAndDone);

            Assert.AreEqual(ToDoStatusWire.NotStarted, original.Status, "the Mac's struct semantics are kept by copying");
            Assert.AreEqual("todo", original.CustomStatusId);
        }

        /// <summary>
        /// Columns arrive from the API in no particular order, and both rules sort by <c>order</c>. A
        /// column with none sorts as if it were first, the same way on both paths.
        /// </summary>
        [TestMethod]
        public void BoardOrderComesFromTheOrderFieldNotTheArrayOrder()
        {
            ProjectCustomStatus[] shuffled =
            {
                Column("done", "Done", 2, completes: true),
                Column("todo", "To Do", 0),
                Column("doing", "Doing", 1),
            };

            Assert.AreEqual("done", ToDoStatusToggle.Apply(Task("a"), true, shuffled).CustomStatusId);
            Assert.AreEqual("todo", ToDoStatusToggle.Apply(Task("b"), false, shuffled).CustomStatusId);
            Assert.AreEqual(ToDoColumnWrite.MoveTo("todo"), ToDoCompletionState.ResolveColumnWrite(false, "done", shuffled));
        }

        [TestMethod]
        public void TickingATaskWhoseProjectIsNotInHand_KeepsItsColumn()
        {
            // The list's catalogue is active projects only and loads alongside the tasks; an unknown board used
            // to count as one with no columns, and the tick cleared custom_status_id.
            ToDoTask task = Task("a", customStatusId: "writing");
            task.ProjectId = "p-archived";

            ToDoTask ticked = ToDoStatusToggle.ApplyWithOwner(task, true, null);

            Assert.AreEqual(ToDoStatusWire.Completed, ticked.Status);
            Assert.AreEqual("writing", ticked.CustomStatusId);
            Assert.AreEqual(ToDoStatusWire.NotStarted, ToDoStatusToggle.ApplyWithOwner(ticked, false, null).Status);
            Assert.AreEqual("writing", task.CustomStatusId, "the task read is not mutated");
        }

        [TestMethod]
        public void TickingWithTheProjectInHand_IsTheBoardRule()
        {
            var board = new Project("p1", "Board", ToDoAndDone);
            ToDoTask task = Task("a", customStatusId: "todo");
            task.ProjectId = "p1";

            Assert.AreEqual(ToDoStatusToggle.Apply(task, true, ToDoAndDone).CustomStatusId,
                            ToDoStatusToggle.ApplyWithOwner(task, true, board).CustomStatusId);
            Assert.IsNull(ToDoStatusToggle.ApplyWithOwner(task, true, new Project("p1", "Plain")).CustomStatusId, "a known board with no columns drops it");
            Assert.IsNull(ToDoStatusToggle.ApplyWithOwner(Task("b", customStatusId: "stray"), true, null).CustomStatusId, "no project at all, no column");
        }
    }
}
