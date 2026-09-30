using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Pomodoro;
using Focus_Bear.Utils;
using FocusBear.Core.Blocking;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Focus_Bear.Preferences
{
    /// <summary>
    /// The block-list screen, rebuilt natively after the Mac's SwiftUI <c>BlockListView</c> (Figma
    /// "DESKTOP BLOCKLIST"): a rail of block-list cards on the left and, on the right, the selected list's
    /// name, a search box, category chips, one-tap suggestions, the entry table and an add row.
    ///
    /// It edits exactly what the old <see cref="FocusPage"/> edited, in the same way: the in-memory
    /// <see cref="FocusMode"/> objects in <c>AppSettings.arrFocusModes</c> (and, for the default mode, the
    /// app-wide Always Blocked URLs), with create / duplicate / delete going to the API straight away and
    /// every other edit synced by <c>UpdateAllFocusModes</c> when the screen is hidden. Categories are
    /// inferred for display only — nothing new is stored.
    /// </summary>
    public class BlockListsView : UserControl
    {
        private readonly AppSettings _settings = AppSettings.GetInstance;
        private readonly FocusBearMainWindow _parentWindow;

        private FocusMode _selected;
        private DistractionCategory? _activeCategory;
        private string _search = "";
        private bool _editingName;
        private BlockListMetrics _metrics = BlockListLayout.Metrics(1100, 0);

        private FocusStartPalette _p;
        private readonly Grid _root = new Grid();
        private readonly ColumnDefinition _railColumn = new ColumnDefinition();
        private readonly ContentControl _railHost = new ContentControl { Focusable = false };
        private readonly ContentControl _panelHost = new ContentControl { Focusable = false };
        // Panel parts that change without the whole panel being rebuilt (so the search box keeps focus).
        private StackPanel _chipsRow;
        private ContentControl _suggestionsHost;
        private ContentControl _tableHost;
        private TextBlock _titleText;
        private ContentControl _headerHost;

        private static readonly string[] SwatchColours =
        {
            "#A3FFA1", "#FFB3A1", "#FFD6A1", "#FFF4A1", "#A1E3FF", "#A1B8FF",
            "#D3A1FF", "#FFA1D8", "#D1D1D6", "#9747FF", "#DE7400", "#D93A2B",
        };

        public BlockListsView(FocusBearMainWindow parentWindow)
        {
            _parentWindow = parentWindow;
            AutomationProperties.SetAutomationId(this, "BlockListsView");
            ApplyAppFont();
            SetResourceReference(BackgroundProperty, "BackgroundColor");
            // The Preferences host (BlocksTab) carries implicit styles that paint every Grid with the page
            // background and restyle buttons and text; this screen draws its own surfaces, so block them.
            foreach (Type type in new[] { typeof(Grid), typeof(TextBlock), typeof(Button), typeof(TextBox), typeof(StackPanel) })
                Resources[type] = new Style(type);

            _root.ColumnDefinitions.Add(_railColumn);
            _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(BlockListLayout.RailPanelGap) });
            _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(_panelHost, 2);
            _root.Children.Add(_railHost);
            _root.Children.Add(_panelHost);
            Content = _root;

            SizeChanged += (s, e) => OnWidthChanged(e.NewSize.Width);
            IsVisibleChanged += OnIsVisibleChanged;
            Loaded += (s, e) => { App.ThemeChanged -= OnThemeChanged; App.ThemeChanged += OnThemeChanged; TitleBarThemeChange(); };
            Unloaded += (s, e) => App.ThemeChanged -= OnThemeChanged;

            Reload();
        }

        // MARK: - Public surface (what BlocksTab calls)

        /// <summary>Re-reads the block lists and redraws. Keeps the selection when it still exists.</summary>
        public void Reload()
        {
            _p = FocusStartPalette.Current();
            var visible = VisibleModes();
            if (_selected == null || !visible.Contains(_selected)) _selected = visible.FirstOrDefault();
            _railColumn.Width = new GridLength(_metrics.RailWidth);
            BuildRail();
            BuildPanel();
        }

        /// <summary>Selects the block list with this name (case-insensitive), as the old page did.</summary>
        public void SelectFocusMode(string name)
        {
            var match = _settings.arrFocusModes.FirstOrDefault(m => string.Equals(m.name, name, StringComparison.OrdinalIgnoreCase));
            if (match != null && BlockListRules.ShowsInList(Flags(match))) Select(match);
        }

        // MARK: - Data

        private List<FocusMode> VisibleModes() =>
            (_settings.arrFocusModes ?? new ObservableCollection<FocusMode>()).Where(m => m != null && BlockListRules.ShowsInList(Flags(m))).ToList();

        private static BlockListModeFlags Flags(FocusMode m) => new BlockListModeFlags
        {
            IsDefault = m.isDefault,
            IsMeetings = m.isMeetings,
            IsLockedFocus = m.isLockedFocus,
            IsRelaxFocus = m.isRelaxFocus,
            IsDeepWorkFocus = m.isDeepWorkFocus,
            IsAllowedListApproach = m.isAllowedListApproach,
            IsAllowedAllApps = m.isAllowedAllApps,
        };

        /// <summary>The website list this mode edits: the Always Blocked URLs for the default mode, else its allow or block list.</summary>
        private ObservableCollection<string> WebsiteList(FocusMode m)
        {
            if (m.isRelaxFocus) return null;
            if (m.isDefault) return _settings.arrAlwaysBlockedURLs;
            return m.isAllowedListApproach ? m.arrAllowedUrls : m.arrBlockedUrls;
        }

        private IReadOnlyList<BlockListEntry> EntriesFor(FocusMode m)
        {
            var flags = Flags(m);
            var websites = WebsiteList(m) ?? new ObservableCollection<string>();
            IEnumerable<string> apps = BlockListRules.ShowsApps(flags)
                ? m.arrAllowedAppsWin.Where(a => a != null).Select(a => a.AppName ?? "")
                : Enumerable.Empty<string>();
            return BlockListEntries.Build(websites.ToList(), BlockListRules.WebsitesAreAllowed(flags), apps.ToList(), BlockListRules.AppsAreAllowed(flags));
        }

        // MARK: - Text

        private string T(string key, string fallback)
        {
            string value = _settings.GetConfigStringSafe(fallback, "blockListViewInfo", key);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private string Cfg(string block, string key, string fallback)
        {
            string value = _settings.GetConfigStringSafe(fallback, block, key);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private string CategoryName(DistractionCategory category)
        {
            switch (category)
            {
                case DistractionCategory.SocialMedia: return Cfg("onboardingBlockedUrlsInfo", "categorySocialMedia", "Social Media");
                case DistractionCategory.News: return Cfg("onboardingBlockedUrlsInfo", "categoryNews", "News");
                case DistractionCategory.Video: return Cfg("onboardingBlockedUrlsInfo", "categoryStreamingServices", "Streaming services");
                case DistractionCategory.Dating: return Cfg("onboardingBlockedUrlsInfo", "categoryDating", "Dating");
                case DistractionCategory.RealEstate: return Cfg("onboardingBlockedUrlsInfo", "categoryRealEstate", "Real estate");
                case DistractionCategory.Gaming: return T("categoryGaming", "Gaming");
                case DistractionCategory.Porn: return T("categoryPorn", "Adult");
                case DistractionCategory.Gambling: return T("categoryGambling", "Gambling");
                default: return Cfg("onboardingBlockedUrlsInfo", "categoryOthers", "Others");
            }
        }

        private string NoticeText(BlockListNotice notice)
        {
            switch (notice)
            {
                case BlockListNotice.AllWebsitesAllowed: return Cfg("WindowsSpecific", "allWebsitesAllowed", "All websites are allowed");
                case BlockListNotice.AlwaysBlocked:
                    return T("alwaysBlockedNotice", "When you use this block list, all websites are allowed except your Always Blocked URLs (below), and the apps listed below are blocked.");
                case BlockListNotice.AllowListEverything:
                    return T("allowListNotice", "This block list uses an allow list: everything is blocked except the sites and apps below.");
                case BlockListNotice.AllowListWebsites:
                    return T("allowListWebsitesNotice", "This block list uses an allow list for websites: every website is blocked except the sites below.");
                case BlockListNotice.AllowListApps:
                    return T("allowListAppsNotice", "Only the apps below are allowed while this block list is on.");
                default: return null;
            }
        }

        // MARK: - Layout

        private void OnWidthChanged(double width)
        {
            var metrics = BlockListLayout.Metrics(width, 0);
            bool changed = Math.Abs(metrics.RailWidth - _metrics.RailWidth) > 0.5
                           || Math.Abs(metrics.Columns.Name - _metrics.Columns.Name) > 0.5
                           || Math.Abs(metrics.TitleFontSize - _metrics.TitleFontSize) > 0.1;
            _metrics = metrics;
            if (!changed) return;
            _railColumn.Width = new GridLength(metrics.RailWidth);
            if (_titleText != null) { _titleText.FontSize = metrics.TitleFontSize; _titleText.MaxWidth = TitleMaxWidth(); }
            RefreshEntries();
        }

        /// <summary>The panel's inner width less the pencil, colour swatch and edit button beside the title.</summary>
        private double TitleMaxWidth() => Math.Max(80, _metrics.PanelWidth - BlockListLayout.PanelInset * 2 - 110);

        // MARK: - Left rail

        private void BuildRail()
        {
            var modes = VisibleModes();
            var rail = new Grid();
            rail.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rail.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rail.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = Text(T("myBlockLists", "MY BLOCKLISTS") + " (" + modes.Count + ")", 13, _p.Secondary, FontWeights.SemiBold, false);
            label.Margin = new Thickness(0, 0, 0, 12);
            AutomationProperties.SetAutomationId(label, "txtMyBlockLists");
            rail.Children.Add(label);

            var cards = new StackPanel();
            for (int i = 0; i < modes.Count; i++)
            {
                var card = Card(modes[i], i);
                card.Margin = new Thickness(0, 0, 2, 8);
                cards.Children.Add(card);
            }
            var scroll = new ScrollViewer { Content = cards, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };
            Grid.SetRow(scroll, 1);
            rail.Children.Add(scroll);

            var createLabel = Text(T("createNewBlockList", "Create New Block"), 15, Brushes.White, FontWeights.Medium, false);
            createLabel.HorizontalAlignment = HorizontalAlignment.Center;
            createLabel.VerticalAlignment = VerticalAlignment.Center;
            var createFace = new Border { Background = _p.Accent, CornerRadius = new CornerRadius(10), Height = 40, Child = createLabel, BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xF9, 0xF2)), BorderThickness = new Thickness(1) };
            var create = FocusStartUi.Clickable(createFace, CreateBlockList, "btnAddFocusMode");
            create.Margin = new Thickness(0, 12, 0, 0);
            Grid.SetRow(create, 2);
            rail.Children.Add(create);

            _railHost.Content = rail;
        }

        private FrameworkElement Card(FocusMode mode, int index)
        {
            bool selected = mode == _selected;
            var flags = Flags(mode);
            var dark = Solid(0x10, 0x10, 0x14);

            var content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            string emoji = BlockListCategories.LeadingEmoji(mode.name);
            string title = mode.name ?? "";
            FrameworkElement leading;
            if (emoji != null)
            {
                title = title.Trim().Substring(emoji.Length).Trim();
                leading = new Emoji.Wpf.TextBlock { Text = emoji, FontSize = 20, VerticalAlignment = VerticalAlignment.Center };
            }
            else
            {
                leading = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(4), Background = ColourBrush(mode.color), VerticalAlignment = VerticalAlignment.Center };
            }
            leading.Margin = new Thickness(0, 0, 8, 0);
            content.Children.Add(leading);

            var name = Text(title, 15, selected ? dark : _p.Primary, FontWeights.SemiBold);
            name.MaxHeight = 15 * 1.4 * 3;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(name, 1);
            content.Children.Add(name);

            int count = EntriesFor(mode).Count;
            var badgeText = Text(BlockListEntries.BlocksBadge(count, T("blocksCount", "<COUNT> blocks"), T("oneBlockCount", "1 block")).ToUpperInvariant(),
                                 11, selected ? _p.Selection : Solid(0xDE, 0x82, 0x00), FontWeights.SemiBold, false);
            var badge = new Border
            {
                Child = badgeText,
                Padding = new Thickness(8, 4, 8, 4),
                CornerRadius = new CornerRadius(12),
                Background = selected ? (Brush)_p.Accent : new SolidColorBrush(Color.FromArgb(0x33, 0xE4, 0x8E, 0x24)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
            };
            Grid.SetColumn(badge, 2);
            content.Children.Add(badge);

            var stack = new StackPanel();
            stack.Children.Add(content);
            if (selected)
            {
                var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
                if (BlockListRules.CanDelete(flags))
                    actions.Children.Add(CardAction(0xE74D, T("deleteBlockList", "Delete"), () => DeleteBlockList(mode), "btnDeleteFocusMode"));
                if (BlockListRules.CanDuplicate(flags))
                    actions.Children.Add(CardAction(0xE8C8, T("duplicateBlockList", "Duplicate"), () => DuplicateBlockList(mode), "btnDuplicateFocusMode"));
                if (actions.Children.Count > 0) stack.Children.Add(actions);
            }

            var face = new Border
            {
                Child = stack,
                Padding = new Thickness(12, 16, 12, 16),
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(2),
                BorderBrush = selected ? _p.Accent : _p.Border,
                Background = selected ? _p.Selection : _p.Surface,
            };
            var button = FocusStartUi.Clickable(face, () => Select(mode), "blockListCard_" + index);
            AutomationProperties.SetName(button, mode.name ?? "");
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            // A block list selected from elsewhere (just created, or picked by name) scrolls into view.
            if (selected) button.Loaded += (s, e) => button.BringIntoView();
            return button;
        }

        private Button CardAction(int glyph, string tooltip, Action onClick, string automationId)
        {
            var face = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0xDE, 0x74, 0x00)),
                Background = Brushes.Transparent,
                Child = Centered(FocusStartUi.Glyph(glyph, 13, Solid(0x10, 0x10, 0x14))),
            };
            var button = FocusStartUi.Clickable(face, onClick, automationId, tooltip);
            button.Margin = new Thickness(8, 0, 0, 0);
            return button;
        }

        // MARK: - Right panel

        private void BuildPanel()
        {
            _chipsRow = null;
            _suggestionsHost = null;
            _tableHost = null;
            _titleText = null;

            var panel = new Border
            {
                BorderBrush = _p.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Background = _p.Surface,
                Padding = new Thickness(BlockListLayout.PanelInset),
                ClipToBounds = true,
            };

            if (_selected == null)
            {
                var empty = Text(T("noBlockListSelected", "Select a block list on the left, or create a new one."), 15, _p.Secondary);
                empty.HorizontalAlignment = HorizontalAlignment.Center;
                empty.VerticalAlignment = VerticalAlignment.Center;
                panel.Child = empty;
                _panelHost.Content = panel;
                return;
            }

            var mode = _selected;
            var flags = Flags(mode);
            var grid = new Grid();
            int row = 0;
            Action<UIElement, bool> add = (element, fill) =>
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = fill ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
                Grid.SetRow(element, row++);
                grid.Children.Add(element);
            };

            _headerHost = new ContentControl { Focusable = false, Content = Header(mode, flags) };
            add(_headerHost, false);
            if (BlockListRules.ShowsApproachChoices(flags)) add(ApproachRow(mode), false);

            if (!BlockListRules.CanEditEntries(flags))
            {
                // A relax mode blocks nothing: there is nothing to search, filter or list.
                var relax = Text(NoticeText(BlockListNotice.AllWebsitesAllowed), 15, _p.Secondary);
                relax.Margin = new Thickness(0, 20, 0, 0);
                AutomationProperties.SetAutomationId(relax, "txtBlockListNotice");
                add(relax, false);
                add(new Border(), true);
                panel.Child = grid;
                _panelHost.Content = panel;
                return;
            }

            add(SearchBox(), false);

            _chipsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 48, 2) };
            var chipsScroll = new ScrollViewer
            {
                Content = _chipsRow,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Focusable = false,
                Margin = new Thickness(0, 14, 0, 0),
            };
            // The chips scroll sideways; the fade tells the user the row carries on past the edge.
            var fade = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            fade.GradientStops.Add(new GradientStop(Colors.Black, 0));
            fade.GradientStops.Add(new GradientStop(Colors.Black, 0.92));
            fade.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            chipsScroll.OpacityMask = fade;
            chipsScroll.PreviewMouseWheel += (s, e) => { chipsScroll.ScrollToHorizontalOffset(chipsScroll.HorizontalOffset - e.Delta / 3.0); e.Handled = true; };
            add(chipsScroll, false);

            add(Divider(new Thickness(0, 14, 0, 0)), false);

            string notice = NoticeText(BlockListRules.Notice(flags));
            if (notice != null)
            {
                var noticeText = Text(notice, 12, _p.Secondary);
                noticeText.Margin = new Thickness(0, 14, 0, 0);
                AutomationProperties.SetAutomationId(noticeText, "txtBlockListNotice");
                add(noticeText, false);
            }

            _suggestionsHost = new ContentControl { Focusable = false };
            add(_suggestionsHost, false);

            _tableHost = new ContentControl { Focusable = false, Margin = new Thickness(0, 14, 0, 0), VerticalContentAlignment = VerticalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            add(_tableHost, true);

            if (BlockListRules.CanEditEntries(flags))
            {
                add(Divider(new Thickness(0, 14, 0, 0)), false);
                add(AddRow(mode, flags), false);
            }

            panel.Child = grid;
            _panelHost.Content = panel;
            RefreshEntries();
        }

        private FrameworkElement Header(FocusMode mode, BlockListModeFlags flags)
        {
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            bool canRename = BlockListRules.CanRename(flags);
            if (_editingName && canRename)
            {
                var box = new TextBox
                {
                    Text = mode.name,
                    FontSize = _metrics.TitleFontSize,
                    FontWeight = FontWeights.SemiBold,
                    MaxLength = BlockListRules.MaxRenameLength,
                    Padding = new Thickness(6, 2, 6, 2),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Background = _p.Field,
                    Foreground = _p.Primary,
                    CaretBrush = _p.Primary,
                    BorderBrush = _p.Accent,
                    BorderThickness = new Thickness(2),
                };
                AutomationProperties.SetAutomationId(box, "txtBoxFocusName");
                bool done = false;
                Action<bool> finish = commit =>
                {
                    if (done) return;
                    done = true;
                    _editingName = false;
                    if (commit) Rename(mode, box.Text);
                    // Only the header and rail change: rebuilding the whole panel would throw away the
                    // search box the user may just have clicked into (which is what ended the edit).
                    BuildRail();
                    if (_selected == mode && _headerHost != null) _headerHost.Content = Header(mode, Flags(mode));
                };
                box.KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter) { e.Handled = true; finish(true); }
                    else if (e.Key == Key.Escape) { e.Handled = true; finish(false); }
                };
                box.LostKeyboardFocus += (s, e) => finish(true);
                box.Loaded += (s, e) => { box.Focus(); box.SelectAll(); };
                header.Children.Add(box);
            }
            else
            {
                var titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                // Emoji-aware (a block list's name may start with one), and capped so a long name ends in
                // an ellipsis before the colour and edit buttons rather than running under them.
                _titleText = new Emoji.Wpf.TextBlock
                {
                    Text = mode.name ?? "",
                    FontSize = _metrics.TitleFontSize,
                    Foreground = _p.Primary,
                    FontWeight = FontWeights.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = TitleMaxWidth(),
                };
                AutomationProperties.SetAutomationId(_titleText, "txtBlockListTitle");
                titleRow.Children.Add(_titleText);
                if (canRename)
                {
                    var pencil = FocusStartUi.Glyph(0xE70F, 15, _p.Secondary);
                    pencil.Margin = new Thickness(10, 2, 0, 0);
                    titleRow.Children.Add(pencil);
                    var rename = FocusStartUi.Clickable(titleRow, () => { _editingName = true; _headerHost.Content = Header(mode, flags); }, "btnRenameBlockList", T("renameBlockList", "Rename this block list"));
                    rename.HorizontalAlignment = HorizontalAlignment.Left;
                    header.Children.Add(rename);
                }
                else header.Children.Add(titleRow);
            }

            // The colour swatch: click to recolour (the menu-bar / list colour of this block list).
            var swatch = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(4),
                Background = ColourBrush(mode.color),
                BorderBrush = _p.Border,
                BorderThickness = new Thickness(1),
            };
            Popup popup = null;
            var swatchButton = FocusStartUi.Clickable(swatch, () => { if (popup != null) popup.IsOpen = true; }, "btnBlockListColour", T("changeColor", "Change colour"));
            swatchButton.Margin = new Thickness(12, 0, 0, 0);
            swatchButton.VerticalAlignment = VerticalAlignment.Center;
            popup = ColourPopup(mode, swatchButton);
            Grid.SetColumn(swatchButton, 1);
            header.Children.Add(swatchButton);

            // The design's edit affordance: the Advanced sheet (URLs blocked inside an allow list).
            var editGlyph = FocusStartUi.Glyph(0xE713, 17, _p.Primary);
            var edit = FocusStartUi.Clickable(Centered(editGlyph, 28), () => OpenAdvanced(mode), "btnAdvanced",
                                              Cfg("focusPreferencesInfo", "buttonAdvancedText", "Advanced") + " — " + T("editBlockList", "Edit this block list"));
            edit.Margin = new Thickness(8, 0, 0, 0);
            edit.VerticalAlignment = VerticalAlignment.Center;
            edit.IsEnabled = BlockListRules.AdvancedAvailable(flags);
            edit.Opacity = edit.IsEnabled ? 1 : 0.35;
            Grid.SetColumn(edit, 2);
            header.Children.Add(edit);
            return header;
        }

        private Popup ColourPopup(FocusMode mode, UIElement target)
        {
            var popup = new Popup { PlacementTarget = target, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, VerticalOffset = 6 };
            var grid = new UniformGrid { Columns = 6 };
            foreach (string colour in SwatchColours)
            {
                string chosen = colour;
                bool current = string.Equals(mode.color, colour, StringComparison.OrdinalIgnoreCase);
                var chip = new Border
                {
                    Width = 22,
                    Height = 22,
                    Margin = new Thickness(4),
                    CornerRadius = new CornerRadius(4),
                    Background = ColourBrush(colour),
                    BorderBrush = current ? _p.Accent : _p.Border,
                    BorderThickness = new Thickness(current ? 2 : 1),
                };
                grid.Children.Add(FocusStartUi.Clickable(chip, () => { popup.IsOpen = false; SetColour(mode, chosen); }, "swatch_" + chosen.TrimStart('#')));
            }
            popup.Child = new Border
            {
                Child = grid,
                Background = _p.Surface,
                BorderBrush = _p.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 12, ShadowDepth = 0, Opacity = 0.18 },
            };
            return popup;
        }

        /// <summary>
        /// The two approach choices the old editor offered as radio buttons: whether websites use a block
        /// list or an allow list, and whether every app or only the listed apps are allowed.
        /// </summary>
        private FrameworkElement ApproachRow(FocusMode mode)
        {
            var wrap = new WrapPanel { Margin = new Thickness(0, 14, 0, 0) };
            string blockList = Cfg("focusPreferencesInfo", "titleBlockUrlList", "Block List").TrimEnd(':', ' ');
            string allowList = Cfg("focusPreferencesInfo", "titleAllowUrlList", "Allow List").TrimEnd(':', ' ');
            wrap.Children.Add(Segmented(T("websitesLabel", "Websites"),
                new[] { blockList, allowList }, mode.isAllowedListApproach ? 1 : 0,
                i => SetWebsiteApproach(mode, i == 1), "segWebsites", new[] { "rbBlockList", "rbAllowList" }));
            wrap.Children.Add(Segmented(T("appsLabel", "Apps"),
                new[] { Cfg("focusPreferencesInfo", "titleAllowedAllApps", "Allow all apps"), Cfg("focusPreferencesInfo", "titleAllowedSelectedApps", "Allow selected apps") },
                mode.isAllowedAllApps ? 0 : 1, i => SetAppApproach(mode, i == 0), "segApps", new[] { "rbAllowAllApps", "rbAllowSelectedApps" }));
            return wrap;
        }

        private FrameworkElement Segmented(string label, string[] options, int selectedIndex, Action<int> onPick, string automationId, string[] optionIds)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 24, 6) };
            var caption = Text(label.ToUpperInvariant(), 13, _p.Secondary, FontWeights.SemiBold, false);
            caption.VerticalAlignment = VerticalAlignment.Center;
            caption.Margin = new Thickness(0, 0, 10, 0);
            row.Children.Add(caption);
            var track = new StackPanel { Orientation = Orientation.Horizontal };
            var trackFace = new Border { Child = track, CornerRadius = new CornerRadius(8), BorderBrush = _p.Border, BorderThickness = new Thickness(1), Padding = new Thickness(2), Background = _p.Field };
            AutomationProperties.SetAutomationId(trackFace, automationId);
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                bool on = i == selectedIndex;
                var text = Text(options[i], 13, on ? Solid(0x10, 0x10, 0x14) : _p.Secondary, on ? FontWeights.SemiBold : FontWeights.Normal, false);
                var face = new Border { Child = text, Padding = new Thickness(12, 5, 12, 5), CornerRadius = new CornerRadius(6), Background = on ? (Brush)_p.Selection : Brushes.Transparent };
                if (on) face.BorderBrush = _p.Accent;
                face.BorderThickness = new Thickness(on ? 1 : 0);
                track.Children.Add(FocusStartUi.Clickable(face, () => { if (index != selectedIndex) onPick(index); }, optionIds[i]));
            }
            row.Children.Add(trackFace);
            return row;
        }

        private FrameworkElement SearchBox()
        {
            var box = PlainTextBox(T("searchEntries", "Search URLs and apps"), "txtSearchBlockList");
            box.Text = _search;
            box.TextChanged += (s, e) => { _search = box.Text; RefreshEntries(); };
            var field = Field(0xE721, box);
            field.Margin = new Thickness(0, 14, 0, 0);
            return field;
        }

        private FrameworkElement AddRow(FocusMode mode, BlockListModeFlags flags)
        {
            var grid = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var box = PlainTextBox(T("addUrlPlaceholder", "Add a new URL"), "txtAddBlockListUrl");
            grid.Children.Add(Field(0xE774, box));

            var plusGlyph = FocusStartUi.Glyph(0xE710, 15, _p.Secondary);
            plusGlyph.FontWeight = FontWeights.Bold;
            var plusFace = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(10), Child = Centered(plusGlyph) };
            var plus = FocusStartUi.Clickable(plusFace, () => { if (AddWebsite(mode, box.Text)) box.Text = ""; }, "btnAddAllowedURLs", T("addUrl", "Add URL"));
            plus.Margin = new Thickness(12, 0, 0, 0);
            Action paint = () =>
            {
                bool enabled = box.Text.Trim().Length > 0;
                plus.IsEnabled = enabled;
                plusFace.Background = enabled ? (Brush)_p.Accent : new SolidColorBrush(Color.FromArgb(0x38, 0x8E, 0x8E, 0x93));
                plusGlyph.Foreground = enabled ? Brushes.White : _p.Secondary;
            };
            box.TextChanged += (s, e) => paint();
            box.KeyDown += (s, e) => { if (e.Key == Key.Enter && AddWebsite(mode, box.Text)) { box.Text = ""; e.Handled = true; } };
            paint();
            Grid.SetColumn(plus, 1);
            grid.Children.Add(plus);

            if (BlockListRules.ShowsApps(flags))
            {
                var appLabel = Text(T("addApp", "Add App"), 15, _p.Accent, FontWeights.Medium, false);
                appLabel.VerticalAlignment = VerticalAlignment.Center;
                var appFace = new Border { Height = 40, Padding = new Thickness(16, 0, 16, 0), CornerRadius = new CornerRadius(10), BorderBrush = _p.Accent, BorderThickness = new Thickness(2), Child = appLabel };
                var addApp = FocusStartUi.Clickable(appFace, () => AddApp(mode), "btnAddAllowedApp");
                addApp.Margin = new Thickness(12, 0, 0, 0);
                Grid.SetColumn(addApp, 2);
                grid.Children.Add(addApp);
            }
            return grid;
        }

        /// <summary>Redraws the chips, suggestions and table for the current search and category.</summary>
        private void RefreshEntries()
        {
            if (_selected == null || _chipsRow == null) return;
            var mode = _selected;
            var flags = Flags(mode);
            var entries = EntriesFor(mode);

            _chipsRow.Children.Clear();
            _chipsRow.Children.Add(Chip(null, entries));
            foreach (var category in DistractionCategories.OrderedForFiltering) _chipsRow.Children.Add(Chip(category, entries));

            _suggestionsHost.Content = BlockListRules.OffersSuggestions(flags, _activeCategory) ? Suggestions(mode, entries) : null;
            _tableHost.Content = Table(mode, flags, entries);
        }

        private FrameworkElement Chip(DistractionCategory? category, IReadOnlyList<BlockListEntry> entries)
        {
            bool on = _activeCategory == category;
            Brush colour = on ? (Brush)_p.Accent : _p.Secondary;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Opacity = on ? 1 : 0.8 };
            var title = Text(category.HasValue ? CategoryName(category.Value) : T("filterAll", "All"), 15, colour, FontWeights.SemiBold, false);
            var count = Text("(" + BlockListEntries.Count(entries, category) + ")", 12, colour, FontWeights.Medium, false);
            count.Margin = new Thickness(4, 0, 0, 1);
            count.VerticalAlignment = VerticalAlignment.Bottom;
            row.Children.Add(title);
            row.Children.Add(count);
            var chip = FocusStartUi.Clickable(row, () => { _activeCategory = category; RefreshEntries(); },
                                              "chip_" + (category.HasValue ? DistractionCategories.WireValue(category.Value) : "all"));
            chip.Margin = new Thickness(0, 0, 16, 0);
            return chip;
        }

        private FrameworkElement Suggestions(FocusMode mode, IReadOnlyList<BlockListEntry> entries)
        {
            var suggestions = BlockListEntries.Suggestions(entries, _activeCategory);
            if (suggestions.Count == 0) return null;
            var stack = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            string heading = T("commonlyBlockedIn", "COMMONLY BLOCKED IN <CATEGORY>:").Replace("<CATEGORY>", CategoryName(_activeCategory.Value).ToUpperInvariant());
            var label = Text(heading, 13, _p.Secondary, FontWeights.SemiBold, false);
            label.Margin = new Thickness(0, 0, 0, 8);
            stack.Children.Add(label);
            var row = new WrapPanel();
            foreach (string suggestion in suggestions)
            {
                string value = suggestion;
                var content = new StackPanel { Orientation = Orientation.Horizontal };
                var plus = FocusStartUi.Glyph(0xE710, 11, _p.Primary);
                plus.Margin = new Thickness(0, 1, 8, 0);
                content.Children.Add(plus);
                content.Children.Add(Text(value, 15, _p.Primary, FontWeights.Medium, false));
                var face = new Border { Child = content, Padding = new Thickness(12, 8, 12, 8), CornerRadius = new CornerRadius(10), BorderBrush = _p.Border, BorderThickness = new Thickness(2) };
                var chip = FocusStartUi.Clickable(face, () => AddWebsite(mode, value), "suggest_" + value);
                chip.Margin = new Thickness(0, 0, 12, 6);
                row.Children.Add(chip);
            }
            stack.Children.Add(row);
            return stack;
        }

        private FrameworkElement Table(FocusMode mode, BlockListModeFlags flags, IReadOnlyList<BlockListEntry> entries)
        {
            var columns = _metrics.Columns;
            double rowWidth = _metrics.LaidOutRowWidth;
            var table = new Grid();
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var header = RowGrid(columns, rowWidth);
            header.Margin = new Thickness(0, 0, 0, 8);
            AddCell(header, 1, Text(T("columnName", "NAME"), 13, _p.Secondary, FontWeights.SemiBold, false));
            AddCell(header, 2, Text(T("columnCategory", "CATEGORY"), 13, _p.Secondary, FontWeights.SemiBold, false));
            AddCell(header, 3, Text(T("columnType", "TYPE"), 13, _p.Secondary, FontWeights.SemiBold, false));
            table.Children.Add(header);

            var visible = BlockListEntries.Visible(entries, _activeCategory, _search);
            FrameworkElement body;
            if (visible.Count == 0)
            {
                bool unfiltered = _activeCategory == null && _search.Trim().Length == 0;
                string message = _search.Trim().Length > 0
                    ? T("noSearchMatches", "Nothing in this list matches your search.")
                    : !unfiltered
                    ? T("noEntriesForCategory", "Nothing in this category yet. Add a site above.")
                    : BlockListRules.Notice(flags) == BlockListNotice.None || BlockListRules.Notice(flags) == BlockListNotice.AlwaysBlocked
                        ? T("noEntries", "Nothing is blocked in this list yet.")
                        : T("noEntriesAllowList", "Nothing is allowed in this list yet.");
                var empty = Text(message, 15, _p.Secondary);
                empty.Margin = new Thickness(0, 20, 0, 20);
                AutomationProperties.SetAutomationId(empty, "txtNoEntries");
                body = empty;
            }
            else
            {
                var rows = new StackPanel();
                foreach (var entry in visible)
                {
                    rows.Children.Add(EntryRow(mode, entry, columns, rowWidth));
                    rows.Children.Add(Divider(new Thickness(0)));
                }
                body = new ScrollViewer { Content = rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };
            }
            Grid.SetRow(body, 1);
            table.Children.Add(body);
            return table;
        }

        private static Grid RowGrid(BlockListColumnWidths columns, double rowWidth)
        {
            var grid = new Grid { Width = rowWidth, HorizontalAlignment = HorizontalAlignment.Left };
            foreach (double width in new[] { BlockListLayout.AvatarSize, columns.Name, columns.Category, columns.Type, BlockListLayout.RemoveButtonWidth })
            {
                if (grid.ColumnDefinitions.Count > 0) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(BlockListLayout.ColumnSpacing) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
            }
            return grid;
        }

        /// <summary>Puts <paramref name="element"/> in logical column 0–4 (the spacer columns sit between them).</summary>
        private static void AddCell(Grid grid, int column, FrameworkElement element)
        {
            element.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(element, column * 2);
            grid.Children.Add(element);
        }

        private FrameworkElement EntryRow(FocusMode mode, BlockListEntry entry, BlockListColumnWidths columns, double rowWidth)
        {
            var row = RowGrid(columns, rowWidth);
            row.Margin = new Thickness(0, 12, 0, 12);
            row.Background = Brushes.Transparent;

            var letter = Text(entry.AvatarLetter, 15, _p.Primary, null, false);
            var avatar = new Border
            {
                Width = BlockListLayout.AvatarSize,
                Height = BlockListLayout.AvatarSize,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(0x33, 0x71, 0x71, 0x7A)),
                Child = Centered(letter),
            };
            AddCell(row, 0, avatar);

            var name = Text(entry.DisplayName, 15, _p.Primary, null, false);
            name.ToolTip = entry.DisplayName;
            AddCell(row, 1, name);

            var pillText = Text(CategoryName(entry.Category), 13, _p.Accent, FontWeights.SemiBold, false);
            var pill = new Border
            {
                Child = pillText,
                Padding = new Thickness(12, 5, 12, 5),
                CornerRadius = new CornerRadius(8),
                Background = Solid(0xFF, 0xED, 0xC9),
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = columns.Category,
            };
            AddCell(row, 2, pill);

            string type = entry.Type == BlockListEntryType.App ? T("typeApp", "App") : T("typeWebsite", "Website");
            var typeCell = new StackPanel();
            typeCell.Children.Add(Text(type, 15, _p.Primary, null, false));
            if (entry.IsAllowed)
            {
                var allowed = Text(T("allowedTag", "ALLOWED"), 10, Solid(0x2E, 0x9E, 0x5B), FontWeights.SemiBold, false);
                allowed.Margin = new Thickness(0, 1, 0, 0);
                typeCell.Children.Add(allowed);
            }
            AddCell(row, 3, typeCell);

            var removeFace = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(6),
                BorderBrush = _p.Border,
                BorderThickness = new Thickness(1),
                Child = Centered(FocusStartUi.Glyph(0xE738, 12, _p.Primary)),
            };
            var remove = FocusStartUi.Clickable(removeFace, () => RemoveEntry(mode, entry), "btnRemove_" + entry.Value, T("removeEntry", "Remove"));
            remove.HorizontalAlignment = HorizontalAlignment.Center;
            AddCell(row, 4, remove);

            AutomationProperties.SetName(row, entry.Value);
            if (entry.Type == BlockListEntryType.Website)
            {
                // Double-click a website to edit it, as in the old list.
                row.Cursor = Cursors.Hand;
                row.MouseLeftButtonDown += (s, e) => { if (e.ClickCount == 2) { e.Handled = true; EditWebsite(mode, entry.Value); } };
            }
            return row;
        }

        // MARK: - Actions (the old FocusPage's behaviour)

        private void Select(FocusMode mode)
        {
            if (mode == _selected) return;
            _selected = mode;
            _editingName = false;
            BuildRail();
            BuildPanel();
        }

        private void CreateBlockList()
        {
            LogWriter.LogWrite("BlockListsView CreateBlockList");
            ShowShade(true);
            var dialog = new AddFocusType(_parentWindow) { Topmost = true };
            bool? result = dialog.ShowDialog();
            ShowShade(false);
            string name = result == true ? dialog.sFocusNameToAdd ?? "" : "";
            if (name.Length == 0) return;
            name = BlockListRules.NewBlockListName(name);
            _settings.CapturePostHog("add-new-focus-mode");
            // #A3FFA1 is the default colour; a new block list starts as an allow list with every app allowed, as before.
            AddAndUpload(new FocusMode(Guid.NewGuid().ToString(), name, new ObservableCollection<string>(), new ObservableCollection<string>(), new ObservableCollection<AppDetails>(),
                                       "#A3FFA1", false, true, true, false, false, false, false, false, false));
        }

        private void DuplicateBlockList(FocusMode mode)
        {
            LogWriter.LogWrite("BlockListsView DuplicateBlockList");
            string name = BlockListRules.DuplicateName(mode.name, _settings.arrFocusModes.Select(m => m.name));
            _settings.CapturePostHog("duplicate-focus-mode");
            AddAndUpload(DuplicateOf(mode, Guid.NewGuid().ToString(), name));
        }

        /// <summary>
        /// A copy of a block list with lists of its OWN. The duplicate used to be handed the original's collections,
        /// so adding a site or app to either list added it to both — and the next sync uploaded the change twice.
        /// The copy also keeps the original's AI threshold and server metadata (AI blocking and the rest), which
        /// were dropped.
        /// </summary>
        internal static FocusMode DuplicateOf(FocusMode source, string id, string name)
        {
            var copy = new FocusMode(id, name,
                                     new ObservableCollection<string>(source.arrAllowedUrls ?? new ObservableCollection<string>()),
                                     new ObservableCollection<string>(source.arrBlockedUrls ?? new ObservableCollection<string>()),
                                     new ObservableCollection<AppDetails>((source.arrAllowedAppsWin ?? new ObservableCollection<AppDetails>()).Where(a => a != null).Select(CopyOf)),
                                     source.color, false, source.isAllowedAllApps, source.isAllowedListApproach, false, false, false, false, false, false,
                                     AlignmentScoreThreshold: source.AlignmentScoreThreshold);
            copy.serverMetadata = source.serverMetadata?.DeepClone() as Newtonsoft.Json.Linq.JObject;
            return copy;
        }

        private static AppDetails CopyOf(AppDetails app) => new AppDetails
        {
            AppName = app.AppName,
            DisplayIcon = app.DisplayIcon,
            AppInstallLocation = app.AppInstallLocation,
            ExeNames = new List<string>(app.ExeNames ?? new List<string>()),
        };

        private void AddAndUpload(FocusMode mode)
        {
            try
            {
                _settings.arrFocusModes.Add(mode);
                ((App)Application.Current).FocusModeAddedOrDeleted();
                _selected = mode;
                _activeCategory = null;
                _search = "";
                Reload();
                LogWriter.LogWrite("attempt to AddFocusModeToArrayAndUploadtoAPI");
                _ = new FocusBearAPIClient().AddFocusMode(mode);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("BlockListsView AddAndUpload ex - " + ex.Message);
            }
        }

        /// <summary>Where a block-list delete goes (the tests' fake stands in for the server).</summary>
        internal static Func<string, System.Threading.Tasks.Task<int>> DeleteOnServer = id => new FocusBearAPIClient().DeleteFocusMode(id);

        /// <summary>
        /// Removes the block list at once and asks the server to delete it; a refusal (offline, or the server said
        /// no) puts it back where it was and returns false. It used to be fire-and-forget: the list vanished, the
        /// server kept it, and it silently came back on the next fetch.
        /// </summary>
        internal static async System.Threading.Tasks.Task<bool> DeleteWithRollback(IList<FocusMode> modes, FocusMode mode)
        {
            int index = modes.IndexOf(mode);
            if (index < 0) return false;
            modes.RemoveAt(index);
            int result;
            try { result = await DeleteOnServer(mode.id); }
            catch (Exception ex) { LogWriter.LogWrite("BlockListsView delete ex - " + ex.Message); result = 0; }
            if (result == 1) return true;
            if (!modes.Contains(mode)) modes.Insert(Math.Min(index, modes.Count), mode);
            return false;
        }

        private async void DeleteBlockList(FocusMode mode)
        {
            try
            {
                if (!BlockListRules.CanDelete(Flags(mode)))
                {
                    Message(Cfg("focusPreferencesInfo", "focusCanNotDeleteSubTitle", "The Default focus type cannot be deleted."));
                    return;
                }
                if (mode.isOn)
                {
                    Message(Cfg("focusPreferencesInfo", "focusModeOnText", "Focus mode is on. Please deactivate the focus mode to delete it."));
                    return;
                }
                var confirm = new FBMessageBox(Cfg("focusPreferencesInfo", "deleteFocusTypeMessageText", "Are you sure you want to delete the selected Focus Mode?"), "Focus Bear", MessageBoxButton.YesNo);
                confirm.Owner = _parentWindow;
                confirm.ShowDialog();
                if (confirm.btnClicked != MessageBoxResult.Yes) return;

                _selected = null;
                var deleting = DeleteWithRollback(_settings.arrFocusModes, mode);
                ((App)Application.Current).FocusModeAddedOrDeleted();
                Reload();
                if (await deleting)
                {
                    _settings.CapturePostHog("delete-focus-mode");
                    return;
                }
                ((App)Application.Current).FocusModeAddedOrDeleted();
                _selected = mode;
                Reload();
                Message(Cfg("focusPreferencesInfo", "deleteFocusModeFailedText", "Could not delete the block list. Check your connection and try again."));
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("BlockListsView DeleteBlockList ex - " + ex.Message);
            }
        }

        private void Rename(FocusMode mode, string rawName)
        {
            var others = _settings.arrFocusModes.Where(m => m != mode).Select(m => m.name);
            string name;
            switch (BlockListRules.ValidateRename(rawName, mode.name, others, out name))
            {
                case BlockListRenameOutcome.Renamed:
                    mode.name = name;
                    _settings.CapturePostHog("change-focus-mode-name");
                    ((App)Application.Current).FocusModeAddedOrDeleted();
                    break;
                case BlockListRenameOutcome.Duplicate:
                    Message(Cfg("focusPreferencesInfo", "errorSameFocusType", "Focus Mode with the same name already exists."));
                    break;
            }
        }

        private void SetColour(FocusMode mode, string colour)
        {
            if (string.Equals(mode.color, colour, StringComparison.OrdinalIgnoreCase)) return;
            mode.color = colour;
            _settings.CapturePostHog("change-focus-mode-color");
            ((App)Application.Current).FocusModeAddedOrDeleted();
            Reload();
        }

        private void SetWebsiteApproach(FocusMode mode, bool allowList)
        {
            LogWriter.LogWrite(allowList ? "AllowList_Checked" : "BlockList_Checked");
            mode.isAllowedListApproach = allowList;
            BuildPanel();
            BuildRail();
        }

        private void SetAppApproach(FocusMode mode, bool allowAll)
        {
            LogWriter.LogWrite(allowAll ? "AllowAllApps_Checked" : "AllowSelectedApps_Checked");
            mode.isAllowedAllApps = allowAll;
            BuildPanel();
            BuildRail();
        }

        private bool AddWebsite(FocusMode mode, string rawValue)
        {
            var list = WebsiteList(mode);
            if (list == null) return false;
            string value;
            switch (BlockListRules.ValidateNewWebsite(rawValue, list, out value))
            {
                case BlockListAddOutcome.Empty:
                    return false;
                case BlockListAddOutcome.Duplicate:
                    Message(Cfg("focusPreferencesInfo", "urlAlreadyExistsText", "<APPNAME> already exists in the list.").Replace("<APPNAME>", value));
                    return false;
            }
            list.Add(value);
            if (mode.isDefault)
            {
                _settings.CapturePostHog("add-always-blocked-url");
                _settings.bHasEditedBlockedURLs = true;
            }
            else _settings.CapturePostHog(mode.isAllowedListApproach ? "add-allowed-url-in-focus-mode" : "add-blocked-url-in-focus-mode");
            RefreshEntries();
            BuildRail();
            return true;
        }

        private void EditWebsite(FocusMode mode, string current)
        {
            var list = WebsiteList(mode);
            if (list == null) return;
            ShowShade(true);
            var dialog = mode.isAllowedListApproach && !mode.isDefault
                ? new AddWebUrl(_parentWindow, current, true)
                : new AddWebUrl(_parentWindow, current, true, true);
            dialog.Topmost = true;
            bool? result = dialog.ShowDialog();
            ShowShade(false);
            if (result != true) return;
            string url = (dialog.sURLToAdd ?? "").Trim();
            if (url.Length == 0 || url == current) return;
            if (list.Contains(url))
            {
                Message(Cfg("focusPreferencesInfo", "urlAlreadyExistsText", "<APPNAME> already exists in the list.").Replace("<APPNAME>", url));
                return;
            }
            int index = list.IndexOf(current);
            if (index < 0) return;
            list[index] = url;
            if (mode.isDefault) _settings.bHasEditedBlockedURLs = true;
            RefreshEntries();
        }

        private void RemoveEntry(FocusMode mode, BlockListEntry entry)
        {
            try
            {
                if (entry.Type == BlockListEntryType.Website)
                {
                    var list = WebsiteList(mode);
                    if (list == null || !list.Remove(entry.Value)) return;
                    if (mode.isDefault)
                    {
                        _settings.CapturePostHog("remove-always-blocked-url");
                        _settings.bHasEditedBlockedURLs = true;
                    }
                }
                else
                {
                    var app = mode.arrAllowedAppsWin.FirstOrDefault(a => (a?.AppName ?? "") == entry.Value);
                    if (app == null) return;
                    mode.arrAllowedAppsWin.Remove(app);
                }
                RefreshEntries();
                BuildRail();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("BlockListsView RemoveEntry ex - " + ex.Message);
            }
        }

        /// <summary>The old flow: the app picker, and from it the "add apps manually" dialog.</summary>
        private void AddApp(FocusMode mode)
        {
            ShowShade(true);
            var picker = new AddApp(_parentWindow, mode) { Topmost = true };
            bool? result = picker.ShowDialog();
            ShowShade(false);
            if (result == false) AddAppsManually(mode);
            RefreshEntries();
            BuildRail();
        }

        private void AddAppsManually(FocusMode mode)
        {
            LogWriter.LogWrite("ShowAddAppManuallyWindow()");
            ShowShade(true);
            var dialog = new AddAppsManually(_parentWindow) { Topmost = true };
            bool? result = dialog.ShowDialog();
            ShowShade(false);
            string typed = result == true ? dialog.sAppsToAdd ?? "" : "";
            try
            {
                bool added = false;
                foreach (var custom in dialog.SelectedCustomApps)
                {
                    if (!mode.arrAllowedAppsWin.Any(a => a.AppName.ToLower() == custom.AppName.ToLower()))
                    {
                        mode.arrAllowedAppsWin.Add(custom);
                        added = true;
                    }
                }
                foreach (string part in typed.Split(','))
                {
                    string app = part.Trim().ToLower();
                    if (app.Length > 0 && app.Contains(".exe") && !mode.arrAllowedAppsWin.Any(a => a.AppName == app))
                    {
                        mode.arrAllowedAppsWin.Add(new AppDetails(app, app));
                        added = true;
                    }
                }
                if (added) _settings.CapturePostHog("add-allowed-app-manually-in-focus-mode");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowAddAppManuallyWindow ex - " + ex.Message);
            }
        }

        private void OpenAdvanced(FocusMode mode)
        {
            LogWriter.LogWrite("btnadvanced_Click");
            _settings.CapturePostHog("open-advanced-focus-mode-preferences");
            ShowShade(true);
            var advanced = new FocusAdvanced(_parentWindow, mode) { Topmost = true };
            advanced.ShowDialog();
            ShowShade(false);
            RefreshEntries();
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible) return;
            // Leaving the screen syncs every block list, exactly as the old page did.
            LogWriter.LogWrite("attempt to UpdateAllFocusModes() API");
            _ = new FocusBearAPIClient().UpdateAllFocusModes();
        }

        // MARK: - Helpers

        private void Message(string text)
        {
            var box = new FBMessageBox(text, "Focus Bear");
            box.Owner = _parentWindow;
            box.ShowDialog();
        }

        private void ShowShade(bool show)
        {
            if (_parentWindow == null) return;
            if (show) _parentWindow.RepositionWindow();
            _parentWindow.GridShade.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnThemeChanged()
        {
            Dispatcher.BeginInvoke(new Action(() => { TitleBarThemeChange(); Reload(); }));
        }

        private void TitleBarThemeChange()
        {
            var source = PresentationSource.FromVisual(this) as HwndSource;
            if (source != null) ((App)Application.Current).UseImmersiveDarkMode(source.Handle, _settings.sTheme != "Light");
        }

        private void ApplyAppFont()
        {
            string font = _settings.sSelectedAppFont;
            if (string.IsNullOrEmpty(font)) return;
            if (font == "Poppins" || font == "IntelOne Mono" || font == "OpenDyslexic3 Regular")
                FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", font));
            else
                FontFamily = new FontFamily(font);
        }

        private TextBlock Text(string text, double size, Brush colour, FontWeight? weight = null, bool wrap = true) =>
            FocusStartUi.Text(text, size, colour, weight, wrap);

        private TextBox PlainTextBox(string placeholder, string automationId)
        {
            var box = new TextBox
            {
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = _p.Primary,
                CaretBrush = _p.Primary,
                FontSize = 15,
                VerticalContentAlignment = VerticalAlignment.Center,
                Tag = placeholder,
            };
            AutomationProperties.SetAutomationId(box, automationId);
            AutomationProperties.SetName(box, placeholder);
            return box;
        }

        /// <summary>A 40pt rounded field: a leading glyph, the text box, and its placeholder behind it.</summary>
        private FrameworkElement Field(int glyph, TextBox box)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var icon = FocusStartUi.Glyph(glyph, 14, _p.Secondary);
            icon.Margin = new Thickness(0, 0, 8, 0);
            grid.Children.Add(icon);
            var placeholder = Text(box.Tag as string, 15, _p.Secondary, null, false);
            placeholder.IsHitTestVisible = false;
            placeholder.VerticalAlignment = VerticalAlignment.Center;
            placeholder.Margin = new Thickness(3, 0, 0, 0);
            placeholder.Visibility = string.IsNullOrEmpty(box.Text) ? Visibility.Visible : Visibility.Collapsed;
            box.TextChanged += (s, e) => placeholder.Visibility = string.IsNullOrEmpty(box.Text) ? Visibility.Visible : Visibility.Collapsed;
            Grid.SetColumn(placeholder, 1);
            Grid.SetColumn(box, 1);
            grid.Children.Add(placeholder);
            grid.Children.Add(box);
            var face = new Border
            {
                Height = 40,
                Padding = new Thickness(12, 0, 12, 0),
                CornerRadius = new CornerRadius(10),
                BorderBrush = _p.Border,
                BorderThickness = new Thickness(2),
                Background = _p.Field,
                Child = grid,
                Cursor = Cursors.IBeam,
            };
            face.MouseLeftButtonDown += (s, e) => { box.Focus(); };
            return face;
        }

        private Border Divider(Thickness margin) => new Border { Height = 1, Background = _p.Border, Margin = margin };

        private static FrameworkElement Centered(FrameworkElement child, double size = double.NaN)
        {
            child.HorizontalAlignment = HorizontalAlignment.Center;
            child.VerticalAlignment = VerticalAlignment.Center;
            var grid = new Grid { Background = Brushes.Transparent };
            if (!double.IsNaN(size)) { grid.Width = size; grid.Height = size; }
            grid.Children.Add(child);
            return grid;
        }

        private static SolidColorBrush Solid(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        private static Brush ColourBrush(string colour)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(colour))
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colour.StartsWith("#") ? colour : "#" + colour));
            }
            catch (FormatException) { }
            return Brushes.Transparent;
        }
    }
}
