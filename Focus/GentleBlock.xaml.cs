using Focus_Bear.API;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
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

        int nTimer = 15; //15 Seconds
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
                btnSaveForRelax.Visibility = Visibility.Visible;
                /**
                try
                {
                    Uri myUri = new Uri(sURL);
                    string host = myUri.Host; //fails if there is no http://
                    sActiveURL = host;
                    Debug.WriteLine(sActiveURL);
                }
                catch (Exception ex)
                {
                    /*
                    This constructor initializes a new instance of the UriBuilder class with the Fragment, Host, Path, Port, Query, Scheme, and Uri properties set as specified in uri.
                    If uri does not specify a scheme, the scheme defaults to "http:".
                    * /
                    Uri myUri = new UriBuilder(sURL).Uri;
                    sActiveURL = myUri.Host;
                    Debug.WriteLine(ex.Message);
                }**/

                if(appSettings.bAiCheckUrlEnabled)
                    sHeading = sHeading + "\n\n" + appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["checkingUrlText"].ToString();
            }
            else
            {
                btnSaveForRelax.Visibility = Visibility.Collapsed;
            }

            btnJustThisTimeText.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["buttonAllowText"].ToString();
            btnGettingDistractedText.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["buttonBlockText"].ToString();
            btnSaveForRelaxText.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["buttonBlockAndAddToRelaxModeText"].ToString();

            if (sAppName.Length > 0)
            {
                sHeading = sHeading.Replace("<URL>", sAppName + ".exe");
                this.Show(); //Will show this window for apps
            }
            else
            {
                sHeading = sHeading.Replace("<URL>", sActiveURL);
                this.Hide(); //Will keep this window hidden will we get response from API
            }
            sHeading = sHeading.Replace("<FOCUSTYPE>", sFocusModeTitle);
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
            nTimer = 15;
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);

            if (sURL.Length > 0 && appSettings.bAiCheckUrlEnabled) //check settings from AI tab
            {
                CheckWithChatGPT();
            }
            else
            {
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
                    FocusBearAPIClient client = new FocusBearAPIClient();
                    var nResponse = await client.ChatGPTIsURLSafe(sActiveURL, sCurrentTabTitle, "", appSettings.sRunningFocusModeName, appSettings.sRunningFocusModeIntention, appSettings.sLangCode);
                    double xProbability = double.Parse(nResponse.ToString());

                    await Task.Delay(20);

                    string sHeading = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["title"].ToString();

                    if (xProbability == -1)
                    {
                        //API failed. Nothing to do.
                       
                        await Dispatcher.BeginInvoke((Action)(() =>
                        {
                            sHeading = sHeading + "\n\n" + "The status of this url could not be verified.";
                            sHeading = sHeading.Replace("<URL>", sActiveURL);
                            sHeading = sHeading.Replace("<FOCUSTYPE>", sFocusModeName);
                            Heading.Text = sHeading;

                            dispatcherTimer.Start();
                            CommandManager.InvalidateRequerySuggested();
                            this.Show();
                        }));
                    }
                    else  //we have got the probability
                    {
                        await Dispatcher.BeginInvoke((Action)(() =>
                        {
                           
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
                                //if (((App)(Application.Current)).wndFocusProgress != null)
                                //{
                                //    ((App)(Application.Current)).wndFocusProgress.TopIcon.ToolTip = sHeading; //reason
                                //}
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
                                    sHeading = sHeading.Replace("<URL>", sActiveURL);
                                    sHeading = sHeading.Replace("<FOCUSTYPE>", sFocusModeName);
                                    Heading.Text = sHeading;
                                    dispatcherTimer.Start();
                                    CommandManager.InvalidateRequerySuggested();
                                    this.Show();
                                }
                            }
                
                            //if (appSettings.bAiAllowUrlShowAlertEnabled)
                            //{
                            //    string heading = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["titleUrlSafeText"].ToString();
                            //    heading = heading.Replace("<PERCENTAGE>", (appSettings.dProbablity * 100).ToString());

                            //    string sReason = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["reasonText"].ToString();
                            //    sReason = sReason.Replace("<REASON>", appSettings.sChatGPTReasonForCurrentURL);
                            //    FBMessageBox ff = new FBMessageBox(sReason, heading, MessageBoxButton.YesNo, appSettings.ConfigFileStrings["common"]["buttonOkText"].ToString(), appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["buttonBadDecisionText"].ToString(),"",true);
                            //    ff.ShowDialog();
                            //    if (ff.btnClicked == MessageBoxResult.No) //
                            //    {
                            //        //Bad Decision
                            //        string ss = string.Format("\"payload\":{0},\"response\":{1}", appSettings.sChatGPTPayLoad, appSettings.sChatGPTResponse);
                            //        appSettings.CapturePostHog("bad-decision-ai-allowed-url", ss);
                            //    }
                            //}

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
            }
            dispatcherTimer.Stop();
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
    }
}
