using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Preferences;
using Focus_Bear.Utils;
using FocusBear.Core.Schedule;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// Saves the Day Plan's settings edits — "Edit routine timing" and a habit's "Edit" — through the
    /// app's existing settings path: change the user-settings JSON (<see cref="AppSettings.sRawUserSettings"/>),
    /// keep the registry copy in step (<see cref="RegistryManager.SaveUserSettings"/>) and PUT it with
    /// <see cref="FocusBearAPIClient.PutUserSettings"/> — exactly what
    /// <see cref="AppSettings.ChangeStartupTimeToCurrentTimeAndSaveSettings"/> and the Breaks tab do. The
    /// JSON edits themselves are pure and unit-tested in FocusBear.Core (RoutineTiming / HabitSettingsEdit).
    ///
    /// Unlike the Mac (which parks edits in a draft until "Save and restart app"), an edit here is saved
    /// straight away and applied to the live values the app reads, so the Day Plan re-plans at once. A
    /// save the server refuses is rolled back locally, so the app never runs on settings the account
    /// doesn't have.
    /// </summary>
    public static class DayPlanSettingsEdits
    {
        private static AppSettings Settings => AppSettings.GetInstance;

        // Seams for tests: where the latest settings come from, where the edited document goes, and the local copy.
        internal static Func<Task<string>> FetchLatestSettings = () => new FocusBearAPIClient().GetLatestUserSettingsRaw();
        internal static Func<Task<bool>> PutSettings = async () => await new FocusBearAPIClient().PutUserSettings() == 1;
        internal static Action SaveLocalCopy = RegistryManager.SaveUserSettings;
        /// <summary>How long to wait for the latest copy before editing the local one (the HttpClient default is 100 s).</summary>
        internal static TimeSpan FetchTimeout = TimeSpan.FromSeconds(8);

        /// <summary>
        /// The settings document an edit is applied to. <c>PUT /user/settings</c> replaces the WHOLE document, so
        /// editing the copy fetched at launch undid every change made since on the web or another device — a habit
        /// renamed elsewhere came back under its old name the moment a routine's time was nudged here. The latest
        /// server copy is fetched first; without a connection the local copy is all there is (and the PUT will not
        /// go either).
        /// </summary>
        internal static async Task<JObject> LatestSettingsAsync()
        {
            try
            {
                Task<string> fetch = FetchLatestSettings();
                if (await Task.WhenAny(fetch, Task.Delay(FetchTimeout)) != fetch)
                {
                    LogWriter.LogWrite("DayPlanSettingsEdits.LatestSettings - no answer in " + FetchTimeout.TotalSeconds + " s, using the local copy");
                    _ = fetch.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                    return CurrentSettings();
                }
                string latest = await fetch;
                if (!string.IsNullOrWhiteSpace(latest))
                {
                    JObject root = ParseSettings(latest);
                    ApplyUnsynced(root);
                    return root;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DayPlanSettingsEdits.LatestSettings ex - " + ex.Message);
            }
            return CurrentSettings();
        }

        // Local edits kept although their PUT failed (offline "start the morning now", the office-friendly break).
        // The server's copy doesn't have them, so the next edit - which starts from that copy - would PUT them away,
        // on this device and the server. They are re-applied to the fetched copy until a PUT succeeds.
        private static readonly object UnsyncedLock = new object();
        private static readonly List<KeyValuePair<string, Action<JObject>>> Unsynced = new List<KeyValuePair<string, Action<JObject>>>();

        /// <summary>Keeps <paramref name="apply"/> (replacing an earlier one with the same name) until a PUT succeeds.</summary>
        internal static void KeepUnsynced(string name, Action<JObject> apply)
        {
            lock (UnsyncedLock)
            {
                Unsynced.RemoveAll(u => u.Key == name);
                Unsynced.Add(new KeyValuePair<string, Action<JObject>>(name, apply));
            }
            LogWriter.LogWrite("DayPlanSettingsEdits: kept an unsynced settings edit (" + name + ")");
        }

        internal static void ClearUnsynced()
        {
            lock (UnsyncedLock) Unsynced.Clear();
        }

        private static void ApplyUnsynced(JObject root)
        {
            List<KeyValuePair<string, Action<JObject>>> edits;
            lock (UnsyncedLock) edits = Unsynced.ToList();
            foreach (var edit in edits)
            {
                try { edit.Value(root); }
                catch (Exception ex) { LogWriter.LogWrite("DayPlanSettingsEdits: re-applying " + edit.Key + " ex - " + ex.Message); }
            }
        }

        /// <summary>PUTs the current settings; on success the unsynced edits (carried in them) are done with.</summary>
        internal static async Task<bool> PutAndSettle()
        {
            bool ok;
            try { ok = await PutSettings(); }
            catch (Exception ex) { LogWriter.LogWrite("DayPlanSettingsEdits: PutUserSettings ex - " + ex.Message); ok = false; }
            if (ok) ClearUnsynced();
            return ok;
        }

        /// <summary>The same gate the Settings screen uses when a settings password is set.</summary>
        public static bool Authorize(Window owner)
        {
            try
            {
                // The same rule as opening Settings (change-settings, after-cutoff, super-strict; 2-minute grace).
                if (!Settings.PasswordRequiredToChangeSettings()) return true;
                var pwd = new AskPassword { Owner = owner };
                if (pwd.ShowDialog() != true) return false;
                Settings.SettingsUnlockedAt = DateTime.Now;
                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DayPlanSettingsEdits.Authorize ex - " + ex.Message);
                return false;
            }
        }

        private static readonly JsonSerializerSettings KeepDateStrings = new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };

        /// <summary>
        /// The settings document with every string left as sent. Plain <c>JObject.Parse</c> turns ISO date strings into
        /// DateTime values, and the whole document is PUT back, so each edit rewrote every date in it in another format.
        /// </summary>
        private static JObject ParseSettings(string json) => JsonConvert.DeserializeObject<JObject>(json, KeepDateStrings);

        /// <summary>A copy of the current settings JSON, or null when there isn't one to edit.</summary>
        public static JObject CurrentSettings()
        {
            try
            {
                return string.IsNullOrEmpty(Settings.sRawUserSettings) ? null : ParseSettings(Settings.sRawUserSettings);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DayPlanSettingsEdits.CurrentSettings ex - " + ex.Message);
                return null;
            }
        }

        // MARK: Routine timing

        /// <summary>Saves a routine's new start (and the evening's cutoff). False when invalid or the server refused it.</summary>
        public static async Task<bool> SaveRoutineTimingAsync(RoutineSettingsSection section, int minutes, int? eveningCutoffMinutes)
        {
            JObject root = await LatestSettingsAsync();
            if (root == null || !RoutineTiming.Apply(root, section, minutes, eveningCutoffMinutes)) return false;

            var s = Settings;
            string oldRaw = s.sRawUserSettings, oldStart = s.sDayStartTime, oldShut = s.sDayShutOffTime, oldCut = s.sDayCutOffTime;
            if (section == RoutineSettingsSection.Morning)
            {
                s.sDayStartTime = RoutineTiming.TimeStringFromMinutes(minutes);
            }
            else
            {
                s.sDayShutOffTime = RoutineTiming.TimeStringFromMinutes(minutes);
                s.sDayCutOffTime = eveningCutoffMinutes.HasValue ? RoutineTiming.TimeStringFromMinutes(eveningCutoffMinutes.Value) : "";
            }
            LogWriter.LogWrite("[user-action] day plan: routine timing " + section + " -> " + RoutineTiming.TimeStringFromMinutes(minutes)
                               + (section == RoutineSettingsSection.Evening ? " cutoff " + (s.sDayCutOffTime == "" ? "none" : s.sDayCutOffTime) : ""));

            if (await PersistAsync(root)) return true;

            s.sRawUserSettings = oldRaw;
            s.sDayStartTime = oldStart;
            s.sDayShutOffTime = oldShut;
            s.sDayCutOffTime = oldCut;
            SaveLocalCopy();
            return false;
        }

        // MARK: Habit edit

        /// <summary>The editor's starting values for a habit, or null when it isn't in the settings.</summary>
        public static bool TryReadHabit(string habitId, out RoutineSettingsSection section, out string icon, out HabitEditValues values)
        {
            section = RoutineSettingsSection.Morning;
            icon = "";
            values = null;
            var located = HabitSettingsEdit.Locate(CurrentSettings(), habitId);
            if (located == null) return false;
            section = located.Item1;
            icon = HabitSettingsEdit.ReadIcon(located.Item2);
            values = HabitSettingsEdit.Read(located.Item2);
            return true;
        }

        /// <summary>Saves a habit edit and applies it to every live copy of the habit. False when the server refused it.</summary>
        public static async Task<bool> SaveHabitAsync(string habitId, string icon, HabitEditValues values)
        {
            JObject root = await LatestSettingsAsync();
            var located = HabitSettingsEdit.Locate(root, habitId);
            if (located == null) return false;
            HabitSettingsEdit.Write(located.Item2, located.Item1, icon, values);

            var s = Settings;
            string oldRaw = s.sRawUserSettings;
            var snapshot = SnapshotLiveHabits();
            ApplyToLiveHabits(habitId, icon, values);
            LogWriter.LogWrite("[user-action] day plan: habit edited (" + located.Item1 + ")");

            if (await PersistAsync(root)) return true;

            s.sRawUserSettings = oldRaw;
            SaveLocalCopy();
            RestoreLiveHabits(snapshot);
            return false;
        }

        private static List<List<habit>> LiveLists()
        {
            var s = Settings;
            return new List<List<habit>>
            {
                s.arrAllMorningHabits, s.arrGLOBALAllMorningHabits, s.arrAllMorningHabitsFiltered,
                s.arrAllEveningHabits, s.arrGLOBALAllEveningHabits, s.arrAllEveningHabitsFiltered,
            }.Where(l => l != null).Distinct().ToList();
        }

        private static List<Tuple<List<habit>, List<habit>>> SnapshotLiveHabits()
        {
            return LiveLists().Select(l => Tuple.Create(l, new List<habit>(l))).ToList();
        }

        private static void RestoreLiveHabits(List<Tuple<List<habit>, List<habit>>> snapshot)
        {
            foreach (var pair in snapshot)
            {
                pair.Item1.Clear();
                pair.Item1.AddRange(pair.Item2);
            }
        }

        /// <summary>habit is a struct, so each list's copy is rewritten in place.</summary>
        private static void ApplyToLiveHabits(string habitId, string icon, HabitEditValues v)
        {
            foreach (List<habit> list in LiveLists())
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].id != habitId) continue;
                    habit h = list[i];
                    h.name = HabitSettingsEdit.DisplayName(icon, v.TrimmedName);
                    h.nameWithoutIcon = v.TrimmedName;
                    h.useTimer = v.UsesTimer;
                    if (v.UsesTimer)
                    {
                        h.durationSeconds = v.DurationSeconds;
                        h.completion_requirements = "";
                    }
                    else
                    {
                        h.completion_requirements = (v.CompletionRequirement ?? "").Trim();
                    }
                    bool evening = list == Settings.arrAllEveningHabits || list == Settings.arrGLOBALAllEveningHabits || list == Settings.arrAllEveningHabitsFiltered;
                    if (evening)
                    {
                        h.bHighPriorityActivity = v.IsHighPriority;
                        h.sCutOffTime = v.IsHighPriority && v.CutoffEnabled ? RoutineTiming.TimeStringFromMinutes(v.CutoffMinutes) : "";
                    }
                    h.DaysOfWeek = HabitSettingsEdit.OrderedDays(v.DaysOfWeek);
                    // The rest of the full editor, mapped the way AppSettings.ReadAndParseSettingsJson derives it.
                    var apps = v.UsesFocusMode ? new List<string>() : HabitEditValues.Cleaned(v.AllowedApps).ToList();
                    var urls = v.UsesFocusMode ? new List<string>() : HabitEditValues.Cleaned(v.AllowedUrls).Select(HabitSettingsEdit.StripScheme).ToList();
                    h.videoUrls = HabitEditValues.Cleaned(v.VideoUrls).ToList();
                    h.allowedApps = apps;
                    h.allowedUrls = urls;
                    h.openSpecificApps = apps.Count > 0;
                    h.allowedFocusMode = v.UsesFocusMode ? v.AllowedFocusModeId : "";
                    h.show_saved_distracting_websites = v.ShowSavedDistractingWebsites;
                    if (v.RandomChoiceCount == 0)
                    {
                        var questions = v.LogQuantity
                            ? v.LogQuestions.Select(q => new QuantityQuestion(q.Id, ParseInt(q.MinValue), ParseInt(q.MaxValue),
                                                                               (q.Question ?? "").Trim(), v.LogSummaryType, 0, "", "")).ToList()
                            : new List<QuantityQuestion>();
                        // Keep the descriptions the editor doesn't show.
                        foreach (QuantityQuestion q in questions)
                        {
                            QuantityQuestion old = (h.logQuantityQuestions ?? new List<QuantityQuestion>()).FirstOrDefault(o => o.id == q.id);
                            if (old == null) continue;
                            q.minValueDescription = old.minValueDescription;
                            q.maxValueDescription = old.maxValueDescription;
                        }
                        h.logQuantityQuestions = questions;
                        h.logQuantity = questions.Count > 0;
                        h.check_list = HabitEditValues.Cleaned(v.Checklist).ToList();
                    }
                    h.text_instructions = v.TextInstructions ?? "";
                    var oldImages = h.image_urls ?? new List<ActivityImage>();
                    h.image_urls = HabitEditValues.Cleaned(v.ImageUrls)
                        .Select(u => oldImages.Any(o => o.Url == u) ? oldImages.First(o => o.Url == u) : new ActivityImage { Url = u })
                        .ToList();
                    list[i] = h;
                }
            }
        }

        private static int ParseInt(string text)
        {
            int n;
            return int.TryParse((text ?? "").Trim(), out n) ? n : 0;
        }

        // MARK: Delete habit

        /// <summary>
        /// Removes a habit from its routine (settings JSON + every live copy) and saves it. The active
        /// routine's current position is kept on the same habit. False when the server refused it (rolled back).
        /// </summary>
        public static async Task<bool> DeleteHabitAsync(string habitId)
        {
            JObject root = await LatestSettingsAsync();
            if (!HabitSettingsEdit.Delete(root, habitId)) return false;

            var s = Settings;
            string oldRaw = s.sRawUserSettings;
            int oldIndex = s.nHabitIndex;
            var snapshot = SnapshotLiveHabits();
            List<habit> active = s.nCurrentActivityType == CurrentActivityType.MORNING_HABITS ? s.arrAllMorningHabits
                               : s.nCurrentActivityType == CurrentActivityType.EVENING_HABITS ? s.arrAllEveningHabits : null;
            int removedAt = active == null ? -1 : active.FindIndex(h => h.id == habitId);
            foreach (List<habit> list in LiveLists()) list.RemoveAll(h => h.id == habitId);
            if (removedAt >= 0 && s.nHabitIndex > removedAt) s.nHabitIndex--;
            LogWriter.LogWrite("[user-action] day plan: habit deleted");

            if (await PersistAsync(root)) return true;

            s.sRawUserSettings = oldRaw;
            SaveLocalCopy();
            RestoreLiveHabits(snapshot);
            s.nHabitIndex = oldIndex;
            return false;
        }

        /// <summary>The user's focus modes (block lists), id -> name, for the editor's Allow Apps tab.</summary>
        public static List<KeyValuePair<string, string>> FocusModes()
        {
            var result = new List<KeyValuePair<string, string>>();
            try
            {
                foreach (FocusMode m in Settings.arrFocusModes)
                    if (m != null && !string.IsNullOrEmpty(m.id)) result.Add(new KeyValuePair<string, string>(m.id, string.IsNullOrEmpty(m.name) ? m.id : m.name));
            }
            catch (Exception ex) { LogWriter.LogWrite("DayPlanSettingsEdits.FocusModes ex - " + ex.Message); }
            return result;
        }

        // MARK: Save path

        private static async Task<bool> PersistAsync(JObject root)
        {
            var s = Settings;
            s.sRawUserSettings = root.ToString();
            SaveLocalCopy();
            if (await PutAndSettle()) return true;
            LogWriter.LogWrite("DayPlanSettingsEdits: PutUserSettings refused");
            return false;
        }
    }
}
