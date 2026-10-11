using System;
using System.Collections.Generic;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// The seed domains behind each category: what onboarding offers as pre-ticked suggestions, and
    /// what classifies an entry the user never filed by hand. Ported from the Mac's
    /// <c>DistractionCategoryCatalog</c>, whose table was itself lifted out of
    /// <c>onboardingBlockedUrlsVc</c> so the two screens could not drift apart.
    /// </summary>
    public static class DistractionCategoryCatalog
    {
        /// <summary>The seed onboarding drops for students, who legitimately study on YouTube.</summary>
        public const string StudentExcludedSeed = "youtube.com";

        /// <summary>
        /// Shortest entry worth matching by substring. A one- or two-character entry ("x") would
        /// otherwise match x.com and half the catalog, so it is left uncategorised instead.
        /// </summary>
        private const int MinimumMatchLength = 3;

        private static readonly string[] None = new string[0];

        public static IReadOnlyList<string> SeedEntries(DistractionCategory category)
        {
            switch (category)
            {
                case DistractionCategory.SocialMedia:
                    return new[] { "facebook.com", "x.com", "instagram.com", "tiktok.com", "youtube.com",
                                   "reddit.com", "www.linkedin.com/feed/" };
                case DistractionCategory.News:
                    return new[] { "bbc.com", "cnn.com", "theguardian.com", "abc.net.au", "news", "nytimes.com",
                                   "news.com.au", "smh.com.au", "foxnews.com" };
                case DistractionCategory.Video:
                    return new[] { "netflix", "disney", "hulu" };
                case DistractionCategory.Dating:
                    return new[] { "tinder.com", "bumble.com", "hinge.co", "match.com", "okcupid.com", "pof.com",
                                   "eharmony.com", "grindr.com" };
                case DistractionCategory.RealEstate:
                    return new[] { "realestate.com.au", "domain.com.au", "zillow.com", "realtor.com", "rightmove.co.uk" };
                case DistractionCategory.Gaming:
                    return new[] { "twitch.tv", "steampowered.com", "roblox.com", "epicgames.com", "chess.com" };
                case DistractionCategory.Porn:
                case DistractionCategory.Gambling:
                    // Deliberately empty: these exist so a user can file an entry under them, but
                    // Focus Bear does not ship a list of adult or betting domains.
                    return None;
                case DistractionCategory.Other:
                    return None;
                default:
                    throw new ArgumentOutOfRangeException(nameof(category));
            }
        }

        /// <summary>
        /// Best-effort category for an entry the user has not filed by hand, or <c>null</c> when
        /// nothing fits — the caller falls back to <see cref="DistractionCategories.Fallback"/>.
        ///
        /// Matching is substring-based in BOTH directions because a block entry may be a bare keyword
        /// ("netflix") or a full URL ("www.instagram.com/explore"), and the seeds are a mix of both.
        /// </summary>
        public static DistractionCategory? InferredCategory(string entry)
        {
            if (entry == null) return null;
            string needle = entry.ToLowerInvariant();
            if (needle.Length < MinimumMatchLength) return null;

            foreach (DistractionCategory category in DistractionCategories.OrderedForFiltering)
            {
                if (category == DistractionCategories.Fallback) continue;
                foreach (string seed in SeedEntries(category))
                {
                    string seedLower = seed.ToLowerInvariant();
                    if (needle.IndexOf(seedLower, StringComparison.Ordinal) >= 0
                        || seedLower.IndexOf(needle, StringComparison.Ordinal) >= 0)
                    {
                        return category;
                    }
                }
            }
            return null;
        }
    }
}
