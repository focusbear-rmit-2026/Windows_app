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
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        //  private readonly MenuOptions listener;

       
        public static readonly int UIA_ControlTypePropertyId = 30003;
        public static readonly int UIA_EditControlTypeId = 50004;
        public static readonly int UIA_PATTERN_ID = 10002;

        public static readonly List<string> BLOCKED_URLS = new List<string> { "*.google.com", "facebook.com", "x.com" };
        public static readonly List<string> BLOCKED_APPS = new List<string> { "notepad", "regedit", "registry editor" };

        string sAlwaysBlockedWebUrl = "file:///" + AppSettings.FocusBlockPagePath + "?old_url={0}&block_type=always-blocked";
        string sFcousBlockedWebUrl = "file:///" + AppSettings.FocusBlockPagePath + "?old_url={0}&focus_mode_end_time={1}&focus_mode={2}&focus_mode_intention={3}";
        string sActivityBlockedWebUrl = "file:///" + AppSettings.FocusBlockPagePath + "?old_url={0}&block_type={1}"; //evening-habit, morning-habit, break-activity


        public static readonly List<string> UNBLOCKED_APPLICATIONS = new List<string> { "csrss", "dwm", "idle", "shellexperiencehost", "msedgewebview2", "explorer", "focusbear", "focus bear", "devenv", "taskmgr", "searchui", "searchhost", "applicationframehost", "textinputhost", "startmenuexperiencehost", "shellhost", "openwith", "searchapp", "lockapp", "snippingtool", "systemsettings" };
        public static readonly int SW_MINIMIZE = 6;
        public static readonly int SW_FORCEMINIMIZE = 11;

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


        private IntPtr GetEffectiveWindowHandle(IntPtr processMainWindowHandle)
        {
            if (processMainWindowHandle != IntPtr.Zero && IsWindowVisible(processMainWindowHandle))
            {
                return processMainWindowHandle;
            }
            return GetForegroundWindow();
        }

        private void MinimizeBlockedAppWindow(IntPtr processMainWindowHandle)
        {
            IntPtr hwnd = GetEffectiveWindowHandle(processMainWindowHandle);
            if (hwnd != IntPtr.Zero && IsWindowVisible(hwnd))
            {
                ShowWindow(hwnd, SW_FORCEMINIMIZE);
            }
        }

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
            /// 


            //if(appSettings.sLastActiveAppName != sAppNameInFocus)
            //{
            //    sAppNameInFocus = appSettings.sLastActiveAppName;
            //}
            //else 
            //{
            //    return;
            //}

#if DEBUG
            ///BrowserTitleGrabber.GetTabTitles(BrowserTitleGrabber.EBrowser.Firefox) ;

#endif
            //if its shutoff time, then we need to block all apps
            if (appSettings.nCurrentActivityType == CurrentActivityType.SHUT_OFF)
            {
                ProcessShutOffStateBlockAllApps(element);
            }


            if (appSettings.bHabitwithAllowedAppsRunning && !appSettings.bHabitwithUseFocusModeRunning)//
            {
                ProcessAllowedAppAndURLsInActivity(element); //only for apps
               // ProcessAlloweURLsInActivity(element);
            }

            if (appSettings.isFocusModeRunning || appSettings.bHabitwithUseFocusModeRunning || (appSettings.isBlockingScheduleRunning && !appSettings.ActiveSchedule.isPaused))
            {
                processCurrentFocusElement(element);
            }
 
            if (appSettings.bHabitwithAllowedAppsRunning || appSettings.isFocusModeRunning || (appSettings.isBlockingScheduleRunning && !appSettings.ActiveSchedule.isPaused))
                return;

            if (appSettings.IsAppDeactivated())//no need to track always blocked urls
            {
                return;
            }

            //#if !DEBUG
            if (appSettings.arrAlwaysBlockedURLs.Count() > 0)
                processCurrentURL(element); //For always Blocked URLS
//#endif
 
        }

        private void ProcessShutOffStateBlockAllApps(IUIAutomationElement element)
        {
            try
            {
                using (Process process = Process.GetProcessById(element.CurrentProcessId))
                {
                    if (element != null && process != null)
                    {
                        // block or Unblock apps
                        if (UNBLOCKED_APPLICATIONS.Contains(process.ProcessName.ToLower()))
                        {
                            return;
                        }

                        LogWriter.LogWrite(process.ProcessName + " Not allowed app When in shutOff State -  BLOCK");
                        MinimizeBlockedAppWindow(process.MainWindowHandle);
                        appSettings.CapturePostHog("block-distracting-app");
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ProcessShutOffStateBlockAllApps - " + ex.Message);
            }
        }
        private void ProcessAllowedAppAndURLsInActivity(IUIAutomationElement element)
        {
            try
            {
                using (Process process = Process.GetProcessById(element.CurrentProcessId))
                {
                    if (element != null && process != null)
                    {
                        // block or Unblock apps
                        if (UNBLOCKED_APPLICATIONS.Contains(process.ProcessName.ToLower()))
                        {
                            return;
                        }

                        bool bAllowedAppFound = false;
                        if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                        {
                            foreach (string sApp in appSettings.arrAllMorningHabits[appSettings.nHabitIndex].allowedApps)
                            {
                                string App = sApp.ToLower().Replace(".exe", "");
                                if (App == process.ProcessName.ToLower())
                                {
                                    bAllowedAppFound = true;
                                    LogWriter.LogWrite(sApp + " allow app found in MORNING_HABITS activity - break");
                                    break;
                                }

                                //Now if we have not found any we need to look and match the app and allow / block apps with window title
                                try
                                {
                                    //Check title
                                    const int nChars = 256;
                                    StringBuilder Buff = new StringBuilder(nChars);
                                    if (GetWindowText(process.MainWindowHandle, Buff, nChars) > 0)
                                    {
                                        string sTitle = Buff.ToString().ToLower();
                                        string allowedAppTitle = "*" + App + "*";
                                        if (MatchWildcardString(sTitle, allowedAppTitle))
                                        {
                                            LogWriter.LogWrite(sTitle + "title found in ALLOWED apps apps with window title matched in MORNING_HABITS activity - ALLOW");
                                            bAllowedAppFound = true;
                                            break;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogWriter.LogWrite("ProcessAllowedAppAndURLsInActivity GetWindowText ALLOWED app EX - " + ex.Message);
                                }
                            }
                            if (!bAllowedAppFound)
                            {
                                LogWriter.LogWrite(process.ProcessName + " Not allowed app found in MORNING_HABITS activity - BLOCK");
                                MinimizeBlockedAppWindow(process.MainWindowHandle);
                                appSettings.CapturePostHog("block-distracting-app");
                            }
                        }
                        else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                        {
                            foreach (string sApp in appSettings.arrAllEveningHabits[appSettings.nHabitIndex].allowedApps)
                            {
                                string App = sApp.ToLower().Replace(".exe", "");
                                if (App == process.ProcessName.ToLower())
                                {
                                    bAllowedAppFound = true;
                                    LogWriter.LogWrite(sApp + " allow app found in EVENING_HABITS activity -  break;");
                                    break;
                                }

                                //Now if we have not found any we need to look and match the app and allow / block apps with window title
                                try
                                {
                                    //Check title
                                    const int nChars = 256;
                                    StringBuilder Buff = new StringBuilder(nChars);
                                    if (GetWindowText(process.MainWindowHandle, Buff, nChars) > 0)
                                    {
                                        string sTitle = Buff.ToString().ToLower();
                                        string allowedAppTitle = "*" + App + "*";
                                        if (MatchWildcardString(sTitle, allowedAppTitle))
                                        {
                                            LogWriter.LogWrite(sTitle + "title found in ALLOWED apps apps with window title matched in EVENING_HABITS activity - ALLOW");
                                            bAllowedAppFound = true;
                                            break;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogWriter.LogWrite("ProcessAllowedAppAndURLsInActivity GetWindowText ALLOWED app EX - " + ex.Message);
                                }
                            }
                            if (!bAllowedAppFound)
                            {
                                LogWriter.LogWrite(process.ProcessName + " Not allowed app found in EVENING_HABITS activity - BLOCK");
                                MinimizeBlockedAppWindow(process.MainWindowHandle);
                                appSettings.CapturePostHog("block-distracting-app");
                            }
                        }
                        else  if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                        {

                            if (appSettings.isMandatoryActivityGoing)
                            {
                                foreach (string sApp in appSettings.arrActivitiesIncludeInEveryBreak[appSettings.nHabitIndex].allowedApps)
                                {
                                    string App = sApp.ToLower().Replace(".exe", "");
                                    if (App == process.ProcessName.ToLower())
                                    {
                                        bAllowedAppFound = true;
                                        LogWriter.LogWrite(sApp + " allow app found in BREAK_ACTIVITY activity -  break;");
                                        break;
                                    }

                                    //Now if we have not found any we need to look and match the app and allow / block apps with window title
                                    try
                                    {
                                        //Check title
                                        const int nChars = 256;
                                        StringBuilder Buff = new StringBuilder(nChars);
                                        if (GetWindowText(process.MainWindowHandle, Buff, nChars) > 0)
                                        {
                                            string sTitle = Buff.ToString().ToLower();
                                            string allowedAppTitle = "*" + App + "*";
                                            if (MatchWildcardString(sTitle, allowedAppTitle))
                                            {
                                                LogWriter.LogWrite(sTitle + "title found in ALLOWED apps apps with window title matched in BREAK_ACTIVITY activity - ALLOW");
                                                bAllowedAppFound = true;
                                                break;
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LogWriter.LogWrite("ProcessAllowedAppAndURLsInActivity GetWindowText ALLOWED app EX - " + ex.Message);
                                    }

                                }
                                if (!bAllowedAppFound)
                                {
                                    LogWriter.LogWrite(process.ProcessName + " Not allowed app found in BREAK_ACTIVITY activity - BLOCK");
                                    MinimizeBlockedAppWindow(process.MainWindowHandle);
                                    appSettings.CapturePostHog("block-distracting-app");
                                }
                            }
                            else
                            {
                                foreach (string sApp in appSettings.arrActivitiesExcludeInEveryBreak[appSettings.nHabitIndex].allowedApps)
                                {
                                    string App = sApp.ToLower().Replace(".exe", "");
                                    if (App == process.ProcessName.ToLower())
                                    {
                                        bAllowedAppFound = true;
                                        LogWriter.LogWrite(sApp + " allow app found in BREAK_ACTIVITY activity -  break;");
                                        break;
                                    }

                                    //Now if we have not found any we need to look and match the app and allow / block apps with window title
                                    try
                                    {
                                        //Check title
                                        const int nChars = 256;
                                        StringBuilder Buff = new StringBuilder(nChars);
                                        if (GetWindowText(process.MainWindowHandle, Buff, nChars) > 0)
                                        {
                                            string sTitle = Buff.ToString().ToLower();
                                            string allowedAppTitle = "*" + App + "*";
                                            if (MatchWildcardString(sTitle, allowedAppTitle))
                                            {
                                                LogWriter.LogWrite(sTitle + "title found in ALLOWED apps apps with window title matched in BREAK_ACTIVITY activity - ALLOW");
                                                bAllowedAppFound = true;
                                                break;
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LogWriter.LogWrite("ProcessAllowedAppAndURLsInActivity GetWindowText ALLOWED app EX - " + ex.Message);
                                    }
                                }
                                if (!bAllowedAppFound)
                                {
                                    LogWriter.LogWrite(process.ProcessName + " Not allowed app found in BREAK_ACTIVITY activity - BLOCK");
                                    MinimizeBlockedAppWindow(process.MainWindowHandle);
                                    appSettings.CapturePostHog("block-distracting-app");
                                }
                            }

                        }

                        //If its an allowed app and if its browser, then let's check the URLs
                        if(bAllowedAppFound)
                        {
                            // block or Unblock URLS
                            if (Global.BROWSERS.Exists(ele => ele == process.ProcessName))
                            {
                               /// if (appSettings.arrAllMorningHabits[appSettings.nHabitIndex].allowedApps.Exists(ele => ele == process.ProcessName))
                                {
                                    if (process.ProcessName.ToLower() == "firefox" || process.ProcessName.ToLower() == "chrome" || process.ProcessName.ToLower() == "msedge")
                                    {
                                        return;
                                        //ProcessAlloweURLsInActivity(appSettings.sLastActiveURLInFirefox,  process.MainWindowHandle, element, null, true);
                                    }
                                    else
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
                                                if (val != null && !string.IsNullOrEmpty(val.CurrentValue) && (!val.CurrentValue.ToString().Contains("focus-block.html")))
                                                {
                                                    ProcessAlloweURLsInActivity(val.CurrentValue, process.MainWindowHandle, element, val);
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
                LogWriter.LogWrite("ProcessAllowedAppInActivity - " + ex.Message);
                Debug.WriteLine(ex.Message);
            }

        }
        private void ProcessAlloweURLsInActivity(string sURL, IntPtr hWnd, IUIAutomationElement element, IUIAutomationValuePattern val, bool bForFirefox = false)
        {
            return; //handled by PBU
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
                        if (UNBLOCKED_APPLICATIONS.Contains(process.ProcessName.ToLower()))
                        {
                            FocusAlignmentScoreManager.SetAppIconForFocusModeScore(null);
                            if("focus bear" != process.ProcessName.ToLower())
                                appSettings.HideGentleBlockWindow();
                            return;
                        }
                        
                        TabMetrics.Instance.FrontAppChanged(process.ProcessName);

                        // Check global whitelist (Always Allowed Apps) - these apps should never be blocked
                        if (appSettings.arrAlwaysAllowedApps != null && appSettings.arrAlwaysAllowedApps.Count > 0)
                        {
                            foreach (AppDetails allowedApp in appSettings.arrAlwaysAllowedApps)
                            {
                                foreach (string sAppExe in allowedApp.ExeNames)
                                {
                                    string appName = sAppExe.Replace(".exe", "").ToLower();
                                    if (appName == process.ProcessName.ToLower())
                                    {
                                        LogWriter.LogWrite("App " + process.ProcessName + " is in Always Allowed Apps list - ALLOW");
                                        return;
                                    }
                                }
                            }
                        }

                        // Check global Block lis (Always blocked Apps) - these apps should ALWAYS be blocked
                        if (appSettings.arrAlwaysBlockedApps != null && appSettings.arrAlwaysBlockedApps.Count > 0)
                        {
                            foreach (AppDetails allowedApp in appSettings.arrAlwaysBlockedApps)
                            {
                                foreach (string sAppExe in allowedApp.ExeNames)
                                {
                                    string appName = sAppExe.Replace(".exe", "").ToLower();
                                    if (appName == process.ProcessName.ToLower())
                                    {
                                        LogWriter.LogWrite("App " + process.ProcessName + " is in Always BLOCKED Apps list - ALLOW");
                                        MinimizeBlockedAppWindow(process.MainWindowHandle);

                                        string sTemp = appSettings.ConfigFileStrings["common"]["superDistractingAppBlockedText"].ToString();
                                        sTemp = sTemp.Replace("<APP_NAME>", process.ProcessName);
                                        appSettings.ShowBaloonTip(sTemp);

                                        LogWriter.LogWrite(process.ProcessName + " Not allowed app found as its Super Distracting app - BLOCK");
                                        appSettings.CapturePostHog("block-super-distracting-app");

                                        return; //if not an allowed app, no need to check URLs
                                    }
                                }
                            }
                        }

                        /////  string fullPath = ProcessExecutablePath(process);
                        List<FocusMode> listItems = new List<FocusMode>();

                        if(appSettings.bHabitwithUseFocusModeRunning)
                        {
                            listItems = appSettings.arrFocusModes.Where(i => i.id == appSettings.sUseFocusModeIDInActivity).ToList();
                        }
                        else if (appSettings.isBlockingScheduleRunning && !appSettings.ActiveSchedule.isPaused) // Blocking schedule
                        {
                            listItems = appSettings.arrFocusModes.Where(i => i.id == appSettings.ActiveSchedule.SelectedFocusModeId).ToList();
                        }
                        else //Normal focus mode running
                        {
                            listItems = appSettings.arrFocusModes.Where(i => i.isOn == true).ToList();
                        }
  
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
                                        string processName = process.ProcessName.ToLower();
                                        // Check if app name is contained in process name (handles "whatsapp" vs "WhatsApp.Root")
                                        if (App == processName || processName.Contains(App))
                                        {
                                            bAllowedAppFound = true;
                                            break;
                                        }
                                    }

                                    if (bAllowedAppFound) //we have found a matching exe name
                                        break;

                                    //Now if we have not found any we need to look and match the app and allow / block apps with window title
                                    try
                                    {
                                        //Check title
                                        const int nChars = 256;
                                        StringBuilder Buff = new StringBuilder(nChars);
                                        if (GetWindowText(process.MainWindowHandle, Buff, nChars) > 0)
                                        {
                                            string sTitle = Buff.ToString().ToLower();
                                            // Strip .exe so "whatsapp.exe" matches window title "WhatsApp"
                                            string appNameClean = sApp.AppName.ToLower().Replace(".exe", "");
                                            string allowedAppTitle = "*" + appNameClean + "*";

                                            if (MatchWildcardString(sTitle, allowedAppTitle))
                                            {
                                                LogWriter.LogWrite(sTitle + "title found in ALLOWED apps  apps with window title matched in focus mode - ALLOW");
                                                bAllowedAppFound = true;
                                                break;
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LogWriter.LogWrite("processCurrentFocusElement GetWindowText ALLOWED URLS EX - " + ex.Message);
                                    }
                                }
                                if(focusType.isDefault)
                                {
                                    //It means we have found an app that user wants to block as its the other way round in Default (block super distracting apps/sites) focus mode, see focus mode setting UI
                                    if(bAllowedAppFound)
                                        bAllowedAppFound = false;
                                    else
                                        bAllowedAppFound = true; //all other app then user listed apps are allowed in this focus mode
                                }
                                if (!bAllowedAppFound)
                                {
                                    // Teacher Mode forces non-cuddly (grizzly) behaviour for apps too, regardless of the
                                    // user's own Focus Bear mode - so an unlisted app is hard-blocked, no gentle popup.
                                    bool bEffectiveCuddly = (appSettings.nFocusBearMode == 1) && !TeacherSessionManager.Instance.IsTeacherSessionActive();
                                    if (bEffectiveCuddly)//Cuddly bearMode
                                    {
                                        var allowedApps = appSettings.TemporaryAllowedAppsGentleBlock.Where(i => i.sURL.ToLower() == process.ProcessName.ToLower());
                                        if (allowedApps.Count() != 0)
                                        {
                                            //If the app is allowed and is not one of the browsers, we will OPEN is to calculate
                                            if (!Global.BROWSERS.Exists(ele => ele == process.ProcessName))
                                            {
                                                FocusAlignmentScoreManager.FocusModeOpenCurrentAppForAlignmentScore(process.ProcessName, appSettings.sRunningFocusModeName, 0, false, process.ProcessName);
                                            }
                                            //found in allowed app
                                            bAllowedAppFound = true; //allow the app //If its a browser we will check the urls too
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
                                                //only open for non browser apps
                                                //If the app is allowed and is not one of the browsers, we will OPEN is to calculate
                                                if (!Global.BROWSERS.Exists(ele => ele == process.ProcessName))
                                                {
                                                    FocusAlignmentScoreManager.FocusModeOpenCurrentAppForAlignmentScore(process.ProcessName, appSettings.sRunningFocusModeName, 0, false, process.ProcessName);
                                                }
                                                appSettings.ShowGentleBlockWindow(process.MainWindowHandle, process.ProcessName);
                                                return;
                                            }
                                        }
                                    }
                                }
                                else //allowed app
                                {
                                    //If the app is allowed and is not one of the browsers, we will OPEN is to calculate
                                    if (!Global.BROWSERS.Exists(ele => ele == process.ProcessName))
                                    {
                                        FocusAlignmentScoreManager.FocusModeOpenCurrentAppForAlignmentScore(process.ProcessName, appSettings.sRunningFocusModeName, 1, true, process.ProcessName);
                                    }
                                }

                                //We must here hide the GEntle block window if its shown for any other app.
                                appSettings.HideGentleBlockWindow();

                                if (!bAllowedAppFound)
                                {
                                    MinimizeBlockedAppWindow(process.MainWindowHandle);

                                    string sTemp = appSettings.ConfigFileStrings["common"]["appBlockNotificationTitle"].ToString();
                                    sTemp = sTemp.Replace("<APP_NAME>", process.ProcessName);
                                    sTemp = sTemp.Replace("<FOCUS_NAME>", appSettings.sRunningFocusModeName);
                                    appSettings.ShowBaloonTip(sTemp);

                                    LogWriter.LogWrite(process.ProcessName + " Not allowed app found in Focus Element activity - BLOCK");
                                    appSettings.CapturePostHog("block-distracting-app");

                                    return; //if not an allowed app, no need to check URLs
                                }
                            }

                            //If the app is one of the browsers, ther we will check the URLS
                            if (Global.BROWSERS.Exists(ele => ele == process.ProcessName))
                            {
                                if (focusType.isAllowedListApproach )//Block everything except safe sites I choose
                                {
                                    //if (focusType.isAllowedAllUrls) //All urls are allowed, nothng to block
                                    //{
                                    //    LogWriter.LogWrite("isAllowedListApproach - isAllowedAllUrls, return No need to get the url");
                                    //    return; //No need to get the url
                                    //}
                                }

                                //
                                LogWriter.LogWrite("FCH - RUNNING Focus Mode is:" + focusType.name);

                                if (process.ProcessName.ToLower() == "firefox" || process.ProcessName.ToLower() == "chrome" || process.ProcessName.ToLower() == "msedge")
                                {
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("processCurrentFocusElement EX - " + ex.Message);
            }
        }

        private void ProcessURLForFocusModes(string sURL, FocusMode focusType, IntPtr hWnd, IUIAutomationElement element, IUIAutomationValuePattern val, bool bForFirefox = false )
        {

            if (sURL.Contains("file://"))
            {
                return;
            }
            if (appSettings.nFocusBearMode == 1)
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
                        LogWriter.LogWrite("FCH processCurrentFocusElement  isAllowedListApproach - Attempt to Suppress URL");
                        if(bForFirefox)
                        {
                            suppressURLForFirefox(hWnd,sURL, "focus");
                        }
                        else
                        {
                            suppressURL(hWnd, val, "focus");
                        }
                        
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
                        LogWriter.LogWrite("FCH - processCurrentFocusElement isBlockedListApproach - Attempt to Suppress URL");
                        if (bForFirefox)
                        {
                            suppressURLForFirefox(hWnd, sURL, "focus");
                        }
                        else
                        {
                            suppressURL(hWnd, val, "focus");
                        }
                        return;
                    }
                    else //Check the URL in Always BLocked URL List
                    {
                        if (appSettings.arrAlwaysBlockedURLs.Count() > 0)
                            processCurrentURL(element); //For always Blocked URLS
                    }
                }
            }

        }

        private void processCurrentURL(IUIAutomationElement element)
        {
            return;
        }

        private void suppressURLForFirefox(IntPtr hWnd, string val, string type, string focusMode = null)
        {
            try
            {
                LogWriter.LogWrite("FCH - suppressURLForFirefox - Block URL - " + val);

                if (type == "focus")
                {
                    if (appSettings.nFocusBearMode == 1)//Cuddly bearMode
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
                            //if found in allowed URLs then add the title in the keywords list
                            try
                            {
                                //Check title
                                const int nChars = 256;
                                StringBuilder Buff = new StringBuilder(nChars);
                                if (GetWindowText(hWnd, Buff, nChars) > 0)
                                {
                                    string sTitle = Buff.ToString();
                                    if(sTitle.Length > 0)
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
                                LogWriter.LogWrite("alwaysblockedurls GetWindowText blockedurls EX - " + ex.Message);
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
                                    LogWriter.LogWrite("FCS suppressURLForFirefox 1 GetWindowText blockedurls EX - " + ex.Message);
                                }

                                appSettings.ShowGentleBlockWindow(hWnd, "", sURLForFocusMode, sTitle);
                                return;
                            }
                        }
                    }

                    
                    if (appSettings.bHabitwithUseFocusModeRunning)
                    {
                        //string sActivityBlockedWebUrl = "https://focus-bear.github.io/assets/focus-blocked?old_url={0}&block_type={1}"; //evening-habit, morning-habit, break-activity
                        string sURL = string.Format(sActivityBlockedWebUrl, val, appSettings.sHabitTypeForUseFocusModeIDInActivity);
                        if (appSettings.nFocusBearMode == 1)//cuddly mode
                            sURL = sURL + "&cuddly_bear_mode=true";
                        appSettings.FirefoxLastAccWindow.accValue[appSettings.objChild] = sURL;

                    }
                    else
                    {
                        DateTime end = DateTime.Parse(appSettings.sRunningFocusModeEndTime).ToUniversalTime();
                        string sEndTime = end.ToString("o");
                        string sFcousBlockedUrl = string.Format(sFcousBlockedWebUrl, val, sEndTime, appSettings.sRunningFocusModeName, appSettings.sRunningFocusModeIntention);
                        // "https://focus-bear.github.io/assets/focus-blocked/?old_url={0)&focus_mode_end_time={1}&focus_mode={2}&focus_mode_intention={3}";
                        
                        if(appSettings.nFocusBearMode == 1)//cuddly mode
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

                SendKeys.SendWait("{ENTER}");

                appSettings.CapturePostHog("block-distracting-url");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("suppressURLForFirefox EX -" + ex.Message);
            }


        }
        private void suppressURL(IntPtr hWnd,  IUIAutomationValuePattern val, string type, string focusMode = null)
        {
            try
            {
                LogWriter.LogWrite("suppressURL - Block URL - " + val.CurrentValue);


                if (type == "focus")
                {
                    if (appSettings.nFocusBearMode == 1)//Cuddly bearMode
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
                            /*
                            This constructor initializes a new instance of the UriBuilder class with the Fragment, Host, Path, Port, Query, Scheme, and Uri properties set as specified in uri.
                            If uri does not specify a scheme, the scheme defaults to "http:".
                            */
                            Uri myUri = new UriBuilder(val.CurrentValue).Uri; 
                            sURLForFocusMode = myUri.Host; 
                            Debug.WriteLine(ex.Message);
                        }

                        var allowedURLs = appSettings.TemporaryAllowedURLsGentleBlock.Where(i => i.sURL.ToLower() == sURLForFocusMode.ToLower());
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
                                    LogWriter.LogWrite("FCS suppressURLForFirefox 1 GetWindowText blockedurls EX - " + ex.Message);
                                }

                                appSettings.ShowGentleBlockWindow(hWnd,"", sURLForFocusMode, sTitle);
                                return;
                            }
                        }
                    }

                    DateTime end = DateTime.Parse(appSettings.sRunningFocusModeEndTime).ToUniversalTime();
                    string sEndTime = end.ToString("o");

                    if (appSettings.bHabitwithUseFocusModeRunning)
                    {
                        //string sActivityBlockedWebUrl = "https://focus-bear.github.io/assets/focus-blocked?old_url={0}&block_type={1}"; //evening-habit, morning-habit, break-activity
                        string sURL = string.Format(sActivityBlockedWebUrl, val.CurrentValue, appSettings.sHabitTypeForUseFocusModeIDInActivity);
                        if (appSettings.nFocusBearMode == 1)//cuddly mode
                            sURL = sURL + "&cuddly_bear_mode=true";

                        val.SetValue(sURL);
                    }
                    else
                    {
                        string sFcousBlockedUrl = string.Format(sFcousBlockedWebUrl, val.CurrentValue, sEndTime, appSettings.sRunningFocusModeName, appSettings.sRunningFocusModeIntention);
                        // "https://focus-bear.github.io/assets/focus-blocked/?old_url={0)&focus_mode_end_time={1}&focus_mode={2}&focus_mode_intention={3}";
                        if (appSettings.nFocusBearMode == 1)//cuddly mode
                            sFcousBlockedUrl = sFcousBlockedUrl + "&cuddly_bear_mode=true";
                        val.SetValue(sFcousBlockedUrl);
                    }
                }
                else if(type == "blocked")
                {
                    val.SetValue(string.Format(sAlwaysBlockedWebUrl, val.CurrentValue));
                
                }
                else
                {
                    //string sActivityBlockedWebUrl = "https://focus-bear.github.io/assets/focus-blocked?old_url={0}&block_type={1}"; //evening-habit, morning-habit, break-activity
                    val.SetValue(string.Format(sActivityBlockedWebUrl, val.CurrentValue, type));
                }

                SendKeys.SendWait("{ENTER}");

                appSettings.CapturePostHog("block-distracting-url");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("suppressURL EX -" + ex.Message);
            }
        }

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
            return tabControl == null ? new List<string>() : (from AutomationElement tab in tabControl.FindAll(System.Windows.Automation.TreeScope.Children, TabItemCondition) select tab.Current.Name).ToList();
        }

        public enum EBrowser
        {
            Chrome,
            Firefox
        }
    }


}
