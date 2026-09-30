using Focus_Bear.API;
using FocusBear.Core.Forecast;
using FocusBear.Core.ToDo;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using F = FocusBear.Core.Forecast.LocalTimeForecast;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>The four calls the reschedule plan writes with — a seam so the write path runs against a fake in tests.</summary>
    internal interface IReprioritiseApi
    {
        /// <summary><c>GET /to-do/by-ids</c>: a JSON array of the current rows, or null when the read failed.</summary>
        Task<string> GetToDosByIds(IEnumerable<string> ids);
        Task<ApiResponse> UpsertToDo(IDictionary<string, object> body);
        Task<Project> GetProject(string id);
        Task<ApiResponse> UpdateProject(string id, JObject body);
    }

    internal sealed class ReprioritiseApi : IReprioritiseApi
    {
        private readonly FocusBearAPIClient _client = new FocusBearAPIClient();
        public Task<string> GetToDosByIds(IEnumerable<string> ids) => _client.GetToDosByIds(ids);
        public Task<ApiResponse> UpsertToDo(IDictionary<string, object> body) => _client.UpsertToDo(body);
        public Task<Project> GetProject(string id) => _client.GetProject(id);
        public Task<ApiResponse> UpdateProject(string id, JObject body) => _client.UpdateProject(id, body);
    }

    /// <summary>
    /// The reschedule plan's writes — the Mac's <c>LocalForecastStore.apply</c> / <c>writeTaskOutcome</c>. Each
    /// task write re-reads the row first and sends only the field it changes plus the identity fields the
    /// <c>PUT /to-do</c> DTO insists on, so nothing else about the task can be overwritten from a stale copy.
    /// </summary>
    internal static class ReprioritiseWrites
    {
        internal sealed class DueDateContext
        {
            public Func<string, ToDoTask> PreviousTask = id => null;
            public Func<string, Project> PreviousProject = id => null;
            /// <summary>False once the signed-in account is no longer the one that reviewed the plan.</summary>
            public Func<bool> AccountUnchanged = () => true;
            /// <summary>After each task's date lands (the Day Plan follows it there).</summary>
            public Action<F.DueDateChange> TaskSaved = c => { };
            public Action<string> Log = s => { };
        }

        /// <summary>Writes the changes in order and stops at the first one refused or not saved. Returns (saved, all saved).</summary>
        public static async Task<Tuple<int, bool>> ApplyDueDatesAsync(IReprioritiseApi api, IList<F.DueDateChange> changes, DueDateContext context)
        {
            int saved = 0;
            foreach (F.DueDateChange change in changes ?? new List<F.DueDateChange>())
            {
                if (!context.AccountUnchanged()) return Tuple.Create(saved, false);
                string day = ToDoDates.Ymd(change.ProposedDate);
                if (change.Kind == F.ChangeKind.Project)
                {
                    Project previous = context.PreviousProject(change.ItemId);
                    Project latest = await api.GetProject(change.ItemId);
                    if (latest == null || previous == null || !ForecastRowGuard.SameDueDate(latest.DueDate, previous.DueDate))
                    {
                        context.Log("reprioritise apply: refused project=" + change.ItemId + " reason=" + (latest == null ? "missingRow" : "editedElsewhere"));
                        return Tuple.Create(saved, false);
                    }
                    var response = await api.UpdateProject(change.ItemId, new JObject { ["due_date"] = day });
                    if (!response.IsSuccess)
                    {
                        context.Log("reprioritise apply: project write failed http " + response.Status);
                        return Tuple.Create(saved, false);
                    }
                }
                else
                {
                    ToDoTask latest = await ReadLatest(api, change.ItemId);
                    ForecastRowGuard.Refusal? refusal = ForecastRowGuard.For(latest, context.PreviousTask(change.ItemId));
                    if (refusal.HasValue)
                    {
                        context.Log("reprioritise apply: refused task=" + change.ItemId + " reason=" + refusal.Value);
                        return Tuple.Create(saved, false);
                    }
                    ToDoTask written = await PutAsync(api, latest, "due_date", day);
                    DateTime? landed = written == null ? null : ForecastSnapshot.ParseDay(written.DueDate);
                    if (written == null || (landed.HasValue && landed.Value != change.ProposedDate.Date))
                    {
                        context.Log("reprioritise apply: task write did not stick task=" + change.ItemId);
                        return Tuple.Create(saved, false);
                    }
                    context.TaskSaved(change);
                }
                saved++;
                context.Log("[user-action] reprioritise: " + F.KindWire(change.Kind) + " due date moved");
            }
            return Tuple.Create(saved, true);
        }

        /// <summary>
        /// A row's outcome (deadline flexibility), on the /10 wire scale. The Mac's <c>writeTaskOutcome</c>:
        /// re-read so the identity fields cannot overwrite a title edited elsewhere, then PUT the outcome
        /// beside them, and count it saved only when the answer carries it.
        /// </summary>
        public static async Task<bool> WriteOutcomeAsync(IReprioritiseApi api, string taskId, int wireOutcome)
        {
            ToDoTask latest = await ReadLatest(api, taskId);
            if (latest == null) return false;
            ToDoTask saved = await PutAsync(api, latest, "outcome", wireOutcome);
            return saved != null && (saved.Id == null || saved.Outcome == wireOutcome);
        }

        /// <summary>A row's estimate, in minutes (<c>duration</c> is sent in seconds).</summary>
        public static async Task<bool> WriteDurationAsync(IReprioritiseApi api, string taskId, int minutes)
        {
            ToDoTask latest = await ReadLatest(api, taskId);
            if (latest == null) return false;
            ToDoTask saved = await PutAsync(api, latest, "duration", minutes * 60);
            return saved != null && (saved.Id == null || saved.Duration == minutes * 60);
        }

        private static async Task<ToDoTask> ReadLatest(IReprioritiseApi api, string id)
        {
            string json = await api.GetToDosByIds(new[] { id });
            return json == null ? null : ToDoJson.ReadTasks(json)?.FirstOrDefault(t => t.Id == id);
        }

        /// <summary>
        /// PUT one field with the identity fields. The saved task the server answers with, an empty
        /// <see cref="ToDoTask"/> when it answered 2xx without one, or null when the write failed.
        /// </summary>
        private static async Task<ToDoTask> PutAsync(IReprioritiseApi api, ToDoTask latest, string key, object value)
        {
            var body = new Dictionary<string, object>
            {
                ["id"] = latest.Id,
                ["title"] = latest.Title,
                ["eisenhower_quadrant"] = latest.EisenhowerQuadrant ?? (int)EisenhowerQuadrant.DoIt,
                [key] = value,
            };
            ApiResponse response = await api.UpsertToDo(body);
            if (response == null || !response.IsSuccess) return null;
            return ToDoJson.ReadTask(response.Body) ?? new ToDoTask();
        }
    }
}
