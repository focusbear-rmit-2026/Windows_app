using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>One checklist sub-task on a <see cref="DayPlanTask"/> (macOS <c>DayPlanSubtask</c>).</summary>
    public class DayPlanSubtask
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "";
        public bool Done { get; set; }

        public DayPlanSubtask() { }
        public DayPlanSubtask(string title, bool done = false) { Title = title; Done = done; }
    }

    /// <summary>
    /// One planned task for a given day — the Windows port of the macOS <c>DayPlanTask</c> struct.
    /// <c>SourceTaskId</c> links back to a backend to-do item when the task was imported from the
    /// user's to-do list; <c>BlockKey</c> nests it under a schedule block (a habit row keyed
    /// <c>"habit:&lt;title&gt;"</c>, a deep-work sprint <c>"deepwork:&lt;epoch&gt;"</c>, or a meeting
    /// <c>"event:&lt;id&gt;"</c>). A null <c>BlockKey</c> means it sits loose in the tasks panel.
    /// </summary>
    public class DayPlanTask
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "";
        public int DurationMinutes { get; set; } = 15;
        public bool Done { get; set; }
        /// <summary>Backend to-do id this task mirrors (null for from-scratch tasks).</summary>
        public string SourceTaskId { get; set; }
        /// <summary>Schedule-block association; null = loose in the panel.</summary>
        public string BlockKey { get; set; }
        /// <summary>Project name, shown as a grouping header: the to-do's <c>project_id</c> resolved
        /// against /projects, else its first tag (FocusBear.Core TodoProjects), or typed here.</summary>
        public string Project { get; set; }
        /// <summary>The backend project the backing to-do was last seen linked to (<c>project_id</c>);
        /// null = none / unknown. Lets a rename or relink made elsewhere follow through (Mac projectID).</summary>
        public string ProjectId { get; set; }
        /// <summary>Longer description, editable in the task detail modal.</summary>
        public string Notes { get; set; }
        /// <summary>Checklist sub-tasks (null/empty for tasks without any).</summary>
        public List<DayPlanSubtask> Subtasks { get; set; }
        /// <summary>Importance / "outcome" (1–10, higher = more important). null = unset.</summary>
        public int? Outcome { get; set; }
        /// <summary>Eisenhower quadrant captured from the to-do (needed for backend sync).</summary>
        public int? EisenhowerQuadrant { get; set; }

        /// <summary>When the done-state was last changed locally (in-memory, not persisted). The
        /// backend status reconcile skips a task touched within the last few seconds so a stale
        /// to-do fetch can't revert a fresh local tick before our write lands.</summary>
        [JsonIgnore]
        public DateTime StatusTouchedUtc { get; set; } = DateTime.MinValue;

        /// <summary>Sub-tasks as a non-null list for convenient counting/rendering.</summary>
        [JsonIgnore]
        public List<DayPlanSubtask> SubtaskList => Subtasks ?? new List<DayPlanSubtask>();

        public DayPlanTask() { }

        public DayPlanTask(string title, int durationMinutes = 15, bool done = false,
            string sourceTaskId = null, string blockKey = null, string project = null,
            int? outcome = null, int? eisenhowerQuadrant = null)
        {
            Title = title;
            DurationMinutes = durationMinutes;
            Done = done;
            SourceTaskId = sourceTaskId;
            BlockKey = blockKey;
            Project = project;
            Outcome = outcome;
            EisenhowerQuadrant = eisenhowerQuadrant;
        }
    }

    /// <summary>
    /// The day-keyed task store — the Windows port of macOS <c>DayPlanTaskStore</c>. A process-wide
    /// singleton holding tasks bucketed by <c>yyyy-MM-dd</c>, persisted locally (registry) so
    /// associations survive a restart. To-do-backed tasks sync through <see cref="DayPlanSync"/>; a task typed
    /// here gets a to-do created for it once it has a name (<see cref="CreateTodoFor"/>), so it reaches the
    /// user's other devices too.
    /// </summary>
    public sealed class DayPlanTaskStore
    {
        private static readonly DayPlanTaskStore _shared = new DayPlanTaskStore();
        public static DayPlanTaskStore Shared => _shared;

        private Dictionary<string, List<DayPlanTask>> _byDay = new Dictionary<string, List<DayPlanTask>>();
        private bool _isRollingOver;
        // In-memory guard so the once-per-day rollover check doesn't hit the registry on every Tasks()
        // call (Tasks() runs once per habit row per render — the registry reads added up to real lag).
        private string _rolledOverDay;

        // Where the store lives: the registry in the app, memory in tests.
        private readonly Func<string> _loadJson = RegistryManager.LoadDayPlanTasks;
        private readonly Action<string> _saveJson = RegistryManager.SaveDayPlanTasks;
        private readonly Func<string> _loadRolloverDay = RegistryManager.LoadDayPlanLastRolloverDay;
        private readonly Action<string> _saveRolloverDay = RegistryManager.SaveDayPlanLastRolloverDay;

        private DayPlanTaskStore() { Load(); }

        /// <summary>A store of its own over in-memory storage (tests); the app uses <see cref="Shared"/>.</summary>
        internal DayPlanTaskStore(string json, Action<string> saved, string rolledOverDay = null)
        {
            string rolled = rolledOverDay;
            _loadJson = () => json;
            _saveJson = saved ?? (_ => { });
            _loadRolloverDay = () => rolled;
            _saveRolloverDay = day => rolled = day;
            Load();
        }

        private static string DayKey(DateTime date) => FocusBear.Core.Schedule.DayPlanDayKey.For(date);

        // ---- persistence -------------------------------------------------------------------------

        private void Load()
        {
            try
            {
                string json = _loadJson();
                if (!string.IsNullOrEmpty(json))
                {
                    var loaded = JsonConvert.DeserializeObject<Dictionary<string, List<DayPlanTask>>>(json);
                    if (loaded != null) _byDay = loaded;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DayPlanTaskStore.Load - " + ex.Message);
                _byDay = new Dictionary<string, List<DayPlanTask>>();
            }
            RememberSaved();
        }

        private void Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_byDay);
                _saveJson(json);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DayPlanTaskStore.Save - " + ex.Message);
            }
            RememberSaved();
        }

        // ---- write-sync bookkeeping --------------------------------------------------------------

        // Each task as it was at the last save. The screens edit the store's own objects in place and then call
        // Update, so comparing with the list entry saw no change: a rename, a new project, a length or a tick made
        // that way never reached the server.
        private Dictionary<string, DayPlanTaskFields> _saved = new Dictionary<string, DayPlanTaskFields>();
        // Local tasks whose to-do create is in flight (one create per task).
        private readonly HashSet<string> _creating = new HashSet<string>();
        // To-dos this store just created, and when: the unfinished catalogue can be older than the create.
        private readonly Dictionary<string, DateTime> _createdUtc = new Dictionary<string, DateTime>();
        private const int FreshCreateGraceSeconds = 300;
        // Project creates in flight by lower-cased name, so two tasks given the same new project share one.
        private readonly Dictionary<string, System.Threading.Tasks.Task<string>> _projectCreates =
            new Dictionary<string, System.Threading.Tasks.Task<string>>();
        private bool _remoteProjectsLoaded;

        private void RememberSaved()
        {
            var saved = new Dictionary<string, DayPlanTaskFields>();
            foreach (List<DayPlanTask> list in _byDay.Values)
                foreach (DayPlanTask t in list)
                    if (t?.Id != null) saved[t.Id] = DayPlanTaskFields.Of(t);
            _saved = saved;
        }

        private DayPlanTaskFields SavedFields(string id) =>
            id != null && _saved.TryGetValue(id, out DayPlanTaskFields f) ? f : null;

        /// <summary>Send an edit to the task's to-do, or create the to-do when the task has none yet.</summary>
        private void SyncEdits(DayPlanTask task, DateTime date, DayPlanTaskFields before)
        {
            if (!DayPlanSync.CanSync(task))
            {
                if (DayPlanSync.NeedsCreate(task)) CreateTodoFor(task, date);
                return;
            }
            if (before != null && !DayPlanTaskFields.Of(task).SameShared(before))
                DayPlanSync.SendEdits(task, before, ProjectIdForAsync);
        }

        /// <summary>
        /// Create a to-do for a task typed into the Day Plan (the Mac's mintTodoIfNeeded), planned and due on its day,
        /// then back the task with it. Best-effort: when it fails (offline) the task stays local and the next edit,
        /// or the next open's <see cref="SyncLocalOnlyTasks"/>, tries again.
        /// </summary>
        private void CreateTodoFor(DayPlanTask task, DateTime date)
        {
            if (!DayPlanSync.NeedsCreate(task) || !_creating.Add(task.Id)) return;
            string localId = task.Id;
            DayPlanTaskFields sent = DayPlanTaskFields.Of(task);
            int quadrant = task.EisenhowerQuadrant ?? DayPlanSync.NeutralQuadrant;
            DayPlanSync.Run(async () =>
            {
                string todoId = null, projectId = null;
                try
                {
                    if (sent.Project.Length > 0) projectId = await ProjectIdForAsync(sent.Project);
                    todoId = await DayPlanSync.ApiFactory().CreateTodo(DayPlanSync.CreateBody(task, date, quadrant, projectId));
                }
                finally { _creating.Remove(localId); }
                if (todoId == null)
                {
                    LogWriter.LogWrite("DayPlanSync: to-do create failed - the task stays on this computer until it is next edited or the Day Plan reopens");
                    return false;
                }
                await OnTodoCreated(localId, todoId, quadrant, projectId, sent, date);
                return true;
            }, "createTodo");
        }

        private async System.Threading.Tasks.Task OnTodoCreated(string localId, string todoId, int quadrant, string projectId,
                                                                DayPlanTaskFields sent, DateTime date)
        {
            string dayKey = null;
            DayPlanTask t = null;
            foreach (var kv in _byDay)
            {
                t = kv.Value.FirstOrDefault(x => x.Id == localId);
                if (t != null) { dayKey = kv.Key; break; }
            }
            if (t == null)
            {
                // Deleted while the create was in flight: take the new to-do back out rather than leave it on other devices.
                LogWriter.LogWrite("DayPlanSync: task removed before its to-do was created - deleting " + todoId);
                await DayPlanSync.ApiFactory().DeleteTodo(todoId);
                return;
            }
            if (DayPlanSync.CanSync(t)) return;

            t.SourceTaskId = todoId;
            if (!t.EisenhowerQuadrant.HasValue) t.EisenhowerQuadrant = quadrant;
            if (projectId != null && string.Equals((t.Project ?? "").Trim(), sent.Project, StringComparison.OrdinalIgnoreCase)) t.ProjectId = projectId;
            _createdUtc[todoId] = DateTime.UtcNow;
            Save();
            try { DayPlanSync.TodoCreated?.Invoke(); } catch { }
            LogWriter.LogWrite("DayPlanSync: created to-do " + todoId + " for a task typed into the Day Plan");

            // Catch up on what happened while the create was in flight.
            DayPlanTaskFields now = DayPlanTaskFields.Of(t);
            if (!now.SameShared(sent)) DayPlanSync.SendEdits(t, sent, ProjectIdForAsync);
            if (now.Done != sent.Done) DayPlanSync.SetTodoStatus(t, now.Done);
            if (dayKey != DayKey(date) || !string.Equals(now.BlockKey, sent.BlockKey, StringComparison.Ordinal))
            {
                DateTime day;
                if (DateTime.TryParseExact(dayKey, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day))
                    DayPlanSync.SetTodoPlanned(t, day, t.BlockKey);
            }
        }

        /// <summary>
        /// The id of the user's project with this name (any case); a new project when there is none. Null when it
        /// can't be known: the project list hasn't loaded (creating then could duplicate one of theirs) or the create failed.
        /// </summary>
        internal async System.Threading.Tasks.Task<string> ProjectIdForAsync(string name)
        {
            string n = (name ?? "").Trim();
            if (n.Length == 0) return null;
            foreach (var kv in RemoteProjectsById)
                if (string.Equals((kv.Value ?? "").Trim(), n, StringComparison.OrdinalIgnoreCase)) return kv.Key;
            if (!_remoteProjectsLoaded) return null;
            string key = n.ToLowerInvariant();
            if (!_projectCreates.TryGetValue(key, out System.Threading.Tasks.Task<string> pending))
            {
                pending = DayPlanSync.ApiFactory().CreateProject(n);
                _projectCreates[key] = pending;
            }
            string id;
            try { id = await pending; }
            finally { _projectCreates.Remove(key); }
            if (id != null && !RemoteProjectsById.ContainsKey(id))
            {
                RemoteProjectsById = new Dictionary<string, string>(RemoteProjectsById) { [id] = n };
                if (!RemoteProjectNames.Contains(n, StringComparer.OrdinalIgnoreCase)) RemoteProjectNames = RemoteProjectNames.Concat(new[] { n }).ToList();
            }
            return id;
        }

        /// <summary>
        /// Create to-dos for the unfinished tasks from today on that exist only on this computer — typed before this
        /// sync existed, or while offline. Finished history is left alone. Returns how many creates were started.
        /// </summary>
        public int SyncLocalOnlyTasks(DateTime today)
        {
            string todayKey = DayKey(today);
            int started = 0;
            foreach (var kv in _byDay.ToList())
            {
                if (string.CompareOrdinal(kv.Key, todayKey) < 0) continue;
                DateTime day;
                if (!DateTime.TryParseExact(kv.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day)) continue;
                foreach (DayPlanTask t in kv.Value.ToList())
                {
                    if (!DayPlanSync.NeedsCreate(t) || _creating.Contains(t.Id)) continue;
                    CreateTodoFor(t, day);
                    started++;
                }
            }
            if (started > 0) LogWriter.LogWrite("DayPlanSync: creating to-dos for " + started + " task(s) that were only on this computer");
            return started;
        }

        /// <summary>A to-do this store created within the grace period — possibly newer than the catalogue in hand.</summary>
        private bool CreatedRecently(string todoId) =>
            todoId != null && _createdUtc.TryGetValue(todoId, out DateTime at) && (DateTime.UtcNow - at).TotalSeconds < FreshCreateGraceSeconds;

        // ---- rollover ----------------------------------------------------------------------------

        /// <summary>
        /// Carry incomplete tasks forward from past days to today, once per day. Completed tasks stay
        /// in their original day (history); rolled tasks lose their time-box (yesterday's blocks are
        /// gone) and land loose in the panel. Idempotent — guarded by a stored last-rollover day.
        /// </summary>
        private void RollOverIfNeeded()
        {
            if (_isRollingOver) return;
            string todayKey = DayKey(DateTime.Today);
            if (_rolledOverDay == todayKey) return;   // already handled this session-day (no registry read)
            if (_loadRolloverDay() == todayKey) { _rolledOverDay = todayKey; return; }
            _isRollingOver = true;
            try
            {
                bool changed = false;
                foreach (string dayK in _byDay.Keys.Where(k => string.CompareOrdinal(k, todayKey) < 0).ToList())
                {
                    List<DayPlanTask> tasks = _byDay[dayK];
                    List<DayPlanTask> incomplete = tasks.Where(t => !t.Done).ToList();
                    if (incomplete.Count == 0) continue;
                    if (!_byDay.TryGetValue(todayKey, out List<DayPlanTask> todayList))
                    {
                        todayList = new List<DayPlanTask>();
                        _byDay[todayKey] = todayList;
                    }
                    foreach (DayPlanTask t in incomplete)
                    {
                        // Don't duplicate an imported to-do that's already on today.
                        if (!string.IsNullOrEmpty(t.SourceTaskId) &&
                            todayList.Any(x => x.SourceTaskId == t.SourceTaskId)) continue;
                        t.BlockKey = null; // re-plan onto today, untimed
                        todayList.Add(t);
                    }
                    _byDay[dayK].RemoveAll(t => !t.Done);
                    changed = true;
                }
                _saveRolloverDay(todayKey);
                _rolledOverDay = todayKey;
                if (changed) Save();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DayPlanTaskStore.RollOverIfNeeded - " + ex.Message);
            }
            finally
            {
                _isRollingOver = false;
            }
        }

        // ---- queries -----------------------------------------------------------------------------

        public List<DayPlanTask> Tasks(DateTime date)
        {
            RollOverIfNeeded();
            return _byDay.TryGetValue(DayKey(date), out List<DayPlanTask> list) ? list : new List<DayPlanTask>();
        }

        /// <summary>Tasks not nested under any schedule block (shown loose in the panel).</summary>
        public List<DayPlanTask> UnassignedTasks(DateTime date) =>
            Tasks(date).Where(t => t.BlockKey == null).ToList();

        /// <summary>Tasks nested under a given schedule block (shown under that block's card).</summary>
        public List<DayPlanTask> TasksAssignedTo(string key, DateTime date) =>
            Tasks(date).Where(t => t.BlockKey == key).ToList();

        public DayPlanTask TaskWithId(string id, DateTime date) =>
            Tasks(date).FirstOrDefault(t => t.Id == id);

        /// <summary>Every stored task with its day — what the reprioritise forecast prices (Mac forecastSnapshot).</summary>
        public List<KeyValuePair<DateTime, DayPlanTask>> AllPlanned()
        {
            var result = new List<KeyValuePair<DateTime, DayPlanTask>>();
            foreach (var kv in _byDay)
            {
                DateTime day;
                if (!DateTime.TryParseExact(kv.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day)) continue;
                foreach (DayPlanTask t in kv.Value) result.Add(new KeyValuePair<DateTime, DayPlanTask>(day, t));
            }
            return result;
        }

        /// <summary>The latest unfinished copy of a to-do on the plan, with its day (Mac plannedCopy(ofSourceTask:)).</summary>
        public KeyValuePair<DateTime, DayPlanTask>? PlannedCopy(string sourceTaskId)
        {
            if (string.IsNullOrEmpty(sourceTaskId)) return null;
            var copies = AllPlanned().Where(p => p.Value.SourceTaskId == sourceTaskId && !p.Value.Done).OrderByDescending(p => p.Key).ToList();
            return copies.Count == 0 ? (KeyValuePair<DateTime, DayPlanTask>?)null : copies[0];
        }

        public bool Contains(string sourceTaskId, DateTime date) =>
            Tasks(date).Any(t => t.SourceTaskId == sourceTaskId);

        // ---- mutations ---------------------------------------------------------------------------

        public void Add(DayPlanTask task, DateTime date)
        {
            // Never add the same imported to-do twice on a day.
            if (!string.IsNullOrEmpty(task.SourceTaskId) && Contains(task.SourceTaskId, date)) return;
            string k = DayKey(date);
            if (!_byDay.TryGetValue(k, out List<DayPlanTask> list))
            {
                list = new List<DayPlanTask>();
                _byDay[k] = list;
            }
            list.Add(task);
            Save();
            // Plan the backing to-do onto this day so it hydrates on other devices (write sync).
            DayPlanSync.SetTodoPlanned(task, date, task.BlockKey);
            // A task added with a real name and no to-do behind it (the AI buddy's "save for later") gets one now.
            if (DayPlanSync.NeedsCreate(task)) CreateTodoFor(task, date);
        }

        /// <param name="alreadyOnServer">The caller wrote this change to the to-do itself (the reprioritise sheet).</param>
        public void Update(DayPlanTask task, DateTime date, bool alreadyOnServer = false)
        {
            string k = DayKey(date);
            if (!_byDay.TryGetValue(k, out List<DayPlanTask> list)) return;
            int i = list.FindIndex(t => t.Id == task.Id);
            if (i < 0) return;
            // Against the last save, not list[i]: callers usually edit the store's own object and pass it back.
            DayPlanTaskFields before = SavedFields(task.Id);
            bool wasDone = before?.Done ?? list[i].Done;
            list[i] = task;
            Save();
            // Reflect a completion flip onto the backend to-do (only on an actual change).
            if (task.Done != wasDone) { task.StatusTouchedUtc = DateTime.UtcNow; DayPlanSync.SetTodoStatus(task, task.Done); }
            // A rename, project, length, importance or checklist change; or the first real name of a new task.
            if (!alreadyOnServer) SyncEdits(task, date, before);
        }

        public void Delete(string id, DateTime date)
        {
            string k = DayKey(date);
            if (!_byDay.TryGetValue(k, out List<DayPlanTask> list)) return;
            DayPlanTask removed = list.FirstOrDefault(t => t.Id == id);
            list.RemoveAll(t => t.Id == id);
            Save();
            // Removing a to-do-backed task from the Day Plan just unplans it (the to-do itself is kept).
            if (removed != null) DayPlanSync.SetTodoPlanned(removed, null, null);
        }

        /// <summary>Nest a task under a block (key) or pull it back to the panel (null).</summary>
        public void SetBlockKey(string key, string taskId, DateTime date)
        {
            string k = DayKey(date);
            if (!_byDay.TryGetValue(k, out List<DayPlanTask> list)) return;
            int i = list.FindIndex(t => t.Id == taskId);
            if (i < 0) return;
            list[i].BlockKey = key;
            Save();
            // Reflect the new block association (and keep the task planned on this day) to the backend.
            DayPlanSync.SetTodoPlanned(list[i], date, key);
        }

        public void SetDone(string taskId, bool done, DateTime date)
        {
            string k = DayKey(date);
            if (!_byDay.TryGetValue(k, out List<DayPlanTask> list)) return;
            int i = list.FindIndex(t => t.Id == taskId);
            if (i < 0) return;
            bool wasDone = list[i].Done;
            list[i].Done = done;
            Save();
            if (done != wasDone) { list[i].StatusTouchedUtc = DateTime.UtcNow; DayPlanSync.SetTodoStatus(list[i], done); }
        }

        /// <summary>
        /// Reorder within a day: move <paramref name="draggedId"/> to just before <paramref name="targetId"/>
        /// in the stored list (the list order IS the panel order). If they're in different project groups,
        /// the dragged task adopts the target's project so it lands in that group where dropped.
        /// </summary>
        public void MoveTaskBefore(string draggedId, string targetId, DateTime date)
        {
            if (draggedId == targetId) return;
            string k = DayKey(date);
            if (!_byDay.TryGetValue(k, out List<DayPlanTask> list)) return;
            int di = list.FindIndex(t => t.Id == draggedId);
            int ti = list.FindIndex(t => t.Id == targetId);
            if (di < 0 || ti < 0) return;

            DayPlanTask dragged = list[di];
            DayPlanTask target = list[ti];
            DayPlanTaskFields before = SavedFields(dragged.Id);
            // Dropping across groups moves the task into the target's project.
            bool projectChanged = !string.Equals(dragged.Project ?? "", target.Project ?? "", StringComparison.OrdinalIgnoreCase);
            dragged.Project = target.Project;

            list.RemoveAt(di);
            ti = list.FindIndex(t => t.Id == targetId);   // recompute after removal
            list.Insert(ti, dragged);
            Save();

            // Best-effort backend reflect: keep it planned on the day (and, if it changed group, the new project).
            if (projectChanged)
            {
                DayPlanSync.SetTodoPlanned(dragged, date, dragged.BlockKey);
                SyncEdits(dragged, date, before);
            }
        }

        /// <summary>Add a from-scratch task pre-filled with a project (the project header "+" button).</summary>
        public DayPlanTask AddInProject(string project, DateTime date)
        {
            var task = new DayPlanTask(DayPlanText.TasksNamePlaceholder, 15)
            {
                Project = string.IsNullOrWhiteSpace(project) ? null : project.Trim()
            };
            string k = DayKey(date);
            if (!_byDay.TryGetValue(k, out List<DayPlanTask> list))
            {
                list = new List<DayPlanTask>();
                _byDay[k] = list;
            }
            list.Add(task);
            Save();
            return task;
        }

        /// <summary>Move a task to another day: pull it from its current day, drop its time-box, add to
        /// the target day. Mirrors the macOS snooze (a moved task re-plans onto the new day, untimed).</summary>
        public void MoveTaskToDay(string taskId, DateTime fromDate, DateTime toDate)
        {
            string fromK = DayKey(fromDate), toK = DayKey(toDate);
            if (!_byDay.TryGetValue(fromK, out List<DayPlanTask> fromList)) return;
            int i = fromList.FindIndex(x => x.Id == taskId);
            if (i < 0) return;
            DayPlanTask moved = fromList[i];
            fromList.RemoveAt(i);
            moved.BlockKey = null;   // the old day's blocks are gone
            if (!_byDay.TryGetValue(toK, out List<DayPlanTask> toList))
            {
                toList = new List<DayPlanTask>();
                _byDay[toK] = toList;
            }
            toList.Add(moved);
            Save();
            // Re-plan the backing to-do onto the new day (untimed), so the move crosses devices.
            DayPlanSync.SetTodoPlanned(moved, toDate, null);
        }

        /// <summary>
        /// Reconcile the backend's planned to-dos for a day into the local store (the cross-device
        /// READ half of Day Plan sync) — the Windows port of the macOS <c>hydrate(fromDay:)</c>. Matches
        /// by <c>SourceTaskId == to-do id</c>. Backend <c>status</c> can mark a task done (upgrade-only —
        /// it must NOT revert a local completion), and a planned to-do we don't have locally is brought
        /// onto the day. Local from-scratch tasks (no SourceTaskId) are never touched. Returns true if
        /// anything changed.
        /// </summary>
        public bool HydrateFromDay(string rawJson, DateTime date)
        {
            if (string.IsNullOrWhiteSpace(rawJson)) return false;
            try
            {
                JToken root = JToken.Parse(rawJson);
                JArray arr = (root as JObject)?["tasks"] as JArray ?? root as JArray;
                if (arr == null) return false;

                string k = DayKey(date);
                if (!_byDay.TryGetValue(k, out List<DayPlanTask> list))
                {
                    list = new List<DayPlanTask>();
                    _byDay[k] = list;
                }
                var indexBySource = new Dictionary<string, int>();
                for (int i = 0; i < list.Count; i++)
                    if (!string.IsNullOrEmpty(list[i].SourceTaskId)) indexBySource[list[i].SourceTaskId] = i;
                // A to-do lives on one day (Mac hydrate(fromDay:) placedOnOtherDay): the backend's planned_date lags
                // a snooze, a move or the rollover, so the day's payload can still list a task that is live on
                // another day here. Re-adding it stranded a copy on both days. A finished copy is history, not a
                // placement, so it doesn't count.
                var placedOnOtherDay = new HashSet<string>(_byDay.Where(kv => kv.Key != k).SelectMany(kv => kv.Value)
                    .Where(t => !t.Done && !string.IsNullOrEmpty(t.SourceTaskId)).Select(t => t.SourceTaskId));

                bool changed = false;
                foreach (JToken t in arr)
                {
                    if (!(t is JObject o)) continue;
                    string id = o.Value<string>("id");
                    if (string.IsNullOrEmpty(id)) continue;
                    bool done = string.Equals(o.Value<string>("status"), "COMPLETED", StringComparison.OrdinalIgnoreCase);

                    if (indexBySource.TryGetValue(id, out int idx))
                    {
                        // Upgrade-only: never revert a local completion (the reflect can lag).
                        if (done && !list[idx].Done) { list[idx].Done = true; changed = true; }
                        // The to-do's title is the truth (a rename elsewhere); last write wins, as on the Mac.
                        string backendTitle = o.Value<string>("title");
                        if (!string.IsNullOrEmpty(backendTitle) && backendTitle != list[idx].Title) { list[idx].Title = backendTitle; changed = true; }
                        // Backfill a quadrant the task was imported without, so its writes carry the real one.
                        if (!list[idx].EisenhowerQuadrant.HasValue && o["eisenhower_quadrant"] != null && o["eisenhower_quadrant"].Type == JTokenType.Integer)
                        { list[idx].EisenhowerQuadrant = (int)o["eisenhower_quadrant"]; changed = true; }
                        if (RefreshProject(list[idx], o)) changed = true;
                    }
                    else if (!placedOnOtherDay.Contains(id))
                    {
                        int minutes = DayPlanDurations.FromTodoDuration(o["duration"]);
                        string blockKey = o.Value<string>("planned_block_key");
                        // project_id resolved against /projects, else the first tag (Mac projectName(for:)).
                        string project = FocusBear.Core.Schedule.TodoProjects.ProjectName(o, RemoteProjectsById);
                        int? quadrant = null;
                        JToken q = o["eisenhower_quadrant"];
                        if (q != null && q.Type != JTokenType.Null && int.TryParse(q.ToString(), out int qv)) quadrant = qv;

                        list.Add(new DayPlanTask(o.Value<string>("title") ?? "Task", minutes, done,
                            sourceTaskId: id, blockKey: blockKey, project: project, eisenhowerQuadrant: quadrant)
                        { ProjectId = FocusBear.Core.Schedule.TodoProjects.ProjectId(o) });
                        changed = true;
                    }
                }
                if (changed) Save();
                return changed;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DayPlanTaskStore.HydrateFromDay - " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Re-files every synced, unfinished task by its DUE DATE (report 2026-09-25_19-54-42: "the day plan
        /// scheduling is wrong. It should use task due date") — the Mac's <c>mergeDueTodos</c>. The complete
        /// unfinished catalogue comes in; each to-do lands on its due day, or today once that day has
        /// passed (<see cref="FocusBear.Core.Schedule.DayPlanDueProjection"/>). Local from-scratch tasks
        /// and finished history stay exactly where they are; a task ticked off today is not resurfaced by
        /// a row that hasn't caught up. A task that stays on its day keeps its block, duration and order.
        /// Returns true if anything changed. Writes nothing to the server.
        /// </summary>
        public bool MergeDueTodos(IEnumerable<FocusBear.Core.ToDo.ToDoTask> todos, DateTime now, string meId, bool persist = true)
        {
            try
            {
                string todayKey = DayKey(now);
                // The most recent placement of each synced task, searched newest day first (Mac `prior`).
                var prior = _byDay.OrderByDescending(kv => kv.Key, StringComparer.Ordinal)
                                  .SelectMany(kv => kv.Value.Select(t => new KeyValuePair<string, DayPlanTask>(kv.Key, t)))
                                  .ToList();
                var rows = new List<KeyValuePair<string, DayPlanTask>>();
                foreach (var todo in todos ?? Enumerable.Empty<FocusBear.Core.ToDo.ToDoTask>())
                {
                    if (string.IsNullOrEmpty(todo.Id)) continue;
                    string due = FocusBear.Core.Schedule.DayPlanDueProjection.CanonicalDay(todo.DueDate, todayKey);
                    if (due == null) continue;
                    string assignee = FocusBear.Core.ToDo.ToDoAssigneeChoices.AssigneeId(todo.AssigneeId, todo.Assignee?.Id);
                    if (FocusBear.Core.ToDo.ToDoAssigneeChoices.IsAssignedToSomebodyElse(assignee, meId)) continue;
                    var old = prior.FirstOrDefault(p => p.Value.SourceTaskId == todo.Id && !p.Value.Done);
                    DayPlanTask task = old.Value != null ? CloneForMerge(old.Value) : new DayPlanTask(todo.Title ?? "Task", sourceTaskId: todo.Id);
                    task.Title = todo.Title ?? task.Title;
                    if (old.Value == null) task.DurationMinutes = DayPlanDurations.FromTodoDuration(todo.Duration.HasValue ? (Newtonsoft.Json.Linq.JToken)todo.Duration.Value : null);
                    task.Done = false;
                    if (old.Key != due) task.BlockKey = null;   // yesterday's block is gone on the new day
                    task.EisenhowerQuadrant = todo.EisenhowerQuadrant ?? task.EisenhowerQuadrant;
                    task.Outcome = todo.Outcome ?? task.Outcome;
                    if (todo.Subtasks != null && todo.Subtasks.Count > 0)
                        task.Subtasks = todo.Subtasks.Select(st => new DayPlanSubtask(st.Name, st.IsCompleted) { Id = st.Id ?? Guid.NewGuid().ToString() }).ToList();
                    var fields = new Newtonsoft.Json.Linq.JObject
                    {
                        ["project_id"] = todo.ProjectId,
                        ["synced_project_id"] = todo.SyncedProjectId,
                        ["tags"] = new Newtonsoft.Json.Linq.JArray((todo.Tags ?? new List<FocusBear.Core.ToDo.ToDoTag>())
                            .Select(tg => new Newtonsoft.Json.Linq.JObject { ["id"] = tg.Id, ["text"] = tg.Text })),
                    };
                    task.Project = FocusBear.Core.Schedule.TodoProjects.ProjectName(fields, RemoteProjectsById) ?? (old.Value == null ? null : task.Project);
                    task.ProjectId = FocusBear.Core.Schedule.TodoProjects.ProjectId(fields);
                    rows.Add(new KeyValuePair<string, DayPlanTask>(due, task));
                }
                var stored = _byDay.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<DayPlanTask>)kv.Value);
                // A to-do created here moments ago can be missing from a catalogue downloaded (or cached) before it:
                // keep it where it is, like a local task, rather than read its absence as "deleted elsewhere".
                var incoming = new HashSet<string>(rows.Select(r => r.Value.SourceTaskId));
                var reconciled = FocusBear.Core.Schedule.DayPlanDueProjection.Reconcile(stored, rows, todayKey,
                    t => string.IsNullOrEmpty(t.SourceTaskId) || (CreatedRecently(t.SourceTaskId) && !incoming.Contains(t.SourceTaskId))
                        ? null : t.SourceTaskId,
                    t => t.Done, t => t.Id);
                if (SameLayout(stored, reconciled)) return false;
                _byDay = reconciled.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
                if (persist) Save();
                LogWriter.LogWrite("DayPlanTaskStore.MergeDueTodos: re-filed synced tasks by due date (" + rows.Count + " rows)");
                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DayPlanTaskStore.MergeDueTodos - " + ex.Message);
                return false;
            }
        }

        private static DayPlanTask CloneForMerge(DayPlanTask t)
        {
            return new DayPlanTask
            {
                Id = t.Id, Title = t.Title, DurationMinutes = t.DurationMinutes, Done = t.Done, SourceTaskId = t.SourceTaskId,
                BlockKey = t.BlockKey, Project = t.Project, ProjectId = t.ProjectId, Notes = t.Notes, Subtasks = t.Subtasks,
                Outcome = t.Outcome, EisenhowerQuadrant = t.EisenhowerQuadrant, StatusTouchedUtc = t.StatusTouchedUtc,
            };
        }

        /// <summary>Whether two layouts put the same tasks, with the same visible fields, on the same days in the same order.</summary>
        private static bool SameLayout(IReadOnlyDictionary<string, IReadOnlyList<DayPlanTask>> a, IReadOnlyDictionary<string, IReadOnlyList<DayPlanTask>> b)
        {
            var ka = a.Where(kv => kv.Value.Count > 0).Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
            var kb = b.Where(kv => kv.Value.Count > 0).Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
            if (!ka.SequenceEqual(kb)) return false;
            foreach (string k in ka)
            {
                var x = a[k]; var y = b[k];
                if (x.Count != y.Count) return false;
                for (int i = 0; i < x.Count; i++)
                {
                    if (x[i].Id != y[i].Id || x[i].Title != y[i].Title || x[i].BlockKey != y[i].BlockKey || x[i].Done != y[i].Done
                        || x[i].Project != y[i].Project || x[i].DurationMinutes != y[i].DurationMinutes) return false;
                }
            }
            return true;
        }

        /// <summary>Project names fetched from the backend (<c>/projects</c>) — set by the host on load,
        /// merged into the suggestions so a project created on web/mobile shows even with no local task.</summary>
        public List<string> RemoteProjectNames { get; set; } = new List<string>();

        /// <summary>The user's projects, id -> name (all pages of <c>/projects</c>) — resolves a to-do's
        /// <c>project_id</c> to its name. Set by the host on load (<see cref="SetRemoteProjects"/>).</summary>
        public Dictionary<string, string> RemoteProjectsById { get; private set; } = new Dictionary<string, string>();

        // The last to-do payload reconciled, so projects arriving after it can still name its tasks.
        private string _lastTodosJson;

        /// <summary>Record the fetched projects and re-name the day's tasks from the last to-do payload
        /// (the projects and the to-dos are fetched in parallel). Returns true if a task changed.</summary>
        public bool SetRemoteProjects(Dictionary<string, string> byId)
        {
            if (byId == null) return false;
            RemoteProjectsById = byId;
            _remoteProjectsLoaded = true;
            RemoteProjectNames = byId.Values.Distinct().ToList();
            return !string.IsNullOrEmpty(_lastTodosJson) && BackfillProjectsFrom(_lastTodosJson);
        }

        /// <summary>A task already on the day, re-named from its backing to-do. True if it changed.</summary>
        private bool RefreshProject(DayPlanTask task, JObject todo)
        {
            bool hasProjectFields = todo["project_id"] != null || todo["synced_project_id"] != null || todo["tags"] != null;
            if (!hasProjectFields) return false;   // a slim payload says nothing about the project
            string name = FocusBear.Core.Schedule.TodoProjects.RefreshedName(task.Project, task.ProjectId, todo, RemoteProjectsById);
            string id = FocusBear.Core.Schedule.TodoProjects.ProjectId(todo);
            bool changed = false;
            if (!string.Equals(name ?? "", task.Project ?? "", StringComparison.Ordinal)) { task.Project = name; changed = true; }
            if (!string.Equals(id ?? "", task.ProjectId ?? "", StringComparison.Ordinal)) { task.ProjectId = id; changed = true; }
            return changed;
        }

        /// <summary>
        /// Name every stored task from a to-do payload (a bare array or <c>{data/tasks/items:[…]}</c>) by its
        /// <c>SourceTaskId</c> — the fix for "most of the tasks in the day plan don't have projects": tasks
        /// imported before, or linked by <c>project_id</c> only, pick their project up. Returns true if any changed.
        /// </summary>
        public bool BackfillProjectsFrom(string todosJson)
        {
            if (string.IsNullOrWhiteSpace(todosJson)) return false;
            try
            {
                JToken root = JToken.Parse(todosJson);
                JArray arr = root as JArray;
                if (arr == null && root is JObject o)
                    arr = o["data"] as JArray ?? o["tasks"] as JArray ?? o["items"] as JArray;
                if (arr == null) return false;
                var byId = new Dictionary<string, JObject>();
                foreach (JObject to in arr.OfType<JObject>())
                {
                    string id = to.Value<string>("id");
                    if (!string.IsNullOrEmpty(id)) byId[id] = to;
                }
                bool changed = false;
                foreach (List<DayPlanTask> list in _byDay.Values)
                    foreach (DayPlanTask t in list)
                    {
                        if (string.IsNullOrEmpty(t.SourceTaskId)) continue;
                        if (!byId.TryGetValue(t.SourceTaskId, out JObject todo)) continue;
                        if (RefreshProject(t, todo)) changed = true;
                    }
                if (changed) Save();
                return changed;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DayPlanTaskStore.BackfillProjectsFrom - " + ex.Message);
                return false;
            }
        }

        /// <summary>All distinct project names — local (from tasks) + remote (from /projects).</summary>
        public List<string> AllProjectNames()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (List<DayPlanTask> list in _byDay.Values)
                foreach (DayPlanTask t in list)
                    if (!string.IsNullOrWhiteSpace(t.Project)) set.Add(t.Project.Trim());
            foreach (string p in RemoteProjectNames)
                if (!string.IsNullOrWhiteSpace(p)) set.Add(p.Trim());
            return set.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Reconcile local done-state from the backend to-do list (a bare array or <c>{data/tasks:[…]}</c>
        /// of to-dos with <c>id</c> + <c>status</c>). Matches by <c>SourceTaskId</c> and mirrors the
        /// backend value BOTH ways (done-on-web ticks here; re-opened-on-web un-ticks here) — EXCEPT a
        /// task changed locally within the last <see cref="LocalChangeGraceSeconds"/> is left alone, so a
        /// stale <c>/to-do/recent</c> (debounced ≤15s) fetched right after a local tick can't revert it
        /// before our write lands. Returns true if anything changed.
        /// </summary>
        private const int LocalChangeGraceSeconds = 20;

        public bool ReconcileStatusFrom(string todosJson)
        {
            if (string.IsNullOrWhiteSpace(todosJson)) return false;
            _lastTodosJson = todosJson;
            bool projectsChanged = BackfillProjectsFrom(todosJson);
            try
            {
                JToken root = JToken.Parse(todosJson);
                JArray arr = root as JArray;
                if (arr == null && root is JObject o)
                    arr = o["data"] as JArray ?? o["tasks"] as JArray ?? o["items"] as JArray;
                if (arr == null) return false;

                var doneById = new Dictionary<string, bool>();
                foreach (JToken t in arr)
                {
                    if (!(t is JObject to)) continue;
                    string id = to.Value<string>("id");
                    if (string.IsNullOrEmpty(id)) continue;
                    doneById[id] = string.Equals(to.Value<string>("status"), "COMPLETED", StringComparison.OrdinalIgnoreCase);
                }

                DateTime nowUtc = DateTime.UtcNow;
                bool changed = false;
                foreach (List<DayPlanTask> list in _byDay.Values)
                    foreach (DayPlanTask t in list)
                    {
                        if (string.IsNullOrEmpty(t.SourceTaskId)) continue;
                        if (!doneById.TryGetValue(t.SourceTaskId, out bool backendDone)) continue;
                        if (t.Done == backendDone) continue;
                        // Don't override a just-made local change (its backend write may still be in flight).
                        if ((nowUtc - t.StatusTouchedUtc).TotalSeconds < LocalChangeGraceSeconds) continue;
                        t.Done = backendDone;
                        changed = true;
                    }
                if (changed) Save();
                return changed || projectsChanged;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DayPlanTaskStore.ReconcileStatusFrom - " + ex.Message);
                return projectsChanged;
            }
        }
    }
}
