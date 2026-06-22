using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Preferences;
using Focus_Bear.Utils;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sentry;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using static Focus_Bear.Focus.TabMetrics;
using Application = System.Windows.Application;
using UserControl = System.Windows.Controls.UserControl;

namespace Focus_Bear.Focus
{
    /// <summary>
    /// Interaction logic for FocusModeEnd.xaml
    /// </summary>
    public partial class FocusModeEnd : UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        DispatcherTimer dispatcherTimerShowTasksEndWindow = new DispatcherTimer(DispatcherPriority.Normal);
        
        bool bDoNotShowBreak = false; 

        bool bUserClickedTake5MinBreak = false;
        bool bUserClickedContinue = false;
        bool bUserClickedExtendSession = false;

        private readonly DispatcherTimer _timerNavigatingToOtherAreasWithoutAction = new DispatcherTimer();

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        public string InfoText => "Shows apps and domains usage";
        public class UsageItem
        {
            public string Name { get; set; }
            public int DurationSeconds { get; set; }
            public double Percentage { get; set; }
            public bool IsApp { get; set; }

            public string DurationText => AppSettings.GetInstance.SecondsToTextHMS(DurationSeconds);
        }
        public ObservableCollection<UsageItem> UsageItems { get; set; }
        public FocusModeEnd()
        {
            InitializeComponent();
            LogWriter.LogWrite("FocusModeEnd() Init");

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }

            appSettings.bStartingAnotherFocusMode = false;

            _timerNavigatingToOtherAreasWithoutAction.Interval = TimeSpan.FromSeconds(1);
            _timerNavigatingToOtherAreasWithoutAction.Tick += (s, e) =>
            {
                _timerNavigatingToOtherAreasWithoutAction.Stop();
                Take5MinsBreak(); 
            };
        }


        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                dispatcherTimerShowTasksEndWindow.Stop();
                ShowTasksEndWindow();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("FocusModeEndWindow dispatcherTimerShowTasksEndWindow ex" + ex.Message);
            }
        }
        private void btnTakeBreak_Click(object sender, RoutedEventArgs e)
        {
            bUserClickedTake5MinBreak = true;

            ((App)Application.Current).wndFocusBearMainWindow.RightContent.Content = null; //We need to empty the view so that isVisible is not invoked when the window is shown after break

            ((App)Application.Current).wndFocusBearMainWindow.Hide();

            Take5MinsBreak();
        }

        private async void Take5MinsBreak()
        {
            if (appSettings.bStartingAnotherFocusMode)
                LogWriter.LogWrite("Take5MinsBreak() called from bStartingAnotherFocusMode");
            else
            {
                LogWriter.LogWrite("Take5MinsBreak() Clicked");

                if(appSettings.bUseSuperPomodoroMode) //If super pomodoro is still running we keep the title as Stop Super focus session
                    ((App)Application.Current).wndFocusBearMainWindow.UpdateLeafTitle(LeftButtonType.StartFocusSession, appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuStopSuperFocusSession"].ToString());
                else
                    ((App)Application.Current).wndFocusBearMainWindow.UpdateLeafTitle(LeftButtonType.StartFocusSession, appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuStartFocusSession"].ToString());
            }


            DateTime now = DateTime.Now;
            TimeSpan difference = now.Subtract(DateTime.Parse(appSettings.sRunningFocusModeStartTime)); // could also write `now - otherTime`
            var durationSeconds = difference.TotalSeconds;
            int nSeconds = Convert.ToInt32(durationSeconds);

            string sAchieve = TxtAchieve.Text.Replace(Environment.NewLine, " ");
            string sDistractions = ""; //removed
            appSettings.AddEntryInCSV("focus block", appSettings.sRunningFocusModeName, nSeconds.ToString(), "", "", appSettings.sRunningFocusModeStartTime, sAchieve, sDistractions, "", "");

            if (appSettings.bUseSuperPomodoroMode)
            {
                if (appSettings.nSuperPomodoroCountCompleted >= appSettings.NumberOfSuperPomodorosToRun) //check if all pomodoros have been finished.
                {
                    appSettings.bUseSuperPomodoroMode = false;
                    ((App)Application.Current).wndFocusBearMainWindow.UpdateLeafTitle(LeftButtonType.StartFocusSession, appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuStartFocusSession"].ToString());
                }
            }

            appSettings.CapturePostHog("completed-focus-session", string.Format("\"focus-type\":\"manual_focus\",\"focusDurationMinutes\":{0}", (nSeconds / 60)));

            //We need to add it back to the focus mode
            var FocusModeRunning = appSettings.arrFocusModes.Where(i => i.id == appSettings.sRunningFocusModeID);
            FocusMode CurrentRunningFocusMode = FocusModeRunning.First<FocusMode>();
            CurrentRunningFocusMode.lastTaskUsed = appSettings.sRawTasks;
           
            //Call api top finish focus mode
            FocusBearAPIClient client = new FocusBearAPIClient();
            try
            { 
                var xResponse = client.FinishFocusMode(appSettings.sRunningFocusModeID, sAchieve, sDistractions);

                var response = client.UpdateAllFocusModes();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Focus Mode end window client.FinishFocusMode API EX - " + ex.Message);
            }

            if(appSettings.bForceClosingFocusMode)
            {
                appSettings.bForceClosingFocusMode = false; ///Reset

                //Also fire the habits button to show the required screen
                ((App)Application.Current).wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.Habits);

                return;
            }

            if (appSettings.bStartingAnotherFocusMode)
                return;

            //Since the user is taking a few minutes break, we need to push back the micro breaks
            //Only if morning habits have been done
            if(((App)(Application.Current)).isMorningHabitsAlreadyShown)
                ((App)(Application.Current)).StartBreakActivityTimer();

            if (!bDoNotShowBreak)
            {
                appSettings.bHabitwithAllowedAppsRunning = false; //reset
                appSettings.bHabitwithUserActionNonTimerRunning = false; //reset
                //Show the Break Window
                ((App)(Application.Current)).ShowBreakWindow();
            }

            //Ask the user for adding temp apps / urls but should not be Super distracting URLs focus mode
            bool bRunningDefaultFocusMode = false;
            var listItemsDefault = appSettings.arrFocusModes.Where(i => i.id.ToLower() == appSettings.sRunningFocusModeID.ToLower());
            if (listItemsDefault.Count() != 0)
            {
                FocusMode ff = listItemsDefault.First();
                bRunningDefaultFocusMode = ff.isDefault ? true : false;
            }

            if (!bRunningDefaultFocusMode && (appSettings.TemporaryAllowedAppsGentleBlock.Count != 0 || appSettings.TemporaryAllowedURLsGentleBlock.Count != 0 || appSettings.TemporaryAllowedURLsTabTitles.Count != 0))
            {
                if(appSettings.TemporaryAllowedURLsTabTitles.Count > 0)
                {
                    LogWriter.LogWrite("Attempt to GetKeywords() -");
                    var m = await client.GetKeywords();
                    if(m == 1)
                    {
                        LogWriter.LogWrite("GetKeywords() Done-");
                        ///
                        appSettings.TemporaryAllowedKeywords.Clear();

                        //Check is we got some response from keywords api
                        if (appSettings.sRawKeywords != null || (appSettings.sRawKeywords.Length != 0))
                        {
                            //Parse the JSON string
                            try
                            {
                                List<string> array = new List<string>();
                                array = JsonConvert.DeserializeObject<List<string>>(appSettings.sRawKeywords);
                                if (array.Count > 0)
                                {
                                    foreach(string ss in array)
                                    {
                                        appSettings.TemporaryAllowedKeywords.Add(new DistractingURL(ss, false));
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogWriter.LogWrite("Parsing appSettings.sRawKeywords  EX - " + ex.Message);
                            }
                        }
                    }
                }

                if (appSettings.bIsGeekModeEnabled) //only in Geek mode
                {
                    try
                    {
                        AddTempAllowedToFocusModeWindow aa = new AddTempAllowedToFocusModeWindow();
                        aa.Topmost = true;
                        aa.Show();
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("shwing AddTempAllowedToFocusModeWindow EX = " + ex.Message);
                    }
                }
            }
             
        }
        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if(IsVisible)
            {

                try
                {
                    //
                    if (appSettings.bAdvancedAnalyticsInFocusModeEnabled)
                    {
                        UsageItems = new ObservableCollection<UsageItem>(GetUsageItems());
                        ListUsageItems.ItemsSource = UsageItems;
                        Column1.Header = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["titleAppOrSite"].ToString();
                        Column2.Header = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["titleTime"].ToString();
                        EnableAdvancedAnalytics.Visibility = Visibility.Collapsed;
                        ListUsageItems.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        //show hyper link button to enable advanced analytics
                        btnEnableAdvancedAnalyticsTXT.Text = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["buttonEnableAdvancedAnalyticsText"].ToString();
                        EnableAdvancedAnalytics.Visibility = Visibility.Visible;
                        ListUsageItems.Visibility = Visibility.Collapsed;
                    }

                    bDoNotShowBreak = false;
                    bUserClickedTake5MinBreak = false;
                    appSettings.bStartingAnotherFocusMode = false;
                    bUserClickedContinue = false;
                    bUserClickedExtendSession = false;

                    //if a break is running and focus session ends, the button should be Continue Break
                    if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                    {
                        btnContinueText.Text = "Continue Break";//appSettings.ConfigFileStrings["focusCompletedInfo"]["buttonContinueMicroBreak"].ToString();
                    }
                    else
                    {
                        btnContinueText.Text = appSettings.ConfigFileStrings["common"]["buttonContinueText"].ToString();
                    }
                    
                    Heading.Text = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["title"].ToString();

                    Description.Text = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["subtitleNew"].ToString();//"Focus block complete – log your achievement to track progress!"; 
                    StatsText.Text = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["titleStats"].ToString();
                   
                    DeepWork.Text = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["titleDeepWorkTime"].ToString();
                    InfoDeepWork.ToolTip = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["tooltipDeepWotkTimeThisSession"].ToString();

                    appSettings.CurrentFocusAlignmentScore = FocusAlignmentScoreManager.CalculateFocusAlignmentScore();
                    var duration = (appSettings.CurrentFocusSessionDuration * appSettings.CurrentFocusAlignmentScore) / 100;
                    DeepWorkValue.Text = appSettings.SecondsToTextHMS((int) Math.Round(duration));

                    TotalDeepWork.Text = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["titleDeepWorkTimeToday"].ToString();
                    TotalDeepWorkValue.Text = appSettings.SecondsToTextHMS((int)appSettings.TotalDeepFocusedTime + (int)appSettings.CurrentFocusSessionDuration);
                    InfoTotalDeepWork.ToolTip = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["tooltipTotalWotkTimeToday"].ToString();

                    FCA.Text = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["titleFocusAlignmentScore"].ToString();
                    FCAValue.Text = appSettings.CurrentFocusAlignmentScore.ToString() + "%";
                    InfoFCA.ToolTip = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["tooltipFocusAlignmentScoreAi"].ToString();

                    YourIntention.Text = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["titleIntention"].ToString();
                    Achieve.Text = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["questionFirstText"].ToString();
                    
                    SelectMinsLabel.Text = appSettings.ConfigFileStrings["common"]["extendFocusMinutesText"].ToString();

                    string sBreakMinutes = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["buttonTakeBreakText"].ToString();
                    if(appSettings.sMinutesBreakPomodoro.Length > 0 && appSettings.sMinutesBreakPomodoro != "0")
                    {
                        sBreakMinutes = sBreakMinutes.Replace("5", appSettings.sMinutesBreakPomodoro);
                    }
                    btnTakeBreakText.Text = sBreakMinutes;
                    string sFocusModeIntention =  appSettings.sRunningFocusModeIntention ?? "";
                    string sIntention = "";
                    try
                    {
                        if (appSettings.sRawTasks != null && appSettings.sRawTasks.Length > 2) //Now the empty array [] is returned
                        {

                            dispatcherTimerShowTasksEndWindow.Stop();

                            //Remove the previous event handler so that new can be set
                            var eventField = dispatcherTimerShowTasksEndWindow.GetType().GetField("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
                            var eventDelegate = (Delegate)eventField.GetValue(dispatcherTimerShowTasksEndWindow);
                            if (eventDelegate != null)
                            {
                                var invocatationList = eventDelegate.GetInvocationList();

                                foreach (var handler in invocatationList)
                                    dispatcherTimerShowTasksEndWindow.Tick -= ((EventHandler)handler);
                            }
       
                            //Set a timer after 2 seconds to show the task end window
                            dispatcherTimerShowTasksEndWindow.Tick += new EventHandler(dispatcherTimer_Tick);
                            dispatcherTimerShowTasksEndWindow.Interval = new TimeSpan(0, 0, 2);
                            dispatcherTimerShowTasksEndWindow.Start();
                            CommandManager.InvalidateRequerySuggested();
                            
                            string arrTaskNames = "";
                            JArray array = (JArray)JsonConvert.DeserializeObject(appSettings.sRawTasks);

                            for (int i = 0; i < array.Count(); i++)
                            {
                                JObject TodoItem = JObject.Parse(array[i].ToString());
                                string sName = TodoItem.ContainsKey("title") ? TodoItem["title"].ToString() : "";
                                arrTaskNames += sName;
                                arrTaskNames += "\n";
                            }
                            sIntention = arrTaskNames;
                        }

                        YourIntention.Text = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["titleIntention"].ToString() +
                            ": " + sIntention;
                        if(sFocusModeIntention.Length > 0)
                        {
                            YourIntention.Text = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["titleIntention"].ToString() +
                                ": " + sIntention + " " + sFocusModeIntention;
                        }
                        YourIntention.ToolTip = YourIntention.Text;
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("btnSelectTasks_Click() sRawTasks Exception - " + ex.Message);
                    }
                    //

                    btnStartAnotherFocusModeText.Text = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["buttonStartFocusText"].ToString();

                    //Empty the boxes
                    TxtAchieve.Text = "";

                    if (!appSettings.bIsGeekModeEnabled) //simple mode
                    {
                        btnStartAnotherFocusMode.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        if (appSettings.bUseSuperPomodoroMode)
                        {
                            btnStartAnotherFocusMode.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            btnStartAnotherFocusMode.Visibility = Visibility.Visible;
                        }
                    }

                    if (appSettings.bForceClosingFocusMode)//Only show continue
                    {
                        btnContinue.Visibility = Visibility.Visible;

                        btnTakeBreak.Visibility = Visibility.Collapsed;
                        btnStartAnotherFocusMode.Visibility = Visibility.Collapsed;
                        ExtendSessionPanel.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        //if a break is running and focus session ends, the button should be Continue Break
                        if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                        {
                            btnStartAnotherFocusMode.Visibility = Visibility.Collapsed;
                            btnTakeBreak.Visibility = Visibility.Collapsed;
                            btnContinue.Visibility = Visibility.Visible;
                            ExtendSessionPanel.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            btnTakeBreak.Visibility = Visibility.Visible;
                            btnContinue.Visibility = Visibility.Collapsed;

                            // Only show extend option if the timer actually expired (not manual stop)
                            bool bTimerExpired = false;
                            try
                            {
                                if (!string.IsNullOrEmpty(appSettings.sRunningFocusModeEndTime))
                                {
                                    DateTime endTime = DateTime.Parse(appSettings.sRunningFocusModeEndTime);
                                    bTimerExpired = DateTime.Now >= endTime;
                                }
                            }
                            catch (Exception exTimer)
                            {
                                LogWriter.LogWrite("FocusModeEnd timer check EX = " + exTimer.Message);
                            }

                            ExtendSessionPanel.Visibility = bTimerExpired ? Visibility.Visible : Visibility.Collapsed;
                        }
                    }

                    try
                    {
                        ExtendSessionLabel.Text = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["askExtendFocusText"].ToString();
                        btnExtendSessionText.Text = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["buttonExtendSessionText"].ToString();
                    }
                    catch (Exception exLoc)
                    {
                        LogWriter.LogWrite("FocusModeEnd extend session localization EX = " + exLoc.Message);
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("FocusModeEnd Window_IsVisibleChanged EX = " + ex.Message);
                }
            }
            else
            {
                LogWriter.LogWrite("FocusModeEnd Window User moving away without actions");
                //when user is moving away without any actions
                if (!bUserClickedTake5MinBreak && !appSettings.bStartingAnotherFocusMode && !bUserClickedContinue && !bUserClickedExtendSession)
                {
                    bDoNotShowBreak = true;
                    appSettings.bUseSuperPomodoroMode = false; //Force stop the super focus session

                    // Fix for “You did it” screen freezes after focus session and blocks navigation #1191
                    //Now after 1 seconds the API and other stuff fires so the navigated view renders and shows up correctly.
                    _timerNavigatingToOtherAreasWithoutAction.Stop();  // cancel previous pending fire
                    _timerNavigatingToOtherAreasWithoutAction.Start(); // schedule 1 second from now
                }
            }
        }
        private void btnStartAnotherFocusMode_Click(object sender, RoutedEventArgs e)
        {
            appSettings.bStartingAnotherFocusMode = true;

            Take5MinsBreak();//needed to execute the finish focus mode api

            //fire the start focus session button again
            ButtonAutomationPeer peer = new ButtonAutomationPeer(((App)(Application.Current)).wndTrayMenu.btnStartPomodoroMode);
            IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            invokeProv.Invoke();
        }

        public void ShowTasksEndWindow()
        {
            //14 May 2025 - Jeremy wants not to show the taks end window for now,
            //So, just returning from here, the timers are intact, when needed back, remove this return statement.
            return;
        }

        private string GetStatusText(string sStatusConstant)
        {
            if (sStatusConstant == "COMPLETED")
            {
                return appSettings.ConfigFileStrings["common"]["completedText"].ToString();//"Completed"; 
            }
            else if(sStatusConstant == "IN_PROGRESS")
            {
                return appSettings.ConfigFileStrings["common"]["inProgressText"].ToString(); //"In Progress";
            }
            else if (sStatusConstant == "NOT_STARTED")
            {
                return appSettings.ConfigFileStrings["common"]["notStartedText"].ToString(); //"Not Started";
            }
            return "";
        }

        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            bUserClickedContinue = true;
            bDoNotShowBreak = true;
             var duration = (appSettings.CurrentFocusSessionDuration * appSettings.CurrentFocusAlignmentScore) / 100;
                    DeepWorkValue.Text = appSettings.SecondsToTextHMS((int) Math.Round(duration));
            if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
            {
                //go to micro breaks screen
                ((App)Application.Current).wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.MicroBreaks);
            }
            else
            {
                appSettings.bUseSuperPomodoroMode = false; //Force stop the super focus session
            }

            Take5MinsBreak();
        }

        private async void btnExtendSession_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogWriter.LogWrite("btnExtendSession_Click()");

                int nMinutesToExtend = 15;
                if (cmbExtendMinutes.SelectedItem != null)
                {
                    ComboBoxItem selectedItem = (ComboBoxItem)cmbExtendMinutes.SelectedItem;
                    int.TryParse(selectedItem.Content.ToString(), out nMinutesToExtend);
                }

                LogWriter.LogWrite("Extending focus session by " + nMinutesToExtend + " minutes");

                appSettings.CapturePostHog("extend-focus-session-from-complete-screen", string.Format("\"extendMinutes\":{0}", nMinutesToExtend));

                DateTime newEndTime = DateTime.Now.AddMinutes(nMinutesToExtend);
                appSettings.sRunningFocusModeMinutes = nMinutesToExtend.ToString();
                appSettings.sRunningFocusModeEndTime = newEndTime.ToString();
                appSettings.focusSessionStartTime = DateTime.Now;

                FocusBearAPIClient client = new FocusBearAPIClient();
                var response = await client.ExtendFocusMode(appSettings.sRunningFocusModeID, nMinutesToExtend);

                if (response == 1)
                {
                    LogWriter.LogWrite("ExtendFocusMode API success");
                }
                else
                {
                    LogWriter.LogWrite("ExtendFocusMode API failed, continuing with local extension");
                }
                bUserClickedExtendSession = true;
                ((App)Application.Current).wndFocusBearMainWindow.RightContent.Content = null;
                ((App)Application.Current).wndFocusBearMainWindow.Hide();

                appSettings.isFocusModeRunning = true;
                appSettings.isFocusModePaused = false;
                appSettings.nPausedFocusRemainingSeconds = 0;

                var focusModeRunning = appSettings.arrFocusModes.Where(i => i.id == appSettings.sRunningFocusModeID);
                if (focusModeRunning.Any())
                {
                    focusModeRunning.First().isOn = true;
                }

                ((App)Application.Current).ShowFocusProgressWindow();

                ((App)(Application.Current)).wndTrayMenu.OnUntill.Visibility = Visibility.Visible;
                ((App)(Application.Current)).wndTrayMenu.OrangeHeader.Height = 45;

                if (appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
                {
                    ((App)(Application.Current)).wndTrayMenu.OrangeHeader.Height = 70;
                }

                string sTill = appSettings.ConfigFileStrings["menuInfo"]["untilTimeText"].ToString();
                sTill = sTill.Replace("<TIME>", newEndTime.ToString());
                ((App)(Application.Current)).wndTrayMenu.OnUntill.Content = sTill;

                int nHours = nMinutesToExtend / 60;
                int nMinutes = nMinutesToExtend % 60;
                ((App)(Application.Current)).wndTrayMenu.StartFocusModeTimer(nHours, nMinutes, true);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnExtendSession_Click EX = " + ex.Message);
            }
        }

        public List<UsageItem> GetUsageItems()
        {
            var apps = TabMetrics.Instance.ClubAllSameAppEntries();
            var domains = TabMetrics.Instance.ClubAllSameDomainEntries();

            var combined = new List<UsageItem>();

            // Apps
            foreach (var app in apps)
            {
                combined.Add(new UsageItem
                {
                    Name = app.AppName,
                    DurationSeconds = app.Duration,
                    IsApp = true
                });
            }

            // Domains
            foreach (var domain in domains)
            {
                combined.Add(new UsageItem
                {
                    Name = domain.Domain,
                    DurationSeconds = domain.Duration,
                    IsApp = false
                });
            }

            // Total time
            int total = combined.Sum(x => x.DurationSeconds);

            if (total > 0)
            {
                foreach (var item in combined)
                {
                    item.Percentage = (double)item.DurationSeconds / total * 100;
                }
            }

            // Sort descending
            return combined
                .OrderByDescending(x => x.DurationSeconds)
                .ToList();
        }

        private void btnEnableAdvancedAnalytics_Click(object sender, RoutedEventArgs e)
        {
            appSettings.bAdvancedAnalyticsInFocusModeEnabled = true;

            UsageItems = new ObservableCollection<UsageItem>(GetUsageItems());
            ListUsageItems.ItemsSource = UsageItems;
            Column1.Header = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["titleAppOrSite"].ToString();
            Column2.Header = appSettings.ConfigFileStrings["focusInfo"]["focusCompletedInfo"]["titleTime"].ToString();
            EnableAdvancedAnalytics.Visibility = Visibility.Collapsed;
            ListUsageItems.Visibility = Visibility.Visible;
        }
    }
}
