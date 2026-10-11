using FocusBear.Core.Layout;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Layout
{
    /// <summary>Report dayplan-top-space-2026-09-26_11-54-27: "there's still a bit of space above".</summary>
    [TestClass]
    public class DayPlanTopSpacingTests
    {
        [TestMethod]
        public void TheAgendaMovesUpIntoTheEmptyTopBar()
        {
            Assert.AreEqual(-30, DayPlanTopSpacing.AgendaTopOffset(30));
            // So the date bar starts 8 below the window's top, not 30 + 8.
            Assert.AreEqual(8, 30 + DayPlanTopSpacing.AgendaTopOffset(30) + DayPlanTopSpacing.AgendaGap);
        }

        [TestMethod]
        public void WithoutATopBarNothingMoves()
        {
            Assert.AreEqual(0, DayPlanTopSpacing.AgendaTopOffset(0));
            Assert.AreEqual(0, DayPlanTopSpacing.AgendaTopOffset(double.NaN), "not measured yet");
            Assert.AreEqual(0, DayPlanTopSpacing.AgendaTopOffset(-5));
        }

        [TestMethod]
        public void TheTasksPanelClearsTheExitButtonOnlyWhileItIsShown()
        {
            Assert.AreEqual(54, DayPlanTopSpacing.TasksPanelTop(exitButtonShown: true));
            Assert.AreEqual(14, DayPlanTopSpacing.TasksPanelTop(exitButtonShown: false));
        }
    }
}
