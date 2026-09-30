using System;
using System.Net;
using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Blocking
{
    [TestClass]
    public class BlockPageUnblockTests
    {
        private const string PagePath = @"C:\Users\jerem\AppData\Local\Focus Bear\focus-block.html";
        private static readonly DateTime T0 = new DateTime(2026, 9, 27, 1, 0, 0, DateTimeKind.Utc);

        private static string BlockPage(BlockPageTokens tokens, string oldUrl) =>
            tokens.Stamp("file:///" + PagePath + "?old_url=" + Uri.EscapeDataString(oldUrl) + "&block_type=always-blocked&block_mode=cuddly&version=3.0.250",
                         oldUrl, T0);

        // What the page's "Oops, I need this" does: window.location.href + "&unblock_it=true".
        private static string Clicked(string pageUrl) => pageUrl + "&unblock_it=true";

        [TestMethod]
        public void TheBlockPagesOwnUnblockOpensTheBlockedUrl()
        {
            var tokens = new BlockPageTokens();
            string url = Clicked(BlockPage(tokens, "https://www.reddit.com/r/all?x=1&y=2"));
            Assert.AreEqual("https://www.reddit.com/r/all?x=1&y=2", BlockPageUnblock.AuthorisedOldUrl(url, PagePath, tokens, T0.AddMinutes(1)));
        }

        [TestMethod]
        public void ATypedUnblockAddressNeverUnblocks()
        {
            var tokens = new BlockPageTokens();
            BlockPage(tokens, "https://reddit.com");
            Assert.IsNull(BlockPageUnblock.AuthorisedOldUrl("https://reddit.com/?&unblock_it=true&old_url=https%3A%2F%2Freddit.com", PagePath, tokens, T0),
                "not the block page");
            Assert.IsNull(BlockPageUnblock.AuthorisedOldUrl("file:///" + PagePath + "?old_url=https%3A%2F%2Freddit.com&unblock_it=true", PagePath, tokens, T0),
                "the block page typed by hand has no token");
            Assert.IsNull(BlockPageUnblock.AuthorisedOldUrl("file:///" + PagePath + "?old_url=https%3A%2F%2Freddit.com&fb_unblock=0123456789abcdef0123456789abcdef&unblock_it=true", PagePath, tokens, T0),
                "a made-up token");
        }

        [TestMethod]
        public void ATokenUnblocksOnlyTheUrlItWasIssuedFor()
        {
            var tokens = new BlockPageTokens();
            string page = BlockPage(tokens, "https://news.ycombinator.com");
            string edited = page.Replace(Uri.EscapeDataString("https://news.ycombinator.com"), Uri.EscapeDataString("https://reddit.com"));
            Assert.IsNull(BlockPageUnblock.AuthorisedOldUrl(Clicked(edited), PagePath, tokens, T0));
        }

        [TestMethod]
        public void ATokenExpires()
        {
            var tokens = new BlockPageTokens();
            string url = Clicked(BlockPage(tokens, "https://reddit.com"));
            Assert.IsNotNull(BlockPageUnblock.AuthorisedOldUrl(url, PagePath, tokens, T0 + BlockPageTokens.Lifetime - TimeSpan.FromSeconds(1)));
            Assert.IsNull(BlockPageUnblock.AuthorisedOldUrl(url, PagePath, tokens, T0 + BlockPageTokens.Lifetime));
        }

        [TestMethod]
        public void TheBlockPageIsRecognisedHoweverTheBrowserShowsIt()
        {
            Assert.IsTrue(BlockPageUnblock.IsOwnBlockPage("file:///C:/Users/jerem/AppData/Local/Focus%20Bear/focus-block.html?old_url=x", PagePath));
            Assert.IsTrue(BlockPageUnblock.IsOwnBlockPage("C:/Users/jerem/AppData/Local/Focus Bear/focus-block.html?old_url=x", PagePath));
            Assert.IsTrue(BlockPageUnblock.IsOwnBlockPage("file:///" + PagePath, PagePath));
            Assert.IsFalse(BlockPageUnblock.IsOwnBlockPage("https://evil.com/Focus Bear/focus-block.html?old_url=x", PagePath));
            Assert.IsFalse(BlockPageUnblock.IsOwnBlockPage("file:///C:/Temp/focus-block.html", PagePath), "another copy of the page");
        }

        [TestMethod]
        public void OldUrlIsTheAppsEncodingDecoded()
        {
            // The app writes old_url with WebUtility.UrlEncode (spaces as "+").
            string oldUrl = "https://example.com/search?q=a b+c&x=1";
            var tokens = new BlockPageTokens();
            string page = tokens.Stamp("file:///" + PagePath + "?old_url=" + WebUtility.UrlEncode(oldUrl) + "&version=1", oldUrl, T0);
            Assert.AreEqual(oldUrl, BlockPageUnblock.AuthorisedOldUrl(Clicked(page), PagePath, tokens, T0));
        }

        [TestMethod]
        public void TheTriggerComesOffForTheReload()
        {
            Assert.AreEqual("file:///p?old_url=x&fb_unblock=t", BlockPageUnblock.WithoutTrigger("file:///p?old_url=x&fb_unblock=t&unblock_it=true"));
            Assert.IsFalse(BlockPageUnblock.HasTrigger("file:///p?old_url=x&fb_unblock=t"));
            Assert.IsTrue(BlockPageUnblock.HasTrigger("file:///p?old_url=x&unblock_it=true"));
        }

        [TestMethod]
        public void HostOfNeverThrows()
        {
            Assert.AreEqual("reddit.com", BlockPageUnblock.HostOf("https://Reddit.com/r/x"));
            Assert.AreEqual("reddit.com", BlockPageUnblock.HostOf("reddit.com/r/x"));
            Assert.IsNull(BlockPageUnblock.HostOf(""));
            Assert.IsNull(BlockPageUnblock.HostOf("http://"));
            Assert.IsNull(BlockPageUnblock.HostOf("::::"));
        }

        [TestMethod]
        public void TheStoreStaysBounded()
        {
            int n = 0;
            var tokens = new BlockPageTokens(() => "t" + (n++));
            for (int i = 0; i < BlockPageTokens.MaxTokens + 50; i++) tokens.Issue("u" + i, T0);
            Assert.AreEqual(BlockPageTokens.MaxTokens, tokens.Count);
            Assert.IsFalse(tokens.IsValid("t0", "u0", T0), "the oldest went first");
            Assert.IsTrue(tokens.IsValid("t" + (BlockPageTokens.MaxTokens + 49), "u" + (BlockPageTokens.MaxTokens + 49), T0));
        }

        [TestMethod]
        public void AnUnblockAfterAClickOnAnInPageLinkStillWorks()
        {
            // The page's "#cuddlyBearMode" and "#" links leave a fragment; its Oops then appends "&unblock_it=true"
            // after it (window.location.href + "&unblock_it=true"), so the trigger lands in the fragment.
            var tokens = new BlockPageTokens();
            string url = Clicked(BlockPage(tokens, "https://reddit.com") + "#cuddlyBearMode");
            Assert.IsTrue(BlockPageUnblock.HasTrigger(url));
            Assert.AreEqual("https://reddit.com", BlockPageUnblock.AuthorisedOldUrl(url, PagePath, tokens, T0));
        }

        [TestMethod]
        public void TheConfirmationModalsUnblockWorks()
        {
            // confirmation.modal.js: href + "&unlock_duration_minutes=5&distraction_intention=...&unblock_it=true".
            var tokens = new BlockPageTokens();
            string url = BlockPage(tokens, "https://reddit.com") + "&unlock_duration_minutes=5&distraction_intention=" + Uri.EscapeDataString("check a DM") + "&unblock_it=true";
            Assert.AreEqual("https://reddit.com", BlockPageUnblock.AuthorisedOldUrl(url, PagePath, tokens, T0));
        }

        [TestMethod]
        public void ThePagesOwnQueryRewriteKeepsTheToken()
        {
            // removeQueryParam / updateQueryParamsAndReload re-serialise the query with URLSearchParams: '+' for
            // spaces and every reserved character escaped, the token still there.
            string oldUrl = "https://example.com/a b?q=1&r=2";
            var tokens = new BlockPageTokens(() => "tok");
            tokens.Stamp("file:///x", oldUrl, T0);
            string rewritten = "file:///C:/Users/jerem/AppData/Local/Focus%20Bear/focus-block.html?old_url=https%3A%2F%2Fexample.com%2Fa+b%3Fq%3D1%26r%3D2&version=3&fb_unblock=tok";
            Assert.AreEqual(oldUrl, BlockPageUnblock.AuthorisedOldUrl(rewritten + "&unblock_it=true", PagePath, tokens, T0));
        }

        [TestMethod]
        public void ABrowserThatShowsOldUrlDecodedStillUnblocksTheIssuedUrl()
        {
            // An address bar that un-escapes the query for display splits old_url at its own '&': the token still
            // names the URL the app blocked, and that (not the displayed text) is what opens.
            string oldUrl = "https://www.reddit.com/r/all?x=1&y=2";
            var tokens = new BlockPageTokens(() => "tok");
            tokens.Stamp("file:///x", oldUrl, T0);
            string shown = "file:///C:/Users/jerem/AppData/Local/Focus Bear/focus-block.html?old_url=https://www.reddit.com/r/all?x=1&y=2&version=3&fb_unblock=tok&unblock_it=true";
            Assert.AreEqual(oldUrl, BlockPageUnblock.AuthorisedOldUrl(shown, PagePath, tokens, T0));
            string otherSite = shown.Replace("www.reddit.com", "news.ycombinator.com");
            Assert.IsNull(BlockPageUnblock.AuthorisedOldUrl(otherSite, PagePath, tokens, T0), "the token is tied to reddit's host");
        }

        [TestMethod]
        public void TokensSurviveARestartButNotTheirLifetime()
        {
            int n = 0;
            var before = new BlockPageTokens(() => "t" + (n++));
            before.Issue("https://reddit.com", T0);
            before.Issue("https://youtube.com", T0.AddHours(11));
            string saved = before.ToJson();

            var after = BlockPageTokens.FromJson(saved, T0.AddHours(12).AddMinutes(1));
            Assert.IsFalse(after.IsValid("t0", "https://reddit.com", T0.AddHours(12).AddMinutes(1)), "expired while the app was off");
            Assert.IsTrue(after.IsValid("t1", "https://youtube.com", T0.AddHours(12).AddMinutes(1)));
            Assert.AreEqual(1, after.Count, "an expired token isn't kept");
        }

        [TestMethod]
        public void AnUnreadableSavedFileStartsEmptyAndTheCapHolds()
        {
            Assert.AreEqual(0, BlockPageTokens.FromJson("not json", T0).Count);
            Assert.AreEqual(0, BlockPageTokens.FromJson(null, T0).Count);
            Assert.AreEqual(0, BlockPageTokens.FromJson("{\"tokens\":[{\"token\":\"t\",\"url\":\"u\",\"expires\":\"2026-09-28T13:00:00.0000000Z\"}]}", T0).Count,
                "a saved token lasting past its lifetime was not issued by this app");
            int n = 0;
            var big = new BlockPageTokens(() => "t" + (n++));
            for (int i = 0; i < BlockPageTokens.MaxTokens; i++) big.Issue("u" + i, T0);
            var loaded = BlockPageTokens.FromJson(big.ToJson(), T0);
            Assert.AreEqual(BlockPageTokens.MaxTokens, loaded.Count);
            loaded.Issue("new", T0);
            Assert.AreEqual(BlockPageTokens.MaxTokens, loaded.Count);
            Assert.IsFalse(loaded.IsValid("t0", "u0", T0), "the oldest loaded one goes first");
        }

        [TestMethod]
        public void EveryIssueReportsTheNewStateToSave()
        {
            string saved = null;
            var tokens = new BlockPageTokens(() => "tok") { Saved = json => saved = json };
            tokens.Issue("https://reddit.com", T0);
            Assert.IsTrue(BlockPageTokens.FromJson(saved, T0).IsValid("tok", "https://reddit.com", T0));
        }

        [TestMethod]
        public void FirefoxsAddressBarNameIsMatchedForEveryBuildsFolder()
        {
            const string e2e = @"C:\Users\jerem\AppData\Local\Focus Bear E2E\focus-block.html";
            Assert.IsTrue(BlockPageUnblock.MentionsBlockPage("file:///C:/Users/jerem/AppData/Local/Focus Bear E2E/focus-block.html?old_url=x", e2e));
            Assert.IsTrue(BlockPageUnblock.MentionsBlockPage("file:///C:/Users/jerem/AppData/Local/Focus%20Bear%20E2E/focus-block.html?old_url=x", e2e));
            Assert.IsTrue(BlockPageUnblock.MentionsBlockPage("C:/Users/jerem/AppData/Local/Focus Bear/focus-block.html", PagePath));
            Assert.IsFalse(BlockPageUnblock.MentionsBlockPage("file:///C:/Users/jerem/AppData/Local/Focus Bear/focus-block.html", e2e), "the live app's page isn't the E2E build's");
            Assert.IsFalse(BlockPageUnblock.MentionsBlockPage("Search with Google or enter address", PagePath));
        }

        [TestMethod]
        public void APageThatOffersNoUnblockGetsNoToken()
        {
            // main.js hides "Oops, I actually need this" in grizzly mode, on strict and confirmed-distraction blocks,
            // and on brain-dump routine / always-blocked pages. Its token would only let a hand-typed
            // "&unblock_it=true" do what the page itself refuses.
            var tokens = new BlockPageTokens();
            string page = "file:///" + PagePath + "?old_url=https%3A%2F%2Freddit.com&block_type=always-blocked&version=3";
            foreach (string refused in new[] { "&block_mode=grizzly", "&strict_blocking=true", "&confirm_super_distracting=true",
                                               "&confirm_ai_distracting_url=true", "&braindump=true" })
            {
                string stamped = tokens.Stamp(page + refused, "https://reddit.com", T0);
                Assert.IsNull(BlockPageUnblock.Query(stamped, BlockPageUnblock.TokenParam), refused);
                Assert.IsNull(BlockPageUnblock.AuthorisedOldUrl(stamped + "&unblock_it=true", PagePath, tokens, T0), refused);
            }
            Assert.AreEqual(0, tokens.Count);

            string cuddly = tokens.Stamp(page + "&block_mode=cuddly", "https://reddit.com", T0);
            Assert.AreEqual("https://reddit.com", BlockPageUnblock.AuthorisedOldUrl(cuddly + "&unblock_it=true", PagePath, tokens, T0));
            string focus = tokens.Stamp("file:///" + PagePath + "?old_url=x&focus_mode_end_time=t&focus_mode=Work&version=3", "x", T0);
            Assert.IsNotNull(BlockPageUnblock.Query(focus, BlockPageUnblock.TokenParam), "a focus page offers it (the app then applies the session's rules)");
            string brainDumpFocus = tokens.Stamp("file:///" + PagePath + "?old_url=x&focus_mode=Work&braindump=true&version=3", "x", T0);
            Assert.IsNotNull(BlockPageUnblock.Query(brainDumpFocus, BlockPageUnblock.TokenParam), "brain dump hides it only on routine and always-blocked pages");
        }
    }
}
