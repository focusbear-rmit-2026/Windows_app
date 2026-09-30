using System;
using System.Collections.Generic;
using FocusBear.Core.Stats;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Stats
{
    /// <summary>
    /// Ported from the Mac's FocusAlignmentScoreTests (Focus-Bear/Mac-App#1628). The Mac's version
    /// drives global state; here the same arithmetic is exercised directly.
    /// </summary>
    [TestClass]
    public class FocusAlignmentScoreTests
    {
        private static readonly DateTime Now = new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc);

        private static FocusAlignmentActivity Activity(double probability, int totalSeconds)
        {
            return new FocusAlignmentActivity
            {
                Probability = probability,
                TotalSeconds = totalSeconds,
                IsVisibleApp = false,
                IsVisibleUrl = false
            };
        }

        private static double Score(params FocusAlignmentActivity[] activities)
        {
            return FocusAlignmentScore.Calculate(activities, Now);
        }

        [TestMethod]
        public void EmptyActivityListScoresOneHundred()
        {
            Assert.AreEqual(100, Score(), "With no tracked activities the score defaults to 100");
        }

        [TestMethod]
        public void AllAlignedScoresHigh()
        {
            double score = Score(Activity(1.0, 600), Activity(1.0, 300));

            Assert.IsTrue(score >= 90, "Fully aligned activities (probability 1.0) yield a score near 100");
        }

        [TestMethod]
        public void AllUnalignedScoresZero()
        {
            Assert.AreEqual(0, Score(Activity(0.0, 600)), "All-unaligned time yields 0");
        }

        [TestMethod]
        public void MixedActivitiesScoreProportionally()
        {
            // (1.0*300 + 0.0*300) / 600 * 100 = 50
            Assert.AreEqual(50.0, Score(Activity(1.0, 300), Activity(0.0, 300)), 0.01);
        }

        [TestMethod]
        public void LongerAlignedActivityDominates()
        {
            // 900s aligned vs 100s unaligned → (900*1.0)/1000*100 = 90
            Assert.AreEqual(90.0, Score(Activity(1.0, 900), Activity(0.0, 100)), 0.01);
        }

        [TestMethod]
        public void ScoreNeverExceedsOneHundred()
        {
            // A probability above 1.0 is an edge case the classifier should not produce, but the cap
            // is what stops it reaching the user as a 150% focused session.
            Assert.IsTrue(Score(Activity(1.5, 600)) <= 100);
        }

        [TestMethod]
        public void ScoreIsRoundedToTwoDecimalPlaces()
        {
            // 1s aligned + 2s unaligned → 1/3 * 100 = 33.333… → 33.33
            Assert.AreEqual(33.33, Score(Activity(1.0, 1), Activity(0.0, 2)));
        }

        [TestMethod]
        public void VisibleActivityAccruesTimeSinceItCameToTheFront()
        {
            FocusAlignmentActivity visible = new FocusAlignmentActivity
            {
                Probability = 0.0,
                TotalSeconds = 0,
                IsVisibleUrl = true,
                StartedTime = Now.AddSeconds(-300)
            };

            // 300s banked aligned + 300s unaligned still on screen → 50, so the score reacts to the
            // distraction the user is looking at right now, not only to time already recorded.
            Assert.AreEqual(50.0, FocusAlignmentScore.Calculate(
                new List<FocusAlignmentActivity> { Activity(1.0, 300), visible }, Now), 0.01);
        }

        [TestMethod]
        public void BackgroundActivityDoesNotAccrueTime()
        {
            FocusAlignmentActivity background = new FocusAlignmentActivity
            {
                Probability = 0.0,
                TotalSeconds = 0,
                StartedTime = Now.AddSeconds(-300)
            };

            Assert.AreEqual(100, FocusAlignmentScore.Calculate(
                new List<FocusAlignmentActivity> { Activity(1.0, 300), background }, Now),
                "An activity that is not on screen contributes no seconds at all");
        }

        /// <summary>
        /// Pins the Mac's route to 100 for an empty session: the score is set to 100 and then divided
        /// by zero tracked seconds, giving positive infinity, which the cap pulls back to 100. Both
        /// languages produce infinity rather than an error for double division by zero, so the port
        /// agrees — but only because of the cap, which is worth knowing before anyone "tidies" it.
        /// </summary>
        [TestMethod]
        public void TheEmptyCaseReachesOneHundredThroughTheCapNotThroughAGuard()
        {
            double infinity = FocusAlignmentScore.DefaultScore / 0;

            Assert.IsTrue(double.IsPositiveInfinity(infinity));
            Assert.AreEqual(FocusAlignmentScore.MaximumScore, Score());
        }
    }
}
