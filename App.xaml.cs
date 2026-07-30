using Focus_Bear.API;
using Focus_Bear.BrainDump;
using Focus_Bear.Focus;
using Focus_Bear.Habits;
using Focus_Bear.Login;
using Focus_Bear.Models;
using Focus_Bear.Onboarding;
using Focus_Bear.Preferences;
using Focus_Bear.Quit;
using Focus_Bear.ShutOff;
using Focus_Bear.Upgrade;
using Focus_Bear.Utils;
using FocusBear.Utils;
using Interop.UIAutomationClient;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;
using Sentry;
using Microsoft.Win32;
using Focus_Bear.Relax;
using Focus_Bear.Tour;
using Focus_Bear.Survey;
using Focus_Bear.Pomodoro;
using Focus_Bear.CallDetection;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Web.UI;
using System.Windows.Controls;
using Focus_Bear.Habits.NaturalBreaks;
using Focus_Bear.Services;
using Focus_Bear.FocusMusic;
using Focus_Bear.AccountabilityBuddy;

using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Collections.ObjectModel;
using static Focus_Bear.Focus.TabMetrics;
using Focus_Bear.Services.FocusButton;
using Sentry.Protocol;

namespace Focus_Bear
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        AppSettings appSettings = AppSettings.GetInstance;
        
        private const string MutexGUID = "369F3719-1C05-41A1-B9E1-FC0CF0FCE3AE";

        private const string EventMutex = "9E2C7FEB-476B-41D3-9F7F-D02C512595C0";
        private EventWaitHandle appWaitHandle;

        private Mutex appMutex;
        bool createdNew;

        [DllImport("oleacc.dll")]
        public static extern uint AccessibleObjectFromEvent(IntPtr hwnd, uint dwObjectID, uint dwChildID, out IAccessible ppacc, [MarshalAs(UnmanagedType.Struct)] out object pvarChild);

        [DllImport("user32.dll")]
        static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        WinEventDelegate dele = null;

        delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
        [DllImport("user32.dll")]
        static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        static extern IntPtr UnhookWinEvent(IntPtr hWinEventHook);
        IntPtr m_hhook = IntPtr.Zero;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const int SW_SHOW = 5;

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private const int SW_RESTORE = 9;


        private const uint WINEVENT_OUTOFCONTEXT = 0;
        private const uint EVENT_SYSTEM_FOREGROUND = 3;
        private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

        private bool bScreenSaverStarted = false;
        private bool bSuggestMeetingsPopupShownForThisCallSession = false;
        public bool bUserManuallyClosedNotifierForThisCallSession = false;

        bool bLoadURLAgainInToDoPlayer = false;

        public TabMetrics TebMetricsObject = new TabMetrics();

        public static FocusButtonService FocusButton = new FocusButtonService();
        public void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            //Always get the last active app name when the menu is clicked and ignore Focus Bear
            string sAppName = GetLastActiveAppName(hwnd);

            if (string.IsNullOrEmpty(sAppName))
                return;

            // The hook fires for object-level events (focus / value-change) from EVERY process, not just
            // foreground changes. Background apps (e.g. Visual Studio, Teams) emit a constant stream of
            // these, and acting on them spuriously hid the Set Intention popup the instant it appeared
            // while the user was still on the foreground browser. Only treat an event as an app-switch when
            // it actually comes from the foreground window's process; otherwise ignore this hide pass.
            bool bFromForegroundApp = false;
            try
            {
                _ = GetWindowThreadProcessId(hwnd, out IntPtr evtPid);
                _ = GetWindowThreadProcessId(GetForegroundWindow(), out IntPtr fgPid);
                bFromForegroundApp = evtPid != IntPtr.Zero && evtPid == fgPid;
            }
            catch (Exception ex) { LogWriter.LogWrite("foreground-check ex - " + ex.Message); }

            if (bFromForegroundApp && sAppName != "Focus Bear.exe" && sAppName != "explorer.exe")
            {
                //Click through is permitted from blur window and if the user was clicking on the same, it was changing app and blur was being removed. placing this check resolves it
                bool bIsBrowser = Global.BROWSERS.Contains(Path.GetFileNameWithoutExtension(sAppName));
                if (bIsBrowser && appSettings.sLastActiveAppName != sAppName)
                {
                    appSettings.Hide_SetIntentionWindow();
                }
                else if(!bIsBrowser)
                {
                    appSettings.Hide_SetIntentionWindow();
                }

                appSettings.sLastActiveAppName = sAppName;
            }
            else if(bFromForegroundApp && sAppName == "explorer.exe")
            {
                appSettings.Hide_SetIntentionWindow();
            }

            // Tab limiter: fire a debounced check whenever the user interacts with a browser
            if (Global.BROWSERS.Contains(Path.GetFileNameWithoutExtension(sAppName)))
            {
                TriggerTabLimitCheck();
            }


            try
            {
                if (appSettings.sLastActiveAppName == "firefox.exe")
                {
                    int nRet;
                    // Pre-allocate 256 characters, since this is the maximum class name length.
                    StringBuilder ClassName = new StringBuilder(256);
                    //Get the window class name
                    nRet = GetClassName(hwnd, ClassName, ClassName.Capacity);
                    if (nRet != 0)
                    {
                        if (string.Compare(ClassName.ToString(), "MozillaWindowClass", true, CultureInfo.InvariantCulture) == 0)
                        {
                            object objChild = null;
                            IAccessible accWindow = null;
                            uint hr = AccessibleObjectFromEvent(hwnd, (uint)idObject, (uint)idChild, out accWindow, out objChild);
                            if (hr == 0 && accWindow != null)
                            {
                                if (eventType == 0x800E)
                                {
                                    string sName = accWindow.accName[objChild];
                                    string sURL = accWindow.accValue[objChild];
                                    if(sURL == null)
                                    {
                                        appSettings.Hide_SetIntentionWindow();
                                    }
                                    if (sName != null && (sName.StartsWith("Buscar con") || sName.StartsWith("Search with")) && (sURL != null && sURL.Length > 0)) //It's our FF address bar
                                    {
                                        if (int.Parse(accWindow.accState[objChild].ToString()) == 1074791424) //I just found this to be appropriate and not editable state
                                        {
                                            Debug.WriteLine("accName = " + sName);
                                            Debug.WriteLine("accValue = " + sURL);
                                            Debug.WriteLine("accChild = " + accWindow.accChild[objChild].ToString());
                                            Debug.WriteLine("accState = " + accWindow.accState[objChild].ToString());

                                            appSettings.FirefoxLastAccWindow = accWindow;
                                            appSettings.sLastActiveURLInFirefox = sURL;
                                            appSettings.objChild = objChild;

                                            ProcessBrowserURLs.ProcessCurrentURL(hwnd);
                                        }

                                    }
                                    else if (sName != null && (sName.Contains("Focus Bear/focus-block.html")) && (sURL != null && sURL.Length > 0 && sURL.Contains("&unblock_it=true")))
                                    {
                                        LogWriter.LogWrite("App.cs - URL = " + sURL);

                                        if (int.Parse(accWindow.accState[objChild].ToString()) == 1074791424) //I just found this to be appropriate and not editable state
                                        {
                                            Debug.WriteLine("accName = " + sName);
                                            Debug.WriteLine("accValue = " + sURL);
                                            Debug.WriteLine("accChild = " + accWindow.accChild[objChild].ToString());
                                            Debug.WriteLine("accState = " + accWindow.accState[objChild].ToString());

                                            appSettings.FirefoxLastAccWindow = accWindow;
                                            appSettings.sLastActiveURLInFirefox = sURL;
                                            appSettings.objChild = objChild;

                                            ProcessBrowserURLs.ProcessCurrentURL(hwnd);
                                        }

                                    }
                                    else if (sName != null && sName.Contains("focus-block.html") &&
                                             Focus.TeacherSessionManager.Instance.IsTeacherSessionActive())
                                    {
                                        //Teacher Mode - the student is (back) on a class-session block page whose address
                                        //bar value reads empty (so the branches above don't catch it), but sName holds the
                                        //full block-page URL. If the teacher has since approved this URL, remove the block
                                        //page and load the original URL. This covers the case where the approval arrived
                                        //while the student was on another tab and they later return to the block page.
                                        string sBlockOldUrl = ProcessBrowserURLs.ExtractOldUrl(sName) ?? "";
                                        if (sBlockOldUrl.Length > 0 && Focus.TeacherSessionManager.Instance.IsUrlAllowedInTeacherMode(sBlockOldUrl))
                                        {
                                            LogWriter.LogWrite("App.cs - teacher block page for now-allowed url, reloading - " + sBlockOldUrl);
                                            appSettings.FirefoxLastAccWindow = accWindow;
                                            appSettings.objChild = objChild;
                                            appSettings.sLastActiveURLInFirefox = sName;
                                            ProcessBrowserURLs.ReloadForegroundBrowserToUrl(hwnd, sBlockOldUrl);
                                            appSettings.sLastFocusBlockedUrl = "";
                                        }
                                    }
                                    else
                                    {
                                        LogWriter.LogWrite("Mozilla's sName is null or other language then EN / ES- OR (sURL != null && sURL.Length > 0 FAILS. TITLE = " + sName + "URL = " + sURL);
                                    }
                                }
                            }
                        }
                    }
                }
                else if (appSettings.sLastActiveAppName == "chrome.exe" || appSettings.sLastActiveAppName == "msedge.exe" || appSettings.sLastActiveAppName == "brave.exe" || appSettings.sLastActiveAppName == "vivaldi.exe" || appSettings.sLastActiveAppName == "comet.exe")
                {
                    int nRet;
                    // Pre-allocate 256 characters, since this is the maximum class name length.
                    StringBuilder ClassName = new StringBuilder(256);
                    //Get the window class name
                    nRet = GetClassName(hwnd, ClassName, ClassName.Capacity);
                    // Vivaldi fires events from a child hwnd with no class name, so we walk up to the root window
                    if (nRet == 0 && appSettings.sLastActiveAppName == "vivaldi.exe")
                    {
                        IntPtr rootHwnd = GetAncestor(hwnd, GA_ROOT);
                        nRet = GetClassName(rootHwnd, ClassName, ClassName.Capacity);
                        if (nRet != 0)
                            hwnd = rootHwnd;
                    }

                    if (nRet != 0)
                    {
                        // For Vivaldi, events can also fire from Chrome_RenderWidgetHostHWND instead of Chrome_WidgetWin_1
                        bool isVivaldiRenderHost = appSettings.sLastActiveAppName == "vivaldi.exe" && string.Compare(ClassName.ToString(), "Chrome_RenderWidgetHostHWND", true, CultureInfo.InvariantCulture) == 0;
                        if (string.Compare(ClassName.ToString(), "Chrome_WidgetWin_1", true, CultureInfo.InvariantCulture) == 0 || isVivaldiRenderHost)
                        {
                            object objChild = null;
                            IAccessible accWindow = null;
                            uint hr = AccessibleObjectFromEvent(hwnd, (uint)idObject, (uint)idChild, out accWindow, out objChild);
                            if (hr == 0 && accWindow != null)
                            {
                                if (eventType == 0x800E)
                                {
                                    string sName = accWindow.accName[objChild];

                                    string sURL = accWindow.accValue[objChild];
                                    if (sURL != null && sURL.Length > 0) //It's our chrome address bar value
                                    {
                                        Debug.WriteLine("accState = " + accWindow.accState[objChild].ToString());
                                        //1049600 = EDGE editing completed
                                        //1048576 = CHROME EDITING COMPLETED
                                        if (int.Parse(accWindow.accState[objChild].ToString()) == 1048576 || int.Parse(accWindow.accState[objChild].ToString()) == 1049600) //I just found this to be appropriate and not editable state
                                        {
                                            Debug.WriteLine("accName = " + sName);
                                            Debug.WriteLine("accValue = " + sURL);
                                            Debug.WriteLine("accChild = " + accWindow.accChild[objChild].ToString());
                                            Debug.WriteLine("accState = " + accWindow.accState[objChild].ToString());

                                            appSettings.FirefoxLastAccWindow = accWindow;
                                            appSettings.sLastActiveURLInFirefox = sURL;
                                            appSettings.objChild = objChild;

                                            ProcessBrowserURLs.ProcessCurrentURL(hwnd);
                                        }

                                    }
                                    else
                                    {
                                        appSettings.Hide_SetIntentionWindow();
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    appSettings.Hide_ShowIntentionWindow();
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception " + ex);
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);
        private const uint GA_ROOT = 2; // Get the root window in the hierarchy

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out IntPtr lpdwProcessId);


        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_CONTROL      = 0x11;
        private const byte VK_W            = 0x57;
        private const uint WM_CLOSE        = 0x0010;


        //Cache the last resolved foreground (PID -> exe name) so this high-frequency WinEvent hook
        //(EVENT_OBJECT_* 0x8005-0x800E, incl. location/state/value changes) doesn't re-resolve the name
        //on every event. Accessed only from the hook-owning UI thread (WINEVENT_OUTOFCONTEXT), so no lock.
        private static int _lastActivePid = 0;
        private static string _lastActiveAppName = string.Empty;

        private static string GetLastActiveAppName(IntPtr hwnd)
        {
            try
            {
                if (hwnd == IntPtr.Zero)
                {
                    //Event not tied to a window (e.g. a system object) - nothing to report
                    return string.Empty;
                }

                GetWindowThreadProcessId(hwnd, out IntPtr activeAppProcessId);
                int pid = (int)activeAppProcessId;
                if (pid == 0)
                {
                    return string.Empty;
                }

                //Common case: consecutive events come from the same foreground app - return the cached
                //name and skip the (relatively expensive) lookup below.
                if (pid == _lastActivePid)
                {
                    return _lastActiveAppName;
                }

                //No OpenProcess here on purpose. The old code called OpenProcess() and never closed the
                //handle it returned, leaking a process handle on every event until the OS ran out of
                //handles - the reported system-wide freeze / "not responding". Process.GetProcessById
                //hands back no such raw handle; the using block just disposes the managed Process wrapper.
                //(ProcessName enumerates processes internally, which is why the PID cache above matters.)
                using (Process process = Process.GetProcessById(pid))
                {
                    //Callers compare against "<name>.exe" (e.g. "chrome.exe", "Focus Bear.exe").
                    //ProcessName is the exe base name without its extension, so re-add ".exe".
                    _lastActiveAppName = process.ProcessName + ".exe";
                    _lastActivePid = pid;
                    return _lastActiveAppName;
                }
            }
            catch (Exception ex)
            {
                //Process may have exited between reading the PID and querying it - log, don't crash.
                LogWriter.LogWrite("GetLastActiveAppName ex - " + ex.Message);
            }

            return string.Empty;
        }


        DispatcherTimer dispatcherTimerBreaks = new DispatcherTimer(DispatcherPriority.Normal);
        DispatcherTimer dispatcherTimerBackground = new DispatcherTimer(DispatcherPriority.Normal);
        DispatcherTimer dispatcherTimerPostponeHabits = new DispatcherTimer(DispatcherPriority.Normal);
        DispatcherTimer dispatcherTimerResumeHabitsAfterPostpone = new DispatcherTimer(DispatcherPriority.Normal);
        DispatcherTimer dispatcherTimerProductivityStats = new DispatcherTimer(DispatcherPriority.Normal);

        public WinForms.NotifyIcon trayIcon = null;

        public bool isMorningHabitsAlreadyShown = false;

        ShutOffWindow wndShutOff = null;
        SplashWindow wndSplash = null;

        public FocusBearMainWindow wndFocusBearMainWindow = null;
        public PreferencesWindow wndPreferences = null;
        public TrayMenu wndTrayMenu = null;

        public FocusModeEnd wndFocusModeEnd = null;
        public FocusModeEnd wndFocusModeEndForced = null;

        public BreakWindow wndTakeABreak = null;

        public FocusActivitySummary wndFocusSummary = null;

        public EasySkipPostpone wndEasyPostpone = null;

        public BrainDumpWindow wndBrainDump = null;

        public FocusProgress wndFocusProgress = null;

        public HabitsResumeInPopup wndHabitsResumeInPopup = null;

        public RelaxModeWindow wndRelaxMode = null;

        public StartPomodoro wndStartPomodoro = null;

        public StartSimplePomodoro wndStartSimplePomodoro = null;

        public CallFocusNotifier wndCallFocusNotifier = null;

        public NaturalBreakDetectionWindow wndNaturalBreakDetectionWindow = null;

        bool bAppExtracted = false;
        public static event Action FocusModeAddedDeletedEvent;

        public static event Action ThemeChanged;


        void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            SentrySdk.ConfigureScope(scope =>
            {
                scope.User = new Sentry.User() { Id = appSettings.userAccount.userID };
            });

            SentrySdk.CaptureException(e.Exception);

            // If you want to avoid the application from crashing:
            e.Handled = true;
        }
        public App()
        {
            SentrySdk.Init(o =>
            {
                // Tells which project in Sentry to send events to:
                o.Dsn = "https://6f9de528660aea5d587013fec00ff5d7@o4509122919006208.ingest.us.sentry.io/4509123825696768";

                // When configuring for the first time, to see what the SDK is doing:
                o.Debug = true;

                // Set traces_sample_rate to 1.0 to capture 100% of transactions for performance monitoring.
                // We recommend adjusting this value in production.
                o.TracesSampleRate = 1.0;

                // Enable Global Mode since this is a client app
                o.IsGlobalModeEnabled = true;

            });

            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            LogWriter.LogWrite(string.Format("App Started - Version {0}", appSettings.AppVersion.ToString()));

            bool bStartingAfterReactivate = false;
            if (e.Args.Length > 0)
            {
                if (e.Args[0] == "/startupYes")
                {
                    SetAtWindowsStartup();
                    Application.Current.Shutdown(0);
                    return;
                }
                else if (e.Args[0] == "/restartbyself")
                {
                    Task.Delay(900);
                    bStartingAfterReactivate = true;
                }
                else if (e.Args[0] == "/afterdeactivate")
                {
                    Task.Delay(900);
                    appSettings.bStartingAfterDeactivated = true;
                }
            }

            if (!bStartingAfterReactivate || !appSettings.bStartingAfterDeactivated)
            {
                try
                {
                    #region Managing Instances
                    //Handling running instances
                    
                    appMutex = new Mutex(true, MutexGUID, out createdNew);
                    appWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, EventMutex);

                    GC.KeepAlive(this.appMutex);
                    if (!createdNew)
                    {
                        appWaitHandle.Set();
                        Application.Current.Shutdown(0);
                        return;
                    }
                    else
                    {
                        var thread = new Thread(() =>
                        {
                            while (this.appWaitHandle.WaitOne())
                            {
                                Current.Dispatcher.BeginInvoke((Action)(() =>
                                {
                                    if (Current.MainWindow is TrayMenu)
                                    {
                                        var alreadyRunningMainWin = (TrayMenu)Current.MainWindow;
                                        alreadyRunningMainWin.Topmost = true;
                                        alreadyRunningMainWin.UpdateTrayMenu();
                                        alreadyRunningMainWin.Show();
                                        alreadyRunningMainWin.BringIntoView();
                                        alreadyRunningMainWin.Activate();
                                        alreadyRunningMainWin.Focus();
                                        alreadyRunningMainWin.SetTrayMenuPosition();
                                    }
                                }));
                            }
                        });
                        thread.IsBackground = true;
                        thread.Start();
                    }


                    #endregion
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("isRunning check - " + ex.Message);
                }
            }


            SetAtWindowsStartup();

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            //Ensuring Autoplay video in webview
            //Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", "--autoplay-policy=no-user-gesture-required");
            // Enable autoplay in WebView2 and remote debugging when running automated tests
            var debugPort = Environment.GetEnvironmentVariable("FOCUSBEAR_DEBUG_PORT");
            var browserArgs = "--autoplay-policy=no-user-gesture-required";
            if (!string.IsNullOrEmpty(debugPort))
            {
                browserArgs += $" --remote-debugging-port={debugPort}";
            }
            Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", browserArgs);

            RegistryManager.GetLangCode();

            RegistryManager.GetThemeMode();

            RegistryManager.GetAutoThemeMode();

            RegistryManager.GetTrayModeChangeFB();

            appSettings.CheckAndGetGlobalUUID();

            // NOTE: tab-limiter settings are loaded/migrated post-login (after the per-user registry path and
            // the synced settings are available) — see ReadAndParseUserLocalDeviceSettingsJson + the post-login
            // LoadMaxTabLimitSetting call. Loading here (pre-login) would read the wrong per-user path.
            appSettings.LoadNotificationSoundsSetting();

            appSettings.DownloadAndLoadConfigFile(); // TODO: Only allow download in production mode by checking the current environment

            //System sleep / wake up event
            SystemEvents.PowerModeChanged += OnPowerChange;

            //Display settings change event
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            // When display settings change (resolution, multiple monitors, etc.)
            // Ensure all app windows are visible
            Focus_Bear.Utils.WindowHelper.EnsureAllVisible();
        }

        private void OnPowerChange(object s, PowerModeChangedEventArgs e)
        {
            switch (e.Mode)
            {
                case PowerModes.Resume:
                    LogWriter.LogWrite("PowerModes.Resume");
                    ResumeHabitsAfterSystemWakeup();
                    // Proactively refresh + push the access token when waking from sleep
                    // (handles >10h downtime and tokens that expired while suspended).
                    _ = WebViewTokenSyncService.Instance.OnSystemResumeAsync();
                    break;
                case PowerModes.Suspend:
                    LogWriter.LogWrite("PowerModes.Suspend");
                    PauseHabitsOnSystemSleep();
                    WebViewTokenSyncService.Instance.OnSystemSuspend();
                    break;
            }
        }

        public void SetAtWindowsStartup()
        {
            LogWriter.LogWrite("SetAtWindowsStartup()");

            try
            {
                AppTaskScheduler taskSchedule = new AppTaskScheduler();

                if (taskSchedule.CheckTaskExists("Focus Bear"))
                    return;

                if (WinSecurity.IsAdmin())
                {
                    LogWriter.LogWrite("User IS admin, setting in task scheduler for startup");
                    taskSchedule.CreateTask("Focus Bear", "");
                }
                else
                {
                    LogWriter.LogWrite("User not admin, setting in RUN for startup");
                    RegistryManager.AddToStartup();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetAtWindowsStartup() ex - " + ex.Message);
            }

        }

        public void ShowSplashWindow(bool bProgressIndicator = false)
        {
            LogWriter.LogWrite("Show SplashWindow()");
            if (bProgressIndicator) //need force creation of object in this case
            {
                if (wndSplash != null)
                {
                    wndSplash.Close();
                }
                wndSplash = null;
            }

            if (wndSplash == null)
            {
                wndSplash = new SplashWindow(bProgressIndicator);
                wndSplash.Show();
            }
        }
        public void ProceedAfterDownloadingConfigFile()
        {
            LogWriter.LogWrite("ProceedAfterDownloadingConfigFile()");

            ///
            //Show the splash window here
            if (!appSettings.bStartingAfterDeactivated)
            {
                ShowSplashWindow();
            }

            var status = appSettings.DownloadBlockHTMLFile();

            //App first install configuration
            
            if (!RegistryManager.GetFirstInstallDate())
            {
                LogWriter.LogWrite("IsFirstInstall = true;");

                //If first install DT is not available from registry then it will be considered that 
                appSettings.IsFirstInstall = true;
                appSettings.FirstAppInstallDT = DateTime.Now;
                RegistryManager.SetFirstInstallDate();
            }
            else
            {
                CheckForUpdate(false);
            }

            // Clear WebView2 cache if user is logged out, prevent user data from being mixed between different accounts.
            if (!RegistryManager.GetLoggedInStatus())
            {
                try
                {
                    string webViewCachePath = System.IO.Path.Combine(AppSettings.AppDataDirPath, "FBCustomWebView2");
                    if (System.IO.Directory.Exists(webViewCachePath))
                    {
                        System.IO.Directory.Delete(webViewCachePath, true);
                        LogWriter.LogWrite("Cleared FBCustomWebView2 cache directory - EDIT HABITS and other webviews");
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("Failed to clear FBCustomWebView2 cache: " + ex.Message);
                }
            }

            //Enbale url blocking we need this
            dele = new WinEventDelegate(WinEventProc);
            m_hhook = SetWinEventHook(0x8005, 0x800E, IntPtr.Zero, dele, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

            //Create the Main application Window: V3
            wndFocusBearMainWindow = new FocusBearMainWindow();
            
            if (!RegistryManager.GetWelcomeAboardingDoneStatus())
            {
                LogWriter.LogWrite("GetWelcomeAboardingDoneStatus NO");

                if (wndSplash != null)
                {
                    LogWriter.LogWrite("Hide SplashWindow() to show onboardiong flow");
                    wndSplash.Close();
                }
                
                //GEt the list of Installed app
                var engine = new AppsEngine();
                engine.GetData();
                bAppExtracted = true;

                ShowWelcomeAbordOnBoradingFlow();
            }
            else
            {
                LogWriter.LogWrite("GetWelcomeAboardingDoneStatus YES");
                appSettings.GetLoginAndTokenValidSettings();
            }


        }

        private WinForms.ContextMenuStrip CreateContextMenu()
        {
            var menu = new WinForms.ContextMenuStrip
            {
                Renderer = new WinForms.ToolStripProfessionalRenderer()
            };

            var exitItem = new WinForms.ToolStripMenuItem(appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["buttonQuitText"].ToString());
            exitItem.Name = "ExitItem";
            exitItem.Click += (s, e) =>
            {
                ButtonAutomationPeer peer = new ButtonAutomationPeer(wndTrayMenu.btnQuitIcon);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            };

            menu.Items.Add(exitItem);
            return menu;
        }

        public void InitializeTrayIcon()
        {
            LogWriter.LogWrite("InitializeTrayIcon()");
            try
            {
                if (trayIcon == null)
                {
                    trayIcon = new WinForms.NotifyIcon();
                    trayIcon.Text = "Focus Bear";
                    trayIcon.BalloonTipText = "Focus Bear";
                    trayIcon.Visible = true;
                    trayIcon.ContextMenuStrip = CreateContextMenu();

                    if (wndSplash != null)
                    {
                        wndSplash.Close();
                    }

                    if (appSettings.IsFirstInstall)
                    {
                        LogWriter.LogWrite("Show FocusBearMenuTour");
                        //Whether the user had opted to see the video or not, this tour will come here.
                        FocusBearMenuTour wndFocusBearMenuTour = new FocusBearMenuTour();
                        wndFocusBearMenuTour.ShowDialog();
                    }

                    if (!appSettings.bStartingAfterDeactivated)//do not show if starting after deactivated
                    {
                        new TrayBalloon(0, appSettings.ConfigFileStrings["WindowsSpecific"]["runningbg"].ToString()).Show();
                    }
                    using (Stream iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Focus Bear;component/Images/focus-bear.ico")).Stream)
                    {
                        trayIcon.Icon = new System.Drawing.Icon(iconStream);
                    }

                    trayIcon.MouseClick += TrayIcon_MouseClick;
                }

                if (wndTrayMenu == null)
                {
                    wndTrayMenu = new TrayMenu();
                    Application.Current.MainWindow = wndTrayMenu;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("InitializeTrayIcon() Exception" + ex.Message);
            }
        }

        private void TrayIcon_MouseClick(object sender, WinForms.MouseEventArgs e)
        {
            LogWriter.LogWrite("TrayIcon_MouseClick()");
            try
            {

                if (((App)(Application.Current)).wndTakeABreak != null && !(((App)(Application.Current)).wndTakeABreak.IsVisible))
                {
                    if (((App)(Application.Current)).wndTakeABreak.bShowingForNonTimerActivities)
                    {
                        ((App)(Application.Current)).wndTakeABreak.Show();
                    }
                }

                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    double x = WinForms.Control.MousePosition.X;
                    double y = WinForms.Control.MousePosition.Y;
                    if (wndTrayMenu == null)
                    {
                        wndTrayMenu = new TrayMenu();
                    }
                    wndTrayMenu.Topmost = true;
                    wndTrayMenu.UpdateTrayMenu();
                    wndTrayMenu.Show();
                    wndTrayMenu.BringIntoView();
                    wndTrayMenu.Activate();
                    wndTrayMenu.Focus();
                    wndTrayMenu.SetTrayMenuPosition();
                }

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TrayIcon_MouseClick" + ex.Message);

            }
        }

        public void StopBreakActivityTimer()
        {
            dispatcherTimerBreaks.Stop();
        }
        public void StartBreakActivityTimer(bool bUserPostponed = false, int nMinutesToPostpone = 0)
        {
            LogWriter.LogWrite("StartBreakActivityTimer() invoked");

            if (!appSettings.bAreBreaksEnabled)
            {
                dispatcherTimerBreaks.Stop(); //caution
                LogWriter.LogWrite("breaks not enabled so returning from StartBreakActivityTimer()");
                return;
            }

            if (appSettings.IsShutOffTime())
            {
                LogWriter.LogWrite("It Is IsShutOffTime() so returning from StartBreakActivityTimer()");
                return;
            }

            if (appSettings.arrAllbreakActivities.Count == 0)
            {
                LogWriter.LogWrite("No activities in break activities array so returning from StartBreakActivityTimer()");
                return;
            }
            if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
            {
                return;
            }
            //Remove the previous event handler so that new can be set
            try
            {
                dispatcherTimerBreaks.Stop();

                var eventField = dispatcherTimerBreaks.GetType().GetField("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
                var eventDelegate = (Delegate)eventField.GetValue(dispatcherTimerBreaks);
                if (eventDelegate != null)
                {
                    var invocatationList = eventDelegate.GetInvocationList();

                    foreach (var handler in invocatationList)
                        dispatcherTimerBreaks.Tick -= ((EventHandler)handler);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StartBreakActivityTimer" + ex.Message);
            }

            try
            {
                dispatcherTimerBreaks.Tick += new EventHandler(dispatcherTimer_Breaks);

                if (bUserPostponed) //settimer for 5 minutes
                {
                    LogWriter.LogWrite("User Postponed set StartBreakActivityTimer() for 5 minutes");
#if DEBUG
                    dispatcherTimerBreaks.Interval = new TimeSpan(0, 10, 15);
                    appSettings.NextBreakAt = DateTime.Now.AddMinutes(10); 
#else
                    dispatcherTimerBreaks.Interval = new TimeSpan(0, nMinutesToPostpone, 0);
                    appSettings.NextBreakAt = DateTime.Now.AddMinutes(nMinutesToPostpone);
#endif
                }
                else
                {
#if DEBUG
                    dispatcherTimerBreaks.Interval = new TimeSpan(0, 0, 10);
                    appSettings.NextBreakAt = DateTime.Now.AddMinutes(1);
#else
                    dispatcherTimerBreaks.Interval = new TimeSpan(0,  appSettings.breakAfterMinutes, 0);
                    appSettings.NextBreakAt = DateTime.Now.AddMinutes(appSettings.breakAfterMinutes);
#endif
                }

                dispatcherTimerBreaks.Start();
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
            }

        }

        private async void dispatcherTimer_Breaks(object sender, EventArgs e)
        {
            LogWriter.LogWrite("dispatcherTimer_Breaks()");

            dispatcherTimerBreaks.Stop(); //Tick Once only

            if(appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
            {
                LogWriter.LogWrite("dispatcherTimer_Breaks()  CurrentActivityType.BREAK_ACTIVITY - manual break already runing, return");
                return;
            }
            try
            {
                //Preferred approach is to detect keyboard / mouse activity and wait for 5 seconds of idle time.
                await IdleWaiter.WaitForIdleAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                // Log or handle errors 
                LogWriter.LogWrite("dispatcherTimer_Breaks() WaitForIdleAsync Error: " + ex.Message);
            }

            try
            {
                

                CommandManager.InvalidateRequerySuggested();

                if (appSettings.IsShutOffTime())
                {
                    LogWriter.LogWrite("dispatcherTimer_Breaks() It Is IsShutOffTime() so returning from dispatcherTimer_Breaks() after stopping timer");
                    return;
                }

                if (appSettings.nCurrentActivityType == CurrentActivityType.SHUT_OFF || appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    LogWriter.LogWrite("SHUT_OFF or EVENING_HABITS - Return from dispatcherTimer_Breaks()");
                    return;
                }
                //We should also check if the morning routine has not been show, should return from here - case found from logs of #256

                /**if (RegistryManager.GetMorningHabitsShownTillDate())
                {
                    if (appSettings.MorningHabitsShownTillDate < DateTime.Now)
                    {
                        LogWriter.LogWrite("MorningHabits Not Shown for this Date - Return from dispatcherTimer_Breaks()");
                        return;
                    }
                }**/

                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS && !appSettings.bHabitsPostponedState)
                {
                    LogWriter.LogWrite("MorningHabits ongoing, return");
                    return;
                }

                if (appSettings.bSkipMicroBreaksDuringFocus && (appSettings.isFocusModeRunning || appSettings.bUseSuperPomodoroMode))
                {
                    LogWriter.LogWrite("appSettings.isFocusModeRunning && appSettings.bSkipMicroBreaksDuringFocus - return");
                    //Start the break Activities Timer
                    StartBreakActivityTimer();
                    return;
                }

                if(appSettings.ActiveSchedule != null && appSettings.isBlockingScheduleRunning)
                {
                    if (!appSettings.ActiveSchedule.bMicroBreaksEnabled)
                    {
                        LogWriter.LogWrite("appSettings.ActiveSchedule.bMicroBreaksEnabled is false - return reset timer");
                        //Start the break Activities Timer
                        StartBreakActivityTimer();
                        return;
                    }
                }
                if (appSettings.IsAppDeactivated())
                {
                    LogWriter.LogWrite("appSettings.IsAppDeactivated() - return from breaks timer");
                    //Start the break Activities Timer
                    StartBreakActivityTimer();
                    return;
                }

                if (appSettings.bIsUserOnCall && !appSettings.bShowBreaksWhileInCall)
                {
                    LogWriter.LogWrite("User is on Call - bShowBreaksWhileInCall false, restart break activity timer");
                    return;
                }


                //Check here if the Breaks window is visible, we will hide it.
                if (((App)(Application.Current)).wndTakeABreak != null && ((App)(Application.Current)).wndTakeABreak.IsVisible)
                {
                    ((App)(Application.Current)).wndTakeABreak.Close();
                    ((App)(Application.Current)).wndTakeABreak = null;
                }
                LogWriter.LogWrite("Show Break Activities Warning");
                BreakWarning windBreakWarning = new BreakWarning();
                windBreakWarning.WindowStartupLocation = WindowStartupLocation.Manual;
                windBreakWarning.Topmost = true;
                windBreakWarning.Show();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("dispatcherTimer_Breaks Exception" + ex.Message);
            }
        }


        public async void ShowBreakActivities()
        {
            LogWriter.LogWrite("ShowBreakActivities()");

            if(wndNaturalBreakDetectionWindow != null)
            {
                wndNaturalBreakDetectionWindow.Hide();
            }

            if (appSettings.nCurrentActivityType == CurrentActivityType.SHUT_OFF  )
            {
                LogWriter.LogWrite("SHUT_OFF or EVENING_HABITS - Return from ShowBreakActivities()");
                return;
            }

            if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS &&
                (!appSettings.bHabitsPostponedState && !appSettings.bFocusSummaryPostponedState ))
            {
                LogWriter.LogWrite("SHUT_OFF or EVENING_HABITS - Return from ShowBreakActivities()");
                return;
            }

            if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS &&
                !appSettings.bHabitsPostponedState)
            {
                LogWriter.LogWrite("MORNING_HABITS - Return from ShowBreakActivities()");
                return;
            }

            wndFocusBearMainWindow.DisableAllButtons(LeftButtonType.MicroBreaks);//Disable the left buttons when break starts
            wndFocusBearMainWindow.CollapseSideBar();

            appSettings.nCurrentActivityType = CurrentActivityType.BREAK_ACTIVITY;
            LogWriter.LogWrite(" appSettings.nCurrentActivityType = CurrentActivityType.BREAK_ACTIVITY;");
            PauseFocusMusicForRoutine();
            bool bCanShowBreaks = appSettings.PrepareBreakActivities();
            if (!appSettings.isMandatoryActivityGoing) //There is no Includeinevery break activity
            {
                if (appSettings.arrActivitiesExcludeInEveryBreak.Count() > 0)
                {
                    bCanShowBreaks = true;
                }
                else
                {
                    bCanShowBreaks = false; //There is not Includeineverybreak activity as well as no excludeineverybreakactivity, do not show breaks
                }
            }
            if (bCanShowBreaks) //Then only call to show the break activities.
            {
                ////
                try
                {
                    //Here first we will check if we have some pending sequence or if some activities from morning routine are done.
                    //If some are done, we will continue from there..
                    //if morning routine for today is completed, we will start break activities timer
                    //if there is some pending sequnce from any previous days, we will force complete it.

                    FocusBearAPIClient client = new FocusBearAPIClient();

                    var sResponse = await client.GetLastActivityInfo();
                    int x = int.Parse(sResponse.ToString());
                    if (x == 1)
                    {
                        LogWriter.LogWrite("GetLastActivityInfo Success");

                        if (appSettings.sLastActivityDoneInfo != null || appSettings.sLastActivityDoneInfo.Length > 0)
                        {
                            //Parse the JSON string
                            try
                            {
                                JObject sLastActivityDone = JObject.Parse(appSettings.sLastActivityDoneInfo);
                                //first Detect if the user has changed their habits and pull the new habits from the backend
                                if (sLastActivityDone.ContainsKey("last_time_user_settings_modified"))
                                {
                                    string s_edited_settings_date = sLastActivityDone["last_time_user_settings_modified"].ToString();
                                    ValidateSettingsChange(s_edited_settings_date);

                                }

                                if (sLastActivityDone.ContainsKey("current_activity_sequence"))
                                {
                                    string currentActivitySequence = sLastActivityDone["current_activity_sequence"].ToString();

                                    if (currentActivitySequence.Length > 0)
                                    {
                                        string sequenceId = sLastActivityDone["current_activity_sequence"]["id"].ToString();
                                        LogWriter.LogWrite("Pending routine is not for use, clean all pending sequence before break activities");
                                        var sResponse1 = await client.ForceCompleteCurrentSequence(sequenceId);
                                        int x1 = int.Parse(sResponse.ToString());
                                        if (x1 == 1)
                                        {
                                            ////Start Default
                                        }

                                    }
                                    else
                                    {
                                        LogWriter.LogWrite("Start new break activities sequence:- current_activity_sequence is null");
                                    }
                                }
                                else
                                {
                                    LogWriter.LogWrite("Start new break activities sequence:- current_activity_sequence not found as dictionary");
                                    //Start Default
                                }
                                // 
                            }
                            catch (Exception ex)
                            {
                                LogWriter.LogWrite("GetLastActivityInfo JSON PARSING FAILED" + ex.Message);
                            }
                        }
                        else
                        {

                        }

                    }
                    else
                    {
                        LogWriter.LogWrite("GetLastActivityInfo Failed");
                        //We could not get last activity info so we will start the routine for sure
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("ShowBreakActivities()" + ex.Message);
                }

                wndFocusBearMainWindow.GridShade.Visibility = Visibility.Collapsed; //May be this was shown from the start break button on no habit status screen

                ShowBlankWindowOnOtherScreens();
                wndFocusBearMainWindow.PageHabits.InitializeInnerScreens(); //V2 most important to load all screens as required.

                ShowBreaksSectionInMainWindow();

                // Pedro: Update the tray menu for current activity
                if (appSettings.isFocusModePaused && appSettings.isFocusModeRunning)
                {
                    HideFocusUIForRoutine();
                }
            }
            else
            {
                appSettings.nCurrentActivityType = CurrentActivityType.NONE;

                RestoreFocusUIAfterRoutine();
            }
        }
        public void ShowWelcomeAbordOnBoradingFlow()
        {
            LogWriter.LogWrite("ShowWelcomeAbordOnBoradingFlow");

            appSettings.nCurrentActivityType = CurrentActivityType.ONBOARDING; //Set the state
            
            BringMainWindowToFront();//v3

            wndFocusBearMainWindow.ShowOnboardingViewController();

            //OnboardingViewController PageOnboarding = new OnboardingViewController();

            //NewOnboardingStart windWelcome = new NewOnboardingStart();
            //windWelcome.Topmost = true;
            //windWelcome.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            //windWelcome.Show();
            //windWelcome.Activate();
            //windWelcome.Focus();



            appSettings.CapturePostHog("welcome-aboard-shown");

        }

        public void ShowLoginWindow()
        {
            Task.Delay(3000); //Let the onboarding window be closed

            LogWriter.LogWrite("ShowLogin-MAIN-Window");

            if (wndSplash != null)
            {
                LogWriter.LogWrite("Hide SplashWindow() to ShowLoginWindow");
                wndSplash.Close();
            }


            BringMainWindowToFront();//v3
            wndFocusBearMainWindow.ShowLoginViewController();

            wndFocusBearMainWindow.DisableAllButtons(LeftButtonType.None);
        }

        public void ShowOnBoradingVideo()
        {
            LogWriter.LogWrite("ShowOnBoradingVideo");

            OnboardingVideo windOnBoardingVid = new OnboardingVideo();

            windOnBoardingVid.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            windOnBoardingVid.Topmost = true;
            windOnBoardingVid.ShowDialog();
            windOnBoardingVid.Activate();
            windOnBoardingVid.Focus();
        }
        public void ProceedAfterGetLoginAndTokenValidSettings()
        {
            LogWriter.LogWrite("ProceedAfterGetLoginAndTokenValidSettings");

            //Here we will connect the Mac ID and the Global GUID with the Distinct User ID
            ConnectGlobalAndMacIDWithDistinctUserID();

            // Start webview token sync (periodic check + startup proactive refresh, downtime tracking).
            // Proactive (Path C) enumerates the live dashboard webviews through this provider.
            WebViewTokenSyncService.Instance.AliveWebViewProvider = GetAliveTokenWebViews;
            WebViewTokenSyncService.Instance.Start();

            //Proceed Normal to show routines
            ProceedAfterLogin();
        }

        /// <summary>
        /// Enumerates the currently-open dashboard webviews for proactive (Path C) token pushes.
        /// Reached directly from the FocusBearMainWindow page fields; only webviews whose
        /// CoreWebView2 is initialised are returned. Must run on the UI thread (touches CoreWebView2).
        /// </summary>
        private System.Collections.Generic.List<Microsoft.Web.WebView2.Wpf.WebView2> GetAliveTokenWebViews()
        {
            var list = new System.Collections.Generic.List<Microsoft.Web.WebView2.Wpf.WebView2>();
            try
            {
                var w = wndFocusBearMainWindow;
                if (w == null) return list;

                AddIfAlive(list, w.PageEditHabits != null ? w.PageEditHabits.webView : null);
                AddIfAlive(list, w.PageStats != null ? w.PageStats.webView : null);
                AddIfAlive(list, w.PageToDoList != null ? w.PageToDoList.webView : null);
                AddIfAlive(list, w.PageToDoPlayer != null ? w.PageToDoPlayer.webView : null);
                if (w.PageHelp != null)
                {
                    AddIfAlive(list, w.PageHelp.webViewGetSupport);
                    AddIfAlive(list, w.PageHelp.webViewTutorials);
                }
                if (w.PageHabits != null)
                {
                    AddIfAlive(list, w.PageHabits.PageHabitExtraPersonal != null ? w.PageHabits.PageHabitExtraPersonal.webView : null);
                    AddIfAlive(list, w.PageHabits.PageWelcomeList != null ? w.PageHabits.PageWelcomeList.webView : null);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetAliveTokenWebViews ex - " + ex.Message);
            }
            return list;
        }

        private static void AddIfAlive(System.Collections.Generic.List<Microsoft.Web.WebView2.Wpf.WebView2> list, Microsoft.Web.WebView2.Wpf.WebView2 wv)
        {
            try
            {
                if (wv != null && wv.CoreWebView2 != null)
                {
                    list.Add(wv);
                }
            }
            catch { }
        }
        public void ProceedAfterOnboardingDonePostLogin()
        {
            LogWriter.LogWrite("ProceedAfterOnboardingDonePostLogin ()");
            RegistryManager.SetOnboardingAfterLoginDoneStatus();

            ExecutePostLoginTasks();
        }
        public async void ProceedAfterLogin()
        {
            LogWriter.LogWrite("ProceedAfterLogin");
            
            RegistryManager.SetWelcomeAboardingDoneStatus();//Update onboarding before login status now

            RegistryManager.GetApplicationFont();

            //We also need to get the user settings to chck if the user was an old other device user and do nto show the distracting site screen
            LogWriter.LogWrite("attempt to Get User Local Device Settings");
            FocusBearAPIClient client = new FocusBearAPIClient();
            var userLocalSettingsResponse = await client.GetUserLocalDeviceSettingsInfo();
            int m = int.Parse(userLocalSettingsResponse.ToString());
            if (m == 1)
            {
                LogWriter.LogWrite("GetUserLocalDeviceSettings Success");
                //User Local Device retrieved Successfully, Process them.
                appSettings.ReadAndParseUserLocalDeviceSettingsJson();
            }
            else
            {
                LogWriter.LogWrite("GetUserLocalDeviceSettings Failed");
                appSettings.ReadAndParseUserLocalDeviceSettingsJson();
            }

            // If an existing user's tab-limiter setting was just migrated from their local count, push it so
            // the migrated on/off + count become the synced truth (and aren't re-clobbered next launch).
            if (appSettings.bTabLimiterMigrationNeedsPush)
            {
                appSettings.bTabLimiterMigrationNeedsPush = false;
                LocalSettingsManager.PutLocalSettings();
            }

            if (!RegistryManager.GetOnboardingAfterLoginDoneStatus())
            {
                //Check if the user is an old user and settings already exists, then do not take to onboarding
                if (!appSettings.CheckIfLocalDeviceSettingsJsonExistsForOldUser())
                {
                    if (wndSplash != null)
                    {
                        LogWriter.LogWrite("hide SplashWindow() show Onboarding case");
                        wndSplash.Close();
                    }

                    //Save the default settings
                    //Save the local settings and upload to server
                    LocalSettingsManager.PutLocalSettings();

                    ShowWelcomeAbordOnBoradingFlow();//Will show the onboarding view in right content

                    wndFocusBearMainWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._HOLD_ON_);
                    return;
                }
                else
                {
                    appSettings.nCurrentActivityType = CurrentActivityType.NONE; //Set state to none now.
                    RegistryManager.SetOnboardingVideoShownStatus();//User alredy had done onboarding somewhere as he has settings set
                }

            }
            else
            {
                appSettings.nCurrentActivityType = CurrentActivityType.NONE; //Set state to none now.
            }

            ExecutePostLoginTasks();
        }

        public async void ExecutePostLoginTasks()
        {
            wndFocusBearMainWindow.EnableAllButtons();
            wndFocusBearMainWindow.Hide(); //Hiding once as it will be shown again
            ShowSplashWindow(true);

            if (!RegistryManager.GetOnboardingVideoShownStatus())
            {
                FocusBearAPIClient client = new FocusBearAPIClient();
                var userConsentTermsResponseUnused = await client.RecordUserConsent("terms_of_service");

                var userConsentPPResponseUnused = await client.RecordUserConsent("privacy_policy");

                var SyncPlatformCoursesResponseUnused = await client.SyncPlatformCourses();

                var EnrolForCoursesUnused = await client.EnrollForCourses(); //Enroll for courses 

                //We are setting evening routine shown here for thenext 2 hours so that evening routine willnot come for the next 2 hours the video is shown
                appSettings.ShutOFFAbortedTillDate = DateTime.Now.AddHours(2);

                RegistryManager.SetShutOffAbortedTillDate();

                //Additional safety not to show morning routine
                //No need to show the Morning Habits
                //WE will set the morning habits shown date for today
                RegistryManager.SetMorningHabitsShownTillDate();


                RegistryManager.SetOnboardingVideoShownStatus();

                if (appSettings.bShowOnBoardingVideo) //If user had agreed to see the video
                    ShowOnBoradingVideo();//Show Onboarding Video

                appSettings.CapturePostHog("menu_tour_setup_complete");
            }

            //Check if we have a Devie ID Set, Otherwise generate one and set it
            if (RegistryManager.GetDeviceID())
            {
                LogWriter.LogWrite("GetDeviceID Success");
                //We have a device ID already, so proceed with next.
                ProeedAfterGettingDeviceID();
            }
            else
            {
                LogWriter.LogWrite("GetDeviceID NO Success - Call the API to REgister Device");
                //Call the API to REgister Device
                FocusBearAPIClient client = new FocusBearAPIClient();

                var authenticationResponse = await client.PostDeviceID();
                int x = int.Parse(authenticationResponse.ToString()); ;

                if (x == 0)
                {
                    LogWriter.LogWrite("Device ID could not be set/get Show login window");
                    //Device ID could not be set/get Show login window
                    ShowLoginWindow();
                }
                else
                {
                    LogWriter.LogWrite("GetDeviceID Success");
                    ProeedAfterGettingDeviceID();
                }

            }
        }
        public async void ProeedAfterGettingDeviceID()
        {
            LogWriter.LogWrite("ProeedAfterGettingDeviceID");

            //Get User Settings
            FocusBearAPIClient client = new FocusBearAPIClient();

            await Task.Run(async () =>
            {
                CommandManager.InvalidateRequerySuggested();
                LogWriter.LogWrite("attempt to Get User DETAILS");
                var userDetailsResponse = await client.GetUserDetailsInfo();
                int y = int.Parse(userDetailsResponse.ToString());
                if (y == 1)
                {
                    LogWriter.LogWrite("GetUserDetailsInfo Success");
                    //User Settings retrieved Successfully, Process them.
                    appSettings.ReadAndParseUserDetailsJson();
                }
                else
                {
                    LogWriter.LogWrite("GetUserDetailsInfo Failed");
                    appSettings.ReadAndParseUserDetailsJson();
                }
                CommandManager.InvalidateRequerySuggested();
            });
            
            // Teacher Mode (issue #1343): detect whether this user is a teacher (administers a class)
            // so the Focus button starts a class focus session instead of a personal one. Non-teachers
            // are unaffected (the call sets bIsTeacher = false).
            await Task.Run(async () =>
            {
                try
                {
                    await client.GetClassManagementClasses();
                }
                catch (Exception exTeacher)
                {
                    LogWriter.LogWrite("GetClassManagementClasses startup ex - " + exTeacher.Message);
                }
            });

            await Task.Run(async () =>
            {
                CommandManager.InvalidateRequerySuggested();
                LogWriter.LogWrite("attempt to Get Focus Modes");
                var focusModesResponse = await client.GetFocusModes();
                int y = int.Parse(focusModesResponse.ToString());
                if (y == 1)
                {
                    LogWriter.LogWrite("GetFocusModes Success");
                    //Focus Modes retrieved Successfully, Process them.
                    appSettings.ReadAndParseFocusModes();
                }
                else
                {
                    LogWriter.LogWrite("GetFocusModes Failed");
                    appSettings.ReadAndParseFocusModes();
                }
                CommandManager.InvalidateRequerySuggested();
            });


            if (appSettings.isExpired)
            {
                LogWriter.LogWrite("isExpired TRUE");

                if (wndSplash != null)
                {
                    LogWriter.LogWrite("Show SplashWindow() EXPIRED case");
                    wndSplash.Close();
                }

                //WE need to make sure that the app is running in the SIMPLE MODE and NOT THE GEEK MODE
                appSettings.bIsGeekModeEnabled = false;
                ShowUpgradeToProNag("");
            }
            //Get local settings

            //Get Focus settings aka user details

            new Thread(new ThreadStart(new Action(async () =>
            {
                CommandManager.InvalidateRequerySuggested();

                //Write the Default CSV file with headers
                appSettings.WriteDefaultCSVFile();

                //Write the Offline data CSV file with headers
                appSettings.WriteDefaultOffilineCSVFile();

                CommandManager.InvalidateRequerySuggested();
            }))).Start();

            //IF its the onboarding time, we have already extracted thelist above
            if (!bAppExtracted)
            {
                //GEt the list of Installed app
                var engine = new AppsEngine(); 
                engine.GetData();
            }


            await Task.Run(async () =>
            {
                CommandManager.InvalidateRequerySuggested();

                LogWriter.LogWrite("attempt to GetUserSettings");
                var userSettingsResponse = await client.GetUserSettings();
                int x = int.Parse(userSettingsResponse.ToString());
                if (x == 1)
                {
                    LogWriter.LogWrite("GetUserSettings Success");
                    //User SEttings retrieved Successfully, Process them.
                    appSettings.ReadAndParseSettingsJson();
                }
                else
                {
                    LogWriter.LogWrite("GetUserSettings Failed");
                    appSettings.ReadAndParseSettingsJson(); //This function handles if the settings are not derieved from Server
                }

                CommandManager.InvalidateRequerySuggested();

                
                //IF its the onboarding time, we have already extracted thelist above
                if (!bAppExtracted)
                {
                    LogWriter.LogWrite("attempt to Get User Local Device Settings");
                    var userLocalSettingsResponse = await client.GetUserLocalDeviceSettingsInfo();
                    int m = int.Parse(userLocalSettingsResponse.ToString());
                    if (m == 1)
                    {
                        LogWriter.LogWrite("GetUserLocalDeviceSettings Success");
                        //User Local Device retrieved Successfully, Process them.
                        appSettings.ReadAndParseUserLocalDeviceSettingsJson();
                    }
                    else
                    {
                        LogWriter.LogWrite("GetUserLocalDeviceSettings Failed");
                        appSettings.ReadAndParseUserLocalDeviceSettingsJson();
                    }
                    CommandManager.InvalidateRequerySuggested();
                }

                LogWriter.LogWrite("attempt to Get all blocking schedules");
                var blockingSchedulesResponse = await client.GetAllBlockingSchedules();
                int mRes = int.Parse(blockingSchedulesResponse.ToString());
                if (mRes == 1)
                {
                    LogWriter.LogWrite("GetAllBlockingSchedules Success");
                    //User SEttings retrieved Successfully, Process them.
                    BlockingScheduleManager.ReadAndParseBlockingSchedules();
                }
                else
                {
                    LogWriter.LogWrite("GetAllBlockingSchedules Failed");
                    BlockingScheduleManager.ReadAndParseBlockingSchedules(); 
                }


                CommandManager.InvalidateRequerySuggested();

                LogWriter.LogWrite("attempt to Get all Trackss");
                var TracksResponse = await client.GetAllTracks();
                int mTrack = int.Parse(TracksResponse.ToString());
                if (mTrack == 1)
                {
                    LogWriter.LogWrite("GetAllTracks Success");
                    //focus music trcks retrieved Successfully, Process them.
                    FocusMusicManager.ReadAndParseFocusMusicTracks();
                }
                else
                {
                    LogWriter.LogWrite("GetAllTracks Failed");
                    FocusMusicManager.ReadAndParseFocusMusicTracks();
                }
                
                LogWriter.LogWrite("attempt to Get all buddies");
                var buddyResponse = await client.GetAllBuddies();
                int mBuddy = int.Parse(buddyResponse.ToString());
                if (mBuddy == 1)
                {
                    LogWriter.LogWrite("GetAllBuddies Success");
                    //buddy list retrieved Successfully, Process them.
                    AccountabilityBuddyManager.ReadAndParseAccountabilityBuddyList();
                }
                else
                {
                    LogWriter.LogWrite("GetAllBuddies Failed");
                    AccountabilityBuddyManager.ReadAndParseAccountabilityBuddyList();
                }
            });


            LogWriter.LogWrite("Attempt to Start the handler for Blocking Global URLS / Apps etc.");
            //Start the handler for Blocking Global URLS / Apps etc.


            appSettings.automation = new CUIAutomation();
            appSettings.handler = new FocusChangeHandler();
            appSettings.automation.AddFocusChangedEventHandler(null, appSettings.handler);
            LogWriter.LogWrite("Handler for Blocking Global URLS / Apps etc. STARTED");

            await Task.Run(async () =>
            {
                //One the USer details have been parsed and focus mode names have been fetched, we will check for default focus modes, if not present we will create them
                appSettings.ReadAndCreateDefaultFocusModes();

                
                if (!RegistryManager.GetFocusModeSetupDone())
                {
                    if (appSettings.arrFocusModes.Count <= 5)
                    {
                        //Get focus mode templates

                        var Response = await client.GetFocusTemplates();
                        int n = int.Parse(Response.ToString());
                        if (n == 1)
                        {
                            LogWriter.LogWrite("GetFocusTemplates Success");
                        }
                        else
                        {
                            LogWriter.LogWrite("GetFocusTemplates Failed");
                        }

                    }
                }
            });


            if (wndSplash != null)
            {
                LogWriter.LogWrite("Hide SplashWindow()");
                wndSplash.Close();
            }

            //Initialize the Tray Icon
            InitializeTrayIcon();

            // Reload per-user settings now that we're logged in. These are read once in the App ctor too,
            // but that runs before userAccount.userID is set, so the per-user registry path is wrong there
            // and the values come back as defaults (tab limit 0, etc.). Re-read them here — before
            // StartWorkFlow starts the tab-limit timer and before any browser-focus trigger fires.
            appSettings.LoadMaxTabLimitSetting();
            appSettings.LoadNotificationSoundsSetting();

            StartWorkFlow();
        }

        // Teacher Mode (issue #1343): true when pressing Focus should start a class (teacher) session
        // rather than a personal one — i.e. the user is a teacher, hasn't ticked "personal session
        // instead", and we know which class to start for.
        public bool IsTeacherClassSessionStart()
        {
            return appSettings.bIsTeacher
                && !appSettings.bTeacherStartPersonalSession
                && !string.IsNullOrWhiteSpace(appSettings.sTeacherClassTeamId);
        }

        // Start (and AWAIT) the class focus session for the given mode's allow-list. Returns 1 on success,
        // 0 on failure (appSettings.sLastClassSessionError holds the reason). The Focus button awaits this
        // BEFORE starting the local session so it can abort + tell the teacher when the backend refuses
        // (outside allowed hours, class already live, etc.) instead of silently starting focus anyway.
        public async Task<int> StartClassSessionForMode(FocusMode mode)
        {
            LogWriter.LogWrite("StartClassSessionForMode() - class session for " + appSettings.sTeacherClassName);

            // Send both the display name and the executable name(s) - lowercased and without ".exe" -
            // so cross-platform enforcement (which matches on process/exe name) recognises apps like
            // "chrome" whether the teacher typed "chrome" or picked "Google Chrome" (chrome.exe).
            HashSet<string> appSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (mode?.arrAllowedAppsWin != null)
            {
                foreach (var a in mode.arrAllowedAppsWin)
                {
                    if (a == null) continue;
                    if (!string.IsNullOrWhiteSpace(a.AppName))
                    {
                        appSet.Add(a.AppName.Trim());
                        // Map common typed/display names (e.g. "google chrome", "word", "teams")
                        // to their real Windows process name (chrome / winword / ms-teams).
                        foreach (string mapped in CommonAppMap.Resolve(a.AppName)) appSet.Add(mapped);
                    }
                    if (a.ExeNames != null)
                        foreach (var exe in a.ExeNames)
                            if (!string.IsNullOrWhiteSpace(exe))
                            {
                                appSet.Add(exe.Trim().ToLower().Replace(".exe", ""));
                                foreach (string mapped in CommonAppMap.Resolve(exe)) appSet.Add(mapped);
                            }
                }
            }
            List<string> allowedApps = new List<string>(appSet);

            List<string> allowedUrls = new List<string>();
            if (mode?.arrAllowedUrls != null)
                foreach (var u in mode.arrAllowedUrls)
                    if (!string.IsNullOrWhiteSpace(u)) allowedUrls.Add(u);

            return await new FocusBearAPIClient().StartClassSession(allowedApps, allowedUrls, mode?.name);
        }

        // Register the personal backend focus mode. A teacher's class session is started up-front by
        // StartClassSessionForMode, so this no-ops for that path and only runs for non-teachers / teachers
        // who chose a personal session.
        public void StartFocusSessionBackend(FocusMode mode)
        {
            if (IsTeacherClassSessionStart()) return;
            _ = new FocusBearAPIClient().StartFocusMode(mode.id);
        }

        public async void StartWorkFlow()
        {
            LogWriter.LogWrite("StartWorkFlow()");

            try
            {
                PusherHandler pusher = new PusherHandler();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PusherHandler() Initialization EX =" + ex.Message);
            }

            //Teacher Mode - adopt a live class session on launch (GET /class-session/current):
            //if the student already joined, rebuild the enforced allow-list session and resume
            //heartbeats; if there is a pending invitation, show the join prompt.
            TeacherSessionManager.Instance.AdoptCurrentSessionOnLaunch();

            //Connect to the Focus Button up-front, before any of the conditional workflow branches
            //below (postponed habits, shut-off, etc.) which can return early. Connecting here ensures
            //the serial port is always opened on startup, so focus sessions started remotely (from the
            //phone via Pusher) or manually always reach the device. Previously this lived at the very
            //end of the method and was skipped on every early-return path, leaving the button silent.
            FocusButton.Start();


#if DEBUG
            //Write any routines or functionality to test in DEBUG mode here
            //wndFocusBearMainWindow = new FocusBearMainWindow();
            //wndFocusBearMainWindow.ShowDialog();

#endif

            if (appSettings.IsFirstInstall)
            {
                LogWriter.LogWrite("appSettings.IsFirstInstall true");
                //No need to show the Morning Habits
                //WE will set the morning habits shown date for today
                RegistryManager.SetMorningHabitsShownTillDate();
            }

            //Right after the procastinating screen have been shown and the tray menu window have been created, we will show the start simple pomodoro window
            if (!RegistryManager.GetSimplePomodoroTourShownStatus()) //can use this to detect the first pomodoro session
            {
                ((App)(Application.Current)).ShowSimplePomodoroWindow();
            }

            //Do clear the Postponed variables
            RegistryManager.RemovePostponedHabitData();

            //Check if a focus mode is still in progress on the backend, resume it.
            //Awaited so the resume sets isFocusModeRunning/isOn BEFORE the routine branching below,
            //letting ShowMorningRoutine/ShowShutOffView's force-stop reliably show the focus-end window.
            await CheckAndResumeRunningFocusMode();

            //Restore a habit postpone persisted in a previous session (survives an app restart). Honour it
            //only while (a) its own end time hasn't passed, (b) we're still inside the routine window (the
            //guard: morning → today's shut-off, evening → next morning start), and (c) the routine isn't
            //already finished — e.g. completed on another device (RoutineHasRemainingHabits). Otherwise clear.
            if (RegistryManager.GetHabitsPostponeEndTime())
            {
                DateTime guard = HabitsPostponeGuardTime(appSettings.HabitsPostponeActivityType);
                bool bWithinWindow = DateTime.Now < appSettings.HabitsPostponeEndTime && DateTime.Now < guard;

                //Fetch the routine's completed/skipped activities (ShowMorningRoutine/ShowEveningRoutine, which
                //normally does this, is bypassed here) so the remaining-habits guard and the Day Plan reflect
                //what's actually done — including habits finished on another device while this app was closed.
                bool bRoutinePending = false;
                if (bWithinWindow)
                {
                    await PrepareRoutineDataForPostponeRestore(appSettings.HabitsPostponeActivityType);
                    bRoutinePending = !RoutineAlreadyCompletedToday(appSettings.HabitsPostponeActivityType)
                                      && RoutineHasRemainingHabits(appSettings.HabitsPostponeActivityType);
                }

                if (bWithinWindow && bRoutinePending)
                {
                    LogWriter.LogWrite("StartWorkFlow() - restoring persisted postpone until " + appSettings.HabitsPostponeEndTime + " (guard " + guard + ", focusSummary " + appSettings.bHabitsPostponeWasFocusSummary + ")");
                    appSettings.nCurrentActivityType = appSettings.HabitsPostponeActivityType;
                    TimeSpan postponeRemaining = appSettings.HabitsPostponeEndTime - DateTime.Now;

                    //A focus-summary postpone (shown at the start of the evening routine) resumes the summary,
                    //not the habits — restore its own flag + timer so expiry re-shows the summary, not habits.
                    if (appSettings.bHabitsPostponeWasFocusSummary)
                    {
                        appSettings.bFocusSummaryPostponedState = true;
                        wndFocusBearMainWindow.PageFocusSummary.RestorePostponeTimer(postponeRemaining);
                    }
                    else
                    {
                        appSettings.bHabitsPostponedState = true;
                        RestoreHabitsPostponeTimer(postponeRemaining);
                    }

                    wndFocusBearMainWindow.EnableAllButtons();
                    if (appSettings.bIsGeekModeEnabled)
                        ShowPomodoroWindow();
                    else
                        wndFocusBearMainWindow.PageHabits.ShowHabitsPostponedView();

                    try
                    {
                        dispatcherTimerBackground.Tick += new EventHandler(dispatcherTimer_Background);
                        dispatcherTimerBackground.Interval = new TimeSpan(0, 0, 5);
                        dispatcherTimerBackground.Start();
                        CommandManager.InvalidateRequerySuggested();
                    }
                    catch (Exception ex) { LogWriter.LogWrite("StartWorkFlow() - restore postpone bg timer EX - " + ex.Message); }

                    return;
                }
                else
                {
                    LogWriter.LogWrite("StartWorkFlow() - persisted postpone stale (withinWindow=" + bWithinWindow + " pending=" + bRoutinePending + "), clearing");
                    RegistryManager.RemoveHabitsPostponeEndTime();
                    appSettings.bHabitsPostponedState = false;
                    appSettings.bFocusSummaryPostponedState = false;
                }
            }

            //Check if its shut off time, show evening routine
            //If morning time show morning routine
            //if not both show break activties
            if (RegistryManager.GetMorningHabitsShownTillDate())
            {
                if (DateTime.Now < appSettings.MorningHabitsShownTillDate)
                {
                    isMorningHabitsAlreadyShown = true;
                    LogWriter.LogWrite("isMorningHabitsAlreadyShown = true");
                }
                else
                {
                    isMorningHabitsAlreadyShown = false;
                    LogWriter.LogWrite("isMorningHabitsAlreadyShown = false");
                }
            }
            else
            {
                isMorningHabitsAlreadyShown = false;
                LogWriter.LogWrite("isMorningHabitsAlreadyShown = false");
            }

            //Check if its shutoff time
            if (appSettings.IsShutOffTime())
            {
                LogWriter.LogWrite("IsShutOffTime = true");

                //Check if shutoff time is aborted, if not show the shutoff screen
                if (RegistryManager.GetShutOffAbortedTillDate())
                {
                    if (DateTime.Now > appSettings.ShutOFFAbortedTillDate) //Looks like a previous day aborted value
                    {
                        LogWriter.LogWrite("ShutOFFAbortedTillDate = false - Remove the key for ShutOFFAbortedTillDate");
                        //Remove the key for ShutOFFAbortedTillDate
                        RegistryManager.RemoveShutOffAbortedTillDate();
                    }
                    else
                    {
                        LogWriter.LogWrite("ShutOFFAbortedTillDate = true");
                        //Nothing to do as shutoff is aborted for the day

                        //Start the background timer
                        try
                        {
                            dispatcherTimerBackground.Tick += new EventHandler(dispatcherTimer_Background);
                            dispatcherTimerBackground.Interval = new TimeSpan(0, 0, 5);

                            dispatcherTimerBackground.Start();
                            CommandManager.InvalidateRequerySuggested();
                        }
                        catch (Exception ex)
                        {
                            LogWriter.LogWrite("StartWorkFlow()  ShutOffAbortedTillDate" + ex.Message);
                        }

                        return;
                    }
                }

                //Check if we have already show the evening habits, we need to show the Shut OFf screen
                if (RegistryManager.GetEveningHabitsShownTillDate())
                {
                    LogWriter.LogWrite("appSettings.EveningHabitsShownTillDate - " + appSettings.EveningHabitsShownTillDate.ToString());

                    if (DateTime.Now > appSettings.EveningHabitsShownTillDate) //Looks like a previous day Evening shown value
                    {
                        LogWriter.LogWrite("dispatcherTimer_Background() -  DateTime.Now > appSettings.EveningHabitsShownTillDate true ShowEveningRoutine();");
                        ShowEveningRoutine();
                    }
                    else
                    {
                        LogWriter.LogWrite("dispatcherTimer_Background() -  DateTime.Now > appSettings.EveningHabitsShownTillDate false ShowShutOffScreen(); ");
                        ShowShutOffScreen();
                    }
                }
                else
                {
                    LogWriter.LogWrite("GetEveningHabitsShownTillDate = false - ShowEveningRoutine");
                    ShowEveningRoutine();
                }


            }
            else
            {
                if (!appSettings.IsAppDeactivated())
                {
                    if (isMorningHabitsAlreadyShown)
                    {
                        //Start the break Activities Timer
                        StartBreakActivityTimer();
                    }
                    else
                    {
                        LogWriter.LogWrite("StartWorkFlow() -  ShowMorningRoutine()");
                        ShowMorningRoutine();
                    }
                }
            }
            //Start the background timer
            try
            {
                dispatcherTimerBackground.Tick += new EventHandler(dispatcherTimer_Background);
                dispatcherTimerBackground.Interval = new TimeSpan(0, 0, 5);

                dispatcherTimerBackground.Start();
                CommandManager.InvalidateRequerySuggested();

                dispatcherTimerProductivityStats.Tick += new EventHandler(dispatcherTimer_ProductivityStats);
                dispatcherTimerProductivityStats.Interval = new TimeSpan(0, 0, 3); //every three seconds

                dispatcherTimerProductivityStats.Start();
                CommandManager.InvalidateRequerySuggested();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StartWorkFlow()" + ex.Message);
            }

            appSettings.CheckLogFileRotation();

            //Get total time for distracting sites for today
            appSettings.GetTotalTimeForDistractingSitesToday();

            //Note: FocusButton.Start() is intentionally called near the top of this method now,
            //so it runs on every startup regardless of which workflow branch returns early.
        }
        private void dispatcherTimer_ProductivityStats(object sender, EventArgs e)
        {
            try
            {
                if (appSettings.isFocusModeRunning)
                {
                    TabMetrics.Instance.AddEntryInProductivityStatsCsv();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("dispatcherTimer_ProductivityStats Exception - " + ex.Message);
            }

            // Tab limiter fallback check — runs every 3s in case tabs were opened without browser focus
            try
            {
                CheckTabLimitAndAlert();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("CheckTabLimitAndAlert Exception - " + ex.Message);
            }
        }
        private void dispatcherTimer_Background(object sender, EventArgs e)
        {
           

            try
            {
                CommandManager.InvalidateRequerySuggested();

                if (appSettings.IsAppDeactivated())
                {
                    //Calculate total time while app is deactivated
                    appSettings.nTimeSpentBlockingDeactivated += 5;
                }
                else
                {
                    //Calculate total time while app is activated
                    appSettings.nTimeSpentBlockingActivated += 5;
                }


                if (appSettings.bEnableMeetingsDetection)
                {
                    if (AudioUtilities.IsUserOnaAudioCall() || CameraUtilities.IsUserOnVideoCall())
                    {
                        if (!bUserManuallyClosedNotifierForThisCallSession)
                            ShowCallFocusNotifier();
                        appSettings.bIsUserOnCall = true;
                    }
                    else
                    {
                        bUserManuallyClosedNotifierForThisCallSession = false;
                        appSettings.bIsUserOnCall = false;
                        HideCallFocusNotifier();
                    }
                }
                else
                {
                    appSettings.bIsUserOnCall = false;
                }

                OfflineActivities.ProcessOfflineData();

                if (ScreenSaverHandler.IsScreenSaverActive)//If screen saver is enabled
                {
                    Console.WriteLine("IsScreenSaverActive");
                    if (ScreenSaverHandler.IsScreenSaverRunning)
                    {
                        Console.WriteLine("IsScreenSaverRunning");
                        if (!bScreenSaverStarted) //so that its invoked only once
                            PauseHabitsOnSystemSleep();
                        bScreenSaverStarted = true;
                        //there is nothing to do, so we can return from here
                        return;
                    }
                    else
                    {
                        Console.WriteLine("FALSE IsScreenSaverRunning");
                        if (bScreenSaverStarted)
                        {
                            bScreenSaverStarted = false;
                            //Screen saver had stopped
                            // Don't end an active postpone just because the screensaver woke - the postpone
                            // has its own timer (which keeps running during a screensaver) that resumes at
                            // the chosen time. Calling ResumeAfterPostponeFinished() unconditionally here
                            // cleared the postpone early, whenever the screensaver kicked in and was
                            // dismissed, instead of at the requested time. Only resume when not postponed.
                            if (!appSettings.bHabitsPostponedState)
                            {
                                ResumeAfterPostponeFinished();
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("FALSE IsScreenSaverActive");
                }
                //Show notification just before Evening habits / Shutoff screen
                TimeSpan tsShutOffTime = TimeSpan.Parse(appSettings.sDayShutOffTime);

                DateTime now = DateTime.Now;
                DateTime myShutOffTime = new DateTime(now.Year, now.Month, now.Day, tsShutOffTime.Hours, tsShutOffTime.Minutes, 00);
                Console.WriteLine("{0}", myShutOffTime.ToString());

                TimeSpan tsStartTime = TimeSpan.Parse(appSettings.sDayStartTime);
                DateTime myStartTime = new DateTime(now.Year, now.Month, now.Day, tsStartTime.Hours, tsStartTime.Minutes, 00);
                Console.WriteLine("{0}", myStartTime.ToString());

                var fiveMinutesBeforeShutOffTime = myShutOffTime.AddMinutes(-5);
                var fiveMinutesFiveSecondsBeforeShutOffTime = fiveMinutesBeforeShutOffTime.AddSeconds(-5);

                bool bShowEveningRoutine = true;
                //If app is deactivated, then do not show FOcus Summary, evening habits screen and the notification that comes 5 minutes before the evening routine starts
                //Only show evening routine if a password is set for Postponing evening routine
                if ((appSettings.IsShutOffTime()) && (appSettings.IsAppDeactivated()))
                {
                    //If cut off time elapsed and password is set of cut off time
                    if (appSettings.DidCutOffTimePassed() && appSettings.bPasswordRequireToSkipEveningAfterCutOff)
                    {
                        //Proceed to Show evening routine
                        bShowEveningRoutine = true;
                    }
                    else if (appSettings.bPasswordRequireInSkipEvening)
                    {
                        //Proceed to Show evening routine
                        bShowEveningRoutine = true;
                    }
                    else
                    {
                        bShowEveningRoutine = false;
                        return;
                    }
                }

                if (now > fiveMinutesFiveSecondsBeforeShutOffTime && now < fiveMinutesBeforeShutOffTime)
                {
                    if (bShowEveningRoutine)
                    {
                        LogWriter.LogWrite("fiveMinutesFiveSecondsBeforeShutOffTime - show notification shutdown approaching");

                        TrayBalloon tt = new TrayBalloon(1, appSettings.ConfigFileStrings["common"]["shutdownNotificationTitle"].ToString());
                        tt.Show();
                    }
                }
                if (appSettings.IsShutOffTime())
                {
                    if (!bShowEveningRoutine)
                    {
                        //Looks like app is deactivated and evening password is not set, so we do not need to show evening routine 
                        LogWriter.LogWrite("App is deactivated and skip evening password is not set / or cutoff one, so we do not need to show evening routine, return");
                        return;
                    }
                    if (appSettings.nCurrentActivityType == CurrentActivityType.SHUT_OFF)
                    {
                        LogWriter.LogWrite("SHUT_OFF STATE - Return from timer");
                        //If shutoff window is already visible, we need to check the 4 hours before morning time and show the up early section
                        if (CheckIf4HoursBeforeNextMorningRoutine())
                        {
                            if (wndShutOff != null && wndShutOff.IsVisible)
                            {
                                wndShutOff.ShowUpEarlySection();
                            }
                        }

                        return;
                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                    {
                        LogWriter.LogWrite("EVENING_HABITS STATE - Return from timer");

                        if (appSettings.nHabitIndex <= 0) //habit index is -1 if focus summary is visible and 0 if first habit is still not started
                        {
                            //if focus summary screen is visible OR if evening state and habits not started, check if its 4 hours before the next morning time, show the shutoff window.
                            //This way we will show shut ioff screen before 4 hours of morning routine so that user can start morning routine or focus sessions.
                            if (CheckIf4HoursBeforeNextMorningRoutine())
                            {
                                //Only if evening routine have not started
                                ShowShutOffScreen();
                                return;
                            }
                        }

                        if (appSettings.DidCutOffTimePassed())
                        {
                            if (appSettings.bHabitsPostponedState && !appSettings.bEveningHabitsPostponedAfterCutoffTime)
                            {
                                //Kick the user off at the cutoff time
                                appSettings.nCurrentActivityType = CurrentActivityType.NONE;
                                LogWriter.LogWrite("Kick the user off at the cutoff time as it was postponed before cutoff time elapsed");

                                return;
                            }

                            if (appSettings.bFocusSummaryPostponedState && !appSettings.bEveningHabitsPostponedAfterCutoffTime)
                            {
                                ((App)(Application.Current)).wndFocusBearMainWindow.PageFocusSummary.ResumeFocusSummary();
                                LogWriter.LogWrite("Kick the user off at the cutoff time as Focus Summary was postponed before cutoff time elapsed");
                                return;
                            }
                            //Check if the current habit index is of High Priority or not, show only if not
                            else if (appSettings.nHabitIndex != -1) //habit index is -1 if focus summary is visible
                            {
                                if (!appSettings.arrAllEveningHabits[appSettings.nHabitIndex].bHighPriorityActivity)
                                {

                                    //since a high priority activity is not running, we need to skip it and show the finish screen 
                                    //and allow starting next high priority activity or show unlock button
                                    if (wndFocusBearMainWindow != null)
                                    {
                                        if (wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing != null)
                                        {
                                            if (wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing.bTimerRunning)
                                            {
                                                wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing.StopTimerOfRunningActivity(); //Stop the timer
                                                                                                                 //we need to call the Activity Skipped API here to skip the current activity

                                                //it may be relax activity window visible
                                                if (wndRelaxMode != null && wndRelaxMode.IsVisible)
                                                {
                                                    wndRelaxMode.bForceClose = true;
                                                    wndRelaxMode.Close();

                                                }
                                                //If the above is null, finish screen may be visible, as ShowACtivityFinishedScreen() make is null
                                                appSettings.RemoveSecondaryWindows();

                                                LogWriter.LogWrite("EVENING_HABITS STATE - now > myCutoffTime - Show Alert");
                                                FBMessageBox msg = new FBMessageBox(appSettings.ConfigFileStrings["common"]["cutOffTimePassedText"].ToString());
                                                msg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                                                msg.ShowDialog();

                                                ShowHabitsSectionInMainWindow(); //V2

                                                appSettings.bSkipCurrentActivityCantDoItToday = true;
                                                LogWriter.LogWrite("appSettings.bSkipCurrentActivityCantDoItToday = true; - After cutoff time");
                                                wndFocusBearMainWindow.PageHabits.ShowACtivityFinishedScreen(true);
                                            }
                                            else
                                            {
                                                //It may be the activity finish screen already
                                                //Or activity may be in postponed state
                                                //Once the user clicks ok, we need to show the High Priority activities only.
                                            }
                                        }


                                    }
                                }
                                else
                                {
                                    //If its a high priority habit, we will need to check if its cutoff time have passed then stop and act accordingly
                                    //cutOffTimePassedForHabitText
                                    //since a high priority activity is running, we need to skip it and show the finish screen 
                                    //and allow starting next high priority activity or show unlock button
                                    //Let's also check if the cut off time of this individual habit and not add if the time already elapsed
                                    if (appSettings.arrAllEveningHabits[appSettings.nHabitIndex].sCutOffTime != "")
                                    {
                                        DateTime now1 = DateTime.Now;
                                        TimeSpan tsTime = TimeSpan.Parse(appSettings.arrAllEveningHabits[appSettings.nHabitIndex].sCutOffTime);
                                        DateTime myAvtivityCutoffTime = new DateTime(now1.Year, now1.Month, now1.Day, tsTime.Hours, tsTime.Minutes, 00);
                                        if (now > myAvtivityCutoffTime)
                                        {
                                            //Skip this habit and move to next
                                            if (wndFocusBearMainWindow != null)
                                            {
                                                if (wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing != null)
                                                {
                                                    if (wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing.bTimerRunning)
                                                    {
                                                        wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing.StopTimerOfRunningActivity(); //Stop the timer
                                                                                                                         //we need to call the Activity Skipped API here to skip the current activity

                                                        //it may be relax activity window visible
                                                        if (wndRelaxMode != null && wndRelaxMode.IsVisible)
                                                        {
                                                            wndRelaxMode.bForceClose = true;
                                                            wndRelaxMode.Close();
                                                        }
                                                        //If the above is null, finish screen may be visible, as ShowACtivityFinishedScreen() make is null
                                                        LogWriter.LogWrite("wndHabits.Hide()");
                                                        //mj// todo handling may be main app window
                                                        //mj//wndHabits.Hide();
                                                        appSettings.RemoveSecondaryWindows();

                                                        LogWriter.LogWrite("EVENING_HABITS STATE - now > myCutoffTime of Individual habit - Show Alert");
                                                        
                                                        FBMessageBox msg = new FBMessageBox(appSettings.ConfigFileStrings["common"]["cutOffTimePassedForHabitText"].ToString(), "", MessageBoxButton.OK, "", "", "", false, "", true);
                                                        msg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                                                        msg.ShowDialog();

                                                        ShowHabitsSectionInMainWindow(); //V2
                                                        appSettings.bSkipCurrentActivityCantDoItToday = true;
                                                        LogWriter.LogWrite("appSettings.bSkipCurrentActivityCantDoItToday = true; - After cutoff time of individual habit - " + appSettings.arrAllEveningHabits[appSettings.nHabitIndex].name);
                                                        wndFocusBearMainWindow.PageHabits.ShowACtivityFinishedScreen(true);
                                                    }
                                                    else
                                                    {
                                                        //It may be the activity finish screen already
                                                        //Or activity may be in postponed state
                                                        //Once the user clicks ok, we need to show the High Priority activities only.
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }

                            }
                        }
                        if (appSettings.bFocusSummaryPostponedState || appSettings.bHabitsPostponedState)
                        {
                            if (!appSettings.isBlockingScheduleRunning)
                            {
                                BlockingScheduleManager.CheckCustomBlockingScheduleTime();
                            }
                            else
                            {
                                BlockingScheduleManager.CheckCustomBlockingScheduleTimeToStop();
                            }
                        }
                        else
                        {
                            BlockingScheduleManager.CheckCustomBlockingScheduleTimeToStop();
                        }
                        return;
                    }
                    //Check if shutoff time is aborted, if not show the shutoff screen
                    if (RegistryManager.GetShutOffAbortedTillDate())
                    {
                        if (DateTime.Now > appSettings.ShutOFFAbortedTillDate) //Looks like a previous day aborted value
                        {
                            //Remove the key for ShutOFFAbortedTillDate
                            RegistryManager.RemoveShutOffAbortedTillDate();
                        }
                        else
                        {
                            //Nothing to do as shutoff is aborted for the day
                            if (!appSettings.isBlockingScheduleRunning)
                            {
                                BlockingScheduleManager.CheckCustomBlockingScheduleTime();
                            }
                            else
                            {
                                BlockingScheduleManager.CheckCustomBlockingScheduleTimeToStop();
                            }
                            return;
                        }
                    }


                    //Check if we have already show the evening habits, we need to show the Shut OFf screen
                    if (RegistryManager.GetEveningHabitsShownTillDate())
                    {
                        if (DateTime.Now > appSettings.EveningHabitsShownTillDate) //Looks like a previous day Evening shown value
                        {
                            LogWriter.LogWrite("dispatcherTimer_Background() DateTime.Now > appSettings.EveningHabitsShownTillDate true ShowEveningRoutine();");
                            ShowEveningRoutine();
                        }
                        else
                        {
                            LogWriter.LogWrite("dispatcherTimer_Background() DateTime.Now > appSettings.EveningHabitsShownTillDate false ShowShutOffScreen(); ");
                            ShowShutOffScreen();
                        }
                    }
                    else
                    {
                        LogWriter.LogWrite("dispatcherTimer_Background() -  ShowEveningRoutine()");
                        ShowEveningRoutine();
                    }
                }
                else
                {
                    //Here first we will check if the app is deactivated then we need not show the Morning routine or the break activities
                    if (appSettings.IsAppDeactivated())
                    {
                        LogWriter.LogWrite("appSettings.IsAppDeactivated() - DO not show morning routine");
                        return;
                    }
                    
                    if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                    {
                        if(appSettings.bHabitsPostponedState)
                        {
                            if (!appSettings.isBlockingScheduleRunning)
                            {
                                BlockingScheduleManager.CheckCustomBlockingScheduleTime();
                            }
                            else
                            {
                                BlockingScheduleManager.CheckCustomBlockingScheduleTimeToStop();
                            }
                        }
                        else
                        {
                            BlockingScheduleManager.CheckCustomBlockingScheduleTimeToStop();
                        }
                        return;
                    }


                    if (RegistryManager.GetMorningHabitsShownTillDate())
                    {
                        if (DateTime.Now < appSettings.MorningHabitsShownTillDate)
                        {
                            isMorningHabitsAlreadyShown = true;
                        }
                        else
                        {
                            isMorningHabitsAlreadyShown = false;
                        }
                    }
                    else
                    {
                        isMorningHabitsAlreadyShown = false;
                    }

                    if (isMorningHabitsAlreadyShown)
                    {
                        //Check idle time
                        if (IdleDetector.IsUserIdle())
                        {
                            //MessageBox.Show("You've been idle for 10 minutes.");
                            if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY ||
                                appSettings.bIsUserOnCall ||
                                appSettings.arrAllbreakActivities.Count == 0 ||
                                !appSettings.bAreBreaksEnabled ||
                                appSettings.bDontShowNaturalBreakDetection)
                            {
                                return;
                            }
                            ShowNaturalBreakDetectionWindow();
                        }

                        //Check if custom blocking schedule time and no active schedule is running
                        if (appSettings.ActiveSchedule == null && !appSettings.isBlockingScheduleRunning)
                        {
                            BlockingScheduleManager.CheckCustomBlockingScheduleTime();
                        }
                        else
                        {
                            BlockingScheduleManager.CheckCustomBlockingScheduleTimeToStop();
                        }

                        return;
                    }
                    else
                    {
                        //it might be possible that user have postponed mornign routine and started breaks
                        if(appSettings.bHabitsPostponedState && appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY )
                        {
                            LogWriter.LogWrite("morning habits are postponed and user is doing break activity..so donot show morning routine, return");
                            return;
                        }
                    }

                    if (!isMorningHabitsAlreadyShown)
                    {
                        LogWriter.LogWrite("dispatcherTimer_Background() -  ShowMorningRoutine()");
                        ShowMorningRoutine();
                    }
                    else
                    {
                        //Start the break Activities Timer
                        StartBreakActivityTimer();
                    }

                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("dispatcherTimer_Background Exception" + ex.Message);
            }

        }
        public async void ShowMorningRoutine()
        {
            LogWriter.LogWrite("ShowMorningRoutine()");

            //Teacher Mode - a teacher controlled class session takes over the screen; the morning
            //routine must not start (or resume) while it is active. It resumes normally on the next
            //background tick once the session ends.
            if (TeacherSessionManager.Instance.IsTeacherSessionActive())
            {
                LogWriter.LogWrite("ShowMorningRoutine() - teacher class session active, not starting the morning routine");
                return;
            }

            if (appSettings.bIsUserOnCall)
            {
                LogWriter.LogWrite("User is on Call - Do not start Morning Routine, return");
                return;
            }

            if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
            {
                LogWriter.LogWrite("ShowMorningRoutine() CurrentActivityType.MORNING_HABITS return");
                return; //Already showing morning routine
            }

            try
            {
                if (wndEasyPostpone != null)
                {
                    wndEasyPostpone.Close();
                    wndEasyPostpone = null;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(" wndEasyPostpone.Close();  /  wndHabits.Close(); ex- " + ex.Message);
            }

            //Let's also reset the Postponed and Fous Summary Postponed states
            appSettings.bFocusSummaryPostponedState = false;
            appSettings.bHabitsPostponedState = false;
            appSettings.bEveningHabitsPostponedAfterCutoffTime = false; //reset

            appSettings.bShowActivitiesFromMid = false;
            LogWriter.LogWrite("appSettings.bShowActivitiesFromMid = false;");

            try
            {
                //Here we will filter the array to just have the current days activity
                //It might be possible that the app is contineously running since many days 
                //SO we always need to first fetch from the GLobal array and then filer it per days
                appSettings.arrAllMorningHabits.Clear();
                DayOfWeek Today = DateTime.Now.DayOfWeek;
                for (int i = 0; i < appSettings.arrGLOBALAllMorningHabits.Count; i++)
                {
                    for (int m = 0; m < appSettings.arrGLOBALAllMorningHabits[i].DaysOfWeek.Count(); m++)
                    {
                        if (appSettings.arrGLOBALAllMorningHabits[i].DaysOfWeek[m].ToLower() == "all")
                        {
                            appSettings.arrAllMorningHabits.Add(appSettings.arrGLOBALAllMorningHabits[i]);
                            break;
                        }
                        else
                        {
                            bool bfound = false;
                            string day = appSettings.arrGLOBALAllMorningHabits[i].DaysOfWeek[m].ToLower();
                            switch (Today)
                            {
                                case DayOfWeek.Sunday:
                                    {
                                        if (day.ToLower() == "sun")
                                        {
                                            bfound = true;
                                        }
                                        break;
                                    }
                                case DayOfWeek.Monday:
                                    {
                                        if (day.ToLower() == "mon")
                                        {
                                            bfound = true;
                                        }
                                        break;
                                    }
                                case DayOfWeek.Tuesday:
                                    {
                                        if (day.ToLower() == "tue")
                                        {
                                            bfound = true;
                                        }
                                        break;
                                    }
                                case DayOfWeek.Wednesday:
                                    {
                                        if (day.ToLower() == "wed")
                                        {
                                            bfound = true;
                                        }
                                        break;
                                    }
                                case DayOfWeek.Thursday:
                                    {
                                        if (day.ToLower() == "thu")
                                        {
                                            bfound = true;
                                        }
                                        break;
                                    }
                                case DayOfWeek.Friday:
                                    {
                                        if (day.ToLower() == "fri")
                                        {
                                            bfound = true;
                                        }
                                        break;
                                    }
                                case DayOfWeek.Saturday:
                                    {
                                        if (day.ToLower() == "sat")
                                        {
                                            bfound = true;
                                        }
                                        break;
                                    }

                            }
                            if (bfound)
                            {
                                appSettings.arrAllMorningHabits.Add(appSettings.arrGLOBALAllMorningHabits[i]);
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("extracting DayOfWeek ex- " + ex.Message);
            }

            try
            {

                if (appSettings.arrAllMorningHabits.Count == 0)
                {
                    LogWriter.LogWrite("ShowMorningRoutine() arrAllMorningHabits.Count == 0 StartBreakActivityTimer and return");

                    //WE will set the morning habits shown date for today
                    RegistryManager.SetMorningHabitsShownTillDate();

                    appSettings.nCurrentActivityType = CurrentActivityType.NONE;
                    LogWriter.LogWrite(" appSettings.nCurrentActivityType = CurrentActivityType.NONE;");

                    RestoreFocusUIAfterRoutine();

                    //Start the break Activities Timer
                    StartBreakActivityTimer();

                    return;
                }

                if (((App)(Application.Current)).wndTakeABreak != null)
                {
                    ((App)(Application.Current)).wndTakeABreak.bShowingForNonTimerActivities = false;

                    if (((App)(Application.Current)).wndTakeABreak.IsVisible)
                    {
                        ((App)(Application.Current)).wndTakeABreak.Close();
                        ((App)(Application.Current)).wndTakeABreak = null;
                    }
                }

                LogWriter.LogWrite("ShowMorningRoutine() - show morning habits screen now");

                appSettings.nCurrentActivityType = CurrentActivityType.MORNING_HABITS;
                LogWriter.LogWrite("appSettings.nCurrentActivityType = CurrentActivityType.MORNING_HABITS;");
                PauseFocusMusicForRoutine();

                appSettings.nHabitIndex = 0;
                LogWriter.LogWrite("nHabitIndex = 0");
                //Here first we will check if we have some pending sequence or if some activities from morning routine are done.
                //If some are done, we will continue from there..
                //if morning routine for today is completed, we will start break activities timer
                //if there is some pending sequnce from any previous days, we will force complete it.

                FocusBearAPIClient client = new FocusBearAPIClient();
                var sResponse = await client.GetLastActivityInfo();
                int xResponse = int.Parse(sResponse.ToString());
                if (xResponse == 1)
                {
                    LogWriter.LogWrite("GetLastActivityInfo Success");

                    if (appSettings.sLastActivityDoneInfo != null || appSettings.sLastActivityDoneInfo.Length > 0)
                    {
                        LogWriter.LogWrite("GetLastActivityInfo Value - " + appSettings.sLastActivityDoneInfo);
                        //Parse the JSON string
                        try
                        {
                            JObject sLastActivityDone = JObject.Parse(appSettings.sLastActivityDoneInfo);

                            //first Detect if the user has changed their habits and pull the new habits from the backend
                            if (sLastActivityDone.ContainsKey("last_time_user_settings_modified"))
                            {
                                string s_edited_settings_date = sLastActivityDone["last_time_user_settings_modified"].ToString();
                                ValidateSettingsChange(s_edited_settings_date);

                            }

                            DateTime now = DateTime.Now;
                            TimeSpan tsStartTime = TimeSpan.Parse(appSettings.sDayStartTime);
                            DateTime myStartTime = new DateTime(now.Year, now.Month, now.Day, tsStartTime.Hours, tsStartTime.Minutes, 00).ToUniversalTime();
                            Console.WriteLine("{0}", myStartTime.ToString());

                            if (now.ToUniversalTime() < myStartTime) //Possibly reaching here if time > 00:00
                            {
                                myStartTime = myStartTime.AddDays(-1);
                            }


                            if (sLastActivityDone.ContainsKey("last_completed_sequence"))
                            {
                                string last_completed_sequence = sLastActivityDone["last_completed_sequence"].ToString();
                                if (last_completed_sequence.Length > 0)
                                {
                                    string lastCompletedSequenceId = sLastActivityDone["last_completed_sequence"]["id"].ToString();
                                    if (lastCompletedSequenceId.Length > 0)
                                    {
                                        var sequenceID = appSettings.arrAllMorningHabits[0].activity_sequence_id;
                                        if (lastCompletedSequenceId == sequenceID)
                                        {
                                            string lastCompletedSequenceStartedAt = sLastActivityDone["last_completed_sequence_started_at"].ToString();
                                            if (lastCompletedSequenceStartedAt.Length > 0)
                                            {

                                                DateTime lastCompletedSequenceStartedAtDate = DateTime.Parse(lastCompletedSequenceStartedAt);
                                                if (lastCompletedSequenceStartedAtDate > myStartTime)
                                                {
                                                    //Today's Morning activity is done
                                                    LogWriter.LogWrite("Morning habits already shown for today - return");
                                                    RegistryManager.SetMorningHabitsShownTillDate();

                                                    appSettings.nCurrentActivityType = CurrentActivityType.NONE;
                                                    LogWriter.LogWrite(" appSettings.nCurrentActivityType = CurrentActivityType.NONE;");

                                                    RestoreFocusUIAfterRoutine();

                                                    //Start the break Activities Timer
                                                    StartBreakActivityTimer();

                                                    return;
                                                }
                                            }
                                        }
                                    }
                                }
                            }


                            //
                            if (sLastActivityDone.ContainsKey("current_activity_assigned_at"))
                            {
                                string current_activity_assigned_at = sLastActivityDone["current_activity_assigned_at"].ToString();
                                if (current_activity_assigned_at.Length > 0)
                                {
                                    if (sLastActivityDone.ContainsKey("current_activity_sequence"))
                                    {
                                        string currentActivitySequence = sLastActivityDone["current_activity_sequence"].ToString();

                                        if (currentActivitySequence.Length > 0)
                                        {
                                            if (sLastActivityDone.ContainsKey("current_activity"))
                                            {
                                                string currentActivity = sLastActivityDone["current_activity"].ToString();
                                                if (currentActivity.Length > 0)
                                                {
                                                    string sequenceId = sLastActivityDone["current_activity_sequence"]["id"].ToString();
                                                    string activityId = sLastActivityDone["current_activity"]["id"].ToString();

                                                    string habitSequenceID = appSettings.arrAllMorningHabits[0].activity_sequence_id;
                                                    if (habitSequenceID.Length > 0)
                                                    {
                                                        DateTime activityAssignedDate = DateTime.Parse(current_activity_assigned_at);
                                                        if (activityAssignedDate > myStartTime)
                                                        {
                                                            //Morning routine started but did not finished
                                                            if (habitSequenceID == sequenceId)
                                                            {
                                                                LogWriter.LogWrite("Morning Habit sequnce is pending. Start from current pending habit");

                                                                appSettings.arraySkipped.Clear();//Clear while initializing
                                                                appSettings.arrayCompletedActivities.Clear();//Clear while initializing

                                                                if (sLastActivityDone.ContainsKey("current_sequence_skipped_activities"))
                                                                {
                                                                    //string skippedActivities = sLastActivityDone["current_sequence_skipped_activities"].ToString();
                                                                    string sArraySkipped = sLastActivityDone["current_sequence_skipped_activities"].ToString();
                                                                    if (sArraySkipped.Length > 0)
                                                                        appSettings.arraySkipped = (JArray)sLastActivityDone["current_sequence_skipped_activities"];
                                                                }

                                                                //get the completed activities dones on other devices
                                                                if (sLastActivityDone.ContainsKey("current_sequence_completed_activities"))
                                                                {
                                                                    string sArrayCompletedActivities = sLastActivityDone["current_sequence_completed_activities"].ToString();
                                                                    if (sArrayCompletedActivities.Length > 0)
                                                                        appSettings.arrayCompletedActivities = (JArray)sLastActivityDone["current_sequence_completed_activities"];
                                                                }

                                                                //Initialize the time for habits
                                                                appSettings.nTotalSecondsAllHabits = 0;
                                                                appSettings.nHabitCompletedSeconds = 0;
                                                                bool bCurrentActivityFound = false;
                                                                for (int i = 0; i < appSettings.arrAllMorningHabits.Count; i++)
                                                                {

                                                                    if (appSettings.arrAllMorningHabits[i].id == activityId) //current activity to be done
                                                                    {
                                                                        bCurrentActivityFound = true;

                                                                        if (i != 0)
                                                                        {
                                                                            appSettings.nHabitIndex = i - 1; //We will assign the habit index from the previous activity and show the finish screen for that activity

                                                                            //Now we do not need to start normal but need to show the finish screen of this
                                                                            appSettings.bShowActivitiesFromMid = true;
                                                                            LogWriter.LogWrite("appSettings.bShowActivitiesFromMid = true; arrAllMorningHabits");
                                                                            for (int m = 0; m < appSettings.arraySkipped.Count; m++)
                                                                            {
                                                                                //Check if the last activity was in the array skipped list
                                                                                if (appSettings.arraySkipped[m].ToString() == appSettings.arrAllMorningHabits[i - 1].id)
                                                                                {
                                                                                    appSettings.bShowActivitiesFromMidLastSkipped = true;
                                                                                    LogWriter.LogWrite("appSettings.bShowActivitiesFromMidLastSkipped = true; arrAllMorningHabits");
                                                                                    break;
                                                                                }
                                                                            }
                                                                        }
                                                                        else
                                                                        {
                                                                            //Now since we have the support of doing activites in random order in mobile, if a user had done some other activity then the first one, the current activity will be 0th one so we need to show the entier list and not start from mid
                                                                            //Here we need to set the habit index to 0 and show the welcome list
                                                                            appSettings.nHabitIndex = 0;
                                                                        }

                                                                        LogWriter.LogWrite("nHabitIndex = " + appSettings.nHabitIndex.ToString());
                                                                    }
                                                                    else if (bCurrentActivityFound)
                                                                    {
                                                                        //If we have found the current activity, now we need to check further to this activity that if there are activitis done
                                                                        //if done randomly, we need to calculate the time and then remove them
                                                                        for (int n = 0; n < appSettings.arrayCompletedActivities.Count; n++)
                                                                        {
                                                                            if (appSettings.arrAllMorningHabits[i].id == appSettings.arrayCompletedActivities[n].ToString())
                                                                            {
                                                                                appSettings.nTotalSecondsAllHabits += appSettings.arrAllMorningHabits[i].durationSeconds;

                                                                                //If found in completed, we need to check if this is not in the skipped array
                                                                                bool bIsSkipped = false;
                                                                                for (int m = 0; m < appSettings.arraySkipped.Count; m++)
                                                                                {
                                                                                    if (appSettings.arraySkipped[m].ToString() == appSettings.arrAllMorningHabits[i].id)
                                                                                    {
                                                                                        bIsSkipped = true;
                                                                                        break;
                                                                                    }
                                                                                }
                                                                                if (!bIsSkipped)
                                                                                {
                                                                                    appSettings.nHabitCompletedSeconds += appSettings.arrAllMorningHabits[i].durationSeconds;
                                                                                }
                                                                                //RE
                                                                                appSettings.arrAllMorningHabits.RemoveAt(i);
                                                                                i--;

                                                                                break;
                                                                            }
                                                                        }
                                                                    }

                                                                }
                                                            }
                                                            else
                                                            {
                                                                //it means current activity is not morning habit and morning habit done for today
                                                                LogWriter.LogWrite("Current Activity is not morning habit and it means morning habit done for today");
                                                                RegistryManager.SetMorningHabitsShownTillDate();

                                                                appSettings.nCurrentActivityType = CurrentActivityType.NONE;
                                                                LogWriter.LogWrite(" appSettings.nCurrentActivityType = CurrentActivityType.NONE;");

                                                                RestoreFocusUIAfterRoutine();

                                                                return;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            LogWriter.LogWrite("Pending routine is not from current day clean all running sequence");
                                                            var sResponse1 = await client.ForceCompleteCurrentSequence(sequenceId);

                                                        }

                                                    }
                                                    else
                                                    {
                                                        LogWriter.LogWrite("Start new Morning routine sequence:- current sequence Id or current activity Id not found");
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                LogWriter.LogWrite("start new Morning routine sequence:- current_activity not found as dictionary");
                                                string sequenceId = sLastActivityDone["current_activity_sequence"]["id"].ToString();
                                                if (sequenceId.Length > 0)
                                                {
                                                    LogWriter.LogWrite("Pending routine is not for use, clean all pending sequence");
                                                    var sResponse1 = await client.ForceCompleteCurrentSequence(sequenceId);

                                                }

                                            }
                                        }
                                    }
                                    else
                                    {
                                        LogWriter.LogWrite("Start new Morning routine sequence:- current_activity_sequence not found as dictionary");
                                        //Start Default
                                    }
                                }
                            }
                            else
                            {
                                LogWriter.LogWrite("Start new Morning routine sequence :- current_activity_assigned_at not found");
                                //Start Default
                            }

                            // 
                        }
                        catch (Exception ex)
                        {
                            LogWriter.LogWrite("GetLastActivityInfo JSON PARSING FAILED" + ex.Message);
                        }
                    }
                    else
                    {

                    }

                }
                else
                {
                    LogWriter.LogWrite("GetLastActivityInfo Failed");
                    //We could not get last activity info so we will start the routine for sure
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Props block ex- " + ex.Message);
            }

            //Check if we the user have selected some activities for today, first check date, then get the habits , complicated logic
            appSettings.arrAllMorningHabitsFiltered = new List<habit>(appSettings.arrAllMorningHabits);

            if (RegistryManager.GetSelectedMorningHabitsDate())
            {
                TimeSpan tsStartTime = TimeSpan.Parse(appSettings.sDayStartTime);
                TimeSpan tsShutOffTime = TimeSpan.Parse(appSettings.sDayShutOffTime);

                DateTime now = DateTime.Now;

                DateTime myStartTime = new DateTime(now.Year, now.Month, now.Day, tsStartTime.Hours, tsStartTime.Minutes, 00);
                Console.WriteLine("{0}", myStartTime.ToString());

                DateTime myShutOffTime = new DateTime(now.Year, now.Month, now.Day, tsShutOffTime.Hours, tsShutOffTime.Minutes, 00);
                Console.WriteLine("{0}", myShutOffTime.ToString());

                if (appSettings.SelectedMorningHabitsDate > myStartTime && appSettings.SelectedMorningHabitsDate < myShutOffTime)
                {
                    //So user selectd habits for today.
                    //Now extract the saved habits list
                    RegistryManager.GetSelectedMorningHabitsForToday(); //These will come in the arrAllMorningHabits array

                    //Now filter the arrAllMorningHabits so that if any habit was done, it should not be there in the Filtered array and thus should be removed from arrAllMorningHabits too
                    //run a loof for the arrAllMorningHabitsFiltered
                    for (int i = 0; i < appSettings.arrAllMorningHabits.Count; i++)
                    {
                        string sID = appSettings.arrAllMorningHabits[i].id;
                        bool bFound = false;
                        for (int j = 0; j < appSettings.arrAllMorningHabitsFiltered.Count; j++)
                        {
                            if (sID == appSettings.arrAllMorningHabitsFiltered[j].id)
                            {
                                bFound = true;
                                break;
                            }
                        }
                        if (!bFound)
                        {
                            appSettings.arrAllMorningHabits.RemoveAt(i);
                            i--;
                        }
                    }
                }
            }

            //If its the expired version, then we need to show top 3 activities only
            if (appSettings.isExpired)
            {
                appSettings.bShowReducedHabitsOnExpired = false;
                for (int i = 0; i < appSettings.arrAllMorningHabits.Count; i++)
                {
                    if (i > 2)
                    {
                        appSettings.arrAllMorningHabits.RemoveAt(i);
                        i--;
                        appSettings.bShowReducedHabitsOnExpired = true;
                    }
                }
            }
            
            FocusButton.SendHabits();

            try
            {
                LogWriter.LogWrite("ShowMorningRoutine - Now show the HabitWindow");
                                 
                ShowBlankWindowOnOtherScreens();

                wndFocusBearMainWindow.PageHabits.InitializeInnerScreens(); //V2 most important to load all screens as required.

                if (!appSettings.bHabitwithAllowedAppsRunning) //Only show window when habit with allowed app is not running..
                {
                    ShowHabitsSectionInMainWindow(); //V2
                }

                if (appSettings.isFocusModePaused && appSettings.isFocusModeRunning)
                {
                    HideFocusUIForRoutine();
                }


                if (appSettings.isFocusModeRunning)
                {
                    ForceStopFocusSessionOnHabitsTime();
                }

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowMorningRoutine" + ex.Message);
            }

        }

        public async void ShowEveningRoutine()
        {
            LogWriter.LogWrite("ShowEveningRoutine() func");

            //Teacher Mode - a teacher controlled class session takes over the screen; the evening
            //routine must not start (or resume) while it is active. It resumes normally on the next
            //background tick once the session ends.
            if (TeacherSessionManager.Instance.IsTeacherSessionActive())
            {
                LogWriter.LogWrite("ShowEveningRoutine() - teacher class session active, not starting the evening routine");
                return;
            }

            if (appSettings.bIsUserOnCall)
            {
                LogWriter.LogWrite("User is on Call - Do not start Evening Routine, return");
                return;
            }

            if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
            {
                LogWriter.LogWrite("ShowEveningRoutine() - appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS return");

                //if focus summary screen is visible OR if evening state and habits not started, check if its 4 hours before the next morning time, show the shutoff window.
                //This way we will show shut off screen before 4 hours of morning routine so that user can start morning routine or focus sessions.
                if (CheckIf4HoursBeforeNextMorningRoutine())
                {
                    //Only if evening routine have not started

                    ShowShutOffScreen();
                    return;
                }

                return; //Already showing evening routine
            }

            try
            {

                if (wndEasyPostpone != null)
                {
                    wndEasyPostpone.Close();
                    wndEasyPostpone = null;
                }
                //We need to check if anything is going on and kill that first
                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS ||
                    appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                {

                    if (wndFocusBearMainWindow != null)
                    {
                        //It might be possible that the break activity is running..
                        if (wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing != null)
                        {
                            if (wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing.bTimerRunning)
                            {
                                wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing.StopTimerOfRunningActivity(); //Stop the timer
                            }
                        }

                        LogWriter.LogWrite("We need to check if anything is going on and kill that first, MORNING_HABITS OR BREAK_ACTIVITY ");
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowEveningRoutine() func - trying to close old windows" + ex.Message);
            }
            //check if there are evening habits otherwise return from here
            if (appSettings.arrGLOBALAllEveningHabits == null || appSettings.arrGLOBALAllEveningHabits.Count == 0)
            {
                LogWriter.LogWrite("ShowEveningRoutine() func - No evening habits, returning");
                return;
            }

            appSettings.nCurrentActivityType = CurrentActivityType.EVENING_HABITS; //We use this for Focus Summary as well, when FOcus summary ends we change this to none
            LogWriter.LogWrite("appSettings.nCurrentActivityType = CurrentActivityType.EVENING_HABITS;");
            PauseFocusMusicForRoutine();

            //Let's also reset the Postponed and Fous Summary Postponed states
            appSettings.bFocusSummaryPostponedState = false;
            appSettings.bHabitsPostponedState = false;
            appSettings.bEveningHabitsPostponedAfterCutoffTime = false; //reset

            appSettings.bShowActivitiesFromMid = false;
            LogWriter.LogWrite("appSettings.bShowActivitiesFromMid = false;");

            //Stopping the breaks timer here
            dispatcherTimerBreaks.Stop();

            //Check if we have already show the Focus Summary,  
            if (RegistryManager.GetFocusSummaryShownTillDate())
            {
                if (DateTime.Now > appSettings.FocusSummaryShownTillDate) //Looks like a previous day Evening shown value
                {
                    LogWriter.LogWrite("DateTime.Now > appSettings.FocusSummaryShownTillDate true Focus Summary for some other day");
                    ShowFocusSummaryWindow();
                    return;
                }
                else
                {
                    LogWriter.LogWrite("DateTime.Now > appSettings.FocusSummaryShownTillDate false - focus summary shown for today procees to evening routine checks(); ");
                    //Proceed procees to evening routine checks();
                }
            }
            else
            {
                ShowFocusSummaryWindow();
                return;
            }

            //Here we will filter the array to just have the current days activity
            //It might be possible that the app is contineously running since many days 
            //SO we always need to first fetch from the GLobal array and then filer it per days
            appSettings.bSomeHabitsRemovedDueToCutOffTime = false; //Initialte Again

            appSettings.arrAllEveningHabits.Clear();
            DayOfWeek Today = DateTime.Now.DayOfWeek;
            if(appSettings.arrGLOBALAllEveningHabits != null)
            { 
                for (int i = 0; i < appSettings.arrGLOBALAllEveningHabits.Count; i++)
                {
                    for (int m = 0; m < appSettings.arrGLOBALAllEveningHabits[i].DaysOfWeek.Count(); m++)
                    {
                        if (appSettings.arrGLOBALAllEveningHabits[i].DaysOfWeek[m].ToLower() == "all")
                        {
                            //Let's also check if the cut off time of this individual habit and not add if the time already elapsed
                            if (appSettings.DidCutOffTimePassed())
                            {
                                if (appSettings.arrGLOBALAllEveningHabits[i].bHighPriorityActivity)
                                {
                                    if (appSettings.arrGLOBALAllEveningHabits[i].sCutOffTime != "")
                                    {
                                        DateTime now = DateTime.Now;
                                        TimeSpan tsTime = TimeSpan.Parse(appSettings.arrGLOBALAllEveningHabits[i].sCutOffTime);
                                        DateTime myAvtivityStartTime = new DateTime(now.Year, now.Month, now.Day, tsTime.Hours, tsTime.Minutes, 00);
                                        if (now < myAvtivityStartTime)
                                        {
                                            appSettings.arrAllEveningHabits.Add(appSettings.arrGLOBALAllEveningHabits[i]);
                                        }
                                        else
                                        {
                                            appSettings.bSomeHabitsRemovedDueToCutOffTime = true;
                                        }
                                    }
                                    else
                                    {
                                        appSettings.arrAllEveningHabits.Add(appSettings.arrGLOBALAllEveningHabits[i]); //Add if there is no 
                                    }
                                }
                            }
                            else
                            {
                                appSettings.arrAllEveningHabits.Add(appSettings.arrGLOBALAllEveningHabits[i]);
                            }
                            break;
                        }
                        else
                        {
                            bool bfound = false;
                            string day = appSettings.arrGLOBALAllEveningHabits[i].DaysOfWeek[m].ToLower();
                            switch (Today)
                            {
                                case DayOfWeek.Sunday:
                                    {
                                        if (day.ToLower() == "sun")
                                        {
                                            bfound = true;
                                        }
                                        break;
                                    }
                                case DayOfWeek.Monday:
                                    {
                                        if (day.ToLower() == "mon")
                                        {
                                            bfound = true;
                                        }
                                        break;
                                    }
                                case DayOfWeek.Tuesday:
                                    {
                                        if (day.ToLower() == "tue")
                                        {
                                            bfound = true;
                                        }
                                        break;
                                    }
                                case DayOfWeek.Wednesday:
                                    {
                                        if (day.ToLower() == "wed")
                                        {
                                            bfound = true;
                                        }
                                        break;
                                    }
                                case DayOfWeek.Thursday:
                                    {
                                        if (day.ToLower() == "thu")
                                        {
                                            bfound = true;
                                        }
                                        break;
                                    }
                                case DayOfWeek.Friday:
                                    {
                                        if (day.ToLower() == "fri")
                                        {
                                            bfound = true;
                                        }
                                        break;
                                    }
                                case DayOfWeek.Saturday:
                                    {
                                        if (day.ToLower() == "sat")
                                        {
                                            bfound = true;
                                        }
                                        break;
                                    }

                            }

                            if (bfound)
                            {
                                //Let's also check if the cut off time of this individual habit and not add if the time already elapsed
                                if (appSettings.DidCutOffTimePassed())
                                {
                                    if (appSettings.arrGLOBALAllEveningHabits[i].bHighPriorityActivity)
                                    {
                                        if (appSettings.arrGLOBALAllEveningHabits[i].sCutOffTime != "")
                                        {
                                            DateTime now = DateTime.Now;
                                            TimeSpan tsTime = TimeSpan.Parse(appSettings.arrGLOBALAllEveningHabits[i].sCutOffTime);
                                            DateTime myAvtivityStartTime = new DateTime(now.Year, now.Month, now.Day, tsTime.Hours, tsTime.Minutes, 00);
                                            if (now > myAvtivityStartTime)
                                            {
                                                bfound = false;
                                                appSettings.bSomeHabitsRemovedDueToCutOffTime = true;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        bfound = false;
                                        appSettings.bSomeHabitsRemovedDueToCutOffTime = true;
                                    }
                                }
                            }

                            if (bfound)
                            {
                                appSettings.arrAllEveningHabits.Add(appSettings.arrGLOBALAllEveningHabits[i]);
                                break;
                            }
                        }
                    }
                }
            }

            if (appSettings.arrAllEveningHabits.Count == 0)
            {
                LogWriter.LogWrite("ShowEveningRoutine() - appSettings.arrAllEveningHabits.Count == 0 ShowShutOffScreen and return");

                ShowShutOffScreen(); //IF there are no evening habits, skip to shutoff.
                return;
            }

            //Set initial Habit Index to 0;
            appSettings.nHabitIndex = 0;
            LogWriter.LogWrite("nHabitIndex = 0");
            //Here first we will check if we have some pending sequence or if some activities from morning routine are done.
            //If some are done, we will continue from there..
            //if morning routine for today is completed, we will start break activities timer
            //if there is some pending sequnce from any previous days, we will force complete it.

            FocusBearAPIClient client = new FocusBearAPIClient();

            var sResponse = await client.GetLastActivityInfo();
            int xResponse = int.Parse(sResponse.ToString());
            if (xResponse == 1)
            {
                LogWriter.LogWrite("GetLastActivityInfo Success - checking before evening routine");

                if (appSettings.sLastActivityDoneInfo != null || appSettings.sLastActivityDoneInfo.Length > 0)
                {
                    LogWriter.LogWrite("GetLastActivityInfo Value - " + appSettings.sLastActivityDoneInfo);
                    //Parse the JSON string
                    try
                    {
                        JObject sLastActivityDone = JObject.Parse(appSettings.sLastActivityDoneInfo);

                        //first Detect if the user has changed their habits and pull the new habits from the backend
                        if (sLastActivityDone.ContainsKey("last_time_user_settings_modified"))
                        {
                            string s_edited_settings_date = sLastActivityDone["last_time_user_settings_modified"].ToString();
                            ValidateSettingsChange(s_edited_settings_date);

                        }
                        DateTime now = DateTime.Now;
                        TimeSpan tsShutoffTime = TimeSpan.Parse(appSettings.sDayShutOffTime);
                        DateTime myShutOffTime = new DateTime(now.Year, now.Month, now.Day, tsShutoffTime.Hours, tsShutoffTime.Minutes, 00).ToUniversalTime();
                        Console.WriteLine("{0}", myShutOffTime.ToString());

                        if (now.ToUniversalTime() < myShutOffTime) //Possibly reaching here if current time > 00:00
                        {
                            myShutOffTime = myShutOffTime.AddDays(-1);
                        }

                        if (sLastActivityDone.ContainsKey("last_completed_sequence"))
                        {
                            string lastCompletedSequenceId = sLastActivityDone["last_completed_sequence"]["id"].ToString();
                            if (lastCompletedSequenceId.Length > 0)
                            {
                                var sequenceID = appSettings.arrAllEveningHabits[0].activity_sequence_id;
                                if (lastCompletedSequenceId == sequenceID)
                                {
                                    string lastCompletedSequenceStartedAt = sLastActivityDone["last_completed_sequence_started_at"].ToString();
                                    if (lastCompletedSequenceStartedAt.Length > 0)
                                    {

                                        DateTime lastCompletedSequenceStartedAtDate = DateTime.Parse(lastCompletedSequenceStartedAt);
                                        if (lastCompletedSequenceStartedAtDate > myShutOffTime)
                                        {
                                            //Today's Evening activity is done
                                            LogWriter.LogWrite("Evening habits already shown for today - return");

                                            TimeSpan tsShutOffTime = TimeSpan.Parse(appSettings.sDayShutOffTime);
                                            DateTime myShutOffTimeLocal = new DateTime(now.Year, now.Month, now.Day, tsShutOffTime.Hours, tsShutOffTime.Minutes, 00);
                                            Console.WriteLine("{0}", myShutOffTimeLocal.ToString());


                                            TimeSpan tsStartTime = TimeSpan.Parse(appSettings.sDayStartTime);
                                            DateTime myStartTime = new DateTime(now.Year, now.Month, now.Day, tsStartTime.Hours, tsStartTime.Minutes, 00);
                                            Console.WriteLine("{0}", myStartTime.ToString());

                                            if (now > myShutOffTimeLocal) //Same Day like befoee 12.00 am
                                            {
                                                //Get the morning time and prepare a DT for Evening Habits Shown Till Date
                                                myStartTime = myStartTime.AddDays(1);
                                                Console.WriteLine("{0}", myStartTime.ToString());
                                                appSettings.EveningHabitsShownTillDate = myStartTime;
                                            }
                                            else //Next Day like after 12.00 am
                                            {
                                                appSettings.EveningHabitsShownTillDate = myStartTime;//Today's Morning time
                                            }
                                            LogWriter.LogWrite(string.Format("Set EveningHabitsShownTillDate - {0}", appSettings.EveningHabitsShownTillDate.ToString()));

                                            RegistryManager.SetEveningHabitsShownTillDate();

                                            appSettings.nCurrentActivityType = CurrentActivityType.NONE;
                                            LogWriter.LogWrite(" appSettings.nCurrentActivityType = CurrentActivityType.NONE;");

                                            RestoreFocusUIAfterRoutine();

                                            return;
                                        }

                                    }
                                }
                            }
                        }


                        //
                        if (sLastActivityDone.ContainsKey("current_activity_assigned_at"))
                        {
                            string current_activity_assigned_at = sLastActivityDone["current_activity_assigned_at"].ToString();
                            if (current_activity_assigned_at.Length > 0)
                            {
                                if (sLastActivityDone.ContainsKey("current_activity_sequence"))
                                {
                                    string currentActivitySequence = sLastActivityDone["current_activity_sequence"].ToString();

                                    if (currentActivitySequence.Length > 0)
                                    {
                                        if (sLastActivityDone.ContainsKey("current_activity"))
                                        {
                                            string currentActivity = sLastActivityDone["current_activity"].ToString();
                                            if (currentActivity.Length > 0)
                                            {
                                                string sequenceId = sLastActivityDone["current_activity_sequence"]["id"].ToString();
                                                string activityId = sLastActivityDone["current_activity"]["id"].ToString();

                                                string habitSequenceID = appSettings.arrAllEveningHabits[0].activity_sequence_id;
                                                if (habitSequenceID.Length > 0)
                                                {
                                                    DateTime activityAssignedDate = DateTime.Parse(current_activity_assigned_at);
                                                    if (activityAssignedDate > myShutOffTime)
                                                    {
                                                        //evening routine started but did not finished
                                                        if (habitSequenceID == sequenceId)
                                                        {
                                                            LogWriter.LogWrite("Evening Habit sequnce is pending. Start from current pending habit");

                                                            appSettings.arraySkipped.Clear();//Clear while initializing
                                                            appSettings.arrayCompletedActivities.Clear();//Clear while initializing

                                                            if (sLastActivityDone.ContainsKey("current_sequence_skipped_activities"))
                                                            {
                                                                string sArraySkipped = sLastActivityDone["current_sequence_skipped_activities"].ToString();
                                                                if (sArraySkipped.Length > 0)
                                                                    appSettings.arraySkipped = (JArray)sLastActivityDone["current_sequence_skipped_activities"];
                                                            }

                                                            //get the completed activities dones on other devices
                                                            if (sLastActivityDone.ContainsKey("current_sequence_completed_activities"))
                                                            {
                                                                string sArrayCompletedActivities = sLastActivityDone["current_sequence_completed_activities"].ToString();
                                                                if (sArrayCompletedActivities.Length > 0)
                                                                    appSettings.arrayCompletedActivities = (JArray)sLastActivityDone["current_sequence_completed_activities"];
                                                            }

                                                            //Initialize the time for habits
                                                            appSettings.nTotalSecondsAllHabits = 0;
                                                            appSettings.nHabitCompletedSeconds = 0;
                                                            bool bCurrentActivityFound = false;

                                                            for (int i = 0; i < appSettings.arrAllEveningHabits.Count; i++)
                                                            {
                                                                if (appSettings.arrAllEveningHabits[i].id == activityId)
                                                                {
                                                                    bCurrentActivityFound = true;

                                                                    if (i != 0)
                                                                    {
                                                                        appSettings.nHabitIndex = i - 1; //We will assign the habit index from the previous actiity and show the finish screen for that activity

                                                                        //Now we do not need to start normal but need to show the finish screen of this
                                                                        appSettings.bShowActivitiesFromMid = true;
                                                                        LogWriter.LogWrite("appSettings.bShowActivitiesFromMid = true; arrAllEveningHabits");

                                                                        for (int m = 0; m < appSettings.arraySkipped.Count; m++)
                                                                        {
                                                                            //Check if the last activity was in the array skipped list
                                                                            if (appSettings.arraySkipped[m].ToString() == appSettings.arrAllEveningHabits[i - 1].id)
                                                                            {
                                                                                appSettings.bShowActivitiesFromMidLastSkipped = true;
                                                                                LogWriter.LogWrite("appSettings.bShowActivitiesFromMidLastSkipped = true; arrAllEveningHabits");
                                                                                break;
                                                                            }
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        //Now since we have the support of doing activites in random order in mobile, if a user had done some other activity then the first one, the current activity will be 0th one so we need to show the entier list and not start from mid
                                                                        //Here we need to set the habit index to 0 and show the welcome list
                                                                        appSettings.nHabitIndex = 0;
                                                                    }

                                                                    LogWriter.LogWrite("nHabitIndex = " + appSettings.nHabitIndex.ToString());
                                                                }
                                                                else if (bCurrentActivityFound)
                                                                {
                                                                    //If we have found the current activity, now we need to check further to this activity that if there are activitis done
                                                                    //if done randomly, we need to calculate the time and then remove them
                                                                    for (int n = 0; n < appSettings.arrayCompletedActivities.Count; n++)
                                                                    {
                                                                        if (appSettings.arrAllEveningHabits[i].id == appSettings.arrayCompletedActivities[n].ToString())
                                                                        {
                                                                            appSettings.nTotalSecondsAllHabits += appSettings.arrAllEveningHabits[i].durationSeconds;

                                                                            //If found in completed, we need to check if this is not in the skipped array
                                                                            bool bIsSkipped = false;
                                                                            for (int m = 0; m < appSettings.arraySkipped.Count; m++)
                                                                            {
                                                                                if (appSettings.arraySkipped[m].ToString() == appSettings.arrAllEveningHabits[i].id)
                                                                                {
                                                                                    bIsSkipped = true;
                                                                                    break;
                                                                                }
                                                                            }
                                                                            if (!bIsSkipped)
                                                                            {
                                                                                appSettings.nHabitCompletedSeconds += appSettings.arrAllEveningHabits[i].durationSeconds;
                                                                            }
                                                                            //RE
                                                                            appSettings.arrAllEveningHabits.RemoveAt(i);
                                                                            i--;
                                                                            break;
                                                                        }
                                                                    }
                                                                }

                                                            }
                                                        }
                                                        else
                                                        {
                                                            //it means Current Activity is not Evening habit and it means Evening habit done for today
                                                            LogWriter.LogWrite("Current Activity is not Evening habit and it means Evening habit done for today");
                                                            appSettings.EveningHabitsShownTillDate = DateTime.Now;
                                                            RegistryManager.SetEveningHabitsShownTillDate();
                                                        }
                                                    }
                                                    else
                                                    {
                                                        LogWriter.LogWrite("Pending routine is not from current day clean all running sequence");
                                                        var sResponse1 = await client.ForceCompleteCurrentSequence(sequenceId);
                                                    }

                                                }
                                                else
                                                {
                                                    LogWriter.LogWrite("Start new Evening routine sequence:- current sequence Id or current activity Id not found");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            LogWriter.LogWrite("start new Evening routine sequence:- current_activity not found as dictionary");
                                            string sequenceId = sLastActivityDone["current_activity_sequence"]["id"].ToString();
                                            if (sequenceId.Length > 0)
                                            {
                                                LogWriter.LogWrite("Pending routine is not for use, clean all pending sequence");
                                                var sResponse1 = await client.ForceCompleteCurrentSequence(sequenceId);
                                            }

                                        }
                                    }
                                }
                                else
                                {
                                    LogWriter.LogWrite("Start new Evening routine sequence:- current_activity_sequence not found as dictionary");
                                    //Start Default
                                }
                            }
                        }
                        else
                        {
                            LogWriter.LogWrite("Start new Evening routine sequence :- current_activity_assigned_at not found");
                            //Start Default
                        }

                        // 
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("GetLastActivityInfo JSON PARSING FAILED" + ex.Message);
                    }
                }
                else
                {

                }

            }
            else
            {
                LogWriter.LogWrite("GetLastActivityInfo Failed");
                //We could not get last activity info so we will start the routine for sure
            }

            //Check if we the user have selected some activities for today, first check date, then get the Evening habits , complicated logic
            appSettings.arrAllEveningHabitsFiltered = new List<habit>(appSettings.arrAllEveningHabits);

            if (RegistryManager.GetSelectedEveningHabitsDate())
            {
                TimeSpan tsStartTime = TimeSpan.Parse(appSettings.sDayStartTime);
                TimeSpan tsShutOffTime = TimeSpan.Parse(appSettings.sDayShutOffTime);

                DateTime now = DateTime.Now;

                DateTime myStartTimeNextDay = new DateTime(now.Year, now.Month, now.Day, tsStartTime.Hours, tsStartTime.Minutes, 00);
                myStartTimeNextDay = myStartTimeNextDay.AddDays(1); //next day
                Console.WriteLine("{0}", myStartTimeNextDay.ToString());

                DateTime myShutOffTime = new DateTime(now.Year, now.Month, now.Day, tsShutOffTime.Hours, tsShutOffTime.Minutes, 00);
                Console.WriteLine("{0}", myShutOffTime.ToString());

                if (appSettings.SelectedEveningHabitsDate > myShutOffTime && appSettings.SelectedEveningHabitsDate < myStartTimeNextDay)
                {
                    //So user selectd habits for today.
                    //Now extract the saved habits list
                    RegistryManager.GetSelectedEveningHabitsForToday(); //These will come in the arrAllEveningHabits array

                    //Now filter the arrAllEveningHabits so that if any habit was done, it should not be there in the Filtered array and thus should be removed from arrAllMorningHabits too
                    //run a loof for the arrAllEveningHabitsFiltered
                    for (int i = 0; i < appSettings.arrAllEveningHabits.Count; i++)
                    {
                        string sID = appSettings.arrAllEveningHabits[i].id;
                        bool bFound = false;
                        for (int j = 0; j < appSettings.arrAllEveningHabitsFiltered.Count; j++)
                        {
                            if (sID == appSettings.arrAllEveningHabitsFiltered[j].id)
                            {
                                bFound = true;
                                break;
                            }
                        }
                        if (!bFound)
                        {
                            appSettings.arrAllEveningHabits.RemoveAt(i);
                            i--;
                        }
                    }
                }
            }
            //If its the expired version, then we need to show top 3 activities only but ignore the relax one whereever it is in the list
            if (appSettings.isExpired)
            {
                appSettings.bShowReducedHabitsOnExpired = false;
           
                var list = appSettings.arrAllEveningHabits;

                // Find the relax habit (first one with the flag)
                int relaxIndex = list.FindIndex(h => h.show_saved_distracting_websites);

                List<habit> reduced;

                if (relaxIndex >= 0)
                {
                    var relaxHabit = list[relaxIndex];

                    // Take the top 3 *excluding* the special habit
                    var others = list
                        .Where(h => !h.show_saved_distracting_websites)
                        .Take(3)
                        .ToList();

                    reduced = new List<habit>();
                    reduced.AddRange(others);

                    // Add the relax habit (ensure it's included)
                    reduced.Add(relaxHabit);
                }
                else
                {
                    // No special → only top 3
                    reduced = list.Take(3).ToList();
                }

                // Replace original list
                bool wasReduced = list.Count != reduced.Count;
                appSettings.arrAllEveningHabits = reduced;
                appSettings.bShowReducedHabitsOnExpired = wasReduced;

            }

            FocusButton.SendHabits();

            LogWriter.LogWrite("ShowEveningRoutine() - show evening habits screen now");

            try
            {
                ShowBlankWindowOnOtherScreens();

                wndFocusBearMainWindow.PageHabits.InitializeInnerScreens(); //V2 most important to load all screens as required.

                if (!appSettings.bHabitwithAllowedAppsRunning) //Only show window when habit with allowed app is not running..
                {
                    ShowHabitsSectionInMainWindow(); //V2
                }

                if (appSettings.isFocusModePaused && appSettings.isFocusModeRunning)
                {
                    HideFocusUIForRoutine();
                }

                if (appSettings.isFocusModeRunning)
                {
                    ForceStopFocusSessionOnHabitsTime();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowEveningRoutine" + ex.Message);
            }
        }

        public void ShowShutOffScreen()
        {
            LogWriter.LogWrite("ShowShutOffScreen()");

            if (appSettings.nCurrentActivityType == CurrentActivityType.SHUT_OFF)
            {
                LogWriter.LogWrite("ShowShutOffScreen() - appSettings.nCurrentActivityType == CurrentActivityType.SHUT_OFF return");
                return; 
            }

            if(!appSettings.DidCutOffTimePassed())
            {
                appSettings.nCurrentActivityType = CurrentActivityType.NONE;
                LogWriter.LogWrite("ShowShutOffScreen() - cutoff time yet to come return");
                return;
            }

            appSettings.nCurrentActivityType = CurrentActivityType.SHUT_OFF;
            LogWriter.LogWrite("appSettings.nCurrentActivityType = CurrentActivityType.SHUT_OFF;");

            try
            {
                ShowBlankWindowOnOtherScreens();

                ShowHabitsSectionInMainWindow();  
               
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowShutOffScreen() ex - " + ex.Message);
            }

            if (appSettings.isFocusModeRunning)
            {
                if (appSettings.isFocusModePaused)
                {
                    appSettings.isFocusModePaused = false;
                    appSettings.nPausedFocusRemainingSeconds = 0;
                }

                ForceStopFocusSessionOnHabitsTime(); // Use the same function as morning routine
            }
        }



        public void ShowExpiredNag(bool bShowingOnAppStartedExpired = false)
        {
            LogWriter.LogWrite("ShowExpiredNag()");

            UpgradeNow up = new UpgradeNow();
            bool? bResult = up.ShowDialog();

            if (bShowingOnAppStartedExpired)
            {
                if (bResult.Value == false)
                {
                    LogWriter.LogWrite("Upgrade Nag Closed at very beginning, Terminate application");
                    Application.Current.Shutdown(0);
                }
            }
        }

        public void ShowUpgradeToProNag(string sTitleContext)
        {
            LogWriter.LogWrite("ShowUpgradeToProNag()");

            UpgradeToPro up = new UpgradeToPro(sTitleContext);
            bool? bResult = up.ShowDialog();

        }

        public void BringMainWindowToFront()
        {
            wndFocusBearMainWindow.Show();

            WindowInteropHelper helper = new WindowInteropHelper(wndFocusBearMainWindow);
            IntPtr hWnd = helper.Handle;

            ShowWindow(hWnd, SW_SHOW);

            if ((appSettings.nCurrentActivityType == CurrentActivityType.ONBOARDING) ||
                !appSettings.IsLoggedIn) //v3
            {
                wndFocusBearMainWindow.Topmost = false;
                wndFocusBearMainWindow.SetMinimizeButtonEnabled(true);
                wndFocusBearMainWindow.SetMaximizeButtonEnabled(true);
                wndFocusBearMainWindow.CollapseSideBar();
                wndFocusBearMainWindow.DisableAllButtons(LeftButtonType.None);
            }
            else 
            { 

                if (((appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS ||
                appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS ||
                appSettings.nCurrentActivityType == CurrentActivityType.SHUT_OFF
                ) && !appSettings.bHabitsPostponedState && !appSettings.bFocusSummaryPostponedState) ||
                (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY))
                {
                    if (appSettings.bPlayActivitiesInFullScreenMode) //Dsiable all in full screen
                    {
                        wndFocusBearMainWindow.WindowStyle = WindowStyle.None;
                        wndFocusBearMainWindow.WindowState = WindowState.Maximized;
                        // WindowStyle.None alone keeps the (invisible) resize frame, so the locked
                        // full-screen activity window could still be dragged/resized. NoResize removes it.
                        wndFocusBearMainWindow.ResizeMode = ResizeMode.NoResize;
                    }
                    wndFocusBearMainWindow.Topmost = true; //when user is in habits we will keep topmost
                    wndFocusBearMainWindow.SetMinimizeButtonEnabled(false);
                    wndFocusBearMainWindow.SetMaximizeButtonEnabled(false);
                }
                else
                {
                    wndFocusBearMainWindow.Topmost = false; //when user is not in habits we will remove topmost
                    wndFocusBearMainWindow.WindowStyle = WindowStyle.SingleBorderWindow;
                    wndFocusBearMainWindow.ResizeMode = ResizeMode.CanResize; //restore normal resizing outside locked activities
                    // Restore window if minimized
                    ShowWindow(hWnd, SW_RESTORE);

                    wndFocusBearMainWindow.SetMinimizeButtonEnabled(true);
                    wndFocusBearMainWindow.SetMaximizeButtonEnabled(true);

                }
            }
            

            // Bring to foreground
            SetForegroundWindow(hWnd);

            wndFocusBearMainWindow.Visibility = Visibility.Visible;
            wndFocusBearMainWindow.Activate();

            // Force redraw
            Dispatcher.BeginInvoke(new Action(() =>
            {
                wndFocusBearMainWindow.InvalidateVisual();
                wndFocusBearMainWindow.UpdateLayout();
            }), System.Windows.Threading.DispatcherPriority.Render);
        }
        public void ShowHabitsSectionInMainWindow()
        {
            LogWriter.LogWrite("ShowHabitsSectionInMainWindow ()");

            BringMainWindowToFront();

            wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.Habits);
        }
        public void ShowBreaksSectionInMainWindow()
        {
            LogWriter.LogWrite("ShowBreaksSectionInMainWindow ()");

            BringMainWindowToFront();

            wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.MicroBreaks);
        }
        public void ShowPreferencesWindow()
        {
            LogWriter.LogWrite("ShowPreferencesWindow ()");

            BringMainWindowToFront();

            wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.Settings);
        }
        public void ShowFocusEndWindow()
        {
            LogWriter.LogWrite("ShowFocusEndWindow ()");

            BringMainWindowToFront();
            //Just select the Start focus session button in left tree
            wndFocusBearMainWindow.SelectLeafInTreeView(LeftButtonType.StartFocusSession);

            wndFocusBearMainWindow.ShowFocusModeEndView();
        }
        public void ShowBreakWindow()
        {
            LogWriter.LogWrite("ShowBreakWindow ()");
            try
            {
                if (wndTakeABreak == null)
                {
                    wndTakeABreak = new BreakWindow();
                }

                wndTakeABreak.Show();
                Focus_Bear.Utils.WindowHelper.EnsureWindowVisible(wndTakeABreak); // Make sure the window is visible
                wndTakeABreak.BringIntoView();

                int nBreakMinutes = 300; //Default to 5 minutes
                if (appSettings.sMinutesBreakPomodoro.Length > 0)
                {
                    nBreakMinutes = int.Parse(appSettings.sMinutesBreakPomodoro);
                    nBreakMinutes = nBreakMinutes * 60;
                }

                wndTakeABreak.ShowBreakWindowAndStartTimer(nBreakMinutes);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowBreakWindow() ex - " + ex.Message);
            }

        }

        public void ShowRelaxActivityWindow(int nTime)
        {
            try
            {

                if (wndRelaxMode == null)
                {
                    wndRelaxMode = new RelaxModeWindow(nTime);
                }
                wndRelaxMode.Show();
                wndRelaxMode.BringIntoView();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowRelaxActivityWindow() ex - " + ex.Message);
            }
        }

        public void ShowBreakWindowForAllowedAppsActivity(int nDuration, string sTitle)
        {
            LogWriter.LogWrite("ShowBreakWindowForAllowedAppsActivity()");

            try
            {
                if (wndTakeABreak == null)
                {
                    wndTakeABreak = new BreakWindow();
                }

                wndTakeABreak.bShowingForNonTimerActivities = true;
                wndTakeABreak.Show();
                wndTakeABreak.BringIntoView();
                wndTakeABreak.ShowBreakWindowAndStartTimer(nDuration, sTitle);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowBreakWindowForAllowedAppsActivity() ex - " + ex.Message);

                wndTakeABreak = new BreakWindow();

                wndTakeABreak.bShowingForNonTimerActivities = true;
                wndTakeABreak.Show();
                wndTakeABreak.BringIntoView();
                wndTakeABreak.ShowBreakWindowAndStartTimer(nDuration, sTitle);
            }

        }

        public void ShowEasyPostponeWindow()
        {
            //always create new
            wndEasyPostpone = new EasySkipPostpone(wndFocusBearMainWindow);
            wndFocusBearMainWindow.GridShade.Visibility = Visibility.Visible;
            wndEasyPostpone.ShowDialog();
            wndFocusBearMainWindow.GridShade.Visibility = Visibility.Collapsed;

            // WPF re-activates the owner window when a modal dialog closes.
            // Hide after ShowDialog returns — same pattern used in FocusActivitySummary.
            if (appSettings.bHabitsPostponedState && !appSettings.isFocusModeRunning && appSettings.AutoCloseAfterRoutineSkipPostpone)
            {
                wndFocusBearMainWindow.Hide();
            }
        }

        //bForClassSession (Teacher Mode): postpone the routine for the whole class session (duration + 1
        //min, from the backend). Two differences from a normal user postpone: the current activity type
        //is kept as MORNING/EVENING_HABITS (the routine is only paused for the session, not cancelled),
        //and the "your habits resume in 2 minutes" popup is suppressed so it never flashes over the
        //locked class session. Enforcement stays correct because the habit-based enforcement is gated on
        //bHabitwithAllowedAppsRunning (set false below), not on the activity type.
        // ── Postpone persistence across restarts (#1278 cherry-pick, postpone-only) ───────────────────

        // The routine-window guard: a persisted postpone is honoured on restart only within the routine's
        // own window — a MORNING postpone until today's shut-off (when the evening routine / focus summary
        // appears), an EVENING postpone until the next day's morning start. Past that the window is over.
        private DateTime HabitsPostponeGuardTime(CurrentActivityType type)
        {
            DateTime now = DateTime.Now;
            try
            {
                if (type == CurrentActivityType.MORNING_HABITS)
                {
                    TimeSpan ts = TimeSpan.Parse(appSettings.sDayShutOffTime);
                    return new DateTime(now.Year, now.Month, now.Day, ts.Hours, ts.Minutes, 0);
                }
                // Evening habits (or evening focus summary) → next day's morning start.
                TimeSpan tsm = TimeSpan.Parse(appSettings.sDayStartTime);
                DateTime tm = now.AddDays(1);
                return new DateTime(tm.Year, tm.Month, tm.Day, tsm.Hours, tsm.Minutes, 0);
            }
            catch { return DateTime.MaxValue; }   // unparseable times → don't let the guard reject
        }

        // Persist the current postpone (absolute end time + routine type + after-cutoff flag) so a restart
        // can restore it. Cleared at every resume/finish chokepoint.
        internal void PersistHabitsPostpone(DateTime endTime, bool bIsFocusSummary)
        {
            appSettings.HabitsPostponeEndTime = endTime;
            appSettings.HabitsPostponeActivityType = appSettings.nCurrentActivityType;
            appSettings.bHabitsPostponeWasFocusSummary = bIsFocusSummary;
            RegistryManager.SetHabitsPostponeEndTime();
            LogWriter.LogWrite("PersistHabitsPostpone - end " + endTime + " type " + appSettings.nCurrentActivityType);
        }

        // Cross-device finish guard: on restart the routine may have been completed on another device, in
        // which case the postpone should not be restored. Returns false when none of today's habits for the
        // routine remain (all done/skipped per the fetched activity props / backend). Uncertain → true (keep).
        private bool RoutineHasRemainingHabits(CurrentActivityType type)
        {
            try
            {
                var globalArr = (type == CurrentActivityType.EVENING_HABITS)
                    ? appSettings.arrGLOBALAllEveningHabits : appSettings.arrGLOBALAllMorningHabits;
                if (globalArr == null) return true;
                string todayKey = DateTime.Now.ToString("yyyy-MM-dd");
                string dow3 = DateTime.Now.DayOfWeek.ToString().Substring(0, 3).ToLower();
                int scheduled = 0, remaining = 0;
                foreach (var h in globalArr)
                {
                    bool forToday = h.DaysOfWeek != null && h.DaysOfWeek.Any(d => { string dl = (d ?? "").ToLower(); return dl == "all" || dl == dow3; });
                    if (!forToday) continue;
                    scheduled++;
                    if (string.IsNullOrEmpty(h.id) || !Focus_Bear.Habits.DayPlan.DayPlanDataSource.IsHabitDoneOrSkipped(h.id, todayKey))
                        remaining++;
                }
                LogWriter.LogWrite("RoutineHasRemainingHabits(" + type + ") scheduled=" + scheduled + " remaining=" + remaining);
                if (scheduled == 0) return false;   // nothing scheduled today for this routine
                return remaining > 0;
            }
            catch (Exception ex) { LogWriter.LogWrite("RoutineHasRemainingHabits ex - " + ex.Message); return true; }
        }

        // Reliable "this routine is already finished for today" check — mirrors the last_completed_sequence
        // logic in ShowMorningRoutine/ShowEveningRoutine. Needed because once a routine is completed (e.g. on
        // another device) the backend starts a fresh sequence, so current_sequence_completed_activities resets
        // to empty and the per-habit remaining check alone would wrongly think the routine is still pending.
        // Assumes PrepareRoutineDataForPostponeRestore already fetched sLastActivityDoneInfo + today's habit list.
        private bool RoutineAlreadyCompletedToday(CurrentActivityType type)
        {
            try
            {
                if (string.IsNullOrEmpty(appSettings.sLastActivityDoneInfo)) return false;
                var arr = (type == CurrentActivityType.EVENING_HABITS) ? appSettings.arrAllEveningHabits : appSettings.arrAllMorningHabits;
                if (arr == null || arr.Count == 0) return false;
                string sequenceID = arr[0].activity_sequence_id;
                if (string.IsNullOrEmpty(sequenceID)) return false;

                JObject info = JObject.Parse(appSettings.sLastActivityDoneInfo);
                var lcs = info["last_completed_sequence"];
                if (lcs == null || lcs.Type == JTokenType.Null || lcs["id"] == null) return false;
                if (lcs["id"].ToString() != sequenceID) return false;

                string startedAt = info["last_completed_sequence_started_at"] != null ? info["last_completed_sequence_started_at"].ToString() : "";
                if (startedAt.Length == 0) return false;
                DateTime startedAtDate = DateTime.Parse(startedAt);

                DateTime now = DateTime.Now;
                TimeSpan ts = TimeSpan.Parse(type == CurrentActivityType.EVENING_HABITS ? appSettings.sDayShutOffTime : appSettings.sDayStartTime);
                DateTime routineStart = new DateTime(now.Year, now.Month, now.Day, ts.Hours, ts.Minutes, 0).ToUniversalTime();
                if (now.ToUniversalTime() < routineStart) routineStart = routineStart.AddDays(-1);

                bool done = startedAtDate > routineStart;
                LogWriter.LogWrite("RoutineAlreadyCompletedToday(" + type + ") = " + done + " (startedAt " + startedAtDate + " vs " + routineStart + ")");
                return done;
            }
            catch (Exception ex) { LogWriter.LogWrite("RoutineAlreadyCompletedToday ex - " + ex.Message); return false; }
        }

        // Re-arm the habits postpone timer for the remaining time when restoring a persisted postpone.
        private void RestoreHabitsPostponeTimer(TimeSpan remaining)
        {
            try
            {
                dispatcherTimerPostponeHabits.Stop();
                var eventField = dispatcherTimerPostponeHabits.GetType().GetField("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
                var eventDelegate = (Delegate)eventField.GetValue(dispatcherTimerPostponeHabits);
                if (eventDelegate != null)
                    foreach (var handler in eventDelegate.GetInvocationList())
                        dispatcherTimerPostponeHabits.Tick -= ((EventHandler)handler);
            }
            catch (Exception ex) { LogWriter.LogWrite("RestoreHabitsPostponeTimer clear ex - " + ex.Message); }

            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            dispatcherTimerPostponeHabits.Tick += new EventHandler(dispatcherTimer_Postpone);
            dispatcherTimerPostponeHabits.Interval = remaining;
            dispatcherTimerPostponeHabits.Start();

            if (remaining.TotalMinutes > 2)
                SetShowingYourHabitsResumeInPopup((int)remaining.TotalMinutes - 2);
        }

        // Before restoring a persisted postpone we bypass ShowMorningRoutine/ShowEveningRoutine, so their
        // fetch of the routine's completed activities never runs. Do the minimum here: build today's filtered
        // habit list (so the Day Plan has something to show) and fetch the current sequence's completed /
        // skipped activities (done on this or another device) into arrayCompletedActivities / arraySkipped,
        // so IsHabitDoneOrSkipped — and therefore the Day Plan display AND the cross-device remaining check —
        // reflect them.
        private async Task PrepareRoutineDataForPostponeRestore(CurrentActivityType type)
        {
            try
            {
                string dow3 = DateTime.Now.DayOfWeek.ToString().Substring(0, 3).ToLower();
                var globalArr = (type == CurrentActivityType.EVENING_HABITS) ? appSettings.arrGLOBALAllEveningHabits : appSettings.arrGLOBALAllMorningHabits;
                var todayArr = (type == CurrentActivityType.EVENING_HABITS) ? appSettings.arrAllEveningHabits : appSettings.arrAllMorningHabits;
                todayArr.Clear();
                if (globalArr != null)
                    foreach (var h in globalArr)
                        if (h.DaysOfWeek != null && h.DaysOfWeek.Any(d => { string dl = (d ?? "").ToLower(); return dl == "all" || dl == dow3; }))
                            todayArr.Add(h);

                FocusBearAPIClient client = new FocusBearAPIClient();
                await client.GetLastActivityInfo();
                appSettings.arrayCompletedActivities.Clear();
                appSettings.arraySkipped.Clear();
                if (!string.IsNullOrEmpty(appSettings.sLastActivityDoneInfo))
                {
                    JObject info = JObject.Parse(appSettings.sLastActivityDoneInfo);
                    if (info["current_sequence_completed_activities"] != null && info["current_sequence_completed_activities"].ToString().Length > 0)
                        appSettings.arrayCompletedActivities = (JArray)info["current_sequence_completed_activities"];
                    if (info["current_sequence_skipped_activities"] != null && info["current_sequence_skipped_activities"].ToString().Length > 0)
                        appSettings.arraySkipped = (JArray)info["current_sequence_skipped_activities"];
                }
                LogWriter.LogWrite("PrepareRoutineDataForPostponeRestore - today=" + todayArr.Count + " completed=" + appSettings.arrayCompletedActivities.Count + " skipped=" + appSettings.arraySkipped.Count);
            }
            catch (Exception ex) { LogWriter.LogWrite("PrepareRoutineDataForPostponeRestore ex - " + ex.Message); }
        }

        public void PostponeHabitWork(int nMinutesToPostpone, bool bForClassSession = false)
        {
            LogWriter.LogWrite("PostponeHabitWork()");

            try
            {
                //It might be possible that habits are already postponed and user is trying to postpone again from the 2 min popup that appears before 2 mins of resumin habits
                //fix for: Postponing evening routine ends active focus session and still prompts routine #984
                if (appSettings.bHabitsPostponedState)
                {
                    LogWriter.LogWrite("PostponeHabitWork Timer extended for minutes from now - " + nMinutesToPostpone.ToString());
                    //If any old timer for postpone is running, we need to stop that add minutes from now and start it back.
                    dispatcherTimerPostponeHabits.Stop();
                    dispatcherTimerPostponeHabits.Interval = TimeSpan.FromMinutes(nMinutesToPostpone);
                    dispatcherTimerPostponeHabits.Start();

                    // Re-persist the new end time so a restart restores the extended postpone (keeps the
                    // existing routine type + flags already stored on the first postpone).
                    appSettings.HabitsPostponeEndTime = DateTime.Now.AddMinutes(nMinutesToPostpone);
                    RegistryManager.SetHabitsPostponeEndTime();

                    if (nMinutesToPostpone > 2 && !bForClassSession) //If the postpone time is more than 2 minutes, we need to show a popup again before the postpone time is over and the habits are resumed.
                    {
                        SetShowingYourHabitsResumeInPopup(nMinutesToPostpone - 2);
                    }
                    return;
                }

                appSettings.RemoveSecondaryWindows();

                //If any old timer for postpone is running, we need to stop that
                dispatcherTimerPostponeHabits.Stop();

                //Start the timer for the postpone duration
                //Remove the previous event handler so that new can be set
                try
                {
                    var eventField = dispatcherTimerPostponeHabits.GetType().GetField("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
                    var eventDelegate = (Delegate)eventField.GetValue(dispatcherTimerPostponeHabits);
                    if (eventDelegate != null)
                    {
                        var invocatationList = eventDelegate.GetInvocationList();

                        foreach (var handler in invocatationList)
                            dispatcherTimerPostponeHabits.Tick -= ((EventHandler)handler);
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("PostponeHabitWork remove timer event ex - " + ex.Message);
                }

                try
                {
                    dispatcherTimerPostponeHabits.Tick += new EventHandler(dispatcherTimer_Postpone);
#if DEBUG
                    dispatcherTimerPostponeHabits.Interval = new TimeSpan(0, 5, nMinutesToPostpone);
#else
                    dispatcherTimerPostponeHabits.Interval = new TimeSpan(0,  nMinutesToPostpone, 0);
#endif
                    dispatcherTimerPostponeHabits.Start();

                    appSettings.bHabitsPostponedState = true;
                    appSettings.bHabitwithAllowedAppsRunning = false; //Reset
                    appSettings.bHabitwithUseFocusModeRunning = false;
                    appSettings.bHabitwithUserActionNonTimerRunning = false;
                    
                    wndFocusBearMainWindow.EnableAllButtons();//Enable all the left menu buttons

                    if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                    {
                        if (appSettings.DidCutOffTimePassed())
                        {
                            appSettings.bEveningHabitsPostponedAfterCutoffTime = true;
                        }
                    }

                    // Persist the postpone so it survives an app restart (restored in StartWorkFlow, capped by
                    // the routine-window guard). Done here while nCurrentActivityType is still the routine type
                    // (it may be set to NONE just below when a focus session is running).
                    PersistHabitsPostpone(DateTime.Now.AddMinutes(nMinutesToPostpone), false);

                    // If a focus session exists (paused or running), restore UI after postponing
                    if (appSettings.isFocusModeRunning)
                    {
                        //Teacher Mode keeps the routine's activity type (MORNING/EVENING_HABITS) so it is
                        //merely paused for the session and resumes on its own when the postpone timer fires.
                        if (!bForClassSession)
                        {
                            appSettings.nCurrentActivityType = CurrentActivityType.NONE;
                        }
                        RestoreFocusUIAfterRoutine();
                    }

                    // If user opted in, auto-close the window. Otherwise show appropriate view for mode.
                    if (!appSettings.isFocusModeRunning && appSettings.AutoCloseAfterRoutineSkipPostpone)
                    {
                        wndFocusBearMainWindow.Hide();
                    }
                    else if (!appSettings.isFocusModeRunning)
                    {
                        if (appSettings.bIsGeekModeEnabled)
                        {
                            ShowPomodoroWindow();
                        }
                        else
                        {
                            wndFocusBearMainWindow.PageHabits.ShowHabitsPostponedView();
                        }
                    }


                    LogWriter.LogWrite("appSettings.bHabitsPostponedState = true;");

                    CommandManager.InvalidateRequerySuggested();
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("dispatcherTimerPostponeHabits ex - " + ex.Message);
                }

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PostponeHabitWork - " + ex.Message);
            }

            if (nMinutesToPostpone > 2 && !bForClassSession) //If the postpone time is more than 2 minutes, we need to show a popup bfore the postpone time is over and the habits are resumed.
            {
                SetShowingYourHabitsResumeInPopup(nMinutesToPostpone - 2);
            }
        }

        //Teacher Mode - a class session can arrive while the student is in the middle of their morning
        //(or evening) routine. When they join, push the routine aside so the locked class session can
        //take over the screen, exactly as if they had postponed it themselves. The routine is postponed
        //for the whole session (class duration + 1 min, from the backend's scheduled_finish_time) with
        //its activity type kept as MORNING/EVENING_HABITS, so it resumes on its own ~1 min after the
        //scheduled class end. ShowMorningRoutine is also guarded to stay away while the session is active.
        //Returns true if it actually postponed a running routine (so the caller knows to resume it when
        //the class session ends). Returns false when no routine was in progress, or on error.
        public bool PostponeHabitsForClassSession(int nSessionMinutes)
        {
            try
            {
                if (appSettings.nCurrentActivityType != CurrentActivityType.MORNING_HABITS &&
                    appSettings.nCurrentActivityType != CurrentActivityType.EVENING_HABITS)
                {
                    return false; //no routine in progress, nothing to postpone
                }
                LogWriter.LogWrite("PostponeHabitsForClassSession() - postponing the running routine for the class session");

                //pause the running habit activity exactly like a manual postpone: HandleTimerForPostpone(true)
                //stops the timer, SAVES the remaining time (RegistryManager.SetPostponedHabitData) so the
                //routine resumes this activity from where it was left instead of from the start, and removes
                //the allowed-app top-right popup (wndTakeABreak) + its timer. Same call the manual
                //postpone / sleep-pause paths use.
                try
                {
                    if (wndFocusBearMainWindow != null && wndFocusBearMainWindow.PageHabits != null &&
                        wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing != null)
                    {
                        wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing.HandleTimerForPostpone(true);
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("PostponeHabitsForClassSession HandleTimerForPostpone ex - " + ex.Message);
                }

                //postpone for the class duration + 1 min so the routine resumes just after the class ends.
                //isFocusModeRunning is already true here (the caller started the class focus session first),
                //so PostponeHabitWork restores the focus UI; bForClassSession keeps the activity type and
                //suppresses the "habits resume in 2 minutes" popup that would otherwise show mid-session.
                int nMinutesToPostpone = nSessionMinutes + 1;
                if (nMinutesToPostpone < 1)
                {
                    nMinutesToPostpone = 1;
                }
                PostponeHabitWork(nMinutesToPostpone, bForClassSession: true);
                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PostponeHabitsForClassSession ex - " + ex.Message);
            }
            return false;
        }

        //Teacher Mode - called from TeacherSessionFinished when the routine was auto-postponed for the
        //class session, so it resumes as soon as the session ends (rather than waiting out the class
        //duration + 1 min postpone timer, which stays only as a fallback). Clears the postpone state and
        //lets the background loop re-show the routine on its next tick (the teacher session is already
        //cleared by the time this runs, so ShowMorningRoutine's guard no longer blocks it). Deliberately
        //does NOT call RestoreFocusUIAfterRoutine: the class focus session has just been torn down, so
        //there is no focus UI to restore.
        public void ResumeHabitsAfterClassSession()
        {
            LogWriter.LogWrite("ResumeHabitsAfterClassSession()");
            try
            {
                dispatcherTimerPostponeHabits.Stop();
                dispatcherTimerResumeHabitsAfterPostpone.Stop();
                if (wndHabitsResumeInPopup != null)
                {
                    wndHabitsResumeInPopup.HideHabitsResumeInPopupAndStopTimer();
                }
                appSettings.bHabitsPostponedState = false;
                appSettings.bEveningHabitsPostponedAfterCutoffTime = false;
                appSettings.nCurrentActivityType = CurrentActivityType.NONE;
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ResumeHabitsAfterClassSession ex - " + ex.Message);
            }
        }

        public void SetShowingYourHabitsResumeInPopup(int nMinutesBeforeResume)
        {
            LogWriter.LogWrite("SetShowingYourHabitsResumeInPopup()");
            if (!appSettings.bHabitsPostponedState)
                return;

            try
            {
                //Start the timer for the postpone duration
                //Remove the previous event handler so that new can be set
                try
                {
                    var eventField = dispatcherTimerResumeHabitsAfterPostpone.GetType().GetField("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
                    var eventDelegate = (Delegate)eventField.GetValue(dispatcherTimerResumeHabitsAfterPostpone);
                    if (eventDelegate != null)
                    {
                        var invocatationList = eventDelegate.GetInvocationList();

                        foreach (var handler in invocatationList)
                            dispatcherTimerResumeHabitsAfterPostpone.Tick -= ((EventHandler)handler);
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("dispatcherTimerResumeHabitsAfterPostpone remove timer event ex - " + ex.Message);
                }

                try
                {
                    dispatcherTimerResumeHabitsAfterPostpone.Tick += new EventHandler(dispatcherTimer_ResumeHabitsPopupBefore2Minutes);
#if DEBUG
                    dispatcherTimerResumeHabitsAfterPostpone.Interval = new TimeSpan(0, 0, nMinutesBeforeResume);
#else
                    dispatcherTimerResumeHabitsAfterPostpone.Interval = new TimeSpan(0, nMinutesBeforeResume, 0);
#endif
                    dispatcherTimerResumeHabitsAfterPostpone.Start();

                    CommandManager.InvalidateRequerySuggested();
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("dispatcherTimerResumeHabitsAfterPostpone ex - " + ex.Message);
                }

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetShowingYourHabitsResumeInPopup - " + ex.Message);
            }

        }

        private void dispatcherTimer_ResumeHabitsPopupBefore2Minutes(object sender, EventArgs e)
        {
            LogWriter.LogWrite("dispatcherTimer_ResumeHabitsPopupBefore2Minutes()");
            try
            {
                dispatcherTimerResumeHabitsAfterPostpone.Stop(); //Tick Once only
                //Show the resume habits in popup
                if (wndHabitsResumeInPopup == null)
                {
                    wndHabitsResumeInPopup = new HabitsResumeInPopup();
                }
                wndHabitsResumeInPopup.Show();
                wndHabitsResumeInPopup.ShowHabitsResumeInPopupAndStartTimer(120);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("dispatcherTimer_ResumeHabitsPopupBefore2Minutes Ex - " + ex.Message);
            }
        }

          
        private void dispatcherTimer_Postpone(object sender, EventArgs e)
        {
            LogWriter.LogWrite("dispatcherTimer_Postpone()");
            try
            {
                ResumeAfterPostponeFinished();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("dispatcherTimer_Postpone Exception" + ex.Message);
            }
        }


        public void ResumeAfterPostponeFinished()
        {
            LogWriter.LogWrite("ResumeAfterPostponeFinished()");

            try
            {
                dispatcherTimerPostponeHabits.Stop(); //Tick Once only stop the timer

                //we also need to stop the timer for showing the 2 minutes resume habits in popup
                dispatcherTimerResumeHabitsAfterPostpone.Stop();

                if (wndHabitsResumeInPopup != null)
                {
                    wndHabitsResumeInPopup.HideHabitsResumeInPopupAndStopTimer(); //hide the resume habits in popup
                }

                CommandManager.InvalidateRequerySuggested();

                appSettings.bHabitsPostponedState = false;
                appSettings.bEveningHabitsPostponedAfterCutoffTime = false; //reset
                RegistryManager.RemoveHabitsPostponeEndTime(); //postpone consumed - don't restore it on restart
                LogWriter.LogWrite(" appSettings.bHabitsPostponedState = false;");

                //Simply set the activity type to NONE and the background thread will take care.
                appSettings.nCurrentActivityType = CurrentActivityType.NONE;
                LogWriter.LogWrite(" appSettings.nCurrentActivityType = CurrentActivityType.NONE;");

                RestoreFocusUIAfterRoutine();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PostponeHabitWork - " + ex.Message);
            }

        }

        /// <summary>
        /// Resume a postponed routine because the user is starting a habit from the Day Plan: stop the
        /// postpone timers and clear the postponed state, then re-enter routine mode (tabs disabled).
        /// Unlike <see cref="ResumeAfterPostponeFinished"/> this does NOT end the routine (no NONE /
        /// non-routine UI) — the caller starts the selected habit right after.
        /// </summary>
        public void ResumeHabitsForDayPlanPlay()
        {
            LogWriter.LogWrite("ResumeHabitsForDayPlanPlay()");
            try
            {
                dispatcherTimerPostponeHabits.Stop();
                dispatcherTimerResumeHabitsAfterPostpone.Stop();
                if (wndHabitsResumeInPopup != null)
                {
                    wndHabitsResumeInPopup.HideHabitsResumeInPopupAndStopTimer();
                }
                appSettings.bHabitsPostponedState = false;
                appSettings.bEveningHabitsPostponedAfterCutoffTime = false;
                RegistryManager.RemoveHabitsPostponeEndTime(); //postpone resumed via Day Plan play - don't restore on restart

                // Evening focus-summary postpone: playing a habit from the Day Plan must also clear this
                // state and stop the focus-summary's OWN postpone timer — otherwise it later fires and
                // forces the routine to NONE mid-habit, and the tray keeps showing "Resume Habits" (#1365).
                if (appSettings.bFocusSummaryPostponedState)
                {
                    appSettings.bFocusSummaryPostponedState = false;
                    try { wndFocusBearMainWindow?.PageFocusSummary?.StopPostponeTimerForDayPlanPlay(); } catch { }
                }
                // Refresh the tray so "Resume Habits" is hidden now that a habit is actively playing.
                try { wndTrayMenu?.UpdateTrayMenu(); } catch { }

                if (wndFocusBearMainWindow != null)
                {
                    wndFocusBearMainWindow.DisableAllButtons(LeftButtonType.Habits);
                    wndFocusBearMainWindow.CollapseSideBar();   // routine mode collapses the side bar

                    // Enter the running-habit window chrome: hide the minimize/maximize buttons (leaving
                    // only Close) and keep it topmost, matching the normal routine state. Clearing the
                    // postponed flags above isn't enough — the chrome is otherwise only (re)applied by
                    // BringMainWindowToFront (which gates it on "not postponed"), and that isn't called on
                    // this path, so the window kept showing all three (min/max/close) buttons after playing
                    // a habit from a postponed Day Plan (#1365).
                    wndFocusBearMainWindow.SetMinimizeButtonEnabled(false);
                    wndFocusBearMainWindow.SetMaximizeButtonEnabled(false);
                    wndFocusBearMainWindow.Topmost = true;
                    if (appSettings.bPlayActivitiesInFullScreenMode)
                    {
                        wndFocusBearMainWindow.WindowStyle = WindowStyle.None;
                        wndFocusBearMainWindow.WindowState = WindowState.Maximized;
                        wndFocusBearMainWindow.ResizeMode = ResizeMode.NoResize;
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ResumeHabitsForDayPlanPlay ex - " + ex.Message);
            }
        }
        public void EasyPostponeCancelled()
        {
            LogWriter.LogWrite("EasyPostponeCancelled()");
            try
            {
                if (wndFocusBearMainWindow != null)
                {
                    wndFocusBearMainWindow.PageHabits.EasyPostponeCancelled(); //enable the timers

                    ShowBlankWindowOnOtherScreens();
                }
                
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("EasyPostponeCancelled() ex - " + ex.Message);
            }

        }
        public void SimulatePleaseSpareMe()
        {
            LogWriter.LogWrite("SimulatePleaseSpareMe()");
            try
            {
                //Just make sure that we have the habits time or breaks time
                if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                {
                    //Show habits view it it was elsewhere
                    if (!wndFocusBearMainWindow.IsLeafSelected(LeftButtonType.MicroBreaks))
                        wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.MicroBreaks);
                    wndFocusBearMainWindow.PageHabits.SimulatePleaseSpareMeClick();
                }
                else if ((appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS || appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS) &&
                    !appSettings.bHabitsPostponedState && !appSettings.bFocusSummaryPostponedState)
                {
                    if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS && wndFocusSummary != null && wndFocusSummary.IsVisible)
                    {
                        wndFocusSummary.SimulateNeedOneMinute_Click();
                    }
                    else 
                    {
                      
                        //Show habits view it it was elsewhere
                        if (!wndFocusBearMainWindow.IsLeafSelected(LeftButtonType.Habits))
                            wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.Habits);
                        
                        wndFocusBearMainWindow.PageHabits.SimulatePleaseSpareMeClick();
                    }
                    
                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.SHUT_OFF)
                {
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(wndFocusBearMainWindow.PageShutOffWindow.btnAction);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else if(appSettings.nCurrentActivityType == CurrentActivityType.ONBOARDING)
                {
                    wndFocusBearMainWindow.Close();
                }
                else if(!appSettings.IsLoggedIn)
                {
                    appSettings.bRestartAppNeeded = false;

                    Application.Current.Shutdown(0);
                }
                else
                {
                    wndFocusBearMainWindow.Hide();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("APP SimulatePleaseSpareMe() ex - " + ex.Message);
            }
        }
        public void ShowFocusSummaryWindow()
        {
            LogWriter.LogWrite("ShowFocusSummaryWindow()");
            try
            {
                wndFocusBearMainWindow.GridShade.Visibility = Visibility.Collapsed;//caution break window's initiated

                ShowBlankWindowOnOtherScreens();

                ShowHabitsSectionInMainWindow();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowFocusSummaryWindow() ex - " + ex.Message);
            }
        }
        public async void CheckForUpdate(bool isCheckedManually)
        {
            LogWriter.LogWrite("CheckForUpdate()");

            await Task.Factory.StartNew(async () =>
            {
                try
                {
                    //check for App update
                    var updater = new AppUpdater();



                    var status = await updater.CheckForUpdate();
                    if (status)
                    {
                        await Dispatcher.BeginInvoke((Action)(() =>
                        {
                            //Waiting if existing dialoge is showing
                            LogWriter.LogWrite("A new version of Focus Bear is available");
                            {
                                string displayMsg = appSettings.ConfigFileStrings["WindowsSpecific"]["newVersionAvailable"].ToString(); //"A new version of Focus Bear is available. Do you wish to update to the latest version of Focus Bear?";
                                FBMessageBox ff = new FBMessageBox(displayMsg, appSettings.ConfigFileStrings["WindowsSpecific"]["newUpdateAvailable"].ToString(), MessageBoxButton.YesNo);
                                ff.ShowDialog();
                                //if user has cancelled the update process
                                if (ff.btnClicked == MessageBoxResult.No)
                                {
                                    return;
                                }
                            }

                            UpdateWin winUpdate = new UpdateWin(ref updater);
                            winUpdate.Topmost = true;
                            winUpdate.Show();
                            winUpdate.Activate();
                            winUpdate.Focus();
                        }));
                    }
                    else
                    {
                        if (isCheckedManually)
                        {
                            await Dispatcher.BeginInvoke((Action)(() =>
                            {
                                string displayMsg = appSettings.ConfigFileStrings["WindowsSpecific"]["alreadyRunningLatestVersion"].ToString();//"You are already running the latest version of Focus Bear.";  
                                FBMessageBox ff = new FBMessageBox(displayMsg, appSettings.ConfigFileStrings["WindowsSpecific"]["noNewUpdateAvailable"].ToString());
                                ff.ShowDialog();

                                //if user has cancelled the update process
                                if (ff.btnClicked == MessageBoxResult.Cancel)
                                {
                                    return;
                                }
                            }));
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            });
        }

        public void ShowBrainDumpWindow()
        {
            LogWriter.LogWrite("ShowBrainDumpWindow()");

            BringMainWindowToFront();

            wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.BrainDump);
            wndFocusBearMainWindow.PageBrainDump.ShowBrainDumpWindowAndStartTimer();
        }
        public void ShowFocusProgressWindow()
        {
            try
            {
                if (wndFocusProgress == null)
                {
                    wndFocusProgress = new FocusProgress();
                }
                
                wndFocusProgress.StartTimer(int.Parse(appSettings.sRunningFocusModeMinutes) * 60);
                wndFocusProgress.Show();
                wndFocusProgress.BringIntoView();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowFocusProgressWindow" + ex.Message);
            }
        }
        public void CloseFocusProgressWindow()
        {
            LogWriter.LogWrite("CloseFocusProgressWindow()");

            if (wndFocusProgress != null)
            {
                wndFocusProgress.Close();
                wndFocusProgress = null;
            }
        }

        private void PauseFocusMusicForRoutine()
        {
            try
            {
                var track = appSettings.arrFocusMusicTracks?.FirstOrDefault(t => t.IsPlaying);
                if (track == null) return;
                wndFocusBearMainWindow?.PageFocusMusic?.PauseTrack(track);
                appSettings.bFocusMusicPausedAutomatically = true;
                wndTrayMenu?.UpdateTrayMenu();
                LogWriter.LogWrite("PauseFocusMusicForRoutine() - paused: " + track.Name);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PauseFocusMusicForRoutine() - " + ex.Message);
            }
        }

        public void ResumeFocusMusicAfterRoutine()
        {
            try
            {
                //Only resume music that WE auto-paused, not music the user paused manually
                if (!appSettings.bFocusMusicPausedAutomatically) return;
                appSettings.bFocusMusicPausedAutomatically = false;

                var track = appSettings.arrFocusMusicTracks?.FirstOrDefault(t => t.IsPaused);
                if (track == null) return;
                wndFocusBearMainWindow?.PageFocusMusic?.ResumeTrack(track);
                wndTrayMenu?.UpdateTrayMenu();
                LogWriter.LogWrite("ResumeFocusMusicAfterRoutine() - resumed: " + track.Name);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ResumeFocusMusicAfterRoutine() - " + ex.Message);
            }
        }

        public void HideFocusUIForRoutine()
        {
            // Hide contronls during routine
            PauseFocusMusicForRoutine();
            wndTrayMenu?.UpdateTrayMenu();
            HideToDoPlayer();
            wndFocusProgress?.Hide();
        }

        public void RestoreFocusUIAfterRoutine()
        {
            // Restore controls after routine
            ResumeFocusMusicAfterRoutine();
            wndTrayMenu?.UpdateTrayMenu();
            wndFocusProgress?.UpdatePauseControls();
            wndFocusProgress?.Show();
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            LogWriter.LogWrite("Application_Exit()");

            //Teacher Mode - best-effort final {in_focus:false} heartbeat so the teacher's roster
            //shows the student as out of focus right away (the server also infers quit from the
            //heartbeat gap). Synchronous with a 2 second cap because the process is terminating.
            try
            {
                TeacherSessionManager.Instance.SendFinalHeartbeatBeforeExit();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Application_Exit SendFinalHeartbeatBeforeExit ex - " + ex.Message);
            }

            try
            {
                LogWriter.LogWrite("Application_Exit() 1");

                RegistryManager.SetTotalTimeForBlockingActivated();
                RegistryManager.SetTotalTimeForBlockingDeactivated();
                RegistryManager.SetTotalDistractingSiteBlockedToday();

                appSettings.csvFocusProductivityStats.Dispose();
                appSettings.csvUsageTimingsExplanation.Dispose();
                appSettings.csvfocusAIDecisions.Dispose();

                if (trayIcon != null)
                {
                    trayIcon.Dispose();
                }
                // We dispose of the control so the internal WebView objects are released
                // and the associated browser process exits.
                if (wndFocusBearMainWindow != null)
                {
                    if (wndFocusBearMainWindow.PageEditHabits != null)
                    {
                        wndFocusBearMainWindow.PageEditHabits.webView.Dispose();
                    }
                }
                LogWriter.LogWrite("Application_Exit() 2");

                if(createdNew && appMutex != null)
                    appMutex.ReleaseMutex();
                // appMutex.Dispose();
                //Remove the focus handler
                //Commented in 1.0.50 as this was taking full 1 minute and was slowing down the exit and restart process after saving settings
                /**if (appSettings.handler != null)
                    appSettings.automation.RemoveFocusChangedEventHandler(appSettings.handler);**/
                LogWriter.LogWrite("Application_Exit() 3");

                //Remove the hook
                if (m_hhook != IntPtr.Zero)
                    UnhookWinEvent(m_hhook);
                LogWriter.LogWrite("Application_Exit() 4");

                // Unsubscribe from the events when you're done
                SystemEvents.PowerModeChanged -= OnPowerChange;
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;


                try
                {
                    if (wndFocusBearMainWindow.PageToDoPlayer != null)
                    {
                        wndFocusBearMainWindow.PageToDoPlayer.webView.Dispose();
                    }
                    if (wndFocusBearMainWindow.PageToDoList != null)
                    {
                        wndFocusBearMainWindow.PageToDoList.webView.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("Todo Player webView.Dispose(); - " + ex.Message);
                }
                
                FocusButton.Stop();

                LogWriter.LogWrite("Application_Exit() done");

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Application_Exit() EX - " + ex.Message);
            }
            if (appSettings.bRestartAppNeeded)
            {
                string currentAppPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (appSettings.bRestartAppNeededAfterDeactivating)
                {
                    Process.Start(currentAppPath, "/afterdeactivate");
                }
                else
                {
                    Process.Start(currentAppPath, "/restartbyself");
                }
                LogWriter.LogWrite(" Process.Start FIRED!");
            }
        }

        public void ShowBlankWindowOnOtherScreens()
        {
            if (!appSettings.bPlayActivitiesInFullScreenMode)
            {
                return;
            }

            LogWriter.LogWrite("ShowBlankWindowOnOtherScreens()");
            //WE will place a small check here
            //if its not any activity time, this should not execute
            if (appSettings.nCurrentActivityType == CurrentActivityType.NONE)
            {
                LogWriter.LogWrite("return from ShowBlankWindowOnOtherScreens() as appSettings.nCurrentActivityType == CurrentActivityType.NONE");
                appSettings.RemoveSecondaryWindows();
                return;
            }

            /*screen class see line 120, Screen.AllScreens uses the field Screen.screens for cache. In my solution, I use the reflection api to change the Screen class. I clear Screens.screens before calling Screen.AllScreens.*/
            typeof(WinForms.Screen).GetField("screens", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).SetValue(null, null);


            foreach (WinForms.Screen s in WinForms.Screen.AllScreens)
            {
                if (s != WinForms.Screen.PrimaryScreen) // Blackout only the secondary screens
                {
                    BlankWindow bb = new BlankWindow();
                    bb.WindowStartupLocation = WindowStartupLocation.Manual;
                    bb.Left = s.Bounds.Left;
                    bb.Top = s.Bounds.Top;
                    bb.Width = s.Bounds.Width;
                    bb.Height = s.Bounds.Height;
                    bb.Show();
                    SetWindowPos(new WindowInteropHelper(bb).Handle, IntPtr.Zero, s.Bounds.Left, s.Bounds.Top, s.Bounds.Width, s.Bounds.Height, 0x0040);
                    appSettings.SecondaryWindows.Add(bb);
                }
            }
        }

        void ResumeHabitsAfterSystemWakeup()
        {
            LogWriter.LogWrite("ResumeHabitsAfterSystemWakeup()");
            //We will call the same function as we do when it resumes from postpone.
            //The reason is that it might be possible that the system went to sleep when it was the postpone event
            //or also when the resume in 2 minutes was being shown
            //this function will handle all.

            // Don't end an active postpone just because the system woke up — the postpone
            // has its own timer that resumes at the chosen time. Only resume when not postponed.
            if (!appSettings.bHabitsPostponedState)
            {
                ResumeAfterPostponeFinished();
            }

        }
        void PauseHabitsOnSystemSleep()
        {
            LogWriter.LogWrite("PauseHabitsOnSystemSleep()");
            if (wndFocusBearMainWindow != null)
            {
                if (wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing != null) //V2
                {
                    wndFocusBearMainWindow.PageHabits.PageHabitActivityOnGoing.HandleTimerForPostpone(true); //Stop the timer
                }
                LogWriter.LogWrite(" wndHabits.Hide()");
                //mj// todo handling may be main app window
                //mj// wndHabits.Hide();

                appSettings.RemoveSecondaryWindows();
            }


        }
        public void RemoveAllowedAppHabitTimerPopup()
        {
            if (((App)(Application.Current)).wndTakeABreak != null )
            {
                ((App)(Application.Current)).wndTakeABreak.Close();
                ((App)(Application.Current)).wndTakeABreak = null;
            }
        }

        void ValidateSettingsChange(string s_edited_settings_date)
        {
            DateTime d_edited_settings_date = DateTime.Parse(s_edited_settings_date);
            if (d_edited_settings_date > appSettings.dSettingsFetchedTime)
            {
                //User had edited settings after we have feteched
                //Need to alert and ask to restart.
                FBMessageBox msg = new FBMessageBox(appSettings.ConfigFileStrings["common"]["settingsChangedText"].ToString());
                msg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                msg.ShowDialog();

                LogWriter.LogWrite("ValidateSettingsChange - Restart app now for settings to take Effect!");

                appSettings.bRestartAppNeeded = true;

                Application.Current.Shutdown(0);
            }
        }

        public void ShowSurvey()
        {
            if (!RegistryManager.GetSurveyShownStatus())
            {
                //we need to check if 7 days have passed the installed date, if yet, need to who the survey window.
                DateTime now = DateTime.Now;
                TimeSpan difference = now.Subtract(appSettings.FirstAppInstallDT);

                var nDays = difference.Days;

                if (nDays >= 3)
                {
                    SurveyWindow wndSurvey = new SurveyWindow();
                    wndSurvey.Show();
                    RegistryManager.SetSurveyShownStatus();
                }
            }

        }

        public void EnterPomodoroModeAfterMorningRoutine()
        {
            if (!appSettings.bIsGeekModeEnabled)
            {
                return; //return in Simple mode
            }

            LogWriter.LogWrite("EnterPomodoroModeAfterMorningRoutine()");
            //WE will first need to check if automatic hardcode pomodoro is on and then the days are valid
            if (appSettings.bAutomaticUseSuperPomodoroModeSetting)
            {
                if (appSettings.AutomaticSuperPomodoroSelectedDaysArray != null && appSettings.AutomaticSuperPomodoroSelectedDaysArray.Count > 0)
                {
                    DayOfWeek Today = DateTime.Now.DayOfWeek;
                    bool bFound = false;
                    for (int i = 0; i < appSettings.AutomaticSuperPomodoroSelectedDaysArray.Count(); i++)
                    {
                        string sName = appSettings.AutomaticSuperPomodoroSelectedDaysArray[i].ToString();

                        switch (sName)
                        {
                            case "Monday":
                                if (Today == DayOfWeek.Monday)
                                {
                                    bFound = true;
                                }
                                break;
                            case "Tuesday":
                                if (Today == DayOfWeek.Tuesday)
                                {
                                    bFound = true;
                                }
                                break;
                            case "Wednesday":
                                if (Today == DayOfWeek.Wednesday)
                                {
                                    bFound = true;
                                }
                                break;
                            case "Thursday":
                                if (Today == DayOfWeek.Thursday)
                                {
                                    bFound = true;
                                }
                                break;
                            case "Friday":
                                if (Today == DayOfWeek.Friday)
                                {
                                    bFound = true;
                                }
                                break;
                            case "Saturday":
                                if (Today == DayOfWeek.Saturday)
                                {
                                    bFound = true;
                                }
                                break;
                            case "Sunday":
                                if (Today == DayOfWeek.Sunday)
                                {
                                    bFound = true;
                                }
                                break;

                        }
                        if (bFound)
                            break;
                    }
                    if (bFound)
                    {
                        appSettings.CapturePostHog("start-super-pomodoro-mode-automatically");
                        ShowPomodoroWindow(true);
                        return;
                    }
                    else
                        return;
                }
            }

            ShowPomodoroWindow(); //Normal Pomodoro
        }
        public void ShowPomodoroWindow(bool bEnterSuperPomodoroModeAfterMorningRoutine = false)
        {
            LogWriter.LogWrite("ShowPomodoroWindow()");

            if(bEnterSuperPomodoroModeAfterMorningRoutine)
            {
                appSettings.bEnterSuperPomodoroModeAfterMorningRoutine = true;
            }
            else
            {
                appSettings.bEnterSuperPomodoroModeAfterMorningRoutine = false;
            }
            BringMainWindowToFront();

            wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.StartFocusSession);
        }

        public void ShowSimplePomodoroWindow()
        {
            LogWriter.LogWrite("ShowSimplePomodoroWindow()");

            BringMainWindowToFront();

            wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.StartFocusSession);


        }

        private async Task CheckAndResumeRunningFocusMode()
        {
            LogWriter.LogWrite("CheckAndResumeRunningFocusMode()");

            try
            {
                FocusBearAPIClient client = new FocusBearAPIClient();
                var xResponse = await client.GetLastActivityInfo();
                if (xResponse != 1) return;
                if (string.IsNullOrWhiteSpace(appSettings.sLastActivityDoneInfo)) return;

                JObject lastActivityDone = JObject.Parse(appSettings.sLastActivityDoneInfo);

                //Teacher Mode - if the pending focus block belongs to a class session, do NOT
                //resume it as a plain focus session here: TeacherSessionManager.AdoptCurrentSessionOnLaunch
                //adopts it with teacher-lock semantics instead.
                if (lastActivityDone.ContainsKey("completing_focus_block") &&
                    lastActivityDone["completing_focus_block"].Type == JTokenType.Object &&
                    lastActivityDone["completing_focus_block"]["class_session_id"] != null &&
                    lastActivityDone["completing_focus_block"]["class_session_id"].Type != JTokenType.Null)
                {
                    LogWriter.LogWrite("CheckAndResumeRunningFocusMode skipped - pending focus block belongs to a class session");
                    return;
                }

                JToken focusModeToken = lastActivityDone["current_focus_mode"];
                if (focusModeToken == null || focusModeToken.Type != JTokenType.Object) return;

                string sFocusModeId = focusModeToken["id"]?.ToString();
                if (string.IsNullOrEmpty(sFocusModeId)) return;

                string sFinishTime = lastActivityDone["current_focus_mode_finish_time"]?.ToString();
                if (string.IsNullOrEmpty(sFinishTime)) return;

                DateTime finishTimeLocal = DateTime.Parse(sFinishTime).ToLocalTime();
                TimeSpan remaining = finishTimeLocal - DateTime.Now;
                if (remaining.TotalSeconds <= 0)
                {
                    LogWriter.LogWrite("CheckAndResumeRunningFocusMode - session already expired at: " + finishTimeLocal);
                    return;
                }

                LogWriter.LogWrite("CheckAndResumeRunningFocusMode - resuming session " + sFocusModeId + ", remaining seconds: " + (int)remaining.TotalSeconds);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    StartWorkFocusMode(sFocusModeId, (int)remaining.TotalSeconds, false, true);
                });
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("CheckAndResumeRunningFocusMode ex: " + ex.Message);
            }
        }

        public void RestartApp()
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    appSettings.bRestartAppNeeded = true;

                    ((App)(Application.Current)).Shutdown(0);
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("Pusher settings-updated app restart EX = " + ex.Message);
                }
            });

        }

        private async void ConnectGlobalAndMacIDWithDistinctUserID()
        {

            //If UUID is already connected with this user, write and proceed
            if (!RegistryManager.GetGlobalUUIDLinked()) //Specific for each user
            {
                FocusBearAPIClient client = new FocusBearAPIClient();
                var sResponse = await client.PostHogIdentify(appSettings.sGlobalUUID);
                RegistryManager.SetGlobalUUIDLinked();
                RegistryManager.RemoveGlobalUUID();
                appSettings.sGlobalUUID = "";
            }
            //If mac ID is already connected for this user, write and proceed
            if (!RegistryManager.GetMacIDLinked()) //Specific for each user
            {
                FocusBearAPIClient client = new FocusBearAPIClient();
                var sResponse = await client.PostHogIdentify(appSettings.sMacAddress);
                RegistryManager.SetMacIDLinked();
            }

        }

        public void ShowCallFocusNotifier()
        {
            if (wndCallFocusNotifier == null)
            {
                wndCallFocusNotifier = new CallFocusNotifier();
            }
            wndCallFocusNotifier.Show();
            wndCallFocusNotifier.BringIntoView();
            wndCallFocusNotifier.UpdateTexts();

            if (appSettings.isFocusModeRunning ||
                    ((appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS ||
                    appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS ||
                    appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY) &&
                    !appSettings.bHabitsPostponedState))
            {
                //If we are in an activity state or focus mode already running, do not suggest to start meetings focus mode
                return;
            }
            //suggest the meetings focus mode check
            if (appSettings.bAutomaticallySuggestStartingMeetingsFocus)
            {
                if (!bSuggestMeetingsPopupShownForThisCallSession) //flag to show popup only once
                {
                    bSuggestMeetingsPopupShownForThisCallSession = true;
                    FBMessageBox msg = new FBMessageBox(appSettings.ConfigFileStrings["focusInfo"]["suggestFocusInfo"]["title"].ToString() + "\n\n" + appSettings.ConfigFileStrings["focusInfo"]["suggestFocusInfo"]["subTitle"].ToString(), appSettings.ConfigFileStrings["common"]["notificationVideoCallText"].ToString(), MessageBoxButton.YesNoCancel, appSettings.ConfigFileStrings["focusInfo"]["suggestFocusInfo"]["buttonBlockText"].ToString(), appSettings.ConfigFileStrings["focusInfo"]["suggestFocusInfo"]["buttonCancelText"].ToString(), appSettings.ConfigFileStrings["focusInfo"]["suggestFocusInfo"]["buttonFalseDetectionText"].ToString(), true, appSettings.ConfigFileStrings["focusInfo"]["suggestFocusInfo"]["buttonCheckBoxText"].ToString());
                    msg.bSuggestMeetingsFocusModeView = true;
                    msg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    msg.ShowDialog();

                    if (msg.btnClicked == MessageBoxResult.Yes)
                    {
                        // start the meetings focus mode
                        StartMeetingsFocusMode();
                    }
                    else if (msg.btnClicked == MessageBoxResult.Cancel)
                    {
                        //wrong, not in meeting, ask to disable meetings detection
                        FBMessageBox msg1 = new FBMessageBox(appSettings.ConfigFileStrings["focusInfo"]["suggestFocusInfo"]["titleFalseDetection"].ToString(), "", MessageBoxButton.YesNo, appSettings.ConfigFileStrings["focusInfo"]["suggestFocusInfo"]["buttonDisableText"].ToString(), appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString());
                        msg1.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                        msg1.ShowDialog();

                        if (msg1.btnClicked == MessageBoxResult.Yes)
                        {
                            //disable meeting detection clicked
                            appSettings.bEnableMeetingsDetection = false;
                            //Also close the call notifier
                            ((App)(Application.Current)).HideCallFocusNotifier();
                        }
                    }
                }
            }
        }
        public void HideCallFocusNotifier()
        {
            bSuggestMeetingsPopupShownForThisCallSession = false; //reinitiale for next call session
            if (wndCallFocusNotifier != null)
            {
                wndCallFocusNotifier.Hide();
            }
        }


        private void StartMeetingsFocusMode()
        {
            LogWriter.LogWrite("StartMeetingsFocusMode()");

            for (int i = 0; i < appSettings.arrFocusModes.Count(); i++)
            {
                if (appSettings.arrFocusModes[i].isMeetings)
                {
                    int TotalMinutes = 25;
                    int nHours = TotalMinutes / 60;
                    int nMinutes = TotalMinutes % 60;

                    //used when user starts another focus block
                    appSettings.sRunningFocusModeHours = nHours.ToString();
                    appSettings.sRunningFocusModeMinutes = nMinutes.ToString();

                    appSettings.sMinutesBreakPomodoro = "5";
                    appSettings.sMinutesSuperPomodoro = "5";


                    try
                    {
                        Sounds.PlayFocusModeStartSound();
                        //Start the Focus Mode
                        //Change the play icon for this to STOP icon
                        appSettings.arrFocusModes[i].isOn = true;

                        //Make the global variable true
                        appSettings.isFocusModeRunning = true;
                        appSettings.isFocusModePaused = false;

                        ((App)Application.Current).ShowFocusProgressWindow();

                        if (((App)Application.Current).wndBrainDump != null && ((App)Application.Current).wndBrainDump.IsVisible) //Update the window if its made or is visible
                        {
                            ((App)Application.Current).wndBrainDump.ShowBrainDumpWindowAndStartTimer();
                        }

                        //Show and Set on untill value
                        ((App)(Application.Current)).wndTrayMenu.OnUntill.Visibility = Visibility.Visible;
                        // Header auto-sizes in the redesigned tray menu; no manual OrangeHeader height needed.

                        DateTime dTill = DateTime.Now;

                        appSettings.sRunningFocusModeStartTime = dTill.ToString("yyyy-MM-dd HH:mm:ss");
                        appSettings.focusSessionStartTime = DateTime.Now;
                        appSettings.CurrentFocusSessionDuration = 0;

                        dTill = dTill.AddHours(nHours);
                        dTill = dTill.AddMinutes(nMinutes);
                        string sTill = appSettings.ConfigFileStrings["menuInfo"]["untilTimeText"].ToString();
                        sTill = sTill.Replace("<TIME>", dTill.ToString());
                        ((App)(Application.Current)).wndTrayMenu.OnUntill.Content = sTill;

                        appSettings.sRunningFocusModeID = appSettings.arrFocusModes[i].id;
                        appSettings.sRunningFocusModeName = appSettings.arrFocusModes[i].name;
                        appSettings.sRunningFocusModeEndTime = dTill.ToString();
                        //appSettings.sRunningFocusModeIntention = TxtIntention.Text;

                        //Clear any old stuff for gentle blocking
                        appSettings.TemporaryAllowedAppsGentleBlock.Clear();
                        appSettings.TemporarryBlockedAppsGentleBlock.Clear();
                        appSettings.TemporaryAllowedURLsGentleBlock.Clear();
                        appSettings.TemporarryBlockedURLsGentleBlock.Clear();

                        appSettings.TemporaryAllowedKeywords.Clear();
                        appSettings.TemporaryAllowedURLsTabTitles.Clear();
                        //Start the Focus Mode Timer to end after x hours / minutes
                        ((App)(Application.Current)).wndTrayMenu.StartFocusModeTimer(nHours, nMinutes);

                        FocusBearAPIClient client = new FocusBearAPIClient();
                        var xResponse = client.StartFocusMode(appSettings.arrFocusModes[i].id);
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("Start meetings focus mode from suggestion window Exception - " + ex.Message);
                    }
                }
            }
        }

        public void StartWorkFocusMode(string sIDToStart, int SessionTotalSeconds = 1500, bool bCommandFromDevice = false, bool bResumingSession = false)
        {
            LogWriter.LogWrite("StartWorkFocusMode()");

            //We will first look for the id, its index, if that is not found we need to start the first one in the list.
            int nIndexToStart = 0;
            if (sIDToStart.Length != 0)
            {
                for (int i = 0; i < appSettings.arrFocusModes.Count(); i++)
                {
                    if (appSettings.arrFocusModes[i].id == sIDToStart)
                    {
                        //Found it
                        nIndexToStart = i;
                        break;
                    }
                }
            }
 
            try
            {
                int TotalMinutes = SessionTotalSeconds / 60;
                int nHours = TotalMinutes / 60;
                int nMinutes = TotalMinutes % 60;
                int nSeconds = SessionTotalSeconds % 60;

                //used when user starts another focus block
                appSettings.sRunningFocusModeHours = nHours.ToString();
                appSettings.sRunningFocusModeMinutes = nMinutes.ToString();

                appSettings.sMinutesBreakPomodoro = "5";
                appSettings.sMinutesSuperPomodoro = "5";



                Sounds.PlayFocusModeStartSound();
                //Start the Focus Mode
                //Change the play icon for this to STOP icon
                appSettings.arrFocusModes[nIndexToStart].isOn = true;
                appSettings.sMostRecentFocusmodeDoneID = appSettings.arrFocusModes[nIndexToStart].id;

                //Make the global variable true
                appSettings.isFocusModeRunning = true;
                appSettings.isFocusModePaused = false;

                DateTime dTill = DateTime.Now;

                appSettings.sRunningFocusModeStartTime = dTill.ToString("yyyy-MM-dd HH:mm:ss");
                appSettings.focusSessionStartTime = DateTime.Now;
                appSettings.CurrentFocusSessionDuration = 0;

                dTill = dTill.AddHours(nHours);
                dTill = dTill.AddMinutes(nMinutes);
                dTill = dTill.AddSeconds(nSeconds);
                string sTill = appSettings.ConfigFileStrings["menuInfo"]["untilTimeText"].ToString();
                sTill = sTill.Replace("<TIME>", dTill.ToString());
                ((App)(Application.Current)).wndTrayMenu.OnUntill.Content = sTill;

                appSettings.sRunningFocusModeID = appSettings.arrFocusModes[nIndexToStart].id;
                appSettings.sRunningFocusModeName = appSettings.arrFocusModes[nIndexToStart].name;
                appSettings.sRunningFocusModeEndTime = dTill.ToString();

                //Show the progress window after the end time is set so its timer anchors to the exact end time
                ((App)Application.Current).ShowFocusProgressWindow();

                //Clear any old stuff for gentle blocking
                appSettings.TemporaryAllowedAppsGentleBlock.Clear();
                appSettings.TemporarryBlockedAppsGentleBlock.Clear();
                appSettings.TemporaryAllowedURLsGentleBlock.Clear();
                appSettings.TemporarryBlockedURLsGentleBlock.Clear();

                appSettings.TemporaryAllowedKeywords.Clear();
                appSettings.TemporaryAllowedURLsTabTitles.Clear();
                //Start the Focus Mode Timer to end after x hours / minutes
                ((App)(Application.Current)).wndTrayMenu.StartFocusModeTimer(nHours, nMinutes, nSeconds, false, bCommandFromDevice);

                //When resuming a session that is already in progress on the backend, do not start it again
                if (!bResumingSession)
                {
                    FocusBearAPIClient client = new FocusBearAPIClient();
                    var xResponse = client.StartFocusMode(appSettings.arrFocusModes[nIndexToStart].id);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Start WORK focus mode from suggestion window / button Exception - " + ex.Message);
            }
                
        }

        // True while a tab-limit alert is visible — prevents repeated alerts per breach.
        // volatile: read/written from WinEvent hook, fallback timer, and Closed event handler.
        private volatile bool _bTabLimitAlertShowing = false;

        // Throttle flag — true while a tab check is in-flight; incoming events are dropped.
        // volatile: written from async continuation, read from WinEvent hook thread.
        private volatile bool _tabCheckPending = false;

        // Snapshot of tab IDs taken the last time the count was at or below the limit.
        // Tabs NOT in this set are "new" (opened after limit was exceeded) and are closed first.
        private HashSet<string> _tabSnapshotAtLimit = new HashSet<string>();

        private static string GetTabRuntimeId(AutomationElement tab)
        {
            try { return string.Join("-", tab.GetRuntimeId()); }
            catch { return Guid.NewGuid().ToString(); } // unknown → treat as new tab
        }

        // Triggered by WinEventHook when user interacts with a browser.
        // Throttles rapid focus events: the first event triggers a check immediately;
        // subsequent events during the same check cycle are ignored (not reset like debounce).
        private async void TriggerTabLimitCheck()
        {
            if (_tabCheckPending) return;
            _tabCheckPending = true;
            try
            {
                await Task.Delay(CHROMIUM_UIA_REGISTRATION_MS);
                await Dispatcher.InvokeAsync(() => CheckTabLimitAndAlert());
                await Task.Delay(POST_CHECK_COOLDOWN_MS);
            }
            finally
            {
                _tabCheckPending = false;
            }
        }

        // Time for Chromium-based browsers to register a new tab in the UIA tree after opening.
        private const int CHROMIUM_UIA_REGISTRATION_MS = 200;
        // Cooldown after each check cycle — prevents hammering UIA on rapid focus events.
        private const int POST_CHECK_COOLDOWN_MS = 800;
        // Brave (and some Edge builds) need extra time to fully activate a tab before Ctrl+W works.
        private const int BRAVE_TAB_ACTIVATION_MS = 150;


        // Closes browser tabs that exceed nLimit across ALL browsers combined.
        // Called on a background thread — must not touch UI.
        private void CloseExcessTabs(int nLimit)
        {
            // Close new tabs (not in snapshot) first, then fall back to rightmost on screen.
            // This ensures tabs opened before the limit was set are never closed first.
            var snapshot = _tabSnapshotAtLimit;
            var allTabs = TabAudit.TabAuditService.CollectBrowserTabs()
                .OrderBy(t => snapshot.Contains(GetTabRuntimeId(t.Tab)) ? 1 : 0) // 0 = new tab → close first
                .ThenByDescending(t => { try { return t.Tab.Current.BoundingRectangle.X; } catch { return 0.0; } })
                .ToList();
            int nToClose = allTabs.Count - nLimit;
            if (nToClose <= 0) return;

            // Track remaining tabs per window to detect when the last tab is reached.
            var remainingPerWindow = allTabs
                .GroupBy(e => e.Hwnd)
                .ToDictionary(g => g.Key, g => g.Count());

            // List is sorted rightmost-first, so index 0 = most recently opened tab.
            for (int i = 0; i < allTabs.Count && nToClose > 0; i++, nToClose--)
            {
                var (tab, closeBtn, hwnd, browser) = allTabs[i];
                bool bClosed = false;

                remainingPerWindow[hwnd]--;
                bool isLastTabInWindow = remainingPerWindow[hwnd] == 0;

                // When this is the last tab in the window, close the entire window.
                // All browsers open a new blank tab if you just close the last tab — WM_CLOSE
                // avoids that and cleanly removes the browser window.
                if (isLastTabInWindow)
                {
                    try { PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero); bClosed = true; }
                    catch (Exception ex) { LogWriter.LogWrite($"CloseExcessTabs: WM_CLOSE ex — {ex.Message}"); }
                }

                // Primary: UIA InvokePattern on the × button — no cursor movement (Chrome, Edge, Firefox).
                if (!bClosed && closeBtn != null)
                {
                    try
                    {
                        var invokePattern = closeBtn.GetCurrentPattern(
                            System.Windows.Automation.InvokePattern.Pattern)
                            as System.Windows.Automation.InvokePattern;
                        if (invokePattern != null) { invokePattern.Invoke(); bClosed = true; }
                    }
                    catch (Exception ex) { LogWriter.LogWrite($"CloseExcessTabs: InvokePattern ex — {ex.Message}"); }
                }

                // Fallback: SelectionItemPattern + Ctrl+W.
                // Activates the tab via the accessibility API, then closes it with keyboard.
                // Covers Brave inactive tabs (no accessible × button) and other edge cases.
                if (!bClosed)
                {
                    try
                    {
                        var selectPattern = tab.GetCurrentPattern(
                            System.Windows.Automation.SelectionItemPattern.Pattern)
                            as System.Windows.Automation.SelectionItemPattern;
                        selectPattern?.Select();
                        System.Threading.Thread.Sleep(BRAVE_TAB_ACTIVATION_MS);

                        SetForegroundWindow(hwnd);
                        System.Threading.Thread.Sleep(BRAVE_TAB_ACTIVATION_MS);
                        keybd_event(VK_CONTROL, 0, 0,               UIntPtr.Zero);
                        keybd_event(VK_W,       0, 0,               UIntPtr.Zero);
                        keybd_event(VK_W,       0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    }
                    catch (Exception ex) { LogWriter.LogWrite($"CloseExcessTabs: Ctrl+W ex — {ex.Message}"); }
                }

                // Let the browser process each close before moving to the next tab.
                System.Threading.Thread.Sleep(BRAVE_TAB_ACTIVATION_MS);
            }
        }

        private async void CheckTabLimitAndAlert()
        {
           // LogWriter.LogWrite($"CheckTabLimitAndAlert: limit={appSettings.nMaxTabLimit}, purchased={appSettings.isPurchased}");

            // Nothing needs a browser scan unless the count is being consumed: the tab limiter or the
            // productivity stats (only recorded during a focus session). Skipping otherwise avoids a full
            // cross-process UIA scan every 3s on every machine for no reason (#1167).
            // The limiter enforces only when it's enabled, the subscription/trial hasn't expired, AND we're
            // in an active Focus Session or an active Blocking Schedule (matches macOS — see the
            // "Works only during active Focus Sessions or active Blocking Schedules" setting subtitle).
            bool bLimiterActive = appSettings.bTabLimiterEnabled && !appSettings.isExpired
                                  && (appSettings.isFocusModeRunning || appSettings.isBlockingScheduleRunning);
            if (!appSettings.isFocusModeRunning && !bLimiterActive)
                return;

            System.Collections.Generic.List<(AutomationElement Tab, AutomationElement CloseBtn, IntPtr Hwnd, string Browser)> currentTabs = null;
            try
            {
                currentTabs = await Task.Run(() => TabAudit.TabAuditService.CollectBrowserTabs());
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite($"CheckTabLimitAndAlert Task.Run ex: {ex.Message}");
            }

            int nTabCount = currentTabs?.Count ?? 0;
            appSettings.nCountOpenTabs = nTabCount;

            //LogWriter.LogWrite($"CheckTabLimitAndAlert: tabs={nTabCount}, limit={appSettings.nMaxTabLimit}");

            // Enforce only when the limiter is actually active (valid subscription/trial + a configured
            // limit). The scan above may instead have run only to feed the productivity stats during a
            // focus session, so bail out of enforcement here.
            if (!bLimiterActive)
                return;

            if (_bTabLimitAlertShowing)
                return; // Already showing — don't spam

            if (nTabCount > appSettings.nMaxTabLimit)
            {
                LogWriter.LogWrite($"CheckTabLimitAndAlert: tab limit exceeded ({nTabCount} > {appSettings.nMaxTabLimit})");
                _bTabLimitAlertShowing = true;
                appSettings.CapturePostHog("tab-limit-exceeded");

                AlertMaxTabLimitWindow alertWnd = new AlertMaxTabLimitWindow(
                    nTabCount,
                    appSettings.nMaxTabLimit,
                    TabAudit.TabAuditService.GetBrowserTabCount,
                    CloseExcessTabs);
                alertWnd.Closed += (s, e) =>
                {
                    _bTabLimitAlertShowing = false;
                };
                alertWnd.Show();
            }
            else
            {
                _bTabLimitAlertShowing = false;
                // Within limit — snapshot current tabs so CloseExcessTabs can identify new ones later
                if (currentTabs != null)
                    _tabSnapshotAtLimit = new HashSet<string>(currentTabs.Select(t => GetTabRuntimeId(t.Tab)));
            }
        }

        public static List<string> GetTabs()
        {
            System.Windows.Automation.Condition TabItemCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem);


            List<String> result = new List<string>();

            Process[] allChromeProcesses = Process.GetProcessesByName("chrome");

            if (allChromeProcesses.Length == 0)
                return result;

            Process[] mainChromes = allChromeProcesses.Where(p => !String.IsNullOrEmpty(p.MainWindowTitle)).ToArray();

            if (mainChromes.Length == 0)
                return result;

            for (int c = 0; c < mainChromes.Length; c++)
            {
                AutomationElement chromeMainUIAElement = AutomationElement.FromHandle(mainChromes[c].MainWindowHandle);

                var chromeTabCollection = chromeMainUIAElement.FindAll(System.Windows.Automation.TreeScope.Descendants, TabItemCondition);

                for (int i = 0; i < chromeTabCollection.Count; i++)
                {
                    AutomationElement tab = chromeTabCollection[i];
                    string title = tab.Current.Name;

                }
            }

            return result;
        }

        public bool CheckIf4HoursBeforeNextMorningRoutine()
        {

            if (appSettings.bCompleteYourEveningRoutineClicked)
            {
                return false;
            }
            //Check if its 4 hours before the morning time 
            try
            {
                DateTime MorningHabitsDateTimeNextDay;

                DateTime now = DateTime.Now;
                TimeSpan tsStartTime = TimeSpan.Parse(appSettings.sDayStartTime);
                DateTime myStartTime = new DateTime(now.Year, now.Month, now.Day, tsStartTime.Hours, tsStartTime.Minutes, 00);
                Console.WriteLine("{0}", myStartTime.ToString());

                if (now > myStartTime) //Same Day like before 12.00 am, so get for next day
                {
                    //Get the morning time and prepare a DT  
                    myStartTime = myStartTime.AddDays(1);
                    MorningHabitsDateTimeNextDay = myStartTime;
                    LogWriter.LogWrite(myStartTime.ToString());
                }
                else //get for same Day like now is after 12.00 am 
                {
                    MorningHabitsDateTimeNextDay = myStartTime;//Today's Morning time
                    LogWriter.LogWrite(myStartTime.ToString());
                }

                if (now > MorningHabitsDateTimeNextDay.AddHours(-4))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("CheckIf4HoursBeforeNextMorningRoutine ex - " + ex.Message);
            }///


            return false;
        }

        public void ShowToDoPlayer()
        {
            if(appSettings.isFocusModeRunning)
            {
                //change the title of the todo player button
                wndFocusBearMainWindow.UpdateLeafTitle(LeftButtonType.ToDoList, appSettings.ConfigFileStrings["menuInfo"]["menuItemToDoPlayerTitle"].ToString());
            }
            BringMainWindowToFront();

            wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.ToDoList);

            if (bLoadURLAgainInToDoPlayer)
            {
                wndFocusBearMainWindow.PageToDoPlayer.LoadURL();
                bLoadURLAgainInToDoPlayer = false;
            }

        }

        public void ShowToDoList()
        {
            BringMainWindowToFront();
            wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.ToDoList);
        }
        public void HideToDoPlayer()
        {
            // Guard CoreWebView2: a just-created/resumed player may not have finished initializing,
            // and Reload() throws if CoreWebView2 isn't ready yet.
            var webView = wndFocusBearMainWindow?.PageToDoPlayer?.webView;
            if (webView?.CoreWebView2 != null)
                webView.Reload(); // Refresh webview to stop background execution
            bLoadURLAgainInToDoPlayer = true;
        }
        public void NotifyToDoPlayerPauseState(bool isPaused)
        {
            // Update title and refresh view based on pause state
            string toDoTitle = isPaused
                ? appSettings.ConfigFileStrings["menuInfo"]["menuItemToDoListTitle"].ToString()
                : appSettings.ConfigFileStrings["menuInfo"]["menuItemToDoPlayerTitle"].ToString();

            wndFocusBearMainWindow?.UpdateLeafTitle(LeftButtonType.ToDoList, toDoTitle);

            if (wndFocusBearMainWindow?.IsLeafSelected(LeftButtonType.ToDoList) == true)
            {
                wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.ToDoList);
            }

            try
            {
                var webView = wndFocusBearMainWindow?.PageToDoPlayer?.webView;
                if (webView?.CoreWebView2 == null)
                {
                    return;
                }

                string pausedValue = isPaused ? "true" : "false";
                string script = $@"
                    (function() {{
                        if (window.setFocusPausedFromDesktopApp) {{
                        window.setFocusPausedFromDesktopApp({pausedValue});
                        }}
                    }})();
                ";

                var _ = webView.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("NotifyToDoPlayerPauseState ex - " + ex.Message);
            }
        }

        public void ShowNaturalBreakDetectionWindow()
        {
            LogWriter.LogWrite("ShowNaturalBreakDetectionWindow()");
            
            if (wndNaturalBreakDetectionWindow == null)
            {
                wndNaturalBreakDetectionWindow = new NaturalBreakDetectionWindow();
            }
            wndNaturalBreakDetectionWindow.Hide(); // avoid multiple
            wndNaturalBreakDetectionWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            wndNaturalBreakDetectionWindow.Topmost = true;
            wndNaturalBreakDetectionWindow.Show();
        }
        public void FocusModeAddedOrDeleted()
        {
            FocusModeAddedDeletedEvent?.Invoke();
        } 

        public void ForceStopFocusSessionOnHabitsTime()
        {
            LogWriter.LogWrite("ForceStopFocusSessionOnHabitsTime() - Stop the Focus Mode as Habits time had arrieved.");

            //Teacher Mode - habits time must not tear down a teacher controlled class session;
            //it only ends via class-session-ended or its scheduled finish time.
            if (TeacherSessionManager.Instance.IsTeacherSessionActive())
            {
                LogWriter.LogWrite("ForceStopFocusSessionOnHabitsTime skipped - teacher class session is active");
                return;
            }

            if (appSettings.isFocusModePaused)
            {
                LogWriter.LogWrite("ForceStopFocusSessionOnHabitsTime() - Focus was paused, resetting pause state before stopping.");
                appSettings.isFocusModePaused = false;
                appSettings.nPausedFocusRemainingSeconds = 0;
            }

            try
            {
                //Double check
                var listItems = appSettings.arrFocusModes.Where(i => i.isOn == true);
                if (listItems.Count() != 0)
                {
                    try
                    {
                        //Stop the focus mode
                        listItems.First<FocusMode>().isOn = false;

                        appSettings.bUseSuperPomodoroMode = false;

                        FinishFocusModeCommonTasks();

                        appSettings.bStartingAnotherFocusMode = false; //Resetting

                        appSettings.bForceClosingFocusMode = true;
                        //Show the Focusmode end dialog
                        wndFocusBearMainWindow.ShowFocusModeEndView();

                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("ForceStopFocusSessionOnHabitsTime ex - " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ForceStopFocusSessionOnHabitsTime ex - " + ex.Message);
            }
             
        }

        public async void FinishFocusModeCommonTasks()
        {
            try
            {
                LogWriter.LogWrite("FinishFocusModeCommonTasks()");

                // Sync the todo tasks first
                try
                {
                    await SyncTodoTasksFromPlayer();
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("FinishFocusModeCommonTasks() ex = " + ex.Message);
                }
                Sounds.PlayFocusModeEndSound();

                appSettings.CurrentFocusAlignmentScore = FocusAlignmentScoreManager.CalculateFocusAlignmentScore();//calculate before we empty it
                
                if (appSettings.focusSessionStartTime != null)
                {
                    appSettings.CurrentFocusSessionDuration += (int)(DateTime.Now - appSettings.focusSessionStartTime.Value).TotalSeconds;
                    appSettings.focusSessionStartTime = null;
                }

                //Set Alignment score related
                FocusAlignmentScoreManager.SetAppIconForFocusModeScore(null);
                FocusAlignmentScoreManager.DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus.Clear();

                //Write the usage_timings_explanation CSV: 24 April 2026
                if (appSettings.bAdvancedAnalyticsInFocusModeEnabled)
                {
                    WriteUsageTimingsExplanationCSV();
                }

                //Make the global variables false
                appSettings.isFocusModeRunning = false;

                App.FocusButton.FinishFocusSession();

                //Clear the Gentle block temp arrays
                appSettings.TemporarryBlockedAppsGentleBlock.Clear();
                appSettings.TemporarryBlockedURLsGentleBlock.Clear();

                //Close the required windows
                CloseFocusProgressWindow();
                
                HideToDoPlayer();

                //change the title of the todo player button
                wndFocusBearMainWindow.UpdateLeafTitle(LeftButtonType.ToDoList, appSettings.ConfigFileStrings["menuInfo"]["menuItemToDoListTitle"].ToString());
                wndFocusBearMainWindow.UpdateLeafTitle(LeftButtonType.StartFocusSession, appSettings.ConfigFileStrings["menuInfo"]["menuItemStartPomodoroTitle"].ToString());

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnQuit_Click sync: " + ex.Message);
            }
        }
        
        /* Invoke Window_Closing to retrieve the latest to-do tasks from the player and store them in appSettings.sRawTasks. 
            This redundancy helps ensure consistency between the to-do list in the player 
            and the task list displayed in the focus and end WebViews.
        */
        public async Task SyncTodoTasksFromPlayer()
        {
            if (wndFocusBearMainWindow.PageToDoPlayer != null)
            {
                var tasksArray = await wndFocusBearMainWindow.PageToDoPlayer.webView.ExecuteScriptAsync("getToDosFromPlayer()");
                appSettings.sRawTasks = tasksArray?.ToString() ?? "[]";
            }
        }

        public void WriteUsageTimingsExplanationCSV()
        {
            LogWriter.LogWrite("WriteUsageTimingsExplanationCSV()");

            var now = DateTime.Now;
            var AppRecords = TabMetrics.Instance.GetAppRecordsSnapshot();
            foreach (var r in AppRecords)
            {
                var end = r.EndTime ?? now;
                int duration = r.Duration > 0 ? r.Duration : (int)(end - r.StartTime).TotalSeconds;
                string csvLine = $"{r.StartTime},{r.EndTime},{r.AppName},{r.Duration}";
                appSettings.csvUsageTimingsExplanation.Append(csvLine);
            }
            
            var DomainRecords = TabMetrics.Instance.GetDomainRecordsSnapshot();
            foreach (var r in DomainRecords)
            {
                var end = r.EndTime ?? now;
                int duration = r.Duration > 0 ? r.Duration : (int)(end - r.StartTime).TotalSeconds;
                string csvLine = $"{r.StartTime},{r.EndTime},{r.Domain},{r.Duration}";
                appSettings.csvUsageTimingsExplanation.Append(csvLine);
            }

        }
        public void ChangeTheme(string theme)
        {
            ResourceDictionary newTheme = new ResourceDictionary();
            if (theme == "Dark")
            {
                newTheme.Source = new Uri("Theme/DarkTheme.xaml", UriKind.Relative);

            }
            else if (theme == "Light")
            {
                newTheme.Source = new Uri("Theme/LightTheme.xaml", UriKind.Relative);
            }
            ThemeChanged?.Invoke();


            // Clear old theme and apply new theme
            Application.Current.Resources.MergedDictionaries.Clear();
            //Application.Current.Resources.MergedDictionaries.Add(themeStyle);

            Application.Current.Resources.MergedDictionaries.Add(newTheme);
            // Save theme setting
            appSettings.sTheme = theme;

        }
        public bool UseImmersiveDarkMode(IntPtr handle, bool enabled)
        {
            Version osVersion = Environment.OSVersion.Version;
            bool isWindows10OrGreater = (osVersion.Major == 10 && osVersion.Build >= 17763) || osVersion.Major > 10;

            if (isWindows10OrGreater)
            {
                var attribute = 19;
                if (osVersion.Build >= 18985)
                {
                    attribute = 20;
                }

                int useImmersiveDarkMode = enabled ? 1 : 0;
                return DwmSetWindowAttribute(handle, attribute, ref useImmersiveDarkMode, sizeof(int)) == 0;
            }
            return false;
        }

        public void ApplyThemeToPage(System.Windows.Controls.Page page)
        {
            LogWriter.LogWrite("app xaml ApplyThemeToPage: " +appSettings.sTheme );

            var source = (HwndSource)PresentationSource.FromVisual(page);
            if(source != null)
            {
                ((App)Current).UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }
            if (appSettings.sTheme == "Light")
            {
                page.Background = new SolidColorBrush(Colors.White);
            }
            else if (appSettings.sTheme == "Dark")
            {
                page.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
            }

            bool isDarkMode = (appSettings.sTheme == "Dark");
            Application.Current.Resources["TooltipBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isDarkMode ? "#2B2B2B" : "#F5F5F5"));
            Application.Current.Resources["TooltipForeground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isDarkMode ? "#F1F1F1" : "#1E1E1E"));
            Application.Current.Resources["TooltipBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isDarkMode ? "#444444" : "#CCCCCC"));


        }

        public void ShowFocusMusic()
        {
            BringMainWindowToFront();

            wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.FocusMusic);
        }

        public async void StopFocusSessionFromEvents()
        {
            try
            {
                var tray = ((App)Application.Current).wndTrayMenu;
                if (tray != null)
                {
                    await tray.StopFocusSessionRealAsync();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite($"Error in btnEndFocus_Click: {ex.Message}");
            }
        }
    }
}
