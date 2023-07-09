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
    /// Interaction logic for OnboardingNewWelcome.xaml
    /// </summary>
    public partial class OnboardingNewWelcome : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        private NewOnboardingStart _parentWindow;
        public OnboardingNewWelcome()
        {
        }
        public OnboardingNewWelcome(NewOnboardingStart parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Welcome.Text = appSettings.ConfigFileStrings["welcomeOnboardingVcInfo"]["subTitle"].ToString();

                Descriptiopn1.Text = appSettings.ConfigFileStrings["welcomeVcInfo"]["descriptionFirstText"].ToString(); //Focus Bear helps you avoid distractions and build healthy habits.
                Descriptiopn2.Text = appSettings.ConfigFileStrings["welcomeVcInfo"]["descriptionSecondText"].ToString(); //Let's start out by blocking distractions.
                btnHaveAccountText.Text = appSettings.ConfigFileStrings["welcomeVcInfo"]["buttonAlreadyHaveAccountText"].ToString(); //Let's start out by blocking distractions.
    
                buttonLetsDoItText.Text = appSettings.ConfigFileStrings["welcomeOnboardingVcInfo"]["buttonLetsDoItText"].ToString();// "Let's do it",
                Copyright.Text = appSettings.ConfigFileStrings["welcomeOnboardingVcInfo"]["allRightsReservedText"].ToString();// "© 2022 Focus Bear, All Rights Reserved."
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingNewWelcome()k EX -" + ex.Message);
            }

        }
 

        private void btnHaveAccount_Click(object sender, RoutedEventArgs e)
        {
            _parentWindow.ShowLoginScreen();
        }

        private void btnLetsDoIt_Click(object sender, RoutedEventArgs e)
        {
            _parentWindow.ShowDistractionsScreen();
        }
    }
}
