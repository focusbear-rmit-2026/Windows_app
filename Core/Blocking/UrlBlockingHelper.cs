using System;
using System.Collections.Generic;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// The Mac's <c>URLBlockingHelper</c> — list membership, then the block/allow decision.
    ///
    /// Note which matcher this uses: BOTH lists go through <see cref="UrlValidation.IsUrlValid"/>,
    /// the substring matcher. <see cref="UrlValidation.IsUrlAlwaysBlocked"/> — the anchored one
    /// written for exactly this list — is NOT called from here on the Mac either. Keeping the
    /// mismatch means "m.facebook.com" is blocked by a "facebook.com" entry on both platforms; the
    /// anchored matcher is ported alongside so the difference stays visible rather than being
    /// quietly resolved one way or the other.
    /// </summary>
    public static class UrlBlockingHelper
    {
        public static bool IsUrlInAlwaysBlockedList(string url, IEnumerable<string> alwaysBlockedUrls)
        {
            return MatchesAny(url, alwaysBlockedUrls);
        }

        public static bool IsUrlInAllowedList(string url, IEnumerable<string> allowedUrls)
        {
            return MatchesAny(url, allowedUrls);
        }

        /// <summary>
        /// Always-blocked wins over the allowed list, and the allowed list only blocks anything at
        /// all when the block list is running the allow-list approach. With that approach an EMPTY
        /// allowed list blocks everything, because nothing has been whitelisted — the opposite of an
        /// empty always-blocked list, which blocks nothing.
        /// </summary>
        public static bool ShouldBlockUrl(string url, IEnumerable<string> alwaysBlockedUrls,
                                          IEnumerable<string> allowedUrls, bool isAllowedListApproach)
        {
            if (IsUrlInAlwaysBlockedList(url, alwaysBlockedUrls)) return true;
            if (isAllowedListApproach) return !IsUrlInAllowedList(url, allowedUrls);
            return false;
        }

        private static bool MatchesAny(string url, IEnumerable<string> entries)
        {
            if (url == null) throw new ArgumentNullException(nameof(url));
            if (entries == null) return false;
            foreach (string entry in entries)
            {
                if (entry != null && UrlValidation.IsUrlValid(url, entry)) return true;
            }
            return false;
        }
    }
}
