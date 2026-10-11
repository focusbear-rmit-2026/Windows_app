using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Blocking
{
    /// <summary>
    /// Ported from the "Why blocking is off" sections of the Mac app's
    /// <c>CustomBlockingScheduleTests.swift</c> (Mac-App#1978, reports 2026-09-10_07-54-05 and
    /// 2026-09-11_16-56-33).
    ///
    /// The through-line of every case: a reason is listed only when the inputs ESTABLISH it. Sending
    /// someone to fix a thing that isn't broken is worse than saying nothing.
    /// </summary>
    [TestClass]
    public class BlockingStatusTests
    {
        /// <summary>Everything granted, a schedule running — nothing to explain.</summary>
        private static BlockingStatus BlockingOn()
        {
            return new BlockingStatus
            {
                IsAppEnabled = true,
                Permissions = BlockingPermissionsState.Granted,
                IsAnyBrowserEnabled = true,
                ScheduleCount = 1,
                IsAnyScheduleOn = true,
                IsAnySchedulePaused = false,
                IsAnyScheduleCoveringNow = true,
                IsFocusSessionRunning = false,
                IsRoutineRunning = false,
            };
        }

        private static void AssertReasons(BlockingStatus status, params BlockingOffReason[] expected)
        {
            CollectionAssert.AreEqual(expected, status.OffReasons.ToArray());
        }

        [TestMethod]
        public void NothingIsReportedWhileAScheduleIsRunning()
        {
            AssertReasons(BlockingOn());
        }

        // MARK: - Browser reads that stopped answering

        [TestMethod]
        public void ABrowserThatStoppedAnsweringIsReportedEvenWhileAScheduleRuns()
        {
            BlockingStatus status = BlockingOn();
            status.IsBrowserUrlReadFailing = true;

            AssertReasons(status, BlockingOffReason.BrowserNotResponding);
            Assert.AreEqual(BlockingStatusCardKind.Off, BlockingStatusCard.KindFor(status.OffReasons),
                "a browser that cannot be read is not a mere warning — nothing can be checked at all");
        }

        [TestMethod]
        public void FailingReadsAreNotMentionedWhenNoBrowserIsSetUpAtAll()
        {
            BlockingStatus status = BlockingOn();
            status.IsAnyBrowserEnabled = false;
            status.IsBrowserUrlReadFailing = true;

            AssertReasons(status, BlockingOffReason.NoBrowserEnabled);
        }

        [TestMethod]
        public void BrowserReadHealthNeedsARunOfFailuresAndReportsEachTransitionOnce()
        {
            var health = new BrowserUrlReadHealth();
            DateTime now = new DateTime(2026, 9, 16, 12, 0, 0);

            for (int attempt = 1; attempt < BrowserUrlReadHealth.FailureThreshold; attempt++)
            {
                Assert.IsFalse(health.Record(succeeded: false, now), "a handful of empty reads is normal");
                Assert.IsFalse(health.IsFailing);
            }
            Assert.IsTrue(health.Record(succeeded: false, now), "the threshold failure is the one worth logging");
            Assert.IsTrue(health.IsFailing);
            Assert.IsFalse(health.Record(succeeded: false, now), "still failing is not news");
            Assert.IsTrue(health.Record(succeeded: true, now), "recovery is news");
            Assert.IsFalse(health.IsFailing);
            Assert.IsFalse(health.Record(succeeded: true, now));
        }

        [TestMethod]
        public void OneGoodReadResetsTheRun()
        {
            var health = new BrowserUrlReadHealth();
            DateTime now = new DateTime(2026, 9, 16, 12, 0, 0);

            for (int attempt = 1; attempt < BrowserUrlReadHealth.FailureThreshold; attempt++)
            {
                health.Record(succeeded: false, now);
            }
            health.Record(succeeded: true, now);
            for (int attempt = 1; attempt < BrowserUrlReadHealth.FailureThreshold; attempt++)
            {
                Assert.IsFalse(health.Record(succeeded: false, now));
            }
            Assert.IsFalse(health.IsFailing, "the count starts again after a browser answers");
        }

        // MARK: - Focus sessions and routines block on their own

        [TestMethod]
        public void NothingIsReportedDuringAFocusSessionWithNoSchedule()
        {
            BlockingStatus status = BlockingOn();
            status.ScheduleCount = 0;
            status.IsAnyScheduleOn = false;
            status.IsFocusSessionRunning = true;

            AssertReasons(status);
        }

        [TestMethod]
        public void NothingIsReportedDuringARoutineWithEverythingGranted()
        {
            // The reported false positive: mid-morning-routine, schedule activation is suppressed, and
            // the card claimed both missing permissions and that no schedule covered right now.
            BlockingStatus status = BlockingOn();
            status.IsAnyScheduleOn = false;
            status.IsRoutineRunning = true;

            AssertReasons(status);
        }

        [TestMethod]
        public void MissingPermissionsAreNamedEvenMidSession()
        {
            BlockingStatus status = BlockingOn();
            status.Permissions = BlockingPermissionsState.Missing;
            status.IsFocusSessionRunning = true;

            AssertReasons(status, BlockingOffReason.PermissionsMissing);
        }

        // MARK: - The three schedule gaps

        [TestMethod]
        public void TheThreeScheduleGapsAreDistinguished()
        {
            BlockingStatus status = BlockingOn();
            status.IsAnyScheduleOn = false;

            status.ScheduleCount = 0;
            AssertReasons(status, BlockingOffReason.NoSchedule);

            status.ScheduleCount = 1;
            status.IsAnySchedulePaused = true;
            AssertReasons(status, BlockingOffReason.SchedulePaused);

            status.IsAnySchedulePaused = false;
            status.IsAnyScheduleCoveringNow = false;
            AssertReasons(status, BlockingOffReason.NoScheduleRightNow);
        }

        [TestMethod]
        public void AScheduleCoveringNowIsNeverCalledAGap()
        {
            // A schedule covers right now yet none is on: real, but the cause can't be named from
            // here, and "no schedule covers right now" would be a plain falsehood.
            BlockingStatus status = BlockingOn();
            status.IsAnyScheduleOn = false;
            status.IsAnyScheduleCoveringNow = true;

            AssertReasons(status);
        }

        // MARK: - Severity and precedence

        [TestMethod]
        public void EveryCauseThatAppliesIsListedMostSevereFirst()
        {
            BlockingStatus status = BlockingOn();
            status.Permissions = BlockingPermissionsState.Missing;
            status.ScheduleCount = 0;
            status.IsAnyScheduleOn = false;

            AssertReasons(status, BlockingOffReason.PermissionsMissing, BlockingOffReason.NoSchedule);
        }

        [TestMethod]
        public void ADeactivatedAppIsTheWholeStory()
        {
            BlockingStatus status = BlockingOn();
            status.IsAppEnabled = false;
            status.Permissions = BlockingPermissionsState.Missing;
            status.ScheduleCount = 0;
            status.IsAnyScheduleOn = false;

            AssertReasons(status, BlockingOffReason.AppDeactivated);
            Assert.AreEqual(BlockingStatusCardKind.Off, BlockingStatusCard.KindFor(status.OffReasons));
        }

        [TestMethod]
        public void NoBrowserEnabledIsNotAMissingPermissionAndIsOutrankedByOne()
        {
            BlockingStatus status = BlockingOn();
            status.IsAnyBrowserEnabled = false;
            AssertReasons(status, BlockingOffReason.NoBrowserEnabled);
            Assert.AreNotEqual(BlockingOffReasons.FixTitle(BlockingOffReason.NoBrowserEnabled),
                               BlockingOffReasons.FixTitle(BlockingOffReason.PermissionsMissing),
                               "the two must not send the user to the same place");

            // A missing grant already explains why nothing is blocked; the browser list is the next
            // thing to fix, not a second cause to bury it under.
            status.Permissions = BlockingPermissionsState.Missing;
            AssertReasons(status, BlockingOffReason.PermissionsMissing);
        }

        [TestMethod]
        public void PermissionAndBrowserGapsAloneAreAWarningNotAnOff()
        {
            // Per-browser hooks and the extension are independent of the OS grants, so neither gap
            // establishes that ALL blocking has stopped.
            BlockingStatus status = BlockingOn();
            status.Permissions = BlockingPermissionsState.Missing;
            Assert.AreEqual(BlockingStatusCardKind.Attention, BlockingStatusCard.KindFor(status.OffReasons));

            status.IsAnyScheduleOn = false;
            status.IsFocusSessionRunning = true;
            Assert.AreEqual(BlockingStatusCardKind.Attention, BlockingStatusCard.KindFor(status.OffReasons));

            status.IsFocusSessionRunning = false;
            status.IsRoutineRunning = true;
            Assert.AreEqual(BlockingStatusCardKind.Attention, BlockingStatusCard.KindFor(status.OffReasons));

            BlockingStatus noBrowser = BlockingOn();
            noBrowser.IsAnyBrowserEnabled = false;
            Assert.AreEqual(BlockingStatusCardKind.Attention, BlockingStatusCard.KindFor(noBrowser.OffReasons));
        }

        [TestMethod]
        public void ARealOffCauseKeepsTheOffHeadlineAlongsideAPermissionWarning()
        {
            BlockingStatus status = BlockingOn();
            status.Permissions = BlockingPermissionsState.Missing;
            status.IsAnyScheduleOn = false;
            status.IsAnyScheduleCoveringNow = false;
            Assert.AreEqual(BlockingStatusCardKind.Off, BlockingStatusCard.KindFor(status.OffReasons));
        }

        // MARK: - Unknown is not a denial

        [TestMethod]
        public void NothingIsClaimedWhilePermissionsAreUnknown()
        {
            // An unfinished or timed-out probe must not be reported as a denial: the menu renders
            // before the async probe lands, and a busy queue is not a refusal.
            BlockingStatus status = BlockingOn();
            status.Permissions = BlockingPermissionsState.Unknown;
            AssertReasons(status);

            status.IsAnyScheduleOn = false;
            status.IsAnyScheduleCoveringNow = false;
            AssertReasons(status, BlockingOffReason.NoScheduleRightNow);
        }

        [TestMethod]
        public void PermissionHealthNamesOnlyConfirmedAffectedCapabilities()
        {
            var health = new BlockingPermissionHealth(accessibilityGranted: false,
                                                      systemEvents: BlockingPermissionsState.Unknown);
            Assert.AreEqual(BlockingPermissionsState.Missing, health.State);
            CollectionAssert.AreEqual(new[] { BlockingPermissionIssue.Accessibility }, health.Issues.ToArray());

            health.SystemEvents = BlockingPermissionsState.Missing;
            CollectionAssert.AreEqual(
                new[] { BlockingPermissionIssue.Accessibility, BlockingPermissionIssue.SystemEvents },
                health.Issues.ToArray());

            health.AccessibilityGranted = true;
            CollectionAssert.AreEqual(new[] { BlockingPermissionIssue.SystemEvents }, health.Issues.ToArray());

            health.SystemEvents = BlockingPermissionsState.Unknown;
            Assert.AreEqual(BlockingPermissionsState.Unknown, health.State);
            Assert.AreEqual(0, health.Issues.Count);

            health.SystemEvents = BlockingPermissionsState.Granted;
            Assert.AreEqual(BlockingPermissionsState.Granted, health.State);
        }

        // MARK: - The probe cache

        [TestMethod]
        public void OnlyOneCheckIsInFlightAndEvidenceRefreshesAfterAGrant()
        {
            var probe = new BlockingPermissionProbeCache();
            Assert.AreEqual(BlockingPermissionsState.Unknown, probe.State(100));
            Assert.IsTrue(probe.Begin(100));
            Assert.IsFalse(probe.Begin(101), "a second probe must not start while one is running");

            probe.Complete(BlockingPermissionsState.Missing, startedAt: 100);
            Assert.AreEqual(BlockingPermissionsState.Missing, probe.State(101));
            Assert.IsFalse(probe.Begin(105), "too soon after the last sample");
            Assert.IsTrue(probe.Begin(110));

            probe.Complete(BlockingPermissionsState.Granted, startedAt: 110);
            Assert.AreEqual(BlockingPermissionsState.Granted, probe.State(111));

            // A duplicate or late completion of an OLDER probe cannot overwrite the newer grant.
            probe.Complete(BlockingPermissionsState.Missing, startedAt: 100);
            Assert.AreEqual(BlockingPermissionsState.Granted, probe.State(112));
        }

        [TestMethod]
        public void EvidenceExpiresFromTheProbeStartNotItsLateCompletion()
        {
            var probe = new BlockingPermissionProbeCache();
            Assert.IsTrue(probe.Begin(100));
            probe.Complete(BlockingPermissionsState.Missing, startedAt: 100);

            Assert.AreEqual(BlockingPermissionsState.Missing, probe.State(130));
            Assert.AreEqual(BlockingPermissionsState.Unknown, probe.State(131));
            Assert.AreEqual(BlockingPermissionsState.Unknown, probe.State(99), "a clock that went backwards proves nothing");

            Assert.IsTrue(probe.Begin(140));
            probe.Complete(BlockingPermissionsState.Missing, startedAt: 140);
            Assert.AreEqual(BlockingPermissionsState.Unknown, probe.State(171),
                "a slow completion must not give old evidence a new 30-second lifetime");
        }

        [TestMethod]
        public void AnInconclusiveRecheckDoesNotRetainAnEarlierDenial()
        {
            var probe = new BlockingPermissionProbeCache();
            Assert.IsTrue(probe.Begin(100));
            probe.Complete(BlockingPermissionsState.Missing, startedAt: 100);
            Assert.IsTrue(probe.Begin(110));
            probe.Complete(BlockingPermissionsState.Unknown, startedAt: 110);
            Assert.AreEqual(BlockingPermissionsState.Unknown, probe.State(111));
        }

        // MARK: - The log line

        [TestMethod]
        public void EveryReasonHasAnExplanationAndAFix()
        {
            foreach (BlockingOffReason reason in BlockingOffReasons.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(BlockingOffReasons.Explanation(reason)),
                    BlockingOffReasons.Identifier(reason) + " needs an explanation");
                Assert.IsFalse(string.IsNullOrEmpty(BlockingOffReasons.FixTitle(reason)),
                    BlockingOffReasons.Identifier(reason) + " needs a fix button");
            }
            Assert.AreEqual(Enum.GetValues(typeof(BlockingOffReason)).Length, BlockingOffReasons.All.Count,
                "a reason added to the enum must be added to the ordered list too");
        }

        [TestMethod]
        public void TheLogLineCarriesEveryInputAndTheConclusion()
        {
            // The log line is what makes the next report answerable without a code read.
            BlockingStatus status = BlockingOn();
            status.Permissions = BlockingPermissionsState.Unknown;
            string line = status.LogDescription;

            foreach (string field in new[] { "appEnabled=", "permissions=", "browserEnabled=", "schedules=",
                                             "scheduleOn=", "schedulePaused=", "scheduleCoversNow=",
                                             "focusSession=", "routine=" })
            {
                StringAssert.Contains(line, field, "log line is missing " + field + ": " + line);
            }
            StringAssert.Contains(line, "no confirmed blocking issue",
                "unknown permission evidence cannot prove blocking is on: " + line);

            status.Permissions = BlockingPermissionsState.Missing;
            StringAssert.Contains(status.LogDescription,
                BlockingOffReasons.Identifier(BlockingOffReason.PermissionsMissing),
                "the concluded reasons must be named in the log");
        }

        [TestMethod]
        public void TheLogLineUsesTheMacsSpellingsSoTheTwoCanBeComparedSideBySide()
        {
            BlockingStatus status = BlockingOn();
            StringAssert.Contains(status.LogDescription, "appEnabled=true");
            StringAssert.Contains(status.LogDescription, "permissions=granted");
        }
    }
}
