using Focus_Bear.Models;
using Focus_Bear.Onboarding.NEW;
using Focus_Bear.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
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

namespace Focus_Bear.Onboarding.New
{
    /// <summary>
    /// Interaction logic for NewOnboardingStart.xaml
    /// </summary>
    public partial class NewOnboardingStart : Window
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
        }

        AppSettings appSettings = AppSettings.GetInstance;
        public OnboardingNewBuildingHabits pageBuildHabits = null;

        public bool bShutdownApp = true;
        public NewOnboardingStart()
        {
            InitializeComponent();
            FrameSteps.Navigate(new OnboardingNewWelcome(this));
            this.Loaded += MainWindow_Loaded;
            App.ThemeChanged += TitleBarThemeChange;
        }

        public void ShowDistractionsScreen()
        {
            FrameSteps.Navigate(new OnboardingDistractingSites(this));
        } 
        public void ShowFocusModeAppsSetup()
        {
          
        }
        public void ShowSetupFocusURLsScreen()
        {
            
        }
        public void ShowFocusTipsScreen()
        {
            //also includes how did you hear about focus bear screen
            //In new onboarding we are only using the screen that asks where did you heard from FB and a video desc screen
        }
        public void ShowLoginScreen()
        {
            RegistryManager.SetWelcomeAboardingDoneStatus();

            ((App)(Application.Current)).ShowLoginWindow();
            bShutdownApp = false;
            Close();
        }
        public void ShowBlockingScheduleScreen()
        {
            appSettings.CapturePostHog("onboarding-blocking-schedule-shown");
            FrameSteps.Navigate(new OnboardingNewBlockingSchedule(this));
        }
        public void ShowPrivacyScreen()
        {
            appSettings.CapturePostHog("privacy-and-security-shown");
            FrameSteps.Navigate(new OnboardingNewPrivacy(this));
        }
        public void ShowDistractionsShieldActivatedScreen()
        {
            FrameSteps.Navigate(new OnboardingNewDistractionsShieldActivated(this));
        }
        public void ShowWhichBrowserScreen()
        {
            FrameSteps.Navigate(new OnboardingNewWhichBrowser(this));
        }
        public void ShowBuildingHabitsScreen()
        {
            pageBuildHabits = new OnboardingNewBuildingHabits(this);
            FrameSteps.Navigate(pageBuildHabits );
        }
        public void ShowSetupFocusBearAccount()
        {
            FrameSteps.Navigate(new OnboardingSetupFocusBearAccount(this) );
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (bShutdownApp) //If the user closes the app window, we need to shutdown
                Application.Current.Shutdown(0);
        }
        private void TitleBarThemeChange()
        {
            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
            {
                UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
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
            ((App)Application.Current).ChangeTheme(selectedTheme);
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
            UseImmersiveDarkMode(hwndSource.Handle, !isLightTheme);
        }
        private static bool IsLightTheme()
        {
            var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i > 0;
        }
        private static bool UseImmersiveDarkMode(IntPtr handle, bool enabled)
        {
            Version osVersion = Environment.OSVersion.Version;
            bool isWindows10OrGreater = (osVersion.Major == 10 && osVersion.Build >= 17763) || osVersion.Major > 10;

            if (isWindows10OrGreater)
            {
                var attribute = 20;
                if (osVersion.Build >= 18985)
                {
                    attribute = 19;
                }

                int useImmersiveDarkMode = enabled ? 1 : 0;
                return DwmSetWindowAttribute(handle, attribute, ref useImmersiveDarkMode, sizeof(int)) == 0;
            }

            return false;
        }
    }
}
