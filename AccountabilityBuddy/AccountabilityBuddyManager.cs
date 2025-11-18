using Focus_Bear.API;
using Focus_Bear.FocusMusic;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Focus_Bear.AccountabilityBuddy
{
    public static class AccountabilityBuddyManager
    {

        private static AppSettings appSettings = AppSettings.GetInstance;

        public static void ReadAndParseAccountabilityBuddyList()
        {
            LogWriter.LogWrite("ReadAndParseAccountabilityBuddyList()");
            try
            {
                //Check is settings are there
                if (appSettings.sRawBuddiesList == null || appSettings.sRawBuddiesList.Length == 0)
                {
                    LogWriter.LogWrite("sRawBuddiesList == null - Read previously saved data from registry");
                    //Read previously saved in registry
                    RegistryManager.GetSavedBuddyList();
                }
                if (appSettings.sRawBuddiesList.Length > 0)
                {

                    //Parse the JSON string
                    try
                    {
                        JArray BuddiesList = JArray.Parse(appSettings.sRawBuddiesList);

                        if (BuddiesList != null && BuddiesList.Count() > 0)
                        {
                            foreach (JObject item in BuddiesList)
                            {
                                try
                                {
                                    string id = item.ContainsKey("id") ? item.GetValue("id").ToString() : "";
                                    string buddy_user_id = item.ContainsKey("buddy_user_id") ? item.GetValue("buddy_user_id").ToString() : "";
                                    string buddy_email = item.ContainsKey("buddy_email") ? item.GetValue("buddy_email").ToString() : "";
                                    string invitation_status = item.ContainsKey("invitation_status") ? item.GetValue("invitation_status").ToString() : "";
                                   
                                    appSettings.arrBuddiesList.Add(new BuddyDetails(id, buddy_user_id, buddy_email, invitation_status));
                                }
                                catch (Exception ex)
                                {
                                    LogWriter.LogWrite("ReadAndParseAccountabilityBuddyList() Reading item in arrBuddiesList failed ex = " + ex.Message);
                                    continue;  
                                }
                            }

                        }

                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("ReadAndParseAccountabilityBuddyList() ex - " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ReadAndParseAccountabilityBuddyList() top ex -" + ex.Message);
            }
        }

        public static async Task <bool> GetApprovedUnlockRequests()
        {
            FocusBearAPIClient client = new FocusBearAPIClient();
            var _Response = await client.GetAllUnlockRequests();
            int mBuddy = int.Parse(_Response.ToString());
            if (mBuddy == 1)
            {
                LogWriter.LogWrite("GetAllUnlockRequests Success");
                //unlocks list retrieved Successfully, Process them.

                // Parse the JSON string
                JObject jsonObj = JObject.Parse(appSettings.sUnlockRequestsInfo);

                // Extract "data" as JArray
                JArray dataArray = (JArray)jsonObj["data"];

                foreach (var item in dataArray)
                {
                    string id = item["id"]?.ToString();
                    string status = item["status"]?.ToString();
                    int minutes = item["unlock_duration_minutes"]?.Value<int>() ?? 0;
                    if (status == "approved")
                    {
                        appSettings.UnlockTillTime = DateTime.Now.AddMinutes(minutes);
                        return true;
                    }
                }

               
            }
            else
            {
                LogWriter.LogWrite("GetAllUnlockRequests Failed");
            }
            return false;
        }
    }
}
