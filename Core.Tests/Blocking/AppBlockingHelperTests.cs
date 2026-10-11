using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Blocking
{
    /// <summary>Ported from the Mac app's <c>AppBlockingTests.swift</c>.</summary>
    [TestClass]
    public class AppBlockingHelperTests
    {
        private static readonly string[] None = new string[0];

        // MARK: - List membership

        [TestMethod]
        public void AnAppMatchesItsBundlePathByName()
        {
            var allowed = new[] { "/Applications/Calculator.app", "/Applications/Notes.app" };
            Assert.IsTrue(AppBlockingHelper.IsAppInAlwaysAllowedList("Calculator", allowed));
            Assert.IsTrue(AppBlockingHelper.IsAppInAlwaysAllowedList("Notes", allowed),
                "the whole list is searched");
            Assert.IsFalse(AppBlockingHelper.IsAppInAlwaysAllowedList("Safari", allowed));
            Assert.IsFalse(AppBlockingHelper.IsAppInAlwaysAllowedList("Calculator", None));
        }

        [TestMethod]
        public void NameMatchingIsCaseInsensitive()
        {
            Assert.IsTrue(AppBlockingHelper.IsAppInAlwaysAllowedList("CALCULATOR", new[] { "/Applications/Calculator.app" }));
            Assert.IsTrue(AppBlockingHelper.IsAppInBlockedList("TWITTER", new[] { "/Applications/Twitter.app" }));
        }

        [TestMethod]
        public void PathsWithSpacesNestedDirectoriesAndNoExtensionAllReduceToTheAppName()
        {
            Assert.IsTrue(AppBlockingHelper.IsAppInAlwaysAllowedList("Visual Studio Code",
                new[] { "/Applications/Visual Studio Code.app" }));
            Assert.IsTrue(AppBlockingHelper.IsAppInAllowedList("Visual Studio Code",
                new[] { "/Applications/Xcode.app", "/Applications/Visual Studio Code.app" }));
            Assert.IsTrue(AppBlockingHelper.IsAppInAllowedList("Xcode", new[] { "/Users/me/Apps/Xcode.app" }));
            Assert.IsTrue(AppBlockingHelper.IsAppInAlwaysAllowedList("Slack", new[] { "/Applications/Slack" }));
        }

        [TestMethod]
        public void BlockedAndAllowedListMembership()
        {
            var blocked = new[] { "/Applications/Twitter.app", "/Applications/Facebook.app" };
            Assert.IsTrue(AppBlockingHelper.IsAppInBlockedList("Twitter", blocked));
            Assert.IsFalse(AppBlockingHelper.IsAppInBlockedList("Safari", blocked));
            Assert.IsFalse(AppBlockingHelper.IsAppInBlockedList("Safari", None));

            var allowed = new[] { "/Applications/Xcode.app", "/Applications/Terminal.app" };
            Assert.IsTrue(AppBlockingHelper.IsAppInAllowedList("Xcode", allowed));
            Assert.IsFalse(AppBlockingHelper.IsAppInAllowedList("Safari", allowed));
            Assert.IsFalse(AppBlockingHelper.IsAppInAllowedList("Safari", None));
        }

        [TestMethod]
        public void AnEmptyAppNameMatchesNothing()
        {
            Assert.IsFalse(AppBlockingHelper.IsAppInBlockedList(string.Empty, None));
            Assert.IsFalse(AppBlockingHelper.IsAppInBlockedList(string.Empty, new[] { "/Applications/Twitter.app" }));
        }

        // MARK: - Precedence

        [TestMethod]
        public void AlwaysAllowedBeatsEverything()
        {
            Assert.IsFalse(AppBlockingHelper.ShouldBlockApp("Calculator",
                alwaysAllowedApps: new[] { "/Applications/Calculator.app" },
                blockedApps: new[] { "/Applications/Calculator.app" },
                allowedApps: None, isAllowedAllAppsInFocus: false, defaultAllowedApps: None),
                "always-allowed wins over the blocked list");

            Assert.IsFalse(AppBlockingHelper.ShouldBlockApp("Calculator",
                alwaysAllowedApps: new[] { "/Applications/Calculator.app" },
                blockedApps: None, allowedApps: None,
                isAllowedAllAppsInFocus: false, defaultAllowedApps: None));
        }

        [TestMethod]
        public void BlockedBeatsEveryAllowRuleBelowIt()
        {
            Assert.IsTrue(AppBlockingHelper.ShouldBlockApp("Twitter",
                None, new[] { "/Applications/Twitter.app" }, None,
                isAllowedAllAppsInFocus: true, defaultAllowedApps: None),
                "the blocked list wins over \"allow all apps in focus\"");

            Assert.IsTrue(AppBlockingHelper.ShouldBlockApp("Twitter",
                None, new[] { "/Applications/Twitter.app" }, new[] { "/Applications/Twitter.app" },
                isAllowedAllAppsInFocus: false, defaultAllowedApps: None),
                "the blocked list wins over the allowed list");

            Assert.IsTrue(AppBlockingHelper.ShouldBlockApp("twitter",
                None, new[] { "/Applications/Twitter.app" }, None,
                isAllowedAllAppsInFocus: false, defaultAllowedApps: new[] { "twitter" }),
                "the blocked list is checked before the default-allowed apps");
        }

        [TestMethod]
        public void AllowAllAppsInFocusLetsAnUnlistedAppThrough()
        {
            Assert.IsFalse(AppBlockingHelper.ShouldBlockApp("Safari",
                None, None, None, isAllowedAllAppsInFocus: true, defaultAllowedApps: None));
        }

        [TestMethod]
        public void TheDefaultAllowedTableMustBeWrittenInLowerCase()
        {
            // It is compared against the already-lower-cased app name with exact equality, unlike
            // every other list here, so the casing of its entries is load-bearing.
            Assert.IsFalse(AppBlockingHelper.ShouldBlockApp("finder",
                None, None, None, isAllowedAllAppsInFocus: false,
                defaultAllowedApps: new[] { "finder", "universalaccessauthwarn" }));

            Assert.IsTrue(AppBlockingHelper.ShouldBlockApp("Finder",
                None, None, None, isAllowedAllAppsInFocus: false, defaultAllowedApps: new[] { "Finder" }),
                "a capitalised entry never matches, so the app is blocked");
        }

        [TestMethod]
        public void AnAppInTheAllowedListIsNotBlocked()
        {
            Assert.IsFalse(AppBlockingHelper.ShouldBlockApp("Xcode",
                None, None, new[] { "/Applications/Xcode.app" },
                isAllowedAllAppsInFocus: false, defaultAllowedApps: None));
        }

        [TestMethod]
        public void AnAppInNoListAtAllIsBlocked()
        {
            Assert.IsTrue(AppBlockingHelper.ShouldBlockApp("Safari",
                None, None, new[] { "/Applications/Xcode.app" },
                isAllowedAllAppsInFocus: false, defaultAllowedApps: new[] { "finder" }));

            Assert.IsTrue(AppBlockingHelper.ShouldBlockApp("RandomApp",
                new[] { "/Applications/Calculator.app" }, new[] { "/Applications/Twitter.app" },
                new[] { "/Applications/Xcode.app" },
                isAllowedAllAppsInFocus: false, defaultAllowedApps: new[] { "finder" }));
        }
    }
}
