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
using Focus_Bear.Utils;
using Focus_Bear.API;
using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace Focus_Bear.Login
{
    /// <summary>
    /// Interaction logic for LoginSignUp.xaml
    /// </summary>
    public partial class LoginSignIn : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;

        private LoginMain _parentWindow;
        public LoginSignIn()
        {
            
        }

        public LoginSignIn(LoginMain parentWindow) : this()
        {
            InitializeComponent();
            _parentWindow = parentWindow;
            /*
             * "customLoginVcInfo": {
                "titleSignIn": "Sign In",
                     "subTitleWelcomeback": "Welcome back",
                "titleEmailAddress": "Email Address",
                "titlePassword": "Password",
                 "buttonCreateAccountText": "Create an account",
                         "buttonSignInText": "Sign In",
             */

            Heading.Text = appSettings.ConfigFileStrings["customLoginVcInfo"]["titleSignIn"].ToString();
            Description.Text = appSettings.ConfigFileStrings["customLoginVcInfo"]["subTitleWelcomeback"].ToString();
            lblEmail.Text = appSettings.ConfigFileStrings["customLoginVcInfo"]["titleEmailAddress"].ToString();
            lblPassword.Text = appSettings.ConfigFileStrings["customLoginVcInfo"]["titlePassword"].ToString();
            btnSignInText.Text = appSettings.ConfigFileStrings["customLoginVcInfo"]["buttonSignInText"].ToString();
            btnCreateAccountText.Text = appSettings.ConfigFileStrings["customLoginVcInfo"]["buttonCreateAccountText"].ToString();

           // PleaseWait.Text = "Please Wait"; //TOdO Win text

            Loaded += (sender, e) => TxtEmailAddress.Focus();
        }

        private void btnCreatAccount_Click(object sender, RoutedEventArgs e)
        {
            _parentWindow.ShowSignUpWindow();
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            _parentWindow.ShowLoginHomeWindow();
        }

        private async void btnSignIn_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            try
            {

                string emailAddress = TxtEmailAddress.Text;
                if (emailAddress.Length > 0)
                {
                    if (Global.IsValidEmailAddress(emailAddress))
                    {
                        string password = TxtPassword.Password.ToString();
                        if (password.Length > 0)
                        {
                            EnablControls(false);
                            await Task.Run(async () =>
                            {
                                try
                                {
                                    var authenticator = new Authentication();
                                    if (await authenticator.Login(emailAddress, password))
                                    {
                                        await Dispatcher.BeginInvoke((Action)(() =>
                                        {
                                            //Login Success
                                            appSettings.CapturePostHog("login-homemade");

                                            ((App)(Application.Current)).ProceedAfterLogin();
                                            _parentWindow.bDoNotShutDown = true;
                                            _parentWindow.Close();
                                        }));
                                    }
                                    else
                                    {
                                        await Dispatcher.BeginInvoke((Action)(() =>
                                        {
                                            MessageBox.Show(authenticator.sErrorMessage, authenticator.sErrorMessage, MessageBoxButton.OK, MessageBoxImage.Error);
                                        }));
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogWriter.LogWrite(ex.Message);
                                    Debug.WriteLine(ex.Message);
                                }
                            });
                        }
                        else
                        {
                            MessageBox.Show("xpassword empty", "xSignin Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show("xInvalid email", "xSignin Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("xEmail empty", "xSignin Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                EnablControls(true);
            }

        }

        public void EnablControls(bool bEnable)
        {
            if (bEnable)
            {
                StatusAct.Visibility = Visibility.Collapsed;
            }
            else
            {
                StatusAct.Visibility = Visibility.Visible;
            }
            TxtEmailAddress.IsEnabled = bEnable;
            TxtPassword.IsEnabled = bEnable;
            btnBack.IsEnabled = bEnable;
            btnSignIn.IsEnabled = bEnable;
            btnCreatAccount.IsEnabled = bEnable;
        }


    }
}
