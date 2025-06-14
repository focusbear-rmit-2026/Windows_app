using Focus_Bear.Models;
using Focus_Bear.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI;
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
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

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

            var source = (HwndSource)PresentationSource.FromVisual(this);
            var isLightTheme = app.IsLightTheme();
            if (appSettings.bAutoTheme)
            {
                if (source != null)
                {
                    app.UseImmersiveDarkMode(source.Handle, !isLightTheme);
                }
            }
            else
            {
                if (appSettings.sTheme == "Light")
                {
                    app.UseImmersiveDarkMode(source.Handle, false);
                }
                else if (appSettings.sTheme == "Dark")
                {
                    app.UseImmersiveDarkMode(source.Handle, true);
                }
            }
        }

        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;

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

                HeadingWantFeedback.Text = appSettings.ConfigFileStrings["onboardingFeedbackVcInfo"]["title"].ToString();
                DescriptionWantFeedback.Text = appSettings.ConfigFileStrings["onboardingFeedbackVcInfo"]["subTitle"].ToString();
                btnContinueFeedbackTxt.Text = appSettings.ConfigFileStrings["onboardingFeedbackVcInfo"]["buttonContinueText"].ToString();


            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingNotesWindow() EX + " + ex.Message);
            }
            this.Loaded += MainWindow_Loaded;
            App.ThemeChanged += TitleBarThemeChange;
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
        private void TitleBarThemeChange()
        {
            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
            {
                app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }
            if (appSettings.sTheme == "Light")
            {
                this.Background = new SolidColorBrush(Colors.White);
            }
            else if (appSettings.sTheme == "Dark")
            {
                this.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
            }
        }
        private IntPtr WndProctheme(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            int WM_SETTINGCHANGE = 0x1A;
            if (msg == WM_SETTINGCHANGE)
            {
                // Apply theme when system settings change
                ChangeThemeByWindows();
            }
            return IntPtr.Zero;
        }
        private void ChangeThemeByWindows()
        {
            bool isDarkMode = IsLightTheme();
            string theme = isDarkMode ? "Light" : "Dark";
            LogWriter.LogWrite("NewOnboarding detect theme changed WndProctheme" + theme);
            string selectedTheme = appSettings.sTheme;
            LogWriter.LogWrite("NewOnboarding report autotheme status" + appSettings.bAutoTheme);
            if (isDarkMode == true && appSettings.bAutoTheme == true)
            {
                selectedTheme = "Light";
            }
            else if (isDarkMode == false && appSettings.bAutoTheme == true)
            {
                selectedTheme = "Dark";
            }
            TitleBarThemeChange();
            app.ChangeTheme(selectedTheme);
            RegistryManager.SetThemeMode();

        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Hook into Windows messages
            var hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            if (hwndSource != null)
            {
                hwndSource.AddHook(WndProctheme);
            }

            // Apply the correct theme at startup
            ChangeThemeByWindows();
            var isLightTheme = IsLightTheme();
            app.UseImmersiveDarkMode(hwndSource.Handle, !isLightTheme);
        }
        private static bool IsLightTheme()
        {
            var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i > 0;
        }
        //private static bool UseImmersiveDarkMode(IntPtr handle, bool enabled)
        //{
        //    Version osVersion = Environment.OSVersion.Version;
        //    bool isWindows10OrGreater = (osVersion.Major == 10 && osVersion.Build >= 17763) || osVersion.Major > 10;

        //    if (isWindows10OrGreater)
        //    {
        //        var attribute = 20;
        //        if (osVersion.Build >= 18985)
        //        {
        //            attribute = 19;
        //        }

        //        int useImmersiveDarkMode = enabled ? 1 : 0;
        //        return DwmSetWindowAttribute(handle, attribute, ref useImmersiveDarkMode, sizeof(int)) == 0;
        //    }

        //    return false;
        //}
    }
}
