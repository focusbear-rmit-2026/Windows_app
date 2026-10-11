using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>What the board should hold once the server has answered a move.</summary>
    public struct ProjectTaskMoveOutcome : IEquatable<ProjectTaskMoveOutcome>
    {
        public ProjectTaskMoveOutcome(ToDoTask task, bool failed)
        {
            Task = task;
            Failed = failed;
        }

        /// <summary>The row to keep: the server's on success, the pre-drag row on failure.</summary>
        public ToDoTask Task { get; }

        /// <summary>True when the write was rejected, so the host can say so rather than swallow it.</summary>
        public bool Failed { get; }

        public bool Equals(ProjectTaskMoveOutcome other)
        {
            return ReferenceEquals(Task, other.Task) && Failed == other.Failed;
        }

        public override bool Equals(object obj)
        {
            return obj is ProjectTaskMoveOutcome && Equals((ProjectTaskMoveOutcome)obj);
        }

        public override int GetHashCode()
        {
            unchecked { return ((Task?.GetHashCode() ?? 0) * 397) ^ Failed.GetHashCode(); }
        }
    }

    /// <summary>
    /// What a drop (or a "Move to" menu pick) should write, and what the board should hold once the
    /// server has answered.
    ///
    /// Both paths go through here, so the board cannot drift from the menu and neither can drift from
    /// what the server was actually told.
    /// </summary>
    public static class ProjectTaskMove
    {
        /// <summary>
        /// The task as it will be PUT, or null when nothing should be written at all: the viewer may
        /// not change this project's tasks, the column is not one of this board's, or the card was
        /// dropped back on the column it came from — a no-op must not cost a request.
        ///
        /// "The column it came from" is the column the card is DRAWN in, not the one its
        /// <c>custom_status_id</c> names. A task ticked off from the Day Plan keeps an open column id
        /// while <see cref="ProjectKanban.ColumnId"/> already draws it under Done; dropping it on Done
        /// therefore moves nothing the user can see, and writing anyway would spend a request to
        /// change a field the board does not read.
        ///
        /// Column and status travel together (<see cref="ProjectKanban.Moved"/>), which makes this the
        /// mirror of <see cref="ToDoStatusToggle.Apply"/>: that one moves the column when a task is
        /// completed elsewhere, this one sets the status when the card is moved. There is no third rule.
        /// </summary>
        public static ToDoTask Plan(ToDoTask task, string columnId, Project project,
                                    IList<ProjectColumn> columns, bool canMove)
        {
            if (!canMove) return null;
            if (columns == null || !columns.Any(column => column.Id == columnId)) return null;

            string current = ProjectKanban.ColumnId(task, columns, project.UsesCustomStatuses);
            if (current == columnId) return null;

            ToDoTask moved = ProjectKanban.Moved(task, columnId, columns, project.UsesCustomStatuses);
            moved.Tags = ProjectKanban.Tags(moved, project);

            // A synced project's task belongs to the provider's project; re-pointing it here would
            // claim a task this app does not own. Everything else is relinked to the board it is on.
            if (!project.IsSynced) moved.ProjectId = project.Id;
            return moved;
        }

        /// <summary>The successful HTTP range — a move is accepted on any 2xx, body or no body.</summary>
        private const int FirstSuccessStatus = 200;
        private const int FirstNonSuccessStatus = 300;

        /// <summary>
        /// The row the board keeps after the write: what the server stored on success, and the row as
        /// it was BEFORE the drag on failure — the card returns to the column it came from instead of
        /// sitting in one the server never accepted.
        ///
        /// A 2xx with no body (a 204) keeps the moved card: the server accepted the move and simply
        /// had nothing to say about it.
        /// </summary>
        public static ProjectTaskMoveOutcome Settled(int statusCode, ToDoTask saved, ToDoTask attempted,
                                                     ToDoTask previous)
        {
            if (statusCode < FirstSuccessStatus || statusCode >= FirstNonSuccessStatus)
            {
                return new ProjectTaskMoveOutcome(previous, true);
            }
            return new ProjectTaskMoveOutcome(saved ?? attempted, false);
        }
    }
}
