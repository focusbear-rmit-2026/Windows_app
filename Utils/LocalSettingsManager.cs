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
            

            if (appSettings.sUserLocalDeviceSettings.Length > 0 && appSettings.sUserLocalDeviceSettings != null)
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

                            if (sMacOSSettingsOBJ.ContainsKey("kAiAllowUrlShowAlertEnabled"))
                            {
                                sMacOSSettingsOBJ.Remove("kAiAllowUrlShowAlertEnabled"); //Remove  
                                                                                         //Add new  
                                sMacOSSettingsOBJ.Add("kAiAllowUrlShowAlertEnabled", appSettings.bAiAllowUrlShowAlertEnabled);
                            }
                            else
                            {
                                sMacOSSettingsOBJ.Add("kAiAllowUrlShowAlertEnabled", appSettings.bAiAllowUrlShowAlertEnabled);
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
