using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Focus_Bear.RelevanceClassifier
{
    internal class NativeRelevanceClassifier
    {
        private InferenceSession _logisticRegressionSession;
        private InferenceSession _xgboost1Session;
        private InferenceSession _xgboost2Session;

        private InferenceSession _miniLmSession;

        private string _contextEmbeddingInput = string.Empty;
        private bool _trackingContextCleaned;
        private int _lastMiniLmSequenceLength;
        private int _contextMiniLmSequenceLength;


        private JObject _goalTfidfData;
        private JObject _contextTfidfData;

        private NativeTfidfVectorizer _goalVectorizer;
        private NativeTfidfVectorizer _contextVectorizer;

        private NativeMiniLmTokenizer _miniLmTokenizer;

        // Serialize predictions because the goal embedding cache is shared.
        private readonly object _predictionLock = new object();

        // Captured under _predictionLock for comparison on identical features.
        private float[][] _individualProbabilities;
        private double[] _individualLatencyMs;
        private Dictionary<string, double> _timings;
        private JObject _featureDetails;
        private string[] _goalFeatureNames;
        private string[] _contextFeatureNames;
        private long _predictionSequence;
        private double _predictionLockWaitMs;
        private bool _goalCacheHit;
        private JObject _videoFeatureProbe;
        private readonly int _onnxThreadCount = ReadOnnxThreadCount();
        private readonly bool _enableVideoFeatureProbe = string.Equals(
            Environment.GetEnvironmentVariable("FOCUSBEAR_VIDEO_FEATURE_PROBE"),
            "1", StringComparison.Ordinal);

        // Feature dumps are opt-in; ordinary predictions do no diagnostic disk IO.
        internal bool EnableFeatureDiagnostics { get; set; } = string.Equals(
            Environment.GetEnvironmentVariable("FOCUSBEAR_FEATURE_DIAGNOSTICS"),
            "1", StringComparison.Ordinal);

        private double _startupWarmupMs;
        private bool _startupWarmupCompleted;

        private string _cachedFocusIntention;
        private float[] _cachedFocusEmbedding;

        internal int EffectiveOnnxThreadCount => _onnxThreadCount;
        internal bool TrackingContextExperimentEnabled => string.Equals(
            Environment.GetEnvironmentVariable("FOCUSBEAR_TRACKING_CONTEXT_EXPERIMENT"),
            "1", StringComparison.Ordinal);

        private static int ReadOnnxThreadCount()
        {
            int count;
            return int.TryParse(Environment.GetEnvironmentVariable("FOCUSBEAR_ONNX_THREADS"),
                out count) && count >= 0 && count <= 32 ? count : 1;
        }

        private InferenceSession CreateOnnxSession(string path)
        {
            // Zero restores ORT's automatic thread selection for comparison.
            using (var options = new SessionOptions())
            {
                options.IntraOpNumThreads = _onnxThreadCount;
                options.InterOpNumThreads = 1;
                options.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
                return new InferenceSession(path, options);
            }
        }

        public bool LoadModels()
        {
            try
            {
                string modelDirectory = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "RelevanceClassifier"
                );

                string logisticPath = Path.Combine(
                    modelDirectory,
                    "logistic_regression.onnx"
                );

                string xgboost1Path = Path.Combine(
                    modelDirectory,
                    "xgboost_1.onnx"
                );

                string xgboost2Path = Path.Combine(
                    modelDirectory,
                    "xgboost_2.onnx"
                );

                string miniLmPath = Path.Combine(
                    modelDirectory,
                    "minilm_l6_v2.onnx"
                );

                string vocabPath = Path.Combine(
                    modelDirectory,
                    "vocab.txt"
                );

                string goalTfidfPath = Path.Combine(
                    modelDirectory,
                    "tfidf_goal.json"
                );

                string contextTfidfPath = Path.Combine(
                    modelDirectory,
                    "tfidf_context.json"
                );

                // ---------------------------------------------------------
                // Verify required files.
                // ---------------------------------------------------------

                if (!File.Exists(logisticPath))
                    throw new FileNotFoundException(
                        "logistic_regression.onnx not found.",
                        logisticPath
                    );

                if (!File.Exists(xgboost1Path))
                    throw new FileNotFoundException(
                        "xgboost_1.onnx not found.",
                        xgboost1Path
                    );

                if (!File.Exists(xgboost2Path))
                    throw new FileNotFoundException(
                        "xgboost_2.onnx not found.",
                        xgboost2Path
                    );

                if (!File.Exists(miniLmPath))
                    throw new FileNotFoundException(
                        "minilm_l6_v2.onnx not found.",
                        miniLmPath
                    );

                if (!File.Exists(vocabPath))
                    throw new FileNotFoundException(
                        "vocab.txt not found.",
                        vocabPath
                    );

                if (!File.Exists(goalTfidfPath))
                    throw new FileNotFoundException(
                        "tfidf_goal.json not found.",
                        goalTfidfPath
                    );

                if (!File.Exists(contextTfidfPath))
                    throw new FileNotFoundException(
                        "tfidf_context.json not found.",
                        contextTfidfPath
                    );

                // ---------------------------------------------------------
                // Load classifier ONNX models.
                // ---------------------------------------------------------

                _logisticRegressionSession =
                    CreateOnnxSession(logisticPath);

                _xgboost1Session =
                    CreateOnnxSession(xgboost1Path);

                _xgboost2Session =
                    CreateOnnxSession(xgboost2Path);

                // ---------------------------------------------------------
                // STEP 35A:
                // Inspect classifier ONNX input/output metadata.
                // ---------------------------------------------------------

                Debug.WriteLine(
                    "[NativeRelevanceClassifier] ===== CLASSIFIER ONNX METADATA ====="
                );

                // Logistic Regression
                Debug.WriteLine(
                    "[NativeRelevanceClassifier] --- Logistic Regression ---"
                );

                foreach (var input in _logisticRegressionSession.InputMetadata)
                {
                    Debug.WriteLine(
                        "[NativeRelevanceClassifier] LR INPUT: " +
                        input.Key +
                        " | Type: " +
                        input.Value.ElementType +
                        " | Dimensions: [" +
                        string.Join(",", input.Value.Dimensions) +
                        "]"
                    );
                }

                foreach (var output in _logisticRegressionSession.OutputMetadata)
                {
                    Debug.WriteLine(
                        "[NativeRelevanceClassifier] LR OUTPUT: " +
                        output.Key +
                        " | Type: " +
                        output.Value.ElementType +
                        " | Dimensions: [" +
                        string.Join(",", output.Value.Dimensions) +
                        "]"
                    );
                }

                // XGBoost 1
                Debug.WriteLine(
                    "[NativeRelevanceClassifier] --- XGBoost 1 ---"
                );

                foreach (var input in _xgboost1Session.InputMetadata)
                {
                    Debug.WriteLine(
                        "[NativeRelevanceClassifier] XGB1 INPUT: " +
                        input.Key +
                        " | Type: " +
                        input.Value.ElementType +
                        " | Dimensions: [" +
                        string.Join(",", input.Value.Dimensions) +
                        "]"
                    );
                }

                foreach (var output in _xgboost1Session.OutputMetadata)
                {
                    Debug.WriteLine(
                        "[NativeRelevanceClassifier] XGB1 OUTPUT: " +
                        output.Key +
                        " | Type: " +
                        output.Value.ElementType +
                        " | Dimensions: [" +
                        string.Join(",", output.Value.Dimensions) +
                        "]"
                    );
                }

                // XGBoost 2
                Debug.WriteLine(
                    "[NativeRelevanceClassifier] --- XGBoost 2 ---"
                );

                foreach (var input in _xgboost2Session.InputMetadata)
                {
                    Debug.WriteLine(
                        "[NativeRelevanceClassifier] XGB2 INPUT: " +
                        input.Key +
                        " | Type: " +
                        input.Value.ElementType +
                        " | Dimensions: [" +
                        string.Join(",", input.Value.Dimensions) +
                        "]"
                    );
                }

                foreach (var output in _xgboost2Session.OutputMetadata)
                {
                    Debug.WriteLine(
                        "[NativeRelevanceClassifier] XGB2 OUTPUT: " +
                        output.Key +
                        " | Type: " +
                        output.Value.ElementType +
                        " | Dimensions: [" +
                        string.Join(",", output.Value.Dimensions) +
                        "]"
                    );
                }

                Debug.WriteLine(
                    "[NativeRelevanceClassifier] ===== END CLASSIFIER ONNX METADATA ====="
                );
                // ---------------------------------------------------------
                // Load MiniLM ONNX model.
                // ---------------------------------------------------------

                _miniLmSession =
                    CreateOnnxSession(miniLmPath);

                Debug.WriteLine(
                    "[NativeRelevanceClassifier] SUCCESS: MiniLM ONNX model loaded."
                );

                // ---------------------------------------------------------
                // Load native MiniLM tokenizer.
                // ---------------------------------------------------------

                _miniLmTokenizer =
                    new NativeMiniLmTokenizer(vocabPath);

                Debug.WriteLine(
                    "[NativeRelevanceClassifier] SUCCESS: MiniLM tokenizer loaded."
                );

                Debug.WriteLine(
                    "[NativeRelevanceClassifier] MiniLM vocabulary size: " +
                    _miniLmTokenizer.VocabularySize
                );

                // ---------------------------------------------------------
                // Load TF-IDF configuration.
                // ---------------------------------------------------------

                _goalTfidfData =
                    JObject.Parse(
                        File.ReadAllText(goalTfidfPath)
                    );

                _contextTfidfData =
                    JObject.Parse(
                        File.ReadAllText(contextTfidfPath)
                    );

                _goalVectorizer =
                    new NativeTfidfVectorizer(
                        _goalTfidfData
                    );

                _contextVectorizer =
                    new NativeTfidfVectorizer(
                        _contextTfidfData
                    );

                _goalFeatureNames = ReadFeatureNames(_goalTfidfData, 533);
                _contextFeatureNames = ReadFeatureNames(_contextTfidfData, 5000);

                Debug.WriteLine(
                    "[NativeRelevanceClassifier] SUCCESS: TF-IDF JSON files loaded."
                );

                Debug.WriteLine(
                    "[NativeRelevanceClassifier] Goal TF-IDF features: " +
                    _goalVectorizer.FeatureCount
                );

                Debug.WriteLine(
                    "[NativeRelevanceClassifier] Context TF-IDF features: " +
                    _contextVectorizer.FeatureCount
                );


                Debug.WriteLine(
                    "[NativeRelevanceClassifier] SUCCESS: All native models loaded."
                );

                WarmUpModels();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "[NativeRelevanceClassifier] MODEL LOAD FAILED: " +
                    ex
                );

                return false;
            }
        }

        private void WarmUpModels()
        {
            // Run the feature and ONNX paths without producing a website decision.
            // This cost belongs to startup and is recorded separately.
            lock (_predictionLock)
            {
                var timer = Stopwatch.StartNew();
                try
                {
                    _timings = new Dictionary<string, double>();
                    const string goal = "Learn programming";
                    const string title = "Programming tutorial";
                    float[] features = BuildCompleteFeatureVector(
                        goal, title, "https://example.com/tutorial", title);
                    RunSoftVotingEnsemble(features);
                    _startupWarmupCompleted = true;
                }
                finally
                {
                    timer.Stop();
                    _startupWarmupMs = timer.Elapsed.TotalMilliseconds;
                    // Never reuse the artificial warm-up goal for a user prediction.
                    _cachedFocusIntention = null;
                    _cachedFocusEmbedding = null;
                    _individualProbabilities = null;
                    _individualLatencyMs = null;
                    _featureDetails = null;
                    _timings = null;
                }
                Trace.WriteLine("[NativeRelevanceClassifier] STARTUP WARMUP MS=" +
                    _startupWarmupMs.ToString("0.00", CultureInfo.InvariantCulture));
                try
                {
                    string directory = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "FocusBear");
                    Directory.CreateDirectory(directory);
                    var record = new JObject
                    {
                        ["TimestampUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                        ["Mode"] = "WARMUP",
                        ["WarmupCompleted"] = _startupWarmupCompleted,
                        ["WarmupLatencyMs"] = _startupWarmupMs
                    };
                    File.AppendAllText(Path.Combine(directory, "native-relevance-shadow.log"),
                        record.ToString(Newtonsoft.Json.Formatting.None) + Environment.NewLine);
                }
                catch (IOException ex)
                {
                    Trace.WriteLine("[NativeRelevanceClassifier] Warm-up log unavailable: " + ex.Message);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Trace.WriteLine("[NativeRelevanceClassifier] Warm-up log unavailable: " + ex.Message);
                }
            }
        }

        // =============================================================
        // STEP 41:
        // Get confidence for the predicted relevance tier.
        // Confidence = ensemble probability of the winning tier.
        // =============================================================

        private float GetPredictionConfidence(
            float[] probabilities,
            int predictedTier)
        {
            if (probabilities == null)
            {
                throw new ArgumentNullException(
                    nameof(probabilities)
                );
            }

            if (probabilities.Length != 3)
            {
                throw new ArgumentException(
                    "Expected exactly 3 class probabilities.",
                    nameof(probabilities)
                );
            }

            if (predictedTier < 0 || predictedTier >= probabilities.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(predictedTier)
                );
            }

            return probabilities[predictedTier];
        }

        // =============================================================
        // STEP 42:
        // Apply the classifier confidence gate.
        // The deployed classifier uses a 0.70 confidence threshold.
        // =============================================================

        private bool PassesConfidenceGate(float confidence)
        {
            const float ConfidenceThreshold = 0.70f;

            return confidence >= ConfidenceThreshold;
        }

        // =============================================================
        // Native MiniLM inference.
        // =============================================================

        private float[] GetFocusIntentionEmbedding(string focusIntention)
        {
            focusIntention = focusIntention ?? string.Empty;

            if (_cachedFocusEmbedding != null &&
                string.Equals(
                    _cachedFocusIntention,
                    focusIntention,
                    StringComparison.Ordinal))
            {
                Debug.WriteLine(
                    "[NativeRelevanceClassifier] Focus embedding CACHE HIT"
                );

                return _cachedFocusEmbedding;
            }

            Debug.WriteLine(
                "[NativeRelevanceClassifier] Focus embedding CACHE MISS"
            );

            _cachedFocusEmbedding =
                GenerateMiniLmEmbedding(focusIntention);

            _cachedFocusIntention = focusIntention;

            return _cachedFocusEmbedding;
        }
        // Opt-in shadow experiment. Keep TF-IDF, URL features and reference labels
        // unchanged; compare against the original replay before adopting this policy.
        private static string PrepareContextEmbeddingInput(
            string context, string url, out bool cleaned)
        {
            cleaned = false;
            context = context ?? string.Empty;
            if (!string.Equals(Environment.GetEnvironmentVariable(
                "FOCUSBEAR_TRACKING_CONTEXT_EXPERIMENT"), "1", StringComparison.Ordinal))
                return context;

            Uri parsed;
            string candidate = url ?? string.Empty;
            if (!Uri.TryCreate(candidate.Contains("://") ? candidate : "https://" + candidate,
                    UriKind.Absolute, out parsed) ||
                (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) ||
                !parsed.AbsolutePath.StartsWith("/events/public/v1/encoded/track/",
                    StringComparison.OrdinalIgnoreCase))
                return context;

            // Remove only long mixed letter/digit tracking tokens from this route.
            // Preserve readable context, including its title and ordinary path words.
            string result = Regex.Replace(context, @"\S{80,}", match =>
            {
                string token = match.Value;
                return token.Any(char.IsLetter) && token.Any(char.IsDigit)
                    ? string.Empty : token;
            });
            result = Regex.Replace(result, @"\s+", " ").Trim();
            if (result.Length == 0 || string.Equals(result, context, StringComparison.Ordinal))
                return context;
            cleaned = true;
            return result;
        }

        private float[] GenerateMiniLmEmbedding(string text)
        {
            if (_miniLmSession == null)
                throw new InvalidOperationException(
                    "MiniLM ONNX session has not been loaded."
                );

            if (_miniLmTokenizer == null)
                throw new InvalidOperationException(
                    "MiniLM tokenizer has not been loaded."
                );

            var inputPreparationTimer = Stopwatch.StartNew();
            NativeMiniLmTokenizer.TokenizedInput tokenized =
                _miniLmTokenizer.Encode(
                    text ?? string.Empty
                );

            int sequenceLength =
                tokenized.SequenceLength;
            _lastMiniLmSequenceLength = sequenceLength;

            if (sequenceLength <= 0)
                throw new InvalidOperationException(
                    "MiniLM tokenizer produced an empty sequence."
                );

            DenseTensor<long> inputIdsTensor =
                new DenseTensor<long>(
                    tokenized.InputIds,
                    new[] { 1, sequenceLength }
                );

            DenseTensor<long> attentionMaskTensor =
                new DenseTensor<long>(
                    tokenized.AttentionMask,
                    new[] { 1, sequenceLength }
                );

            DenseTensor<long> tokenTypeIdsTensor =
                new DenseTensor<long>(
                    tokenized.TokenTypeIds,
                    new[] { 1, sequenceLength }
                );

            List<NamedOnnxValue> inputs =
                new List<NamedOnnxValue>
                {
            NamedOnnxValue.CreateFromTensor(
                "input_ids",
                inputIdsTensor
            ),

            NamedOnnxValue.CreateFromTensor(
                "attention_mask",
                attentionMaskTensor
            ),

            NamedOnnxValue.CreateFromTensor(
                "token_type_ids",
                tokenTypeIdsTensor
            )
                };

            // =============================================================
            // Measure actual MiniLM ONNX inference latency.
            // =============================================================

            inputPreparationTimer.Stop();
            AddTiming("MiniLmInputPreparationMs", inputPreparationTimer.Elapsed.TotalMilliseconds);
            var miniLmInferenceStopwatch =
                System.Diagnostics.Stopwatch.StartNew();

            using (
                IDisposableReadOnlyCollection<DisposableNamedOnnxValue>
                    results =
                        _miniLmSession.Run(inputs)
            )
            {
                miniLmInferenceStopwatch.Stop();
                AddTiming("MiniLmOnnxRunMs", miniLmInferenceStopwatch.Elapsed.TotalMilliseconds);
                var poolingTimer = Stopwatch.StartNew();

                System.Diagnostics.Debug.WriteLine(
                    "[NativeRelevanceClassifier] MINILM ONNX RUN MS=" +
                    miniLmInferenceStopwatch.Elapsed.TotalMilliseconds.ToString("F2")
                );

                DisposableNamedOnnxValue hiddenStateOutput =
                    results.FirstOrDefault(
                        result =>
                            result.Name == "last_hidden_state"
                    );

                if (hiddenStateOutput == null)
                    throw new InvalidOperationException(
                        "MiniLM output 'last_hidden_state' was not returned."
                    );

                Tensor<float> hiddenState =
                    hiddenStateOutput.AsTensor<float>();

                int[] dimensions =
                    hiddenState.Dimensions.ToArray();

                if (dimensions.Length != 3)
                    throw new InvalidOperationException(
                        "Unexpected MiniLM output rank. Expected 3 dimensions."
                    );

                int batchSize =
                    dimensions[0];

                int outputSequenceLength =
                    dimensions[1];

                int embeddingSize =
                    dimensions[2];

                Debug.WriteLine(
                    "[NativeRelevanceClassifier] MiniLM output shape: [" +
                    batchSize +
                    "," +
                    outputSequenceLength +
                    "," +
                    embeddingSize +
                    "]"
                );

                if (batchSize != 1)
                    throw new InvalidOperationException(
                        "MiniLM inference expected batch size 1."
                    );

                if (outputSequenceLength != sequenceLength)
                    throw new InvalidOperationException(
                        "MiniLM output sequence length does not match tokenizer output."
                    );

                // ---------------------------------------------------------
                // SentenceTransformer mean pooling.
                // ---------------------------------------------------------

                float[] embedding =
                    new float[embeddingSize];

                float maskSum = 0.0f;

                for (
                    int tokenIndex = 0;
                    tokenIndex < outputSequenceLength;
                    tokenIndex++
                )
                {
                    float mask =
                        tokenized.AttentionMask[tokenIndex];

                    if (mask <= 0.0f)
                        continue;

                    maskSum += mask;

                    for (
                        int embeddingIndex = 0;
                        embeddingIndex < embeddingSize;
                        embeddingIndex++
                    )
                    {
                        embedding[embeddingIndex] +=
                            hiddenState[
                                0,
                                tokenIndex,
                                embeddingIndex
                            ] * mask;
                    }
                }

                if (maskSum <= 0.0f)
                    throw new InvalidOperationException(
                        "MiniLM attention mask contains no active tokens."
                    );

                // ---------------------------------------------------------
                // Finish mean pooling.
                // ---------------------------------------------------------

                for (
                    int embeddingIndex = 0;
                    embeddingIndex < embeddingSize;
                    embeddingIndex++
                )
                {
                    embedding[embeddingIndex] /=
                        maskSum;
                }

                // ---------------------------------------------------------
                // SentenceTransformer L2 normalization.
                //
                // Normalize the pooled 384-dimensional embedding to
                // unit length before it is returned.
                // ---------------------------------------------------------

                double normSquared = 0.0;

                for (
                    int embeddingIndex = 0;
                    embeddingIndex < embeddingSize;
                    embeddingIndex++
                )
                {
                    double value =
                        embedding[embeddingIndex];

                    normSquared +=
                        value * value;
                }

                double norm =
                    Math.Sqrt(normSquared);

                if (norm > 0.0)
                {
                    for (
                        int embeddingIndex = 0;
                        embeddingIndex < embeddingSize;
                        embeddingIndex++
                    )
                    {
                        embedding[embeddingIndex] =
                            (float)(
                                embedding[embeddingIndex] /
                                norm
                            );
                    }
                }

                poolingTimer.Stop();
                AddTiming("MiniLmOutputAndPoolingMs", poolingTimer.Elapsed.TotalMilliseconds);
                return embedding;
            }
        }

        // =============================================================
        // Real semantic cosine similarity.
        // =============================================================

        private float CalculateCosineSimilarity(
            float[] firstEmbedding,
            float[] secondEmbedding)
        {
            if (firstEmbedding == null)
                throw new ArgumentNullException(
                    nameof(firstEmbedding)
                );

            if (secondEmbedding == null)
                throw new ArgumentNullException(
                    nameof(secondEmbedding)
                );

            if (firstEmbedding.Length != secondEmbedding.Length)
                throw new InvalidOperationException(
                    "MiniLM embedding dimensions do not match."
                );

            if (firstEmbedding.Length == 0)
                return 0.0f;

            double dotProduct = 0.0;
            double firstMagnitude = 0.0;
            double secondMagnitude = 0.0;

            for (int i = 0; i < firstEmbedding.Length; i++)
            {
                double firstValue =
                    firstEmbedding[i];

                double secondValue =
                    secondEmbedding[i];

                dotProduct +=
                    firstValue * secondValue;

                firstMagnitude +=
                    firstValue * firstValue;

                secondMagnitude +=
                    secondValue * secondValue;
            }

            if (firstMagnitude <= 0.0 ||
                secondMagnitude <= 0.0)
            {
                return 0.0f;
            }

            double cosineSimilarity =
                dotProduct /
                (
                    Math.Sqrt(firstMagnitude) *
                    Math.Sqrt(secondMagnitude)
                );

            if (cosineSimilarity > 1.0)
                cosineSimilarity = 1.0;

            if (cosineSimilarity < -1.0)
                cosineSimilarity = -1.0;

            return (float)cosineSimilarity;
        }

        // =============================================================
        // STEP 34:
        // Build exact 5545-feature classifier vector.
        //
        // 0-532     = goal TF-IDF       (533)
        // 533-5532  = context TF-IDF    (5000)
        // 5533-5544 = engineered        (12)
        // =============================================================
        private float[] BuildCompleteFeatureVector(
             string focusIntention,
             string websiteContext,
             string websiteUrl,
             string tabTitle)
        {
            focusIntention =
                focusIntention ?? string.Empty;

            websiteContext =
                websiteContext ?? string.Empty;

            if (_goalVectorizer == null ||
                _contextVectorizer == null)
            {
                throw new InvalidOperationException(
                    "TF-IDF vectorizers have not been loaded."
                );
            }

            // ---------------------------------------------------------
            // Goal TF-IDF = 533
            // ---------------------------------------------------------

            var tfidfStopwatch = System.Diagnostics.Stopwatch.StartNew();

            float[] goalFeatures =
                _goalVectorizer.Transform(
                    focusIntention
                );

            if (goalFeatures.Length != 533)
            {
                throw new InvalidOperationException(
                    "Expected 533 goal TF-IDF features but received " +
                    goalFeatures.Length +
                    "."
                );
            }

            // ---------------------------------------------------------
            // Context TF-IDF = 5000
            // ---------------------------------------------------------

            float[] contextFeatures =
                _contextVectorizer.Transform(
                    websiteContext
                );

            if (contextFeatures.Length != 5000)
            {
                throw new InvalidOperationException(
                    "Expected 5000 context TF-IDF features but received " +
                    contextFeatures.Length +
                    "."
                );
            }

            tfidfStopwatch.Stop();
            AddTiming("TfidfMs", tfidfStopwatch.Elapsed.TotalMilliseconds);

            Debug.WriteLine(
                "[NativeRelevanceClassifier] LATENCY BREAKDOWN - TFIDF MS=" +
                tfidfStopwatch.Elapsed.TotalMilliseconds.ToString(
                    "0.00",
                    CultureInfo.InvariantCulture
                )
            );

            // ---------------------------------------------------------
            // Generate native MiniLM embeddings.
            // ---------------------------------------------------------

            var miniLmStopwatch = System.Diagnostics.Stopwatch.StartNew();

            _goalCacheHit = _cachedFocusEmbedding != null &&
                string.Equals(_cachedFocusIntention, focusIntention, StringComparison.Ordinal);
            var goalEmbeddingTimer = Stopwatch.StartNew();
            float[] goalEmbedding =
                GetFocusIntentionEmbedding(
                    focusIntention
                );

            goalEmbeddingTimer.Stop();
            AddTiming("GoalEmbeddingMs", goalEmbeddingTimer.Elapsed.TotalMilliseconds);
            var contextEmbeddingTimer = Stopwatch.StartNew();
            _contextEmbeddingInput = PrepareContextEmbeddingInput(
                websiteContext, websiteUrl, out _trackingContextCleaned);
            float[] contextEmbedding =
                GenerateMiniLmEmbedding(
                    _contextEmbeddingInput
                );
            _contextMiniLmSequenceLength = _lastMiniLmSequenceLength;

            contextEmbeddingTimer.Stop();
            AddTiming("ContextEmbeddingMs", contextEmbeddingTimer.Elapsed.TotalMilliseconds);
            miniLmStopwatch.Stop();
            AddTiming("MiniLmTotalMs", miniLmStopwatch.Elapsed.TotalMilliseconds);

            Debug.WriteLine(
                "[NativeRelevanceClassifier] LATENCY BREAKDOWN - MINILM MS=" +
                miniLmStopwatch.Elapsed.TotalMilliseconds.ToString(
                    "0.00",
                    CultureInfo.InvariantCulture
                )
            );

            if (goalEmbedding.Length != 384 ||
                contextEmbedding.Length != 384)
            {
                throw new InvalidOperationException(
                    "MiniLM embeddings must contain exactly 384 values."
                );
            }

            // ---------------------------------------------------------
            // Real semantic cosine.
            // ---------------------------------------------------------

            float cosineSimilarity =
                CalculateCosineSimilarity(
                    goalEmbedding,
                    contextEmbedding
                );

            // ---------------------------------------------------------
            // Engineered features = 12
            // ---------------------------------------------------------

            float[] simpleFeatures =
               BuildSimpleFeatures(
                   focusIntention,
                   websiteContext,
                   cosineSimilarity,
                   websiteUrl,
                   tabTitle
               );

            if (simpleFeatures.Length != 12)
            {
                throw new InvalidOperationException(
                    "Expected 12 engineered features but received " +
                    simpleFeatures.Length +
                    "."
                );
            }

            // ---------------------------------------------------------
            // Assemble exact classifier input.
            // ---------------------------------------------------------

            float[] completeFeatures =
                new float[5545];

            // 0-532
            Array.Copy(
                goalFeatures,
                0,
                completeFeatures,
                0,
                goalFeatures.Length
            );

            // 533-5532
            Array.Copy(
                contextFeatures,
                0,
                completeFeatures,
                533,
                contextFeatures.Length
            );

            // 5533-5544
            Array.Copy(
                simpleFeatures,
                0,
                completeFeatures,
                5533,
                simpleFeatures.Length
            );

            if (completeFeatures.Length != 5545)
            {
                throw new InvalidOperationException(
                    "Complete feature vector must contain exactly 5545 features."
                );
            }

            return completeFeatures;
        }

        // =============================================================
        // STEP 44:
        // Result returned by the native ONNX shadow classifier.
        // =============================================================

        internal class NativePredictionResult
        {
            public int Tier { get; set; }

            public string Action { get; set; }

            public float Confidence { get; set; }

            public float[] Probabilities { get; set; }

            public float CosineSimilarity { get; set; }

            public bool LlmFallbackRequired => !ConfidenceGatePassed;

            public bool ConfidenceGatePassed { get; set; }
            public double LatencyMs { get; set; }
        }

        // =============================================================
        // STEP 44C:
        // Run one complete native ONNX relevance prediction.
        // =============================================================

        internal NativePredictionResult Predict(
            string focusIntention,
            string websiteContext,
            string websiteUrl,
            string tabTitle)
        {
            var lockWaitTimer = Stopwatch.StartNew();
            lock (_predictionLock)
            {
                lockWaitTimer.Stop();
                _predictionLockWaitMs = lockWaitTimer.Elapsed.TotalMilliseconds;
                return PredictCore(focusIntention, websiteContext, websiteUrl, tabTitle);
            }
        }

        private NativePredictionResult PredictCore(
            string focusIntention,
            string websiteContext,
            string websiteUrl,
            string tabTitle)
        {
            focusIntention = (focusIntention ?? string.Empty).Trim();
            websiteContext = (websiteContext ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(focusIntention) &&
                string.IsNullOrWhiteSpace(tabTitle))
            {
                throw new ArgumentException("Provide at least a focus intention or tab title.");
            }

            _timings = new Dictionary<string, double>();
            _featureDetails = null;
            _videoFeatureProbe = null;
            _predictionSequence++;
            int gc0Before = GC.CollectionCount(0);
            int gc1Before = GC.CollectionCount(1);
            int gc2Before = GC.CollectionCount(2);

            // DEBUG: Check live website inputs
            Trace.WriteLine(
                "[NativeRelevanceClassifier] LIVE INPUT " +
                "Focus=" + focusIntention +
                " | Context=" + websiteContext +
                " | URL=" + websiteUrl +
                " | Title=" + tabTitle
            );

            var totalPredictionStopwatch = System.Diagnostics.Stopwatch.StartNew();

            var featureStopwatch = System.Diagnostics.Stopwatch.StartNew();

            float[] features =
               BuildCompleteFeatureVector(
                   focusIntention,
                   websiteContext,
                   websiteUrl,
                   tabTitle
               );

            if (features.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
                throw new InvalidOperationException("Classifier features contain a non-finite value.");

            if (EnableFeatureDiagnostics)
            {
                try
                {
                    string diagnosticDirectory = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "FocusBearDiagnostics");
                    Directory.CreateDirectory(diagnosticDirectory);
                    string diagnosticPath = Path.Combine(diagnosticDirectory, "native_features.csv");
                    File.WriteAllText(diagnosticPath, string.Join(
                        ",", features.Select(value => value.ToString("R", CultureInfo.InvariantCulture))));
                    Debug.WriteLine("[NativeRelevanceClassifier] FEATURE VECTOR SAVED: " + diagnosticPath);
                }
                catch (IOException ex)
                {
                    Debug.WriteLine("[NativeRelevanceClassifier] Feature dump failed: " + ex.Message);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Debug.WriteLine("[NativeRelevanceClassifier] Feature dump failed: " + ex.Message);
                }
            }

            // Diagnostic only: inspect the input vector.
            int nonZeroFeatures = features.Count(
                value => value != 0.0f
            );

            int goalTfidfNonZero = features
                .Take(533)
                .Count(value => value != 0.0f);

            int contextTfidfNonZero = features
                .Skip(533)
                .Take(5000)
                .Count(value => value != 0.0f);

            int engineeredNonZero = features
                .Skip(5533)
                .Take(12)
                .Count(value => value != 0.0f);

            Debug.WriteLine(
                "[NativeRelevanceClassifier] TFIDF CHECK " +
                "Goal=" + goalTfidfNonZero +
                " | Context=" + contextTfidfNonZero +
                " | Engineered=" + engineeredNonZero
            );

            Debug.WriteLine(
                "[NativeRelevanceClassifier] FEATURE CHECK " +
                "Length=" + features.Length +
                " | NonZero=" + nonZeroFeatures
            );

            for (int i = 5533; i < 5545; i++)
            {
                Debug.WriteLine(
                    "[NativeRelevanceClassifier] ENGINEERED[" +
                    (i - 5533) + "]=" +
                    features[i].ToString("F4")
                );
            }

            featureStopwatch.Stop();
            AddTiming("FeaturePreparationTotalMs", featureStopwatch.Elapsed.TotalMilliseconds);

            Trace.WriteLine(
                "[NativeRelevanceClassifier] LATENCY BREAKDOWN - FEATURES+MINILM MS=" +
                featureStopwatch.Elapsed.TotalMilliseconds.ToString(
                    "0.00",
                    CultureInfo.InvariantCulture
                )
            );

            var ensembleStopwatch = System.Diagnostics.Stopwatch.StartNew();

            float[] probabilities =
                RunSoftVotingEnsemble(features);

            Trace.WriteLine(
                "[NativeRelevanceClassifier] LIVE ENSEMBLE PROBABILITIES " +
                "Block=" + probabilities[0].ToString("F4") +
                " | Caution=" + probabilities[1].ToString("F4") +
                " | Allow=" + probabilities[2].ToString("F4")
            );

            ensembleStopwatch.Stop();
            AddTiming("EnsembleTotalMs", ensembleStopwatch.Elapsed.TotalMilliseconds);

            Debug.WriteLine(
                "[NativeRelevanceClassifier] LATENCY BREAKDOWN - ENSEMBLE MS=" +
                ensembleStopwatch.Elapsed.TotalMilliseconds.ToString(
                    "0.00",
                    CultureInfo.InvariantCulture
                )
            );

            var resultStopwatch = System.Diagnostics.Stopwatch.StartNew();

            int predictedTier =
                GetPredictedTier(probabilities);

            string predictedAction =
                GetPredictedAction(predictedTier);

            float predictionConfidence =
                GetPredictionConfidence(
                    probabilities,
                    predictedTier
                );

            bool confidenceGatePassed =
                PassesConfidenceGate(
                    predictionConfidence
                );

            resultStopwatch.Stop();
            AddTiming("ResultLogicMs", resultStopwatch.Elapsed.TotalMilliseconds);

            Debug.WriteLine(
                "[NativeRelevanceClassifier] LATENCY BREAKDOWN - RESULT LOGIC MS=" +
                resultStopwatch.Elapsed.TotalMilliseconds.ToString(
                    "0.00",
                    CultureInfo.InvariantCulture
                )
            );

            totalPredictionStopwatch.Stop();
            AddTiming("PredictionLockWaitMs", _predictionLockWaitMs);
            AddTiming("PredictionTotalMs", totalPredictionStopwatch.Elapsed.TotalMilliseconds);
            AddTiming("OtherPredictionWorkMs", Math.Max(0.0,
                totalPredictionStopwatch.Elapsed.TotalMilliseconds -
                featureStopwatch.Elapsed.TotalMilliseconds -
                ensembleStopwatch.Elapsed.TotalMilliseconds -
                resultStopwatch.Elapsed.TotalMilliseconds));
            AddTiming("GcGen0Collections", GC.CollectionCount(0) - gc0Before);
            AddTiming("GcGen1Collections", GC.CollectionCount(1) - gc1Before);
            AddTiming("GcGen2Collections", GC.CollectionCount(2) - gc2Before);

            Debug.WriteLine(
                "[NativeRelevanceClassifier] NATIVE PREDICT TOTAL MS=" +
                totalPredictionStopwatch.Elapsed.TotalMilliseconds.ToString(
                    "0.00",
                    CultureInfo.InvariantCulture
                )
            );

            Trace.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "[NativeRelevanceClassifier] RESULT Action={0} | Tier={1} | " +
                "Confidence={2:F4} | Cosine={3:F4} | LLMFallback={4} | " +
                "Block={5:F4} | Caution={6:F4} | Allow={7:F4} | LatencyMs={8:F2}",
                predictedAction, predictedTier, predictionConfidence, features[5533],
                !confidenceGatePassed, probabilities[0], probabilities[1], probabilities[2],
                totalPredictionStopwatch.Elapsed.TotalMilliseconds));

            var result = new NativePredictionResult
            {
                Tier = predictedTier,
                Action = predictedAction,
                Confidence = predictionConfidence,
                Probabilities = (float[])probabilities.Clone(),
                CosineSimilarity = features[5533],
                ConfidenceGatePassed = confidenceGatePassed,
                LatencyMs = totalPredictionStopwatch.Elapsed.TotalMilliseconds
            };

            // Feature report construction and file IO are outside PredictionLatencyMs.
            _featureDetails = BuildFeatureDetails(features);
            Uri probeUrl;
            string probeUrlText = websiteUrl ?? string.Empty;
            if (!probeUrlText.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                probeUrlText = "https://" + probeUrlText;
            if (_enableVideoFeatureProbe && !string.IsNullOrWhiteSpace(focusIntention) &&
                Uri.TryCreate(probeUrlText, UriKind.Absolute, out probeUrl) &&
                (probeUrl.Host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase) ||
                 probeUrl.Host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase)))
                _videoFeatureProbe = RunVideoFeatureProbe(
                    features, focusIntention, websiteContext, websiteUrl, tabTitle);
            WriteShadowResultLog(focusIntention, websiteContext, websiteUrl, tabTitle, result);
            return result;
        }

        private JObject RunVideoFeatureProbe(float[] baselineFeatures,
            string goal, string context, string url, string title)
        {
            // Diagnostic counterfactuals, not replacement decisions. Preserve
            // the baseline timing report and cache-hit flag during extra runs.
            Dictionary<string, double> baselineTimings = _timings;
            bool baselineCacheHit = _goalCacheHit;
            _timings = null;
            var timer = Stopwatch.StartNew();
            var report = new JObject { ["UsedForDecision"] = false };
            try
            {
                float[] withoutUrl = (float[])baselineFeatures.Clone();
                for (int i = 5534; i <= 5537; i++) withoutUrl[i] = 0.0f;
                report["UrlMetadataZeroed"] = EvaluateProbeVector(withoutUrl);

                float[] withoutContextTfidf = (float[])baselineFeatures.Clone();
                Array.Clear(withoutContextTfidf, 533, 5000);
                report["ContextTfidfZeroed"] = EvaluateProbeVector(withoutContextTfidf);

                // Remove one active TF-IDF coordinate at a time, without
                // renormalizing or changing embeddings/engineered features.
                // These synthetic ablations isolate feature influence only.
                var termProbes = new JArray();
                float[] withoutPlatformTerms = (float[])baselineFeatures.Clone();
                var platformTerms = new JArray();
                for (int localIndex = 0; localIndex < 5000; localIndex++)
                {
                    int featureIndex = 533 + localIndex;
                    float value = baselineFeatures[featureIndex];
                    if (value == 0.0f) continue;
                    string term = _contextFeatureNames[localIndex] ?? string.Empty;
                    float[] withoutTerm = (float[])baselineFeatures.Clone();
                    withoutTerm[featureIndex] = 0.0f;
                    JObject comparison = EvaluateProbeVector(withoutTerm);
                    comparison.Remove("FeatureDetails");
                    termProbes.Add(new JObject
                    {
                        ["Term"] = term,
                        ["FeatureIndex"] = featureIndex,
                        ["OriginalValue"] = value,
                        ["Result"] = comparison
                    });
                    if (string.Equals(term, "youtube", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(term, "watch", StringComparison.OrdinalIgnoreCase))
                    {
                        withoutPlatformTerms[featureIndex] = 0.0f;
                        platformTerms.Add(term);
                    }
                }
                report["IndividualContextTermProbes"] = termProbes;
                JObject platformComparison = EvaluateProbeVector(withoutPlatformTerms);
                platformComparison.Remove("FeatureDetails");
                report["YouTubeAndWatchTfidfZeroed"] = new JObject
                {
                    ["RemovedTerms"] = platformTerms,
                    ["Result"] = platformComparison
                };

                string cleanContext = Regex.Replace(context ?? string.Empty,
                    @"^\s*\(\d+\)\s*", string.Empty);
                string cleanTitle = Regex.Replace(title ?? string.Empty,
                    @"^\s*\(\d+\)\s*", string.Empty);
                report["NotificationCounterRemoved"] = EvaluateProbeVector(
                    BuildCompleteFeatureVector(goal, cleanContext, url, cleanTitle));

                cleanContext = Regex.Replace(context ?? string.Empty,
                    @"\s+-\s+Google Chrome(?=\s|$)", string.Empty);
                cleanTitle = Regex.Replace(title ?? string.Empty,
                    @"\s+-\s+Google Chrome$", string.Empty);
                report["BrowserSuffixRemoved"] = EvaluateProbeVector(
                    BuildCompleteFeatureVector(goal, cleanContext, url, cleanTitle));
            }
            catch (Exception ex)
            {
                report["Error"] = ex.Message;
            }
            finally
            {
                _timings = baselineTimings;
                _goalCacheHit = baselineCacheHit;
                timer.Stop();
            }
            report["ProbeLatencyMs"] = timer.Elapsed.TotalMilliseconds;
            return report;
        }

        private JObject EvaluateProbeVector(float[] features)
        {
            float[] treeFeatures = BuildXGBoostSparseInput(features);
            float[] lr = RunLogisticRegression(features);
            float[] xgb1 = RunXGBoost1(treeFeatures);
            float[] xgb2 = RunXGBoost2(treeFeatures);
            ValidateProbabilities(lr, "LR feature probe");
            ValidateProbabilities(xgb1, "XGB1 feature probe");
            ValidateProbabilities(xgb2, "XGB2 feature probe");
            float[] ensemble = new float[3];
            for (int i = 0; i < 3; i++)
                ensemble[i] = (lr[i] + 2.0f * xgb1[i] + 2.0f * xgb2[i]) / 5.0f;
            return new JObject
            {
                ["LogisticRegression"] = DescribeProbeResult(lr),
                ["XGBoost1"] = DescribeProbeResult(xgb1),
                ["XGBoost2"] = DescribeProbeResult(xgb2),
                ["Ensemble"] = DescribeProbeResult(ensemble),
                ["FeatureDetails"] = BuildFeatureDetails(features)
            };
        }

        private JObject DescribeProbeResult(float[] probabilities)
        {
            int tier = GetPredictedTier(probabilities);
            return new JObject
            {
                ["Action"] = GetPredictedAction(tier),
                ["Confidence"] = probabilities[tier],
                ["BlockProbability"] = probabilities[0],
                ["CautionProbability"] = probabilities[1],
                ["AllowProbability"] = probabilities[2]
            };
        }

        private void AddTiming(string name, double milliseconds)
        {
            if (_timings == null) return;
            double previous;
            _timings.TryGetValue(name, out previous);
            _timings[name] = previous + milliseconds;
        }

        private static string[] ReadFeatureNames(JObject data, int count)
        {
            string[] names = new string[count];
            foreach (var property in ((JObject)data["vocabulary"]).Properties())
            {
                int index = (int)property.Value;
                if (index >= 0 && index < count) names[index] = property.Name;
            }
            return names;
        }

        private JObject BuildFeatureDetails(float[] features)
        {
            var goal = new JArray();
            var context = new JArray();
            for (int i = 0; i < 5533; i++)
            {
                if (features[i] == 0.0f) continue;
                bool isGoal = i < 533;
                int localIndex = isGoal ? i : i - 533;
                var entry = new JObject
                {
                    ["FeatureIndex"] = i,
                    ["Term"] = isGoal ? _goalFeatureNames[localIndex] : _contextFeatureNames[localIndex],
                    ["Value"] = features[i]
                };
                if (isGoal) goal.Add(entry); else context.Add(entry);
            }
            string[] names =
            {
                "CosineSimilarity", "UrlPathDepth", "UrlLength", "UrlHasSubdomain",
                "UrlHasQuery", "IsFeedOrMessaging", "TabTitleMissing", "WordOverlap",
                "GoalWordCount", "ContextWordCount", "CosineTimesOverlap", "CosineTimesFeed"
            };
            var engineered = new JObject();
            for (int i = 0; i < names.Length; i++)
                engineered[names[i]] = features[5533 + i];
            return new JObject
            {
                ["FeatureCount"] = features.Length,
                ["GoalTfidfNonZeroCount"] = goal.Count,
                ["ContextTfidfNonZeroCount"] = context.Count,
                ["GoalTfidfNonZero"] = goal,
                ["ContextTfidfNonZero"] = context,
                ["EngineeredStartIndex"] = 5533,
                ["Engineered"] = engineered
            };
        }

        private static float[] BuildXGBoostSparseInput(float[] features)
        {
            // Training used scipy CSR with no stored zeros. Absent entries
            // follow the learned missing-value branch in XGBoost trees.
            // Keep the original finite vector for LR and feature diagnostics.
            float[] treeFeatures = (float[])features.Clone();
            for (int i = 0; i < treeFeatures.Length; i++)
            {
                if (treeFeatures[i] == 0.0f)
                    treeFeatures[i] = float.NaN;
            }
            return treeFeatures;
        }

        private JObject BuildIndividualModelLog()
        {
            string[] names = { "LogisticRegression", "XGBoost1", "XGBoost2" };
            var models = new JObject();
            for (int i = 0; i < names.Length; i++)
            {
                float[] probabilities = _individualProbabilities[i];
                int tier = GetPredictedTier(probabilities);
                models[names[i]] = new JObject
                {
                    ["Action"] = GetPredictedAction(tier),
                    ["Tier"] = tier,
                    ["Confidence"] = probabilities[tier],
                    ["BlockProbability"] = probabilities[0],
                    ["CautionProbability"] = probabilities[1],
                    ["AllowProbability"] = probabilities[2],
                    ["ModelCallLatencyMs"] = _individualLatencyMs[i]
                };
            }
            return models;
        }

        private void WriteShadowResultLog(
            string focusIntention, string websiteContext, string websiteUrl,
            string tabTitle, NativePredictionResult result)
        {
            try
            {
#if DEBUG
                const string buildConfiguration = "Debug";
#else
                const string buildConfiguration = "Release";
#endif
                string directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FocusBear");
                Directory.CreateDirectory(directory);
                var record = new JObject
                {
                    ["TimestampUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    ["Build"] = buildConfiguration,
                    ["DebuggerAttached"] = Debugger.IsAttached,
                    ["Mode"] = "SHADOW",
                    ["Goal"] = focusIntention ?? string.Empty,
                    ["Context"] = websiteContext ?? string.Empty,
                    ["ContextEmbeddingInput"] = _contextEmbeddingInput,
                    ["ContextEmbeddingSequenceLength"] = _contextMiniLmSequenceLength,
                    ["TrackingContextCleaned"] = _trackingContextCleaned,
                    ["TrackingContextExperimentEnabled"] = string.Equals(
                        Environment.GetEnvironmentVariable("FOCUSBEAR_TRACKING_CONTEXT_EXPERIMENT"),
                        "1", StringComparison.Ordinal),
                    ["Url"] = websiteUrl ?? string.Empty,
                    ["Title"] = tabTitle ?? string.Empty,
                    ["Action"] = result.Action,
                    ["Tier"] = result.Tier,
                    ["Confidence"] = result.Confidence,
                    ["CosineSimilarity"] = result.CosineSimilarity,
                    ["LLMFallback"] = result.LlmFallbackRequired,
                    ["PredictionLatencyMs"] = result.LatencyMs,
                    ["BlockProbability"] = result.Probabilities[0],
                    ["CautionProbability"] = result.Probabilities[1],
                    ["AllowProbability"] = result.Probabilities[2],
                    ["IndividualModels"] = BuildIndividualModelLog(),
                    ["XGBoostInputPolicy"] = "SparseEquivalent_ZeroToNaN",
                    ["StartupWarmupCompleted"] = _startupWarmupCompleted,
                    ["StartupWarmupMs"] = _startupWarmupMs,
                    ["PredictionSequence"] = _predictionSequence,
                    ["GoalEmbeddingCacheHit"] = _goalCacheHit,
                    ["TimingBreakdown"] = JObject.FromObject(_timings),
                    ["FeatureDetails"] = _featureDetails,
                    ["OnnxThreadSettings"] = new JObject
                    {
                        ["IntraOpNumThreads"] = _onnxThreadCount,
                        ["ExecutionMode"] = "Sequential"
                    },
                    ["GoalMissing"] = string.IsNullOrWhiteSpace(focusIntention),
                    ["VideoFeatureProbe"] = _videoFeatureProbe
                };
                File.AppendAllText(Path.Combine(directory, "native-relevance-shadow.log"),
                    record.ToString(Newtonsoft.Json.Formatting.None) + Environment.NewLine);
            }
            catch (IOException ex)
            {
                Trace.WriteLine("[NativeRelevanceClassifier] Shadow file log unavailable: " + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                Trace.WriteLine("[NativeRelevanceClassifier] Shadow file log unavailable: " + ex.Message);
            }
            catch (System.Security.SecurityException ex)
            {
                Trace.WriteLine("[NativeRelevanceClassifier] Shadow file log unavailable: " + ex.Message);
            }
        }

        // =============================================================
        // STEP 35B:
        // Logistic Regression ONNX inference.
        // =============================================================

        private float[] RunLogisticRegression(
            float[] features)
        {
            if (_logisticRegressionSession == null)
            {
                throw new InvalidOperationException(
                    "Logistic Regression ONNX session has not been loaded."
                );
            }

            if (features == null)
            {
                throw new ArgumentNullException(
                    nameof(features)
                );
            }

            if (features.Length != 5545)
            {
                throw new ArgumentException(
                    "Logistic Regression expects exactly 5545 features.",
                    nameof(features)
                );
            }

            DenseTensor<float> featureTensor =
                new DenseTensor<float>(
                    features,
                    new[] { 1, 5545 }
                );

            List<NamedOnnxValue> inputs =
                new List<NamedOnnxValue>
                {
            NamedOnnxValue.CreateFromTensor(
                "features",
                featureTensor
            )
                };

            var lrStopwatch = System.Diagnostics.Stopwatch.StartNew();

            using (
                IDisposableReadOnlyCollection<DisposableNamedOnnxValue>
                    results =
                        _logisticRegressionSession.Run(inputs)
            )
            {
                AddTiming("LogisticRegressionOnnxRunMs", lrStopwatch.Elapsed.TotalMilliseconds);
                DisposableNamedOnnxValue probabilityOutput =
                    results.FirstOrDefault(
                        result =>
                            result.Name == "probabilities"
                    );

                if (probabilityOutput == null)
                {
                    throw new InvalidOperationException(
                        "Logistic Regression output 'probabilities' was not returned."
                    );
                }

                Tensor<float> probabilityTensor =
                    probabilityOutput.AsTensor<float>();

                float[] probabilities =
                    probabilityTensor.ToArray();

                if (probabilities.Length != 3)
                {
                    throw new InvalidOperationException(
                        "Logistic Regression expected exactly 3 class probabilities."
                    );
                }

                lrStopwatch.Stop();

                System.Diagnostics.Trace.WriteLine(
                     "[NativeRelevanceClassifier] LR PROBABILITIES " +
                     "Class0=" + probabilities[0].ToString("F4") +
                     " | Class1=" + probabilities[1].ToString("F4") +
                     " | Class2=" + probabilities[2].ToString("F4")
                );

                Debug.WriteLine(
                    "[NativeRelevanceClassifier] LATENCY BREAKDOWN - LR MS=" +
                    lrStopwatch.Elapsed.TotalMilliseconds.ToString(
                        "0.00",
                        CultureInfo.InvariantCulture
                    )
                );

                return probabilities;
            }
        }

        // =============================================================
        // STEP 36:
        // XGBoost 1 ONNX inference.
        // =============================================================

        private float[] RunXGBoost1(
            float[] features)
        {
            if (_xgboost1Session == null)
            {
                throw new InvalidOperationException(
                    "XGBoost 1 ONNX session has not been loaded."
                );
            }

            if (features == null)
            {
                throw new ArgumentNullException(
                    nameof(features)
                );
            }

            if (features.Length != 5545)
            {
                throw new ArgumentException(
                    "XGBoost 1 expects exactly 5545 features.",
                    nameof(features)
                );
            }

            DenseTensor<float> featureTensor =
                new DenseTensor<float>(
                    features,
                    new[] { 1, 5545 }
                );

            List<NamedOnnxValue> inputs =
                new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(
                        "features",
                        featureTensor
                    )
                };

            var xgb1Stopwatch = System.Diagnostics.Stopwatch.StartNew();

            using (
                IDisposableReadOnlyCollection<DisposableNamedOnnxValue>
                    results =
                        _xgboost1Session.Run(inputs)
            )
            {
                AddTiming("XGBoost1OnnxRunMs", xgb1Stopwatch.Elapsed.TotalMilliseconds);
                DisposableNamedOnnxValue probabilityOutput =
                    results.FirstOrDefault(
                        result =>
                            result.Name == "probabilities"
                    );

                if (probabilityOutput == null)
                {
                    throw new InvalidOperationException(
                        "XGBoost 1 output 'probabilities' was not returned."
                    );
                }

                Tensor<float> probabilityTensor =
                    probabilityOutput.AsTensor<float>();

                float[] probabilities =
                    probabilityTensor.ToArray();

                if (probabilities.Length != 3)
                {
                    throw new InvalidOperationException(
                        "XGBoost 1 expected exactly 3 class probabilities."
                    );
                }

                xgb1Stopwatch.Stop();

                System.Diagnostics.Trace.WriteLine(
                    "[NativeRelevanceClassifier] XGB1 PROBABILITIES " +
                    "Class0=" + probabilities[0].ToString("F4") +
                    " | Class1=" + probabilities[1].ToString("F4") +
                    " | Class2=" + probabilities[2].ToString("F4")
                );

                Debug.WriteLine(
                    "[NativeRelevanceClassifier] LATENCY BREAKDOWN - XGB1 MS=" +
                    xgb1Stopwatch.Elapsed.TotalMilliseconds.ToString(
                        "0.00",
                        CultureInfo.InvariantCulture
                    )
                );

                return probabilities;
            }
        }

        // =============================================================
        // STEP 37:
        // XGBoost 2 ONNX inference.
        // =============================================================

        private float[] RunXGBoost2(
            float[] features)
        {
            if (_xgboost2Session == null)
            {
                throw new InvalidOperationException(
                    "XGBoost 2 ONNX session has not been loaded."
                );
            }

            if (features == null)
            {
                throw new ArgumentNullException(
                    nameof(features)
                );
            }

            if (features.Length != 5545)
            {
                throw new ArgumentException(
                    "XGBoost 2 expects exactly 5545 features.",
                    nameof(features)
                );
            }

            DenseTensor<float> featureTensor =
                new DenseTensor<float>(
                    features,
                    new[] { 1, 5545 }
                );

            List<NamedOnnxValue> inputs =
                new List<NamedOnnxValue>
                {
            NamedOnnxValue.CreateFromTensor(
                "features",
                featureTensor
            )
                };

            var xgb2Stopwatch = System.Diagnostics.Stopwatch.StartNew();

            using (
                IDisposableReadOnlyCollection<DisposableNamedOnnxValue>
                    results =
                        _xgboost2Session.Run(inputs)
            )
            {
                AddTiming("XGBoost2OnnxRunMs", xgb2Stopwatch.Elapsed.TotalMilliseconds);
                DisposableNamedOnnxValue probabilityOutput =
                    results.FirstOrDefault(
                        result =>
                            result.Name == "probabilities"
                    );

                if (probabilityOutput == null)
                {
                    throw new InvalidOperationException(
                        "XGBoost 2 output 'probabilities' was not returned."
                    );
                }

                Tensor<float> probabilityTensor =
                    probabilityOutput.AsTensor<float>();

                float[] probabilities =
                    probabilityTensor.ToArray();

                if (probabilities.Length != 3)
                {
                    throw new InvalidOperationException(
                        "XGBoost 2 expected exactly 3 class probabilities."
                    );
                }

                xgb2Stopwatch.Stop();

                System.Diagnostics.Trace.WriteLine(
                    "[NativeRelevanceClassifier] XGB2 PROBABILITIES " +
                    "Class0=" + probabilities[0].ToString("F4") +
                    " | Class1=" + probabilities[1].ToString("F4") +
                    " | Class2=" + probabilities[2].ToString("F4")
                );

                Debug.WriteLine(
                    "[NativeRelevanceClassifier] LATENCY BREAKDOWN - XGB2 MS=" +
                    xgb2Stopwatch.Elapsed.TotalMilliseconds.ToString(
                        "0.00",
                        CultureInfo.InvariantCulture
                    )
                );

                return probabilities;
            }
        }

        // =============================================================
        // STEP 38:
        // Original soft-voting ensemble.
        // Weights:
        // Logistic Regression = 1
        // XGBoost 1          = 2
        // XGBoost 2          = 2
        // =============================================================

        private float[] RunSoftVotingEnsemble(float[] features)
        {
            float[] treeFeatures = BuildXGBoostSparseInput(features);

            var lrTimer = Stopwatch.StartNew();

            float[] lrProbabilities =
                RunLogisticRegression(features);

            lrTimer.Stop();

            Trace.WriteLine(
                "[NativeRelevanceClassifier] FULL LR CALL MS=" +
                lrTimer.Elapsed.TotalMilliseconds.ToString("F2")
            );


            var xgb1Timer = Stopwatch.StartNew();

            float[] xgb1Probabilities =
                RunXGBoost1(treeFeatures);

            xgb1Timer.Stop();

            Trace.WriteLine(
                "[NativeRelevanceClassifier] FULL XGB1 CALL MS=" +
                xgb1Timer.Elapsed.TotalMilliseconds.ToString("F2")
            );


            var xgb2Timer = Stopwatch.StartNew();

            float[] xgb2Probabilities =
                RunXGBoost2(treeFeatures);

            xgb2Timer.Stop();

            Trace.WriteLine(
                "[NativeRelevanceClassifier] FULL XGB2 CALL MS=" +
                xgb2Timer.Elapsed.TotalMilliseconds.ToString("F2")
            );

            ValidateProbabilities(lrProbabilities, "Logistic Regression");
            ValidateProbabilities(xgb1Probabilities, "XGBoost 1");
            ValidateProbabilities(xgb2Probabilities, "XGBoost 2");

            _individualProbabilities = new[]
            {
                lrProbabilities, xgb1Probabilities, xgb2Probabilities
            };
            _individualLatencyMs = new[]
            {
                lrTimer.Elapsed.TotalMilliseconds,
                xgb1Timer.Elapsed.TotalMilliseconds,
                xgb2Timer.Elapsed.TotalMilliseconds
            };

            float[] ensembleProbabilities =
                new float[3];

            for (int i = 0; i < 3; i++)
            {
                ensembleProbabilities[i] =
                    (
                        lrProbabilities[i] +
                        (2.0f * xgb1Probabilities[i]) +
                        (2.0f * xgb2Probabilities[i])
                    ) / 5.0f;
            }

            return ensembleProbabilities;
        }

        // =============================================================
        // STEP 39:
        // Select the winning relevance tier from ensemble probabilities.
        // 0 = Definitely Not Relevant
        // 1 = Maybe Relevant
        // 2 = Likely Relevant
        // =============================================================

        // Reject invalid ONNX outputs rather than reporting a misleading decision.
        private static void ValidateProbabilities(float[] values, string modelName)
        {
            if (values == null || values.Length != 3)
                throw new InvalidOperationException(modelName + " must return three probabilities.");
            double sum = 0;
            foreach (float value in values)
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 1)
                    throw new InvalidOperationException(modelName + " returned an invalid probability.");
                sum += value;
            }
            if (Math.Abs(sum - 1.0) > 0.001)
                throw new InvalidOperationException(modelName + " probabilities do not sum to one.");
        }

        private int GetPredictedTier(
            float[] probabilities)
        {
            if (probabilities == null)
            {
                throw new ArgumentNullException(
                    nameof(probabilities)
                );
            }

            if (probabilities.Length != 3)
            {
                throw new ArgumentException(
                    "Expected exactly 3 class probabilities.",
                    nameof(probabilities)
                );
            }

            int predictedTier = 0;

            for (int i = 1; i < probabilities.Length; i++)
            {
                if (probabilities[i] > probabilities[predictedTier])
                {
                    predictedTier = i;
                }
            }

            return predictedTier;
        }

        // =============================================================
        // STEP 40:
        // Map the predicted relevance tier to the Focus Bear action.
        // 0 = Block
        // 1 = Caution
        // 2 = Allow
        // =============================================================

        private string GetPredictedAction(int predictedTier)
        {
            switch (predictedTier)
            {
                case 0:
                    return "Block";

                case 1:
                    return "Caution";

                case 2:
                    return "Allow";

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(predictedTier),
                        "Predicted tier must be 0, 1, or 2."
                    );
            }
        }

        // =============================================================
        // Engineered features.
        // =============================================================

        private float[] BuildSimpleFeatures(
            string focusIntention,
            string websiteContext,
            float cosineSimilarity,
            string websiteUrl,
            string tabTitle)
{
    focusIntention =
        focusIntention ?? string.Empty;

    websiteContext =
        websiteContext ?? string.Empty;

    string[] goalWords =
        focusIntention
            .ToLowerInvariant()
            .Split(
                new[] { ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries
            );

    string[] contextWords =
        websiteContext
            .ToLowerInvariant()
            .Split(
                new[] { ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries
            );

    HashSet<string> goalSet =
        new HashSet<string>(goalWords);

    HashSet<string> contextSet =
        new HashSet<string>(contextWords);

    int intersectionCount =
        goalSet
            .Intersect(contextSet)
            .Count();

    float overlap =
       goalSet.Count > 0
           ? (float)intersectionCount / goalSet.Count
           : 0.0f;

            float goalLength =
        goalWords.Length;

    float contextLength =
        contextWords.Length;

            string normalizedUrl = websiteUrl ?? string.Empty;

            if (!normalizedUrl.StartsWith(
                    "http",
                    StringComparison.OrdinalIgnoreCase))
            {
                normalizedUrl = "https://" + normalizedUrl;
            }

            Uri parsedUrl;
            bool validUrl = Uri.TryCreate(
                normalizedUrl,
                UriKind.Absolute,
                out parsedUrl
            );

            float urlPathDepth = 0.0f;
            float urlLength = 0.0f;

            if (validUrl)
            {
                urlPathDepth = parsedUrl.AbsolutePath
                    .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                    .Length;

                urlLength = normalizedUrl.Length;
            }

            float hasSubdomain = 0.0f;
            float hasQuery = 0.0f;

            if (validUrl)
            {
                string[] domainParts = parsedUrl.Host.Split('.');

                hasSubdomain =
                    domainParts.Length > 2 &&
                    domainParts[0] != "www" &&
                    domainParts[0] != ""
                        ? 1.0f
                        : 0.0f;

                hasQuery =
                    !string.IsNullOrEmpty(parsedUrl.Query)
                        ? 1.0f
                        : 0.0f;
            }

            string[] guardrailKeywords =
{
    "feed", "home", "timeline", "messages",
    "inbox", "notifications", "trending",
    "dashboard", "news feed", "social"
};

            string tabTitleLower =
                (tabTitle ?? string.Empty).ToLowerInvariant();

            float isFeedOrMessaging =
                guardrailKeywords.Any(keyword =>
                    tabTitleLower.Contains(keyword))
                    ? 1.0f
                    : 0.0f;

            float tabTitleMissing =
                string.IsNullOrWhiteSpace(tabTitle)
                    ? 1.0f
                    : 0.0f;

            float[] urlFeatures =
            {
    urlPathDepth,       // 5534
    urlLength,          // 5535
    hasSubdomain,       // 5536
    hasQuery,           // 5537
    isFeedOrMessaging,  // 5538
    tabTitleMissing     // 5539
};

            float cosineOverlap =
        cosineSimilarity * overlap;

            float cosineFeed =
        cosineSimilarity * isFeedOrMessaging;

            return new float[]
    {
        cosineSimilarity,   // 5533

        urlFeatures[0],      // 5534
        urlFeatures[1],      // 5535
        urlFeatures[2],      // 5536
        urlFeatures[3],      // 5537
        urlFeatures[4],      // 5538
        urlFeatures[5],      // 5539

        overlap,             // 5540
        goalLength,          // 5541
        contextLength,       // 5542
        cosineOverlap,       // 5543
        cosineFeed           // 5544
    };
}

// Close NativeRelevanceClassifier class
}

// Close Focus_Bear.RelevanceClassifier namespace
}
