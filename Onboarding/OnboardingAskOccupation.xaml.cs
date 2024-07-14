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
        private bool RadioButton1JustChecked = false;
        private bool RadioButton2JustChecked = false;
        private bool RadioButton3JustChecked = false;
        private bool RadioButton4JustChecked = false;
        private bool RadioButton5JustChecked = false;
        private bool RadioButton6JustChecked = false;
        private bool RadioButton7JustChecked = false;
        private bool RadioButton8JustChecked = false;
        
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
            string sJobs = "[";
            if(RadioButton1.IsChecked.Value)
            {
                sJobs = sJobs + "\"" + RadioButton1.Content.ToString() + "\"";
            }
            if (RadioButton2.IsChecked.Value)
            {
                sJobs = sJobs + "," + "\"" + RadioButton2.Content.ToString() + "\"";
            }
            if (RadioButton3.IsChecked.Value)
            {
                sJobs = sJobs + "," + "\"" + RadioButton3.Content.ToString() + "\"";
            }
            if (RadioButton4.IsChecked.Value)
            {
                sJobs = sJobs + "," + "\"" + RadioButton4.Content.ToString() + "\"";
            }
            if (RadioButton5.IsChecked.Value)
            {
                sJobs = sJobs + "," + "\"" + RadioButton5.Content.ToString() + "\"";
            }
            if (RadioButton6.IsChecked.Value)
            {
                sJobs = sJobs + "," + "\"" + RadioButton6.Content.ToString() + "\"";
            }
            if (RadioButton7.IsChecked.Value)
            {
                sJobs = sJobs + "," + "\"" + RadioButton7.Content.ToString() + "\"";
            }
            if (RadioButton8.IsChecked.Value)
            {
                sJobs = sJobs + "," + "\"" + TxtOtherJob.Text + "\""; ;
            }
            sJobs = sJobs + "]";

            appSettings.CapturePostHog("occupation-selected", string.Format("\"occupation\":{0}", sJobs));
            _parentWindow.ShowOnboardingGoalsScreen();
        }

        private void RadioButton1_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = true;
            RadioButton1JustChecked = true;
        }

        private void RadioButton2_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = true;
            RadioButton2JustChecked = true;
        }

        private void RadioButton3_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = true;
            RadioButton3JustChecked = true;
            
        }

        private void RadioButton4_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = true;
            RadioButton4JustChecked = true;
            
        }

        private void RadioButton5_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = true;
            RadioButton5JustChecked = true;
            
        }

        private void RadioButton6_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = true;
            RadioButton6JustChecked = true;
            
        }

        private void RadioButton7_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = true;
            RadioButton7JustChecked = true;
            
        }

        private void RadioButton8_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = false;
            RadioButton8JustChecked = true;
            TxtOtherJob.Visibility = Visibility.Visible;
            TxtOtherJob.Text = "";
            SetContinueButtonState();
        }

        private void TxtOtherJob_TextChanged(object sender, TextChangedEventArgs e)
        {
            SetContinueButtonState();
        }
        private void RadioButton1_Click(object sender, RoutedEventArgs e)
        {
            if (RadioButton1JustChecked)
            {
                RadioButton1JustChecked = false;
                e.Handled = true;
                return;
            }
            if (RadioButton1.IsChecked.Value)
                RadioButton1.IsChecked = false;

            SetContinueButtonState();
        }

        private void RadioButton2_Click(object sender, RoutedEventArgs e)
        {
            if (RadioButton2JustChecked)
            {
                RadioButton2JustChecked = false;
                e.Handled = true;
                return;
            }
            if (RadioButton2.IsChecked.Value)
                RadioButton2.IsChecked = false;

            SetContinueButtonState();
        }

        private void RadioButton3_Click(object sender, RoutedEventArgs e)
        {
            if (RadioButton3JustChecked)
            {
                RadioButton3JustChecked = false;
                e.Handled = true;
                return;
            }
            if (RadioButton3.IsChecked.Value)
                RadioButton3.IsChecked = false;

            SetContinueButtonState();
        }

        private void RadioButton4_Click(object sender, RoutedEventArgs e)
        {
            if (RadioButton4JustChecked)
            {
                RadioButton4JustChecked = false;
                e.Handled = true;
                return;
            }
            if (RadioButton4.IsChecked.Value)
                RadioButton4.IsChecked = false;

            SetContinueButtonState();
        }

        private void RadioButton5_Click(object sender, RoutedEventArgs e)
        {
            if (RadioButton5JustChecked)
            {
                RadioButton5JustChecked = false;
                e.Handled = true;
                return;
            }
            if (RadioButton5.IsChecked.Value)
                RadioButton5.IsChecked = false;

            SetContinueButtonState();
        }

        private void RadioButton6_Click(object sender, RoutedEventArgs e)
        {
            if (RadioButton6JustChecked)
            {
                RadioButton6JustChecked = false;
                e.Handled = true;
                return;
            }
            if (RadioButton6.IsChecked.Value)
                RadioButton6.IsChecked = false;

            SetContinueButtonState();
        }

        private void RadioButton7_Click(object sender, RoutedEventArgs e)
        {
            if (RadioButton7JustChecked)
            {
                RadioButton7JustChecked = false;
                e.Handled = true;
                return;
            }
            if (RadioButton7.IsChecked.Value)
                RadioButton7.IsChecked = false;

            SetContinueButtonState();
        }

        private void RadioButton8_Click(object sender, RoutedEventArgs e)
        {
            if (RadioButton8JustChecked)
            {
                RadioButton8JustChecked = false;
                e.Handled = true;
                return;
            }
            if (RadioButton8.IsChecked.Value)
            {
                RadioButton8.IsChecked = false;

                TxtOtherJob.Visibility = Visibility.Collapsed;
                TxtOtherJob.Text = "";
            }
            SetContinueButtonState();
        }

        private void SetContinueButtonState()
        {
            if (RadioButton1.IsChecked.Value)
            {
                btnContinue.IsEnabled = true;
                return;
            }
            else if (RadioButton2.IsChecked.Value)
            {
                btnContinue.IsEnabled = true;
                return;
            }
            else if (RadioButton3.IsChecked.Value)
            {
                btnContinue.IsEnabled = true;
                return;
            }
            else if (RadioButton4.IsChecked.Value)
            {
                btnContinue.IsEnabled = true;
                return;
            }
            else if (RadioButton5.IsChecked.Value)
            {
                btnContinue.IsEnabled = true;
                return;
            }
            else if (RadioButton6.IsChecked.Value)
            {
                btnContinue.IsEnabled = true;
                return;
            }
            else if (RadioButton7.IsChecked.Value)
            {
                btnContinue.IsEnabled = true;
                return;
            }
            else if (RadioButton8.IsChecked.Value)
            {
                if (TxtOtherJob.Text.Length > 0)
                {
                    btnContinue.IsEnabled = true;
                    return;
                }
            }

            btnContinue.IsEnabled = false;
        }
    }
}
