using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>One project whose next task still has to be fetched.</summary>
    public sealed class PortfolioFetchTarget : IEquatable<PortfolioFetchTarget>
    {
        public PortfolioFetchTarget(string projectId, string syncedProjectId = null)
        {
            ProjectId = projectId;
            SyncedProjectId = syncedProjectId;
        }

        public string ProjectId { get; }

        /// <summary>Set when the project mirrors an external provider's, which is what it is queried by.</summary>
        public string SyncedProjectId { get; }

        public bool Equals(PortfolioFetchTarget other)
        {
            return other != null && ProjectId == other.ProjectId && SyncedProjectId == other.SyncedProjectId;
        }

        public override bool Equals(object obj) => Equals(obj as PortfolioFetchTarget);

        public override int GetHashCode()
        {
            return (ProjectId?.GetHashCode() ?? 0) * 397 ^ (SyncedProjectId?.GetHashCode() ?? 0);
        }

        public override string ToString() => ProjectId;
    }

    /// <summary>
    /// Web `utils/portfolio-top-priority-task/`: which task a project card offers as the next one to do.
    ///
    /// The wire payload's `top_priority_task` (on both `GET /projects` and `GET /portfolio`) is NOT
    /// trusted. The server picks the highest stored `top_score` across all of a project's to-dos,
    /// completed ones included, and a completed task keeps the score it had when it was finished — on
    /// a real account that made 14 of 30 cards advertise a task the user had already ticked off. The
    /// web works around the same backend behaviour by recomputing client-side, and so does this: drop
    /// the completed tasks and take the highest LOCAL TOP score.
    /// </summary>
    public static class PortfolioNextTask
    {
        /// <summary>
        /// `GET /to-do` page size per project. The server's SMART order is itself polluted by completed
        /// tasks, so a whole page is fetched and re-ranked locally rather than the web's take of three.
        /// </summary>
        public const int FetchTake = 50;

        /// <summary>How many project fetches run at once (web `PORTFOLIO_TOP_PRIORITY_TASK.MAX_CONCURRENCY`).</summary>
        public const int MaxConcurrency = 3;

        /// <summary>
        /// `pickTopPriorityTask`: open tasks only, highest TOP score wins. Null for a project whose
        /// tasks are all finished — a finished project offers no next task rather than a stale one.
        /// </summary>
        public static ProjectTopPriorityTask Pick(IEnumerable<ToDoTask> tasks, DateTime now)
        {
            IEnumerable<ToDoTask> open = (tasks ?? Enumerable.Empty<ToDoTask>()).Where(task => !task.IsCompleted);
            ToDoTask top = ToDoSorting.Sort(open, ToDoSortMode.Smart, now).FirstOrDefault();
            if (top == null) return null;

            return new ProjectTopPriorityTask(top.Id, top.Title, ToDoScoring.TopScoreOf(top, now));
        }

        /// <summary>
        /// `collectTopPriorityTaskFetchTargets`: the listed projects that have tasks, plus any project
        /// the portfolio named that the project list does not carry, each fetched once.
        /// </summary>
        public static IList<PortfolioFetchTarget> FetchTargets(IEnumerable<Project> projects,
                                                               IEnumerable<string> additionalProjectIds = null)
        {
            IList<Project> listed = (projects ?? Enumerable.Empty<Project>()).ToList();
            var byId = new Dictionary<string, Project>(StringComparer.Ordinal);
            foreach (Project project in listed)
            {
                if (!byId.ContainsKey(project.Id)) byId[project.Id] = project;
            }

            IEnumerable<string> withTasks = listed
                .Where(project => (project.TaskCount ?? 0) > 0)
                .Select(project => project.Id);
            IEnumerable<string> portfolioOnly = (additionalProjectIds ?? Enumerable.Empty<string>())
                .Where(id => !byId.ContainsKey(id));

            var seen = new HashSet<string>(StringComparer.Ordinal);
            return withTasks.Concat(portfolioOnly)
                .Where(id => seen.Add(id))
                .Select(id => new PortfolioFetchTarget(id, SyncedIdOf(byId, id)))
                .ToList();
        }

        /// <summary>`filterUncachedTopPriorityTargets`.</summary>
        public static IList<PortfolioFetchTarget> Uncached(IEnumerable<PortfolioFetchTarget> targets,
                                                           ISet<string> resolved)
        {
            return (targets ?? Enumerable.Empty<PortfolioFetchTarget>())
                .Where(target => resolved == null || !resolved.Contains(target.ProjectId))
                .ToList();
        }

        private static string SyncedIdOf(IDictionary<string, Project> byId, string projectId)
        {
            Project project;
            return byId.TryGetValue(projectId, out project) ? project.SyncedProjectId : null;
        }
    }
}
