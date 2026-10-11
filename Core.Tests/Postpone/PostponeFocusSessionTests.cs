using FocusBear.Core.Postpone;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Postpone
{
    [TestClass]
    public class PostponeFocusSessionTests
    {
        private const string BuddyMode = PostponeFocusSession.ModeIdPrefix + "0f3c9a";
        private const string Standup = "attend and stay focused in your meeting: Daily Stand Up Meeting";
        private static readonly AutoStartedSessionKind Buddy = AutoStartedSessionKind.BuddyPostpone;
        private static readonly AutoStartedSessionKind Meeting = AutoStartedSessionKind.Meeting;

        [TestMethod]
        public void OnlyARunningBuddyModeIsLocked()
        {
            Assert.IsTrue(PostponeFocusSession.IsLocked(true, BuddyMode));
            Assert.IsFalse(PostponeFocusSession.IsLocked(false, BuddyMode), "a finished buddy session locks nothing");
            Assert.IsFalse(PostponeFocusSession.IsLocked(true, "6523a1f0-deep-work"), "an ordinary session");
            Assert.IsFalse(PostponeFocusSession.IsLocked(true, null));
            Assert.IsFalse(PostponeFocusSession.IsPostponeMode("AI-BUDDY-x"), "the prefix is the app's own, matched exactly");
        }

        [TestMethod]
        public void SessionsAreClassifiedByModeThenIntention()
        {
            Assert.AreEqual(Buddy, PostponeFocusSession.Classify(true, BuddyMode, "Send the Acme proposal"));
            Assert.AreEqual(Meeting, PostponeFocusSession.Classify(true, "1f2ee09c-meetings", Standup), "the Late No More session from another computer");
            Assert.AreEqual(Meeting, PostponeFocusSession.Classify(true, "1f2ee09c-meetings", "your meeting"), "an untitled meeting");
            Assert.AreEqual(Meeting, PostponeFocusSession.Classify(true, "x", "  Attend and stay focused in your meeting: BDA meeting"));
            Assert.AreEqual(AutoStartedSessionKind.None, PostponeFocusSession.Classify(true, "1f2ee09c-meetings", "Prepare for my meeting"), "a session the user started, even in the Meetings mode");
            Assert.AreEqual(AutoStartedSessionKind.None, PostponeFocusSession.Classify(false, BuddyMode, Standup), "nothing running");
            Assert.IsTrue(PostponeFocusSession.IsUnpausable(Meeting));
            Assert.IsFalse(PostponeFocusSession.IsUnpausable(AutoStartedSessionKind.None));
        }

        [TestMethod]
        public void NeitherKindCanBePaused()
        {
            foreach (var kind in new[] { Buddy, Meeting })
            {
                Assert.AreEqual(PostponeSessionResponse.KeepRunning, PostponeFocusSession.Decide(FocusSessionControl.Pause, kind, false, true), kind.ToString());
                Assert.AreEqual(PostponeSessionResponse.KeepRunning, PostponeFocusSession.Decide(FocusSessionControl.Pause, kind, false, false), kind.ToString());
                Assert.AreEqual(PostponeSessionResponse.Proceed, PostponeFocusSession.Decide(FocusSessionControl.Pause, kind, true, true), "a session somehow already paused may always resume");
            }
        }

        [TestMethod]
        public void StoppingEitherKindHandsBackAPostponedRoutine()
        {
            foreach (var kind in new[] { Buddy, Meeting })
            {
                Assert.AreEqual(PostponeSessionResponse.EndAndResumeRoutine, PostponeFocusSession.Decide(FocusSessionControl.Stop, kind, false, true), kind.ToString());
                Assert.AreEqual(PostponeSessionResponse.EndAndResumeRoutine, PostponeFocusSession.Decide(FocusSessionControl.Stop, kind, true, true), kind.ToString());
                Assert.AreEqual(PostponeSessionResponse.Proceed, PostponeFocusSession.Decide(FocusSessionControl.Stop, kind, false, false), "nothing postponed: an ordinary stop, as the Mac allows");
            }
            Assert.IsTrue(PostponeFocusSession.StopResumesRoutine(Meeting, true));
            Assert.IsFalse(PostponeFocusSession.StopResumesRoutine(Meeting, false));
            Assert.IsFalse(PostponeFocusSession.StopResumesRoutine(AutoStartedSessionKind.None, true));
        }

        [TestMethod]
        public void AnotherDeviceCantEndTheBuddySessionButEndsItsOwnMeeting()
        {
            Assert.AreEqual(PostponeSessionResponse.KeepRunning, PostponeFocusSession.Decide(FocusSessionControl.RemoteFinish, Buddy, false, true));
            Assert.AreEqual(PostponeSessionResponse.KeepRunning, PostponeFocusSession.Decide(FocusSessionControl.RemoteFinish, Buddy, false, false));
            Assert.AreEqual(PostponeSessionResponse.Proceed, PostponeFocusSession.Decide(FocusSessionControl.RemoteFinish, Meeting, false, true), "the Mac that started the meeting session ends it when the meeting ends");
        }

        [TestMethod]
        public void OrdinarySessionsAreUntouched()
        {
            foreach (FocusSessionControl control in System.Enum.GetValues(typeof(FocusSessionControl)))
                Assert.AreEqual(PostponeSessionResponse.Proceed, PostponeFocusSession.Decide(control, AutoStartedSessionKind.None, false, true), control.ToString());
        }

        [TestMethod]
        public void TheBuddysSessionIsSuperStrictSoMinusFiveCantWhittleItAway()
        {
            // Mac startScopedAiFocusSession: the buddy's session is super strict; a meeting only when it covers a routine.
            Assert.IsTrue(PostponeFocusSession.RunsSuperStrict(AutoStartedSessionKind.BuddyPostpone, routineIsPostponed: true));
            Assert.IsTrue(PostponeFocusSession.RunsSuperStrict(AutoStartedSessionKind.Meeting, routineIsPostponed: true));
            Assert.IsFalse(PostponeFocusSession.RunsSuperStrict(AutoStartedSessionKind.Meeting, routineIsPostponed: false));
            Assert.IsFalse(PostponeFocusSession.RunsSuperStrict(AutoStartedSessionKind.None, routineIsPostponed: true));

            var kind = PostponeFocusSession.Classify(true, BuddyMode, "write the report");
            var rules = new FocusBear.Core.Focus.FocusSessionRules(true, false, false,
                isSuperStrict: PostponeFocusSession.RunsSuperStrict(kind, routineIsPostponed: true), isClassSession: false);
            Assert.IsFalse(rules.CanReduce, "-5 is what let the postponed routine's cover shrink to a minute");
            Assert.IsTrue(rules.IsGoalLocked);
        }
    }
}
