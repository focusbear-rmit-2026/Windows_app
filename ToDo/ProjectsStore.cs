using Focus_Bear.API;
using Focus_Bear.Utils;
using FocusBear.Core.Forecast;
using FocusBear.Core.ToDo;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Focus_Bear.ToDoScreens
{
    /// <summary>The project calls the Projects tab makes, so its stores can be driven by a fake in tests.</summary>
    internal interface IProjectsApi
    {
        Task<PortfolioSnapshot> GetPortfolio();
        Task<ToDoProjectsPage> GetProjectsPage(int page, string search, ProjectLifecycleStatus status);
        Task<Project> GetProject(string id);
        Task<ToDoPage> GetProjectTasks(Project project, ToDoSortMode sortMode, int page);
        Task<IList<ToDoTask>> GetNextTaskCandidates(PortfolioFetchTarget target);
        Task<ApiResponse> UpdateProject(string id, JObject body);
        Task<bool> DeleteProject(string id);
        Task<ApiResponse> CreateProject(string name, int? outcome);
        Task<ApiResponse> UpsertToDo(IDictionary<string, object> body);
    }

    internal sealed class ProjectsApi : IProjectsApi
    {
        private static FocusBearAPIClient Client() => new FocusBearAPIClient();
        public Task<PortfolioSnapshot> GetPortfolio() => Client().GetPortfolio();
        public Task<ToDoProjectsPage> GetProjectsPage(int page, string search, ProjectLifecycleStatus status) => Client().GetProjectsPage(page, search, status);
        public Task<Project> GetProject(string id) => Client().GetProject(id);
        public Task<ToDoPage> GetProjectTasks(Project project, ToDoSortMode sortMode, int page) => Client().GetProjectTasks(project, sortMode, FocusBearAPIClient.ToDoTasksPerPage, page);
        public Task<IList<ToDoTask>> GetNextTaskCandidates(PortfolioFetchTarget target) => Client().GetProjectNextTaskCandidates(target);
        public Task<ApiResponse> UpdateProject(string id, JObject body) => Client().UpdateProject(id, body);
        public Task<bool> DeleteProject(string id) => Client().DeleteProject(id);
        public Task<ApiResponse> CreateProject(string name, int? outcome) => Client().CreateRatedProject(name, outcome);
        public Task<ApiResponse> UpsertToDo(IDictionary<string, object> body) => Client().UpsertToDo(body);
    }

    /// <summary>
    /// The local forecast "What matters" is built from — the reprioritise forecast (the Mac's
    /// <c>LocalForecastStore</c>, which its Projects tab reads the same way). A fake in tests.
    /// </summary>
    internal interface IPortfolioForecast
    {
        event Action Changed;

        /// <summary>The latest calculation, or null before the first one (or when it could not be made).</summary>
        LocalTimeForecast.Result Result { get; }

        /// <summary>The catalogue the result was calculated from.</summary>
        IList<Project> Projects { get; }
        IList<ToDoTask> Tasks { get; }
        IList<LocalTimeForecast.TaskInput> TaskInputs { get; }

        /// <summary>Recalculate in the background when the last calculation is old; a fresh one is reused.</summary>
        void RefreshIfStale();

        /// <summary>Recalculate from a fresh catalogue, after a write changed what it was built from.</summary>
        void Refresh();
    }

    internal sealed class ReprioritisePortfolioForecast : IPortfolioForecast
    {
        private static Focus_Bear.Habits.DayPlan.ReprioritiseForecast Shared => Focus_Bear.Habits.DayPlan.ReprioritiseForecast.Shared;

        public event Action Changed
        {
            add { Shared.Changed += value; }
            remove { Shared.Changed -= value; }
        }

        public LocalTimeForecast.Result Result => Shared.Result;
        public IList<Project> Projects => Shared.Catalogue?.Projects ?? new List<Project>();
        public IList<ToDoTask> Tasks => Shared.Catalogue?.Tasks ?? new List<ToDoTask>();
        public IList<LocalTimeForecast.TaskInput> TaskInputs => Shared.TaskInputs;

        public void RefreshIfStale()
        {
            if (Shared.IsStale) _ = Shared.RefreshAsync(false);
        }

        public void Refresh()
        {
            Focus_Bear.Habits.DayPlan.ToDoCatalogue.Invalidate();
            _ = Shared.RefreshAsync(true);
        }
    }

    internal enum ProjectsViewMode { Focus, All }

    internal enum ProjectsEmptyVariant { None, ActiveZero, Search, Completed, Archived, Failed }

    /// <summary>
    /// State for the Projects tab — the Mac's <c>ProjectsStore</c>. "What matters" files the projects into
    /// lanes from the local reprioritise forecast (<see cref="LocalForecastPortfolio"/>: Ask for an extension /
    /// Start now / On track / Backburner), falling back to regrouping the server's <c>GET /portfolio</c> rows
    /// while no forecast has been calculated. "All projects" pages <c>GET /projects</c>, searched on the server
    /// and filtered by lifecycle status. Each card's NEXT task comes from the forecast's own tasks when it has
    /// them, else is fetched per project, a few at a time, as the web does.
    /// </summary>
    internal sealed class ProjectsStore
    {
        private readonly IProjectsApi _api;
        private readonly Func<DateTime> _now;
        private readonly DispatcherTimer _searchTimer;
        private readonly DispatcherTimer _statusTimer;
        private readonly Dictionary<string, ProjectTopPriorityTask> _nextTasks = new Dictionary<string, ProjectTopPriorityTask>();
        private readonly HashSet<string> _resolvedNext = new HashSet<string>();
        private readonly HashSet<string> _requestedNext = new HashSet<string>();
        private readonly SemaphoreSlim _nextGate = new SemaphoreSlim(PortfolioNextTask.MaxConcurrency);
        private int _generation;
        private int _portfolioGeneration;
        private int _page = 1;

        /// <summary>The search the loaded "All projects" list was fetched with; later pages must use the same one.</summary>
        private string _loadedQuery = "";

        private static bool SameQuery(string a, string b) => string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.Ordinal);
        private ProjectsViewMode _viewMode = ProjectsViewMode.Focus;
        private ProjectLifecycleStatus _statusFilter = ProjectLifecycleStatus.Active;

        public ProjectsStore(ToDoStrings strings, string currentUserId, IProjectsApi api = null, Func<DateTime> now = null,
                             IPortfolioForecast forecast = null)
            : this(strings, () => currentUserId, api, now, forecast) { }

        /// <summary>
        /// The signed-in user is read when needed, not when the store is made: the To-do screen (and this store) is
        /// built with the main window, before a first sign-in, so a fixed id stayed empty and a new user could not
        /// edit, rate, date or delete their own projects until the app restarted. It also follows a change of user.
        /// </summary>
        public ProjectsStore(ToDoStrings strings, Func<string> currentUserId, IProjectsApi api = null, Func<DateTime> now = null,
                             IPortfolioForecast forecast = null)
        {
            Strings = strings;
            _currentUserId = currentUserId ?? (() => "");
            _api = api ?? new ProjectsApi();
            _now = now ?? (() => DateTime.Now);
            _forecast = forecast;
            if (_forecast != null) _forecast.Changed += Notify;
            _searchTimer = new DispatcherTimer { Interval = ToDoStore.SearchDebounce };
            _searchTimer.Tick += async (s, e) => { _searchTimer.Stop(); if (_viewMode == ProjectsViewMode.All) await LoadProjectsAsync(); };
            _statusTimer = new DispatcherTimer { Interval = ToDoStore.StatusMessageLifetime };
            _statusTimer.Tick += (s, e) => { _statusTimer.Stop(); StatusMessage = null; StatusChanged?.Invoke(); };
        }

        public event Action Changed;

        /// <summary>A project's NEXT task arrived or changed; only that card's line needs drawing again.</summary>
        public event Action<string> NextTaskChanged;

        /// <summary>Only <see cref="StatusMessage"/> changed; nothing else on screen did.</summary>
        public event Action StatusChanged;

        public ToDoStrings Strings { get; }
        private readonly Func<string> _currentUserId;
        public string CurrentUserId => _currentUserId() ?? "";
        public PortfolioSnapshot Portfolio { get; private set; }
        public bool IsPortfolioLoading { get; private set; }
        public IList<Project> Projects { get; private set; } = new List<Project>();
        public bool IsProjectsLoading { get; private set; }
        public bool IsLoadingMore { get; private set; }
        public bool HasNextPage { get; private set; }
        public bool HasFailed { get; private set; }
        public string SearchQuery { get; private set; } = "";
        public string StatusMessage { get; private set; }
        public ProjectDetailStore Detail { get; private set; }
        public bool IsCreating { get; private set; }

        public ProjectsViewMode ViewMode => _viewMode;
        public ProjectLifecycleStatus StatusFilter => _statusFilter;

        private void Notify() => Changed?.Invoke();

        // MARK: Local forecast

        private readonly IPortfolioForecast _forecast;
        private LocalForecastPortfolio _localPortfolio;
        private IDictionary<string, ProjectTopPriorityTask> _localNextTasks = new Dictionary<string, ProjectTopPriorityTask>();
        private LocalTimeForecast.Result _localPortfolioSource;

        /// <summary>
        /// "What matters" from the local forecast, or null while there is none — then the server's own portfolio
        /// rows are grouped instead, so the view never waits on a calculation to show anything at all.
        /// </summary>
        public LocalForecastPortfolio LocalPortfolio
        {
            get
            {
                var result = _forecast?.Result;
                if (result == null) return null;
                if (!ReferenceEquals(result, _localPortfolioSource))
                {
                    _localPortfolioSource = result;
                    _localNextTasks = LocalForecastPortfolio.NextTasks(_forecast.TaskInputs, _forecast.Tasks, _now());
                    var topScores = _localNextTasks.ToDictionary(entry => entry.Key, entry => entry.Value.TopScore);
                    _localPortfolio = LocalForecastPortfolio.Make(_forecast.Projects, result, topScores);
                }
                return _localPortfolio;
            }
        }

        /// <summary>The cards matching the search, in their lanes.</summary>
        public PortfolioActionGroups ActionGroups
        {
            get
            {
                var local = LocalPortfolio;
                if (local == null)
                    return PortfolioGrouping.Group(PortfolioGrouping.Matching(Portfolio?.Projects ?? new List<PortfolioProject>(), SearchQuery));
                return new PortfolioActionGroups
                {
                    AskForExtension = PortfolioGrouping.Matching(local.Groups.AskForExtension, SearchQuery),
                    StartNow = PortfolioGrouping.Matching(local.Groups.StartNow, SearchQuery),
                    SteadyProgress = PortfolioGrouping.Matching(local.Groups.SteadyProgress, SearchQuery),
                    BackBurner = PortfolioGrouping.Matching(local.Groups.BackBurner, SearchQuery),
                };
            }
        }

        /// <summary>Whether "What matters" has anything to show yet: the local forecast, or the server's rows.</summary>
        public bool HasPortfolio => LocalPortfolio != null || Portfolio != null;

        /// <summary>The overloaded warning: the forecast's own verdict when it has one, else the server's.</summary>
        public bool IsOvercommitted => LocalPortfolio?.IsOvercommitted ?? Portfolio?.IsOvercommitted ?? false;

        /// <summary>
        /// Whether a card's bucket is a real assessment worth a badge. A locally "at risk" project whose work has
        /// no estimate is only the forecast saying it can't tell; the server's rows are always shown as sent.
        /// </summary>
        public bool IsAssessed(string projectId)
        {
            var local = LocalPortfolio;
            return local == null || local.AssessedProjectIds.Contains(projectId);
        }

        /// <summary>The overloaded banner's link: the host opens the reschedule plan.</summary>
        public event Action ReprioritiseRequested;

        public void OpenReprioritisePlan() => ReprioritiseRequested?.Invoke();

        public IDictionary<string, PortfolioProject> RisksById =>
            (Portfolio?.Projects ?? new List<PortfolioProject>()).GroupBy(project => project.Id).ToDictionary(group => group.Key, group => group.First());

        /// <summary>All projects, riskiest first (web <c>sortProjectsByRisk</c>).</summary>
        public IList<Project> SortedProjects => PortfolioGrouping.SortByRisk(Projects, RisksById);

        public ProjectsEmptyVariant EmptyVariant
        {
            get
            {
                if (Projects.Count > 0 || IsProjectsLoading) return ProjectsEmptyVariant.None;
                // A list that never arrived is not an empty list: "No projects yet — create your first" after a
                // failed request reads as if every project had gone.
                if (HasFailed) return ProjectsEmptyVariant.Failed;
                if (!string.IsNullOrWhiteSpace(_loadedQuery)) return ProjectsEmptyVariant.Search;
                switch (_statusFilter)
                {
                    case ProjectLifecycleStatus.Completed: return ProjectsEmptyVariant.Completed;
                    case ProjectLifecycleStatus.Archived: return ProjectsEmptyVariant.Archived;
                    default: return ProjectsEmptyVariant.ActiveZero;
                }
            }
        }

        public Project Project(string id) => Projects.FirstOrDefault(project => project.Id == id);

        /// <summary>
        /// The card's NEXT task. With a local forecast it is picked from the forecast's own tasks, as the lane
        /// order is, so the TOP chip and the card's place in its lane can never disagree.
        /// </summary>
        public ProjectTopPriorityTask NextTask(string projectId)
        {
            if (LocalPortfolio != null) return _localNextTasks.TryGetValue(projectId, out var local) ? local : null;
            return _nextTasks.TryGetValue(projectId, out var task) ? task : null;
        }

        public bool IsNextTaskPending(string projectId) => LocalPortfolio == null && !_resolvedNext.Contains(projectId);

        /// <summary>A project whose owner is somebody else: the card says "Member".</summary>
        public bool IsMemberOnly(Project project) =>
            !string.IsNullOrEmpty(project.OwnerId) && CurrentUserId.Length > 0 && project.OwnerId != CurrentUserId;

        public bool CanManage(Project project) =>
            ProjectPermissions.CanManageMembers(ProjectPermissions.ViewerRole(project, CurrentUserId)) || project.OwnerId == CurrentUserId;

        // MARK: Loading

        public async Task ScreenAppearedAsync()
        {
            _forecast?.RefreshIfStale();
            await Task.WhenAll(LoadPortfolioAsync(), LoadProjectsAsync());
        }

        public async Task LoadPortfolioAsync()
        {
            int generation = ++_portfolioGeneration;
            IsPortfolioLoading = true;
            Notify();
            var portfolio = await _api.GetPortfolio();
            // Every write reloads the portfolio; an older answer landing after a newer one would regroup the
            // cards by the dates and ratings from before the write.
            if (generation != _portfolioGeneration) return;
            IsPortfolioLoading = false;
            if (portfolio == null) ShowStatus(Strings.RequestFailed);
            else
            {
                Portfolio = portfolio;
                Invalidate(portfolio.Projects.Select(project => project.Id));
            }
            Notify();
            _ = LoadNextTasksAsync();
        }

        public async Task LoadProjectsAsync()
        {
            int generation = ++_generation;
            string query = SearchQuery;
            IsProjectsLoading = true;
            HasFailed = false;
            Notify();
            var page = await _api.GetProjectsPage(1, query, _statusFilter);
            if (generation != _generation) return;
            IsProjectsLoading = false;
            if (page == null)
            {
                HasFailed = true;
                ShowStatus(Strings.CouldNotLoadProjects);
                Notify();
                return;
            }
            _page = 1;
            _loadedQuery = query;
            Projects = page.Projects.ToList();
            HasNextPage = page.HasNextPage;
            Invalidate(page.Projects.Select(project => project.Id));
            Notify();
            _ = LoadNextTasksAsync();
        }

        public async Task LoadMoreAsync()
        {
            if (!HasNextPage || IsLoadingMore || IsProjectsLoading) return;
            int generation = _generation;
            IsLoadingMore = true;
            Notify();
            var page = await _api.GetProjectsPage(_page + 1, _loadedQuery, _statusFilter);
            IsLoadingMore = false;
            if (generation != _generation) return;
            if (page == null) ShowStatus(Strings.CouldNotLoadProjects);
            else
            {
                _page++;
                var known = new HashSet<string>(Projects.Select(project => project.Id));
                Projects = Projects.Concat(page.Projects.Where(project => known.Add(project.Id))).ToList();
                HasNextPage = page.HasNextPage;
            }
            Notify();
            _ = LoadNextTasksAsync();
        }

        /// <summary>Everything again, the local forecast included: a write changed what it was calculated from.</summary>
        public Task RefreshAsync()
        {
            _resolvedNext.Clear();
            _requestedNext.Clear();
            _forecast?.Refresh();
            return Task.WhenAll(LoadPortfolioAsync(), LoadProjectsAsync());
        }

        private void Invalidate(IEnumerable<string> projectIds)
        {
            foreach (var id in projectIds)
            {
                _resolvedNext.Remove(id);
                _requestedNext.Remove(id);
            }
        }

        /// <summary>
        /// Web <c>fetchPortfolioTopPriorityTasks</c>: the listed projects that have tasks, plus the portfolio's,
        /// fetched <see cref="PortfolioNextTask.MaxConcurrency"/> at a time. A project with no tasks needs no
        /// request — it has no next task, and its card says so.
        /// </summary>
        private async Task LoadNextTasksAsync()
        {
            var targets = PortfolioNextTask.FetchTargets(Projects, (Portfolio?.Projects ?? new List<PortfolioProject>()).Select(project => project.Id));
            var targetIds = new HashSet<string>(targets.Select(target => target.ProjectId));
            foreach (var project in Projects.Where(project => !targetIds.Contains(project.Id)))
            {
                _resolvedNext.Add(project.Id);
                _nextTasks.Remove(project.Id);
            }
            var pending = targets.Where(target => _requestedNext.Add(target.ProjectId)).ToList();
            await Task.WhenAll(pending.Select(async target =>
            {
                await _nextGate.WaitAsync();
                try
                {
                    var tasks = await _api.GetNextTaskCandidates(target);
                    if (tasks == null)
                    {
                        _requestedNext.Remove(target.ProjectId);
                        return;
                    }
                    _resolvedNext.Add(target.ProjectId);
                    var next = PortfolioNextTask.Pick(tasks, _now());
                    if (next == null) _nextTasks.Remove(target.ProjectId);
                    else _nextTasks[target.ProjectId] = next;
                    // One card's NEXT line, not the whole screen: these land one by one for every project
                    // (over a hundred on a big portfolio), and a full redraw per answer rebuilt every card
                    // ten times a second for as long as they kept coming.
                    NextTaskChanged?.Invoke(target.ProjectId);
                }
                finally { _nextGate.Release(); }
            }));
        }

        // MARK: Filters

        public void SetViewMode(ProjectsViewMode mode)
        {
            if (mode == _viewMode) return;
            _viewMode = mode;
            // "What matters" filters locally and never asks the server, so a query typed there left "All
            // projects" showing the unfiltered list under a search box that said otherwise.
            if (mode == ProjectsViewMode.All && !SameQuery(_loadedQuery, SearchQuery))
            {
                _searchTimer.Stop();
                _ = LoadProjectsAsync();
                return;
            }
            Notify();
        }

        /// <summary>"What matters" searches the rows it has; "All projects" asks the server, debounced.</summary>
        public void SetSearchQuery(string query)
        {
            if ((query ?? "") == SearchQuery) return;
            SearchQuery = query ?? "";
            _searchTimer.Stop();
            _searchTimer.Start();
            Notify();
        }

        public void SetStatusFilter(ProjectLifecycleStatus status)
        {
            if (status == _statusFilter) return;
            _statusFilter = status;
            _ = LoadProjectsAsync();
        }

        // MARK: Detail

        public void Open(Project project)
        {
            Detail = new ProjectDetailStore(Strings, CurrentUserId, project, _api, _now);
            Detail.Changed += Notify;
            Notify();
            _ = Detail.LoadAsync();
        }

        /// <summary>A portfolio row carries no columns or members; the detail loads the full project itself.</summary>
        public void Open(PortfolioProject risk)
        {
            Open(Project(risk.Id) ?? new Project(risk.Id, risk.Name) { DueDate = risk.DueDate });
        }

        public void CloseDetail()
        {
            if (Detail != null) Detail.Changed -= Notify;
            bool changed = Detail != null && Detail.HasChanges;
            Detail = null;
            Notify();
            if (changed) _ = RefreshAsync();
        }

        // MARK: Writes

        /// <summary><c>PUT /projects/:id { status }</c> — mark complete, archive, or restore.</summary>
        public async Task<bool> SetLifecycleAsync(Project project, ProjectLifecycleStatus status)
        {
            var response = await _api.UpdateProject(project.Id, new JObject { ["status"] = ProjectLifecycleStatusWire.Of(status) });
            if (!response.IsSuccess)
            {
                ShowFailure(Strings.CouldNotUpdateProject, ToDoServerError.Message(response.Body));
                return false;
            }
            ShowStatus(status == ProjectLifecycleStatus.Completed ? Strings.ProjectMarkedComplete
                     : status == ProjectLifecycleStatus.Archived ? Strings.ProjectArchived : Strings.ProjectRestored);
            await RefreshAsync();
            return true;
        }

        private readonly HashSet<string> _updatingDueIds = new HashSet<string>();

        /// <summary>A due-date save for this project is on its way to the server.</summary>
        public bool IsUpdatingDueDate(string projectId) => _updatingDueIds.Contains(projectId);

        /// <summary>The project's due date as the cards show it: the open page's, the list's, else the portfolio row's.</summary>
        public string DueDateOf(string projectId)
        {
            if (Detail != null && Detail.Project.Id == projectId) return Detail.Project.DueDate;
            var project = Project(projectId);
            if (project != null) return project.DueDate;
            var row = (Portfolio?.Projects ?? new List<PortfolioProject>()).Concat(LocalPortfolio?.Rows ?? new List<PortfolioProject>())
                .FirstOrDefault(candidate => candidate.Id == projectId);
            return row?.DueDate;
        }

        /// <summary>
        /// Mac <c>ProjectsStore.setDueDate</c>: <c>PUT /projects/:id { due_date }</c> — a yyyy-MM-dd day, or null to
        /// clear it. Every card showing the project changes at once; a refused save puts them back and says why,
        /// and a saved one reloads the lists so the portfolio regroups by the new date.
        /// </summary>
        public async Task<bool> SetDueDateAsync(string projectId, DateTime? day)
        {
            if (string.IsNullOrEmpty(projectId) || _updatingDueIds.Contains(projectId)) return false;
            if (ProjectDueDateEdit.IsUnchanged(DueDateOf(projectId), day)) return true;
            string ymd = ProjectDueDateEdit.WireValue(day);

            var projects = CopiesOf(projectId);
            var rows = (Portfolio?.Projects ?? new List<PortfolioProject>()).Concat(LocalPortfolio?.Rows ?? new List<PortfolioProject>())
                .Where(row => row.Id == projectId).ToList();
            var previousProjects = projects.Select(p => p.DueDate).ToList();
            var previousRows = rows.Select(row => new { row.DueDate, row.DaysUntilDue }).ToList();

            foreach (var project in projects) project.DueDate = ymd;
            foreach (var row in rows)
            {
                row.DueDate = ymd;
                row.DaysUntilDue = ProjectDueDateEdit.DaysUntilDue(day, _now());
            }
            _updatingDueIds.Add(projectId);
            Notify();

            var response = await _api.UpdateProject(projectId, new JObject { ["due_date"] = ymd == null ? JValue.CreateNull() : new JValue(ymd) });
            _updatingDueIds.Remove(projectId);
            if (!response.IsSuccess)
            {
                for (int i = 0; i < projects.Count; i++) projects[i].DueDate = previousProjects[i];
                for (int i = 0; i < rows.Count; i++)
                {
                    rows[i].DueDate = previousRows[i].DueDate;
                    rows[i].DaysUntilDue = previousRows[i].DaysUntilDue;
                }
                Notify();
                ShowFailure(Strings.CouldNotUpdateProject, ToDoServerError.Message(response.Body));
                return false;
            }
            ShowStatus(Strings.ProjectUpdated);
            await RefreshAsync();
            return true;
        }

        /// <summary>Every loaded copy of a project: the list's row and the open page's (the same object until the page reloads it).</summary>
        private List<Project> CopiesOf(string projectId)
        {
            var projects = new List<Project>();
            foreach (var candidate in Projects.Where(p => p.Id == projectId).Concat(Detail != null && Detail.Project.Id == projectId ? new[] { Detail.Project } : new Project[0]))
                if (!projects.Contains(candidate)) projects.Add(candidate);
            return projects;
        }

        private readonly HashSet<string> _updatingOutcomeIds = new HashSet<string>();

        public bool IsUpdatingOutcome(string projectId) => _updatingOutcomeIds.Contains(projectId);

        /// <summary>
        /// Mac <c>ProjectDetailStore.setOutcome</c>: <c>PUT /projects/:id { outcome }</c> — the /10 wire value
        /// (2, 4 … 10 for 1–5), or null for "Not set". Shown at once; a refused save puts the old rating back
        /// and says why. Legacy <c>importance</c> is never touched.
        /// </summary>
        public async Task<bool> SetOutcomeAsync(string projectId, int? wire)
        {
            if (string.IsNullOrEmpty(projectId) || _updatingOutcomeIds.Contains(projectId)) return false;
            var projects = CopiesOf(projectId);
            if (projects.Count == 0 || projects[0].Outcome == wire) return true;
            var previous = projects.Select(p => p.Outcome).ToList();
            foreach (var project in projects) project.Outcome = wire;
            _updatingOutcomeIds.Add(projectId);
            Notify();

            var response = await _api.UpdateProject(projectId, new JObject { ["outcome"] = wire.HasValue ? new JValue(wire.Value) : JValue.CreateNull() });
            _updatingOutcomeIds.Remove(projectId);
            if (!response.IsSuccess)
            {
                for (int i = 0; i < projects.Count; i++) projects[i].Outcome = previous[i];
                Notify();
                ShowFailure(Strings.CouldNotUpdateProject, ToDoServerError.Message(response.Body));
                return false;
            }
            ShowStatus(Strings.ProjectUpdated);
            await RefreshAsync();
            return true;
        }

        /// <summary>Mac <c>createProject(named:dueDate:outcome:)</c> — <c>POST /projects</c>, with the rating when one was picked.</summary>
        public async Task<Project> CreateProjectAsync(string name, int? outcome = null)
        {
            if (IsCreating || string.IsNullOrWhiteSpace(name)) return null;
            IsCreating = true;
            Notify();
            var response = await _api.CreateProject(name.Trim(), outcome);
            IsCreating = false;
            var project = response.IsSuccess ? ToDoJson.ReadProject(response.Body) : null;
            if (project == null)
            {
                ShowFailure(Strings.CouldNotCreateProject, ToDoServerError.Message(response.Body));
                return null;
            }
            ShowStatus(Strings.ProjectCreated);
            Projects = new[] { project }.Concat(Projects.Where(existing => existing.Id != project.Id)).ToList();
            Notify();
            return project;
        }

        public async Task<bool> DeleteProjectAsync(Project project)
        {
            if (!await _api.DeleteProject(project.Id))
            {
                ShowStatus(Strings.CouldNotDeleteProject);
                return false;
            }
            if (Detail != null && Detail.Project.Id == project.Id)
            {
                Detail.Changed -= Notify;
                Detail = null;
            }
            Projects = Projects.Where(existing => existing.Id != project.Id).ToList();
            ShowStatus(Strings.ProjectDeleted);
            _ = RefreshAsync();
            return true;
        }

        public void ShowFailure(string generic, string reason) => ShowStatus(string.IsNullOrEmpty(reason) ? generic : generic + ": " + reason);

        public void ShowStatus(string message)
        {
            StatusMessage = message;
            _statusTimer.Stop();
            _statusTimer.Start();
            StatusChanged?.Invoke();
        }
    }

    internal enum ProjectTasksMode { List, Board }

    /// <summary>
    /// One project's page — the Mac's <c>ProjectDetailStore</c>: the full project, its tasks as a list or a
    /// board of its columns, and the moves between them. Every move — drag, the card's "Move to" menu, the
    /// checkbox — goes through <see cref="ProjectTaskMove"/> / <see cref="ToDoStatusToggle"/> and rolls back
    /// if the server refuses it.
    /// </summary>
    internal sealed class ProjectDetailStore
    {
        private readonly IProjectsApi _api;
        private readonly Func<DateTime> _now;

        public ProjectDetailStore(ToDoStrings strings, string currentUserId, Project project, IProjectsApi api, Func<DateTime> now)
        {
            Strings = strings;
            CurrentUserId = currentUserId ?? "";
            Project = project;
            _api = api;
            _now = now;
        }

        public event Action Changed;

        public ToDoStrings Strings { get; }
        public string CurrentUserId { get; }
        public Project Project { get; private set; }
        public IList<ToDoTask> Tasks { get; private set; } = new List<ToDoTask>();
        public bool IsLoading { get; private set; }
        public bool IsTasksLoading { get; private set; }
        public bool NotFound { get; private set; }
        public ProjectTasksMode TasksMode { get; private set; } = ProjectTasksMode.List;
        public ISet<string> UpdatingTaskIds { get; } = new HashSet<string>();
        public string StatusMessage { get; private set; }

        /// <summary>Something here was written, so the project list is stale when we go back to it.</summary>
        public bool HasChanges { get; private set; }

        private void Notify() => Changed?.Invoke();

        public ProjectMemberRole? ViewerRole => ProjectPermissions.ViewerRole(Project, CurrentUserId);
        public bool CanMoveTask => ProjectPermissions.CanMoveTask(ViewerRole) || Project.OwnerId == CurrentUserId || Project.MemberList.Count == 0;
        public bool CanManage => ProjectPermissions.CanManageMembers(ViewerRole) || Project.OwnerId == CurrentUserId;

        /// <summary>Mac <c>canEditName</c>: the owner may change the due date and outcome from the page's header.</summary>
        public bool CanEditHeader => ProjectPermissions.CanEditName(ViewerRole) || (Project.OwnerId == CurrentUserId && CurrentUserId.Length > 0);
        public bool CanDelete => ProjectPermissions.CanDeleteProject(ViewerRole) || Project.OwnerId == CurrentUserId;

        public IList<ProjectColumn> Columns => ProjectKanban.Columns(Project, Strings.ColumnTitles());

        public IList<ToDoTask> SortedTasks => ToDoSorting.Sort(Tasks, ToDoSortMode.Smart, _now());

        public IList<ToDoTask> TasksIn(ProjectColumn column) =>
            SortedTasks.Where(task => ProjectKanban.ColumnId(task, Columns, Project.UsesCustomStatuses) == column.Id).ToList();

        public string ColumnIdOf(ToDoTask task) => ProjectKanban.ColumnId(task, Columns, Project.UsesCustomStatuses);

        public async Task LoadAsync()
        {
            IsLoading = true;
            Notify();
            var loadTasks = LoadTasksAsync();
            var project = await _api.GetProject(Project.Id);
            IsLoading = false;
            if (project == null || project.Id != Project.Id) NotFound = Project.CustomStatuses.Count == 0 && string.IsNullOrEmpty(Project.OwnerId);
            else Project = project;
            Notify();
            await loadTasks;
        }

        /// <summary>A cap on the pages one project reads, so a server that always says "more" cannot spin forever.</summary>
        internal const int MaxTaskPages = 40;

        private int _tasksGeneration;

        /// <summary>
        /// Every task of the project, page by page. Page 1 alone (50 rows) left a bigger project's list and board
        /// silently short — the rest unreachable, the column counts wrong. A load overtaken by a newer one (the
        /// editor saved while the page was still loading) is dropped rather than put back over it.
        /// </summary>
        public async Task LoadTasksAsync()
        {
            int generation = ++_tasksGeneration;
            IsTasksLoading = true;
            Notify();
            var all = new List<ToDoTask>();
            var seen = new HashSet<string>();
            bool failed = false;
            for (int number = 1; number <= MaxTaskPages; number++)
            {
                var page = await _api.GetProjectTasks(Project, ToDoSortMode.Smart, number);
                if (generation != _tasksGeneration) return;
                if (page == null) { failed = true; break; }
                int added = 0;
                foreach (var task in page.Tasks)
                    if (!string.IsNullOrEmpty(task.Id) && seen.Add(task.Id)) { all.Add(task); added++; }
                if (!page.HasNextPage || added == 0) break;
            }
            IsTasksLoading = false;
            // A page that failed leaves the list as it was, rather than a shortened one passing for the whole.
            if (failed) ShowStatus(Strings.RequestFailed);
            else Tasks = all;
            Notify();
        }

        public void SetTasksMode(ProjectTasksMode mode)
        {
            TasksMode = mode;
            Notify();
        }

        /// <summary>Web <c>moveProjectDetailsKanbanTask</c>: move at once, <c>PUT /to-do</c>, roll back on failure.</summary>
        public Task MoveAsync(ToDoTask task, string columnId)
        {
            var moved = ProjectTaskMove.Plan(task, columnId, Project, Columns, CanMoveTask);
            return moved == null ? Task.CompletedTask : PersistAsync(moved, task, current => MoveAsync(current, columnId));
        }

        public Task ToggleCompleteAsync(ToDoTask task)
        {
            var statuses = Project.UsesCustomStatuses ? Project.CustomStatuses : new List<ProjectCustomStatus>();
            return PersistAsync(ToDoStatusToggle.Apply(task, !task.IsCompleted, statuses), task, current => ToggleCompleteAsync(current));
        }

        // Board actions asked for while that card's write was out; they run, in order, once it settles.
        private readonly Dictionary<string, List<Func<Task>>> _afterWrite = new Dictionary<string, List<Func<Task>>>();

        private ToDoTask CardNow(string id) => Tasks.FirstOrDefault(t => t.Id == id);

        private void AfterWrite(string id, Func<Task> action)
        {
            if (!_afterWrite.TryGetValue(id, out List<Func<Task>> queue)) { queue = new List<Func<Task>>(); _afterWrite[id] = queue; }
            queue.Add(action);
        }

        private async Task RunQueuedAsync(string id, List<Func<Task>> queue)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                if (UpdatingTaskIds.Contains(id)) { for (int j = i; j < queue.Count; j++) AfterWrite(id, queue[j]); return; }
                try { await queue[i](); }
                catch (Exception ex) { Focus_Bear.Utils.LogWriter.LogWrite("ProjectsStore queued action - " + ex.Message); }
            }
        }

        private async Task PersistAsync(ToDoTask task, ToDoTask previous, Func<ToDoTask, Task> redoFromCurrent = null)
        {
            // One write per card at a time. The checkbox hides while a card saves, but a drag or the "Move to"
            // menu does not: a second move raced the first, the first's answer overwrote it, and the first's
            // rollback put the card back behind both. The later one now waits for the first and is then planned
            // again from where the card is (it used to be dropped without a word).
            if (UpdatingTaskIds.Contains(task.Id))
            {
                string id = task.Id;
                if (redoFromCurrent != null)
                    AfterWrite(id, () => { ToDoTask card = CardNow(id); return card == null ? Task.CompletedTask : redoFromCurrent(card); });
                return;
            }
            ReplaceLocally(task);
            UpdatingTaskIds.Add(task.Id);
            Notify();
            // GET /to-do carries the assignee only as a relation (backend#2414); a board move that sent the
            // flat field as it arrived would unassign the task.
            var outgoing = task.Copy();
            outgoing.AssigneeId = ToDoAssigneeChoices.AssigneeId(task.AssigneeId, task.Assignee?.Id);
            var response = await _api.UpsertToDo(ToDoUpsertBody.Make(outgoing));
            if (response.IsSuccess) Focus_Bear.Habits.DayPlan.ToDoCatalogue.Invalidate();   // the Day Plan re-reads it
            UpdatingTaskIds.Remove(task.Id);
            List<Func<Task>> waiting = null;
            if (_afterWrite.TryGetValue(task.Id, out waiting)) _afterWrite.Remove(task.Id);
            var outcome = ProjectTaskMove.Settled(response.Status, response.IsSuccess ? ToDoJson.ReadTask(response.Body) : null, task, previous);
            ReplaceLocally(outcome.Task);
            if (outcome.Failed)
            {
                string reason = ToDoServerError.Message(response.Body);
                string generic = ToDoStrings.Format(Strings.CouldNotUpdateTask, ToDoStrings.TitleToken, task.Title);
                ShowStatus(string.IsNullOrEmpty(reason) ? generic : generic + ": " + reason);
            }
            else HasChanges = true;
            Notify();
            if (waiting != null) _ = RunQueuedAsync(task.Id, waiting);   // started, not awaited: this caller doesn't wait on the next write
        }

        private void ReplaceLocally(ToDoTask task)
        {
            var list = Tasks.ToList();
            int index = list.FindIndex(existing => existing.Id == task.Id);
            if (index >= 0) list[index] = task;
            else list.Insert(0, task);
            Tasks = list;
        }

        /// <summary>After the editor saved or deleted a task.</summary>
        public void MarkChanged()
        {
            HasChanges = true;
            _ = LoadTasksAsync();
        }

        public void ShowStatus(string message)
        {
            StatusMessage = message;
            Notify();
        }
    }
}
