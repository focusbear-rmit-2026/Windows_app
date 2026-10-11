using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Blocking
{
    /// <summary>
    /// The ladder in <see cref="AiBlockingDecision"/>, rung by rung, plus the precedence between
    /// rungs — which is the part a reimplementation gets wrong. Every case here was read off the
    /// Mac's two <c>isAIBlockingEnabled()</c> functions.
    /// </summary>
    [TestClass]
    public class AiBlockingDecisionTests
    {
        /// <summary>
        /// The baseline is deliberately "AI would be on": global toggle on, nothing else happening.
        /// A test that flips one field then asserts false is therefore testing that field alone.
        /// </summary>
        private static AiBlockingInputs AiWouldBeOn() => new AiBlockingInputs
        {
            GlobalAiCheckUrlEnabled = true,
        };

        private static AiScheduleContext AllowListSchedule(bool? aiEnabled, bool ownFlag = false) =>
            new AiScheduleContext
            {
                BlockList = new ScheduleBlockList { AiEnabled = aiEnabled, IsAllowedListApproach = true },
                OwnAiFlag = ownFlag,
            };

        // ---- the plain global case -------------------------------------------------------------

        [TestMethod]
        public void GlobalToggleDecidesWhenNothingElseIsRunning()
        {
            Assert.IsTrue(AiBlockingDecision.IsAiBlockingEnabled(AiWouldBeOn()));

            AiBlockingInputs off = AiWouldBeOn();
            off.GlobalAiCheckUrlEnabled = false;
            Assert.IsFalse(AiBlockingDecision.IsAiBlockingEnabled(off));
        }

        // ---- rung 1: the snooze ----------------------------------------------------------------

        [TestMethod]
        public void SnoozeVetoesEverythingBelowIt()
        {
            AiBlockingInputs i = AiWouldBeOn();
            i.BlockingPopupsSnoozed = true;
            // Set every lower rung to its most emphatic "yes" — the snooze still wins.
            i.AccountabilityBuddySessionActive = true;
            i.TeacherSessionActive = true;
            i.TeacherAiEnabledForAllowList = true;

            Assert.IsFalse(AiBlockingDecision.IsAiBlockingEnabled(i),
                           "a paused pop-up must not be answered by a question we cannot show");
        }

        // ---- rung 2: the teacher ---------------------------------------------------------------

        [TestMethod]
        public void TeacherChoiceOverridesTheStudentInBothDirections()
        {
            AiBlockingInputs teacherSaysYes = AiWouldBeOn();
            teacherSaysYes.TeacherSessionActive = true;
            teacherSaysYes.TeacherAiEnabledForAllowList = true;
            teacherSaysYes.GlobalAiCheckUrlEnabled = false;
            Assert.IsTrue(AiBlockingDecision.IsAiBlockingEnabled(teacherSaysYes),
                          "the teacher can turn AI on for a student who has it off globally");

            AiBlockingInputs teacherSaysNo = AiWouldBeOn();
            teacherSaysNo.TeacherSessionActive = true;
            teacherSaysNo.TeacherAiEnabledForAllowList = false;
            Assert.IsFalse(AiBlockingDecision.IsAiBlockingEnabled(teacherSaysNo),
                           "and off for a student who has it on");
        }

        [TestMethod]
        public void TeacherSessionOutranksABuddySession()
        {
            AiBlockingInputs i = AiWouldBeOn();
            i.TeacherSessionActive = true;
            i.TeacherAiEnabledForAllowList = false;
            i.AccountabilityBuddySessionActive = true;

            Assert.IsFalse(AiBlockingDecision.IsAiBlockingEnabled(i),
                           "buddy sessions force AI on, but not over a teacher who said no");
        }

        // ---- rung 3: the buddy session ---------------------------------------------------------

        [TestMethod]
        public void BuddySessionForcesAiOnEvenWithTheGlobalToggleOff()
        {
            AiBlockingInputs i = AiWouldBeOn();
            i.AccountabilityBuddySessionActive = true;
            i.GlobalAiCheckUrlEnabled = false;
            i.RunningFocusModeAiEnabled = false;
            i.FocusModeRunning = true;

            Assert.IsTrue(AiBlockingDecision.IsAiBlockingEnabled(i),
                          "judging everything against the stated goal IS the AI feature");
        }

        // ---- rung 4: the focus mode veto -------------------------------------------------------

        [TestMethod]
        public void RunningFocusModeAiEnabledFalseIsAVeto()
        {
            AiBlockingInputs i = AiWouldBeOn();
            i.FocusModeRunning = true;
            i.RunningFocusModeAiEnabled = false;

            Assert.IsFalse(AiBlockingDecision.IsAiBlockingEnabled(i),
                           "this is the cross-platform bug: the Mac writes is_ai_enabled=false and " +
                           "Windows used to ignore it");
        }

        [TestMethod]
        public void AnAbsentIsAiEnabledMeansOnNotOff()
        {
            AiBlockingInputs i = AiWouldBeOn();
            i.FocusModeRunning = true;
            i.RunningFocusModeAiEnabled = null;

            Assert.IsTrue(AiBlockingDecision.IsAiBlockingEnabled(i),
                          "absent falls through to the global toggle; only an explicit false vetoes");
        }

        [TestMethod]
        public void TheVetoNeverForcesAiOn()
        {
            AiBlockingInputs i = AiWouldBeOn();
            i.FocusModeRunning = true;
            i.RunningFocusModeAiEnabled = true;
            i.GlobalAiCheckUrlEnabled = false;

            Assert.IsFalse(AiBlockingDecision.IsAiBlockingEnabled(i),
                           "is_ai_enabled=true must not override a user who turned AI off globally");
        }

        [TestMethod]
        public void AnExplicitIsAiEnabledTrueLeavesTheGlobalToggleAlone()
        {
            AiBlockingInputs i = AiWouldBeOn();
            i.FocusModeRunning = true;
            i.RunningFocusModeAiEnabled = true;

            Assert.IsTrue(AiBlockingDecision.IsAiBlockingEnabled(i),
                          "true is not a veto either: it falls through to the global toggle, which " +
                          "is on. Only an explicit false stops here.");
        }

        [TestMethod]
        public void AnAllowedAppActivityCountsAsTheFocusModeContext()
        {
            AiBlockingInputs i = AiWouldBeOn();
            i.AllowedFocusModeActivityRunning = true;
            i.RunningFocusModeAiEnabled = false;

            Assert.IsFalse(AiBlockingDecision.IsAiBlockingEnabled(i),
                           "a routine's allowed-app activity is a focus-mode context too");
        }

        [TestMethod]
        public void TheVetoDoesNotApplyWhenAScheduleIsOn()
        {
            AiBlockingInputs i = AiWouldBeOn();
            i.FocusModeRunning = true;
            i.RunningFocusModeAiEnabled = false;   // would veto, but...
            i.AnyCustomScheduleOn = true;          // ...a schedule brings its own block list
            i.ActiveSchedule = AllowListSchedule(aiEnabled: true);

            Assert.IsTrue(AiBlockingDecision.IsAiBlockingEnabled(i),
                          "the schedule's block list answers, not the separately-running focus mode");
        }

        // ---- rung 5: the active schedule -------------------------------------------------------

        [TestMethod]
        public void AScheduleReadsItsBlockListNotTheGlobalToggle()
        {
            AiBlockingInputs i = AiWouldBeOn();
            i.AnyCustomScheduleOn = true;
            i.GlobalAiCheckUrlEnabled = false;
            i.ActiveSchedule = AllowListSchedule(aiEnabled: true);

            Assert.IsTrue(AiBlockingDecision.IsAiBlockingEnabled(i),
                          "once a schedule is on, its block list is the whole answer");

            i.ActiveSchedule = AllowListSchedule(aiEnabled: false);
            Assert.IsFalse(AiBlockingDecision.IsAiBlockingEnabled(i));
        }

        [TestMethod]
        public void AScheduleBlockListWithNoIsAiEnabledDefaultsToOn()
        {
            AiBlockingInputs i = AiWouldBeOn();
            i.AnyCustomScheduleOn = true;
            i.ActiveSchedule = AllowListSchedule(aiEnabled: null);

            Assert.IsTrue(AiBlockingDecision.IsAiBlockingEnabled(i));
        }

        [TestMethod]
        public void ABlacklistNeverRunsAiHoweverItsFlagIsSet()
        {
            AiBlockingInputs i = AiWouldBeOn();
            i.AnyCustomScheduleOn = true;
            i.ActiveSchedule = new AiScheduleContext
            {
                BlockList = new ScheduleBlockList { AiEnabled = true, IsAllowedListApproach = false },
            };

            Assert.IsFalse(AiBlockingDecision.IsAiBlockingEnabled(i),
                           "a blacklist short-circuits un-listed URLs to allowed and never reaches " +
                           "the AI, so reporting true starts a session that blocks nothing");
        }

        [TestMethod]
        public void AnUnresolvableBlockListFallsBackToTheSchedulesOwnFlag()
        {
            AiBlockingInputs i = AiWouldBeOn();
            i.AnyCustomScheduleOn = true;
            i.ActiveSchedule = new AiScheduleContext { BlockList = null, OwnAiFlag = true };
            Assert.IsTrue(AiBlockingDecision.IsAiBlockingEnabled(i),
                          "the hidden checkbox is the last resort, which is why it still exists");

            i.ActiveSchedule = new AiScheduleContext { BlockList = null, OwnAiFlag = false };
            Assert.IsFalse(AiBlockingDecision.IsAiBlockingEnabled(i));
        }

        [TestMethod]
        public void TheSchedulesOwnFlagIsIgnoredWhenTheBlockListResolves()
        {
            AiBlockingInputs i = AiWouldBeOn();
            i.AnyCustomScheduleOn = true;
            i.ActiveSchedule = AllowListSchedule(aiEnabled: false, ownFlag: true);

            Assert.IsFalse(AiBlockingDecision.IsAiBlockingEnabled(i),
                           "AI belongs to the block list; the schedule checkbox must not override it");
        }

        [TestMethod]
        public void ScheduleOnButNoScheduleActuallyInTheOnStateIsOff()
        {
            AiBlockingInputs i = AiWouldBeOn();
            i.AnyCustomScheduleOn = true;
            i.ActiveSchedule = null;

            Assert.IsFalse(AiBlockingDecision.IsAiBlockingEnabled(i),
                           "the two reads race on the Mac and it resolves to off; stay identical");
        }
    }
}
