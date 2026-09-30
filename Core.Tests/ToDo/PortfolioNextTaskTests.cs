using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FocusBear.Core.Tests.ToDo.ToDoFixtures;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>
    /// The task a project card offers as its NEXT one, ported from the Mac's `PortfolioNextTask` cases.
    /// </summary>
    [TestClass]
    public class PortfolioNextTaskTests
    {
        private const double Tolerance = 0.0001;

        private static string[] TargetIds(IEnumerable<PortfolioFetchTarget> targets)
        {
            return targets.Select(target => target.ProjectId).ToArray();
        }

        [TestMethod]
        public void TheNextTaskIsNeverOneTheUserHasAlreadyTickedOff()
        {
            // The reported bug: the wire payload's own `top_priority_task` named a completed to-do on 14
            // of 30 project cards, because the server ranks completed tasks on the score they kept.
            var tasks = new[]
            {
                Task("done", ToDoStatus.Completed, dueOffset: -3, outcome: 8, perspiration: 2, topScore: 44),
                Task("open", dueOffset: 2, outcome: 3, perspiration: 2),
            };

            Assert.AreEqual("open", PortfolioNextTask.Pick(tasks, Now).Id);
        }

        [TestMethod]
        public void TheNextTaskIsTheHighestScoringOpenOne()
        {
            var tasks = new[]
            {
                Task("low", dueOffset: 30, outcome: 1, perspiration: 5),
                Task("high", dueOffset: 0, outcome: 9, perspiration: 1),
                Task("mid", dueOffset: 5, outcome: 5, perspiration: 3),
            };

            ProjectTopPriorityTask picked = PortfolioNextTask.Pick(tasks, Now);

            Assert.AreEqual("high", picked.Id);
            Assert.AreEqual("high", picked.Title);
            Assert.AreEqual(ToDoScoring.TopScoreOf(tasks[1], Now), picked.TopScore, Tolerance,
                            "the card shows the locally computed score, not the stored one");
        }

        [TestMethod]
        public void TheCardsScoreIsTheLiveOneNotTheStaleStoredValue()
        {
            // A single row whose stored score contradicts its inputs: the card must show what the rest
            // of the app ranks by, or the same task reads two different urgencies on two screens.
            ToDoTask stale = Task("t", dueOffset: 0, outcome: 5, perspiration: 2, topScore: 999);

            Assert.AreEqual(ToDoScoring.TopScore(5, 2, Day(0), Now), PortfolioNextTask.Pick(new[] { stale }, Now).TopScore,
                            Tolerance);
        }

        [TestMethod]
        public void AFinishedProjectOffersNoNextTaskRatherThanAStaleOne()
        {
            Assert.IsNull(PortfolioNextTask.Pick(
                new[] { Task("a", ToDoStatus.Completed), Task("b", ToDoStatus.Completed) }, Now));
            Assert.IsNull(PortfolioNextTask.Pick(new ToDoTask[0], Now));
        }

        [TestMethod]
        public void FetchTargetsSkipEmptyProjectsAndDeduplicate()
        {
            var projects = new[]
            {
                Project(id: "a", name: "a", taskCount: 3),
                Project(id: "b", name: "b", taskCount: 0),
                Project(id: "c", name: "c", taskCount: 1, syncedProjectId: "ext-c"),
            };

            IList<PortfolioFetchTarget> targets = PortfolioNextTask.FetchTargets(
                projects, new[] { "a", "portfolio-only" });

            CollectionAssert.AreEqual(new[] { "a", "c", "portfolio-only" }, TargetIds(targets),
                                      "empty projects are skipped; a listed project is not fetched twice");
            Assert.AreEqual("ext-c", targets[1].SyncedProjectId, "a synced project is queried by its external id");
            Assert.IsNull(targets[2].SyncedProjectId);
        }

        [TestMethod]
        public void AProjectWithNoReportedTaskCountIsNotFetched()
        {
            // `GET /projects` omits the count on some payloads; fetching every project on the chance it
            // has tasks is what the per-project concurrency limit exists to avoid.
            IList<PortfolioFetchTarget> targets = PortfolioNextTask.FetchTargets(
                new[] { Project(id: "a", name: "a") });

            Assert.AreEqual(0, targets.Count);
        }

        [TestMethod]
        public void UncachedTargetsSkipAlreadyResolvedProjects()
        {
            IList<PortfolioFetchTarget> targets = PortfolioNextTask.FetchTargets(new[]
            {
                Project(id: "a", name: "a", taskCount: 1),
                Project(id: "b", name: "b", taskCount: 1),
            });

            CollectionAssert.AreEqual(new[] { "b" },
                                      TargetIds(PortfolioNextTask.Uncached(targets, new HashSet<string> { "a" })));
            Assert.AreEqual(0, PortfolioNextTask.Uncached(targets, new HashSet<string> { "a", "b" }).Count);
            Assert.AreEqual(2, PortfolioNextTask.Uncached(targets, new HashSet<string>()).Count);
        }
    }
}
