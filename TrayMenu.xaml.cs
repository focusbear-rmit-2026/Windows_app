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
using Focus_Bear.Onboarding.AfterLogin;
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

        bool isFocusModeWindowOpen = false;
        

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
        }

        private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            TrayContextMenu.PlacementTarget = this;
            TrayContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            TrayContextMenu.IsOpen = true;
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("TrayMenu Exit clicked via right-click menu.");
            System.Windows.Application.Current.Shutdown(); // Clean shutdown
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
                btnStopPomodoroModetxt.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemStopHardcorePomodoroTitle"].ToString();
                Quit.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemQuitTitle"].ToString();
                UpgradeNow.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemUpgradeNowTitle"].ToString();
                Focus.Content = appSettings.ConfigFileStrings["menuInfo"]["menuItemFocusTitle"].ToString();
                btnResumeHabitsTxt.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemResumeHabitsTitle"].ToString();

                Tools.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemToolsTitle"].ToString();
                btnBrainDump.ToolTip = appSettings.ConfigFileStrings["menuInfo"]["menuItemBrainDumpTitle"].ToString();
                btnToDoPlayer.ToolTip = appSettings.ConfigFileStrings["menuInfo"]["menuItemToDoPlayerTitle"].ToString();
                btnFocusMusic.ToolTip = appSettings.ConfigFileStrings["menuInfo"]["menuItemFocusMusicTitle"].ToString();

                btnSetupFocusModeText.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemSetFocusModesTitle"].ToString();

                LimitedBlocking.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemBlockingDisabledTitle"].ToString();
                LimitedBlockingInfo.ToolTip = appSettings.ConfigFileStrings["menuInfo"]["addBlockingScheduleInfo"].ToString();
                LimitedBlockingEdit.ToolTip = appSettings.ConfigFileStrings["menuInfo"]["toolTipButtonEditBlockingSchedule"].ToString();

                BlockingScheduleText.Text = appSettings.ConfigFileStrings["menuInfo"]["titleBlockingSchedule"].ToString();

                if (appSettings.isOfficeModeEnabled)
                {
                    OfficeMode.IsChecked = true;
                }
                else
                {
                    WFHMode.IsChecked = true;
                }

                if (appSettings.bIsGeekModeEnabled)
                {
                    GeekMode.IsChecked = true;
                }
                else
                {
                    SimpleMode.IsChecked = true;
                }

                if (appSettings.sTheme == "Light" && appSettings.bAutoTheme == false)
                {
                    ((App)Application.Current).ChangeTheme("Light");
                }
                else if (appSettings.sTheme == "Dark" && appSettings.bAutoTheme == false)
                {
                    ((App)Application.Current).ChangeTheme("Dark");
                }

                GeekMode.Checked += GeekMode_Checked;
                SimpleMode.Checked += SimpleMode_Checked;

                GeekMode.Content = appSettings.ConfigFileStrings["geekModeInfo"]["titleGeekMode"].ToString();
                SimpleMode.Content = appSettings.ConfigFileStrings["geekModeInfo"]["titleSimpleMode"].ToString();
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
                if(appSettings.arrFocusMusicTracks.Count > 0)
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
                        }
                        else if (current.IsPaused)
                        {
                            string imagePath = ("pack://application:,,,/Focus Bear;component/images/play_icon.png");
                            var image = new BitmapImage();
                            image.BeginInit();
                            image.UriSource = new Uri(imagePath);
                            image.EndInit();

                            PlayPauseButtonImage.Source = image;
                        }
                    }
                    else
                    {
                        btnPlayPauseMusic.Visibility = Visibility.Collapsed;
                    }
                       
                }

                if(appSettings.arrCustomBlockingSchedules.Count == 0)
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

                GeekMode.Content = appSettings.ConfigFileStrings["geekModeInfo"]["titleGeekMode"].ToString();
                GeekMode.ToolTip = appSettings.ConfigFileStrings["geekModeInfo"]["tooltipGeekMode"].ToString();
                SimpleMode.Content = appSettings.ConfigFileStrings["geekModeInfo"]["titleSimpleMode"].ToString();
                SimpleMode.ToolTip = appSettings.ConfigFileStrings["geekModeInfo"]["tooltipSimpleMode"].ToString();


                if (appSettings.bHabitsPostponedState || appSettings.bFocusSummaryPostponedState)
                {
                    btnResumeHabits.Visibility = Visibility.Visible;
                }
                else
                {
                    btnResumeHabits.Visibility = Visibility.Collapsed;
                }

                GeekMode.Checked -= GeekMode_Checked;
                SimpleMode.Checked -= SimpleMode_Checked;
                if (appSettings.bIsGeekModeEnabled)
                {
                    GeekMode.IsChecked = true;
                }
                else
                {
                    SimpleMode.IsChecked = true;
                }
                GeekMode.Checked += GeekMode_Checked;
                SimpleMode.Checked += SimpleMode_Checked;

                btnSetupFocusMode.Visibility = Visibility.Collapsed; //hide in simple mode

                if (appSettings.bIsGeekModeEnabled) //GEEK MODE ENABLED
                {
                    ///FocusSection.Visibility = Visibility.Visible;

                    if (RegistryManager.GetFocusModeSetupDone())
                    {
                        btnSetupFocusMode.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        btnSetupFocusMode.Visibility = Visibility.Visible;
                    }
                    if (appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
                    {
                        if (!appSettings.isFocusModeRunning)
                            OrangeHeader.Height = 30;
                        else
                            OrangeHeader.Height = 70;
                    }

                    /**if (appSettings.bUseSuperPomodoroMode)
                    {
                        Focus.Content = appSettings.ConfigFileStrings["menuInfo"]["menuItemHardcorePomodoroModeTitle"].ToString();
                    }
                    else
                    {
                        Focus.Content = appSettings.ConfigFileStrings["menuInfo"]["menuItemFocusTitle"].ToString();
                    }**/

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
                            btnStopPomodoroModetxt.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemStopPomodoroTitle"].ToString(); //todo win texts
                        }
                    }
                    else
                    {
                        //if focus session is not running, possible a super pomodoro break time
                        if(appSettings.bUseSuperPomodoroMode)
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
                    OfficeModeSection.Visibility = Visibility.Visible;

                    //Focus Alignment score
                    if (appSettings.isFocusModeRunning || 
                        (appSettings.isBlockingScheduleRunning && !appSettings.ActiveSchedule.isPaused && appSettings.ActiveSchedule.bAIBlockingEnabled))
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
                    ///FocusSection.Visibility = Visibility.Collapsed;
                    btnBrainDump.Visibility = Visibility.Collapsed;

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
                            btnStopPomodoroModetxt.Text =  btnStopPomodoroModetxt.Text =  btnStopPomodoroModetxt.Text = "Stop Focus Session"; //todo win texts
                        }
                    }
                    else
                    {
                        btnStartPomodoroMode.Visibility = Visibility.Visible;
                        btnStopPomodoroMode.Visibility = Visibility.Collapsed;
                    }
                    if (((appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS ||
                       appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS ||
                       appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY) &&
                       !appSettings.bHabitsPostponedState))
                    {
                        btnStartPomodoroMode.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        if (!appSettings.isFocusModeRunning)
                        {
                            btnStartPomodoroMode.Visibility = Visibility.Visible; //both start and stop
                        }
                    }

                    OfficeModeSection.Visibility = Visibility.Collapsed;

                    AlignmentScoreText.Visibility = Visibility.Collapsed;
                }

                if (appSettings.isFocusModeRunning)
                {
                    btnToDoPlayer.ToolTip = appSettings.ConfigFileStrings["menuInfo"]["menuItemToDoPlayerTitle"].ToString();
                }
                else
                {
                    btnToDoPlayer.ToolTip = appSettings.ConfigFileStrings["menuInfo"]["menuItemToDoListTitle"].ToString();
                }

                if(appSettings.isBlockingScheduleRunning)
                {
                    BlockingScheduleSection.Visibility = Visibility.Visible;
                    BlockingScheduleName.Text = appSettings.ActiveSchedule.name;
                    BlockingScheduleTime.Text = appSettings.ActiveSchedule.BlockingScheduleDaysTime;
                    if(appSettings.ActiveSchedule.isPaused)
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
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("UpdateTrayMenu() + " + ex.Message);
            }

            if (appSettings.bMenuOpeningFirstTime)
            {
                //when the user is opening menu for the first time we need to highlight the start piomodoro button and show a tooltip
                appSettings.bMenuOpeningFirstTime = false;
                FlashStartPomodoroButton();
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

        private async void btnQuit_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnQuit_Click()");
            if(bQuitVisible)
            {
                return;
            }
            try
            {
                //Chck if the Focus mode is running, then we should terminate it
                //Call api top finish focus mode
                if (appSettings.isFocusModeRunning)
                {
                    // Sync the todo tasks first
                    try
                    { 
                        await((App)Application.Current).SyncTodoTasksFromPlayer(); 
                    }
                    catch (Exception ex)
                    {                        
                         LogWriter.LogWrite("btnQuit_Click sync: " + ex.Message); 
                    }

                    appSettings.isFocusModeRunning = false;
                    ((App)Application.Current).CloseFocusProgressWindow();
                    ((App)Application.Current).HideToDoPlayer();

                    FocusBearAPIClient client = new FocusBearAPIClient();
                    var xResponse = client.FinishFocusMode(appSettings.sRunningFocusModeID, "", "");
                }
                
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
            ((App)(Application.Current)).ShowExpiredNag(); //The old upgrade now nag
            
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnSettings_Click() Tray Menu");

            //if (appSettings.isExpired)
            //{
            //    Hide();
            //    ((App)(Application.Current)).ShowExpiredNag();
            //    return;
            //}
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

            Hide();

            if (appSettings.isStartPomodoroWindowOpen || isFocusModeWindowOpen || appSettings.isFocusModeRunning)
            {
                LogWriter.LogWrite("isStartPomodoroWindowOpen || isFocusModeWindowOpen || appSettings.isFocusModeRunning - return");
                return;
            }

            if ((appSettings.isFocusModeRunning ||
               appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS ||
               appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS ||
               appSettings.nCurrentActivityType == CurrentActivityType.SHUT_OFF ||
               appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY ) &&
               (!appSettings.bHabitsPostponedState) && (!appSettings.bFocusSummaryPostponedState))
            {
                //do not start if some habits or breaks are running. Case is when 5 mins break ends, we start the focus session back but if habits have resumed by then, then do not show it
                return;
            }

            if (appSettings.bIsGeekModeEnabled)
            {
                ((App)(Application.Current)).ShowPomodoroWindow();
            }
            else
            {
                ((App)(Application.Current)).ShowSimplePomodoroWindow();
            }
            return;

        }

        private async void btnStopPomodoroMode_Click(object sender, RoutedEventArgs e)
        {
            
            //stop the current running focus session

            LogWriter.LogWrite("btnStopPomodoroMode_Click - button Stop FOcus Session Clicked");
   
            try
            {
                
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

                            if (((App)(Application.Current)).wndStartPomodoro != null)
                            {
                                ((App)(Application.Current)).wndStartPomodoro.Hide();
                                appSettings.isStartPomodoroWindowOpen = false;
                            }
                            //Stop the focus mode
                            //Show WIndow here - Removed 3 oct 2025 

                            //Show the Focusmode end dialog
                            ((App)(Application.Current)).ShowFocusEndWindow();

                            OnUntill.Content = "";
                            CurrentRunningFocusMode.isOn = false;
                            Sounds.PlayFocusModeEndSound();


                            if (!appSettings.bIsGeekModeEnabled)//Simple mode
                            {
                                appSettings.bAiCheckUrlEnabled = appSettings.bAiCheckUrlEnabledLastValueBeforeSimplePomodoro; //resetting the AI value
                            }

                            FocusAlignmentScoreManager.SetAppIconForFocusModeScore(null);
                            FocusAlignmentScoreManager.DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus.Clear();

                            //Turn off super pomodoro too as user have manually stopped
                            appSettings.bUseSuperPomodoroMode = false;


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

                            ((App)(Application.Current)).HideToDoPlayer();
                            //Deselect the Seleted list view item
                            //ListFocusModes.SelectedItem = null;
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
        private void Window_Deactivated(object sender, EventArgs e)
        {
            Hide(); 
        }

        private void ListFocusModes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //Code not used anymore as list of focus mode is not shown anymore
            return;
            

            LogWriter.LogWrite("ListFocusModes_SelectionChanged Tray Menu");
            //if(appSettings.isExpired)
            //{
            //    Hide();
            //    ((App)(Application.Current)).ShowExpiredNag();
            //    return;
            //}

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
                        if (appSettings.isStartPomodoroWindowOpen || isFocusModeWindowOpen || appSettings.bUseSuperPomodoroMode)
                        {
                            ListFocusModes.SelectedItem = null;
                            return;
                        }

                        if ((appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS ||
                               appSettings.nCurrentActivityType == CurrentActivityType.SHUT_OFF ||
                               appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS ||
                               appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY) &&
                               !appSettings.bHabitsPostponedState && !appSettings.bFocusSummaryPostponedState)
                        {
                            LogWriter.LogWrite("A routine is ongoing, cannot start focus mode");
                            ListFocusModes.SelectedItem = null;
                            FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["focusInfo"]["startingFocusModeInfo"]["activityRunningCantStartFocusText"].ToString(), "Focus Bear");
                            ff.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                            ff.ShowDialog();
                            return;
                        }

                        isFocusModeWindowOpen = true;

                        //Check here if the Breaks window is visible, we will hide it.
                        if (((App)(Application.Current)).wndTakeABreak != null && ((App)(Application.Current)).wndTakeABreak.IsVisible)
                        {
                            ((App)(Application.Current)).wndTakeABreak.Close();
                            ((App)(Application.Current)).wndTakeABreak = null;
                        }

                        if(selectedItem.isRelaxFocus && (appSettings.isPasswordSet && appSettings.bPasswordRequireToUseRelaxFocusMode))
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

                        appSettings.sRawTasks = ""; //reset this as there may be something selected from old focus mode 

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
                                if (((App)(Application.Current)).wndStartPomodoro != null)
                                {
                                    ((App)(Application.Current)).wndStartPomodoro.Hide();
                                    appSettings.isStartPomodoroWindowOpen = false;
                                }
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

                                FocusAlignmentScoreManager.SetAppIconForFocusModeScore(null);
                                FocusAlignmentScoreManager.DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus.Clear();

                                //Turn off super pomodoro too as user have manually stopped
                                appSettings.bUseSuperPomodoroMode = false;
                                

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

                                    FocusAlignmentScoreManager.SetAppIconForFocusModeScore(null);
                                    FocusAlignmentScoreManager.DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus.Clear();

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
                                    ListFocusModes.SelectedItem = CurrentRunningFocusMode.id;
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

                //Start the todo player
                ((App)(Application.Current)).ShowToDoPlayer();
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

                        FocusAlignmentScoreManager.SetAppIconForFocusModeScore(null);
                        FocusAlignmentScoreManager.DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus.Clear();

                        //Stop the focus mode
                        //Show WIndow here
                        OnUntill.Content = "";
                        listItems.First<FocusMode>().isOn = false;

                        if(appSettings.bUseSuperPomodoroMode)
                        {
                            appSettings.nSuperPomodoroCountCompleted++;
                        }
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

                        ((App)(Application.Current)).HideToDoPlayer();

                        if (appSettings.bShowFocusModeEnd) //Only if the user have selected the setting to show Focus mode end window
                        {
                            //Show the Focusmode end dialog
                            ((App)(Application.Current)).ShowFocusEndWindow();
                        }
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

            //if (appSettings.isExpired)
            //{
            //    Hide();
            //    ((App)(Application.Current)).ShowExpiredNag();
            //    return;
            //}

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

        private void GeekMode_Checked(object sender, RoutedEventArgs e)
        {
            if(appSettings.isExpired)
            {
                ((App)(Application.Current)).ShowUpgradeToProNag(appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["titleNoGeekMode"].ToString());
                return;
            }
            if (!appSettings.bDontShowModeChangeFB)
            {
                MessageBoxButton mb = new MessageBoxButton();
                FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["geekModeInfo"]["alertGeekModeEnabled"].ToString(), "", mb, "", "", "", true, "Don't show this again thanks.",false);
                ff.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                ff.ShowDialog();
            }
            appSettings.bIsGeekModeEnabled = true;
            if(((App)(Application.Current)).wndPreferences != null)
            {
                ((App)(Application.Current)).wndPreferences.PageSettings.SetGeekModeStatus();

            }
            appSettings.CapturePostHog("switch-to-geek-mode");
            UpdateTrayMenu();
            SetTrayMenuPosition();
        }

        private void SimpleMode_Checked(object sender, RoutedEventArgs e)
        {
            MessageBoxButton mb = new MessageBoxButton();
            if (!appSettings.bDontShowModeChangeFB)
            {
                FBMessageBox ff1 = new FBMessageBox(appSettings.ConfigFileStrings["geekModeInfo"]["alertSimpleModeEnabled"].ToString(), "", mb, "", "", "", true, "Don't show this again thanks.", false);
                ff1.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                ff1.ShowDialog();
            }
            appSettings.bIsGeekModeEnabled = false;
            if (((App)(Application.Current)).wndPreferences != null)
            {
                ((App)(Application.Current)).wndPreferences.PageSettings.SetGeekModeStatus();

            }
            appSettings.CapturePostHog("switch-to-simple-mode");
            UpdateTrayMenu();
            SetTrayMenuPosition();
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
            ((App)(Application.Current)).ShowPreferencesWindow();
            ((App)(Application.Current)).wndPreferences.ShowBlocksTab();
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
            LogWriter.LogWrite("BlockingSchedule Paused from menu");
            if (appSettings.ActiveSchedule != null)
            {
                appSettings.ActiveSchedule.isPaused = false;
                BlockingScheduleState.Text = appSettings.ConfigFileStrings["menuInfo"]["stateActive"].ToString(); //"Active";
            }
        }

        private void btnPauseBlockingSchedule_Unchecked(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("BlockingSchedule Reactiveted from menu");
            if (appSettings.ActiveSchedule != null)
            {
                if (appSettings.ActiveSchedule.BlockLevel == FOCUSMODESTRICTNESS.CHILL_MODE)
                {
                    appSettings.ActiveSchedule.isPaused = true;

                    BlockingScheduleState.Text = appSettings.ConfigFileStrings["menuInfo"]["statePaused"].ToString(); //"Paused"; 
                }
                else if (appSettings.ActiveSchedule.BlockLevel == FOCUSMODESTRICTNESS.STRICT)
                {
                    //Timer
                    BlockingScheduleStrictTimer mwndTimer = new BlockingScheduleStrictTimer();
                    mwndTimer.Topmost = true;
                    mwndTimer.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    bool? dialogResult = mwndTimer.ShowDialog();

                    if (dialogResult.Value == true)
                    {
                        if (appSettings.ActiveSchedule != null)
                        {
                            appSettings.ActiveSchedule.isPaused = true;

                            ((App)(Application.Current)).wndTrayMenu.BlockingScheduleState.Text = appSettings.ConfigFileStrings["menuInfo"]["statePaused"].ToString(); //"Active";
                        }
                    }
                    else
                    {
                        appSettings.ActiveSchedule.isPaused = false;

                        BlockingScheduleState.Text = appSettings.ConfigFileStrings["menuInfo"]["stateActive"].ToString(); //"Active";
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            btnPauseBlockingSchedule.IsChecked = true;
                        }), System.Windows.Threading.DispatcherPriority.Normal);
                    }
                }
                else if (appSettings.ActiveSchedule.BlockLevel == FOCUSMODESTRICTNESS.IRON_FOCUS)
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
                            appSettings.ActiveSchedule.isPaused = true;
                            BlockingScheduleState.Text = appSettings.ConfigFileStrings["menuInfo"]["statePaused"].ToString(); //"Paused"; 

                        }
                        else
                        {
                            appSettings.ActiveSchedule.isPaused = false;

                            BlockingScheduleState.Text = appSettings.ConfigFileStrings["menuInfo"]["stateActive"].ToString(); //"Active";
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                btnPauseBlockingSchedule.IsChecked = true;
                            }), System.Windows.Threading.DispatcherPriority.Normal);
          
                        }
                    }
                    else
                    {
                        //todo win texts
                        FBMessageBox ff = new FBMessageBox("You're running in Super Strict mode and no password is set. You will need to set a password to pause this custom schedule.", "Focus Bear");
                        ff.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                        ff.ShowDialog();

                        appSettings.ActiveSchedule.isPaused = false;

                        BlockingScheduleState.Text = appSettings.ConfigFileStrings["menuInfo"]["stateActive"].ToString(); //"Active";
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            btnPauseBlockingSchedule.IsChecked = true;
                        }), System.Windows.Threading.DispatcherPriority.Normal);

                    }

                }

            }
        }

        private void btnFocusMusic_Click(object sender, RoutedEventArgs e)
        {
            ((App)Application.Current).ShowFocusMusic();
        }

        private void btnPlayPauseMusic_Click(object sender, RoutedEventArgs e)
        {
            //Focus Music icon
            if (appSettings.arrFocusMusicTracks.Count > 0)
            {
                var current = appSettings.arrFocusMusicTracks.FirstOrDefault(t => t.IsPlaying || t.IsPaused);
                if (current != null)
                {
                    if (current.IsPlaying)
                    {
                        ((App)Application.Current).wndFocusMusic.PauseTrack(current);

                        string imagePath = ("pack://application:,,,/Focus Bear;component/images/play_icon.png");
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.UriSource = new Uri(imagePath);
                        image.EndInit();

                        PlayPauseButtonImage.Source = image;
                    }
                    else if (current.IsPaused)
                    {
                        ((App)Application.Current).wndFocusMusic.ResumeTrack(current);

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
