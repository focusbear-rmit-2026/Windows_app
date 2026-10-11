using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>
    /// Ported from <c>ProjectKanbanDragTests.swift</c> (Mac report 2026-09-15_20-13-04): which column
    /// the pointer is over, what a drop writes — including whether it completes or reopens the task —
    /// and what the board keeps once the server answers.
    ///
    /// The Mac's cases are ported as DATA, not as a reading: every assertion below has a counterpart
    /// in the Swift suite, so a divergence between the two boards fails here.
    /// </summary>
    [TestClass]
    public class ProjectKanbanDragTests
    {
        private const string Todo = "todo";
        private const string Doing = "doing";
        private const string Done = "done";
        private const string ProjectId = "p";
        private const string TaskId = "t-1";
        private const string Owner = "owner-1";

        private static readonly IList<ProjectCustomStatus> Statuses = new[]
        {
            ToDoFixtures.Column(Todo, "To Do", 0),
            ToDoFixtures.Column(Doing, "In Progress", 1),
            ToDoFixtures.Column(Done, "Done", 2, completes: true),
        };

        private static Project Project(bool usesCustomStatuses = true,
                                       IEnumerable<ProjectMember> members = null)
        {
            return ToDoFixtures.Project(
                ProjectId, "Project", Owner,
                usesCustomStatuses ? Statuses : new List<ProjectCustomStatus>(),
                members: members ?? new[] { ToDoFixtures.Member(Owner, ProjectMemberRole.Owner) });
        }

        private static IList<ProjectColumn> Columns(Project project)
        {
            return ProjectKanban.Columns(project, ToDoFixtures.SectionTitles);
        }

        private static ToDoTask Task(ToDoStatus status = ToDoStatus.NotStarted, string column = Todo)
        {
            return ToDoFixtures.Task(TaskId, status, customStatusId: column, projectId: ProjectId);
        }

        private static ToDoTask Plan(ToDoTask task, string columnId, Project project, bool canMove = true)
        {
            return ProjectTaskMove.Plan(task, columnId, project, Columns(project), canMove);
        }

        // MARK: - Which column is under the pointer

        private static readonly IDictionary<string, BoardRect> Frames = new Dictionary<string, BoardRect>
        {
            { Todo, new BoardRect(0, 0, 240, 400) },
            { Doing, new BoardRect(250, 0, 240, 400) },
            { Done, new BoardRect(500, 0, 240, 400) },
        };

        [TestMethod]
        public void PointerOverAColumnNamesThatColumn()
        {
            Assert.AreEqual(Doing, ProjectBoardDrag.ColumnId(new BoardPoint(300, 120), Frames));
            Assert.AreEqual(Done, ProjectBoardDrag.ColumnId(new BoardPoint(600, 10), Frames));
        }

        [TestMethod]
        public void PointerBetweenOrOutsideColumnsNamesNone()
        {
            Assert.IsNull(ProjectBoardDrag.ColumnId(new BoardPoint(245, 120), Frames),
                          "The gutter between two columns belongs to neither");
            Assert.IsNull(ProjectBoardDrag.ColumnId(new BoardPoint(300, 900), Frames),
                          "Below the board is off the board");
        }

        /// <summary>
        /// Half-open on both axes, so two columns laid side by side never both claim the seam. The
        /// left column ends at 240 exclusive and the right begins at 250 inclusive.
        /// </summary>
        [TestMethod]
        public void AColumnOwnsItsNearEdgeAndNotItsFarEdge()
        {
            Assert.AreEqual(Todo, ProjectBoardDrag.ColumnId(new BoardPoint(0, 0), Frames));
            Assert.IsNull(ProjectBoardDrag.ColumnId(new BoardPoint(240, 0), Frames));
            Assert.AreEqual(Doing, ProjectBoardDrag.ColumnId(new BoardPoint(250, 0), Frames));
        }

        [TestMethod]
        public void OverlappingFramesResolveToTheSmallestColumn()
        {
            var nested = new Dictionary<string, BoardRect>
            {
                { "outer", new BoardRect(0, 0, 500, 500) },
                { "inner", new BoardRect(100, 100, 100, 100) },
            };
            Assert.AreEqual("inner", ProjectBoardDrag.ColumnId(new BoardPoint(150, 150), nested));
            Assert.AreEqual("outer", ProjectBoardDrag.ColumnId(new BoardPoint(400, 400), nested));
        }

        /// <summary>
        /// Two frames of identical size overlapping: the id decides, so the drop highlight cannot
        /// flicker between them as the dictionary is re-enumerated.
        /// </summary>
        [TestMethod]
        public void EquallySizedOverlappingColumnsBreakTheTieOnTheirId()
        {
            var tied = new Dictionary<string, BoardRect>
            {
                { "zebra", new BoardRect(0, 0, 100, 100) },
                { "apple", new BoardRect(0, 0, 100, 100) },
            };
            Assert.AreEqual("apple", ProjectBoardDrag.ColumnId(new BoardPoint(50, 50), tied));

            var reversed = new Dictionary<string, BoardRect>
            {
                { "apple", new BoardRect(0, 0, 100, 100) },
                { "zebra", new BoardRect(0, 0, 100, 100) },
            };
            Assert.AreEqual("apple", ProjectBoardDrag.ColumnId(new BoardPoint(50, 50), reversed),
                            "Same frames, same answer, whatever order they were registered in");
        }

        // MARK: - What a drop writes

        [TestMethod]
        public void DropOnACompletingColumnCompletesTheTask()
        {
            ToDoTask moved = Plan(Task(), Done, Project());
            Assert.AreEqual(Done, moved.CustomStatusId);
            Assert.AreEqual(ToDoStatusWire.Completed, moved.Status);
            Assert.IsTrue(ToDoCompletionState.IsFinished(moved, Statuses));
        }

        [TestMethod]
        public void DropOnANonCompletingColumnLeavesTheTaskIncomplete()
        {
            ToDoTask moved = Plan(Task(), Doing, Project());
            Assert.AreEqual(Doing, moved.CustomStatusId);
            Assert.AreEqual(ToDoStatusWire.InProgress, moved.Status);
            Assert.IsFalse(ToDoCompletionState.IsFinished(moved, Statuses));
        }

        /// <summary>
        /// The mirror of <see cref="ToDoStatusToggle.Apply"/>: a task completed elsewhere must not
        /// stay completed in a column that does not complete, which is the inconsistency the board is
        /// meant to end.
        /// </summary>
        [TestMethod]
        public void DraggingACompletedTaskBackToAnOpenColumnReopensIt()
        {
            ToDoTask moved = Plan(Task(ToDoStatus.Completed, Done), Todo, Project());
            Assert.AreEqual(Todo, moved.CustomStatusId);
            Assert.AreEqual(ToDoStatusWire.NotStarted, moved.Status);
            Assert.IsFalse(ToDoCompletionState.IsFinished(moved, Statuses));
        }

        [TestMethod]
        public void DropOnItsOwnColumnWritesNothing()
        {
            Assert.IsNull(Plan(Task(), Todo, Project()));
        }

        /// <summary>
        /// Ticked off from the Day Plan, so the row says COMPLETED while its column still says To Do:
        /// the board already draws it under Done, and dropping it on Done must stay a no-op.
        /// </summary>
        [TestMethod]
        public void DropOnTheColumnTheCardIsAlreadyDrawnInWritesNothing()
        {
            Assert.IsNull(Plan(Task(ToDoStatus.Completed, Todo), Done, Project()));
        }

        [TestMethod]
        public void DropOnAColumnThatIsNotOnThisBoardWritesNothing()
        {
            Assert.IsNull(Plan(Task(), "some-other-projects-column", Project()));
        }

        [TestMethod]
        public void DropRelinksTheTaskToTheProjectAndItsTag()
        {
            ToDoTask moved = Plan(Task(), Doing, Project());
            Assert.AreEqual(ProjectId, moved.ProjectId);
            CollectionAssert.AreEqual(new[] { ProjectId }, moved.TagList.Select(tag => tag.Id).ToArray());
        }

        /// <summary>The project's own tag goes LAST, behind whatever labels the task already carried.</summary>
        [TestMethod]
        public void DropKeepsTheTasksOwnLabelsAndPutsTheProjectTagLast()
        {
            ToDoTask labelled = ToDoFixtures.Task(TaskId, customStatusId: Todo, projectId: ProjectId,
                                                  tags: new[] { new ToDoTag(ProjectId, "Project"), new ToDoTag("urgent", "Urgent") });
            ToDoTask moved = Plan(labelled, Doing, Project());
            CollectionAssert.AreEqual(new[] { "urgent", ProjectId },
                                      moved.TagList.Select(tag => tag.Id).ToArray());
        }

        /// <summary>A planned move must never mutate the row the board is still drawing.</summary>
        [TestMethod]
        public void PlanningAMoveLeavesTheOriginalRowAlone()
        {
            ToDoTask original = Task();
            Plan(original, Done, Project());
            Assert.AreEqual(Todo, original.CustomStatusId);
            Assert.AreEqual(ToDoStatusWire.NotStarted, original.Status);
            Assert.AreEqual(0, original.TagList.Count);
        }

        /// <summary>Without custom statuses the columns ARE the default statuses.</summary>
        [TestMethod]
        public void DropOnADefaultColumnSetsTheStatusItself()
        {
            Project plain = Project(usesCustomStatuses: false);
            ToDoTask moved = ProjectTaskMove.Plan(Task(column: null), ToDoStatusWire.Completed, plain,
                                                  Columns(plain), true);
            Assert.AreEqual(ToDoStatusWire.Completed, moved.Status);
            Assert.IsNull(moved.CustomStatusId);
        }

        /// <summary>
        /// A synced project's tasks belong to the provider. Moving one still writes the column, but
        /// must not re-point <c>project_id</c> at a project this app does not own.
        /// </summary>
        [TestMethod]
        public void MovingASyncedProjectsCardLeavesItsProjectIdAlone()
        {
            // A synced project's columns belong to the provider, so it carries none of its own.
            Project synced = ToDoFixtures.Project(ProjectId, "Zoho", Owner, null, syncedProjectId: "zoho-42",
                                                  members: new[] { ToDoFixtures.Member(Owner, ProjectMemberRole.Owner) });
            ToDoTask task = ToDoFixtures.Task(TaskId, projectId: "provider-project");

            ToDoTask moved = ProjectTaskMove.Plan(task, ToDoStatusWire.Completed, synced, Columns(synced), true);
            Assert.AreEqual("provider-project", moved.ProjectId);
            Assert.AreEqual(ToDoStatusWire.Completed, moved.Status);
        }

        /// <summary>
        /// A Mac inconsistency, ported AS-IS rather than tidied.
        ///
        /// <see cref="ProjectKanban.Columns"/> draws a board from <c>OrderedStatuses</c> — which does
        /// not care whether the project is synced — while every rule that RESOLVES a card's column is
        /// handed <c>UsesCustomStatuses</c>, which is false for a synced project. The two disagree, so
        /// a synced project that also carried custom statuses would draw those columns and then
        /// mis-handle a drop on one: <see cref="ProjectKanban.Moved"/> takes its no-custom-columns
        /// branch and writes the COLUMN ID into <c>status</c>, where a wire status belongs, while
        /// clearing <c>custom_status_id</c> entirely.
        ///
        /// Pinned rather than fixed, because the shape CANNOT come from the server. The only path that
        /// returns a synced project — backend <c>ProjectService.mapSyncedProjectToResponse</c> — sets
        /// <c>custom_statuses: []</c> outright; the provider's columns travel as the synced row's
        /// <c>available_statuses</c> and never reach this model (checked on backend main, 2026-09-24).
        /// So the bug needs a client to build the object itself, and neither does. Tidying it would
        /// change behaviour on one platform only for a case no user can reach.
        ///
        /// If the backend ever starts sending a synced project's own columns, this test is the tripwire:
        /// the fix is then to make <c>Columns</c> and the resolving rules agree on BOTH clients, not to
        /// make <c>Columns</c> synced-aware here alone.
        /// </summary>
        [TestMethod]
        public void ASyncedProjectWithCustomColumnsDrawsThemButMishandlesADropOnThem()
        {
            Project odd = ToDoFixtures.Project(ProjectId, "Zoho", Owner, Statuses, syncedProjectId: "zoho-42",
                                               members: new[] { ToDoFixtures.Member(Owner, ProjectMemberRole.Owner) });
            IList<ProjectColumn> drawn = Columns(odd);

            CollectionAssert.AreEqual(new[] { Todo, Doing, Done }, drawn.Select(column => column.Id).ToArray(),
                                      "The board is drawn from the custom statuses regardless of syncing");
            Assert.IsFalse(odd.UsesCustomStatuses, "...but every column-resolving rule is told otherwise");

            ToDoTask moved = ProjectTaskMove.Plan(Task(), Done, odd, drawn, true);
            Assert.AreEqual(Done, moved.Status, "The column id lands in `status`, which is the bug");
            Assert.IsNull(moved.CustomStatusId);
        }

        // MARK: - Which status a column position implies

        /// <summary>
        /// The POSITION decides, not the label: a board whose first column is "Backlog" still reports
        /// NOT_STARTED for it, and a column that is not on the board at all is IN_PROGRESS rather than
        /// an error — that is what keeps the default-status half of a row meaningful to a client that
        /// cannot see this project's columns.
        /// </summary>
        [TestMethod]
        public void ColumnPositionDecidesTheDefaultStatus()
        {
            IList<ProjectColumn> columns = Columns(Project());
            Assert.AreEqual(ToDoStatus.NotStarted, ProjectKanban.StatusForColumn(Todo, columns));
            Assert.AreEqual(ToDoStatus.InProgress, ProjectKanban.StatusForColumn(Doing, columns));
            Assert.AreEqual(ToDoStatus.Completed, ProjectKanban.StatusForColumn(Done, columns));
            Assert.AreEqual(ToDoStatus.InProgress, ProjectKanban.StatusForColumn("not-a-column", columns));
        }

        /// <summary>
        /// A completing column in FIRST position is COMPLETED, not NOT_STARTED — the completion flag
        /// is checked before the position. A one-column "Done" board is a real shape on the web.
        /// </summary>
        [TestMethod]
        public void ACompletingFirstColumnStillCompletes()
        {
            var board = new List<ProjectColumn> { new ProjectColumn(Done, "Done", true) };
            Assert.AreEqual(ToDoStatus.Completed, ProjectKanban.StatusForColumn(Done, board));
        }

        // MARK: - Permission

        [TestMethod]
        public void AViewerWhoIsNotAMemberCannotMoveACard()
        {
            ProjectMemberRole? role = ProjectPermissions.ViewerRole(Project(), "someone-else");
            Assert.IsNull(role);
            Assert.IsFalse(ProjectPermissions.CanMoveTask(role));
            Assert.IsNull(Plan(Task(), Done, Project(), ProjectPermissions.CanMoveTask(role)));
        }

        [TestMethod]
        public void EveryMemberOfTheProjectCanMoveACard()
        {
            foreach (ProjectMemberRole role in Enum.GetValues(typeof(ProjectMemberRole)).Cast<ProjectMemberRole>())
            {
                Assert.IsTrue(ProjectPermissions.CanMoveTask(role), role + " should be able to move a card");
            }
        }

        // MARK: - Optimistic write, and putting the card back

        [TestMethod]
        public void AcceptedWriteKeepsWhatTheServerStored()
        {
            ToDoTask previous = Task();
            ToDoTask attempted = Plan(previous, Done, Project());
            ToDoTask stored = attempted.Copy();
            stored.Title = "Renamed on the server";

            ProjectTaskMoveOutcome outcome = ProjectTaskMove.Settled(200, stored, attempted, previous);
            Assert.IsFalse(outcome.Failed);
            Assert.AreEqual("Renamed on the server", outcome.Task.Title);
        }

        [TestMethod]
        public void AcceptedWriteWithNoBodyKeepsTheMovedCard()
        {
            ToDoTask previous = Task();
            ToDoTask attempted = Plan(previous, Done, Project());

            ProjectTaskMoveOutcome outcome = ProjectTaskMove.Settled(204, null, attempted, previous);
            Assert.IsFalse(outcome.Failed);
            Assert.AreEqual(Done, outcome.Task.CustomStatusId);
        }

        [TestMethod]
        public void RejectedWriteRestoresTheOriginalColumn()
        {
            ToDoTask previous = Task();
            ToDoTask attempted = Plan(previous, Done, Project());

            ProjectTaskMoveOutcome outcome = ProjectTaskMove.Settled(500, null, attempted, previous);
            Assert.IsTrue(outcome.Failed, "A rejected move must be reported, not swallowed");
            Assert.AreEqual(Todo, outcome.Task.CustomStatusId);
            Assert.AreEqual(ToDoStatusWire.NotStarted, outcome.Task.Status);
        }

        /// <summary>
        /// The success range is 2xx and nothing else. A 301 carries no stored row, and treating it as
        /// success would leave the card in a column the server never accepted.
        /// </summary>
        [TestMethod]
        public void OnlyATwoHundredRangeStatusCountsAsAccepted()
        {
            ToDoTask previous = Task();
            ToDoTask attempted = Plan(previous, Done, Project());

            Assert.IsFalse(ProjectTaskMove.Settled(299, null, attempted, previous).Failed);
            Assert.IsTrue(ProjectTaskMove.Settled(300, null, attempted, previous).Failed);
            Assert.IsTrue(ProjectTaskMove.Settled(199, null, attempted, previous).Failed);
        }
    }
}
