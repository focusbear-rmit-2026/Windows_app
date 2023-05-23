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
        private NewOnboardingStart _parentWindow;
        public OnboardingNewPrivacy()
        {
        }
        public OnboardingNewPrivacy(NewOnboardingStart parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            /*
            "onboardingPrivacyTipVcInfo": {
                "title": "Privacy and Security",
                "subTitle": "We take your privacy and security very seriously and will never share your data ever.",
                "descriptionText": "🙈 We do not log which websites or apps you use.\n\n🔄 We do sync your habit data to our server but this is only to support cross device distraction blocking.\n\n🔒 Your data is encrypted and we don't let anyone else see it.",
                "moreDetailText": "For more details, refer to our privacy policy or feel free to email team@focusbear.io.",
                "buttonViewPrivacyText": "Click here to view Privacy Policy",
                "buttonContinueText": "Continue"
            },
          */
            try
            {
                Heading.Text = appSettings.ConfigFileStrings["onboardingPrivacyTipVcInfo"]["title"].ToString();

                Welcome.Text = appSettings.ConfigFileStrings["onboardingPrivacyTipVcInfo"]["subTitle"].ToString();

                Descriptiopn1.Text = appSettings.ConfigFileStrings["onboardingPrivacyTipVcInfo"]["descriptionText"].ToString() + "\n\n" +
                appSettings.ConfigFileStrings["onboardingPrivacyTipVcInfo"]["moreDetailText"].ToString();

                btnViewPrivacyText.Text = appSettings.ConfigFileStrings["onboardingPrivacyTipVcInfo"]["buttonViewPrivacyText"].ToString(); //Let's start out by blocking distractions.
                buttonLetsDoItText.Text = appSettings.ConfigFileStrings["onboardingPrivacyTipVcInfo"]["buttonContinueText"].ToString();// "Let's do it",
               
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingNewPrivacy()k EX -" + ex.Message);
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
    }
}
