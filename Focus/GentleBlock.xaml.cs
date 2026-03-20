using Focus_Bear.API;
using Focus_Bear.Utils;
using Sentry.Protocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
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
            string sHeading = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["title"].ToString();

            if (sURL.Length > 0)
            {
                if (appSettings.bAiCheckUrlEnabled)
                    sHeading = sHeading + "\n\n" + appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["checkingUrlText"].ToString();
            }
            else
            {

            }

            btnJustThisTimeText.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["buttonAllowText"].ToString();
            btnGettingDistractedText.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["buttonBlockText"].ToString();

            TextReportBadAIDecision.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["reportBadAiTooltip"].ToString();

            bool bAICheckRequired = false;
            if (sAppName.Length > 0)
            {
                ReportBadAIDecision.Visibility = Visibility.Collapsed;
                AllowThisDomain.Visibility = Visibility.Collapsed;
                AllowThisDomainText.Visibility = Visibility.Collapsed;

                sHeading = sHeading.Replace("<URL>", sAppName + ".exe");
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

                AllowThisDomain.Visibility = Visibility.Visible;
                AllowThisDomainText.Visibility = Visibility.Visible;
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
                AllowThisDomainText.Text = sDomain;
                ////////////////////////////////////////////////////

                sHeading = sHeading.Replace("<URL>", sActiveURL.Length > 108 ? sActiveURL.Substring(0, 108) : sActiveURL);
                appSettings.HideBlurWindow();
                this.Hide(); //Will keep this window hidden will we get response from API
            }



            if (appSettings.isBlockingScheduleRunning && !appSettings.ActiveSchedule.isPaused) // Blocking schedule
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
            }
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

                    await Task.Delay(20);

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
                                ButtonAutomationPeer peer = new ButtonAutomationPeer(btnJustThisTime);
                                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                                invokeProv.Invoke();
                            }
                            else if (currentFocusAlignmentScore < 80 /*minFocusAlignmentScoreToShowDistractingPopUp*/)// Need to ASk USER if probability is < 0
                            {
                                if (appSettings.sChatGPTReasonForCurrentURL.Length > 0) //add reason from chat gpt
                                {
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

                                    Heading.Text = sHeading;
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
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowGentleBlockWindowAndStartTimer Window timer EX -  " + ex.Message);
            }

        }

        private void btnJustThisTime_Click(object sender, RoutedEventArgs e)
        {
            if (sActiveAppName.Length > 0)
            {
                appSettings.TemporaryAllowedAppsGentleBlock.Add( new DistractingURL(sActiveAppName.ToLower(), true));
            }
            else
            {
                if(AllowThisDomain.IsChecked.Value)
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
                        */
                        Uri myUri = new UriBuilder(sActiveURL).Uri;
                        sHost = myUri.Host;
                        Debug.WriteLine(ex.Message);
                    }

                    appSettings.TemporaryAllowedURLsGentleBlock.Add(new DistractingURL(sHost.ToLower(), true, true));
                }
                else
                {
                    appSettings.TemporaryAllowedURLsGentleBlock.Add(new DistractingURL(sActiveURL.ToLower(), true));
                }
                
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
    }
}
