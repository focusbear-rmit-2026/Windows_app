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

namespace Focus_Bear
{
    /// <summary>
    /// Interaction logic for TrayMenu.xaml
    /// </summary>
    public partial class TrayMenu : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        bool isFocusModeWindowOpen = false;
        
        DispatcherTimer dispatcherTimerFocus = new DispatcherTimer();

      
        public TrayMenu()
        {
            InitializeComponent();
            InitializeSettings();
            SetTrayMenuPosition();
            ShowInTaskbar = false;
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
            this.Left = (desktopWorkingArea.Right - this.Width);
            this.Top = (desktopWorkingArea.Bottom - this.ActualHeight);

        }

        private void InitializeSettings()
        {

            ListFocusModes.ItemsSource = appSettings.arrFocusModes;

            /*"menuInfo":{
             *  "menuItemReactivateTitle":"Reactivate",

             "menuItemPreferencesTitle":"Preferences",
             "menuItemFocusTitle":"Focus",
              "menuItemHardcorePomodoroModeTitle":"Super Pomodoro Mode",
        "menuItemStartPomodoroTitle":"Start Pomodoro Mode",
        "menuItemStopHardcorePomodoroTitle":"Stop Super Pomodoro Mode",
         "menuItemEnableOfficeModeTitle":"Office Mode Enabled",
        "menuItemDisableOfficeModeTitle": "WFH Mode Enabled",
        "menuItemQuitTitle":"Quit",
         "menuItemUpgradeNowTitle":"Upgrade Now",
        "subscriptionExpiredText":"Your subscription has expired",

            *  "common":{
            *          "daysRemainingText":"Trial version - <DAYS> of 31 trial days remaining",
            *           "trialExpiredText":"Your trial has expired!",
        * */

            Reactivate.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemReactivateTitle"].ToString();
            Preferences.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemPreferencesTitle"].ToString();
            lblOfficeMode.Content = appSettings.ConfigFileStrings["menuInfo"]["menuItemEnableOfficeModeTitle"].ToString();
            PomodoroMode.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemStartPomodoroTitle"].ToString();
            Quit.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemQuitTitle"].ToString();
            UpgradeNow.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemUpgradeNowTitle"].ToString();
            Focus.Content = appSettings.ConfigFileStrings["menuInfo"]["menuItemFocusTitle"].ToString();

            UpdateTrayMenu();

        }

        public void UpdateTrayMenu()
        {
            LogWriter.LogWrite("UpdateTrayMenu()");

            try
            {
                if (appSettings.isExpired)
                {
                    TrialInfo.Text = appSettings.ConfigFileStrings["menuInfo"]["subscriptionExpiredText"].ToString();
                }
                else
                {
                    string sTrial = appSettings.ConfigFileStrings["common"]["daysRemainingText"].ToString();

                    TrialInfo.Text = sTrial.Replace("<DAYS>", appSettings.nTrialDays.ToString()); ;
                }

                if (appSettings.isPurchased)
                {
                    //hide the upgrade now button
                    btnUpgradeNow.Visibility = Visibility.Collapsed;
                }

                if (appSettings.IsAppDeactivated())
                {
                    btnReactivate.Visibility = Visibility.Visible;
                }
                else
                {
                    btnReactivate.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("UpdateTrayMenu() + " + ex.Message);

            }
        }


        private void OfficeModeButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Work In Progress!", "WIP", MessageBoxButton.OK, MessageBoxImage.Error);
           // Close();
        }

        private void btnQuit_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnQuit_Click()");

            try
            {
                // DisposeTrayIcon();

                //Chck if the Focus mode is running, then we should terminate it
                //Call api top finish focus mode
                if (appSettings.isFocusModeRunning)
                {
                    appSettings.isFocusModeRunning = false;
                    FocusBearAPIClient client = new FocusBearAPIClient();
                    var x = client.FinishFocusMode(appSettings.sRunningFocusModeID, "", "");
                }


                // isClosed = true;
                //mainWin.ShouldShutDown = true; // App should show BeforeClosing Nag
                //Application.Current.Shutdown(0);
                Close();
                // mainWin.ShouldShutDown = true;
                // mainWin.BringToTopAndClose();
                Application.Current.Shutdown(0);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnQuit_Click - " + ex.Message);
   
            }
        }

        private void btnReactivate_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnReactivate_Click() Tray Menu");

            RegistryManager.RemoveAppDeactivatedTillDate();

            Hide();
        }

        private void btnUpgradeNow_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnUpgradeNow_Click() Tray Menu");

            Hide();
            ((App)(Application.Current)).ShowExpiredNag();
            
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnSettings_Click() Tray Menu");
            //MessageBox.Show("Work In Progress!", "WIP", MessageBoxButton.OK, MessageBoxImage.Error);
            Hide();

            appSettings.CapturePostHog("open-preferences");

            ((App)(Application.Current)).ShowPreferencesWindow();
            
        }

        private void btnStartPomodoroMode_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Work In Progress!", "WIP", MessageBoxButton.OK, MessageBoxImage.Error);
            Hide();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Hide(); 
        }

        private void ListFocusModes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LogWriter.LogWrite("ListFocusModes_SelectionChanged Tray Menu");
            try
            {
                var selectedItem = ListFocusModes.SelectedItem as FocusMode;

                if (selectedItem != null)
                {
                    Hide(); 
                    //Check is any other FOcus modes is running, if yes, give an alert to stop that mode
                    //If not we will start the focus mode
                    //First check if it is already added
                    var listItems = appSettings.arrFocusModes.Where(i => i.isOn == true);
                    if (listItems.Count() == 0)
                    {
                        if (isFocusModeWindowOpen)
                            return;
                        isFocusModeWindowOpen = true;

                        //Check here if the Breaks window is visible, we will hide it.
                        if (((App)(Application.Current)).wndTakeABreak != null && ((App)(Application.Current)).wndTakeABreak.IsVisible)
                        {
                            ((App)(Application.Current)).wndTakeABreak.HideBreakWindowAndStopTimer();
                        }

                        //show the start focus window
                        StartFocusMode wndFocus = new StartFocusMode(selectedItem.name, selectedItem.isDefault, selectedItem.isLockedFocus);
                        wndFocus.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                        wndFocus.Topmost = true;

                        bool? bResult = wndFocus.ShowDialog();

                        isFocusModeWindowOpen = false;

                        string sIntention = wndFocus.sIntention;
                        string sHours = wndFocus.sHours;
                        string sMinutes = wndFocus.sMinutes;

                        if (bResult.Value == true)
                        {
                            Sounds.PlayFocusModeStartSound();
                            //Start the Focus Mode
                            //Change the play icon for this to STOP icon
                            selectedItem.isOn = true;

                            //Make the global variable true
                            appSettings.isFocusModeRunning = true;

                            //If is the loacked FOcus we need to add the active app and active url in allowed apps/urls
                            if (selectedItem.isLockedFocus)
                            {
                                selectedItem.isAllowedListApproach = true;
                                selectedItem.isAllowedAllUrls = false;

                                selectedItem.isAllowedAllApps = false;
                                selectedItem.arrAllowedAppsWin.Clear();//Remove old
                                selectedItem.arrAllowedAppsWin.Add(new AppDetails(appSettings.sLastActiveAppName, appSettings.sLastActiveAppName) ); //Add new

                                selectedItem.arrAllowedUrls.Clear();//Remove old
                                selectedItem.arrAllowedUrls.Add(appSettings.sLastActiveURL); //Add new
                            }

                            //Show and Set on untill value
                            OnUntill.Visibility = Visibility.Visible;
                            //Increase hearder height
                            OrangeHeader.Height = 45; //Originally 25

                            int nHours = int.Parse(sHours);
                            int nMinutes = int.Parse(sMinutes);
                            DateTime dTill = DateTime.Now;

                            appSettings.sRunningFocusModeStartTime = dTill.ToString();
                            
                            dTill = dTill.AddHours(nHours);
                            dTill = dTill.AddMinutes(nMinutes);
                            /* "menuInfo":{
                               "untilTimeText":"On Until <TIME>",*/
                            string sTill = appSettings.ConfigFileStrings["menuInfo"]["untilTimeText"].ToString();
                            sTill = sTill.Replace("<TIME>", dTill.ToString());
                            OnUntill.Content = sTill;

                            appSettings.sRunningFocusModeID = selectedItem.id;
                            appSettings.sRunningFocusModeName = selectedItem.name;
                            appSettings.sRunningFocusModeEndTime = dTill.ToString();
                            appSettings.sRunningFocusModeIntention = sIntention;
                            
                            //Start the Focus Mode Timer to end after x hours / minutes
                            StartFocusModeTimer(nHours, nMinutes);

                            //Reposition 
                            SetTrayMenuPosition();

                            FocusBearAPIClient client = new FocusBearAPIClient();
                            var x = client.StartFocusMode(selectedItem.id);

                        }
                        //Deselect the Seleted list view item
                        ListFocusModes.SelectedItem = null;

                    }
                    else
                    {
                        try
                        {

                            //Check if its the same focusmode that is running, then let's stop it.
                            if(listItems.First<FocusMode>().id == selectedItem.id)
                            {
                                //Stop the focus mode
                                //Show WIndow here
                                StopFocusMode ss = new StopFocusMode();
                                bool? bResult = ss.ShowDialog();
                                if (bResult.Value == false)
                                {
                                    //Cancel CLicked, nothing to do
                                    //Deselect the Seleted list view item
                                    ListFocusModes.SelectedItem = null;
                                    return;
                                }
                              
                                OnUntill.Content = "";
                                selectedItem.isOn = false;
                                Sounds.PlayFocusModeEndSound();

                                //Make the global variable false
                                appSettings.isFocusModeRunning = false;

                                //Show and Set on untill value
                                OnUntill.Visibility = Visibility.Collapsed;
                                //Increase hearder height
                                OrangeHeader.Height = 25; //Originally 25

                                //Deselect the Seleted list view item
                                ListFocusModes.SelectedItem = null;

                            }
                            else
                            {
                                //Deselect the Seleted list view item
                                ListFocusModes.SelectedItem = null;

                                /*"focusInfo":{
                                "focusAlreadyRunningText":"A focus mode is already running. Please deactivate the running focus mode to start a new one.",*/
                                MessageBox.Show(appSettings.ConfigFileStrings["focusInfo"]["focusAlreadyRunningText"].ToString(), "Focus Bear", MessageBoxButton.OK, MessageBoxImage.Error);
                            }

                        }
                        catch (Exception ex)
                        {
                            LogWriter.LogWrite("ListFocusModes_SelectionChanged = " + ex.Message);
                            //System.Diagnostics.Debug.WriteLine(ex.Message);
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ListFocusModes_SelectionChanged = " + ex.Message);
                //System.Diagnostics.Debug.WriteLine(ex.Message);
            }

        }

        public void StartFocusModeTimer(int nHours, int nMinutes)
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
                //Debug.WriteLine(ex.Message);
            }

            try
            {
                dispatcherTimerFocus.Tick += new EventHandler(dispatcherTimer_Focus);
           
#if DEBUG
                dispatcherTimerFocus.Interval = new TimeSpan(nHours, nMinutes, 0);
#else
                 dispatcherTimerFocus.Interval = new TimeSpan(nHours, nMinutes, 0);
#endif

                dispatcherTimerFocus.Start();
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StartFocusModeTimer 2 - " + ex.Message);
                //Debug.WriteLine(ex.Message);
            }

        }

        private void dispatcherTimer_Focus(object sender, EventArgs e)
        {
            LogWriter.LogWrite("dispatcherTimer_Focus()");
            try
            {
                dispatcherTimerFocus.Stop(); //Tick Once only

                CommandManager.InvalidateRequerySuggested();

                LogWriter.LogWrite("Stop the Focus Mode");

                var listItems = appSettings.arrFocusModes.Where(i => i.isOn == true);
                if (listItems.Count() != 0)
                {
                    try
                    {
                        Sounds.PlayFocusModeEndSound();
                        //Stop the focus mode
                        //Show WIndow here
                        OnUntill.Content = "";
                        listItems.First<FocusMode>().isOn = false;

                        //Make the global variable false
                        appSettings.isFocusModeRunning = false;

                        //Show and Set on untill value
                        OnUntill.Visibility = Visibility.Collapsed;
                        //Increase hearder height
                        OrangeHeader.Height = 25; //Originally 25

                        //Reposition 
                        SetTrayMenuPosition();

                        //Show the Focusmode end dialog
                        ((App)(Application.Current)).ShowFocusEndWindow();

                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("dispatcherTimer_Focus" + ex.Message);
                        //Debug.WriteLine(ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("dispatcherTimer_Focus" + ex.Message);
               // Debug.WriteLine(ex.Message);
            }
        }
    }
}
