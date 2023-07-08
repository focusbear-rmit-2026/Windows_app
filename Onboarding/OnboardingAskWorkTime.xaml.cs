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
                Heading.Text = appSettings.ConfigFileStrings["askWorkTimeVcInfo"]["questionText"].ToString();
                StartupTime.Text = appSettings.ConfigFileStrings["askWorkTimeVcInfo"]["questionStartWorkTimeText"].ToString();
                ShutdownTime.Text = appSettings.ConfigFileStrings["askWorkTimeVcInfo"]["questionFinishWorkTimeText"].ToString();
                btnContinueTXT.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["buttonContinueText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingAskWorkTime() ex - " + ex.Message);
            }

            StartupTimePicker.NUDHTextBox.Text = "08";
            StartupTimePicker.NUDMTextBox.Text = "30";
            StartupTimePicker.NUDAMPTextBox.Text = "AM";

            ShutdownTimePicker.NUDHTextBox.Text = "06";
            ShutdownTimePicker.NUDMTextBox.Text = "30";
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

            _parentWindow.ShowOnboardingBreakRemindersScreen();
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
                Console.WriteLine(appSettings.InitialStartupTimeValue.ToString("hh:mm tt"));

                appSettings.InitialShutoffTimeValue = Convert.ToDateTime(sShutdownHours + ":" + sShutdownMinutes + " " + sShutdownAMPM);
                Console.WriteLine(appSettings.InitialShutoffTimeValue.ToString("hh:mm tt"));


                string ss = appSettings.ConfigFileStrings["askWorkTimeVcInfo"]["explanationText"].ToString();
                int nStartDiff = -1 * (((15*60) + int.Parse(appSettings.sMorningRoutineDuration))/60) ;
                ss = ss.Replace("<MORNING_ROUTINE_START_TIME>", appSettings.InitialStartupTimeValue.AddMinutes(nStartDiff).ToString("hh:mm tt"));
                ss = ss.Replace("<MORNING_ROUTINE_END_TIME>", appSettings.InitialStartupTimeValue.AddMinutes(-15).ToString("hh:mm tt"));

                ss = ss.Replace("<START_WORK_TIME>", appSettings.InitialStartupTimeValue.ToString("hh:mm tt"));
                ss = ss.Replace("<END_WORK_TIME>", appSettings.InitialShutoffTimeValue.ToString("hh:mm tt"));

                ss = ss.Replace("<EVENING_ROUTINE_END_TIME>", appSettings.InitialShutoffTimeValue.AddMinutes(int.Parse(appSettings.sEveningRoutineDuration) / 60).ToString("hh:mm tt"));

                Schedule.Text = ss;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StartupTimePicker_ValueChanged() ex - " + ex.Message);
            }
        }
    }
}
