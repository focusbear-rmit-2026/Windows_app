using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// Interaction logic for OnboardingWorkType.xaml
    /// </summary>
    public partial class OnboardingWorkType : Page
    {
        private OnBoardingWindow _parentWindow = null;

        AppSettings appSettings = AppSettings.GetInstance;

        public OnboardingWorkType()
        {

        }
        public OnboardingWorkType(OnBoardingWindow parentWindow) : this()
        {
            _parentWindow = parentWindow;
            InitializeComponent();

            if (appSettings.bGoalHealthyHabits)
            {
                string sActivityName = appSettings.OnboardingActivityName;///appSettings.ConfigFileStrings["welcomeInfo"]["introActivities"][0]["name"].ToString();
                string Title = appSettings.ConfigFileStrings["activityInfo"]["activityFinishedText"].ToString();
                if (appSettings.bOnboardingActivitySkipped)
                {
                    Title = appSettings.ConfigFileStrings["activityInfo"]["activitySkippedText"].ToString();
                }
                else
                {
                    Title = appSettings.ConfigFileStrings["activityInfo"]["activityFinishedText"].ToString();
                }

                Title = Title.Replace("<ACTIVITY>", sActivityName);
                Heading.Text = Title.ToString();
            }
            else
            {
                Heading.Text = "";

                //Let's set the intial startup time / shutoff time
                appSettings.InitialStartupTimeValue = Convert.ToDateTime("00:00 AM");
                Console.WriteLine(appSettings.InitialStartupTimeValue.ToString("HH:mm"));

                appSettings.InitialShutoffTimeValue = Convert.ToDateTime("11:59 PM");
                Console.WriteLine(appSettings.InitialShutoffTimeValue.ToString("HH:mm"));

                appSettings.InitialSleepTimeValue = Convert.ToDateTime("11:59 PM");
                Console.WriteLine(appSettings.InitialSleepTimeValue.ToString("HH:mm"));

            }
            //"Now let's set up an account for you"
            TypeOfWork.Text = appSettings.ConfigFileStrings["common"]["stayFocusedText"].ToString(); //stay focused
            
            btnContinueTxt.Text = appSettings.ConfigFileStrings["preloadFocusModesViewInfo"]["buttonContinueText"].ToString();
            btnContinue.IsEnabled = true;

        }

      
        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _parentWindow.HideFrameAndClose();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }
    }
}
