using EspDotNet.Tools;
using Focus_Bear.API;
using Focus_Bear.Focus;
using Focus_Bear.Models;
using Focus_Bear.Preferences;
using Focus_Bear.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Focus_Bear.Services
{
    public static class BlockingScheduleManager
    {
        private static AppSettings appSettings = AppSettings.GetInstance;
        private static FocusBearAPIClient client = new FocusBearAPIClient();

        // Raised (on the UI thread) after a server refresh actually changes the schedule collection.
        // The open details screen listens so it can close itself when its schedule was deleted elsewhere. #1114
        public static event Action SchedulesRefreshed;

        // Raised (on the UI thread) after a server refresh actually changes the focus-mode collection.
        // The open schedule editor listens so its block-list combo reflects modes added elsewhere. #1114
        public static event Action FocusModesRefreshed;
        /// <summary>
        /// Adds a new blocking schedule from the provided Instance
        /// </summary>
        public static void AddBlockingSchedule(CustomBlockingSchedule Instance, bool bUpdating = false)
        {
            //Convert to data and then call the API
            int[] activeDays = Instance.DaysOfWeek.Select((isActive, index) => new { isActive, index })
                                                            .Where(x => x.isActive)
                                                            .Select(x => x.index)
                                                            .ToArray();
            string blockLevel = "gentle";

            if (Instance.BlockLevel == FOCUSMODESTRICTNESS.CHILL_MODE )
            {
                blockLevel = "gentle";
            }
            else if (Instance.BlockLevel ==  FOCUSMODESTRICTNESS.STRICT)
            {
                blockLevel = "strict" ;
            }
            else if (Instance.BlockLevel == FOCUSMODESTRICTNESS.IRON_FOCUS)
            {
                blockLevel = "super-strict";
            }

            string metadata = $"{{\"is_micro_breaks_enabled\":{JsonConvert.SerializeObject(Instance.bMicroBreaksEnabled)}}}";

            string addJsonString = $"{{\"id\":\"{Instance.id}\"," +
                                    $"\"name\":\"{Instance.name}\"," +
                                    $"\"start_time\":\"{Instance.StartTimeValue}\"," +
                                    $"\"end_time\":\"{Instance.EndTimeValue}\"," +
                                    $"\"days_of_week\":{JsonConvert.SerializeObject(activeDays)}," +
                                    $"\"focus_mode_id\":\"{Instance.SelectedFocusModeId}\"," +
                                    $"\"pause_friction\":\"none\"," +
                                    $"\"block_level\":\"{blockLevel}\"," +
                                    $"\"is_ai_blocking_enabled\":{JsonConvert.SerializeObject(Instance.bAIBlockingEnabled)}," +
                                    // Top-level "emoji" (nullable, <=32 chars) - matches the mac app's
                                    // customBlockingSchedule.emoji field (backend#2094), not nested in metadata.
                                    $"\"emoji\":{JsonConvert.SerializeObject(Instance.Emoji)}," +
                                    $"\"metadata\":{metadata}}}";

            if(bUpdating)
            {
                _ = client.UpdateCustomBlockingSchedule(Instance.id, addJsonString);
            }
            else
            {
                _ = client.AddCustomBlockingSchedule(addJsonString);
            }
           

        }

        /// <summary>
        /// Deletes a blocking schedule by ID.
        /// </summary>
        /// <param name="id">The ID of the schedule to delete.</param>
        public static void DeleteBlockingSchedule(string id)
        {
            LogWriter.LogWrite("DeleteBlockingSchedule()");
            _ = client.DeleteCustomBlockingSchedule(id);
        }

        public static void ReadAndParseBlockingSchedules()
        {
            LogWriter.LogWrite("ReadAndParseBlockingSchedules()");

            try
            {
                //Check is settings are there
                if (appSettings.sRawBlockingSchedules == null || appSettings.sRawBlockingSchedules.Length == 0)
                {
                    LogWriter.LogWrite("sRawBlockingSchedules == null - Read previously saved data from registry");
                    //Read previously saved in registry
                    RegistryManager.GetSavedBlockingSchedules();
                }
                if (appSettings.sRawBlockingSchedules.Length > 0)
                {

                    //Parse the JSON string
                    try
                    {
                        JArray BlockingSchedules = JArray.Parse(appSettings.sRawBlockingSchedules);

                        if (BlockingSchedules != null && BlockingSchedules.Count() > 0)
                        {
                            foreach (JObject item in BlockingSchedules)
                            {


                                try
                                {
                                    string id = item.ContainsKey("id") ? item.GetValue("id").ToString() : "";
                                    string name = item.ContainsKey("name") ? item.GetValue("name").ToString() : "";
                                    string startTime = item.ContainsKey("start_time") ? item.GetValue("start_time").ToString() : "";
                                    string endTime = item.ContainsKey("end_time") ? item.GetValue("end_time").ToString() : "";

                                    JArray daysArray = (JArray)item.GetValue("days_of_week");

                                    int[] activeDays = daysArray
                                        .Where(t => t.Type == JTokenType.Integer)
                                        .Select(t => t.Value<int>())
                                        .ToArray();

                                    bool isAll = daysArray
                                        .Any(t => t.Type == JTokenType.String &&
                                                  t.ToString().Equals("ALL", StringComparison.OrdinalIgnoreCase));

                                    if (isAll)
                                    {
                                        activeDays = new int[] { 0, 1, 2, 3, 4, 5, 6 };
                                    }

                                    //convert to bool array that we are using
                                    bool[] daysOfWeek = new bool[7];
                                    foreach (int idx in activeDays)
                                    {
                                        if (idx >= 0 && idx < 7)                // guard against bad values
                                            daysOfWeek[idx] = true;
                                    }


                                    string focusModeId = item.ContainsKey("focus_mode_id") ? item.GetValue("focus_mode_id").ToString() : "";
                                    string pauseFriction = item.ContainsKey("pause_friction") ? item.GetValue("pause_friction").ToString() : "";
                                    string blockLevel = item.ContainsKey("block_level") ? item.GetValue("block_level").ToString() : "";
                                    FOCUSMODESTRICTNESS FMS = FOCUSMODESTRICTNESS.CHILL_MODE;
                                    if(blockLevel == "gentle")
                                    {
                                        FMS = FOCUSMODESTRICTNESS.CHILL_MODE;
                                    }
                                    else if (blockLevel == "strict")
                                    {
                                        FMS = FOCUSMODESTRICTNESS.STRICT;
                                    }
                                    else if (blockLevel == "super-strict")
                                    {
                                        FMS = FOCUSMODESTRICTNESS.IRON_FOCUS;
                                    }
                                   
                                   

                                    bool isAiBlockingEnabled = false;
                                    bool isMicroBreaksEnabled = false;
                                    isAiBlockingEnabled = item.ContainsKey("is_ai_blocking_enabled") ? Convert.ToBoolean(item.GetValue("is_ai_blocking_enabled")) : false;

                                    if (item.ContainsKey("metadata"))
                                    {
                                        string sMetadata = item.GetValue("metadata").ToString();
                                        if (sMetadata.Length > 0)
                                        {
                                            JObject metadata = JObject.Parse(sMetadata);
                                            isMicroBreaksEnabled = metadata.ContainsKey("is_micro_breaks_enabled") ? Convert.ToBoolean(metadata.GetValue("is_micro_breaks_enabled")) : false;
                                        }
                                    }

                                    string emoji = item.ContainsKey("emoji") && item.GetValue("emoji").Type != JTokenType.Null
                                        ? item.GetValue("emoji").ToString()
                                        : null;

                                    var newSchedule = new CustomBlockingSchedule(id, name, false, startTime, endTime, daysOfWeek, focusModeId, FMS, isAiBlockingEnabled, isMicroBreaksEnabled);
                                    newSchedule.Emoji = emoji;
                                    appSettings.arrCustomBlockingSchedules.Add(newSchedule);
                                }
                                catch (Exception ex)
                                {
                                    LogWriter.LogWrite("ReadAndParseUserDetailsJson() Reading item in BlockingSchedules failed ex = " + ex.Message);
                                    continue; //Even if one focus mode have failed we will read the next one
                                }
                            }
                           
                        }
                        
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("ReadAndParseBlockingSchedules() ex - " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ReadAndParseBlockingSchedules() top ex -" + ex.Message);
            }
        }

        // Re-fetches blocking schedules from the server and, when the data changed, clears and
        // refills the live collection the same way startup does. Safe to call from any thread. #1114
        public static async Task<bool> RefreshBlockingSchedulesFromServerAsync()
        {
            try
            {
                LogWriter.LogWrite("RefreshBlockingSchedulesFromServerAsync()");

                // A schedule references a focus mode (block list), and those are otherwise only fetched
                // at startup. Refresh them on the same trigger so a block list created on another device
                // is selectable in the schedule editor without restarting the app. #1114
                await RefreshFocusModesFromServerAsync();

                string oldRaw = appSettings.sRawBlockingSchedules;

                int result = await client.GetAllBlockingSchedules();
                if (result != 1)
                {
                    LogWriter.LogWrite("RefreshBlockingSchedulesFromServerAsync - fetch failed");
                    return false;
                }

                if (oldRaw == appSettings.sRawBlockingSchedules)
                {
                    return false;
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        appSettings.arrCustomBlockingSchedules.Clear();
                        ReadAndParseBlockingSchedules();
                        LogWriter.LogWrite("RefreshBlockingSchedulesFromServerAsync - schedules updated");
                        SchedulesRefreshed?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("RefreshBlockingSchedulesFromServerAsync Dispatcher - " + ex.Message);
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("RefreshBlockingSchedulesFromServerAsync - " + ex.Message);
                return false;
            }
        }

        // Re-fetches focus modes (block lists) from the server and, when the data changed, clears and
        // refills the live collection the same way startup does. ReadAndParseFocusModes appends, so the
        // Clear() first is required to avoid duplicates. Safe to call from any thread. #1114
        public static async Task<bool> RefreshFocusModesFromServerAsync()
        {
            try
            {
                LogWriter.LogWrite("RefreshFocusModesFromServerAsync()");

                string oldRaw = appSettings.sFocusModes;

                int result = await client.GetFocusModes();
                if (result != 1)
                {
                    LogWriter.LogWrite("RefreshFocusModesFromServerAsync - fetch failed");
                    return false;
                }

                if (oldRaw == appSettings.sFocusModes)
                {
                    return false;
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        appSettings.arrFocusModes.Clear();
                        appSettings.ReadAndParseFocusModes();
                        LogWriter.LogWrite("RefreshFocusModesFromServerAsync - focus modes updated");
                        FocusModesRefreshed?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("RefreshFocusModesFromServerAsync Dispatcher - " + ex.Message);
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("RefreshFocusModesFromServerAsync - " + ex.Message);
                return false;
            }
        }

        public static bool HasOverlappingSchedules(string  targetId)
        {
            var target = appSettings.arrCustomBlockingSchedules.FirstOrDefault(s => s.id == targetId);
            if (target == null) return false;

            // Parse target times (handles AM/PM or 24h)
            if (!DateTime.TryParse(target.StartTimeValue, out DateTime targetStart) ||
                !DateTime.TryParse(target.EndTimeValue, out DateTime targetEnd))
                return false;

            for (int i = 0; i < appSettings.arrCustomBlockingSchedules.Count; i++)
            {
                var other = appSettings.arrCustomBlockingSchedules[i];
                if (other.id == targetId)
                    continue;

                // check day overlap first
                for (int d = 0; d < 7; d++)
                {
                    if (target.DaysOfWeek[d] && other.DaysOfWeek[d])
                    {
                        // Parse other's times
                        if (!DateTime.TryParse(other.StartTimeValue, out DateTime otherStart) ||
                            !DateTime.TryParse(other.EndTimeValue, out DateTime otherEnd))
                            continue;

                        // Convert both to TimeSpan for easy compare
                        var tStart = targetStart.TimeOfDay;
                        var tEnd = targetEnd.TimeOfDay;
                        var oStart = otherStart.TimeOfDay;
                        var oEnd = otherEnd.TimeOfDay;

                        // check overlap
                        if (tStart < oEnd && oStart < tEnd)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }


        public static bool DoesNewScheduleOverlap(bool[] newDaysOfWeek, string newStartTimeValue, string newEndTimeValue)
        {
            // Try parsing new schedule times (support both 24hr and 12hr formats)
            if (!TryParseTime(newStartTimeValue, out TimeSpan newStart) ||
                !TryParseTime(newEndTimeValue, out TimeSpan newEnd))
            {
                return false; // invalid input
            }

            foreach (var existing in appSettings.arrCustomBlockingSchedules)
            {
                for (int day = 0; day < 7; day++)
                {
                    if (existing.DaysOfWeek[day] && newDaysOfWeek[day])
                    {
                        if (!TryParseTime(existing.StartTimeValue, out TimeSpan existStart) ||
                            !TryParseTime(existing.EndTimeValue, out TimeSpan existEnd))
                        {
                            continue;
                        }

                        // Overlap condition
                        if (newStart < existEnd && existStart < newEnd)
                        {
                            return true; // Overlap found
                        }
                    }
                }
            }

            return false; // No overlaps
        }
        private static bool TryParseTime(string timeValue, out TimeSpan result)
        {
            result = default;

            if (string.IsNullOrWhiteSpace(timeValue))
                return false;

            // Try 24-hour format first
            if (TimeSpan.TryParse(timeValue, out result))
                return true;

            // Try 12-hour AM/PM formats
            if (DateTime.TryParseExact(timeValue.Trim(),
                new[] { "h:mm tt", "hh:mm tt", "h tt", "hh tt" },
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out DateTime parsedTime))
            {
                result = parsedTime.TimeOfDay;
                return true;
            }

            return false; // Failed to parse
        }

        public static void CheckCustomBlockingScheduleTime()
        {
            if (appSettings.isFocusModeRunning ||
                ((appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS ||
                appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS ||
                appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY) &&
                (!appSettings.bHabitsPostponedState && !appSettings.bFocusSummaryPostponedState)))
            {
                //If we are in an activity state or focus mode already running, do not check for custom blocking schedule
                return;
            }

            bool wasAlreadyRunning = appSettings.isBlockingScheduleRunning;
            string previousActiveId = appSettings.ActiveSchedule?.id;

            var newSchedule = GetActiveSchedule();
            appSettings.ActiveSchedule = newSchedule;
            if(newSchedule != null)
            {
                //Start it
                newSchedule.isOn = true;
                appSettings.isBlockingScheduleRunning = true;

                // #1420: this schedule just started (not a repeated poll tick while it was already
                // running) - if it's an Allow-List block list with AI off, every new page on an
                // already-allowed site will still trigger a permission prompt. Warn once, non-modally,
                // since nobody may be watching when a schedule auto-starts.
                if (!wasAlreadyRunning || previousActiveId != newSchedule.id)
                {
                    var modeForAiWarning = appSettings.arrFocusModes.FirstOrDefault(f => f.id == newSchedule.SelectedFocusModeId);
                    if (modeForAiWarning != null && modeForAiWarning.isAllowedListApproach && !newSchedule.bAIBlockingEnabled)
                    {
                        ShowScheduleStartedWithoutAiWarning(newSchedule, modeForAiWarning.name);
                    }
                }

                FocusAlignmentScoreManager.DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus.Clear();

                appSettings.sRunningFocusModeIntention = ""; //Reset
                appSettings.sCurrentTaskPlayingInToDoPlayer = ""; //Reset

                if (!newSchedule.isPaused) //check if it was running and paused
                    ((App)(Application.Current)).wndTrayMenu.btnPauseBlockingSchedule.IsChecked = true; //Active
                else
                    ((App)(Application.Current)).wndTrayMenu.btnPauseBlockingSchedule.IsChecked = false; //Paused
            }
        }

        // #1420: non-modal, self-dismissing heads-up shown when a schedule auto-starts with AI off on an
        // Allow-List block list. Uses FBMessageBox's existing auto-close mechanism (Show() instead of
        // ShowDialog() so it never blocks the schedule from actually running); an unattended 30s timeout
        // leaves btnClicked at its constructed default (No) so it can't silently flip a setting the user
        // never confirmed.
        private static void ShowScheduleStartedWithoutAiWarning(CustomBlockingSchedule schedule, string sBlockListName)
        {
            try
            {
                string sMessage = appSettings.GetConfigStringSafe("'<SCHEDULE>' started with AI off — its '<BLOCKLIST>' block list uses an Allow List, so you may get repeated prompts on already-allowed sites.", "blockingScheduleEditorVcInfo", "warningScheduleStartedNoAiText")
                    .Replace("<SCHEDULE>", schedule.name)
                    .Replace("<BLOCKLIST>", sBlockListName);

                FBMessageBox msg = new FBMessageBox(
                    sMessage,
                    appSettings.GetConfigStringSafe("AI isn't enabled", "focusInfo", "startingFocusModeInfo", "titleAllowListNoAiWarning"),
                    MessageBoxButton.YesNo,
                    appSettings.GetConfigStringSafe("Turn On AI", "focusInfo", "startingFocusModeInfo", "buttonTurnOnAiText"),
                    appSettings.GetConfigStringSafe("Save Without AI", "blockingScheduleEditorVcInfo", "buttonSaveWithoutAiText"),
                    "", false, "",
                    bAutoClose: true,
                    btnClicked: MessageBoxResult.No,
                    nAutoCloseSeconds: 30);
                msg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                msg.Closed += (s, e) =>
                {
                    if (msg.btnClicked == MessageBoxResult.Yes)
                    {
                        schedule.bAIBlockingEnabled = true;
                        AddBlockingSchedule(schedule, true);
                    }
                };
                msg.Show(); //non-modal - a schedule can auto-start with nobody watching
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowScheduleStartedWithoutAiWarning ex - " + ex.Message);
            }
        }

        public static void CheckCustomBlockingScheduleTimeToStop()
        {
            if (appSettings.TryGetActiveSchedule(out var currentSchedule))
            {
                CustomBlockingSchedule TempSchedule = GetActiveSchedule();
                if(TempSchedule == null || appSettings.ActiveSchedule != TempSchedule) //will be null if no time matches
                {
                    // Stop: update the captured reference, then clear the flag and null the field. Readers on
                    // the background thread use the capture-once accessors so they never deref a nulled field.
                    currentSchedule.isOn = false;
                    appSettings.isBlockingScheduleRunning = false;
                    appSettings.ActiveSchedule = null;
                }
                else
                {
                    //if a focusmode is running then also we need to stop it
                    if(appSettings.isFocusModeRunning || ((appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS) && (!appSettings.bHabitsPostponedState && !appSettings.bFocusSummaryPostponedState)))
                    {
                        currentSchedule.isOn = false;
                        appSettings.isBlockingScheduleRunning = false;
                        appSettings.ActiveSchedule = null;
                    }
                }
            }
        }

        // True when an iron-focus blocking schedule is currently active (running and not paused).
        // Focus session views use this to lock controls and show the active-schedule banner.
        public static bool IsIronFocusScheduleActive()
        {
            return appSettings.IsBlockingScheduleActiveAndNotPaused(out var schedule)
                && schedule.BlockLevel == FOCUSMODESTRICTNESS.IRON_FOCUS;
        }

        // Public so AppSettings.IsBlockingScheduleActiveAndNotPaused can re-verify against this authoritative,
        // stateless time-window check instead of trusting only the cached isBlockingScheduleRunning/
        // ActiveSchedule fields (#1314 - those are maintained by CheckCustomBlockingScheduleTime/ToStop calls
        // scattered across dispatcherTimer_Background's many nCurrentActivityType branches, and a gap in any
        // one of them leaves the cached "running" state stale past the schedule's actual end time).
        public static CustomBlockingSchedule GetActiveSchedule()
        {
            var now = DateTime.Now;
            int todayIndex = (int)now.DayOfWeek - 1;
            if (todayIndex < 0) todayIndex = 6; // Sunday fix → 6
            int yesterdayIndex = (todayIndex + 6) % 7; // day before today (indices 0-6 = Mon-Sun)

            foreach (var schedule in appSettings.arrCustomBlockingSchedules)
            {
                if (schedule.DaysOfWeek == null || schedule.DaysOfWeek.Length < 7)
                    continue;

                // Parse start and end
                if (!DateTime.TryParse(schedule.StartTimeValue, out DateTime start) ||
                    !DateTime.TryParse(schedule.EndTimeValue, out DateTime end))
                    continue;

                var nowTime = now.TimeOfDay;
                var startTime = start.TimeOfDay;
                var endTime = end.TimeOfDay;

                // Active check that handles schedules wrapping past midnight (e.g. 19:15 -> 07:00).
                // The day-of-week flag applies to the day the schedule STARTS: the evening portion
                // (now >= start) uses today's flag, and the after-midnight portion (now <= end) uses
                // yesterday's flag, since that window belongs to the schedule that started the day before.
                bool isActive;
                if (startTime <= endTime)
                {
                    // Same-day range (e.g. 09:00 -> 17:00)
                    isActive = schedule.DaysOfWeek[todayIndex] && nowTime >= startTime && nowTime <= endTime;
                }
                else if (nowTime >= startTime)
                {
                    // Overnight range, evening portion — started today
                    isActive = schedule.DaysOfWeek[todayIndex];
                }
                else if (nowTime <= endTime)
                {
                    // Overnight range, after-midnight portion — started yesterday
                    isActive = schedule.DaysOfWeek[yesterdayIndex];
                }
                else
                {
                    // Overnight range, but currently in the inactive gap (end < now < start)
                    isActive = false;
                }

                if (isActive)
                {
                    return schedule; // First match found — stop checking others
                }
            }

            return null; // No active schedule found
        }
    }
}
