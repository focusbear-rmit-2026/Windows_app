using Focus_Bear.Models;
using Focus_Bear.Onboarding;
using Focus_Bear.Preferences;
using Focus_Bear.Utils;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Focus_Bear.Habits
{
    /// <summary>
    /// Interaction logic for HabitActivityFinished.xaml
    /// </summary>
    public partial class HabitActivityFinished : Page
    {
        private HabitWindow _parentWindow = null;
        AppSettings appSettings = AppSettings.GetInstance;

        int nTimer = 30;
 
        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);

        int bExcludedActivityIndex = -1;
        
        public int nQuestionIndexToShowGraphs = 0;

        private bool bBreakActivitiesFinished;

        public bool bWebViewInitialized = false;
        public bool bCallLoadGraphDataAfterWebViewInitialized = false;
        public HabitActivityFinished()
        {
           
        }
        public HabitActivityFinished(HabitWindow parentWindow) : this()
        {
            InitializeComponent();
            webView.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
            webView1.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted1;


            _parentWindow = parentWindow;
            bBreakActivitiesFinished = false;
            bExcludedActivityIndex = -1;
           /// SetWebView();
        }
       
        public async Task SetWebView()
        {
            try
            {
                var userDataFolder = System.IO.Path.Combine(AppSettings.AppDataDirPath, "FBCustomWebView2");

                var task = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
 
                if (webView != null)
                {
                    await webView.EnsureCoreWebView2Async(task);
                }

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Activity Finished SetWebView() EX - " + ex.Message);
            }
        }
        public async Task SetWebView1()
        {
            try
            {
                var userDataFolder = System.IO.Path.Combine(AppSettings.AppDataDirPath, "FBCustomWebView2");

                var task = await CoreWebView2Environment.CreateAsync(null, userDataFolder);

                if (webView1 != null)
                {
                    await webView1.EnsureCoreWebView2Async(task);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Activity Finished SetWebView1() EX - " + ex.Message);
            }
        }

        private void CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                try
                {

                       //this.webView.NavigationStarting += NavigationStarting;
                    //this.webView.NavigationCompleted += NavigationCompleted;
                    webView.CoreWebView2.Settings.AreHostObjectsAllowed = true;
                    webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                    webView.CoreWebView2.Settings.IsScriptEnabled = true;
                    webView.CoreWebView2.Settings.IsWebMessageEnabled = true;

                    webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                    webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

                    string curDir = Directory.GetCurrentDirectory() + "\\Graph";
                    webView.CoreWebView2.SetVirtualHostNameToFolderMapping("focusbear", curDir, CoreWebView2HostResourceAccessKind.Allow);

                    bWebViewInitialized = true;

                    if (appSettings.bShowActivitiesFromMid || bCallLoadGraphDataAfterWebViewInitialized)//Justload the graph data in this case
                    {
                        LoadGraphData();
                    }

                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("Activity Finished CoreWebView2InitializationCompleted()" + ex.Message);
                }
            }
        }

        private void CoreWebView2InitializationCompleted1(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                try
                {

                    //this.webView.NavigationStarting += NavigationStarting;
                    //this.webView.NavigationCompleted += NavigationCompleted;
                    webView1.CoreWebView2.Settings.AreHostObjectsAllowed = true;
                    webView1.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                    webView1.CoreWebView2.Settings.IsScriptEnabled = true;
                    webView1.CoreWebView2.Settings.IsWebMessageEnabled = true;

                    webView1.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                    webView1.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

                    string curDir = Directory.GetCurrentDirectory() + "\\Graph";
                    webView1.CoreWebView2.SetVirtualHostNameToFolderMapping("focusbear", curDir, CoreWebView2HostResourceAccessKind.Allow);

                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("Activity Finished CoreWebView2InitializationCompleted1()" + ex.Message);
                    Debug.WriteLine(ex.Message);
                }
            }
        }

        public void LoadData(bool bSkipCurrentActivity = false)
        {
            LogWriter.LogWrite("ActivityFinished LoadData()");
            /*
            *   "activityInfo":{
             "activityFinishedText":"<ACTIVITY> finished!✅",
             "lastActivityFinishedText":"<ACTIVITY> finished!✅ You’ve earned your screen time. 💪",
            "startActivityButtonText":"Start <TIME> <NAME>",

               "common":{
              "unlockMachineButtonText":"Unlock the machine",
                  "wrapupButtonText":"Let's wrap up for the day",
            */
            try
            {
                // webView.Source = new Uri(string.Format("file:///{0}", appSettings.sGraphDaysPath), System.UriKind.Absolute);
                bBreakActivitiesFinished = false;

                btnSkipThisHabitTXT.Text = appSettings.ConfigFileStrings["activityInfo"]["buttonSkipHabitText"].ToString();

                string sHeading = "";

                //Set button text with next activity start info
                //"startActivityButtonText":"Start <TIME> <NAME>",
                string sButtonTitle = "";

                int totalSecondsAllHabits = 0;
                int habitCompletedSeconds = 0;
                string sTotal = "";
                string sCompleted = "";
                string sRemaining = "";

                bool bLastActivity = false;

                //Show the skip this habit button initially, it will be handled below
                SkipThisHabitText.Visibility = Visibility.Visible;

                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                {
                    sTextCompletedDuration.Visibility = Visibility.Visible;

                    for (int i = 0; i < appSettings.arrAllMorningHabits.Count; i++)
                    {
                        bool bIsSkipped = false;
                        for (int m = 0; m < appSettings.arraySkipped.Count; m++)
                        {
                            if (appSettings.arraySkipped[m].ToString() == appSettings.arrAllMorningHabits[i].id)
                            {
                                bIsSkipped = true;
                                break;
                            }
                        }
                        if (!bIsSkipped && !(i == appSettings.nHabitIndex && appSettings.bSkipCurrentActivityCantDoItToday))
                        {
                            totalSecondsAllHabits += appSettings.arrAllMorningHabits[i].durationSeconds;
                            if (i <= appSettings.nHabitIndex)
                            {
                                habitCompletedSeconds += appSettings.arrAllMorningHabits[i].durationSeconds;
                            }
                        }
                    }

                    sTotal = appSettings.SecondsToText(totalSecondsAllHabits);
                    sCompleted = appSettings.SecondsToText(habitCompletedSeconds);
                    sRemaining = appSettings.SecondsToText(totalSecondsAllHabits - habitCompletedSeconds);

                    if (appSettings.arrAllMorningHabits.Count == (appSettings.nHabitIndex + 1)) //Last Activity
                    {
                        bLastActivity = true;
                        appSettings.CapturePostHog("complete-morning-routine");

                        if (appSettings.bSkipCurrentActivityCantDoItToday || appSettings.bShowActivitiesFromMidLastSkipped)
                        {
                            appSettings.bShowActivitiesFromMidLastSkipped = false;
                            sHeading = appSettings.ConfigFileStrings["activityInfo"]["lastActivitySkippedText"].ToString();
                        }
                        else
                        {
                            sHeading = appSettings.ConfigFileStrings["activityInfo"]["lastActivityFinishedText"].ToString();
                        }
                        sButtonTitle = appSettings.ConfigFileStrings["common"]["unlockMachineButtonText"].ToString();

                        //Hide the skip this habit button on last activity
                        SkipThisHabitText.Visibility = Visibility.Collapsed;

                        //We need to hide the Please SPare Me Button
                        _parentWindow.btnPleaseSpareMe.Visibility = Visibility.Collapsed;

                        //Progress text

                        string sProgressLast = appSettings.ConfigFileStrings["activityInfo"]["habitsCompleteText"].ToString();
                        sProgressLast = sProgressLast.Replace("<duration>", sTotal);
                        sTextCompletedDuration.Text = sProgressLast;

                        Sounds.PlayMorningRoutineFinishedSound();
                    }
                    else
                    {
                        appSettings.CapturePostHog("complete-morning-routine-activity");

                        if (appSettings.bSkipCurrentActivityCantDoItToday || appSettings.bShowActivitiesFromMidLastSkipped)
                        {
                            appSettings.bShowActivitiesFromMidLastSkipped = false;
                            sHeading = appSettings.ConfigFileStrings["activityInfo"]["activitySkippedText"].ToString();
                        }
                        else
                        {
                            sHeading = appSettings.ConfigFileStrings["activityInfo"]["activityFinishedText"].ToString();
                        }
                        sButtonTitle = appSettings.ConfigFileStrings["activityInfo"]["startActivityButtonText"].ToString();
                        sButtonTitle = sButtonTitle.Replace("<NAME>", appSettings.arrAllMorningHabits[appSettings.nHabitIndex + 1].name);
                        sButtonTitle = sButtonTitle.Replace("<TIME>", appSettings.SecondsToText(appSettings.arrAllMorningHabits[appSettings.nHabitIndex + 1].durationSeconds));

                        //Progress text
                        /*
                           * "activityInfo":{
                          "habitsProgressText":"You have completed <duration> of habits - <totalduration> to go. You can do it!",
                          "habitsCompleteText":"You have completed <duration> of habits.",
                          */
                        string sProgress = appSettings.ConfigFileStrings["activityInfo"]["habitsProgressText"].ToString();
                        sProgress = sProgress.Replace("<duration>", sCompleted);
                        sProgress = sProgress.Replace("<totalduration>", sRemaining);

                        sTextCompletedDuration.Text = sProgress;

                        Sounds.PlayHabitFinishedSound();
                    }

                    //Reset the cnt do it today variable here
                    //appSettings.bSkipCurrentActivityCantDoItToday = false;

                    sHeading = sHeading.Replace("<ACTIVITY>", appSettings.arrAllMorningHabits[appSettings.nHabitIndex].name);
                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    sTextCompletedDuration.Visibility = Visibility.Visible;

                    for (int i = 0; i < appSettings.arrAllEveningHabits.Count; i++)
                    {
                        bool bIsSkipped = false;
                        for (int m = 0; m < appSettings.arraySkipped.Count; m++)
                        {
                            if (appSettings.arraySkipped[m].ToString() == appSettings.arrAllEveningHabits[i].id)
                            {
                                bIsSkipped = true;
                                break;
                            }
                        }
                        if (!bIsSkipped && !(i == appSettings.nHabitIndex && appSettings.bSkipCurrentActivityCantDoItToday))
                        {
                            totalSecondsAllHabits += appSettings.arrAllEveningHabits[i].durationSeconds;
                            if (i <= appSettings.nHabitIndex)
                            {
                                habitCompletedSeconds += appSettings.arrAllEveningHabits[i].durationSeconds;
                            }
                        }
                    }

                    sTotal = appSettings.SecondsToText(totalSecondsAllHabits);
                    sCompleted = appSettings.SecondsToText(habitCompletedSeconds);
                    sRemaining = appSettings.SecondsToText(totalSecondsAllHabits - habitCompletedSeconds);

                    if (appSettings.arrAllEveningHabits.Count == (appSettings.nHabitIndex + 1)) //Last Activity
                    {
                        bLastActivity = true;
                        appSettings.CapturePostHog("complete-evening-routine");

                        if (appSettings.bSkipCurrentActivityCantDoItToday || appSettings.bShowActivitiesFromMidLastSkipped)
                        {
                            appSettings.bShowActivitiesFromMidLastSkipped = false;
                            sHeading = appSettings.ConfigFileStrings["activityInfo"]["lastActivitySkippedText"].ToString();
                        }
                        else
                        {
                            sHeading = appSettings.ConfigFileStrings["activityInfo"]["lastActivityFinishedText"].ToString();
                        }

                        sButtonTitle = appSettings.ConfigFileStrings["common"]["wrapupButtonText"].ToString();

                        //Hide the skip this habit button on last activity
                        SkipThisHabitText.Visibility = Visibility.Collapsed;

                        //Progress text
                        string sProgressLast = appSettings.ConfigFileStrings["activityInfo"]["habitsCompleteText"].ToString();
                        sProgressLast = sProgressLast.Replace("<duration>", sTotal);
                        sTextCompletedDuration.Text = sProgressLast;

                        //If its the last activity of evening habits,. we need a 30 seconds timer to wrap up automatically
                        sTextTimerWrapUp.Text = "00:30";
                        sTextTimerWrapUp.Visibility = Visibility.Visible;
                        dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
                        dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
                        dispatcherTimer.Start();
                        CommandManager.InvalidateRequerySuggested();

                        Sounds.PlayEveningRoutineFinishedSound();
                    }
                    else
                    {
                        appSettings.CapturePostHog("complete-evening-routine-activity");


                        if (appSettings.bSkipCurrentActivityCantDoItToday || appSettings.bShowActivitiesFromMidLastSkipped)
                        {
                            appSettings.bShowActivitiesFromMidLastSkipped = false;
                            sHeading = appSettings.ConfigFileStrings["activityInfo"]["activitySkippedText"].ToString();
                        }
                        else
                        {
                            sHeading = appSettings.ConfigFileStrings["activityInfo"]["activityFinishedText"].ToString();
                        }

                        sButtonTitle = appSettings.ConfigFileStrings["activityInfo"]["startActivityButtonText"].ToString();
                        sButtonTitle = sButtonTitle.Replace("<NAME>", appSettings.arrAllEveningHabits[appSettings.nHabitIndex + 1].name);
                        sButtonTitle = sButtonTitle.Replace("<TIME>", appSettings.SecondsToText(appSettings.arrAllEveningHabits[appSettings.nHabitIndex + 1].durationSeconds));

                        //Progress text
                        /*
                           * "activityInfo":{
                          "habitsProgressText":"You have completed <duration> of habits - <totalduration> to go. You can do it!",
                          "habitsCompleteText":"You have completed <duration> of habits.",
                          */
                        string sProgress = appSettings.ConfigFileStrings["activityInfo"]["habitsProgressText"].ToString();
                        sProgress = sProgress.Replace("<duration>", sCompleted);
                        sProgress = sProgress.Replace("<totalduration>", sRemaining);

                        sTextCompletedDuration.Text = sProgress;

                        Sounds.PlayHabitFinishedSound();
                    }

                    //Reset the cnt do it today variable here
                    //appSettings.bSkipCurrentActivityCantDoItToday = false;

                    sHeading = sHeading.Replace("<ACTIVITY>", appSettings.arrAllEveningHabits[appSettings.nHabitIndex].name);

                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                {
                    sTextCompletedDuration.Text = ""; //Nothing to show in Breaks

                    Sounds.PlayHabitFinishedSound();

                    //Hide the skip this habit button in breaks
                    SkipThisHabitText.Visibility = Visibility.Collapsed;

                    if (appSettings.isAnyMandatoryActivityIncluded)
                    {
                        if (appSettings.isMandatoryActivityGoing)
                        {
                            
                            if (appSettings.nHabitIndex >= appSettings.arrActivitiesIncludeInEveryBreak.Count() - 1) // Last activity of Include arr have been done, show details of exclude array
                            {
                                appSettings.nCountBreaksPostponedTimes = 0; //Resettings the postpone breaks counter as break activities have been finished.

                                if ( appSettings.arrActivitiesExcludeInEveryBreak.Count() == 0) //there are no EXCLUDE activities, Set Finish Titles
                                {
                                    bLastActivity = true;
                                    bBreakActivitiesFinished = true;

                                    sHeading = appSettings.ConfigFileStrings["activityInfo"]["lastActivityFinishedText"].ToString();
                                    sButtonTitle = appSettings.ConfigFileStrings["common"]["unlockMachineButtonText"].ToString();

                                    //We need to hide the Please SPare Me Button
                                    _parentWindow.btnPleaseSpareMe.Visibility = Visibility.Collapsed;

                                    var currentActivity = appSettings.arrActivitiesIncludeInEveryBreak[appSettings.nHabitIndex];
                                    if (!currentActivity.isRandomChoice)
                                    {
                                        sHeading = sHeading.Replace("<ACTIVITY>", appSettings.arrActivitiesIncludeInEveryBreak[appSettings.nHabitIndex].name);
                                    }
                                    else
                                    {
                                        sHeading = sHeading.Replace("<ACTIVITY>", appSettings.currentActivityChoice.name);
                                    }
                                }
                                else
                                {
                                    bLastActivity = true;

                                    sHeading = appSettings.ConfigFileStrings["activityInfo"]["activityFinishedText"].ToString();
                                    var currentActivity = appSettings.arrActivitiesIncludeInEveryBreak[appSettings.nHabitIndex];
                                    if (!currentActivity.isRandomChoice)
                                    {
                                        sHeading = sHeading.Replace("<ACTIVITY>", appSettings.arrActivitiesIncludeInEveryBreak[appSettings.nHabitIndex].name);
                                    }
                                    else
                                    {
                                        sHeading = sHeading.Replace("<ACTIVITY>", appSettings.currentActivityChoice.name);
                                    }

                                    //Now we need to get 1 Random activity from theExclude Array.
                                    Random rnd = new Random();
                                    int nRandom = rnd.Next(0, appSettings.arrActivitiesExcludeInEveryBreak.Count() - 1);
                                    bExcludedActivityIndex = nRandom;

                                    var currentExcludeActivity = appSettings.arrActivitiesExcludeInEveryBreak[bExcludedActivityIndex];

                                    if (!currentExcludeActivity.isRandomChoice)
                                    {
                                        sButtonTitle = appSettings.ConfigFileStrings["activityInfo"]["startActivityButtonText"].ToString();
                                        sButtonTitle = sButtonTitle.Replace("<NAME>", currentExcludeActivity.name);
                                        sButtonTitle = sButtonTitle.Replace("<TIME>", appSettings.SecondsToText(currentExcludeActivity.durationSeconds));
                                    }
                                    else
                                    {
                                        //Here we need to get the Randome Choice activity from Choices and put in  appSettings.currentActivityChoice
                                        //random choice
                                        Random rnd1 = new Random();
                                        int nRandom1 = rnd1.Next(0, currentExcludeActivity.choices.Count() - 1);
                                        appSettings.currentActivityChoice = currentExcludeActivity.choices[nRandom1]; //currentActivityChoice is the choice that we need to perform as the first activity.

                                        sButtonTitle = appSettings.ConfigFileStrings["activityInfo"]["startActivityButtonText"].ToString();
                                        sButtonTitle = sButtonTitle.Replace("<NAME>", appSettings.currentActivityChoice.name);
                                        sButtonTitle = sButtonTitle.Replace("<TIME>", appSettings.SecondsToText(currentExcludeActivity.durationSeconds));
                                    }
                                }
                            }
                            else
                            {
                                //GEt finished heading from the activity just finished
                                var activityJustFinished = appSettings.arrActivitiesIncludeInEveryBreak[appSettings.nHabitIndex];
                                if (!activityJustFinished.isRandomChoice)
                                {
                                    sHeading = appSettings.ConfigFileStrings["activityInfo"]["activityFinishedText"].ToString();
                                    sHeading = sHeading.Replace("<ACTIVITY>", activityJustFinished.name);
                                }
                                else
                                {
                                    var randomChoice = appSettings.currentActivityChoice;
                                    sHeading = appSettings.ConfigFileStrings["activityInfo"]["activityFinishedText"].ToString();
                                    sHeading = sHeading.Replace("<ACTIVITY>", randomChoice.name);
                                }

                                //GEt next activity and the button text
                                var currentActivity = appSettings.arrActivitiesIncludeInEveryBreak[appSettings.nHabitIndex + 1]; //Get the next activity to do

                                if (!currentActivity.isRandomChoice)
                                {
                                    sButtonTitle = appSettings.ConfigFileStrings["activityInfo"]["startActivityButtonText"].ToString();
                                    sButtonTitle = sButtonTitle.Replace("<NAME>", currentActivity.name);
                                    sButtonTitle = sButtonTitle.Replace("<TIME>", appSettings.SecondsToText(currentActivity.durationSeconds));
                                }
                                else
                                {
                                    //Here we need to get the Randome Choice activity from Choices and put in  appSettings.currentActivityChoice
                                    //random choice
                                    Random rnd = new Random();
                                    int nRandom = rnd.Next(0, currentActivity.choices.Count() - 1);
                                    appSettings.currentActivityChoice = currentActivity.choices[nRandom]; //currentActivityChoice is the choice that we need to perform as the first activity.

                                    sButtonTitle = appSettings.ConfigFileStrings["activityInfo"]["startActivityButtonText"].ToString();
                                    sButtonTitle = sButtonTitle.Replace("<NAME>", appSettings.currentActivityChoice.name);
                                    sButtonTitle = sButtonTitle.Replace("<TIME>", appSettings.SecondsToText(currentActivity.durationSeconds));
                                }
                            }
                        }
                        else
                        {
                            bBreakActivitiesFinished = true;
                            appSettings.nCountBreaksPostponedTimes = 0; //Resettings the postpone breaks counter as break activities have been finished.


                            sHeading = appSettings.ConfigFileStrings["activityInfo"]["lastActivityFinishedText"].ToString();
                            sButtonTitle = appSettings.ConfigFileStrings["common"]["unlockMachineButtonText"].ToString();

                            //We need to hide the Please SPare Me Button
                            _parentWindow.btnPleaseSpareMe.Visibility = Visibility.Collapsed;

                            var currentActivity = appSettings.arrActivitiesExcludeInEveryBreak[appSettings.nHabitIndex];
                            if (!currentActivity.isRandomChoice)
                            {
                                sHeading = sHeading.Replace("<ACTIVITY>", appSettings.arrActivitiesExcludeInEveryBreak[appSettings.nHabitIndex].name);
                            }
                            else
                            {
                                sHeading = sHeading.Replace("<ACTIVITY>", appSettings.currentActivityChoice.name);
                            }
                        }
                    }
                    else
                    {
                        sHeading = appSettings.ConfigFileStrings["activityInfo"]["lastActivityFinishedText"].ToString();
                        sButtonTitle = appSettings.ConfigFileStrings["common"]["unlockMachineButtonText"].ToString();

                        //We need to hide the Please SPare Me Button
                        _parentWindow.btnPleaseSpareMe.Visibility = Visibility.Collapsed;

                        sHeading = sHeading.Replace("<ACTIVITY>", appSettings.arrActivitiesExcludeInEveryBreak[appSettings.nHabitIndex].name);
                        bLastActivity = true;
                        bBreakActivitiesFinished = true;
                        appSettings.nCountBreaksPostponedTimes = 0; //Resettings the postpone breaks counter as break activities have been finished.

                    }


                }

                LogWriter.LogWrite(sHeading);
                LogWriter.LogWrite(sButtonTitle);

                Heading.Text = sHeading;
                btnStartNextActivityTXT.Text = sButtonTitle;

                if(bLastActivity)
                {
                    Grid1.RowDefinitions[0].Height = new GridLength(160);
                    TopIcon.Visibility = Visibility.Visible;
                    string imagePath = ("pack://application:,,,/Focus Bear;component/images/gifs/Smile_Small.gif");
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.UriSource = new Uri(imagePath);
                    image.EndInit();
                    ImageBehavior.SetAnimatedSource(TopIcon, image);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ActivityFinished LoadData() - " + ex.Message);
            }
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
#if DEBUG
            int val = nTimer -= 10;
#else
            int val = nTimer--;
#endif

            sTextTimerWrapUp.Text = appSettings.SecondsToTimerText(nTimer);

            CommandManager.InvalidateRequerySuggested();

            if (nTimer < 0)
            {
                sTextTimerWrapUp.Text = "00:00";
                sTextTimerWrapUp.Visibility = Visibility.Collapsed;
                try
                {
                    dispatcherTimer.Stop();
                    _parentWindow.HideFrameAndClose();
                    CommandManager.InvalidateRequerySuggested();
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite(ex.Message);
                    Debug.WriteLine(ex.Message);
                }
            }
        }

        public async void LoadGraphData()
        {
            LogWriter.LogWrite("LoadGraphData()");

            var sCurrentActivityName ="";
            bool bLogQuantity = false;
            string sCurrentLogType = "";
            bool bAverage = false; //Default SUM

            /*
             *  "common":{
                "graphQuantityText":"Quantity",
                "graphMinutesText":"Minutes",
                "graphDateText":"Date",
                "graphTimeText":"Time"
             * */

            //Clean old data
            ///webView.Source = new System.Uri("about:blank");
            ///webView1.Source = new System.Uri("about:blank");

            try
            {
                //double check graph files exists
                LogWriter.LogWrite("double check graph files exists");
                if (!File.Exists(appSettings.sGraphDaysPath))
                    File.Copy(@"Graph/GraphDays.html", appSettings.sGraphDaysPath);
                if (!File.Exists(appSettings.sGraphDaysDurationPath))
                    File.Copy(@"Graph/GraphDaysDuration.html", appSettings.sGraphDaysDurationPath);
                if (!File.Exists(appSettings.sGraphTimePath))
                    File.Copy(@"Graph/GraphTime.html", appSettings.sGraphTimePath);
                if (!File.Exists(appSettings.sGraphTimesDurationPath))
                    File.Copy(@"Graph/GraphTimeDuration.html", appSettings.sGraphTimesDurationPath);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("double check graph files exists EX- " + ex.Message);
            } 

            string sLabelQuantity = appSettings.ConfigFileStrings["common"]["graphQuantityText"].ToString();
            string sLabelMinutes = appSettings.ConfigFileStrings["common"]["graphMinutesText"].ToString();
            string sLabelDate = appSettings.ConfigFileStrings["common"]["graphDateText"].ToString();
            string sLabelTime = appSettings.ConfigFileStrings["common"]["graphTimeText"].ToString();

            string curDir = Directory.GetCurrentDirectory();
            //Uri Url = new Uri(String.Format("file:///{0}/graph/GraphDays.html", curDir));

            webView.Visibility = Visibility.Visible;
            if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
            {
                BreakGraph2.Visibility = Visibility.Collapsed;

                sCurrentLogType = "morning routine";
                if (appSettings.arrAllMorningHabits[appSettings.nHabitIndex].logQuantity)
                {
                   //mj// webView.Source = new Uri(string.Format("file:///{0}", appSettings.sGraphDaysPath), System.UriKind.Absolute);
                    webView.Source = new Uri(String.Format("file:///{0}/graph/GraphDays.html", curDir), System.UriKind.Absolute);
                }
                else
                {
                    //Prepare Duration data
                    //mj//webView.Source = new Uri(string.Format("file:///{0}", appSettings.sGraphDaysDurationPath), System.UriKind.Absolute);
                    webView.Source = new Uri(string.Format("file:///{0}/graph/GraphDaysDuration.html", curDir), System.UriKind.Absolute);
                }
                sCurrentActivityName = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].name;
                bLogQuantity = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].logQuantity;
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
            {
                BreakGraph2.Visibility = Visibility.Collapsed;

                sCurrentLogType = "evening routine";

                if (appSettings.arrAllEveningHabits[appSettings.nHabitIndex].logQuantity)
                {
                    //mj//webView.Source = new Uri(string.Format("file:///{0}", appSettings.sGraphDaysPath), System.UriKind.Absolute);
                    webView.Source = new Uri(String.Format("file:///{0}/graph/GraphDays.html", curDir), System.UriKind.Absolute);
                }
                else
                {
                    //Prepare Duration data
                    //mj//webView.Source = new Uri(string.Format("file:///{0}", appSettings.sGraphDaysDurationPath), System.UriKind.Absolute);
                    webView.Source = new Uri(string.Format("file:///{0}/graph/GraphDaysDuration.html", curDir), System.UriKind.Absolute);
                }
                sCurrentActivityName = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].name;
                bLogQuantity = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].logQuantity;
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
            {
                //ALso show the second graph
                BreakGraph2.Visibility = Visibility.Visible;
                sCurrentLogType = "break";

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
                    sCurrentActivityName = currentActivity.name;
                    bAverage = (currentActivity.logQuantityType == "AVG") ? true : false;
                }
                else
                {
                    //random choice
                    var randomChoice = appSettings.currentActivityChoice;
                    if (randomChoice.id != null)
                    {
                        bLogQuantity = randomChoice.logQuantity;
                        sCurrentActivityName = randomChoice.name;
                        bAverage = (currentActivity.logQuantityType == "AVG") ? true : false;
                    }
                }

                if (bLogQuantity)
                {
                    //mj//webView.Source = new Uri(string.Format("file:///{0}", appSettings.sGraphTimePath), System.UriKind.Absolute);
                    webView.Source = new Uri(String.Format("file:///{0}/graph/GraphTime.html", curDir), System.UriKind.Absolute);
                    //mj//webView1.Source = new Uri(string.Format("file:///{0}", appSettings.sGraphDaysPath), System.UriKind.Absolute);
                    webView1.Source = new Uri(String.Format("file:///{0}/graph/GraphDays.html", curDir), System.UriKind.Absolute);
                }
                else
                {
                    //Prepare Duration data
                    //mj//webView.Source = new Uri(string.Format("file:///{0}", appSettings.sGraphTimesDurationPath), System.UriKind.Absolute);
                    webView.Source = new Uri(String.Format("file:///{0}/graph/GraphTimeDuration.html", curDir), System.UriKind.Absolute);
                    //mj//webView1.Source = new Uri(string.Format("file:///{0}", appSettings.sGraphDaysDurationPath), System.UriKind.Absolute);
                    webView1.Source = new Uri(String.Format("file:///{0}/graph/GraphDaysDuration.html", curDir), System.UriKind.Absolute);

                }

            }

            CommandManager.InvalidateRequerySuggested();
            if (appSettings.bShowActivitiesFromMid)
            {
                await Task.Delay(2700);
            }
            else
            {
                await Task.Delay(1300);
            }


            CommandManager.InvalidateRequerySuggested();

            try
            {
                //LOAD DAYS GRAPH

                //Check if we have the data from the API
                //we will prepare the list same as we do in csv data

                List<LogEntry> ListDataForGraph = new List<LogEntry>();

                List<LogEntry> ListAPIData = new List<LogEntry>();
                string sQuestionLabelToDisplayOnGraph = "";
                string sQuestionIDToDisplayOnGraph = "";
                if (appSettings.sOneMonthActivityDataForGraph != null && appSettings.sOneMonthActivityDataForGraph.Length > 0)
                {
                    LogWriter.LogWrite("sOneMonthActivityDataForGraph from API Found!");
                    /*
                     * {"activity_id":"9a794979-3cf9-44c6-9be7-d69955168635","days_number":30,"timezone":"UTC","log_summary_type":"SUM","stat_type":"duration","items":[{"date":"2022-11-16T00:00:00.000Z","summary":"18"}]}
                     * 
                     */
                    
                    JObject sLastActivityData = JObject.Parse(appSettings.sOneMonthActivityDataForGraph);
                    if (sLastActivityData.ContainsKey("log_quantity_answers_stats"))
                    {
                        JArray log_quantity_answer_stats_arrayItems = (JArray)sLastActivityData["log_quantity_answers_stats"];
                        
                        if(log_quantity_answer_stats_arrayItems.Count() > 1)
                        {
                            if (nQuestionIndexToShowGraphs > log_quantity_answer_stats_arrayItems.Count() - 1)
                            {
                                nQuestionIndexToShowGraphs = log_quantity_answer_stats_arrayItems.Count() - 1; //Handling for multiple clicks
                               // return;
                            }
                            if (nQuestionIndexToShowGraphs == log_quantity_answer_stats_arrayItems.Count()-1)
                            {
                                //Last element
                                ButtonRight.Visibility = Visibility.Collapsed;
                            }
                            else
                            {
                                ButtonRight.Visibility = Visibility.Visible;
                            }

                            if(nQuestionIndexToShowGraphs == 0)
                            {
                                ButtonLeft.Visibility = Visibility.Collapsed;
                            }
                            else
                            {
                                ButtonLeft.Visibility = Visibility.Visible;
                            }
                        }
                        else
                        {
                            ButtonRight.Visibility = Visibility.Collapsed;
                            ButtonLeft.Visibility = Visibility.Collapsed;
                        }

                        int i = 0;
                        foreach (JObject answeritem in log_quantity_answer_stats_arrayItems)
                        {
                            if (i == nQuestionIndexToShowGraphs)
                            {
                                if (answeritem.ContainsKey("items"))
                                {
                                    JArray arrayItems = (JArray)answeritem["items"];
                                    foreach (JObject item in arrayItems)
                                    {
                                        string date = item.ContainsKey("date") ? item.GetValue("date").ToString() : "";
                                        string summary = item.ContainsKey("summary") ? item.GetValue("summary").ToString() : "";
                                        LogEntry ll = new LogEntry("", "", sCurrentActivityName, (bLogQuantity ? "" : summary), "", (bLogQuantity ? summary : ""), date, "", "", "", "");
                                        ListAPIData.Add(ll);
                                    }
                                }
                                if (answeritem.ContainsKey("question"))
                                {
                                    JObject QuestionDetails = JObject.Parse(answeritem.GetValue("question").ToString());
                                    if(QuestionDetails.ContainsKey("question"))
                                    {
                                        sQuestionLabelToDisplayOnGraph = QuestionDetails.GetValue("question").ToString();
                                        sQuestionIDToDisplayOnGraph = QuestionDetails.GetValue("id").ToString();
                                    }
                                }

                                break;
                            }
                            i++;
                        }
                    }
                }

                List<LogEntry> ss = new List<LogEntry>();
                //Seperate try catch block so that if csv reading fails somehow, it should work further 
                try
                {
                    //Get data from CSV
                    ss = appSettings.ReadCSV().ToList<LogEntry>();
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("appSettings.ReadCSV().ToList<LogEntry>() FAILED" + ex.Message);

                }

                //Get data from CSV
               // var ss = appSettings.ReadCSV().ToList<LogEntry>();

                LogEntry lastOne;
                //We need to add Todays Current Activity Done data. Logically that should be the last entry of the CSV for sure. So we can read from there and add it.
                if (appSettings.bShowActivitiesFromMid)
                {
                  // appSettings.bShowActivitiesFromMid = false; //This is the last point we need it so we should reset it to be able to post completed API further.
                  //Removed from here to support multiple graphs for multiple questions. This is now set below on action button click
                }
                else
                {
                    /** if(ss.Count > 0)
                     {
                         lastOne = ss[ss.Count - 1];
                         ListAPIData.Add(lastOne);
                     }**/
                    //Now we cannot get the current activity stats from the CSV so we need to get them from the array itself.
                    if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                    {

                        if (appSettings.arrAllMorningHabits[appSettings.nHabitIndex].logQuantityQuestions != null)
                        {
                            int m = 0;
                            foreach (QuantityQuestion question in appSettings.arrAllMorningHabits[appSettings.nHabitIndex].logQuantityQuestions)
                            {
                                if (nQuestionIndexToShowGraphs == m)
                                {
                                    string date = DateTime.Now.ToString();
                                    string summary = question.quantity.ToString();

                                    LogEntry ll = new LogEntry("", "", sCurrentActivityName, (bLogQuantity ? "" : summary), "", (bLogQuantity ? summary : ""), date, "", "", "", "");
                                    ListAPIData.Add(ll);
                                    break;
                                }
                                m++;
                            }
                        }
                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                    {
                        if (appSettings.arrAllEveningHabits[appSettings.nHabitIndex].logQuantityQuestions != null)
                        {
                            int m = 0;
                            foreach (QuantityQuestion question in appSettings.arrAllEveningHabits[appSettings.nHabitIndex].logQuantityQuestions)
                            {
                                if (nQuestionIndexToShowGraphs == m)
                                {
                                    string date = DateTime.Now.ToString();
                                    string summary = question.quantity.ToString();

                                    LogEntry ll = new LogEntry("", "", sCurrentActivityName, (bLogQuantity ? "" : summary), "", (bLogQuantity ? summary : ""), date, "", "", "", "");
                                    ListAPIData.Add(ll);
                                    break;
                                }
                                m++;
                            }
                        }
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
                            sCurrentActivityName = currentActivity.name;
                            bAverage = (currentActivity.logQuantityType == "AVG") ? true : false;
                            if (currentActivity.logQuantityQuestions != null)
                            {
                                int m = 0;
                                foreach (QuantityQuestion question in currentActivity.logQuantityQuestions)
                                {
                                    if (nQuestionIndexToShowGraphs == m)
                                    {
                                        string date = DateTime.Now.ToString();
                                        string summary = question.quantity.ToString();

                                        LogEntry ll = new LogEntry("", "", sCurrentActivityName, (bLogQuantity ? "" : summary), "", (bLogQuantity ? summary : ""), date, "", "", "", "");
                                        ListAPIData.Add(ll);
                                        break;
                                    }
                                    m++;
                                }
                            }
                        }
                        else
                        {
                            //random choice
                            var randomChoice = appSettings.currentActivityChoice;
                            if (randomChoice.id != null)
                            {
                                bLogQuantity = randomChoice.logQuantity;
                                sCurrentActivityName = randomChoice.name;
                                bAverage = (currentActivity.logQuantityType == "AVG") ? true : false;
                                if (randomChoice.logQuantityQuestions != null)
                                {
                                    int m = 0;
                                    foreach (QuantityQuestion question in randomChoice.logQuantityQuestions)
                                    {
                                        if (nQuestionIndexToShowGraphs == m)
                                        {
                                            string date = DateTime.Now.ToString();
                                            string summary = question.quantity.ToString();

                                            LogEntry ll = new LogEntry("", "", sCurrentActivityName, (bLogQuantity ? "" : summary), "", (bLogQuantity ? summary : ""), date, "", "", "", "");
                                            ListAPIData.Add(ll);
                                            break;
                                        }
                                        m++;
                                    }
                                }
                            }
                        }
                        
                    }

                        ///
                }
                ///


                if (ListAPIData.Count > 0) //Means we got something from the API
                {
                    ListDataForGraph = ListAPIData;
                }
                else
                {
                    Func<LogEntry, bool> GetCurrentActivityStats = x => x.sActivityName == sCurrentActivityName && x.sLogType == sCurrentLogType;
                    var res = ss.Where(GetCurrentActivityStats);
                    ListDataForGraph = res.ToList<LogEntry>();
                }


                string dataarrayDaysGraph = "[";

                string dataarrayTimeGraph = "[";

                string TimeValues = "[";

                //add a dummy 0 for break activities at the start and at the end.
                if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                {
                    dataarrayTimeGraph = dataarrayTimeGraph + "0,";

                    DateTime now = DateTime.Now;

                    TimeSpan tsStartTime = TimeSpan.Parse(appSettings.sDayStartTime);
                    DateTime myStartTime = new DateTime(now.Year, now.Month, now.Day, tsStartTime.Hours, tsStartTime.Minutes, 00);
                    Console.WriteLine("{0}", myStartTime.ToString());

                    TimeValues = TimeValues + "\"" + myStartTime.ToString("yyyy-MM-dd HH:mm:ss") + "\",";
                }

                //we need full one month data to show in graph
                int nMax = 0;

                for(int n = 30; n >= 0; n--)
                {
                    DateTime d30dayaback = DateTime.Now.AddDays(-n).Date;

                    bool bFound = false;
                    double nTotalQuantityForTheDay = 0;
                    int nTotalDurationForTheDay = 0;
                    int nCountForAverage = 0;
                    foreach (var m in ListDataForGraph)
                    {
                        double nQtyLogged = (m.sQuantityLogged.Length > 0) ? double.Parse(m.sQuantityLogged) : 0;

                        DateTime activityDate = DateTime.Parse(m.sStartTime).Date;
                        if (d30dayaback == activityDate)
                        {
                            bFound = true;

                            if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)//In case of break activity we need day total
                            {
                                //Lets see if we need to show the Duration Data or the Log Quantity data
                                if (bLogQuantity)
                                {
                                    nTotalQuantityForTheDay += nQtyLogged;
                                    nCountForAverage++;
                                }
                                else
                                {
                                    nTotalDurationForTheDay += int.Parse(m.sDurationInSeconds);
                                }
                            }
                            else //Morning / evening routine
                            {
                                //Lets see if we need to show the Duration Data or the Log Quantity data
                                if (bLogQuantity)
                                {
                                    Console.WriteLine(m.sQuantityLogged);
                                   
                                    dataarrayDaysGraph = dataarrayDaysGraph + nQtyLogged.ToString();

                                    if (nQtyLogged > nMax)
                                        nMax = (int) nQtyLogged;
                                }
                                else
                                {
                                    dataarrayDaysGraph = dataarrayDaysGraph + m.sDurationInSeconds;
                                    if (int.Parse(m.sDurationInSeconds) > nMax)
                                        nMax = int.Parse(m.sDurationInSeconds);
                                }

                                dataarrayDaysGraph = dataarrayDaysGraph + ",";
                                break; //In morning evening habits we need to break after we get one but in breaks we need day's Total
                            }
                        }
                                                
                    }
                    if (!bFound)
                    {
                        dataarrayDaysGraph = dataarrayDaysGraph + "0,";
                    }
                    else
                    {
                        if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                        {
                            if (bLogQuantity)
                            {
                                Console.WriteLine(nTotalQuantityForTheDay);
                                if (bAverage) //SUM or AVG
                                {
                                    nTotalQuantityForTheDay = nTotalQuantityForTheDay / nCountForAverage;
                                    dataarrayDaysGraph = dataarrayDaysGraph + nTotalQuantityForTheDay.ToString();
                                }
                                else
                                {
                                    dataarrayDaysGraph = dataarrayDaysGraph + nTotalQuantityForTheDay.ToString();
                                }
                               
                                if (nTotalQuantityForTheDay > nMax)
                                    nMax = (int) nTotalQuantityForTheDay;
                            }
                            else
                            {
                                dataarrayDaysGraph = dataarrayDaysGraph + nTotalDurationForTheDay.ToString();
                                if (nTotalDurationForTheDay > nMax)
                                    nMax = nTotalDurationForTheDay;
                            }
                            dataarrayDaysGraph = dataarrayDaysGraph + ",";
                        }
                    }
                }

                // nMax = nMax + 100;

                // nMax = nMax.Round(100) ;
                //WE need to keep the graph stats close to the actual data..so set the max values
                if (nMax < 50)
                {
                    nMax = nMax + 5;
                }
                else if (nMax < 99)
                {
                    nMax = nMax + 10;
                }
                else if (nMax < 999)
                {
                    nMax = nMax + 20;
                }
                else if (nMax < 9999)
                {
                    nMax = nMax + 50;
                }
                else
                {
                    nMax = nMax + 200;
                }

                dataarrayDaysGraph = dataarrayDaysGraph.Remove(dataarrayDaysGraph.Length - 1, 1) + "]";

                //Make the time array
                ///
                Func<LogEntry, bool> GetCurrentActivityStatsTime = x => (x.sActivityName == sCurrentActivityName && x.sLogType == sCurrentLogType );
                var timedata = ss.Where(GetCurrentActivityStatsTime); //Todays Data
                                                                      //bool bFound = false;
                int nMaxTimeGraph = 0;
                ///
                if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                {
                    if (bLogQuantity)
                    {
                        if (appSettings.sCurrentDayActivityDataForGraphQuestionsStats != null && appSettings.sCurrentDayActivityDataForGraphQuestionsStats.Length > 0)
                        {
                            LogWriter.LogWrite("sCurrentDayActivityDataForGraphQuestionsStats from API Found!");
                           

                            JObject sLastActivityData = JObject.Parse(appSettings.sCurrentDayActivityDataForGraphQuestionsStats);
                            if (sLastActivityData.ContainsKey(sQuestionIDToDisplayOnGraph))
                            {
                                JArray log_quantity_answer_stats_arrayItems = (JArray)sLastActivityData[sQuestionIDToDisplayOnGraph];
                                //int i = 0;
                                foreach (JObject answeritem in log_quantity_answer_stats_arrayItems)
                                {
                                    string date = answeritem.ContainsKey("date_logged") ? answeritem.GetValue("date_logged").ToString() : "";
                                    string summary = answeritem.ContainsKey("logged_value") ? answeritem.GetValue("logged_value").ToString() : "";
                                    DateTime DTime = DateTime.Parse(date).ToLocalTime(); //Need to convert utc to local time
                                    Console.WriteLine(summary);
                                    dataarrayTimeGraph = dataarrayTimeGraph + summary.ToString();

                                    TimeValues = TimeValues + "\"" + DTime.ToString("yyyy-MM-dd HH:mm:ss") + "\"";

                                    double nQtyLogged = (summary.Length > 0) ? double.Parse(summary) : 0;

                                    if (nQtyLogged > nMaxTimeGraph)
                                        nMaxTimeGraph = (int)nQtyLogged;

                                    dataarrayTimeGraph = dataarrayTimeGraph + ",";
                                    TimeValues = TimeValues + ",";

                                }
                            }
                        }
                        //Now we need to add the current done value
                        ///
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
                            if (currentActivity.logQuantityQuestions != null)
                            {
                                int m = 0;
                                foreach (QuantityQuestion question in currentActivity.logQuantityQuestions)
                                {
                                    if (nQuestionIndexToShowGraphs == m)
                                    {
                                       // string date = DateTime.Now.ToString();
                                        string summary = question.quantity.ToString();

                                        dataarrayTimeGraph = dataarrayTimeGraph + summary.ToString();

                                        TimeValues = TimeValues + "\"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\"";

                                        double nQtyLogged = (summary.Length > 0) ? double.Parse(summary) : 0;

                                        if (nQtyLogged > nMaxTimeGraph)
                                            nMaxTimeGraph = (int)nQtyLogged;

                                        dataarrayTimeGraph = dataarrayTimeGraph + ",";
                                        TimeValues = TimeValues + ",";

                                        break;
                                    }
                                    m++;
                                }
                            }
                        }
                        else
                        {
                            //random choice
                            var randomChoice = appSettings.currentActivityChoice;
                            if (randomChoice.id != null)
                            {
                                 if (randomChoice.logQuantityQuestions != null)
                                {
                                    int m = 0;
                                    foreach (QuantityQuestion question in randomChoice.logQuantityQuestions)
                                    {
                                        if (nQuestionIndexToShowGraphs == m)
                                        {
                                            string date = DateTime.Now.ToString();
                                            string summary = question.quantity.ToString();

                                            dataarrayTimeGraph = dataarrayTimeGraph + summary.ToString();

                                            TimeValues = TimeValues + "\"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\"";

                                            double nQtyLogged = (summary.Length > 0) ? double.Parse(summary) : 0;

                                            if (nQtyLogged > nMaxTimeGraph)
                                                nMaxTimeGraph = (int)nQtyLogged;

                                            dataarrayTimeGraph = dataarrayTimeGraph + ",";
                                            TimeValues = TimeValues + ",";
                                            break;
                                        }
                                        m++;
                                    }
                                }
                            }
                        }
                        ///

                    }
                    else
                    {
                        foreach (var m in timedata)
                        {
 
                            //Chck if the date is current date
                            Console.WriteLine("{0} ---{1}...{2}", m.sStartTime, DateTime.Parse(m.sStartTime).Date.ToString("yyyy-MM-dd"), DateTime.Now.Date.ToString("yyyy-MM-dd"));
                            if ((DateTime.Parse(m.sStartTime).Date.ToString("yyyy-MM-dd") == DateTime.Now.Date.ToString("yyyy-MM-dd")))
                            {
                                Console.WriteLine("Yes");
                                //Lets see if we need to show the Duration Data or the Log Quantity data
                                if (bLogQuantity)
                                {
                                   
                                }
                                else
                                {
                                    TimeValues = TimeValues + "\"" + m.sStartTime + "\"";

                                    dataarrayTimeGraph = dataarrayTimeGraph + m.sDurationInSeconds.ToString();
                                    if (int.Parse(m.sDurationInSeconds) > nMaxTimeGraph)
                                        nMaxTimeGraph = int.Parse(m.sDurationInSeconds);
                                }

                                dataarrayTimeGraph = dataarrayTimeGraph + ",";
                                TimeValues = TimeValues + ",";
                            }
                            else
                            {
                                Console.WriteLine("No");
                            }


                        }
                    }
                    ///
                    if (nMaxTimeGraph < 50)
                    {
                        nMaxTimeGraph = nMaxTimeGraph + 5;
                    }
                    else if (nMaxTimeGraph < 99)
                    {
                        nMaxTimeGraph = nMaxTimeGraph + 10;
                    }
                    else if (nMaxTimeGraph < 999)
                    {
                        nMaxTimeGraph = nMaxTimeGraph + 20;
                    }
                    else if (nMaxTimeGraph < 9999)
                    {
                        nMaxTimeGraph = nMaxTimeGraph + 50;
                    }
                    else
                    {
                        nMaxTimeGraph = nMaxTimeGraph + 200;
                    }
                    ///

                    //nMaxTimeGraph = nMaxTimeGraph + 50;
                }
                ///

               

                if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                {
                    //add a dummy 0 for break activities at the start and at the end.
                    dataarrayTimeGraph = dataarrayTimeGraph + "0,";


                    DateTime now = DateTime.Now;

                    TimeSpan tsShutOffTime = TimeSpan.Parse(appSettings.sDayShutOffTime);
                    DateTime myShutOffTime = new DateTime(now.Year, now.Month, now.Day, tsShutOffTime.Hours, tsShutOffTime.Minutes, 00);
                    Console.WriteLine("{0}", myShutOffTime.ToString());

                    TimeValues = TimeValues + "\"" + myShutOffTime.ToString("yyyy-MM-dd HH:mm:ss") + "\",";
                }

                dataarrayTimeGraph = dataarrayTimeGraph.Remove(dataarrayTimeGraph.Length - 1, 1) + "]";
                TimeValues = TimeValues.Remove(TimeValues.Length - 1, 1) + "]";

                ///



                string json = "";
                string jsonTime = "";
                //Lets see if we need to show the Duration Data or the Log Quantity data
                if (bLogQuantity)
                {
                    //Manish jain yahan se chalu karo
                    //formulate the data
                    if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                    {
                        //Prepare Log Quantity data
                        json = string.Format("{{ \"label\": \"{0}\", \"labelX\": \"{1}\", \"labelY\": \"{2}\", \"data\": {3}, \"max\": {4} }}", sQuestionLabelToDisplayOnGraph, sLabelDate, sLabelQuantity, dataarrayDaysGraph, nMax.ToString());

                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                    {
                        //Prepare Log Quantity data
                        json = string.Format("{{ \"label\": \"{0}\", \"labelX\": \"{1}\", \"labelY\": \"{2}\", \"data\": {3}, \"max\": {4} }}", sQuestionLabelToDisplayOnGraph, sLabelDate, sLabelQuantity, dataarrayDaysGraph, nMax.ToString());

                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                    {
                        //Prepare Log Quantity data
                        json = string.Format("{{ \"label\": \"{0}\", \"labelX\": \"{1}\", \"labelY\": \"{2}\", \"data\": {3}, \"max\": {4} }}", sQuestionLabelToDisplayOnGraph, sLabelDate, sLabelQuantity, dataarrayDaysGraph, nMax.ToString());
                        jsonTime = string.Format("{{ \"label\": \"{0}\", \"labelX\": \"{1}\", \"labelY\": \"{2}\", \"data\": {3}, \"max\": {4}, \"TimeValues\": {5} }}", sQuestionLabelToDisplayOnGraph, sLabelTime, sLabelQuantity, dataarrayTimeGraph, nMaxTimeGraph.ToString(), TimeValues);
                    }

                }
                else
                {
                   
                    if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                    {
                        //Prepare Duration data
                        json = string.Format("{{ \"label\": \"{0}\", \"labelX\": \"{1}\", \"labelY\": \"{2}\", \"data\": {3}, \"max\": {4} }}", appSettings.arrAllMorningHabits[appSettings.nHabitIndex].name, sLabelDate, sLabelMinutes, dataarrayDaysGraph, nMax.ToString());

                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                    {
                        //Prepare Duration data
                        json = string.Format("{{ \"label\": \"{0}\", \"labelX\": \"{1}\", \"labelY\": \"{2}\", \"data\": {3}, \"max\": {4} }}", appSettings.arrAllEveningHabits[appSettings.nHabitIndex].name, sLabelDate, sLabelMinutes, dataarrayDaysGraph, nMax.ToString());

                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                    {
                        //Prepare Duration data
                        json = string.Format("{{ \"label\": \"{0}\", \"labelX\": \"{1}\", \"labelY\": \"{2}\", \"data\": {3}, \"max\": {4} }}", sCurrentActivityName, sLabelDate, sLabelMinutes, dataarrayDaysGraph, nMax.ToString());
                        jsonTime = string.Format("{{ \"label\": \"{0}\", \"labelX\": \"{1}\", \"labelY\": \"{2}\", \"data\": {3}, \"max\": {4}, \"TimeValues\": {5} }}", sCurrentActivityName, sLabelTime, sLabelMinutes, dataarrayTimeGraph, nMaxTimeGraph.ToString(), TimeValues);
                    }
                }


                string functiontocall = string.Format("getData({0});", json);

                string functiontocallTime = string.Format("getData({0});", jsonTime);

                if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                {
                    await webView.ExecuteScriptAsync(functiontocallTime);
                    await webView1.ExecuteScriptAsync(functiontocall);
                }
                else
                {
                    await webView.ExecuteScriptAsync(functiontocall);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("LoadGraphData() - " + ex.Message);
 
            }

        }
        private void btnStartNextActivity_Click(object sender, RoutedEventArgs e)
        {
            if (appSettings.bShowActivitiesFromMid)
            {
                appSettings.bShowActivitiesFromMid = false; //This is the last point we need it so we should reset it to be able to post completed API further.
            }

            if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
            {
                
                if (bBreakActivitiesFinished)
                {
                    if(appSettings.sBrainDump.Length > 0) //Onmly show if there is text.
                        ((App)(Application.Current)).ShowBrainDumpWindow();
                    
                    _parentWindow.HideFrameAndClose();
                    return;
                }
                if (bExcludedActivityIndex >= 0) // Last activity of Include arr have been done, show details of exclude array
                {
                    appSettings.isMandatoryActivityGoing = false;
                    appSettings.nHabitIndex = bExcludedActivityIndex;
                }
                else
                {
                    appSettings.nHabitIndex++; //Increment the index to do the next habit
                }
                _parentWindow.ShowActivityStartScreen();
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
            {
                if (appSettings.arrAllMorningHabits.Count == (appSettings.nHabitIndex + 1)) //Last Activity
                {
                    _parentWindow.HideFrameAndClose();
                }
                else
                {
                    appSettings.nHabitIndex++; //Increment the index to do the next habit
                    _parentWindow.ShowActivityStartScreen();
                }
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
            {
                if (appSettings.arrAllEveningHabits.Count == (appSettings.nHabitIndex + 1)) //Last Activity
                {
                    _parentWindow.HideFrameAndClose();
                }
                else
                {
                    appSettings.nHabitIndex++; //Increment the index to do the next habit
                    _parentWindow.ShowActivityStartScreen();
                }
            }

            LogWriter.LogWrite("HabitIndex now = " + appSettings.nHabitIndex.ToString());
        }

        private void btnSkipThisHabit_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Activity Finished Screen btnSkipThisHabit_Clicked ");

            if (appSettings.bShowActivitiesFromMid)
            {
                appSettings.bShowActivitiesFromMid = false; //This is the last point we need it so we should reset it to be able to post completed API further.
            }

            ///
            if (appSettings.isPasswordSet)
            {
                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                {
                    if (appSettings.bPasswordRequireInSkipMorning)
                    {
                        //It might be possible that a webview is visible, we will need to hide that
                        webView.Visibility = Visibility.Collapsed;
                        webView1.Visibility = Visibility.Collapsed;

                        AskPassword pwdWnd = new AskPassword();
                        pwdWnd.Owner = _parentWindow;
                        _parentWindow.GridShade.Visibility = Visibility.Visible;

                        bool? bResult = pwdWnd.ShowDialog();

                        _parentWindow.GridShade.Visibility = Visibility.Collapsed;
                         webView.Visibility = Visibility.Visible;
                         webView1.Visibility = Visibility.Visible;
                        

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
                else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    if (appSettings.bPasswordRequireInSkipEvening)
                    {
                        //It might be possible that a webview is visible, we will need to hide that
                       
                        webView.Visibility = Visibility.Collapsed;
                        webView1.Visibility = Visibility.Collapsed;
                       
                        AskPassword pwdWnd = new AskPassword();
                        pwdWnd.Owner = _parentWindow;
                        _parentWindow.GridShade.Visibility = Visibility.Visible;

                        bool? bResult = pwdWnd.ShowDialog();

                        _parentWindow.GridShade.Visibility = Visibility.Collapsed;
                         webView.Visibility = Visibility.Visible;
                         webView1.Visibility = Visibility.Visible;
                         

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
            ///

            webView.Visibility = Visibility.Collapsed; /*When using Webview2 in WPF, unable to overlay WPF controls on the Webview - https://github.com/MicrosoftEdge/WebView2Feedback/issues/286*/
            _parentWindow.ShowWhySkipHabitsWindow();
        }

        private void ButtonLeft_Click(object sender, RoutedEventArgs e)
        {
            nQuestionIndexToShowGraphs--;
            LoadGraphData();
        }

        private void ButtonRight_Click(object sender, RoutedEventArgs e)
        {
            nQuestionIndexToShowGraphs++;
            LoadGraphData();
        }
    }
}
