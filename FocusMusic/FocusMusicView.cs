using Focus_Bear.Models;
using Focus_Bear.Utils;
using FocusBear.Core.FocusMusic;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Path = System.IO.Path;

namespace Focus_Bear.FocusMusic
{
    /// <summary>
    /// The Focus Music screen — the Mac's <c>FocusMusicView</c> / <c>FocusMusicViewModel</c>, built in code. Whatever is
    /// playing, from any source, sits in a Now Playing card pinned under the header; below it the user's own Spotify
    /// links (opened in the Spotify app) and YouTube Music links (played in a tiny embedded player), the "Add my own
    /// music" inputs, and the curated, downloadable library (played locally). Only one source plays at a time.
    /// The screen lives as long as the main window, so music keeps playing while other screens are shown.
    /// </summary>
    public sealed class FocusMusicView : UserControl
    {
        private static readonly Color AccentColor = Color.FromRgb(0xDE, 0x74, 0x00);
        private static readonly Brush Accent = Frozen(new SolidColorBrush(AccentColor));
        private static readonly Brush AccentTint = Frozen(new SolidColorBrush(Color.FromArgb(0x1F, 0xDE, 0x74, 0x00)));
        private static readonly Brush AccentEdge = Frozen(new SolidColorBrush(Color.FromArgb(0x66, 0xDE, 0x74, 0x00)));
        private static readonly Brush SpotifyGreen = Frozen(new SolidColorBrush(Color.FromRgb(0x1D, 0xB9, 0x54)));
        private static readonly Brush YouTubeRed = Frozen(new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x33)));
        private static readonly Brush ErrorRed = Frozen(new SolidColorBrush(Color.FromRgb(0xE5, 0x48, 0x3E)));
        private static readonly FontFamily Symbols = new FontFamily("Segoe MDL2 Assets");

        private const int GlyphPlay = 0xE768, GlyphPause = 0xE769, GlyphStop = 0xE71A, GlyphDelete = 0xE74D, GlyphDownload = 0xE896,
            GlyphVolume = 0xE767, GlyphMute = 0xE74F, GlyphAdd = 0xE710, GlyphMusic = 0xE8D6, GlyphOpen = 0xE8A7;

        private readonly AppSettings appSettings = AppSettings.GetInstance;
        private readonly MediaPlayer _player = new MediaPlayer();
        private readonly DispatcherTimer _playbackTimer;
        private FocusMusicTrack _currentTrack;

        // Added links. Spotify plays in its own app, so all we know is what we last opened (and paused).
        private CustomMusicLinkList _links = CustomMusicLinkStore.Load();
        private string _spotifyPlayingId;
        private bool _spotifyPaused, _spotifyInBrowser;
        private readonly YouTubeMusicPlayer _youtube = new YouTubeMusicPlayer { Width = 112, Height = 63 };

        // Built once and moved between rebuilds, so typing and the player survive a re-render.
        private readonly StackPanel _body = new StackPanel();
        private readonly Border _nowPlaying = new Border();
        private readonly Slider _volume;
        private readonly TextBlock _volumeValue;
        private readonly TextBox _spotifyInput, _youtubeInput;
        private readonly TextBlock _spotifyError, _youtubeError;
        private bool _showAddOwnMusic;
        private FrameworkElement _addOwnMusicSection;
        private bool _renderQueued;

        // The Now Playing card's live parts.
        private TextBlock _nowTime;
        private ScaleTransform _nowProgress;

        private readonly Strings _s = new Strings();

        public FocusMusicView()
        {
            LogWriter.LogWrite("FocusMusicView() Initialized");
            AutomationProperties.SetAutomationId(this, "FocusMusicView");
            SetResourceReference(BackgroundProperty, "BackgroundColor");
            ApplyAppFont();
            // The Preferences host paints every Grid with the page background, which covers a row's hover
            // and playing tint behind its text; this screen draws its own surfaces, so block that style.
            Resources[typeof(Grid)] = new Style(typeof(Grid));

            _volume =new Slider { Minimum = 0, Maximum = 1, Value = 0.6, Width = 150, VerticalAlignment = VerticalAlignment.Center, IsMoveToPointEnabled = true };
            AutomationProperties.SetAutomationId(_volume, "sldVolume");
            AutomationProperties.SetName(_volume, "Volume");
            _volumeValue = Text("", 12, secondary: true, wrap: false);
            _volumeValue.Width = 34;
            _volumeValue.VerticalAlignment = VerticalAlignment.Center;
            _volume.ValueChanged += (s, e) => ApplyVolume();

            _spotifyInput = Input("txtSpotifyLink");
            _youtubeInput = Input("txtYouTubeLink");
            _spotifyError = Text("", 12, wrap: true);
            _spotifyError.Foreground = ErrorRed;
            _youtubeError = Text("", 12, wrap: true);
            _youtubeError.Foreground = ErrorRed;
            _spotifyInput.TextChanged += (s, e) => _spotifyError.Visibility = Visibility.Collapsed;
            _youtubeInput.TextChanged += (s, e) => _youtubeError.Visibility = Visibility.Collapsed;
            _spotifyInput.KeyDown += (s, e) => { if (e.Key == Key.Enter) AddSpotify(); };
            _youtubeInput.KeyDown += (s, e) => { if (e.Key == Key.Enter) AddYouTube(); };
            _spotifyError.Visibility = _youtubeError.Visibility = Visibility.Collapsed;

            _youtube.TrackInfo += (title, author) => Dispatcher.BeginInvoke(new Action(() => ResolvePlayingYouTubeTitle(title, author)));

            Content = BuildLayout();
            ApplyVolume();

            _player.MediaEnded += Player_MediaEnded;
            _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _playbackTimer.Tick += PlaybackTimer_Tick;   // runs only while a track plays (started with it)

            appSettings.arrFocusMusicTracks.CollectionChanged += Tracks_CollectionChanged;
            foreach (var track in appSettings.arrFocusMusicTracks) track.PropertyChanged += Track_PropertyChanged;

            Render();
            BackfillLinkTitles();
        }

        // ------------------------------------------------------------------ layout

        private FrameworkElement BuildLayout()
        {
            var title = Text(_s.Title, 22, FontWeights.SemiBold, wrap: false);
            var description = Text(_s.Description, 13, secondary: true);
            description.Margin = new Thickness(0, 6, 0, 0);
            description.MaxWidth = 640;
            description.HorizontalAlignment = HorizontalAlignment.Left;
            AutomationProperties.SetAutomationId(description, "txtFocusMusicIntro");
            var titles = new StackPanel();
            titles.Children.Add(title);
            titles.Children.Add(description);

            var volumeRow = new StackPanel { Orientation = Orientation.Horizontal };
            var speaker = Glyph(GlyphVolume, 14, "TraySubtitleColor");
            speaker.Margin = new Thickness(0, 0, 8, 0);
            speaker.ToolTip = _s.Volume;
            volumeRow.Children.Add(speaker);
            volumeRow.Children.Add(_volume);
            _volumeValue.Margin = new Thickness(8, 0, 0, 0);
            volumeRow.Children.Add(_volumeValue);
            var volumeCard = Card(volumeRow, new Thickness(14, 8, 12, 8), 18);
            volumeCard.VerticalAlignment = VerticalAlignment.Top;
            volumeCard.Margin = new Thickness(24, 2, 0, 0);
            AutomationProperties.SetName(volumeCard, _s.Volume);

            var header = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(volumeCard, Dock.Right);
            header.Children.Add(volumeCard);
            header.Children.Add(titles);

            // Pinned above the scrolling lists: the YouTube player is a native window, which can't be clipped by a
            // ScrollViewer, and what's playing should stay in view anyway (the Mac pins it to the top too).
            _nowPlaying.Margin = new Thickness(0, 18, 0, 0);
            AutomationProperties.SetAutomationId(_nowPlaying, "cardNowPlaying");

            var top = new StackPanel { Margin = new Thickness(0, 22, 0, 14) };
            top.Children.Add(header);
            top.Children.Add(_nowPlaying);

            var column = new DockPanel { MaxWidth = 980, Margin = new Thickness(28, 0, 28, 0) };
            DockPanel.SetDock(top, Dock.Top);
            column.Children.Add(top);
            var divider = new Border { Height = 1 };
            divider.SetResourceReference(Border.BackgroundProperty, "TrayCardBorder");
            DockPanel.SetDock(divider, Dock.Top);
            column.Children.Add(divider);

            _body.Margin = new Thickness(0, 18, 16, 28);
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _body, Focusable = false };
            column.Children.Add(scroll);
            return column;
        }

        /// <summary>Re-render on the next dispatcher turn — track state changes arrive in bursts.</summary>
        private void QueueRender()
        {
            if (_renderQueued) return;
            _renderQueued = true;
            Dispatcher.BeginInvoke(new Action(() => { _renderQueued = false; Render(); }), DispatcherPriority.Background);
        }

        private void Render()
        {
            RenderNowPlaying();
            _body.Children.Clear();

            var spotify = _links.Of(CustomMusicSource.Spotify).ToList();
            var youtube = _links.Of(CustomMusicSource.YouTubeMusic).ToList();
            if (spotify.Count > 0) _body.Children.Add(Section(_s.Spotify, Badge(SpotifyGreen, GlyphMusic), RowsCard(spotify.Select(SpotifyRow))));
            if (youtube.Count > 0)
            {
                var rows = youtube.Select(YouTubeRow).ToList();
                var card = RowsCard(rows);
                var section = Section(_s.YouTubeMusic, Badge(YouTubeRed, GlyphPlay), card);
                if (_youtube.PlayingId == null)
                {
                    var hint = Text(_s.YouTubeHint, 12, secondary: true);
                    hint.Margin = new Thickness(2, 8, 0, 0);
                    ((StackPanel)section).Children.Add(hint);
                }
                _body.Children.Add(section);
            }

            if (_showAddOwnMusic)
            {
                // Built once: what's typed, and its placeholder hint, survive every re-render.
                if (_addOwnMusicSection == null) _addOwnMusicSection = AddOwnMusicCard();
                _body.Children.Add(_addOwnMusicSection);
            }
            else
            {
                _body.Children.Add(AddOwnMusicButton());
            }
            _body.Children.Add(LibrarySection());
        }

        // ------------------------------------------------------------------ now playing

        private void RenderNowPlaying()
        {
            _nowTime = null;
            _nowProgress = null;
            if (_youtube.Parent is Panel oldSlot) oldSlot.Children.Remove(_youtube);

            var local = appSettings.arrFocusMusicTracks.FirstOrDefault(t => t.IsPlaying || t.IsPaused);
            var spotify = _spotifyPlayingId == null ? null : _links.Find(CustomMusicSource.Spotify, _spotifyPlayingId);
            var youtube = _youtube.PlayingId == null ? null : _links.Find(CustomMusicSource.YouTubeMusic, _youtube.PlayingId);

            FrameworkElement art;
            string title, subtitle;
            bool isPlaying;
            Action toggle, stop;
            FrameworkElement extra = null;

            if (local != null)
            {
                art = Artwork(local, 56, local.IsPlaying);
                title = local.Name;
                subtitle = _s.Library;
                isPlaying = local.IsPlaying;
                toggle = () => TogglePause(local);
                stop = StopLocal;
                var timing = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
                _nowTime = Text("", 12, secondary: true, wrap: false);
                _nowTime.Margin = new Thickness(10, 0, 0, 0);
                _nowTime.VerticalAlignment = VerticalAlignment.Center;
                DockPanel.SetDock(_nowTime, Dock.Right);
                _nowProgress = new ScaleTransform(0, 1);
                var track = new Border { Height = 4, CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Center };
                track.SetResourceReference(Border.BackgroundProperty, "TrayCardBorder");
                track.Child = new Border { Background = Accent, CornerRadius = new CornerRadius(2), RenderTransform = _nowProgress };
                timing.Children.Add(_nowTime);
                timing.Children.Add(track);
                extra = timing;
                UpdateNowPlayingTime();
            }
            else if (youtube != null)
            {
                var slot = new Grid { Width = 112, Height = 63 };
                slot.Children.Add(_youtube);
                art = slot;
                title = youtube.Name;
                subtitle = _s.YouTubeMusic;
                isPlaying = !_youtube.IsPaused;
                toggle = () => { _youtube.SetPaused(!_youtube.IsPaused); QueueRender(); };
                stop = StopYouTube;
            }
            else if (spotify != null)
            {
                art = Badge(SpotifyGreen, GlyphMusic, 56);
                title = spotify.Name;
                subtitle = _s.Spotify + "  ·  " + (_spotifyInBrowser ? _s.SpotifyOpenedInBrowser : _s.SpotifyOpenedInApp);
                isPlaying = !_spotifyPaused;
                toggle = ToggleSpotifyPause;
                stop = StopSpotify;
            }
            else
            {
                _nowPlaying.Visibility = Visibility.Collapsed;
                _nowPlaying.Child = null;
                return;
            }

            var label = Text(_s.NowPlaying.ToUpperInvariant(), 11, FontWeights.Bold, wrap: false);
            label.Foreground = Accent;
            var name = Text(title, 17, FontWeights.SemiBold, wrap: false);
            name.Margin = new Thickness(0, 2, 0, 0);
            AutomationProperties.SetAutomationId(name, "txtNowPlayingTitle");
            var sub = Text(subtitle, 12, secondary: true, wrap: false);
            sub.Margin = new Thickness(0, 2, 0, 0);
            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 16, 0) };
            info.Children.Add(label);
            info.Children.Add(name);
            info.Children.Add(sub);
            if (extra != null) info.Children.Add(extra);

            var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (youtube != null)
            {
                var mute = CircleButton(_youtube.IsMuted ? GlyphMute : GlyphVolume, false, 36, _youtube.IsMuted ? _s.Unmute : _s.Mute, () => { _youtube.SetMuted(!_youtube.IsMuted); QueueRender(); }, "btnNowPlayingMute");
                mute.Margin = new Thickness(0, 0, 10, 0);
                controls.Children.Add(mute);
            }
            var playPause = CircleButton(isPlaying ? GlyphPause : GlyphPlay, true, 44, isPlaying ? _s.Pause : _s.Play, toggle, "btnNowPlayingPlayPause");
            controls.Children.Add(playPause);
            var stopButton = CircleButton(GlyphStop, false, 36, _s.Stop, stop, "btnNowPlayingStop");
            stopButton.Margin = new Thickness(10, 0, 0, 0);
            controls.Children.Add(stopButton);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            art.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(info, 1);
            Grid.SetColumn(controls, 2);
            grid.Children.Add(art);
            grid.Children.Add(info);
            grid.Children.Add(controls);

            _nowPlaying.Child = grid;
            _nowPlaying.Background = AccentTint;
            _nowPlaying.BorderBrush = AccentEdge;
            _nowPlaying.BorderThickness = new Thickness(1);
            _nowPlaying.CornerRadius = new CornerRadius(14);
            _nowPlaying.Padding = new Thickness(14, 12, 16, 12);
            _nowPlaying.Visibility = Visibility.Visible;
        }

        private void UpdateNowPlayingTime()
        {
            var track = appSettings.arrFocusMusicTracks.FirstOrDefault(t => t.IsPlaying || t.IsPaused);
            if (track == null || _nowTime == null) return;
            var elapsed = track.PausedOffset + (track.IsPlaying ? DateTime.Now - track.StartTime : TimeSpan.Zero);
            string total = string.IsNullOrEmpty(track.DurationFormatted) ? "" : "  /  " + track.DurationFormatted;
            _nowTime.Text = FocusMusicTime.Clock(elapsed) + total;
            if (_nowProgress != null) _nowProgress.ScaleX = FocusMusicTime.Fraction(elapsed, track.Duration);
        }

        // ------------------------------------------------------------------ sections

        private StackPanel Section(string title, FrameworkElement icon, FrameworkElement content)
        {
            var heading = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 0, 0, 10) };
            if (icon != null)
            {
                icon.Margin = new Thickness(0, 0, 10, 0);
                icon.VerticalAlignment = VerticalAlignment.Center;
                heading.Children.Add(icon);
            }
            var text = Text(title, 15, FontWeights.SemiBold, wrap: false);
            text.VerticalAlignment = VerticalAlignment.Center;
            heading.Children.Add(text);
            var section = new StackPanel { Margin = new Thickness(0, 0, 0, 24) };
            section.Children.Add(heading);
            section.Children.Add(content);
            return section;
        }

        private static Border RowsCard(System.Collections.Generic.IEnumerable<FrameworkElement> rows)
        {
            var stack = new StackPanel();
            var list = rows.ToList();
            bool first = true;
            for (int i = 0; i < list.Count; i++)
            {
                var row = list[i];
                // A highlighted first / last row follows the card's rounded corners.
                if (row is Border face)
                    face.CornerRadius = new CornerRadius(i == 0 ? 11 : 0, i == 0 ? 11 : 0, i == list.Count - 1 ? 11 : 0, i == list.Count - 1 ? 11 : 0);
                if (!first)
                {
                    // Edge to edge: an inset line left notches in a hovered or playing row's fill.
                    var line = new Border { Height = 1 };
                    line.SetResourceReference(Border.BackgroundProperty, "TrayCardBorder");
                    stack.Children.Add(line);
                }
                first = false;
                stack.Children.Add(row);
            }
            return Card(stack, new Thickness(0), 12);
        }

        private FrameworkElement SpotifyRow(CustomMusicLink link)
        {
            bool playing = _spotifyPlayingId == link.Id;
            var play = CircleButton(playing ? GlyphStop : GlyphPlay, playing, 34, playing ? _s.Stop : _s.Play, () => ToggleSpotify(link), "btnSpotifyPlay");
            string kind = SpotifyLink.KindOf(link.Id) ?? "";
            return LinkRow(play, link.Name, string.IsNullOrEmpty(kind) ? link.Url : Capitalize(kind) + "  ·  " + link.Url, playing,
                () => RemoveLink(link), "rowSpotify");
        }

        private FrameworkElement YouTubeRow(CustomMusicLink link)
        {
            bool playing = _youtube.PlayingId == link.Id;
            var play = CircleButton(playing ? GlyphStop : GlyphPlay, playing, 34, playing ? _s.Stop : _s.Play, () => ToggleYouTube(link), "btnYouTubePlay");
            return LinkRow(play, link.Name, link.Url, playing, () => RemoveLink(link), "rowYouTube");
        }

        private FrameworkElement LinkRow(FrameworkElement play, string name, string caption, bool playing, Action remove, string automationId)
        {
            var titleText = Text(name, 14, playing ? FontWeights.SemiBold : FontWeights.Normal, wrap: false);
            var captionText = Text(caption, 12, secondary: true, wrap: false);
            captionText.TextTrimming = TextTrimming.CharacterEllipsis;
            captionText.Margin = new Thickness(0, 2, 0, 0);
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 12, 0) };
            text.Children.Add(titleText);
            text.Children.Add(captionText);
            var trash = IconButton(GlyphDelete, _s.Remove, remove, "btnRemoveLink");

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(text, 1);
            Grid.SetColumn(trash, 2);
            grid.Children.Add(play);
            grid.Children.Add(text);
            grid.Children.Add(trash);
            var row = new Border { Child = grid, Padding = new Thickness(12, 9, 10, 9), Background = playing ? AccentTint : Brushes.Transparent };
            AutomationProperties.SetAutomationId(row, automationId);
            AutomationProperties.SetName(row, name);
            return row;
        }

        private FrameworkElement AddOwnMusicButton()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var plus = Glyph(GlyphAdd, 12, "ForegroundColor");
            plus.Margin = new Thickness(0, 0, 8, 0);
            row.Children.Add(plus);
            row.Children.Add(Text(_s.AddOwnMusic, 13, FontWeights.SemiBold, wrap: false));
            var face = new Border { Child = row, CornerRadius = new CornerRadius(18), Padding = new Thickness(16, 8, 18, 8), BorderThickness = new Thickness(1) };
            face.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            face.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
            var button = Clickable(face, () => { _showAddOwnMusic = true; Render(); _spotifyInput.Focus(); }, "btnAddOwnMusic", null);
            button.HorizontalAlignment = HorizontalAlignment.Left;
            button.Margin = new Thickness(0, 0, 0, 26);
            Hover(button, face);
            return button;
        }

        private FrameworkElement AddOwnMusicCard()
        {
            var stack = new StackPanel();
            stack.Children.Add(AddRow(Badge(SpotifyGreen, GlyphMusic), _spotifyInput, _s.SpotifyPlaceholder, AddSpotify, "btnAddSpotify"));
            _spotifyError.Margin = new Thickness(42, 6, 0, 0);
            stack.Children.Add(_spotifyError);
            var gap = new Border { Height = 12 };
            stack.Children.Add(gap);
            stack.Children.Add(AddRow(Badge(YouTubeRed, GlyphPlay), _youtubeInput, _s.YouTubePlaceholder, AddYouTube, "btnAddYouTube"));
            _youtubeError.Margin = new Thickness(42, 6, 0, 0);
            stack.Children.Add(_youtubeError);
            var card = Card(stack, new Thickness(16), 12);
            AutomationProperties.SetAutomationId(card, "cardAddOwnMusic");
            return Section(_s.AddOwnMusic, null, card);
        }

        private FrameworkElement AddRow(FrameworkElement badge, TextBox input, string placeholder, Action add, string automationId)
        {
            var hint = Text(placeholder, 13, secondary: true, wrap: false);
            hint.IsHitTestVisible = false;
            hint.VerticalAlignment = VerticalAlignment.Center;
            hint.Margin = new Thickness(11, 0, 0, 0);
            Action syncHint = () => hint.Visibility = input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            input.TextChanged += (s, e) => syncHint();
            syncHint();
            var field = new Grid();
            field.Children.Add(input);
            field.Children.Add(hint);

            var addButton = PillButton(_s.Add, add, automationId);
            addButton.Margin = new Thickness(10, 0, 0, 0);
            badge.Margin = new Thickness(0, 0, 12, 0);
            badge.VerticalAlignment = VerticalAlignment.Center;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(field, 1);
            Grid.SetColumn(addButton, 2);
            grid.Children.Add(badge);
            grid.Children.Add(field);
            grid.Children.Add(addButton);
            return grid;
        }

        private FrameworkElement LibrarySection()
        {
            var tracks = appSettings.arrFocusMusicTracks.ToList();
            FrameworkElement content;
            if (tracks.Count == 0)
            {
                var empty = Text(_s.LibraryEmpty, 13, secondary: true);
                empty.Margin = new Thickness(16);
                content = Card(empty, new Thickness(0), 12);
            }
            else
            {
                content = RowsCard(tracks.Select(LibraryRow));
            }
            AutomationProperties.SetAutomationId(content, "lvListTracks");
            return Section(_s.Library, null, content);
        }

        private FrameworkElement LibraryRow(FocusMusicTrack track)
        {
            bool active = track.IsPlaying || track.IsPaused;
            var art = Artwork(track, 48, track.IsPlaying);

            var name = Text(track.Name, 14, active ? FontWeights.SemiBold : FontWeights.Normal, wrap: false);
            var meta = Text(track.DurationFormatted, 12, secondary: true, wrap: false);
            meta.Margin = new Thickness(0, 3, 0, 0);
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 12, 0) };
            text.Children.Add(name);
            text.Children.Add(meta);

            FrameworkElement action;
            if (track.IsDownloading)
            {
                var bar = new ProgressBar { Width = 96, Height = 4, Minimum = 0, Maximum = 100, Value = track.DownloadProgress, Foreground = Accent, BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Center };
                bar.SetResourceReference(BackgroundProperty, "TrayCardBorder");
                var percent = Text(track.DownloadProgress + "%", 12, secondary: true, wrap: false);
                percent.Width = 38;
                percent.TextAlignment = TextAlignment.Right;
                percent.VerticalAlignment = VerticalAlignment.Center;
                var progress = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                progress.Children.Add(bar);
                progress.Children.Add(percent);
                progress.Tag = track;
                AutomationProperties.SetAutomationId(progress, "pbDownload");
                action = progress;
            }
            else if (!track.IsAudioDownloaded)
            {
                action = DownloadButton(track);
            }
            else
            {
                action = CircleButton(track.IsPlaying ? GlyphPause : GlyphPlay, active, 36, track.IsPlaying ? _s.Pause : _s.Play, () => PlayOrToggle(track), "btnPlayPause");
            }
            action.VerticalAlignment = VerticalAlignment.Center;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(text, 1);
            Grid.SetColumn(action, 2);
            grid.Children.Add(art);
            grid.Children.Add(text);
            grid.Children.Add(action);
            var row = new Border { Child = grid, Padding = new Thickness(12, 10, 14, 10), Background = active ? AccentTint : Brushes.Transparent };
            AutomationProperties.SetAutomationId(row, "rowTrack");
            AutomationProperties.SetName(row, track.Name ?? "");
            // A downloaded track plays on a click anywhere on its row, like the Mac's list.
            if (track.IsAudioDownloaded && !track.IsDownloading)
            {
                row.Cursor = Cursors.Hand;
                row.MouseLeftButtonUp += (s, e) => { if (!(e.OriginalSource is DependencyObject d && IsInsideButton(d, row))) PlayOrToggle(track); };
                if (!active) Hover(row, row);
            }
            return row;
        }

        private FrameworkElement DownloadButton(FocusMusicTrack track)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var icon = new TextBlock { Text = ((char)GlyphDownload).ToString(), FontFamily = Symbols, FontSize = 12, Foreground = Accent, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) };
            row.Children.Add(icon);
            row.Children.Add(Text(string.IsNullOrEmpty(track.DownloadButtonText) ? _s.Download : track.DownloadButtonText, 13, FontWeights.SemiBold, wrap: false));
            var face = new Border { Child = row, CornerRadius = new CornerRadius(16), Padding = new Thickness(13, 6, 15, 6), BorderThickness = new Thickness(1), BorderBrush = AccentEdge, Background = Brushes.Transparent };
            var button = Clickable(face, async () => await DownloadAudioWithProgressAsync(track), "btnDownload", null);
            // One "Download" per track: say which.
            AutomationProperties.SetName(button, _s.Download + " " + track.Name);
            Hover(button, face);
            return button;
        }

        /// <summary>The track's cover (or a note on a tile until it's downloaded); the animated wave while it plays.</summary>
        private FrameworkElement Artwork(FocusMusicTrack track, double size, bool playing)
        {
            var tile = new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size / 5), ClipToBounds = true };
            tile.SetResourceReference(Border.BackgroundProperty, "TrayCardBorder");
            if (playing)
            {
                tile.Background = AccentTint;
                tile.Child = Equalizer(size);
                return tile;
            }
            var cover = LoadCover(track.LocalImagePath, (int)(size * 2));
            if (cover != null)
            {
                // The library's covers are black silhouettes on transparency: paint them in the accent on an accent
                // tint, which reads in light and dark alike (black on a grey tile vanished in dark mode).
                tile.Background = AccentTint;
                tile.Child = new Rectangle { Fill = Accent, OpacityMask = new ImageBrush(cover) { Stretch = Stretch.Uniform }, Margin = new Thickness(size * 0.12) };
                return tile;
            }
            var note = Glyph(GlyphMusic, size * 0.4, "TraySubtitleColor");
            note.HorizontalAlignment = HorizontalAlignment.Center;
            tile.Child = note;
            return tile;
        }

        /// <summary>Four accent bars bouncing out of step — the Mac's animated "audio wave", drawn rather than a GIF.</summary>
        private static FrameworkElement Equalizer(double size)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Height = size * 0.5 };
            double[] seconds = { 0.55, 0.8, 0.45, 0.7 };
            double[] low = { 0.3, 0.2, 0.4, 0.25 };
            for (int i = 0; i < seconds.Length; i++)
            {
                var scale = new ScaleTransform(1, 1);
                var bar = new Border { Width = Math.Max(3, size * 0.075), Margin = new Thickness(size * 0.03, 0, size * 0.03, 0), Background = Accent, CornerRadius = new CornerRadius(2), RenderTransformOrigin = new Point(0.5, 1), RenderTransform = scale };
                var bounce = new System.Windows.Media.Animation.DoubleAnimation(low[i], 1, TimeSpan.FromSeconds(seconds[i]))
                {
                    AutoReverse = true,
                    RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                    EasingFunction = new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut },
                };
                Utils.LoadedAnimation.Run(bar, scale, ScaleTransform.ScaleYProperty, bounce);   // stops when the list is redrawn
                row.Children.Add(bar);
            }
            return row;
        }

        private static BitmapImage LoadCover(string path, int decodeWidth)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = decodeWidth;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("FocusMusicView cover - " + ex.Message);
                return null;
            }
        }

        // ------------------------------------------------------------------ track list events

        private void Tracks_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null) foreach (FocusMusicTrack track in e.NewItems) track.PropertyChanged += Track_PropertyChanged;
            if (e.OldItems != null) foreach (FocusMusicTrack track in e.OldItems) track.PropertyChanged -= Track_PropertyChanged;
            Dispatcher.BeginInvoke(new Action(QueueRender));
        }

        private void Track_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(FocusMusicTrack.ElapsedFormatted):
                case nameof(FocusMusicTrack.PausedOffset):
                    return;
                case nameof(FocusMusicTrack.DownloadProgress):
                    Dispatcher.BeginInvoke(new Action(() => UpdateDownloadProgress((FocusMusicTrack)sender)));
                    return;
                default:
                    Dispatcher.BeginInvoke(new Action(QueueRender));
                    return;
            }
        }

        private void UpdateDownloadProgress(FocusMusicTrack track)
        {
            foreach (var progress in Descendants(_body).OfType<StackPanel>().Where(p => p.Tag == track))
            {
                if (progress.Children.Count == 2 && progress.Children[0] is ProgressBar bar && progress.Children[1] is TextBlock percent)
                {
                    bar.Value = track.DownloadProgress;
                    percent.Text = track.DownloadProgress + "%";
                }
            }
        }

        private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                yield return child;
                foreach (var descendant in Descendants(child)) yield return descendant;
            }
        }

        // ------------------------------------------------------------------ library playback (unchanged behaviour)

        private void Player_MediaEnded(object sender, EventArgs e)
        {
            if (_currentTrack == null) return;
            StopTrack(_currentTrack);
            QueueRender();
        }

        private void PlaybackTimer_Tick(object sender, EventArgs e)
        {
            // Capture into a local: the tick only refreshes the current track's elapsed time.
            var track = _currentTrack;
            // Nothing playing: stop ticking (it ran 3 times a second from start-up, for nothing).
            if (track == null || !track.IsPlaying) { _playbackTimer.Stop(); return; }
            var elapsed = track.PausedOffset + (DateTime.Now - track.StartTime);
            track.ElapsedFormatted = elapsed.ToString(@"mm\:ss");
            UpdateNowPlayingTime();
        }

        private void PlayOrToggle(FocusMusicTrack track)
        {
            LogWriter.LogWrite("FocusMusicView() PlayOrToggle");
            if (track == null || !track.IsAudioDownloaded) return;
            // The user took charge of the music: a routine ending mustn't resume what they chose.
            appSettings.bFocusMusicPausedAutomatically = false;
            if (_currentTrack != track || (!track.IsPlaying && !track.IsPaused))
            {
                PlayNewTrack(track);
                return;
            }
            TogglePause(track);
        }

        private void TogglePause(FocusMusicTrack track)
        {
            appSettings.bFocusMusicPausedAutomatically = false;
            if (track.IsPlaying) PauseTrack(track);
            else ResumeTrack(track);
        }

        private void PlayNewTrack(FocusMusicTrack newTrack)
        {
            StopSpotify();
            StopYouTube();
            if (_currentTrack != null) StopTrack(_currentTrack);

            _currentTrack = newTrack;
            newTrack.PausedOffset = TimeSpan.Zero;
            newTrack.StartTime = DateTime.Now;
            newTrack.IsPlaying = true;
            _playbackTimer.Start();
            newTrack.IsPaused = false;

            if (!string.IsNullOrEmpty(newTrack.LocalAudioPath))
            {
                _player.Open(new Uri(newTrack.LocalAudioPath, UriKind.Absolute));
                _player.Volume = _volume.Value;
                _player.Position = TimeSpan.Zero;
                _player.Play();
            }
            UpdateTray();
        }

        public void PauseTrack(FocusMusicTrack track)
        {
            if (track == null) return;
            track.IsPlaying = false;
            track.IsPaused = true;
            track.PausedOffset += DateTime.Now - track.StartTime;
            _player.Pause();
            UpdateTray();
        }

        public void ResumeTrack(FocusMusicTrack track)
        {
            if (track == null) return;
            if (_currentTrack != track)
            {
                PlayNewTrack(track);
                return;
            }
            track.StartTime = DateTime.Now;
            track.IsPlaying = true;
            _playbackTimer.Start();
            track.IsPaused = false;
            _player.Position = track.PausedOffset;
            _player.Play();
            UpdateTray();
        }

        private void StopTrack(FocusMusicTrack track)
        {
            if (track == null) return;
            track.IsPlaying = false;
            track.IsPaused = false;
            track.PausedOffset = TimeSpan.Zero;
            track.ElapsedFormatted = "00:00";
            _player.Stop();
            UpdateTray();
        }

        private void StopLocal()
        {
            var track = appSettings.arrFocusMusicTracks.FirstOrDefault(t => t.IsPlaying || t.IsPaused) ?? _currentTrack;
            if (track != null) StopTrack(track);
            _currentTrack = null;
        }

        private void UpdateTray()
        {
            try { ((App)Application.Current)?.wndTrayMenu?.UpdateTrayMenu(); }
            catch (Exception ex) { LogWriter.LogWrite("FocusMusicView tray - " + ex.Message); }
        }

        public async Task DownloadAudioWithProgressAsync(FocusMusicTrack track)
        {
            LogWriter.LogWrite("FocusMusicView() DownloadAudio invoked");

            if (track == null || track.IsAudioDownloaded || track.IsDownloading || string.IsNullOrWhiteSpace(track.ServerAudioUrl))
                return;

            string trackFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FocusBear.Core.AppInstance.DataFolderName, "FocusMusic", "Tracks");
            Directory.CreateDirectory(trackFolder);

            string fileName = $"{track.Id}_audio{Path.GetExtension(track.ServerAudioUrl).Split('?')[0]}";
            track.LocalAudioPath = Path.Combine(trackFolder, fileName);
            string tempFile = Path.Combine(trackFolder, "temp_" + fileName);

            if (File.Exists(tempFile))
                File.Delete(tempFile);

            track.IsDownloading = true;
            track.DownloadProgress = 0;
            long? totalBytes = null;
            try
            {
                using (var client = new HttpClient())
                using (var response = await client.GetAsync(track.ServerAudioUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    totalBytes = response.Content.Headers.ContentLength ?? -1;

                    using (var inputStream = await response.Content.ReadAsStreamAsync())
                    using (var outputStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        byte[] buffer = new byte[81920];
                        int bytesRead;
                        long totalRead = 0;
                        while ((bytesRead = await inputStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await outputStream.WriteAsync(buffer, 0, bytesRead);
                            totalRead += bytesRead;
                            if (totalBytes > 0)
                                track.DownloadProgress = (int)((double)totalRead / totalBytes * 100);
                        }
                        await outputStream.FlushAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                track.IsDownloading = false;
                track.DownloadProgress = 0;
                track.LocalAudioPath = null;
                MessageBox.Show($"Error downloading file: {ex.Message}");
                return;
            }

            var fileInfo = new FileInfo(tempFile);
            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                MessageBox.Show("Download failed or corrupted file.");
                track.LocalAudioPath = null;
                track.IsDownloading = false;
                return;
            }

            if (totalBytes != null && totalBytes > 0 && totalBytes != fileInfo.Length)
            {
                MessageBox.Show("Incomplete download. Retrying...");
                track.LocalAudioPath = null;
                track.IsDownloading = false;
                return;
            }

            await Task.Delay(50);
            // Move the temp file into place only after every stream is closed, and only then call it downloaded.
            if (File.Exists(track.LocalAudioPath)) File.Delete(track.LocalAudioPath);
            File.Move(tempFile, track.LocalAudioPath);
            track.IsDownloading = false;
            track.IsAudioDownloaded = true;
        }

        private void ApplyVolume()
        {
            _player.Volume = _volume.Value;
            _youtube.SetVolume(_volume.Value);
            _volumeValue.Text = (int)Math.Round(_volume.Value * 100) + "%";
        }

        // ------------------------------------------------------------------ Spotify

        private void AddSpotify()
        {
            string input = _spotifyInput.Text;
            if (string.IsNullOrWhiteSpace(input)) return;
            var link = SpotifyLink.Parse(input);
            if (link == null)
            {
                ShowError(_spotifyError, _s.SpotifyInvalid);
                return;
            }
            string url = input.Trim();
            _links = _links.Add(new CustomMusicLink(CustomMusicSource.Spotify, link.Uri, CustomMusicNames.SpotifyPlaceholder(_s.Spotify, link.Kind), url));
            CustomMusicLinkStore.Save(_links);
            _spotifyInput.Text = "";
            if (!SpotifyDesktop.IsInstalled()) ShowError(_spotifyError, _s.SpotifyNotInstalled);
            Render();
            ResolveSpotifyTitle(link.Uri, url.StartsWith("spotify:", StringComparison.Ordinal) ? link.WebUrl : url);
        }

        private async void ResolveSpotifyTitle(string id, string url)
        {
            string title = await CustomMusicLinkStore.FetchSpotifyTitle(url);
            if (title == null) return;
            _links = CustomMusicLinkStore.Load().Rename(CustomMusicSource.Spotify, id, title);
            CustomMusicLinkStore.Save(_links);
            QueueRender();
        }

        private void ToggleSpotify(CustomMusicLink link)
        {
            if (_spotifyPlayingId == link.Id)
            {
                StopSpotify();
                Render();
                return;
            }
            StopLocal();
            StopYouTube();
            if (!SpotifyDesktop.Open(SpotifyLink.Parse(link.Id), out bool inBrowser)) return;
            _spotifyPlayingId = link.Id;
            _spotifyPaused = false;
            _spotifyInBrowser = inBrowser;
            Render();
        }

        private void ToggleSpotifyPause()
        {
            if (_spotifyPlayingId == null || _spotifyInBrowser) return;
            _spotifyPaused = !_spotifyPaused;
            if (_spotifyPaused) SpotifyDesktop.Pause();
            else SpotifyDesktop.Resume();
            QueueRender();
        }

        private void StopSpotify()
        {
            if (_spotifyPlayingId == null) return;
            if (!_spotifyInBrowser) SpotifyDesktop.Pause();
            _spotifyPlayingId = null;
            _spotifyPaused = false;
            QueueRender();
        }

        // ------------------------------------------------------------------ YouTube Music

        private void AddYouTube()
        {
            string input = _youtubeInput.Text;
            if (string.IsNullOrWhiteSpace(input)) return;
            var link = YouTubeMusicLink.Parse(input);
            if (link == null)
            {
                ShowError(_youtubeError, _s.YouTubeInvalid);
                return;
            }
            _links = _links.Add(new CustomMusicLink(CustomMusicSource.YouTubeMusic, link.Id, CustomMusicNames.YouTubePlaceholder(_s.YouTubeMusic, link.Kind), input.Trim(), link.Kind));
            CustomMusicLinkStore.Save(_links);
            _youtubeInput.Text = "";
            Render();
            ResolveYouTubeTitle(link.Id, link.Kind);
        }

        private async void ResolveYouTubeTitle(string id, YouTubeMusicKind kind)
        {
            string title = await CustomMusicLinkStore.FetchYouTubeTitle(id, kind);
            if (title == null) return;
            _links = CustomMusicLinkStore.Load().Rename(CustomMusicSource.YouTubeMusic, id, title);
            CustomMusicLinkStore.Save(_links);
            QueueRender();
        }

        /// <summary>A mix's name only exists once a song plays: take the player's title, but never over a real one.</summary>
        private void ResolvePlayingYouTubeTitle(string title, string author)
        {
            var link = _youtube.PlayingId == null ? null : _links.Find(CustomMusicSource.YouTubeMusic, _youtube.PlayingId);
            string resolved = CustomMusicNames.FromPlayer(title, author);
            if (link == null || resolved == null || !CustomMusicNames.IsYouTubePlaceholder(link.Name, _s.YouTubeMusic)) return;
            _links = _links.Rename(CustomMusicSource.YouTubeMusic, link.Id, resolved);
            CustomMusicLinkStore.Save(_links);
            QueueRender();
        }

        /// <summary>Links added before a title could be found get another try each launch (cheap, best effort).</summary>
        private void BackfillLinkTitles()
        {
            foreach (var link in _links.Of(CustomMusicSource.Spotify).Where(l => l.Name.Contains("http") || l.Name.StartsWith(_s.Spotify, StringComparison.Ordinal)).ToList())
            {
                var parsed = SpotifyLink.Parse(link.Id);
                if (parsed != null) ResolveSpotifyTitle(link.Id, parsed.WebUrl);
            }
            foreach (var link in _links.Of(CustomMusicSource.YouTubeMusic).Where(l => CustomMusicNames.IsYouTubePlaceholder(l.Name, _s.YouTubeMusic)).ToList())
                ResolveYouTubeTitle(link.Id, link.Kind);
        }

        private void ToggleYouTube(CustomMusicLink link)
        {
            if (_youtube.PlayingId == link.Id)
            {
                StopYouTube();
                Render();
                return;
            }
            StopLocal();
            StopSpotify();
            _youtube.Play(link.Id, link.Kind, _volume.Value);
            Render();
        }

        private void StopYouTube()
        {
            if (_youtube.PlayingId == null) return;
            _youtube.Stop();
            QueueRender();
        }

        private void RemoveLink(CustomMusicLink link)
        {
            if (link.Source == CustomMusicSource.Spotify && _spotifyPlayingId == link.Id) StopSpotify();
            if (link.Source == CustomMusicSource.YouTubeMusic && _youtube.PlayingId == link.Id) StopYouTube();
            _links = _links.Remove(link.Source, link.Id);
            CustomMusicLinkStore.Save(_links);
            Render();
        }

        private static void ShowError(TextBlock block, string message)
        {
            block.Text = message;
            block.Visibility = Visibility.Visible;
        }

        // ------------------------------------------------------------------ routines

        /// <summary>A routine is starting: pause the added-link music we're playing. True when something was paused.</summary>
        public bool PauseLinkMusicForRoutine()
        {
            if (_youtube.PlayingId != null && !_youtube.IsPaused)
            {
                _youtube.SetPaused(true);
                QueueRender();
                return true;
            }
            if (_spotifyPlayingId != null && !_spotifyPaused && !_spotifyInBrowser)
            {
                ToggleSpotifyPause();
                return true;
            }
            return false;
        }

        /// <summary>The routine is over: resume the added-link music <see cref="PauseLinkMusicForRoutine"/> paused.</summary>
        public void ResumeLinkMusicAfterRoutine()
        {
            if (_youtube.PlayingId != null && _youtube.IsPaused)
            {
                _youtube.SetPaused(false);
                QueueRender();
            }
            else if (_spotifyPlayingId != null && _spotifyPaused)
            {
                ToggleSpotifyPause();
            }
        }

        // ------------------------------------------------------------------ building blocks

        private void ApplyAppFont()
        {
            string font = appSettings.sSelectedAppFont;
            if (string.IsNullOrEmpty(font)) return;
            if (font == "Poppins" || font == "IntelOne Mono" || font == "OpenDyslexic3 Regular")
                FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", font));
            else
                FontFamily = new FontFamily(font);
        }

        private static Brush Frozen(SolidColorBrush brush)
        {
            brush.Freeze();
            return brush;
        }

        private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        private static TextBlock Text(string text, double size, FontWeight? weight = null, bool secondary = false, bool wrap = true)
        {
            var block = new TextBlock
            {
                Text = text ?? "",
                FontSize = size,
                FontWeight = weight ?? FontWeights.Normal,
                TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
                TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis,
            };
            block.SetResourceReference(TextBlock.ForegroundProperty, secondary ? "TraySubtitleColor" : "ForegroundColor");
            return block;
        }

        private static TextBlock Glyph(int code, double size, string colorKey)
        {
            var block = new TextBlock { Text = ((char)code).ToString(), FontFamily = Symbols, FontSize = size, VerticalAlignment = VerticalAlignment.Center };
            block.SetResourceReference(TextBlock.ForegroundProperty, colorKey);
            return block;
        }

        private static Border Card(UIElement child, Thickness padding, double radius)
        {
            var card = new Border { Child = child, Padding = padding, CornerRadius = new CornerRadius(radius), BorderThickness = new Thickness(1) };
            card.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
            card.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            return card;
        }

        /// <summary>A service's round badge: its brand colour behind a white glyph.</summary>
        private static FrameworkElement Badge(Brush color, int glyph, double size = 28)
        {
            var grid = new Grid { Width = size, Height = size };
            grid.Children.Add(new Ellipse { Fill = color });
            grid.Children.Add(new TextBlock { Text = ((char)glyph).ToString(), FontFamily = Symbols, FontSize = size * 0.45, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            return grid;
        }

        private static ControlTemplate PlainTemplate()
        {
            var template = new ControlTemplate(typeof(ButtonBase));
            template.VisualTree = new FrameworkElementFactory(typeof(ContentPresenter));
            return template;
        }

        private static Button Clickable(UIElement content, Action onClick, string automationId, string tooltip)
        {
            if (content is Panel panel && panel.Background == null) panel.Background = Brushes.Transparent;
            else if (content is Border border && border.Background == null) border.Background = Brushes.Transparent;
            var button = new Button { Content = content, Template = PlainTemplate(), Cursor = Cursors.Hand, Background = Brushes.Transparent, ToolTip = tooltip, FocusVisualStyle = null };
            button.Click += (s, e) => onClick();
            if (automationId != null) AutomationProperties.SetAutomationId(button, automationId);
            if (tooltip != null) AutomationProperties.SetName(button, tooltip);
            return button;
        }

        /// <summary>A round play / pause / stop button: filled orange for the main action, outlined otherwise.</summary>
        private static Button CircleButton(int glyph, bool filled, double size, string tooltip, Action onClick, string automationId)
        {
            var circle = new Ellipse { StrokeThickness = filled ? 0 : 1.2 };
            if (filled) circle.Fill = Accent;
            else
            {
                circle.SetResourceReference(Shape.StrokeProperty, "TrayCardBorder");
                circle.SetResourceReference(Shape.FillProperty, "TrayCardBg");
            }
            var icon = new TextBlock { Text = ((char)glyph).ToString(), FontFamily = Symbols, FontSize = size * 0.36, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            // The play triangle sits optically left of centre in its glyph box.
            if (glyph == GlyphPlay) icon.Margin = new Thickness(size * 0.06, 0, 0, 0);
            if (filled) icon.Foreground = Brushes.White;
            else icon.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            var grid = new Grid { Width = size, Height = size };
            grid.Children.Add(circle);
            grid.Children.Add(icon);
            var button = Clickable(grid, onClick, automationId, tooltip);
            button.MouseEnter += (s, e) => grid.Opacity = 0.85;
            button.MouseLeave += (s, e) => grid.Opacity = 1;
            return button;
        }

        private static Button IconButton(int glyph, string tooltip, Action onClick, string automationId)
        {
            var icon = Glyph(glyph, 14, "TraySubtitleColor");
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            var face = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Child = icon, Background = Brushes.Transparent };
            var button = Clickable(face, onClick, automationId, tooltip);
            Hover(button, face);
            return button;
        }

        private static Button PillButton(string title, Action onClick, string automationId)
        {
            var label = new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            var face = new Border { Child = label, Background = Accent, CornerRadius = new CornerRadius(16), Padding = new Thickness(18, 7, 18, 7) };
            var button = Clickable(face, onClick, automationId, null);
            button.MouseEnter += (s, e) => face.Opacity = 0.88;
            button.MouseLeave += (s, e) => face.Opacity = 1;
            return button;
        }

        private static TextBox Input(string automationId)
        {
            var box = new TextBox { FontSize = 13, Padding = new Thickness(8, 7, 8, 7), BorderThickness = new Thickness(1), VerticalContentAlignment = VerticalAlignment.Center };
            box.SetResourceReference(BackgroundProperty, "BackgroundColor");
            box.SetResourceReference(ForegroundProperty, "ForegroundColor");
            box.SetResourceReference(BorderBrushProperty, "TrayCardBorder");
            box.SetResourceReference(TextBoxBase.CaretBrushProperty, "ForegroundColor");
            AutomationProperties.SetAutomationId(box, automationId);
            return box;
        }

        /// <summary>A soft highlight under the pointer.</summary>
        private static void Hover(UIElement target, Border face)
        {
            var rest = face.Background;
            target.MouseEnter += (s, e) => face.SetResourceReference(Border.BackgroundProperty, "TrayCardBorder");
            target.MouseLeave += (s, e) => face.Background = rest;
        }

        private static bool IsInsideButton(DependencyObject element, DependencyObject stop)
        {
            for (var node = element; node != null && node != stop; node = VisualTreeHelper.GetParent(node))
                if (node is ButtonBase) return true;
            return false;
        }

        /// <summary>The screen's strings: the Mac's <c>focusMusicInfo</c> / <c>allTracksVcInfo</c> keys, with English fallbacks.</summary>
        private sealed class Strings
        {
            private readonly AppSettings _settings = AppSettings.GetInstance;

            private string Get(string fallback, params string[] path)
            {
                try { return _settings.ConfigFileStrings == null ? fallback : _settings.GetConfigStringSafe(fallback, path); }
                catch { return fallback; }
            }

            internal string Title => Get("Focus Music", "leftMenuVcInfo", "menuFocusMusic");
            internal string Description => Get("Here are some focus music tracks (some are binaural beats) you can use to help you focus.", "allTracksVcInfo", "title");
            internal string Volume => Get("Volume:", "allTracksVcInfo", "titleVolumeText").TrimEnd(':', ' ');
            internal string Download => Get("Download", "allTracksVcInfo", "downloadButtonText");
            internal string NowPlaying => Get("Now Playing", "focusMusicInfo", "nowPlayingLabelText");
            internal string Spotify => Get("Spotify", "focusMusicInfo", "sectionTitleSpotify");
            internal string YouTubeMusic => Get("YouTube Music", "focusMusicInfo", "sectionTitleYouTubeMusic");
            internal string Library => Get("Focus Music Library", "focusMusicInfo", "sectionTitleLibrary");
            internal string SpotifyPlaceholder => Get("Paste a Spotify track/album/playlist link", "focusMusicInfo", "spotifyAddPlaceholder");
            internal string YouTubePlaceholder => Get("Paste a music.youtube.com link", "focusMusicInfo", "youtubeAddPlaceholder");
            internal string Add => Get("Add", "focusMusicInfo", "addButtonText");
            internal string YouTubeHint => Get("Add a music.youtube.com video or playlist link to play it here.", "focusMusicInfo", "youtubeHintText");
            internal string AddOwnMusic => Get("Add my own music", "focusMusicInfo", "addOwnMusicButtonText");
            internal string SpotifyInvalid => Get("That doesn't look like a Spotify track/album/playlist link.", "focusMusicInfo", "spotifyInvalidLinkError");
            internal string YouTubeInvalid => Get("That doesn't look like a music.youtube.com link.", "focusMusicInfo", "youtubeInvalidLinkError");
            // The shared key's text names "this Mac", and Windows opens the web player instead of refusing: its own keys.
            internal string SpotifyNotInstalled => Get("Spotify isn't installed on this PC, so this will open in the Spotify web player.", "focusMusicInfo", "spotifyNotInstalledErrorWindows");
            internal string SpotifyOpenedInApp => Get("Playing in the Spotify app", "focusMusicInfo", "spotifyOpenedInAppText");
            internal string SpotifyOpenedInBrowser => Get("Opened in your browser", "focusMusicInfo", "spotifyOpenedInBrowserText");
            internal string LibraryEmpty => Get("The focus music library is loading…", "focusMusicInfo", "libraryEmptyText");
            internal string Play => Get("Play", "focusMusicInfo", "playText");
            internal string Pause => Get("Pause", "focusMusicInfo", "pauseText");
            internal string Stop => Get("Stop", "focusMusicInfo", "stopText");
            internal string Mute => Get("Mute", "focusMusicInfo", "muteText");
            internal string Unmute => Get("Unmute", "focusMusicInfo", "unmuteText");
            internal string Remove => Get("Remove", "focusMusicInfo", "removeText");
        }
    }
}
