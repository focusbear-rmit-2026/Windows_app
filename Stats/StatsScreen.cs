using Focus_Bear.Models;
using Focus_Bear.Utils;
using FocusBear.Core.Stats;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Focus_Bear.StatsScreens
{
    /// <summary>
    /// The native Stats screen, replacing the dashboard.focusbear.io/webview/stats webview: a title, the tab
    /// strip, and the selected tab. Port of the Mac's <c>StatsScreen</c> / <c>StatsStore</c>: a tab loads the
    /// first time it is visited, every appearance of the screen reloads (the webview reloaded on each open),
    /// and the last tab is remembered.
    /// </summary>
    public sealed class StatsScreen : UserControl
    {
        private readonly StatsStrings _strings = StatsStrings.Load();
        private readonly Dictionary<StatsTab, StatsTabView> _tabs = new Dictionary<StatsTab, StatsTabView>();
        private readonly HashSet<StatsTab> _loaded = new HashSet<StatsTab>();
        private readonly WrapPanel _tabStrip = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        private readonly ContentControl _content = new ContentControl();
        private readonly ScrollViewer _scroll;
        private readonly bool _remembersTab;
        private StatsTab _selected;

        /// <summary><paramref name="initialTab"/> fixes the opening tab (and turns off remembering it).</summary>
        public StatsScreen(StatsTab? initialTab = null)
        {
            _remembersTab = initialTab == null;
            _selected = initialTab ?? StatsTabs.Parse(RegistryManager.GetStatsSelectedTab());
            SetResourceReference(BackgroundProperty, "BackgroundColor");
            Resources[typeof(Grid)] = new Style(typeof(Grid));   // the app's page-background Grid style would show inside the cards

            var title = StatsUi.Text(_strings.Title, 20, FontWeights.SemiBold, wrap: false);
            title.VerticalAlignment = VerticalAlignment.Center;
            title.Margin = new Thickness(0, 0, 18, 6);
            var header = new DockPanel { Margin = new Thickness(24, 16, 24, 10), LastChildFill = true };
            DockPanel.SetDock(title, Dock.Left);
            header.Children.Add(title);
            header.Children.Add(_tabStrip);

            var divider = new Border { Height = 1, Margin = new Thickness(0) };
            divider.SetResourceReference(Border.BackgroundProperty, "TrayCardBorder");

            _scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = new Border { Padding = new Thickness(24, 20, 24, 20), Child = _content } };

            var root = new DockPanel();
            DockPanel.SetDock(header, Dock.Top);
            DockPanel.SetDock(divider, Dock.Top);
            root.Children.Add(header);
            root.Children.Add(divider);
            root.Children.Add(_scroll);
            Content = root;

            BuildTabStrip();
            IsVisibleChanged += (s, e) =>
            {
                if ((bool)e.NewValue) ScreenAppeared();
                else foreach (var tab in _tabs.Values) tab.Leave();
            };
        }

        public StatsTab SelectedTab => _selected;

        /// <summary>Reload every tab the next time it is shown, starting with the current one.</summary>
        public void ScreenAppeared()
        {
            _loaded.Clear();
            Show(_selected);
        }

        public void Select(StatsTab tab)
        {
            if (_selected != tab && _tabs.TryGetValue(_selected, out var previous)) previous.Leave();
            _selected = tab;
            if (_remembersTab) RegistryManager.SetStatsSelectedTab(tab.RawValue());
            BuildTabStrip();
            Show(tab);
        }

        private static string Icon(StatsTab tab)
        {
            switch (tab)
            {
                case StatsTab.Goals: return "\U0001F3AF";
                case StatsTab.Progress: return "\U0001F4C8";
                case StatsTab.Streak: return "⚡";
                case StatsTab.PepTalk: return "✨";
                case StatsTab.Leaderboard: return "\U0001F465";
                case StatsTab.HabitStats: return "\U0001F3C5";
                case StatsTab.Productivity: return "\U0001F4CA";
                default: return "\U0001F4CB";
            }
        }

        private void BuildTabStrip()
        {
            _tabStrip.Children.Clear();
            foreach (var tab in StatsTabs.All)
            {
                var captured = tab;
                var pill = StatsUi.Pill(_strings.TabTitle(tab), tab == _selected, (s, e) => Select(captured), Icon(tab));
                System.Windows.Automation.AutomationProperties.SetAutomationId(pill, "statsTab" + tab);
                _tabStrip.Children.Add(pill);
            }
        }

        private void Show(StatsTab tab)
        {
            if (!_tabs.TryGetValue(tab, out var view))
            {
                view = Create(tab);
                _tabs[tab] = view;
            }
            _content.Content = view;
            _scroll.ScrollToTop();
            if (_loaded.Add(tab) || view.NeedsReload) view.Load();
        }

        private StatsTabView Create(StatsTab tab)
        {
            switch (tab)
            {
                case StatsTab.Goals: return new GoalsTab(_strings);
                case StatsTab.Progress: return new ProgressTab(_strings);
                case StatsTab.PepTalk: return new PepTalkTab(_strings);
                case StatsTab.Leaderboard: return new LeaderboardTab(_strings);
                case StatsTab.HabitStats: return new HabitStatsTab(_strings);
                case StatsTab.Productivity: return new ProductivityTab(_strings);
                case StatsTab.FocusLog: return new FocusLogTab(_strings);
                default: return new StreakTab(_strings);
            }
        }
    }

    /// <summary>One tab. It builds its own content and owns its loading state.</summary>
    internal abstract class StatsTabView : UserControl
    {
        protected readonly StatsStrings Strings;
        /// <summary>Bumped by every load, so a slow response for an earlier one is dropped.</summary>
        protected int Generation;

        protected StatsTabView(StatsStrings strings)
        {
            Strings = strings;
        }

        public abstract void Load();

        /// <summary>The tab was left before it had anything to show, so its next appearance loads again.</summary>
        internal bool NeedsReload { get; set; }

        /// <summary>The screen is going away (or another tab is selected).</summary>
        public virtual void Leave() { }

        protected void Show(UIElement element) { Content = element; }

        protected void ShowLoading() { Content = StatsUi.Loading(); }

        protected void ShowError() { Content = StatsUi.Error(Strings, Load); }

        protected static API.FocusBearAPIClient Api() => new API.FocusBearAPIClient();

        protected static string LanguageCode
        {
            get
            {
                string code = AppSettings.GetInstance.sLangCode;
                return string.IsNullOrEmpty(code) ? "en" : code;
            }
        }

        protected static string Timezone => TimeZoneInfoToIana.Current();
    }

    /// <summary>Renders <see cref="MarkdownParser"/> output: paragraphs, headings, bullets, numbered lines, links.</summary>
    internal static class MarkdownView
    {
        internal static StackPanel Build(IList<MarkdownBlock> blocks, double fontSize)
        {
            var stack = new StackPanel();
            foreach (var block in blocks)
            {
                var text = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = fontSize, LineHeight = fontSize * 1.45, Margin = new Thickness(0, 0, 0, 10) };
                text.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
                if (block.Kind == MarkdownBlockKind.Heading)
                {
                    text.FontSize = fontSize * (block.HeadingLevel == 1 ? 1.45 : block.HeadingLevel == 2 ? 1.25 : 1.1);
                    text.FontWeight = FontWeights.Bold;
                }
                foreach (var run in block.Runs)
                {
                    // The model often ends a sentence with two or three spaces; outside code they read as gaps.
                    string runText = run.IsCode ? run.Text : System.Text.RegularExpressions.Regex.Replace(run.Text, " {2,}", " ");
                    Inline inline = new Run(runText)
                    {
                        FontWeight = run.IsBold ? FontWeights.Bold : text.FontWeight,
                        FontStyle = run.IsItalic ? FontStyles.Italic : FontStyles.Normal,
                    };
                    if (run.IsCode) inline.FontFamily = new FontFamily("Consolas");
                    string url = FocusBear.Core.Notes.NoteLinks.OpenableUrl(run.LinkUrl);
                    if (url != null)
                    {
                        var link = new Hyperlink(inline) { Foreground = StatsUi.Info };
                        link.Click += (s, e) => StatsUi.OpenExternal(url);
                        inline = link;
                    }
                    text.Inlines.Add(inline);
                }
                if (block.Kind == MarkdownBlockKind.Bullet || block.Kind == MarkdownBlockKind.Numbered)
                {
                    var row = new DockPanel { Margin = text.Margin };
                    text.Margin = new Thickness(0);
                    var marker = StatsUi.Text(block.Kind == MarkdownBlockKind.Bullet ? "•" : block.Label, fontSize);
                    marker.Width = 26;
                    DockPanel.SetDock(marker, Dock.Left);
                    row.Children.Add(marker);
                    row.Children.Add(text);
                    stack.Children.Add(row);
                }
                else
                {
                    stack.Children.Add(text);
                }
            }
            return stack;
        }
    }

    /// <summary>
    /// The IANA zone name the stats endpoints expect ("Australia/Sydney"). Windows zone ids ("AUS Eastern
    /// Standard Time") are not accepted by the server, so the common ones are mapped; anything else falls back
    /// to a fixed-offset "Etc/GMT" zone, which keeps the day boundaries right even if the name is generic.
    /// </summary>
    internal static class TimeZoneInfoToIana
    {
        private static readonly Dictionary<string, string> Map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "AUS Eastern Standard Time", "Australia/Sydney" }, { "E. Australia Standard Time", "Australia/Brisbane" },
            { "Cen. Australia Standard Time", "Australia/Adelaide" }, { "AUS Central Standard Time", "Australia/Darwin" },
            { "W. Australia Standard Time", "Australia/Perth" }, { "Tasmania Standard Time", "Australia/Hobart" },
            { "New Zealand Standard Time", "Pacific/Auckland" }, { "GMT Standard Time", "Europe/London" },
            { "W. Europe Standard Time", "Europe/Berlin" }, { "Romance Standard Time", "Europe/Paris" },
            { "Central European Standard Time", "Europe/Warsaw" }, { "Central Europe Standard Time", "Europe/Budapest" },
            { "Eastern Standard Time", "America/New_York" }, { "Central Standard Time", "America/Chicago" },
            { "Mountain Standard Time", "America/Denver" }, { "Pacific Standard Time", "America/Los_Angeles" },
            { "US Mountain Standard Time", "America/Phoenix" }, { "Alaskan Standard Time", "America/Anchorage" },
            { "Hawaiian Standard Time", "Pacific/Honolulu" }, { "India Standard Time", "Asia/Kolkata" },
            { "Singapore Standard Time", "Asia/Singapore" }, { "China Standard Time", "Asia/Shanghai" },
            { "Tokyo Standard Time", "Asia/Tokyo" }, { "Korea Standard Time", "Asia/Seoul" },
            { "SA Pacific Standard Time", "America/Bogota" }, { "E. South America Standard Time", "America/Sao_Paulo" },
            { "Argentina Standard Time", "America/Argentina/Buenos_Aires" }, { "Mexico Standard Time", "America/Mexico_City" },
            { "Central America Standard Time", "America/Guatemala" }, { "Pacific SA Standard Time", "America/Santiago" },
            { "South Africa Standard Time", "Africa/Johannesburg" }, { "Arabian Standard Time", "Asia/Dubai" },
            { "Israel Standard Time", "Asia/Jerusalem" }, { "Turkey Standard Time", "Europe/Istanbul" },
            { "Russian Standard Time", "Europe/Moscow" }, { "UTC", "UTC" }, { "Coordinated Universal Time", "UTC" },
            // Zones whose clocks change, or whose offset is not whole hours, need their real name to bucket days right.
            { "Iran Standard Time", "Asia/Tehran" }, { "Nepal Standard Time", "Asia/Kathmandu" },
            { "Newfoundland Standard Time", "America/St_Johns" }, { "Sri Lanka Standard Time", "Asia/Colombo" },
            { "Afghanistan Standard Time", "Asia/Kabul" }, { "Myanmar Standard Time", "Asia/Yangon" },
            { "Atlantic Standard Time", "America/Halifax" }, { "Canada Central Standard Time", "America/Regina" },
            { "FLE Standard Time", "Europe/Kiev" }, { "GTB Standard Time", "Europe/Bucharest" },
            { "E. Europe Standard Time", "Europe/Chisinau" }, { "Egypt Standard Time", "Africa/Cairo" },
            { "W. Central Africa Standard Time", "Africa/Lagos" }, { "E. Africa Standard Time", "Africa/Nairobi" },
            { "SE Asia Standard Time", "Asia/Bangkok" }, { "Taipei Standard Time", "Asia/Taipei" },
            { "North Asia East Standard Time", "Asia/Irkutsk" }, { "Pakistan Standard Time", "Asia/Karachi" },
            { "Bangladesh Standard Time", "Asia/Dhaka" }, { "West Asia Standard Time", "Asia/Tashkent" },
            { "Arab Standard Time", "Asia/Riyadh" }, { "Morocco Standard Time", "Africa/Casablanca" },
            { "Greenwich Standard Time", "Atlantic/Reykjavik" }, { "Mountain Standard Time (Mexico)", "America/Chihuahua" },
            { "Central Standard Time (Mexico)", "America/Mexico_City" }, { "Venezuela Standard Time", "America/Caracas" },
            { "Montevideo Standard Time", "America/Montevideo" }, { "Fiji Standard Time", "Pacific/Fiji" },
            { "Tonga Standard Time", "Pacific/Tongatapu" },
        };

        internal static string Current()
        {
            var zone = TimeZoneInfo.Local;
            if (Map.TryGetValue(zone.Id, out string iana)) return iana;
            // An unlisted zone by its offset; a half-hour one used to fall back to plain UTC.
            return FocusBear.Core.Stats.StatsTimeZone.ForOffset(zone.GetUtcOffset(DateTime.Now));
        }
    }
}
