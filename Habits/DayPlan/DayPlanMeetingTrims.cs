using Focus_Bear.Models;
using Focus_Bear.Utils;
using FocusBear.Core.Schedule;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// The Day Plan "Fix" → Shorten on a meeting: a local, one-day trim of the slot the Day Plan keeps for
    /// it. Never a calendar write — the meeting on the calendar is untouched, so nobody else sees a change.
    /// The rules are FocusBear.Core's <see cref="DayPlanMeetingDurations"/>; this is its registry home
    /// (Mac: UserDefaults "dayPlanMeetingDurationOverrides").
    /// </summary>
    public static class DayPlanMeetingTrims
    {
        private static DayPlanMeetingDurations _store;

        private static DayPlanMeetingDurations Store
        {
            get
            {
                if (_store != null) return _store;
                Dictionary<string, Dictionary<string, int>> stored = null;
                try
                {
                    string json = RegistryManager.LoadDayPlanMeetingDurations();
                    if (!string.IsNullOrEmpty(json))
                        stored = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(json);
                }
                catch (Exception ex) { LogWriter.LogWrite("DayPlanMeetingTrims load - " + ex.Message); }
                _store = new DayPlanMeetingDurations(stored);
                return _store;
            }
        }

        private static DateTimeOffset Day(DateTime day) => new DateTimeOffset(day.Date);

        public static int? OverrideSeconds(string eventId, DateTime day) => Store.OverrideSeconds(eventId, Day(day));

        /// <summary>The end the Day Plan plans a meeting to, after any trim for that day.</summary>
        public static DateTime TrimmedEnd(string eventId, DateTime day, DateTime start, DateTime end)
        {
            try
            {
                return Store.TrimmedEnd(eventId, Day(day), new DateTimeOffset(start), new DateTimeOffset(end)).LocalDateTime;
            }
            catch (Exception ex) { LogWriter.LogWrite("DayPlanMeetingTrims.TrimmedEnd - " + ex.Message); return end; }
        }

        public static int? SetOverride(int seconds, string eventId, DateTime day, int actual)
        {
            int? applied = Store.SetOverride(seconds, eventId, Day(day), actual);
            Save();
            LogWriter.LogWrite("[user-action] day plan: meeting shortened to " + (applied.HasValue ? applied.Value.ToString() : "full") + " seconds for " + DayPlanDayKey.For(Day(day)));
            return applied;
        }

        public static void ClearOverride(string eventId, DateTime day)
        {
            Store.ClearOverride(eventId, Day(day));
            Save();
        }

        private static void Save()
        {
            try { RegistryManager.SaveDayPlanMeetingDurations(JsonConvert.SerializeObject(Store.OverridesByDay)); }
            catch (Exception ex) { LogWriter.LogWrite("DayPlanMeetingTrims save - " + ex.Message); }
        }
    }
}
