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
    /// Interaction logic for OnboardingConfirmBreaks.xaml
    /// </summary>
    public partial class OnboardingConfirmBreaks : Page
    {
        private OnBoardingWindow _parentWindow;
        AppSettings appSettings = AppSettings.GetInstance;
        public OnboardingConfirmBreaks()
        {

        }
        public OnboardingConfirmBreaks(OnBoardingWindow parentWindow) : this()
        {
            InitializeComponent();

            _parentWindow = parentWindow;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["title"].ToString();
                
                RememberBreaks.Text = appSettings.ConfigFileStrings["takeBreaksInfoVcInfo"]["subTitle"].ToString();
                Warning.Text = appSettings.ConfigFileStrings["takeBreaksInfoVcInfo"]["warningText"].ToString();
               
                CareAboutBreaks.Text = appSettings.ConfigFileStrings["takeBreaksInfoVcInfo"]["titleHowMuchCare"].ToString();  
                SelectOne.Text = appSettings.ConfigFileStrings["takeBreaksInfoVcInfo"]["selectOneText"].ToString(); //"Select one that apply";
                DoNotCare.Content = appSettings.ConfigFileStrings["takeBreaksInfoVcInfo"]["buttonDontCareText"].ToString(); //"Meh - don't really care";
                NotCommitted.Content = appSettings.ConfigFileStrings["takeBreaksInfoVcInfo"]["buttonNotCommittedText"].ToString();  
                Committed.Content = appSettings.ConfigFileStrings["takeBreaksInfoVcInfo"]["buttonCommittedText"].ToString();  

                btnContinueTXT.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["buttonContinueText"].ToString();

                appSettings.CapturePostHog("take-breaks-info-screen-shown");

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingGoals() ex - " + ex.Message);
            }
           
            btnContinue.IsEnabled = false;
            CareDesc.Text = "";
        }

        public void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //Initialize with false
                appSettings.bAreBreaksEnabled = true;
 
                if (DoNotCare.IsChecked.Value)  
                {
                    appSettings.bAreBreaksEnabled = false;
                    appSettings.CapturePostHog("cares-about-breaks", "\"commitment-level-for-breaks\":\"do_not_care\"");
                }
                else if (NotCommitted.IsChecked.Value)  
                {
                    appSettings.bAreBreaksEnabled = false;
                    appSettings.CapturePostHog("cares-about-breaks", "\"commitment-level-for-breaks\":\"not_committed\"");
                }
                else if (Committed.IsChecked.Value)  
                {
                    appSettings.bAreBreaksEnabled = true;
                    appSettings.CapturePostHog("cares-about-breaks", "\"commitment-level-for-breaks\":\"committed\"");
                }
              

                //If the user hadclicked EXIT full screen we will take up the default values as breaks enabled
                if (appSettings.bExitFullScreenClickedWhileOnboarding)
                {   //taking the default values
                    appSettings.bAreBreaksEnabled = true; 
                }

                //will get habit packs only if the user is doing habits
                if (appSettings.bAreBreaksEnabled)
                { 
                    _parentWindow.ShowOnboardingBreakRemindersScreen();
                    return;
                }
                else
                {
                    _parentWindow.ShowOnboardingWelcomeScreen();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnContinue_Click ex - " + ex.Message);
            }
        }

        private void DoNotCare_Checked(object sender, RoutedEventArgs e)
        {
            CareDesc.Text = appSettings.ConfigFileStrings["takeBreaksInfoVcInfo"]["resultDisableBreaksText"].ToString();
            btnContinue.IsEnabled = true;
        }
        private void NotCommitted_Checked(object sender, RoutedEventArgs e)
        {
            CareDesc.Text = appSettings.ConfigFileStrings["takeBreaksInfoVcInfo"]["resultDisableBreaksText"].ToString();
            btnContinue.IsEnabled = true;
        }

        private void Committed_Checked(object sender, RoutedEventArgs e)
        {
            CareDesc.Text = appSettings.ConfigFileStrings["takeBreaksInfoVcInfo"]["resultEnableBreaksText"].ToString();
            btnContinue.IsEnabled = true;
        }


    }
}
