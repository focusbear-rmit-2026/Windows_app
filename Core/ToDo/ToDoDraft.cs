using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>
    /// The task an editor starts from — web `DEFAULT_TODO_NEW_TASK` / `mapTaskToFormData`.
    /// </summary>
    public static class ToDoDraft
    {
        /// <summary>Where a new task lands when the caller has no better day for it.</summary>
        private const int FallbackDueDayOffset = 1;

        /// <summary>
        /// A new task: due tomorrow, default TOP levels, quadrant "Do" (the web's silent default now
        /// that the quadrant picker is hidden).
        ///
        /// Opened from a project it is pre-linked to that project — tag, `project_id` and the project's
        /// first column — <b>from the project object itself</b>, never a catalogue lookup, which may
        /// not yet know a project the user just created.
        /// </summary>
        /// <param name="dueDate">
        /// The caller's "next free day" from the forecast. Without one this falls back to tomorrow,
        /// which is what a new task always got.
        /// </param>
        public static ToDoTask NewTask(string title, DateTime now, Project project = null, DateTime? dueDate = null)
        {
            DateTime due = dueDate ?? now.AddDays(FallbackDueDayOffset);
            var task = new ToDoTask
            {
                Id = Guid.NewGuid().ToString(),
                Title = title,
                Details = string.Empty,
                Objective = string.Empty,
                Status = ToDoStatusWire.NotStarted,
                DueDate = ToDoDates.Ymd(due),

                // The server stamps `created_at`; a draft has not been created yet.
                CreatedAt = string.Empty,

                EisenhowerQuadrant = (int)ToDo.EisenhowerQuadrant.DoIt,
                Outcome = ToDoScoring.DefaultOutcome,
                PerspirationLevel = ToDoScoring.DefaultPerspiration,
                Tags = new List<ToDoTag>(),
                Subtasks = new List<ToDoSubtask>(),
            };

            if (project == null) return task;

            task.Tags = new List<ToDoTag> { new ToDoTag(project.Id, project.Name) };
            task.ProjectId = project.Id;
            task.CustomStatusId = project.UsesCustomStatuses
                ? project.OrderedStatuses.Select(status => status.Id).FirstOrDefault()
                : null;
            return task;
        }

        /// <summary>
        /// An existing task as an editor holds it: due date as a local <c>yyyy-MM-dd</c>, perspiration
        /// defaulted, details as plain text, assignee resolved from whichever shape the server sent.
        ///
        /// The outcome is deliberately NOT defaulted. Opening an unrated task must not silently give it
        /// a rating the user never chose — the next Save would then persist it.
        ///
        /// Returns a copy: the caller's task is an editor's input, not its scratch space.
        /// </summary>
        public static ToDoTask Editable(ToDoTask task)
        {
            ToDoTask copy = task.Copy();
            copy.DueDate = ToDoDates.YmdFromIso(task.DueDate);
            copy.PerspirationLevel = task.PerspirationLevel ?? ToDoScoring.DefaultPerspiration;
            copy.Details = ToDoHtml.PlainText(task.Details ?? string.Empty);

            // `GET /to-do` sends the assignee as a relation and no flat `assignee_id` (backend#2414).
            // Resolving it HERE, once, is what keeps null meaning "unassigned" everywhere downstream;
            // without it the next save wiped the assignment (Mac report 2026-09-16_15-14-36).
            copy.AssigneeId = ToDoAssigneeChoices.AssigneeId(task.AssigneeId,
                                                             task.Assignee == null ? null : task.Assignee.Id);
            return copy;
        }
    }
}
