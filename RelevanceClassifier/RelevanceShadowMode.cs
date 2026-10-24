using Focus_Bear.Utils;
using FocusBear.Core.Diagnostics;
using FocusBear.Core.Relevance;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static Focus_Bear.RelevanceClassifier.NativeRelevanceClassifier;

namespace Focus_Bear.RelevanceClassifier
{
    /// <summary>
    /// Owns the opt-in shadow classifier: verifies the pinned model on a background task, loads it, and runs
    /// predictions off every blocking path. Nothing here can delay or change a blocking decision: until the files
    /// are in place and verified it simply has no classifier, and every prediction is fire-and-forget.
    /// </summary>
    internal static class RelevanceShadowMode
    {
        private const string ModelFolder = "RelevanceModel";
        private const string LocalDataFolder = "FocusBear";
        private const int CopyBufferBytes = 81920;
        private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(10);

        // GentleBlock allows a page when the backend's probability reaches this; mirrored here only to log disagreement.
        private const double BackendAllowProbability = 0.80;

        // Do not allocate a downloader when the research feature is disabled.
        private static readonly Lazy<HttpClient> Http = new Lazy<HttpClient>(
            () => new HttpClient { Timeout = DownloadTimeout });
        private static readonly object StartLock = new object();
        private static Task _start;
        private static readonly object PredictionLock = new object();
        private static readonly SemaphoreSlim PredictionSlot = new SemaphoreSlim(1, 1);
        private static string _lastPredictionKey;
        private static DateTime _lastPredictionUtc;
        private static Task<NativePredictionResult> _lastPrediction;
        private static volatile NativeRelevanceClassifier _classifier;

        internal static NativeRelevanceClassifier Classifier => DevFlags.NativeRelevance ? _classifier : null;

        /// <summary>Starts the download/verify/load once; later calls return the same task.</summary>
        internal static Task StartInBackground()
        {
            if (!DevFlags.NativeRelevance)
                return Task.CompletedTask;

            lock (StartLock)
            {
                if (_start == null) _start = Task.Run(() => LoadAsync());
                return _start;
            }
        }

        /// <summary>Predicts on a pool thread. The task never faults; its result is null when there's nothing to say.</summary>
        internal static Task<NativePredictionResult> PredictInBackground(RelevanceInput input)
        {
            if (!DevFlags.NativeRelevance)
                return Task.FromResult<NativePredictionResult>(null);

            NativeRelevanceClassifier classifier = _classifier;
            if (classifier == null || input == null)
                return Task.FromResult<NativePredictionResult>(null);

            return QueuePrediction(classifier, input);
        }

        /// <summary>
        /// Queues a snapshot of a browser tab. Strings are captured before scheduling;
        /// title/context preparation and inference cannot delay the backend decision.
        /// YouTube content can be left to ProcessBrowserURLs' stable-title queue.
        /// </summary>
        internal static Task<NativePredictionResult> QueueBrowserTabPrediction(
            string intention, string task, string url, string title, bool skipYouTubeContent = false)
        {
            if (!DevFlags.NativeRelevance)
                return Task.FromResult<NativePredictionResult>(null);

            NativeRelevanceClassifier classifier = _classifier;
            if (classifier == null)
                return Task.FromResult<NativePredictionResult>(null);

            return Task.Run(async () =>
            {
                try
                {
                    if (skipYouTubeContent && BrowserTitle.IsYouTubeContentPage(RelevanceInput.ParseWebUrl(url)))
                        return null;

                    RelevanceInput input = RelevanceInput.ForBrowserTab(intention, task, url, title);
                    return input == null ? null : await QueuePrediction(classifier, input).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Do not log page data carried by exception messages.
                    WriteSummary("NativeRelevanceModel SHADOW setup failed - " + ex.GetType().Name);
                    return null;
                }
            });
        }

        // Both entry points share one bounded inference queue. A busy classifier drops
        // an optional sample instead of accumulating work behind the blocking flow.
        private static Task<NativePredictionResult> QueuePrediction(
            NativeRelevanceClassifier classifier, RelevanceInput input)
        {
            string key = input.Goal + "\n" + input.Context + "\n" + input.Url + "\n" + input.Title;
            lock (PredictionLock)
            {
                if (key == _lastPredictionKey && _lastPrediction != null &&
                    DateTime.UtcNow - _lastPredictionUtc < TimeSpan.FromSeconds(30))
                    return _lastPrediction;
                if (!PredictionSlot.Wait(0))
                    return Task.FromResult<NativePredictionResult>(null);

                _lastPredictionKey = key;
                _lastPredictionUtc = DateTime.UtcNow;
                try
                {
                    _lastPrediction = Task.Run(() =>
                    {
                        try { return Predict(classifier, input); }
                        finally { PredictionSlot.Release(); }
                    });
                    return _lastPrediction;
                }
                catch
                {
                    PredictionSlot.Release();
                    _lastPredictionKey = null;
                    _lastPrediction = null;
                    return Task.FromResult<NativePredictionResult>(null);
                }
            }
        }

        /// <summary>When the shadow prediction finishes, logs whether it disagrees with the backend's verdict.</summary>
        internal static void LogDisagreementWhenDone(Task<NativePredictionResult> shadow, double backendProbability)
        {
            if (!DevFlags.NativeRelevance || shadow == null)
                return;

            shadow.ContinueWith(done =>
            {
                if (done.IsCanceled) return;
                if (done.IsFaulted)
                {
                    // Observe failures without exposing page data or affecting the backend.
                    _ = done.Exception;
                    return;
                }
                NativePredictionResult result = done.Result;
                if (result == null || double.IsNaN(backendProbability) ||
                    double.IsInfinity(backendProbability) || backendProbability < 0 || backendProbability > 1) return;
                bool backendAllows = backendProbability >= BackendAllowProbability;
                if (backendAllows == (result.Tier == RelevanceTier.Allow)) return;
                WriteSummary("RelevanceModel DISAGREEMENT | ShadowAction=" + result.Action +
                    " | ShadowConfidence=" + result.Confidence.ToString("0.0000", CultureInfo.InvariantCulture) +
                    " | ShadowLLMFallback=" + result.LlmFallbackRequired +
                    " | BackendProbability=" + backendProbability.ToString("0.00", CultureInfo.InvariantCulture));
            }, System.Threading.Tasks.TaskScheduler.Default);
        }

        private static NativePredictionResult Predict(NativeRelevanceClassifier classifier, RelevanceInput input)
        {
            try
            {
                NativePredictionResult result = classifier.Predict(input.Goal, input.Context, input.Url, input.Title);
                // Release logs carry no goal, URL or title: only what the model said and how long it took.
                WriteSummary("NativeRelevanceModel SHADOW | Action=" + result.Action +
                    " | Confidence=" + result.Confidence.ToString("0.0000", CultureInfo.InvariantCulture) +
                    " | EmailGoalRule=" + result.AllowedByEmailGoalRule +
                    " | LatencyMs=" + result.LatencyMs.ToString("0.00", CultureInfo.InvariantCulture));
                RelevanceShadowLog.Record(input, result);
                return result;
            }
            catch (Exception ex)
            {
                WriteSummary("NativeRelevanceModel SHADOW failed - " + ex.GetType().Name);
                return null;
            }
        }

        private static async Task LoadAsync()
        {
            if (!DevFlags.NativeRelevance)
                return;

            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                var installer = new RelevanceModelInstaller(ModelDirectory(), RelevanceModelFiles.All, DownloadAsync);
                bool installed = installer.IsInstalled();
                if (!installed && !RelevanceModelFiles.DownloadsEnabled)
                {
                    WriteSummary("NativeRelevanceModel unavailable: install the pinned shared model files locally; shadow mode stays off.");
                    return;
                }
                if (!installed && !await installer.EnsureInstalledAsync(CancellationToken.None).ConfigureAwait(false))
                {
                    WriteSummary("NativeRelevanceModel unavailable: download or checksum failed, shadow mode stays off.");
                    return;
                }
                var classifier = new NativeRelevanceClassifier();
                classifier.LoadModels(installer.Directory);
                _classifier = classifier;
                WriteSummary("NativeRelevanceModel loaded | Mode=SHADOW | Version=" + RelevanceModelFiles.Version +
                    " | LoadMs=" + timer.Elapsed.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture) +
                    " | WarmUpMs=" + classifier.WarmUpMs.ToString("0", CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                WriteSummary("NativeRelevanceModel failed to load, shadow mode stays off - " + ex.GetType().Name);
            }
        }

        private static async Task DownloadAsync(Uri url, Stream destination, CancellationToken cancellationToken)
        {
            using (HttpResponseMessage response = await Http.Value.GetAsync(url, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                using (Stream body = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    await body.CopyToAsync(destination, CopyBufferBytes, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private static void WriteSummary(string message)
        {
            try
            {
                LogWriter.LogWrite(message);
            }
            catch
            {
                // Shadow diagnostics must never change the app's blocking behaviour.
            }
        }

        private static string ModelDirectory() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            LocalDataFolder, ModelFolder, RelevanceModelFiles.Version);
    }

    /// <summary>
    /// The per-page shadow log (goal, URL, title, probabilities) for developers comparing the model with the backend.
    /// Detailed records are Debug-only and capped: one 2 MB file plus one rolled copy.
    /// Release builds use only the summary log above.
    /// </summary>
    internal static class RelevanceShadowLog
    {
#if DEBUG
        private const string FileName = "native-relevance-shadow.log";
        private const long MaxBytes = 2L * 1024 * 1024;
        private const int RolledFilesKept = 1;
        private static readonly Lazy<RollingLogWriter> Writer = new Lazy<RollingLogWriter>(() =>
            new RollingLogWriter(() => Path.Combine(AppSettings.AppDataDirLogsPath, FileName),
                MaxBytes, RolledFilesKept));
#endif

        [Conditional("DEBUG")]
        internal static void Record(RelevanceInput input, NativePredictionResult result)
        {
#if DEBUG
            if (!DevFlags.FeatureDiagnostics)
                return;

            var record = new JObject
            {
                ["TimestampUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                ["ModelVersion"] = RelevanceModelFiles.Version,
                ["Goal"] = input.Goal,
                ["Context"] = input.Context,
                ["Url"] = input.Url,
                ["Title"] = input.Title,
                ["Action"] = result.Action,
                ["Confidence"] = result.Confidence,
                ["Probabilities"] = new JArray(result.Probabilities),
                ["CosineSimilarity"] = result.CosineSimilarity,
                ["EmailGoalRule"] = result.AllowedByEmailGoalRule,
                ["LatencyMs"] = result.LatencyMs,
            };
            Writer.Value.Write(record.ToString(Newtonsoft.Json.Formatting.None));
#endif
        }
    }
}
