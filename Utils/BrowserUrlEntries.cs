using System;
using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Blocking;

namespace Focus_Bear.Utils
{
    /// <summary>Which part of the page an entry matched, so callers can log the reason.</summary>
    public enum BrowserUrlEntryMatch
    {
        None,
        Url,
        WindowTitle,
    }

    /// <summary>
    /// The seam between the browser URL checks in <see cref="ProcessBrowserURLs"/> and FocusBear.Core.
    /// <para>
    /// The URL rule is <see cref="UrlValidation.IsUrlValid"/>, the Mac's matcher for every allow and
    /// block list, and it lives in Core where it is tested. This class adds only what Windows has and
    /// Core does not:
    /// </para>
    /// <list type="bullet">
    /// <item>Entries are trimmed of trailing slashes first. Windows always did that, and Core does
    ///       not, so without it an entry saved as "youtube.com/" would stop matching "youtube.com".</item>
    /// <item>A window-title fallback: an entry also matches when it appears in the browser window's
    ///       title. Titles are matched case-sensitively, as before; only URLs are case-folded.</item>
    /// </list>
    /// </summary>
    public static class BrowserUrlEntries
    {
        /// <summary>
        /// Stripped from an entry before its trailing slashes, so "https://" alone becomes empty and
        /// keeps matching every page, as it always has on Windows. Core strips these again after
        /// lower-casing; doing it here first is what makes the slash trim see the same text it used to.
        /// </summary>
        private static readonly string[] StrippedEntryPrefixes = { "http://", "https://", "www." };

        private const char PathSeparator = '/';

        /// <summary>
        /// Whether any entry matches <paramref name="url"/>, or failing that the window title.
        /// </summary>
        /// <param name="readWindowTitle">
        /// Null skips the title fallback (the always-blocked list never had one). Called at most once,
        /// and only when no entry matched the URL, because reading a title is a Win32 call.
        /// </param>
        public static BrowserUrlEntryMatch Match(string url, IEnumerable<string> entries, Func<string> readWindowTitle = null)
        {
            if (url == null || entries == null) return BrowserUrlEntryMatch.None;

            List<string> needles = entries.Where(entry => entry != null).Select(ToNeedle).ToList();
            if (needles.Any(needle => UrlValidation.IsUrlValid(url, needle))) return BrowserUrlEntryMatch.Url;

            if (readWindowTitle == null) return BrowserUrlEntryMatch.None;
            string title = readWindowTitle();
            if (string.IsNullOrEmpty(title)) return BrowserUrlEntryMatch.None;

            return needles.Any(needle => TitleContains(title, needle))
                ? BrowserUrlEntryMatch.WindowTitle
                : BrowserUrlEntryMatch.None;
        }

        internal static string ToNeedle(string entry)
        {
            string needle = entry;
            foreach (string prefix in StrippedEntryPrefixes)
            {
                needle = needle.Replace(prefix, string.Empty);
            }
            return needle.TrimEnd(PathSeparator);
        }

        private static bool TitleContains(string title, string needle)
        {
            return WildcardPattern.Matches(title, WildcardPattern.AnySequence + needle + WildcardPattern.AnySequence);
        }
    }
}
