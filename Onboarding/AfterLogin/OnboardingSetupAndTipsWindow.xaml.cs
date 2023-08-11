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
         
        AppSettings appSettings = AppSettings.GetInstance;
        
        public OnboardingSetupAndTipsWindow()
        {
            InitializeComponent();

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }

            ShowTypeOfWorkScreen();
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
            FrameSteps.Navigate(new OnboardingSetupFocusTips(this)); 
        }
        public void ShowAIConfirmScreen()
        {
            FrameSteps.Navigate(new OnboardingConfirmForAI(this));
        }
    }
}
