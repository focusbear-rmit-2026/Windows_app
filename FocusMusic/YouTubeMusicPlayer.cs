using Focus_Bear.Utils;
using FocusBear.Core.FocusMusic;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Focus_Bear.FocusMusic
{
    /// <summary>
    /// The embedded YouTube Music player — the Mac's <c>YouTubeMusicPlayerView</c>: YouTube's IFrame Player API in a
    /// deliberately tiny web view (it confirms something is playing; it isn't somewhere to watch YouTube). The page is
    /// served from an https origin through <see cref="CoreWebView2.WebResourceRequested"/> so YouTube accepts the embed,
    /// and the app drives volume / mute / pause through the page's <c>fbSet…</c> functions.
    /// </summary>
    internal sealed class YouTubeMusicPlayer : Border
    {
        private readonly WebView2 _web = new WebView2 { DefaultBackgroundColor = System.Drawing.Color.Black };
        private readonly RetryableStart _ready;
        private string _html;
        private int _volume = 60;
        private bool _paused, _muted;

        /// <summary>(title, author) once a video starts — the only way to name an "RD…" radio mix.</summary>
        internal event Action<string, string> TrackInfo;

        internal YouTubeMusicPlayer()
        {
            Background = Brushes.Black;
            CornerRadius = new CornerRadius(6);
            ClipToBounds = true;
            Child = _web;
            // A failed WebView2 start is tried again on the next Play, not kept for good.
            _ready = new RetryableStart(Initialize);
        }

        internal string PlayingId { get; private set; }

        internal void Play(string id, YouTubeMusicKind kind, double volume)
        {
            _volume = Percent(volume);
            _paused = false;
            _html = YouTubeMusicPlayerPage.Html(id, kind, _volume);
            if (_html == null) return;
            PlayingId = id;
            Load();
        }

        internal void Stop()
        {
            PlayingId = null;
            _html = null;
            try { _web.CoreWebView2?.Navigate("about:blank"); }
            catch (Exception ex) { LogWriter.LogWrite("YouTubeMusicPlayer.Stop - " + ex.Message); }
        }

        internal void SetVolume(double volume)
        {
            _volume = Percent(volume);
            Run("fbSetVolume(" + _volume + ");");
        }

        internal bool IsPaused => _paused;

        internal void SetPaused(bool paused)
        {
            _paused = paused;
            Run("fbSetPaused(" + (paused ? "true" : "false") + ");");
        }

        internal bool IsMuted => _muted;

        internal void SetMuted(bool muted)
        {
            _muted = muted;
            Run("fbSetMuted(" + (muted ? "true" : "false") + ");");
        }

        private static int Percent(double volume) => (int)Math.Round(Math.Max(0, Math.Min(1, volume)) * 100);

        private async void Load()
        {
            try
            {
                await EnsureReady();
                if (_html == null || _web.CoreWebView2 == null) return;
                _web.CoreWebView2.Navigate(YouTubeMusicPlayerPage.Url);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("YouTubeMusicPlayer.Load - " + ex.Message);
            }
        }

        private Task EnsureReady() => _ready.EnsureStarted();

        private async Task Initialize()
        {
            // A retry after the control did come up (the failure was later) must not start it a second time.
            if (_web.CoreWebView2 != null) return;
            // Its own profile, so the player's cookies never mix with the dashboard web views — and its own
            // browser process, which is what lets it autoplay without a click inside the page (the Play click
            // happened in WPF; the Mac clears WebKit's user-gesture requirement for the same reason).
            var options = new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required");
            var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(AppSettings.AppDataDirPath, "FBFocusMusicWebView2"), options);
            await _web.EnsureCoreWebView2Async(environment);
            var core = _web.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.AddWebResourceRequestedFilter(YouTubeMusicPlayerPage.Url + "*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (s, e) =>
            {
                if (!e.Request.Uri.StartsWith(YouTubeMusicPlayerPage.Url, StringComparison.Ordinal)) return;
                var body = new MemoryStream(Encoding.UTF8.GetBytes(_html ?? "<!DOCTYPE html><html><body style=\"background:#000\"></body></html>"));
                e.Response = core.Environment.CreateWebResourceResponse(body, 200, "OK", "Content-Type: text/html; charset=utf-8");
            };
            // The page is the only top-level document: YouTube links in the player never take it over.
            core.NavigationStarting += (s, e) =>
            {
                if (!e.Uri.StartsWith(YouTubeMusicPlayerPage.Url, StringComparison.Ordinal) && e.Uri != "about:blank") e.Cancel = true;
            };
            core.NewWindowRequested += (s, e) => e.Handled = true;
            core.WebMessageReceived += OnMessage;
            core.NavigationCompleted += (s, e) =>
            {
                if (_muted) SetMuted(true);
            };
        }

        private void OnMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var message = JObject.Parse(e.WebMessageAsJson);
                string type = (string)message["type"];
                if (type == "trackInfo") TrackInfo?.Invoke((string)message["title"], (string)message["author"]);
                else if (type == "error") LogWriter.LogWrite("YouTubeMusicPlayer error " + message["code"]);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("YouTubeMusicPlayer message - " + ex.Message);
            }
        }

        private async void Run(string script)
        {
            try
            {
                if (_web.CoreWebView2 == null || PlayingId == null) return;
                await _web.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("YouTubeMusicPlayer script - " + ex.Message);
            }
        }

        /// <summary>The player's state for tests and diagnostics: YT.PlayerState, or null before it's ready.</summary>
        internal async Task<string> StateAsync()
        {
            if (_web.CoreWebView2 == null) return null;
            return await _web.CoreWebView2.ExecuteScriptAsync("(window.fbPlayerReady && fbPlayer.getPlayerState) ? fbPlayer.getPlayerState() : null");
        }
    }
}
