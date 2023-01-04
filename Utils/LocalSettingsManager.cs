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
            //Right now we are just changing the ALways blocked URLs
            //so Let's get the settings that we saved, and update blocked URLS
            string sJson = appSettings.sUserLocalDeviceSettings;
            /*{

            "MacOS":{
              "kIsEasySkipEnabled":false,
               "kIsMeetingDetectionEnabled":false,
               "kHardcorePomodoroEnabled":false,
                "kIsOfficeModeEnabled":true,
                "kIsReducedMotion":false,
                "kPomodoroDurationMinutes":25,
                "kHardcorePomodoroArrDays":[

              ],

              "kIsPasswordRequireInSkipMorning":true,
                "kBreakDurationMinutes":5,
              "kPomodoroModeEnabled":true,
              "kIsPasswordRequireInChangeSettings":true,
              "kIsPasswordRequireInStopPomodoro":true,
            "kIsPasswordRequireInAbortShutoff":true,
             "kHardcorePomodoroFocusedCount":0,
              "kIsPasswordRequireInSkipEvening":true,
              "kIsPromptToRememberHabit":true,
              "kArrBlockedUrls":[
                 "facebook.com",
                 "twitter.com",
                 "instagram.com",
                 "tiktok.com",
                 "bbc.com",
                 "cnn.com"
              ],
              "kHardcorePomodoroFocusedName":"Diseño gráfico Trabajo",
              "kHideAllWindowInScreenShots":false,
              "kCuddlyModeEnabled":true,
              "kIsPasswordRequireInQuitApp":true,
              "kHardcorePomodoroSkipMicroBreaks":false
           },

           "Windows":{
           },

           "Android":{
            },

           "iOS":{
           },
           "Web":{
              "hasEditedSettings":false
           }
        }            */

            if (appSettings.sUserLocalDeviceSettings.Length > 0)
            {

                //Parse the JSON string
                try
                {
                    JObject LocalSettings = JObject.Parse(appSettings.sUserLocalDeviceSettings);

                    if (LocalSettings.ContainsKey("MacOS"))
                    {
                        string sMacOSSettings = LocalSettings.ContainsKey("MacOS") ? LocalSettings["MacOS"].ToString() : "";

                        JObject sMacOSSettingsOBJ = JObject.Parse(sMacOSSettings);

                        if (sMacOSSettingsOBJ.ContainsKey("kArrBlockedUrls"))
                        {
                            sMacOSSettingsOBJ.Remove("kArrBlockedUrls"); //Remove blocked URL
                            //Add new upadted blocked URLs
                            sMacOSSettingsOBJ.Add("kArrBlockedUrls", JArray.FromObject(appSettings.arrAlwaysBlockedURLs));
                        }
                       
                        //Now remove macOS from local and add updated one
                        LocalSettings.Remove("MacOS");
                        //add updated for macOS
                        LocalSettings.Add("MacOS", sMacOSSettingsOBJ);

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
                    LogWriter.LogWrite("ReadAndParseUserLocalDeviceSettingsJson() ex - " + ex.Message);
                }
            }
        }
    }
}
