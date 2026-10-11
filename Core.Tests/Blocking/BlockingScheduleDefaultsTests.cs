using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Blocking
{
    /// <summary>
    /// Ported from the Mac app's <c>BlockingScheduleDefaultsTests</c> (it lives at the foot of
    /// <c>CustomBlockingScheduleTests.swift</c>), report 2026-09-15_17-43-33.
    ///
    /// The editor seeds a brand-new schedule from the defaults and loads an existing one from its own
    /// saved values. Those two paths are what keep "change the default" from rewriting anybody's
    /// saved window.
    /// </summary>
    [TestClass]
    public class BlockingScheduleDefaultsTests
    {
        [TestMethod]
        public void ANewScheduleDefaultsToEightAmUntilFivePm()
        {
            // The old "two minutes from now, for an hour" made onboarding create a block that
            // expired the same morning.
            var model = new BlockingScheduleEditorModel();
            model.ApplyNewScheduleDefaults();

            Assert.AreEqual(8 * 60, model.StartMinutes, "a new schedule should start at 08:00");
            Assert.AreEqual(17 * 60, model.EndMinutes, "a new schedule should end at 17:00");
            Assert.IsFalse(model.IsEditing);
        }

        [TestMethod]
        public void ANewScheduleLeavesTheAiAndMicrobreakOptionsUnticked()
        {
            var model = new BlockingScheduleEditorModel();
            model.UseAI = true;
            model.SkipMicroBreaks = true;
            model.ApplyNewScheduleDefaults();

            Assert.IsFalse(model.UseAI, "\"use AI to block distractions\" must not be ticked by default");
            Assert.IsFalse(model.SkipMicroBreaks, "\"skip microbreaks\" must not be ticked by default");
        }

        [TestMethod]
        public void ANewScheduleKeepsAPrefilledWindow()
        {
            var model = new BlockingScheduleEditorModel();
            model.ApplyNewScheduleDefaults(startMinutes: 20 * 60, endMinutes: 23 * 60);

            Assert.AreEqual(20 * 60, model.StartMinutes, "a dragged or preset start time still wins over the default");
            Assert.AreEqual(23 * 60, model.EndMinutes);
        }

        [TestMethod]
        public void APrefilledWindowThatDoesNotAdvanceGetsAnHour()
        {
            var model = new BlockingScheduleEditorModel();
            model.ApplyNewScheduleDefaults(startMinutes: 20 * 60, endMinutes: 20 * 60);
            Assert.AreEqual(21 * 60, model.EndMinutes);

            // …and never past the end of the day.
            model.ApplyNewScheduleDefaults(startMinutes: 23 * 60 + 30, endMinutes: 0);
            Assert.AreEqual(BlockingScheduleDefaults.MinutesPerDay, model.EndMinutes);
        }

        [TestMethod]
        public void AnExistingScheduleKeepsItsSavedWindowAndToggles()
        {
            var model = new BlockingScheduleEditorModel();
            model.ApplySavedSchedule(startMinutes: 9 * 60, endMinutes: 18 * 60,
                                     isAIBlockingEnabled: true, isMicroBreaksEnabled: false);

            Assert.IsTrue(model.IsEditing);
            Assert.AreEqual(9 * 60, model.StartMinutes, "an existing user's saved start time is untouched");
            Assert.AreEqual(18 * 60, model.EndMinutes, "an existing user's saved end time is untouched");
            Assert.IsTrue(model.UseAI, "a saved schedule with AI on stays on");
            Assert.IsTrue(model.SkipMicroBreaks,
                "the schedule stores whether microbreaks are ENABLED; the editor offers to SKIP them");
        }

        [TestMethod]
        public void AnExistingCrossMidnightScheduleClampsToTheEndOfTheDay()
        {
            var model = new BlockingScheduleEditorModel();
            model.ApplySavedSchedule(startMinutes: 22 * 60, endMinutes: 2 * 60,
                                     isAIBlockingEnabled: false, isMicroBreaksEnabled: true);

            Assert.AreEqual(22 * 60, model.StartMinutes);
            Assert.AreEqual(BlockingScheduleDefaults.MinutesPerDay, model.EndMinutes,
                "a window running past midnight clamps to the end of the day");
            Assert.IsFalse(model.SkipMicroBreaks);
        }

        [TestMethod]
        public void AnExistingScheduleNeverCollapsesBelowAQuarterHourOnTheSlider()
        {
            var model = new BlockingScheduleEditorModel();
            model.ApplySavedSchedule(startMinutes: 9 * 60, endMinutes: 9 * 60 + 1,
                                     isAIBlockingEnabled: false, isMicroBreaksEnabled: true);
            Assert.AreEqual(9 * 60 + 15, model.EndMinutes);
        }
    }
}
