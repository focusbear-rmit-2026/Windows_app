using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
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

namespace Focus_Bear.Onboarding
{
    /// <summary>
    /// Interaction logic for HoldOn.xaml
    /// </summary>
    public partial class HoldOn : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private FocusBearMainWindow _parentWindow;
        // Times how long the user sits on the "hold on" gate before proceeding, so analytics
        // can see where onboarding stalls right before habit setup (analytics#33).
        private readonly System.Diagnostics.Stopwatch _dwellStopwatch = new System.Diagnostics.Stopwatch();
        public HoldOn()
        {
        }
        public HoldOn(FocusBearMainWindow parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["holdOnVcInfo"]["titleText"].ToString(); //"Hold on a minute!"; 
                Bubble1.Text = appSettings.ConfigFileStrings["holdOnVcInfo"]["bubbleText"].ToString(); //"Arr… if you want to focus better, you’ve got to get your habits shipshape first.";  
                btnOkayText.Text = appSettings.ConfigFileStrings["holdOnVcInfo"]["buttonContinueText"].ToString(); //"Okay...";
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingHoldOn EX -" + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;

        }

        private void btnOkay_Click(object sender, RoutedEventArgs e)
        {
            _dwellStopwatch.Stop();

            //Disambiguates the holdon-screen-shown abandonment signal: without this event,
            //users who clicked Okay but stalled on the next (habits webview) screen are
            //indistinguishable from users who quit on this screen (windows-app-v2#1316)
            appSettings.CapturePostHog("holdon-okay-clicked");
            //Dual-fire the analytics-canonical outcome name with dwell time (analytics#33).
            appSettings.CapturePostHog(Constants.Events.HoldOnScreenSucceeded,
                $"\"duration_ms\":{_dwellStopwatch.ElapsedMilliseconds}");

            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._SET_UP_HABITS_);
        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }

        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                _dwellStopwatch.Restart();
                appSettings.CapturePostHog("holdon-screen-shown");
            }
        }
    }
}
