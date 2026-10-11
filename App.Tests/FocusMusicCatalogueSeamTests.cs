using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Focus_Bear.FocusMusic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The Focus Music catalogue as the post-login work refreshes it. It used to append every track again on each
    /// refresh, from a pool thread, into the collection the screen is bound to: the log shows the adds refused
    /// ("CollectionView does not support changes … from a thread different from the Dispatcher thread") and the
    /// screen's render crashing on "Collection was modified".
    /// </summary>
    [TestClass]
    public class FocusMusicCatalogueSeamTests
    {
        private const string Catalogue = "[{\"id\":\"t1\",\"name\":\"Rain\",\"download_url\":\"https://r2/rain.mp3?sig=1\",\"thumbnail_download_url\":\"https://r2/rain.png\",\"duration\":\"120\"}," +
                                         "{\"id\":\"t2\",\"name\":\"Waves\",\"download_url\":\"https://r2/waves.mp3?sig=1\",\"duration\":\"x\"}," +
                                         "{\"id\":\"t1\",\"name\":\"dup\"},{\"name\":\"no id\"}]";

        [TestMethod]
        public void RefreshingTheCatalogueTwice_ListsEachTrackOnce()
        {
            var target = new ObservableCollection<FocusMusicTrack>();
            FocusMusicManager.MergeInto(target, FocusMusicManager.ParseTracks(Catalogue, "Download"));
            FocusMusicManager.MergeInto(target, FocusMusicManager.ParseTracks(Catalogue, "Download"));

            CollectionAssert.AreEqual(new[] { "t1", "t2" }, target.Select(t => t.Id).ToList());
            Assert.AreEqual("Rain", target[0].Name);
        }

        [TestMethod]
        public void ARefresh_KeepsThePlayingTrack_AndTakesTheNewUrls()
        {
            var target = new ObservableCollection<FocusMusicTrack>();
            FocusMusicManager.MergeInto(target, FocusMusicManager.ParseTracks(Catalogue, null));
            var playing = target[0];
            playing.IsPlaying = true;

            FocusMusicManager.MergeInto(target, FocusMusicManager.ParseTracks(Catalogue.Replace("sig=1", "sig=2").Replace("\"t2\"", "\"t3\""), null));

            Assert.AreSame(playing, target.Single(t => t.Id == "t1"), "the playing track keeps its object and its state");
            Assert.IsTrue(playing.IsPlaying);
            StringAssert.Contains(playing.ServerAudioUrl, "sig=2");
            CollectionAssert.AreEqual(new[] { "t1", "t3" }, target.Select(t => t.Id).ToList(), "a track the server dropped goes");
        }

        [TestMethod]
        public void AnUnreadableCatalogue_LeavesTheTracksAsTheyWere()
        {
            Assert.IsNull(FocusMusicManager.ParseTracks("Bad Gateway", null));
            Assert.IsNull(FocusMusicManager.ParseTracks("", null));
            Assert.AreEqual(2, FocusMusicManager.ParseTracks(Catalogue, null).Count, "a bad duration is 0:00, not a dropped track");
        }
    }
}
