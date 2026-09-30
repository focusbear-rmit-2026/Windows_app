using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// The block page's "Oops, I actually need this" unblock. The page (focus-block.html, cached in the app's data
    /// folder and opened as a local file) answers the click by reloading itself with <c>&amp;unblock_it=true</c>
    /// appended; the app sees that URL in the address bar and opens <c>old_url</c> again.
    /// <para>
    /// The app used to honour any address containing <c>&amp;unblock_it=true</c> and an <c>old_url=</c>, so typing
    /// <c>reddit.com/?&amp;unblock_it=true&amp;old_url=https://reddit.com</c> unblocked reddit. Now the request counts
    /// only when it is the app's own block page (its local path) AND carries the token the app put in that page's
    /// URL when it blocked <c>old_url</c> (the page keeps every query parameter when it appends the trigger). A
    /// token only ever unblocks the URL it was issued for, and expires with <see cref="BlockPageTokens.Lifetime"/>.
    /// The Mac serves its page from a local server and checks only the substring; this is stricter.
    /// </para>
    /// </summary>
    public static class BlockPageUnblock
    {
        public const string TriggerParam = "unblock_it";
        public const string TokenParam = "fb_unblock";

        /// <summary>The address carries the page's unblock trigger (whoever put it there).</summary>
        public static bool HasTrigger(string url) => string.Equals(PageParam(url, TriggerParam), "true", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Whether the page shows "Oops, I actually need this" at all: the page script's (focus-blocked/js/main.js)
        /// <c>shouldSkipUnblockPrompt</c> hides it in grizzly mode, on strict blocks, on confirmed super-distracting or
        /// AI-distracting blocks, and on brain-dump routine or always-blocked pages. Only a page that offers it gets a
        /// token, so a hand-typed trigger can't do what the page refuses.
        /// </summary>
        public static bool OffersUnblock(string pageUrl)
        {
            bool Is(string name, string value) => string.Equals(Query(pageUrl, name), value, StringComparison.OrdinalIgnoreCase);
            if (Is("block_mode", "grizzly") || Is("strict_blocking", "true") || Is("confirm_super_distracting", "true")
                || Is("confirm_ai_distracting_url", "true")) return false;
            if (Is("braindump", "true"))
            {
                string type = (Query(pageUrl, "block_type") ?? "").ToLowerInvariant();
                if (type == "always-block" || type == "always-blocked" || type == "morning" || type == "morning-habit"
                    || type == "evening" || type == "evening-habit") return false;
            }
            return true;
        }

        /// <summary>
        /// A parameter the block page's own script adds, which may sit after a <c>#fragment</c>: the page's in-page
        /// links ("#cuddlyBearMode", "#") leave one, and its Oops appends "&amp;unblock_it=true" to the whole href.
        /// The query is read first; the fragment only when the query doesn't have it.
        /// </summary>
        public static string PageParam(string url, string name)
        {
            string value = Query(url, name);
            if (value != null || string.IsNullOrEmpty(url)) return value;
            int hash = url.IndexOf('#');
            return hash < 0 ? null : Query("?" + url.Substring(hash + 1).Replace('#', '&'), name);
        }

        /// <summary>
        /// Whether <paramref name="url"/> is the app's own block page at <paramref name="blockPagePath"/>. Browsers show
        /// a file URL with or without <c>file:///</c>, with <c>%20</c> or a space, and with either slash.
        /// </summary>
        public static bool IsOwnBlockPage(string url, string blockPagePath)
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(blockPagePath)) return false;
            string path = url.Trim();
            int q = path.IndexOfAny(new[] { '?', '#' });
            if (q >= 0) path = path.Substring(0, q);
            return NormalisedPath(path) == NormalisedPath(blockPagePath);
        }

        /// <summary>
        /// Whether a browser's accessible name or title (Firefox names its address bar after the page) mentions this
        /// app's block page: its own data folder and file name, spelled with a space or <c>%20</c>, either slash. The
        /// E2E build's page lives in "Focus Bear E2E", so a hard-coded "Focus Bear/focus-block.html" never matched it.
        /// </summary>
        public static bool MentionsBlockPage(string text, string blockPagePath)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(blockPagePath)) return false;
            string page = NormalisedPath(blockPagePath);
            int cut = page.LastIndexOf('/');
            cut = cut <= 0 ? -1 : page.LastIndexOf('/', cut - 1);
            string tail = cut < 0 ? page : page.Substring(cut + 1);   // "focus bear e2e/focus-block.html"
            return Decode(text).Replace('\\', '/').ToLowerInvariant().Contains(tail);
        }

        /// <summary>
        /// The URL to open again when this is an unblock the app itself offered; null for anything else (a typed or
        /// edited address, a stale or foreign token, or a URL that isn't the block page).
        /// </summary>
        public static string AuthorisedOldUrl(string url, string blockPagePath, BlockPageTokens tokens, DateTime nowUtc)
        {
            if (tokens == null || !HasTrigger(url) || !IsOwnBlockPage(url, blockPagePath)) return null;
            string shown = Query(url, "old_url");
            if (string.IsNullOrEmpty(shown)) return null;
            // The token names the URL the app blocked, and that is what opens. The address bar's old_url must be that
            // URL, or at least the same site: an address bar that shows the query un-escaped splits old_url at its own
            // '&', but a token issued for one site never unblocks another.
            string issued = tokens.IssuedUrl(PageParam(url, TokenParam), nowUtc);
            if (issued == null) return null;
            if (string.Equals(issued, shown, StringComparison.Ordinal)) return issued;
            string issuedHost = HostOf(issued);
            return issuedHost != null && issuedHost == HostOf(shown) ? issued : null;
        }

        /// <summary>The block page URL without the trigger, so reloading it doesn't fire the unblock again.</summary>
        public static string WithoutTrigger(string url) =>
            (url ?? "").Replace("&" + TriggerParam + "=true", "").Replace("?" + TriggerParam + "=true&", "?").Replace("?" + TriggerParam + "=true", "");

        /// <summary>
        /// The host of a URL the user was on, with or without a scheme ("reddit.com/r/x" reads as reddit.com); null
        /// when there is none. Never throws, unlike the <c>new Uri</c> / <c>new UriBuilder</c> pair it replaces.
        /// </summary>
        public static string HostOf(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            string text = url.Trim();
            if (!FocusBear.Core.SafeUri.TryCreate(text, UriKind.Absolute, out Uri uri) || string.IsNullOrEmpty(uri.Host))
            {
                // No scheme ("reddit.com/r/x"): read it as http. One that has a scheme but no host has no host.
                if (text.Contains("://") || !FocusBear.Core.SafeUri.TryCreate("http://" + text, UriKind.Absolute, out uri)) return null;
            }
            string host = uri.Host;
            return string.IsNullOrEmpty(host) ? null : host.ToLowerInvariant();
        }

        /// <summary>A query parameter's decoded value, or null. The first occurrence wins.</summary>
        public static string Query(string url, string name)
        {
            if (string.IsNullOrEmpty(url)) return null;
            int q = url.IndexOf('?');
            if (q < 0) return null;
            string query = url.Substring(q + 1);
            int hash = query.IndexOf('#');
            if (hash >= 0) query = query.Substring(0, hash);
            foreach (string pair in query.Split('&'))
            {
                int eq = pair.IndexOf('=');
                string key = eq < 0 ? pair : pair.Substring(0, eq);
                if (!string.Equals(Decode(key), name, StringComparison.Ordinal)) continue;
                return eq < 0 ? "" : Decode(pair.Substring(eq + 1));
            }
            return null;
        }

        private static string Decode(string s)
        {
            try { return Uri.UnescapeDataString((s ?? "").Replace('+', ' ')); }
            catch (UriFormatException) { return s; }
        }

        private static string NormalisedPath(string path)
        {
            string p = Decode(path.Trim()).Replace('\\', '/');
            if (p.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) p = p.Substring(5);
            return p.TrimStart('/').ToLowerInvariant();
        }
    }

    /// <summary>
    /// The unblock tokens the app has put in block-page URLs this run: each is random, tied to the URL it blocked,
    /// and good for <see cref="Lifetime"/>. At most <see cref="MaxTokens"/> are kept (the oldest go first).
    /// Thread-safe: pages are built on the automation threads.
    /// </summary>
    public sealed class BlockPageTokens
    {
        public static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);
        public const int MaxTokens = 500;

        private readonly object _lock = new object();
        private readonly Dictionary<string, KeyValuePair<string, DateTime>> _issued = new Dictionary<string, KeyValuePair<string, DateTime>>(StringComparer.Ordinal);
        private readonly Queue<string> _order = new Queue<string>();
        private readonly Func<string> _newToken;

        public BlockPageTokens(Func<string> newToken = null)
        {
            _newToken = newToken ?? (() => Guid.NewGuid().ToString("N"));
        }

        /// <summary>
        /// Called with the whole store as JSON after every issue, so a block page already open still unblocks after the
        /// app restarts (the host writes it to the app's data folder). Called outside the lock.
        /// </summary>
        public Action<string> Saved { get; set; }

        /// <summary>A fresh token for a block page offering to unblock <paramref name="oldUrl"/>.</summary>
        public string Issue(string oldUrl, DateTime nowUtc)
        {
            string token = _newToken();
            lock (_lock)
            {
                _issued[token] = new KeyValuePair<string, DateTime>(oldUrl ?? "", nowUtc + Lifetime);
                _order.Enqueue(token);
                while (_order.Count > MaxTokens) _issued.Remove(_order.Dequeue());
            }
            try { Saved?.Invoke(ToJson()); } catch { /* the in-memory store still works */ }
            return token;
        }

        /// <summary>Appends a fresh token to a block page URL, when that page offers an unblock at all.</summary>
        public string Stamp(string blockPageUrl, string oldUrl, DateTime nowUtc) =>
            !BlockPageUnblock.OffersUnblock(blockPageUrl) ? blockPageUrl
            : blockPageUrl + (blockPageUrl.Contains("?") ? "&" : "?") + BlockPageUnblock.TokenParam + "=" + Issue(oldUrl, nowUtc);

        public bool IsValid(string token, string oldUrl, DateTime nowUtc) =>
            IssuedUrl(token, nowUtc) is string issued && string.Equals(issued, oldUrl ?? "", StringComparison.Ordinal);

        /// <summary>The URL a live token was issued for, or null for an unknown or expired one.</summary>
        public string IssuedUrl(string token, DateTime nowUtc)
        {
            if (string.IsNullOrEmpty(token)) return null;
            lock (_lock)
            {
                if (!_issued.TryGetValue(token, out var entry)) return null;
                return nowUtc < entry.Value ? entry.Key : null;
            }
        }

        public int Count { get { lock (_lock) return _issued.Count; } }

        /// <summary>The store, oldest first: <c>{ tokens: [{ token, url, expires }] }</c> (expires in UTC, ISO).</summary>
        public string ToJson()
        {
            var list = new JArray();
            lock (_lock)
            {
                foreach (string token in _order)
                {
                    if (!_issued.TryGetValue(token, out var entry)) continue;
                    list.Add(new JObject
                    {
                        ["token"] = token,
                        ["url"] = entry.Key,
                        ["expires"] = entry.Value.ToString("o", CultureInfo.InvariantCulture),
                    });
                }
            }
            return new JObject { ["tokens"] = list }.ToString(Formatting.None);
        }

        /// <summary>
        /// A store read back from <see cref="ToJson"/>: expired tokens are dropped, and so is any whose expiry lies
        /// further out than <see cref="Lifetime"/> (the app never issued it). At most <see cref="MaxTokens"/>, newest
        /// kept. Unreadable input gives an empty store.
        /// </summary>
        public static BlockPageTokens FromJson(string json, DateTime nowUtc, Func<string> newToken = null)
        {
            var store = new BlockPageTokens(newToken);
            if (string.IsNullOrWhiteSpace(json)) return store;
            try
            {
                JObject root;
                using (var reader = new JsonTextReader(new System.IO.StringReader(json)) { DateParseHandling = DateParseHandling.None })
                    root = JToken.ReadFrom(reader) as JObject;
                if (!(root?["tokens"] is JArray list)) return store;
                foreach (JObject o in list.OfType<JObject>())
                {
                    string token = o.Value<string>("token"), url = o.Value<string>("url"), expires = o.Value<string>("expires");
                    if (string.IsNullOrEmpty(token) || url == null) continue;
                    if (!DateTime.TryParse(expires, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime at)) continue;
                    if (at <= nowUtc || at > nowUtc + Lifetime) continue;
                    lock (store._lock)
                    {
                        if (store._issued.ContainsKey(token)) continue;
                        store._issued[token] = new KeyValuePair<string, DateTime>(url, DateTime.SpecifyKind(at, DateTimeKind.Utc));
                        store._order.Enqueue(token);
                        while (store._order.Count > MaxTokens) store._issued.Remove(store._order.Dequeue());
                    }
                }
            }
            catch (JsonException) { return new BlockPageTokens(newToken); }
            return store;
        }
    }
}
