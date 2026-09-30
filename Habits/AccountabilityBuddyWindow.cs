using Focus_Bear.Utils;
using FocusBear.Core.Postpone;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Focus_Bear.Habits
{
    /// <summary>
    /// The AI accountability buddy (the Mac's <c>AccountabilityChatView</c>): opened in place of the postpone
    /// minutes picker, after the postpone password. The user says what they need the computer for; the buddy
    /// approves (a scoped, super-strict AI focus session for that goal, and the routine postponed for its length)
    /// or denies, in the chat. The X at the top means "do my habit".
    ///
    /// When the buddy can't be reached, or keeps denying the same way, a recovery card offers a 10-second hold to
    /// postpone anyway — the only path that postpones without an approval. As on the Mac since PR #2649 (report
    /// 2026-09-25_15-40-49) the card says which of those it is: a stuck conversation first asks one quiet question
    /// ("Is the AI accountability buddy getting annoying? Yes"), offline and server-down each explain themselves,
    /// and "Do my habit" / "Try my buddy again" are gone (sending another message already retries; the card's own
    /// X closes it).
    /// </summary>
    internal sealed class AccountabilityBuddyWindow : Window
    {
        internal enum Outcome { Cancelled, Approved, HoldPostpone }

        /// <summary>A duration in the "Push back by" picker. The app's RoundedComboBoxStyle shows <c>name</c>.</summary>
        public sealed class DurationChoice
        {
            public DurationChoice(int minutes, string name) { Minutes = minutes; this.name = name; }
            public int Minutes { get; }
            // Lower-case: RoundedComboBoxStyle binds SelectedItem.name (its first user shows focus modes).
            public string name { get; }
            public override string ToString() => name;
        }

        private static readonly SolidColorBrush Accent = Frozen(0xDE, 0x74, 0x00);
        private readonly AppSettings _settings = AppSettings.GetInstance;
        private readonly Func<IList<AccountabilityMessage>, Task<AccountabilityReply>> _evaluate;
        private readonly List<AccountabilityMessage> _messages = new List<AccountabilityMessage>();
        private readonly StackPanel _transcript = new StackPanel();
        private readonly ScrollViewer _scroll;
        private readonly TextBox _input = new TextBox();
        private readonly Button _send;
        private readonly Border _approvedBar = new Border();
        private readonly Border _recoveryBar = new Border();
        private readonly TextBlock _thinking;
        private readonly AccountabilityHold _hold = new AccountabilityHold();
        private readonly DispatcherTimer _holdTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        private AccountabilityRecoveryReason _recoveryReason = AccountabilityRecoveryReason.Unavailable;
        private bool _recoveryExpanded;
        private int _recoveryMinutes = AccountabilityRecovery.DefaultMinutes;
        private ProgressBar _holdProgress;
        private TextBlock _holdLabel;
        private bool _thinkingNow;
        private int _request;
        private string _firstUserMessage = "";

        internal Outcome Result { get; private set; } = Outcome.Cancelled;
        internal AccountabilityVerdict ApprovedVerdict { get; private set; }
        internal string Goal { get; private set; } = "";
        internal int Minutes { get; private set; }

        internal AccountabilityBuddyWindow(Func<IList<AccountabilityMessage>, Task<AccountabilityReply>> evaluate)
        {
            _evaluate = evaluate;
            Title = S("aiBuddyWindowTitle", "Accountability Buddy");
            Width = 480;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            // Never taller than the screen's work area (a 569-px laptop screen at 175%): the reply box and its buttons stays on it.
            Focus_Bear.ToDoScreens.DialogFit.Apply(this);
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            AutomationProperties.SetAutomationId(this, "wndAccountabilityBuddy");

            var root = new DockPanel();
            // Header: shield, title, and close (= do my habit).
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            var close = IconButton("", 12, S("aiBuddyButtonDoHabit", "Do my habit"), "btnBuddyClose", GiveUp);
            DockPanel.SetDock(close, Dock.Right);
            header.Children.Add(close);
            var shield = new TextBlock { Text = "", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 16, Foreground = Accent, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            DockPanel.SetDock(shield, Dock.Left);
            header.Children.Add(shield);
            var title = Text(S("aiBuddyHeader", "Accountability buddy"), 17, FontWeights.SemiBold);
            title.VerticalAlignment = VerticalAlignment.Center;
            header.Children.Add(title);
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);
            var topRule = Rule();
            DockPanel.SetDock(topRule, Dock.Top);
            root.Children.Add(topRule);

            // Input row.
            var inputRow = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
            _send = ActionButton(S("aiBuddyButtonAsk", "Ask my buddy"), true, Send, "btnBuddySend");
            _send.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(_send, Dock.Right);
            inputRow.Children.Add(_send);
            _input.MinHeight = 34;
            _input.Padding = new Thickness(8, 6, 8, 6);
            _input.TextWrapping = TextWrapping.Wrap;
            _input.VerticalContentAlignment = VerticalAlignment.Center;
            _input.MaxLength = AccountabilityRequest.MessageLimit;
            _input.SetResourceReference(TextBox.BackgroundProperty, "TextBoxBackgroundColor");
            _input.SetResourceReference(TextBox.ForegroundProperty, "ForegroundColor");
            _input.SetResourceReference(TextBox.CaretBrushProperty, "ForegroundColor");
            _input.KeyDown += (s, e) => { if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None) { e.Handled = true; Send(); } };
            AutomationProperties.SetAutomationId(_input, "txtBuddyInput");
            AutomationProperties.SetName(_input, S("aiBuddyGoalPrompt", "What do you want to achieve on the computer instead?"));
            inputRow.Children.Add(_input);
            DockPanel.SetDock(inputRow, Dock.Bottom);
            root.Children.Add(inputRow);
            var bottomRule = Rule();
            bottomRule.Margin = new Thickness(0, 10, 0, 0);
            DockPanel.SetDock(bottomRule, Dock.Bottom);
            root.Children.Add(bottomRule);

            _recoveryBar.Visibility = Visibility.Collapsed;
            AutomationProperties.SetAutomationId(_recoveryBar, "pnlBuddyRecovery");
            DockPanel.SetDock(_recoveryBar, Dock.Bottom);
            root.Children.Add(_recoveryBar);
            _approvedBar.Visibility = Visibility.Collapsed;
            DockPanel.SetDock(_approvedBar, Dock.Bottom);
            root.Children.Add(_approvedBar);

            _thinking = Text(S("aiBuddyChecking", "Checking whether that's worth postponing your habits…"), 12, FontWeights.Normal, secondary: true);
            _thinking.Visibility = Visibility.Collapsed;
            _thinking.Margin = new Thickness(4, 6, 0, 0);
            DockPanel.SetDock(_thinking, Dock.Bottom);
            root.Children.Add(_thinking);

            _scroll = new ScrollViewer { Content = _transcript, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 12, 0, 0) };
            root.Children.Add(_scroll);

            var frame = new Border { Child = root, CornerRadius = new CornerRadius(14), Padding = new Thickness(18), BorderThickness = new Thickness(1), Margin = new Thickness(10) };
            frame.SetResourceReference(Border.BackgroundProperty, "BackgroundColor");
            frame.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            frame.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 0, Opacity = 0.25 };
            frame.MouseLeftButtonDown += (s, e) => { if (e.ChangedButton == MouseButton.Left && e.OriginalSource == frame) DragMove(); };
            Content = frame;

            PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { e.Handled = true; GiveUp(); } };
            _holdTimer.Tick += (s, e) => TickHold();
            Loaded += (s, e) => { Seed(); _input.Focus(); };
            Deactivated += (s, e) => ReleaseHold();
        }

        private string S(string key, string fallback) => _settings.GetConfigStringSafe(fallback, "accountabilityBuddyInfo", key);

        // ------------------------------------------------------------------ conversation

        private void Seed()
        {
            _messages.Add(new AccountabilityMessage(true, S("aiBuddyGoalPrompt", "What do you want to achieve on the computer instead?")));
            Draw();
        }

        private void Send()
        {
            string text = _input.Text.Trim();
            if (text.Length == 0 || _thinkingNow) return;
            if (_firstUserMessage.Length == 0) _firstUserMessage = text;
            _input.Text = "";
            _messages.Add(new AccountabilityMessage(false, text));
            Draw();
            _ = Evaluate();
        }

        private async Task Evaluate()
        {
            int request = ++_request;
            _thinkingNow = true;
            _send.IsEnabled = false;
            _thinking.Visibility = Visibility.Visible;
            _approvedBar.Visibility = Visibility.Collapsed;
            HideRecovery();
            AccountabilityReply reply;
            try { reply = await _evaluate(_messages.ToList()) ?? AccountabilityReply.Failed(AccountabilityRecoveryReason.Unavailable); }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AccountabilityBuddy evaluate - " + ex.Message);
                reply = AccountabilityReply.Failed(AccountabilityRecoveryReason.Unavailable);
            }
            if (request != _request || !IsLoaded) return;
            _thinkingNow = false;
            _send.IsEnabled = true;
            _thinking.Visibility = Visibility.Collapsed;
            var verdict = reply.Verdict;
            if (verdict == null)
            {
                // Every failure takes the same deliberate, held fallback, and none postpones. The card says which
                // kind of failure it was, so there is no "something went wrong" bubble.
                LogWriter.LogWrite("accountability buddy: no verdict (" + reply.Failure + ")");
                ShowRecovery(reply.Failure);
                return;
            }
            _messages.Add(new AccountabilityMessage(true, verdict.Message.Length > 0 ? verdict.Message : (verdict.Approved ? S("aiBuddyApprovedTitle", "Approved") : S("aiBuddyDeniedTitle", "Let's not"))));
            Draw();
            if (verdict.Approved) { ShowApproved(verdict); return; }
            // A denial stays in the chat: the user answers in the box below. Only a conversation going in circles
            // (the seeded question aside) offers the way out.
            var stuck = AccountabilityRecovery.ForVerdict(verdict, _messages.Where(m => m.FromBuddy).Skip(1).Select(m => m.Text));
            if (stuck.HasValue) ShowRecovery(stuck.Value);
            _input.Focus();
        }

        private void Draw()
        {
            _transcript.Children.Clear();
            foreach (var message in _messages)
            {
                var text = Text(message.Text, 13, FontWeights.Normal);
                if (!message.FromBuddy) text.Foreground = Brushes.White;
                var bubble = new Border
                {
                    Child = text,
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(message.FromBuddy ? 0 : 60, 0, message.FromBuddy ? 60 : 0, 8),
                    HorizontalAlignment = message.FromBuddy ? HorizontalAlignment.Left : HorizontalAlignment.Right,
                };
                if (message.FromBuddy)
                {
                    bubble.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
                    bubble.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
                    bubble.BorderThickness = new Thickness(1);
                }
                else bubble.Background = Accent;
                AutomationProperties.SetName(bubble, message.Text);
                AutomationProperties.SetAutomationId(bubble, message.FromBuddy ? "txtBuddyMessage" : "txtUserMessage");
                _transcript.Children.Add(bubble);
            }
            if (_messages.Count == 1)
            {
                var help = Text(S("aiBuddyGoalHelp", "Your buddy will unlock only the apps and sites this needs, then block everything else."), 12, FontWeights.Normal, secondary: true);
                help.Margin = new Thickness(4, 0, 0, 8);
                _transcript.Children.Add(help);
            }
            _scroll.ScrollToEnd();
        }

        // ------------------------------------------------------------------ approval

        private void ShowApproved(AccountabilityVerdict verdict)
        {
            int minutes = verdict.ClampedDurationMinutes;
            var stack = new StackPanel();
            stack.Children.Add(Unlocked(S("aiBuddyAppsUnlocked", "Apps unlocked"), verdict.AllowedApps, S("aiBuddyNoApps", "No specific apps")));
            stack.Children.Add(Unlocked(S("aiBuddySitesUnlocked", "Sites unlocked"), verdict.AllowedUrls, S("aiBuddyNoSites", "No specific sites")));
            var blocked = Text(S("aiBuddyBlockedFor", "Everything else stays blocked for {0} minutes (super-strict).").Replace("{0}", minutes.ToString()), 12, FontWeights.Normal, secondary: true);
            blocked.Margin = new Thickness(0, 4, 0, 10);
            stack.Children.Add(blocked);
            var start = ActionButton(S("aiBuddyStartForMinutes", "Start focus session · {minutes} minutes").Replace("{minutes}", minutes.ToString()), true, () =>
            {
                if (_thinkingNow) return;
                Result = Outcome.Approved;
                ApprovedVerdict = verdict;
                Goal = verdict.GoalOr(_firstUserMessage);
                Minutes = minutes;
                Close();
            }, "btnBuddyStart", stretch: true);
            stack.Children.Add(start);
            Bar(_approvedBar, stack, Color.FromArgb(0x22, 0x2E, 0x9E, 0x5B));
            _approvedBar.Visibility = Visibility.Visible;
        }

        // ------------------------------------------------------------------ recovery card

        private void ShowRecovery(AccountabilityRecoveryReason reason)
        {
            _recoveryReason = reason;
            _recoveryExpanded = !AccountabilityRecovery.StartsCollapsed(reason);
            _approvedBar.Visibility = Visibility.Collapsed;
            DrawRecovery();
            _recoveryBar.Visibility = Visibility.Visible;
        }

        /// <summary>The card's own X: hides it and keeps the conversation (it returns on the next stuck denial or failure).</summary>
        private void HideRecovery()
        {
            ReleaseHold();
            _recoveryExpanded = false;
            _recoveryBar.Visibility = Visibility.Collapsed;
        }

        private void DrawRecovery()
        {
            ReleaseHold();
            _holdProgress = null;
            _holdLabel = null;
            var stack = new StackPanel();
            var top = new DockPanel();
            var dismiss = IconButton("", 10, S("aiBuddyRecoveryClose", "Close"), "btnBuddyRecoveryClose", HideRecovery);
            dismiss.VerticalAlignment = VerticalAlignment.Top;
            DockPanel.SetDock(dismiss, Dock.Right);
            top.Children.Add(dismiss);
            top.Children.Add(RecoveryHeading());
            stack.Children.Add(top);
            if (_recoveryExpanded) stack.Children.Add(RecoveryControls());
            _recoveryBar.Child = stack;
            _recoveryBar.Background = new SolidColorBrush(Color.FromArgb(_recoveryExpanded ? (byte)0x1C : (byte)0x0E, 0xDE, 0x74, 0x00));
            _recoveryBar.CornerRadius = new CornerRadius(10);
            _recoveryBar.Padding = _recoveryExpanded ? new Thickness(14, 12, 10, 14) : new Thickness(14, 6, 10, 6);
            _recoveryBar.Margin = new Thickness(0, 8, 0, 0);
        }

        private FrameworkElement RecoveryHeading()
        {
            switch (_recoveryReason)
            {
                case AccountabilityRecoveryReason.Stuck when !_recoveryExpanded:
                {
                    var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    var ask = Text(S("aiBuddyAnnoyingPrompt", "Is the AI accountability buddy getting annoying?"), 13, FontWeights.Normal, secondary: true);
                    ask.VerticalAlignment = VerticalAlignment.Center;
                    row.Children.Add(ask);
                    var yes = ActionButton(S("aiBuddyAnnoyingYes", "Yes"), false, () => { _recoveryExpanded = true; DrawRecovery(); }, "btnBuddyAnnoyingYes", small: true);
                    yes.Margin = new Thickness(8, 0, 0, 0);
                    row.Children.Add(yes);
                    return row;
                }
                case AccountabilityRecoveryReason.Stuck:
                    return Text(S("aiBuddyRecoveryTitle", "Need to push your habits back?"), 14, FontWeights.SemiBold);
                case AccountabilityRecoveryReason.Offline:
                    return Explanation(S("aiBuddyOfflineTitle", "You're offline"),
                        S("aiBuddyOfflineExplanation", "Your buddy needs the internet to reply. Reconnect and send your message again, or push your habits back below."));
                default:
                    return Explanation(S("aiBuddyUnavailableTitle", "Your buddy isn't responding"),
                        S("aiBuddyUnavailableExplanation", "We couldn't reach your accountability buddy. That's a problem on our side, not yours. Send your message again in a minute, or push your habits back below."));
            }
        }

        private FrameworkElement Explanation(string title, string body)
        {
            var stack = new StackPanel();
            stack.Children.Add(Text(title, 14, FontWeights.SemiBold));
            var text = Text(body, 13, FontWeights.Normal);
            text.Margin = new Thickness(0, 4, 0, 0);
            stack.Children.Add(text);
            return stack;
        }

        private FrameworkElement RecoveryControls()
        {
            var stack = new StackPanel { Margin = new Thickness(0, 10, 4, 0) };

            // "Push back by [15 minutes ▾]", in the app's rounded picker rather than WPF's default ComboBox.
            var durationRow = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            var label = Text(S("aiBuddyRecoveryDuration", "Push back by"), 13, FontWeights.Normal);
            label.VerticalAlignment = VerticalAlignment.Center;
            label.Margin = new Thickness(0, 0, 10, 0);
            durationRow.Children.Add(label);
            var picker = new ComboBox { Width = 150, Height = 32, FontSize = 13, HorizontalAlignment = HorizontalAlignment.Left };
            if (TryFindResource("RoundedComboBoxStyle") is Style rounded) picker.Style = rounded;
            string minutesFormat = S("aiBuddyRecoveryMinutes", "{minutes} minutes");
            foreach (int m in AccountabilityHold.DurationChoices) picker.Items.Add(new DurationChoice(m, minutesFormat.Replace("{minutes}", m.ToString())));
            picker.SelectedIndex = Math.Max(0, Array.IndexOf(AccountabilityHold.DurationChoices, _recoveryMinutes));
            picker.SelectionChanged += (s, e) =>
            {
                if (picker.SelectedItem is DurationChoice choice) _recoveryMinutes = choice.Minutes;
                ReleaseHold();   // a changed duration needs a fresh hold
            };
            AutomationProperties.SetAutomationId(picker, "cmbBuddyRecoveryMinutes");
            AutomationProperties.SetName(picker, S("aiBuddyRecoveryDuration", "Push back by"));
            durationRow.Children.Add(picker);
            stack.Children.Add(durationRow);

            // A real press-and-hold (mouse, or Space/Return while focused) on an ordinary-looking button, with the
            // progress underneath, as on the Mac. Letting go, leaving the button or the window restarts the ten seconds.
            string holdHelp = S("aiBuddyRecoveryHoldHelp", "Keep it pressed, or focus the button and hold Space or Return. Let go and the 10 seconds start again.");
            _holdLabel = new TextBlock { Text = S("aiBuddyRecoveryHold", "Hold for 10 seconds to postpone"), FontSize = 13, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            _holdLabel.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            var holdFace = new HoldFace { Child = _holdLabel, Height = 36, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Cursor = Cursors.Hand, Focusable = true, ToolTip = holdHelp, FocusVisualStyle = null };
            holdFace.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
            holdFace.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            holdFace.MouseEnter += (s, e) => holdFace.BorderBrush = Accent;
            holdFace.MouseLeave += (s, e) => { holdFace.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder"); ReleaseHold(); };
            holdFace.GotKeyboardFocus += (s, e) => holdFace.BorderBrush = Accent;
            holdFace.MouseLeftButtonDown += (s, e) => { holdFace.Focus(); holdFace.CaptureMouse(); PressHold(); e.Handled = true; };
            holdFace.MouseLeftButtonUp += (s, e) => { holdFace.ReleaseMouseCapture(); ReleaseHold(); };
            holdFace.LostMouseCapture += (s, e) => ReleaseHold();
            holdFace.KeyDown += (s, e) => { if (e.Key == Key.Space || e.Key == Key.Enter) { e.Handled = true; if (!e.IsRepeat) PressHold(); } };
            holdFace.KeyUp += (s, e) => { if (e.Key == Key.Space || e.Key == Key.Enter) { e.Handled = true; ReleaseHold(); } };
            holdFace.LostKeyboardFocus += (s, e) => { holdFace.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder"); ReleaseHold(); };
            AutomationProperties.SetAutomationId(holdFace, "btnBuddyHold");
            AutomationProperties.SetName(holdFace, S("aiBuddyRecoveryHold", "Hold for 10 seconds to postpone"));
            AutomationProperties.SetHelpText(holdFace, holdHelp);
            stack.Children.Add(holdFace);

            _holdProgress = new ProgressBar { Minimum = 0, Maximum = 1, Height = 4, Margin = new Thickness(0, 8, 0, 0), Foreground = Accent, BorderThickness = new Thickness(0) };
            _holdProgress.Background = new SolidColorBrush(Color.FromArgb(0x33, 0xDE, 0x74, 0x00));
            _holdProgress.Template = ThinProgressTemplate();
            AutomationProperties.SetName(_holdProgress, S("aiBuddyRecoveryHold", "Hold for 10 seconds to postpone"));
            stack.Children.Add(_holdProgress);

            var help = Text(holdHelp, 11.5, FontWeights.Normal, secondary: true);
            help.Margin = new Thickness(0, 8, 0, 0);
            stack.Children.Add(help);
            return stack;
        }

        private void PressHold()
        {
            if (_recoveryBar.Visibility != Visibility.Visible || !_recoveryExpanded || _thinkingNow) return;
            _hold.Press(DateTime.UtcNow);
            _holdTimer.Start();
        }

        private void ReleaseHold()
        {
            _hold.Release();
            _holdTimer.Stop();
            if (_holdProgress != null) _holdProgress.Value = 0;
            if (_holdLabel != null) _holdLabel.Text = S("aiBuddyRecoveryHold", "Hold for 10 seconds to postpone");
        }

        private void TickHold()
        {
            if (_holdProgress == null || _holdLabel == null) { ReleaseHold(); return; }
            var now = DateTime.UtcNow;
            _holdProgress.Value = _hold.Progress(now);
            _holdLabel.Text = S("aiBuddyRecoveryHolding", "Almost there — {seconds} seconds to go").Replace("{seconds}", Math.Max(1, _hold.SecondsLeft(now)).ToString());
            if (!_hold.IsComplete(now)) return;
            _holdTimer.Stop();
            Result = Outcome.HoldPostpone;
            Minutes = _recoveryMinutes;
            _request++;   // a reply still in flight must not act after this
            LogWriter.LogWrite("[user-action] accountability buddy: postpone after 10-second hold (" + _recoveryReason + "), minutes=" + Minutes);
            Close();
        }

        private void GiveUp()
        {
            Result = Outcome.Cancelled;
            _request++;   // a reply still in flight must not act after this
            Close();
        }

        // ------------------------------------------------------------------ pieces

        private FrameworkElement Unlocked(string label, IList<string> items, string none)
        {
            var line = Text(label + ": " + (items.Count == 0 ? none : string.Join(" · ", items)), 12, FontWeights.Normal, secondary: true);
            line.Margin = new Thickness(0, 0, 0, 2);
            return line;
        }

        private static void Bar(Border bar, UIElement content, Color tint)
        {
            bar.Child = content;
            bar.Background = new SolidColorBrush(tint);
            bar.CornerRadius = new CornerRadius(10);
            bar.Padding = new Thickness(12);
            bar.Margin = new Thickness(0, 8, 0, 0);
        }

        private static Border Rule()
        {
            var rule = new Border { Height = 1 };
            rule.SetResourceReference(Border.BackgroundProperty, "TrayCardBorder");
            return rule;
        }

        private static TextBlock Text(string text, double size, FontWeight weight, bool secondary = false)
        {
            var block = new TextBlock { Text = text, FontSize = size, FontWeight = weight, TextWrapping = TextWrapping.Wrap };
            block.SetResourceReference(TextBlock.ForegroundProperty, secondary ? "TraySubtitleColor" : "ForegroundColor");
            return block;
        }

        private static Button IconButton(string glyph, double size, string tip, string automationId, Action onClick)
        {
            var button = new Button { Content = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = size, Width = 26, Height = 26, Cursor = Cursors.Hand, Background = Brushes.Transparent, BorderThickness = new Thickness(0), ToolTip = tip };
            button.Template = new ControlTemplate(typeof(Button)) { VisualTree = Centered() };
            button.SetResourceReference(ForegroundProperty, "TraySubtitleColor");
            button.Click += (s, e) => onClick();
            AutomationProperties.SetAutomationId(button, automationId);
            AutomationProperties.SetName(button, tip);
            return button;
        }

        private static FrameworkElementFactory Centered()
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            return border;
        }

        private static Button ActionButton(string text, bool primary, Action onClick, string automationId, bool stretch = false, bool small = false)
        {
            var label = new TextBlock { Text = text, FontSize = small ? 12 : 13, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Margin = small ? new Thickness(10, 3, 10, 3) : new Thickness(14, 7, 14, 7) };
            var face = new Border { Child = label, CornerRadius = new CornerRadius(small ? 6 : 8), BorderThickness = new Thickness(1) };
            if (primary)
            {
                label.Foreground = Brushes.White;
                face.Background = Accent;
                face.BorderBrush = Accent;
            }
            else
            {
                label.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
                face.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
                face.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            }
            var button = new Button { Content = face, Cursor = Cursors.Hand, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            if (stretch) button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.Template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) };
            button.Click += (s, e) => onClick();
            button.IsEnabledChanged += (s, e) => face.Opacity = button.IsEnabled ? 1 : 0.5;
            AutomationProperties.SetAutomationId(button, automationId);
            AutomationProperties.SetName(button, text);
            return button;
        }

        /// <summary>A 4px rounded track and fill (the default ProgressBar template ignores the theme).</summary>
        private static ControlTemplate ThinProgressTemplate()
        {
            var template = new ControlTemplate(typeof(ProgressBar));
            var grid = new FrameworkElementFactory(typeof(Grid));
            var track = new FrameworkElementFactory(typeof(Border), "PART_Track");
            track.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
            track.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            var indicator = new FrameworkElementFactory(typeof(Border), "PART_Indicator");
            indicator.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
            indicator.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.ForegroundProperty));
            indicator.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Left);
            grid.AppendChild(track);
            grid.AppendChild(indicator);
            template.VisualTree = grid;
            return template;
        }

        private static SolidColorBrush Frozen(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }

    /// <summary>
    /// The press-and-hold face. A plain Border has no UI Automation peer, so screen readers and UIA never saw the
    /// hold at all (its AutomationId and name went nowhere); this one reports itself as a button.
    /// </summary>
    internal sealed class HoldFace : Border
    {
        protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new HoldFacePeer(this);

        private sealed class HoldFacePeer : System.Windows.Automation.Peers.FrameworkElementAutomationPeer
        {
            public HoldFacePeer(HoldFace owner) : base(owner) { }
            protected override System.Windows.Automation.Peers.AutomationControlType GetAutomationControlTypeCore() => System.Windows.Automation.Peers.AutomationControlType.Button;
            protected override string GetClassNameCore() => "HoldFace";
            protected override bool IsControlElementCore() => true;
            protected override bool IsContentElementCore() => true;
            protected override bool IsKeyboardFocusableCore() => Owner.Focusable;
        }
    }
}
