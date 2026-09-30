using System;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// The Mac app's <c>urlValidation</c> struct — the matcher behind every website block and allow
    /// decision. Ported rule for rule, including the parts that are more permissive than they look:
    /// a port that tightened them would block pages the Mac lets through (and vice versa), which is
    /// a behaviour difference between two clients enforcing one user's block list.
    ///
    /// Both entry points normalise the same way — lower-case, drop the scheme, drop <c>www.</c> —
    /// and then differ in how they anchor the pattern:
    ///
    /// <list type="bullet">
    /// <item><see cref="IsUrlValid"/> wraps the entry in <c>*</c>…<c>*</c>, so it is a plain
    ///       substring test. "facebook.com" therefore matches "m.facebook.com" (wanted: a subdomain
    ///       of a blocked site is blocked) and equally "notfacebook.com" and
    ///       "safe.example/?u=facebook.com" (not wanted, but shipped).</item>
    /// <item><see cref="IsUrlAlwaysBlocked"/> anchors a dotted entry at the START
    ///       (<c>entry*</c>), so "facebook.com" does NOT match "m.facebook.com" there. The two
    ///       functions genuinely disagree about subdomains; see the remarks on each.</item>
    /// </list>
    /// </summary>
    public static class UrlValidation
    {
        private const string HttpScheme = "http://";
        private const string HttpsScheme = "https://";
        private const string FileScheme = "file://";
        private const string SchemeSeparator = "://";
        private const string WwwPrefix = "www.";

        /// <summary>
        /// The one entry that carries a hand-written exception, from Mac-App#1209 ("Improve x.com in
        /// blocked list"): as a bare substring "x.com" matched netflix.com, max.com and every other
        /// host ending in "x.com".
        /// </summary>
        private const string XComEntry = "x.com";

        /// <summary>
        /// Whether <paramref name="urlToCheck"/> matches a single allow-list or block-list entry.
        ///
        /// Despite the name this is the matcher for BOTH lists — <c>URLBlockingHelper</c> calls it
        /// for the always-blocked list as well as the allowed list, so "valid" here means only
        /// "matches this entry".
        /// </summary>
        public static bool IsUrlValid(string urlToCheck, string allowUrl)
        {
            if (urlToCheck == null) throw new ArgumentNullException(nameof(urlToCheck));
            if (allowUrl == null) throw new ArgumentNullException(nameof(allowUrl));

            string normalizedEntry = Normalize(allowUrl);
            string normalizedUrl = Normalize(urlToCheck);

            if (normalizedEntry.IndexOf(FileScheme, StringComparison.Ordinal) >= 0)
            {
                return MatchesFileEntry(urlToCheck, normalizedEntry);
            }

            // Substring match: the entry may appear anywhere in the URL, host or path alike.
            bool matches = WildcardPattern.Matches(
                normalizedUrl,
                WildcardPattern.AnySequence + normalizedEntry + WildcardPattern.AnySequence);

            if (matches && normalizedEntry == XComEntry && normalizedUrl.IndexOf(XComEntry, StringComparison.Ordinal) >= 0)
            {
                // #1209: only reject when a LETTER runs into it, which is what makes "netflix.com"
                // a false positive and leaves "a.x.com" (a real subdomain) matching. It also leaves
                // "x.company" matching, because nothing precedes the match there at all.
                if (HasLetterBefore(XComEntry, normalizedUrl)) matches = false;
            }

            return matches;
        }

        /// <summary>
        /// The Mac's <c>isUrlAlwaysBlocked</c>, added by #1233 ("Change all suggested blocked apps in
        /// onboarding to start with https://").
        ///
        /// A dotted entry is treated as a domain and anchored at the start of the normalised URL, so
        /// "facebook.com" blocks "facebook.com/feed" but NOT "m.facebook.com". A dotless entry is
        /// treated as a keyword and matched anywhere.
        ///
        /// Two consequences worth knowing before relying on it: anchoring at the start still admits a
        /// lookalike SUFFIX ("facebook.com.evil.example" matches "facebook.com"), and an embedded
        /// credential defeats it entirely, because "user:pw@facebook.com/" does not START with the
        /// entry. Both are the Mac's behaviour today, both are reproduced here, and both are why
        /// <see cref="ManagedResource"/> exists for the managed-policy path.
        /// </summary>
        public static bool IsUrlAlwaysBlocked(string urlToCheck, string blockedUrl)
        {
            if (urlToCheck == null) throw new ArgumentNullException(nameof(urlToCheck));
            if (blockedUrl == null) throw new ArgumentNullException(nameof(blockedUrl));

            string normalizedEntry = Normalize(blockedUrl);
            string normalizedUrl = Normalize(urlToCheck);

            if (normalizedEntry.IndexOf(FileScheme, StringComparison.Ordinal) >= 0)
            {
                return MatchesFileEntry(urlToCheck, normalizedEntry);
            }

            bool isDomainEntry = normalizedEntry.IndexOf('.') >= 0;
            string pattern = isDomainEntry
                ? normalizedEntry + WildcardPattern.AnySequence
                : WildcardPattern.AnySequence + normalizedEntry + WildcardPattern.AnySequence;
            return WildcardPattern.Matches(normalizedUrl, pattern);
        }

        /// <summary>
        /// Lower-case, then strip every occurrence of the scheme and of "www." — every occurrence,
        /// not just a leading one, so "safe.example/www.github.com" normalises to
        /// "safe.example/github.com" and can then match a "github.com" entry.
        /// </summary>
        private static string Normalize(string value)
        {
            string normalized = value.ToLowerInvariant();
            // "https://" does not contain "http://", so stripping the shorter one first is safe.
            normalized = normalized.Replace(HttpScheme, string.Empty);
            normalized = normalized.Replace(HttpsScheme, string.Empty);
            normalized = normalized.Replace(WwwPrefix, string.Empty);
            return normalized;
        }

        /// <summary>
        /// A <c>file://</c> entry matches the URL EXACTLY AS TYPED — the un-lowercased original —
        /// against a case-sensitive pattern, so "FILE:///Users/a/p.jpg" does not match the entry
        /// "file:///*" even though "file:///Users/a/p.jpg" does. This is the one place the two sides
        /// of the comparison are not case-folded together; it is preserved because a local-file
        /// allow entry that silently stopped matching would un-block a file the user allowed.
        /// </summary>
        private static bool MatchesFileEntry(string originalUrl, string normalizedEntry)
        {
            if (originalUrl.IndexOf(SchemeSeparator, StringComparison.Ordinal) < 0) return false;
            return WildcardPattern.Matches(originalUrl, normalizedEntry);
        }

        /// <summary>
        /// The Mac's free function <c>hasLetterBefore</c>. Only the FIRST occurrence is examined, so
        /// "vox.com/x.com" is rejected on the "x" in "vox" and the real "x.com" later in the path is
        /// never reached.
        /// </summary>
        internal static bool HasLetterBefore(string substring, string mainString)
        {
            int index = mainString.IndexOf(substring, StringComparison.Ordinal);
            if (index <= 0) return false;
            return char.IsLetter(mainString[index - 1]);
        }
    }
}
