using Focus_Bear.API;
using Focus_Bear.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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
        App app = (App)Application.Current;
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
                
                btnSignInText.Text = appSettings.ConfigFileStrings["askLogInVcInfo"]["buttonSignInEmailText"].ToString();
                btnSignInWithGoogleText.Text = appSettings.ConfigFileStrings["askLogInVcInfo"]["buttonSignInGoogleText"].ToString();

                btnSignUpText.Text = appSettings.ConfigFileStrings["customLoginVcInfo"]["buttonCreateAccountText"].ToString();

                Suggest.Text = appSettings.ConfigFileStrings["askLogInVcInfo"]["descriptionText"].ToString();
                appSettings.CapturePostHog("pre-signin-view-shown");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("LoginHome set texts ex - " + ex.Message);
            }
            TitleBarThemeChange();
            App.ThemeChanged += TitleBarThemeChange;
        }
       
        private void btnSignIn_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("button-signin-with-email-clicked");//to match with mac
            _parentWindow.ShowSignInWindow();
        }

        private void btnSignUp_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("button-signup-with-email-clicked"); //to match with mac
            _parentWindow.ShowSignUpWindow();
        }

        private void btnInfo_Click(object sender, RoutedEventArgs e)
        {
            Descriptiopn.Visibility = Visibility.Visible;
            Suggest.Visibility = Visibility.Collapsed;
            btnInfo.Visibility = Visibility.Collapsed;

            appSettings.CapturePostHog("pre-signin-view-info-icon-clicked");  
        }

        private void TitleBarThemeChange()
        {
            bool bDarkMode = Global.IsLightTheme();
            string theme = bDarkMode ? "Light" : "Dark";
            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
            {
                app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }
            if (theme == "Light")
            {
                this.Background = new SolidColorBrush(Colors.White);

                Suggest.Foreground = new SolidColorBrush(Colors.Black);
                Descriptiopn.Foreground = new SolidColorBrush(Colors.Black);
            }
            else if (theme == "Dark")
            {
                this.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 40, 40));

                Suggest.Foreground = new SolidColorBrush(Colors.LightGray);
                Descriptiopn.Foreground = new SolidColorBrush(Colors.LightGray);
            }
        }

        private async void btnSignInWithGoogle_Click(object sender, RoutedEventArgs e)
        {
            _parentWindow.IsEnabled = false;
            appSettings.CapturePostHog("button-signin-with-google-click");

            try
            {
                var authenticator = new Authentication();
                if (await authenticator.LoginWithGoogle(_parentWindow))
                {
                    //Login Success
                    appSettings.GetLoginAndTokenValidSettings();
                    appSettings.CapturePostHog("login-homemade");

                    _parentWindow.IsEnabled = true;
                    _parentWindow.bDoNotShutDown = true;
                    _parentWindow.Close();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnSignIn_Click EX - " + ex.Message);
            }
            finally
            {
                _parentWindow.IsEnabled = true;
            }
        }
    }
}
