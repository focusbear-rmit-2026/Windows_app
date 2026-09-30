using System;
using System.Collections.Generic;

namespace FocusBear.Core.Stats
{
    /// <summary>
    /// One tracked app or site inside the running focus session, as the alignment score sees it.
    /// Mirrors an entry of the Mac's <c>dictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus</c>.
    /// </summary>
    public sealed class FocusAlignmentActivity
    {
        /// <summary>The relevance classifier's 0…1 verdict for this app/site against the intention.</summary>
        public double Probability { get; set; }

        /// <summary>Seconds already banked against this activity.</summary>
        public int TotalSeconds { get; set; }

        /// <summary>This app is on screen right now, so time since <see cref="StartedTime"/> still counts.</summary>
        public bool IsVisibleApp { get; set; }

        /// <summary>This site is the foreground tab right now.</summary>
        public bool IsVisibleUrl { get; set; }

        /// <summary>When the still-visible activity came to the front.</summary>
        public DateTime? StartedTime { get; set; }
    }

    /// <summary>
    /// The focus alignment score: how much of the session's time was spent on things the relevance
    /// classifier judged to belong to the stated intention, as a 0…100 percentage.
    ///
    /// Port of the Mac's <c>calculateFocusAlignmentScore()</c> (Focus-Bear/Mac-App#1628), minus the
    /// globals it writes and the nudge window it may open — this is only the arithmetic.
    /// </summary>
    public static class FocusAlignmentScore
    {
        /// <summary>A session with nothing tracked yet is assumed perfectly aligned.</summary>
        public const double DefaultScore = 100.0;

        public const double MaximumScore = 100.0;

        /// <summary>The score is reported to two decimal places, matching what the Mac stores and sends.</summary>
        private const double TwoDecimalPlacesScale = 100.0;

        /// <summary>
        /// The time-weighted alignment score for the tracked activities, 0…100.
        ///
        /// Deliberately keeps the Mac's exact shape, including the two edge cases it leans on:
        /// a score of 0 skips the division entirely (all-unaligned time stays 0 rather than
        /// becoming NaN), and the empty case sets the score to 100 and then divides it by zero
        /// seconds — positive infinity, which the cap immediately pulls back to 100. Both clients
        /// therefore report 100 for a session with no tracked activity, and it is worth knowing
        /// that the Mac arrives there through infinity rather than through a guard.
        /// </summary>
        public static double Calculate(IEnumerable<FocusAlignmentActivity> activities, DateTime now)
        {
            if (activities == null)
            {
                throw new ArgumentNullException(nameof(activities));
            }

            double totalAlignmentScore = 0;
            int totalSeconds = 0;
            int activityCount = 0;

            foreach (FocusAlignmentActivity activity in activities)
            {
                activityCount++;
                int seconds = activity.TotalSeconds + VisibleSecondsSince(activity, now);
                totalAlignmentScore += seconds * activity.Probability;
                totalSeconds += seconds;
            }

            if (activityCount == 0)
            {
                totalAlignmentScore = DefaultScore;
            }

            if (totalAlignmentScore != 0)
            {
                totalAlignmentScore = totalAlignmentScore / totalSeconds * 100;
            }

            if (totalAlignmentScore > MaximumScore)
            {
                totalAlignmentScore = MaximumScore;
            }

            return SwiftRounding.Rounded(totalAlignmentScore * TwoDecimalPlacesScale) / TwoDecimalPlacesScale;
        }

        /// <summary>
        /// Time the activity has been on screen since it came to the front, which has not been banked
        /// into <see cref="FocusAlignmentActivity.TotalSeconds"/> yet. Only a visible app or tab
        /// accrues it — a background one stopped consuming the user's attention.
        /// </summary>
        private static int VisibleSecondsSince(FocusAlignmentActivity activity, DateTime now)
        {
            if (!activity.IsVisibleApp && !activity.IsVisibleUrl)
            {
                return 0;
            }

            DateTime startedTime = activity.StartedTime ?? now;
            return (int)(now - startedTime).TotalSeconds;
        }
    }
}
