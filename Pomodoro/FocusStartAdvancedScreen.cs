using Focus_Bear.Habits.DayPlan;
using Focus_Bear.Models;
using Focus_Bear.Services;
using Focus_Bear.Utils;
using FocusBear.Core.Focus;
using FocusBear.Core.ToDo;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Focus_Bear.Pomodoro
{
    /// <summary>
    /// The advanced start screen (the Mac's <c>FocusStartAdvancedScreen</c>, Figma "0.1–0.3"): three accordion
    /// cards — how long, what to achieve (a goal, a project and real to-dos for the session queue), and which
    /// block list — with at most one open (the first on each view), the bear commenting on it, and Start Session pinned below.
    /// It starts sessions through <see cref="FocusSessionStarter"/>, so Teacher Mode, Super Focus, the #1420
    /// warning and the super-strict schedule lock behave as on the old screen.
    /// Not ported: the Mac's per-session strictness levels (Windows has one app-wide cuddly/grizzly setting).
    /// </summary>
    public sealed class FocusStartAdvancedScreen : UserControl
    {
        private const int TaskSearchDebounceMs = 300;
        private readonly FocusBearMainWindow _parent;
        private readonly AppSettings _settings = AppSettings.GetInstance;
        private readonly DispatcherTimer _taskSearch = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TaskSearchDebounceMs) };
        private readonly TextBox _goalBox = new TextBox();
        private readonly TextBox _taskBox = new TextBox();
        private readonly TextBox _blockListBox = new TextBox();

        // The open card, or null when every card is collapsed. At most one is open; the first opens on each view.
        private FocusStartCard? _expanded = FocusStartCard.Duration;
        private FocusStartDuration _duration = new FocusStartDuration(FocusStartDuration.DefaultMinutes);
        private bool _breakSettingsOpen;
        private bool _superFocus;
        private int _superFocusCount = 2;
        private string _goal = "";
        private Project _project;
        private bool _goalChosenByUser;
        private readonly List<ToDoTask> _selectedTasks = new List<ToDoTask>();
        private IList<Project> _projects = new List<Project>();
        private List<ToDoTask> _suggestions = new List<ToDoTask>();
        private int _taskPage;
        private bool _hasMoreTasks, _loadingTasks, _loadingProjects, _workChoicesFailed;
        private int _taskRequest;
        private FocusMode _mode;
        private double _factor;
        private bool _starting;
        private string _addingTask, _addTaskFailed;

        public FocusStartAdvancedScreen(FocusBearMainWindow parent)
        {
            _parent = parent;
            SetResourceReference(BackgroundProperty, "BackgroundColor");
            SizeChanged += (s, e) =>
            {
                double factor = new FocusStartScale(ActualWidth).Factor;
                if (Math.Abs(factor - _factor) > 0.03) Render();
            };
            _taskSearch.Tick += async (s, e) => { _taskSearch.Stop(); await LoadTasks(more: false); };
            _goalBox.TextChanged += (s, e) => RenderProjectsOnly();
            _goalBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; UseCustomGoal(); } };
            _taskBox.TextChanged += (s, e) => { _addTaskFailed = null; _taskSearch.Stop(); _taskSearch.Start(); };
            _taskBox.KeyDown += async (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; await AddTypedTask(); } };
            _blockListBox.TextChanged += (s, e) => RenderBlockListsOnly();
        }

        private FocusStartStrings Strings => new FocusStartStrings(_settings.ConfigFileStrings?["focusStartVcInfo"] as JObject);

        private bool IronFocus => BlockingScheduleManager.IsIronFocusScheduleActive();

        /// <summary>
        /// Called whenever the screen is shown. <paramref name="enterSuperPomodoroAfterMorningRoutine"/> is the
        /// automatic Super Focus after the morning routine; a Super Focus run in progress continues at once.
        /// </summary>
        public void UpdateValues(bool enterSuperPomodoroAfterMorningRoutine = false)
        {
            LogWriter.LogWrite("FocusStartAdvancedScreen UpdateValues()");
            try
            {
                BlockingScheduleManager.CheckCustomBlockingScheduleTime();
                _expanded = FocusStartCard.Duration;   // each view starts with the first card open, whatever was left open or collapsed
                _pageScroll = _blockListScroll = null;  // and from the top
                _duration = FocusStartDuration.FromStored(_settings.PomodoroMinutes);
                _settings.sRawTasks = "";
                _selectedTasks.Clear();
                _goal = "";
                _project = null;
                _goalChosenByUser = false;
                _goalBox.Text = "";
                _taskBox.Text = "";
                _addingTask = _addTaskFailed = null;
                _mode = InitialMode();

                if (enterSuperPomodoroAfterMorningRoutine)
                {
                    _settings.bUseSuperPomodoroMode = true;
                    _settings.sMinutesSuperPomodoro = _duration.Minutes.ToString();
                    _settings.sMinutesBreakPomodoro = FocusSessionStarter.BreakFromRatio(_duration.Minutes);
                    _settings.SuperPomodoroFocusedName = _settings.AutomaticSuperPomodoroFocusedName;
                    _settings.NumberOfSuperPomodorosToRun = _settings.AutomaticNumberOfSuperPomodorosToRun;
                    _settings.bEnterSuperPomodoroModeAfterMorningRoutine = false;
                }
                if (_settings.bUseSuperPomodoroMode)
                {
                    // A Super Focus run carries on with its own length and block list.
                    _superFocus = true;
                    _superFocusCount = _settings.NumberOfSuperPomodorosToRun;
                    _duration = FocusStartDuration.FromStored(_settings.sMinutesSuperPomodoro);
                    _mode = _settings.arrFocusModes.FirstOrDefault(m => m.name == _settings.SuperPomodoroFocusedName) ?? _mode;
                    Render();
                    FocusSessionStarter.Start(_mode, _duration.Minutes, _settings.sMinutesBreakPomodoro, _mode?.lastTaskUsed, personalSession: true);
                    return;
                }
                _superFocus = false;
                Render();
                _ = LoadWorkChoices();
            }
            catch (Exception ex) { LogWriter.LogWrite("FocusStartAdvancedScreen UpdateValues ex - " + ex.Message); }
        }

        private IEnumerable<FocusMode> BlockLists => _settings.arrFocusModes.Where(m => !m.isMeetings);

        private FocusMode InitialMode()
        {
            if (IronFocus)
            {
                string scheduled = _settings.ActiveSchedule?.FocusModeName;
                var locked = BlockLists.FirstOrDefault(m => m.name == scheduled);
                if (locked != null) return locked;
            }
            _settings.LoadMostRecentFocusMode();
            return BlockLists.FirstOrDefault(m => m.id == _settings.sMostRecentFocusmodeDoneID) ?? BlockLists.FirstOrDefault();
        }

        private bool AiEnabledFor(FocusMode mode) => mode != null && !mode.isDefault && !mode.isRelaxFocus && _settings.bAiCheckUrlEnabled;

        // ------------------------------------------------------------------ work choices

        private async Task LoadWorkChoices()
        {
            _loadingProjects = true;
            _workChoicesFailed = false;
            var projects = await new API.FocusBearAPIClient().GetAllActiveToDoProjects();
            _loadingProjects = false;
            if (projects == null) { _workChoicesFailed = true; if (_expanded == FocusStartCard.Goal) Render(); return; }
            var dayPlan = DayPlanProjectNames();
            _projects = FocusStartWorkSelection.Projects(projects, dayPlan);
            if (!_goalChosenByUser && _project == null)
            {
                // The top Day Plan project is the likely job for this session, until the user says otherwise.
                var top = FocusStartWorkSelection.TopDayPlanProject(projects, dayPlan);
                if (top != null) { _project = top; _goal = top.Name; }
            }
            Render();
            await LoadTasks(more: false);
        }

        private static List<string> DayPlanProjectNames()
        {
            try { return DayPlanTaskStore.Shared.Tasks(DateTime.Today).Where(t => !t.Done).Select(t => t.Project).ToList(); }
            catch (Exception ex) { LogWriter.LogWrite("FocusStart DayPlanProjectNames - " + ex.Message); return new List<string>(); }
        }

        private async Task LoadTasks(bool more)
        {
            int request = ++_taskRequest;
            _loadingTasks = true;
            string query = _taskBox.Text.Trim();
            var api = new API.FocusBearAPIClient();
            IList<ToDoTask> rows;
            bool hasMore = false;
            if (query.Length > 0) rows = await api.SearchToDos(query);
            else
            {
                // The list endpoint pages one status bucket at a time, as the To-Do screen reads it.
                int page = more ? _taskPage + 1 : 1;
                var doing = await api.GetToDoPage(page, ToDoStatus.InProgress, ToDoSortMode.Smart, null);
                var todo = await api.GetToDoPage(page, ToDoStatus.NotStarted, ToDoSortMode.Smart, null);
                rows = doing == null || todo == null ? null : doing.Tasks.Concat(todo.Tasks).ToList();
                if (rows != null) { _taskPage = page; hasMore = doing.HasNextPage || todo.HasNextPage; }
            }
            if (request != _taskRequest) return;
            _loadingTasks = false;
            if (rows == null) { _workChoicesFailed = true; RenderGoalOnly(); return; }
            _workChoicesFailed = false;
            var pool = more ? _suggestions.Concat(rows) : rows;
            _suggestions = FocusStartWorkSelection.Tasks(pool.GroupBy(t => t.Id).Select(g => g.First()), _project).ToList();
            _hasMoreTasks = hasMore;
            RenderGoalOnly();
        }

        private void UseCustomGoal()
        {
            string text = _goalBox.Text.Trim();
            if (text.Length == 0) return;
            _goal = text;
            _project = null;
            _goalChosenByUser = true;
            _goalBox.Text = "";
            Render();
        }

        private void SelectProject(Project project)
        {
            _project = project;
            _goal = project.Name;
            _goalChosenByUser = true;
            _goalBox.Text = "";
            _suggestions = FocusStartWorkSelection.Tasks(_suggestions, _project).ToList();
            Render();
        }

        private void ClearGoal()
        {
            _project = null;
            _goal = "";
            _goalChosenByUser = true;
            Render();
        }

        private void EditGoal()
        {
            _goalBox.Text = _goal;
            _project = null;
            _goal = "";
            _goalChosenByUser = true;
            Render();
            _goalBox.Focus();
            _goalBox.CaretIndex = _goalBox.Text.Length;
        }

        // ------------------------------------------------------------------ render

        private StackPanel _projectsHost, _goalHost, _blockListsHost;

        // Every Render() rebuilds the page, so the scroll positions are carried over to the new scroll viewers:
        // picking a block list near the bottom used to jump both the list and the page back to the top.
        private ScrollViewer _pageScroll, _blockListScroll;
        private double _blockListOffset;

        private void Render()
        {
            double pageOffset = _pageScroll?.VerticalOffset ?? 0;
            _blockListOffset = _blockListScroll?.VerticalOffset ?? 0;
            _blockListScroll = null;   // set again by BlockingBody when that card is open

            var p = FocusStartPalette.Current();
            double width = ActualWidth > 0 ? ActualWidth : 1100;
            var s = new FocusStartScale(width);
            _factor = s.Factor;
            var strings = Strings;
            double column = s.Pt(1367);

            var centre = new StackPanel { Width = column };
            centre.Children.Add(Title(strings, p, s));
            int breaks = FocusStartWorkSelection.BreakCount(_duration, !_settings.bSkipMicroBreaksDuringFocus, _settings.breakAfterMinutes);
            centre.Children.Add(Spaced(FocusStartUi.AccordionCard(0xE916, strings["titleDurationCard"], strings.DurationSummary(_duration, breaks),
                _expanded == FocusStartCard.Duration, DurationBody(strings, p, s), p, s, () => Toggle(FocusStartCard.Duration), "cardFocusStartDuration"), s));
            _goalHost = new StackPanel();
            centre.Children.Add(Spaced(FocusStartUi.AccordionCard(0xE734, strings["titleGoalCard"], strings.GoalSummary(_goal, _selectedTasks.Count),
                _expanded == FocusStartCard.Goal, _expanded == FocusStartCard.Goal ? GoalBody(strings, p, s) : null, p, s, () => Toggle(FocusStartCard.Goal), "cardFocusStartGoal"), s));
            centre.Children.Add(Spaced(FocusStartUi.AccordionCard(0xE8F8, strings["titleBlockingCard"], strings.BlockingSummary(_mode?.name, AiEnabledFor(_mode)),
                _expanded == FocusStartCard.Blocking, _expanded == FocusStartCard.Blocking ? BlockingBody(strings, p, s) : null, p, s, () => Toggle(FocusStartCard.Blocking), "cardFocusStartBlocking"), s));
            if (_superFocus && _settings.bUseSuperPomodoroMode && _settings.nSuperPomodoroCountCompleted < _settings.NumberOfSuperPomodorosToRun)
            {
                string progress = (_settings.ConfigFileStrings?["pomodoroInfo"]?["completedPomodoroCountText"]?.ToString() ?? "")
                    .Replace("<COMPLETED_POMODORO>", _settings.nSuperPomodoroCountCompleted.ToString())
                    .Replace("<TOTAL_POMODORO>", _settings.NumberOfSuperPomodorosToRun.ToString())
                    .Replace("<REMAINING_POMODORO>", (_settings.NumberOfSuperPomodorosToRun - _settings.nSuperPomodoroCountCompleted).ToString());
                var line = FocusStartUi.Text(progress, s.Pt(36), p.Secondary);
                line.TextAlignment = TextAlignment.Center;
                centre.Children.Add(line);
            }

            var columns = new Grid();
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(centre, 1);
            columns.Children.Add(centre);
            double side = (width - column - s.Pt(48)) / 2;
            if (side >= s.Pt(706) * 0.45)
            {
                // The bear shrinks into the side column rather than crowd the cards; it hides when there is no room.
                var bearScale = new FocusStartScale(width * Math.Min(1, side / s.Pt(706)));
                // With every card collapsed the bear stays by the first card and keeps its comment.
                FocusStartCard bearCard = _expanded ?? FocusStartCard.Duration;
                string bubble = bearCard == FocusStartCard.Duration ? strings["bubbleDuration"] : bearCard == FocusStartCard.Goal ? strings["bubbleGoal"] : strings["bubbleBlocking"];
                var bear = FocusStartUi.Bear("focusStartBearAdvanced", bubble, p, bearScale, bubbleBeside: false, bearWidthPx: 533, bubbleAutomationId: "txtFocusStartBubble");
                bear.Margin = new Thickness(s.Pt(24), s.Pt(200) + (int)bearCard * s.Pt(180), 0, 0);
                bear.VerticalAlignment = VerticalAlignment.Top;
                Grid.SetColumn(bear, 2);
                columns.Children.Add(bear);
            }

            var page = new StackPanel { Margin = new Thickness(0, 0, 0, s.Pt(48)) };
            var link = FocusStartUi.ModeLink(strings["linkSimpleTop"], strings["linkSimpleBottom"], true, p, s, SwitchToSimple);
            link.HorizontalAlignment = HorizontalAlignment.Left;
            link.Margin = new Thickness(s.Pt(60), s.Pt(48), 0, 0);
            page.Children.Add(link);
            columns.Margin = new Thickness(0, s.Pt(14), 0, 0);
            page.Children.Add(columns);

            // While a session is being started (a teacher's class session waits for the server) the button says so.
            var start = FocusStartUi.Primary(_starting ? strings["buttonStartingSession"] : strings["buttonStartSession"], p, s, async () => await StartSession(), "btnFocusStartStart", busy: _starting);
            start.Width = column;
            start.IsEnabled = !_starting && _mode != null && _duration.EffectiveMinutes >= 1;
            double footerTop = s.Pt(32), footerBottom = s.Pt(40);
            var footer = new Border { Child = start, Padding = new Thickness(0, footerTop, 0, footerBottom) };
            footer.SetResourceReference(Border.BackgroundProperty, "BackgroundColor");

            // A grid, page first and footer second: the footer was docked first, which put Start Session first in
            // the Tab order, before the page above it.
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var scroll = new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            // The cards are centred in the page minus its scrollbar, the button in the full width: keep the
            // scrollbar's width free beside the button too, so it sits exactly under the cards.
            scroll.ScrollChanged += (o, e) =>
            {
                double bar = Math.Max(0, scroll.ActualWidth - scroll.ViewportWidth);
                if (Math.Abs(footer.Padding.Right - bar) > 0.5) footer.Padding = new Thickness(0, footerTop, bar, footerBottom);
            };
            if (pageOffset > 0) scroll.Loaded += (o, e) => scroll.ScrollToVerticalOffset(pageOffset);
            _pageScroll = scroll;
            root.Children.Add(scroll);
            Grid.SetRow(footer, 1);
            root.Children.Add(footer);
            Content = root;
        }

        private static FrameworkElement Spaced(FrameworkElement card, FocusStartScale s)
        {
            card.Margin = new Thickness(0, 0, 0, s.Pt(51));
            return card;
        }

        private FrameworkElement Title(FocusStartStrings strings, FocusStartPalette p, FocusStartScale s)
        {
            var title = new TextBlock { FontSize = s.Pt(96), FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.NoWrap };
            title.Inlines.Add(new System.Windows.Documents.Run(strings["titleAdvancedPrefix"]) { Foreground = p.Primary });
            title.Inlines.Add(new System.Windows.Documents.Run(strings["titleAdvancedEmphasis"]) { Foreground = p.Accent });
            title.Inlines.Add(new System.Windows.Documents.Run(strings["titleAdvancedSuffix"]) { Foreground = p.Primary });
            AutomationProperties.SetAutomationId(title, "txtFocusStartTitle");
            AutomationProperties.SetName(title, strings["titleAdvancedPrefix"] + strings["titleAdvancedEmphasis"] + strings["titleAdvancedSuffix"]);
            // One line, shrinking to fit the column (the Mac's lineLimit(1) + minimumScaleFactor).
            return new Viewbox { Child = title, StretchDirection = StretchDirection.DownOnly, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 0, s.Pt(76)) };
        }

        private void Toggle(FocusStartCard card)
        {
            // Opening a card closes the others; clicking the open one collapses it, so all three can be closed.
            _expanded = _expanded == card ? (FocusStartCard?)null : card;
            Render();
        }

        // ------------------------------------------------------------------ duration card

        private UIElement DurationBody(FocusStartStrings strings, FocusStartPalette p, FocusStartScale s)
        {
            var body = new StackPanel();
            var dial = new FocusStartDurationDial(_duration, strings, p, s) { IsEnabled = !_settings.bUseSuperPomodoroMode };
            dial.DurationChanged += d => _duration = d;
            body.Children.Add(dial);

            body.Children.Add(LabelledRow(strings["labelUntimedMode"], FocusStartUi.Toggle(_duration.IsUntimed, p, s, on =>
            {
                _duration = _duration.WithUntimed(on);
                Render();
            }, "tglFocusStartUntimed"), p, s));

            // Break settings: collapsed by default, its header still says what the session will do.
            bool microBreaks = !_settings.bSkipMicroBreaksDuringFocus;
            var header = new DockPanel();
            // Same arrows as the cards around it: ">" when collapsed, a down arrow when expanded.
            var chevron = FocusStartUi.Glyph(_breakSettingsOpen ? 0xE70D : 0xE76C, s.Pt(36) + 1, p.Primary);
            DockPanel.SetDock(chevron, Dock.Right);
            header.Children.Add(chevron);
            var headerText = new StackPanel();
            var headerTitle = new StackPanel { Orientation = Orientation.Horizontal };
            var pause = FocusStartUi.Glyph(0xE769, s.Pt(40), p.Accent);
            pause.Margin = new Thickness(0, 0, s.Pt(16), 0);
            headerTitle.Children.Add(pause);
            headerTitle.Children.Add(FocusStartUi.Text(strings["titleBreakSettings"], s.Pt(48), p.Primary, FontWeights.Medium, wrap: false));
            headerText.Children.Add(headerTitle);
            var summary = FocusStartUi.Text(microBreaks ? strings["summaryMicroBreaksOn"] : strings["summaryMicroBreaksOff"], s.Pt(36), p.Secondary);
            headerText.Children.Add(summary);
            header.Children.Add(headerText);
            var section = new StackPanel();
            section.Children.Add(FocusStartUi.Clickable(header, () => { _breakSettingsOpen = !_breakSettingsOpen; Render(); }, "btnFocusStartBreakSettings", strings["titleBreakSettings"]));
            if (_breakSettingsOpen)
            {
                var toggle = FocusStartUi.Toggle(microBreaks, p, s, on =>
                {
                    // The same app-wide setting the old screen's "Micro-breaks on/off" link toggled.
                    _settings.bSkipMicroBreaksDuringFocus = !on;
                    Render();
                }, "tglFocusStartMicroBreaks");
                toggle.IsEnabled = !_settings.bUseSuperPomodoroMode;
                section.Children.Add(LabelledRow(strings["labelEnableMicroBreaks"], toggle, p, s));
                if (microBreaks && _settings.breakAfterMinutes > 0)
                    section.Children.Add(Margin(FocusStartUi.Text(strings.WithCount("summaryBreakFrequency", _settings.breakAfterMinutes)
                        .Replace("<COUNT>", _settings.breakAfterMinutes.ToString(CultureInfo.CurrentCulture)), s.Pt(36), p.Primary), 0, s.Pt(16), 0, 0));
                section.Children.Add(Margin(FocusStartUi.Text(strings["explainerMicroBreaks"], s.Pt(36), p.Secondary), 0, s.Pt(16), 0, 0));
                var ratio = FocusStartUi.Text((_settings.ConfigFileStrings?["pomodoroInfo"]?["workRatioText"]?.ToString() ?? "")
                    .Replace("<WORKRATIO>", _settings.PomodoroWorkToBreakRatio.ToString()).Replace("<BREAKRATIO>", _settings.PomodoroBreakRatio.ToString()), s.Pt(36), p.Secondary);
                var edit = FocusStartUi.Clickable(FocusStartUi.Text(_settings.ConfigFileStrings?["pomodoroInfo"]?["buttonEditRatioText"]?.ToString() ?? "Edit", s.Pt(36), p.Accent, FontWeights.Medium, wrap: false),
                    FocusSessionStarter.OpenBreakRatioSettings, "btnFocusStartEditRatio");
                var ratioRow = new WrapPanel { Margin = new Thickness(0, s.Pt(16), 0, 0) };
                ratio.Margin = new Thickness(0, 0, s.Pt(16), 0);
                ratioRow.Children.Add(ratio);
                ratioRow.Children.Add(edit);
                section.Children.Add(ratioRow);
            }
            body.Children.Add(Margin(new Border
            {
                Child = section,
                Background = p.Field,
                BorderBrush = p.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(s.Pt(34)),
                Padding = new Thickness(s.Pt(42)),
            }, 0, s.Pt(41), 0, 0));

            // Super Focus: several sessions back to back with the same block list (the old "Super Pomodoro").
            var superToggle = FocusStartUi.Toggle(_superFocus, p, s, on =>
            {
                if (on && !_settings.bUseSuperPomodoroMode)
                {
                    _parent.GridShade.Visibility = Visibility.Visible;
                    var warn = new FBMessageBox(_settings.ConfigFileStrings["pomodoroInfo"]["alertWarningPaswwordSetText"].ToString()) { Owner = _parent };
                    warn.ShowDialog();
                    _parent.GridShade.Visibility = Visibility.Collapsed;
                }
                _superFocus = on;
                Render();
            }, "tglFocusStartSuperFocus");
            superToggle.IsEnabled = !_settings.bUseSuperPomodoroMode;
            var superRow = new StackPanel { Orientation = Orientation.Horizontal };
            superRow.Children.Add(superToggle);
            if (_superFocus)
            {
                var count = new ComboBox { Width = Math.Max(56, s.Pt(150)), Margin = new Thickness(s.Pt(24), 0, s.Pt(16), 0), IsEnabled = !_settings.bUseSuperPomodoroMode, VerticalAlignment = VerticalAlignment.Center };
                for (int i = 2; i <= 20; i++) count.Items.Add(i);
                count.SelectedItem = Math.Max(2, Math.Min(20, _superFocusCount));
                count.SelectionChanged += (o, e) => { if (count.SelectedItem is int n) _superFocusCount = n; };
                AutomationProperties.SetAutomationId(count, "cmbFocusStartSuperFocusCount");
                superRow.Children.Add(count);
                var unit = FocusStartUi.Text(strings["unitSuperFocusSessions"], s.Pt(44), p.Primary, wrap: false);
                unit.VerticalAlignment = VerticalAlignment.Center;
                superRow.Children.Add(unit);
            }
            body.Children.Add(LabelledRow(strings["labelSuperFocus"], superRow, p, s));
            return body;
        }

        private static FrameworkElement LabelledRow(string label, UIElement control, FocusStartPalette p, FocusStartScale s)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, s.Pt(41), 0, 0) };
            var text = FocusStartUi.Text(label, s.Pt(44), p.Primary, wrap: false);
            text.VerticalAlignment = VerticalAlignment.Center;
            text.Margin = new Thickness(0, 0, s.Pt(24), 0);
            row.Children.Add(text);
            row.Children.Add(control);
            // The pill toggles draw no text, so a screen reader would say only "toggle button": name them after the row.
            var toggle = control as ToggleButton ?? (control as Panel)?.Children.OfType<ToggleButton>().FirstOrDefault();
            if (toggle != null && string.IsNullOrEmpty(AutomationProperties.GetName(toggle)))
                AutomationProperties.SetName(toggle, (label ?? "").Trim().TrimEnd(':').Trim());
            return row;
        }

        private static FrameworkElement Margin(FrameworkElement element, double left, double top, double right, double bottom)
        {
            element.Margin = new Thickness(left, top, right, bottom);
            return element;
        }

        // ------------------------------------------------------------------ goal card

        private UIElement GoalBody(FocusStartStrings strings, FocusStartPalette p, FocusStartScale s)
        {
            _goalHost = new StackPanel();
            FillGoal(strings, p, s);
            return _goalHost;
        }

        private void RenderGoalOnly()
        {
            if (_goalHost == null || _expanded != FocusStartCard.Goal) return;
            FillGoal(Strings, FocusStartPalette.Current(), new FocusStartScale(ActualWidth > 0 ? ActualWidth : 1100));
        }

        private void RenderProjectsOnly()
        {
            if (_projectsHost == null) return;
            FillProjects(Strings, FocusStartPalette.Current(), new FocusStartScale(ActualWidth > 0 ? ActualWidth : 1100));
        }

        private void FillGoal(FocusStartStrings strings, FocusStartPalette p, FocusStartScale s)
        {
            Detach(_goalBox);
            Detach(_taskBox);
            _goalHost.Children.Clear();
            if (_goal.Length == 0)
            {
                var row = new DockPanel();
                var add = FocusStartUi.Clickable(FocusStartUi.Glyph(0xE710, s.Pt(40), p.Accent), UseCustomGoal, "btnFocusStartUseGoal", strings["buttonUseGoal"]);
                add.Margin = new Thickness(s.Pt(18), 0, 0, 0);
                DockPanel.SetDock(add, Dock.Right);
                row.Children.Add(add);
                row.Children.Add(FocusStartUi.SearchField(_goalBox, strings["placeholderGoal"], p, s, "txtFocusStartGoal"));
                _goalHost.Children.Add(row);
                _projectsHost = new StackPanel();
                _goalHost.Children.Add(_projectsHost);
                FillProjects(strings, p, s);
            }
            else
            {
                _projectsHost = null;
                _goalHost.Children.Add(SelectedGoal(strings, p, s));
            }

            var tasksLabel = FocusStartUi.Text(strings["labelTasks"], s.Pt(44), p.Primary);
            tasksLabel.Margin = new Thickness(0, s.Pt(41), 0, s.Pt(20));
            _goalHost.Children.Add(tasksLabel);
            foreach (var task in _selectedTasks.ToList())
            {
                var captured = task;
                _goalHost.Children.Add(TaskCard(task, p, s, 0xE711, strings["buttonRemoveTask"], () => { _selectedTasks.Remove(captured); Render(); }, "btnFocusStartRemoveTask"));
            }
            _goalHost.Children.Add(FocusStartUi.SearchField(_taskBox, strings["placeholderTask"], p, s, "txtFocusStartTaskSearch"));

            var list = new StackPanel { Margin = new Thickness(0, s.Pt(16), 0, 0) };
            var chosen = new HashSet<string>(_selectedTasks.Select(t => t.Id));
            string newTitle = _loadingTasks ? null : FocusStartWorkSelection.NewTaskTitle(_taskBox.Text, _suggestions.Concat(_selectedTasks));
            if (_addingTask != null)
                _goalHost.Children.Add(Margin(FocusStartUi.Text(strings["addingNewTask"].Replace("<NAME>", _addingTask), s.Pt(36), p.Secondary), 0, s.Pt(16), 0, 0));
            else if (newTitle != null)
                _goalHost.Children.Add(Margin(NewTaskRow(newTitle, strings, p, s), 0, s.Pt(16), 0, 0));
            if (_addTaskFailed != null && _addingTask == null)
            {
                var failed = FocusStartUi.Text(strings["couldNotAddNewTask"].Replace("<NAME>", _addTaskFailed), s.Pt(36), p.Warning);
                AutomationProperties.SetAutomationId(failed, "txtFocusStartAddTaskFailed");
                _goalHost.Children.Add(Margin(failed, 0, s.Pt(12), 0, 0));
            }
            foreach (var task in _suggestions.Where(t => !chosen.Contains(t.Id)).Take(40))
            {
                var captured = task;
                list.Children.Add(TaskCard(task, p, s, 0xE710, strings["labelTasks"], () =>
                {
                    _selectedTasks.Add(captured);
                    _taskBox.Text = "";
                    Render();
                }, "btnFocusStartAddTask", suggestion: true));
            }
            _goalHost.Children.Add(new ScrollViewer { Content = list, MaxHeight = Math.Max(160, s.Pt(4 * 150)), VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            if (_loadingTasks) _goalHost.Children.Add(FocusStartUi.Text("…", s.Pt(36), p.Secondary));
            else if (_workChoicesFailed)
            {
                _goalHost.Children.Add(FocusStartUi.Text(strings["workChoicesUnavailable"], s.Pt(36), p.Warning));
                _goalHost.Children.Add(FocusStartUi.Clickable(FocusStartUi.Text(strings["buttonRetryWorkChoices"], s.Pt(36), p.Accent, FontWeights.Medium), () => _ = LoadWorkChoices(), "btnFocusStartRetry"));
            }
            else if (_suggestions.All(t => chosen.Contains(t.Id)) && newTitle == null && _addingTask == null) _goalHost.Children.Add(FocusStartUi.Text(strings["noMatchingTasks"], s.Pt(36), p.Secondary));
            if (_hasMoreTasks && _taskBox.Text.Trim().Length == 0 && !_loadingTasks)
                _goalHost.Children.Add(Margin(FocusStartUi.Clickable(FocusStartUi.Text(strings["buttonMoreTasks"], s.Pt(36), p.Accent, FontWeights.Medium), () => _ = LoadTasks(more: true), "btnFocusStartMoreTasks"), 0, s.Pt(12), 0, 0));
        }

        /// <summary>"+ Add “windows app coding” as a new task" — the typed search, offered as a task of its own.</summary>
        private FrameworkElement NewTaskRow(string title, FocusStartStrings strings, FocusStartPalette p, FocusStartScale s)
        {
            var row = new DockPanel();
            var plus = FocusStartUi.Glyph(0xE710, s.Pt(32), p.Accent);
            plus.Margin = new Thickness(0, 0, s.Pt(18), 0);
            DockPanel.SetDock(plus, Dock.Left);
            row.Children.Add(plus);
            var enter = FocusStartUi.Text("Enter ↵", s.Pt(30), p.Secondary, wrap: false);
            enter.VerticalAlignment = VerticalAlignment.Center;
            enter.Margin = new Thickness(s.Pt(18), 0, 0, 0);
            DockPanel.SetDock(enter, Dock.Right);
            row.Children.Add(enter);
            var label = FocusStartUi.Text(strings["buttonAddNewTask"].Replace("<NAME>", title), s.Pt(36), p.Accent, FontWeights.SemiBold);
            label.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(label);
            var face = new Border
            {
                Child = row,
                Background = p.Field,
                BorderBrush = p.Accent,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(s.Pt(20)),
                Padding = new Thickness(s.Pt(22)),
            };
            var button = FocusStartUi.Clickable(face, () => _ = CreateTypedTask(title), "btnFocusStartAddNewTask", label.Text);
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            return button;
        }

        /// <summary>
        /// Enter in the task search: the task titled exactly as typed if there is one, otherwise the text as a
        /// new task. The search is re-run first so the choice is made against this text's results, not the
        /// previous keystroke's.
        /// </summary>
        private async Task AddTypedTask()
        {
            string typed = _taskBox.Text.Trim();
            if (typed.Length == 0 || _addingTask != null) return;
            _taskSearch.Stop();
            await LoadTasks(more: false);
            if (_taskBox.Text.Trim() != typed || _workChoicesFailed) return;
            var chosen = new HashSet<string>(_selectedTasks.Select(t => t.Id));
            var match = FocusStartWorkSelection.ExactMatch(typed, _suggestions.Where(t => !chosen.Contains(t.Id)));
            if (match != null) { _selectedTasks.Add(match); _taskBox.Text = ""; RenderKeepingTaskFocus(); return; }
            if (FocusStartWorkSelection.ExactMatch(typed, _selectedTasks) != null) { _taskBox.Text = ""; RenderKeepingTaskFocus(); return; }
            await CreateTypedTask(typed);
        }

        /// <summary>
        /// Creates the to-do on the server at once (<c>PUT /to-do</c>, as the Mac's in-session "Create new task"
        /// and menu quick-add do) and puts it in this session's tasks. Due today, in the goal's project if any.
        /// </summary>
        private async Task CreateTypedTask(string title)
        {
            if (_addingTask != null || string.IsNullOrWhiteSpace(title)) return;
            _addingTask = title.Trim();
            _addTaskFailed = null;
            RenderGoalOnly();
            ToDoTask saved = null;
            try
            {
                var draft = FocusStartWorkSelection.NewSessionTask(_addingTask, DateTime.Now, _project);
                var response = await new API.FocusBearAPIClient().UpsertToDo(ToDoUpsertBody.Make(draft));
                if (response != null && response.IsSuccess) saved = ToDoJson.ReadTask(response.Body) ?? draft;
                else LogWriter.LogWrite("FocusStartAdvancedScreen - new task not saved");
            }
            catch (Exception ex) { LogWriter.LogWrite("FocusStartAdvancedScreen CreateTypedTask ex - " + ex.Message); }
            string created = _addingTask;
            _addingTask = null;
            if (saved == null) { _addTaskFailed = created; RenderGoalOnly(); return; }
            LogWriter.LogWrite("FocusStartAdvancedScreen - added a new task to the session");
            if (_selectedTasks.All(t => t.Id != saved.Id)) _selectedTasks.Add(saved);
            if (_taskBox.Text.Trim() == created) _taskBox.Text = "";
            RenderKeepingTaskFocus();
        }

        /// <summary>Re-renders and puts the caret back in the task search, so several tasks can be typed in a row.</summary>
        private void RenderKeepingTaskFocus()
        {
            Render();
            Dispatcher.BeginInvoke(new Action(() => { _taskBox.Focus(); _taskBox.CaretIndex = _taskBox.Text.Length; }), DispatcherPriority.Input);
        }

        private static void Detach(TextBox box)
        {
            if (box.Parent is Panel panel) panel.Children.Remove(box);
        }

        private void FillProjects(FocusStartStrings strings, FocusStartPalette p, FocusStartScale s)
        {
            _projectsHost.Children.Clear();
            string query = _goalBox.Text.Trim();
            var visible = _projects.Where(pr => query.Length == 0 || (pr.Name ?? "").IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
                .Take(FocusStartWorkSelection.MaxProjectChips).ToList();
            if (_loadingProjects) _projectsHost.Children.Add(FocusStartUi.Text("…", s.Pt(36), p.Secondary));
            if (visible.Count == 0) return;
            var heading = FocusStartUi.Text(strings["headingProjects"], s.Pt(36), p.Secondary, FontWeights.SemiBold);
            heading.Margin = new Thickness(0, s.Pt(27), 0, s.Pt(12));
            _projectsHost.Children.Add(heading);
            var chips = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            foreach (var project in visible)
            {
                var captured = project;
                var inner = new StackPanel { Orientation = Orientation.Horizontal };
                if (!string.IsNullOrEmpty(project.Icon))
                {
                    var icon = FocusStartUi.Text(project.Icon, s.Pt(40), p.Primary, wrap: false);
                    icon.FontFamily = FocusStartUi.Emoji;
                    icon.Margin = new Thickness(0, 0, s.Pt(14), 0);
                    icon.VerticalAlignment = VerticalAlignment.Center;
                    inner.Children.Add(icon);
                }
                var name = FocusStartUi.Text(project.Name, s.Pt(36), p.Primary, wrap: false);
                name.VerticalAlignment = VerticalAlignment.Center;
                name.MaxWidth = s.Pt(420);
                inner.Children.Add(name);
                var plus = FocusStartUi.Glyph(0xE710, s.Pt(28), p.Accent);
                plus.Margin = new Thickness(s.Pt(14), 0, 0, 0);
                inner.Children.Add(plus);
                var chip = new Border { Child = inner, Background = p.Field, BorderBrush = p.Border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(s.Pt(12)), Padding = new Thickness(s.Pt(22)), Margin = new Thickness(0, 0, s.Pt(18), 0) };
                var button = FocusStartUi.Clickable(chip, () => SelectProject(captured), "btnFocusStartProject", project.Name);
                chips.Children.Add(button);
            }
            _projectsHost.Children.Add(new ScrollViewer { Content = chips, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        }

        private FrameworkElement SelectedGoal(FocusStartStrings strings, FocusStartPalette p, FocusStartScale s)
        {
            var row = new DockPanel();
            var clear = FocusStartUi.Clickable(FocusStartUi.Glyph(0xE711, s.Pt(36), p.Accent), ClearGoal, "btnFocusStartClearGoal", strings["buttonClearGoal"]);
            clear.Margin = new Thickness(s.Pt(24), 0, 0, 0);
            DockPanel.SetDock(clear, Dock.Right);
            row.Children.Add(clear);
            var edit = FocusStartUi.Clickable(FocusStartUi.Glyph(0xE70F, s.Pt(36), p.Accent), EditGoal, "btnFocusStartEditGoal", strings["buttonChangeGoal"]);
            edit.Margin = new Thickness(s.Pt(24), 0, 0, 0);
            DockPanel.SetDock(edit, Dock.Right);
            row.Children.Add(edit);
            var text = new StackPanel { Orientation = Orientation.Horizontal };
            if (_project != null && !string.IsNullOrEmpty(_project.Icon))
            {
                var icon = FocusStartUi.Text(_project.Icon, s.Pt(40), p.Primary, wrap: false);
                icon.FontFamily = FocusStartUi.Emoji;
                icon.Margin = new Thickness(0, 0, s.Pt(18), 0);
                text.Children.Add(icon);
            }
            var goal = FocusStartUi.Text(_goal, s.Pt(40), p.Primary, FontWeights.Medium);
            AutomationProperties.SetAutomationId(goal, "txtFocusStartSelectedGoal");
            text.Children.Add(goal);
            row.Children.Add(text);
            return new Border { Child = row, Background = p.Field, BorderBrush = p.Border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(s.Pt(20)), Padding = new Thickness(s.Pt(24)) };
        }

        private static FrameworkElement TaskCard(ToDoTask task, FocusStartPalette p, FocusStartScale s, int actionGlyph, string actionLabel, Action action, string automationId, bool suggestion = false)
        {
            var row = new DockPanel();
            var icon = FocusStartUi.Glyph(actionGlyph, s.Pt(32), p.Accent);
            icon.Margin = new Thickness(s.Pt(18), 0, 0, 0);
            DockPanel.SetDock(icon, Dock.Right);
            row.Children.Add(icon);
            var text = new StackPanel();
            var title = new StackPanel { Orientation = Orientation.Horizontal };
            if (!string.IsNullOrEmpty(task.Icon))
            {
                var emoji = FocusStartUi.Text(task.Icon, s.Pt(36), p.Primary, wrap: false);
                emoji.FontFamily = FocusStartUi.Emoji;
                emoji.Margin = new Thickness(0, 0, s.Pt(12), 0);
                title.Children.Add(emoji);
            }
            var name = FocusStartUi.Text(task.Title, s.Pt(36), p.Primary);
            title.Children.Add(name);
            text.Children.Add(title);
            var meta = new WrapPanel { Margin = new Thickness(0, s.Pt(8), 0, 0) };
            if (task.TopScore.HasValue && !double.IsNaN(task.TopScore.Value))
                meta.Children.Add(FocusStartUi.Badge("⚡ TOP " + task.TopScore.Value.ToString("0.0", CultureInfo.CurrentCulture), p.SecondaryAccent, Brushes.White, s));
            if (task.DurationSeconds > 0)
                meta.Children.Add(FocusStartUi.Badge("🕒 " + (int)Math.Ceiling(task.DurationSeconds / 60.0) + " min", p.Metadata, p.Accent, s));
            if (meta.Children.Count > 0) text.Children.Add(meta);
            row.Children.Add(text);
            var card = new Border
            {
                Child = row,
                Background = suggestion ? Brushes.Transparent : p.Field,
                BorderBrush = suggestion ? Brushes.Transparent : p.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(s.Pt(20)),
                Padding = new Thickness(s.Pt(22)),
                Margin = new Thickness(0, 0, 0, s.Pt(12)),
            };
            // "Add tasks to focus on:" already ends in a colon; don't read "…on:: <task>".
            var button = FocusStartUi.Clickable(card, action, automationId, actionLabel.TrimEnd(':', ' ') + ": " + task.Title);
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            return button;
        }

        // ------------------------------------------------------------------ blocking card

        private UIElement BlockingBody(FocusStartStrings strings, FocusStartPalette p, FocusStartScale s)
        {
            var body = new StackPanel();
            var chooser = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
            chooser.Children.Add(FocusStartUi.Text(strings["labelChooseBlockList"] + " ", s.Pt(44), p.Primary, wrap: false));
            var selectedName = FocusStartUi.Text(_mode?.name ?? "", s.Pt(44), p.Accent, FontWeights.SemiBold, wrap: false);
            AutomationProperties.SetAutomationId(selectedName, "txtFocusStartSelectedBlockList");
            chooser.Children.Add(selectedName);
            body.Children.Add(chooser);

            Detach(_blockListBox);
            var list = new StackPanel();
            _blockListsHost = new StackPanel();
            list.Children.Add(Margin(FocusStartUi.SearchField(_blockListBox, strings["placeholderBlockList"], p, s, "txtFocusStartBlockListSearch"), 0, 0, 0, s.Pt(12)));
            var listScroll = new ScrollViewer { Content = _blockListsHost, MaxHeight = Math.Max(150, s.Pt(5 * 120)), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            double listOffset = _blockListOffset;
            if (listOffset > 0) listScroll.Loaded += (o, e) => listScroll.ScrollToVerticalOffset(listOffset);
            _blockListScroll = listScroll;
            list.Children.Add(listScroll);
            FillBlockLists(strings, p, s);
            body.Children.Add(Margin(list, 0, s.Pt(27), 0, 0));

            if (IronFocus)
            {
                var notice = FocusStartUi.Text(_settings.ConfigFileStrings?["pomodoroInfo"]?["superStrictBlockingActiveMessage"]?.ToString() ?? "A super strict blocking schedule is active.", s.Pt(36), p.Warning);
                notice.TextAlignment = TextAlignment.Center;
                AutomationProperties.SetAutomationId(notice, "txtFocusStartScheduleNotice");
                body.Children.Add(Margin(notice, 0, s.Pt(24), 0, 0));
            }
            var editText = FocusStartUi.Text(_settings.ConfigFileStrings?["pomodoroInfo"]?["buttonEditFocusModesText"]?.ToString() ?? strings["editBlockLists"], s.Pt(36), p.Accent, FontWeights.Medium, wrap: false);
            editText.TextDecorations = TextDecorations.Underline;
            var edit = FocusStartUi.Clickable(editText, FocusSessionStarter.OpenBlockLists, "btnFocusStartEditBlockLists");
            edit.HorizontalAlignment = HorizontalAlignment.Center;
            body.Children.Add(Margin(edit, 0, s.Pt(27), 0, 0));

            // The popup threshold the old screen kept under "advanced options": only meaningful with AI in cuddly mode.
            if (_mode != null && AiEnabledFor(_mode) && !_settings.IsEffectivelyGrizzly())
            {
                var row = new WrapPanel { Margin = new Thickness(0, s.Pt(27), 0, 0) };
                var label = FocusStartUi.Text(strings["labelFocusAlignmentScore"] + " ", s.Pt(36), p.Primary);
                label.VerticalAlignment = VerticalAlignment.Center;
                row.Children.Add(label);
                var threshold = new ComboBox { Width = Math.Max(60, s.Pt(170)), VerticalAlignment = VerticalAlignment.Center };
                for (int v = 50; v <= 100; v += 5) threshold.Items.Add(v);
                int current = _mode.AlignmentScoreThreshold >= 50 && _mode.AlignmentScoreThreshold <= 100 ? _mode.AlignmentScoreThreshold / 5 * 5 : 80;
                threshold.SelectedItem = current;
                var mode = _mode;
                threshold.SelectionChanged += (o, e) => { if (threshold.SelectedItem is int v) mode.AlignmentScoreThreshold = v; };
                AutomationProperties.SetAutomationId(threshold, "cmbFocusStartThreshold");
                row.Children.Add(threshold);
                body.Children.Add(row);
            }

            if (_settings.bIsTeacher)
            {
                // A session started here is always the teacher's own. Class sessions are started from the web
                // dashboard, so the "personal session instead" tick box and the class picker are gone; a note
                // says where to go.
                body.Children.Add(Margin(TeacherNote(p, s), 0, s.Pt(27), 0, 0));
            }
            return body;
        }

        /// <summary>An info note for teacher accounts: this screen starts a personal session, the dashboard a class one.</summary>
        private FrameworkElement TeacherNote(FocusStartPalette p, FocusStartScale s)
        {
            var row = new DockPanel();
            var icon = FocusStartUi.Glyph(0xE946, Math.Max(14, s.Pt(40)), p.Accent);   // info, in a circle
            icon.VerticalAlignment = VerticalAlignment.Top;
            icon.Margin = new Thickness(0, s.Pt(4), s.Pt(20), 0);
            DockPanel.SetDock(icon, Dock.Left);
            row.Children.Add(icon);

            var text = new StackPanel();
            text.Children.Add(FocusStartUi.Text(
                _settings.GetConfigStringSafe("This starts a personal focus session", "teacherSessionInfo", "personalSessionOnlyTitle"),
                Math.Max(11, s.Pt(36)), p.Primary, FontWeights.SemiBold));
            var detail = FocusStartUi.Text(
                _settings.GetConfigStringSafe("To start a class session for your students, use the Focus Bear dashboard in your browser.", "teacherSessionInfo", "classSessionFromDashboardText"),
                Math.Max(11, s.Pt(34)), p.Secondary);
            detail.Margin = new Thickness(0, s.Pt(6), 0, 0);
            text.Children.Add(detail);
            row.Children.Add(text);

            var note = new Border
            {
                Child = row,
                Background = p.Halo,
                BorderBrush = p.Accent,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(s.Pt(24)),
                Padding = new Thickness(s.Pt(28), s.Pt(22), s.Pt(28), s.Pt(22)),
            };
            AutomationProperties.SetAutomationId(text, "txtFocusStartTeacherNote");
            return note;
        }

        private void RenderBlockListsOnly()
        {
            if (_blockListsHost == null || _expanded != FocusStartCard.Blocking) return;
            FillBlockLists(Strings, FocusStartPalette.Current(), new FocusStartScale(ActualWidth > 0 ? ActualWidth : 1100));
        }

        private void FillBlockLists(FocusStartStrings strings, FocusStartPalette p, FocusStartScale s)
        {
            _blockListsHost.Children.Clear();
            string query = _blockListBox.Text.Trim();
            var rows = BlockLists.Where(m => query.Length == 0 || (m.name ?? "").IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0).ToList();
            if (rows.Count == 0) { _blockListsHost.Children.Add(FocusStartUi.Text(strings["noMatchingBlockLists"], s.Pt(36), p.Secondary)); return; }
            bool editable = !IronFocus && !_settings.bUseSuperPomodoroMode;
            foreach (var mode in rows)
            {
                var captured = mode;
                bool selected = mode == _mode;
                // As the Mac counts them: an allow list's own apps + sites; a block list acts on the always-blocked ones (plus its own).
                int count = mode.isAllowedListApproach ? mode.arrAllowedUrls.Count + mode.arrAllowedAppsWin.Count
                    : _settings.arrAlwaysBlockedURLs.Count + _settings.arrAlwaysBlockedApps.Count + mode.arrBlockedUrls.Count;
                var row = new DockPanel();
                var pencil = FocusStartUi.Clickable(FocusStartUi.Glyph(0xE70F, s.Pt(32), p.Accent), FocusSessionStarter.OpenBlockLists, "btnFocusStartEditBlockList", strings["editBlockLists"] + ": " + mode.name);
                pencil.Margin = new Thickness(s.Pt(18), 0, 0, 0);
                DockPanel.SetDock(pencil, Dock.Right);
                row.Children.Add(pencil);
                var check = new Border { Width = s.Pt(48), Height = s.Pt(48), CornerRadius = new CornerRadius(s.Pt(24)), Background = p.Accent, Opacity = selected ? 1 : 0, Child = FocusStartUi.Glyph(0xE73E, s.Pt(22), Brushes.White) };
                ((TextBlock)check.Child).HorizontalAlignment = HorizontalAlignment.Center;
                DockPanel.SetDock(check, Dock.Right);
                row.Children.Add(check);
                var line = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
                var name = FocusStartUi.Text(mode.name, s.Pt(36), selected ? p.Accent : p.Primary, selected ? FontWeights.SemiBold : FontWeights.Normal, wrap: false);
                name.Margin = new Thickness(0, 0, s.Pt(14), 0);
                line.Children.Add(name);
                var countText = FocusStartUi.Text(strings.WithCount("countAppsAndWebsites", count), s.Pt(36), selected ? p.SelectionText : p.Secondary, wrap: false);
                countText.Margin = new Thickness(0, 0, s.Pt(14), 0);
                line.Children.Add(countText);
                bool ai = AiEnabledFor(mode);
                line.Children.Add(FocusStartUi.Badge(ai ? strings["badgeAiEnabled"] : strings["badgeAiDisabled"], ai ? p.Accent : new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93)), Brushes.White, s));
                row.Children.Add(line);
                var face = new Border
                {
                    Child = row,
                    Background = selected ? p.Selection : p.Surface,
                    BorderBrush = selected ? p.Accent : p.Border,
                    BorderThickness = new Thickness(selected ? 1.5 : 0.5),
                    Padding = new Thickness(s.Pt(22)),
                };
                var button = FocusStartUi.Clickable(face, () =>
                {
                    if (!editable) return;
                    _mode = captured;
                    Render();
                }, "btnFocusStartBlockList", mode.name);
                button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                button.IsEnabled = editable || selected;
                _blockListsHost.Children.Add(button);
            }
        }

        // ------------------------------------------------------------------ mode switch and start

        private void SwitchToSimple()
        {
            _parent.PageSettings.SwitchAppMode(geek: false);
            _parent.HandleLeafSelection(LeftButtonType.StartFocusSession);
        }

        private async Task StartSession()
        {
            if (_starting || _mode == null) return;
            LogWriter.LogWrite("FocusStartAdvancedScreen - start session");
            var clock = System.Diagnostics.Stopwatch.StartNew();
            _settings.sRunningFocusModeIntention = _goal;
            App.FocusButton.SendFocusGoal(_settings.sRunningFocusModeIntention);

            if (_settings.bUseSuperPomodoroMode)
            {
                // A Super Focus run is already going: only the goal changes.
                _parent.Hide();
                _settings.isStartPomodoroWindowOpen = false;
                return;
            }
            int minutes = _duration.EffectiveMinutes;
            string breakMinutes = FocusSessionStarter.BreakFromRatio(minutes);
            if (_superFocus)
            {
                _settings.bUseSuperPomodoroMode = true;
                _settings.NumberOfSuperPomodorosToRun = _superFocusCount;
                _settings.nSuperPomodoroCountCompleted = 0;
                _settings.CapturePostHog("start-focus-session-manually", string.Format("\"type\":\"super-focus\",\"focusDuration\":\"{0}\",\"breakDuration\":\"{1}\"", minutes, breakMinutes));
            }
            else _settings.CapturePostHog("start-focus-session-manually", string.Format("\"type\":\"single-focus\",\"focusDuration\":\"{0}\",\"breakDuration\":\"{1}\"", minutes, breakMinutes));

            int threshold = _mode.AlignmentScoreThreshold >= 50 && _mode.AlignmentScoreThreshold <= 100 ? _mode.AlignmentScoreThreshold : 80;
            _settings.sRunningFocusModeAlignmentScoreThreshold = (double)threshold / 100;
            _settings.sMinutesBreakPomodoro = "";

            _starting = true;
            Render();
            try
            {
                // Always a personal session, for teachers too: with personalSession this only records that choice
                // and never starts a class session (those start from the web dashboard).
                await FocusSessionStarter.StartClassSessionIfTeacher(_mode, minutes, personalSession: true);
                FocusSessionStarter.WarnAllowListWithoutAi(_mode, _parent);
                string queue = FocusStartWorkSelection.SessionQueue(_selectedTasks, _projects);
                FocusSessionStarter.Start(_mode, minutes, breakMinutes, queue == FocusStartWorkSelection.EmptyQueue ? "" : queue, personalSession: true);
                _settings.isStartPomodoroWindowOpen = false;
                LogWriter.LogWrite("[timing] start session: running " + clock.ElapsedMilliseconds + " ms after Start was pressed");
            }
            finally
            {
                _starting = false;
                Render();
            }
        }
    }
}
