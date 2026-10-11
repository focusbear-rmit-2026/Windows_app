using FocusBear.Core.Breaks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Breaks
{
    /// <summary>
    /// The break warning counts down 30 seconds and then starts the break. It checked nothing when the
    /// countdown ended, so a focus session started during those 30 seconds with "skip micro-breaks during
    /// focus" on was taken over by the break anyway. The Mac starts the break only if its state is still
    /// "micro break" when the countdown ends; this is the same re-check.
    /// </summary>
    [TestClass]
    public class AutoBreakGateTests
    {
        private static AutoBreakConditions Quiet() => new AutoBreakConditions();

        [TestMethod]
        public void NothingChanged_TheBreakStarts()
        {
            Assert.AreEqual(AutoBreakDecision.Start, AutoBreakGate.WhenWarningEnds(Quiet()));
        }

        [TestMethod]
        public void AFocusSessionThatSkipsBreaks_PutsTheBreakBackOnTheTimer()
        {
            var c = Quiet();
            c.SkipBreaksDuringFocus = true;
            c.FocusSessionRunning = true;
            Assert.AreEqual(AutoBreakDecision.Reschedule, AutoBreakGate.WhenWarningEnds(c));

            c.FocusSessionRunning = false;
            c.SuperFocusRunning = true;
            Assert.AreEqual(AutoBreakDecision.Reschedule, AutoBreakGate.WhenWarningEnds(c));

            c.SkipBreaksDuringFocus = false;
            Assert.AreEqual(AutoBreakDecision.Start, AutoBreakGate.WhenWarningEnds(c), "breaks were wanted during focus");
        }

        [TestMethod]
        public void DeactivatedOrABlockingScheduleWithoutBreaks_Reschedules()
        {
            var c = Quiet();
            c.AppDeactivated = true;
            Assert.AreEqual(AutoBreakDecision.Reschedule, AutoBreakGate.WhenWarningEnds(c));

            c = Quiet();
            c.ScheduleDisablesBreaks = true;
            Assert.AreEqual(AutoBreakDecision.Reschedule, AutoBreakGate.WhenWarningEnds(c));
        }

        [TestMethod]
        public void ACallStartedDuringTheWarning_RespectsShowBreaksInCall()
        {
            var c = Quiet();
            c.OnCall = true;
            Assert.AreEqual(AutoBreakDecision.Reschedule, AutoBreakGate.WhenWarningEnds(c));
            c.ShowBreaksInCall = true;
            Assert.AreEqual(AutoBreakDecision.Start, AutoBreakGate.WhenWarningEnds(c));
        }

        [TestMethod]
        public void ABreakAlreadyRunning_IsLeftAlone()
        {
            var c = Quiet();
            c.BreakAlreadyRunning = true;
            c.AppDeactivated = true;
            Assert.AreEqual(AutoBreakDecision.Nothing, AutoBreakGate.WhenWarningEnds(c));
        }

        [TestMethod]
        public void ShutOffTimeArrivedDuringTheWarning_NoBreakAndNoNewTimer()
        {
            var c = Quiet();
            c.ShutOffTime = true;
            Assert.AreEqual(AutoBreakDecision.Nothing, AutoBreakGate.WhenWarningEnds(c));
        }
    }
}
