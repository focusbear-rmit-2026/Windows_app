using Focus_Bear.API;
using Focus_Bear.Utils;
using System;
using System.Threading.Tasks;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// The Day Plan write-sync facade — the Windows port of macOS <c>DayPlanSync.shared</c>. The task
    /// store calls these when a to-do-backed task is planned/unplanned/completed; they fire the matching
    /// <c>PUT /to-do</c> upsert in the background (best-effort, like the macOS <c>send</c>), so the store
    /// stays synchronous and the UI never blocks on the network. From-scratch tasks (no
    /// <c>SourceTaskId</c>) and tasks without a captured Eisenhower quadrant are never synced — a status
    /// or plan write without the real quadrant would clobber it on the create-DTO upsert.
    /// </summary>
    public static class DayPlanSync
    {
        /// <summary>True when this task is backed by a real to-do we can safely upsert.</summary>
        public static bool CanSync(DayPlanTask t) =>
            t != null && !string.IsNullOrEmpty(t.SourceTaskId) && t.EisenhowerQuadrant.HasValue;

        /// <summary>Reflect a completion flip onto the backend to-do.</summary>
        public static void SetTodoStatus(DayPlanTask t, bool completed)
        {
            if (!CanSync(t)) return;
            FireAndForget(() => new FocusBearAPIClient().SetTodoStatus(
                t.SourceTaskId, t.Title, t.EisenhowerQuadrant.Value, completed), "setTodoStatus");
        }

        /// <summary>Plan (or, with <paramref name="plannedDate"/> null, unplan) a to-do onto a day/block.</summary>
        public static void SetTodoPlanned(DayPlanTask t, DateTime? plannedDate, string blockKey, int? order = null)
        {
            if (!CanSync(t)) return;
            FireAndForget(() => new FocusBearAPIClient().SetTodoPlanned(
                t.SourceTaskId, t.Title, t.EisenhowerQuadrant.Value, plannedDate, blockKey, order), "setTodoPlanned");
        }

        private static async void FireAndForget(Func<Task<bool>> call, string label)
        {
            try { await call(); }
            catch (Exception ex) { LogWriter.LogWrite("DayPlanSync " + label + " - " + ex.Message); }
        }
    }
}
