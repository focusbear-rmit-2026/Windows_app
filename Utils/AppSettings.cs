using Focus_Bear.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Focus_Bear.Utils;
using System.Windows;
using Focus_Bear.API;
using Interop.UIAutomationClient;
using FocusBear.Utils;
using System.Reflection;
using System.Collections.ObjectModel;
using Focus_Bear.Habits;
using Focus_Bear.Focus;
using System.Windows.Threading;
using System.Threading;

namespace Focus_Bear.Utils          
{
    public class AppSettings
    {
        private static readonly AppSettings instance = new AppSettings();
        public static AppSettings GetInstance
        {
            get
            {
                return instance;
            }
        }

        //Local Path
        public static string AppDataDirPath
        {
            get
            {
                var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\" + "Focus Bear";
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    DirectoryInfo dirInfo = new DirectoryInfo(path);
                    dirInfo.Attributes = FileAttributes.Hidden;
                }
                return path;
            }
        }
        public static string AppDataDirLogsPath
        {
            get
            {
                var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\" + "Focus Bear\\Logs";
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    DirectoryInfo dirInfo = new DirectoryInfo(path);
                    dirInfo.Attributes = FileAttributes.Hidden;
                }
                return path;
            }
        }

        //App Update
        public static string AppIniFileURL { get { return "https://downloads.focusbear.io/update/windows/update.ini"; } }
        public static readonly string AppUpdateINIPath = AppDataDirPath + "\\update.ini";

        public static readonly string FocusBlockPagePath = AppDataDirPath + "\\focus-block.html";

        public bool bRestartAppNeeded = false;
        public string sMacAddress { get; set; }
        public string sOSVersion { get; set; }
        public Version AppVersion { get; set; }
        public CurrentActivityType nCurrentActivityType { get; set; }
        public int nHabitIndex; //This is used to track the activity number from the activities array
        public DateTime dLastActivityPerformedStartTime; //This is the start time of the Last activity recently started.
        public int nTrialDays { get; set; }
        public bool isPurchased { get; set; }
        public bool isExpired { get; set; }
        public bool IsLoggedIn { get; set; }
        public bool IsFirstInstall { get; set; }
        public DateTime FirstAppInstallDT { get; set; }

        public bool bHasUserEditedSettings = false;
        public DateTime InitialStartupTimeValue { get; set; }
        public DateTime InitialShutoffTimeValue { get; set; }

        public FocusMode FocusModeNameToCreateWhileOnBoarding;
        public string OnboardingActivityName = "";
        public int OnboardingActivityDurationSeconds;
        public string OnboardingVideoUrl = "";
        public bool bOnboardingActivitySkipped = false;

        public bool bGoalHealthyHabits { get; set; }
        public bool bGoalStayFocused { get; set; }
        public bool bExitFullScreenClickedWhileOnboarding { get; set; }
        public DateTime MorningHabitsShownDate { get; set; }
        public DateTime EveningHabitsShownTillDate { get; set; }

        public DateTime FocusSummaryShownTillDate { get; set; }
        public DateTime ShutOFFAbortedTillDate { get; set; }

        public DateTime AppDeactivatedTillDate { get; set; }

        public string CurrentRunningSequenceID { get; set; }

        public string sLangCode = "en";

        public bool bBlockingWorkedWhileOnboarding = false;
        //API infos Related
        public string sDeviceID;
        public string sRawUserSettings;
        public string sLastActivityDoneInfo; //Last activity infor from current-activity-props api
        public string sRawHabitPacks;
        public string sHabitPackIdToInstall;
        public string sMorningRoutineDuration;
        public string sEveningRoutineDuration;
        public int BreaksTimeInterval = 20;

        public string sUserDetailsInfo; //All user details from API
        public string sUser_id;
        public string sUser_name;
        public string sUser_email;

        public string sUserLocalDeviceSettings; // All local user device settings.


        public string sOneMonthActivityDataForGraph;
        public string sCurrentDayActivityDataForGraph;


        public string sActivityAllDaySummaryData;

        public bool isAppEnable = true;
        public string sDayStartTime;
        public string sDayShutOffTime;

        //Habits Realted
        public int currentHabitIndex = -1;
        public int currentActivityIndex = -1;
        public breakActivity currentActivityPlaying;
        public habit currentHabitPlaying;

        public List<habit> arrAllMorningHabits = new List<habit>();
        public List<habit> arrAllEveningHabits = new List<habit>();

        public bool bHabitwithAllowedAppsRunning = false;
        public bool bHabitwithUseFocusModeRunning = false;
        public string sUseFocusModeIDInActivity = "";
        public string sCurrentRunningActivity = "";
        public string sHabitTypeForUseFocusModeIDInActivity = "";//evening-habit, morning-habit, break-activity

        public bool bShowActivitiesFromMid = false;

        public bool bHabitsPostponedState = false; //variable to handle the Resume Habits menu item

        public JArray arraySkipped = new JArray();
        public bool bShowActivitiesFromMidLastSkipped = false;
        public bool bSkipCurrentActivity = false;
        public bool bSkipCurrentActivityCantDoItToday = false;
        public List<SKIPPED_ACTIVITY> indexSkippedActivities = new List<SKIPPED_ACTIVITY>();
        public bool bSkipWhileActivityOngoing = false;

        public List <BlankWindow> SecondaryWindows = new List<BlankWindow>();

        //Breaks Related
        public string[] activityAllowedFocusApps;
        public string[] activityAllowedFocusUrls;
        public bool isAnyMandatoryActivityIncluded;
        public bool isMandatoryActivityGoing;
        public int breakAfterMinutes;
        public int nCountBreaksPostponedTimes;
        //public bool bBreakWarningWindowVisible = false;

        public List<breakActivity> arrAllbreakActivities = new List<breakActivity>();
        public List<breakActivity> arrActivitiesIncludeInEveryBreak = new List<breakActivity>();
        public List<breakActivity> arrActivitiesExcludeInEveryBreak = new List<breakActivity>();

        public List<breakActivity> arrBreakActivitiesTODOInOrder = new List<breakActivity>();

        public activityChoice currentActivityChoice;

        public List<LastShownActivityChoice> arrLastShownRandomChoice = new List<LastShownActivityChoice>();

        public List<AppDetails> InstalledApps = new List<AppDetails>();
        /// 


        public string sFocusModeTemplates; //All FOcus Mode templates  from API

        //public List<FocusMode> arrFocusModes = new List<FocusMode>();
        public ObservableCollection<FocusMode> arrFocusModes = new ObservableCollection<FocusMode>();

        public IAccessible FirefoxLastAccWindow = null;
        public object objChild = null; 
        public string sLastActiveURLInFirefox = "";

        public bool bShowOnBoardingVideo = false;

        public string sLastActiveAppName = "";
        public string sLastActiveURL = "";

        public string sBrainDump = "";

        public bool isFocusModeRunning = false;
        public string sRunningFocusModeID = "";
        public string sRunningFocusModeName = "";
        public string sRunningFocusModeStartTime = "";
        public string sRunningFocusModeEndTime = "";
        public string sRunningFocusModeIntention = "";
        public bool bSkipMicroBreaksDuringFocus = false;
        public string sChatGPTReasonForCurrentURL = "";
        public double dProbablity = 0;
        public string sChatGPTPayLoad = "";
        public string sChatGPTResponse = "";

        public string sRunningFocusModeHours = "";
        public string sRunningFocusModeMinutes = "";
        public bool bStartingAnotherFocusMode = false;

        public string sRawFocusModeTemplates = "";

        //Gentle Block related
        public ObservableCollection<DistractingURL> TemporaryAllowedAppsGentleBlock = new ObservableCollection<DistractingURL>();
        public List<string> TemporarryBlockedAppsGentleBlock = new List<string>();

        public ObservableCollection<DistractingURL> TemporaryAllowedURLsGentleBlock = new ObservableCollection<DistractingURL>();
        public List<string> TemporarryBlockedURLsGentleBlock = new List<string>();

        public ObservableCollection<DistractingURL> TemporaryAllowedKeywords = new ObservableCollection<DistractingURL>();
        public List<string> TemporaryAllowedURLsTabTitles = new List<string>();
        public string sRawKeywords = "";

        GentleBlock wndGentleBlock = null;

        public int nFocudBearMode = 1; //1 is cuddly bear mode
        ////
        public bool isOfficeModeEnabled;

        public ObservableCollection<string> arrAlwaysBlockedURLs = new ObservableCollection<string>();

        public ObservableCollection<DistractingURL> arrDistractingURLs = new ObservableCollection<DistractingURL>();

        //Password settings related
        public string sUserPassword { get; set; }
        public bool isPasswordSet { get; set; }
        public bool bPasswordRequireInChangeSettings { get; set; }
        public bool bPasswordRequireInSkipMorning { get; set; }
        public bool bPasswordRequireInSkipEvening { get; set; }
        public bool bPasswordRequireInAbortShutoff { get; set; }
        public bool bPasswordRequireInStopPomodoro { get; set; }
        public bool bPasswordRequireInQuitApp { get; set; }

        //AI Tab
        public bool bMotivationalMessageEnabled { get; set; }
        public bool bAiCheckUrlEnabled { get; set; }
        public bool bAiAllowUrlShowAlertEnabled { get; set; }
        /// 

        public string sConfigFilePath;
        public string sLogQuantityCSVPath;

        public string sGraphDaysPath;
        public string sGraphDaysDurationPath;
        public string sGraphTimePath;
        public string sGraphTimesDurationPath;

        public bool bStringsLoaded;

        public CUIAutomation automation;
        public IUIAutomationFocusChangedEventHandler handler;

        public JObject ConfigFileStrings;
        public UserAccount userAccount { get; set; } = new UserAccount() { userID = "", username = "", email = "", accessToken = "", refreshToken = "" };
        static AppSettings() { }
        private AppSettings()
        {
            GetCurrentAppVersion();

            sMacAddress = Global.GetMacAddress();
            sOSVersion = Environment.OSVersion.Version.ToString();

            nHabitIndex = -1;
            nCurrentActivityType = CurrentActivityType.NONE;

            sConfigFilePath = AppDataDirPath + "\\config.json";
            sLogQuantityCSVPath = AppDataDirPath + "\\FocusBearStats.CSV";

            sGraphDaysPath = AppDataDirPath + "\\GraphDays.html";
            sGraphDaysDurationPath = AppDataDirPath + "\\GraphDaysDuration.html";
            sGraphTimePath = AppDataDirPath + "\\GraphTime.html";
            sGraphTimesDurationPath = AppDataDirPath + "\\GraphTimeDuration.html";

            isAnyMandatoryActivityIncluded = false;
            isMandatoryActivityGoing = false;
            breakAfterMinutes = 0;
            nCountBreaksPostponedTimes = 0;

            isOfficeModeEnabled = false;

            bGoalHealthyHabits = true;
            bGoalStayFocused = true;
            bExitFullScreenClickedWhileOnboarding = false;

            isPasswordSet = false;

            bSkipMicroBreaksDuringFocus = false;
        }
        public void GetLoginAndTokenValidSettings()
        {
            LogWriter.LogWrite("GetLoginAndTokenValidSettings()");
            try
            {

                IsLoggedIn = RegistryManager.GetLoggedInStatus();
                if (IsLoggedIn)
                {
                    //GEt Logged user Info first
                    RegistryManager.GetLoggedInUserInfo();

                    GetTokens();
                    var authenticator = new Authentication();

                    if (!authenticator._isEmptyOrInvalid(userAccount.accessToken))
                    {
                        LogWriter.LogWrite("Token valid, GEt all trial Info");

                        //Token valid, GEt all trial Info
                        GetAllTrialInfo();
                        ((App)(Application.Current)).ProceedAfterGetLoginAndTokenValidSettings();
                    }
                    else
                    {
                        LogWriter.LogWrite("Token INVALID, call RenewToken()");

                        //Renew the Token
                        RenewToken();
                    }
                }
                else
                {
                    //Download Config File and Show Login Window
                    ((App)(Application.Current)).ShowLoginWindow();
                    return;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetLoginAndTokenValidSettings()" + ex.Message);
            }

        }

        public void GetAllTrialInfo()
        {
            LogWriter.LogWrite("GetAllTrialInfo()");
            try
            {


                var authenticator = new Authentication();

                string strTopLevelClaim = authenticator.getJWTTokenClaim(userAccount.accessToken, "https://eyst-backend-prod-8th3.onrender.com/user");

                if (strTopLevelClaim.Length > 0)
                {
                    JObject strTopLevelClaimsParsed = JObject.Parse(strTopLevelClaim);

                    string subscriptionStatus = strTopLevelClaimsParsed["subscriptionStatus"].ToString();

                    LogWriter.LogWrite("subscriptionStatus - " + subscriptionStatus);

                    /*
                     {
                      "activeEntitlements": [
                        "trial"
                      ],
                      "expirations": {
                        "trial": {
                          "days_left": 29,
                          "expires_date": "2022-11-07T14:16:17Z",
                          "purchase_date": "2022-10-07T14:16:17Z"
                        }
                      },
                      "hasActiveSubscription": true
                    }
                    */
                    JObject subscriptionStatusParsed = JObject.Parse(subscriptionStatus);
                    bool hasActiveSubscription = (bool)subscriptionStatusParsed["hasActiveSubscription"];
                    if (hasActiveSubscription)
                    {
                        
                        LogWriter.LogWrite("hasActiveSubscription");
                        isExpired = false;

                        string activeEntitlement = subscriptionStatusParsed["activeEntitlements"][0].ToString();
                        if (activeEntitlement == "personal" || activeEntitlement == "team_member" || activeEntitlement == "team_owner" || activeEntitlement == "team_size_5")
                        {
                            isPurchased = true;
                        }
                        else if (activeEntitlement == "trial")
                        {
                            isPurchased = false;
                        }
                        else
                        {
                            isPurchased = false;
                        }

                        var expirations = subscriptionStatusParsed["expirations"].ToString();
                        JObject expirationsParsed = JObject.Parse(expirations);
                        var expireInfo = expirationsParsed[activeEntitlement];
                        int vDaysLeft = (int)expireInfo["days_left"];

                        if (isPurchased == false && vDaysLeft > 0)
                        {
                            //Valid Trial
                            nTrialDays = vDaysLeft;
                        }
                    }
                    else
                    {
                        LogWriter.LogWrite("isExpired true");
                        //Download Config File and Show Subscription Expired Nag
                        isExpired = true;
                    }

                    //int nDaysLeft = vDaysLeft.ToInt();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetAllTrialInfo() ex - " + ex.Message);
            }
        }
        public async void RenewToken()
        {
            LogWriter.LogWrite("RenewToken()");
            var authenticator = new Authentication();
            var x = await authenticator.RefreshToken(userAccount.refreshToken);
            if (x)
            {
                //token renewed
                if (!authenticator._isEmptyOrInvalid(userAccount.accessToken))
                {
                    //All Good, Proceed Normally
                    ((App)(Application.Current)).ProceedAfterGetLoginAndTokenValidSettings();
                }
            }
            else
            {
                //token renewed failed, Show Login Window after downloading Config file
                ((App)(Application.Current)).ShowLoginWindow();
            }
        }

        public bool ValidateTokenValidBeforeAPICall()
        {
            LogWriter.LogWrite("ValidateTokenValidBeforeAPICall()");
            try
            {

                IsLoggedIn = RegistryManager.GetLoggedInStatus();
                if (IsLoggedIn)
                {
                    GetTokens();
                    var authenticator = new Authentication();

                    if (!authenticator._isEmptyOrInvalid(userAccount.accessToken))
                    {
                        LogWriter.LogWrite("Token valid, can proceed with API");
                        return true;
                    }
                    else
                    {
                        LogWriter.LogWrite("Token INVALID should RenewToken() before API");
                        return false;
                    }
                }
                else
                {
                    LogWriter.LogWrite("ValidateTokenValidBeforeAPICall() - user not logged in, should Restart the app");
                    //user not logged in, should Restart the app
                    bRestartAppNeeded = true;

                    Application.Current.Shutdown(0);
                    //string strExeFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    //Process.Start(strExeFilePath, "/restartbyself");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ValidateTokenValidBeforeAPICall()" + ex.Message);
                Debug.WriteLine(ex.Message);
                return false;
            }

        }


        public async void DownloadAndLoadConfigFile()
        {
            LogWriter.LogWrite("DownloadAndLoadConfigFile");
            try
            {
                var status = await DownloadConfigFile();
                if (status)
                {
                    LogWriter.LogWrite("DownloadAndLoadConfigFile Success - Proceed to read");
                    try
                    {
                        ConfigFileStrings = JObject.Parse(File.ReadAllText(sConfigFilePath));
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("DownloadAndLoadConfigFile  JObject.Parse(File.ReadAllText(sConfigFilePath)); ex = " + ex.Message);
                        //Use Local File
                        Global.DeleteFile(sConfigFilePath, false);
                        File.Copy(@"config.json", sConfigFilePath);
                        ConfigFileStrings = JObject.Parse(File.ReadAllText(sConfigFilePath));
                    }

                    ((App)(Application.Current)).ProceedAfterDownloadingConfigFile();
                }
                else
                {
                    LogWriter.LogWrite("DownloadAndLoadConfigFile Failed - Proceed to read Local File");
                    //Use Local File
                    Global.DeleteFile(sConfigFilePath, false);
                    File.Copy(@"config.json", sConfigFilePath);
                    ConfigFileStrings = JObject.Parse(File.ReadAllText(sConfigFilePath));
                    ((App)(Application.Current)).ProceedAfterDownloadingConfigFile();
                }

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DownloadAndLoadConfigFile() - " + ex.Message);
                //Debug.WriteLine(ex.Message);
            }
        }
        public void CopyGraphsToLocal()
        {
            
            LogWriter.LogWrite("CopyGraphsToLocal");
            try
            {
                sGraphDaysPath = AppDataDirPath + "\\GraphDays.html";
                sGraphDaysDurationPath = AppDataDirPath + "\\GraphDaysDuration.html";
                sGraphTimePath = AppDataDirPath + "\\GraphTime.html";
                sGraphTimesDurationPath = AppDataDirPath + "\\GraphTimeDuration.html";

                Global.DeleteFile(sGraphDaysPath, false);
                Global.DeleteFile(sGraphDaysDurationPath, false);
                Global.DeleteFile(sGraphTimePath, false);
                Global.DeleteFile(sGraphTimesDurationPath, false);

                File.Copy(@"Graph/GraphDays.html", sGraphDaysPath);
                File.Copy(@"Graph/GraphDaysDuration.html", sGraphDaysDurationPath);
                File.Copy(@"Graph/GraphTime.html", sGraphTimePath);
                File.Copy(@"Graph/GraphTimeDuration.html", sGraphTimesDurationPath);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("CopyGraphsToLocal failed ex - " + ex.Message);
            }
        }

        public void SaveLoggedInUserInfo()
        {
            LogWriter.LogWrite("SaveLoggedInUserInfo()");

            RegistryManager.SaveLoggedInUserInfo();
            // RegistryManager.SetLoggedInStatus();
        }

        public void SaveTokens()
        {
            LogWriter.LogWrite("SaveTokens()");

            RegistryManager.SaveTokens();
            RegistryManager.SetLoggedInStatus();
        }
        public void GetTokens()
        {
            LogWriter.LogWrite("GetTokens()");

            RegistryManager.GetTokens();
        }
        public async Task<bool> DownloadConfigFile()
        {
            LogWriter.LogWrite("DownloadConfigFile");

            try
            {
                string sConfigFileURL = "https://focus-bear.github.io/assets/config/config.json";

                if (sLangCode == "en")
                {
                    sConfigFileURL = "https://focus-bear.github.io/assets/config/config.json";
                }
                else if (sLangCode == "es")
                {
                    sConfigFileURL = "https://focus-bear.github.io/assets/config/config-es.json";
                }
                
                Global.DeleteFile(sConfigFilePath, false);

                HttpCommunication http = new HttpCommunication();
                if (await http.Download(sConfigFileURL, sConfigFilePath, false))
                {
                    LogWriter.LogWrite("DownloadConfigFile Success!");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DownloadConfigFile()" + ex.Message);
                return false;
            }
        }

        public async Task<bool> DownloadBlockHTMLFile()
        {
            LogWriter.LogWrite("DownloadBlockHTMLFile");

            try
            {
                string sHtmlFileURL = "https://focus-bear.github.io/assets/focus-blocked/index.html";

                Global.DeleteFile(AppSettings.FocusBlockPagePath + "1", false); //remove temp

                HttpCommunication http = new HttpCommunication();
                if (await http.Download(sHtmlFileURL, AppSettings.FocusBlockPagePath + "1", false)) //download temp file
                {
                    LogWriter.LogWrite("DownloadBlockHTMLFile Success!");

                    if(File.Exists(AppSettings.FocusBlockPagePath))
                        Global.DeleteFile(AppSettings.FocusBlockPagePath, false);

                    File.Move(AppSettings.FocusBlockPagePath + "1", AppSettings.FocusBlockPagePath); //move to original name

                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DownloadBlockHTMLFile()" + ex.Message);
                return false;
            }
        }

        public void ReadAndParseSettingsJson()
        {
            LogWriter.LogWrite("ReadAndParseSettingsJson()");

            arrAllMorningHabits.Clear();
            arrAllEveningHabits.Clear();
            arrAllbreakActivities.Clear();

            //Check is settings are there
            if (sRawUserSettings == null || sRawUserSettings.Length == 0)
            {
                LogWriter.LogWrite("sRawUserSettings == null - Read previously saved in registry");
                //Read previously saved in registry
                RegistryManager.GetSavedUserSettings();
            }
            if (sRawUserSettings.Length > 0)
            {

                //Parse the JSON string
                try
                {
                    JObject UserSettings = JObject.Parse(sRawUserSettings);
                    sDayStartTime = UserSettings.ContainsKey("startup_time") ? UserSettings["startup_time"].ToString() : "05:15";
                    sDayShutOffTime = UserSettings.ContainsKey("shutdown_time") ? UserSettings["shutdown_time"].ToString() : "20:00";
                    breakAfterMinutes = UserSettings.ContainsKey("break_after_minutes") ? (int)UserSettings["break_after_minutes"] : 15;
                    bHasUserEditedSettings = UserSettings.ContainsKey("has_edited_settings") ? (bool)UserSettings["has_edited_settings"] : false;
                    LogWriter.LogWrite("sDayStartTime - " + sDayStartTime);
                    LogWriter.LogWrite("sDayShutOffTime - " + sDayShutOffTime);

                    JArray arrayMorningActivities = (JArray)UserSettings["morning_activities"];
                    JArray arrayEveningActivities = (JArray)UserSettings["evening_activities"];
                    JArray arrayBreakActivities = (JArray)UserSettings["break_activities"];

                    if (arrayMorningActivities.Count() > 0)
                    {
                        foreach (JObject item in arrayMorningActivities)
                        {
                            string id = item.ContainsKey("id") ? item.GetValue("id").ToString() : "";

                            string name = item.ContainsKey("name") ? item.GetValue("name").ToString() : "";

                            int durationSeconds = item.ContainsKey("duration_seconds") ? (int)item.GetValue("duration_seconds") : 0;

                            List<string> arrVideoUrlsArray = new List<string>(); ;
                            if (item.ContainsKey("video_urls"))
                            {
                                JArray arrayurlsArray = (JArray)item.GetValue("video_urls");
                                arrVideoUrlsArray = arrayurlsArray.ToObject<List<string>>();
                            }

                            //string[] urlsArray = item.GetValue("allowed_urls") ;
                            bool logQuantity = item.ContainsKey("log_quantity") ? (bool)item.GetValue("log_quantity") : false;

                            string logQuantityQuestion = item.ContainsKey("log_quantity_question") ? item.GetValue("log_quantity_question").ToString() : "";

                            List<string> allowedUrls = new List<string>(); ;
                            if (item.ContainsKey("allowed_urls"))
                            {
                                JArray arrayurlsArray = (JArray)item.GetValue("allowed_urls");
                                allowedUrls = arrayurlsArray.ToObject<List<string>>();
                            }

                            List<string> allowedApps = new List<string>();
                            if (item.ContainsKey("allowed_apps"))
                            {
                                JArray arrayurlsArray = (JArray)item.GetValue("allowed_apps");
                                allowedApps = arrayurlsArray.ToObject<List<string>>();
                            }

                            bool openSpecificApps = false;
                            if (allowedApps.Count > 0)
                            {
                                openSpecificApps = true;
                            }

                            string allowedFocusMode = item.ContainsKey("allowed_focus_mode_id") ? item.GetValue("allowed_focus_mode_id").ToString() : "";
                            string activity_sequence_id = item.ContainsKey("activity_sequence_id") ? item.GetValue("activity_sequence_id").ToString() : "";

                            arrAllMorningHabits.Add(new habit(id, name, durationSeconds, arrVideoUrlsArray, logQuantity, logQuantityQuestion, openSpecificApps, allowedUrls, allowedApps, allowedFocusMode, activity_sequence_id));
                        }
                    }
                    if (arrayEveningActivities.Count() > 0)
                    {
                        foreach (JObject item in arrayEveningActivities)
                        {
                            string id = item.ContainsKey("id") ? item.GetValue("id").ToString() : "";

                            string name = item.ContainsKey("name") ? item.GetValue("name").ToString() : "";

                            int durationSeconds = item.ContainsKey("duration_seconds") ? (int)item.GetValue("duration_seconds") : 0;

                            List<string> arrVideoUrlsArray = new List<string>(); ;
                            if (item.ContainsKey("video_urls"))
                            {
                                JArray arrayurlsArray = (JArray)item.GetValue("video_urls");
                                arrVideoUrlsArray = arrayurlsArray.ToObject<List<string>>();
                            }

                            //string[] urlsArray = item.GetValue("allowed_urls") ;
                            bool logQuantity = item.ContainsKey("log_quantity") ? (bool)item.GetValue("log_quantity") : false;

                            string logQuantityQuestion = item.ContainsKey("log_quantity_question") ? item.GetValue("log_quantity_question").ToString() : "";

                            List<string> allowedUrls = new List<string>(); ;
                            if (item.ContainsKey("allowed_urls"))
                            {
                                JArray arrayurlsArray = (JArray)item.GetValue("allowed_urls");
                                allowedUrls = arrayurlsArray.ToObject<List<string>>();
                            }

                            List<string> allowedApps = new List<string>();
                            if (item.ContainsKey("allowed_apps"))
                            {
                                JArray arrayurlsArray = (JArray)item.GetValue("allowed_apps");
                                allowedApps = arrayurlsArray.ToObject<List<string>>();
                            }

                            bool openSpecificApps = false;
                            if (allowedApps.Count > 0)
                            {
                                openSpecificApps = true;
                            }

                            string allowedFocusMode = item.ContainsKey("allowed_focus_mode_id") ? item.GetValue("allowed_focus_mode_id").ToString() : "";
                            string activity_sequence_id = item.ContainsKey("activity_sequence_id") ? item.GetValue("activity_sequence_id").ToString() : "";

                            arrAllEveningHabits.Add(new habit(id, name, durationSeconds, arrVideoUrlsArray, logQuantity, logQuantityQuestion, openSpecificApps, allowedUrls, allowedApps, allowedFocusMode, activity_sequence_id));
                        }
                    }
                    if (arrayBreakActivities.Count() > 0)
                    {
                        foreach (JObject item in arrayBreakActivities)
                        {
                            string id = item.ContainsKey("id") ? item.GetValue("id").ToString() : "";

                            string name = item.ContainsKey("name") ? item.GetValue("name").ToString() : "";

                            int durationSeconds = item.ContainsKey("duration_seconds") ? (int)item.GetValue("duration_seconds") : 0;

                            bool IncludeInEveryBreak = item.ContainsKey("include_in_every_break") ? (bool)item.GetValue("include_in_every_break") : false;

                            List<string> arrVideoUrlsArray = new List<string>(); ;
                            if (item.ContainsKey("video_urls"))
                            {
                                JArray arrayurlsArray = (JArray)item.GetValue("video_urls");
                                arrVideoUrlsArray = arrayurlsArray.ToObject<List<string>>();
                            }

                            bool logQuantity = item.ContainsKey("log_quantity") ? (bool)item.GetValue("log_quantity") : false;

                            List<string> allowedUrls = new List<string>(); ;
                            if (item.ContainsKey("allowed_urls"))
                            {
                                JArray arrayurlsArray = (JArray)item.GetValue("allowed_urls");
                                allowedUrls = arrayurlsArray.ToObject<List<string>>();
                            }

                            List<string> allowedApps = new List<string>();
                            if (item.ContainsKey("allowed_apps"))
                            {
                                JArray arrayurlsArray = (JArray)item.GetValue("allowed_apps");
                                allowedApps = arrayurlsArray.ToObject<List<string>>();
                            }

                            bool openSpecificApps = false;
                            if (allowedApps.Count > 0)
                            {
                                openSpecificApps = true;
                            }

                            bool isOfficeFriendly = item.ContainsKey("is_office_friendly") ? (bool)item.GetValue("is_office_friendly") : false;

                            string allowedFocusMode = item.ContainsKey("allowed_focus_mode_id") ? item.GetValue("allowed_focus_mode_id").ToString() : "";

                            string activity_sequence_id = item.ContainsKey("activity_sequence_id") ? item.GetValue("activity_sequence_id").ToString() : "";

                            bool isRandomChoice = false;
                            string random_choice_type = item.ContainsKey("choice_type") ? item.GetValue("choice_type").ToString() : "";
                            if (random_choice_type == "random")
                            {
                                isRandomChoice = true;
                            }


                            List<activityChoice> choicesToAdd = new List<activityChoice>();
                            if (item.ContainsKey("choices"))
                            {
                                JArray aChoicesArray = (JArray)item.GetValue("choices");
                                // allowedApps = aChoicesArray.ToObject<List<string>>();
                                foreach (JObject choiceitem in aChoicesArray)
                                {
                                    string idchoice = choiceitem.ContainsKey("id") ? choiceitem.GetValue("id").ToString() : "";
                                    string namechoice = choiceitem.ContainsKey("name") ? choiceitem.GetValue("name").ToString() : "";
                                    List<string> arrVideoUrlsArraychoice = new List<string>(); ;
                                    if (choiceitem.ContainsKey("video_urls"))
                                    {
                                        JArray arrayurlsArraychoice = (JArray)choiceitem.GetValue("video_urls");
                                        arrVideoUrlsArraychoice = arrayurlsArraychoice.ToObject<List<string>>();
                                    }
                                    string logQuantityQuestionchoice = choiceitem.ContainsKey("log_quantity_question") ? choiceitem.GetValue("log_quantity_question").ToString() : "";
                                    bool logQuantitychoice = choiceitem.ContainsKey("log_quantity") ? (bool)choiceitem.GetValue("log_quantity") : false;
                                    string logQuantityTypechoice = choiceitem.ContainsKey("log_summary_type") ? choiceitem.GetValue("log_summary_type").ToString() : "";
                                    choicesToAdd.Add(new activityChoice(idchoice, namechoice, arrVideoUrlsArraychoice, logQuantityQuestionchoice, logQuantitychoice, logQuantityTypechoice));

                                }
                            }
                            if (choicesToAdd.Count() == 0)
                            {
                                isRandomChoice = false;
                            }

                            string logQuantityQuestion = item.ContainsKey("log_quantity_question") ? item.GetValue("log_quantity_question").ToString() : "";
                            string logQuantityType = item.ContainsKey("log_summary_type") ? item.GetValue("log_summary_type").ToString() : "";


                            arrAllbreakActivities.Add(new breakActivity(id, name, durationSeconds, arrVideoUrlsArray, logQuantity, logQuantityQuestion, openSpecificApps, allowedUrls, allowedApps, allowedFocusMode, activity_sequence_id, IncludeInEveryBreak, isRandomChoice, choicesToAdd, isOfficeFriendly, logQuantityType));
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("ReadAndParseSettingsJson() EX - " + ex.Message);
                }
                //"startup_time": "05:15",
                //"shutdown_time": "20:30",
                //"break_after_minutes": 15,

                //            JObject MorningActivities = UserSettings["morning_activities"];
                // string subscriptionStatus = sUderId["subscriptionStatus"].ToString();

            }

        }

        public void ReadAndParseUserDetailsJson()
        {
            LogWriter.LogWrite("ReadAndParseUserDetailsJson()");

            try
            {
                //Check is settings are there
                if (sUserDetailsInfo == null || sUserDetailsInfo.Length == 0)
                {
                    LogWriter.LogWrite("sUserDetailsInfo == null - Read previously saved in registry");
                    //Read previously saved in registry
                    RegistryManager.GetSavedUserSettings();
                }
                if (sUserDetailsInfo.Length > 0)
                {

                    //Parse the JSON string
                    try
                    {
                        JObject UserSettings = JObject.Parse(sUserDetailsInfo);
                        sUser_name = UserSettings.ContainsKey("email") ? UserSettings["email"].ToString() : "";
                        sUser_email = UserSettings.ContainsKey("name") ? UserSettings["name"].ToString() : "";
                        sUser_id = UserSettings.ContainsKey("id") ? UserSettings["id"].ToString() : "";

                        LogWriter.LogWrite("EMAIL IS:" + sUser_email);
                        LogWriter.LogWrite("USER Name is:" + sUser_name);
                        LogWriter.LogWrite("USER ID is:" + sUser_id);

                        JArray jarrFocusModes = UserSettings.ContainsKey("focus_modes") ? (JArray)UserSettings["focus_modes"] : null;

                        if (jarrFocusModes != null && jarrFocusModes.Count() > 0)
                        {
                            foreach (JObject item in jarrFocusModes)
                            {
                                try
                                {
                                    string id = item.ContainsKey("id") ? item.GetValue("id").ToString() : "";
                                    string name = item.ContainsKey("name") ? item.GetValue("name").ToString() : "";

                                    ObservableCollection<string> allowedUrls = new ObservableCollection<string>();

                                    ObservableCollection<AppDetails> allowedAppsWin = new ObservableCollection<AppDetails>();

                                    ObservableCollection<string> blcokedURLs = new ObservableCollection<string>();

                                    string color = "";
                                    bool isOn = false;
                                    bool isAllowedAllApps = false;
                                    bool isAllowedListApproach = false;
                                    bool isAllowedAllUrls = false;
                                    bool isBlockedAllUrls = false;
                                    bool isDefault = false;
                                    bool isMeetings = false;
                                    bool isLockedFocus = false;

                                    if (item.ContainsKey("allowed_urls"))
                                    {
                                        JArray arrayurlsArray = (JArray)item.GetValue("allowed_urls");
                                        allowedUrls = arrayurlsArray.ToObject<ObservableCollection<string>>();
                                    }
                                    if (item.ContainsKey("metadata"))
                                    {
                                        string sMetadata = item.GetValue("metadata").ToString();
                                        JObject metadata = JObject.Parse(sMetadata);

                                        if (metadata.ContainsKey("allowed_apps_win"))
                                        {
                                            JArray allowedApps = (JArray)metadata.GetValue("allowed_apps_win");
                                            try
                                            {
                                                allowedAppsWin = allowedApps.ToObject<ObservableCollection<AppDetails>>();
                                            }
                                            catch (Exception ex)
                                            {
                                                LogWriter.LogWrite("allowedAppsWin() Reading FAILED Possibly old data + " + ex.Message);
                                            }
                                        }

                                        if (metadata.ContainsKey("blockUrlsArray"))
                                        {
                                            JArray tempblockedUrls = (JArray)metadata.GetValue("blockUrlsArray");
                                            blcokedURLs = tempblockedUrls.ToObject<ObservableCollection<string>>();
                                        }

                                        color = metadata.ContainsKey("color") ? "#" + metadata.GetValue("color").ToString() : "";
                                        isOn = metadata.ContainsKey("isOn") ? Convert.ToBoolean(metadata.GetValue("isOn")) : false;
                                        isAllowedAllApps = metadata.ContainsKey("isAllowedAllApps") ? Convert.ToBoolean(metadata.GetValue("isAllowedAllApps")) : false;
                                        isAllowedListApproach = metadata.ContainsKey("isAllowedListApproach") ? Convert.ToBoolean(metadata.GetValue("isAllowedListApproach")) : false;
                                        ///isAllowedAllUrls = metadata.ContainsKey("isAllowedAllUrls") ? Convert.ToBoolean(metadata.GetValue("isAllowedAllUrls")) : false;
                                        ///isBlockedAllUrls = metadata.ContainsKey("isBlockedAllUrls") ? Convert.ToBoolean(metadata.GetValue("isBlockedAllUrls")) : false;
                                        isDefault = metadata.ContainsKey("isDefault") ? Convert.ToBoolean(metadata.GetValue("isDefault")) : false;
                                        isMeetings = metadata.ContainsKey("isMeetings") ? Convert.ToBoolean(metadata.GetValue("isMeetings")) : false;
                                        isLockedFocus = metadata.ContainsKey("isLockedFocus") ? Convert.ToBoolean(metadata.GetValue("isLockedFocus")) : false;
                                    }

                                    arrFocusModes.Add(new FocusMode(id, name, allowedUrls, blcokedURLs, allowedAppsWin, color, isOn, isAllowedAllApps, isAllowedListApproach, isDefault, isMeetings, isLockedFocus, (isMeetings ? ConfigFileStrings["focusPreferencesInfo"]["meetingsFocusInfoText"].ToString() : "")));
                                }

                                catch (Exception ex)
                                {
                                    LogWriter.LogWrite("ReadAndParseUserDetailsJson() Reading jarrFocusModes + " + ex.Message);
                                    continue; //Even if one focus mode have failed we will read the next one
                                }
                            }
                            //Sort the focus mode array to take the Locked Focus mode at the end of the list.
                            FocusMode TempFocus = null;
                            foreach (FocusMode FF in arrFocusModes)
                            {
                                if (FF.isLockedFocus)
                                {
                                    TempFocus = FF;
                                    arrFocusModes.Remove(TempFocus);
                                    break;
                                }
                            }
                            if (TempFocus != null)
                                arrFocusModes.Add(TempFocus);

                            //We need to decide if the foucs mode had been setup
                            if(!RegistryManager.GetFocusModeSetupDone())
                            {
                                if(arrFocusModes.Count > 3)
                                {
                                    RegistryManager.SetFocusModeSetupDone(true);
                                }
                            }
                          
                        }

                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("ReadAndParseUserDetailsJson() + " + ex.Message);
                        Debug.WriteLine(ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ReadAndParseUserDetailsJson() top" + ex.Message);
            }
        }

        public void ReadAndParseUserLocalDeviceSettingsJson()
        {
            LogWriter.LogWrite("ReadAndParseUserLocalDeviceSettingsJson()");

            try
            {
                //Check is settings are there
                if (sUserLocalDeviceSettings == null || sUserLocalDeviceSettings.Length == 0)
                {
                    LogWriter.LogWrite("sUserLocalDeviceSettings == null - Read previously saved in registry");
                    //Read previously saved in registry
                    RegistryManager.GetLocalDeviceSettings();
                }
                if (sUserLocalDeviceSettings.Length > 0)
                {

                    //Parse the JSON string
                    try
                    {
                        JObject LocalSettings = JObject.Parse(sUserLocalDeviceSettings);
                        
                        string sMacOSSettings = LocalSettings.ContainsKey("MacOS") ? LocalSettings["MacOS"].ToString() : "";

                        JObject sMacOSSettingsOBJ = JObject.Parse(sMacOSSettings);

                        if (sMacOSSettingsOBJ.ContainsKey("kArrBlockedUrls"))
                        { 
                            JArray jarrALwaysBlockedURLs = sMacOSSettingsOBJ.ContainsKey("kArrBlockedUrls") ? (JArray)sMacOSSettingsOBJ["kArrBlockedUrls"] : null;

                            if (jarrALwaysBlockedURLs != null && jarrALwaysBlockedURLs.Count() > 0)
                            {
                                arrAlwaysBlockedURLs = jarrALwaysBlockedURLs.ToObject<ObservableCollection<string>>(); ;
                            }
                        }
                       
                        isOfficeModeEnabled = sMacOSSettingsOBJ.ContainsKey("kIsOfficeModeEnabled") ? (bool)sMacOSSettingsOBJ.GetValue("kIsOfficeModeEnabled") : false;

                        sUserPassword = sMacOSSettingsOBJ.ContainsKey("kAppPassword") ? (string)sMacOSSettingsOBJ.GetValue("kAppPassword") : "";
                        RegistryManager.SetUserPassword(); //save to local
                        if (sUserPassword.Length > 0)
                        {
                            isPasswordSet = true;
                        }
                        else
                        {
                            isPasswordSet = false;
                        }

                        //Cuddly bear or grizlly bear mode
                        bool bCuddlyMode = sMacOSSettingsOBJ.ContainsKey("kCuddlyModeEnabled") ? (bool)sMacOSSettingsOBJ.GetValue("kCuddlyModeEnabled") : false;
                        if (bCuddlyMode)
                            nFocudBearMode = 1;//cuddly bear mode
                        else
                            nFocudBearMode = 2; //whatever

                        bPasswordRequireInChangeSettings = sMacOSSettingsOBJ.ContainsKey("kIsPasswordRequireInChangeSettings") ? (bool)sMacOSSettingsOBJ.GetValue("kIsPasswordRequireInChangeSettings") : false;
                        bPasswordRequireInSkipMorning = sMacOSSettingsOBJ.ContainsKey("kIsPasswordRequireInSkipMorning") ? (bool)sMacOSSettingsOBJ.GetValue("kIsPasswordRequireInSkipMorning") : false;
                        bPasswordRequireInSkipEvening = sMacOSSettingsOBJ.ContainsKey("kIsPasswordRequireInSkipEvening") ? (bool)sMacOSSettingsOBJ.GetValue("kIsPasswordRequireInSkipEvening") : false;
                        bPasswordRequireInAbortShutoff = sMacOSSettingsOBJ.ContainsKey("kIsPasswordRequireInAbortShutoff") ? (bool)sMacOSSettingsOBJ.GetValue("kIsPasswordRequireInAbortShutoff") : false;
                        // bPasswordRequireInStopPomodoro = sMacOSSettingsOBJ.ContainsKey("kIsPasswordRequireInStopPomodoro") ? (bool)sMacOSSettingsOBJ.GetValue("kIsPasswordRequireInStopPomodoro") : false;
                        bPasswordRequireInQuitApp = sMacOSSettingsOBJ.ContainsKey("kIsPasswordRequireInQuitApp") ? (bool)sMacOSSettingsOBJ.GetValue("kIsPasswordRequireInQuitApp") : false;

                        bMotivationalMessageEnabled =  sMacOSSettingsOBJ.ContainsKey("kMotivationalMessageEnabled") ? (bool)sMacOSSettingsOBJ.GetValue("kMotivationalMessageEnabled") : false;
                        bAiCheckUrlEnabled = sMacOSSettingsOBJ.ContainsKey("kAiCheckUrlEnabled") ? (bool)sMacOSSettingsOBJ.GetValue("kAiCheckUrlEnabled") : true;
                        bAiAllowUrlShowAlertEnabled = sMacOSSettingsOBJ.ContainsKey("kAiAllowUrlShowAlertEnabled") ? (bool)sMacOSSettingsOBJ.GetValue("kAiAllowUrlShowAlertEnabled") : true;

                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("ReadAndParseUserLocalDeviceSettingsJson() 1 ex - " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ReadAndParseUserLocalDeviceSettingsJson() 2 ex - " + ex.Message);
            }
        }
    
        public void AddFocusModeToArrayAndUploadtoAPI(FocusMode ff)
        {
            LogWriter.LogWrite("AddFocusModeToArrayAndUploadtoAPI()");
            try
            {
                //add to focus modes  array
                arrFocusModes.Add(ff);

                //Upload to api
                FocusBearAPIClient client = new FocusBearAPIClient();

                LogWriter.LogWrite("attempt to AddFocusModeToArrayAndUploadtoAPI for default focus modes");
                var userSettingsResponse = client.AddFocusMode(ff);

            }
            catch (Exception ex)
            {
                Debug.WriteLine("AddFocusModeToArrayAndUploadtoAPI()" + ex.Message);
            }
        }

        public void ReadAndCreateDefaultFocusModes()
        {
            LogWriter.LogWrite("ReadAndCreateDefaultFocusModes()");

            //var thread = new Thread(() =>
            //{
                try
                {
                    if (arrFocusModes != null)
                    {
                        //28 Jan 2023 stopped creating this as a focus mode, it will be available in Tools as in mac app
                        //Check Default Focus Mode
                        var listItemsD = arrFocusModes.Where(i => i.isDefault == true);
                        if (listItemsD.Count() == 0)
                        {
                            LogWriter.LogWrite("Default focus mode not found, create new");
                            //Call the API to upload the newly cread focus mode
                            //add to focus modes  array and Upload to api
                            //#A3FFA1 is the default color
                            string sName = ConfigFileStrings["focusPreferencesInfo"]["defaultFocusText"].ToString(); //"Complete anarchy! Block nothing 😬"
                            AddFocusModeToArrayAndUploadtoAPI(new FocusMode(System.Guid.NewGuid().ToString(), sName, new ObservableCollection<string>(), new ObservableCollection<string>(), new ObservableCollection<AppDetails>(), "#A3FFA1", false, true, true, true, false, false));

                        }
                        //Check Meetings Focus Mode
                        var listItemsM = arrFocusModes.Where(m => m.isMeetings == true);
                        if (listItemsM.Count() == 0)
                        {
                            LogWriter.LogWrite("isMeetings focus mode not found, create new");
                            //Call the API to upload the newly cread focus mode
                            //add to focus modes  array and Upload to api
                            //#A3FFA1 is the default color
                            //public List<string> arrAllowedApps; //wont use this as this is for mac apps
                            ObservableCollection<string> arrAllowedUrls = new ObservableCollection<string>();
                            arrAllowedUrls.Add("http://meet.google.com");

                            ObservableCollection<AppDetails> arrAllowedAppsWin = new ObservableCollection<AppDetails>();
                            //"allowed_apps": ["/Applications/Microsoft Teams.app", "/Applications/zoom.us.app", "/Applications/Jitsi.app", "/Applications/Safari.app", "/Applications/Google Chrome.app", "/Applications/Firefox.app", "/Applications/Slack.app"]
                            arrAllowedAppsWin.Add(new AppDetails("Microsoft Teams", "teams.exe"));
                            arrAllowedAppsWin.Add(new AppDetails("Zoom", "zoom.exe"));
                            arrAllowedAppsWin.Add(new AppDetails("Jitsi", "jitsi.exe"));
                            arrAllowedAppsWin.Add(new AppDetails("Slack", "slack.exe"));
                            arrAllowedAppsWin.Add(new AppDetails("Microsoft Edge", "msedge.exe"));
                            arrAllowedAppsWin.Add(new AppDetails("Mozilla Firefox", "firefox.exe"));
                            arrAllowedAppsWin.Add(new AppDetails("Google Chrome", "chrome.exe"));

                            string sName = ConfigFileStrings["focusPreferencesInfo"]["meetingsFocusText"].ToString(); //"meetingsFocusText":"Meetings",
                            AddFocusModeToArrayAndUploadtoAPI(new FocusMode(System.Guid.NewGuid().ToString(), sName, arrAllowedUrls, new ObservableCollection<string>(), arrAllowedAppsWin, "#A3FFA1", false, false, true, false, true, false));

                        }
                        //Check Locked Focus Mode
                        var listItemsL = arrFocusModes.Where(l => l.isLockedFocus == true);
                        if (listItemsL.Count() == 0)
                        {
                            LogWriter.LogWrite("isLockedFocus focus mode not found, create new");
                            //Call the API to upload the newly cread focus mode
                            //add to focus modes  array and Upload to api
                            //#A3FFA1 is the default color
                            string sName = ConfigFileStrings["focusPreferencesInfo"]["lockedFocusText"].ToString();//        "lockedFocusText":"Locked Focus on current activity",
                            AddFocusModeToArrayAndUploadtoAPI(new FocusMode(System.Guid.NewGuid().ToString(), sName, new ObservableCollection<string>(), new ObservableCollection<string>(), new ObservableCollection<AppDetails>(), "#A3FFA1", false, false, true, false, false, true));

                        }

                        //Check the focus mode for the job type and create if its not already there
                        //var listItemsW = arrFocusModes.Where(i => i.name == FocusModeNameToCreateWhileOnBoarding.name);
                        //if (listItemsW.Count() == 0)
                        //{
                        //    //Call the API to upload the newly cread focus mode
                        //    //add to focus modes  array and Upload to api
                        //    //#A3FFA1 is the default color

                        //    AddFocusModeToArrayAndUploadtoAPI(FocusModeNameToCreateWhileOnBoarding);

                        //}
                    }

                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("ReadAndCreateDefaultFocusModes() EX - " + ex.Message);
                }
           // });
          //  thread.IsBackground = true;
            //thread.Start();

        }
        public bool PrepareBreakActivities()
        {
            LogWriter.LogWrite("PrepareBreakActivities()");
            /// addLog(text: "startBreakActivities")

            ///self.breakTimer?.invalidate()
            ///self.breakTimer = nil
            ///
            if (arrAllbreakActivities.Count() != 0)
            {
                /// self.btnAbort.isHidden = false
                /// 
                isAnyMandatoryActivityIncluded = false;

                isMandatoryActivityGoing = false;

                arrActivitiesExcludeInEveryBreak.Clear();
                arrActivitiesIncludeInEveryBreak.Clear();

                foreach (breakActivity activity in arrAllbreakActivities)
                {
                    if (isOfficeModeEnabled)
                    {
                        if (!activity.isOfficeFriendly)
                        {
                            continue;
                        }
                    }

                    if (activity.IncludeInEveryBreak)
                    {
                        isAnyMandatoryActivityIncluded = true;//than two step will be happen// first mandatory arra than normal array
                        arrActivitiesIncludeInEveryBreak.Add(activity);
                    }
                    else
                    {
                        arrActivitiesExcludeInEveryBreak.Add(activity);
                    }
                }

                // checking if  Office Mode is on and if there is not any activity office friendly than turn off office mode
                if (isOfficeModeEnabled)
                {
                    if (arrActivitiesExcludeInEveryBreak.Count() == 0 && arrActivitiesIncludeInEveryBreak.Count() == 0)
                    {
                        // addLog(text: "SettingsManagerObject.isOfficeModeEnabled false becuse no office friendly activity found.");
                        isOfficeModeEnabled = false;
                        RegistryManager.SetisOfficeModeEnabled(false);


                        ///SettingsManagerObject.isSettingsChanged = true;
                        ///SettingsManagerObject.putUserLocalDeviceSettings();

                        foreach (breakActivity activity in arrAllbreakActivities)
                        {
                            if (activity.IncludeInEveryBreak)
                            {
                                isAnyMandatoryActivityIncluded = true;//than two step will be happen// first mandatory array than normal array
                                arrActivitiesIncludeInEveryBreak.Add(activity);
                            }
                            else
                            {
                                arrActivitiesExcludeInEveryBreak.Add(activity);
                            }
                        }

                    }
                }

                if (arrActivitiesExcludeInEveryBreak.Count() == 0 && arrActivitiesIncludeInEveryBreak.Count() == 0)
                {
                    // addLog(text: "filterd arrActivitiesExcludeInEveryBreak and arrActivitiesIncludeInEveryBreak is zero .")
                    /// self.setActivityTimer()
                    return false;
                }
                if (isAnyMandatoryActivityIncluded)
                {
                    isMandatoryActivityGoing = true;
                    nHabitIndex = 0;

                    LogWriter.LogWrite(" isMandatoryActivityGoing = true; and Habit Index = 0" );
                }
                else
                {
                    if (arrActivitiesExcludeInEveryBreak.Count() != 0)
                    {
                        Random rnd = new Random();
                        int nRandom = rnd.Next(0, arrActivitiesExcludeInEveryBreak.Count() - 1);
                        nHabitIndex = nRandom;
                    }
                    else
                    {
                        nHabitIndex = -1;
                    }

                    LogWriter.LogWrite(" isMandatoryActivityGoing = false; and Habit Index = " + nHabitIndex.ToString());
                }
                return true;

            }

            return false;

        }

        public void WriteDefaultCSVFile()
        {
            LogWriter.LogWrite("WriteDefaultCSVFile()");

            try
            {
                if (File.Exists(sLogQuantityCSVPath))
                {
                    return;
                }
                using (var w = new StreamWriter(sLogQuantityCSVPath))
                {
                    var sHeaders = "Log type,Timestamp,Activity,Duration in seconds,Quantity Question,Quantity Logged,Start Time,Work achieved,Distractions encountered,Abort reason,Feedback";//start time will only be used in focus block start time 
                    w.WriteLine(sHeaders);
                    w.Flush();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("WriteDefaultCSVFile - " + ex.Message);
                //Debug.WriteLine(ex.Message);
            }
        }

        public void AddEntryInCSV(string sLogType, string sActivityName, string sDurationInSeconds, string sLogQuantityQuestion, string sQuantityLogged, string sStartTime, string sWorkAchieved = "", string sDistractionsEncountered = "", string sAbortReason = "", string sFeedback = "")
        {
            LogWriter.LogWrite("AddEntryInCSV()");

            try
            {
                //Timestamp need to be prepared
                string sTimeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                using (System.IO.StreamWriter sw = System.IO.File.AppendText(sLogQuantityCSVPath))
                {
                    string csv = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}", sLogType, sTimeStamp, sActivityName, sDurationInSeconds, sLogQuantityQuestion, sQuantityLogged, sStartTime, sWorkAchieved, sDistractionsEncountered, sAbortReason, sFeedback);
                    sw.WriteLine(csv);
                    sw.Flush();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AddEntryInCSV - " + ex.Message);
                //Debug.WriteLine(ex.Message);
            }

        }

        public void ReplaceLastEntryInCSV()
        {

        }

        public IEnumerable<LogEntry> ReadCSV()
        {
            LogWriter.LogWrite("ReadCSV()");
            try
            {
                string[] lines = File.ReadAllLines(sLogQuantityCSVPath);

                // lines.Select allows me to project each line as a LogEntry. 
                // This will give me an IEnumerable<LogEntry> back.
                return lines.Select(line =>
                {
                    string[] data = line.Split(',');
                    // We return a LogEntry with the data in order.
                    return new LogEntry(data[0], data[1], data[2], data[3], data[4], data[5], data[6], data[7], data[8], data[9], data[10]);
                });
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ReadCSV()" + ex.Message);
            }
            return Enumerable.Empty<LogEntry>();
        }

        public bool IsShutOffTime()
        {
            
            TimeSpan tsStartTime = TimeSpan.Parse(sDayStartTime);
            TimeSpan tsShutOffTime = TimeSpan.Parse(sDayShutOffTime);

            DateTime now = DateTime.Now;

            DateTime myStartTime = new DateTime(now.Year, now.Month, now.Day, tsStartTime.Hours, tsStartTime.Minutes, 00);
            Console.WriteLine("{0}", myStartTime.ToString());
            //LogWriter.LogWrite(string.Format("myStartTime - {0} ", myStartTime.ToString()));

            DateTime myShutOffTime = new DateTime(now.Year, now.Month, now.Day, tsShutOffTime.Hours, tsShutOffTime.Minutes, 00);
            Console.WriteLine("{0}", myShutOffTime.ToString());
            //LogWriter.LogWrite(string.Format("myShutOffTime - {0} ", myShutOffTime.ToString()));

            bool isShutOffTime = false;
            if (myStartTime > myShutOffTime)
            {
                if (now < myStartTime && now > myShutOffTime)
                {
                    //LogWriter.LogWrite("(now < myStartTime && now > myShutOffTime)");
                    isShutOffTime = true;
                }
            }
            else if (now < myStartTime || now > myShutOffTime)
            {
                //LogWriter.LogWrite("(now < myStartTime || now > myShutOffTime)");
                isShutOffTime = true;
            }

            //if(isShutOffTime)
            //{
            //    LogWriter.LogWrite("IsShutOffTime() TRUE");
            //}

            return isShutOffTime;
        }

        public bool IsAppDeactivated()
        {
            try
            {
                DateTime now = DateTime.Now;
                bool bDeactivated = RegistryManager.GetAppDeactivatedTillDate();
                if (!bDeactivated)
                {
                    return false;
                }

                if (now > AppDeactivatedTillDate)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("IsAppDeactivated() - " + ex.Message);
                return false;
            }

        }


        public string SecondsToTimerText(int secs)
        {
            try
            {
                int hours = secs / 3600;
                int mins = (secs % 3600) / 60;
                secs = secs % 60;
                if (hours > 0 && mins > 0 && secs > 0)
                    return string.Format("{0}:{1}:{2}", hours.ToString("D2"), mins.ToString("D2"), secs.ToString("D2"));
                else if (hours > 0 && mins > 0)
                    return string.Format("{0}:{1}:{2}", hours.ToString("D2"), mins.ToString("D2"), secs.ToString("D2"));
                else if (hours > 0 && secs > 0)
                    return string.Format("{0}:{1}:{2}", hours.ToString("D2"), mins.ToString("D2"), secs.ToString("D2"));
                else if (mins > 0 && secs > 0)
                    return string.Format("{0}:{1}", mins.ToString("D2"), secs.ToString("D2"));
                else if (hours > 0)
                    return string.Format("{0}:{1}:{2}", hours.ToString("D2"), mins.ToString("D2"), secs.ToString("D2"));
                else if (mins > 0)
                    return string.Format("{0}:{1}", mins.ToString("D2"), secs.ToString("D2"));
                else if (secs > 0)
                    return string.Format("{0}:{1}", mins.ToString("D2"), secs.ToString("D2"));

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SecondsToTimerText - " + ex.Message);
 
            }

            return "";
            //return string.Format("{0:D2} {1} {2:D2} {3} {4:D2} {5}", hours, "hours", mins, "minutes", secs, "seconds");
        }
        public string SecondsToText(int secs)
        {
            try
            {
                string sHour = ConfigFileStrings["common"]["hourText"].ToString();
                string sHours = ConfigFileStrings["common"]["hoursText"].ToString();
                string sMinute = ConfigFileStrings["common"]["minuteText"].ToString(); ;
                string sMinutes = ConfigFileStrings["common"]["minutesText"].ToString(); ;
                string sSeconds = ConfigFileStrings["common"]["secondsText"].ToString(); ;


                int hours = secs / 3600;
                int mins = (secs % 3600) / 60;
                secs = secs % 60;
                if (hours > 0 && mins > 0 && secs > 0)
                    return string.Format("{0} {1} {2} {3} {4} {5}", hours, (hours > 1 ? sHours : sHour), mins, (mins > 1 ? sMinutes : sMinute), secs, sSeconds);
                else if (hours > 0 && mins > 0)
                    return string.Format("{0} {1} {2} {3}", hours, (hours > 1 ? sHours : sHour), mins, (mins > 1 ? sMinutes : sMinute));
                else if (hours > 0 && secs > 0)
                    return string.Format("{0} {1} {2} {3}", hours, (hours > 1 ? sHours : sHour), secs, sSeconds);
                else if (mins > 0 && secs > 0)
                    return string.Format("{0} {1} {2} {3}", mins, (mins > 1 ? sMinutes : sMinute), secs, sSeconds);
                else if (hours > 0)
                    return string.Format("{0} {1}", hours, (hours > 1 ? sHours : sHour));
                else if (mins > 0)
                    return string.Format("{0} {1}", mins, (mins > 1 ? sMinutes : sMinute));
                else if (secs >= 0)
                    return string.Format("{0} {1} ", secs, sSeconds);

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SecondsToText" + ex.Message);
                //Debug.WriteLine(ex.Message);
            }

            return "";
            //return string.Format("{0:D2} {1} {2:D2} {3} {4:D2} {5}", hours, "hours", mins, "minutes", secs, "seconds");
        }
        public string SecondsToTextHMS(int secs)
        {
            try
            {
                string sHour = ConfigFileStrings["common"]["hoursHText"].ToString();
                string sHours = ConfigFileStrings["common"]["hoursHText"].ToString();
                string sMinute = ConfigFileStrings["common"]["minutesMText"].ToString(); ;
                string sMinutes = ConfigFileStrings["common"]["minutesMText"].ToString(); ;
                string sSeconds = ConfigFileStrings["common"]["secondsSText"].ToString(); ;


                int hours = secs / 3600;
                int mins = (secs % 3600) / 60;
                secs = secs % 60;
                if (hours > 0 && mins > 0 && secs > 0)
                    return string.Format("{0}{1} {2}{3} {4}{5}", hours, (hours > 1 ? sHours : sHour), mins, (mins > 1 ? sMinutes : sMinute), secs, sSeconds);
                else if (hours > 0 && mins > 0)
                    return string.Format("{0}{1} {2}{3}", hours, (hours > 1 ? sHours : sHour), mins, (mins > 1 ? sMinutes : sMinute));
                else if (hours > 0 && secs > 0)
                    return string.Format("{0}{1} {2}{3}", hours, (hours > 1 ? sHours : sHour), secs, sSeconds);
                else if (mins > 0 && secs > 0)
                    return string.Format("{0}{1} {2}{3}", mins, (mins > 1 ? sMinutes : sMinute), secs, sSeconds);
                else if (hours > 0)
                    return string.Format("{0}{1}", hours, (hours > 1 ? sHours : sHour));
                else if (mins > 0)
                    return string.Format("{0}{1}", mins, (mins > 1 ? sMinutes : sMinute));
                else if (secs >= 0)
                    return string.Format("{0}{1} ", secs, sSeconds);

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SecondsToTextHMS" + ex.Message);
                //Debug.WriteLine(ex.Message);
            }

            return "";
            //return string.Format("{0:D2} {1} {2:D2} {3} {4:D2} {5}", hours, "hours", mins, "minutes", secs, "seconds");
        }
        public void GetCurrentAppVersion()
        {
            LogWriter.LogWrite("GetCurrentAppVersion()");
            try
            {
                var currentVersion = Assembly.GetEntryAssembly().GetName().Version;
                AppVersion = currentVersion;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetCurrentAppVersion()" + ex.Message);

            }
        }

        public void GetActiveTabURL(string sProcessName) //TO get the active tab URL of any process
        {
            LogWriter.LogWrite("GetActiveTabURL ()");
            try
            {
                int UIA_ControlTypePropertyId = 30003;
                int UIA_EditControlTypeId = 50004;
                int UIA_PATTERN_ID = 10002;

                // there are always multiple chrome processes, so we have to loop through all of them to find the
                // process with a Window Handle and an automation element of name "Address and search bar"
                Process[] procsChrome = Process.GetProcessesByName(sProcessName);
                foreach (Process process in procsChrome)
                {
                    // the chrome process must have a window
                    if (process.MainWindowHandle == IntPtr.Zero)
                    {
                        continue;
                    }

                    IUIAutomationElement elm = automation.ElementFromHandle(process.MainWindowHandle);
                    IUIAutomationCondition Cond = automation.CreatePropertyCondition(UIA_ControlTypePropertyId, UIA_EditControlTypeId);
                    IUIAutomationElementArray elm2 = elm.FindAll(TreeScope.TreeScope_Descendants, Cond);
                    if (elm2 != null)
                    {
                        for (int i = 0; i < elm2.Length; i++)
                        {
                            IUIAutomationElement elm3 = elm2.GetElement(i);
                            IUIAutomationValuePattern val = (IUIAutomationValuePattern)elm3.GetCurrentPattern(UIA_PATTERN_ID);
                            if (val != null && !string.IsNullOrEmpty(val.CurrentValue))
                            {

                                ////if (Uri.IsWellFormedUriString(val.CurrentValue, UriKind.RelativeOrAbsolute))
                                {
                                    try
                                    {
                                        Uri myUri = new Uri(val.CurrentValue);
                                        string host = myUri.Host; //fails if ther eis no http://
                                        sLastActiveURL = host;
                                        Debug.WriteLine(sLastActiveURL);
                                    }
                                    catch (Exception ex)
                                    {
                                        sLastActiveURL = val.CurrentValue;
                                        Debug.WriteLine(ex.Message);
                                    }
                                  
                                    //appSettings.bGetCurrentURL = false;
                                    return;
                                }

                            }
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetActiveTabURL" + ex.Message);
            }
        }

        public void CapturePostHog(string sEvent, string sMetadataJSON = "")
        {
            FocusBearAPIClient client = new FocusBearAPIClient();
            var x = client.CapturePostHog(sEvent, sMetadataJSON);

            if (userAccount.accessToken.Length > 0) //it is possible that this function is called before the user actually logs in, in that case we willnot call thebelow endpoint
            {
                var y = client.CaptureEventEndpoint(sEvent, sMetadataJSON);
            }
        }

        /*
         * PENDING
        edit-super-pomodoro-mode-settings
        start-pomodoro-mode-manually { "type": "single-pomodoro" | "super-pomodoro", "focusDuration": 25, "breakDuration": 5 }
        start-super-pomodoro-mode-automatically
        export-statistics
        check-for-updates-manually
        edit-focus-mode-colour
        logout
        uninstall

        enable-office-mode
        disable-office-mode
        change-password
        disable-password

        set-apple-script-before-starting-focus-mode
        set-apple-script-after-completing-focus-mode
         */

        public void RemoveSecondaryWindows()
        {
            LogWriter.LogWrite("RemoveSecondaryWindows()");
            try
            {
                if (SecondaryWindows.Count() > 0)
                {
                    foreach (BlankWindow bw in SecondaryWindows)
                    {
                        bw.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("RemoveSecondaryWindows ex - " + ex.Message);
            }

        }

        public void ShowGentleBlockWindow(IntPtr hWnd, string sEXE, string sURL = "", string sTabTitle = "")
        {
           // Task.Run(() =>
           // {
                ((App)(Application.Current)).Dispatcher.BeginInvoke((Action)(() =>
                {
                    if (wndGentleBlock == null)
                    {
                        wndGentleBlock = new GentleBlock();
                    }
                    wndGentleBlock.Show();

                    if (bHabitwithUseFocusModeRunning)//if an activity with use focus mode is running, we need to show the activity name
                    {
                        wndGentleBlock.ShowGentleBlockWindowAndStartTimer(hWnd,sCurrentRunningActivity, sEXE, sURL, "", sTabTitle);
                    }
                    else
                    {
                        wndGentleBlock.ShowGentleBlockWindowAndStartTimer(hWnd, sRunningFocusModeName, sEXE, sURL, sRunningFocusModeIntention, sTabTitle);
                    }
                }));
           // });

           

        }
    }
}
