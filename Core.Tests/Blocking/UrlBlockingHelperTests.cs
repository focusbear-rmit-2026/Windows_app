using System;
using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Blocking
{
    /// <summary>
    /// Ported from the Mac app's <c>URLBlockingTests.swift</c> (the URL half — the rest of that file
    /// covers AppleScript error interpretation, which is Mac-only).
    /// </summary>
    [TestClass]
    public class UrlBlockingHelperTests
    {
        private static readonly string[] None = new string[0];

        [TestMethod]
        public void AlwaysBlockedListMembership()
        {
            var blocked = new[] { "facebook.com", "twitter.com" };
            Assert.IsTrue(UrlBlockingHelper.IsUrlInAlwaysBlockedList("https://www.facebook.com", blocked));
            Assert.IsFalse(UrlBlockingHelper.IsUrlInAlwaysBlockedList("https://www.github.com", blocked));
            Assert.IsFalse(UrlBlockingHelper.IsUrlInAlwaysBlockedList("https://www.facebook.com", None));
        }

        [TestMethod]
        public void AllowedListMembership()
        {
            var allowed = new[] { "github.com", "stackoverflow.com" };
            Assert.IsTrue(UrlBlockingHelper.IsUrlInAllowedList("https://github.com/Focus-Bear", allowed));
            Assert.IsFalse(UrlBlockingHelper.IsUrlInAllowedList("https://www.facebook.com", allowed));
            Assert.IsTrue(UrlBlockingHelper.IsUrlInAllowedList("https://github.com/Focus-Bear/repo", new[] { "github.com" }),
                "a path does not stop the domain matching");
            Assert.IsTrue(UrlBlockingHelper.IsUrlInAllowedList("https://stackoverflow.com/q/1", allowed),
                "the whole list is searched, not just the first entry");
            Assert.IsFalse(UrlBlockingHelper.IsUrlInAllowedList("https://www.github.com", None));
        }

        [TestMethod]
        public void ListMatchingIsCaseInsensitiveAndAcceptsKeywords()
        {
            Assert.IsTrue(UrlBlockingHelper.IsUrlInAlwaysBlockedList("HTTPS://TWITTER.COM", new[] { "twitter.com" }));
            Assert.IsTrue(UrlBlockingHelper.IsUrlInAlwaysBlockedList("https://m.facebook.com/messages", new[] { "facebook" }));
            Assert.IsTrue(UrlBlockingHelper.IsUrlInAlwaysBlockedList("https://m.facebook.com/messages", new[] { "*facebook*" }));
            Assert.IsTrue(UrlBlockingHelper.IsUrlInAlwaysBlockedList("https://m.facebook.com", new[] { "*.facebook.com" }));
            Assert.IsFalse(UrlBlockingHelper.IsUrlInAlwaysBlockedList(string.Empty, new[] { "facebook.com" }));
        }

        [TestMethod]
        public void BlockedUrlIsBlockedWhicheverApproachIsInUse()
        {
            Assert.IsTrue(UrlBlockingHelper.ShouldBlockUrl("https://www.facebook.com",
                new[] { "facebook.com" }, new[] { "facebook.com" }, isAllowedListApproach: false),
                "a URL in the always-blocked list is blocked");
            Assert.IsTrue(UrlBlockingHelper.ShouldBlockUrl("https://x.com",
                new[] { "x.com" }, new[] { "x.com" }, isAllowedListApproach: true),
                "always-blocked wins even when the URL is also allowed");
            Assert.IsTrue(UrlBlockingHelper.ShouldBlockUrl("https://reddit.com",
                new[] { "reddit" }, None, isAllowedListApproach: false),
                "a keyword in the always-blocked list blocks too");
        }

        [TestMethod]
        public void WithoutTheAllowListApproachOnlyTheBlockListBlocks()
        {
            Assert.IsFalse(UrlBlockingHelper.ShouldBlockUrl("https://www.twitter.com",
                None, None, isAllowedListApproach: false));
            Assert.IsFalse(UrlBlockingHelper.ShouldBlockUrl("https://anything.com",
                new[] { "reddit.com" }, None, isAllowedListApproach: false));
        }

        [TestMethod]
        public void WithTheAllowListApproachAnythingUnlistedIsBlocked()
        {
            Assert.IsTrue(UrlBlockingHelper.ShouldBlockUrl("https://www.twitter.com",
                None, new[] { "github.com", "stackoverflow.com" }, isAllowedListApproach: true));
            Assert.IsFalse(UrlBlockingHelper.ShouldBlockUrl("https://github.com/Focus-Bear",
                None, new[] { "github.com" }, isAllowedListApproach: true));
            Assert.IsFalse(UrlBlockingHelper.ShouldBlockUrl("https://github.com/explore",
                None, new[] { "github.com" }, isAllowedListApproach: true));
        }

        [TestMethod]
        public void AnEmptyAllowedListBlocksEverythingUnderTheAllowListApproach()
        {
            // Nothing is whitelisted, so nothing is allowed. Contrast with an empty ENTRY, which is
            // wrapped to "**" and matches every URL — an empty list is not an empty pattern.
            Assert.IsTrue(UrlBlockingHelper.ShouldBlockUrl("https://github.com",
                None, None, isAllowedListApproach: true));
        }

        [TestMethod]
        public void ANullListIsTreatedAsEmptyRatherThanThrowing()
        {
            // A list that has not loaded yet must not take the blocking loop down mid-session.
            Assert.IsFalse(UrlBlockingHelper.IsUrlInAlwaysBlockedList("https://github.com", null));
            Assert.IsTrue(UrlBlockingHelper.ShouldBlockUrl("https://github.com", null, null, isAllowedListApproach: true));
        }
    }
}
