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
    /// Interaction logic for WelcomeLetsDoIt.xaml
    /// </summary>
    public partial class WelcomeLetsDoIt : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private FocusBearMainWindow _parentWindow;
        bool _hasAnimated = false;
        public WelcomeLetsDoIt()
        {
        }
        public WelcomeLetsDoIt(FocusBearMainWindow parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Bubble1.Text = appSettings.ConfigFileStrings["welcomeLetsDoItVcInfo"]["bubbleLeftText"].ToString(); //"It’s so hard to focus with all the fun.. I mean distracting, websites!";  
                Bubble2.Text = appSettings.ConfigFileStrings["welcomeLetsDoItVcInfo"]["bubbleRightText"].ToString(); //"Want to team up? We can focus together!";

                btnLetsDoItText.Text = appSettings.ConfigFileStrings["welcomeLetsDoItVcInfo"]["buttonLetsDoIt"].ToString(); //"Let's do it!";
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingNewWelcomeLetsDoIt() EX - " + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;

        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }

        private void btnLetsDoIt_Click(object sender, RoutedEventArgs e)
        {
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._SET_UP_ACCOUNT_);
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
                appSettings.CapturePostHog("welcome-lets-do-it-screen-shown");
            }
        }
    }
}
