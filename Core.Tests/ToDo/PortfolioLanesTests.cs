using System.Linq;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>The "What matters" lane rules, in order — ported from the Mac's LocalForecastPortfolioTests.</summary>
    [TestClass]
    public class PortfolioLanesTests
    {
        private static PortfolioLanes.Facts Facts(PortfolioBucket bucket, int? days, bool beyondHorizon = false, double? top = null,
                                                  bool open = true, bool finished = false)
        {
            return new PortfolioLanes.Facts(bucket, days, beyondHorizon, top, open, finished);
        }

        private static PortfolioActionSection Lane(PortfolioLanes.Facts facts) => PortfolioLanes.Section(facts);

        [TestMethod]
        public void LaneRulesApplyInOrder()
        {
            Assert.AreEqual(PortfolioActionSection.BackBurner, Lane(Facts(PortfolioBucket.Critical, 2, open: false, finished: true)), "1. done");
            Assert.AreEqual(PortfolioActionSection.BackBurner, Lane(Facts(PortfolioBucket.NoDeadline, null, top: 110)), "2. no due date, whatever its score");
            Assert.AreEqual(PortfolioActionSection.AskForExtension, Lane(Facts(PortfolioBucket.Critical, 20, top: 20)), "3. cannot fit");
            Assert.AreEqual(PortfolioActionSection.BackBurner, Lane(Facts(PortfolioBucket.AtRisk, 0, top: 20, open: false)), "4. nothing to work on, though due today");
            Assert.AreEqual(PortfolioActionSection.BackBurner, Lane(Facts(PortfolioBucket.AtRisk, -3, open: false)), "4. an old empty project");
            Assert.AreEqual(PortfolioActionSection.StartNow, Lane(Facts(PortfolioBucket.OnTrack, PortfolioLanes.FocusWindowDays)), "5. due this week");
            Assert.AreEqual(PortfolioActionSection.SteadyProgress, Lane(Facts(PortfolioBucket.OnTrack, PortfolioLanes.FocusWindowDays + 1)));
            Assert.AreEqual(PortfolioActionSection.StartNow, Lane(Facts(PortfolioBucket.AtRisk, -3)), "5. overdue with work open");
            Assert.AreEqual(PortfolioActionSection.BackBurner, Lane(Facts(PortfolioBucket.AtRisk, 90, beyondHorizon: true, top: 20)), "6. beyond the horizon");
            Assert.AreEqual(PortfolioActionSection.StartNow, Lane(Facts(PortfolioBucket.AtRisk, 20)), "7. at risk");
            Assert.AreEqual(PortfolioActionSection.SteadyProgress, Lane(Facts(PortfolioBucket.OnTrack, 20, top: 90)), "8. due inside the horizon, whatever its score");
        }

        [TestMethod]
        public void LanesOrderByTopScoreThenDueDateThenForecastOrderWithFinishedLast()
        {
            var entries = new[]
            {
                Entry("late", Facts(PortfolioBucket.OnTrack, 6, top: 5)),
                Entry("soon", Facts(PortfolioBucket.OnTrack, 2, top: 5)),
                Entry("first", Facts(PortfolioBucket.OnTrack, 4)),
                Entry("second", Facts(PortfolioBucket.OnTrack, 4)),
                Entry("top", Facts(PortfolioBucket.OnTrack, 7, top: 40)),
                Entry("done", Facts(PortfolioBucket.OnTrack, null, top: 99, open: false, finished: true)),
                Entry("undated", Facts(PortfolioBucket.NoDeadline, null, top: 3)),
            };
            PortfolioActionGroups groups = PortfolioLanes.Group(entries);
            CollectionAssert.AreEqual(new[] { "top", "soon", "late", "first", "second" }, groups.StartNow.Select(p => p.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "undated", "done" }, groups.BackBurner.Select(p => p.Id).ToArray());
            Assert.AreEqual(0, groups.SteadyProgress.Count);
        }

        [TestMethod]
        public void SteadyProgressCountsTowardsANonEmptyBoard()
        {
            PortfolioActionGroups groups = PortfolioLanes.Group(new[] { Entry("steady", Facts(PortfolioBucket.OnTrack, 20)) });
            Assert.IsFalse(groups.IsEmpty);
            CollectionAssert.AreEqual(new[] { "steady" }, groups.SteadyProgress.Select(p => p.Id).ToArray());
        }

        private static PortfolioLanes.Entry Entry(string id, PortfolioLanes.Facts facts)
        {
            return new PortfolioLanes.Entry(new PortfolioProject { Id = id, Name = id, BucketRaw = PortfolioBucketWire.Of(facts.Bucket) }, facts);
        }
    }
}
