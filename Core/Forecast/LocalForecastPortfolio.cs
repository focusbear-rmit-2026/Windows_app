using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.ToDo;
using F = FocusBear.Core.Forecast.LocalTimeForecast;

namespace FocusBear.Core.Forecast
{
    /// <summary>
    /// The "What matters" view built from the local forecast — the Mac's <c>LocalForecastPortfolio</c>. It
    /// turns the reprioritise forecast into the portfolio cards the Projects tab already draws, and files
    /// them into lanes with <see cref="PortfolioLanes"/>. These are presentation rows, never server risk
    /// estimates or proposed schedule writes: uncertain work stays visible without an unsupported "on
    /// track" / "critical" claim (<see cref="AssessedProjectIds"/>).
    /// </summary>
    public sealed class LocalForecastPortfolio
    {
        private LocalForecastPortfolio(IList<PortfolioProject> rows, PortfolioActionGroups groups,
                                       ISet<string> assessedProjectIds, bool isOvercommitted)
        {
            Rows = rows; Groups = groups; AssessedProjectIds = assessedProjectIds; IsOvercommitted = isOvercommitted;
        }

        /// <summary>One card per forecast project, in the forecast's own project order.</summary>
        public IList<PortfolioProject> Rows { get; }

        public PortfolioActionGroups Groups { get; }

        /// <summary>
        /// The projects whose bucket is a real assessment. A project that is "at risk" only because some of
        /// its work has no estimate, or none of it fits the horizon, is not — its card shows no bucket badge.
        /// </summary>
        public ISet<string> AssessedProjectIds { get; }

        /// <summary>Something must ask for an extension, or some day in the horizon is over capacity.</summary>
        public bool IsOvercommitted { get; }

        /// <param name="projects">The catalogue's projects; a forecast project not in it gets no card.</param>
        /// <param name="topScores">
        /// Each project's NEXT-task TOP score, keyed by project id — the number its card shows, so the order
        /// inside a lane can never disagree with the chip on the card.
        /// </param>
        public static LocalForecastPortfolio Make(IEnumerable<Project> projects, F.Result forecast,
                                                  IDictionary<string, double> topScores = null)
        {
            var catalogue = new Dictionary<string, Project>(StringComparer.Ordinal);
            foreach (Project project in projects ?? Enumerable.Empty<Project>())
                if (!string.IsNullOrEmpty(project.Id) && !catalogue.ContainsKey(project.Id)) catalogue[project.Id] = project;
            var scores = topScores ?? new Dictionary<string, double>();
            DateTime? today = forecast.Days.Count > 0 ? forecast.Days[0].Date.Date : (DateTime?)null;
            DateTime? horizon = forecast.Days.Count > 0 ? forecast.Days[forecast.Days.Count - 1].Date.Date : (DateTime?)null;
            var unfinishedIds = ProjectIds(forecast.Tasks.Where(t => !t.Task.Completed));
            var completedIds = ProjectIds(forecast.Tasks.Where(t => t.Task.Completed));
            var startsTodayIds = new HashSet<string>(forecast.Tasks.SelectMany(t => t.Allocations)
                .Where(a => today.HasValue && a.Day.Date == today.Value && a.TotalMinutes > F.CapacityEpsilon && a.ProjectId != null)
                .Select(a => a.ProjectId));

            var rows = new List<PortfolioProject>();
            var entries = new List<PortfolioLanes.Entry>();
            var assessed = new HashSet<string>(StringComparer.Ordinal);
            foreach (F.ProjectResult estimate in forecast.Projects)
            {
                Project project;
                if (!catalogue.TryGetValue(estimate.Id, out project)) continue;
                DateTime? due = estimate.Project.DueDate.HasValue ? estimate.Project.DueDate.Value.Date : (DateTime?)null;
                bool unfinished = unfinishedIds.Contains(estimate.Id);
                bool isAssessed;
                PortfolioBucket bucket = BucketFor(estimate, due, horizon, unfinished, out isAssessed);
                if (isAssessed) assessed.Add(project.Id);

                double remaining = Math.Max(0, estimate.EstimatedMinutes - estimate.CompletedMinutes);
                double deficit = Math.Max(0, remaining - estimate.AllocatedByDeadlineMinutes);
                int? days = today.HasValue && due.HasValue ? (int)Math.Round((due.Value - today.Value).TotalDays) : (int?)null;
                var badges = new List<string>();
                if (startsTodayIds.Contains(estimate.Id) && unfinished) badges.Add(PortfolioBadgeWire.StartNow);
                if (bucket == PortfolioBucket.Critical) badges.Add(PortfolioBadgeWire.Overcommitted);
                var row = new PortfolioProject
                {
                    Id = project.Id,
                    Name = project.Name,
                    DueDate = project.DueDate,
                    // Known effort is only a lower bound while some tasks have no estimate, so no total is claimed.
                    RequiredHours = estimate.UnknownTaskCount > 0 ? (double?)null : remaining / MinutesPerHour,
                    RiskScore = remaining > F.CapacityEpsilon ? deficit / remaining : (double?)null,
                    BucketRaw = PortfolioBucketWire.Of(bucket),
                    DaysUntilDue = days,
                    Badges = badges,
                };
                rows.Add(row);
                double score;
                bool isBeyondHorizon = due.HasValue && horizon.HasValue && due.Value > horizon.Value;
                entries.Add(new PortfolioLanes.Entry(row, new PortfolioLanes.Facts(
                    bucket, days, isBeyondHorizon, scores.TryGetValue(project.Id, out score) ? score : (double?)null,
                    unfinished, !unfinished && (estimate.IsFinished || completedIds.Contains(project.Id)))));
            }

            PortfolioActionGroups groups = PortfolioLanes.Group(entries);
            bool overcommitted = groups.AskForExtension.Count > 0
                || forecast.Days.Any(d => d.Capacity.OvercommittedMinutes > F.CapacityEpsilon);
            return new LocalForecastPortfolio(rows, groups, assessed, overcommitted);
        }

        /// <summary>
        /// The NEXT task each project's card offers, picked from the forecast's own open tasks — the same
        /// tasks it plans — so the card, the lane order and the plan all describe one set of work.
        /// </summary>
        public static IDictionary<string, ProjectTopPriorityTask> NextTasks(IEnumerable<F.TaskInput> plannedTasks,
                                                                             IEnumerable<ToDoTask> tasks, DateTime now)
        {
            var projectByTaskId = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (F.TaskInput input in plannedTasks ?? Enumerable.Empty<F.TaskInput>())
                if (!input.Completed && input.ProjectId != null && !projectByTaskId.ContainsKey(input.Id)) projectByTaskId[input.Id] = input.ProjectId;
            var result = new Dictionary<string, ProjectTopPriorityTask>(StringComparer.Ordinal);
            var grouped = (tasks ?? Enumerable.Empty<ToDoTask>())
                .Where(task => task.Id != null && projectByTaskId.ContainsKey(task.Id))
                .GroupBy(task => projectByTaskId[task.Id]);
            foreach (var group in grouped)
            {
                ProjectTopPriorityTask next = PortfolioNextTask.Pick(group, now);
                if (next != null) result[group.Key] = next;
            }
            return result;
        }

        private const double MinutesPerHour = 60;

        /// <summary>
        /// The card's bucket, and whether it is an assessment (<paramref name="isAssessed"/>) or only the
        /// honest "we can't tell yet" of work with no estimate or no room inside the horizon.
        /// </summary>
        private static PortfolioBucket BucketFor(F.ProjectResult estimate, DateTime? due, DateTime? horizon, bool unfinished,
                                                 out bool isAssessed)
        {
            double remaining = Math.Max(0, estimate.EstimatedMinutes - estimate.CompletedMinutes);
            double deficit = Math.Max(0, remaining - estimate.AllocatedByDeadlineMinutes);
            DateTime? completion = estimate.CompletionDate.HasValue ? estimate.CompletionDate.Value.Date : (DateTime?)null;
            isAssessed = true;
            // A missing deadline is a fact, not a feasibility claim.
            if (!due.HasValue) return PortfolioBucket.NoDeadline;
            // Even the known work cannot fit before this deadline.
            if (horizon.HasValue && due.Value <= horizon.Value && deficit > F.CapacityEpsilon) return PortfolioBucket.Critical;
            // Nothing outstanding is left to place, so the deadline cannot be missed.
            if (estimate.IsFinished) return PortfolioBucket.OnTrack;
            if (estimate.UnknownTaskCount > 0 || (unfinished && !completion.HasValue) || estimate.EstimatedMinutes <= F.CapacityEpsilon)
            {
                isAssessed = false;
                return PortfolioBucket.AtRisk;
            }
            if (completion.HasValue && completion.Value >= due.Value)
                return completion.Value > due.Value ? PortfolioBucket.Critical : PortfolioBucket.AtRisk;
            return PortfolioBucket.OnTrack;
        }

        private static HashSet<string> ProjectIds(IEnumerable<F.TaskResult> tasks)
        {
            return new HashSet<string>(tasks.Where(t => t.Task.ProjectId != null).Select(t => t.Task.ProjectId), StringComparer.Ordinal);
        }
    }
}
