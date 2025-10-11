using Focus_Bear.API;
using Focus_Bear.Focus;
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
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Focus_Bear.Pomodoro
{
    /// <summary>
    /// Interaction logic for StartPomodoro.xaml
    /// </summary>
    public partial class StartPomodoro : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        public StartPomodoro()
        {
            InitializeComponent();
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.Topmost = true;
            App.FocusModeAddedDeletedEvent += AddFocusModesToComboBox;

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }
            App.ThemeChanged += TitleBarThemeChange;
            try
            {
                ShowAdvancedLabel.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["buttonAdvancedOptionsText"].ToString();
                Heading.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["title"].ToString();
                UseSuperPomodoroMode.Content = appSettings.ConfigFileStrings["pomodoroInfo"]["buttonHardcorePomodoroText"].ToString();

                WhichFocusMode.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["questionSecondText"].ToString();
                
                EditFocusModesTXT.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["buttonEditFocusModeText"].ToString();
                PomodoroCountText.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["howManyPomodoroText"].ToString();
                
                HowMuchTime.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["pomodoroModeDurationText"].ToString();
                MinutesTitle.Text = appSettings.ConfigFileStrings["preferencesInfo"]["minutesText"].ToString();
                BreakDuration.Text = appSettings.ConfigFileStrings["preferencesInfo"]["breakDurationText"].ToString();
                MinutesBreakTitle.Text = appSettings.ConfigFileStrings["preferencesInfo"]["minutesText"].ToString();

                string sTip = appSettings.ConfigFileStrings["pomodoroInfo"]["toggleBreaksTooltip"].ToString();
                sTip = sTip.Replace("<BREAK_FREQUENCY>", appSettings.breakAfterMinutes.ToString());
                sTip = sTip.Replace("<BREAK_DURATION>", BreakDuration.ToString());

                MicroBreaksInfo.ToolTip = sTip;

                ShowAccordingFocusAlignmentScoreText.Text = appSettings.ConfigFileStrings["focusPreferencesAdvancedInfo"]["focusAlignmentScoreTitle"].ToString();

                WhatPlan.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["questionFirstText"].ToString();

                TimeUnfocusedText.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["timerText"].ToString();

                SelectedTasksTitle.Text = "Selected Tasks:";

                // For updating the text based on the current value of microbreak setting
                SkipMicroBreaks.Inlines.Clear();
                SkipMicroBreaks.Inlines.Add(
                    new Run(appSettings.bSkipMicroBreaksDuringFocus
                        ? appSettings.ConfigFileStrings["pomodoroInfo"]["titleBreaksOff"].ToString()
                        : appSettings.ConfigFileStrings["pomodoroInfo"]["titleBreaksOn"].ToString()
                    )
                );


                //ShowFocusModeEnd.Content = appSettings.ConfigFileStrings["pomodoroInfo"]["buttonShowFocusProgressText"].ToString();// need to add ES in config
                ShowFocusModeEnd.Content = "Show focus status when session end"; //to do win texts
                btnStartSessionText.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["buttonLetsDoItText"].ToString();

                appSettings.bShowFocusModeEnd = true; //1 sep 2025: FORCING to show the end window.

                AddFocusModesToComboBox();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StartPomodoro text setting ex - " + ex.Message);
            }

            UpdateValues();

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
                if (!(appSettings.arrFocusModes[i].isMeetings || appSettings.arrFocusModes[i].isLockedFocus))
                    ComboBoxFocusModes.Items.Add(appSettings.arrFocusModes[i].name);
            }
            if (ComboBoxFocusModes.Items.Count > 0)
            {
                ComboBoxFocusModes.SelectedIndex = 0;
                
                FocusAlignmentThresholdCombo.SelectedItem = appSettings.arrFocusModes[0].AlignmentScoreThreshold;
            }
        }

        public void UpdateValues(bool bEnterSuperPomodoroModeAfterMorningRoutine = false)
        {
            LogWriter.LogWrite("Start Pomodoro - Pomodoro window UpdateValues()" );
            try
            {
                Minutes.Text = appSettings.PomodoroMinutes;
                MinutesBreak.Text = appSettings.PomodoroBreakMinutes;

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
                GridShade.Visibility = Visibility.Visible;
                FBMessageBox msg = new FBMessageBox(appSettings.ConfigFileStrings["pomodoroInfo"]["alertWarningPaswwordSetText"].ToString());
                msg.Owner = this;
                msg.ShowDialog();
                GridShade.Visibility = Visibility.Collapsed;
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
            ((App)(Application.Current)).ShowPreferencesWindow();
            ((App)(Application.Current)).wndPreferences.ShowFocusMode(ComboBoxFocusModes.SelectedItem.ToString());
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
            HandleStartFocusEnableDisable();
            //Update tooltip text
            string sTip = appSettings.ConfigFileStrings["pomodoroInfo"]["toggleBreaksTooltip"].ToString();
            sTip = sTip.Replace("<BREAK_FREQUENCY>", appSettings.breakAfterMinutes.ToString());
            sTip = sTip.Replace("<BREAK_DURATION>", MinutesBreak.Text.ToString());
            MicroBreaksInfo.ToolTip = sTip;
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


        private void btnStartSession_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnStartSession_Click - Start Pomodoro Window");
            appSettings.sRunningFocusModeIntention = TxtIntention.Text;

            if (appSettings.bUseSuperPomodoroMode)
            {
                LogWriter.LogWrite("appSettings.bUseSuperPomodoroMode, set intention return; ");

                Hide();
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
            //Start Normal Focus mode            
            StartFocusMode();
            Hide();
            appSettings.isStartPomodoroWindowOpen = false;
        }

        private void StartFocusMode()
        {
            string sFocusModeToStart = ComboBoxFocusModes.SelectedItem.ToString();

            for (int i = 0; i < appSettings.arrFocusModes.Count(); i++)
            {
                if (appSettings.arrFocusModes[i].name == sFocusModeToStart)
                {
                    int TotalMinutes = int.Parse(Minutes.Text);
                    int nHours = TotalMinutes / 60;
                    int nMinutes = TotalMinutes % 60;

                    //used when user starts another focus block
                    appSettings.sRunningFocusModeHours = nHours.ToString();
                    appSettings.sRunningFocusModeMinutes = nMinutes.ToString();

                    appSettings.sMinutesBreakPomodoro = MinutesBreak.Text;
                    appSettings.sMinutesSuperPomodoro = Minutes.Text;
                    appSettings.SuperPomodoroFocusedName = appSettings.arrFocusModes[i].name;

                    try
                    {
                        Sounds.PlayFocusModeStartSound();
                        //Start the Focus Mode
                        //Change the play icon for this to STOP icon
                        appSettings.arrFocusModes[i].isOn = true;

                        //Check if there are Last Task list saved, add it to raw tasks
                        appSettings.sRawTasks = appSettings.arrFocusModes[i].lastTaskUsed;

                        //Make the global variable true
                        appSettings.isFocusModeRunning = true;

                        ((App)Application.Current).ShowFocusProgressWindow();

                        if (((App)Application.Current).wndBrainDump != null && ((App)Application.Current).wndBrainDump.IsVisible) //Update the window if its made or is visible
                        {
                            ((App)Application.Current).wndBrainDump.ShowBrainDumpWindowAndStartTimer();
                        }

                        //Show and Set on untill value
                        ((App)(Application.Current)).wndTrayMenu.OnUntill.Visibility = Visibility.Visible;
                        //Increase hearder height
                        ((App)(Application.Current)).wndTrayMenu.OrangeHeader.Height = 45; //Originally 25

                        if (appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
                        {
                            ((App)(Application.Current)).wndTrayMenu.OrangeHeader.Height = 70;
                        }

                        DateTime dTill = DateTime.Now;

                        appSettings.sRunningFocusModeStartTime = dTill.ToString("yyyy-MM-dd HH:mm:ss");

                        dTill = dTill.AddHours(nHours);
                        dTill = dTill.AddMinutes(nMinutes);
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
                        ((App)(Application.Current)).wndTrayMenu.StartFocusModeTimer(nHours, nMinutes);

                        FocusBearAPIClient client = new FocusBearAPIClient();
                        var xResponse = client.StartFocusMode(appSettings.arrFocusModes[i].id);
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("Start focus mode from pomodoro window Exception - " + ex.Message);
                    }

                    break; //we found the need one, so stop
                }
            }
        }
        
        private void MinutesBreak_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {

        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
            appSettings.isStartPomodoroWindowOpen = false;
        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
            {
                app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            } 
        }
        
        private void TitleBarThemeChange()
        {
            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
            {
                app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                this.Width = 744;
                this.Height = 700;

                double screenLogicalWidth = SystemParameters.WorkArea.Width;
                double screenLogicalHeight = SystemParameters.WorkArea.Height;

                LogWriter.LogWrite(string.Format("screenLogicalWidth = {0}, screenLogicalHeight = {1}", screenLogicalWidth.ToString(), screenLogicalHeight.ToString()));

                // “ideal” logical minimum
                const double idealMinW = 744, idealMinH = 700;

                //Cap the minimum to what actually fits
                this.MinWidth = Math.Min(idealMinW, screenLogicalWidth);
                this.MinHeight = Math.Min(idealMinH, screenLogicalHeight);

                // 
                this.Width = this.MinWidth;
                this.Height = this.MinHeight;

                // 6)
                this.Left = Math.Max(0, (screenLogicalWidth - this.Width) / 2);
                this.Top = Math.Max(0, (screenLogicalHeight - this.Height) / 2);
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
    }
}
