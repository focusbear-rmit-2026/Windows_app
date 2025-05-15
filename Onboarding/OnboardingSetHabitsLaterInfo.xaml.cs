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
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Focus_Bear.Onboarding
{
    /// <summary>
    /// Interaction logic for OnboardingSetHabitsLaterInfo.xaml
    /// </summary>
    public partial class OnboardingSetHabitsLaterInfo : Page
    {
        private OnBoardingWindow _parentWindow;
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        public OnboardingSetHabitsLaterInfo()
        {
            
        }
        public OnboardingSetHabitsLaterInfo(OnBoardingWindow parentWindow) : this()
        {
            InitializeComponent();

            _parentWindow = parentWindow;
            try
            {
                Heading.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["title"].ToString();
                Desc.Text = "If you ever decide you want to activate habits in the future, you can open Preferences from the menu bar and click Edit Habits"; //todo win texts
                btnContinueTXT.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["buttonContinueText"].ToString();

                //Let's set the intial startup time / shutoff time when no habits are there so defaulting to maximum time
                appSettings.InitialStartupTimeValue = Convert.ToDateTime("00:00 AM");
                Console.WriteLine(appSettings.InitialStartupTimeValue.ToString("HH:mm"));

                appSettings.InitialShutoffTimeValue = Convert.ToDateTime("11:59 PM");
                Console.WriteLine(appSettings.InitialShutoffTimeValue.ToString("HH:mm"));

                appSettings.InitialSleepTimeValue = Convert.ToDateTime("11:59 PM");
                Console.WriteLine(appSettings.InitialSleepTimeValue.ToString("HH:mm"));
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingSetHabitsLaterInfo() ex - " + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;
        }
        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            //mj//_parentWindow.ShowMindFullBreathingFinished();//this will handle to show only setting account info
            _parentWindow.HideFrameAndClose();
        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }
    }
}
