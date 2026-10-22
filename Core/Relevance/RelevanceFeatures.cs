using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FocusBear.Core.Relevance
{
    /// <summary>
    /// The 6,313-wide feature vector of the shared model, in training order:
    /// goal TF-IDF (533), page TF-IDF (5,000), 12 scalars, goal embedding (384), page embedding (384).
    /// </summary>
    public static class RelevanceFeatures
    {
        public const int GoalTfidfCount = 533;
        public const int ContextTfidfCount = 5000;
        public const int ScalarCount = 12;
        public const int EmbeddingSize = 384;
        public const int Count = GoalTfidfCount + ContextTfidfCount + ScalarCount + 2 * EmbeddingSize;

        public const int ScalarsStart = GoalTfidfCount + ContextTfidfCount;
        public const int CosineIndex = ScalarsStart;
        public const int FeedFlagIndex = ScalarsStart + 5;

        private const string SecureScheme = "https://";
        private const string HttpPrefix = "http";
        private static readonly char[] WhitespaceSeparators = { ' ', '\t', '\r', '\n', '\f', '\v' };

        // Training computed the flag on the page title by substring ("home" matches "homepage"), but on titles that
        // were later rewritten: 37 of the 45 rows where today's titles disagree with the stored flag are
        // "Home page | …" titles the flag was never set for. Removing "home page"/"homepage" first leaves 8.
        private static readonly Regex HomePage = new Regex(@"home\s*page", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly string[] FeedKeywords =
        {
            "feed", "home", "timeline", "messages", "inbox", "notifications", "trending", "dashboard", "news feed", "social",
        };

        public static float[] Assemble(float[] goalTfidf, float[] contextTfidf, float[] scalars,
            float[] goalEmbedding, float[] contextEmbedding)
        {
            var parts = new[]
            {
                Checked(goalTfidf, GoalTfidfCount, nameof(goalTfidf)),
                Checked(contextTfidf, ContextTfidfCount, nameof(contextTfidf)),
                Checked(scalars, ScalarCount, nameof(scalars)),
                Checked(goalEmbedding, EmbeddingSize, nameof(goalEmbedding)),
                Checked(contextEmbedding, EmbeddingSize, nameof(contextEmbedding)),
            };
            var features = new float[Count];
            int offset = 0;
            foreach (float[] part in parts)
            {
                Array.Copy(part, 0, features, offset, part.Length);
                offset += part.Length;
            }
            if (features.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
                throw new InvalidOperationException("Relevance features contain a non-finite value.");
            return features;
        }

        /// <summary>
        /// cosine, URL path depth, URL length, has subdomain, has query, feed/messaging flag, title missing,
        /// word overlap, goal words, page words, cosine × overlap, cosine × feed flag.
        /// </summary>
        public static float[] Scalars(string goal, string context, float cosine, string url, string title)
        {
            string[] goalWords = Words(goal);
            string[] contextWords = Words(context);
            var goalSet = new HashSet<string>(goalWords);
            float overlap = goalSet.Count == 0 ? 0f : (float)goalSet.Intersect(contextWords).Count() / goalSet.Count;
            float[] urlMetadata = UrlMetadata(url);
            float feed = IsFeedOrMessaging(title, url, goal) ? 1f : 0f;
            float titleMissing = string.IsNullOrWhiteSpace(title) ? 1f : 0f;
            return new[]
            {
                cosine, urlMetadata[0], urlMetadata[1], urlMetadata[2], urlMetadata[3], feed, titleMissing,
                overlap, goalWords.Length, contextWords.Length, cosine * overlap, cosine * feed,
            };
        }

        public static bool IsFeedOrMessaging(string title, string url, string goal)
        {
            if (EmailGoal.AllowsMailPage(goal, url, title)) return false;
            string lowered = HomePage.Replace((title ?? string.Empty).ToLowerInvariant(), " ");
            return FeedKeywords.Any(lowered.Contains);
        }

        /// <summary>Path depth, length, has-subdomain and has-query of the URL; zeros without a usable URL, as in training.</summary>
        public static float[] UrlMetadata(string url)
        {
            var none = new float[4];
            if (string.IsNullOrWhiteSpace(url)) return none;
            string normalized = url.StartsWith(HttpPrefix, StringComparison.Ordinal) ? url : SecureScheme + url;
            if (!Uri.TryCreate(normalized, UriKind.Absolute, out Uri parsed)) return none;
            string[] hostParts = parsed.Host.Split('.');
            bool hasSubdomain = hostParts.Length > 2 && hostParts[0] != "www" && hostParts[0].Length > 0;
            int depth = parsed.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).Length;
            return new[] { depth, (float)normalized.Length, hasSubdomain ? 1f : 0f, parsed.Query.Length > 0 ? 1f : 0f };
        }

        public static float Cosine(float[] first, float[] second)
        {
            if (first.Length != second.Length) throw new ArgumentException("Embedding sizes differ.");
            double dot = 0, firstNorm = 0, secondNorm = 0;
            for (int i = 0; i < first.Length; i++)
            {
                dot += first[i] * second[i];
                firstNorm += first[i] * first[i];
                secondNorm += second[i] * second[i];
            }
            if (firstNorm <= 0 || secondNorm <= 0) return 0f;
            return (float)Math.Max(-1.0, Math.Min(1.0, dot / (Math.Sqrt(firstNorm) * Math.Sqrt(secondNorm))));
        }

        private static string[] Words(string text) =>
            (text ?? string.Empty).ToLowerInvariant().Split(WhitespaceSeparators, StringSplitOptions.RemoveEmptyEntries);

        private static float[] Checked(float[] part, int expected, string name)
        {
            if (part == null) throw new ArgumentNullException(name);
            if (part.Length != expected) throw new ArgumentException($"Expected {expected} values, got {part.Length}.", name);
            return part;
        }
    }
}
