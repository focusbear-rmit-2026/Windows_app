using Focus_Bear.API;
using Focus_Bear.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// Fetches the user's calendar meetings for the Day Plan. Windows has no EventKit, so — unlike the
    /// macOS app (which reads Apple Calendar locally) — this uses the backend calendar sync:
    ///   • GET /calendar/{platform}/calendar-datas  → the connected calendars/accounts (google | microsoft)
    ///   • GET /calendar/{platform}/events?account= → that account's events (raw Google / Microsoft shape)
    /// Events are parsed into <see cref="ScheduleMeetingInput"/> (the engine already lays meetings out and
    /// packs habits around them). Declined and all-day events are dropped. Read-only: the backend has no
    /// create-event endpoint, so creating/editing meetings isn't supported here (noted for follow-up).
    /// Results are cached by day; the Day Plan reads <see cref="MeetingsFor"/> and refreshes in the
    /// background via <see cref="RefreshAsync"/>.
    /// </summary>
    public sealed class DayPlanCalendarManager
    {
        private static readonly DayPlanCalendarManager _shared = new DayPlanCalendarManager();
        public static DayPlanCalendarManager Shared => _shared;

        private static readonly string[] Platforms = { "google", "microsoft" };

        private readonly object _lock = new object();
        private Dictionary<string, List<ScheduleMeetingInput>> _byDay = new Dictionary<string, List<ScheduleMeetingInput>>();
        // Event ids the user hid for a day (from GET /day-plan/day/{day}.dismissed_event_ids, plus local edits).
        private readonly Dictionary<string, HashSet<string>> _dismissedByDay = new Dictionary<string, HashSet<string>>();
        // Event ids the user explicitly hid/restored this session — kept authoritative so a subsequent backend
        // hydrate (which may lag, or not persist yet) can't silently undo the user's own toggle.
        private readonly Dictionary<string, HashSet<string>> _userTouched = new Dictionary<string, HashSet<string>>();
        // Per-event colour override (event id -> hex), from GET /day-plan/event-colour plus local edits.
        private readonly Dictionary<string, string> _colourByEvent = new Dictionary<string, string>();
        // Event ids whose colour the user set/cleared this session — kept authoritative over backend hydrate.
        private readonly HashSet<string> _colourTouched = new HashSet<string>();
        private DateTime _lastRefreshUtc = DateTime.MinValue;
        private bool _refreshing;

        private DayPlanCalendarManager() { }

        private static string DayKey(DateTime d) => d.ToString("yyyy-MM-dd");

        /// <summary>
        /// Cached meetings for a day, each tagged with its dismissed state. Dismissed meetings are omitted
        /// unless <paramref name="includeDismissed"/> is true (the "show dismissed events" toggle).
        /// </summary>
        public List<ScheduleMeetingInput> MeetingsFor(DateTime date, bool includeDismissed = false)
        {
            lock (_lock)
            {
                string k = DayKey(date);
                if (!_byDay.TryGetValue(k, out List<ScheduleMeetingInput> list)) return new List<ScheduleMeetingInput>();
                _dismissedByDay.TryGetValue(k, out HashSet<string> dismissed);
                var result = new List<ScheduleMeetingInput>();
                foreach (ScheduleMeetingInput m in list)
                {
                    bool isDismissed = dismissed != null && dismissed.Contains(m.Id);
                    if (isDismissed && !includeDismissed) continue;
                    _colourByEvent.TryGetValue(m.Id, out string colour);
                    result.Add(new ScheduleMeetingInput(m.Id, m.Title, m.Start, m.End, isDismissed, colour));
                }
                return result;
            }
        }

        /// <summary>
        /// Apply the dismissed-event set from the backend day-plan payload, but keep any hide/restore the
        /// user made THIS session authoritative — otherwise a lagging (or not-yet-persisted) backend read
        /// would undo the toggle the user just made when they navigate away and back.
        /// </summary>
        public void SetDismissed(DateTime date, IEnumerable<string> eventIds)
        {
            lock (_lock)
            {
                string k = DayKey(date);
                var set = new HashSet<string>(eventIds ?? Enumerable.Empty<string>());
                if (_userTouched.TryGetValue(k, out HashSet<string> touched) &&
                    _dismissedByDay.TryGetValue(k, out HashSet<string> current))
                {
                    foreach (string id in touched)
                    {
                        if (current.Contains(id)) set.Add(id); else set.Remove(id);
                    }
                }
                _dismissedByDay[k] = set;
            }
        }

        public bool IsDismissed(string eventId, DateTime date)
        {
            lock (_lock)
            {
                return _dismissedByDay.TryGetValue(DayKey(date), out HashSet<string> set) && set.Contains(eventId);
            }
        }

        /// <summary>Hide / un-hide a meeting for a day (local set + backend, best-effort).</summary>
        public void SetDismissed(string eventId, DateTime date, bool dismissed)
        {
            if (string.IsNullOrEmpty(eventId)) return;
            string k = DayKey(date);
            lock (_lock)
            {
                if (!_dismissedByDay.TryGetValue(k, out HashSet<string> set)) { set = new HashSet<string>(); _dismissedByDay[k] = set; }
                if (dismissed) set.Add(eventId); else set.Remove(eventId);
                if (!_userTouched.TryGetValue(k, out HashSet<string> touched)) { touched = new HashSet<string>(); _userTouched[k] = touched; }
                touched.Add(eventId);   // remember the user decided this one — survives later backend hydrates
            }
            FireAndForget(() => dismissed
                ? new FocusBearAPIClient().DismissEvent(k, eventId)
                : new FocusBearAPIClient().UndismissEvent(k, eventId));
        }

        // ---- per-event colour --------------------------------------------------------------------

        /// <summary>The user's colour override for a meeting (hex), or null for the default.</summary>
        public string ColourFor(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return null;
            lock (_lock) { return _colourByEvent.TryGetValue(eventId, out string c) ? c : null; }
        }

        /// <summary>
        /// Set (or, with a null colour, clear) a meeting's colour. Applied locally at once and marked
        /// authoritative for the session so a lagging backend hydrate can't undo it, then persisted best-effort.
        /// </summary>
        public void SetColour(string eventId, string colour)
        {
            if (string.IsNullOrEmpty(eventId)) return;
            lock (_lock)
            {
                if (string.IsNullOrEmpty(colour)) _colourByEvent.Remove(eventId);
                else _colourByEvent[eventId] = colour;
                _colourTouched.Add(eventId);
            }
            FireAndForget(() => string.IsNullOrEmpty(colour)
                ? new FocusBearAPIClient().ClearEventColour(eventId)
                : new FocusBearAPIClient().SetEventColour(eventId, colour));
        }

        /// <summary>
        /// Load the per-event colour map from the backend, keeping any colour the user set/cleared this
        /// session authoritative (same rationale as the dismissed-set hydrate).
        /// </summary>
        private async Task LoadColours(FocusBearAPIClient client)
        {
            try
            {
                string json = await client.GetEventColours();
                if (string.IsNullOrWhiteSpace(json)) return;
                JArray arr = AsArray(ParseJson(json));
                if (arr == null) return;
                var fresh = new Dictionary<string, string>();
                foreach (JToken t in arr)
                {
                    if (!(t is JObject o)) continue;
                    string id = o.Value<string>("event_id") ?? o.Value<string>("eventId");
                    string colour = o.Value<string>("colour") ?? o.Value<string>("color");
                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(colour)) fresh[id] = colour;
                }
                lock (_lock)
                {
                    foreach (string id in _colourTouched)   // keep the user's session edits over the snapshot
                    {
                        if (_colourByEvent.TryGetValue(id, out string c)) fresh[id] = c; else fresh.Remove(id);
                    }
                    _colourByEvent.Clear();
                    foreach (var kv in fresh) _colourByEvent[kv.Key] = kv.Value;
                }
            }
            catch (Exception ex) { LogWriter.LogWrite("DayPlanCalendar.LoadColours - " + ex.Message); }
        }

        private static async void FireAndForget(Func<Task<bool>> call)
        {
            try { await call(); } catch (Exception ex) { LogWriter.LogWrite("DayPlanCalendar dismiss - " + ex.Message); }
        }

        /// <summary>
        /// Pull events from every connected calendar (both platforms) and rebuild the by-day cache.
        /// Throttled to once per <paramref name="minGapSeconds"/> unless <paramref name="force"/>. Returns
        /// true if the cache changed (so the caller can re-render). Best-effort; failures just no-op.
        /// </summary>
        public async Task<bool> RefreshAsync(bool force = false, int minGapSeconds = 45)
        {
            lock (_lock)
            {
                if (_refreshing) return false;
                if (!force && (DateTime.UtcNow - _lastRefreshUtc).TotalSeconds < minGapSeconds) return false;
                _refreshing = true;
            }
            try
            {
                var fresh = new Dictionary<string, List<ScheduleMeetingInput>>();
                var seenIds = new HashSet<string>();
                var client = new FocusBearAPIClient();

                foreach (string platform in Platforms)
                {
                    List<string> accounts = await AccountsFor(client, platform);
                    foreach (string account in accounts)
                    {
                        string json = await client.GetCalendarEvents(platform, account);
                        foreach (ScheduleMeetingInput m in ParseEvents(json, platform))
                        {
                            if (!seenIds.Add(m.Id)) continue;   // de-dupe across accounts
                            string k = DayKey(m.Start);
                            if (!fresh.TryGetValue(k, out List<ScheduleMeetingInput> list))
                            {
                                list = new List<ScheduleMeetingInput>();
                                fresh[k] = list;
                            }
                            list.Add(m);
                        }
                    }
                }
                foreach (var kv in fresh) kv.Value.Sort((a, b) => a.Start.CompareTo(b.Start));

                await LoadColours(client);

                lock (_lock)
                {
                    _byDay = fresh;
                    _lastRefreshUtc = DateTime.UtcNow;
                }
                string todayK = DayKey(DateTime.Today);
                int todayCount = fresh.TryGetValue(todayK, out List<ScheduleMeetingInput> tl) ? tl.Count : 0;
                string breakdown = string.Join(", ", fresh
                    .Where(kv => string.CompareOrdinal(kv.Key, todayK) >= 0)
                    .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                    .Take(8).Select(kv => kv.Key + ":" + kv.Value.Count));
                LogWriter.LogWrite("DayPlanCalendar refreshed: " + fresh.Values.Sum(v => v.Count) + " meetings across "
                    + fresh.Count + " days | today(" + todayK + ")=" + todayCount + " | next: " + breakdown);
                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DayPlanCalendar.RefreshAsync - " + ex.Message);
                return false;
            }
            finally
            {
                lock (_lock) { _refreshing = false; }
            }
        }

        /// <summary>
        /// Connected account emails for a platform. The backend returns
        /// <c>{ calendars: [{ account, requires_reauth, expired, items:[…] }], keywords:[…] }</c>;
        /// the events endpoint's <c>account</c> param is each entry's <c>account</c> (email). Accounts
        /// that need re-auth are skipped (their events can't be fetched).
        /// </summary>
        private async Task<List<string>> AccountsFor(FocusBearAPIClient client, string platform)
        {
            var accounts = new List<string>();
            try
            {
                string json = await client.GetCalendarDatas(platform);
                if (string.IsNullOrWhiteSpace(json)) return accounts;
                JArray cals = (ParseJson(json) as JObject)?["calendars"] as JArray;
                if (cals == null) { LogWriter.LogWrite("DayPlanCalendar.AccountsFor(" + platform + "): no 'calendars' array"); return accounts; }
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (JToken t in cals)
                {
                    if (!(t is JObject o)) continue;
                    if (o.Value<bool?>("requires_reauth") == true) continue;   // can't fetch — needs reconnect
                    string acct = o.Value<string>("account") ?? o.Value<string>("email");
                    if (!string.IsNullOrEmpty(acct) && seen.Add(acct)) accounts.Add(acct);
                }
                LogWriter.LogWrite("DayPlanCalendar.AccountsFor(" + platform + "): " + accounts.Count + " account(s)");
            }
            catch (Exception ex) { LogWriter.LogWrite("DayPlanCalendar.AccountsFor(" + platform + ") - " + ex.Message); }
            return accounts;
        }

        // ---- event parsing -----------------------------------------------------------------------

        /// <summary>
        /// Parse the events response. The endpoint returns the backend's NORMALIZED event shape (same for
        /// Google + Microsoft): <c>{ external_id, summary, event_begins, event_ends, platform, … }</c>,
        /// with <c>event_begins</c>/<c>event_ends</c> as ISO-UTC. All-day events are already dropped
        /// server-side. Falls back to raw Google/Microsoft field names defensively, in case the shape differs.
        /// </summary>
        private static IEnumerable<ScheduleMeetingInput> ParseEvents(string json, string platform)
        {
            var result = new List<ScheduleMeetingInput>();
            if (string.IsNullOrWhiteSpace(json)) return result;
            JArray arr;
            try { arr = AsArray(ParseJson(json)); }
            catch (Exception ex) { LogWriter.LogWrite("DayPlanCalendar.ParseEvents parse - " + ex.Message); return result; }
            if (arr == null)
            {
                LogWriter.LogWrite("DayPlanCalendar.ParseEvents(" + platform + "): no array (shape: " + Head(json) + ")");
                return result;
            }
            int noId = 0, noDate = 0;
            foreach (JToken t in arr)
            {
                try
                {
                    if (!(t is JObject o)) continue;
                    string id = o.Value<string>("external_id") ?? o.Value<string>("id");
                    if (string.IsNullOrEmpty(id)) { noId++; continue; }

                    // Normalized fields first, then raw Google/Microsoft as a fallback.
                    string beginsStr = o.Value<string>("event_begins")
                                       ?? (o["start"] as JObject)?.Value<string>("dateTime");
                    string endsStr = o.Value<string>("event_ends")
                                     ?? (o["end"] as JObject)?.Value<string>("dateTime");
                    if (!TryParseDate(beginsStr, (o["start"] as JObject)?.Value<string>("timeZone"), out DateTime s)) { noDate++; continue; }
                    if (!TryParseDate(endsStr, (o["end"] as JObject)?.Value<string>("timeZone"), out DateTime e)) e = s.AddMinutes(30);

                    string title = o.Value<string>("summary") ?? o.Value<string>("subject");
                    result.Add(new ScheduleMeetingInput(id, string.IsNullOrWhiteSpace(title) ? "(no title)" : title, s, e));
                }
                catch (Exception ex) { LogWriter.LogWrite("DayPlanCalendar.ParseEvents item - " + ex.Message); }
            }
            LogWriter.LogWrite("DayPlanCalendar.ParseEvents(" + platform + "): " + arr.Count + " raw -> " + result.Count
                + " meetings (dropped: noId=" + noId + ", noDate=" + noDate + ")");
            return result;
        }

        private static string Head(string s) => s == null ? "null" : (s.Length <= 200 ? s : s.Substring(0, 200));

        /// <summary>
        /// Parse an event timestamp to LOCAL time. Google gives an RFC3339 string with an offset
        /// (DateTimeOffset handles it). Microsoft gives an offset-less dateTime plus a timeZone name —
        /// treat "UTC" as UTC (the Graph default), otherwise best-effort as local.
        /// </summary>
        private static bool TryParseDate(string value, string timeZone, out DateTime local)
        {
            local = default(DateTime);
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (DateTimeOffset.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTimeOffset dto)
                && value.IndexOfAny(new[] { 'Z', '+' }) >= 0)   // had an explicit offset (Google)
            {
                local = dto.ToLocalTime().DateTime;
                return true;
            }
            if (DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.None, out DateTime dt))
            {
                if (!string.IsNullOrEmpty(timeZone) && timeZone.Equals("UTC", StringComparison.OrdinalIgnoreCase))
                    local = DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToLocalTime();
                else
                    local = dt;   // best-effort: treat as local wall-clock
                return true;
            }
            return false;
        }

        /// <summary>
        /// Parse JSON WITHOUT auto-converting ISO date strings to DateTime tokens. The default
        /// <see cref="JToken.Parse(string)"/> turns "2026-07-23T06:00:00.000Z" into a Date token and,
        /// when read back as a string, renders it in the local culture and DROPS the timezone
        /// ("07/23/2026 06:00:00") — which then fails to parse. Keeping it a raw string preserves the Z.
        /// </summary>
        private static JToken ParseJson(string json)
        {
            using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
                return JToken.ReadFrom(reader);
        }

        private static JArray AsArray(JToken root)
        {
            if (root is JArray a) return a;
            if (root is JObject o)
            {
                foreach (string k in new[] { "data", "events", "value", "items", "results" })
                    if (o[k] is JArray inner) return inner;
            }
            return null;
        }
    }
}
