using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FocusBear.Core.Sync
{
    public enum OfflineBatchResult
    {
        /// <summary>The server stored the batch.</summary>
        Accepted,
        /// <summary>The server refused the batch (a 4xx other than timeout, auth or rate limit): something in it is bad.</summary>
        Rejected,
        /// <summary>No answer, or a 5xx / timeout / auth / rate-limit answer: try again later.</summary>
        Unavailable,
    }

    public sealed class OfflineSyncResult<T>
    {
        public int Sent { get; internal set; }
        /// <summary>Entries the server refused on their own; they are not retried.</summary>
        public IReadOnlyList<T> Dropped { get; internal set; }
        /// <summary>Entries not sent yet, in order, to keep for the next attempt.</summary>
        public IReadOnlyList<T> Kept { get; internal set; }
        public bool StoppedUnavailable { get; internal set; }
    }

    /// <summary>
    /// Sends the offline queue of completed activities in batches. A batch the server refuses is retried one
    /// entry at a time so only the entry it refuses on its own is dropped; when the server can't be reached the
    /// run stops and everything not yet sent is kept. The old sync deleted the whole queue after five failed
    /// batches, and counted a refused batch as sent (one bad entry threw away nine good ones).
    /// </summary>
    public static class OfflineQueueSync
    {
        public const int MaxQueue = 5000;

        public static async Task<OfflineSyncResult<T>> RunAsync<T>(IReadOnlyList<T> entries, int batchSize,
                                                                   Func<IReadOnlyList<T>, Task<OfflineBatchResult>> send)
        {
            if (batchSize < 1) batchSize = 1;
            var dropped = new List<T>();
            int sent = 0;
            int i = 0;
            bool unavailable = false;

            while (i < entries.Count && !unavailable)
            {
                List<T> batch = entries.Skip(i).Take(batchSize).ToList();
                OfflineBatchResult result = await send(batch).ConfigureAwait(false);
                if (result == OfflineBatchResult.Accepted)
                {
                    sent += batch.Count;
                    i += batch.Count;
                }
                else if (result == OfflineBatchResult.Unavailable)
                {
                    unavailable = true;
                }
                else if (batch.Count == 1)
                {
                    dropped.Add(batch[0]);
                    i++;
                }
                else
                {
                    // Find the bad entry: the rest of this batch one at a time.
                    foreach (T entry in batch)
                    {
                        OfflineBatchResult single = await send(new[] { entry }).ConfigureAwait(false);
                        if (single == OfflineBatchResult.Unavailable)
                        {
                            unavailable = true;
                            break;
                        }
                        if (single == OfflineBatchResult.Accepted)
                            sent++;
                        else
                            dropped.Add(entry);
                        i++;
                    }
                }
            }

            return new OfflineSyncResult<T>
            {
                Sent = sent,
                Dropped = dropped,
                Kept = entries.Skip(i).ToList(),
                StoppedUnavailable = unavailable,
            };
        }

        /// <summary>
        /// What an answer means for the queue: 2xx stored; no answer, 5xx, 408 timeout, 401/403 auth and 429 rate
        /// limit are "try later"; any other 4xx is the server refusing the content.
        /// </summary>
        public static OfflineBatchResult Classify(int? statusCode)
        {
            if (statusCode == null)
                return OfflineBatchResult.Unavailable;
            int code = statusCode.Value;
            if (code >= 200 && code < 300)
                return OfflineBatchResult.Accepted;
            if (code >= 400 && code < 500 && code != 401 && code != 403 && code != 408 && code != 429)
                return OfflineBatchResult.Rejected;
            return OfflineBatchResult.Unavailable;
        }

        /// <summary>At most <paramref name="max"/> entries, keeping the newest (the end of the list).</summary>
        public static IReadOnlyList<T> Cap<T>(IReadOnlyList<T> entries, int max)
        {
            if (entries.Count <= max)
                return entries;
            return entries.Skip(entries.Count - max).ToList();
        }
    }

    /// <summary>
    /// When the next offline sync may run after the server could not be reached: the wait doubles from
    /// <c>initial</c> up to <c>max</c>, and a successful run resets it.
    /// </summary>
    public sealed class OfflineSyncBackoff
    {
        private readonly TimeSpan _initial;
        private readonly TimeSpan _max;
        private int _failures;
        private DateTime _notBefore = DateTime.MinValue;

        public OfflineSyncBackoff(TimeSpan initial, TimeSpan max)
        {
            _initial = initial;
            _max = max;
        }

        public bool CanAttempt(DateTime now) => now >= _notBefore;

        public void Failed(DateTime now)
        {
            _failures = Math.Min(_failures + 1, 30);
            double seconds = Math.Min(_initial.TotalSeconds * Math.Pow(2, _failures - 1), _max.TotalSeconds);
            _notBefore = now.AddSeconds(seconds);
        }

        public void Succeeded()
        {
            _failures = 0;
            _notBefore = DateTime.MinValue;
        }
    }
}
