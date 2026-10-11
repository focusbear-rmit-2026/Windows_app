using System;
using System.Linq;
using FocusBear.Core.Postpone;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Postpone
{
    [TestClass]
    public class AccountabilityBuddyTests
    {
        [TestMethod]
        public void TheVerdictIsReadFromTheServersShape()
        {
            var verdict = AccountabilityVerdict.Read(@"{ ""verdict"": ""approve"", ""intention"": ""Prepare the standup"", ""clampedDurationMinutes"": 30,
                ""allowedApps"": [""Outlook"", """", ""Outlook""], ""allowedUrls"": [""github.com""], ""reason"": ""Sounds important."" }");
            Assert.IsTrue(verdict.Approved);
            Assert.AreEqual(30, verdict.ClampedDurationMinutes);
            CollectionAssert.AreEqual(new[] { "Outlook" }, verdict.AllowedApps.ToArray());
            Assert.AreEqual("Prepare the standup", verdict.GoalOr("raw"));
            Assert.AreEqual("Sounds important.", verdict.Message);

            var denied = AccountabilityVerdict.Read(@"{ ""verdict"": ""deny"", ""reason"": ""Do the habit first."" }");
            Assert.IsFalse(denied.Approved);
            Assert.AreEqual(25, denied.ClampedDurationMinutes, "no duration falls back to 25");
            Assert.AreEqual("raw goal", denied.GoalOr(" raw goal "));
            Assert.IsNull(AccountabilityVerdict.Read("<html>"));
            Assert.IsNull(AccountabilityVerdict.Read(@"{ ""message"": ""no verdict"" }"));
        }

        [TestMethod]
        public void DurationsAreClampedToFiveToOneEighty()
        {
            Assert.AreEqual(5, new AccountabilityVerdict { DurationMinutes = 2 }.ClampedDurationMinutes);
            Assert.AreEqual(180, new AccountabilityVerdict { DurationMinutes = 600 }.ClampedDurationMinutes);
        }

        [TestMethod]
        public void TheRequestKeepsToTheBackendLimits()
        {
            var body = AccountabilityRequest.Build(
                new[] { new AccountabilityMessage(true, "What instead?"), new AccountabilityMessage(false, new string('a', 600)), new AccountabilityMessage(false, "  ") },
                new[] { AccountabilityRequest.Line("Meditate", 10) }, new string[0], null, null);
            var messages = (Newtonsoft.Json.Linq.JArray)body["messages"];
            Assert.AreEqual(2, messages.Count, "blank turns are dropped");
            Assert.AreEqual("assistant", (string)messages[0]["role"]);
            Assert.AreEqual(500, ((string)messages[1]["content"]).Length);
            Assert.AreEqual("Meditate (~10 min)", (string)body["uncompletedHabits"][0]);
            Assert.AreEqual("English", (string)body["language"]);
            Assert.IsNull(body["currentMeetingTitle"]);
            Assert.AreEqual("😀", AccountabilityRequest.Bounded("😀x", 2), "a surrogate pair is never split");
            Assert.AreEqual("Report [#Work] (~30 min)", AccountabilityRequest.Line("Report", 30, "Work"));
        }

        [TestMethod]
        public void ARepeatedOrLongDenialIsStuck()
        {
            Assert.IsFalse(AccountabilityLoopGuard.IsStuck(new[] { "What exactly?" }));
            Assert.IsTrue(AccountabilityLoopGuard.IsStuck(new[] { "I still need one concrete answer: what exactly?", "Hmm.", "I still need one concrete answer — what exactly?" }));
            Assert.IsTrue(AccountabilityLoopGuard.IsStuck(new[] { "a", "b", "c", "d" }));
            Assert.IsFalse(AccountabilityLoopGuard.IsStuck(new[] { "a", "b", "c" }));
        }

        [TestMethod]
        public void TheHoldNeedsTenUnbrokenSeconds()
        {
            var hold = new AccountabilityHold();
            var start = new DateTime(2026, 9, 25, 9, 0, 0);
            hold.Press(start);
            Assert.AreEqual(5, hold.SecondsLeft(start.AddSeconds(5)));
            hold.Release();
            hold.Press(start.AddSeconds(6));
            Assert.IsFalse(hold.IsComplete(start.AddSeconds(15)), "letting go starts the ten seconds again");
            Assert.IsTrue(hold.IsComplete(start.AddSeconds(16)));
        }
    

        [TestMethod]
        public void AFailedCallSaysWhetherTheUserIsOfflineOrTheServerIsDown()
        {
            Assert.AreEqual(AccountabilityRecoveryReason.Offline, AccountabilityReply.From(false, false, null).Failure);
            Assert.IsNull(AccountabilityReply.From(false, true, @"{ ""verdict"": ""approve"" }").Verdict, "no connectivity means no verdict, whatever the body");
            Assert.AreEqual(AccountabilityRecoveryReason.Unavailable, AccountabilityReply.From(true, false, null).Failure, "sent then failed (timeout, 5xx) is the server, not the network");
            Assert.AreEqual(AccountabilityRecoveryReason.Unavailable, AccountabilityReply.From(true, true, "<html>").Failure, "an unusable body is the server too");
            var answered = AccountabilityReply.From(true, true, @"{ ""verdict"": ""deny"", ""reason"": ""Do the habit first."" }");
            Assert.IsNotNull(answered.Verdict);
            Assert.AreEqual("Do the habit first.", answered.Verdict.Message);
        }

        [TestMethod]
        public void OnlyAStuckDenialShowsTheCardAndItStartsAsOneQuietQuestion()
        {
            var denied = new AccountabilityVerdict { Approved = false, Message = "Legitimate work alone does not establish why your habits must wait." };
            Assert.IsNull(AccountabilityRecovery.ForVerdict(denied, new[] { denied.Message }), "a first denial stays in the chat, with no card and no 'your buddy didn't answer'");
            Assert.AreEqual(AccountabilityRecoveryReason.Stuck, AccountabilityRecovery.ForVerdict(denied, new[] { denied.Message, "Why now?", denied.Message }));
            var approved = new AccountabilityVerdict { Approved = true, Message = "a" };
            Assert.IsNull(AccountabilityRecovery.ForVerdict(approved, new[] { "a", "b", "c", "d" }), "an approval never offers the manual override");

            Assert.IsTrue(AccountabilityRecovery.StartsCollapsed(AccountabilityRecoveryReason.Stuck));
            Assert.IsFalse(AccountabilityRecovery.StartsCollapsed(AccountabilityRecoveryReason.Offline), "an outage opens straight onto the explanation and the controls");
            Assert.IsFalse(AccountabilityRecovery.StartsCollapsed(AccountabilityRecoveryReason.Unavailable));
            CollectionAssert.Contains(AccountabilityHold.DurationChoices, AccountabilityRecovery.DefaultMinutes, "the preselected duration is one of the choices");
        }
    }
}
