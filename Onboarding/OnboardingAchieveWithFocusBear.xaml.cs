using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Focus_Bear.Onboarding
{
    /// <summary>
    /// Interaction logic for OnboardingAchieveWithFocusBear.xaml
    /// </summary>
    public partial class OnboardingAchieveWithFocusBear : System.Windows.Controls.Page
    {
        private OnBoardingWindow _parentWindow;
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        public OnboardingAchieveWithFocusBear()
        {

        }
        public OnboardingAchieveWithFocusBear(OnBoardingWindow parentWindow) : this()
        {
            InitializeComponent();

            _parentWindow = parentWindow;

            //todo win texts
            Heading.Text = appSettings.ConfigFileStrings["achieveWithFocusBearVcInfo"]["title"].ToString(); //"What do you want to achieve with Focus Bear?"
            Description.Text = appSettings.ConfigFileStrings["achieveWithFocusBearVcInfo"]["subTitle"].ToString(); //"How do you want your life to change from using Focus Bear?";
            btnContinueTXT.Text = appSettings.ConfigFileStrings["achieveWithFocusBearVcInfo"]["buttonContinueText"].ToString(); //"Continue";
            InfoChange.ToolTip = "We’ll save this as a long term goal for you - it will also help us understand what you’re expecting so we can improve the app"; //todo win texts
            Page_Theme();
            App.ThemeChanged += Page_Theme;
        }

        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            string sParam = string.Format("\"customGoal\": \"{0}\"", TxtAchieve.Text);
            appSettings.CapturePostHog("custom-goal-selected", sParam);
            appSettings.sCustomGoalSelected = TxtAchieve.Text;
            _parentWindow.ShowOnboardingGoalsScreen();
        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this, BackgroundImage);
        }
    }
}
