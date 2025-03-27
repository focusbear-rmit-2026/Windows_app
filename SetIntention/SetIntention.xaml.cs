using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
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

namespace Focus_Bear.SetIntention
{
    /// <summary>
    /// Interaction logic for SetIntention.xaml
    /// </summary>
    public partial class SetIntention : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [DllImport("user32.dll")]
        private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

        private const uint MF_BYCOMMAND = 0x00000000;
        private const uint MF_GRAYED = 0x00000001;
        private const uint SC_CLOSE = 0xF060;
        private const int WM_SHOWWINDOW = 0x00000018;

        private const int TypingDelay = 500; // Delay in milliseconds after user stops typing
        private DateTime _lastKeyPressTime;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hWnd = new WindowInteropHelper(this);
            var sysMenu = GetSystemMenu(hWnd.Handle, false);
            EnableMenuItem(sysMenu, SC_CLOSE, MF_BYCOMMAND | MF_GRAYED);
        }
        public string sActiveURL = "";
        IntPtr HWND = IntPtr.Zero;
        public string sFullURLForBlockedPage = "";

        DispatcherTimer dispatcherTimerUnlock = new DispatcherTimer(DispatcherPriority.Normal);
        int nTimer = 5;

        int nAutoBlockTimer = 30; //30 Seconds
        DispatcherTimer dispatcherTimerAutoBlock = new DispatcherTimer(DispatcherPriority.Normal);

        public SetIntention()
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

            ShowInTaskbar = false;
            Topmost = true;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            try
            {
                string sHeading = appSettings.ConfigFileStrings["setIntentionBlockedUrlsVcInfo"]["titleDistracting"].ToString();
                sHeading = sHeading.Replace("<SITE>", sActiveURL);
                Heading.Text = sHeading;

                btnAllowTemporarilyText.Text = appSettings.ConfigFileStrings["setIntentionBlockedUrlsVcInfo"]["buttonAllowText"].ToString();
                btnBlockItText.Text = appSettings.ConfigFileStrings["setIntentionBlockedUrlsVcInfo"]["buttonBlockText"].ToString();
                DoNotAskAnymoreText.Text = appSettings.ConfigFileStrings["setIntentionBlockedUrlsVcInfo"]["buttonDontShowAgainForBlockAppText"].ToString();
                InfoIcon.ToolTip = appSettings.ConfigFileStrings["setIntentionBlockedUrlsVcInfo"]["privacyNoteText"].ToString();

                //Inner popup
                HeadingAchieve.Text = appSettings.ConfigFileStrings["setIntentionBlockedUrlsVcInfo"]["titleSetIntention"].ToString();
                btnCancelText.Text = appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString();
                SelectMinutes.Text = appSettings.ConfigFileStrings["setIntentionBlockedUrlsVcInfo"]["selectMinutesText"].ToString();
                btnSetIntentionText.Text = appSettings.ConfigFileStrings["setIntentionBlockedUrlsVcInfo"]["buttonSetIntentionText"].ToString();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetIntention text setting ex - " + ex.Message);
            }
        }

        private void btnSetIntention_Click(object sender, RoutedEventArgs e)
        {
            dispatcherTimerAutoBlock.Stop();
            dispatcherTimerUnlock.Stop();

            double nMinutesToUseSite = 0; //treat this as 30 seconds or half a minute

            appSettings.GetTotalTimeForDistractingSitesToday();//we get this is to make sure if the app is contineously running, date changes.

            try
            {
                ComboBoxItem typeItem = (ComboBoxItem)ComboBoxMinutes.SelectedItem;
                string nSelectedMinutes = typeItem.Content.ToString();
                if(nSelectedMinutes == "30s")
                {
                    nMinutesToUseSite = 0.5;
                }
                else
                {
                    nSelectedMinutes = nSelectedMinutes.Replace("m", ""); //remove m
                    nMinutesToUseSite = double.Parse(nSelectedMinutes);
                }
               
                LogWriter.LogWrite("nMinutesTouse - " + nMinutesToUseSite.ToString());

                if (nMinutesToUseSite == 0.5)
                {
                    appSettings.nTotalTimeForDistractingSitesToday += 30;
                }
                else
                {
                    appSettings.nTotalTimeForDistractingSitesToday += ((int)nMinutesToUseSite) * 60;
                }
                RegistryManager.SetTotalTimeForDistractingSites();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnSetIntention_Click getting minutes from comboxbox ex - " + ex.Message);
            }

            //we may be coming from set intetion for the second time after double confirmation, we need to check if the same url exists in the set intention array, we will need to replace that with new intention and new end time
            var allowedURLs = appSettings.TemporaryAllowedURLsWithSetIntention.Where(i => i.sURL.ToLower() == sActiveURL.ToLower());
            if (allowedURLs.Count() != 0)
            {
                //found in allowed url, remove it
                appSettings.TemporaryAllowedURLsWithSetIntention.Remove(allowedURLs.First());
            }

            appSettings.TemporaryAllowedURLsWithSetIntention.Add(new DistractingURLAllowedWithIntention(sActiveURL.ToLower(), TxtIntention.Text, DateTime.Now.AddMinutes(nMinutesToUseSite), nMinutesToUseSite.ToString(), false));

            Hide();

            //Show the Intention window at bottom Right corner of the screen
            appSettings.Show_ShowIntentionWindow(HWND, sFullURLForBlockedPage, TxtIntention.Text);
            
            appSettings.CapturePostHog("set-super-distracting-intention", string.Format("\"seconds\":{0}", nMinutesToUseSite * 60) );
        }

        private void btnBlockIt_Click(object sender, RoutedEventArgs e)
        {
            dispatcherTimerAutoBlock.Stop();
            dispatcherTimerUnlock.Stop();

            //if its in the allowed urls, remove it
            var allowedURLs = appSettings.TemporaryAllowedURLsWithSetIntention.Where(i => i.sURL.ToLower() == sActiveURL.ToLower());
            if (allowedURLs.Count() != 0)
            {
                //found in allowed url, remove it
                appSettings.TemporaryAllowedURLsWithSetIntention.Remove(allowedURLs.First());
            }

            appSettings.TemporarryBlockedURLsWithSetIntention.Add(sActiveURL.ToLower()); //add the domain only
            Hide();
            ProcessBrowserURLs.suppressURLForFirefox(HWND, sFullURLForBlockedPage, "blocked"); //here we will send the full URL
        }

        private void DoNotAskAnymore_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.bImmediatelyBlockDistractingSites = true;

            appSettings.CapturePostHog("settings-changed-immediately-block-url-enabled");
        }

        private void DoNotAskAnymore_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bImmediatelyBlockDistractingSites = false;

            appSettings.CapturePostHog("settings-changed-immediately-block-url-disabled");
        }

        public void ShowSetIntentionWindow(IntPtr hWnd, string sAppName="", string sURL = "", string sTabTitle = "", bool bShowTimerAndWarning = false)
        {
            //Here we shouild hide the Show intention box visible for any URL
            Console.WriteLine("Hide_ShowIntentionWindow from ShowSetIntentionWindow");
            appSettings.Hide_ShowIntentionWindow();

            btnSetIntention.IsEnabled = false;
            TxtIntention.Text = "";

            GridShade.Visibility = Visibility.Collapsed;
            GridAskIntentionPopup.Visibility = Visibility.Collapsed;

            SetTotalTimeSpent();

            sFullURLForBlockedPage = sURL;
            sActiveURL = sURL;
            
            HWND = hWnd;
           
            string sHeading = appSettings.ConfigFileStrings["setIntentionBlockedUrlsVcInfo"]["titleDistracting"].ToString();

            if (sURL.Length > 0)
            {    
                try
                {
                    Uri myUri = new Uri(sURL);
                    string host = myUri.Host; //fails if there is no http://
                    sActiveURL = host;
                }
                catch (Exception ex)
                {
                    /*
                    This constructor initializes a new instance of the UriBuilder class with the Fragment, Host, Path, Port, Query, Scheme, and Uri properties set as specified in uri.
                    If uri does not specify a scheme, the scheme defaults to "http:".
                    */
                    Uri myUri = new UriBuilder(sURL).Uri;
                    sActiveURL = myUri.Host;
                }
            }

            sHeading = sHeading.Replace("<SITE>", sActiveURL);
            this.Show(); //Will keep this window hidden will we get response from API
           
           
            Heading.Text = sHeading;

            if(bShowTimerAndWarning)
            {
                var allowedURLs = appSettings.TemporaryAllowedURLsWithSetIntention.Where(i => i.sURL.ToLower() == sActiveURL.ToLower());
                if (allowedURLs.Count() != 0)
                {
                    string sconfirm = appSettings.ConfigFileStrings["setIntentionBlockedUrlsVcInfo"]["confirmUseAgainText"].ToString();
                    sconfirm = sconfirm.Replace("<MINUTES>", allowedURLs.First().Duration);
                    AreYouSure.Text = sconfirm;

                    TxtTimer.Text = "00:05";
                    //disable buttons, set intention box and combo box
                    ButtonsSection.Visibility = Visibility.Hidden;
                    TxtAutoBlockTimer.Visibility = Visibility.Hidden;

                    DoubleConfirm.Visibility = Visibility.Visible;

                    //Start the 5 second timer
                    dispatcherTimerUnlock.Stop();
                    //Remove the previous event handler so that new can be set
                    try
                    {
                        var eventField = dispatcherTimerUnlock.GetType().GetField("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
                        var eventDelegate = (Delegate)eventField.GetValue(dispatcherTimerUnlock);
                        if (eventDelegate != null)
                        {
                            var invocatationList = eventDelegate.GetInvocationList();

                            foreach (var handler in invocatationList)
                                dispatcherTimerUnlock.Tick -= ((EventHandler)handler);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("ShowSetIntentionWindow resetting timer dispatcherTimerUnlock ex - " + ex.Message);
                    }
                    nTimer = 5;
                    TxtTimer.Text = appSettings.SecondsToTimerText(nTimer);
                    dispatcherTimerUnlock.Tick += new EventHandler(dispatcherTimer_Unlock);
                    dispatcherTimerUnlock.Interval = new TimeSpan(0, 0, 1);
                    dispatcherTimerUnlock.Start();
                    CommandManager.InvalidateRequerySuggested();
                }

                
            }
            else
            {
                DoubleConfirm.Visibility = Visibility.Collapsed;
                ButtonsSection.Visibility = Visibility.Visible;
                TxtAutoBlockTimer.Visibility = Visibility.Visible;
            }

            // Manually alter window height and width
            this.SizeToContent = SizeToContent.Manual;

            // Automatically resize height relative to content
            this.SizeToContent = SizeToContent.Height;

            //Start the 30 seconds automatic block timer if the user takes no action
            dispatcherTimerAutoBlock.Stop();
            //Remove the previous event handler so that new can be set
            try
            {
                var eventField = dispatcherTimerAutoBlock.GetType().GetField("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
                var eventDelegate = (Delegate)eventField.GetValue(dispatcherTimerAutoBlock);
                if (eventDelegate != null)
                {
                    var invocatationList = eventDelegate.GetInvocationList();

                    foreach (var handler in invocatationList)
                        dispatcherTimerAutoBlock.Tick -= ((EventHandler)handler);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowSetIntentionWindow resetting timer ex - " + ex.Message);
            }

            TxtAutoBlockTimer.Text = "00:30";
            TxtAutoBlockTimer1.Text = "00:30";
            nAutoBlockTimer = 30;
            dispatcherTimerAutoBlock.Tick += new EventHandler(dispatcherTimer_AutoBlock);
            dispatcherTimerAutoBlock.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimerAutoBlock.Start();
            CommandManager.InvalidateRequerySuggested();
        }

        private void dispatcherTimer_Unlock(object sender, EventArgs e)
        {
            nTimer--;
            try
            {
                TxtTimer.Text = appSettings.SecondsToTimerText(nTimer);
                CommandManager.InvalidateRequerySuggested();
                if (nTimer == 0)
                {
                    TxtTimer.Text = "00:00";
                    DoubleConfirm.Visibility = Visibility.Collapsed;
                    ButtonsSection.Visibility = Visibility.Visible; //Show the buttons section to allow or block
                    TxtAutoBlockTimer.Visibility = Visibility.Visible;
                }
                else if (nTimer <= 0)
                {
                    CommandManager.InvalidateRequerySuggested();
                    dispatcherTimerUnlock.Stop();
                    TxtIntention.IsEnabled = true;
                    TxtIntention.Visibility = Visibility.Visible;
                    ComboBoxMinutes.IsEnabled = true;
                    btnSetIntention.IsEnabled = true;
                    btnBlockIt.IsEnabled = true;

                    //Remove the previous event handler so that new can be set
                    try
                    {
                        var eventField = dispatcherTimerUnlock.GetType().GetField("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
                        var eventDelegate = (Delegate)eventField.GetValue(dispatcherTimerUnlock);
                        if (eventDelegate != null)
                        {
                            var invocatationList = eventDelegate.GetInvocationList();

                            foreach (var handler in invocatationList)
                                dispatcherTimerUnlock.Tick -= ((EventHandler)handler);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("BreakWarning = " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShutOff window dispatcherTimer_Unlock" + ex.Message);
            }

        }

        private void dispatcherTimer_AutoBlock(object sender, EventArgs e)
        {
            nAutoBlockTimer--;
            try
            {
                TxtAutoBlockTimer.Text = appSettings.SecondsToTimerText(nAutoBlockTimer);
                TxtAutoBlockTimer1.Text = appSettings.SecondsToTimerText(nAutoBlockTimer);
                Console.WriteLine(TxtAutoBlockTimer.Text);
                CommandManager.InvalidateRequerySuggested();
                if (nAutoBlockTimer == 0)
                {
                    TxtAutoBlockTimer.Text = "00:00";
                    TxtAutoBlockTimer1.Text = "00:00";
                }
                if (nAutoBlockTimer <= 0)
                {
                    CommandManager.InvalidateRequerySuggested();
                    dispatcherTimerAutoBlock.Stop();

                    ButtonAutomationPeer peer = new ButtonAutomationPeer(btnBlockIt);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();

                    //Remove the previous event handler so that new can be set
                    try
                    {
                        var eventField = dispatcherTimerAutoBlock.GetType().GetField("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
                        var eventDelegate = (Delegate)eventField.GetValue(dispatcherTimerAutoBlock);
                        if (eventDelegate != null)
                        {
                            var invocatationList = eventDelegate.GetInvocationList();

                            foreach (var handler in invocatationList)
                                dispatcherTimerAutoBlock.Tick -= ((EventHandler)handler);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("dispatcherTimerAutoBlock remove event ex = " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("dispatcherTimerAutoBlock ex-" + ex.Message);
            }

        }

        private void SetTotalTimeSpent()
        {
            TotalTimeSpent.Text = "Total time spent: " + appSettings.SecondsToTextHMS(appSettings.nTotalTimeForDistractingSitesToday); //todo win texts
        }
        private void TxtIntention_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtIntention.Text.Trim().Length != 0)
            {
                btnSetIntention.IsEnabled = true;
            }
            else
            {
                btnSetIntention.IsEnabled = false;
            }

            // Pause the timer when the user is typing
            dispatcherTimerAutoBlock.Stop();
            _lastKeyPressTime = DateTime.Now;

            // Restart the timer after a delay if no typing happens
            DispatcherTimer restartTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(TypingDelay)
            };
            restartTimer.Tick += (s, args) =>
            {
                if ((DateTime.Now - _lastKeyPressTime).TotalMilliseconds >= TypingDelay)
                {
                    // Resume the timer after typing delay
                    dispatcherTimerAutoBlock.Start();
                    restartTimer.Stop();
                }
            };
            restartTimer.Start();

        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if(!IsVisible)
            {
                dispatcherTimerAutoBlock.Stop();
                dispatcherTimerUnlock.Stop();
            }
        }

        private void btnAllowTemporarily_Click(object sender, RoutedEventArgs e)
        {
            GridShade.Visibility = Visibility.Visible;
            GridAskIntentionPopup.Visibility = Visibility.Visible;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            GridShade.Visibility = Visibility.Collapsed;
            GridAskIntentionPopup.Visibility = Visibility.Collapsed;
        }
    }
}
