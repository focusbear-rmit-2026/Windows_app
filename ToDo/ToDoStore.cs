using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using FocusBear.Core.ToDo;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Focus_Bear.ToDoScreens
{
    /// <summary>The to-do calls the screen makes, so the store can be driven by a fake in tests.</summary>
    internal interface IToDoApi
    {
        Task<ToDoPage> GetPage(int page, ToDoStatus? status, ToDoSortMode sortMode, string projectId);
        Task<IList<ToDoTask>> Search(string title);
        Task<ApiResponse> Upsert(IDictionary<string, object> body);
        Task<bool> Delete(string id);
        Task<IList<Project>> GetProjects();
        Task<ApiResponse> CreateProject(string name);
    }

    internal sealed class ToDoApi : IToDoApi
    {
        private static FocusBearAPIClient Client() => new FocusBearAPIClient();
        public Task<ToDoPage> GetPage(int page, ToDoStatus? status, ToDoSortMode sortMode, string projectId) => Client().GetToDoPage(page, status, sortMode, projectId);
        public Task<IList<ToDoTask>> Search(string title) => Client().SearchToDos(title);
        public Task<ApiResponse> Upsert(IDictionary<string, object> body) => Client().UpsertToDo(body);
        public Task<bool> Delete(string id) => Client().DeleteToDo(id);
        public Task<IList<Project>> GetProjects() => Client().GetAllActiveToDoProjects();
        public Task<ApiResponse> CreateProject(string name) => Client().CreateToDoProject(name);
    }

    /// <summary>Paging state of one status bucket (web <c>todoStatusPagination</c>).</summary>
    internal sealed class ToDoStatusPaging
    {
        public int Page = 1;
        public bool HasNextPage;
        public int ItemCount;

        /// <summary>Rows the server has sent for this bucket, unreadable ones included.</summary>
        public int ReceivedCount;

        /// <summary>A task moving in or out locally is one more (or fewer) row on the server AND in hand.</summary>
        public void AdjustForLocalChange(int delta)
        {
            ItemCount = Math.Max(0, ItemCount + delta);
            ReceivedCount = Math.Max(0, ReceivedCount + delta);
        }
    }

    internal enum ToDoEmptyState { None, Loading, FirstTask, NoSearchMatch, NoFilterMatch }

    internal enum ToDoDueDatePreset { Today, ThisWeek, NextWeek, Next30Days }

    /// <summary>
    /// State for the native To-Do screen — the Mac's <c>ToDoStore</c>, itself the web's to-do slice: three
    /// status buckets fetched per status (<c>GET /to-do?status=</c>), SMART / newest / due-date sort, a
    /// debounced title search, client-side filters, per-bucket paging, and optimistic completion.
    /// Everything runs on the UI thread; <see cref="Changed"/> tells the view to redraw.
    /// </summary>
    internal sealed class ToDoStore
    {
        internal static readonly TimeSpan SearchDebounce = TimeSpan.FromMilliseconds(500);
        internal static readonly TimeSpan StatusMessageLifetime = TimeSpan.FromSeconds(3);
        internal const int ExportMaxPages = 400;

        private readonly IToDoApi _api;
        private readonly Func<DateTime> _now;

        /// <summary>The signed-in user's id; tasks assigned to anybody else are left off the list (report 2026-09-25_21-22-28).</summary>
        internal Func<string> MeId = () =>
        {
            try { return AppSettings.GetInstance?.userAccount?.userID; } catch { return null; }
        };

        private bool IsMine(ToDoTask task) =>
            !ToDoAssigneeChoices.IsAssignedToSomebodyElse(ToDoAssigneeChoices.AssigneeId(task.AssigneeId, task.Assignee?.Id), MeId());
        private readonly Random _random = new Random();
        private readonly Dictionary<ToDoStatus, List<ToDoTask>> _buckets = new Dictionary<ToDoStatus, List<ToDoTask>>();
        private readonly Dictionary<ToDoStatus, ToDoStatusPaging> _paging = new Dictionary<ToDoStatus, ToDoStatusPaging>();
        private readonly Dictionary<string, Project> _projectsById = new Dictionary<string, Project>();
        private readonly DispatcherTimer _searchTimer;
        private readonly DispatcherTimer _statusTimer;
        private readonly DispatcherTimer _bannerTimer;
        private int _generation;
        private bool _hasAutoExpanded;
        private ToDoSortMode _sortMode;

        public ToDoStore(ToDoStrings strings, IToDoApi api = null, Func<DateTime> now = null, ToDoSortMode? sortMode = null)
        {
            Strings = strings;
            _api = api ?? new ToDoApi();
            _now = now ?? (() => DateTime.Now);
            _sortMode = sortMode ?? ReadSortMode();
            _searchTimer = new DispatcherTimer { Interval = SearchDebounce };
            _searchTimer.Tick += async (s, e) => { _searchTimer.Stop(); await RunSearchAsync(); };
            _statusTimer = new DispatcherTimer { Interval = StatusMessageLifetime };
            _statusTimer.Tick += (s, e) => { _statusTimer.Stop(); StatusMessage = null; Changed?.Invoke(); };
            _bannerTimer = new DispatcherTimer { Interval = ToDoCompletionBatch.Lifetime };
            _bannerTimer.Tick += (s, e) => { _bannerTimer.Stop(); Banner = null; Changed?.Invoke(); };
        }

        public event Action Changed;

        public ToDoStrings Strings { get; }
        public IList<Project> Projects { get; private set; } = new List<Project>();
        public bool IsFetching { get; private set; }
        public bool HasFetchedOnce { get; private set; }
        public ToDoStatus? LoadingMoreStatus { get; private set; }
        public string SearchQuery { get; private set; } = "";
        public IList<ToDoTask> SearchResults { get; private set; }
        public bool IsSearching { get; private set; }
        public ToDoFilters Filters { get; private set; } = new ToDoFilters();
        public bool ShowFilters { get; private set; }
        public string ExpandedSectionId { get; private set; }
        public string SelectedTaskId { get; private set; }
        public ISet<string> UpdatingTaskIds { get; } = new HashSet<string>();

        // Row actions asked for while that row's write was out. They were silently dropped (a delete confirmation
        // closed and nothing happened); now they run, against the row as it then stands, once the write settles.
        private readonly Dictionary<string, List<Func<Task>>> _afterWrite = new Dictionary<string, List<Func<Task>>>();

        private void AfterWrite(string taskId, Func<Task> action)
        {
            if (!_afterWrite.TryGetValue(taskId, out List<Func<Task>> queue))
            {
                queue = new List<Func<Task>>();
                _afterWrite[taskId] = queue;
            }
            queue.Add(action);
        }

        /// <summary>
        /// The row's write is done: release it and start what was asked for meanwhile, in order. Started, not awaited:
        /// the write's own caller must not wait on the next write.
        /// </summary>
        private Task SettleAsync(string taskId)
        {
            UpdatingTaskIds.Remove(taskId);
            if (!_afterWrite.TryGetValue(taskId, out List<Func<Task>> queue)) return Task.CompletedTask;
            _afterWrite.Remove(taskId);
            _ = RunQueuedAsync(taskId, queue);
            return Task.CompletedTask;
        }

        private async Task RunQueuedAsync(string taskId, List<Func<Task>> queue)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                if (UpdatingTaskIds.Contains(taskId))
                {
                    // The previous one started a write of its own: the rest waits for that one.
                    for (int j = i; j < queue.Count; j++) AfterWrite(taskId, queue[j]);
                    return;
                }
                try { await queue[i](); }
                catch (Exception ex) { Focus_Bear.Utils.LogWriter.LogWrite("ToDoStore queued action - " + ex.Message); }
            }
        }

        private ToDoTask CurrentRow(string taskId) => LoadedTasks.FirstOrDefault(t => t.Id == taskId);
        public bool IsSavingTask { get; private set; }
        public bool IsExporting { get; private set; }
        public string StatusMessage { get; private set; }
        public ToDoCompletionBatch Banner { get; private set; }

        public ToDoSortMode SortMode => _sortMode;

        public bool IsSearchActive => !string.IsNullOrWhiteSpace(SearchQuery);

        /// <summary>Every loaded task, Doing then To do then Done (web <c>state.toDo.tasks</c>).</summary>
        public IList<ToDoTask> LoadedTasks => ToDoSectioning.DefaultOrder.SelectMany(Bucket).ToList();

        /// <summary>Web <c>ListOfTasks</c>: search results (or the buckets) → client filters → sections.</summary>
        public IList<ToDoSection> Sections
        {
            get
            {
                RollDuePreset();
                IEnumerable<ToDoTask> source = IsSearchActive ? (SearchResults ?? new List<ToDoTask>()) : LoadedTasks;
                var filtered = source.Where(Filters.Matches).ToList();
                return ToDoSectioning.Build(filtered, Projects, _sortMode, Strings.SectionTitles(), _now());
            }
        }

        /// <summary>
        /// Web <c>getTotalCountByStatus</c>, corrected so a heading never promises a row the section can't
        /// show. A custom column, a search or a filter has no server total, so those count their own rows.
        /// </summary>
        public int TotalCount(ToDoSection section)
        {
            if (!section.Status.HasValue || IsSearchActive || Filters.IsActive
                || !_paging.TryGetValue(section.Status.Value, out var paging)) return section.Tasks.Count;
            return ToDoSectionCount.HeaderCount(section.Tasks.Count, paging.ItemCount, paging.ReceivedCount);
        }

        public bool HasMore(ToDoSection section) =>
            section.Status.HasValue && !IsSearchActive
            && _paging.TryGetValue(section.Status.Value, out var paging) && paging.HasNextPage;

        public ToDoEmptyState EmptyState
        {
            get
            {
                int visible = Sections.Sum(section => section.Tasks.Count);
                if (visible > 0) return ToDoEmptyState.None;
                int loaded = LoadedTasks.Count;
                if ((IsFetching || !HasFetchedOnce) && loaded == 0 && !IsSearchActive) return ToDoEmptyState.Loading;
                if (IsSearching && IsSearchActive) return ToDoEmptyState.Loading;
                if (loaded == 0 && !IsSearchActive) return ToDoEmptyState.FirstTask;
                return IsSearchActive ? ToDoEmptyState.NoSearchMatch : ToDoEmptyState.NoFilterMatch;
            }
        }

        public Project Project(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return _projectsById.TryGetValue(id, out var project) ? project : null;
        }

        /// <summary>The custom columns of the project a task's status belongs to.</summary>
        public IList<ProjectCustomStatus> CustomStatuses(ToDoTask task) =>
            Project(task.ProjectId)?.CustomStatuses ?? new List<ProjectCustomStatus>();

        public string CustomStatusLabel(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var project in Projects)
            {
                var status = project.CustomStatuses.FirstOrDefault(s => s.Id == id);
                if (status != null) return status.Label;
            }
            return null;
        }

        /// <summary>Every project the task belongs to, by name: its tags, plus the status-owner project.</summary>
        public IList<string> ProjectNames(ToDoTask task)
        {
            var ids = task.TagList.Select(tag => tag.Id).ToList();
            if (!string.IsNullOrEmpty(task.ProjectId) && !ids.Contains(task.ProjectId)) ids.Add(task.ProjectId);
            var names = new List<string>();
            foreach (var id in ids.Distinct())
            {
                string raw = Project(id)?.Name ?? task.TagList.FirstOrDefault(tag => tag.Id == id)?.Text;
                if (!string.IsNullOrEmpty(raw)) names.Add(Strings.ProjectName(raw));
            }
            return names;
        }

        private List<ToDoTask> Bucket(ToDoStatus status) =>
            _buckets.TryGetValue(status, out var bucket) ? bucket : new List<ToDoTask>();

        private void Notify() => Changed?.Invoke();

        /// <summary>
        /// A to-do was created, changed or deleted: the Day Plan's copy of the catalogue (kept up to a minute) is out
        /// of date. Without this a task made "due tomorrow" was missing from tomorrow's Day Plan for up to a minute
        /// (report 2026-09-25_19-54-42, found again by the E2E run).
        /// </summary>
        private static void DayPlanCatalogueChanged() => Focus_Bear.Habits.DayPlan.ToDoCatalogue.Invalidate();

        // MARK: Lifecycle

        /// <summary>Web <c>ToDosTab</c> mount: every active project, then all three buckets.</summary>
        public async Task ScreenAppearedAsync()
        {
            var projects = LoadProjectsAsync();
            var tasks = RefreshAllAsync();
            if (IsSearchActive) await RunSearchAsync();
            await Task.WhenAll(projects, tasks);
        }

        public async Task LoadProjectsAsync()
        {
            var projects = await _api.GetProjects();
            if (projects == null) return;
            SetProjects(projects);
            Notify();
        }

        private void SetProjects(IEnumerable<Project> projects)
        {
            Projects = projects.OrderBy(project => Strings.ProjectName(project.Name), StringComparer.CurrentCultureIgnoreCase).ToList();
            _projectsById.Clear();
            foreach (var project in Projects)
            {
                if (!_projectsById.ContainsKey(project.Id)) _projectsById[project.Id] = project;
            }
        }

        // MARK: Fetching

        /// <summary>Web <c>refreshTodosAllStatuses</c>: page 1 of every bucket with the current sort and project filter.</summary>
        public async Task RefreshAllAsync()
        {
            int generation = ++_generation;
            IsFetching = true;
            Notify();
            await Task.WhenAll(ToDoSectioning.DefaultOrder.Select(status => FetchPageAsync(1, status, generation)));
            if (generation != _generation) return;
            IsFetching = false;
            HasFetchedOnce = true;
            AutoExpandIfNeeded();
            Notify();
        }

        public async Task LoadNextPageAsync(ToDoSection section)
        {
            // While a refresh is replacing page 1, the paging in hand is the old one: its "next" page would land
            // after the new page 1 and skip every page in between.
            if (!section.Status.HasValue || !HasMore(section) || LoadingMoreStatus.HasValue || IsSearchActive || IsFetching) return;
            var status = section.Status.Value;
            LoadingMoreStatus = status;
            Notify();
            await FetchPageAsync(_paging[status].Page + 1, status, _generation);
            LoadingMoreStatus = null;
            Notify();
        }

        private async Task FetchPageAsync(int page, ToDoStatus status, int generation)
        {
            var result = await _api.GetPage(page, status, _sortMode, Filters.ProjectId);
            // A page for a sort or filter the user has since changed must not replace the list.
            if (generation != _generation) return;
            if (result == null)
            {
                ShowStatus(Strings.RequestFailed);
                return;
            }
            bool isFirstPage = page == 1;
            // A later page only extends the page it followed; one that lands after page 1 was re-fetched would
            // stitch page N onto page 1.
            if (!isFirstPage && (!_paging.TryGetValue(status, out var extended) || extended.Page != page - 1)) return;
            var bucket = isFirstPage ? new List<ToDoTask>() : new List<ToDoTask>(Bucket(status));
            var known = new HashSet<string>(bucket.Select(task => task.Id));
            // Rows left off still count as received, so the header's "more on the server" maths stays right.
            bucket.AddRange(result.Tasks.Where(task => IsMine(task) && known.Add(task.Id)));
            _buckets[status] = bucket;
            int receivedBefore = isFirstPage || !_paging.ContainsKey(status) ? 0 : _paging[status].ReceivedCount;
            _paging[status] = new ToDoStatusPaging
            {
                Page = page,
                HasNextPage = result.HasNextPage,
                ItemCount = result.ItemCount ?? bucket.Count,
                ReceivedCount = receivedBefore + result.ReceivedRowCount,
            };
        }

        /// <summary>Web accordion: open Doing when it has tasks, else To do — once.</summary>
        private void AutoExpandIfNeeded()
        {
            if (_hasAutoExpanded) return;
            _hasAutoExpanded = true;
            if (Bucket(ToDoStatus.InProgress).Count > 0) ExpandedSectionId = ToDoStatusWire.InProgress;
            else if (Bucket(ToDoStatus.NotStarted).Count > 0) ExpandedSectionId = ToDoStatusWire.NotStarted;
        }

        public void ToggleSection(ToDoSection section)
        {
            ExpandedSectionId = ExpandedSectionId == section.Id ? null : section.Id;
            SelectedTaskId = null;
            Notify();
        }

        public void Select(ToDoTask task)
        {
            SelectedTaskId = task?.Id;
            Notify();
        }

        // MARK: Search / sort / filters

        public void SetSearchQuery(string query)
        {
            if ((query ?? "") == SearchQuery) return;
            SearchQuery = query ?? "";
            _searchTimer.Stop();
            if (IsSearchActive)
            {
                IsSearching = true;
                _searchTimer.Start();
            }
            else
            {
                IsSearching = false;
                SearchResults = null;
            }
            Notify();
        }

        private async Task RunSearchAsync()
        {
            string query = SearchQuery.Trim();
            if (query.Length == 0) return;
            IsSearching = true;
            Notify();
            var results = await _api.Search(query);
            if (query != SearchQuery.Trim()) return;
            IsSearching = false;
            SearchResults = (results ?? new List<ToDoTask>()).Where(IsMine).ToList();
            // Offline, "Could not find tasks" alone reads as "you have no such task".
            if (results == null) ShowStatus(Strings.RequestFailed);
            Notify();
        }

        public void SetSortMode(ToDoSortMode mode)
        {
            if (mode == _sortMode) return;
            _sortMode = mode;
            try { RegistryManager.SetToDoSortMode(ToDoSortModeWire.Of(mode)); } catch (Exception ex) { LogWriter.LogWrite("ToDo sort save ex - " + ex.Message); }
            _ = RefreshAllAsync();
        }

        private static ToDoSortMode ReadSortMode()
        {
            string raw = null;
            try { raw = RegistryManager.GetToDoSortMode(); } catch (Exception) { }
            switch (raw)
            {
                case ToDoSortModeWire.Chronological: return ToDoSortMode.Chronological;
                case ToDoSortModeWire.DueDate: return ToDoSortMode.DueDate;
                default: return ToDoSortMode.Smart;
            }
        }

        public void ToggleFilters()
        {
            ShowFilters = !ShowFilters;
            if (!ShowFilters && Filters.IsActive) SetFilters(new ToDoFilters());
            else Notify();
        }

        /// <summary>Only the project filter is the server's (web <c>refreshTodosAllStatuses</c> on project change).</summary>
        public void SetFilters(ToDoFilters filters)
        {
            bool projectChanged = filters.ProjectId != Filters.ProjectId;
            Filters = filters;
            _duePreset = PresetMatching(filters, _now());
            if (projectChanged) _ = RefreshAllAsync();
            else Notify();
        }

        /// <summary>"Show in task list" from a project: its filter on, the filters panel open.</summary>
        public void ShowProjectFilter(string projectId)
        {
            ShowFilters = true;
            SetFilters(new ToDoFilters { ProjectId = projectId });
        }

        /// <summary>A project the catalogue doesn't know yet (just created, or opened from a card).</summary>
        public void EnsureProject(Project project)
        {
            if (project == null || _projectsById.ContainsKey(project.Id)) return;
            SetProjects(Projects.Concat(new[] { project }));
        }

        public ToDoFilters CopyFilters() => new ToDoFilters
        {
            ProjectId = Filters.ProjectId,
            EnergyLevel = Filters.EnergyLevel,
            DueDateStart = Filters.DueDateStart,
            DueDateEnd = Filters.DueDateEnd,
        };

        /// <summary>The local day range a preset selects, both ends inclusive; weeks start on Monday (ISO).</summary>
        public static Tuple<DateTime, DateTime> DueDateRange(ToDoDueDatePreset preset, DateTime now)
        {
            DateTime today = now.Date;
            int sinceMonday = ((int)today.DayOfWeek + 6) % 7;
            DateTime weekStart = today.AddDays(-sinceMonday);
            switch (preset)
            {
                case ToDoDueDatePreset.Today: return Tuple.Create(today, today);
                case ToDoDueDatePreset.ThisWeek: return Tuple.Create(weekStart, weekStart.AddDays(6));
                case ToDoDueDatePreset.NextWeek: return Tuple.Create(weekStart.AddDays(7), weekStart.AddDays(13));
                default: return Tuple.Create(today, today.AddDays(30));
            }
        }

        public ToDoDueDatePreset? ActiveDueDatePreset
        {
            get
            {
                RollDuePreset();
                return _duePreset;
            }
        }

        /// <summary>
        /// The preset the due-date filter was set with. "Today" means today: the screen stays open for days, and a
        /// range fixed when it was picked went on filtering to yesterday after midnight while the menu, no longer
        /// recognising the dates, read "All".
        /// </summary>
        private ToDoDueDatePreset? _duePreset;

        private static ToDoDueDatePreset? PresetMatching(ToDoFilters filters, DateTime now)
        {
            if (!filters.DueDateStart.HasValue || !filters.DueDateEnd.HasValue) return null;
            foreach (ToDoDueDatePreset preset in Enum.GetValues(typeof(ToDoDueDatePreset)))
            {
                var range = DueDateRange(preset, now);
                if (range.Item1 == filters.DueDateStart.Value && range.Item2 == filters.DueDateEnd.Value) return preset;
            }
            return null;
        }

        /// <summary>Moves a preset's range on to the current day once the day has changed.</summary>
        private void RollDuePreset()
        {
            if (!_duePreset.HasValue) return;
            var range = DueDateRange(_duePreset.Value, _now());
            if (Filters.DueDateStart == range.Item1 && Filters.DueDateEnd == range.Item2) return;
            Filters.DueDateStart = range.Item1;
            Filters.DueDateEnd = range.Item2;
        }

        // MARK: Local merge (web applyMergedTodoTaskFromPayload)

        private void UpsertLocally(ToDoTask task)
        {
            var newStatus = task.DefaultStatus;
            ToDoStatus? removedFrom = null;
            foreach (var status in ToDoSectioning.DefaultOrder)
            {
                var bucket = Bucket(status);
                if (bucket.RemoveAll(existing => existing.Id == task.Id) > 0)
                {
                    _buckets[status] = bucket;
                    removedFrom = status;
                }
            }
            var target = Bucket(newStatus);
            target.Insert(0, task);
            _buckets[newStatus] = target;
            if (removedFrom != newStatus)
            {
                if (removedFrom.HasValue && _paging.ContainsKey(removedFrom.Value)) _paging[removedFrom.Value].AdjustForLocalChange(-1);
                if (_paging.ContainsKey(newStatus)) _paging[newStatus].AdjustForLocalChange(1);
            }
            if (SearchResults != null)
            {
                var results = SearchResults.ToList();
                int index = results.FindIndex(existing => existing.Id == task.Id);
                if (index >= 0)
                {
                    results[index] = task;
                    SearchResults = results;
                }
            }
        }

        private void RemoveLocally(string id)
        {
            foreach (var status in ToDoSectioning.DefaultOrder)
            {
                var bucket = Bucket(status);
                if (bucket.RemoveAll(task => task.Id == id) > 0)
                {
                    _buckets[status] = bucket;
                    if (_paging.ContainsKey(status)) _paging[status].AdjustForLocalChange(-1);
                }
            }
            if (SearchResults != null) SearchResults = SearchResults.Where(task => task.Id != id).ToList();
        }

        // MARK: Row actions

        /// <summary>Web <c>useTaskCompletionToggle</c>: move it at once, <c>PUT /to-do</c>, move it back on failure.</summary>
        public async Task ToggleCompleteAsync(ToDoTask task)
        {
            if (UpdatingTaskIds.Contains(task.Id)) return;
            bool completed = !task.IsCompleted;
            var updated = ToDoStatusToggle.ApplyWithOwner(task, completed, Project(task.ProjectId));
            UpsertLocally(updated);
            var saved = await PersistAsync(updated, task);
            if (saved == null) return;
            if (saved.IsCompleted) NotifyCompleted(saved.Title);
            else NotifyUncompleted();
        }

        /// <summary>Web <c>TaskInfo.handleDueDateSave</c> — null clears the due date.</summary>
        public async Task SetDueDateAsync(ToDoTask task, DateTime? day)
        {
            // One write per row at a time, as the checkbox already is: a second PUT built from the first one's
            // optimistic row would re-send its change, and the first one's rollback would undo this one. It waits
            // for that write and then goes, built from the row as it then stands.
            if (UpdatingTaskIds.Contains(task.Id))
            {
                string id = task.Id;
                AfterWrite(id, () => { ToDoTask row = CurrentRow(id); return row == null ? Task.CompletedTask : SetDueDateAsync(row, day); });
                return;
            }
            string ymd = day.HasValue ? ToDoDates.Ymd(day.Value) : null;
            if (ToDoDates.YmdFromIso(task.DueDate) == ymd) return;
            var updated = task.Copy();
            updated.DueDate = ymd;
            SelectedTaskId = null;
            UpsertLocally(updated);
            await PersistAsync(updated, task);
        }

        /// <summary>
        /// The write behind a row action. A row off the wire carries its assignee only as a relation
        /// (backend#2414), so it is resolved here — sending the bare flat field would unassign the task.
        /// </summary>
        private async Task<ToDoTask> PersistAsync(ToDoTask task, ToDoTask previous)
        {
            UpdatingTaskIds.Add(task.Id);
            Notify();
            var body = task.Copy();
            body.AssigneeId = ToDoAssigneeChoices.AssigneeId(task.AssigneeId, task.Assignee?.Id);
            var response = await _api.Upsert(ToDoUpsertBody.Make(body));
            ToDoTask result = null;
            if (!response.IsSuccess)
            {
                UpsertLocally(previous);
                ShowFailure(ToDoStrings.Format(Strings.CouldNotUpdateTask, ToDoStrings.TitleToken, task.Title), ToDoServerError.Message(response.Body));
            }
            else
            {
                // A 2xx whose body doesn't decode still saved; keep the optimistic row.
                result = ToDoJson.ReadTask(response.Body) ?? task;
                UpsertLocally(result);
                DayPlanCatalogueChanged();
            }
            Notify();
            await SettleAsync(task.Id);
            return result;
        }

        /// <summary>One banner that batches rapid completions, the count rising while they keep landing.</summary>
        private void NotifyCompleted(string title)
        {
            var phrases = Strings.TaskCompletedPhrases;
            string phrase = phrases.Count > 0 ? phrases[_random.Next(phrases.Count)] : "";
            Banner = ToDoCompletionBatch.Recording(title, Banner, phrase, DateTimeOffset.Now);
            _bannerTimer.Stop();
            _bannerTimer.Interval = ToDoCompletionBatch.Lifetime;
            _bannerTimer.Start();
            Notify();
        }

        private void NotifyUncompleted()
        {
            Banner = Banner?.Uncompleting(DateTimeOffset.Now);
            if (Banner == null) _bannerTimer.Stop();
            Notify();
        }

        public void DismissBanner()
        {
            _bannerTimer.Stop();
            Banner = null;
            Notify();
        }

        public string BannerText
        {
            get
            {
                if (Banner == null) return null;
                if (Banner.Count == 1)
                {
                    return ToDoStrings.Format(ToDoStrings.Format(Strings.TaskCompletedSingle, ToDoStrings.PhraseToken, Banner.Phrase),
                                              ToDoStrings.TitleToken, Banner.LastTitle);
                }
                return ToDoStrings.Format(ToDoStrings.Format(Strings.TaskCompletedMultiple, ToDoStrings.PhraseToken, Banner.Phrase),
                                          ToDoStrings.CountToken, Banner.Count.ToString(CultureInfo.InvariantCulture));
            }
        }

        // MARK: Editor

        /// <summary>Web <c>submitToDoFromFlow</c>: <c>PUT /to-do</c>, then refresh every bucket.</summary>
        public async Task<bool> SaveTaskAsync(ToDoTask task, bool isNew, bool clearOutcome)
        {
            if (IsSavingTask) return false;
            IsSavingTask = true;
            Notify();
            var response = await _api.Upsert(ToDoUpsertBody.Make(task, clearOutcome));
            IsSavingTask = false;
            if (!response.IsSuccess)
            {
                ShowFailure(ToDoStrings.Format(isNew ? Strings.CouldNotCreateTask : Strings.CouldNotUpdateTask, ToDoStrings.TitleToken, task.Title),
                            ToDoServerError.Message(response.Body));
                Notify();
                return false;
            }
            UpsertLocally(ToDoJson.ReadTask(response.Body) ?? task);
            DayPlanCatalogueChanged();
            ShowStatus(isNew ? Strings.TaskCreated : Strings.TaskUpdated);
            _ = RefreshAllAsync();
            return true;
        }

        /// <summary>A body built elsewhere (a brain-dump suggestion), through the same API as every other write.</summary>
        public Task<ApiResponse> UpsertRaw(IDictionary<string, object> body) => _api.Upsert(body);

        /// <summary><c>DELETE /to-do</c> straight away — the row's inline confirmation already asked.</summary>
        public async Task<bool> DeleteAsync(ToDoTask task)
        {
            // Not while the row's own write is out: its answer would put the deleted task back on the list
            // (UpsertLocally re-inserts it), and a refused write would roll it back in too. It goes once that
            // write lands (it used to be dropped, the confirmation closing on nothing).
            if (UpdatingTaskIds.Contains(task.Id))
            {
                string id = task.Id;
                AfterWrite(id, async () => { ToDoTask row = CurrentRow(id); if (row != null) await DeleteAsync(row); });
                return true;
            }
            if (SelectedTaskId == task.Id) SelectedTaskId = null;
            UpdatingTaskIds.Add(task.Id);
            Notify();
            bool success = await _api.Delete(task.Id);
            if (success)
            {
                RemoveLocally(task.Id);
                DayPlanCatalogueChanged();
                _afterWrite.Remove(task.Id);   // nothing left to apply to a deleted row
            }
            ShowStatus(success ? Strings.TaskDeleted : Strings.CouldNotDeleteTask);
            await SettleAsync(task.Id);
            return success;
        }

        /// <summary><c>POST /projects</c> from the editor's inline "Create “…”" row.</summary>
        public async Task<Project> CreateProjectAsync(string name)
        {
            var response = await _api.CreateProject(name);
            var project = response.IsSuccess ? ToDoJson.ReadProject(response.Body) : null;
            if (project == null)
            {
                ShowFailure(Strings.CouldNotCreateProject, ToDoServerError.Message(response.Body));
                return null;
            }
            if (!_projectsById.ContainsKey(project.Id)) SetProjects(Projects.Concat(new[] { project }));
            Notify();
            return project;
        }

        // MARK: Export

        /// <summary>
        /// Web <c>useExportCsv</c>: every task (no filters), 50 a page, up to 400 pages. When a page fails the
        /// tasks already loaded are exported instead, and the status line says so.
        /// </summary>
        public async Task<string> BuildExportAsync()
        {
            if (IsExporting) return null;
            IsExporting = true;
            Notify();
            var all = new List<ToDoTask>();
            var seen = new HashSet<string>();
            bool failed = false;
            for (int page = 1; page <= ExportMaxPages; page++)
            {
                var result = await _api.GetPage(page, null, ToDoSortMode.Chronological, null);
                if (result == null) { failed = true; break; }
                // A task created while the export pages shifts every later page by one; the same row must not
                // be written twice. A "next page" that brings nothing new is the end, not 400 more requests.
                int added = 0;
                foreach (var task in result.Tasks)
                    if (!string.IsNullOrEmpty(task.Id) && seen.Add(task.Id)) { all.Add(task); added++; }
                if (!result.HasNextPage || added == 0) break;
            }
            IsExporting = false;
            var rows = failed ? LoadedTasks : all;
            var headers = new[] { Strings.CreateTaskTitle, Strings.Description, Strings.Objective, Strings.Status, Strings.Quadrant,
                                  Strings.FilterProject, Strings.Subtasks, Strings.FilterDueDate, Strings.CreatedShort };
            string csv = ToDoCsv.Build(rows, headers, task => Strings.StatusLabel(task.DefaultStatus),
                                       task => Strings.QuadrantLabel((EisenhowerQuadrant)(task.EisenhowerQuadrant ?? 1)));
            if (failed) ShowStatus(Strings.ExportFailed);
            Notify();
            return csv;
        }

        // MARK: Status line

        /// <summary>The generic failure plus the server's reason when it gave one.</summary>
        public void ShowFailure(string generic, string reason)
        {
            ShowStatus(string.IsNullOrEmpty(reason) ? generic : generic + ": " + reason);
        }

        public void ShowStatus(string message)
        {
            StatusMessage = message;
            _statusTimer.Stop();
            _statusTimer.Start();
            Notify();
        }
    }
}
