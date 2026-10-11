using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FocusBear.Core.Notes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Tests.Notes
{
    /// <summary>
    /// A project's shared notes past the first page. <c>GET /projects/:id/notes</c> pages at 20 by default and
    /// the screen read page 1 only, so a project's 21st note simply did not exist on Windows.
    /// </summary>
    [TestClass]
    public class ProjectNotesPagingTests
    {
        private static string Page(IEnumerable<string> ids, bool hasNext) =>
            new JObject
            {
                ["data"] = new JArray(ids.Select(id => new JObject { ["id"] = id, ["title"] = "Note " + id })),
                ["meta"] = new JObject { ["hasNextPage"] = hasNext },
            }.ToString();

        private static Func<int, Task<Tuple<int, string>>> Server(Dictionary<int, Tuple<int, string>> pages, List<int> asked) =>
            page =>
            {
                asked.Add(page);
                return Task.FromResult(pages.TryGetValue(page, out var answer) ? answer : Tuple.Create(200, Page(new string[0], false)));
            };

        [TestMethod]
        public void FetchAll_ReadsEveryPageUntilTheServerSaysThereIsNoMore()
        {
            var asked = new List<int>();
            var pages = new Dictionary<int, Tuple<int, string>>
            {
                [1] = Tuple.Create(200, Page(Enumerable.Range(1, 50).Select(i => "n" + i), true)),
                [2] = Tuple.Create(200, Page(new[] { "n51", "n52" }, false)),
            };

            ProjectNotesFetch result = ProjectNotesPaging.FetchAll(Server(pages, asked)).GetAwaiter().GetResult();

            Assert.AreEqual(52, result.Notes.Count);
            Assert.AreEqual("n52", result.Notes.Last().Id);
            CollectionAssert.AreEqual(new[] { 1, 2 }, asked);
        }

        [TestMethod]
        public void FetchAll_ALaterPageThatFails_FailsTheProject_NotShortensIt()
        {
            // A quietly short list is indistinguishable from the notes being gone; the sweep retries a 429.
            var pages = new Dictionary<int, Tuple<int, string>>
            {
                [1] = Tuple.Create(200, Page(new[] { "a" }, true)),
                [2] = Tuple.Create(429, "{\"message\":\"slow down\"}"),
            };

            ProjectNotesFetch result = ProjectNotesPaging.FetchAll(Server(pages, new List<int>())).GetAwaiter().GetResult();

            Assert.IsNull(result.Notes);
            Assert.AreEqual(429, result.Status);
        }

        [TestMethod]
        public void FetchAll_AnUnreadableSuccessIsATemporaryFailure()
        {
            var pages = new Dictionary<int, Tuple<int, string>> { [1] = Tuple.Create(200, "<html>proxy</html>") };

            ProjectNotesFetch result = ProjectNotesPaging.FetchAll(Server(pages, new List<int>())).GetAwaiter().GetResult();

            Assert.IsNull(result.Notes);
            Assert.AreEqual(0, result.Status, "0 is the sweep's \"ask again shortly\"");
        }

        [TestMethod]
        public void FetchAll_ANextPageThatRepeatsItselfStops_AndNothingIsListedTwice()
        {
            var asked = new List<int>();
            var same = Tuple.Create(200, Page(new[] { "a", "b" }, true));
            var pages = new Dictionary<int, Tuple<int, string>> { [1] = same, [2] = same, [3] = same };

            ProjectNotesFetch result = ProjectNotesPaging.FetchAll(Server(pages, asked)).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "a", "b" }, result.Notes.Select(n => n.Id).ToList());
            CollectionAssert.AreEqual(new[] { 1, 2 }, asked);
        }

        [TestMethod]
        public void FetchAll_ABareArrayIsUnreadable_NotAThrow()
        {
            var pages = new Dictionary<int, Tuple<int, string>> { [1] = Tuple.Create(200, "[{\"id\":\"a\"}]") };

            Assert.IsNull(ProjectNotesPaging.FetchAll(Server(pages, new List<int>())).GetAwaiter().GetResult().Notes);
        }
    }
}
