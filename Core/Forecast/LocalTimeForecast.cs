using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Schedule;

namespace FocusBear.Core.Forecast
{
    /// <summary>
    /// Ported from the Mac's <c>LocalTimeForecast.swift</c>: the pure Day Plan capacity and priority
    /// allocation behind "Reprioritise" (the reschedule plan), the Day Plan's buffer / over-capacity
    /// warning and the "Fix" modal's task route. No API, persistence, UI or localisation dependencies.
    ///
    /// All amounts are minutes; fractional minutes preserve short habits and partial calendar events.
    /// Dates are local wall-clock <see cref="DateTime"/>s; a day is its <c>.Date</c>. (The Swift engine
    /// used <c>Calendar</c> so a DST day keeps its real length; here a day is always 24 wall-clock hours,
    /// which moves a DST day's capacity by at most an hour.)
    /// </summary>
    public static class LocalTimeForecast
    {
        /// <summary>Below this a day counts as full, so float arithmetic can't leave a millionth of a minute looking free.</summary>
        public const double CapacityEpsilon = 0.000001;

        /// <summary>The shortest stretch that counts as time to work in (ten minutes, by the user's call).</summary>
        public const double MinimumUsableWorkMinutes = 10;

        /// <summary>Today and tomorrow are taken as settled ("assume the day plan for today + tomorrow is locked in").</summary>
        public const int LockedPlanDays = 2;

        public sealed class CapacitySettings
        {
            public int SleepStartMinute { get; set; } = 22 * 60;
            public int SleepEndMinute { get; set; } = 6 * 60;
            /// <summary>Extra time not already represented by calendar events or placed habits.</summary>
            public int SelfCareMinutes { get; set; }

            public bool IsValid
            {
                get
                {
                    return SleepStartMinute >= 0 && SleepStartMinute < 1440 && SleepEndMinute >= 0 && SleepEndMinute < 1440
                           && SleepStartMinute != SleepEndMinute && SelfCareMinutes >= 0 && SelfCareMinutes <= 1440;
                }
            }
        }

        public struct Interval : IEquatable<Interval>
        {
            public Interval(DateTime start, DateTime end) { Start = start; End = end; }
            public DateTime Start { get; }
            public DateTime End { get; }
            public double Minutes { get { return (End - Start).TotalMinutes; } }
            public bool Equals(Interval other) { return Start == other.Start && End == other.End; }
            public override bool Equals(object obj) { return obj is Interval && Equals((Interval)obj); }
            public override int GetHashCode() { return Start.GetHashCode() ^ End.GetHashCode(); }
        }

        public enum BlockKind { Meeting, Habit }

        /// <summary>
        /// Time the day has already committed to something placed on it — a calendar event, or a habit.
        /// Task work dropped onto one of these is ABSORBED by it, so it isn't charged twice.
        /// </summary>
        public sealed class PlacedBlock
        {
            public PlacedBlock(string id, BlockKind kind, string blockKey, Interval interval)
            {
                Id = id; Kind = kind; BlockKey = blockKey; Interval = interval;
            }

            /// <summary>A single scheduled occurrence.</summary>
            public string Id { get; }
            public BlockKind Kind { get; }
            /// <summary>The Day Plan block key a task carries once pinned here; null when nothing can be pinned.</summary>
            public string BlockKey { get; }
            public Interval Interval { get; }

            public static PlacedBlock Meeting(string id, Interval interval, string blockKey = null)
            {
                return new PlacedBlock(id, BlockKind.Meeting, blockKey, interval);
            }

            public static PlacedBlock Habit(string id, string blockKey, Interval interval)
            {
                return new PlacedBlock(id, BlockKind.Habit, blockKey, interval);
            }
        }

        public sealed class DayInput
        {
            public DayInput(DateTime date, IEnumerable<PlacedBlock> blocks, CapacitySettings settings = null,
                            DateTime? availableFrom = null, bool isLocked = false)
            {
                Date = date;
                Blocks = (blocks ?? Enumerable.Empty<PlacedBlock>()).ToList();
                Settings = settings ?? new CapacitySettings();
                AvailableFrom = availableFrom;
                IsLocked = isLocked;
            }

            public DateTime Date { get; }
            /// <summary>Everything placed on the day: calendar events are charged first, then habits.</summary>
            public IReadOnlyList<PlacedBlock> Blocks { get; }
            public CapacitySettings Settings { get; }
            public DateTime? AvailableFrom { get; }
            /// <summary>The day's plan is settled: it holds only work already placed on it.</summary>
            public bool IsLocked { get; set; }

            /// <summary>Callers that only measure capacity: bare event intervals, identified by their span.</summary>
            public static DayInput FromIntervals(DateTime date, IEnumerable<Interval> meetings, IEnumerable<PlacedBlock> habits,
                                                 CapacitySettings settings = null, DateTime? availableFrom = null, bool isLocked = false)
            {
                var blocks = (meetings ?? Enumerable.Empty<Interval>())
                    .Select(i => PlacedBlock.Meeting("meeting:" + i.Start.Ticks + "-" + i.End.Ticks, i))
                    .Concat(habits ?? Enumerable.Empty<PlacedBlock>());
                return new DayInput(date, blocks, settings, availableFrom, isLocked);
            }

            /// <summary>
            /// The Day Plan's placed blocks are authoritative: meetings, and habits that got a slot. Overflow
            /// markers, sleep and deep-work fillers must not consume the budget a second time — a deep-work
            /// block IS the day's work time.
            /// </summary>
            public static DayInput FromDayPlan(DateTime date, IEnumerable<ScheduleBlock> blocks, CapacitySettings settings = null,
                                               DateTime? availableFrom = null, bool isLocked = false)
            {
                var list = (blocks ?? Enumerable.Empty<ScheduleBlock>()).ToList();
                var placed = new List<PlacedBlock>();
                foreach (ScheduleBlock b in list.Where(b => b.Kind == ScheduleBlockKind.Meeting))
                {
                    placed.Add(PlacedBlock.Meeting((b.EventId ?? b.Title) + ":" + b.Start.ToUnixTimeSeconds(),
                                                   new Interval(b.Start.LocalDateTime, b.End.LocalDateTime),
                                                   b.EventId == null ? null : DayPlanBlockKey.Event(b.EventId)));
                }
                foreach (ScheduleBlock b in list.Where(b => b.Kind == ScheduleBlockKind.Habit && !b.DidOverflow))
                {
                    placed.Add(PlacedBlock.Habit((b.HabitId ?? b.Title) + ":" + b.Start.ToUnixTimeSeconds(),
                                                 DayPlanBlockKey.Habit(b.Title),
                                                 new Interval(b.Start.LocalDateTime, b.End.LocalDateTime)));
                }
                return new DayInput(date, placed, settings, availableFrom, isLocked);
            }
        }

        public sealed class ProjectInput
        {
            public ProjectInput(string id, int outcome = 2, DateTime? dueDate = null, int? totalTaskCount = null,
                                int? uncompletedTaskCount = null)
            {
                Id = id; Outcome = outcome; DueDate = dueDate;
                TotalTaskCount = totalTaskCount; UncompletedTaskCount = uncompletedTaskCount;
            }

            public string Id { get; }
            /// <summary>The server's /10 outcome. 0 = unrated ("no opinion"), which never tightens a task.</summary>
            public int Outcome { get; }
            public DateTime? DueDate { get; }
            public int? TotalTaskCount { get; }
            public int? UncompletedTaskCount { get; }
            public bool HasRating { get { return Outcome > 0; } }

            /// <summary>Share of the project's tasks already finished, by count, when the counters make sense.</summary>
            public double? CompletedShare
            {
                get
                {
                    if (!TotalTaskCount.HasValue || !UncompletedTaskCount.HasValue) return null;
                    int total = TotalTaskCount.Value, open = UncompletedTaskCount.Value;
                    if (total <= 0 || open < 0 || open > total) return null;
                    return (double)(total - open) / total;
                }
            }
        }

        /// <summary>A task pinned to one block on one day — the Day Plan drop that says "do this inside that".</summary>
        public struct BlockAssignment : IEquatable<BlockAssignment>
        {
            public BlockAssignment(DateTime date, string blockKey) { Date = date; BlockKey = blockKey; }
            public DateTime Date { get; }
            public string BlockKey { get; }
            public bool Equals(BlockAssignment other) { return Date == other.Date && BlockKey == other.BlockKey; }
            public override bool Equals(object obj) { return obj is BlockAssignment && Equals((BlockAssignment)obj); }
            public override int GetHashCode() { return Date.GetHashCode() ^ (BlockKey ?? "").GetHashCode(); }
        }

        public sealed class TaskInput
        {
            /// <summary>What the forecast plans with when a task carries no rating of its own.</summary>
            public const int UnratedOutcome = 2;

            public TaskInput(string id, string projectId = null, double? estimatedMinutes = null, int outcome = UnratedOutcome,
                             int perspiration = 1, DateTime? dueDate = null, DateTime? notBefore = null,
                             BlockAssignment? blockAssignment = null, bool completed = false)
            {
                Id = id; ProjectId = projectId; EstimatedMinutes = estimatedMinutes; Outcome = outcome;
                Perspiration = perspiration; DueDate = dueDate; NotBefore = notBefore;
                BlockAssignment = blockAssignment; Completed = completed;
            }

            public string Id { get; }
            public string ProjectId { get; }
            /// <summary>null or &lt;= 0 is unknown effort, never a free task.</summary>
            public double? EstimatedMinutes { get; }
            public int Outcome { get; }
            public int Perspiration { get; }
            public DateTime? DueDate { get; }
            /// <summary>An explicit future Day Plan placement: a lower bound, not a deadline.</summary>
            public DateTime? NotBefore { get; }
            public BlockAssignment? BlockAssignment { get; }
            public bool Completed { get; }

            /// <summary>The day this task is explicitly PLACED on (not its deadline).</summary>
            public DateTime? PinnedDay
            {
                get
                {
                    var candidates = new List<DateTime>();
                    if (NotBefore.HasValue) candidates.Add(NotBefore.Value);
                    if (BlockAssignment.HasValue) candidates.Add(BlockAssignment.Value.Date);
                    return candidates.Count == 0 ? (DateTime?)null : candidates.Max();
                }
            }

            public TaskInput With(double? estimatedMinutes = null, int? outcome = null, string projectId = null)
            {
                return new TaskInput(Id, projectId ?? ProjectId, estimatedMinutes ?? EstimatedMinutes, outcome ?? Outcome,
                                     Perspiration, DueDate, NotBefore, BlockAssignment, Completed);
            }
        }

        public sealed class Capacity
        {
            public DateTime Day { get; set; }
            public double MeetingMinutes { get; set; }
            /// <summary>Union of placed habit intervals after removing calendar overlaps.</summary>
            public double HabitMinutes { get; set; }
            public double SleepMinutes { get; set; }
            public double SelfCareMinutes { get; set; }
            public double TotalMinutes { get; set; }
            /// <summary>Work minutes in stretches long enough to actually work in; null without interval detail.</summary>
            public double? UsableWorkMinutes { get; set; }
            public double SleepAndSelfCareMinutes { get { return SleepMinutes + SelfCareMinutes; } }
            public double RawWorkMinutes { get { return TotalMinutes - MeetingMinutes - HabitMinutes - SleepAndSelfCareMinutes; } }
            /// <summary>A day whose only gaps are three and five minutes long has no time for work, whatever the subtraction says.</summary>
            public double AvailableWorkMinutes { get { return Math.Max(0, Math.Min(RawWorkMinutes, UsableWorkMinutes ?? RawWorkMinutes)); } }
            public double OvercommittedMinutes { get { return Math.Max(0, -RawWorkMinutes); } }
        }

        public sealed class Allocation
        {
            public Allocation(string taskId, string projectId, DateTime day, double workMinutes, double absorbedBlockMinutes)
            {
                TaskId = taskId; ProjectId = projectId; Day = day; WorkMinutes = workMinutes; AbsorbedBlockMinutes = absorbedBlockMinutes;
            }

            public string TaskId { get; }
            public string ProjectId { get; }
            public DateTime Day { get; }
            public double WorkMinutes { get; }
            public double AbsorbedBlockMinutes { get; }
            public double TotalMinutes { get { return WorkMinutes + AbsorbedBlockMinutes; } }
        }

        public sealed class Day
        {
            public Day(Capacity capacity, IReadOnlyList<Allocation> allocations, bool isLocked)
            {
                Capacity = capacity; Allocations = allocations; IsLocked = isLocked;
            }

            public Capacity Capacity { get; }
            public IReadOnlyList<Allocation> Allocations { get; }
            public bool IsLocked { get; internal set; }
            public DateTime Date { get { return Capacity.Day; } }
            public double AllocatedWorkMinutes { get { return Allocations.Sum(a => a.WorkMinutes); } }
            public double AbsorbedBlockMinutes { get { return Allocations.Sum(a => a.AbsorbedBlockMinutes); } }
            /// <summary>Minutes of the work window nothing has claimed yet (a locked day still reports its real leftover).</summary>
            public double RemainingWorkMinutes { get { return Math.Max(0, Capacity.AvailableWorkMinutes - AllocatedWorkMinutes); } }
        }

        public sealed class TaskResult
        {
            public TaskResult(TaskInput task, IReadOnlyList<Allocation> allocations, double unallocatedMinutes)
            {
                Task = task; Allocations = allocations; UnallocatedMinutes = unallocatedMinutes;
            }

            public TaskInput Task { get; }
            public IReadOnlyList<Allocation> Allocations { get; }
            public double UnallocatedMinutes { get; }
            public bool HasEstimate { get { return Estimate(Task).HasValue; } }

            public DateTime? CompletionDate
            {
                get
                {
                    if (!HasEstimate || Task.Completed || UnallocatedMinutes >= CapacityEpsilon) return null;
                    return Allocations.Count == 0 ? (DateTime?)null : Allocations[Allocations.Count - 1].Day;
                }
            }
        }

        public sealed class ProjectResult
        {
            public ProjectInput Project { get; set; }
            public double EstimatedMinutes { get; set; }
            public double CompletedMinutes { get; set; }
            public double AllocatedByDeadlineMinutes { get; set; }
            public int UnknownTaskCount { get; set; }
            public int OutstandingTaskCount { get; set; }
            public DateTime? CompletionDate { get; set; }
            public bool HasUnallocatedWork { get; set; }
            public string Id { get { return Project.Id; } }
            public bool IsFinished { get { return OutstandingTaskCount == 0 && (Project.TotalTaskCount ?? 0) > 0; } }

            /// <summary>How much of the whole project is expected to be done by its due day (or the horizon's end).</summary>
            public int? LikelyCompletionPercent
            {
                get
                {
                    if (IsFinished) return 100;
                    if (!(EstimatedMinutes > 0) || UnknownTaskCount != 0) return null;
                    double remainingShare = Math.Min(1, Math.Max(0, (CompletedMinutes + AllocatedByDeadlineMinutes) / EstimatedMinutes));
                    double done = Project.CompletedShare ?? 0;
                    return Math.Min(100, Math.Max(0, (int)Math.Floor(100 * (done + (1 - done) * remainingShare))));
                }
            }
        }

        public sealed class Result
        {
            public Result(IReadOnlyList<Day> days, IReadOnlyList<TaskResult> tasks, IReadOnlyList<ProjectResult> projects)
            {
                Days = days; Tasks = tasks; Projects = projects;
            }

            public IReadOnlyList<Day> Days { get; }
            public IReadOnlyList<TaskResult> Tasks { get; }
            public IReadOnlyList<ProjectResult> Projects { get; }
        }

        /// <summary>
        /// How loaded a day is: what it can hold and what is already committed to it. THE definition, shared
        /// by the Day Plan header and every other place that names a day's shortfall.
        /// </summary>
        public struct DayLoad : IEquatable<DayLoad>
        {
            public DayLoad(DateTime day, double capacityMinutes, double committedMinutes)
            {
                Day = day; CapacityMinutes = capacityMinutes; CommittedMinutes = committedMinutes;
            }

            public DateTime Day { get; }
            public double CapacityMinutes { get; }
            public double CommittedMinutes { get; }
            /// <summary>What is left after the committed work — NEGATIVE when the day is over. The Day Plan's "buffer".</summary>
            public double BufferMinutes { get { return CapacityMinutes - CommittedMinutes; } }
            public double OvercommittedMinutes { get { return Math.Max(0, -BufferMinutes); } }
            public double FreeMinutes { get { return Math.Max(0, BufferMinutes); } }
            public bool IsOvercommitted { get { return OvercommittedMinutes > CapacityEpsilon; } }

            public bool Equals(DayLoad other)
            {
                return Day == other.Day && CapacityMinutes.Equals(other.CapacityMinutes) && CommittedMinutes.Equals(other.CommittedMinutes);
            }
            public override bool Equals(object obj) { return obj is DayLoad && Equals((DayLoad)obj); }
            public override int GetHashCode() { return Day.GetHashCode() ^ CapacityMinutes.GetHashCode() ^ CommittedMinutes.GetHashCode(); }
        }

        /// <summary>The Day Plan header's arithmetic: time absorbed by a habit or event is work the day already pays for.</summary>
        public static DayLoad DayLoadFor(DateTime day, double capacityMinutes, double plannedMinutes, double absorbedMinutes = 0)
        {
            return new DayLoad(day, capacityMinutes, Math.Max(0, plannedMinutes - absorbedMinutes));
        }

        /// <summary>
        /// The same reading from a horizon forecast. The allocator never over-fills a day, so a day's
        /// over-commitment survives only as work PINNED to it that the day could not hold.
        /// </summary>
        public static DayLoad? DayLoadOn(DateTime day, Result forecast)
        {
            Day row = forecast.Days.FirstOrDefault(d => d.Date.Date == day.Date);
            if (row == null) return null;
            var pinnedHere = forecast.Tasks.Where(r => !r.Task.Completed && r.Task.PinnedDay.HasValue
                                                       && r.Task.PinnedDay.Value.Date == row.Date.Date).ToList();
            double demanded = pinnedHere.Sum(r => Estimate(r.Task) ?? 0);
            double absorbed = pinnedHere.SelectMany(r => r.Allocations).Where(a => a.Day.Date == row.Date.Date)
                                        .Sum(a => a.AbsorbedBlockMinutes);
            var pinnedIds = new HashSet<string>(pinnedHere.Select(r => r.Task.Id));
            double otherWork = row.Allocations.Where(a => !pinnedIds.Contains(a.TaskId)).Sum(a => a.WorkMinutes);
            return DayLoadFor(row.Date, row.Capacity.AvailableWorkMinutes, demanded + otherWork, absorbed);
        }

        public enum ChangeKind { Task, Project }

        public static string KindWire(ChangeKind kind) { return kind == ChangeKind.Task ? "task" : "project"; }

        public sealed class DueDateChange
        {
            public DueDateChange(ChangeKind kind, string itemId, DateTime? previousDate, DateTime proposedDate)
            {
                Kind = kind; ItemId = itemId; PreviousDate = previousDate; ProposedDate = proposedDate;
            }

            public ChangeKind Kind { get; }
            public string ItemId { get; }
            public DateTime? PreviousDate { get; }
            public DateTime ProposedDate { get; }
            public string Id { get { return KindWire(Kind) + ":" + ItemId; } }
        }

        public enum ConflictReason { HardDeadline, PostponementLimit, OutsideHorizon, MissingEstimate, MissingOutcome, ProjectEndDate }

        public sealed class PlanConflict
        {
            public PlanConflict(ChangeKind kind, string itemId, ConflictReason reason, DateTime? earliestCompletionDate)
            {
                Kind = kind; ItemId = itemId; Reason = reason; EarliestCompletionDate = earliestCompletionDate;
            }

            public ChangeKind Kind { get; }
            public string ItemId { get; }
            public ConflictReason Reason { get; }
            public DateTime? EarliestCompletionDate { get; }
            /// <summary>The id the change would have, so a typed date keeps one key when the row turns into a change.</summary>
            public string ChangeId { get { return KindWire(Kind) + ":" + ItemId; } }
        }

        /// <summary>Only a proposal: the caller shows the exact changes and gets explicit review before any write.</summary>
        public sealed class WorkPlan
        {
            public WorkPlan(IReadOnlyList<DueDateChange> changes, IReadOnlyList<PlanConflict> conflicts)
            {
                Changes = changes; Conflicts = conflicts;
            }

            public IReadOnlyList<DueDateChange> Changes { get; }
            public IReadOnlyList<PlanConflict> Conflicts { get; }

            /// <summary>A hard deadline is the user's own instruction that the date is fixed — nothing to review.</summary>
            public IReadOnlyList<PlanConflict> ReviewableConflicts
            {
                get { return Conflicts.Where(c => c.Reason != ConflictReason.HardDeadline).ToList(); }
            }

            /// <summary>The plan with the dates the user typed put back (a typed date is an instruction, not a proposal).</summary>
            public WorkPlan ApplyingManualDates(IReadOnlyDictionary<string, DateTime> dates)
            {
                if (dates == null || dates.Count == 0) return this;
                var updated = new List<DueDateChange>();
                foreach (DueDateChange change in Changes)
                {
                    DateTime chosen;
                    if (!dates.TryGetValue(change.Id, out chosen)) { updated.Add(change); continue; }
                    DateTime day = chosen.Date;
                    if (change.PreviousDate.HasValue && change.PreviousDate.Value.Date == day) continue;
                    updated.Add(new DueDateChange(change.Kind, change.ItemId, change.PreviousDate, day));
                }
                return new WorkPlan(updated, Conflicts);
            }

            /// <summary>
            /// A date typed on a row the planner REFUSED turns it into a proposal ("let me reschedule manually
            /// if I want to"). A missing estimate is still refused: without one the task can't be placed.
            /// </summary>
            public WorkPlan AdoptingManualDates(IReadOnlyDictionary<string, DateTime> dates, Func<ChangeKind, string, DateTime?> previousDate)
            {
                if (dates == null || dates.Count == 0) return this;
                var proposed = new HashSet<string>(Changes.Select(c => c.Id));
                var adopted = new List<DueDateChange>();
                var kept = new List<PlanConflict>();
                foreach (PlanConflict conflict in Conflicts)
                {
                    DateTime chosen;
                    if (conflict.Reason == ConflictReason.MissingEstimate || proposed.Contains(conflict.ChangeId)
                        || !dates.TryGetValue(conflict.ChangeId, out chosen))
                    {
                        kept.Add(conflict);
                        continue;
                    }
                    DateTime day = chosen.Date;
                    DateTime? previous = previousDate(conflict.Kind, conflict.ItemId);
                    if (!(previous.HasValue && previous.Value.Date == day))
                        adopted.Add(new DueDateChange(conflict.Kind, conflict.ItemId, previous, day));
                }
                if (adopted.Count == 0 && kept.Count == Conflicts.Count) return this;
                return new WorkPlan(Changes.Concat(adopted).ToList(), kept);
            }
        }

        // MARK: - Allocation

        /// <summary>
        /// Allocates every unfinished task to the days, in priority order (<see cref="Precedes"/>). Hard
        /// deadlines are reserved on their own due day first; a locked day takes only work pinned to it.
        /// <paramref name="unreservedTaskId"/> opts one task out of the hard-deadline reservation (for
        /// choosing that task's date).
        /// </summary>
        public static Result Calculate(IEnumerable<DayInput> days, IEnumerable<ProjectInput> projects, IEnumerable<TaskInput> tasks,
                                       string unreservedTaskId = null)
        {
            List<ProjectInput> uniqueProjects = Unique(projects ?? Enumerable.Empty<ProjectInput>(), p => p.Id);
            List<TaskInput> uniqueTasks = Unique(tasks ?? Enumerable.Empty<TaskInput>(), t => t.Id);
            var projectById = uniqueProjects.ToDictionary(p => p.Id);
            List<DayBudget> budgets = PreparedDays(days ?? Enumerable.Empty<DayInput>());
            List<TaskInput> ordered = uniqueTasks.ToList();
            ordered.Sort((l, r) => Precedes(l, r, projectById) ? -1 : Precedes(r, l, projectById) ? 1 : 0);

            // A completed task linked to a block has already used its share of that block.
            foreach (TaskInput task in ordered.Where(t => t.Completed))
            {
                double? duration = Estimate(task);
                if (!duration.HasValue || !task.BlockAssignment.HasValue) continue;
                BlockAssignment assignment = task.BlockAssignment.Value;
                DayBudget budget = budgets.FirstOrDefault(b => b.Capacity.Day == assignment.Date.Date);
                if (budget != null) budget.Consume(assignment.BlockKey, duration.Value);
            }

            // Hard deadlines are already scheduled — by the user. Reserve them on their own due day first.
            var fixedResults = new Dictionary<string, TaskResult>();
            foreach (TaskInput task in ordered.Where(t => !t.Completed && !t.PinnedDay.HasValue && t.Id != unreservedTaskId))
            {
                double? duration = Estimate(task);
                ProjectInput owner = task.ProjectId != null && projectById.ContainsKey(task.ProjectId) ? projectById[task.ProjectId] : null;
                if (!duration.HasValue || duration.Value <= 0 || AllowsPostponement(EffectiveOutcome(task, owner)) || !task.DueDate.HasValue)
                    continue;
                DayBudget budget = budgets.FirstOrDefault(b => b.Capacity.Day == task.DueDate.Value.Date);
                if (budget == null || !budget.AcceptsUnplannedWork) continue;
                budget.RemainingWork = Math.Max(0, budget.RemainingWork - duration.Value);
                var allocation = new Allocation(task.Id, task.ProjectId, budget.Capacity.Day, duration.Value, 0);
                budget.Allocations.Add(allocation);
                fixedResults[task.Id] = new TaskResult(task, new[] { allocation }, 0);
            }

            var results = new List<TaskResult>();
            foreach (TaskInput task in ordered)
            {
                TaskResult fixedResult;
                if (fixedResults.TryGetValue(task.Id, out fixedResult)) { results.Add(fixedResult); continue; }
                double? duration = Estimate(task);
                if (!duration.HasValue || task.Completed)
                {
                    results.Add(new TaskResult(task, new Allocation[0], 0));
                    continue;
                }
                double remaining = duration.Value;
                var allocations = new List<Allocation>();
                DateTime? earliest = null;
                if (task.NotBefore.HasValue) earliest = task.NotBefore.Value.Date;
                if (task.BlockAssignment.HasValue)
                {
                    DateTime a = task.BlockAssignment.Value.Date.Date;
                    earliest = earliest.HasValue ? (a > earliest.Value ? a : earliest) : a;
                }
                foreach (DayBudget budget in budgets)
                {
                    if (remaining <= CapacityEpsilon) break;
                    DateTime day = budget.Capacity.Day;
                    if (earliest.HasValue && day < earliest.Value) continue;
                    bool pinnedHere = task.PinnedDay.HasValue && task.PinnedDay.Value.Date == day;
                    if (!budget.AcceptsUnplannedWork && !pinnedHere) continue;
                    double absorbed = 0;
                    if (task.BlockAssignment.HasValue && task.BlockAssignment.Value.Date.Date == day)
                        absorbed = budget.Consume(task.BlockAssignment.Value.BlockKey, remaining);
                    double work = Math.Min(remaining - absorbed, budget.RemainingWork);
                    if (!(work + absorbed > 0)) continue;
                    budget.RemainingWork -= work;
                    remaining = Math.Max(0, remaining - absorbed - work);
                    var allocation = new Allocation(task.Id, task.ProjectId, day, work, absorbed);
                    allocations.Add(allocation);
                    budget.Allocations.Add(allocation);
                }
                results.Add(new TaskResult(task, allocations, remaining));
            }

            List<ProjectInput> projectOrder = uniqueProjects.ToList();
            projectOrder.Sort((l, r) => ProjectPrecedes(l, r) ? -1 : ProjectPrecedes(r, l) ? 1 : 0);
            var projectResults = projectOrder.Select(project =>
            {
                var members = results.Where(r => r.Task.ProjectId == project.Id).ToList();
                DateTime? deadline = project.DueDate.HasValue ? project.DueDate.Value.Date : (DateTime?)null;
                double known = members.Sum(r => Estimate(r.Task) ?? 0);
                double completed = members.Where(r => r.Task.Completed).Sum(r => Estimate(r.Task) ?? 0);
                double byDeadline = members.SelectMany(r => r.Allocations)
                                           .Where(a => !deadline.HasValue || a.Day <= deadline.Value).Sum(a => a.TotalMinutes);
                int unknown = members.Count(r => !Estimate(r.Task).HasValue);
                int outstanding = members.Count(r => !r.Task.Completed);
                bool unallocated = members.Any(r => r.UnallocatedMinutes > CapacityEpsilon);
                DateTime? finish = null;
                if (unknown == 0 && !unallocated)
                {
                    var dates = members.Select(r => r.CompletionDate).Where(d => d.HasValue).Select(d => d.Value).ToList();
                    if (dates.Count > 0) finish = dates.Max();
                }
                return new ProjectResult
                {
                    Project = project, EstimatedMinutes = known, CompletedMinutes = completed,
                    AllocatedByDeadlineMinutes = byDeadline, UnknownTaskCount = unknown, OutstandingTaskCount = outstanding,
                    CompletionDate = finish, HasUnallocatedWork = unallocated,
                };
            }).ToList();

            return new Result(budgets.Select(b => new Day(b.Capacity, b.Allocations, !b.AcceptsUnplannedWork)).ToList(),
                              results, projectResults);
        }

        /// <summary>
        /// Turns a forecast into proposed due-date changes plus the rows it refused, and why. Generating it
        /// has no effects.
        /// </summary>
        public static WorkPlan ProposeWorkPlan(Result forecast)
        {
            var changes = new List<DueDateChange>();
            var conflicts = new List<PlanConflict>();
            var projectById = new Dictionary<string, ProjectInput>();
            foreach (ProjectResult p in forecast.Projects) if (!projectById.ContainsKey(p.Id)) projectById[p.Id] = p.Project;
            var lockedDays = new HashSet<DateTime>(forecast.Days.Where(d => d.IsLocked).Select(d => d.Date.Date));

            Action<ChangeKind, string, int, DateTime?, DateTime?, bool, bool, DateTime?> consider =
                (kind, id, outcome, due, finish, unknown, unallocated, ceiling) =>
            {
                if (unknown)
                {
                    conflicts.Add(new PlanConflict(kind, id, ConflictReason.MissingEstimate, finish));
                    return;
                }
                if (!finish.HasValue || unallocated)
                {
                    conflicts.Add(new PlanConflict(kind, id, ConflictReason.OutsideHorizon, finish));
                    return;
                }
                DateTime? normalizedDue = due.HasValue ? due.Value.Date : (DateTime?)null;
                bool isPostponement = normalizedDue.HasValue && finish.Value > normalizedDue.Value;
                // An unrated item is judged first: the fix is to rate it.
                if (isPostponement && outcome <= 0)
                {
                    conflicts.Add(new PlanConflict(kind, id, ConflictReason.MissingOutcome, finish));
                    return;
                }
                // The project's end date is a ceiling on its tasks, and outranks the flexibility bands.
                if (ForecastCeiling.Breaches(finish.Value, ceiling) && normalizedDue != finish)
                {
                    conflicts.Add(new PlanConflict(kind, id, ConflictReason.ProjectEndDate, finish));
                    return;
                }
                // "Hard deadline — can't shift" means it cannot shift, in EITHER direction.
                if (normalizedDue.HasValue && finish.Value != normalizedDue.Value && !AllowsPostponement(outcome))
                {
                    conflicts.Add(new PlanConflict(kind, id, ConflictReason.HardDeadline, finish));
                    return;
                }
                if (normalizedDue.HasValue && isPostponement)
                {
                    int? allowance = PostponementDays(outcome);
                    if (allowance.HasValue && finish.Value > normalizedDue.Value.AddDays(allowance.Value))
                    {
                        conflicts.Add(new PlanConflict(kind, id, ConflictReason.PostponementLimit, finish));
                        return;
                    }
                }
                if (normalizedDue == finish) return;
                // Projects keep an achievable due date; only a later one is proposed for them.
                if (kind == ChangeKind.Project && normalizedDue.HasValue && finish.Value < normalizedDue.Value) return;
                // Never a date on a locked day: the only work finishing there is work already planned there.
                if (lockedDays.Contains(finish.Value.Date)) return;
                changes.Add(new DueDateChange(kind, id, due, finish.Value));
            };

            foreach (TaskResult task in forecast.Tasks.Where(t => !t.Task.Completed))
            {
                ProjectInput owner = task.Task.ProjectId != null && projectById.ContainsKey(task.Task.ProjectId)
                    ? projectById[task.Task.ProjectId] : null;
                consider(ChangeKind.Task, task.Task.Id, EffectiveOutcome(task.Task, owner), task.Task.DueDate,
                         task.CompletionDate, !task.HasEstimate, task.UnallocatedMinutes > CapacityEpsilon,
                         ForecastCeiling.ForTask(task.Task, owner));
            }
            foreach (ProjectResult project in forecast.Projects.Where(p => p.CompletedMinutes < p.EstimatedMinutes || p.UnknownTaskCount > 0))
            {
                consider(ChangeKind.Project, project.Id, project.Project.Outcome, project.Project.DueDate,
                         project.CompletionDate, project.UnknownTaskCount > 0, project.HasUnallocatedWork, null);
            }
            return new WorkPlan(changes, conflicts);
        }

        // MARK: - Rating rules

        /// <summary>
        /// The deadline flexibility a task is planned under: its project's rating can only TIGHTEN it. An
        /// unrated project (outcome 0 — nearly every real project) contributes nothing.
        /// </summary>
        public static int EffectiveOutcome(TaskInput task, ProjectInput project)
        {
            if (project == null || !project.HasRating) return task.Outcome;
            return Math.Max(task.Outcome, project.Outcome);
        }

        /// <summary>Whether this task's rating came from its project, so the screen can say so.</summary>
        public static bool InheritsProjectOutcome(TaskInput task, ProjectInput project)
        {
            return project != null && project.HasRating && project.Outcome > task.Outcome;
        }

        public static bool AllowsPostponement(int outcome)
        {
            int? days = PostponementDays(outcome);
            return !(days.HasValue && days.Value == 0);
        }

        /// <summary>
        /// How far a date rated this way may be pushed BACK — the /5 wording, literally: 5 hard deadline → 0
        /// days, 4 → 7, 3 → 21, 2 → 90, 1 → no finite limit.
        /// </summary>
        public static int? PostponementDays(int outcome)
        {
            if (outcome >= 10) return 0;
            if (outcome >= 8) return 7;
            if (outcome >= 6) return 21;
            if (outcome >= 4) return 90;
            return null;
        }

        /// <summary>The one question every path that names a date must ask: is it past the owning project's end?</summary>
        public static class ForecastCeiling
        {
            public static DateTime? ForTask(TaskInput task, ProjectInput project)
            {
                if (task.ProjectId == null || project == null || !project.DueDate.HasValue) return null;
                return project.DueDate.Value.Date;
            }

            /// <summary>A null ceiling is the ABSENCE of a ceiling, never a reason to refuse.</summary>
            public static bool Allows(DateTime day, DateTime? ceiling)
            {
                return !ceiling.HasValue || day <= ceiling.Value;
            }

            public static bool Breaches(DateTime day, DateTime? ceiling)
            {
                return !Allows(day, ceiling);
            }
        }

        /// <summary>
        /// Whether <paramref name="day"/> falls inside the settled stretch at the start of the horizon (today
        /// and tomorrow). <paramref name="locksPlannedDays"/> is the sheet's "Don't change tasks scheduled for
        /// today or tomorrow" checkbox.
        /// </summary>
        public static bool IsPlanLocked(DateTime day, DateTime horizonStart, bool locksPlannedDays)
        {
            if (!locksPlannedDays) return false;
            int offset = (int)Math.Round((day.Date - horizonStart.Date).TotalDays);
            return offset >= 0 && offset < LockedPlanDays;
        }

        public static double? Estimate(TaskInput task)
        {
            double? value = task.EstimatedMinutes;
            if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value.Value <= 0) return null;
            return value;
        }

        // MARK: - Capacity

        private sealed class BlockBudget
        {
            public BlockKind Kind;
            public string Key;
            public double Minutes;
        }

        private sealed class DayBudget
        {
            public Capacity Capacity;
            public double RemainingWork;
            public List<BlockBudget> Blocks = new List<BlockBudget>();
            public bool AcceptsUnplannedWork;
            public List<Allocation> Allocations = new List<Allocation>();

            public double Consume(string key, double requested)
            {
                double consumed = 0;
                foreach (BlockBudget block in Blocks.Where(b => b.Key != null && b.Key == key))
                {
                    double amount = Math.Min(block.Minutes, requested - consumed);
                    block.Minutes -= amount;
                    consumed += amount;
                    if (consumed >= requested) break;
                }
                return consumed;
            }
        }

        private static List<DayBudget> PreparedDays(IEnumerable<DayInput> days)
        {
            // Duplicate snapshots for one date merge rather than granting another 24 hours.
            var grouped = days.GroupBy(d => d.Date.Date).OrderBy(g => g.Key);
            var result = new List<DayBudget>();
            foreach (var group in grouped)
            {
                DateTime day = group.Key;
                DateTime end = day.AddDays(1);
                var inputs = group.ToList();
                DateTime from = inputs.Where(i => i.AvailableFrom.HasValue).Select(i => i.AvailableFrom.Value)
                                      .DefaultIfEmpty(day).Max();
                var bounds = new Interval(Min(end, Max(day, from)), end);
                CapacitySettings settings = inputs[0].Settings != null && inputs[0].Settings.IsValid ? inputs[0].Settings : new CapacitySettings();
                var sleep = SleepIntervals(day, settings).Select(i => Clipped(i, bounds)).Where(i => i.HasValue).Select(i => i.Value).ToList();
                List<Interval> occupied = Union(sleep);
                var budgets = new List<BlockBudget>();
                // Events before habits, each charged only for time nothing ahead of it already holds.
                var placed = Unique(inputs.SelectMany(i => i.Blocks), b => b.Id)
                    .OrderBy(b => b.Kind == BlockKind.Meeting ? 0 : 1)
                    .ThenBy(b => b.Interval.Start)
                    .ThenBy(b => b.Id, StringComparer.Ordinal)
                    .ToList();
                foreach (PlacedBlock block in placed)
                {
                    Interval? interval = Clipped(block.Interval, bounds);
                    if (!interval.HasValue) continue;
                    var overlap = occupied.Select(o => Clipped(o, interval.Value)).Where(o => o.HasValue).Select(o => o.Value).ToList();
                    double minutes = Math.Max(0, Measure(new[] { interval.Value }) - Measure(Union(overlap)));
                    budgets.Add(new BlockBudget { Kind = block.Kind, Key = block.BlockKey, Minutes = minutes });
                    occupied = Union(occupied.Concat(new[] { interval.Value }));
                }
                double usableGaps = Measure(Gaps(bounds, occupied).Where(g => g.Minutes >= MinimumUsableWorkMinutes));
                var capacity = new Capacity
                {
                    Day = day,
                    MeetingMinutes = budgets.Where(b => b.Kind == BlockKind.Meeting).Sum(b => b.Minutes),
                    HabitMinutes = budgets.Where(b => b.Kind == BlockKind.Habit).Sum(b => b.Minutes),
                    SleepMinutes = Measure(sleep),
                    SelfCareMinutes = settings.SelfCareMinutes,
                    TotalMinutes = Math.Max(0, (end - bounds.Start).TotalMinutes),
                    UsableWorkMinutes = Math.Max(0, usableGaps - settings.SelfCareMinutes),
                };
                result.Add(new DayBudget
                {
                    Capacity = capacity,
                    RemainingWork = capacity.AvailableWorkMinutes,
                    Blocks = budgets,
                    AcceptsUnplannedWork = !inputs.Any(i => i.IsLocked),
                });
            }
            return result;
        }

        /// <summary>Sleep on a day: one interval when sleep doesn't cross midnight, else its two ends.</summary>
        public static List<Interval> SleepIntervals(DateTime date, CapacitySettings settings)
        {
            DateTime day = date.Date;
            if (settings == null || !settings.IsValid) return new List<Interval>();
            DateTime start = day.AddMinutes(settings.SleepStartMinute), finish = day.AddMinutes(settings.SleepEndMinute);
            if (settings.SleepStartMinute < settings.SleepEndMinute) return new List<Interval> { new Interval(start, finish) };
            return new List<Interval> { new Interval(day, finish), new Interval(start, day.AddDays(1)) };
        }

        /// <summary>What is left of <paramref name="bounds"/> once every occupied interval is removed.</summary>
        public static List<Interval> Gaps(Interval bounds, IEnumerable<Interval> occupied)
        {
            var result = new List<Interval>();
            DateTime cursor = bounds.Start;
            foreach (Interval i in Union(occupied).Select(o => Clipped(o, bounds)).Where(o => o.HasValue).Select(o => o.Value).OrderBy(o => o.Start))
            {
                if (i.Start > cursor) result.Add(new Interval(cursor, i.Start));
                cursor = Max(cursor, i.End);
            }
            if (cursor < bounds.End) result.Add(new Interval(cursor, bounds.End));
            return result;
        }

        private static List<Interval> Union(IEnumerable<Interval> intervals)
        {
            var result = new List<Interval>();
            foreach (Interval i in intervals.Where(i => i.End > i.Start).OrderBy(i => i.Start))
            {
                if (result.Count > 0 && i.Start <= result[result.Count - 1].End)
                {
                    Interval previous = result[result.Count - 1];
                    result[result.Count - 1] = new Interval(previous.Start, Max(previous.End, i.End));
                }
                else result.Add(i);
            }
            return result;
        }

        private static double Measure(IEnumerable<Interval> intervals) { return intervals.Sum(i => i.Minutes); }

        private static Interval? Clipped(Interval interval, Interval bounds)
        {
            DateTime start = Max(interval.Start, bounds.Start), end = Min(interval.End, bounds.End);
            return end > start ? new Interval(start, end) : (Interval?)null;
        }

        private static DateTime Min(DateTime a, DateTime b) { return a <= b ? a : b; }
        private static DateTime Max(DateTime a, DateTime b) { return a >= b ? a : b; }

        private static List<T> Unique<T>(IEnumerable<T> values, Func<T, string> id)
        {
            var seen = new HashSet<string>();
            return values.Where(v => seen.Add(id(v))).ToList();
        }

        // MARK: - Ordering

        /// <summary>
        /// Which project's work is done first: deadline before rating (a rating is a claim about postponement,
        /// not urgency); the rating only breaks ties between projects due the same day.
        /// </summary>
        private static bool ProjectPrecedes(ProjectInput left, ProjectInput right)
        {
            DateTime ld = left.DueDate.HasValue ? left.DueDate.Value.Date : DateTime.MaxValue;
            DateTime rd = right.DueDate.HasValue ? right.DueDate.Value.Date : DateTime.MaxValue;
            if (ld != rd) return ld < rd;
            if (left.Outcome != right.Outcome) return left.Outcome > right.Outcome;
            return string.CompareOrdinal(left.Id, right.Id) < 0;
        }

        private static bool Precedes(TaskInput left, TaskInput right, IDictionary<string, ProjectInput> projects)
        {
            // Unprojected tasks act as individual projects using their own outcome and deadline.
            ProjectInput lp = left.ProjectId != null && projects.ContainsKey(left.ProjectId) ? projects[left.ProjectId]
                : new ProjectInput("unprojected", left.Outcome, left.DueDate);
            ProjectInput rp = right.ProjectId != null && projects.ContainsKey(right.ProjectId) ? projects[right.ProjectId]
                : new ProjectInput("unprojected", right.Outcome, right.DueDate);
            if (lp.Id != rp.Id) return ProjectPrecedes(lp, rp);
            DateTime ld = left.DueDate.HasValue ? left.DueDate.Value.Date : DateTime.MaxValue;
            DateTime rd = right.DueDate.HasValue ? right.DueDate.Value.Date : DateTime.MaxValue;
            if (ld != rd) return ld < rd;
            if (left.Outcome != right.Outcome) return left.Outcome > right.Outcome;
            if (left.Perspiration != right.Perspiration) return left.Perspiration < right.Perspiration;
            return string.CompareOrdinal(left.Id, right.Id) < 0;
        }
    }
}
