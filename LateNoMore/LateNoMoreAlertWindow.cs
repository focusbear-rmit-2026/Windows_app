using Focus_Bear.Utils;
using FocusBear.Core.LateNoMore;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace Focus_Bear.LateNoMore
{
    internal enum LateNoMoreAlertChoice
    {
        /// <summary>Open the meeting link (and attend).</summary>
        Join,
        /// <summary>"I don't need to attend": stop reminding, here and on the other devices.</summary>
        NotAttending,
        /// <summary>Hide for a minute.</summary>
        Snooze,
        /// <summary>Close the heads-up (or the alert once attending).</summary>
        GotIt,
    }

    /// <summary>
    /// The alert's face, hostable on its own (the harness renders it). Two kinds, as in the Mac Late No More app:
    /// <list type="bullet">
    /// <item>the heads-up (<c>warnUserVc</c>): "{title} ({calendar}) is starting in 15 minutes. I'll remind you again 2
    /// minutes before." with Got it;</item>
    /// <item>the main alert (<c>customAlertVc</c>): a live countdown that turns red once the meeting has started, the
    /// meeting, its date and join link (or "couldn't find a link"), Join now, the two mental-preparation prompts, and
    /// Snooze / I don't need to attend. There is no "I'm attending": joining is detected (<see cref="LateNoMoreService"/>,
    /// <c>LateNoMoreJoinDetection</c>), after which it says the focus session is covering the meeting. When the meeting starts and the user hasn't joined, it speaks a short alert every 5 seconds for up to 5
    /// minutes (the Mac's "bark pool").</item>
    /// </list>
    /// </summary>
    internal sealed class LateNoMoreAlertView : UserControl
    {
        /// <summary>The Mac app's default verbal alerts (<c>arrBarkPools</c>).</summary>
        internal static readonly string[] BarkPool =
        {
            "yellow alert, meeting imminent", "red alert", "heyyoo... it's time", "beep beep!", "meeting time!", "hey!!", "check your calendar!",
        };

        private readonly LateNoMoreEvent _meeting;
        private readonly Func<DateTime> _now;
        private readonly bool _verbal, _test;
        private readonly DispatcherTimer _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private readonly Random _random = new Random();
        private TextBlock _countdown;
        private StackPanel _actions, _attending;
        private TextBlock _attendingText;
        private DateTime? _verbalStarted;
        private DateTime _lastBark = DateTime.MinValue;
        private bool _joined;
        private object _speech;

        public LateNoMoreAlertKind Kind { get; }
        public LateNoMoreEvent Meeting => _meeting;
        public event Action<LateNoMoreAlertChoice> Choice;

        public LateNoMoreAlertView(LateNoMoreEvent meeting, LateNoMoreAlertKind kind, bool verbalAlerts, bool test, Func<DateTime> now = null)
        {
            _meeting = meeting;
            Kind = kind;
            _verbal = verbalAlerts;
            _test = test;
            _now = now ?? (() => DateTime.Now);
            LnmUi.ApplyAppFont(this);
            AutomationProperties.SetAutomationId(this, kind == LateNoMoreAlertKind.Main ? "LateNoMoreAlert" : "LateNoMoreHeadsUp");
            Content = kind == LateNoMoreAlertKind.Main ? BuildMain() : BuildHeadsUp();
            if (kind == LateNoMoreAlertKind.Main)
            {
                _clock.Tick += (s, e) => TickClock();
                Loaded += (s, e) => { TickClock(); _clock.Start(); };
                Unloaded += (s, e) => Stop();
            }
        }

        private Border Frame(UIElement child, double width)
        {
            var card = new Border { Child = child, Width = width, Padding = new Thickness(24, 20, 24, 22), CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1) };
            card.SetResourceReference(Border.BackgroundProperty, "BackgroundColor");
            card.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            card.Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 4, Opacity = 0.28, Color = Colors.Black };
            card.Margin = new Thickness(18);
            return card;
        }

        private FrameworkElement Header(string caption, Action close)
        {
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
            var x = LnmUi.IconButton(LnmUi.GlyphClose, LnmUi.S("Close", "lateNoMoreInfo", "close"), close, "btnLnmClose");
            DockPanel.SetDock(x, Dock.Right);
            header.Children.Add(x);
            var badge = LnmUi.Badge(LnmUi.GlyphClock, 28);
            badge.Margin = new Thickness(0, 0, 10, 0);
            DockPanel.SetDock(badge, Dock.Left);
            header.Children.Add(badge);
            var label = LnmUi.Text(caption, 12, FontWeights.Bold, secondary: true, wrap: false);
            label.VerticalAlignment = VerticalAlignment.Center;
            header.Children.Add(label);
            return header;
        }

        private string TitleText => string.IsNullOrWhiteSpace(_meeting.Title) ? "(no title)" : _meeting.Title.Trim();

        // ------------------------------------------------------------------ heads-up

        private FrameworkElement BuildHeadsUp()
        {
            var stack = new StackPanel();
            stack.Children.Add(Header((_test ? "LATE NO MORE · TEST" : "LATE NO MORE"), () => Choice?.Invoke(LateNoMoreAlertChoice.GotIt)));
            int minutes = LateNoMoreCountdown.MinutesLeft(_meeting.Start, _now());
            string cal = string.IsNullOrWhiteSpace(_meeting.CalendarName) ? "" : " (" + _meeting.CalendarName + ")";
            var line = LnmUi.Text("", 16, FontWeights.SemiBold);
            line.Inlines.Add(new System.Windows.Documents.Run(TitleText));
            line.Inlines.Add(new System.Windows.Documents.Run(cal) { FontWeight = FontWeights.Normal });
            line.Inlines.Add(new System.Windows.Documents.Run(" is starting in " + minutes + (minutes == 1 ? " minute." : " minutes.")) { FontWeight = FontWeights.Normal });
            AutomationProperties.SetAutomationId(line, "txtLnmHeadsUp");
            stack.Children.Add(line);
            int? main = LateNoMoreService.Shared.Config.MainAlertMinutes;
            if (main.HasValue && main.Value < minutes)
            {
                var again = LnmUi.Text("I'll remind you again " + main.Value + (main.Value == 1 ? " minute" : " minutes") + " before.", 13, secondary: true);
                again.Margin = new Thickness(0, 6, 0, 0);
                stack.Children.Add(again);
            }
            var ok = LnmUi.Pill(LnmUi.S("Got it", "lateNoMoreInfo", "gotIt"), true, () => Choice?.Invoke(LateNoMoreAlertChoice.GotIt), "btnLnmGotIt");
            ok.HorizontalAlignment = HorizontalAlignment.Right;
            ok.Margin = new Thickness(0, 18, 0, 0);
            stack.Children.Add(ok);
            return Frame(stack, 400);
        }

        // ------------------------------------------------------------------ main alert

        private FrameworkElement BuildMain()
        {
            var stack = new StackPanel();
            stack.Children.Add(Header(_test ? "LATE NO MORE · TEST ALERT" : "LATE NO MORE", () => Choice?.Invoke(LateNoMoreAlertChoice.GotIt)));

            _countdown = LnmUi.Text("", 24, FontWeights.Bold, wrap: false);
            _countdown.FontFeatures();
            AutomationProperties.SetAutomationId(_countdown, "txtLnmCountdown");
            stack.Children.Add(_countdown);

            var name = LnmUi.Text(TitleText, 19, FontWeights.SemiBold);
            name.Margin = new Thickness(0, 12, 0, 2);
            AutomationProperties.SetAutomationId(name, "txtLnmMeeting");
            stack.Children.Add(name);
            var when = LnmUi.Text(DateLine(), 13, secondary: true);
            stack.Children.Add(when);

            // The link, or why there isn't one.
            var linkRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            bool hasLink = !string.IsNullOrEmpty(_meeting.MeetingUrl);
            linkRow.Children.Add(LnmUi.Glyph(hasLink ? LnmUi.GlyphLink : LnmUi.GlyphInfo, 13, "TraySubtitleColor"));
            if (hasLink)
            {
                var link = LnmUi.TextButton(ShortUrl(_meeting.MeetingUrl), () => Choose(LateNoMoreAlertChoice.Join), "btnLnmLink", underline: true, colour: LnmUi.Accent);
                link.ToolTip = _meeting.MeetingUrl;
                link.Margin = new Thickness(4, 0, 0, 0);
                linkRow.Children.Add(link);
            }
            else
            {
                var none = LnmUi.Text("Couldn't find a meeting link in this event.", 13, secondary: true);
                none.Margin = new Thickness(8, 0, 0, 0);
                AutomationProperties.SetAutomationId(none, "txtLnmNoLink");
                linkRow.Children.Add(none);
            }
            stack.Children.Add(linkRow);

            // The Mac's two mental-preparation prompts: just for the user, nothing is saved.
            // (The Mac's 🧠 / ⭐ emoji are drawn as accent glyphs: WPF text renders emoji in monochrome.)
            stack.Children.Add(Prompt(0xE82F, "What do you want to achieve in this meeting?", "txtLnmAchieve"));   // lightbulb
            stack.Children.Add(Prompt(0xE734, "How do you want to show up for this meeting?", "txtLnmShowUp"));    // star

            _actions = new StackPanel { Margin = new Thickness(0, 20, 0, 0) };
            // No "I'm attending" (report 2026-09-28_16-01-47): joining is detected, as on the Mac - a meeting app on the
            // microphone or camera, or the meeting's page open in the browser.
            var join = LnmUi.Pill(LnmUi.S("Join now", "lateNoMoreInfo", "joinNow"), true, () => Choose(LateNoMoreAlertChoice.Join), "btnLnmJoin", LnmUi.GlyphVideo);
            join.IsEnabled = hasLink;
            if (!hasLink) join.ToolTip = "No meeting link in this event";
            _actions.Children.Add(join);
            var quiet = new DockPanel { Margin = new Thickness(0, 12, 0, 0), LastChildFill = false };
            var snooze = LnmUi.TextButton("Snooze 1 min", () => Choose(LateNoMoreAlertChoice.Snooze), "btnLnmSnooze");
            var skip = LnmUi.TextButton(LnmUi.S("I don't need to attend", "lateNoMoreInfo", "dontNeedToAttend"), () => Choose(LateNoMoreAlertChoice.NotAttending), "btnLnmNotAttending");
            DockPanel.SetDock(snooze, Dock.Left);
            DockPanel.SetDock(skip, Dock.Right);
            quiet.Children.Add(snooze);
            quiet.Children.Add(skip);
            _actions.Children.Add(quiet);
            stack.Children.Add(_actions);

            // Shown instead of the actions once the user is attending (the Mac's "you can close this popup").
            _attending = new StackPanel { Margin = new Thickness(0, 20, 0, 0), Visibility = Visibility.Collapsed };
            var ok = new StackPanel { Orientation = Orientation.Horizontal };
            ok.Children.Add(LnmUi.Badge(LnmUi.GlyphCheck, 22, LnmUi.OkGreen));
            _attendingText = LnmUi.Text(_test ? "This is where your meeting's focus session would start." : "A focus session is covering your meeting. You can close this.", 13);
            var okText = _attendingText;
            okText.Margin = new Thickness(10, 0, 0, 0);
            okText.VerticalAlignment = VerticalAlignment.Center;
            okText.MaxWidth = 340;
            AutomationProperties.SetAutomationId(okText, "txtLnmAttending");
            ok.Children.Add(okText);
            _attending.Children.Add(ok);
            var close = LnmUi.Pill(LnmUi.S("Close", "lateNoMoreInfo", "close"), false, () => Choice?.Invoke(LateNoMoreAlertChoice.GotIt), "btnLnmDone");
            close.HorizontalAlignment = HorizontalAlignment.Right;
            close.Margin = new Thickness(0, 14, 0, 0);
            _attending.Children.Add(close);
            stack.Children.Add(_attending);

            return Frame(stack, 460);
        }

        private FrameworkElement Prompt(int glyph, string question, string automationId)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            var icon = LnmUi.Glyph(glyph, 13);
            icon.Foreground = LnmUi.Accent;
            icon.Margin = new Thickness(0, 1, 8, 0);
            head.Children.Add(icon);
            head.Children.Add(LnmUi.Text(question, 13, FontWeights.SemiBold));
            panel.Children.Add(head);
            var box = LnmUi.Input(automationId, question);
            box.AcceptsReturn = true;
            box.TextWrapping = TextWrapping.Wrap;
            box.MinHeight = 36;
            box.MaxHeight = 90;
            panel.Children.Add(box);
            return panel;
        }

        private string DateLine()
        {
            DateTime s = _meeting.Start;
            string day = s.Date == _now().Date ? "Today, " : (s.Date == _now().Date.AddDays(1) ? "Tomorrow, " : "");
            string text = day + s.ToString("dddd d MMMM, ", CultureInfo.CurrentCulture) + s.ToString("t", CultureInfo.CurrentCulture)
                          + " – " + _meeting.End.ToString("t", CultureInfo.CurrentCulture);
            if (!string.IsNullOrWhiteSpace(_meeting.CalendarName)) text += "  ·  " + _meeting.CalendarName;
            return text;
        }

        private static string ShortUrl(string url)
        {
            try
            {
                var u = new Uri(url);
                string path = u.AbsolutePath.TrimEnd('/');
                string s = u.Host + (path.Length > 28 ? path.Substring(0, 28) + "…" : path);
                return s;
            }
            catch { return url; }
        }

        private void Choose(LateNoMoreAlertChoice choice)
        {
            if (choice == LateNoMoreAlertChoice.Join) ShowAttending();
            Choice?.Invoke(choice);
        }

        /// <summary>Whether the user has joined (clicked Join now, or joining was detected).</summary>
        public bool Joined => _joined;

        /// <summary>Joining was detected: stop the countdown's verbal alerts and say so.</summary>
        public void ShowJoined()
        {
            if (Kind != LateNoMoreAlertKind.Main) return;
            ShowAttending();
            if (_attendingText != null && !_test) _attendingText.Text = "You've joined. A focus session is covering your meeting.";
        }

        private void ShowAttending()
        {
            _joined = true;
            StopSpeaking();
            if (_actions != null) _actions.Visibility = Visibility.Collapsed;
            if (_attending != null) _attending.Visibility = Visibility.Visible;
        }

        // ------------------------------------------------------------------ countdown + verbal alerts

        private void TickClock()
        {
            DateTime now = _now();
            _countdown.Text = LateNoMoreCountdown.Text(_meeting.Start - now, out bool late);
            if (late) _countdown.Foreground = LnmUi.LateRed;
            else _countdown.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");

            // Verbal alerts at the meeting's start, until the user joins, for at most five minutes (Mac customAlertVc).
            if (!_verbal || _joined || now < _meeting.Start) return;
            if (_verbalStarted == null) { _verbalStarted = now; LogWriter.LogWrite("LateNoMore: verbal alerts started"); }
            if (now - _verbalStarted.Value > TimeSpan.FromMinutes(5)) { StopSpeaking(); return; }
            if ((now - _lastBark).TotalSeconds < 5) return;
            _lastBark = now;
            Speak(BarkPool[_random.Next(BarkPool.Length)]);
        }

        private void Speak(string text)
        {
            try
            {
                var synth = _speech as System.Speech.Synthesis.SpeechSynthesizer;
                if (synth == null)
                {
                    synth = new System.Speech.Synthesis.SpeechSynthesizer();
                    synth.SetOutputToDefaultAudioDevice();
                    _speech = synth;
                }
                synth.SpeakAsyncCancelAll();
                synth.SpeakAsync(text);
            }
            catch (Exception ex) { LogWriter.LogWrite("LateNoMore Speak - " + ex.Message); _speech = null; }
        }

        private void StopSpeaking()
        {
            try
            {
                if (_speech is System.Speech.Synthesis.SpeechSynthesizer synth) { synth.SpeakAsyncCancelAll(); synth.Dispose(); }
            }
            catch { }
            _speech = null;
        }

        internal void Stop()
        {
            _clock.Stop();
            StopSpeaking();
        }
    }

    /// <summary>Hosts the alert on top of everything (including a full-screen routine). The heads-up sits bottom-right.</summary>
    internal sealed class LateNoMoreAlertWindow : Window
    {
        private readonly LateNoMoreAlertView _view;
        private bool _closingQuietly;

        public LateNoMoreAlertKind Kind => _view.Kind;
        public LateNoMoreEvent Meeting => _view.Meeting;
        public bool Joined => _view.Joined;
        public bool IsTest { get; }
        public event Action<LateNoMoreAlertChoice> Choice;

        /// <summary>Joining was detected: say so, then close by itself (the Mac's popup closes once the meeting is attended).</summary>
        public void ShowJoined(TimeSpan closeAfter)
        {
            _view.ShowJoined();
            var timer = new DispatcherTimer { Interval = closeAfter };
            timer.Tick += (s, e) => { timer.Stop(); if (IsLoaded) CloseQuietly(); };
            timer.Start();
        }

        public LateNoMoreAlertWindow(LateNoMoreEvent meeting, LateNoMoreAlertKind kind, bool verbalAlerts, bool test)
        {
            _view = new LateNoMoreAlertView(meeting, kind, verbalAlerts, test);
            IsTest = test;
            _view.Choice += c => Choice?.Invoke(c);
            Title = "Late No More";
            Content = _view;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            ShowInTaskbar = kind == LateNoMoreAlertKind.Main;
            ShowActivated = kind == LateNoMoreAlertKind.Main;
            AutomationProperties.SetAutomationId(this, "wndLateNoMore");
            MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed && !(e.OriginalSource is TextBox)) try { DragMove(); } catch { } };
            Closed += (s, e) => _view.Stop();
            try
            {
                var icon = Application.Current?.MainWindow?.Icon;
                if (icon != null) Icon = icon;
            }
            catch { }
        }

        /// <summary>The harness places alerts off-screen with this (never set in the app).</summary>
        internal static Action<Window> TestPlacement;

        public void ShowAlert()
        {
            if (Kind == LateNoMoreAlertKind.Main) WindowStartupLocation = WindowStartupLocation.CenterScreen;
            else
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Loaded += (s, e) =>
                {
                    Rect area = SystemParameters.WorkArea;
                    Left = area.Right - ActualWidth - 8;
                    Top = area.Bottom - ActualHeight - 8;
                };
            }
            if (TestPlacement != null) { TestPlacement(this); Show(); return; }
            Show();
            if (Kind == LateNoMoreAlertKind.Main) BringForward();
        }

        public void BringForward()
        {
            try
            {
                if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
                Topmost = false;
                Topmost = true;
                Activate();
            }
            catch (Exception ex) { LogWriter.LogWrite("LateNoMore BringForward - " + ex.Message); }
        }

        /// <summary>Closes without counting as a choice (replaced by the full alert, or dismissed on another device).</summary>
        public void CloseQuietly()
        {
            _closingQuietly = true;
            Close();
        }

        internal bool ClosedQuietly => _closingQuietly;
    }

    internal static class TextBlockExtensions
    {
        /// <summary>Tabular digits, so the countdown doesn't jitter as it ticks.</summary>
        public static void FontFeatures(this TextBlock block) => System.Windows.Documents.Typography.SetNumeralAlignment(block, FontNumeralAlignment.Tabular);
    }
}
