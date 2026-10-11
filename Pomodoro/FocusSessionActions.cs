using Focus_Bear.API;
using Focus_Bear.Habits.DayPlan;
using Focus_Bear.Utils;
using FocusBear.Core.Focus;
using FocusBear.Core.Notes;
using FocusBear.Core.ToDo;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Focus_Bear.Pomodoro
{
    /// <summary>
    /// The one write path for the running session's task queue (the Mac's <c>FocusSessionQueueWriter</c>).
    ///
    /// The queue lives where it always has: <c>sRawTasks</c> (what the AI checks, the block popup and the
    /// focus-end screen read), the running focus mode's <c>lastTaskUsed</c>, and
    /// <c>sCurrentTaskPlayingInToDoPlayer</c> for the active task's title. The web To Do Player used to own
    /// these; the native session screen now does, except in a class session with a teacher's checklist
    /// (<see cref="NativeOwnsQueue"/> false), where the web player is still the only thing that shows it.
    /// </summary>
    internal static class FocusSessionQueueWriter
    {
        private static AppSettings Settings => AppSettings.GetInstance;

        /// <summary>Raised after every write, so an open session screen redraws at once rather than on its next tick.</summary>
        internal static event Action Changed;

        /// <summary>
        /// False only in class-session To Do Player mode: the teacher's checklist is loaded by the web player from
        /// the class session id and never passes through <c>sRawTasks</c>.
        /// </summary>
        internal static bool NativeOwnsQueue => string.IsNullOrEmpty(Settings.sClassSessionTodoSessionId);

        internal static IList<FocusSessionTask> Tasks => FocusSessionQueue.Decode(Settings.sRawTasks);

        internal static void Persist(IList<FocusSessionTask> tasks)
        {
            if (!NativeOwnsQueue) return;
            // The player wrote "" for an empty list, and every reader treats "" and "[]" alike.
            Settings.sRawTasks = tasks.Count == 0 ? "" : FocusSessionQueue.Encode(tasks);
            var running = Settings.arrFocusModes.FirstOrDefault(m => m.isOn);
            if (running != null) running.lastTaskUsed = Settings.sRawTasks;
            Settings.sCurrentTaskPlayingInToDoPlayer = FocusSessionQueue.Active(tasks)?.Title ?? "";
            Changed?.Invoke();
        }

        /// <summary>
        /// Completes a queue entry and ticks the to-do everywhere else: through the Day Plan when it is planned
        /// today (which pushes the status to the backing to-do), otherwise straight on the to-do.
        /// </summary>
        internal static IList<FocusSessionTask> Complete(IList<FocusSessionTask> tasks, string id)
        {
            var task = tasks.FirstOrDefault(t => t.Id == id);
            var updated = FocusSessionQueue.Completing(tasks, id);
            Persist(updated);
            if (task != null && !task.IsCompleted) _ = CompleteOnServerAsync(task);
            return updated;
        }

        private static async Task CompleteOnServerAsync(FocusSessionTask task)
        {
            try
            {
                if (string.IsNullOrEmpty(task.Id)) return;
                DateTime today = DateTime.Today;
                var planned = DayPlanTaskStore.Shared.Tasks(today).FirstOrDefault(t => !t.Done && (t.SourceTaskId == task.Id || t.Id == task.Id));
                if (planned != null)
                {
                    DayPlanTaskStore.Shared.SetDone(planned.Id, true, today);
                    if (DayPlanSync.CanSync(planned)) return;
                }
                var api = new FocusBearAPIClient();
                string json = await api.GetToDosByIds(new[] { task.Id });
                var latest = json == null ? null : ToDoJson.ReadTasks(json)?.FirstOrDefault(t => t.Id == task.Id);
                if (latest == null || latest.IsCompleted) return;
                bool saved = await api.SetTodoStatus(latest.Id, latest.Title, latest.EisenhowerQuadrant ?? (int)EisenhowerQuadrant.DoIt, true);
                LogWriter.LogWrite("FocusSession - completed a queued task on the server: " + saved);
            }
            catch (Exception ex) { LogWriter.LogWrite("FocusSession CompleteOnServerAsync ex - " + ex.Message); }
        }

        /// <summary>
        /// The block popup's "add a task and play it" (<c>sCurrentTaskCreated</c>, a saved to-do row), which used
        /// to reach only the web player.
        /// </summary>
        internal static void AddAndPlay(string createdTaskJson, bool completeCurrent)
        {
            if (!NativeOwnsQueue || string.IsNullOrWhiteSpace(createdTaskJson)) return;
            try
            {
                var row = JObject.Parse(createdTaskJson);
                string id = row["id"]?.ToString() ?? "";
                string title = row["title"]?.ToString() ?? "";
                if (id.Length == 0 && title.Length == 0) return;
                var task = new FocusSessionTask(id, title, durationSeconds: FocusSessionQueue.DurationSeconds(row["duration"]));
                var tasks = Tasks;
                var current = FocusSessionQueue.Active(tasks);
                var updated = FocusSessionQueue.Playing(tasks, task, completeCurrent);
                Persist(updated);
                if (completeCurrent && current != null && current.Id != id) _ = CompleteOnServerAsync(current);
            }
            catch (Exception ex) { LogWriter.LogWrite("FocusSession AddAndPlay ex - " + ex.Message); }
        }

        /// <summary>A to-do row as a queue entry, with the start screen's defaults.</summary>
        internal static FocusSessionTask FromToDo(ToDoTask task, string projectName)
        {
            var queue = FocusSessionQueue.Decode(FocusStartWorkSelection.SessionQueue(new[] { task }, null));
            var entry = queue.FirstOrDefault() ?? new FocusSessionTask(task.Id, task.Title);
            return entry.With(isActive: false, projectName: projectName);
        }
    }

    /// <summary>
    /// The rail's quick brain dump: the same note the Notes screen edits (<c>is_brain_dump</c>), saved through the
    /// same <c>PUT /note</c> upsert. The rail edits plain text, so a save writes it back as paragraphs.
    /// </summary>
    internal sealed class FocusSessionBrainDump
    {
        private readonly BrainDumpTracker _tracker = new BrainDumpTracker();
        private string _lastSavedBody;

        internal async Task<string> LoadAsync()
        {
            NotesPage page = NotesWire.ReadPage(await new FocusBearAPIClient().GetNotesPage(NotesPaging.FirstPage, NotesPaging.BrainDumpScanTake));
            Note latest = page == null ? null : _tracker.Observe(page.Data);
            if (latest == null) return null;
            _lastSavedBody = latest.Body ?? "";
            return NoteHtml.PlainText(latest.Body ?? "");
        }

        internal async Task<bool> SaveAsync(string plainText, string defaultTitle)
        {
            string html = FocusSessionBrainDumpText.ToHtml(plainText);
            Note existing = _tracker.Adopted;
            if (!BrainDumpSaveRules.ShouldCommit(html, _lastSavedBody, existing != null)) return true;
            string title = existing != null && !string.IsNullOrEmpty(existing.Title) ? existing.Title : defaultTitle;
            Note saved = NotesWire.ReadNote(await new FocusBearAPIClient().UpsertNote(NoteUpsertBody.ForBrainDump(existing?.Id, title, html)));
            if (saved == null) return false;
            _tracker.Saved(saved);
            _lastSavedBody = html;
            return true;
        }
    }
}
