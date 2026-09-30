using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// A URL reduced to one canonical form, or nothing at all. Ported from the Mac's
    /// <c>ManagedResource</c> — the strict counterpart to <see cref="UrlValidation"/>'s substring
    /// matching, used wherever a decision has to be defensible rather than forgiving.
    ///
    /// The point of canonicalising is that two spellings of the same page must not produce two
    /// different answers, and two DIFFERENT pages must not collapse into one. Everything it refuses
    /// is refused because it is a way of writing one thing that reads as another:
    ///
    /// <list type="bullet">
    /// <item>EMBEDDED CREDENTIALS (<c>https://trusted.example@evil.example/</c>) — the part before
    ///       the "@" is a username, not the host, and a reader sees the wrong site. Rejected outright
    ///       rather than parsed, because there is no safe reading of it.</item>
    /// <item>BACKSLASHES, CONTROL CHARACTERS AND SPACES — browsers differ on where these end the
    ///       host, so any answer given here could be the wrong one.</item>
    /// <item>MALFORMED PERCENT-ESCAPES (<c>%ZZ</c>, a truncated <c>%4</c>) — undecodable, so the
    ///       path cannot be compared.</item>
    /// <item>NON-HTTP SCHEMES and an EMPTY HOST — nothing to match against.</item>
    /// <item>OVER-LONG INPUT — bounded work, and nothing legitimate is that long.</item>
    /// </list>
    ///
    /// And everything it rewrites is a spelling that would otherwise dodge a comparison: host case,
    /// a trailing dot on the host, IDN spelled in Unicode, an IPv4 address written short/octal/hex,
    /// an uncompressed IPv6 address, a default port, <c>%61</c> for <c>a</c>, and <c>..</c> path
    /// segments. The fragment is dropped (it never reaches the server); the query is KEPT, in its
    /// original order, because it routinely selects the page.
    ///
    /// Divergences from the Swift, all outside the shared fixture: the length limit counts UTF-16
    /// units here and grapheme clusters there; and a raw non-ASCII character in a path is passed
    /// through here where Swift mangles it into its UTF-8 bytes. Neither changes a match outcome,
    /// because both sides of a comparison run through this same function.
    /// </summary>
    public sealed class ManagedResource : IEquatable<ManagedResource>
    {
        /// <summary>Longest input considered at all.</summary>
        public const int MaximumLength = 4096;

        private const string SchemeSeparator = "://";
        private const string Https = "https";
        private const string Http = "http";
        private const int HttpsDefaultPort = 443;
        private const int HttpDefaultPort = 80;

        private const string UnreservedCharacters =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";

        /// <summary>
        /// A "%" that does not begin a complete two-hex-digit escape: a non-hex character after it,
        /// a single hex digit then a non-hex character or the end, or a "%" at the very end.
        /// </summary>
        private static readonly Regex MalformedPercentEscape =
            new Regex("%([^0-9a-fA-F]|[0-9a-fA-F]([^0-9a-fA-F]|$)|$)", RegexOptions.CultureInvariant);

        private ManagedResource(string normalizedUrl, string host, string path, int? port)
        {
            NormalizedUrl = normalizedUrl;
            Host = host;
            Path = path;
            Port = port;
        }

        /// <summary>The canonical absolute URL. Fragment dropped, query preserved verbatim.</summary>
        public string NormalizedUrl { get; }

        /// <summary>The canonical host, WITHOUT the brackets an IPv6 literal keeps in the URL.</summary>
        public string Host { get; }

        /// <summary>The canonical path. Always starts with "/".</summary>
        public string Path { get; }

        /// <summary>The port, or null when it is the scheme's default.</summary>
        public int? Port { get; }

        /// <summary>SHA-256 of the canonical URL, lower-case hex — how a resource is named in reports.</summary>
        public string Hash { get { return Sha256Hex(NormalizedUrl); } }

        /// <summary>The canonical form of <paramref name="raw"/>, or null if it cannot be trusted.</summary>
        public static ManagedResource TryCreate(string raw)
        {
            if (raw == null || raw.Length > MaximumLength) return null;
            foreach (char character in raw)
            {
                if (character <= 32 || character == 127) return null;
            }
            if (raw.IndexOf('\\') >= 0) return null;

            // A bare host is read as https, so "example.org" and "https://example.org" agree.
            string input = raw.IndexOf(SchemeSeparator, StringComparison.Ordinal) >= 0
                ? raw
                : Https + SchemeSeparator + raw;

            string beforeQuery = SplitBefore(SplitBefore(input, '?'), '#');
            if (MalformedPercentEscape.IsMatch(beforeQuery)) return null;

            int schemeEnd = input.IndexOf(SchemeSeparator, StringComparison.Ordinal);
            if (schemeEnd <= 0) return null;
            string scheme = input.Substring(0, schemeEnd).ToLowerInvariant();
            if (scheme != Https && scheme != Http) return null;

            string rest = input.Substring(schemeEnd + SchemeSeparator.Length);
            int authorityEnd = IndexOfAny(rest, '/', '?', '#');
            string authority = authorityEnd < 0 ? rest : rest.Substring(0, authorityEnd);
            string remainder = authorityEnd < 0 ? string.Empty : rest.Substring(authorityEnd);

            // Any "@" here means a user (and maybe a password) component.
            if (authority.IndexOf('@') >= 0) return null;

            string hostLiteral;
            int? port;
            if (!TrySplitAuthority(authority, out hostLiteral, out port)) return null;

            string canonicalHostLiteral = CanonicalHostLiteral(hostLiteral);
            if (canonicalHostLiteral == null) return null;

            if (port.HasValue
                && ((scheme == Https && port.Value == HttpsDefaultPort)
                    || (scheme == Http && port.Value == HttpDefaultPort)))
            {
                port = null;
            }

            string rawPath = SplitBefore(SplitBefore(remainder, '?'), '#');
            string path = NormalizePath(rawPath);
            if (path == null) return null;

            string query = ExtractQuery(remainder);

            var normalized = new StringBuilder();
            normalized.Append(scheme).Append(SchemeSeparator).Append(canonicalHostLiteral);
            if (port.HasValue) normalized.Append(':').Append(port.Value.ToString(CultureInfo.InvariantCulture));
            normalized.Append(path);
            if (query != null) normalized.Append('?').Append(query);

            return new ManagedResource(normalized.ToString(), canonicalHostLiteral.Trim('[', ']'), path, port);
        }

        public static string Sha256Hex(string value)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
                var hex = new StringBuilder(digest.Length * 2);
                foreach (byte octet in digest) hex.Append(octet.ToString("x2", CultureInfo.InvariantCulture));
                return hex.ToString();
            }
        }

        private static string SplitBefore(string value, char separator)
        {
            int index = value.IndexOf(separator);
            return index < 0 ? value : value.Substring(0, index);
        }

        private static int IndexOfAny(string value, char first, char second, char third)
        {
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (character == first || character == second || character == third) return index;
            }
            return -1;
        }

        /// <summary>
        /// The query as written, or null when there is none or it is empty ("…/?").
        ///
        /// The fragment is cut FIRST, so a "?" that appears only inside one ("/a#b?c") stays part of
        /// the discarded fragment instead of being mistaken for a query.
        /// </summary>
        private static string ExtractQuery(string remainder)
        {
            string beforeFragment = SplitBefore(remainder, '#');
            int start = beforeFragment.IndexOf('?');
            if (start < 0) return null;
            string query = beforeFragment.Substring(start + 1);
            return query.Length == 0 ? null : query;
        }

        private static bool TrySplitAuthority(string authority, out string hostLiteral, out int? port)
        {
            hostLiteral = null;
            port = null;
            if (authority.Length == 0) return false;

            string portText = null;
            if (authority[0] == '[')
            {
                int close = authority.IndexOf(']');
                if (close < 0) return false;
                hostLiteral = authority.Substring(0, close + 1);
                string after = authority.Substring(close + 1);
                if (after.Length > 0)
                {
                    if (after[0] != ':') return false;
                    portText = after.Substring(1);
                }
            }
            else
            {
                int colon = authority.LastIndexOf(':');
                if (colon >= 0)
                {
                    hostLiteral = authority.Substring(0, colon);
                    portText = authority.Substring(colon + 1);
                }
                else
                {
                    hostLiteral = authority;
                }
            }

            if (string.IsNullOrEmpty(portText)) return true;
            foreach (char digit in portText)
            {
                if (digit < '0' || digit > '9') return false;
            }
            int parsed;
            if (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out parsed)) return false;
            port = parsed;
            return true;
        }

        /// <summary>
        /// The host as it appears in the canonical URL — IPv6 keeps its brackets — or null when it
        /// cannot be canonicalised.
        /// </summary>
        private static string CanonicalHostLiteral(string hostLiteral)
        {
            string host = hostLiteral.ToLowerInvariant();

            if (host.Length > 1 && host[0] == '[' && host[host.Length - 1] == ']')
            {
                return CanonicalIpv6(host.Substring(1, host.Length - 2));
            }

            // A trailing dot is the root label; "example.org." and "example.org" are one host.
            if (host.EndsWith(".", StringComparison.Ordinal)) host = host.Substring(0, host.Length - 1);
            if (host.Length == 0) return null;

            // An IDN spelled in Unicode has to become its ASCII form before anything is compared.
            try
            {
                host = new IdnMapping().GetAscii(host).ToLowerInvariant();
            }
            catch (ArgumentException)
            {
                return null;
            }
            if (host.Length == 0) return null;

            return CanonicalIpv4(host);
        }

        private static string CanonicalIpv6(string bare)
        {
            // inet_pton knows nothing about scope ids; IPAddress does, so refuse them explicitly.
            if (bare.IndexOf('%') >= 0) return null;
            IPAddress address;
            if (!IPAddress.TryParse(bare, out address)) return null;
            if (address.AddressFamily != AddressFamily.InterNetworkV6) return null;
            return "[" + address.ToString() + "]";
        }

        /// <summary>
        /// Rewrites the many spellings of an IPv4 address to dotted-quad, and leaves a real hostname
        /// alone. "127.1", "0x7f.1" and "0177.0.0.1" all name 127.0.0.1, so all three must produce
        /// the same string or an allow-list entry could be walked around by writing the number
        /// differently.
        /// </summary>
        private static string CanonicalIpv4(string host)
        {
            string[] parts = host.Split('.');
            string last = parts[parts.Length - 1];

            // Only a final label that is entirely numeric (or hex) can make this an address.
            bool numericTail = last.Length > 0;
            foreach (char character in last)
            {
                if (!char.IsNumber(character)) { numericTail = false; break; }
            }
            if (!numericTail && !last.StartsWith("0x", StringComparison.Ordinal)) return host;
            if (parts.Length < 1 || parts.Length > 4) return null;

            var numbers = new List<ulong>(parts.Length);
            foreach (string part in parts)
            {
                ulong value;
                if (!TryParseIpv4Part(part, out value)) return null;
                numbers.Add(value);
            }

            for (int index = 0; index < numbers.Count - 1; index++)
            {
                if (numbers[index] > 255) return null;
            }

            // The final part absorbs every byte the earlier parts did not name.
            ulong tail = numbers[numbers.Count - 1];
            int tailBits = 8 * (5 - numbers.Count);
            if (tailBits < 64 && tail >= (1UL << tailBits)) return null;

            ulong packed = tail;
            for (int index = 0; index < numbers.Count - 1; index++)
            {
                packed += numbers[index] << (8 * (3 - index));
            }

            return string.Join(".", new[]
            {
                ((packed >> 24) & 255).ToString(CultureInfo.InvariantCulture),
                ((packed >> 16) & 255).ToString(CultureInfo.InvariantCulture),
                ((packed >> 8) & 255).ToString(CultureInfo.InvariantCulture),
                (packed & 255).ToString(CultureInfo.InvariantCulture),
            });
        }

        private static bool TryParseIpv4Part(string part, out ulong value)
        {
            value = 0;
            if (part.Length == 0) return false;
            if (part.StartsWith("0x", StringComparison.Ordinal))
            {
                return TryParseRadix(part.Substring(2), 16, out value);
            }
            if (part.Length > 1 && part[0] == '0')
            {
                return TryParseRadix(part.Substring(1), 8, out value);
            }
            return TryParseRadix(part, 10, out value);
        }

        private static bool TryParseRadix(string digits, int radix, out ulong value)
        {
            value = 0;
            if (digits.Length == 0) return false;
            foreach (char character in digits)
            {
                int digit;
                if (character >= '0' && character <= '9') digit = character - '0';
                else if (character >= 'a' && character <= 'f') digit = character - 'a' + 10;
                else if (character >= 'A' && character <= 'F') digit = character - 'A' + 10;
                else return false;
                if (digit >= radix) return false;
                if (value > (ulong.MaxValue - (ulong)digit) / (ulong)radix) return false;
                value = value * (ulong)radix + (ulong)digit;
            }
            return true;
        }

        /// <summary>
        /// Decodes the escapes that did not need escaping, upper-cases the ones that did, then
        /// resolves "." and ".." segments. Returns null if an escape cannot be decoded.
        ///
        /// Decoding happens BEFORE the segments are split, which is what makes "%2e%2e" a real ".."
        /// (so it cannot smuggle a directory traversal past the comparison) while "%2f" stays encoded
        /// and therefore does NOT become a path separator.
        /// </summary>
        internal static string NormalizePath(string raw)
        {
            var escaped = new StringBuilder(raw.Length);
            int index = 0;
            while (index < raw.Length)
            {
                if (raw[index] != '%') { escaped.Append(raw[index]); index++; continue; }
                if (index + 2 >= raw.Length) return null;
                int value;
                if (!int.TryParse(raw.Substring(index + 1, 2), NumberStyles.HexNumber,
                                  CultureInfo.InvariantCulture, out value)) return null;
                char decoded = (char)value;
                if (UnreservedCharacters.IndexOf(decoded) >= 0) escaped.Append(decoded);
                else escaped.Append('%').Append(value.ToString("X2", CultureInfo.InvariantCulture));
                index += 3;
            }

            string decodedPath = escaped.ToString();
            string[] parts = decodedPath.Split('/');
            var segments = new List<string>(parts.Length);
            for (int position = 0; position < parts.Length; position++)
            {
                string part = parts[position];
                // A leading "/" produces an empty first part that is the root, not a segment.
                if (position == 0 && part.Length == 0) continue;
                if (part == "..") { if (segments.Count > 0) segments.RemoveAt(segments.Count - 1); }
                else if (part != ".") segments.Add(part);
            }

            // "/a/." and "/a/.." name a directory, so the slash they lost is put back.
            bool needsTrailingSlash = decodedPath.EndsWith("/.", StringComparison.Ordinal)
                                      || decodedPath.EndsWith("/..", StringComparison.Ordinal);
            string normalized = "/" + string.Join("/", segments.ToArray());
            if (needsTrailingSlash && !normalized.EndsWith("/", StringComparison.Ordinal)) normalized += "/";
            return normalized;
        }

        public bool Equals(ManagedResource other)
        {
            return other != null && NormalizedUrl == other.NormalizedUrl;
        }

        public override bool Equals(object obj) { return Equals(obj as ManagedResource); }

        public override int GetHashCode() { return NormalizedUrl.GetHashCode(); }

        public override string ToString() { return NormalizedUrl; }
    }
}
