using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using FocusBear.Core.Forecast;
using FocusBear.Core.ToDo;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using F = FocusBear.Core.Forecast.LocalTimeForecast;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// The reprioritise forecast — the Mac's <c>LocalForecastStore</c>, trimmed to what the reschedule plan,
    /// the To-do list's prompt and the Day Plan's "Fix" need. It prices the next 90 days from the Day Plan's
    /// own schedule (routine habits + synced meetings, per day), plans the unfinished project backlog into
    /// them with FocusBear.Core's <see cref="LocalTimeForecast"/>, and proposes due-date changes. Nothing is
    /// written until the user applies selected changes, and each write re-reads the row first.
    /// </summary>
    public sealed class ReprioritiseForecast
    {
        public static readonly ReprioritiseForecast Shared = new ReprioritiseForecast();

        public const int HorizonDays = 90;

        private ReprioritiseForecast() { }

        /// <summary>Where the writes go; a fake in tests.</summary>
        internal Func<IReprioritiseApi> ApiFactory = () => new ReprioritiseApi();

        public bool IsLoading { get; private set; }
        public bool IsApplying { get; private set; }
        public string Error { get; private set; }
        /// <summary>A row edit that did not save (outcome, duration), shown above the list; cleared by the next edit.</summary>
        public string EditMessage { get; private set; }
        public F.Result Result { get; private set; }
        public DateTime? ComputedAt { get; private set; }
        public ToDoCatalogue.Snapshot Catalogue { get; private set; }
        public List<F.TaskInput> TaskInputs { get; private set; } = new List<F.TaskInput>();
        public List<F.ProjectInput> ProjectInputs { get; private set; } = new List<F.ProjectInput>();
        private readonly Dictionary<string, DateTime> _manualDates = new Dictionary<string, DateTime>();
        /// <summary>The priced days of the last calculation, so a row edit can re-plan without re-reading anything.</summary>
        private List<F.DayInput> _days;

        public event Action Changed;

        /// <summary>No forecast yet, or one older than the Mac's fifteen-minute reuse window.</summary>
        public bool IsStale => !IsLoading && (!ComputedAt.HasValue || (DateTime.Now - ComputedAt.Value).TotalMinutes > 15);

        private static string Account => AppSettings.GetInstance.userAccount?.userID ?? "";

        /// <summary>Whose work this plan is: the signed-in user, or the account the catalogue was read for.</summary>
        private string MeId => !string.IsNullOrEmpty(Account) ? Account : Catalogue?.Account;

        /// <summary>"Don't change tasks scheduled for today or tomorrow" — defaults to on, remembered on this computer.</summary>
        public bool LocksPlannedDays
        {
            get { return RegistryManager.LoadReprioritiseLocksPlannedDays(); }
            set { RegistryManager.SaveReprioritiseLocksPlannedDays(value); Relock(); Changed?.Invoke(); }
        }

        // MARK: - Loading

        /// <summary>Recalculate from a fresh catalogue. False when the catalogue could not be read in full.</summary>
        public async Task<bool> RefreshAsync(bool force = true)
        {
            if (IsApplying || IsLoading) return false;   // one at a time: building the days now yields to the UI
            IsLoading = true;
            Error = null;
            Changed?.Invoke();
            try
            {
                ToDoCatalogue.Snapshot catalogue = await ToDoCatalogue.GetAsync(force);
                if (catalogue == null)
                {
                    Error = ForecastText.Get("loadFailed");
                    return false;
                }
                Catalogue = catalogue;
                // The days are priced from the Day Plan's own schedule, meetings included, so the synced calendar
                // must cover the whole horizon (the Day Plan itself only keeps 45 days fresh).
                await DayPlanCalendarManager.Shared.EnsureWindowAsync(Math.Min(HorizonDays, DayPlanCalendarManager.MaxWindowDays));
                DateTime now = DateTime.Now, today = now.Date;
                var clock = System.Diagnostics.Stopwatch.StartNew();
                var days = await BuildDaysAsync(now);
                long buildMs = clock.ElapsedMilliseconds;
                var planned = DayPlanTaskStore.Shared.AllPlanned()
                    .Where(p => !p.Value.Done && !string.IsNullOrEmpty(p.Value.SourceTaskId))
                    .Select(p => new ForecastSnapshot.PlannedCopy(p.Value.SourceTaskId, p.Key, p.Value.DurationMinutes, p.Value.BlockKey));
                ProjectInputs = ForecastSnapshot.Projects(catalogue.Projects);
                // Work assigned to somebody else is theirs: it takes none of this user's time and gets no new date.
                TaskInputs = ForecastSnapshot.ProjectTasks(catalogue.Tasks, catalogue.Projects, planned, today, MeId);
                // Pure Core maths over plain inputs: off the UI thread so the screen stays responsive.
                var projectInputs = ProjectInputs;
                var taskInputs = TaskInputs;
                Result = await System.Threading.Tasks.Task.Run(() => F.Calculate(days, projectInputs, taskInputs));
                _days = days;
                EditMessage = null;
                ComputedAt = now;
                LogWriter.LogWrite("ReprioritiseForecast: " + days.Count + " days, " + TaskInputs.Count + " project tasks, "
                                   + ProjectInputs.Count + " projects (days " + buildMs + " ms, plan " + (clock.ElapsedMilliseconds - buildMs) + " ms)");
                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ReprioritiseForecast.RefreshAsync - " + ex);
                Error = ForecastText.Get("loadFailed");
                return false;
            }
            finally
            {
                IsLoading = false;
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// Each day's placed blocks, exactly as the Day Plan lays them out: meetings, and habits that got a
        /// slot. Deep-work fillers are the day's work time and are not charged.
        /// </summary>
        private async Task<List<F.DayInput>> BuildDaysAsync(DateTime now)
        {
            var result = new List<F.DayInput>();
            DateTime start = now.Date;
            bool locks = LocksPlannedDays;
            for (int i = 0; i < HorizonDays; i++)
            {
                DateTime date = start.AddDays(i);
                List<ScheduleBlock> blocks;
                try { blocks = DayPlanDataSource.BuildBlocks(date); }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("ReprioritiseForecast.BuildDays " + FocusBear.Core.Schedule.DayPlanDayKey.For(date) + " - " + ex.Message);
                    blocks = new List<ScheduleBlock>();
                }
                result.Add(DayInputFrom(date, blocks, i == 0 ? now : (DateTime?)null, F.IsPlanLocked(date, start, locks)));
                // Laying out 90 days (with 90 days of meetings) is too long for one UI-thread block: let input
                // and rendering in between every few days.
                if (i % 5 == 4) await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
            }
            return result;
        }

        /// <summary>The Day Plan's blocks for a day as forecast input (Mac DayInput.fromDayPlan).</summary>
        public static F.DayInput DayInputFrom(DateTime date, IEnumerable<ScheduleBlock> blocks, DateTime? availableFrom, bool isLocked)
        {
            var placed = new List<F.PlacedBlock>();
            foreach (ScheduleBlock b in blocks ?? Enumerable.Empty<ScheduleBlock>())
            {
                if (b.Kind == ScheduleBlockKind.Meeting && !b.IsDismissed)
                {
                    placed.Add(F.PlacedBlock.Meeting((b.EventID ?? b.Title) + ":" + b.Start.Ticks, new F.Interval(b.Start, b.End),
                                                     b.EventID == null ? null : FocusBear.Core.Schedule.DayPlanBlockKey.Event(b.EventID)));
                }
                else if (b.Kind == ScheduleBlockKind.Habit && !b.DidOverflow && !b.IsEarlier)
                {
                    placed.Add(F.PlacedBlock.Habit((b.HabitId ?? b.Title) + ":" + b.Start.Ticks,
                                                   FocusBear.Core.Schedule.DayPlanBlockKey.Habit(b.Title), new F.Interval(b.Start, b.End)));
                }
            }
            return new F.DayInput(date.Date, placed, new F.CapacitySettings(), availableFrom, isLocked);
        }

        /// <summary>Locking changes what the allocator may do, so recalculate from a (cached) catalogue.</summary>
        private void Relock()
        {
            if (Result == null) return;
            _ = RefreshAsync(false);
        }

        // MARK: - The plan

        /// <summary>The forecast's proposal with the user's typed dates put back (they survive every recalculation).</summary>
        public F.WorkPlan Plan
        {
            get
            {
                if (Result == null) return null;
                return F.ProposeWorkPlan(Result)
                    .ApplyingManualDates(_manualDates)
                    .AdoptingManualDates(_manualDates, (kind, id) => kind == F.ChangeKind.Task ? TaskDueDate(id) : ProjectDueDate(id));
            }
        }

        /// <summary>The plan's own proposal for a change, before any date the user typed over it.</summary>
        public F.DueDateChange ProposedChange(string changeId)
        {
            if (Result == null) return null;
            return F.ProposeWorkPlan(Result).Changes.FirstOrDefault(c => c.Id == changeId);
        }

        public DateTime? ManualDate(string changeId)
        {
            DateTime d;
            return _manualDates.TryGetValue(changeId, out d) ? d : (DateTime?)null;
        }

        public void SetManualDate(string changeId, DateTime? date)
        {
            if (IsApplying || IsLoading) return;
            if (date.HasValue) _manualDates[changeId] = date.Value.Date;
            else _manualDates.Remove(changeId);
            Changed?.Invoke();
        }

        public ToDoTask Task(string id) => Catalogue?.Tasks.FirstOrDefault(t => t.Id == id);
        public Project ProjectById(string id) => id == null ? null : Catalogue?.Projects.FirstOrDefault(p => p.Id == id);

        public DateTime? TaskDueDate(string id) => ForecastSnapshot.ParseDay(Task(id)?.DueDate);
        public DateTime? ProjectDueDate(string id) => ForecastSnapshot.ParseDay(ProjectById(id)?.DueDate);

        public string TaskProjectId(string id) => TaskInputs.FirstOrDefault(t => t.Id == id)?.ProjectId;

        public string ProjectName(string id)
        {
            return ProjectById(id)?.Name ?? ForecastText.Get("unprojected");
        }

        public string Name(F.ChangeKind kind, string id)
        {
            return kind == F.ChangeKind.Project ? ProjectName(id) : (Task(id)?.Title ?? id);
        }

        public double? EstimateMinutes(string taskId) => TaskInputs.FirstOrDefault(t => t.Id == taskId)?.EstimatedMinutes;

        /// <summary>A task row can be written when it is in the catalogue; a project deadline when it's a native project we may edit.</summary>
        public bool CanApply(F.DueDateChange change)
        {
            if (change.Kind == F.ChangeKind.Task) return Task(change.ItemId) != null;
            Project project = ProjectById(change.ItemId);
            if (project == null || project.IsSynced) return false;
            if (project.OwnerId == Account) return true;
            return ProjectPermissions.CanViewSettings(ProjectPermissions.ViewerRole(project, Account));
        }

        public string ConflictReason(F.PlanConflict conflict)
        {
            F.TaskInput task = conflict.Kind == F.ChangeKind.Task ? TaskInputs.FirstOrDefault(t => t.Id == conflict.ItemId) : null;
            F.ProjectInput owner = task?.ProjectId == null ? null : ProjectInputs.FirstOrDefault(p => p.Id == task.ProjectId);
            int? outcome = task != null ? F.EffectiveOutcome(task, owner)
                : ProjectInputs.FirstOrDefault(p => p.Id == conflict.ItemId)?.Outcome;
            return ForecastText.Conflict(conflict.Reason, conflict.EarliestCompletionDate, outcome,
                                         owner == null ? null : ProjectName(owner.Id), owner?.DueDate);
        }

        // MARK: - Row edits (the Mac's setTaskOutcome / setDuration)

        /// <summary>The outcome the plan used for this task (its own, or the unrated default) on the /10 wire scale.</summary>
        public int? Outcome(string taskId) => TaskInputs.FirstOrDefault(t => t.Id == taskId)?.Outcome ?? Task(taskId)?.Outcome;

        /// <summary>The project's rating these tasks are planned under when it is stricter than their own; null otherwise.</summary>
        public int? InheritedOutcome(string projectId, IEnumerable<string> taskIds)
        {
            F.ProjectInput project = ProjectInputs.FirstOrDefault(p => p.Id == projectId);
            if (project == null) return null;
            bool any = (taskIds ?? Enumerable.Empty<string>())
                .Select(id => TaskInputs.FirstOrDefault(t => t.Id == id))
                .Any(t => t != null && t.ProjectId == projectId && F.InheritsProjectOutcome(t, project));
            return any ? project.Outcome : (int?)null;
        }

        /// <summary>This task's estimate in whole minutes, or null when it has none.</summary>
        public int? DurationMinutes(string taskId)
        {
            double? minutes = EstimateMinutes(taskId);
            if (minutes.HasValue && minutes.Value > 0 && !double.IsInfinity(minutes.Value) && minutes.Value < int.MaxValue)
                return (int)Math.Ceiling(minutes.Value);
            int? seconds = Task(taskId)?.Duration;
            return seconds.HasValue && seconds.Value > 0 ? seconds.Value / 60 : (int?)null;
        }

        public bool CanEditRows => !IsApplying && !IsLoading && _days != null && Result != null;

        /// <summary>
        /// A row's outcome (deadline flexibility), on the /10 wire scale. Applied to this snapshot and re-planned
        /// at once, so pinning a task to a hard deadline takes it off the list and the rest re-flow around the
        /// days it frees; then written (re-read, then <c>PUT /to-do { id, title, eisenhower_quadrant, outcome }</c>).
        /// A write that does not stick puts the old rating back and says so.
        /// </summary>
        public async Task<bool> SetTaskOutcomeAsync(string taskId, int wireOutcome)
        {
            ToDoTask task = Task(taskId);
            if (!CanEditRows || task == null) return false;
            string account = Account;
            int clamped = ToDoScoring.ClampLevel(wireOutcome);
            int? previous = task.Outcome;
            if (previous == clamped) return true;
            EditMessage = null;
            LogWriter.LogWrite("[user-action] reprioritise: task outcome " + (previous.HasValue ? previous.Value.ToString(CultureInfo.InvariantCulture) : "none")
                               + " -> " + clamped.ToString(CultureInfo.InvariantCulture));
            await PatchAndReplanAsync(taskId, t => t.Outcome = clamped, i => i.With(outcome: clamped));
            bool saved;
            try { saved = await ReprioritiseWrites.WriteOutcomeAsync(ApiFactory(), taskId, clamped); }
            catch (Exception ex) { LogWriter.LogWrite("ReprioritiseForecast.SetTaskOutcomeAsync - " + ex.Message); saved = false; }
            if (Account != account) return false;
            if (!saved)
            {
                LogWriter.LogWrite("ReprioritiseForecast: outcome write did not stick");
                EditMessage = ForecastText.Get("taskOutcomeNotSavedWindows");
                await PatchAndReplanAsync(taskId, t => t.Outcome = previous, i => i.With(outcome: previous ?? F.TaskInput.UnratedOutcome));
                return false;
            }
            ToDoCatalogue.Invalidate();
            return true;
        }

        /// <summary>
        /// A row's estimate, in minutes. Written first (<c>PUT /to-do { …, duration }</c>, in seconds), then applied
        /// to this snapshot and to the task's Day Plan copies, and the plan re-flows.
        /// </summary>
        public async Task<bool> SetDurationAsync(string taskId, int minutes)
        {
            ToDoTask task = Task(taskId);
            if (!CanEditRows || task == null || minutes <= 0 || minutes > int.MaxValue / 60) return false;
            if (DurationMinutes(taskId) == minutes && task.Duration == minutes * 60) return true;
            string account = Account;
            EditMessage = null;
            int? previousSeconds = task.Duration;
            double? previousEstimate = TaskInputs.FirstOrDefault(t => t.Id == taskId)?.EstimatedMinutes;
            // Show the new estimate at once (and re-flow around it), then write it, as the outcome edit does.
            await PatchAndReplanAsync(taskId, t => t.Duration = minutes * 60, i => i.With(estimatedMinutes: minutes));
            bool saved;
            try { saved = await ReprioritiseWrites.WriteDurationAsync(ApiFactory(), taskId, minutes); }
            catch (Exception ex) { LogWriter.LogWrite("ReprioritiseForecast.SetDurationAsync - " + ex.Message); saved = false; }
            if (Account != account) return false;
            if (!saved)
            {
                LogWriter.LogWrite("ReprioritiseForecast: duration write did not stick");
                EditMessage = ForecastText.Get("durationNotSaved");
                await PatchAndReplanAsync(taskId, t => t.Duration = previousSeconds, i => previousEstimate.HasValue
                    ? i.With(estimatedMinutes: previousEstimate)
                    : new F.TaskInput(i.Id, i.ProjectId, null, i.Outcome, i.Perspiration, i.DueDate, i.NotBefore, i.BlockAssignment, i.Completed));
                return false;
            }
            LogWriter.LogWrite("[user-action] reprioritise: task duration set to " + minutes.ToString(CultureInfo.InvariantCulture) + "m");
            foreach (var placement in DayPlanTaskStore.Shared.AllPlanned().Where(p => p.Value.SourceTaskId == taskId && p.Value.DurationMinutes != minutes).ToList())
            {
                placement.Value.DurationMinutes = minutes;
                DayPlanTaskStore.Shared.Update(placement.Value, placement.Key, alreadyOnServer: true);
            }
            ToDoCatalogue.Invalidate();
            return true;
        }

        /// <summary>Patch one task in the snapshot and recalculate from it — the same inputs a refresh uses, minus the download.</summary>
        private async Task PatchAndReplanAsync(string taskId, Action<ToDoTask> editTask, Func<F.TaskInput, F.TaskInput> editInput)
        {
            ToDoTask task = Task(taskId);
            if (task != null) editTask(task);
            int index = TaskInputs.FindIndex(t => t.Id == taskId);
            if (index >= 0) TaskInputs[index] = editInput(TaskInputs[index]);
            var days = _days;
            var projects = ProjectInputs;
            var inputs = TaskInputs.ToList();
            if (days != null)
                Result = await System.Threading.Tasks.Task.Run(() => F.Calculate(days, projects, inputs));
            Changed?.Invoke();
        }

        // MARK: - Prompt + day focus

        public int OverdueCount => Catalogue == null ? 0 : ForecastSnapshot.OverdueCount(Catalogue.Tasks, DateTime.Now, MeId);

        /// <summary>Whether to offer the reschedule plan right now, and why; null = say nothing.</summary>
        public ReprioritiseSuggestion.Reason? Suggestion
        {
            get
            {
                if (Result == null) return null;
                return ReprioritiseSuggestion.For(OverdueCount, ReprioritiseSuggestion.WorstDayOvercommitMinutes(Result, DateTime.Now),
                                                  RegistryManager.LoadReprioritiseDismissedAt(Account), DateTime.Now);
            }
        }

        public void DismissSuggestion()
        {
            RegistryManager.SaveReprioritiseDismissedAt(Account, DateTime.Now);
            Changed?.Invoke();
        }

        public DateTime? PlannedDay(string taskId) => DayPlanTaskStore.Shared.PlannedCopy(taskId)?.Key;

        /// <summary>The proposals that take work off a focused day (opened from that day's "Fix").</summary>
        public List<F.DueDateChange> ChangesMovingWorkOff(DateTime day)
        {
            F.WorkPlan plan = Plan;
            return plan == null ? new List<F.DueDateChange>() : ForecastDayFocus.ChangesMovingWorkOff(day, plan.Changes, PlannedDay);
        }

        public ForecastDayFocus.Relief ReliefFor(ForecastDayFocus.Target target)
        {
            var offered = ChangesMovingWorkOff(target.Day);
            var offeredIds = new HashSet<string>(offered.Select(c => c.ItemId));
            var immovable = DayPlanTaskStore.Shared.Tasks(target.Day)
                .Where(t => !t.Done && !string.IsNullOrEmpty(t.SourceTaskId) && !offeredIds.Contains(t.SourceTaskId) && IsPinnedByRating(t.SourceTaskId))
                .Select(t => (double)t.DurationMinutes);
            return ForecastDayFocus.ReliefFor(offered, target,
                id => DayPlanTaskStore.Shared.PlannedCopy(id)?.Value.DurationMinutes ?? 0, immovable);
        }

        private bool IsPinnedByRating(string taskId)
        {
            F.TaskInput task = TaskInputs.FirstOrDefault(t => t.Id == taskId);
            if (task == null) return false;
            F.ProjectInput owner = task.ProjectId == null ? null : ProjectInputs.FirstOrDefault(p => p.Id == task.ProjectId);
            return !F.AllowsPostponement(F.EffectiveOutcome(task, owner));
        }

        // MARK: - Applying

        /// <summary>
        /// Writes the selected changes, one at a time, each only after re-reading its row and finding it still
        /// the one the user reviewed (<see cref="ReprioritiseWrites"/>). A task put on a day of the Day Plan
        /// follows its new date. Returns how many were saved and whether all were.
        /// </summary>
        public async Task<Tuple<int, bool>> ApplyAsync(IEnumerable<F.DueDateChange> selected)
        {
            var changes = (selected ?? Enumerable.Empty<F.DueDateChange>()).Where(CanApply).ToList();
            if (changes.Count == 0 || IsApplying) return Tuple.Create(0, true);
            string account = Account;
            IsApplying = true;
            Changed?.Invoke();
            var lockedDays = Result?.Days.Where(d => d.IsLocked).Select(d => d.Date).ToList() ?? new List<DateTime>();
            Tuple<int, bool> outcome;
            try
            {
                outcome = await ReprioritiseWrites.ApplyDueDatesAsync(ApiFactory(), changes, new ReprioritiseWrites.DueDateContext
                {
                    PreviousTask = Task,
                    PreviousProject = ProjectById,
                    AccountUnchanged = () => Account == account,
                    TaskSaved = change => Reposition(change, lockedDays),
                    Log = LogWriter.LogWrite,
                });
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ReprioritiseForecast.ApplyAsync - " + ex.Message);
                outcome = Tuple.Create(0, false);
            }
            finally
            {
                IsApplying = false;
            }
            _manualDates.Clear();
            ToDoCatalogue.Invalidate();
            await RefreshAsync(true);
            return outcome;
        }

        /// <summary>A task on a day of the Day Plan follows its new date (Core DayPlanRepositioning decides).</summary>
        private void Reposition(F.DueDateChange change, List<DateTime> lockedDays)
        {
            var placement = DayPlanTaskStore.Shared.PlannedCopy(change.ItemId);
            if (!placement.HasValue) return;
            var move = DayPlanRepositioning.For(placement.Value.Value.Id, placement.Value.Key, change.ProposedDate, DateTime.Today, lockedDays);
            if (!move.HasValue) return;
            DayPlanTaskStore.Shared.MoveTaskToDay(move.Value.TaskId, move.Value.From, move.Value.To);
        }
    }
}
