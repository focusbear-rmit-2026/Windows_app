using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
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
using System.Windows.Shapes;

namespace Focus_Bear.Onboarding.AfterLogin
{
    /// <summary>
    /// Interaction logic for OnboardingNotesWindow.xaml
    /// </summary>
    public partial class OnboardingNotesWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [DllImport("user32.dll")]
        private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

        private const uint MF_BYCOMMAND = 0x00000000;
        private const uint MF_GRAYED = 0x00000001;
        private const uint SC_CLOSE = 0xF060;
        private const int WM_SHOWWINDOW = 0x00000018;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hWnd = new WindowInteropHelper(this);
            var sysMenu = GetSystemMenu(hWnd.Handle, false);
            EnableMenuItem(sysMenu, SC_CLOSE, MF_BYCOMMAND | MF_GRAYED);
        }

        AppSettings appSettings = AppSettings.GetInstance;

        int nStep = 1;
        public OnboardingNotesWindow()
        {
            LogWriter.LogWrite("OnboardingNotesWindow()");

            InitializeComponent();
            try
            {
                HeadingHear.Text = appSettings.ConfigFileStrings["howHeardVcInfo"]["title"].ToString();
                btnContinueTxt.Text = appSettings.ConfigFileStrings["howHeardVcInfo"]["buttonContinueText"].ToString();
                btnContinue.IsEnabled = false;

                GridHowDidYouHearAboutFocusBear.Visibility = Visibility.Visible;

                AccountReady.Text = appSettings.ConfigFileStrings["askOnboardingVideoVcInfo"]["title"].ToString();
                Desc.Text = appSettings.ConfigFileStrings["askOnboardingVideoVcInfo"]["subTitle"].ToString();
                btnPlayVideoTxt.Text = appSettings.ConfigFileStrings["askOnboardingVideoVcInfo"]["buttonPlayText"].ToString();
                btnNoThanksText.Text = appSettings.ConfigFileStrings["askOnboardingVideoVcInfo"]["buttonNoThanksText"].ToString();

                //todo win texts
                HeadingWantFeedback.Text = "We want your feedback"; //onboardingFeedbackVcInfo
                DescriptionWantFeedback.Text = "We're a small independent team, not a faceless mega-corporation. Focus Bear is still a new app and we really want your feedback. If you have suggestions, please email support@focusbear.io.";
                btnContinueFeedbackTxt.Text = appSettings.ConfigFileStrings["howHeardVcInfo"]["buttonContinueText"].ToString(); //appSettings.ConfigFileStrings["onboardingFeedbackVcInfo"]["buttonContinueText"].ToString();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingNotesWindow() EX + " + ex.Message);
            }
        }
        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            string ss = string.Format("\"heard-from\":\"{0}\"", TxtSomewhereElse.Text);
            appSettings.CapturePostHog("heard_about_focus_bear", ss);
             
            GridHowDidYouHearAboutFocusBear.Visibility = Visibility.Hidden;
            GridWantFeedback.Visibility = Visibility.Visible;
            appSettings.CapturePostHog("show_want_feedback_screen");
        }

        private void TxtSomewhereElse_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtSomewhereElse.Text.Length > 0)
            {
                btnContinue.IsEnabled = true;
            }
            else
            {
                btnContinue.IsEnabled = false;
            }
        }

        private void btnPlayVideo_Click(object sender, RoutedEventArgs e)
        {
            appSettings.bShowOnBoardingVideo = true;
            Close();
        }

        private void btnNoThanks_Click(object sender, RoutedEventArgs e)
        {
            appSettings.bShowOnBoardingVideo = false;
            Close();
        }

        private void btnContinueFeedback_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
