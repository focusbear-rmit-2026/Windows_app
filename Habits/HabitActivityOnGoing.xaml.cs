using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Preferences;
using Focus_Bear.Utils;
using FocusBear.Utils;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
    /// Interaction logic for HabitActivityOnGoing.xaml
    /// </summary>
    public partial class HabitActivityOnGoing : Page
    {
        /// <summary>
        /// The saved remaining seconds of a postponed habit, or null to start it from its full duration. It was read
        /// with int.Parse, so a corrupt or negative registry value threw instead.
        /// </summary>
        internal static int? SavedTimerSeconds(string saved)
        {
            if (int.TryParse((saved ?? "").Trim(), System.Globalization.NumberStyles.None,
                             System.Globalization.CultureInfo.InvariantCulture, out int seconds) && seconds >= 0)
                return seconds;
            return null;
        }

        private FocusBearMainWindow _parentWindow = null;

        AppSettings appSettings = AppSettings.GetInstance;

        int nTimer = 30;
        int nTickCount;
        public DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);
        public bool bTimerRunning;
        bool isVideoPresent;

        string sActivityIdToGetDataFromAPI = "";
        string sActivityChoiceIdToGetDataFromAPI = "";

        int nVideoUrlsCount = 0;
        List<string> videoUrlsBreaks = new List<string>();
        // Held for the lifetime of the screen so a ticked checkbox survives the ItemsControl regenerating
        // its containers (pause/resume/layout). Local-only visual state; never sent to the backend. (#343)
        private List<ChecklistItem> _checklistItems;
        // Instructions images (web_dashboard#2184), resolved (for file_path entries) after population.
        private ObservableCollection<ActivityImageDisplayItem> _instructionsImages;

        // Video / Checklist / Instructions: which content types this activity actually has, and which is
        // showing. Tabs (ChecklistVideoTabs) only appear when 2+ are present - with exactly 1, it fills
        // the content area directly with no tabs (unchanged from the original #343 checklist-only case,
        // now generalised to any single type, including Instructions-only). (#343, web_dashboard#2184)
        private enum HabitContentTab { Video, Checklist, Instructions }
        private bool _hasVideo;
        private bool _hasChecklist;
        private bool _hasInstructions;
        private HabitContentTab _activeTab = HabitContentTab.Video;
        private bool HasTabs => (new[] { _hasVideo, _hasChecklist, _hasInstructions }.Count(b => b)) >= 2;
        public HabitActivityOnGoing()
        {
            bTimerRunning = false;
            nTickCount = 0;
        }
        public HabitActivityOnGoing(FocusBearMainWindow parentWindow, bool bStartingAfterPostponed) : this()
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }


            SetWebView();

            webView.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
            webView.NavigationCompleted += CoreWebView2NavigationCompleted;

            appSettings.bSkipWhileActivityOngoing = false;

            breakActivity currentActivity = new breakActivity();

            try
            {
                if (bStartingAfterPostponed)
                {
                    LogWriter.LogWrite("Starting After Postponed in mid- Get the set timer value");
                    //Get the set timer value
                    string sTimer = RegistryManager.GetCurrentActivityPostponedTimerVal();
                    int? savedSeconds = SavedTimerSeconds(sTimer);
                    if (savedSeconds.HasValue)
                    {
                        nTimer = savedSeconds.Value;
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

                bool show_saved_distracting_websites = false;

                videoUrlsBreaks.Clear();
                appSettings.bHabitwithUserActionNonTimerRunning = false;

                btnSkipThisHabitTXT.Text = appSettings.ConfigFileStrings["activityInfo"]["buttonSkipHabitText"].ToString();
                SkipThisHabitText.Visibility = Visibility.Collapsed;
                List<string> currentChecklist = null;   // this activity's checklist steps, if any (#343)
                string currentInstructionsText = null;   // Instructions note, if any (web_dashboard#2184)
                List<ActivityImage> currentImages = null;   // Instructions images, if any (web_dashboard#2184)
                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                {
                    sActivityIdToGetDataFromAPI = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].id;
                    currentChecklist = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].check_list;
                    currentInstructionsText = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].text_instructions;
                    currentImages = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].image_urls;

                    show_saved_distracting_websites = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].show_saved_distracting_websites;

                    Heading.Text = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].name; //Activity Name
                    appSettings.sCurrentRunningActivity = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].name; //Activity Name
                    appSettings.sHabitTypeForUseFocusModeIDInActivity = "morning-habit";//evening-habit, morning-habit, break-activity

                    if (!bStartingAfterPostponed)
                        nTimer = Focus_Bear.Habits.DayPlan.DayPlanHabitTrims.TimerSecondsToday(appSettings.arrAllMorningHabits[appSettings.nHabitIndex]);   // honours a Day Plan "Shorten"

                    if (appSettings.arrAllMorningHabits[appSettings.nHabitIndex].useTimer)
                    {
                        SectionNonTimer.Visibility = Visibility.Collapsed;
                        SectionTimer.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        nTimer = appSettings.nTimerValForNonTimerActivities;
                        appSettings.bHabitwithUserActionNonTimerRunning = true;

                        SectionNonTimer.Visibility = Visibility.Visible;
                        SectionTimer.Visibility = Visibility.Collapsed;
                        string sCompleteTxt = appSettings.ConfigFileStrings["activityInfo"]["completionRequirementsText"].ToString();
                        sCompleteTxt = sCompleteTxt.Replace("<REQUIREMENTS>", appSettings.arrAllMorningHabits[appSettings.nHabitIndex].completion_requirements);
                        CompleteAfter.Text = sCompleteTxt;
                        btnIAmDoneTXT.Text = appSettings.ConfigFileStrings["activityInfo"]["buttonActivityDoneText"].ToString();
                    }

                    nAllowedApps = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].allowedApps.Count;
                    nAllowedURLs = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].allowedUrls.Count;
                    nVideoUrlsCount = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].videoUrls.Count;
                    appSettings.sUseFocusModeIDInActivity = appSettings.arrAllMorningHabits[appSettings.nHabitIndex].allowedFocusMode;

                    App.FocusButton.StartHabit(appSettings.arrAllMorningHabits[appSettings.nHabitIndex].idFakeForDevice);

                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    sActivityIdToGetDataFromAPI = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].id;
                    currentChecklist = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].check_list;
                    currentInstructionsText = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].text_instructions;
                    currentImages = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].image_urls;

                    show_saved_distracting_websites = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].show_saved_distracting_websites;

                    Heading.Text = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].name; //Activity Name
                    appSettings.sCurrentRunningActivity = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].name; //Activity Name
                    appSettings.sHabitTypeForUseFocusModeIDInActivity = "evening-habit";//evening-habit, morning-habit, break-activity

                    if (!bStartingAfterPostponed)
                        nTimer = Focus_Bear.Habits.DayPlan.DayPlanHabitTrims.TimerSecondsToday(appSettings.arrAllEveningHabits[appSettings.nHabitIndex]);   // honours a Day Plan "Shorten"

                    if (appSettings.arrAllEveningHabits[appSettings.nHabitIndex].useTimer)
                    {
                        SectionNonTimer.Visibility = Visibility.Collapsed;
                        SectionTimer.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        nTimer = appSettings.nTimerValForNonTimerActivities;
                        appSettings.bHabitwithUserActionNonTimerRunning = true;

                        SectionNonTimer.Visibility = Visibility.Visible;
                        SectionTimer.Visibility = Visibility.Collapsed;
                        string sCompleteTxt = appSettings.ConfigFileStrings["activityInfo"]["completionRequirementsText"].ToString();
                        sCompleteTxt = sCompleteTxt.Replace("<REQUIREMENTS>", appSettings.arrAllEveningHabits[appSettings.nHabitIndex].completion_requirements);
                        CompleteAfter.Text = sCompleteTxt;
                        btnIAmDoneTXT.Text = appSettings.ConfigFileStrings["activityInfo"]["buttonActivityDoneText"].ToString();
                    }

                    nAllowedApps = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].allowedApps.Count;
                    nAllowedURLs = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].allowedUrls.Count;
                    nVideoUrlsCount = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].videoUrls.Count;
                    appSettings.sUseFocusModeIDInActivity = appSettings.arrAllEveningHabits[appSettings.nHabitIndex].allowedFocusMode;

                    App.FocusButton.StartHabit(appSettings.arrAllEveningHabits[appSettings.nHabitIndex].idFakeForDevice);
                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                {

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
                        currentChecklist = currentActivity.check_list;
                        currentInstructionsText = currentActivity.text_instructions;
                        currentImages = currentActivity.image_urls;
                        Heading.Text = currentActivity.name;
                        appSettings.sCurrentRunningActivity = currentActivity.name;
                        appSettings.sHabitTypeForUseFocusModeIDInActivity = "break-activity";//evening-habit, morning-habit, break-activity


                        nTimer = currentActivity.durationSeconds;
                        nAllowedApps = currentActivity.allowedApps.Count;
                        nAllowedURLs = currentActivity.allowedUrls.Count;
                        nVideoUrlsCount = currentActivity.videoUrls.Count;
                        videoUrlsBreaks = currentActivity.videoUrls;
                        appSettings.sUseFocusModeIDInActivity = currentActivity.allowedFocusMode;

                        if (currentActivity.useTimer)
                        {
                            SectionNonTimer.Visibility = Visibility.Collapsed;
                            SectionTimer.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            nTimer = appSettings.nTimerValForNonTimerActivities;
                            appSettings.bHabitwithUserActionNonTimerRunning = true;

                            SectionNonTimer.Visibility = Visibility.Visible;
                            SectionTimer.Visibility = Visibility.Collapsed;
                            string sCompleteTxt = appSettings.ConfigFileStrings["activityInfo"]["completionRequirementsText"].ToString();
                            sCompleteTxt = sCompleteTxt.Replace("<REQUIREMENTS>", currentActivity.completion_requirements);
                            CompleteAfter.Text = sCompleteTxt;
                            btnIAmDoneTXT.Text = appSettings.ConfigFileStrings["activityInfo"]["buttonActivityDoneText"].ToString();
                        }

                    }
                    else
                    {
                        //random choice
                        var randomChoice = appSettings.currentActivityChoice;
                        if (randomChoice.id != null)
                        {
                            sActivityChoiceIdToGetDataFromAPI = randomChoice.id;
                            currentChecklist = randomChoice.check_list;
                            currentInstructionsText = randomChoice.text_instructions;
                            currentImages = randomChoice.image_urls;

                            Heading.Text = randomChoice.name;
                            appSettings.sCurrentRunningActivity = currentActivity.name;
                            appSettings.sHabitTypeForUseFocusModeIDInActivity = "break-activity";//evening-habit, morning-habit, break-activity

                            nVideoUrlsCount = randomChoice.videoUrls.Count;
                            videoUrlsBreaks = randomChoice.videoUrls;

                            nTimer = currentActivity.durationSeconds;
                            nAllowedApps = currentActivity.allowedApps.Count;
                            nAllowedURLs = currentActivity.allowedUrls.Count;
                            appSettings.sUseFocusModeIDInActivity = currentActivity.allowedFocusMode;

                            if (currentActivity.useTimer) //still using the current activity and not the choice
                            {
                                SectionNonTimer.Visibility = Visibility.Collapsed;
                                SectionTimer.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                nTimer = appSettings.nTimerValForNonTimerActivities;
                                appSettings.bHabitwithUserActionNonTimerRunning = true;

                                SectionNonTimer.Visibility = Visibility.Visible;
                                SectionTimer.Visibility = Visibility.Collapsed;
                                string sCompleteTxt = appSettings.ConfigFileStrings["activityInfo"]["completionRequirementsText"].ToString();
                                sCompleteTxt = sCompleteTxt.Replace("<REQUIREMENTS>", currentActivity.completion_requirements);
                                CompleteAfter.Text = sCompleteTxt;
                                btnIAmDoneTXT.Text = appSettings.ConfigFileStrings["activityInfo"]["buttonActivityDoneText"].ToString();
                            }

                        }
                    }
                }


                // Checklist activity (#343): completion stays manual via "I'm done" (parsed as non-timer),
                // matching mobile — ticking is a local visual aid, not a gate. Held in _checklistItems so
                // ticks survive re-renders.
                _hasVideo = nVideoUrlsCount > 0;
                _hasChecklist = currentChecklist != null && currentChecklist.Count > 0;
                _hasInstructions = !string.IsNullOrEmpty(currentInstructionsText) || (currentImages != null && currentImages.Count > 0);

                if (_hasChecklist)
                {
                    _checklistItems = new List<ChecklistItem>();
                    foreach (string step in currentChecklist)
                        _checklistItems.Add(new ChecklistItem { Text = step, IsChecked = false });
                    ChecklistItemsControl.ItemsSource = _checklistItems;
                    // The checklist is the completion guide, so drop the single-line "Complete after: …"
                    // text; the "I'm done" button below stays.
                    CompleteAfter.Visibility = Visibility.Collapsed;
                }

                if (_hasInstructions)
                {
                    PopulateInstructions(currentInstructionsText, currentImages);
                }

                // Video / Checklist / Instructions tabs (#343, web_dashboard#2184): shown only when 2+ of
                // these are present, so the user can switch between them (macOS parity). With exactly one
                // present, it fills the content area directly with no tabs. Default priority when tabs
                // show: Video (it autoplays via the WebView init flow, and still LOADS even while hidden
                // so switching to it later isn't blank), then Checklist, then Instructions.
                if (HasTabs)
                {
                    btnTabVideo.Visibility = _hasVideo ? Visibility.Visible : Visibility.Collapsed;
                    btnTabChecklist.Visibility = _hasChecklist ? Visibility.Visible : Visibility.Collapsed;
                    btnTabInstructions.Visibility = _hasInstructions ? Visibility.Visible : Visibility.Collapsed;
                    try
                    {
                        btnTabVideoTXT.Text = appSettings.ConfigFileStrings["activityInfo"]["buttonVideoText"].ToString();
                        btnTabChecklistTXT.Text = appSettings.ConfigFileStrings["activityInfo"]["titleCheckList"].ToString();
                        btnTabInstructionsTXT.Text = appSettings.ConfigFileStrings["activityInfo"]["buttonInstructionsText"].ToString();
                    }
                    catch (Exception exTab) { LogWriter.LogWrite("checklist tab labels - " + exTab.Message); }
                    ChecklistVideoTabs.Visibility = Visibility.Visible;
                }
                else
                {
                    ChecklistVideoTabs.Visibility = Visibility.Collapsed;
                }
                _activeTab = _hasVideo ? HabitContentTab.Video : (_hasChecklist ? HabitContentTab.Checklist : HabitContentTab.Instructions);
                ApplyActiveTabVisibility();

                TxtTimer.Text = appSettings.SecondsToTimerText(nTimer);

                if (show_saved_distracting_websites)
                {
                    _parentWindow.Hide();
                    //here we also need to remove the secondary screen windows.
                    appSettings.RemoveSecondaryWindows();

                    ((App)(Application.Current)).ShowRelaxActivityWindow(nTimer);
                }
                else if (nAllowedApps > 0 || nAllowedURLs > 0 || appSettings.sUseFocusModeIDInActivity.Length > 0)
                {
                    appSettings.bHabitwithAllowedAppsRunning = true;
                    //_parentWindow.WindowState = WindowState.Minimized;

                    _parentWindow.Hide();
                    // _parentWindow.ShowInTaskbar = false;

                    //here we also need to remove the secondary screen windows.
                    appSettings.RemoveSecondaryWindows();

                    if (appSettings.sUseFocusModeIDInActivity.Length > 0)//means there is a focus mode in activity
                    {
                        appSettings.bHabitwithUseFocusModeRunning = true;
                    }
                    //we will need to show the break window with the duration and the timer
                    //Show the Break Window
                    //If its a non timer (86400) activity, pass the completre after text, else pass the heading text (activity name)
                    ((App)(Application.Current)).ShowBreakWindowForAllowedAppsActivity(nTimer, (appSettings.bHabitwithUserActionNonTimerRunning) ? CompleteAfter.Text : Heading.Text);
                }

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("HabitActivityOnGoing EX - " + ex.Message);
            }

            _parentWindow.PageHabits.ActivityStartTime = DateTime.UtcNow;

            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            bTimerRunning = true;
            pause.Visibility = Visibility.Visible;
            play.Visibility = Visibility.Collapsed;

            //moving to navigation completed
            //Start only if video is not present
            if (!isVideoPresent)
            {
                dispatcherTimer.Start();
                CommandManager.InvalidateRequerySuggested();
            }

            Global.DisableSystemSleep();

            try
            {
                //Also get the activity data from server as soon as we start the timer as these will be used to show graph on finish screen
                FocusBearAPIClient client = new FocusBearAPIClient();

                var sResponse = client.GetActivityStats(sActivityIdToGetDataFromAPI, sActivityChoiceIdToGetDataFromAPI);

                if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
                {
                    if (!currentActivity.isRandomChoice)
                    {
                        if (currentActivity.logQuantity)
                        {
                            //call the apI to get the log quantity questions data
                            var sResponseActivityCurrentDayQuestionsStats = client.GetActivityCurrentDayQuestionsStats(sActivityIdToGetDataFromAPI, sActivityChoiceIdToGetDataFromAPI, currentActivity.logQuantityQuestions);
                        }
                        else
                        {
                            var sResponse1 = client.GetActivityCurrentDayStats(sActivityIdToGetDataFromAPI, sActivityChoiceIdToGetDataFromAPI);

                        }
                    }
                    else
                    {
                        var randomChoice = appSettings.currentActivityChoice;
                        if (randomChoice.id != null)
                        {
                            if (randomChoice.logQuantity)
                            {
                                //call the apI to get the log quantity questions data
                                var sResponseActivityCurrentDayQuestionsStats = client.GetActivityCurrentDayQuestionsStats(sActivityIdToGetDataFromAPI, sActivityChoiceIdToGetDataFromAPI, randomChoice.logQuantityQuestions);

                            }
                            else
                            {
                                var sResponse1 = client.GetActivityCurrentDayStats(sActivityIdToGetDataFromAPI, sActivityChoiceIdToGetDataFromAPI);

                            }
                        }
                    }


                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
            }
        }
        private async Task SetWebView()
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
                LogWriter.LogWrite("Activity ongoing SetWebView() EX - " + ex.Message);
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

                    // Set the Referer header for YouTube embed requests
                    webView.CoreWebView2.AddWebResourceRequestedFilter("https://www.youtube.com/embed/*", CoreWebView2WebResourceContext.Document);
                    webView.CoreWebView2.WebResourceRequested += (s, args) =>
                    {
                        args.Request.Headers.SetHeader("Referer", "https://www.focusbear.io/");
                    };

                    // WebView2 initializes asynchronously. Load the video whenever this activity has one -
                    // in tabbed mode it still loads while hidden, so switching to its tab later isn't
                    // blank; its visibility follows the active tab. (#343, web_dashboard#2184)
                    if (_hasVideo)
                    {
                        webView.Visibility = (_activeTab == HabitContentTab.Video) ? Visibility.Visible : Visibility.Collapsed;
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

                        LogWriter.LogWrite("HabitActivityOnGoing attempt to load video URL - " + videoUrl);

                        if (videoUrl.Contains("&")) // Fix for URLS like https://www.youtube.com/watch?v=vNeRn90QjyA&pp=ygUQbWVkaXRhdGlvbiBtdXNpYw%3D%3D 
                        {
                            videoUrl = videoUrl.Substring(0, videoUrl.IndexOf("&", 1));
                        }

                        if (videoUrl.Contains("watch?v="))
                        {
                            videoUrl = videoUrl.Replace("watch?v=", "embed/");
                        }
                        else if (videoUrl.ToLower().Contains("youtu.be/"))
                        {
                            string lastItem = videoUrl.Split('/').Last();
                            if (lastItem.Length > 0)
                            {
                                //Handle non permitted embedded videos
                                videoUrl = "https://www.youtube.com/embed/" + lastItem;
                            }
                        }

                        if (videoUrl.Contains("?"))
                        {
                            videoUrl = videoUrl + "&autoplay=1&rel=0";
                        }
                        else
                        {
                            videoUrl = videoUrl + "?autoplay=1&rel=0";
                        }

                        LogWriter.LogWrite("HabitActivityOnGoing Final URL trying to video URL - " + videoUrl);

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
                    LogWriter.LogWrite("CoreWebView2InitializationCompleted()" + ex.Message);
                }
            }
        }
        private void CoreWebView2NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            //We need to remove this (if (e.IsSuccess)) as in offline the timer did not start, possibly wont start when the video is not available.
            {
                Debug.Print("NavigationId: " + e.NavigationId.ToString());
                Debug.Print("URL: " + this.webView.CoreWebView2.Source);
                if (!(this.webView.CoreWebView2.Source == "about:blank")) //this is set when ending the ongoing screen so avoid restarting timer here
                {
                    //we will start the timer here after youtube video has been loaded
                    dispatcherTimer.Start();
                    CommandManager.InvalidateRequerySuggested();
                }

            }
        }
            
        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
#if DEBUG
            int val = nTimer --;
#else
            int val = nTimer--;
#endif
            nTickCount++;
            if (nTickCount > 5) //Hide the top image after 5 seconds
            {
                TopIcon.Visibility = Visibility.Collapsed;
                if (isVideoPresent)
                    btnPausePlay.Margin = new Thickness(0, 10, 0, 40);
            }

            TxtTimer.Text = appSettings.SecondsToTimerText(nTimer);

            CommandManager.InvalidateRequerySuggested();

            if (nTimer < 0)
            {
                TxtTimer.Text = "00:00";
                //Stop the video too — guard in case the WebView2 core was never created (checklist
                // activity) or is already disposed, so it can't block the finish flow below. (#343)
                try
                {
                    if (webView != null && webView.CoreWebView2 != null)
                        webView.Source = new System.Uri("about:blank");
                }
                catch (Exception exBlank)
                {
                    LogWriter.LogWrite("HabitActivityOnGoing blank source - " + exBlank.Message);
                }
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

                    if (nAllowedApps > 0 || nAllowedURLs > 0 || appSettings.sUseFocusModeIDInActivity.Length > 0)
                    {
                        LogWriter.LogWrite("nAllowedApps > 0 || nAllowedURLs > 0 || appSettings.sUseFocusModeIDInActivity.Length > 0 = means there is a focus mode in activity");

                        if (appSettings.sUseFocusModeIDInActivity.Length > 0)//means there is a focus mode in activity
                        {
                            appSettings.bHabitwithUseFocusModeRunning = false;
                            appSettings.sUseFocusModeIDInActivity = "";
                        }

                        appSettings.bHabitwithAllowedAppsRunning = false;
                        
                        _parentWindow.WindowState = WindowState.Normal;
                        _parentWindow.Show();

                       ((App)(Application.Current)).ShowBlankWindowOnOtherScreens();

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

                        CommandManager.InvalidateRequerySuggested();
                    }


                    CommandManager.InvalidateRequerySuggested();
                    dispatcherTimer.Stop();
                    CommandManager.InvalidateRequerySuggested();
                    Global.EnableSystemSleep();
                    _parentWindow.PageHabits.ShowACtivityFinishedScreen();

                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("Habit Activity Ongoing dispatcherTimer_Tick EX - " + ex.Message);
                }
            }
        }
        private async void btnPausePlay_Click(object sender, RoutedEventArgs e)
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
                if (appSettings.nCurrentActivityType != CurrentActivityType.BREAK_ACTIVITY)
                {
                    SkipThisHabitText.Visibility = Visibility.Visible;//Only show in paused mode
                    // Reveal "Back to Day Plan" while paused — except on the last habit (nothing to go back for).
                    btnBackToDayPlan.Visibility = IsLastHabit() ? Visibility.Collapsed : Visibility.Visible;
                }
                await PauseVideoAsync();

                App.FocusButton.PauseHabit();

                appSettings.CapturePostHog("pause-activity");
            }
            else
            {
                bTimerRunning = true;
                play.Visibility = Visibility.Collapsed;
                pause.Visibility = Visibility.Visible;
                if (appSettings.nCurrentActivityType != CurrentActivityType.BREAK_ACTIVITY)
                {
                    SkipThisHabitText.Visibility = Visibility.Collapsed;
                    btnBackToDayPlan.Visibility = Visibility.Collapsed;   // hide when resuming
                }

                dispatcherTimer.Start();
                CommandManager.InvalidateRequerySuggested();

                Global.DisableSystemSleep();

                await ResumeVideoAsync();

                App.FocusButton.ResumeHabit();

                appSettings.CapturePostHog("resume-activity");

                if (appSettings.bHabitwithAllowedAppsRunning) //if app was an allowed app
                {
                    _parentWindow.Hide();
                   
                    //here we also need to remove the secondary screen windows.
                    appSettings.RemoveSecondaryWindows();

                    //we will need to show the break window with the duration and the timer
                    //Show the Break Window
                    //If its a non timer (86400) activity, pass the completre after text, else pass the heading text (activity name)
                    ((App)(Application.Current)).ShowBreakWindowForAllowedAppsActivity(nTimer, (appSettings.bHabitwithUserActionNonTimerRunning) ? CompleteAfter.Text : Heading.Text);
                }
            }
        }
        // Video / Checklist / Instructions tab switching (#343, web_dashboard#2184). Only wired up when
        // 2+ of these are present (ChecklistVideoTabs is otherwise collapsed, and only the tabs for
        // present types are shown). The video keeps loading while hidden, so switching to it is instant;
        // audio is paused/resumed so a hidden video doesn't keep playing.
        private void btnTabVideo_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SetActiveTab(HabitContentTab.Video);
        }

        private void btnTabChecklist_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SetActiveTab(HabitContentTab.Checklist);
        }

        private void btnTabInstructions_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SetActiveTab(HabitContentTab.Instructions);
        }

        private void SetActiveTab(HabitContentTab tab)
        {
            bool wasVideoActive = _activeTab == HabitContentTab.Video;
            _activeTab = tab;
            ApplyActiveTabVisibility();

            bool videoActiveNow = _activeTab == HabitContentTab.Video;
            // Only auto-resume on switching back to the Video tab if the habit itself isn't paused -
            // otherwise switching tabs while paused (e.g. Video -> Instructions -> Video) incorrectly
            // restarted playback regardless of the user's own pause/play state.
            if (videoActiveNow && !wasVideoActive && bTimerRunning)
                _ = ResumeVideoAsync();
            else if (!videoActiveNow && wasVideoActive)
                _ = PauseVideoAsync();
        }

        // Applies _activeTab to the tab buttons' colours and to which content section is visible.
        // Called both on initial population and on every tab switch.
        private void ApplyActiveTabVisibility()
        {
            SolidColorBrush orange = new SolidColorBrush(Color.FromRgb(0xE8, 0x88, 0x00));

            bool videoActive = _activeTab == HabitContentTab.Video;
            bool checklistActive = _activeTab == HabitContentTab.Checklist;
            bool instructionsActive = _activeTab == HabitContentTab.Instructions;

            btnTabVideo.Background = videoActive ? (Brush)orange : Brushes.Transparent;
            btnTabVideoTXT.Foreground = videoActive ? Brushes.White : orange;
            btnTabChecklist.Background = checklistActive ? (Brush)orange : Brushes.Transparent;
            btnTabChecklistTXT.Foreground = checklistActive ? Brushes.White : orange;
            btnTabInstructions.Background = instructionsActive ? (Brush)orange : Brushes.Transparent;
            btnTabInstructionsTXT.Foreground = instructionsActive ? Brushes.White : orange;

            SectionChecklist.Visibility = (_hasChecklist && checklistActive) ? Visibility.Visible : Visibility.Collapsed;
            SectionInstructions.Visibility = (_hasInstructions && instructionsActive) ? Visibility.Visible : Visibility.Collapsed;
            webView.Visibility = (_hasVideo && videoActive) ? Visibility.Visible : Visibility.Collapsed;
        }

        // Instructions section (web_dashboard#2184): a note (row 2) + image thumbnails (row 1). url-based
        // images display immediately; file_path (R2) images resolve asynchronously via
        // GetActivityImageReadUrl and update their thumbnail once resolved.
        private void PopulateInstructions(string instructionsText, List<ActivityImage> images)
        {
            TxtInstructionsNote.Text = instructionsText ?? "";
            TxtInstructionsNote.Visibility = string.IsNullOrEmpty(instructionsText) ? Visibility.Collapsed : Visibility.Visible;

            _instructionsImages = new ObservableCollection<ActivityImageDisplayItem>();
            InstructionsImagesItemsControl.ItemsSource = _instructionsImages;
            SectionInstructionsImages.Visibility = (images != null && images.Count > 0) ? Visibility.Visible : Visibility.Collapsed;
            if (images == null) return;

            string instructionsActivityId = sActivityChoiceIdToGetDataFromAPI.Length > 0 ? sActivityChoiceIdToGetDataFromAPI : sActivityIdToGetDataFromAPI;
            foreach (ActivityImage img in images)
            {
                var displayItem = new ActivityImageDisplayItem();
                _instructionsImages.Add(displayItem);
                if (!string.IsNullOrEmpty(img.Url))
                {
                    displayItem.DisplayUrl = img.Url;
                }
                else if (!string.IsNullOrEmpty(img.FilePath))
                {
                    _ = ResolveInstructionsImageAsync(instructionsActivityId, img.FilePath, displayItem);
                }
            }
        }

        private async Task ResolveInstructionsImageAsync(string activityId, string fileKey, ActivityImageDisplayItem displayItem)
        {
            try
            {
                FocusBearAPIClient client = new FocusBearAPIClient();
                string readUrl = await client.GetActivityImageReadUrl(activityId, fileKey);
                if (!string.IsNullOrEmpty(readUrl))
                {
                    displayItem.DisplayUrl = readUrl;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ResolveInstructionsImageAsync ex - " + ex.Message);
            }
        }

        private async Task PauseVideoAsync()
        {
            if (webView == null || webView.CoreWebView2 == null) return;
            try
            {
                await webView.ExecuteScriptAsync(@"
                        (function () {
                        try {

                            // Pause HTML5 videos
                            document.querySelectorAll('video').forEach(function (v) {
                                try { v.pause(); } catch (e) {}
                            });

                            // Pause YouTube player via its internal API (for direct YouTube embed loaded in WebView)
                            var ytPlayer = document.getElementById('movie_player');
                            if (ytPlayer && typeof ytPlayer.pauseVideo === 'function') {
                                try { ytPlayer.pauseVideo(); } catch (e) {}
                            }

                            // Pause YouTube in iframes (postMessage)
                            document.querySelectorAll('iframe').forEach(function (f) {
                                try {
                                    if (f.src && (f.src.includes('youtube.com') || f.src.includes('youtu.be'))) {
                                        f.contentWindow.postMessage('{""event"":""command"",""func"":""pauseVideo"",""args"":""""}', '*');
                                    }
                                } catch (e) {}
                            });

                            // Pause Vimeo
                            document.querySelectorAll('iframe').forEach(function (f) {
                                try {
                                    if (f.src && f.src.includes('vimeo.com')) {
                                        f.contentWindow.postMessage('{""method"":""pause""}', '*');
                                    }
                                } catch (e) {}
                            });

                        } catch (err) {}

                    })();");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PauseVideoAsync() EX - " + ex.Message);
            }
        }

        private async Task ResumeVideoAsync()
        {
            if (webView == null || webView.CoreWebView2 == null) return;
            try
            {
                await webView.ExecuteScriptAsync(@"
                    (function () {
                        try {

                            // Play HTML5 videos
                            document.querySelectorAll('video').forEach(function (v) {
                                try { v.play(); } catch (e) {}
                            });

                            // Resume YouTube player via its internal API (for direct YouTube embed loaded in WebView)
                            var ytPlayer = document.getElementById('movie_player');
                            if (ytPlayer && typeof ytPlayer.playVideo === 'function') {
                                try { ytPlayer.playVideo(); } catch (e) {}
                            }

                            // Play YouTube in iframes (postMessage)
                            document.querySelectorAll('iframe').forEach(function (f) {
                                try {
                                    if (f.src && (f.src.includes('youtube.com') || f.src.includes('youtu.be'))) {
                                        f.contentWindow.postMessage('{""event"":""command"",""func"":""playVideo"",""args"":""""}', '*');
                                    }
                                } catch (e) {}
                            });

                            // Play Vimeo
                            document.querySelectorAll('iframe').forEach(function (f) {
                                try {
                                    if (f.src && f.src.includes('vimeo.com')) {
                                        f.contentWindow.postMessage('{""method"":""play""}', '*');
                                    }
                                } catch (e) {}
                            });

                        } catch (err) {}

                    })();");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ResumeVideoAsync() EX - " + ex.Message);
            }
        }
        public void StopTimerOfRunningActivity()
        {
            if (bTimerRunning)
            {
                dispatcherTimer.Stop();
                Global.EnableSystemSleep();
                CommandManager.InvalidateRequerySuggested();
            }
        }
        public void HandleTimerForPostpone(bool bStop)
        {
           
            if (bStop)
            {
                LogWriter.LogWrite("HandleTimerForPostpone () bStop");

                if (bTimerRunning)
                {
                    LogWriter.LogWrite("HandleTimerForPostpone () bTimerRunning, stop it");
                    dispatcherTimer.Stop();
                    Global.EnableSystemSleep();
                    CommandManager.InvalidateRequerySuggested();
                    _ = PauseVideoAsync();
                }

                //It might be an allowed app habit, so need to remove the break window too
                 ((App)(Application.Current)).RemoveAllowedAppHabitTimerPopup();

                LogWriter.LogWrite("HandleTimerForPostpone () SetPostponedHabitData for activity - " + sActivityIdToGetDataFromAPI);
                //Here we need to set the timer and the activity id as CurrentActivityPostponedID, CurrentActivityPostponedTimerVal, CurrentActivityPostponedDT
                RegistryManager.SetPostponedHabitData(sActivityIdToGetDataFromAPI, nTimer.ToString());
            }
            else
            {
                Global.DisableSystemSleep();
                if (bTimerRunning)//If it was not paused then start it
                {
                    dispatcherTimer.Start();
                    CommandManager.InvalidateRequerySuggested();

                    _ = ResumeVideoAsync();

                    if (appSettings.bHabitwithAllowedAppsRunning) //if app was an allowed app
                    {
                        _parentWindow.Hide();

                        //here we also need to remove the secondary screen windows.
                        appSettings.RemoveSecondaryWindows();

                        //we will need to show the break window with the duration and the timer
                        //Show the Break Window
                        //If its a non timer (86400) activity, pass the completre after text, else pass the heading text (activity name)
                        ((App)(Application.Current)).ShowBreakWindowForAllowedAppsActivity(nTimer, (appSettings.bHabitwithUserActionNonTimerRunning) ? CompleteAfter.Text : Heading.Text);
                    }
                }
            }
        }

        /// <summary>Whether the running habit is the last one in its routine (no earlier habits to go back to).</summary>
        private bool IsLastHabit()
        {
            if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                return appSettings.nHabitIndex >= appSettings.arrAllMorningHabits.Count - 1;
            if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                return appSettings.nHabitIndex >= appSettings.arrAllEveningHabits.Count - 1;
            return true;   // breaks etc. — no back button
        }

        /// <summary>
        /// Back to the Day Plan from a paused habit — parks this habit (saving its remaining time so it
        /// can resume) and returns to the Day Plan. The linear routine flow is otherwise unchanged.
        /// </summary>
        private void btnBackToDayPlan_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Activity OnGoing Screen btnBackToDayPlan_Click");
            _parentWindow.PageHabits.ShowDayPlanFromPausedHabit();
        }

        private void btnSkipThisHabit_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Activity OnGOing Screen btnSkipThisHabit_Clicked ");

            ///
            if (appSettings.isPasswordSet)
            {
                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                {
                    if (appSettings.bPasswordRequireInSkipMorning)
                    {
                        //It might be possible that a webview is visible, we will need to hide that
                        webView.Visibility = Visibility.Collapsed;

                        AskPassword pwdWnd = new AskPassword();
                        pwdWnd.Owner = _parentWindow;
                        _parentWindow.GridShade.Visibility = Visibility.Visible;

                        bool? bResult = pwdWnd.ShowDialog();

                        _parentWindow.GridShade.Visibility = Visibility.Collapsed;
                        webView.Visibility = Visibility.Visible;


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

                        webView.Visibility = Visibility.Collapsed;

                        AskPassword pwdWnd = new AskPassword();
                        pwdWnd.Owner = _parentWindow;
                        _parentWindow.GridShade.Visibility = Visibility.Visible;

                        bool? bResult = pwdWnd.ShowDialog();

                        _parentWindow.GridShade.Visibility = Visibility.Collapsed;
                        webView.Visibility = Visibility.Visible;


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
            appSettings.bSkipWhileActivityOngoing = true;
            webView.Visibility = Visibility.Collapsed; /*When using Webview2 in WPF, unable to overlay WPF controls on the Webview - https://github.com/MicrosoftEdge/WebView2Feedback/issues/286*/
            _parentWindow.PageHabits.ShowWhySkipHabitsWindow();
        }

        private void btnIAmDone_Click(object sender, RoutedEventArgs e)
        {
            //Earlier I did a lot of checks to manage the timer in the non timer activities 
            //but then I set the duration to 24 hours initially and worked same as the timer activities.
            //Now we just hide the timer in case of non timer activities but it keeps running in background.
            //WE now need to set the duration seconds to actual for the grpahs and logging
            if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
            {
                habit tempHabit = appSettings.arrAllMorningHabits[appSettings.nHabitIndex];
                tempHabit.durationSeconds = appSettings.nTimerValForNonTimerActivities - nTimer;
                appSettings.arrAllMorningHabits[appSettings.nHabitIndex] = tempHabit;
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
            {
                habit tempHabit = appSettings.arrAllEveningHabits[appSettings.nHabitIndex];
                tempHabit.durationSeconds = appSettings.nTimerValForNonTimerActivities - nTimer;
                appSettings.arrAllEveningHabits[appSettings.nHabitIndex] = tempHabit;
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY)
            {
                breakActivity tempHabit;
                if (appSettings.isMandatoryActivityGoing)
                {
                    tempHabit = appSettings.arrActivitiesIncludeInEveryBreak[appSettings.nHabitIndex];
                    tempHabit.durationSeconds = appSettings.nTimerValForNonTimerActivities - nTimer;
                    appSettings.arrActivitiesIncludeInEveryBreak[appSettings.nHabitIndex] = tempHabit;
                }
                else
                {
                    tempHabit = appSettings.arrActivitiesExcludeInEveryBreak[appSettings.nHabitIndex];
                    tempHabit.durationSeconds = appSettings.nTimerValForNonTimerActivities - nTimer;
                    appSettings.arrActivitiesExcludeInEveryBreak[appSettings.nHabitIndex] = tempHabit;
                }

            }
            nTimer = 0;
            //WE need to check if the timer is not running, we will play it
            if (!bTimerRunning) //Pause mode
            {
                ButtonAutomationPeer peer = new ButtonAutomationPeer(btnPausePlay);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }
        }
        public void SetTimerToZeroToFinish()
        {
            nTimer = 0;
        }
    }
}
