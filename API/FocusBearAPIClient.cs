
using Focus_Bear.Models;
using Focus_Bear.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Focus_Bear.API
{
    class FocusBearAPIClient
    {

        AppSettings appSettings = AppSettings.GetInstance;

        public static string PostDevice { get { return "https://api.focusbear.io/device"; } }
        public string UserSettings { get { return "https://api.focusbear.io/user-settings"; } set { } }

        public static string CompletedActivity { get { return "https://api.focusbear.io/completed-activity"; } }
        public static string SkipActivity { get { return "https://api.focusbear.io/completed-activity/skip"; } }
        public static string CurrentActivityProps { get { return "https://api.focusbear.io/user/details/current-activity-props"; } }

        public static string GetUserDetails { get { return "https://api.focusbear.io/user/details"; } }

        public static string FocusMode { get { return "https://api.focusbear.io/focus-mode"; } }

        public static string GetUserLocalDeviceSettings { get { return "https://api.focusbear.io/user-local-device-settings"; } }

        public static string SubscriptionInfo { get { return "https://api.focusbear.io/user/subscription"; } }

        internal async Task<int> RenewToken()
        {
            //Renew the token and check again
            LogWriter.LogWrite("Attempting Renew Token before API call");
            var authenticator = new Authentication();
            var x = await authenticator.RefreshToken(appSettings.userAccount.refreshToken);
            if (x)
            {
                //token renewed
                LogWriter.LogWrite("RenewToken DONE before API call");
                if (authenticator._isEmptyOrInvalid(appSettings.userAccount.accessToken))
                {
                    return 0;
                }
                //All Good, Proceed Normally
                return 1;
            }
            else
            {
                //token renewed failed
                return 0;
            }
        }

        HttpClient GetClientWithHeaders()
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("platform", "Windows");
            client.DefaultRequestHeaders.Add("app-version", appSettings.AppVersion.ToString());
            client.DefaultRequestHeaders.Add("x-focus-bear-user-id", appSettings.userAccount.userID);

            if (!string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appSettings.userAccount.accessToken);
            }

            return client;
        }

        internal async Task <bool> IsInternetOn()
        {
            if (NetworkInterface.GetIsNetworkAvailable())
            {
                try
                {
                    HttpClient client = new HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(4);
                    var response =  await client.GetAsync("https://api.focusbear.io/api").ConfigureAwait(false);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                catch (HttpRequestException e)
                {
                    LogWriter.LogWrite("\nInternet Exception Caught! - " + e.Message);
                }
                catch (WebException ex)
                {
                    LogWriter.LogWrite("IsInternetOn WebException" + ex.Message);
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("IsInternetOn - Exception" + ex.Message);
                }
                return false;
            }
            else
            {
                return false;
            }
        }

        internal async Task<int> PostActivityCompleted(string activity_id, string activity_sequence_id, DateTime start_time, int duration_logged, string choice_id, /*Double quantity_logged = -1*/ List<QuantityQuestion> logQuantityQuestions = null, bool is_skipped = false)
        {
            LogWriter.LogWrite("PostActivityCompleted() = " + activity_id);
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();

                    DateTime finish_time = start_time;

                    bool bTrue = true;
                    string metadata = $"{{}}"; ;

                    if (is_skipped) //if activity is skipped just add one second to finish time
                    {
                        metadata = $"{{\"skipped_did_complete\":{JsonConvert.SerializeObject(bTrue)}}}";
                        finish_time = finish_time.AddSeconds(1);
                    }
                    else
                    {
                        finish_time = finish_time.AddSeconds(duration_logged);
                    }
                   
                   
                    string addItemJsonString = $"{{\"activity_id\":\"{activity_id}\"," +
                        $"\"duration_logged\":{duration_logged}," +
                        $"\"device_id\":\"{appSettings.sDeviceID}\"," +
                        $"\"activity_sequence_id\":\"{activity_sequence_id}\"," +
                        $"\"start_time\":\"{start_time.ToString("o")}\"," +
                        $"\"finish_time\":\"{finish_time.ToString("o")}\"," +
                         $"\"metadata\":{metadata},";

                    if ( choice_id.Length > 0)
                    {
                        addItemJsonString = addItemJsonString + $"\"choice_id\":\"{choice_id}\",";
                    }

                    if (logQuantityQuestions != null)
                    {
                        string LogQuantityAnswers = $"[";
                        foreach (QuantityQuestion question in logQuantityQuestions)
                        {
                            LogQuantityAnswers += $"{{\"question_id\":\"{question.id}\"," +
                            $"\"logged_value\":{question.quantity}}},";
                        }
                        LogQuantityAnswers = LogQuantityAnswers.Remove(LogQuantityAnswers.Length - 1, 1);
                        LogQuantityAnswers = LogQuantityAnswers + "]";

                        addItemJsonString = addItemJsonString + $"\"log_quantity_answers\":{LogQuantityAnswers},";
                    }

                    //Remove last comma -  we have 100% added above
                    addItemJsonString = addItemJsonString.Remove(addItemJsonString.Length - 1, 1);
                    Console.WriteLine(addItemJsonString);

                    addItemJsonString = addItemJsonString + "}";

                    

                    if (!string.IsNullOrEmpty(addItemJsonString))
                    {
                        StringContent content = new StringContent(addItemJsonString, Encoding.UTF8, "application/json");

                        var response = await client.PostAsync(CompletedActivity, content);
                        if (response.StatusCode == System.Net.HttpStatusCode.Created) //201
                        {
                            LogWriter.LogWrite("PostActivityCompleted() SUCCESS");

                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);

                                return 1;
                            }
                        }
                        else
                        {
                            if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError) //500
                            {
                                SaveEntryInOfflineActivitiesCSV(activity_id, activity_sequence_id, choice_id, start_time, duration_logged, logQuantityQuestions, is_skipped);
                            }
                            
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                string sErrorMessage = resJson["message"].ToString();
                                LogWriter.LogWrite("PostActivityCompleted() FAILED - " + sErrorMessage);
                                return 0;
                            }
                            return 0;
                        }
                    }
                    else
                    {
                        return 0;
                    }
                }
                else
                {
                    SaveEntryInOfflineActivitiesCSV(activity_id, activity_sequence_id, choice_id, start_time, duration_logged, logQuantityQuestions, is_skipped);
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("PostActivityCompleted WebException" + ex.Message);

                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PostActivityCompleted Exception" + ex.Message);
 
            }
            return 0;
        }

        internal void SaveEntryInOfflineActivitiesCSV(string activity_id, string activity_sequence_id, string choice_id, DateTime start_time, int duration_logged, List<QuantityQuestion> logQuantityQuestions, bool is_skipped)
        {
            DateTime finish_time = start_time;
            if (is_skipped) //if activity is skipped just add one second to finish time
            {
                finish_time = finish_time.AddSeconds(1);
            }
            else
            {
                finish_time = finish_time.AddSeconds(duration_logged);
            }

            string LogQuantityAnswers = "";
            if (logQuantityQuestions != null)
            {
                LogQuantityAnswers = $"[";
                foreach (QuantityQuestion question in logQuantityQuestions)
                {
                    LogQuantityAnswers += $"{{\"question_id\":\"{question.id}\";" +
                    $"\"logged_value\":{question.quantity}}};";
                }
                LogQuantityAnswers = LogQuantityAnswers.Remove(LogQuantityAnswers.Length - 1, 1);
                LogQuantityAnswers = LogQuantityAnswers + "]";
            }

            appSettings.AddEntryInOfflineActivitiesCSV(activity_id, duration_logged.ToString(), activity_sequence_id, start_time.ToString("o"), finish_time.ToString("o"), (is_skipped ? "true" : ""), choice_id, LogQuantityAnswers, "", "");

        }
        internal async Task<int> PostActivitySkipped(string activity_id, string activity_sequence_id)
        {
            LogWriter.LogWrite("PostActivitySkipped() = " + activity_id);
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();

                    string addItemJsonString = $"{{\"activity_id\":\"{activity_id}\"," +
                        $"\"device_id\":\"{appSettings.sDeviceID}\"," +
                        $"\"activity_sequence_id\":\"{activity_sequence_id}\"}}";

                    if (!string.IsNullOrEmpty(addItemJsonString))
                    {
                        StringContent content = new StringContent(addItemJsonString, Encoding.UTF8, "application/json");

                        var response = await client.PostAsync(SkipActivity, content);
                        if (response.StatusCode == System.Net.HttpStatusCode.Created) //201
                        {
                            LogWriter.LogWrite("PostActivitySkipped() SUCCESS");

                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);

                                return 1;
                            }
                        }
                        else
                        {
                            if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError) //500
                            {
                                DateTime start_time = DateTime.UtcNow;
                                DateTime finish_time = start_time.AddSeconds(1);

                                appSettings.AddEntryInOfflineActivitiesCSV(activity_id, "", activity_sequence_id, start_time.ToString("o"), finish_time.ToString("o"), "true", "", "", "", "");
                            }
                            //Get Device ID Failed
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                string sErrorMessage = resJson["message"].ToString();
                                LogWriter.LogWrite("PostActivitySkipped() FAILED - " + sErrorMessage);
                                return 0;
                            }
                            return 0;
                        }
                    }
                    else
                    {
                        return 0;
                    }
                }
                else
                {
                    DateTime start_time = DateTime.UtcNow;
                    DateTime finish_time = start_time.AddSeconds(1);

                    appSettings.AddEntryInOfflineActivitiesCSV(activity_id, "", activity_sequence_id, start_time.ToString("o"), finish_time.ToString("o"), "true", "", "", "", "");
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("PostActivitySkipped WebException" + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PostActivitySkipped Exception" + ex.Message);
            }
            return 0;
        }

        internal async Task<int> ForceCompleteCurrentSequence(string activity_sequence_id, bool bTodaysActivity = false) //OK
        {
            LogWriter.LogWrite("ForceCompleteCurrentSequence() + " + activity_sequence_id);
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();

                    // dummy payload
                    String jsonString = JsonConvert.SerializeObject(
                        new
                        {
                            operating_system = "Windows",
                            metadata = new { }
                        }
                    );


                    if (!string.IsNullOrEmpty(jsonString))
                    {   
                        StringContent content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                        string apiPath = string.Format("https://api.focusbear.io/completed-activity-sequence/{0}/force-complete-current-sequence", activity_sequence_id);

                        if(bTodaysActivity)
                        {
                            apiPath = apiPath + "?cancel_habits_for_today=true";
                        }

                        var response = await client.PostAsync(apiPath, content);
                        if (response.StatusCode == System.Net.HttpStatusCode.Created) //201
                        {
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);

                                LogWriter.LogWrite("ForceCompleteCurrentSequence() SUCCESS");

                                return 1;
                            }
                        }
                        else
                        {
                            //Get Device ID Failed
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                string sErrorMessage = resJson["message"].ToString();
                                LogWriter.LogWrite("ForceCompleteCurrentSequence() FAILED");
                                return 0;
                            }
                            return 0;
                        }
                    }
                    else
                    {
                        return 0;
                    }
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("ForceCompleteCurrentSequence WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ForceCompleteCurrentSequence Exception - " + ex.Message);
            }
            return 0;
        }

        internal async Task<int> PostDeviceID() //OK
        {
            LogWriter.LogWrite("PostDeviceID()");
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if(!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();
        
                    // dummy payload
                    String jsonString = JsonConvert.SerializeObject(
                        new 
                        {
                            operating_system = "Windows",
                            metadata = new { }
                        }
                    );

                    if (!string.IsNullOrEmpty(jsonString))
                    {
                        StringContent content = new StringContent(jsonString, Encoding.UTF8, "application/json");
                     
                        var response = await client.PostAsync(PostDevice, content);
                        if (response.StatusCode == System.Net.HttpStatusCode.Created) //201
                        {
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                appSettings.sDeviceID = resJson["id"].ToString();

                                RegistryManager.SetDeviceID();

                                LogWriter.LogWrite("PostDeviceID() GOT Success");
                                return 1;
                            }
                        }
                        else
                        {
                            //Get Device ID Failed
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                string sErrorMessage = resJson["message"].ToString();
                                LogWriter.LogWrite("PostDeviceID() FAILED");
                                return 0;
                            }
                            return 0;
                        }
                    }
                    else
                    {
                        return 0;
                    }
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("PostDeviceID() WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PostDeviceID() Exception - " + ex.Message);
            }
            return 0;
        }

        //FocusModes
        internal async Task<int> AddFocusMode(FocusMode FF) // OK
        {
            LogWriter.LogWrite("AddFocusMode()");
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();
 
                   //Remove # from color
                   string color = FF.color.Replace("#", "");

                   string metadata = $"{{\"blockUrlsArray\":{JsonConvert.SerializeObject(FF.arrBlockedUrls.ToArray<string>())}," +
                   $"\"allowed_apps_win\":{JsonConvert.SerializeObject(FF.arrAllowedAppsWin.ToArray<AppDetails>())}," +
                   $"\"color\":\"{color}\"," +
                   $"\"isDefault\":{JsonConvert.SerializeObject(FF.isDefault)}," +
                   $"\"isMeetings\":{JsonConvert.SerializeObject(FF.isMeetings)}," +
                   $"\"isLockedFocus\":{JsonConvert.SerializeObject(FF.isLockedFocus)}," +
                   $"\"isRelaxFocus\":{JsonConvert.SerializeObject(FF.isRelaxFocus)}," +
                   $"\"isAllowedAllApps\":{JsonConvert.SerializeObject(FF.isAllowedAllApps)}," +
                   $"\"isAllowedListApproach\":{JsonConvert.SerializeObject(FF.isAllowedListApproach)}}}";

                    string addFocusJsonString = $"{{\"id\":\"{FF.id}\"," +
                        $"\"name\":\"{FF.name}\"," +
                        $"\"allowed_urls\":{JsonConvert.SerializeObject(FF.arrAllowedUrls.ToArray<string>())}," +
                        $"\"metadata\":{metadata}}}" ;

                    if (!string.IsNullOrEmpty(addFocusJsonString))
                    {
                        StringContent content = new StringContent(addFocusJsonString, Encoding.UTF8, "application/json");

                        //Make the data for Focus Mode
                        var response = await client.PostAsync(FocusMode, content);
                        if (response.StatusCode == System.Net.HttpStatusCode.Created) //201
                        {
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                LogWriter.LogWrite("AddFocusMode() GOT Success");
                                return 1;
                            }
                        }
                        else
                        {
                            //Get Device ID Failed
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                string sErrorMessage = resJson["message"].ToString();
                                LogWriter.LogWrite("AddFocusMode() FAILED" + sErrorMessage);
                                return 0;
                            }
                            return 0;
                        }
                    }
                    else
                    {
                        return 0;
                    }
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("AddFocusMode WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AddFocusMode Exception - " + ex.Message);
            }
            return 0;
        }

        internal async Task<int> DeleteFocusMode(string sFocusModeId) // 
        {
            LogWriter.LogWrite("DeleteFocusMode() API");
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();
                    
                    string path = FocusMode + "?id=" + sFocusModeId;

                    var response = await client.DeleteAsync(path);
                    if (response.StatusCode == System.Net.HttpStatusCode.NoContent) //204
                    {
                        LogWriter.LogWrite("DeleteFocusMode() GOT Success");
                        return 1;
                    }
                    else
                    {
                        //Get Device ID Failed
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            JObject resJson = JObject.Parse(responseCode);
                            string sErrorMessage = resJson["message"].ToString();
                            LogWriter.LogWrite("DeleteFocusMode() FAILED");
                            return 0;
                        }
                        return 0;
                    }
                    
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("DeleteFocusMode WebException" + ex.Message);
 
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DeleteFocusMode Exception" + ex.Message);
 
            }
            return 0;
        }

        internal async Task<int> UpdateAllFocusModes() // OK
        {
            LogWriter.LogWrite("UpdateAllFocusModes()");
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();

                    string arrAllFocusModesArray = $"[";
 
                    foreach (FocusMode FF in appSettings.arrFocusModes)
                    {
                        //Remove # from color
                        string color = FF.color.Replace("#", "");

                        string metadata = $"{{\"blockUrlsArray\":{JsonConvert.SerializeObject(FF.arrBlockedUrls.ToArray<string>())}," +
                        $"\"allowed_apps_win\":{JsonConvert.SerializeObject(FF.arrAllowedAppsWin.ToArray<AppDetails>())}," +
                        $"\"color\":\"{color}\"," +
                        $"\"isAllowedAllApps\":{JsonConvert.SerializeObject(FF.isAllowedAllApps)}," +
                        $"\"isAllowedListApproach\":{JsonConvert.SerializeObject(FF.isAllowedListApproach)}," +
                        $"\"isDefault\":{JsonConvert.SerializeObject(FF.isDefault)}," +
                        $"\"isMeetings\":{JsonConvert.SerializeObject(FF.isMeetings)}," +
                        $"\"isLockedFocus\":{JsonConvert.SerializeObject(FF.isLockedFocus)}," +
                        $"\"isRelaxFocus\":{JsonConvert.SerializeObject(FF.isRelaxFocus)}," +
                        $"\"isAllowedAllApps\":{JsonConvert.SerializeObject(FF.isAllowedAllApps)}," +
                        $"\"isAllowedListApproach\":{JsonConvert.SerializeObject(FF.isAllowedListApproach)}}}";

                        string addFocusJsonString = $"{{\"id\":\"{FF.id}\"," +
                            $"\"name\":\"{FF.name}\"," +
                            $"\"allowed_urls\":{JsonConvert.SerializeObject(FF.arrAllowedUrls.ToArray<string>())}," +
                            $"\"metadata\":{metadata}}},";

                        arrAllFocusModesArray = arrAllFocusModesArray + addFocusJsonString;
                    }

                    //Remove last comma -  we have 100% added above
                    arrAllFocusModesArray = arrAllFocusModesArray.Remove(arrAllFocusModesArray.Length - 1, 1);

                    arrAllFocusModesArray = arrAllFocusModesArray + "]";

                    if (!string.IsNullOrEmpty(arrAllFocusModesArray))
                    {
                        StringContent content = new StringContent(arrAllFocusModesArray, Encoding.UTF8, "application/json");
                        
                        //Make the data for Focus Mode
                        var response = await client.PutAsync(FocusMode, content);
                        if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                        {
                            if (response.Content != null)
                            {
                                LogWriter.LogWrite("UpdateAllFocusModes() GOT Success");
                                var responseCode = await response.Content.ReadAsStringAsync();
                                //JObject resJson = JObject.Parse(responseCode);
                                
                                return 1;
                            }
                        }
                        else
                        {
                            //Get Device ID Failed
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                string sErrorMessage = resJson["message"].ToString();
                                LogWriter.LogWrite("UpdateAllFocusModes() FAILED" + sErrorMessage);
                                return 0;
                            }
                            return 0;
                        }
                    }
                    else
                    {
                        return 0;
                    }
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("UpdateAllFocusModes WebException - " + ex.Message);
 
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("UpdateAllFocusModes Exception - " + ex.Message);
 
            }
            return 0;
        }

        internal async Task<int> StartFocusMode(string sId) 
        {
            LogWriter.LogWrite("StartFocusMode() API");
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();

                    DateTime start = DateTime.Parse(appSettings.sRunningFocusModeStartTime).ToUniversalTime();
                    DateTime end = DateTime.Parse(appSettings.sRunningFocusModeEndTime).ToUniversalTime();

                    string sTasksArray = (appSettings.sRawTasks != null && appSettings.sRawTasks.Length > 0) ? appSettings.sRawTasks : "[]";

                    string addFocusJsonString = $"{{\"intention\":\"{ appSettings.sRunningFocusModeIntention}\"," +
                        $"\"finish_time\":\"{end.ToString("o")}\"," +
                        $"\"start_time\":\"{start.ToString("o")}\"," +
                        $"\"to_dos\":{sTasksArray}}}";
                    
                    if (!string.IsNullOrEmpty(addFocusJsonString))
                    {
                        StringContent content = new StringContent(addFocusJsonString, Encoding.UTF8, "application/json");

                        //Make the data for Focus Mode
                        string apiPath = string.Format("https://api.focusbear.io/focus-mode/{0}/start", sId);

                        var response = await client.PostAsync(apiPath, content);
                        if (response.StatusCode == System.Net.HttpStatusCode.Created) //201
                        {
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                LogWriter.LogWrite("StartFocusMode() GOT Success");
                                return 1;
                            }
                        }
                        else
                        {
                            
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                string sErrorMessage = resJson["message"].ToString();
                                LogWriter.LogWrite("StartFocusMode() FAILED" + sErrorMessage);
                                return 0;
                            }
                            return 0;
                        }
                    }
                    else
                    {
                        return 0;
                    }
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("StartFocusMode() WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StartFocusMode() Exception - " + ex.Message);
            }
            return 0;
        }

        internal async Task<int> FinishFocusMode(string sId, string achievements, string distractions)
        {
            LogWriter.LogWrite("FinishFocusMode()");
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();
 
                    DateTime end = DateTime.UtcNow;

                    string sTasksArray = (appSettings.sRawTasksDone != null && appSettings.sRawTasksDone.Length > 0) ? appSettings.sRawTasksDone : "[]";

                    string addFocusJsonString = $"{{\"achievements\":\"{achievements}\"," +
                        $"\"distractions\":\"{distractions}\"," +
                        $"\"finish_time\":\"{end.ToString("o")}\"," +
                        $"\"to_dos\":{sTasksArray}}}";


                    if (!string.IsNullOrEmpty(addFocusJsonString))
                    {
                        StringContent content = new StringContent(addFocusJsonString, Encoding.UTF8, "application/json");

                        //Make the data for Focus Mode
                        string apiPath = string.Format("https://api.focusbear.io/focus-mode/{0}/finish", sId);

                        var response = await client.PostAsync(apiPath, content);
                        if (response.StatusCode == System.Net.HttpStatusCode.Created) //201
                        {
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                LogWriter.LogWrite("FinishFocusMode() GOT Success");
                                return 1;
                            }
                        }
                        else
                        {
                            //Get Device ID Failed
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                string sErrorMessage = resJson["message"].ToString();
                                LogWriter.LogWrite("FinishFocusMode() FAILED" + sErrorMessage);
                                return 0;
                            }
                            return 0;
                        }
                    }
                    else
                    {
                        return 0;
                    }
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("FinishFocusMode() WebException -" + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("FinishFocusMode() Exception -" + ex.Message);
            }
            return 0;
        }

        internal async Task<int> GetKeywords()
        {
            LogWriter.LogWrite("GetKeywords()");
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();

                    DateTime end = DateTime.UtcNow;

                    string addFocusJsonString = $"{{\"titles\":{JsonConvert.SerializeObject(appSettings.TemporaryAllowedURLsTabTitles.ToArray())}}}";

                    if (!string.IsNullOrEmpty(addFocusJsonString))
                    {
                        StringContent content = new StringContent(addFocusJsonString, Encoding.UTF8, "application/json");

                        var response = await client.PostAsync("https://api.focusbear.io/tab-keywords", content);
                        if (response.StatusCode == System.Net.HttpStatusCode.Created) //201
                        {
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                appSettings.sRawKeywords = responseCode.ToString();

                                //JObject resJson = JObject.Parse(responseCode);
                                LogWriter.LogWrite("GetKeywords() GOT Success");
                                return 1;
                            }
                        }
                        else
                        {
                            //Get Device ID Failed
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                string sErrorMessage = resJson["message"].ToString();
                                LogWriter.LogWrite("GetKeywords() FAILED" + sErrorMessage);
                                return 0;
                            }
                            return 0;
                        }
                    }
                    else
                    {
                        return 0;
                    }
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("GetKeywords() WebException -" + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetKeywords() Exception -" + ex.Message);
            }
            return 0;
        }

        internal async Task<int> GetUserSettings(string sCustomHabitPackID = "") //OK
        {
            LogWriter.LogWrite("GetUserSettings()");
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();
                   
                    string responsePath = UserSettings;

                    if (appSettings.IsFirstInstall)
                    {
                         responsePath = string.Format("https://api.focusbear.io/habit-packs/default?pack_id={0}", appSettings.sHabitPackIdToInstall);
                    }
                    else if(sCustomHabitPackID.Length > 0)
                    {
                        responsePath = string.Format("https://api.focusbear.io/habit-packs/default?pack_id={0}", sCustomHabitPackID);
                    }
                    else
                    {
                        responsePath = responsePath + "?language=" + appSettings.sLangCode;
                    }
                    var response = await client.GetAsync(responsePath);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            appSettings.sRawUserSettings = responseCode.ToString();
                            appSettings.dSettingsFetchedTime = DateTime.UtcNow;

                            LogWriter.LogWrite("UserSettings - " + appSettings.sRawUserSettings);

                            RegistryManager.SaveUserSettings();
                            return 1;
                        }
                    }
                    else
                    {
                        //GetUserSettings Failed
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            return 0;
                        }
                        return 0;
                    }
                    
                }
                else
                {
                    LogWriter.LogWrite("Is nInternetOn FALSE");
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("GetUserSettings - WebException" + ex.Message);
 
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetUserSettings - Exception" + ex.Message);
 
            }
            return 0;
        }

        internal async Task<int> PutUserSettings(bool is_onboarding = false) //OK
        {
            LogWriter.LogWrite("PutUserSettings()");
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();

                    // dummy payload
                    String jsonString = appSettings.sRawUserSettings;

                    if (!string.IsNullOrEmpty(jsonString))
                    {
                        StringContent content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                        if(is_onboarding)
                            UserSettings += "?is_onboarding=true";

                        if(is_onboarding)
                            UserSettings += "?is_onboarding=true";

                        var response = await client.PutAsync(UserSettings, content);
                        if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                        {
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                if (appSettings.IsFirstInstall)
                                {
                                    appSettings.sRawUserSettings = responseCode.ToString();
                                    RegistryManager.SaveUserSettings();
                                }
                                LogWriter.LogWrite(" PutUserSettings () GOT Success");
                                return 1;
                            }
                        }
                        else
                        {
                            //Get Device ID Failed
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                string sErrorMessage = resJson["message"].ToString();
                                LogWriter.LogWrite("PutUserSettings() FAILED");
                                return 0;
                            }
                            return 0;
                        }
                    }
                    else
                    {
                        return 0;
                    }
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("PutUserSettings WebException - " + ex.Message);

 
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PutUserSettings Exception - " + ex.Message);
 
            }
            return 0;
        }


        internal async Task<int> GetLastActivityInfo() //OK
        {
            LogWriter.LogWrite("GetLastActivityInfo()");
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();

                    var response = await client.GetAsync(CurrentActivityProps);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            appSettings.sLastActivityDoneInfo = responseCode.ToString();

                            LogWriter.LogWrite("GetLastActivityInfo() GOT Success");

                            return 1;
                        }
                    }
                    else
                    {
                        //GetLastActivityInfo Failed
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            JObject resJson = JObject.Parse(responseCode);
                            string sErrorMessage = resJson["message"].ToString();
                            LogWriter.LogWrite("GetLastActivityInfo() FAILED - " + sErrorMessage);
                            return 0;
                        }
                        return 0;
                    }

                }
                else
                {
                    LogWriter.LogWrite("Is nInternetOn FALSE");
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("GetLastActivityInfo WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetLastActivityInfo Exception - " + ex.Message);
            }
            return 0;
        }

        internal async Task<int> GetUserDetailsInfo() //OK
        {
            LogWriter.LogWrite("GetUserDetailsInfo()");
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();

                    var response = await client.GetAsync(GetUserDetails);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();

                            appSettings.sUserDetailsInfo = responseCode.ToString();

                            LogWriter.LogWrite("GetUserDetailsInfo() GOT Success");

                            RegistryManager.SaveUserDetailsInfo();
                            return 1;
                        }
                    }
                    else
                    {
                        //GetUserSettings Failed
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            JObject resJson = JObject.Parse(responseCode);
                            string sErrorMessage = resJson["message"].ToString();
                            LogWriter.LogWrite("GetUserDetailsInfo() FAILED - " + sErrorMessage);
                            return 0;
                        }
                        return 0;
                    }

                }
                else
                {
                    LogWriter.LogWrite("Is nInternetOn FALSE");
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("GetUserDetailsInfo WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetUserDetailsInfo Exception - " + ex.Message);
            }
            return 0;
        }

        internal async Task<int> GetUserLocalDeviceSettingsInfo() //OK
        {
            LogWriter.LogWrite("GetUserLocalDeviceSettingsInfo()");
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();
        
                    var response = await client.GetAsync(GetUserLocalDeviceSettings);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();

                            appSettings.sUserLocalDeviceSettings = responseCode.ToString();

                            LogWriter.LogWrite("GetUserLocalDeviceSettingsInfo() GOT Success");

                            RegistryManager.SaveLocalDeviceSettings();
                            return 1;
                        }
                    }
                    else
                    {
                        //GetUserSettings Failed
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            JObject resJson = JObject.Parse(responseCode);
                            string sErrorMessage = resJson["message"].ToString();
                            LogWriter.LogWrite("GetUserLocalDeviceSettingsInfo() FAILED - " + sErrorMessage);
                            return 0;
                        }
                        return 0;
                    }

                }
                else
                {
                    LogWriter.LogWrite("Is nInternetOn FALSE");
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("GetUserLocalDeviceSettingsInfo WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetUserLocalDeviceSettingsInfo Exception - " + ex.Message);
            }
            return 0;
        }

        internal async Task<int> PutUserLocalDeviceSettingsInfo( JObject SettingsJSON) // OK
        {
            LogWriter.LogWrite("PutUserLocalDeviceSettingsInfo()");
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();

                    string addItemJsonString = SettingsJSON.ToString();

                    if (!string.IsNullOrEmpty(addItemJsonString))
                    {
                        StringContent content = new StringContent(addItemJsonString, Encoding.UTF8, "application/json");

                        var response = await client.PutAsync(GetUserLocalDeviceSettings, content);
                        if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                        {
                            if (response.Content != null)
                            {
                                LogWriter.LogWrite("PutUserLocalDeviceSettingsInfo() GOT Success");
                                var responseCode = await response.Content.ReadAsStringAsync();
                                //JObject resJson = JObject.Parse(responseCode);

                                return 1;
                            }
                        }
                        else
                        {
                            //Get Device ID Failed
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                string sErrorMessage = resJson["message"].ToString();
                                LogWriter.LogWrite("PutUserLocalDeviceSettingsInfo() FAILED - " + sErrorMessage);
                                return 0;
                            }
                            return 0;
                        }
                    }
                    else
                    {
                        return 0;
                    }
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("PutUserLocalDeviceSettingsInfo WebException - " + ex.Message);

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PutUserLocalDeviceSettingsInfo Exception - " + ex.Message);

            }
            return 0;
        }


        //Activity stats for the activity for month
        internal async Task<int> GetActivityStats(string activityId, string activityChoiceId)   //OK
        {
            LogWriter.LogWrite("GetActivityStats()");
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();

                    var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
                    //UTC+05:30 // UTC%2B5:30
                    string sign = offset < TimeSpan.Zero ? "-" : "+";
                    sign = HttpUtility.UrlEncode(sign);
                    string time = "UTC" + sign + offset.ToString(@"hh\:mm");

                    string apiPath = string.Format("https://api.focusbear.io/completed-activity/{0}/stats?days_number=30&timezone={1}", (activityChoiceId.Length > 0 ? activityChoiceId : activityId), time);

                    LogWriter.LogWrite("GetActivityStats() URL - " + apiPath);

                    var response = await client.GetAsync(apiPath);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            appSettings.sOneMonthActivityDataForGraph = responseCode.ToString();

                            LogWriter.LogWrite("GetActivityStats() GOT Success");
                            return 1;
                        }
                    }
                    else
                    {
                        //GetUserSettings Failed
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            JObject resJson = JObject.Parse(responseCode);
                            string sErrorMessage = resJson["message"].ToString();
                            LogWriter.LogWrite("GetActivityStats() FAILED - " + sErrorMessage);
                            return 0;
                        }
                        return 0;
                    }

                }
                else
                {
                    LogWriter.LogWrite("Is nInternetOn FALSE");
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("GetActivityStats WebException + " + ex.Message);
 
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetActivityStats Exception + " + ex.Message);
 
            }
            return 0;
        }

        //mainly used for Break activities where current day stats matter
        internal async Task<int> GetActivityCurrentDayStats(string activityId, string activityChoiceId)   //OK
        {
            LogWriter.LogWrite("GetActivityCurrentDayStats()");
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();

                    TimeSpan tsShutOffTime = TimeSpan.Parse(appSettings.sDayShutOffTime);

                    DateTime now = DateTime.Now;
                    DateTime myShutOffTime = new DateTime(now.Year, now.Month, now.Day, tsShutOffTime.Hours, tsShutOffTime.Minutes, 00).ToUniversalTime();
                    Console.WriteLine("{0}", myShutOffTime.ToString());

                    TimeSpan tsStartTime = TimeSpan.Parse(appSettings.sDayStartTime);
                    DateTime myStartTime = new DateTime(now.Year, now.Month, now.Day, tsStartTime.Hours, tsStartTime.Minutes, 00).ToUniversalTime();
                    Console.WriteLine("{0}", myStartTime.ToString());

                    if(myShutOffTime < myStartTime)
                    {
                        myShutOffTime = myShutOffTime.AddHours(24);
                    }
                    string apiPath = string.Format("https://api.focusbear.io/completed-activity/{0}?from_time={1}&to_time={2}", (activityChoiceId.Length > 0 ? activityChoiceId : activityId) , myStartTime.ToString("o"), myShutOffTime.ToString("o"));

                    LogWriter.LogWrite("GetActivityCurrentDayStats() URL - " + apiPath);

                    var response = await client.GetAsync(apiPath);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            appSettings.sCurrentDayActivityDataForGraph = responseCode.ToString();

                            LogWriter.LogWrite("GetActivityCurrentDayStats() GOT Success");
                            return 1;
                        }
                    }
                    else
                    {
                        //GetUserSettings Failed
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            JObject resJson = JObject.Parse(responseCode);
                            string sErrorMessage = resJson["message"].ToString();
                            LogWriter.LogWrite("GetActivityCurrentDayStats() FAILED - " + sErrorMessage);
                            return 0;
                        }
                        return 0;
                    }

                }
                else
                {
                    LogWriter.LogWrite("Is nInternetOn FALSE");
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("GetActivityCurrentDayStats WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetActivityCurrentDayStats WebException - " + ex.Message);
            }
            return 0;
        }
        //Used for break activities questions wise stats
        internal async Task<int> GetActivityCurrentDayQuestionsStats(string activityId, string activityChoiceId, List<QuantityQuestion> logQuantityQuestions)   //OK
        {
            LogWriter.LogWrite("getActivityCurrentDayQuestionsStats()");
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();

                    TimeSpan tsShutOffTime = TimeSpan.Parse(appSettings.sDayShutOffTime);

                    DateTime now = DateTime.Now;
                    DateTime myShutOffTime = new DateTime(now.Year, now.Month, now.Day, tsShutOffTime.Hours, tsShutOffTime.Minutes, 00).ToUniversalTime();
                    Console.WriteLine("{0}", myShutOffTime.ToString());

                    TimeSpan tsStartTime = TimeSpan.Parse(appSettings.sDayStartTime);
                    DateTime myStartTime = new DateTime(now.Year, now.Month, now.Day, tsStartTime.Hours, tsStartTime.Minutes, 00).ToUniversalTime();
                    Console.WriteLine("{0}", myStartTime.ToString());

                    string apiPath = string.Format("https://api.focusbear.io/completed-activity/answer-logs?from_time={0}&to_time={1}", myStartTime.ToString("o"), myShutOffTime.ToString("o"));

                    LogWriter.LogWrite("GetActivityCurrentDayQuestionsStats() URL - " + apiPath);


                    string addItemJsonString = $"{{";
                    if (logQuantityQuestions != null)
                    {
                        string LogQuantityQuestions = $"[";
                        foreach (QuantityQuestion question in logQuantityQuestions)
                        {
                            LogQuantityQuestions += $"\"{question.id}\",";
                        }
                        LogQuantityQuestions = LogQuantityQuestions.Remove(LogQuantityQuestions.Length - 1, 1);
                        LogQuantityQuestions = LogQuantityQuestions + "]";

                        addItemJsonString = addItemJsonString + $"\"question_ids\":{LogQuantityQuestions}";
                    }
                    addItemJsonString = addItemJsonString + "}";

                    if (!string.IsNullOrEmpty(addItemJsonString))
                    {
                        StringContent content = new StringContent(addItemJsonString, Encoding.UTF8, "application/json");

                        var response = await client.PostAsync(apiPath, content);
                        if (response.StatusCode == System.Net.HttpStatusCode.Created) //200
                        {
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                appSettings.sCurrentDayActivityDataForGraphQuestionsStats = responseCode.ToString();

                                LogWriter.LogWrite("GetActivityCurrentDayQuestionsStats() GOT Success");

                                return 1;
                            }
                        }
                        else
                        {
                            //GetUserSettings Failed
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                string sErrorMessage = resJson["message"].ToString();
                                LogWriter.LogWrite("GetActivityCurrentDayQuestionsStats() FAILED - " + sErrorMessage);
                                return 0;
                            }
                            return 0;
                        }

                    }
                    else
                    {
                        LogWriter.LogWrite("Is nInternetOn FALSE");
                    }
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("GetActivityCurrentDayQuestionsStats WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetActivityCurrentDayQuestionsStats WebException - " + ex.Message);
            }
            return 0;
        }
        internal async Task<int> GetActivityAllDaySummary()   //OK
        {
            LogWriter.LogWrite("GetActivityAllDaySummary()");
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();

                    var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
                    //UTC+05:30 // UTC%2B5:30
                    string sign = offset < TimeSpan.Zero ? "-" : "+";
                    sign = HttpUtility.UrlEncode(sign);
                    string time = "UTC" + sign + offset.ToString(@"hh\:mm");

                    string apiPath = string.Format("https://api.focusbear.io/completed-activity/day-summary?timezone={0}", time);

                    LogWriter.LogWrite("GetActivityAllDaySummary() URL - " + apiPath);
                    
                    var response = await client.GetAsync(apiPath);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();

                            appSettings.sActivityAllDaySummaryData = responseCode.ToString();

                            LogWriter.LogWrite("GetActivityAllDaySummary() GOT Success");
                            return 1;
                        }
                    }
                    else
                    {
                        //GetUserSettings Failed
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            JObject resJson = JObject.Parse(responseCode);
                            string sErrorMessage = resJson["message"].ToString();
                            LogWriter.LogWrite("GetActivityAllDaySummary() FAILED - " + sErrorMessage);
                            return 0;
                        }
                        return 0;
                    }

                }
                else
                {
                    LogWriter.LogWrite("Is nInternetOn FALSE");
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("GetActivityAllDaySummary WebException - " + ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetActivityAllDaySummary Exception -" + ex.Message);
                Debug.WriteLine(ex.Message);
            }
            return 0;
        }


        internal async Task<int> GetHabitPacks() //OK
        {
            LogWriter.LogWrite("GetHabitPacks()");
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    HttpClient client = new HttpClient();
    
                    client.DefaultRequestHeaders.Add("platform", "Windows");
                    client.DefaultRequestHeaders.Add("app-version", appSettings.AppVersion.ToString());

                    string responsePath = "";

                    if (appSettings.bGoalHealthyHabits)
                    {
                        responsePath = "https://api.focusbear.io/habit-packs?featured_for_onboarding=true&breaks_only=false";
                    }
                    else
                    {
                        responsePath = "https://api.focusbear.io/habit-packs?featured_for_onboarding=true&breaks_only=true";
                    }

                    var response = await client.GetAsync(responsePath);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            appSettings.sRawHabitPacks = responseCode.ToString();

                            LogWriter.LogWrite("habit-packs - " + appSettings.sRawHabitPacks);
                            return 1;
                        }
                    }
                    else
                    {
                        //GetUserSettings Failed
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            return 0;
                        }
                        return 0;
                    }

                }
                else
                {
                    LogWriter.LogWrite("Is nInternetOn FALSE");
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("GetHabitPacks - WebException" + ex.Message);

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetHabitPacks - Exception" + ex.Message);

            }
            return 0;
        }

        internal async Task<int> GetFocusTemplates() //OK
        {
            LogWriter.LogWrite("GetFocusTemplates()");
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();

                    string apiPath = string.Format("https://api.focusbear.io/focus-mode-template?featured_for_onboarding=true&language={0}", appSettings.sLangCode);

                    var response = await client.GetAsync(apiPath);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();

                            appSettings.sFocusModeTemplates = responseCode.ToString();

                            LogWriter.LogWrite("GetFocusTemplates() GOT Success");

                            //RegistryManager.SaveUserSettings();
                            return 1;
                        }
                    }
                    else
                    {
                        //GetUserSettings Failed
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            JObject resJson = JObject.Parse(responseCode);
                            string sErrorMessage = resJson["message"].ToString();
                            LogWriter.LogWrite("GetFocusTemplates() FAILED - " + sErrorMessage);
                            return 0;
                        }
                        return 0;
                    }

                }
                else
                {
                    LogWriter.LogWrite("Is nInternetOn FALSE");
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("GetFocusTemplates WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetFocusTemplates Exception - " + ex.Message);
            }
            return 0;
        }


        internal async Task<int> CapturePostHog(string sEvent, string sMetadata) // OK
        {
            LogWriter.LogWrite("CapturePostHog() - " + sEvent);
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    HttpClient client = new HttpClient();

                    string sOSVersion = appSettings.sOSVersion;
                    bool bUsesWindowsApp = true;
                    string sID = (appSettings.sUser_id != null && appSettings.sUser_id.Length > 0) ? appSettings.sUser_id : appSettings.sMacAddress;
                    string context = $"{{\"os\":\"windows\"," +
                                     $"\"osVersion\":\"{sOSVersion}\"," +
                                     $"\"appVersion\":\"{appSettings.AppVersion.ToString()}\"}}";
                 

                    string properties = $"{{\"distinct_id\":\"{sID}\"," +
                    $"\"USES_WINDOWS_APP\":{JsonConvert.SerializeObject(bUsesWindowsApp)}";

                    if (sMetadata.Length > 0)
                    {
                        properties += ",";
                        properties += sMetadata;
                    }
                    properties += "}";

                    string sJsonString = $"{{\"api_key\":\"phc_Y6hD2dvO1zylm7CLij5CuNt6S5gTxe7EVXoxZcb2xUm\"," +
                        $"\"event\":\"{sEvent}\"," +
                        $"\"context\":{context}," + 
                        $"\"properties\":{properties}}}";


                    if (!string.IsNullOrEmpty(sJsonString))
                    {
                         StringContent content = new StringContent(sJsonString, Encoding.UTF8, "application/json");

                        //Make the data for Focus Mode
                        var response = await client.PostAsync("https://app.posthog.com/capture/", content);
                        if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                        {
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                return 1;
                            }
                        }
                        else
                        {
                            //Get Device ID Failed
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                string sErrorMessage = resJson["message"].ToString();
                                LogWriter.LogWrite("CapturePostHog() FAILED" + sErrorMessage);
                                return 0;
                            }
                            return 0;
                        }
                    }
                    else
                    {
                        return 0;
                    }
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("CapturePostHog WebException - " + ex.Message);

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("CapturePostHog Exception - " + ex.Message);

            }
            return 0;
        }

        internal async Task<int> CaptureEventEndpoint(string sEvent, string sMetadata) // OK
        {
            LogWriter.LogWrite("CaptureEventEndpoint() - " + sEvent);
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();

                    string sOSVersion = appSettings.sOSVersion;
                    bool bUsesWindowsApp = true;
                    string sID = (appSettings.sUser_id != null && appSettings.sUser_id.Length > 0) ? appSettings.sUser_id : appSettings.sMacAddress;

                    string event_data = "{\"data\":{";
                    if (sMetadata.Length > 0)
                    {
                        event_data += sMetadata;
                    }
                    event_data += "}}";

                    string properties = $"{{\"distinct_id\":\"{sID}\"," +
                         $"\"USERNAME\":\"{appSettings.sUser_name}\"," +
                         $"\"APPVERSION\":\"{appSettings.AppVersion.ToString()}\","+
                         $"\"USES_WINDOWS_APP\":{JsonConvert.SerializeObject(bUsesWindowsApp)}}}";


                    string sJsonString = $"{{\"event_type\":\"{sEvent}\"," +
                        $"\"event_data\":{event_data}," +
                        $"\"user_properties\":{properties}}}";

                    if (!string.IsNullOrEmpty(sJsonString))
                    {
                        StringContent content = new StringContent(sJsonString, Encoding.UTF8, "application/json");

                        //Make the data for Focus Mode
                        var response = await client.PostAsync("https://api.focusbear.io/events", content);
                        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) //204
                        {
                            if (response.Content != null)
                            {
                                return 1;
                            }
                        }
                        else
                        {
                            //Get Device ID Failed
                            if (response.Content != null)
                            {
                                LogWriter.LogWrite("CaptureEventEndpoint() FAILED" );
                                return 0;
                            }
                            return 0;
                        }
                    }
                    else
                    {
                        return 0;
                    }
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("CaptureEventEndpoint WebException - " + ex.Message);

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("CaptureEventEndpoint Exception - " + ex.Message);

            }
            return 0;
        }

        internal async Task<int> ChatGPTIsURLSafe(string url, string tab_title, string meta_description, string focus_mode, string intention, string language)
        {
            LogWriter.LogWrite("ChatGPTIsURLSafe() = " + url);
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();

                    string lang = "english";
                    if (language == "es")
                        lang = "spanish";

                    string addItemJsonString = $"{{\"url\":\"{url}\"," +
                        $"\"tab_title\":\"{tab_title}\"," +
                        $"\"meta_description\":\"{meta_description}\"," +
                        $"\"focus_mode\":\"{focus_mode}\"," +
                        $"\"intention\":\"{intention}\"," +
                        $"\"language\":\"{lang}\"}}";


                    if (!string.IsNullOrEmpty(addItemJsonString))
                    {
                        StringContent content = new StringContent(addItemJsonString, Encoding.UTF8, "application/json");
                        
                        appSettings.sChatGPTPayLoad = addItemJsonString;

                        client.Timeout = TimeSpan.FromSeconds(5); // 5 seconds time out

                        var response = await client.PostAsync("https://api.focusbear.io/user/is-url-safe-to-use", content);
                        if (response.StatusCode == System.Net.HttpStatusCode.Created) //201
                        {
                            LogWriter.LogWrite("ChatGPTIsURLSafe() SUCCESS");
                            LogWriter.LogWrite("response = " + response.ToString());
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                if (responseCode == "")
                                {
                                    LogWriter.LogWrite("responseCode = null" );
                                    
                                    return -1;
                                }
                                
                                appSettings.sChatGPTResponse = responseCode;

                                JObject resJson = JObject.Parse(responseCode);

                                if (resJson.ContainsKey("reason"))
                                {
                                    appSettings.sChatGPTReasonForCurrentURL = resJson["reason"].ToString();
                                }

                                if (resJson.ContainsKey("allowed_probability"))
                                {
                                    double dProbability = double.Parse( resJson["allowed_probability"].ToString());
                                    appSettings.dProbablity = dProbability;

                                    if (dProbability >= 0.80)
                                    {
                                        return 1; // Safe URL
                                    }
                                    else if(dProbability >= 0.50 && dProbability < 0.80)
                                    {
                                        //Ask user
                                        return 2;//we will check with "2" if we need to ask user
                                    }
                                    else
                                    {
                                        return 0; //Unsafe url
                                    }
                                }
                               
                                return 1;
                            }
                        }
                        else
                        {
                            //  Failed
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                string sErrorMessage = resJson["message"].ToString();
                                LogWriter.LogWrite("ChatGPTIsURLSafe() FAILED - " + sErrorMessage);
                                return -1;
                            }
                            return -1;
                        }
                    }
                    else
                    {
                        return -1;
                    }
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("ChatGPTIsURLSafe WebException" + ex.Message);

                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ChatGPTIsURLSafe Exception" + ex.Message);
                Debug.WriteLine(ex.Message);
            }
            return -1;
        }

        internal async Task<int> PostAppLogs(string sFeedbackMessage = "")
        {
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();

                    var filePath = Path.Combine(AppSettings.AppDataDirLogsPath, "Debug_Logs.txt");

                    using (var multipartFormContent = new MultipartFormDataContent())
                    {
                        //Load the file and set the file's Content-Type header
                        var fileStreamContent = new StreamContent(File.OpenRead(filePath));
                        fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

                        //Add the file
                        multipartFormContent.Add(fileStreamContent, name: "file", fileName: "Debug_Logs.txt");

                        string sPath = string.Format("https://api.focusbear.io/app-logs?app_platform=Windows&app_version={0}&feedback_message={1}", appSettings.AppVersion.ToString(), HttpUtility.UrlEncode(sFeedbackMessage)); 
                        var response = await client.PostAsync(sPath, multipartFormContent);
                        if (response.StatusCode == System.Net.HttpStatusCode.Created) //201
                        {
                            LogWriter.LogWrite("PostAppLogs() SUCCESS");

                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                return 1;
                            }
                        }
                        else
                        {
                            //Get Device ID Failed
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                string sErrorMessage = resJson["message"].ToString();
                                LogWriter.LogWrite("PostAppLogs() FAILED - " + sErrorMessage);
                                return 0;
                            }
                            return 0;
                        }

                    }
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("PostAppLogs WebException" + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PostAppLogs Exception" + ex.Message);
            }
            return 0;
        }

        internal async Task<int> GetSubscriptionInfo() //OK
        {
            LogWriter.LogWrite("GetSubscriptionInfo()");
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();

                    var response = await client.GetAsync(SubscriptionInfo);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            appSettings.sSubscriptionInfo = responseCode.ToString();

                            LogWriter.LogWrite("GetSubscriptionInfo() GOT Success");

                            return 1;
                        }
                    }
                    else if(response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        //User seems to be deleted
                        return 404;
                    }
                    else
                    {
                        
                        //GetLastActivityInfo Failed
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            JObject resJson = JObject.Parse(responseCode);
                            string sErrorMessage = resJson["message"].ToString();
                            LogWriter.LogWrite("GetSubscriptionInfo() FAILED - " + sErrorMessage);
                            return 0;
                        }
                        return 0;
                    }

                }
                else
                {
                    LogWriter.LogWrite("Is nInternetOn FALSE");
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("GetSubscriptionInfo WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetSubscriptionInfo Exception - " + ex.Message);
            }
            return 0;
        }

        internal async Task<int> RecordUserConsent() // OK
        {
            LogWriter.LogWrite("RecordUserConsent()");
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();

                    bool bConsentStatus = true;
                    string addItemJsonString = $"{{\"consent_type\":\"terms_of_service\"," +
                           $"\"consent_status\":\"{JsonConvert.SerializeObject(bConsentStatus)}\"," +
                           $"\"metadata\":{{\"policy_version\":\"1.0.0\"}}}}";


                    if (!string.IsNullOrEmpty(addItemJsonString))
                    {
                        StringContent content = new StringContent(addItemJsonString, Encoding.UTF8, "application/json");

                        //Make the data for Focus Mode
                        var response = await client.PutAsync("https://api.focusbear.io/user/consent", content);
                        if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                        {
                            if (response.Content != null)
                            {
                                LogWriter.LogWrite("RecordUserConsent() GOT Success");
                                var responseCode = await response.Content.ReadAsStringAsync();
                                return 1;
                            }
                        }
                        else
                        {
                            //Get Device ID Failed
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                string sErrorMessage = resJson["message"].ToString();
                                LogWriter.LogWrite("RecordUserConsent() FAILED" + sErrorMessage);
                                return 0;
                            }
                            return 0;
                        }
                    }
                    else
                    {
                        return 0;
                    }
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("RecordUserConsent WebException - " + ex.Message);

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("RecordUserConsent Exception - " + ex.Message);

            }
            return 0;
        }

        internal async Task<int> GetSavedWebsite() //OK
        {
            LogWriter.LogWrite("GetSavedWebsite()");
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();
                    
                    string apiPath = "https://api.focusbear.io/saved-website";

                    var response = await client.GetAsync(apiPath);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();

                            appSettings.sSavedURLSForRelaxActivity = responseCode.ToString();

                            LogWriter.LogWrite("GetSavedWebsite() GOT Success");
                            return 1;
                        }
                    }
                    else
                    {
                        //GetSavedWebsite Failed
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            JObject resJson = JObject.Parse(responseCode);
                            string sErrorMessage = resJson["message"].ToString();
                            LogWriter.LogWrite("GetSavedWebsite() FAILED - " + sErrorMessage);
                            return 0;
                        }
                        return 0;
                    }

                }
                else
                {
                    LogWriter.LogWrite("Is nInternetOn FALSE");
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("GetSavedWebsite WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetSavedWebsite Exception - " + ex.Message);
            }
            return 0;
        }
        internal async Task<int> DeleteSavedSite(string sWebsiteId) // ok
        {
            LogWriter.LogWrite("DeleteSavedSite() API");
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();
                   
                    string apiPath = "https://api.focusbear.io/saved-website?website_id=" + sWebsiteId;

                    var response = await client.DeleteAsync(apiPath);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        var responseCode = await response.Content.ReadAsStringAsync();
                        LogWriter.LogWrite("DeleteSavedSite() GOT Success");
                        return 1;
                    }
                    else
                    {
                        //Get Device ID Failed
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                           // JObject resJson = JObject.Parse(responseCode);
                           // string sErrorMessage = resJson["message"].ToString();
                            LogWriter.LogWrite("DeleteSavedSite() FAILED");
                            return 0;
                        }
                        return 0;
                    }

                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("DeleteSavedSite WebException" + ex.Message);

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DeleteSavedSite Exception" + ex.Message);

            }
            return 0;
        }

        internal async Task<int> PostSavedWebsite(string sUrl, string sTitle = "", string sMetadata = "")
        {
            LogWriter.LogWrite("PostSavedWebsite() = " + sUrl);
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();

                    string addItemJsonString = $"[{{\"url\":\"{sUrl}\"," +
                        $"\"title\":\"{sTitle}\"," +
                        $"\"metadata\":\"{sMetadata}\"}}]"; //accepts an array

                    if (!string.IsNullOrEmpty(addItemJsonString))
                    {
                        StringContent content = new StringContent(addItemJsonString, Encoding.UTF8, "application/json");

                        string apiPath = "https://api.focusbear.io/saved-website";
                        var response = await client.PostAsync(apiPath, content);
                        if (response.StatusCode == System.Net.HttpStatusCode.Created) //201
                        {
                            LogWriter.LogWrite("PostSavedWebsite() SUCCESS");

                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);

                                return 1;
                            }
                        }
                        else
                        {
                            //PostSavedWebsite Failed
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                string sErrorMessage = resJson["message"].ToString();
                                LogWriter.LogWrite("PostSavedWebsite() FAILED - " + sErrorMessage);
                                return 0;
                            }
                            return 0;
                        }
                    }
                    else
                    {
                        return 0;
                    }
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("PostSavedWebsite WebException" + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PostSavedWebsite Exception" + ex.Message);
            }
            return 0;
        }

        internal async Task<int> SyncOfflineData(string addItemJsonString)
        {
            LogWriter.LogWrite("SyncOfflineData()");
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        var xRenwTokenStatus = await RenewToken();
                        if (xRenwTokenStatus == 0)
                        {
                            return 0;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();

                    if (!string.IsNullOrEmpty(addItemJsonString))
                    {
                        LogWriter.LogWrite(addItemJsonString);

                        StringContent content = new StringContent(addItemJsonString, Encoding.UTF8, "application/json");

                        var response = await client.PostAsync("https://api.focusbear.io/completed-activity/sync", content);
                        if (response.StatusCode == System.Net.HttpStatusCode.Created) //201
                        {
                            LogWriter.LogWrite("SyncOfflineData() SUCCESS");

                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                return 1;
                            }
                        }
                        else
                        {
                            LogWriter.LogWrite("SyncOfflineData() FAILED code = " + response.StatusCode.ToString());

                            if (response.StatusCode != System.Net.HttpStatusCode.RequestTimeout && ( response.StatusCode >= System.Net.HttpStatusCode.BadRequest && response.StatusCode <= System.Net.HttpStatusCode.UpgradeRequired )) //400 to 426 except 408
                            {
                                //Deon asked that if the request fails with a 400 error, we should not try again
                                //So we will return 1 here so that the entries are removed from the offline csv file
                                return 1;
                            }
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                return 0;
                            }
                            return 0;
                        }
                    }
                    else
                    {
                        return 0;
                    }
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("SyncOfflineData WebException" + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SyncOfflineData Exception" + ex.Message);
            }
            return 0;
        }

    }

}
 
