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
           
            TypeOfWork.Text = appSettings.ConfigFileStrings["common"]["letsCreatAccountText"].ToString();
 
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
