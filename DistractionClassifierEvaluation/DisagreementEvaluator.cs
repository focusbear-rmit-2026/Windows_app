using Focus_Bear.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Focus_Bear
{
    // Explicit opt-in evaluation only. Does not call the backend or enforce blocking.
    internal static class DisagreementEvaluator
    {
        private static int started;

        internal static async Task RunFiveExampleTestAsync(string focusModeName, string languageCode)
        {
            if (!DevFlags.DisagreementEval || Interlocked.Exchange(ref started, 1) != 0)
                return;
            var app = Application.Current as App;
            if (app == null || app.NativeRelevanceClassifier == null)
            {
                LogWriter.LogWrite("DisagreementEvaluator - native classifier not loaded.");
                return;
            }
            string input = Environment.GetEnvironmentVariable("FOCUSBEAR_EVALUATION_INPUT");
            if (string.IsNullOrWhiteSpace(input))
            {
                // Preserve the existing five-example diagnostic when no dataset is selected.
                string[][] examples = {
                    new[] { "Learn Python Programming", "Python For Beginners | Python.org", "https://www.python.org/about/gettingstarted/" },
                    new[] { "Learn Python Programming", "The Python Tutorial — Python documentation", "https://docs.python.org/3/tutorial/" },
                    new[] { "Learn Python Programming", "Instagram", "https://www.instagram.com/" },
                    new[] { "Learn Python Programming", "Netflix", "https://www.netflix.com/" },
                    new[] { "study on typescript", "TypeScript: Handbook", "https://www.typescriptlang.org/docs/handbook/intro.html" }
                };
                await Task.Run(() => {
                    foreach (var x in examples)
                    {
                        var r = app.NativeRelevanceClassifier.Predict(x[0], x[1], x[2], x[1]);
                        LogWriter.LogWrite("DisagreementEvaluator - example | URL=" + x[2] + " | Action=" + r.Action);
                    }
                });
                return;
            }
            try
            {
                if (Debugger.IsAttached)
                    throw new InvalidOperationException("Exit the debugger and launch the Release executable directly.");
#if DEBUG
                throw new InvalidOperationException("Dataset evaluation requires a Release build.");
#endif
                await Task.Run(() => {
                    var data = JsonConvert.DeserializeObject<EvaluationInput>(File.ReadAllText(input));
                    if (data == null || data.Rows == null || data.Rows.Count == 0)
                        throw new InvalidDataException("No evaluation rows supplied.");
                    if (data.Rows.Select(x => x.Id).Distinct().Count() != data.Rows.Count)
                        throw new InvalidDataException("Duplicate row IDs.");
                    foreach (var row in data.Rows)
                    {
                        Uri uri;
                        string validationUrl = (row.Url ?? string.Empty).Trim();
                        if (validationUrl.Length > 0 && !validationUrl.Contains("://"))
                            validationUrl = "https://" + validationUrl;
                        if (string.IsNullOrWhiteSpace(row.Url) || !Uri.TryCreate(validationUrl, UriKind.Absolute, out uri) ||
                            (uri.Scheme != "https" && uri.Scheme != "http"))
                            throw new InvalidDataException("Invalid evaluation URL at row: " + row.Id);
                        if (row.ExpectedTier < 0 || row.ExpectedTier > 2 || row.Goal == null || row.Context == null ||
                            row.Title == null)
                            throw new InvalidDataException("Invalid evaluation row: " + row.Id);
                    }
                    string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "FocusBear", "Evaluation", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture));
                    Directory.CreateDirectory(root);
                    var hashes = new Dictionary<string, string>();
                    foreach (string name in new[] { "logistic_regression.onnx", "xgboost_1.onnx", "xgboost_2.onnx",
                        "minilm_l6_v2.onnx", "tfidf_goal.json", "tfidf_context.json", "vocab.txt" })
                        hashes[name] = Hash(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RelevanceClassifier", name));
                    var manifest = new {
                        StartedUtc = DateTime.UtcNow, InputSha256 = Hash(input), Models = hashes,
                        Process64Bit = Environment.Is64BitProcess, DebuggerAttached = Debugger.IsAttached,
                        OS = Environment.OSVersion.ToString(), ProcessorCount = Environment.ProcessorCount,
                        Executable = Process.GetCurrentProcess().MainModule.FileName,
                        ExecutableSha256 = Hash(Process.GetCurrentProcess().MainModule.FileName),
                        IntraOpNumThreads = app.NativeRelevanceClassifier.EffectiveOnnxThreadCount,
                        InterOpNumThreads = 1, ExecutionMode = "Sequential",
                        TrackingContextExperimentEnabled = app.NativeRelevanceClassifier.TrackingContextExperimentEnabled,
                        Protocol = "3 sequential passes, deterministic shuffle seed 42; existing startup warmup; logging enabled. Pass 1 includes evaluation first-call costs. No cache reset between passes.",
                        Scope = "Offline classifier input replay; excludes browser capture and title-cleaning paths. No backend calls.",
                        Limitations = data.Limitations, DatasetRows = data.Rows.Count
                    };
                    File.WriteAllText(Path.Combine(root, "manifest.json"), JsonConvert.SerializeObject(manifest, Formatting.Indented));
                    var results = new List<EvaluationResult>();
                    using (var writer = new StreamWriter(Path.Combine(root, "predictions.jsonl")))
                    {
                        var random = new Random(42);
                        for (int pass = 1; pass <= 3; pass++)
                        {
                            var order = data.Rows.ToList();
                            for (int i = order.Count - 1; i > 0; i--)
                            { int j = random.Next(i + 1); var tmp = order[i]; order[i] = order[j]; order[j] = tmp; }
                            int done = 0;
                            foreach (var row in order)
                            {
                                var item = new EvaluationResult { Id = row.Id, Pass = pass, ExpectedTier = row.ExpectedTier,
                                    TrainingInputOverlap = row.TrainingInputOverlap };
                                var timer = Stopwatch.StartNew();
                                try
                                {
                                    var r = app.NativeRelevanceClassifier.Predict(row.Goal, row.Context, row.Url, row.Title);
                                    timer.Stop();
                                    if (r.Tier < 0 || r.Tier > 2 || double.IsNaN(r.LatencyMs) || double.IsInfinity(r.LatencyMs))
                                        throw new InvalidDataException("Invalid prediction returned.");
                                    item.PredictedTier = r.Tier; item.Action = r.Action;
                                    item.Confidence = r.Confidence; item.Probabilities = r.Probabilities;
                                    item.PredictionMs = r.LatencyMs;
                                }
                                catch (Exception ex) { timer.Stop(); item.Error = ex.ToString(); }
                                item.CallWallMs = timer.Elapsed.TotalMilliseconds;
                                results.Add(item); writer.WriteLine(JsonConvert.SerializeObject(item));
                                if (++done % 100 == 0)
                                { writer.Flush(); LogWriter.LogWrite("Native evaluation pass " + pass + ": " + done + "/" + order.Count); }
                            }
                        }
                    }
                    var summaries = new List<object>();
                    for (int pass = 1; pass <= 3; pass++)
                    {
                        summaries.Add(Summarize(results.Where(x => x.Pass == pass).ToList(), pass, "All recovered records"));
                        summaries.Add(Summarize(results.Where(x => x.Pass == pass && !x.TrainingInputOverlap).ToList(),
                            pass, "Without exact reconstructed training-input overlap; NOT proven independent"));
                    }
                    File.WriteAllText(Path.Combine(root, "summary.json"), JsonConvert.SerializeObject(summaries, Formatting.Indented));
                    LogWriter.LogWrite("Native evaluation COMPLETE | Results=" + root);
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => MessageBox.Show(
                        "Native evaluation completed. Results saved to:\n" + root,
                        "Focus Bear evaluation", MessageBoxButton.OK, MessageBoxImage.Information)));
                });
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Native evaluation FAILED: " + ex);
                MessageBox.Show(ex.Message, "Evaluation failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static object Summarize(List<EvaluationResult> rows, int pass, string subset)
        {
            var valid = rows.Where(x => x.Error == null && x.PredictedTier.HasValue).ToList();
            var matrix = new int[3, 3];
            foreach (var r in valid) matrix[r.ExpectedTier, r.PredictedTier.Value]++;
            var metrics = new List<object>(); double macroF1 = 0; int correct = 0;
            for (int k = 0; k < 3; k++)
            {
                int tp = matrix[k, k], support = 0, predicted = 0;
                for (int j = 0; j < 3; j++) { support += matrix[k, j]; predicted += matrix[j, k]; }
                double precision = predicted == 0 ? 0 : (double)tp / predicted;
                double recall = support == 0 ? 0 : (double)tp / support;
                double f1 = precision + recall == 0 ? 0 : 2 * precision * recall / (precision + recall);
                correct += tp; macroF1 += f1 / 3;
                metrics.Add(new { Tier = k, SupportSuccessful = support, Precision = precision, Recall = recall, F1 = f1 });
            }
            return new { Pass = pass, Subset = subset, Attempted = rows.Count, Successful = valid.Count,
                Errors = rows.Count - valid.Count,
                AccuracySuccessful = valid.Count == 0 ? (double?)null : (double)correct / valid.Count,
                CorrectFractionAllAttempts = rows.Count == 0 ? (double?)null : (double)correct / rows.Count,
                MacroF1 = macroF1, PerClass = metrics,
                ConfusionMatrixRowsExpectedColumnsPredicted = matrix,
                PredictionLatency = Latency(valid.Select(x => x.PredictionMs.Value).ToList()),
                CallWallLatencyIncludingLogging = Latency(valid.Select(x => x.CallWallMs).ToList()) };
        }
        private static object Latency(List<double> values)
        {
            values.Sort();
            if (values.Count == 0) return null;
            int over = values.Count(x => x > 50);
            return new { Count = values.Count, MedianMs = values.Count % 2 == 0 ?
                (values[values.Count / 2 - 1] + values[values.Count / 2]) / 2 : values[values.Count / 2],
                P95NearestRankMs = values[(int)Math.Ceiling(values.Count * .95) - 1],
                MaxMs = values[values.Count - 1], Over50Ms = over, Over50Percent = 100.0 * over / values.Count };
        }
        private static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        private sealed class EvaluationInput { public List<EvaluationRow> Rows { get; set; } public string[] Limitations { get; set; } }
        private sealed class EvaluationRow
        {
            public int Id { get; set; } public string Goal { get; set; } public string Context { get; set; }
            public string Url { get; set; } public string Title { get; set; } public int ExpectedTier { get; set; }
            public bool TrainingInputOverlap { get; set; }
        }
        private sealed class EvaluationResult
        {
            public int Id { get; set; } public int Pass { get; set; } public int ExpectedTier { get; set; }
            public bool TrainingInputOverlap { get; set; } public int? PredictedTier { get; set; }
            public string Action { get; set; } public float Confidence { get; set; } public float[] Probabilities { get; set; }
            public double? PredictionMs { get; set; } public double CallWallMs { get; set; } public string Error { get; set; }
        }
    }
}
