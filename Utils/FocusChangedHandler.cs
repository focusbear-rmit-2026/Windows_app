using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Interop.UIAutomationClient;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using TreeScope = Interop.UIAutomationClient.TreeScope;
using Focus_Bear.Utils;
using System.Text.RegularExpressions;
using System.Linq;
using System.Management;
using Focus_Bear.Models;
using System.Windows.Automation;
using System.Text;
using Focus_Bear.Focus;
using System.Windows.Threading;
using Focus_Bear;
using System.Threading;

namespace FocusBear.Utils
{
    public class FocusChangeHandler : IUIAutomationFocusChangedEventHandler
    {
        AppSettings appSettings = AppSettings.GetInstance;

  
        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        //  private readonly MenuOptions listener;

        public static readonly List<string> BROWSERS = new List<string> { "chrome", "firefox", "opera", "msedge" };
        public static readonly int UIA_ControlTypePropertyId = 30003;
        public static readonly int UIA_EditControlTypeId = 50004;
        public static readonly int UIA_PATTERN_ID = 10002;

        public static readonly List<string> BLOCKED_URLS = new List<string> { "*.google.com", "facebook.com", "twitter.com" };
        public static readonly List<string> BLOCKED_APPS = new List<string> { "notepad", "regedit", "registry editor" };

        string sAlwaysBlockedWebUrl = "https://focus-bear.github.io/assets/focus-blocked?old_url={0}&block_type=always-blocked";
        string sFcousBlockedWebUrl = "https://focus-bear.github.io/assets/focus-blocked/?old_url={0}&focus_mode_end_time={1}&focus_mode={2}&focus_mode_intention={3}";
        string sActivityBlockedWebUrl = "https://focus-bear.github.io/assets/focus-blocked?old_url={0}&block_type={1}"; //evening-habit, morning-habit, break-activity


        public static readonly List<string> UNBLOCKED_APPLICATIONS = new List<string> { "explorer", "FocusBear", "Focus Bear", "devenv", "taskmgr" };
        public static readonly int SW_MINIMIZE = 6;

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
            "focus-bear.github.io"};


        private Boolean MatchWildcardString(String input, String pattern)
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


        public FocusChangeHandler(/*MenuOptions listener*/)
        {
            //this.listener = listener;
        }

        public void HandleFocusChangedEvent(IUIAutomationElement element)
        {
            ///  string currentPomodoro = Settings.preferenceSetting.synchronizeSetting.currentPomodoroMode;
            /// string currentFocus = Settings.preferenceSetting.synchronizeSetting.currentFocusMode;
            /// if (!string.IsNullOrEmpty(currentPomodoro))
            if (appSettings.bHabitwithAllowedAppsRunning)
            {
                ProcessAllowedAppInActivity(element);
                ProcessAlloweURLsInActivity(element);
            }

            if (appSettings.isFocusModeRunning)
            {
                processCurrentFocusElement(element);
            }
            if (appSettings.bHabitwithAllowedAppsRunning || appSettings.isFocusModeRunning)
                return;

#if !DEBUG
            processCurrentURL(element); //For always Blocked URLS
#endif

            //    processCurrentPomodoroElement(element);
            ///  else if (!string.IsNullOrEmpty(currentFocus))
            //  processCurrentFocusElement(element);
            ///  else if (Settings.preferenceSetting.synchronizeSetting.blockedURLs.Count > 0)



        }

        private void ProcessAllowedAppInActivity(IUIAutomationElement element)
        {
            try
            {
                using (Process process = Process.GetProcessById(element.CurrentProcessId))
                {
                    if (element != null && process != null)
                    {
                        // block or Unblock apps
                        if (UNBLOCKED_APPLICATIONS.Contains(process.ProcessName))
                        {
                            return;
                        }

                        bool bAllowedAppFound = false;
                        if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                        {
                            foreach (string sApp in appSettings.arrAllMorningHabits[appSettings.nHabitIndex].allowedApps)
                            {
                                if (sApp.ToLower() == process.ProcessName.ToLower())
                                {
                                    bAllowedAppFound = true;
                                    LogWriter.LogWrite(sApp + " allow app found in MORNING_HABITS activity - Rrturn");
                                    return;
                                }
                            }
                            if (!bAllowedAppFound)
                            {
                                LogWriter.LogWrite(process.ProcessName + " Not allowed app found in MORNING_HABITS activity - BLOCK");
                                ShowWindow(process.MainWindowHandle, SW_MINIMIZE);
                                appSettings.CapturePostHog("block-distracting-app");
                            }
                        }
                        else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                        {
                            foreach (string sApp in appSettings.arrAllEveningHabits[appSettings.nHabitIndex].allowedApps)
                            {
                                if (sApp.ToLower() == process.ProcessName.ToLower())
                                {
                                    bAllowedAppFound = true;
                                    LogWriter.LogWrite(sApp + " allow app found in EVENING_HABITS activity - Rrturn");
                                    return;
                                }
                            }
                            if (!bAllowedAppFound)
                            {
                                LogWriter.LogWrite(process.ProcessName + " Not allowed app found in EVENING_HABITS activity - BLOCK");
                                ShowWindow(process.MainWindowHandle, SW_MINIMIZE);
                                appSettings.CapturePostHog("block-distracting-app");
                            }
                        }
                        else  if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                        {

                            if (appSettings.isMandatoryActivityGoing)
                            {
                                foreach (string sApp in appSettings.arrActivitiesIncludeInEveryBreak[appSettings.nHabitIndex].allowedApps)
                                {
                                    if (sApp.ToLower() == process.ProcessName.ToLower())
                                    {
                                        bAllowedAppFound = true;
                                        LogWriter.LogWrite(sApp + " allow app found in BREAK_ACTIVITY activity - Rrturn");
                                        return;
                                    }
                                }
                                if (!bAllowedAppFound)
                                {
                                    LogWriter.LogWrite(process.ProcessName + " Not allowed app found in BREAK_ACTIVITY activity - BLOCK");
                                    ShowWindow(process.MainWindowHandle, SW_MINIMIZE);
                                    appSettings.CapturePostHog("block-distracting-app");
                                }
                            }
                            else
                            {
                                foreach (string sApp in appSettings.arrActivitiesExcludeInEveryBreak[appSettings.nHabitIndex].allowedApps)
                                {
                                    if (sApp.ToLower() == process.ProcessName.ToLower())
                                    {
                                        bAllowedAppFound = true;
                                        LogWriter.LogWrite(sApp + " allow app found in BREAK_ACTIVITY activity - Rrturn");
                                        return;
                                    }
                                }
                                if (!bAllowedAppFound)
                                {
                                    LogWriter.LogWrite(process.ProcessName + " Not allowed app found in BREAK_ACTIVITY activity - BLOCK");
                                    ShowWindow(process.MainWindowHandle, SW_MINIMIZE);
                                    appSettings.CapturePostHog("block-distracting-app");
                                }
                            }

                        }

                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ProcessAllowedAppInActivity - " + ex.Message);
                Debug.WriteLine(ex.Message);
            }

        }
        private void ProcessAlloweURLsInActivity(IUIAutomationElement element)
        {
            try
            {
                using (Process process = Process.GetProcessById(element.CurrentProcessId))
                {
                    if (element != null && process != null)
                    {
                        // block or Unblock URLS
                        if (BROWSERS.Exists(ele => ele == process.ProcessName))
                        {
                            if (appSettings.arrAllMorningHabits[appSettings.nHabitIndex].allowedApps.Exists(ele => ele == process.ProcessName))
                            {
                                IUIAutomationElement elm = appSettings.automation.ElementFromHandle(process.MainWindowHandle);
                                IUIAutomationCondition Cond = appSettings.automation.CreatePropertyCondition(UIA_ControlTypePropertyId, UIA_EditControlTypeId);
                                IUIAutomationElementArray elm2 = elm.FindAll(TreeScope.TreeScope_Descendants, Cond);
                                if (elm2 != null)
                                {
                                    for (int i = 0; i < elm2.Length; i++)
                                    {
                                        IUIAutomationElement elm3 = elm2.GetElement(i);
                                        IUIAutomationValuePattern val = (IUIAutomationValuePattern)elm3.GetCurrentPattern(UIA_PATTERN_ID);
                                        if (val != null && !string.IsNullOrEmpty(val.CurrentValue) && (!val.CurrentValue.ToString().Contains("focus-bear.github.io")))
                                        {
                                            if (val.CurrentValue.Contains("file://"))
                                            {
                                                return;
                                            }
                                            //Check if its an always unblocked url
                                            for (int m = 0; m < UNBLOCKED_URLS.Count(); m++)
                                            {
                                                string sToCheck = UNBLOCKED_URLS[m];
                                                if (val.CurrentValue.Contains(sToCheck))
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
                                                        var sUrlToCheck = val.CurrentValue.Replace("http://", "");
                                                        sUrlToCheck = sUrlToCheck.Replace("https://", "");
                                                        sUrlToCheck = sUrlToCheck.Replace("www.", "");

                                                        var allowedURL = url.Replace("http://", "");
                                                        allowedURL = allowedURL.Replace("https://", "");
                                                        allowedURL = allowedURL.Replace("www.", "");

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
                                                                    if (GetWindowText(process.MainWindowHandle, Buff, nChars) > 0)
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
                                                        var sUrlToCheck = val.CurrentValue.Replace("http://", "");
                                                        sUrlToCheck = sUrlToCheck.Replace("https://", "");
                                                        sUrlToCheck = sUrlToCheck.Replace("www.", "");

                                                        var allowedURL = url.Replace("http://", "");
                                                        allowedURL = allowedURL.Replace("https://", "");
                                                        allowedURL = allowedURL.Replace("www.", "");

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
                                                                    if (GetWindowText(process.MainWindowHandle, Buff, nChars) > 0)
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
                                                            var sUrlToCheck = val.CurrentValue.Replace("http://", "");
                                                            sUrlToCheck = sUrlToCheck.Replace("https://", "");
                                                            sUrlToCheck = sUrlToCheck.Replace("www.", "");

                                                            var allowedURL = url.Replace("http://", "");
                                                            allowedURL = allowedURL.Replace("https://", "");
                                                            allowedURL = allowedURL.Replace("www.", "");

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
                                                                        if (GetWindowText(process.MainWindowHandle, Buff, nChars) > 0)
                                                                        {
                                                                            string sTitle = Buff.ToString();
                                                                            if (MatchWildcardString(sTitle, allowedURL))
                                                                            {
                                                                                LogWriter.LogWrite(sTitle + "titlefound in ALLOWED URLS MatchWildcardString of BREAK_ACTIVITY - ALLOW");
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
                                                            var sUrlToCheck = val.CurrentValue.Replace("http://", "");
                                                            sUrlToCheck = sUrlToCheck.Replace("https://", "");
                                                            sUrlToCheck = sUrlToCheck.Replace("www.", "");

                                                            var allowedURL = url.Replace("http://", "");
                                                            allowedURL = allowedURL.Replace("https://", "");
                                                            allowedURL = allowedURL.Replace("www.", "");

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
                                                                        if (GetWindowText(process.MainWindowHandle, Buff, nChars) > 0)
                                                                        {
                                                                            string sTitle = Buff.ToString();
                                                                            if (MatchWildcardString(sTitle, allowedURL))
                                                                            {
                                                                                LogWriter.LogWrite(sTitle + "titlefound in ALLOWED URLS MatchWildcardString of BREAK_ACTIVITY - ALLOW");
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
                                                LogWriter.LogWrite(val.CurrentValue + " BLOCKED URL found in activity - BLOCK");
                                                //  string sActivityBlockedWebUrl = "https://focus-bear.github.io/assets/focus-blocked?old_url={0}&block_type={0}"; //evening-habit, morning-habit, break-activity
                                                suppressURL(val, sHabitType);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ProcessAlloweURLsInActivity - " + ex.Message);
            }


        }
        
        private void processCurrentPomodoroElement(IUIAutomationElement element)
        {
            try
            {
                using (Process process = Process.GetProcessById(element.CurrentProcessId))
                {
                    if (element != null && process != null)
                    {
                        // block or Unblock apps
                        if (UNBLOCKED_APPLICATIONS.Contains(process.ProcessName))
                        {
                            return;
                        }
                        
                        foreach (string sApp in BLOCKED_APPS)
                        {
                            if (sApp == process.ProcessName)
                            {
                                ShowWindow(process.MainWindowHandle, SW_MINIMIZE);
                            }
                        }

                       

                        // block or Unblock apps
                        //if (Constants.UNBLOCKED_APPLICATIONS.Exists(app => app != process.ProcessName) && Settings.preferenceSetting.synchronizeSetting != null
                        //&& isAppExists(Settings.preferenceSetting.synchronizeSetting.installedApplications, process.ProcessName))
                        //{
                        //    FocusType focusType = Settings.preferenceSetting.synchronizeSetting.focusTypes.Find(focus => focus.name == Settings.preferenceSetting.synchronizeSetting.currentPomodoroMode);
                        //    if (focusType != null && focusType.allowedApps.Count > 0)
                        //    {
                        //        if (!isAppExists(focusType.allowedApps, process.ProcessName) && IsWindowVisible(process.MainWindowHandle))
                        //            ShowWindow(process.MainWindowHandle, Constants.SW_MINIMIZE);
                        //    }
                        //}

                        //// block or Unblock URLS
                        //if (Constants.BROWSERS.Exists(ele => ele == process.ProcessName))
                        //{
                        //    IUIAutomationElement elm = listener.automation.ElementFromHandle(process.MainWindowHandle);
                        //    IUIAutomationCondition Cond = listener.automation.CreatePropertyCondition(Constants.UIA_ControlTypePropertyId, Constants.UIA_EditControlTypeId);
                        //    IUIAutomationElementArray elm2 = elm.FindAll(TreeScope.TreeScope_Descendants, Cond);
                        //    if (elm2 != null)
                        //    {
                        //        for (int i = 0; i < elm2.Length; i++)
                        //        {
                        //            IUIAutomationElement elm3 = elm2.GetElement(i);
                        //            IUIAutomationValuePattern val = (IUIAutomationValuePattern)elm3.GetCurrentPattern(Constants.UIA_PATTERN_ID);
                        //            if (val != null && !string.IsNullOrEmpty(val.CurrentValue))
                        //            {
                        //                FocusType type = Settings.preferenceSetting.synchronizeSetting.focusTypes.Find(ele => ele.name == Settings.preferenceSetting.synchronizeSetting.currentPomodoroMode);
                        //                if (type != null && type.allowedURLs.Count > 0)
                        //                {
                        //                    if (!type.allowedURLs.Exists(u => val.CurrentValue.StartsWith(u.Substring(u.IndexOf(".") + 1)))
                        //                        && Uri.IsWellFormedUriString(val.CurrentValue, UriKind.RelativeOrAbsolute)
                        //                        && val.CurrentValue != String.Format(@"file:///{0}Focus Bear/index.html?old_url={1}",
                        //                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), val.CurrentValue))
                        //                    {
                        //                        suppressURL(val, "focusMode", type.name);
                        //                    }
                        //                }
                        //            }
                        //        }
                        //    }
                        //}
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        static private string ProcessExecutablePath(Process process)
        {
            try
            {
                return process.MainModule.FileName; //32 bit
            }
            catch //64 bit apps
            {
                string query = "SELECT ExecutablePath, ProcessID FROM Win32_Process";
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);

                foreach (ManagementObject item in searcher.Get())
                {
                    object id = item["ProcessID"];
                    object path = item["ExecutablePath"];

                    if (path != null && id.ToString() == process.Id.ToString())
                    {
                        string MyPath = path.ToString();
                        string MyPathWithoutDriveOrNetworkShare = "";  MyPath.Substring(Path.GetPathRoot(MyPath).Length);

                        if ((MyPath.IndexOf(":") == 1) || (MyPath.IndexOf("\\\\") == 0))
                        {
                            MyPathWithoutDriveOrNetworkShare = MyPath.Substring(2); 
                        }
                        if (MyPathWithoutDriveOrNetworkShare.IndexOf("\\") > 0)
                            MyPathWithoutDriveOrNetworkShare = MyPathWithoutDriveOrNetworkShare.Substring(MyPathWithoutDriveOrNetworkShare.IndexOf("\\"));

                        return MyPathWithoutDriveOrNetworkShare;
                    }
                }
            }

            return "";
        }
        private void processCurrentFocusElement(IUIAutomationElement element)
        {
            try
            {
                using (Process process = Process.GetProcessById(element.CurrentProcessId))
                {
                    if (element != null && process != null)
                    {
                        // block or Unblock apps
                        // block or Unblock apps
                        if (UNBLOCKED_APPLICATIONS.Contains(process.ProcessName))
                        {
                            return;
                        }

                      /////  string fullPath = ProcessExecutablePath(process);

                        var listItems = appSettings.arrFocusModes.Where(i => i.isOn == true);
                        if (listItems.Count() != 0)
                        {
                            FocusMode focusType = listItems.First();
                            if (focusType != null && !focusType.isAllowedAllApps )
                            {
                                bool bAllowedAppFound = false;
                                foreach (AppDetails sApp in focusType.arrAllowedAppsWin)
                                {
                                    foreach (string sAppExe in sApp.ExeNames)
                                    {
                                        string App = sAppExe.Replace(".exe", "").ToLower();
                                        if (App == process.ProcessName.ToLower())
                                        {
                                            bAllowedAppFound = true;
                                            break;
                                        }
                                    }

                                    if (bAllowedAppFound)
                                        break;
                                }
                                if (!bAllowedAppFound)
                                {
                                    if (appSettings.nFocudBearMode == 1)//Cuddly bearMode
                                    {
                                        var allowedApps = appSettings.TemporaryAllowedAppsGentleBlock.Where(i => i.ToLower() == process.ProcessName.ToLower());
                                        if (allowedApps.Count() != 0)
                                        {
                                            //found in allowed app
                                            return;//allow the app
                                        }
                                        else
                                        {
                                            var blockedApps = appSettings.TemporarryBlockedAppsGentleBlock.Where(i => i.ToLower() == process.ProcessName.ToLower());
                                            if (blockedApps.Count() != 0)
                                            {
                                                //found in blocked apps..block
                                            }
                                            else
                                            {
                                                appSettings.ShowGentleBlockWindow(process.ProcessName);
                                                return;
                                            }
                                        }
                                    }
                                    
                                    if (IsWindowVisible(process.MainWindowHandle))
                                        ShowWindow(process.MainWindowHandle, SW_MINIMIZE);

                                    appSettings.CapturePostHog("block-distracting-app");

                                    return; //if not an allowed app, no need to check URLs
                                }
                            }

                            //If the app is one of the browsers, ther we will check the URLS
                            if (BROWSERS.Exists(ele => ele == process.ProcessName))
                            {
                                if (focusType.isAllowedListApproach )//Block everything except safe sites I choose
                                {
                                    if (focusType.isAllowedAllUrls) //All urls are allowed, nothng to block
                                    {
                                        LogWriter.LogWrite("isAllowedListApproach - isAllowedAllUrls, return No need to get the url");
                                        return; //No need to get the url
                                    }
                                }

                                //
                                LogWriter.LogWrite("RUNNING Focus Mode is:" + focusType.name);

                                //First let's get the URL value

                                IUIAutomationElement elm = appSettings.automation.ElementFromHandle(process.MainWindowHandle);
                                IUIAutomationCondition Cond = appSettings.automation.CreatePropertyCondition(UIA_ControlTypePropertyId, UIA_EditControlTypeId);
                                IUIAutomationElementArray elm2 = elm.FindAll(TreeScope.TreeScope_Descendants, Cond);
                                if (elm2 != null)
                                {
                                    for (int i = 0; i < elm2.Length; i++)
                                    {
                                        IUIAutomationElement elm3 = elm2.GetElement(i);
                                        IUIAutomationValuePattern val = (IUIAutomationValuePattern)elm3.GetCurrentPattern(UIA_PATTERN_ID);
                                        if (val != null && !string.IsNullOrEmpty(val.CurrentValue) && (!val.CurrentValue.ToString().Contains("focus-bear.github.io")))
                                        {
                                          
                                            if (val.CurrentValue.Contains("file://"))
                                            {
                                                return;
                                            }
                                            //Check if its an always unblocked url
                                            for (int m= 0; m < UNBLOCKED_URLS.Count(); m++)
                                            {
                                                string sToCheck = UNBLOCKED_URLS[m];
                                                if(val.CurrentValue.Contains(sToCheck))
                                                {
                                                    return;
                                                }
                                            }

                                            bool bAllowedURLFound = true; //by default allowed url is true

                                            if (focusType.isAllowedListApproach)//Block everything except safe sites I choose
                                            {
                                                LogWriter.LogWrite("isAllowedListApproach - Block everything except safe sites I choose - " + focusType.name);

                                                if (focusType.isAllowedAllUrls) //All urls are allowed, nothng to block
                                                {
                                                    LogWriter.LogWrite("isAllowedListApproach - isAllowedAllUrls, return");
                                                    return;
                                                }
                                                else //Selected URLs are allowed
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
                                                            var sUrlToCheck = val.CurrentValue.Replace("http://", "");
                                                            sUrlToCheck = sUrlToCheck.Replace("https://", "");
                                                            sUrlToCheck = sUrlToCheck.Replace("www.", "");

                                                            var allowedURL = url.Replace("http://", "");
                                                            allowedURL = allowedURL.Replace("https://", "");
                                                            allowedURL = allowedURL.Replace("www.", "");

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
                                                                        if (GetWindowText(process.MainWindowHandle, Buff, nChars) > 0)
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

                                                                var sUrlToCheck = val.CurrentValue.Replace("http://", "");
                                                                sUrlToCheck = sUrlToCheck.Replace("https://", "");
                                                                sUrlToCheck = sUrlToCheck.Replace("www.", "");

                                                                var allowedURL = url.Replace("http://", "");
                                                                allowedURL = allowedURL.Replace("https://", "");
                                                                allowedURL = allowedURL.Replace("www.", "");

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
                                                                            if (GetWindowText(process.MainWindowHandle, Buff, nChars) > 0)
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
                                                        suppressURL(val, "focus");
                                                        return;
                                                    }
                                                }
                                            }
                                            else //Allow everything except districting sites
                                            {
                                                LogWriter.LogWrite("isBlockedListApproach - Allow everything except districting sites");

                                                if (focusType.isBlockedAllUrls) //All urls are BLOCKED
                                                {
                                                    LogWriter.LogWrite("isBlockedListApproach - isBlockedAllUrls");
                                                    bAllowedURLFound = false;

                                                    LogWriter.LogWrite("processCurrentFocusElement  isBlockedListApproach isBlockedAllUrls - Attempt to Suppress URL");
                                                    suppressURL(val, "focus");
                                                    return;

                                                }
                                                else //selected URLS are blocked
                                                {
                                                    LogWriter.LogWrite("isBlockedListApproach - selected URLS are blocked");

                                                    bAllowedURLFound = true; //allowed unless we find it in the blocked urls array

                                                    //check if the current URL is in the list of Blocked urls, then block else allow
                                                    foreach (string url in focusType.arrBlockedUrls)
                                                    {
                                                        ////if (Uri.IsWellFormedUriString(val.CurrentValue, UriKind.RelativeOrAbsolute))
                                                        {
                                                            var sUrlToCheck = val.CurrentValue.Replace("http://", "");
                                                            sUrlToCheck = sUrlToCheck.Replace("https://", "");
                                                            sUrlToCheck = sUrlToCheck.Replace("www.", "");

                                                            var allowedURL = url.Replace("http://", "");
                                                            allowedURL = allowedURL.Replace("https://", "");
                                                            allowedURL = allowedURL.Replace("www.", "");

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
                                                                        if (GetWindowText(process.MainWindowHandle, Buff, nChars) > 0)
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
                                                        suppressURL(val, "focus");
                                                        return;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                ///

                            }

                        }
                             
                        

                        //// block or Unblock URLS
                        //if (Constants.BROWSERS.Exists(ele => ele == process.ProcessName))
                        //{
                        //    IUIAutomationElement elm = listener.automation.ElementFromHandle(process.MainWindowHandle);
                        //    IUIAutomationCondition Cond = listener.automation.CreatePropertyCondition(Constants.UIA_ControlTypePropertyId, Constants.UIA_EditControlTypeId);
                        //    IUIAutomationElementArray elm2 = elm.FindAll(TreeScope.TreeScope_Descendants, Cond);
                        //    if (elm2 != null)
                        //    {
                        //        for (int i = 0; i < elm2.Length; i++)
                        //        {
                        //            IUIAutomationElement elm3 = elm2.GetElement(i);
                        //            IUIAutomationValuePattern val = (IUIAutomationValuePattern)elm3.GetCurrentPattern(Constants.UIA_PATTERN_ID);
                        //            if (val != null && !string.IsNullOrEmpty(val.CurrentValue))
                        //            {
                        //                FocusType type = Settings.preferenceSetting.synchronizeSetting.focusTypes.Find(ele => ele.name == Settings.preferenceSetting.synchronizeSetting.currentFocusMode);
                        //                if (type != null && type.isFocused && type.allowedURLs.Count > 0)
                        //                {
                        //                    if (!type.allowedURLs.Exists(u => val.CurrentValue.StartsWith(u.Substring(u.IndexOf(".") + 1)))
                        //                        && Uri.IsWellFormedUriString(val.CurrentValue, UriKind.RelativeOrAbsolute)
                        //                        && val.CurrentValue != String.Format(@"file:///{0}Focus Bear/index.html?old_url={1}",
                        //                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), val.CurrentValue))
                        //                    {
                        //                        suppressURL(val, "focusMode", type.name);
                        //                    }
                        //                }
                        //            }
                        //        }
                        //    }
                        //}
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("processCurrentFocusElement EX - " + ex.Message);
            }
        }

        private void processCurrentURL(IUIAutomationElement element)
        {
            try
            {
                using (Process process = Process.GetProcessById(element.CurrentProcessId))
                {
                    if (element != null && process != null)
                    {
                        // block or Unblock URLS
                        if (BROWSERS.Exists(ele => ele == process.ProcessName))
                        {
                            IUIAutomationElement elm = appSettings.automation.ElementFromHandle(process.MainWindowHandle);
                            IUIAutomationCondition Cond = appSettings.automation.CreatePropertyCondition(UIA_ControlTypePropertyId, UIA_EditControlTypeId);
                            IUIAutomationElementArray elm2 = elm.FindAll(TreeScope.TreeScope_Descendants, Cond);
                            if (elm2 != null)
                            {
                               
                                for (int i = 0; i < elm2.Length; i++)
                                {
                                    IUIAutomationElement elm3 = elm2.GetElement(i);
                                    IUIAutomationValuePattern val = (IUIAutomationValuePattern)elm3.GetCurrentPattern(UIA_PATTERN_ID);
                                    if (val != null && !string.IsNullOrEmpty(val.CurrentValue) && (!val.CurrentValue.ToString().Contains("focus-bear.github.io")) )
                                    {
                                        foreach (string url in appSettings.arrAlwaysBlockedURLs)
                                        {
                                            ////if (Uri.IsWellFormedUriString(val.CurrentValue, UriKind.RelativeOrAbsolute))
                                            {
                                                var sUrlToCheck = val.CurrentValue.Replace("http://", "");
                                                sUrlToCheck = sUrlToCheck.Replace("https://", "");
                                                sUrlToCheck = sUrlToCheck.Replace("www.", "");

                                                var allowedURL = url.Replace("http://", "");
                                                allowedURL = allowedURL.Replace("https://", "");
                                                allowedURL = allowedURL.Replace("www.", "");

                                                if (sUrlToCheck.StartsWith(allowedURL))
                                                {
                                                    LogWriter.LogWrite(sUrlToCheck + "found in ALWAYS blocked URLS - BLOCK");
                                                    suppressURL(val, "blocked");
                                                    
                                                    break;
                                                }
                                                else
                                                {
                                                    // if (Regex.Match(sUrlToCheck, Regex.Escape(allowedURL).Replace("\\*", "(.+)")).Success)
                                                    allowedURL = "*" + allowedURL + "*";
                                                    if (MatchWildcardString(sUrlToCheck, allowedURL))
                                                    {
                                                        LogWriter.LogWrite(sUrlToCheck + "found in ALWAYS blocked URLS MatchWildcardString - BLOCK");
                                                        suppressURL(val, "blocked");
                                                        break;
                                                    }
                                                    else
                                                    {
                                                        try
                                                        {
                                                            //Check title
                                                            const int nChars = 256;
                                                            StringBuilder Buff = new StringBuilder(nChars);
                                                            if (GetWindowText(process.MainWindowHandle, Buff, nChars) > 0)
                                                            {
                                                                string sTitle = Buff.ToString();
                                                                if (MatchWildcardString(sTitle, allowedURL))
                                                                {
                                                                    LogWriter.LogWrite(sTitle + "title found in ALWAYS blocked URLS MatchWildcardString - BLOCK");
                                                                    suppressURL(val, "blocked");
                                                                    break;
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
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("processCurrentURL EX - " + ex.Message);
            }

        }
         

        private void suppressURL(IUIAutomationValuePattern val, string type, string focusMode = null)
        {
            try
            {
                LogWriter.LogWrite("suppressURL - Block URL - " + val.CurrentValue);


                if (type == "focus")
                {
                    if (appSettings.nFocudBearMode == 1)//Cuddly bearMode
                    {
                        string sURLForFocusMode = "";
                        try
                        {
                            Uri myUri = new Uri(val.CurrentValue);
                            string host = myUri.Host; //fails if ther eis no http://
                            sURLForFocusMode = host;
                            Debug.WriteLine(sURLForFocusMode);
                        }
                        catch (Exception ex)
                        {
                            sURLForFocusMode = val.CurrentValue;
                            Debug.WriteLine(ex.Message);
                        }

                        var allowedURLs = appSettings.TemporaryAllowedURLsGentleBlock.Where(i => i.ToLower() == sURLForFocusMode.ToLower());
                        if (allowedURLs.Count() != 0)
                        {
                            //found in allowed app
                            return;//allow the app
                        }
                        else
                        {
                            var blockedURLs = appSettings.TemporarryBlockedURLsGentleBlock.Where(i => i.ToLower() == sURLForFocusMode.ToLower());
                            if (blockedURLs.Count() != 0)
                            {
                                //found in blocked apps..block
                            }
                            else
                            {
                                appSettings.ShowGentleBlockWindow("", sURLForFocusMode);
                                return;
                            }
                        }
                    }

                    DateTime end = DateTime.Parse(appSettings.sRunningFocusModeEndTime).ToUniversalTime();
                    string sEndTime = end.ToString("o");

                    string sFcousBlockedUrl = string.Format(sFcousBlockedWebUrl, val.CurrentValue, sEndTime, appSettings.sRunningFocusModeName, appSettings.sRunningFocusModeIntention);
                    // "https://focus-bear.github.io/assets/focus-blocked/?old_url={0)&focus_mode_end_time={1}&focus_mode={2}&focus_mode_intention={3}";
                    val.SetValue(string.Format(sFcousBlockedUrl, val.CurrentValue));
                }
                else if(type == "blocked")
                {
                    val.SetValue(string.Format(sAlwaysBlockedWebUrl, val.CurrentValue));
                }
                else
                {
                    //string sActivityBlockedWebUrl = "https://focus-bear.github.io/assets/focus-blocked?old_url={0}&block_type={1}"; //evening-habit, morning-habit, break-activity
                    val.SetValue(string.Format(sActivityBlockedWebUrl, val.CurrentValue, focusMode));
                }

                //else
                //{
                //    val.SetValue(string.Format(Constants.URL_ALWAYS_BLOCKED,
                //        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Focus Bear"), val.CurrentValue));
                //}
                SendKeys.SendWait("{ENTER}");

                appSettings.CapturePostHog("block-distracting-url");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("suppressURL EX -" + ex.Message);
            }
        }

      
        // search a current running process name recursively within the installation location
        //private bool isAppExists(List<InstalledApplication> apps, string process)
        //{
        //    string[] files;

        //    foreach (InstalledApplication app in apps)
        //    {
        //        if (!string.IsNullOrEmpty(app.installedLocation))
        //        {
        //            files = Directory.GetFiles(app.installedLocation, process + ".exe", SearchOption.AllDirectories);
        //            if (files.Length > 0)
        //                return true;
        //        }
        //    }
        //    return false;
        //}
    }

    
    public static class BrowserTitleGrabber
    {

        public static readonly Condition TabControlCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Tab);
        public static readonly Condition TabItemCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem);

        private delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern bool EnumThreadWindows(uint dwThreadId, EnumThreadDelegate lpfn, IntPtr lParam);

        public static IEnumerable<string> GetTabTitles(EBrowser browser)
        {
            switch (browser)
            {
                case EBrowser.Chrome:
                    return GetTabTitles("chrome");
                case EBrowser.Firefox:
                    return GetTabTitles("firefox");
                default:
                    throw new ArgumentOutOfRangeException(nameof(browser), browser, null);
            }
        }
        public static IEnumerable<string> GetTabTitles(string procName)
        {
            _cBt = new List<string>();
            foreach (var process in Process.GetProcessesByName(procName))
            {
                foreach (ProcessThread processThread in process.Threads)
                {
                    EnumThreadWindows((uint)processThread.Id, EnumThreadWindowsCb, IntPtr.Zero);
                }
            }
            return _cBt;
        }

        private static List<string> _cBt;
        private static bool EnumThreadWindowsCb(IntPtr hWnd, IntPtr lParam)
        {
            try
            {
                _cBt.AddRange(GetTabTitles(hWnd));
            }
            catch (Exception)
            {
                // ignored
            }
            return true;
        }

        private static IEnumerable<string> GetTabTitles(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return new List<string>();
            var rootElement = AutomationElement.FromHandle(handle);
            if (rootElement == null) return new List<string>();
            var tabControl = rootElement.FindFirst(System.Windows.Automation.TreeScope.Descendants, TabControlCondition);
            if (tabControl == null)
                return new List<string>();
            else
            {
                foreach( AutomationElement tab in tabControl.FindAll(System.Windows.Automation.TreeScope.Children, TabItemCondition))
                {
                    if(tab.Current.HasKeyboardFocus)
                    {
                        ; // return (from AutomationElement tab in tabControl.FindAll(System.Windows.Automation.TreeScope.Children, TabItemCondition) select tab.Current.Name).ToList();
                    }
                    try
                    {
                        AutomationPattern[] patterns = tab.GetSupportedPatterns();

                        SelectionItemPattern selectionItemPattern = tab.GetCurrentPattern(SelectionItemPattern.Pattern) as SelectionItemPattern;
                        if (selectionItemPattern.Current.IsSelected)
                        {
                            ;
                        }
                        else
                        {
                            ;// return selectionItemPattern.Current.SelectionContainer;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("xx EX -" + ex.Message);
                    }

                }
               
                return (from AutomationElement tab in tabControl.FindAll(System.Windows.Automation.TreeScope.Children, TabItemCondition) select tab.Current.Name).ToList();
            }
        }

        public enum EBrowser
        {
            Chrome,
            Firefox
        }
    }
}
