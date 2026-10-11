using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Interop.UIAutomationClient;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using TreeScope = Interop.UIAutomationClient.TreeScope;
using Focus_Bear.Utils;
using System.Linq;
using System.Management;
using Focus_Bear.Models;
using System.Windows.Automation;
using System.Text;
using Focus_Bear.Focus;
using System.Windows.Threading;
using Focus_Bear;
using System.Threading;
using FocusBear.Core.Blocking;

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

       

        public static readonly List<string> BLOCKED_URLS = new List<string> { "*.google.com", "facebook.com", "x.com" };
        public static readonly List<string> BLOCKED_APPS = new List<string> { "notepad", "regedit", "registry editor" };



        public static readonly List<string> UNBLOCKED_APPLICATIONS = new List<string> { "csrss", "dwm", "idle", "shellexperiencehost", "msedgewebview2", "explorer", "focusbear", "focus bear", "devenv", "taskmgr", "searchui", "searchhost", "applicationframehost", "textinputhost", "startmenuexperiencehost", "shellhost", "openwith", "searchapp", "lockapp", "snippingtool", "systemsettings" };
        public static readonly int SW_MINIMIZE = 6;
        public static readonly int SW_FORCEMINIMIZE = 11;



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

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);
        private const int SW_RESTORE = 9;

        /// <summary>
        /// "Oops I actually need it" on the app-blocked notice - the same flow as the blocked page's "Oops, I
        /// actually need this" and the Mac's notice button: forget this session's block on the app and bring it
        /// back to the front. The focus handler then sees it as fresh and shows the gentle block popup (allow /
        /// explain to the AI buddy / set intention), with that popup's usual gates and its time-limited allow.
        /// Nothing is allowed here directly.
        /// </summary>
        private void NeedBlockedAppAfterAll(string sAppName, IntPtr hWnd)
        {
            try
            {
                AppBlockNotice.ForgetSessionBlock(appSettings.TemporarryBlockedAppsGentleBlock, sAppName);
                appSettings.CapturePostHog("unblock-it-for-me-worked");
                LogWriter.LogWrite("FCH - Oops I actually need it - " + sAppName + ", re-presenting the block popup");
                if (hWnd == IntPtr.Zero || !IsWindow(hWnd))
                {
                    // The window minimised at the block can be gone (the app re-created it): use the app's main window.
                    hWnd = Process.GetProcessesByName(sAppName).Select(p => p.MainWindowHandle)
                                  .FirstOrDefault(h => h != IntPtr.Zero);
                }
                if (hWnd != IntPtr.Zero && IsWindow(hWnd))
                {
                    ShowWindow(hWnd, SW_RESTORE);
                    SetForegroundWindow(hWnd);
                    // Our own SetForegroundWindow raises no focus event we act on, so the app came back with no
                    // popup at all: effectively let through. Show the block popup for it now, as a switch to it
                    // would (cuddly mode: the gentle popup, which asks the AI when it's on).
                    LogWriter.LogWrite("FCH - Oops: showing the block popup again for " + sAppName);
                    // The same two steps as the cuddly path in processCurrentFocusElement: open the app for the
                    // alignment score (which asks the AI when it's on), then the popup.
                    FocusAlignmentScoreManager.FocusModeOpenCurrentAppForAlignmentScore(sAppName, appSettings.sRunningFocusModeName, 0, false, sAppName);
                    appSettings.ShowGentleBlockWindow(hWnd, sAppName);
                }
                else
                {
                    LogWriter.LogWrite("FCH - Oops: no window left for " + sAppName);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("FCH - NeedBlockedAppAfterAll ex - " + ex.Message);
            }
        }

        public FocusChangeHandler(/*MenuOptions listener*/)
        {
            //this.listener = listener;
        }

 
        public void HandleFocusChangedEvent(IUIAutomationElement element)
        {
            try { HandleFocusChangedEventCore(element); }
            finally { Focus_Bear.Utils.E2EBuild.NoteFocusHandled(element); }
        }

        private void HandleFocusChangedEventCore(IUIAutomationElement element)
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


            // A flag left set after its habit has gone must not minimise every app: clear it (logged once).
            RunningActivityRules.ClearIfStale(appSettings);

            if (appSettings.bHabitwithAllowedAppsRunning && !appSettings.bHabitwithUseFocusModeRunning)//
            {
                ProcessAllowedAppAndURLsInActivity(element); //only for apps
            }

            //A paused focus session must not keep blocking apps - pausing means "suspend focus enforcement
            //until resumed" (#1274). Treat isFocusModePaused like a paused blocking schedule (already
            //excluded via !ActiveSchedule.isPaused).
            if ((appSettings.isFocusModeRunning && !appSettings.isFocusModePaused) || appSettings.bHabitwithUseFocusModeRunning || appSettings.IsBlockingScheduleActiveAndNotPaused(out _))
            {
                processCurrentFocusElement(element);
            }

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

                        // One rule for every routine habit and break activity (Core ActivityAllowList): a blank entry
                        // allows nothing (it used to become the title pattern "**", allowing every app), and an index
                        // that doesn't point at the running activity no longer throws into "block nothing".
                        var rules = RunningActivityRules.For(appSettings);
                        if (rules.Label.Length == 0) return;   // not in a habit or break activity
                        if (rules.Known && ActivityAllowList.IsAppAllowed(process.ProcessName, ReadWindowTitle(process.MainWindowHandle), rules.Apps))
                        {
                            LogWriter.LogWrite(process.ProcessName + " allowed in " + rules.Label + " activity - ALLOW");
                            return;
                        }

                        LogWriter.LogWrite(process.ProcessName + " Not allowed app found in " + rules.Label + " activity"
                            + (rules.Known ? "" : " (the running activity's rules could not be found)") + " - BLOCK");
                        MinimizeBlockedAppWindow(process.MainWindowHandle);
                        appSettings.CapturePostHog("block-distracting-app");
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ProcessAllowedAppInActivity - " + ex.Message);
                Debug.WriteLine(ex.Message);
            }

        }

        private static string ReadWindowTitle(IntPtr hWnd)
        {
            try
            {
                const int nChars = 256;
                StringBuilder Buff = new StringBuilder(nChars);
                return GetWindowText(hWnd, Buff, nChars) > 0 ? Buff.ToString() : "";
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ProcessAllowedAppAndURLsInActivity GetWindowText ALLOWED app EX - " + ex.Message);
                return "";
            }
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

                        // Pre-check: if the focus mode governing blocking right now is the Relax / "block
                        // nothing" mode (e.g. a blocking schedule set to "Free time - no blocking"), nothing
                        // should be blocked - not the focus mode's own list and not the always-blocked apps.
                        // The URL path already skips everything for a relax mode; do the same for apps so a
                        // browser like Chrome isn't blocked under a no-blocking schedule (#1262).
                        FocusMode activeBlockingMode = appSettings.GetActiveBlockingFocusMode();
                        if (activeBlockingMode != null && activeBlockingMode.isRelaxFocus)
                        {
                            FocusAlignmentScoreManager.SetAppIconForFocusModeScore(null);
                            return;
                        }

                        // Check global whitelist (Always Allowed Apps) - these apps should never be blocked.
                        // Exception: during a teacher class session the student's personal whitelist must NOT
                        // apply - only apps the teacher has allowed are permitted, so skip this global list.
                        if (appSettings.arrAlwaysAllowedApps != null && appSettings.arrAlwaysAllowedApps.Count > 0
                            && !TeacherSessionManager.Instance.IsTeacherSessionActive())
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
                        else if (appSettings.IsBlockingScheduleActiveAndNotPaused(out var scheduleForFocusMode)) // Blocking schedule
                        {
                            listItems = appSettings.arrFocusModes.Where(i => i.id == scheduleForFocusMode.SelectedFocusModeId).ToList();
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

                                            if (WildcardPattern.Matches(sTitle, allowedAppTitle))
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

                                    // One ladder, in FocusBear.Core: snooze > teacher > buddy > the block list's
                                    // is_ai_enabled > the global toggle. This used to be hand-rolled here and skipped
                                    // the block list, so the Mac's is_ai_enabled=false had no effect on Windows.
                                    bool bAiCheckEnabled = AiBlocking.IsEnabled();

                                    // Cuddly -> gentle app popup. Non-cuddly (incl. Teacher) with AI enabled -> route through the
                                    // AI app-check too (GentleBlock blocks immediately on an AI-negative verdict). Non-cuddly
                                    // without AI falls through to the hard block below.
                                    if (bEffectiveCuddly || bAiCheckEnabled)//Cuddly bearMode, or non-cuddly with AI app-check
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
                                    // The window we are about to minimise - "Oops I actually need it" brings it back.
                                    IntPtr hBlockedWindow = GetEffectiveWindowHandle(process.MainWindowHandle);
                                    MinimizeBlockedAppWindow(process.MainWindowHandle);

                                    if (TeacherSessionManager.Instance.IsTeacherSessionActive())
                                    {
                                        // During a class session, the notification offers a "Request unlock" action that
                                        // opens the unlock-request form pre-filled with this app's name.
                                        string sAppName = process.ProcessName;
                                        string sMsg;
                                        try
                                        {
                                            sMsg = appSettings.ConfigFileStrings["teacherSessionInfo"]["appBlockedRequestUnlockText"].ToString();
                                        }
                                        catch
                                        {
                                            sMsg = "<APP_NAME> is blocked during your class session. Ask your teacher to unlock it.";
                                        }
                                        sMsg = sMsg.Replace("<APP_NAME>", sAppName);
                                        string sBtn = appSettings.ConfigFileStrings["teacherSessionInfo"]["buttonRequestUnlock"].ToString();
                                        appSettings.ShowBaloonTip(sMsg, "", sBtn,
                                            () => TeacherSessionManager.Instance.ShowTeacherUnlockRequestWindow(sAppName));
                                    }
                                    else
                                    {
                                        string sTemp = appSettings.ConfigFileStrings["common"]["appBlockNotificationTitle"].ToString();
                                        sTemp = sTemp.Replace("<APP_NAME>", process.ProcessName);
                                        sTemp = sTemp.Replace("<FOCUS_NAME>", appSettings.sRunningFocusModeName);

                                        // Mac FocusBlockNotificationManager: in cuddly mode the notice carries
                                        // "Oops I actually need it", the app equivalent of the blocked page's
                                        // "Oops, I actually need this" (which is also cuddly-only).
                                        bool bCuddlyNow = appSettings.nFocusBearMode == 1;
                                        if (AppBlockNotice.OffersNeedItButton(bCuddlyNow, false))
                                        {
                                            string sAppName = process.ProcessName;
                                            string sBtn = appSettings.GetConfigStringSafe("Oops I actually need it", "common", "appBlockNotificationAllowButton");
                                            appSettings.ShowBaloonTip(sTemp, "", sBtn, () => NeedBlockedAppAfterAll(sAppName, hBlockedWindow));
                                        }
                                        else
                                        {
                                            appSettings.ShowBaloonTip(sTemp);
                                        }
                                    }

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
