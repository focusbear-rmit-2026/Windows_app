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


                var arrOptions = new string[] { appSettings.ConfigFileStrings["howHeardVcInfo"]["optionFirstText"].ToString(), appSettings.ConfigFileStrings["howHeardVcInfo"]["optionSecondText"].ToString(), appSettings.ConfigFileStrings["howHeardVcInfo"]["optionThirdText"].ToString(), appSettings.ConfigFileStrings["howHeardVcInfo"]["optionRemarkableText"].ToString() };
                Random rnd = new Random();
                string[] MyRandomArray = arrOptions.OrderBy(x => rnd.Next()).ToArray();

                Radio1.Content = MyRandomArray[0];
                Radio2.Content = MyRandomArray[1];
                Radio3.Content = MyRandomArray[2];
                Radio4.Content = MyRandomArray[3];

                SomewhereElse.Content = appSettings.ConfigFileStrings["howHeardVcInfo"]["optionFourthText"].ToString();

                GridHowDidYouHearAboutFocusBear.Visibility = Visibility.Visible;

                AccountReady.Text = appSettings.ConfigFileStrings["askOnboardingVideoVcInfo"]["title"].ToString();
                Desc.Text = appSettings.ConfigFileStrings["askOnboardingVideoVcInfo"]["subTitle"].ToString();
                btnPlayVideoTxt.Text = appSettings.ConfigFileStrings["askOnboardingVideoVcInfo"]["buttonPlayText"].ToString();
                btnNoThanksText.Text = appSettings.ConfigFileStrings["askOnboardingVideoVcInfo"]["buttonNoThanksText"].ToString();



            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingNotesWindow() EX + " + ex.Message);
            }
        }
        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            if (Radio1.IsChecked.Value)
            {
                string ss = string.Format("\"heard-from\":\"{0}\"", Radio1.Content.ToString());
                appSettings.CapturePostHog("heard_about_focus_bear", ss);

            }
            else if (Radio2.IsChecked.Value)
            {
                string ss = string.Format("\"heard-from\":\"{0}\"", Radio2.Content.ToString());
                appSettings.CapturePostHog("heard_about_focus_bear", ss);
            }
            else if (Radio3.IsChecked.Value)
            {
                string ss = string.Format("\"heard-from\":\"{0}\"", Radio3.Content.ToString());
                appSettings.CapturePostHog("heard_about_focus_bear", ss);
            }
            else if (Radio4.IsChecked.Value)
            {
                string ss = string.Format("\"heard-from\":\"{0}\"", Radio4.Content.ToString());
                appSettings.CapturePostHog("heard_about_focus_bear", ss);
            }
            else if (SomewhereElse.IsChecked.Value)
            {
                string ss = string.Format("\"heard-from\":\"{0}\"", TxtSomewhereElse.Text);
                appSettings.CapturePostHog("heard_about_focus_bear", ss);
            }

            //6 Sep 2023 - Once the user clicks contionue, now we do not need to show the video confirmation screen, as we will be opening the post install page
            appSettings.bShowOnBoardingVideo = false;
            Close();
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

        private void SomewhereElse_Checked(object sender, RoutedEventArgs e)
        {
            TxtSomewhereElse.Visibility = Visibility.Visible;
            if (TxtSomewhereElse.Text.Length > 0)
            {
                btnContinue.IsEnabled = true;
            }
            else
            {
                btnContinue.IsEnabled = false;
            }
        }

        private void Radio3_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = true;
            TxtSomewhereElse.Visibility = Visibility.Collapsed;
        }

        private void Radio2_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = true;
            TxtSomewhereElse.Visibility = Visibility.Collapsed;
        }

        private void Radio1_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = true;
            TxtSomewhereElse.Visibility = Visibility.Collapsed;
        }

        private void Radio4_Checked(object sender, RoutedEventArgs e)
        {
            btnContinue.IsEnabled = true;
            TxtSomewhereElse.Visibility = Visibility.Collapsed;
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
    }
}
