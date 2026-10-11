using System;
using System.Collections.Generic;
using System.Linq;
using ChangeKind = FocusBear.Core.Forecast.LocalTimeForecast.ChangeKind;
using DueDateChange = FocusBear.Core.Forecast.LocalTimeForecast.DueDateChange;
using PlanConflict = FocusBear.Core.Forecast.LocalTimeForecast.PlanConflict;

namespace FocusBear.Core.Forecast
{
    /// <summary>
    /// When to offer "Reprioritise" (the Mac's <c>ReprioritiseSuggestion</c>): the PO's rule, verbatim —
    /// "the banner should appear if they have &gt; 4 overdue tasks or the next 5 days have negative buffer
    /// of 2h or more". A dismissed suggestion stays quiet for a week.
    /// </summary>
    public static class ReprioritiseSuggestion
    {
        public enum ReasonKind { Overdue, DayOverCapacity }

        public struct Reason : IEquatable<Reason>
        {
            public Reason(ReasonKind kind, int overdueCount, double overMinutes)
            {
                Kind = kind; OverdueCount = overdueCount; OverMinutes = overMinutes;
            }

            public ReasonKind Kind { get; }
            public int OverdueCount { get; }
            /// <summary>The worst SINGLE day inside <see cref="HorizonDays"/>, and how far over it is.</summary>
            public double OverMinutes { get; }

            public bool Equals(Reason other) { return Kind == other.Kind && OverdueCount == other.OverdueCount && OverMinutes.Equals(other.OverMinutes); }
            public override bool Equals(object obj) { return obj is Reason && Equals((Reason)obj); }
            public override int GetHashCode() { return (int)Kind ^ OverdueCount ^ OverMinutes.GetHashCode(); }
        }

        /// <summary>"&gt; 4" taken literally: five or more.</summary>
        public const int OverdueThreshold = 5;
        /// <summary>"Massively over", not merely over: a day two hours over is one no effort rescues.</summary>
        public const double MassivelyOverMinutes = 120;
        /// <summary>Today plus the next four. The same constant bounds the query and feeds the sentence.</summary>
        public const int HorizonDays = 5;
        public const int DismissalDays = 7;

        /// <summary>null = say nothing. <paramref name="worstDayOvercommitMinutes"/> is one day's deficit, never a sum.</summary>
        public static Reason? For(int overdueCount, double worstDayOvercommitMinutes, DateTime? dismissedAt, DateTime now)
        {
            if (dismissedAt.HasValue && now < dismissedAt.Value.AddDays(DismissalDays)) return null;
            if (overdueCount >= OverdueThreshold) return new Reason(ReasonKind.Overdue, overdueCount, 0);
            if (worstDayOvercommitMinutes >= MassivelyOverMinutes) return new Reason(ReasonKind.DayOverCapacity, 0, worstDayOvercommitMinutes);
            return null;
        }

        /// <summary>The worst single day's over-commitment in the next <see cref="HorizonDays"/> days (today included).</summary>
        public static double WorstDayOvercommitMinutes(LocalTimeForecast.Result forecast, DateTime today)
        {
            if (forecast == null) return 0;
            DateTime start = today.Date, end = start.AddDays(HorizonDays);
            return forecast.Days.Where(d => d.Date >= start && d.Date < end)
                .Select(d => LocalTimeForecast.DayLoadOn(d.Date, forecast))
                .Where(l => l.HasValue).Select(l => l.Value.OvercommittedMinutes)
                .DefaultIfEmpty(0).Max();
        }
    }

    /// <summary>The reschedule sheet's grouping rules (the Mac's <c>ForecastChangeGrouping</c> / <c>ForecastConflictGrouping</c>).</summary>
    public static class ForecastGrouping
    {
        public const string Unprojected = "__unprojected";
        /// <summary>"Major changes (moved &gt; 5 days)" / "Minor (&lt; 5)": 5 itself counts as major.</summary>
        public const int MajorThresholdDays = 5;

        public sealed class Group<T>
        {
            public Group(string projectId, IReadOnlyList<T> rows) { ProjectId = projectId; Rows = rows; }
            /// <summary>null for rows with no project.</summary>
            public string ProjectId { get; }
            public IReadOnlyList<T> Rows { get; }
        }

        /// <summary>Whole days moved, absolute (earlier interferes as much as later). No previous date = the biggest move.</summary>
        public static int DaysMoved(DueDateChange change)
        {
            if (!change.PreviousDate.HasValue) return int.MaxValue;
            return Math.Abs((int)Math.Round((change.ProposedDate.Date - change.PreviousDate.Value.Date).TotalDays));
        }

        public static bool IsMajor(DueDateChange change) { return DaysMoved(change) >= MajorThresholdDays; }

        /// <summary>Project-deadline rows are split out: moving a whole project's deadline is a different act.</summary>
        public static void Split(IEnumerable<DueDateChange> changes, out List<DueDateChange> tasks, out List<DueDateChange> projectDeadlines)
        {
            var all = (changes ?? Enumerable.Empty<DueDateChange>()).ToList();
            tasks = all.Where(c => c.Kind == ChangeKind.Task).ToList();
            projectDeadlines = all.Where(c => c.Kind == ChangeKind.Project).ToList();
        }

        /// <summary>Grouped by project in first-seen order, so the list still reads "what gets done first".</summary>
        public static List<Group<DueDateChange>> Groups(IEnumerable<DueDateChange> changes, Func<string, string> taskProjectId)
        {
            return GroupBy(changes, c => c.Kind == ChangeKind.Project ? c.ItemId : taskProjectId(c.ItemId));
        }

        public static List<Group<PlanConflict>> ConflictGroups(IEnumerable<PlanConflict> conflicts, Func<string, string> taskProjectId)
        {
            return GroupBy(conflicts, c => c.Kind == ChangeKind.Project ? c.ItemId : taskProjectId(c.ItemId));
        }

        private static List<Group<T>> GroupBy<T>(IEnumerable<T> rows, Func<T, string> key)
        {
            var order = new List<string>();
            var byKey = new Dictionary<string, List<T>>();
            foreach (T row in rows ?? Enumerable.Empty<T>())
            {
                string k = key(row) ?? Unprojected;
                List<T> list;
                if (!byKey.TryGetValue(k, out list)) { list = new List<T>(); byKey[k] = list; order.Add(k); }
                list.Add(row);
            }
            return order.Select(k => new Group<T>(k == Unprojected ? null : k, byKey[k])).ToList();
        }

        public enum SelectionState { None, Some, All }

        /// <summary>A bulk control takes TASK changes it can apply, and nothing else (never a project deadline).</summary>
        public static List<string> BulkSelectableIds(IEnumerable<DueDateChange> changes, Func<DueDateChange, bool> canApply)
        {
            return (changes ?? Enumerable.Empty<DueDateChange>()).Where(c => c.Kind == ChangeKind.Task).Where(canApply).Select(c => c.Id).ToList();
        }

        public static SelectionState State(IReadOnlyCollection<string> selectable, ISet<string> selected)
        {
            if (selectable == null || selectable.Count == 0) return SelectionState.None;
            int chosen = selectable.Count(selected.Contains);
            if (chosen == 0) return SelectionState.None;
            return chosen == selectable.Count ? SelectionState.All : SelectionState.Some;
        }

        /// <summary>Everything selected → clear the group; anything else → select all of it.</summary>
        public static HashSet<string> Toggled(IReadOnlyCollection<string> selectable, ISet<string> selected)
        {
            var updated = new HashSet<string>(selected);
            if (State(selectable, selected) == SelectionState.All) foreach (string id in selectable) updated.Remove(id);
            else foreach (string id in selectable) updated.Add(id);
            return updated;
        }
    }

    /// <summary>Opening the reschedule sheet from one day's "Fix" (the Mac's <c>ForecastDayFocus</c>).</summary>
    public static class ForecastDayFocus
    {
        public struct Target
        {
            public Target(DateTime day, double shortfallMinutes) { Day = day; ShortfallMinutes = shortfallMinutes; }
            public DateTime Day { get; }
            public double ShortfallMinutes { get; }
        }

        public struct Relief
        {
            public Relief(double freed, double shortfall, int immovableCount, double immovableMinutes)
            {
                FreedMinutes = freed; ShortfallMinutes = shortfall; ImmovableCount = immovableCount; ImmovableMinutes = immovableMinutes;
            }

            public double FreedMinutes { get; }
            public double ShortfallMinutes { get; }
            public int ImmovableCount { get; }
            public double ImmovableMinutes { get; }
            public double RemainingMinutes { get { return Math.Max(0, ShortfallMinutes - FreedMinutes); } }
            public bool ClearsTheDay { get { return RemainingMinutes <= LocalTimeForecast.CapacityEpsilon; } }
            public bool RemainderIsPinned { get { return !ClearsTheDay && ImmovableMinutes >= RemainingMinutes - LocalTimeForecast.CapacityEpsilon; } }
        }

        /// <summary>A proposal takes work off the day when the task sits on that day and the new date is another.</summary>
        public static bool MovesWorkOff(DateTime day, DueDateChange change, Func<string, DateTime?> plannedDay)
        {
            if (change.Kind != ChangeKind.Task) return false;
            DateTime? planned = plannedDay(change.ItemId);
            return planned.HasValue && planned.Value.Date == day.Date && change.ProposedDate.Date != day.Date;
        }

        public static List<DueDateChange> ChangesMovingWorkOff(DateTime day, IEnumerable<DueDateChange> changes, Func<string, DateTime?> plannedDay)
        {
            return (changes ?? Enumerable.Empty<DueDateChange>()).Where(c => MovesWorkOff(day, c, plannedDay)).ToList();
        }

        public static Relief ReliefFor(IEnumerable<DueDateChange> freeing, Target target, Func<string, double> minutes,
                                       IEnumerable<double> immovableMinutes)
        {
            var immovable = (immovableMinutes ?? Enumerable.Empty<double>()).ToList();
            return new Relief((freeing ?? Enumerable.Empty<DueDateChange>()).Sum(c => minutes(c.ItemId)),
                              Math.Max(0, target.ShortfallMinutes), immovable.Count, immovable.Sum());
        }
    }

    /// <summary>
    /// Where an applied reschedule leaves the user's Day Plan (the Mac's <c>DayPlanRepositioning</c>): a task
    /// placed on a day today or later follows its new date, unless that date is a locked day.
    /// </summary>
    public static class DayPlanRepositioning
    {
        public struct Move
        {
            public Move(string taskId, DateTime from, DateTime to) { TaskId = taskId; From = from; To = to; }
            public string TaskId { get; }
            public DateTime From { get; }
            public DateTime To { get; }
        }

        public static Move? For(string taskId, DateTime? plannedOn, DateTime proposedDate, DateTime today, IEnumerable<DateTime> lockedDays)
        {
            if (!plannedOn.HasValue) return null;
            DateTime from = plannedOn.Value.Date, to = proposedDate.Date, t = today.Date;
            if (from < t || to < t || from == to) return null;
            if ((lockedDays ?? Enumerable.Empty<DateTime>()).Any(d => d.Date == to)) return null;
            return new Move(taskId, from, to);
        }
    }
}
