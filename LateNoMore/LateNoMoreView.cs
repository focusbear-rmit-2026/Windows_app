using Focus_Bear.Habits.DayPlan;
using Focus_Bear.Utils;
using FocusBear.Core.LateNoMore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace Focus_Bear.LateNoMore
{
    /// <summary>
    /// TOOLS → Late No More: the Mac Late No More app's settings window (<c>ViewController</c>: "Which calendars do you want
    /// to get notifications for?", "What hours do you want to get verbal alerts?", "Choose keywords to ignore") plus the
    /// two alert timings (fixed at 15 and 2 minutes on the Mac, a list on mobile) and mobile's "only meetings with a
    /// link". Every change is saved at once (no Apply button), locally and to <c>/late-no-more/config</c>.
    /// </summary>
    public sealed class LateNoMoreView : UserControl
    {
        internal static readonly int[] MainChoices = { 1, 2, 3, 5, 10 };
        internal static readonly int[] HeadsUpChoices = { 0, 5, 10, 15, 30, 60 };

        private readonly LateNoMoreService _service;
        private readonly StackPanel _body = new StackPanel();
        private readonly TextBox _keywordInput;
        private readonly TextBlock _keywordHint;
        private readonly DispatcherTimer _saveDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        private readonly DispatcherTimer _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        private ToggleButton _enabled;
        private LateNoMoreConfig _config;
        private List<LateNoMoreCalendar> _calendars = new List<LateNoMoreCalendar>();
        private bool _calendarsLoaded, _renderQueued;
        private string _keywordError;

        public LateNoMoreView() : this(LateNoMoreService.Shared) { }

        internal LateNoMoreView(LateNoMoreService service)
        {
            _service = service;
            Resources[typeof(Grid)] = new Style(typeof(Grid));   // the app's page-background Grid style would show inside the cards
            LogWriter.LogWrite("LateNoMoreView() Initialized");
            AutomationProperties.SetAutomationId(this, "LateNoMoreView");
            SetResourceReference(BackgroundProperty, "BackgroundColor");
            LnmUi.ApplyAppFont(this);

            _keywordInput = LnmUi.Input("txtLnmKeyword", "Keyword to ignore");
            _keywordInput.Width = 260;
            _keywordInput.MaxLength = LateNoMoreConfig.MaxKeywordLength;
            _keywordInput.KeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; AddKeyword(); } };
            _keywordHint = LnmUi.Text("Add a word, e.g. Lunch or Focus time", 13, secondary: true, wrap: false);
            _keywordHint.IsHitTestVisible = false;
            _keywordHint.Margin = new Thickness(10, 0, 0, 0);
            _keywordHint.VerticalAlignment = VerticalAlignment.Center;
            _keywordInput.TextChanged += (s, e) =>
            {
                _keywordHint.Visibility = string.IsNullOrEmpty(_keywordInput.Text) ? Visibility.Visible : Visibility.Collapsed;
                if (_keywordError != null) { _keywordError = null; QueueRender(); }
            };

            _config = _service.Config;
            _saveDebounce.Tick += async (s, e) => { _saveDebounce.Stop(); await SaveNow(); };
            _statusTimer.Tick += (s, e) => QueueRender();
            Content = BuildLayout();
            Render();

            _service.ConfigChanged += OnServiceConfigChanged;
            IsVisibleChanged += (s, e) =>
            {
                if (IsVisible) { _statusTimer.Start(); _ = RefreshAsync(); }
                else _statusTimer.Stop();
            };
        }

        private void OnServiceConfigChanged()
        {
            if (_saveDebounce.IsEnabled) return;   // an edit is waiting to be saved: it wins
            Dispatcher.BeginInvoke(new Action(() => { _config = _service.Config; QueueRender(); }));
        }

        /// <summary>Re-reads the settings and the calendar list (GETs only).</summary>
        internal async Task RefreshAsync()
        {
            try
            {
                var pull = _service.PullConfigAsync();
                var calendars = DayPlanCalendarManager.Shared.LoadCalendarsAsync();
                // The next two days of meetings for the status line (skipped when the cache is fresh).
                // EnsureWindowAsync waits for a refresh already running instead of skipping it.
                var meetings = DayPlanCalendarManager.Shared.EnsureWindowAsync(2, LateNoMoreService.CalendarFreshSeconds);
                await Task.WhenAll(pull, calendars, meetings);
                if (!_saveDebounce.IsEnabled) _config = pull.Result;
                _calendars = calendars.Result ?? new List<LateNoMoreCalendar>();
                _calendarsLoaded = true;
                QueueRender();
            }
            catch (Exception ex) { LogWriter.LogWrite("LateNoMoreView.RefreshAsync - " + ex.Message); }
        }

        /// <summary>For the harness: show these calendars instead of reading them.</summary>
        internal void UseCalendars(IEnumerable<LateNoMoreCalendar> calendars)
        {
            _calendars = calendars?.ToList() ?? new List<LateNoMoreCalendar>();
            _calendarsLoaded = true;
            QueueRender();
        }

        // ------------------------------------------------------------------ layout

        private FrameworkElement BuildLayout()
        {
            var title = LnmUi.Text(LnmUi.S("Late No More", "leftMenuVcInfo", "menuLateNoMore"), 22, FontWeights.SemiBold, wrap: false);
            var description = LnmUi.Text("A heads-up before each meeting, then an alert with a countdown and a one-click join. " +
                "When you join, a focus session on your Meetings block list covers the meeting.", 13, secondary: true);
            description.Margin = new Thickness(0, 6, 0, 0);
            description.MaxWidth = 620;
            description.HorizontalAlignment = HorizontalAlignment.Left;
            var titles = new StackPanel();
            titles.Children.Add(title);
            titles.Children.Add(description);

            _enabled = LnmUi.Switch(_config.Enabled, "tglLnmEnabled", "Late No More alerts");
            _enabled.Checked += (s, e) => Edit(c => c.Enabled = true);
            _enabled.Unchecked += (s, e) => Edit(c => c.Enabled = false);
            var enabledRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(24, 4, 0, 0) };
            var enabledLabel = LnmUi.Text("Alerts", 13, FontWeights.SemiBold, wrap: false);
            enabledLabel.VerticalAlignment = VerticalAlignment.Center;
            enabledLabel.Margin = new Thickness(0, 0, 10, 0);
            enabledRow.Children.Add(enabledLabel);
            enabledRow.Children.Add(_enabled);

            var header = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(enabledRow, Dock.Right);
            header.Children.Add(enabledRow);
            header.Children.Add(titles);

            var top = new StackPanel { Margin = new Thickness(0, 22, 0, 14) };
            top.Children.Add(header);

            var column = new DockPanel { MaxWidth = 900, Margin = new Thickness(28, 0, 28, 0) };
            DockPanel.SetDock(top, Dock.Top);
            column.Children.Add(top);
            var divider = LnmUi.Divider(new Thickness(0));
            DockPanel.SetDock(divider, Dock.Top);
            column.Children.Add(divider);

            _body.Margin = new Thickness(0, 18, 16, 28);
            column.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _body, Focusable = false });
            return column;
        }

        private void QueueRender()
        {
            if (_renderQueued) return;
            _renderQueued = true;
            Dispatcher.BeginInvoke(new Action(() => { _renderQueued = false; Render(); }), DispatcherPriority.Background);
        }

        private void Render()
        {
            if (_enabled.IsChecked != _config.Enabled) _enabled.IsChecked = _config.Enabled;
            bool keywordFocused = _keywordInput.IsKeyboardFocused;
            _body.Children.Clear();
            _body.Children.Add(StatusCard());
            var settings = new StackPanel { IsEnabled = _config.Enabled, Opacity = _config.Enabled ? 1 : 0.5 };
            settings.Children.Add(Section("When to alert", TimingCard()));
            settings.Children.Add(Section("Calendars", CalendarsCard(), "Which calendars do you want alerts for?"));
            settings.Children.Add(Section("Ignore meetings containing", KeywordsCard(), "Meetings whose title contains one of these words don't alert."));
            _body.Children.Add(settings);
            _body.Children.Add(TestRow());
            if (keywordFocused) Dispatcher.BeginInvoke(new Action(() => _keywordInput.Focus()), DispatcherPriority.Input);
        }

        private static StackPanel Section(string title, FrameworkElement content, string caption = null)
        {
            var section = new StackPanel { Margin = new Thickness(0, 0, 0, 24) };
            var heading = LnmUi.Text(title, 15, FontWeights.SemiBold, wrap: false);
            heading.Margin = new Thickness(2, 0, 0, caption == null ? 10 : 2);
            section.Children.Add(heading);
            if (caption != null)
            {
                var c = LnmUi.Text(caption, 12, secondary: true);
                c.Margin = new Thickness(2, 0, 0, 10);
                section.Children.Add(c);
            }
            section.Children.Add(content);
            return section;
        }

        private static Border RowsCard(IList<FrameworkElement> rows)
        {
            var stack = new StackPanel();
            for (int i = 0; i < rows.Count; i++)
            {
                if (i > 0) stack.Children.Add(LnmUi.Divider(new Thickness(14, 0, 14, 0)));
                stack.Children.Add(rows[i]);
            }
            return LnmUi.Card(stack, new Thickness(0));
        }

        /// <summary>A settings row: title and caption on the left, a control on the right.</summary>
        private static FrameworkElement Row(string title, string caption, FrameworkElement control, string automationId)
        {
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
            text.Children.Add(LnmUi.Text(title, 14, FontWeights.SemiBold));
            if (!string.IsNullOrEmpty(caption))
            {
                var c = LnmUi.Text(caption, 12, secondary: true);
                c.Margin = new Thickness(0, 2, 0, 0);
                text.Children.Add(c);
            }
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(text);
            if (control != null)
            {
                control.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(control, 1);
                grid.Children.Add(control);
            }
            var row = new Border { Child = grid, Padding = new Thickness(16, 12, 14, 12) };
            AutomationProperties.SetAutomationId(row, automationId);
            return row;
        }

        // ------------------------------------------------------------------ status

        private FrameworkElement StatusCard()
        {
            var rows = new StackPanel();
            DateTime now = DateTime.Now;
            string line;
            int glyph = LnmUi.GlyphCalendar;
            if (!_config.Enabled) { line = "Alerts are off."; glyph = LnmUi.GlyphInfo; }
            else
            {
                var next = LateNoMoreScheduler.NextAlerting(_config, _service.MeetingsAhead(), now, _service.Dismissals);
                if (next == null)
                    line = DayPlanCalendarManager.Shared.LastRefreshUtc == DateTime.MinValue ? "Reading your calendar…" : "No meetings to alert for today or tomorrow.";
                else
                {
                    int lead = _config.AlertLeadMinutes.DefaultIfEmpty(2).Max();
                    string day = next.Start.Date == now.Date ? "" : (next.Start.Date == now.Date.AddDays(1) ? "tomorrow " : next.Start.ToString("ddd ", CultureInfo.CurrentCulture));
                    line = "Next: " + next.Title.Trim() + " at " + day + next.Start.ToString("t", CultureInfo.CurrentCulture)
                           + " — first alert at " + next.Start.AddMinutes(-lead).ToString("t", CultureInfo.CurrentCulture) + ".";
                }
            }
            rows.Children.Add(IconLine(glyph, line, "txtLnmStatus", secondary: false));

            // TODO(late-no-more): drop this note once /late-no-more/config is deployed (backend PR feat/late-no-more-config).
            if (_service.RemoteState == LateNoMoreRemoteState.NotDeployed)
                rows.Children.Add(IconLine(LnmUi.GlyphInfo, "Saved on this computer for now. Your account will keep these settings once Focus Bear's servers support them.", "txtLnmLocalOnly", secondary: true));
            else if (_service.HasPendingUpload && (_service.RemoteState == LateNoMoreRemoteState.Offline || _service.RemoteState == LateNoMoreRemoteState.Error))
                rows.Children.Add(IconLine(LnmUi.GlyphWarning, "Saved on this computer. It will sync to your account when Focus Bear is back online.", "txtLnmPending", secondary: true));

            var card = LnmUi.Card(rows, new Thickness(16, 12, 16, 12));
            card.Margin = new Thickness(0, 0, 0, 24);
            return card;
        }

        private static FrameworkElement IconLine(int glyph, string text, string automationId, bool secondary)
        {
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            var icon = LnmUi.Glyph(glyph, 14, secondary ? "TraySubtitleColor" : "ForegroundColor");
            icon.Margin = new Thickness(0, 1, 10, 0);
            icon.VerticalAlignment = VerticalAlignment.Top;
            if (!secondary) icon.Foreground = LnmUi.Accent;
            DockPanel.SetDock(icon, Dock.Left);
            row.Children.Add(icon);
            var t = LnmUi.Text(text, 13, secondary ? FontWeights.Normal : FontWeights.SemiBold, secondary: secondary);
            AutomationProperties.SetAutomationId(t, automationId);
            row.Children.Add(t);
            return row;
        }

        // ------------------------------------------------------------------ timing

        private FrameworkElement TimingCard()
        {
            int main = _config.MainAlertMinutes ?? 2;
            int headsUp = _config.HeadsUpMinutes ?? 0;
            var mainPicker = Picker(MainChoices.Union(new[] { main }).OrderBy(m => m).Select(m => new Choice(m, Minutes(m) + " before")), main, "cmbLnmMainLead",
                m => Edit(c => c.SetLeads(m, c.HeadsUpMinutes)));
            var headsUpPicker = Picker(HeadsUpChoices.Union(new[] { headsUp }).OrderBy(m => m).Select(m => new Choice(m, m == 0 ? "Off" : Minutes(m) + " before")), headsUp, "cmbLnmHeadsUp",
                m => Edit(c => c.SetLeads(c.MainAlertMinutes ?? 2, m == 0 ? (int?)null : m)));

            var hours = new StackPanel { Orientation = Orientation.Horizontal };
            LateNoMoreConfig.TryParseClock(_config.ActiveHoursStart, out int from);
            LateNoMoreConfig.TryParseClock(_config.ActiveHoursEnd, out int to);
            hours.Children.Add(ClockPicker(from, "cmbLnmHoursFrom", v => Edit(c => c.ActiveHoursStart = LateNoMoreConfig.FormatClock(v))));
            var and = LnmUi.Text("to", 13, secondary: true, wrap: false);
            and.VerticalAlignment = VerticalAlignment.Center;
            and.Margin = new Thickness(10, 0, 10, 0);
            hours.Children.Add(and);
            hours.Children.Add(ClockPicker(to, "cmbLnmHoursTo", v => Edit(c => c.ActiveHoursEnd = LateNoMoreConfig.FormatClock(v))));

            var requireLink = LnmUi.Switch(_config.RequireMeetingUrl, "tglLnmRequireLink", "Only meetings with a join link");
            requireLink.Checked += (s, e) => Edit(c => c.RequireMeetingUrl = true);
            requireLink.Unchecked += (s, e) => Edit(c => c.RequireMeetingUrl = false);
            var verbal = LnmUi.Switch(_config.VerbalAlertsEnabled, "tglLnmVerbal", "Spoken alerts");
            verbal.Checked += (s, e) => Edit(c => c.VerbalAlertsEnabled = true);
            verbal.Unchecked += (s, e) => Edit(c => c.VerbalAlertsEnabled = false);

            return RowsCard(new List<FrameworkElement>
            {
                Row("Meeting alert", "The alert with the countdown and the Join button.", mainPicker, "rowLnmMainLead"),
                Row("Heads-up", "A small notice a while before, so you can wrap up.", headsUpPicker, "rowLnmHeadsUp"),
                Row("Alert hours", "Only meetings starting in these hours alert.", hours, "rowLnmHours"),
                Row("Only meetings with a join link", "Skip in-person events and reminders that have no video link.", requireLink, "rowLnmRequireLink"),
                Row("Spoken alerts", "If you haven't joined when the meeting starts, say so out loud for up to 5 minutes.", verbal, "rowLnmVerbal"),
            });
        }

        private static string Minutes(int m) => m == 60 ? "1 hour" : m + (m == 1 ? " minute" : " minutes");

        /// <summary>A picker entry. Public with a lowercase <c>name</c>: the app's RoundedComboBoxStyle binds <c>SelectedItem.name</c>.</summary>
        public sealed class Choice
        {
            public Choice(int value, string name) { Value = value; this.name = name; }
            public int Value { get; }
            public string name { get; }
            public override string ToString() => name;
        }

        private ComboBox Picker(IEnumerable<Choice> choices, int selected, string automationId, Action<int> changed)
        {
            var picker = new ComboBox { Width = 170, Height = 32, FontSize = 13 };
            if (TryFindResource("RoundedComboBoxStyle") is Style rounded) picker.Style = rounded;
            foreach (Choice c in choices) picker.Items.Add(c);
            picker.SelectedItem = picker.Items.Cast<Choice>().FirstOrDefault(c => c.Value == selected);
            picker.SelectionChanged += (s, e) => { if (picker.SelectedItem is Choice c) changed(c.Value); };
            AutomationProperties.SetAutomationId(picker, automationId);
            return picker;
        }

        private ComboBox ClockPicker(int minutes, string automationId, Action<int> changed)
        {
            var values = Enumerable.Range(0, 48).Select(i => i * 30).Union(new[] { minutes }).OrderBy(v => v);
            var picker = Picker(values.Select(v => new Choice(v, DateTime.Today.AddMinutes(v).ToString("t", CultureInfo.CurrentCulture))), minutes, automationId, changed);
            picker.Width = 110;
            picker.MaxDropDownHeight = 280;
            return picker;
        }

        // ------------------------------------------------------------------ calendars

        private FrameworkElement CalendarsCard()
        {
            if (!_calendarsLoaded)
                return LnmUi.Card(IconLine(LnmUi.GlyphCalendar, "Loading your calendars…", "txtLnmCalendarsLoading", secondary: true), new Thickness(16, 12, 16, 12));
            if (_calendars.Count == 0)
            {
                var none = new StackPanel();
                none.Children.Add(IconLine(LnmUi.GlyphInfo, "No calendars are connected to Focus Bear yet. Connect Google Calendar or Outlook, then come back here.", "txtLnmNoCalendars", secondary: true));
                var connect = LnmUi.TextButton("Connect a calendar", () => Open("https://dashboard.focusbear.io/integrations"), "btnLnmConnectCalendar", colour: LnmUi.Accent);
                connect.HorizontalAlignment = HorizontalAlignment.Left;
                connect.Margin = new Thickness(18, 6, 0, 0);
                none.Children.Add(connect);
                return LnmUi.Card(none, new Thickness(16, 12, 16, 12));
            }
            var synced = _calendars.Where(c => c.IsSynced).Select(c => c.CalendarId).ToList();
            var rows = new List<FrameworkElement>();
            foreach (var group in _calendars.GroupBy(c => c.Account ?? "").OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (_calendars.Select(c => c.Account).Distinct().Count() > 1 || !string.IsNullOrEmpty(group.Key))
                {
                    var head = LnmUi.Text(group.Key + (group.First().Platform == "microsoft" ? "  ·  Outlook" : "  ·  Google"), 12, FontWeights.SemiBold, secondary: true, wrap: false);
                    rows.Add(new Border { Child = head, Padding = new Thickness(16, 10, 14, 8) });
                }
                foreach (var cal in group.OrderByDescending(c => c.IsSynced).ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase))
                    rows.Add(CalendarRow(cal, synced));
            }
            return RowsCard(rows);
        }

        private FrameworkElement CalendarRow(LateNoMoreCalendar cal, List<string> synced)
        {
            bool on = cal.IsSynced && _config.IsCalendarOn(cal.CalendarId);
            var box = new Border { Width = 20, Height = 20, CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(on ? 0 : 1.5), Margin = new Thickness(0, 0, 12, 0) };
            if (on) { box.Background = LnmUi.Accent; box.Child = new TextBlock { Text = ((char)LnmUi.GlyphCheck).ToString(), FontFamily = LnmUi.Symbols, FontSize = 12, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }; }
            else box.SetResourceReference(Border.BorderBrushProperty, "TraySubtitleColor");
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(LnmUi.Text(cal.Name, 14, wrap: false));
            if (!cal.IsSynced)
            {
                var why = LnmUi.Text("Not synced to Focus Bear, so it has no meetings here", 12, secondary: true);
                text.Children.Add(why);
            }
            var row = new DockPanel();
            DockPanel.SetDock(box, Dock.Left);
            row.Children.Add(box);
            row.Children.Add(text);
            var face = new Border { Child = row, Padding = new Thickness(16, 10, 14, 10) };
            if (!cal.IsSynced)
            {
                face.Opacity = 0.55;
                AutomationProperties.SetAutomationId(face, "rowLnmCalendar");
                AutomationProperties.SetName(face, cal.Name);
                return face;
            }
            var button = LnmUi.Clickable(face, () => Edit(c => c.SetCalendarOn(cal.CalendarId, !on, synced)), "rowLnmCalendar", cal.Name);
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            AutomationProperties.SetItemStatus(button, on ? "on" : "off");
            button.MouseEnter += (s, e) => face.SetResourceReference(Border.BackgroundProperty, "TrayCardBorder");
            button.MouseLeave += (s, e) => face.Background = Brushes.Transparent;
            return button;
        }

        // ------------------------------------------------------------------ keywords

        private FrameworkElement KeywordsCard()
        {
            var stack = new StackPanel();
            var chips = new WrapPanel();
            foreach (string k in _config.IgnoredKeywords)
            {
                string keyword = k;
                chips.Children.Add(LnmUi.Chip(keyword, () => Edit(c => c.RemoveKeyword(keyword)), "btnLnmRemoveKeyword"));
            }
            if (_config.IgnoredKeywords.Count == 0)
            {
                var none = LnmUi.Text("No keywords: every meeting alerts.", 13, secondary: true);
                none.Margin = new Thickness(0, 0, 0, 10);
                stack.Children.Add(none);
            }
            else stack.Children.Add(chips);

            var add = new StackPanel { Orientation = Orientation.Horizontal };
            if (_keywordInput.Parent is Panel old) old.Children.Remove(_keywordInput);   // kept across renders (typing survives)
            if (_keywordHint.Parent is Panel oldHint) oldHint.Children.Remove(_keywordHint);
            var field = new Grid();
            field.Children.Add(_keywordInput);
            field.Children.Add(_keywordHint);
            add.Children.Add(field);
            var addButton = LnmUi.Pill(LnmUi.S("Add", "focusMusicInfo", "addButtonText"), true, AddKeyword, "btnLnmAddKeyword", LnmUi.GlyphAdd, 13);
            addButton.Margin = new Thickness(10, 0, 0, 0);
            add.Children.Add(addButton);
            stack.Children.Add(add);
            if (_keywordError != null)
            {
                var err = LnmUi.Text(_keywordError, 12);
                err.Foreground = LnmUi.LateRed;
                err.Margin = new Thickness(2, 6, 0, 0);
                AutomationProperties.SetAutomationId(err, "txtLnmKeywordError");
                stack.Children.Add(err);
            }
            return LnmUi.Card(stack, new Thickness(16, 14, 16, 14));
        }

        private void AddKeyword()
        {
            string k = (_keywordInput.Text ?? "").Trim();
            if (k.Length == 0) return;
            var copy = _config.Clone();
            if (!copy.AddKeyword(k))
            {
                _keywordError = copy.IgnoredKeywords.Count >= LateNoMoreConfig.MaxKeywords ? "That's the most keywords you can have." : "That keyword is already in the list.";
                QueueRender();
                return;
            }
            _keywordInput.Text = "";
            Edit(c => c.AddKeyword(k));
        }

        // ------------------------------------------------------------------ test + saving

        private FrameworkElement TestRow()
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var test = LnmUi.Pill("Test alert", false, () => _service.ShowTestAlert(), "btnLnmTest", LnmUi.GlyphClock);
            DockPanel.SetDock(test, Dock.Left);
            row.Children.Add(test);
            var caption = LnmUi.Text("Shows the meeting alert for a pretend meeting two minutes from now. Nothing is started or sent.", 12, secondary: true);
            caption.VerticalAlignment = VerticalAlignment.Center;
            caption.Margin = new Thickness(14, 0, 0, 0);
            row.Children.Add(caption);
            return row;
        }

        private void Edit(Action<LateNoMoreConfig> change)
        {
            var copy = _config.Clone();
            change(copy);
            copy.Normalize();
            if (copy.ToUpdateJson() == _config.ToUpdateJson()) return;
            _config = copy;
            _saveDebounce.Stop();
            _saveDebounce.Start();
            QueueRender();
        }

        private async Task SaveNow()
        {
            try
            {
                var saved = await _service.SaveConfigAsync(_config);
                if (!_saveDebounce.IsEnabled) _config = saved;
                QueueRender();
            }
            catch (Exception ex) { LogWriter.LogWrite("LateNoMoreView.SaveNow - " + ex.Message); }
        }

        /// <summary>For the harness: apply any pending edit now.</summary>
        internal Task FlushAsync()
        {
            if (!_saveDebounce.IsEnabled) return Task.CompletedTask;
            _saveDebounce.Stop();
            return SaveNow();
        }

        private static void Open(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { LogWriter.LogWrite("LateNoMoreView.Open - " + ex.Message); }
        }
    }
}
