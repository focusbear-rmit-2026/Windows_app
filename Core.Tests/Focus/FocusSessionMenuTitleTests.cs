using System;
using FocusBear.Core.Focus;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Focus
{
    [TestClass]
    public class FocusSessionMenuTitleTests
    {
        private static readonly DateTime Now = new DateTime(2026, 9, 26, 12, 0, 0);

        private static string Title(bool running, bool paused, bool super, DateTime? end, string template = "Focusing (<MINUTES>m left)") =>
            FocusSessionMenuTitle.For(running, paused, super, end, Now, template, "Resume Focus Session", "Stop Super Focus Session", "Start Focus Session");

        [TestMethod]
        public void ARunningSessionSaysHowLongIsLeftInWholeMinutesRoundedUp()
        {
            Assert.AreEqual("Focusing (29m left)", Title(true, false, false, Now.AddMinutes(28).AddSeconds(30)));
            Assert.AreEqual("Focusing (29m left)", Title(true, false, false, Now.AddMinutes(29)));
            Assert.AreEqual("Focusing (1m left)", Title(true, false, false, Now.AddSeconds(20)), "twenty seconds still reads 1m, not 0m");
            Assert.AreEqual("Focusing (65m left)", Title(true, false, false, Now.AddMinutes(65)), "the Mac keeps minutes past the hour");
            Assert.AreEqual("Focusing (0m left)", Title(true, false, false, Now.AddMinutes(-1)), "an overrun never counts backwards");
            Assert.AreEqual("Focusing (0m left)", Title(true, false, false, null));
            Assert.AreEqual("Focusing (29m left)", Title(true, false, true, Now.AddMinutes(29)), "a Super Focus session that is running is focusing too");
        }

        [TestMethod]
        public void OtherStatesKeepTheirTitles()
        {
            Assert.AreEqual("Resume Focus Session", Title(true, true, false, Now.AddMinutes(10)));
            Assert.AreEqual("Stop Super Focus Session", Title(false, false, true, null));
            Assert.AreEqual("Start Focus Session", Title(false, false, false, null));
        }

        [TestMethod]
        public void ATranslatedTemplateIsUsedAndABrokenOneFallsBack()
        {
            Assert.AreEqual("Konzentriert (5 Min. übrig)", Title(true, false, false, Now.AddMinutes(5), "Konzentriert (<MINUTES> Min. übrig)"));
            Assert.AreEqual("Focusing (5m left)", Title(true, false, false, Now.AddMinutes(5), "Focusing"), "a template without the token");
            Assert.AreEqual("Focusing (5m left)", Title(true, false, false, Now.AddMinutes(5), null));
        }
    }
}
