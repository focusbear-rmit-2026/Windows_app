using Focus_Bear.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
                bIsDefaultFocusMode = bIsDefault;
                bIsisLockedFocusMode = isLockedFocus;

                Title = appSettings.ConfigFileStrings["focusInfo"]["startingFocusModeInfo"]["buttonStartFocusText"].ToString();

                string sHeading = appSettings.ConfigFileStrings["focusInfo"]["startingFocusModeInfo"]["title"].ToString();
                sHeading = sHeading.Replace("<FOCUSTYPE>", sFocusName);

                Heading.Text = sHeading;
                HowMuchTime.Text = appSettings.ConfigFileStrings["focusInfo"]["startingFocusModeInfo"]["howMuchTimeText"].ToString();
                WhatPlan.Text = appSettings.ConfigFileStrings["focusInfo"]["startingFocusModeInfo"]["questionText"].ToString();
                Description.Text = appSettings.ConfigFileStrings["focusInfo"]["startingFocusModeInfo"]["explanationText"].ToString();

                HoursTitle.Text = appSettings.ConfigFileStrings["focusInfo"]["startingFocusModeInfo"]["hoursText"].ToString();
                MinutesTitle.Text = appSettings.ConfigFileStrings["focusInfo"]["startingFocusModeInfo"]["minutesText"].ToString();
                btnStartFocusText.Text = appSettings.ConfigFileStrings["focusInfo"]["startingFocusModeInfo"]["buttonStartFocusText"].ToString();

                SkipMicroBreaks.Content = appSettings.ConfigFileStrings["pomodoroInfo"]["buttonSkipMicrobreaksText"].ToString();

                btnSelectTasksTXT.Text = "Choose tasks to work on"; //todo win texts
                btnWriteFreeFormIntentionTXT.Text = "Write a freeform intention instead";
                SelectedTasksTitle.Text = "Selected Tasks:";
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StartFocusMode()" + ex.Message);
            }
            Hours.Text = "00";
            Minutes.Text = "20"; //Defaults

            if (!appSettings.bStartingAnotherFocusMode)
            {
                //appSettings.bSkipMicroBreaksDuringFocus = true; //default to true
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
                appSettings.CapturePostHog("start-focus-session-manually");

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
            }
            else
            {
                HandleStartFocusEnableDisable();
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
                    HandleStartFocusEnableDisable();
                }
                else
                {
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
            }
            else
            {
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
                    HandleStartFocusEnableDisable();
                }
                else
                {
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
                //Nothing to do
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

        private void btnSelectTasks_Click(object sender, RoutedEventArgs e)
        {
            WhatPlan.Visibility = Visibility.Collapsed;
            TxtIntention.Visibility = Visibility.Collapsed;
            Description.Visibility = Visibility.Collapsed;

            SelectTasks wndSelectTasks = new SelectTasks(this);
            wndSelectTasks.ShowDialog();

            try
            {
                if (appSettings.sRawTasks != null && appSettings.sRawTasks.Length > 0)
                {
                    FreeFormIntention.Visibility = Visibility.Collapsed;
                    SelectedTasksTitle.Visibility = Visibility.Visible;
                    ListTasks.Visibility = Visibility.Visible;

                    List<string> arrTaskNames = new List<string>();
                    JArray array = (JArray)JsonConvert.DeserializeObject(appSettings.sRawTasks);

                    for (int i = 0; i < array.Count(); i++)
                    {
                        JObject TodoItem = JObject.Parse(array[i].ToString());
                        string sName = TodoItem.ContainsKey("title") ? TodoItem["title"].ToString() : "";
                        arrTaskNames.Add(sName);
                    }
                    ListTasks.ItemsSource = arrTaskNames;
                }
                else
                {
                    FreeFormIntention.Visibility = Visibility.Visible;
                    SelectedTasksTitle.Visibility = Visibility.Collapsed;
                    ListTasks.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnSelectTasks_Click() sRawTasks Exception - " + ex.Message);
            }
        }

        private void btnWriteFreeFormIntention_Click(object sender, RoutedEventArgs e)
        {
            WhatPlan.Visibility = Visibility.Visible;
            TxtIntention.Visibility = Visibility.Visible;
            Description.Visibility = Visibility.Visible;
        }
    }
}
