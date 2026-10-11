using Focus_Bear.Habits.DayPlan;
using Focus_Bear.Pomodoro;
using Focus_Bear.Utils;
using FocusBear.Core.Blocking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Focus_Bear.Focus
{
    /// <summary>What the check-in needs from the blocking code, and what it hands back.</summary>
    internal sealed class AiBlockBuddyRequest
    {
        internal bool IsApp;
        internal string Label = "";
        internal string Url = "";
        internal string PageTitle = "";
        internal string Reason = "";
        internal double? Probability;
        internal string CurrentTask = "";
        internal Func<string, Task<double?>> Argue;
        internal Action<int, string> Allow;
        internal Action Block;
        internal Action<DayPlanTask> PickTask;
        internal Action Report;
        /// <summary>A page check-in: whether the browser is still on the page (null for an app).</summary>
        internal Func<bool> StillOnItem;
        /// <summary>The process the check-in is about: the blocked app, or the page's browser.</summary>
        internal string ItemProcess = "";
        internal Action Dismissed;
    }

    /// <summary>
    /// The AI block check-in (the Mac's "AI Blocking Latest" <c>AiBlockBuddyView</c>): instead of a bare block
    /// page, a reminder of the top Day Plan tasks, why this looks like it could wait, and three ways on —
    /// explain why it is relevant (the AI re-checks with the explanation and can allow it for a while), save
    /// it as a task for later, or switch the current task. Closing blocks it; leaving the page or app just
    /// dismisses the check-in.
    /// </summary>
    internal sealed class AiBlockBuddyWindow : Window
    {
        private enum Phase { Choose, Argue, Save }

        private readonly AiBlockBuddyRequest _r;
        private readonly AppSettings _settings = AppSettings.GetInstance;
        private readonly FocusStartPalette _p = FocusStartPalette.Current();
        private readonly Border _body = new Border();
        private readonly DispatcherTimer _leaveWatch = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(500) };
        private DateTime _shownAt = DateTime.Now;
        private readonly List<Tuple<bool, string>> _argument = new List<Tuple<bool, string>>();
        private Phase _phase = Phase.Choose;
        private bool _thinking, _argueFailed, _pickerOpen, _resolved;
        private double? _argueProbability;
        private int _allowMinutes = AiBlockBuddy.DefaultAllowMinutes;
        private readonly TextBox _argueBox = new TextBox();
        private readonly TextBox _pickerBox = new TextBox();

        internal AiBlockBuddyWindow(AiBlockBuddyRequest request)
        {
            _r = request;
            Title = "Focus Bear";
            Focus_Bear.Utils.WindowTheme.FollowTheme(this);
            Resources[typeof(Grid)] = new Style(typeof(Grid));   // the app's page-background Grid style would show inside the cards
            Width = 640;
            Height = Math.Max(480, Math.Min(760, SystemParameters.WorkArea.Height * 0.8));
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            ShowInTaskbar = false;
            SetResourceReference(BackgroundProperty, "BackgroundColor");
            AutomationProperties.SetAutomationId(this, "wndAiBlockBuddy");
            Content = _body;
            Draw();
            // Mac AiBlockBuddyDismissObserver: once the user moves off the app or page - another app in front,
            // Focus Bear's own main window, or the browser off the page - the check-in goes: no block, no allow.
            // Coming back runs the check again.
            _leaveWatch.Tick += (s, e) =>
            {
                if (_resolved) return;
                var front = BlockForeground.Classify(out string frontProcess);
                bool onPage = _r.StillOnItem == null || _r.StillOnItem();
                if (!AiBlockBuddy.HasLeftItem(front, frontProcess, _r.ItemProcess, _r.IsApp, onPage, DateTime.Now - _shownAt)) return;
                LogWriter.LogWrite("AiBlockBuddy - left " + _r.Label + " (in front: " + front + " " + frontProcess + (onPage ? "" : ", off the page") + "), dismissing");
                Resolve(_r.Dismissed);
            };
            Loaded += (s, e) => { _shownAt = DateTime.Now; _leaveWatch.Start(); };
            IsVisibleChanged += (s, e) => { if (IsVisible) _settings.NotifyBlockPopupShown(); };   // on the backdrop, never under it
            Closed += (s, e) =>
            {
                _leaveWatch.Stop();
                // The title-bar close is "Block" (Mac #2577): there is no separate Block button.
                if (!_resolved) { _resolved = true; _r.Block?.Invoke(); }
            };
        }

        private string S(string key, string fallback) => _settings.GetConfigStringSafe(fallback, "aiBlockBuddyInfo", key);

        /// <summary>A check-in for another app or page is taking over: go without blocking or allowing.</summary>
        internal void DismissStale() => Resolve(_r.Dismissed);

        private void Resolve(Action action)
        {
            if (_resolved) return;
            _resolved = true;
            _leaveWatch.Stop();
            action?.Invoke();
            Close();
        }

        private void Draw()
        {
            FrameworkElement content;
            switch (_phase)
            {
                case Phase.Argue: content = ArgueView(); break;
                case Phase.Save: content = SaveView(); break;
                default: content = ChooseView(); break;
            }
            _body.Child = content;
        }

        // ------------------------------------------------------------------ choose

        private static List<DayPlanTask> TopTasks()
        {
            try { return DayPlanTaskStore.Shared.Tasks(DateTime.Today).Where(t => !t.Done).Take(3).ToList(); }
            catch (Exception ex) { LogWriter.LogWrite("AiBlockBuddy TopTasks - " + ex.Message); return new List<DayPlanTask>(); }
        }

        private FrameworkElement ChooseView()
        {
            var stack = new StackPanel { Margin = new Thickness(28, 22, 28, 12) };
            var tasks = TopTasks();
            stack.Children.Add(Text(tasks.Count == 0 ? S("noTasks", "You don't have any tasks in your Day Plan yet. Add a couple so your buddy can help you prioritise.") : S("topTasksIntro", "Here's a reminder of the top tasks in your day plan:"), 13, _p.Secondary));
            if (tasks.Count > 0)
            {
                var cards = new System.Windows.Controls.Primitives.UniformGrid { Columns = tasks.Count, Margin = new Thickness(0, 10, 0, 0) };
                foreach (var task in tasks) cards.Children.Add(TaskCard(task));
                stack.Children.Add(cards);
            }
            var bear = new Image { Source = new BitmapImage(new Uri("pack://application:,,,/Focus Bear;component/Images/FocusStart/aiBuddyBear.png")), Height = 120, Margin = new Thickness(0, 14, 0, 6), HorizontalAlignment = HorizontalAlignment.Center };
            RenderOptions.SetBitmapScalingMode(bear, BitmapScalingMode.HighQuality);   // the art is far larger than 120px; the default scaler pixelates it
            stack.Children.Add(bear);

            bool noGoal = string.IsNullOrWhiteSpace(_r.CurrentTask);
            var heading = Text(S("couldWaitTitle", "This looks like it could wait..."), 24, _p.Primary, FontWeights.SemiBold);
            heading.TextAlignment = TextAlignment.Center;
            AutomationProperties.SetAutomationId(heading, "txtAiBlockHeading");
            stack.Children.Add(heading);
            var subline = new DockPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
            var info = FocusStartUi.Glyph(0xE946, 14, _p.Secondary);
            info.Margin = new Thickness(8, 0, 0, 0);
            info.ToolTip = string.IsNullOrWhiteSpace(_r.Reason) ? S("aiDecisionNoDetail", "No AI reason recorded for this decision.")
                : S("aiReasonPrefix", "AI reason:") + " " + _r.Reason + (AiBlockBuddy.ScoreLabel(_r.Probability).Length > 0 ? "\n" + S("alignmentScorePrefix", "Focus alignment:") + " " + AiBlockBuddy.ScoreLabel(_r.Probability) : "");
            ToolTipService.SetInitialShowDelay(info, 100);
            AutomationProperties.SetAutomationId(info, "icoAiBlockReason");
            DockPanel.SetDock(info, Dock.Right);
            subline.Children.Add(info);
            var sub = Text(noGoal ? S("lowerPriority", "Hey — it looks like you might be working on something lower priority ({0}).").Replace("{0}", _r.Label)
                                  : S("alignToCurrentTaskPrefix", "{0} doesn't seem to align with your current task").Replace("{0}", _r.Label), 14, _p.Primary);
            sub.TextAlignment = TextAlignment.Center;
            sub.MaxWidth = 520;
            AutomationProperties.SetAutomationId(sub, "txtAiBlockSubline");
            subline.Children.Add(sub);
            stack.Children.Add(subline);

            if (!noGoal) stack.Children.Add(CurrentTaskRow());
            if (_pickerOpen) stack.Children.Add(Picker());

            var buttons = new StackPanel { Margin = new Thickness(28, 8, 28, 18) };
            var argue = Primary(S("argueButton", "This is relevant - let me explain"), () => { _phase = Phase.Argue; Draw(); _argueBox.Focus(); }, "btnAiBlockArgue");
            buttons.Children.Add(argue);
            var save = Secondary(S("saveCurrentButton", "Save as task for later"), () => { _phase = Phase.Save; Draw(); }, "btnAiBlockSave");
            save.Margin = new Thickness(0, 8, 0, 0);
            buttons.Children.Add(save);
            var report = FocusStartUi.Clickable(Text(S("reportBadAiTooltip", "Report a bad AI decision"), 12, _p.Secondary), () => Resolve(_r.Report), "btnAiBlockReport");
            report.HorizontalAlignment = HorizontalAlignment.Center;
            report.Margin = new Thickness(0, 10, 0, 0);
            buttons.Children.Add(report);
            return Layout(stack, buttons);
        }

        private FrameworkElement TaskCard(DayPlanTask task)
        {
            var stack = new StackPanel();
            var title = Text(task.Title, 13, _p.Primary, FontWeights.Medium);
            title.MaxHeight = 36;
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.ToolTip = task.Title;
            stack.Children.Add(title);
            if (!string.IsNullOrWhiteSpace(task.Project))
            {
                var chip = FocusStartUi.Badge("#" + task.Project, _p.Metadata, _p.Accent, new FocusStartScale(1675));
                chip.HorizontalAlignment = HorizontalAlignment.Left;
                chip.Margin = new Thickness(0, 6, 0, 0);
                stack.Children.Add(chip);
            }
            var row = new DockPanel();
            var play = FocusStartUi.Glyph(0xE768, 14, _p.Accent);
            play.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(play, Dock.Right);
            row.Children.Add(play);
            row.Children.Add(stack);
            var face = new Border { Child = row, Background = _p.Surface, BorderBrush = _p.Border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12), Margin = new Thickness(0, 0, 8, 0) };
            var button = FocusStartUi.Clickable(face, () => SwitchTo(task), "btnAiBlockTopTask", S("pickHint", "Pick a task to switch to it") + ": " + task.Title);
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            return button;
        }

        private FrameworkElement CurrentTaskRow()
        {
            var row = new DockPanel();
            var badge = FocusStartUi.Badge(S("currentTaskBadge", "CURRENT TASK"), _p.Selection, _p.Accent, new FocusStartScale(1675));
            DockPanel.SetDock(badge, Dock.Right);
            row.Children.Add(badge);
            var name = Text(_r.CurrentTask, 14, _p.Primary, FontWeights.Medium);
            AutomationProperties.SetAutomationId(name, "txtAiBlockCurrentTask");
            row.Children.Add(name);
            var face = new Border { Child = row, Background = _p.Field, BorderBrush = _p.Border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 16, 0, 0) };
            var button = FocusStartUi.Clickable(face, () => { _pickerOpen = !_pickerOpen; Draw(); if (_pickerOpen) _pickerBox.Focus(); }, "btnAiBlockCurrentTask", S("changeCurrentTaskHint", "Change your current task"));
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            return button;
        }

        /// <summary>A typeahead over today's unfinished tasks; Enter on text that matches none adds it as a new task.</summary>
        private FrameworkElement Picker()
        {
            if (_pickerBox.Parent is Panel old) old.Children.Remove(_pickerBox);
            var stack = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            var list = new StackPanel();
            Action fill = () =>
            {
                list.Children.Clear();
                string query = _pickerBox.Text.Trim();
                IEnumerable<DayPlanTask> all;
                try { all = DayPlanTaskStore.Shared.Tasks(DateTime.Today).Where(t => !t.Done); } catch { all = Enumerable.Empty<DayPlanTask>(); }
                foreach (var task in all.Where(t => query.Length == 0 || (t.Title ?? "").IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0).Take(6))
                {
                    var captured = task;
                    var line = FocusStartUi.Clickable(new Border { Child = Text(task.Title, 13, _p.Primary), Padding = new Thickness(10, 6, 10, 6) }, () => SwitchTo(captured), "btnAiBlockPickTask", task.Title);
                    line.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                    list.Children.Add(line);
                }
            };
            _pickerBox.TextChanged -= PickerChanged;
            _pickerTextChanged = fill;
            _pickerBox.TextChanged += PickerChanged;
            _pickerBox.KeyDown -= PickerKey;
            _pickerBox.KeyDown += PickerKey;
            stack.Children.Add(FocusStartUi.SearchField(_pickerBox, S("updateTaskPlaceholder", "Type to pick or add a task"), _p, new FocusStartScale(1675), "txtAiBlockPicker"));
            stack.Children.Add(list);
            fill();
            return stack;
        }

        private Action _pickerTextChanged;
        private void PickerChanged(object sender, TextChangedEventArgs e) => _pickerTextChanged?.Invoke();

        private void PickerKey(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            string text = _pickerBox.Text.Trim();
            if (text.Length > 0) SwitchTo(new DayPlanTask(text));
        }

        /// <summary>"Task changed to …" for a moment, then the blocking code re-judges the page against the new task.</summary>
        private void SwitchTo(DayPlanTask task)
        {
            if (_resolved) return;
            _body.Child = Centered(Text(S("taskChangedTo", "Task changed to {0}").Replace("{0}", task.Title), 18, _p.Primary, FontWeights.SemiBold));
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            timer.Tick += (s, e) => { timer.Stop(); Resolve(() => _r.PickTask?.Invoke(task)); };
            timer.Start();
        }

        // ------------------------------------------------------------------ argue

        private FrameworkElement ArgueView()
        {
            var stack = new StackPanel { Margin = new Thickness(28, 22, 28, 12) };
            stack.Children.Add(BackLink());
            stack.Children.Add(Text(S("argueHeader", "Could you tell me why it's relevant?"), 20, _p.Primary, FontWeights.SemiBold));
            foreach (var turn in _argument)
            {
                var bubble = new Border
                {
                    Child = Text(turn.Item2, 13, turn.Item1 ? _p.Primary : Brushes.White),
                    Background = turn.Item1 ? _p.Field : _p.Accent,
                    BorderBrush = _p.Border,
                    BorderThickness = new Thickness(turn.Item1 ? 1 : 0),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(turn.Item1 ? 0 : 80, 10, turn.Item1 ? 80 : 0, 0),
                    HorizontalAlignment = turn.Item1 ? HorizontalAlignment.Left : HorizontalAlignment.Right,
                };
                AutomationProperties.SetName(bubble, turn.Item2);
                stack.Children.Add(bubble);
            }
            if (_thinking) stack.Children.Add(Margin(Text(S("argueThinking", "Checking against your top task…"), 12, _p.Secondary), 0, 10, 0, 0));
            if (_argueFailed) stack.Children.Add(Margin(Text(S("argueError", "Something went wrong. You can try again, or block it and stay focused."), 12, _p.Warning), 0, 10, 0, 0));

            var bottom = new StackPanel { Margin = new Thickness(28, 8, 28, 18) };
            if (AiBlockBuddy.IsRelevant(_argueProbability))
            {
                var approved = new StackPanel();
                var prompt = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                var promptText = Text(S("argueDurationPrompt", "How long do you need?") + "  ", 13, _p.Primary);
                promptText.VerticalAlignment = VerticalAlignment.Center;
                prompt.Children.Add(promptText);
                var value = Text(_allowMinutes + " " + S("argueMinutesUnit", "min"), 14, _p.Accent, FontWeights.SemiBold);
                value.VerticalAlignment = VerticalAlignment.Center;
                value.Margin = new Thickness(8, 0, 8, 0);
                AutomationProperties.SetAutomationId(value, "txtAiBlockAllowMinutes");
                prompt.Children.Add(Stepper(0xE738, -5, "btnAiBlockMinus"));
                prompt.Children.Add(value);
                prompt.Children.Add(Stepper(0xE710, 5, "btnAiBlockPlus"));
                approved.Children.Add(prompt);
                var allow = Primary(S("argueAllowForButton", "Let me use it for {0} min").Replace("{0}", _allowMinutes.ToString()), () =>
                {
                    string why = string.Join(" ", _argument.Where(t => !t.Item1).Select(t => t.Item2));
                    Resolve(() => _r.Allow?.Invoke(_allowMinutes, why));
                }, "btnAiBlockAllow");
                allow.Margin = new Thickness(0, 10, 0, 0);
                approved.Children.Add(allow);
                bottom.Children.Add(approved);
            }
            else
            {
                if (_argueBox.Parent is Panel old) old.Children.Remove(_argueBox);
                _argueBox.AcceptsReturn = false;
                _argueBox.KeyDown -= ArgueKey;
                _argueBox.KeyDown += ArgueKey;
                var row = new DockPanel();
                var send = Secondary(S("argueSend", "Send"), () => _ = SendArgument(), "btnAiBlockArgueSend");
                send.Margin = new Thickness(8, 0, 0, 0);
                send.IsEnabled = !_thinking;
                DockPanel.SetDock(send, Dock.Right);
                row.Children.Add(send);
                row.Children.Add(FocusStartUi.SearchField(_argueBox, S("argueInputPlaceholder", "Why does this help your top task?"), _p, new FocusStartScale(1675), "txtAiBlockArgue"));
                bottom.Children.Add(row);
            }
            return Layout(stack, bottom);
        }

        private void ArgueKey(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            _ = SendArgument();
        }

        private async Task SendArgument()
        {
            string text = _argueBox.Text.Trim();
            if (text.Length == 0 || _thinking || _r.Argue == null) return;
            _argueBox.Text = "";
            _argument.Add(Tuple.Create(false, text));
            _thinking = true;
            _argueFailed = false;
            Draw();
            double? probability = null;
            try { probability = await _r.Argue(string.Join("\n", _argument.Where(t => !t.Item1).Select(t => t.Item2))); }
            catch (Exception ex) { LogWriter.LogWrite("AiBlockBuddy argue - " + ex.Message); }
            if (_resolved) return;
            _thinking = false;
            if (!probability.HasValue || probability.Value < 0) { _argueFailed = true; Draw(); _argueBox.Focus(); return; }
            _argueProbability = probability;
            string reply = string.IsNullOrWhiteSpace(_settings.sChatGPTReasonForCurrentURL) ? "" : _settings.sChatGPTReasonForCurrentURL;
            if (reply.Length > 0) _argument.Add(Tuple.Create(true, reply));
            Draw();
            if (!AiBlockBuddy.IsRelevant(probability)) _argueBox.Focus();
        }

        private Button Stepper(int glyph, int delta, string id)
        {
            var face = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(15), BorderBrush = _p.Accent, BorderThickness = new Thickness(1.5), Child = FocusStartUi.Glyph(glyph, 12, _p.Accent) };
            ((TextBlock)face.Child).HorizontalAlignment = HorizontalAlignment.Center;
            return FocusStartUi.Clickable(face, () => { _allowMinutes = AiBlockBuddy.ClampAllowMinutes(_allowMinutes + delta); Draw(); }, id, delta < 0 ? "−5" : "+5");
        }

        // ------------------------------------------------------------------ save for later

        private readonly TextBox _saveTitle = new TextBox(), _saveDetails = new TextBox(), _saveMinutes = new TextBox(), _saveProject = new TextBox();
        private bool _saveSeeded;

        private FrameworkElement SaveView()
        {
            if (!_saveSeeded)
            {
                _saveSeeded = true;
                _saveTitle.Text = !string.IsNullOrWhiteSpace(_r.PageTitle) ? _r.PageTitle.Trim() : _r.Label;
                _saveDetails.Text = _r.IsApp ? "" : _r.Url;
                _saveMinutes.Text = "15";
            }
            var stack = new StackPanel { Margin = new Thickness(28, 22, 28, 12) };
            stack.Children.Add(BackLink());
            stack.Children.Add(Text(S("saveCurrentTitle", "Save what you're doing as a task"), 20, _p.Primary, FontWeights.SemiBold));
            stack.Children.Add(Margin(Text(S("saveCurrentSubtitle", "We'll add this to your Day Plan so you can come back to it later."), 13, _p.Secondary), 0, 6, 0, 10));
            stack.Children.Add(Field(S("fieldTitle", "Title"), _saveTitle, "txtAiBlockSaveTitle"));
            stack.Children.Add(Field(S("fieldDescription", "Description"), _saveDetails, "txtAiBlockSaveDetails"));
            stack.Children.Add(Field(S("fieldDuration", "Duration (min)"), _saveMinutes, "txtAiBlockSaveMinutes"));
            stack.Children.Add(Field(S("fieldProject", "Project"), _saveProject, "txtAiBlockSaveProject"));
            var bottom = new StackPanel { Margin = new Thickness(28, 8, 28, 18) };
            var save = Primary(S("saveTaskButton", "Save task"), SaveTask, "btnAiBlockSaveTask");
            bottom.Children.Add(save);
            return Layout(stack, bottom);
        }

        private void SaveTask()
        {
            string title = _saveTitle.Text.Trim();
            if (title.Length == 0) return;
            int minutes = int.TryParse(_saveMinutes.Text.Trim(), out int m) && m > 0 ? Math.Min(m, 24 * 60) : 15;
            try
            {
                var task = new DayPlanTask(title, minutes, project: string.IsNullOrWhiteSpace(_saveProject.Text) ? null : _saveProject.Text.Trim());
                DayPlanTaskStore.Shared.Add(task, DateTime.Today);
                LogWriter.LogWrite("AiBlockBuddy - saved for later: " + title);
            }
            catch (Exception ex) { LogWriter.LogWrite("AiBlockBuddy save task - " + ex.Message); }
            _saveSeeded = false;
            _phase = Phase.Choose;
            Draw();
        }

        // ------------------------------------------------------------------ pieces

        private FrameworkElement Layout(FrameworkElement scrolling, FrameworkElement pinned)
        {
            var root = new DockPanel();
            DockPanel.SetDock(pinned, Dock.Bottom);
            root.Children.Add(pinned);
            root.Children.Add(new ScrollViewer { Content = scrolling, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            return root;
        }

        private FrameworkElement BackLink()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(FocusStartUi.Glyph(0xE72B, 12, _p.Accent));
            var label = Text(" " + S("back", "Back"), 13, _p.Accent, FontWeights.Medium);
            row.Children.Add(label);
            var link = FocusStartUi.Clickable(row, () => { _phase = Phase.Choose; Draw(); }, "btnAiBlockBack");
            link.HorizontalAlignment = HorizontalAlignment.Left;
            link.Margin = new Thickness(0, 0, 0, 12);
            return link;
        }

        private FrameworkElement Field(string label, TextBox box, string id)
        {
            if (box.Parent is Panel old) old.Children.Remove(box);
            var stack = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            stack.Children.Add(Text(label, 12, _p.Secondary, FontWeights.Medium));
            box.Margin = new Thickness(0, 4, 0, 0);
            box.Padding = new Thickness(8, 5, 8, 5);
            box.Background = _p.Field;
            box.Foreground = _p.Primary;
            box.CaretBrush = _p.Primary;
            box.BorderBrush = _p.Border;
            AutomationProperties.SetAutomationId(box, id);
            AutomationProperties.SetName(box, label);
            stack.Children.Add(box);
            return stack;
        }

        private Button Primary(string text, Action onClick, string id)
        {
            var label = Text(text, 15, Brushes.White, FontWeights.SemiBold);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.TextWrapping = TextWrapping.NoWrap;
            var face = new Border { Child = label, Background = _p.Accent, CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 11, 16, 11) };
            var button = FocusStartUi.Clickable(face, onClick, id, text);
            button.IsEnabledChanged += (s, e) => face.Opacity = button.IsEnabled ? 1 : 0.5;
            return button;
        }

        private Button Secondary(string text, Action onClick, string id)
        {
            var label = Text(text, 14, Brushes.Black, FontWeights.SemiBold);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.TextWrapping = TextWrapping.NoWrap;
            var face = new Border { Child = label, Background = Brushes.White, BorderBrush = _p.Accent, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 9, 16, 9) };
            var button = FocusStartUi.Clickable(face, onClick, id, text);
            button.IsEnabledChanged += (s, e) => face.Opacity = button.IsEnabled ? 1 : 0.5;
            return button;
        }

        private static FrameworkElement Centered(UIElement child) => new Grid { Children = { child }, Margin = new Thickness(28) };

        private static FrameworkElement Margin(FrameworkElement element, double l, double t, double r, double b)
        {
            element.Margin = new Thickness(l, t, r, b);
            return element;
        }

        private static TextBlock Text(string text, double size, Brush color, FontWeight? weight = null)
        {
            var block = FocusStartUi.Text(text, size, color, weight);
            block.HorizontalAlignment = HorizontalAlignment.Stretch;
            return block;
        }
    }
}
