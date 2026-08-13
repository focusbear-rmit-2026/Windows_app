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
using System.Drawing;
using Focus_Bear.SetIntention;
using Focus_Bear.Focus.BlurWindow;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Focus_Bear.FocusMusic;
using Focus_Bear.AccountabilityBuddy;
using System.Windows.Shapes;
using Path = System.IO.Path;
using Focus_Bear.Services;
using System.Security.Policy;
using System.Web;
using System.Windows.Documents.DocumentStructures;

namespace Focus_Bear.Utils
{
    // Teacher Mode (issue #1343): a class the logged-in teacher administers.
    public class TeacherClass
    {
        public string TeamId { get; set; } = "";
        public string Name { get; set; } = "";
        public override string ToString() => Name;   // so it displays nicely in a ComboBox
    }

    // Teacher Mode (issue #1343): maps common app display names / aliases to their Windows process
    // (exe base) name(s). On Windows the display name and the process name often diverge (Word ->
    // winword, PowerPoint -> powerpnt, Edge -> msedge, Teams -> ms-teams), so when a teacher types
    // "chrome" / "google chrome" / "word" as an allowed app we resolve it to the real process name(s)
    // and cross-platform enforcement (which matches on process name) recognises it.
    public static class CommonAppMap
    {
        private static readonly Dictionary<string, string[]> _map = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            // Browsers
            { "chrome", new[]{ "chrome" } },
            { "google chrome", new[]{ "chrome" } },
            { "edge", new[]{ "msedge" } },
            { "microsoft edge", new[]{ "msedge" } },
            { "firefox", new[]{ "firefox" } },
            { "mozilla firefox", new[]{ "firefox" } },
            { "brave", new[]{ "brave" } },
            { "opera", new[]{ "opera" } },
            { "arc", new[]{ "arc" } },
            // Microsoft Office
            { "word", new[]{ "winword" } },
            { "microsoft word", new[]{ "winword" } },
            { "ms word", new[]{ "winword" } },
            { "excel", new[]{ "excel" } },
            { "microsoft excel", new[]{ "excel" } },
            { "powerpoint", new[]{ "powerpnt" } },
            { "microsoft powerpoint", new[]{ "powerpnt" } },
            { "ppt", new[]{ "powerpnt" } },
            { "outlook", new[]{ "outlook" } },
            { "microsoft outlook", new[]{ "outlook" } },
            { "onenote", new[]{ "onenote" } },
            { "access", new[]{ "msaccess" } },
            { "publisher", new[]{ "mspub" } },
            { "teams", new[]{ "ms-teams", "teams" } },            // new Teams (ms-teams.exe) + classic (Teams.exe)
            { "microsoft teams", new[]{ "ms-teams", "teams" } },
            { "ms teams", new[]{ "ms-teams", "teams" } },
            // Communication
            { "slack", new[]{ "slack" } },
            { "zoom", new[]{ "zoom" } },
            { "discord", new[]{ "discord" } },
            { "skype", new[]{ "skype" } },
            { "webex", new[]{ "webex" } },
            { "cisco webex", new[]{ "webex" } },
            { "whatsapp", new[]{ "whatsapp" } },
            { "telegram", new[]{ "telegram" } },
            // Dev
            { "vscode", new[]{ "code" } },
            { "vs code", new[]{ "code" } },
            { "visual studio code", new[]{ "code" } },
            { "visual studio", new[]{ "devenv" } },
            { "notepad++", new[]{ "notepad++" } },
            { "notepad", new[]{ "notepad" } },
            { "windows terminal", new[]{ "windowsterminal", "wt" } },
            // Productivity / media
            { "notion", new[]{ "notion" } },
            { "obsidian", new[]{ "obsidian" } },
            { "evernote", new[]{ "evernote" } },
            { "spotify", new[]{ "spotify" } },
            { "figma", new[]{ "figma" } },
            { "photoshop", new[]{ "photoshop" } },
            { "acrobat", new[]{ "acrobat", "acrord32" } },
            { "adobe acrobat", new[]{ "acrobat", "acrord32" } },
            { "adobe reader", new[]{ "acrord32" } },
            { "acrobat reader", new[]{ "acrord32" } },
        };

        // Returns the mapped process name(s) for a typed/display app name, or nothing if unknown.
        public static IEnumerable<string> Resolve(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                yield break;
            string key = input.Trim();
            if (key.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                key = key.Substring(0, key.Length - 4);
            if (_map.TryGetValue(key, out string[] procs))
                foreach (string p in procs)
                    yield return p;
        }
    }

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
        public static readonly string AppUpdateINIPath = Path.Combine(AppDataDirPath, "update.ini");

        public static string FocusButtonUpdateIniFileURL { get { return "https://downloads.focusbear.io/update/focusbutton/focusbuttonupdate.ini"; } }
        public static readonly string FocusButtonUpdateINIPath = Path.Combine(AppDataDirPath, "focusbuttonupdate.ini");

        public static readonly string FocusBlockPagePath = Path.Combine(AppDataDirPath, "focus-block.html");

        public bool bRestartAppNeeded = false;
        public bool bRestartAppNeededAfterDeactivating = false;
        public bool bStartingAfterDeactivated = false;
        public string sMacAddress { get; set; }
        public string sGlobalUUID { get; set; }//Used initially for new users specifically to link later with distinct user ID
        public string sOSVersion { get; set; }
        public Version AppVersion { get; set; }
        public CurrentActivityType nCurrentActivityType { get; set; }
        public int nHabitIndex; //This is used to track the activity number from the activities array
        public DateTime dLastActivityPerformedStartTime; //This is the start time of the Last activity recently started.

        public DateTime dSettingsFetchedTime;
        public string sSubscriptionInfo { get; set; }
        public int nTrialDays { get; set; }
        public bool isPurchased { get; set; }
        public bool isExpired { get; set; }
        public bool IsLoggedIn { get; set; }
        public bool IsFirstInstall { get; set; }
        public DateTime FirstAppInstallDT { get; set; }

        public bool bHasUserEditedSettings = false;
        public DateTime InitialStartupTimeValue { get; set; }
        public DateTime InitialShutoffTimeValue { get; set; }
        public DateTime InitialSleepTimeValue { get; set; }

        public string sSavedOccupationForFirstFocusSession = "";
        public string sSavedIntentionFirstFocusSession = "";

        public string sSavedOccupationForPosthog = "";

        public FocusMode FocusModeNameToCreateWhileOnBoarding;
        public string OnboardingActivityName = "";
        public int OnboardingActivityDurationSeconds;
        public string OnboardingVideoUrl = "";
        public bool bOnboardingActivitySkipped = false;
        public bool bProgrammaticSelectioninLeftTree; //

        public bool bAlreadyHaveAnAccountClicked = false;
        public bool bGoalHealthyHabits { get; set; }
        public bool bGoalStayFocused { get; set; }
        public string sCareFactorString = "";

        public bool bAreBreaksEnabled = true; //default ON
        public bool bDontShowNaturalBreakDetection = false; //resets on every app launch

        public string sGeneratedOccupationRelevantSites = "";
        public ObservableCollection<DistractingURL> AllowedURLsWhileOnboarding = new ObservableCollection<DistractingURL>();
        public ObservableCollection<DistractingURL> DistractingURLsWhileOnboarding = new ObservableCollection<DistractingURL>();
        public string sHabitsParam = "";

        public bool bExitFullScreenClickedWhileOnboarding { get; set; }
        public DateTime MorningHabitsShownTillDate { get; set; }
        public DateTime EveningHabitsShownTillDate { get; set; }

        public DateTime FocusSummaryShownTillDate { get; set; }
        public DateTime ShutOFFAbortedTillDate { get; set; }
        public DateTime ShutOFFAbortedFor30MinsTillDate { get; set; }
        public DateTime AppDeactivatedTillDate { get; set; }

        public DateTime SelectedMorningHabitsDate { get; set; }
        public DateTime SelectedEveningHabitsDate { get; set; }
        public string CurrentRunningSequenceID { get; set; }

        public string sLangCode = "en";

        public string sTheme = "Light";
        public bool bAutoTheme = false;

        public bool bBlockWhileOnbording = false;  

        public bool bBlockingWorkedWhileOnboarding = false;
        //API infos Related
        public string sDeviceID = "";
        public string sRawUserSettings = "";
        public string sLastActivityDoneInfo = ""; //Last activity infor from current-activity-props api
        public string sRawHabitPacks = "";
        public string sHabitPackIdToInstall = "";
        public int nMorningRoutineDuration = 0;
        public int nEveningRoutineDuration = 0;
        public int BreaksTimeInterval = 20;
        public DateTime? NextBreakAt;

        public string sUserDetailsInfo; //All user details from API
        public string sUser_id;
        public string sUser_name;
        public string sUser_email;
        public bool bEmailVerified = false;

        public string sUserLocalDeviceSettings; // All local user device settings.
        public string sFocusModes; //array of all focus modes

        public string sOneMonthActivityDataForGraph;
        public string sCurrentDayActivityDataForGraph;
        public string sCurrentDayActivityDataForGraphQuestionsStats;


        public string sActivityAllDaySummaryData = "";

        public bool isAppEnable = true;
        public string sDayStartTime;
        public string sDayShutOffTime;
        public string sDayCutOffTime;
        public bool bSomeHabitsRemovedDueToCutOffTime = false;

        //Habits Realted
        public int currentHabitIndex = -1;
        public int currentActivityIndex = -1;
        public breakActivity currentActivityPlaying;
        public habit currentHabitPlaying;

        public List<habit> arrGLOBALAllMorningHabits = new List<habit>();
        public List<habit> arrGLOBALAllEveningHabits = new List<habit>();

        public List<habit> arrAllMorningHabitsFiltered = new List<habit>(); //these will now be filtered with the Days Of week
        public List<habit> arrAllEveningHabitsFiltered = new List<habit>();

        public List<habit> arrAllMorningHabits = new List<habit>(); //these will now be filtered with the Days Of week
        public List<habit> arrAllEveningHabits = new List<habit>();

        // Habit ids completed or skipped in the current routine — the Day Plan hides these so a
        // mid-routine return (via the paused-habit back button) shows only the remaining habits.
        // Cleared when a fresh routine's Day Plan is shown.
        public HashSet<string> dayPlanDoneOrSkippedHabitIds = new HashSet<string>();

        // Habit ids completed per day (key "yyyy-MM-dd") per the backend
        // (GET /completed-activity-sequence/routine-progress), fetched when the Day Plan opens/navigates.
        // Reliable across restarts / habit edits and after the routine is finished — used to strike
        // through done habits on today's "earlier" accordion and when viewing past days.
        public Dictionary<string, HashSet<string>> dayPlanCompletedHabitIdsByDay = new Dictionary<string, HashSet<string>>();

        // Backend-sourced SKIPPED habit ids per day (yyyy-MM-dd) from routine-progress. Kept separate
        // from completed so the Day Plan can mark skipped habits distinctly (struck through + a "skipped"
        // tag) instead of showing them as still-doable after a restart.
        public Dictionary<string, HashSet<string>> dayPlanSkippedHabitIdsByDay = new Dictionary<string, HashSet<string>>();

        // Whether the "set your intention" (HabitExtraPersonal) screen has been shown this routine —
        // it appears only for the first habit; later plays (incl. from the Day Plan) skip it.
        public bool bRoutineIntentionShown = false;

        // Remaining seconds for habits parked (paused → back) from the Day Plan, keyed by habit id, so
        // each habit resumes from its OWN progress. The single registry postpone slot only holds one
        // habit, which broke multi-habit pause/resume. Cleared at routine start.
        public Dictionary<string, int> dayPlanHabitRemainingSeconds = new Dictionary<string, int>();

        public ObservableCollection<HabitListSelectable> _ObjHabitListSelectable = new ObservableCollection<HabitListSelectable>();

        public bool bShowReducedHabitsOnExpired = false;

        public bool bHabitwithAllowedAppsRunning = false;
        public bool bHabitwithUseFocusModeRunning = false;
        public bool bHabitwithUserActionNonTimerRunning = false;
        public string sUseFocusModeIDInActivity = "";
        public string sCurrentRunningActivity = "";
        public string sHabitTypeForUseFocusModeIDInActivity = "";//evening-habit, morning-habit, break-activity

        public bool bShowActivitiesFromMid = false;

        public bool bHabitsPostponedState = false; //variable to handle the Resume Habits menu item
        public bool bFocusSummaryPostponedState = false; //variable to handle the Resume Habits menu item in case of focus summary postponed
        public bool bEveningHabitsPostponedAfterCutoffTime = false;

        // Postpone persistence across app restarts (see RegistryManager.*HabitsPostponeEndTime).
        public DateTime HabitsPostponeEndTime;                                             // absolute time the postpone expires
        public CurrentActivityType HabitsPostponeActivityType = CurrentActivityType.NONE;  // routine the postpone belongs to
        public bool bHabitsPostponeWasFocusSummary = false;                                // true if it was an evening focus-summary postpone

        public string sRawTasks = "";
        public string sRawTasksDone = ""; //used to get a list of tasks done from focus mode end window.
        public string sCurrentTaskPlayingInToDoPlayer = "";
        public string sAllTasks = "";
        public string sCurrentTaskCreated = "";
        public string sExtraJustificationForThisSite = "";
        public string sConvincingAIForURL = "";

        public JArray arraySkipped = new JArray();
        public JArray arrayCompletedActivities = new JArray();
        public int nTotalSecondsAllHabits = 0;
        public int nHabitCompletedSeconds = 0;

        public bool bShowActivitiesFromMidLastSkipped = false;
        public bool bSkipCurrentActivity = false;
        public bool bSkipCurrentActivityCantDoItToday = false;
        public List<SKIPPED_ACTIVITY> indexSkippedActivities = new List<SKIPPED_ACTIVITY>();
        public bool bSkipWhileActivityOngoing = false;

        public List<BlankWindow> SecondaryWindows = new List<BlankWindow>();

        public bool bPlayActivitiesInFullScreenMode = false;

        //Breaks Related
        public string[] activityAllowedFocusApps;
        public string[] activityAllowedFocusUrls;
        public bool isAnyMandatoryActivityIncluded;
        public bool isMandatoryActivityGoing;
        public int breakAfterMinutes;
        public int nCountBreaksPostponedTimes;

        public List<breakActivity> arrAllbreakActivities = new List<breakActivity>();
        public List<breakActivity> arrActivitiesIncludeInEveryBreak = new List<breakActivity>();
        public List<breakActivity> arrActivitiesExcludeInEveryBreak = new List<breakActivity>();

        public List<breakActivity> arrBreakActivitiesTODOInOrder = new List<breakActivity>();

        public activityChoice currentActivityChoice;

        public activityChoice currentActivityChoiceTemp;

        public List<LastShownActivityChoice> arrLastShownRandomChoice = new List<LastShownActivityChoice>();

        public List<AppDetails> InstalledApps = new List<AppDetails>();

        public string sFocusModeTemplates; //All FOcus Mode templates  from API

        public ObservableCollection<FocusMode> arrFocusModes = new ObservableCollection<FocusMode>();

        public IAccessible FirefoxLastAccWindow = null;
        public object objChild = null;
        public string sLastActiveURLInFirefox = "";

        //Teacher Mode - the URL the student requested when they clicked "Oops I need this" on a class
        //session block page (captured at that moment - reliable). Reloaded if the teacher approves it,
        //but only while the foreground tab is still that block page (matched by its old_url).
        public string sLastFocusBlockedUrl = "";

        public bool bShowOnBoardingVideo = false;

        public string sCustomGoalSelected = "";
        public string sAdditionalGoals = "";

        public string sLastActiveAppName = "";
        public string sLastActiveURL = "";

        public string sBrainDump = "";

        public bool isFocusModeRunning = false;

        public bool isFocusModePaused = false;

        public int nPausedFocusRemainingSeconds = 0;

        public string sRunningFocusModeID = "";
        public string sRunningFocusModeName = "";
        public string sRunningFocusModeStartTime = "";
        public string sRunningFocusModeEndTime = "";
        public string sRunningFocusModeIntention = "";
        public double sRunningFocusModeAlignmentScoreThreshold;
        private string _sMostRecentFocusmodeDoneID = "";
        // Persisted to the registry on every set so the last-used focus mode survives restarts.
        // Every focus-mode start path writes this (geek/simple/meetings/StartWorkFocusMode/pusher),
        // so persisting in the setter covers all of them without touching each call site.
        public string sMostRecentFocusmodeDoneID
        {
            get { return _sMostRecentFocusmodeDoneID; }
            set
            {
                _sMostRecentFocusmodeDoneID = value;
                RegistryManager.SetMostRecentFocusModeId(value);
            }
        }

        public readonly Queue<string> _last5Justifications = new Queue<string>();
        public const int MaxJustifications = 5;

        public DateTime? focusSessionStartTime;   // when current active segment started for the current focus session to calculate exact time used, ignoring pause time
        public double CurrentFocusSessionDuration = 0; //will be used to save in API and show in end window

        public double CurrentFocusAlignmentScore = 100; //will be used to save in API
        public double TotalDeepFocusedTime = 0;

        public bool bSkipMicroBreaksDuringFocus;
        public bool bShowFocusModeEnd; 
        public bool bDontShowModeChangeFB = false;
        public string sChatGPTReasonForCurrentURL = "";
        public double dProbablity = 0;
        public string sChatGPTPayLoad = "";
        public string sChatGPTResponse = "";

        public string sRunningFocusModeHours = "";
        public string sRunningFocusModeMinutes = "";
        public bool bStartingAnotherFocusMode = false;

        public string sRawFocusModeTemplates = "";

        //Super Pomodoro Related
        public bool bForceClosingFocusMode = false;
        public bool isStartPomodoroWindowOpen = false;

        public bool bUseSuperPomodoroMode = false;
        public bool bEnterSuperPomodoroModeAfterMorningRoutine = false;

        /// <summary>
        /// Maximum number of browser tabs before an alert is shown. 0 = disabled.
        /// </summary>
        public int nMaxTabLimit = 0;
        // Tab limiter on/off (macOS: kisTabLimiterEnabled, default false). When enabled, nMaxTabLimit is
        // always >= 1 (0 means "off", which is expressed by this flag being false instead).
        public bool bTabLimiterEnabled = false;
        // Set during the one-time migration of an existing user so the migrated on/off + count get pushed to
        // the server (making the migrated state the synced truth instead of being overwritten by it).
        public bool bTabLimiterMigrationNeedsPush = false;
        public int nCountOpenTabs = 0;

        /// <summary>
        /// When true, all notification sounds (habits, focus mode, routines) are silenced.
        /// Does NOT affect focus music.
        /// </summary>
        public bool bDisableNotificationSounds = false;

        public string sMinutesSuperPomodoro = "";
        public string sMinutesBreakPomodoro = "";
        public string SuperPomodoroFocusedName = "";
        public int NumberOfSuperPomodorosToRun = 2;
        public int nSuperPomodoroCountCompleted = 0;

        public string PomodoroMinutes = "25";
        public string PomodoroBreakMinutes = "5";
        public int PomodoroWorkToBreakRatio = 5;
        public int PomodoroBreakRatio = 1;

        //Pomodoro Advanced
        public string AutomaticSuperPomodoroFocusedName = "";
        public int AutomaticNumberOfSuperPomodorosToRun = 2;
        public bool bAutomaticSuperPomodoroSkipMicroBreaks = false;
        public JArray AutomaticSuperPomodoroSelectedDaysArray = new JArray();

        public bool bEnterPomodoroModeAfterMorningRoutine = true;
        public bool bAutomaticUseSuperPomodoroModeSetting = false;

        //Gentle Block related
        public ObservableCollection<DistractingURL> TemporaryAllowedAppsGentleBlock = new ObservableCollection<DistractingURL>();
        public List<string> TemporarryBlockedAppsGentleBlock = new List<string>();

        public ObservableCollection<DistractingURL> TemporaryAllowedURLsGentleBlock = new ObservableCollection<DistractingURL>();
        public List<string> TemporarryBlockedURLsGentleBlock = new List<string>();

        public ObservableCollection<DistractingURL> TemporaryAllowedKeywords = new ObservableCollection<DistractingURL>();
        public List<string> TemporaryAllowedURLsTabTitles = new List<string>();
        public string sRawKeywords = "";

        GentleBlock wndGentleBlock = null;
        ReportBadAIDecision wndReportBadAIDecision = null;
        public BlurWindow wndBlurWindow = null;

        public string sUploadURL = "";
        public List<DroppedFile> droppedFiles = new List<DroppedFile>();

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        // Browsers keep a strip at the top of the blur so their address bar stays reachable; every other
        // (app) window has no address bar, so the blur should cover it fully (#blur-app-sizing).
        private static readonly System.Collections.Generic.HashSet<string> BlurBrowserProcessNames =
            new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "chrome", "firefox", "msedge", "brave", "vivaldi", "opera", "comet" };

        public int nFocusBearMode = 1; //1 is cuddly bear mode
        ////
        public bool isOfficeModeEnabled = false;

        public ObservableCollection<string> arrAlwaysBlockedURLs = new ObservableCollection<string>();
        public ObservableCollection<AppDetails> arrAlwaysBlockedApps = new ObservableCollection<AppDetails>();

        public ObservableCollection<AppDetails> arrAlwaysAllowedApps = new ObservableCollection<AppDetails>();

        public ObservableCollection<DistractingURL> arrDistractingURLs = new ObservableCollection<DistractingURL>();

        //Set intention for super distrating urls related
        public bool bImmediatelyBlockDistractingSites { get; set; }
        SetIntention.SetIntention wndSetIntention = null;
        public ObservableCollection<DistractingURLAllowedWithIntention> TemporaryAllowedURLsWithSetIntention = new ObservableCollection<DistractingURLAllowedWithIntention>();
        public List<string> TemporarryBlockedURLsWithSetIntention = new List<string>();

        ShowIntention wndShowIntention = null;
        public int nTotalTimeForDistractingSitesToday = 0; 
        public DateTime Date_TotalTimeForDistractingSitesToday = DateTime.Today;

        //Password settings related
        public string sUserPassword { get; set; }
        public bool isPasswordSet { get; set; }
        public bool bPasswordRequireInChangeSettings { get; set; }
        public bool bPasswordRequireInSkipMorning { get; set; }
        public bool bPasswordRequireInSkipEvening { get; set; }
        public bool bPasswordRequireInAbortShutoff { get; set; }
        public bool bPasswordRequireInStopPomodoro { get; set; }
        public bool bPasswordRequireInQuitApp { get; set; }
        public bool bPasswordRequireToUseRelaxFocusMode { get; set; }
        public bool bPasswordRequireToSkipEveningAfterCutOff { get; set; }
        // Separate password rules for POSTPONE (vs Skip). Local device settings; default OFF (set per
        // user via the Password settings checkboxes), so Postpone can be protected while Skip is open.
        public bool bPasswordRequireInPostponeMorning { get; set; }
        public bool bPasswordRequireInPostponeEvening { get; set; }
        public bool bPasswordRequireInPostponeEveningAfterCutOff { get; set; }

        //AI Tab
        public bool bMotivationalMessageEnabled { get; set; }
        public bool bAiCheckUrlEnabled { get; set; }
       
        public bool bAiCheckUrlEnabledLastValueBeforeSimplePomodoro { get; set; }
        /// 

        public string sConfigFilePath;
        public string sLogQuantityCSVPath;

        public string sOfflineDataCSVPath;

        public CsvLogger csvFocusProductivityStats = null;
        public CsvLogger csvUsageTimingsExplanation = null;
        public CsvLogger csvfocusAIDecisions = null;

        public bool bStringsLoaded;

        public int nTimerValForNonTimerActivities = 86400; //Timer val for non - timer activities

        public bool bHasEditedBlockedURLs = false;

        public bool bIsTestUser = false;

        //Custom Blocking Schedule Related:
        public string sRawBlockingSchedules = "";
        public ObservableCollection<CustomBlockingSchedule> arrCustomBlockingSchedules = new ObservableCollection<CustomBlockingSchedule>();
        public bool isBlockingScheduleRunning = false;
        public CustomBlockingSchedule ActiveSchedule = null;

        //Meetings detection related
        public bool bIsUserOnCall = false;
        public string sLastAppUsingMicrophone = "";
        public string sLastAppUsingWebCam = "";

        public bool bEnableMeetingsDetection = true;
        public bool bAutomaticallySuggestStartingMeetingsFocus = false;
        public bool bBlockSuperDistractingSitesDuringMeetings = false;
        public bool bShowBreaksWhileInCall = false;
        public bool bUserClickedNoDoNotSuggestAnymore = false;
        public bool bAdvancedAnalyticsInFocusModeEnabled = false;

        public bool bIsGeekModeEnabled = false;


        public bool AutoCloseAfterRoutineSkipPostpone { get; set; } = false;
        //Simple mode Focus summary related
        public int nTotalDistractingSitesBlocked = 0;
        public int nTimeSpentBlockingDistractingSites = 0;
        public int nTimeSpentBlockingActivated = 0;
        public int nTimeSpentBlockingDeactivated = 0;

        public bool bCompleteYourEveningRoutineClicked = false;
        //FBTT
        public bool bFocusBearTimeTrackerEnabled = false;

        //Fonts related
        public string[] arrFontFamily = { "Segoe UI", "Arial", "Comic Sans MS", "Verdana", "Tahoma", "Trebuchet MS", "Century Gothic", "Poppins", "IntelOne Mono", "OpenDyslexic3 Regular" };
        public string sSelectedAppFont = "Segoe UI";

        //Relax activities related
        public string sSavedURLSForRelaxActivity; //To get the urls from API

        //menu opening first time variable
        public bool bMenuOpeningFirstTime = false;

        public string sRawFocusMusicTracks = "";
        public ObservableCollection<FocusMusicTrack> arrFocusMusicTracks = new ObservableCollection<FocusMusicTrack>();

        //Set when focus music is auto-paused for a routine/break, so we only auto-resume music that WE paused (not music the user paused manually).
        public bool bFocusMusicPausedAutomatically = false;

        public string sRawBuddiesList = "";
        public ObservableCollection<BuddyDetails> arrBuddiesList = new ObservableCollection<BuddyDetails>();
        public string sUnlockRequestsInfo = "";
        public DateTime UnlockTillTime = DateTime.Now;

        //After a correct password is entered, subsequent password-protected actions are auto-approved until this time
        public DateTime PasswordVerifiedTillTime = DateTime.MinValue;
        public BLOCKINGSCHEDULE nBlockingSchedule { get; set; }

        public CUIAutomation automation;
        public IUIAutomationFocusChangedEventHandler handler;
        public string RecordingPath { get; set; }
        public bool bActivityStartedFromDevice = false;
        public string sFirmwareVersion = "";
        public Version FirmwareVersion { get; set; }

        public JObject ConfigFileStrings;
        // Global keyboard shortcuts (issue #383/#1150): action id -> shortcut. Loaded from the registry on
        // startup and registered via GlobalHotkeyManager; edited in the Keyboard Shortcuts settings tab.
        public Dictionary<string, Focus_Bear.Models.KeyboardShortcut> KeyboardShortcuts = new Dictionary<string, Focus_Bear.Models.KeyboardShortcut>();
        public UserAccount userAccount { get; set; } = new UserAccount() { userID = "", username = "", email = "", accessToken = "", refreshToken = "" };

        // Teacher Mode (issue #1343): when the logged-in user is a teacher (administers >= 1 class),
        // pressing Focus starts a class focus session for their class instead of a personal one.
        // Populated once on startup from GET /class-management/classes.
        public bool bIsTeacher = false;
        // All classes the teacher administers, so the Focus screen can offer a picker.
        public List<TeacherClass> arrTeacherClasses = new List<TeacherClass>();
        // team_id of the class the next session will start for (the picked / remembered class).
        public string sTeacherClassTeamId = "";
        // Friendly name of that class (for logging / any UI hint).
        public string sTeacherClassName = "";
        // Per-attempt override: a teacher ticked "start a personal session instead" on the Focus screen.
        public bool bTeacherStartPersonalSession = false;
        // Id of the class focus session currently started from this device, so it can be ended early.
        public string sRunningClassSessionId = "";
        // Reason the last class-session start failed (shown to the teacher instead of starting focus).
        public string sLastClassSessionError = "";

        // Student class-session TodoPlayer mode (in-memory only; not persisted).
        // Non-empty → loadMetaDataForToDoPlayer includes class_session_id and tasks:[]; FE owns checklist.
        public string sClassSessionTodoSessionId = "";
        // Personal sRawTasks captured before class mode forces []; restored on session end.
        public string sPersonalRawTasksBackup = "";

        static AppSettings() { }
        private AppSettings()
        {
            GetCurrentAppVersion();

            sMacAddress = Global.GetMacAddress();

            sOSVersion = Environment.OSVersion.Version.ToString();

            nHabitIndex = -1;
            LogWriter.LogWrite("nHabitIndex = " + nHabitIndex);
            nCurrentActivityType = CurrentActivityType.NONE;

            sConfigFilePath = Path.Combine(AppDataDirPath, "config.json");
            sLogQuantityCSVPath = Path.Combine(AppDataDirPath, "FocusBearStats.CSV");

            sOfflineDataCSVPath = Path.Combine(AppDataDirPath, "FocusBearOfflineData.CSV");

            csvFocusProductivityStats = new CsvLogger(Path.Combine(AppDataDirPath, "focusProductivityStats.CSV"), true);
            csvUsageTimingsExplanation = new CsvLogger(Path.Combine(AppDataDirPath, "usage_timings.CSV"), false, true);
            csvfocusAIDecisions = new CsvLogger(Path.Combine(AppDataDirPath, "focusAIDecisions.CSV"), false, false, true);

            isAnyMandatoryActivityIncluded = false;
            isMandatoryActivityGoing = false;
            breakAfterMinutes = 0;
            nCountBreaksPostponedTimes = 0;

            isOfficeModeEnabled = false;

            bGoalHealthyHabits = false;
            bGoalStayFocused = false;
            bExitFullScreenClickedWhileOnboarding = false;

            isPasswordSet = false;

            bSkipMicroBreaksDuringFocus = true;

            //Default true on 23 June 2023 #188
            bMotivationalMessageEnabled = true;
            bAiCheckUrlEnabled = true;

            bPasswordRequireInChangeSettings = false;
            bPasswordRequireInSkipMorning = false;
            bPasswordRequireInSkipEvening = false;
            bPasswordRequireInAbortShutoff = false;
            bPasswordRequireInStopPomodoro = false;
            bPasswordRequireInQuitApp = false;
            bPasswordRequireToUseRelaxFocusMode = false;
            bPasswordRequireToSkipEveningAfterCutOff = false;
            bPasswordRequireInPostponeMorning = false;
            bPasswordRequireInPostponeEvening = false;
            bPasswordRequireInPostponeEveningAfterCutOff = false;
            bImmediatelyBlockDistractingSites = false;

            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "is_fb_test_user.txt") ;
            bIsTestUser = File.Exists(path);
        }

        public void CheckAndGetFBTTStatus()
        {
            bFocusBearTimeTrackerEnabled = RegistryManager.GetFocusBearTimeTrackerStatus();
        }

        public void LoadMaxTabLimitSetting()
        {
            nMaxTabLimit = RegistryManager.GetMaxTabLimit();

            bool? bEnabled = RegistryManager.GetTabLimiterEnabled();
            if (bEnabled == null)
            {
                // First run of the enable/disable model — migrate existing users to match macOS:
                // an existing count >= 1 means the limiter was in use → keep it on with that count;
                // a count of 0 (or unset) means it was off → disable and normalise the count to 1
                // (the count can no longer be 0; "off" is the disabled flag).
                bTabLimiterEnabled = nMaxTabLimit >= 1;
                if (nMaxTabLimit < 1) nMaxTabLimit = 1;
                RegistryManager.SaveMaxTabLimit(nMaxTabLimit);
                RegistryManager.SaveTabLimiterEnabled(bTabLimiterEnabled);
            }
            else
            {
                bTabLimiterEnabled = bEnabled.Value;
                if (bTabLimiterEnabled && nMaxTabLimit < 1) nMaxTabLimit = 1;
            }
        }

        public void LoadNotificationSoundsSetting()
        {
            bDisableNotificationSounds = RegistryManager.GetDisableNotificationSounds();
        }

        public void LoadMostRecentFocusMode()
        {
            // Set the backing field directly so loading does not write back to the registry.
            _sMostRecentFocusmodeDoneID = RegistryManager.GetMostRecentFocusModeId();
        }

        public void CheckAndGetGlobalUUID()
        {
            try
            {
                if (RegistryManager.GetGlobalUUID())
                {
                    LogWriter.LogWrite("GetGlobalUUID Success");
                    //We have a device GlobalUUID already, so proceed with next.
                }
                else
                {
                    sGlobalUUID = System.Guid.NewGuid().ToString();
                    RegistryManager.SetGlobalUUID();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("CheckAndGetGlobalUUID() ex - " + ex.Message);
            }
        }
        public async void GetLoginAndTokenValidSettings()
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
                        LogWriter.LogWrite("Token valid, Get all trial Info");

                        //Token valid, GEt all trial Info
                        int nLogged = await GetAllTrialInfo();
                        if (nLogged == 404)
                        {
                            //Server no longer recognises this account (involuntary session loss)
                            CaptureLogout(Constants.Events.LogoutReason.AuthError);
                            ((App)(Application.Current)).ShowLoginWindow();
                            return;
                        }
                        
                        CheckAndGetFBTTStatus();

                        ((App)(Application.Current)).ProceedAfterGetLoginAndTokenValidSettings();
                    }
                    else
                    {
                        LogWriter.LogWrite("Token INVALID, call RenewToken()");

                        //Disable the focus bear time tracker status
                        bFocusBearTimeTrackerEnabled = false;
                        RegistryManager.SetFocusBearTimeTrackerStatus();

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
                ((App)(Application.Current)).ShowLoginWindow(); //In any case if we are not able to get the logged in info, we shold ask to login back
            }

        }

        public async Task<int> GetAllTrialInfo()
        {
            LogWriter.LogWrite("GetAllTrialInfo()");
            try
            {
                FocusBearAPIClient client = new FocusBearAPIClient();
                var xResponse = await client.GetSubscriptionInfo();

                if (xResponse == 404)
                {
                    return 404;
                }

                if (sSubscriptionInfo?.Length > 0)
                {
                    LogWriter.LogWrite("subscriptionStatus - " + sSubscriptionInfo);

                    JObject subscriptionStatusParsed = JObject.Parse(sSubscriptionInfo);
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
                    return 1;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetAllTrialInfo() ex - " + ex.Message);
            }
            return 0;
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
                //Refresh failed while the user was signed in = involuntary session loss.
                CaptureLogout(Constants.Events.LogoutReason.RefreshFailed);
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
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ValidateTokenValidBeforeAPICall()" + ex.Message);
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

                        string sConfigPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
                        sConfigPath = sConfigPath.TrimEnd('\\');
                        if (sLangCode == "en")
                        {
                            sConfigPath = Path.Combine(sConfigPath, "config.json");
                        }
                        else if (sLangCode == "es")
                        {
                            sConfigPath = Path.Combine(sConfigPath, "config-es.json");
                        }

                        File.Copy(sConfigPath, sConfigFilePath);

                        ConfigFileStrings = JObject.Parse(File.ReadAllText(sConfigFilePath));
                    }

                    ((App)(Application.Current)).ProceedAfterDownloadingConfigFile();
                }
                else
                {
                    LogWriter.LogWrite("DownloadAndLoadConfigFile Failed - Proceed to read Local File");
                    //Use Local File
                    Global.DeleteFile(sConfigFilePath, false);

                    string sConfigPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
                    sConfigPath = sConfigPath.TrimEnd('\\');
                    if (sLangCode == "en")
                    {
                        sConfigPath = Path.Combine(sConfigPath, "config.json");
                    }
                    else if (sLangCode == "es")
                    {
                        sConfigPath = Path.Combine(sConfigPath, "config-es.json");
                    }

                    File.Copy(sConfigPath, sConfigFilePath);
                    ConfigFileStrings = JObject.Parse(File.ReadAllText(sConfigFilePath));
                    ((App)(Application.Current)).ProceedAfterDownloadingConfigFile();
                }

            }
            catch (Exception ex)
            {
                // Log the full exception (stack + line), not just the message - this catch swallowed a
                // culture-dependent DateTime.Parse that aborted startup on non-US regions (#1374).
                LogWriter.LogWrite("DownloadAndLoadConfigFile() - " + ex.ToString());
            }
        }
        public void SaveLoggedInUserInfo()
        {
            LogWriter.LogWrite("SaveLoggedInUserInfo()");

            RegistryManager.SaveLoggedInUserInfo();
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
                FocusBearAPIClient client = new FocusBearAPIClient();
                var isInternetOn = await client.IsInternetOn();

                if (isInternetOn == false)
                {
                    return false;
                }

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

                    if (File.Exists(AppSettings.FocusBlockPagePath))
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

            if (arrGLOBALAllMorningHabits != null)
                arrGLOBALAllMorningHabits.Clear();
            if (arrGLOBALAllEveningHabits != null)
                arrGLOBALAllEveningHabits.Clear();

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
                    sDayCutOffTime = UserSettings.ContainsKey("cutoff_time_for_non_high_priority_activities") ? UserSettings["cutoff_time_for_non_high_priority_activities"].ToString() : "";
                    breakAfterMinutes = UserSettings.ContainsKey("break_after_minutes") ? (int)UserSettings["break_after_minutes"] : 15;
                    bHasUserEditedSettings = UserSettings.ContainsKey("has_edited_settings") ? (bool)UserSettings["has_edited_settings"] : false;
                    LogWriter.LogWrite("sDayStartTime - " + sDayStartTime);
                    LogWriter.LogWrite("sDayShutOffTime - " + sDayShutOffTime);

                    JArray arrayMorningActivities = (JArray)UserSettings["morning_activities"];
                    JArray arrayEveningActivities = (JArray)UserSettings["evening_activities"];
                    JArray arrayBreakActivities = (JArray)UserSettings["break_activities"];

                    if (arrayMorningActivities.Count() > 0)
                    {
                        int nIdFakeForDevice = 1;

                        foreach (JObject item in arrayMorningActivities)
                        {
                            string id = item.ContainsKey("id") ? item.GetValue("id").ToString() : "";

                            string name = item.ContainsKey("name") ? item.GetValue("name").ToString() : "";
                            string nameWithoutIcon = name; //used for device

                            string habit_icon = item.ContainsKey("habit_icon") ? item.GetValue("habit_icon").ToString() : "";
                            
                            if(habit_icon.Length > 0)
                            {
                                name = habit_icon + " " + name;
                            }

                            int durationSeconds = item.ContainsKey("duration_seconds") ? (int)item.GetValue("duration_seconds") : 0;

                            List<string> arrVideoUrlsArray = new List<string>(); ;
                            if (item.ContainsKey("video_urls"))
                            {
                                JArray arrayurlsArray = (JArray)item.GetValue("video_urls");
                                arrVideoUrlsArray = arrayurlsArray.ToObject<List<string>>();
                            }

                            string logQuantityQuestion = item.ContainsKey("log_quantity_question") ? item.GetValue("log_quantity_question").ToString() : "";

                            List<QuantityQuestion> logQuantityQuestions = new List<QuantityQuestion>(); ;
                            if (item.ContainsKey("log_quantity_questions"))
                            {
                                JArray quesArray = (JArray)item.GetValue("log_quantity_questions");
                                //Read one by one
                                if (quesArray.Count() > 0)
                                {
                                    foreach (JObject ques in quesArray)
                                    {
                                        string qid = ques.ContainsKey("id") ? ques.GetValue("id").ToString() : "";
                                        int minValue = ques.ContainsKey("min_value") ? (int)ques.GetValue("min_value") : 0;
                                        int maxValue = ques.ContainsKey("max_value") ? (int)ques.GetValue("max_value") : 0;
                                        string question = ques.ContainsKey("question") ? ques.GetValue("question").ToString() : "";
                                        string logQuantityType = ques.ContainsKey("log_summary_type") ? ques.GetValue("log_summary_type").ToString() : "";
                                        string min_value_description = ques.ContainsKey("min_value_description") ? ques.GetValue("min_value_description").ToString() : "";
                                        string max_value_description = ques.ContainsKey("max_value_description") ? ques.GetValue("max_value_description").ToString() : "";

                                        logQuantityQuestions.Add(new QuantityQuestion(qid, minValue, maxValue, question, logQuantityType, 0, min_value_description, max_value_description));
                                    }
                                }
                            }

                            bool logQuantity = item.ContainsKey("log_quantity") ? (bool)item.GetValue("log_quantity") : false;
                            if (logQuantityQuestions.Count() == 0)
                            {
                                logQuantity = false;
                            }

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
                            string completion_requirements = item.ContainsKey("completion_requirements") ? item.GetValue("completion_requirements").ToString() : "";
                            bool useTimer = true;
                            if (completion_requirements.Length > 0)
                            {
                                useTimer = false;
                            }
                            bool show_saved_distracting_websites = item.ContainsKey("show_saved_distracting_websites") ? (bool)item.GetValue("show_saved_distracting_websites") : false;
                            bool bHighPriorityActivity = false;

                            List<string> arrDaysOfWeek = new List<string>(); ;
                            if (item.ContainsKey("days_of_week"))
                            {
                                JArray arrTempDays = (JArray)item.GetValue("days_of_week");
                                arrDaysOfWeek = arrTempDays.ToObject<List<string>>();
                            }

                            arrAllMorningHabits.Add(new habit(id, name, nIdFakeForDevice.ToString(), nameWithoutIcon, durationSeconds, arrVideoUrlsArray, logQuantity, logQuantityQuestion, openSpecificApps, allowedUrls, allowedApps, allowedFocusMode, activity_sequence_id, logQuantityQuestions, completion_requirements, useTimer, show_saved_distracting_websites, bHighPriorityActivity, arrDaysOfWeek, ""));
                            nIdFakeForDevice++;
                        }
                        //copy to Global Array but do not reference
                        arrGLOBALAllMorningHabits = new List<habit>(arrAllMorningHabits);
                        arrAllMorningHabitsFiltered = new List<habit>(arrAllMorningHabits);
                    }
                    if (arrayEveningActivities.Count() > 0)
                    {
                        int nIdFakeForDevice = 1;
                        foreach (JObject item in arrayEveningActivities)
                        {
                            string id = item.ContainsKey("id") ? item.GetValue("id").ToString() : "";

                            string name = item.ContainsKey("name") ? item.GetValue("name").ToString() : "";
                            string nameWithoutIcon = name;
                            string habit_icon = item.ContainsKey("habit_icon") ? item.GetValue("habit_icon").ToString() : "";

                            if (habit_icon.Length > 0)
                            {
                                name = habit_icon + " " + name;
                            }

                            int durationSeconds = item.ContainsKey("duration_seconds") ? (int)item.GetValue("duration_seconds") : 0;

                            List<string> arrVideoUrlsArray = new List<string>(); ;
                            if (item.ContainsKey("video_urls"))
                            {
                                JArray arrayurlsArray = (JArray)item.GetValue("video_urls");
                                arrVideoUrlsArray = arrayurlsArray.ToObject<List<string>>();
                            }


                            string logQuantityQuestion = item.ContainsKey("log_quantity_question") ? item.GetValue("log_quantity_question").ToString() : "";


                            List<QuantityQuestion> logQuantityQuestions = new List<QuantityQuestion>(); ;
                            if (item.ContainsKey("log_quantity_questions"))
                            {
                                JArray quesArray = (JArray)item.GetValue("log_quantity_questions");
                                //Read one by one
                                ////
                                if (quesArray.Count() > 0)
                                {
                                    foreach (JObject ques in quesArray)
                                    {
                                        string qid = ques.ContainsKey("id") ? ques.GetValue("id").ToString() : "";
                                        int minValue = ques.ContainsKey("min_value") ? (int)ques.GetValue("min_value") : 0;
                                        int maxValue = ques.ContainsKey("max_value") ? (int)ques.GetValue("max_value") : 0;
                                        string question = ques.ContainsKey("question") ? ques.GetValue("question").ToString() : "";
                                        string logQuantityType = ques.ContainsKey("log_summary_type") ? ques.GetValue("log_summary_type").ToString() : "";
                                        string min_value_description = ques.ContainsKey("min_value_description") ? ques.GetValue("min_value_description").ToString() : "";
                                        string max_value_description = ques.ContainsKey("max_value_description") ? ques.GetValue("max_value_description").ToString() : "";

                                        logQuantityQuestions.Add(new QuantityQuestion(qid, minValue, maxValue, question, logQuantityType, 0, min_value_description, max_value_description));
                                    }
                                }
                            }

                            bool logQuantity = item.ContainsKey("log_quantity") ? (bool)item.GetValue("log_quantity") : false;
                            if (logQuantityQuestions.Count() == 0)
                            {
                                logQuantity = false;
                            }

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

                            string completion_requirements = item.ContainsKey("completion_requirements") ? item.GetValue("completion_requirements").ToString() : "";
                            bool useTimer = true;
                            if (completion_requirements.Length > 0)
                            {
                                useTimer = false;
                            }
                            bool show_saved_distracting_websites = item.ContainsKey("show_saved_distracting_websites") ? (bool)item.GetValue("show_saved_distracting_websites") : false;
                            bool bHighPriorityActivity = item.ContainsKey("priority") ? (("HIGH" == item.GetValue("priority").ToString()) ? true : false) : false;

                            List<string> arrDaysOfWeek = new List<string>(); ;
                            if (item.ContainsKey("days_of_week"))
                            {
                                JArray arrTempDays = (JArray)item.GetValue("days_of_week");
                                arrDaysOfWeek = arrTempDays.ToObject<List<string>>();
                            }
                            string sCutOffTime = "";
                            if (item.ContainsKey("cutoff_time_for_doing_activity"))
                            {
                                string sTime = item.GetValue("cutoff_time_for_doing_activity").ToString();
                                if(sTime != null && sTime != "")
                                {
                                    sCutOffTime = sTime;//We have a cutoff time for this habit
                                }
                            }

                            arrAllEveningHabits.Add(new habit(id, name, nIdFakeForDevice.ToString(), nameWithoutIcon, durationSeconds, arrVideoUrlsArray, logQuantity, logQuantityQuestion, openSpecificApps, allowedUrls, allowedApps, allowedFocusMode, activity_sequence_id, logQuantityQuestions, completion_requirements, useTimer, show_saved_distracting_websites, bHighPriorityActivity, arrDaysOfWeek, sCutOffTime));
                            nIdFakeForDevice++;
                        }

                        //copy to Global Array but do not reference
                        arrGLOBALAllEveningHabits = new List<habit>(arrAllEveningHabits);
                        arrAllEveningHabitsFiltered = new List<habit>(arrAllEveningHabits);
                    }
                    if (arrayBreakActivities.Count() > 0)
                    {
                        foreach (JObject item in arrayBreakActivities)
                        {
                            string id = item.ContainsKey("id") ? item.GetValue("id").ToString() : "";

                            string name = item.ContainsKey("name") ? item.GetValue("name").ToString() : "";

                            string habit_icon = item.ContainsKey("habit_icon") ? item.GetValue("habit_icon").ToString() : "";

                            if (habit_icon.Length > 0)
                            {
                                name = habit_icon + " " + name;
                            }

                            int durationSeconds = item.ContainsKey("duration_seconds") ? (int)item.GetValue("duration_seconds") : 0;

                            bool IncludeInEveryBreak = item.ContainsKey("include_in_every_break") ? (bool)item.GetValue("include_in_every_break") : false;

                            List<string> arrVideoUrlsArray = new List<string>(); ;
                            if (item.ContainsKey("video_urls"))
                            {
                                JArray arrayurlsArray = (JArray)item.GetValue("video_urls");
                                arrVideoUrlsArray = arrayurlsArray.ToObject<List<string>>();
                            }


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

                            string completion_requirements = item.ContainsKey("completion_requirements") ? item.GetValue("completion_requirements").ToString() : "";
                            bool useTimer = true;
                            if (completion_requirements.Length > 0)
                            {
                                useTimer = false;
                            }

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
                                    string habit_iconC = item.ContainsKey("habit_icon") ? item.GetValue("habit_icon").ToString() : "";

                                    if (habit_iconC.Length > 0)
                                    {
                                        name = habit_iconC + " " + name;
                                    }
                                    List<string> arrVideoUrlsArraychoice = new List<string>(); ;
                                    if (choiceitem.ContainsKey("video_urls"))
                                    {
                                        JArray arrayurlsArraychoice = (JArray)choiceitem.GetValue("video_urls");
                                        arrVideoUrlsArraychoice = arrayurlsArraychoice.ToObject<List<string>>();
                                    }
                                    string logQuantityQuestionchoice = choiceitem.ContainsKey("log_quantity_question") ? choiceitem.GetValue("log_quantity_question").ToString() : "";
                                    string logQuantityTypechoice = choiceitem.ContainsKey("log_summary_type") ? choiceitem.GetValue("log_summary_type").ToString() : "";

                                    List<QuantityQuestion> logQuantityQuestionsCh = new List<QuantityQuestion>();
                                    if (item.ContainsKey("log_quantity_questions"))
                                    {
                                        JArray quesArray = (JArray)choiceitem.GetValue("log_quantity_questions");
                                        //Read one by one
                                        ////
                                        if (quesArray.Count() > 0)
                                        {
                                            foreach (JObject ques in quesArray)
                                            {
                                                string qid = ques.ContainsKey("id") ? ques.GetValue("id").ToString() : "";
                                                int minValue = ques.ContainsKey("min_value") ? (int)ques.GetValue("min_value") : 0;
                                                int maxValue = ques.ContainsKey("max_value") ? (int)ques.GetValue("max_value") : 0;
                                                string question = ques.ContainsKey("question") ? ques.GetValue("question").ToString() : "";
                                                string logQuantityTypeQ = ques.ContainsKey("log_summary_type") ? ques.GetValue("log_summary_type").ToString() : "";
                                                string min_value_description = ques.ContainsKey("min_value_description") ? ques.GetValue("min_value_description").ToString() : "";
                                                string max_value_description = ques.ContainsKey("max_value_description") ? ques.GetValue("max_value_description").ToString() : "";

                                                logQuantityQuestionsCh.Add(new QuantityQuestion(qid, minValue, maxValue, question, logQuantityTypeQ, 0, min_value_description, max_value_description));
                                            }
                                        }
                                    }
                                    bool logQuantitychoice = choiceitem.ContainsKey("log_quantity") ? (bool)choiceitem.GetValue("log_quantity") : false;

                                    if (logQuantityQuestionsCh.Count() == 0)
                                    {
                                        logQuantitychoice = false;
                                    }

                                    string completion_requirementsCH = choiceitem.ContainsKey("completion_requirements") ? choiceitem.GetValue("completion_requirements").ToString() : "";
                                    bool useTimerCH = true;
                                    if (completion_requirementsCH.Length > 0)
                                    {
                                        useTimerCH = false;
                                    }

                                    choicesToAdd.Add(new activityChoice(idchoice, namechoice, arrVideoUrlsArraychoice, logQuantityQuestionchoice, logQuantitychoice, logQuantityTypechoice, logQuantityQuestionsCh, completion_requirementsCH, useTimerCH));

                                }
                            }
                            if (choicesToAdd.Count() == 0)
                            {
                                isRandomChoice = false;
                            }

                            string logQuantityQuestion = item.ContainsKey("log_quantity_question") ? item.GetValue("log_quantity_question").ToString() : "";
                            string logQuantityType = item.ContainsKey("log_summary_type") ? item.GetValue("log_summary_type").ToString() : "";
                            List<QuantityQuestion> logQuantityQuestions = new List<QuantityQuestion>(); ;
                            if (item.ContainsKey("log_quantity_questions"))
                            {
                                JArray quesArray = (JArray)item.GetValue("log_quantity_questions");
                                //Read one by one
                                ////
                                if (quesArray.Count() > 0)
                                {
                                    foreach (JObject ques in quesArray)
                                    {
                                        string qid = ques.ContainsKey("id") ? ques.GetValue("id").ToString() : "";
                                        int minValue = ques.ContainsKey("min_value") ? (int)ques.GetValue("min_value") : 0;
                                        int maxValue = ques.ContainsKey("max_value") ? (int)ques.GetValue("max_value") : 0;
                                        string question = ques.ContainsKey("question") ? ques.GetValue("question").ToString() : "";
                                        string logQuantityTypeQ = ques.ContainsKey("log_summary_type") ? ques.GetValue("log_summary_type").ToString() : "";
                                        string min_value_description = ques.ContainsKey("min_value_description") ? ques.GetValue("min_value_description").ToString() : "";
                                        string max_value_description = ques.ContainsKey("max_value_description") ? ques.GetValue("max_value_description").ToString() : "";

                                        logQuantityQuestions.Add(new QuantityQuestion(qid, minValue, maxValue, question, logQuantityTypeQ, 0, min_value_description, max_value_description));
                                    }
                                }
                            }

                            bool logQuantity = item.ContainsKey("log_quantity") ? (bool)item.GetValue("log_quantity") : false;
                            if (logQuantityQuestions.Count() == 0)
                            {
                                logQuantity = false;
                            }


                            arrAllbreakActivities.Add(new breakActivity(id, name, durationSeconds, arrVideoUrlsArray, logQuantity, logQuantityQuestion, openSpecificApps, allowedUrls, allowedApps, allowedFocusMode, activity_sequence_id, IncludeInEveryBreak, isRandomChoice, choicesToAdd, isOfficeFriendly, logQuantityType, logQuantityQuestions, completion_requirements, useTimer));
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("ReadAndParseSettingsJson() EX - " + ex.Message);
                }
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
                    RegistryManager.GetSavedUserDetailsInfo();
                }
                if (sUserDetailsInfo.Length > 0)
                {

                    //Parse the JSON string
                    try
                    {
                        JObject UserSettings = JObject.Parse(sUserDetailsInfo);
                        sUser_email = UserSettings.ContainsKey("email") ? UserSettings["email"].ToString() : "";
                        sUser_name = UserSettings.ContainsKey("username") ? UserSettings["username"].ToString() : "";
                        sUser_id = UserSettings.ContainsKey("id") ? UserSettings["id"].ToString() : "";

                        LogWriter.LogWrite("EMAIL IS:" + sUser_email);
                        LogWriter.LogWrite("USER Name is:" + sUser_name);
                        LogWriter.LogWrite("USER ID is:" + sUser_id);

                        bEmailVerified = UserSettings.ContainsKey("email_verified") ? Convert.ToBoolean(UserSettings["email_verified"]) : false;

                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("ReadAndParseUserDetailsJson() + " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ReadAndParseUserDetailsJson() top" + ex.Message);
            }
        }
        public void ReadAndParseFocusModes()
        {
            LogWriter.LogWrite("ReadAndParseFocusModes()");

            try
            {
                //Check is settings are there
                if (sFocusModes == null || sFocusModes.Length == 0)
                {
                    LogWriter.LogWrite("sFocusModes == null - Read previously saved in registry");
                    //Read previously saved in registry
                    RegistryManager.GetSavedFocusModes();
                }
                if (sFocusModes.Length > 0)
                {

                    //Parse the JSON string
                    try
                    {

                        JArray jarrFocusModes = JArray.Parse(sFocusModes); 

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
                                    bool isDefault = false;
                                    bool isMeetings = false;
                                    bool isLockedFocus = false;
                                    bool isRelaxFocus = false;
                                    bool isDeepWorkFocus = false;
                                    bool isWorkFocus = false;
                                    string sTooltip = "";
                                    string lastTaskUsed = "";
                                    int focusAlignmentScoreThreshold = 80;
                                    bool isAutoSuggestFocusModeOnAllowedURL = false;

                                    if (name == "Free time - block nothing")
                                    {
                                        sTooltip = ConfigFileStrings["focusPreferencesInfo"]["allWebsitesAllowedText"].ToString();
                                    }
                                    if (name == "Block super distracting sites/apps")
                                    {
                                        sTooltip = ConfigFileStrings["focusPreferencesInfo"]["infoAlwaysBlockedUrlsText"].ToString();
                                    }
                                    if (name == "Deep Work")
                                    {
                                        sTooltip = ConfigFileStrings["focusPreferencesInfo"]["titleAllowUrlListApproch"].ToString();
                                    }

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

                                        if (metadata.ContainsKey("lastTaskUsed"))
                                        {
                                            lastTaskUsed = metadata.GetValue("lastTaskUsed").ToString();
                                        }

                                        color = metadata.ContainsKey("color") ? "#" + metadata.GetValue("color").ToString() : "";
                                        isOn = false; // we will keep it hardcoded false as we are also finishing any focus mode that is running.
                                        isAllowedAllApps = metadata.ContainsKey("isAllowedAllApps") ? Convert.ToBoolean(metadata.GetValue("isAllowedAllApps")) : false;
                                        isAllowedListApproach = metadata.ContainsKey("isAllowedListApproach") ? Convert.ToBoolean(metadata.GetValue("isAllowedListApproach")) : false;
                                        focusAlignmentScoreThreshold = metadata.ContainsKey("focusAlignmentScoreThreshold") ? Convert.ToInt32(metadata.GetValue("focusAlignmentScoreThreshold")) : 80;
                                        isDefault = metadata.ContainsKey("isDefault") ? Convert.ToBoolean(metadata.GetValue("isDefault")) : false;
                                        isMeetings = metadata.ContainsKey("isMeetings") ? Convert.ToBoolean(metadata.GetValue("isMeetings")) : false;
                                        isLockedFocus = metadata.ContainsKey("isLockedFocus") ? Convert.ToBoolean(metadata.GetValue("isLockedFocus")) : false;
                                        isRelaxFocus = metadata.ContainsKey("isRelaxFocus") ? Convert.ToBoolean(metadata.GetValue("isRelaxFocus")) : false;
                                        isDeepWorkFocus = metadata.ContainsKey("isDeepWorkFocus") ? Convert.ToBoolean(metadata.GetValue("isDeepWorkFocus")) : false;
                                        isWorkFocus = metadata.ContainsKey("isWorkFocus") ? Convert.ToBoolean(metadata.GetValue("isWorkFocus")) : false;
                                        isAutoSuggestFocusModeOnAllowedURL = metadata.ContainsKey("autoSuggestFocusModeOnAllowedURL") ? Convert.ToBoolean(metadata.GetValue("autoSuggestFocusModeOnAllowedURL")) : (isWorkFocus ? true : false);
                                    }

                                    arrFocusModes.Add(new FocusMode(id, name, allowedUrls, blcokedURLs, allowedAppsWin, color, isOn, isAllowedAllApps, isAllowedListApproach, isDefault, isMeetings, isLockedFocus, isRelaxFocus, isDeepWorkFocus, isWorkFocus, isAutoSuggestFocusModeOnAllowedURL, (isMeetings ? ConfigFileStrings["focusPreferencesInfo"]["meetingsFocusInfoText"].ToString() : sTooltip), lastTaskUsed, focusAlignmentScoreThreshold));
                                }

                                catch (Exception ex)
                                {
                                    LogWriter.LogWrite("ReadAndParseFocusModes() Reading jarrFocusModes + " + ex.Message);
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

                            //We need to decide if the Focus mode had been setup
                            if (!RegistryManager.GetFocusModeSetupDone())
                            {
                                if (arrFocusModes.Count > 5)
                                {
                                    RegistryManager.SetFocusModeSetupDone(true);
                                }
                            }

                        }
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("ReadAndParseFocusModes() + " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ReadAndParseFocusModes() top" + ex.Message);
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
                if (sUserLocalDeviceSettings != null && sUserLocalDeviceSettings.Length > 0)
                {

                    //Parse the JSON string
                    try
                    {
                        JObject LocalSettings = JObject.Parse(sUserLocalDeviceSettings);
                        string sWebSettings = LocalSettings.ContainsKey("Web") ? LocalSettings["Web"].ToString() : "";
                        if (sWebSettings != null && sWebSettings.Length > 0)
                        {
                            JObject sWebSettingsOBJ = JObject.Parse(sWebSettings);

                            if (sWebSettingsOBJ.ContainsKey("onboarding"))
                            {
                                JObject onboardingObj = sWebSettingsOBJ["onboarding"] as JObject;
                                if (onboardingObj != null && onboardingObj.ContainsKey("currentStep"))
                                {
                                    string currentStep = onboardingObj["currentStep"]?.ToString();
                                    if(currentStep.ToLower() == "completed")
                                    {
                                        //User have done onboarding on Web, so we can skip to show further onboarding screens
                                        RegistryManager.SetOnboardingAfterLoginDoneStatus();
                                    }
                                }
                            }
                           
                        }
                        
                        string sMacOSSettings = LocalSettings.ContainsKey("MacOS") ? LocalSettings["MacOS"].ToString() : "";
                        if (sMacOSSettings != null && sMacOSSettings.Length > 0)
                        {
                            JObject sMacOSSettingsOBJ = JObject.Parse(sMacOSSettings);

                            if (sMacOSSettingsOBJ.ContainsKey("kArrBlockedUrls"))
                            {
                                JArray jarrALwaysBlockedURLs = sMacOSSettingsOBJ.ContainsKey("kArrBlockedUrls") ? (JArray)sMacOSSettingsOBJ["kArrBlockedUrls"] : null;

                                if (jarrALwaysBlockedURLs != null && jarrALwaysBlockedURLs.Count() > 0)
                                {
                                    arrAlwaysBlockedURLs = jarrALwaysBlockedURLs.ToObject<ObservableCollection<string>>(); ;
                                }
                            }

                            if (sMacOSSettingsOBJ.ContainsKey("kArrAlwaysAllowedAppsWin"))
                            {
                                JArray jarrAlwaysAllowedApps = sMacOSSettingsOBJ.ContainsKey("kArrAlwaysAllowedAppsWin") ? (JArray)sMacOSSettingsOBJ["kArrAlwaysAllowedAppsWin"] : null;

                                if (jarrAlwaysAllowedApps != null && jarrAlwaysAllowedApps.Count() > 0)
                                {
                                    arrAlwaysAllowedApps.Clear();
                                    
                                    try
                                    {
                                        arrAlwaysAllowedApps = jarrAlwaysAllowedApps.ToObject<ObservableCollection<AppDetails>>();
                                    }
                                    catch (Exception ex)
                                    {
                                        LogWriter.LogWrite("arrAlwaysAllowedApps() Reading FAILED Possibly old data + " + ex.Message);
                                    }
                                    
                                }
                            }

                            if (sMacOSSettingsOBJ.ContainsKey("kArrAlwaysBlockedAppsWin"))
                            {
                                JArray jarrAlwaysBLockedApps = sMacOSSettingsOBJ.ContainsKey("kArrAlwaysBlockedAppsWin") ? (JArray)sMacOSSettingsOBJ["kArrAlwaysBlockedAppsWin"] : null;

                                if (jarrAlwaysBLockedApps != null && jarrAlwaysBLockedApps.Count() > 0)
                                {
                                    arrAlwaysBlockedApps.Clear();
                                    try
                                    {
                                        arrAlwaysBlockedApps = jarrAlwaysBLockedApps.ToObject<ObservableCollection<AppDetails>>();
                                    }
                                    catch (Exception ex)
                                    {
                                        LogWriter.LogWrite("arrAlwaysBlockedApps() Reading FAILED Possibly old data + " + ex.Message);
                                    }
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

                            bool bCuddlyMode = true;
                            //Cuddly bear or grizlly bear mode
                            if (sMacOSSettingsOBJ.ContainsKey("kCuddlyModeEnabled"))
                            {
                                bCuddlyMode = (bool)sMacOSSettingsOBJ.GetValue("kCuddlyModeEnabled");
                            }

                            if (bCuddlyMode)
                                nFocusBearMode = 1;//cuddly bear mode
                            else
                                nFocusBearMode = 2; //whatever

                            bPasswordRequireInChangeSettings = sMacOSSettingsOBJ.ContainsKey("kIsPasswordRequireInChangeSettings") ? (bool)sMacOSSettingsOBJ.GetValue("kIsPasswordRequireInChangeSettings") : false;
                            bPasswordRequireInSkipMorning = sMacOSSettingsOBJ.ContainsKey("kIsPasswordRequireInSkipMorning") ? (bool)sMacOSSettingsOBJ.GetValue("kIsPasswordRequireInSkipMorning") : false;
                            bPasswordRequireInSkipEvening = sMacOSSettingsOBJ.ContainsKey("kIsPasswordRequireInSkipEvening") ? (bool)sMacOSSettingsOBJ.GetValue("kIsPasswordRequireInSkipEvening") : false;
                            bPasswordRequireInAbortShutoff = sMacOSSettingsOBJ.ContainsKey("kIsPasswordRequireInAbortShutoff") ? (bool)sMacOSSettingsOBJ.GetValue("kIsPasswordRequireInAbortShutoff") : false;
                            bPasswordRequireInStopPomodoro = sMacOSSettingsOBJ.ContainsKey("kIsPasswordRequireInStopPomodoro") ? (bool)sMacOSSettingsOBJ.GetValue("kIsPasswordRequireInStopPomodoro") : false;
                            bPasswordRequireInQuitApp = sMacOSSettingsOBJ.ContainsKey("kIsPasswordRequireInQuitApp") ? (bool)sMacOSSettingsOBJ.GetValue("kIsPasswordRequireInQuitApp") : false;
                            bPasswordRequireToUseRelaxFocusMode = sMacOSSettingsOBJ.ContainsKey("kIsPasswordRequireToUseRelaxFocusMode") ? (bool)sMacOSSettingsOBJ.GetValue("kIsPasswordRequireToUseRelaxFocusMode") : false;
                            bPasswordRequireToSkipEveningAfterCutOff = sMacOSSettingsOBJ.ContainsKey("kIsPasswordRequireInSkipEveningAfterCutOff") ? (bool)sMacOSSettingsOBJ.GetValue("kIsPasswordRequireInSkipEveningAfterCutOff") : false;
                            // Postpone password rules (separate from Skip) - synced as local device settings.
                            bPasswordRequireInPostponeMorning = sMacOSSettingsOBJ.ContainsKey("kIsPasswordRequireInPostponeMorning") ? (bool)sMacOSSettingsOBJ.GetValue("kIsPasswordRequireInPostponeMorning") : false;
                            bPasswordRequireInPostponeEvening = sMacOSSettingsOBJ.ContainsKey("kIsPasswordRequireInPostponeEvening") ? (bool)sMacOSSettingsOBJ.GetValue("kIsPasswordRequireInPostponeEvening") : false;
                            bPasswordRequireInPostponeEveningAfterCutOff = sMacOSSettingsOBJ.ContainsKey("kIsPasswordRequireInPostponeEveningAfterCutOff") ? (bool)sMacOSSettingsOBJ.GetValue("kIsPasswordRequireInPostponeEveningAfterCutOff") : false;

                            bMotivationalMessageEnabled = sMacOSSettingsOBJ.ContainsKey("kMotivationalMessageEnabled") ? (bool)sMacOSSettingsOBJ.GetValue("kMotivationalMessageEnabled") : true; //Default true on 23 June 2023 #188
                            bAiCheckUrlEnabled = sMacOSSettingsOBJ.ContainsKey("kAiCheckUrlEnabled") ? (bool)sMacOSSettingsOBJ.GetValue("kAiCheckUrlEnabled") : true;
                            
                            bPlayActivitiesInFullScreenMode = sMacOSSettingsOBJ.ContainsKey("kisFullScreenEnabled") ? (bool)sMacOSSettingsOBJ.GetValue("kisFullScreenEnabled") : false; //default false

                            bEnterPomodoroModeAfterMorningRoutine = sMacOSSettingsOBJ.ContainsKey("kPomodoroModeEnabled") ? (bool)sMacOSSettingsOBJ.GetValue("kPomodoroModeEnabled") : true; //default true
                            bAutomaticUseSuperPomodoroModeSetting = sMacOSSettingsOBJ.ContainsKey("kHardcorePomodoroEnabled") ? (bool)sMacOSSettingsOBJ.GetValue("kHardcorePomodoroEnabled") : false; //default false
                            PomodoroMinutes = sMacOSSettingsOBJ.ContainsKey("kPomodoroDurationMinutes") ? (string)sMacOSSettingsOBJ.GetValue("kPomodoroDurationMinutes") : "25";
                            PomodoroBreakMinutes = sMacOSSettingsOBJ.ContainsKey("kBreakDurationMinutes") ? (string)sMacOSSettingsOBJ.GetValue("kBreakDurationMinutes") : "5";
                            PomodoroWorkToBreakRatio = sMacOSSettingsOBJ.ContainsKey("kWorkToBreakRatio") ? (int)sMacOSSettingsOBJ.GetValue("kWorkToBreakRatio") : 5;
                            if (PomodoroWorkToBreakRatio < 1) PomodoroWorkToBreakRatio = 5;
                            PomodoroBreakRatio = sMacOSSettingsOBJ.ContainsKey("kBreakRatio") ? (int)sMacOSSettingsOBJ.GetValue("kBreakRatio") : 1;
                            if (PomodoroBreakRatio < 1) PomodoroBreakRatio = 1;

                            AutomaticSuperPomodoroFocusedName = sMacOSSettingsOBJ.ContainsKey("kHardcorePomodoroFocusedName") ? (string)sMacOSSettingsOBJ.GetValue("kHardcorePomodoroFocusedName") : "";
                            AutomaticNumberOfSuperPomodorosToRun = sMacOSSettingsOBJ.ContainsKey("kHardcorePomodoroFocusedCount") ? (int)sMacOSSettingsOBJ.GetValue("kHardcorePomodoroFocusedCount") : 2;
                            if (AutomaticNumberOfSuperPomodorosToRun < 2)
                                AutomaticNumberOfSuperPomodorosToRun = 2;
                            bAutomaticSuperPomodoroSkipMicroBreaks = sMacOSSettingsOBJ.ContainsKey("kHardcorePomodoroSkipMicroBreaks") ? (bool)sMacOSSettingsOBJ.GetValue("kHardcorePomodoroSkipMicroBreaks") : false; //default false
                            AutomaticSuperPomodoroSelectedDaysArray = sMacOSSettingsOBJ.ContainsKey("kHardcorePomodoroArrDays") ? (JArray)sMacOSSettingsOBJ.GetValue("kHardcorePomodoroArrDays") : new JArray();

                            bImmediatelyBlockDistractingSites = sMacOSSettingsOBJ.ContainsKey("kImmediatelyBlockDistractingSites") ? (bool)sMacOSSettingsOBJ.GetValue("kImmediatelyBlockDistractingSites") : false;

                            bEnableMeetingsDetection = sMacOSSettingsOBJ.ContainsKey("kIsMeetingDetectionEnabled") ? (bool)sMacOSSettingsOBJ.GetValue("kIsMeetingDetectionEnabled") : true;
                            bAutomaticallySuggestStartingMeetingsFocus = sMacOSSettingsOBJ.ContainsKey("kShowMeetingDetectionAlert") ? (bool)sMacOSSettingsOBJ.GetValue("kShowMeetingDetectionAlert") : false;
                            bBlockSuperDistractingSitesDuringMeetings = sMacOSSettingsOBJ.ContainsKey("kIsBlockDistractingSitesDuringMeetingsEnabled") ? (bool)sMacOSSettingsOBJ.GetValue("kIsBlockDistractingSitesDuringMeetingsEnabled") : false;
                            bShowBreaksWhileInCall = sMacOSSettingsOBJ.ContainsKey("kIsShowBreaksInCallEnabled") ? (bool)sMacOSSettingsOBJ.GetValue("kIsShowBreaksInCallEnabled") : false;
                            bAdvancedAnalyticsInFocusModeEnabled = sMacOSSettingsOBJ.ContainsKey("kIsAdvancedAnalyticsInFocusModeEnabled") ? (bool)sMacOSSettingsOBJ.GetValue("kIsAdvancedAnalyticsInFocusModeEnabled") : false;


                            bIsGeekModeEnabled = sMacOSSettingsOBJ.ContainsKey("kIsGeekModeEnabled") ? (bool)sMacOSSettingsOBJ.GetValue("kIsGeekModeEnabled") : false;


                            AutoCloseAfterRoutineSkipPostpone = sMacOSSettingsOBJ.ContainsKey("kAutoCloseAfterRoutineSkipPostpone") ? (bool)sMacOSSettingsOBJ.GetValue("kAutoCloseAfterRoutineSkipPostpone") : false;

                            bAreBreaksEnabled = sMacOSSettingsOBJ.ContainsKey("kUserdefaultsBreaksEnabledFromOnboarding") ? (bool)sMacOSSettingsOBJ.GetValue("kUserdefaultsBreaksEnabledFromOnboarding") : true; //breaks ON by default

                            if(!IsFirstInstall)
                            {
                                //Let's keep from the local variable that we saved with user's selection otherwise read from settings
                                nBlockingSchedule = sMacOSSettingsOBJ.ContainsKey("kBlockingSchedule") ? ((int)(sMacOSSettingsOBJ.GetValue("kBlockingSchedule")) == 0 ? BLOCKINGSCHEDULE.UNTIL_I_FINISH_WORK : BLOCKINGSCHEDULE.WHEN_STARTING_FOCUS_SESSION) : BLOCKINGSCHEDULE.UNTIL_I_FINISH_WORK;
                            }

                            // Tab limiter (synced with macOS). One-time migration for existing users takes
                            // precedence over the synced value: if this device has never recorded the on/off
                            // flag, derive it from the existing local tab count (>=1 -> on & keep the count;
                            // otherwise off & count 1 — the count can no longer be 0), persist it, and flag it
                            // to be pushed so the migrated state becomes the synced truth instead of being
                            // overwritten by an older/absent synced value. Once migrated, honour the synced value.
                            if (RegistryManager.GetTabLimiterEnabled() == null)
                            {
                                int localCount = RegistryManager.GetMaxTabLimit();
                                bTabLimiterEnabled = localCount >= 1;
                                nMaxTabLimit = localCount >= 1 ? localCount : 1;
                                RegistryManager.SaveMaxTabLimit(nMaxTabLimit);
                                RegistryManager.SaveTabLimiterEnabled(bTabLimiterEnabled);
                                bTabLimiterMigrationNeedsPush = true;
                                LogWriter.LogWrite("Tab limiter migrated from local count " + localCount + " -> enabled=" + bTabLimiterEnabled + " count=" + nMaxTabLimit);
                            }
                            else if (sMacOSSettingsOBJ.ContainsKey("kisTabLimiterEnabled"))
                            {
                                bTabLimiterEnabled = (bool)sMacOSSettingsOBJ.GetValue("kisTabLimiterEnabled");
                                // Prefer the synced count; fall back to the stored count if the backend didn't
                                // send it, so an enabled limiter never silently resets to 1.
                                nMaxTabLimit = sMacOSSettingsOBJ.ContainsKey("kTabLimitCount")
                                    ? (int)sMacOSSettingsOBJ.GetValue("kTabLimitCount")
                                    : RegistryManager.GetMaxTabLimit();
                                if (bTabLimiterEnabled && nMaxTabLimit < 1) nMaxTabLimit = 1;
                                RegistryManager.SaveMaxTabLimit(nMaxTabLimit);
                                RegistryManager.SaveTabLimiterEnabled(bTabLimiterEnabled);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("ReadAndParseUserLocalDeviceSettingsJson() 1 ex - " + ex.Message);
                    }
                }
                else
                {
                    bIsGeekModeEnabled = false;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ReadAndParseUserLocalDeviceSettingsJson() 2 ex - " + ex.Message);
            }
        }

        public bool CheckIfLocalDeviceSettingsJsonExistsForOldUser()
        {
            LogWriter.LogWrite("CheckIfLocalDeviceSettingsJsonExistsForOldUser()");

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
                        if (sMacOSSettings != null && sMacOSSettings.Length > 0)
                        {
                            JObject sMacOSSettingsOBJ = JObject.Parse(sMacOSSettings);

                            if (sMacOSSettingsOBJ.Count > 0)
                            {
                                return true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("ReadAndParseUserLocalDeviceSettingsJson() 1 ex - " + ex.Message);
                    }
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ReadAndParseUserLocalDeviceSettingsJson() 2 ex - " + ex.Message);
            }
            return false;
        }
        public void AddFocusModeToArrayAndUploadtoAPI(FocusMode ff)
        {
            LogWriter.LogWrite("AddFocusModeToArrayAndUploadtoAPI() - " + ff.name);
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
                        //Call the API to upload the newly created focus mode
                        //add to focus modes  array and Upload to api
                        //#A3FFA1 is the default color
                        string sName = ConfigFileStrings["focusPreferencesInfo"]["defaultFocusText"].ToString(); //Block super distracting sites/apps
                        string sId = System.Guid.NewGuid().ToString();
                        AddFocusModeToArrayAndUploadtoAPI(new FocusMode(sId, sName, new ObservableCollection<string>(), new ObservableCollection<string>(), new ObservableCollection<AppDetails>(), "#A3FFA1", false, true, true, true, false, false, false, false, false));

                        //while onboarding we created a blocking schedule, we need to add this id to that
                        if(arrCustomBlockingSchedules.Count > 0)
                        {
                            arrCustomBlockingSchedules[0].SelectedFocusModeId = sId;
                            _ = Task.Run(async () =>
                            {
                                await Task.Delay(TimeSpan.FromSeconds(10));
                                BlockingScheduleManager.AddBlockingSchedule(arrCustomBlockingSchedules[0]);
                            });
                        }

                    }
                    else if(listItemsD.Count() > 1) //addressing problems where we are seeing duplicate default focus modes
                    {
                        // Remove items in reverse order to avoid index issues
                        for (int i = arrFocusModes.Count - 1; i >= 0; i--)
                        {
                            if (arrFocusModes[i].isDefault && arrFocusModes[i].name != ConfigFileStrings["focusPreferencesInfo"]["defaultFocusText"].ToString()) //Block super distracting sites/apps
                            {
                                //call the API and remove from backend
                                FocusBearAPIClient client = new FocusBearAPIClient();
                                var response = client.DeleteFocusMode(arrFocusModes[i].id);

                                arrFocusModes.RemoveAt(i);
                            }
                        }
                    } 
                    //Check Meetings Focus Mode
                    var listItemsM = arrFocusModes.Where(m => m.isMeetings == true);
                    if (listItemsM.Count() == 0)
                    {
                        LogWriter.LogWrite("isMeetings focus mode not found, create new");
                        //Call the API to upload the newly created focus mode
                        //add to focus modes  array and Upload to api
                        //#A3FFA1 is the default color
                        //public List<string> arrAllowedApps; //wont use this as this is for mac apps
                        ObservableCollection<string> arrAllowedUrls = new ObservableCollection<string>();
                        arrAllowedUrls.Add("meet.google.com");
                        arrAllowedUrls.Add("meet.zoho.com");
                        arrAllowedUrls.Add("focusmate.com");
                        arrAllowedUrls.Add("shimmer.care");
                        arrAllowedUrls.Add("webex.com");
                        arrAllowedUrls.Add("gotomeeting.com");
                        arrAllowedUrls.Add("goto.com/meeting");
                        arrAllowedUrls.Add("skype.com");
                        arrAllowedUrls.Add("adobeconnect");
                        arrAllowedUrls.Add("join.me");
                        arrAllowedUrls.Add("joinme.com");
                        arrAllowedUrls.Add("slack.com");
                        arrAllowedUrls.Add("whereby.com");
                        arrAllowedUrls.Add("chime.aws");
                        arrAllowedUrls.Add("aws.amazon.com/chime/");
                        arrAllowedUrls.Add("dialpad.com");


                        ObservableCollection<AppDetails> arrAllowedAppsWin = new ObservableCollection<AppDetails>();
                        arrAllowedAppsWin.Add(new AppDetails("Microsoft Teams", "teams.exe"));
                        arrAllowedAppsWin.Add(new AppDetails("Zoom", "zoom.exe"));
                        arrAllowedAppsWin.Add(new AppDetails("Jitsi", "jitsi.exe"));
                        arrAllowedAppsWin.Add(new AppDetails("Slack", "slack.exe"));
                        arrAllowedAppsWin.Add(new AppDetails("Microsoft Edge", "msedge.exe"));
                        arrAllowedAppsWin.Add(new AppDetails("Mozilla Firefox", "firefox.exe"));
                        arrAllowedAppsWin.Add(new AppDetails("Google Chrome", "chrome.exe"));
                        arrAllowedAppsWin.Add(new AppDetails("Brave Browser", "brave.exe"));
                        arrAllowedAppsWin.Add(new AppDetails("Vivaldi Browser", "vivaldi.exe"));
                        arrAllowedAppsWin.Add(new AppDetails("Comet", "comet.exe"));
                        arrAllowedAppsWin.Add(new AppDetails("Slack", "slack.exe"));

                        string sName = ConfigFileStrings["focusPreferencesInfo"]["meetingsFocusText"].ToString(); //"meetingsFocusText":"Meetings",
                        AddFocusModeToArrayAndUploadtoAPI(new FocusMode(System.Guid.NewGuid().ToString(), sName, arrAllowedUrls, new ObservableCollection<string>(), arrAllowedAppsWin, "#A3FFA1", false, false, true, false, true, false, false, false,false));

                    }
                    else if (listItemsM.Count() > 1) //addressing problems where we are seeing duplicate meetings focus modes
                    {
                        // Remove items in reverse order to avoid index issues
                        for (int i = arrFocusModes.Count - 1; i >= 0; i--)
                        {
                            if (arrFocusModes[i].isMeetings && arrFocusModes[i].name != ConfigFileStrings["focusPreferencesInfo"]["meetingsFocusText"].ToString())
                            {
                                LogWriter.LogWrite("ReadAndCreateDefaultFocusModes - removing duplicate meetings focus mode: " + arrFocusModes[i].name);
                                FocusBearAPIClient client = new FocusBearAPIClient();
                                var response = client.DeleteFocusMode(arrFocusModes[i].id);
                                arrFocusModes.RemoveAt(i);
                            }
                        }
                    }
                    //Check Locked Focus Mode
                    var listItemsL = arrFocusModes.Where(l => l.isLockedFocus == true);
                    if (listItemsL.Count() == 0)
                    {
                        LogWriter.LogWrite("isLockedFocus focus mode not found, create new");
                        //Call the API to upload the newly created focus mode
                        //add to focus modes  array and Upload to api
                        //#A3FFA1 is the default color
                        string sName = ConfigFileStrings["focusPreferencesInfo"]["lockedFocusText"].ToString();//        "lockedFocusText":"Locked Focus on current activity",
                        AddFocusModeToArrayAndUploadtoAPI(new FocusMode(System.Guid.NewGuid().ToString(), sName, new ObservableCollection<string>(), new ObservableCollection<string>(), new ObservableCollection<AppDetails>(), "#A3FFA1", false, false, true, false, false, true, false, false,false));
                    }

                    //Check Relax Focus Mode
                    var listItemsR = arrFocusModes.Where(l => l.isRelaxFocus == true);
                    if (listItemsR.Count() == 0)
                    {
                        LogWriter.LogWrite("isRelaxFocus focus mode not found, create new");
                        //Call the API to upload the newly created focus mode
                        //add to focus modes  array and Upload to api
                        //#A3FFA1 is the default color
                        string sName = ConfigFileStrings["focusPreferencesInfo"]["relaxFocusText"].ToString(); //"Relax - block nothing"
                        AddFocusModeToArrayAndUploadtoAPI(new FocusMode(System.Guid.NewGuid().ToString(), sName, new ObservableCollection<string>(), new ObservableCollection<string>(), new ObservableCollection<AppDetails>(), "#A3FFA1", false, true, false, false, false, false, true, false, false));
                    }

                    //Check Deep Work Focus Mode
                    var listItemsDeep = arrFocusModes.Where(l => l.isDeepWorkFocus == true);
                    if (listItemsDeep.Count() == 0)
                    {
                        LogWriter.LogWrite("isDeepWorkFocus focus mode not found, create new");
                        //Call the API to upload the newly created focus mode
                        //add to focus modes  array and Upload to api
                        //#A3FFA1 is the default color
                        ObservableCollection<string> arrAllowedUrls = new ObservableCollection<string>();
                        arrAllowedUrls.Add("google.com");

                        string sName = "Deep Work";// todo win texts ConfigFileStrings["focusPreferencesInfo"]["relaxFocusText"].ToString(); //"Relax - block nothing"
                        AddFocusModeToArrayAndUploadtoAPI(new FocusMode(System.Guid.NewGuid().ToString(), sName, arrAllowedUrls, new ObservableCollection<string>(), new ObservableCollection<AppDetails>(), "#A3FFA1", false, true, true, false, false, false, false, true,false));
                    }

                    var listItemsWork = arrFocusModes.Where(l => l.isWorkFocus == true);
                    if (listItemsWork.Count() == 0)
                    {
                        string sName = "Work"; //to do win texts
                        string sId = System.Guid.NewGuid().ToString();
                        ObservableCollection<string> arrAllowedUrls = new ObservableCollection<string>();
                        ObservableCollection<string> arrBlockedUrls = new ObservableCollection<string>();

                        //Only create Work focusmode when some values are there
                        if (AllowedURLsWhileOnboarding.Count() > 0 || DistractingURLsWhileOnboarding.Count() > 0)
                        {
                            foreach (DistractingURL item in AllowedURLsWhileOnboarding)
                            {
                                if (item.IsItemChecked)
                                    arrAllowedUrls.Add(item.sURL);
                            }
                            foreach (DistractingURL item in DistractingURLsWhileOnboarding)
                            {
                                if (item.IsItemChecked)
                                    arrBlockedUrls.Add(item.sURL);
                            }

                            AddFocusModeToArrayAndUploadtoAPI(new FocusMode(sId, sName, arrAllowedUrls, arrBlockedUrls, new ObservableCollection<AppDetails>(), "#A3FFA1", false, true, true, false, false, false, false, false, true));
                        }

                    }
                }

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ReadAndCreateDefaultFocusModes() EX - " + ex.Message);
            }
        }
        public bool PrepareBreakActivities()
        {
            LogWriter.LogWrite("PrepareBreakActivities()");
            if (arrAllbreakActivities.Count() != 0)
            {
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
                        isOfficeModeEnabled = false;
                        RegistryManager.SetisOfficeModeEnabled(false);

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
                    return false;
                }

                //If its expired version we will need to limit to three activities
                if (isExpired)
                {
                    bShowReducedHabitsOnExpired = false;
                    for (int i = 0; i < arrActivitiesIncludeInEveryBreak.Count; i++)
                    {
                        if (i > 2)
                        {
                            arrActivitiesIncludeInEveryBreak.RemoveAt(i);
                            i--;
                            bShowReducedHabitsOnExpired = true;
                        }
                    }
                    if (bShowReducedHabitsOnExpired) //remove all from arrActivitiesExcludeInEveryBreak as we have 3 in include
                        arrActivitiesExcludeInEveryBreak.Clear();
                    else
                    {
                        for (int i = 0; i < arrActivitiesExcludeInEveryBreak.Count; i++)
                        {
                            if (i > 2)
                            {
                                arrActivitiesExcludeInEveryBreak.RemoveAt(i);
                                i--;
                                bShowReducedHabitsOnExpired = true;
                            }
                        }
                    }
                }


                if (isAnyMandatoryActivityIncluded)
                {
                    isMandatoryActivityGoing = true;
                    nHabitIndex = 0;
                    LogWriter.LogWrite("nHabitIndex = " + nHabitIndex.ToString());

                    LogWriter.LogWrite(" isMandatoryActivityGoing = true; and Habit Index = 0");
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

                    LogWriter.LogWrite("nHabitIndex = " + nHabitIndex);

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
                    string csv = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}", sLogType, sTimeStamp, sActivityName, sDurationInSeconds, sLogQuantityQuestion, sQuantityLogged, sStartTime, EscapeForCsv(sWorkAchieved), EscapeForCsv(sDistractionsEncountered), EscapeForCsv(sAbortReason), EscapeForCsv(sFeedback));
                    sw.WriteLine(csv);
                    sw.Flush();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AddEntryInCSV - " + ex.Message);
            }

        }
        string EscapeForCsv(string value)
        {
            // Remove or replace line breaks
            value = value.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");

            if (value.Contains("\""))
                value = value.Replace("\"", "\"\""); // Escape quotes

            if (value.Contains(","))
                return $"\"{value}\""; // Still wrap in quotes if it has a comma

            return value;
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
                    Console.WriteLine(data[1].ToString());
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

        public void WriteDefaultOffilineCSVFile()
        {
            LogWriter.LogWrite("WriteDefaultOffilineCSVFile()");

            try
            {
                if (File.Exists(sOfflineDataCSVPath))
                {
                    return;
                }
                using (var w = new StreamWriter(sOfflineDataCSVPath))
                {
                    var sHeaders = "activity_id,duration_logged,activity_sequence_id,start_time,finish_time,skipped_did_complete,choice_id,quantity_logged,skipped_did_not_complete,note_logged";
                    w.WriteLine(sHeaders);
                    w.Flush();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("WriteDefaultOffilineCSVFile ex- " + ex.Message);
            }
        }

        //log a new entry to the offline data csv

        public void AddEntryInOfflineActivitiesCSV(string activity_id, string duration_logged, string activity_sequence_id, string start_time, string finish_time, string skipped_did_complete, string choice_id, string quantity_logged, string skipped_did_not_complete, string note_logged)
        {
            LogWriter.LogWrite("AddEntryInOfflineActivitiesCSV()");

            try
            {
                using (System.IO.StreamWriter csvWriter = System.IO.File.AppendText(sOfflineDataCSVPath))
                {
                    string csv = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}", activity_id, duration_logged, activity_sequence_id, start_time, finish_time, skipped_did_complete, choice_id, quantity_logged, skipped_did_not_complete, note_logged);
                    csvWriter.WriteLine(csv);
                    csvWriter.Flush();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AddEntryInOfflineActivitiesCSV ex - " + ex.Message);
            }

        }

        public bool IsShutOffTime()
        {
            TimeSpan tsStartTime = TimeSpan.Parse(sDayStartTime);
            TimeSpan tsShutOffTime = TimeSpan.Parse(sDayShutOffTime);

            DateTime now = DateTime.Now;

            DateTime myStartTime = new DateTime(now.Year, now.Month, now.Day, tsStartTime.Hours, tsStartTime.Minutes, 00);
            Console.WriteLine("{0}", myStartTime.ToString());

            DateTime myShutOffTime = new DateTime(now.Year, now.Month, now.Day, tsShutOffTime.Hours, tsShutOffTime.Minutes, 00);
            Console.WriteLine("{0}", myShutOffTime.ToString());

            bool isShutOffTime = false;
            if (myStartTime > myShutOffTime)
            {
                if (now < myStartTime && now > myShutOffTime)
                {
                    isShutOffTime = true;
                }
            }
            else if (now < myStartTime || now > myShutOffTime)
            {
                isShutOffTime = true;
            }
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
                    return string.Format("{0} {1}", secs, sSeconds);

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SecondsToText" + ex.Message);
            }

            return "";
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
                    return string.Format("{0}{1}", secs, sSeconds);

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SecondsToTextHMS" + ex.Message);
            }

            return "";
        }

        public string GetFocusPauseButtonText()
        {
            try
            {
                if (bUseSuperPomodoroMode)
                {
                    return ConfigFileStrings["menuInfo"]["menuItemStopHardcorePomodoroTitle"].ToString();
                }

                string stopTitle = ConfigFileStrings["menuInfo"]["menuItemStopPomodoroTitle"].ToString();
                string resumeTitle = ConfigFileStrings["menuInfo"]["menuItemResumeFocusTitle"].ToString();
                return isFocusModePaused ? resumeTitle : stopTitle;
            }
            catch
            {
                return "Stop Focus Session";
            }
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
                LogWriter.LogWrite("GetCurrentAppVersion() EX- " + ex.Message);

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
                                return;

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
            if ((userAccount.email != null && userAccount.email.Length > 0) && userAccount.email.ToLower().Contains("internaltest"))
            {
                return; //Do not log events for internal test users
            }
            else if(bIsTestUser)
            {
                return;
            }

            FocusBearAPIClient client = new FocusBearAPIClient();
            var xResponse = client.CapturePostHog(sEvent, sMetadataJSON);

            if (userAccount.accessToken.Length > 0) //it is possible that this function is called before the user actually logs in, in that case we willnot call thebelow endpoint
            {
                var y = client.CaptureEventEndpoint(sEvent, sMetadataJSON);
            }
        }

        // "Stay anonymous" onboarding creates users with this email prefix (see SetupAccount);
        // shared here so anonymous-user detection stays in sync with account creation.
        public const string AnonymousEmailPrefix = "anonymoususer+";

        public bool IsAnonymousUser()
        {
            return userAccount.email != null &&
                   userAccount.email.StartsWith(AnonymousEmailPrefix, StringComparison.OrdinalIgnoreCase);
        }

        // guest = anonymous ("stay anonymous") account, registered = real account.
        public string CurrentAuthState()
        {
            return IsAnonymousUser()
                ? Constants.Events.AuthState.Guest
                : Constants.Events.AuthState.Registered;
        }

        // Records a session-loss with the reason so analytics can tell a deliberate sign-out
        // from an involuntary one (analytics#33). Call this immediately before clearing the
        // login state / showing the sign-in screen, while the user id is still available.
        public void CaptureLogout(string reason)
        {
            CapturePostHog(Constants.Events.Logout, $"\"reason\":\"{reason}\"");
        }

        // Tags the sign-in/sign-up view with the current auth_state (analytics#33).
        public void CaptureAuthViewShown(string sEvent)
        {
            CapturePostHog(sEvent, $"\"auth_state\":\"{CurrentAuthState()}\"");
        }

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
                    SecondaryWindows.Clear(); //Let's also empty the array
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("RemoveSecondaryWindows ex - " + ex.Message);
            }

        }

        public void ShowSuggestStartWorkFocusModeWindow(IntPtr hWnd, FocusMode focusType)
        {
            if (focusType.bUserClickedNoDoNotSuggestAnymore)
                return;
            LogWriter.LogWrite("ShowSuggestStartWorkFocusModeWindow - focus name = " + focusType.name);

            ((App)(Application.Current)).Dispatcher.BeginInvoke((Action)(() =>
            {
                string sTemp = ConfigFileStrings["alertStartWorkFocusMode"]["title"].ToString();
                sTemp = sTemp.Replace("<WORK>", focusType.name);
                FBMessageBox msg = new FBMessageBox(ConfigFileStrings["alertStartWorkFocusMode"]["subTitle"].ToString(),
                    sTemp,
                    MessageBoxButton.YesNo, ConfigFileStrings["alertStartWorkFocusMode"]["buttonYes"].ToString(), ConfigFileStrings["alertStartWorkFocusMode"]["buttonNotNow"].ToString(), "", false, "", true, MessageBoxResult.No);
                msg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                msg.ShowDialog();
                if (msg.btnClicked == MessageBoxResult.No)
                {
                    LogWriter.LogWrite("ShowSuggestStartWorkFocusModeWindow - NO");

                    focusType.bUserClickedNoDoNotSuggestAnymore = true;
                    return;
                }
                else
                {
                    //Start the focus mode
                    ((App) Application.Current).StartWorkFocusMode(focusType.id);
                }

            }));
        }
        public void ShowGentleBlockWindow(IntPtr hWnd, string sEXE, string sURL = "", string sTabTitle = "")
        {
            ((App)(Application.Current)).Dispatcher.BeginInvoke((Action)(() =>
            {
                if (wndGentleBlock == null)
                {
                    wndGentleBlock = new GentleBlock();
                }

                if (bHabitwithUseFocusModeRunning)//if an activity with use focus mode is running, we need to show the activity name
                {
                    wndGentleBlock.ShowGentleBlockWindowAndStartTimer(hWnd, sCurrentRunningActivity, sEXE, sURL, "", sTabTitle);
                }
                else
                {
                    wndGentleBlock.ShowGentleBlockWindowAndStartTimer(hWnd, sRunningFocusModeName, sEXE, sURL, sRunningFocusModeIntention, sTabTitle);
                }
            }));
        }

        public void HideGentleBlockWindow()
        {
            ((App)(Application.Current)).Dispatcher.BeginInvoke((Action)(() =>
            {
                if (wndGentleBlock != null)
                {
                    if (wndGentleBlock.IsVisible)
                        HideBlurWindow();
                    wndGentleBlock.StopTimer(); //also take care of closing modal popups over it
                    wndGentleBlock.Hide();

                }
            }));
        }

        // The focus mode that currently governs blocking: the running habit's use-focus-mode, else the
        // active (non-paused) blocking schedule's mode, else the switched-on focus mode. Returns null if
        // none resolves. Used to pre-check for a "block nothing" (Relax) mode so neither the app nor the
        // URL path blocks anything while it is active (#1262).
        public FocusMode GetActiveBlockingFocusMode()
        {
            try
            {
                string id = null;
                if (bHabitwithUseFocusModeRunning)
                    id = sUseFocusModeIDInActivity;
                else if (isBlockingScheduleRunning && ActiveSchedule != null && !ActiveSchedule.isPaused)
                    id = ActiveSchedule.SelectedFocusModeId;

                if (!string.IsNullOrEmpty(id))
                    return arrFocusModes.FirstOrDefault(i => i.id == id);

                return arrFocusModes.FirstOrDefault(i => i.isOn == true);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetActiveBlockingFocusMode ex - " + ex.Message);
                return null;
            }
        }

        public bool DidCutOffTimePassed()
        {
            try
            {
                if (sDayCutOffTime != null && sDayCutOffTime.Length > 0)
                {
                    DateTime now = DateTime.Now;
                    //check if cutoff time have elapsed
                    TimeSpan tsCutoffTime = TimeSpan.Parse(sDayCutOffTime);
                    DateTime myCutoffTime = new DateTime(now.Year, now.Month, now.Day, tsCutoffTime.Hours, tsCutoffTime.Minutes, 00);
                    Console.WriteLine("{0}", myCutoffTime.ToString());

                    //In the overnight window (after cutoff, before next morning start) the relevant
                    //cutoff is the previous day's — mirror IsShutOffTime() logic to handle this.
                    if (sDayStartTime != null && sDayStartTime.Length > 0)
                    {
                        TimeSpan tsStartTime = TimeSpan.Parse(sDayStartTime);
                        DateTime myStartTime = new DateTime(now.Year, now.Month, now.Day, tsStartTime.Hours, tsStartTime.Minutes, 00);

                        if (myStartTime > myCutoffTime)
                            return now < myStartTime && now > myCutoffTime;
                        else
                            return now < myStartTime || now > myCutoffTime;
                    }

                    if (now > myCutoffTime)
                        return true;
                    else
                        return false;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DidCutOffTimePassed() ex - " + ex.Message);
                return false;
            }
        }

        public string GetFontEquivalentForWeb(string sAppFont)
        {
            if (sAppFont == "IntelOne Mono")
            {
                return "Intel One Mono";
            }
            else if (sAppFont == "OpenDyslexic3 Regular")
            {
                return "Open Dyslexic";
            }
            else
            {
                return sAppFont; //rest are same as our array
            }
        }

        public void ShowSetIntentionWindow(IntPtr hWnd, string sEXE, string sURL = "", string sTabTitle = "", bool bShowTimerAndWarning = false)
        {
            ((App)(Application.Current)).Dispatcher.BeginInvoke((Action)(() =>
            {
                ShowBlurWindow(hWnd);

                if (wndSetIntention == null)
                {
                    wndSetIntention = new SetIntention.SetIntention();
                }
                wndSetIntention.ShowSetIntentionWindow(hWnd, sEXE, sURL, sTabTitle, bShowTimerAndWarning);
            }));
        }
        public void Show_ShowIntentionWindow(IntPtr hWnd, string sURL, string sIntention)
        {
            ((App)(Application.Current)).Dispatcher.BeginInvoke((Action)(() =>
            {
                if (wndShowIntention == null)
                {
                    wndShowIntention = new ShowIntention();
                }
               
                if (bHabitwithUseFocusModeRunning)//if an activity with use focus mode is running, we need to show the activity name
                {
                    wndShowIntention.Show_ShowIntentionWindow(hWnd, sURL, sIntention);
                }
                else
                {
                    wndShowIntention.Show_ShowIntentionWindow(hWnd, sURL, sIntention);
                }
            }));
        }

        public void Hide_SetIntentionWindow()
        {
            ((App)(Application.Current)).Dispatcher.BeginInvoke((Action)(() =>
            {
                if (wndSetIntention != null)
                {
                    wndSetIntention.Hide();
                }
                HideBlurWindow();
            }));
        }
        public void Hide_ShowIntentionWindow()
        {
            if (wndShowIntention != null)
            {
                wndShowIntention.Hide();
            }

        }
        public void GetTotalTimeForDistractingSitesToday() //we will use thi to also get the nTimeSpentBlockingDeactivated and nTimeSpentBlockingActivated
        {
            if(RegistryManager.GetDateFor_TotalTimeForDistractingSites())
            {
                if(DateTime.Today.Date == Date_TotalTimeForDistractingSitesToday.Date)
                {
                    RegistryManager.GetTotalTimeForDistractingSites();

                    RegistryManager.GetTotalTimeForBlockingActivated();
                    RegistryManager.GetTotalTimeForBlockingDeactivated();
                    RegistryManager.GetTotalDistractingSiteBlockedToday();
                }
                else
                {
                    nTotalTimeForDistractingSitesToday = 0;
                    nTimeSpentBlockingActivated = 0;
                    nTimeSpentBlockingDeactivated = 0;
                    nTotalDistractingSitesBlocked = 0;


                    Date_TotalTimeForDistractingSitesToday = DateTime.Now;
                    RegistryManager.SetDateFor_TotalTimeForDistractingSites();
                }
            }
            else
            {
                nTotalTimeForDistractingSitesToday = 0;
                nTimeSpentBlockingActivated = 0;
                nTimeSpentBlockingDeactivated = 0;
                nTotalDistractingSitesBlocked = 0;

                Date_TotalTimeForDistractingSitesToday = DateTime.Now;
                RegistryManager.SetDateFor_TotalTimeForDistractingSites();
            }
        }

        public async void ChangeStartupTimeToCurrentTimeAndSaveSettings()
        {
            LogWriter.LogWrite("ChangeStartupTimeToCurrentTimeAndSaveSettings()");

            JObject UserSettings = JObject.Parse(sRawUserSettings);
            DateTime now = DateTime.Now;
           
            Console.WriteLine(now.ToString("HH:mm"));

            if (UserSettings.ContainsKey("startup_time"))
            {
                UserSettings.Remove("startup_time");
                UserSettings.Add("startup_time", now.ToString("HH:mm"));
            }
           
            sDayStartTime = now.ToString("HH:mm");
           
            //Also save the udpated settings to Registry
            sRawUserSettings = UserSettings.ToString();
            RegistryManager.SaveUserSettings();

            await Task.Run(async () =>
            {
                FocusBearAPIClient client = new FocusBearAPIClient();
                //We will need to put the settings.
                var y = client.PutUserSettings();
            });
        }

        public async void CheckLogFileRotation()
        {

            LogWriter.LogWrite("CheckLogFileRotation() ");
           
            var DebugLogsFilePath = Path.Combine(AppSettings.AppDataDirLogsPath, "Debug_Logs.txt");

            DateTime? creationDate = FileCreationDate(DebugLogsFilePath);
            if (creationDate != null)
            {
                Console.WriteLine(creationDate);
                TimeSpan diff = DateTime.Now - creationDate.Value;
                Console.WriteLine(diff.TotalDays);

                if (diff.TotalDays > 3)
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd");
                    string oldLogPath = Path.Combine(AppSettings.AppDataDirLogsPath, $"Debug_Logs-{timestamp}.txt");

                    LogWriter.LogWrite("CheckLogFileRotation:- move log files");
                    try
                    {
                        File.Move(DebugLogsFilePath, oldLogPath);

                        //Need to create teh debug file and wait a while
                        LogWriter.LogWrite("CheckLogFileRotation :- create / write to Debug_Logs.txt");
                        await Task.Delay(2);
                        File.SetCreationTime(DebugLogsFilePath, DateTime.Now); //we will need to set the CreationDate as File.Move() preserves the original CreationTime Only the path changes
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("CheckLogFileRotation ex - " + ex);
                    }
                }
            }

            try
            {
                var logFiles = Directory.GetFiles(AppSettings.AppDataDirLogsPath, "*.txt")
                    .Select(f => new FileInfo(f))
                    .ToList();

                var logFileInfos = new List<Dictionary<string, object>>();

                foreach (var fileInfo in logFiles)
                {
                    logFileInfos.Add(new Dictionary<string, object>
                {
                    { "path", fileInfo.FullName },
                    { "date", fileInfo.CreationTime }
                });
                }

                if (logFileInfos.Count > 5)
                {
                    var sorted = logFileInfos.OrderByDescending(x => (DateTime)x["date"]).ToList();

                    while (sorted.Count > 5)
                    {
                        try
                        {
                            File.Delete((string)sorted[5]["path"]);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                        }
                        sorted.RemoveAt(5);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private DateTime? FileCreationDate(string path)
        {
            if (File.Exists(path))
            {
                return File.GetCreationTime(path);
            }
            return null;
        }


        public void ShowBaloonTip(string sBaloonTip, string sHeading = "")
        {
            ((App)(Application.Current)).Dispatcher.BeginInvoke((Action)(() =>
            {
                TrayBalloon tt = new TrayBalloon(1, sBaloonTip, sHeading);
                tt.Show();
            }));
        }

        // Balloon tip with an action button (e.g. teacher class session "Request unlock").
        public void ShowBaloonTip(string sBaloonTip, string sHeading, string sActionButtonText, Action onAction)
        {
            ((App)(Application.Current)).Dispatcher.BeginInvoke((Action)(() =>
            {
                TrayBalloon tt = new TrayBalloon(1, sBaloonTip, sHeading, sActionButtonText, onAction);
                tt.Show();
            }));
        }
        
        public async void ShowBlurWindow(IntPtr hwndTarget)
        {
#if DEBUG
         return;
#endif

            if (wndBlurWindow == null)
            {
                wndBlurWindow = new BlurWindow();
            }

            PositionBlurWindowOn(hwndTarget, wndBlurWindow);
            wndBlurWindow.Show();
            await Task.Delay(1000); // 1000 milliseconds = 1 seconds
        }
        public void HideBlurWindow()
        {
            if(wndBlurWindow != null)
            {
                wndBlurWindow.Hide();
            }
        }

        public void PositionBlurWindowOn(IntPtr hwndTarget, Window blurWindow)
        {
            if (GetWindowRect(hwndTarget, out RECT rect))
            {
                // Convert size and position
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;

                // Browsers: leave ~90px at the top so the address bar stays visible (the user needs it to
                // navigate away from the blocked site). Apps have no address bar, so cover the whole window.
                int topOffset = IsBrowserWindow(hwndTarget) ? 90 : 0;

                blurWindow.Left = rect.Left;
                blurWindow.Top = rect.Top + topOffset;
                blurWindow.Width = width;
                blurWindow.Height = height;
            }
        }

        // True when the target window belongs to a supported browser process; used to decide whether the
        // blur leaves the address bar visible (browser) or covers the whole window (app).
        private bool IsBrowserWindow(IntPtr hwnd)
        {
            try
            {
                GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == 0) return false;
                using (var p = System.Diagnostics.Process.GetProcessById((int)pid))
                {
                    return BlurBrowserProcessNames.Contains(p.ProcessName);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("IsBrowserWindow ex - " + ex.Message);
                return false;
            }
        }

        public void ShowReportBadAIDecision(string sPayload, string sURL)
        {
            ((App)(Application.Current)).Dispatcher.BeginInvoke((Action)(() =>
            {
                if (wndReportBadAIDecision == null)
                {
                    wndReportBadAIDecision = new ReportBadAIDecision();
                }
                wndReportBadAIDecision.ShowReportBadAIDecisionWithPayload(sPayload, sURL);
                wndReportBadAIDecision.Show();
            }));
        }

        public async void GetTotalDeepFocusedTime()
        {
            LogWriter.LogWrite("GetTotalDeepFocusedTime()");

            TotalDeepFocusedTime = 0;
            
            try
            {
                FocusBearAPIClient client = new FocusBearAPIClient();
                var xResponse = await client.GetActivityAllDaySummary();


                //Parse the Activity data got from API
                if (sActivityAllDaySummaryData.Length > 0)
                {
                    JObject sActivityData = JObject.Parse(sActivityAllDaySummaryData);
                    if (sActivityData.ContainsKey("focusSummary"))
                    {
                        JArray arrayItems = (JArray)sActivityData["focusSummary"];

                        foreach (JObject item in arrayItems)
                        {
                            int durationSeconds = item.ContainsKey("duration") ? (int)item.GetValue("duration") : 0;

                            if (item.TryGetValue("metadata", out JToken metadataToken) &&
                                    metadataToken is JObject metadataObj &&
                                    metadataObj.TryGetValue("focus_alignment_score", out JToken fcaToken))
                            {
                                double fca = fcaToken.Value<double>();
                                var deepWorkDuration = (durationSeconds * fca) / 100;
                                TotalDeepFocusedTime += deepWorkDuration;
                            }
                            else
                            {
                                TotalDeepFocusedTime += durationSeconds;
                            }

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("GetTotalDeepFocusedTime() ex + " + ex.Message);
            }

        }

        public void AddJustification(string justification)
        {
            _last5Justifications.Enqueue(justification);

            while (_last5Justifications.Count > MaxJustifications)
            {
                _last5Justifications.Dequeue();
            }
        }
    }
}


