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
    /// Interaction logic for OnboardingConfirmEveningRoutine.xaml
    /// </summary>
    public partial class OnboardingConfirmEveningRoutine : Page
    {
        private OnBoardingWindow _parentWindow;
        AppSettings appSettings = AppSettings.GetInstance;
        public OnboardingConfirmEveningRoutine()
        {

        }
        public OnboardingConfirmEveningRoutine(OnBoardingWindow parentWindow) : this()
        {
            InitializeComponent();
            _parentWindow = parentWindow;
            LoadTexts();
        }

        public void LoadTexts()
        {
            try
            {
                Heading.Text = appSettings.ConfigFileStrings["confirmEveningRoutineTimeVcInfo"]["title"].ToString();
                ToBeClearDesc.Text = appSettings.ConfigFileStrings["confirmEveningRoutineTimeVcInfo"]["subTitle"].ToString();

                string sAtTime = appSettings.ConfigFileStrings["confirmEveningRoutineTimeVcInfo"]["timeText"].ToString();
                AtTime1.Text = sAtTime.Replace("<TIME>", appSettings.InitialShutoffTimeValue.ToString("hh:mm tt"));

                pointFirstRelaxTimeText.Text = appSettings.ConfigFileStrings["confirmEveningRoutineTimeVcInfo"]["pointFirstMorningTimeText"].ToString();
                pointSecondRelaxTimeText.Text = appSettings.ConfigFileStrings["confirmEveningRoutineTimeVcInfo"]["pointSecondMorningTimeText"].ToString();
                pointThirdRelaxTimeText.Text = appSettings.ConfigFileStrings["confirmEveningRoutineTimeVcInfo"]["pointThirdMorningTimeText"].ToString();

                TimeSpan difference = appSettings.InitialSleepTimeValue.Subtract(appSettings.InitialShutoffTimeValue);
                int nMinutesBetweenFinishWorkAndSleepTime = (int)(difference.TotalSeconds / 60);
                int nActualMinutesToRelax = nMinutesBetweenFinishWorkAndSleepTime - (appSettings.nEveningRoutineDuration / 60) - 30; //we need to reduce the actual evening time duration //30 is the buffer before sleep time

                string sAtTime2 = appSettings.ConfigFileStrings["confirmEveningRoutineTimeVcInfo"]["timeText"].ToString();
                AtTime2.Text = sAtTime2.Replace("<TIME>", appSettings.InitialShutoffTimeValue.AddMinutes(nActualMinutesToRelax).AddMinutes(appSettings.nEveningRoutineDuration / 60).ToString("hh:mm tt"));

                
                pointFirstEveningTimeText.Text = appSettings.ConfigFileStrings["confirmEveningRoutineTimeVcInfo"]["pointFirstMorningTimeText"].ToString();
                pointSecondEveningTimeText.Text = appSettings.ConfigFileStrings["confirmEveningRoutineTimeVcInfo"]["pointSecondMorningTimeText"].ToString();
                pointThirdEveningTimeText.Text = appSettings.ConfigFileStrings["confirmEveningRoutineTimeVcInfo"]["pointThirdMorningTimeText"].ToString();
                pointFourthEveningTimeText.Text = appSettings.ConfigFileStrings["confirmEveningRoutineTimeVcInfo"]["pointThirdMorningTimeText"].ToString();

                btnChangeThisTXT.Text = appSettings.ConfigFileStrings["confirmMorningRoutineTimeVcInfo"]["buttonReviewText"].ToString();
                btnSoundsGoodTXT.Text = appSettings.ConfigFileStrings["confirmMorningRoutineTimeVcInfo"]["buttonContinueText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingAskWorkTime() ex - " + ex.Message); //Todo Win Texts

                Heading.Text = "Confirm your evening routine";
                ToBeClearDesc.Text = "To be super clear, here's what's going to happen in the evening.";

                string sAtTime1 = "At <TIME>:";
                AtTime1.Text = sAtTime1.Replace("<TIME>", appSettings.InitialShutoffTimeValue.ToString("hh:mm tt"));

                pointFirstRelaxTimeText.Text = "You'll enter \"Relax mode\".";
                pointSecondRelaxTimeText.Text = "You can use any app (including websites that are normally blocked).";
                pointThirdRelaxTimeText.Text = "Good for playing games or unrestricted browsing. You've earned it after a hard day of focusing.";

                TimeSpan difference = appSettings.InitialSleepTimeValue.Subtract(appSettings.InitialShutoffTimeValue);
                int nMinutesBetweenFinishWorkAndSleepTime = (int)(difference.TotalSeconds / 60);
                int nActualMinutesToRelax = nMinutesBetweenFinishWorkAndSleepTime - (appSettings.nEveningRoutineDuration / 60) - 30; //we need to reduce the actual evening time duration //30 is the buffer before sleep time

                AtTime2.Text = sAtTime1.Replace("<TIME>", appSettings.InitialShutoffTimeValue.AddMinutes(nActualMinutesToRelax).AddMinutes(appSettings.nEveningRoutineDuration / 60).ToString("hh:mm tt"));

                pointFirstEveningTimeText.Text = "You'll enter \"Wind Down mode\".";
                pointSecondEveningTimeText.Text = "It's time to do your evening habits and get ready to sleep.";
                pointThirdEveningTimeText.Text = "Games and other websites are blocked.";
                pointFourthEveningTimeText.Text = "If you really need to keep using your computer, you can unlock it but we recommend sticking to your routine.";

                
                btnSoundsGoodTXT.Text = "Sounds good";
                btnChangeThisTXT.Text = "Change this";
            }
        }

        private void btnChangeThis_Click(object sender, RoutedEventArgs e)
        {
            _parentWindow.ShowOnboardingAskWorkTimeScreen();
        }

        private void btnSoundsGood_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("continue-confirm-evening-routine");
            _parentWindow.ShowOnboardingConfirmBreaksScreen();
        }
    }
}
