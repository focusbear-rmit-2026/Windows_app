using System;
using System.Collections.Generic;

namespace FocusBear.Core.Relevance
{
    /// <summary>One file of the relevance model, with the size and SHA-256 it must have.</summary>
    public sealed class RelevanceModelFile
    {
        public RelevanceModelFile(string name, string sha256, long bytes)
        {
            Name = name;
            Sha256 = sha256;
            Bytes = bytes;
        }

        public string Name { get; }
        public string Sha256 { get; }
        public long Bytes { get; }
    }

    /// <summary>
    /// The shared relevance model the Windows, Mac and Android apps all run: the research repo's
    /// <c>ensemble.pkl</c> (logistic regression + XGBoost + tuned XGBoost, soft vote 1:2:2, 6,313 features with the
    /// goal and page MiniLM embeddings appended), exported once and hosted on R2 under one versioned prefix.
    ///
    /// The hashes are compiled in on purpose: the manifest.json next to the files comes from the same host, so it
    /// can't vouch for them. A file whose size or hash differs is never loaded, and shadow mode stays off.
    /// </summary>
    public static class RelevanceModelFiles
    {
        public const string Version = "v2.1";
        public const string BaseUrl = "https://pub-a0f25f0d306d4d63bbfc52aa624ae389.r2.dev";

        // Downloads are attempted only after the application's explicit native-shadow opt-in.
        public static bool DownloadsEnabled => true;

        public const string Encoder = "minilm.onnx";
        public const string Vocabulary = "vocab.txt";
        public const string LogisticRegression = "lr.onnx";
        public const string XGBoost = "xgb.onnx";
        public const string XGBoostTuned = "xgb_tuned.onnx";
        public const string GoalTfidf = "tfidf_goal.json";
        public const string ContextTfidf = "tfidf_context.json";

        public static readonly IReadOnlyList<RelevanceModelFile> All = new[]
        {
            new RelevanceModelFile(Encoder, "8eb426ef41663e2f3c4e4c98fb894bb2056bcab48312ba623793ba2b88ca23c4", 90386902),
            new RelevanceModelFile(Vocabulary, "07eced375cec144d27c900241f3e339478dec958f92fddbc551f295c992038a3", 231508),
            new RelevanceModelFile(LogisticRegression, "38a2835f87eb75cc71a62509b29ab539f43c1329d4ad911564b3ac1096f09663", 95180),
            new RelevanceModelFile(XGBoost, "5ca2ac39543431054f734ce2c2dbe9f857ee7beebaaa4067b6726e5359fb5a6b", 1434224),
            new RelevanceModelFile(XGBoostTuned, "332d107d73c2a00b57382fd4c7ba067765c65b034fbb65c578f446e4107ae003", 2613492),
            new RelevanceModelFile(GoalTfidf, "bbc57c9917e4cfc193dd176f6fefbda8817d9004eeadee346a128ec9b71bdf26", 18441),
            new RelevanceModelFile(ContextTfidf, "27906589d04ae9238f0b8a9e3927721121aceb28db0cfef46e2f5b0c7424c621", 180874),
        };

        public static Uri DownloadUrl(RelevanceModelFile file) =>
            new Uri(BaseUrl + "/" + Version + "/" + Uri.EscapeDataString(file.Name));
    }
}
