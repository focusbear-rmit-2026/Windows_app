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

namespace Focus_Bear.Onboarding.AfterLogin
{
    /// <summary>
    /// Interaction logic for OnboardingSetupAndTipsWindow.xaml
    /// </summary>
    public partial class OnboardingSetupAndTipsWindow : Window
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
        
        public OnboardingSetupAndTipsWindow()
        {
            InitializeComponent();

            if(appSettings.arrAlwaysBlockedURLs.Count > 0)
            {
                ShowTypeOfWorkScreen();
            }
            else
            {
                FrameSteps.Navigate(new OnboardingDistractingSites(this));
            }
           

        }

        public void ShowTypeOfWorkScreen()
        {
            FrameSteps.Navigate(new OnboardingSetWorkType(this));
        } 
        public void ShowFocusModeAppsSetup()
        {
            FrameSteps.Navigate(new OnboardingSetupFocusApps(this));
        }
        public void ShowSetupFocusURLsScreen()
        {
            FrameSteps.Navigate(new OnboardingSetupFocusUrls(this));
        }
        public void ShowFocusTipsScreen()
        {
            FrameSteps.Navigate(new OnboardingSetupFocusTips(this)); //also includes how did you hear about focus bear screen
        }

    }
}
