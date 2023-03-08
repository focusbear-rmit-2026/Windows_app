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
    /// Interaction logic for OnboardingAskWorkTime.xaml
    /// </summary>
    public partial class OnboardingAskWorkTime : Page
    {
        private OnBoardingWindow _parentWindow;
        AppSettings appSettings = AppSettings.GetInstance;

        public OnboardingAskWorkTime()
        {
        }
        public OnboardingAskWorkTime(OnBoardingWindow parentWindow) : this()
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["welcomeInfo"]["welcomeMessageHeading"].ToString();
                Description.Text = appSettings.ConfigFileStrings["askWorkTimeVcInfo"]["questionText"].ToString();
                StartupTime.Text = appSettings.ConfigFileStrings["askWorkTimeVcInfo"]["titleStartupTime"].ToString();
                ShutdownTime.Text = appSettings.ConfigFileStrings["askWorkTimeVcInfo"]["titleShutDownTime"].ToString();
                btnContinueTXT.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["buttonContinueText"].ToString();
                string ss = appSettings.ConfigFileStrings["askWorkTimeVcInfo"]["descriptionStartupTime"].ToString();
                ss = ss.Replace("<STARTTIME>", "05:15 AM");
                ss = ss.Replace("<SHUTOFFTIME>", "08:30 PM");
                StartupTimeDesc.Text = ss;

                string ss1 = appSettings.ConfigFileStrings["askWorkTimeVcInfo"]["descriptionShutDownTime"].ToString();
                ss1 = ss1.Replace("<SHUTOFFTIME>", "08:30 PM");
                ShutdownTimeDesc.Text = ss1;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingAskWorkTime() ex - " + ex.Message);
            }

            StartupTimePicker.NUDHTextBox.Text = "05";
            StartupTimePicker.NUDMTextBox.Text = "15";
            StartupTimePicker.NUDSTextBox.Text = "0";
            StartupTimePicker.NUDAMPTextBox.Text = "AM";

            ShutdownTimePicker.NUDHTextBox.Text = "08";
            ShutdownTimePicker.NUDMTextBox.Text = "30";
            ShutdownTimePicker.NUDSTextBox.Text = "0";
            ShutdownTimePicker.NUDAMPTextBox.Text = "PM";
        }

        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            string sStartupHours = StartupTimePicker.NUDHTextBox.Text;
            string sStartupMinutes = StartupTimePicker.NUDMTextBox.Text;
            string sStartupAMPM = StartupTimePicker.NUDAMPTextBox.Text; ;

            string sShutdownHours = ShutdownTimePicker.NUDHTextBox.Text;
            string sShutdownMinutes = ShutdownTimePicker.NUDMTextBox.Text;
            string sShutdownAMPM = ShutdownTimePicker.NUDAMPTextBox.Text; ;

            appSettings.InitialStartupTimeValue = Convert.ToDateTime(sStartupHours + ":" + sStartupMinutes + " " + sStartupAMPM);
            Console.WriteLine(appSettings.InitialStartupTimeValue.ToString("HH:mm"));

            appSettings.InitialShutoffTimeValue = Convert.ToDateTime(sShutdownHours + ":" + sShutdownMinutes + " " + sShutdownAMPM);
            Console.WriteLine(appSettings.InitialShutoffTimeValue.ToString("HH:mm"));

            _parentWindow.ShowOnboardingWelcomeScreen();
        }

        private void StartupTimePicker_ValueChanged(object sender, bool e)
        {
            UpdateTexts();
        }

        private void ShutdownTimePicker_ValueChanged(object sender, bool e)
        {
            UpdateTexts();
        }
        private void UpdateTexts()
        {
            try
            {
                string sStartupHours = StartupTimePicker.NUDHTextBox.Text;
                string sStartupMinutes = StartupTimePicker.NUDMTextBox.Text;
                string sStartupAMPM = StartupTimePicker.NUDAMPTextBox.Text; ;

                string sShutdownHours = ShutdownTimePicker.NUDHTextBox.Text;
                string sShutdownMinutes = ShutdownTimePicker.NUDMTextBox.Text;
                string sShutdownAMPM = ShutdownTimePicker.NUDAMPTextBox.Text; ;

                appSettings.InitialStartupTimeValue = Convert.ToDateTime(sStartupHours + ":" + sStartupMinutes + " " + sStartupAMPM);
                Console.WriteLine(appSettings.InitialStartupTimeValue.ToString("HH:mm"));

                appSettings.InitialShutoffTimeValue = Convert.ToDateTime(sShutdownHours + ":" + sShutdownMinutes + " " + sShutdownAMPM);
                Console.WriteLine(appSettings.InitialShutoffTimeValue.ToString("HH:mm"));


                string ss = appSettings.ConfigFileStrings["askWorkTimeVcInfo"]["descriptionStartupTime"].ToString();
                ss = ss.Replace("<STARTTIME>", appSettings.InitialStartupTimeValue.ToString("HH:mm"));
                ss = ss.Replace("<SHUTOFFTIME>", appSettings.InitialShutoffTimeValue.ToString("HH:mm"));
                StartupTimeDesc.Text = ss;

                string ss1 = appSettings.ConfigFileStrings["askWorkTimeVcInfo"]["descriptionShutDownTime"].ToString();
                ss1 = ss1.Replace("<SHUTOFFTIME>", appSettings.InitialShutoffTimeValue.ToString("HH:mm"));
                ShutdownTimeDesc.Text = ss1;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StartupTimePicker_ValueChanged() ex - " + ex.Message);
            }
        }
    }
}
