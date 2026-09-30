using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Stats;

namespace Focus_Bear.Focus
{
    /// <summary>
    /// The seam between the session's tracked-activity dictionary and the alignment score in
    /// FocusBear.Core, where the arithmetic lives and is tested. Kept out of
    /// <see cref="FocusAlignmentScoreManager"/> because that class's static constructor reads the
    /// AppSettings singleton, which a headless test cannot build.
    /// </summary>
    internal static class FocusAlignmentActivities
    {
        private const string ProbabilityKey = "probability";
        private const string TotalSecondsKey = "totalSeconds";
        private const string IsVisibleAppKey = "isVisibleApp";
        private const string IsVisibleUrlKey = "isVisibleUrl";
        private const string StartedTimeKey = "startedTime";

        /// <summary>The session's alignment score, 0…100, as the Mac computes it.</summary>
        internal static double Score(IEnumerable<IDictionary<string, object>> entries, DateTime now)
        {
            return FocusAlignmentScore.Calculate(From(entries), now);
        }

        /// <summary>
        /// Maps each entry, reading keys exactly as the inline calculation did. A missing key is 0 /
        /// false, as before. The one difference: a <c>startedTime</c> that is not a DateTime used to
        /// throw an InvalidCastException out of every caller; it now counts as "started now".
        /// </summary>
        internal static IList<FocusAlignmentActivity> From(IEnumerable<IDictionary<string, object>> entries)
        {
            return entries.Select(entry => new FocusAlignmentActivity
            {
                Probability = entry.TryGetValue(ProbabilityKey, out object probability) ? Convert.ToDouble(probability) : 0,
                TotalSeconds = entry.TryGetValue(TotalSecondsKey, out object seconds) ? Convert.ToInt32(seconds) : 0,
                IsVisibleApp = IsTrue(entry, IsVisibleAppKey),
                IsVisibleUrl = IsTrue(entry, IsVisibleUrlKey),
                StartedTime = entry.TryGetValue(StartedTimeKey, out object started) ? started as DateTime? : null,
            }).ToList();
        }

        private static bool IsTrue(IDictionary<string, object> entry, string key)
        {
            return entry.TryGetValue(key, out object value) && value is bool flag && flag;
        }
    }
}
