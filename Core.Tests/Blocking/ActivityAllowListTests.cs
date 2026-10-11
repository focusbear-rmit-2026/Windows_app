using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Blocking
{
    [TestClass]
    public class ActivityAllowListTests
    {
        [TestMethod]
        public void AFlagWithNoRunningHabitIsStale()
        {
            Assert.IsTrue(ActivityAllowList.IsStaleActivityFlag(flagSet: true, inHabitOrBreakActivity: true, runningActivityResolved: false));
            Assert.IsFalse(ActivityAllowList.IsStaleActivityFlag(true, true, runningActivityResolved: true), "a habit that resolves keeps its rules");
            Assert.IsFalse(ActivityAllowList.IsStaleActivityFlag(true, inHabitOrBreakActivity: false, runningActivityResolved: false), "not ours to clear");
            Assert.IsFalse(ActivityAllowList.IsStaleActivityFlag(false, true, false));
        }

        [TestMethod]
        public void ABlankEntryAllowsNothing()
        {
            // The old loop built "*" + "" + "*" = "**" for it, which every window title matches.
            Assert.IsFalse(ActivityAllowList.IsAppAllowed("chrome", "Reddit - Google Chrome", new[] { "" }));
            Assert.IsFalse(ActivityAllowList.IsAppAllowed("chrome", "Reddit - Google Chrome", new[] { "   ", ".exe", "*", null }));
            Assert.IsFalse(ActivityAllowList.IsAppAllowed("chrome", "Reddit - Google Chrome", new string[0]));
            Assert.IsFalse(ActivityAllowList.IsAppAllowed("chrome", "Reddit - Google Chrome", null));
        }

        [TestMethod]
        public void TheAppByProcessNameOrByWindowTitle()
        {
            var allowed = new[] { "Code.exe", " Notion " };
            Assert.IsTrue(ActivityAllowList.IsAppAllowed("Code", "", allowed));
            Assert.IsTrue(ActivityAllowList.IsAppAllowed("chrome", "Notion – My workspace", allowed), "a web app window");
            Assert.IsFalse(ActivityAllowList.IsAppAllowed("chrome", "Reddit", allowed));
            Assert.IsFalse(ActivityAllowList.IsAppAllowed("codehelper", "", allowed), "a process name must match whole");
        }

        [TestMethod]
        public void NamesAreNormalisedOnce()
        {
            CollectionAssert.AreEqual(new[] { "code", "notion" }, (System.Collections.ICollection)ActivityAllowList.AppNames(new[] { "Code.exe", "code", "", " NOTION ", "*" }));
        }

        [TestMethod]
        public void TheIndexMustPointIntoTheList()
        {
            Assert.IsTrue(ActivityAllowList.InRange(0, 1));
            Assert.IsFalse(ActivityAllowList.InRange(-1, 3));
            Assert.IsFalse(ActivityAllowList.InRange(3, 3));
        }
    }
}
