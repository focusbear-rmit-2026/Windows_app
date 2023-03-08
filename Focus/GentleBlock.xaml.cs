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
        public GentleBlock()
        {
            InitializeComponent();
            ShowInTaskbar = false;
            Topmost = true;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        public void ShowGentleBlockWindowAndStartTimer(IntPtr hWnd, string sFocusModeTitle, string sAppName, string sURL = "", string sIntention = "", string sTabTitle = "")
        {
            /*
             * "popUpBlockConfirmVcInfo": {
                "title": "Do you need to use <URL> when you're focusing on <FOCUSTYPE>?",
                "buttonAllowText": "Yes - just this time",
                "buttonBlockText": "No - I was getting distracted",
                "buttonEditSettingsText": "Edit Settings",
                "intentionText": "Your intention was \"<INTENTION>\""
            },
             */
            sFocusModeName = sFocusModeTitle;
            sActiveAppName = sAppName; 
            sFullURLForBlockedPage = sURL;
            sActiveURL = sURL;
            sCurrentTabTitle = sTabTitle;
            HWND = hWnd;

            if (sURL.Length > 0)
            {
                try
                {
                    Uri myUri = new Uri(sURL);
                    string host = myUri.Host; //fails if ther eis no http://
                    sActiveURL = host;
                    Debug.WriteLine(sActiveURL);
                }
                catch (Exception ex)
                {
                    /*
                    This constructor initializes a new instance of the UriBuilder class with the Fragment, Host, Path, Port, Query, Scheme, and Uri properties set as specified in uri.
                    If uri does not specify a scheme, the scheme defaults to "http:".
                    */
                    Uri myUri = new UriBuilder(sURL).Uri;
                    sActiveURL = myUri.Host;
                    Debug.WriteLine(ex.Message);
                }
            }

            btnJustThisTimeText.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["buttonAllowText"].ToString();
            btnGettingDistractedText.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["buttonBlockText"].ToString();
            btnEditSettingsText.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["buttonEditSettingsText"].ToString();

            string sHeading = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["title"].ToString();
            if (sAppName.Length > 0)
            {
                sHeading = sHeading.Replace("<URL>", sAppName + ".exe");
            }
            else
            {
                sHeading = sHeading.Replace("<URL>", sActiveURL);
            }
            sHeading = sHeading.Replace("<FOCUSTYPE>", sFocusModeTitle);
            Heading.Text = sHeading;

            if (sIntention.Length > 0) //Set the activity title if thelength is > 0
            {
                string sInt = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["intentionText"].ToString();
                sInt = sInt.Replace("<INTENTION>",sIntention);
                Intention.Text = sInt;

                this.Height = 400;
                Intention.Visibility = Visibility.Visible;
            }
            else
            {
                this.Height = 360;
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
            dispatcherTimer.Start();
            CommandManager.InvalidateRequerySuggested();
        }
        //public void HideBreakWindowAndStopTimer()
        //{
        //    dispatcherTimer.Stop();
        //    Hide();
        //    TxtTimer.Text = "00:00";
        //}


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

        private void btnGettingDistracted_Click(object sender, RoutedEventArgs e)
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

        private void btnEditSettings_Click(object sender, RoutedEventArgs e)
        {
            ((App)(Application.Current)).ShowPreferencesWindow();
            ((App)(Application.Current)).wndPreferences.ShowFocusMode(sFocusModeName);
            dispatcherTimer.Stop();
            Hide();
        }
    }
}
