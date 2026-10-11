using Focus_Bear.API;
using Focus_Bear.Utils;
using FocusBear.Core.Focus;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace Focus_Bear.Focus
{
    /// <summary>
    /// The teacher's checklist for the class session the student is in, shown by the native session screen in
    /// place of its own task queue. The web To Do Player used to load this list itself; now the app holds it:
    /// seeded from the join / current payload, refetched from <c>GET /class-session/current</c> when the teacher
    /// amends it (<c>class-session-tasks-updated</c>), ticked through the class-session task endpoints.
    /// </summary>
    internal sealed class ClassSessionChecklistStore
    {
        internal static readonly ClassSessionChecklistStore Shared = new ClassSessionChecklistStore();

        private IList<FocusSessionTask> _tasks = new List<FocusSessionTask>();
        private string _sessionId = "";
        private int _version;
        private bool _busy;

        /// <summary>Raised on the UI thread after the list changes, so the session screen redraws at once.</summary>
        internal event Action Changed;

        internal IList<FocusSessionTask> Tasks => _tasks;
        internal string SessionId => _sessionId;
        internal bool IsBusy => _busy;

        /// <summary>The list from a join / current payload, replacing whatever was held.</summary>
        internal void Apply(JObject session)
        {
            string id = session?["id"]?.ToString() ?? session?["class_session_id"]?.ToString() ?? "";
            if (id.Length == 0) return;
            _sessionId = id;
            _version = ClassSessionChecklist.TasksVersion(session);
            _tasks = ClassSessionChecklist.Read(session);
            LogWriter.LogWrite("ClassSessionChecklist - " + _tasks.Count + " task(s), " + ClassSessionChecklist.CompletedCount(_tasks)
                + " done, version " + _version + " for session " + _sessionId);
            Notify();
        }

        /// <summary>The session ended or was left: nothing to show.</summary>
        internal void Clear()
        {
            if (_sessionId.Length == 0 && _tasks.Count == 0) return;
            _sessionId = "";
            _version = 0;
            _tasks = new List<FocusSessionTask>();
            Notify();
        }

        /// <summary>A <c>class-session-tasks-updated</c> event: reload when it is for this session and newer.</summary>
        internal void HandleTasksUpdated(JObject payload)
        {
            string eventSession = payload?["class_session_id"]?.ToString() ?? "";
            int eventVersion = ClassSessionChecklist.TasksVersion(payload);
            if (!ClassSessionChecklist.ShouldReload(_sessionId, _version, eventSession, eventVersion)) return;
            LogWriter.LogWrite("ClassSessionChecklist - teacher amended the list (version " + eventVersion + "), reloading");
            _ = ReloadAsync();
        }

        /// <summary>
        /// Refetches the current session for the authoritative list and the student's own ticks (the event
        /// payload carries the tasks but not who completed what).
        /// </summary>
        internal async Task ReloadAsync()
        {
            try
            {
                string expected = _sessionId;
                var session = await new FocusBearAPIClient().GetCurrentClassSession();
                string id = session?["id"]?.ToString() ?? "";
                if (session == null || id.Length == 0 || (expected.Length > 0 && id != expected)) return;
                Apply(session);
            }
            catch (Exception ex) { LogWriter.LogWrite("ClassSessionChecklist reload ex - " + ex.Message); }
        }

        /// <summary>
        /// Ticks or unticks a task, in order (see <see cref="ClassSessionChecklist"/>). The list changes at once;
        /// a refused save puts it back and returns false so the screen can say so.
        /// </summary>
        internal async Task<bool> SetCompletedAsync(string taskId, bool completed)
        {
            if (_busy || _sessionId.Length == 0) return false;
            var before = _tasks;
            var after = ClassSessionChecklist.Marking(before, taskId, completed);
            if (ReferenceEquals(after, before) || FocusSessionQueue.SameQueue(after, before)) return false;
            _busy = true;
            _tasks = after;
            Notify();
            bool ok = false;
            try { ok = await new FocusBearAPIClient().SetClassSessionTaskCompleted(_sessionId, taskId, completed); }
            catch (Exception ex) { LogWriter.LogWrite("ClassSessionChecklist tick ex - " + ex.Message); }
            _busy = false;
            if (!ok)
            {
                LogWriter.LogWrite("ClassSessionChecklist - could not " + (completed ? "tick" : "untick") + " task " + taskId);
                _tasks = before;
            }
            Notify();
            return ok;
        }

        private void Notify()
        {
            var handler = Changed;
            if (handler == null) return;
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess()) handler();
            else dispatcher.BeginInvoke(new Action(handler));
        }
    }
}
