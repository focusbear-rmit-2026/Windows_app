using Focus_Bear.Models;
using Focus_Bear.Utils;
using FocusBear.Core.Schedule;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// The Day Plan's "Shorten" — a habit trimmed for one day only, kept on this computer and never sent
    /// to the server (the habit's real length is shared with every day and device). The rules live in
    /// <see cref="DayPlanHabitDurations"/> (FocusBear.Core, unit-tested); this is just its registry-backed
    /// home. Mirrors the Mac's DayPlanHabitDurations (UserDefaults).
    /// </summary>
    public static class DayPlanHabitTrims
    {
        private static DayPlanHabitDurations _store;

        private static DayPlanHabitDurations Store
        {
            get
            {
                if (_store != null) return _store;
                Dictionary<string, Dictionary<string, int>> stored = null;
                try
                {
                    string json = RegistryManager.LoadDayPlanHabitDurations();
                    if (!string.IsNullOrEmpty(json))
                        stored = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(json);
                }
                catch (Exception ex) { LogWriter.LogWrite("DayPlanHabitTrims load - " + ex.Message); }
                _store = new DayPlanHabitDurations(stored);
                return _store;
            }
        }

        private static DateTimeOffset Day(DateTime day) => new DateTimeOffset(day.Date);

        private static void Save()
        {
            try { RegistryManager.SaveDayPlanHabitDurations(JsonConvert.SerializeObject(Store.OverridesByDay)); }
            catch (Exception ex) { LogWriter.LogWrite("DayPlanHabitTrims save - " + ex.Message); }
        }

        /// <summary>The trim for this habit on this day, or null when it runs its real length.</summary>
        public static int? OverrideSeconds(string habitId, DateTime day) => Store.OverrideSeconds(habitId, Day(day));

        public static bool IsShortened(string habitId, DateTime day) => OverrideSeconds(habitId, day).HasValue;

        /// <summary>How long to plan (or run) the habit on this day — never longer than its real length.</summary>
        public static int EffectiveSeconds(string habitId, DateTime day, int actual) => Store.EffectiveSeconds(habitId, Day(day), actual);

        /// <summary>Records a trim (asking for the full length clears it). Returns the trim now in force.</summary>
        public static int? SetOverride(int seconds, string habitId, DateTime day, int actual)
        {
            int? applied = Store.SetOverride(seconds, habitId, Day(day), actual);
            Save();
            LogWriter.LogWrite("[user-action] day plan: habit shortened to " + (applied.HasValue ? applied.Value.ToString() : "full") + " seconds for " + DayPlanDayKey.For(Day(day)));
            return applied;
        }

        public static void ClearOverride(string habitId, DateTime day)
        {
            Store.ClearOverride(habitId, Day(day));
            Save();
            LogWriter.LogWrite("[user-action] day plan: habit restored to full length for " + DayPlanDayKey.For(Day(day)));
        }

        /// <summary>
        /// The length a habit's timer should run today: the trim for timer habits only — a non-timer
        /// habit's duration is the "no duration" sentinel, and trimming it would turn it into a countdown.
        /// (Mac: timerViewcontroller.tryStartTimerHabit.)
        /// </summary>
        public static int TimerSecondsToday(habit h)
        {
            if (!h.useTimer) return h.durationSeconds;
            try { return EffectiveSeconds(h.id, DateTime.Today, h.durationSeconds); }
            catch (Exception ex) { LogWriter.LogWrite("DayPlanHabitTrims timer - " + ex.Message); return h.durationSeconds; }
        }
    }
}
