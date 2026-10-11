using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Schedule;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Tests.Schedule
{
    /// <summary>
    /// Report 2026-09-25_09-44-27: "why do most of the tasks in the day plan not have projects
    /// associated". A to-do's project is its project_id resolved against /projects, else its first tag
    /// (Mac DayPlanTaskStore.projectName(for:)).
    /// </summary>
    [TestClass]
    public class TodoProjectsTests
    {
        private static readonly Dictionary<string, string> Projects = new Dictionary<string, string>
        {
            { "p-book", "Book" },
            { "p-sales", "Sales" },
        };

        private static JObject Todo(string json) => JObject.Parse(json);

        [TestMethod]
        public void ProjectIdWinsOverTheTag()
        {
            var todo = Todo(@"{ ""project_id"": ""p-book"", ""tags"": [ { ""id"": ""t"", ""text"": ""Old name"" } ] }");
            Assert.AreEqual("Book", TodoProjects.ProjectName(todo, Projects));
        }

        [TestMethod]
        public void AProjectIdWithNoTagStillNamesTheProject()
        {
            // The reported bug: these showed under "No project" because only tags[0] was read.
            Assert.AreEqual("Sales", TodoProjects.ProjectName(Todo(@"{ ""project_id"": ""p-sales"", ""tags"": [] }"), Projects));
            Assert.AreEqual("Sales", TodoProjects.ProjectName(Todo(@"{ ""project_id"": ""p-sales"" }"), Projects));
        }

        [TestMethod]
        public void FallsBackToTheFirstTag()
        {
            Assert.AreEqual("ClickUp list", TodoProjects.ProjectName(
                Todo(@"{ ""project_id"": null, ""synced_project_id"": ""cu1"", ""tags"": [ { ""id"": ""1"", ""text"": ""ClickUp list"" } ] }"), Projects));
            Assert.AreEqual("Legacy", TodoProjects.ProjectName(Todo(@"{ ""tags"": [ { ""name"": ""Legacy"" } ] }"), Projects));
            Assert.AreEqual("Tagged", TodoProjects.ProjectName(Todo(@"{ ""project_id"": ""unknown"", ""tags"": [ { ""text"": ""Tagged"" } ] }"), Projects),
                            "an id not in /projects (yet) falls back to the tag");
        }

        [TestMethod]
        public void NoProjectAtAll()
        {
            Assert.IsNull(TodoProjects.ProjectName(Todo(@"{ ""title"": ""x"" }"), Projects));
            Assert.IsNull(TodoProjects.ProjectName(Todo(@"{ ""tags"": [ { ""text"": ""  "" } ] }"), Projects));
            Assert.IsNull(TodoProjects.ProjectName(Todo(@"{ ""project_id"": ""p-book"" }"), null));
        }

        // MARK: - Refreshing a task already on the day

        [TestMethod]
        public void AnUnnamedTaskPicksUpItsProject()
        {
            Assert.AreEqual("Book", TodoProjects.RefreshedName(null, null, Todo(@"{ ""project_id"": ""p-book"" }"), Projects));
            Assert.AreEqual("Tagged", TodoProjects.RefreshedName("", null, Todo(@"{ ""tags"": [ { ""text"": ""Tagged"" } ] }"), Projects));
        }

        [TestMethod]
        public void ALinkedTaskFollowsARenameOrRelinkElsewhere()
        {
            Assert.AreEqual("Sales", TodoProjects.RefreshedName("Book", "p-book", Todo(@"{ ""project_id"": ""p-sales"" }"), Projects));
        }

        [TestMethod]
        public void ALocallyTypedProjectIsKeptUntilTheBackendKnowsIt()
        {
            Assert.AreEqual("Garden", TodoProjects.RefreshedName("Garden", null, Todo(@"{ ""title"": ""dig"" }"), Projects));
            Assert.AreEqual("Garden", TodoProjects.RefreshedName("Garden", null, Todo(@"{ ""tags"": [ { ""text"": ""label"" } ] }"), Projects),
                            "a plain label tag doesn't override a name typed here");
        }

        [TestMethod]
        public void AnUnresolvableIdNeverBlanksAName()
        {
            Assert.AreEqual("Book", TodoProjects.RefreshedName("Book", "p-book", Todo(@"{ ""project_id"": ""p-book"" }"), new Dictionary<string, string>()));
        }

        [TestMethod]
        public void AnUnlinkElsewhereClearsTheName()
        {
            Assert.IsNull(TodoProjects.RefreshedName("Book", "p-book", Todo(@"{ ""project_id"": null, ""tags"": [] }"), Projects));
        }

        // MARK: - /projects pages

        [TestMethod]
        public void ParsesEveryResponseShape()
        {
            int total;
            var bare = TodoProjects.ParseProjectsPage(JToken.Parse(@"[ { ""id"": ""a"", ""name"": ""A"" }, { ""id"": ""b"", ""title"": ""B"" }, { ""id"": ""c"" } ]"), out total);
            CollectionAssert.AreEqual(new[] { "A", "B" }, bare.Select(p => p.Value).ToList());
            Assert.AreEqual(0, total);

            var wrapped = TodoProjects.ParseProjectsPage(JToken.Parse(@"{ ""data"": [ { ""id"": ""a"", ""name"": ""A"" } ], ""total_count"": 7 }"), out total);
            Assert.AreEqual("a", wrapped.Single().Key);
            Assert.AreEqual(7, total);

            var meta = TodoProjects.ParseProjectsPage(JToken.Parse(@"{ ""projects"": [ { ""id"": ""a"", ""name"": ""A"" } ], ""meta"": { ""total"": 3 } }"), out total);
            Assert.AreEqual(1, meta.Count);
            Assert.AreEqual(3, total);
        }
    }
}
