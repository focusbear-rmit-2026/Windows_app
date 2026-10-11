using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.LateNoMore
{
    /// <summary>
    /// The user's Late No More settings. On the Mac these live in the standalone Late No More app's local
    /// <c>events.json</c> (<c>calendarsToExclude</c>, <c>eventKeywordsToIgnore</c>, <c>barkStartTime</c> /
    /// <c>barkEndTime</c>, <c>voiceOptions</c>) and on mobile in local redux state (<c>lateNoMoreNotificationsEnabled</c>,
    /// <c>lateNoMoreReminderTimes</c>, <c>lateNoMoreRequireMeetingUrl</c>): neither syncs. Windows stores it on the
    /// backend (<c>GET/PUT /late-no-more/config</c>) and keeps a local copy. The JSON is that endpoint's snake_case shape.
    /// </summary>
    public sealed class LateNoMoreConfig
    {
        /// <summary>The Mac app's two warnings: a heads-up 15 minutes out and the full alert 2 minutes out.</summary>
        public static readonly IReadOnlyList<int> DefaultLeadMinutes = new[] { 15, 2 };
        public const string DefaultActiveHoursStart = "07:00";
        public const string DefaultActiveHoursEnd = "21:00";
        /// <summary>The Mac app's default keyword ("no-notify" in an event title silences it).</summary>
        public const string DefaultIgnoredKeyword = "no-notify";

        public const int MaxLeadMinutes = 120;
        public const int MaxLeadCount = 5;
        public const int MaxKeywords = 50;
        public const int MaxKeywordLength = 100;
        public const int MaxCalendarIds = 200;

        public bool Enabled { get; set; } = true;
        /// <summary>Provider calendar ids to alert for; null means every synced calendar.</summary>
        public List<string> CalendarIds { get; set; }
        /// <summary>Minutes before a meeting to alert, largest first. The smallest is the full alert; the rest are heads-ups.</summary>
        public List<int> AlertLeadMinutes { get; set; } = DefaultLeadMinutes.ToList();
        public string ActiveHoursStart { get; set; } = DefaultActiveHoursStart;
        public string ActiveHoursEnd { get; set; } = DefaultActiveHoursEnd;
        public List<string> IgnoredKeywords { get; set; } = new List<string> { DefaultIgnoredKeyword };
        public bool RequireMeetingUrl { get; set; }
        public bool VerbalAlertsEnabled { get; set; } = true;
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public static LateNoMoreConfig Defaults() => new LateNoMoreConfig();

        public LateNoMoreConfig Clone() => FromJson(ToJson()) ?? Defaults();

        /// <summary>The full-alert lead (the smallest), or null when no lead is configured.</summary>
        public int? MainAlertMinutes => AlertLeadMinutes == null || AlertLeadMinutes.Count == 0 ? (int?)null : AlertLeadMinutes.Min();

        /// <summary>The heads-up lead (the largest one above the main alert), or null for none.</summary>
        public int? HeadsUpMinutes
        {
            get
            {
                if (AlertLeadMinutes == null || AlertLeadMinutes.Count < 2) return null;
                return AlertLeadMinutes.Max();
            }
        }

        /// <summary>Sets the two leads the settings screen edits; a null or non-larger heads-up means none.</summary>
        public void SetLeads(int mainMinutes, int? headsUpMinutes)
        {
            var leads = new List<int> { mainMinutes };
            if (headsUpMinutes.HasValue && headsUpMinutes.Value > mainMinutes) leads.Add(headsUpMinutes.Value);
            AlertLeadMinutes = leads;
            Normalize();
        }

        /// <summary>
        /// Clamps and tidies every field into what the backend accepts: leads 0..120, unique, at most five, largest
        /// first (defaults if none are left); keywords trimmed, 1..100 characters, de-duplicated ignoring case, at most
        /// fifty; hours as HH:mm (defaults when unparseable); calendar ids de-duplicated, blanks dropped.
        /// </summary>
        public LateNoMoreConfig Normalize()
        {
            AlertLeadMinutes = (AlertLeadMinutes ?? new List<int>())
                .Where(m => m >= 0 && m <= MaxLeadMinutes).Distinct().OrderByDescending(m => m).Take(MaxLeadCount).ToList();
            if (AlertLeadMinutes.Count == 0) AlertLeadMinutes = DefaultLeadMinutes.ToList();

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IgnoredKeywords = (IgnoredKeywords ?? new List<string>())
                .Select(k => (k ?? "").Trim())
                .Where(k => k.Length > 0 && k.Length <= MaxKeywordLength && seen.Add(k))
                .Take(MaxKeywords).ToList();

            ActiveHoursStart = TryParseClock(ActiveHoursStart, out int start) ? FormatClock(start) : DefaultActiveHoursStart;
            ActiveHoursEnd = TryParseClock(ActiveHoursEnd, out int end) ? FormatClock(end) : DefaultActiveHoursEnd;

            if (CalendarIds != null)
                CalendarIds = CalendarIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).Take(MaxCalendarIds).ToList();
            return this;
        }

        /// <summary>Adds a keyword (trimmed); false when it is blank, too long, a duplicate, or the list is full.</summary>
        public bool AddKeyword(string keyword)
        {
            string k = (keyword ?? "").Trim();
            if (k.Length == 0 || k.Length > MaxKeywordLength) return false;
            IgnoredKeywords = IgnoredKeywords ?? new List<string>();
            if (IgnoredKeywords.Count >= MaxKeywords) return false;
            if (IgnoredKeywords.Any(x => string.Equals(x, k, StringComparison.OrdinalIgnoreCase))) return false;
            IgnoredKeywords.Add(k);
            return true;
        }

        public bool RemoveKeyword(string keyword) =>
            IgnoredKeywords != null && IgnoredKeywords.RemoveAll(x => string.Equals(x, keyword, StringComparison.OrdinalIgnoreCase)) > 0;

        /// <summary>
        /// Whether a calendar is alerted for. <paramref name="allCalendarIds"/> is every synced calendar, needed to turn
        /// "all" (null) into an explicit list the first time one is switched off.
        /// </summary>
        public bool IsCalendarOn(string calendarId) => CalendarIds == null || (calendarId != null && CalendarIds.Contains(calendarId));

        public void SetCalendarOn(string calendarId, bool on, IEnumerable<string> allCalendarIds)
        {
            if (string.IsNullOrEmpty(calendarId)) return;
            var all = (allCalendarIds ?? Enumerable.Empty<string>()).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
            var current = CalendarIds ?? all.ToList();
            if (on) { if (!current.Contains(calendarId)) current.Add(calendarId); }
            else current.Remove(calendarId);
            // Every calendar on again is stored as "all", so a calendar connected later is included by default.
            CalendarIds = all.Count > 0 && all.All(current.Contains) ? null : current;
        }

        // ------------------------------------------------------------------ clock strings

        /// <summary>Parses "HH:mm" (24 h) into minutes after midnight.</summary>
        public static bool TryParseClock(string value, out int minutes)
        {
            minutes = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;
            var parts = value.Trim().Split(':');
            if (parts.Length != 2) return false;
            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int h)) return false;
            if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int m)) return false;
            if (parts[1].Length != 2 || h < 0 || h > 23 || m < 0 || m > 59) return false;
            minutes = h * 60 + m;
            return true;
        }

        public static string FormatClock(int minutes)
        {
            minutes = ((minutes % 1440) + 1440) % 1440;
            return (minutes / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (minutes % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Whether a meeting starting at <paramref name="localStart"/> falls in the alert hours. The Mac only reads
        /// meetings between <c>barkStartTime</c> and <c>barkEndTime</c> (inclusive). An end before the start wraps past midnight.
        /// </summary>
        public bool IsWithinActiveHours(DateTime localStart)
        {
            if (!TryParseClock(ActiveHoursStart, out int from)) from = 0;
            if (!TryParseClock(ActiveHoursEnd, out int to)) to = 1439;
            int at = localStart.Hour * 60 + localStart.Minute;
            if (from == to) return true;
            return from < to ? at >= from && at <= to : at >= from || at <= to;
        }

        // ------------------------------------------------------------------ JSON (the backend's snake_case shape)

        public string ToJson() => ToJObject().ToString(Formatting.None);

        public JObject ToJObject() => new JObject
        {
            ["enabled"] = Enabled,
            ["calendar_ids"] = CalendarIds == null ? JValue.CreateNull() : new JArray(CalendarIds),
            ["alert_lead_minutes"] = new JArray(AlertLeadMinutes ?? new List<int>()),
            ["active_hours_start"] = ActiveHoursStart,
            ["active_hours_end"] = ActiveHoursEnd,
            ["ignored_keywords"] = new JArray(IgnoredKeywords ?? new List<string>()),
            ["require_meeting_url"] = RequireMeetingUrl,
            ["verbal_alerts_enabled"] = VerbalAlertsEnabled,
            ["created_at"] = CreatedAt.HasValue ? (JToken)CreatedAt.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) : JValue.CreateNull(),
            ["updated_at"] = UpdatedAt.HasValue ? (JToken)UpdatedAt.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) : JValue.CreateNull(),
        };

        /// <summary>The body a PUT sends: every editable field, without the server's timestamps.</summary>
        public string ToUpdateJson()
        {
            var o = ToJObject();
            o.Remove("created_at");
            o.Remove("updated_at");
            return o.ToString(Formatting.None);
        }

        /// <summary>Reads the endpoint's (or the local copy's) JSON; missing fields keep their defaults. Null when unreadable.</summary>
        public static LateNoMoreConfig FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            JObject o;
            try
            {
                using (var reader = new JsonTextReader(new System.IO.StringReader(json)) { DateParseHandling = DateParseHandling.None })
                    o = JToken.ReadFrom(reader) as JObject;
            }
            catch (JsonException) { return null; }
            if (o == null) return null;
            // Tolerate a wrapped response ({ data: {...} }).
            if (o["enabled"] == null && o["data"] is JObject inner) o = inner;

            var c = Defaults();
            if (o["enabled"]?.Type == JTokenType.Boolean) c.Enabled = o.Value<bool>("enabled");
            if (o["calendar_ids"] is JArray ids) c.CalendarIds = ids.Where(t => t.Type == JTokenType.String).Select(t => (string)t).ToList();
            else if (o["calendar_ids"] != null && o["calendar_ids"].Type == JTokenType.Null) c.CalendarIds = null;
            if (o["alert_lead_minutes"] is JArray leads)
                c.AlertLeadMinutes = leads.Where(t => t.Type == JTokenType.Integer || t.Type == JTokenType.Float).Select(t => (int)Math.Round((double)t)).ToList();
            if (o["active_hours_start"]?.Type == JTokenType.String) c.ActiveHoursStart = (string)o["active_hours_start"];
            if (o["active_hours_end"]?.Type == JTokenType.String) c.ActiveHoursEnd = (string)o["active_hours_end"];
            if (o["ignored_keywords"] is JArray words) c.IgnoredKeywords = words.Where(t => t.Type == JTokenType.String).Select(t => (string)t).ToList();
            if (o["require_meeting_url"]?.Type == JTokenType.Boolean) c.RequireMeetingUrl = o.Value<bool>("require_meeting_url");
            if (o["verbal_alerts_enabled"]?.Type == JTokenType.Boolean) c.VerbalAlertsEnabled = o.Value<bool>("verbal_alerts_enabled");
            c.CreatedAt = ReadDate(o["created_at"]);
            c.UpdatedAt = ReadDate(o["updated_at"]);
            return c.Normalize();
        }

        private static DateTime? ReadDate(JToken t)
        {
            if (t == null || t.Type != JTokenType.String) return null;
            return DateTimeOffset.TryParse((string)t, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset d)
                ? d.UtcDateTime : (DateTime?)null;
        }
    }
}
