using Focus_Bear.Utils;
using FocusBear.Core.FocusMusic;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Focus_Bear.FocusMusic
{
    /// <summary>
    /// Where the user's added Spotify / YouTube Music links live: a JSON file beside the downloaded tracks, on this PC
    /// only (the Mac keeps them in UserDefaults), apart from the curated library the server replaces on every launch.
    /// Also resolves their titles through each service's public oEmbed endpoint (no account, no key).
    /// </summary>
    internal static class CustomMusicLinkStore
    {
        private static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            FocusBear.Core.AppInstance.DataFolderName, "FocusMusic", "custom-links.json");

        private static readonly HttpClient Http = CreateClient();

        internal static CustomMusicLinkList Load()
        {
            try
            {
                return File.Exists(FilePath) ? CustomMusicLinkList.Deserialize(File.ReadAllText(FilePath)) : new CustomMusicLinkList();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("CustomMusicLinkStore.Load - " + ex.Message);
                return new CustomMusicLinkList();
            }
        }

        internal static void Save(CustomMusicLinkList links)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, links.Serialize());
                if (File.Exists(FilePath)) File.Replace(temp, FilePath, null);
                else File.Move(temp, FilePath);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("CustomMusicLinkStore.Save - " + ex.Message);
            }
        }

        internal static async Task<string> FetchSpotifyTitle(string url)
        {
            return MusicTitleParser.OEmbedTitle(await Get(SpotifyLink.OEmbedUrl(url)));
        }

        /// <summary>oEmbed first; for a playlist oEmbed won't name, the playlist page's og:title.</summary>
        internal static async Task<string> FetchYouTubeTitle(string id, YouTubeMusicKind kind)
        {
            string title = MusicTitleParser.OEmbedTitle(await Get(YouTubeMusicLink.OEmbedUrl(id, kind)));
            if (title != null || kind != YouTubeMusicKind.Playlist) return title;
            return MusicTitleParser.OgTitle(await Get(YouTubeMusicLink.PageUrl(id, kind)));
        }

        private static async Task<string> Get(string url)
        {
            try
            {
                using (var response = await Http.GetAsync(url).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode) return null;
                    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("CustomMusicLinkStore title lookup - " + ex.Message);
                return null;
            }
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            // A desktop browser's headers get the server-rendered page that carries og:title.
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
            return client;
        }
    }
}
