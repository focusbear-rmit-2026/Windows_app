using FocusBear.Core.Postpone;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Postpone
{
    [TestClass]
    public class FocusSessionSyncTests
    {
        private const string BuddyMode = PostponeFocusSession.ModeIdPrefix + "0f3c9a";

        [TestMethod]
        public void ARemoteStartNeverReplacesARunningSession()
        {
            Assert.IsTrue(FocusSessionSync.IgnoresRemoteStart(true, false, true), "report: the meeting session took over the buddy session while habits were postponed");
            Assert.IsTrue(FocusSessionSync.IgnoresRemoteStart(true, false, false));
            Assert.IsTrue(FocusSessionSync.IgnoresRemoteStart(false, true, false), "a routine in progress");
            Assert.IsFalse(FocusSessionSync.IgnoresRemoteStart(false, true, true), "a postponed routine with no session may take one");
            Assert.IsFalse(FocusSessionSync.IgnoresRemoteStart(false, false, false));
        }

        [TestMethod]
        public void TheBackendHearsOfTheBuddySessionUnderTheDefaultMode()
        {
            Assert.AreEqual("default-uuid", FocusSessionSync.BackendFocusModeId(BuddyMode, "default-uuid"), "the server rejects the temporary id");
            Assert.AreEqual("deep-uuid", FocusSessionSync.BackendFocusModeId("deep-uuid", "default-uuid"));
            Assert.AreEqual(BuddyMode, FocusSessionSync.BackendFocusModeId(BuddyMode, null), "no default mode: unchanged");
        }

        [TestMethod]
        public void TheEndScreenFinishesOnlyALivePersonalSession()
        {
            var server = new ServerFocusSession();
            Assert.IsFalse(server.HasSessionToFinish, "nothing started");

            server.PersonalStarted();
            Assert.IsTrue(server.HasSessionToFinish);
            server.Finished();
            Assert.IsFalse(server.HasSessionToFinish, "a second finish would be for a stale id");

            // 26 Sep 12:02: a meeting session finished, then a class session started and was stopped.
            server.PersonalStarted();
            server.ClassSessionStarted();
            Assert.IsFalse(server.HasSessionToFinish, "a class session is ended through class-management");

            server.PersonalStarted();
            server.Finished();   // focus-mode-finished from another device
            Assert.IsFalse(server.HasSessionToFinish);
        }
    }
}
