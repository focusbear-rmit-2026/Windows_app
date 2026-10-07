using Focus_Bear.API;
using Focus_Bear.Utils;
using FocusBear.Core.ToDo;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>The calls the Day Plan's write-sync makes; the app's API client in the app, a fake in tests.</summary>
    internal interface IDayPlanSyncApi
    {
        /// <summary><c>GET /to-do/by-ids</c>: a JSON array of the current rows, or null when the read failed.</summary>
        Task<string> GetToDosByIds(IEnumerable<string> ids);
        Task<bool> SetTodoStatus(string todoId, string title, int quadrant, bool completed);
        Task<bool> SetTodoPlanned(string todoId, string title, int quadrant, DateTime? plannedDate, string blockKey, int? order);
        /// <summary><c>PUT /to-do</c> without an id (a create): the new to-do's id, or null when it failed.</summary>
        Task<string> CreateTodo(JObject body);
        /// <summary><c>PUT /to-do</c> with an id: a partial update of an existing to-do.</summary>
        Task<bool> UpsertTodo(JObject body);
        /// <summary><c>POST /projects { name }</c>: the new project's id, or null.</summary>
        Task<string> CreateProject(string name);
        /// <summary><c>DELETE /to-do?todo_id=</c>.</summary>
        Task<bool> DeleteTodo(string todoId);
    }

    internal sealed class DayPlanSyncApi : IDayPlanSyncApi
    {
        private readonly FocusBearAPIClient _client = new FocusBearAPIClient();
        public Task<string> GetToDosByIds(IEnumerable<string> ids) => _client.GetToDosByIds(ids);
        public Task<bool> SetTodoStatus(string todoId, string title, int quadrant, bool completed) => _client.SetTodoStatus(todoId, title, quadrant, completed);
        public Task<bool> SetTodoPlanned(string todoId, string title, int quadrant, DateTime? plannedDate, string blockKey, int? order) =>
            _client.SetTodoPlanned(todoId, title, quadrant, plannedDate, blockKey, order);

        public async Task<string> CreateTodo(JObject body)
        {
            ApiResponse response = await _client.SendJsonRequest("PUT", FocusBearAPIClient.ToDo, body, "DayPlanSync createTodo");
            return response.IsSuccess ? IdOf(response.Body) : null;
        }

        public async Task<bool> UpsertTodo(JObject body) =>
            (await _client.SendJsonRequest("PUT", FocusBearAPIClient.ToDo, body, "DayPlanSync updateTodo")).IsSuccess;

        public async Task<string> CreateProject(string name)
        {
            ApiResponse response = await _client.CreateToDoProject(name);
            return response.IsSuccess ? IdOf(response.Body) : null;
        }

        public Task<bool> DeleteTodo(string todoId) => _client.DeleteToDo(todoId);

        private static string IdOf(string body) => (ToDoJson.Parse(body) as JObject)?.Value<string>("id");
    }

    /// <summary>
    /// What other devices see of a Day Plan task — the fields an edit is compared on. The store hands out its own
    /// task objects and the screens edit them in place, so "what changed" has to be measured against a copy taken
    /// at the last save, not against the list entry.
    /// </summary>
    public sealed class DayPlanTaskFields
    {
        public string Title { get; private set; }
        public string Project { get; private set; }
        public int? Outcome { get; private set; }
        public int DurationMinutes { get; private set; }
        public string SubtasksKey { get; private set; }
        public bool Done { get; private set; }
        public string BlockKey { get; private set; }

        public static DayPlanTaskFields Of(DayPlanTask t) => new DayPlanTaskFields
        {
            Title = t.Title ?? "",
            Project = (t.Project ?? "").Trim(),
            Outcome = t.Outcome,
            DurationMinutes = t.DurationMinutes,
            SubtasksKey = string.Join("\u001f", t.SubtaskList.Select(s => (s.Done ? "1" : "0") + (s.Title ?? ""))),
            Done = t.Done,
            BlockKey = t.BlockKey,
        };

        public bool TitleChanged(DayPlanTaskFields before) => !string.Equals(Title, before.Title, StringComparison.Ordinal);
        public bool ProjectChanged(DayPlanTaskFields before) => !string.Equals(Project, before.Project, StringComparison.OrdinalIgnoreCase);
        public bool OutcomeChanged(DayPlanTaskFields before) => Outcome != before.Outcome;
        public bool DurationChanged(DayPlanTaskFields before) => DurationMinutes != before.DurationMinutes;
        public bool SubtasksChanged(DayPlanTaskFields before) => !string.Equals(SubtasksKey, before.SubtasksKey, StringComparison.Ordinal);

        /// <summary>True when nothing the to-do carries (title, project, importance, length, checklist) differs.</summary>
        public bool SameShared(DayPlanTaskFields before) =>
            !TitleChanged(before) && !ProjectChanged(before) && !OutcomeChanged(before) && !DurationChanged(before) && !SubtasksChanged(before);
    }

    /// <summary>
    /// The Day Plan write-sync facade — the Windows port of macOS <c>DayPlanSync.shared</c>. The task
    /// store calls these when a task is created, planned/unplanned, completed or edited; they fire the matching
    /// <c>PUT /to-do</c> upsert in the background (best-effort, like the macOS <c>send</c>), so the store
    /// stays synchronous and the UI never blocks on the network.
    ///
    /// A task typed into the Day Plan starts local. Once it has a real name the store creates a to-do for it
    /// (<see cref="CreateBody"/>, the Mac's <c>mintTodoIfNeeded</c>) and from then on it syncs like an imported one
    /// (report 2026-09-28_21-59-10: such tasks never left the computer they were typed on).
    ///
    /// The upsert insists on an Eisenhower quadrant. A task that doesn't carry one (planned before the quadrant
    /// was kept, or a to-do that has none) used to be skipped outright, so ticking it done, deleting it or
    /// snoozing it never reached the server: with the Day Plan filing tasks by due date, the delete and the snooze
    /// were undone at the next open, and the tick was un-ticked by the status reconcile 20 s later. Now its row is
    /// re-read first and the write carries the server's own quadrant (so it can't be clobbered), or the Mac's
    /// neutral quadrant when the to-do has none.
    /// </summary>
    public static class DayPlanSync
    {
        /// <summary>
        /// For a to-do that has no quadrant at all: 1 (Do it), as the web (<c>eisenhower_quadrant || 1</c>), the Mac's
        /// To-do model and the reschedule writes read a missing one. 4 would file it under "not important, not urgent".
        /// </summary>
        public const int NeutralQuadrant = (int)EisenhowerQuadrant.DoIt;

        /// <summary>Where the writes go; a fake in tests.</summary>
        internal static Func<IDayPlanSyncApi> ApiFactory = () => new DayPlanSyncApi();

        /// <summary>The "+ Add task" row's placeholder name (config text); a row still called this isn't created.</summary>
        internal static Func<string> PlaceholderTitle = () => DayPlanText.TasksNamePlaceholder;

        /// <summary>Run after a to-do is created, so the next due-date refresh downloads a catalogue that has it.</summary>
        internal static Action TodoCreated = ToDoCatalogue.Invalidate;

        /// <summary>True when this task is backed by a real to-do.</summary>
        public static bool CanSync(DayPlanTask t) => t != null && !string.IsNullOrEmpty(t.SourceTaskId);

        /// <summary>
        /// True for a task that lives only on this computer and is ready to become a to-do: unfinished, with a real
        /// name — not blank, not the placeholder of a row just added, not a bare "#project" draft waiting for its name.
        /// </summary>
        public static bool NeedsCreate(DayPlanTask t) => t != null && !CanSync(t) && !t.Done && IsRealTitle(t.Title);

        internal static bool IsRealTitle(string title)
        {
            string s = (title ?? "").Trim();
            if (s.Length == 0) return false;
            if (string.Equals(s, "Task name", StringComparison.OrdinalIgnoreCase)) return false;
            string placeholder = null;
            try { placeholder = PlaceholderTitle?.Invoke(); } catch { }
            if (!string.IsNullOrWhiteSpace(placeholder) && string.Equals(s, placeholder.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
            if (s[0] == '#' && !s.Any(char.IsWhiteSpace)) return false;
            return true;
        }

        /// <summary>Reflect a completion flip onto the backend to-do.</summary>
        public static void SetTodoStatus(DayPlanTask t, bool completed) =>
            FireAndForget(t?.SourceTaskId, () => SendStatusAsync(t, completed), "setTodoStatus");

        /// <summary>Plan (or, with <paramref name="plannedDate"/> null, unplan) a to-do onto a day/block.</summary>
        public static void SetTodoPlanned(DayPlanTask t, DateTime? plannedDate, string blockKey, int? order = null) =>
            FireAndForget(t?.SourceTaskId, () => SendPlannedAsync(t, plannedDate, blockKey, order), "setTodoPlanned");

        /// <summary>Push what changed on a to-do-backed task since <paramref name="before"/> (title, project, importance, length, checklist).</summary>
        internal static void SendEdits(DayPlanTask t, DayPlanTaskFields before, Func<string, Task<string>> projectIdFor) =>
            FireAndForget(t?.SourceTaskId, () => SendEditsAsync(t, before, projectIdFor), "updateTodo");

        /// <summary>Run a store-side sync job in the background, logging a failure rather than throwing.</summary>
        internal static void Run(Func<Task<bool>> call, string label) => FireAndForget(call, label);

        internal static async Task<bool> SendStatusAsync(DayPlanTask t, bool completed)
        {
            if (!CanSync(t)) return false;
            IDayPlanSyncApi api = ApiFactory();
            int? quadrant = await QuadrantFor(api, t, keepLocalTitle: false);
            return quadrant.HasValue && await api.SetTodoStatus(t.SourceTaskId, t.Title, quadrant.Value, completed);
        }

        internal static async Task<bool> SendPlannedAsync(DayPlanTask t, DateTime? plannedDate, string blockKey, int? order)
        {
            if (!CanSync(t)) return false;
            IDayPlanSyncApi api = ApiFactory();
            int? quadrant = await QuadrantFor(api, t, keepLocalTitle: false);
            return quadrant.HasValue && await api.SetTodoPlanned(t.SourceTaskId, t.Title, quadrant.Value, plannedDate, blockKey, order);
        }

        internal static async Task<bool> SendEditsAsync(DayPlanTask t, DayPlanTaskFields before, Func<string, Task<string>> projectIdFor)
        {
            if (!CanSync(t) || before == null) return false;
            DayPlanTaskFields now = DayPlanTaskFields.Of(t);
            if (now.SameShared(before)) return true;
            IDayPlanSyncApi api = ApiFactory();
            // The title here is the one just typed: it must not be swapped for the server's while reading the quadrant.
            int? quadrant = await QuadrantFor(api, t, keepLocalTitle: true);
            if (!quadrant.HasValue) return false;

            bool linkProject = false;
            string projectId = null;
            if (now.ProjectChanged(before))
            {
                if (now.Project.Length == 0) linkProject = true;   // cleared: unlink
                else
                {
                    projectId = projectIdFor == null ? null : await projectIdFor(now.Project);
                    linkProject = projectId != null;
                    if (linkProject) t.ProjectId = projectId;
                }
            }
            return await api.UpsertTodo(EditBody(t, quadrant.Value, before, linkProject, projectId));
        }

        /// <summary>
        /// The create for a local task (Mac mintTodoIfNeeded, in one request): planned and due on its day. With
        /// <paramref name="todoId"/> the row is created under that id (the backend treats an unknown id as a create),
        /// so a retry after a lost reply updates it rather than adding another (#1646).
        /// </summary>
        internal static JObject CreateBody(DayPlanTask t, DateTime date, int quadrant, string projectId, string todoId = null)
        {
            string day = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var body = new JObject();
            if (!string.IsNullOrEmpty(todoId)) body["id"] = todoId;
            body.Merge(new JObject
            {
                ["title"] = (t.Title ?? "").Trim(),
                ["eisenhower_quadrant"] = quadrant,
                // Due on the day it was planned: the Day Plan files synced tasks by due date on every device.
                ["due_date"] = day,
                ["planned_date"] = day,
                ["duration"] = t.DurationMinutes * 60,
            });
            if (!string.IsNullOrEmpty(t.BlockKey)) body["planned_block_key"] = t.BlockKey;
            if (t.Outcome.HasValue) body["outcome"] = t.Outcome.Value;
            if (projectId != null) body["project_id"] = projectId;
            if (!string.IsNullOrWhiteSpace(t.Notes)) body["details"] = t.Notes;
            if (t.SubtaskList.Count > 0) body["subtasks"] = SubtasksJson(t);
            return body;
        }

        /// <summary>A partial update: id, title and quadrant (the DTO requires them) plus only the fields that changed.</summary>
        internal static JObject EditBody(DayPlanTask t, int quadrant, DayPlanTaskFields before, bool linkProject, string projectId)
        {
            DayPlanTaskFields now = DayPlanTaskFields.Of(t);
            var body = new JObject
            {
                ["id"] = t.SourceTaskId,
                ["title"] = t.Title,
                ["eisenhower_quadrant"] = quadrant,
            };
            if (now.DurationChanged(before)) body["duration"] = t.DurationMinutes * 60;
            if (now.OutcomeChanged(before)) body["outcome"] = t.Outcome.HasValue ? (JToken)t.Outcome.Value : JValue.CreateNull();
            if (now.SubtasksChanged(before)) body["subtasks"] = SubtasksJson(t);
            if (linkProject) body["project_id"] = projectId != null ? (JToken)projectId : JValue.CreateNull();
            return body;
        }

        /// <summary>The backend's subtask shape, <c>{ name, is_completed }</c> (Mac setTodoSubtasks).</summary>
        private static JArray SubtasksJson(DayPlanTask t) =>
            new JArray(t.SubtaskList.Select(s => new JObject { ["name"] = s.Title ?? "", ["is_completed"] = s.Done }));

        /// <summary>
        /// The task's own quadrant; else the server's (read now, and kept on the task); else the neutral one. Null
        /// when the row couldn't be read or no longer exists: then nothing is written.
        /// </summary>
        private static async Task<int?> QuadrantFor(IDayPlanSyncApi api, DayPlanTask t, bool keepLocalTitle)
        {
            if (t.EisenhowerQuadrant.HasValue) return t.EisenhowerQuadrant.Value;
            string json = await api.GetToDosByIds(new[] { t.SourceTaskId });
            ToDoTask latest = json == null ? null : ToDoJson.ReadTasks(json)?.FirstOrDefault(r => r.Id == t.SourceTaskId);
            if (latest == null) { LogWriter.LogWrite("DayPlanSync: to-do not readable, write skipped"); return null; }
            if (latest.EisenhowerQuadrant.HasValue) t.EisenhowerQuadrant = latest.EisenhowerQuadrant;
            // The write sends a title: the server's, which may have been renamed on the web since the Day Plan read it.
            if (!keepLocalTitle && !string.IsNullOrWhiteSpace(latest.Title)) t.Title = latest.Title;
            return latest.EisenhowerQuadrant ?? NeutralQuadrant;
        }

        private static async void FireAndForget(Func<Task<bool>> call, string label)
        {
            try { await call(); }
            catch (Exception ex) { LogWriter.LogWrite("DayPlanSync " + label + " - " + ex.Message); }
        }

        // The writes in flight per to-do, so each one starts after the previous one has finished (#1646): the backend
        // saves a PUT as the stored row with the body laid over it, and two concurrent writes to one to-do (the details
        // window's Save plus a Move, say) could each save the other's stale fields.
        private static readonly Dictionary<string, Task> _chains = new Dictionary<string, Task>();
        private static readonly object _chainLock = new object();

        private static void FireAndForget(string todoId, Func<Task<bool>> call, string label)
        {
            if (string.IsNullOrEmpty(todoId)) { FireAndForget(call, label); return; }
            var done = new TaskCompletionSource<bool>();
            Task previous;
            lock (_chainLock)
            {
                _chains.TryGetValue(todoId, out previous);
                _chains[todoId] = done.Task;
            }
            RunAfter(previous, call, label, done, todoId);
        }

        private static async void RunAfter(Task previous, Func<Task<bool>> call, string label, TaskCompletionSource<bool> done, string todoId)
        {
            try
            {
                if (previous != null) await previous;
                await call();
            }
            catch (Exception ex) { LogWriter.LogWrite("DayPlanSync " + label + " - " + ex.Message); }
            finally
            {
                done.TrySetResult(true);
                lock (_chainLock)
                {
                    if (_chains.TryGetValue(todoId, out Task current) && current == done.Task) _chains.Remove(todoId);
                }
            }
        }

        /// <summary>Tests: whether a write to this to-do is still in flight.</summary>
        internal static bool HasWriteInFlight(string todoId)
        {
            lock (_chainLock) return todoId != null && _chains.ContainsKey(todoId);
        }
    }
}
