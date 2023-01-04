using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using FocusBear.Utils;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// Interaction logic for HabitActivityOnGoing.xaml
    /// </summary>
    public partial class HabitActivityOnGoing : Page
    {
        private HabitWindow _parentWindow = null;

        AppSettings appSettings = AppSettings.GetInstance;

        int nTimer = 30;
        int nTickCount;
        DispatcherTimer dispatcherTimer = new DispatcherTimer();
        public bool bTimerRunning;
        bool isVideoPresent;

        string sActivityIdToGetDataFromAPI = "";
        string sActivityChoiceIdToGetDataFromAPI = "";

        public HabitActivityOnGoing()
        {
            bTimerRunning = false;
            nTickCount = 0;
        }
        public HabitActivityOnGoing(HabitWindow parentWindow, bool bStartingAfterPostponed) : this()
        {
            InitializeComponent();

            SetWebView();

            _parentWindow = parentWindow;


            try
            {
                if(bStartingAfterPostponed)
                {
                    LogWriter.LogWrite("Starting After Postponed in mid- Get the set timer value");
                    //Get the set timer value
                    string sTimer = RegistryManager.GetCurrentActivityPostponedTimerVal();
                    if (sTimer != null && sTimer.Length > 0)
                    {
                        nTimer = int.Parse(sTimer);
                    }
                    else
                    {
                        bStartingAfterPostponed = false;//Let's read the original timer value and start from there
                    }
                }

                Random rnd1 = new Random();
                int nRandom1 = rnd1.Next(1, 5);
                string imagePath = string.Format("pack://application:,,,/Focus Bear;component/images/gifs/random/{0}.gif", nRandom1.ToString());
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(imagePath);
                image.EndInit();
                ImageBehavior.SetAnimatedSource(TopIcon, image);

                int nAllowedApps = 0;
                int nAllowedURLs = 0;
                int nVideoUrlsCount = 0;
                List<string> videoUrlsBreaks = new List<string>();

                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                {
                    sActivityIdToGetDataFromAPI = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].id;

                    Heading.Text = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].name; //ACtivity Name
                    if (!bStartingAfterPostponed)
                        nTimer = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].durationSeconds; //DurationSeconds;
                    nAllowedApps = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].allowedApps.Count;
                    nAllowedURLs = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].allowedUrls.Count;
                    nVideoUrlsCount = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].videoUrls.Count;
                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    sActivityIdToGetDataFromAPI = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].id;

                    Heading.Text = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].name; //ACtivity Name
                    if (!bStartingAfterPostponed) 
                        nTimer = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].durationSeconds; //DurationSeconds;
                    nAllowedApps = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].allowedApps.Count;
                    nAllowedURLs = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].allowedUrls.Count;
                    nVideoUrlsCount = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].videoUrls.Count;
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

                    sActivityIdToGetDataFromAPI = currentActivity.id;

                    if (!currentActivity.isRandomChoice)
                    {
                        Heading.Text = currentActivity.name; //ACtivity Name
                        nTimer = currentActivity.durationSeconds; //DurationSeconds;
                        nAllowedApps = currentActivity.allowedApps.Count;
                        nAllowedURLs = currentActivity.allowedUrls.Count;
                        nVideoUrlsCount = currentActivity.videoUrls.Count;
                        videoUrlsBreaks = currentActivity.videoUrls;

                    }
                    else
                    {
                        //random choice
                        var randomChoice = appSettings.currentActivityChoice;
                        if (randomChoice.id != null)
                        {
                            sActivityChoiceIdToGetDataFromAPI = randomChoice.id;

                            Heading.Text = randomChoice.name; //ACtivity Name
                            nVideoUrlsCount = randomChoice.videoUrls.Count;
                            videoUrlsBreaks = randomChoice.videoUrls;

                            nTimer = currentActivity.durationSeconds; //DurationSeconds;
                            nAllowedApps = currentActivity.allowedApps.Count;
                            nAllowedURLs = currentActivity.allowedUrls.Count;
                        }
                    }
                }


                TxtTimer.Text = appSettings.SecondsToTimerText(nTimer);


                if (nAllowedApps > 0 || nAllowedURLs > 0)
                {
                    appSettings.bHabitwithAllowedAppsRunning = true;
                    _parentWindow.WindowState = WindowState.Minimized;
                    _parentWindow.ShowInTaskbar = false;

                    //we will need to show the break window with the duration and the timer
                    //Show the Break Window
                    ((App)(Application.Current)).ShowBreakWindowForAllowedAppsActivity(nTimer, Heading.Text);
                }


                if (nVideoUrlsCount > 0)
                {
                    webView.Visibility = Visibility.Visible;
                    isVideoPresent = true;

                    Random rnd = new Random();
                    int nRandom = rnd.Next(0, nVideoUrlsCount - 1);
                    string videoUrl = "";
                    if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                    {
                        videoUrl = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].videoUrls[nRandom].ToString();
                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                    {
                        videoUrl = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].videoUrls[nRandom].ToString();
                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                    {
                        videoUrl = videoUrlsBreaks[nRandom].ToString();
                    }

                    //string videoUrl = "https://www.youtube.com/watch?v=HhMXYsk_Pvo";
                    //string videoUrl = "https://youtu.be/lExuhKRS8vo";
                    if (videoUrl.Contains("watch?v="))
                    {
                        videoUrl = videoUrl.Replace("watch?v=", "embed/");
                    }
                    else
                    {
                        if (videoUrl.ToLower().Contains("https://youtu.be/"))
                        {
                            //Handle non permitted embedded videos
                            //https://youtu.be/lExuhKRS8vo
                            string lastItem = videoUrl.Split('/').Last();
                            if (lastItem.Length > 0)
                            {
                                videoUrl = "https://www.youtube.com/embed/" + lastItem;
                            }

                        }
                    }
                    if (videoUrl.Contains("?"))
                    {
                        videoUrl = videoUrl + "&autoplay=1";
                    }
                    else
                    {
                        videoUrl = videoUrl + "?autoplay=1";
                    }

                    webView.Source = new System.Uri(videoUrl);

                    appSettings.CapturePostHog("play-youtube-video-in-activity");
                }
                else
                {
                    isVideoPresent = false;
                    webView.Source = new System.Uri("about:blank");
                    webView.Visibility = Visibility.Collapsed;
                }

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("HabitActivityOnGoing - " + ex.Message);
                Debug.WriteLine(ex.Message);
            }

            _parentWindow.ActivityStartTime = DateTime.UtcNow;

            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            bTimerRunning = true;
            pause.Visibility = Visibility.Visible;
            play.Visibility = Visibility.Collapsed;
            dispatcherTimer.Start();
            CommandManager.InvalidateRequerySuggested();
            Global.DisableSystemSleep();
            try
            {
                //Also get the activity data from server as soon as we start the timer as these will be used to show graph on finish screen
                FocusBearAPIClient client = new FocusBearAPIClient();

                var sResponse = client.GetActivityStats(sActivityIdToGetDataFromAPI, sActivityChoiceIdToGetDataFromAPI);
                // int x = int.Parse(sResponse.ToString());
                // if (x == 1){}

                if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                {
                    var sResponse1 = client.GetActivityCurrentDayStats(sActivityIdToGetDataFromAPI, sActivityChoiceIdToGetDataFromAPI);
                    //int x1 = int.Parse(sResponse.ToString());
                    // if (x1 == 1) {}
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }
        private async void SetWebView()
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
                LogWriter.LogWrite("SetWebView()" + ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }
        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
#if DEBUG
            int val = nTimer -= 20;
#else
            int val = nTimer--;
#endif
            nTickCount++;
            if (nTickCount > 5) //Hide the top image after 5 seconds
            {
                TopIcon.Visibility = Visibility.Collapsed;
                if(isVideoPresent)
                    btnPausePlay.Margin = new Thickness(0, 10, 0, 40);
            }

            TxtTimer.Text = appSettings.SecondsToTimerText(nTimer);

            CommandManager.InvalidateRequerySuggested();

            if (nTimer < 0)
            {
                TxtTimer.Text = "00:00";
                //Stop the video too
                webView.Source = new System.Uri("about:blank");
                try
                {
                    //iF THE ACTIVITY IS FOR ALLOWED APPS, ENLARGE IT BACK.
                    int nAllowedApps = 0;
                    int nAllowedURLs = 0;

                    if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                    {
                        nAllowedApps = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].allowedApps.Count;
                        nAllowedURLs = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].allowedUrls.Count;

                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                    {
                        nAllowedApps = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].allowedApps.Count;
                        nAllowedURLs = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].allowedUrls.Count;

                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                    {
            
                        if (appSettings.isMandatoryActivityGoing)
                        {
                            nAllowedApps = appSettings.arrActivitiesIncludeInEveryBreak[appSettings.nHabitIndex].allowedApps.Count;
                            nAllowedURLs = appSettings.arrActivitiesIncludeInEveryBreak[appSettings.nHabitIndex].allowedUrls.Count;
                        }
                        else
                        {
                            nAllowedApps = appSettings.arrActivitiesExcludeInEveryBreak[appSettings.nHabitIndex].allowedApps.Count;
                            nAllowedURLs = appSettings.arrActivitiesExcludeInEveryBreak[appSettings.nHabitIndex].allowedUrls.Count;
                        }

                    }

                    if (nAllowedApps > 0 || nAllowedURLs > 0)
                    {
                        appSettings.bHabitwithAllowedAppsRunning = false;
                        _parentWindow.WindowState = WindowState.Normal;
                        CommandManager.InvalidateRequerySuggested();
                    }
             
                    _parentWindow.ShowACtivityFinishedScreen();
                    CommandManager.InvalidateRequerySuggested();
                    dispatcherTimer.Stop();
                    Global.EnableSystemSleep();
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("Habit Activity Ongoing dispatcherTimer_Tick" + ex.Message);
                }
            }
        }
        private void btnPausePlay_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnPausePlay_Click");

            if (bTimerRunning)
            {
                dispatcherTimer.Stop();
                CommandManager.InvalidateRequerySuggested();

                Global.EnableSystemSleep();

                bTimerRunning = false;
                pause.Visibility = Visibility.Collapsed;
                play.Visibility = Visibility.Visible;

                appSettings.CapturePostHog("pause-activity");
            }
            else
            {
                bTimerRunning = true;
                play.Visibility = Visibility.Collapsed;
                pause.Visibility = Visibility.Visible;
                dispatcherTimer.Start();
                CommandManager.InvalidateRequerySuggested();
                
                Global.DisableSystemSleep();

                appSettings.CapturePostHog("resume-activity");
            }
            //  _parentWindow.Close();
        }

        public void HandleTimerForPostpone(bool bStop)
        {
            if (bStop)
            {
                if (bTimerRunning)
                {
                    dispatcherTimer.Stop();
                    Global.EnableSystemSleep();
                    CommandManager.InvalidateRequerySuggested();
                }

                //Here we need to set the timer and the activity id as CurrentActivityPostponedID, CurrentActivityPostponedTimerVal, CurrentActivityPostponedDT
                RegistryManager.SetPostponedHabitData(sActivityIdToGetDataFromAPI, nTimer.ToString());
            }
            else
            {
                dispatcherTimer.Start();
                Global.DisableSystemSleep();
                CommandManager.InvalidateRequerySuggested();
            }
        }

    }
}
