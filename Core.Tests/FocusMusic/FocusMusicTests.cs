using System;
using System.Linq;
using FocusBear.Core.FocusMusic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.FocusMusic
{
    [TestClass]
    public class FocusMusicTests
    {
        [TestMethod]
        public void SpotifyLinksBecomeSpotifyUris()
        {
            var web = SpotifyLink.Parse("  https://open.spotify.com/playlist/37i9dQZF1DWZeKCadgRdKQ?si=abc123 ");
            Assert.AreEqual("spotify:playlist:37i9dQZF1DWZeKCadgRdKQ", web.Uri);
            Assert.AreEqual("playlist", web.Kind);
            Assert.AreEqual("https://open.spotify.com/playlist/37i9dQZF1DWZeKCadgRdKQ", web.WebUrl);
            Assert.AreEqual("spotify:track:4uLU6hMCjMI75M1A2tKUQC", SpotifyLink.Parse("https://open.spotify.com/intl-de/track/4uLU6hMCjMI75M1A2tKUQC").Uri, "locale prefix");
            Assert.AreEqual("spotify:album:1ATL5GLyefJaxhQzSPVrLX", SpotifyLink.Parse("spotify:album:1ATL5GLyefJaxhQzSPVrLX").Uri);
            Assert.AreEqual("episode", SpotifyLink.KindOf("https://open.spotify.com/episode/0Q86acNRm6V9GYx55SXKwf"));
        }

        [TestMethod]
        public void NonSpotifyOrUnsafeLinksAreRefused()
        {
            Assert.IsNull(SpotifyLink.Parse(""));
            Assert.IsNull(SpotifyLink.Parse(null));
            Assert.IsNull(SpotifyLink.Parse("https://example.com/track/4uLU6hMCjMI75M1A2tKUQC"));
            Assert.IsNull(SpotifyLink.Parse("https://open.spotify.com/artist/4uLU6hMCjMI75M1A2tKUQC"), "artists aren't playable items");
            Assert.IsNull(SpotifyLink.Parse("spotify:track:abc\" & calc"), "ids are base62 only");
            Assert.IsNull(SpotifyLink.Parse("spotify:track"));
            Assert.IsNull(SpotifyLink.Parse("https://open.spotify.com/"));
        }

        [TestMethod]
        public void YouTubeMusicVideoAndPlaylistLinksParse()
        {
            var video = YouTubeMusicLink.Parse("https://music.youtube.com/watch?v=jfKfPfyJRdk&feature=share");
            Assert.AreEqual("jfKfPfyJRdk", video.Id);
            Assert.AreEqual(YouTubeMusicKind.Video, video.Kind);
            Assert.AreEqual(YouTubeMusicKind.Video, YouTubeMusicLink.Parse("https://music.youtube.com/watch?v=jfKfPfyJRdk&list=RDAMVMjfKfPfyJRdk").Kind, "a video in a mix plays the video");
            Assert.AreEqual("jfKfPfyJRdk", YouTubeMusicLink.Parse("https://music.youtube.com/embed/jfKfPfyJRdk").Id);
            var mix = YouTubeMusicLink.Parse("https://music.youtube.com/playlist?list=RDCLAK5uy_kb7EBi6y3GrtJri4_ZH56Ms786DFEimbM");
            Assert.AreEqual(YouTubeMusicKind.Playlist, mix.Kind);
            Assert.AreEqual("RDCLAK5uy_kb7EBi6y3GrtJri4_ZH56Ms786DFEimbM", mix.Id);
        }

        [TestMethod]
        public void OnlyMusicYouTubeLinksAreAccepted()
        {
            Assert.IsNull(YouTubeMusicLink.Parse("https://www.youtube.com/watch?v=jfKfPfyJRdk"), "plain YouTube isn't focus music");
            Assert.IsNull(YouTubeMusicLink.Parse("https://youtu.be/jfKfPfyJRdk"));
            Assert.IsNull(YouTubeMusicLink.Parse("https://music.youtube.com.evil.com/watch?v=jfKfPfyJRdk"));
            Assert.IsNull(YouTubeMusicLink.Parse("https://music.youtube.com/watch?v=short"));
            Assert.IsNull(YouTubeMusicLink.Parse("https://music.youtube.com/playlist?list=PL'%3Balert(1)"), "ids go into the player page");
            Assert.IsNull(YouTubeMusicLink.Parse("https://music.youtube.com/"));
            Assert.IsNull(YouTubeMusicLink.Parse("not a link"));
        }

        [TestMethod]
        public void TitleLookupUrlsTargetThePublicOEmbedEndpoints()
        {
            Assert.AreEqual("https://open.spotify.com/oembed?url=https%3A%2F%2Fopen.spotify.com%2Ftrack%2Fabc", SpotifyLink.OEmbedUrl("https://open.spotify.com/track/abc"));
            Assert.AreEqual("https://www.youtube.com/oembed?url=https%3A%2F%2Fwww.youtube.com%2Fplaylist%3Flist%3DPLx&format=json", YouTubeMusicLink.OEmbedUrl("PLx", YouTubeMusicKind.Playlist));
            Assert.AreEqual("https://www.youtube.com/watch?v=jfKfPfyJRdk", YouTubeMusicLink.PageUrl("jfKfPfyJRdk", YouTubeMusicKind.Video));
        }

        [TestMethod]
        public void TitlesComeFromOEmbedOrTheOgTitle()
        {
            Assert.AreEqual("lofi hip hop radio", MusicTitleParser.OEmbedTitle("{\"title\":\" lofi hip hop radio \",\"type\":\"video\"}"));
            Assert.IsNull(MusicTitleParser.OEmbedTitle("Not Found"));
            Assert.IsNull(MusicTitleParser.OEmbedTitle("{\"title\":\"\"}"));
            Assert.AreEqual("Deep Focus & \"Calm\"", MusicTitleParser.OgTitle("<head><meta property=\"og:title\" content=\"Deep Focus &amp; &quot;Calm&quot;\"><meta></head>"));
            Assert.IsNull(MusicTitleParser.OgTitle("<meta property=\"og:title\" content=\"YouTube\">"), "YouTube's generic title isn't a name");
            Assert.IsNull(MusicTitleParser.OgTitle("<html>consent</html>"));
        }

        [TestMethod]
        public void TheLinkListAddsOnceRenamesRemovesAndRoundTrips()
        {
            var list = new CustomMusicLinkList()
                .Add(new CustomMusicLink(CustomMusicSource.Spotify, "spotify:playlist:abc", "Spotify playlist", "https://open.spotify.com/playlist/abc"))
                .Add(new CustomMusicLink(CustomMusicSource.YouTubeMusic, "RDmix", "YouTube Music playlist", "https://music.youtube.com/playlist?list=RDmix", YouTubeMusicKind.Playlist))
                .Add(new CustomMusicLink(CustomMusicSource.Spotify, "spotify:playlist:abc", "again", "dup"));
            Assert.AreEqual(2, list.All.Count, "a link is added once");
            list = list.Rename(CustomMusicSource.Spotify, "spotify:playlist:abc", " Deep Focus ").Rename(CustomMusicSource.Spotify, "spotify:playlist:abc", "  ");
            Assert.AreEqual("Deep Focus", list.Find(CustomMusicSource.Spotify, "spotify:playlist:abc").Name, "a blank name never replaces one");

            var back = CustomMusicLinkList.Deserialize(list.Serialize());
            Assert.AreEqual(2, back.All.Count);
            var yt = back.Of(CustomMusicSource.YouTubeMusic).Single();
            Assert.AreEqual(YouTubeMusicKind.Playlist, yt.Kind);
            Assert.AreEqual("https://music.youtube.com/playlist?list=RDmix", yt.Url);
            Assert.AreEqual(0, back.Remove(CustomMusicSource.YouTubeMusic, "RDmix").Of(CustomMusicSource.YouTubeMusic).Count());
            Assert.AreEqual(0, CustomMusicLinkList.Deserialize("{broken").All.Count);
            Assert.AreEqual(0, CustomMusicLinkList.Deserialize(null).All.Count);
        }

        [TestMethod]
        public void PlaceholderNamesGiveWayToThePlayersTitle()
        {
            Assert.AreEqual("Spotify playlist", CustomMusicNames.SpotifyPlaceholder("Spotify", "playlist"));
            Assert.AreEqual("YouTube Music playlist", CustomMusicNames.YouTubePlaceholder("YouTube Music", YouTubeMusicKind.Playlist));
            Assert.AreEqual("YouTube Music", CustomMusicNames.YouTubePlaceholder("YouTube Music", YouTubeMusicKind.Video));
            Assert.IsTrue(CustomMusicNames.IsYouTubePlaceholder("YouTube Music playlist", "YouTube Music"));
            Assert.IsTrue(CustomMusicNames.IsYouTubePlaceholder("https://music.youtube.com/x", "YouTube Music"));
            Assert.IsFalse(CustomMusicNames.IsYouTubePlaceholder("Lofi Girl mix", "YouTube Music"), "a real title stays as a mix moves on");
            Assert.AreEqual("Song — Artist", CustomMusicNames.FromPlayer(" Song ", "Artist"));
            Assert.AreEqual("Song", CustomMusicNames.FromPlayer("Song", ""));
            Assert.IsNull(CustomMusicNames.FromPlayer(" ", "Artist"));
        }

        [TestMethod]
        public void ThePlayerPageEmbedsOnlyValidIds()
        {
            string video = YouTubeMusicPlayerPage.Html("jfKfPfyJRdk", YouTubeMusicKind.Video, 150);
            StringAssert.Contains(video, "videoId: 'jfKfPfyJRdk'");
            StringAssert.Contains(video, "var fbVolume = 100;");
            StringAssert.Contains(video, "https://www.youtube.com/iframe_api");
            string playlist = YouTubeMusicPlayerPage.Html("RDmix_-1", YouTubeMusicKind.Playlist, 40);
            StringAssert.Contains(playlist, "listType: 'playlist', list: 'RDmix_-1'");
            Assert.IsFalse(playlist.Contains("videoId"));
            Assert.IsNull(YouTubeMusicPlayerPage.Html("x'); alert(1); //", YouTubeMusicKind.Playlist, 50));
            Assert.IsNull(YouTubeMusicPlayerPage.Html("short", YouTubeMusicKind.Video, 50));
        }

        [TestMethod]
        public void NowPlayingTimesReadAsAClock()
        {
            Assert.AreEqual("0:00", FocusMusicTime.Clock(TimeSpan.FromSeconds(-3)));
            Assert.AreEqual("4:05", FocusMusicTime.Clock(TimeSpan.FromSeconds(245)));
            Assert.AreEqual("1:02:09", FocusMusicTime.Clock(new TimeSpan(1, 2, 9)));
            Assert.AreEqual(0.5, FocusMusicTime.Fraction(TimeSpan.FromMinutes(45), "5400"), 1e-9);
            Assert.AreEqual(1, FocusMusicTime.Fraction(TimeSpan.FromHours(3), "5400"));
            Assert.AreEqual(0, FocusMusicTime.Fraction(TimeSpan.FromHours(3), ""));
        }
    }
}
