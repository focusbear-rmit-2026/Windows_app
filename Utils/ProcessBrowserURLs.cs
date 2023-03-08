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

        private static string sAlwaysBlockedWebUrl = "file:///" + AppSettings.FocusBlockPagePath + "?old_url={0}&block_type=always-blocked";
        private static string sFcousBlockedWebUrl = "file:///" + AppSettings.FocusBlockPagePath + "?old_url={0}&focus_mode_end_time={1}&focus_mode={2}&focus_mode_intention={3}";
        private static string sActivityBlockedWebUrl = "file:///" + AppSettings.FocusBlockPagePath + "?old_url={0}&block_type={1}"; //evening-habit, morning-habit, break-activity

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
            "focus-block.html",};

       // public static readonly List<string> UNBLOCKED_APPLICATIONS = new List<string> { "msedgewebview2.exe", "explorer.exe", "focus bear.exe", "focusbear.exe", "devenv.exe", "taskmgr.exe" };
        //public static readonly int SW_MINIMIZE = 6;

        public static void ProcessCurrentURL(IntPtr hwnd)
        {
            AppSettings appSettings = AppSettings.GetInstance;
            if (appSettings.bHabitwithAllowedAppsRunning && !appSettings.bHabitwithUseFocusModeRunning)//
            {
               /// ProcessAllowedAppAndURLsInActivity(element);
                ProcessAlloweURLsInActivity(hwnd);
            }

            if (appSettings.isFocusModeRunning || appSettings.bHabitwithUseFocusModeRunning)
            {
                ProcessURLForFocusModes(hwnd);
            }

            if (appSettings.bHabitwithAllowedAppsRunning || appSettings.isFocusModeRunning)
                return;

            //#if !DEBUG
            if (appSettings.arrAlwaysBlockedURLs.Count() > 0)
                ProcessCurrentAlwaysBlockedURL(hwnd); //For always Blocked URLS
        }

        public static void ProcessCurrentAlwaysBlockedURL(IntPtr hwnd)
        {
            AppSettings appSettings = AppSettings.GetInstance;

            if (appSettings.FirefoxLastAccWindow != null && (appSettings.sLastActiveURLInFirefox != null && appSettings.sLastActiveURLInFirefox.Length > 0 && (!appSettings.sLastActiveURLInFirefox.Contains("focus-block.html"))))
            {
                //Check if its an always unblocked url
                for (int m = 0; m < UNBLOCKED_URLS.Count(); m++)
                {
                    string sToCheck = UNBLOCKED_URLS[m];
                    if (appSettings.sLastActiveURLInFirefox.Contains(sToCheck))
                    {
                        return;
                    }
                }

                foreach (string url in appSettings.arrAlwaysBlockedURLs)
                {
                    ////if (Uri.IsWellFormedUriString(val.CurrentValue, UriKind.RelativeOrAbsolute))
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

                            return;
                        }
                        else
                        {
                            // if (Regex.Match(sUrlToCheck, Regex.Escape(allowedURL).Replace("\\*", "(.+)")).Success)
                            allowedURL = "*" + allowedURL + "*";
                            if (MatchWildcardString(sUrlToCheck, allowedURL))
                            {

                                LogWriter.LogWrite(sUrlToCheck + "found in ALWAYS blocked URLS MatchWildcardString - BLOCK");
                                suppressURLForFirefox(hwnd, appSettings.sLastActiveURLInFirefox, "blocked");

                                return;
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

                                            return;
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
                return;
            }
            if (sURL.Contains("file://"))
            {
                return;
            }
            if (appSettings.nFocudBearMode == 1)
            {
                if (sURL.Contains("focus_bear_temporarily_allow=true"))
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
                    return;
                }
            }

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
                if (focusType != null && !focusType.isAllowedAllApps)
                {
                    if (focusType.isAllowedListApproach)//Block everything except safe sites I choose
                    {
                        LogWriter.LogWrite("isAllowedListApproach - Block everything except safe sites I choose - " + focusType.name);

                        ////if (focusType.isAllowedAllUrls) //All urls are allowed, nothng to block
                        ////{
                        ////    LogWriter.LogWrite("isAllowedListApproach - isAllowedAllUrls, return");
                        ////    return;
                        ////}
                        ////else //Selected URLs are allowed
                        {
                            LogWriter.LogWrite("isAllowedListApproach - Selected URLs are allowed");
                            //We will first check if the url is in the blocked urls list of the focusmode, then we will not check allowed
                            bAllowedURLFound = true; //allowed unless we find it in the blocked urls array

                            // LogWriter.LogWrite("focusType.arrBlockedUrls COUNT is:" + focusType.arrBlockedUrls.Count().ToString());

                            //check if the current URL is in the list of Blocked urls, then block else allow
                            foreach (string url in focusType.arrBlockedUrls)
                            {
                                ////if (Uri.IsWellFormedUriString(val.CurrentValue, UriKind.RelativeOrAbsolute))
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

                                        // if (Regex.Match(sUrlToCheck, Regex.Escape(allowedURL).Replace("\\*", "(.+)")).Success)
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

                                //LogWriter.LogWrite("focusType.arrAllowedUrls COUNT is:" + focusType.arrAllowedUrls.Count().ToString());

                                //check if the current URL is in the list of allowed urls, then allow else block
                                foreach (string url in focusType.arrAllowedUrls)
                                {
                                    /// LogWriter.LogWrite("focusType.arrAllowedUrl ITEM = " + url);

                                    //// if (Uri.IsWellFormedUriString(val.CurrentValue, UriKind.RelativeOrAbsolute))
                                    {
                                        /// LogWriter.LogWrite("focusType.arrAllowedUrl ITEM Uri.IsWellFormedUriString OK");

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
                                            return;
                                        }
                                        else
                                        {
                                            //if (Regex.Match(sUrlToCheck, Regex.Escape(allowedURL).Replace("\\*", "(.+)")).Success)
                                            allowedURL = "*" + allowedURL + "*";
                                            if (MatchWildcardString(sUrlToCheck, allowedURL))
                                            {
                                                LogWriter.LogWrite(sUrlToCheck + " found in ALLOWED URLS WILDCARD match of focus mode - ALLOW");

                                                bAllowedURLFound = true;
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
                                                            LogWriter.LogWrite(sTitle + "title found in ALLOWED URLS WILDCARD match of focus mode - ALLOW");
                                                            bAllowedURLFound = true;
                                                            return;
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
                                }
                            }

                            if (!bAllowedURLFound)
                            {
                                LogWriter.LogWrite("processCurrentFocusElement  isAllowedListApproach - Attempt to Suppress URL");

                                suppressURLForFirefox(hWnd, sURL, "focus");
                                return;
                            }
                        }
                    }
                    else //Allow everything except districting sites
                    {
                        LogWriter.LogWrite("isBlockedListApproach - Allow everything except districting sites");

                        ////if (focusType.isBlockedAllUrls) //All urls are BLOCKED
                        ////{
                        ////    LogWriter.LogWrite("isBlockedListApproach - isBlockedAllUrls");
                        ////    bAllowedURLFound = false;

                        ////    LogWriter.LogWrite("processCurrentFocusElement  isBlockedListApproach isBlockedAllUrls - Attempt to Suppress URL");
                        ////    suppressURL(val, "focus");
                        ////    return;

                        ////}
                        ////else //selected URLS are blocked
                        {
                            LogWriter.LogWrite("isBlockedListApproach - selected URLS are blocked");

                            bAllowedURLFound = true; //allowed unless we find it in the blocked urls array

                            //check if the current URL is in the list of Blocked urls, then block else allow
                            foreach (string url in focusType.arrBlockedUrls)
                            {
                                ////if (Uri.IsWellFormedUriString(val.CurrentValue, UriKind.RelativeOrAbsolute))
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
                                        // if (Regex.Match(sUrlToCheck, Regex.Escape(allowedURL).Replace("\\*", "(.+)")).Success)
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

                //if (val != null && !string.IsNullOrEmpty(val.CurrentValue) && (!val.CurrentValue.ToString().Contains("focus-block.html")))
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
                            /////if (Uri.IsWellFormedUriString(val.CurrentValue, UriKind.RelativeOrAbsolute))
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
                                    //if (Regex.Match(sUrlToCheck, Regex.Escape(allowedURL).Replace("\\*", "(.+)")).Success)
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
                            //////if (Uri.IsWellFormedUriString(val.CurrentValue, UriKind.RelativeOrAbsolute))
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
                                    //if (Regex.Match(sUrlToCheck, Regex.Escape(allowedURL).Replace("\\*", "(.+)")).Success)
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
                                ///// if (Uri.IsWellFormedUriString(val.CurrentValue, UriKind.RelativeOrAbsolute))
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
                                        //if (Regex.Match(sUrlToCheck, Regex.Escape(allowedURL).Replace("\\*", "(.+)")).Success)
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
                                ////// if (Uri.IsWellFormedUriString(val.CurrentValue, UriKind.RelativeOrAbsolute))
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
                                        //if (Regex.Match(sUrlToCheck, Regex.Escape(allowedURL).Replace("\\*", "(.+)")).Success)
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
                        //  string sActivityBlockedWebUrl = "https://focus-bear.github.io/assets/focus-blocked?old_url={0}&block_type={0}"; //evening-habit, morning-habit, break-activity
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
                    if (appSettings.nFocudBearMode == 1)//Cuddly bearMode
                    {
                        string sURLForFocusMode = "";
                        try
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
                            */
                            Uri myUri = new UriBuilder(val).Uri;
                            sURLForFocusMode = myUri.Host;
                            Debug.WriteLine(ex.Message);
                        }

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
                        string sURL = string.Format(sActivityBlockedWebUrl, val, appSettings.sHabitTypeForUseFocusModeIDInActivity);
                        if (appSettings.nFocudBearMode == 1)//cuddly mode
                            sURL = sURL + "&cuddly_bear_mode=true";
                        appSettings.FirefoxLastAccWindow.accValue[appSettings.objChild] = sURL;

                    }
                    else
                    {
                        DateTime end = DateTime.Parse(appSettings.sRunningFocusModeEndTime).ToUniversalTime();
                        string sEndTime = end.ToString("o");
                        string sFcousBlockedUrl = string.Format(sFcousBlockedWebUrl, val, sEndTime, appSettings.sRunningFocusModeName, appSettings.sRunningFocusModeIntention);
                        // "https://focus-bear.github.io/assets/focus-blocked/?old_url={0)&focus_mode_end_time={1}&focus_mode={2}&focus_mode_intention={3}";

                        if (appSettings.nFocudBearMode == 1)//cuddly mode
                            sFcousBlockedUrl = sFcousBlockedUrl + "&cuddly_bear_mode=true";

                        appSettings.FirefoxLastAccWindow.accValue[appSettings.objChild] = sFcousBlockedUrl;

                    }

                }
                else if (type == "blocked")
                {
                    appSettings.FirefoxLastAccWindow.accValue[appSettings.objChild] = string.Format(sAlwaysBlockedWebUrl, val);
                }
                else
                {
                    //string sActivityBlockedWebUrl = "https://focus-bear.github.io/assets/focus-blocked?old_url={0}&block_type={1}"; //evening-habit, morning-habit, break-activity
                    appSettings.FirefoxLastAccWindow.accValue[appSettings.objChild] = string.Format(sActivityBlockedWebUrl, val, type);
                }
                               
                SetForegroundWindow(hWnd);
                Task.Delay(1); 
                //appSettings.FirefoxLastAccWindow.accSelect(0x1, appSettings.objChild);//take selection
                
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
