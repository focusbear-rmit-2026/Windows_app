using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Focus_Bear.Onboarding.AfterLogin
{
    /// <summary>
    /// Interaction logic for OnboardingConfirmForAI.xaml
    /// </summary>
    public partial class OnboardingConfirmForAI : System.Windows.Controls.Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private OnboardingSetupAndTipsWindow _parentWindow;
        public OnboardingConfirmForAI()
        {

        }
        public OnboardingConfirmForAI(OnboardingSetupAndTipsWindow parentWindow)
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

            _parentWindow = parentWindow;

            LogWriter.LogWrite("OnboardingConfirmForAI screen");

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["aiPermissionVcInfo"]["title"].ToString();
                Description.Text = appSettings.ConfigFileStrings["aiPermissionVcInfo"]["descriptionText"].ToString();

                btnNoThanksTXT.Text = appSettings.ConfigFileStrings["aiPermissionVcInfo"]["buttonCancelText"].ToString();
                btnOKTXT.Text = appSettings.ConfigFileStrings["common"]["buttonOkText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Onboarding set work type () EX -" + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;
        }
         
        private void btnNoThanks_Click(object sender, RoutedEventArgs e)
        {
            FBMessageBox ff = new FBMessageBox("", appSettings.ConfigFileStrings["aiPermissionVcInfo"]["titleAlertText"].ToString());
            ff.Owner = _parentWindow;
            _parentWindow.GridShade.Visibility = Visibility.Visible;
            ff.ShowDialog();
            _parentWindow.GridShade.Visibility = Visibility.Collapsed;

            appSettings.bAiCheckUrlEnabled = false;

            _parentWindow.ShowFocusTipsScreen();
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            FBMessageBox ff = new FBMessageBox("", appSettings.ConfigFileStrings["aiPermissionVcInfo"]["titleAlertText"].ToString());
            ff.Owner = _parentWindow;
            _parentWindow.GridShade.Visibility = Visibility.Visible;
            ff.ShowDialog();
            _parentWindow.GridShade.Visibility = Visibility.Collapsed;


            appSettings.bAiCheckUrlEnabled = true;

            _parentWindow.ShowFocusTipsScreen();
        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }
    }
}
