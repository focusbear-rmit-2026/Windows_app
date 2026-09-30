using System;
using System.Collections.Generic;

namespace FocusBear.Core.Blocking
{
    /// <summary>One allow-list entry of a managed policy.</summary>
    public sealed class ManagedAllowlistEntry
    {
        public ManagedAllowlistEntry(string url, bool includeSubdomains = false, DateTimeOffset? expiresAt = null)
        {
            Url = url;
            IncludeSubdomains = includeSubdomains;
            ExpiresAt = expiresAt;
        }

        /// <summary>The allowed URL. Canonicalised through <see cref="ManagedResource"/> before use.</summary>
        public string Url { get; }

        /// <summary>Whether hosts BELOW this one are allowed too. Off by default.</summary>
        public bool IncludeSubdomains { get; }

        /// <summary>When this entry stops applying, or null for a permanent one.</summary>
        public DateTimeOffset? ExpiresAt { get; }
    }

    /// <summary>
    /// The Mac's <c>ManagedPolicyEvaluator.matchesAllowlist</c> — whether a page is explicitly
    /// permitted by a managed policy.
    ///
    /// This is an ALLOW list, so every rule here is deliberately narrow; a loose one hands someone a
    /// way to reach a blocked page through a URL that merely looks like an allowed one.
    ///
    /// <list type="bullet">
    /// <item>HOST is exact, or — only with <see cref="ManagedAllowlistEntry.IncludeSubdomains"/> — a
    ///       host ending in "." + the allowed host. The dot is what makes it a subdomain: without it
    ///       "notexample.org" and "example.org.evil.example" would both pass as "example.org".</item>
    /// <item>PORT must be equal, including both being the scheme default. A different port is a
    ///       different service.</item>
    /// <item>PATH matches on SEGMENT boundaries: an entry for "/docs" allows "/docs" and "/docs/…"
    ///       but not "/docsecret". An entry with no path (or just "/") allows the whole host.</item>
    /// <item>An EXPIRED entry matches nothing, and an unparseable entry URL matches nothing —
    ///       failure is always towards blocking.</item>
    /// </list>
    ///
    /// The SCHEME is deliberately not compared, matching the Mac: an entry written as http allows the
    /// https page on the same host, path and port.
    /// </summary>
    public static class ManagedPolicyAllowlist
    {
        public static bool Matches(IEnumerable<ManagedAllowlistEntry> allowlist, string url, DateTimeOffset now)
        {
            if (allowlist == null) return false;
            ManagedResource resource = ManagedResource.TryCreate(url);
            if (resource == null) return false;

            foreach (ManagedAllowlistEntry entry in allowlist)
            {
                if (entry != null && MatchesEntry(entry, resource, now)) return true;
            }
            return false;
        }

        private static bool MatchesEntry(ManagedAllowlistEntry entry, ManagedResource resource, DateTimeOffset now)
        {
            if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value <= now) return false;

            ManagedResource allowed = ManagedResource.TryCreate(entry.Url);
            if (allowed == null) return false;
            if (allowed.Port != resource.Port) return false;

            bool hostMatches = resource.Host == allowed.Host
                               || (entry.IncludeSubdomains
                                   && resource.Host.EndsWith("." + allowed.Host, StringComparison.Ordinal));
            if (!hostMatches) return false;

            // A canonical path is never empty ("/" at minimum); dropping its trailing slash is what
            // turns a bare host entry into "any path on this host".
            string path = allowed.Path.EndsWith("/", StringComparison.Ordinal)
                ? allowed.Path.Substring(0, allowed.Path.Length - 1)
                : allowed.Path;

            return path.Length == 0
                   || resource.Path == path
                   || resource.Path.StartsWith(path + "/", StringComparison.Ordinal);
        }
    }
}
