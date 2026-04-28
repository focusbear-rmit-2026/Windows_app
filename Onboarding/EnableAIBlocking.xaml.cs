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
    /// Interaction logic for EnableAIBlocking.xaml
    /// </summary>
    public partial class EnableAIBlocking : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private FocusBearMainWindow _parentWindow;
        public EnableAIBlocking()
        {
        }
        public EnableAIBlocking(FocusBearMainWindow parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["enableAIBlockingVcInfo"]["title"].ToString(); //"Do you want to use AI to block distractions?";
                Description.Text = appSettings.ConfigFileStrings["enableAIBlockingVcInfo"]["subTitle"].ToString(); //"Focus Bear can use AI to detect if you’re getting distracted and guide you back on track.";

                Bubble1.Text = appSettings.ConfigFileStrings["askSetDistractionsVcInfo"]["bubbleText"].ToString(); //"Don’t worry, the AI provider doesn’t get your data!";

                Item1.Text = appSettings.ConfigFileStrings["enableAIBlockingVcInfo"]["pointFirstText"].ToString(); //"The AI never knows who you are.";
                Item2.Text = appSettings.ConfigFileStrings["enableAIBlockingVcInfo"]["pointSecondText"].ToString(); //"Your browsing stays private - we don't share it with AI providers.";
                Item3.Text = appSettings.ConfigFileStrings["enableAIBlockingVcInfo"]["pointThirdText"].ToString(); //"You can turn this off whenever you like.";

                btnNoThanksText.Text = appSettings.ConfigFileStrings["enableAIBlockingVcInfo"]["buttonNoThanks"].ToString(); //"No thanks";
                btnMakeMeIndistractibleText.Text = appSettings.ConfigFileStrings["enableAIBlockingVcInfo"]["buttonContinue"].ToString(); //"Make me indistractible";
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("EnableAIBlocking() EX - " + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;

        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }

        private void btnMakeMeIndistractible_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("opt-in-for-ai-yes");
            appSettings.bAiCheckUrlEnabled = true;
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._ASK_SET_DISTRACTIONS_);
        }

        private void btnNoThanks_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("opt-in-for-ai-no");
            appSettings.bAiCheckUrlEnabled = false;
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._ASK_SET_DISTRACTIONS_);
        }

        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                appSettings.CapturePostHog("enable-ai-blocking-screen-shown");
            }
        }
    }
}
