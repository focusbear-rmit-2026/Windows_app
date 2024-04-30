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
    /// Interaction logic for OnboardingAskOccupation.xaml
    /// </summary>
    public partial class OnboardingAskOccupation : Page
    {
        private OnBoardingWindow _parentWindow;
        AppSettings appSettings = AppSettings.GetInstance;
        public OnboardingAskOccupation()
        {
           
        }

        public OnboardingAskOccupation(OnBoardingWindow parentWindow) : this()
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["title"].ToString();
                Occupation.Text = appSettings.ConfigFileStrings["askOccupationVcInfo"]["subTitle"].ToString();
                btnContinueTXT.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["buttonContinueText"].ToString();
                btnContinue.IsEnabled = false;
                TxtOtherJob.Visibility = Visibility.Collapsed;

                RadioButton1.Content = appSettings.ConfigFileStrings["askOccupationVcInfo"]["arrayOccupations"][0].ToString();
                RadioButton2.Content = appSettings.ConfigFileStrings["askOccupationVcInfo"]["arrayOccupations"][1].ToString();
                RadioButton3.Content = appSettings.ConfigFileStrings["askOccupationVcInfo"]["arrayOccupations"][2].ToString();
                RadioButton4.Content = appSettings.ConfigFileStrings["askOccupationVcInfo"]["arrayOccupations"][3].ToString();
                RadioButton5.Content = appSettings.ConfigFileStrings["askOccupationVcInfo"]["arrayOccupations"][4].ToString();
                RadioButton6.Content = appSettings.ConfigFileStrings["askOccupationVcInfo"]["arrayOccupations"][5].ToString();
                RadioButton7.Content = appSettings.ConfigFileStrings["askOccupationVcInfo"]["arrayOccupations"][6].ToString();
                RadioButton8.Content = appSettings.ConfigFileStrings["askOccupationVcInfo"]["arrayOccupations"][7].ToString();

                appSettings.CapturePostHog("ask-occupation-screen-shown");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingAskOccupation() ex - " + ex.Message);
            }

        }
        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            _parentWindow.ShowOnboardingGoalsScreen();
        }

        private void RadioButton1_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = true;
            TxtOtherJob.Visibility = Visibility.Collapsed;
            appSettings.sSelectedOccupationWhileOnboarding = appSettings.ConfigFileStrings["askOccupationVcInfo"]["arrayOccupations"][0].ToString();
        }

        private void RadioButton2_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = true;
            TxtOtherJob.Visibility = Visibility.Collapsed;
            appSettings.sSelectedOccupationWhileOnboarding = appSettings.ConfigFileStrings["askOccupationVcInfo"]["arrayOccupations"][1].ToString();
        }

        private void RadioButton3_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = true;
            TxtOtherJob.Visibility = Visibility.Collapsed;
            appSettings.sSelectedOccupationWhileOnboarding = appSettings.ConfigFileStrings["askOccupationVcInfo"]["arrayOccupations"][2].ToString();
        }

        private void RadioButton4_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = true;
            TxtOtherJob.Visibility = Visibility.Collapsed;
            appSettings.sSelectedOccupationWhileOnboarding = appSettings.ConfigFileStrings["askOccupationVcInfo"]["arrayOccupations"][3].ToString();
        }

        private void RadioButton5_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = true;
            TxtOtherJob.Visibility = Visibility.Collapsed;
            appSettings.sSelectedOccupationWhileOnboarding = appSettings.ConfigFileStrings["askOccupationVcInfo"]["arrayOccupations"][4].ToString();
        }

        private void RadioButton6_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = true;
            TxtOtherJob.Visibility = Visibility.Collapsed;
            appSettings.sSelectedOccupationWhileOnboarding = TxtOtherJob.Text;
            appSettings.sSelectedOccupationWhileOnboarding = appSettings.ConfigFileStrings["askOccupationVcInfo"]["arrayOccupations"][5].ToString();
        }

        private void RadioButton7_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = true;
            TxtOtherJob.Visibility = Visibility.Collapsed;
            appSettings.sSelectedOccupationWhileOnboarding = appSettings.ConfigFileStrings["askOccupationVcInfo"]["arrayOccupations"][6].ToString();
        }

        private void RadioButton8_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = false;
            TxtOtherJob.Visibility = Visibility.Visible;
            TxtOtherJob.Text = "";
        }

        private void TxtOtherJob_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtOtherJob.Text.Length > 0)
            {
                btnContinue.IsEnabled = true;
            }
            else
            {
                btnContinue.IsEnabled = false;
            }
            appSettings.sSelectedOccupationWhileOnboarding = TxtOtherJob.Text;
        }
    }
}
