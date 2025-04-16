using Focus_Bear.Onboarding.New;
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
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Focus_Bear.Onboarding.NEW
{
    /// <summary>
    /// Interaction logic for OnboardingNewPrivacy.xaml
    /// </summary>
    public partial class OnboardingNewPrivacy : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private NewOnboardingStart _parentWindow;
        public OnboardingNewPrivacy()
        {
        }
        public OnboardingNewPrivacy(NewOnboardingStart parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["onboardingPrivacyTipVcInfo"]["title"].ToString();

                Welcome.Text = appSettings.ConfigFileStrings["onboardingPrivacyTipVcInfo"]["subTitle"].ToString();

                Descriptiopn1.Text = appSettings.ConfigFileStrings["onboardingPrivacyTipVcInfo"]["descriptionText"].ToString() + "\n\n" +
                appSettings.ConfigFileStrings["onboardingPrivacyTipVcInfo"]["moreDetailText"].ToString();

                btnViewPrivacyText.Text = appSettings.ConfigFileStrings["onboardingPrivacyTipVcInfo"]["buttonViewPrivacyText"].ToString(); //Let's start out by blocking distractions.
                buttonLetsDoItText.Text = appSettings.ConfigFileStrings["onboardingPrivacyTipVcInfo"]["buttonContinueText"].ToString();// "Let's do it",

                AcceptPrivacyPolicy.Content = appSettings.ConfigFileStrings["onboardingPrivacyTipVcInfo"]["buttonAcceptPrivacyPolicyText"].ToString();//"I accept the Privacy Policy";  
                SetContinueButtonState();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingNewPrivacy()k EX -" + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;

        }

        void SetContinueButtonState()
        {
            if(AcceptPrivacyPolicy.IsChecked.Value)
            {
                btnLetsDoIt.IsEnabled = true;
            }
            else
            {
                btnLetsDoIt.IsEnabled = false;
            }
        }
        private void btnLetsDoIt_Click(object sender, RoutedEventArgs e) //continue button
        {
            _parentWindow.ShowDistractionsShieldActivatedScreen();
        }

        private void btnViewPrivacy_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnViewPrivacy_Click");
            try
            {
                System.Diagnostics.Process.Start("https://www.focusbear.io/en/privacy-policy");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnViewPrivacy_Click + " + ex.Message);
            }
        }

        private void AcceptPrivacyPolicy_Checked(object sender, RoutedEventArgs e)
        {
            SetContinueButtonState();
        }

        private void AcceptPrivacyPolicy_Unchecked(object sender, RoutedEventArgs e)
        {
            SetContinueButtonState();
        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this, BackgroundImage);
        }
    }
}
