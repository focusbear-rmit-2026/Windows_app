using Focus_Bear.API;
using Focus_Bear.Focus;
using Focus_Bear.Models;
using Focus_Bear.Services;
using Focus_Bear.Utils;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Focus_Bear.Pomodoro
{
    /// <summary>
    /// Interaction logic for StartPomodoro.xaml
    /// </summary>
    public partial class StartPomodoro : UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        FocusBearMainWindow _ParentWindow = null;
        public StartPomodoro()
        {
        }
        public StartPomodoro(FocusBearMainWindow ParentWindow)
        {
            InitializeComponent();

            _ParentWindow = ParentWindow;

            App.FocusModeAddedDeletedEvent += AddFocusModesToComboBox;

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }

            try
            {
                ShowAdvancedLabel.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["buttonAdvancedOptionsText"].ToString();
                Heading.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["title"].ToString();
                UseSuperPomodoroMode.Content = appSettings.ConfigFileStrings["pomodoroInfo"]["buttonHardcorePomodoroText"].ToString();

                WhichFocusMode.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["titleSelectBlockList"].ToString();
                EditFocusModesTXT.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["buttonEditBlockListsText"].ToString();
                PomodoroCountText.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["howManyPomodoroText"].ToString();
                
                HowMuchTime.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["pomodoroModeDurationText"].ToString();
                MinutesTitle.Text = appSettings.ConfigFileStrings["preferencesInfo"]["minutesText"].ToString();
                BreakDuration.Text = appSettings.ConfigFileStrings["preferencesInfo"]["breakDurationText"].ToString();
                MinutesBreakTitle.Text = appSettings.ConfigFileStrings["preferencesInfo"]["minutesText"].ToString();
                EditRatioTXT.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["buttonEditRatioText"].ToString();

                string sTip = appSettings.ConfigFileStrings["pomodoroInfo"]["toggleBreaksTooltip"].ToString();
                sTip = sTip.Replace("<BREAK_FREQUENCY>", appSettings.breakAfterMinutes.ToString());
                sTip = sTip.Replace("<BREAK_DURATION>", BreakDuration.ToString());

                MicroBreaksInfo.ToolTip = sTip;

                ShowAccordingFocusAlignmentScoreText.Text = appSettings.ConfigFileStrings["focusPreferencesAdvancedInfo"]["focusAlignmentScoreTitle"].ToString();

                WhatPlan.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["questionFirstText"].ToString();

                TimeUnfocusedText.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["timerText"].ToString();

                SelectedTasksTitle.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["selectedTasksTitle"].ToString();

                // For updating the text based on the current value of microbreak setting
                SkipMicroBreaks.Inlines.Clear();
                SkipMicroBreaks.Inlines.Add(
                    new Run(appSettings.bSkipMicroBreaksDuringFocus
                        ? appSettings.ConfigFileStrings["pomodoroInfo"]["titleBreaksOff"].ToString()
                        : appSettings.ConfigFileStrings["pomodoroInfo"]["titleBreaksOn"].ToString()
                    )
                );


                ShowFocusModeEnd.Content = appSettings.ConfigFileStrings["pomodoroInfo"]["buttonShowFocusProgressText"].ToString();
                btnStartSessionText.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["buttonLetsDoItText"].ToString();

                appSettings.bShowFocusModeEnd = true; //1 sep 2025: FORCING to show the end window.

                AddFocusModesToComboBox();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StartPomodoro text setting ex - " + ex.Message);
            }

            UpdateValues();

            this.IsVisibleChanged += (s, e) =>
            {
                if (this.IsVisible)
                {
                    UpdateValues();
                }
            };

            ChkShowAdvanced.Checked += (s, e) => ShowAdvancedOptionsControls(true);
            ChkShowAdvanced.Unchecked += (s, e) => ShowAdvancedOptionsControls(false);
            ShowAdvancedOptionsControls(ChkShowAdvanced.IsChecked == true);
        }

        public void AddFocusModesToComboBox()
        {
            //Load the combox box with focus mode name, trigged by event when new mode added
            ComboBoxFocusModes.Items.Clear();
            for (int i = 0; i < appSettings.arrFocusModes.Count(); i++)
            {
                if (!(appSettings.arrFocusModes[i].isMeetings))
                    ComboBoxFocusModes.Items.Add(appSettings.arrFocusModes[i].name);
            }
            if (ComboBoxFocusModes.Items.Count > 0)
            {
                appSettings.LoadMostRecentFocusMode();
                string lastUsedId = appSettings.sMostRecentFocusmodeDoneID;
                int indexToSelect = 0;
                if (!string.IsNullOrEmpty(lastUsedId))
                {
                    for (int j = 0; j < appSettings.arrFocusModes.Count(); j++)
                    {
                        if (appSettings.arrFocusModes[j].id == lastUsedId && !appSettings.arrFocusModes[j].isMeetings)
                        {
                            int found = ComboBoxFocusModes.Items.IndexOf(appSettings.arrFocusModes[j].name);
                            if (found >= 0)
                            {
                                indexToSelect = found;
                                break;
                            }
                        }
                    }
                }
                ComboBoxFocusModes.SelectedIndex = indexToSelect;

                var selectedMode = appSettings.arrFocusModes.FirstOrDefault(m => m.name == ComboBoxFocusModes.SelectedItem?.ToString());
                if (selectedMode != null)
                    FocusAlignmentThresholdCombo.SelectedItem = selectedMode.AlignmentScoreThreshold;
            }
        }

        public void UpdateValues(bool bEnterSuperPomodoroModeAfterMorningRoutine = false)
        {
            LogWriter.LogWrite("Start Pomodoro - Pomodoro window UpdateValues()" );
            try
            {
                // Teacher Mode (issue #1343): teachers get a class picker (when >1 class) + a
                // "start a personal session instead" option; everyone else sees nothing here.
                SetupTeacherOptions();

                AddFocusModesToComboBox();

                // Ensure blocking schedule state is current before checking banner visibility
                BlockingScheduleManager.CheckCustomBlockingScheduleTime();

                Minutes.Text = appSettings.PomodoroMinutes;
                MinutesBreak.Text = CalculateBreakFromRatio(appSettings.PomodoroMinutes);
                UpdateWorkToBreakRatioInfo();

                //Should empty the value for tasks here
                appSettings.sRawTasks = "";
                List<string> arrTaskNames = new List<string>();
                ListTasks.ItemsSource = arrTaskNames;
                 
                SelectedTasksTitle.Visibility = Visibility.Collapsed;
                ListTasks.Visibility = Visibility.Collapsed;
                ///

                if (bEnterSuperPomodoroModeAfterMorningRoutine)
                {
                    appSettings.bUseSuperPomodoroMode = true;
                    appSettings.sMinutesSuperPomodoro = Minutes.Text;
                    appSettings.sMinutesBreakPomodoro = MinutesBreak.Text;
                    appSettings.SuperPomodoroFocusedName = appSettings.AutomaticSuperPomodoroFocusedName;
                    appSettings.NumberOfSuperPomodorosToRun = appSettings.AutomaticNumberOfSuperPomodorosToRun;
                    appSettings.bEnterSuperPomodoroModeAfterMorningRoutine = false; //Reset for next time poomdoro view is shown
                }

                UseSuperPomodoroMode.IsEnabled = !appSettings.bUseSuperPomodoroMode;
                ComboBoxFocusModes.IsEnabled = !appSettings.bUseSuperPomodoroMode;
                EditFocusModes.IsEnabled = !appSettings.bUseSuperPomodoroMode;
                ComboBoxNumberOfPomodoros.IsEnabled = !appSettings.bUseSuperPomodoroMode;
                Minutes.IsEnabled = !appSettings.bUseSuperPomodoroMode;
                MinutesBreak.IsEnabled = !appSettings.bUseSuperPomodoroMode;
                SkipMicroBreaks.IsEnabled = !appSettings.bUseSuperPomodoroMode;

                //set the previous values selected by user in case of Super Pomodoro by user
                if (appSettings.bUseSuperPomodoroMode)
                {
                    UseSuperPomodoroMode.IsChecked = true;
                    
                    ComboBoxFocusModes.SelectedItem = appSettings.SuperPomodoroFocusedName;
                    ComboBoxNumberOfPomodoros.SelectedIndex = (appSettings.NumberOfSuperPomodorosToRun - 2);
                    Minutes.Text = appSettings.sMinutesSuperPomodoro;
                    MinutesBreak.Text = appSettings.sMinutesBreakPomodoro;

                    if (appSettings.nSuperPomodoroCountCompleted >= appSettings.NumberOfSuperPomodorosToRun)
                    {
                        PomodoroCompleted.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        PomodoroCompleted.Visibility = Visibility.Visible;
                        string sPomodoroDoneInfo = appSettings.ConfigFileStrings["pomodoroInfo"]["completedPomodoroCountText"].ToString();
                        sPomodoroDoneInfo = sPomodoroDoneInfo.Replace("<COMPLETED_POMODORO>", appSettings.nSuperPomodoroCountCompleted.ToString());
                        sPomodoroDoneInfo = sPomodoroDoneInfo.Replace("<TOTAL_POMODORO>", appSettings.NumberOfSuperPomodorosToRun.ToString());
                        sPomodoroDoneInfo = sPomodoroDoneInfo.Replace("<REMAINING_POMODORO>", (appSettings.NumberOfSuperPomodorosToRun - appSettings.nSuperPomodoroCountCompleted).ToString());
                        PomodoroCompleted.Text = sPomodoroDoneInfo;
                    }
                    //STart the focus mode immediately
                    StartFocusMode();
                }
                else
                {
                    PomodoroCompleted.Text = "";
                }
                if (appSettings.bShowFocusModeEnd)
                {
                    ShowFocusModeEnd.IsChecked = true;
                }
                else
                {
                    ShowFocusModeEnd.IsChecked = false;
                }

                UpdateBlockingScheduleUI();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Pomodoro window UpdateValues() EX - " + ex.Message);
            }
        }
        private void UseSuperPomodoroMode_Checked(object sender, RoutedEventArgs e)
        {
            WhichFocusMode.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["chooseTheFocusModeText"].ToString();

            PomodoroCountText.Visibility = Visibility.Visible;
            ComboBoxNumberOfPomodoros.Visibility = Visibility.Visible;

            if (!appSettings.bUseSuperPomodoroMode)
            {
                _ParentWindow.GridShade.Visibility = Visibility.Visible;
                FBMessageBox msg = new FBMessageBox(appSettings.ConfigFileStrings["pomodoroInfo"]["alertWarningPaswwordSetText"].ToString());
                msg.Owner = _ParentWindow;
                msg.ShowDialog();
                _ParentWindow.GridShade.Visibility = Visibility.Collapsed;
            }
        }

        private void UseSuperPomodoroMode_Unchecked(object sender, RoutedEventArgs e)
        {
            WhichFocusMode.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["questionSecondText"].ToString();

            PomodoroCountText.Visibility = Visibility.Collapsed;
            ComboBoxNumberOfPomodoros.Visibility = Visibility.Collapsed;
        }

        private void ComboBoxFocusModes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxFocusModes.SelectedItem == null)
            {
                return;
            }
            string sFocusModeToStart = ComboBoxFocusModes.SelectedItem.ToString();

            for (int i = 0; i < appSettings.arrFocusModes.Count(); i++)
            {
                if (appSettings.arrFocusModes[i].name == sFocusModeToStart)
                {
                    FocusAlignmentThresholdCombo.SelectedIndex = (appSettings.arrFocusModes[i].AlignmentScoreThreshold / 10) - 5;
                }
            }
        }

        private void btnEditFocusModes_Click(object sender, RoutedEventArgs e)
        {
            ((App)(Application.Current)).wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.Blocking);
            ButtonAutomationPeer peer = new ButtonAutomationPeer(((App)(Application.Current)).wndFocusBearMainWindow.PageBlocks.btnBlockLists);
            IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            invokeProv.Invoke();
        }

        private void Minutes_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            Regex regex = new Regex("^[0-9]*$");
            e.Handled = !regex.IsMatch((sender as TextBox).Text.Insert((sender as TextBox).SelectionStart, e.Text));
        }

        private void Minutes_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = false;
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }

        }

        private void Minutes_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if ((e.Key == Key.Delete) || (e.Key == Key.Back))
            {
                HandleStartFocusEnableDisable();
            }
        }

        private void Minutes_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(Minutes.Text, out _))
            {
                MinutesBreak.Text = CalculateBreakFromRatio(Minutes.Text);
                UpdateWorkToBreakRatioInfo();
            }
            HandleStartFocusEnableDisable();
            string sTip = appSettings.ConfigFileStrings["pomodoroInfo"]["toggleBreaksTooltip"].ToString();
            sTip = sTip.Replace("<BREAK_FREQUENCY>", appSettings.breakAfterMinutes.ToString());
            sTip = sTip.Replace("<BREAK_DURATION>", MinutesBreak.Text.ToString());
            MicroBreaksInfo.ToolTip = sTip;
        }

        private void MinutesBreak_TextChanged(object sender, TextChangedEventArgs e)
        {
            HandleStartFocusEnableDisable();
        }

        private string CalculateBreakFromRatio(string workMinutesText)
        {
            if (int.TryParse(workMinutesText, out int workMinutes)
                && appSettings.PomodoroWorkToBreakRatio > 0
                && appSettings.PomodoroBreakRatio > 0)
            {
                int breakMinutes = Math.Max(1, (int)Math.Round((double)workMinutes * appSettings.PomodoroBreakRatio / appSettings.PomodoroWorkToBreakRatio));
                return breakMinutes.ToString();
            }
            return appSettings.PomodoroBreakMinutes;
        }

        private void UpdateWorkToBreakRatioInfo()
        {
            try
            {
                string ratioText = appSettings.ConfigFileStrings["pomodoroInfo"]["workRatioText"].ToString();
                ratioText = ratioText.Replace("<WORKRATIO>", appSettings.PomodoroWorkToBreakRatio.ToString());
                ratioText = ratioText.Replace("<BREAKRATIO>", appSettings.PomodoroBreakRatio.ToString());
                WorkToBreakRatioInfoText.Text = ratioText;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StartPomodoro UpdateWorkToBreakRatioInfo ex = " + ex.Message);
            }
        }

        private void btnEditRatio_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = ((App)(Application.Current)).wndFocusBearMainWindow;
            mainWindow.HandleLeafSelection(LeftButtonType.Settings);
            mainWindow.PageSettings.NavigateToPomodoroTab();
        }

        private void HandleStartFocusEnableDisable()
        {
            string MinutesText = Minutes.Text;
            string MinutesBreakText = MinutesBreak.Text;

            if ((MinutesText.Length > 0 && int.Parse(MinutesText) > 0) && (MinutesBreakText.Length > 0 && int.Parse(MinutesBreakText) > 0))
            {
                btnStartSession.IsEnabled = true;
            }
            else
            {
                btnStartSession.IsEnabled = false;
            }
        }

        private void SkipMicroBreaks_Click(object sender, RoutedEventArgs e)
        {
            appSettings.bSkipMicroBreaksDuringFocus = !appSettings.bSkipMicroBreaksDuringFocus;

            SkipMicroBreaks.Inlines.Clear();
            SkipMicroBreaks.Inlines.Add(
                new Run(appSettings.bSkipMicroBreaksDuringFocus
                    ? appSettings.ConfigFileStrings["pomodoroInfo"]["titleBreaksOff"].ToString()
                    : appSettings.ConfigFileStrings["pomodoroInfo"]["titleBreaksOn"].ToString()
                )
            );
        }


        private void ShowFocusModeEnd_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.bShowFocusModeEnd = true;
        }

        private void ShowFocusModeEnd_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bShowFocusModeEnd = false;
        }


        private async void btnStartSession_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnStartSession_Click - Start Pomodoro Window");
            appSettings.sRunningFocusModeIntention = TxtIntention.Text;
            App.FocusButton.SendFocusGoal(appSettings.sRunningFocusModeIntention);

            if (appSettings.bUseSuperPomodoroMode)
            {
                LogWriter.LogWrite("appSettings.bUseSuperPomodoroMode, set intention return; ");

                _ParentWindow.Hide();
                appSettings.isStartPomodoroWindowOpen = false;
                return;
            }
            else
            {
                if (UseSuperPomodoroMode.IsChecked.Value)
                {
                    appSettings.bUseSuperPomodoroMode = true;
                    ComboBoxItem typeItem = (ComboBoxItem)ComboBoxNumberOfPomodoros.SelectedItem;
                    appSettings.NumberOfSuperPomodorosToRun = int.Parse(typeItem.Content.ToString());
                    appSettings.nSuperPomodoroCountCompleted = 0; //Initialize
                    appSettings.CapturePostHog("start-focus-session-manually", string.Format("\"type\":\"super-focus\",\"focusDuration\":\"{0}\",\"breakDuration\":\"{1}\"", Minutes.Text, MinutesBreak.Text));
                }
                else
                {
                    appSettings.CapturePostHog("start-focus-session-manually", string.Format("\"type\":\"single-focus\",\"focusDuration\":\"{0}\",\"breakDuration\":\"{1}\"", Minutes.Text, MinutesBreak.Text));
                }

            }
            ComboBoxItem selectedItem = FocusAlignmentThresholdCombo.SelectedItem as ComboBoxItem;
            int value = int.Parse(selectedItem.Content.ToString());
            appSettings.sRunningFocusModeAlignmentScoreThreshold = (double) value / 100;

            appSettings.sMinutesBreakPomodoro = "";

            // Teacher Mode (#1343): if a teacher (who hasn't ticked "personal session") presses Focus, the
            // class session must be created FIRST. If the backend refuses (outside allowed hours, class
            // already live, ...), tell the teacher and start nothing — instead of silently starting focus.
            appSettings.bTeacherStartPersonalSession = ChkTeacherPersonalSession.IsChecked == true;
            if (((App)Application.Current).IsTeacherClassSessionStart())
            {
                string modeName = ComboBoxFocusModes.SelectedItem?.ToString();
                FocusMode mode = appSettings.arrFocusModes.FirstOrDefault(m => m.name == modeName);
                if (mode != null && int.TryParse(Minutes.Text, out int mins))
                {
                    appSettings.sRunningFocusModeEndTime = DateTime.Now.AddMinutes(mins).ToString();
                    int classResult = await ((App)Application.Current).StartClassSessionForMode(mode);
                    if (classResult != 1)
                    {
                        ShowClassSessionCannotStart(appSettings.sLastClassSessionError);
                        return;   // abort — no focus session started; the start screen stays open to retry
                    }
                }
            }

            // #1420: an Allow-List mode with AI off means every new page on an already-allowed site
            // still gets a permission prompt - warn before starting rather than letting the user hit
            // that wall mid-session.
            string sSelectedModeName = ComboBoxFocusModes.SelectedItem?.ToString();
            FocusMode selectedMode = appSettings.arrFocusModes.FirstOrDefault(m => m.name == sSelectedModeName);
            if (selectedMode != null && selectedMode.isAllowedListApproach && !appSettings.bAiCheckUrlEnabled)
            {
                _ParentWindow.GridShade.Visibility = Visibility.Visible;
                FBMessageBox msg = new FBMessageBox(
                    appSettings.GetConfigStringSafe("This focus mode uses an Allow List, but AI is off — you'll be asked for permission on every new page, even within a site you've already allowed.", "focusInfo", "startingFocusModeInfo", "bodyAllowListNoAiWarningText"),
                    appSettings.GetConfigStringSafe("AI isn't enabled", "focusInfo", "startingFocusModeInfo", "titleAllowListNoAiWarning"),
                    MessageBoxButton.YesNoCancel,
                    appSettings.GetConfigStringSafe("Turn On AI", "focusInfo", "startingFocusModeInfo", "buttonTurnOnAiText"),
                    appSettings.GetConfigStringSafe("Start Without AI", "focusInfo", "startingFocusModeInfo", "buttonStartWithoutAiText"),
                    appSettings.GetConfigStringSafe("Cancel", "common", "buttonCancelText"));
                msg.Owner = _ParentWindow;
                msg.ShowDialog();
                _ParentWindow.GridShade.Visibility = Visibility.Collapsed;

                if (msg.btnClicked == MessageBoxResult.Cancel)
                    return;
                if (msg.btnClicked == MessageBoxResult.Yes)
                {
                    appSettings.bAiCheckUrlEnabled = true;
                    LocalSettingsManager.PutLocalSettings();
                }
            }

            //Start Normal Focus mode
            StartFocusMode();

            appSettings.isStartPomodoroWindowOpen = false;
        }

        // Teacher Mode (#1343): tell the teacher a class session couldn't be started (so they know focus
        // did NOT start), and hint they can tick "personal session" to start a personal one instead.
        // Themed in-window notice (grid shade + card) rather than a jarring system MessageBox.
        private void ShowClassSessionCannotStart(string reason)
        {
            string msg = string.IsNullOrWhiteSpace(reason) ? "The class focus session couldn't be started." : reason;
            LogWriter.LogWrite("ShowClassSessionCannotStart - " + msg);
            ClassSessionNoticeText.Text = msg + "\n\nTo start a personal session instead, tick “Start a personal session instead” and press Focus again.";
            GridShade.Visibility = Visibility.Visible;
            ClassSessionNoticeOverlay.Visibility = Visibility.Visible;
        }

        private void BtnClassSessionNoticeOk_Click(object sender, RoutedEventArgs e)
        {
            ClassSessionNoticeOverlay.Visibility = Visibility.Collapsed;
            GridShade.Visibility = Visibility.Collapsed;
        }

        // Teacher Mode (issue #1343): show/populate the teacher-only class picker + personal-session
        // option on the Focus screen. Hidden entirely for non-teachers; the picker row only appears
        // when the teacher administers more than one class.
        private void SetupTeacherOptions()
        {
            try
            {
                TeacherOptionsPanel.Visibility = appSettings.bIsTeacher ? Visibility.Visible : Visibility.Collapsed;
                ChkTeacherPersonalSession.IsChecked = false;

                if (appSettings.bIsTeacher)
                {
                    TeacherClassPickerRow.Visibility = appSettings.arrTeacherClasses.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
                    CmbTeacherClass.ItemsSource = appSettings.arrTeacherClasses;
                    CmbTeacherClass.SelectedItem = appSettings.arrTeacherClasses.FirstOrDefault(c => c.TeamId == appSettings.sTeacherClassTeamId)
                                                   ?? appSettings.arrTeacherClasses.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetupTeacherOptions() ex - " + ex.Message);
            }
        }

        private void CmbTeacherClass_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CmbTeacherClass.SelectedItem is TeacherClass tc)
            {
                appSettings.sTeacherClassTeamId = tc.TeamId;
                appSettings.sTeacherClassName = tc.Name;
            }
        }

        private void StartFocusMode()
        {
            if (ComboBoxFocusModes.SelectedItem == null)
            {
                LogWriter.LogWrite("StartFocusMode() - No focus mode selected, return");
                return;
            }
            string sFocusModeToStart = ComboBoxFocusModes.SelectedItem?.ToString();

            for (int i = 0; i < appSettings.arrFocusModes.Count(); i++)
            {
                if (appSettings.arrFocusModes[i].name == sFocusModeToStart)
                {
                    int TotalMinutes = int.Parse(Minutes.Text);

                    //used when user starts another focus block
                    appSettings.sRunningFocusModeMinutes = TotalMinutes.ToString();

                    appSettings.sMinutesBreakPomodoro = MinutesBreak.Text;
                    appSettings.sMinutesSuperPomodoro = Minutes.Text;
                    appSettings.SuperPomodoroFocusedName = appSettings.arrFocusModes[i].name;

                    try
                    {
                        Sounds.PlayFocusModeStartSound();
                        //Start the Focus Mode
                        //Change the play icon for this to STOP icon
                        appSettings.arrFocusModes[i].isOn = true;
                        appSettings.sMostRecentFocusmodeDoneID = appSettings.arrFocusModes[i].id;

                        //Check if there are Last Task list saved, add it to raw tasks
                        appSettings.sRawTasks = appSettings.arrFocusModes[i].lastTaskUsed;

                        //Make the global variable true
                        appSettings.isFocusModeRunning = true;

                        // Reset pause state when a new focus mode starts
                        appSettings.isFocusModePaused = false;
                        appSettings.nPausedFocusRemainingSeconds = 0;

                        ((App)Application.Current).ShowFocusProgressWindow();

                       
                        DateTime dTill = DateTime.Now;

                        appSettings.sRunningFocusModeStartTime = dTill.ToString("yyyy-MM-dd HH:mm:ss");
                        appSettings.focusSessionStartTime = DateTime.Now;
                        appSettings.CurrentFocusSessionDuration = 0;

                        dTill = dTill.AddMinutes(TotalMinutes);
                        string sTill = appSettings.ConfigFileStrings["menuInfo"]["untilTimeText"].ToString();
                        sTill = sTill.Replace("<TIME>", dTill.ToString());
                        ((App)(Application.Current)).wndTrayMenu.OnUntill.Content = sTill;

                        appSettings.sRunningFocusModeID = appSettings.arrFocusModes[i].id;
                        appSettings.sRunningFocusModeName = appSettings.arrFocusModes[i].name;
                        appSettings.sRunningFocusModeEndTime = dTill.ToString();

                        //Clear any old stuff for gentle blocking
                        appSettings.TemporaryAllowedAppsGentleBlock.Clear();
                        appSettings.TemporarryBlockedAppsGentleBlock.Clear();
                        appSettings.TemporaryAllowedURLsGentleBlock.Clear();
                        appSettings.TemporarryBlockedURLsGentleBlock.Clear();

                        appSettings.TemporaryAllowedKeywords.Clear();
                        appSettings.TemporaryAllowedURLsTabTitles.Clear();
                        //Start the Focus Mode Timer to end after x hours / minutes
                        ((App)(Application.Current)).wndTrayMenu.StartFocusModeTimer(0, TotalMinutes);

                        // Teacher Mode (issue #1343): a teacher starts a class focus session here (unless
                        // they ticked "personal session"); everyone else starts a personal session, unchanged.
                        appSettings.bTeacherStartPersonalSession = ChkTeacherPersonalSession.IsChecked == true;
                        ((App)Application.Current).StartFocusSessionBackend(appSettings.arrFocusModes[i]);
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("Start focus mode from pomodoro window Exception - " + ex.Message);
                    }

                    break; //we found the need one, so stop
                }
            }
        }
        
        private void ShowAdvancedOptionsControls(bool visible)
        {
            var vis = visible ? Visibility.Visible : Visibility.Collapsed;
            UseSuperPomodoroMode.Visibility = vis;
            PomodoroCountPanel.Visibility = vis;
            ShowAccordingFocusAlignmentScorePanel.Visibility = vis;
        }

        private void FocusAlignmentThresholdCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(ComboBoxFocusModes.SelectedItem == null)
            {
                return;
            }
            string sFocusModeToStart = ComboBoxFocusModes.SelectedItem.ToString();

            for (int i = 0; i < appSettings.arrFocusModes.Count(); i++)
            {
                if (appSettings.arrFocusModes[i].name == sFocusModeToStart)
                {
                    ComboBoxItem selectedItem = FocusAlignmentThresholdCombo.SelectedItem as ComboBoxItem;
                    int value = int.Parse(selectedItem.Content.ToString());
                    appSettings.arrFocusModes[i].AlignmentScoreThreshold = value;
                }
            }
        }
        private void UpdateBlockingScheduleUI()
        {
            if (BlockingScheduleManager.IsIronFocusScheduleActive())
            {
                var scheduleForPomodoro = appSettings.ActiveSchedule;   // capture once — may be nulled on the UI thread
                ComboBoxFocusModes.SelectedValue = scheduleForPomodoro?.FocusModeName;
                ComboBoxFocusModes.IsEnabled = false;
                BlockingScheduleBannerText.Text = appSettings.ConfigFileStrings?["pomodoroInfo"]?["superStrictBlockingActiveMessage"]?.ToString()
                    ?? "A super strict blocking schedule is active.";

                // Clear any held fade from a previous visit, otherwise the retained Opacity=0 wins over the line below and the banner never shows again
                BlockingScheduleBanner.BeginAnimation(OpacityProperty, null);
                BlockingScheduleBanner.Opacity = 1;
                BlockingScheduleBanner.Visibility = Visibility.Visible;
                // Auto-dismiss the message after 5 seconds, stays disabled regardless
                DoubleAnimation fadeOut = new DoubleAnimation { From = 1.0, To = 0.0, BeginTime = TimeSpan.FromSeconds(5), Duration = TimeSpan.FromSeconds(2) };
                fadeOut.Completed += (s, e) => BlockingScheduleBanner.Visibility = Visibility.Collapsed;
                BlockingScheduleBanner.BeginAnimation(OpacityProperty, fadeOut);
            }
            else
            {
                ComboBoxFocusModes.IsEnabled = true;
                BlockingScheduleBanner.BeginAnimation(OpacityProperty, null);
                BlockingScheduleBanner.Visibility = Visibility.Collapsed;
            }
        }
    }
}
