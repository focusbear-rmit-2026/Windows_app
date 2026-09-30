using System.Collections.Generic;
using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Blocking
{
    [TestClass]
    public class AppBlockNoticeTests
    {
        [TestMethod]
        public void TheButtonIsOfferedOnlyInCuddlyModeOutsideAClassSession()
        {
            Assert.IsTrue(AppBlockNotice.OffersNeedItButton(cuddlyMode: true, teacherSessionActive: false));
            Assert.IsFalse(AppBlockNotice.OffersNeedItButton(cuddlyMode: false, teacherSessionActive: false),
                "strict mode has no popup to go back to");
            Assert.IsFalse(AppBlockNotice.OffersNeedItButton(cuddlyMode: true, teacherSessionActive: true),
                "a class session offers Request unlock instead");
        }

        [TestMethod]
        public void NeedingItForgetsEveryCopyOfTheSessionBlockAndNothingElse()
        {
            var blocked = new List<string> { "windowsterminal", "slack", "WindowsTerminal" };
            Assert.IsTrue(AppBlockNotice.ForgetSessionBlock(blocked, "WindowsTerminal"));
            CollectionAssert.AreEqual(new[] { "slack" }, blocked);
            Assert.IsFalse(AppBlockNotice.ForgetSessionBlock(blocked, "WindowsTerminal"), "already gone");
            Assert.IsFalse(AppBlockNotice.ForgetSessionBlock(null, "x"));
            Assert.IsFalse(AppBlockNotice.ForgetSessionBlock(blocked, ""));
        }
    }
}
