using Focus_Bear.Utils;
using FocusBear.Core.Stats;
using FocusBear.Core.ToDo;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Focus_Bear.StatsScreens
{
    /// <summary>
    /// The Focus Log tab (web <c>dashboard/stats/focusSummary</c>, the Mac's <c>FocusLogView</c>): a sortable,
    /// filterable table of sessions, per-session detail, manual sessions, edit and delete, the summary graph
    /// and CSV export.
    /// </summary>
    internal sealed class FocusLogTab : StatsTabView
    {
        private const double DurationWidth = 130, TimeWidth = 230, ModeWidth = 130, ActionsWidth = 34;
        private const string TimePattern = "MMM dd yyyy, hh:mm tt";
        /// <summary>A same-day session's finish: the date is already on the start.</summary>
        private const string ClockPattern = "hh:mm tt";

        private List<FocusLogEntry> _entries;
        private bool _canLoadMore, _isLoading;
        private FocusLogColumn _sortColumn = FocusLogColumn.Time;
        private FocusLogColumn? _filterColumn;
        private string _filterText = "";
        private DateTime? _filterDay;
        private List<FocusModeSummary> _focusModes = new List<FocusModeSummary>();
        private string _status;
        private StackPanel _rows;

        internal FocusLogTab(StatsStrings strings) : base(strings) { }

        public override async void Load()
        {
            int generation = ++Generation;
            _isLoading = true;
            _status = null;
            // A refresh keeps the list up and shows a progress line; it used to give no sign of running.
            if (_entries == null) ShowLoading(); else Render();
            var entries = StatsWire.ReadFocusLog(await Api().GetFocusLog(FocusLogMath.PageSize, 0));
            if (generation != Generation) return;
            _isLoading = false;
            if (entries == null)
            {
                if (_entries == null) ShowError();
                else { _status = Strings.LoadFailed; Render(); }
                return;
            }
            _entries = entries;
            _canLoadMore = entries.Count == FocusLogMath.PageSize;
            Render();
            if (_focusModes.Count == 0) _focusModes = StatsWire.ReadFocusModes(await Api().GetFocusModeSummaries()) ?? new List<FocusModeSummary>();
        }

        private async void LoadMore()
        {
            if (!_canLoadMore || _isLoading) return;
            _isLoading = true;
            int generation = Generation;
            var page = StatsWire.ReadFocusLog(await Api().GetFocusLog(FocusLogMath.PageSize, _entries.Count)) ?? new List<FocusLogEntry>();
            if (generation != Generation) return;
            _isLoading = false;
            _entries = FocusLogMath.Merge(_entries, page);
            _canLoadMore = page.Count == FocusLogMath.PageSize;
            Render();
        }

        private List<FocusLogEntry> Visible
        {
            get
            {
                IEnumerable<FocusLogEntry> list = _entries;
                if (_filterColumn != null) list = FocusLogMath.Filtered(list, _filterColumn.Value, _filterText, _filterDay, Strings.HumanizeSeconds);
                return FocusLogMath.Sorted(list, _sortColumn);
            }
        }

        internal static string TimeText(string raw)
        {
            var date = ToDoDates.Parse(raw);
            return date == null ? "--" : StatsDates.Format(date.Value, TimePattern);
        }

        /// <summary>"Sep 11 2026, 12:14 PM – 12:58 PM" — the date repeated only for a session that ran past midnight.</summary>
        internal static string RangeText(string start, string finish)
        {
            string startText = TimeText(start);
            DateTime? s = ToDoDates.Parse(start), f = ToDoDates.Parse(finish);
            if (s == null || f == null) return finish == null ? startText : startText + " – " + TimeText(finish);
            string finishText = s.Value.Date == f.Value.Date ? StatsDates.Format(f.Value, ClockPattern) : TimeText(finish);
            return startText + " – " + finishText;
        }

        private void Render()
        {
            var stack = new StackPanel();
            stack.Children.Add(Toolbar());
            if (_isLoading)
                stack.Children.Add(new ProgressBar { IsIndeterminate = true, Height = 3, Foreground = StatsUi.Orange, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 0, 10) });
            if (_entries.Count == 0)
            {
                stack.Children.Add(StatsUi.Empty("\U0001F4CB", Strings.NoFocusSummary, Strings.StartFocusSessionHint));
            }
            else
            {
                var table = new StackPanel();
                table.Children.Add(Header());
                if (_filterColumn != null) table.Children.Add(FilterBar());
                table.Children.Add(Divider());
                _rows = new StackPanel();
                table.Children.Add(_rows);
                RenderRows();
                stack.Children.Add(StatsUi.Card(table, padding: new Thickness(16, 10, 16, 6)));
                if (_canLoadMore)
                {
                    var more = StatsUi.Button(Strings.LoadMoreSessions, (s, e) => LoadMore());
                    more.HorizontalAlignment = HorizontalAlignment.Center;
                    more.Margin = new Thickness(0);
                    stack.Children.Add(more);
                }
            }
            Show(stack);
        }

        private void RenderRows()
        {
            _rows.Children.Clear();
            var visible = Visible;
            if (visible.Count == 0)
            {
                var none = StatsUi.Text(Strings.NoFocusSummary, 13, secondary: true);
                none.Margin = new Thickness(0, 20, 0, 20);
                _rows.Children.Add(none);
            }
            foreach (var entry in visible)
            {
                _rows.Children.Add(Row(entry));
                _rows.Children.Add(Divider());
            }
        }

        private FrameworkElement Toolbar()
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 12), LastChildFill = false };
            if (_status != null)
            {
                var status = new TextBlock { Text = _status, FontSize = 12, Foreground = StatsUi.Danger, VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(status, Dock.Left);
                row.Children.Add(status);
            }
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(buttons, Dock.Right);
            var add = StatsUi.Button("+  " + Strings.AddManualSession, (s, e) => OpenEditor(null), primary: true);
            System.Windows.Automation.AutomationProperties.SetAutomationId(add, "btnAddManualSession");
            buttons.Children.Add(add);
            var refresh = StatsUi.Button("↻  " + Strings.Refresh, (s, e) => Load());
            refresh.IsEnabled = !_isLoading;
            System.Windows.Automation.AutomationProperties.SetAutomationId(refresh, "btnFocusLogRefresh");
            buttons.Children.Add(refresh);
            var graph = StatsUi.Button("\U0001F4C8  " + Strings.FocusBlockSummary, (s, e) => OpenGraph());
            graph.IsEnabled = _entries.Count > 0;
            System.Windows.Automation.AutomationProperties.SetAutomationId(graph, "btnFocusBlockSummary");
            buttons.Children.Add(graph);
            var export = StatsUi.Button("⭳  " + Strings.Export, (s, e) => ExportCsv());
            export.IsEnabled = _entries.Count > 0;
            export.Margin = new Thickness(0);
            buttons.Children.Add(export);
            row.Children.Add(buttons);
            return row;
        }

        private static Border Divider()
        {
            var line = new Border { Height = 1 };
            line.SetResourceReference(Border.BackgroundProperty, "TrayCardBorder");
            return line;
        }

        private static Grid Columns()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(DurationWidth) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TimeWidth) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 80 });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 80 });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ModeWidth) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ActionsWidth) });
            return grid;
        }

        private static readonly FocusLogColumn[] ColumnOrder = { FocusLogColumn.Duration, FocusLogColumn.Time, FocusLogColumn.Intention, FocusLogColumn.Distractions, FocusLogColumn.FocusMode };

        private FrameworkElement Header()
        {
            var grid = Columns();
            grid.Margin = new Thickness(0, 6, 0, 6);
            for (int i = 0; i < ColumnOrder.Length; i++)
            {
                var column = ColumnOrder[i];
                var cell = new StackPanel { Orientation = Orientation.Horizontal };
                cell.Children.Add(StatsUi.Text(Strings.ColumnTitle(column), 11, FontWeights.SemiBold, secondary: true, wrap: false));
                cell.Children.Add(IconButton("↓", _sortColumn == column, () => { _sortColumn = column; Render(); }, "sort" + column));
                cell.Children.Add(IconButton("⫶", _filterColumn == column, () =>
                {
                    _filterColumn = _filterColumn == column ? (FocusLogColumn?)null : column;
                    _filterText = "";
                    _filterDay = null;
                    Render();
                }, "filter" + column));
                Grid.SetColumn(cell, i);
                grid.Children.Add(cell);
            }
            return grid;
        }

        private static Button IconButton(string glyph, bool active, Action onClick, string id)
        {
            var text = new TextBlock { Text = glyph, FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            if (active) StatsUi.WithOrangeText(text);
            else text.SetResourceReference(TextBlock.ForegroundProperty, "TraySubtitleColor");
            var button = new Button { Content = text, Template = StatsUi.PlainTemplate(), Cursor = System.Windows.Input.Cursors.Hand, Focusable = false };
            System.Windows.Automation.AutomationProperties.SetAutomationId(button, id);
            button.Click += (s, e) => onClick();
            return button;
        }

        private FrameworkElement FilterBar()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var column = _filterColumn.Value;
            var label = StatsUi.Text(Strings.ColumnTitle(column), 12, FontWeights.SemiBold, wrap: false);
            label.VerticalAlignment = VerticalAlignment.Center;
            label.Margin = new Thickness(0, 0, 8, 0);
            row.Children.Add(label);
            if (column == FocusLogColumn.Time)
            {
                var picker = new DatePicker { SelectedDate = _filterDay ?? DateTime.Today, Width = 150 };
                if (_filterDay == null) _filterDay = DateTime.Today;
                picker.SelectedDateChanged += (s, e) => { _filterDay = picker.SelectedDate; RenderRows(); };
                row.Children.Add(picker);
            }
            else
            {
                var box = new TextBox { Text = _filterText, Width = 260, Height = 30, FontSize = 13, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(6, 0, 6, 0), ToolTip = Strings.FilterPlaceholder, BorderBrush = Brushes.LightGray };
                box.SetResourceReference(Control.BackgroundProperty, "TextBoxBackgroundColor");
                box.SetResourceReference(Control.ForegroundProperty, "ForegroundColor");
                System.Windows.Automation.AutomationProperties.SetAutomationId(box, "txtFocusLogFilter");
                box.TextChanged += (s, e) => { _filterText = box.Text; RenderRows(); };
                row.Children.Add(box);
                box.Loaded += (s, e) => box.Focus();
            }
            var clear = new Button { Content = new TextBlock { Text = "✖", Foreground = StatsUi.Danger, FontSize = 12 }, Template = StatsUi.PlainTemplate(), Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            clear.Click += (s, e) => { _filterColumn = null; Render(); };
            row.Children.Add(clear);
            return row;
        }

        private FrameworkElement Row(FocusLogEntry entry)
        {
            var grid = Columns();
            grid.Margin = new Thickness(0, 8, 0, 8);
            void Cell(string text, int column, bool wrap)
            {
                var block = StatsUi.Text(text, 12, wrap: wrap);
                if (wrap) { block.MaxHeight = 34; block.TextTrimming = TextTrimming.CharacterEllipsis; }
                block.Margin = new Thickness(0, 0, 8, 0);
                block.ToolTip = text;
                Grid.SetColumn(block, column);
                grid.Children.Add(block);
            }
            Cell(Strings.HumanizeSeconds(FocusLogMath.EffectiveDurationSeconds(entry)), 0, false);
            Cell(RangeText(entry.StartTime, entry.FinishTime), 1, false);
            Cell(FocusLogMath.DisplayText(entry.Intention), 2, true);
            Cell(FocusLogMath.DisplayText(entry.Distractions), 3, true);
            Cell(entry.FocusModeName, 4, false);
            var more = new Button { Content = StatsUi.Text("⋯", 16, secondary: true, wrap: false), Template = StatsUi.PlainTemplate(), Cursor = System.Windows.Input.Cursors.Hand, ToolTip = Strings.More, HorizontalAlignment = HorizontalAlignment.Center };
            System.Windows.Automation.AutomationProperties.SetAutomationId(more, "focusLogMore" + entry.Id);
            more.Click += (s, e) => OpenDetail(entry);
            Grid.SetColumn(more, 5);
            grid.Children.Add(more);
            return grid;
        }

        // ------------------------------------------------------------------ dialogs

        private Window Dialog(string title, double width)
        {
            var window = new Window
            {
                Title = title, Width = width, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = Window.GetWindow(this), ShowInTaskbar = false,
            };
            window.SetResourceReference(Control.BackgroundProperty, "BackgroundColor");
            window.FontFamily = FontFamily;
            return window;
        }

        private FrameworkElement DetailRow(string label, string value)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            var caption = StatsUi.Text(label, 12, FontWeights.SemiBold, secondary: true);
            caption.Width = 120;
            DockPanel.SetDock(caption, Dock.Left);
            row.Children.Add(caption);
            row.Children.Add(StatsUi.Text(value, 13));
            return row;
        }

        private void OpenDetail(FocusLogEntry entry)
        {
            var window = Dialog(entry.FocusModeName.Length == 0 ? Strings.FocusMode : entry.FocusModeName, 540);
            var stack = new StackPanel { Margin = new Thickness(20) };
            var heading = StatsUi.Text(entry.FocusModeName.Length == 0 ? Strings.FocusMode : entry.FocusModeName, 16, FontWeights.SemiBold);
            heading.Margin = new Thickness(0, 0, 0, 14);
            stack.Children.Add(heading);
            stack.Children.Add(DetailRow(Strings.Duration, Strings.HumanizeSeconds(FocusLogMath.EffectiveDurationSeconds(entry))));
            stack.Children.Add(DetailRow(Strings.StartTime, TimeText(entry.StartTime)));
            stack.Children.Add(DetailRow(Strings.FinishTime, TimeText(entry.FinishTime)));
            stack.Children.Add(DetailRow(Strings.Intention, FocusLogMath.DisplayText(entry.Intention)));
            stack.Children.Add(DetailRow(Strings.Achievements, FocusLogMath.DisplayText(entry.Achievements)));
            stack.Children.Add(DetailRow(Strings.Distractions, FocusLogMath.DisplayText(entry.Distractions)));
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            if (entry.IsManual)
                buttons.Children.Add(StatsUi.Button(Strings.Edit, (s, e) => { window.Close(); OpenEditor(entry); }));
            var close = StatsUi.Button(Strings.Close, (s, e) => window.Close(), primary: true);
            close.Margin = new Thickness(0);
            System.Windows.Automation.AutomationProperties.SetAutomationId(close, "btnDetailClose");
            buttons.Children.Add(close);
            stack.Children.Add(buttons);
            window.Content = stack;
            window.ShowDialog();
        }

        internal const int FocusBlockTextMaxLength = 400;

        private TextBox Field(string label, string value, StackPanel into)
        {
            var caption = StatsUi.Text(label, 12, FontWeights.SemiBold, secondary: true);
            caption.Margin = new Thickness(0, 8, 0, 4);
            into.Children.Add(caption);
            // CreateFocusBlockDto caps intention / achievements / distractions at 400; past that the save was refused.
            var box = new TextBox { Text = value, MaxLength = FocusBlockTextMaxLength, Height = 60, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontSize = 13, Padding = new Thickness(4), BorderBrush = Brushes.LightGray };
            box.SetResourceReference(Control.BackgroundProperty, "TextBoxBackgroundColor");
            box.SetResourceReference(Control.ForegroundProperty, "ForegroundColor");
            into.Children.Add(box);
            return box;
        }

        /// <summary>Create a manual session (<paramref name="entry"/> null) or edit one's text.</summary>
        private void OpenEditor(FocusLogEntry entry)
        {
            bool creating = entry == null;
            var window = Dialog(creating ? Strings.AddManualSession : Strings.Edit, 560);
            var stack = new StackPanel { Margin = new Thickness(20) };
            var heading = StatsUi.Text(creating ? Strings.AddManualSession : (entry.FocusModeName.Length > 0 ? entry.FocusModeName : Strings.Edit), 16, FontWeights.SemiBold);
            heading.Margin = new Thickness(0, 0, 0, 10);
            stack.Children.Add(heading);

            DateTimeField start = null, finish = null;
            TextBlock durationText = null, problemText = null;
            Button save = null;
            if (creating)
            {
                start = new DateTimeField(Strings.StartTime, DateTime.Now.AddHours(-1));
                finish = new DateTimeField(Strings.FinishTime, DateTime.Now);
                stack.Children.Add(start);
                stack.Children.Add(finish);
                durationText = StatsUi.Text("", 12, secondary: true);
                durationText.Margin = new Thickness(0, 6, 0, 0);
                stack.Children.Add(durationText);
                problemText = new TextBlock { FontSize = 12, Foreground = StatsUi.Danger, Margin = new Thickness(0, 4, 0, 0) };
                stack.Children.Add(problemText);
            }
            else
            {
                var times = StatsUi.Text(TimeText(entry.StartTime) + " → " + TimeText(entry.FinishTime), 12, secondary: true);
                stack.Children.Add(times);
            }
            var intention = Field(Strings.Intention, creating ? "" : (FocusLogMath.DisplayText(entry.Intention) == "-" ? "" : FocusLogMath.DisplayText(entry.Intention)), stack);
            var achievements = Field(Strings.Achievements, creating ? "" : entry.Achievements ?? "", stack);
            var distractions = Field(Strings.Distractions, creating ? "" : entry.Distractions ?? "", stack);

            Action validate = () =>
            {
                if (!creating || save == null) return;
                var problem = FocusLogMath.Validate(start.Value, finish.Value, DateTime.Now);
                durationText.Text = Strings.Duration + ": " + Strings.HumanizeSeconds(Math.Max((int)(finish.Value - start.Value).TotalSeconds, 0));
                problemText.Text = problem.HasValue ? Strings.Problem(problem.Value) : "";
                problemText.Visibility = problem.HasValue ? Visibility.Visible : Visibility.Collapsed;
                save.IsEnabled = !problem.HasValue;
            };

            var buttons = new DockPanel { Margin = new Thickness(0, 14, 0, 0), LastChildFill = false };
            if (!creating)
            {
                var delete = StatsUi.Button(Strings.Delete, async (s, e) =>
                {
                    var confirm = new FBMessageBox(Strings.ConfirmDeleteSession, Strings.Delete, MessageBoxButton.YesNo, Strings.Delete, Strings.Cancel) { Owner = window };
                    confirm.ShowDialog();
                    if (confirm.btnClicked != MessageBoxResult.Yes) return;
                    window.Close();
                    Finish(await Api().DeleteFocusBlock(entry.Id));
                });
                DockPanel.SetDock(delete, Dock.Left);
                buttons.Children.Add(delete);
            }
            save = StatsUi.Button(Strings.Save, async (s, e) =>
            {
                save.IsEnabled = false;
                bool ok = creating
                    ? await Api().CreateFocusBlock(StatsWire.CreateFocusBlockBody(intention.Text, achievements.Text, distractions.Text, start.Value, finish.Value, FocusLogMath.ManualFocusModeId(_focusModes)))
                    : await Api().UpdateFocusBlock(entry.Id, StatsWire.UpdateFocusBlockBody(intention.Text, achievements.Text, distractions.Text));
                if (ok) window.Close();
                else save.IsEnabled = true;
                Finish(ok);
            }, primary: true);
            System.Windows.Automation.AutomationProperties.SetAutomationId(save, "btnSaveFocusSession");
            save.Margin = new Thickness(0);
            DockPanel.SetDock(save, Dock.Right);
            buttons.Children.Add(save);
            var cancel = StatsUi.Button(Strings.Cancel, (s, e) => window.Close());
            System.Windows.Automation.AutomationProperties.SetAutomationId(cancel, "btnEditorCancel");
            DockPanel.SetDock(cancel, Dock.Right);
            buttons.Children.Add(cancel);
            stack.Children.Add(buttons);
            if (creating)
            {
                start.Changed += validate;
                finish.Changed += validate;
                validate();
            }
            window.Content = stack;
            window.ShowDialog();
        }

        private void Finish(bool success)
        {
            if (success)
            {
                _status = null;
                Load();
                return;
            }
            _status = Strings.SessionSaveFailed;
            Render();
        }

        private void OpenGraph()
        {
            var window = Dialog(Strings.FocusBlockSummary, 760);
            var stack = new StackPanel { Margin = new Thickness(20) };
            var period = ProductivityPeriod.Daily;
            var chartHost = new ContentControl();
            var pickerHost = new ContentControl();
            Action render = null;
            render = () =>
            {
                chartHost.Content = new StatsChart(FocusLogMath.GraphPoints(_entries, period), StatsChart.ChartStyle.Bars, Strings.DurationHours, Strings.Date, Strings.NoFocusSummary,
                    value => (value / 3600).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                pickerHost.Content = StatsUi.Segmented(new[] { ProductivityPeriod.Daily, ProductivityPeriod.Monthly, ProductivityPeriod.Yearly }, period, Strings.PeriodTitle, p => { period = p; render(); });
            };
            render();
            var heading = StatsUi.Text(Strings.FocusBlockSummary, 16, FontWeights.SemiBold);
            heading.Margin = new Thickness(0, 0, 0, 12);
            stack.Children.Add(heading);
            stack.Children.Add(chartHost);
            var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0), LastChildFill = false };
            DockPanel.SetDock(pickerHost, Dock.Left);
            footer.Children.Add(pickerHost);
            var close = StatsUi.Button(Strings.Close, (s, e) => window.Close(), primary: true);
            close.Margin = new Thickness(0);
            System.Windows.Automation.AutomationProperties.SetAutomationId(close, "btnGraphClose");
            DockPanel.SetDock(close, Dock.Right);
            footer.Children.Add(close);
            stack.Children.Add(footer);
            window.Content = stack;
            window.ShowDialog();
        }

        /// <summary>The web handed the CSV to the Mac to save; natively we just write the file.</summary>
        private void ExportCsv()
        {
            var headers = new[] { Strings.FocusMode, Strings.Duration, Strings.StartTime, Strings.FinishTime, Strings.Intention, Strings.Achievements, Strings.Distractions };
            var dialog = new SaveFileDialog { FileName = FocusLogMath.CsvFileName, DefaultExt = ".csv", Filter = "CSV (*.csv)|*.csv" };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            try { File.WriteAllText(dialog.FileName, FocusLogMath.Csv(_entries, headers), new UTF8Encoding(true)); }
            catch (Exception ex) { LogWriter.LogWrite("Stats export csv failed - " + ex.Message); }
        }
    }

    /// <summary>A date picker plus hour and minute, for a manual session's start and finish.</summary>
    internal sealed class DateTimeField : StackPanel
    {
        private readonly DatePicker _date;
        private readonly ComboBox _hour, _minute;

        internal event Action Changed;

        internal DateTimeField(string label, DateTime value)
        {
            Orientation = Orientation.Horizontal;
            Margin = new Thickness(0, 4, 0, 4);
            var caption = StatsUi.Text(label, 12, FontWeights.SemiBold, secondary: true, wrap: false);
            caption.Width = 100;
            caption.VerticalAlignment = VerticalAlignment.Center;
            Children.Add(caption);
            _date = new DatePicker { SelectedDate = value.Date, Width = 140 };
            _hour = new ComboBox { Width = 64, Margin = new Thickness(8, 0, 0, 0) };
            _minute = new ComboBox { Width = 64, Margin = new Thickness(4, 0, 0, 0) };
            for (int h = 0; h < 24; h++) _hour.Items.Add(h.ToString("00"));
            for (int m = 0; m < 60; m++) _minute.Items.Add(m.ToString("00"));
            _hour.SelectedIndex = value.Hour;
            _minute.SelectedIndex = value.Minute;
            _date.SelectedDateChanged += (s, e) => Changed?.Invoke();
            _hour.SelectionChanged += (s, e) => Changed?.Invoke();
            _minute.SelectionChanged += (s, e) => Changed?.Invoke();
            Children.Add(_date);
            Children.Add(_hour);
            Children.Add(new TextBlock { Text = ":", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) });
            Children.Add(_minute);
        }

        internal DateTime Value => (_date.SelectedDate ?? DateTime.Today).Date.AddHours(Math.Max(_hour.SelectedIndex, 0)).AddMinutes(Math.Max(_minute.SelectedIndex, 0));
    }
}
