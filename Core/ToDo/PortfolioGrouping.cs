using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FocusBear.Core.Stats;

namespace FocusBear.Core.ToDo
{
    /// <summary>The "This week" view's groups.</summary>
    public sealed class PortfolioActionGroups
    {
        public IList<PortfolioProject> AskForExtension { get; set; } = new List<PortfolioProject>();
        public IList<PortfolioProject> StartNow { get; set; } = new List<PortfolioProject>();

        /// <summary>Only the local forecast fills this (<see cref="PortfolioLanes"/>); the server rows have no horizon to judge it by.</summary>
        public IList<PortfolioProject> SteadyProgress { get; set; } = new List<PortfolioProject>();

        public IList<PortfolioProject> BackBurner { get; set; } = new List<PortfolioProject>();

        public bool IsEmpty => AskForExtension.Count == 0 && StartNow.Count == 0 && SteadyProgress.Count == 0 && BackBurner.Count == 0;
    }

    /// <summary>A recommended daily pace, split into whole hours and minutes.</summary>
    public struct PortfolioDurationParts : IEquatable<PortfolioDurationParts>
    {
        public PortfolioDurationParts(int hours, int minutes)
        {
            Hours = hours;
            Minutes = minutes;
        }

        public int Hours { get; }
        public int Minutes { get; }

        public bool Equals(PortfolioDurationParts other) => Hours == other.Hours && Minutes == other.Minutes;
        public override bool Equals(object obj) => obj is PortfolioDurationParts other && Equals(other);
        public override int GetHashCode() => Hours * 397 ^ Minutes;

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}h {1}m", Hours, Minutes);
        }
    }

    /// <summary>
    /// Web `utils/portfolio.util.ts`. Buckets and risk scores come from the server; this only regroups
    /// and orders what it sent.
    /// </summary>
    public static class PortfolioGrouping
    {
        /// <summary>How many back-burner projects are promoted when there is nothing else to start.</summary>
        public const int EncouragedStartNowLimit = 3;

        /// <summary>A recommended pace is rounded to this, and never falls below it.</summary>
        private const int PaceRoundMinutes = 5;

        private const int MinutesPerHour = 60;

        /// <summary>
        /// Badges worth showing on a card that already sits under a section heading.
        ///
        /// A card in "Ask for an extension" carried an "Overloaded" badge and a "Start now" badge at
        /// once — the first repeats the heading above it, the second contradicts it (Mac report
        /// 2026-09-11_20-39-23). "Start now" is a section of its own, so it never earns a badge; a card
        /// outside the grouped view keeps everything, having no heading to lean on.
        /// </summary>
        public static IList<PortfolioBadge> Badges(IEnumerable<PortfolioBadge> badges, PortfolioActionSection? section)
        {
            IList<PortfolioBadge> all = (badges ?? Enumerable.Empty<PortfolioBadge>()).ToList();
            if (!section.HasValue) return all;

            return all
                .Where(badge => badge != PortfolioBadge.StartNow)
                .Where(badge => badge != PortfolioBadge.Overcommitted
                             || section.Value != PortfolioActionSection.AskForExtension)
                .ToList();
        }

        /// <summary>
        /// Filters portfolio rows by name for the "What matters" search box. A blank query is
        /// everything, so the sections behave exactly as before until the user types (Mac report
        /// 2026-09-11_21-39-35).
        /// </summary>
        public static IList<PortfolioProject> Matching(IEnumerable<PortfolioProject> projects, string query)
        {
            string trimmed = (query ?? string.Empty).Trim();
            return (projects ?? Enumerable.Empty<PortfolioProject>())
                .Where(project => ProjectNames.Matches(project.Name, trimmed))
                .ToList();
        }

        public static PortfolioActionGroups Group(IEnumerable<PortfolioProject> projects)
        {
            var groups = new PortfolioActionGroups();
            foreach (PortfolioProject project in projects ?? Enumerable.Empty<PortfolioProject>())
            {
                switch (project.Bucket)
                {
                    case PortfolioBucket.Critical:
                        groups.AskForExtension.Add(project);
                        break;
                    case PortfolioBucket.AtRisk:
                        groups.StartNow.Add(project);
                        break;
                    default:
                        (NeedsStartNow(project) ? groups.StartNow : groups.BackBurner).Add(project);
                        break;
                }
            }

            return groups.StartNow.Count > 0 ? Sorted(groups) : PromoteFromBackBurner(groups);
        }

        /// <summary>
        /// An on-track project still needs starting when the server says so, or when it has a deadline
        /// but nothing of it is in the Day Plan yet (#2418) — "on track" then only means "there is free
        /// space", not "it is planned".
        /// </summary>
        private static bool NeedsStartNow(PortfolioProject project)
        {
            return project.BadgeList.Contains(PortfolioBadge.StartNow)
                || (project.IsUnscheduled && project.HasDueDate);
        }

        /// <summary>
        /// With nothing to start now, encourage up to three due-dated on-track projects (soonest
        /// first), else up to three of high or critical importance.
        /// </summary>
        private static PortfolioActionGroups PromoteFromBackBurner(PortfolioActionGroups groups)
        {
            IList<PortfolioProject> encouraged = groups.BackBurner
                .Where(project => project.Bucket == PortfolioBucket.OnTrack && project.HasDueDate)
                .OrderBy(project => project.DueDate, StringComparer.Ordinal)
                .ThenByDescending(project => project.ImportanceLevel)
                .Take(EncouragedStartNowLimit)
                .ToList();

            if (encouraged.Count == 0)
            {
                encouraged = groups.BackBurner
                    .Where(project => project.ImportanceLevel == PortfolioImportance.High
                                   || project.ImportanceLevel == PortfolioImportance.Critical)
                    .OrderByDescending(project => project.ImportanceLevel)
                    .Take(EncouragedStartNowLimit)
                    .ToList();
            }

            var encouragedIds = new HashSet<string>(encouraged.Select(project => project.Id), StringComparer.Ordinal);
            var result = new PortfolioActionGroups
            {
                AskForExtension = groups.AskForExtension,
                StartNow = encouraged,
                BackBurner = groups.BackBurner.Where(project => !encouragedIds.Contains(project.Id)).ToList(),
            };
            return Sorted(result);
        }

        private static PortfolioActionGroups Sorted(PortfolioActionGroups groups)
        {
            return new PortfolioActionGroups
            {
                AskForExtension = groups.AskForExtension
                    .OrderByDescending(project => project.Risk)
                    .ToList(),

                // Overdue first, then higher risk.
                StartNow = groups.StartNow
                    .OrderByDescending(project => project.IsOverdue)
                    .ThenByDescending(project => project.Risk)
                    .ToList(),

                // Due-dated first, then higher risk, then higher importance.
                BackBurner = groups.BackBurner
                    .OrderByDescending(project => project.HasDueDate)
                    .ThenByDescending(project => project.Risk)
                    .ThenByDescending(project => project.ImportanceLevel)
                    .ToList(),
            };
        }

        /// <summary>
        /// Web `getRecommendedDailyHours`: null without effort or a due date; the whole remaining
        /// effort when the project is due today or overdue.
        /// </summary>
        public static double? RecommendedDailyHours(PortfolioProject project)
        {
            if (project.Hours <= 0 || !project.DaysUntilDue.HasValue) return null;

            int days = project.DaysUntilDue.Value;
            if (days <= 0) return project.Hours;
            return project.Hours / days;
        }

        /// <summary>
        /// Web `roundHoursToDurationParts`: to the nearest five minutes, never below five — a pace of
        /// "0 minutes a day" would read as "nothing to do".
        /// </summary>
        public static PortfolioDurationParts DurationParts(double hours)
        {
            if (hours <= 0) return new PortfolioDurationParts(0, 0);

            int steps = SwiftRounding.RoundedToInt(hours * MinutesPerHour / PaceRoundMinutes);
            int minutes = Math.Max(PaceRoundMinutes, steps * PaceRoundMinutes);
            return new PortfolioDurationParts(minutes / MinutesPerHour, minutes % MinutesPerHour);
        }

        /// <summary>
        /// Web `getRiskRank` / `sortProjectsByRisk` for the All-projects grid: riskiest first, projects
        /// with no deadline (and projects the portfolio never scored) last.
        /// </summary>
        public static IList<Project> SortByRisk(IEnumerable<Project> projects,
                                                IDictionary<string, PortfolioProject> risks)
        {
            return (projects ?? Enumerable.Empty<Project>())
                .Select((project, index) => new { project, index })
                .OrderByDescending(entry => Rank(entry.project, risks))
                .ThenBy(entry => entry.index)
                .Select(entry => entry.project)
                .ToList();
        }

        /// <summary>Below every real risk score, which the server keeps at or above zero.</summary>
        private const double UnrankedRisk = -1;

        private static double Rank(Project project, IDictionary<string, PortfolioProject> risks)
        {
            PortfolioProject risk;
            if (risks == null || !risks.TryGetValue(project.Id, out risk)) return UnrankedRisk;
            return risk.Bucket == PortfolioBucket.NoDeadline ? UnrankedRisk : risk.Risk;
        }
    }
}
