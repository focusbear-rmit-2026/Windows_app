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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Diagnostics;
using Focus_Bear.Utils;
using System.Windows.Threading;
using Focus_Bear.Models;
using System.Windows.Interop;

namespace Focus_Bear.Onboarding
{
   

    /// <summary>
    /// Interaction logic for WelcomeAbord.xaml
    /// </summary>
    public partial class WelcomeAbord : Window
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
        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);
        int nPostponeTime;
        bool bShutdownApp = true;
        public WelcomeAbord()
        {
           InitializeComponent();
            nPostponeTime = 0;
        
            Welcome.Text = appSettings.ConfigFileStrings["welcomeOnboardingVcInfo"]["subTitle"].ToString();

            Descriptiopn1.Text = appSettings.ConfigFileStrings["welcomeOnboardingVcInfo"]["descriptionFirstText"].ToString(); //"Looking forward to helping you form healthy habits and get more deep work done. The onboarding process takes 5 minutes and requires 100% attention from you.";            
            Descriptiopn2.Text = appSettings.ConfigFileStrings["welcomeOnboardingVcInfo"]["descriptionSecondText"].ToString(); //"You won't be able to use other apps during the onboarding. Do you have time now or shall we do it a bit later?";

            btnABitLaterPleaseText.Text = appSettings.ConfigFileStrings["welcomeOnboardingVcInfo"]["buttonLaterText"].ToString(); //A bit later please",
            buttonLetsDoItText.Text = appSettings.ConfigFileStrings["welcomeOnboardingVcInfo"]["buttonLetsDoItText"].ToString();// "Let's do it",
            Copyright.Text = appSettings.ConfigFileStrings["welcomeOnboardingVcInfo"]["allRightsReservedText"].ToString();// "© 2022 Focus Bear, All Rights Reserved."
        }

        private void btnLetsDoIt_Click(object sender, RoutedEventArgs e)
        {
            FBMessageBox ff = new FBMessageBox( "",appSettings.ConfigFileStrings["welcomeOnboardingVcInfo"]["confirmText"].ToString(), MessageBoxButton.YesNo, appSettings.ConfigFileStrings["welcomeOnboardingVcInfo"]["buttonConfirmLetsDoItText"].ToString(), appSettings.ConfigFileStrings["welcomeOnboardingVcInfo"]["buttonNeedMoreTimeText"].ToString());
            ff.Owner = this;
            GridShade.Visibility = Visibility.Visible;
            ff.ShowDialog();
            GridShade.Visibility = Visibility.Collapsed;
            if (ff.btnClicked == MessageBoxResult.Yes)
            {
                bShutdownApp = false;
                RegistryManager.SetWelcomeAboardingDoneStatus();

                ((App)(Application.Current)).ShowOnBoradingFlow();
               
                Close();
            }
            else
            {
                WelcomeAbordPostpone wndPostpone = new WelcomeAbordPostpone();
                wndPostpone.ShowDialog();
            }
        }

        private void btnABitLaterPlease_Click(object sender, RoutedEventArgs e)
        {
            WelcomeAbordPostpone wndPostpone = new WelcomeAbordPostpone();
            wndPostpone.ShowDialog();
        }
        public void PostponeOnboarding(int nMinutesToPostpone)
        {
            //Hide this window, Set a timer for x nMinutesToPostpone and Hide the window and reappear again
            
            nPostponeTime = nMinutesToPostpone;

            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
#if DEBUG
            dispatcherTimer.Interval = new TimeSpan(0, 0, nMinutesToPostpone);
#else
             dispatcherTimer.Interval = new TimeSpan(0, nMinutesToPostpone, 0);
#endif
            dispatcherTimer.Start();
            
            Hide();

            CommandManager.InvalidateRequerySuggested();
        }
        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            CommandManager.InvalidateRequerySuggested();
 
            try
            {
                dispatcherTimer.Stop();
                Show();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            
        }


        private void GridShade_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {

        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if(bShutdownApp)
                Application.Current.Shutdown(0);
        }

    }
}
