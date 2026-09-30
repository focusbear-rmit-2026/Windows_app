using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.LateNoMore
{
    /// <summary>One calendar meeting as Late No More needs it: what the Day Plan keeps, plus its calendar and link.</summary>
    public sealed class LateNoMoreEvent
    {
        public LateNoMoreEvent(string id, string title, DateTime start, DateTime end, string calendarId = null,
            string platform = null, string meetingUrl = null, bool isDeclined = false, string calendarName = null)
        {
            Id = id ?? "";
            Title = title ?? "";
            Start = start;
            End = end < start ? start : end;
            CalendarId = calendarId;
            Platform = platform;
            MeetingUrl = meetingUrl;
            IsDeclined = isDeclined;
            CalendarName = calendarName;
        }

        public string Id { get; }
        public string Title { get; }
        /// <summary>Local time.</summary>
        public DateTime Start { get; }
        /// <summary>Local time.</summary>
        public DateTime End { get; }
        /// <summary>The provider's calendar id (the backend's <c>calendar_id</c>), when known.</summary>
        public string CalendarId { get; }
        public string Platform { get; }
        /// <summary>The join link, or null when none could be found.</summary>
        public string MeetingUrl { get; }
        /// <summary>The user said no to the invitation.</summary>
        public bool IsDeclined { get; }
        public string CalendarName { get; }

        /// <summary>Identity for "already alerted" bookkeeping: a moved meeting is a new one.</summary>
        public string Key => Id + "|" + Start.ToString("yyyy-MM-ddTHH:mm");

        public LateNoMoreEvent WithCalendarName(string name) =>
            new LateNoMoreEvent(Id, Title, Start, End, CalendarId, Platform, MeetingUrl, IsDeclined, name);

        public LateNoMoreMeeting AsMeeting() => new LateNoMoreMeeting(Id, Title, Start);
    }

    /// <summary>
    /// Finds a meeting's join link. Order follows the mobile app's <c>extractMeetingUrl</c> (dedicated fields, then
    /// conference entry points), then the text fields (location, description) scanned with the Mac app's
    /// <c>parseMeetingNotes</c> patterns (Google Meet, Zoom, Teams, Webex, Jitsi, Skype) plus mobile's other domains.
    /// </summary>
    public static class MeetingLink
    {
        private static readonly Regex[] Patterns =
        {
            new Regex(@"https://meet\.google\.com/[a-z0-9\-]+", RegexOptions.IgnoreCase),
            new Regex(@"https://[\w\.]*zoom\.us/(j|my|w)/[^\s<>""')]+", RegexOptions.IgnoreCase),
            new Regex(@"https://teams\.(microsoft\.com|live\.com)/[^\s<>""')]+", RegexOptions.IgnoreCase),
            new Regex(@"https://[a-zA-Z0-9\-]+\.webex\.com/[^\s<>""')]+", RegexOptions.IgnoreCase),
            new Regex(@"https://meet\.jit\.si/[a-zA-Z0-9\-]+", RegexOptions.IgnoreCase),
            new Regex(@"https://join\.skype\.com/[a-zA-Z0-9]+", RegexOptions.IgnoreCase),
            new Regex(@"https://([\w\-]+\.)?(gotomeeting\.com|gotomeet\.me|bluejeans\.com|whereby\.com|around\.co|chime\.aws)/[^\s<>""')]+", RegexOptions.IgnoreCase),
        };

        private static readonly Regex AnyUrl = new Regex(@"https?://[^\s<>""')]+", RegexOptions.IgnoreCase);

        /// <summary>The first known meeting link in free text (notes, description, location), or null.</summary>
        public static string FromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            // HTML bodies (Outlook) escape ampersands inside hrefs.
            string plain = text.Replace("&amp;", "&");
            foreach (Regex p in Patterns)
            {
                Match m = p.Match(plain);
                if (m.Success) return m.Value.TrimEnd('.', ',', ';');
            }
            return null;
        }

        /// <summary>A location that is itself a web link counts (the Mac app joins an event whose location is a URL).</summary>
        public static string FromLocation(string location)
        {
            string known = FromText(location);
            if (known != null) return known;
            if (string.IsNullOrWhiteSpace(location)) return null;
            string trimmed = location.Trim();
            return AnyUrl.IsMatch(trimmed) && AnyUrl.Match(trimmed).Value.Length == trimmed.Length ? trimmed : null;
        }

        /// <summary>
        /// The link for one event from the backend's normalized shape (<c>{ summary, description, external_metadata }</c>,
        /// where <c>external_metadata</c> is the raw Google or Microsoft event).
        /// </summary>
        public static string FromBackendEvent(JObject e)
        {
            if (e == null) return null;
            JObject raw = e["external_metadata"] as JObject ?? e;

            // Dedicated fields: Google hangoutLink, Microsoft onlineMeeting.joinUrl / onlineMeetingUrl.
            foreach (string candidate in new[]
            {
                Str(raw["hangoutLink"]),
                Str((raw["onlineMeeting"] as JObject)?["joinUrl"]),
                Str(raw["onlineMeetingUrl"]),
            })
            {
                if (IsWebUrl(candidate)) return candidate;
            }

            // Google conferenceData: the video entry point first, any other with a web uri after.
            if (raw["conferenceData"] is JObject conf && conf["entryPoints"] is JArray points)
            {
                var objects = points.OfType<JObject>().ToList();
                string video = objects.Where(p => Str(p["entryPointType"]) == "video").Select(p => Str(p["uri"])).FirstOrDefault(IsWebUrl);
                if (video != null) return video;
                string any = objects.Select(p => Str(p["uri"])).FirstOrDefault(IsWebUrl);
                if (any != null) return any;
            }

            string location = Str(raw["location"]) ?? Str((raw["location"] as JObject)?["displayName"]) ?? Str(e["location"]);
            string fromLocation = FromLocation(location);
            if (fromLocation != null) return fromLocation;

            foreach (string text in new[]
            {
                Str(e["description"]), Str(raw["description"]),
                Str((raw["body"] as JObject)?["content"]), Str(raw["bodyPreview"]),
            })
            {
                string found = FromText(text);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Whether the signed-in user declined the invitation (Google attendees[self], Microsoft responseStatus).</summary>
        public static bool IsDeclined(JObject e)
        {
            if (e == null) return false;
            JObject raw = e["external_metadata"] as JObject ?? e;
            if (raw["attendees"] is JArray attendees)
            {
                foreach (JObject a in attendees.OfType<JObject>())
                    if (a["self"]?.Type == JTokenType.Boolean && (bool)a["self"])
                        return string.Equals(Str(a["responseStatus"]), "declined", StringComparison.OrdinalIgnoreCase);
            }
            if (raw["responseStatus"] is JObject status)
                return string.Equals(Str(status["response"]), "declined", StringComparison.OrdinalIgnoreCase);
            return false;
        }

        private static bool IsWebUrl(string s) =>
            !string.IsNullOrWhiteSpace(s) && FocusBear.Core.SafeUri.TryCreate(s.Trim(), UriKind.Absolute, out Uri u)
            && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp) && !string.IsNullOrEmpty(u.Host);

        private static string Str(JToken t) => t != null && t.Type == JTokenType.String ? (string)t : null;
    }

    /// <summary>A connected calendar, as <c>GET /calendar/{platform}/calendar-datas</c> lists it.</summary>
    public sealed class LateNoMoreCalendar
    {
        public LateNoMoreCalendar(string calendarId, string name, string account, string platform, bool isSynced)
        {
            CalendarId = calendarId;
            Name = string.IsNullOrWhiteSpace(name) ? calendarId : name;
            Account = account;
            Platform = platform;
            IsSynced = isSynced;
        }

        public string CalendarId { get; }
        public string Name { get; }
        public string Account { get; }
        public string Platform { get; }
        /// <summary>The backend fetches events only for calendars selected for sync (<c>is_selected</c>).</summary>
        public bool IsSynced { get; }

        /// <summary>
        /// Reads a calendar-datas response: <c>{ calendars: [{ account, requires_reauth, items: [...] }], keywords }</c>.
        /// Item fields are read defensively (<c>calendar_id</c> / <c>id</c>, <c>displayName</c> / <c>summary</c>, <c>is_selected</c> / <c>is_Selected</c>).
        /// </summary>
        public static List<LateNoMoreCalendar> ParseCalendarDatas(JToken root, string platform)
        {
            var result = new List<LateNoMoreCalendar>();
            if (!(root is JObject o) || !(o["calendars"] is JArray accounts)) return result;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject account in accounts.OfType<JObject>())
            {
                string email = Str(account["account"]) ?? Str(account["email"]);
                if (!(account["items"] is JArray items)) continue;
                foreach (JObject item in items.OfType<JObject>())
                {
                    string id = Str(item["calendar_id"]) ?? Str(item["calendarId"]) ?? Str(item["id"]);
                    if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                    string name = Str(item["displayName"]) ?? Str(item["summary"]) ?? Str(item["name"]) ?? Str(item["title"]);
                    JToken sel = item["is_selected"] ?? item["is_Selected"] ?? item["isSelected"] ?? item["selected"];
                    bool synced = sel == null || sel.Type != JTokenType.Boolean || (bool)sel;
                    result.Add(new LateNoMoreCalendar(id, name, email, platform, synced));
                }
            }
            return result;
        }

        private static string Str(JToken t) => t != null && t.Type == JTokenType.String ? (string)t : null;
    }
}
