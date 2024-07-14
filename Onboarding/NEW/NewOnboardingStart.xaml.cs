using Focus_Bear.Models;
using Focus_Bear.Onboarding.NEW;
using Focus_Bear.Utils;
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
    }
}
