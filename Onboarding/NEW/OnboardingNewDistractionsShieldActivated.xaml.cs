using Focus_Bear.Onboarding.New;
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

namespace Focus_Bear.Onboarding.NEW
{
    /// <summary>
    /// Interaction logic for OnboardingNewDistractionsShieldActivated.xaml
    /// </summary>
    public partial class OnboardingNewDistractionsShieldActivated : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        private NewOnboardingStart _parentWindow;
        public OnboardingNewDistractionsShieldActivated()
        {
        }
        public OnboardingNewDistractionsShieldActivated(NewOnboardingStart parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            /*
              "tryBlockUrlVcInfo": {
                "titleShieldActivated": "Distraction Shield activated!",
                "descriptionTryBlockText": "Try it out - please attempt to access <BLOCKED_URL> through your web browser. It should block the site.",
                "buttonItBlockedText": "It blocked it!",
                "buttonDidNotBlockedText": "It didn't block it 😒",
    
                  */
            try
            {
                Heading.Text = appSettings.ConfigFileStrings["tryBlockUrlVcInfo"]["titleShieldActivated"].ToString();
                Welcome.Text = appSettings.ConfigFileStrings["tryBlockUrlVcInfo"]["descriptionTryBlockText"].ToString();
                if(appSettings.arrAlwaysBlockedURLs.Count > 0)
                    Welcome.Text = appSettings.ConfigFileStrings["tryBlockUrlVcInfo"]["descriptionTryBlockText"].ToString().Replace("<BLOCKED_URL>", appSettings.arrAlwaysBlockedURLs[0]);
                btnDidntBlockedText.Text = appSettings.ConfigFileStrings["tryBlockUrlVcInfo"]["buttonDidNotBlockedText"].ToString();  
                btnItBlockedText.Text = appSettings.ConfigFileStrings["tryBlockUrlVcInfo"]["buttonItBlockedText"].ToString(); 
               
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingNewDistractionsShieldActivated()k EX -" + ex.Message);
            }

        }
   
        private void btnDidntBlocked_Click(object sender, RoutedEventArgs e)
        {
            _parentWindow.ShowWhichBrowserScreen();
        }

        private void btnItBlocked_Click(object sender, RoutedEventArgs e)
        {
            appSettings.bBlockingWorkedWhileOnboarding = true;
            _parentWindow.ShowBuildingHabitsScreen();
        }
    }
}
