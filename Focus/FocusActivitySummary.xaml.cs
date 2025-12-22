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
    public partial class FocusActivitySummary : FullScreenParent
    {

        AppSettings appSettings = AppSettings.GetInstance;

        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);

        bool bNeedMoreTime = false;

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
        public FocusActivitySummary()
        {
            InitializeComponent();

            LogWriter.LogWrite("FocusActivitySummary() window Initating");

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
                this.Topmost = true;

                if (!appSettings.bPlayActivitiesInFullScreenMode)
                {
                    this.WindowState = WindowState.Normal;
                    this.WindowStyle = WindowStyle.ToolWindow;
                    this.Width = 1300;
                    this.Height = 750;

                    MainGrid.RowDefinitions[0].Height = new GridLength(100);
                }

                //Set the minimum so that in non full screen mode we do not go below this.
                this.MinWidth = 1300;
                this.MinHeight = 750;
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;


                int nCountFocusBlocks = 0;
                int nTotalFocusTime = 0;

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

                btnNeedOneMinuteTXT.Text = appSettings.ConfigFileStrings["focusInfo"]["focusSummaryInfo"]["buttonNeedMoreTimeText"].ToString();

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
                LogWriter.LogWrite("FocusActivitySummary() + " + ex.Message);
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
            //todo win texts
            Heading.Text = "Your day summary";//todo win texts

            TotalDistractingSitesBlocked.Text = "Total distracting sites blocked: " + appSettings.nTotalDistractingSitesBlocked; //ok
            TimeSpentBlockingDistractingSites.Text = "Time spent on super distracting sites: " + appSettings.SecondsToTextHMS(appSettings.nTotalTimeForDistractingSitesToday);  //ok
            TimeSpentBlockingActivated.Text = "Time spent with blocking activated: " + appSettings.SecondsToTextHMS(appSettings.nTimeSpentBlockingActivated);
            TimeSpentBlockingDeactivated.Text = "Time spent with blocking deactivated: " + appSettings.SecondsToTextHMS(appSettings.nTimeSpentBlockingDeactivated);

        }
    public void SimulateNeedOneMinute_Click()
        {
            LogWriter.LogWrite("SimulateNeedOneMinute_Click()");

            if (GridShade.IsVisible)
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
                        pwdWnd.Owner = this;
                        GridShade.Visibility = Visibility.Visible;

                        bool? bResult = pwdWnd.ShowDialog();

                        GridShade.Visibility = Visibility.Collapsed;

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


            if (bNeedMoreTime)
            {
                LogWriter.LogWrite("bNeedMoreTime - show postpone dialog");

                GridShade.Visibility = Visibility.Visible;
                //Show the postpone dialog here
                PostponeHabits wndPostpone = new PostponeHabits(this);
                wndPostpone.ShowDialog();
                GridShade.Visibility = Visibility.Collapsed;
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
                    Hide(); //hide self window for 1 minute
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
                btnNeedOneMinuteTXT.Text = appSettings.ConfigFileStrings["focusInfo"]["focusSummaryInfo"]["buttonStillNeedMoreTimeText"].ToString();
                bNeedMoreTime = true;
                ((App)(Application.Current)).ShowBlankWindowOnOtherScreens();
                Show();
                appSettings.bFocusSummaryPostponedState = false;

                //If by chance the start pomodoro window is open, we need to close it.
                /**if (appSettings.isStartPomodoroWindowOpen)
                {
                    //Let's close the start pomodoro window 
                    if (((App)(Application.Current)).wndStartPomodoro != null)
                    {
                        ((App)(Application.Current)).wndStartPomodoro.Hide();
                    }
                    appSettings.isStartPomodoroWindowOpen = false;
                }**/ //mj// todo handling

                if (appSettings.isFocusModeRunning)
                {
                    ((App)(Application.Current)).ForceStopFocusSessionOnHabitsTime();
                }

                //Make the global variable false
                appSettings.isFocusModeRunning = false;
                appSettings.bUseSuperPomodoroMode = false;
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

            if (appSettings.DidCutOffTimePassed())
            {
                appSettings.bEveningHabitsPostponedAfterCutoffTime = true;
            }

            dispatcherTimer.Start();

            Hide();
            appSettings.RemoveSecondaryWindows();

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
            Show();
            appSettings.bFocusSummaryPostponedState = false;
            appSettings.bEveningHabitsPostponedAfterCutoffTime = false;
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
            LogWriter.LogWrite(" appSettings.nCurrentActivityType = CurrentActivityType.NONE;");

            Close();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            appSettings.RemoveSecondaryWindows();
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
            }
        }

        private void FullScreenParent_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                this.Width = 1300;
                this.Height = 750;
                // 1) Grab the DPI scale for this monitor
                var src = PresentationSource.FromVisual(this);
                if (src?.CompositionTarget == null) return;
                var m = src.CompositionTarget.TransformToDevice;
                double dpiX = m.M11, dpiY = m.M22;


                // 2) Compute how many logical units the screen can show
                //    (SystemParameters.WorkArea is in physical pixels)
                double screenLogicalWidth = SystemParameters.WorkArea.Width / dpiX;
                double screenLogicalHeight = SystemParameters.WorkArea.Height / dpiY;

                // 3) Your “ideal” logical minimum
                const double idealMinW = 1300, idealMinH = 750;

                // 4) set to what actually fits
                this.Width = Math.Min(idealMinW, screenLogicalWidth);
                this.Height = Math.Min(idealMinH, screenLogicalHeight);
                this.MinWidth = Math.Min(idealMinW, screenLogicalWidth);
                this.MinHeight = Math.Min(idealMinH, screenLogicalHeight);

                // 6) Center it
                this.Left = Math.Max(0, (screenLogicalWidth - this.Width) / 2);
                this.Top = Math.Max(0, (screenLogicalHeight - this.Height) / 2);
            }


            if (!appSettings.bIsGeekModeEnabled)
                SetSimpleModeTexts();
        }
    }
}
