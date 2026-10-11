using Focus_Bear.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// E2E test seam (<see cref="E2EBuild.CalendarFixtureVariable"/>): the Day Plan's calendar reads come from a JSON
    /// file instead of the backend, so a test can show meetings without a real calendar. Only the E2E build with the
    /// variable set ever uses this; every other build reads the backend (<see cref="DayPlanCalendarApi"/>).
    ///
    /// The file is read again on every refresh, so a test can rewrite it while the app runs. Schema:
    /// <code>
    /// { "events": [
    ///     { "id": "e2e-standup",                 // required, unique
    ///       "title": "Standup",
    ///       "start": "2026-09-27T10:00:00",      // local wall-clock time (no offset) or ISO with offset/Z
    ///       "end":   "2026-09-27T10:30:00",
    ///       // or, instead of start/end, relative to the moment the file is read:
    ///       "start_minutes_from_now": 30, "duration_minutes": 30,
    ///       "join_url": "https://meet.google.com/abc-defg-hij",   // optional (Late No More / join button)
    ///       "declined": false,                                     // optional
    ///       "calendar_id": "e2e-calendar"                          // optional
    ///     } ] }
    /// </code>
    /// It appears as one connected Google calendar, account "e2e-fixture@focusbear.io". Colours and hidden meetings
    /// still go through the normal (test account) API.
    /// </summary>
    internal sealed class DayPlanCalendarFixtureApi : IDayPlanCalendarApi
    {
        internal const string Account = "e2e-fixture@focusbear.io";
        private readonly string _path;
        private readonly DayPlanCalendarApi _real = new DayPlanCalendarApi();

        internal DayPlanCalendarFixtureApi(string path) { _path = path; }

        public Task<string> GetCalendarDatas(string platform)
        {
            if (platform != "google") return Task.FromResult("{\"calendars\":[]}");
            var root = new JObject
            {
                ["calendars"] = new JArray
                {
                    new JObject
                    {
                        ["account"] = Account,
                        ["requires_reauth"] = false,
                        ["expired"] = false,
                        ["items"] = new JArray
                        {
                            new JObject { ["id"] = "e2e-calendar", ["summary"] = "E2E fixture", ["is_synced"] = true },
                        },
                    },
                },
                ["keywords"] = new JArray(),
            };
            return Task.FromResult(root.ToString(Newtonsoft.Json.Formatting.None));
        }

        public Task<string> GetCalendarEvents(string platform, string account, string range = null)
        {
            if (platform != "google") return Task.FromResult("[]");
            return Task.FromResult(Read());
        }

        public Task<string> GetEventColours() => _real.GetEventColours();

        /// <summary>The fixture as the backend's normalised event list (external_id, summary, event_begins, ...).</summary>
        internal string Read()
        {
            var result = new JArray();
            try
            {
                if (!File.Exists(_path))
                {
                    LogWriter.LogWrite("E2E calendar fixture: " + _path + " not found - no meetings");
                    return result.ToString();
                }
                JToken root = JToken.Parse(File.ReadAllText(_path));
                JArray events = root as JArray ?? (root as JObject)?["events"] as JArray ?? new JArray();
                DateTime now = DateTime.Now;
                foreach (JObject e in events.OfType<JObject>())
                {
                    string id = e.Value<string>("id");
                    if (string.IsNullOrEmpty(id)) continue;
                    string start = e.Value<string>("start");
                    string end = e.Value<string>("end");
                    if (start == null && e["start_minutes_from_now"] != null)
                    {
                        DateTime s = now.AddMinutes(e.Value<double>("start_minutes_from_now"));
                        s = new DateTime(s.Year, s.Month, s.Day, s.Hour, s.Minute, 0);
                        start = s.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
                        end = s.AddMinutes(e.Value<double?>("duration_minutes") ?? 30).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
                    }
                    if (start == null) continue;
                    var o = new JObject
                    {
                        ["external_id"] = id,
                        ["summary"] = e.Value<string>("title") ?? "(no title)",
                        ["event_begins"] = start,
                        ["event_ends"] = end ?? start,
                        ["platform"] = "google",
                        ["calendar_id"] = e.Value<string>("calendar_id") ?? "e2e-calendar",
                    };
                    string join = e.Value<string>("join_url");
                    if (!string.IsNullOrEmpty(join)) o["hangoutLink"] = join;
                    if (e.Value<bool?>("declined") == true)
                        o["attendees"] = new JArray { new JObject { ["self"] = true, ["responseStatus"] = "declined" } };
                    result.Add(o);
                }
                LogWriter.LogWrite("E2E calendar fixture: " + result.Count + " event(s) from " + _path);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("E2E calendar fixture read ex - " + ex.Message);
            }
            return result.ToString(Newtonsoft.Json.Formatting.None);
        }
    }
}
