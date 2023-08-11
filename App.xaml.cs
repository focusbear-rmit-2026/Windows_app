using Focus_Bear.API;
using Focus_Bear.BrainDump;
using Focus_Bear.Focus;
using Focus_Bear.Habits;
using Focus_Bear.Login;
using Focus_Bear.Models;
using Focus_Bear.Onboarding;
using Focus_Bear.Onboarding.AfterLogin;
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
using Focus_Bear.Onboarding.New;
using Microsoft.Win32;
using Focus_Bear.Relax;
using Focus_Bear.Tour;

namespace Focus_Bear
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        AppSettings appSettings = AppSettings.GetInstance;

        private const string MutexGUID = "369F3719-1C05-41A1-B9E1-FC0CF0FCE3AE";

        private Mutex appMutex;

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

        private const uint WINEVENT_OUTOFCONTEXT = 0;
        private const uint EVENT_SYSTEM_FOREGROUND = 3;
        private const uint WINEVENT_SKIPOWNPROCESS = 0x0002 ;
 
        public void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            //Always get the last active app name when the menu is clicked and ignore Focus Bear
            string sAppName = GetLastActiveAppName();
            
            if (sAppName.Length > 0 && sAppName != "Focus Bear.exe" && sAppName != "explorer.exe")
            {
                appSettings.sLastActiveAppName = sAppName;
            }

            try 
            {  
                if (appSettings.sLastActiveAppName == "firefox.exe" )
                {
                    int nRet;
                    // Pre-allocate 256 characters, since this is the maximum class name length.
                    StringBuilder ClassName = new StringBuilder(256);
                    //Get the window class name
                    nRet = GetClassName(hwnd, ClassName, ClassName.Capacity);
                    if (nRet != 0)
                    {
                        if(string.Compare(ClassName.ToString(), "MozillaWindowClass", true, CultureInfo.InvariantCulture) == 0)
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

                                    if (sName != null && (sName.StartsWith("Buscar con") || sName.StartsWith("Search with") ) && (sURL != null && sURL.Length > 0) ) //It's our FF address bar
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
                                }
                            }
                        }
                    }
                }
                else if (appSettings.sLastActiveAppName == "chrome.exe" || appSettings.sLastActiveAppName == "msedge.exe" || appSettings.sLastActiveAppName == "brave.exe")
                {
                    int nRet;
                    // Pre-allocate 256 characters, since this is the maximum class name length.
                    StringBuilder ClassName = new StringBuilder(256);
                    //Get the window class name
                    nRet = GetClassName(hwnd, ClassName, ClassName.Capacity);
                    if (nRet != 0)
                    {
                        if (string.Compare(ClassName.ToString(), "Chrome_WidgetWin_1", true, CultureInfo.InvariantCulture) == 0)
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
                                }
                            }
                        }
                    }
                }

            } 
            catch (Exception ex) 
            {
                Debug.WriteLine("Exception " + ex);
            }
        }

        public enum ProcessAccessFlags : uint
        {
            PROCESS_ALL_ACCESS = 0x001F0FFF,
            PROCESS_CREATE_PROCESS = 0x00000080,
            PROCESS_CREATE_THREAD = 0x00000002,
            PROCESS_DUP_HANDLE = 0x00000040,
            PROCESS_QUERY_INFORMATION = 0x00000400,
            PROCESS_QUERY_LIMITED_INFORMATION = 0x00001000,
            PROCESS_SET_INFORMATION = 0x00000200,
            PROCESS_SET_QUOTA = 0x00000100,
            PROCESS_SUSPEND_RESUME = 0x00000800,
            PROCESS_TERMINATE = 0x00000001,
            PROCESS_VM_OPERATION = 0x00000008,
            PROCESS_VM_READ = 0x00000010,
            PROCESS_VM_WRITE = 0x00000020
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out IntPtr lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool QueryFullProcessImageName([In] IntPtr hProcess, [In] uint dwFlags, [Out] StringBuilder lpExeName, ref uint lpdwSize);

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);


        private static string GetLastActiveAppName()
        {
            try
            {
                IntPtr foregroundWindowId = GetForegroundWindow();
                GetWindowThreadProcessId(foregroundWindowId, out IntPtr activeAppProcessId);
                IntPtr hprocess = OpenProcess(ProcessAccessFlags.PROCESS_QUERY_LIMITED_INFORMATION, false, (int)activeAppProcessId);
                uint lpdwSize = 1024;
                StringBuilder lpExeName = new StringBuilder((int)lpdwSize);
                QueryFullProcessImageName(hprocess, 0, lpExeName, ref lpdwSize);
                var exePath = lpExeName.ToString();
                return Path.GetFileName(exePath);
            }
            catch (Exception ex)
            {
                //LogWriter.LogWrite("GetLastActiveAppName() - " + ex.Message);
            }

            return "";
        }


        DispatcherTimer dispatcherTimerBreaks = new DispatcherTimer(DispatcherPriority.Normal);
        DispatcherTimer dispatcherTimerBackground = new DispatcherTimer(DispatcherPriority.Normal);
        DispatcherTimer dispatcherTimerPostponeHabits = new DispatcherTimer(DispatcherPriority.Normal);
        DispatcherTimer dispatcherTimerResumeHabitsAfterPostpone = new DispatcherTimer(DispatcherPriority.Normal);


        public WinForms.NotifyIcon trayIcon = null;

        bool isMorningHabitsAlreadyShown = false;

        public HabitWindow wndHabits;
        ShutOffWindow wndShutOff;
        SplashWindow wndSplash;

        public PreferencesWindow wndPreferences = null;
        public TrayMenu wndTrayMenu = null;

        public FocusModeEnd wndFocusModeEnd = null;

        public BreakWindow wndTakeABreak = null;

        public FocusActivitySummary wndFocusSummary = null;

        public EasySkipPostpone wndEasyPostpone = null;

        public BrainDumpWindow wndBrainDump = null;

        public FocusProgress wndFocusProgress = null;

        public HabitsResumeInPopup wndHabitsResumeInPopup = null;

        public RelaxModeWindow wndRelaxMode = null;

        bool bAppExtracted = false;

        void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            SentrySdk.ConfigureScope(scope => {
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
                o.Dsn = "https://e10a0e8886bd4bb89d44cfa909a71ac9@o1187124.ingest.sentry.io/4504950047309824";

                // When configuring for the first time, to see what the SDK is doing:
                o.Debug = true;

                // Set traces_sample_rate to 1.0 to capture 100% of transactions for performance monitoring.
                // We recommend adjusting this value in production.
                o.TracesSampleRate = 1.0;

                // Enable Global Mode since this is a client app
                o.IsGlobalModeEnabled = true;

                //TODO: any other options you need go here
            });

            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            LogWriter.LogWrite( string.Format("App Started - Version {0}", appSettings.AppVersion.ToString()) );

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
            }

            if (!bStartingAfterReactivate)
            {
                try
                {
                    #region Managing Instances
                    //Handling running instances
                    bool createdNew;
                    appMutex = new Mutex(true, MutexGUID, out createdNew);

                    GC.KeepAlive(this.appMutex);
                    if (!createdNew)
                    {
                        // The application is already running
                        LogWriter.LogWrite("The application is already running - terminate this new instance");
                        Application.Current.Shutdown(0);
                        return;
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
            Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", "--autoplay-policy=no-user-gesture-required");

            RegistryManager.GetLangCode();

            appSettings.DownloadAndLoadConfigFile();

            //System sleep / wake up event
            SystemEvents.PowerModeChanged += OnPowerChange;
        }

        private void OnPowerChange(object s, PowerModeChangedEventArgs e)
        {
            switch (e.Mode)
            {
                case PowerModes.Resume:
                    LogWriter.LogWrite("PowerModes.Resume");
                    ResumeHabitsAfterSystemWakeup();
                    break;
                case PowerModes.Suspend:
                    LogWriter.LogWrite("PowerModes.Suspend");
                    PauseHabitsOnSystemSleep();
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
                    taskSchedule.CreateTask("Focus Bear", "");
                }
                else
                {
                    WinSecurity.RestartElevated(true);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetAtWindowsStartup() ex - " + ex.Message);
            }
           
        }
        public void ProceedAfterDownloadingConfigFile()
        {
            LogWriter.LogWrite("ProceedAfterDownloadingConfigFile()");

            ///
            //Show the splash window here
            wndSplash = new SplashWindow();
            LogWriter.LogWrite("Show SplashWindow()");
            wndSplash.Show();

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

            //Enbale url blocking we need this
            dele = new WinEventDelegate(WinEventProc);
            m_hhook = SetWinEventHook(0x8005, 0x800E, IntPtr.Zero, dele, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);


            if (!RegistryManager.GetWelcomeAboardingDoneStatus())
            {
                LogWriter.LogWrite("GetWelcomeAboardingDoneStatus NO");
                
                if (wndSplash != null)
                {
                    LogWriter.LogWrite("Hide SplashWindow() to show onboardiong flow");
                    wndSplash.Close();
                }


                ShowWelcomeAbordOnBoradingFlow();
            }
            else
            {
                LogWriter.LogWrite("GetWelcomeAboardingDoneStatus YES");
                appSettings.GetLoginAndTokenValidSettings();
            }


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

                    new TrayBalloon(0, appSettings.ConfigFileStrings["WindowsSpecific"]["runningbg"].ToString()).Show();  
                    using (Stream iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Focus Bear;component/Images/focus-bear.ico")).Stream)
                    {
                        trayIcon.Icon = new System.Drawing.Icon(iconStream);
                    }

                    trayIcon.MouseClick += TrayIcon_MouseClick;
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
                if (((App)(Application.Current)).wndTakeABreak != null && !(((App)(Application.Current)).wndTakeABreak.IsVisible))
                {
                    if(((App)(Application.Current)).wndTakeABreak.bShowingForNonTimeActivities)
                    {
                        ((App)(Application.Current)).wndTakeABreak.Show();
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TrayIcon_MouseClick" + ex.Message);
 
            }
        }

        public void StartBreakActivityTimer(bool bUserPostponed = false, int nMinutesToPostpone = 0)
        {
            LogWriter.LogWrite("StartBreakActivityTimer() invoked");

            if (appSettings.IsShutOffTime())
            {
                LogWriter.LogWrite("It Is IsShutOffTime() so returning from StartBreakActivityTimer()");
                return;
            }

            //Remove the previous event handler so that new can be set
            try
            {
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

                if(bUserPostponed) //settimer for 5 minutes
                {
                    LogWriter.LogWrite("User Postponed set StartBreakActivityTimer() for 5 minutes");
#if DEBUG
                    dispatcherTimerBreaks.Interval = new TimeSpan(0, 0, 15);
#else
                    dispatcherTimerBreaks.Interval = new TimeSpan(0, nMinutesToPostpone, 0);
#endif
                }
                else
                {
#if DEBUG
                    dispatcherTimerBreaks.Interval = new TimeSpan(0, 0, appSettings.breakAfterMinutes);
#else
                    dispatcherTimerBreaks.Interval = new TimeSpan(0,  appSettings.breakAfterMinutes, 0);
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

        private void dispatcherTimer_Breaks(object sender, EventArgs e)
        {
            LogWriter.LogWrite("dispatcherTimer_Breaks()");
           

            try
            {
                dispatcherTimerBreaks.Stop(); //Tick Once only

                CommandManager.InvalidateRequerySuggested();

                if (appSettings.nCurrentActivityType == CurrentActivityType.SHUT_OFF || appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    LogWriter.LogWrite("SHUT_OFF or EVENING_HABITS - Return from dispatcherTimer_Breaks()");
                    return;
                }

                if (appSettings.isFocusModeRunning && appSettings.bSkipMicroBreaksDuringFocus)
                {
                    LogWriter.LogWrite("appSettings.isFocusModeRunning && appSettings.bSkipMicroBreaksDuringFocus - return");
                    //Start the break Activities Timer
                    StartBreakActivityTimer();
                    return;
                }

                if(appSettings.IsAppDeactivated())
                {
                    LogWriter.LogWrite("appSettings.IsAppDeactivated() - return from breaks timer");
                    //Start the break Activities Timer
                    StartBreakActivityTimer();
                    return;
                }
                
                //Check here if the Breaks window is visible, we will hide it.
                if (((App)(Application.Current)).wndTakeABreak != null && ((App)(Application.Current)).wndTakeABreak.IsVisible)
                {
                    ((App)(Application.Current)).wndTakeABreak.HideBreakWindowAndStopTimer();
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

            if (appSettings.nCurrentActivityType == CurrentActivityType.SHUT_OFF || appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
            {
                LogWriter.LogWrite("SHUT_OFF or EVENING_HABITS - Return from ShowBreakActivities()");
                return;
            }

        

            appSettings.nCurrentActivityType = CurrentActivityType.BREAK_ACTIVITY;
            LogWriter.LogWrite(" appSettings.nCurrentActivityType = CurrentActivityType.BREAK_ACTIVITY;");
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
 
                wndHabits = new HabitWindow();
                wndHabits.Topmost = true;
                wndHabits.Show();
                wndHabits.Activate();
                wndHabits.Focus();
                ShowBlankWindowOnOtherScreens();
            }
            else
            {
                appSettings.nCurrentActivityType = CurrentActivityType.NONE;
            }
        }
        public void ShowWelcomeAbordOnBoradingFlow()
        {
            LogWriter.LogWrite("ShowWelcomeAbordOnBoradingFlow");

            NewOnboardingStart windWelcome = new NewOnboardingStart();
            windWelcome.Topmost = true;
            windWelcome.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            windWelcome.Show();
            windWelcome.Activate();
            windWelcome.Focus();
        }

        public void ShowOnBoradingFlow(bool bSimulateFlowHidden = false)
        {
            LogWriter.LogWrite("ShowOnBoradingFlow");

           
            if (!bSimulateFlowHidden)
            { 
                OnBoardingWindow windOnBoarding = new OnBoardingWindow();
                windOnBoarding.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                windOnBoarding.Topmost = true;
                windOnBoarding.Show();
                windOnBoarding.Activate();
                windOnBoarding.Focus();

                ShowBlankWindowOnOtherScreens();
            }
            else
            {
                //We need to pass on all screens in hidden window, and collect all defaults
                OnBoardingWindow windOnBoarding = new OnBoardingWindow(true);

            }
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

            LoginMain windLogin = new LoginMain();
            // MainWindow = windLogin;
            windLogin.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            windLogin.Show();
            windLogin.Activate();
            windLogin.Focus();
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

            //appSettings.DownloadAndLoadConfigFile();
            //Show Tray Menu
            //Proceed Normal to show routines
            ProceedAfterLogin();
        }
        public async void ProceedAfterLogin()
        {
            LogWriter.LogWrite("ProceedAfterLogin");

            RegistryManager.GetApplicationFont();

            if (!RegistryManager.GetOnboardingVideoShownStatus())
            {
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

                await Task.Delay(1000); //Let the login window be closed

                var userConsentResponseUnused = client.RecordUserConsent();
                

                if (wndSplash != null)
                {
                    LogWriter.LogWrite("hide SplashWindow() show Onboarding case");
                    wndSplash.Close();
                }

                
                OnboardingNotesWindow windOnBoardingNotes = new OnboardingNotesWindow();
                windOnBoardingNotes.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                windOnBoardingNotes.Topmost = true;
                windOnBoardingNotes.ShowDialog();
                windOnBoardingNotes.Activate();
                windOnBoardingNotes.Focus();

                //We are setting evening routine shown here for thenext 2 hours so that evening routine willnot come for the next 2 hours the video is shown
                appSettings.ShutOFFAbortedTillDate = DateTime.Now.AddHours(2);

                RegistryManager.SetShutOffAbortedTillDate();

                //Additional safety not to show morning routine
                //No need to show the Morning Habits
                //WE will set the morning habits shown date for today
                appSettings.MorningHabitsShownDate = DateTime.Now;
                RegistryManager.SetMorningHabitsShownDate();


                RegistryManager.SetOnboardingVideoShownStatus();

                if (appSettings.bShowOnBoardingVideo) //If user had agreed to see the video
                    ShowOnBoradingVideo();//Show Onboarding Video

                LogWriter.LogWrite("Show FocusBearMenuTour");
                //Whether the user had opted to see the video or not, this tour will come here.
                FocusBearMenuTour wndFocusBearMenuTour = new FocusBearMenuTour();
                wndFocusBearMenuTour.ShowDialog();
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

                if ( x == 0)
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
           


            if (appSettings.isExpired)
            {
                LogWriter.LogWrite("isExpired TRUE");

                if (wndSplash != null)
                {
                    LogWriter.LogWrite("Show SplashWindow() EXPIRED case");
                    wndSplash.Close();
                }

                ShowExpiredNag(true);
            }
            //Get local settings

            //Get Focus settings aka user details

            new Thread(new ThreadStart(new Action(async () =>
            {
                CommandManager.InvalidateRequerySuggested();
                //Copy all graphs to local
                appSettings.CopyGraphsToLocal();

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
                var engine = new AppsEngine();// SmartUninstallerEngine(_cts.Token, _PauseResetEvent);
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

                //If its the first install time, we will need to put the user settings back if the statup or shutoff time is changed
                if (appSettings.IsFirstInstall)
                {
                    String jsonString = appSettings.sRawUserSettings;
  
                    JObject UserSettings = JObject.Parse(appSettings.sRawUserSettings);

                    if (UserSettings.ContainsKey("startup_time"))
                    {
                        UserSettings.Remove("startup_time");
                        UserSettings.Add("startup_time", appSettings.InitialStartupTimeValue.ToString("HH:mm"));
                    }
                    if (UserSettings.ContainsKey("shutdown_time"))
                    {
                        UserSettings.Remove("shutdown_time");
                        UserSettings.Add("shutdown_time", appSettings.InitialShutoffTimeValue.ToString("HH:mm"));
                    }
                    if (UserSettings.ContainsKey("sleep_time"))
                    {
                        UserSettings.Remove("sleep_time");
                        UserSettings.Add("sleep_time", appSettings.InitialSleepTimeValue.ToString("HH:mm"));
                    }
                    else
                    {
                        UserSettings.Add("sleep_time", appSettings.InitialSleepTimeValue.ToString("HH:mm"));
                    }
                    if (UserSettings.ContainsKey("break_after_minutes"))
                    {
                        UserSettings.Remove("break_after_minutes");
                        UserSettings.Add("break_after_minutes", appSettings.BreaksTimeInterval);
                    }
                    appSettings.sDayStartTime = appSettings.InitialStartupTimeValue.ToString("HH:mm");
                    appSettings.sDayShutOffTime = appSettings.InitialShutoffTimeValue.ToString("HH:mm");

                    //Also save the udpated settings to Registry
                    appSettings.sRawUserSettings = UserSettings.ToString();
                    RegistryManager.SaveUserSettings();

                    //We will need to put the settings.
                    var y = client.PutUserSettings();
                }

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
                    if (appSettings.arrFocusModes.Count <= 4)
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

            StartWorkFlow();
        }

        public void StartWorkFlow()
        {
            LogWriter.LogWrite("StartWorkFlow()");


#if DEBUG
            //Write any routines or functionality to test in DEBUG mode here
#endif

            if (appSettings.IsFirstInstall)
            {
                LogWriter.LogWrite("appSettings.IsFirstInstall true");
                //No need to show the Morning Habits
                //WE will set the morning habits shown date for today
                appSettings.MorningHabitsShownDate = DateTime.Now;
                RegistryManager.SetMorningHabitsShownDate();
            }
            

            //Do clear the Postponed variables
            RegistryManager.RemovePostponedHabitData();

            //Check if its shut off time, show evening routine
            //If morning time show morning routine
            //if not both show break activties
            if (RegistryManager.GetMorningHabitsShownDate())
            {
                if (appSettings.MorningHabitsShownDate.Date == DateTime.Now.Date)
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
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StartWorkFlow()" + ex.Message);
            }

             
        }
        private void dispatcherTimer_Background(object sender, EventArgs e)
        {
            try
            {
                CommandManager.InvalidateRequerySuggested();

                OfflineActivities.ProcessOfflineData();

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

                if(now > fiveMinutesFiveSecondsBeforeShutOffTime && now < fiveMinutesBeforeShutOffTime)
                {
                    LogWriter.LogWrite("fiveMinutesFiveSecondsBeforeShutOffTime - show notification shutdown approaching");

                    TrayBalloon tt = new TrayBalloon(1, appSettings.ConfigFileStrings["common"]["shutdownNotificationTitle"].ToString());
                    tt.Show();
                }
                if (appSettings.IsShutOffTime())
                {
                    if (appSettings.nCurrentActivityType == CurrentActivityType.SHUT_OFF)
                    {
                        LogWriter.LogWrite("SHUT_OFF STATE - Return from timer");
                        return;
                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                    {
                        LogWriter.LogWrite("EVENING_HABITS STATE - Return from timer");

                       
                        if(appSettings.DidCutOffTimePassed())
                        {
                            //Check if the current habit index is of High Priority or not, show only if not
                            if (appSettings.nHabitIndex != -1) //habit index is -1 if focus summary is visible
                            {
                                if (!appSettings.arrAllEveningHabits[appSettings.nHabitIndex].bHighPriorityActivity)
                                {
                                    
                                    //since a high priority activity is running, we need to skip it and show the finish screen 
                                    //and allow starting next high priority activity or show unlock button
                                    if (wndHabits != null)
                                    {
                                        if (wndHabits.PageHabitActivityOnGoing != null)
                                        {
                                            if (wndHabits.PageHabitActivityOnGoing.bTimerRunning)
                                            {
                                                wndHabits.PageHabitActivityOnGoing.StopTimerOfRunningActivity(); //Stop the timer
                                                                                                                 //we need to call the Activity Skipped API here to skip the current activity

                                                //it may be relax activity window visible
                                                if(wndRelaxMode != null && wndRelaxMode.IsVisible)
                                                {
                                                    wndRelaxMode.bForceClose = true;
                                                    wndRelaxMode.Close();
                                                    wndHabits.WindowState = WindowState.Maximized;
                                                }
                                                //If the above is null, finish screen may be visible, as ShowACtivityFinishedScreen() make is null
                                                LogWriter.LogWrite("wndHabits.Hide()");
                                                wndHabits.Hide();
                                                appSettings.RemoveSecondaryWindows();

                                                LogWriter.LogWrite("EVENING_HABITS STATE - now > myCutoffTime - Show Alert");
                                                FBMessageBox msg = new FBMessageBox(appSettings.ConfigFileStrings["common"]["cutOffTimePassedText"].ToString());
                                                msg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                                                msg.ShowDialog();

                                                wndHabits.Show();
                                                appSettings.bSkipCurrentActivityCantDoItToday = true;
                                                LogWriter.LogWrite("appSettings.bSkipCurrentActivityCantDoItToday = true; - After cutoff time");
                                                wndHabits.ShowACtivityFinishedScreen(true);
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
                        return;


                    if (RegistryManager.GetMorningHabitsShownDate())
                    {
                        if (appSettings.MorningHabitsShownDate.Date == DateTime.Now.Date)
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
                        return;

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

            if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
            {
                LogWriter.LogWrite("ShowMorningRoutine() CurrentActivityType.MORNING_HABITS return");
                return; //Already showing morning routine
            }
            
            //If still shutoff screen is shown, remove it.
            if (appSettings.nCurrentActivityType == CurrentActivityType.SHUT_OFF)
            {
                LogWriter.LogWrite("ShowMorningRoutine() If still shutoff screen is shown, remove it.");

                try
                {
                    if (wndShutOff != null)
                    {
                        wndShutOff.Close();
                        wndShutOff = null;
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("appSettings.nCurrentActivityType == CurrentActivityType.SHUT_OFF wndShutOffex - " + ex.Message);
                }
               
            }

            //If still evening habits screen is shown, remove it.
            if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
            {
                LogWriter.LogWrite("ShowMorningRoutine() If still EVENING_HABITS screen or focus summary is shown, remove it.");
                try 
                { 
                    if (wndHabits != null)
                    {
                        wndHabits.Close();
                        wndHabits = null;
                    }
                    if (wndFocusSummary != null)
                    {
                        wndFocusSummary.Close();
                        wndFocusSummary = null;
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS wndHabits  / wndFocusSummary ex- " + ex.Message);
                }

            }

            try 
            { 
                if (wndEasyPostpone != null)
                {
                    wndEasyPostpone.Close();
                    wndEasyPostpone = null;
                }

                //In any case if there is a habit window left from system sleep and then wakeup, and if nCurrentActivityType is NONE 
                //we must check the existing window and close it as we are going to create a new one further.
                if (appSettings.nCurrentActivityType == CurrentActivityType.NONE)
                {
                    if (wndHabits != null)
                    {
                        wndHabits.Close();
                        wndHabits = null;
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(" wndEasyPostpone.Close();  /  wndHabits.Close(); ex- " + ex.Message);
            }

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
                    appSettings.MorningHabitsShownDate = DateTime.Now;
                    RegistryManager.SetMorningHabitsShownDate();

                    appSettings.nCurrentActivityType = CurrentActivityType.NONE;
                    LogWriter.LogWrite(" appSettings.nCurrentActivityType = CurrentActivityType.NONE;");

                    //Start the break Activities Timer
                    StartBreakActivityTimer();

                    return;
                }

                if (((App)(Application.Current)).wndTakeABreak != null && ((App)(Application.Current)).wndTakeABreak.IsVisible)
                {
                    ((App)(Application.Current)).wndTakeABreak.HideBreakWindowAndStopTimer();
                }

                LogWriter.LogWrite("ShowMorningRoutine() - show morning habits screen now");

                appSettings.nCurrentActivityType = CurrentActivityType.MORNING_HABITS;
                LogWriter.LogWrite("appSettings.nCurrentActivityType = CurrentActivityType.MORNING_HABITS;");

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
                                                    appSettings.MorningHabitsShownDate = DateTime.Now;
                                                    RegistryManager.SetMorningHabitsShownDate();

                                                    appSettings.nCurrentActivityType = CurrentActivityType.NONE;
                                                    LogWriter.LogWrite(" appSettings.nCurrentActivityType = CurrentActivityType.NONE;");

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

                                                                if (sLastActivityDone.ContainsKey("current_sequence_skipped_activities"))
                                                                {
                                                                    //string skippedActivities = sLastActivityDone["current_sequence_skipped_activities"].ToString();
                                                                    string sArraySkipped = sLastActivityDone["current_sequence_skipped_activities"].ToString();
                                                                    if (sArraySkipped.Length > 0)
                                                                        appSettings.arraySkipped = (JArray)sLastActivityDone["current_sequence_skipped_activities"];
                                                                }

                                                                for (int i = 0; i < appSettings.arrAllMorningHabits.Count; i++)
                                                                {
                                                                    if (appSettings.arrAllMorningHabits[i].id == activityId) //current activity to be done
                                                                    {
                                                                        appSettings.nHabitIndex = i - 1; //We will assign the habit index from the previous activity and show the finish screen for that activity
                                                                                                         //Now we do not need to start normal but need to show the finish screen of this
                                                                        appSettings.bShowActivitiesFromMid = true;
                                                                        LogWriter.LogWrite("nHabitIndex = " + appSettings.nHabitIndex.ToString());

                                                                        for (int m = 0; m < appSettings.arraySkipped.Count; m++)
                                                                        {
                                                                            //Check if the last activity was in the array skipped list
                                                                            if (appSettings.arraySkipped[m].ToString() == appSettings.arrAllMorningHabits[i - 1].id)
                                                                            {
                                                                                appSettings.bShowActivitiesFromMidLastSkipped = true;
                                                                                break;
                                                                            }
                                                                        }

                                                                        break;
                                                                    }
                                                                }
                                                            }
                                                            else
                                                            {
                                                                //it means current activity is not morning habit and morning habit done for today
                                                                LogWriter.LogWrite("Current Activity is not morning habit and it means morning habit done for today");
                                                                appSettings.MorningHabitsShownDate = DateTime.Now;
                                                                RegistryManager.SetMorningHabitsShownDate();

                                                                appSettings.nCurrentActivityType = CurrentActivityType.NONE;
                                                                LogWriter.LogWrite(" appSettings.nCurrentActivityType = CurrentActivityType.NONE;");

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

            try
            {
                LogWriter.LogWrite("ShowMorningRoutine - Now show the HabitWindow");

                wndHabits = new HabitWindow();
               
                wndHabits.Topmost = true;
                wndHabits.Show();
                wndHabits.Activate();
                wndHabits.Focus();

                ShowBlankWindowOnOtherScreens();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowMorningRoutine" + ex.Message);
            }

        }

        public async void ShowEveningRoutine()
        {
            LogWriter.LogWrite("ShowEveningRoutine() func");

            if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
            {
                LogWriter.LogWrite("ShowEveningRoutine() - appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS return");
                return; //Already showign morning routine
            }
           
            if(wndEasyPostpone != null)
            {
                wndEasyPostpone.Close();
                wndEasyPostpone = null;
            }
            //We need to check if anything is going on and kill that first
            if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS ||
                appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
            {
                if (wndHabits != null)
                {
                    LogWriter.LogWrite("wndHabits.Close();");
                    wndHabits.Close();
                    wndHabits = null;
                }
            }

            //In any case if there is a habit window left from system sleep and then wakeup, and if nCurrentActivityType is NONE 
            //we must check the existing window and close it as we are going to create a new one further.
            //Deon's logs helped
            if (appSettings.nCurrentActivityType == CurrentActivityType.NONE)
            {
                if (wndHabits != null)
                {
                    wndHabits.Close();
                    wndHabits = null;
                }
            }


            appSettings.nCurrentActivityType = CurrentActivityType.EVENING_HABITS; //We use this for Focus Summary as well, when FOcus summary ends we change this to none
            LogWriter.LogWrite("appSettings.nCurrentActivityType = CurrentActivityType.EVENING_HABITS;");

            appSettings.bShowActivitiesFromMid = false;
            LogWriter.LogWrite("appSettings.bShowActivitiesFromMid = false;");
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
            appSettings.arrAllEveningHabits.Clear();
            DayOfWeek Today = DateTime.Now.DayOfWeek;
            for (int i = 0; i < appSettings.arrGLOBALAllEveningHabits.Count; i++)
            {
                for (int m = 0; m < appSettings.arrGLOBALAllEveningHabits[i].DaysOfWeek.Count(); m++)
                {
                    if (appSettings.arrGLOBALAllEveningHabits[i].DaysOfWeek[m].ToLower() == "all")
                    {
                        appSettings.arrAllEveningHabits.Add(appSettings.arrGLOBALAllEveningHabits[i]);
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
                            appSettings.arrAllEveningHabits.Add(appSettings.arrGLOBALAllEveningHabits[i]);
                            break;
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
                                            //DateTime now = DateTime.Now;
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

                                                            if (sLastActivityDone.ContainsKey("current_sequence_skipped_activities"))
                                                            {
                                                                string sArraySkipped = sLastActivityDone["current_sequence_skipped_activities"].ToString();
                                                                if(sArraySkipped.Length > 0)
                                                                    appSettings.arraySkipped = (JArray)sLastActivityDone["current_sequence_skipped_activities"];
                                                            }

                                                            for (int i = 0; i < appSettings.arrAllEveningHabits.Count; i++)
                                                            {
                                                                if (appSettings.arrAllEveningHabits[i].id == activityId)
                                                                {
                                                                    appSettings.nHabitIndex = i - 1; //We will assign the habit index from the previous actiity and show the finish screen for that activity
                                                                    //Now we do not need to start normal but need to show the finish screen of this
                                                                    appSettings.bShowActivitiesFromMid = true;
                                                                    LogWriter.LogWrite("nHabitIndex = " + appSettings.nHabitIndex.ToString());

                                                                    for (int m = 0; m < appSettings.arraySkipped.Count; m++)
                                                                    {
                                                                        //Check if the last activity was in the array skipped list
                                                                        if (appSettings.arraySkipped[m].ToString() == appSettings.arrAllEveningHabits[i-1].id)
                                                                        {
                                                                            appSettings.bShowActivitiesFromMidLastSkipped = true;
                                                                            break;
                                                                        }
                                                                    }

                                                                    break;
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


            LogWriter.LogWrite("ShowEveningRoutine() - show evening habits screen now");

            try
            {
                wndHabits = new HabitWindow();
                wndHabits.Topmost = true;
                wndHabits.Show();
                wndHabits.Activate();
                wndHabits.Focus();
                ShowBlankWindowOnOtherScreens();
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
                return; //Already showign morning routine
            }

            appSettings.nCurrentActivityType = CurrentActivityType.SHUT_OFF;
            LogWriter.LogWrite("appSettings.nCurrentActivityType = CurrentActivityType.SHUT_OFF;");

            wndShutOff = new ShutOffWindow();
            wndShutOff.Topmost = true;
            wndShutOff.Show();
            wndShutOff.Activate();
            wndShutOff.Focus();
            ShowBlankWindowOnOtherScreens();
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

        public void ShowPreferencesWindow()
        {
            if (wndPreferences == null)
            {
                wndPreferences = new PreferencesWindow();
            }
            wndPreferences.Show();
            wndPreferences.BringIntoView();

        } 
        public void ShowFocusEndWindow()
        {
            if (wndFocusModeEnd == null)
            {
                wndFocusModeEnd = new FocusModeEnd();
            }
            wndFocusModeEnd.Show();
            wndFocusModeEnd.BringIntoView();

        }
        public void ShowBreakWindow()
        {
            LogWriter.LogWrite("ShowBreakWindow ()");

            if (wndTakeABreak == null)
            {
                wndTakeABreak = new BreakWindow();
            }
            wndTakeABreak.Show();
            wndTakeABreak.BringIntoView();
            wndTakeABreak.ShowBreakWindowAndStartTimer(300);

        }

        public void ShowRelaxActivityWindow(int nTime)
        {
            if (wndRelaxMode == null)
            {
                wndRelaxMode = new RelaxModeWindow(nTime);
            }
            wndRelaxMode.Show();
            wndRelaxMode.BringIntoView();
            //wndRelaxMode.ShowBrainDumpWindowAndStartTimer();
        }

        public void ShowBreakWindowForAllowedAppsActivity(int nDuration, string sTitle)
        {
            if (wndTakeABreak == null)
            {
                wndTakeABreak = new BreakWindow();
            }
            wndTakeABreak.bShowingForNonTimeActivities = true;
            wndTakeABreak.Show();
            wndTakeABreak.BringIntoView();
            wndTakeABreak.ShowBreakWindowAndStartTimer(nDuration, sTitle);

        }

        public void ShowEasyPostponeWindow()
        {
            //always create new
            wndEasyPostpone = new EasySkipPostpone();
            wndEasyPostpone.Show();
            wndEasyPostpone.BringIntoView();
 
        }

        public void PostponeHabitWork(int nMinutesToPostpone)
        {
            LogWriter.LogWrite("PostponeHabitWork()");

            try
            {
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
                    dispatcherTimerPostponeHabits.Interval = new TimeSpan(0, nMinutesToPostpone, 0);
#else
                    dispatcherTimerPostponeHabits.Interval = new TimeSpan(0,  nMinutesToPostpone, 0);
#endif
                    dispatcherTimerPostponeHabits.Start();

                    appSettings.bHabitsPostponedState = true;
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

            if(nMinutesToPostpone > 2) //If the piostpone time is more than 2 minutes, we need to show a popup bfore the postpone time is over and the habits are resumed.
            {
                SetShowingYourHabitsResumeInPopup(nMinutesToPostpone - 2);
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
                LogWriter.LogWrite(" appSettings.bHabitsPostponedState = false;");

                //Simply set the activity type to NONE and the background thread will take care.
                appSettings.nCurrentActivityType = CurrentActivityType.NONE;
                LogWriter.LogWrite(" appSettings.nCurrentActivityType = CurrentActivityType.NONE;");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PostponeHabitWork - " + ex.Message);
            }

        }
        public void EasyPostponeCancelled()
        {
            LogWriter.LogWrite("EasyPostponeCancelled()");
            try
            {
                if (wndHabits != null)
                {
                    wndHabits.Show();
                    wndHabits.EasyPostponeCancelled(); //enable the timers

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
            { //Just make sure that we have the habits time or breaks time
                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS || appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS || appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                {
                    if(appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS && wndFocusSummary != null && wndFocusSummary.IsVisible)
                    {
                        wndFocusSummary.SimulateNeedOneMinute_Click();
                    }
                    else if (wndHabits != null && wndHabits.IsVisible)
                    {
                        wndHabits.SimulatePleaseSpareMeClick();
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("APP SimulatePleaseSpareMe() ex - " + ex.Message);
            }
        }
        public async void ShowFocusSummaryWindow()
        {
            LogWriter.LogWrite("ShowFocusSummaryWindow()");
            try
            {
                appSettings.sActivityAllDaySummaryData = "";

                FocusBearAPIClient client = new FocusBearAPIClient();
                var xResponse = await client.GetActivityAllDaySummary();


                if (wndFocusSummary == null)
                {
                    wndFocusSummary = new FocusActivitySummary();
                }
                ShowBlankWindowOnOtherScreens();
                wndFocusSummary.ShowDialog();
                wndFocusSummary.BringIntoView();
                appSettings.RemoveSecondaryWindows();
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
                            LogWriter.LogWrite("A new version of Focus Bear is available"); //TODO Win Text
                            {
                                //todo Win Text
                                string displayMsg = "A new version of Focus Bear is available. Do you wish to update to the latest version of Focus Bear?";
                                FBMessageBox ff = new FBMessageBox(displayMsg, "New Update Available", MessageBoxButton.YesNo);
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
                                string displayMsg = "You are already running the latest version of Focus Bear."; //TODO Win Text
                                FBMessageBox ff = new FBMessageBox(displayMsg, "No New Update Available");
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
            if (wndBrainDump == null)
            {
                wndBrainDump = new BrainDumpWindow();
            }
            wndBrainDump.Show();
            wndBrainDump.BringIntoView();
            wndBrainDump.ShowBrainDumpWindowAndStartTimer();
        }
        public void ShowFocusProgressWindow()
        {
            try
            {
                if (wndFocusProgress == null)
                {
                    wndFocusProgress = new FocusProgress();
                }
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
            if (wndFocusProgress != null)
            {
                wndFocusProgress.Close();
                wndFocusProgress = null;
            }
        }
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            LogWriter.LogWrite("Application_Exit()");
            try
            {
                LogWriter.LogWrite("Application_Exit() 1");
                if (trayIcon != null)
                {
                    trayIcon.Dispose();
                }
                // We dispose of the control so the internal WebView objects are released
                // and the associated browser process exits.
                if (wndPreferences != null)
                {
                    if(wndPreferences.PageEditHabits != null)
                    {
                        wndPreferences.PageEditHabits.webView.Dispose();
                    }
                }
                LogWriter.LogWrite("Application_Exit() 2");

                appMutex.ReleaseMutex();
                appMutex.Dispose();
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

                LogWriter.LogWrite("Application_Exit() done");

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Application_Exit() EX - " + ex.Message);
            }
            if (appSettings.bRestartAppNeeded)
            {
                string currentAppPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                Process.Start(currentAppPath, "/restartbyself");
                LogWriter.LogWrite(" Process.Start FIRED!");
            }
        }

        public void ShowBlankWindowOnOtherScreens()
        {
            LogWriter.LogWrite("ShowBlankWindowOnOtherScreens()");
            //WE will place a small check here
            //if its not any activity time, this should not execute
            if(appSettings.nCurrentActivityType == CurrentActivityType.NONE)
            {
                LogWriter.LogWrite("return from ShowBlankWindowOnOtherScreens() as appSettings.nCurrentActivityType == CurrentActivityType.NONE");
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

            ResumeAfterPostponeFinished();

        }
        void PauseHabitsOnSystemSleep()
        {
            LogWriter.LogWrite("PauseHabitsOnSystemSleep()");
            if (wndHabits != null)
            {
                if (wndHabits.PageHabitActivityOnGoing != null)
                {
                    wndHabits.PageHabitActivityOnGoing.HandleTimerForPostpone(true); //Stop the timer
                }
                LogWriter.LogWrite(" wndHabits.Hide()");
                wndHabits.Hide();

                appSettings.RemoveSecondaryWindows();
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
                msg.ShowDialog();

                LogWriter.LogWrite("Restart app now for settings to take Effect!");

                appSettings.bRestartAppNeeded = true;

                Application.Current.Shutdown(0);
            }
        }
    }
}
