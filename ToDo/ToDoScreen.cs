using Focus_Bear.StatsScreens;
using Focus_Bear.Utils;
using FocusBear.Core.ToDo;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using static Focus_Bear.ToDoScreens.ToDoUi;

namespace Focus_Bear.ToDoScreens
{
    /// <summary>
    /// The native To-Do screen, replacing the dashboard.focusbear.io/webview/tools-todo-list webview: the
    /// toolbar (new / search / filter / sort / refresh / more), the completion banner, the filters panel and
    /// the Doing · To do · Done sections. Port of the Mac's <c>TasksTabView</c>: a click on a row reveals
    /// Snooze · Edit · Mark complete · Delete beneath it, a double-click opens the editor, the checkbox
    /// completes, and the Due cell edits the due date.
    /// </summary>
    public sealed class ToDoScreen : UserControl
    {
        private const int MaxProjectChips = 3;

        private readonly ToDoStrings _strings = ToDoStrings.Load();
        private readonly ToDoStore _store;
        private readonly ProjectsStore _projects;
        private readonly ContentControl _tabs = new ContentControl { VerticalAlignment = VerticalAlignment.Center };
        private readonly ContentControl _page = new ContentControl();
        private FrameworkElement _tasksPage;
        private ProjectsView _projectsView;
        private bool _showingProjects;
        private readonly TextBox _search = new TextBox();
        private readonly StackPanel _toolbarRight = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        private readonly ContentControl _sort = new ContentControl { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        private readonly ContentControl _filterButton = new ContentControl { VerticalAlignment = VerticalAlignment.Center };
        private readonly StackPanel _banners = new StackPanel();
        private readonly StackPanel _list = new StackPanel();
        private readonly ScrollViewer _scroll;
        private readonly TextBlock _statusLine;
        private string _confirmingDeleteId;
        private bool _redrawQueued;

        public ToDoScreen() : this(null) { }

        internal ToDoScreen(ToDoStore store, ProjectsStore projects = null)
        {
            _store = store ?? new ToDoStore(_strings);
            _projects = projects ?? new ProjectsStore(_strings, () => Utils.AppSettings.GetInstance.userAccount?.userID);
            SetResourceReference(BackgroundProperty, "BackgroundColor");

            var title = StatsUi.Text(_strings.Title, 20, FontWeights.SemiBold, wrap: false);
            title.VerticalAlignment = VerticalAlignment.Center;
            title.Margin = new Thickness(0, 0, 16, 0);
            var titleRow = new DockPanel { LastChildFill = false, Margin = new Thickness(24, 16, 24, 12) };
            DockPanel.SetDock(title, Dock.Left);
            titleRow.Children.Add(title);
            DockPanel.SetDock(_tabs, Dock.Left);
            titleRow.Children.Add(_tabs);

            var toolbar = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 12) };
            var newTask = StatsUi.Button("+  " + _strings.NewTask, (s, e) => OpenEditor(null), primary: true);
            AutomationProperties.SetAutomationId(newTask, "btnToDoNewTask");
            DockPanel.SetDock(newTask, Dock.Left);
            toolbar.Children.Add(newTask);

            AutomationProperties.SetAutomationId(_search, "txtToDoSearch");
            _search.TextChanged += (s, e) => _store.SetSearchQuery(_search.Text);
            _search.KeyDown += (s, e) => { if (e.Key == Key.Escape) _search.Text = ""; };
            var searchField = Field(_search, _strings.SearchPlaceholder);
            searchField.Width = 260;
            searchField.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(searchField, Dock.Left);
            toolbar.Children.Add(searchField);
            DockPanel.SetDock(_filterButton, Dock.Left);
            toolbar.Children.Add(_filterButton);
            DockPanel.SetDock(_sort, Dock.Left);
            toolbar.Children.Add(_sort);
            DockPanel.SetDock(_toolbarRight, Dock.Right);
            toolbar.Children.Add(_toolbarRight);

            _toolbarRight.Children.Add(IconButton(Glyph(0xE72C), _strings.Refresh, false, async (s, e) =>
            {
                await _store.LoadProjectsAsync();
                await _store.RefreshAllAsync();
            }, "btnToDoRefresh"));
            _toolbarRight.Children.Add(MoreButton());

            _statusLine = StatsUi.Text("", 12, secondary: true);
            _statusLine.Margin = new Thickness(0, 8, 0, 0);
            AutomationProperties.SetAutomationId(_statusLine, "txtToDoStatus");

            var header = new StackPanel { Margin = new Thickness(24, 0, 24, 0) };
            header.Children.Add(toolbar);
            header.Children.Add(_banners);

            _scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = new Border { Padding = new Thickness(24, 4, 24, 16), Child = _list },
            };
            _scroll.ScrollChanged += (s, e) =>
            {
                // Web infinite scroll: near the bottom of an open section, fetch its next page.
                if (e.VerticalChange > 0 && _scroll.ScrollableHeight - _scroll.VerticalOffset < 120) LoadMoreForOpenSection();
            };

            var footer = new Border { Padding = new Thickness(24, 0, 24, 12), Child = _statusLine };
            var tasksPage = new DockPanel();
            DockPanel.SetDock(header, Dock.Top);
            DockPanel.SetDock(footer, Dock.Bottom);
            tasksPage.Children.Add(header);
            tasksPage.Children.Add(footer);
            tasksPage.Children.Add(_scroll);
            _tasksPage = tasksPage;
            _page.Content = tasksPage;
            var root = new DockPanel();
            DockPanel.SetDock(titleRow, Dock.Top);
            root.Children.Add(titleRow);
            root.Children.Add(_page);
            Content = root;
            DrawTabs();

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control) { OpenEditor(null); e.Handled = true; }
                else if (e.Key == Key.Escape && _store.SelectedTaskId != null && !(e.OriginalSource is TextBox)) { _store.Select(null); e.Handled = true; }
            };

            _store.Changed += QueueRedraw;
            Focus_Bear.Habits.DayPlan.ReprioritiseForecast.Shared.Changed += QueueRedraw;
            IsVisibleChanged += async (s, e) =>
            {
                if (!(bool)e.NewValue) return;
                // The prompt needs a forecast; price it in the background at most every fifteen minutes.
                var forecast = Focus_Bear.Habits.DayPlan.ReprioritiseForecast.Shared;
                if (forecast.IsStale) _ = forecast.RefreshAsync(false);
                if (_showingProjects) await _projects.ScreenAppearedAsync();
                else await _store.ScreenAppearedAsync();
            };
            Redraw();
        }

        internal ToDoStore Store => _store;
        internal ProjectsStore Projects => _projects;

        private void DrawTabs()
        {
            var tabs = StatsUi.Segmented(new[] { false, true }, _showingProjects, projects => projects ? _strings.TabProjects : _strings.TabTasks, ShowTab);
            AutomationProperties.SetAutomationId(tabs, "segToDoTabs");
            _tabs.Content = tabs;
        }

        /// <summary>Tasks or Projects. The Projects tab is built the first time it is opened.</summary>
        internal void ShowTab(bool projects)
        {
            if (projects == _showingProjects) return;
            _showingProjects = projects;
            DrawTabs();
            if (projects)
            {
                if (_projectsView == null)
                {
                    _projectsView = new ProjectsView(_projects, _store, ShowProjectInTaskList, OpenEditor);
                    _ = _projects.ScreenAppearedAsync();
                }
                _page.Content = _projectsView;
            }
            else
            {
                _page.Content = _tasksPage;
                _ = _store.ScreenAppearedAsync();
            }
        }

        private void ShowProjectInTaskList(string projectId)
        {
            ShowTab(false);
            _store.ShowProjectFilter(projectId);
        }

        /// <summary>Several store changes in one turn of the dispatcher draw once.</summary>
        private void QueueRedraw()
        {
            if (_redrawQueued) return;
            _redrawQueued = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _redrawQueued = false;
                Redraw();
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private void Redraw()
        {
            _filterButton.Content = IconButton(Glyph(0xE71C), _strings.Filter, _store.ShowFilters || _store.Filters.IsActive, (s, e) => _store.ToggleFilters(), "btnToDoFilter");
            ((FrameworkElement)_filterButton.Content).Margin = new Thickness(8, 0, 0, 0);
            _sort.Content = SortControl();
            _statusLine.Text = _store.StatusMessage ?? "";
            DrawBanners();
            DrawList();
        }

        private FrameworkElement SortControl()
        {
            var modes = new[] { ToDoSortMode.Smart, ToDoSortMode.Chronological, ToDoSortMode.DueDate };
            var control = StatsUi.Segmented(modes, _store.SortMode, _strings.SortTitle, mode => _store.SetSortMode(mode));
            control.ToolTip = _strings.SortTooltip(_store.SortMode);
            AutomationProperties.SetAutomationId(control, "segToDoSort");
            return control;
        }

        private Button MoreButton()
        {
            Button button = null;
            button = IconButton(Glyph(0xE712), _strings.MoreOptions, false, (s, e) =>
            {
                // Same order, separators and icons as the Mac's ToDoListView.toolsMenu; each glyph is the
                // closest Segoe MDL2 match for the Mac's SF Symbol (named alongside).
                var menu = new ToDoMenu(button);
                // The Mac's "Re-prioritise todo list": the reschedule plan (report 2026-09-25_14-33-34).
                menu.Add(Focus_Bear.Habits.DayPlan.ForecastText.Get("reprioritiseMenuItem"), false,
                         () => Focus_Bear.Habits.DayPlan.ReprioritiseWindow.Open(Window.GetWindow(this)), "menuToDoReprioritise",
                         icon: 0xEC92);  // calendar.badge.clock -> DateTime
                menu.AddSeparator();
                menu.Add(_strings.BrainDumpToTasks, false, () => ToDoToolsWindows.ShowBrainDump(Window.GetWindow(this), _store), "menuToDoBrainDump",
                         icon: 0xE82F);  // brain -> Lightbulb
                menu.Add(_strings.ExportCsv, false, ExportCsv, "menuToDoExportCsv", enabled: !_store.IsExporting, tooltip: _strings.ExportCsvTooltip,
                         icon: 0xE72D);  // square.and.arrow.up -> Share
                menu.Add(_strings.EisenhowerMatrix, false, () => ToDoToolsWindows.ShowMatrix(Window.GetWindow(this), _store, OpenEditor), "menuToDoMatrix",
                         icon: 0xF0E2);  // square.grid.2x2 -> GridView
                menu.Add(_strings.Procrastinate, false, () => ToDoToolsWindows.ShowProcrastinate(Window.GetWindow(this), _store), "menuToDoProcrastinate",
                         icon: 0xE728);  // bookmark -> FavoriteList (Windows bookmarks)
                menu.AddSeparator();
                menu.Add(_strings.ManageIntegrations, false, () => StatsUi.OpenExternal("https://dashboard.focusbear.io/integrations"), "menuToDoIntegrations",
                         icon: 0xE71B);  // link -> Link
                menu.Open();
            }, "btnToDoMore");
            return button;
        }

        private async void ExportCsv()
        {
            string csv = await _store.BuildExportAsync();
            if (csv == null) return;
            var dialog = new SaveFileDialog { FileName = "focus-bear-tasks.csv", DefaultExt = ".csv", Filter = "CSV (*.csv)|*.csv" };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            try
            {
                File.WriteAllText(dialog.FileName, csv, new UTF8Encoding(true));
                _store.ShowStatus(_strings.ExportSaved);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ToDo export csv failed - " + ex.Message);
                _store.ShowStatus(_strings.RequestFailed);
            }
        }

        // ------------------------------------------------------------------ banners and filters

        private void DrawBanners()
        {
            _banners.Children.Clear();
            string banner = _store.BannerText;
            if (banner != null)
            {
                var row = new DockPanel();
                var close = new Button { Content = Glyph(0xE711), FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 10, Template = StatsUi.PlainTemplate(), Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
                close.SetResourceReference(Control.ForegroundProperty, "TraySubtitleColor");
                close.Click += (s, e) => _store.DismissBanner();
                DockPanel.SetDock(close, Dock.Right);
                row.Children.Add(close);
                var check = new TextBlock { Text = Glyph(0xE930), FontFamily = new FontFamily("Segoe MDL2 Assets"), Foreground = Good, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
                DockPanel.SetDock(check, Dock.Left);
                row.Children.Add(check);
                var text = StatsUi.Text(banner, 13, FontWeights.Medium, wrap: false);
                AutomationProperties.SetAutomationId(text, "txtToDoBanner");
                row.Children.Add(text);
                _banners.Children.Add(new Border { Child = row, Background = StatsUi.Tint(Good, 0.12), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 0, 10) });
            }
            var suggestion = Focus_Bear.Habits.DayPlan.ReprioritiseForecast.Shared.Suggestion;
            if (suggestion.HasValue) _banners.Children.Add(ReprioritiseBanner(suggestion.Value));
            if (_store.ShowFilters) _banners.Children.Add(FiltersPanel());
        }

        /// <summary>
        /// The Mac's reprioritise prompt: a suggestion, not an alarm — shown when five or more tasks are
        /// overdue, or a day in the next five is two hours or more over, and quiet for a week once dismissed.
        /// </summary>
        private FrameworkElement ReprioritiseBanner(FocusBear.Core.Forecast.ReprioritiseSuggestion.Reason reason)
        {
            var orange = new SolidColorBrush(Color.FromRgb(0xFF, 0x95, 0x00));
            var row = new DockPanel();
            var close = new Button { Content = Glyph(0xE711), FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 10, Template = StatsUi.PlainTemplate(), Cursor = Cursors.Hand,
                                     VerticalAlignment = VerticalAlignment.Center, ToolTip = Focus_Bear.Habits.DayPlan.ForecastText.Get("reprioritiseDismiss") };
            close.SetResourceReference(Control.ForegroundProperty, "TraySubtitleColor");
            AutomationProperties.SetAutomationId(close, "btnToDoReprioritiseDismiss");
            close.Click += (s, e) => Focus_Bear.Habits.DayPlan.ReprioritiseForecast.Shared.DismissSuggestion();
            DockPanel.SetDock(close, Dock.Right);
            row.Children.Add(close);
            var warn = new TextBlock { Text = Glyph(0xE7BA), FontFamily = new FontFamily("Segoe MDL2 Assets"), Foreground = orange, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            DockPanel.SetDock(warn, Dock.Left);
            row.Children.Add(warn);
            var line = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            var text = StatsUi.Text(Focus_Bear.Habits.DayPlan.ForecastText.SuggestionText(reason), 13, FontWeights.Medium);
            text.Margin = new Thickness(0, 0, 10, 0);
            AutomationProperties.SetAutomationId(text, "txtToDoReprioritise");
            line.Children.Add(text);
            var link = new TextBlock { Text = Focus_Bear.Habits.DayPlan.ForecastText.Get("reprioritiseLink"), FontSize = 13, Foreground = orange,
                                       TextDecorations = TextDecorations.Underline, Cursor = Cursors.Hand };
            AutomationProperties.SetAutomationId(link, "lnkToDoReprioritise");
            link.MouseLeftButtonUp += (s, e) => Focus_Bear.Habits.DayPlan.ReprioritiseWindow.Open(Window.GetWindow(this));
            line.Children.Add(link);
            row.Children.Add(line);
            return new Border { Child = row, Background = StatsUi.Tint(orange, 0.12), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 0, 10) };
        }

        private FrameworkElement FiltersPanel()
        {
            var panel = new WrapPanel { Orientation = Orientation.Horizontal };
            var filters = _store.Filters;

            var duePreset = _store.ActiveDueDatePreset;
            panel.Children.Add(FilterColumn(_strings.FilterDueDate, MenuButton(duePreset.HasValue ? PresetLabel(duePreset.Value) : _strings.FilterAll, duePreset.HasValue, menu =>
            {
                menu.Add(_strings.FilterAll, !duePreset.HasValue, () => ApplyDuePreset(null));
                foreach (ToDoDueDatePreset preset in Enum.GetValues(typeof(ToDoDueDatePreset)))
                {
                    var captured = preset;
                    menu.Add(PresetLabel(preset), duePreset == preset, () => ApplyDuePreset(captured), "menuToDoDue" + preset);
                }
            }, "btnToDoFilterDue")));

            var energy = filters.EnergyLevel;
            panel.Children.Add(FilterColumn(_strings.FilterEnergyLevel, MenuButton(EnergyLabel(energy), energy.HasValue, menu =>
            {
                menu.Add(_strings.FilterAll, !energy.HasValue, () => ApplyEnergy(null));
                menu.Add(EnergyLabel(ToDoEnergyLevel.Low), energy == ToDoEnergyLevel.Low, () => ApplyEnergy(ToDoEnergyLevel.Low), "menuToDoEnergyLow");
                menu.Add(EnergyLabel(ToDoEnergyLevel.Full), energy == ToDoEnergyLevel.Full, () => ApplyEnergy(ToDoEnergyLevel.Full), "menuToDoEnergyFull");
            }, "btnToDoFilterEnergy")));

            var project = _store.Project(filters.ProjectId);
            string projectLabel = filters.ProjectId == null ? _strings.FilterAll : _strings.ProjectName(project != null ? project.Name : filters.ProjectId);
            panel.Children.Add(FilterColumn(_strings.FilterProject, MenuButton(projectLabel, filters.ProjectId != null, menu =>
            {
                menu.Add(_strings.FilterAll, filters.ProjectId == null, () => ApplyProject(null));
                foreach (var candidate in _store.Projects)
                {
                    var captured = candidate.Id;
                    menu.Add(_strings.ProjectName(candidate.Name), filters.ProjectId == captured, () => ApplyProject(captured));
                }
                menu.MaxHeight = 420;
            }, "btnToDoFilterProject")));

            if (filters.IsActive)
            {
                var clear = StatsUi.Button(_strings.ClearFilters, (s, e) => _store.SetFilters(new ToDoFilters()), height: 30);
                clear.VerticalAlignment = VerticalAlignment.Bottom;
                clear.Margin = new Thickness(0, 0, 0, 6);
                AutomationProperties.SetAutomationId(clear, "btnToDoClearFilters");
                panel.Children.Add(clear);
            }
            var card = StatsUi.Card(panel, padding: new Thickness(14, 10, 14, 6));
            card.Margin = new Thickness(0, 0, 0, 10);
            return card;
        }

        private static FrameworkElement FilterColumn(string title, FrameworkElement control)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 18, 0) };
            var caption = StatsUi.Text(title, 11, FontWeights.SemiBold, secondary: true);
            caption.Margin = new Thickness(2, 0, 0, 4);
            stack.Children.Add(caption);
            stack.Children.Add(control);
            return stack;
        }

        private string PresetLabel(ToDoDueDatePreset preset)
        {
            switch (preset)
            {
                case ToDoDueDatePreset.Today: return _strings.DueToday;
                case ToDoDueDatePreset.ThisWeek: return _strings.DueThisWeek;
                case ToDoDueDatePreset.NextWeek: return _strings.DueNextWeek;
                default: return _strings.Next30Days;
            }
        }

        private string EnergyLabel(ToDoEnergyLevel? level)
        {
            if (!level.HasValue) return _strings.FilterAll;
            return level == ToDoEnergyLevel.Low
                ? _strings.LowSpoons + " · " + _strings.LowSpoonsSubtitle
                : _strings.FullSpoons + " · " + _strings.FullSpoonsSubtitle;
        }

        private void ApplyDuePreset(ToDoDueDatePreset? preset)
        {
            var filters = _store.CopyFilters();
            var range = preset.HasValue ? ToDoStore.DueDateRange(preset.Value, DateTime.Now) : null;
            filters.DueDateStart = range?.Item1;
            filters.DueDateEnd = range?.Item2;
            _store.SetFilters(filters);
        }

        private void ApplyEnergy(ToDoEnergyLevel? level)
        {
            var filters = _store.CopyFilters();
            filters.EnergyLevel = level;
            _store.SetFilters(filters);
        }

        private void ApplyProject(string projectId)
        {
            var filters = _store.CopyFilters();
            filters.ProjectId = projectId;
            _store.SetFilters(filters);
        }

        // ------------------------------------------------------------------ sections

        private void DrawList()
        {
            // A redraw replaces every row: keep keyboard focus on the row that had it (a selection redraws the
            // list, and the store's own refresh can redraw it again straight after).
            if (Keyboard.FocusedElement is ToDoRowCard focusedRow && _list.IsAncestorOf(focusedRow))
                _focusTaskId = focusedRow.TaskId;
            _list.Children.Clear();
            switch (_store.EmptyState)
            {
                case ToDoEmptyState.Loading:
                    _list.Children.Add(StatsUi.Loading());
                    return;
                case ToDoEmptyState.FirstTask:
                    _list.Children.Add(EmptyState(_strings.AddYourFirstTask, true));
                    return;
                case ToDoEmptyState.NoSearchMatch:
                    _list.Children.Add(EmptyState(_strings.CouldNotFindTasks, false));
                    return;
                case ToDoEmptyState.NoFilterMatch:
                    _list.Children.Add(EmptyState(_strings.NoTasksMatchFilters, false));
                    return;
            }
            foreach (var section in _store.Sections)
            {
                _list.Children.Add(SectionHeader(section));
                if (_store.ExpandedSectionId == section.Id) DrawSectionBody(section);
            }
        }

        private FrameworkElement EmptyState(string title, bool showNewTask)
        {
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 60, 0, 40) };
            var icon = StatsUi.Text(Glyph(0xE9D5), 36, secondary: true);
            icon.FontFamily = new FontFamily("Segoe MDL2 Assets");
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Children.Add(icon);
            var heading = StatsUi.Text(title, 15, FontWeights.SemiBold);
            heading.HorizontalAlignment = HorizontalAlignment.Center;
            heading.Margin = new Thickness(0, 10, 0, 12);
            AutomationProperties.SetAutomationId(heading, "txtToDoEmpty");
            stack.Children.Add(heading);
            if (showNewTask)
            {
                var button = StatsUi.Button("+  " + _strings.NewTask, (s, e) => OpenEditor(null), primary: true);
                button.HorizontalAlignment = HorizontalAlignment.Center;
                button.Margin = new Thickness(0);
                stack.Children.Add(button);
            }
            return stack;
        }

        private FrameworkElement SectionHeader(ToDoSection section)
        {
            bool isOpen = _store.ExpandedSectionId == section.Id;
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var chevron = StatsUi.Text(Glyph(isOpen ? 0xE70D : 0xE76C), 10, secondary: true, wrap: false);
            chevron.FontFamily = new FontFamily("Segoe MDL2 Assets");
            chevron.Width = 16;
            chevron.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(chevron);
            var title = StatsUi.Text(section.Title, 13, FontWeights.SemiBold, wrap: false);
            title.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(title);
            var count = StatsUi.Text(_store.TotalCount(section).ToString(), 11, FontWeights.SemiBold, secondary: true, wrap: false);
            AutomationProperties.SetAutomationId(count, "txtToDoSectionCount_" + section.Id);
            row.Children.Add(new Border { Child = count, Background = StatsUi.Tint(Neutral, 0.10), CornerRadius = new CornerRadius(8), Padding = new Thickness(7, 1, 7, 1), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });

            var card = new Border { Child = row, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 0, 0, 6), Cursor = Cursors.Hand };
            card.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
            var button = new Button { Content = card, Template = StatsUi.PlainTemplate(), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            button.Click += (s, e) => _store.ToggleSection(section);
            AutomationProperties.SetAutomationId(button, "btnToDoSection_" + section.Id);
            return button;
        }

        private void DrawSectionBody(ToDoSection section)
        {
            if (section.Tasks.Count == 0)
            {
                var empty = StatsUi.Text(_strings.SectionEmpty, 12, secondary: true);
                empty.Margin = new Thickness(12, 2, 0, 10);
                _list.Children.Add(empty);
                return;
            }
            foreach (var task in section.Tasks) _list.Children.Add(TaskRow(task));
            if (_store.LoadingMoreStatus.HasValue && _store.LoadingMoreStatus == section.Status)
            {
                _list.Children.Add(StatsUi.Loading());
            }
            else if (_store.HasMore(section))
            {
                var more = StatsUi.Button(_strings.LoadMore, async (s, e) => await _store.LoadNextPageAsync(section), height: 28);
                more.HorizontalAlignment = HorizontalAlignment.Center;
                more.Margin = new Thickness(0, 4, 0, 10);
                AutomationProperties.SetAutomationId(more, "btnToDoLoadMore_" + section.Id);
                _list.Children.Add(more);
            }
        }

        private void LoadMoreForOpenSection()
        {
            var open = _store.Sections.FirstOrDefault(section => section.Id == _store.ExpandedSectionId);
            if (open != null && _store.HasMore(open)) _ = _store.LoadNextPageAsync(open);
        }

        // ------------------------------------------------------------------ rows

        private FrameworkElement TaskRow(ToDoTask task)
        {
            bool selected = _store.SelectedTaskId == task.Id;
            bool updating = _store.UpdatingTaskIds.Contains(task.Id);

            var grid = new Grid { Margin = new Thickness(12, 9, 12, 9) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            FrameworkElement check;
            if (updating)
            {
                check = new ProgressBar { IsIndeterminate = true, Width = 18, Height = 3, Foreground = Accent, Background = Brushes.Transparent, BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Center };
            }
            else
            {
                string tip = task.IsCompleted ? _strings.MarkIncomplete : ToDoStrings.Format(_strings.MarkComplete, ToDoStrings.TitleToken, task.Title);
                var button = CompletionCheck(task.IsCompleted, tip, async (s, e) => await _store.ToggleCompleteAsync(task));
                AutomationProperties.SetAutomationId(button, "btnToDoCheck_" + task.Id);
                check = button;
            }
            check.Margin = new Thickness(0, 1, 10, 0);
            grid.Children.Add(check);

            var body = new StackPanel();
            Grid.SetColumn(body, 1);
            body.Children.Add(TitleLine(task));
            var metadata = MetadataLine(task);
            if (metadata.Children.Count > 0) body.Children.Add(metadata);
            grid.Children.Add(body);

            var metrics = Metrics(task);
            Grid.SetColumn(metrics, 2);
            grid.Children.Add(metrics);

            var content = new StackPanel();
            content.Children.Add(grid);
            if (selected) content.Children.Add(ActionBar(task, updating));

            var card = new ToDoRowCard
            {
                Child = content,
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(selected ? 2 : 1),
                Margin = new Thickness(0, 0, 0, 6),
                Opacity = task.IsCompleted && !selected ? 0.7 : 1,
                Background = Brushes.Transparent,
            };
            card.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
            if (selected) card.BorderBrush = Accent;
            else card.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            AutomationProperties.SetAutomationId(card, "rowToDo_" + task.Id);
            card.TaskId = task.Id;
            AutomationProperties.SetName(card, task.Title);
            AutomationProperties.SetItemStatus(card, selected ? _strings.Selected : "");
            // Keyboard: Tab reaches the row, Space selects it (showing the action bar), Enter opens the editor.
            card.Select = () => { _focusTaskId = task.Id; if (_store.SelectedTaskId != task.Id) { _confirmingDeleteId = null; _store.Select(task); } };
            card.Open = () => { _focusTaskId = task.Id; OpenEditor(task); };
            if (_focusTaskId == task.Id) card.Loaded += (s, e) => { _focusTaskId = null; card.Focus(); };

            // A click selects (and reveals the action bar), a double-click opens the editor. Buttons in the
            // row keep their own clicks: they handle the event before it bubbles here.
            grid.MouseLeftButtonUp += (s, e) =>
            {
                if (e.Handled) return;
                if (_store.SelectedTaskId != task.Id) { _confirmingDeleteId = null; _store.Select(task); }
            };
            grid.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2) { e.Handled = true; OpenEditor(task); }
            };
            card.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                RowMenu(card, task).Open();
            };
            return card;
        }

        /// <summary>The task whose row had keyboard focus when it was selected: its redrawn row takes focus back.</summary>
        private string _focusTaskId;

        private ToDoMenu RowMenu(UIElement card, ToDoTask task)
        {
            var menu = new ToDoMenu(card, PlacementMode.MousePoint);
            menu.Add(_strings.EditTaskTitle, false, () => OpenEditor(task), "menuToDoRowEdit");
            menu.Add(task.IsCompleted ? _strings.MarkIncomplete : ToDoStrings.Format(_strings.MarkComplete, ToDoStrings.TitleToken, task.Title), false,
                     () => _ = _store.ToggleCompleteAsync(task), "menuToDoRowComplete");
            menu.Add(_strings.Delete, false, () => { _confirmingDeleteId = task.Id; _store.Select(task); }, "menuToDoRowDelete");
            return menu;
        }

        private FrameworkElement TitleLine(ToDoTask task)
        {
            var line = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxHeight = 42, TextTrimming = TextTrimming.CharacterEllipsis };
            string emoji = EmojiIcon(task.Icon);
            if (emoji != null) line.Inlines.Add(new System.Windows.Documents.Run(emoji + " ") { FontFamily = EmojiFont });
            var title = new System.Windows.Documents.Run(task.Title) { FontSize = 14, FontWeight = FontWeights.Medium };
            if (task.IsCompleted) title.TextDecorations = TextDecorations.Strikethrough;
            line.Inlines.Add(title);
            line.SetResourceReference(TextBlock.ForegroundProperty, task.IsCompleted ? "TraySubtitleColor" : "ForegroundColor");
            return line;
        }

        /// <summary>Web <c>resolveTaskIconEmoji</c>: an emoji glyph, or the hex codepoints the web's picker stores.</summary>
        internal static string EmojiIcon(string raw)
        {
            string icon = (raw ?? "").Trim();
            if (icon.Length == 0) return null;
            var parts = icon.ToLowerInvariant().Split('-');
            bool looksLikeHex = parts.All(part => part.Length >= 4 && part.Length <= 6 && part.All(Uri.IsHexDigit));
            if (!looksLikeHex) return icon;
            var result = new StringBuilder();
            foreach (var part in parts)
            {
                int value = Convert.ToInt32(part, 16);
                if (value > 0x10FFFF || (value >= 0xD800 && value <= 0xDFFF)) return icon;
                result.Append(char.ConvertFromUtf32(value));
            }
            return result.ToString();
        }

        private WrapPanel MetadataLine(ToDoTask task)
        {
            var line = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            var projects = _store.ProjectNames(task);
            foreach (var name in projects.Take(MaxProjectChips)) line.Children.Add(Chip("#" + name, Accent));
            if (projects.Count > MaxProjectChips)
            {
                var more = Chip("+" + (projects.Count - MaxProjectChips));
                more.ToolTip = string.Join("\n", projects.Skip(MaxProjectChips));
                line.Children.Add(more);
            }
            if (task.DurationSeconds > 0) line.Children.Add(Chip(ToDoDates.DurationLabelFromSeconds(task.DurationSeconds), glyph: "⏱"));
            if (task.SubtaskList.Count > 0)
            {
                int done = task.SubtaskList.Count(subtask => subtask.IsCompleted);
                var chip = Chip(done + "/" + task.SubtaskList.Count + " " + _strings.Subtasks.ToLower(ToDoStrings.Culture), glyph: "☑");
                chip.ToolTip = string.Join("\n", task.SubtaskList.Select(subtask => "• " + subtask.Name));
                line.Children.Add(chip);
            }
            string details = ToDoHtml.PlainText(task.Details ?? "");
            if (!string.IsNullOrWhiteSpace(details))
            {
                var icon = StatsUi.Text(Glyph(0xE8A4), 11, secondary: true, wrap: false);
                icon.FontFamily = new FontFamily("Segoe MDL2 Assets");
                icon.VerticalAlignment = VerticalAlignment.Center;
                icon.Margin = new Thickness(0, 0, 6, 0);
                icon.ToolTip = details;
                line.Children.Add(icon);
            }
            string custom = _store.CustomStatusLabel(task.CustomStatusId);
            if (custom != null) line.Children.Add(Chip(custom));
            return line;
        }

        /// <summary>Web <c>TaskSortSegment</c>: Due · Created · TOP, the active sort key highlighted. Due edits the date.</summary>
        private FrameworkElement Metrics(ToDoTask task)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(10, 0, 0, 0) };
            var now = DateTime.Now;
            var state = ToDoDates.DueState(task.DueDate, task.IsCompleted, now);
            Brush tint = state == ToDoDueState.Overdue ? Overdue : state == ToDoDueState.Today ? TodayTint : null;
            string due = string.IsNullOrEmpty(task.DueDate) ? "—" : _strings.DueLabel(task.DueDate, now);
            var dueCell = Metric(_strings.DueShort, due, _store.SortMode == ToDoSortMode.DueDate, tint);
            var dueButton = new Button { Content = dueCell, Template = StatsUi.PlainTemplate(), Cursor = Cursors.Hand, ToolTip = _strings.EditDueDate, Background = Brushes.Transparent };
            dueButton.Click += (s, e) => ShowDuePopup(dueButton, task);
            AutomationProperties.SetAutomationId(dueButton, "btnToDoDue_" + task.Id);
            // "DUE Tomorrow"; with no date the cell shows "—", which a screen reader skips, so say "DUE No due date".
            AutomationProperties.SetName(dueButton, _strings.DueShort + " " + (string.IsNullOrEmpty(task.DueDate) ? _strings.NoDueDate : due));
            row.Children.Add(dueButton);
            row.Children.Add(Metric(_strings.CreatedShort, _strings.Abbreviated(task.CreatedAt), _store.SortMode == ToDoSortMode.Chronological, null));
            double score = ToDoScoring.TopScoreOf(task, now);
            var top = Metric(_strings.TopShort, score > 0 ? ToDoScoring.Rounded(score) : "—", _store.SortMode == ToDoSortMode.Smart, null);
            top.ToolTip = _strings.TopScoreTooltip;
            row.Children.Add(top);
            return row;
        }

        private static FrameworkElement Metric(string label, string value, bool active, Brush tint)
        {
            var stack = new StackPanel { MinWidth = 58, Margin = new Thickness(8, 0, 0, 0), Background = Brushes.Transparent };
            var caption = StatsUi.Text(label.ToUpperInvariant(), 9, FontWeights.SemiBold, secondary: true, wrap: false);
            caption.HorizontalAlignment = HorizontalAlignment.Right;
            stack.Children.Add(caption);
            var text = new TextBlock { Text = value, FontSize = 11, FontWeight = active || tint != null ? FontWeights.SemiBold : FontWeights.Normal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 1, 0, 0) };
            if (tint != null) text.Foreground = ToDoUi.TextTint(tint);
            else if (active) text.Foreground = ToDoUi.TextTint(Accent);
            else text.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            stack.Children.Add(text);
            return stack;
        }

        /// <summary>
        /// Beneath the selected row, inside its orange border: Snooze · Edit · Mark complete · Delete, which
        /// asks inline before it goes.
        /// </summary>
        private FrameworkElement ActionBar(ToDoTask task, bool updating)
        {
            var bar = new DockPanel { LastChildFill = false, Margin = new Thickness(12, 6, 12, 6) };
            if (_confirmingDeleteId == task.Id)
            {
                var question = StatsUi.Text(_strings.DeleteTaskConfirmation, 12, wrap: false);
                question.VerticalAlignment = VerticalAlignment.Center;
                DockPanel.SetDock(question, Dock.Left);
                bar.Children.Add(question);
                var delete = SmallButton(_strings.Delete, async (s, e) => { _confirmingDeleteId = null; await _store.DeleteAsync(task); }, primary: true, glyph: Glyph(0xE74D));
                AutomationProperties.SetAutomationId(delete, "btnToDoConfirmDelete");
                DockPanel.SetDock(delete, Dock.Right);
                bar.Children.Add(delete);
                var cancel = SmallButton(_strings.Cancel, (s, e) => { _confirmingDeleteId = null; QueueRedraw(); });
                DockPanel.SetDock(cancel, Dock.Right);
                bar.Children.Add(cancel);
            }
            else
            {
                Button snooze = null;
                snooze = SmallButton(_strings.Snooze, (s, e) => ShowDuePopup(snooze, task), glyph: Glyph(0xE823));
                var edit = SmallButton(_strings.Edit, (s, e) => OpenEditor(task), glyph: Glyph(0xE70F));
                var complete = SmallButton(task.IsCompleted ? _strings.MarkIncomplete : _strings.MarkCompleteShort,
                                           async (s, e) => await _store.ToggleCompleteAsync(task), glyph: Glyph(task.IsCompleted ? 0xE7A7 : 0xE73E));
                var delete = SmallButton(_strings.Delete, (s, e) => { _confirmingDeleteId = task.Id; QueueRedraw(); }, glyph: Glyph(0xE74D));
                AutomationProperties.SetAutomationId(snooze, "btnToDoSnooze");
                AutomationProperties.SetAutomationId(edit, "btnToDoEdit");
                AutomationProperties.SetAutomationId(complete, "btnToDoComplete");
                AutomationProperties.SetAutomationId(delete, "btnToDoDelete");
                foreach (var button in new[] { snooze, edit, complete, delete })
                {
                    button.IsEnabled = !updating;
                    DockPanel.SetDock(button, Dock.Left);
                    bar.Children.Add(button);
                }
            }
            return new Border { Child = bar, Background = StatsUi.Tint(Accent, 0.1), CornerRadius = new CornerRadius(0, 0, 6, 6) };
        }

        // ------------------------------------------------------------------ due date popover

        /// <summary>Web <c>DueDateSnoozePopover</c>: free text ("2d", "30th", "fri") with a live preview, or a preset.</summary>
        private void ShowDuePopup(FrameworkElement anchor, ToDoTask task)
        {
            _duePopup = ToDoDueDatePopup.Show(anchor, _strings, task.DueDate, day => _ = _store.SetDueDateAsync(task, day), "ToDoDue", withCalendar: false);
        }

        /// <summary>The open due-date popup, for tests.</summary>
        internal Popup _duePopup;

        // ------------------------------------------------------------------ editor

        private void OpenEditor(ToDoTask task) => OpenEditor(task, null);

        /// <summary>From a project page, a new task starts in that project and the page reloads after a save.</summary>
        private void OpenEditor(ToDoTask task, Project project)
        {
            string title = task == null && project == null ? _search.Text.Trim() : "";
            var editor = new ToDoEditorWindow(_store, task, title, task == null ? project : null) { Owner = Window.GetWindow(this) };
            editor.ShowDialog();
            if (editor.Committed && _projects.Detail != null) _projects.Detail.MarkChanged();
        }
    }

    /// <summary>
    /// A To-do row or project card: a Border that can take keyboard focus and has an automation peer (a plain
    /// Border has neither in .NET 4.7.2), so Tab reaches it, Narrator reads it as a list item named after the task
    /// or project, and Space / Enter or UIA Invoke select or open it.
    /// </summary>
    internal sealed class ToDoRowCard : Border
    {
        internal Action Select;
        internal Action Open;
        /// <summary>The task this row shows (the list keeps focus on it across redraws).</summary>
        internal string TaskId;

        public ToDoRowCard()
        {
            Focusable = true;
            KeyboardNavigation.SetIsTabStop(this, true);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (ReferenceEquals(e.OriginalSource, this))
            {
                if (e.Key == Key.Enter) { e.Handled = true; Open?.Invoke(); return; }
                if (e.Key == Key.Space) { e.Handled = true; Select?.Invoke(); return; }
            }
            base.OnKeyDown(e);
        }

        protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

        private sealed class RowPeer : System.Windows.Automation.Peers.FrameworkElementAutomationPeer,
                                       System.Windows.Automation.Provider.IInvokeProvider
        {
            public RowPeer(ToDoRowCard owner) : base(owner) { }

            protected override System.Windows.Automation.Peers.AutomationControlType GetAutomationControlTypeCore() =>
                System.Windows.Automation.Peers.AutomationControlType.ListItem;

            protected override string GetClassNameCore() => "ToDoRow";

            protected override bool IsKeyboardFocusableCore() => true;

            public override object GetPattern(System.Windows.Automation.Peers.PatternInterface patternInterface) =>
                patternInterface == System.Windows.Automation.Peers.PatternInterface.Invoke ? this : base.GetPattern(patternInterface);

            public void Invoke() => ((ToDoRowCard)Owner).Select?.Invoke();
        }
    }
}
