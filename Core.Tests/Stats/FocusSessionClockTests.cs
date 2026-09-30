using System;
using FocusBear.Core.Stats;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Stats
{
    /// <summary>Ported from the Mac's FocusSessionTests — the countdown and progress arithmetic.</summary>
    [TestClass]
    public class FocusSessionClockTests
    {
        private static readonly DateTime Now = new DateTime(2026, 9, 20, 9, 0, 0, DateTimeKind.Utc);
        private static readonly FocusSessionDurationUnits Units = new FocusSessionDurationUnits("hr", "min", "sec");

        [TestMethod]
        public void RemainingSecondsCountsDownToTheEndDate()
        {
            Assert.AreEqual(90, FocusSessionClock.RemainingSeconds(Now.AddSeconds(90), Now),
                "Ninety seconds out reads as ninety seconds");
        }

        [TestMethod]
        public void RemainingSecondsNeverGoesNegative()
        {
            Assert.AreEqual(0, FocusSessionClock.RemainingSeconds(Now.AddSeconds(-30), Now),
                "An overrun session reads zero, not backwards");
        }

        [TestMethod]
        public void HeadlineShowsMinutesAndSeconds()
        {
            Assert.AreEqual("24 min 36 sec", FocusSessionClock.Headline(24 * 60 + 36, Units),
                "This is the format the design specifies");
        }

        [TestMethod]
        public void HeadlineShowsHoursAndMinutesPastAnHour()
        {
            Assert.AreEqual("1 hr 24 min", FocusSessionClock.Headline(3600 + 24 * 60 + 36, Units),
                "Seconds are dropped once the session is over an hour");
        }

        [TestMethod]
        public void HeadlineShowsSecondsOnlyInTheLastMinute()
        {
            Assert.AreEqual("36 sec", FocusSessionClock.Headline(36, Units));
        }

        [TestMethod]
        public void HeadlineHandlesZero()
        {
            Assert.AreEqual("0 sec", FocusSessionClock.Headline(0, Units), "A finished session reads zero seconds");
        }

        [TestMethod]
        public void ClockTextPadsMinutesAndSeconds()
        {
            Assert.AreEqual("07:04", FocusSessionClock.ClockText(7 * 60 + 4), "Under an hour the clock is mm:ss");
        }

        [TestMethod]
        public void ClockTextAddsHoursPastAnHour()
        {
            Assert.AreEqual("01:07:04", FocusSessionClock.ClockText(3600 + 7 * 60 + 4));
        }

        [TestMethod]
        public void ProgressIsTheFractionElapsed()
        {
            Assert.AreEqual(0.25, FocusSessionClock.Progress(300, 1200), 0.0001);
        }

        [TestMethod]
        public void ProgressIsZeroWhenTotalIsUnknown()
        {
            Assert.AreEqual(0, FocusSessionClock.Progress(300, 0), "Without a session length there is nothing to divide by");
        }

        [TestMethod]
        public void ProgressIsClampedToOne()
        {
            Assert.AreEqual(1, FocusSessionClock.Progress(2000, 1200), "An overrun session fills the bar, not overflows it");
        }

        [TestMethod]
        public void MinutesRemainingRoundsUpSoAlmostDoneNeverReadsZero()
        {
            Assert.AreEqual(1, FocusSessionClock.MinutesRemaining(Now.AddSeconds(20), Now),
                "Twenty seconds left reads 1m, not 0m");
            Assert.AreEqual(29, FocusSessionClock.MinutesRemaining(Now.AddMinutes(29), Now),
                "An exact 29 minutes reads 29m");
            Assert.AreEqual(30, FocusSessionClock.MinutesRemaining(Now.AddSeconds(29 * 60 + 5), Now),
                "A part-minute rounds up to the next whole minute");
        }

        [TestMethod]
        public void MinutesRemainingIsZeroOnceTheSessionHasPassed()
        {
            Assert.AreEqual(0, FocusSessionClock.MinutesRemaining(Now.AddSeconds(-120), Now),
                "An overrun session has no minutes left rather than negative ones");
        }

        /// <summary>
        /// Regression: the live session length can still hold a PREVIOUS session's value when a new
        /// one starts. Reading it alone gave a total shorter than the time remaining, so elapsed
        /// clamped to zero — the bar sat at 00:00 and the total disagreed with the countdown.
        /// </summary>
        [TestMethod]
        public void SessionTotalIsNeverShorterThanTheTimeRemaining()
        {
            int total = FocusSessionClock.SessionTotalSeconds(0, 270, 1192);

            Assert.IsTrue(total >= 1192, "A stale live length must not make the total shorter than what is left");
            Assert.IsTrue(total - 1192 > -1, "…so elapsed can never be negative");
        }

        [TestMethod]
        public void SessionTotalPrefersThePlannedLength()
        {
            Assert.AreEqual(1800, FocusSessionClock.SessionTotalSeconds(30, 0, 1200), "A 30-minute session totals 30 minutes");
        }

        [TestMethod]
        public void SessionTotalFollowsAnExtendedSession()
        {
            Assert.AreEqual(2400, FocusSessionClock.SessionTotalSeconds(30, 2400, 2300),
                "Extending grows the total the bar measures against");
        }

        [TestMethod]
        public void ProgressAdvancesAsTheSessionRuns()
        {
            int total = FocusSessionClock.SessionTotalSeconds(30, 0, 1800);
            int laterTotal = FocusSessionClock.SessionTotalSeconds(30, 0, 900);

            Assert.AreEqual(0, FocusSessionClock.Progress(total - 1800, total), 0.0001, "Nothing elapsed at the start");
            Assert.AreEqual(0.5, FocusSessionClock.Progress(laterTotal - 900, laterTotal), 0.0001,
                "Half the session gone is half the bar");
        }

        [TestMethod]
        public void CompactElapsedReadsCoarselySoTheCardIsNotTicking()
        {
            Assert.AreEqual("42 sec", FocusSessionClock.CompactElapsed(42, Units), "Seconds in the first minute");
            Assert.AreEqual("12 min", FocusSessionClock.CompactElapsed(12 * 60 + 30, Units),
                "Whole minutes after that — no second-by-second churn");
            Assert.AreEqual("1 hr 5 min", FocusSessionClock.CompactElapsed(3600 + 5 * 60, Units),
                "Hours and minutes past the hour");
        }
    }

    /// <summary>Ported from the Mac's FocusSessionTests — time on the current task.</summary>
    [TestClass]
    public class FocusSessionTaskTimerTests
    {
        private static readonly DateTime Start = new DateTime(2026, 9, 20, 9, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void CountsFromWhenTheTaskBecameActive()
        {
            FocusSessionTaskTimer timer = new FocusSessionTaskTimer();
            timer.Activate("a", Start);

            Assert.AreEqual(90, timer.ElapsedSeconds(Start.AddSeconds(90)));
        }

        [TestMethod]
        public void RestartsWhenTheActiveTaskChanges()
        {
            FocusSessionTaskTimer timer = new FocusSessionTaskTimer();
            timer.Activate("a", Start);
            timer.Activate("b", Start.AddSeconds(120));

            Assert.AreEqual(30, timer.ElapsedSeconds(Start.AddSeconds(150)),
                "Switching tasks starts the new task's count from zero");
        }

        [TestMethod]
        public void IgnoresBeingToldTheSameTaskAgain()
        {
            FocusSessionTaskTimer timer = new FocusSessionTaskTimer();
            timer.Activate("a", Start);
            timer.Activate("a", Start.AddSeconds(60));

            Assert.AreEqual(90, timer.ElapsedSeconds(Start.AddSeconds(90)), "A refresh must not reset the clock");
        }

        [TestMethod]
        public void DoesNotAccrueWhileThePauseIsOn()
        {
            FocusSessionTaskTimer timer = new FocusSessionTaskTimer();
            timer.Activate("a", Start);
            timer.Pause(Start.AddSeconds(60));

            Assert.AreEqual(60, timer.ElapsedSeconds(Start.AddSeconds(600)), "A paused session accrues no time on the task");

            timer.Resume(Start.AddSeconds(600));

            Assert.AreEqual(90, timer.ElapsedSeconds(Start.AddSeconds(630)), "Resuming picks up where it left off");
        }

        [TestMethod]
        public void IsEmptyWithNoTask()
        {
            FocusSessionTaskTimer timer = new FocusSessionTaskTimer();
            timer.Activate(null, Start);

            Assert.AreEqual(0, timer.ElapsedSeconds(Start.AddSeconds(600)), "No task means nothing to count");
            Assert.IsFalse(timer.IsRunning, "…and nothing running");
        }
    }

    /// <summary>
    /// Ported from the Mac's FocusSessionTests — the intention and the current task are separate
    /// values, and only the task may be changed from outside the goal editor.
    /// </summary>
    [TestClass]
    public class FocusSessionWorkContextTests
    {
        [TestMethod]
        public void KeepsTheIntentionAndTheTaskApart()
        {
            FocusSessionWorkContext context = FocusSessionWorkContext.Resolve("Re-do proposal", "call schools");

            Assert.AreEqual("Re-do proposal", context.CurrentTask, "The task is what the user is doing now");
            Assert.AreEqual("call schools", context.Intention, "The intention is the session's stated goal");
        }

        [TestMethod]
        public void HasNoTaskWhenTheQueueIsEmpty()
        {
            FocusSessionWorkContext context = FocusSessionWorkContext.Resolve(null, "call schools");

            Assert.IsNull(context.CurrentTask, "No queued task means no current task, not a copy of the goal");
            Assert.AreEqual("call schools", context.Intention, "The goal is unaffected");
        }

        [TestMethod]
        public void TreatsABlankTaskAsAbsentAndTrims()
        {
            Assert.IsNull(FocusSessionWorkContext.Resolve("  ", "g").CurrentTask, "A blank title is not a task");
            Assert.AreEqual("Re-do proposal", FocusSessionWorkContext.Resolve(" Re-do proposal ", " g ").CurrentTask,
                "Values are sent onward, so they are trimmed");
        }

        /// <summary>
        /// Regression for the strictness bypass: a caller that "set the current task" by writing the
        /// intention edited the goal, which a super-strict session forbids.
        /// </summary>
        [TestMethod]
        public void SuperStrictSessionForbidsChangingTheIntentionButNeverTheTask()
        {
            Assert.IsFalse(FocusSessionIntentionLock.MayChangeIntention(true), "A super-strict session locks the goal");
            Assert.IsTrue(FocusSessionIntentionLock.MayChangeIntention(false), "Other strictness levels allow editing it");
            Assert.IsTrue(FocusSessionIntentionLock.MayChangeCurrentTask(true),
                "Picking what you work on next is never locked — only the stated goal is");
            Assert.IsTrue(FocusSessionIntentionLock.MayChangeCurrentTask(false));
        }
    }
}
