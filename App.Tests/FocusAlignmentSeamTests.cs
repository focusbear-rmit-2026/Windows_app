using System;
using System.Collections.Generic;
using Focus_Bear.Focus;
using FocusBear.Core.Stats;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// Core's FocusAlignmentScoreTests prove the formula. These prove the app hands it the session's
    /// dictionary entries faithfully — the keys are untyped strings, so a misspelt one would silently
    /// read as 0 and no Core test could notice.
    /// </summary>
    [TestClass]
    public class FocusAlignmentSeamTests
    {
        private static readonly DateTime Now = new DateTime(2026, 9, 24, 10, 0, 0);

        private static IDictionary<string, object> Entry(double probability, int totalSeconds,
                                                         bool isVisibleApp = false, bool isVisibleUrl = false,
                                                         object startedTime = null)
        {
            var entry = new Dictionary<string, object>
            {
                { "probability", probability },
                { "totalSeconds", totalSeconds },
                { "isVisibleApp", isVisibleApp },
                { "isVisibleUrl", isVisibleUrl },
                { "reason", "ignored by the score" },
            };
            if (startedTime != null) entry["startedTime"] = startedTime;
            return entry;
        }

        [TestMethod]
        public void EveryFieldTheScoreReadsIsCarriedAcross()
        {
            DateTime started = Now.AddSeconds(-30);

            FocusAlignmentActivity activity = FocusAlignmentActivities.From(new[]
            {
                Entry(0.75, 120, isVisibleApp: true, isVisibleUrl: false, startedTime: started),
            })[0];

            Assert.AreEqual(0.75, activity.Probability);
            Assert.AreEqual(120, activity.TotalSeconds);
            Assert.IsTrue(activity.IsVisibleApp);
            Assert.IsFalse(activity.IsVisibleUrl);
            Assert.AreEqual(started, activity.StartedTime);
        }

        [TestMethod]
        public void NumbersStoredAsTheOtherNumericTypeStillRead()
        {
            // The dictionary is filled from several call sites; Convert.* is what the inline
            // calculation used, so an int probability or a double second count must keep working.
            var entry = new Dictionary<string, object>
            {
                { "probability", 1 },
                { "totalSeconds", 45.0 },
                { "isVisibleUrl", true },
            };

            FocusAlignmentActivity activity = FocusAlignmentActivities.From(new[] { entry })[0];

            Assert.AreEqual(1.0, activity.Probability);
            Assert.AreEqual(45, activity.TotalSeconds);
            Assert.IsTrue(activity.IsVisibleUrl);
            Assert.IsNull(activity.StartedTime);
        }

        [TestMethod]
        public void MissingKeysReadAsNothingTracked()
        {
            FocusAlignmentActivity activity = FocusAlignmentActivities.From(new[] { new Dictionary<string, object>() })[0];

            Assert.AreEqual(0, activity.Probability);
            Assert.AreEqual(0, activity.TotalSeconds);
            Assert.IsFalse(activity.IsVisibleApp || activity.IsVisibleUrl);
        }

        [TestMethod]
        public void AStartedTimeThatIsNotADateNoLongerThrows()
        {
            FocusAlignmentActivity activity = FocusAlignmentActivities.From(new[]
            {
                Entry(0.5, 10, isVisibleUrl: true, startedTime: "2026-09-24T10:00:00"),
            })[0];

            Assert.IsNull(activity.StartedTime, "counted as started now rather than failing every caller");
        }

        [TestMethod]
        public void ANewSessionScoresFullyAlignedAsOnTheMac()
        {
            // Windows used to report 0 here, crediting no deep work to a session that simply had not
            // triggered an AI check yet.
            Assert.AreEqual(100.0, FocusAlignmentActivities.Score(new IDictionary<string, object>[0], Now));
        }

        [TestMethod]
        public void VisibleTimeSinceStartCounts()
        {
            // 60 banked unaligned seconds + 60 live aligned seconds = 50%.
            double score = FocusAlignmentActivities.Score(new[]
            {
                Entry(0, 60),
                Entry(1, 0, isVisibleUrl: true, startedTime: Now.AddSeconds(-60)),
            }, Now);

            Assert.AreEqual(50.0, score);
        }

        [TestMethod]
        public void AHalfHundredthRoundsAwayFromZero()
        {
            // 32 aligned seconds of 1024 is exactly 3.125% in binary floating point. Swift rounds the
            // half up to 3.13; Math.Round, which this used to call, rounds it to even: 3.12.
            double score = FocusAlignmentActivities.Score(new[] { Entry(1, 32), Entry(0, 992) }, Now);

            Assert.AreEqual(3.13, score);
            Assert.AreEqual(3.12, Math.Round(3.125 * 100) / 100, "the old rounding, for contrast");
        }
    }
}
