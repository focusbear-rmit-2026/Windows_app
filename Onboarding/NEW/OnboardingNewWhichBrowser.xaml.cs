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
    /// Interaction logic for OnboardingNewWhichBrowser.xaml
    /// </summary>
    public partial class OnboardingNewWhichBrowser : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        private NewOnboardingStart _parentWindow;
        public OnboardingNewWhichBrowser()
        {
        }
        public OnboardingNewWhichBrowser(NewOnboardingStart parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            /*
               "titleWhichBrowser": "Oops! Which browser did you try?",
        "tryBrowsersBlockText": "Did you use Safari, Google Chrome, Sidekick, Brave Browser or Microsoft Edge?\nWe can only block if you use those browsers.",
        "buttonStillNotBlockedText": "Still not working",
        "buttonNowBlockedText": "I just tried those browsers and it worked!",
        "emailUsText": "Sorry about that 😓 Any chance you could email us (team@focusbear.io) and we'll try and fix it for you?"
    
                  */
            try
            {
                Heading.Text = appSettings.ConfigFileStrings["tryBlockUrlVcInfo"]["titleWhichBrowser"].ToString();
                btnDidntBlockedText.Text = appSettings.ConfigFileStrings["tryBlockUrlVcInfo"]["buttonStillNotBlockedText"].ToString();
                btnItWorkedText.Text = appSettings.ConfigFileStrings["tryBlockUrlVcInfo"]["buttonNowBlockedText"].ToString();
              
                Welcome.Text = appSettings.ConfigFileStrings["tryBlockUrlVcInfo"]["tryBrowsersBlockTextWIN"].ToString();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingNewWhichBrowser()k EX -" + ex.Message);
            }

        }
   
        private void btnDidntBlocked_Click(object sender, RoutedEventArgs e) //still not working!
        {
            FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["tryBlockUrlVcInfo"]["emailUsText"].ToString());
            ff.Owner = _parentWindow;
            _parentWindow.GridShade.Visibility = Visibility.Visible;
            ff.ShowDialog();
            _parentWindow.GridShade.Visibility = Visibility.Collapsed;

            appSettings.bBlockingWorkedWhileOnboarding = false;
           _parentWindow.ShowBuildingHabitsScreen();
        }

        private void btnItWorked_Click(object sender, RoutedEventArgs e)
        {
            appSettings.bBlockingWorkedWhileOnboarding = true;
            _parentWindow.ShowBuildingHabitsScreen();
        }
    }
}
