using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace Focus_Bear.Pomodoro
{
    /// <summary>
    /// Starting a focus session from a start screen — the code the old StartPomodoro / StartSimplePomodoro
    /// screens each carried, moved here unchanged so the new start screens (FocusStartScreens) run exactly
    /// the same steps: the #1420 allow-list warning, the Teacher Mode class session, the session globals, the
    /// progress window, the timer and the backend start.
    /// </summary>
    internal static class FocusSessionStarter
    {
        private static AppSettings Settings => AppSettings.GetInstance;
        private static App TheApp => (App)Application.Current;

        /// <summary>
        /// #1420: an Allow-List mode with AI off means every new page on an already-allowed site still gets a
        /// permission prompt — warn before starting. Skipped in grizzly mode, which blocks with no back-and-forth.
        /// </summary>
        internal static void WarnAllowListWithoutAi(FocusMode mode, FocusBearMainWindow parent)
        {
            if (mode == null || !mode.isAllowedListApproach || Settings.bAiCheckUrlEnabled || Settings.IsEffectivelyGrizzly()) return;
            parent.GridShade.Visibility = Visibility.Visible;
            FBMessageBox msg = new FBMessageBox(
                Settings.GetConfigStringSafe("You've started an allow list session without AI which means you're going to get asked for approval a lot.\n\nWe suggest you either make the session stricter or enable AI.", "focusInfo", "startingFocusModeInfo", "bodyAllowListNoAiWarningText"),
                Settings.GetConfigStringSafe("This might be annoying", "focusInfo", "startingFocusModeInfo", "titleAllowListNoAiWarning"),
                MessageBoxButton.YesNoCancel,
                Settings.GetConfigStringSafe("Make it stricter - block non-allowed sites immediately", "focusInfo", "startingFocusModeInfo", "buttonMakeStricterText"),
                Settings.GetConfigStringSafe("Enable AI", "focusInfo", "startingFocusModeInfo", "buttonTurnOnAiText"),
                Settings.GetConfigStringSafe("Continue anyway", "focusInfo", "startingFocusModeInfo", "buttonStartWithoutAiText"),
                bCancelAsLink: true);
            msg.Owner = parent;
            msg.ShowDialog();
            parent.GridShade.Visibility = Visibility.Collapsed;

            if (msg.btnClicked == MessageBoxResult.Yes) //Make it stricter
            {
                Settings.nFocusBearMode = 0; //Grizzly bear mode
                LocalSettingsManager.PutLocalSettings();
            }
            else if (msg.btnClicked == MessageBoxResult.No) //Enable AI
            {
                Settings.bAiCheckUrlEnabled = true;
                LocalSettingsManager.PutLocalSettings();
            }
            //Cancel = Continue anyway - proceed with the session as configured
        }

        /// <summary>
        /// Teacher Mode (#1343): a teacher who did not ask for a personal session gets the class session
        /// created first. Returns false (with <c>sLastClassSessionError</c>) when the backend refuses, in which
        /// case nothing may start.
        /// </summary>
        internal static async Task<bool> StartClassSessionIfTeacher(FocusMode mode, int minutes, bool personalSession)
        {
            Settings.bTeacherStartPersonalSession = personalSession;
            if (!TheApp.IsTeacherClassSessionStart() || mode == null) return true;
            Settings.sRunningFocusModeEndTime = DateTime.Now.AddMinutes(minutes).ToString();
            return await TheApp.StartClassSessionForMode(mode) == 1;
        }

        /// <summary>
        /// Starts <paramref name="mode"/> for <paramref name="minutes"/>. <paramref name="rawTasks"/> is the task
        /// queue chosen on the start screen ("" or "[]" for none) — never the mode's <c>lastTaskUsed</c>.
        /// </summary>
        /// <summary>
        /// E2E build only (the probe's <c>start-session</c>): a focus session on the named focus mode, as the advanced
        /// start screen's Start runs it, without driving the screen. <paramref name="enableAi"/> answers the "allow list
        /// without AI" warning with Enable AI. Returns "" or why it didn't start.
        /// </summary>
        internal static string StartForE2E(string modeName, string goal, int minutes, bool enableAi)
        {
            if (!Focus_Bear.Utils.E2EBuild.IsActive) return "not the E2E build";
            if (Settings.isFocusModeRunning) return "a session is already running";
            FocusMode mode = null;
            foreach (var m in Settings.arrFocusModes) if (m.name == modeName) { mode = m; break; }
            if (mode == null) return "no focus mode named " + modeName;
            if (mode.isAllowedListApproach && !Settings.bAiCheckUrlEnabled && enableAi)
            {
                Settings.bAiCheckUrlEnabled = true;
                LocalSettingsManager.PutLocalSettings();
            }
            Settings.sRunningFocusModeIntention = goal ?? "";
            int threshold = mode.AlignmentScoreThreshold >= 50 && mode.AlignmentScoreThreshold <= 100 ? mode.AlignmentScoreThreshold : 80;
            Settings.sRunningFocusModeAlignmentScoreThreshold = (double)threshold / 100;
            Settings.sMinutesBreakPomodoro = "";
            LogWriter.LogWrite("E2E: starting a focus session (test seam) on " + modeName);
            Start(mode, minutes, BreakFromRatio(minutes), "", true);
            Settings.isStartPomodoroWindowOpen = false;
            return "";
        }

        internal static void Start(FocusMode mode, int minutes, string breakMinutes, string rawTasks, bool personalSession)
        {
            if (mode == null) { LogWriter.LogWrite("FocusSessionStarter.Start - no focus mode"); return; }

            //used when user starts another focus block
            Settings.sRunningFocusModeMinutes = minutes.ToString();
            Settings.sMinutesBreakPomodoro = breakMinutes;
            Settings.sMinutesSuperPomodoro = minutes.ToString();
            Settings.SuperPomodoroFocusedName = mode.name;

            var clock = System.Diagnostics.Stopwatch.StartNew();
            Action<string> step = name => LogWriter.LogWrite("[timing] FocusSessionStarter.Start: " + name + " at " + clock.ElapsedMilliseconds + " ms");
            try
            {
                Sounds.PlayFocusModeStartSound();
                step("start sound");
                mode.isOn = true;
                Settings.sMostRecentFocusmodeDoneID = mode.id;
                Settings.sRawTasks = rawTasks ?? "";

                Settings.isFocusModeRunning = true;
                // Reset pause state when a new focus mode starts
                Settings.isFocusModePaused = false;
                Settings.nPausedFocusRemainingSeconds = 0;

                TheApp.ShowFocusProgressWindow();
                step("progress window");
                if (TheApp.wndBrainDump != null && TheApp.wndBrainDump.IsVisible) TheApp.wndBrainDump.ShowBrainDumpWindowAndStartTimer();

                DateTime dTill = DateTime.Now;
                Settings.sRunningFocusModeStartTime = dTill.ToString("yyyy-MM-dd HH:mm:ss");
                Settings.focusSessionStartTime = DateTime.Now;
                Settings.CurrentFocusSessionDuration = 0;

                dTill = dTill.AddMinutes(minutes);
                string sTill = Settings.ConfigFileStrings["menuInfo"]["untilTimeText"].ToString();
                sTill = sTill.Replace("<TIME>", dTill.ToString());
                TheApp.wndTrayMenu.OnUntill.Content = sTill;

                Settings.sRunningFocusModeID = mode.id;
                Settings.sRunningFocusModeName = mode.name;
                Settings.sRunningFocusModeEndTime = dTill.ToString();

                //Clear any old stuff for gentle blocking
                Settings.TemporaryAllowedAppsGentleBlock.Clear();
                Settings.TemporarryBlockedAppsGentleBlock.Clear();
                Settings.TemporaryAllowedURLsGentleBlock.Clear();
                Settings.TemporarryBlockedURLsGentleBlock.Clear();
                Settings.TemporaryAllowedKeywords.Clear();
                Settings.TemporaryAllowedURLsTabTitles.Clear();

                //Start the Focus Mode Timer to end after x hours / minutes
                TheApp.wndTrayMenu.StartFocusModeTimer(0, minutes);
                step("focus timer and session screen");

                Settings.bTeacherStartPersonalSession = personalSession;
                TheApp.StartFocusSessionBackend(mode);
                step("backend start sent");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("FocusSessionStarter.Start Exception - " + ex.Message);
            }
        }

        /// <summary>The block-list editor, as the old screens' "Edit block lists" link opened it.</summary>
        internal static void OpenBlockLists()
        {
            var main = TheApp.wndFocusBearMainWindow;
            main.HandleLeafSelection(LeftButtonType.Blocking);
            if (!main.IsShowing(LeftButtonType.Blocking)) return;   // the password prompt was cancelled: stay on the start screen
            var peer = new ButtonAutomationPeer(main.PageBlocks.btnBlockLists);
            (peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider)?.Invoke();
        }

        /// <summary>The Pomodoro settings tab, where the work:break ratio is edited.</summary>
        internal static void OpenBreakRatioSettings()
        {
            var main = TheApp.wndFocusBearMainWindow;
            main.HandleLeafSelection(LeftButtonType.Settings);
            if (!main.IsShowing(LeftButtonType.Settings)) return;   // the password prompt was cancelled: stay on the start screen
            main.PageSettings.NavigateToPomodoroTab();
        }

        /// <summary>Break minutes from the saved work:break ratio, as both old screens derived them.</summary>
        internal static string BreakFromRatio(int workMinutes)
        {
            if (Settings.PomodoroWorkToBreakRatio > 0 && Settings.PomodoroBreakRatio > 0)
                return Math.Max(1, (int)Math.Round((double)workMinutes * Settings.PomodoroBreakRatio / Settings.PomodoroWorkToBreakRatio)).ToString();
            return Settings.PomodoroBreakMinutes;
        }

        /// <summary>The simple screen's two modes: AI (deep work) or "just the super distracting sites" (default).</summary>
        internal static FocusMode SimpleMode(bool ai) =>
            Settings.arrFocusModes.LastOrDefault(m => ai ? m.isDeepWorkFocus : m.isDefault);
    }
}
