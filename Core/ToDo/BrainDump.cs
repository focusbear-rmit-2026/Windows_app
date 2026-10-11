using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>
    /// One task the AI proposed from a brain dump — <c>BraindumpTaskDto</c> of
    /// <c>POST /to-do/convert-brain-dump</c>, as far as the write rules read it.
    ///
    /// <see cref="Id"/> is generated on the CLIENT and is the whole reason a retry is safe: the write
    /// is a PUT of that id, so re-sending a task whose response never arrived upserts the same row
    /// rather than creating a second copy of it.
    ///
    /// <see cref="EstimatedMinutes"/> is a 64-bit integer to match Swift's <c>Int</c>, because the
    /// overflow it guards against is only reachable at 64-bit widths — see
    /// <see cref="BrainDumpTaskPayload.Body"/>.
    /// </summary>
    public sealed class BrainDumpTask
    {
        public BrainDumpTask(string taskName, long estimatedMinutes, IEnumerable<string> subtasks = null,
                             int? outcome = null, int? perspirationLevel = null, string dueDate = null,
                             string id = null)
        {
            Id = id ?? Guid.NewGuid().ToString();
            TaskName = taskName;
            EstimatedMinutes = estimatedMinutes;
            Subtasks = (subtasks ?? Enumerable.Empty<string>()).ToList();
            Outcome = outcome;
            PerspirationLevel = perspirationLevel;
            DueDate = dueDate;
        }

        /// <summary>Stable for the life of the draft, so a retry is an upsert of the same task.</summary>
        public string Id { get; }

        public string TaskName { get; set; }

        /// <summary>The backend sends <c>estimated_time</c>; the web reads
        /// <c>estimated_duration_minutes</c>. Either fills this.</summary>
        public long EstimatedMinutes { get; set; }

        public IList<string> Subtasks { get; set; }

        public int? Outcome { get; set; }

        public int? PerspirationLevel { get; set; }

        /// <summary>The AI's suggested date, when it offered one.</summary>
        public string DueDate { get; set; }

        public IList<string> SubtaskList => Subtasks ?? new List<string>();
    }

    /// <summary>
    /// The `PUT /to-do` body for one accepted brain-dump suggestion, or null when the suggestion is
    /// not writable at all.
    ///
    /// This is a VALIDATOR as much as a builder: an AI suggestion is the one task source that is not
    /// typed by a person, so a nonsense estimate reaches the wire unless something stops it here.
    /// Returning null rather than clamping is deliberate — a task silently rewritten to a duration the
    /// user never saw is worse than one the review screen keeps until it is fixed.
    /// </summary>
    public static class BrainDumpTaskPayload
    {
        private const int SecondsPerMinute = 60;

        /// <summary>
        /// The keys this body carries. Deliberately NOT <see cref="ToDoUpsertBody"/>: that builds the
        /// body of an existing <see cref="ToDoTask"/>, and a brain-dump suggestion is not one yet.
        /// </summary>
        public static class Keys
        {
            public const string Id = "id";
            public const string Title = "title";
            public const string Details = "details";
            public const string Objective = "objective";
            public const string Tags = "tags";
            public const string DueDate = "due_date";
            public const string Duration = "duration";
            public const string EisenhowerQuadrant = "eisenhower_quadrant";
            public const string Subtasks = "subtasks";
            public const string Status = "status";
            public const string Outcome = "outcome";
            public const string PerspirationLevel = "perspiration_level";
        }

        /// <param name="today">
        /// <c>yyyy-MM-dd</c>. A suggestion with no date of its own is due TODAY: the whole point of a
        /// brain dump is to get what is in the user's head onto the day in front of them, and an
        /// undated task would land in the backlog they were trying to empty.
        /// </param>
        public static IDictionary<string, object> Body(BrainDumpTask task, string today)
        {
            if (task == null) return null;
            if (string.IsNullOrWhiteSpace(task.TaskName)) return null;
            if (task.EstimatedMinutes <= 0) return null;

            long durationSeconds;
            try
            {
                durationSeconds = checked(task.EstimatedMinutes * SecondsPerMinute);
            }
            catch (OverflowException)
            {
                // Swift's `multipliedReportingOverflow` wraps rather than trapping; this is the same
                // test, spelled the way C# reports it.
                return null;
            }

            // The column is a 32-bit integer server-side, so an estimate that will not fit is refused
            // here instead of becoming a negative duration in the database.
            if (durationSeconds > int.MaxValue) return null;

            List<object> subtasks = task.SubtaskList
                .Select(name => (name ?? string.Empty).Trim())
                .Where(name => name.Length > 0)
                .Select(name => (object)new Dictionary<string, object>
                {
                    { ToDoUpsertBody.SubtaskKeys.Name, name },
                    { ToDoUpsertBody.SubtaskKeys.IsCompleted, false },
                })
                .ToList();

            return new Dictionary<string, object>
            {
                // Lower-cased because the backend stores ids lower-cased; sending the raw GUID would
                // make the retry a DIFFERENT id from the one the first attempt created.
                { Keys.Id, task.Id.ToLowerInvariant() },
                { Keys.Title, task.TaskName },
                { Keys.Details, string.Empty },
                { Keys.Objective, string.Empty },
                { Keys.Tags, new List<object>() },
                { Keys.DueDate, string.IsNullOrEmpty(task.DueDate) ? today : task.DueDate },
                { Keys.Duration, (int)durationSeconds },
                { Keys.EisenhowerQuadrant, (int)ToDo.EisenhowerQuadrant.DoIt },
                { Keys.Subtasks, subtasks },
                { Keys.Status, ToDoStatusWire.NotStarted },

                // Present-with-a-null-value IS the JSON null, and the backend clears a field sent as
                // null while leaving an absent key alone. An unrated suggestion must therefore send
                // null rather than omit the key: omitting it invokes the backend's default of 1, which
                // would rate a task the AI declined to rate.
                { Keys.Outcome, task.Outcome.HasValue ? (object)task.Outcome.Value : null },
                { Keys.PerspirationLevel, task.PerspirationLevel.HasValue ? (object)task.PerspirationLevel.Value : null },
            };
        }
    }
}
