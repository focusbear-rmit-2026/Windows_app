using System;
using System.Net;
using Focus_Bear.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The app's unblock gate over its real block page path: only the page it built (with its token) unblocks, a
    /// typed "&amp;unblock_it=true" address never does.
    /// </summary>
    [TestClass]
    public class BlockPageUnblockSeamTests
    {
        private static string PageFor(string oldUrl) =>
            ProcessBrowserURLs.UnblockTokens.Stamp("file:///" + AppSettings.FocusBlockPagePath + "?old_url=" + WebUtility.UrlEncode(oldUrl)
                + "&block_type=always-blocked&block_mode=cuddly&version=3", oldUrl, DateTime.UtcNow);

        [TestMethod]
        public void TheAppsOwnBlockPageUnblocks()
        {
            string clicked = PageFor("https://www.reddit.com/r/all") + "&unblock_it=true";
            Assert.AreEqual("https://www.reddit.com/r/all", ProcessBrowserURLs.AuthorisedUnblock(clicked));
        }

        [TestMethod]
        public void ATypedUnblockDoesNot()
        {
            PageFor("https://reddit.com");
            Assert.IsNull(ProcessBrowserURLs.AuthorisedUnblock("https://reddit.com/?&unblock_it=true&old_url=https%3A%2F%2Freddit.com"));
            Assert.IsNull(ProcessBrowserURLs.AuthorisedUnblock("file:///" + AppSettings.FocusBlockPagePath + "?old_url=https%3A%2F%2Freddit.com&unblock_it=true"));
            Assert.IsNull(ProcessBrowserURLs.AuthorisedUnblock(PageFor("https://reddit.com")), "no trigger, nothing to do");
        }

        [TestMethod]
        public void ATokenIsSavedInTheAppsOwnDataFolderForTheNextRun()
        {
            string clicked = PageFor("https://www.youtube.com/watch?v=1") + "&unblock_it=true";
            string path = ProcessBrowserURLs.UnblockTokensPath;
            StringAssert.StartsWith(path, AppSettings.AppDataDirPath, "never the live app's folder from a test host");
            Assert.IsTrue(System.IO.File.Exists(path));
            // What the next run loads: the same file, read back.
            var nextRun = FocusBear.Core.Blocking.BlockPageTokens.FromJson(System.IO.File.ReadAllText(path), DateTime.UtcNow);
            Assert.AreEqual("https://www.youtube.com/watch?v=1",
                FocusBear.Core.Blocking.BlockPageUnblock.AuthorisedOldUrl(clicked, AppSettings.FocusBlockPagePath, nextRun, DateTime.UtcNow));
        }
    }
}
