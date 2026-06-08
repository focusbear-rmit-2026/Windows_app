using Focus_Bear.Models;
using Focus_Bear.Onboarding;
using Focus_Bear.Preferences;
using Focus_Bear.Services.FocusButton;
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
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
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
        private FocusBearMainWindow _parentWindow = null;
        AppSettings appSettings = AppSettings.GetInstance;

        int nTimer = 30;
 
        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);

        int bExcludedActivityIndex = -1;
        
        public int nQuestionIndexToShowGraphs = 0;

        private bool bBreakActivitiesFinished;

        public bool bWebViewInitialized = false;
        public bool bWebView2Initialized = false;
        public bool bCallLoadGraphDataAfterWebViewInitialized = false;
        public HabitActivityFinished()
        {
           
        }
        public HabitActivityFinished(FocusBearMainWindow parentWindow) : this()
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


            webView.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
            webView1.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted1;

            SetWebView();
            SetWebView1();

            _parentWindow = parentWindow;
            bBreakActivitiesFinished = false;
            bExcludedActivityIndex = -1;
        
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
                    webView.CoreWebView2.Settings.AreHostObjectsAllowed = true;
                    webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                    webView.CoreWebView2.Settings.IsScriptEnabled = true;
                    webView.CoreWebView2.Settings.IsWebMessageEnabled = true;

                    webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                    webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

                    bWebViewInitialized = true;

                    if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                    {
                        if(bWebView2Initialized) //Only if webview 2 is also initialized in case of break activities
                        {
                            if (appSettings.bShowActivitiesFromMid || bCallLoadGraphDataAfterWebViewInitialized)//Justload the graph data in this case
                            {
                                LoadGraphData();
                            }
                        }
                    }
                    else
                    {
                        if (appSettings.bShowActivitiesFromMid || bCallLoadGraphDataAfterWebViewInitialized)//Justload the graph data in this case
                        {
                            LoadGraphData();
                        }
                    }
                   

                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("Activity Finished CoreWebView2InitializationCompleted() ex - " + ex.Message);
                }
            }
        }

        private void CoreWebView2InitializationCompleted1(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                try
                {
                    webView1.CoreWebView2.Settings.AreHostObjectsAllowed = true;
                    webView1.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                    webView1.CoreWebView2.Settings.IsScriptEnabled = true;
                    webView1.CoreWebView2.Settings.IsWebMessageEnabled = true;

                    webView1.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                    webView1.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

                    bWebView2Initialized = true;

                    if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                    {
                        if (bWebViewInitialized) //Only if webview 1 is also initialized in case of break activities
                        {
                            if (appSettings.bShowActivitiesFromMid || bCallLoadGraphDataAfterWebViewInitialized)//Justload the graph data in this case
                            {
                                LoadGraphData();
                            }
                        }
                    }

                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("Activity Finished CoreWebView2InitializationCompleted1() ex - " + ex.Message);
                }
            }
        }

        public void SwapTempChoiceData()
        {
            if(appSettings.currentActivityChoiceTemp.id != null)
                appSettings.currentActivityChoice = appSettings.currentActivityChoiceTemp;
        }
        public void LoadData(bool bSkipCurrentActivity = false)
        {
            LogWriter.LogWrite("ActivityFinished LoadData()");

            //Placing an special check here
            //in any case if the habit index exceeds the array count, we will close the habit window and make state none. Rest will be handled.
            if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
            {
                if(appSettings.nHabitIndex > appSettings.arrAllMorningHabits.Count())
                {
                    LogWriter.LogWrite("ActivityFinished LoadData() appSettings.nHabitIndex > appSettings.arrAllMorningHabits.Count() MAKE NONE");
                    _parentWindow.PageHabits.HideFrameAndClose();
                }
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
            {
                if (appSettings.nHabitIndex > appSettings.arrAllEveningHabits.Count())
                {
                    LogWriter.LogWrite("ActivityFinished LoadData() appSettings.nHabitIndex > appSettings.arrAllEveningHabits.Count() MAKE NONE");
                    _parentWindow.PageHabits.HideFrameAndClose();
                }
            }

            try
            {
                bBreakActivitiesFinished = false;

                btnSkipThisHabitTXT.Text = appSettings.ConfigFileStrings["activityInfo"]["buttonSkipHabitText"].ToString();
                ChooseAnotherHabitTXT.Text = appSettings.ConfigFileStrings["activityInfo"]["buttonChooseHabitText"].ToString();

                string sHeading = "";

                //Set button text with next activity start info
                string sButtonTitle = "";

                int totalSecondsAllHabits = appSettings.nTotalSecondsAllHabits;
                int habitCompletedSeconds = appSettings.nHabitCompletedSeconds;

                string sTotal = "";
                string sCompleted = "";
                string sRemaining = "";

                bool bLastActivity = false;

                //Show the skip this habit button initially, it will be handled below
                SkipThisHabitText.Visibility = Visibility.Visible;
                ChooseAnotherHabit.Visibility = Visibility.Visible;
                Seperator.Visibility = Visibility.Visible;

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

                    if (appSettings.arrAllMorningHabits.Count == (appSettings.nHabitIndex + 2)) //Second Last Activity
                    {
                        //do not show the  ChooseAnotherHabit button
                        ChooseAnotherHabit.Visibility = Visibility.Collapsed;
                        Seperator.Visibility = Visibility.Collapsed;
                    }
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
                        ChooseAnotherHabit.Visibility = Visibility.Collapsed;
                        Seperator.Visibility = Visibility.Collapsed;

                        //We need to hide the Please SPare Me Button
                        _parentWindow.PageHabits.btnPleaseSpareMe.Visibility = Visibility.Collapsed;
                        _parentWindow.PageHabits.HideCloseButton();

                        //Progress text
                        string sProgressLast = appSettings.ConfigFileStrings["activityInfo"]["habitsCompleteText"].ToString();
                        sProgressLast = sProgressLast.Replace("<duration>", sTotal);
                        sTextCompletedDuration.Text = sProgressLast;

                        Sounds.PlayMorningRoutineFinishedSound();

                        App.FocusButton.RemoveHabits(); //Remove habits List to device after finishing this last habit  
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
                        
                        if(appSettings.arrAllMorningHabits[appSettings.nHabitIndex + 1].useTimer)
                        {
                            sButtonTitle = appSettings.ConfigFileStrings["activityInfo"]["startActivityButtonText"].ToString();
                            sButtonTitle = sButtonTitle.Replace("<NAME>", appSettings.arrAllMorningHabits[appSettings.nHabitIndex + 1].name);
                            sButtonTitle = sButtonTitle.Replace("<TIME>", appSettings.SecondsToText(appSettings.arrAllMorningHabits[appSettings.nHabitIndex + 1].durationSeconds));

                        }
                        else
                        {
                            sButtonTitle = appSettings.ConfigFileStrings["activityInfo"]["startNonTimerActivityButtonText"].ToString();
                            sButtonTitle = sButtonTitle.Replace("<NAME>", appSettings.arrAllMorningHabits[appSettings.nHabitIndex + 1].name);
                        }
                        //Progress text
                        string sProgress = appSettings.ConfigFileStrings["activityInfo"]["habitsProgressText"].ToString();
                        sProgress = sProgress.Replace("<duration>", sCompleted);
                        sProgress = sProgress.Replace("<totalduration>", sRemaining);

                        sTextCompletedDuration.Text = sProgress;

                        Sounds.PlayHabitFinishedSound();

                        App.FocusButton.SendHabits(true); //Update habits List to device after finishing this habit  
                    }
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
                    ///
                    bool isSequenceFinished = false;
                    int newHabitIndex = appSettings.nHabitIndex + 1;

                    if (appSettings.arrAllEveningHabits.Count == (appSettings.nHabitIndex + 2)) //Second Last Activity
                    {
                        //do not show the  ChooseAnotherHabit button
                        ChooseAnotherHabit.Visibility = Visibility.Collapsed;
                        Seperator.Visibility = Visibility.Collapsed;
                    }

                    if (appSettings.nHabitIndex < appSettings.arrAllEveningHabits.Count - 1)
                    {
                        if (appSettings.DidCutOffTimePassed())
                        {
                            while (!appSettings.arrAllEveningHabits[newHabitIndex].bHighPriorityActivity)
                            {
                                newHabitIndex = newHabitIndex + 1;

                                if (newHabitIndex > appSettings.arrAllEveningHabits.Count - 1)
                                {
                                    isSequenceFinished = true;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        isSequenceFinished = true;
                    }
                    
                    ///
                    if (isSequenceFinished) //Last Activity
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


                        sHeading = sHeading.Replace("<ACTIVITY>", appSettings.arrAllEveningHabits[appSettings.nHabitIndex].name);

                        if (appSettings.DidCutOffTimePassed())
                        {
                            sButtonTitle = appSettings.ConfigFileStrings["common"]["wrapupButtonText"].ToString();
                        }
                        else
                        {
                            sButtonTitle = appSettings.ConfigFileStrings["common"]["unlockMachineButtonText"].ToString(); //unlock machine
                        }

                        //Hide the skip this habit button on last activity
                        SkipThisHabitText.Visibility = Visibility.Collapsed;
                        ChooseAnotherHabit.Visibility = Visibility.Collapsed;
                        Seperator.Visibility = Visibility.Collapsed;

                        //We need to hide the Please SPare Me Button
                        _parentWindow.PageHabits.btnPleaseSpareMe.Visibility = Visibility.Collapsed;
                        _parentWindow.PageHabits.HideCloseButton();

                        //Progress text
                        string sProgressLast = appSettings.ConfigFileStrings["activityInfo"]["habitsCompleteText"].ToString();
                        sProgressLast = sProgressLast.Replace("<duration>", sTotal);
                        sTextCompletedDuration.Text = sProgressLast;

                        appSettings.nHabitIndex = newHabitIndex - 1; //assign the new habit index to our main habit index less 1 as it will be increased on action button click
                        LogWriter.LogWrite("nHabitIndex = " + appSettings.nHabitIndex.ToString());


                        //If its the last activity of evening habits,. we need a 30 seconds timer to wrap up automatically
                        if (!appSettings.arrAllEveningHabits[appSettings.nHabitIndex].logQuantity)
                        {
                            sTextTimerWrapUp.Text = "00:30";
                            sTextTimerWrapUp.Visibility = Visibility.Visible;
                            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
                            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
                            dispatcherTimer.Start();
                            CommandManager.InvalidateRequerySuggested();
                        }

                        Sounds.PlayEveningRoutineFinishedSound();

                        App.FocusButton.RemoveHabits(); //Remove habits List to device after finishing this last habit  
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

                        sHeading = sHeading.Replace("<ACTIVITY>", appSettings.arrAllEveningHabits[appSettings.nHabitIndex].name);

                        if (appSettings.arrAllEveningHabits[newHabitIndex].useTimer)
                        {

                            sButtonTitle = appSettings.ConfigFileStrings["activityInfo"]["startActivityButtonText"].ToString();
                            sButtonTitle = sButtonTitle.Replace("<NAME>", appSettings.arrAllEveningHabits[newHabitIndex].name);
                            sButtonTitle = sButtonTitle.Replace("<TIME>", appSettings.SecondsToText(appSettings.arrAllEveningHabits[newHabitIndex].durationSeconds));
                        }
                        else
                        {
                            sButtonTitle = appSettings.ConfigFileStrings["activityInfo"]["startNonTimerActivityButtonText"].ToString();
                            sButtonTitle = sButtonTitle.Replace("<NAME>", appSettings.arrAllEveningHabits[newHabitIndex].name);
                        }

                        string sProgress = appSettings.ConfigFileStrings["activityInfo"]["habitsProgressText"].ToString();
                        sProgress = sProgress.Replace("<duration>", sCompleted);
                        sProgress = sProgress.Replace("<totalduration>", sRemaining);

                        sTextCompletedDuration.Text = sProgress;

                        appSettings.nHabitIndex = newHabitIndex - 1; //assign the new habit index to our main habit index less 1 as it will be increased on action button click
                        LogWriter.LogWrite("nHabitIndex = " + appSettings.nHabitIndex.ToString());

                        Sounds.PlayHabitFinishedSound();

                        App.FocusButton.SendHabits(true); //Update habits List to device after finishing this habit  
                    }

                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                {
                    sTextCompletedDuration.Text = ""; //Nothing to show in Breaks

                    Sounds.PlayHabitFinishedSound();

                    //Hide the skip this habit button in breaks
                    SkipThisHabitText.Visibility = Visibility.Collapsed;
                    ChooseAnotherHabit.Visibility = Visibility.Collapsed;
                    Seperator.Visibility = Visibility.Collapsed;

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

                                    //We need to hide the Please Spare Me Button
                                    _parentWindow.PageHabits.btnPleaseSpareMe.Visibility = Visibility.Collapsed;
                                    _parentWindow.PageHabits.HideCloseButton();

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
                                        appSettings.currentActivityChoiceTemp = currentExcludeActivity.choices[nRandom1]; //currentActivityChoice is the choice that we need to perform as the first activity.

                                        sButtonTitle = appSettings.ConfigFileStrings["activityInfo"]["startActivityButtonText"].ToString();
                                        sButtonTitle = sButtonTitle.Replace("<NAME>", appSettings.currentActivityChoiceTemp.name);
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

                                //Get next activity and the button text
                                var currentActivity = appSettings.arrActivitiesIncludeInEveryBreak[appSettings.nHabitIndex + 1]; //Get the next activity to do

                                if (!currentActivity.isRandomChoice)
                                {
                                    if(currentActivity.useTimer)
                                    {
                                        sButtonTitle = appSettings.ConfigFileStrings["activityInfo"]["startActivityButtonText"].ToString();
                                        sButtonTitle = sButtonTitle.Replace("<NAME>", currentActivity.name);
                                        sButtonTitle = sButtonTitle.Replace("<TIME>", appSettings.SecondsToText(currentActivity.durationSeconds));
                                    }
                                    else
                                    {
                                        sButtonTitle = appSettings.ConfigFileStrings["activityInfo"]["startNonTimerActivityButtonText"].ToString();
                                        sButtonTitle = sButtonTitle.Replace("<NAME>", currentActivity.name);

                                    }
                                   
                                }
                                else
                                {
                                    //Here we need to get the Randome Choice activity from Choices and put in  appSettings.currentActivityChoice
                                    //random choice
                                    Random rnd = new Random();
                                    int nRandom = rnd.Next(0, currentActivity.choices.Count() - 1);
                                    appSettings.currentActivityChoiceTemp = currentActivity.choices[nRandom]; //currentActivityChoice is the choice that we need to perform as the first activity.

                                    if (currentActivity.useTimer)
                                    {
                                        sButtonTitle = appSettings.ConfigFileStrings["activityInfo"]["startActivityButtonText"].ToString();
                                        sButtonTitle = sButtonTitle.Replace("<NAME>", appSettings.currentActivityChoiceTemp.name);
                                        sButtonTitle = sButtonTitle.Replace("<TIME>", appSettings.SecondsToText(currentActivity.durationSeconds));
                                    }
                                    else
                                    {
                                        sButtonTitle = appSettings.ConfigFileStrings["activityInfo"]["startNonTimerActivityButtonText"].ToString();
                                        sButtonTitle = sButtonTitle.Replace("<NAME>", appSettings.currentActivityChoiceTemp.name);

                                    }
                                }
                            }
                        }
                        else
                        {
                            bBreakActivitiesFinished = true;
                            appSettings.nCountBreaksPostponedTimes = 0; //Resetting the postpone breaks counter as break activities have been finished.


                            sHeading = appSettings.ConfigFileStrings["activityInfo"]["lastActivityFinishedText"].ToString();
                            sButtonTitle = appSettings.ConfigFileStrings["common"]["unlockMachineButtonText"].ToString();

                            //We need to hide the Please Spare Me Button
                            _parentWindow.PageHabits.btnPleaseSpareMe.Visibility = Visibility.Collapsed;
                            _parentWindow.PageHabits.HideCloseButton();

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
                        _parentWindow.PageHabits.btnPleaseSpareMe.Visibility = Visibility.Collapsed;
                        _parentWindow.PageHabits.HideCloseButton();

                        sHeading = sHeading.Replace("<ACTIVITY>", appSettings.arrActivitiesExcludeInEveryBreak[appSettings.nHabitIndex].name);
                        bLastActivity = true;
                        bBreakActivitiesFinished = true;
                        appSettings.nCountBreaksPostponedTimes = 0; //Resetting the postpone breaks counter as break activities have been finished.

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
                LogWriter.LogWrite("ActivityFinished LoadData() EX - " + ex.Message);
            }
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
#if DEBUG
            int val = nTimer -= 2;
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
                    _parentWindow.PageHabits.HideFrameAndClose();
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

            string sLabelQuantity = appSettings.ConfigFileStrings["common"]["graphQuantityText"].ToString();
            string sLabelMinutes = appSettings.ConfigFileStrings["common"]["graphMinutesText"].ToString();
            string sLabelDate = appSettings.ConfigFileStrings["common"]["graphDateText"].ToString();
            string sLabelTime = appSettings.ConfigFileStrings["common"]["graphTimeText"].ToString();

            string curDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            curDir = curDir.TrimEnd('\\');

            webView.Visibility = Visibility.Visible;
            if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
            {
                BreakGraph2.Visibility = Visibility.Collapsed;

                sCurrentLogType = "morning routine";
                if (appSettings.arrAllMorningHabits[appSettings.nHabitIndex].logQuantity)
                {
                    webView.Source = new Uri(string.Format("file:///{0}/graph/GraphDays.html", curDir), System.UriKind.Absolute);
                    LogWriter.LogWrite("GraphDays.html PATH - " + webView.Source);
                }
                else
                {
                    //Prepare Duration data
                    webView.Source = new Uri(string.Format("file:///{0}/graph/GraphDaysDuration.html", curDir), System.UriKind.Absolute);
                    LogWriter.LogWrite("GraphDaysDuration.html PATH - " + webView.Source);
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
                    webView.Source = new Uri(string.Format("file:///{0}/graph/GraphDays.html", curDir), System.UriKind.Absolute);
                    LogWriter.LogWrite("GraphDays.html PATH - " + webView.Source);
                }
                else
                {
                    //Prepare Duration data
                    webView.Source = new Uri(string.Format("file:///{0}/graph/GraphDaysDuration.html", curDir), System.UriKind.Absolute);
                    LogWriter.LogWrite("GraphDaysDuration.html PATH - " + webView.Source);
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
                    webView.Source = new Uri(string.Format("file:///{0}/graph/GraphTime.html", curDir), System.UriKind.Absolute);
                    webView1.Source = new Uri(string.Format("file:///{0}/graph/GraphDays.html", curDir), System.UriKind.Absolute);
                    LogWriter.LogWrite("GraphTime.html PATH - " + webView.Source);
                    LogWriter.LogWrite("GraphDays.html PATH - " + webView1.Source);
                }
                else
                {
                    //Prepare Duration data
                    webView.Source = new Uri(string.Format("file:///{0}/graph/GraphTimeDuration.html", curDir), System.UriKind.Absolute);
                    webView1.Source = new Uri(string.Format("file:///{0}/graph/GraphDaysDuration.html", curDir), System.UriKind.Absolute);
                    LogWriter.LogWrite("GraphTimeDuration.html PATH - " + webView.Source);
                    LogWriter.LogWrite("GraphDaysDuration.html PATH - " + webView1.Source);

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
       
                    JObject sLastActivityData = JObject.Parse(appSettings.sOneMonthActivityDataForGraph);
                    if (bLogQuantity)
                    {
                        if (sLastActivityData.ContainsKey("log_quantity_answers_stats"))
                        {
                            JArray log_quantity_answer_stats_arrayItems = (JArray)sLastActivityData["log_quantity_answers_stats"];

                            if (log_quantity_answer_stats_arrayItems.Count() > 1)
                            {
                                if (nQuestionIndexToShowGraphs > log_quantity_answer_stats_arrayItems.Count() - 1)
                                {
                                    nQuestionIndexToShowGraphs = log_quantity_answer_stats_arrayItems.Count() - 1; //Handling for multiple clicks
                                }
                                if (nQuestionIndexToShowGraphs == log_quantity_answer_stats_arrayItems.Count() - 1)
                                {
                                    //Last element
                                    ButtonRight.Visibility = Visibility.Collapsed;
                                }
                                else
                                {
                                    ButtonRight.Visibility = Visibility.Visible;
                                }

                                if (nQuestionIndexToShowGraphs == 0)
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
                                        if (QuestionDetails.ContainsKey("question"))
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
                    else if (sLastActivityData.ContainsKey("items")) //FOr duration related monthly data
                    {
                        JArray arrayItems = (JArray)sLastActivityData["items"];
                        foreach (JObject item in arrayItems)
                        {
                            string date = item.ContainsKey("date") ? item.GetValue("date").ToString() : "";
                            string summary = item.ContainsKey("summary") ? item.GetValue("summary").ToString() : "";
                            LogEntry ll = new LogEntry("", "", sCurrentActivityName, (bLogQuantity ? "" : summary), "", (bLogQuantity ? summary : ""), date, "", "", "", "");
                            ListAPIData.Add(ll);
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
                    //best to delete the file as it cannot be red further.
                    if (File.Exists(appSettings.sLogQuantityCSVPath))
                    {
                        Global.DeleteFile(appSettings.sLogQuantityCSVPath, false);
                    }
                    
                }

                if (!bLogQuantity)
                {
                    if (ss.Count > 0)
                    {
                        LogEntry lastOne = ss[ss.Count - 1];
                        ListAPIData.Add(lastOne);
                    }
                }


                //We need to add Todays Current Activity Done data. Logically that should be the last entry of the CSV for sure. So we can read from there and add it.
                if (appSettings.bShowActivitiesFromMid)
                {
                  // appSettings.bShowActivitiesFromMid = false; //This is the last point we need it so we should reset it to be able to post completed API further.
                  //Removed from here to support multiple graphs for multiple questions. This is now set below on action button click
                }
                else
                {
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
                }


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
                Func<LogEntry, bool> GetCurrentActivityStatsTime = x => (x.sActivityName == sCurrentActivityName && x.sLogType == sCurrentLogType );
                var timedata = ss.Where(GetCurrentActivityStatsTime); //Todays Data

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
                }

                if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                {
                    //add a dummy 0 for break activities at the start and at the end.
                    dataarrayTimeGraph = dataarrayTimeGraph + "0,";


                    DateTime now = DateTime.Now;

                    TimeSpan tsShutOffTime = TimeSpan.Parse(appSettings.sDayShutOffTime);
                    DateTime myShutOffTime = new DateTime(now.Year, now.Month, now.Day, tsShutOffTime.Hours, tsShutOffTime.Minutes, 00);
                    Console.WriteLine("{0}", myShutOffTime.ToString());

                    //Precaution checking the start time shutof time if after 12:00
                    TimeSpan tsStartTime = TimeSpan.Parse(appSettings.sDayStartTime);
                    DateTime myStartTime = new DateTime(now.Year, now.Month, now.Day, tsStartTime.Hours, tsStartTime.Minutes, 00);
                    Console.WriteLine("{0}", myStartTime.ToString());

                    if(myShutOffTime < myStartTime)
                    {
                        myShutOffTime = myShutOffTime.AddHours(23);
                        myShutOffTime = myShutOffTime.AddMinutes(59);
                    }
                    
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
            LogWriter.LogWrite("Activity Finished Screen: btnStartNextActivity_Click");

            appSettings.bSkipCurrentActivityCantDoItToday = false;//should reset it here for next activity
            LogWriter.LogWrite("appSettings.bSkipCurrentActivityCantDoItToday = false;");

            if (appSettings.bShowActivitiesFromMid)
            {
                appSettings.bShowActivitiesFromMid = false; //This is the last point we need it so we should reset it to be able to post completed API further.
            }

            if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
            {
                
                if (bBreakActivitiesFinished)
                {
                    _parentWindow.PageHabits.HideFrameAndClose();
                    return;
                }

                //Once we are done with the activitychoice with all data we need to load graph, we will load thenext temp one
                SwapTempChoiceData();

                if (bExcludedActivityIndex >= 0) // Last activity of Include arr have been done, show details of exclude array
                {
                    appSettings.isMandatoryActivityGoing = false;
                    appSettings.nHabitIndex = bExcludedActivityIndex;
                    LogWriter.LogWrite("nHabitIndex = " + appSettings.nHabitIndex);

                }
                else
                {
                    appSettings.nHabitIndex++; //Increment the index to do the next habit
                    LogWriter.LogWrite("nHabitIndex = " + appSettings.nHabitIndex);

                }
                _parentWindow.PageHabits.ShowActivityStartScreen();
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
            {
                if (appSettings.arrAllMorningHabits.Count == (appSettings.nHabitIndex + 1)) //Last Activity
                {
                    _parentWindow.PageHabits.HideFrameAndClose();
                }
                else
                {
                    appSettings.nHabitIndex++; //Increment the index to do the next habit
                    LogWriter.LogWrite("nHabitIndex = " + appSettings.nHabitIndex.ToString());
                    _parentWindow.PageHabits.ShowActivityStartScreen();
                }
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
            {
                if (appSettings.arrAllEveningHabits.Count == (appSettings.nHabitIndex + 1)) //Last Activity
                {
                    _parentWindow.PageHabits.HideFrameAndClose();
                }
                else
                {
                    appSettings.nHabitIndex++; //Increment the index to do the next habit
                    LogWriter.LogWrite("nHabitIndex = " + appSettings.nHabitIndex.ToString());
                    _parentWindow.PageHabits.ShowActivityStartScreen();
                }
            }
            else
            {
                //edge Case when few habits are done on windows and then rest are finished on the mobile, the action button on last habit does not do anything.
                //take to home screen
                ((App)Application.Current).wndFocusBearMainWindow.HandleLeafSelection(LeftButtonType.Habits);
            }

            LogWriter.LogWrite("HabitIndex now = " + appSettings.nHabitIndex.ToString());
        }

        private void btnSkipThisHabit_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Activity Finished Screen btnSkipThisHabit_Clicked ");

            appSettings.bSkipWhileActivityOngoing = false; //if the last activity was skipped when ongoing, reset it

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
                            return;
                        }

                    }
                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    if (appSettings.bPasswordRequireInSkipEvening || (appSettings.DidCutOffTimePassed() && appSettings.bPasswordRequireToSkipEveningAfterCutOff) )
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
                            return;
                        }
                    }
                }

            }
            ///

            webView.Visibility = Visibility.Collapsed; /*When using Webview2 in WPF, unable to overlay WPF controls on the Webview - https://github.com/MicrosoftEdge/WebView2Feedback/issues/286*/
            _parentWindow.PageHabits.ShowWhySkipHabitsWindow();
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

        private void btnChooseAnotherHabit_Click(object sender, RoutedEventArgs e)
        {
            webView.Visibility = Visibility.Collapsed;
            webView1.Visibility = Visibility.Collapsed;
            bool bStartHabitNow = _parentWindow.PageHabits.ShowChooseAnotherHabitsWindow();
            webView.Visibility = Visibility.Visible;
            webView1.Visibility = Visibility.Visible;
            if (bStartHabitNow)
            {
                ButtonAutomationPeer peer = new ButtonAutomationPeer(btnStartNextActivity);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }
        }
    }
}
