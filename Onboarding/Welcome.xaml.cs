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
    /// Interaction logic for Welcome.xaml
    /// </summary>
    public partial class Welcome : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private FocusBearMainWindow _parentWindow;
        public Welcome()
        {
        }
        public Welcome(FocusBearMainWindow parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Bubble1.Text = appSettings.ConfigFileStrings["welcomeVcInfo"]["bubbleText"].ToString();  //"Howdy! I'm Focus Bear Junior";  
                btnHaveAccountText.Text = appSettings.ConfigFileStrings["welcomeVcInfo"]["buttonAlreadyHaveAccountText"].ToString(); 
                btnHowdyText.Text = appSettings.ConfigFileStrings["welcomeVcInfo"]["buttonHowdy"].ToString();  //"Howdy!";
                Copyright.Text = appSettings.ConfigFileStrings["welcomeOnboardingVcInfo"]["allRightsReservedText"].ToString();// "© 2022 Focus Bear, All Rights Reserved."
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingNewWelcome()k EX -" + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;

        }


        private void btnHaveAccount_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("button-already-have-account-clicked");
            appSettings.bAlreadyHaveAnAccountClicked = true;
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._CREATE_ACCOUNT_);
        }

        private void btnHowdy_Click(object sender, RoutedEventArgs e)
        {
            appSettings.bAlreadyHaveAnAccountClicked = false;
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._WELCOME_ADHD_);
        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }

        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if(IsVisible)
            {
                appSettings.CapturePostHog("welcome-screen-shown");
            }
        }
    }
}

