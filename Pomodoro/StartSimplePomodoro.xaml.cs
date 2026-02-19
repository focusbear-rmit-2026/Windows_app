using Focus_Bear.API;
using Focus_Bear.Focus;
using Focus_Bear.Models;
using Focus_Bear.Services;
using Focus_Bear.Utils;
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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Focus_Bear.Pomodoro
{
    /// <summary>
    /// Interaction logic for StartSimplePomodoro.xaml
    /// </summary>
    public partial class StartSimplePomodoro : UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        int nStep = 1;
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        FocusBearMainWindow _ParentWindow = null;
        private DispatcherTimer blockingSchedulePopupTimer;
        public StartSimplePomodoro()
        { 
        }
        public StartSimplePomodoro(FocusBearMainWindow ParentWindow)
        {
            InitializeComponent();

            _ParentWindow = ParentWindow;

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
                Heading.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["title"].ToString();
                RadioAI.Content = appSettings.ConfigFileStrings["pomodoroInfo"]["buttonBlockingStyleAiText"].ToString(); //"AI powered blocking(recommended)";
                RadioAI.ToolTip = appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["toolTipAiBlocking"].ToString();//"When you open a website you haven't specifically blocked or allowed, we'll use AI to check whether the website is relevant or not based on your focus intention. In strict mode, it will block it immediately if it's not relevant. In non strict mode, it will show a popup asking whether you really need it.",;
                RadioSuperDistracting.Content = appSettings.ConfigFileStrings["pomodoroInfo"]["buttonBlockingStyleDistractingSitesText"].ToString(); //"Just block super distracting sites";//
                RadioSuperDistracting.ToolTip = appSettings.ConfigFileStrings["blockedUrlsViewInfo"]["checkBoxText"].ToString(); //"Immediately block super distracting sites"
                BlockingStyle.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["titleBlockingStyleText"].ToString(); //"Blocking Style: ";

                FocusDurationInfo.ToolTip = appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["subTitleWorkDuration"].ToString();
                BreakDurationInfo.ToolTip = appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["subTitleBreakDuration"].ToString();
                AIPoweredBlockingInfo.ToolTip = appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["toolTipAiBlocking"].ToString(); //"AI powered blocking uses machine learning to identify and block distracting websites and applications based on your usage patterns. It adapts over time to better suit your needs."
                IntentionInfo.ToolTip = appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["subTitleIntention"].ToString();

                HowMuchTime.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["pomodoroModeDurationText"].ToString();

                ((TextBlock)MoreOptionsExpander.Header).Text = "More Options"; // todo win texts //appSettings.ConfigFileStrings["pomodoroInfo"]["moreOptionsText"].ToString();

                MinutesTitle.Text = appSettings.ConfigFileStrings["preferencesInfo"]["minutesText"].ToString();
                BreakDuration.Text = appSettings.ConfigFileStrings["preferencesInfo"]["breakDurationText"].ToString();
                MinutesBreakTitle.Text = appSettings.ConfigFileStrings["preferencesInfo"]["minutesText"].ToString();

                WhatPlan.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["questionFirstText"].ToString();  //"What do you want to achieve in this pomodoro session?";
                TimeUnfocusedText.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["timerText"].ToString();
                btnStartSessionText.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["buttonLetsDoItText"].ToString();

                if(appSettings.isExpired)
                {
                    RadioSuperDistracting.IsChecked = true;
                }
                else
                {
                    RadioAI.IsChecked = true; //default checked in trial and full version
                }

                btnNextPopupTXT.Text = appSettings.ConfigFileStrings["menuTourVcInfo"]["buttonNextText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StartSimplePomodoro text setting ex - " + ex.Message);
            }

            UpdateValues();

            this.IsVisibleChanged += (s, e) =>
            {
                if (this.IsVisible)
                {
                    UpdateValues();
                }
                else
                {
                    // Stop timer and close popup when navigating away
                    if (blockingSchedulePopupTimer != null)
                    {
                        blockingSchedulePopupTimer.Stop();
                        blockingSchedulePopupTimer.Tick -= BlockingSchedulePopupTimer_Tick;
                        blockingSchedulePopupTimer = null;
                    }
                    BlockingSchedulePopup.IsOpen = false;
                }
            };

            _ParentWindow.Deactivated += (s, e) =>
            {
                BlockingSchedulePopup.IsOpen = false;
            };

            _ParentWindow.Activated += (s, e) =>
            {
                if (this.IsVisible)
                    UpdateValues();
            };

            //Removing the tour popup as now the UI has changed to show minimal options: 10 Oct 2025

        }
        private void ShowTourPopup()
        {
            // Update the popup position before opening it
            //UpdatePopupPosition();

            // Open the popup
            MyPopup.IsOpen = true;
            UpdateTourPopupTexts();
        }

        private void UpdateTourPopupTexts()
        {
            try
            {
                HeadingPopup.Text = appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["titleTour"].ToString();


                string sStep = appSettings.ConfigFileStrings["menuTourVcInfo"]["indexText"].ToString();
                sStep = sStep.Replace("<CURRENTINDEX>", (nStep).ToString()); //1
                sStep = sStep.Replace("<TOTALCOUNT>", "5");
                Steps.Text = sStep;

                if (nStep == 1)
                {
                    Title.Text = appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["titlePomodoroTime"].ToString();
                    Description.Text = appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["subTitlePomodoroTime"].ToString();
                }
                else if (nStep == 2)
                {
                    MyPopup.VerticalOffset = MyPopup.VerticalOffset + 70;
                    Title.Text = appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["titleBlockingStyle"].ToString();
                    Description.Text = appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["subTitleBlockingStyle"].ToString();
                }
                else if (nStep == 3)
                {
                    MyPopup.VerticalOffset = MyPopup.VerticalOffset + 85;
                    Title.Text = appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["titleWorkDuration"].ToString();
                    Description.Text = appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["subTitleWorkDuration"].ToString();
                }
                else if (nStep == 4)
                {
                    MyPopup.VerticalOffset = MyPopup.VerticalOffset + 50;
                    Title.Text = appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["titleBreakDuration"].ToString();
                    Description.Text = appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["subTitleBreakDuration"].ToString();
                }
                else if (nStep == 5)
                {
                    MyPopup.VerticalOffset = MyPopup.VerticalOffset + 50;
                    Title.Text = appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["titleIntention"].ToString();
                    Description.Text = appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["subTitleIntention"].ToString();

                    btnNextPopupTXT.Text = appSettings.ConfigFileStrings["menuTourVcInfo"]["buttonFinishText"].ToString(); //Finish
                }
                else if (nStep == 6)
                {
                    RegistryManager.SetSimplePomodoroTourShownStatus();
                    //Close the popup
                    MyPopup.IsOpen = false;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("UpdateTourPopupTexts() simple pomodoro popup ex - " + ex.Message);
            }
        }
      
        public void UpdateValues()
        {
            LogWriter.LogWrite("Start Simple Pomodoro - Pomodoro window UpdateValues()" );
            try
            {
                // Ensure blocking schedule state is current before checking banner visibility
                BlockingScheduleManager.CheckCustomBlockingScheduleTime();

                if (appSettings.isExpired)
                {
                    RadioSuperDistracting.IsChecked = true;
                }
                else
                {
                    RadioAI.IsChecked = true; //default checked in trial and full version
                }

                Minutes.Text = appSettings.PomodoroMinutes;
                MinutesBreak.Text = appSettings.PomodoroBreakMinutes;

                //Should empty the value for tasks here
                appSettings.sRawTasks = "";

                if (appSettings.ActiveSchedule != null && appSettings.isBlockingScheduleRunning)
                {
                    if (appSettings.ActiveSchedule.BlockLevel == FOCUSMODESTRICTNESS.IRON_FOCUS)
                    {
                        BlockingSchedulePopupTitle.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["superStrictBlockingActiveMessage"].ToString();
                    }
                    else if (appSettings.ActiveSchedule.BlockLevel == FOCUSMODESTRICTNESS.STRICT)
                    {
                        BlockingSchedulePopupTitle.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["strictBlockingCannotChangeMessage"].ToString();
                    }
                    else
                    {
                        BlockingSchedulePopupTitle.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["strictBlockingActiveMessage"].ToString();
                    }

                    // Show popup and auto-dismiss after 10 seconds
                    // Stop any previous timer before starting a new one
                    if (blockingSchedulePopupTimer != null)
                    {
                        blockingSchedulePopupTimer.Stop();
                        blockingSchedulePopupTimer.Tick -= BlockingSchedulePopupTimer_Tick;
                    }
                    BlockingSchedulePopupBorder.BeginAnimation(OpacityProperty, null);
                    BlockingSchedulePopupBorder.Opacity = 1;
                    BlockingSchedulePopup.IsOpen = true;
                    blockingSchedulePopupTimer = new DispatcherTimer();
                    blockingSchedulePopupTimer.Interval = TimeSpan.FromSeconds(10);
                    blockingSchedulePopupTimer.Tick += BlockingSchedulePopupTimer_Tick;
                    blockingSchedulePopupTimer.Start();
                }

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Pomodoro window UpdateValues() EX - " + ex.Message);
            }
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
  
        private void btnStartSession_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnStartSession_Click - Simple pomodoro Window");

            appSettings.CapturePostHog("start-focus-session-manually", string.Format("\"type\":\"simple-focus\",\"focusDuration\":\"{0}\",\"breakDuration\":\"{1}\"", Minutes.Text, MinutesBreak.Text));

            appSettings.bAiCheckUrlEnabledLastValueBeforeSimplePomodoro = appSettings.bAiCheckUrlEnabled;

            appSettings.sRunningFocusModeIntention = TxtIntention.Text;

            if (RadioAI.IsChecked.Value)
            {
                appSettings.bAiCheckUrlEnabled = true;
            }
            else
            {
                appSettings.bAiCheckUrlEnabled = false;
            }
            

            appSettings.sMinutesBreakPomodoro = "";
            //Start Normal Focus mode            
            StartFocusMode();
            
            //Close the popup if open
            MyPopup.IsOpen = false;
            RegistryManager.SetSimplePomodoroTourShownStatus();

            _ParentWindow.Hide();
            appSettings.isStartPomodoroWindowOpen = false;
        }

        private void StartFocusMode()
        {
            string sFocusModeToStart = "";

            for (int i = 0; i < appSettings.arrFocusModes.Count(); i++)
            {
                if (RadioAI.IsChecked.Value && appSettings.arrFocusModes[i].isDeepWorkFocus)
                {
                    sFocusModeToStart = appSettings.arrFocusModes[i].name;
                }
                else if (RadioSuperDistracting.IsChecked.Value && appSettings.arrFocusModes[i].isDefault)
                {
                    sFocusModeToStart = appSettings.arrFocusModes[i].name;
                }
            }

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

                        //Make the global variable true
                        appSettings.isFocusModeRunning = true;

                        appSettings.isFocusModePaused = false;
                        appSettings.nPausedFocusRemainingSeconds = 0;

                        ((App)Application.Current).ShowFocusProgressWindow();

                        if (((App)Application.Current).wndBrainDump != null && ((App)Application.Current).wndBrainDump.IsVisible) //Update the window if its made or is visible
                        {
                            ((App)Application.Current).wndBrainDump.ShowBrainDumpWindowAndStartTimer();
                        }

                        DateTime dTill = DateTime.Now;

                        appSettings.sRunningFocusModeStartTime = dTill.ToString("yyyy-MM-dd HH:mm:ss");

                        dTill = dTill.AddMinutes(TotalMinutes);
                        string sTill = appSettings.ConfigFileStrings["menuInfo"]["untilTimeText"].ToString();
                        sTill = sTill.Replace("<TIME>", dTill.ToString());

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

                        FocusBearAPIClient client = new FocusBearAPIClient();
                        var xResponse = client.StartFocusMode(appSettings.arrFocusModes[i].id);
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("Start focus mode from pomodoro window Exception - " + ex.Message);
                    }
                }
            }
        }
        private void MinutesBreak_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {

        }

        private void RadioAI_Checked(object sender, RoutedEventArgs e)
        {
            if (appSettings.isExpired)
            {
                e.Handled = true;

                //RadioSuperDistracting.Checked -= RadioSuperDistracting_Checked;
                RadioSuperDistracting.IsChecked = true;
               // RadioSuperDistracting.Checked += RadioSuperDistracting_Checked;

                ((App)(Application.Current)).ShowUpgradeToProNag(appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["titleNoAiBlocking"].ToString());
                // Prevent the click event from being processed further

                return;
            }
            e.Handled = false;
        }

        private void RadioSuperDistracting_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void TxtIntention_TextChanged(object sender, TextChangedEventArgs e)
        {
            HandleStartFocusEnableDisable();
        }

        private void btnNextPopup_Click(object sender, RoutedEventArgs e)
        {   
            nStep++;
            UpdateTourPopupTexts();
        }
        private void BlockingSchedulePopupTimer_Tick(object sender, EventArgs e)
        {
            blockingSchedulePopupTimer.Stop();
            blockingSchedulePopupTimer.Tick -= BlockingSchedulePopupTimer_Tick;

            DoubleAnimation fadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromSeconds(2)
            };

            fadeOut.Completed += (s, a) => { BlockingSchedulePopup.IsOpen = false; };
            BlockingSchedulePopupBorder.BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}
