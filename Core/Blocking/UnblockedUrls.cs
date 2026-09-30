using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// The URLs blocking never applies to: browser-internal pages, Focus Bear's own pages, and the
    /// block page itself.
    /// <para>
    /// This existed as <c>UNBLOCKED_URLS</c> in TWO files — <c>ProcessBrowserURLs</c> and
    /// <c>FocusChangedHandler</c> — matched with <c>url.Contains(entry)</c> at five call sites. The
    /// two copies had already drifted: <c>brave://</c>, <c>comet://</c> and <c>localhost</c> were in
    /// one and not the other, so a Brave internal page was exempt from the URL check and not from
    /// the app-switch check.
    /// </para>
    /// <para>
    /// The substring matching was worse than untidy. <c>"localhost"</c> as a substring exempts
    /// <c>https://evil.com/?redirect=localhost</c> and <c>https://mylocalhost-tracker.com</c> —
    /// any site could opt itself out of blocking by naming one of these anywhere in its URL. Entries
    /// are now classified, and a HOST entry is matched against the parsed host.
    /// </para>
    /// </summary>
    public static class UnblockedUrls
    {
        /// <summary>
        /// Schemes a browser uses for its own internal pages. Matched as a prefix, which is what a
        /// scheme is — there is no host to parse in <c>chrome://settings</c>.
        /// </summary>
        public static readonly IReadOnlyList<string> InternalSchemes = new[]
        {
            "about:blank", "chrome://", "chrome-extension://", "devtools://",
            "view-source:", "favorites:", "brave://", "comet://",
        };

        /// <summary>
        /// Focus Bear's own domains. Matched on the parsed HOST, so a subdomain counts and a
        /// mention inside somebody else's URL does not.
        /// </summary>
        public static readonly IReadOnlyList<string> OwnHosts = new[]
        {
            "earnyourscreentime.com", "focusbear.io", "focus-bear.github.io", "localhost",
        };

        /// <summary>
        /// Focus Bear's own local pages. These are served from a <c>file://</c> URL on Windows, so
        /// there is no host — the filename in the path is the identity.
        /// </summary>
        public static readonly IReadOnlyList<string> OwnPageFileNames = new[]
        {
            "focus-block.html", "eyst-focus.html",
        };

        /// <summary>
        /// Whether blocking should leave this URL alone entirely.
        /// </summary>
        public static bool IsAlwaysUnblocked(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            string trimmed = url.Trim();

            if (InternalSchemes.Any(scheme => trimmed.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // The block page is a local file, so only its own path may name it. Checking the whole
            // URL would let https://evil.com/?next=focus-block.html through.
            if (IsLocalFile(trimmed) &&
                OwnPageFileNames.Any(name => PathOf(trimmed).IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }

            string host = HostOf(trimmed);
            if (host == null) return false;
            return OwnHosts.Any(owned => IsHostOrSubdomainOf(host, owned));
        }

        /// <summary>
        /// Host equality, or a subdomain of it. Deliberately not <c>EndsWith(owned)</c>: that treats
        /// <c>notfocusbear.io</c> as Focus Bear's, and <c>mylocalhost</c> as loopback.
        /// </summary>
        private static bool IsHostOrSubdomainOf(string host, string owned)
        {
            if (string.Equals(host, owned, StringComparison.OrdinalIgnoreCase)) return true;
            return host.EndsWith("." + owned, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLocalFile(string url)
        {
            return url.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                || url.IndexOf(":\\", StringComparison.Ordinal) == 1;   // C:\... as handed over by a browser
        }

        private static string PathOf(string url)
        {
            int query = url.IndexOf('?');
            return query >= 0 ? url.Substring(0, query) : url;
        }

        /// <summary>
        /// The host, or null when the URL has none. A browser hands over both "https://x/y" and a
        /// bare "x/y", so a scheme is supplied when one is missing — but only when the text does not
        /// already start with "scheme://", or "localhost:50501/x" would parse "localhost" as the
        /// scheme and report no host at all.
        /// </summary>
        private static string HostOf(string url)
        {
            bool hasScheme = System.Text.RegularExpressions.Regex.IsMatch(url, @"^[A-Za-z][A-Za-z0-9+.-]*://");
            if (!hasScheme && IsLocalFile(url)) return null;
            Uri parsed;
            if (!FocusBear.Core.SafeUri.TryCreate(hasScheme ? url : "http://" + url, UriKind.Absolute, out parsed)) return null;
            return string.IsNullOrEmpty(parsed.Host) ? null : parsed.Host;
        }
    }
}
