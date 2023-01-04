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
    /// Interaction logic for OnboardingWelcome.xaml
    /// </summary>
    public partial class OnboardingWelcome : Page
    {

        private OnBoardingWindow _parentWindow;
        AppSettings appSettings = AppSettings.GetInstance;

        public OnboardingWelcome()
        {

        }

        public OnboardingWelcome(OnBoardingWindow parentWindow) : this()
        {
            _parentWindow = parentWindow;
            InitializeComponent();

            Heading.Text = appSettings.ConfigFileStrings["welcomeInfo"]["welcomeMessageHeading"].ToString();
            Description.Text = appSettings.ConfigFileStrings["welcomeInfo"]["welcomeMessageIntro"].ToString();


            //    "startActivityButtonText":"Start <TIME> <NAME>",
            string Title = appSettings.ConfigFileStrings["activityInfo"]["startActivityButtonText"].ToString();
            string sName = appSettings.ConfigFileStrings["welcomeInfo"]["introActivities"][0]["name"].ToString();
            int nDurationSeconds = (int)appSettings.ConfigFileStrings["welcomeInfo"]["introActivities"][0]["duration_seconds"];

            Title = Title.Replace("<NAME>", sName);
            Title = Title.Replace("<TIME>", appSettings.SecondsToText(nDurationSeconds));
            //Prashant?

            btnStartMindfulBreathingTXT.Text = Title;
          
        }
        private void btnStartMindfulBreathing_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _parentWindow.ShowMindFullBreathing();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }
    }
}
