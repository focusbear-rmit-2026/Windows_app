using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Focus
{
    /// <summary>
    /// A student's view of the teacher's checklist in a class session, as the session screen's task queue.
    /// The list comes from the class-session payloads (<c>tasks</c> ordered by <c>order</c>, the student's
    /// <c>completed_task_ids</c>), never from <c>sRawTasks</c>: the teacher owns the list, the student only ticks.
    /// The rules are the web dashboard's <c>class-ordered-tasks.util.ts</c>: tasks are done in order, so only the
    /// first task still to do can be ticked, and only the latest ticked one can be unticked.
    /// </summary>
    public static class ClassSessionChecklist
    {
        /// <summary>The checklist in a join / current payload, in the teacher's order; empty when the session has none.</summary>
        public static IList<FocusSessionTask> Read(JObject session)
        {
            if (session == null) return new List<FocusSessionTask>();
            var completed = new HashSet<string>((session["completed_task_ids"] as JArray ?? new JArray())
                .Select(t => t?.ToString() ?? "").Where(id => id.Length > 0));
            var rows = (session["tasks"] as JArray ?? new JArray()).OfType<JObject>()
                .Select(row => new { Row = row, Id = row["id"]?.ToString() ?? "", Order = Order(row["order"]) })
                .Where(r => r.Id.Length > 0)
                .OrderBy(r => r.Order)
                .ToList();
            var tasks = new List<FocusSessionTask>();
            foreach (var r in rows)
            {
                bool done = completed.Contains(r.Id);
                tasks.Add(new FocusSessionTask(r.Id, DecodeTitle(r.Row["title"]?.Type == JTokenType.String ? (string)r.Row["title"] : ""),
                    durationSeconds: DurationSeconds(r.Row["duration_minutes"]),
                    status: done ? FocusSessionTaskStatus.Completed : FocusSessionTaskStatus.NotStarted,
                    raw: (JObject)r.Row.DeepClone()));
            }
            return Activated(tasks);
        }

        /// <summary><c>tasks_version</c>, or 0 when the payload has none (a session started before versions existed).</summary>
        public static int TasksVersion(JObject session)
        {
            var token = session?["tasks_version"];
            return token != null && (token.Type == JTokenType.Integer || token.Type == JTokenType.Float) ? (int)token : 0;
        }

        /// <summary>A newer <c>tasks_version</c> for the same session means the teacher amended the list: reload.</summary>
        public static bool ShouldReload(string expectedSessionId, int currentVersion, string eventSessionId, int eventVersion) =>
            !string.IsNullOrEmpty(expectedSessionId) && expectedSessionId == eventSessionId && eventVersion > currentVersion;

        /// <summary>The next task to do is the one and only the student can tick.</summary>
        public static bool CanComplete(IEnumerable<FocusSessionTask> tasks, string id)
        {
            var current = (tasks ?? Enumerable.Empty<FocusSessionTask>()).FirstOrDefault(t => !t.IsCompleted);
            return current != null && current.Id == id;
        }

        /// <summary>Only the most recently ticked task can be unticked, so the order holds.</summary>
        public static bool CanUncomplete(IEnumerable<FocusSessionTask> tasks, string id)
        {
            var latest = (tasks ?? Enumerable.Empty<FocusSessionTask>()).LastOrDefault(t => t.IsCompleted);
            return latest != null && latest.Id == id;
        }

        /// <summary>The list with one task ticked or unticked, and the active flag moved to the next task to do.</summary>
        public static IList<FocusSessionTask> Marking(IEnumerable<FocusSessionTask> tasks, string id, bool completed)
        {
            var list = (tasks ?? Enumerable.Empty<FocusSessionTask>()).ToList();
            if (completed ? !CanComplete(list, id) : !CanUncomplete(list, id)) return list;
            return Activated(list.Select(t => t.Id == id
                ? t.With(status: completed ? FocusSessionTaskStatus.Completed : FocusSessionTaskStatus.NotStarted)
                : t).ToList());
        }

        public static int CompletedCount(IEnumerable<FocusSessionTask> tasks) =>
            (tasks ?? Enumerable.Empty<FocusSessionTask>()).Count(t => t.IsCompleted);

        /// <summary>The backend HTML-escapes titles (<c>&amp;#39;</c> for an apostrophe); the row shows plain text.</summary>
        public static string DecodeTitle(string title) => WebUtility.HtmlDecode(title ?? "");

        private static IList<FocusSessionTask> Activated(IList<FocusSessionTask> tasks)
        {
            var next = tasks.FirstOrDefault(t => !t.IsCompleted);
            return tasks.Select(t => t.With(isActive: next != null && ReferenceEquals(t, next))).ToList();
        }

        private static int Order(JToken token) =>
            token != null && (token.Type == JTokenType.Integer || token.Type == JTokenType.Float) ? (int)token : int.MaxValue;

        private static int DurationSeconds(JToken minutes)
        {
            if (minutes == null || (minutes.Type != JTokenType.Integer && minutes.Type != JTokenType.Float)) return FocusSessionTask.DefaultDurationSeconds;
            double value = (double)minutes;
            return value > 0 ? (int)Math.Round(value * 60) : FocusSessionTask.DefaultDurationSeconds;
        }
    }
}
