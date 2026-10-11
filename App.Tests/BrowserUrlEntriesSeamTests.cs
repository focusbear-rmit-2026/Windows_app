using System;
using Focus_Bear.Utils;
using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// Core's UrlValidationTests prove the matching rule. These prove what the adapter adds on top of
    /// it — the trailing-slash trim and the window-title fallback — and that the cutover's intended
    /// behaviour changes (case-folding, subdomains, the x.com exception) actually reach the app.
    /// </summary>
    [TestClass]
    public class BrowserUrlEntriesSeamTests
    {
        private static BrowserUrlEntryMatch Match(string url, string entry, Func<string> readWindowTitle = null)
        {
            return BrowserUrlEntries.Match(url, new[] { entry }, readWindowTitle);
        }

        [TestMethod]
        public void UrlsAndEntriesMatchWhateverTheirCase()
        {
            // Windows used to compare case-sensitively, so "YouTube.com" did not block youtube.com.
            Assert.AreEqual(BrowserUrlEntryMatch.Url, Match("https://www.youtube.com/watch?v=1", "YouTube.com"));
            Assert.AreEqual(BrowserUrlEntryMatch.Url, Match("HTTPS://WWW.YOUTUBE.COM/", "youtube.com"));
        }

        [TestMethod]
        public void AnEntrySavedWithATrailingSlashStillMatchesTheBareHost()
        {
            Assert.IsFalse(UrlValidation.IsUrlValid("youtube.com", "youtube.com/"),
                "the reason the adapter trims: Core alone would stop matching this entry");
            Assert.AreEqual(BrowserUrlEntryMatch.Url, Match("youtube.com", "https://youtube.com/"));
        }

        [TestMethod]
        public void AnAlwaysBlockedDomainNowCoversItsSubdomains()
        {
            // The always-blocked list used to be anchored at the start of the URL, so m.facebook.com
            // got through a "facebook.com" entry. The Mac has always blocked it.
            Assert.AreEqual(BrowserUrlEntryMatch.Url, Match("https://m.facebook.com/home", "facebook.com"));
        }

        [TestMethod]
        public void XComMatchesItselfButNotEveryHostEndingInIt()
        {
            Assert.AreEqual(BrowserUrlEntryMatch.Url, Match("https://x.com/home", "x.com"));
            Assert.AreEqual(BrowserUrlEntryMatch.None, Match("https://www.netflix.com/browse", "x.com"));
        }

        [TestMethod]
        public void TheWindowTitleIsTheFallbackWhenTheUrlDoesNotMatch()
        {
            Assert.AreEqual(BrowserUrlEntryMatch.WindowTitle,
                Match("https://example.com/", "Reddit", () => "Reddit - Dive into anything - Google Chrome"));
        }

        [TestMethod]
        public void TheTitleIsOnlyReadWhenTheUrlDidNotMatch()
        {
            int reads = 0;
            Match("https://reddit.com/", "reddit.com", () => { reads++; return "anything"; });
            Assert.AreEqual(0, reads, "reading a title is a Win32 call; a URL match must not pay for it");
        }

        [TestMethod]
        public void TitlesStayCaseSensitive()
        {
            // Only URL matching was case-folded; a title check that grew looser would allow more.
            Assert.AreEqual(BrowserUrlEntryMatch.None, Match("https://example.com/", "reddit", () => "Reddit - Google Chrome"));
        }

        [TestMethod]
        public void NoTitleReaderOrNoTitleMeansNoTitleMatch()
        {
            Assert.AreEqual(BrowserUrlEntryMatch.None, Match("https://example.com/", "Reddit"));
            Assert.AreEqual(BrowserUrlEntryMatch.None, Match("https://example.com/", "Reddit", () => null));
        }

        [TestMethod]
        public void ABareSchemeEntryStillMatchesEveryPage()
        {
            // Stripped to nothing before the slash trim, as it always was. Changing what an existing
            // entry matches is not this cutover's call.
            Assert.AreEqual(BrowserUrlEntryMatch.Url, Match("https://anything.example/", "https://"));
        }

        [TestMethod]
        public void MissingListsAndEntriesMatchNothing()
        {
            Assert.AreEqual(BrowserUrlEntryMatch.None, BrowserUrlEntries.Match("https://reddit.com/", null));
            Assert.AreEqual(BrowserUrlEntryMatch.None, BrowserUrlEntries.Match("https://reddit.com/", new string[] { null }));
            Assert.AreEqual(BrowserUrlEntryMatch.None, BrowserUrlEntries.Match(null, new[] { "reddit.com" }));
        }

        [TestMethod]
        public void TheNeedleIsTheEntryWithoutSchemeWwwOrTrailingSlashes()
        {
            Assert.AreEqual("example.com/path", BrowserUrlEntries.ToNeedle("https://www.example.com/path//"));
        }
    }
}
