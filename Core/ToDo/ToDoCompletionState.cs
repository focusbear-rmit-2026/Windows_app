using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>What a completion write should do with the to-do's board column.</summary>
    public struct ToDoColumnWrite : IEquatable<ToDoColumnWrite>
    {
        private ToDoColumnWrite(bool moves, string columnId)
        {
            Moves = moves;
            ColumnId = columnId;
        }

        /// <summary>
        /// False means: omit <c>custom_status_id</c> from the PUT, so the backend keeps whatever it
        /// has. The right answer whenever we cannot improve on the stored column.
        /// </summary>
        public bool Moves { get; }

        /// <summary>The column id to send alongside the new status, when <see cref="Moves"/>.</summary>
        public string ColumnId { get; }

        public static readonly ToDoColumnWrite LeaveAlone = new ToDoColumnWrite(false, null);

        public static ToDoColumnWrite MoveTo(string columnId) => new ToDoColumnWrite(true, columnId);

        public bool Equals(ToDoColumnWrite other) => Moves == other.Moves && ColumnId == other.ColumnId;
        public override bool Equals(object obj) => obj is ToDoColumnWrite other && Equals(other);
        public override int GetHashCode() => Moves.GetHashCode() * 397 ^ (ColumnId?.GetHashCode() ?? 0);
        public override string ToString() => Moves ? "MoveTo(" + ColumnId + ")" : "LeaveAlone";
    }

    /// <summary>
    /// Whether a to-do reads as finished, wherever it was completed.
    ///
    /// Status FIRST, deliberately. Completing a task from the Day Plan, the to-do list, the routine
    /// player or another device sets <c>status</c> alone and leaves <c>custom_status_id</c> on whatever
    /// open column the task was sitting in. The board and the sectioned list already draw those rows
    /// as done, so any other reading of the same row contradicts what the user is looking at — which
    /// is how the Mac's forecast came to plan work the user had already ticked off
    /// (report 2026-09-15_17-41-45).
    ///
    /// The Mac's comment on this rule records that a third, divergent copy of it existed before it was
    /// centralised here. This port is the Windows copy of the SAME rule, not a fourth one: anything on
    /// Windows that needs to know whether a task is done calls into this class.
    /// </summary>
    public static class ToDoCompletionState
    {
        /// <summary>Custom column ids, across any number of projects, whose tasks count as finished.</summary>
        public static ISet<string> CompletingStatusIds(IEnumerable<Project> projects)
        {
            IEnumerable<string> ids = (projects ?? Enumerable.Empty<Project>())
                .SelectMany(project => project.CustomStatuses)
                .Where(status => status.Completes)
                .Select(status => status.Id);
            return new HashSet<string>(ids, StringComparer.Ordinal);
        }

        public static bool IsFinished(ToDoTask task, ISet<string> completingStatusIds)
        {
            if (task.IsCompleted) return true;

            string columnId = task.CustomStatusId;
            if (string.IsNullOrEmpty(columnId)) return false;
            return completingStatusIds != null && completingStatusIds.Contains(columnId);
        }

        public static bool IsFinished(ToDoTask task, IEnumerable<ProjectCustomStatus> customStatuses)
        {
            IEnumerable<string> ids = (customStatuses ?? Enumerable.Empty<ProjectCustomStatus>())
                .Where(status => status.Completes)
                .Select(status => status.Id);
            return IsFinished(task, new HashSet<string>(ids, StringComparer.Ordinal));
        }

        /// <summary>
        /// Where a to-do should sit after being completed (or re-opened) OUTSIDE the project board —
        /// from the Day Plan, the routine player or the focus session.
        ///
        /// Completing has to move the card as well as flip the status. Setting the status alone leaves
        /// the task in whatever open column it was in, which is how five finished tasks kept reading
        /// as outstanding on the kanban, on web and on mobile (report 2026-09-15_17-41-45, "why are
        /// tasks remaining in ToDo when they were completed?").
        ///
        /// <list type="bullet">
        /// <item>A completion moves to the project's FIRST column flagged <c>should_complete_task</c>,
        /// which is the column the kanban checkbox uses, so the same task lands in the same place
        /// whichever screen ticked it.</item>
        /// <item>A project with NO completing column is left alone. The board's own rendering falls
        /// back to the project's last column; that fallback must not be written here, because parking
        /// a finished task in a non-completing column is exactly the state that makes the server call
        /// it outstanding. Leaving the column untouched keeps <c>status</c> as the only signal, which
        /// is read correctly.</item>
        /// <item>Re-opening moves back to the project's first column, but ONLY from a completing one.
        /// A task the user had in "In Progress" must not be dragged back to "To Do" by an un-tick, and
        /// a task left sitting in "Done" while its status says NOT_STARTED is the same inconsistency
        /// mirrored.</item>
        /// </list>
        ///
        /// Named <c>Resolve...</c> rather than the Mac's <c>columnWrite</c> only because C# cannot
        /// share a name between the method and the <see cref="ToDoColumnWrite"/> type it returns.
        /// </summary>
        public static ToDoColumnWrite ResolveColumnWrite(bool completed, string currentColumnId,
                                                         IEnumerable<ProjectCustomStatus> customStatuses)
        {
            IList<ProjectCustomStatus> ordered = ToDoOrdering.ByOrder(customStatuses);
            ProjectCustomStatus completing = ordered.FirstOrDefault(status => status.Completes);
            if (completing == null) return ToDoColumnWrite.LeaveAlone;

            if (completed)
            {
                return currentColumnId == completing.Id
                    ? ToDoColumnWrite.LeaveAlone
                    : ToDoColumnWrite.MoveTo(completing.Id);
            }

            // Only a column we know finishes tasks is worth undoing.
            if (string.IsNullOrEmpty(currentColumnId)) return ToDoColumnWrite.LeaveAlone;
            if (!ordered.Any(status => status.Id == currentColumnId && status.Completes)) return ToDoColumnWrite.LeaveAlone;

            ProjectCustomStatus first = ordered.FirstOrDefault();
            if (first == null || first.Id == currentColumnId) return ToDoColumnWrite.LeaveAlone;
            return ToDoColumnWrite.MoveTo(first.Id);
        }
    }

    /// <summary>The checkbox rule: what ticking or un-ticking a to-do writes.</summary>
    public static class ToDoStatusToggle
    {
        /// <summary>
        /// Web `buildProjectDetailsTaskStatusToggle`: with custom columns the task moves to the
        /// project's done column or back to the first column; without them the default status flips
        /// and any custom id is dropped.
        ///
        /// Completing uses the first column flagged <c>should_complete_task</c> AND NOTHING ELSE.
        /// Web's `resolveProjectKanbanColumnId` falls back to the project's LAST column when none
        /// completes, and <see cref="ProjectKanban.ColumnId"/> keeps that fallback for RENDERING — but
        /// writing it would park a finished task in a column the server reads as open, which is how
        /// completed work kept showing as outstanding (Mac report 2026-09-15_17-41-45). Nothing is
        /// lost by not writing it: <see cref="ProjectKanban.ColumnId"/> already draws a COMPLETED task
        /// under the done column whatever its stored id, so the card lands in the same place either way.
        ///
        /// So a completion here can leave <c>CustomStatusId</c> pointing at an open column. That is
        /// deliberate, and is the reason this returns a task rather than just a column id.
        /// </summary>
        /// <summary>
        /// <see cref="Apply"/> for a caller that may not have the task's project at all — the list's catalogue is
        /// active projects only, and loads alongside the tasks. An unknown board is not a board without columns:
        /// clearing <c>custom_status_id</c> there moved the card out of its column (ticking a task before the
        /// catalogue arrived, or one in an archived project). With no project in hand only the status changes,
        /// and <see cref="ProjectKanban.ColumnId"/> already draws a completed task under Done.
        /// </summary>
        public static ToDoTask ApplyWithOwner(ToDoTask task, bool completed, Project owner)
        {
            if (owner == null && !string.IsNullOrEmpty(task.ProjectId))
            {
                ToDoTask updated = task.Copy();
                updated.Status = completed ? ToDoStatusWire.Completed : ToDoStatusWire.NotStarted;
                return updated;
            }
            return Apply(task, completed, owner != null && owner.UsesCustomStatuses ? owner.CustomStatuses : null);
        }

        public static ToDoTask Apply(ToDoTask task, bool completed, IEnumerable<ProjectCustomStatus> customStatuses)
        {
            ToDoTask updated = task.Copy();
            updated.Status = completed ? ToDoStatusWire.Completed : ToDoStatusWire.NotStarted;

            IList<ProjectCustomStatus> ordered = ToDoOrdering.ByOrder(customStatuses);
            if (ordered.Count == 0)
            {
                updated.CustomStatusId = null;
                return updated;
            }

            if (!completed)
            {
                updated.CustomStatusId = ordered[0].Id;
                return updated;
            }

            ProjectCustomStatus completing = ordered.FirstOrDefault(status => status.Completes);
            if (completing != null) updated.CustomStatusId = completing.Id;
            return updated;
        }
    }
}
