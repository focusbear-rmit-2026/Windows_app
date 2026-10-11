using System;
using System.Globalization;

namespace FocusBear.Core.FocusMusic
{
    /// <summary>The page the embedded YouTube Music player loads — the Mac's <c>YouTubeMusicPlayerView</c> HTML.</summary>
    public static class YouTubeMusicPlayerPage
    {
        /// <summary>
        /// Served from this https origin (never loaded as a bare string): YouTube's embed refuses to play for a
        /// page with no origin / referrer ("Error 153"), which is why the Mac loads it with a base URL too.
        /// </summary>
        public const string Url = "https://dashboard.focusbear.io/windows-focus-music-player";

        /// <summary>
        /// YouTube's IFrame Player API around one video or playlist, autoplaying at <paramref name="volumePercent"/>.
        /// It posts <c>{type:"trackInfo",title,author}</c> once a video plays, and exposes
        /// <c>fbSetVolume / fbSetMuted / fbSetPaused</c> for the app. Null when the id isn't a valid one.
        /// </summary>
        public static string Html(string id, YouTubeMusicKind kind, int volumePercent)
        {
            bool valid = kind == YouTubeMusicKind.Playlist ? YouTubeMusicLink.IsValidPlaylistId(id) : YouTubeMusicLink.IsValidVideoId(id);
            if (!valid) return null;
            int volume = Math.Max(0, Math.Min(100, volumePercent));
            string videoIdLine = kind == YouTubeMusicKind.Video ? "videoId: '" + id + "'," : "";
            string playlistVars = kind == YouTubeMusicKind.Playlist ? "listType: 'playlist', list: '" + id + "'," : "";
            return "<!DOCTYPE html>\n<html><head><meta charset=\"utf-8\"><style>\n" +
                   "html,body{margin:0;padding:0;background:#000;overflow:hidden;width:100%;height:100%;}\n" +
                   "#player{width:100%;height:100%;}\n</style></head>\n<body>\n<div id=\"player\"></div>\n<script>\n" +
                   "var tag = document.createElement('script');\n" +
                   "tag.src = 'https://www.youtube.com/iframe_api';\n" +
                   "document.body.appendChild(tag);\n" +
                   "var fbPlayer; var fbVolume = " + volume.ToString(CultureInfo.InvariantCulture) + ";\n" +
                   "function fbPost(m) { try { window.chrome.webview.postMessage(m); } catch (e) {} }\n" +
                   "function onYouTubeIframeAPIReady() {\n" +
                   "  fbPlayer = new YT.Player('player', {\n" +
                   "    height: '100%', width: '100%',\n" +
                   "    " + videoIdLine + "\n" +
                   "    playerVars: { autoplay: 1, playsinline: 1, controls: 1, " + playlistVars + " },\n" +
                   "    events: {\n" +
                   "      'onReady': function (event) { window.fbPlayerReady = true; event.target.setVolume(fbVolume); event.target.playVideo(); },\n" +
                   "      'onStateChange': function (event) {\n" +
                   "        fbPost({ type: 'state', state: event.data });\n" +
                   "        if (event.data === 1 && fbPlayer.getVideoData) {\n" +
                   "          try { var d = fbPlayer.getVideoData(); if (d && d.title) fbPost({ type: 'trackInfo', title: d.title, author: d.author || '' }); } catch (e) {}\n" +
                   "        }\n" +
                   "      },\n" +
                   "      'onError': function (event) { fbPost({ type: 'error', code: event.data }); }\n" +
                   "    }\n" +
                   "  });\n" +
                   "}\n" +
                   "function fbSetVolume(v) { fbVolume = v; if (window.fbPlayerReady && fbPlayer.setVolume) fbPlayer.setVolume(v); }\n" +
                   "function fbSetMuted(m) { if (window.fbPlayerReady) { if (m) fbPlayer.mute(); else fbPlayer.unMute(); } }\n" +
                   "function fbSetPaused(p) { if (window.fbPlayerReady) { if (p) fbPlayer.pauseVideo(); else fbPlayer.playVideo(); } }\n" +
                   "</script>\n</body></html>\n";
        }
    }

    /// <summary>Time labels for the Now Playing card.</summary>
    public static class FocusMusicTime
    {
        /// <summary>"4:05", or "1:02:09" past an hour.</summary>
        public static string Clock(TimeSpan elapsed)
        {
            if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
            int hours = (int)elapsed.TotalHours;
            return hours > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", hours, elapsed.Minutes, elapsed.Seconds)
                : string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", elapsed.Minutes, elapsed.Seconds);
        }

        /// <summary>How far through the track, 0…1; 0 when the length is unknown.</summary>
        public static double Fraction(TimeSpan elapsed, string durationSeconds)
        {
            if (!double.TryParse(durationSeconds, NumberStyles.Float, CultureInfo.InvariantCulture, out var total) || total <= 0) return 0;
            return Math.Max(0, Math.Min(1, elapsed.TotalSeconds / total));
        }
    }
}
