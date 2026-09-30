using FocusBear.Core.Stats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Focus_Bear.StatsScreens
{
    // ================================================================== Leaderboard

    internal sealed class LeaderboardTab : StatsTabView
    {
        private const double RankWidth = 50, StreakWidth = 118;
        private const string UsernamePlaceholder = "-----";

        private LeaderboardResponse _response;
        private LeaderboardSortKey _sortKey = LeaderboardSortKey.Morning;
        private bool _ascending;
        private string _expandedUserId;
        private bool _isLoading, _isSavingUsername;
        private string _usernameDraft = "", _savedUsername = "";
        private TextBox _usernameBox;
        private Button _updateButton;
        private TextBlock _usernameRule;

        internal LeaderboardTab(StatsStrings strings) : base(strings) { }

        /// <summary>The server ranks — and picks the top N — by <c>streak_type</c>, so a sort change refetches.</summary>
        public override async void Load()
        {
            int generation = ++Generation;
            _isLoading = true;
            if (_response == null) ShowLoading(); else Render();
            var key = _sortKey;
            var response = StatsWire.ReadLeaderboard(await Api().GetLeaderboard(API.FocusBearAPIClient.StatsLeaderboardLimit, LeaderboardSortKeys.WireValue(key)));
            if (generation != Generation) return;
            _isLoading = false;
            if (response == null)
            {
                if (_response == null) ShowError(); else Render();
                return;
            }
            _response = response;
            _savedUsername = response.UserRank?.Username ?? "";
            _usernameDraft = _savedUsername;
            Render();
        }

        private void Render()
        {
            var stack = new StackPanel();
            stack.Children.Add(Toolbar());
            stack.Children.Add(StatsUi.Card(Table(), padding: new Thickness(16, 10, 16, 6)));
            stack.Children.Add(UsernameEditor());
            Show(stack);
        }

        private FrameworkElement Toolbar()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            var sortBy = StatsUi.Text(Strings.SortBy, 13, secondary: true, wrap: false);
            sortBy.VerticalAlignment = VerticalAlignment.Center;
            sortBy.Margin = new Thickness(0, 0, 10, 0);
            row.Children.Add(sortBy);
            row.Children.Add(StatsUi.Segmented(LeaderboardSortKeys.All, _sortKey, Strings.SortTitle, key =>
            {
                if (key == _sortKey) return;
                _sortKey = key;
                Load();
            }));
            var direction = StatsUi.Button((_ascending ? "↑  " + Strings.Ascending : "↓  " + Strings.Descending), (s, e) => { _ascending = !_ascending; Render(); });
            direction.Margin = new Thickness(10, 0, 0, 0);
            row.Children.Add(direction);
            if (_isLoading && _response != null)
            {
                var spinner = StatsUi.Text("…", 14, secondary: true);
                spinner.VerticalAlignment = VerticalAlignment.Center;
                spinner.Margin = new Thickness(10, 0, 0, 0);
                row.Children.Add(spinner);
            }
            return row;
        }

        private FrameworkElement Table()
        {
            var stack = new StackPanel();
            stack.Children.Add(Cells(new[] { Strings.Rank, Strings.Name, Strings.MorningStreak, Strings.BreakStreak, Strings.EveningStreak, Strings.FocusMode }, header: true));
            stack.Children.Add(Divider());
            var rows = LeaderboardSorting.Rank(_response.UsersRankings, _sortKey, _ascending);
            if (rows.Count == 0)
            {
                var none = StatsUi.Text(Strings.NoUserFound, 13, secondary: true);
                none.Margin = new Thickness(0, 20, 0, 20);
                stack.Children.Add(none);
            }
            foreach (var row in rows)
            {
                stack.Children.Add(UserRow(row.User, row.Rank));
                stack.Children.Add(Divider());
            }
            return stack;
        }

        private static Border Divider()
        {
            var line = new Border { Height = 1 };
            line.SetResourceReference(Border.BackgroundProperty, "TrayCardBorder");
            return line;
        }

        private Grid Columns()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(RankWidth) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 90 });
            for (int i = 0; i < 4; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(StreakWidth) });
            return grid;
        }

        private Grid Cells(string[] titles, bool header)
        {
            var grid = Columns();
            grid.Margin = new Thickness(4, 6, 4, 6);
            for (int i = 0; i < titles.Length; i++)
            {
                var cell = StatsUi.Text(titles[i], 11, FontWeights.SemiBold, secondary: true, wrap: false);
                Grid.SetColumn(cell, i);
                grid.Children.Add(cell);
            }
            return grid;
        }

        private static readonly LeaderboardSortKey[] MetricOrder = { LeaderboardSortKey.Morning, LeaderboardSortKey.MicroBreak, LeaderboardSortKey.Evening, LeaderboardSortKey.FocusMode };

        private string Percent(LeaderboardMetric metric) => StatsText.Fill(Strings.PercentValue, ("value", StatsText.PlainNumber(metric.Percentage)));

        private string StreakText(LeaderboardMetric metric) => StatsText.Fill(Strings.StreakValue, ("streak", StatsText.PlainNumber(metric.Streak)));

        private string Summary(LeaderboardMetric metric) => LeaderboardSorting.CompletionSummary(metric, Strings.DaysCompletedOutOf, Strings.DaysCompletedOutOfExpected);

        private FrameworkElement UserRow(LeaderboardUser user, int rank)
        {
            bool isCurrentUser = _response.UserRank != null && user.Id == _response.UserRank.Id;
            var grid = Columns();
            grid.Margin = new Thickness(4, 9, 4, 9);
            var rankText = StatsUi.Text(rank.ToString(), 13, wrap: false);
            grid.Children.Add(rankText);
            var name = StatsUi.Text(user.Username ?? UsernamePlaceholder, 13, isCurrentUser ? FontWeights.SemiBold : FontWeights.Normal, wrap: false);
            Grid.SetColumn(name, 1);
            grid.Children.Add(name);
            for (int i = 0; i < MetricOrder.Length; i++)
            {
                var metric = LeaderboardSorting.Metric(user, MetricOrder[i]);
                var cell = new StackPanel { Orientation = Orientation.Horizontal, ToolTip = StreakText(metric) + "\n" + Summary(metric) };
                cell.Children.Add(StatsUi.Text(Percent(metric), 13, FontWeights.Medium, wrap: false));
                if (metric.WindowDays > 0)
                {
                    var fraction = StatsUi.Text(" (" + StatsText.PlainNumber(metric.CompletedDays) + "/" + StatsText.PlainNumber(metric.WindowDays) + ")", 11, secondary: true, wrap: false);
                    fraction.VerticalAlignment = VerticalAlignment.Center;
                    cell.Children.Add(fraction);
                }
                Grid.SetColumn(cell, i + 2);
                grid.Children.Add(cell);
            }
            var rowButton = new Button { Content = grid, Template = StatsUi.PlainTemplate(), Cursor = System.Windows.Input.Cursors.Hand, HorizontalContentAlignment = HorizontalAlignment.Stretch, Focusable = false };
            rowButton.Click += (s, e) => { _expandedUserId = _expandedUserId == user.Id ? null : user.Id; Render(); };
            var container = new StackPanel();
            var highlight = new Border { Child = rowButton, CornerRadius = new CornerRadius(6), Background = isCurrentUser ? StatsUi.Tint(StatsUi.Orange, 0.12) : Brushes.Transparent };
            container.Children.Add(highlight);
            if (_expandedUserId == user.Id)
            {
                var details = new UniformGrid { Columns = 4, Margin = new Thickness(0, 4, -10, 8) };
                for (int i = 0; i < MetricOrder.Length; i++)
                {
                    var metric = LeaderboardSorting.Metric(user, MetricOrder[i]);
                    var panel = new StackPanel();
                    panel.Children.Add(StatsUi.Text(Strings.SortTitle(MetricOrder[i]), 12, FontWeights.SemiBold));
                    panel.Children.Add(StatsUi.WithOrangeText(new TextBlock { Text = Percent(metric), FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 4) }));
                    panel.Children.Add(StatsUi.Text(StreakText(metric), 12, secondary: true));
                    panel.Children.Add(StatsUi.Text(Summary(metric), 11, secondary: true));
                    var box = new Border { Child = panel, Padding = new Thickness(10), CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 10, 0) };
                    box.SetResourceReference(Border.BackgroundProperty, "BackgroundColor");
                    details.Children.Add(box);
                }
                container.Children.Add(details);
            }
            return container;
        }

        private FrameworkElement UsernameEditor()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            _usernameBox = new TextBox { Text = _usernameDraft, Width = 260, Height = 32, FontSize = 13, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(6, 0, 6, 0), BorderBrush = Brushes.LightGray };
            _usernameBox.SetResourceReference(Control.BackgroundProperty, "TextBoxBackgroundColor");
            _usernameBox.SetResourceReference(Control.ForegroundProperty, "ForegroundColor");
            System.Windows.Automation.AutomationProperties.SetAutomationId(_usernameBox, "txtLeaderboardUsername");
            _usernameBox.TextChanged += (s, e) => { _usernameDraft = _usernameBox.Text; UpdateUsernameControls(); };
            row.Children.Add(_usernameBox);
            _updateButton = StatsUi.Button(Strings.Update, (s, e) => SaveUsername(), primary: true);
            _updateButton.Margin = new Thickness(8, 0, 0, 0);
            row.Children.Add(_updateButton);
            _usernameRule = new TextBlock { Text = Strings.UsernameRule, FontSize = 12, Foreground = StatsUi.Danger, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            row.Children.Add(_usernameRule);
            UpdateUsernameControls();
            return StatsUi.Card(row, Strings.LeaderboardUsername);
        }

        private void UpdateUsernameControls()
        {
            if (_updateButton == null) return;
            _updateButton.Visibility = LeaderboardUsername.CanUpdate(_usernameDraft, _savedUsername, _isSavingUsername) ? Visibility.Visible : Visibility.Collapsed;
            _usernameRule.Visibility = _usernameDraft.Length > 0 && !LeaderboardUsername.IsValid(_usernameDraft) ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void SaveUsername()
        {
            if (!LeaderboardUsername.CanUpdate(_usernameDraft, _savedUsername, _isSavingUsername)) return;
            _isSavingUsername = true;
            UpdateUsernameControls();
            string username = _usernameDraft;
            bool success = await Api().UpdateUsername(username);
            _isSavingUsername = false;
            if (!success) { UpdateUsernameControls(); return; }
            _savedUsername = username;
            Load();
        }
    }

    // ================================================================== Habit Stats

    internal sealed class HabitStatsTab : StatsTabView
    {
        private IList<StatsActivity> _activities = new List<StatsActivity>();
        private HabitConsistencyRanking.Split _ranking;
        private DateTime? _joinDate;
        private string _selectedHabitId, _selectedQuestionId;
        private IList<ActivityStatItem> _items;
        private bool _isDurationBased = true;
        private IReadOnlyList<GraphPoint> _questionPoints = new List<GraphPoint>();
        private bool _isLoadingDetails;
        private int _detailsRequest;

        internal HabitStatsTab(StatsStrings strings) : base(strings) { }

        private StatsActivity SelectedHabit => _activities.FirstOrDefault(a => a.Id == _selectedHabitId);

        public override async void Load()
        {
            int generation = ++Generation;
            if (_ranking == null) ShowLoading();
            var settings = StatsWire.ReadUserSettings(await Api().GetStatsUserSettings());
            if (generation != Generation) return;
            if (settings == null) { ShowError(); return; }
            _activities = settings.UniqueActivities;
            _joinDate = settings.CreatedAt;
            if (_activities.Count == 0)
            {
                Show(StatsUi.Empty("\U0001F4ED", Strings.NoHabitsTitle, StatsText.Fill(Strings.NoHabitsDescription, ("navItem", Strings.EditHabitsNavItem))));
                return;
            }
            var consistency = StatsWire.ReadConsistency(await Api().GetConsistencyStats(_activities.Select(a => a.Id), Timezone));
            if (generation != Generation) return;
            if (consistency == null) { ShowError(); return; }
            var ranked = HabitConsistencyRanking.Build(_activities.Select(a => new ConsistencyInput { ActivityId = a.Id, CreatedAt = a.CreatedAt }), consistency, DateTime.Now);
            _ranking = HabitConsistencyRanking.SplitRanked(ranked);
            // Web: default to the most consistent habit, else the least consistent list's first row.
            var fallback = _ranking.MostConsistent.FirstOrDefault() ?? _ranking.LeastConsistent.FirstOrDefault();
            if (SelectedHabit != null) Select(SelectedHabit);
            else if (fallback != null) Select(_activities.First(a => a.Id == fallback.ActivityId));
            else Render();
        }

        private async void Select(StatsActivity habit)
        {
            _selectedHabitId = habit.Id;
            _selectedQuestionId = habit.Questions.Count == 1 ? habit.Questions[0].Id : null;
            _items = null;
            _questionPoints = new List<GraphPoint>();
            _isLoadingDetails = true;
            int request = ++_detailsRequest;
            Render();
            var stats = StatsWire.ReadActivityStats(await Api().GetActivityStats(habit.Id, HeatMapBuilder.FetchDays, Timezone));
            if (request != _detailsRequest) return;
            _items = stats?.Items ?? new List<ActivityStatItem>();
            _isDurationBased = stats?.IsDurationBased ?? true;
            _isLoadingDetails = false;
            Render();
            if (_selectedQuestionId != null) LoadQuestion(_selectedQuestionId);
        }

        private async void LoadQuestion(string questionId)
        {
            int request = _detailsRequest;
            var items = StatsWire.ReadQuestionStats(await Api().GetLogQuestionStats(questionId));
            if (request != _detailsRequest || _selectedQuestionId != questionId) return;
            _questionPoints = LogQuantityGraph.Points(items ?? new List<ActivityStatItem>());
            Render();
        }

        private void Render()
        {
            var stack = new StackPanel();
            if (_ranking != null)
            {
                var sections = new Grid();
                sections.ColumnDefinitions.Add(new ColumnDefinition());
                if (_ranking.ShowsLeastSection)
                {
                    sections.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
                    sections.ColumnDefinitions.Add(new ColumnDefinition());
                }
                sections.Children.Add(Section(Strings.MostConsistentHabits, _ranking.MostConsistent, StatsUi.Positive));
                if (_ranking.ShowsLeastSection)
                {
                    var least = Section(Strings.LeastConsistentHabits, _ranking.LeastConsistent, StatsUi.Warning);
                    Grid.SetColumn(least, 2);
                    sections.Children.Add(least);
                }
                stack.Children.Add(StatsUi.Card(sections));
            }
            var habit = SelectedHabit;
            if (habit != null)
            {
                if (_isLoadingDetails || _items == null) stack.Children.Add(StatsUi.Loading());
                else
                {
                    var today = DateTime.Now;
                    var summary = HabitStatSummary.Compute(_items, today);
                    var heatMap = HeatMapBuilder.Build(_items, _joinDate, today);
                    var tiles = new List<UIElement>
                    {
                        StatsUi.Tile(Strings.TotalCompletions, summary.TotalCompletions.ToString(), StatsUi.Positive),
                    };
                    // Hidden for a quantity habit: the summary is the logged amount there, not seconds.
                    if (_isDurationBased) tiles.Add(StatsUi.Tile(Strings.HoursSpent, StatsText.Fill(Strings.HoursValue, ("value", StatsText.PlainNumber(summary.HoursSpent))), StatsUi.Info));
                    tiles.Add(StatsUi.Tile(Strings.CurrentStreak, StatsText.Fill(Strings.DaysValue, ("value", summary.CurrentStreak.ToString())), StatsUi.Orange));
                    tiles.Add(StatsUi.Tile(Strings.Last30Days, StatsText.Fill(Strings.PercentValue, ("value", summary.Last30DaysPercentage.ToString())), StatsUi.Highlight));
                    stack.Children.Add(StatsUi.Tiles(tiles.ToArray()));
                    if (heatMap.TotalCount > 0 || _questionPoints.Count > 0) stack.Children.Add(History(habit, heatMap));
                }
            }
            Show(stack);
        }

        private FrameworkElement Section(string title, IReadOnlyList<HabitConsistencyItem> items, Brush color)
        {
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = color });
            var caption = StatsUi.Text(StatsText.Fill(Strings.PercentOfLastDays, ("days", HabitConsistencyRanking.WindowMaxDays.ToString())), 11, secondary: true);
            caption.Margin = new Thickness(0, 2, 0, 8);
            stack.Children.Add(caption);
            var list = new StackPanel();
            foreach (var item in items)
            {
                var activity = _activities.FirstOrDefault(a => a.Id == item.ActivityId);
                if (activity != null) list.Children.Add(ConsistencyRow(activity, item, color));
            }
            stack.Children.Add(new ScrollViewer { Content = list, MaxHeight = 46 * 5 + 8, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            return stack;
        }

        private FrameworkElement ConsistencyRow(StatsActivity activity, HabitConsistencyItem item, Brush color)
        {
            bool selected = activity.Id == _selectedHabitId;
            var top = new DockPanel();
            var percent = StatsUi.Text(item.Percentage + "%", 12, FontWeights.SemiBold, wrap: false);
            DockPanel.SetDock(percent, Dock.Right);
            top.Children.Add(percent);
            top.Children.Add(StatsUi.Text((string.IsNullOrEmpty(activity.HabitIcon) ? "•" : activity.HabitIcon) + "  " + (activity.Name ?? ""), 13, wrap: false));
            var track = new Grid { Height = 6, Margin = new Thickness(0, 5, 0, 0) };
            var background = new Border { CornerRadius = new CornerRadius(3) };
            background.SetResourceReference(Border.BackgroundProperty, "TrayCardBorder");
            track.Children.Add(background);
            track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(item.Percentage, 0), GridUnitType.Star) });
            track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(100 - item.Percentage, 0), GridUnitType.Star) });
            Grid.SetColumnSpan(background, 2);
            track.Children.Add(new Border { CornerRadius = new CornerRadius(3), Background = color });
            var body = new StackPanel();
            body.Children.Add(top);
            body.Children.Add(track);
            var frame = new Border
            {
                Child = body, Padding = new Thickness(8, 6, 8, 6), CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 0, 0, 4),
                Background = selected ? StatsUi.Tint(StatsUi.Orange, 0.12) : Brushes.Transparent,
                BorderBrush = selected ? StatsUi.Orange : Brushes.Transparent, BorderThickness = new Thickness(1),
            };
            var button = new Button { Content = frame, Template = StatsUi.PlainTemplate(), Cursor = System.Windows.Input.Cursors.Hand, HorizontalContentAlignment = HorizontalAlignment.Stretch, Focusable = false };
            System.Windows.Automation.AutomationProperties.SetAutomationId(button, "habit" + activity.Id);
            button.Click += (s, e) => Select(activity);
            return button;
        }

        private FrameworkElement History(StatsActivity habit, HeatMapModel heatMap)
        {
            var stack = new StackPanel();
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            if (habit.Questions.Count > 1)
            {
                var picker = new ComboBox { Width = 260, Height = 32, FontSize = 13 };
                picker.Items.Add(new ComboBoxItem { Content = Strings.SelectQuestions, Tag = null });
                foreach (var question in habit.Questions)
                {
                    var option = new ComboBoxItem { Content = question.Question ?? question.Id, Tag = question.Id };
                    picker.Items.Add(option);
                    if (question.Id == _selectedQuestionId) picker.SelectedItem = option;
                }
                if (picker.SelectedItem == null) picker.SelectedIndex = 0;
                picker.SelectionChanged += (s, e) =>
                {
                    var id = (picker.SelectedItem as ComboBoxItem)?.Tag as string;
                    _selectedQuestionId = id;
                    _questionPoints = new List<GraphPoint>();
                    if (id != null) LoadQuestion(id); else Render();
                };
                DockPanel.SetDock(picker, Dock.Right);
                header.Children.Add(picker);
            }
            header.Children.Add(StatsUi.Heading(habit.Title));
            stack.Children.Add(header);
            stack.Children.Add(new ScrollViewer { Content = new HeatMapView(heatMap, Strings, LanguageCode), HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
            var caption = StatsUi.Text(Strings.HabitCompletionHistory, 11, secondary: true);
            caption.Margin = new Thickness(0, 4, 0, 0);
            stack.Children.Add(caption);
            if (_selectedQuestionId != null)
            {
                var question = habit.Questions.FirstOrDefault(q => q.Id == _selectedQuestionId);
                var divider = new Border { Height = 1, Margin = new Thickness(0, 14, 0, 14) };
                divider.SetResourceReference(Border.BackgroundProperty, "TrayCardBorder");
                stack.Children.Add(divider);
                string yTitle = question != null ? (question.IsAveraged ? Strings.DailyAverage : Strings.DailyTotal) : Strings.QuantityLogged;
                stack.Children.Add(new StatsChart(_questionPoints.ToList(), StatsChart.ChartStyle.Line, yTitle, Strings.Date, Strings.NoDataAvailable));
                var chartCaption = StatsUi.Text(Strings.LogQuantityHistory, 11, secondary: true);
                chartCaption.Margin = new Thickness(0, 4, 0, 0);
                stack.Children.Add(chartCaption);
            }
            return StatsUi.Card(stack);
        }
    }

    // ================================================================== Productivity

    internal sealed class ProductivityTab : StatsTabView
    {
        private const string RangePattern = "MMM dd, yyyy";
        private static readonly ProductivityPeriod[] Periods = { ProductivityPeriod.Daily, ProductivityPeriod.Monthly, ProductivityPeriod.Yearly };

        private ProductivityPeriod _period = ProductivityPeriod.Daily;
        private List<FocusSessionSummary> _sessions;
        private ProductivitySummary _summary;
        private (DateTime From, DateTime To) _range;

        internal ProductivityTab(StatsStrings strings) : base(strings) { }

        public override async void Load()
        {
            int generation = ++Generation;
            _range = ProductivitySummary.DateRange(_period, DateTime.Now);
            if (_sessions == null) ShowLoading();
            var period = _period;
            var sessions = StatsWire.ReadFocusSessions(await Api().GetCompletedFocusBlocks(_range.From, _range.To));
            if (generation != Generation) return;
            if (sessions == null) { ShowError(); return; }
            _sessions = sessions;
            _summary = ProductivitySummary.Compute(sessions, period);
            Render();
        }

        private FrameworkElement PeriodPicker() =>
            StatsUi.Segmented(Periods, _period, Strings.PeriodTitle, p => { if (p != _period) { _period = p; Load(); } });

        private void Render()
        {
            var stack = new StackPanel();
            if (_sessions.Count == 0)
            {
                stack.Children.Add(StatsUi.Empty("\U0001F4CA", Strings.NoProductivityData));
                var picker = PeriodPicker();
                ((FrameworkElement)picker).HorizontalAlignment = HorizontalAlignment.Center;
                stack.Children.Add(picker);
                Show(stack);
                return;
            }
            var range = StatsUi.Text(StatsDates.Format(_range.From, RangePattern) + " - " + StatsDates.Format(_range.To, RangePattern), 12, secondary: true);
            range.Margin = new Thickness(0, 0, 0, 12);
            stack.Children.Add(range);

            var tiles = new List<UIElement>
            {
                StatsUi.Tile(Strings.FocusedMinutes + " (" + Strings.Total + ")", Strings.FocusedTime(_summary.FocusedMinutes), StatsUi.Orange),
                StatsUi.Tile(Strings.AverageFocusPerDay, Strings.FocusedTime(_summary.AverageMinutesPerDay), StatsUi.Info),
                StatsUi.Tile(Strings.MostFocusedDay, _summary.MostFocusedDay == null ? "-" :
                    StatsDates.Format(_summary.MostFocusedDay.Date, RangePattern) + " (" + Strings.FocusedTime(_summary.MostFocusedDay.Value) + ")", StatsUi.Positive, wrapValue: true),
            };
            if (_summary.HasTabMetrics)
            {
                tiles.Add(StatsUi.Tile(Strings.AverageTabCount, StatsText.Fill(Strings.TabsValue, ("value", StatsText.PlainNumber(_summary.AverageTabCount))), StatsUi.Highlight));
                tiles.Add(StatsUi.Tile(Strings.AverageTabDuration, StatsText.Fill(Strings.SecondsShortValue, ("value", StatsText.PlainNumber(_summary.AverageTabDuration))), StatsUi.Highlight));
            }
            stack.Children.Add(StatsUi.Tiles(tiles.ToArray()));

            string title = _summary.UsesHours ? Strings.FocusedHours : Strings.FocusedMinutes;
            var chartStack = new StackPanel();
            chartStack.Children.Add(StatsUi.Heading(title));
            var description = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 12) };
            description.Children.Add(StatsUi.Text(Strings.FocusedMinutesDescription, 12, secondary: true, wrap: false));
            var info = StatsUi.Text("ⓘ", 12, secondary: true, wrap: false);
            info.Margin = new Thickness(6, 0, 0, 0);
            info.ToolTip = new ToolTip { Content = new TextBlock { Text = Strings.AlignmentScoreTooltip, TextWrapping = TextWrapping.Wrap, MaxWidth = 320 } };
            description.Children.Add(info);
            chartStack.Children.Add(description);
            chartStack.Children.Add(new StatsChart(_summary.Chart, StatsChart.ChartStyle.Bars, title, Strings.Date, Strings.NoProductivityData));
            var picker2 = PeriodPicker();
            ((FrameworkElement)picker2).HorizontalAlignment = HorizontalAlignment.Center;
            ((FrameworkElement)picker2).Margin = new Thickness(0, 10, 0, 0);
            chartStack.Children.Add(picker2);
            stack.Children.Add(StatsUi.Card(chartStack));
            Show(stack);
        }
    }
}
