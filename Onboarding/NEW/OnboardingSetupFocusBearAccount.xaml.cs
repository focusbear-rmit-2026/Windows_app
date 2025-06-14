using Focus_Bear.Models;
using Focus_Bear.Onboarding.New;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Focus_Bear.Onboarding.NEW
{
    /// <summary>
    /// Interaction logic for OnboardingSetupFocusBearAccount.xaml
    /// </summary>
    public partial class OnboardingSetupFocusBearAccount : System.Windows.Controls.Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private NewOnboardingStart _parentWindow;

        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);
        int nPostponeTime;

        public OnboardingSetupFocusBearAccount()
        {
        }
        public OnboardingSetupFocusBearAccount(NewOnboardingStart parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["askAccountVcInfo"]["title"].ToString();
                Description.Text = appSettings.ConfigFileStrings["askAccountVcInfo"]["descriptionText"].ToString();// appSettings.ConfigFileStrings["askAccountVcInfo"]["descriptionText"].ToString();
                AccountCost.Text = appSettings.ConfigFileStrings["askAccountVcInfo"]["tipText"].ToString();
                
                btnNotRightNowText.Text = appSettings.ConfigFileStrings["askAccountVcInfo"]["buttonNotRightNowText"].ToString();  
                btnSignupText.Text = appSettings.ConfigFileStrings["onboardingPrivacyTipVcInfo"]["buttonContinueText"].ToString(); 
                 
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingSetupFocusBearAccount()k EX -" + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;
        }
         
 
        private void btnNotRightNow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FBMessageBox ff = new FBMessageBox("", appSettings.ConfigFileStrings["askAccountVcInfo"]["alertText"].ToString() );
                ff.Owner = _parentWindow;
                _parentWindow.GridShade.Visibility = Visibility.Visible;
                ff.ShowDialog();
                _parentWindow.GridShade.Visibility = Visibility.Collapsed;

                _parentWindow.WindowState = WindowState.Minimized;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnNoThanks_Click + " + ex.Message);
            }
        }

        private void btnSignup_Click(object sender, RoutedEventArgs e)
        {
            //Here we need to show the login window but we need to pass through the way of the onblarding flow so that all APIs are called and defaults are selected
            //Show we will simulate loading of onboarding flow but will not show the window and hit the please spare me window
            //Lastly that will show the login window

            //mj//((App)(Application.Current)).ShowOnBoradingFlow(true);
            ((App)(Application.Current)).ShowLoginWindow();
            _parentWindow.bShutdownApp = false;
            _parentWindow.Close();
        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }

    }
}
