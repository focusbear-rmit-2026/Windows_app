using System;
using System.IO;
using FocusBear.Core.Notes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Notes
{
    [TestClass]
    public class ProjectNoteSyncTests
    {
        private static readonly ProjectNoteIdentity Me = new ProjectNoteIdentity("acct-1", "proj-1", "note-1");

        private static string Snapshot(long revision, string state = "AQID") =>
            "{\"revision\":" + revision + ",\"yjs_state\":\"" + state + "\",\"content\":{\"type\":\"doc\",\"content\":[]}}";

        private static string TempDirectory() => Path.Combine(Path.GetTempPath(), "fb-note-drafts-" + Guid.NewGuid().ToString("N"));

        [TestMethod]
        public void OnlyASnapshotTheEditorCouldHaveProducedHasARevision()
        {
            Assert.AreEqual(3L, ProjectNoteSnapshot.Revision(Snapshot(3)));
            Assert.IsNull(ProjectNoteSnapshot.Revision(Snapshot(-1)), "negative revision");
            Assert.IsNull(ProjectNoteSnapshot.Revision(Snapshot(1, "")), "empty state");
            Assert.IsNull(ProjectNoteSnapshot.Revision(Snapshot(1, "AQI")), "non-canonical base64");
            Assert.IsNull(ProjectNoteSnapshot.Revision("{\"revision\":1,\"yjs_state\":\"AQID\",\"content\":{\"type\":\"paragraph\"}}"), "root must be a doc");
            Assert.IsNull(ProjectNoteSnapshot.Revision("{\"revision\":1.5,\"yjs_state\":\"AQID\",\"content\":{\"type\":\"doc\"}}"), "fractional revision");
            Assert.IsNull(ProjectNoteSnapshot.Revision("{\"revision\":true,\"yjs_state\":\"AQID\",\"content\":{\"type\":\"doc\"}}"), "boolean revision");
            Assert.IsNull(ProjectNoteSnapshot.Revision("not json"));
        }

        [TestMethod]
        public void DraftsAreScopedToTheWholeIdentity_AndOnlyTheOwnedCopyIsReplacedOrRemoved()
        {
            string dir = TempDirectory();
            try
            {
                var store = new ProjectNoteDraftStore(dir);
                Assert.IsNull(store.Load(Me));
                store.Save(Snapshot(1), Me);
                Assert.AreEqual(Snapshot(1), store.Load(Me));
                Assert.IsNull(store.Load(new ProjectNoteIdentity("acct-2", "proj-1", "note-1")), "another account never sees it");

                Assert.IsFalse(store.Save(Snapshot(2), Me, expected: Snapshot(9)), "someone else's copy is left alone");
                Assert.IsTrue(store.Save(Snapshot(2), Me, expected: Snapshot(1)));
                Assert.IsFalse(store.Remove(Me, Snapshot(1)), "a stale acknowledgement keeps the newer draft");
                Assert.IsTrue(store.Remove(Me, Snapshot(2)));
                Assert.IsNull(store.Load(Me));
                Assert.ThrowsException<ArgumentException>(() => store.Save("{}", Me));
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }

        [TestMethod]
        public void TheQueueSendsAtMostEightASecond()
        {
            var queue = new ProjectNoteOutboundQueue(Me);
            for (int i = 0; i < 10; i++) Assert.AreEqual(ProjectNoteEnqueueResult.Accepted, queue.Enqueue("client-yjs-update", "{\"i\":" + i + "}", Me));
            int sent = 0;
            for (int tick = 0; tick < 10; tick++) if (queue.Next(0.05 * tick, Me) != null) sent++;
            Assert.AreEqual(8, sent, "within the first second");
            Assert.IsNotNull(queue.Next(1.1, Me), "the budget refills");
        }

        [TestMethod]
        public void ABadMessageOrAnotherNoteForcesAResync()
        {
            var queue = new ProjectNoteOutboundQueue(Me);
            Assert.AreEqual(ProjectNoteEnqueueResult.WrongIdentity, queue.Enqueue("client-yjs-update", "{}", new ProjectNoteIdentity("acct-1", "proj-1", "note-2")));
            Assert.AreEqual(ProjectNoteEnqueueResult.ResyncRequired, queue.Enqueue("client-other", "{\"a\":1}", Me));
            Assert.IsTrue(queue.RequiresResync);
            Assert.AreEqual(ProjectNoteEnqueueResult.ResyncRequired, queue.Enqueue("client-yjs-update", "{\"a\":1}", Me), "sticky until resumed");
            queue.ResumeAfterResync();
            Assert.AreEqual(ProjectNoteEnqueueResult.Accepted, queue.Enqueue("client-yjs-update", "{\"a\":1}", Me));
            Assert.AreEqual(ProjectNoteEnqueueResult.ResyncRequired, queue.Enqueue("client-yjs-update", "{\"d\":\"" + new string('x', 9600) + "\"}", Me), "oversized payload");
        }

        [TestMethod]
        public void OnlySafeIdsReachTheUrl()
        {
            Assert.IsTrue(Me.IsSafeForPath);
            Assert.IsFalse(new ProjectNoteIdentity("a", "../x", "n").IsSafeForPath);
            Assert.AreEqual("presence-project-note-note-1", Me.ChannelName);
        }
    }
}
