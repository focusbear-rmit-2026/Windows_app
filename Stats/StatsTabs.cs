using FocusBear.Core.Stats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Focus_Bear.StatsScreens
{
    /// <summary>Streak and Progress both read <c>GET /user/stats/onboarding</c>.</summary>
    internal abstract class OnboardingTab : StatsTabView
    {
        protected OnboardingStats Stats;

        protected OnboardingTab(StatsStrings strings) : base(strings) { }

        public override async void Load()
        {
            int generation = ++Generation;
            if (Stats == null) ShowLoading();
            var stats = StatsWire.ReadOnboardingStats(await Api().GetOnboardingStats());
            if (generation != Generation) return;
            if (stats == null)
            {
                if (Stats == null) ShowError();
                return;
            }
            Stats = stats;
            Render();
        }

        protected abstract void Render();
    }

    // ================================================================== Streak

    internal sealed class StreakTab : OnboardingTab
    {
        internal StreakTab(StatsStrings strings) : base(strings) { }

        protected override void Render()
        {
            var stack = new StackPanel();
            stack.Children.Add(Row("\U0001F305", Strings.MorningRoutine, Stats.MorningRoutineCompletionStreakDays ?? 0, Stats.RoutinesThreshold ?? 0,
                StatsText.Fill(Strings.PercentValue, ("value", StatsText.PlainNumber(Stats.AverageMorningRoutinesCompletionPercentage ?? 0)))));
            stack.Children.Add(Row("\U0001F319", Strings.EveningRoutine, Stats.EveningRoutineCompletionStreakDays ?? 0, Stats.RoutinesThreshold ?? 0,
                StatsText.Fill(Strings.PercentValue, ("value", StatsText.PlainNumber(Stats.AverageEveningRoutinesCompletionPercentage ?? 0)))));
            stack.Children.Add(Row("\U0001F3AF", Strings.FocusBlocks, Stats.FocusModeCompletionStreakDays ?? 0, Stats.FocusModesThreshold ?? 0,
                StatsText.Fill(Strings.PerDayValue, ("value", StatsText.PlainNumber(Stats.AverageNumFocusModesCompletedPerDay ?? 0)))));
            Show(stack);
        }

        private Border Row(string icon, string title, int streak, int threshold, string average)
        {
            var badge = new Border { Width = 52, Height = 52, CornerRadius = new CornerRadius(26), Background = StatsUi.Orange, Margin = new Thickness(0, 0, 16, 0) };
            badge.Child = new TextBlock { Text = icon, FontSize = 22, Foreground = StatsUi.OnOrange, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

            var middle = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            middle.Children.Add(StatsUi.Text(title, 16, FontWeights.SemiBold));
            if (threshold - streak > 0)
            {
                var levelUp = StatsUi.Text(StatsText.Fill(Strings.DaysUntilLevelUp, ("days", (threshold - streak).ToString())), 12, secondary: true);
                levelUp.Margin = new Thickness(0, 3, 0, 0);
                middle.Children.Add(levelUp);
            }
            var averageLine = new TextBlock { FontSize = 12, Margin = new Thickness(0, 3, 0, 0) };
            averageLine.SetResourceReference(TextBlock.ForegroundProperty, "TraySubtitleColor");
            averageLine.Inlines.Add(new Run(Strings.AverageCompletion + " "));
            var value = new Run(average) { FontWeight = FontWeights.SemiBold };
            value.SetResourceReference(TextElement.ForegroundProperty, "ForegroundColor");
            averageLine.Inlines.Add(value);
            middle.Children.Add(averageLine);

            var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, MinWidth = 90 };
            right.Children.Add(StatsUi.WithOrangeText(new TextBlock { Text = streak.ToString(), FontSize = 30, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center }));
            var caption = StatsUi.Text(Strings.DayStreak, 11, secondary: true);
            caption.HorizontalAlignment = HorizontalAlignment.Center;
            right.Children.Add(caption);

            var row = new DockPanel();
            DockPanel.SetDock(badge, Dock.Left);
            DockPanel.SetDock(right, Dock.Right);
            row.Children.Add(badge);
            row.Children.Add(right);
            row.Children.Add(middle);
            return StatsUi.Card(row);
        }
    }

    // ================================================================== Progress (levels)

    internal sealed class ProgressTab : OnboardingTab
    {
        private int? _previewLevel;
        private bool _showsNextLevels;

        internal ProgressTab(StatsStrings strings) : base(strings) { }

        private int DisplayedLevel => _previewLevel ?? Stats.Level;

        protected override void Render()
        {
            var stack = new StackPanel();
            stack.Children.Add(LevelHeaders());
            stack.Children.Add(LevelCard());
            if (Stats.Level < LevelTargets.MaxLevel) stack.Children.Add(NextLevels());
            Show(stack);
        }

        private string HeaderTitle(int index)
        {
            if (index == 0) return Strings.Start;
            if (index == LevelTargets.MaxLevel + 1) return Strings.End;
            return StatsText.Fill(Strings.LevelTitle, ("level", index.ToString()));
        }

        private FrameworkElement LevelHeaders()
        {
            var row = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
            var range = LevelTargets.VisibleHeaderRange(Stats.Level);
            for (int index = range.Start; index < range.End; index++)
            {
                int captured = index;
                var pill = StatsUi.Pill(HeaderTitle(index), index == DisplayedLevel, (s, e) =>
                {
                    _previewLevel = captured == Stats.Level ? (int?)null : captured;
                    Render();
                });
                if (index > Stats.Level && index != DisplayedLevel) pill.Opacity = 0.6;
                row.Children.Add(pill);
            }
            return row;
        }

        private FrameworkElement LevelCard()
        {
            int level = DisplayedLevel;
            var left = new StackPanel { Width = 130, Margin = new Thickness(0, 0, 24, 0) };
            left.Children.Add(Badge(level));
            var name = StatsUi.Text(Strings.LevelName(level), 13, FontWeights.SemiBold);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.Margin = new Thickness(0, 6, 0, 0);
            left.Children.Add(name);

            FrameworkElement right = level > 0
                ? Requirements(StatsText.Fill(Strings.LevelGoals, ("level", level.ToString())), LevelTargets.Requirements(level, Stats))
                : Checklist();
            var row = new DockPanel();
            DockPanel.SetDock(left, Dock.Left);
            row.Children.Add(left);
            row.Children.Add(right);
            return StatsUi.Card(row);
        }

        /// <summary>The level's bear figure, as on the web; a numbered orange disc for a level with no art.</summary>
        private FrameworkElement Badge(int level)
        {
            string asset = LevelArtwork.AssetName(level);
            if (asset != null)
            {
                try
                {
                    var image = new BitmapImage(new Uri("pack://application:,,,/Focus Bear;component/Images/Levels/" + asset + ".png"));
                    var figure = new Image { Source = image, Width = 104, Height = 116, Stretch = Stretch.Uniform, ToolTip = Strings.LevelName(level) };
                    RenderOptions.SetBitmapScalingMode(figure, BitmapScalingMode.HighQuality);   // the art is about 5x this size; the default scaler pixelates it
                    return figure;
                }
                catch (Exception) { /* the art is optional; fall through to the disc */ }
            }
            var disc = new Border { Width = 64, Height = 64, CornerRadius = new CornerRadius(32), Background = StatsUi.Orange, HorizontalAlignment = HorizontalAlignment.Center };
            disc.Child = new TextBlock { Text = level.ToString(), FontSize = 28, FontWeight = FontWeights.Bold, Foreground = StatsUi.OnOrange, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            return disc;
        }

        private FrameworkElement Requirements(string title, IList<LevelRequirement> requirements)
        {
            var stack = new StackPanel();
            var heading = StatsUi.Text(title, 14, FontWeights.SemiBold);
            heading.Margin = new Thickness(0, 0, 0, 8);
            stack.Children.Add(heading);
            foreach (var requirement in requirements)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                if (!requirement.IsBoolean)
                {
                    var counter = new Border { Background = StatsUi.Tint(StatsUi.Orange, 0.15), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 8, 0) };
                    counter.Child = StatsUi.Text(requirement.Current + "/" + requirement.Target, 11, FontWeights.SemiBold, wrap: false);
                    row.Children.Add(counter);
                }
                var name = StatsUi.Text(Strings.RequirementName(requirement.Kind), 13, wrap: false);
                name.VerticalAlignment = VerticalAlignment.Center;
                row.Children.Add(name);
                row.Children.Add(Tick(requirement.IsMet));
                stack.Children.Add(row);
            }
            return stack;
        }

        private static TextBlock Tick(bool met)
        {
            var tick = new TextBlock { Text = met ? "✔" : "○", FontSize = 13, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            if (met) tick.Foreground = StatsUi.Positive;
            else tick.SetResourceReference(TextBlock.ForegroundProperty, "TraySubtitleColor");
            return tick;
        }

        /// <summary>Level 0: the web's explainer links, opened in the browser.</summary>
        private FrameworkElement Checklist()
        {
            var stack = new StackPanel();
            stack.Children.Add(LinkRow(Strings.SetupBlockedUrls, Stats.HasEditedAlwaysBlockedUrls == true, StatsExternalLinks.SetupBlockedUrls));
            var mobile = LinkRow(Strings.InstallMobileApp, Stats.HasInstalledMobileApp == true, StatsExternalLinks.InstallMobileApp);
            var stores = new TextBlock { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
            stores.Inlines.Add(StatsUi.Link("iOS", StatsExternalLinks.IosApp));
            stores.Inlines.Add(new Run("  ·  "));
            stores.Inlines.Add(StatsUi.Link("Android", StatsExternalLinks.AndroidApp));
            mobile.Children.Add(stores);
            stack.Children.Add(mobile);
            stack.Children.Add(LinkRow(Strings.EditYourHabits, Stats.HasEditedSettings == true, StatsExternalLinks.EditHabits));
            return stack;
        }

        private static StackPanel LinkRow(string title, bool done, string url)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            var text = new TextBlock { FontSize = 13 };
            text.Inlines.Add(StatsUi.Link(title, url));
            row.Children.Add(text);
            var mark = new TextBlock { Text = done ? "✔" : "✖", FontSize = 13, Margin = new Thickness(8, 0, 0, 0), Foreground = done ? StatsUi.Positive : StatsUi.Danger };
            row.Children.Add(mark);
            return row;
        }

        private FrameworkElement NextLevels()
        {
            var stack = new StackPanel();
            var toggle = StatsUi.Button((_showsNextLevels ? "▲  " : "▼  ") + Strings.NextLevelRequirements, (s, e) => { _showsNextLevels = !_showsNextLevels; Render(); });
            toggle.HorizontalAlignment = HorizontalAlignment.Left;
            toggle.Margin = new Thickness(0, 0, 0, 12);
            stack.Children.Add(toggle);
            if (_showsNextLevels)
            {
                var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2, Margin = new Thickness(0, 0, -16, 0) };
                foreach (int level in new[] { Stats.Level + 1, Stats.Level + 2 }.Where(l => l <= LevelTargets.MaxLevel))
                {
                    var card = StatsUi.Card(Requirements(StatsText.Fill(Strings.LevelGoals, ("level", level.ToString())), LevelTargets.Requirements(level, Stats)));
                    card.Margin = new Thickness(0, 0, 16, 0);
                    grid.Children.Add(card);
                }
                stack.Children.Add(grid);
            }
            return stack;
        }
    }

    // ================================================================== Your Goals

    internal sealed class GoalsTab : StatsTabView
    {
        private List<string> _goals = new List<string>();
        private List<string> _saved = new List<string>();
        private bool _isSaving;
        private string _status;
        private readonly DispatcherTimer _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        private bool _loadedOnce;

        internal GoalsTab(StatsStrings strings) : base(strings)
        {
            _statusTimer.Tick += (s, e) => { _statusTimer.Stop(); _status = null; Render(); };
        }

        public override async void Load()
        {
            int generation = ++Generation;
            if (!_loadedOnce) ShowLoading();
            var goals = StatsWire.ReadGoals(await Api().GetLongTermGoals());
            if (generation != Generation) return;
            if (goals == null) { ShowError(); return; }
            _loadedOnce = true;
            _goals = goals.ToList();
            _saved = goals.ToList();
            Render();
        }

        private bool HasUnsavedChanges => !_goals.SequenceEqual(_saved);

        private void Render()
        {
            var stack = new StackPanel();
            if (_goals.Count == 0) stack.Children.Add(StatsUi.Text(Strings.NoLongTermGoals, 13, secondary: true));
            for (int i = 0; i < _goals.Count; i++)
            {
                int index = i;
                var number = StatsUi.Text((i + 1) + ".", 13, secondary: true, wrap: false);
                number.Width = 26;
                number.TextAlignment = TextAlignment.Right;
                number.VerticalAlignment = VerticalAlignment.Center;
                number.Margin = new Thickness(0, 0, 8, 0);
                var field = new TextBox { Text = _goals[i], Height = 32, FontSize = 13, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(6, 0, 6, 0), BorderBrush = Brushes.LightGray };
                field.SetResourceReference(Control.BackgroundProperty, "TextBoxBackgroundColor");
                field.SetResourceReference(Control.ForegroundProperty, "ForegroundColor");
                field.ToolTip = Strings.GoalPlaceholder;
                System.Windows.Automation.AutomationProperties.SetAutomationId(field, "txtGoal" + i);
                field.TextChanged += (s, e) => { _goals[index] = field.Text; UpdateSave(); };
                var remove = new Button { Content = "\U0001F5D1", Width = 30, Height = 30, Margin = new Thickness(8, 0, 0, 0), Cursor = System.Windows.Input.Cursors.Hand, ToolTip = Strings.Delete, Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
                remove.Click += (s, e) => { _goals.RemoveAt(index); Render(); };
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
                DockPanel.SetDock(number, Dock.Left);
                DockPanel.SetDock(remove, Dock.Right);
                row.Children.Add(number);
                row.Children.Add(remove);
                row.Children.Add(field);
                stack.Children.Add(row);
            }
            var footer = new DockPanel { Margin = new Thickness(0, 8, 0, 0), LastChildFill = false };
            var add = StatsUi.Button("+  " + Strings.AddGoal, (s, e) => { _goals.Add(""); Render(); });
            DockPanel.SetDock(add, Dock.Left);
            footer.Children.Add(add);
            _saveButton = StatsUi.Button(Strings.Save, (s, e) => Save(), primary: true);
            _saveButton.Margin = new Thickness(0);
            System.Windows.Automation.AutomationProperties.SetAutomationId(_saveButton, "btnSaveGoals");
            DockPanel.SetDock(_saveButton, Dock.Right);
            footer.Children.Add(_saveButton);
            if (_status != null)
            {
                var status = StatsUi.Text(_status, 12, secondary: true, wrap: false);
                status.VerticalAlignment = VerticalAlignment.Center;
                status.Margin = new Thickness(0, 0, 12, 0);
                DockPanel.SetDock(status, Dock.Right);
                footer.Children.Add(status);
            }
            stack.Children.Add(footer);
            Show(StatsUi.Card(stack, Strings.TabGoals));
            UpdateSave();
        }

        private Button _saveButton;

        private void UpdateSave()
        {
            if (_saveButton != null) _saveButton.IsEnabled = HasUnsavedChanges && !_isSaving;
        }

        private async void Save()
        {
            var cleaned = LongTermGoals.Cleaned(_goals);
            _isSaving = true;
            UpdateSave();
            bool success = await Api().UpdateLongTermGoals(cleaned);
            _isSaving = false;
            if (success)
            {
                _goals = cleaned.ToList();
                _saved = cleaned.ToList();
            }
            _status = success ? Strings.GoalsSaved : Strings.GoalsSaveFailed;
            _statusTimer.Stop();
            _statusTimer.Start();
            Render();
        }
    }

    // ================================================================== Pep Talk

    internal sealed class PepTalkTab : StatsTabView
    {
        private static readonly Random Random = new Random();

        private string _tone = PepTalkTones.All[0];
        private string _text = "";
        private bool _isStreaming;
        private PepTalkFailure _failure;
        private DateTime? _retryAvailableAt;
        private CancellationTokenSource _cancel;
        private readonly DispatcherTimer _retryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };

        internal PepTalkTab(StatsStrings strings) : base(strings)
        {
            _retryTimer.Tick += (s, e) =>
            {
                if (_retryAvailableAt == null || DateTime.Now >= _retryAvailableAt) { _retryTimer.Stop(); Render(); }
            };
        }

        /// <summary>
        /// A screen appearance shows a recent pep talk when there is one rather than generating another; a
        /// deliberate action (a tone, Retry) still streams. The daily budget is for what the user asked for.
        /// </summary>
        public override void Load()
        {
            NeedsReload = false;
            var reusable = PepTalkCache.Reusable(LanguageCode, DateTime.Now);
            if (reusable != null)
            {
                _tone = reusable.Value.Tone;
                _text = reusable.Value.Text;
                _failure = null;
                _isStreaming = false;
                Render();
                return;
            }
            Select(PepTalkTones.All[Random.Next(PepTalkTones.All.Length)]);
        }

        public override void Leave()
        {
            // Leaving mid-stream drops the request; with nothing shown yet, coming back has to ask again
            // (the loading bar used to stay up for good, as the screen loads a visited tab only once).
            if (_isStreaming && _text.Length == 0) NeedsReload = true;
            Cancel();
        }

        private void Cancel()
        {
            Generation++;
            if (_cancel != null) { _cancel.Cancel(); _cancel = null; }
            _isStreaming = false;
        }

        private async void Select(string tone)
        {
            Cancel();
            _tone = tone;
            _text = "";
            _failure = null;
            _isStreaming = true;
            int generation = Generation;
            var cancel = _cancel = new CancellationTokenSource();
            Render();
            var result = await Api().StreamPepTalk(tone, LanguageCode, chunk =>
            {
                if (generation != Generation) return;
                _text += chunk;
                RenderMessage();
            }, cancel.Token);
            if (generation != Generation) return;
            _isStreaming = false;
            if (result.Succeeded) PepTalkCache.Store(_text, _tone, LanguageCode, DateTime.Now);
            _failure = result.Succeeded ? null : result.Failure ?? PepTalkFailure.Other;
            // Hold Retry for as long as the server asked: pressing it during a rate limit cannot work.
            var delay = _failure?.RetryDelay;
            _retryAvailableAt = delay.HasValue ? DateTime.Now + delay.Value : (DateTime?)null;
            if (_retryAvailableAt != null) _retryTimer.Start();
            Render();
        }

        private Border _messageCard;

        private void Render()
        {
            var stack = new StackPanel();
            var tones = new WrapPanel();
            foreach (var tone in PepTalkTones.All)
            {
                string captured = tone;
                var pill = StatsUi.Pill(Strings.ToneName(tone), tone == _tone, (s, e) => Select(captured), "✨", enabled: !_isStreaming);
                System.Windows.Automation.AutomationProperties.SetAutomationId(pill, "tone" + tone.Replace(" ", ""));
                tones.Children.Add(pill);
            }
            var toneStack = new StackPanel();
            var heading = StatsUi.Text(Strings.MotivationalStyle, 13, FontWeights.SemiBold);
            heading.Margin = new Thickness(0, 0, 0, 10);
            toneStack.Children.Add(heading);
            toneStack.Children.Add(tones);
            var notice = StatsUi.Text(Strings.MotivationalNotice, 11, secondary: true);
            notice.Margin = new Thickness(0, 6, 0, 0);
            toneStack.Children.Add(notice);
            stack.Children.Add(StatsUi.Card(toneStack));
            _messageCard = StatsUi.Card(null);
            stack.Children.Add(_messageCard);
            RenderMessage();
            Show(stack);
        }

        private void RenderMessage()
        {
            if (_messageCard == null) return;
            var inner = (StackPanel)_messageCard.Child;
            inner.Children.Clear();
            if (_text.Length == 0 && _isStreaming)
            {
                inner.Children.Add(StatsUi.Loading());
            }
            else if (_failure != null && _text.Length == 0)
            {
                // Say which failure it was: "try again later" during a rate limit is advice the user cannot act on.
                inner.Children.Add(StatsUi.Text(_failure.IsRateLimited ? Strings.PepTalkRateLimited : Strings.PepTalkUnavailable, 13, secondary: true));
                var retry = StatsUi.Button(Strings.Retry, (s, e) => Select(_tone));
                retry.HorizontalAlignment = HorizontalAlignment.Left;
                retry.Margin = new Thickness(0, 10, 0, 0);
                retry.IsEnabled = _retryAvailableAt == null || DateTime.Now >= _retryAvailableAt;
                inner.Children.Add(retry);
            }
            else
            {
                inner.Children.Add(MarkdownView.Build(MarkdownParser.Parse(_text), 15));
            }
        }
    }
}
