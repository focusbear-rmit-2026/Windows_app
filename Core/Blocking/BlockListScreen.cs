using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.Blocking
{
    /// <summary>Whether a block-list entry is a website (URL or keyword) or an application.</summary>
    public enum BlockListEntryType
    {
        Website,
        App,
    }

    /// <summary>One row of the block-list screen's table.</summary>
    public sealed class BlockListEntry
    {
        public BlockListEntry(string value, BlockListEntryType type, DistractionCategory category, bool isAllowed)
        {
            Value = value ?? "";
            Type = type;
            Category = category;
            IsAllowed = isAllowed;
        }

        /// <summary>The stored string: a URL/keyword for a website, the app's name for an app.</summary>
        public string Value { get; }
        public BlockListEntryType Type { get; }
        /// <summary>The category the entry is shown under. Inferred from the catalog; nothing is stored.</summary>
        public DistractionCategory Category { get; }
        /// <summary>
        /// True when the entry sits on an ALLOW list (an allow-list block list's websites, or a block
        /// list's "allow selected apps" list) rather than a block list.
        /// </summary>
        public bool IsAllowed { get; }

        public string DisplayName => Value;

        /// <summary>The leading letter drawn in the row's avatar tile, per the design's favicon placeholder.</summary>
        public string AvatarLetter
        {
            get
            {
                string trimmed = DisplayName.Trim();
                if (trimmed.Length == 0) return "?";
                int length = char.IsHighSurrogate(trimmed[0]) && trimmed.Length > 1 ? 2 : 1;
                return trimmed.Substring(0, length).ToUpperInvariant();
            }
        }
    }

    /// <summary>The flags of one focus mode ("block list") that decide what the screen offers for it.</summary>
    public sealed class BlockListModeFlags
    {
        public bool IsDefault { get; set; }
        public bool IsMeetings { get; set; }
        public bool IsLockedFocus { get; set; }
        public bool IsRelaxFocus { get; set; }
        public bool IsDeepWorkFocus { get; set; }
        /// <summary>Websites use an allow list ("block everything except…") instead of a block list.</summary>
        public bool IsAllowedListApproach { get; set; }
        /// <summary>Every app is allowed; when false only the listed apps are.</summary>
        public bool IsAllowedAllApps { get; set; }
    }

    /// <summary>The explanatory line the screen shows above a block list's table.</summary>
    public enum BlockListNotice
    {
        None,
        /// <summary>A relax ("free time") mode: nothing is blocked and there is nothing to edit.</summary>
        AllWebsitesAllowed,
        /// <summary>The default mode edits the app-wide Always Blocked list.</summary>
        AlwaysBlocked,
        /// <summary>Both websites and apps are allow lists.</summary>
        AllowListEverything,
        AllowListWebsites,
        AllowListApps,
    }

    public enum BlockListRenameOutcome
    {
        Renamed,
        Unchanged,
        Empty,
        Duplicate,
    }

    public enum BlockListAddOutcome
    {
        Added,
        Empty,
        Duplicate,
    }

    /// <summary>
    /// The rules behind the block-list screen, kept apart from WPF. They are the Windows app's existing
    /// focus-mode editor rules (what may be deleted, renamed, which lists a mode edits), laid out the way
    /// the Mac's redesigned screen presents them.
    /// </summary>
    public static class BlockListRules
    {
        /// <summary>The longest name a rename keeps (the old name box's MaxLength).</summary>
        public const int MaxRenameLength = 45;
        /// <summary>The longest name a new block list keeps.</summary>
        public const int MaxNewNameLength = 250;
        /// <summary>How many "commonly blocked" suggestion chips are offered at once.</summary>
        public const int MaxSuggestions = 4;

        /// <summary>The built-in modes keep a fixed name and cannot be deleted.</summary>
        public static bool IsBuiltIn(BlockListModeFlags mode) =>
            mode.IsDefault || mode.IsMeetings || mode.IsLockedFocus || mode.IsRelaxFocus || mode.IsDeepWorkFocus;

        public static bool CanDelete(BlockListModeFlags mode) => !IsBuiltIn(mode);

        public static bool CanRename(BlockListModeFlags mode) => !IsBuiltIn(mode);

        /// <summary>
        /// The default mode edits the app-wide Always Blocked list, which a copy would not carry, so it
        /// is not offered for duplication (the Mac hides it too).
        /// </summary>
        public static bool CanDuplicate(BlockListModeFlags mode) => !mode.IsDefault && !mode.IsLockedFocus;

        /// <summary>The Locked Focus pseudo-mode never appears in the list.</summary>
        public static bool ShowsInList(BlockListModeFlags mode) => !mode.IsLockedFocus;

        /// <summary>A relax mode blocks nothing, so it has no entries to add or remove.</summary>
        public static bool CanEditEntries(BlockListModeFlags mode) => !mode.IsRelaxFocus;

        /// <summary>The website and app approach choices (block list / allow list, all / selected apps).</summary>
        public static bool ShowsApproachChoices(BlockListModeFlags mode) => !mode.IsDefault && !mode.IsRelaxFocus;

        /// <summary>
        /// The Advanced sheet edits the URLs blocked INSIDE an allow list, so it only means something for
        /// an allow-list mode — and never for the default mode.
        /// </summary>
        public static bool AdvancedAvailable(BlockListModeFlags mode) =>
            !mode.IsDefault && !mode.IsRelaxFocus && mode.IsAllowedListApproach;

        /// <summary>Whether the listed websites are allowed (an allow list) rather than blocked.</summary>
        public static bool WebsitesAreAllowed(BlockListModeFlags mode) =>
            !mode.IsDefault && !mode.IsRelaxFocus && mode.IsAllowedListApproach;

        /// <summary>
        /// Whether the mode has an app list at all: the default mode's apps-to-block, or another mode's
        /// "allow selected apps" list. "Allow all apps" has none.
        /// </summary>
        public static bool ShowsApps(BlockListModeFlags mode) =>
            !mode.IsRelaxFocus && (mode.IsDefault || !mode.IsAllowedAllApps);

        /// <summary>The default mode's app list blocks; every other mode's app list allows.</summary>
        public static bool AppsAreAllowed(BlockListModeFlags mode) => !mode.IsDefault;

        public static BlockListNotice Notice(BlockListModeFlags mode)
        {
            if (mode.IsRelaxFocus) return BlockListNotice.AllWebsitesAllowed;
            if (mode.IsDefault) return BlockListNotice.AlwaysBlocked;
            bool websites = WebsitesAreAllowed(mode);
            bool apps = ShowsApps(mode) && AppsAreAllowed(mode);
            if (websites && apps) return BlockListNotice.AllowListEverything;
            if (websites) return BlockListNotice.AllowListWebsites;
            if (apps) return BlockListNotice.AllowListApps;
            return BlockListNotice.None;
        }

        /// <summary>
        /// Suggestions are distracting sites, so they are only offered while a real category chip is
        /// active, and only where adding one BLOCKS it — never onto an allow list.
        /// </summary>
        public static bool OffersSuggestions(BlockListModeFlags mode, DistractionCategory? category) =>
            category.HasValue && CanEditEntries(mode) && !WebsitesAreAllowed(mode);

        /// <summary>
        /// Validates a rename. The name is trimmed and cut to <see cref="MaxRenameLength"/>; it must be
        /// non-empty and must not match another block list's name, ignoring case.
        /// </summary>
        public static BlockListRenameOutcome ValidateRename(string rawName, string currentName,
                                                            IEnumerable<string> otherNames, out string name)
        {
            name = (rawName ?? "").Trim();
            if (name.Length == 0) return BlockListRenameOutcome.Empty;
            if (name.Length > MaxRenameLength) name = name.Substring(0, MaxRenameLength);
            if (string.Equals(name, currentName, StringComparison.Ordinal)) return BlockListRenameOutcome.Unchanged;
            string candidate = name;
            bool clashes = (otherNames ?? Enumerable.Empty<string>())
                .Any(other => string.Equals(other ?? "", candidate, StringComparison.OrdinalIgnoreCase));
            return clashes ? BlockListRenameOutcome.Duplicate : BlockListRenameOutcome.Renamed;
        }

        /// <summary>"Name 1", "Name 2", … — the first that no existing block list uses (ignoring case).</summary>
        public static string DuplicateName(string originalName, IEnumerable<string> existingNames)
        {
            var taken = new HashSet<string>((existingNames ?? Enumerable.Empty<string>()).Select(n => (n ?? "").ToLowerInvariant()));
            string original = originalName ?? "";
            for (int count = 1; ; count++)
            {
                string candidate = original + " " + count;
                if (!taken.Contains(candidate.ToLowerInvariant())) return candidate;
            }
        }

        /// <summary>A new block list's name, cut to <see cref="MaxNewNameLength"/>.</summary>
        public static string NewBlockListName(string rawName)
        {
            string name = rawName ?? "";
            return name.Length > MaxNewNameLength ? name.Substring(0, MaxNewNameLength) : name;
        }

        /// <summary>
        /// Validates a website typed into the add field: trimmed, non-empty, and not already in the list
        /// it is going into (an exact match, as the old editor checked).
        /// </summary>
        public static BlockListAddOutcome ValidateNewWebsite(string rawValue, IEnumerable<string> existing, out string value)
        {
            value = (rawValue ?? "").Trim();
            if (value.Length == 0) return BlockListAddOutcome.Empty;
            string candidate = value;
            return (existing ?? Enumerable.Empty<string>()).Any(e => e == candidate)
                ? BlockListAddOutcome.Duplicate
                : BlockListAddOutcome.Added;
        }
    }

    /// <summary>Builds and filters the rows of the block-list table.</summary>
    public static class BlockListEntries
    {
        /// <summary>The category an entry is shown under: the catalog's guess, or the catch-all.</summary>
        public static DistractionCategory CategoryFor(string value) =>
            DistractionCategoryCatalog.InferredCategory(value) ?? DistractionCategories.Fallback;

        /// <summary>Websites first, then apps, each in stored order.</summary>
        public static IReadOnlyList<BlockListEntry> Build(IEnumerable<string> websites, bool websitesAllowed,
                                                          IEnumerable<string> apps, bool appsAllowed)
        {
            var entries = new List<BlockListEntry>();
            foreach (string url in websites ?? Enumerable.Empty<string>())
            {
                if (url == null) continue;
                entries.Add(new BlockListEntry(url, BlockListEntryType.Website, CategoryFor(url), websitesAllowed));
            }
            foreach (string app in apps ?? Enumerable.Empty<string>())
            {
                if (app == null) continue;
                entries.Add(new BlockListEntry(app, BlockListEntryType.App, CategoryFor(app), appsAllowed));
            }
            return entries;
        }

        /// <summary>Entries after the search box and the active category chip (<c>null</c> = All) are applied.</summary>
        public static IReadOnlyList<BlockListEntry> Visible(IEnumerable<BlockListEntry> entries, DistractionCategory? category, string search)
        {
            string needle = (search ?? "").Trim().ToLowerInvariant();
            return (entries ?? Enumerable.Empty<BlockListEntry>())
                .Where(e => (!category.HasValue || e.Category == category.Value)
                            && (needle.Length == 0 || e.DisplayName.ToLowerInvariant().Contains(needle)))
                .ToList();
        }

        /// <summary>How many entries sit in a category (<c>null</c> = all of them), for the chip counts.</summary>
        public static int Count(IEnumerable<BlockListEntry> entries, DistractionCategory? category) =>
            (entries ?? Enumerable.Empty<BlockListEntry>()).Count(e => !category.HasValue || e.Category == category.Value);

        /// <summary>The category's seed domains this list does not already contain — the one-tap chips.</summary>
        public static IReadOnlyList<string> Suggestions(IEnumerable<BlockListEntry> entries, DistractionCategory? category,
                                                        int limit = BlockListRules.MaxSuggestions)
        {
            if (!category.HasValue) return new string[0];
            var existing = new HashSet<string>((entries ?? Enumerable.Empty<BlockListEntry>()).Select(e => e.Value.ToLowerInvariant()));
            return DistractionCategoryCatalog.SeedEntries(category.Value)
                .Where(seed => !existing.Contains(seed.ToLowerInvariant()))
                .Take(Math.Max(0, limit))
                .ToList();
        }

        /// <summary>
        /// Fills a "&lt;COUNT&gt; blocks" template for a card's badge, using the singular text for one.
        /// </summary>
        public static string BlocksBadge(int count, string pluralTemplate, string singular)
        {
            if (count == 1) return singular ?? "1 block";
            return (pluralTemplate ?? "<COUNT> blocks").Replace("<COUNT>", count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
