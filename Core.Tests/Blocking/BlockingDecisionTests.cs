using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Blocking
{
    /// <summary>
    /// Ported from the Mac app's <c>BlockingDecisionTests.swift</c> (Mac-App#1625), which covers
    /// <c>getBlockedUrlsToBlock()</c> and <c>getBlockedAppsToBlock()</c>.
    /// </summary>
    [TestClass]
    public class BlockingDecisionTests
    {
        private static BlockingDecisionInputs Normal()
        {
            return new BlockingDecisionInputs
            {
                IsBlockDistractingSitesDuringMeetingsEnabled = true,
            };
        }

        private static IReadOnlyList<string> ThreeUrls
        {
            get { return new[] { "facebook.com", "twitter.com", "reddit.com" }; }
        }

        // MARK: - Nothing unusual going on

        [TestMethod]
        public void NormalStateReturnsTheWholeList()
        {
            CollectionAssert.AreEqual(ThreeUrls.ToArray(),
                BlockingDecision.BlockedUrlsToBlock(ThreeUrls, Normal()).ToArray());
            CollectionAssert.AreEqual(new[] { "Twitter", "Facebook", "Reddit" },
                BlockingDecision.BlockedAppsToBlock(new[] { "Twitter", "Facebook", "Reddit" }, Normal()).ToArray());
        }

        [TestMethod]
        public void AnEmptyListStaysEmpty()
        {
            Assert.AreEqual(0, BlockingDecision.BlockedUrlsToBlock(new string[0], Normal()).Count);
            Assert.AreEqual(0, BlockingDecision.BlockedAppsToBlock(new string[0], Normal()).Count);
        }

        // MARK: - Breaks, which only apply under a custom schedule

        [TestMethod]
        public void AFocusBreakUnderACustomScheduleSuppressesEverythingWhenTheUserAskedItTo()
        {
            BlockingDecisionInputs inputs = Normal();
            inputs.IsAnyCustomScheduleOn = true;
            inputs.IsFocusBreakOn = true;
            inputs.DoNotBlockUrlsDuringSuperPomodoroBreaks = true;

            Assert.AreEqual(0, BlockingDecision.BlockedUrlsToBlock(new[] { "facebook.com" }, inputs).Count);
            Assert.AreEqual(0, BlockingDecision.BlockedAppsToBlock(new[] { "Twitter" }, inputs).Count);
        }

        [TestMethod]
        public void AnExplicitlyFalsePreferenceKeepsBlocking()
        {
            BlockingDecisionInputs inputs = Normal();
            inputs.IsAnyCustomScheduleOn = true;
            inputs.IsFocusBreakOn = true;
            inputs.DoNotBlockUrlsDuringSuperPomodoroBreaks = false;

            Assert.AreEqual(1, BlockingDecision.BlockedUrlsToBlock(new[] { "facebook.com" }, inputs).Count);
        }

        [TestMethod]
        public void AnUnsetPreferenceAlsoKeepsBlocking()
        {
            // The Mac reads the preference as an optional and acts only on an explicit true, so
            // "never set" must not be collapsed into false-or-true by the port.
            BlockingDecisionInputs inputs = Normal();
            inputs.IsAnyCustomScheduleOn = true;
            inputs.IsFocusBreakOn = true;
            inputs.DoNotBlockUrlsDuringSuperPomodoroBreaks = null;

            Assert.AreEqual(1, BlockingDecision.BlockedUrlsToBlock(new[] { "facebook.com" }, inputs).Count);
        }

        [TestMethod]
        public void TheMicroBreakStateSuppressesEverythingOnTheSameCondition()
        {
            BlockingDecisionInputs inputs = Normal();
            inputs.IsAnyCustomScheduleOn = true;
            inputs.IsMicroBreakState = true;
            inputs.DoNotBlockUrlsDuringSuperPomodoroBreaks = true;

            Assert.AreEqual(0, BlockingDecision.BlockedUrlsToBlock(new[] { "facebook.com" }, inputs).Count);
            Assert.AreEqual(0, BlockingDecision.BlockedAppsToBlock(new[] { "Twitter" }, inputs).Count);
        }

        [TestMethod]
        public void WithNoCustomScheduleABreakSuppressesNothing()
        {
            // The break rules sit inside the custom-schedule gate: during a focus session or a
            // routine the same break leaves blocking fully on.
            BlockingDecisionInputs inputs = Normal();
            inputs.IsAnyCustomScheduleOn = false;
            inputs.IsFocusBreakOn = true;
            inputs.DoNotBlockUrlsDuringSuperPomodoroBreaks = true;

            Assert.AreEqual(2, BlockingDecision.BlockedAppsToBlock(new[] { "Twitter", "Facebook" }, inputs).Count);
        }

        // MARK: - Meetings

        [TestMethod]
        public void AMeetingSuppressesEverythingOnlyWhenBlockingDuringMeetingsIsOff()
        {
            BlockingDecisionInputs inputs = Normal();
            inputs.IsAnyCustomScheduleOn = true;
            inputs.IsUserOnMeeting = true;
            inputs.IsBlockDistractingSitesDuringMeetingsEnabled = false;
            Assert.AreEqual(0, BlockingDecision.BlockedUrlsToBlock(new[] { "facebook.com" }, inputs).Count);

            inputs.IsBlockDistractingSitesDuringMeetingsEnabled = true;
            Assert.AreEqual(1, BlockingDecision.BlockedUrlsToBlock(new[] { "facebook.com" }, inputs).Count);
        }

        [TestMethod]
        public void AMeetingWithNoCustomScheduleSuppressesNothing()
        {
            BlockingDecisionInputs inputs = Normal();
            inputs.IsAnyCustomScheduleOn = false;
            inputs.IsUserOnMeeting = true;
            inputs.IsBlockDistractingSitesDuringMeetingsEnabled = false;
            Assert.AreEqual(1, BlockingDecision.BlockedUrlsToBlock(new[] { "facebook.com" }, inputs).Count);
        }

        // MARK: - Expired licence

        [TestMethod]
        public void AnExpiredAppCapsTheListRatherThanClearingIt()
        {
            BlockingDecisionInputs inputs = Normal();
            inputs.IsExpired = true;

            var manyUrls = Enumerable.Range(0, 10).Select(index => "site" + index + ".com").ToArray();
            IReadOnlyList<string> urls = BlockingDecision.BlockedUrlsToBlock(manyUrls, inputs);
            Assert.AreEqual(BlockingDecision.AlwaysBlockedCountInExpiredApp, urls.Count);
            CollectionAssert.AreEqual(new[] { "site0.com", "site1.com", "site2.com" }, urls.ToArray(),
                "the cap keeps the FIRST entries, so which ones survive is stable");

            var manyApps = Enumerable.Range(0, 10).Select(index => "App" + index).ToArray();
            Assert.AreEqual(BlockingDecision.AlwaysBlockedCountInExpiredApp,
                BlockingDecision.BlockedAppsToBlock(manyApps, inputs).Count);
        }

        [TestMethod]
        public void AnExpiredAppWithFewerEntriesThanTheCapKeepsThemAll()
        {
            BlockingDecisionInputs inputs = Normal();
            inputs.IsExpired = true;
            Assert.AreEqual(1, BlockingDecision.BlockedUrlsToBlock(new[] { "site1.com" }, inputs).Count);
        }

        [TestMethod]
        public void SuppressionBeatsTheExpiredCap()
        {
            // Both rules can fire at once; an empty list is an empty list either way.
            BlockingDecisionInputs inputs = Normal();
            inputs.IsExpired = true;
            inputs.IsAnyCustomScheduleOn = true;
            inputs.IsMicroBreakState = true;
            inputs.DoNotBlockUrlsDuringSuperPomodoroBreaks = true;

            var manyUrls = Enumerable.Range(0, 10).Select(index => "site" + index + ".com").ToArray();
            Assert.AreEqual(0, BlockingDecision.BlockedUrlsToBlock(manyUrls, inputs).Count);
        }
    }
}
