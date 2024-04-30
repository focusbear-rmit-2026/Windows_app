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
                SleepTime.Text = appSettings.ConfigFileStrings["askWorkTimeVcInfo"]["questionSleepTimeText"].ToString(); //"What time do you want to go to sleep?";
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingAskWorkTime() ex - " + ex.Message);
            }

            if(appSettings.bGoalHealthyHabits)
            {
                StartupTimePicker.NUDHTextBox.Text = "08";
                StartupTimePicker.NUDMTextBox.Text = "30";
                StartupTimePicker.NUDAMPTextBox.Text = "AM";

                ShutdownTimePicker.NUDHTextBox.Text = "06";
                ShutdownTimePicker.NUDMTextBox.Text = "30";
                ShutdownTimePicker.NUDAMPTextBox.Text = "PM";

                SleepTimePicker.NUDHTextBox.Text = "09";
                SleepTimePicker.NUDMTextBox.Text = "30";
                SleepTimePicker.NUDAMPTextBox.Text = "PM";
            }
            else
            {
                StartupTimePicker.NUDHTextBox.Text = "00";
                StartupTimePicker.NUDMTextBox.Text = "00";
                StartupTimePicker.NUDAMPTextBox.Text = "AM";

                ShutdownTimePicker.NUDHTextBox.Text = "11";
                ShutdownTimePicker.NUDMTextBox.Text = "59";
                ShutdownTimePicker.NUDAMPTextBox.Text = "PM";

                SleepTimePicker.NUDHTextBox.Text = "11";
                SleepTimePicker.NUDMTextBox.Text = "59";
                SleepTimePicker.NUDAMPTextBox.Text = "PM";
            }
           

        }

        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("ask-work-time-continue-clicked");

            string sStartupHours = StartupTimePicker.NUDHTextBox.Text;
            string sStartupMinutes = StartupTimePicker.NUDMTextBox.Text;
            string sStartupAMPM = StartupTimePicker.NUDAMPTextBox.Text; ;

            string sShutdownHours = ShutdownTimePicker.NUDHTextBox.Text;
            string sShutdownMinutes = ShutdownTimePicker.NUDMTextBox.Text;
            string sShutdownAMPM = ShutdownTimePicker.NUDAMPTextBox.Text; ;

            string sSleepTimeHours = SleepTimePicker.NUDHTextBox.Text;
            string sSleepTimeMinutes = SleepTimePicker.NUDMTextBox.Text;
            string sSleepTimeAMPM = SleepTimePicker.NUDAMPTextBox.Text; 

            appSettings.InitialStartupTimeValue = Convert.ToDateTime(sStartupHours + ":" + sStartupMinutes + " " + sStartupAMPM);
            Console.WriteLine(appSettings.InitialStartupTimeValue.ToString("HH:mm"));

            appSettings.InitialShutoffTimeValue = Convert.ToDateTime(sShutdownHours + ":" + sShutdownMinutes + " " + sShutdownAMPM);
            Console.WriteLine(appSettings.InitialShutoffTimeValue.ToString("HH:mm"));

            appSettings.InitialSleepTimeValue = Convert.ToDateTime(sSleepTimeHours + ":" + sSleepTimeMinutes + " " + sSleepTimeAMPM);
            Console.WriteLine(appSettings.InitialSleepTimeValue.ToString("HH:mm"));

            if(appSettings.InitialSleepTimeValue < appSettings.InitialShutoffTimeValue)
            {
                FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["askWorkTimeVcInfo"]["sleepTimeAlertText"].ToString(), "Focus Bear"); //"Sleep time cannot be less than finish work time." 
                ff.Owner = this._parentWindow;
                ff.ShowDialog();
                return;
            }
            ///_parentWindow.ShowOnboardingBreakRemindersScreen();
            _parentWindow.ShowOnbordingConfirmMorningRoutine();
        }

        private void StartupTimePicker_ValueChanged(object sender, bool e)
        {
            UpdateTexts();
        }

        private void ShutdownTimePicker_ValueChanged(object sender, bool e)
        {
            UpdateTexts();
        }
        private void SleepTimePicker_ValueChanged(object sender, bool e)
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

                string sSleepTimeHours = SleepTimePicker.NUDHTextBox.Text;
                string sSleepTimeMinutes = SleepTimePicker.NUDMTextBox.Text;
                string sSleepTimeAMPM = SleepTimePicker.NUDAMPTextBox.Text;

                appSettings.InitialStartupTimeValue = Convert.ToDateTime(sStartupHours + ":" + sStartupMinutes + " " + sStartupAMPM);
                Console.WriteLine(appSettings.InitialStartupTimeValue.ToString("hh:mm tt"));

                appSettings.InitialShutoffTimeValue = Convert.ToDateTime(sShutdownHours + ":" + sShutdownMinutes + " " + sShutdownAMPM);
                Console.WriteLine(appSettings.InitialShutoffTimeValue.ToString("hh:mm tt"));

                appSettings.InitialSleepTimeValue = Convert.ToDateTime(sSleepTimeHours + ":" + sSleepTimeMinutes + " " + sSleepTimeAMPM);
                Console.WriteLine(appSettings.InitialShutoffTimeValue.ToString("HH:mm"));

                TimeSpan difference =  appSettings.InitialSleepTimeValue.Subtract(appSettings.InitialShutoffTimeValue);  
                int nMinutesBetweenFinishWorkAndSleepTime = (int) (difference.TotalSeconds / 60);

                int nActualMinutesToRelax = nMinutesBetweenFinishWorkAndSleepTime - (int.Parse(appSettings.sEveningRoutineDuration) / 60) - 30; //we need to reduce the actual evening time duration //30 is the buffer before sleep time

                string ss = appSettings.ConfigFileStrings["askWorkTimeVcInfo"]["explanationText"].ToString();

                ss = "Your schedule looks like:\n\n🐥 <MORNING_ROUTINE_START_TIME> - <MORNING_ROUTINE_END_TIME> - Morning routine\n\n💼 <MORNING_ROUTINE_END_TIME> - <START_WORK_TIME> - Prepare to start work (buffer time)\n\n💪 <START_WORK_TIME> - <END_WORK_TIME> - Work\n\n🥱 <END_WORK_TIME> - <RELAX_END_TIME> - Relax - block nothing\n\n<RELAX_END_TIME> - <EVENING_ROUTINE_END_TIME> - Do evening routine.\n\n😴 <EVENING_ROUTINE_END_TIME> - Finish evening routine - computer locked (you can unlock it if you need it)"; //Todo win texts

                int nStartDiff = -1 * (((15*60) + int.Parse(appSettings.sMorningRoutineDuration))/60) ;
                ss = ss.Replace("<MORNING_ROUTINE_START_TIME>", appSettings.InitialStartupTimeValue.AddMinutes(nStartDiff).ToString("hh:mm tt"));
                ss = ss.Replace("<MORNING_ROUTINE_END_TIME>", appSettings.InitialStartupTimeValue.AddMinutes(-15).ToString("hh:mm tt"));

                ss = ss.Replace("<START_WORK_TIME>", appSettings.InitialStartupTimeValue.ToString("hh:mm tt"));
                ss = ss.Replace("<END_WORK_TIME>", appSettings.InitialShutoffTimeValue.ToString("hh:mm tt"));

                ss = ss.Replace("<RELAX_END_TIME>", appSettings.InitialShutoffTimeValue.AddMinutes(nActualMinutesToRelax).ToString("hh:mm tt"));

                ss = ss.Replace("<EVENING_ROUTINE_END_TIME>", appSettings.InitialShutoffTimeValue.AddMinutes(nActualMinutesToRelax).AddMinutes(int.Parse(appSettings.sEveningRoutineDuration) / 60).ToString("hh:mm tt"));

                Schedule.Text = ss;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("StartupTimePicker_ValueChanged() ex - " + ex.Message);
            }
        }

    }
}
