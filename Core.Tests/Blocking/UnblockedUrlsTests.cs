using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Blocking
{
    /// <summary>
    /// The list that decides what blocking never touches. Most of these cases pass under the old
    /// <c>url.Contains(entry)</c> too — the ones that matter are the ones that did not.
    /// </summary>
    [TestClass]
    public class UnblockedUrlsTests
    {
        // ---- what must stay exempt -------------------------------------------------------------

        [TestMethod]
        public void BrowserInternalPagesAreLeftAlone()
        {
            foreach (string url in new[] { "about:blank", "chrome://settings", "chrome-extension://abc/x.html",
                                           "devtools://devtools/bundled/inspector.html", "view-source:https://x.com",
                                           "favorites:whatever", "brave://rewards", "comet://flags" })
            {
                Assert.IsTrue(UnblockedUrls.IsAlwaysUnblocked(url), url);
            }
        }

        [TestMethod]
        public void BraveAndCometAreExemptForAppSwitchesToo()
        {
            // They were in ProcessBrowserURLs' copy of the list and NOT in FocusChangedHandler's, so
            // the two checks disagreed about the same page. One list means they cannot.
            Assert.IsTrue(UnblockedUrls.IsAlwaysUnblocked("brave://settings"));
            Assert.IsTrue(UnblockedUrls.IsAlwaysUnblocked("comet://version"));
        }

        [TestMethod]
        public void FocusBearsOwnSitesAreLeftAlone()
        {
            foreach (string url in new[] { "https://focusbear.io/pricing", "https://app.focusbear.io/",
                                           "https://earnyourscreentime.com", "https://focus-bear.github.io/docs",
                                           "http://localhost:50501/", "http://localhost/" })
            {
                Assert.IsTrue(UnblockedUrls.IsAlwaysUnblocked(url), url);
            }
        }

        [TestMethod]
        public void TheBlockPageItselfIsLeftAlone()
        {
            Assert.IsTrue(UnblockedUrls.IsAlwaysUnblocked(
                @"file:///C:/Users/x/AppData/Focus Bear/focus-block.html?old_url=https%3A%2F%2Freddit.com"));
            Assert.IsTrue(UnblockedUrls.IsAlwaysUnblocked(
                @"file:///C:/Users/x/AppData/Focus Bear/eyst-focus.html"));
        }

        [TestMethod]
        public void AUrlWithoutASchemeStillResolvesItsHost()
        {
            Assert.IsTrue(UnblockedUrls.IsAlwaysUnblocked("focusbear.io/pricing"));
            Assert.IsTrue(UnblockedUrls.IsAlwaysUnblocked("localhost:50501/?old_url=x"),
                          "'localhost' must not be read as the scheme");
        }

        // ---- the hole the substring match left open --------------------------------------------

        [TestMethod]
        public void ASiteThatMerelyMentionsAnExemptNameIsNotExempt()
        {
            // Every one of these passed `url.Contains(entry)` and so skipped blocking entirely.
            foreach (string url in new[] { "https://evil.com/?redirect=localhost",
                                           "https://evil.com/?next=focusbear.io",
                                           "https://evil.com/focus-block.html",
                                           "https://reddit.com/r/localhost" })
            {
                Assert.IsFalse(UnblockedUrls.IsAlwaysUnblocked(url),
                               url + " must not opt itself out of blocking");
            }
        }

        [TestMethod]
        public void ALookalikeDomainIsNotOurs()
        {
            // EndsWith(owned) would wave all of these through.
            foreach (string url in new[] { "https://notfocusbear.io/", "https://mylocalhost/",
                                           "https://focusbear.io.evil.com/", "https://fakeearnyourscreentime.com/" })
            {
                Assert.IsFalse(UnblockedUrls.IsAlwaysUnblocked(url), url);
            }
        }

        [TestMethod]
        public void AnOrdinarySiteIsNotExempt()
        {
            foreach (string url in new[] { "https://reddit.com", "https://news.ycombinator.com", "https://x.com/home" })
            {
                Assert.IsFalse(UnblockedUrls.IsAlwaysUnblocked(url), url);
            }
        }

        [TestMethod]
        public void EmptyAndMalformedInputIsNotExempt()
        {
            foreach (string url in new[] { null, "", "   ", "not a url" })
            {
                Assert.IsFalse(UnblockedUrls.IsAlwaysUnblocked(url), url ?? "<null>");
            }
        }
    }
}
