using Focus_Bear.Habits;
using Focus_Bear.Models;
using Focus_Bear.Preferences;
using Focus_Bear.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Focus_Bear.Focus
{
    /// <summary>
    /// Interaction logic for FocusActivitySummary.xaml
    /// </summary>
    public partial class FocusActivitySummary : UserControl
    {

        AppSettings appSettings = AppSettings.GetInstance;

        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);

        bool bNeedMoreTime = false;
        bool bNeed1MinuteClicked = false;
        struct FocusSummary
        {
            public string name { get; set; }
            public string start_time { get; set; }
            public string duration { get; set; }
            public string achievements { get; set; }
            public string distractions { get; set; }


            public FocusSummary(string name, string start_time, string duration, string achievements, string distractions)
            {
                this.name = name;
                this.start_time = start_time;
                this.duration = duration;
                this.achievements = achievements;
                this.distractions = distractions;
            }
        }

        struct ActivitySummary
        {
            public string name { get; set; }
            public string duration { get; set; } //Will be used as Quantity also
            public ActivitySummary(string name, string duration)
            {
                this.name = name;
                this.duration = duration;
            }
        }

        private List<FocusSummary> FocusSummaryItems = new List<FocusSummary>();

        private List<ActivitySummary> ActivityQuantityItems = new List<ActivitySummary>();
        private List<ActivitySummary> ActivityAverageItems = new List<ActivitySummary>();
        private List<ActivitySummary> ActivityDurationItems = new List<ActivitySummary>();
        FocusBearMainWindow ParentWindow = null;
        public FocusActivitySummary(FocusBearMainWindow _ParentWindow)
        {
            InitializeComponent();
            
            ParentWindow = _ParentWindow;

            LogWriter.LogWrite("FocusActivitySummary() window Initating");

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }
        }
        public void InitializeFocusSummaryView()
        {
            try
            {
                if(bNeed1MinuteClicked)
                {
                    bNeedMoreTime = true; //case is user clicks need a minutes then comeback to this screen, then need 1 min should not be availble but postpone should
                }
                if (!appSettings.bPlayActivitiesInFullScreenMode)
                {
                    MainGrid.RowDefinitions[0].Height = new GridLength(100);
                }
                int nCountFocusBlocks = 0;
                int nTotalFocusTime = 0;

                //Reinitialize
                FocusSummaryItems.Clear();
                ActivityQuantityItems.Clear();
                ActivityAverageItems.Clear();
                ActivityDurationItems.Clear();

                //Parse the Activity data got from API
                if (appSettings.sActivityAllDaySummaryData.Length > 0)
                {
                    JObject sActivityData = JObject.Parse(appSettings.sActivityAllDaySummaryData);
                    if (sActivityData.ContainsKey("focusSummary"))
                    {
                        JArray arrayItems = (JArray)sActivityData["focusSummary"];

                        nCountFocusBlocks = arrayItems.Count();

                        foreach (JObject item in arrayItems)
                        {
                            string name = item.ContainsKey("name") ? item.GetValue("name").ToString() : "";
                            string start_time = item.ContainsKey("start_time") ? item.GetValue("start_time").ToString() : "";
                            DateTime Local = DateTime.Parse(start_time).ToLocalTime(); //Convert the UTC time to Local time

                            int durationSeconds = item.ContainsKey("duration") ? (int)item.GetValue("duration") : 0;
                            nTotalFocusTime += durationSeconds;
                            string duration = appSettings.SecondsToText(durationSeconds);
                            string achievements = item.ContainsKey("achievements") ? item.GetValue("achievements").ToString() : "";
                            achievements = HttpUtility.UrlDecode(achievements);
                            string distractions = item.ContainsKey("distractions") ? item.GetValue("distractions").ToString() : "";

                            FocusSummaryItems.Add(new FocusSummary(name, Local.ToString(), duration, achievements, distractions));
                        }
                    }

                    if (sActivityData.ContainsKey("daySummarySUM"))
                    {
                        JArray arrayItems = (JArray)sActivityData["daySummarySUM"];

                        foreach (JObject item in arrayItems)
                        {
                            string name = item.ContainsKey("name") ? item.GetValue("name").ToString() : "";
                            int nQuantity = item.ContainsKey("quantity") ? (int)item.GetValue("quantity") : 0;

                            ActivityQuantityItems.Add(new ActivitySummary(name, nQuantity.ToString()));
                        }
                    }

                    if (sActivityData.ContainsKey("daySummaryAVG"))
                    {
                        JArray arrayItems = (JArray)sActivityData["daySummaryAVG"];

                        foreach (JObject item in arrayItems)
                        {
                            string name = item.ContainsKey("name") ? item.GetValue("name").ToString() : "";
                            int nQuantity = item.ContainsKey("quantity") ? (int)item.GetValue("quantity") : 0;

                            ActivityAverageItems.Add(new ActivitySummary(name, nQuantity.ToString()));
                        }
                    }

                    if (sActivityData.ContainsKey("daySummaryDuration"))
                    {
                        JArray arrayItems = (JArray)sActivityData["daySummaryDuration"];

                        foreach (JObject item in arrayItems)
                        {
                            string name = item.ContainsKey("name") ? item.GetValue("name").ToString() : "";
                            int durationSeconds = item.ContainsKey("duration") ? (int)item.GetValue("duration") : 0;
                            string duration = appSettings.SecondsToText(durationSeconds);

                            ActivityDurationItems.Add(new ActivitySummary(name, duration));
                        }
                    }
                }

                ListFocusSummary.ItemsSource = FocusSummaryItems;

                ListQuantity.ItemsSource = ActivityQuantityItems;
                ListAverage.ItemsSource = ActivityAverageItems;
                ListDuration.ItemsSource = ActivityDurationItems;

                string sHeading = appSettings.ConfigFileStrings["focusInfo"]["focusSummaryInfo"]["title"].ToString();
                Heading.Text = sHeading.Replace("<COUNT>", nCountFocusBlocks.ToString());

                FocusSummaryTxt.Text = appSettings.ConfigFileStrings["focusInfo"]["focusSummaryInfo"]["titleFocusSummaryText"].ToString();

                string sTotalTIme = appSettings.ConfigFileStrings["focusInfo"]["focusSummaryInfo"]["totalFocusTimeText"].ToString();
                TotalTime.Text = sTotalTIme.Replace("<TIME>", appSettings.SecondsToText(nTotalFocusTime));

                FocusType.Text = appSettings.ConfigFileStrings["focusInfo"]["focusSummaryInfo"]["focusTableColumnFirstText"].ToString();
                StartTime.Text = appSettings.ConfigFileStrings["focusInfo"]["focusSummaryInfo"]["focusTableColumnSecondText"].ToString();
                DurationTxt.Text = appSettings.ConfigFileStrings["focusInfo"]["focusSummaryInfo"]["focusTableColumnThirdText"].ToString();
                Work.Text = appSettings.ConfigFileStrings["focusInfo"]["focusSummaryInfo"]["focusTableColumnFourthText"].ToString();
                Distractions.Text = appSettings.ConfigFileStrings["focusInfo"]["focusSummaryInfo"]["focusTableColumnFifthText"].ToString();
                ActivitySummaryTxt.Text = appSettings.ConfigFileStrings["focusInfo"]["focusSummaryInfo"]["titleActivitySummaryText"].ToString();

                Activity1.Text = appSettings.ConfigFileStrings["focusInfo"]["focusSummaryInfo"]["activityTableColumnFirstText"].ToString();
                Quantity.Text = appSettings.ConfigFileStrings["focusInfo"]["focusSummaryInfo"]["activityTableColumnSecondText"].ToString();

                Activity2.Text = appSettings.ConfigFileStrings["focusInfo"]["focusSummaryInfo"]["activityTableColumnFirstText"].ToString();
                Average.Text = appSettings.ConfigFileStrings["focusInfo"]["focusSummaryInfo"]["activityTableColumnThirdText"].ToString();

                Activity3.Text = appSettings.ConfigFileStrings["focusInfo"]["focusSummaryInfo"]["activityTableColumnFirstText"].ToString();
                TotalDuration.Text = appSettings.ConfigFileStrings["focusInfo"]["focusSummaryInfo"]["activityTableColumnSixthText"].ToString();

                if (bNeedMoreTime)
                {
                    btnNeedOneMinuteTXT.Text = appSettings.ConfigFileStrings["focusInfo"]["focusSummaryInfo"]["buttonStillNeedMoreTimeText"].ToString();
                }
                else{
                    btnNeedOneMinuteTXT.Text = appSettings.ConfigFileStrings["focusInfo"]["focusSummaryInfo"]["buttonNeedMoreTimeText"].ToString();
                }

                btnContinueTXT.Text = appSettings.ConfigFileStrings["preloadFocusModesViewInfo"]["buttonContinueText"].ToString();

                if (appSettings.bIsGeekModeEnabled)
                {
                    GridFocusSummary.Visibility = Visibility.Visible;
                    GridSummarySimpleMode.Visibility = Visibility.Collapsed;
                }
                else
                {
                    GridFocusSummary.Visibility = Visibility.Collapsed;
                    GridSummarySimpleMode.Visibility = Visibility.Visible;

                    SetSimpleModeTexts();

                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("FocusActivitySummary() InitializeFocusSummaryView ex + " + ex.Message);
            }
        }
        private void SetSimpleModeTexts()
        {
            if (appSettings.bIsGeekModeEnabled)
            {
                GridFocusSummary.Visibility = Visibility.Visible;
                GridSummarySimpleMode.Visibility = Visibility.Collapsed;
            }
            else
            {
                GridFocusSummary.Visibility = Visibility.Collapsed;
                GridSummarySimpleMode.Visibility = Visibility.Visible;
            }
            Heading.Text = appSettings.ConfigFileStrings["focusInfo"]["focusSummaryInfo"]["titleDaySummary"].ToString();

            TotalDistractingSitesBlocked.Text = "Total distracting sites blocked: " + appSettings.nTotalDistractingSitesBlocked; //ok
            TimeSpentBlockingDistractingSites.Text = "Time spent on super distracting sites: " + appSettings.SecondsToTextHMS(appSettings.nTotalTimeForDistractingSitesToday);  //ok
            TimeSpentBlockingActivated.Text = "Time spent with blocking activated: " + appSettings.SecondsToTextHMS(appSettings.nTimeSpentBlockingActivated);
            TimeSpentBlockingDeactivated.Text = "Time spent with blocking deactivated: " + appSettings.SecondsToTextHMS(appSettings.nTimeSpentBlockingDeactivated);

        }
        public void SimulateNeedOneMinute_Click()
        {
            LogWriter.LogWrite("SimulateNeedOneMinute_Click()");

            if (ParentWindow.GridShade.IsVisible)
            {
                LogWriter.LogWrite("GridShade.IsVisible return from SimulateNeedOneMinute_Click()");
                return;
            }

            ButtonAutomationPeer peer = new ButtonAutomationPeer(btnNeedOneMinute);
            IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            invokeProv.Invoke();
        }
        private void btnNeedOneMinute_Click(object sender, RoutedEventArgs e)
        {
            if (appSettings.DidCutOffTimePassed())
            {
                if (appSettings.isPasswordSet)
                {
                    if (appSettings.bPasswordRequireToSkipEveningAfterCutOff)
                    {
                        AskPassword pwdWnd = new AskPassword();
                        pwdWnd.Owner = ParentWindow;
                        ParentWindow.GridShade.Visibility = Visibility.Visible;

                        bool? bResult = pwdWnd.ShowDialog();

                        ParentWindow.GridShade.Visibility = Visibility.Collapsed;

                        if (bResult.Value)
                        {
                            //Password is correct
                        }
                        else
                        {
                            return;
                        }
                    }
                }
            }

            bNeed1MinuteClicked = true;

            if (bNeedMoreTime)
            {
                LogWriter.LogWrite("bNeedMoreTime - show postpone dialog");

                ParentWindow.GridShade.Visibility = Visibility.Visible;
                //Show the postpone dialog here
                PostponeHabits wndPostpone = new PostponeHabits(ParentWindow);
                wndPostpone.ShowDialog();
                ParentWindow.GridShade.Visibility = Visibility.Collapsed;
                if (appSettings.bFocusSummaryPostponedState && !appSettings.isFocusModeRunning && appSettings.AutoCloseAfterRoutineSkipPostpone)
                {
                    ParentWindow.Hide();
                }
            }
            else
            {
                try
                {
                    LogWriter.LogWrite("btnNeedOneMinute_Click() clicked");

                    dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
#if DEBUG
                    dispatcherTimer.Interval = new TimeSpan(0, 0, 10); //One minute timer
#else
                    dispatcherTimer.Interval = new TimeSpan(0, 1, 0); //One minute timer
#endif
                    dispatcherTimer.Start();
                    ParentWindow.Hide(); //hide self window for 1 minute
                    CommandManager.InvalidateRequerySuggested();
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("btnNeedOneMinute_Click() + " + ex.Message);
                }
            }
            
        }


        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
 
            try
            {
                CommandManager.InvalidateRequerySuggested();
                dispatcherTimer.Stop(); //Tick Once Only
                RegistryManager.RemoveHabitsPostponeEndTime(); //postpone resolved - clear any persisted restore
                btnNeedOneMinuteTXT.Text = appSettings.ConfigFileStrings["focusInfo"]["focusSummaryInfo"]["buttonStillNeedMoreTimeText"].ToString();
                bNeedMoreTime = true;
                ((App)(Application.Current)).ShowBlankWindowOnOtherScreens();

                //((App)Application.Current).wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.Habits);
                //ParentWindow.Show();
                appSettings.bFocusSummaryPostponedState = false;

                //if (appSettings.isFocusModeRunning)
                //{
                //    ((App)Application.Current).wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.Habits);
                //    ((App)(Application.Current)).ForceStopFocusSessionOnHabitsTime();
                //}
                //else
                //{
                    appSettings.nCurrentActivityType = CurrentActivityType.NONE; //Background thread will take care of evening routine
                //}

                
                LogWriter.LogWrite(" appSettings.nCurrentActivityType = CurrentActivityType.NONE;");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Need 1 minute timer EX = " + ex.Message);
            }

        }

        public void PostponeFocusSummary(int nMinutesToPostpone)
        {
            //Hide this window, Set a timer for x nMinutesToPostpone and Hide the window and reappear again
            LogWriter.LogWrite("PostponeFocusSummary()");

#if DEBUG
            dispatcherTimer.Interval = new TimeSpan(0, 0, nMinutesToPostpone);
#else
             dispatcherTimer.Interval = new TimeSpan(0, nMinutesToPostpone, 0);
#endif
            appSettings.bFocusSummaryPostponedState = true;
            appSettings.sActivityAllDaySummaryData = ""; //Setting it empty so that API can be fetched again.
            if (appSettings.DidCutOffTimePassed())
            {
                appSettings.bEveningHabitsPostponedAfterCutoffTime = true;
            }

            //Persist so the focus-summary postpone survives an app restart (restored in StartWorkFlow, which
            //re-arms this timer via RestorePostponeTimer). nCurrentActivityType is still EVENING_HABITS here.
            ((App)Application.Current).PersistHabitsPostpone(DateTime.Now.AddMinutes(nMinutesToPostpone), true);

            dispatcherTimer.Start();

            ParentWindow.Hide();
            appSettings.RemoveSecondaryWindows();

            ((App)Application.Current).wndFocusBearMainWindow.EnableAllButtons();//Enable all the left menu buttons

             //V2: we need to show the Start Focus session Section if its Geek mode OR the Resume habits window secion.
            if (appSettings.bIsGeekModeEnabled)
            {
                ((App)Application.Current).ShowPomodoroWindow();
            }
            else
            {
                //V2 todo - in simple mode, show morning habits view again with button - resume habits
                ((App)Application.Current).wndFocusBearMainWindow.PageHabits.ShowHabitsPostponedView();
            }


            CommandManager.InvalidateRequerySuggested();
        }

        public void ResumeFocusSummary()
        {

            LogWriter.LogWrite("ResumeFocusSummary()");

            dispatcherTimer.Stop(); //Tick Once Only

            if (!appSettings.bFocusSummaryPostponedState)
            {
                LogWriter.LogWrite("ResumeFocusSummary() - Returning as the bFocusSummaryPostponedState is no longer Valid");
                return;
            }

            ((App)(Application.Current)).ShowBlankWindowOnOtherScreens();
            ParentWindow.Show();
            appSettings.bFocusSummaryPostponedState = false;
            appSettings.bEveningHabitsPostponedAfterCutoffTime = false;
            RegistryManager.RemoveHabitsPostponeEndTime(); //postpone resolved - clear any persisted restore

            appSettings.nCurrentActivityType = CurrentActivityType.NONE; //Background thread will take care of evening routine
            LogWriter.LogWrite(" appSettings.nCurrentActivityType = CurrentActivityType.NONE;");
        }

        // Re-arm the focus-summary postpone timer for the remaining time when restoring a persisted postpone
        // on app restart (StartWorkFlow). Mirrors PostponeFocusSummary's timer setup: on expiry dispatcherTimer_Tick
        // runs (drops nCurrentActivityType to NONE) so the background loop re-shows the summary, exactly as a
        // live postpone would. The postponed view itself is shown by StartWorkFlow.
        public void RestorePostponeTimer(TimeSpan remaining)
        {
            try
            {
                if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
                appSettings.bFocusSummaryPostponedState = true;
                dispatcherTimer.Stop();
                dispatcherTimer.Tick -= new EventHandler(dispatcherTimer_Tick); //avoid a double subscription
                dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
                dispatcherTimer.Interval = remaining;
                dispatcherTimer.Start();
                LogWriter.LogWrite("FocusActivitySummary.RestorePostponeTimer - remaining " + remaining);
            }
            catch (Exception ex) { LogWriter.LogWrite("RestorePostponeTimer ex - " + ex.Message); }
        }

        /// <summary>
        /// Stop the focus-summary postpone timer WITHOUT ending the routine. Used when the user resumes
        /// the evening routine by playing a habit from the Day Plan: the routine must continue (so, unlike
        /// <see cref="ResumeFocusSummary"/>, we must NOT set nCurrentActivityType = NONE), but the stray
        /// postpone timer has to be stopped or it later fires and forces the routine to NONE mid-habit (#1365).
        /// </summary>
        public void StopPostponeTimerForDayPlanPlay()
        {
            try { dispatcherTimer.Stop(); }
            catch (Exception ex) { LogWriter.LogWrite("StopPostponeTimerForDayPlanPlay ex - " + ex.Message); }
        }

        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            //The window end only on pressing Continue, need to proceed to evening routine after setting focus summary shown date
            //Today's Focus Summary is done
            LogWriter.LogWrite("Focus Summary shown for today - Update Date");

            DateTime now = DateTime.Now;

            TimeSpan tsShutOffTime = TimeSpan.Parse(appSettings.sDayShutOffTime);
            //DateTime now = DateTime.Now;
            DateTime myShutOffTimeLocal = new DateTime(now.Year, now.Month, now.Day, tsShutOffTime.Hours, tsShutOffTime.Minutes, 00);
            Console.WriteLine("{0}", myShutOffTimeLocal.ToString());


            TimeSpan tsStartTime = TimeSpan.Parse(appSettings.sDayStartTime);
            DateTime myStartTime = new DateTime(now.Year, now.Month, now.Day, tsStartTime.Hours, tsStartTime.Minutes, 00);
            Console.WriteLine("{0}", myStartTime.ToString());

            if (now > myShutOffTimeLocal) //Same Day like befoee 12.00 am
            {
                //Get the morning time and prepare a DT for Evening Habits Shown Till Date
                myStartTime = myStartTime.AddDays(1);
                Console.WriteLine("{0}", myStartTime.ToString());
                appSettings.FocusSummaryShownTillDate = myStartTime;
            }
            else //Next Day like after 12.00 am
            {
                appSettings.FocusSummaryShownTillDate = myStartTime;//Today's Morning time
            }
            LogWriter.LogWrite(string.Format("Set FocusSummaryShownTillDate - {0}", appSettings.FocusSummaryShownTillDate.ToString()));

            RegistryManager.SetFocusSummaryShownTillDate();

            appSettings.nCurrentActivityType = CurrentActivityType.NONE; //Background thread will take care of evening routine
            LogWriter.LogWrite("appSettings.nCurrentActivityType = CurrentActivityType.NONE;");

            //Fire the habits button to show the required screen
            ((App)Application.Current).wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.Habits);
        }

        private void FullScreenParent_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            bool wasCodeClosed = new StackTrace().GetFrames().FirstOrDefault(x => x.GetMethod() == typeof(Window).GetMethod("Close")) != null;
            if (wasCodeClosed)
            {
                // Closed with this.Close()
            }
            else
            {
                e.Cancel = true;
                // Closed some other way - like close button clicked.
                // first Need one more minute button to postpone
                ButtonAutomationPeer peer = new ButtonAutomationPeer(btnNeedOneMinute);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();

                //mj// todo hanle in main window close

            }
        }

        private void FullScreenParent_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                if (!appSettings.bIsGeekModeEnabled)
                    SetSimpleModeTexts();
            }
            else
            {
                appSettings.RemoveSecondaryWindows();
            }
           
        }
    }
}
