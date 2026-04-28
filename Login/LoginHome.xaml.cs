using Focus_Bear.API;
using Focus_Bear.Onboarding;
using Focus_Bear.Utils;
using Microsoft.Win32;
using Sentry.Protocol;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Linq;
using System.Security.Policy;
using System.Security.Principal;
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
        private FocusBearMainWindow ParentWindow;
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;

        Authentication authenticator = new Authentication();
        public LoginHome()
        {
        }

        public LoginHome(FocusBearMainWindow _parentWindow) : this()
        {
            ParentWindow = _parentWindow;
            InitializeComponent();

            UpdateTexts();
            
            TitleBarThemeChange();
            App.ThemeChanged += TitleBarThemeChange;
        }

        public void UpdateTexts()
        {
            try
            {
                btnHaveAccountText.Text = appSettings.ConfigFileStrings["welcomeVcInfo"]["buttonAlreadyHaveAccountText"].ToString();

                if (appSettings.bAlreadyHaveAnAccountClicked)
                {
                    Heading.Text = appSettings.ConfigFileStrings["customLoginVcInfo"]["subTitleSignIn"].ToString();
                    Bubble1.Visibility = Visibility.Collapsed;

                    btnSignInWithGoogleText.Text = appSettings.ConfigFileStrings["askLogInVcInfo"]["buttonSignInGoogleText"].ToString();
                    btnSignInAppleText.Text = appSettings.ConfigFileStrings["askLogInVcInfo"]["buttonSignInAppleText"].ToString();
                    btnSignUpText.Text = appSettings.ConfigFileStrings["askLogInVcInfo"]["buttonSignInEmailText"].ToString();

                    btnHaveAccountText.Text = appSettings.ConfigFileStrings["customLoginVcInfo"]["subTitleCreateAccount"].ToString();
                }
                else
                {
                    Heading.Text = appSettings.ConfigFileStrings["askLogInVcInfo"]["buttonCreateAccount"].ToString(); //"Create your account"; 

                    Bubble1.Visibility = Visibility.Visible;
                    Bubble1.Text = appSettings.ConfigFileStrings["askLogInVcInfo"]["bubbleText"].ToString(); //"One account, all your devices - let's get you set up!";//todo win texts

                    btnSignInWithGoogleText.Text = appSettings.ConfigFileStrings["askLogInVcInfo"]["buttonSignUpWithGoogle"].ToString();
                    btnSignInAppleText.Text = appSettings.ConfigFileStrings["askLogInVcInfo"]["buttonSignUpWithApple"].ToString();
                    btnSignUpText.Text = appSettings.ConfigFileStrings["askLogInVcInfo"]["buttonSignUpWithEmail"].ToString();

                    btnHaveAccountText.Text = appSettings.ConfigFileStrings["welcomeVcInfo"]["buttonAlreadyHaveAccountText"].ToString();
                }

                OR.Text = appSettings.ConfigFileStrings["askLogInVcInfo"]["orText"].ToString(); //"OR";

                appSettings.CapturePostHog("pre-signin-view-shown");

                CancelTXT.Text = appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("LoginHome set texts ex - " + ex.Message);
            }
        }
        private void btnSignIn_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("button-signin-with-email-clicked");//to match with mac

            if (appSettings.bAlreadyHaveAnAccountClicked) //Sign in
            {
                ParentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._LOGIN_);
            }
            else //Sign up
            {
                ParentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._SIGN_UP_);
            }
        }

        private void btnSignUp_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("button-signup-with-email-clicked"); //to match with mac

            try
            {
                System.Diagnostics.Process.Start("https://dashboard.focusbear.io/login?ui=signup&source=win_app");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnSignUp_Click ex + " + ex.Message);
            }
            // erlier we used to ShowSignUpWindow() here;
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
            btnSignInGoogle.IsEnabled = false;
            btnSignInApple.IsEnabled = false;
            
            btnSignUp.IsEnabled = false;
            CancelButton.Visibility = Visibility.Visible;
            HaveAccount.Visibility = Visibility.Collapsed;
            appSettings.CapturePostHog("button-signin-with-google-click");

            try
            {
                
                if (await authenticator.LoginWithGoogle(ParentWindow))
                {
                    //Login Success
                    appSettings.GetLoginAndTokenValidSettings();
                    appSettings.CapturePostHog("login-homemade");

                    CancelButton.Visibility = Visibility.Collapsed;
                    HaveAccount.Visibility = Visibility.Visible;
                    btnSignInGoogle.IsEnabled = true;
                    btnSignInApple.IsEnabled = true;
            
                    btnSignUp.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnSignIn_Click EX - " + ex.Message);
            }
            finally
            {
                btnSignInGoogle.IsEnabled = true;
                btnSignInApple.IsEnabled = true;
            
                btnSignUp.IsEnabled = true;
                CancelButton.Visibility = Visibility.Collapsed;
                HaveAccount.Visibility = Visibility.Visible;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            btnSignInGoogle.IsEnabled = true;
            btnSignInApple.IsEnabled = true;
            HaveAccount.Visibility = Visibility.Visible;

            btnSignUp.IsEnabled = true;
            CancelButton.Visibility = Visibility.Collapsed;
            HaveAccount.Visibility = Visibility.Visible;
            authenticator.CancelGoogleLogin();
        }

        private async void btnSignInWithApple_Click(object sender, RoutedEventArgs e)
        {
            btnSignInGoogle.IsEnabled = false;
            btnSignInApple.IsEnabled = false;
            
            btnSignUp.IsEnabled = false;
            CancelButton.Visibility = Visibility.Visible;
            HaveAccount.Visibility = Visibility.Collapsed;

            appSettings.CapturePostHog("button-signin-with-apple-click");

            try
            {

                if (await authenticator.LoginWithGoogle(ParentWindow, true))
                {
                    //Login Success
                    appSettings.GetLoginAndTokenValidSettings();
                    appSettings.CapturePostHog("login-homemade");

                    CancelButton.Visibility = Visibility.Collapsed;
                    HaveAccount.Visibility = Visibility.Visible;

                    btnSignInGoogle.IsEnabled = true;
                    btnSignInApple.IsEnabled = true;
            
                    btnSignUp.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnSignInWithApple_Click EX - " + ex.Message);
            }
            finally
            {
                btnSignInGoogle.IsEnabled = true;
                btnSignInApple.IsEnabled = true;
            
                btnSignUp.IsEnabled = true;
                CancelButton.Visibility = Visibility.Collapsed;
                HaveAccount.Visibility = Visibility.Visible;
            }
        }

        private void btnHaveAccount_Click(object sender, RoutedEventArgs e)
        {
            appSettings.bAlreadyHaveAnAccountClicked = !appSettings.bAlreadyHaveAnAccountClicked;
            
            UpdateTexts();
        }
    }
}
