using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FocusBear.Core.Sync;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Sync
{
    /// <summary>
    /// The offline queue of completed activities. The old sync deleted the whole queue once a batch had failed
    /// five times (a network outage lost everything), treated a 4xx on a batch as "sent" (one bad entry threw
    /// away nine good ones), and only ever read the first 500 entries before deleting the file.
    /// </summary>
    [TestClass]
    public class OfflineQueueSyncTests
    {
        /// <summary>A fake API: rejects any batch holding a "bad" entry, and goes down after a number of calls.</summary>
        private sealed class FakeApi
        {
            public int DownAfterCalls = int.MaxValue;
            public readonly List<string> Stored = new List<string>();
            public readonly List<int> BatchSizes = new List<int>();
            private int _calls;

            public Task<OfflineBatchResult> Send(IReadOnlyList<string> batch)
            {
                _calls++;
                BatchSizes.Add(batch.Count);
                if (_calls > DownAfterCalls)
                    return Task.FromResult(OfflineBatchResult.Unavailable);
                if (batch.Any(e => e.StartsWith("bad")))
                    return Task.FromResult(OfflineBatchResult.Rejected);
                Stored.AddRange(batch);
                return Task.FromResult(OfflineBatchResult.Accepted);
            }
        }

        private static List<string> Entries(int n, params int[] badAt) =>
            Enumerable.Range(0, n).Select(i => (badAt.Contains(i) ? "bad" : "ok") + i).ToList();

        [TestMethod]
        public void EverythingAccepted_NothingKept()
        {
            var api = new FakeApi();
            var result = OfflineQueueSync.RunAsync(Entries(25), 10, api.Send).Result;
            Assert.AreEqual(25, result.Sent);
            Assert.AreEqual(0, result.Kept.Count);
            Assert.AreEqual(0, result.Dropped.Count);
            CollectionAssert.AreEqual(new[] { 10, 10, 5 }, api.BatchSizes);
        }

        [TestMethod]
        public void ARejectedBatchIsRetriedOneByOne_AndOnlyTheBadEntryIsDropped()
        {
            var api = new FakeApi();
            var result = OfflineQueueSync.RunAsync(Entries(12, 3), 10, api.Send).Result;
            CollectionAssert.AreEqual(new[] { "bad3" }, result.Dropped.ToList());
            Assert.AreEqual(11, result.Sent);
            Assert.AreEqual(0, result.Kept.Count);
            Assert.AreEqual(11, api.Stored.Count, "the nine good entries of the rejected batch still reach the server");
        }

        [TestMethod]
        public void TheServerGoingAway_KeepsEverythingNotYetSent()
        {
            var api = new FakeApi { DownAfterCalls = 1 };
            var result = OfflineQueueSync.RunAsync(Entries(25), 10, api.Send).Result;
            Assert.AreEqual(10, result.Sent);
            Assert.IsTrue(result.StoppedUnavailable);
            CollectionAssert.AreEqual(Entries(25).Skip(10).ToList(), result.Kept.ToList());
            Assert.AreEqual(0, result.Dropped.Count);
        }

        [TestMethod]
        public void GoingAwayDuringTheOneByOneRetry_KeepsTheRestOfThatBatchToo()
        {
            // Call 1: batch rejected. Calls 2-3: ok0, ok1 alone. Call 4: down.
            var api = new FakeApi { DownAfterCalls = 3 };
            var result = OfflineQueueSync.RunAsync(Entries(12, 5), 10, api.Send).Result;
            Assert.AreEqual(2, result.Sent);
            Assert.IsTrue(result.StoppedUnavailable);
            CollectionAssert.AreEqual(Entries(12, 5).Skip(2).ToList(), result.Kept.ToList());
        }

        [TestMethod]
        public void AnswersAreClassified()
        {
            Assert.AreEqual(OfflineBatchResult.Accepted, OfflineQueueSync.Classify(201));
            Assert.AreEqual(OfflineBatchResult.Rejected, OfflineQueueSync.Classify(400));
            Assert.AreEqual(OfflineBatchResult.Rejected, OfflineQueueSync.Classify(422));
            foreach (int? code in new int?[] { null, 401, 403, 408, 429, 500, 502, 503 })
                Assert.AreEqual(OfflineBatchResult.Unavailable, OfflineQueueSync.Classify(code), code?.ToString() ?? "no answer");
        }

        [TestMethod]
        public void CapKeepsTheNewest()
        {
            var capped = OfflineQueueSync.Cap(Entries(10), 4);
            CollectionAssert.AreEqual(new[] { "ok6", "ok7", "ok8", "ok9" }, capped.ToList());
            Assert.AreEqual(3, OfflineQueueSync.Cap(Entries(3), 4).Count);
        }

        [TestMethod]
        public void BackoffWaitsLongerAfterEachOutage_AndResetsOnSuccess()
        {
            var t0 = new DateTime(2026, 9, 27, 8, 0, 0);
            var backoff = new OfflineSyncBackoff(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(30));
            Assert.IsTrue(backoff.CanAttempt(t0));

            backoff.Failed(t0);
            Assert.IsFalse(backoff.CanAttempt(t0.AddSeconds(29)));
            Assert.IsTrue(backoff.CanAttempt(t0.AddSeconds(30)));

            backoff.Failed(t0);
            Assert.IsFalse(backoff.CanAttempt(t0.AddSeconds(59)));
            Assert.IsTrue(backoff.CanAttempt(t0.AddSeconds(60)));

            for (int i = 0; i < 20; i++) backoff.Failed(t0);
            Assert.IsTrue(backoff.CanAttempt(t0.AddMinutes(30)), "capped");

            backoff.Succeeded();
            Assert.IsTrue(backoff.CanAttempt(t0));
        }
    }
}
