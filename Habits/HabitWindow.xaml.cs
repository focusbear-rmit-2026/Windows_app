using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Onboarding;
using Focus_Bear.Preferences;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using TextBox = System.Windows.Controls.TextBox;

namespace Focus_Bear.Habits
{
    /// <summary>
    /// Interaction logic for HabitWindow.xaml
    /// </summary>
    public partial class HabitWindow : UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;
        private HabitActivityFinished nScreenHabitActivityFinished = null;
        public DateTime ActivityStartTime;

        public HabitActivityOnGoing PageHabitActivityOnGoing = null;
        public HabitsWelcomeList PageWelcomeList = null;
        public HabitExtraPersonal PageHabitExtraPersonal;

        HabitsPostponedView PageHabitsPostponedView = new HabitsPostponedView();
        NoHabitsRunningState PageNoHabitsRunningState = new NoHabitsRunningState();

        int nTimer = 5;

        DispatcherTimer dispatcherTimerSkippingIn = new DispatcherTimer(DispatcherPriority.Normal);

        DispatcherTimer dispatcherTimerShowDateTime = new DispatcherTimer(DispatcherPriority.Normal);

        DispatcherTimer dispatcherTimerAskSleepAndMood = new DispatcherTimer(DispatcherPriority.Normal);

        DispatcherTimer dispatcherTimerShowReducedHabitsMessage = null;

        FocusBearMainWindow ParentWindow = null;
        public HabitWindow()
        {
        }
        public HabitWindow(FocusBearMainWindow _ParentWindow)
        {
            InitializeComponent();

            ParentWindow = _ParentWindow;

        }
        public async void InitializeInnerScreens()
        {
            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }

            LogWriter.LogWrite("HabitWindow() - InitializeInnerScreens()");

            if (!appSettings.bPlayActivitiesInFullScreenMode)
            {
                btnPleaseSpareMeTXT.Text = appSettings.ConfigFileStrings["common"]["buttonPostponeText"].ToString();
            }
            else
            {
                btnPleaseSpareMeTXT.Text = appSettings.ConfigFileStrings["common"]["buttonAbortText"].ToString();
            }
            btnPleaseSpareMe.Visibility = Visibility.Visible;

            TodaysTime.Text = DateTime.Now.ToString(CultureInfo.CurrentCulture.DateTimeFormat.FullDateTimePattern);

            dispatcherTimerShowDateTime.Tick += new EventHandler(dispatcherTimerShowDateTime_Tick);
            dispatcherTimerShowDateTime.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimerShowDateTime.Start();


            Heading.Text = "";

            //Show the Please Spare Me Button
            btnPleaseSpareMe.Visibility = Visibility.Visible;

            AppFrameHabitScreens.Visibility = Visibility.Visible;

            dispatcherTimerSkippingIn.Tick += new EventHandler(dispatcherTimerSkippingIn_Tick);
            dispatcherTimerSkippingIn.Interval = new TimeSpan(0, 0, 1);

            if (nScreenHabitActivityFinished != null) //Will create afresh
            {
                nScreenHabitActivityFinished.webView.Dispose();
                nScreenHabitActivityFinished.webView1.Dispose();
                nScreenHabitActivityFinished = null;
            }

            nScreenHabitActivityFinished = new HabitActivityFinished(ParentWindow);

            if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
            {
                //We need to hide the Please SPare Me Button
                LogWriter.LogWrite("AppFrameHabitScreens.Navigate(new BreakActivityBrainDump(this));");
                AppFrameHabitScreens.Navigate(new BreakActivityBrainDump(ParentWindow));


                // Note: subscription limit popup for break activities is handled in
                // BreakActivityBrainDump.btnStartBreak_Click to avoid a double-popup.
                // See: https://github.com/Focus-Bear/windows-app-v2/issues/1006
            }
            else
            {
                //WE need to get the Sequnece ID that canbe used to force compplete the Sequence morning or evening if the user deactivates teh app for 4 hours
                appSettings.CurrentRunningSequenceID = (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS) ? appSettings.arrAllMorningHabits[appSettings.nHabitIndex].activity_sequence_id : ((appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS) ? appSettings.arrAllEveningHabits[appSettings.nHabitIndex].activity_sequence_id : "");

                //If the last activity was postponed after geting started, we must resume that
                string sPostponedActivityId = RegistryManager.GetCurrentActivityPostponedID();
                if (sPostponedActivityId != null && sPostponedActivityId.Length > 0)
                {
                    if (appSettings.bShowActivitiesFromMid)
                    {
                        //Increment the habit index to show the correct activity ongoing which was postponed
                        appSettings.nHabitIndex++;
                        LogWriter.LogWrite("nHabitIndex = " + appSettings.nHabitIndex.ToString());
                    }
                    string sActivityID = (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS) ? appSettings.arrAllMorningHabits[appSettings.nHabitIndex].id : ((appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS) ? appSettings.arrAllEveningHabits[appSettings.nHabitIndex].id : "");
                    if (sPostponedActivityId == sActivityID)
                    {

                        //Maybe the last aqctivity was a skipped activity but now we are starting a postponed activity..so adjust the vailable
                        appSettings.bShowActivitiesFromMidLastSkipped = false;
                        appSettings.bShowActivitiesFromMid = false;
                        //
                        //Means it was the same activity that was postponed after starting
                        ShowActivityStartScreen(true);
                        return;
                    }
                }

                if (appSettings.bShowActivitiesFromMid) //WE are showing some intermediate Activity Finished screen
                {
                    //Get the stats data from API
                    try
                    {
                        string sActivityID = (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS) ? appSettings.arrAllMorningHabits[appSettings.nHabitIndex].id : ((appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS) ? appSettings.arrAllEveningHabits[appSettings.nHabitIndex].id : "");
                        //Also get the activity data from server  as these will be used to show graph on finish screen
                        FocusBearAPIClient client = new FocusBearAPIClient();
                        _ = client.GetActivityStats(sActivityID, "");
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite(ex.Message);
                    }

                    ShowACtivityFinishedScreen();
                }
                else
                {
                    LogWriter.LogWrite("Show HabitsWelcomeList Page");
                    PageWelcomeList = new HabitsWelcomeList(ParentWindow);
                    AppFrameHabitScreens.Navigate(PageWelcomeList); //Normal process

                    ParentWindow.GridShade.Visibility = Visibility.Collapsed; //safety

                    AskToAnalyzeSleepAndMood();

                    if (appSettings.isExpired)
                    {
                        if (((appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS) && appSettings.arrGLOBALAllMorningHabits.Count > 3) ||
                            ((appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS) && appSettings.arrGLOBALAllEveningHabits.Count > 3)
                          )
                            ShowReducedHabitsMessageOnExpired();
                    }
                    else //Let's ask Only in registered version
                    {
                        if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                            AskConfirmationForLateHabits();
                    }
                }
            }
        }

        public void ShowHabitsPostponedView()
        {
            AppFrameHabitScreens.Visibility = Visibility.Visible;
            AppFrameHabitScreens.Navigate(PageHabitsPostponedView);
            btnPleaseSpareMe.Visibility = Visibility.Collapsed;
        }

        public void ShowMorningRoutineFinishedState()
        {
            PageNoHabitsRunningState.ShowMorningRoutineFinishedState();
            AppFrameHabitScreens.Visibility = Visibility.Visible;
            AppFrameHabitScreens.Navigate(PageNoHabitsRunningState);
            btnPleaseSpareMe.Visibility = Visibility.Collapsed;
        }
        public void ShowAppDeactivatedStateView()
        {
            PageNoHabitsRunningState.ShowAppDeactivatedStateView();
            AppFrameHabitScreens.Visibility = Visibility.Visible;
            AppFrameHabitScreens.Navigate(PageNoHabitsRunningState);
            btnPleaseSpareMe.Visibility = Visibility.Collapsed;
        }
        public void ShowShutoffAbortedView()
        {
            PageNoHabitsRunningState.ShowShutoffAbortedView();
            AppFrameHabitScreens.Visibility = Visibility.Visible;
            AppFrameHabitScreens.Navigate(PageNoHabitsRunningState);
            btnPleaseSpareMe.Visibility = Visibility.Collapsed;
        }
        public void ShowShutOffFreeTimeView()
        {
            PageNoHabitsRunningState.ShowShutOffFreeTimeView();
            AppFrameHabitScreens.Visibility = Visibility.Visible;
            AppFrameHabitScreens.Navigate(PageNoHabitsRunningState);
            btnPleaseSpareMe.Visibility = Visibility.Collapsed;
        }
        public void ShowMicroBreakStatusView()
        {
            PageNoHabitsRunningState.ShowMicroBreakStatusView();
            AppFrameHabitScreens.Visibility = Visibility.Visible;
            AppFrameHabitScreens.Navigate(PageNoHabitsRunningState);
            btnPleaseSpareMe.Visibility = Visibility.Collapsed;
        }
        public void ShowReducedHabitsMessageOnExpired()
        {
            if (dispatcherTimerShowReducedHabitsMessage == null)
            {
                dispatcherTimerShowReducedHabitsMessage = new DispatcherTimer(DispatcherPriority.Normal);
                dispatcherTimerShowReducedHabitsMessage.Tick += new EventHandler(dispatcherTimerShowReducedHabitsMessage_Tick);
                dispatcherTimerShowReducedHabitsMessage.Interval = new TimeSpan(0, 0, 1);
            }
            dispatcherTimerShowReducedHabitsMessage.Start();
        }

        private void dispatcherTimerShowReducedHabitsMessage_Tick(object sender, EventArgs e)
        {

            try
            {
                dispatcherTimerShowReducedHabitsMessage.Stop();
                CommandManager.InvalidateRequerySuggested();
                ParentWindow.GridShade.Visibility = Visibility.Visible;

                appSettings.CapturePostHog("alert-limited-usage-shown");

                string sTitle = appSettings.ConfigFileStrings["alertLimitedUsageVcInfo"]["title"].ToString().Replace("<COUNT>", "3");
                string sSubTitle = appSettings.ConfigFileStrings["alertLimitedUsageVcInfo"]["subTitle"].ToString().Replace("<COUNT>", "3");
                FBMessageBox ff = new FBMessageBox(sSubTitle, sTitle, MessageBoxButton.YesNo, appSettings.ConfigFileStrings["alertLimitedUsageVcInfo"]["buttonUpgradeNowText"].ToString(), appSettings.ConfigFileStrings["alertLimitedUsageVcInfo"]["buttonContinueText"].ToString());
                ff.Owner = ParentWindow;
                ff.ShowDialog();

                ParentWindow.GridShade.Visibility = Visibility.Collapsed;

                //if user has cancelled the update process
                if (ff.btnClicked == MessageBoxResult.Yes)
                {
                    appSettings.CapturePostHog("alert-limited-usage-button-upgrade-clicked");
                    ((App)(Application.Current)).ShowUpgradeToProNag("");
                }
                else
                {
                    appSettings.CapturePostHog("alert-limited-usage-button-continue-clicked");
                }

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("dispatcherTimerAskSleepAndMood_Tick ex = " + ex.Message);
            }

        }

        public void ShowStartBreakActivityScreen()
        {
            AppFrameHabitScreens.Navigate(new StartBreakActivity(ParentWindow));
        }
        public void ShowExtraPersonalFocus()
        {
            LogWriter.LogWrite("ShowExtraPersonalFocus()");

            PageHabitExtraPersonal = new HabitExtraPersonal(ParentWindow);
            AppFrameHabitScreens.Navigate(PageHabitExtraPersonal);
        }
        public void ShowActivityStartScreen(bool bStartingAfterPostponed = false)
        {
            LogWriter.LogWrite("ShowActivityStartScreen()");

            appSettings.dLastActivityPerformedStartTime = DateTime.Now; //Set the activity Start Time

            PageHabitActivityOnGoing = new HabitActivityOnGoing(ParentWindow, bStartingAfterPostponed);

            AppFrameHabitScreens.Navigate(PageHabitActivityOnGoing);
        }
        public void ShowACtivityFinishedScreen(bool bIsActivitySkipped = false)
        {
            LogWriter.LogWrite("ShowACtivityFinishedScreen();");

            try
            {
                //////////////////////////////////////////////////////////
                //On activity Finished, do clear the Postponed variables
                RegistryManager.RemovePostponedHabitData();

                //May be the timer small window is running, close that
                //Check here if the Breaks window is visible, we will hide it.
                if (((App)(Application.Current)).wndTakeABreak != null)
                {
                    ((App)(Application.Current)).wndTakeABreak.bShowingForNonTimerActivities = false;

                    if (((App)(Application.Current)).wndTakeABreak.IsVisible)
                    {
                        ((App)(Application.Current)).wndTakeABreak.Close();
                        ((App)(Application.Current)).wndTakeABreak = null;
                    }
                }

                //////////////////////////////////////////////////////////
                //Here we need to set the activity ongoing screen as null so that postpone timers can be handled as there we check if the object is not null
                if (PageHabitActivityOnGoing != null)
                {
                    PageHabitActivityOnGoing.webView.Dispose();
                }

                PageHabitActivityOnGoing = null;

                //we now have several questions. Initially the graph will be loaded for the first question.
                nScreenHabitActivityFinished.nQuestionIndexToShowGraphs = 0;

                AppFrameHabitScreens.Navigate(nScreenHabitActivityFinished);


                nScreenHabitActivityFinished.LoadData(appSettings.bSkipCurrentActivity);

                if (appSettings.bShowActivitiesFromMid)//Justload the graph data in this case
                {
                    return;
                }

                bool bLogQuantity = false;

                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                {
                    bLogQuantity = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].logQuantity;
                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    bLogQuantity = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].logQuantity;
                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                {
                    breakActivity currentActivity;
                    if (appSettings.isMandatoryActivityGoing)
                    {
                        currentActivity = appSettings.arrActivitiesIncludeInEveryBreak[appSettings.nHabitIndex];
                    }
                    else
                    {
                        currentActivity = appSettings.arrActivitiesExcludeInEveryBreak[appSettings.nHabitIndex];
                    }

                    if (!currentActivity.isRandomChoice)
                    {
                        bLogQuantity = currentActivity.logQuantity;
                    }
                    else
                    {
                        //random choice
                        var randomChoice = appSettings.currentActivityChoice;
                        if (randomChoice.id != null)
                        {
                            bLogQuantity = randomChoice.logQuantity;
                        }
                    }
                }


                if (bLogQuantity && !appSettings.bSkipCurrentActivityCantDoItToday)
                {
                    LogWriter.LogWrite("ACTIVITY Finished  - ShowLogWindow();");
                    ShowLogWindow();
                }
                else
                {
                    LogWriter.LogWrite("ACTIVITY Finished  - Log duration and return");

                    FocusBearAPIClient client = new FocusBearAPIClient();

                    //Log duration and return
                    string sLogType = "";
                    string sStartTime = appSettings.dLastActivityPerformedStartTime.ToString("yyyy-MM-dd HH:mm:ss");

                    if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                    {
                        int nDurationLogged = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].durationSeconds;
                        if (!appSettings.arrAllMorningHabits[appSettings.nHabitIndex].useTimer)
                        {
                            DateTime now = DateTime.Now;
                            TimeSpan difference = now.Subtract(ActivityStartTime.ToLocalTime());
                            var durationSeconds = difference.TotalSeconds;
                            nDurationLogged = Convert.ToInt32(durationSeconds);
                        }

                        sLogType = "morning routine";
                        appSettings.AddEntryInCSV(sLogType, appSettings.arrAllMorningHabits[appSettings.nHabitIndex].name, nDurationLogged.ToString(), appSettings.arrAllMorningHabits[appSettings.nHabitIndex].logQuantityQuestion, "", sStartTime);
                        if (appSettings.bSkipCurrentActivityCantDoItToday)
                        {
                            _ = client.PostActivitySkipped(appSettings.arrAllMorningHabits[appSettings.nHabitIndex].id, appSettings.arrAllMorningHabits[appSettings.nHabitIndex].activity_sequence_id);
                        }
                        else
                        {

                            _ = client.PostActivityCompleted(appSettings.arrAllMorningHabits[appSettings.nHabitIndex].id, appSettings.arrAllMorningHabits[appSettings.nHabitIndex].activity_sequence_id, ActivityStartTime, nDurationLogged, "", null, appSettings.bSkipCurrentActivity);
                        }
                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                    {
                        int nDurationLogged = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].durationSeconds;
                        if (!appSettings.arrAllEveningHabits[appSettings.nHabitIndex].useTimer)
                        {
                            DateTime now = DateTime.Now;
                            TimeSpan difference = now.Subtract(ActivityStartTime.ToLocalTime());
                            var durationSeconds = difference.TotalSeconds;
                            nDurationLogged = Convert.ToInt32(durationSeconds);
                        }

                        sLogType = "evening routine";
                        appSettings.AddEntryInCSV(sLogType, appSettings.arrAllEveningHabits[appSettings.nHabitIndex].name, nDurationLogged.ToString(), appSettings.arrAllEveningHabits[appSettings.nHabitIndex].logQuantityQuestion, "", sStartTime);
                        if (appSettings.bSkipCurrentActivityCantDoItToday)
                        {
                            _ = client.PostActivitySkipped(appSettings.arrAllEveningHabits[appSettings.nHabitIndex].id, appSettings.arrAllEveningHabits[appSettings.nHabitIndex].activity_sequence_id);
                        }
                        else
                        {
                            _ = client.PostActivityCompleted(appSettings.arrAllEveningHabits[appSettings.nHabitIndex].id, appSettings.arrAllEveningHabits[appSettings.nHabitIndex].activity_sequence_id, ActivityStartTime, nDurationLogged, "", null, appSettings.bSkipCurrentActivity);
                        }
                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                    {
                        sLogType = "break";
                        breakActivity currentActivity;
                        if (appSettings.isMandatoryActivityGoing)
                        {
                            currentActivity = appSettings.arrActivitiesIncludeInEveryBreak[appSettings.nHabitIndex];
                        }
                        else
                        {
                            currentActivity = appSettings.arrActivitiesExcludeInEveryBreak[appSettings.nHabitIndex];
                        }

                        int nDurationLogged = currentActivity.durationSeconds;
                        if (!currentActivity.useTimer)
                        {
                            DateTime now = DateTime.Now;
                            TimeSpan difference = now.Subtract(ActivityStartTime.ToLocalTime());
                            var durationSeconds = difference.TotalSeconds;
                            nDurationLogged = Convert.ToInt32(durationSeconds);
                        }

                        if (!currentActivity.isRandomChoice)
                        {
                            appSettings.AddEntryInCSV(sLogType, currentActivity.name, nDurationLogged.ToString(), currentActivity.logQuantityQuestion, "", sStartTime);
                            _ = client.PostActivityCompleted(currentActivity.id, currentActivity.activity_sequence_id, ActivityStartTime, nDurationLogged, "", null);
                        }
                        else
                        {
                            //random choice
                            var randomChoice = appSettings.currentActivityChoice;
                            if (randomChoice.id != null)
                            {
                                appSettings.AddEntryInCSV(sLogType, randomChoice.name, nDurationLogged.ToString(), randomChoice.logQuantityQuestion, "", sStartTime);
                                _ = client.PostActivityCompleted(currentActivity.id, currentActivity.activity_sequence_id, ActivityStartTime, nDurationLogged, randomChoice.id, null);
                            }
                        }

                    }

                    if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                    {
                        if (nScreenHabitActivityFinished.bWebViewInitialized && nScreenHabitActivityFinished.bWebView2Initialized)
                            nScreenHabitActivityFinished.LoadGraphData();
                        else
                            nScreenHabitActivityFinished.bCallLoadGraphDataAfterWebViewInitialized = true;
                    }
                    else
                    {
                        if (nScreenHabitActivityFinished.bWebViewInitialized)
                            nScreenHabitActivityFinished.LoadGraphData();
                        else
                            nScreenHabitActivityFinished.bCallLoadGraphDataAfterWebViewInitialized = true;
                    }

                }


            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowActivityFinishedScreen() Exception - " + ex.Message);
            }
        }


        public async void HideFrameAndClose()
        {
            appSettings.bShowActivitiesFromMid = false;//Reseting it

            //Dispose the web views
            if (nScreenHabitActivityFinished != null)
            {
                nScreenHabitActivityFinished.webView.Dispose();
                nScreenHabitActivityFinished.webView1.Dispose();
            }

            if (PageHabitActivityOnGoing != null)
            {
                PageHabitActivityOnGoing.webView.Dispose();
            }
            PageHabitActivityOnGoing = null;

            bool bMorningHabitsFinished = false;

            if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
            {
                LogWriter.LogWrite("LAST BREAK_ACTIVITY Finished");
                if (appSettings.bHabitsPostponedState)
                {
                    //possibly user postponed habits and started breaks, so we need to set the state back
                    if (appSettings.IsShutOffTime())
                    {
                        //evening routine time
                        appSettings.nCurrentActivityType = CurrentActivityType.MORNING_HABITS;
                    }
                    else
                    {
                        //morning routine time
                        appSettings.nCurrentActivityType = CurrentActivityType.MORNING_HABITS;
                    }

                }
                else
                {
                    //Set the current activity type to NONE
                    appSettings.nCurrentActivityType = CurrentActivityType.NONE;
                }

                //Start the break Activities Timer
                ((App)(Application.Current)).StartBreakActivityTimer();

                //enable the left buttons
                ((App)(Application.Current)).wndFocusBearMainWindow.EnableAllButtons();

                ((App)(Application.Current)).ShowPomodoroWindow(); //Lets show pomodoro view same as mac app

            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
            {
                LogWriter.LogWrite("LAST MORNING_HABITS Activity Finished");
                Heading.Text = appSettings.ConfigFileStrings["common"]["stayFocusedText"].ToString();
                AppFrameHabitScreens.Visibility = Visibility.Collapsed;

                //We need to hide the Please SPare Me Button
                btnPleaseSpareMe.Visibility = Visibility.Collapsed;

                await Task.Delay(1000);

                //WE will set the morning habits shown date for today
                RegistryManager.SetMorningHabitsShownTillDate();

                //Set the current activity type to NONE
                appSettings.nCurrentActivityType = CurrentActivityType.NONE;
                LogWriter.LogWrite(" appSettings.nCurrentActivityType = CurrentActivityType.NONE;");

                //Start the break Activities Timer
                ((App)(Application.Current)).StartBreakActivityTimer();

                bMorningHabitsFinished = true;
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
            {
                //We will set the EVening habits shown date till tomorrow's morning time
                LogWriter.LogWrite("LAST EVENING_HABITS Activity Finished");
                try
                {
                    TimeSpan tsShutOffTime = TimeSpan.Parse(appSettings.sDayShutOffTime);
                    DateTime now = DateTime.Now;
                    DateTime myShutOffTime = new DateTime(now.Year, now.Month, now.Day, tsShutOffTime.Hours, tsShutOffTime.Minutes, 00);
                    Console.WriteLine("{0}", myShutOffTime.ToString());


                    TimeSpan tsStartTime = TimeSpan.Parse(appSettings.sDayStartTime);
                    DateTime myStartTime = new DateTime(now.Year, now.Month, now.Day, tsStartTime.Hours, tsStartTime.Minutes, 00);
                    Console.WriteLine("{0}", myStartTime.ToString());

                    if (now > myShutOffTime) //Same Day like before 12.00 am
                    {
                        //Get the morning time and prepare a DT for shutoff aborted till
                        myStartTime = myStartTime.AddDays(1);
                        Console.WriteLine("{0}", myStartTime.ToString());
                        appSettings.EveningHabitsShownTillDate = myStartTime;
                    }
                    else //Next Day like after 12.00 am
                    {
                        appSettings.EveningHabitsShownTillDate = myStartTime;//Today's Morning time
                    }
                    LogWriter.LogWrite(string.Format("Set EveningHabitsShownTillDate - {0}", appSettings.EveningHabitsShownTillDate.ToString()));

                    RegistryManager.SetEveningHabitsShownTillDate();

                    //reset the complete evening habits button if it was clicked
                    appSettings.bCompleteYourEveningRoutineClicked = false;

                    //Set the current activity type to NONE
                    appSettings.nCurrentActivityType = CurrentActivityType.NONE;
                    LogWriter.LogWrite(" appSettings.nCurrentActivityType = CurrentActivityType.NONE;");

                    //Show the Shutoff Screen
                    if (appSettings.DidCutOffTimePassed())
                        ((App)(Application.Current)).ShowShutOffScreen();
                    else
                    {
                        ((App)(Application.Current)).wndFocusBearMainWindow.EnableAllButtons();
                        ((App)(Application.Current)).wndFocusBearMainWindow.Hide();
                    }

                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("HideFrameAndClose()" + ex.Message);
                }
            }

            if (bMorningHabitsFinished)
            {
                ((App)(Application.Current)).wndFocusBearMainWindow.EnableAllButtons();

                ((App)(Application.Current)).ShowSurvey();

                if (appSettings.bEnterPomodoroModeAfterMorningRoutine && appSettings.bIsGeekModeEnabled)
                {
                    ((App)(Application.Current)).EnterPomodoroModeAfterMorningRoutine();
                }
                else if (appSettings.AutoCloseAfterRoutineSkipPostpone)
                {
                    ((App)(Application.Current)).wndFocusBearMainWindow.Hide();
                }
                else
                {
                    ((App)(Application.Current)).ShowPomodoroWindow();
                }
            }
        }

        //GridLogQuantityNag Event Handlers
        public void ShowLogWindow()
        {

            LogWriter.LogWrite("ShowLogWindow()");
            bool bLogQuantity = false;

            if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
            {
                bLogQuantity = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].logQuantity;
                ListLogQuestions.ItemsSource = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].logQuantityQuestions;
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
            {
                bLogQuantity = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].logQuantity;
                ListLogQuestions.ItemsSource = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].logQuantityQuestions;
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
            {
                breakActivity currentActivity;
                if (appSettings.isMandatoryActivityGoing)
                {
                    currentActivity = appSettings.arrActivitiesIncludeInEveryBreak[appSettings.nHabitIndex];
                }
                else
                {
                    currentActivity = appSettings.arrActivitiesExcludeInEveryBreak[appSettings.nHabitIndex];
                }

                if (!currentActivity.isRandomChoice)
                {
                    bLogQuantity = currentActivity.logQuantity;
                    ListLogQuestions.ItemsSource = currentActivity.logQuantityQuestions;
                }
                else
                {
                    //random choice
                    var randomChoice = appSettings.currentActivityChoice;
                    if (randomChoice.id != null)
                    {
                        bLogQuantity = randomChoice.logQuantity;
                        ListLogQuestions.ItemsSource = randomChoice.logQuantityQuestions;
                    }
                }
            }

            nScreenHabitActivityFinished.webView.Visibility = Visibility.Collapsed;

            GridShade.Visibility = Visibility.Visible;
            GridLogQuantityNag.Visibility = Visibility.Visible;

            string sLogQuantityQuestion = "";
            if (bLogQuantity)
            {

                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                {
                    sLogQuantityQuestion = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].logQuantityQuestion;
                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    sLogQuantityQuestion = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].logQuantityQuestion;
                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                {
                    breakActivity currentActivity;
                    if (appSettings.isMandatoryActivityGoing)
                    {
                        currentActivity = appSettings.arrActivitiesIncludeInEveryBreak[appSettings.nHabitIndex];
                    }
                    else
                    {
                        currentActivity = appSettings.arrActivitiesExcludeInEveryBreak[appSettings.nHabitIndex];
                    }

                    if (!currentActivity.isRandomChoice)
                    {
                        sLogQuantityQuestion = currentActivity.logQuantityQuestion;
                    }
                    else
                    {
                        //random choice
                        var randomChoice = appSettings.currentActivityChoice;
                        if (randomChoice.id != null)
                        {
                            sLogQuantityQuestion = randomChoice.logQuantityQuestion;
                        }
                    }
                }
            }
            if (sLogQuantityQuestion.Length > 0)
            {

            }
            else
            {
                sLogQuantityQuestion = appSettings.ConfigFileStrings["logQuantityInfo"]["logQuantityQuestionText"].ToString();

                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                {
                    sLogQuantityQuestion = sLogQuantityQuestion.Replace("<activity>", appSettings.arrAllMorningHabits[appSettings.nHabitIndex].name);
                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    sLogQuantityQuestion = sLogQuantityQuestion.Replace("<activity>", appSettings.arrAllEveningHabits[appSettings.nHabitIndex].name);
                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                {

                    breakActivity currentActivity;
                    if (appSettings.isMandatoryActivityGoing)
                    {
                        currentActivity = appSettings.arrActivitiesIncludeInEveryBreak[appSettings.nHabitIndex];
                    }
                    else
                    {
                        currentActivity = appSettings.arrActivitiesExcludeInEveryBreak[appSettings.nHabitIndex];
                    }

                    if (!currentActivity.isRandomChoice)
                    {
                        sLogQuantityQuestion = sLogQuantityQuestion.Replace("<activity>", currentActivity.name);
                    }
                    else
                    {
                        //random choice
                        var randomChoice = appSettings.currentActivityChoice;
                        if (randomChoice.id != null)
                        {
                            sLogQuantityQuestion = sLogQuantityQuestion.Replace("<activity>", randomChoice.name);
                        }
                    }

                }
            }
            HeadingLogQuantity.Text = appSettings.ConfigFileStrings["logQuantityInfo"]["title"].ToString();
            btnCancelText.Text = appSettings.ConfigFileStrings["logQuantityInfo"]["buttonCancelText"].ToString();
            btnOKText.Text = appSettings.ConfigFileStrings["logQuantityInfo"]["buttonOkText"].ToString();

            //Hide the Graph again
            nScreenHabitActivityFinished.webView.Visibility = Visibility.Collapsed;
            nScreenHabitActivityFinished.BreakGraph2.Visibility = Visibility.Collapsed;

        }
        public void HideLogWindow()
        {
            LogWriter.LogWrite("HideLogWindow()");

            GridShade.Visibility = Visibility.Collapsed;
            GridLogQuantityNag.Visibility = Visibility.Collapsed;

            //We will need to show the webview that we had hidden when showing this box
            if (nScreenHabitActivityFinished != null)
            {
                nScreenHabitActivityFinished.webView.Visibility = Visibility.Visible;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnCancel_Click()");
            try
            {
                string sLogType = "";
                string sStartTime = appSettings.dLastActivityPerformedStartTime.ToString("yyyy-MM-dd HH:mm:ss");

                {
                    FocusBearAPIClient client = new FocusBearAPIClient();

                    if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                    {
                        int nDurationLogged = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].durationSeconds;
                        if (!appSettings.arrAllMorningHabits[appSettings.nHabitIndex].useTimer)
                        {
                            DateTime now = DateTime.Now;
                            TimeSpan difference = now.Subtract(ActivityStartTime.ToLocalTime());
                            var durationSeconds = difference.TotalSeconds;
                            nDurationLogged = Convert.ToInt32(durationSeconds);
                        }

                        sLogType = "morning routine";
                        appSettings.AddEntryInCSV(sLogType, appSettings.arrAllMorningHabits[appSettings.nHabitIndex].name, nDurationLogged.ToString(), appSettings.arrAllMorningHabits[appSettings.nHabitIndex].logQuantityQuestion, "", sStartTime);
                        _ = client.PostActivityCompleted(appSettings.arrAllMorningHabits[appSettings.nHabitIndex].id, appSettings.arrAllMorningHabits[appSettings.nHabitIndex].activity_sequence_id, ActivityStartTime, nDurationLogged, "", null, appSettings.bSkipCurrentActivity);

                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                    {
                        int nDurationLogged = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].durationSeconds;
                        if (!appSettings.arrAllEveningHabits[appSettings.nHabitIndex].useTimer)
                        {
                            DateTime now = DateTime.Now;
                            TimeSpan difference = now.Subtract(ActivityStartTime.ToLocalTime());
                            var durationSeconds = difference.TotalSeconds;
                            nDurationLogged = Convert.ToInt32(durationSeconds);
                        }


                        sLogType = "evening routine";
                        appSettings.AddEntryInCSV(sLogType, appSettings.arrAllEveningHabits[appSettings.nHabitIndex].name, nDurationLogged.ToString(), appSettings.arrAllEveningHabits[appSettings.nHabitIndex].logQuantityQuestion, "", sStartTime);
                        _ = client.PostActivityCompleted(appSettings.arrAllEveningHabits[appSettings.nHabitIndex].id, appSettings.arrAllEveningHabits[appSettings.nHabitIndex].activity_sequence_id, ActivityStartTime, nDurationLogged, "", null, appSettings.bSkipCurrentActivity);

                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                    {
                        sLogType = "break";
                        breakActivity currentActivity;
                        if (appSettings.isMandatoryActivityGoing)
                        {
                            currentActivity = appSettings.arrActivitiesIncludeInEveryBreak[appSettings.nHabitIndex];
                        }
                        else
                        {
                            currentActivity = appSettings.arrActivitiesExcludeInEveryBreak[appSettings.nHabitIndex];
                        }

                        int nDurationLogged = currentActivity.durationSeconds;
                        if (!currentActivity.useTimer)
                        {
                            DateTime now = DateTime.Now;
                            TimeSpan difference = now.Subtract(ActivityStartTime.ToLocalTime());
                            var durationSeconds = difference.TotalSeconds;
                            nDurationLogged = Convert.ToInt32(durationSeconds);
                        }

                        if (!currentActivity.isRandomChoice)
                        {
                            appSettings.AddEntryInCSV(sLogType, currentActivity.name, nDurationLogged.ToString(), currentActivity.logQuantityQuestion, "", sStartTime);
                            _ = client.PostActivityCompleted(currentActivity.id, currentActivity.activity_sequence_id, ActivityStartTime, nDurationLogged, "");

                        }
                        else
                        {
                            //random choice
                            var randomChoice = appSettings.currentActivityChoice;
                            if (randomChoice.id != null)
                            {
                                appSettings.AddEntryInCSV(sLogType, randomChoice.name, nDurationLogged.ToString(), randomChoice.logQuantityQuestion, "", sStartTime);
                                _ = client.PostActivityCompleted(currentActivity.id, currentActivity.activity_sequence_id, ActivityStartTime, nDurationLogged, randomChoice.id);

                            }
                        }
                    }
                }

                HideLogWindow();

                //Load the Graph DAta
                if (nScreenHabitActivityFinished.bWebViewInitialized)
                    nScreenHabitActivityFinished.LoadGraphData();
                else
                    nScreenHabitActivityFinished.bCallLoadGraphDataAfterWebViewInitialized = true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Log Quantitiy btnCancel_Click ex - " + ex.Message);
            }
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnOK_Click() on log quantity window");
            try
            {
                string sLogType = "";
                string sStartTime = appSettings.dLastActivityPerformedStartTime.ToString("yyyy-MM-dd HH:mm:ss");
                {

                    FocusBearAPIClient client = new FocusBearAPIClient();

                    if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                    {
                        int nDurationLogged = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].durationSeconds;
                        if (!appSettings.arrAllMorningHabits[appSettings.nHabitIndex].useTimer)
                        {
                            DateTime now = DateTime.Now;
                            TimeSpan difference = now.Subtract(ActivityStartTime.ToLocalTime());
                            var durationSeconds = difference.TotalSeconds;
                            nDurationLogged = Convert.ToInt32(durationSeconds);
                        }

                        sLogType = "morning routine";
                        appSettings.AddEntryInCSV(sLogType, appSettings.arrAllMorningHabits[appSettings.nHabitIndex].name, nDurationLogged.ToString(), appSettings.arrAllMorningHabits[appSettings.nHabitIndex].logQuantityQuestion, /*dQuantityLogged.ToString()*/"", sStartTime);
                        _ = client.PostActivityCompleted(appSettings.arrAllMorningHabits[appSettings.nHabitIndex].id, appSettings.arrAllMorningHabits[appSettings.nHabitIndex].activity_sequence_id, ActivityStartTime, nDurationLogged, "", /*dQuantityLogged*/appSettings.arrAllMorningHabits[appSettings.nHabitIndex].logQuantityQuestions, appSettings.bSkipCurrentActivity);
                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                    {
                        int nDurationLogged = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].durationSeconds;
                        if (!appSettings.arrAllEveningHabits[appSettings.nHabitIndex].useTimer)
                        {
                            DateTime now = DateTime.Now;
                            TimeSpan difference = now.Subtract(ActivityStartTime.ToLocalTime());
                            var durationSeconds = difference.TotalSeconds;
                            nDurationLogged = Convert.ToInt32(durationSeconds);
                        }
                        sLogType = "evening routine";
                        appSettings.AddEntryInCSV(sLogType, appSettings.arrAllEveningHabits[appSettings.nHabitIndex].name, nDurationLogged.ToString(), appSettings.arrAllEveningHabits[appSettings.nHabitIndex].logQuantityQuestion,/* dQuantityLogged.ToString()*/"", sStartTime);
                        _ = client.PostActivityCompleted(appSettings.arrAllEveningHabits[appSettings.nHabitIndex].id, appSettings.arrAllEveningHabits[appSettings.nHabitIndex].activity_sequence_id, ActivityStartTime, nDurationLogged, "", /*dQuantityLogged*/appSettings.arrAllEveningHabits[appSettings.nHabitIndex].logQuantityQuestions, appSettings.bSkipCurrentActivity);
                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                    {
                        sLogType = "break";

                        breakActivity currentActivity;
                        if (appSettings.isMandatoryActivityGoing)
                        {
                            currentActivity = appSettings.arrActivitiesIncludeInEveryBreak[appSettings.nHabitIndex];
                        }
                        else
                        {
                            currentActivity = appSettings.arrActivitiesExcludeInEveryBreak[appSettings.nHabitIndex];
                        }

                        int nDurationLogged = currentActivity.durationSeconds;
                        if (!currentActivity.useTimer)
                        {
                            DateTime now = DateTime.Now;
                            TimeSpan difference = now.Subtract(ActivityStartTime.ToLocalTime());
                            var durationSeconds = difference.TotalSeconds;
                            nDurationLogged = Convert.ToInt32(durationSeconds);
                        }

                        if (!currentActivity.isRandomChoice)
                        {
                            appSettings.AddEntryInCSV(sLogType, currentActivity.name, nDurationLogged.ToString(), currentActivity.logQuantityQuestion, /*dQuantityLogged.ToString()*/"", sStartTime);
                            _ = client.PostActivityCompleted(currentActivity.id, currentActivity.activity_sequence_id, ActivityStartTime, nDurationLogged, "", /*dQuantityLogged*/ currentActivity.logQuantityQuestions);
                        }
                        else
                        {
                            //random choice
                            var randomChoice = appSettings.currentActivityChoice;
                            if (randomChoice.id != null)
                            {
                                appSettings.AddEntryInCSV(sLogType, randomChoice.name, nDurationLogged.ToString(), randomChoice.logQuantityQuestion,/* dQuantityLogged.ToString()*/"", sStartTime);
                                _ = client.PostActivityCompleted(currentActivity.id, currentActivity.activity_sequence_id, ActivityStartTime, nDurationLogged, randomChoice.id, /*dQuantityLogged*/ randomChoice.logQuantityQuestions);
                            }
                        }

                    }
                }

                HideLogWindow();

                //Load the Graph Data
                if (nScreenHabitActivityFinished.bWebViewInitialized)
                    nScreenHabitActivityFinished.LoadGraphData();
                else
                    nScreenHabitActivityFinished.bCallLoadGraphDataAfterWebViewInitialized = true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnOK_Click ex- " + ex.Message);
            }
        }

        private void Quantity_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("^[.][0-9]+$|^[0-9]*[.]{0,1}[0-9]*$");
            e.Handled = !regex.IsMatch((sender as TextBox).Text.Insert((sender as TextBox).SelectionStart, e.Text));
            if (e.Handled)
            {
            }
            else
            {
            }
        }

        private void Quantity_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = false;
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private void Quantity_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.Delete) || (e.Key == Key.Back))
            {
            }
        }

        public void SimulatePleaseSpareMeClick()
        {
            LogWriter.LogWrite("Habit Window SimulatePleaseSpareMeClick()");

            ButtonAutomationPeer peer = new ButtonAutomationPeer(btnPleaseSpareMe);
            IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            invokeProv.Invoke();
        }
        private async void btnPleaseSpareMe_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Habit Window btnPleaseSpareMe_Click Clicked");
            //WE may have edit habits screen visible and user may have clicked Backt o my habits
            //in that case we need to check and return from here
            //If edits habits is open in welcome list, hide it
            if (PageWelcomeList != null)
            {
                if (PageWelcomeList.WebGrid.IsVisible)
                {
                    string GetCheckUserSettingsModifiedDirectReturn = @"
                                        (function() {
                                            try {
                                                const data = window?.hasUserModifiedSettings?.();
                                                return data?.isUserSettingsModified ?? false;
                                            } catch (e) {
                                                return false;
                                            }
                                        })();
                                        ";
                    string jsResult = await PageWelcomeList.webView.ExecuteScriptAsync(GetCheckUserSettingsModifiedDirectReturn);

                    bool hasModified = System.Text.Json.JsonSerializer.Deserialize<bool>(jsResult);
                    if (hasModified)
                    {
                        FBMessageBox messageBox = new FBMessageBox(appSettings.ConfigFileStrings["settings"]["settingsEditedButNotSavedText"].ToString(), "Focus Bear", MessageBoxButton.YesNo, appSettings.ConfigFileStrings["settings"]["buttonReviewSettingsText"].ToString(), appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString());
                        messageBox.Owner = ParentWindow;
                        PageWelcomeList.WebGrid.Visibility = Visibility.Collapsed;
                        ParentWindow.GridShade.Visibility = Visibility.Visible;
                        messageBox.ShowDialog();
                        ParentWindow.GridShade.Visibility = Visibility.Collapsed;
                        PageWelcomeList.WebGrid.Visibility = Visibility.Visible;
                        if (messageBox.btnClicked == MessageBoxResult.Yes)
                        {
                            return;
                        }
                    }

                    PageWelcomeList.WebGrid.Visibility = Visibility.Collapsed;
                    PageWelcomeList.Normal.Visibility = Visibility.Visible;

                    if (!appSettings.bPlayActivitiesInFullScreenMode)
                    {
                        btnPleaseSpareMeTXT.Text = appSettings.ConfigFileStrings["common"]["buttonPostponeText"].ToString();
                    }
                    else
                    {
                        btnPleaseSpareMeTXT.Text = appSettings.ConfigFileStrings["common"]["buttonAbortText"].ToString();
                    }

                    return;
                }
            }
            //If a password is set, ask fop a password

            if (appSettings.isPasswordSet)
            {
                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                {
                    if (appSettings.bPasswordRequireInSkipMorning)
                    {
                        //It might be possible that a webview is visible, we will need to hide that
                        if (nScreenHabitActivityFinished != null)
                        {
                            if (nScreenHabitActivityFinished.webView.IsVisible)
                                nScreenHabitActivityFinished.webView.Visibility = Visibility.Collapsed;
                            if (nScreenHabitActivityFinished.webView1.IsVisible)
                                nScreenHabitActivityFinished.webView1.Visibility = Visibility.Collapsed;
                        }

                        //If edits habits is open in welcome list, hide it
                        bool bshowPageWelcomeListWebView = false;
                        if (PageWelcomeList != null)
                        {
                            if (PageWelcomeList.webView.IsVisible)
                            {
                                PageWelcomeList.webView.Visibility = Visibility.Collapsed;
                                bshowPageWelcomeListWebView = true;
                            }
                        }

                        if (PageHabitExtraPersonal != null)
                        {
                            if (appSettings.bMotivationalMessageEnabled)
                                PageHabitExtraPersonal.webView.Visibility = Visibility.Collapsed;
                        }

                        if (PageHabitActivityOnGoing != null)
                        {
                            if (PageHabitActivityOnGoing.webView.IsVisible)
                            {
                                PageHabitActivityOnGoing.webView.Visibility = Visibility.Collapsed;
                            }
                        }
                        AskPassword pwdWnd = new AskPassword();
                        pwdWnd.Owner = ParentWindow;
                        ParentWindow.GridShade.Visibility = Visibility.Visible;

                        bool? bResult = pwdWnd.ShowDialog();

                        ParentWindow.GridShade.Visibility = Visibility.Collapsed;
                        if (nScreenHabitActivityFinished != null)
                        {
                            if (!nScreenHabitActivityFinished.webView.IsVisible)
                                nScreenHabitActivityFinished.webView.Visibility = Visibility.Visible;
                            if (!nScreenHabitActivityFinished.webView1.IsVisible)
                                nScreenHabitActivityFinished.webView1.Visibility = Visibility.Visible;
                        }

                        //If edits habits is open in welcome list, hide it
                        if (PageWelcomeList != null)
                        {
                            if (bshowPageWelcomeListWebView)
                                PageWelcomeList.webView.Visibility = Visibility.Visible;
                        }

                        if (PageHabitExtraPersonal != null)
                        {
                            if (appSettings.bMotivationalMessageEnabled)
                                PageHabitExtraPersonal.webView.Visibility = Visibility.Visible;
                        }
                        if (PageHabitActivityOnGoing != null)
                        {
                            if (!PageHabitActivityOnGoing.webView.IsVisible)
                            {
                                PageHabitActivityOnGoing.webView.Visibility = Visibility.Visible;
                            }
                        }
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
                else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    if (appSettings.bPasswordRequireInSkipEvening || (appSettings.DidCutOffTimePassed() && appSettings.bPasswordRequireToSkipEveningAfterCutOff))
                    {
                        //It might be possible that a webview is visible, we will need to hide that
                        if (nScreenHabitActivityFinished != null)
                        {
                            if (nScreenHabitActivityFinished.webView.IsVisible)
                                nScreenHabitActivityFinished.webView.Visibility = Visibility.Collapsed;
                            if (nScreenHabitActivityFinished.webView1.IsVisible)
                                nScreenHabitActivityFinished.webView1.Visibility = Visibility.Collapsed;
                        }

                        //If edits habits is open in welcome list, hide it
                        bool bshowPageWelcomeListWebView = false;
                        if (PageWelcomeList != null)
                        {
                            if (PageWelcomeList.webView.IsVisible)
                            {
                                PageWelcomeList.webView.Visibility = Visibility.Collapsed;
                                bshowPageWelcomeListWebView = true;
                            }
                        }

                        if (PageHabitExtraPersonal != null)
                        {
                            if (appSettings.bMotivationalMessageEnabled)
                                PageHabitExtraPersonal.webView.Visibility = Visibility.Collapsed;
                        }

                        if (PageHabitActivityOnGoing != null)
                        {
                            if (PageHabitActivityOnGoing.webView.IsVisible)
                            {
                                PageHabitActivityOnGoing.webView.Visibility = Visibility.Collapsed;
                            }
                        }

                        AskPassword pwdWnd = new AskPassword();
                        pwdWnd.Owner = ParentWindow;
                        ParentWindow.GridShade.Visibility = Visibility.Visible;

                        bool? bResult = pwdWnd.ShowDialog();

                        ParentWindow.GridShade.Visibility = Visibility.Collapsed;
                        if (nScreenHabitActivityFinished != null)
                        {
                            if (!nScreenHabitActivityFinished.webView.IsVisible)
                                nScreenHabitActivityFinished.webView.Visibility = Visibility.Visible;
                            if (!nScreenHabitActivityFinished.webView1.IsVisible)
                                nScreenHabitActivityFinished.webView1.Visibility = Visibility.Visible;
                        }
                        if (PageHabitActivityOnGoing != null)
                        {
                            if (!PageHabitActivityOnGoing.webView.IsVisible)
                            {
                                PageHabitActivityOnGoing.webView.Visibility = Visibility.Visible;
                            }
                        }
                        //If edits habits is open in welcome list, hide it
                        if (PageWelcomeList != null)
                        {
                            if (bshowPageWelcomeListWebView)
                                PageWelcomeList.webView.Visibility = Visibility.Visible;
                        }

                        if (PageHabitExtraPersonal != null)
                        {
                            if (appSettings.bMotivationalMessageEnabled)
                                PageHabitExtraPersonal.webView.Visibility = Visibility.Visible;
                        }

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

            if (PageHabitActivityOnGoing != null)
            {
                PageHabitActivityOnGoing.HandleTimerForPostpone(true); //Stop the timer
            }
            LogWriter.LogWrite("Habit Window btnPleaseSpareMe_Click Clicked - Hide() now");

            //Show the Easy Postpone 
            ((App)(Application.Current)).ShowEasyPostponeWindow();

        }

        public void EasyPostponeCancelled()
        {
            if (PageHabitActivityOnGoing != null)
            {
                PageHabitActivityOnGoing.HandleTimerForPostpone(false); //start the timer
            }
        }
        public void ShowWhySkipHabitsWindow()
        {
            LogWriter.LogWrite("ShowWhySkipHabitsWindow()");

            if (PageHabitExtraPersonal != null)
            {
                if (appSettings.bMotivationalMessageEnabled)
                    PageHabitExtraPersonal.webView.Visibility = Visibility.Collapsed;
            }
            if (nScreenHabitActivityFinished != null)
            {
                if (nScreenHabitActivityFinished.webView.IsVisible)
                    nScreenHabitActivityFinished.webView.Visibility = Visibility.Collapsed;
            }

            appSettings.bSkipCurrentActivity = false; //initiating, will be true after showing the nag for why do you want to skip
            appSettings.bSkipCurrentActivityCantDoItToday = false;

            LogWriter.LogWrite("appSettings.bSkipCurrentActivityCantDoItToday = false;");

            HeadingWhySkip.Text = appSettings.ConfigFileStrings["whySkipHabitVcInfo"]["title"].ToString();
            btnAlreadyDidItTXT.Text = appSettings.ConfigFileStrings["whySkipHabitVcInfo"]["buttonAlreadyDidItText"].ToString();
            btnCantDoItTodayTXT.Text = appSettings.ConfigFileStrings["whySkipHabitVcInfo"]["buttonCanNotDoItText"].ToString();

            // Reason input — clear on each show and set placeholder label from config
            TxtSkipReason.Text = string.Empty;
            try
            {
                TxtSkipReasonLabel.Text = appSettings.ConfigFileStrings["whySkipHabitVcInfo"]["reasonPlaceholderText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowWhySkipHabitsWindow fallback text - " + ex.Message);
                TxtSkipReasonLabel.Text = "Optional: add a reason";
            }

            GridShade.Visibility = Visibility.Visible;
            GridWhyDoYouWantToSkip.Visibility = Visibility.Visible;

        }
        public void HideWhySkipHabitsWindow()
        {
            LogWriter.LogWrite("HideWhySkipHabitsWindow()");

            GridShade.Visibility = Visibility.Collapsed;
            GridWhyDoYouWantToSkip.Visibility = Visibility.Collapsed;
            TxtSkipReason.Text = string.Empty; // Clear reason so it doesn't persist between habits

            if (PageHabitExtraPersonal != null)
            {
                if (appSettings.bMotivationalMessageEnabled)
                    PageHabitExtraPersonal.webView.Visibility = Visibility.Visible;
            }
            if (nScreenHabitActivityFinished != null)
            {
                if (!nScreenHabitActivityFinished.webView.IsVisible)
                    nScreenHabitActivityFinished.webView.Visibility = Visibility.Visible;
            }

        }
        public void ShowSkippingHabitInWindow()
        {
            LogWriter.LogWrite("ShowSkippingHabitInWindow()");
            HeadingSkippingIn.Text = appSettings.ConfigFileStrings["skipHabitVcInfo"]["title"].ToString();
            HeadingSkippingInTimer.Text = "00:05";
            btnChangedMyMindTXT.Text = appSettings.ConfigFileStrings["skipHabitVcInfo"]["buttonDoHabitsText"].ToString(); ;

            GridShade.Visibility = Visibility.Visible;
            GridSkippingHabitIn.Visibility = Visibility.Visible;

            if (PageHabitExtraPersonal != null)
            {
                if (appSettings.bMotivationalMessageEnabled)
                    PageHabitExtraPersonal.webView.Visibility = Visibility.Collapsed;
            }

            if (nScreenHabitActivityFinished != null)
            {
                if (nScreenHabitActivityFinished.webView.IsVisible)
                    nScreenHabitActivityFinished.webView.Visibility = Visibility.Collapsed;
            }

            nTimer = 5;
            dispatcherTimerSkippingIn.Start();
            CommandManager.InvalidateRequerySuggested();

        }
        public void HideSkippingHabitInWindow()
        {
            LogWriter.LogWrite("HideSkippingHabitInWindow()");
            try
            {
                GridShade.Visibility = Visibility.Collapsed;
                GridSkippingHabitIn.Visibility = Visibility.Collapsed;
                //We will need to show the webview that we had hidden when showing this box
                if (nScreenHabitActivityFinished != null)
                {
                    if (!nScreenHabitActivityFinished.webView.IsVisible)
                        nScreenHabitActivityFinished.webView.Visibility = Visibility.Visible;
                }
                if (PageHabitExtraPersonal != null)
                {
                    if (appSettings.bMotivationalMessageEnabled)
                        PageHabitExtraPersonal.webView.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("HideSkippingHabitInWindow() ex = " + ex.Message);
            }
        }
        private void btnAlreadyDidIt_Click(object sender, RoutedEventArgs e)
        {
            //Proceed to show log window if its a log activity
            //if not skip and move to next activity
            LogWriter.LogWrite("btnAlreadyDidIt_Click()");

            // Capture optional skip reason before hiding the window
            string sReason = TxtSkipReason.Text?.Trim() ?? string.Empty;
            // Use JsonConvert to properly escape all special chars (backslash, quotes, control chars, etc.)
            string sReasonSafe = Newtonsoft.Json.JsonConvert.SerializeObject(sReason).Trim('"');
            string sHabitName = appSettings.currentHabitPlaying.name ?? string.Empty;
            string sHabitNameSafe = Newtonsoft.Json.JsonConvert.SerializeObject(sHabitName).Trim('"');
            appSettings.CapturePostHog("skip-habit-already-did-it",
                $"\"habit_name\":\"{sHabitNameSafe}\",\"reason\":\"{sReasonSafe}\"");
            LogWriter.LogWrite("btnAlreadyDidIt_Click() reason=" + sReason);

            HideWhySkipHabitsWindow();
            appSettings.bSkipCurrentActivity = true;

            if (!appSettings.bSkipWhileActivityOngoing) //Increase the index only if the activity was skipped other than when it was ongoing.
            {
                appSettings.nHabitIndex++; //Increment the index to do the next habit
                LogWriter.LogWrite("nHabitIndex = " + appSettings.nHabitIndex.ToString());
            }

            ShowACtivityFinishedScreen(true);
        }

        private void btnCantDoItToday_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnCantDoItToday_Click()");

            // Capture optional skip reason before hiding the window
            string sReason = TxtSkipReason.Text?.Trim() ?? string.Empty;
            // Use JsonConvert to properly escape all special chars (backslash, quotes, control chars, etc.)
            string sReasonSafe = Newtonsoft.Json.JsonConvert.SerializeObject(sReason).Trim('"');
            string sHabitName = appSettings.currentHabitPlaying.name ?? string.Empty;
            string sHabitNameSafe = Newtonsoft.Json.JsonConvert.SerializeObject(sHabitName).Trim('"');
            appSettings.CapturePostHog("skip-habit-cant-do-today",
                $"\"habit_name\":\"{sHabitNameSafe}\",\"reason\":\"{sReasonSafe}\"");
            LogWriter.LogWrite("btnCantDoItToday_Click() reason=" + sReason);

            //Hide why skip and show the skipping-in window
            HideWhySkipHabitsWindow();

            ShowSkippingHabitInWindow();
            //Work to skip the habit

        }
        private void btnGridWhyDoYouWantToSkipClose_Click(object sender, RoutedEventArgs e)
        {
            HideWhySkipHabitsWindow();
        }
        private void btnChangedMyMind_Click(object sender, RoutedEventArgs e)
        {
            appSettings.bSkipWhileActivityOngoing = false;
            HideSkippingHabitInWindow();
            dispatcherTimerSkippingIn.Stop(); //Stop the timer as user had cancelled skipping
        }


        private void dispatcherTimerShowDateTime_Tick(object sender, EventArgs e)
        {
            if (appSettings.bPlayActivitiesInFullScreenMode && !appSettings.bHabitsPostponedState && appSettings.nCurrentActivityType != CurrentActivityType.NONE)
            {
                TodaysTime.Visibility = Visibility.Visible;
            }
            else
            {
                TodaysTime.Visibility = Visibility.Collapsed;
                return;
            }
            TodaysTime.Text = DateTime.Now.ToString(CultureInfo.CurrentCulture.DateTimeFormat.FullDateTimePattern);
            CommandManager.InvalidateRequerySuggested();
        }

        private void dispatcherTimerSkippingIn_Tick(object sender, EventArgs e)
        {
#if DEBUG
            int val = nTimer--;
#else
            int val = nTimer--;
#endif

            try
            {

                HeadingSkippingInTimer.Text = appSettings.SecondsToTimerText(nTimer);
                CommandManager.InvalidateRequerySuggested();

                if (nTimer < 0)
                {
                    dispatcherTimerSkippingIn.Stop();

                    HideSkippingHabitInWindow();
                    appSettings.bSkipCurrentActivityCantDoItToday = true;

                    LogWriter.LogWrite("appSettings.bSkipCurrentActivityCantDoItToday = true;");

                    if (!appSettings.bSkipWhileActivityOngoing) //Increase the index only if the activity was skipped other than when it was ongoing.
                    {
                        appSettings.nHabitIndex++; //Increment the index to do the next habit
                        LogWriter.LogWrite("nHabitIndex = " + appSettings.nHabitIndex.ToString());
                    }
                    ShowACtivityFinishedScreen(true);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("dispatcherTimerSkippingIn_Tick ex = " + ex.Message);
            }

        }
        public void HideCloseButton()//need to hide the close button if is the last activity finished screen
        {
            if (appSettings.bPlayActivitiesInFullScreenMode)
            {
                return;
            }
        }

        private void AskToAnalyzeSleepAndMood()
        {
            if (!RegistryManager.GetAnalyzeSleepAndMoodStatus())
            {
                //we need to check if 7 days have passed the installed date, if yet, need to who the survey window.
                DateTime now = DateTime.Now;
                TimeSpan difference = now.Subtract(appSettings.FirstAppInstallDT);

                var nDays = difference.Days;

                if (nDays >= 2)
                {
                    dispatcherTimerAskSleepAndMood.Tick += new EventHandler(dispatcherTimerAskSleepAndMood_Tick);
                    dispatcherTimerAskSleepAndMood.Interval = new TimeSpan(0, 0, 1);
                    dispatcherTimerAskSleepAndMood.Start();
                }
            }
        }

        private void AskConfirmationForLateHabits()
        {
            try
            {
                LogWriter.LogWrite("AskConfirmationForLateHabits()");

                DateTime now = DateTime.Now;
                TimeSpan tsStartTime = TimeSpan.Parse(appSettings.sDayStartTime);
                DateTime myStartTime = new DateTime(now.Year, now.Month, now.Day, tsStartTime.Hours, tsStartTime.Minutes, 00);
                Console.WriteLine("{0}", myStartTime.ToString());

                if (now > myStartTime.AddHours(4)) //if more then 4 hours have elapsed then startup time
                {
                    /* "lateMorningHabitPopupVcInfo": {
                        "title": "Hiya, do you still want to do your morning routine today or skip it?",
                        "subTitle": "(Asking because it's <HOUR> hours since your routine start time and it might not make sense to do it today)",
                        "buttonSkipText": "Skip it",
                        "buttonDoHabitsText": "Do my habits"
                    },*/
                    var timer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(2)
                    };

                    timer.Tick += (s, e) =>
                    {
                        timer.Stop(); // Stop timer after it fires once

                        string title = appSettings.ConfigFileStrings["lateMorningHabitPopupVcInfo"]["title"].ToString();
                        string subTitle = appSettings.ConfigFileStrings["lateMorningHabitPopupVcInfo"]["subTitle"].ToString();
                        string buttonDo = appSettings.ConfigFileStrings["lateMorningHabitPopupVcInfo"]["buttonDoHabitsText"].ToString();
                        string buttonSkip = appSettings.ConfigFileStrings["lateMorningHabitPopupVcInfo"]["buttonSkipText"].ToString();

                        // Calculate the time difference
                        TimeSpan timeDiff = now - myStartTime;
                        // Get the total hours (can be fractional)
                        double hoursElapsed = timeDiff.TotalHours;
                        int roundedHours = (int)Math.Round(hoursElapsed);
                        subTitle = subTitle.Replace("<HOUR>", roundedHours.ToString());

                        FBMessageBox ff = new FBMessageBox(subTitle, title, MessageBoxButton.YesNo, buttonDo, buttonSkip)
                        {
                            Owner = ParentWindow
                        };
                        ParentWindow.GridShade.Visibility = Visibility.Visible;
                        ff.ShowDialog();
                        ParentWindow.GridShade.Visibility = Visibility.Collapsed;
                        if (ff.btnClicked == MessageBoxResult.Yes) //do habits
                        {
                        }
                        else
                        {
                            //skip habits
                            //WE will set the morning habits shown date for today
                            RegistryManager.SetMorningHabitsShownTillDate();

                            FocusBearAPIClient client = new FocusBearAPIClient();
                            var sResponse1 = client.ForceCompleteCurrentSequence(appSettings.CurrentRunningSequenceID, true);

                            appSettings.RemoveSecondaryWindows();
                            appSettings.nCurrentActivityType = CurrentActivityType.NONE; //Set it to none as morning routine is done

                            if (appSettings.AutoCloseAfterRoutineSkipPostpone)
                                ((App)Application.Current).wndFocusBearMainWindow.Hide();
                            else
                                ((App)Application.Current).ShowHabitsSectionInMainWindow();
                        }
                    };

                    timer.Start(); // Start the 2-second timer
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AskConfirmationForLateHabits() ex = " + ex.Message);
            }
        }
        private void dispatcherTimerAskSleepAndMood_Tick(object sender, EventArgs e)
        {
            try
            {
                dispatcherTimerAskSleepAndMood.Stop();
                CommandManager.InvalidateRequerySuggested();
                ParentWindow.GridShade.Visibility = Visibility.Visible;
                AnalyzeSleepMoodWindow wndAnalyzeSleepMood = new AnalyzeSleepMoodWindow(ParentWindow);
                bool? bResult = wndAnalyzeSleepMood.ShowDialog();
                ParentWindow.GridShade.Visibility = Visibility.Collapsed;

                if (bResult.Value == true)
                {
                    FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["preferencesInfo"]["alertChangeLanguageInfoText"].ToString(), "Focus Bear");
                    ff.Owner = ParentWindow;
                    ff.ShowDialog();
                    appSettings.bRestartAppNeeded = true;
                    Application.Current.Shutdown(0);
                }

                RegistryManager.SetAnalyzeSleepAndMoodStatus();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("dispatcherTimerAskSleepAndMood_Tick ex = " + ex.Message);
            }

        }

        public void ShowChooseHabitsWindow()
        {
            LogWriter.LogWrite("ShowChooseHabitsWindow ()");

            ParentWindow.GridShade.Visibility = Visibility.Visible;
            ChooseHabits WndChooseHabits = new ChooseHabits(ParentWindow);

            bool? DialogResult = WndChooseHabits.ShowDialog();
            ParentWindow.GridShade.Visibility = Visibility.Collapsed;

            if (DialogResult.Value == true) //means user wants to skip morning habits completely
            {
                ((App)Application.Current).wndFocusBearMainWindow.Hide();

                appSettings.RemoveSecondaryWindows();
                appSettings.nCurrentActivityType = CurrentActivityType.NONE; //Set it to none as morning routine is done
            }

        }

        public bool ShowChooseAnotherHabitsWindow()
        {
            LogWriter.LogWrite("ShowChooseAnotherHabitsWindow ()");

            ParentWindow.GridShade.Visibility = Visibility.Visible;

            ChooseAnotherHabit WndChooseAnotherHabit = new ChooseAnotherHabit(ParentWindow);

            bool? DialogResult = WndChooseAnotherHabit.ShowDialog();
            ParentWindow.GridShade.Visibility = Visibility.Collapsed;

            if (DialogResult.Value) //True means user have selected another habit and that is to be done
            {
                return true;
            }
            return false;
        }

        public void ResumeHabitsBeforeStartActivityTrigger(string sHabitName)
        {
            if (((App) Application.Current).wndTrayMenu != null)
            {
                ButtonAutomationPeer peer = new ButtonAutomationPeer(((App)Application.Current).wndTrayMenu.btnResumeHabits);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }
            //Now recall this function after 6 seconds.
            //await Task.Delay(6000);

            //StartActivityTriggeredFromDevice(sHabitName, true);
        }
        public void StartActivityTriggeredFromDevice(string sHabitId)
        {
            LogWriter.LogWrite("StartActivityTriggeredFromDevice() - " + sHabitId);

            try
            {
                //if the Activity finish screen is visible, we will fire the start button there.
                bool bScreenHabitActivityFinishedVisible = (((App)Application.Current).wndFocusBearMainWindow.PageHabits.nScreenHabitActivityFinished != null &&
                ((App)Application.Current).wndFocusBearMainWindow.PageHabits.nScreenHabitActivityFinished.IsVisible);

                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                {
                    habit itemToMove = appSettings.arrAllMorningHabits.FirstOrDefault(h => h.idFakeForDevice == sHabitId);
                    if (!string.IsNullOrEmpty(itemToMove.id))
                    {
                        // Valid habit found and id is not empty
                        // Remove the item from arrAllMorningHabits
                        appSettings.arrAllMorningHabits.Remove(itemToMove);
                        // Insert it at the front (index 0) of arrAllMorningHabits
                        appSettings.arrAllMorningHabits.Insert(bScreenHabitActivityFinishedVisible ? (appSettings.nHabitIndex + 1) : appSettings.nHabitIndex, itemToMove);
                    }
                    else
                    {
                        LogWriter.LogWrite("StartActivityTriggeredFromDevice No habit with name found: " + sHabitId);
                        return;
                    }
                   
                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    habit itemToMove = appSettings.arrAllEveningHabits.FirstOrDefault(h => h.idFakeForDevice == sHabitId);
                    if (!string.IsNullOrEmpty(itemToMove.id))
                    {
                        // Remove the item from arrAllEveningHabits
                        appSettings.arrAllEveningHabits.Remove(itemToMove);
                        // Insert it at the front (index 0) of arrAllEveningHabits
                        appSettings.arrAllEveningHabits.Insert(bScreenHabitActivityFinishedVisible ? (appSettings.nHabitIndex + 1) : appSettings.nHabitIndex, itemToMove);
                    }
                    else
                    {
                        LogWriter.LogWrite("StartActivityTriggeredFromDevice No habit with name found: " + sHabitId);
                        return;
                    }
                }
 
                if (bScreenHabitActivityFinishedVisible) 
                {
                    //If we fire the start next activity button, it takes care of habitindex
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(((App)Application.Current).wndFocusBearMainWindow.PageHabits.nScreenHabitActivityFinished.btnStartNextActivity);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else //Possibly Welcome screen is visible
                {
                    //Show habits view it it was elsewhere
                    if (!((App)Application.Current).wndFocusBearMainWindow.IsLeafSelected(LeftButtonType.Habits))
                        ((App)Application.Current).wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.Habits);

                    ShowActivityStartScreen();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StartActivityTriggeredFromDevice ex =" + ex.Message);
            }
        }

        public void StartActivityTriggeredFromAudioCommand(string sHabitName)
        {
            LogWriter.LogWrite("StartActivityTriggeredFromAudioCommand() - " + sHabitName);

            try
            {
                //if the Activity finish screen is visible, we will fire the start button there.
                bool bScreenHabitActivityFinishedVisible = (((App)Application.Current).wndFocusBearMainWindow.PageHabits.nScreenHabitActivityFinished != null &&
                ((App)Application.Current).wndFocusBearMainWindow.PageHabits.nScreenHabitActivityFinished.IsVisible);

                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                {
                    habit itemToMove = appSettings.arrAllMorningHabits.FirstOrDefault(h => h.nameWithoutIcon.ToLower() == sHabitName.ToLower());
                    if (!string.IsNullOrEmpty(itemToMove.id))
                    {
                        // Valid habit found and id is not empty
                        // Remove the item from arrAllMorningHabits
                        appSettings.arrAllMorningHabits.Remove(itemToMove);
                        // Insert it at the front (index 0) of arrAllMorningHabits
                        appSettings.arrAllMorningHabits.Insert(bScreenHabitActivityFinishedVisible ? (appSettings.nHabitIndex + 1) : appSettings.nHabitIndex, itemToMove);
                    }
                    else
                    {
                        LogWriter.LogWrite("StartActivityTriggeredFromDevice No habit with name found: " + sHabitName);
                        return;
                    }

                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    habit itemToMove = appSettings.arrAllEveningHabits.FirstOrDefault(h => h.nameWithoutIcon == sHabitName.ToLower());
                    if (!string.IsNullOrEmpty(itemToMove.id))
                    {
                        // Remove the item from arrAllEveningHabits
                        appSettings.arrAllEveningHabits.Remove(itemToMove);
                        // Insert it at the front (index 0) of arrAllEveningHabits
                        appSettings.arrAllEveningHabits.Insert(bScreenHabitActivityFinishedVisible ? (appSettings.nHabitIndex + 1) : appSettings.nHabitIndex, itemToMove);
                    }
                    else
                    {
                        LogWriter.LogWrite("StartActivityTriggeredFromDevice No habit with name found: " + sHabitName);
                        return;
                    }
                }

                if (bScreenHabitActivityFinishedVisible)
                {
                    //If we fire the start next activity button, it takes care of habitindex
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(((App)Application.Current).wndFocusBearMainWindow.PageHabits.nScreenHabitActivityFinished.btnStartNextActivity);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else //Possibly Welcome screen is visible
                {
                    //Show habits view it it was elsewhere
                    if (!((App)Application.Current).wndFocusBearMainWindow.IsLeafSelected(LeftButtonType.Habits))
                        ((App)Application.Current).wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.Habits);

                    ShowActivityStartScreen();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StartActivityTriggeredFromDevice ex =" + ex.Message);
            }
        }
    }
}
