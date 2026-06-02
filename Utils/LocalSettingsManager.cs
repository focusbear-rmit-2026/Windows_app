using Focus_Bear.API;
using Focus_Bear.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Focus_Bear.Utils
{
    public static class LocalSettingsManager
    {
        static AppSettings appSettings = AppSettings.GetInstance;
        public static void PutLocalSettings()
        {
            if(!RegistryManager.GetHasEditedBlockedURLs())
            {
                if (appSettings.bHasEditedBlockedURLs)
                    RegistryManager.SetHasEditedBlockedURLs();
            }
           
            string sJson = appSettings.sUserLocalDeviceSettings;
            

            if (appSettings.sUserLocalDeviceSettings != null && appSettings.sUserLocalDeviceSettings.Length > 0 )
            {

                //Parse the JSON string
                try
                {
                    JObject LocalSettings = JObject.Parse(appSettings.sUserLocalDeviceSettings);

                    if (LocalSettings.ContainsKey("MacOS"))
                    {
                        string sMacOSSettings = LocalSettings.ContainsKey("MacOS") ? LocalSettings["MacOS"].ToString() : "";

                        if (sMacOSSettings.Length > 0)
                        {
                            JObject sMacOSSettingsOBJ = JObject.Parse(sMacOSSettings);

                            if (sMacOSSettingsOBJ.ContainsKey("kArrBlockedUrls"))
                            {
                                sMacOSSettingsOBJ.Remove("kArrBlockedUrls"); //Remove blocked URL
                                                                             //Add new upadted blocked URLs
                                sMacOSSettingsOBJ.Add("kArrBlockedUrls", JArray.FromObject(appSettings.arrAlwaysBlockedURLs));
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kArrBlockedUrls", JArray.FromObject(appSettings.arrAlwaysBlockedURLs));
                            }

                            if (sMacOSSettingsOBJ.ContainsKey("kArrAlwaysAllowedAppsWin"))
                            {
                                sMacOSSettingsOBJ.Remove("kArrAlwaysAllowedAppsWin"); 
                                                                              
                                sMacOSSettingsOBJ.Add("kArrAlwaysAllowedAppsWin", JArray.FromObject(appSettings.arrAlwaysAllowedApps));
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kArrAlwaysAllowedAppsWin", JArray.FromObject(appSettings.arrAlwaysAllowedApps));
                            }

                            if (sMacOSSettingsOBJ.ContainsKey("kArrAlwaysBlockedAppsWin"))
                            {
                                sMacOSSettingsOBJ.Remove("kArrAlwaysBlockedAppsWin");

                                sMacOSSettingsOBJ.Add("kArrAlwaysBlockedAppsWin", JArray.FromObject(appSettings.arrAlwaysBlockedApps));
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kArrAlwaysBlockedAppsWin", JArray.FromObject(appSettings.arrAlwaysBlockedApps));
                            }

                            if (sMacOSSettingsOBJ.ContainsKey("kCuddlyModeEnabled"))
                            {
                                sMacOSSettingsOBJ.Remove("kCuddlyModeEnabled"); //Remove blocked URL
                                                                                  //Add new upadted blocked URLs
                                sMacOSSettingsOBJ.Add("kCuddlyModeEnabled", (appSettings.nFocusBearMode == 1) ? true : false);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kCuddlyModeEnabled", (appSettings.nFocusBearMode == 1) ? true : false);
                            }


                            if (sMacOSSettingsOBJ.ContainsKey("kIsOfficeModeEnabled"))
                            {
                                sMacOSSettingsOBJ.Remove("kIsOfficeModeEnabled"); //Remove blocked URL
                                                                                  //Add new upadted blocked URLs
                                sMacOSSettingsOBJ.Add("kIsOfficeModeEnabled", appSettings.isOfficeModeEnabled);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kIsOfficeModeEnabled", appSettings.isOfficeModeEnabled);
                            }

                            if (sMacOSSettingsOBJ.ContainsKey("kAppPassword"))
                            {
                                sMacOSSettingsOBJ.Remove("kAppPassword"); //Remove blocked URL
                                                                             //Add new upadted blocked URLs
                                sMacOSSettingsOBJ.Add("kAppPassword", appSettings.sUserPassword);
                            }
                            else
                            {
                                if(appSettings.isPasswordSet)
                                    sMacOSSettingsOBJ.Add("kAppPassword", appSettings.sUserPassword);
                            }

                            // bPasswordRequireInStopPomodoro = sMacOSSettingsOBJ.ContainsKey("kIsPasswordRequireInStopPomodoro") ? (bool)sMacOSSettingsOBJ.GetValue("kIsPasswordRequireInStopPomodoro") : false;

                            if (sMacOSSettingsOBJ.ContainsKey("kIsPasswordRequireInChangeSettings"))
                            {
                                sMacOSSettingsOBJ.Remove("kIsPasswordRequireInChangeSettings"); //Remove  
                                                                                                //Add new updated
                                sMacOSSettingsOBJ.Add("kIsPasswordRequireInChangeSettings", (appSettings.bPasswordRequireInChangeSettings));
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kIsPasswordRequireInChangeSettings", (appSettings.bPasswordRequireInChangeSettings)); 
                            }
                            if (sMacOSSettingsOBJ.ContainsKey("kIsPasswordRequireInSkipMorning"))
                            {
                                sMacOSSettingsOBJ.Remove("kIsPasswordRequireInSkipMorning"); //Remove  
                                                                                  //Add new updated  
                                sMacOSSettingsOBJ.Add("kIsPasswordRequireInSkipMorning", (appSettings.bPasswordRequireInSkipMorning));
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kIsPasswordRequireInSkipMorning", (appSettings.bPasswordRequireInSkipMorning));
                            }
                            if (sMacOSSettingsOBJ.ContainsKey("kIsPasswordRequireInSkipEvening"))
                            {
                                sMacOSSettingsOBJ.Remove("kIsPasswordRequireInSkipEvening"); //Remove  
                                                                                             //Add new updated
                                sMacOSSettingsOBJ.Add("kIsPasswordRequireInSkipEvening", (appSettings.bPasswordRequireInSkipEvening));
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kIsPasswordRequireInSkipEvening", (appSettings.bPasswordRequireInSkipEvening));
                            }
                            if (sMacOSSettingsOBJ.ContainsKey("kIsPasswordRequireInAbortShutoff"))
                            {
                                sMacOSSettingsOBJ.Remove("kIsPasswordRequireInAbortShutoff"); //Remove  
                                                                                  //Add new 
                                sMacOSSettingsOBJ.Add("kIsPasswordRequireInAbortShutoff", (appSettings.bPasswordRequireInAbortShutoff));
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kIsPasswordRequireInAbortShutoff", (appSettings.bPasswordRequireInAbortShutoff));
                            }
                            if (sMacOSSettingsOBJ.ContainsKey("kIsPasswordRequireInStopPomodoro"))
                            {
                                sMacOSSettingsOBJ.Remove("kIsPasswordRequireInStopPomodoro");
                                sMacOSSettingsOBJ.Add("kIsPasswordRequireInStopPomodoro", appSettings.bPasswordRequireInStopPomodoro);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kIsPasswordRequireInStopPomodoro", appSettings.bPasswordRequireInStopPomodoro);
                            }
                            if (sMacOSSettingsOBJ.ContainsKey("kIsPasswordRequireInQuitApp"))
                            {
                                sMacOSSettingsOBJ.Remove("kIsPasswordRequireInQuitApp"); //Remove  
                                                                                  //Add new  
                                sMacOSSettingsOBJ.Add("kIsPasswordRequireInQuitApp",  appSettings.bPasswordRequireInQuitApp);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kIsPasswordRequireInQuitApp", appSettings.bPasswordRequireInQuitApp);
                            }

                            if (sMacOSSettingsOBJ.ContainsKey("kUserdefaultsPasswordRequireInUseRelaxFocusMode"))
                            {
                                sMacOSSettingsOBJ.Remove("kUserdefaultsPasswordRequireInUseRelaxFocusMode"); //Remove  
                                                                                         //Add new  
                                sMacOSSettingsOBJ.Add("kUserdefaultsPasswordRequireInUseRelaxFocusMode", appSettings.bPasswordRequireToUseRelaxFocusMode);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kUserdefaultsPasswordRequireInUseRelaxFocusMode", appSettings.bPasswordRequireToUseRelaxFocusMode);
                            }

                            if (sMacOSSettingsOBJ.ContainsKey("kIsPasswordRequireInSkipEveningAfterCutOff"))
                            {
                                sMacOSSettingsOBJ.Remove("kIsPasswordRequireInSkipEveningAfterCutOff"); //Remove  
                                                                                         //Add new  
                                sMacOSSettingsOBJ.Add("kIsPasswordRequireInSkipEveningAfterCutOff", appSettings.bPasswordRequireToSkipEveningAfterCutOff);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kIsPasswordRequireInSkipEveningAfterCutOff", appSettings.bPasswordRequireToSkipEveningAfterCutOff);
                            }

                            //AI Tab settings
                            if (sMacOSSettingsOBJ.ContainsKey("kMotivationalMessageEnabled"))
                            {
                                sMacOSSettingsOBJ.Remove("kMotivationalMessageEnabled"); //Remove  
                                                                                         //Add new  
                                sMacOSSettingsOBJ.Add("kMotivationalMessageEnabled", appSettings.bMotivationalMessageEnabled);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kMotivationalMessageEnabled", appSettings.bMotivationalMessageEnabled);
                            }

                            if (sMacOSSettingsOBJ.ContainsKey("kAiCheckUrlEnabled"))
                            {
                                sMacOSSettingsOBJ.Remove("kAiCheckUrlEnabled"); //Remove  
                                                                                         //Add new  
                                sMacOSSettingsOBJ.Add("kAiCheckUrlEnabled", appSettings.bAiCheckUrlEnabled);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kAiCheckUrlEnabled", appSettings.bAiCheckUrlEnabled);
                            }

                            if (sMacOSSettingsOBJ.ContainsKey("has_edited_blocked_urls"))
                            {
                                sMacOSSettingsOBJ.Remove("has_edited_blocked_urls"); //Remove  
                                                                                         //Add new  
                                sMacOSSettingsOBJ.Add("has_edited_blocked_urls", appSettings.bHasEditedBlockedURLs);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("has_edited_blocked_urls", appSettings.bHasEditedBlockedURLs);
                            }

                            if (sMacOSSettingsOBJ.ContainsKey("kisFullScreenEnabled"))
                            {
                                sMacOSSettingsOBJ.Remove("kisFullScreenEnabled"); //Remove  
                                                                                         //Add new  
                                sMacOSSettingsOBJ.Add("kisFullScreenEnabled", appSettings.bPlayActivitiesInFullScreenMode);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kisFullScreenEnabled", appSettings.bPlayActivitiesInFullScreenMode);
                            }

                            //Pomodoro related settings.
                            if (sMacOSSettingsOBJ.ContainsKey("kPomodoroModeEnabled"))
                            {
                                sMacOSSettingsOBJ.Remove("kPomodoroModeEnabled"); //Remove  
                                                                                  //Add new  
                                sMacOSSettingsOBJ.Add("kPomodoroModeEnabled", appSettings.bEnterPomodoroModeAfterMorningRoutine);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kPomodoroModeEnabled", appSettings.bEnterPomodoroModeAfterMorningRoutine);
                            }

                            if (sMacOSSettingsOBJ.ContainsKey("kHardcorePomodoroEnabled"))
                            {
                                sMacOSSettingsOBJ.Remove("kHardcorePomodoroEnabled"); //Remove  
                                                                                  //Add new  
                                sMacOSSettingsOBJ.Add("kHardcorePomodoroEnabled", appSettings.bAutomaticUseSuperPomodoroModeSetting);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kHardcorePomodoroEnabled", appSettings.bAutomaticUseSuperPomodoroModeSetting);
                            }

                            if (sMacOSSettingsOBJ.ContainsKey("kPomodoroDurationMinutes"))
                            {
                                sMacOSSettingsOBJ.Remove("kPomodoroDurationMinutes"); //Remove  
                                                                                      //Add new  
                                sMacOSSettingsOBJ.Add("kPomodoroDurationMinutes", appSettings.PomodoroMinutes);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kPomodoroDurationMinutes", appSettings.PomodoroMinutes);
                            }


                            if (sMacOSSettingsOBJ.ContainsKey("kBreakDurationMinutes"))
                            {
                                sMacOSSettingsOBJ.Remove("kBreakDurationMinutes"); //Remove  
                                                                                      //Add new  
                                sMacOSSettingsOBJ.Add("kBreakDurationMinutes", appSettings.PomodoroBreakMinutes);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kBreakDurationMinutes", appSettings.PomodoroBreakMinutes);
                            }

                            if (sMacOSSettingsOBJ.ContainsKey("kHardcorePomodoroFocusedName"))
                            {
                                sMacOSSettingsOBJ.Remove("kHardcorePomodoroFocusedName"); //Remove  
                                                                                   //Add new  
                                sMacOSSettingsOBJ.Add("kHardcorePomodoroFocusedName", appSettings.AutomaticSuperPomodoroFocusedName);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kHardcorePomodoroFocusedName", appSettings.AutomaticSuperPomodoroFocusedName);
                            }

                            if (sMacOSSettingsOBJ.ContainsKey("kHardcorePomodoroFocusedCount"))
                            {
                                sMacOSSettingsOBJ.Remove("kHardcorePomodoroFocusedCount"); //Remove  
                                                                                          //Add new  
                                sMacOSSettingsOBJ.Add("kHardcorePomodoroFocusedCount", appSettings.AutomaticNumberOfSuperPomodorosToRun);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kHardcorePomodoroFocusedCount", appSettings.AutomaticNumberOfSuperPomodorosToRun);
                            }

                            if (sMacOSSettingsOBJ.ContainsKey("kHardcorePomodoroSkipMicroBreaks"))
                            {
                                sMacOSSettingsOBJ.Remove("kHardcorePomodoroSkipMicroBreaks"); //Remove  
                                                                                           //Add new  
                                sMacOSSettingsOBJ.Add("kHardcorePomodoroSkipMicroBreaks", appSettings.bAutomaticSuperPomodoroSkipMicroBreaks);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kHardcorePomodoroSkipMicroBreaks", appSettings.bAutomaticSuperPomodoroSkipMicroBreaks);
                            }

                            if (sMacOSSettingsOBJ.ContainsKey("kHardcorePomodoroArrDays"))
                            {
                                sMacOSSettingsOBJ.Remove("kHardcorePomodoroArrDays"); //Remove  
                                                                                              //Add new  
                                sMacOSSettingsOBJ.Add("kHardcorePomodoroArrDays", appSettings.AutomaticSuperPomodoroSelectedDaysArray);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kHardcorePomodoroArrDays", appSettings.AutomaticSuperPomodoroSelectedDaysArray);
                            }

                            if (sMacOSSettingsOBJ.ContainsKey("kImmediatelyBlockDistractingSites"))
                            {
                                sMacOSSettingsOBJ.Remove("kImmediatelyBlockDistractingSites"); //Remove  
                                                                                                        //Add new  
                                sMacOSSettingsOBJ.Add("kImmediatelyBlockDistractingSites", appSettings.bImmediatelyBlockDistractingSites);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kImmediatelyBlockDistractingSites", appSettings.bImmediatelyBlockDistractingSites);
                            }


                            //Meetings Call detection related
                            if (sMacOSSettingsOBJ.ContainsKey("kIsMeetingDetectionEnabled"))
                            {
                                sMacOSSettingsOBJ.Remove("kIsMeetingDetectionEnabled"); //Remove  
                                                                                               //Add new  
                                sMacOSSettingsOBJ.Add("kIsMeetingDetectionEnabled", appSettings.bEnableMeetingsDetection);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kIsMeetingDetectionEnabled", appSettings.bEnableMeetingsDetection);
                            }

                            if (sMacOSSettingsOBJ.ContainsKey("kShowMeetingDetectionAlert"))
                            {
                                sMacOSSettingsOBJ.Remove("kShowMeetingDetectionAlert"); //Remove  
                                                                                        //Add new  
                                sMacOSSettingsOBJ.Add("kShowMeetingDetectionAlert", appSettings.bAutomaticallySuggestStartingMeetingsFocus);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kShowMeetingDetectionAlert", appSettings.bAutomaticallySuggestStartingMeetingsFocus);
                            }


                            if (sMacOSSettingsOBJ.ContainsKey("kIsBlockDistractingSitesDuringMeetingsEnabled"))
                            {
                                sMacOSSettingsOBJ.Remove("kIsBlockDistractingSitesDuringMeetingsEnabled"); //Remove  
                                                                                        //Add new  
                                sMacOSSettingsOBJ.Add("kIsBlockDistractingSitesDuringMeetingsEnabled", appSettings.bBlockSuperDistractingSitesDuringMeetings);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kIsBlockDistractingSitesDuringMeetingsEnabled", appSettings.bBlockSuperDistractingSitesDuringMeetings);
                            }

                            if (sMacOSSettingsOBJ.ContainsKey("kIsShowBreaksInCallEnabled"))
                            {
                                sMacOSSettingsOBJ.Remove("kIsShowBreaksInCallEnabled"); //Remove  
                                                                                                           //Add new  
                                sMacOSSettingsOBJ.Add("kIsShowBreaksInCallEnabled", appSettings.bShowBreaksWhileInCall);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kIsShowBreaksInCallEnabled", appSettings.bShowBreaksWhileInCall);
                            }

                            if (sMacOSSettingsOBJ.ContainsKey("kIsAdvancedAnalyticsInFocusModeEnabled"))
                            {
                                sMacOSSettingsOBJ.Remove("kIsAdvancedAnalyticsInFocusModeEnabled"); //Remove  
                                                                                        //Add new  
                                sMacOSSettingsOBJ.Add("kIsAdvancedAnalyticsInFocusModeEnabled", appSettings.bAdvancedAnalyticsInFocusModeEnabled);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kIsAdvancedAnalyticsInFocusModeEnabled", appSettings.bAdvancedAnalyticsInFocusModeEnabled);
                            }

                            //Simple / geek mode setting
                            if (sMacOSSettingsOBJ.ContainsKey("kIsGeekModeEnabled"))
                            {
                                sMacOSSettingsOBJ.Remove("kIsGeekModeEnabled"); //Remove  
                                                                                        //Add new  
                                sMacOSSettingsOBJ.Add("kIsGeekModeEnabled", appSettings.bIsGeekModeEnabled);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kIsGeekModeEnabled", appSettings.bIsGeekModeEnabled);
                            }

                            //breaks enabled or disabled
                            if (sMacOSSettingsOBJ.ContainsKey("kUserdefaultsBreaksEnabledFromOnboarding"))
                            {
                                sMacOSSettingsOBJ.Remove("kUserdefaultsBreaksEnabledFromOnboarding"); //Remove  
                                                                                //Add new  
                                sMacOSSettingsOBJ.Add("kUserdefaultsBreaksEnabledFromOnboarding", appSettings.bAreBreaksEnabled);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kUserdefaultsBreaksEnabledFromOnboarding", appSettings.bAreBreaksEnabled);
                            }

                            if (sMacOSSettingsOBJ.ContainsKey("kAutoCloseAfterRoutineSkipPostpone"))
                            {
                                sMacOSSettingsOBJ.Remove("kAutoCloseAfterRoutineSkipPostpone");
                                sMacOSSettingsOBJ.Add("kAutoCloseAfterRoutineSkipPostpone", appSettings.AutoCloseAfterRoutineSkipPostpone);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kAutoCloseAfterRoutineSkipPostpone", appSettings.AutoCloseAfterRoutineSkipPostpone);
                            }

                            //Blocks schedule related
                            if (sMacOSSettingsOBJ.ContainsKey("kBlockingSchedule"))
                            {
                                sMacOSSettingsOBJ.Remove("kBlockingSchedule"); //Remove  
                                                                                                      //Add new  
                                sMacOSSettingsOBJ.Add("kBlockingSchedule", (int) appSettings.nBlockingSchedule);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kBlockingSchedule", (int) appSettings.nBlockingSchedule);
                            }

                            //Now remove macOS from local and add updated one
                            LocalSettings.Remove("MacOS");

                            //add updated for macOS
                            LocalSettings.Add("MacOS", sMacOSSettingsOBJ);
                        }
                        else
                        {
                            JObject sMacOSSettingsOBJ = new JObject();
                            sMacOSSettingsOBJ.Add("kArrBlockedUrls", JArray.FromObject(appSettings.arrAlwaysBlockedURLs));
                            sMacOSSettingsOBJ.Add("kIsOfficeModeEnabled", JsonConvert.SerializeObject(appSettings.isOfficeModeEnabled));

                            //Now remove macOS from local and add updated one
                            LocalSettings.Remove("MacOS");

                            //add updated for macOS
                            LocalSettings.Add("MacOS", sMacOSSettingsOBJ);
                        }
                       

                        //Also save the udpated settings to Registry
                        appSettings.sUserLocalDeviceSettings = LocalSettings.ToString();
                        RegistryManager.SaveLocalDeviceSettings();

                        FocusBearAPIClient client = new FocusBearAPIClient();
                        LogWriter.LogWrite("attempt to PutUserLocalDeviceSettingsInfo for Local Device Settings");
                        var userSettingsResponse = client.PutUserLocalDeviceSettingsInfo(LocalSettings);

                    }

                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("PutUserLocalDeviceSettingsInfo() ex - " + ex.Message);
                }
            }
        }
    }
}
