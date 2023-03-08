using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Onboarding;
using Focus_Bear.Preferences;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        int nTimer = 15;
        string TxtTimer = "";

        DispatcherTimer dispatcherTimerSkippingIn = new DispatcherTimer(DispatcherPriority.Normal);

       

        public HabitWindow()
        {
            InitializeComponent();

          ///  _hookID = SetHook(_proc);

#if DEBUG
            this.WindowState = WindowState.Normal;
            this.WindowStyle = WindowStyle.ToolWindow;
#endif
            Heading.Text = "";
            btnPleaseSpareMeTXT.Text = appSettings.ConfigFileStrings["common"]["buttonAbortText"].ToString();
            //Show the Please SPare Me Button
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
                /// btnPleaseSpareMe.Visibility = Visibility.Collapsed; - not hiding as we need postpone in breaks too...
                /// 

                //AppFrameHabitScreens.Navigate(new StartBreakActivity(this));

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
                        var sResponse = client.GetActivityStats(sActivityID, "");
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite(ex.Message);
                    }

                    ShowACtivityFinishedScreen();
                }
                else
                {
                    AppFrameHabitScreens.Navigate(new HabitsWelcomeList(this)); //Normal process
                }
            }

        }
      
        public void ShowStartBreakActivityScreen()
        {
            AppFrameHabitScreens.Navigate(new StartBreakActivity(this));
        }
        public void ShowExtraPersonalFocus()
        {
            AppFrameHabitScreens.Navigate(new HabitExtraPersonal(this));
        }
        public void ShowActivityStartScreen(bool bStartingAfterPostponed = false)
        {
            //Dispose the web view if left from old screen
            //if (nScreenHabitActivityFinished != null)
            //{
            //    nScreenHabitActivityFinished.webView.Dispose();
            //    nScreenHabitActivityFinished.webView1.Dispose();
            //    nScreenHabitActivityFinished = null;//setting to null so that new webview is created
            //}

            appSettings.dLastActivityPerformedStartTime = DateTime.Now; //SEt the activity Start Time

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

                //if (nScreenHabitActivityFinished == null)
                //{
                //    nScreenHabitActivityFinished = new HabitActivityFinished(this);
                //}

                AppFrameHabitScreens.Navigate(nScreenHabitActivityFinished);

                nScreenHabitActivityFinished.LoadData(appSettings.bSkipCurrentActivity);

                if(appSettings.bShowActivitiesFromMid)//Justload the graph data in this case
                {
                    System.Threading.Tasks.Task.Factory.StartNew(() =>
                    {
                        Thread.Sleep(900);
                        Dispatcher.BeginInvoke((Action)(() => nScreenHabitActivityFinished.LoadGraphData()));
                    });

                   
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
                        sLogType = "morning routine";
                        appSettings.AddEntryInCSV(sLogType, appSettings.arrAllMorningHabits[appSettings.nHabitIndex].name, appSettings.arrAllMorningHabits[appSettings.nHabitIndex].durationSeconds.ToString(), appSettings.arrAllMorningHabits[appSettings.nHabitIndex].logQuantityQuestion, "", sStartTime);
                        if (appSettings.bSkipCurrentActivityCantDoItToday)
                        {
                            var x = client.PostActivitySkipped(appSettings.arrAllMorningHabits[appSettings.nHabitIndex].id, appSettings.arrAllMorningHabits[appSettings.nHabitIndex].activity_sequence_id);
                        }
                        else
                        {
                            var x = client.PostActivityCompleted(appSettings.arrAllMorningHabits[appSettings.nHabitIndex].id, appSettings.arrAllMorningHabits[appSettings.nHabitIndex].activity_sequence_id, ActivityStartTime, appSettings.arrAllMorningHabits[appSettings.nHabitIndex].durationSeconds, "", -1, appSettings.bSkipCurrentActivity);
                        }
                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                    {
                        sLogType = "evening routine";
                        appSettings.AddEntryInCSV(sLogType, appSettings.arrAllEveningHabits[appSettings.nHabitIndex].name, appSettings.arrAllEveningHabits[appSettings.nHabitIndex].durationSeconds.ToString(), appSettings.arrAllEveningHabits[appSettings.nHabitIndex].logQuantityQuestion, "", sStartTime);
                        if (appSettings.bSkipCurrentActivityCantDoItToday)
                        {
                            var x = client.PostActivitySkipped(appSettings.arrAllEveningHabits[appSettings.nHabitIndex].id, appSettings.arrAllEveningHabits[appSettings.nHabitIndex].activity_sequence_id);
                        }
                        else
                        {
                            var x = client.PostActivityCompleted(appSettings.arrAllEveningHabits[appSettings.nHabitIndex].id, appSettings.arrAllEveningHabits[appSettings.nHabitIndex].activity_sequence_id, ActivityStartTime, appSettings.arrAllEveningHabits[appSettings.nHabitIndex].durationSeconds, "", -1, appSettings.bSkipCurrentActivity);
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

                        if (!currentActivity.isRandomChoice)
                        {
                            appSettings.AddEntryInCSV(sLogType, currentActivity.name, currentActivity.durationSeconds.ToString(), currentActivity.logQuantityQuestion, "", sStartTime);
                            var x = client.PostActivityCompleted(currentActivity.id, currentActivity.activity_sequence_id, ActivityStartTime, currentActivity.durationSeconds, "", -1);
                        }
                        else
                        {
                            //random choice
                            var randomChoice = appSettings.currentActivityChoice;
                            if (randomChoice.id != null)
                            {
                                appSettings.AddEntryInCSV(sLogType, randomChoice.name, currentActivity.durationSeconds.ToString(), randomChoice.logQuantityQuestion, "", sStartTime);
                                var x = client.PostActivityCompleted(currentActivity.id, currentActivity.activity_sequence_id, ActivityStartTime, currentActivity.durationSeconds, randomChoice.id, -1);
                            }
                        }
                        
                    }

                    nScreenHabitActivityFinished.LoadGraphData();
                }

                   
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowACtivityFinishedScreen() Exception - " + ex.Message);
                Debug.WriteLine(ex.Message);
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
                // "stayFocusedText":"OK - Stay focused throughout the day!",
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
                    Debug.WriteLine(ex.Message);
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
            //if (e.Key == Key.System && e.SystemKey == Key.F4)
            //{
            //    e.Handled = true;
            //}
        }

        //GridLogQuantityNag Event Handlers
        public void ShowLogWindow()
        {

            LogWriter.LogWrite("ShowLogWindow()");
            /*
             *   "logQuantityInfo": {
            "title": "Log Quantity of activity",
            "buttonCancelText": "Cancel",
            "buttonOkText": "OK",
            "enterValidValueText": "Please enter a valid value.",
            "quantityIsEmptyText": "Log quantity can not be empty."
              "logQuantityQuestionText":"How many <activity> did you do?"
              */

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


            btnOK.IsEnabled = false; //Initially

            nScreenHabitActivityFinished.webView.Visibility = Visibility.Collapsed;

            GridShade.Visibility = Visibility.Visible;
            GridLogQuantityNag.Visibility = Visibility.Visible;

            Quantity.Text = ""; //Refresh for new input
            Quantity.Focus();

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
            HeadingLogQuantityQuestion.Text = sLogQuantityQuestion;
            btnCancelText.Text = appSettings.ConfigFileStrings["logQuantityInfo"]["buttonCancelText"].ToString();
            btnOKText.Text = appSettings.ConfigFileStrings["logQuantityInfo"]["buttonOkText"].ToString();

            //Hide the Graph again
            nScreenHabitActivityFinished.webView.Visibility = Visibility.Collapsed;
            nScreenHabitActivityFinished.BreakGraph2.Visibility = Visibility.Collapsed;

            /*
             * writing to csv
             * //before your loop
                var csv = new StringBuilder();

            //in your loop
                var first = reader[0].ToString();
                var second = image.ToString();
                //Suggestion made by KyleMit
                var newLine = string.Format("{0},{1}", first, second);
                csv.AppendLine(newLine);  

            //after your loop
                File.WriteAllText(filePath, csv.ToString());

            File.AppendAllText(filePath, csv.ToString());

            if you want to keep previous versions of csv in the same file

            test
            var newLine = $"{first},{second}"

            

            I would highly recommend you to go the more tedious route. Especially if your file size is large.

            using(var w = new StreamWriter(path))
            {
                for( /* your loop * /)
                {
                            var first = yourFnToGetFirst();
                            var second = yourFnToGetSecond();
                            var line = string.Format("{0},{1}", first, second);
                            w.WriteLine(line);
                            w.Flush();
                        }
                    }
                    don't forget to close the writer with w.Close(); w.Flush() is not needed.

            var file = @"C:\myOutput.csv";

            using (var stream = File.CreateText(file))
            {
                for (int i = 0; i < reader.Count(); i++)
                {
                    string first = reader[i].ToString();
                    string second = image.ToString();
                    string csvRow = string.Format("{0},{1}", first, second);

                    stream.WriteLine(csvRow);
                }
            }

             * */
        }
        public void HideLogWindow()
        {
            LogWriter.LogWrite("HideLogWindow()");

            GridShade.Visibility = Visibility.Collapsed;
            GridLogQuantityNag.Visibility = Visibility.Collapsed;

            //Load the Graph DAta
           // nScreenHabitActivityFinished.LoadGraphData();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnCancel_Click()");
            try
            {
                Quantity.Text = "";
                string sLogType = "";
                string sStartTime = appSettings.dLastActivityPerformedStartTime.ToString("yyyy-MM-dd HH:mm:ss");

                {
                    /*
                       *"logQuantityInfo": {
                       "title": "Log Quantity of activity",
                       "buttonCancelText": "Cancel",
                       "buttonOkText": "OK",
                       "enterValidValueText": "Please enter a valid value.",
                       "quantityIsEmptyText": "Log quantity can not be empty."
                       "logQuantityQuestionText":"How many <activity> did you do?"
                   */
                    FocusBearAPIClient client = new FocusBearAPIClient();

                    if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                    {
                        sLogType = "morning routine";
                        appSettings.AddEntryInCSV(sLogType, appSettings.arrAllMorningHabits[appSettings.nHabitIndex].name, appSettings.arrAllMorningHabits[appSettings.nHabitIndex].durationSeconds.ToString(), appSettings.arrAllMorningHabits[appSettings.nHabitIndex].logQuantityQuestion, "", sStartTime);
                        var x = client.PostActivityCompleted(appSettings.arrAllMorningHabits[appSettings.nHabitIndex].id, appSettings.arrAllMorningHabits[appSettings.nHabitIndex].activity_sequence_id, ActivityStartTime, appSettings.arrAllMorningHabits[appSettings.nHabitIndex].durationSeconds, "", -1, appSettings.bSkipCurrentActivity);

                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                    {
                        sLogType = "evening routine";
                        appSettings.AddEntryInCSV(sLogType, appSettings.arrAllEveningHabits[appSettings.nHabitIndex].name, appSettings.arrAllEveningHabits[appSettings.nHabitIndex].durationSeconds.ToString(), appSettings.arrAllEveningHabits[appSettings.nHabitIndex].logQuantityQuestion, "", sStartTime);
                        var x = client.PostActivityCompleted(appSettings.arrAllEveningHabits[appSettings.nHabitIndex].id, appSettings.arrAllEveningHabits[appSettings.nHabitIndex].activity_sequence_id, ActivityStartTime, appSettings.arrAllEveningHabits[appSettings.nHabitIndex].durationSeconds, "", -1, appSettings.bSkipCurrentActivity);

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

                        if (!currentActivity.isRandomChoice)
                        {
                            appSettings.AddEntryInCSV(sLogType, currentActivity.name, currentActivity.durationSeconds.ToString(), currentActivity.logQuantityQuestion, "", sStartTime);
                            var x = client.PostActivityCompleted(currentActivity.id, currentActivity.activity_sequence_id, ActivityStartTime, currentActivity.durationSeconds, "");

                        }
                        else
                        {
                            //random choice
                            var randomChoice = appSettings.currentActivityChoice;
                            if (randomChoice.id != null)
                            {
                                appSettings.AddEntryInCSV(sLogType, randomChoice.name, currentActivity.durationSeconds.ToString(), randomChoice.logQuantityQuestion, "", sStartTime);
                                var x = client.PostActivityCompleted(currentActivity.id, currentActivity.activity_sequence_id, ActivityStartTime, currentActivity.durationSeconds, randomChoice.id);

                            }
                        }
                    }
                }

                HideLogWindow();

                //Load the Graph DAta
                nScreenHabitActivityFinished.LoadGraphData();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnOK_Click()");
            try
            {
                string sLogType = "";
                string sStartTime = appSettings.dLastActivityPerformedStartTime.ToString("yyyy-MM-dd HH:mm:ss");

                string sQuantity = Quantity.Text;
                if (sQuantity.Length > 0)
                {
                    double dQuantityLogged = double.Parse(sQuantity);
                    FocusBearAPIClient client = new FocusBearAPIClient();

                    if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                    {
                        sLogType = "morning routine";
                        appSettings.AddEntryInCSV(sLogType, appSettings.arrAllMorningHabits[appSettings.nHabitIndex].name, appSettings.arrAllMorningHabits[appSettings.nHabitIndex].durationSeconds.ToString(), appSettings.arrAllMorningHabits[appSettings.nHabitIndex].logQuantityQuestion, dQuantityLogged.ToString(), sStartTime);
                        var x = client.PostActivityCompleted(appSettings.arrAllMorningHabits[appSettings.nHabitIndex].id, appSettings.arrAllMorningHabits[appSettings.nHabitIndex].activity_sequence_id, ActivityStartTime, appSettings.arrAllMorningHabits[appSettings.nHabitIndex].durationSeconds, "", dQuantityLogged,appSettings.bSkipCurrentActivity);
                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                    {
                        sLogType = "evening routine";
                        appSettings.AddEntryInCSV(sLogType, appSettings.arrAllEveningHabits[appSettings.nHabitIndex].name, appSettings.arrAllEveningHabits[appSettings.nHabitIndex].durationSeconds.ToString(), appSettings.arrAllEveningHabits[appSettings.nHabitIndex].logQuantityQuestion, dQuantityLogged.ToString(), sStartTime);
                        var x = client.PostActivityCompleted(appSettings.arrAllEveningHabits[appSettings.nHabitIndex].id, appSettings.arrAllEveningHabits[appSettings.nHabitIndex].activity_sequence_id, ActivityStartTime, appSettings.arrAllEveningHabits[appSettings.nHabitIndex].durationSeconds, "", dQuantityLogged, appSettings.bSkipCurrentActivity);
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

                        if (!currentActivity.isRandomChoice)
                        {
                            appSettings.AddEntryInCSV(sLogType, currentActivity.name, currentActivity.durationSeconds.ToString(), currentActivity.logQuantityQuestion, dQuantityLogged.ToString(), sStartTime);
                            var x = client.PostActivityCompleted(currentActivity.id, currentActivity.activity_sequence_id, ActivityStartTime, currentActivity.durationSeconds, "", dQuantityLogged);
                        }
                        else
                        {
                            //random choice
                            var randomChoice = appSettings.currentActivityChoice;
                            if (randomChoice.id != null)
                            {
                                appSettings.AddEntryInCSV(sLogType, randomChoice.name, currentActivity.durationSeconds.ToString(), randomChoice.logQuantityQuestion, dQuantityLogged.ToString(), sStartTime);
                                var x = client.PostActivityCompleted(currentActivity.id, currentActivity.activity_sequence_id, ActivityStartTime, currentActivity.durationSeconds, randomChoice.id, dQuantityLogged);
                            }
                        }

                    }
                }
                else
                {
                    /*
                       *"logQuantityInfo": {
                       "title": "Log Quantity of activity",
                       "buttonCancelText": "Cancel",
                       "buttonOkText": "OK",
                       "enterValidValueText": "Please enter a valid value.",
                       "quantityIsEmptyText": "Log quantity can not be empty."
                       "logQuantityQuestionText":"How many <activity> did you do?"
                   */
                    FocusBearAPIClient client = new FocusBearAPIClient();
                    if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                    {
                        sLogType = "morning routine";
                        appSettings.AddEntryInCSV(sLogType, appSettings.arrAllMorningHabits[appSettings.nHabitIndex].name, appSettings.arrAllMorningHabits[appSettings.nHabitIndex].durationSeconds.ToString(), appSettings.arrAllMorningHabits[appSettings.nHabitIndex].logQuantityQuestion, "", sStartTime);
                        var x = client.PostActivityCompleted(appSettings.arrAllMorningHabits[appSettings.nHabitIndex].id, appSettings.arrAllMorningHabits[appSettings.nHabitIndex].activity_sequence_id, ActivityStartTime, appSettings.arrAllMorningHabits[appSettings.nHabitIndex].durationSeconds, "");
                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                    {
                        sLogType = "evening routine";
                        appSettings.AddEntryInCSV(sLogType, appSettings.arrAllEveningHabits[appSettings.nHabitIndex].name, appSettings.arrAllEveningHabits[appSettings.nHabitIndex].durationSeconds.ToString(), appSettings.arrAllEveningHabits[appSettings.nHabitIndex].logQuantityQuestion, "", sStartTime);
                        var x = client.PostActivityCompleted(appSettings.arrAllEveningHabits[appSettings.nHabitIndex].id, appSettings.arrAllEveningHabits[appSettings.nHabitIndex].activity_sequence_id, ActivityStartTime, appSettings.arrAllEveningHabits[appSettings.nHabitIndex].durationSeconds, "");
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

                        if (!currentActivity.isRandomChoice)
                        {
                            appSettings.AddEntryInCSV(sLogType, currentActivity.name, currentActivity.durationSeconds.ToString(), currentActivity.logQuantityQuestion, "", sStartTime);
                            var x = client.PostActivityCompleted(currentActivity.id, currentActivity.activity_sequence_id, ActivityStartTime, currentActivity.durationSeconds, "");
                        }
                        else
                        {
                            //random choice
                            var randomChoice = appSettings.currentActivityChoice;
                            if (randomChoice.id != null)
                            {
                                appSettings.AddEntryInCSV(sLogType, randomChoice.name, currentActivity.durationSeconds.ToString(), randomChoice.logQuantityQuestion, "", sStartTime);
                                var x = client.PostActivityCompleted(currentActivity.id, currentActivity.activity_sequence_id, ActivityStartTime, currentActivity.durationSeconds, randomChoice.id);
                            }
                        }
                    }
                   

                }

                HideLogWindow();

                //Load the Graph DAta
                nScreenHabitActivityFinished.LoadGraphData();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnOK_Click - " + ex.Message);
            }
        }

        private void Quantity_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("^[.][0-9]+$|^[0-9]*[.]{0,1}[0-9]*$");
            e.Handled = !regex.IsMatch((sender as TextBox).Text.Insert((sender as TextBox).SelectionStart, e.Text));
            if (e.Handled)
            {
               // btnOK.IsEnabled = false;
            }
            else
            {
                btnOK.IsEnabled = true; // all set enable the ok button
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
                string text = Quantity.Text;
                text = text.Trim();
                if (text.Length > 0)
                {
                    btnOK.IsEnabled = true;
                }
                else
                {
                    btnOK.IsEnabled = false;
                }
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

                        if (bResult.Value)
                        {
                            //Password is correct
                        }
                        else
                        {
                            //Hide();
                            return;
                        }

                    }
                }
                else if(appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    if (appSettings.bPasswordRequireInSkipEvening)
                    {
                        //It might be possible that a webview is visible, we will need to hide that
                        if (nScreenHabitActivityFinished != null)
                        {
                            if (nScreenHabitActivityFinished.webView.IsVisible)
                                nScreenHabitActivityFinished.webView.Visibility = Visibility.Collapsed;
                            if (nScreenHabitActivityFinished.webView1.IsVisible)
                                nScreenHabitActivityFinished.webView1.Visibility = Visibility.Collapsed;
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

                        if (bResult.Value)
                        {
                            //Password is correct
                        }
                        else
                        {
                            //Hide();
                            return;
                        }
                    }
                }

            }

            // if (appSettings.nCurrentActivityType != CurrentActivityType.BREAK_ACTIVITY) //Only for Morning and Evening Habits. FOr Breaks we will simply cancel the current routine.
            {
                if (PageHabitActivityOnGoing != null)
                {
                    PageHabitActivityOnGoing.HandleTimerForPostpone(true); //Stop the timer
                }

                Hide();
            }
            //else
            //{
            //    Close();//For Breaks we will simply cancel the current break activities and start afresh.
            //}

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
            appSettings.RemoveSecondaryWindows();
            ////UnhookWindowsHookEx(_hookID);
        }

        public void ShowWhySkipHabitsWindow()
        {
            LogWriter.LogWrite("ShowWhySkipHabitsWindow()");
            /* "whySkipHabitVcInfo": {
                "title": "Why do you want to skip?",
                "buttonAlreadyDidItText": "I already did it",
                "buttonCanNotDoItText": "Can't do it today"
            },*/

            appSettings.bSkipCurrentActivity = false; //initiating, will be true after showing the nag for why do you want to skip
            appSettings.bSkipCurrentActivityCantDoItToday = false;

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
        }
        public void ShowSkippingHabitInWindow()
        {
            LogWriter.LogWrite("ShowSkippingHabitInWindow()");
            /*    "skipHabitVcInfo": {
                "title": "Skipping habit in",
                "buttonDoHabitsText": "I changed my mind - I'll do my habits"
            },*/

            HeadingSkippingIn.Text = appSettings.ConfigFileStrings["skipHabitVcInfo"]["title"].ToString();
            HeadingSkippingInTimer.Text = "00:15";
            btnChangedMyMindTXT.Text = appSettings.ConfigFileStrings["skipHabitVcInfo"]["buttonDoHabitsText"].ToString(); ;

            GridShade.Visibility = Visibility.Visible;
            GridSkippingHabitIn.Visibility = Visibility.Visible;

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
            HideWhySkipHabitsWindow();
            appSettings.bSkipCurrentActivity = true;
            //appSettings.indexSkippedActivities.Add(new SKIPPED_ACTIVITY(false, true));
            appSettings.nHabitIndex++; //Increment the index to do the next habit

            ShowACtivityFinishedScreen(true);

            //We will need to show the webview that we had hidden when showing this box
           // nScreenHabitActivityFinished.webView.Visibility = Visibility.Visible;
        }

        private void btnCantDoItToday_Click(object sender, RoutedEventArgs e)
        {
            //Hide why skip and show the skipping-in window
            HideWhySkipHabitsWindow();

            ShowSkippingHabitInWindow();
            //Work to skip the habit

        }

        private void btnChangedMyMind_Click(object sender, RoutedEventArgs e)
        {
            HideSkippingHabitInWindow();
            dispatcherTimerSkippingIn.Stop(); //Stop the timer as user had cancelled skipping
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
                    appSettings.nHabitIndex++; //Increment the index to do the next habit
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
