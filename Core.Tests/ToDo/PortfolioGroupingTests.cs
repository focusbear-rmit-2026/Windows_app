using System.Collections.Generic;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FocusBear.Core.Tests.ToDo.ToDoFixtures;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>
    /// The portfolio's "This week" grouping, ported from the Mac's `testGroupingSplitsBucketsAndSorts`,
    /// `testGroupingPromotesBackBurnerWhenNothingToStart`, `testUnscheduledDueDatedProjectsNeedStartingNow`
    /// and the badge/name/pace cases.
    /// </summary>
    [TestClass]
    public class PortfolioGroupingTests
    {
        private const double Tolerance = 0.0001;

        [TestMethod]
        public void GroupingSplitsBucketsAndSorts()
        {
            PortfolioActionGroups groups = PortfolioGrouping.Group(new[]
            {
                Risk("critical-low", PortfolioBucket.Critical, score: 70),
                Risk("critical-high", PortfolioBucket.Critical, score: 90),
                Risk("at-risk", PortfolioBucket.AtRisk, score: 40, days: 3),
                Risk("overdue", PortfolioBucket.OnTrack, score: 10, days: -2,
                     badges: new[] { PortfolioBadge.StartNow }),
                Risk("no-deadline", PortfolioBucket.NoDeadline),
                Risk("on-track", PortfolioBucket.OnTrack, score: 5, due: "2026-10-01T00:00:00.000Z", days: 20),
            });

            CollectionAssert.AreEqual(new[] { "critical-high", "critical-low" }, RiskIds(groups.AskForExtension));
            CollectionAssert.AreEqual(new[] { "overdue", "at-risk" }, RiskIds(groups.StartNow),
                                      "overdue first, then risk descending");
            CollectionAssert.AreEqual(new[] { "on-track", "no-deadline" }, RiskIds(groups.BackBurner),
                                      "due-dated first");
        }

        [TestMethod]
        public void GroupingPromotesBackBurnerWhenThereIsNothingToStart()
        {
            PortfolioActionGroups groups = PortfolioGrouping.Group(new[]
            {
                Risk("later", PortfolioBucket.OnTrack, due: "2026-12-01T00:00:00.000Z"),
                Risk("sooner", PortfolioBucket.OnTrack, due: "2026-11-01T00:00:00.000Z",
                     importance: (int)PortfolioImportance.Low),
                Risk("undated", PortfolioBucket.NoDeadline, importance: (int)PortfolioImportance.Critical),
            });

            CollectionAssert.AreEquivalent(new[] { "later", "sooner" }, RiskIds(groups.StartNow));
            CollectionAssert.AreEqual(new[] { "undated" }, RiskIds(groups.BackBurner));
        }

        [TestMethod]
        public void WithNoDueDatedWorkAtAllTheImportantProjectsAreEncouraged()
        {
            PortfolioActionGroups groups = PortfolioGrouping.Group(new[]
            {
                Risk("low", PortfolioBucket.NoDeadline, importance: (int)PortfolioImportance.Low),
                Risk("high", PortfolioBucket.NoDeadline, importance: (int)PortfolioImportance.High),
                Risk("critical", PortfolioBucket.NoDeadline, importance: (int)PortfolioImportance.Critical),
            });

            CollectionAssert.AreEqual(new[] { "critical", "high" }, RiskIds(groups.StartNow));
            CollectionAssert.AreEqual(new[] { "low" }, RiskIds(groups.BackBurner));
        }

        [TestMethod]
        public void NoMoreThanThreeProjectsAreEverEncouraged()
        {
            var due = new List<PortfolioProject>();
            for (int index = 0; index < 5; index++)
            {
                due.Add(Risk("p" + index, PortfolioBucket.OnTrack, due: "2026-1" + index + "-01T00:00:00.000Z"));
            }

            PortfolioActionGroups groups = PortfolioGrouping.Group(due);

            Assert.AreEqual(PortfolioGrouping.EncouragedStartNowLimit, groups.StartNow.Count);
            Assert.AreEqual(2, groups.BackBurner.Count);
        }

        [TestMethod]
        public void UnscheduledDueDatedProjectsNeedStartingNow()
        {
            // #2418: "on track" only means there is free space; a due-dated project with nothing in the
            // Day Plan still needs starting. Undated unscheduled work stays on the back burner.
            PortfolioActionGroups groups = PortfolioGrouping.Group(new[]
            {
                Risk("planned", PortfolioBucket.OnTrack, score: 5, due: "2026-10-01T00:00:00.000Z", days: 20),
                Risk("unplanned", PortfolioBucket.OnTrack, score: 4, due: "2026-10-05T00:00:00.000Z", days: 24,
                     badges: new[] { PortfolioBadge.Unscheduled }),
                Risk("undated-unplanned", PortfolioBucket.NoDeadline, badges: new[] { PortfolioBadge.Unscheduled }),
            });

            CollectionAssert.AreEqual(new[] { "unplanned" }, RiskIds(groups.StartNow));
            CollectionAssert.AreEqual(new[] { "planned", "undated-unplanned" }, RiskIds(groups.BackBurner));
        }

        [TestMethod]
        public void TheBackBurnerBreaksTiesOnRiskBeforeImportance()
        {
            // All three are due-dated, so the first key decides nothing and the order has to come from
            // the risk the server scored — with importance only settling a genuine tie.
            PortfolioActionGroups groups = PortfolioGrouping.Group(new[]
            {
                Risk("calm-critical", PortfolioBucket.OnTrack, score: 1, due: "2026-12-01T00:00:00.000Z",
                     importance: (int)PortfolioImportance.Critical),
                Risk("risky-low", PortfolioBucket.OnTrack, score: 9, due: "2026-12-02T00:00:00.000Z",
                     importance: (int)PortfolioImportance.Low),
                Risk("started", PortfolioBucket.OnTrack, score: 5, due: "2026-12-03T00:00:00.000Z",
                     badges: new[] { PortfolioBadge.StartNow }),
            });

            CollectionAssert.AreEqual(new[] { "started" }, RiskIds(groups.StartNow),
                                      "something to start now, so nothing is promoted");
            CollectionAssert.AreEqual(new[] { "risky-low", "calm-critical" }, RiskIds(groups.BackBurner));
        }

        [TestMethod]
        public void StartNowPutsOverdueWorkFirstWhateverItsRisk()
        {
            PortfolioActionGroups groups = PortfolioGrouping.Group(new[]
            {
                Risk("risky", PortfolioBucket.AtRisk, score: 90, days: 4),
                Risk("overdue", PortfolioBucket.AtRisk, score: 1, days: -1),
            });

            CollectionAssert.AreEqual(new[] { "overdue", "risky" }, RiskIds(groups.StartNow));
        }

        [TestMethod]
        public void AnEmptyPortfolioGroupsToNothing()
        {
            Assert.IsTrue(PortfolioGrouping.Group(new PortfolioProject[0]).IsEmpty);
        }

        [TestMethod]
        public void BadgesDropTheOnesTheSectionHeadingAlreadySays()
        {
            // Report 2026-09-11_20-39-23: a card under "Ask for an extension" carried "Overloaded",
            // which repeats the heading, and "Start now", which contradicts it.
            var all = new[]
            {
                PortfolioBadge.StartNow, PortfolioBadge.Overcommitted,
                PortfolioBadge.Sacrifice, PortfolioBadge.Unscheduled,
            };

            CollectionAssert.AreEqual(new[] { PortfolioBadge.Sacrifice, PortfolioBadge.Unscheduled },
                                      (System.Collections.ICollection)PortfolioGrouping
                                          .Badges(all, PortfolioActionSection.AskForExtension));
            CollectionAssert.AreEqual(
                new[] { PortfolioBadge.Overcommitted, PortfolioBadge.Sacrifice, PortfolioBadge.Unscheduled },
                (System.Collections.ICollection)PortfolioGrouping.Badges(all, PortfolioActionSection.StartNow),
                "Overloaded still says something outside the extension section");
            CollectionAssert.AreEqual(all, (System.Collections.ICollection)PortfolioGrouping.Badges(all, null),
                                      "an ungrouped card has no heading to lean on");
        }

        [TestMethod]
        public void SearchFiltersByNameAndABlankQueryIsEverything()
        {
            var projects = new[]
            {
                Risk("Grant application", PortfolioBucket.Critical),
                Risk("House", PortfolioBucket.OnTrack),
            };

            CollectionAssert.AreEqual(new[] { "Grant application" },
                                      RiskIds(PortfolioGrouping.Matching(projects, "GRANT")));
            Assert.AreEqual(2, PortfolioGrouping.Matching(projects, "   ").Count,
                            "the sections behave as before until the user types");
        }

        [TestMethod]
        public void RecommendedDailyHoursSpreadTheEffortOverTheDaysLeft()
        {
            Assert.IsNull(PortfolioGrouping.RecommendedDailyHours(Risk("a", PortfolioBucket.NoDeadline, hours: 2)),
                          "no deadline, no pace");
            Assert.IsNull(PortfolioGrouping.RecommendedDailyHours(Risk("b", PortfolioBucket.AtRisk, days: 4)),
                          "no remaining effort, no pace");
            Assert.AreEqual(2, PortfolioGrouping
                .RecommendedDailyHours(Risk("c", PortfolioBucket.Critical, days: -1, hours: 2)).Value, Tolerance,
                "due today or overdue means all of it today");
            Assert.AreEqual(0.5, PortfolioGrouping
                .RecommendedDailyHours(Risk("d", PortfolioBucket.AtRisk, days: 4, hours: 2)).Value, Tolerance);
        }

        [TestMethod]
        public void APaceIsRoundedToFiveMinutesAndNeverReadsAsNothing()
        {
            Assert.AreEqual(new PortfolioDurationParts(1, 30), PortfolioGrouping.DurationParts(1.51));
            Assert.AreEqual(5, PortfolioGrouping.DurationParts(0.01).Minutes, "never below five minutes");
            Assert.AreEqual(new PortfolioDurationParts(0, 0), PortfolioGrouping.DurationParts(0));
        }

        [TestMethod]
        public void APaceRoundsHalvesAwayFromZeroAsSwiftDoes()
        {
            // 7.5 minutes is one and a half steps: Swift's rounded() gives 2 steps (10 minutes) where
            // .NET's default banker's rounding would give 2 as well, but 12.5 minutes (2.5 steps) is the
            // case that splits them — Swift says 3 steps, banker's rounding says 2.
            Assert.AreEqual(15, PortfolioGrouping.DurationParts(12.5 / 60).Minutes);
        }

        [TestMethod]
        public void OvercommittedNamesTruncate()
        {
            var projects = new List<PortfolioProject>();
            foreach (string name in new[] { "A", "B", "C", "D", "E" })
            {
                projects.Add(Risk(name, PortfolioBucket.Critical));
            }

            Assert.AreEqual("A, B, C, and 2 more",
                            PortfolioGrouping.OvercommittedNames(projects, count => "and " + count + " more"));
            Assert.AreEqual("A, B",
                            PortfolioGrouping.OvercommittedNames(projects.GetRange(0, 2),
                                                                 count => "and " + count + " more"));
            Assert.AreEqual(string.Empty,
                            PortfolioGrouping.OvercommittedNames(new PortfolioProject[0], count => "never"));
        }

        [TestMethod]
        public void SortByRiskPutsUnscoredAndDeadlineFreeProjectsLast()
        {
            var risks = new Dictionary<string, PortfolioProject>
            {
                { "risky", Risk("risky", PortfolioBucket.Critical, score: 90) },
                { "calm", Risk("calm", PortfolioBucket.OnTrack, score: 10) },
                { "undated", Risk("undated", PortfolioBucket.NoDeadline, score: 99) },
            };
            var projects = new[]
            {
                Project(id: "undated", name: "Undated"),
                Project(id: "unknown", name: "Unknown"),
                Project(id: "calm", name: "Calm"),
                Project(id: "risky", name: "Risky"),
            };

            CollectionAssert.AreEqual(new[] { "risky", "calm", "undated", "unknown" },
                                      ProjectIds(PortfolioGrouping.SortByRisk(projects, risks)),
                                      "a no-deadline project's score does not count, and ties keep their order");
        }

        [TestMethod]
        public void AnUnknownBadgeOrBucketNeverBreaksTheView()
        {
            Assert.AreEqual(PortfolioBadge.Unknown, PortfolioBadgeWire.Parse("brand_new_badge"));
            Assert.AreEqual(PortfolioBucket.NoDeadline, PortfolioBucketWire.Parse("brand_new_bucket"));
            Assert.AreEqual(PortfolioImportance.Medium,
                            Risk("a", PortfolioBucket.OnTrack, importance: 9).ImportanceLevel);
        }
    }
}
