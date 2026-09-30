using System;
using System.Collections.Generic;
using System.Linq;
using DayPlanBlockKey = FocusBear.Core.Schedule.DayPlanBlockKey;
using FocusBear.Core.ToDo;

namespace FocusBear.Core.Forecast
{
    /// <summary>
    /// Builds the forecast's inputs from the server catalogue — the Mac's <c>LocalForecastSnapshot</c>.
    /// </summary>
    public static class ForecastSnapshot
    {
        /// <summary>A task the user has placed on a day of their Day Plan.</summary>
        public struct PlannedCopy
        {
            public PlannedCopy(string sourceTaskId, DateTime day, int durationMinutes, string blockKey)
            {
                SourceTaskId = sourceTaskId; Day = day; DurationMinutes = durationMinutes; BlockKey = blockKey;
            }

            public string SourceTaskId { get; }
            public DateTime Day { get; }
            public int DurationMinutes { get; }
            public string BlockKey { get; }
        }

        /// <summary>The project inputs, with an unrated project read as 0 ("no opinion").</summary>
        public static List<LocalTimeForecast.ProjectInput> Projects(IEnumerable<Project> projects)
        {
            return (projects ?? Enumerable.Empty<Project>()).Where(p => !string.IsNullOrEmpty(p.Id))
                .Select(p => new LocalTimeForecast.ProjectInput(p.Id, p.Outcome ?? 0, ParseDay(p.DueDate), p.TaskCount, p.UncompletedTaskCount))
                .ToList();
        }

        /// <summary>
        /// Prices the project backlog on its due dates, rolling overdue work onto today (Mac
        /// <c>projectTasks</c>). Local copies contribute duration and same-day block assignments, never an
        /// independent placement date. Tasks outside any known project are absent, and finished rows (by the
        /// project's own completing columns) are dropped. So is work assigned to somebody else
        /// (<paramref name="meId"/>): it is theirs to schedule, so it neither takes this user's time nor gets
        /// a new date proposed.
        /// </summary>
        public static List<LocalTimeForecast.TaskInput> ProjectTasks(IEnumerable<ToDoTask> remote, IEnumerable<Project> projects,
                                                                     IEnumerable<PlannedCopy> planned, DateTime today, string meId = null)
        {
            var projectList = (projects ?? Enumerable.Empty<Project>()).Where(p => !string.IsNullOrEmpty(p.Id)).ToList();
            var projectById = new Dictionary<string, Project>();
            foreach (Project p in projectList) if (!projectById.ContainsKey(p.Id)) projectById[p.Id] = p;
            var imported = projectList.Where(p => !string.IsNullOrEmpty(p.SyncedProjectId))
                                      .GroupBy(p => p.SyncedProjectId).ToDictionary(g => g.Key, g => g.ToList());
            // The latest placement wins when the same to-do sits on more than one day.
            var placement = new Dictionary<string, PlannedCopy>();
            foreach (PlannedCopy copy in planned ?? Enumerable.Empty<PlannedCopy>())
            {
                if (string.IsNullOrEmpty(copy.SourceTaskId)) continue;
                PlannedCopy existing;
                if (!placement.TryGetValue(copy.SourceTaskId, out existing) || copy.Day > existing.Day) placement[copy.SourceTaskId] = copy;
            }
            DateTime startOfToday = today.Date;
            var seen = new HashSet<string>();
            var result = new List<LocalTimeForecast.TaskInput>();
            foreach (ToDoTask task in remote ?? Enumerable.Empty<ToDoTask>())
            {
                Project native = task.ProjectId != null && projectById.ContainsKey(task.ProjectId) ? projectById[task.ProjectId] : null;
                List<Project> importedMatches = null;
                if (task.SyncedProjectId != null) imported.TryGetValue(task.SyncedProjectId, out importedMatches);
                Project project = native ?? (importedMatches != null && importedMatches.Count == 1 ? importedMatches[0] : null);
                if (project == null || string.IsNullOrEmpty(task.Id) || IsSomebodyElses(task, meId) || !seen.Add(task.Id)) continue;
                if (ToDoCompletionState.IsFinished(task, project.CustomStatuses)) continue;
                DateTime? due = ParseDay(task.DueDate);
                DateTime? dueDay = due.HasValue ? (due.Value < startOfToday ? startOfToday : due.Value) : (DateTime?)null;
                PlannedCopy copy;
                bool placed = placement.TryGetValue(task.Id, out copy);
                LocalTimeForecast.BlockAssignment? assignment = null;
                if (placed && dueDay.HasValue && copy.Day.Date == dueDay.Value && DayPlanBlockKey.Absorbs(copy.BlockKey))
                    assignment = new LocalTimeForecast.BlockAssignment(copy.Day.Date, copy.BlockKey);
                // Price placed work exactly as the Day Plan does.
                double? minutes = placed ? copy.DurationMinutes
                    : task.Duration.HasValue && task.Duration.Value > 0 ? task.Duration.Value / 60.0
                    : task.PerspirationLevel.HasValue ? ToDoScoring.MinutesForPerspiration(task.PerspirationLevel.Value) : (double?)null;
                result.Add(new LocalTimeForecast.TaskInput(task.Id, project.Id, minutes,
                                                           task.Outcome ?? LocalTimeForecast.TaskInput.UnratedOutcome,
                                                           task.EffectivePerspiration, due, dueDay, assignment, false));
            }
            return result;
        }

        /// <summary>
        /// Unfinished to-dos whose due date has passed — through the same rule the list's red chips use —
        /// leaving out work assigned to somebody else, which is not this user's to re-prioritise.
        /// </summary>
        public static int OverdueCount(IEnumerable<ToDoTask> tasks, DateTime now, string meId = null)
        {
            return (tasks ?? Enumerable.Empty<ToDoTask>())
                .Count(t => !IsSomebodyElses(t, meId) && ToDoDates.DueState(t.DueDate, t.IsCompleted, now) == ToDoDueState.Overdue);
        }

        /// <summary>Assigned to a known other person (flat id or the relation <c>GET /to-do</c> actually sends).</summary>
        public static bool IsSomebodyElses(ToDoTask task, string meId)
        {
            return task != null && ToDoAssigneeChoices.IsAssignedToSomebodyElse(
                ToDoAssigneeChoices.AssigneeId(task.AssigneeId, task.Assignee?.Id), meId);
        }

        public static DateTime? ParseDay(string value)
        {
            DateTime? parsed = ToDoDates.Parse(value);
            return parsed.HasValue ? parsed.Value.Date : (DateTime?)null;
        }
    }
}
