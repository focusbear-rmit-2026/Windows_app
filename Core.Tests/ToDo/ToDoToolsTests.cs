using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    [TestClass]
    public class ToDoToolsTests
    {
        [TestMethod]
        public void ABriefDumpIsVague_AFullSentenceIsNot()
        {
            Assert.IsTrue(ToDoTools.IsBrainDumpVague("buy milk"));
            Assert.IsTrue(ToDoTools.IsBrainDumpVague("a b c d e"), "five words but under twenty characters");
            Assert.IsFalse(ToDoTools.IsBrainDumpVague("email the landlord about the leaking tap and book a plumber"));
        }

        [TestMethod]
        public void LongerDumpsGetLongerTimeouts()
        {
            Assert.AreEqual(TimeSpan.FromSeconds(30), ToDoTools.BrainDumpTimeout(300));
            Assert.AreEqual(TimeSpan.FromSeconds(45), ToDoTools.BrainDumpTimeout(301));
            Assert.AreEqual(TimeSpan.FromSeconds(60), ToDoTools.BrainDumpTimeout(701));
        }

        [TestMethod]
        public void BrainDumpSuggestions_ReadEitherEstimateSpelling_AndGetTheirOwnIds()
        {
            var tasks = ToDoTools.ReadBrainDumpTasks(@"[
              { ""task_name"": ""Email landlord"", ""estimated_time"": 10, ""subtasks"": [""draft"", 3], ""outcome"": 6 },
              { ""task_name"": ""Book plumber"", ""estimated_duration_minutes"": ""20"", ""due_date"": ""2026-09-26"" },
              { ""estimated_time"": 5 }
            ]");
            Assert.AreEqual(3, tasks.Count);
            Assert.AreEqual(10, tasks[0].EstimatedMinutes);
            CollectionAssert.AreEqual(new[] { "draft" }, tasks[0].SubtaskList.ToArray());
            Assert.AreEqual(6, tasks[0].Outcome);
            Assert.AreEqual(20, tasks[1].EstimatedMinutes);
            Assert.AreEqual("2026-09-26", tasks[1].DueDate);
            Assert.AreEqual("", tasks[2].TaskName);
            Assert.AreNotEqual(tasks[0].Id, tasks[1].Id);
            Assert.IsNull(BrainDumpTaskPayload.Body(tasks[2], "2026-09-25"), "a suggestion without a name is not writable");
            Assert.IsNull(ToDoTools.ReadBrainDumpTasks("oops"));
        }

        [TestMethod]
        public void SavedWebsites_AreRead_AndRowsWithoutIdsDropped()
        {
            var sites = ToDoTools.ReadSavedWebsites(@"[{ ""id"": ""w1"", ""url"": ""youtube.com/watch?v=1"", ""title"": null, ""note"": ""later"" }, { ""url"": ""x.com"" }]");
            Assert.AreEqual("w1", sites.Single().Id);
            Assert.AreEqual("later", sites.Single().Note);
        }

        [TestMethod]
        public void UrlsNeedADottedHost_TheSchemeIsOptional()
        {
            Assert.IsTrue(ToDoTools.IsValidUrl("youtube.com/watch?v=1"));
            Assert.IsTrue(ToDoTools.IsValidUrl("https://news.ycombinator.com"));
            Assert.IsFalse(ToDoTools.IsValidUrl("localhost"));
            Assert.IsFalse(ToDoTools.IsValidUrl("not a url"));
            Assert.IsFalse(ToDoTools.IsValidUrl(".com"));
            Assert.IsFalse(ToDoTools.IsValidUrl(""));
        }

        [TestMethod]
        public void DuplicatesIgnoreSchemeWwwCaseAndTrailingSlash()
        {
            var saved = new List<SavedWebsite> { new SavedWebsite { Id = "1", Url = "www.YouTube.com/" } };
            Assert.IsTrue(ToDoTools.IsDuplicateUrl("https://youtube.com", saved));
            Assert.IsFalse(ToDoTools.IsDuplicateUrl("youtube.com/shorts", saved));
        }

        [TestMethod]
        public void OpenableUrlsGainAScheme_AndOnlyWebLinksOpen()
        {
            Assert.AreEqual("https://youtube.com/watch?v=1", ToDoTools.OpenableUrl("youtube.com/watch?v=1"));
            Assert.AreEqual("http://a.org/", ToDoTools.OpenableUrl("http://a.org"));
            Assert.IsNull(ToDoTools.OpenableUrl("file:///c:/windows"));
            Assert.IsNull(ToDoTools.OpenableUrl("javascript://alert(1)"));
        }
    }
}
