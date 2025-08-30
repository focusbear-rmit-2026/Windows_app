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

        Authentication authenticator = new Authentication();
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

                CancelTXT.Text = appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString();
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
            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
            {
                app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }

            if (appSettings.sTheme == "Light")
            {
                this.Background = new SolidColorBrush(Colors.White);
            }
            else // Dark
            {
                this.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 40, 40));
            }
        }

        private async void btnSignInWithGoogle_Click(object sender, RoutedEventArgs e)
        {
            btnSignInWithGoogle.IsEnabled = false;
            btnSignIn.IsEnabled = false;
            btnSignUp.IsEnabled = false;
            CancelButton.Visibility = Visibility.Visible;
            appSettings.CapturePostHog("button-signin-with-google-click");

            try
            {
                
                if (await authenticator.LoginWithGoogle(_parentWindow))
                {
                    //Login Success
                    appSettings.GetLoginAndTokenValidSettings();
                    appSettings.CapturePostHog("login-homemade");

                    CancelButton.Visibility = Visibility.Collapsed;
                    btnSignInWithGoogle.IsEnabled = true;
                    btnSignIn.IsEnabled = true;
                    btnSignUp.IsEnabled = true;
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
                btnSignInWithGoogle.IsEnabled = true;
                btnSignIn.IsEnabled = true;
                btnSignUp.IsEnabled = true;
                CancelButton.Visibility = Visibility.Collapsed;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            btnSignInWithGoogle.IsEnabled = true;
            btnSignIn.IsEnabled = true;
            btnSignUp.IsEnabled = true;
            CancelButton.Visibility = Visibility.Collapsed;
            authenticator.CancelGoogleLogin();
        }
    }
}
