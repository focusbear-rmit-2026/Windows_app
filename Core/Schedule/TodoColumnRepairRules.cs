using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Schedule
{
    /// <summary>A kanban column of a project (<c>ProjectCustomStatus</c>).</summary>
    public sealed class ProjectCustomStatus
    {
        public ProjectCustomStatus(string id, string label, int? order = null, bool? shouldCompleteTask = null)
        {
            Id = id;
            Label = label;
            Order = order;
            ShouldCompleteTask = shouldCompleteTask;
        }

        public string Id { get; }
        public string Label { get; }
        public int? Order { get; }
        /// <summary>Moving a card here finishes the task.</summary>
        public bool? ShouldCompleteTask { get; }
    }

    /// <summary>The <c>status</c> values of a to-do.</summary>
    public static class ToDoStatus
    {
        public const string NotStarted = "NOT_STARTED";
        public const string InProgress = "IN_PROGRESS";
        public const string Completed = "COMPLETED";
    }

    /// <summary>The fields of a backend to-do the column sweep reads.</summary>
    public sealed class ToDoRow
    {
        public ToDoRow(string id, string status, string projectId = null, string customStatusId = null,
                       string title = null)
        {
            Id = id;
            Status = status;
            ProjectId = projectId;
            CustomStatusId = customStatusId;
            Title = title;
        }

        public string Id { get; }
        public string Status { get; }
        public string ProjectId { get; }
        public string CustomStatusId { get; set; }
        public string Title { get; }

        public bool IsCompleted
        {
            get { return Status == ToDoStatus.Completed; }
        }
    }

    /// <summary>
    /// Pure rules behind the one-off column repair, ported from the Mac's <c>DayPlanProjectRepair.swift</c>.
    ///
    /// Before the forward fix, completing a task from the Day Plan, the to-do list, the routine player
    /// or another device set <c>status: COMPLETED</c> and left <c>custom_status_id</c> on whatever open
    /// column the task was in. Those rows still read as outstanding on the kanban, on web, on mobile and
    /// to the server's own <c>exclude_completed</c> (report 2026-09-15_17-41-45).
    ///
    /// <c>status</c> is the ONLY evidence of completion here. Completion is never inferred from a column
    /// — that inference is the bug this whole change exists to remove — so a row is a candidate only
    /// when the backend itself says COMPLETED.
    /// </summary>
    public static class TodoColumnRepairRules
    {
        /// <summary>
        /// Bump to re-run the sweep after changing these rules. Stored per account, so a repaired
        /// account is never swept twice and a deliberate re-run is one number away.
        ///
        /// v2: v1 read completed to-dos newest-first and stopped at 500 rows of 4,596. Due-date order
        /// put 500 project-less rows on the front, so it found nothing, reported <c>noColumn=500</c> and
        /// stored its marker — on an account with 47 rows to repair.
        /// </summary>
        public const int SchemaVersion = 2;

        /// <summary>One row to move, and where.</summary>
        public sealed class Repair : IEquatable<Repair>
        {
            public Repair(string taskId, string projectId, string fromColumnId, string toColumnId)
            {
                TaskId = taskId;
                ProjectId = projectId;
                FromColumnId = fromColumnId;
                ToColumnId = toColumnId;
            }

            public string TaskId { get; }
            public string ProjectId { get; }
            public string FromColumnId { get; }
            public string ToColumnId { get; }

            public bool Equals(Repair other)
            {
                return !ReferenceEquals(other, null) && TaskId == other.TaskId && ProjectId == other.ProjectId
                       && FromColumnId == other.FromColumnId && ToColumnId == other.ToColumnId;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as Repair);
            }

            public override int GetHashCode()
            {
                unchecked { return ((TaskId == null ? 0 : TaskId.GetHashCode()) * 397) ^ (ToColumnId == null ? 0 : ToColumnId.GetHashCode()); }
            }

            public override string ToString()
            {
                return TaskId + ": " + FromColumnId + " → " + ToColumnId;
            }
        }

        /// <summary>Why a row was left alone. Every skip is logged: silence is what let this go unnoticed.</summary>
        public enum SkipReason
        {
            /// <summary>Status is not COMPLETED — never infer completion from a column.</summary>
            NotCompleted,
            /// <summary><c>custom_status_id</c> came back null: <c>status</c> alone already reads correctly.</summary>
            NoColumn,
            /// <summary>Not in a native project.</summary>
            NoProject,
            /// <summary>Its project's columns were not available.</summary>
            ProjectNotLoaded,
            /// <summary><c>custom_status_id</c> names no column of that project.</summary>
            ColumnUnknown,
            /// <summary>Correctly filed already.</summary>
            ColumnAlreadyCompletes,
            /// <summary>Nowhere better to put it.</summary>
            ProjectHasNoDoneColumn,
        }

        /// <summary>
        /// The wire text of each reason — the Swift enum's raw values, which is what the log line the
        /// support team reads is made of. One table, so the log format cannot drift from the enum.
        /// </summary>
        private static readonly IReadOnlyDictionary<SkipReason, string> ReasonText =
            new Dictionary<SkipReason, string>
            {
                { SkipReason.NotCompleted, "notCompleted" },
                { SkipReason.NoColumn, "noColumn" },
                { SkipReason.NoProject, "noProject" },
                { SkipReason.ProjectNotLoaded, "projectNotLoaded" },
                { SkipReason.ColumnUnknown, "columnUnknown" },
                { SkipReason.ColumnAlreadyCompletes, "columnAlreadyCompletes" },
                { SkipReason.ProjectHasNoDoneColumn, "projectHasNoDoneColumn" },
            };

        public static string TextFor(SkipReason reason)
        {
            return ReasonText[reason];
        }

        public sealed class Skip : IEquatable<Skip>
        {
            public Skip(string taskId, SkipReason reason)
            {
                TaskId = taskId;
                Reason = reason;
            }

            public string TaskId { get; }
            public SkipReason Reason { get; }

            public bool Equals(Skip other)
            {
                return !ReferenceEquals(other, null) && TaskId == other.TaskId && Reason == other.Reason;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as Skip);
            }

            public override int GetHashCode()
            {
                unchecked { return ((TaskId == null ? 0 : TaskId.GetHashCode()) * 397) ^ (int)Reason; }
            }

            public override string ToString()
            {
                return TaskId + "=" + TextFor(Reason);
            }
        }

        public sealed class Plan
        {
            public Plan(IReadOnlyList<Repair> repairs, IReadOnlyList<Skip> skips)
            {
                Repairs = repairs;
                Skips = skips;
            }

            public IReadOnlyList<Repair> Repairs { get; }
            public IReadOnlyList<Skip> Skips { get; }
        }

        /// <summary>Split the rows into those that are unambiguously mis-filed and those to leave alone.</summary>
        public static Plan Build(IEnumerable<ToDoRow> tasks,
                                 IReadOnlyDictionary<string, IReadOnlyList<ProjectCustomStatus>> columnsByProject)
        {
            List<Repair> repairs = new List<Repair>();
            List<Skip> skips = new List<Skip>();

            foreach (ToDoRow task in tasks.Where(t => !string.IsNullOrEmpty(t.Id)))
            {
                if (!task.IsCompleted) { skips.Add(new Skip(task.Id, SkipReason.NotCompleted)); continue; }
                if (string.IsNullOrEmpty(task.CustomStatusId)) { skips.Add(new Skip(task.Id, SkipReason.NoColumn)); continue; }
                if (string.IsNullOrEmpty(task.ProjectId)) { skips.Add(new Skip(task.Id, SkipReason.NoProject)); continue; }

                IReadOnlyList<ProjectCustomStatus> columns;
                if (!columnsByProject.TryGetValue(task.ProjectId, out columns))
                {
                    skips.Add(new Skip(task.Id, SkipReason.ProjectNotLoaded));
                    continue;
                }

                ProjectCustomStatus current = columns.FirstOrDefault(c => c.Id == task.CustomStatusId);
                if (current == null) { skips.Add(new Skip(task.Id, SkipReason.ColumnUnknown)); continue; }
                if (current.ShouldCompleteTask == true)
                {
                    skips.Add(new Skip(task.Id, SkipReason.ColumnAlreadyCompletes));
                    continue;
                }

                ProjectCustomStatus target = columns
                    .OrderBy(c => c.Order ?? 0)
                    .FirstOrDefault(c => c.ShouldCompleteTask == true);
                if (target == null) { skips.Add(new Skip(task.Id, SkipReason.ProjectHasNoDoneColumn)); continue; }

                repairs.Add(new Repair(task.Id, task.ProjectId, task.CustomStatusId, target.Id));
            }

            return new Plan(repairs, skips);
        }

        /// <summary>e.g. "columnAlreadyCompletes=1 columnUnknown=1 noColumn=1", in reason-name order.</summary>
        public static string Summary(IEnumerable<Skip> skips)
        {
            return string.Join(" ", skips
                .GroupBy(s => s.Reason)
                .Select(g => new { Text = TextFor(g.Key), Count = g.Count() })
                .OrderBy(g => g.Text, StringComparer.Ordinal)
                .Select(g => g.Text + "=" + g.Count));
        }

        /// <summary>
        /// How many rows in a raw page actually carried a <c>custom_status_id</c> KEY.
        ///
        /// A key the response omits and a key that is genuinely null are identical once decoded, so a
        /// sweep that cannot see the field looks exactly like a sweep with nothing to do. That is not
        /// hypothetical: v1 reported <c>noColumn=500</c>, 0 repairs, and stored its marker. Counting key
        /// presence separately is what makes "the rows have no column" distinguishable from "this pass
        /// is blind", and a blind pass aborts instead of recording success.
        /// </summary>
        public static int RowsCarryingColumnField(string json)
        {
            const string columnKey = "custom_status_id";
            try
            {
                JObject root = JObject.Parse(json ?? string.Empty);
                JArray rows = root["data"] as JArray;
                if (rows == null) return 0;
                return rows.OfType<JObject>().Count(row => row.Property(columnKey) != null);
            }
            catch (Newtonsoft.Json.JsonReaderException)
            {
                return 0;
            }
        }
    }
}
