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

namespace Focus_Bear.Habits
{
    /// <summary>
    /// Interaction logic for HabitWindow.xaml
    /// </summary>
    public partial class HabitWindow : FullScreenParent
    {
        AppSettings appSettings = AppSettings.GetInstance;
        private HabitActivityFinished nScreenHabitActivityFinished = null;
        public DateTime ActivityStartTime;

        public HabitActivityOnGoing PageHabitActivityOnGoing = null;
        public HabitsWelcomeList PageWelcomeList = null;
        public HabitExtraPersonal PageHabitExtraPersonal;

        int nTimer = 15;
       
        DispatcherTimer dispatcherTimerSkippingIn = new DispatcherTimer(DispatcherPriority.Normal);

        DispatcherTimer dispatcherTimerShowDateTime = new DispatcherTimer(DispatcherPriority.Normal);

        protected override async void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            try
            {
                if (nScreenHabitActivityFinished != null)
                {
                    await nScreenHabitActivityFinished.SetWebView();
                    await nScreenHabitActivityFinished.SetWebView1();
                
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
            }
        }

        public HabitWindow()
        {
            InitializeComponent();

            LogWriter.LogWrite("HabitWindow()");

#if DEBUG
            this.WindowState = WindowState.Normal;
            this.WindowStyle = WindowStyle.ToolWindow;
#endif
            Time.Text = DateTime.Now.ToString(CultureInfo.CurrentCulture.DateTimeFormat.FullDateTimePattern);

            dispatcherTimerShowDateTime.Tick += new EventHandler(dispatcherTimerShowDateTime_Tick);
            dispatcherTimerShowDateTime.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimerShowDateTime.Start();


            Heading.Text = "";
            btnPleaseSpareMeTXT.Text = appSettings.ConfigFileStrings["common"]["buttonAbortText"].ToString();

            //Show the Please Spare Me Button
            btnPleaseSpareMe.Visibility = Visibility.Visible;

            AppFrameHabitScreens.Visibility = Visibility.Visible;

            dispatcherTimerSkippingIn.Tick += new EventHandler(dispatcherTimerSkippingIn_Tick);
            dispatcherTimerSkippingIn.Interval = new TimeSpan(0, 0, 1);

            if (nScreenHabitActivityFinished == null)
            {
                nScreenHabitActivityFinished = new HabitActivityFinished(this);
            }

            if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
            {
                //We need to hide the Please SPare Me Button
                LogWriter.LogWrite("AppFrameHabitScreens.Navigate(new BreakActivityBrainDump(this));");
                AppFrameHabitScreens.Navigate(new BreakActivityBrainDump(this));

            }
            else
            {
                //WE need to get the Sequnece ID that canbe used to force compplete the Sequence morning or evening if the user deactivates teh app for 4 hours
                appSettings.CurrentRunningSequenceID = (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS) ? appSettings.arrAllMorningHabits[appSettings.nHabitIndex].activity_sequence_id : ((appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS) ? appSettings.arrAllEveningHabits[appSettings.nHabitIndex].activity_sequence_id : "");

                //If the last activity was postponed after geting started, we must resume that
                string sPostponedActivityId = RegistryManager.GetCurrentActivityPostponedID();
                if(sPostponedActivityId != null && sPostponedActivityId.Length > 0)
                {
                    if(appSettings.bShowActivitiesFromMid)
                    {
                        //Increment the habit index to show the correct activity ongoing which was postponed
                        appSettings.nHabitIndex++;
                        LogWriter.LogWrite("nHabitIndex = " + appSettings.nHabitIndex.ToString());
                    }
                    string sActivityID = (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS) ? appSettings.arrAllMorningHabits[appSettings.nHabitIndex].id : ((appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS) ? appSettings.arrAllEveningHabits[appSettings.nHabitIndex].id : "");
                    if(sPostponedActivityId == sActivityID)
                    {
                       
                        //Maybe the last aqctivity was a skipped activity but now we are starting a postponed activity..so adjust the vailable
                        appSettings.bShowActivitiesFromMidLastSkipped = false;
                        appSettings.bShowActivitiesFromMid= false;
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
                        string sActivityID = (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS) ? appSettings.arrAllMorningHabits[appSettings.nHabitIndex].id : ((appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS) ? appSettings.arrAllEveningHabits[appSettings.nHabitIndex].id  : "" );
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
                    PageWelcomeList = new HabitsWelcomeList(this);
                    AppFrameHabitScreens.Navigate(PageWelcomeList); //Normal process
                }
            }

        }
      
        public void ShowStartBreakActivityScreen()
        {
            AppFrameHabitScreens.Navigate(new StartBreakActivity(this));
        }
        public void ShowExtraPersonalFocus()
        {
            LogWriter.LogWrite("ShowExtraPersonalFocus()");

            PageHabitExtraPersonal = new HabitExtraPersonal(this);
            AppFrameHabitScreens.Navigate(PageHabitExtraPersonal);
        }
        public void ShowActivityStartScreen(bool bStartingAfterPostponed = false)
        {
            LogWriter.LogWrite("ShowActivityStartScreen()");

            appSettings.dLastActivityPerformedStartTime = DateTime.Now; //Set the activity Start Time

            PageHabitActivityOnGoing = new HabitActivityOnGoing(this, bStartingAfterPostponed);

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

                if(appSettings.bShowActivitiesFromMid)//Justload the graph data in this case
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
            if(nScreenHabitActivityFinished != null)
            {
                nScreenHabitActivityFinished.webView.Dispose();
                nScreenHabitActivityFinished.webView1.Dispose();
            }

            if (PageHabitActivityOnGoing != null)
            {
                PageHabitActivityOnGoing.webView.Dispose();
            }

            if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
            {
                LogWriter.LogWrite("LAST BREAK_ACTIVITY Finished");
                //Set the current activity type to NONE
               appSettings.nCurrentActivityType = CurrentActivityType.NONE;

                //Start the break Activities Timer
                ((App)(Application.Current)).StartBreakActivityTimer();
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
            {
                LogWriter.LogWrite("LAST MORNING_HABITS Activity Finished");
                Heading.Text = appSettings.ConfigFileStrings["common"]["stayFocusedText"].ToString();
                AppFrameHabitScreens.Visibility = Visibility.Collapsed;

                //We need to hide the Please SPare Me Button
                btnPleaseSpareMe.Visibility = Visibility.Collapsed;
                
                await Task.Delay(3000);

                //WE will set the morning habits shown date for today
                appSettings.MorningHabitsShownDate = DateTime.Now;
                RegistryManager.SetMorningHabitsShownDate();

                //Set the current activity type to NONE
                appSettings.nCurrentActivityType = CurrentActivityType.NONE;
                LogWriter.LogWrite(" appSettings.nCurrentActivityType = CurrentActivityType.NONE;");

                //Start the break Activities Timer
                ((App)(Application.Current)).StartBreakActivityTimer();
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

                    if (now > myShutOffTime) //Same Day like befoee 12.00 am
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


                    //Set the current activity type to NONE
                    appSettings.nCurrentActivityType = CurrentActivityType.NONE;
                    LogWriter.LogWrite(" appSettings.nCurrentActivityType = CurrentActivityType.NONE;");

                    //Show the Shutoff Screen
                    ((App)(Application.Current)).ShowShutOffScreen();

                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("HideFrameAndClose()" + ex.Message); 
                }
            }

            Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
#if DEBUG
            if (e.Key == Key.Escape)
            {
                Close();
            }
#endif
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
                        _ = client.PostActivityCompleted(appSettings.arrAllMorningHabits[appSettings.nHabitIndex].id, appSettings.arrAllMorningHabits[appSettings.nHabitIndex].activity_sequence_id, ActivityStartTime, nDurationLogged, "", /*dQuantityLogged*/appSettings.arrAllMorningHabits[appSettings.nHabitIndex].logQuantityQuestions,  appSettings.bSkipCurrentActivity);
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
            if (!this.IsVisible || GridShade.IsVisible) //Either the window is not visible means its postponsed or the grid share is visible mean either log window or the skip confirm window is there..so return from here
            {
                LogWriter.LogWrite("Habit Window Not visible or GridShade.IsVisible, return ");
                return;
            }
            if(!btnPleaseSpareMe.IsVisible)
            {
                LogWriter.LogWrite("btnPleaseSpareMe Not visible, return");
                return;
            }
            ButtonAutomationPeer peer = new ButtonAutomationPeer(btnPleaseSpareMe);
            IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            invokeProv.Invoke();
        }
        private void btnPleaseSpareMe_Click(object sender, RoutedEventArgs e)
        {
            //If a password is set, ask fop a password
            
            if (appSettings.isPasswordSet)
            {
                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                {
                    if (appSettings.bPasswordRequireInSkipMorning)
                    {
                        //It might be possible that a webview is visible, we will need to hide that
                        if(nScreenHabitActivityFinished != null)
                        {
                            if(nScreenHabitActivityFinished.webView.IsVisible)
                                nScreenHabitActivityFinished.webView.Visibility = Visibility.Collapsed;
                            if( nScreenHabitActivityFinished.webView1.IsVisible)
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

                        if(PageHabitExtraPersonal != null)
                        {
                            if (appSettings.bMotivationalMessageEnabled)
                                PageHabitExtraPersonal.webView.Visibility = Visibility.Collapsed;
                        }

                        if(PageHabitActivityOnGoing != null)
                        {
                            if (PageHabitActivityOnGoing.webView.IsVisible)
                            {
                                PageHabitActivityOnGoing.webView.Visibility = Visibility.Collapsed;
                            }
                        }
                        AskPassword pwdWnd = new AskPassword();
                        pwdWnd.Owner = this;
                        GridShade.Visibility = Visibility.Visible;

                        bool? bResult = pwdWnd.ShowDialog();

                        GridShade.Visibility = Visibility.Collapsed;
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
                else if(appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
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


                        AskPassword pwdWnd = new AskPassword();
                        pwdWnd.Owner = this;
                        GridShade.Visibility = Visibility.Visible;

                        bool? bResult = pwdWnd.ShowDialog();

                        GridShade.Visibility = Visibility.Collapsed;
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

            Hide();

            appSettings.RemoveSecondaryWindows();
            
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
        private void Window_Closed(object sender, EventArgs e)
        {
            //If its shutoff window already shown then do not remove secondary monitor windows
            if(appSettings.nCurrentActivityType != CurrentActivityType.SHUT_OFF)
                appSettings.RemoveSecondaryWindows();
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

            GridShade.Visibility = Visibility.Visible;
            GridWhyDoYouWantToSkip.Visibility = Visibility.Visible;

        }
        public void HideWhySkipHabitsWindow()
        {
            LogWriter.LogWrite("HideWhySkipHabitsWindow()");

            GridShade.Visibility = Visibility.Collapsed;
            GridWhyDoYouWantToSkip.Visibility = Visibility.Collapsed;

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
            HeadingSkippingInTimer.Text = "00:15";
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

            nTimer = 15;
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
            Time.Text = DateTime.Now.ToString(CultureInfo.CurrentCulture.DateTimeFormat.FullDateTimePattern);
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
    }
}
