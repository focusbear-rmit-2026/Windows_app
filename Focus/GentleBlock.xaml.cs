using Focus_Bear.API;
using Focus_Bear.Utils;
using Newtonsoft.Json.Linq;
using Sentry.Protocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Web.ModelBinding;
using System.Web.UI.WebControls;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Focus_Bear.Focus
{
    /// <summary>
    /// Interaction logic for GentleBlock.xaml
    /// </summary>
    public partial class GentleBlock : Window
    {
        public class TaskItem
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }

        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        int nTimer = 30; //30 Seconds
        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [DllImport("user32.dll")]
        private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

        private const uint MF_BYCOMMAND = 0x00000000;
        private const uint MF_GRAYED = 0x00000001;
        private const uint SC_CLOSE = 0xF060;
        private const int WM_SHOWWINDOW = 0x00000018;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hWnd = new WindowInteropHelper(this);
            var sysMenu = GetSystemMenu(hWnd.Handle, false);
            EnableMenuItem(sysMenu, SC_CLOSE, MF_BYCOMMAND | MF_GRAYED);
            var source = (HwndSource)PresentationSource.FromVisual(this);
            app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");

        }

        private string sFocusModeName = "";
        public string sActiveAppName = "";
        public string sActiveURL = "";
        public string sFullURLForBlockedPage = "";
        public string sCurrentTabTitle = "";
        IntPtr HWND = IntPtr.Zero;

        public string CurrentURLBeingProcessedWithChatGPTAPI = "";

        ConvinceAIFailed wndConvinceAiFailed = null;
        TemporaryAccess wndTemporaryAccess = null;
        AddFocusGoalAndTask wndAddGoal = null;
        ConvinceAI wndConvince = null;

        public GentleBlock()
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

            // Manually alter window height and width
            this.SizeToContent = SizeToContent.Manual;

            // Automatically resize height relative to content
            this.SizeToContent = SizeToContent.Height;

            ShowInTaskbar = false;
            Topmost = true;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            TitleBarThemeChange();
            App.ThemeChanged += TitleBarThemeChange;
        }

        public void ShowGentleBlockWindowAndStartTimer(IntPtr hWnd, string sFocusModeTitle, string sAppName, string sURL = "", string sIntention = "", string sTabTitle = "")
        {
            sFocusModeName = sFocusModeTitle;
            sActiveAppName = sAppName;
            sFullURLForBlockedPage = sURL;
            sActiveURL = sURL;
            sCurrentTabTitle = sTabTitle;
            HWND = hWnd;
            if (appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.Height = 600;
                MainGrid.RowDefinitions[1].Height = new GridLength(180);
            }
            Heading.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["titleChangedFocus"].ToString();
            WantToChangeCurrentTask.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["changeTaskText"].ToString();
            MarkPreviousTaskCompleteText.Text = appSettings.ConfigFileStrings["popUpAddNewFocusGoalVcInfo"]["buttonMarkCurrentTaskAsCompleted"].ToString();

            btnJustThisTimeText.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["buttonAllowText"].ToString();
            TextBlockThisPage.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["buttonBlockUrlText"].ToString();

            TextReportBadAIDecision.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["reportBadAiTooltip"].ToString();
            btnSetTaskText.Text = appSettings.ConfigFileStrings["popUpFocusGoalConfirmationVcInfo"]["buttonSetTask"].ToString();

            bool bAICheckRequired = false;
            if (sAppName.Length > 0)
            {
                ReportBadAIDecision.Visibility = Visibility.Collapsed;
                ////AllowThisDomain.Visibility = Visibility.Collapsed;
               ///// AllowThisDomainText.Visibility = Visibility.Collapsed;

                ////sHeading = sHeading.Replace("<URL>", sAppName + ".exe");
                appSettings.ShowBlurWindow(HWND);

                this.Show(); //Will show this window for apps
            }
            else
            {
                ///////////////////////////////////////////////////////////////////
                ///
                if (appSettings.bAiCheckUrlEnabled)
                {
                    ReportBadAIDecision.Visibility = Visibility.Visible;
                    bAICheckRequired = true;
                }
                else
                {
                    ReportBadAIDecision.Visibility = Visibility.Collapsed;
                    bAICheckRequired = false;
                }

                if(appSettings.isBlockingScheduleRunning && appSettings.ActiveSchedule != null)
                {
                    if(appSettings.ActiveSchedule.bAIBlockingEnabled)
                    {
                        ReportBadAIDecision.Visibility = Visibility.Visible;
                        bAICheckRequired = true;
                    }
                    else
                    {
                        ReportBadAIDecision.Visibility = Visibility.Collapsed;
                        bAICheckRequired = false;
                    }
                }

                
                string sHost = "";
                try
                {
                    Uri myUri = new Uri(sActiveURL);
                    sHost = myUri.Host; //fails if there is no http://
                }
                catch (Exception ex)
                {
                    /*
                    This constructor initializes a new instance of the UriBuilder class with the Fragment, Host, Path, Port, Query, Scheme, and Uri properties set as specified in uri.
                    If uri does not specify a scheme, the scheme defaults to "http:".
                    */
                    // Ensure it has a scheme, otherwise UriBuilder will throw
                    Uri myUri = new UriBuilder(sActiveURL).Uri;
                    sHost = myUri.Host;

                    Debug.WriteLine(ex.Message);
                }

                string sDomain = appSettings.ConfigFileStrings["common"]["allowAllUrlsFromDomainText"].ToString();
                sDomain = sDomain.Replace("<DOMAIN>", sHost);

                ////////////////////////////////////////////////////

                ////sHeading = sHeading.Replace("<URL>", sActiveURL.Length > 108 ? sActiveURL.Substring(0, 108) : sActiveURL);
                ///
                //Close all existing popups if any which is handled by Stop timer function
                StopTimer();

                appSettings.HideBlurWindow();
                this.Hide(); //Will keep this window hidden will we get response from API
            }

            if (appSettings.sAllTasks.Length > 0)
            {
                //File the tasks combo box
                JObject obj = JObject.Parse(appSettings.sAllTasks);

                JArray tasks = (JArray)obj["data"];

                var taskList = tasks
                    .Select(t => new TaskItem
                    {
                        Id = t["id"]?.ToString(),
                        Name = t["title"]?.ToString()
                    })
                    .ToList();

                ComboBoxTasks.ItemsSource = taskList;
                ComboBoxTasks.DisplayMemberPath = "Name";
            }
            /** if (appSettings.isBlockingScheduleRunning && !appSettings.ActiveSchedule.isPaused) // Blocking schedule
             {
                 sHeading = sHeading.Replace("<FOCUSTYPE>", appSettings.ActiveSchedule.name);
             }
             else
             {
                 sHeading = sHeading.Replace("<FOCUSTYPE>", sFocusModeTitle);
             }

             //sHeading = sHeading.Length > 108 ? sHeading.Substring(0, 108) : sHeading; //If there are more characters, take only first 100 approx
             Heading.Text = sHeading;

             if (sIntention.Length > 0) //Set the activity title if thelength is > 0
             {
                 string sInt = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["intentionText"].ToString();
                 sInt = sInt.Replace("<INTENTION>",sIntention);
                 Intention.Text = sInt;
                 Intention.Visibility = Visibility.Visible;
             }
             else
             {
                 Intention.Visibility = Visibility.Collapsed;
             }**/
            dispatcherTimer.Stop();
            //Remove the previous event handler so that new can be set
            try
            {
                var eventField = dispatcherTimer.GetType().GetField("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
                var eventDelegate = (Delegate)eventField.GetValue(dispatcherTimer);
                if (eventDelegate != null)
                {
                    var invocatationList = eventDelegate.GetInvocationList();

                    foreach (var handler in invocatationList)
                        dispatcherTimer.Tick -= ((EventHandler)handler);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowGentleBlockWindowAndStartTimer - " + ex.Message);
            }
            nTimer = 30;
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);

            if (sURL.Length > 0 && bAICheckRequired) //check settings from AI tab
            {
                CheckWithChatGPT();
            }
            else
            {
                string sHeading = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["title"].ToString();
                if (sAppName.Length > 0)
                {
                    sHeading = sHeading.Replace("<URL>", sActiveURL.Length > 108 ? sActiveURL.Substring(0, 108) : sAppName);
                    TextBlockThisPage.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["buttonBlockAppText"].ToString();
                }
                else
                {
                    sHeading = sHeading.Replace("<URL>", sActiveURL.Length > 108 ? sActiveURL.Substring(0, 108) : sActiveURL);
                    TextBlockThisPage.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["buttonBlockUrlText"].ToString();
                }

                if (appSettings.isBlockingScheduleRunning && !appSettings.ActiveSchedule.isPaused) // Blocking schedule
                {
                    sHeading = sHeading.Replace("<FOCUSTYPE>", appSettings.ActiveSchedule.name);
                }
                else
                {
                    sHeading = sHeading.Replace("<FOCUSTYPE>", sFocusModeName);
                }

                Heading.Text = sHeading;

                string sDescription = "";
                if(appSettings.sRunningFocusModeIntention.Length > 0)
                {
                    string sIntent = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["intentionText"].ToString();
                    sIntent = sIntent.Replace("<INTENTION>", appSettings.sRunningFocusModeIntention);
                    sDescription = sIntent;
                }
                
                if (appSettings.sCurrentTaskPlayingInToDoPlayer.Length > 0)
                {
                    string sTask = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["currentTaskText"].ToString();
                    sTask = sTask.Replace("<TASK>", appSettings.sCurrentTaskPlayingInToDoPlayer);
                    sDescription = sDescription + "\n\n" + sTask;
                }
                WorkingOnTask.Text = sDescription;

                WantToChangeCurrentTask.Visibility = Visibility.Collapsed;
                MarkPreviousTaskComplete.Visibility = Visibility.Collapsed;
                ReportBadAIDecision.Visibility = Visibility.Collapsed;
                ComboxBoxSection.Visibility = Visibility.Collapsed;
                ScoreSection.Visibility = Visibility.Collapsed;
                btnNoIamWorkingOnSomethingElse.Visibility = Visibility.Collapsed;

                CommandManager.InvalidateRequerySuggested();


                appSettings.ShowBlurWindow(HWND);
                this.Show(); //Will show this window 
                dispatcherTimer.Start();
                CommandManager.InvalidateRequerySuggested();
            }
        }
         
        private async void CheckWithChatGPT()
        {
            //Here first we need to call the chat GPT API and decide per that to show the Gentle block window or not
            ///
            if (CurrentURLBeingProcessedWithChatGPTAPI == sActiveURL)
                return;
            CurrentURLBeingProcessedWithChatGPTAPI = sActiveURL;

            if(appSettings.sConvincingAIForURL != sActiveURL)
            {
                appSettings.sExtraJustificationForThisSite = ""; //empty the extra justification if the url is changed.
                appSettings.sConvincingAIForURL = ""; //reset
            }

            await Task.Run(async () =>
            {
                try
                {
                    //Show thinking icon just before the API call
                    await Dispatcher.BeginInvoke((Action)(() =>
                    {
                        if (((App)(Application.Current)).wndFocusProgress != null)
                        {
                            string imagePath = ("pack://application:,,,/Focus Bear;component/images/thinking_icon.png");
                            var image = new BitmapImage();
                            image.BeginInit();
                            image.UriSource = new Uri(imagePath);
                            image.EndInit();
                            ((App)(Application.Current)).wndFocusProgress.TopIcon.Source = image;
                        }
                    }));


                    FocusBearAPIClient client = new FocusBearAPIClient();
                    var nResponse = await client.ChatGPTIsURLSafe(sActiveURL, sCurrentTabTitle, "", appSettings.sRunningFocusModeName, appSettings.sRunningFocusModeIntention, appSettings.sLangCode);
                    double xProbability = double.Parse(nResponse.ToString());

                    if (CurrentURLBeingProcessedWithChatGPTAPI != sActiveURL) //If user have moved to another url, do not do anything
                        return;

                    await Task.Delay(20);

                    //Write to focus ai decisions csv
                    string sHost = "";
                    try
                    {
                        Uri myUri = new Uri(sActiveURL);
                        sHost = myUri.Host; //fails if there is no http://
                    }
                    catch (Exception ex)
                    {
                        /*
                        This constructor initializes a new instance of the UriBuilder class with the Fragment, Host, Path, Port, Query, Scheme, and Uri properties set as specified in uri.
                        If uri does not specify a scheme, the scheme defaults to "http:".
                        */
                        // Ensure it has a scheme, otherwise UriBuilder will throw
                        Uri myUri = new UriBuilder(sActiveURL).Uri;
                        sHost = myUri.Host;

                        Debug.WriteLine(ex.Message);
                    }

                    try
                    {
                        var currentDate = DateTime.UtcNow;

                        string isoString = currentDate.ToString("o", CultureInfo.InvariantCulture); // ISO 8601

                        //"Timestamp,Url,Domain,TabTitle,AIRelevanceScore,AdditionalUserJustification,currentTaskName,focusIntention,AIReason”
                        string csvLine = $"{isoString},\"{sActiveURL}\",{sHost},\"{sCurrentTabTitle}\",{xProbability}, {""}, {""}, \"{appSettings.sRunningFocusModeIntention}\", \"{appSettings.sChatGPTReasonForCurrentURL}\" ";
                        appSettings.csvfocusAIDecisions.Append(csvLine);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("csvfocusAIDecisions write error: " + ex.Message);
                    }

                    string sHeading = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["title"].ToString();

                    if (xProbability == -1)
                    {
                        CurrentURLBeingProcessedWithChatGPTAPI = "";
                        await Dispatcher.BeginInvoke((Action)(() =>
                        {
                            //API failed. Nothing to do.
                            ReportBadAIDecision.Visibility = Visibility.Collapsed;
                            sHeading = sHeading + "\n\n" + "The status of this url could not be verified.";
                            sHeading = sHeading.Replace("<URL>", sActiveURL.Length > 108 ? sActiveURL.Substring(0, 108) : sActiveURL);

                            if (appSettings.isBlockingScheduleRunning && !appSettings.ActiveSchedule.isPaused) // Blocking schedule
                            {
                                sHeading = sHeading.Replace("<FOCUSTYPE>", appSettings.ActiveSchedule.name);
                            }
                            else
                            {
                                sHeading = sHeading.Replace("<FOCUSTYPE>", sFocusModeName);
                            }

                            Heading.Text = sHeading;
                            
                            CommandManager.InvalidateRequerySuggested();
                            
                            if (appSettings.sLastActiveURLInFirefox == sActiveURL)
                            {
                                dispatcherTimer.Start();
                                appSettings.ShowBlurWindow(HWND);
                                this.Show();
                            }
                        }));
                    }
                    else  //we have got the probability
                    {
                       
                        await Dispatcher.BeginInvoke((Action)(() =>
                        {
                            ReportBadAIDecision.Visibility = Visibility.Visible;
                            Console.WriteLine("*****CheckWithChatGPT****current URL:" + sFullURLForBlockedPage);
                            //Safe URL, need to allow.
                            if (!FocusAlignmentScoreManager.DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus.ContainsKey(sFullURLForBlockedPage))
                            {
                                FocusAlignmentScoreManager.DictionaryScoreAiCheckUrlsInCuddlyModeCurrentFocus[sFullURLForBlockedPage] = new Dictionary<string, object>
                                {
                                    { "probability", xProbability },
                                    { "isVisibleUrl", true },
                                    { "totalSeconds", 0 },
                                    { "startedTime", DateTime.Now },
                                    { "reason", appSettings.sChatGPTReasonForCurrentURL }
                                };

                                FocusAlignmentScoreManager.CustomToolTip = appSettings.sChatGPTReasonForCurrentURL;
                                FocusAlignmentScoreManager.SetAppIconForFocusModeScore(xProbability);

                                Console.WriteLine("*****CheckWithChatGPT****current URL tooltip:" + appSettings.sChatGPTReasonForCurrentURL);
                                Console.WriteLine("*****CheckWithChatGPT****current URL xProbability:" + xProbability.ToString());

                            }

                            double currentFocusAlignmentScore = FocusAlignmentScoreManager.CalculateFocusAlignmentScore();

                            Console.WriteLine("******currentFocusAlignmentScore: - " + currentFocusAlignmentScore.ToString());
                           
                            Task.Delay(2000);

                            if (xProbability >= 0.80)
                            {
                                /**ButtonAutomationPeer peer = new ButtonAutomationPeer(btnJustThisTime);
                                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                                invokeProv.Invoke();**/

                                appSettings.TemporaryAllowedURLsGentleBlock.Add(new DistractingURL(sActiveURL.ToLower(), true));

                                appSettings.CapturePostHog("block-popup-allow-automatically");

                                sCurrentTabTitle = sCurrentTabTitle.Replace(" - Google Chrome", "");
                                sCurrentTabTitle = sCurrentTabTitle.Replace(" — Mozilla Firefox", "");
                                sCurrentTabTitle = sCurrentTabTitle.Replace(" - Microsoft? Edge", "");
                                //Lets add it to keyword array, if its not already there
                                if (sCurrentTabTitle.Length > 0)
                                {
                                    var titleExists = appSettings.TemporaryAllowedURLsTabTitles.Where(i => i.ToLower() == sCurrentTabTitle.ToLower());
                                    if (titleExists.Count() == 0)
                                    {
                                        //add to list
                                        appSettings.TemporaryAllowedURLsTabTitles.Add(sCurrentTabTitle);
                                    }
                                }
                                dispatcherTimer.Stop();
                                appSettings.HideBlurWindow();
                                Hide();

                            }
                            else if (currentFocusAlignmentScore < 80 /*minFocusAlignmentScoreToShowDistractingPopUp*/)// Need to ASk USER if probability is < 0
                            {
                                if (appSettings.sCurrentTaskPlayingInToDoPlayer.Length == 0 && appSettings.sRunningFocusModeIntention.Length == 0)
                                {
                                    //Mean there is no task and no intention
                                    //Fire the yes button to ask for goal and intention
                                    ButtonAutomationPeer peer = new ButtonAutomationPeer(btnJustThisTime);
                                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                                    invokeProv.Invoke();

                                }
                                else if (appSettings.sConvincingAIForURL == sActiveURL)
                                {
                                    //Means AI was not convinced and we need to show a different popup to user that AI is not convinced and allow to convince again
                                    
                                    wndConvinceAiFailed = new ConvinceAIFailed(xProbability, nTimer);
                                    wndConvinceAiFailed.Owner = this;

                                    //Show the main window first
                                    appSettings.ShowBlurWindow(HWND);
                                    this.Show();
                                    dispatcherTimer.Stop();//stop the timer here as the convince failed will start it.

                                    GridShade.Visibility = Visibility.Visible;
                                    bool? bResult = wndConvinceAiFailed.ShowDialog(); //yeh beechmein hai
                                    GridShade.Visibility = Visibility.Collapsed;

                                    appSettings.sConvincingAIForURL = ""; //reset

                                    if (bResult.Value)
                                    {
                                        if (wndConvinceAiFailed.bReportBadAIDecision)
                                        {
                                            Hyperlink link = BtnReportBadAIDecision;   //  Hyperlink control
                                            var peerH = new HyperlinkAutomationPeer(link);
                                            var invokeProvH = peerH.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                                            invokeProvH?.Invoke();
                                        }
                                        else if (wndConvinceAiFailed.bShowConvincePopupAgain) //Let me convince AI again -> Yes this site is relevant
                                        {
                                            //This button click will automatically take care of firing the convince aI as task is already there
                                            ButtonAutomationPeer peer = new ButtonAutomationPeer(btnJustThisTime);
                                            IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                                            invokeProv.Invoke();
                                        }
                                        else if (wndConvinceAiFailed.bShowAskNewGoalPopup) //Let me convince AI again -> I am working on something else
                                        {
                                            ShowAskNewGoalPopup();
                                        }
                                        else if (wndConvinceAiFailed.bShowTemporaryAccessPopup)
                                        {
                                            wndTemporaryAccess = new TemporaryAccess(sActiveURL);
                                            wndTemporaryAccess.Owner = this;

                                            GridShade.Visibility = Visibility.Visible;
                                            bool? bResultTemp = wndTemporaryAccess.ShowDialog();
                                            GridShade.Visibility = Visibility.Collapsed;
                                            if (bResult.Value) //User temporarily allowed the url
                                            {
                                                dispatcherTimer.Stop();
                                                appSettings.HideBlurWindow();
                                                Hide();
                                                return;//allow the url
                                            }
                                            else //BLock this page
                                            {
                                                BlockSiteWork();
                                            }
                                            wndTemporaryAccess = null;
                                        }

                                    }
                                    else
                                    {
                                        BlockSiteWork();
                                    }
                                    wndConvinceAiFailed = null;
                                }
                                else if (appSettings.sChatGPTReasonForCurrentURL.Length > 0) //add reason from chat gpt
                                {
                                    JObject obj = JObject.Parse(appSettings.sChatGPTResponse);

                                    string suggestedTask = obj["suggested_task"]?.ToString();
                                    if(suggestedTask != null)
                                    {
                                        string suggestedTaskId = obj["suggested_task_id"]?.ToString();

                                        string sTask = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["suggestedTaskText"].ToString();
                                        WorkingOnTask.Text = sTask.Replace("<TASK>", suggestedTask);

                                        ComboBoxTasks.Text = suggestedTask; //Add the suggested task to the textview of the combo box

                                        WantToChangeCurrentTask.Visibility = Visibility.Visible;
                                        MarkPreviousTaskComplete.Visibility = Visibility.Visible;
                                        ComboxBoxSection.Visibility = Visibility.Visible;

                                        btnNoIamWorkingOnSomethingElse.Visibility = Visibility.Collapsed;
                                    }
                                    else
                                    {
                                        WorkingOnTask.Text = "";

                                        WantToChangeCurrentTask.Visibility = Visibility.Collapsed;
                                        MarkPreviousTaskComplete.Visibility = Visibility.Collapsed;
                                        ComboxBoxSection.Visibility = Visibility.Collapsed;

                                        string sHead = appSettings.ConfigFileStrings["popUpFocusGoalConfirmationVcInfo"]["titleGoal"].ToString();
                                        if(appSettings.sCurrentTaskPlayingInToDoPlayer.Length > 0)
                                        {
                                            sHead = appSettings.ConfigFileStrings["popUpFocusGoalConfirmationVcInfo"]["titleGoalAndTask"].ToString();
                                            sHead = sHead.Replace("<TASK>", appSettings.sCurrentTaskPlayingInToDoPlayer);
                                        }
                                        sHead = sHead.Replace("<GOAL>", appSettings.sRunningFocusModeIntention );
                                        Heading.Text = sHead;

                                        btnNoIamWorkingOnSomethingElse.Visibility = Visibility.Visible;
                                        btnNoIamWorkingOnSomethingElseText.Text = appSettings.ConfigFileStrings["popUpFocusGoalConfirmationVcInfo"]["buttonWorkingSomethingElseText"].ToString();
                                        
                                        btnJustThisTimeText.Text = appSettings.ConfigFileStrings["popUpFocusGoalConfirmationVcInfo"]["buttonSiteRelevantText"].ToString();
                                    }

                                    ScoreSection.Visibility = Visibility.Visible;
                                    string sScore = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["aiRelevanceScoreText"].ToString();
                                    AIRelevanceScore.Text = sScore.Replace("<SCORE>", ((int)(xProbability * 100)).ToString());

                                    InfoIcon.ToolTip = appSettings.sChatGPTReasonForCurrentURL;
                                    if (suggestedTask != null) //only if there was a suggested task
                                    {
                                        string sStillWorking = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["buttoWorkingOnOldTask"].ToString();
                                        if (appSettings.sCurrentTaskPlayingInToDoPlayer.Length > 0)
                                        {
                                            sStillWorking = sStillWorking.Replace("<TASK>", appSettings.sCurrentTaskPlayingInToDoPlayer);
                                            btnJustThisTimeText.Text = sStillWorking;
                                        }
                                        else if (appSettings.sRunningFocusModeIntention.Length > 0)
                                        {
                                            sStillWorking = sStillWorking.Replace("<TASK>", appSettings.sRunningFocusModeIntention);
                                            btnJustThisTimeText.Text = sStillWorking;
                                        }
                                    }
                                    /***
                                    sHeading = sHeading + "\n\n" + appSettings.sChatGPTReasonForCurrentURL;
                                    sHeading = sHeading.Replace("<URL>", sActiveURL.Length > 108 ? sActiveURL.Substring(0, 108) : sActiveURL);

                                    
                                    if (appSettings.isBlockingScheduleRunning && !appSettings.ActiveSchedule.isPaused) // Blocking schedule
                                    {
                                        sHeading = sHeading.Replace("<FOCUSTYPE>", appSettings.ActiveSchedule.name);
                                    }
                                    else
                                    {
                                        sHeading = sHeading.Replace("<FOCUSTYPE>", sFocusModeName);
                                    }

                                    Heading.Text = sHeading;***/

                                    CommandManager.InvalidateRequerySuggested();
                                    if (appSettings.sLastActiveURLInFirefox == sActiveURL)
                                    {
                                        dispatcherTimer.Start();
                                        appSettings.ShowBlurWindow(HWND);
                                        this.Show();
                                    }
                                }
                            }
                        }));
                        
                        return;//allow the url
                    }
                    

                   
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("attemp to ChatGPTIsURLSafe EX -" + ex.Message);
                }
            });
        }

        private void ShowAskNewGoalPopup()
        {
            dispatcherTimer.Stop();

            wndAddGoal = new AddFocusGoalAndTask(false); //true for bNoGoalNoTask
            wndAddGoal.Owner = this;

            GridShade.Visibility = Visibility.Visible;
            wndAddGoal.ShowDialog();
            GridShade.Visibility = Visibility.Collapsed;

            wndAddGoal = null;

            //Reset the same url to force check again
            CurrentURLBeingProcessedWithChatGPTAPI = "";

            //now we need to call the chatGPT safe URL api again for this domain
            appSettings.ShowGentleBlockWindow(HWND, "", sActiveURL, sCurrentTabTitle);

            dispatcherTimer.Stop();
            appSettings.HideBlurWindow();
            Hide();
        }
        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
#if DEBUG
            int val = nTimer--;
#else
            int val = nTimer--;
#endif

            try
            {

                TxtTimer.Text = appSettings.SecondsToTimerText(nTimer);

                CommandManager.InvalidateRequerySuggested();

                if (nTimer < 0)
                {
                    if (sActiveAppName.Length > 0)
                    {
                        appSettings.TemporarryBlockedAppsGentleBlock.Add(sActiveAppName.ToLower());//block the app
                    }
                    else
                    {
                        appSettings.TemporarryBlockedURLsGentleBlock.Add(sActiveURL.ToLower());//block the URL
                        ProcessBrowserURLs.suppressURLForFirefox(HWND, sFullURLForBlockedPage, "focus"); //here we will send the full URL
                    }
                    TxtTimer.Text = "00:00";
                    dispatcherTimer.Stop();
                    appSettings.HideBlurWindow();
                    Hide();
                }
                else if(nTimer == 15)
                {
                    BorderTimer.Background = new SolidColorBrush(Colors.Orange);
                }
                else if (nTimer > 15)
                {
                    BorderTimer.Background = new SolidColorBrush(Colors.Transparent);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowGentleBlockWindowAndStartTimer Window timer EX -  " + ex.Message);
            }

        }

        private void btnJustThisTime_Click(object sender, RoutedEventArgs e)
        {
            ///////////////////////////////////////////////////////////////////
            ///
            bool bAICheckRequired = false;
            if (appSettings.bAiCheckUrlEnabled)
            {
                ReportBadAIDecision.Visibility = Visibility.Visible;
                bAICheckRequired = true;
            }
            else
            {
                ReportBadAIDecision.Visibility = Visibility.Collapsed;
                bAICheckRequired = false;
            }

            if (appSettings.isBlockingScheduleRunning && appSettings.ActiveSchedule != null)
            {
                if (appSettings.ActiveSchedule.bAIBlockingEnabled)
                {
                    ReportBadAIDecision.Visibility = Visibility.Visible;
                    bAICheckRequired = true;
                }
                else
                {
                    ReportBadAIDecision.Visibility = Visibility.Collapsed;
                    bAICheckRequired = false;
                }
            }
            ///////////////////////////////////////////
            if (sActiveAppName.Length > 0)
            {
                appSettings.TemporaryAllowedAppsGentleBlock.Add( new DistractingURL(sActiveAppName.ToLower(), true));
            }
            else if(!bAICheckRequired) //No AI check
            {
                appSettings.TemporaryAllowedURLsGentleBlock.Add(new DistractingURL(sActiveURL.ToLower(), false));
            }
            else
            {
                //If the task name and intention are empty, we will ask the user the new goal and the task
                if (appSettings.sCurrentTaskPlayingInToDoPlayer.Length > 0 || appSettings.sRunningFocusModeIntention.Length > 0)
                {
                    //ask user to convinve the AI

                    dispatcherTimer.Stop();
                    appSettings.sExtraJustificationForThisSite = ""; //Reset
                    //Ask the user to convinve the AI
                    wndConvince = new ConvinceAI(nTimer); //Pass the current timer value
                    wndConvince.Owner = this;

                    GridShade.Visibility = Visibility.Visible;
                    bool? bResult = wndConvince.ShowDialog();
                    GridShade.Visibility = Visibility.Collapsed;

                    if (bResult.Value == true) //Convince clicked, get the reason
                    {
                        LogWriter.LogWrite("ConvinceAI with intention - " + wndConvince.TxtIntention.Text);
                        appSettings.sExtraJustificationForThisSite = wndConvince.TxtIntention.Text;

                        //Reset the same url to force check again
                        CurrentURLBeingProcessedWithChatGPTAPI = "";

                        appSettings.sConvincingAIForURL = sActiveURL; //store to compare when AI results come.

                        appSettings.ShowGentleBlockWindow(HWND, "", sActiveURL, sCurrentTabTitle);
                        //now we need to call the chatGPT safe URL api again for this domain
                        dispatcherTimer.Stop();
                        appSettings.HideBlurWindow();
                        Hide();
                    }
                    else
                    {
                        //Cancel clicked or timer went off
                        nTimer = wndConvince.nTimer;//Let's see how much time was spent after convince was taken off, start from there.
                        dispatcherTimer.Start();
                    }
                    wndConvince = null;
                    return;
                }
                else 
                {
                    //If the task name and intention are empty, we will ask the user the new goal and the task
                    this.Show(); //We need to show the window too

                    dispatcherTimer.Stop();
                    
                    wndAddGoal = new AddFocusGoalAndTask(true); //true for bNoGoalNoTask
                    wndAddGoal.Owner = this;

                    GridShade.Visibility = Visibility.Visible;
                    bool? bResult = wndAddGoal.ShowDialog();
                    GridShade.Visibility = Visibility.Collapsed;

                    wndAddGoal = null;

                    //Reset the same url to force check again
                    CurrentURLBeingProcessedWithChatGPTAPI = "";

                    //now we need to call the chatGPT safe URL api again for this domain
                    appSettings.ShowGentleBlockWindow(HWND, "", sActiveURL, sCurrentTabTitle);


                    dispatcherTimer.Stop();
                    appSettings.HideBlurWindow();
                    Hide();

                }

                /*** if(AllowThisDomain.IsChecked.Value)
                 {
                     string sHost = "";
                     try
                     {
                         Uri myUri = new Uri(sActiveURL);
                         sHost = myUri.Host; //fails if there is no http://
                     }
                     catch (Exception ex)
                     {
                         /*
                         This constructor initializes a new instance of the UriBuilder class with the Fragment, Host, Path, Port, Query, Scheme, and Uri properties set as specified in uri.
                         If uri does not specify a scheme, the scheme defaults to "http:".
                         * /
                         Uri myUri = new UriBuilder(sActiveURL).Uri;
                         sHost = myUri.Host;
                         Debug.WriteLine(ex.Message);
                     }

                     appSettings.TemporaryAllowedURLsGentleBlock.Add(new DistractingURL(sHost.ToLower(), true, true));
                 }
                 else
                 {
                     appSettings.TemporaryAllowedURLsGentleBlock.Add(new DistractingURL(sActiveURL.ToLower(), true));
                 }***/

            }
            dispatcherTimer.Stop();
            appSettings.HideBlurWindow();
            Hide();
        }

        private void BlockSiteWork()
        {
            if (sActiveAppName.Length > 0)
            {
                appSettings.TemporarryBlockedAppsGentleBlock.Add(sActiveAppName.ToLower());
            }
            else
            {
                appSettings.TemporarryBlockedURLsGentleBlock.Add(sActiveURL.ToLower());
                ProcessBrowserURLs.suppressURLForFirefox(HWND, sFullURLForBlockedPage, "focus"); //here we will send the full URL
            }
            dispatcherTimer.Stop();
            appSettings.HideBlurWindow();
            Hide();
        }

        private void btnGettingDistracted_Click(object sender, RoutedEventArgs e)
        {
            BlockSiteWork();
        }

        private void btnSaveForRelax_Click(object sender, RoutedEventArgs e)
        {
            BlockSiteWork();

            FocusBearAPIClient client = new FocusBearAPIClient();
            var sResponse = client.PostSavedWebsite(sFullURLForBlockedPage); 
        }

        public void StopTimer()
        {
            dispatcherTimer.Stop();

            //reset and close all popups if any is open
            wndConvinceAiFailed?.Close();
            wndTemporaryAccess?.Close();
            wndAddGoal?.Close();
            wndConvince?.Close();

            wndConvinceAiFailed = null;
            wndTemporaryAccess = null;
            wndAddGoal = null;
            wndConvince = null;
        }
        private void TitleBarThemeChange()
        {
            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
            {
                app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }
            if (appSettings.sTheme == "Light")
            {
                this.Background = new SolidColorBrush(Colors.White);
            }
            else if (appSettings.sTheme == "Dark")
            {
                this.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
            }
        }
        private void BtnReportBadAIDecision_Click(object sender, RoutedEventArgs e)
        {
            dispatcherTimer.Stop();
            appSettings.HideBlurWindow();
            Hide();

            string addItemJsonString = $"{{\"url\":\"{sActiveURL}\"," +
                   $"\"tab_title\":\"{sCurrentTabTitle}\"," +
                   $"\"meta_description\":\"\"," +
                   $"\"focus_mode\":\"{appSettings.sRunningFocusModeName}\"," +
                   $"\"intention\":\"{appSettings.sRunningFocusModeIntention}\"," +
                   $"\"language\":\"{appSettings.sLangCode}\"}}";

            //Show the Report Bad AI Desicion Feedback Window
            appSettings.ShowReportBadAIDecision(addItemJsonString, sActiveURL);
        }

        private async void btnSetTask_Click(object sender, RoutedEventArgs e)
        {
            TaskItem selectedTask = ComboBoxTasks.SelectedItem as TaskItem;

            string selectedName;
            string selectedId;

            if (selectedTask != null)
            {
                // Existing item selected
                selectedName = selectedTask.Name;
                selectedId = selectedTask.Id;

                if (appSettings.sAllTasks.Length > 0)
                {
                    JObject obj = JObject.Parse(appSettings.sAllTasks);

                    JArray tasks = (JArray)obj["data"];

                    JObject matchedTask = tasks.FirstOrDefault(t => t["id"]?.ToString() == selectedId) as JObject;

                    if (matchedTask != null)
                    {
                        // Found exact task
                        //Let's put it in the sCurrentTaskCreated and use the same in AddAndPlayTaskInCurrentList
                        appSettings.sCurrentTaskCreated = matchedTask.ToString();

                        //Add in current tasks list and play it in todoplayer
                        bool bMarkCurrentTaskAsComplete = MarkPreviousTaskComplete.IsChecked.Value;

                        await Dispatcher.BeginInvoke((Action)(() =>
                        {
                            if (((App)(Application.Current)).wndFocusBearMainWindow.PageToDoPlayer != null)
                            {
                                ((App)(Application.Current)).wndFocusBearMainWindow.PageToDoPlayer.AddAndPlayTaskInCurrentList(bMarkCurrentTaskAsComplete);
                            }
                        }));
                    }
                }

            }
            else
            {
                // User typed new value
                selectedName = ComboBoxTasks.Text;
                selectedId = null;

                if (selectedName.Length > 0)
                {
                    FocusBearAPIClient client = new FocusBearAPIClient();
                    var m = await client.AddTask(selectedName);
                    if (m == 1)
                    {
                        bool bMarkCurrentTaskAsComplete = MarkPreviousTaskComplete.IsChecked.Value;
                        //Add in current tasks list and play it in todoplayer
                        if (appSettings.sCurrentTaskCreated.Length > 0)
                        {
                            await Dispatcher.BeginInvoke((Action)(() =>
                            {
                                if (((App)(Application.Current)).wndFocusBearMainWindow.PageToDoPlayer != null)
                                {
                                    ((App)(Application.Current)).wndFocusBearMainWindow.PageToDoPlayer.AddAndPlayTaskInCurrentList(bMarkCurrentTaskAsComplete);
                                }
                            }));
                        }
                    }
                }
            }
            
        }

        private void BtnBlockThisPage_Click(object sender, RoutedEventArgs e)
        {
            BlockSiteWork();
        }

        private void ComboBoxTasks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void btnNoIamWorkingOnSomethingElse_Click(object sender, RoutedEventArgs e)
        {
            ShowAskNewGoalPopup();
        }
    }
}
