using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Focus_Bear.RelevanceClassifier
{
    internal class NativeTfidfVectorizer
    {
        private readonly Dictionary<string, int> _vocabulary;
        private readonly double[] _idf;
        private readonly bool _lowercase;
        private readonly bool _sublinearTf;
        private readonly string _norm;

        public int FeatureCount => _idf.Length;

        public NativeTfidfVectorizer(JObject data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            JObject vocabularyObject = (JObject)data["vocabulary"];
            JArray idfArray = (JArray)data["idf"];

            if (vocabularyObject == null)
                throw new InvalidOperationException(
                    "TF-IDF vocabulary is missing."
                );

            if (idfArray == null)
                throw new InvalidOperationException(
                    "TF-IDF IDF values are missing."
                );

            _vocabulary = vocabularyObject
                .Properties()
                .ToDictionary(
                    property => property.Name,
                    property => (int)property.Value
                );

            _idf = idfArray
                .Select(value => (double)value)
                .ToArray();

            _lowercase =
                data["lowercase"] != null &&
                (bool)data["lowercase"];

            _sublinearTf =
                data["sublinear_tf"] != null &&
                (bool)data["sublinear_tf"];

            _norm =
                data["norm"] != null
                    ? (string)data["norm"]
                    : "l2";

            if (_vocabulary.Count != _idf.Length)
            {
                throw new InvalidOperationException(
                    "TF-IDF vocabulary and IDF sizes do not match."
                );
            }
        }

        public float[] Transform(string text)
        {
            if (text == null)
                text = string.Empty;

            if (_lowercase)
                text = text.ToLowerInvariant();

            MatchCollection matches =
                Regex.Matches(text, @"\b\w\w+\b");

            List<string> tokens = new List<string>();

            foreach (Match match in matches)
                tokens.Add(match.Value);

            Dictionary<string, int> termCounts =
                new Dictionary<string, int>();

            // Unigrams
            foreach (string token in tokens)
                AddTerm(termCounts, token);

            // Bigrams
            for (int i = 0; i < tokens.Count - 1; i++)
            {
                string bigram =
                    tokens[i] + " " + tokens[i + 1];

                AddTerm(termCounts, bigram);
            }

            double[] vector = new double[_idf.Length];

            foreach (KeyValuePair<string, int> term in termCounts)
            {
                int index;

                if (!_vocabulary.TryGetValue(term.Key, out index))
                    continue;

                double tf = term.Value;

                if (_sublinearTf && tf > 0)
                    tf = 1.0 + Math.Log(tf);

                vector[index] = tf * _idf[index];
            }

            if (string.Equals(
                _norm,
                "l2",
                StringComparison.OrdinalIgnoreCase))
            {
                double sumSquares = 0.0;

                for (int i = 0; i < vector.Length; i++)
                    sumSquares += vector[i] * vector[i];

                double magnitude = Math.Sqrt(sumSquares);

                if (magnitude > 0.0)
                {
                    for (int i = 0; i < vector.Length; i++)
                        vector[i] /= magnitude;
                }
            }

            float[] result = new float[vector.Length];

            for (int i = 0; i < vector.Length; i++)
                result[i] = (float)vector[i];

            return result;
        }

        private static void AddTerm(
            Dictionary<string, int> counts,
            string term)
        {
            int currentCount;

            if (counts.TryGetValue(term, out currentCount))
                counts[term] = currentCount + 1;
            else
                counts[term] = 1;
        }
    }
}