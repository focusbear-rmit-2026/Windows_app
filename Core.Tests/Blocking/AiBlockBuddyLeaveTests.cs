using System;
using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Blocking
{
    [TestClass]
    public class AiBlockBuddyLeaveTests
    {
        private static readonly TimeSpan Later = TimeSpan.FromSeconds(5);

        [TestMethod]
        public void SwitchingToAnotherAppLeavesAnAppCheckIn()
        {
            // The report: a WindowsTerminal check-in stayed up after the user moved to Chrome.
            Assert.IsTrue(AiBlockBuddy.HasLeftItem(ForegroundKind.OtherApp, "chrome", "WindowsTerminal", true, true, Later));
            Assert.IsFalse(AiBlockBuddy.HasLeftItem(ForegroundKind.OtherApp, "windowsterminal", "WindowsTerminal", true, true, Later),
                "still on the blocked app (process names compare case-insensitively)");
        }

        [TestMethod]
        public void SwitchingToFocusBearsMainWindowLeavesButNotRightAway()
        {
            Assert.IsTrue(AiBlockBuddy.HasLeftItem(ForegroundKind.OwnMainWindow, "Focus Bear", "WindowsTerminal", true, true, Later));
            Assert.IsTrue(AiBlockBuddy.HasLeftItem(ForegroundKind.OwnMainWindow, "Focus Bear", "chrome", false, true, Later));
            Assert.IsFalse(AiBlockBuddy.HasLeftItem(ForegroundKind.OwnMainWindow, "Focus Bear", "chrome", false, true, TimeSpan.FromMilliseconds(300)),
                "presenting the check-in can bring the app forward for a moment");
        }

        [TestMethod]
        public void TheCheckInItsDialogsTheShellAndUnreadableWindowsNeverCount()
        {
            foreach (var kind in new[] { ForegroundKind.OwnPopup, ForegroundKind.SystemShell, ForegroundKind.Unknown })
                Assert.IsFalse(AiBlockBuddy.HasLeftItem(kind, "snippingtool", "WindowsTerminal", true, true, Later), kind.ToString());
            Assert.IsFalse(AiBlockBuddy.HasLeftItem(ForegroundKind.OtherApp, "", "WindowsTerminal", true, true, Later), "unreadable process");
        }

        [TestMethod]
        public void APageCheckInGoesWhenTheBrowserLeavesThePageOrTheUserLeavesTheBrowser()
        {
            Assert.IsTrue(AiBlockBuddy.HasLeftItem(ForegroundKind.OwnPopup, "Focus Bear", "chrome", false, false, Later), "navigated away");
            Assert.IsTrue(AiBlockBuddy.HasLeftItem(ForegroundKind.OtherApp, "WindowsTerminal", "chrome", false, true, Later), "left the browser");
            Assert.IsFalse(AiBlockBuddy.HasLeftItem(ForegroundKind.OtherApp, "chrome", "chrome", false, true, Later));
        }

        [TestMethod]
        public void TheShellIsNotAnotherApp()
        {
            Assert.IsTrue(AiBlockBuddy.IsShellProcess("SnippingTool"));
            Assert.IsTrue(AiBlockBuddy.IsShellProcess("explorer"));
            Assert.IsFalse(AiBlockBuddy.IsShellProcess("chrome"));
            Assert.IsFalse(AiBlockBuddy.IsShellProcess(null));
        }

        [TestMethod]
        public void OneCheckInPerAppOrPage()
        {
            Assert.AreEqual(AiBlockBuddy.ItemKey(true, "WindowsTerminal", ""), AiBlockBuddy.ItemKey(true, "windowsterminal", "x"));
            Assert.AreNotEqual(AiBlockBuddy.ItemKey(true, "WindowsTerminal", ""), AiBlockBuddy.ItemKey(false, "", "en.wikipedia.org/wiki/x"));
            Assert.AreNotEqual(AiBlockBuddy.ItemKey(false, "", "a.com/1"), AiBlockBuddy.ItemKey(false, "", "a.com/2"));
        }
    }
}
