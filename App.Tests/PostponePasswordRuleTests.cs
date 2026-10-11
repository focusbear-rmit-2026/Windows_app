using Focus_Bear.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The one rule every postpone entry point asks (Focus Summary, the resume popup, HabitWindow). A user who
    /// only wanted a password to postpone the evening routine after the cutoff was let through without one,
    /// because the Focus Summary checked a skip setting instead.
    /// </summary>
    [TestClass]
    public class PostponePasswordRuleTests
    {
        private static bool Rule(CurrentActivityType routine, bool passwordSet = true, bool morning = false,
                                 bool evening = false, bool eveningAfterCutOff = false, bool cutOffPassed = false) =>
            AppSettings.PostponeRequiresPassword(routine, passwordSet, morning, evening, eveningAfterCutOff, cutOffPassed);

        [TestMethod]
        public void WithoutAPasswordNothingIsGated()
        {
            Assert.IsFalse(Rule(CurrentActivityType.MORNING_HABITS, passwordSet: false, morning: true));
            Assert.IsFalse(Rule(CurrentActivityType.EVENING_HABITS, passwordSet: false, evening: true, eveningAfterCutOff: true, cutOffPassed: true));
        }

        [TestMethod]
        public void MorningObeysPostponeMorningOnly()
        {
            Assert.IsTrue(Rule(CurrentActivityType.MORNING_HABITS, morning: true));
            Assert.IsFalse(Rule(CurrentActivityType.MORNING_HABITS, evening: true, eveningAfterCutOff: true, cutOffPassed: true));
        }

        [TestMethod]
        public void AfterTheCutoff_TheEveningObeysPostponeAfterCutoff()
        {
            // The reported case: only "postpone evening after cutoff" on, 19:52 with a 17:00 cutoff.
            Assert.IsTrue(Rule(CurrentActivityType.EVENING_HABITS, eveningAfterCutOff: true, cutOffPassed: true));
            Assert.IsFalse(Rule(CurrentActivityType.EVENING_HABITS, evening: true, cutOffPassed: true));
        }

        [TestMethod]
        public void BeforeTheCutoff_TheEveningObeysPostponeEvening()
        {
            Assert.IsTrue(Rule(CurrentActivityType.EVENING_HABITS, evening: true));
            Assert.IsFalse(Rule(CurrentActivityType.EVENING_HABITS, eveningAfterCutOff: true));
        }

        [TestMethod]
        public void TheResumePopupGatesThePostponedRoutine_EvenWhenAFocusSessionClearedTheActivityType()
        {
            // Postponing during a focus session drops the activity type to NONE; the resume popup's Postpone
            // button then asked the rule about NONE and let the user through without the password.
            Assert.AreEqual(CurrentActivityType.EVENING_HABITS,
                AppSettings.PostponedRoutine(CurrentActivityType.NONE, true, CurrentActivityType.EVENING_HABITS));
            Assert.AreEqual(CurrentActivityType.MORNING_HABITS,
                AppSettings.PostponedRoutine(CurrentActivityType.MORNING_HABITS, true, CurrentActivityType.MORNING_HABITS));
            // Nothing postponed: the current activity stands.
            Assert.AreEqual(CurrentActivityType.NONE,
                AppSettings.PostponedRoutine(CurrentActivityType.NONE, false, CurrentActivityType.EVENING_HABITS));
        }

        [TestMethod]
        public void ExtendingAnEveningPostponeAfterTheCutoffMakesItAPostCutoffPostpone()
        {
            // Extending kept the flag from the first postpone, so a postpone first made before the cutoff and
            // extended after it still counted as a pre-cutoff one.
            Assert.IsTrue(AppSettings.PostponedAfterCutoff(false, CurrentActivityType.EVENING_HABITS, cutOffPassed: true));
            Assert.IsTrue(AppSettings.PostponedAfterCutoff(true, CurrentActivityType.EVENING_HABITS, cutOffPassed: false), "once after, stays after");
            Assert.IsFalse(AppSettings.PostponedAfterCutoff(false, CurrentActivityType.EVENING_HABITS, cutOffPassed: false));
            Assert.IsFalse(AppSettings.PostponedAfterCutoff(false, CurrentActivityType.MORNING_HABITS, cutOffPassed: true), "the cutoff is an evening rule");
        }

        [TestMethod]
        public void OtherActivitiesAreNeverGated()
        {
            Assert.IsFalse(Rule(CurrentActivityType.BREAK_ACTIVITY, morning: true, evening: true, eveningAfterCutOff: true, cutOffPassed: true));
        }
    }
}
