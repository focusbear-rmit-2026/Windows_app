using System;
using FocusBear.Core.Breaks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Breaks
{
    /// <summary>
    /// Mac parity (#1590 and the deactivate-breaks prompt): postponing a break from the warning takes a
    /// press-and-hold that grows 5 s, 10 s, 15 s with each consecutive postpone (taking a break resets it), and
    /// every second postpone from the warning asks whether to turn breaks off.
    /// </summary>
    [TestClass]
    public class BreakPostponeFrictionTests
    {
        [TestMethod]
        public void TheHoldGrowsWithEachPostponeUpTo15Seconds_AndResetsWhenABreakIsTaken()
        {
            var f = new BreakPostponeFriction();
            Assert.AreEqual(5, f.HoldSeconds);
            f.PostponedFromWarning();
            Assert.AreEqual(10, f.HoldSeconds);
            f.PostponedFromWarning();
            f.PostponedFromWarning();
            Assert.AreEqual(15, f.HoldSeconds);
            f.BreakTaken();
            Assert.AreEqual(5, f.HoldSeconds);
        }

        [TestMethod]
        public void EverySecondPostponeAsksAboutTurningBreaksOff()
        {
            var f = new BreakPostponeFriction();
            Assert.IsFalse(f.PostponedFromWarning());
            Assert.IsTrue(f.PostponedFromWarning());
            Assert.IsFalse(f.PostponedFromWarning());
            Assert.IsTrue(f.PostponedFromWarning());
        }

        [TestMethod]
        public void BreaksOffForTodayLastsUntilMidnight()
        {
            var setAt = new DateTime(2026, 9, 27, 15, 30, 0);
            Assert.IsTrue(BreakPostponeFriction.BreaksOffToday(setAt, new DateTime(2026, 9, 27, 23, 59, 0)));
            Assert.IsFalse(BreakPostponeFriction.BreaksOffToday(setAt, new DateTime(2026, 9, 28, 0, 0, 1)));
            Assert.IsFalse(BreakPostponeFriction.BreaksOffToday(null, setAt));
        }

        [TestMethod]
        public void BreaksOffForToday_HoldsTheAutomaticBreakBack()
        {
            var c = new AutoBreakConditions { BreaksOffToday = true };
            Assert.AreEqual(AutoBreakDecision.Reschedule, AutoBreakGate.WhenWarningEnds(c));
        }
    }
}
