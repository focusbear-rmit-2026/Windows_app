using System;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>
    /// The cutoff's say over a routine postpone, ported case-for-case from the Mac's
    /// <c>CutoffPostponeLimitTests</c>: a postpone may not end after the user's cutoff time, and once
    /// the cutoff has passed a user who asked for a password must be asked for one.
    ///
    /// Anchored on the incident the rules come from (report 2026-09-17_17-05-10): cutoff 17:00, the
    /// running evening postpone abandoned at 17:00:02 because the cutoff arrived, the Focus Summary
    /// shown at 17:00:11, and at 17:00:23 the AI buddy granting ten minutes — to 17:10, past the
    /// cutoff, with no password asked.
    /// </summary>
    [TestClass]
    public class CutoffPostponeLimitTests
    {
        /// <summary>17 September 2026, 17:00:00 local — the incident's cutoff time.</summary>
        private static readonly DateTime Cutoff = new DateTime(2026, 9, 17, 17, 0, 0, DateTimeKind.Local);

        /// <summary><paramref name="minutes"/> either side of the cutoff (negative = before it).</summary>
        private static DateTime Time(double minutes) => Cutoff.AddMinutes(minutes);

        private static int Granted(int requested, DateTime now, DateTime? cutoff, bool isAfterCutoff = false,
                                   DateTime? running = null)
        {
            return CutoffPostponeLimit.GrantedMinutes(requested, now, cutoff, isAfterCutoff, running);
        }

        [TestMethod]
        public void APostponeThatFitsBeforeTheCutoffIsUntouched()
        {
            // 16:30, cutoff 17:00, asking for 20 minutes: it ends at 16:50, so there is nothing to clamp.
            Assert.AreEqual(20, Granted(20, Time(-30), Cutoff));
        }

        [TestMethod]
        public void APostponeCrossingTheCutoffIsClampedToIt()
        {
            // 16:48, cutoff 17:00: the dropdown's 30 becomes the 12 minutes that are actually left.
            Assert.AreEqual(12, Granted(30, Time(-12), Cutoff));
        }

        [TestMethod]
        public void TheBuddysApprovedDurationIsClampedToo()
        {
            // The incident's shape, replayed a few minutes earlier: at 16:55 the buddy's 10-minute grant
            // comes back as 5 — the postpone must hand the user to their routine AT the cutoff.
            Assert.AreEqual(5, Granted(10, Time(-5), Cutoff));
        }

        [TestMethod]
        public void PartialMinutesRoundDownSoThePostponeNeverOverrunsTheCutoff()
        {
            // 16:47:30 — 12.5 minutes left. Rounding up would end the postpone half a minute late.
            Assert.AreEqual(12, Granted(30, Time(-12.5), Cutoff));
        }

        [TestMethod]
        public void NoCutoffConfiguredLeavesTheRequestAlone()
        {
            Assert.AreEqual(90, Granted(90, Time(-5), null));
        }

        [TestMethod]
        public void PastTheCutoffThereIsNothingToClampToSoTheRequestStands()
        {
            // With no cutoff ahead the clamp has no ceiling to offer; the password rule owns that case.
            // A silent zero-minute postpone would be worse than either answer — it would drop the user
            // straight back into their routine with no explanation.
            Assert.AreEqual(10, Granted(10, Time(0.5), Cutoff, isAfterCutoff: true));
        }

        [TestMethod]
        public void TheOvernightWindowIsDrivenByTheFlagNotTheDate()
        {
            // 03:00 with a 17:00 cutoff: today's cutoff moment is 14 hours away, but we are after
            // YESTERDAY's cutoff. The flag is what knows that, which is why it is passed in.
            DateTime threeAm = Cutoff.AddHours(-14);

            Assert.AreEqual(30, Granted(30, threeAm, Cutoff, isAfterCutoff: true));
            Assert.AreEqual(30, Granted(30, threeAm, Cutoff),
                            "14 hours of room — the ceiling is far above the request either way");
        }

        [TestMethod]
        public void AnAlreadyRunningLongerPostponeIsNotCutShortByTheCutoff()
        {
            // 16:50, cutoff 17:00, a postpone already running to 17:20 (Late No More covering a meeting).
            // Clamping to 10 would resume the routine mid-call — exactly what meeting coverage prevents.
            // The running postpone wins; the app still abandons it at the cutoff.
            Assert.AreEqual(30, Granted(30, Time(-10), Cutoff, running: Time(20)));
        }

        [TestMethod]
        public void TheRunningPostponeFloorAppliesEvenWhenItExceedsTheRequest()
        {
            // A short request cannot shorten a long postpone either: 5 asked, 20 minutes still running.
            Assert.AreEqual(20, Granted(5, Time(-40), Cutoff, running: Time(-20)));
        }

        [TestMethod]
        public void AShorterRunningPostponeDoesNotRaiseTheClampedResult()
        {
            // Running to 16:55, cutoff 17:00, asking 30 at 16:48: the ceiling (12) still governs.
            Assert.AreEqual(12, Granted(30, Time(-12), Cutoff, running: Time(-5)));
        }

        [TestMethod]
        public void MinutesUntilCutoffIsWhatTheDropdownIsTrimmedTo()
        {
            Assert.AreEqual(12, CutoffPostponeLimit.MinutesUntilCutoff(Time(-12), Cutoff, false));
            Assert.IsNull(CutoffPostponeLimit.MinutesUntilCutoff(Time(-12), null, false),
                          "no cutoff configured — no ceiling");
            Assert.IsNull(CutoffPostponeLimit.MinutesUntilCutoff(Time(1), Cutoff, true),
                          "already past it — no ceiling");
            Assert.IsNull(CutoffPostponeLimit.MinutesUntilCutoff(Time(-0.5), Cutoff, false),
                          "less than a whole minute left is no room at all, not a zero-minute postpone");
        }

        [TestMethod]
        public void AfterTheCutoffAUserWhoAskedForAPasswordIsAskedForOne()
        {
            // The incident: 17:00:23, cutoff 17:00, "require a password to postpone after the cutoff" on.
            Assert.IsTrue(CutoffPostponeLimit.RequiresPassword(hasCutoff: true, isAfterCutoff: true,
                                                               isCutoffStrict: true));
        }

        [TestMethod]
        public void BeforeTheCutoffNoPasswordIsAsked()
        {
            Assert.IsFalse(CutoffPostponeLimit.RequiresPassword(hasCutoff: true, isAfterCutoff: false,
                                                                isCutoffStrict: true));
        }

        [TestMethod]
        public void WithoutTheAfterCutoffSettingNothingChanges()
        {
            // The setting is the user's declaration that after the cutoff is different from before it.
            // Someone who never made it keeps the postpone they have always had.
            Assert.IsFalse(CutoffPostponeLimit.RequiresPassword(hasCutoff: true, isAfterCutoff: true,
                                                                isCutoffStrict: false));
        }

        [TestMethod]
        public void WithNoCutoffConfiguredNothingIsGated()
        {
            Assert.IsFalse(CutoffPostponeLimit.RequiresPassword(hasCutoff: false, isAfterCutoff: true,
                                                                isCutoffStrict: true));
        }
    }
}
