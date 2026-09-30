using System;
using FocusBear.Core.Stats;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Stats
{
    /// <summary>
    /// Ported from the Mac's FocusSessionExtensionTests. The product-owner report behind them: "this
    /// screen doesn't seem to be connected to the LLM. It allowed me to continue with this input lol"
    /// — the input being "play games". Only the pure decisions come across; the Mac's AI-blocking
    /// assertions drive app globals and a live settings object (see the port notes).
    /// </summary>
    [TestClass]
    public class FocusSessionExtensionRulesTests
    {
        private const string FallbackPushBack = "Fallback push-back";

        [TestMethod]
        public void ChatExtensionCannotGrantMoreThanRequestedOrApproved()
        {
            AccountabilityChatPurpose extend = AccountabilityChatPurpose.ExtendSession(10);

            Assert.AreEqual(10, extend.GrantedMinutes(25), "The chat may approve longer than the sheet offered");
            Assert.AreEqual(5, extend.GrantedMinutes(5));
            Assert.AreEqual(0, extend.GrantedMinutes(0));
            Assert.AreEqual(25, AccountabilityChatPurpose.PostponeRoutine.GrantedMinutes(25),
                "A routine postpone takes the buddy's duration as it is");
        }

        [TestMethod]
        public void ExtensionReasonBecomesPartOfTheFocusIntention()
        {
            Assert.AreEqual("Write the release notes — Proofread the last section",
                FocusSessionExtensionRules.IntentionAfterExtension("Write the release notes", "Proofread the last section"));
        }

        [TestMethod]
        public void ExtensionReasonIsTheIntentionWhenThereWasNoneYet()
        {
            Assert.AreEqual("Finish the invoice",
                FocusSessionExtensionRules.IntentionAfterExtension(string.Empty, "Finish the invoice"));
        }

        [TestMethod]
        public void BlankExtensionReasonLeavesTheIntentionUnchanged()
        {
            Assert.AreEqual("Write the release notes",
                FocusSessionExtensionRules.IntentionAfterExtension("Write the release notes", "   \n "));
        }

        [TestMethod]
        public void RepeatedExtensionReasonIsNotAppendedTwice()
        {
            string once = FocusSessionExtensionRules.IntentionAfterExtension("Write the release notes", "Proofread");

            Assert.AreEqual(once, FocusSessionExtensionRules.IntentionAfterExtension(once, "Proofread"),
                "Extending repeatedly for the same reason must not grow the intention without bound");
        }

        [TestMethod]
        public void ARepeatedReasonIsRecognisedWhateverItsCase()
        {
            Assert.AreEqual("Write the release notes",
                FocusSessionExtensionRules.IntentionAfterExtension("Write the release notes", "RELEASE NOTES"),
                "The Mac compares case-insensitively (localizedCaseInsensitiveContains)");
        }

        [TestMethod]
        public void AnAlignedReasonExtendsStraightAway()
        {
            Assert.AreEqual(ExtendReasonVerdict.ExtendNow(), FocusSessionExtensionRules.VerdictForExtensionReason(
                new TaskIntentionAlignment(true, string.Empty), FallbackPushBack));
        }

        [TestMethod]
        public void AConflictingReasonPushesBackWithTheServersWords()
        {
            const string serverReason = "Your focus intention is \"Write the release notes\", but this task is \"play games\".";

            Assert.AreEqual(ExtendReasonVerdict.PushBack(serverReason), FocusSessionExtensionRules.VerdictForExtensionReason(
                new TaskIntentionAlignment(false, serverReason), FallbackPushBack));
        }

        [TestMethod]
        public void AConflictWithNoReasonFallsBackToOurOwnWording()
        {
            Assert.AreEqual(ExtendReasonVerdict.PushBack(FallbackPushBack),
                FocusSessionExtensionRules.VerdictForExtensionReason(new TaskIntentionAlignment(false, "  "), FallbackPushBack));
        }

        /// <summary>
        /// A null answer stands for every way the check fails to produce one — unreachable server,
        /// timed-out request, unparseable body. Offline must never cost the user the minutes they
        /// asked for, and a modal sheet must never be left spinning on a stalled request.
        /// </summary>
        [TestMethod]
        public void AnAnswerThatNeverArrivedExtendsAnyway()
        {
            Assert.AreEqual(ExtendReasonVerdict.ExtendNow(),
                FocusSessionExtensionRules.VerdictForExtensionReason(null, FallbackPushBack));
        }

        [TestMethod]
        public void ASessionWithNoIntentionIsNotWorthChecking()
        {
            Assert.IsFalse(FocusSessionExtensionRules.ShouldCheckExtensionReason("  ", "play games"),
                "Nothing to compare the answer against");
        }

        [TestMethod]
        public void ASkippedQuestionIsNotSentToTheServer()
        {
            Assert.IsFalse(FocusSessionExtensionRules.ShouldCheckExtensionReason("Write the release notes", "   "),
                "Skip still means skip");
            Assert.IsTrue(FocusSessionExtensionRules.ShouldCheckExtensionReason("Write the release notes", "play games"));
        }
    }

    /// <summary>
    /// Ported from the Mac's FocusExtensionCutoffLimitTests. The report (2026-09-17_17-05-10, cutoff
    /// 17:00): the buddy approved ten minutes at 17:00:23 and the session ran "until 05:10 pm".
    /// </summary>
    [TestClass]
    public class FocusExtensionCutoffLimitTests
    {
        private static readonly DateTime Now = new DateTime(1970, 1, 12, 13, 46, 40, DateTimeKind.Utc);

        private static int Grant(int request = 5, double? remainingSeconds = null, double? secondsUntilCutoff = 60,
                                 bool after = false, bool strict = true, bool protects = true)
        {
            return FocusExtensionCutoffLimit.GrantedMinutes(
                request,
                Now,
                remainingSeconds.HasValue ? Now.AddSeconds(remainingSeconds.Value) : (DateTime?)null,
                secondsUntilCutoff.HasValue ? Now.AddSeconds(secondsUntilCutoff.Value) : (DateTime?)null,
                after,
                strict,
                protects);
        }

        [TestMethod]
        public void ReportedFiveMinuteExtensionWithFortyTwoSecondsLeftCannotCrossCutoff()
        {
            Assert.AreEqual(0, Grant(remainingSeconds: 42, secondsUntilCutoff: 60));
            Assert.AreEqual(0, Grant(remainingSeconds: 42, secondsUntilCutoff: 59),
                "The last minute is a real limit, never an absent cutoff");
        }

        [TestMethod]
        public void ExtraMinutesAreMeasuredFromTheExistingEnd()
        {
            Assert.AreEqual(2, Grant(remainingSeconds: 10 * 60, secondsUntilCutoff: 12 * 60));
            Assert.AreEqual(1, Grant(remainingSeconds: 42, secondsUntilCutoff: 102));
            Assert.AreEqual(0, Grant(remainingSeconds: 42, secondsUntilCutoff: 101.9));
            Assert.AreEqual(0, Grant(remainingSeconds: 15 * 60, secondsUntilCutoff: 12 * 60),
                "An existing longer grant cannot authorize more time");
        }

        [TestMethod]
        public void CompletedSessionRestartsFromNowAndStillRespectsCutoff()
        {
            Assert.AreEqual(2, Grant(secondsUntilCutoff: 120));
            Assert.AreEqual(2, Grant(remainingSeconds: -30, secondsUntilCutoff: 120));
            Assert.AreEqual(0, Grant(secondsUntilCutoff: 59.9));
        }

        [TestMethod]
        public void ApprovalAfterCutoffCannotReuseAnEarlierGrant()
        {
            DateTime cutoff = Now.AddSeconds(120);

            Assert.AreEqual(2, Grant(secondsUntilCutoff: 120));
            Assert.AreEqual(0, FocusExtensionCutoffLimit.GrantedMinutes(2, cutoff.AddSeconds(1), null, cutoff,
                isAfterCutoff: true, isCutoffStrict: true, protectsRoutine: true));
        }

        [TestMethod]
        public void UnrelatedFocusAndExistingNonStrictAfterCutoffPolicyRemainUnchanged()
        {
            Assert.AreEqual(5, Grant(after: true, protects: false), "A session protecting no routine is not the cutoff's business");
            Assert.AreEqual(5, Grant(secondsUntilCutoff: null), "No cutoff configured, nothing to clamp to");
            Assert.AreEqual(5, Grant(after: true, strict: false));
            Assert.AreEqual(0, Grant(after: true));
            Assert.AreEqual(0, Grant(0));
        }
    }
}
