using System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Focus_Bear.Utils;
using Focus_Bear.Focus;
using System.Windows.Threading;
using System.Reflection;
using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Quit;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Focus_Bear.Preferences;
using Focus_Bear.Pomodoro;
using System.Threading;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Windows.Interop;

namespace Focus_Bear
{
    /// <summary>
    /// Interaction logic for TrayMenu.xaml
    /// </summary>
    public partial class TrayMenu : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        DispatcherTimer dispatcherTimerFocus = new DispatcherTimer(DispatcherPriority.Normal);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int WM_SETTINGCHANGE = 0x001A;

        bool bQuitVisible = false;
        public TrayMenu()
        {
            InitializeComponent();

            // The tray menu lives for the whole run and is the app's MainWindow. Closing it (Alt+F4 while it has
            // focus, or any WM_CLOSE) destroyed it, and the tray icon never showed a menu again until a restart
            // (found by the E2E suite). Hide it instead. During Application.Shutdown WPF ignores this Cancel, so
            // quitting still works.
            Closing += (s, e) => { e.Cancel = true; Hide(); };

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }

            InitializeSettings();
            SetTrayMenuPosition();
            ShowInTaskbar = false;
            this.Loaded += MainWindow_Loaded;

            PauseResumeFocusMenuItem.Click += (s, e) =>
            {
                ToggleFocusPauseFromUI();
            };

            StopFocusSessionMenuItem.Click += async (s, e) =>
            {
                await StopFocusSessionRealAsync();
            };

            // The accordion header colours are set in code (FindResource snapshots), so a light/dark switch
            // leaves them frozen at the old theme until the header is toggled. Re-apply them on theme change.
            App.ThemeChanged += OnThemeChanged;
        }

        // Re-evaluates the code-set colours after a theme switch. DynamicResource elements refresh themselves;
        // only the accordion headers styled in StyleAccordionHeader need re-applying for their current state.
        private void OnThemeChanged()
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    StyleAccordionHeader(ToolsSection, Tools, ToolsSubtitle, ToolsChevronPath, ToolsChevron,
                        ToolsBody.Visibility == Visibility.Visible);
                    StyleAccordionHeader(BreaksSection, BreaksTitle, BreaksSubtitle, BreaksChevronPath, BreaksChevron,
                        BreaksBody.Visibility == Visibility.Visible);
                    StyleAccordionHeader(DayPlanSection, DayPlanTitle, DayPlanSubtitle, DayPlanChevronPath, DayPlanChevron,
                        DayPlanBody.Visibility == Visibility.Visible);
                }));
            }
            catch (Exception ex) { LogWriter.LogWrite("TrayMenu OnThemeChanged ex - " + ex.Message); }
        }

        public void SetTrayMenuPosition()
        {
            // Manually alter window height and width
            this.SizeToContent = SizeToContent.Manual;

            // Automatically resize width relative to content
            this.SizeToContent = SizeToContent.Width;

            // Automatically resize height relative to content
            this.SizeToContent = SizeToContent.Height;

            // Automatically resize height and width relative to content
            this.SizeToContent = SizeToContent.WidthAndHeight;

            var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
            // Never taller than the screen: with Breaks, Day Plan and Focus Tools all open the menu ran off the
            // top of a 1080p screen, out of reach of the mouse (found by the E2E tests). The content scrolls.
            this.MaxHeight = desktopWorkingArea.Height;
            this.UpdateLayout();
            this.Left = (desktopWorkingArea.Right - this.Width);
            this.Top = Math.Max(desktopWorkingArea.Top, desktopWorkingArea.Bottom - this.ActualHeight);

        }

        private void InitializeSettings()
        {

            ListFocusModes.ItemsSource = appSettings.arrFocusModes;
            try
            {
                // Preferences and Quit now live as circular icon buttons in the header.
                btnSettingsIcon.ToolTip = appSettings.ConfigFileStrings["menuInfo"]["menuItemPreferencesTitle"].ToString();
                PomodoroMode.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemStartPomodoroTitle"].ToString();
                btnStopPomodoroModetxt.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemStopHardcorePomodoroTitle"].ToString();
                btnQuitIcon.ToolTip = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["buttonQuitText"].ToString();

                UpgradeNow.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemUpgradeNowTitle"].ToString();
                Focus.Content = appSettings.ConfigFileStrings["menuInfo"]["menuItemFocusTitle"].ToString();

                // Focus Tools accordion — row labels (header stays "Focus Tools")
                FocusMusicLabel.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemFocusMusicTitle"].ToString();
                BrainDumpLabel.Text = appSettings.GetConfigStringSafe("Notes", "menuInfo", "menuItemNotesTitle");
                ToDoPlayerLabel.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemToDoPlayerTitle"].ToString();
                // Breaks accordion — "start a break" link text
                StartBreakNowText.Text = appSettings.ConfigFileStrings["breakWarningVcInfo"]["buttonStartBreakNowText"].ToString();

                LimitedBlocking.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemBlockingDisabledTitle"].ToString();
                LimitedBlockingInfo.ToolTip = appSettings.ConfigFileStrings["menuInfo"]["addBlockingScheduleInfo"].ToString();
                LimitedBlockingEdit.ToolTip = appSettings.ConfigFileStrings["menuInfo"]["toolTipButtonEditBlockingSchedule"].ToString();

                BlockingScheduleText.Text = appSettings.ConfigFileStrings["menuInfo"]["titleBlockingSchedule"].ToString();

                // macOS-aligned tray labels (Start Focus card, Day Plan, Focus Tools, Breaks) from menuView.
                ApplyMenuViewStrings();

                if (appSettings.sTheme == "Light" && appSettings.bAutoTheme == false)
                {
                    ((App)Application.Current).ChangeTheme("Light");
                }
                else if (appSettings.sTheme == "Dark" && appSettings.bAutoTheme == false)
                {
                    ((App)Application.Current).ChangeTheme("Dark");
                }

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("TrayMenu InitializeSettings() text setting + " + ex.Message);
            }
            UpdateTrayMenu();

        }

        public void UpdateTrayMenu()
        {
            LogWriter.LogWrite("UpdateTrayMenu()");

            try
            {
                //Focus Music icon
                if (appSettings.arrFocusMusicTracks.Count > 0)
                {
                    var current = appSettings.arrFocusMusicTracks.FirstOrDefault(t => t.IsPlaying || t.IsPaused);
                    if (current != null)
                    {
                        btnPlayPauseMusic.Visibility = Visibility.Visible;
                        btnPlayPauseMusic.ToolTip = current.Name;
                        if (current.IsPlaying)
                        {
                            string imagePath = ("pack://application:,,,/Focus Bear;component/images/pause_icon.png");
                            var image = new BitmapImage();
                            image.BeginInit();
                            image.UriSource = new Uri(imagePath);
                            image.EndInit();

                            PlayPauseButtonImage.Source = image;
                            PlayPauseMusicText.Text = MenuViewString("menuPauseMusic", "Pause music");
                        }
                        else if (current.IsPaused)
                        {
                            string imagePath = ("pack://application:,,,/Focus Bear;component/images/play_icon.png");
                            var image = new BitmapImage();
                            image.BeginInit();
                            image.UriSource = new Uri(imagePath);
                            image.EndInit();

                            PlayPauseButtonImage.Source = image;
                            PlayPauseMusicText.Text = MenuViewString("menuPlayMusic", "Play music");
                        }
                    }
                    else
                    {
                        btnPlayPauseMusic.Visibility = Visibility.Collapsed;
                    }

                }

                UpdatePomodoroPauseButtonText();

                if (appSettings.arrCustomBlockingSchedules.Count == 0)
                {
                    LimitedBlockingSection.Visibility = Visibility.Visible;
                }
                else
                {
                    LimitedBlockingSection.Visibility = Visibility.Collapsed;
                }

                if (appSettings.isPurchased)
                {
                    //hide the upgrade now button
                    btnUpgradeNow.Visibility = Visibility.Collapsed;
                }
                else if (appSettings.isExpired)
                {
                    TrialInfo.Text = appSettings.ConfigFileStrings["menuInfo"]["subscriptionExpiredText"].ToString();
                }
                else
                {
                    string sTrial = appSettings.ConfigFileStrings["common"]["daysRemainingText"].ToString();

                    TrialInfo.Text = sTrial.Replace("<DAYS>", appSettings.nTrialDays.ToString()); ;
                }

                // Voice command mic — available to paid or in-trial users (mirrors the Mac menu header mic).
                btnVoiceCommand.Visibility = (appSettings.isPurchased || appSettings.nTrialDays > 0)
                    ? Visibility.Visible : Visibility.Collapsed;

                UpdateBreaksCard();

                UpdateTeacherSessionJoinCard();

                // Reset the inline focus control to collapsed each time the menu opens, and refresh the Day Plan card.
                FocusStartExpanded.Visibility = Visibility.Collapsed;
                UpdateDayPlanCard();

                if (appSettings.bIsGeekModeEnabled) //GEEK MODE ENABLED
                {
                    // Focus mode running
                    if (appSettings.isFocusModeRunning)
                    {
                        btnStartPomodoroMode.Visibility = Visibility.Collapsed;
                        btnStopPomodoroMode.Visibility = Visibility.Visible;
                        //show the stop focus session button
                        if (appSettings.bUseSuperPomodoroMode)
                        {
                            btnStopPomodoroModetxt.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemStopHardcorePomodoroTitle"].ToString();
                        }
                        else
                        {
                            btnStopPomodoroModetxt.Text = appSettings.GetFocusPauseButtonText();
                        }
                    }
                    else
                    {
                        //if focus session is not running, possible a super pomodoro break time
                        if (appSettings.bUseSuperPomodoroMode)
                        {
                            btnStopPomodoroModetxt.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemStopHardcorePomodoroTitle"].ToString();
                            btnStartPomodoroMode.Visibility = Visibility.Collapsed;
                            btnStopPomodoroMode.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            btnStartPomodoroMode.Visibility = Visibility.Visible;
                            btnStopPomodoroMode.Visibility = Visibility.Collapsed;
                        }
                    }

                    if (appSettings.isFocusModeRunning)
                    {
                        btnStartPomodoroMode.Visibility = Visibility.Collapsed;
                        btnStopPomodoroMode.Visibility = Visibility.Visible;
                        //show the stop focus session button
                        if (appSettings.bUseSuperPomodoroMode)
                        {
                            btnStopPomodoroModetxt.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemStopHardcorePomodoroTitle"].ToString();
                        }
                        else
                        {
                            btnStopPomodoroModetxt.Text = appSettings.GetFocusPauseButtonText();
                        }
                    }
                    else
                    {
                        //if focus session is not running, possible a super pomodoro break time
                        if (appSettings.bUseSuperPomodoroMode)
                        {
                            btnStopPomodoroModetxt.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemStopHardcorePomodoroTitle"].ToString();
                            btnStartPomodoroMode.Visibility = Visibility.Collapsed;
                            btnStopPomodoroMode.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            btnStartPomodoroMode.Visibility = Visibility.Visible;
                            btnStopPomodoroMode.Visibility = Visibility.Collapsed;
                        }
                    }
                    if (((appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS ||
                       appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS ||
                       appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY) &&
                       !appSettings.bHabitsPostponedState &&
                       !appSettings.bFocusSummaryPostponedState))
                    {
                        btnStartPomodoroMode.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        if (!appSettings.isFocusModeRunning && !appSettings.bUseSuperPomodoroMode)
                        {
                            btnStartPomodoroMode.Visibility = Visibility.Visible; //both start and stop
                        }
                    }
                    /**if (appSettings.isFocusModeRunning ||
                        ((appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS ||
                        appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS ||
                        appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY) &&
                        !appSettings.bHabitsPostponedState))
                    {
                        btnStartPomodoroMode.Visibility = Visibility.Collapsed;
                        btnStopPomodoroMode.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        if (appSettings.bUseSuperPomodoroMode)
                        {
                            btnStopPomodoroMode.Visibility = Visibility.Visible;
                            btnStartPomodoroMode.Visibility = Visibility.Collapsed;
                            Focus.Content = appSettings.ConfigFileStrings["menuInfo"]["menuItemHardcorePomodoroModeTitle"].ToString();
                        }
                        else
                        {
                            btnStopPomodoroMode.Visibility = Visibility.Collapsed;
                            btnStartPomodoroMode.Visibility = Visibility.Visible;
                            PomodoroMode.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemStartPomodoroTitle"].ToString();

                            Focus.Content = appSettings.ConfigFileStrings["menuInfo"]["menuItemFocusTitle"].ToString();
                        }
                    }
                    if (appSettings.bUseSuperPomodoroMode) //we need to show only the running focus mode in the list
                    {
                        for (int i = 0; i < appSettings.arrFocusModes.Count(); i++)
                        {
                            if (appSettings.arrFocusModes[i].name == appSettings.SuperPomodoroFocusedName)
                            {
                                appSettings.arrFocusModes[i].isShowAbleInTrayMenuList = true; //only show this one in the list
                            }
                            else
                            {
                                appSettings.arrFocusModes[i].isShowAbleInTrayMenuList = false;
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < appSettings.arrFocusModes.Count(); i++)
                        {
                            appSettings.arrFocusModes[i].isShowAbleInTrayMenuList = true;
                        }
                    }**/

                    btnBrainDump.Visibility = Visibility.Visible;

                    //Focus Alignment score
                    //The score shows for a running focus mode, or for a schedule whose block list has AI
                    //on. AiBlocking.IsEnabled() answers the second half via the ladder in FocusBear.Core,
                    //and also covers the teacher session, which is why no override follows: during a class
                    //session isFocusModeRunning is always true, so the score has to be gated on the
                    //teacher's setting alone.
                    bool bShowAlignmentScore = TeacherSessionManager.Instance.IsTeacherSessionActive()
                        ? AiBlocking.IsEnabled()
                        : appSettings.isFocusModeRunning
                          || (appSettings.IsBlockingScheduleActiveAndNotPaused(out _) && AiBlocking.IsEnabled());
                    if (bShowAlignmentScore)
                    {
                        AlignmentScoreText.Visibility = Visibility.Visible;
                        double CurrentFocusAlignmentScore = FocusAlignmentScoreManager.CalculateFocusAlignmentScore();
                        string sScore = appSettings.ConfigFileStrings["menuInfo"]["focusAlignmentScoreText"].ToString();
                        sScore = sScore.Replace("<SCORE>", CurrentFocusAlignmentScore.ToString());
                        AlignmentScoreText.Text = sScore + "%";
                    }
                    else
                        AlignmentScoreText.Visibility = Visibility.Collapsed;
                }
                else //Simple mode
                {
                    // Brain Dump is shown in the Focus Tools accordion for all modes.
                    btnBrainDump.Visibility = Visibility.Visible;

                    if (appSettings.isFocusModeRunning)
                    {
                        btnStartPomodoroMode.Visibility = Visibility.Collapsed;
                        btnStopPomodoroMode.Visibility = Visibility.Visible;
                        if (appSettings.bUseSuperPomodoroMode)
                        {
                            btnStopPomodoroModetxt.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemStopHardcorePomodoroTitle"].ToString();
                        }
                        else
                        {
                            // show the stop focus session button
                            btnStopPomodoroModetxt.Text = appSettings.GetFocusPauseButtonText();
                        }
                    }
                    else
                    {
                        if (appSettings.bUseSuperPomodoroMode)
                        {
                            btnStopPomodoroModetxt.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemStopHardcorePomodoroTitle"].ToString();
                            btnStartPomodoroMode.Visibility = Visibility.Collapsed;
                            btnStopPomodoroMode.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            btnStartPomodoroMode.Visibility = Visibility.Visible;
                            btnStopPomodoroMode.Visibility = Visibility.Collapsed;
                        }
                    }

                    try
                    {
                        // Update main button icon to match Pause/Resume state 
                        if (StopPomodoroModeMainIcon != null &&
                            appSettings.isFocusModeRunning &&
                            !appSettings.bUseSuperPomodoroMode)
                        {
                            bool isPaused = appSettings.isFocusModePaused;

                            string iconPath = isPaused
                                ? "pack://application:,,,/Focus Bear;component/images/play_circle.png"
                                : "pack://application:,,,/Focus Bear;component/images/stop_circle.png";

                            StopPomodoroModeMainIcon.Source =
                                new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath));
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("UpdateTrayMenu main icon swap ex = " + ex.Message);
                    }

                    if (appSettings.isFocusModeRunning && !appSettings.isFocusModePaused)
                    {
                        btnToDoPlayer.ToolTip = appSettings.ConfigFileStrings["menuInfo"]["menuItemToDoPlayerTitle"].ToString();
                    }
                    else
                    {
                        btnToDoPlayer.ToolTip = appSettings.ConfigFileStrings["menuInfo"]["menuItemToDoListTitle"].ToString();
                    }
                }

                if (appSettings.TryGetActiveSchedule(out var scheduleForTray))
                {
                    BlockingScheduleSection.Visibility = Visibility.Visible;
                    BlockingScheduleName.Text = scheduleForTray.name;
                    BlockingScheduleTime.Text = scheduleForTray.BlockingScheduleDaysTime;
                    if (scheduleForTray.isPaused)
                    {
                        BlockingScheduleState.Text = appSettings.ConfigFileStrings["menuInfo"]["statePaused"].ToString(); //"Paused"; 
                    }
                    else
                    {
                        BlockingScheduleState.Text = appSettings.ConfigFileStrings["menuInfo"]["stateActive"].ToString(); //"Active";
                    }
                }
                else
                {
                    BlockingScheduleSection.Visibility = Visibility.Collapsed;
                }

                // Past the cutoff (shut-off) time it's time to wind down: don't offer to start a focus session
                // (unless the user has aborted shut-off to keep working). A running session's Stop button stays.
                if (appSettings.DidCutOffTimePassed() && !ShutOffAbortedByUser())
                {
                    btnStartPomodoroMode.Visibility = Visibility.Collapsed;
                    FocusStartExpanded.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("UpdateTrayMenu() + " + ex.Message);
            }

            if (appSettings.bMenuOpeningFirstTime)
            {
                //when the user is opening menu for the first time we need to highlight the start piomodoro button and show a tooltip
                appSettings.bMenuOpeningFirstTime = false;
                if (btnStartPomodoroMode.Visibility == Visibility.Visible)   // don't flash a hidden button
                    FlashStartPomodoroButton();
            }
        }

        // True when the user has aborted (unlocked) shut-off to keep working, and that unlock is still valid —
        // so the cutoff wind-down (e.g. hiding "Start Focus Session") shouldn't apply.
        private bool ShutOffAbortedByUser()
        {
            try
            {
                DateTime now = DateTime.Now;
                if (RegistryManager.GetShutOffAbortedTillDate() && now < appSettings.ShutOFFAbortedTillDate) return true;
                if (RegistryManager.GetShutOffAbortedFor30MinsTillDate() && now < appSettings.ShutOFFAbortedFor30MinsTillDate) return true;
            }
            catch (Exception ex) { LogWriter.LogWrite("ShutOffAbortedByUser ex - " + ex.Message); }
            return false;
        }

        private void btnStopPomodoroMode_OpenContextMenu(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateFocusSessionContextMenuItems();

                if (btnStopPomodoroMode?.ContextMenu != null)
                {
                    btnStopPomodoroMode.ContextMenu.PlacementTarget = btnStopPomodoroMode;
                    btnStopPomodoroMode.ContextMenu.IsOpen = true;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnStopPomodoroMode_OpenContextMenu ex = " + ex.Message);
            }
        }

        private void UpdateFocusSessionContextMenuItems()
        {
            if (PauseResumeFocusMenuItem == null || StopFocusSessionMenuItem == null)
                return;

            try
            {
                // Context menu item titles
                string pauseTitle = appSettings.ConfigFileStrings["menuInfo"]["menuItemPauseFocusTitle"].ToString();
                string resumeTitle = appSettings.ConfigFileStrings["menuInfo"]["menuItemResumeFocusTitle"].ToString();
                string stopTitle = appSettings.ConfigFileStrings["menuInfo"]["menuItemStopPomodoroTitle"].ToString();

                bool isPaused = appSettings.isFocusModePaused;

                PauseResumeFocusMenuItem.Header = isPaused ? resumeTitle : pauseTitle;
                StopFocusSessionMenuItem.Header = stopTitle;

                // Update the menu icon to match Pause/Resume state
                if (PauseResumeFocusMenuItem.Icon is Image iconImage)
                {
                    string iconPath = isPaused
                        ? "pack://application:,,,/Focus Bear;component/images/play_icon.png"
                        : "pack://application:,,,/Focus Bear;component/images/pause_icon.png";

                    iconImage.Source = new BitmapImage(new Uri(iconPath));
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("UpdateFocusSessionContextMenuItems ex = " + ex.Message);
            }
        }

        private async void FlashStartPomodoroButton()
        {
            btnStartPomodoroMode.ToolTip = appSettings.ConfigFileStrings["menuInfo"]["toolTipPomodoroSessionText"].ToString();
            var toolTip = new ToolTip();

            if (btnStartPomodoroMode.ToolTip != null)
            {
                if (btnStartPomodoroMode.ToolTip is ToolTip)
                {
                    var castToolTip = (ToolTip)btnStartPomodoroMode.ToolTip;
                    castToolTip.IsOpen = true;
                }
                else
                {
                    toolTip.Content = btnStartPomodoroMode.ToolTip;
                    toolTip.StaysOpen = false;
                    toolTip.IsOpen = true;
                }
            }

            for (int i = 1; i < 8; i++)
            {
                if (i % 2 == 0)
                {

                    btnStartPomodoroMode.IsEnabled = false;

                    await Task.Delay(500);
                }
                else
                {

                    btnStartPomodoroMode.IsEnabled = true;

                    await Task.Delay(500);
                }
            }

            btnStartPomodoroMode.IsEnabled = true;
            btnStartPomodoroMode.ToolTip = "";
            btnStartPomodoroMode.ToolTip = null;
            //new Thread(new ThreadStart(new Action(async () =>
            //{
            //    CommandManager.InvalidateRequerySuggested();
            //    Dispatcher.Invoke(() =>
            //    {
            //        btnStartPomodoroMode.Background = System.Windows.Media.Brushes.Yellow;
            //    });

            //    for (int i = 1; i < 8; i++)
            //    {
            //        if (i % 2 == 0)
            //        {
            //            Dispatcher.Invoke(() =>
            //            {
            //                btnStartPomodoroMode.Background = System.Windows.Media.Brushes.Yellow;
            //            });
            //            Task.Delay(500);
            //        }
            //        else
            //        {
            //            Dispatcher.Invoke(() =>
            //            {
            //                btnStartPomodoroMode.Background = System.Windows.Media.Brushes.Transparent;
            //            });
            //            Task.Delay(500);
            //        }
            //    }

            //    CommandManager.InvalidateRequerySuggested();
            //}))).Start();
        }

        private void OfficeModeButton_Click(object sender, RoutedEventArgs e)
        {            
           
        }

        private void btnQuit_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnQuit_Click()");
            if(bQuitVisible)
            {
                return;
            }
            try
            {
                //A running focus mode is not finished here on purpose, it stays in progress on the backend and is resumed on next app start
                //(this also covers teacher-controlled class sessions: they must never be finished on quit)
                QuitWindow aa = new QuitWindow();
                bQuitVisible = true;
                aa.ShowDialog();
                bQuitVisible = false;
            }
            catch (Exception ex)
            {
                bQuitVisible = false;
                LogWriter.LogWrite("btnQuit_Click - " + ex.Message);
   
            }
        }

        // Resumes postponed habits / focus summary. This used to be the tray "Resume Habits"
        // button; the button was removed from the redesigned menu, but the action is still
        // triggered from the habits-postponed views and the voice-command flow, so it lives here.
        public void ResumeHabitsFromTray()
        {
            LogWriter.LogWrite("ResumeHabitsFromTray()");
            Hide();

            //Teacher Mode - resuming the routine early would tear down the locked class session's screen
            //takeover. Block it while a teacher session is active; the routine resumes on its own once the
            //session ends. (Ported from the old btnResumeHabits_Click guard.)
            if (TeacherSessionManager.Instance.IsTeacherSessionActive())
            {
                LogWriter.LogWrite("ResumeHabitsFromTray blocked - teacher class session is active");
                appSettings.ShowBaloonTip(
                    TeacherSessionManager.Instance.GetLocalizedString("resumeHabitsBlockedBody", "You can't resume your habits during a class focus session. They'll resume automatically when your teacher ends the session."),
                    TeacherSessionManager.Instance.GetLocalizedString("resumeHabitsBlockedTitle", "Class focus session in progress"));
                return;
            }

            if (appSettings.bFocusSummaryPostponedState)
            {
                ((App)(Application.Current)).wndFocusBearMainWindow.PageFocusSummary.ResumeFocusSummary();
            }
            else
            {
                ((App)(Application.Current)).ResumeAfterPostponeFinished();
            }
        }

        // Show the "A class focus session is running / Join" card only when the student declined the join
        // prompt for a still-running class session (so there's a way back in). Hidden once joined/ended.
        private void UpdateTeacherSessionJoinCard()
        {
            try
            {
                if (TeacherSessionManager.Instance.HasJoinableSession())
                {
                    TeacherSessionJoinTitle.Text = TeacherSessionManager.Instance.GetLocalizedString(
                        "classSessionRunningTitle", "A class focus session is running");
                    btnJoinTeacherSessionText.Text = TeacherSessionManager.Instance.GetLocalizedString(
                        "buttonJoin", "Join session");

                    string sName = TeacherSessionManager.Instance.GetJoinableSessionName();
                    if (!string.IsNullOrEmpty(sName))
                    {
                        TeacherSessionJoinName.Text = sName;
                        TeacherSessionJoinName.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        TeacherSessionJoinName.Visibility = Visibility.Collapsed;
                    }

                    TeacherSessionJoinSection.Visibility = Visibility.Visible;
                }
                else
                {
                    TeacherSessionJoinSection.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("UpdateTeacherSessionJoinCard ex - " + ex.Message);
            }
        }

        private void btnJoinTeacherSession_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnJoinTeacherSession_Click()");
            Hide();
            TeacherSessionManager.Instance.JoinDeclinedSession();
        }

        // Reactivates the app after deactivation and restarts it. This used to be the tray
        // "Reactivate" button; the button was removed but the action is still triggered from the
        // no-habits-running screen.
        public void ReactivateFromTray()
        {
            LogWriter.LogWrite("ReactivateFromTray() Tray Menu");
            RegistryManager.RemoveAppDeactivatedTillDate();
            Hide();
            try
            {
                LogWriter.LogWrite("Restart app now for Reactivation to take Effect!");
                appSettings.bRestartAppNeeded = true;
                Application.Current.Shutdown(0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ReactivateFromTray app restart= " + ex.Message);
            }
        }

        private void btnUpgradeNow_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnUpgradeNow_Click() Tray Menu");

            Hide();
            ((App)(Application.Current)).ShowExpiredNag(); //The old upgrade now nag
            
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnSettings_Click() Tray Menu");

            Hide();

            appSettings.CapturePostHog("open-preferences");

            ((App)(Application.Current)).ShowPreferencesWindow();
            
        }

        private void btnStartPomodoroMode_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnStartPomodoroMode_Click Tray Menu");

            if (appSettings.isFocusModeRunning && !appSettings.bIsGeekModeEnabled) //simple pomodoro running
            {
                LogWriter.LogWrite("stop the Simple pomodoro");
                //means default or deep focus is running, stop it
                var listItems = appSettings.arrFocusModes.Where(i => i.id.ToLower() == appSettings.sRunningFocusModeID.ToLower());
                if (listItems.Count() != 0)
                {
                    ListFocusModes.SelectedItem = listItems.First(); //to stop the focus mode
                }
                return;
            }

            //if (appSettings.isExpired)
            //{
            //    Hide();
            //    ((App)(Application.Current)).ShowExpiredNag();
            //    return;
            //}

            // Reveal the inline [−] ▶ Start · NN min [+] control (or toggle it back off). The centre
            // button starts the session; − / + adjust the duration.
            if (FocusStartExpanded.Visibility == Visibility.Visible)
            {
                FocusStartExpanded.Visibility = Visibility.Collapsed;
                btnStartPomodoroMode.Visibility = Visibility.Visible;
            }
            else
            {
                SetAccordionState(false, false, false); // only one card open at a time — close any open accordion
                int mins;
                if (!int.TryParse(appSettings.PomodoroMinutes, out mins) || mins < 1) mins = 25;
                _focusDurationMin = mins;
                UpdateFocusStartCenterText();
                PopulateFocusModes();
                btnStartPomodoroMode.Visibility = Visibility.Collapsed;
                FocusStartExpanded.Visibility = Visibility.Visible;
            }
            SetTrayMenuPosition();
        }

        private int _focusDurationMin = 25;

        private void UpdateFocusStartCenterText()
        {
            // menuFocusMinutes is a "%d MIN" template (macOS-aligned); substitute the chosen duration.
            FocusStartCenterText.Text = MenuViewString("menuFocusMinutes", "%d MIN").Replace("%d", _focusDurationMin.ToString());
        }

        // Fill the "Select Focus Mode" dropdown, preselecting the last-used / default mode.
        private void PopulateFocusModes()
        {
            try
            {
                cmbFocusModes.ItemsSource = appSettings.arrFocusModes;
                FocusMode preferred = null;
                if (!string.IsNullOrEmpty(appSettings.sMostRecentFocusmodeDoneID))
                    preferred = appSettings.arrFocusModes.FirstOrDefault(m => m.id == appSettings.sMostRecentFocusmodeDoneID);
                if (preferred == null) preferred = appSettings.arrFocusModes.FirstOrDefault(m => m.isDefault);
                if (preferred == null) preferred = appSettings.arrFocusModes.FirstOrDefault();
                if (preferred != null) cmbFocusModes.SelectedItem = preferred;
            }
            catch (Exception ex) { LogWriter.LogWrite("PopulateFocusModes ex - " + ex.Message); }
        }

        // Centre of the ± control → start the selected focus mode for the chosen duration.
        private void btnDoStartFocus_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var mode = (cmbFocusModes.SelectedItem as FocusMode)
                           ?? appSettings.arrFocusModes.FirstOrDefault(m => m.isDefault)
                           ?? appSettings.arrFocusModes.FirstOrDefault();
                if (mode == null) { LogWriter.LogWrite("btnDoStartFocus_Click - no focus modes"); return; }

                // Carry the typed goal into the session (same field the voice/other start paths use).
                string goal = txtFocusGoal.Text?.Trim();
                if (!string.IsNullOrEmpty(goal)) appSettings.sRunningFocusModeIntention = goal;

                FocusStartExpanded.Visibility = Visibility.Collapsed;
                Hide();
                appSettings.CapturePostHog("tray-start-focus-inline");
                ((App)(Application.Current)).StartWorkFocusMode(mode.id, _focusDurationMin * 60);
            }
            catch (Exception ex) { LogWriter.LogWrite("btnDoStartFocus_Click ex - " + ex.Message); }
        }

        // Tools/wrench (top-right of the focus card) → open the full focus screen in the main window.
        private void btnFocusTools_Click(object sender, RoutedEventArgs e)
        {
            FocusStartExpanded.Visibility = Visibility.Collapsed;
            btnStartPomodoroMode.Visibility = Visibility.Visible;
            Hide();
            if (appSettings.bIsGeekModeEnabled)
                ((App)(Application.Current)).ShowPomodoroWindow();
            else
                ((App)(Application.Current)).ShowSimplePomodoroWindow();
        }

        private void btnFocusDurUp_Click(object sender, RoutedEventArgs e)
        {
            _focusDurationMin = Math.Min(240, _focusDurationMin + 5);
            UpdateFocusStartCenterText();
        }

        private void btnFocusDurDown_Click(object sender, RoutedEventArgs e)
        {
            _focusDurationMin = Math.Max(5, _focusDurationMin - 5);
            UpdateFocusStartCenterText();
        }

        // Day Plan card — open the full Day Plan tab (the "Day Plan" nav leaf maps to LeftButtonType.Habits).
        private void btnDayPlan_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            try
            {
                appSettings.CapturePostHog("tray-open-day-plan");
                ((App)(Application.Current)).BringMainWindowToFront();
                ((App)(Application.Current)).wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.Habits);
            }
            catch (Exception ex) { LogWriter.LogWrite("btnDayPlan_Click ex - " + ex.Message); }
        }

        // Day Plan card subtitle: show the next actionable habit for today, else a generic label.
        private void UpdateDayPlanCard()
        {
            try
            {
                var blocks = Focus_Bear.Habits.DayPlan.DayPlanDataSource.BuildBlocks(DateTime.Now);
                var habitBlocks = blocks?.Where(b => b.HabitIndex.HasValue).ToList();
                // Only an ACTIONABLE habit (a routine that's live now) can be started from here. A past/preview
                // habit (e.g. tomorrow's first morning habit shown before the routine's time) is not startable —
                // don't fall back to it, or the Start button would do nothing when clicked.
                var current = blocks?.FirstOrDefault(b => b.IsCurrent && b.IsActionable)
                              ?? blocks?.FirstOrDefault(b => b.IsActionable && b.HabitIndex.HasValue);

                // Is a habit activity in progress right now (running OR paused on-screen)? A *parked* habit
                // (backed out to the Day Plan, remaining time saved) is NOT "in progress" — it should still
                // offer Start (which resumes it). Parking saves to dayPlanHabitRemainingSeconds but leaves the
                // ongoing view non-null, so distinguish the two by whether the current habit is parked.
                bool habitInProgress = false;
                try
                {
                    var ongoing = ((App)(Application.Current)).wndFocusBearMainWindow?.PageHabits?.PageHabitActivityOnGoing;
                    var arr = (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                        ? appSettings.arrAllEveningHabits : appSettings.arrAllMorningHabits;
                    string currentHabitId = (arr != null && appSettings.nHabitIndex >= 0 && appSettings.nHabitIndex < arr.Count)
                        ? arr[appSettings.nHabitIndex].id : null;
                    bool currentParked = !string.IsNullOrEmpty(currentHabitId)
                        && appSettings.dayPlanHabitRemainingSeconds != null
                        && appSettings.dayPlanHabitRemainingSeconds.ContainsKey(currentHabitId);
                    habitInProgress = ongoing != null && !currentParked;
                }
                catch { }

                // A postponed routine (incl. while a focus session runs) must still offer Start — clicking it
                // resumes the habits and starts the listed one — so never treat "postponed" as in-progress.
                if (appSettings.bHabitsPostponedState || appSettings.bFocusSummaryPostponedState)
                    habitInProgress = false;

                string currentTitle = (current != null && !string.IsNullOrWhiteSpace(current.Title)) ? current.Title.Trim() : null;

                if (habitInProgress && current != null)
                {
                    // A habit is running: header shows the running habit; the body shows the NEXT one up, and
                    // there's no Start button until it finishes (can't start another mid-habit / restart it).
                    DayPlanSubtitle.Text = currentTitle;
                    int ci = habitBlocks != null ? habitBlocks.FindIndex(b => ReferenceEquals(b, current)) : -1;
                    string nextTitle = (habitBlocks != null && ci >= 0 && ci + 1 < habitBlocks.Count)
                        ? habitBlocks[ci + 1].Title?.Trim() : null;
                    DayPlanNextHabit.Text = nextTitle ?? MenuViewString("menuLastHabit", "This is the last habit");
                    btnDayPlanStart.Visibility = Visibility.Collapsed;
                }
                else if (currentTitle != null)
                {
                    // A startable habit is up next → show it with the Start button.
                    DayPlanSubtitle.Text = currentTitle;
                    DayPlanNextHabit.Text = currentTitle;
                    btnDayPlanStart.Visibility = Visibility.Visible;
                }
                else
                {
                    // Nothing to start right now — the orange header shows when the next routine begins, and the
                    // NEXT HABIT line just shows that routine's first habit name (if any). No Start button, since
                    // it can't be started before its time.
                    string upcomingFirstHabit;
                    string upcoming = NextRoutineStartText(out upcomingFirstHabit);
                    DayPlanSubtitle.Text = upcoming ?? MenuViewString("menuDayPlanSubtitle", "View your day");
                    DayPlanNextHabit.Text = upcomingFirstHabit ?? MenuViewString("menuNothingScheduled", "Nothing scheduled right now");
                    btnDayPlanStart.Visibility = Visibility.Collapsed;
                }

                // Tasks for today (incomplete), a few of them.
                var tasks = Focus_Bear.Habits.DayPlan.DayPlanTaskStore.Shared.Tasks(DateTime.Now);
                var pending = tasks?.Where(t => !t.Done).Take(4).ToList();
                DayPlanTasksList.ItemsSource = pending;
                DayPlanTasksSection.Visibility = (pending != null && pending.Count > 0) ? Visibility.Visible : Visibility.Collapsed;

                // "Habits postponed until <time>" (mirrors macOS) — only while a routine is postponed and the
                // stored end time is still ahead; the short-time format follows the system locale.
                if ((appSettings.bHabitsPostponedState || appSettings.bFocusSummaryPostponedState)
                    && appSettings.HabitsPostponeEndTime > DateTime.Now)
                {
                    // menuHabitsPostponedUntil is a "Habits postponed until %@" template (macOS-aligned);
                    // %@ is the postpone end time in the system's short-time format.
                    DayPlanPostponedText.Text = MenuViewString("menuHabitsPostponedUntil", "Habits postponed until %@")
                        .Replace("%@", appSettings.HabitsPostponeEndTime.ToString("t"));
                    DayPlanPostponedRow.Visibility = Visibility.Visible;
                }
                else
                {
                    DayPlanPostponedRow.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("UpdateDayPlanCard ex - " + ex.Message);
                DayPlanSubtitle.Text = "View your day";
                DayPlanNextHabit.Text = "No habits scheduled right now";
                btnDayPlanStart.Visibility = Visibility.Collapsed;
            }
        }

        // When no habit is startable now, describe the next routine's start (e.g. "🌅 Morning routine starts
        // at 7:00 AM") for the Day Plan card — the evening routine if it's still ahead today, otherwise the
        // next morning routine (today if its time hasn't passed, else tomorrow). Null if neither is scheduled.
        private string NextRoutineStartText(out string firstHabitName)
        {
            firstHabitName = null;
            try
            {
                DateTime now = DateTime.Now;
                string startsAt = MenuViewString("dayPlanStartsAt", "starts at");

                // Evening routine still ahead today?
                DateTime? eveningStart = ParseTodayTime(appSettings.sDayShutOffTime, now);
                var eveningToday = Focus_Bear.Habits.DayPlan.RoutineHabitDays.ScheduledOn(appSettings.arrGLOBALAllEveningHabits, now);
                if (eveningStart.HasValue && eveningStart.Value > now && eveningToday.Count > 0)
                {
                    firstHabitName = eveningToday[0].name;
                    return Focus_Bear.Habits.DayPlan.DayPlanText.EveningRoutine + " " + startsAt + " " + eveningStart.Value.ToString("t");
                }

                // Otherwise the next morning routine (today if still ahead, else tomorrow).
                DateTime? morningStart = ParseTodayTime(appSettings.sDayStartTime, now);
                if (morningStart.HasValue)
                {
                    DateTime m = morningStart.Value;
                    if (m <= now) m = m.AddDays(1);
                    var morning = Focus_Bear.Habits.DayPlan.RoutineHabitDays.ScheduledOn(appSettings.arrGLOBALAllMorningHabits, m);
                    if (morning.Count > 0)
                    {
                        firstHabitName = morning[0].name;
                        return Focus_Bear.Habits.DayPlan.DayPlanText.MorningRoutine + " " + startsAt + " " + m.ToString("t");
                    }
                }
            }
            catch (Exception ex) { LogWriter.LogWrite("NextRoutineStartText ex - " + ex.Message); }
            return null;
        }

        // A DateTime at "HH:mm"(":ss") on today's date, or null if the time string is empty/unparseable.
        private DateTime? ParseTodayTime(string time, DateTime now)
        {
            try
            {
                if (string.IsNullOrEmpty(time)) return null;
                TimeSpan ts = TimeSpan.Parse(time);
                return new DateTime(now.Year, now.Month, now.Day, ts.Hours, ts.Minutes, 0);
            }
            catch { return null; }
        }

        // Start the next habit — same flow as clicking Play in the Day Plan view (resume if postponed, etc.).
        private void btnDayPlanStart_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            try
            {
                appSettings.CapturePostHog("tray-day-plan-start-habit");
                var app = (App)(Application.Current);
                app.BringMainWindowToFront();
                app.wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.Habits);
                // Let the Day Plan page render/create first, then play the current habit.
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { app.wndFocusBearMainWindow.PageHabits.PageDayPlan?.PlayCurrentHabit(); }
                    catch (Exception ex2) { LogWriter.LogWrite("btnDayPlanStart play ex - " + ex2.Message); }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex) { LogWriter.LogWrite("btnDayPlanStart_Click ex - " + ex.Message); }
        }

        // Tapping a task in the Day Plan card marks it done (syncs to the to-do) and drops it off the list.
        private void DayPlanTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var id = (sender as System.Windows.Controls.Button)?.Tag as string;
                if (string.IsNullOrEmpty(id)) return;
                Focus_Bear.Habits.DayPlan.DayPlanTaskStore.Shared.SetDone(id, true, DateTime.Now);
                UpdateDayPlanCard();       // re-filter (only incomplete tasks are shown)
                SetTrayMenuPosition();
            }
            catch (Exception ex) { LogWriter.LogWrite("DayPlanTask_Click ex - " + ex.Message); }
        }

        public async Task StopFocusSessionRealAsync()
        {
            
            //stop the current running focus session

            LogWriter.LogWrite("btnStopPomodoroMode_Click - button Stop FOcus Session Clicked");

            //Teacher Mode early-stop lock - a teacher controlled class session cannot be stopped early,
            //route to the unlock-request UX instead of the password path. Only class-session-ended or
            //the scheduled finish time can end it.
            if (TeacherSessionManager.Instance.IsTeacherSessionActive())
            {
                Hide();
                if (TeacherSessionManager.Instance.CanTeacherSessionFinishNow())
                {
                    TeacherSessionManager.Instance.TeacherSessionFinished("stopped after the scheduled finish time");
                }
                else
                {
                    LogWriter.LogWrite("StopFocusSessionRealAsync blocked - teacher class session is active");
                    TeacherSessionManager.Instance.ShowTeacherSessionLockedAlert();
                }
                return;
            }

            //A buddy-approved postpone or Late No More meeting session over a postponed routine can't just
            //be stopped: Stop hands the user back to that routine (no completion screen, no password), as on the Mac.
            if (((App)Application.Current).HandlePostponeSessionControl(FocusBear.Core.Postpone.FocusSessionControl.Stop))
            {
                return;
            }

            try
            {
                bool needsPassword = appSettings.bUseSuperPomodoroMode &&
                       appSettings.isPasswordSet &&
                       appSettings.bPasswordRequireInStopPomodoro;
                if (needsPassword)
                {
                    var pwdWnd = new AskPassword { Owner = this };
                    bool? ok = pwdWnd.ShowDialog();
                    if (!ok.GetValueOrDefault())
                    {
                        return; // no password entered, do not stop
                    }
                }

                // Teacher Mode (issue #1343): if this device started a class focus session, end it early
                // on the backend now that the teacher is stopping focus before the scheduled finish.
                if (!string.IsNullOrWhiteSpace(appSettings.sRunningClassSessionId))
                {
                    _ = new FocusBearAPIClient().EndClassSession(appSettings.sRunningClassSessionId);
                }

                Hide();
                //Check is any other FOcus modes is running, if yes, give an alert to stop that mode
                //If not we will start the focus mode
                //First check if it is already added
                   
                {
                    try
                    {
                        await((App)(Application.Current)).SyncTodoTasksFromPlayer(); // sync todo tasks

                        var listItems = appSettings.arrFocusModes.Where(i => i.isOn == true);
                        //Check if its the same focusmode that is running, then let's stop it.
                        if (listItems.Count() != 0)
                        {
                            FocusMode CurrentRunningFocusMode = listItems.First<FocusMode>();

                            OnUntill.Content = "";
                            CurrentRunningFocusMode.isOn = false;

                            if (!appSettings.bIsGeekModeEnabled)//Simple mode
                            {
                                appSettings.bAiCheckUrlEnabled = appSettings.bAiCheckUrlEnabledLastValueBeforeSimplePomodoro; //resetting the AI value
                            }

                            //Turn off super pomodoro too as user have manually stopped
                            appSettings.bUseSuperPomodoroMode = false;

                            ((App)(Application.Current)).FinishFocusModeCommonTasks();

                            //Show the Focusmode end dialog
                            ((App)(Application.Current)).ShowFocusEndWindow();

                        }

                        if (appSettings.bUseSuperPomodoroMode)
                        {
                            LogWriter.LogWrite("Stop the super pomodoro mode and return");
                            appSettings.bUseSuperPomodoroMode = false; //stop the super pomodoro mode and return
                        }

                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("btnStopPomodoroMode_Click ex = " + ex.Message);
                    }

                }
                
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnStopPomodoroMode_Click = " + ex.Message);
            }

        }

        private async void btnStopPomodoroMode_Click(object sender, RoutedEventArgs e)
        {
            // In normal focus mode, main button click opens the context menu
            if (appSettings.isFocusModeRunning && !appSettings.bUseSuperPomodoroMode)
            {
                btnStopPomodoroMode_OpenContextMenu(sender, e);
                return;
            }

            // Otherwise, perform the real stop
            await StopFocusSessionRealAsync();
        }

        public bool ToggleFocusPauseFromUI(bool bComingFromPhysicalDevice = false)
        {
            try
            {
                LogWriter.LogWrite("ToggleFocusPauseFromUI()");

                //Teacher Mode early-stop lock - pausing a teacher controlled class session is not
                //allowed either, route to the unlock-request UX instead.
                if (TeacherSessionManager.Instance.IsTeacherSessionActive())
                {
                    LogWriter.LogWrite("ToggleFocusPauseFromUI blocked - teacher class session is active");
                    Hide();
                    TeacherSessionManager.Instance.ShowTeacherSessionLockedAlert();
                    return false;
                }

                if (!appSettings.isFocusModeRunning)
                {
                    LogWriter.LogWrite("ToggleFocusPauseFromUI() - No focus mode running, returning false");
                    return false;
                }

                if (appSettings.bUseSuperPomodoroMode)
                {
                    LogWriter.LogWrite("ToggleFocusPauseFromUI() - Super Pomodoro mode active, pause not allowed, returning false");
                    return false;
                }

                //Sessions the app started for the user (an accountability-buddy postpone, a Late No More
                //meeting) can't be paused; the full focus screen comes back instead.
                if (((App)Application.Current).HandlePostponeSessionControl(FocusBear.Core.Postpone.FocusSessionControl.Pause))
                {
                    Hide();
                    return false;
                }

                if (!DateTime.TryParse(appSettings.sRunningFocusModeEndTime, out DateTime endTime))
                {
                    LogWriter.LogWrite("ToggleFocusPauseFromUI() - Could not parse sRunningFocusModeEndTime, returning false");
                    return false;
                }

                if (!appSettings.isFocusModePaused)
                {
                    // ----------- PAUSE -----------
                    TimeSpan remaining = endTime - DateTime.Now;
                    int remainingSeconds = (int)remaining.TotalSeconds;
                    if (remainingSeconds <= 0)
                    {
                        LogWriter.LogWrite("ToggleFocusPauseFromUI() - Remaining seconds <= 0, nothing to pause, returning false");
                        return false;
                    }
                    // Calculate active time till now
                    if (appSettings.focusSessionStartTime != null)
                    {
                        int activeSeconds = (int)(DateTime.Now - appSettings.focusSessionStartTime.Value).TotalSeconds;
                        appSettings.CurrentFocusSessionDuration += activeSeconds;
                    }

                    appSettings.nPausedFocusRemainingSeconds = remainingSeconds;
                    appSettings.isFocusModePaused = true;

                    dispatcherTimerFocus.Stop();

                    LogWriter.LogWrite("ToggleFocusPauseFromUI() - Paused focus mode, remainingSeconds = " + appSettings.nPausedFocusRemainingSeconds);
                }
                else
                {
                    // ----------- RESUME -----------
                    if (appSettings.nPausedFocusRemainingSeconds <= 0)
                    {
                        LogWriter.LogWrite("ToggleFocusPauseFromUI() - Resume requested but nPausedFocusRemainingSeconds <= 0, returning false");
                        return false;
                    }
                    // Start a new temp active session time
                    appSettings.focusSessionStartTime = DateTime.Now;

                    // New end time = now + what was left when we paused
                    int remainingSeconds = appSettings.nPausedFocusRemainingSeconds;
                    DateTime newEnd = DateTime.Now.AddSeconds(remainingSeconds);
                    appSettings.sRunningFocusModeEndTime = newEnd.ToString();

                    appSettings.nPausedFocusRemainingSeconds = 0;
                    appSettings.isFocusModePaused = false;

                    string sTill = appSettings.ConfigFileStrings["menuInfo"]["untilTimeText"].ToString();
                    sTill = sTill.Replace("<TIME>", newEnd.ToString());
                    OnUntill.Content = sTill;

                    dispatcherTimerFocus.Interval = TimeSpan.FromSeconds(remainingSeconds);
                    dispatcherTimerFocus.Start();

                    LogWriter.LogWrite("ToggleFocusPauseFromUI() - Resumed focus mode, new end time = " + appSettings.sRunningFocusModeEndTime);
                }

                if (!bComingFromPhysicalDevice) //Only when instructions not coming from device
                {
                    App.FocusButton.PauseFocusSession();
                }

                // Notify all UI components of the pause state change
                var app = (App)Application.Current;
                app.UpdateFocusEventsSubscription();

                UpdateTrayMenu();
                app.wndFocusProgress?.UpdatePauseControls();
                // Refresh timer display immediately on resume so the arc and label don't wait for the next tick
                if (!appSettings.isFocusModePaused)
                    app.wndFocusProgress?.UpdateUI();
                app.wndFocusBearMainWindow?.PageStopFocusMode?.UpdatePauseButtonText();
                app.NotifyToDoPlayerPauseState(appSettings.isFocusModePaused);

                // Update left menu title based on pause state
                if (appSettings.isFocusModePaused)
                {
                    ((App)Application.Current).wndFocusBearMainWindow?.UpdateLeafTitle(LeftButtonType.StartFocusSession, appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuResumeFocusSession"].ToString());
                }
                else
                {
                    string leafTitle = appSettings.bUseSuperPomodoroMode
                        ?appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuStopSuperFocusSession"].ToString()
                        :appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuStopFocusSession"].ToString();

                    ((App)Application.Current).wndFocusBearMainWindow?.UpdateLeafTitle(LeftButtonType.StartFocusSession, leafTitle);
                }

                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ToggleFocusPauseFromUI() exception = " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// The session screen's −5 / +5: moves a running (not paused) session's end by <paramref name="minutes"/>,
        /// never to less than a minute from now, and re-arms the end timer the way Resume does. A class session's
        /// schedule belongs to the teacher, so it is left alone. Returns whether the end moved.
        /// </summary>
        public bool AdjustRunningFocusSession(int minutes)
        {
            try
            {
                if (!appSettings.isFocusModeRunning || appSettings.isFocusModePaused) return false;
                if (TeacherSessionManager.Instance.IsTeacherSessionActive()) return false;
                if (!DateTime.TryParse(appSettings.sRunningFocusModeEndTime, out DateTime endTime)) return false;
                DateTime now = DateTime.Now;
                // +5 on a session covering a postponed routine can't reach past the cutoff (Mac cutoffLimitedMinutes).
                DateTime? cutoff = Focus_Bear.Habits.DayPlan.DayPlanDataSource.TimeToday(appSettings.sDayCutOffTime, now);
                DateTime? moved = FocusBear.Core.Focus.FocusSessionProgress.AdjustedEndWithinCutoff(endTime, now, minutes, cutoff,
                    appSettings.DidCutOffTimePassed(), appSettings.bPasswordRequireInPostponeEveningAfterCutOff,
                    ((App)Application.Current).SessionProtectsRoutine);
                if (moved == null)
                {
                    if (minutes > 0 && ((App)Application.Current).SessionProtectsRoutine && !string.IsNullOrEmpty(appSettings.sDayCutOffTime))
                    {
                        LogWriter.LogWrite("AdjustRunningFocusSession(" + minutes + ") - refused: the cutoff leaves no room");
                        ((App)Application.Current).ShowCutoffExtensionRefused();
                    }
                    return false;
                }
                DateTime newEnd = moved.Value;
                appSettings.sRunningFocusModeEndTime = newEnd.ToString();
                OnUntill.Content = appSettings.ConfigFileStrings["menuInfo"]["untilTimeText"].ToString().Replace("<TIME>", newEnd.ToString());
                dispatcherTimerFocus.Stop();
                dispatcherTimerFocus.Interval = newEnd - now;
                dispatcherTimerFocus.Start();
                App.FocusButton.ExtendFocusSession((int)(newEnd - now).TotalSeconds);
                ((App)Application.Current).wndFocusProgress?.UpdateUI();
                // The server (and every other device) keeps the old end otherwise, and its finish there ends this
                // session early after a +5 (the Mac's extendFocusMode / reduce both PATCH it).
                if (appSettings.ServerFocusSession.HasSessionToFinish)
                    _ = new FocusBearAPIClient().ExtendFocusMode(appSettings.sRunningFocusModeID, minutes);
                LogWriter.LogWrite("AdjustRunningFocusSession(" + minutes + ") - new end time = " + appSettings.sRunningFocusModeEndTime);
                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AdjustRunningFocusSession ex = " + ex.Message);
                return false;
            }
        }

        private void btnPauseFocusSession_Click(object sender, RoutedEventArgs e)
        {
            // Delegate to the shared pause/resume logic
            ToggleFocusPauseFromUI();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Hide();
        }

        private void ListFocusModes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //Code not used anymore as list of focus mode is not shown anymore
            return;
        }

        public void StartFocusModeTimer(int nHours, int nMinutes, int nSeconds = 0, bool bExtending = false, bool bCommandFromDevice = false)
        {
            LogWriter.LogWrite("StartFocusModeTimer() invoked");

            //Remove the previous event handler so that new can be set
            try
            {
                var eventField = dispatcherTimerFocus.GetType().GetField("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
                var eventDelegate = (Delegate)eventField.GetValue(dispatcherTimerFocus);
                if (eventDelegate != null)
                {
                    var invocatationList = eventDelegate.GetInvocationList();

                    foreach (var handler in invocatationList)
                        dispatcherTimerFocus.Tick -= ((EventHandler)handler);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StartFocusModeTimer 1" + ex.Message);
            }

            try
            {
                if (!bExtending)
                {
                    TabMetrics.Instance.FocusModeStartedResetAllData();//Reset all data.
                }

                appSettings._last5Justifications.Clear();//reset

                appSettings.sCurrentTaskPlayingInToDoPlayer = ""; //reset

                //We will need to get the list of all the tasks to show in gentle block
                FocusBearAPIClient client = new FocusBearAPIClient();
                _ = client.GetAllTasks();

                //we need to fetch all focus mode data to show in focus mode end window
                appSettings.GetTotalDeepFocusedTime();

                dispatcherTimerFocus.Tick += new EventHandler(dispatcherTimer_Focus);
           
                dispatcherTimerFocus.Interval = new TimeSpan(nHours, nMinutes, nSeconds);

                if (!bCommandFromDevice) //Start only when the command was not from device to avoid loop
                {
                    //Include the hours component too - sessions >= 60 min have nHours > 0, and sending
                    //only nMinutes would tell the device the wrong (too-short) duration.
                    App.FocusButton.StartFocusSession((nHours * 60 + nMinutes) * 60, appSettings.sRunningFocusModeIntention);
                }

                dispatcherTimerFocus.Start();
                CommandManager.InvalidateRequerySuggested();
                ((App)Application.Current).UpdateFocusEventsSubscription();   // blocking starts now, not on the next tick

                //Show the running session. The native session screen needs nothing loaded first, so it goes up
                //straight away; the one-second wait is kept only for the web To Do Player a class checklist uses.
                if (FocusSessionQueueWriter.NativeOwnsQueue)
                {
                    ((App)(Application.Current)).ShowToDoPlayer();
                }
                else
                {
                    Task.Run(async () =>
                    {
                        await Task.Delay(1000);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ((App)(Application.Current)).ShowToDoPlayer();
                        });
                    });
                }

                if (appSettings.isFocusModeRunning)
                {
                    //change the title of the start focus session button
                    if (appSettings.bUseSuperPomodoroMode)
                        ((App)Application.Current).wndFocusBearMainWindow.UpdateLeafTitle(LeftButtonType.StartFocusSession, appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuStopSuperFocusSession"].ToString());
                    else
                        ((App)Application.Current).wndFocusBearMainWindow.UpdateLeafTitle(LeftButtonType.StartFocusSession, appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuStopFocusSession"].ToString());
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StartFocusModeTimer 2 - " + ex.Message);
            }

        }

        private void dispatcherTimer_Focus(object sender, EventArgs e)
        {
            LogWriter.LogWrite("dispatcherTimer_Focus() ended");
            try
            {
                dispatcherTimerFocus.Stop(); //Tick Once only

                CommandManager.InvalidateRequerySuggested();

                //Teacher Mode - the scheduled finish time is a legit end for a teacher controlled
                //class session; the manager does the cleanup (no personal focus-end survey window)
                if (TeacherSessionManager.Instance.IsTeacherSessionActive())
                {
                    if (TeacherSessionManager.Instance.CanTeacherSessionFinishNow())
                    {
                        TeacherSessionManager.Instance.TeacherSessionFinished("scheduled finish time reached");
                    }
                    else
                    {
                        //should not happen (the timer is armed with the session duration) - the session
                        //will still end via class-session-ended or the next legit finish check
                        LogWriter.LogWrite("dispatcherTimer_Focus - teacher session active but the scheduled finish time is not reached, ignoring");
                    }
                    return;
                }

                LogWriter.LogWrite("Stop the Focus Mode");

                var listItems = appSettings.arrFocusModes.Where(i => i.isOn == true);
                if (listItems.Count() != 0)
                {
                    try
                    {

                        //Stop the focus mode
                        OnUntill.Content = "";
                        listItems.First<FocusMode>().isOn = false;

                        if(appSettings.bUseSuperPomodoroMode)
                        {
                            appSettings.nSuperPomodoroCountCompleted++;
                        }

                        ((App)(Application.Current)).FinishFocusModeCommonTasks();

                        //Show the Focusmode end dialog
                        ((App)(Application.Current)).ShowFocusEndWindow();
                        
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("dispatcherTimer_Focus" + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("dispatcherTimer_Focus" + ex.Message);
            }
        }

        private void btnBrainDump_Click(object sender, RoutedEventArgs e)
        {

            //if (appSettings.isExpired)
            //{
            //    Hide();
            //    ((App)(Application.Current)).ShowExpiredNag();
            //    return;
            //}

            ((App)(Application.Current)).ShowBrainDumpWindow();

            Hide();

        }

        // Opens the in-app voice-command window (mic). Records the spoken command, transcribes it
        // and executes the intent (create task with due date/time, start focus, start habit, ...).
        private void btnVoiceCommand_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            try
            {
                appSettings.CapturePostHog("open-voice-command");
                var wnd = new Focus.VoiceCommandWindow();
                wnd.Show();
                wnd.Activate();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnVoiceCommand_Click ex - " + ex.Message);
            }
        }

        // Opens the Tab Audit window (extract open browser tabs to a CSV report).
        private void btnTabAudit_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            try
            {
                appSettings.CapturePostHog("open-tab-audit");
                // Tab Audit lives as a main-window tab (TabAudit.TabAuditView). Bring the main window to the
                // front and navigate straight to the Tab Audit leaf. NB: do NOT go via ShowPreferencesWindow —
                // that selects the Settings leaf first, which triggers the "password to change settings"
                // prompt. Tab Audit is a Tools tab and shouldn't ask for a password.
                ((App)(Application.Current)).BringMainWindowToFront();
                ((App)(Application.Current)).wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.TabAudit);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnTabAudit_Click ex - " + ex.Message);
            }
        }

        // Focus Tools accordion — expand this one, closing the others (only one open at a time).
        private void btnToolsHeader_Click(object sender, RoutedEventArgs e)
        {
            bool expand = ToolsBody.Visibility != Visibility.Visible;
            SetAccordionState(toolsOpen: expand, breaksOpen: false, dayPlanOpen: false);
        }

        // Breaks accordion — expand this one, closing the others (only one open at a time).
        private void btnBreaksHeader_Click(object sender, RoutedEventArgs e)
        {
            bool expand = BreaksBody.Visibility != Visibility.Visible;
            SetAccordionState(toolsOpen: false, breaksOpen: expand, dayPlanOpen: false);
        }

        // Day Plan accordion — expand this one, closing the others.
        private void btnDayPlanHeader_Click(object sender, RoutedEventArgs e)
        {
            bool expand = DayPlanBody.Visibility != Visibility.Visible;
            SetAccordionState(toolsOpen: false, breaksOpen: false, dayPlanOpen: expand);
        }

        // Applies accordion open/closed state. Only one accordion is ever open at a time.
        // Each card is a white/theme card when collapsed and turns orange when expanded (matches macOS).
        private void SetAccordionState(bool toolsOpen, bool breaksOpen, bool dayPlanOpen)
        {
            // Opening any accordion closes the inline focus card too (only one card open at a time).
            if (FocusStartExpanded.Visibility == Visibility.Visible)
            {
                FocusStartExpanded.Visibility = Visibility.Collapsed;
                if (!appSettings.isFocusModeRunning) btnStartPomodoroMode.Visibility = Visibility.Visible;
            }
            ToolsBody.Visibility = toolsOpen ? Visibility.Visible : Visibility.Collapsed;
            BreaksBody.Visibility = breaksOpen ? Visibility.Visible : Visibility.Collapsed;
            DayPlanBody.Visibility = dayPlanOpen ? Visibility.Visible : Visibility.Collapsed;
            StyleAccordionHeader(ToolsSection, Tools, ToolsSubtitle, ToolsChevronPath, ToolsChevron, toolsOpen);
            StyleAccordionHeader(BreaksSection, BreaksTitle, BreaksSubtitle, BreaksChevronPath, BreaksChevron, breaksOpen);
            StyleAccordionHeader(DayPlanSection, DayPlanTitle, DayPlanSubtitle, DayPlanChevronPath, DayPlanChevron, dayPlanOpen);
            SetTrayMenuPosition();
        }

        // Colours an accordion header for its open/closed state: collapsed = white card with a dark title,
        // grey subtitle and grey right-chevron; expanded = orange card with a near-black title/subtitle and a
        // near-black chevron rotated to point down. The icon square (peach) and body stay as-is.
        private void StyleAccordionHeader(Border card, TextBlock title, TextBlock subtitle,
                                          System.Windows.Shapes.Path chevron, RotateTransform chevronRotate, bool expanded)
        {
            try
            {
                if (expanded)
                {
                    card.Background = (Brush)new BrushConverter().ConvertFromString("#f0932a");
                    // Keep a border but make it orange so the selected card is clearly outlined
                    // (matches macOS — the expanded Focus Tools card has an orange outline).
                    card.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#f0932a");
                    card.BorderThickness = new Thickness(1.3);
                    // Near-black on the orange (7.5:1); white there was 2.4:1 (title) and 2.1:1 (subtitle).
                    title.Foreground = (Brush)FindResource("OnOrangeText");
                    subtitle.Foreground = (Brush)FindResource("OnOrangeSubtitle");
                    chevron.Stroke = (Brush)FindResource("OnOrangeText");
                    chevronRotate.Angle = 90;
                }
                else
                {
                    card.Background = (Brush)FindResource("TrayCardBg");
                    card.BorderBrush = (Brush)FindResource("TrayCardBorder");
                    card.BorderThickness = new Thickness(1);
                    title.Foreground = (Brush)FindResource("ForegroundColor");
                    subtitle.Foreground = (Brush)FindResource("TraySubtitleColor");
                    chevron.Stroke = (Brush)FindResource("TrayChevronColor");
                    chevronRotate.Angle = 0;
                }
            }
            catch (Exception ex) { LogWriter.LogWrite("StyleAccordionHeader ex - " + ex.Message); }
        }

        // Hover feedback for the accordion cards: highlight only the border (orange), never fill the header.
        // A collapsed card's grey border turns orange on hover; an expanded card is already orange, so leave it.
        private bool IsAccordionExpanded(Border card)
        {
            return (card == BreaksSection && BreaksBody.Visibility == Visibility.Visible)
                || (card == ToolsSection && ToolsBody.Visibility == Visibility.Visible)
                || (card == DayPlanSection && DayPlanBody.Visibility == Visibility.Visible);
        }

        private void AccordionCard_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Border card && !IsAccordionExpanded(card))
                card.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#f0932a");
        }

        private void AccordionCard_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Border card && !IsAccordionExpanded(card))
                card.BorderBrush = (Brush)FindResource("TrayCardBorder");
        }

        // Starts a micro break immediately (from the Breaks accordion).
        private void btnStartBreakNow_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            try
            {
                appSettings.CapturePostHog("tray-start-break-now");
                // Show a break immediately (StartBreakActivityTimer only schedules the *next* break).
                ((App)(Application.Current)).ShowBreakActivities();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnStartBreakNow_Click ex - " + ex.Message);
            }
        }

        // Populates the Breaks accordion header subtitle and body based on the current break state,
        // and hides the whole section while a routine / break / shut-off is active.
        private void UpdateBreaksCard()
        {
            try
            {
                // Don't offer breaks from the tray while a break or shut-off is running, or while a routine
                // is ACTIVELY running — but DO show them when the routine is postponed (mirrors the guards in
                // ShowBreakActivities, which allows breaks during a postponed routine).
                var act = appSettings.nCurrentActivityType;
                bool morningActive = act == CurrentActivityType.MORNING_HABITS && !appSettings.bHabitsPostponedState;
                bool eveningActive = act == CurrentActivityType.EVENING_HABITS
                                     && !appSettings.bHabitsPostponedState && !appSettings.bFocusSummaryPostponedState;
                if (act == CurrentActivityType.BREAK_ACTIVITY || act == CurrentActivityType.SHUT_OFF
                    || morningActive || eveningActive)
                {
                    BreaksSection.Visibility = Visibility.Collapsed;
                    return;
                }
                BreaksSection.Visibility = Visibility.Visible;
                btnResumeAutoBreaks.Visibility = Visibility.Collapsed; // only shown in the focus-session-breaks-off case below

                // Breaks turned off entirely in settings.
                if (!appSettings.bAreBreaksEnabled)
                {
                    SetBreaksDisabled(MenuViewString("menuBreaksOff", "Breaks are turned off"),
                        MenuViewString("menuBreaksOffBody", "Turn on breaks in settings to take regular breaks."));
                    return;
                }

                // Focus / Super Focus session running with micro-breaks skipped (mirrors dispatcherTimer_Breaks).
                // Unlike a blocking schedule, a manual break is still allowed here — so keep "Start a break now"
                // (when there are activities) and offer to resume automatic breaks for the session. (#1215)
                if (appSettings.bSkipMicroBreaksDuringFocus && (appSettings.isFocusModeRunning || appSettings.bUseSuperPomodoroMode))
                {
                    string key = appSettings.bUseSuperPomodoroMode ? "breaksDisabledSuperFocusSessionRunning" : "breaksDisabledFocusSessionRunning";
                    string dflt = appSettings.bUseSuperPomodoroMode ? "Super Focus session is running. Breaks disabled." : "Focus session is running. Breaks disabled.";
                    string msg = MenuInfoString(key, dflt);
                    BreaksSubtitle.Text = msg;
                    BreaksStatusText.Text = msg;

                    bool hasActivities = appSettings.arrAllbreakActivities != null && appSettings.arrAllbreakActivities.Count > 0;
                    btnStartBreakNow.Visibility = hasActivities ? Visibility.Visible : Visibility.Collapsed;
                    btnAddBreakActivity.Visibility = Visibility.Collapsed;

                    btnResumeAutoBreaks.Visibility = Visibility.Visible;
                    ResumeAutoBreaksText.Text = BreakStateString("txtEnableMicroBreaksForFocusSession", "Resume automatic breaks");
                    return;
                }

                // A blocking schedule is active (and not paused) with micro-breaks off. Mirrors
                // dispatcherTimer_Breaks, which suppresses breaks when !ActiveSchedule.bMicroBreaksEnabled
                // (field means "enabled": true = on). A paused schedule enforces nothing, so breaks resume.
                if (appSettings.IsBlockingScheduleActiveAndNotPaused(out var scheduleForBreaks)
                    && !scheduleForBreaks.bMicroBreaksEnabled)
                {
                    string msg = MenuInfoString("breaksDisabledBlockingScheduleActive", "Blocking schedule is active. Breaks disabled.");
                    SetBreaksDisabled(msg, msg);
                    return;
                }

                // No break activities configured → there's nothing to run on a break, so don't offer "Start a
                // break now". Show the status and an Add action that opens Edit Habits to create one.
                if (appSettings.arrAllbreakActivities == null || appSettings.arrAllbreakActivities.Count == 0)
                {
                    string noAct = BreakStateString("noBreakAddedText", "No break activities added yet.");
                    BreaksSubtitle.Text = noAct;
                    BreaksStatusText.Text = noAct;
                    btnStartBreakNow.Visibility = Visibility.Collapsed;
                    AddBreakActivityText.Text = BreakStateString("buttonAddBreakActivity", "Add break activity");
                    btnAddBreakActivity.Visibility = Visibility.Visible;
                    return;
                }
                btnAddBreakActivity.Visibility = Visibility.Collapsed;

                // Breaks available.
                btnStartBreakNow.Visibility = Visibility.Visible;
                if (appSettings.NextBreakAt.HasValue)
                {
                    int mins = (int)Math.Round((appSettings.NextBreakAt.Value - DateTime.Now).TotalMinutes);
                    if (mins > 0)
                    {
                        BreaksSubtitle.Text = MenuViewString("menuNextBreakIn", "Next break in %d min").Replace("%d", mins.ToString());
                        BreaksStatusText.Text = MenuViewString("menuNextBreakAt", "Your next break is at %@.")
                            .Replace("%@", appSettings.NextBreakAt.Value.ToString("t"));
                    }
                    else
                    {
                        BreaksSubtitle.Text = MenuViewString("menuBreakDueNow", "Break due now");
                        BreaksStatusText.Text = MenuViewString("menuBreakDueNowBody", "It's time for a break.");
                    }
                }
                else
                {
                    BreaksSubtitle.Text = MenuViewString("menuTakeBreakSubtitle", "Take a break");
                    BreaksStatusText.Text = MenuViewString("menuTakeBreakBody", "Take a break whenever you need one.");
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("UpdateBreaksCard ex - " + ex.Message);
            }
        }

        // Shared "breaks are disabled" presentation: show the reason, hide the start-a-break / add actions.
        private void SetBreaksDisabled(string subtitle, string body)
        {
            BreaksSubtitle.Text = subtitle;
            BreaksStatusText.Text = body;
            btnStartBreakNow.Visibility = Visibility.Collapsed;
            btnAddBreakActivity.Visibility = Visibility.Collapsed;
        }

        private void btnResumeAutoBreaks_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Turn automatic micro-breaks back on for the current focus session, then refresh the card
                // (which now shows the normal "next break" state instead of "breaks disabled"). (#1215)
                appSettings.bSkipMicroBreaksDuringFocus = false;
                UpdateBreaksCard();
            }
            catch (Exception ex) { LogWriter.LogWrite("btnResumeAutoBreaks_Click ex - " + ex.Message); }
        }

        // Reads a breakStateVcInfo config string with an English fallback.
        private string BreakStateString(string key, string fallback)
        {
            try
            {
                var val = appSettings.ConfigFileStrings["breakStateVcInfo"]?[key];
                return (val != null && val.ToString().Length > 0) ? val.ToString() : fallback;
            }
            catch { return fallback; }
        }

        // No break activities → open Edit Habits so the user can add one.
        private void btnAddBreakActivity_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            try
            {
                var app = (App)(Application.Current);
                app.BringMainWindowToFront();
                app.wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.EditHabits);
            }
            catch (Exception ex) { LogWriter.LogWrite("btnAddBreakActivity_Click ex - " + ex.Message); }
        }

        // Reads a menuInfo config string with an English fallback.
        private string MenuInfoString(string key, string fallback)
        {
            try
            {
                var val = appSettings.ConfigFileStrings["menuInfo"][key];
                return (val != null && val.ToString().Length > 0) ? val.ToString() : fallback;
            }
            catch { return fallback; }
        }

        // Reads a menuView config string (the macOS-aligned tray section) with an English fallback.
        private string MenuViewString(string key, string fallback)
        {
            try
            {
                var val = appSettings.ConfigFileStrings["menuView"][key];
                return (val != null && val.ToString().Length > 0) ? val.ToString() : fallback;
            }
            catch { return fallback; }
        }

        // Applies the static, macOS-aligned tray labels from the menuView config section (English fallbacks so
        // it still reads correctly before the remote config downloads). Dynamic labels (focus minutes, breaks
        // status, day-plan next-habit, postponed line) are set in their own update methods.
        private void ApplyMenuViewStrings()
        {
            try
            {
                FocusGoalPlaceholder.Text = MenuViewString("menuFocusGoalPlaceholder", "Enter Focus Goal here");
                SelectFocusModeLabel.Text = MenuViewString("menuSelectFocusMode", "Select Focus Mode");
                FocusTapToStartText.Text = MenuViewString("menuTapToStart", "TAP TO START");
                ChangeTimeHint.Text = MenuViewString("menuChangeTimeHint", "CHANGE THE TIME WITH + AND -");
                BreaksTitle.Text = MenuViewString("menuBreaks", "Breaks");
                DayPlanTitle.Text = MenuViewString("menuDayPlan", "Day Plan");
                NextHabitLabel.Text = MenuViewString("menuNextHabitLabel", "NEXT HABIT");
                DayPlanStartText.Text = MenuViewString("menuStart", "Start");
                TasksForTodayLabel.Text = MenuViewString("menuTasksForToday", "TASKS FOR TODAY");
                ViewFullDayPlanText.Text = MenuViewString("menuViewFullDayPlan", "View full day plan");
                Tools.Text = MenuViewString("menuFocusTools", "Focus Tools");
                ToolsSubtitle.Text = MenuViewString("menuFocusToolsSubtitle", "Music, brain dump & to-dos");
                PlayPauseMusicText.Text = MenuViewString("menuPauseMusic", "Pause music");
                btnFocusTools.ToolTip = MenuViewString("menuOpenFocusScreen", "Open the full focus screen");
            }
            catch (Exception ex) { LogWriter.LogWrite("ApplyMenuViewStrings ex - " + ex.Message); }
        }

        private void btnToDoPlayer_Click(object sender, RoutedEventArgs e)
        {
            if(appSettings.isFocusModeRunning)
            {
                ((App)(Application.Current)).ShowToDoPlayer();
            }
            else
            {
                ((App)(Application.Current)).ShowToDoList();
            }
            
        }

        private void btnLimitedBlockingEdit_Click(object sender, RoutedEventArgs e)
        {
            // Straight to Blocking. Going through ShowPreferencesWindow selected Settings first, so with a password
            // rule on it asked twice - and a Cancel on the Settings prompt queued a jump to Help that then landed
            // on top of the Blocking prompt.
            Hide();
            ((App)(Application.Current)).BringMainWindowToFront();
            ((App)(Application.Current)).wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.Blocking);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Hook into Windows messages
            var hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            if (hwndSource != null)
            {
                hwndSource.AddHook(WndProctheme);
            }

            // Apply the correct theme at startup
            ChangeThemeByWindows();
            var isLightTheme = Global.IsLightTheme();
            UseImmersiveDarkMode(hwndSource.Handle, !isLightTheme);
        }
        private void ChangeThemeByWindows()
        {
            bool isDarkMode = Global.IsLightTheme();
            string theme = isDarkMode ? "Light" : "Dark";
            LogWriter.LogWrite("TrayMenu detect theme changed WndProctheme" + theme);
            string selectedTheme = appSettings.sTheme;
            LogWriter.LogWrite("TrayMenu report autotheme status" + appSettings.bAutoTheme);
            if (isDarkMode == true && appSettings.bAutoTheme == true)
            {
                selectedTheme = "Light";
            }
            else if (isDarkMode == false && appSettings.bAutoTheme == true)
            {
                selectedTheme = "Dark";
            }
            ((App)Application.Current).ChangeTheme(selectedTheme);
            RegistryManager.SetThemeMode();

        }
        private IntPtr WndProctheme(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            int WM_SETTINGCHANGE = 0x1A;
            if (msg == WM_SETTINGCHANGE)
            {
                // Apply theme when system settings change
                ChangeThemeByWindows();
            }
            return IntPtr.Zero;
        }
        private static bool UseImmersiveDarkMode(IntPtr handle, bool enabled)
        {
            Version osVersion = Environment.OSVersion.Version;
            bool isWindows10OrGreater = (osVersion.Major == 10 && osVersion.Build >= 17763) || osVersion.Major > 10;

            if (isWindows10OrGreater)
            {
                var attribute = DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1;
                if (osVersion.Build >= 18985)
                {
                    attribute = DWMWA_USE_IMMERSIVE_DARK_MODE;
                }

                int useImmersiveDarkMode = enabled ? 1 : 0;
                return DwmSetWindowAttribute(handle, attribute, ref useImmersiveDarkMode, sizeof(int)) == 0;
            }

            return false;
        }

        private void btnPauseBlockingSchedule_Checked(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("BlockingSchedule Reactivated from menu");
            var scheduleToReactivate = appSettings.ActiveSchedule;   // capture once
            if (scheduleToReactivate != null)
            {
                scheduleToReactivate.isPaused = false;
                BlockingScheduleState.Text = appSettings.ConfigFileStrings["menuInfo"]["stateActive"].ToString(); //"Active";

                //In case the pomodoro page is visible, lets reload it
                ((App)Application.Current).wndFocusBearMainWindow?.ReloadLeafViewIfVisible(LeftButtonType.StartFocusSession);

                UpdateBreaksCard();   // schedule active again → refresh whether breaks are allowed
            }
        }

        private void btnPauseBlockingSchedule_Unchecked(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("BlockingSchedule Paused from menu");
            var scheduleToPause = appSettings.ActiveSchedule;   // capture once
            if (scheduleToPause != null)
            {
                if (scheduleToPause.BlockLevel == FOCUSMODESTRICTNESS.CHILL_MODE)
                {
                    scheduleToPause.isPaused = true;

                    BlockingScheduleState.Text = appSettings.ConfigFileStrings["menuInfo"]["statePaused"].ToString(); //"Paused";
                }
                else if (scheduleToPause.BlockLevel == FOCUSMODESTRICTNESS.STRICT)
                {
                    //Timer
                    BlockingScheduleStrictTimer mwndTimer = new BlockingScheduleStrictTimer();
                    mwndTimer.Topmost = true;
                    mwndTimer.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    bool? dialogResult = mwndTimer.ShowDialog();

                    if (dialogResult.Value == true)
                    {
                        var scheduleAfterDialog = appSettings.ActiveSchedule;   // re-capture after the dialog
                        if (scheduleAfterDialog != null)
                        {
                            scheduleAfterDialog.isPaused = true;

                            ((App)(Application.Current)).wndTrayMenu.BlockingScheduleState.Text = appSettings.ConfigFileStrings["menuInfo"]["statePaused"].ToString(); //"Active";
                        }
                    }
                    else
                    {
                        scheduleToPause.isPaused = false;

                        BlockingScheduleState.Text = appSettings.ConfigFileStrings["menuInfo"]["stateActive"].ToString(); //"Active";
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            btnPauseBlockingSchedule.IsChecked = true;
                        }), System.Windows.Threading.DispatcherPriority.Normal);
                    }
                }
                else if (scheduleToPause.BlockLevel == FOCUSMODESTRICTNESS.IRON_FOCUS)
                {
                    if(appSettings.isPasswordSet)
                    {
                        //ask password
                        AskPassword pwdWnd = new AskPassword();
                        pwdWnd.Owner = this;
                        bool? bRes = pwdWnd.ShowDialog();

                        if (bRes.Value)
                        {
                            //Password is correct
                            scheduleToPause.isPaused = true;
                            BlockingScheduleState.Text = appSettings.ConfigFileStrings["menuInfo"]["statePaused"].ToString(); //"Paused";

                        }
                        else
                        {
                            scheduleToPause.isPaused = false;

                            BlockingScheduleState.Text = appSettings.ConfigFileStrings["menuInfo"]["stateActive"].ToString(); //"Active";
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                btnPauseBlockingSchedule.IsChecked = true;
                            }), System.Windows.Threading.DispatcherPriority.Normal);
          
                        }
                    }
                    else
                    {
                        FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["leftMenuVcInfo"]["superStrictNeedPasswordText"].ToString(), appSettings.ConfigFileStrings["common"]["appName"].ToString());
                        ff.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                        ff.ShowDialog();

                        scheduleToPause.isPaused = false;

                        BlockingScheduleState.Text = appSettings.ConfigFileStrings["menuInfo"]["stateActive"].ToString(); //"Active";
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            btnPauseBlockingSchedule.IsChecked = true;
                        }), System.Windows.Threading.DispatcherPriority.Normal);

                    }

                }

                //In case the pomodoro page is visible, lets reload it
                ((App)Application.Current).wndFocusBearMainWindow?.ReloadLeafViewIfVisible(LeftButtonType.StartFocusSession);

                UpdateBreaksCard();   // pausing a schedule lifts its break suppression → refresh the card
            }
        }

        private void UpdatePomodoroPauseButtonText()
        {
            try
            {
                if (btnStopPomodoroMode == null || btnStopPomodoroModetxt == null)
                    return;

                // If no focus session is running, leave the default text or visibility logic
                if (!appSettings.isFocusModeRunning)
                    return;

                btnStopPomodoroModetxt.Text = appSettings.GetFocusPauseButtonText();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("UpdatePomodoroPauseButtonText() ex = " + ex.Message);
            }
        }

        private void btnFocusMusic_Click(object sender, RoutedEventArgs e)
        {
            ((App)Application.Current).ShowFocusMusic();
        }

        private void btnPlayPauseMusic_Click(object sender, RoutedEventArgs e)
        { //mj// todo
            //Focus Music icon
            if (appSettings.arrFocusMusicTracks.Count > 0)
            {
                var current = appSettings.arrFocusMusicTracks.FirstOrDefault(t => t.IsPlaying || t.IsPaused);
                if (current != null)
                {
                    //User took manual control of music - don't auto-resume it when the routine ends
                    appSettings.bFocusMusicPausedAutomatically = false;

                    if (current.IsPlaying)
                    {
                        ((App)Application.Current).wndFocusBearMainWindow?.PageFocusMusic?.PauseTrack(current);

                        string imagePath = ("pack://application:,,,/Focus Bear;component/images/play_icon.png");
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.UriSource = new Uri(imagePath);
                        image.EndInit();

                        PlayPauseButtonImage.Source = image;
                    }
                    else if (current.IsPaused)
                    {
                        ((App)Application.Current).wndFocusBearMainWindow?.PageFocusMusic?.ResumeTrack(current);

                        string imagePath = ("pack://application:,,,/Focus Bear;component/images/pause_icon.png");
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.UriSource = new Uri(imagePath);
                        image.EndInit();

                        PlayPauseButtonImage.Source = image;
                    }
                }

            }
        }
    }
}
