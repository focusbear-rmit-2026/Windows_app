using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>One board column: a project custom status, or a default status when the project has none.</summary>
    public sealed class ProjectColumn
    {
        public ProjectColumn(string id, string label, bool completes, string color = null)
        {
            Id = id;
            Label = label;
            Completes = completes;
            Color = color;
        }

        public string Id { get; }
        public string Label { get; }
        public string Color { get; }
        public bool Completes { get; }
    }

    /// <summary>
    /// How a project board lays its tasks out, and what moving a card between its columns writes
    /// (web <c>utils/projects/project-details.util.ts</c>).
    /// </summary>
    public static class ProjectKanban
    {
        /// <summary>Custom statuses in board order, else the three default columns.</summary>
        public static IList<ProjectColumn> Columns(Project project, IDictionary<ToDoStatus, string> defaultLabels)
        {
            IList<ProjectCustomStatus> ordered = project.OrderedStatuses;
            if (ordered.Count > 0)
            {
                return ordered
                    .Select(status => new ProjectColumn(status.Id, status.Label, status.Completes, status.Color))
                    .ToList();
            }

            return new[] { ToDoStatus.NotStarted, ToDoStatus.InProgress, ToDoStatus.Completed }
                .Select(status => new ProjectColumn(ToDoStatusWire.Of(status), Label(defaultLabels, status),
                                                    status == ToDoStatus.Completed))
                .ToList();
        }

        /// <summary>
        /// The column a completed card is drawn under: the first flagged <c>should_complete_task</c>,
        /// else the LAST. That last-column fallback is for drawing only — see
        /// <see cref="ToDoStatusToggle.Apply"/> for why it is never written.
        /// </summary>
        public static ProjectColumn DoneColumn(IList<ProjectColumn> columns)
        {
            return columns.FirstOrDefault(column => column.Completes) ?? columns.LastOrDefault();
        }

        /// <summary>
        /// `resolveProjectKanbanColumnId`: the task's custom status when it is a column of this board,
        /// else the legacy status-to-column mapping (first two open columns, then the done column).
        ///
        /// One deliberate step beyond the web: a task whose <c>status</c> is COMPLETED but whose custom
        /// id still points at an OPEN column — ticked off from the Day Plan, mobile, or a list that did
        /// not know the project's columns — is drawn in the done column rather than struck through
        /// under "To Do".
        /// </summary>
        public static string ColumnId(ToDoTask task, IList<ProjectColumn> columns, bool usesCustomStatuses)
        {
            if (!usesCustomStatuses) return ToDoStatusWire.Of(task.DefaultStatus);

            ProjectColumn done = DoneColumn(columns);
            string custom = task.CustomStatusId;
            if (!string.IsNullOrEmpty(custom))
            {
                ProjectColumn column = columns.FirstOrDefault(candidate => candidate.Id == custom);
                if (column != null)
                {
                    return task.IsCompleted && !column.Completes ? done?.Id : custom;
                }
            }

            IList<ProjectColumn> open = columns.Where(column => !column.Completes).ToList();
            switch (task.DefaultStatus)
            {
                case ToDoStatus.NotStarted:
                    return (open.FirstOrDefault() ?? columns.FirstOrDefault())?.Id;
                case ToDoStatus.InProgress:
                    ProjectColumn second = open.Count > 1 ? open[1] : open.FirstOrDefault();
                    return (second ?? columns.FirstOrDefault())?.Id;
                case ToDoStatus.Completed:
                    return done?.Id;
                default:
                    throw new ArgumentOutOfRangeException(nameof(task));
            }
        }

        /// <summary>
        /// `deriveProjectKanbanStatusFromColumn`: a completing column is COMPLETED, the FIRST column is
        /// NOT_STARTED, and everything else — including a column that is not on this board at all — is
        /// IN_PROGRESS.
        ///
        /// The position, not the label, is what decides: a board whose first column is called
        /// "Backlog" still reports NOT_STARTED for it, and a second open column called "To Do" still
        /// reports IN_PROGRESS. That is what keeps the default-status half of the row meaningful to
        /// clients that cannot see this project's columns.
        /// </summary>
        public static ToDoStatus StatusForColumn(string columnId, IList<ProjectColumn> columns)
        {
            int index = IndexOfColumn(columns, columnId);
            if (index < 0) return ToDoStatus.InProgress;
            if (columns[index].Completes) return ToDoStatus.Completed;
            return index == 0 ? ToDoStatus.NotStarted : ToDoStatus.InProgress;
        }

        /// <summary>
        /// `buildProjectKanbanMovedTask`: the task as it looks in its new column.
        ///
        /// Column and status are decided TOGETHER, which is what makes a drop onto a
        /// <c>should_complete_task</c> column complete the task and a drop onto any other column reopen
        /// it. Without a custom board there are no columns to hold, so the column id IS the status and
        /// any stale custom id is dropped.
        /// </summary>
        public static ToDoTask Moved(ToDoTask task, string columnId, IList<ProjectColumn> columns,
                                     bool usesCustomStatuses)
        {
            ToDoTask copy = task.Copy();
            if (usesCustomStatuses)
            {
                copy.CustomStatusId = columnId;
                copy.Status = ToDoStatusWire.Of(StatusForColumn(columnId, columns));
            }
            else
            {
                copy.Status = columnId;
                copy.CustomStatusId = null;
            }
            return copy;
        }

        /// <summary>
        /// `ensureProjectTaskTags`: keep the task's label tags, and put the project's own tag LAST.
        ///
        /// A project is linked to its tasks through a tag carrying the project's id, so a write that
        /// dropped it would take the task off the board it was just dragged across. The position is
        /// not cosmetic either — the tag row is rendered in order, and the project tag reads as the
        /// card's home rather than as one label among several.
        /// </summary>
        public static IList<ToDoTag> Tags(ToDoTask task, Project project)
        {
            IList<ToDoTag> existing = task.TagList;
            List<ToDoTag> others = existing.Where(tag => tag.Id != project.Id).ToList();
            ToDoTag own = existing.FirstOrDefault(tag => tag.Id == project.Id)
                ?? new ToDoTag(project.Id, project.Name);
            others.Add(own);
            return others;
        }

        private static int IndexOfColumn(IList<ProjectColumn> columns, string columnId)
        {
            if (columns == null) return -1;
            for (int index = 0; index < columns.Count; index++)
            {
                if (columns[index].Id == columnId) return index;
            }
            return -1;
        }

        private static string Label(IDictionary<ToDoStatus, string> labels, ToDoStatus status)
        {
            string label;
            if (labels != null && labels.TryGetValue(status, out label)) return label;
            return ToDoStatusWire.Of(status);
        }
    }
}
