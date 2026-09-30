using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.FocusMusic
{
    /// <summary>
    /// A pasted Spotify link (web URL or <c>spotify:</c> URI) reduced to its <c>spotify:&lt;kind&gt;:&lt;id&gt;</c> URI.
    /// Port of the Mac's <c>SpotifyLinkParser</c>: the id is base62, so the URI is always safe to hand to the shell.
    /// </summary>
    public sealed class SpotifyLink
    {
        private static readonly HashSet<string> SupportedKinds = new HashSet<string> { "track", "album", "playlist", "episode", "show" };

        private SpotifyLink(string kind, string id)
        {
            Kind = kind;
            Id = id;
        }

        public string Kind { get; }
        public string Id { get; }

        /// <summary><c>spotify:track:…</c> — what the Spotify desktop app opens, and the link's unique id.</summary>
        public string Uri => "spotify:" + Kind + ":" + Id;

        /// <summary>The same item on the Spotify web player, for PCs without the desktop app.</summary>
        public string WebUrl => "https://open.spotify.com/" + Kind + "/" + Id;

        public static SpotifyLink Parse(string input)
        {
            string trimmed = (input ?? "").Trim();
            if (trimmed.Length == 0) return null;

            if (trimmed.StartsWith("spotify:", StringComparison.Ordinal))
            {
                var parts = trimmed.Split(':');
                if (parts.Length != 3 || !SupportedKinds.Contains(parts[1]) || !IsValidId(parts[2])) return null;
                return new SpotifyLink(parts[1], parts[2]);
            }

            if (!FocusBear.Core.SafeUri.TryCreate(trimmed, UriKind.Absolute, out var url)) return null;
            if (url.Scheme != System.Uri.UriSchemeHttps && url.Scheme != System.Uri.UriSchemeHttp) return null;
            if (!url.Host.EndsWith("spotify.com", StringComparison.OrdinalIgnoreCase)) return null;
            var segments = url.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2) return null;
            string kind = segments[segments.Length - 2];
            string id = segments[segments.Length - 1];
            if (!SupportedKinds.Contains(kind) || !IsValidId(id)) return null;
            return new SpotifyLink(kind, id);
        }

        /// <summary><c>spotify:track:ID</c> → its kind, or null when it isn't one.</summary>
        public static string KindOf(string uriOrUrl) => Parse(uriOrUrl)?.Kind;

        /// <summary>Spotify's public oEmbed endpoint (no auth), which answers <c>{"title": …}</c> for the link.</summary>
        public static string OEmbedUrl(string link) => "https://open.spotify.com/oembed?url=" + System.Uri.EscapeDataString(link ?? "");

        private static bool IsValidId(string id) => id.Length > 0 && id.All(c => c < 128 && char.IsLetterOrDigit(c));
    }

    public enum YouTubeMusicKind
    {
        Video,
        Playlist,
    }

    /// <summary>
    /// A pasted <b>music.youtube.com</b> link — a video (<c>watch?v=</c>, <c>/embed/</c>) or a playlist (<c>list=</c>,
    /// including the "RD…" radio mixes YouTube Music's Share button makes). Port of the Mac's
    /// <c>YouTubeMusicLinkParser</c>: plain youtube.com / youtu.be links are refused on purpose — this is focus
    /// music, not "watch any YouTube video". Ids are validated, so they are safe to put in the player page.
    /// </summary>
    public sealed class YouTubeMusicLink
    {
        private YouTubeMusicLink(string id, YouTubeMusicKind kind)
        {
            Id = id;
            Kind = kind;
        }

        public string Id { get; }
        public YouTubeMusicKind Kind { get; }

        public static YouTubeMusicLink Parse(string input)
        {
            string trimmed = (input ?? "").Trim();
            if (trimmed.Length == 0 || !FocusBear.Core.SafeUri.TryCreate(trimmed, UriKind.Absolute, out var url)) return null;
            if (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp) return null;
            string host = url.Host.ToLowerInvariant();
            if (host != "music.youtube.com" && !host.EndsWith(".music.youtube.com", StringComparison.Ordinal)) return null;

            var query = Query(url);
            if (query.TryGetValue("v", out var v) && IsValidVideoId(v)) return new YouTubeMusicLink(v, YouTubeMusicKind.Video);
            if (url.AbsolutePath.StartsWith("/embed/", StringComparison.Ordinal))
            {
                string last = url.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                if (last != null && IsValidVideoId(last)) return new YouTubeMusicLink(last, YouTubeMusicKind.Video);
            }
            if (query.TryGetValue("list", out var list) && IsValidPlaylistId(list)) return new YouTubeMusicLink(list, YouTubeMusicKind.Playlist);
            return null;
        }

        /// <summary>The youtube.com page for the id — what oEmbed and the og:title fallback understand.</summary>
        public static string PageUrl(string id, YouTubeMusicKind kind) =>
            kind == YouTubeMusicKind.Playlist ? "https://www.youtube.com/playlist?list=" + id : "https://www.youtube.com/watch?v=" + id;

        public static string OEmbedUrl(string id, YouTubeMusicKind kind) =>
            "https://www.youtube.com/oembed?url=" + Uri.EscapeDataString(PageUrl(id, kind)) + "&format=json";

        private static Dictionary<string, string> Query(Uri url)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in url.Query.TrimStart('?').Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = pair.IndexOf('=');
                string name = Uri.UnescapeDataString(eq < 0 ? pair : pair.Substring(0, eq));
                string value = eq < 0 ? "" : Uri.UnescapeDataString(pair.Substring(eq + 1));
                if (!result.ContainsKey(name)) result[name] = value;
            }
            return result;
        }

        private static bool IsIdChar(char c) => c < 128 && (char.IsLetterOrDigit(c) || c == '_' || c == '-');

        internal static bool IsValidVideoId(string id) => id != null && id.Length == 11 && id.All(IsIdChar);

        /// <summary>"PL…" playlists are ~34 characters and "RD…" mixes longer: only the characters are checked.</summary>
        internal static bool IsValidPlaylistId(string id) => !string.IsNullOrEmpty(id) && id.All(IsIdChar);
    }
}
