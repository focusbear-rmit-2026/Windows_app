using Focus_Bear.API;
using Focus_Bear.Focus;
using Focus_Bear.Habits;
using Focus_Bear.Login;
using Focus_Bear.Models;
using Focus_Bear.Onboarding;
using Focus_Bear.Preferences;
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
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace Focus_Bear
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        AppSettings appSettings = AppSettings.GetInstance;

        private const string EventMutex = "818AB7BF-E9CF-43D6-8C15-95642441ED50";
        private const string MutexGUID = "369F3719-1C05-41A1-B9E1-FC0CF0FCE3AE";

        private EventWaitHandle appWaitHandle;
        private Mutex appMutex;


        WinEventDelegate dele = null;

        delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
        [DllImport("user32.dll")]
        static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        private const uint WINEVENT_OUTOFCONTEXT = 0;
        private const uint EVENT_SYSTEM_FOREGROUND = 3;

        public void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            //Always get the last active app name when the menu is clicked and ignore Focus Bear
            string sAppName = GetLastActiveAppName();
            /// if (sAppName.Length > 0 && sAppName != "Focus Bear" && sAppName != "Windows Explorer")
            if (sAppName.Length > 0 && sAppName != "Focus Bear.exe" && sAppName != "explorer.exe")
            {
                appSettings.sLastActiveAppName = sAppName;
            }

            Debug.WriteLine(appSettings.sLastActiveAppName);
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
           // LogWriter.LogWrite("GetLastActiveAppName()");
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
                ///FileVersionInfo appInfo = FileVersionInfo.GetVersionInfo(exePath);
                ///return String.IsNullOrEmpty(appInfo.FileDescription) ? Path.GetFileName(exePath) : appInfo.FileDescription;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetLastActiveAppName() - " + ex.Message);
                Debug.WriteLine(ex.Message);
            }

            return "";
        }


        DispatcherTimer dispatcherTimerBreaks = new DispatcherTimer();
        DispatcherTimer dispatcherTimerBackground = new DispatcherTimer();
        DispatcherTimer dispatcherTimerPostponeHabits = new DispatcherTimer();

        public WinForms.NotifyIcon trayIcon = null;

        bool isMorningHabitsAlreadyShown = false;

        HabitWindow wndHabits;
        ShutOffWindow wndShutOff;

        public PreferencesWindow wndPreferences = null;
        public TrayMenu wndTrayMenu = null;

        public FocusModeEnd wndFocusModeEnd = null;

        public BreakWindow wndTakeABreak = null;

        public FocusActivitySummary wndFocusSummary = null;

        public EasySkipPostpone wndEasyPostpone = null;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            LogWriter.LogWrite( string.Format("App Started - Version {0}", appSettings.AppVersion.ToString()) );


            //output = HttpUtility.UrlEncode(output);//
            //FocusBearAPIClient client = new FocusBearAPIClient();
            //var x = client.CapturePostHog("login-universal", "");
            //var x = client.GetActivityAllDaySummary();
            ///
            if (e.Args.Length > 0)
            {
                if (e.Args[0] == "/startupYes")
                {
                    SetAtWindowsStartup();
                    Application.Current.Shutdown(0);
                    return;
                }
            }

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
     

            SetAtWindowsStartup();

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            //Ensuring Autoplay video in webview
            Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", "--autoplay-policy=no-user-gesture-required");

            //GEt the Strings File so that text can be loaded on all screens
#if DEBUG

 
            //Global.DeleteFile(appSettings.sConfigFilePath, false);
            //File.Copy(@"config.json", appSettings.sConfigFilePath);
            //appSettings.ConfigFileStrings = JObject.Parse(File.ReadAllText(appSettings.sConfigFilePath));

            //appSettings.sRawUserSettings = File.ReadAllText("D:\\Manish\\FocusBear\\settings.json");
            //appSettings.ReadAndParseSettingsJson();

#else
           /// appSettings.DownloadAndLoadConfigFile();

#endif
            appSettings.DownloadAndLoadConfigFile();

            /////////////////////////////////////////// Testing Phase
            ///
#if DEBUG

            //Read from Local file for DEBUG vewrsion for testing

            //// appSettings.sRawUserSettings = File.ReadAllText("D:\\Manish\\FocusBear\\settings.json");
            ///appSettings.ReadAndParseSettingsJson();
#endif
            ///////////////////////////////////////////
 
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
                Debug.WriteLine(ex.Message);
            }
           
        }
        public void ProceedAfterDownloadingConfigFile()
        {
            LogWriter.LogWrite("ProceedAfterDownloadingConfigFile()");

            var test = appSettings.ConfigFileStrings["welcomeInfo"]["introActivities"].Children(); ;
            JArray array = (JArray)appSettings.ConfigFileStrings["welcomeInfo"]["introActivities"][0]["video_urls"];
            int n = array.Count;
            string ss = array[0].ToString();

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

            if (!RegistryManager.GetWelcomeAboardingDoneStatus())
            {
                LogWriter.LogWrite("GetWelcomeAboardingDoneStatus NO");
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
                    new TrayBalloon(0, "Focus Bear is running in Background").Show(); //TODO Win Text
                    using (Stream iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Focus Bear;component/Images/focus-bear.ico")).Stream)
                    {
                        trayIcon.Icon = new System.Drawing.Icon(iconStream);
                    }

                    trayIcon.MouseClick += TrayIcon_MouseClick;
                    //trayIcon.DoubleClick += DoubleClick;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("InitializeTrayIcon() Exception" + ex.Message);
               //Debug.WriteLine(ex.Message);
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
                    //Application.Current.MainWindow = menu;
                }
                //else if (e.Button == System.Windows.Forms.MouseButtons.Left)
                //{

                //    trayIcon.Dispose();
                //    trayIcon = null;
                //}
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
                Debug.WriteLine(ex.Message);
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
                Debug.WriteLine(ex.Message);
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

                LogWriter.LogWrite("Show Break Activities Warning");
                BreakWarning windBreakWarning = new BreakWarning();
                windBreakWarning.WindowStartupLocation = WindowStartupLocation.Manual;
                windBreakWarning.Topmost = true;
                bool? bReturn = windBreakWarning.ShowDialog();

                switch (bReturn)
                {
                    case true:
                        // User clicked postpone 5 minutes button
                        StartBreakActivityTimer(true, 5 );
                        break;
                    case false:
                        // User did not postponed the breaks..normal flow follows
                        break;
                    default:
                        // Indeterminate
                        break;
                }
                if (bReturn.Value == true)
                    return;
 
                ShowBreakActivities();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("dispatcherTimer_Breaks Exception" + ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }
   

        public async void ShowBreakActivities()
        {
            LogWriter.LogWrite("ShowBreakActivities()");

            appSettings.nCurrentActivityType = CurrentActivityType.BREAK_ACTIVITY;

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
                                Debug.WriteLine(ex.Message);

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
                    Debug.WriteLine(ex.Message);
                }

                ///


                wndHabits = new HabitWindow();
                wndHabits.Topmost = true;
                wndHabits.Show();
                wndHabits.Activate();
                wndHabits.Focus();
            }
        }
        public void ShowWelcomeAbordOnBoradingFlow()
        {
            LogWriter.LogWrite("ShowWelcomeAbordOnBoradingFlow");

            WelcomeAbord windWelcome = new WelcomeAbord();
            windWelcome.Topmost = true;
            windWelcome.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            windWelcome.Show();
            windWelcome.Activate();
            windWelcome.Focus();
            //Application.Current.MainWindow = windWelcome;
        }

        public void ShowOnBoradingFlow()
        {
            LogWriter.LogWrite("ShowOnBoradingFlow");

            OnBoardingWindow windOnBoarding = new OnBoardingWindow();
            windOnBoarding.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            windOnBoarding.Topmost = true;
            windOnBoarding.Show();
            windOnBoarding.Activate();
            windOnBoarding.Focus();
            //Application.Current.MainWindow = windWelcome;
        }

        public void ShowLoginWindow()
        {
            LogWriter.LogWrite("ShowLoginWindow");

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
            // MainWindow = windLogin;
            windOnBoardingVid.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            windOnBoardingVid.Topmost = true;
            windOnBoardingVid.Show();
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

            if (!RegistryManager.GetOnboardingVideoShownStatus())
            {
                RegistryManager.SetOnboardingVideoShownStatus();
                //Show Onboarding Video
                ShowOnBoradingVideo();
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

        public async void  ProeedAfterGettingDeviceID()
        {
            LogWriter.LogWrite("ProeedAfterGettingDeviceID");

            if (appSettings.isExpired)
            {
                LogWriter.LogWrite("isExpired TRUE");

                ShowExpiredNag(true);
                //return;
            }
            //Get local settings
           
            //Get Focus setings aka user details

            //Copy all graphs to local
            appSettings.CopyGraphsToLocal();

            //Write the Default CSV file with headers
            appSettings.WriteDefaultCSVFile();

            //GEt the list of Installed app
            var engine = new AppsEngine();// SmartUninstallerEngine(_cts.Token, _PauseResetEvent);
            engine.GetData();

            //Start the handler fro Blocking Global URLS / Apps etc.
            appSettings.automation = new CUIAutomation();
            appSettings.handler = new FocusChangeHandler();
            appSettings.automation.AddFocusChangedEventHandler(null, appSettings.handler);
            //appSettings.automation.RemoveFocusChangedEventHandler(appSettings.handler);

            //Initialize the Tray Icon
            InitializeTrayIcon();

             //Get User Settings
            FocusBearAPIClient client = new FocusBearAPIClient();

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



            LogWriter.LogWrite("attempt to Get User DETAILS");
            var userDetailsResponse = await client.GetUserDetailsInfo();
            int y  = int.Parse(userDetailsResponse.ToString());
            if (y == 1)
            {
                LogWriter.LogWrite("GetUserDetailsInfo Success");
                //User SEttings retrieved Successfully, Process them.
                appSettings.ReadAndParseUserDetailsJson();
            }
            else
            {
                LogWriter.LogWrite("GetUserDetailsInfo Failed");
                appSettings.ReadAndParseUserDetailsJson();  
            }

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


            //One the USer details have been parsed and focus mode names have been fetched, we will check for default focus modes, if not present we will create them
            appSettings.ReadAndCreateDefaultFocusModes();

            StartWorkFlow();

        }

        public void StartWorkFlow()
        {
            LogWriter.LogWrite("StartWorkFlow()");

            ///FocusBearAPIClient client = new FocusBearAPIClient();
            /// var x = client.GetActivityCurrentDayStats("49e4a673-4817-44e5-8d9c-b39e869148c3", "");
#if DEBUG
            //StartFocusMode menu = new StartFocusMode();
            //menu.Topmost = true;
            //menu.ShowDialog();
            // ShowFocusSummaryWindow();
            //ShowBreakActivities();
            // appSettings.CapturePostHog("deactivate-routine", "\"routine-type\":\"evening_routine\",\"deactivate_reason\":\"easy postpone\"");
           // GentleBlock gg = new GentleBlock();
           //// gg.Show();
           // gg.ShowGentleBlockWindowAndStartTimer("Meetings", "testapp","kya intent");
           // gg.ShowDialog();

#endif
            dele = new WinEventDelegate(WinEventProc);
            IntPtr m_hhook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, dele, 0, 0, WINEVENT_OUTOFCONTEXT);


            if (appSettings.IsFirstInstall)
            {
                LogWriter.LogWrite("appSettings.IsFirstInstall true");
                //No need to show the Morning Habits
                //WE will set the morning habits shown date for today
                appSettings.MorningHabitsShownDate = DateTime.Now;
                RegistryManager.SetMorningHabitsShownDate();

                //Start the break Activities Timer
                //StartBreakActivityTimer();
            }
           // else
            {

                //On activity Finished, do clear the Postponed variables
                RegistryManager.RemovePostponedHabitData();


                //LogWriter.LogWrite("appSettings.IsFirstInstall false");
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
                    Debug.WriteLine(ex.Message);
                }

            }
        }
        private void dispatcherTimer_Background(object sender, EventArgs e)
        {
            try
            {
                CommandManager.InvalidateRequerySuggested();

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
                    if (appSettings.nCurrentActivityType == CurrentActivityType.SHUT_OFF || appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                    {
                        LogWriter.LogWrite("SHUT_OFF or EVENING_HABITS - Return from timer");
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
                        ShowEveningRoutine();
                    }
                }
                else
                {
                    //Here first we will check if the app is deactivated then we need not show the Morning routine or the break activities
                    if (appSettings.IsAppDeactivated())
                        return;
                    
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
            if(appSettings.arrAllMorningHabits.Count == 0)
            {
                LogWriter.LogWrite("ShowMorningRoutine() arrAllMorningHabits.Count == 0 StartBreakActivityTimer and return");

                //WE will set the morning habits shown date for today
                appSettings.MorningHabitsShownDate = DateTime.Now;
                RegistryManager.SetMorningHabitsShownDate();

                //Start the break Activities Timer
                StartBreakActivityTimer();

                return;
            }

            
            //If still shutoff screen is shown, remove it.
            if (appSettings.nCurrentActivityType == CurrentActivityType.SHUT_OFF)
            {
                LogWriter.LogWrite("ShowMorningRoutine() If still shutoff screen is shown, remove it.");

                if (wndShutOff != null)
                {
                    wndShutOff.Close();
                    wndShutOff = null;
                }
            }

            //If still evening habits screen is shown, remove it.
            if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
            {
                LogWriter.LogWrite("ShowMorningRoutine() If still EVENING_HABITS screen or focus summary is shown, remove it.");
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

            if (wndEasyPostpone != null)
            {
                wndEasyPostpone.Close();
                wndEasyPostpone = null;
            }

            LogWriter.LogWrite("ShowMorningRoutine() - show morning habits screen now");

            appSettings.nCurrentActivityType = CurrentActivityType.MORNING_HABITS;

            appSettings.nHabitIndex = 0;

            //Here first we will check if we have some pending sequence or if some activities from morning routine are done.
            //If some are done, we will continue from there..
            //if morning routine for today is completed, we will start break activities timer
            //if there is some pending sequnce from any previous days, we will force complete it.
            
            FocusBearAPIClient client = new FocusBearAPIClient();
            //var x = client.ForceCompleteCurrentSequence("49e4a673-4817-44e5-8d9c-b39e869148c3");
            //var x = client.GetActivityAllDaySummary();

     

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
                                                                //int n = appSettings.arraySkipped.Count;
                                                                //if (skippedActivities.Length > 0)
                                                                //{
                                                                //   //????
                                                                //}
                                                            }

                                                            ////startHabitsCall(activityId: activityId, skippedActivities: skippedActivities);
                                                            for (int i = 0; i < appSettings.arrAllMorningHabits.Count; i++)
                                                            {
                                                                if (appSettings.arrAllMorningHabits[i].id == activityId)
                                                                {
                                                                    appSettings.nHabitIndex = i - 1 ; //We will assign the habit index from the previous actiity and show the finish screen for that activity
                                                                    //Now we do not need to start normal but need to show the finish screen of this
                                                                    appSettings.bShowActivitiesFromMid = true;

                                                                    for (int m = 0; m < appSettings.arraySkipped.Count; m++)
                                                                    {
                                                                        if (appSettings.arraySkipped[m].ToString() == appSettings.arrAllMorningHabits[appSettings.nHabitIndex].id)
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

                                                            return;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        LogWriter.LogWrite("Pending routine is not from current day clean all running sequence");
                                                        var sResponse1 = await client.ForceCompleteCurrentSequence(sequenceId);
                                                        int x1 = int.Parse(sResponse1.ToString());
                                                        if (x1 == 1)
                                                        {
                                                            ////Start Default
                                                        }

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
                                                int x1 = int.Parse(sResponse1.ToString());
                                                if (x1 == 1)
                                                {
                                                    ////Start Default
                                                }
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
                            LogWriter.LogWrite("Start new Mmorning routine sequence :- current_activity_assigned_at not found");
                            //Start Default
                        }

                        // 
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("GetLastActivityInfo JSON PARSING FAILED" + ex.Message);
                        Debug.WriteLine(ex.Message);

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
                LogWriter.LogWrite("ShowMorningRoutine" + ex.Message);
                Debug.WriteLine(ex.Message);
            }

        }

        public async void ShowEveningRoutine()
        {
            LogWriter.LogWrite("ShowEveningRoutine()");

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
                    wndHabits.Close();
                    wndHabits = null;
                }
            }

            appSettings.nCurrentActivityType = CurrentActivityType.EVENING_HABITS; //We use this for Focus Summary as well, when FOcus summary ends we change this to none

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

            if (appSettings.arrAllEveningHabits.Count == 0)
            {
                LogWriter.LogWrite("ShowEveningRoutine() - appSettings.arrAllEveningHabits.Count == 0 ShowShutOffScreen and return");

                ShowShutOffScreen(); //IF there are no evening habits, skip to shutoff.
                return;
            }

            //Set initial Habit Index to 0;
            appSettings.nHabitIndex = 0;

            //Here first we will check if we have some pending sequence or if some activities from morning routine are done.
            //If some are done, we will continue from there..
            //if morning routine for today is completed, we will start break activities timer
            //if there is some pending sequnce from any previous days, we will force complete it.

            FocusBearAPIClient client = new FocusBearAPIClient();
            
            var sResponse = await client.GetLastActivityInfo();
            int x = int.Parse(sResponse.ToString());
            if (x == 1)
            {
                LogWriter.LogWrite("GetLastActivityInfo Success - checking before evening routine");

                if (appSettings.sLastActivityDoneInfo != null || appSettings.sLastActivityDoneInfo.Length > 0)
                {
                    //Parse the JSON string
                    try
                    {
                        JObject sLastActivityDone = JObject.Parse(appSettings.sLastActivityDoneInfo);

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
                                                                //int n = appSettings.arraySkipped.Count;
                                                            }

                                                            ////startHabitsCall(activityId: activityId, skippedActivities: skippedActivities);
                                                            for (int i = 0; i < appSettings.arrAllEveningHabits.Count; i++)
                                                            {
                                                                if (appSettings.arrAllEveningHabits[i].id == activityId)
                                                                {
                                                                    appSettings.nHabitIndex = i - 1; //We will assign the habit index from the previous actiity and show the finish screen for that activity
                                                                    //Now we do not need to start normal but need to show the finish screen of this
                                                                    appSettings.bShowActivitiesFromMid = true;

                                                                    for (int m = 0; m < appSettings.arraySkipped.Count; m++)
                                                                    {
                                                                        if (appSettings.arraySkipped[m].ToString() == appSettings.arrAllEveningHabits[appSettings.nHabitIndex].id)
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
                                                        int x1 = int.Parse(sResponse.ToString());
                                                        if (x1 == 1)
                                                        {
                                                            ////Start Default
                                                        }

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
                                                int x1 = int.Parse(sResponse.ToString());
                                                if (x1 == 1)
                                                {
                                                    ////Start Default
                                                }
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
                        Debug.WriteLine(ex.Message);

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
                Debug.WriteLine(ex.Message);
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

            wndShutOff = new ShutOffWindow();
            wndShutOff.Topmost = true;
            wndShutOff.Show();
            wndShutOff.Activate();
            wndShutOff.Focus();
            ShowBlankWindowOnOtherScreens();
        }

      

        public void ShowExpiredNag(bool bStarting = false)
        {
            LogWriter.LogWrite("ShowExpiredNag()");

            UpgradeNow up = new UpgradeNow();
            bool? bResult = up.ShowDialog();

            if (bStarting)
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
            if (wndTakeABreak == null)
            {
                wndTakeABreak = new BreakWindow();
            }
            wndTakeABreak.Show();
            wndTakeABreak.BringIntoView();
            wndTakeABreak.ShowBreakWindowAndStartTimer(300);

        }

        public void ShowBreakWindowForAllowedAppsActivity(int nDuration, string sTitle)
        {
            if (wndTakeABreak == null)
            {
                wndTakeABreak = new BreakWindow();
            }
            wndTakeABreak.Show();
            wndTakeABreak.BringIntoView();
            wndTakeABreak.ShowBreakWindowAndStartTimer(nDuration, sTitle);

        }

        public void ShowEasyPostponeWindow()
        {
           // if (wndEasyPostpone == null) //always create new
            {
                wndEasyPostpone = new EasySkipPostpone();
            }
            wndEasyPostpone.Show();
            wndEasyPostpone.BringIntoView();
 
        }

        public void PostponeHabitWork(int nMinutesToPostpone)
        {
            LogWriter.LogWrite("PostponeHabitWork()");

            try
            {
                //wndHabits.Hide();

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
                    dispatcherTimerPostponeHabits.Interval = new TimeSpan(0, 0, nMinutesToPostpone);
#else
                    dispatcherTimerPostponeHabits.Interval = new TimeSpan(0,  nMinutesToPostpone, 0);
#endif
                    dispatcherTimerPostponeHabits.Start();
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

        }

 

        private void dispatcherTimer_Postpone(object sender, EventArgs e)
        {
            LogWriter.LogWrite("dispatcherTimer_Postpone()");
            try
            {
                dispatcherTimerPostponeHabits.Stop(); //Tick Once only

                CommandManager.InvalidateRequerySuggested();

                ResumeAfterPostponeFinished();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("dispatcherTimer_Postpone Exception" + ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }


        public void ResumeAfterPostponeFinished()
        {
            LogWriter.LogWrite("ResumeAfterPostponeFinished()");

            try
            {
                //Simply set the activity type to NONE and the background thread will take care.
                appSettings.nCurrentActivityType = CurrentActivityType.NONE;
                // wndHabits.ResumeAfterPostpone();
                // wndHabits.Show();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PostponeHabitWork - " + ex.Message);
            }

        }

        public async void ShowFocusSummaryWindow()
        {
            try
            {
                appSettings.sActivityAllDaySummaryData = "";

                FocusBearAPIClient client = new FocusBearAPIClient();
                var x = await client.GetActivityAllDaySummary();


                if (wndFocusSummary == null)
                {
                    wndFocusSummary = new FocusActivitySummary();
                }
                ShowBlankWindowOnOtherScreens();
                wndFocusSummary.ShowDialog();
                wndFocusSummary.BringIntoView();
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
                            //Cts.Token.WaitHandle.WaitOne();
                            LogWriter.LogWrite("A new version of Focus Bear is available"); //TODO Win Text
                            //if (isCheckedManually)
                            {
                                //todo Win Text
                                string displayMsg = "A new version of Focus Bear is available. Do you wish to update to the latest version of Focus Bear?";
                                MessageBoxResult boxResult = MessageBox.Show(displayMsg, "New Update Available", MessageBoxButton.OKCancel);
                                //if user has cancelled the update process
                                if (boxResult == MessageBoxResult.Cancel)
                                {
                                    return;
                                }
                            }

                            UpdateWin winUpdate = new UpdateWin(ref updater);

                            //If Auto App update is selected then silently update app
                            //if (!isCheckedManually)
                           // {
                            //    winUpdate.Height = 0;
                            //    winUpdate.Width = 0;
                             //   updater.ForceUpdate = true;
                           // }

                            //if (updater.ForceUpdate)
                            {
                                winUpdate.Topmost = true;
                                winUpdate.Show();
                                winUpdate.Activate();
                                winUpdate.Focus();
                                
                            }
                            

                        }));
                    }
                    else
                    {
                        if (isCheckedManually)
                        {
                            await Dispatcher.BeginInvoke((Action)(() =>
                            {
                                string displayMsg = "You are already running the latest version of Focus Bear."; //TODO Win Text
                                MessageBoxResult boxResult = MessageBox.Show(displayMsg, "No New Update Available", MessageBoxButton.OK);
                                //if user has cancelled the update process
                                if (boxResult == MessageBoxResult.Cancel)
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

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if (trayIcon != null)
            {
                trayIcon.Dispose();
            }
        }

        public void ShowBlankWindowOnOtherScreens()
        {
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

    }
}
