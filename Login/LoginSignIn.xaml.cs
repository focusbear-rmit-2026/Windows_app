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
          
            try
            {
                Heading.Text = appSettings.ConfigFileStrings["customLoginVcInfo"]["titleSignIn"].ToString();
                Description.Text = appSettings.ConfigFileStrings["customLoginVcInfo"]["subTitleWelcomeback"].ToString();
                lblEmail.Text = appSettings.ConfigFileStrings["customLoginVcInfo"]["titleEmailAddress"].ToString();
                lblPassword.Text = appSettings.ConfigFileStrings["customLoginVcInfo"]["titlePassword"].ToString();
                btnSignInText.Text = appSettings.ConfigFileStrings["customLoginVcInfo"]["buttonSignInText"].ToString();
                btnCreateAccountText.Text = appSettings.ConfigFileStrings["customLoginVcInfo"]["buttonCreateAccountText"].ToString();
                ForgetPasswordTXT.Text = appSettings.ConfigFileStrings["customLoginVcInfo"]["buttonForgotPasswordText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("LoginSignIn ex = " + ex.Message);
            }
            Loaded += (sender, e) => TxtEmailAddress.Focus();

            appSettings.CapturePostHog("view-signin-shown");

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
            appSettings.CapturePostHog("button-signin-clicked");

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
                                            appSettings.GetLoginAndTokenValidSettings();
                                            appSettings.CapturePostHog("login-homemade");
                                            
                                            _parentWindow.bDoNotShutDown = true;
                                            _parentWindow.Close();
                                        }));
                                    }
                                    else
                                    {
                                        await Dispatcher.BeginInvoke((Action)(() =>
                                        {
                                            FBMessageBox ff = new FBMessageBox(authenticator.sErrorMessage, authenticator.sErrorMessage);
                                            ff.Owner = _parentWindow;
                                            ff.ShowDialog();
                                        }));
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogWriter.LogWrite("btnSignIn_Click EX - " + ex.Message);
                                }
                            });
                        }
                        else
                        {
                            FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["customLoginVcInfo"]["titlePasswordIsBlank"].ToString(), "Focus Bear");
                            ff.Owner = _parentWindow;
                            ff.ShowDialog();
                        }
                    }
                    else
                    {
                        FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["customLoginVcInfo"]["emailInvalidText"].ToString(), "Focus Bear");
                        ff.Owner = _parentWindow;
                        ff.ShowDialog();
                    }
                }
                else
                {
                    FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["customLoginVcInfo"]["titleEmailIsBlank"].ToString(), "Focus Bear");
                    ff.Owner = _parentWindow;
                    ff.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
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

        private async void btnForgetPassword_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            try
            {
                appSettings.CapturePostHog("button-forgot-password-clicked");
                string emailAddress = TxtEmailAddress.Text;
                if (emailAddress.Length > 0)
                {
                    EnablControls(false);
                    await Task.Run(async () =>
                    {
                        try
                        {
                            var authenticator = new Authentication();
                            if (await authenticator.ChangePassword(emailAddress))
                            {
                                await Dispatcher.BeginInvoke((Action)(() =>
                                {
                                    Mouse.OverrideCursor = null;
                                    EnablControls(true); 
                                    appSettings.CapturePostHog("change-password");
                                    FBMessageBox ff = new FBMessageBox("", authenticator.sChangePasswordMessage);
                                    ff.Owner = _parentWindow;
                                    ff.ShowDialog();
                                }));
                            }
                            else
                            {
                                await Dispatcher.BeginInvoke((Action)(() =>
                                {
                                    FBMessageBox ff = new FBMessageBox(authenticator.sErrorMessage, authenticator.sErrorMessage);
                                    ff.Owner = _parentWindow;
                                    ff.ShowDialog();
                                }));
                            }
                        }
                        catch (Exception ex)
                        {
                            LogWriter.LogWrite("btnSignIn_Click EX - " + ex.Message);
                        }
                    });
                }
                else
                {
                    FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["customLoginVcInfo"]["titleEmailIsBlank"].ToString(), "Focus Bear");
                    ff.Owner = _parentWindow;
                    ff.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
 
            }
            finally
            {
                Mouse.OverrideCursor = null;
                EnablControls(true);
            }
        }
    }
}
