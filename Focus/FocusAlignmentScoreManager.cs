using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Focus_Bear.Focus
{
    public static class FocusAlignmentScoreManager
    {
        static AppSettings appSettings = AppSettings.GetInstance;
        public static bool IsCuddlyModeEnabled { get; set; }
        public static bool IsAiCheckUrlEnabled { get; set; }
        public static string AppAllowedInFocusModeText { get; set; }
        public static string AppNotAllowedInFocusModeText { get; set; }
        public static string WhatToAchieveInFocusBlock { get; set; }
        public static Dictionary<string, Dictionary<string, object>> DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus { get; set; }
        public static List<string> ArrBrowsersLowerCasedNames { get; set; }
        public static int DefaultFocusAlignmentScoreToShowDistractingPopUp { get; set; }
        public static double MinFocusAlignmentScoreToShowDistractingPopUp { get; set; }
        public static double CurrentFocusAlignmentScore { get; set; }
        public static string CustomToolTip { get; set; }
        static FocusAlignmentScoreManager()
        {

            // Initialize variables
            IsCuddlyModeEnabled = (appSettings.nFocusBearMode == 1) ? true : false;
            IsAiCheckUrlEnabled = appSettings.bAiCheckUrlEnabled;
            AppAllowedInFocusModeText = appSettings.ConfigFileStrings["common"]["appAllowedInFocusModeText"].ToString();
            AppNotAllowedInFocusModeText = appSettings.ConfigFileStrings["common"]["appNotAllowedInFocusModeText"].ToString();
            WhatToAchieveInFocusBlock = string.Empty;
            DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus = new Dictionary<string, Dictionary<string, object>>();
            ArrBrowsersLowerCasedNames = new List<string>();

            DefaultFocusAlignmentScoreToShowDistractingPopUp = 80;
            MinFocusAlignmentScoreToShowDistractingPopUp = 80;
            CurrentFocusAlignmentScore = 0;
        }

        public static void FocusModeOpenCurrentAppForAlignmentScore(string appNameLowercased, string focusModeName, double probability, bool isAppAllowedByAllowedAppList, string appName)
        {
            Console.WriteLine("////////focusModeOpenCurrentAppForAlignmentScore//////////// " + appName);

            IsCuddlyModeEnabled = (appSettings.nFocusBearMode == 1) ? true : false;
            IsAiCheckUrlEnabled = appSettings.bAiCheckUrlEnabled;

            if (appSettings.TryGetActiveSchedule(out var scheduleForAppScore))
            {
                if (!scheduleForAppScore.isPaused && scheduleForAppScore.bAIBlockingEnabled)
                {
                    IsAiCheckUrlEnabled = true;
                }
                else
                {
                    IsAiCheckUrlEnabled = false;
                }
            }
            else
            {
                IsAiCheckUrlEnabled = appSettings.bAiCheckUrlEnabled;
            }

            //Teacher Mode - the class session's ai_blocking_enabled overrides while it is active.
            if (Focus_Bear.Focus.TeacherSessionManager.Instance.IsTeacherSessionActive())
            {
                IsAiCheckUrlEnabled = Focus_Bear.Focus.TeacherSessionManager.Instance.bTeacherSessionAiBlockingEnabled;
            }


            CustomToolTip = "";
            Console.WriteLine("customToolTip blank set");

            if (IsCuddlyModeEnabled && IsAiCheckUrlEnabled)
            {
                if (DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus.TryGetValue(appNameLowercased, out var info))
                {
                    if (info.TryGetValue("isVisibleApp", out var isVisibleAppObj) && isVisibleAppObj is bool isVisibleApp)
                    {
                        if (!isVisibleApp)
                        {
                            FocusModeCloseCurrentAppForAlignmentScore();
                            info["isVisibleApp"] = true;
                            info["startedTime"] = DateTime.Now;
                            DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus[appNameLowercased] = info;
                        }
                    }

                    if (isAppAllowedByAllowedAppList)
                    {
                        info["probability"] = probability;
                    }

                    double currentProbability = info.ContainsKey("probability") ? Convert.ToDouble(info["probability"]) : 0;
                    if (currentProbability >= appSettings.sRunningFocusModeAlignmentScoreThreshold)
                    {
                        string reason = AppAllowedInFocusModeText.Replace("<APP>", appName).Replace("<FOCUSMODE>", focusModeName);
                        CustomToolTip = reason;
                    }
                    else
                    {
                        string reason = AppNotAllowedInFocusModeText.Replace("<APP>", appName).Replace("<FOCUSMODE>", focusModeName);
                        CustomToolTip = reason;
                    }

                    SetAppIconForFocusModeScore(currentProbability);
                }
                else
                {
                    FocusModeCloseCurrentAppForAlignmentScore();

                    if (!ArrBrowsersLowerCasedNames.Contains(appNameLowercased))
                    {
                        DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus[appNameLowercased] = new Dictionary<string, object>
                    {
                        { "probability", probability },
                        { "isVisibleApp", true },
                        { "totalSeconds", 0 },
                        { "startedTime", DateTime.Now }
                    };

                        if (probability >= appSettings.sRunningFocusModeAlignmentScoreThreshold)
                        {
                            string reason = AppAllowedInFocusModeText.Replace("<APP>", appName).Replace("<FOCUSMODE>", focusModeName);
                            CustomToolTip = reason;
                        }
                        else
                        {
                            string reason = AppNotAllowedInFocusModeText.Replace("<APP>", appName).Replace("<FOCUSMODE>", focusModeName);
                            CustomToolTip = reason;
                        }

                        SetAppIconForFocusModeScore(probability);
                    }
                }
            }
        }

        public static void FocusModeCloseCurrentAppForAlignmentScore()
        {
            Console.WriteLine("////////focusModeCloseCurrentAppForAlignmentScore//////////////---------------");
            var keys = DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus.Keys.ToList();
            foreach (var key in keys)
            {
                if (DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus.TryGetValue(key, out var dictionary))
                {
                    if (dictionary.TryGetValue("isVisibleApp", out var isVisibleAppObj) && isVisibleAppObj is bool isVisibleApp && isVisibleApp)
                    {
                        dictionary["isVisibleApp"] = false;
                        var startTime = dictionary.ContainsKey("startedTime") ? (DateTime)dictionary["startedTime"] : DateTime.Now;
                        var endTime = DateTime.Now;
                        var secondsBetween = (endTime - startTime).TotalSeconds;
                        var seconds = dictionary.ContainsKey("totalSeconds") ? Convert.ToInt32(dictionary["totalSeconds"]) : 0;
                        seconds += (int)secondsBetween;
                        dictionary["totalSeconds"] = seconds;
                        DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus[key] = dictionary;
                        break;
                    }
                }
            }
        }


        public static void FocusModeOpenCurrentUrlForAlignmentScore(string domain)
        {
            if (appSettings.TryGetActiveSchedule(out var scheduleForUrlScore))
            {
                if (!scheduleForUrlScore.isPaused && scheduleForUrlScore.bAIBlockingEnabled)
                {
                    IsAiCheckUrlEnabled = true;
                }
                else
                {
                    IsAiCheckUrlEnabled = false;
                }
            }
            else
            {
                IsAiCheckUrlEnabled = appSettings.bAiCheckUrlEnabled;
            }

            //Teacher Mode - the class session's ai_blocking_enabled overrides while it is active.
            if (Focus_Bear.Focus.TeacherSessionManager.Instance.IsTeacherSessionActive())
            {
                IsAiCheckUrlEnabled = Focus_Bear.Focus.TeacherSessionManager.Instance.bTeacherSessionAiBlockingEnabled;
            }

            if (IsCuddlyModeEnabled && IsAiCheckUrlEnabled)
            {
                if (DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus.TryGetValue(domain, out var info))
                {
                    if (info.TryGetValue("isVisibleUrl", out var isVisibleUrlObj) && isVisibleUrlObj is bool isVisibleUrl)
                    {
                        if (!isVisibleUrl)
                        {
                            FocusModeCloseCurrentUrlForAlignmentScore();
                            info["isVisibleUrl"] = true;
                            info["startedTime"] = DateTime.Now;
                            CustomToolTip = info.ContainsKey("reason") ? info["reason"].ToString() : "";
                            DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus[domain] = info;
                        }
                    }

                    double probability = info.ContainsKey("probability") ? Convert.ToDouble(info["probability"]) : 0;
                    SetAppIconForFocusModeScore(probability);
                }
                else
                {
                    SetFocusBearIconToNormal();
                    FocusModeCloseCurrentUrlForAlignmentScore();
                }
            }
        }

        public static void FocusModeCloseCurrentUrlForAlignmentScore()
        {
            var keys = DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus.Keys.ToList();
            foreach (var key in keys)
            {
                if (DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus.TryGetValue(key, out var dictionary))
                {
                    if (dictionary.TryGetValue("isVisibleUrl", out var isVisibleUrlObj) && isVisibleUrlObj is bool isVisibleUrl && isVisibleUrl)
                    {
                        dictionary["isVisibleUrl"] = false;
                        var startTime = dictionary.ContainsKey("startedTime") ? (DateTime)dictionary["startedTime"] : DateTime.Now;
                        var endTime = DateTime.Now;
                        var secondsBetween = (endTime - startTime).TotalSeconds;
                        var seconds = dictionary.ContainsKey("totalSeconds") ? Convert.ToInt32(dictionary["totalSeconds"]) : 0;
                        seconds += (int)secondsBetween;
                        dictionary["totalSeconds"] = seconds;
                        DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus[key] = dictionary;
                        break;
                    }
                }
            }
        }


        public static double CalculateFocusAlignmentScore()
        {
            double totalAlignmentScore = 0;
            int totalSeconds = 0;
            var keys = DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus.Keys.ToList();

            foreach (var key in keys)
            {
                if (DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus.TryGetValue(key, out var dictionary))
                {
                    int seconds = dictionary.ContainsKey("totalSeconds") ? Convert.ToInt32(dictionary["totalSeconds"]) : 0;
                    bool shouldCalculateSecondsOfCurrentUrl = false;

                    if (dictionary.TryGetValue("isVisibleApp", out var isVisibleAppObj) && isVisibleAppObj is bool isVisibleApp && isVisibleApp)
                    {
                        shouldCalculateSecondsOfCurrentUrl = true;
                    }

                    if (dictionary.TryGetValue("isVisibleUrl", out var isVisibleUrlObj) && isVisibleUrlObj is bool isVisibleUrl && isVisibleUrl)
                    {
                        shouldCalculateSecondsOfCurrentUrl = true;
                    }

                    if (shouldCalculateSecondsOfCurrentUrl)
                    {
                        var startTime = dictionary.ContainsKey("startedTime") ? (DateTime)dictionary["startedTime"] : DateTime.Now;
                        var endTime = DateTime.Now;
                        var secondsBetween = (endTime - startTime).TotalSeconds;
                        seconds += (int)secondsBetween;
                    }

                    double probability = dictionary.ContainsKey("probability") ? Convert.ToDouble(dictionary["probability"]) : 0;
                    double score = (seconds * probability);
                    totalSeconds += seconds;
                    totalAlignmentScore += score;
                }
            }

            if (totalAlignmentScore != 0)
            {
                totalAlignmentScore = (totalAlignmentScore / totalSeconds) * 100;
            }

            if (totalAlignmentScore > 100)
            {
                totalAlignmentScore = 100;
            }

            totalAlignmentScore = Math.Round(totalAlignmentScore * 100) / 100;
            CurrentFocusAlignmentScore = totalAlignmentScore;

            NotifyToDoPlayerFocusAlignmentScore(CurrentFocusAlignmentScore);

            return CurrentFocusAlignmentScore;
        }

        // Pushes the latest focus alignment score to the ToDoPlayer webview.
        // Centralised here so every score calculation (FocusProgress, GentleBlock, browser URL checks, etc.)
        // keeps the ToDoPlayer in sync without recalculating or polling on a timer.
        private static void NotifyToDoPlayerFocusAlignmentScore(double score)
        {
            try
            {
                bool aiActiveForSession = appSettings.isFocusModeRunning && appSettings.bAiCheckUrlEnabled;
                bool aiActiveForSchedule = appSettings.IsBlockingScheduleActiveAndNotPaused(out var scheduleForNotify)
                    && scheduleForNotify.bAIBlockingEnabled;

                if (!(aiActiveForSession || aiActiveForSchedule))
                    return;

                Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                {
                    ((App)(Application.Current)).wndFocusBearMainWindow?.PageToDoPlayer?.NotifyFocusAlignmentScore(score);
                }));
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("FocusAlignmentScoreManager.NotifyToDoPlayerFocusAlignmentScore - " + ex.Message);
            }
        }

        private static readonly object _lock = new object();
        public static double GetFocusAlignmentScoreFromLastEntry()
        {
            DateTime lastEntryTime = TabMetrics.Instance.lastEntryInProductivityStatsTime ?? DateTime.Now;

            double totalAlignmentScore = 0;
            int totalSeconds = 0;

            // -------------------------
            // INLINE SNAPSHOT (thread-safe)
            // -------------------------
            Dictionary<string, Dictionary<string, object>> snapshot;

            lock (_lock)
            {
                snapshot = DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus
                    .ToDictionary(
                        entry => entry.Key,
                        entry => new Dictionary<string, object>(entry.Value)
                    );
            }

            // -------------------------
            foreach (var kvp in snapshot)
            {
                var dictionary = kvp.Value;

                DateTime startTime = dictionary.ContainsKey("startedTime")
                    ? (DateTime)dictionary["startedTime"]
                    : DateTime.Now;

                int seconds = dictionary.ContainsKey("totalSeconds")
                    ? Convert.ToInt32(dictionary["totalSeconds"])
                    : 0;

                DateTime endTime = dictionary.ContainsKey("endTime")
                    ? (DateTime)dictionary["endTime"]
                    : DateTime.Now;

                bool shouldCalculateSeconds = false;

                if (dictionary.ContainsKey("isVisibleApp") &&
                    dictionary["isVisibleApp"] is bool isVisibleApp && isVisibleApp)
                {
                    shouldCalculateSeconds = true;
                }

                if (dictionary.ContainsKey("isVisibleUrl") &&
                    dictionary["isVisibleUrl"] is bool isVisibleUrl && isVisibleUrl)
                {
                    shouldCalculateSeconds = true;
                }

                // -------------------------
                if (startTime < lastEntryTime)
                {
                    if (shouldCalculateSeconds)
                    {
                        var secondsBetween = (DateTime.Now - lastEntryTime).TotalSeconds;
                        seconds = (int)secondsBetween;
                    }
                }

                if (!shouldCalculateSeconds)
                {
                    var secondsBetween = (endTime - lastEntryTime).TotalSeconds;
                    seconds = (int)secondsBetween;

                    if (seconds < 0)
                        continue;
                }
                else
                {
                    DateTime currentDate = DateTime.Now;

                    DateTime timeToCheck = lastEntryTime;

                    if (startTime > lastEntryTime)
                        timeToCheck = startTime;

                    int secondsBetween = (int)(currentDate - timeToCheck).TotalSeconds;

                    if (seconds > secondsBetween)
                        seconds = secondsBetween;

                    if (seconds < 0)
                        continue;
                }

                if (seconds < 0)
                    continue;

                double probability = dictionary.ContainsKey("probability")
                    ? Convert.ToDouble(dictionary["probability"])
                    : 0;

                double score = 100 * probability * seconds;

                totalSeconds += seconds;
                totalAlignmentScore += score;
            }

            if (totalAlignmentScore != 0 && totalSeconds > 0)
            {
                totalAlignmentScore = totalAlignmentScore / totalSeconds;
            }

            if (totalAlignmentScore > 100)
                totalAlignmentScore = 100;

            totalAlignmentScore = Math.Round(totalAlignmentScore, 2);

            return totalAlignmentScore;
        }

        public static void SetAppIconForFocusModeScore(double? probability)
        {
            try
            {
                if (probability == null)
                {
                    SetFocusBearIconToNormal();
                    FocusModeCloseCurrentUrlForAlignmentScore(); //URLs
                    FocusModeCloseCurrentAppForAlignmentScore(); //APPs
                }
                else
                {
                    if (probability >= appSettings.sRunningFocusModeAlignmentScoreThreshold)
                    {
                        Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                        {
                            if (((App)(Application.Current)).wndFocusProgress != null)
                            {
                                string imagePath = ("pack://application:,,,/Focus Bear;component/images/productive_icon.png");
                                var image = new BitmapImage();
                                image.BeginInit();
                                image.UriSource = new Uri(imagePath);
                                image.EndInit();
                                ((App)(Application.Current)).wndFocusProgress.TopIcon.Source = image;
                            }
                        }));
                    }
                    else
                    {
                        Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                        {
                            if (((App)(Application.Current)).wndFocusProgress != null)
                            {
                                string imagePath = ("pack://application:,,,/Focus Bear;component/images/notProductive_icon.png");
                                var image = new BitmapImage();
                                image.BeginInit();
                                image.UriSource = new Uri(imagePath);
                                image.EndInit();
                                ((App)(Application.Current)).wndFocusProgress.TopIcon.Source = image;
                            }
                        }));
                    }
                    Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                    {
                        if (((App)(Application.Current)).wndFocusProgress != null)
                        {
                            ((App)(Application.Current)).wndFocusProgress.TopIcon.ToolTip = CustomToolTip;
                        }
                    }));

                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetAppIconForFocusModeScore EX - " + ex.Message);
            }
        }

        public static bool CheckURLAlreadyCheckWithAI(string sURL)
        {
            if (FocusAlignmentScoreManager.DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus.TryGetValue(sURL, out var info))
            {
                string reason = info.ContainsKey("reason") ? info["reason"] as string ?? "" : "";
                Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                {
                    if (((App)(Application.Current)).wndFocusProgress != null)
                    {
                        ((App)(Application.Current)).wndFocusProgress.TopIcon.ToolTip = CustomToolTip;
                    }
                }));

                double probability = info.ContainsKey("probability") ? (double)info["probability"] : 0.0;
                double currentFocusAlignmentScore = CalculateFocusAlignmentScore();
                Console.WriteLine(currentFocusAlignmentScore);
                Console.WriteLine(probability);

                if (probability< 0.8)
                {
                    if (currentFocusAlignmentScore < appSettings.sRunningFocusModeAlignmentScoreThreshold)
                    {
                        // Unsafe url
                        //showPopupBlockConfirmWindow(
                        //    blockedAppName: "",
                        //    currentApp: null,
                        //    url: url,
                        //    script: script,
                        //    isBlockApp: false,
                        //    browser: browser,
                        //    tabTitle: tabTitle,
                        //    fullUrl: fullUrl,
                        //    aiUrlBlockReason: reason,
                        //    isTimeOut: false);
                    }
                }
            }
            return false;
        }
        public static void SetFocusBearIconToNormal()
        {
            try
            {
                Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                {

                    if (((App)(Application.Current)).wndFocusProgress != null)
                    {
                        string imagePath = ("pack://application:,,,/Focus Bear;component/images/FBicon-64.png");
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.UriSource = new Uri(imagePath);
                        image.EndInit();
                        ((App)(Application.Current)).wndFocusProgress.TopIcon.Source = image;
                        ((App)(Application.Current)).wndFocusProgress.TopIcon.ToolTip = "";
                        CustomToolTip = "";
                    }
                }));
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetFocusBearIconToNormal EX - " + ex.Message);
            }
        }
    }

}
