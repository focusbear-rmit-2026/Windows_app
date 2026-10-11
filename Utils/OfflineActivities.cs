using Focus_Bear.API;
using FocusBear.Core.Reports;
using FocusBear.Core.Sync;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Focus_Bear.Utils
{
    /// <summary>
    /// The offline queue of completed activities (FocusBearOfflineData.CSV) and its sync to the server.
    ///
    /// The old sync read at most 500 entries, then deleted the file whatever happened: after five failed
    /// batches (an outage lost the whole queue), after any exception, and after a 4xx, which it counted as
    /// "sent" (one bad entry threw away nine good ones). Now a refused batch is retried one entry at a time and
    /// only the entry the server refuses on its own is dropped (and logged); when the server can't be reached
    /// everything not yet sent is kept and the next attempt waits (doubling, up to 30 minutes); entries added
    /// while a sync runs are kept; and the queue is capped at <see cref="OfflineQueueSync.MaxQueue"/> entries.
    /// </summary>
    public static class OfflineActivities
    {
        internal const string Header = "activity_id,duration_logged,activity_sequence_id,start_time,finish_time,skipped_did_complete,choice_id,quantity_logged,skipped_did_not_complete,note_logged";
        private const int BatchSize = 10;

        /// <summary>Guards every read and write of the queue file (the sync runs off the UI thread).</summary>
        internal static readonly object FileLock = new object();

        private static bool bIsProcesssingOfflineData = false;
        // The queue is empty nearly always: it is read again only once it changes (FocusBear.Core.UnchangedFileGate).
        // Every write-back forgets the gate, so an entry is never skipped because the file looked unchanged.
        private static readonly FocusBear.Core.UnchangedFileGate EmptyQueue = new FocusBear.Core.UnchangedFileGate();
        private static readonly OfflineSyncBackoff Backoff = new OfflineSyncBackoff(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(30));

        public static void ProcessOfflineData()
        {
            if (bIsProcesssingOfflineData || !Backoff.CanAttempt(DateTime.Now))
                return;

            // The file check first: it is one stat and nearly always says "still empty". GetIsNetworkAvailable
            // enumerates every adapter's IP configuration (~18 ms) and this runs on the UI thread every 5 s.
            AppSettings appSettings = AppSettings.GetInstance;
            if (!ShouldSync(appSettings.sOfflineDataCSVPath, EmptyQueue))
                return;
            if (!NetworkInterface.GetIsNetworkAvailable())
                return;
            bIsProcesssingOfflineData = true;

            // Task.Run rather than new Thread(): avoids a Win32Exception when the OS cannot allocate a thread stack.
            _ = Task.Run(async () =>
            {
                try
                {
                    FocusBearAPIClient client = new FocusBearAPIClient();
                    OfflineSyncResult<string> result = await SyncFileAsync(appSettings.sOfflineDataCSVPath, appSettings.sDeviceID, client.PostOfflineBatch, EmptyQueue);
                    if (result == null)
                        return;
                    if (result.StoppedUnavailable)
                        Backoff.Failed(DateTime.Now);
                    else
                        Backoff.Succeeded();
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("ProcessOfflineData() ex - " + ex.Message);
                }
                finally
                {
                    bIsProcesssingOfflineData = false;
                }
            });
        }

        /// <summary>
        /// One sync of the queue file at <paramref name="path"/>: sends it, then writes back what was not sent
        /// plus anything appended meanwhile. Null when the queue was empty. The seam the tests drive with a fake post.
        /// </summary>
        internal static async Task<OfflineSyncResult<string>> SyncFileAsync(string path, string deviceId, Func<string, Task<int?>> post,
                                                                           FocusBear.Core.UnchangedFileGate emptyQueue = null)
        {
            string[] snapshot;
            long? stampLength;
            DateTime? stampWrite;
            lock (FileLock)
            {
                //Stamped under the lock, before the read: an append after it changes the stamp, so it is never skipped.
                QueueFileStamp(path, out stampLength, out stampWrite);
                snapshot = File.Exists(path) ? File.ReadAllLines(path) : new string[0];
            }

            var entries = new List<string>();
            foreach (string line in snapshot.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                if (TryParse(line, out _))
                    entries.Add(line);
                else
                    LogWriter.LogWrite("Offline queue: dropped an unreadable entry " + LogSafe.Describe(line));
            }
            if (entries.Count == 0)
            {
                if (snapshot.Length > 1)
                {
                    WriteBack(path, snapshot.Length, new List<string>()); //dropped unreadable lines: the file changed
                    emptyQueue?.Forget();
                }
                else
                {
                    emptyQueue?.RememberEmpty(stampLength, stampWrite);
                }
                return null;
            }

            LogWriter.LogWrite("Offline queue: " + entries.Count + " entries to sync");
            OfflineSyncResult<string> result = await OfflineQueueSync.RunAsync(entries, BatchSize, async batch =>
            {
                var activities = new JArray();
                foreach (string line in batch)
                {
                    TryParse(line, out OfflineEntry entry);
                    activities.Add(ActivityCompletedBody.FromOffline(entry, deviceId));
                }
                string body = new JObject { ["completed_activities"] = activities }.ToString(Formatting.None);
                return OfflineQueueSync.Classify(await post(body).ConfigureAwait(false));
            }).ConfigureAwait(false);

            foreach (string line in result.Dropped)
            {
                TryParse(line, out OfflineEntry entry);
                LogWriter.LogWrite("Offline queue: the server refused activity " + entry.activity_id + " (" + LogSafe.Describe(line) + "), dropped");
            }
            LogWriter.LogWrite("Offline queue: sent " + result.Sent + ", dropped " + result.Dropped.Count + ", kept " + result.Kept.Count
                               + (result.StoppedUnavailable ? " (server unavailable, will retry)" : ""));

            WriteBack(path, snapshot.Length, result.Kept);
            emptyQueue?.Forget(); //the file was rewritten; the next tick reads it again
            return result;
        }

        /// <summary>Whether the queue file may hold something new since the read that last found it empty.</summary>
        internal static bool ShouldSync(string path, FocusBear.Core.UnchangedFileGate emptyQueue)
        {
            QueueFileStamp(path, out long? length, out DateTime? lastWriteUtc);
            return emptyQueue.ShouldRead(length, lastWriteUtc);
        }

        private static void QueueFileStamp(string path, out long? length, out DateTime? lastWriteUtc)
        {
            length = null;
            lastWriteUtc = null;
            try
            {
                var info = new FileInfo(path);
                if (info.Exists)
                {
                    length = info.Length;
                    lastWriteUtc = info.LastWriteTimeUtc;
                }
            }
            catch (Exception)
            {
                //unknown: read it, then
            }
        }

        /// <summary>Appends one entry, trimming the oldest when the queue is over its cap.</summary>
        internal static void Append(string path, string line)
        {
            lock (FileLock)
            {
                if (!File.Exists(path))
                    File.WriteAllLines(path, new[] { Header });
                File.AppendAllLines(path, new[] { line });

                string[] all = File.ReadAllLines(path);
                if (all.Length - 1 > OfflineQueueSync.MaxQueue)
                {
                    IReadOnlyList<string> capped = OfflineQueueSync.Cap(all.Skip(1).ToList(), OfflineQueueSync.MaxQueue);
                    LogWriter.LogWrite("Offline queue: over " + OfflineQueueSync.MaxQueue + " entries, dropped the oldest " + (all.Length - 1 - capped.Count));
                    File.WriteAllLines(path, new[] { Header }.Concat(capped));
                }
            }
        }

        /// <summary>Rewrites the file as the kept entries plus whatever was appended after the snapshot was read.</summary>
        private static void WriteBack(string path, int snapshotLength, IReadOnlyList<string> kept)
        {
            lock (FileLock)
            {
                string[] now = File.Exists(path) ? File.ReadAllLines(path) : new string[0];
                var appended = now.Skip(Math.Max(snapshotLength, 1)).Where(l => !string.IsNullOrWhiteSpace(l));
                IReadOnlyList<string> queue = OfflineQueueSync.Cap(kept.Concat(appended).ToList(), OfflineQueueSync.MaxQueue);
                File.WriteAllLines(path, new[] { Header }.Concat(queue));
            }
        }

        internal static bool TryParse(string line, out OfflineEntry entry)
        {
            entry = default(OfflineEntry);
            string[] data = (line ?? "").Split(',');
            if (data.Length < 10 || string.IsNullOrEmpty(data[0]))
                return false;
            entry = new OfflineEntry(data[0], data[1], data[2], data[3], data[4], data[5], data[6], data[7], data[8], data[9]);
            return true;
        }
    }
}
