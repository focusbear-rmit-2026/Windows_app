using System;
using System.Collections.Generic;
using System.Linq;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// Natural-language "move task to a day" parser — the Windows port of the macOS
    /// <c>parseSnoozeDate</c>. Accepts <c>today/tod</c>, <c>tom/tmr/tomorrow</c>, a weekday prefix
    /// (<c>mon</c>…<c>sun</c> → next occurrence), <c>&lt;n&gt;&lt;unit&gt;</c> (<c>d/w/mo/y</c>), or an
    /// absolute date (<c>1 Jul</c>, <c>Jul 1</c>, <c>1/7</c>). Returns the target day (date-only) or null.
    /// </summary>
    public static class DayPlanSnooze
    {
        public static DateTime? Parse(string input, DateTime baseDate)
        {
            string s = (input ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(s)) return null;
            DateTime today = baseDate.Date;

            // Allow a leading "next " / "this " so "next tue", "next week", "this fri" all work (the
            // help text advertises "next tue", so it must be accepted). "next <unit>" = one unit ahead.
            if (s.StartsWith("next ")) s = s.Substring(5).Trim();
            else if (s.StartsWith("this ")) s = s.Substring(5).Trim();

            if (s == "today" || s == "tod") return today;
            if (s == "tom" || s == "tmr" || s == "tomorrow") return today.AddDays(1);

            // Bare unit words (e.g. from "next week"): treat as one unit ahead.
            switch (s)
            {
                case "day": case "days": return today.AddDays(1);
                case "week": case "wk": case "wks": case "weeks": return today.AddDays(7);
                case "month": case "mo": case "months": return today.AddMonths(1);
                case "year": case "yr": case "yrs": case "years": return today.AddYears(1);
            }

            var weekdays = new (string name, DayOfWeek wd)[]
            {
                ("sun", DayOfWeek.Sunday), ("mon", DayOfWeek.Monday), ("tue", DayOfWeek.Tuesday),
                ("wed", DayOfWeek.Wednesday), ("thu", DayOfWeek.Thursday), ("fri", DayOfWeek.Friday),
                ("sat", DayOfWeek.Saturday)
            };
            foreach (var (name, wd) in weekdays)
            {
                if (!s.StartsWith(name)) continue;
                DateTime d = today.AddDays(1);
                while (d.DayOfWeek != wd) d = d.AddDays(1);
                return d;
            }

            // <number><unit>: d/w/mo/y
            int digits = 0;
            while (digits < s.Length && char.IsDigit(s[digits])) digits++;
            if (digits > 0 && int.TryParse(s.Substring(0, digits), out int n) && n > 0)
            {
                string unit = s.Substring(digits);
                switch (unit)
                {
                    case "d": case "day": case "days": return today.AddDays(n);
                    case "w": case "wk": case "wks": case "week": case "weeks": return today.AddDays(n * 7);
                    case "mo": case "month": case "months": return today.AddMonths(n);
                    case "y": case "yr": case "yrs": case "year": case "years": return today.AddYears(n);
                }
            }

            // Absolute calendar date; if it already passed this year (no year given), roll forward a year.
            if (DateTime.TryParse(input, System.Globalization.CultureInfo.CurrentCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime abs))
            {
                DateTime day = abs.Date;
                if (day < today) day = day.AddYears(1);
                return day >= today ? day : (DateTime?)null;
            }
            return null;
        }
    }

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
        /// <summary>Project/tag name (from the to-do's first tag), shown as a grouping header.</summary>
        public string Project { get; set; }
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
    /// associations survive a restart. Backend cross-device sync is a later phase; for now this is the
    /// single source of truth for what's in the panel and what's nested under each block.
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

        private DayPlanTaskStore() { Load(); }

        private static string DayKey(DateTime date) => date.ToString("yyyy-MM-dd");

        // ---- persistence -------------------------------------------------------------------------

        private void Load()
        {
            try
            {
                string json = RegistryManager.LoadDayPlanTasks();
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
        }

        private void Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_byDay);
                RegistryManager.SaveDayPlanTasks(json);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DayPlanTaskStore.Save - " + ex.Message);
            }
        }

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
            if (RegistryManager.LoadDayPlanLastRolloverDay() == todayKey) { _rolledOverDay = todayKey; return; }
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
                RegistryManager.SaveDayPlanLastRolloverDay(todayKey);
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
        }

        public void Update(DayPlanTask task, DateTime date)
        {
            string k = DayKey(date);
            if (!_byDay.TryGetValue(k, out List<DayPlanTask> list)) return;
            int i = list.FindIndex(t => t.Id == task.Id);
            if (i < 0) return;
            bool wasDone = list[i].Done;
            list[i] = task;
            Save();
            // Reflect a completion flip onto the backend to-do (only on an actual change).
            if (task.Done != wasDone) { task.StatusTouchedUtc = DateTime.UtcNow; DayPlanSync.SetTodoStatus(task, task.Done); }
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
            // Dropping across groups moves the task into the target's project.
            bool projectChanged = !string.Equals(dragged.Project ?? "", target.Project ?? "", StringComparison.OrdinalIgnoreCase);
            dragged.Project = target.Project;

            list.RemoveAt(di);
            ti = list.FindIndex(t => t.Id == targetId);   // recompute after removal
            list.Insert(ti, dragged);
            Save();

            // Best-effort backend reflect: keep it planned on the day (and, if it changed group, the new project).
            if (projectChanged) DayPlanSync.SetTodoPlanned(dragged, date, dragged.BlockKey);
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
                    }
                    else
                    {
                        int secs = 0;
                        JToken durTok = o["duration"];
                        if (durTok != null && durTok.Type != JTokenType.Null) int.TryParse(durTok.ToString(), out secs);
                        int minutes = secs > 0 ? Math.Max(1, secs / 60) : 15;
                        string blockKey = o.Value<string>("planned_block_key");
                        string project = null;
                        if (o["tags"] is JArray tags && tags.Count > 0 && tags[0] is JObject tag0)
                            project = tag0.Value<string>("text") ?? tag0.Value<string>("name");
                        int? quadrant = null;
                        JToken q = o["eisenhower_quadrant"];
                        if (q != null && q.Type != JTokenType.Null && int.TryParse(q.ToString(), out int qv)) quadrant = qv;

                        list.Add(new DayPlanTask(o.Value<string>("title") ?? "Task", minutes, done,
                            sourceTaskId: id, blockKey: blockKey, project: project, eisenhowerQuadrant: quadrant));
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

        /// <summary>Project names fetched from the backend (<c>/projects</c>) — set by the host on load,
        /// merged into the suggestions so a project created on web/mobile shows even with no local task.</summary>
        public List<string> RemoteProjectNames { get; set; } = new List<string>();

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
                return changed;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DayPlanTaskStore.ReconcileStatusFrom - " + ex.Message);
                return false;
            }
        }
    }
}
