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
    /// Owns the shadow classifier: downloads and verifies the shared model on a background task, loads it, and runs
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

        private static readonly HttpClient Http = new HttpClient { Timeout = DownloadTimeout };
        private static readonly object StartLock = new object();
        private static Task _start;
        private static volatile NativeRelevanceClassifier _classifier;

        internal static NativeRelevanceClassifier Classifier => _classifier;

        /// <summary>Starts the download/verify/load once; later calls return the same task.</summary>
        internal static Task StartInBackground()
        {
            lock (StartLock)
            {
                if (_start == null) _start = Task.Run(() => LoadAsync());
                return _start;
            }
        }

        /// <summary>Predicts on a pool thread. The task never faults; its result is null when there's nothing to say.</summary>
        internal static Task<NativePredictionResult> PredictInBackground(RelevanceInput input)
        {
            NativeRelevanceClassifier classifier = _classifier;
            if (classifier == null || input == null) return Task.FromResult<NativePredictionResult>(null);
            return Task.Run(() => Predict(classifier, input));
        }

        /// <summary>When the shadow prediction finishes, logs whether it disagrees with the backend's verdict.</summary>
        internal static void LogDisagreementWhenDone(Task<NativePredictionResult> shadow, double backendProbability)
        {
            shadow.ContinueWith(done =>
            {
                NativePredictionResult result = done.Result;
                if (result == null || backendProbability < 0) return;
                bool backendAllows = backendProbability >= BackendAllowProbability;
                if (backendAllows == (result.Tier == RelevanceTier.Allow)) return;
                LogWriter.LogWrite("RelevanceModel DISAGREEMENT | ShadowAction=" + result.Action +
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
                LogWriter.LogWrite("NativeRelevanceModel SHADOW | Action=" + result.Action +
                    " | Confidence=" + result.Confidence.ToString("0.0000", CultureInfo.InvariantCulture) +
                    " | EmailGoalRule=" + result.AllowedByEmailGoalRule +
                    " | LatencyMs=" + result.LatencyMs.ToString("0.00", CultureInfo.InvariantCulture));
                RelevanceShadowLog.Record(input, result);
                return result;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("NativeRelevanceModel SHADOW failed - " + ex.GetType().Name + ": " + ex.Message);
                return null;
            }
        }

        private static async Task LoadAsync()
        {
            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                var installer = new RelevanceModelInstaller(ModelDirectory(), RelevanceModelFiles.All, DownloadAsync);
                if (!await installer.EnsureInstalledAsync(CancellationToken.None).ConfigureAwait(false))
                {
                    LogWriter.LogWrite("NativeRelevanceModel unavailable: download or checksum failed, shadow mode stays off.");
                    return;
                }
                var classifier = new NativeRelevanceClassifier();
                classifier.LoadModels(installer.Directory);
                _classifier = classifier;
                LogWriter.LogWrite("NativeRelevanceModel loaded | Mode=SHADOW | Version=" + RelevanceModelFiles.Version +
                    " | LoadMs=" + timer.Elapsed.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture) +
                    " | WarmUpMs=" + classifier.WarmUpMs.ToString("0", CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("NativeRelevanceModel failed to load, shadow mode stays off - " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static async Task DownloadAsync(Uri url, Stream destination, CancellationToken cancellationToken)
        {
            using (HttpResponseMessage response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                using (Stream body = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    await body.CopyToAsync(destination, CopyBufferBytes, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private static string ModelDirectory() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            LocalDataFolder, ModelFolder, RelevanceModelFiles.Version);
    }

    /// <summary>
    /// The per-page shadow log (goal, URL, title, probabilities) for developers comparing the model with the backend.
    /// Debug builds only, and capped: one 2 MB file plus one rolled copy. Release builds write nothing.
    /// </summary>
    internal static class RelevanceShadowLog
    {
#if DEBUG
        private const string FileName = "native-relevance-shadow.log";
        private const long MaxBytes = 2L * 1024 * 1024;
        private const int RolledFilesKept = 1;
        private static readonly RollingLogWriter Writer = new RollingLogWriter(
            () => Path.Combine(AppSettings.AppDataDirLogsPath, FileName), MaxBytes, RolledFilesKept);
#endif

        [Conditional("DEBUG")]
        internal static void Record(RelevanceInput input, NativePredictionResult result)
        {
#if DEBUG
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
            Writer.Write(record.ToString(Newtonsoft.Json.Formatting.None));
#endif
        }
    }
}
