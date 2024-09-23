using Focus_Bear.API;
using Focus_Bear.Focus;
using Focus_Bear.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Threading;

namespace Focus_Bear.Utils
{
    public static class ProcessBrowserURLs
    {
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private static string sAlwaysBlockedWebUrl = "file:///" + AppSettings.FocusBlockPagePath + "?old_url={0}&block_type=always-blocked&block_mode={1}&version={2}";
        private static string sFcousBlockedWebUrl = "file:///" + AppSettings.FocusBlockPagePath + "?old_url={0}&focus_mode_end_time={1}&focus_mode={2}&focus_mode_intention={3}&version={4}";
        private static string sActivityBlockedWebUrl = "file:///" + AppSettings.FocusBlockPagePath + "?old_url={0}&block_type={1}&version={2}"; //evening-habit, morning-habit, break-activity

        public static readonly List<string> UNBLOCKED_URLS = new List<string> { "about:blank",
            "chrome://",
            "favorites:",
            "eyst-focus.html",
            "earnyourscreentime.com",
            "devtools://",
            "chrome-extension://",
            "focusbear.io",
            "view-source:",
            "about:blank",
            "focus-bear.github.io",
            "focus-block.html","brave://","localhost",};

       
        public static void ProcessCurrentURL(IntPtr hwnd)
        {
            AppSettings appSettings = AppSettings.GetInstance;

            //In case the set intention window is open, we should hide it
            appSettings.Hide_SetIntentionWindow();

            if (appSettings.bHabitwithAllowedAppsRunning && !appSettings.bHabitwithUseFocusModeRunning)//
            {
                
                //If its evening routine and the relax activity is on, no need to check anhything or block anything
                if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    if (appSettings.arrAllMorningHabits[appSettings.nHabitIndex].show_saved_distracting_websites)
                    {
                        return;
                    }
                }
                
                ProcessAlloweURLsInActivity(hwnd);
            }

            if (appSettings.isFocusModeRunning || appSettings.bHabitwithUseFocusModeRunning)
            {
                ProcessURLForFocusModes(hwnd);
            }

            if (appSettings.bHabitwithAllowedAppsRunning || appSettings.isFocusModeRunning)
                return;

            if(appSettings.IsAppDeactivated())//no need to track always blocked urls
            {
                return;
            }

            if (appSettings.bIsUserOnCall && !appSettings.bBlockSuperDistractingSitesDuringMeetings)
            {
                LogWriter.LogWrite("User is on Call - bBlockSuperDistractingSitesDuringMeetings false, return");
                return;
            }

            if (appSettings.arrAlwaysBlockedURLs.Count() > 0)
                ProcessCurrentAlwaysBlockedURL(hwnd); //For always Blocked URLS
        }

        static string ExtractOldUrl(string input)
        {
            // Regular expression to find the string between old_url= and &block_type=
            string pattern = @"[?&]old_url=([^&]+)";
            Match match = Regex.Match(input, pattern);

            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return null; // Return null if no match is found
        }

        public static bool ProcessCurrentAlwaysBlockedURL(IntPtr hwnd)
        {
            AppSettings appSettings = AppSettings.GetInstance;

            if (appSettings.FirefoxLastAccWindow != null && (appSettings.sLastActiveURLInFirefox != null && appSettings.sLastActiveURLInFirefox.Length > 0 ))
            {
                //Check if the user is trying to unblock the URL that is blocked using Oops I actually need this
                if(appSettings.sLastActiveURLInFirefox.Contains("&unblock_it=true"))
                {
                    string oldURLToOpen = ExtractOldUrl(appSettings.sLastActiveURLInFirefox);

                    if (oldURLToOpen != null && oldURLToOpen.Length > 0)
                    {
                        string sDomain = "";
                        try
                        {
                            Uri myUri = new Uri(oldURLToOpen);
                            string host = myUri.Host; //fails if there is no http://
                            sDomain = host;
                        }
                        catch (Exception ex)
                        {
                            /*
                            This constructor initializes a new instance of the UriBuilder class with the Fragment, Host, Path, Port, Query, Scheme, and Uri properties set as specified in uri.
                            If uri does not specify a scheme, the scheme defaults to "http:".
                            */
                            Uri myUri = new UriBuilder(oldURLToOpen).Uri;
                            sDomain = myUri.Host;
                        }

                        //Open the url and also add it to temporary allowed URL list
                        var blockedURLs = appSettings.TemporarryBlockedURLsWithSetIntention.Where(i => i.ToLower() == sDomain.ToLower());

                        if (blockedURLs.Count() != 0)
                        {
                            appSettings.TemporarryBlockedURLsWithSetIntention.Remove(sDomain);
                        }

                        //Now fire the OLD url so that it will ask for intention again
                        appSettings.FirefoxLastAccWindow.accValue[appSettings.objChild] = oldURLToOpen;
                        SetForegroundWindow(hwnd);
                        Task.Delay(1);
                        appSettings.FirefoxLastAccWindow.accSelect(0x1, appSettings.objChild);//take selection

                        SendKeys.SendWait("{ENTER}");

                        appSettings.CapturePostHog("unblock-distracting-url");
                    }
                }
                else if(appSettings.sLastActiveURLInFirefox.Contains("focus-block.html"))
                {
                    //return if its a normal block page
                    return false;
                }
                //Check if its an always unblocked url
                for (int m = 0; m < UNBLOCKED_URLS.Count(); m++)
                {
                    string sToCheck = UNBLOCKED_URLS[m];
                    if (appSettings.sLastActiveURLInFirefox.Contains(sToCheck))
                    {
                        return false;
                    }
                }

                foreach (string url in appSettings.arrAlwaysBlockedURLs)
                {
                    
                    {

                        var sUrlToCheck = appSettings.sLastActiveURLInFirefox.Replace("http://", "");
                        sUrlToCheck = sUrlToCheck.Replace("https://", "");
                        sUrlToCheck = sUrlToCheck.Replace("www.", "");

                        var allowedURL = url.Replace("http://", "");
                        allowedURL = allowedURL.Replace("https://", "");
                        allowedURL = allowedURL.Replace("www.", "");
                        //Remove last slash from allowed url
                        allowedURL = allowedURL.TrimEnd(new[] { '/' });

                        if (sUrlToCheck.StartsWith(allowedURL))
                        {
                            LogWriter.LogWrite(sUrlToCheck + "found in ALWAYS blocked URLS - BLOCK");
                            suppressURLForFirefox(hwnd,appSettings.sLastActiveURLInFirefox, "blocked");

                            return true;
                        }
                        else
                        {
                            allowedURL = "*" + allowedURL + "*";
                            if (MatchWildcardString(sUrlToCheck, allowedURL))
                            {

                                LogWriter.LogWrite(sUrlToCheck + "found in ALWAYS blocked URLS MatchWildcardString - BLOCK");
                                suppressURLForFirefox(hwnd, appSettings.sLastActiveURLInFirefox, "blocked");

                                return true;
                            }
                            else
                            {
                                try
                                {
                                    //Check title
                                    const int nChars = 256;
                                    StringBuilder Buff = new StringBuilder(nChars);
                                    if (GetWindowText(hwnd, Buff, nChars) > 0)
                                    {
                                        string sTitle = Buff.ToString();
                                        if (MatchWildcardString(sTitle, allowedURL))
                                        {
                                            LogWriter.LogWrite(sTitle + "title found in ALWAYS blocked URLS MatchWildcardString - BLOCK");
                                            suppressURLForFirefox(hwnd, appSettings.sLastActiveURLInFirefox, "blocked");

                                            return true;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogWriter.LogWrite("alwaysblockedurls GetWindowText blockedurls EX - " + ex.Message);
                                }

                            }
                        }
                    }
                }
            }

            appSettings.Hide_ShowIntentionWindow();
            Console.WriteLine("Hide_ShowIntentionWindow from processbrowserurl");

            return false;
        }

        

        private static void ProcessURLForFocusModes(IntPtr hWnd )
        {
            AppSettings appSettings = AppSettings.GetInstance;

            string sURL = "";
            if (appSettings.FirefoxLastAccWindow != null && (appSettings.sLastActiveURLInFirefox != null && appSettings.sLastActiveURLInFirefox.Length > 0 && (!appSettings.sLastActiveURLInFirefox.Contains("focus-block.html"))))
            {
                sURL = appSettings.sLastActiveURLInFirefox;
            }
            else
            {
                if ((appSettings.nFocusBearMode == 1) && appSettings.bAiCheckUrlEnabled)
                {
                    FocusAlignmentScoreManager.FocusModeCloseCurrentUrlForAlignmentScore();
                    FocusAlignmentScoreManager.SetAppIconForFocusModeScore(null);
                }
                return;
            }
            if (sURL.Contains("file://"))
            {
                if ((appSettings.nFocusBearMode == 1) && appSettings.bAiCheckUrlEnabled)
                {
                    FocusAlignmentScoreManager.FocusModeCloseCurrentUrlForAlignmentScore();
                    FocusAlignmentScoreManager.SetAppIconForFocusModeScore(null);
                }

                return;
            }
            if (appSettings.nFocusBearMode == 1)
            {
                if (sURL.Contains("focus_bear_temporarily_allow=true"))//If user have clicked I need this URL from blocked page, ignore it
                {
                    return;
                }
            }

            //Check if its an always unblocked url
            for (int m = 0; m < UNBLOCKED_URLS.Count(); m++)
            {
                string sToCheck = UNBLOCKED_URLS[m];
                if (sURL.Contains(sToCheck))
                {
                    if ((appSettings.nFocusBearMode == 1) && appSettings.bAiCheckUrlEnabled)
                    {
                        FocusAlignmentScoreManager.FocusModeCloseCurrentUrlForAlignmentScore();
                        FocusAlignmentScoreManager.SetAppIconForFocusModeScore(null);
                    }
                    return;
                }
            }

                       
            
            FocusAlignmentScoreManager.FocusModeOpenCurrentUrlForAlignmentScore(sURL);

            bool bAllowedURLFound = true; //by default allowed url is true

            List<FocusMode> listItems = new List<FocusMode>();

            if (appSettings.bHabitwithUseFocusModeRunning)
            {
                listItems = appSettings.arrFocusModes.Where(i => i.id == appSettings.sUseFocusModeIDInActivity).ToList();
            }
            else //Normal focus mode running
            {
                listItems = appSettings.arrFocusModes.Where(i => i.isOn == true).ToList();
            }

            if (listItems.Count() != 0)
            {
                FocusMode focusType = listItems.First();
                
                if(focusType.isRelaxFocus)
                {
                    //if its relax focus mode, we will not block anything, so return from here
                    return;
                }

                if (focusType != null )
                {
                    if (focusType.isAllowedListApproach)//Block everything except safe sites I choose
                    {
                        LogWriter.LogWrite("isAllowedListApproach - Block everything except safe sites I choose - " + focusType.name);

                        {
                            LogWriter.LogWrite("isAllowedListApproach - Selected URLs are allowed");
                            //We will first check if the url is in the blocked urls list of the focusmode, then we will not check allowed
                            bAllowedURLFound = true; //allowed unless we find it in the blocked urls array

                               //check if the current URL is in the list of Blocked urls, then block else allow
                            foreach (string url in focusType.arrBlockedUrls)
                            {
                                
                                {
                                    var sUrlToCheck = sURL.Replace("http://", "");
                                    sUrlToCheck = sUrlToCheck.Replace("https://", "");
                                    sUrlToCheck = sUrlToCheck.Replace("www.", "");

                                    var allowedURL = url.Replace("http://", "");
                                    allowedURL = allowedURL.Replace("https://", "");
                                    allowedURL = allowedURL.Replace("www.", "");
                                    //Remove last slash from allowed url
                                    allowedURL = allowedURL.TrimEnd(new[] { '/' });

                                    if (sUrlToCheck.StartsWith(allowedURL))
                                    {
                                        LogWriter.LogWrite(sUrlToCheck + " found in blocked URLS of focus mode - BLOCK");
                                        bAllowedURLFound = false;
                                        break;
                                    }
                                    else
                                    {

                                        allowedURL = "*" + allowedURL + "*";
                                        if (MatchWildcardString(sUrlToCheck, allowedURL))
                                        {
                                            LogWriter.LogWrite(sUrlToCheck + " found in blocked URLS WILDCARD match of focus mode - BLOCK");
                                            bAllowedURLFound = false;
                                            break;
                                        }
                                        else
                                        {
                                            try
                                            {
                                                //Check title
                                                const int nChars = 256;
                                                StringBuilder Buff = new StringBuilder(nChars);
                                                if (GetWindowText(hWnd, Buff, nChars) > 0)
                                                {
                                                    string sTitle = Buff.ToString();
                                                    if (MatchWildcardString(sTitle, allowedURL))
                                                    {
                                                        LogWriter.LogWrite(sTitle + "title found in blocked URLS WILDCARD match of focus mode - BLOCK");
                                                        bAllowedURLFound = false;
                                                        break;
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                LogWriter.LogWrite("processCurrentFocusElement GetWindowText blockedurls EX - " + ex.Message);
                                            }

                                        }
                                    }
                                }
                            }

                            if (bAllowedURLFound)//Means it was not found in the blocked urls..now let's check Allowed URLs
                            {
                                bAllowedURLFound = false;

                                //check if the current URL is in the list of allowed urls, then allow else block
                                foreach (string url in focusType.arrAllowedUrls)
                                {
                                    {
                                        var sUrlToCheck = sURL.Replace("http://", "");
                                        sUrlToCheck = sUrlToCheck.Replace("https://", "");
                                        sUrlToCheck = sUrlToCheck.Replace("www.", "");

                                        var allowedURL = url.Replace("http://", "");
                                        allowedURL = allowedURL.Replace("https://", "");
                                        allowedURL = allowedURL.Replace("www.", "");

                                        //Remove last slash from allowed url
                                        allowedURL = allowedURL.TrimEnd(new[] { '/' });

                                        if (sUrlToCheck.StartsWith(allowedURL))
                                        {
                                            LogWriter.LogWrite(sUrlToCheck + " found in ALLOWED URLS of focus mode - ALLOW");
                                            bAllowedURLFound = true;
                                            ///return;
                                        }
                                        else
                                        {
                                            allowedURL = "*" + allowedURL + "*";
                                            if (MatchWildcardString(sUrlToCheck, allowedURL))
                                            {
                                                LogWriter.LogWrite(sUrlToCheck + " found in ALLOWED URLS WILDCARD match of focus mode - ALLOW");

                                                bAllowedURLFound = true;
                                                ///return;
                                            }
                                            else
                                            {
                                                try
                                                {
                                                    //Check title
                                                    const int nChars = 256;
                                                    StringBuilder Buff = new StringBuilder(nChars);
                                                    if (GetWindowText(hWnd, Buff, nChars) > 0)
                                                    {
                                                        string sTitle = Buff.ToString();
                                                        if (MatchWildcardString(sTitle, allowedURL))
                                                        {
                                                            LogWriter.LogWrite(sTitle + "title found in ALLOWED URLS WILDCARD match of focus mode - ALLOW");
                                                            bAllowedURLFound = true;
                                                            ///return;
                                                        }
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    LogWriter.LogWrite("processCurrentFocusElement GetWindowText ALLOWED URLS EX - " + ex.Message);
                                                }

                                            }
                                        }

                                    }

                                    if(bAllowedURLFound)
                                    {
                                        string sHeading = appSettings.ConfigFileStrings["common"]["appAllowedInFocusModeText"].ToString();
                                        sHeading = sHeading.Replace("<APP>", sURL);
                                        sHeading = sHeading.Replace("<FOCUSMODE>", appSettings.sRunningFocusModeName);
                                  
                                        //Safe URL, need to allow.
                                        if (!FocusAlignmentScoreManager.DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus.ContainsKey(sURL))
                                        {

                                            FocusAlignmentScoreManager.DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus[sURL] = new Dictionary<string, object>
                                            {
                                                { "probability", 1.0 },
                                                { "isVisibleUrl", true },
                                                { "totalSeconds", 0 },
                                                { "startedTime", DateTime.Now },
                                                { "reason", sHeading }
                                            };

                                            FocusAlignmentScoreManager.CustomToolTip = sHeading;
                                            FocusAlignmentScoreManager.SetAppIconForFocusModeScore(1);
                                        }

                                        double currentFocusAlignmentScore = FocusAlignmentScoreManager.CalculateFocusAlignmentScore();
                                        Console.WriteLine("*****************************currentFocusAlignmentScore");
                                        Console.WriteLine(currentFocusAlignmentScore);

                                        Task.Delay(1000);
                                        return;
                                    }
                                }
                            }

                            if (!bAllowedURLFound)
                            {
                                LogWriter.LogWrite("PBU processCurrentFocusElement  isAllowedListApproach - Attempt to Suppress URL");

                                //Check the URL in Always BLocked URL List
                                {
                                    if (appSettings.arrAlwaysBlockedURLs.Count() > 0)
                                    {
                                        if (ProcessCurrentAlwaysBlockedURL(hWnd))//For always Blocked URLS
                                        {
                                            return; //return as this is handled byy always blocked url
                                        }
                                    }
                                }

                                suppressURLForFirefox(hWnd, sURL, "focus");
                                return;
                            }
                        }
                    }
                    else //Allow everything except districting sites
                    {
                        LogWriter.LogWrite("isBlockedListApproach - Allow everything except districting sites");

                        {
                            LogWriter.LogWrite("isBlockedListApproach - selected URLS are blocked");

                            bAllowedURLFound = true; //allowed unless we find it in the blocked urls array

                            //check if the current URL is in the list of Blocked urls, then block else allow
                            foreach (string url in focusType.arrBlockedUrls)
                            {
                                {
                                    var sUrlToCheck = sURL.Replace("http://", "");
                                    sUrlToCheck = sUrlToCheck.Replace("https://", "");
                                    sUrlToCheck = sUrlToCheck.Replace("www.", "");

                                    var allowedURL = url.Replace("http://", "");
                                    allowedURL = allowedURL.Replace("https://", "");
                                    allowedURL = allowedURL.Replace("www.", "");
                                    //Remove last slash from allowed url
                                    allowedURL = allowedURL.TrimEnd(new[] { '/' });

                                    if (sUrlToCheck.StartsWith(allowedURL))
                                    {
                                        LogWriter.LogWrite(sUrlToCheck + " (BLocked List APproach) found in blocked URLS of focus mode - BLOCK");
                                        bAllowedURLFound = false;
                                        break;
                                    }
                                    else
                                    {
                                        allowedURL = "*" + allowedURL + "*";
                                        if (MatchWildcardString(sUrlToCheck, allowedURL))
                                        {
                                            bAllowedURLFound = false;
                                            LogWriter.LogWrite(sUrlToCheck + " (BLocked List APproach MatchWildcardString) found in blocked URLS of focus mode - BLOCK");
                                            break;
                                        }
                                        else
                                        {
                                            try
                                            {
                                                //Check title
                                                const int nChars = 256;
                                                StringBuilder Buff = new StringBuilder(nChars);
                                                if (GetWindowText(hWnd, Buff, nChars) > 0)
                                                {
                                                    string sTitle = Buff.ToString();
                                                    if (MatchWildcardString(sTitle, allowedURL))
                                                    {
                                                        LogWriter.LogWrite(sTitle + "title found in (BLocked List APproach MatchWildcardString) match of focus mode - BLOCK");
                                                        bAllowedURLFound = false;
                                                        break;
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                LogWriter.LogWrite("processCurrentFocusElement GetWindowText blockedurls EX - " + ex.Message);
                                            }

                                        }
                                    }
                                }
                            }
                            if (!bAllowedURLFound)
                            {
                                LogWriter.LogWrite("processCurrentFocusElement isBlockedListApproach - Attempt to Suppress URL");
                                suppressURLForFirefox(hWnd, sURL, "focus");
                               
                                return;
                            }
                            else //Check the URL in Always BLocked URL List
                            {
                                if (appSettings.arrAlwaysBlockedURLs.Count() > 0)
                                    ProcessCurrentAlwaysBlockedURL(hWnd); //For always Blocked URLS
                            }
                        }
                    }
                }
            }

        }

        private static void ProcessAlloweURLsInActivity(IntPtr hWnd )
        {
            try
            {
                AppSettings appSettings = AppSettings.GetInstance;

                string sURL = "";
                if (appSettings.FirefoxLastAccWindow != null && (appSettings.sLastActiveURLInFirefox != null && appSettings.sLastActiveURLInFirefox.Length > 0 && (!appSettings.sLastActiveURLInFirefox.Contains("focus-block.html"))))
                {
                    sURL = appSettings.sLastActiveURLInFirefox;
                }
                else
                {
                    return;
                }

                {
                    if (sURL.Contains("file://"))
                    {
                        return;
                    }
                    //Check if its an always unblocked url
                    for (int m = 0; m < UNBLOCKED_URLS.Count(); m++)
                    {
                        string sToCheck = UNBLOCKED_URLS[m];
                        if (sURL.Contains(sToCheck))
                        {
                            return;
                        }
                    }

                    bool bAllowedURLFound = false;
                    string sHabitType = "";
                    if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                    {
                        sHabitType = "morning-habit";
                        foreach (string url in appSettings.arrAllMorningHabits[appSettings.nHabitIndex].allowedUrls)
                        {
                            {
                                var sUrlToCheck = sURL.Replace("http://", "");
                                sUrlToCheck = sUrlToCheck.Replace("https://", "");
                                sUrlToCheck = sUrlToCheck.Replace("www.", "");

                                var allowedURL = url.Replace("http://", "");
                                allowedURL = allowedURL.Replace("https://", "");
                                allowedURL = allowedURL.Replace("www.", "");

                                //Remove last slash from allowed url
                                allowedURL = allowedURL.TrimEnd(new[] { '/' });

                                if (sUrlToCheck.StartsWith(allowedURL))
                                {
                                    LogWriter.LogWrite(sUrlToCheck + " found in ALLOWED URLS of MORNING_HABITS - ALLOW");
                                    bAllowedURLFound = true;
                                    //break;
                                    return;
                                }
                                else
                                {
                                    allowedURL = "*" + allowedURL + "*";
                                    if (MatchWildcardString(sUrlToCheck, allowedURL))
                                    {
                                        LogWriter.LogWrite(sUrlToCheck + " found in ALLOWED URLS MatchWildcardString of MORNING_HABITS - ALLOW");
                                        bAllowedURLFound = true;
                                        //break;
                                        return;
                                    }
                                    else
                                    {
                                        try
                                        {
                                            //Check title
                                            const int nChars = 256;
                                            StringBuilder Buff = new StringBuilder(nChars);
                                            if (GetWindowText(hWnd, Buff, nChars) > 0)
                                            {
                                                string sTitle = Buff.ToString();
                                                if (MatchWildcardString(sTitle, allowedURL))
                                                {
                                                    LogWriter.LogWrite(sTitle + "titlefound in ALLOWED URLS MatchWildcardString of MORNING_HABITS - ALLOW");
                                                    bAllowedURLFound = true;
                                                    return;
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            LogWriter.LogWrite("ProcessAlloweURLsInActivity GetWindowText MORNING_HABITS ALLOWED URLS EX - " + ex.Message);
                                        }

                                    }
                                }
                            }
                        }
                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                    {
                        sHabitType = "evening-habit";

                        foreach (string url in appSettings.arrAllEveningHabits[appSettings.nHabitIndex].allowedUrls)
                        {
                            
                            {
                                var sUrlToCheck = sURL.Replace("http://", "");
                                sUrlToCheck = sUrlToCheck.Replace("https://", "");
                                sUrlToCheck = sUrlToCheck.Replace("www.", "");

                                var allowedURL = url.Replace("http://", "");
                                allowedURL = allowedURL.Replace("https://", "");
                                allowedURL = allowedURL.Replace("www.", "");
                                //Remove last slash from allowed url
                                allowedURL = allowedURL.TrimEnd(new[] { '/' });

                                if (sUrlToCheck.StartsWith(allowedURL))
                                {
                                    LogWriter.LogWrite(sUrlToCheck + " found in ALLOWED URLS of EVENING_HABITS - ALLOW");
                                    bAllowedURLFound = true;
                                    // break;
                                    return;
                                }
                                else
                                {
                                    allowedURL = "*" + allowedURL + "*";
                                    if (MatchWildcardString(sUrlToCheck, allowedURL))
                                    {
                                        LogWriter.LogWrite(sUrlToCheck + " found in ALLOWED URLS MatchWildcardString of EVENING_HABITS - ALLOW");

                                        bAllowedURLFound = true;
                                        //break;
                                        return;
                                    }
                                    else
                                    {
                                        try
                                        {
                                            //Check title
                                            const int nChars = 256;
                                            StringBuilder Buff = new StringBuilder(nChars);
                                            if (GetWindowText(hWnd, Buff, nChars) > 0)
                                            {
                                                string sTitle = Buff.ToString();
                                                if (MatchWildcardString(sTitle, allowedURL))
                                                {
                                                    LogWriter.LogWrite(sTitle + "titlefound in ALLOWED URLS MatchWildcardString of EVENING_HABITS - ALLOW");
                                                    bAllowedURLFound = true;
                                                    return;
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            LogWriter.LogWrite("ProcessAlloweURLsInActivity GetWindowText EVENING_HABITS ALLOWED URLS EX - " + ex.Message);
                                        }

                                    }
                                }
                            }
                        }
                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                    {
                        sHabitType = "break-activity";


                        if (appSettings.isMandatoryActivityGoing)
                        {
                            foreach (string url in appSettings.arrActivitiesIncludeInEveryBreak[appSettings.nHabitIndex].allowedUrls)
                            {
                                {
                                    var sUrlToCheck = sURL.Replace("http://", "");
                                    sUrlToCheck = sUrlToCheck.Replace("https://", "");
                                    sUrlToCheck = sUrlToCheck.Replace("www.", "");

                                    var allowedURL = url.Replace("http://", "");
                                    allowedURL = allowedURL.Replace("https://", "");
                                    allowedURL = allowedURL.Replace("www.", "");

                                    //Remove last slash from allowed url
                                    allowedURL = allowedURL.TrimEnd(new[] { '/' });

                                    if (sUrlToCheck.StartsWith(allowedURL))
                                    {
                                        LogWriter.LogWrite(sUrlToCheck + " found in ALLOWED URLS of BREAK_ACTIVITY - ALLOW");
                                        bAllowedURLFound = true;
                                        // break;
                                        return;
                                    }
                                    else
                                    {
                                        allowedURL = "*" + allowedURL + "*";
                                        if (MatchWildcardString(sUrlToCheck, allowedURL))
                                        {
                                            LogWriter.LogWrite(sUrlToCheck + " found in ALLOWED URLS MatchWildcardString of BREAK_ACTIVITY - ALLOW");
                                            bAllowedURLFound = true;
                                            //break;
                                            return;
                                        }
                                        else
                                        {
                                            try
                                            {
                                                //Check title
                                                const int nChars = 256;
                                                StringBuilder Buff = new StringBuilder(nChars);
                                                if (GetWindowText(hWnd, Buff, nChars) > 0)
                                                {
                                                    string sTitle = Buff.ToString();
                                                    if (MatchWildcardString(sTitle, allowedURL))
                                                    {
                                                        LogWriter.LogWrite(sTitle + " - titlefound in ALLOWED URLS MatchWildcardString of BREAK_ACTIVITY - ALLOW");
                                                        bAllowedURLFound = true;
                                                        return;
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                LogWriter.LogWrite("ProcessAlloweURLsInActivity GetWindowText BREAK_ACTIVITY ALLOWED URLS EX - " + ex.Message);
                                            }

                                        }
                                    }
                                }
                            }

                        }
                        else
                        {
                            foreach (string url in appSettings.arrActivitiesExcludeInEveryBreak[appSettings.nHabitIndex].allowedUrls)
                            {
                                {
                                    var sUrlToCheck = sURL.Replace("http://", "");
                                    sUrlToCheck = sUrlToCheck.Replace("https://", "");
                                    sUrlToCheck = sUrlToCheck.Replace("www.", "");

                                    var allowedURL = url.Replace("http://", "");
                                    allowedURL = allowedURL.Replace("https://", "");
                                    allowedURL = allowedURL.Replace("www.", "");
                                    //Remove last slash from allowed url
                                    allowedURL = allowedURL.TrimEnd(new[] { '/' });

                                    if (sUrlToCheck.StartsWith(allowedURL))
                                    {
                                        LogWriter.LogWrite(sUrlToCheck + " found in ALLOWED URLS of BREAK_ACTIVITY - ALLOW");
                                        bAllowedURLFound = true;
                                        // break;
                                        return;
                                    }
                                    else
                                    {
                                        allowedURL = "*" + allowedURL + "*";
                                        if (MatchWildcardString(sUrlToCheck, allowedURL))
                                        {
                                            LogWriter.LogWrite(sUrlToCheck + " found in ALLOWED URLS MatchWildcardString of BREAK_ACTIVITY - ALLOW");
                                            bAllowedURLFound = true;
                                            //break;
                                            return;
                                        }
                                        else
                                        {
                                            try
                                            {
                                                //Check title
                                                const int nChars = 256;
                                                StringBuilder Buff = new StringBuilder(nChars);
                                                if (GetWindowText(hWnd, Buff, nChars) > 0)
                                                {
                                                    string sTitle = Buff.ToString();
                                                    if (MatchWildcardString(sTitle, allowedURL))
                                                    {
                                                        LogWriter.LogWrite(sTitle + " - titlefound in ALLOWED URLS MatchWildcardString of BREAK_ACTIVITY - ALLOW");
                                                        bAllowedURLFound = true;
                                                        return;
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                LogWriter.LogWrite("ProcessAlloweURLsInActivity GetWindowText BREAK_ACTIVITY ALLOWED URLS EX - " + ex.Message);
                                            }

                                        }
                                    }
                                }
                            }
                        }

                    }

                    if (!bAllowedURLFound)
                    {
                        LogWriter.LogWrite(sURL + " BLOCKED URL found in activity - BLOCK");
                        // "https://focus-bear.github.io/assets/focus-blocked?old_url={0}&block_type={0}"; //evening-habit, morning-habit, break-activity
                        suppressURLForFirefox(hWnd, sURL, sHabitType);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ProcessAlloweURLsInActivity - " + ex.Message);
            }


        }

        private static Boolean MatchWildcardString(String input, String pattern)
        {

            String regexPattern = "^";

            foreach (Char c in pattern)
            {
                switch (c)
                {
                    case '*':
                        regexPattern += ".*";
                        break;
                    case '?':
                        regexPattern += ".";
                        break;
                    default:
                        regexPattern += "[" + c + "]";
                        break;
                }
            }
            return new Regex(regexPattern + "$").IsMatch(input);
        }
        public static void suppressURLForFirefox(IntPtr hWnd, string val, string type, string focusMode = null)
        {
            AppSettings appSettings = AppSettings.GetInstance;

            try
            {
                LogWriter.LogWrite("suppressURLForFirefox - Block URL - " + val);


                if (type == "focus")
                {
                    if (appSettings.nFocusBearMode == 1)//Cuddly bearMode
                    {
                        string sURLForFocusMode = "";
                        sURLForFocusMode = val;
                       /** try
                        {
                            Uri myUri = new Uri(val);
                            string host = myUri.Host; //fails if ther eis no http://
                            sURLForFocusMode = host;
                            Debug.WriteLine(sURLForFocusMode);
                        }
                        catch (Exception ex)
                        {
                            /*
                            This constructor initializes a new instance of the UriBuilder class with the Fragment, Host, Path, Port, Query, Scheme, and Uri properties set as specified in uri.
                            If uri does not specify a scheme, the scheme defaults to "http:".
                            * /
                            Uri myUri = new UriBuilder(val).Uri;
                            sURLForFocusMode = myUri.Host;
                            Debug.WriteLine(ex.Message);
                        }**/

                        var allowedURLs = appSettings.TemporaryAllowedURLsGentleBlock.Where(i => i.sURL.ToLower() == sURLForFocusMode.ToLower());
                        if (allowedURLs.Count() != 0)
                        {
                            //found in allowed url
                            //if found in allowed URLs then add the title of what ever tab that is opened to the keywords list to send to server
                            if (hWnd != IntPtr.Zero)
                            {
                                try
                                {
                                    //Check title
                                    const int nChars = 256;
                                    StringBuilder Buff = new StringBuilder(nChars);
                                    if (GetWindowText(hWnd, Buff, nChars) > 0)
                                    {
                                        string sTitle = Buff.ToString();

                                        sTitle = sTitle.Replace(" - Google Chrome", "");
                                        sTitle = sTitle.Replace(" — Mozilla Firefox", "");
                                        sTitle = sTitle.Replace(" - Microsoft? Edge", "");

                                        if (sTitle.Length > 0)
                                        {
                                            var titleExists = appSettings.TemporaryAllowedURLsTabTitles.Where(i => i.ToLower() == sTitle.ToLower());
                                            if (titleExists.Count() == 0)
                                            {
                                                //add to list
                                                appSettings.TemporaryAllowedURLsTabTitles.Add(sTitle);
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogWriter.LogWrite("suppressURLForFirefox GetWindowText blockedurls EX - " + ex.Message);
                                }
                            }
                            LogWriter.LogWrite("Found in appSettings.TemporaryAllowedURLsGentleBlock - returning - " + sURLForFocusMode);
                            return;//allow the url
                        }
                        else
                        {
                            var blockedURLs = appSettings.TemporarryBlockedURLsGentleBlock.Where(i => i.ToLower() == sURLForFocusMode.ToLower());

                            if (blockedURLs.Count() != 0)
                            {
                                //found in blocked urls..block
                            }
                            else
                            {
                                string sTitle = "";
                                try
                                {
                                    //Check title
                                    const int nChars = 256;
                                    StringBuilder Buff = new StringBuilder(nChars);
                                    if (GetWindowText(hWnd, Buff, nChars) > 0)
                                    {
                                        sTitle = Buff.ToString();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogWriter.LogWrite("suppressURLForFirefox 1 GetWindowText blockedurls EX - " + ex.Message);
                                }

                                appSettings.ShowGentleBlockWindow(hWnd, "", val, sTitle); //we will send full url here
                                return;
                            }
                        }
                    }


                    if (appSettings.bHabitwithUseFocusModeRunning)
                    {
                        //string sActivityBlockedWebUrl = "https://focus-bear.github.io/assets/focus-blocked?old_url={0}&block_type={1}"; //evening-habit, morning-habit, break-activity
                        string sURL = string.Format(sActivityBlockedWebUrl, val, appSettings.sHabitTypeForUseFocusModeIDInActivity, appSettings.AppVersion.ToString());
                        if (appSettings.nFocusBearMode == 1)//cuddly mode
                            sURL = sURL + "&cuddly_bear_mode=true";
                        appSettings.FirefoxLastAccWindow.accValue[appSettings.objChild] = sURL;

                    }
                    else
                    {
                        DateTime end = DateTime.Parse(appSettings.sRunningFocusModeEndTime).ToUniversalTime();
                        string sEndTime = end.ToString("o");
                        string sFcousBlockedUrl = string.Format(sFcousBlockedWebUrl, val, sEndTime, appSettings.sRunningFocusModeName, appSettings.sRunningFocusModeIntention, appSettings.AppVersion.ToString());
                        // "https://focus-bear.github.io/assets/focus-blocked/?old_url={0)&focus_mode_end_time={1}&focus_mode={2}&focus_mode_intention={3}";

                        if (appSettings.nFocusBearMode == 1)//cuddly mode
                            sFcousBlockedUrl = sFcousBlockedUrl + "&cuddly_bear_mode=true";

                        appSettings.FirefoxLastAccWindow.accValue[appSettings.objChild] = sFcousBlockedUrl;

                    }

                }
                else if (type == "blocked")
                {
                    if (appSettings.bImmediatelyBlockDistractingSites) //BLock immediately
                    {
                        appSettings.nTotalDistractingSitesBlocked++; 

                        appSettings.FirefoxLastAccWindow.accValue[appSettings.objChild] = string.Format(sAlwaysBlockedWebUrl, val, (appSettings.nFocusBearMode == 1) ? "cuddly" : "grizzly", appSettings.AppVersion.ToString()); //1 is cuddly bear mode
                    }
                    else //show Set intention box
                    {
                        string sURLForSetIntention = "";
                        try
                        {
                            Uri myUri = new Uri(val);
                            string host = myUri.Host; //fails if ther eis no http://
                            sURLForSetIntention = host;
                            Debug.WriteLine(sURLForSetIntention);
                        }
                        catch (Exception ex)
                        {
                            /*
                            This constructor initializes a new instance of the UriBuilder class with the Fragment, Host, Path, Port, Query, Scheme, and Uri properties set as specified in uri.
                            If uri does not specify a scheme, the scheme defaults to "http:".
                            */
                            Uri myUri = new UriBuilder(val).Uri;
                            sURLForSetIntention = myUri.Host;
                            Debug.WriteLine(ex.Message);
                        }

                        var allowedURLs = appSettings.TemporaryAllowedURLsWithSetIntention.Where(i => i.sURL.ToLower() == sURLForSetIntention.ToLower());
                        if (allowedURLs.Count() != 0)
                        {
                            //found in allowed url
                            //if the intention box is not closed for this URL, show it
                            //we will also need to check if the time for this url is elapsed, then we need to ask interntion and confirmation again
                            if (DateTime.Now > allowedURLs.First().EndTime)
                            {
                                appSettings.ShowSetIntentionWindow(hWnd, "", val, "", true); //we will send full url here
                            }
                            else if (!allowedURLs.First().bIsShowIntentionWindowClosedForThisURL)
                            {
                                appSettings.Show_ShowIntentionWindow(hWnd, val, allowedURLs.First().sIntention);
                            }
                            else
                            {
                                appSettings.Hide_ShowIntentionWindow();
                                Console.WriteLine("Hide_ShowIntentionWindow from else of TemporaryAllowedURLsWithSetIntention");
                            }
                            return;//allow the url
                        }
                        else
                        {
                            var blockedURLs = appSettings.TemporarryBlockedURLsWithSetIntention.Where(i => i.ToLower() == sURLForSetIntention.ToLower());

                            if (blockedURLs.Count() != 0)
                            {
                                //found in blocked urls..block
                                appSettings.nTotalDistractingSitesBlocked++;

                                appSettings.FirefoxLastAccWindow.accValue[appSettings.objChild] = string.Format(sAlwaysBlockedWebUrl, val, (appSettings.nFocusBearMode == 1) ? "cuddly" : "grizzly", appSettings.AppVersion.ToString()); //1 is cuddly bear mode
                            }
                            else
                            {
                                appSettings.ShowSetIntentionWindow(hWnd, "", val, ""); //we will send full url here
                                return;
                            }
                        }
         
                    }
                }
                else
                {
                    //string sActivityBlockedWebUrl = "https://focus-bear.github.io/assets/focus-blocked?old_url={0}&block_type={1}"; //evening-habit, morning-habit, break-activity
                    appSettings.FirefoxLastAccWindow.accValue[appSettings.objChild] = string.Format(sActivityBlockedWebUrl, val, type, appSettings.AppVersion.ToString());
                }
                               
                SetForegroundWindow(hWnd);
                Task.Delay(1); 
                appSettings.FirefoxLastAccWindow.accSelect(0x1, appSettings.objChild);//take selection
                
                SendKeys.SendWait("{ENTER}");

                appSettings.CapturePostHog("block-distracting-url");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("suppressURLForFirefox EX -" + ex.Message);
            }


        }
    }
}
