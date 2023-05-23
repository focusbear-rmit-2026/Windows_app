using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Focus_Bear.Focus
{
    /// <summary>
    /// Interaction logic for StartFocusMode.xaml
    /// </summary>
    public partial class StartFocusMode : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        bool bIsDefaultFocusMode = false;
        bool bIsisLockedFocusMode = false;

        public string sIntention;
        public string sHours;
        public string sMinutes;

        
        public StartFocusMode()
        { 
        }

        public StartFocusMode(string sFocusName, bool bIsDefault, bool isLockedFocus)
        {
            LogWriter.LogWrite("StartFocusMode() func");

            InitializeComponent();
            /*
              "focusInfo":{
        "focusAlreadyRunningText":"A focus mode is already running. Please deactivate the running focus mode to start a new one.",
        "startingFocusModeInfo":{
            "buttonStartFocusText":"Start Focus",
            "howMuchTimeText":"How much time do you want to focus for?",
            "hoursText":"hours",
            "minutesText":"minutes",
            "title":"Starting Focus type - <FOCUSTYPE>",
            "questionText":"What do you plan to achieve during this focus block?",
            "onlyAllowSelectedAppText":"You'll only be able to use <APP>",
            "onlyAllowSelectedAppAndUrlText":"You'll only be able to use \"<URL>\" in <APP>",
            "explanationText":"Setting an intention for the focus block helps prime your brain to accomplish that task. To edit the intention after starting the focus mode, click Brain Dump in the menu bar."
        },
        */
            try 
            { 
                bIsDefaultFocusMode = bIsDefault;
                bIsisLockedFocusMode = isLockedFocus;

                Title = appSettings.ConfigFileStrings["focusInfo"]["startingFocusModeInfo"]["buttonStartFocusText"].ToString();

                string sHeading = appSettings.ConfigFileStrings["focusInfo"]["startingFocusModeInfo"]["title"].ToString();
                sHeading = sHeading.Replace("<FOCUSTYPE>", sFocusName);

                Heading.Text = sHeading;
                HowMuchTime.Text = appSettings.ConfigFileStrings["focusInfo"]["startingFocusModeInfo"]["howMuchTimeText"].ToString();
                WhatPlan.Text = appSettings.ConfigFileStrings["focusInfo"]["startingFocusModeInfo"]["questionText"].ToString();
                Descriptiopn.Text = appSettings.ConfigFileStrings["focusInfo"]["startingFocusModeInfo"]["explanationText"].ToString();

                HoursTitle.Text = appSettings.ConfigFileStrings["focusInfo"]["startingFocusModeInfo"]["hoursText"].ToString();
                MinutesTitle.Text = appSettings.ConfigFileStrings["focusInfo"]["startingFocusModeInfo"]["minutesText"].ToString();
                btnStartFocusText.Text = appSettings.ConfigFileStrings["focusInfo"]["startingFocusModeInfo"]["buttonStartFocusText"].ToString();

                SkipMicroBreaks.Content = appSettings.ConfigFileStrings["pomodoroInfo"]["buttonSkipMicrobreaksText"].ToString();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StartFocusMode()" + ex.Message);
            }
            Hours.Text = "00";
            Minutes.Text = "20"; //Defaults

            if (!appSettings.bStartingAnotherFocusMode)
            {
                appSettings.bSkipMicroBreaksDuringFocus = false;
                SkipMicroBreaks.IsChecked = appSettings.bSkipMicroBreaksDuringFocus;
            }
        }

        public void SetTextsIfStartingAnotherFocuModeBlock()
        {
            TxtIntention.Text = appSettings.sRunningFocusModeIntention;
            SkipMicroBreaks.IsChecked = appSettings.bSkipMicroBreaksDuringFocus;
            Hours.Text = appSettings.sRunningFocusModeHours;
            Minutes.Text = appSettings.sRunningFocusModeMinutes;

            //here our varialbe work gets over and we can make it false;
            appSettings.bStartingAnotherFocusMode = false;
        }
        private void btnStartFocus_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                sIntention = TxtIntention.Text;
                sHours = Hours.Text;
                sMinutes = Minutes.Text;

                appSettings.sRunningFocusModeHours = Hours.Text; 
                appSettings.sRunningFocusModeMinutes = Minutes.Text;

                DialogResult = true;
                Close();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StartFocusMode - btnStartFocus_Click" + ex.Message);
                Debug.WriteLine(ex.Message);
            }
   
        }

        private void HandleStartFocusEnableDisable()
        {
             

            string Hourstext = Hours.Text; 
            string Minutestext = Minutes.Text;
        
            if ((Hourstext.Length > 0 && int.Parse(Hourstext) > 0) || (Minutestext.Length > 0 && int.Parse(Minutestext) > 0))
            {
                btnStartFocus.IsEnabled = true;
            }
            else
            {
                btnStartFocus.IsEnabled = false;
            }
        }
        private void Hours_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {

            Regex regex = new Regex("^[0-9]+");
            e.Handled = !regex.IsMatch((sender as TextBox).Text.Insert((sender as TextBox).SelectionStart, e.Text));
            if (e.Handled)
            {
                // btnOK.IsEnabled = false;
            }
            else
            {
                HandleStartFocusEnableDisable();
                //btnStartFocus.IsEnabled = true; // all set enable the ok button 
                
            }
        }

        private void Hours_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = false;
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }

            string text = Hours.Text;
            text = text.Trim();
            if (text.Length > 0)
            {
                if (int.Parse(text) > 12)
                {
                    Hours.Text = "12";
                }
            }
           

        }

        private void Hours_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if ((e.Key == Key.Delete) || (e.Key == Key.Back))
            {
                string text = Hours.Text;
                text = text.Trim();
                if (text.Length > 0)
                {
                    //btnStartFocus.IsEnabled = true;
                    HandleStartFocusEnableDisable();
                }
                else
                {
                    //btnStartFocus.IsEnabled = false;
                    HandleStartFocusEnableDisable();
                }
            }
        }

        private void Minutes_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {

            Regex regex = new Regex("^[0-9]*$");
            e.Handled = !regex.IsMatch((sender as TextBox).Text.Insert((sender as TextBox).SelectionStart, e.Text));
            if (e.Handled)
            {
                // btnOK.IsEnabled = false;
            }
            else
            {
                // btnStartFocus.IsEnabled = true; // all set enable the ok button
               // HandleStartFocusEnableDisable();
            }
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
                string text = Minutes.Text;
                text = text.Trim();
                if (text.Length > 0)
                {
                    //btnStartFocus.IsEnabled = true;
                    HandleStartFocusEnableDisable();
                }
                else
                {
                    //btnStartFocus.IsEnabled = false;
                    HandleStartFocusEnableDisable();
                }
            }
        }

        private void Minutes_TextChanged(object sender, TextChangedEventArgs e)
        {
            HandleStartFocusEnableDisable();
        }

        private void Hours_TextChanged(object sender, TextChangedEventArgs e)
        {
            HandleStartFocusEnableDisable();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            if (bIsDefaultFocusMode)
            {
               // await Task.Delay(50);
               /** Removed Discourage focus on 28 Jan 2023
                GridShade.Visibility = Visibility.Visible;
                DiscourageDefault aa = new DiscourageDefault(this);
                aa.Topmost = true;
                bool? DialogResult = aa.ShowDialog();
                if (DialogResult.Value == true) //Shutup Clicked
                {
                    //Let the Start the Focus Mode
                }
                else//Cancel Clicked // CLose itself too
                {
                    Close();
                }
                GridShade.Visibility = Visibility.Collapsed;
                **/
            }
            else if (bIsisLockedFocusMode)
            {
                this.Cursor = Cursors.Wait;

                string sApp = appSettings.ConfigFileStrings["focusInfo"]["startingFocusModeInfo"]["onlyAllowSelectedAppText"].ToString();
                string sBrowser = appSettings.ConfigFileStrings["focusInfo"]["startingFocusModeInfo"]["onlyAllowSelectedAppAndUrlText"].ToString();

                string ProcessName = appSettings.sLastActiveAppName;
                if (ProcessName.Length > 0)
                {
                    sApp = sApp.Replace("<APP>", ProcessName);
                    sLockedText.Text = sApp;
                    string sBrowserApp = appSettings.sLastActiveAppName.Replace(".exe", "");
                    if (Global.BROWSERS.Exists(ele => ele == sBrowserApp))
                    {
                        appSettings.GetActiveTabURL(sBrowserApp);
                        if(appSettings.sLastActiveURL.Length > 0)
                        {
                            sBrowser = sBrowser.Replace("<APP>", ProcessName);
                            sBrowser = sBrowser.Replace("<URL>", appSettings.sLastActiveURL);
                            sLockedText.Text = sBrowser;
                        }
                        else
                        {
                            sLockedText.Text = sApp;
                        }
                       
                    }
                    sLockedText.Visibility = Visibility.Visible;
                }
                this.Cursor = Cursors.Arrow;

            }
            else if(appSettings.bStartingAnotherFocusMode)
            {
                SetTextsIfStartingAnotherFocuModeBlock();
            }

        }

        private void SkipMicroBreaks_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.bSkipMicroBreaksDuringFocus = true;
        }

        private void SkipMicroBreaks_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bSkipMicroBreaksDuringFocus = false;
        }
    }
}
