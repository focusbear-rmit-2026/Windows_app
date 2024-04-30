using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Focus_Bear.Login
{
    /// <summary>
    /// Interaction logic for LoginHome.xaml
    /// </summary>
    public partial class LoginHome : Page
    {
        private LoginMain _parentWindow;
        AppSettings appSettings = AppSettings.GetInstance;
        public LoginHome()
        {
        }

        public LoginHome(LoginMain parentWindow) : this()
        {
            _parentWindow = parentWindow;
            InitializeComponent();

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["askLogInVcInfo"]["title"].ToString();
                Descriptiopn.Text = appSettings.ConfigFileStrings["askLogInVcInfo"]["messageText"].ToString();
                btnSignInText.Text = appSettings.ConfigFileStrings["askLogInVcInfo"]["buttonSignInText"].ToString();
                btnSignUpText.Text = appSettings.ConfigFileStrings["customLoginVcInfo"]["buttonCreateAccountText"].ToString();

                Suggest.Text = appSettings.ConfigFileStrings["askLogInVcInfo"]["descriptionText"].ToString();
                appSettings.CapturePostHog("pre-signin-view-shown");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("LoginHome set texts ex - " + ex.Message);
            }
        }
       
        private void btnSignIn_Click(object sender, RoutedEventArgs e)
        {
            _parentWindow.ShowSignInWindow();
        }

        private void btnSignUp_Click(object sender, RoutedEventArgs e)
        {
            _parentWindow.ShowSignUpWindow();
        }

        private void btnInfo_Click(object sender, RoutedEventArgs e)
        {
            Descriptiopn.Visibility = Visibility.Visible;
            Suggest.Visibility = Visibility.Collapsed;
            btnInfo.Visibility = Visibility.Collapsed;

            appSettings.CapturePostHog("pre-signin-view-info-icon-clicked");  
        }

        
    }
}
