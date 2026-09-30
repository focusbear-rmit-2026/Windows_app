using Focus_Bear.API;
using Focus_Bear.Utils;
using FocusBear.Core.ToDo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// The complete unfinished to-do catalogue plus the user's active projects, read the way the Mac's
    /// forecast reads them (<c>GET /to-do?sort_mode=DUE_DATE&amp;exclude_completed=true</c>, every page, and
    /// every page of <c>/projects</c>). Shared by the Day Plan's due-date projection and the reprioritise
    /// forecast, and reused for a minute so opening one right after the other doesn't download it twice.
    /// Read-only: nothing here writes.
    /// </summary>
    public static class ToDoCatalogue
    {
        public sealed class Snapshot
        {
            public Snapshot(IList<ToDoTask> tasks, IList<Project> projects, string account, DateTime fetchedUtc)
            {
                Tasks = tasks; Projects = projects; Account = account; FetchedUtc = fetchedUtc;
                CompletingStatusIds = ToDoCompletionState.CompletingStatusIds(projects);
            }

            /// <summary>Every unfinished to-do (finished rows filtered out here too, by project columns).</summary>
            public IList<ToDoTask> Tasks { get; }
            public IList<Project> Projects { get; }
            public ISet<string> CompletingStatusIds { get; }
            public string Account { get; }
            public DateTime FetchedUtc { get; }
        }

        private const int ReuseSeconds = 60;
        private static Snapshot _last;
        private static Task<Snapshot> _inFlight;

        private static string Account => AppSettings.GetInstance.userAccount?.userID ?? "";

        /// <summary>The catalogue, or null when any page failed (a partial catalogue would state absences as fact).</summary>
        public static Task<Snapshot> GetAsync(bool force = false)
        {
            Snapshot last = _last;
            if (!force && last != null && last.Account == Account && (DateTime.UtcNow - last.FetchedUtc).TotalSeconds < ReuseSeconds)
                return Task.FromResult(last);
            if (_inFlight != null && !_inFlight.IsCompleted) return _inFlight;
            _inFlight = LoadAsync();
            return _inFlight;
        }

        /// <summary>Drop the cached copy (after a write that changes due dates).</summary>
        public static void Invalidate() { _last = null; }

        private static async Task<Snapshot> LoadAsync()
        {
            string account = Account;
            try
            {
                var client = new FocusBearAPIClient();
                IList<Project> projects = await client.GetAllActiveToDoProjects();
                if (projects == null) return null;
                IList<ToDoTask> tasks = await client.GetUnfinishedToDoCatalogue();
                if (tasks == null) return null;
                if (Account != account) return null;   // the account changed while we were reading
                var snapshot = new Snapshot(tasks, projects, account, DateTime.UtcNow);
                var completing = snapshot.CompletingStatusIds;
                var unfinished = tasks.Where(t => !ToDoCompletionState.IsFinished(t, completing)).ToList();
                snapshot = new Snapshot(unfinished, projects, account, DateTime.UtcNow);
                _last = snapshot;
                LogWriter.LogWrite("ToDoCatalogue: " + unfinished.Count + " unfinished to-dos, " + projects.Count + " projects");
                return snapshot;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ToDoCatalogue.LoadAsync - " + ex.Message);
                return null;
            }
        }
    }
}
