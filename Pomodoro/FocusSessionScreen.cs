using Focus_Bear.API;
using Focus_Bear.Focus;
using Focus_Bear.ToDoScreens;
using Focus_Bear.Utils;
using FocusBear.Core.Focus;
using FocusBear.Core.Stats;
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
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Focus_Bear.Pomodoro
{
    /// <summary>
    /// The ongoing-focus-session screen (the Mac's <c>FocusSessionScreen</c>, Figma "1. Ongoing Session"), shown on
    /// the Focus Session row while a session runs. It replaces the web To Do Player page and the old
    /// "focus mode ends in" view: countdown and progress, −5 / +5 minutes, pause / resume / end, the focus goal,
    /// the active task with its queue, and a quick brain dump.
    ///
    /// Everything routes through the app's existing paths so their rules keep applying: pause and resume through
    /// <see cref="TrayMenu.ToggleFocusPauseFromUI"/> (Super Focus and class sessions refuse, a buddy-approved
    /// postpone session is kept running), stop through <see cref="App.StopFocusSessionFromEvents"/> (the Super
    /// Focus password, the teacher lock, the postpone session's "back to my habits"), and the queue through
    /// <see cref="FocusSessionQueueWriter"/>.
    /// </summary>
    public sealed class FocusSessionScreen : UserControl
    {
        private const int StepMinutes = 5;
        private readonly FocusBearMainWindow _parent;
        private readonly AppSettings _settings = AppSettings.GetInstance;
        private readonly DispatcherTimer _tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private readonly DispatcherTimer _goalSave = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        private readonly DispatcherTimer _brainDumpSave = new DispatcherTimer { Interval = NotesTimingDelay };
        private readonly TextBox _goalBox = new TextBox();
        private readonly TextBox _brainDumpBox = new TextBox();
        private readonly FocusSessionTaskTimer _taskTimer = new FocusSessionTaskTimer();
        private readonly FocusSessionBrainDump _brainDump = new FocusSessionBrainDump();

        private IList<FocusSessionTask> _tasks = new List<FocusSessionTask>();
        private string _renderedKey = "";
        private string _lastRawTasks;
        private bool _goalExpanded, _notesExpanded = true, _brainDumpLoaded, _suppressBrainDumpSave, _suppressGoalSave, _busy;
        private int _idleTicks;
        private string _error;

        // Live pieces updated every tick without a rebuild.
        private TextBlock _countdown, _progressText, _timeOnTask, _alignment, _workTitle, _goalHint, _brainDumpHint;
        private ColumnDefinition _progressDone, _progressLeft;

        private static TimeSpan NotesTimingDelay => FocusBear.Core.Notes.NotesTiming.BrainDumpSaveDelay;

        /// <summary>Minutes the −5 / +5 buttons have moved this session's end, for the progress total.</summary>
        private static int _adjustedSeconds;
        private static string _adjustedSessionKey;

        public FocusSessionScreen(FocusBearMainWindow parent)
        {
            _parent = parent;
            SetResourceReference(BackgroundProperty, "BackgroundColor");
            AutomationProperties.SetAutomationId(this, "FocusSessionScreen");
            _tick.Tick += (s, e) => Tick();
            _goalSave.Tick += (s, e) => { _goalSave.Stop(); CommitGoal(); };
            _brainDumpSave.Tick += async (s, e) => { _brainDumpSave.Stop(); await SaveBrainDump(); };
            _goalBox.TextChanged += (s, e) =>
            {
                if (_goalHint != null) _goalHint.Visibility = _goalBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                if (!_suppressGoalSave) { _goalSave.Stop(); _goalSave.Start(); }
            };
            _brainDumpBox.TextChanged += (s, e) =>
            {
                if (_brainDumpHint != null) _brainDumpHint.Visibility = _brainDumpBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                if (!_suppressBrainDumpSave) { _brainDumpUnsaved = true; _brainDumpSave.Stop(); _brainDumpSave.Start(); }
            };
            SizeChanged += (s, e) => { if (LayoutKey() != _renderedLayout) Render(); };
            IsVisibleChanged += (s, e) =>
            {
                if (IsVisible) { _tick.Start(); return; }
                _tick.Stop();
                FlushPendingSaves();
            };
            FocusSessionQueueWriter.Changed += () => { if (IsVisible) Dispatcher.BeginInvoke(new Action(() => Refresh())); };
            // A class session's checklist lives in its own store (the teacher amends it, ticks come back from the server).
            Focus_Bear.Focus.ClassSessionChecklistStore.Shared.Changed += () => { if (IsVisible) Refresh(force: true); };
            // A brain dump saved on the Notes screen as it was left reaches the server after this screen is
            // already up: read it again then.
            Focus_Bear.BrainDump.BrainDumpWindow.BrainDumpSaved += () => Dispatcher.BeginInvoke(new Action(() => { if (IsVisible) _ = LoadBrainDump(); }));
        }

        /// <summary>Called whenever the screen is put on the Focus Session row. Re-reads everything, because the tray, the block popup or another device may have changed it meanwhile.</summary>
        public void ScreenAppeared()
        {
            _idleTicks = 0;
            Refresh(force: true);
            _tick.Start();
            // Every time, not only the first: the Notes screen edits the same brain dump, and its changes never
            // showed here because this was read once per run of the app. LoadBrainDump leaves the box alone while
            // it has typing of its own that is unsaved or in progress.
            _ = LoadBrainDump();
        }

        private FocusSessionStrings Strings
        {
            get
            {
                var config = _settings.ConfigFileStrings;
                return new FocusSessionStrings(config?["focusStateVcInfo"] as JObject, config?["focusProgressInfo"] as JObject);
            }
        }

        private App TheApp => Application.Current as App;

        private bool PostponeLocked
        {
            get { try { return TheApp != null && TheApp.IsPostponeLockedFocusSession; } catch { return false; } }
        }

        private bool SuperStrictAutoStarted
        {
            get { try { return TheApp != null && TheApp.IsSuperStrictAutoStartedSession; } catch { return false; } }
        }

        /// <summary>A buddy-approved postpone session or a Late No More meeting session: no Pause, only Stop.</summary>
        private bool Unpausable
        {
            get { try { return TheApp != null && TheApp.IsUnpausableFocusSession; } catch { return false; } }
        }

        private FocusSessionRules ReadRules()
        {
            bool classSession = TeacherSessionManager.Instance.IsTeacherSessionActive();
            // The buddy's postpone session (and a meeting session covering a postponed routine) is super strict, as on
            // the Mac: without this, -5 could whittle it down to a minute and end the cover for the postponed routine.
            bool superStrict = _settings.bUseSuperPomodoroMode || Services.BlockingScheduleManager.IsIronFocusScheduleActive() || SuperStrictAutoStarted;
            return new FocusSessionRules(_settings.isFocusModeRunning, _settings.isFocusModePaused, _settings.bUseSuperPomodoroMode, superStrict, classSession);
        }

        // ------------------------------------------------------------------ clock

        private int RemainingSeconds(FocusSessionRules rules)
        {
            if (rules.Phase == FocusSessionPhase.Paused) return Math.Max(0, _settings.nPausedFocusRemainingSeconds);
            if (rules.Phase != FocusSessionPhase.Running) return 0;
            return DateTime.TryParse(_settings.sRunningFocusModeEndTime, out DateTime end) ? FocusSessionClock.RemainingSeconds(end, DateTime.Now) : 0;
        }

        private int TotalSeconds(int remaining)
        {
            if (_adjustedSessionKey != _settings.sRunningFocusModeStartTime) { _adjustedSessionKey = _settings.sRunningFocusModeStartTime; _adjustedSeconds = 0; }
            int.TryParse(_settings.sRunningFocusModeMinutes, out int planned);
            return FocusSessionProgress.TotalSeconds(planned, _adjustedSeconds, remaining);
        }

        private void Tick()
        {
            if (!IsVisible) return;
            var rules = ReadRules();
            if (rules.Phase == FocusSessionPhase.Idle)
            {
                // A session that ended without anyone replacing this screen (a silent finish, another device):
                // hand the row back to the start screen rather than claim a session is still on.
                if (++_idleTicks >= 2) LeaveForStartScreen();
                return;
            }
            _idleTicks = 0;
            Refresh();
        }

        private void LeaveForStartScreen()
        {
            _tick.Stop();
            if (_parent == null || !_parent.IsVisible || _parent.RightContent.Content != this) return;
            LogWriter.LogWrite("FocusSessionScreen - no session left to show, returning to the start screen");
            _parent.HandleLeafSelection(LeftButtonType.StartFocusSession);
        }

        /// <summary>Rebuilds when anything structural changed; otherwise only the clock-driven text moves.</summary>
        private void Refresh(bool force = false)
        {
            var rules = ReadRules();
            if (_lastRawTasks != _settings.sRawTasks || force)
            {
                _lastRawTasks = _settings.sRawTasks;
                _tasks = FocusSessionQueueWriter.NativeOwnsQueue ? FocusSessionQueueWriter.Tasks : Focus_Bear.Focus.ClassSessionChecklistStore.Shared.Tasks;
            }
            var active = FocusSessionQueue.Active(_tasks);
            _taskTimer.Activate(active?.Id, DateTime.Now);
            if (rules.Phase == FocusSessionPhase.Running) _taskTimer.Resume(DateTime.Now); else _taskTimer.Pause(DateTime.Now);
            if (!_goalBox.IsKeyboardFocusWithin && !_goalSave.IsEnabled && _goalBox.Text != (_settings.sRunningFocusModeIntention ?? ""))
            {
                _suppressGoalSave = true;
                _goalBox.Text = _settings.sRunningFocusModeIntention ?? "";
                _suppressGoalSave = false;
            }
            string key = StructureKey(rules);
            if (force || key != _renderedKey) { Render(); return; }
            UpdateLive(rules);
        }

        private string StructureKey(FocusSessionRules rules) =>
            string.Join("|", rules.Phase, rules.IsSuperFocus, rules.IsSuperStrict, rules.IsClassSession, PostponeLocked, Unpausable,
                _settings.sRunningFocusModeName, FocusSessionQueue.Encode(_tasks), _goalExpanded, _notesExpanded, _error, _busy,
                _goalExpanded ? "" : _settings.sRunningFocusModeIntention, TotalSeconds(RemainingSeconds(rules)) / 60, AlignmentShown());

        private void UpdateLive(FocusSessionRules rules)
        {
            var strings = Strings;
            int remaining = RemainingSeconds(rules), total = TotalSeconds(remaining), elapsed = Math.Max(0, total - remaining);
            if (_countdown != null) _countdown.Text = FocusSessionClock.Headline(remaining, strings.Units);
            if (_progressText != null) _progressText.Text = strings.Progress(elapsed, total);
            if (_progressDone != null)
            {
                double progress = FocusSessionClock.Progress(elapsed, total);
                _progressDone.Width = new GridLength(progress, GridUnitType.Star);
                _progressLeft.Width = new GridLength(1 - progress, GridUnitType.Star);
            }
            if (_timeOnTask != null) _timeOnTask.Text = strings.TimeOnTask(_taskTimer.ElapsedSeconds(DateTime.Now));
            if (_alignment != null) _alignment.Text = AlignmentText(strings);
            if (_workTitle != null) _workTitle.Text = WorkTitle(strings);
        }

        private string BlockListName(FocusSessionStrings strings) =>
            string.IsNullOrWhiteSpace(_settings.sRunningFocusModeName) ? strings["titleUntitledFocusMode"] : _settings.sRunningFocusModeName;

        private string WorkTitle(FocusSessionStrings strings) =>
            FocusSessionProgress.WorkTitle(FocusSessionQueue.Active(_tasks), _settings.sRunningFocusModeIntention, BlockListName(strings));

        private bool AlignmentShown() =>
            TeacherSessionManager.Instance.IsTeacherSessionActive() ? TeacherSessionManager.Instance.bTeacherSessionAiBlockingEnabled : _settings.bAiCheckUrlEnabled;

        private static string AlignmentText(FocusSessionStrings strings)
        {
            double score = FocusAlignmentScoreManager.CurrentFocusAlignmentScore;
            return score > 0 ? strings.AlignmentScore(score) : strings["labelFocusAlignmentScore"].Replace("<SCORE>", "--").Replace("--%", "--");
        }

        // ------------------------------------------------------------------ layout

        private enum Layout { Stacked, TwoColumn, WithRail }

        private string _renderedLayout = "";

        private string LayoutKey()
        {
            double width = ActualWidth > 0 ? ActualWidth : 1100;
            return Choose(width) + ":" + Math.Round(new FocusStartScale(width).Factor, 2).ToString(CultureInfo.InvariantCulture);
        }

        private static Layout Choose(double width) => width >= 1250 ? Layout.WithRail : width >= 880 ? Layout.TwoColumn : Layout.Stacked;

        private void Render()
        {
            var rules = ReadRules();
            _renderedKey = StructureKey(rules);
            _renderedLayout = LayoutKey();
            var strings = Strings;
            var p = FocusStartPalette.Current();
            double width = ActualWidth > 0 ? ActualWidth : 1100;
            var s = new FocusStartScale(width);
            var layout = Choose(width);
            Detach(_goalBox);
            Detach(_brainDumpBox);
            bool goalFocused = _goalBox.IsKeyboardFocusWithin, dumpFocused = _brainDumpBox.IsKeyboardFocusWithin;
            int goalCaret = _goalBox.CaretIndex, dumpCaret = _brainDumpBox.CaretIndex;
            _countdown = _progressText = _timeOnTask = _alignment = _workTitle = _goalHint = _brainDumpHint = null;
            _progressDone = _progressLeft = null;

            FrameworkElement page;
            double gap = s.Pt(84), pad = s.Pt(64);
            if (layout == Layout.Stacked)
            {
                var stack = new StackPanel { Margin = new Thickness(pad, s.Pt(80), pad, pad) };
                stack.Children.Add(Spaced(Header(rules, strings, p, s, compact: true), s.Pt(24)));
                stack.Children.Add(Spaced(TaskPanel(rules, strings, p, s, showActive: true, showQueue: false), s.Pt(24)));
                stack.Children.Add(Spaced(TimingRow(rules, strings, p, s, width - pad * 2), s.Pt(24)));
                stack.Children.Add(Spaced(GoalCard(rules, strings, p, s), s.Pt(24)));
                stack.Children.Add(Spaced(TaskPanel(rules, strings, p, s, showActive: false, showQueue: true), s.Pt(24)));
                stack.Children.Add(QuickTools(strings, p, s));
                page = stack;
            }
            else
            {
                var grid = new Grid { Margin = new Thickness(layout == Layout.WithRail ? 0 : pad, s.Pt(80), pad, pad) };
                if (layout == Layout.WithRail)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(s.Pt(719)) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(gap) });
                }
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(gap) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(300, s.Pt(1018))) });
                int column = 0;
                if (layout == Layout.WithRail)
                {
                    var rail = SideRail(strings, p, s);
                    rail.Margin = new Thickness(0, s.Pt(92), 0, 0);
                    Grid.SetColumn(rail, 0);
                    grid.Children.Add(rail);
                    column = 2;
                }
                var session = new StackPanel();
                session.Children.Add(Spaced(Header(rules, strings, p, s, compact: false), s.Pt(24)));
                session.Children.Add(Spaced(Bear(rules, s, null), s.Pt(24)));
                session.Children.Add(Spaced(TimerPanel(rules, strings, p, s, compact: false), s.Pt(24)));
                session.Children.Add(Spaced(GoalCard(rules, strings, p, s), s.Pt(24)));
                if (layout != Layout.WithRail) session.Children.Add(QuickTools(strings, p, s));
                Grid.SetColumn(session, column);
                grid.Children.Add(session);
                var tasks = TaskPanel(rules, strings, p, s, showActive: true, showQueue: true);
                tasks.Margin = new Thickness(0, s.Pt(92), 0, 0);
                tasks.VerticalAlignment = VerticalAlignment.Top;
                Grid.SetColumn(tasks, column + 2);
                grid.Children.Add(tasks);
                page = grid;
            }
            var previous = Content as ScrollViewer;
            var scroller = new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            Content = scroller;
            if (previous != null) { double offset = previous.VerticalOffset; scroller.Loaded += (o, e) => scroller.ScrollToVerticalOffset(offset); }
            UpdateLive(rules);
            // A rebuild while typing (the queue changed underneath) must not take the caret away.
            if (goalFocused || dumpFocused)
            {
                var box = goalFocused ? _goalBox : _brainDumpBox;
                int caret = goalFocused ? goalCaret : dumpCaret;
                Dispatcher.BeginInvoke(new Action(() => { if (box.IsVisible) { box.Focus(); box.CaretIndex = Math.Min(caret, box.Text.Length); } }), DispatcherPriority.Input);
            }
        }

        private static FrameworkElement Spaced(FrameworkElement element, double bottom)
        {
            element.Margin = new Thickness(element.Margin.Left, element.Margin.Top, element.Margin.Right, bottom);
            return element;
        }

        private static void Detach(TextBox box)
        {
            if (box.Parent is Panel panel) panel.Children.Remove(box);
            else if (box.Parent is Decorator decorator) decorator.Child = null;
        }

        private static Effect Shadow(double blur) => new DropShadowEffect { BlurRadius = blur, ShadowDepth = 0, Opacity = 0.1 };

        private static Border Panel(UIElement child, FocusStartPalette p, FocusStartScale s, double padding = 47, double radius = 60) => new Border
        {
            Child = child,
            Background = p.Surface,
            BorderBrush = p.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(s.Pt(radius)),
            Padding = new Thickness(s.Pt(padding)),
        };

        private static Border Card(UIElement child, FocusStartPalette p, FocusStartScale s, double padding = 24) => new Border
        {
            Child = child,
            Background = p.Surface,
            BorderBrush = p.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(s.Pt(20)),
            Padding = new Thickness(s.Pt(padding)),
            Effect = Shadow(10),
        };

        private static double Font(FocusStartScale s, double designPixels) => Math.Max(11, s.Pt(designPixels));

        // ------------------------------------------------------------------ header, bear, timer

        private FrameworkElement Header(FocusSessionRules rules, FocusSessionStrings strings, FocusStartPalette p, FocusStartScale s, bool compact)
        {
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
            var title = FocusStartUi.Text(rules.Headline(strings), Font(s, compact ? 48 : 64), p.Primary, FontWeights.SemiBold);
            title.TextAlignment = TextAlignment.Center;
            AutomationProperties.SetAutomationId(title, "txtFocusSessionHeadline");
            stack.Children.Add(title);
            if (rules.Phase != FocusSessionPhase.Idle)
            {
                _countdown = FocusStartUi.Text("", Font(s, compact ? 64 : 96), rules.Phase == FocusSessionPhase.Paused ? p.Secondary : p.Accent, FontWeights.Medium);
                _countdown.TextAlignment = TextAlignment.Center;
                AutomationProperties.SetAutomationId(_countdown, "txtFocusSessionCountdown");
                stack.Children.Add(_countdown);
            }
            return stack;
        }

        /// <summary>The design's focus bear on its shadow; greyed while the session is paused.</summary>
        private static FrameworkElement Bear(FocusSessionRules rules, FocusStartScale s, double? width)
        {
            double w = width ?? s.Pt(635 * 0.85);
            BitmapSource colour = new BitmapImage(new Uri("pack://application:,,,/Focus Bear;component/Images/FocusStart/focusSessionBear.png"));
            bool paused = rules.Phase == FocusSessionPhase.Paused;
            BitmapSource source = paused ? new FormatConvertedBitmap(colour, PixelFormats.Gray8, null, 0) : colour;
            var grid = new Grid { Width = w, Height = w * 655 / 635, HorizontalAlignment = HorizontalAlignment.Center };
            grid.Children.Add(new Ellipse
            {
                Width = w * 629 / 635,
                Height = w * 132 / 635,
                VerticalAlignment = VerticalAlignment.Bottom,
                Fill = new SolidColorBrush(Color.FromArgb(0x33, 0x27, 0x27, 0x2A)),
            });
            // Gray8 drops the alpha channel, so the colour image masks the grey one back to the bear's outline.
            ImageBrush mask = paused ? new ImageBrush(colour) : null;
            if (mask != null) RenderOptions.SetBitmapScalingMode(mask, BitmapScalingMode.HighQuality);
            grid.Children.Add(FocusStartUi.Sharp(new Image { Source = source, Width = w, VerticalAlignment = VerticalAlignment.Top, Opacity = paused ? 0.6 : 1,
                OpacityMask = mask }));
            AutomationProperties.SetAutomationId(grid, "imgFocusSessionBear");
            return grid;
        }

        /// <summary>A short window: the timing card beside the bear, the bear on the left (the Mac's compact row).</summary>
        private FrameworkElement TimingRow(FocusSessionRules rules, FocusSessionStrings strings, FocusStartPalette p, FocusStartScale s, double available)
        {
            var panel = TimerPanel(rules, strings, p, s, compact: true);
            if (available < 560) return panel;
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var bear = Bear(rules, s, Math.Min((available - s.Pt(24)) / 2, s.Pt(635 * 0.7)));
            bear.Margin = new Thickness(0, 0, s.Pt(24), 0);
            bear.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(bear);
            Grid.SetColumn(panel, 1);
            row.Children.Add(panel);
            return row;
        }

        private FrameworkElement TimerPanel(FocusSessionRules rules, FocusSessionStrings strings, FocusStartPalette p, FocusStartScale s, bool compact)
        {
            var stack = new StackPanel();
            string blockList = BlockListName(strings);
            string workTitle = WorkTitle(strings);
            var work = _workTitle = FocusStartUi.Text(workTitle, Font(s, compact ? 48 : 64), p.Primary, FontWeights.SemiBold);
            work.TextAlignment = TextAlignment.Center;
            if (compact) { work.MaxHeight = Font(s, 48) * 4.2; work.TextTrimming = TextTrimming.CharacterEllipsis; }
            AutomationProperties.SetAutomationId(work, "txtFocusSessionWorkTitle");
            stack.Children.Add(work);
            if (workTitle != blockList)
            {
                var list = FocusStartUi.Text(strings["blockListLabel"] + " " + blockList, Font(s, 24), p.Secondary);
                list.TextAlignment = TextAlignment.Center;
                list.Margin = new Thickness(0, s.Pt(8), 0, 0);
                stack.Children.Add(list);
            }
            if (AlignmentShown())
            {
                _alignment = FocusStartUi.Text("", Font(s, 24), p.Dark ? new SolidColorBrush(Color.FromRgb(0xA5, 0x94, 0xF9)) : p.SecondaryAccent, FontWeights.SemiBold);
                _alignment.TextAlignment = TextAlignment.Center;
                _alignment.Margin = new Thickness(0, s.Pt(8), 0, 0);
                AutomationProperties.SetAutomationId(_alignment, "txtFocusSessionAlignmentScore");
                stack.Children.Add(_alignment);
            }
            if (rules.Phase != FocusSessionPhase.Idle)
            {
                double height = Math.Max(8, s.Pt(27));
                var bar = new Grid { Height = height, Margin = new Thickness(0, s.Pt(compact ? 28 : 40), 0, 0) };
                _progressDone = new ColumnDefinition { Width = new GridLength(0, GridUnitType.Star) };
                _progressLeft = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
                bar.ColumnDefinitions.Add(_progressDone);
                bar.ColumnDefinitions.Add(_progressLeft);
                var track = new Border { Background = p.Track, CornerRadius = new CornerRadius(height / 2) };
                Grid.SetColumnSpan(track, 2);
                bar.Children.Add(track);
                bar.Children.Add(new Border { Background = p.Accent, CornerRadius = new CornerRadius(height / 2) });
                AutomationProperties.SetAutomationId(bar, "barFocusSessionProgress");
                stack.Children.Add(bar);
                _progressText = FocusStartUi.Text("", Font(s, 24), p.Secondary, FontWeights.Medium);
                _progressText.TextAlignment = TextAlignment.Center;
                _progressText.Margin = new Thickness(0, s.Pt(8), 0, 0);
                AutomationProperties.SetAutomationId(_progressText, "txtFocusSessionProgress");
                stack.Children.Add(_progressText);
            }
            stack.Children.Add(Controls(rules, strings, p, s, compact));
            if (rules.Phase == FocusSessionPhase.Running && (rules.CanPause && !Unpausable))
            {
                // The design only stops from the paused state; the old screen could stop directly, so that stays as a quiet link.
                var link = FocusStartUi.Clickable(FocusStartUi.Text(strings["endFocusSessionText"], Font(s, 32), p.Secondary, wrap: false), RequestEndSession, "btnFocusSessionEndLink", strings["endFocusSessionText"]);
                link.HorizontalAlignment = HorizontalAlignment.Center;
                link.Margin = new Thickness(0, s.Pt(16), 0, 0);
                stack.Children.Add(link);
            }
            var panel = Panel(stack, p, s, compact ? 28 : 47);
            AutomationProperties.SetAutomationId(panel, "pnlFocusSessionTimer");
            return panel;
        }

        private FrameworkElement Controls(FocusSessionRules rules, FocusSessionStrings strings, FocusStartPalette p, FocusStartScale s, bool compact)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, s.Pt(compact ? 28 : 40), 0, 0) };
            double big = Math.Max(52, s.Pt(compact ? 190 : 233)), small = Math.Max(44, s.Pt(compact ? 160 : 192)), gap = s.Pt(24);
            bool locked = PostponeLocked;
            bool durations = rules.ShowsDurationControls && !locked && TotalSeconds(RemainingSeconds(rules)) > 0;
            if (durations)
                row.Children.Add(Margin(Adjust(-StepMinutes, strings, p, s, small, rules.CanReduce,
                    rules.CanReduce ? strings["toolTipReduceDuration"] : strings["noticeDurationLockedInSuperStrict"]), 0, 0, gap, 0));
            if (rules.Phase == FocusSessionPhase.Paused)
            {
                row.Children.Add(Margin(Circle(FocusStartUi.Glyph(0xE768, big * 0.3, p.Primary), big, p, Resume, "btnFocusSessionResume", strings["resumeText"]), 0, 0, gap, 0));
                row.Children.Add(Circle(FocusStartUi.Glyph(0xE71A, big * 0.3, p.Primary), big, p, RequestEndSession, "btnFocusSessionStop", EndLabel(strings)));
            }
            else if (rules.CanPause && !Unpausable)
                row.Children.Add(Circle(FocusStartUi.Glyph(0xE769, big * 0.3, p.Primary), big, p, Pause, "btnFocusSessionPause", strings["pauseText"]));
            else
                row.Children.Add(Circle(FocusStartUi.Glyph(0xE71A, big * 0.3, p.Primary), big, p, RequestEndSession, "btnFocusSessionStop", EndLabel(strings)));
            if (durations)
                row.Children.Add(Margin(Adjust(StepMinutes, strings, p, s, small, true, strings["toolTipExtendDuration"]), gap, 0, 0, 0));
            if (locked || Unpausable)
            {
                // Stop is the only control, so it is labelled: "Back to my habits" over a postponed routine, else "End Session".
                var stack = new StackPanel { Margin = row.Margin };
                row.Margin = new Thickness(0);
                stack.Children.Add(row);
                var label = FocusStartUi.Text(EndLabel(strings), Font(s, 32), p.Secondary, wrap: false);
                label.HorizontalAlignment = HorizontalAlignment.Center;
                label.Margin = new Thickness(0, s.Pt(12), 0, 0);
                stack.Children.Add(label);
                return stack;
            }
            return row;
        }

        private string EndLabel(FocusSessionStrings strings) =>
            PostponeLocked ? _settings.GetConfigStringSafe("Back to my habits", "common", "buttonBackToMyHabitsText") : strings["endFocusSessionText"];

        private static FrameworkElement Margin(FrameworkElement element, double left, double top, double right, double bottom)
        {
            element.Margin = new Thickness(left, top, right, bottom);
            return element;
        }

        private static Button Circle(UIElement inner, double diameter, FocusStartPalette p, Action click, string id, string tooltip)
        {
            var grid = new Grid { Width = diameter, Height = diameter };
            grid.Children.Add(new Ellipse { Fill = p.Surface, Stroke = p.Border, StrokeThickness = 1, Effect = Shadow(18) });
            if (inner is FrameworkElement fe) { fe.HorizontalAlignment = HorizontalAlignment.Center; fe.VerticalAlignment = VerticalAlignment.Center; }
            grid.Children.Add(inner);
            var button = FocusStartUi.Clickable(grid, click, id, tooltip);
            button.VerticalAlignment = VerticalAlignment.Center;
            return button;
        }

        private Button Adjust(int minutes, FocusSessionStrings strings, FocusStartPalette p, FocusStartScale s, double diameter, bool enabled, string tooltip)
        {
            var label = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var number = FocusStartUi.Text((minutes > 0 ? "+" : "−") + Math.Abs(minutes), Math.Max(13, diameter * 0.24), p.Primary, FontWeights.Bold, wrap: false);
            number.HorizontalAlignment = HorizontalAlignment.Center;
            label.Children.Add(number);
            var unit = FocusStartUi.Text(strings["labelMinutesShort"], Math.Max(10, diameter * 0.15), p.Secondary, wrap: false);
            unit.HorizontalAlignment = HorizontalAlignment.Center;
            label.Children.Add(unit);
            var button = Circle(label, diameter, p, () => AdjustDuration(minutes), minutes > 0 ? "btnFocusSessionExtend" : "btnFocusSessionReduce", tooltip);
            button.IsEnabled = enabled;
            button.Opacity = enabled ? 1 : 0.4;
            ToolTipService.SetShowOnDisabled(button, true);
            return button;
        }

        // ------------------------------------------------------------------ goal

        private FrameworkElement GoalCard(FocusSessionRules rules, FocusSessionStrings strings, FocusStartPalette p, FocusStartScale s)
        {
            var stack = new StackPanel();
            var header = new DockPanel();
            var chevron = FocusStartUi.Glyph(_goalExpanded ? 0xE70E : 0xE70D, Font(s, 28), p.Secondary);
            DockPanel.SetDock(chevron, Dock.Right);
            header.Children.Add(chevron);
            var titles = new StackPanel();
            titles.Children.Add(FocusStartUi.Text(strings["titleIntention"], Font(s, 32), p.Primary, FontWeights.SemiBold));
            string goal = _settings.sRunningFocusModeIntention ?? "";
            if (!_goalExpanded && goal.Trim().Length > 0)
            {
                var summary = FocusStartUi.Text(goal, Font(s, 24), p.Secondary);
                summary.MaxHeight = Font(s, 24) * 2.8;
                summary.TextTrimming = TextTrimming.CharacterEllipsis;
                AutomationProperties.SetAutomationId(summary, "txtFocusSessionGoalSummary");
                titles.Children.Add(summary);
            }
            header.Children.Add(titles);
            // The whole row toggles, not just the chevron (Mac #2604).
            stack.Children.Add(FocusStartUi.Clickable(header, () => { _goalExpanded = !_goalExpanded; Render(); }, "btnFocusSessionGoal", strings["titleIntention"]));
            if (_goalExpanded)
            {
                var body = new StackPanel { Margin = new Thickness(0, s.Pt(12), 0, 0) };
                if (rules.IsGoalLocked)
                {
                    body.Children.Add(FocusStartUi.Text(goal.Trim().Length == 0 ? strings["placeholderFocusGoal"] : goal, Font(s, 36), p.Secondary));
                    var notice = FocusStartUi.Text(_settings.GetConfigStringSafe(strings["noticeGoalLocked"], "popUpAddNewFocusGoalVcInfo", "cannotChangeIntentionInSuperStrictText"), Font(s, 24), p.Secondary);
                    notice.Margin = new Thickness(0, s.Pt(12), 0, 0);
                    AutomationProperties.SetAutomationId(notice, "txtFocusSessionGoalLocked");
                    body.Children.Add(notice);
                }
                else
                {
                    _goalBox.AcceptsReturn = true;
                    _goalBox.TextWrapping = TextWrapping.Wrap;
                    _goalBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                    _goalBox.BorderThickness = new Thickness(0);
                    _goalBox.Background = Brushes.Transparent;
                    _goalBox.Foreground = p.Primary;
                    _goalBox.CaretBrush = p.Primary;
                    _goalBox.FontSize = Font(s, 36);
                    AutomationProperties.SetAutomationId(_goalBox, "txtFocusSessionGoal");
                    AutomationProperties.SetName(_goalBox, strings["titleIntention"]);
                    var hint = _goalHint = FocusStartUi.Text(strings["placeholderFocusGoal"], Font(s, 36), p.Secondary);
                    hint.IsHitTestVisible = false;
                    hint.Margin = new Thickness(3, 1, 0, 0);
                    hint.Visibility = _goalBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                    var well = new Grid();
                    well.Children.Add(hint);
                    well.Children.Add(_goalBox);
                    body.Children.Add(new Border
                    {
                        Child = well,
                        BorderBrush = p.Border,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(s.Pt(20)),
                        Padding = new Thickness(s.Pt(12)),
                        Height = Math.Max(80, s.Pt(260)),
                        Background = p.Field,
                    });
                }
                stack.Children.Add(body);
            }
            var card = Card(stack, p, s);
            AutomationProperties.SetAutomationId(card, "cardFocusSessionGoal");
            return card;
        }

        private void CommitGoal()
        {
            var rules = ReadRules();
            string goal = _goalBox.Text;
            if (rules.IsGoalLocked || rules.Phase == FocusSessionPhase.Idle || goal == (_settings.sRunningFocusModeIntention ?? "")) return;
            _settings.sRunningFocusModeIntention = goal;
            App.FocusButton.SendFocusGoal(goal);
            LogWriter.LogWrite("FocusSessionScreen - focus goal changed");
        }

        // ------------------------------------------------------------------ tasks

        private FrameworkElement TaskPanel(FocusSessionRules rules, FocusSessionStrings strings, FocusStartPalette p, FocusStartScale s, bool showActive, bool showQueue)
        {
            var stack = new StackPanel();
            if (!string.IsNullOrEmpty(_error))
            {
                var error = FocusStartUi.Text(_error, Font(s, 28), p.Warning);
                error.Margin = new Thickness(0, 0, 0, s.Pt(24));
                AutomationProperties.SetAutomationId(error, "txtFocusSessionTaskError");
                stack.Children.Add(error);
            }
            if (showActive) stack.Children.Add(ActiveSection(rules, strings, p, s));
            if (showQueue)
            {
                var queue = QueueSection(rules, strings, p, s);
                if (showActive) queue.Margin = new Thickness(0, s.Pt(60), 0, 0);
                stack.Children.Add(queue);
            }
            var panel = Panel(stack, p, s);
            AutomationProperties.SetAutomationId(panel, showActive ? "pnlFocusSessionTasks" : "pnlFocusSessionQueue");
            return panel;
        }

        private FrameworkElement ActiveSection(FocusSessionRules rules, FocusSessionStrings strings, FocusStartPalette p, FocusStartScale s)
        {
            var section = new StackPanel();
            var heading = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, s.Pt(24)) };
            heading.Children.Add(FocusStartUi.Text(strings["labelActiveTask"], Font(s, 48), p.Primary, FontWeights.SemiBold, wrap: false));
            heading.Children.Add(new Ellipse { Width = Math.Max(6, s.Pt(12)), Height = Math.Max(6, s.Pt(12)), Fill = p.Accent, Margin = new Thickness(s.Pt(8), 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            section.Children.Add(heading);
            var active = FocusSessionQueue.Active(_tasks);
            section.Children.Add(active == null ? EmptyActive(rules, strings, p, s) : ActiveCard(active, rules, strings, p, s));
            return section;
        }

        /// <summary>No task yet: the whole card is the button that opens the picker (Mac report 2026-09-15_16-46-10).</summary>
        private FrameworkElement EmptyActive(FocusSessionRules rules, FocusSessionStrings strings, FocusStartPalette p, FocusStartScale s)
        {
            var stack = new StackPanel();
            if (IsClassChecklist)
            {
                // The teacher owns the list: either they added none, or the student has ticked them all.
                bool allDone = _tasks.Count > 0;
                stack.Children.Add(FocusStartUi.Text(strings[allDone ? "titleClassAllDone" : "titleClassNoTasks"], Font(s, 50), p.Primary, FontWeights.SemiBold));
                var note = FocusStartUi.Text(strings[allDone ? "bodyClassAllDone" : "bodyClassNoTasks"], Font(s, 36), p.Secondary);
                note.Margin = new Thickness(0, s.Pt(8), 0, 0);
                stack.Children.Add(note);
                var card = Card(stack, p, s, 38);
                AutomationProperties.SetAutomationId(card, allDone ? "cardFocusSessionClassAllDone" : "cardFocusSessionClassNoTasks");
                return card;
            }
            stack.Children.Add(FocusStartUi.Text(strings["titleNoActiveTask"], Font(s, 50), p.Primary, FontWeights.SemiBold));
            var body = FocusStartUi.Text(strings["bodyNoActiveTask"], Font(s, 36), p.Secondary);
            body.Margin = new Thickness(0, s.Pt(8), 0, s.Pt(8));
            stack.Children.Add(body);
            var add = new StackPanel { Orientation = Orientation.Horizontal };
            var plus = FocusStartUi.Glyph(0xE710, Font(s, 28), p.Accent);
            plus.Margin = new Thickness(0, 0, s.Pt(10), 0);
            add.Children.Add(plus);
            add.Children.Add(FocusStartUi.Text(strings["buttonAddTask"], Font(s, 32), p.Accent, FontWeights.SemiBold, wrap: false));
            stack.Children.Add(add);
            var button = FocusStartUi.Clickable(Card(stack, p, s, 38), OpenPicker, "btnFocusSessionAddFirstTask", strings["toolTipAddTasks"]);
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.IsEnabled = !rules.IsTaskEditingLocked && !_busy;
            button.Opacity = button.IsEnabled ? 1 : 0.5;
            return button;
        }

        private FrameworkElement ActiveCard(FocusSessionTask active, FocusSessionRules rules, FocusSessionStrings strings, FocusStartPalette p, FocusStartScale s)
        {
            bool locked = rules.IsTaskEditingLocked || _busy;
            // A class task cannot be edited, but it can be ticked: that is the student's one job on the list.
            bool canComplete = IsClassChecklist ? !_busy && !Focus_Bear.Focus.ClassSessionChecklistStore.Shared.IsBusy : !locked;
            var stack = new StackPanel();
            var titleRow = new DockPanel();
            if (!IsClassChecklist)
            {
                var edit = FocusStartUi.Clickable(FocusStartUi.Glyph(0xE70F, Font(s, 32), p.Secondary), () => _ = EditTask(active.Id), "btnFocusSessionEditActive", strings["menuEditTask"]);
                edit.IsEnabled = !locked;
                edit.Opacity = locked ? 0.4 : 1;
                edit.VerticalAlignment = VerticalAlignment.Top;
                edit.Margin = new Thickness(s.Pt(16), s.Pt(6), 0, 0);
                DockPanel.SetDock(edit, Dock.Right);
                titleRow.Children.Add(edit);
            }
            if (!string.IsNullOrEmpty(active.Icon))
            {
                var emoji = FocusStartUi.Text(active.Icon, Font(s, 64), p.Primary, wrap: false);
                emoji.FontFamily = FocusStartUi.Emoji;
                emoji.Margin = new Thickness(0, 0, s.Pt(16), 0);
                DockPanel.SetDock(emoji, Dock.Left);
                titleRow.Children.Add(emoji);
            }
            var title = Selectable(active.Title, Font(s, 50), p.Primary, FontWeights.SemiBold);
            AutomationProperties.SetAutomationId(title, "txtFocusSessionActiveTitle");
            titleRow.Children.Add(title);
            stack.Children.Add(titleRow);

            var chips = new WrapPanel { Margin = new Thickness(0, s.Pt(20), 0, 0) };
            // The project is already the big title above the countdown whenever the task has one; say it once.
            if (!string.IsNullOrWhiteSpace(active.ProjectName) && active.ProjectName.Trim() != WorkTitle(strings))
                chips.Children.Add(Chip("📁 " + active.ProjectName, p, s));
            if (active.TopScore.HasValue && !double.IsNaN(active.TopScore.Value))
                chips.Children.Add(Chip("⚡ " + strings.TopScoreBadge(active.TopScore.Value), p, s, p.SecondaryAccent, Brushes.White));
            chips.Children.Add(Chip("🕒 " + strings.DurationBadge(active.DurationMinutes), p, s, p.Metadata, p.Accent));
            stack.Children.Add(chips);

            // The card's own running state — a pulsing dot and the time on this task this session.
            bool running = rules.Phase == FocusSessionPhase.Running;
            var indicator = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, s.Pt(20), 0, 0) };
            var dot = new Ellipse { Width = Math.Max(8, s.Pt(20)), Height = Math.Max(8, s.Pt(20)), Fill = running ? p.Accent : p.Secondary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, s.Pt(8), 0) };
            // Only while on screen: this card is rebuilt every minute, and a forever animation on a dropped card never stops.
            if (running) Utils.LoadedAnimation.Run(dot, dot, OpacityProperty, new DoubleAnimation(1, 0.35, TimeSpan.FromSeconds(1)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            indicator.Children.Add(dot);
            _timeOnTask = FocusStartUi.Text("", Font(s, 24), running ? p.Accent : p.Secondary, FontWeights.Medium, wrap: false);
            _timeOnTask.VerticalAlignment = VerticalAlignment.Center;
            AutomationProperties.SetAutomationId(_timeOnTask, "txtFocusSessionTimeOnTask");
            indicator.Children.Add(_timeOnTask);
            stack.Children.Add(indicator);

            string details = ToDoHtml.PlainText(active.Details ?? "").Trim();
            if (details.Length > 0)
            {
                // Selectable: task notes are often a link to the document the task is about.
                var detail = Selectable(details, Font(s, 36), p.Primary, FontWeights.Normal);
                detail.Margin = new Thickness(0, s.Pt(20), 0, 0);
                detail.MaxHeight = 220;
                detail.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                stack.Children.Add(detail);
            }

            var done = new StackPanel { Orientation = Orientation.Horizontal };
            done.Children.Add(Margin(FocusStartUi.Glyph(0xE73E, Font(s, 24), Brushes.White), 0, 0, s.Pt(8), 0));
            done.Children.Add(FocusStartUi.Text(strings["buttonMarkComplete"], Font(s, 32), Brushes.White, FontWeights.Medium, wrap: false));
            var face = new Border { Child = done, Background = p.Accent, CornerRadius = new CornerRadius(s.Pt(20)), Padding = new Thickness(s.Pt(38), s.Pt(18), s.Pt(38), s.Pt(18)) };
            var complete = FocusStartUi.Clickable(face, () => Complete(active.Id), "btnFocusSessionMarkComplete", strings["buttonMarkComplete"]);
            complete.HorizontalAlignment = HorizontalAlignment.Left;
            complete.Margin = new Thickness(0, s.Pt(28), 0, 0);
            complete.IsEnabled = canComplete;
            complete.Opacity = canComplete ? 1 : 0.5;
            stack.Children.Add(complete);

            var card = new Border
            {
                Child = stack,
                Background = p.Surface,
                BorderBrush = p.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(s.Pt(31)),
                Padding = new Thickness(s.Pt(38)),
                Effect = Shadow(12),
            };
            AutomationProperties.SetAutomationId(card, "cardFocusSessionActiveTask");
            return card;
        }

        private static TextBox Selectable(string text, double size, Brush color, FontWeight weight) => new TextBox
        {
            Text = text ?? "",
            IsReadOnly = true,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = color,
            FontSize = size,
            FontWeight = weight,
            TextWrapping = TextWrapping.Wrap,
            Padding = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.IBeam,
            FocusVisualStyle = null,
        };

        private static Border Chip(string text, FocusStartPalette p, FocusStartScale s, Brush background = null, Brush foreground = null)
        {
            var chip = FocusStartUi.Badge(text, background ?? p.Field, foreground ?? p.Primary, s);
            if (background == null) { chip.BorderBrush = p.Border; chip.BorderThickness = new Thickness(1); }
            chip.Margin = new Thickness(0, 0, s.Pt(12), s.Pt(8));
            ((TextBlock)chip.Child).FontSize = Font(s, 24);
            ((TextBlock)chip.Child).FontFamily = FocusStartUi.Emoji;
            return chip;
        }

        private FrameworkElement QueueSection(FocusSessionRules rules, FocusSessionStrings strings, FocusStartPalette p, FocusStartScale s)
        {
            bool locked = rules.IsTaskEditingLocked || _busy;
            var section = new StackPanel();
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, s.Pt(24)) };
            var plusFace = new Border { Width = Math.Max(26, s.Pt(78)), Height = Math.Max(26, s.Pt(78)), CornerRadius = new CornerRadius(s.Pt(20)), Background = p.Primary };
            var plusGlyph = FocusStartUi.Glyph(0xE710, Font(s, 28), p.Surface);
            plusGlyph.HorizontalAlignment = HorizontalAlignment.Center;
            plusFace.Child = plusGlyph;
            if (!IsClassChecklist)
            {
                var add = FocusStartUi.Clickable(plusFace, OpenPicker, "btnFocusSessionAddTasks", strings["toolTipAddTasks"]);
                add.IsEnabled = !locked;
                add.Opacity = locked ? 0.5 : 1;
                DockPanel.SetDock(add, Dock.Right);
                header.Children.Add(add);
            }
            var queued = FocusSessionQueue.Queued(_tasks);
            string headingText = IsClassChecklist
                ? strings["labelClassChecklist"].Replace("<DONE>", ClassSessionChecklist.CompletedCount(_tasks).ToString()).Replace("<TOTAL>", _tasks.Count.ToString())
                : strings.QueueHeading(FocusSessionQueue.Outstanding(queued).Count);
            var heading = FocusStartUi.Text(headingText, Font(s, 48), p.Primary);
            heading.VerticalAlignment = VerticalAlignment.Center;
            AutomationProperties.SetAutomationId(heading, "txtFocusSessionQueueHeading");
            header.Children.Add(heading);
            section.Children.Add(header);
            if (queued.Count == 0 && !IsClassChecklist)
                section.Children.Add(FocusStartUi.Text(strings["labelEmptyQueue"], Font(s, 36), p.Secondary));
            foreach (var task in queued)
                section.Children.Add(QueueRow(task, locked, strings, p, s));
            return section;
        }

        private FrameworkElement QueueRow(FocusSessionTask task, bool locked, FocusSessionStrings strings, FocusStartPalette p, FocusStartScale s)
        {
            var row = new DockPanel();
            var menu = RowMenu(task, strings);
            if (menu.Items.Count > 0)
            {
                var more = FocusStartUi.Clickable(FocusStartUi.Glyph(0xE712, Font(s, 32), p.Secondary), () => { }, "btnFocusSessionRowMenu", strings["menuEditTask"] + ": " + task.Title);
                more.Width = Math.Max(24, s.Pt(96));
                bool menuLocked = IsClassChecklist ? Focus_Bear.Focus.ClassSessionChecklistStore.Shared.IsBusy : locked;
                more.IsEnabled = !menuLocked;
                more.Opacity = menuLocked ? 0.4 : 1;
                more.ContextMenu = menu;
                more.Click += (o, e) => { more.ContextMenu.PlacementTarget = more; more.ContextMenu.IsOpen = true; };
                DockPanel.SetDock(more, Dock.Right);
                row.Children.Add(more);
            }
            if (!string.IsNullOrEmpty(task.Icon))
            {
                var emoji = FocusStartUi.Text(task.Icon, Font(s, 50), p.Primary, wrap: false);
                emoji.FontFamily = FocusStartUi.Emoji;
                emoji.VerticalAlignment = VerticalAlignment.Center;
                emoji.Margin = new Thickness(0, 0, s.Pt(24), 0);
                DockPanel.SetDock(emoji, Dock.Left);
                row.Children.Add(emoji);
            }
            var text = new StackPanel();
            var title = FocusStartUi.Text(task.Title, Font(s, 32), task.IsCompleted ? p.Secondary : p.Primary, FontWeights.SemiBold);
            if (task.IsCompleted) title.TextDecorations = TextDecorations.Strikethrough;
            AutomationProperties.SetAutomationId(title, "txtFocusSessionQueueTitle");
            text.Children.Add(title);
            var chips = new WrapPanel { Margin = new Thickness(0, s.Pt(8), 0, 0) };
            if (task.TopScore.HasValue && !double.IsNaN(task.TopScore.Value))
                chips.Children.Add(Chip("⚡ " + strings.TopScoreBadge(task.TopScore.Value), p, s, p.SecondaryAccent, Brushes.White));
            chips.Children.Add(Chip("🕒 " + strings.DurationBadge(task.DurationMinutes), p, s, p.Metadata, p.Accent));
            text.Children.Add(chips);
            row.Children.Add(text);
            var card = Card(row, p, s);
            card.Padding = new Thickness(s.Pt(36), s.Pt(24), s.Pt(24), s.Pt(16));
            card.Margin = new Thickness(0, 0, 0, s.Pt(16));
            AutomationProperties.SetAutomationId(card, "rowFocusSessionQueue");
            return card;
        }

        /// <summary>The design's per-row overflow menu; Move up / down stand in for its drag handle, as on the Mac.</summary>
        private ContextMenu RowMenu(FocusSessionTask task, FocusSessionStrings strings)
        {
            var menu = new ContextMenu();
            Action<string, int, Action, string> item = (header, glyph, action, id) =>
            {
                var mi = new MenuItem { Header = header, Icon = new TextBlock { Text = ((char)glyph).ToString(), FontFamily = FocusStartUi.Symbols, FontSize = 13 } };
                mi.Click += (o, e) => action();
                AutomationProperties.SetAutomationId(mi, id);
                menu.Items.Add(mi);
            };
            if (IsClassChecklist)
            {
                // Tasks are done in the teacher's order: the latest tick can be undone, nothing else moves.
                if (ClassSessionChecklist.CanUncomplete(_tasks, task.Id))
                    item(strings["menuMarkNotDone"], 0xE7A7, () => _ = TickClassTask(task.Id, false), "mnuFocusSessionMarkNotDone");
                return menu;
            }
            if (!task.IsCompleted)
            {
                item(strings["menuMarkComplete"], 0xE73E, () => Complete(task.Id), "mnuFocusSessionComplete");
                item(strings["menuFocusOnThisInstead"], 0xE768, () => FocusOn(task.Id), "mnuFocusSessionFocusOn");
            }
            item(strings["menuEditTask"], 0xE70F, () => _ = EditTask(task.Id), "mnuFocusSessionEdit");
            if (!FocusSessionQueue.SameQueue(FocusSessionQueue.Moving(_tasks, task.Id, -1), _tasks))
                item(strings["menuMoveTaskUp"], 0xE74A, () => Apply(FocusSessionQueue.Moving(_tasks, task.Id, -1)), "mnuFocusSessionMoveUp");
            if (!FocusSessionQueue.SameQueue(FocusSessionQueue.Moving(_tasks, task.Id, 1), _tasks))
                item(strings["menuMoveTaskDown"], 0xE74B, () => Apply(FocusSessionQueue.Moving(_tasks, task.Id, 1)), "mnuFocusSessionMoveDown");
            item(strings["menuRemoveFromQueue"], 0xE738, () => Apply(FocusSessionQueue.Removing(_tasks, task.Id)), "mnuFocusSessionRemove");
            return menu;
        }

        // ------------------------------------------------------------------ side rail and quick tools

        private FrameworkElement SideRail(FocusSessionStrings strings, FocusStartPalette p, FocusStartScale s)
        {
            var rail = new StackPanel();
            // The rail's panels hug the window's left edge, so only their right corners are rounded.
            var music = new DockPanel();
            var open = FocusStartUi.Glyph(0xE8A7, Font(s, 32), p.Secondary);
            DockPanel.SetDock(open, Dock.Right);
            music.Children.Add(open);
            var musicIcon = FocusStartUi.Glyph(0xEC4F, Font(s, 48), p.Primary);
            musicIcon.Margin = new Thickness(0, 0, s.Pt(16), 0);
            music.Children.Add(musicIcon);
            music.Children.Add(FocusStartUi.Text(strings["labelFocusMusic"], Font(s, 48), p.Primary, FontWeights.SemiBold, wrap: false));
            var musicButton = FocusStartUi.Clickable(RailPanel(music, p, s), OpenFocusMusic, "btnFocusSessionMusic", strings["buttonOpenFocusMusic"]);
            musicButton.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            musicButton.Margin = new Thickness(0, 0, 0, s.Pt(24));
            rail.Children.Add(musicButton);

            var notes = new StackPanel();
            var header = new DockPanel();
            var chevron = FocusStartUi.Glyph(_notesExpanded ? 0xE70D : 0xE76C, Font(s, 32), p.Secondary);
            DockPanel.SetDock(chevron, Dock.Right);
            header.Children.Add(chevron);
            var notesIcon = FocusStartUi.Glyph(0xE70B, Font(s, 48), p.Primary);
            notesIcon.Margin = new Thickness(0, 0, s.Pt(16), 0);
            header.Children.Add(notesIcon);
            header.Children.Add(FocusStartUi.Text(strings["labelNotes"], Font(s, 48), p.Primary, FontWeights.SemiBold, wrap: false));
            notes.Children.Add(FocusStartUi.Clickable(header, () => { _notesExpanded = !_notesExpanded; Render(); }, "btnFocusSessionNotes", strings["labelNotes"]));
            if (_notesExpanded)
            {
                var label = FocusStartUi.Text(strings["labelBrainDump"], Font(s, 24), p.Accent, FontWeights.Medium);
                label.Margin = new Thickness(0, s.Pt(16), 0, s.Pt(12));
                notes.Children.Add(label);
                notes.Children.Add(BrainDumpEditor(strings, p, s));
                var other = new DockPanel();
                var arrow = FocusStartUi.Glyph(0xE8A7, Font(s, 28), p.Primary);
                DockPanel.SetDock(arrow, Dock.Right);
                other.Children.Add(arrow);
                other.Children.Add(FocusStartUi.Text(strings["buttonSeeOtherNotes"], Font(s, 32), p.Primary, wrap: false));
                var otherButton = FocusStartUi.Clickable(Card(other, p, s), OpenNotes, "btnFocusSessionSeeNotes", strings["buttonSeeOtherNotes"]);
                otherButton.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                otherButton.Margin = new Thickness(0, s.Pt(16), 0, 0);
                notes.Children.Add(otherButton);
            }
            rail.Children.Add(RailPanel(notes, p, s));
            return rail;
        }

        private static Border RailPanel(UIElement child, FocusStartPalette p, FocusStartScale s) => new Border
        {
            Child = child,
            Background = p.Surface,
            BorderBrush = p.Border,
            BorderThickness = new Thickness(0, 1, 1, 1),
            CornerRadius = new CornerRadius(0, s.Pt(60), s.Pt(60), 0),
            Padding = new Thickness(s.Pt(47)),
        };

        private FrameworkElement BrainDumpEditor(FocusSessionStrings strings, FocusStartPalette p, FocusStartScale s)
        {
            _brainDumpBox.AcceptsReturn = true;
            _brainDumpBox.TextWrapping = TextWrapping.Wrap;
            _brainDumpBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _brainDumpBox.BorderThickness = new Thickness(0);
            _brainDumpBox.Background = Brushes.Transparent;
            _brainDumpBox.Foreground = p.Primary;
            _brainDumpBox.CaretBrush = p.Primary;
            _brainDumpBox.FontSize = Font(s, 36);
            AutomationProperties.SetAutomationId(_brainDumpBox, "txtFocusSessionBrainDump");
            AutomationProperties.SetName(_brainDumpBox, strings["labelBrainDump"]);
            var hint = _brainDumpHint = FocusStartUi.Text(strings["placeholderBrainDump"], Font(s, 36), p.Secondary);
            hint.IsHitTestVisible = false;
            hint.Margin = new Thickness(3, 1, 0, 0);
            hint.Visibility = _brainDumpBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            var well = new Grid();
            well.Children.Add(hint);
            well.Children.Add(_brainDumpBox);
            return new Border
            {
                Child = well,
                Background = p.Field,
                BorderBrush = p.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(s.Pt(31)),
                Padding = new Thickness(s.Pt(24)),
                Height = Math.Max(140, s.Pt(560)),
            };
        }

        /// <summary>Below the rail's width the two panels become buttons under the goal (the Mac's quick tools).</summary>
        private FrameworkElement QuickTools(FocusSessionStrings strings, FocusStartPalette p, FocusStartScale s)
        {
            var row = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
            Func<int, string, Action, string, UIElement> tool = (glyph, label, action, id) =>
            {
                var inner = new StackPanel { Orientation = Orientation.Horizontal };
                inner.Children.Add(Margin(FocusStartUi.Glyph(glyph, Font(s, 32), p.Accent), 0, 0, s.Pt(12), 0));
                inner.Children.Add(FocusStartUi.Text(label, Font(s, 32), p.Primary, FontWeights.Medium, wrap: false));
                var button = FocusStartUi.Clickable(Card(inner, p, s, 20), action, id, label);
                button.Margin = new Thickness(s.Pt(12));
                return button;
            };
            row.Children.Add(tool(0xEC4F, strings["labelFocusMusic"], OpenFocusMusic, "btnFocusSessionMusic"));
            row.Children.Add(tool(0xE70B, strings["labelNotes"], OpenNotes, "btnFocusSessionNotes"));
            return row;
        }

        private void OpenFocusMusic() => TheApp?.ShowFocusMusic();

        private void OpenNotes()
        {
            FlushPendingSaves();
            TheApp?.ShowBrainDumpWindow();
        }

        /// <summary>Typing in the box that the server does not have yet. Only that stops a newer brain dump being shown.</summary>
        private bool _brainDumpUnsaved;

        private async Task LoadBrainDump()
        {
            _brainDumpLoaded = true;
            try
            {
                string text = await _brainDump.LoadAsync();
                // Unsaved typing here wins (its save is on the way). Having the caret in the box does not: the box
                // keeps keyboard focus across redraws, so a focus test threw away every change made on the Notes
                // screen until the panel was collapsed and opened again.
                if (text == null || _brainDumpUnsaved || _brainDumpBox.Text == text) return;
                int caret = _brainDumpBox.CaretIndex;
                _suppressBrainDumpSave = true;
                _brainDumpBox.Text = text;
                _suppressBrainDumpSave = false;
                _brainDumpBox.CaretIndex = Math.Min(caret, text.Length);
            }
            catch (Exception ex) { LogWriter.LogWrite("FocusSessionScreen LoadBrainDump ex - " + ex.Message); }
        }

        private async Task SaveBrainDump()
        {
            try
            {
                string title = _settings.GetConfigStringSafe("Brain Dump", "notesViewInfo", "brainDumpTitle");
                string sent = _brainDumpBox.Text;
                if (!await _brainDump.SaveAsync(sent, title)) LogWriter.LogWrite("FocusSessionScreen - brain dump not saved");
                else if (_brainDumpBox.Text == sent) _brainDumpUnsaved = false;   // typing that carried on is still unsaved
            }
            catch (Exception ex) { LogWriter.LogWrite("FocusSessionScreen SaveBrainDump ex - " + ex.Message); }
        }

        private void FlushPendingSaves()
        {
            if (_goalSave.IsEnabled) { _goalSave.Stop(); CommitGoal(); }
            if (_brainDumpSave.IsEnabled) { _brainDumpSave.Stop(); _ = SaveBrainDump(); }
        }

        // ------------------------------------------------------------------ actions

        private void Apply(IList<FocusSessionTask> updated)
        {
            if (ReadRules().IsTaskEditingLocked || FocusSessionQueue.SameQueue(updated, _tasks)) return;
            _tasks = updated;
            FocusSessionQueueWriter.Persist(updated);
            _lastRawTasks = _settings.sRawTasks;
            Render();
        }

        private void Complete(string id)
        {
            if (IsClassChecklist) { _ = TickClassTask(id, true); return; }
            if (ReadRules().IsTaskEditingLocked) return;
            _tasks = FocusSessionQueueWriter.Complete(_tasks, id);
            _lastRawTasks = _settings.sRawTasks;
            Render();
        }

        /// <summary>The student's side of a class session: only the queue's source differs from a personal session.</summary>
        private bool IsClassChecklist => !FocusSessionQueueWriter.NativeOwnsQueue;

        /// <summary>Ticks or unticks a teacher's task; the store redraws the list at once and again after the server answers.</summary>
        private async Task TickClassTask(string id, bool completed)
        {
            _error = null;
            bool ok = await Focus_Bear.Focus.ClassSessionChecklistStore.Shared.SetCompletedAsync(id, completed);
            if (!ok && !Focus_Bear.Focus.ClassSessionChecklistStore.Shared.IsBusy)
            {
                _error = Strings["errorClassTaskSync"];
                Refresh(force: true);
            }
        }

        private void FocusOn(string id) => Apply(FocusSessionQueue.Focusing(_tasks, id));

        private void OpenPicker()
        {
            if (ReadRules().IsTaskEditingLocked || _busy) return;
            var owner = Window.GetWindow(this);
            var picker = new FocusSessionTaskPicker(Strings, new HashSet<string>(_tasks.Select(t => t.Id))) { Owner = owner };
            if (_parent != null) _parent.GridShade.Visibility = Visibility.Visible;
            try { picker.ShowDialog(); }
            finally { if (_parent != null) _parent.GridShade.Visibility = Visibility.Collapsed; }
            if (picker.Chosen.Count == 0) return;
            LogWriter.LogWrite("FocusSessionScreen - queued " + picker.Chosen.Count + " task(s)");
            Apply(FocusSessionQueue.Appending(_tasks, picker.Chosen));
        }

        /// <summary>
        /// Edits a queued task in the app's own to-do editor, on the server's latest copy (a cached title cannot
        /// prove the task still exists), then mirrors what was saved back into the queue.
        /// </summary>
        private async Task EditTask(string id)
        {
            if (ReadRules().IsTaskEditingLocked || _busy) return;
            var strings = Strings;
            _busy = true;
            _error = null;
            Render();
            try
            {
                var api = new FocusBearAPIClient();
                var latest = await Latest(api, id);
                if (latest == null) { _error = strings["errorCouldNotLoadTask"]; return; }
                var store = new ToDoStore(ToDoStrings.Load());
                await store.LoadProjectsAsync();
                var editor = new ToDoEditorWindow(store, latest, null) { Owner = Window.GetWindow(this) };
                if (_parent != null) _parent.GridShade.Visibility = Visibility.Visible;
                try { editor.ShowDialog(); }
                finally { if (_parent != null) _parent.GridShade.Visibility = Visibility.Collapsed; }
                if (!editor.Committed) return;
                var saved = await Latest(api, id);
                var current = FocusSessionQueueWriter.Tasks;
                IList<FocusSessionTask> updated;
                if (saved == null) updated = FocusSessionQueue.Removing(current, id);   // deleted in the editor
                else
                {
                    string project = saved.TagList.FirstOrDefault(t => t.Id == saved.ProjectId)?.Text;
                    updated = current.Select(t => t.Id != id ? t : t.With(title: saved.Title, details: saved.Details ?? "",
                        durationSeconds: saved.DurationSeconds > 0 ? saved.DurationSeconds : t.DurationSeconds,
                        status: saved.IsCompleted ? FocusSessionTaskStatus.Completed : t.IsCompleted ? FocusSessionTaskStatus.NotStarted : t.Status,
                        projectName: project, projectId: saved.ProjectId, clearProject: string.IsNullOrEmpty(saved.ProjectId))).ToList();
                    // Completed in the editor (already saved there): hand the session on, as the menu does.
                    if (saved.IsCompleted) updated = FocusSessionQueue.Completing(updated, id);
                }
                FocusSessionQueueWriter.Persist(updated);
                _tasks = updated;
                _lastRawTasks = _settings.sRawTasks;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("FocusSessionScreen EditTask ex - " + ex.Message);
                _error = strings["errorCouldNotSaveTask"];
            }
            finally
            {
                _busy = false;
                Render();
            }
        }

        private static async Task<ToDoTask> Latest(FocusBearAPIClient api, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            string json = await api.GetToDosByIds(new[] { id });
            return json == null ? null : ToDoJson.ReadTasks(json)?.FirstOrDefault(t => t.Id == id);
        }

        private void Pause()
        {
            TheApp?.wndTrayMenu?.ToggleFocusPauseFromUI();
            Refresh(force: true);
        }

        private void Resume()
        {
            TheApp?.wndTrayMenu?.ToggleFocusPauseFromUI();
            Refresh(force: true);
        }

        private void AdjustDuration(int minutes)
        {
            var tray = TheApp?.wndTrayMenu;
            if (tray == null) return;
            if (minutes < 0 && !ReadRules().CanReduce) return;
            DateTime.TryParse(_settings.sRunningFocusModeEndTime, out DateTime before);
            if (!tray.AdjustRunningFocusSession(minutes)) return;
            DateTime.TryParse(_settings.sRunningFocusModeEndTime, out DateTime after);
            TotalSeconds(0);   // make sure the adjustment belongs to this session
            _adjustedSeconds += (int)Math.Round((after - before).TotalSeconds);
            Refresh(force: true);
        }

        /// <summary>
        /// End Session. A class session and a buddy-approved postpone session go straight to the stop path, whose
        /// gate answers for them (the teacher's unlock request; "back to my habits"). Otherwise the design's
        /// "End session early?" comes first, then the ordinary stop with its Super Focus password.
        /// </summary>
        private void RequestEndSession()
        {
            var app = TheApp;
            if (app == null) return;
            if (TeacherSessionManager.Instance.IsTeacherSessionActive() || PostponeLocked)
            {
                app.StopFocusSessionFromEvents();
                return;
            }
            var strings = Strings;
            if (_parent != null) _parent.GridShade.Visibility = Visibility.Visible;
            var confirm = new FBMessageBox(strings["bodyEndSessionConfirm"], strings["titleEndSessionConfirm"], MessageBoxButton.YesNo,
                strings["endFocusSessionText"], _settings.GetConfigStringSafe(strings["buttonCancelText"], "common", "buttonCancelText"))
            { Owner = Window.GetWindow(this) };
            confirm.ShowDialog();
            if (_parent != null) _parent.GridShade.Visibility = Visibility.Collapsed;
            if (confirm.btnClicked != MessageBoxResult.Yes) return;
            LogWriter.LogWrite("[user-action] FocusSessionScreen - end session");
            app.StopFocusSessionFromEvents();
        }
    }
}
