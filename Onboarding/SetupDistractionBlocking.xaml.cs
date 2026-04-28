using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
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

namespace Focus_Bear.Onboarding
{
    /// <summary>
    /// Interaction logic for SetupDistractionBlocking.xaml
    /// </summary>
    public partial class SetupDistractionBlocking : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private FocusBearMainWindow _parentWindow;
        public SetupDistractionBlocking()
        {
        }
        public SetupDistractionBlocking(FocusBearMainWindow parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["setScheduledDistractionBlockingVcInfo"]["titleText"].ToString(); //"Set up Scheduled Distraction Blocking";  
                Description.Text = appSettings.ConfigFileStrings["setScheduledDistractionBlockingVcInfo"]["subTitleText"].ToString(); //"Here’s what it will look like:";
                Bubble1.Text = appSettings.ConfigFileStrings["setScheduledDistractionBlockingVcInfo"]["bubbleText"].ToString(); // "Set up distraction blocking in two quick steps!";  
                btnNoThanksText.Text = appSettings.ConfigFileStrings["setScheduledDistractionBlockingVcInfo"]["buttonNoThanks"].ToString(); //"No Thanks";
                btnSetupDistractionBlockingText.Text = appSettings.ConfigFileStrings["setScheduledDistractionBlockingVcInfo"]["buttonContinue"].ToString(); //"Set up distraction blocking";
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetupDistractionBlocking EX -" + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;

        }

        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }

        private void btnSetupDistractionBlocking_Click(object sender, RoutedEventArgs e)
        {
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._ENABLE_AI_BLOCKING_);
        }

        private void btnNoThanks_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("set-distraction-blocking-skipped");
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._ALL_SET_);
        }
        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                appSettings.CapturePostHog("set-distraction-blocking-screen-shown");
            }
        }
    }
}
