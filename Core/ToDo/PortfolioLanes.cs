using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>
    /// Which "What matters" lane a project sits in — the Mac's <c>PortfolioLanes</c>. One ordered rule list,
    /// first match wins, so the lane can be predicted from what the card itself shows. Lanes measure time
    /// pressure only.
    ///
    /// 1. Every task is done → Backburner, below every project that still has work.
    /// 2. No due date → Backburner. Without a deadline nothing can be due soon or at risk.
    /// 3. The known work cannot be finished by the due date → Ask for an extension.
    /// 4. Nothing open to work on (an empty project) → Backburner, however near its due date.
    /// 5. Due within <see cref="FocusWindowDays"/> (today included), or overdue → Start now.
    /// 6. Due after the planning horizon → Backburner. Something that far out is never "start now".
    /// 7. At risk: the forecast only finishes it on its due date, or some tasks have no estimate → Start now.
    /// 8. Any other due date (inside the horizon, with room to spare) → On track – steady progress.
    ///
    /// Inside a lane: higher TOP score first, then the sooner due date, then the forecast's own order.
    ///
    /// The TOP score orders cards but never picks a lane. It belongs to the NEXT task, not the project,
    /// and is unbounded (an overdue task scores 10 plus its days overdue), so a "TOP ≥ 8 → Start now" rule
    /// filed every undated project with one stale task above a project due in three days (Mac report
    /// 2026-10-05_08-24-02). An empty project sits below a near due date because Start now is for work
    /// that can be started (same report, follow-up).
    /// </summary>
    public static class PortfolioLanes
    {
        /// <summary>One week: the span the view is built around ("Nothing needs attention this week").</summary>
        public const int FocusWindowDays = 7;

        /// <summary>What a lane is decided from — all of it visible on the card.</summary>
        public struct Facts : IEquatable<Facts>
        {
            public Facts(PortfolioBucket bucket, int? daysUntilDue, bool isBeyondHorizon, double? topScore,
                         bool hasOpenWork, bool isFinished)
            {
                Bucket = bucket; DaysUntilDue = daysUntilDue; IsBeyondHorizon = isBeyondHorizon; TopScore = topScore;
                HasOpenWork = hasOpenWork; IsFinished = isFinished;
            }

            public PortfolioBucket Bucket { get; }

            /// <summary>Calendar days from today to the due date; negative when overdue, null without one.</summary>
            public int? DaysUntilDue { get; }

            public bool IsBeyondHorizon { get; }
            public double? TopScore { get; }

            /// <summary>At least one task is still open.</summary>
            public bool HasOpenWork { get; }

            /// <summary>It had tasks and every one of them is done.</summary>
            public bool IsFinished { get; }

            public bool Equals(Facts other)
            {
                return Bucket == other.Bucket && DaysUntilDue == other.DaysUntilDue && IsBeyondHorizon == other.IsBeyondHorizon
                    && Nullable.Equals(TopScore, other.TopScore) && HasOpenWork == other.HasOpenWork && IsFinished == other.IsFinished;
            }

            public override bool Equals(object obj) => obj is Facts other && Equals(other);
            public override int GetHashCode() => (int)Bucket ^ (DaysUntilDue ?? 0) ^ (HasOpenWork ? 1 : 0) ^ (IsFinished ? 2 : 0);
        }

        /// <summary>A portfolio row with the facts its lane is decided from.</summary>
        public sealed class Entry
        {
            public Entry(PortfolioProject row, Facts facts) { Row = row; Facts = facts; }
            public PortfolioProject Row { get; }
            public Facts Facts { get; }
        }

        public static PortfolioActionSection Section(Facts facts)
        {
            if (facts.IsFinished || facts.Bucket == PortfolioBucket.NoDeadline) return PortfolioActionSection.BackBurner;
            if (facts.Bucket == PortfolioBucket.Critical) return PortfolioActionSection.AskForExtension;
            if (!facts.HasOpenWork) return PortfolioActionSection.BackBurner;
            if (IsDueSoon(facts)) return PortfolioActionSection.StartNow;
            if (facts.IsBeyondHorizon) return PortfolioActionSection.BackBurner;
            return facts.Bucket == PortfolioBucket.AtRisk ? PortfolioActionSection.StartNow : PortfolioActionSection.SteadyProgress;
        }

        /// <summary>Rows in the forecast's order, regrouped into lanes and ordered within each.</summary>
        public static PortfolioActionGroups Group(IEnumerable<Entry> entries)
        {
            var indexed = (entries ?? Enumerable.Empty<Entry>()).Select((entry, offset) => new { entry, offset }).ToList();
            IList<PortfolioProject> Lane(PortfolioActionSection section) => indexed
                .Where(item => Section(item.entry.Facts) == section)
                .OrderBy(item => item.entry.Facts.IsFinished)
                .ThenByDescending(item => item.entry.Facts.TopScore ?? double.NegativeInfinity)
                .ThenBy(item => item.entry.Facts.DaysUntilDue ?? int.MaxValue)
                .ThenBy(item => item.offset)
                .Select(item => item.entry.Row)
                .ToList();

            return new PortfolioActionGroups
            {
                AskForExtension = Lane(PortfolioActionSection.AskForExtension),
                StartNow = Lane(PortfolioActionSection.StartNow),
                SteadyProgress = Lane(PortfolioActionSection.SteadyProgress),
                BackBurner = Lane(PortfolioActionSection.BackBurner),
            };
        }

        /// <summary>Overdue counts as due soon: <see cref="Section"/> has already sent projects with nothing open elsewhere.</summary>
        private static bool IsDueSoon(Facts facts)
        {
            return facts.DaysUntilDue.HasValue && facts.DaysUntilDue.Value <= FocusWindowDays;
        }
    }
}
