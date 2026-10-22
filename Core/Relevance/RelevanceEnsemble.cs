using System;

namespace FocusBear.Core.Relevance
{
    public enum RelevanceTier
    {
        Block = 0,
        Caution = 1,
        Allow = 2,
    }

    /// <summary>The sklearn soft vote of the shared model: logistic regression 1, XGBoost 2, tuned XGBoost 2.</summary>
    public static class RelevanceEnsemble
    {
        public const int ClassCount = 3;
        public const float LogisticRegressionWeight = 1f;
        public const float XGBoostWeight = 2f;
        public const float XGBoostTunedWeight = 2f;
        public const float ConfidenceGate = 0.70f;
        private const double ProbabilitySumTolerance = 0.001;

        public static float[] Vote(float[] logisticRegression, float[] xgboost, float[] xgboostTuned)
        {
            Validate(logisticRegression, nameof(logisticRegression));
            Validate(xgboost, nameof(xgboost));
            Validate(xgboostTuned, nameof(xgboostTuned));
            const float totalWeight = LogisticRegressionWeight + XGBoostWeight + XGBoostTunedWeight;
            var combined = new float[ClassCount];
            for (int i = 0; i < ClassCount; i++)
            {
                combined[i] = (LogisticRegressionWeight * logisticRegression[i] + XGBoostWeight * xgboost[i] +
                    XGBoostTunedWeight * xgboostTuned[i]) / totalWeight;
            }
            return combined;
        }

        /// <summary>
        /// Training fed the trees scipy CSR rows with no stored zeros, so an absent entry takes each split's learned
        /// missing-value branch. A dense zero would not; NaN does.
        /// </summary>
        public static float[] ZerosAsMissing(float[] features)
        {
            var trees = (float[])features.Clone();
            for (int i = 0; i < trees.Length; i++)
            {
                if (trees[i] == 0f) trees[i] = float.NaN;
            }
            return trees;
        }

        public static RelevanceTier MostLikely(float[] probabilities)
        {
            Validate(probabilities, nameof(probabilities));
            int best = 0;
            for (int i = 1; i < ClassCount; i++)
            {
                if (probabilities[i] > probabilities[best]) best = i;
            }
            return (RelevanceTier)best;
        }

        private static void Validate(float[] values, string name)
        {
            if (values == null || values.Length != ClassCount)
                throw new InvalidOperationException(name + " must return three probabilities.");
            double sum = 0;
            foreach (float value in values)
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 1)
                    throw new InvalidOperationException(name + " returned an invalid probability.");
                sum += value;
            }
            if (Math.Abs(sum - 1.0) > ProbabilitySumTolerance)
                throw new InvalidOperationException(name + " probabilities do not sum to one.");
        }
    }
}
