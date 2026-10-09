using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>`PortfolioBucket` — computed server-side by `GET /portfolio`; never recomputed on device.</summary>
    public enum PortfolioBucket
    {
        OnTrack,
        AtRisk,
        Critical,
        NoDeadline,
    }

    /// <summary>The wire spellings of <see cref="PortfolioBucket"/>.</summary>
    public static class PortfolioBucketWire
    {
        public const string OnTrack = "on_track";
        public const string AtRisk = "at_risk";
        public const string Critical = "critical";
        public const string NoDeadline = "no_deadline";

        public static string Of(PortfolioBucket bucket)
        {
            switch (bucket)
            {
                case PortfolioBucket.OnTrack: return OnTrack;
                case PortfolioBucket.AtRisk: return AtRisk;
                case PortfolioBucket.Critical: return Critical;
                case PortfolioBucket.NoDeadline: return NoDeadline;
                default: throw new ArgumentOutOfRangeException(nameof(bucket));
            }
        }

        /// <summary>An unrecognised bucket is treated as having no deadline, so it cannot raise an alarm.</summary>
        public static PortfolioBucket Parse(string raw)
        {
            switch (raw)
            {
                case OnTrack: return PortfolioBucket.OnTrack;
                case AtRisk: return PortfolioBucket.AtRisk;
                case Critical: return PortfolioBucket.Critical;
                default: return PortfolioBucket.NoDeadline;
            }
        }
    }

    /// <summary>
    /// `PortfolioBadge`. <see cref="Unscheduled"/> / <see cref="PushedBack"/> come from the backend's
    /// Day Plan simulation (backend#2363).
    /// </summary>
    public enum PortfolioBadge
    {
        Overcommitted,
        StartNow,
        Sacrifice,
        Unscheduled,
        PushedBack,

        /// <summary>
        /// A badge this build does not know. It decodes rather than failing the whole portfolio, so a
        /// newer backend can add badges without breaking older clients.
        /// </summary>
        Unknown,
    }

    /// <summary>The wire spellings of <see cref="PortfolioBadge"/>.</summary>
    public static class PortfolioBadgeWire
    {
        public const string Overcommitted = "overcommitted";
        public const string StartNow = "start_now";
        public const string Sacrifice = "sacrifice";
        public const string Unscheduled = "unscheduled";
        public const string PushedBack = "pushed_back";

        public static string Of(PortfolioBadge badge)
        {
            switch (badge)
            {
                case PortfolioBadge.Overcommitted: return Overcommitted;
                case PortfolioBadge.StartNow: return StartNow;
                case PortfolioBadge.Sacrifice: return Sacrifice;
                case PortfolioBadge.Unscheduled: return Unscheduled;
                case PortfolioBadge.PushedBack: return PushedBack;
                default: throw new ArgumentOutOfRangeException(nameof(badge));
            }
        }

        public static PortfolioBadge Parse(string raw)
        {
            switch (raw)
            {
                case Overcommitted: return PortfolioBadge.Overcommitted;
                case StartNow: return PortfolioBadge.StartNow;
                case Sacrifice: return PortfolioBadge.Sacrifice;
                case Unscheduled: return PortfolioBadge.Unscheduled;
                case PushedBack: return PortfolioBadge.PushedBack;
                default: return PortfolioBadge.Unknown;
            }
        }
    }

    /// <summary>`PortfolioImportance` — the 1-4 the project carries.</summary>
    public enum PortfolioImportance
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4,
    }

    /// <summary>
    /// The web's three "This week" groups of the "What matters" view, plus the Mac's "steady progress"
    /// lane between Start now and Backburner (<see cref="PortfolioLanes"/>).
    /// </summary>
    public enum PortfolioActionSection
    {
        AskForExtension,
        StartNow,
        SteadyProgress,
        BackBurner,
    }

    /// <summary>
    /// `PortfolioProjectRisk` — one project's row of `GET /portfolio`, as far as the grouping rules
    /// read it. The bucket and the risk score are the server's answers and are never recomputed here;
    /// the payload's own `top_priority_task` is ignored entirely (see <see cref="PortfolioNextTask"/>).
    /// </summary>
    public sealed class PortfolioProject
    {
        public string Id { get; set; }
        public string Name { get; set; }

        /// <summary>ISO-8601, and compared as a string — see <see cref="PortfolioGrouping"/>.</summary>
        public string DueDate { get; set; }

        public int? Importance { get; set; }

        /// <summary>Remaining effort, in hours.</summary>
        public double? RequiredHours { get; set; }

        public double? RiskScore { get; set; }

        /// <summary>Raw `bucket`; read it through <see cref="Bucket"/>.</summary>
        public string BucketRaw { get; set; }

        /// <summary>Negative once the due date has passed; null when the project has no deadline.</summary>
        public int? DaysUntilDue { get; set; }

        /// <summary>Raw `badges`; read them through <see cref="BadgeList"/>.</summary>
        public IList<string> Badges { get; set; } = new List<string>();

        public PortfolioBucket Bucket => PortfolioBucketWire.Parse(BucketRaw);

        public IList<PortfolioBadge> BadgeList =>
            (Badges ?? new List<string>()).Select(PortfolioBadgeWire.Parse).ToList();

        public PortfolioImportance ImportanceLevel => ParseImportance(Importance);

        public double Risk => RiskScore ?? 0;

        public double Hours => RequiredHours ?? 0;

        public bool HasDueDate => !string.IsNullOrEmpty(DueDate);

        public bool IsOverdue => DaysUntilDue.HasValue && DaysUntilDue.Value < 0;

        /// <summary>Remaining work with no task planned inside the forecast horizon.</summary>
        public bool IsUnscheduled => BadgeList.Contains(PortfolioBadge.Unscheduled);

        /// <summary>An unknown or absent importance is medium, as the Mac's decoder makes it.</summary>
        private static PortfolioImportance ParseImportance(int? raw)
        {
            if (!raw.HasValue) return PortfolioImportance.Medium;
            return Enum.IsDefined(typeof(PortfolioImportance), raw.Value)
                ? (PortfolioImportance)raw.Value
                : PortfolioImportance.Medium;
        }
    }

    /// <summary>
    /// The task a project card offers as its NEXT one. Deliberately NOT read from the payload's
    /// `top_priority_task` — see <see cref="PortfolioNextTask"/> — so <see cref="TopScore"/> is always
    /// the locally computed TOP score.
    /// </summary>
    public sealed class ProjectTopPriorityTask
    {
        public ProjectTopPriorityTask(string id, string title, double topScore)
        {
            Id = id;
            Title = title;
            TopScore = topScore;
        }

        public string Id { get; }
        public string Title { get; }
        public double TopScore { get; }
    }
}
