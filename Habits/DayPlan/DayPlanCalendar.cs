using Focus_Bear.API;
using Focus_Bear.Utils;
using FocusBear.Core.LateNoMore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>The calendar reads the Day Plan makes (GET only). The app's API client in the app; tests fake it.</summary>
    internal interface IDayPlanCalendarApi
    {
        /// <summary><c>GET /calendar/{platform}/calendar-datas</c>: the body, or null when the read failed.</summary>
        Task<string> GetCalendarDatas(string platform);
        /// <summary><c>GET /calendar/{platform}/events</c>: the body, or null when the read failed.</summary>
        Task<string> GetCalendarEvents(string platform, string account, string range = null);
        Task<string> GetEventColours();
    }

    internal sealed class DayPlanCalendarApi : IDayPlanCalendarApi
    {
        private readonly FocusBearAPIClient _client = new FocusBearAPIClient();
        public Task<string> GetCalendarDatas(string platform) => _client.GetCalendarDatas(platform);
        public Task<string> GetCalendarEvents(string platform, string account, string range = null) => _client.GetCalendarEvents(platform, account, range);
        public Task<string> GetEventColours() => _client.GetEventColours();
    }

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
        /// <summary>
        /// Days of meetings the Day Plan keeps loaded from today (the backend allows up to 90). Wide enough
        /// that the date picker can colour the rest of this month and most of next without another read.
        /// </summary>
        public const int DefaultWindowDays = 45;
        public const int MaxWindowDays = 90;
        /// <summary>At most this many single-day reads per date-picker month (past the 90-day window).</summary>
        public const int MaxSingleDayReads = 31;
        /// <summary>A day read more recently than this is not read again for the date picker.</summary>
        public const int DayFreshSeconds = 15 * 60;

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
        // The widest window loaded so far today, and when — so the reschedule plan can tell whether its whole
        // 90-day horizon is in the cache (report 2026-09-25_20-18-29 follow-up).
        private DateTime _windowLoadedFor = DateTime.MinValue;
        private int _windowLoadedDays;
        private DateTime _windowLoadedUtc = DateTime.MinValue;
        // When each day's meetings were last read in full (every account answered), by day key. A day with no
        // entry is UNKNOWN: its empty meeting list is not "a free day" (the date picker leaves it unbadged).
        private readonly Dictionary<string, DateTime> _loadedAtUtc = new Dictionary<string, DateTime>();
        // Days a single-day read is already running for, so paging back and forth never doubles a request.
        private readonly HashSet<string> _singleDayInFlight = new HashSet<string>();
        // Late No More's view of the same events (calendar id, join link, declined), by day key, merged exactly
        // like _byDay. Filled by RefreshAsync only — Late No More reads the next hours, never the date picker's days.
        private Dictionary<string, List<LateNoMoreEvent>> _lnmByDay = new Dictionary<string, List<LateNoMoreEvent>>();
        // The calendars each platform's calendar-datas listed on the last read (AccountsFor reads it anyway).
        private readonly Dictionary<string, List<LateNoMoreCalendar>> _calendarsByPlatform = new Dictionary<string, List<LateNoMoreCalendar>>();

        /// <summary>True once the day's meetings have been read (at any age).</summary>
        public bool IsDayLoaded(DateTime date)
        {
            lock (_lock) { return _loadedAtUtc.ContainsKey(DayKey(date)); }
        }

        /// <summary>When the day's meetings were last read in full, or null if never.</summary>
        public DateTime? DayLoadedAtUtc(DateTime date)
        {
            lock (_lock) { return _loadedAtUtc.TryGetValue(DayKey(date), out DateTime at) ? at : (DateTime?)null; }
        }

        /// <summary>A day that was never read, or was read longer ago than <see cref="DayFreshSeconds"/>.</summary>
        public bool DayNeedsLoad(DateTime date)
        {
            lock (_lock)
            {
                string k = DayKey(date);
                if (_singleDayInFlight.Contains(k)) return false;
                return !_loadedAtUtc.TryGetValue(k, out DateTime at) || (DateTime.UtcNow - at).TotalSeconds >= DayFreshSeconds;
            }
        }

        /// <summary>
        /// Make sure meetings for today .. today+<paramref name="days"/>-1 are loaded, fetching them unless a
        /// load that wide finished within <paramref name="maxAgeSeconds"/>. Waits for a refresh already running
        /// (up to 30 s) instead of skipping, so the caller never prices days from a partial cache.
        /// </summary>
        public async Task EnsureWindowAsync(int days, int maxAgeSeconds = 600)
        {
            days = Math.Max(1, Math.Min(MaxWindowDays, days));
            for (int i = 0; i < 120; i++)
            {
                lock (_lock) { if (!_refreshing) break; }
                await Task.Delay(250);
            }
            lock (_lock)
            {
                if (_windowLoadedFor == DateTime.Today && _windowLoadedDays >= days
                    && (DateTime.UtcNow - _windowLoadedUtc).TotalSeconds < maxAgeSeconds) return;
            }
            await RefreshAsync(true, 0, null, days);
        }

        /// <summary>True when today .. today+<paramref name="days"/>-1 was loaded within <paramref name="maxAgeSeconds"/>.</summary>
        public bool IsWindowFresh(int days, int maxAgeSeconds)
        {
            lock (_lock)
            {
                return _windowLoadedFor == DateTime.Today && _windowLoadedDays >= Math.Min(MaxWindowDays, days)
                       && (DateTime.UtcNow - _windowLoadedUtc).TotalSeconds < maxAgeSeconds;
            }
        }

        /// <summary>Days the Day Plan's frequent refresh (timer, day change) keeps fresh; the wide window is reloaded less often.</summary>
        public const int NearWindowDays = 7;
        /// <summary>How often the wide <see cref="DefaultWindowDays"/> window is reloaded (it's ~1.7 MB on a busy calendar).</summary>
        public const int WideWindowMaxAgeSeconds = 900;

        private DayPlanCalendarManager() { }

        /// <summary>A manager of its own over a fake API (tests); the app uses <see cref="Shared"/>.</summary>
        internal DayPlanCalendarManager(IDayPlanCalendarApi api) { _api = api; }

        private readonly IDayPlanCalendarApi _api;
        // The E2E build reads a JSON fixture instead when the test runner sets E2EBuild.CalendarFixtureVariable.
        private IDayPlanCalendarApi Api => _api ?? (E2EBuild.CalendarFixturePath != null
            ? (IDayPlanCalendarApi)new DayPlanCalendarFixtureApi(E2EBuild.CalendarFixturePath)
            : new DayPlanCalendarApi());

        private static string DayKey(DateTime d) => FocusBear.Core.Schedule.DayPlanDayKey.For(d);

        /// <summary>
        /// Cached meetings for a day, each tagged with its dismissed state. Dismissed meetings are omitted
        /// unless <paramref name="includeDismissed"/> is true (the "show dismissed events" toggle).
        /// </summary>
        public List<ScheduleMeetingInput> MeetingsFor(DateTime date, bool includeDismissed = false, bool untrimmed = false)
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
                    // A meeting shortened for this day from the Day Plan's Fix keeps its start and ends earlier
                    // on the plan (never on the calendar).
                    DateTime end = untrimmed ? m.End : DayPlanMeetingTrims.TrimmedEnd(m.Id, date, m.Start, m.End);
                    result.Add(new ScheduleMeetingInput(m.Id, m.Title, m.Start, end, isDismissed, colour));
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
        private async Task LoadColours(IDayPlanCalendarApi client)
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

        /// <summary>What the Day Plan draws from the cache (every meeting's id, times and title, and the colours). Under the lock.</summary>
        private string RenderSignature()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var kv in _byDay.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                sb.Append(kv.Key).Append('{');
                foreach (ScheduleMeetingInput m in kv.Value)
                    sb.Append(m.Id).Append('|').Append(m.Start.Ticks).Append('|').Append(m.End.Ticks).Append('|').Append(m.Title).Append(';');
                sb.Append('}');
            }
            foreach (var kv in _colourByEvent.OrderBy(k => k.Key, StringComparer.Ordinal)) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(';');
            // Also what the plan draws beyond the times: the join link, declined and platform (Late No More's detail),
            // and the dismissed set. The signature covered only the times, titles and colours, so a meeting that gained
            // its link, or one dismissed elsewhere, left the plan as it was.
            foreach (var kv in _lnmByDay.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                sb.Append('L').Append(kv.Key).Append('{');
                foreach (LateNoMoreEvent e in kv.Value.OrderBy(e => e.Id, StringComparer.Ordinal).ThenBy(e => e.Start))
                    sb.Append(e.Id).Append('|').Append(e.MeetingUrl).Append('|').Append(e.IsDeclined ? 'd' : '-').Append('|').Append(e.Platform).Append(';');
                sb.Append('}');
            }
            foreach (var kv in _dismissedByDay.OrderBy(k => k.Key, StringComparer.Ordinal))
                sb.Append('D').Append(kv.Key).Append('=').Append(string.Join(",", kv.Value.OrderBy(x => x, StringComparer.Ordinal))).Append(';');
            return sb.ToString();
        }

        /// <summary>Whether <paramref name="change"/> alters what the Day Plan would draw (the tests' view of the signature).</summary>
        internal bool RenderSignatureChangedBy(Action change)
        {
            string before;
            lock (_lock) before = RenderSignature();
            change();
            lock (_lock) return RenderSignature() != before;
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
        public async Task<bool> RefreshAsync(bool force = false, int minGapSeconds = 45, DateTime? viewedDay = null, int windowDays = DefaultWindowDays)
        {
            lock (_lock)
            {
                if (_refreshing) return false;
                if (!force && (DateTime.UtcNow - _lastRefreshUtc).TotalSeconds < minGapSeconds) return false;
                _refreshing = true;
            }
            string before;
            lock (_lock) before = RenderSignature();
            try
            {
                var fresh = new Dictionary<string, List<ScheduleMeetingInput>>();
                var freshLnm = new Dictionary<string, List<LateNoMoreEvent>>();
                var seenIds = new HashSet<string>();
                var seenLnm = new HashSet<string>();
                var client = Api;

                // Without a range the backend returns today only, so other days showed no meetings (report
                // 2026-09-25_14-31-18 rework). Fetch today + the next windowDays-1 days, plus the viewed day
                // when it is outside that window (a past day, or further out).
                DateTime today = DateTime.Today;
                windowDays = Math.Max(1, Math.Min(MaxWindowDays, windowDays));
                var fetched = new HashSet<string>();
                for (int i = 0; i < windowDays; i++) fetched.Add(DayKey(today.AddDays(i)));
                var ranges = new List<string> { "days=" + windowDays };
                if (viewedDay.HasValue && !fetched.Contains(DayKey(viewedDay.Value.Date)))
                {
                    ranges.Add("day=" + DayKey(viewedDay.Value.Date));
                    fetched.Add(DayKey(viewedDay.Value.Date));
                }
                // A range counts as read only when every account answered it; a failed read leaves its days
                // unknown rather than recording them as meeting-free.
                bool windowOk = true, windowFellBack = false, viewedOk = true, changed;
                long parseMs = 0;
                var refreshClock = System.Diagnostics.Stopwatch.StartNew();

                foreach (string platform in Platforms)
                {
                    List<string> accounts = await AccountsFor(client, platform);
                    // calendar-datas failed (offline, a timeout): this platform's meetings are unknown, not gone.
                    if (accounts == null) { windowOk = false; viewedOk = false; continue; }
                    foreach (string account in accounts)
                    {
                        foreach (string range in ranges)
                        {
                            string json = await client.GetCalendarEvents(platform, account, range);
                            bool isWindow = range.StartsWith("days=", StringComparison.Ordinal);
                            // An older backend without day/days: fall back to today only.
                            if (json == null && isWindow)
                            {
                                json = await client.GetCalendarEvents(platform, account);
                                windowFellBack = true;
                            }
                            if (json == null) { if (isWindow) windowOk = false; else viewedOk = false; }
                            // A 45-90 day window is megabytes of JSON: parse it off the UI thread.
                            var parseClock = System.Diagnostics.Stopwatch.StartNew();
                            string body = json, plat = platform;
                            var details = new List<LateNoMoreEvent>();
                            var parsed = await Task.Run(() => ParseEvents(body, plat, details));
                            parseMs += parseClock.ElapsedMilliseconds;
                            foreach (ScheduleMeetingInput m in parsed)
                            {
                                if (!seenIds.Add(m.Id + "|" + m.Start.ToString("o"))) continue;   // de-dupe across accounts and ranges
                                // Filed under every day it covers: 23:30-00:30 also blocks the next morning.
                                foreach (string k in FocusBear.Core.Schedule.DayPlanDayKey.DaysCovered(m.Start, m.End))
                                {
                                    if (!fresh.TryGetValue(k, out List<ScheduleMeetingInput> list))
                                    {
                                        list = new List<ScheduleMeetingInput>();
                                        fresh[k] = list;
                                    }
                                    list.Add(m);
                                }
                            }
                            foreach (LateNoMoreEvent d in details)
                            {
                                if (!seenLnm.Add(d.Id + "|" + d.Start.ToString("o"))) continue;
                                string k = DayKey(d.Start);
                                if (!freshLnm.TryGetValue(k, out List<LateNoMoreEvent> dl)) { dl = new List<LateNoMoreEvent>(); freshLnm[k] = dl; }
                                dl.Add(d);
                            }
                        }
                    }
                }
                foreach (var kv in fresh) kv.Value.Sort((a, b) => a.Start.CompareTo(b.Start));

                await LoadColours(client);

                lock (_lock)
                {
                    // Days outside what was read keep their cached meetings (e.g. the 90-day window the reschedule
                    // plan loaded). Only days read in full are replaced (so a deleted meeting goes). A read that failed for any
                    // account leaves its days as they were: a network blip must not empty the Day Plan, or stop
                    // Late No More alerting, until the next refresh (log 2026-09-26 17:16:38, "0 meetings").
                    var replaced = new HashSet<string>();
                    if (windowOk)
                    {
                        if (windowFellBack) replaced.Add(DayKey(today));
                        else for (int i = 0; i < windowDays; i++) replaced.Add(DayKey(today.AddDays(i)));
                    }
                    if (viewedOk && ranges.Count > 1) replaced.Add(DayKey(viewedDay.Value.Date));
                    var merged = new Dictionary<string, List<ScheduleMeetingInput>>();
                    foreach (var kv in _byDay)
                        if (!replaced.Contains(kv.Key)) merged[kv.Key] = kv.Value;
                    foreach (var kv in fresh)
                    {
                        if (replaced.Contains(kv.Key) || !merged.ContainsKey(kv.Key)) { merged[kv.Key] = kv.Value; continue; }
                        // A day outside the read that a meeting from the last read day runs into: add it there.
                        var extended = new List<ScheduleMeetingInput>(merged[kv.Key]);
                        foreach (ScheduleMeetingInput m in kv.Value)
                            if (!extended.Any(x => x.Id == m.Id && x.Start == m.Start)) extended.Add(m);
                        extended.Sort((a, b) => a.Start.CompareTo(b.Start));
                        merged[kv.Key] = extended;
                    }
                    _byDay = merged;
                    fresh = merged;
                    var mergedLnm = new Dictionary<string, List<LateNoMoreEvent>>();
                    foreach (var kv in _lnmByDay)
                        if (!replaced.Contains(kv.Key)) mergedLnm[kv.Key] = kv.Value;
                    foreach (var kv in freshLnm)
                        if (replaced.Contains(kv.Key) || !mergedLnm.ContainsKey(kv.Key)) mergedLnm[kv.Key] = kv.Value;
                    _lnmByDay = mergedLnm;
                    _lastRefreshUtc = DateTime.UtcNow;
                    foreach (string k in replaced) _loadedAtUtc[k] = _lastRefreshUtc;
                    bool stale = _windowLoadedFor != today || (DateTime.UtcNow - _windowLoadedUtc).TotalSeconds >= WideWindowMaxAgeSeconds;
                    // A partial read doesn't count as the window loaded: EnsureWindowAsync would otherwise trust it.
                    if (windowOk && !windowFellBack && (stale || windowDays >= _windowLoadedDays))
                    {
                        _windowLoadedFor = today;
                        _windowLoadedDays = windowDays;
                        _windowLoadedUtc = DateTime.UtcNow;
                    }
                    changed = RenderSignature() != before;
                }
                string todayK = DayKey(DateTime.Today);
                int todayCount = fresh.TryGetValue(todayK, out List<ScheduleMeetingInput> tl) ? tl.Count : 0;
                string breakdown = string.Join(", ", fresh
                    .Where(kv => string.CompareOrdinal(kv.Key, todayK) >= 0)
                    .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                    .Take(8).Select(kv => kv.Key + ":" + kv.Value.Count));
                LogWriter.LogWrite("DayPlanCalendar refreshed: " + fresh.Values.Sum(v => v.Count) + " meetings across "
                    + fresh.Count + " days | today(" + todayK + ")=" + todayCount + " | next: " + breakdown
                    + " | window " + windowDays + " days in " + refreshClock.ElapsedMilliseconds + " ms (parse " + parseMs + " ms, off the UI thread)"
                    + (changed ? "" : " | unchanged"));
                // Only a change the Day Plan draws is reported, so its 90 s poll doesn't rebuild the page for nothing.
                return changed;
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
        /// Read the meetings of days outside the rolling window (<c>day=yyyy-MM-dd</c>, one request per day and
        /// account, a few at a time), for the date picker's month view. At most <see cref="MaxSingleDayReads"/>
        /// days; days read recently or already being read are skipped. Merges like <see cref="RefreshAsync"/>:
        /// only the days read are replaced. Returns the days that were read in full.
        /// </summary>
        public async Task<List<DateTime>> LoadDaysAsync(IEnumerable<DateTime> days)
        {
            var wanted = new List<DateTime>();
            lock (_lock)
            {
                foreach (DateTime d in (days ?? Enumerable.Empty<DateTime>()).Select(d => d.Date).Distinct())
                {
                    if (wanted.Count >= MaxSingleDayReads) break;
                    string k = DayKey(d);
                    if (_singleDayInFlight.Contains(k)) continue;
                    if (_loadedAtUtc.TryGetValue(k, out DateTime at) && (DateTime.UtcNow - at).TotalSeconds < DayFreshSeconds) continue;
                    _singleDayInFlight.Add(k);
                    wanted.Add(d);
                }
            }
            var done = new List<DateTime>();
            if (wanted.Count == 0) return done;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var client = Api;
                var accounts = new List<KeyValuePair<string, string>>();
                foreach (string platform in Platforms)
                {
                    List<string> listed = await AccountsFor(client, platform);
                    // calendar-datas failed: no day can be read in full, and none may be recorded as free.
                    if (listed == null) { LogWriter.LogWrite("DayPlanCalendar.LoadDaysAsync: calendar-datas(" + platform + ") failed - days left unknown"); return done; }
                    foreach (string account in listed)
                        accounts.Add(new KeyValuePair<string, string>(platform, account));
                }

                // Four days at a time: a month past the window is ~30 requests per account.
                const int parallel = 4;
                for (int start = 0; start < wanted.Count; start += parallel)
                {
                    var batch = wanted.Skip(start).Take(parallel).ToList();
                    var reads = batch.Select(async day =>
                    {
                        var list = new List<ScheduleMeetingInput>();
                        var seen = new HashSet<string>();
                        bool ok = true;
                        foreach (var acct in accounts)
                        {
                            string json = await client.GetCalendarEvents(acct.Key, acct.Value, "day=" + DayKey(day));
                            if (json == null) { ok = false; continue; }
                            foreach (ScheduleMeetingInput m in ParseEvents(json, acct.Key))
                                if (FocusBear.Core.Schedule.DayPlanDayKey.DaysCovered(m.Start, m.End).Contains(DayKey(day)) && seen.Add(m.Id + "|" + m.Start.ToString("o"))) list.Add(m);
                        }
                        list.Sort((a, b) => a.Start.CompareTo(b.Start));
                        return new { Day = day, Ok = ok, Meetings = list };
                    }).ToList();
                    foreach (var r in await Task.WhenAll(reads))
                    {
                        if (!r.Ok) continue;   // a failed read leaves the day unknown (and its cached meetings alone)
                        lock (_lock)
                        {
                            var merged = new Dictionary<string, List<ScheduleMeetingInput>>(_byDay);
                            if (r.Meetings.Count > 0) merged[DayKey(r.Day)] = r.Meetings; else merged.Remove(DayKey(r.Day));
                            _byDay = merged;
                            _loadedAtUtc[DayKey(r.Day)] = DateTime.UtcNow;
                        }
                        done.Add(r.Day);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DayPlanCalendar.LoadDaysAsync - " + ex.Message);
            }
            finally
            {
                lock (_lock) { foreach (DateTime d in wanted) _singleDayInFlight.Remove(DayKey(d)); }
            }
            LogWriter.LogWrite("DayPlanCalendar.LoadDaysAsync: " + done.Count + "/" + wanted.Count + " days read in " + clock.ElapsedMilliseconds + " ms");
            return done;
        }

        /// <summary>
        /// Connected account emails for a platform. The backend returns
        /// <c>{ calendars: [{ account, requires_reauth, expired, items:[…] }], keywords:[…] }</c>;
        /// the events endpoint's <c>account</c> param is each entry's <c>account</c> (email). Accounts
        /// that need re-auth are skipped (their events can't be fetched). Null when the read failed, so the
        /// caller can tell "no calendar connected" from "couldn't ask".
        /// </summary>
        private async Task<List<string>> AccountsFor(IDayPlanCalendarApi client, string platform)
        {
            var accounts = new List<string>();
            try
            {
                string json = await client.GetCalendarDatas(platform);
                if (json == null) return null;
                if (string.IsNullOrWhiteSpace(json)) return accounts;
                JToken root = ParseJson(json);
                var listed = LateNoMoreCalendar.ParseCalendarDatas(root, platform);
                lock (_lock) { _calendarsByPlatform[platform] = listed; }
                JArray cals = (root as JObject)?["calendars"] as JArray;
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
            catch (Exception ex) { LogWriter.LogWrite("DayPlanCalendar.AccountsFor(" + platform + ") - " + ex.Message); return null; }
            return accounts;
        }

        // ---- Late No More -------------------------------------------------------------------------

        /// <summary>When the cache was last refreshed (by the Day Plan or Late No More), UTC.</summary>
        public DateTime LastRefreshUtc { get { lock (_lock) return _lastRefreshUtc; } }

        /// <summary>
        /// Cached meetings starting in [<paramref name="from"/>, <paramref name="to"/>) with their calendar, join link
        /// and declined state, for Late No More. Calendar names come from the last calendar-datas read.
        /// </summary>
        public List<LateNoMoreEvent> LateNoMoreEvents(DateTime from, DateTime to)
        {
            lock (_lock)
            {
                var names = _calendarsByPlatform.Values.SelectMany(l => l)
                    .GroupBy(c => c.CalendarId).ToDictionary(g => g.Key, g => g.First().Name);
                var result = new List<LateNoMoreEvent>();
                for (DateTime day = from.Date; day <= to.Date; day = day.AddDays(1))
                {
                    if (!_lnmByDay.TryGetValue(DayKey(day), out List<LateNoMoreEvent> list)) continue;
                    foreach (LateNoMoreEvent e in list)
                        if (e.Start >= from && e.Start < to)
                            result.Add(e.CalendarId != null && names.TryGetValue(e.CalendarId, out string n) ? e.WithCalendarName(n) : e);
                }
                result.Sort((a, b) => a.Start.CompareTo(b.Start));
                return result;
            }
        }

        /// <summary>The calendars listed on the last calendar-datas read, every platform.</summary>
        public List<LateNoMoreCalendar> KnownCalendars()
        {
            lock (_lock) return _calendarsByPlatform.Values.SelectMany(l => l).ToList();
        }

        /// <summary>Reads calendar-datas for every platform now (GET only) and returns the calendars it lists.</summary>
        public async Task<List<LateNoMoreCalendar>> LoadCalendarsAsync()
        {
            var client = Api;
            foreach (string platform in Platforms) await AccountsFor(client, platform);
            return KnownCalendars();
        }

        // ---- event parsing -----------------------------------------------------------------------

        /// <summary>
        /// Parse the events response. The endpoint returns the backend's NORMALIZED event shape (same for
        /// Google + Microsoft): <c>{ external_id, summary, event_begins, event_ends, platform, … }</c>,
        /// with <c>event_begins</c>/<c>event_ends</c> as ISO-UTC. All-day events are already dropped
        /// server-side. Falls back to raw Google/Microsoft field names defensively, in case the shape differs.
        /// </summary>
        private static IEnumerable<ScheduleMeetingInput> ParseEvents(string json, string platform, List<LateNoMoreEvent> details = null)
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
                    details?.Add(new LateNoMoreEvent(id, string.IsNullOrWhiteSpace(title) ? "(no title)" : title, s, e,
                        o.Value<string>("calendar_id"), platform, MeetingLink.FromBackendEvent(o), MeetingLink.IsDeclined(o)));
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
