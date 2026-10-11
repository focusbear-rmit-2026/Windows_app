using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.FocusMusic
{
    public enum CustomMusicSource
    {
        Spotify,
        YouTubeMusic,
    }

    /// <summary>
    /// A link the user added on the Focus Music screen — the Mac's <c>SpotifyTrack</c> / <c>YouTubeMusicTrack</c>.
    /// <see cref="Id"/> is the <c>spotify:</c> URI, or the YouTube video / playlist id; a link can be added once.
    /// </summary>
    public sealed class CustomMusicLink
    {
        public CustomMusicLink(CustomMusicSource source, string id, string name, string url, YouTubeMusicKind kind = YouTubeMusicKind.Video)
        {
            Source = source;
            Id = id ?? "";
            Name = name ?? "";
            Url = url ?? "";
            Kind = kind;
        }

        public CustomMusicSource Source { get; }
        public string Id { get; }
        public string Name { get; }
        public string Url { get; }

        /// <summary>Video or playlist — YouTube Music links only.</summary>
        public YouTubeMusicKind Kind { get; }

        public CustomMusicLink WithName(string name) => new CustomMusicLink(Source, Id, name, Url, Kind);
    }

    /// <summary>
    /// The user's added Spotify and YouTube Music links, kept locally (never on the server, as on the Mac) and
    /// separate from the curated library, which the server replaces wholesale. Immutable: every edit returns a new list.
    /// </summary>
    public sealed class CustomMusicLinkList
    {
        private readonly List<CustomMusicLink> _links;

        public CustomMusicLinkList(IEnumerable<CustomMusicLink> links = null)
        {
            _links = (links ?? Enumerable.Empty<CustomMusicLink>()).Where(l => l != null && l.Id.Length > 0).ToList();
        }

        public IReadOnlyList<CustomMusicLink> All => _links;

        public IEnumerable<CustomMusicLink> Of(CustomMusicSource source) => _links.Where(l => l.Source == source);

        public CustomMusicLink Find(CustomMusicSource source, string id) => _links.FirstOrDefault(l => l.Source == source && l.Id == id);

        /// <summary>Appends <paramref name="link"/>; a link already in the list is left where it is.</summary>
        public CustomMusicLinkList Add(CustomMusicLink link)
        {
            if (link == null || link.Id.Length == 0 || Find(link.Source, link.Id) != null) return this;
            return new CustomMusicLinkList(_links.Concat(new[] { link }));
        }

        public CustomMusicLinkList Remove(CustomMusicSource source, string id) =>
            new CustomMusicLinkList(_links.Where(l => !(l.Source == source && l.Id == id)));

        public CustomMusicLinkList Rename(CustomMusicSource source, string id, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return this;
            return new CustomMusicLinkList(_links.Select(l => l.Source == source && l.Id == id ? l.WithName(name.Trim()) : l));
        }

        public string Serialize()
        {
            var array = new JArray(_links.Select(l => new JObject
            {
                ["source"] = l.Source == CustomMusicSource.Spotify ? "spotify" : "youtubeMusic",
                ["id"] = l.Id,
                ["name"] = l.Name,
                ["url"] = l.Url,
                ["kind"] = l.Kind == YouTubeMusicKind.Playlist ? "playlist" : "video",
            }));
            return array.ToString(Formatting.Indented);
        }

        /// <summary>Reads <see cref="Serialize"/>'s JSON; anything unreadable is an empty list, never an exception.</summary>
        public static CustomMusicLinkList Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new CustomMusicLinkList();
            try
            {
                var links = new List<CustomMusicLink>();
                foreach (var token in JArray.Parse(json).OfType<JObject>())
                {
                    string source = (string)token["source"];
                    var kind = (string)token["kind"] == "playlist" ? YouTubeMusicKind.Playlist : YouTubeMusicKind.Video;
                    if (source == "spotify") links.Add(new CustomMusicLink(CustomMusicSource.Spotify, (string)token["id"], (string)token["name"], (string)token["url"]));
                    else if (source == "youtubeMusic") links.Add(new CustomMusicLink(CustomMusicSource.YouTubeMusic, (string)token["id"], (string)token["name"], (string)token["url"], kind));
                }
                return new CustomMusicLinkList(links);
            }
            catch (JsonException)
            {
                return new CustomMusicLinkList();
            }
        }
    }

    /// <summary>Display names for added links — the Mac's placeholder-until-resolved naming.</summary>
    public static class CustomMusicNames
    {
        /// <summary>"Spotify playlist" until the oEmbed lookup answers with the real name.</summary>
        public static string SpotifyPlaceholder(string sourceLabel, string kind) =>
            string.IsNullOrEmpty(kind) ? sourceLabel : sourceLabel + " " + kind;

        public static string YouTubePlaceholder(string sourceLabel, YouTubeMusicKind kind) =>
            kind == YouTubeMusicKind.Playlist ? sourceLabel + " playlist" : sourceLabel;

        /// <summary>
        /// Whether a name is still generic, so the player's reported title may replace it (the only source of a
        /// title for "RD…" radio mixes). A real title is never overwritten as a mix moves on to the next song.
        /// </summary>
        public static bool IsYouTubePlaceholder(string name, string sourceLabel) =>
            string.IsNullOrWhiteSpace(name) || name == sourceLabel || name == sourceLabel + " playlist" || name.Contains("http");

        /// <summary>"Title — Author" as the embedded player reports it.</summary>
        public static string FromPlayer(string title, string author)
        {
            title = (title ?? "").Trim();
            author = (author ?? "").Trim();
            if (title.Length == 0) return null;
            return author.Length == 0 ? title : title + " — " + author;
        }
    }

    /// <summary>Title lookups for added links: parsing what Spotify / YouTube answer.</summary>
    public static class MusicTitleParser
    {
        /// <summary>The <c>title</c> of an oEmbed JSON answer, or null.</summary>
        public static string OEmbedTitle(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                string title = ((string)JObject.Parse(json)["title"])?.Trim();
                return string.IsNullOrEmpty(title) ? null : title;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// A YouTube page's <c>og:title</c> — the fallback for playlists oEmbed won't name. YouTube's own generic
        /// titles (a consent page, an unresolvable playlist) count as no title.
        /// </summary>
        public static string OgTitle(string html)
        {
            if (string.IsNullOrEmpty(html)) return null;
            int meta = html.IndexOf("<meta property=\"og:title\"", StringComparison.Ordinal);
            if (meta < 0) return null;
            int content = html.IndexOf("content=\"", meta, StringComparison.Ordinal);
            if (content < 0) return null;
            content += "content=\"".Length;
            int end = html.IndexOf('"', content);
            if (end < 0) return null;
            string title = DecodeEntities(html.Substring(content, end - content)).Trim();
            return title.Length == 0 || title == "YouTube" || title == "YouTube Music" ? null : title;
        }

        private static string DecodeEntities(string s) =>
            s.Replace("&quot;", "\"").Replace("&#39;", "'").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");
    }
}
