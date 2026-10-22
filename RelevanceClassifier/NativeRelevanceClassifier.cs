using FocusBear.Core.Relevance;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Focus_Bear.RelevanceClassifier
{
    /// <summary>
    /// The shared relevance model (see <see cref="RelevanceModelFiles"/>) run natively with ONNX Runtime, in shadow
    /// mode: it predicts Block / Caution / Allow for a page but never changes what Focus Bear blocks.
    /// </summary>
    internal class NativeRelevanceClassifier
    {
        private const string FeaturesInput = "features";
        private const string ProbabilitiesOutput = "probabilities";
        private const string InputIds = "input_ids";
        private const string AttentionMask = "attention_mask";
        private const string TokenTypeIds = "token_type_ids";
        private const string EmbeddingsOutput = "embeddings";
        private const string ThreadCountVariable = "FOCUSBEAR_ONNX_THREADS";
        private const string TrackingContextVariable = "FOCUSBEAR_TRACKING_CONTEXT_EXPERIMENT";
        private const string TrackingRoute = "/events/public/v1/encoded/track/";
        private const int DefaultThreadCount = 1;
        private const int MaxThreadCount = 32;
        private const string WarmUpGoal = "Learn programming";
        private const string WarmUpTitle = "Programming tutorial";
        private const string WarmUpUrl = "https://example.com/tutorial";

        private static readonly Regex LongTrackingToken = new Regex(@"\S{80,}");
        private static readonly Regex Whitespace = new Regex(@"\s+");

        private InferenceSession _logisticRegression;
        private InferenceSession _xgboost;
        private InferenceSession _xgboostTuned;
        private InferenceSession _encoder;
        private NativeMiniLmTokenizer _tokenizer;
        private NativeTfidfVectorizer _goalVectorizer;
        private NativeTfidfVectorizer _contextVectorizer;

        // Predictions are serialized because the goal embedding cache is shared.
        private readonly object _predictionLock = new object();
        private string _cachedGoal;
        private float[] _cachedGoalEmbedding;

        internal int EffectiveOnnxThreadCount { get; } = ReadThreadCount();
        internal bool TrackingContextExperimentEnabled =>
            string.Equals(Environment.GetEnvironmentVariable(TrackingContextVariable), "1", StringComparison.Ordinal);
        internal string ModelDirectory { get; private set; }
        internal double WarmUpMs { get; private set; }

        internal class NativePredictionResult
        {
            public RelevanceTier Tier { get; set; }
            public string Action => Tier.ToString();
            public float Confidence { get; set; }
            public float[] Probabilities { get; set; }
            public float CosineSimilarity { get; set; }
            public bool ConfidenceGatePassed { get; set; }
            public bool LlmFallbackRequired => !ConfidenceGatePassed;
            /// <summary>True when the email-goal rule allowed a mail inbox the model alone would not have.</summary>
            public bool AllowedByEmailGoalRule { get; set; }
            public double LatencyMs { get; set; }
        }

        /// <summary>Loads every session from a folder the installer has verified, then runs one warm-up prediction.</summary>
        public void LoadModels(string modelDirectory)
        {
            ModelDirectory = modelDirectory;
            _logisticRegression = CreateSession(RelevanceModelFiles.LogisticRegression);
            _xgboost = CreateSession(RelevanceModelFiles.XGBoost);
            _xgboostTuned = CreateSession(RelevanceModelFiles.XGBoostTuned);
            _encoder = CreateSession(RelevanceModelFiles.Encoder);
            _tokenizer = new NativeMiniLmTokenizer(PathOf(RelevanceModelFiles.Vocabulary));
            _goalVectorizer = new NativeTfidfVectorizer(JObject.Parse(File.ReadAllText(PathOf(RelevanceModelFiles.GoalTfidf))));
            _contextVectorizer = new NativeTfidfVectorizer(JObject.Parse(File.ReadAllText(PathOf(RelevanceModelFiles.ContextTfidf))));
            WarmUp();
        }

        internal NativePredictionResult Predict(string goal, string context, string url, string title)
        {
            lock (_predictionLock)
            {
                return PredictCore((goal ?? string.Empty).Trim(), (context ?? string.Empty).Trim(), url ?? string.Empty, title ?? string.Empty);
            }
        }

        private NativePredictionResult PredictCore(string goal, string context, string url, string title)
        {
            if (goal.Length == 0 && string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Provide at least a focus intention or tab title.");

            Stopwatch timer = Stopwatch.StartNew();
            float cosine;
            float[] features = BuildFeatures(goal, context, url, title, out cosine);
            float[] trees = RelevanceEnsemble.ZerosAsMissing(features);
            float[] probabilities = RelevanceEnsemble.Vote(
                RunEstimator(_logisticRegression, features),
                RunEstimator(_xgboost, trees),
                RunEstimator(_xgboostTuned, trees));
            timer.Stop();

            RelevanceTier modelTier = RelevanceEnsemble.MostLikely(probabilities);
            bool emailRule = modelTier != RelevanceTier.Allow && EmailGoal.AllowsMailPage(goal, url, title);
            RelevanceTier tier = emailRule ? RelevanceTier.Allow : modelTier;
            float confidence = probabilities[(int)tier];
            return new NativePredictionResult
            {
                Tier = tier,
                Confidence = confidence,
                Probabilities = probabilities,
                CosineSimilarity = cosine,
                ConfidenceGatePassed = emailRule || confidence >= RelevanceEnsemble.ConfidenceGate,
                AllowedByEmailGoalRule = emailRule,
                LatencyMs = timer.Elapsed.TotalMilliseconds,
            };
        }

        private float[] BuildFeatures(string goal, string context, string url, string title, out float cosine)
        {
            float[] goalEmbedding = GoalEmbedding(goal);
            float[] contextEmbedding = Embed(ContextForEmbedding(context, url));
            cosine = RelevanceFeatures.Cosine(goalEmbedding, contextEmbedding);
            return RelevanceFeatures.Assemble(
                _goalVectorizer.Transform(goal),
                _contextVectorizer.Transform(context),
                RelevanceFeatures.Scalars(goal, context, cosine, url, title),
                goalEmbedding,
                contextEmbedding);
        }

        private float[] GoalEmbedding(string goal)
        {
            if (_cachedGoalEmbedding == null || !string.Equals(_cachedGoal, goal, StringComparison.Ordinal))
            {
                _cachedGoalEmbedding = Embed(goal);
                _cachedGoal = goal;
            }
            return _cachedGoalEmbedding;
        }

        /// <summary>The encoder graph already mean-pools and L2-normalises, so its output is the sentence embedding.</summary>
        private float[] Embed(string text)
        {
            NativeMiniLmTokenizer.TokenizedInput tokens = _tokenizer.Encode(text ?? string.Empty);
            int[] shape = { 1, tokens.SequenceLength };
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(InputIds, new DenseTensor<long>(tokens.InputIds, shape)),
                NamedOnnxValue.CreateFromTensor(AttentionMask, new DenseTensor<long>(tokens.AttentionMask, shape)),
                NamedOnnxValue.CreateFromTensor(TokenTypeIds, new DenseTensor<long>(tokens.TokenTypeIds, shape)),
            };
            using (IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _encoder.Run(inputs))
            {
                float[] embedding = Output(results, EmbeddingsOutput).ToArray();
                if (embedding.Length != RelevanceFeatures.EmbeddingSize)
                    throw new InvalidOperationException("The encoder returned " + embedding.Length + " values, expected " +
                        RelevanceFeatures.EmbeddingSize + ".");
                return embedding;
            }
        }

        private static float[] RunEstimator(InferenceSession session, float[] features)
        {
            var tensor = new DenseTensor<float>(features, new[] { 1, features.Length });
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(FeaturesInput, tensor) };
            using (IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = session.Run(inputs))
            {
                return Output(results, ProbabilitiesOutput).ToArray();
            }
        }

        private static Tensor<float> Output(IEnumerable<DisposableNamedOnnxValue> results, string name)
        {
            DisposableNamedOnnxValue output = results.FirstOrDefault(result => result.Name == name);
            if (output == null) throw new InvalidOperationException("ONNX output '" + name + "' was not returned.");
            return output.AsTensor<float>();
        }

        // Opt-in experiment: long letter+digit tracking tokens in email-tracking redirect URLs make the encoder input
        // 10x longer for no meaning. TF-IDF and URL features keep the original context.
        private string ContextForEmbedding(string context, string url)
        {
            if (!TrackingContextExperimentEnabled) return context;
            Uri page = RelevanceInput.ParseWebUrl(url);
            if (page == null || !page.AbsolutePath.StartsWith(TrackingRoute, StringComparison.OrdinalIgnoreCase)) return context;
            string cleaned = LongTrackingToken.Replace(context, match =>
                match.Value.Any(char.IsLetter) && match.Value.Any(char.IsDigit) ? string.Empty : match.Value);
            cleaned = Whitespace.Replace(cleaned, " ").Trim();
            return cleaned.Length == 0 ? context : cleaned;
        }

        // The first run of each session is several times slower; pay it here, off the browsing path.
        private void WarmUp()
        {
            Stopwatch timer = Stopwatch.StartNew();
            lock (_predictionLock)
            {
                PredictCore(WarmUpGoal, WarmUpTitle, WarmUpUrl, WarmUpTitle);
                _cachedGoal = null;
                _cachedGoalEmbedding = null;
            }
            WarmUpMs = timer.Elapsed.TotalMilliseconds;
        }

        private InferenceSession CreateSession(string fileName)
        {
            using (var options = new SessionOptions())
            {
                // Zero restores ONNX Runtime's automatic thread selection, for comparison runs.
                options.IntraOpNumThreads = EffectiveOnnxThreadCount;
                options.InterOpNumThreads = 1;
                options.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
                return new InferenceSession(PathOf(fileName), options);
            }
        }

        private string PathOf(string fileName) => Path.Combine(ModelDirectory, fileName);

        private static int ReadThreadCount()
        {
            int count;
            return int.TryParse(Environment.GetEnvironmentVariable(ThreadCountVariable), out count) &&
                count >= 0 && count <= MaxThreadCount ? count : DefaultThreadCount;
        }
    }
}
