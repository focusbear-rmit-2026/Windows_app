using Focus_Bear.API;
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

                                    JArray tempblockedUrls = (JArray)item.GetValue("days_of_week");
                                    int[] activeDays = tempblockedUrls.ToObject<int[]>();
                                    
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

                                    appSettings.arrCustomBlockingSchedules.Add(new CustomBlockingSchedule(id, name, false, startTime, endTime, daysOfWeek, focusModeId, FMS, isAiBlockingEnabled, isMicroBreaksEnabled)); 
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

            appSettings.ActiveSchedule = GetActiveSchedule();
            if(appSettings.ActiveSchedule != null)
            {
                //Start it
                appSettings.ActiveSchedule.isOn = true;
                appSettings.isBlockingScheduleRunning = true;

                if(!appSettings.ActiveSchedule.isPaused) //check if it was running and paused
                    ((App)(Application.Current)).wndTrayMenu.btnPauseBlockingSchedule.IsChecked = true; //Active
                else
                    ((App)(Application.Current)).wndTrayMenu.btnPauseBlockingSchedule.IsChecked = false; //Paused
            }
        }

        public static void CheckCustomBlockingScheduleTimeToStop()
        {
            if (appSettings.ActiveSchedule != null && appSettings.isBlockingScheduleRunning)
            {
                CustomBlockingSchedule TempSchedule = GetActiveSchedule();
                if(TempSchedule == null) //will be null if no time matches
                {
                    appSettings.ActiveSchedule.isOn = false;
                    appSettings.isBlockingScheduleRunning = false;
                    appSettings.ActiveSchedule = null; 
                }
                else
                {
                    //if a focusmode is running then also we need to stop it
                    if(appSettings.isFocusModeRunning || ((appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS) && (!appSettings.bHabitsPostponedState && !appSettings.bFocusSummaryPostponedState)))
                    {
                        appSettings.ActiveSchedule.isOn = false;
                        appSettings.isBlockingScheduleRunning = false;
                        appSettings.ActiveSchedule = null;
                    }
                }
            }
        }
        private static CustomBlockingSchedule GetActiveSchedule()
        {
            var now = DateTime.Now;
            int todayIndex = (int)now.DayOfWeek - 1;
            if (todayIndex < 0) todayIndex = 6; // Sunday fix → 6

            foreach (var schedule in appSettings.arrCustomBlockingSchedules)
            {
                if (schedule.DaysOfWeek == null || schedule.DaysOfWeek.Length < 7)
                    continue;

                // Check if this schedule is active today
                if (!schedule.DaysOfWeek[todayIndex])
                    continue;

                // Parse start and end
                if (!DateTime.TryParse(schedule.StartTimeValue, out DateTime start) ||
                    !DateTime.TryParse(schedule.EndTimeValue, out DateTime end))
                    continue;

                var nowTime = now.TimeOfDay;
                var startTime = start.TimeOfDay;
                var endTime = end.TimeOfDay;

                // Check if current time is within range
                if (nowTime >= startTime && nowTime <= endTime)
                {
                    return schedule; // First match found — stop checking others
                }
            }

            return null; // No active schedule found
        }
    }
}
