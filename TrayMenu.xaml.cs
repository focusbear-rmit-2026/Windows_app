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
using Focus_Bear.Onboarding.AfterLogin;
using Focus_Bear.Preferences;

namespace Focus_Bear
{
    /// <summary>
    /// Interaction logic for TrayMenu.xaml
    /// </summary>
    public partial class TrayMenu : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        bool isFocusModeWindowOpen = false;
        
        DispatcherTimer dispatcherTimerFocus = new DispatcherTimer(DispatcherPriority.Normal);

      
        public TrayMenu()
        {
            InitializeComponent();

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
            try
            {
                Reactivate.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemReactivateTitle"].ToString();
                Preferences.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemPreferencesTitle"].ToString();
                PomodoroMode.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemStartPomodoroTitle"].ToString();
                Quit.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemQuitTitle"].ToString();
                UpgradeNow.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemUpgradeNowTitle"].ToString();
                Focus.Content = appSettings.ConfigFileStrings["menuInfo"]["menuItemFocusTitle"].ToString();
                btnResumeHabitsTxt.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemResumeHabitsTitle"].ToString();
                btnBrainDumpTXT.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemBrainDumpTitle"].ToString();

                btnSetupFocusModeText.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemSetFocusModesTitle"].ToString();

                if (appSettings.isOfficeModeEnabled)
                {
                    OfficeMode.IsChecked = true;
                }
                else
                {
                    WFHMode.IsChecked = true;
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

                if (appSettings.IsAppDeactivated())
                {
                    btnReactivate.Visibility = Visibility.Visible;
                    //set title to Quit
                    Quit.Text = appSettings.ConfigFileStrings["quitInfo"]["quittingConfirmInfo"]["buttonQuitText"].ToString();
                }
                else
                {
                    Quit.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemQuitTitle"].ToString();
                    btnReactivate.Visibility = Visibility.Collapsed;
                }

                OfficeMode.Content = appSettings.ConfigFileStrings["menuInfo"]["segmentOfficeModeText"].ToString();
                WFHMode.Content = appSettings.ConfigFileStrings["menuInfo"]["segmentWfhModeText"].ToString();
                OfficeMode.ToolTip = appSettings.ConfigFileStrings["menuInfo"]["toolTipOfficeMode"].ToString();
                WFHMode.ToolTip = appSettings.ConfigFileStrings["menuInfo"]["toolTipWfhMode"].ToString();

               
                if (appSettings.bHabitsPostponedState || appSettings.bFocusSummaryPostponedState)
                {
                    btnResumeHabits.Visibility = Visibility.Visible;
                }
                else
                {
                    btnResumeHabits.Visibility = Visibility.Collapsed;
                }

                if (RegistryManager.GetFocusModeSetupDone())
                {
                    btnSetupFocusMode.Visibility = Visibility.Collapsed;
                }
                if (appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
                {
                    if(!appSettings.isFocusModeRunning)
                        OrangeHeader.Height = 30;  
                    else
                        OrangeHeader.Height = 70;
                }

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("UpdateTrayMenu() + " + ex.Message);

            }
        }


        private void OfficeModeButton_Click(object sender, RoutedEventArgs e)
        {            
           
        }

        private void btnQuit_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnQuit_Click()");

            try
            {
                //Chck if the Focus mode is running, then we should terminate it
                //Call api top finish focus mode
                if (appSettings.isFocusModeRunning)
                {
                    appSettings.isFocusModeRunning = false;
                    ((App)Application.Current).CloseFocusProgressWindow();

                    FocusBearAPIClient client = new FocusBearAPIClient();
                    var xResponse = client.FinishFocusMode(appSettings.sRunningFocusModeID, "", "");
                }

                QuitWindow aa = new QuitWindow();
                aa.ShowDialog();
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

            //Restart the app.
            try
            {
                LogWriter.LogWrite("Restart app now for Reactivation to take Effect!");

                string currentAppPath = System.Reflection.Assembly.GetExecutingAssembly().Location;

                appSettings.bRestartAppNeeded = true;

                Application.Current.Shutdown(0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("btnReactivate_Click app restart= " + ex.Message);
            }
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

            if (appSettings.isExpired)
            {
                Hide();
                ((App)(Application.Current)).ShowExpiredNag();
                return;
            }
            Hide();

            appSettings.CapturePostHog("open-preferences");

            ((App)(Application.Current)).ShowPreferencesWindow();
            
        }

        private void btnStartPomodoroMode_Click(object sender, RoutedEventArgs e)
        {

            if (appSettings.isExpired)
            {
                Hide();
                ((App)(Application.Current)).ShowExpiredNag();
                return;
            }

            //todo Win text
            FBMessageBox ff = new FBMessageBox("With Pomodoro mode, you automatically cycle between focus sessions and breaks (e.g. 25 minutes on, 5 minutes off)", "We haven't built Pomodoro mode yet on Windows. How badly do you want this feature?", MessageBoxButton.YesNoCancel, "I need this!", "I probably wouldn't use it", "I might use it"  );
            ff.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ff.ShowDialog();
            if(ff.btnClicked == MessageBoxResult.Yes) // "I need this!"
            {
                appSettings.CapturePostHog("need-pomodoro");
            }
            else if (ff.btnClicked == MessageBoxResult.No) //"I probably wouldn't use it" 
            {
                appSettings.CapturePostHog("probably-wont-use-pomodoro");
            }
            else if (ff.btnClicked == MessageBoxResult.Cancel) //"I might use it"
            {
                appSettings.CapturePostHog("might-use-pomodoro");
            }

            Hide();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Hide(); 
        }

        private void ListFocusModes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LogWriter.LogWrite("ListFocusModes_SelectionChanged Tray Menu");
            if(appSettings.isExpired)
            {
                Hide();
                ((App)(Application.Current)).ShowExpiredNag();
                return;
            }
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

                        if(selectedItem.isRelaxFocus && appSettings.bPasswordRequireToUseRelaxFocusMode)
                        {
                            AskPassword pwdWnd = new AskPassword();
                            pwdWnd.Owner = this;
                            bool? bRes  = pwdWnd.ShowDialog();

                            if (bRes.Value)
                            {
                                //Password is correct
                            }
                            else
                            {
                                //Deselect the Seleted list view item
                                ListFocusModes.SelectedItem = null;
                                isFocusModeWindowOpen = false;
                                return;
                            }

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

                            ((App)Application.Current).ShowFocusProgressWindow();

                            if( ((App) Application.Current).wndBrainDump != null && ((App)Application.Current).wndBrainDump.IsVisible) //Update the window if its made or is visible
                            {
                                ((App)Application.Current).wndBrainDump.ShowBrainDumpWindowAndStartTimer();
                            }

                            //If is the loacked FOcus we need to add the active app and active url in allowed apps/urls
                            if (selectedItem.isLockedFocus)
                            {
                                selectedItem.isAllowedListApproach = true;
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

                            if (appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
                            {
                                OrangeHeader.Height = 70;
                            }

                            int nHours = int.Parse(sHours);
                            int nMinutes = int.Parse(sMinutes);
                            DateTime dTill = DateTime.Now;

                            appSettings.sRunningFocusModeStartTime = dTill.ToString("yyyy-MM-dd HH:mm:ss");
                            
                            dTill = dTill.AddHours(nHours);
                            dTill = dTill.AddMinutes(nMinutes);
                            string sTill = appSettings.ConfigFileStrings["menuInfo"]["untilTimeText"].ToString();
                            sTill = sTill.Replace("<TIME>", dTill.ToString());
                            OnUntill.Content = sTill;

                            appSettings.sRunningFocusModeID = selectedItem.id;
                            appSettings.sRunningFocusModeName = selectedItem.name;
                            appSettings.sRunningFocusModeEndTime = dTill.ToString();
                            appSettings.sRunningFocusModeIntention = sIntention;

                            //Clear any old stuff for gentle blocking
                            appSettings.TemporaryAllowedAppsGentleBlock.Clear();
                            appSettings.TemporarryBlockedAppsGentleBlock.Clear();
                            appSettings.TemporaryAllowedURLsGentleBlock.Clear();
                            appSettings.TemporarryBlockedURLsGentleBlock.Clear();

                            appSettings.TemporaryAllowedKeywords.Clear();
                            appSettings.TemporaryAllowedURLsTabTitles.Clear();
                            //Start the Focus Mode Timer to end after x hours / minutes
                            StartFocusModeTimer(nHours, nMinutes);

                            //Reposition 
                            SetTrayMenuPosition();

                            FocusBearAPIClient client = new FocusBearAPIClient();
                            var xResponse = client.StartFocusMode(selectedItem.id);

                        }
                        //Deselect the Seleted list view item
                        ListFocusModes.SelectedItem = null;

                    }
                    else
                    {
                        try
                        {

                            //Check if its the same focusmode that is running, then let's stop it.
                            FocusMode CurrentRunningFocusMode = listItems.First<FocusMode>();
                            if (CurrentRunningFocusMode.id == selectedItem.id)
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


                                //Clear the Gentle block temp arrays
                                appSettings.TemporarryBlockedAppsGentleBlock.Clear();
                                appSettings.TemporarryBlockedURLsGentleBlock.Clear();

                                //Make the global variable false
                                appSettings.isFocusModeRunning = false;

                                ((App)Application.Current).CloseFocusProgressWindow();

                                //Show and Set on untill value
                                OnUntill.Visibility = Visibility.Collapsed;
                                //Increase hearder height
                                OrangeHeader.Height = 25; //Originally 25
                                if (appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
                                {
                                    OrangeHeader.Height = 30; //Originally 25
                                }

                                //Deselect the Seleted list view item
                                ListFocusModes.SelectedItem = null;

                            }
                            else
                            {
                                FocusMode NewSelectedFocusMode = ListFocusModes.SelectedItem as FocusMode; 
                                FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["focusInfo"]["focusAlreadyRunningText"].ToString(), "Focus Bear",MessageBoxButton.YesNo, appSettings.ConfigFileStrings["common"]["buttonOkText"].ToString(), appSettings.ConfigFileStrings["focusInfo"]["startingFocusModeInfo"]["buttonStopFocusText"].ToString());
                                ff.ShowDialog();
                                if(ff.btnClicked == MessageBoxResult.No)
                                {
                                    //Stop the current focus mode and start new
                                    OnUntill.Content = "";
                                    CurrentRunningFocusMode.isOn = false;
                                    Sounds.PlayFocusModeEndSound();

                                    //Clear the Gentle block temp arrays
                                    appSettings.TemporarryBlockedAppsGentleBlock.Clear();
                                    appSettings.TemporarryBlockedURLsGentleBlock.Clear();

                                    //Make the global variable false
                                    appSettings.isFocusModeRunning = false;

                                    ((App)Application.Current).CloseFocusProgressWindow();

                                    //Show and Set on untill value
                                    OnUntill.Visibility = Visibility.Collapsed;
                                    //Increase hearder height
                                    OrangeHeader.Height = 25; //Originally 25
                                    if (appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
                                    {
                                        OrangeHeader.Height = 30; //Originally 25
                                    }

                                    //Now open the New selected Focus mode start window
                                    //Deselect the Seleted list view item
                                    ListFocusModes.SelectedItem = null;

                                    ListFocusModes.SelectedItem = NewSelectedFocusMode;
                                }
                                else
                                {
                                    //OK clicked, do nothing
                                    //Deselect the Seleted list view item
                                    ListFocusModes.SelectedItem = null;
                                }

                            }

                        }
                        catch (Exception ex)
                        {
                            LogWriter.LogWrite("ListFocusModes_SelectionChanged = " + ex.Message);
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ListFocusModes_SelectionChanged = " + ex.Message);
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

                        ((App)Application.Current).CloseFocusProgressWindow();

                        //Clear the Gentle block temp arrays
                        appSettings.TemporarryBlockedAppsGentleBlock.Clear();
                        appSettings.TemporarryBlockedURLsGentleBlock.Clear();

                        //Show and Set on untill value
                        OnUntill.Visibility = Visibility.Collapsed;
                        //Increase hearder height
                        OrangeHeader.Height = 25; //Originally 25
                        if (appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
                        {
                            OrangeHeader.Height = 30; //Originally 25
                        }
                        //Reposition 
                        SetTrayMenuPosition();

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

        private void btnResumeHabits_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnResumeHabits_Click" );
            Hide();
            if (appSettings.bFocusSummaryPostponedState)
            {
                ((App)(Application.Current)).wndFocusSummary.ResumeFocusSummary();
            }
            else
            {
                ((App)(Application.Current)).ResumeAfterPostponeFinished();
            }
        }

        private void btnBrainDump_Click(object sender, RoutedEventArgs e)
        {

            if (appSettings.isExpired)
            {
                Hide();
                ((App)(Application.Current)).ShowExpiredNag();
                return;
            }

            ((App)(Application.Current)).ShowBrainDumpWindow();

            Hide();
 
        }

        private void btnSetupFocusMode_Click(object sender, RoutedEventArgs e)
        {
            OnboardingSetupAndTipsWindow windOnBoardingNotes1 = new OnboardingSetupAndTipsWindow();
            windOnBoardingNotes1.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            windOnBoardingNotes1.Topmost = true;
            windOnBoardingNotes1.ShowDialog();
            windOnBoardingNotes1.Activate();
            windOnBoardingNotes1.Focus();
        }

        private void WFHMode_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.isOfficeModeEnabled = false;
            //Save thelocal settings and upload to server
            LocalSettingsManager.PutLocalSettings();
        }

        private void OfficeMode_Checked(object sender, RoutedEventArgs e)
        {
            if (OfficeMode.IsChecked.Value) //means officemode enabled
            {
                //if office friendly activity added then show alert
                var listItems = appSettings.arrAllbreakActivities.Where(i => i.isOfficeFriendly == true);
                if (listItems.Count() == 0)
                {
                    //No office friendly activity is there
                    FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["menuInfo"]["noOfficeFriendlyActivityAlertText"].ToString(), "", MessageBoxButton.YesNo, appSettings.ConfigFileStrings["menuInfo"]["noOfficeFriendlyActivityButtonFirstText"].ToString(), appSettings.ConfigFileStrings["menuInfo"]["noOfficeFriendlyActivityButtonSecondText"].ToString());
                    ff.ShowDialog();
                    if (ff.btnClicked == MessageBoxResult.Yes)
                    {
                        appSettings.isOfficeModeEnabled = true;

                        //create an office friendly activity
                        string ActivityID = System.Guid.NewGuid().ToString();
                        string SequenceID = System.Guid.NewGuid().ToString();
                        if (appSettings.arrAllbreakActivities.Count() > 0)
                        {
                            SequenceID = appSettings.arrAllbreakActivities[0].activity_sequence_id;
                        }
                        //add to our array
                        appSettings.arrAllbreakActivities.Add(new breakActivity(ActivityID, "Stand up and stretch", 20, new List<string>(), false, "", false, new List<string>(), new List<string>(), "", SequenceID, false, false, new List<activityChoice>(), true, "", new List<QuantityQuestion>(), "", true));

                        //add to the json
                        String jsonString = appSettings.sRawUserSettings;

                        JObject UserSettings = JObject.Parse(appSettings.sRawUserSettings);

                        if (UserSettings.ContainsKey("break_activities"))
                        {
                            JArray break_activities_Array = (JArray)UserSettings.GetValue("break_activities");
                            JObject newActivity = new JObject(
                               new JProperty("id", ActivityID),
                               new JProperty("name", appSettings.ConfigFileStrings["common"]["officeFriendlyActivityName"].ToString()), //"Stand up and stretch"
                               new JProperty("duration_seconds", 20),
                               new JProperty("is_office_friendly", true),
                               new JProperty("activity_sequence_id", SequenceID));
                            break_activities_Array.Add(newActivity);

                            UserSettings.Remove("break_activities");
                            UserSettings.Add("break_activities", break_activities_Array);

                            //Also save the udpated settings to Registry
                            appSettings.sRawUserSettings = UserSettings.ToString();
                            RegistryManager.SaveUserSettings();

                            //We will need to put the settings.
                            FocusBearAPIClient client = new FocusBearAPIClient();
                            var xResponse = client.PutUserSettings();

                            FBMessageBox ff1 = new FBMessageBox(appSettings.ConfigFileStrings["menuInfo"]["officeModeEnabledSuccessfullyText"].ToString());
                            ff1.ShowDialog();
                        }
                    }
                    else
                    {
                        appSettings.isOfficeModeEnabled = false;
                        WFHMode.IsChecked = true;
                    }
                }
                else
                {
                    if (!RegistryManager.GetOfficeModeMessageStatus())
                    {
                        //Office friendly activity is there
                        FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["menuInfo"]["officeModeEnabledSuccessfullyText"].ToString());
                        ff.ShowDialog();
                        RegistryManager.SetOfficeModeMessageStatus();
                    }
                    appSettings.isOfficeModeEnabled = true;
                }
            }
            else
            {
                appSettings.isOfficeModeEnabled = false;
                WFHMode.IsChecked = true;
            }
                       
            //Save thelocal settings and upload to server
            LocalSettingsManager.PutLocalSettings();
        }
    }
}
