/*
 * Copyright (c) 2018 Wise Tech Labs Private Limited, All Rights Reserved.
 *
 * File: FocusBearAPIClient.cs
 * Project: SecureAntiMalware
 * User: Gaurav
 *
 * This document is the property of Wise Tech Labs Private Limited
 * It is considered confidential and proprietary.
 *
 * This document may not be reproduced or transmitted in any form, 
 * in whole or in part, without the express written permission of 
 * Wise Tech Labs Private Limited
 *
 */

using Focus_Bear.Models;
using Focus_Bear.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        /*https://api.focusbear.io/api/static/index.html#/*/

        AppSettings appSettings = AppSettings.GetInstance;

        public static string PostDevice { get { return "https://api.focusbear.io/device"; } }
        public static string UserSettings { get { return "https://api.focusbear.io/user-settings"; } }

        public static string CompletedActivity { get { return "https://api.focusbear.io/completed-activity"; } }
        public static string SkipActivity { get { return "https://api.focusbear.io/completed-activity/skip"; } }
        public static string CurrentActivityProps { get { return "https://api.focusbear.io/user/details/current-activity-props"; } }

        public static string GetUserDetails { get { return "https://api.focusbear.io/user/details"; } }
        
        public static string FocusMode { get { return "https://api.focusbear.io/focus-mode"; } }

        public static string GetUserLocalDeviceSettings { get { return "https://api.focusbear.io/user-local-device-settings"; } }

        //internal async Task<int> InstallKey(RequestLicense details)
        //{
        //    try
        //    {
        //        if (NetworkInterface.GetIsNetworkAvailable())
        //        {
        //            HttpClient client = new HttpClient();
        //            var detailsJson = details.ToJsonString();

        //            var cypherText = string.Empty;
        //            using (var crypto = new WtlCryptoServiceProvider())
        //            {
        //                cypherText = crypto.EncryptData(detailsJson);
        //            }

        //            if (!string.IsNullOrEmpty(cypherText))
        //            {
        //                StringContent content = new StringContent(cypherText, Encoding.UTF8, "application/json");
        //                var response = await client.PostAsync(AppInfo.InstallKey, content);
        //                if (response.StatusCode == System.Net.HttpStatusCode.OK)
        //                {
        //                    if (response.Content != null)
        //                    {
        //                        var responseCode = await response.Content.ReadAsStringAsync();
        //                        Debug.WriteLine("InstAct : " + responseCode);
        //                        return Convert.ToInt16(responseCode);
        //                        //return responseCode;
        //                    }
        //                }
        //            }
        //            else
        //            {
        //                return 0;
        //            }
        //        }
        //    }
        //    catch (WebException ex)
        //    {
        //        Debug.WriteLine(ex.Message);
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine(ex.Message);
        //    }
        //    return 0;
        //}

        internal async Task<int> PostActivityCompleted(string activity_id, string activity_sequence_id, DateTime start_time, int duration_logged, string choice_id, Double quantity_logged = -1, bool is_skipped = false)
        {
            LogWriter.LogWrite("PostActivityCompleted() = " + activity_id);
            try
            {
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
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
                        }
                        else
                        {
                            //token renewed failed
                            return 0;
                        }
                    }

                    HttpClient client = new HttpClient();

                    DateTime finish_time = DateTime.UtcNow;
                    if (is_skipped) //if activity is skipped just add one second to finish time
                    {
                        finish_time = finish_time.AddSeconds(1);
                    }
                    else
                    {
                        finish_time = finish_time.AddSeconds(duration_logged);
                    }
                    bool bTrue = true;
                    string metadata = $"{{\"skipped_did_complete\":{JsonConvert.SerializeObject(bTrue)}}}";

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

                    if( quantity_logged != -1)
                    {
                        addItemJsonString = addItemJsonString + $"\"quantity_logged\":{quantity_logged},";
                    }

                    //Remove last comma -  we have 100% added above
                    addItemJsonString = addItemJsonString.Remove(addItemJsonString.Length - 1, 1);
                    Console.WriteLine(addItemJsonString);

                    addItemJsonString = addItemJsonString + "}";
                   
                    if (!string.IsNullOrEmpty(addItemJsonString))
                    {
                        //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header
                        client.DefaultRequestHeaders.Add("platform", "Windows");
                        client.DefaultRequestHeaders.Add("app-version", appSettings.AppVersion.ToString());

                        StringContent content = new StringContent(addItemJsonString, Encoding.UTF8, "application/json");

                        if (!string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken))
                        {
                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appSettings.userAccount.accessToken);
                        }
                        else
                        {
                            return 0;
                        }

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
                            //Get Device ID Failed
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
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("PostActivityCompleted WebException" + ex.Message);

                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PostActivityCompleted Exception" + ex.Message);
                Debug.WriteLine(ex.Message);
            }
            return 0;
        }

        internal async Task<int> PostActivitySkipped(string activity_id, string activity_sequence_id)
        {
            LogWriter.LogWrite("PostActivitySkipped() = " + activity_id);
            try
            {
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
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
                        }
                        else
                        {
                            //token renewed failed
                            return 0;
                        }
                    }

                    HttpClient client = new HttpClient();

                  
                   
                    string addItemJsonString = $"{{\"activity_id\":\"{activity_id}\"," +
                        $"\"device_id\":\"{appSettings.sDeviceID}\"," +
                        $"\"activity_sequence_id\":\"{activity_sequence_id}\"}}";
           

                    if (!string.IsNullOrEmpty(addItemJsonString))
                    {
                        //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header
                        client.DefaultRequestHeaders.Add("platform", "Windows");
                        client.DefaultRequestHeaders.Add("app-version", appSettings.AppVersion.ToString());

                        StringContent content = new StringContent(addItemJsonString, Encoding.UTF8, "application/json");

                        if (!string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken))
                        {
                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appSettings.userAccount.accessToken);
                        }
                        else
                        {
                            return 0;
                        }

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
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("PostActivitySkipped WebException" + ex.Message);

                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PostActivitySkipped Exception" + ex.Message);
                Debug.WriteLine(ex.Message);
            }
            return 0;
        }

        internal async Task<int> ForceCompleteCurrentSequence(string activity_sequence_id, bool bTodaysActivity = false) //OK
        {
            LogWriter.LogWrite("ForceCompleteCurrentSequence() + " + activity_sequence_id);
            try
            {
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        //Renew the token and check again
                        LogWriter.LogWrite("Attempting RenewToken before API call");
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
                        }
                        else
                        {
                            //token renewed failed
                            return 0;
                        }
                    }

                    HttpClient client = new HttpClient();

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
                        //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header
                        client.DefaultRequestHeaders.Add("platform", "Windows");
                        client.DefaultRequestHeaders.Add("app-version", appSettings.AppVersion.ToString());

                        StringContent content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                        if (!string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken))
                        {
                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appSettings.userAccount.accessToken);
                        }
                        else
                        {
                            return 0;
                        }

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
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    if(!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        //Renew the token and check again
                        LogWriter.LogWrite("Attempting RenewToken before API call");
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
                        }
                        else
                        {
                            //token renewed failed
                            return 0;
                        }
                    }

                    HttpClient client = new HttpClient();
        
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
                        //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header
                        client.DefaultRequestHeaders.Add("platform", "Windows");
                        client.DefaultRequestHeaders.Add("app-version", appSettings.AppVersion.ToString());

                        StringContent content = new StringContent(jsonString, Encoding.UTF8, "application/json");
                     
                        if (!string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken))
                        {
                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appSettings.userAccount.accessToken);
                        }
                        else
                        {
                            return 0;
                        }
                       
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

        //FoucsModes
        internal async Task<int> AddFocusMode(FocusMode FF) // OK
        {
            LogWriter.LogWrite("AddFocusMode()");
            try
            {
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        //Renew the token and check again
                        LogWriter.LogWrite("Attempting RenewToken before API call");
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
                        }
                        else
                        {
                            //token renewed failed
                            return 0;
                        }
                    }

                    HttpClient client = new HttpClient();

                    /*  Metadata
                    public ObservableCollection<string> arrBlockedUrls = new ObservableCollection<string>();
                    //Metadata part
                    public ObservableCollection<string> arrAllowedAppsWin = new ObservableCollection<string>();
        
                    public string color { get; set; }
                    public bool isOn;
                    public bool isAllowedAllApps;
                    public bool isAllowedListApproach;
                    public bool isAllowedAllUrls;
                    public bool isBlockedAllUrls;
                    public bool isDefault;
                    public bool isMeetings;
                    public bool isLockedFocus;
                        */

                    //Remove # from color
                   string color = FF.color.Replace("#", "");

                   string metadata = $"{{\"blockUrlsArray\":{JsonConvert.SerializeObject(FF.arrBlockedUrls.ToArray<string>())}," +
                   $"\"allowed_apps_win\":{JsonConvert.SerializeObject(FF.arrAllowedAppsWin.ToArray<AppDetails>())}," +
                   $"\"color\":\"{color}\"," +
                   $"\"isAllowedAllApps\":{JsonConvert.SerializeObject(FF.isAllowedAllApps)}," +
                   $"\"isAllowedListApproach\":{JsonConvert.SerializeObject(FF.isAllowedListApproach)}," +
                   $"\"isAllowedAllUrls\":{JsonConvert.SerializeObject(FF.isAllowedAllUrls)}," +
                   $"\"isBlockedAllUrls\":{JsonConvert.SerializeObject(FF.isBlockedAllUrls)}}}";


                    string addFocusJsonString = $"{{\"id\":\"{FF.id}\"," +
                        $"\"name\":\"{FF.name}\"," +
                        $"\"allowed_urls\":{JsonConvert.SerializeObject(FF.arrAllowedUrls.ToArray<string>())}," +
                        $"\"metadata\":{metadata}}}" ;


                    if (!string.IsNullOrEmpty(addFocusJsonString))
                    {
                        //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header
                        client.DefaultRequestHeaders.Add("platform", "Windows");
                        client.DefaultRequestHeaders.Add("app-version", appSettings.AppVersion.ToString());

                        StringContent content = new StringContent(addFocusJsonString, Encoding.UTF8, "application/json");

                        if (!string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken))
                        {
                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appSettings.userAccount.accessToken);
                        }
                        else
                        {
                            return 0;
                        }

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
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        //Renew the token and check again
                        LogWriter.LogWrite("Attempting RenewToken before API call");
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
                        }
                        else
                        {
                            //token renewed failed
                            return 0;
                        }
                    }

                    HttpClient client = new HttpClient();
 
 
                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header
                    client.DefaultRequestHeaders.Add("platform", "Windows");
                    client.DefaultRequestHeaders.Add("app-version", appSettings.AppVersion.ToString());

                    // StringContent content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                    if (!string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken))
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appSettings.userAccount.accessToken);
                    }
                    else
                    {
                        return 0;
                    }
                    string path = FocusMode + "?id=" + sFocusModeId;

                    var response = await client.DeleteAsync(path);
                    if (response.StatusCode == System.Net.HttpStatusCode.NoContent) //204
                    {
                        //if (response.Content != null)
                       // {
                          //  var responseCode = await response.Content.ReadAsStringAsync();
                            //JObject resJson = JObject.Parse(responseCode);
                         

                            LogWriter.LogWrite("DeleteFocusMode() GOT Success");
                            return 1;
                       // }
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
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        //Renew the token and check again
                        LogWriter.LogWrite("Attempting RenewToken before API call");
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
                        }
                        else
                        {
                            //token renewed failed
                            return 0;
                        }
                    }

                    HttpClient client = new HttpClient();

                    /*  Metadata
                    public ObservableCollection<string> arrBlockedUrls = new ObservableCollection<string>();
                    //Metadata part
                    public ObservableCollection<string> arrAllowedAppsWin = new ObservableCollection<string>();
        
                    public string color { get; set; }
                    public bool isOn;
                    public bool isAllowedAllApps;
                    public bool isAllowedListApproach;
                    public bool isAllowedAllUrls;
                    public bool isBlockedAllUrls;
                    public bool isDefault;
                    public bool isMeetings;
                    public bool isLockedFocus;
                        */

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
                        $"\"isAllowedAllUrls\":{JsonConvert.SerializeObject(FF.isAllowedAllUrls)}," +
                        $"\"isDefault\":{JsonConvert.SerializeObject(FF.isDefault)}," +
                        $"\"isMeetings\":{JsonConvert.SerializeObject(FF.isMeetings)}," +
                        $"\"isLockedFocus\":{JsonConvert.SerializeObject(FF.isLockedFocus)}," +
                        $"\"isBlockedAllUrls\":{JsonConvert.SerializeObject(FF.isBlockedAllUrls)}}}";


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
                        //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header
                        client.DefaultRequestHeaders.Add("platform", "Windows");
                        client.DefaultRequestHeaders.Add("app-version", appSettings.AppVersion.ToString());

                        StringContent content = new StringContent(arrAllFocusModesArray, Encoding.UTF8, "application/json");

                        if (!string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken))
                        {
                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appSettings.userAccount.accessToken);
                        }
                        else
                        {
                            return 0;
                        }

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
            LogWriter.LogWrite("StartFocusMode()");
            try
            {
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        //Renew the token and check again
                        LogWriter.LogWrite("Attempting RenewToken before API call");
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
                        }
                        else
                        {
                            //token renewed failed
                            return 0;
                        }
                    }

                    HttpClient client = new HttpClient();


                    DateTime start = DateTime.Parse(appSettings.sRunningFocusModeStartTime).ToUniversalTime();
                    DateTime end = DateTime.Parse(appSettings.sRunningFocusModeEndTime).ToUniversalTime();
    
                    string addFocusJsonString = $"{{\"intention\":\"{ appSettings.sRunningFocusModeIntention}\"," +
                        $"\"finish_time\":\"{end.ToString("o")}\"," +
                        $"\"start_time\":\"{start.ToString("o")}\"}}";
                    

                    if (!string.IsNullOrEmpty(addFocusJsonString))
                    {
                        //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header
                        client.DefaultRequestHeaders.Add("platform", "Windows");
                        client.DefaultRequestHeaders.Add("app-version", appSettings.AppVersion.ToString());

                        StringContent content = new StringContent(addFocusJsonString, Encoding.UTF8, "application/json");

                        if (!string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken))
                        {
                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appSettings.userAccount.accessToken);
                        }
                        else
                        {
                            return 0;
                        }

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
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        //Renew the token and check again
                        LogWriter.LogWrite("Attempting RenewToken before API call");
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
                        }
                        else
                        {
                            //token renewed failed
                            return 0;
                        }
                    }

                    HttpClient client = new HttpClient();
 
                    DateTime end = DateTime.UtcNow;

                    string addFocusJsonString = $"{{\"achievements\":\"{achievements}\"," +
                        $"\"distractions\":\"{distractions}\"," +
                        $"\"finish_time\":\"{end.ToString("o")}\"}}";


                    if (!string.IsNullOrEmpty(addFocusJsonString))
                    {
                        //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header
                        client.DefaultRequestHeaders.Add("platform", "Windows");
                        client.DefaultRequestHeaders.Add("app-version", appSettings.AppVersion.ToString());

                        StringContent content = new StringContent(addFocusJsonString, Encoding.UTF8, "application/json");

                        if (!string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken))
                        {
                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appSettings.userAccount.accessToken);
                        }
                        else
                        {
                            return 0;
                        }

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

        internal async Task<int> GetUserSettings() //OK
        {
            LogWriter.LogWrite("GetUserSettings()");
            try
            {
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        //Renew the token and check again
                        LogWriter.LogWrite("Attempting RenewToken before API call");
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
                        }
                        else
                        {
                            //token renewed failed
                            return 0;
                        }
                    }

                    HttpClient client = new HttpClient();
                     
                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header
                    client.DefaultRequestHeaders.Add("platform", "Windows");
                    client.DefaultRequestHeaders.Add("app-version", appSettings.AppVersion.ToString());

                    if (!string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken))
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appSettings.userAccount.accessToken);
                    }
                    else
                    {
                        LogWriter.LogWrite("string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken return 0;");
                        return 0;
                    }

                    var response = await client.GetAsync(UserSettings);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            //JObject resJson = JObject.Parse(responseCode);
                            //appSettings.sDeviceID = resJson["id"].ToString();
                            appSettings.sRawUserSettings = responseCode.ToString();
                            
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
                            //JObject resJson = JObject.Parse(responseCode);
                            //string sErrorMessage = resJson["message"].ToString();
                            return 0;
                        }
                        return 0;
                    }
                    
                }
                else
                {
                    LogWriter.LogWrite("NetworkInterface.GetIsNetworkAvailable() FALSE");
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

        internal async Task<int> PutUserSettings() //OK
        {
            LogWriter.LogWrite("PutUserSettings()");
            try
            {
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        //Renew the token and check again
                        LogWriter.LogWrite("Attempting RenewToken before API call");
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
                        }
                        else
                        {
                            //token renewed failed
                            return 0;
                        }
                    }

                    HttpClient client = new HttpClient();

                    // dummy payload
                    String jsonString = appSettings.sRawUserSettings;

                    if (!string.IsNullOrEmpty(jsonString))
                    {
                        //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header
                        client.DefaultRequestHeaders.Add("platform", "Windows");
                        client.DefaultRequestHeaders.Add("app-version", appSettings.AppVersion.ToString());

                        StringContent content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                        if (!string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken))
                        {
                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appSettings.userAccount.accessToken);
                        }
                        else
                        {
                            return 0;
                        }

                        var response = await client.PutAsync(UserSettings, content);
                        if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                        {
                            if (response.Content != null)
                            {
                                var responseCode = await response.Content.ReadAsStringAsync();
                                JObject resJson = JObject.Parse(responseCode);

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
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        //Renew the token and check again
                        LogWriter.LogWrite("Attempting RenewToken before API call");
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
                        }
                        else
                        {
                            //token renewed failed
                            return 0;
                        }
                    }

                    HttpClient client = new HttpClient();

                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header
                    client.DefaultRequestHeaders.Add("platform", "Windows");
                    client.DefaultRequestHeaders.Add("app-version", appSettings.AppVersion.ToString());

                    if (!string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken))
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appSettings.userAccount.accessToken);
                    }
                    else
                    {
                        LogWriter.LogWrite("string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken return 0;");
                        return 0;
                    }

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
                        //GetUserSettings Failed
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
                    LogWriter.LogWrite("NetworkInterface.GetIsNetworkAvailable() FALSE");
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
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        //Renew the token and check again
                        LogWriter.LogWrite("Attempting RenewToken before API call");
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
                        }
                        else
                        {
                            //token renewed failed
                            return 0;
                        }
                    }

                    HttpClient client = new HttpClient();

                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header
                    client.DefaultRequestHeaders.Add("platform", "Windows");
                    client.DefaultRequestHeaders.Add("app-version", appSettings.AppVersion.ToString());

                    if (!string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken))
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appSettings.userAccount.accessToken);
                    }
                    else
                    {
                        LogWriter.LogWrite("string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken return 0;");
                        return 0;
                    }

                    var response = await client.GetAsync(GetUserDetails);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();

                            appSettings.sUserDetailsInfo = responseCode.ToString();

                            LogWriter.LogWrite("GetUserDetailsInfo() GOT Success");

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
                            LogWriter.LogWrite("GetUserDetailsInfo() FAILED - " + sErrorMessage);
                            return 0;
                        }
                        return 0;
                    }

                }
                else
                {
                    LogWriter.LogWrite("NetworkInterface.GetIsNetworkAvailable() FALSE");
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
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        //Renew the token and check again
                        LogWriter.LogWrite("Attempting RenewToken before API call");
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
                        }
                        else
                        {
                            //token renewed failed
                            return 0;
                        }
                    }

                    HttpClient client = new HttpClient();

                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header
                    client.DefaultRequestHeaders.Add("platform", "Windows");
                    client.DefaultRequestHeaders.Add("app-version", appSettings.AppVersion.ToString());

                    if (!string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken))
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appSettings.userAccount.accessToken);
                    }
                    else
                    {
                        LogWriter.LogWrite("string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken return 0;");
                        return 0;
                    }

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
                    LogWriter.LogWrite("NetworkInterface.GetIsNetworkAvailable() FALSE");
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
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        //Renew the token and check again
                        LogWriter.LogWrite("Attempting RenewToken before API call");
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
                        }
                        else
                        {
                            //token renewed failed
                            return 0;
                        }
                    }

                    HttpClient client = new HttpClient();

                    string addItemJsonString = SettingsJSON.ToString();

                    if (!string.IsNullOrEmpty(addItemJsonString))
                    {
                        //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header
                        client.DefaultRequestHeaders.Add("platform", "Windows");
                        client.DefaultRequestHeaders.Add("app-version", appSettings.AppVersion.ToString());

                        StringContent content = new StringContent(addItemJsonString, Encoding.UTF8, "application/json");

                        if (!string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken))
                        {
                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appSettings.userAccount.accessToken);
                        }
                        else    
                        {
                            return 0;
                        }

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
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        //Renew the token and check again
                        LogWriter.LogWrite("Attempting RenewToken before API call");
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
                        }
                        else
                        {
                            //token renewed failed
                            return 0;
                        }
                    }

                    HttpClient client = new HttpClient();

                    string apiPath = string.Format("https://api.focusbear.io/completed-activity/{0}/stats?days_number=30&timezone=UTC", (activityChoiceId.Length > 0 ? activityChoiceId : activityId));

                    LogWriter.LogWrite("GetActivityStats() URL - " + apiPath);

                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header
                    client.DefaultRequestHeaders.Add("platform", "Windows");
                    client.DefaultRequestHeaders.Add("app-version", appSettings.AppVersion.ToString());

                    if (!string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken))
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appSettings.userAccount.accessToken);
                    }
                    else
                    {
                        LogWriter.LogWrite("string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken return 0;");
                        return 0;
                    }

                    var response = await client.GetAsync(apiPath);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            //JObject resJson = JObject.Parse(responseCode);
                            //appSettings.sDeviceID = resJson["id"].ToString();
                            appSettings.sOneMonthActivityDataForGraph = responseCode.ToString();

                            LogWriter.LogWrite("GetActivityStats() GOT Success");

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
                            LogWriter.LogWrite("GetActivityStats() FAILED - " + sErrorMessage);
                            return 0;
                        }
                        return 0;
                    }

                }
                else
                {
                    LogWriter.LogWrite("NetworkInterface.GetIsNetworkAvailable() FALSE");
                }
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite("GetActivityStats WebException + " + ex.Message);
 
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetActivityStats WebException + " + ex.Message);
 
            }
            return 0;
        }

        //mainly used for Break activities where current day stats matter
        internal async Task<int> GetActivityCurrentDayStats(string activityId, string activityChoiceId)   //OK
        {
            LogWriter.LogWrite("GetActivityCurrentDayStats()");
            try
            {
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        //Renew the token and check again
                        LogWriter.LogWrite("Attempting RenewToken before API call");
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
                        }
                        else
                        {
                            //token renewed failed
                            return 0;
                        }
                    }

                    HttpClient client = new HttpClient();

                    TimeSpan tsShutOffTime = TimeSpan.Parse(appSettings.sDayShutOffTime);

                    DateTime now = DateTime.Now;
                    DateTime myShutOffTime = new DateTime(now.Year, now.Month, now.Day, tsShutOffTime.Hours, tsShutOffTime.Minutes, 00).ToUniversalTime();
                    Console.WriteLine("{0}", myShutOffTime.ToString());

                    TimeSpan tsStartTime = TimeSpan.Parse(appSettings.sDayStartTime);
                    DateTime myStartTime = new DateTime(now.Year, now.Month, now.Day, tsStartTime.Hours, tsStartTime.Minutes, 00).ToUniversalTime();
                    Console.WriteLine("{0}", myStartTime.ToString());


                    string apiPath = string.Format("https://api.focusbear.io/completed-activity/{0}?from_time={1}&to_time={2}", (activityChoiceId.Length > 0 ? activityChoiceId : activityId) , myStartTime.ToString("o"), myShutOffTime.ToString("o"));

                    LogWriter.LogWrite("GetActivityCurrentDayStats() URL - " + apiPath);

                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header
                    client.DefaultRequestHeaders.Add("platform", "Windows");
                    client.DefaultRequestHeaders.Add("app-version", appSettings.AppVersion.ToString());

                    if (!string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken))
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appSettings.userAccount.accessToken);
                    }
                    else
                    {
                        LogWriter.LogWrite("string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken return 0;");
                        return 0;
                    }

                    var response = await client.GetAsync(apiPath);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();
                            //JObject resJson = JObject.Parse(responseCode);
                            //appSettings.sDeviceID = resJson["id"].ToString();
                            appSettings.sCurrentDayActivityDataForGraph = responseCode.ToString();

                            LogWriter.LogWrite("GetActivityCurrentDayStats() GOT Success");

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
                            LogWriter.LogWrite("GetActivityCurrentDayStats() FAILED - " + sErrorMessage);
                            return 0;
                        }
                        return 0;
                    }

                }
                else
                {
                    LogWriter.LogWrite("NetworkInterface.GetIsNetworkAvailable() FALSE");
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

        internal async Task<int> GetActivityAllDaySummary()   //OK
        {
            LogWriter.LogWrite("GetActivityAllDaySummary()");
            try
            {
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        //Renew the token and check again
                        LogWriter.LogWrite("Attempting RenewToken before API call");
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
                        }
                        else
                        {
                            //token renewed failed
                            return 0;
                        }
                    }

                    HttpClient client = new HttpClient();
                    var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
                    //UTC+05:30 // UTC%2B5:30
                    string sign = offset < TimeSpan.Zero ? "-" : "+";
                    sign = HttpUtility.UrlEncode(sign);
                    string time = "UTC" + sign + offset.ToString(@"hh\:mm");

                    string apiPath = string.Format("https://api.focusbear.io/completed-activity/day-summary?timezone={0}", time);

                    LogWriter.LogWrite("GetActivityAllDaySummary() URL - " + apiPath);

                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header
                    client.DefaultRequestHeaders.Add("platform", "Windows");
                    client.DefaultRequestHeaders.Add("app-version", appSettings.AppVersion.ToString());

                    if (!string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken))
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appSettings.userAccount.accessToken);
                    }
                    else
                    {
                        LogWriter.LogWrite("string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken return 0;");
                        return 0;
                    }

                    var response = await client.GetAsync(apiPath);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK) //200
                    {
                        if (response.Content != null)
                        {
                            var responseCode = await response.Content.ReadAsStringAsync();

                            appSettings.sActivityAllDaySummaryData = responseCode.ToString();

                            LogWriter.LogWrite("GetActivityAllDaySummary() GOT Success");

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
                            LogWriter.LogWrite("GetActivityAllDaySummary() FAILED - " + sErrorMessage);
                            return 0;
                        }
                        return 0;
                    }

                }
                else
                {
                    LogWriter.LogWrite("NetworkInterface.GetIsNetworkAvailable() FALSE");
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

        internal async Task<int> CapturePostHog(string sEvent, string sMetadata) // OK
        {
            LogWriter.LogWrite("CapturePostHog()");
            try
            {
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                  
                    HttpClient client = new HttpClient();

                    /*  Metadata
                        let json: [String: Any] = [
                    "api_key": "phc_Y6hD2dvO1zylm7CLij5CuNt6S5gTxe7EVXoxZcb2xUm",
                    "event": event,
                    "context": [
                    "os": "MacOS",
                            "osVersion": macOSVersion,
                            "appVersion": appVersion
                        ],
                        "properties": properties
                    ]
                     var properties = [String:Any]()
            
                        properties["distinct_id"] = USER_ID
                        properties["USES_WINDOWS_APP"] = true
                        for key in metadata.keys{
                            properties[key] = metadata[key]
                        }

                    example:
                        {
                        "api_key": "phc_Y6hD2dvO1zylm7CLij5Expletive6S5gTxe7EVXoxZcb2xUm",
                        "event": "deactivate-routine",
                        "context": {
                            "os": "MacOS",
                            "osVersion": "16.0.1"
                        },
                        "properties": {
                            "distinct_id": USER_ID,
                            "USES_WINDOWS_APP" = true,
                            "routine-type": "morning_routine",
                            "deactivate-reason": "app is broken",
                            "detailed-reason": "Still shows morning routine after I finished morning routine"
                        }
                    } 

                        */
                    string sOSVersion = appSettings.sOSVersion;
                    bool bUsesWindowsApp = true;
                    string sID = (appSettings.sUser_id != null && appSettings.sUser_id.Length > 0) ? appSettings.sUser_id : appSettings.sMacAddress;
                    string context = $"{{\"os\":\"windows\"," +
                                     $"\"osVersion\":\"{sOSVersion}\"," +
                                     $"\"appVersion\":\"{appSettings.AppVersion.ToString()}\"";
                    if(sMetadata.Length > 0)
                    {
                        context += ",";
                        context += sMetadata;
                    }
                    context += "}";

                    string properties = $"{{\"distinct_id\":\"{sID}\"," +
                    $"\"USES_WINDOWS_APP\":{JsonConvert.SerializeObject(bUsesWindowsApp)}}}";


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
                                LogWriter.LogWrite("CapturePostHog() GOT Success");
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
            LogWriter.LogWrite("CaptureEventEndpoint()");
            try
            {
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    if (!appSettings.ValidateTokenValidBeforeAPICall())
                    {
                        //Renew the token and check again
                        LogWriter.LogWrite("Attempting RenewToken before API call");
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
                        }
                        else
                        {
                            //token renewed failed
                            return 0;
                        }
                    }

                    HttpClient client = new HttpClient();

                    /*{
                        "event_type": "completed-onboarding",
                        "user_properties": {
                            "FIRSTNAME": "Thomas",
                            "LASTNAME": "Bianchi"
                        },
                        "event_data": {
                            "id": "1234",
                            "data": {
                                "total": 20,
                                "currency": "USD",
                                "country": "USA"
                            }
                        }
                    }
                     */

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
                         $"\"USES_WINDOWS_APP\":{JsonConvert.SerializeObject(bUsesWindowsApp)}}}";


                    string sJsonString = $"{{\"event_type\":\"{sEvent}\"," +
                        $"\"event_data\":{event_data}," +
                        $"\"user_properties\":{properties}}}";



                    if (!string.IsNullOrEmpty(sJsonString))
                    {
                        //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));//ACCEPT header
                        client.DefaultRequestHeaders.Add("platform", "Windows");
                        client.DefaultRequestHeaders.Add("app-version", appSettings.AppVersion.ToString());

                        StringContent content = new StringContent(sJsonString, Encoding.UTF8, "application/json");

                        if (!string.IsNullOrWhiteSpace(appSettings.userAccount.accessToken))
                        {
                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appSettings.userAccount.accessToken);
                        }
                        else
                        {
                            return 0;
                        }

                        //Make the data for Focus Mode
                        var response = await client.PostAsync("https://api.focusbear.io/events", content);
                        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) //204
                        {
                            if (response.Content != null)
                            {
                                //var responseCode = await response.Content.ReadAsStringAsync();
                                //JObject resJson = JObject.Parse(responseCode);
                                LogWriter.LogWrite("CaptureEventEndpoint() GOT Success");
                                return 1;
                            }
                        }
                        else
                        {
                            //Get Device ID Failed
                            if (response.Content != null)
                            {
                                //var responseCode = await response.Content.ReadAsStringAsync();
                                //JObject resJson = JObject.Parse(responseCode);
                                //string sErrorMessage = resJson["message"].ToString();
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
    }

}
 
