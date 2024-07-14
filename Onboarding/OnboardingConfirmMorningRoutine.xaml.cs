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
    /// Interaction logic for OnboardingConfirmMorningRoutine.xaml
    /// </summary>
    public partial class OnboardingConfirmMorningRoutine : Page
    {
        private OnBoardingWindow _parentWindow;
        AppSettings appSettings = AppSettings.GetInstance;
        public OnboardingConfirmMorningRoutine()
        {
        }
        public OnboardingConfirmMorningRoutine(OnBoardingWindow parentWindow) : this()
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            LoadTexts();
        }

        public void LoadTexts()
        {
            try
            {
                Heading.Text = appSettings.ConfigFileStrings["confirmMorningRoutineTimeVcInfo"]["title"].ToString();
                ToBeClearDesc.Text = appSettings.ConfigFileStrings["confirmMorningRoutineTimeVcInfo"]["subTitle"].ToString();

                string sStartTime = appSettings.ConfigFileStrings["confirmMorningRoutineTimeVcInfo"]["timeText"].ToString();
                int nStartDiff = -1 * (((15 * 60) + appSettings.nMorningRoutineDuration) / 60);
                FromTime.Text = sStartTime.Replace("<TIME>", appSettings.InitialStartupTimeValue.AddMinutes(nStartDiff).ToString("hh:mm tt"));

                Point1MorningTimeText.Text = appSettings.ConfigFileStrings["confirmMorningRoutineTimeVcInfo"]["pointFirstMorningTimeText"].ToString();
                Point2MorningTimeText.Text = appSettings.ConfigFileStrings["confirmMorningRoutineTimeVcInfo"]["pointSecondMorningTimeText"].ToString();
                Point3MorningTimeText.Text = appSettings.ConfigFileStrings["confirmMorningRoutineTimeVcInfo"]["pointThirdMorningTimeText"].ToString();
                Point4MorningTimeText.Text = appSettings.ConfigFileStrings["confirmMorningRoutineTimeVcInfo"]["pointFourthMorningTimeText"].ToString();

                btnChangeThisTXT.Text = appSettings.ConfigFileStrings["confirmMorningRoutineTimeVcInfo"]["buttonReviewText"].ToString();
                btnSoundsGoodTXT.Text = appSettings.ConfigFileStrings["confirmMorningRoutineTimeVcInfo"]["buttonContinueText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingAskWorkTime() ex - " + ex.Message); //Todo Win Texts

                Heading.Text = "Confirm your morning routine";
                ToBeClearDesc.Text = "To be super clear, here's what's going to happen in the morning.";
                string sStartTime = "From <TIME>:";
                int nStartDiff = -1 * (((15 * 60) + appSettings.nMorningRoutineDuration) / 60);
                FromTime.Text = sStartTime.Replace("<TIME>", appSettings.InitialStartupTimeValue.AddMinutes(nStartDiff).ToString("hh:mm tt"));

                Point1MorningTimeText.Text = "You'll do your \"Morning routine\".";
                Point2MorningTimeText.Text = "While you're doing your habits, your computer will be locked.";
                Point3MorningTimeText.Text = "(You can unlock it if you have an emergency).";
                Point4MorningTimeText.Text = "After you finish your habits, your computer will be unlocked and Focus Bear will help you avoid distractions during your workday.";
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
            appSettings.CapturePostHog("continue-confirm-morning-routine");
            _parentWindow.ShowOnbordingConfirmEveningRoutine();
        }
    }
}
