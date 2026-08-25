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
    /// Interaction logic for AskWhichDistractions.xaml
    /// </summary>
    public partial class AskWhichDistractions : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private FocusBearMainWindow _parentWindow;
        public AskWhichDistractions()
        {
        }
        public AskWhichDistractions(FocusBearMainWindow parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["askSetDistractionsVcInfo"]["title"].ToString(); //"Not sure which distractions to block?";
                Description.Text = appSettings.ConfigFileStrings["askSetDistractionsVcInfo"]["subTitle"].ToString(); //"We can suggest distractions based on your job - so you can stay in the zone.";

                Bubble1.Text = appSettings.ConfigFileStrings["enableAIBlockingVcInfo"]["bubbleText"].ToString(); //"I see far too many distracting websites";

                btnPickMyselfText.Text = appSettings.ConfigFileStrings["askSetDistractionsVcInfo"]["buttonNoThanks"].ToString(); //"I’ll pick them myself";
                btnHelpMePickText.Text = appSettings.ConfigFileStrings["askSetDistractionsVcInfo"]["buttonContinue"].ToString(); //"Yes, help me pick!";
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AskWhichDistractions() EX - " + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;

        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }


        private void btnHelpMePick_Click(object sender, RoutedEventArgs e)
        {
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._ASK_OCCUPATION_);
        }

        private void btnPickMyself_Click(object sender, RoutedEventArgs e)
        {
            // Blocked URLs/Apps now come before Allowed URLs (flow reorder to match mac).
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._BLOCKED_URLS_);
        }
        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                appSettings.CapturePostHog("suggest-distractions-screen-shown");
            }
        }
    }
}
