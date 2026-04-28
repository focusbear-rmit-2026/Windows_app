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
    /// Interaction logic for Privacy.xaml
    /// </summary>
    public partial class Privacy : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private FocusBearMainWindow _parentWindow;
        public Privacy()
        {
        }
        public Privacy(FocusBearMainWindow parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["onboardingCardsInfo"]["privacyText"].ToString(); //"Privacy & Security";
                Description.Text = appSettings.ConfigFileStrings["onboardingCardsInfo"]["privacyDescription"].ToString(); //"We take your privacy and security very seriously and will never share your data ever.";

                Item1.Text = appSettings.ConfigFileStrings["onboardingPrivacyTipVcInfo"]["pointFirstText"].ToString(); //"We never track which sites or apps you visit.";
                Item2.Text = appSettings.ConfigFileStrings["onboardingPrivacyTipVcInfo"]["pointSecondText"].ToString(); //"Your data syncs across devices so blocking works everywhere.";
                Item3.Text = appSettings.ConfigFileStrings["onboardingPrivacyTipVcInfo"]["pointThirdText"].ToString(); //"Your data is encrypted and yours alone.";
                EULA_PP.Content = appSettings.ConfigFileStrings["onboardingPrivacyTipVcInfo"]["buttonAcceptTermsNewText"].ToString(); //"I agree to the terms of the";
                btnViewEULAText.Text = appSettings.ConfigFileStrings["onboardingPrivacyTipVcInfo"]["buttonViewTermsNewText"].ToString(); //"Software License Agreement";
                And.Text = appSettings.ConfigFileStrings["onboardingPrivacyTipVcInfo"]["andText"].ToString(); //"and";
                btnViewPrivacyText.Text = appSettings.ConfigFileStrings["onboardingPrivacyTipVcInfo"]["buttonViewPrivacyPolicyNewText"].ToString(); //"Privacy Policy";
                Bubble1.Text = appSettings.ConfigFileStrings["onboardingPrivacyTipVcInfo"]["bubbleText"].ToString(); // "We will never compromise on your privacy!";
                btnContinueText.Text = appSettings.ConfigFileStrings["common"]["buttonContinueText"].ToString(); //"Continue";
                btnContinue.IsEnabled = false;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Privacy() EX - " + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;

        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }

        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("button-create-account-clicked");
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._CREATE_ACCOUNT_);
        }

        private void EULA_PP_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = true;
        }

        private void EULA_PP_Unchecked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = false;
        }

        private void btnViewEULA_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnViewEULA_Click");
            try
            {
                System.Diagnostics.Process.Start("https://www.focusbear.io/terms-of-service");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnViewEULA_Click ex - " + ex.Message);
            }
        }

        private void btnViewPrivacy_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnViewPrivacy_Click");
            try
            {
                System.Diagnostics.Process.Start("https://www.focusbear.io/privacy-policy");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnViewPrivacy_Click - " + ex.Message);
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            double dpiScale = VisualTreeHelper.GetDpi(this).DpiScaleX;

            // Adjust the right edge image margin based on DPI
            if (dpiScale > 1.5) // 175 DPI = ~1.82 scale
            {
                SideImage.Margin = new Thickness(0, 80, -70, 0); // Smaller top margin for high DPI
            }
            else
            {
                SideImage.Margin = new Thickness(0, 150, -70, 0);
            }
        }
        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                appSettings.CapturePostHog("privacy-and-security-shown");
            }
        }
    }
}
