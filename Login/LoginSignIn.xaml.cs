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
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Reflection;

namespace Focus_Bear.Login
{
    /// <summary>
    /// Interaction logic for LoginSignUp.xaml
    /// </summary>
    public partial class LoginSignIn : Page
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private LoginMain _parentWindow;
        public bool bEyeOnPasswordBox = false;
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
            TitleBarThemeChange();
            App.ThemeChanged += TitleBarThemeChange;
        }

        private void btnCreateAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("https://dashboard.focusbear.io/login?ui=signup&source=win_app");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnSignUp_Click ex + " + ex.Message);
            }
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
                                            // Todo win texts

                                            // Pedro 10 Sep 2025 - fix for issue #275 (Redundant Wrong email or password)
                                            // The "loginFailed" key was added to the config.json and config-es.json files
                                            // FBMessageBox ff = new FBMessageBox(authenticator.sErrorMessage, appSettings.ConfigFileStrings["customLoginVcInfo"]["loginFailed"].ToString());
                                            FBMessageBox ff = new FBMessageBox(authenticator.sErrorMessage, "Login Failed");
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
            btnCreateAccount.IsEnabled = bEnable;
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
                lblEmail.Foreground = new SolidColorBrush(Colors.Black);
                lblPassword.Foreground = new SolidColorBrush(Colors.Black);
            }
            else
            {
                this.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
                lblEmail.Foreground = new SolidColorBrush(Colors.LightGray);
                lblPassword.Foreground = new SolidColorBrush(Colors.LightGray);
            }
        }

        private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            //TxtPasswordVisible.Text = TxtPassword.Password;
            //TxtPassword.GetType().GetMethod("Select", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(TxtPassword, new object[] { TxtPassword.Password.Length, TxtPassword.Password.Length });
            //TxtPassword.Focus();
        }
        private void TxtPasswordVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            TxtPassword.Password = TxtPasswordVisible.Text;
            TxtPasswordVisible.Focus();
        }
        
        private void EyePassword_MouseUp(object sender, MouseButtonEventArgs e)
        {
            bEyeOnPasswordBox = !bEyeOnPasswordBox;
            if (bEyeOnPasswordBox)
            {
                EyePassword.Source = new BitmapImage(new Uri("pack://application:,,,/Images/eye-orange.png"));
                TxtPasswordVisible.Text = TxtPassword.Password;
                TxtPasswordVisible.Visibility = Visibility.Visible;
                TxtPassword.Visibility = Visibility.Collapsed;

                try
                {
                    EyePassword.ToolTip = appSettings.ConfigFileStrings["customLoginVcInfo"]["tooltipHidePassword"].ToString();
                }
                catch (Exception ex)
                {
                    EyePassword.ToolTip = "Hide Password"; //Fore Time Being
                    LogWriter.LogWrite("EyePassword_MouseUp text ex = " + ex.Message);
                }
            }
            else
            {
                EyePassword.Source = new BitmapImage(new Uri("pack://application:,,,/Images/eye-dark.png"));

                TxtPasswordVisible.Visibility = Visibility.Collapsed;
                TxtPassword.Visibility = Visibility.Visible;
                try
                {
                    EyePassword.ToolTip = appSettings.ConfigFileStrings["customLoginVcInfo"]["tooltipShowPassword"].ToString();
                }
                catch (Exception ex)
                {
                    EyePassword.ToolTip = "Show Password"; //for time being
                    LogWriter.LogWrite("EyePassword_MouseUp text ex = " + ex.Message);
                }
            }
        }

    }
}
