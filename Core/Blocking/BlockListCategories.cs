using System;
using System.Collections.Generic;

namespace FocusBear.Core.Blocking
{
    /// <summary>Keys inside one <c>categories</c> element. The wire format shared with the backend.</summary>
    public static class FocusModeCategoryKey
    {
        public const string Entry = "entry";
        public const string Category = "category";
    }

    /// <summary>Keys inside a focus-mode dictionary that the block-list screen reads or writes.</summary>
    public static class FocusModeKey
    {
        public const string Id = "id";
        public const string Name = "name";
        public const string IsAllowedListApproach = "isAllowedListApproach";
        public const string AllowedUrls = "allowed_urls";
        public const string AllowedApps = "allowed_apps";
        public const string BlockUrlsArray = "blockUrlsArray";
        public const string BlockAppsArray = "blockAppsArray";

        /// <summary>
        /// Per-entry categories, <c>[{"entry": …, "category": …}]</c>. Mirrors the backend's
        /// <c>categories</c> column (backend#2400) and is sent TOP-LEVEL, not inside <c>metadata</c>.
        /// </summary>
        public const string Categories = "categories";
    }

    /// <summary>
    /// The category half of the Mac's <c>BlockListStore</c>: decoding what is stored, encoding what
    /// to store, pruning what no longer applies, and keeping it out of the metadata blob.
    ///
    /// The screen itself, the focus-mode globals and the network sync are not ported — those need
    /// UserDefaults, <c>arrFocusType</c> and the API client. What is here is every rule that decides
    /// what a category payload contains.
    /// </summary>
    public static class BlockListCategories
    {
        /// <summary>
        /// Decodes the stored wire shape into an entry → category map.
        ///
        /// An element with no entry, an empty entry, or a category id this build has never heard of
        /// is DROPPED rather than treated as an error: a newer client or another platform may file an
        /// entry under a future category, and that entry should degrade to uncategorised instead of
        /// breaking the whole decode and blanking the screen.
        /// </summary>
        public static IDictionary<string, DistractionCategory> AssignedCategories(
            IEnumerable<IDictionary<string, string>> stored)
        {
            var result = new Dictionary<string, DistractionCategory>(StringComparer.Ordinal);
            if (stored == null) return result;

            foreach (IDictionary<string, string> item in stored)
            {
                if (item == null) continue;
                string entry;
                if (!item.TryGetValue(FocusModeCategoryKey.Entry, out entry)) continue;
                if (string.IsNullOrEmpty(entry)) continue;

                string rawCategory;
                if (!item.TryGetValue(FocusModeCategoryKey.Category, out rawCategory)) continue;

                DistractionCategory category;
                if (!DistractionCategories.TryParseWireValue(rawCategory, out category)) continue;
                result[entry] = category;
            }
            return result;
        }

        /// <summary>
        /// Encodes the map back to the wire shape, SORTED BY ENTRY so the payload is stable between
        /// saves and an unrelated edit cannot churn the server row.
        /// </summary>
        public static IReadOnlyList<IDictionary<string, string>> EncodeCategories(
            IDictionary<string, DistractionCategory> assigned)
        {
            var encoded = new List<IDictionary<string, string>>();
            if (assigned == null) return encoded;

            var entries = new List<string>(assigned.Keys);
            // Sorted as the Mac's `keys.sorted()` sorts, so a save from either client writes the same
            // payload and the row does not churn between them.
            entries.Sort(SwiftStringComparer.Instance);
            foreach (string entry in entries)
            {
                encoded.Add(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { FocusModeCategoryKey.Entry, entry },
                    { FocusModeCategoryKey.Category, DistractionCategories.WireValue(assigned[entry]) },
                });
            }
            return encoded;
        }

        /// <summary>
        /// Drops categories whose entry is no longer in the block list.
        ///
        /// Removing an entry through the block-list screen already prunes its category, but an older
        /// client can delete a blocked URL without knowing categories exist, stranding the row.
        /// Pruning on the next write is what stops the stored list only ever growing.
        /// </summary>
        public static IDictionary<string, DistractionCategory> PruneOrphanedCategories(
            IDictionary<string, DistractionCategory> assigned, IEnumerable<string> liveEntries)
        {
            var kept = new Dictionary<string, DistractionCategory>(StringComparer.Ordinal);
            if (assigned == null || assigned.Count == 0) return kept;

            var live = new HashSet<string>(StringComparer.Ordinal);
            if (liveEntries != null)
            {
                foreach (string entry in liveEntries) if (entry != null) live.Add(entry);
            }

            foreach (KeyValuePair<string, DistractionCategory> pair in assigned)
            {
                if (live.Contains(pair.Key)) kept.Add(pair.Key, pair.Value);
            }
            return kept;
        }

        /// <summary>
        /// Promotes a focus mode's categories out of the <c>metadata</c> catch-all and onto the top
        /// level of a focus-mode request body.
        ///
        /// Every focus-mode write builds <c>{name, allowed_*, tags, metadata: &lt;every other key&gt;}</c>.
        /// Categories must NOT ride inside <c>metadata</c>: that blob is replaced wholesale on each
        /// save, so a client that predates categories would silently wipe them. The backend gives
        /// them their own field (backend#2400), where an omitted value is preserved rather than
        /// emptied — hence a focus mode with none sends an empty list, not nothing.
        /// </summary>
        public static void ApplyFocusModeCategories(IDictionary<string, object> focusType,
                                                    IDictionary<string, object> json,
                                                    IDictionary<string, object> metadata)
        {
            if (focusType == null) throw new ArgumentNullException(nameof(focusType));
            if (json == null) throw new ArgumentNullException(nameof(json));
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));

            metadata.Remove(FocusModeKey.Categories);

            object stored;
            focusType.TryGetValue(FocusModeKey.Categories, out stored);
            json[FocusModeKey.Categories] = stored as IReadOnlyList<IDictionary<string, string>>
                                            ?? (object)new List<IDictionary<string, string>>();
        }

        /// <summary>
        /// A leading emoji in a block-list name, for the card's own emoji tile — or <c>null</c> when
        /// the name just starts with a letter.
        ///
        /// The Unicode <c>isEmoji</c> property alone is true for plain digits and for <c>#</c>/<c>*</c>
        /// (they have emoji presentation sequences), which would make "1Password" an emoji-led name.
        /// So the test is: the first code point has emoji PRESENTATION, or the cluster spans more
        /// than one code point (a flag, a keycap, a ZWJ family) — and some code point in it is an
        /// emoji at all. The whole grapheme cluster is returned so those stay intact.
        /// </summary>
        public static string LeadingEmoji(string name)
        {
            if (name == null) return null;
            string trimmed = name.Trim();
            if (trimmed.Length == 0) return null;

            IReadOnlyList<int> cluster = EmojiPresentation.LeadingClusterCodePoints(trimmed);
            if (cluster.Count == 0) return null;

            bool looksLikeEmoji = EmojiPresentation.HasEmojiPresentation(cluster[0]) || cluster.Count > 1;
            if (!looksLikeEmoji) return null;

            bool carriesEmoji = false;
            foreach (int codePoint in cluster)
            {
                if (EmojiPresentation.IsEmoji(codePoint)) { carriesEmoji = true; break; }
            }
            if (!carriesEmoji) return null;

            return EmojiPresentation.ClusterText(trimmed, cluster);
        }
    }
}
