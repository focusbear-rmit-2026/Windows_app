
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
    public static class HttpClientExtensions
    {
        public static Task<HttpResponseMessage> PatchAsync(this HttpClient client, string requestUri, HttpContent content)
        {
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), requestUri)
            {
                Content = content
            };
            return client.SendAsync(request);
        }
    }
    partial class FocusBearAPIClient
    {

        AppSettings appSettings = AppSettings.GetInstance;

        public static string PostDevice { get { return "https://api.focusbear.io/device"; } }
        public string UserSettings { get { return "https://api.focusbear.io/user-settings"; } set { } }

        public static string CompletedActivity { get { return "https://api.focusbear.io/completed-activity"; } }
        public static string SkipActivity { get { return "https://api.focusbear.io/completed-activity/skip"; } }
        public static string CurrentActivityProps { get { return "https://api.focusbear.io/user/details/current-activity-props"; } }

        public static string GetUserDetails { get { return "https://api.focusbear.io/user/details/summary"; } }

        public static string FocusMode { get { return "https://api.focusbear.io/focus-mode"; } }

        public static string FocusModes { get { return "https://api.focusbear.io/user/focus-modes"; } }

        public static string GetUserLocalDeviceSettings { get { return "https://api.focusbear.io/user-local-device-settings"; } }

        public static string SubscriptionInfo { get { return "https://api.focusbear.io/user/subscription"; } }

        // Teacher Mode (issue #1343): the teacher's classes, and the endpoint to start a class focus session.
        public static string ClassManagementClasses { get { return "https://api.focusbear.io/class-management/classes"; } }
        public static string ClassManagementSessions { get { return "https://api.focusbear.io/class-management/sessions"; } }

        public static string UploadLogsURL { get { return "https://api.focusbear.io/app-logs/upload-logs-url?filename=Debug_Logs.txt.zip&mimetype=application/zip"; } }
        public static string NotifyUploadLogsSuccessURL { get { return "https://api.focusbear.io/app-logs/notify-upload-success"; } }
        
        public static string BlockingSchedules { get { return "https://api.focusbear.io/blocking-schedules"; } }

        public static string FocusMusicTracks { get { return "https://api.focusbear.io/tracks"; } }

        public static string AccountabilityBuddies { get { return "https://api.focusbear.io/accountability-buddy"; } }

        public static string DeleteAccountUrl { get { return "https://api.focusbear.io/user-data"; } }

        public static string OccupationSpecificRelevantSites { get { return "https://api.focusbear.io/user/generate-occupation-specific-relevant-sites"; } }

        public static string AllTasks { get { return "https://api.focusbear.io/to-do"; } }

        public static string ClassSession { get { return "https://api.focusbear.io/class-session"; } }

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
            client.DefaultRequestHeaders.Add("device-id", appSettings.sDeviceID);
            client.DefaultRequestHeaders.Add("device_id", appSettings.sDeviceID);

            if (!string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appSettings.userAccount.accessToken);
            }

            return client;
        }

        // One probe shared by every caller, and its answer reused briefly (FocusBear.Core.ReachabilityCache): each API
        // call used to enumerate the network adapters on its own thread and send its own GET /api on a new HttpClient.
        private static readonly FocusBear.Core.ReachabilityCache _reachability = new FocusBear.Core.ReachabilityCache();
        private static readonly HttpClient _probeClient = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
        private static readonly object _probeLock = new object();
        private static Task<bool> _probeInFlight;

        internal Task<bool> IsInternetOn()
        {
            bool? cached = _reachability.Cached(DateTime.UtcNow);
            if (cached.HasValue) return Task.FromResult(cached.Value);
            lock (_probeLock)
            {
                if (_probeInFlight == null || _probeInFlight.IsCompleted)
                    _probeInFlight = Task.Run(ProbeAsync);
                return _probeInFlight;
            }
        }

        private static async Task<bool> ProbeAsync()
        {
            bool ok = false;
            try
            {
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    using (var response = await _probeClient.GetAsync("https://api.focusbear.io/api").ConfigureAwait(false))
                        ok = response.StatusCode == System.Net.HttpStatusCode.OK; //200
                }
            }
            catch (HttpRequestException e)
            {
                LogWriter.LogWrite("Internet Exception Caught! - " + e.Message);
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("IsInternetOn WebException" + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("IsInternetOn - Exception" + ex.Message);
            }
            _reachability.Record(ok, DateTime.UtcNow);
            return ok;
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

                    if (is_skipped) //if activity is skipped just add one second to finish time
                    {
                        finish_time = finish_time.AddSeconds(1);
                    }
                    else
                    {
                        finish_time = finish_time.AddSeconds(duration_logged);
                    }

                    //Built as JSON: an empty question list (every break activity without questions) used to
                    //come out as "log_quantity_answers":] and the server refused the completion.
                    string addItemJsonString = ActivityCompletedBody.Build(activity_id, duration_logged, appSettings.sDeviceID,
                        activity_sequence_id, start_time, finish_time, is_skipped, choice_id, logQuantityQuestions);
                    // E2E build only: the tests read the logged answers (log quantity) from here.
                    if (Focus_Bear.Utils.E2EBuild.IsActive) LogWriter.LogWrite("E2E activity completed body: " + addItemJsonString);

                    

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

            //An empty list is stored as nothing (it used to be "]", which failed every later offline batch).
            string LogQuantityAnswers = ActivityCompletedBody.OfflineAnswers(logQuantityQuestions);

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

        // Undo a force-complete/cancel of a routine sequence ("Uncancel my routine"). The backend then serves
        // that routine's habits again via current-activity-props. Mirrors ForceCompleteCurrentSequence; the
        // macOS app hits the same POST .../uncomplete endpoint (expects 201 Created). Returns 1 on success.
        internal async Task<int> UncompleteCurrentSequence(string activity_sequence_id)
        {
            LogWriter.LogWrite("UncompleteCurrentSequence() + " + activity_sequence_id);
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

                    String jsonString = JsonConvert.SerializeObject(
                        new
                        {
                            operating_system = "Windows",
                            metadata = new { }
                        }
                    );

                    StringContent content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                    string apiPath = string.Format("https://api.focusbear.io/completed-activity-sequence/{0}/uncomplete", activity_sequence_id);

                    var response = await client.PostAsync(apiPath, content);
                    if (response.StatusCode == System.Net.HttpStatusCode.Created) //201
                    {
                        LogWriter.LogWrite("UncompleteCurrentSequence() SUCCESS");
                        return 1;
                    }
                    else
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            LogWriter.LogWrite("UncompleteCurrentSequence() FAILED - " + responseCode);
                        }
                        return 0;
                    }
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("UncompleteCurrentSequence WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("UncompleteCurrentSequence Exception - " + ex.Message);
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
 
                    string addFocusJsonString = FocusModeMetadata.RequestBody(FF).ToString(Formatting.None);

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
                                LogWriter.LogWrite("AddFocusMode() FAILED - " + sErrorMessage);
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
                    // 404: another device deleted it already - gone is what was asked for.
                    if (response.StatusCode == System.Net.HttpStatusCode.NoContent || response.StatusCode == System.Net.HttpStatusCode.NotFound) //204
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

                    string arrAllFocusModesArray = FocusModeMetadata.BulkUpdateBody(appSettings.arrFocusModes);

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
                                // The error body is not always JSON ("Bad Request"): log it as it came.
                                var responseCode = await response.Content.ReadAsStringAsync();
                                LogWriter.LogWrite("UpdateAllFocusModes() FAILED - HTTP " + (int)response.StatusCode + " " + responseCode);
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

        /// <summary>
        /// The accountability buddy's temporary focus mode exists only here and the server rejects its id, so its
        /// session is started, extended and finished against the user's default focus mode, as on the Mac.
        /// </summary>
        private string BackendFocusModeId(string sId)
        {
            var modes = appSettings.arrFocusModes;
            string fallback = modes?.FirstOrDefault(m => m.isDefault && !FocusBear.Core.Postpone.PostponeFocusSession.IsPostponeMode(m.id))?.id
                ?? modes?.FirstOrDefault(m => !FocusBear.Core.Postpone.PostponeFocusSession.IsPostponeMode(m.id))?.id;
            return FocusBear.Core.Postpone.FocusSessionSync.BackendFocusModeId(sId, fallback);
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

                    // The intention can be a meeting title (Late No More): quote it as JSON, or a quote in the title breaks the body.
                    string addFocusJsonString = $"{{\"intention\":{Newtonsoft.Json.JsonConvert.ToString(appSettings.sRunningFocusModeIntention ?? "")}," +
                        $"\"finish_time\":\"{end.ToString("o")}\"," +
                        $"\"start_time\":\"{start.ToString("o")}\"," +
                        $"\"to_dos\":{sTasksArray}}}";
                    
                    if (!string.IsNullOrEmpty(addFocusJsonString))
                    {
                        StringContent content = new StringContent(addFocusJsonString, Encoding.UTF8, "application/json");

                        //Make the data for Focus Mode
                        string apiPath = string.Format("https://api.focusbear.io/focus-mode/{0}/start", BackendFocusModeId(sId));

                        LogWriter.LogWrite("StartFocusMode() JSON: " + FocusBear.Core.Reports.LogSafe.Keys(addFocusJsonString));

                        var response = await client.PostAsync(apiPath, content);
                        if (response.StatusCode == System.Net.HttpStatusCode.Created) //201
                        {
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                LogWriter.LogWrite("StartFocusMode() GOT Success");
                                appSettings.ServerFocusSession.PersonalStarted();
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

        // Teacher Mode (issue #1343): fetch the teacher's classes. This endpoint is guarded by the
        // team_admin entitlement, so a teacher gets their class list and a non-teacher gets an empty
        // list or a non-2xx response. Sets appSettings.bIsTeacher and remembers a class to start
        // sessions for. Returns 1 on a successful call (regardless of teacher/not), 0 on failure.
        internal async Task<int> GetClassManagementClasses()
        {
            LogWriter.LogWrite("GetClassManagementClasses()");
            try
            {
                bool nInternetOn = await IsInternetOn();
                if (!nInternetOn)
                    return 0;

                if (!appSettings.ValidateTokenValidBeforeAPICall())
                {
                    var xRenwTokenStatus = await RenewToken();
                    if (xRenwTokenStatus == 0)
                        return 0;
                }

                HttpClient client = GetClientWithHeaders();
                var response = await client.GetAsync(ClassManagementClasses);

                if (response.StatusCode == System.Net.HttpStatusCode.OK && response.Content != null)
                {
                    string body = await response.Content.ReadAsStringAsync();

                    JArray classes = null;
                    try
                    {
                        JToken token = JToken.Parse(body);
                        if (token is JArray arr)
                            classes = arr;
                        else if (token is JObject obj)
                            classes = (obj["classes"] as JArray) ?? (obj["data"] as JArray);
                    }
                    catch (Exception exParse)
                    {
                        LogWriter.LogWrite("GetClassManagementClasses() parse ex - " + exParse.Message);
                    }

                    if (classes != null && classes.Count > 0)
                    {
                        appSettings.bIsTeacher = true;

                        // Keep the full list so the Focus screen can offer a class picker.
                        List<TeacherClass> parsed = new List<TeacherClass>();
                        foreach (JToken c in classes)
                        {
                            string tid = (c["team_id"] ?? c["teamId"] ?? c["id"] ?? "").ToString();
                            string nm = (c["name"] ?? c["class_name"] ?? "").ToString();
                            if (!string.IsNullOrWhiteSpace(tid))
                                parsed.Add(new TeacherClass { TeamId = tid, Name = string.IsNullOrWhiteSpace(nm) ? tid : nm });
                        }
                        appSettings.arrTeacherClasses = parsed;

                        // Prefer a previously-remembered class if it's still in the list, otherwise the first.
                        TeacherClass chosen = parsed.FirstOrDefault(tc => tc.TeamId == appSettings.sTeacherClassTeamId)
                                              ?? parsed.FirstOrDefault();
                        if (chosen != null)
                        {
                            appSettings.sTeacherClassTeamId = chosen.TeamId;
                            appSettings.sTeacherClassName = chosen.Name;
                        }
                        LogWriter.LogWrite($"GetClassManagementClasses() - teacher with {parsed.Count} class(es); using team_id {appSettings.sTeacherClassTeamId} ({appSettings.sTeacherClassName})");
                    }
                    else
                    {
                        appSettings.bIsTeacher = false;
                        appSettings.arrTeacherClasses = new List<TeacherClass>();
                        LogWriter.LogWrite("GetClassManagementClasses() - no classes; not a teacher");
                    }
                    return 1;
                }
                else
                {
                    // Non-OK (e.g. 403 when the user lacks the team_admin entitlement) => not a teacher.
                    appSettings.bIsTeacher = false;
                    LogWriter.LogWrite("GetClassManagementClasses() - non-OK response " + (int)response.StatusCode + "; treating as not a teacher");
                    return 0;
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("GetClassManagementClasses() WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetClassManagementClasses() Exception - " + ex.Message);
            }
            return 0;
        }

        // Teacher Mode (issue #1343): start a class (teacher) focus session for the remembered class,
        // reusing the last focus mode's allow-list and the Focus button's duration (scheduled finish).
        internal async Task<int> StartClassSession(List<string> allowedApps, List<string> allowedUrls, string sessionName)
        {
            LogWriter.LogWrite("StartClassSession() API");
            try
            {
                bool nInternetOn = await IsInternetOn();
                if (!nInternetOn)
                    return 0;

                if (string.IsNullOrWhiteSpace(appSettings.sTeacherClassTeamId))
                {
                    LogWriter.LogWrite("StartClassSession() - no class team_id known; cannot start");
                    return 0;
                }

                if (!appSettings.ValidateTokenValidBeforeAPICall())
                {
                    var xRenwTokenStatus = await RenewToken();
                    if (xRenwTokenStatus == 0)
                        return 0;
                }

                HttpClient client = GetClientWithHeaders();

                DateTime end = DateTime.Parse(appSettings.sRunningFocusModeEndTime).ToUniversalTime();

                JArray appsArray = new JArray();
                if (allowedApps != null)
                    foreach (string a in allowedApps)
                        if (!string.IsNullOrWhiteSpace(a)) appsArray.Add(a);

                JArray urlsArray = new JArray();
                if (allowedUrls != null)
                    foreach (string u in allowedUrls)
                        if (!string.IsNullOrWhiteSpace(u)) urlsArray.Add(u);

                JObject payload = new JObject
                {
                    ["team_id"] = appSettings.sTeacherClassTeamId,
                    ["name"] = string.IsNullOrWhiteSpace(sessionName) ? "Class focus session" : sessionName,
                    ["allowed_apps"] = appsArray,
                    ["allowed_urls"] = urlsArray,
                    ["block_level"] = "super-strict",   // backend BlockLevel enum value is 'super-strict' (hyphen)
                    ["scheduled_finish_time"] = end.ToString("o")
                };

                StringContent content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
                LogWriter.LogWrite("StartClassSession() JSON: = " + payload.ToString());

                var response = await client.PostAsync(ClassManagementSessions, content);
                if (response.StatusCode == System.Net.HttpStatusCode.Created || response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    appSettings.sLastClassSessionError = "";
                    // Remember the new session's id so it can be ended early when the teacher stops focus.
                    appSettings.sRunningClassSessionId = "";
                    if (response.Content != null)
                    {
                        try
                        {
                            string ok = await response.Content.ReadAsStringAsync();
                            JObject resJson = JObject.Parse(ok);
                            appSettings.sRunningClassSessionId = (resJson["id"] ?? resJson["session_id"] ?? resJson["sessionId"] ?? "").ToString();
                            appSettings.ServerFocusSession.ClassSessionStarted();
                        }
                        catch (Exception exId)
                        {
                            LogWriter.LogWrite("StartClassSession() - could not read session id: " + exId.Message);
                        }
                    }
                    LogWriter.LogWrite("StartClassSession() GOT Success; session id " + appSettings.sRunningClassSessionId);
                    return 1;
                }
                else
                {
                    string body = response.Content != null ? await response.Content.ReadAsStringAsync() : "";
                    appSettings.sLastClassSessionError = FriendlyClassSessionError((int)response.StatusCode, body);
                    LogWriter.LogWrite("StartClassSession() FAILED (" + (int)response.StatusCode + " " + response.ReasonPhrase + ") body=" + body);
                    return 0;
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("StartClassSession() WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StartClassSession() Exception - " + ex.Message);
            }
            return 0;
        }

        /// <summary>Turn a class-session start failure into a short message to show the teacher.</summary>
        private static string FriendlyClassSessionError(int status, string body)
        {
            switch (status)
            {
                case 403:
                    // The backend returns a readable reason (e.g. the allowed-hours window) as plain text.
                    return string.IsNullOrWhiteSpace(body) ? "You can't start a class session right now." : body;
                case 409:
                    return "This class already has a live session running. End it before starting a new one.";
                default:
                    return "Couldn't start the class session. Please try again.";
            }
        }

        // Teacher Mode (issue #1343): end the current class focus session early (when the teacher stops
        // their focus session before the scheduled finish). NOTE: the end endpoint isn't given in the
        // issue's contract; POST /class-management/sessions/{id}/end is the assumed shape and may need a
        // small backend confirmation. Best-effort - failures are logged, not surfaced.
        internal async Task<int> EndClassSession(string sessionId)
        {
            LogWriter.LogWrite("EndClassSession() - " + sessionId);
            try
            {
                if (string.IsNullOrWhiteSpace(sessionId))
                    return 0;

                bool nInternetOn = await IsInternetOn();
                if (!nInternetOn)
                    return 0;

                if (!appSettings.ValidateTokenValidBeforeAPICall())
                {
                    var xRenwTokenStatus = await RenewToken();
                    if (xRenwTokenStatus == 0)
                        return 0;
                }

                HttpClient client = GetClientWithHeaders();
                string apiPath = string.Format("{0}/{1}/end", ClassManagementSessions, sessionId);
                StringContent content = new StringContent("{}", Encoding.UTF8, "application/json");

                var response = await client.PostAsync(apiPath, content);
                if (response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.NoContent || response.StatusCode == System.Net.HttpStatusCode.Created)
                {
                    LogWriter.LogWrite("EndClassSession() GOT Success");
                    appSettings.sRunningClassSessionId = "";
                    return 1;
                }
                else
                {
                    if (response.Content != null)
                    {
                        string responseCode = await response.Content.ReadAsStringAsync();
                        LogWriter.LogWrite("EndClassSession() FAILED (" + (int)response.StatusCode + ") " + responseCode);
                    }
                    return 0;
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("EndClassSession() WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("EndClassSession() Exception - " + ex.Message);
            }
            return 0;
        }

        /// <summary>
        /// <c>POST /focus-mode/:id/finish</c>. Built as JSON: the "distractions" the user typed at the end of the
        /// session used to be spliced in as text, so a quote, a backslash or a new line in it made the body
        /// unreadable and the session was never finished on the server. Achievements stay URL-encoded, as they
        /// always went.
        /// </summary>
        internal static JObject FinishFocusModeBody(string achievements, string distractions, DateTime finishUtc, string metadataJson, string toDosJson)
        {
            JToken metadata, toDos;
            try { metadata = JToken.Parse(string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson); } catch (JsonException) { metadata = new JObject(); }
            try { toDos = JToken.Parse(string.IsNullOrWhiteSpace(toDosJson) ? "[]" : toDosJson); } catch (JsonException) { toDos = new JArray(); }
            return new JObject
            {
                // FinishFocusModeDto caps both at 255: longer was refused and the session never finished.
                ["achievements"] = FocusBear.Core.Stats.FocusSessionText.EncodedWithin(achievements, HttpUtility.UrlEncode),
                ["distractions"] = FocusBear.Core.Stats.FocusSessionText.Within(distractions),
                ["finish_time"] = finishUtc.ToString("o"),
                ["metadata"] = metadata,
                ["to_dos"] = toDos,
            };
        }

        /// <summary><c>POST</c> an unlock request to a buddy; the typed reason is data, whatever it contains.</summary>
        /// <summary>{"email": ...} built as JSON - the email was spliced into the string.</summary>
        internal static string EmailBody(string email)
        {
            return new JObject { ["email"] = email ?? "" }.ToString(Formatting.None);
        }

        /// <summary>{"user_occupation": ...} built as JSON - a typed occupation with a quote broke the request.</summary>
        internal static string OccupationBody(string occupation)
        {
            return new JObject { ["user_occupation"] = occupation ?? "" }.ToString(Formatting.None);
        }

        internal static JObject UnlockRequestBody(string buddyUserId, string reason, int minutes)
        {
            return new JObject
            {
                ["accountability_buddy_user_id"] = buddyUserId ?? "",
                ["reason"] = reason ?? "",
                ["unlock_duration_minutes"] = minutes,
            };
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
                   
                    string metadata = $"{{}}";
                    if (appSettings.bAiCheckUrlEnabled) //if activity is skipped just add one second to finish time
                    {
                        metadata = $"{{\"focus_alignment_score\":{appSettings.CurrentFocusAlignmentScore},\"focus_duration_seconds\":{appSettings.CurrentFocusSessionDuration}}}";
                    }
                    string addFocusJsonString = FinishFocusModeBody(achievements, distractions, end, metadata, sTasksArray).ToString(Formatting.None);


                    if (!string.IsNullOrEmpty(addFocusJsonString))
                    {
                        StringContent content = new StringContent(addFocusJsonString, Encoding.UTF8, "application/json");

                        //Make the data for Focus Mode
                        string apiPath = string.Format("https://api.focusbear.io/focus-mode/{0}/finish", BackendFocusModeId(sId));

                        LogWriter.LogWrite("FinishFocusMode() JSON: " + FocusBear.Core.Reports.LogSafe.Keys(addFocusJsonString));

                        var response = await client.PostAsync(apiPath, content);

                        if (response.StatusCode == System.Net.HttpStatusCode.Created) //201
                        {
                            LogWriter.LogWrite("FinishFocusMode() GOT Success");
                            return 1;
                        }
                        else
                        {
                            // Log status + raw body only on failure to help diagnose backend errors
                            string responseBody = response.Content != null ? await response.Content.ReadAsStringAsync() : "(empty)";
                            LogWriter.LogWrite($"FinishFocusMode() FAILED - HTTP {(int)response.StatusCode}");
                            try
                            {
                                JObject resJson = JObject.Parse(responseBody);
                                string sErrorMessage = resJson["message"]?.ToString() ?? "(no message field)";
                                LogWriter.LogWrite($"FinishFocusMode() FAILED - {sErrorMessage}");
                            }
                            catch
                            {
                                LogWriter.LogWrite($"FinishFocusMode() FAILED - raw response: {responseBody}");
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

        internal async Task<int> ExtendFocusMode(string sId, int nMinutesToExtend)
        {
            LogWriter.LogWrite("ExtendFocusMode()");
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

                    DateTime newEndTime = DateTime.Parse(appSettings.sRunningFocusModeEndTime).ToUniversalTime();

                    string extendFocusJsonString = $"{{\"scheduled_finish_time\":\"{newEndTime.ToString("o")}\"}}";

                    if (!string.IsNullOrEmpty(extendFocusJsonString))
                    {
                        StringContent content = new StringContent(extendFocusJsonString, Encoding.UTF8, "application/json");

                        string apiPath = string.Format("https://api.focusbear.io/focus-mode/{0}/update_scheduled_finish", BackendFocusModeId(sId)); 

                        LogWriter.LogWrite("ExtendFocusMode() JSON: " + FocusBear.Core.Reports.LogSafe.Keys(extendFocusJsonString));

                        var response = await client.PatchAsync(apiPath, content);
                        if (response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.Created)
                        {
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                LogWriter.LogWrite("ExtendFocusMode() GOT Success");
                                return 1;
                            }
                        }
                        else
                        {
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                try
                                {
                                    JObject resJson = JObject.Parse(responseCode);
                                    string sErrorMessage = resJson["message"].ToString();
                                    LogWriter.LogWrite("ExtendFocusMode() FAILED: " + sErrorMessage);
                                }
                                catch
                                {
                                    LogWriter.LogWrite("ExtendFocusMode() FAILED: " + responseCode);
                                }
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
                LogWriter.LogWrite("ExtendFocusMode() WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ExtendFocusMode() Exception - " + ex.Message);
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

        /// <summary>
        /// <c>GET /user/settings</c> as it stands on the server right now, or null — nothing stored, nothing
        /// parsed. An edit that PUTs the whole settings document starts from this, not from the copy fetched at
        /// launch, so it does not undo a change made meanwhile on another device.
        /// </summary>
        internal async Task<string> GetLatestUserSettingsRaw()
        {
            var response = await SendJsonRequest("GET", UserSettings + "?language=" + appSettings.sLangCode, null, "GetLatestUserSettingsRaw");
            if (!response.IsSuccess || string.IsNullOrWhiteSpace(response.Body)) return null;
            return response.Body;
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

                    if (appSettings.IsFirstInstall )
                    {
                        if (appSettings.bGoalStayFocused) //Focus Only habit pack
                        {
                            responsePath = string.Format("https://api.focusbear.io/habit-packs/default?pack_id=4a5872f5-a8e5-48c2-a2e3-83c3830fce58");
                        }
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
                            string previousSettings = appSettings.sRawUserSettings;
                            appSettings.sRawUserSettings = responseCode.ToString();
                            appSettings.dSettingsFetchedTime = DateTime.UtcNow;

                            //The log goes out with problem reports: say what changed, not the whole document.
                            LogWriter.LogWrite("UserSettings - " + FocusBear.Core.Reports.LogSafe.Describe(appSettings.sRawUserSettings)
                                + "; changed: " + FocusBear.Core.Reports.LogSafe.ChangedKeys(previousSettings, appSettings.sRawUserSettings));

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
                        string sApiPath = UserSettings;
                        sApiPath = sApiPath + "?device_id=" + appSettings.sDeviceID; //used to identify device for Pusher notifications.

                        if (is_onboarding)
                            sApiPath += "&is_onboarding=true";

                        var response = await client.PutAsync(sApiPath, content);
                        if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                        {
                            //Update date so that we do not get a notification for settings change and app does not restart.
                            appSettings.dSettingsFetchedTime = DateTime.UtcNow;

                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                if (appSettings.IsFirstInstall)
                                {
                                    appSettings.sRawUserSettings = responseCode.ToString();
                                    RegistryManager.SaveUserSettings();
                                }
                                LogWriter.LogWrite("PutUserSettings () GOT Success");
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


        /// <summary>
        /// The habits actually completed on a given day (yyyy-MM-dd), from
        /// GET /completed-activity-sequence/routine-progress — independent of the live routine state, so
        /// the Day Plan can strike through done habits even after the routine is finished. Unions
        /// completed_habit_ids minus skipped_habit_ids across every routine in the payload. Returns null
        /// on any failure. Mirrors the macOS fetchDayRoutineProgress (#2150).
        /// </summary>
        internal async Task<(HashSet<string> completed, HashSet<string> skipped)> GetDayRoutineProgress(string dateKey)
        {
            LogWriter.LogWrite("GetDayRoutineProgress() date=" + dateKey);
            try
            {
                bool nInternetOn = await IsInternetOn();
                if (!nInternetOn) return (null, null);

                if (!appSettings.ValidateTokenValidBeforeAPICall())
                {
                    var xRenwTokenStatus = await RenewToken();
                    if (xRenwTokenStatus == 0) return (null, null);
                }

                HttpClient client = GetClientWithHeaders();

                var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
                string sign = offset < TimeSpan.Zero ? "-" : "+";
                sign = HttpUtility.UrlEncode(sign);
                string time = "UTC" + sign + offset.ToString(@"hh\:mm");

                string apiPath = string.Format("https://api.focusbear.io/completed-activity-sequence/routine-progress?date={0}&timezone={1}", dateKey, time);
                LogWriter.LogWrite("GetDayRoutineProgress() URL - " + apiPath);

                var response = await client.GetAsync(apiPath);
                if (response.StatusCode == System.Net.HttpStatusCode.OK && response.Content != null)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    var completed = new HashSet<string>();
                    var skipped = new HashSet<string>();
                    JObject json = JObject.Parse(body);
                    foreach (var prop in json.Properties())
                    {
                        JObject routine = prop.Value as JObject;
                        if (routine == null) continue;
                        if (routine["skipped_habit_ids"] is JArray skippedArr)
                            foreach (var t in skippedArr) skipped.Add(t.ToString());
                        if (routine["completed_habit_ids"] is JArray doneArr)
                            foreach (var t in doneArr) { string id = t.ToString(); if (!skipped.Contains(id)) completed.Add(id); }
                    }
                    LogWriter.LogWrite("GetDayRoutineProgress() got " + completed.Count + " completed, " + skipped.Count + " skipped");
                    return (completed, skipped);
                }
                LogWriter.LogWrite("GetDayRoutineProgress() non-200: " + response.StatusCode);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetDayRoutineProgress ex - " + ex.Message);
            }
            return (null, null);
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

        private JObject PostHogContext(string osVersion) => new JObject
        {
            ["os"] = "windows",
            ["osVersion"] = osVersion ?? "",
            ["appVersion"] = appSettings.AppVersion.ToString(),
        };

        internal async Task<int> PostHogIdentify(string sAliasID) // OK
        {
            if (AppSettings.IsRunningInCIOrAutomatedTest)
            {
                return 0; //no telemetry in CI/E2E/test builds (analytics#33) - don't create fake PostHog users
            }
            LogWriter.LogWrite("PostHogIdentify() - " + sAliasID);
            try
            {
                bool nInternetOn = await IsInternetOn();

                if (nInternetOn)
                {
                    HttpClient client = new HttpClient();

                    string sOSVersion = appSettings.sOSVersion;
                    bool bUsesWindowsApp = true;
                    string sID = (appSettings.userAccount.userID != null && appSettings.userAccount.userID.Length > 0) ? appSettings.userAccount.userID : appSettings.sGlobalUUID;
                    string sJsonString = new JObject
                    {
                        ["api_key"] = "phc_Y6hD2dvO1zylm7CLij5CuNt6S5gTxe7EVXoxZcb2xUm",
                        ["event"] = "$create_alias",
                        ["context"] = PostHogContext(sOSVersion),
                        ["properties"] = new JObject
                        {
                            ["distinct_id"] = sID,
                            ["alias"] = sAliasID,
                            // Built as JSON: the typed occupation and goal used to be spliced in, and a quote dropped the event.
                            ["$set"] = new JObject
                            {
                                ["USES_WINDOWS_APP"] = bUsesWindowsApp,
                                ["mode"] = appSettings.bIsGeekModeEnabled ? "geek-mode" : "simple-mode",
                                ["occupation"] = appSettings.sSavedOccupationForPosthog ?? "",
                                ["reason-for-using-focus-bear"] = appSettings.sCustomGoalSelected ?? "",
                                ["care-factor-habits"] = appSettings.sCareFactorString ?? "",
                                ["isPayingUser"] = appSettings.isPurchased,
                            },
                        },
                    }.ToString(Formatting.None);


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
                                LogWriter.LogWrite("PostHogIdentify() FAILED" + sErrorMessage);
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
                LogWriter.LogWrite("PostHogIdentify WebException - " + ex.Message);

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PostHogIdentify Exception - " + ex.Message);

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
                    string sID = (appSettings.userAccount.userID != null && appSettings.userAccount.userID.Length > 0) ? appSettings.userAccount.userID : appSettings.sGlobalUUID;
                    var properties = new JObject
                    {
                        ["distinct_id"] = sID,
                        ["osVersion"] = sOSVersion,
                        ["$set"] = new JObject { ["mode"] = appSettings.bIsGeekModeEnabled ? "geek-mode" : "simple-mode" },
                        ["appVersion"] = appSettings.AppVersion.ToString(),
                        ["USES_WINDOWS_APP"] = bUsesWindowsApp,
                    };
                    // The caller's members; a fragment that does not parse costs the event its extras, not the event.
                    JObject extra = FocusBear.Core.Analytics.EventPayload.Properties(sMetadata);
                    if (extra == null) LogWriter.LogWrite("CapturePostHog() - unreadable metadata for " + sEvent);
                    FocusBear.Core.Analytics.EventPayload.Merge(properties, extra);
                    string sJsonString = new JObject
                    {
                        ["api_key"] = "phc_Y6hD2dvO1zylm7CLij5CuNt6S5gTxe7EVXoxZcb2xUm",
                        ["event"] = sEvent ?? "",
                        ["context"] = PostHogContext(sOSVersion),
                        ["isPayingUser"] = appSettings.isPurchased,
                        ["properties"] = properties,
                    }.ToString(Formatting.None);


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
                    string sID = (appSettings.userAccount.userID != null && appSettings.userAccount.userID.Length > 0) ? appSettings.userAccount.userID : appSettings.sGlobalUUID;

                    JObject data = FocusBear.Core.Analytics.EventPayload.Properties(sMetadata);
                    if (data == null) { LogWriter.LogWrite("CaptureEventEndpoint() - unreadable metadata for " + sEvent); data = new JObject(); }
                    // Built as JSON: the user name used to be spliced in, and a quote in it dropped every event.
                    string sJsonString = new JObject
                    {
                        ["event_type"] = sEvent ?? "",
                        ["event_data"] = new JObject { ["data"] = data },
                        ["user_properties"] = new JObject
                        {
                            ["distinct_id"] = sID,
                            ["USERNAME"] = appSettings.sUser_name ?? "",
                            ["APPVERSION"] = appSettings.AppVersion.ToString(),
                            ["USES_WINDOWS_APP"] = bUsesWindowsApp,
                        },
                    }.ToString(Formatting.None);

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

        /// <summary>
        /// The <c>POST</c> body of the URL safety check. Built as JSON: it used to be spliced together as text, and
        /// a tab title, page description, intention or "convince the AI" justification with a quote or backslash
        /// in it - most page titles, and anything a person types - made the body unreadable, so the check failed
        /// and the page was judged without the AI.
        /// </summary>
        internal static JObject UrlSafetyRequest(string url, string tabTitle, string metaDescription, string focusMode, string intention,
                                                 JArray currentTasks, string currentTaskInToDoPlayer, string extraJustification,
                                                 string[] lastFiveJustifications, string language, bool taskMustAlignToFocusIntention)
        {
            var payload = new JObject
            {
                ["url"] = url ?? "",
                ["tab_title"] = tabTitle ?? "",
                ["meta_description"] = metaDescription ?? "",
                ["focus_mode"] = focusMode ?? "",
                ["intention"] = intention ?? "",
                ["current_tasks"] = currentTasks ?? new JArray(),
                ["currentTaskInToDoPlayer"] = currentTaskInToDoPlayer ?? "",
                ["extraJustificationForThisSite"] = extraJustification ?? "",
                ["lastFiveJustificationsInThisFocusSession"] = new JArray((lastFiveJustifications ?? new string[0]).Cast<object>().ToArray()),
                ["language"] = language ?? "",
            };
            if (taskMustAlignToFocusIntention) payload["task_must_align_to_focus_intention"] = true;
            return payload;
        }

        /// <summary>
        /// E2E test seam (E2EBuild.FakeAiVariable): the answer the backend would give, set the same way, without the
        /// request. False (and nothing set) in every other build, or when the variable isn't set.
        /// </summary>
        private bool TryFakeAiAnswer(string item, out double probability)
        {
            if (!Utils.E2EBuild.TryFakeAi(item, out probability, out string reason)) return false;
            var res = new JObject { ["allowed_probability"] = probability, ["reason"] = reason };
            appSettings.sChatGPTResponse = res.ToString(Formatting.None);
            appSettings.sChatGPTReasonForCurrentURL = reason;
            appSettings.dProbablity = probability;
            LogWriter.LogWrite("E2E fake AI: " + item + " -> " + probability.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return true;
        }

        internal async Task<double> ChatGPTIsURLSafe(string url, string tab_title, string meta_description, string focus_mode, string intention, string language)
        {
            LogWriter.LogWrite("ChatGPTIsURLSafe() = " + url);
            if (TryFakeAiAnswer(url, out double fakeUrl)) return fakeUrl;
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

                    //sRawTasks is empty until the task list has been loaded (and could be malformed),
                    //so default to an empty array instead of letting JArray.Parse throw and abort the
                    //whole URL safety check - the API just receives current_tasks: [].
                    JArray rawTasks;
                    if (string.IsNullOrWhiteSpace(appSettings.sRawTasks))
                    {
                        rawTasks = new JArray();
                    }
                    else
                    {
                        try
                        {
                            rawTasks = JArray.Parse(appSettings.sRawTasks);
                        }
                        catch (Exception exParse)
                        {
                            LogWriter.LogWrite("ChatGPTIsURLSafe() - sRawTasks parse failed, using empty list - " + exParse.Message);
                            rawTasks = new JArray();
                        }
                    }

                    JArray sRawTasksToPassInSafeAPI = new JArray(
                        rawTasks.Select(task => new JObject
                        {
                            ["task_name"] = task["title"],
                            ["task_id"] = task["id"]
                        })
                    );

                    // Pass task_must_align_to_focus_intention=true when a blocking schedule is running in super strict mode
                    bool isSuperStrictMode = appSettings.isBlockingScheduleRunning &&
                        appSettings.ActiveSchedule?.BlockLevel == FOCUSMODESTRICTNESS.IRON_FOCUS;
                    string addItemJsonString = UrlSafetyRequest(url, tab_title, meta_description, focus_mode, intention, sRawTasksToPassInSafeAPI,
                                                                appSettings.sCurrentTaskPlayingInToDoPlayer, appSettings.sExtraJustificationForThisSite,
                                                                appSettings._last5Justifications.ToArray(), lang, isSuperStrictMode).ToString(Formatting.None);

                    //"lastFiveJustificationsInThisFocusSession": other_explanations_including_domains,
                    //if let currentHabitName = currentHabitName{
                    //    json["habit_name"] = currentHabitName
                    //}


                    if (!string.IsNullOrEmpty(addItemJsonString))
                    {
                        StringContent content = new StringContent(addItemJsonString, Encoding.UTF8, "application/json");
                        
                        appSettings.sChatGPTPayLoad = addItemJsonString;

                        client.Timeout = TimeSpan.FromSeconds(15); // 15 seconds time out

                        var response = await client.PostAsync("https://api.focusbear.io/user/is-url-safe-to-use", content);
                        if (response.StatusCode == System.Net.HttpStatusCode.Created) //201
                        {
                            LogWriter.LogWrite("ChatGPTIsURLSafe() SUCCESS");
                            LogWriter.LogWrite("response = " + (int)response.StatusCode);
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
                                    double? read = FocusBear.Core.Blocking.AiProbability.Read(resJson["allowed_probability"]);
                                    if (!read.HasValue) { LogWriter.LogWrite("ChatGPTIsURLSafe - unreadable allowed_probability: " + resJson["allowed_probability"]); return -1; }
                                    double dProbability = read.Value;
                                    appSettings.dProbablity = dProbability;

                                    return appSettings.dProbablity;
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
                return 0.70; //mostly timeout case
            }
            return -1;
        }

        // App equivalent of ChatGPTIsURLSafe - hits the separate /user/is-app-safe-to-use endpoint
        // (mirrors the macOS chatGPTIsAppSafe in AllApi.swift). Returns the allowed probability
        // (0..1), 1 if the response had none, or -1 on failure.
        internal async Task<double> ChatGPTIsAppSafe(string appName, string focus_mode, string intention, string language)
        {
            LogWriter.LogWrite("ChatGPTIsAppSafe() = " + appName);
            if (TryFakeAiAnswer(appName, out double fakeApp)) return fakeApp;
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

                    //sRawTasks is empty until the task list has been loaded (and could be malformed),
                    //so default to an empty array instead of letting JArray.Parse throw and abort the
                    //whole app safety check - the API just receives current_tasks: [].
                    JArray rawTasks;
                    if (string.IsNullOrWhiteSpace(appSettings.sRawTasks))
                    {
                        rawTasks = new JArray();
                    }
                    else
                    {
                        try
                        {
                            rawTasks = JArray.Parse(appSettings.sRawTasks);
                        }
                        catch (Exception exParse)
                        {
                            LogWriter.LogWrite("ChatGPTIsAppSafe() - sRawTasks parse failed, using empty list - " + exParse.Message);
                            rawTasks = new JArray();
                        }
                    }

                    JArray sRawTasksToPassInSafeAPI = new JArray(
                        rawTasks.Select(task => new JObject
                        {
                            ["task_name"] = task["title"],
                            ["task_id"] = task["id"]
                        })
                    );

                    // Build the payload with a JObject so app names / justifications are safely escaped.
                    // Keys match the macOS chatGPTIsAppSafe request exactly.
                    JObject payload = new JObject
                    {
                        ["focusMode"] = focus_mode,
                        ["intention"] = intention,
                        ["appName"] = appName,
                        ["language"] = lang,
                        ["lastFiveJustificationsInThisFocusSession"] = JArray.FromObject(appSettings._last5Justifications.ToArray()),
                        ["current_tasks"] = sRawTasksToPassInSafeAPI
                    };

                    if (!string.IsNullOrEmpty(appSettings.sCurrentTaskPlayingInToDoPlayer))
                        payload["currentTaskInToDoPlayer"] = appSettings.sCurrentTaskPlayingInToDoPlayer;

                    // The user's "convince AI" justification for this app (empty on the first check),
                    // so a re-check after convincing can change the verdict - same as the URL flow.
                    payload["extraJustificationForThisSite"] = appSettings.sExtraJustificationForThisSite ?? "";

                    // A habit with "use focus mode" running carries its activity name as the habit_name.
                    if (appSettings.bHabitwithUseFocusModeRunning && !string.IsNullOrEmpty(appSettings.sCurrentRunningActivity))
                        payload["habit_name"] = appSettings.sCurrentRunningActivity;

                    // task_must_align_to_focus_intention when a blocking schedule is running in super strict mode
                    if (appSettings.isBlockingScheduleRunning &&
                        appSettings.ActiveSchedule?.BlockLevel == FOCUSMODESTRICTNESS.IRON_FOCUS)
                        payload["task_must_align_to_focus_intention"] = true;

                    string addItemJsonString = payload.ToString(Formatting.None);

                    StringContent content = new StringContent(addItemJsonString, Encoding.UTF8, "application/json");

                    appSettings.sChatGPTPayLoad = addItemJsonString;

                    client.Timeout = TimeSpan.FromSeconds(15); // 15 seconds time out

                    var response = await client.PostAsync("https://api.focusbear.io/user/is-app-safe-to-use", content);
                    if (response.StatusCode == System.Net.HttpStatusCode.Created) //201
                    {
                        LogWriter.LogWrite("ChatGPTIsAppSafe() SUCCESS");
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            if (responseCode == "")
                            {
                                LogWriter.LogWrite("ChatGPTIsAppSafe() responseCode = null");
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
                                double? read = FocusBear.Core.Blocking.AiProbability.Read(resJson["allowed_probability"]);
                                if (!read.HasValue) { LogWriter.LogWrite("ChatGPTIsAppSafe - unreadable allowed_probability: " + resJson["allowed_probability"]); return -1; }
                                double dProbability = read.Value;
                                appSettings.dProbablity = dProbability;
                                return appSettings.dProbablity;
                            }

                            return 1;
                        }
                    }
                    else
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            try
                            {
                                JObject resJson = JObject.Parse(responseCode);
                                string sErrorMessage = resJson["message"]?.ToString();
                                LogWriter.LogWrite("ChatGPTIsAppSafe() FAILED - " + sErrorMessage);
                            }
                            catch { }
                            return -1;
                        }
                        return -1;
                    }
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("ChatGPTIsAppSafe WebException" + ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ChatGPTIsAppSafe Exception" + ex.Message);
                Debug.WriteLine(ex.Message);
                return 0.70; //mostly timeout case
            }
            return -1;
        }

        private DateTime _lastGetAllTasksCall = DateTime.MinValue;
        internal async Task<int> GetAllTasks() //OK
        {
            LogWriter.LogWrite("GetAllTasks()");

            try
            {
                // Debounce for 15 seconds
                if ((DateTime.UtcNow - _lastGetAllTasksCall).TotalSeconds < 15)
                {
                    return 0;
                }

                _lastGetAllTasksCall = DateTime.UtcNow;


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
                    string sPath = AllTasks + "/recent?take=50"; //To get all the recent tasks
                    var response = await client.GetAsync(sPath);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            appSettings.sAllTasks = responseCode.ToString();

                            LogWriter.LogWrite("GetAllTasks() GOT Success");

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
                            LogWriter.LogWrite("GetAllTasks() FAILED - " + sErrorMessage);
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
                LogWriter.LogWrite("GetAllTasks WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetAllTasks Exception - " + ex.Message);
            }
            return 0;
        }

        /// <summary>
        /// Fetches the to-do list in the backend's SMART (TOP-score) priority order — the same source
        /// and order the web to-do list and the macOS Day Plan picker use. Unlike <see cref="GetAllTasks"/>
        /// (<c>/to-do/recent</c>, recency, and includes completed), this endpoint is prioritised and
        /// already filtered to <c>status=NOT_STARTED</c>, so the Day Plan "From to-do list" picker shows
        /// the user's actual open tasks. Returns the raw JSON body (a plain array or a <c>{ data: [...] }</c>
        /// wrapper), or null on failure. Not debounced — it's only called when the picker opens.
        /// </summary>
        internal async Task<string> GetSmartTasks()
        {
            LogWriter.LogWrite("GetSmartTasks()");
            try
            {
                if (!await IsInternetOn())
                {
                    LogWriter.LogWrite("GetSmartTasks: no internet");
                    return null;
                }

                if (!appSettings.ValidateTokenValidBeforeAPICall())
                {
                    var renew = await RenewToken();
                    if (renew == 0) return null;
                }

                HttpClient client = GetClientWithHeaders();
                string sPath = AllTasks + "?page=1&take=50&status=NOT_STARTED&order=DESC&sort_mode=SMART";
                var response = await client.GetAsync(sPath);
                if (response.StatusCode == System.Net.HttpStatusCode.OK && response.Content != null)
                {
                    string body = await response.Content.ReadAsStringAsync();
                    LogWriter.LogWrite("GetSmartTasks GOT Success (" + (body?.Length ?? 0) + " chars)");
                    return body;
                }

                LogWriter.LogWrite("GetSmartTasks: status " + (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetSmartTasks Exception - " + ex.Message);
            }
            return null;
        }

        /// <summary>
        /// Reads the Day Plan for a day — the to-dos the backend has planned onto it (their
        /// <c>planned_date</c> is this day), so a task planned on the web/mobile shows up here too.
        /// <c>GET /day-plan/day/:day</c>, returning the raw JSON body (<c>{ tasks: ToDo[] }</c>) or null.
        /// READ-ONLY: this is the cross-device *read* half of Day Plan sync. The *write* half
        /// (planning a Windows-made association back to the backend) is intentionally not implemented
        /// yet — its exact payload isn't confirmed and a wrong mutation would corrupt real to-dos.
        /// </summary>
        internal async Task<string> GetDayPlanForDay(string dayKey)
        {
            LogWriter.LogWrite("GetDayPlanForDay(" + dayKey + ")");
            try
            {
                if (!await IsInternetOn()) return null;
                if (!appSettings.ValidateTokenValidBeforeAPICall())
                {
                    var renew = await RenewToken();
                    if (renew == 0) return null;
                }

                HttpClient client = GetClientWithHeaders();
                string sPath = "https://api.focusbear.io/day-plan/day/" + dayKey;
                var response = await client.GetAsync(sPath);
                if (response.StatusCode == System.Net.HttpStatusCode.OK && response.Content != null)
                {
                    string body = await response.Content.ReadAsStringAsync();
                    LogWriter.LogWrite("GetDayPlanForDay GOT Success (" + (body?.Length ?? 0) + " chars)");
                    return body;
                }
                LogWriter.LogWrite("GetDayPlanForDay: status " + (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetDayPlanForDay Exception - " + ex.Message);
            }
            return null;
        }

        /// <summary>
        /// The user's connected calendars for a platform (<c>GET /calendar/{platform}/calendar-datas</c>,
        /// platform = "google" | "microsoft"). Each item carries the account, calendar id, name and an
        /// is-selected flag. Returns the raw JSON body or null. The Windows Day Plan uses the backend
        /// calendar sync (Google / Microsoft) instead of a native calendar, since Windows has no EventKit.
        /// </summary>
        internal async Task<string> GetCalendarDatas(string platform)
        {
            LogWriter.LogWrite("GetCalendarDatas(" + platform + ")");
            try
            {
                if (!await IsInternetOn()) return null;
                if (!appSettings.ValidateTokenValidBeforeAPICall())
                {
                    var renew = await RenewToken();
                    if (renew == 0) return null;
                }
                HttpClient client = GetClientWithHeaders();
                string sPath = "https://api.focusbear.io/calendar/" + platform + "/calendar-datas";
                var response = await client.GetAsync(sPath);
                if (response.StatusCode == System.Net.HttpStatusCode.OK && response.Content != null)
                {
                    string body = await response.Content.ReadAsStringAsync();
                    LogWriter.LogWrite("GetCalendarDatas(" + platform + ") ok (" + (body?.Length ?? 0) + " chars)");
                    return body;
                }
                LogWriter.LogWrite("GetCalendarDatas(" + platform + "): status " + (int)response.StatusCode);
            }
            catch (Exception ex) { LogWriter.LogWrite("GetCalendarDatas Exception - " + ex.Message); }
            return null;
        }

        /// <summary>
        /// Calendar events for a connected account (<c>GET /calendar/{platform}/events?account=…</c>).
        /// Returns the raw platform events (Google or Microsoft shape) as a JSON body, or null. Read-only —
        /// the backend syncs from Google/Outlook; there is no create-event endpoint here.
        /// <paramref name="range"/> is the extra query: <c>days=N</c> (today .. today+N-1, N ≤ 90) or
        /// <c>day=yyyy-MM-dd</c> (one day); without it the backend returns today only.
        /// </summary>
        internal async Task<string> GetCalendarEvents(string platform, string account, string range = null)
        {
            LogWriter.LogWrite("GetCalendarEvents(" + platform + ", " + account + (range != null ? ", " + range : "") + ")");
            try
            {
                if (!await IsInternetOn()) return null;
                if (!appSettings.ValidateTokenValidBeforeAPICall())
                {
                    var renew = await RenewToken();
                    if (renew == 0) return null;
                }
                HttpClient client = GetClientWithHeaders();
                string sPath = "https://api.focusbear.io/calendar/" + platform + "/events?account=" + Uri.EscapeDataString(account ?? "")
                    + (string.IsNullOrEmpty(range) ? "" : "&" + range);
                var response = await client.GetAsync(sPath);
                if (response.StatusCode == System.Net.HttpStatusCode.OK && response.Content != null)
                {
                    string body = await response.Content.ReadAsStringAsync();
                    LogWriter.LogWrite("GetCalendarEvents(" + platform + ") ok (" + (body?.Length ?? 0) + " chars)");
                    return body;
                }
                LogWriter.LogWrite("GetCalendarEvents(" + platform + "): status " + (int)response.StatusCode);
            }
            catch (Exception ex) { LogWriter.LogWrite("GetCalendarEvents Exception - " + ex.Message); }
            return null;
        }

        /// <summary>Hide a meeting for a day (<c>PUT /day-plan/day/{day}/dismissed-event/{eventId}</c>).</summary>
        internal Task<bool> DismissEvent(string dayKey, string eventId) =>
            DayPlanEventDismiss(dayKey, eventId, dismiss: true);

        /// <summary>Un-hide a previously dismissed meeting (<c>DELETE</c> the same path).</summary>
        /// <summary>
        /// <c>GET /to-do/by-ids?ids=a,b,…</c> (at most 100 per call) — the current rows for exactly these
        /// to-dos, completed ones included. Returns one merged JSON array, or null when any call fails,
        /// so a partial answer is never mistaken for the whole picture.
        /// </summary>
        internal async Task<string> GetToDosByIds(IEnumerable<string> ids)
        {
            try
            {
                List<string> all = ids.Where(id => Guid.TryParse(id, out _)).Distinct().ToList();
                var merged = new JArray();
                if (all.Count == 0) return merged.ToString(Formatting.None);
                if (!await IsInternetOn()) return null;
                if (!appSettings.ValidateTokenValidBeforeAPICall())
                {
                    if (await RenewToken() == 0) return null;
                }
                HttpClient client = GetClientWithHeaders();
                for (int i = 0; i < all.Count; i += 100)
                {
                    string path = AllTasks + "/by-ids?ids=" + string.Join(",", all.Skip(i).Take(100));
                    var response = await client.GetAsync(path);
                    if (!response.IsSuccessStatusCode)
                    {
                        LogWriter.LogWrite("GetToDosByIds http " + (int)response.StatusCode);
                        return null;
                    }
                    AppendByIdsRows(merged, await response.Content.ReadAsStringAsync());
                }
                return merged.ToString(Formatting.None);
            }
            catch (Exception ex) { LogWriter.LogWrite("GetToDosByIds - " + ex.Message); return null; }
        }

        /// <summary>
        /// One <c>/to-do/by-ids</c> page into the merged array, its values exactly as the server spelled
        /// them. Read with <see cref="ToDoJson.Parse"/> (no date parsing), never <c>JToken.Parse</c>: Json.NET's
        /// default turns <c>"2026-09-27T00:00:00.000Z"</c> into a DateTime and writes it back as
        /// <c>"2026-09-27T00:00:00Z"</c>, and a row that no longer reads like the catalogue's own looks
        /// edited elsewhere (the reschedule plan's "Saved 0 date changes", 2026-09-25).
        /// </summary>
        internal static void AppendByIdsRows(JArray merged, string body)
        {
            if (FocusBear.Core.ToDo.ToDoJson.Parse(body) is JArray rows)
                foreach (JToken row in rows) merged.Add(row);
        }

        internal Task<bool> UndismissEvent(string dayKey, string eventId) =>
            DayPlanEventDismiss(dayKey, eventId, dismiss: false);

        private async Task<bool> DayPlanEventDismiss(string dayKey, string eventId, bool dismiss)
        {
            string label = dismiss ? "DismissEvent" : "UndismissEvent";
            try
            {
                if (string.IsNullOrEmpty(eventId)) return false;
                if (!await IsInternetOn()) return false;
                if (!appSettings.ValidateTokenValidBeforeAPICall())
                {
                    var renew = await RenewToken();
                    if (renew == 0) return false;
                }
                HttpClient client = GetClientWithHeaders();
                string path = "https://api.focusbear.io/day-plan/day/" + dayKey + "/dismissed-event/" + Uri.EscapeDataString(eventId);
                var response = dismiss
                    ? await client.PutAsync(path, new StringContent("{}", Encoding.UTF8, "application/json"))   // backend rejects an empty json body
                    : await client.DeleteAsync(path);
                if (!response.IsSuccessStatusCode) LogWriter.LogWrite(label + " http " + (int)response.StatusCode);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) { LogWriter.LogWrite(label + " - " + ex.Message); return false; }
        }

        /// <summary>
        /// The user's per-meeting colour overrides (<c>GET /day-plan/event-colour</c>). Returns the raw JSON
        /// body (an array of <c>{ event_id, colour }</c>) or null. Best-effort; a missing endpoint no-ops.
        /// </summary>
        internal async Task<string> GetEventColours()
        {
            try
            {
                if (!await IsInternetOn()) return null;
                if (!appSettings.ValidateTokenValidBeforeAPICall())
                {
                    var renew = await RenewToken();
                    if (renew == 0) return null;
                }
                HttpClient client = GetClientWithHeaders();
                var response = await client.GetAsync("https://api.focusbear.io/day-plan/event-colour");
                if (response.StatusCode == System.Net.HttpStatusCode.OK && response.Content != null)
                {
                    string body = await response.Content.ReadAsStringAsync();
                    LogWriter.LogWrite("GetEventColours ok (" + (body?.Length ?? 0) + " chars)");
                    return body;
                }
                LogWriter.LogWrite("GetEventColours: status " + (int)response.StatusCode);
            }
            catch (Exception ex) { LogWriter.LogWrite("GetEventColours Exception - " + ex.Message); }
            return null;
        }

        /// <summary>Set a meeting's colour (<c>PUT /day-plan/event-colour</c>, body <c>{ event_id, colour }</c>).</summary>
        internal Task<bool> SetEventColour(string eventId, string colour) =>
            EventColourWrite(eventId, colour, clear: false);

        /// <summary>Reset a meeting to the default colour (<c>DELETE /day-plan/event-colour</c>).</summary>
        internal Task<bool> ClearEventColour(string eventId) =>
            EventColourWrite(eventId, null, clear: true);

        private async Task<bool> EventColourWrite(string eventId, string colour, bool clear)
        {
            string label = clear ? "ClearEventColour" : "SetEventColour";
            try
            {
                if (string.IsNullOrEmpty(eventId)) return false;
                if (!await IsInternetOn()) return false;
                if (!appSettings.ValidateTokenValidBeforeAPICall())
                {
                    var renew = await RenewToken();
                    if (renew == 0) return false;
                }
                HttpClient client = GetClientWithHeaders();
                const string path = "https://api.focusbear.io/day-plan/event-colour";
                var payload = new JObject { ["event_id"] = eventId };
                if (!clear) payload["colour"] = colour;
                var content = new StringContent(payload.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");

                HttpResponseMessage response;
                if (clear)
                {
                    // DELETE with a body — HttpClient.DeleteAsync has no body overload, so build the request.
                    var req = new HttpRequestMessage(HttpMethod.Delete, path) { Content = content };
                    response = await client.SendAsync(req);
                }
                else
                {
                    response = await client.PutAsync(path, content);
                }
                if (!response.IsSuccessStatusCode) LogWriter.LogWrite(label + " http " + (int)response.StatusCode);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) { LogWriter.LogWrite(label + " - " + ex.Message); return false; }
        }

        /// <summary>
        /// Fetch the user's project names (<c>GET /projects</c>) so the Day Plan can suggest projects
        /// created on the web/mobile, not just those already used by a local task. Response is a bare
        /// array or <c>{ data:[…], projects:[…] }</c>; each item's name is <c>name</c> or <c>title</c>.
        /// Returns the distinct names (first page only), or null on failure.
        /// </summary>
        internal async Task<List<string>> GetProjects()
        {
            Dictionary<string, string> byId = await GetProjectsById();
            if (byId == null) return null;
            return byId.Values.Distinct().ToList();
        }

        /// <summary>
        /// Every project of the user's, id -> name, across all pages of <c>/projects</c> (the Mac's
        /// fetchProjectsPage: <c>?page=N</c>, deduped by id, stopping at the reported total, an empty
        /// page or 50 pages). Used to name a to-do by its <c>project_id</c>
        /// (<see cref="FocusBear.Core.Schedule.TodoProjects"/>). Null when nothing could be fetched.
        /// </summary>
        internal async Task<Dictionary<string, string>> GetProjectsById()
        {
            try
            {
                if (!await IsInternetOn()) return null;
                if (!appSettings.ValidateTokenValidBeforeAPICall())
                {
                    var renew = await RenewToken();
                    if (renew == 0) return null;
                }
                HttpClient client = GetClientWithHeaders();
                var byId = new Dictionary<string, string>();
                var unnamedIds = 0;
                for (int page = 1; page <= 50; page++)
                {
                    string url = page == 1 ? "https://api.focusbear.io/projects" : "https://api.focusbear.io/projects?page=" + page;
                    var response = await client.GetAsync(url);
                    if (response.StatusCode != System.Net.HttpStatusCode.OK || response.Content == null)
                    {
                        LogWriter.LogWrite("GetProjectsById: page " + page + " status " + (int)response.StatusCode);
                        if (page == 1) return null;
                        break;
                    }
                    string body = await response.Content.ReadAsStringAsync();
                    int total;
                    var items = FocusBear.Core.Schedule.TodoProjects.ParseProjectsPage(JToken.Parse(body), out total);
                    int added = 0;
                    foreach (var item in items)
                    {
                        string key = string.IsNullOrEmpty(item.Key) ? "noid:" + (unnamedIds++) : item.Key;
                        if (byId.ContainsKey(key)) continue;   // ?page= ignored -> the same set repeats: stop
                        byId[key] = item.Value;
                        added++;
                    }
                    if (added == 0 || (total > 0 && byId.Count >= total)) break;
                }
                LogWriter.LogWrite("GetProjectsById got " + byId.Count);
                return byId;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetProjectsById Exception - " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// The write half of Day Plan sync — the Windows port of macOS <c>DayPlanSync</c>. Both calls
        /// reuse the existing <c>PUT /to-do</c> UPSERT with the to-do's <c>id</c> (so they update the
        /// existing to-do, never create a duplicate) and echo back <c>title</c> + <c>eisenhower_quadrant</c>
        /// so the create-DTO upsert doesn't clobber them. Best-effort: failures are logged, not surfaced.
        /// </summary>
        private async Task<bool> PutTodoUpsert(JObject body, string label)
        {
            try
            {
                if (!await IsInternetOn()) return false;
                if (!appSettings.ValidateTokenValidBeforeAPICall())
                {
                    var renew = await RenewToken();
                    if (renew == 0) return false;
                }
                HttpClient client = GetClientWithHeaders();
                var content = new StringContent(body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
                var response = await client.PutAsync(AllTasks, content);
                if (!response.IsSuccessStatusCode)
                    LogWriter.LogWrite("PutTodoUpsert " + label + " http " + (int)response.StatusCode);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PutTodoUpsert " + label + " - " + ex.Message);
                return false;
            }
        }

        /// <summary>Reflect a Day Plan task's completion onto its backend to-do (web/mobile update too).</summary>
        internal Task<bool> SetTodoStatus(string todoId, string title, int quadrant, bool completed)
        {
            var body = new JObject
            {
                ["id"] = todoId,
                ["title"] = title,
                ["eisenhower_quadrant"] = quadrant,
                ["status"] = completed ? "COMPLETED" : "NOT_STARTED",
            };
            return PutTodoUpsert(body, "setTodoStatus");
        }

        /// <summary>Set/clear the day-plan pointer fields on a to-do (<paramref name="plannedDate"/> null unplans it).</summary>
        internal Task<bool> SetTodoPlanned(string todoId, string title, int quadrant, DateTime? plannedDate, string blockKey, int? order)
        {
            var body = new JObject
            {
                ["id"] = todoId,
                ["title"] = title,
                ["eisenhower_quadrant"] = quadrant,
                ["planned_date"] = plannedDate.HasValue
                    ? (JToken)plannedDate.Value.ToString("yyyy-MM-dd")
                    : JValue.CreateNull(),
            };
            if (blockKey != null) body["planned_block_key"] = blockKey;
            if (order.HasValue) body["planned_order"] = order.Value;
            // The Day Plan files synced tasks by DUE DATE (planned_date is the legacy pointer), so planning a
            // to-do onto a day moves its due date there and removing it clears it — the Mac's setTodoPlanned
            // (report 2026-09-25_19-54-42). Without this the next due-date refresh would move it straight back.
            body["due_date"] = plannedDate.HasValue ? (JToken)plannedDate.Value.ToString("yyyy-MM-dd") : JValue.CreateNull();
            return PutTodoUpsert(body, "setTodoPlanned");
        }

        // Overload kept for existing callers that only pass a title (no explicit due date).
        internal Task<int> AddTask(string sTaskTitle) => AddTask(sTaskTitle, null);

        // Creates a to-do. When dueDateUtc is null the due date defaults to one week from now.
        // The voice-command path passes the datetime parsed from the spoken command so the task
        // is created with the requested due date and time.
        internal async Task<int> AddTask(string sTaskTitle, DateTime? dueDateUtc) // OK
        {
            LogWriter.LogWrite("AddTask()");
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

                    DateTime dueDate = (dueDateUtc ?? DateTime.UtcNow.AddDays(7)).ToUniversalTime();

                    // Build via JObject so titles containing quotes/newlines (e.g. from voice
                    // transcription) can't break the JSON payload.
                    JObject addItemPayload = new JObject
                    {
                        ["title"] = sTaskTitle,
                        ["eisenhower_quadrant"] = 1,
                        ["due_date"] = dueDate.ToString("o")
                    };
                    string addItemJsonString = addItemPayload.ToString(Newtonsoft.Json.Formatting.None);

                    if (!string.IsNullOrEmpty(addItemJsonString))
                    {
                        StringContent content = new StringContent(addItemJsonString, Encoding.UTF8, "application/json");

                        //Make the data for Focus Mode
                        var response = await client.PutAsync(AllTasks, content);
                        if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                        {
                            if (response.Content != null)
                            {
                                LogWriter.LogWrite("AddTask() GOT Success");
                                var responseCode = await response.Content.ReadAsStringAsync();
                                appSettings.sCurrentTaskCreated = responseCode.ToString();
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
                                LogWriter.LogWrite("AddTask() FAILED" + sErrorMessage);
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
                LogWriter.LogWrite("AddTask WebException - " + ex.Message);

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AddTask Exception - " + ex.Message);

            }
            return 0;
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
                    LogWriter.Flush();

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

        internal async Task<int> RecordUserConsent(string sConsentType) // OK
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

                    //sConsentType can be terms_of_service or privacy_policy
                    bool bConsentStatus = true;
                    string addItemJsonString = $"{{\"consent_type\":\"{sConsentType}\"," +
                           $"\"consent_status\":\"{JsonConvert.SerializeObject(bConsentStatus)}\"," +
                           $"\"metadata\":{{\"policy_version\":\"1.0.0\",\"operating_system\":\"windows\"}}}}";

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

        internal async Task<int> SyncPlatformCourses() // OK
        {
            LogWriter.LogWrite("SyncPlatformCourses()");
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

                    string addItemJsonString = $"{{\"platform\":\"win\"}}";

                    if (!string.IsNullOrEmpty(addItemJsonString))
                    {
                        StringContent content = new StringContent(addItemJsonString, Encoding.UTF8, "application/json");

                        //Make the data for Focus Mode
                        var response = await client.PostAsync("https://api.focusbear.io/course/sync-platform-courses", content);
                        if (response.StatusCode == System.Net.HttpStatusCode.Created) //200
                        {
                            if (response.Content != null)
                            {
                                LogWriter.LogWrite("SyncPlatformCourses() GOT Success");
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
                                LogWriter.LogWrite("SyncPlatformCourses() FAILED" + sErrorMessage);
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
                LogWriter.LogWrite("SyncPlatformCourses WebException - " + ex.Message);

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SyncPlatformCourses Exception - " + ex.Message);

            }
            return 0;
        }

        internal async Task<int> EnrollForCourses() // OK
        {
            LogWriter.LogWrite("EnrollForCourses()");
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

                    string addItemJsonString = $"{{\"course_id\":\"8c7e4d6e-b43a-496d-bb17-90de77d3b8ee\"}}";

                    if (!string.IsNullOrEmpty(addItemJsonString))
                    {
                        StringContent content = new StringContent(addItemJsonString, Encoding.UTF8, "application/json");

                        //Make the data for Focus Mode
                        var response = await client.PostAsync("https://api.focusbear.io/course/enrolled", content);
                        if (response.StatusCode == System.Net.HttpStatusCode.Created) //200
                        {
                            if (response.Content != null)
                            {
                                LogWriter.LogWrite("EnrollForCourses() GOT Success");
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
                                LogWriter.LogWrite("EnrollForCourses() FAILED" + sErrorMessage);
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
                LogWriter.LogWrite("EnrollForCourses WebException - " + ex.Message);

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("EnrollForCourses Exception - " + ex.Message);

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

        /// <summary>
        /// Posts one batch of the offline queue. Returns the HTTP status, or null when there was no answer (offline,
        /// token renewal failed, exception). OfflineQueueSync.Classify decides what the status means for the queue -
        /// this used to report any 4xx as success, so one bad entry discarded its whole batch.
        /// </summary>
        internal async Task<int?> PostOfflineBatch(string addItemJsonString)
        {
            LogWriter.LogWrite("PostOfflineBatch()");
            try
            {
                if (string.IsNullOrEmpty(addItemJsonString) || !await IsInternetOn())
                    return null;

                if (!appSettings.ValidateTokenValidBeforeAPICall())
                {
                    var xRenwTokenStatus = await RenewToken();
                    if (xRenwTokenStatus == 0)
                        return null;
                }

                HttpClient client = GetClientWithHeaders();
                LogWriter.LogWrite("PostOfflineBatch() body " + FocusBear.Core.Reports.LogSafe.Describe(addItemJsonString));

                StringContent content = new StringContent(addItemJsonString, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("https://api.focusbear.io/completed-activity/sync", content);
                LogWriter.LogWrite("PostOfflineBatch() status " + (int)response.StatusCode);
                return (int)response.StatusCode;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PostOfflineBatch Exception " + ex.Message);
                return null;
            }
        }


        internal async Task<int> PutAdditionalGoals() // OK
        {
            LogWriter.LogWrite("PutAdditionalGoals()");
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

                    
                    string addItemJsonString = $"{{\"goals\":{appSettings.sAdditionalGoals}}}";
                       
                    if (!string.IsNullOrEmpty(addItemJsonString))
                    {
                        StringContent content = new StringContent(addItemJsonString, Encoding.UTF8, "application/json");

                        var response = await client.PutAsync("https://api.focusbear.io/user/long-term-goals", content);
                        if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                        {
                            if (response.Content != null)
                            {
                                LogWriter.LogWrite("PutAdditionalGoals() GOT Success");
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
                                LogWriter.LogWrite("PutAdditionalGoals() FAILED - " + sErrorMessage);
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
                LogWriter.LogWrite("PutAdditionalGoals WebException - " + ex.Message);

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PutAdditionalGoals Exception - " + ex.Message);

            }
            return 0;
        }

        // One-time migration of the local Brain Dump text to the backend note store (matches the mac
        // app's PUT {baseUrl}/note - is_brain_dump identifies it, title is just a required label since
        // the create path 400s without one). Not idempotent server-side (no id in the payload always
        // creates a new note), so the caller must only call this once per user - see
        // AppSettings.MigrateBrainDumpToBackendIfNeeded.
        internal async Task<bool> UpsertBrainDumpNote(string body)
        {
            LogWriter.LogWrite("UpsertBrainDumpNote()");
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
                            return false;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();

                    JObject payload = new JObject
                    {
                        ["title"] = "Brain dump",
                        ["body"] = body ?? "",
                        ["is_brain_dump"] = true
                    };
                    StringContent content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

                    var response = await client.PutAsync("https://api.focusbear.io/note", content);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        LogWriter.LogWrite("UpsertBrainDumpNote() GOT Success");
                        return true;
                    }
                    else
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            LogWriter.LogWrite("UpsertBrainDumpNote() FAILED - " + responseCode);
                        }
                        return false;
                    }
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("UpsertBrainDumpNote WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("UpsertBrainDumpNote Exception - " + ex.Message);
            }
            return false;
        }

        // Resolves a habit Instructions image's R2 object key (ActivityImage.FilePath) to an actual
        // downloadable URL (web_dashboard#2184). The returned URL is only valid for the response's
        // expiresInSeconds (currently 1 hour server-side) - fine here since the ongoing-activity screen
        // that calls this is never open anywhere near that long. url-only images never need this call.
        internal async Task<string> GetActivityImageReadUrl(string activityId, string fileKey)
        {
            LogWriter.LogWrite("GetActivityImageReadUrl()");
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
                            return null;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();

                    string apiPath = string.Format("https://api.focusbear.io/activity/{0}/images?file_key={1}", activityId, HttpUtility.UrlEncode(fileKey));

                    var response = await client.GetAsync(apiPath);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK && response.Content != null) //200
                    {
                        var responseCode = await response.Content.ReadAsStringAsync();
                        JObject resJson = JObject.Parse(responseCode);
                        string readUrl = resJson["readUrl"]?.ToString();
                        LogWriter.LogWrite("GetActivityImageReadUrl() GOT Success");
                        return readUrl;
                    }
                    else
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            LogWriter.LogWrite("GetActivityImageReadUrl() FAILED - " + responseCode);
                        }
                        return null;
                    }
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("GetActivityImageReadUrl WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetActivityImageReadUrl Exception - " + ex.Message);
            }
            return null;
        }

        internal async Task<int> ResetPassword() // OK
        {
            LogWriter.LogWrite("ResetPassword()");
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

                    //Built as JSON (an email is user text), and not logged: the log goes out with problem reports.
                    string addItemJsonString = new Newtonsoft.Json.Linq.JObject { ["email"] = appSettings.userAccount.email ?? "" }
                        .ToString(Newtonsoft.Json.Formatting.None);

                    if (!string.IsNullOrEmpty(addItemJsonString))
                    {
                        LogWriter.LogWrite("ResetPassword() request built");

                        StringContent content = new StringContent(addItemJsonString, Encoding.UTF8, "application/json");

                        //Make the data for Focus Mode
                        var response = await client.PostAsync("https://api.focusbear.io/auth/reset-password", content);
                        if (response.StatusCode == System.Net.HttpStatusCode.Created) //201
                        {
                            if (response.Content != null)
                            {
                                LogWriter.LogWrite("ResetPassword() GOT Success");
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
                                LogWriter.LogWrite("ResetPassword() FAILED" + sErrorMessage);
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
                LogWriter.LogWrite("ResetPassword WebException - " + ex.Message);

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ResetPassword Exception - " + ex.Message);

            }
            return 0;
        }

        // Holds the API's message from the last failed RequestPasswordReset call (e.g. "this account was
        // created with Google - sign in with Google instead") so the login screen can show the real reason
        // instead of a generic failure message (#1238). The response body is the message itself.
        public string sLastResetPasswordErrorMessage = "";

        internal async Task<int> RequestPasswordReset(string email)
        {
            LogWriter.LogWrite("RequestPasswordReset()");
            sLastResetPasswordErrorMessage = ""; //reset before each attempt
            try
            {
                bool nInternetOn = await IsInternetOn();
                if (nInternetOn)
                {
                    HttpClient client = new HttpClient();
                    string json = EmailBody(email);
                    StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync("https://api.focusbear.io/auth/reset-password", content);
                    if (response.StatusCode == System.Net.HttpStatusCode.Created) //201
                    {
                        LogWriter.LogWrite("RequestPasswordReset() success");
                        return 1;
                    }
                    else
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            LogWriter.LogWrite("RequestPasswordReset() failed - " + responseCode);
                            sLastResetPasswordErrorMessage = responseCode;
                        }
                        return 0;
                    }
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("RequestPasswordReset WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("RequestPasswordReset Exception - " + ex.Message);
            }
            return 0;
        }

        internal async Task<int> ResendConfirmationEmail() // OK
        {
            LogWriter.LogWrite("ResendConfirmationEmail()");
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

                    string addItemJsonString = EmailBody(appSettings.userAccount.email);

                    if (!string.IsNullOrEmpty(addItemJsonString))
                    {
                        StringContent content = new StringContent(addItemJsonString, Encoding.UTF8, "application/json");

                        //Make the data for Focus Mode
                        var response = await client.PostAsync("https://api.focusbear.io/auth/email-confirmation", content);
                        if (response.StatusCode == System.Net.HttpStatusCode.Created) //201
                        {
                            if (response.Content != null)
                            {
                                LogWriter.LogWrite("ResendConfirmationEmail() GOT Success");
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
                                LogWriter.LogWrite("ResendConfirmationEmail() FAILED" + sErrorMessage);
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
                LogWriter.LogWrite("ResendConfirmationEmail WebException - " + ex.Message);

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ResendConfirmationEmail Exception - " + ex.Message);

            }
            return 0;
        }

        // Converts the current guest (anonymous) account into a real account by setting the user's email
        // and password. Sends the current access token (via GetClientWithHeaders) so the backend links the
        // new credentials to the existing user id, preserving all their data. Returns 1 on success; on
        // failure returns 0 and stores the server message in appSettings.sConvertGuestError for display. #643
        internal async Task<int> ConvertGuestAccount(string sEmail, string sPassword)
        {
            LogWriter.LogWrite("ConvertGuestAccount()");
            appSettings.sConvertGuestError = "";
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

                    // Backend expects the new password under "newPassword" (not "password"). #643
                    string addItemJsonString = $"{{\"email\":{JsonConvert.SerializeObject(sEmail)},\"newPassword\":{JsonConvert.SerializeObject(sPassword)}}}";
                    StringContent content = new StringContent(addItemJsonString, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync("https://api.focusbear.io/auth/convert-guest", content);
                    if (response.StatusCode == System.Net.HttpStatusCode.Created || response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        LogWriter.LogWrite("ConvertGuestAccount() GOT Success");
                        return 1;
                    }
                    else
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            try
                            {
                                JObject resJson = JObject.Parse(responseCode);
                                appSettings.sConvertGuestError = resJson["message"]?.ToString() ?? "";
                            }
                            catch { }
                            LogWriter.LogWrite("ConvertGuestAccount() FAILED - " + appSettings.sConvertGuestError);
                        }
                        return 0;
                    }
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("ConvertGuestAccount WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ConvertGuestAccount Exception - " + ex.Message);
            }
            return 0;
        }

        internal async Task<int> GetUploadLogsURL() //OK
        {
            LogWriter.LogWrite("GetUploadLogsURL()");
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

                    var response = await client.GetAsync(UploadLogsURL);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            appSettings.sUploadURL = responseCode.ToString();

                            LogWriter.LogWrite("GetUploadLogsURL() GOT Success");

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
                            LogWriter.LogWrite("GetUploadLogsURL() FAILED - " + sErrorMessage);
                            return 0;
                        }
                        return 0;
                    }

                }
                else
                {
                    LogWriter.LogWrite("GetUploadLogsURL() FAILED - nInternetOn returned false / connectivity check timed out!");
                    return 2;
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("GetUploadLogsURL WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetUploadLogsURL Exception - " + ex.Message);
            }
            return 0;
        }

        internal async Task<int> NotifyUploadLogsSuccess(string sFeedback, string sUploadURL) //OK
        {
            LogWriter.LogWrite("NotifyUploadLogsSuccess()");
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

                    // The API rejects a blank feedback_message, so sharing logs failed when the user added
                    // no explanation and no screenshot (#1399). Send a default when it's empty.
                    string sFeedbackToSend = string.IsNullOrWhiteSpace(sFeedback) ? "No Feedback" : sFeedback;

                    string addItemJsonString = new JObject
                    {
                        ["feedback_message"] = HttpUtility.UrlEncode(sFeedbackToSend),
                        ["app_platform"] = "windows",
                        ["uploaded_file_url"] = (sUploadURL ?? "").Trim().Trim('"'),
                        ["app_version"] = appSettings.AppVersion.ToString(),
                    }.ToString(Formatting.None);

                    if (!string.IsNullOrEmpty(addItemJsonString))
                    {
                        StringContent content = new StringContent(addItemJsonString, Encoding.UTF8, "application/json");

                        var response = await client.PostAsync(NotifyUploadLogsSuccessURL, content);
                        if (response.StatusCode == System.Net.HttpStatusCode.Created) //201
                        {
                            LogWriter.LogWrite("NotifyUploadLogsSuccess() SUCCESS");

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
                                LogWriter.LogWrite("NotifyUploadLogsSuccess() FAILED - " + sErrorMessage);
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
                    LogWriter.LogWrite("NotifyUploadLogsSuccess() FAILED - nInternetOn returned false / connectivity check timed out!");
                    return 2;
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("NotifyUploadLogsSuccess WebException" + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("NotifyUploadLogsSuccess Exception" + ex.Message);
            }
            return 0;
        }

        //Blocking Schedule related
        internal async Task<int> GetAllBlockingSchedules() //OK
        {
            LogWriter.LogWrite("GetAllBlockingSchedules()");
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

                    string responsePath = BlockingSchedules;

                    var response = await client.GetAsync(responsePath);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            appSettings.sRawBlockingSchedules = responseCode.ToString();

                            LogWriter.LogWrite("GetAllBlockingSchedules() - " + FocusBear.Core.Reports.LogSafe.Describe(appSettings.sRawBlockingSchedules));

                            RegistryManager.SaveBlockingSchedules();
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
                LogWriter.LogWrite("GetAllBlockingSchedules - WebException" + ex.Message);

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetAllBlockingSchedules - Exception" + ex.Message);

            }
            return 0;
        }

        internal async Task<int> AddCustomBlockingSchedule(string addJsonString) // OK
        {
            LogWriter.LogWrite("AddCustomBlockingSchedule()");
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
 
                    if (!string.IsNullOrEmpty(addJsonString))
                    {
                        StringContent content = new StringContent(addJsonString, Encoding.UTF8, "application/json");

                        //Make the data for Focus Mode
                        // Pass device_id so the backend can skip re-notifying this device once the
                        // blocking-schedule-updated Pusher event ships (avoids a redundant self-refresh). #1114
                        string postPath = BlockingSchedules + "?device_id=" + appSettings.sDeviceID;
                        var response = await client.PostAsync(postPath, content);
                        if (response.StatusCode == System.Net.HttpStatusCode.Created) //201
                        {
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                LogWriter.LogWrite("AddCustomBlockingSchedule() GOT Success");
                                return 1;
                            }
                        }
                        else
                        {
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                //JObject resJson = JObject.Parse(responseCode);
                                //string sErrorMessage = resJson["message"].ToString();
                                LogWriter.LogWrite("AddCustomBlockingSchedule() FAILED ");
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
                LogWriter.LogWrite("AddCustomBlockingSchedule WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AddCustomBlockingSchedule Exception - " + ex.Message);
            }
            return 0;
        }

        internal async Task<int> UpdateCustomBlockingSchedule(string sBlockingScheduleID, string addJsonString) // OK
        {
            LogWriter.LogWrite("UpdateCustomBlockingSchedule()");
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

                    if (!string.IsNullOrEmpty(addJsonString))
                    {
                        StringContent content = new StringContent(addJsonString, Encoding.UTF8, "application/json");

                        string path = BlockingSchedules + "/" + sBlockingScheduleID + "?device_id=" + appSettings.sDeviceID;

                        //Make the data for Focus Mode
                        var response = await client.PutAsync(path, content);
                        if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                        {
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                LogWriter.LogWrite("UpdateCustomBlockingSchedule() GOT Success");
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
                                LogWriter.LogWrite("UpdateCustomBlockingSchedule() FAILED - " + sErrorMessage);
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
                LogWriter.LogWrite("UpdateCustomBlockingSchedule WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("UpdateCustomBlockingSchedule Exception - " + ex.Message);
            }
            return 0;
        }

        internal async Task<int> DeleteCustomBlockingSchedule(string sBlockingScheduleID) // 
        {
            LogWriter.LogWrite("DeleteCustomBlockingSchedule() API");
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

                    string path = BlockingSchedules + "/" + sBlockingScheduleID + "?device_id=" + appSettings.sDeviceID;

                    var response = await client.DeleteAsync(path);
                    if (response.StatusCode == System.Net.HttpStatusCode.NoContent) //204
                    {
                        LogWriter.LogWrite("DeleteCustomBlockingSchedule() GOT Success");
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
                            LogWriter.LogWrite("DeleteCustomBlockingSchedule() FAILED");
                            return 0;
                        }
                        return 0;
                    }

                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("DeleteCustomBlockingSchedule WebException" + ex.Message);

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DeleteCustomBlockingSchedule Exception" + ex.Message);

            }
            return 0;
        }

        internal async Task<int> GetAllTracks() //OK
        {
            LogWriter.LogWrite("GetAllTracks()");
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

                    string responsePath = FocusMusicTracks;

                    var response = await client.GetAsync(responsePath);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            appSettings.sRawFocusMusicTracks = responseCode.ToString();

                            LogWriter.LogWrite("GetAllTracks() - success");

                            RegistryManager.SaveFocusMusicTracks();
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
                LogWriter.LogWrite("GetAllTracks - WebException" + ex.Message);

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetAllTracks - Exception" + ex.Message);

            }
            return 0;
        }

        internal async Task<int> GetAllBuddies() //OK
        {
            LogWriter.LogWrite("GetAllBuddies()");
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

                    string responsePath = AccountabilityBuddies; 

                    var response = await client.GetAsync(responsePath);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            appSettings.sRawBuddiesList = responseCode.ToString();

                            LogWriter.LogWrite("GetAllBuddies() - " + FocusBear.Core.Reports.LogSafe.Describe(appSettings.sRawBuddiesList));

                            RegistryManager.SaveBuddyList();
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
                LogWriter.LogWrite("GetAllBuddies - WebException" + ex.Message);

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetAllBuddies - Exception" + ex.Message);

            }
            return 0;
        }

        internal async Task<int> RequestUnlock(string addJsonString) // OK
        {
            LogWriter.LogWrite("RequestUnlock()");
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

                    if (!string.IsNullOrEmpty(addJsonString))
                    {
                        StringContent content = new StringContent(addJsonString, Encoding.UTF8, "application/json");

                        string sPath = AccountabilityBuddies + "/" + "unlock-request";

                        //Make the data for Focus Mode
                        var response = await client.PostAsync(sPath, content);
                        if (response.StatusCode == System.Net.HttpStatusCode.Created) //201
                        {
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);
                                LogWriter.LogWrite("RequestUnlock() GOT Success");
                                return 1;
                            }
                        }
                        else
                        {
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                //JObject resJson = JObject.Parse(responseCode);
                                //string sErrorMessage = resJson["message"].ToString();
                                LogWriter.LogWrite("RequestUnlock() FAILED - " + responseCode);
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
                LogWriter.LogWrite("RequestUnlock WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("RequestUnlock Exception - " + ex.Message);
            }
            return 0;
        }

        internal async Task<int> GetAllUnlockRequests() //OK
        {
            LogWriter.LogWrite("GetAllUnlockRequests()");
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

                    string responsePath = AccountabilityBuddies + "/" + "unlock-requests";

                    var response = await client.GetAsync(responsePath);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            appSettings.sUnlockRequestsInfo = responseCode.ToString();

                            LogWriter.LogWrite("GetAllUnlockRequests() - " + appSettings.sUnlockRequestsInfo);
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
                LogWriter.LogWrite("GetAllUnlockRequests - WebException" + ex.Message);

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetAllUnlockRequests - Exception" + ex.Message);

            }
            return 0;
        }

        // Permanently deletes the user's account and all their data (Auth0, RevenueCat, Stripe, DB) via
        // DELETE /user-data — GDPR account deletion, NOT recoverable. Returns 1 on success, 0 otherwise.
        internal async Task<int> DeleteAccount()
        {
            LogWriter.LogWrite("DeleteAccount() API");
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

                    var response = await client.DeleteAsync(DeleteAccountUrl);
                    if (response.StatusCode == System.Net.HttpStatusCode.NoContent ||
                        response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        LogWriter.LogWrite("DeleteAccount() GOT Success");
                        return 1;
                    }
                    else
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            LogWriter.LogWrite("DeleteAccount() FAILED: " + responseCode);
                        }
                        return 0;
                    }
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("DeleteAccount WebException" + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DeleteAccount Exception" + ex.Message);
            }
            return 0;
        }

        //*********** Teacher Mode class session APIs (see TeacherSessionManager) ***********//

        //Student approves the join prompt - POST /class-session/{id}/join.
        //Returns the joined session (with allowed_apps/allowed_urls/scheduled_finish_time/allowlist_version)
        //because the caller needs it to build the enforced allow-list mode, or null on failure.
        internal async Task<JObject> JoinClassSession(string sSessionId)
        {
            LogWriter.LogWrite("JoinClassSession()");
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
                            return null;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();

                    StringContent content = new StringContent("{}", Encoding.UTF8, "application/json");

                    string sPath = ClassSession + "/" + sSessionId + "/join";

                    var response = await client.PostAsync(sPath, content);
                    if (response.StatusCode == System.Net.HttpStatusCode.Created || response.StatusCode == System.Net.HttpStatusCode.OK) //201 or 200
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            JObject resJson = JObject.Parse(responseCode);
                            LogWriter.LogWrite("JoinClassSession() GOT Success");
                            return resJson;
                        }
                        return null;
                    }
                    else
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            LogWriter.LogWrite("JoinClassSession() FAILED - " + responseCode);
                        }
                        return null;
                    }
                }
                else
                {
                    LogWriter.LogWrite("Is nInternetOn FALSE");
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("JoinClassSession WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("JoinClassSession Exception - " + ex.Message);
            }
            return null;
        }

        internal async Task<int> DeclineClassSession(string sSessionId, string sReason = "") //POST /class-session/{id}/decline
        {
            LogWriter.LogWrite("DeclineClassSession()");
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

                    //optional free-text decline reason the teacher sees in their report; empty stays "{}"
                    //for back-compatibility (the server rejects an empty body sent as application/json)
                    JObject payload = new JObject();
                    if (!string.IsNullOrWhiteSpace(sReason))
                    {
                        payload["reason"] = sReason.Trim();
                    }
                    StringContent content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");

                    string sPath = ClassSession + "/" + sSessionId + "/decline";

                    var response = await client.PostAsync(sPath, content);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK ||
                        response.StatusCode == System.Net.HttpStatusCode.Created ||
                        response.StatusCode == System.Net.HttpStatusCode.NoContent) //200 / 201 / 204
                    {
                        LogWriter.LogWrite("DeclineClassSession() GOT Success");
                        return 1;
                    }
                    else
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            LogWriter.LogWrite("DeclineClassSession() FAILED - " + responseCode);
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
                LogWriter.LogWrite("DeclineClassSession WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DeclineClassSession Exception - " + ex.Message);
            }
            return 0;
        }

        //GET /class-session/current - live session where the caller is invited/joined.
        //Powers the next-launch adoption; returns null when there is no live session for the user.
        internal async Task<JObject> GetCurrentClassSession()
        {
            LogWriter.LogWrite("GetCurrentClassSession()");
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
                            return null;
                        }
                    }

                    HttpClient client = GetClientWithHeaders();

                    string responsePath = ClassSession + "/" + "current";

                    var response = await client.GetAsync(responsePath);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            //the endpoint returns null/empty when there is no live session for the caller
                            if (string.IsNullOrWhiteSpace(responseCode) || responseCode.Trim() == "null")
                            {
                                return null;
                            }
                            try
                            {
                                JObject resJson = JObject.Parse(responseCode);
                                LogWriter.LogWrite("GetCurrentClassSession() GOT Success");
                                return resJson;
                            }
                            catch (Exception ex)
                            {
                                LogWriter.LogWrite("GetCurrentClassSession parse ex - " + ex.Message);
                                return null;
                            }
                        }
                        return null;
                    }
                    else
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            LogWriter.LogWrite("GetCurrentClassSession() FAILED - " + responseCode);
                        }
                        return null;
                    }
                }
                else
                {
                    LogWriter.LogWrite("Is nInternetOn FALSE");
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("GetCurrentClassSession WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetCurrentClassSession Exception - " + ex.Message);
            }
            return null;
        }

        //POST /class-session/{id}/heartbeat with the canonical body {in_focus, device_id, idle_seconds,
        //alignment_score?}. idle_seconds (seconds since last user input) lets the teacher report derive
        //Engagement server-side; alignment_score (0-100 site-relevance) is sent only when AI URL checking
        //is on (null omits it). Best effort every 25s - no IsInternetOn probe (it would double the request
        //volume) and no retry (the next beat is 25 seconds away; the server tolerates gaps of up to ~90s).
        internal async Task<int> ClassSessionHeartbeat(string sSessionId, bool bInFocus, int idleSeconds, int? alignmentScore = null)
        {
            try
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

                string addJsonString = "{\"in_focus\":" + (bInFocus ? "true" : "false")
                    + ",\"device_id\":\"" + appSettings.sDeviceID + "\""
                    + ",\"idle_seconds\":" + idleSeconds
                    + (alignmentScore.HasValue ? ",\"alignment_score\":" + alignmentScore.Value : "")
                    + "}";
                StringContent content = new StringContent(addJsonString, Encoding.UTF8, "application/json");

                string sPath = ClassSession + "/" + sSessionId + "/heartbeat";

                var response = await client.PostAsync(sPath, content);
                if (response.StatusCode == System.Net.HttpStatusCode.OK ||
                    response.StatusCode == System.Net.HttpStatusCode.Created ||
                    response.StatusCode == System.Net.HttpStatusCode.NoContent) //200 / 201 / 204
                {
                    return 1;
                }
                else
                {
                    LogWriter.LogWrite("ClassSessionHeartbeat() FAILED - StatusCode " + (int)response.StatusCode);
                    return 0;
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("ClassSessionHeartbeat WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ClassSessionHeartbeat Exception - " + ex.Message);
            }
            return 0;
        }

        //POST /class-session/{id}/unlock-requests {requested_app? / requested_domain? / reason?}
        internal async Task<int> RequestClassUnlock(string sSessionId, string sRequestedApp, string sRequestedDomain, string sReason)
        {
            LogWriter.LogWrite("RequestClassUnlock()");
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

                    //JObject serialization escapes any quotes/special characters in the user supplied strings
                    JObject payload = new JObject();
                    if (!string.IsNullOrWhiteSpace(sRequestedApp))
                    {
                        payload["requested_app"] = sRequestedApp;
                    }
                    if (!string.IsNullOrWhiteSpace(sRequestedDomain))
                    {
                        payload["requested_domain"] = sRequestedDomain;
                    }
                    if (!string.IsNullOrWhiteSpace(sReason))
                    {
                        payload["reason"] = sReason;
                    }
                    string addJsonString = payload.ToString(Newtonsoft.Json.Formatting.None);

                    StringContent content = new StringContent(addJsonString, Encoding.UTF8, "application/json");

                    string sPath = ClassSession + "/" + sSessionId + "/unlock-requests";

                    var response = await client.PostAsync(sPath, content);
                    if (response.StatusCode == System.Net.HttpStatusCode.Created || response.StatusCode == System.Net.HttpStatusCode.OK) //201 or 200
                    {
                        LogWriter.LogWrite("RequestClassUnlock() GOT Success");
                        return 1;
                    }
                    else
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            LogWriter.LogWrite("RequestClassUnlock() FAILED - " + responseCode);
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
                LogWriter.LogWrite("RequestClassUnlock WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("RequestClassUnlock Exception - " + ex.Message);
            }
            return 0;
        }

        internal async Task<int> GenerateOccupationRelevantSites(string sOccupation) //OK
        {
            LogWriter.LogWrite("GenerateOccupationRelevantSites()");
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

                    string addItemJsonString = OccupationBody(sOccupation);

                    if (!string.IsNullOrEmpty(addItemJsonString))
                    {
                        StringContent content = new StringContent(addItemJsonString, Encoding.UTF8, "application/json");

                        //Make the data for Focus Mode
                        var response = await client.PostAsync(OccupationSpecificRelevantSites, content);
                        if (response.StatusCode == System.Net.HttpStatusCode.Created) //201
                        {
                            if (response.Content != null)
                            {
                                LogWriter.LogWrite("GenerateOccupationRelevantSites() GOT Success");
                                var responseCode = await response.Content.ReadAsStringAsync();
                                appSettings.sGeneratedOccupationRelevantSites = responseCode;
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
                                LogWriter.LogWrite("GenerateOccupationRelevantSites() FAILED" + sErrorMessage);
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
                    LogWriter.LogWrite("Is nInternetOn FALSE");
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("GenerateOccupationRelevantSites - WebException" + ex.Message);

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GenerateOccupationRelevantSites - Exception" + ex.Message);

            }
            return 0;
        }

        internal async Task<int> GetFocusModes() //OK
        {
            LogWriter.LogWrite("GetFocusModes()");
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

                    var response = await client.GetAsync(FocusModes);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            appSettings.sFocusModes = responseCode.ToString();

                            LogWriter.LogWrite("GetFocusModes() GOT Success");

                            RegistryManager.SaveFocusModes();

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
                            LogWriter.LogWrite("GetFocusModes() FAILED - " + sErrorMessage);
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
                LogWriter.LogWrite("GetFocusModes WebException - " + ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetFocusModes Exception - " + ex.Message);
            }
            return 0;
        }

    }

}
 
