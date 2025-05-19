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
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Focus_Bear.Onboarding.NEW
{
    /// <summary>
    /// Interaction logic for OnboardingNewBlockingSchedule.xaml
    /// </summary>
    public partial class OnboardingNewBlockingSchedule : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private NewOnboardingStart _parentWindow;

        public OnboardingNewBlockingSchedule()
        {
        }
        public OnboardingNewBlockingSchedule(NewOnboardingStart parentWindow)
        {
            InitializeComponent();

            _parentWindow = parentWindow;
            btnContinue.IsEnabled = false;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["onboardingBlockingScheduleVcInfo"]["title"].ToString();

                Welcome.Text = appSettings.ConfigFileStrings["onboardingBlockingScheduleVcInfo"]["subTitle"].ToString();
                RadioButton1.Content = appSettings.ConfigFileStrings["onboardingBlockingScheduleVcInfo"]["radioBtnFirstText"].ToString();
                RadioButton2.Content = appSettings.ConfigFileStrings["onboardingBlockingScheduleVcInfo"]["radioBtnSecondText"].ToString();
                RadioButton3.Content = appSettings.ConfigFileStrings["onboardingBlockingScheduleVcInfo"]["radioBtnThirdText"].ToString();

                Description1.Text = appSettings.ConfigFileStrings["onboardingBlockingScheduleVcInfo"]["descriptionFirstText"].ToString();
                Description2.Text = appSettings.ConfigFileStrings["onboardingBlockingScheduleVcInfo"]["descriptionSecondText"].ToString();
                Description3.Text = appSettings.ConfigFileStrings["onboardingBlockingScheduleVcInfo"]["descriptionThirdText"].ToString();

                btnContinueText.Text = appSettings.ConfigFileStrings["common"]["buttonContinueText"].ToString();// "continue",

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingNewBlockingSchedule() text EX -" + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;
        }

        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            appSettings.bBlockWhileOnbording = true;

            if (RadioButton1.IsChecked.Value)
            {
                appSettings.nBlockingSchedule = BLOCKINGSCHEDULE.UNTIL_I_FINISH_WORK;
                appSettings.CapturePostHog("choose-focus-schedule", "\"schedule-type\":\"until_i_finish_work\"");
            }
            else if(RadioButton2.IsChecked.Value)
            {
                appSettings.nBlockingSchedule = BLOCKINGSCHEDULE.WHEN_STARTING_FOCUS_SESSION;
                appSettings.CapturePostHog("choose-focus-schedule", "\"schedule-type\":\"when_i_tell_you_to\"");
            }
            _parentWindow.ShowPrivacyScreen();
        }

        private void RadioButton1_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = true;
        }

        private void RadioButton2_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = true;
        }

        private void RadioButton3_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("choose-focus-schedule","\"schedule-type\":\"on_a_custom_schedule\"");
            
            Description3.Visibility = Visibility.Visible;
            btnContinue.IsEnabled = false;
        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }
    }
}
