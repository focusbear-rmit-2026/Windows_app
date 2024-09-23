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
    /// Interaction logic for OnboardingAchieveWithFocusBear.xaml
    /// </summary>
    public partial class OnboardingAchieveWithFocusBear : Page
    {
        private OnBoardingWindow _parentWindow;
        AppSettings appSettings = AppSettings.GetInstance;
        public OnboardingAchieveWithFocusBear()
        {

        }
        public OnboardingAchieveWithFocusBear(OnBoardingWindow parentWindow) : this()
        {
            InitializeComponent();

            _parentWindow = parentWindow;

            //todo win texts
            Heading.Text = "What do you want to achieve with Focus Bear?";//appSettings.ConfigFileStrings["askAppWorkVcInfo"]["title"].ToString();
            Description.Text = "How do you want your life to change from using Focus Bear?";
            btnContinueTXT.Text = "Continue"; //appSettings.ConfigFileStrings["askAppWorkVcInfo"]["buttonContinueText"].ToString();
        }

        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            string sParam = string.Format("\"customGoal\": \"{0}\"", TxtAchieve.Text);
            appSettings.CapturePostHog("custom-goal-selected", sParam);

            _parentWindow.ShowOnboardingGoalsScreen();
        }
    }
}
