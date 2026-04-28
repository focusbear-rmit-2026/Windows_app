using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
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
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace Focus_Bear.Onboarding
{
    /// <summary>
    /// Interaction logic for WelcomeADHD.xaml
    /// </summary>
    public partial class WelcomeADHD : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private FocusBearMainWindow _parentWindow;
        bool _hasAnimated = false;
        public WelcomeADHD()
        {
        }
        public WelcomeADHD(FocusBearMainWindow parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Bubble1.Text = appSettings.ConfigFileStrings["welcomeAdhdVcInfo"]["bubbleLeftText"].ToString(); //"I’m actually not that good at focusing though";  
                Bubble2.Text = appSettings.ConfigFileStrings["welcomeAdhdVcInfo"]["bubbleRightText"].ToString(); //"I’ve got ADHD and get distracted all the time 😢";

                btnNeverHadIssueText.Text = appSettings.ConfigFileStrings["welcomeAdhdVcInfo"]["buttonNeverHadIssue"].ToString(); //"I never had that issue";
                btnMeTooText.Text = appSettings.ConfigFileStrings["welcomeAdhdVcInfo"]["buttonMeToo"].ToString(); //"Me too!";
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingNewWelcomeADHD() EX -" + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;

        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }

        private void btnNeverHadIssue_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("button-never-had-issue-clicked");
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._SET_UP_ACCOUNT_);
        }

        private void btnMeToo_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("button-me-too-clicked");
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._WELCOME_LETS_DO_IT_);
        }
        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (_hasAnimated) return;

            _hasAnimated = true;

            await Bubble2.ShowAsync(500); // shows after 0.5s
        }
        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                appSettings.CapturePostHog("welcome-adhd-screen-shown");
            }
        }
    }
}
