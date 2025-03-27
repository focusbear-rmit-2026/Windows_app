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
            appSettings.bBlockWhileOnbording = false;

            FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["tryBlockUrlVcInfo"]["emailUsText"].ToString());
            ff.Owner = _parentWindow;
            _parentWindow.GridShade.Visibility = Visibility.Visible;
            ff.ShowDialog();
            _parentWindow.GridShade.Visibility = Visibility.Collapsed;

            appSettings.bBlockingWorkedWhileOnboarding = false;
           _parentWindow.ShowBuildingHabitsScreen();

            appSettings.CapturePostHog("button-still-not-working-clicked");
        }

        private void btnItWorked_Click(object sender, RoutedEventArgs e)
        {
            appSettings.bBlockWhileOnbording = false;

            appSettings.bBlockingWorkedWhileOnboarding = true;
            _parentWindow.ShowBuildingHabitsScreen();

            appSettings.CapturePostHog("button-just-tried-those-browsers-clicked");
        }
    }
}
