using Focus_Bear.API;
using Focus_Bear.Focus;
using Focus_Bear.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Focus_Bear.Pomodoro
{
    /// <summary>
    /// Interaction logic for StartSimplePomodoro.xaml
    /// </summary>
    public partial class StartSimplePomodoro : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        public StartSimplePomodoro()
        {
            InitializeComponent();
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.Topmost = true;

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
                RadioAI.Content = "AI powered blocking(recommended)";//todo win texts
                
                RadioSuperDistracting.Content = "Just block super distracting sites";//
                BlockingStyle.Text = "Blocking Style: ";
                RadioAI.IsChecked = true; //default checked

                HowMuchTime.Text = appSettings.ConfigFileStrings["preferencesInfo"]["pomodoroModeDurationText"].ToString();
                MinutesTitle.Text = appSettings.ConfigFileStrings["preferencesInfo"]["minutesText"].ToString();
                BreakDuration.Text = appSettings.ConfigFileStrings["preferencesInfo"]["breakDurationText"].ToString();
                MinutesBreakTitle.Text = appSettings.ConfigFileStrings["preferencesInfo"]["minutesText"].ToString();

                WhatPlan.Text = "What do you want to achieve in this pomodoro session?";//todo win texts //appSettings.ConfigFileStrings["pomodoroInfo"]["questionFirstText"].ToString();
                btnStartSessionText.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["buttonLetsDoItText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StartSimplePomodoro text setting ex - " + ex.Message);
            }

            UpdateValues();
        }

        public void UpdateValues()
        {
            LogWriter.LogWrite("Pomodoro window UpdateValues()" );
            try
            {
                Minutes.Text = appSettings.PomodoroMinutes;
                MinutesBreak.Text = appSettings.PomodoroBreakMinutes;

                //Should empty the value for tasks here
                appSettings.sRawTasks = "";
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

            if ((MinutesText.Length > 0 && int.Parse(MinutesText) > 0) && (MinutesBreakText.Length > 0 && int.Parse(MinutesBreakText) > 0) && TxtIntention.Text.Length > 0)
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
            LogWriter.LogWrite("btnStartSession_Click - Simple pomodoro");

            appSettings.CapturePostHog("start-pomodoro-mode-manually", string.Format("\"type\":\"simple-pomodoro\",\"focusDuration\":\"{0}\",\"breakDuration\":\"{1}\"", Minutes.Text, MinutesBreak.Text));

            appSettings.bAiCheckUrlEnabledLastValueBeforeSimplePomodoro = appSettings.bAiCheckUrlEnabled;

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
            Hide();
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

                        //Make the global variable true
                        appSettings.isFocusModeRunning = true;

                        ((App)Application.Current).ShowFocusProgressWindow();

                        if (((App)Application.Current).wndBrainDump != null && ((App)Application.Current).wndBrainDump.IsVisible) //Update the window if its made or is visible
                        {
                            ((App)Application.Current).wndBrainDump.ShowBrainDumpWindowAndStartTimer();
                        }

                        //Show and Set on untill value
                        ////((App)(Application.Current)).wndTrayMenu.OnUntill.Visibility = Visibility.Visible;
                        //Increase hearder height
                        ///((App)(Application.Current)).wndTrayMenu.OrangeHeader.Height = 45; //Originally 25

                        ///if (appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
                        {
                           /// ((App)(Application.Current)).wndTrayMenu.OrangeHeader.Height = 70;
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
                        appSettings.sRunningFocusModeIntention = TxtIntention.Text;

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

        private void RadioAI_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void RadioSuperDistracting_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void TxtIntention_TextChanged(object sender, TextChangedEventArgs e)
        {
            HandleStartFocusEnableDisable();
        }
    }
}
