using System;
using System.Collections.Generic;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// The one distraction-category taxonomy, shared by onboarding's blocked-URL tree and the
    /// block-list screen. Ported from the Mac's <c>DistractionCategory</c>.
    ///
    /// The member names are C#; the WIRE values are in <see cref="DistractionCategories.WireValue"/>
    /// and must never change — they are persisted locally and round-trip to the backend's
    /// <c>focus_mode.categories</c> column, so renaming one silently re-files every entry a user has
    /// already categorised.
    /// </summary>
    public enum DistractionCategory
    {
        SocialMedia,
        News,
        Video,
        Gaming,
        Dating,
        RealEstate,
        Porn,
        Gambling,
        Other,
    }

    public static class DistractionCategories
    {
        /// <summary>The catch-all. Never a suggestion source, and always sorts last.</summary>
        public const DistractionCategory Fallback = DistractionCategory.Other;

        /// <summary>
        /// Declaration order, which is the Mac's <c>allCases</c> order. Ordering is observable —
        /// <see cref="OrderedForFiltering"/> and the inference sweep both walk it — so it is written
        /// out rather than left to <c>Enum.GetValues</c>.
        /// </summary>
        public static readonly IReadOnlyList<DistractionCategory> All = new[]
        {
            DistractionCategory.SocialMedia,
            DistractionCategory.News,
            DistractionCategory.Video,
            DistractionCategory.Gaming,
            DistractionCategory.Dating,
            DistractionCategory.RealEstate,
            DistractionCategory.Porn,
            DistractionCategory.Gambling,
            DistractionCategory.Other,
        };

        /// <summary>Filter chips on the block-list screen, in display order with the catch-all last.</summary>
        public static IReadOnlyList<DistractionCategory> OrderedForFiltering
        {
            get
            {
                var ordered = new List<DistractionCategory>(All.Count);
                foreach (DistractionCategory category in All)
                {
                    if (category != Fallback) ordered.Add(category);
                }
                ordered.Add(Fallback);
                return ordered;
            }
        }

        /// <summary>
        /// The subset onboarding's outline tree has always shown. Onboarding keeps exactly the
        /// categories it shipped with — putting adult or betting categories on a first-run screen is
        /// a product change nobody asked for — while both screens read this one taxonomy.
        /// </summary>
        public static bool AppearsInOnboarding(DistractionCategory category)
        {
            switch (category)
            {
                case DistractionCategory.SocialMedia:
                case DistractionCategory.News:
                case DistractionCategory.Video:
                case DistractionCategory.Dating:
                case DistractionCategory.RealEstate:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The stable wire identifier. Persisted and sent to the backend; never rename one.</summary>
        public static string WireValue(DistractionCategory category)
        {
            switch (category)
            {
                case DistractionCategory.SocialMedia: return "social_media";
                case DistractionCategory.News: return "news";
                case DistractionCategory.Video: return "video";
                case DistractionCategory.Gaming: return "gaming";
                case DistractionCategory.Dating: return "dating";
                case DistractionCategory.RealEstate: return "real_estate";
                case DistractionCategory.Porn: return "porn";
                case DistractionCategory.Gambling: return "gambling";
                case DistractionCategory.Other: return "other";
                default: throw new ArgumentOutOfRangeException(nameof(category));
            }
        }

        /// <summary>
        /// Wire value back to a category. Returns false for an id this build has never heard of —
        /// a newer client or another platform may file an entry under one, and that entry should
        /// degrade to uncategorised rather than break the screen.
        /// </summary>
        public static bool TryParseWireValue(string wireValue, out DistractionCategory category)
        {
            foreach (DistractionCategory candidate in All)
            {
                if (WireValue(candidate) == wireValue) { category = candidate; return true; }
            }
            category = Fallback;
            return false;
        }
    }
}
