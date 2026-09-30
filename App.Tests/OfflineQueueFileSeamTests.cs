using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Focus_Bear.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The offline queue file round trip, against a fake server and a temporary file (never the real queue).
    /// The old sync deleted the file after five failed batches or any exception, and counted a 4xx as sent.
    /// </summary>
    [TestClass]
    public class OfflineQueueFileSeamTests
    {
        private string _path;

        [TestInitialize]
        public void Init() { _path = Path.Combine(Path.GetTempPath(), "fb-offline-" + Guid.NewGuid().ToString("N") + ".csv"); }

        [TestCleanup]
        public void Cleanup() { try { File.Delete(_path); } catch { } }

        private static string Line(string id) => id + ",60,seq-1,2026-09-27T08:00:00Z,2026-09-27T08:01:00Z,,,,,";

        private void Write(params string[] ids) =>
            File.WriteAllLines(_path, new[] { OfflineActivities.Header }.Concat(ids.Select(Line)));

        private List<string> QueuedIds() =>
            File.ReadAllLines(_path).Skip(1).Select(l => l.Split(',')[0]).ToList();

        private static IEnumerable<string> Ids(string body) =>
            ((JArray)JObject.Parse(body)["completed_activities"]).Select(a => (string)a["activity_id"]);

        [TestMethod]
        public void ServerDown_TheWholeQueueStays()
        {
            Write("a", "b", "c");
            var result = OfflineActivities.SyncFileAsync(_path, "dev", body => Task.FromResult<int?>(null)).Result;
            Assert.IsTrue(result.StoppedUnavailable);
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, QueuedIds());

            OfflineActivities.SyncFileAsync(_path, "dev", body => Task.FromResult<int?>(503)).Wait();
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, QueuedIds(), "a 5xx keeps them too");
        }

        [TestMethod]
        public void A4xxDropsOnlyTheEntryTheServerRefuses()
        {
            Write(Enumerable.Range(0, 12).Select(i => i == 4 ? "bad" : "ok" + i).ToArray());
            var stored = new List<string>();
            var result = OfflineActivities.SyncFileAsync(_path, "dev", body =>
            {
                var ids = Ids(body).ToList();
                if (ids.Contains("bad")) return Task.FromResult<int?>(400);
                stored.AddRange(ids);
                return Task.FromResult<int?>(201);
            }).Result;

            Assert.AreEqual(11, stored.Count);
            CollectionAssert.AreEqual(new[] { "bad" }, result.Dropped.Select(l => l.Split(',')[0]).ToList());
            Assert.AreEqual(0, QueuedIds().Count);
        }

        [TestMethod]
        public void AnEntryAddedWhileTheSyncRuns_IsKept()
        {
            Write("a");
            OfflineActivities.SyncFileAsync(_path, "dev", body =>
            {
                OfflineActivities.Append(_path, Line("late"));
                return Task.FromResult<int?>(201);
            }).Wait();
            CollectionAssert.AreEqual(new[] { "late" }, QueuedIds());
        }

        [TestMethod]
        public void UnreadableLinesAreDropped_TheRestSyncs()
        {
            File.WriteAllLines(_path, new[] { OfflineActivities.Header, "garbage", Line("a") });
            var stored = new List<string>();
            OfflineActivities.SyncFileAsync(_path, "dev", body => { stored.AddRange(Ids(body)); return Task.FromResult<int?>(201); }).Wait();
            CollectionAssert.AreEqual(new[] { "a" }, stored);
            Assert.AreEqual(0, QueuedIds().Count);
        }

        [TestMethod]
        public void TheEmptyQueueGate_NeverSkipsANewEntry_AcrossWriteBacks()
        {
            // The 5 s tick reads the queue only when it changed since the read that found it empty. Every
            // write-back must leave the gate open, and an entry appended after any sync must be read.
            var gate = new FocusBear.Core.UnchangedFileGate();
            File.WriteAllLines(_path, new[] { OfflineActivities.Header });

            Assert.IsTrue(OfflineActivities.ShouldSync(_path, gate), "never read yet");
            Assert.IsNull(OfflineActivities.SyncFileAsync(_path, "dev", b => Task.FromResult<int?>(201), gate).Result);
            Assert.IsFalse(OfflineActivities.ShouldSync(_path, gate), "found empty, unchanged since");

            OfflineActivities.Append(_path, Line("a"));
            Assert.IsTrue(OfflineActivities.ShouldSync(_path, gate), "a new entry opens it");

            // Server down: the entry is written back and the gate stays open for the retry.
            OfflineActivities.SyncFileAsync(_path, "dev", b => Task.FromResult<int?>(null), gate).Wait();
            CollectionAssert.AreEqual(new[] { "a" }, QueuedIds());
            Assert.IsTrue(OfflineActivities.ShouldSync(_path, gate));

            // Sent: the file is rewritten empty; the gate is open until a read finds it empty again.
            OfflineActivities.SyncFileAsync(_path, "dev", b => Task.FromResult<int?>(201), gate).Wait();
            Assert.AreEqual(0, QueuedIds().Count);
            Assert.IsTrue(OfflineActivities.ShouldSync(_path, gate));
            Assert.IsNull(OfflineActivities.SyncFileAsync(_path, "dev", b => Task.FromResult<int?>(201), gate).Result);
            Assert.IsFalse(OfflineActivities.ShouldSync(_path, gate));

            OfflineActivities.Append(_path, Line("b"));
            Assert.IsTrue(OfflineActivities.ShouldSync(_path, gate), "an entry after the write-back is still seen");
        }

        [TestMethod]
        public void AppendCapsTheQueue_DroppingTheOldest()
        {
            File.WriteAllLines(_path, new[] { OfflineActivities.Header }.Concat(Enumerable.Range(0, FocusBear.Core.Sync.OfflineQueueSync.MaxQueue).Select(i => Line("old" + i))));
            OfflineActivities.Append(_path, Line("newest"));
            var ids = QueuedIds();
            Assert.AreEqual(FocusBear.Core.Sync.OfflineQueueSync.MaxQueue, ids.Count);
            Assert.AreEqual("old1", ids[0]);
            Assert.AreEqual("newest", ids.Last());
        }
    }
}
