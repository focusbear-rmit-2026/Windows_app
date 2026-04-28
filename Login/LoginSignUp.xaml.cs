using Focus_Bear.Login;
using Focus_Bear.Utils;
using Focus_Bear.API;
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
using Focus_Bear.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Interop;

namespace Focus_Bear.Login
{
    /// <summary>
    /// Interaction logic for LoginSignUp.xaml
    /// </summary>
    /// 

    public partial class LoginSignUp : Page
    {
        private FocusBearMainWindow ParentWindow;
        AppSettings appSettings = AppSettings.GetInstance;
        bool bEyeOnPasswordBox = false;
        App app = (App)Application.Current;
        public LoginSignUp()
        {
        }

        public LoginSignUp(FocusBearMainWindow _parentWindow) : this()
        {
       
            InitializeComponent();
            ParentWindow = _parentWindow;
          
            try
            {
                Heading.Text = appSettings.ConfigFileStrings["customLoginVcInfo"]["subTitleCreateAccount"].ToString();
                Description.Text = appSettings.ConfigFileStrings["customLoginVcInfo"]["explainCreateAccountText"].ToString();
                lblEmail.Text = appSettings.ConfigFileStrings["customLoginVcInfo"]["titleEmailAddress"].ToString();
                lblPassword.Text = appSettings.ConfigFileStrings["customLoginVcInfo"]["titlePassword"].ToString();
                lblConfirmPassword.Text = appSettings.ConfigFileStrings["customLoginVcInfo"]["titleConfirmPassword"].ToString();
                btnCreateAccountText.Text = appSettings.ConfigFileStrings["customLoginVcInfo"]["buttonCreateAccountText"].ToString();

                lblPasswordRules.Text = appSettings.ConfigFileStrings["customLoginVcInfo"]["titlePasswordRules"].ToString();

                lblPassword8Chars.Text = "- " + appSettings.ConfigFileStrings["customLoginVcInfo"]["passwordRuleFirstText"].ToString();
                lblPassword3Types.Text = "- " + appSettings.ConfigFileStrings["customLoginVcInfo"]["passwordRuleSecondText"].ToString();

                lblPasswordLowerCase.Text = " * " + appSettings.ConfigFileStrings["customLoginVcInfo"]["passwordRuleThirdText"].ToString();
                lblPasswordUpperCase.Text = " * " + appSettings.ConfigFileStrings["customLoginVcInfo"]["passwordRuleFourthText"].ToString();
                lblPasswordNumbers.Text = " * " + appSettings.ConfigFileStrings["customLoginVcInfo"]["passwordRuleFifthText"].ToString();
                lblPasswordSpecialChars.Text = " * " + appSettings.ConfigFileStrings["customLoginVcInfo"]["passwordRuleSixthText"].ToString();

                //initially button will be disabled
                btnCreateAccount.IsEnabled = false;

                Loaded += (sender, e) => TxtEmailAddress.Focus();

                EyePassword.ToolTip = appSettings.ConfigFileStrings["customLoginVcInfo"]["tooltipShowPassword"].ToString();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("LoginSignUp Texts Exception - " + ex.Message);
            }
            appSettings.CapturePostHog("view-signup-shown");
            TitleBarThemeChange();
            App.ThemeChanged += TitleBarThemeChange;
        }

        private async void btnCreateAccount_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnCreatAccount_Click");
            appSettings.CapturePostHog("button-create-account-clicked");
            try
            {

                string emailAddress = TxtEmailAddress.Text;
                if (emailAddress.Length > 0)
                {
                    if (Global.IsValidEmailAddress(emailAddress))
                    {
                        //check password
                        string password = TxtPassword.Password.ToString();
                        if (password.Length > 0)
                        {
                            //check confirm password - confirm password removed
                            {
                                EnablControls(false);
                                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

                                await Task.Run(async () =>
                                {
                                    try
                                    {
                                        var authenticator = new Authentication();
                                        if (await authenticator.SignUp(emailAddress, password))
                                        {
                                            LogWriter.LogWrite("SignUp Success, now attempt to Login");

                                            appSettings.CapturePostHog("register-homemade");

                                            await Dispatcher.BeginInvoke((Action)(() =>
                                            {
                                                //Signup Success
                                                //Login User
                                                EnablControls(false);
                                                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

                                                Task.Run(async () =>
                                                {
                                                    try
                                                    {
                                                        if (await authenticator.Login(emailAddress, password))
                                                        {
                                                            await Dispatcher.BeginInvoke((Action)(() =>
                                                            {
                                                                LogWriter.LogWrite("SignUp Success, Login Success!!");

                                                                Mouse.OverrideCursor = null;
                                                                EnablControls(true);

                                                                //Login Success
                                                                appSettings.GetLoginAndTokenValidSettings();
                                                                appSettings.CapturePostHog("login-homemade");

                                                                ParentWindow.Hide();

                                                            }));
                                                        }
                                                        else
                                                        {
                                                            await Dispatcher.BeginInvoke((Action)(() =>
                                                            {
                                                                LogWriter.LogWrite("SignUp Success, Login ERROR !! + " + authenticator.sErrorMessage);
                                                                Mouse.OverrideCursor = null;
                                                                EnablControls(true);
                                                                
                                                                FBMessageBox ff = new FBMessageBox(authenticator.sErrorMessage, "Focus Bear");
                                                                ff.Owner = ParentWindow;
                                                                ff.ShowDialog();
                                                            }));
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        Mouse.OverrideCursor = null;
                                                        EnablControls(true);
                                                        LogWriter.LogWrite("authenticator.Login Exception - " + ex.Message);
                                                        Debug.WriteLine(ex.Message);
                                                    }
                                                });


                                            }));
                                        }
                                        else
                                        {
                                            await Dispatcher.BeginInvoke((Action)(() =>
                                            {
                                                try
                                                {
                                                    JObject resJson = JObject.Parse(authenticator.sErrorMessage);
                                                    string msg = "";
                                                    string policy = "";
                                                    if (resJson.ContainsKey("message"))
                                                    {
                                                        JToken jj = resJson.GetValue("message");
                                                        msg = jj.ToString();

                                                        JToken jjp = resJson.GetValue("policy");
                                                        policy = jjp.ToString();
                                                    }

                                                    Mouse.OverrideCursor = null;
                                                    EnablControls(true);
                                                    
                                                    FBMessageBox ff = new FBMessageBox(policy, "Focus Bear");
                                                    ff.Owner = ParentWindow;
                                                    ff.ShowDialog();
                                                }
                                                catch (Exception ex)
                                                {
                                                    Mouse.OverrideCursor = null;
                                                    EnablControls(true);
                                                    LogWriter.LogWrite(ex.Message);
                                                    
                                                    FBMessageBox ff = new FBMessageBox(authenticator.sErrorMessage, "Focus Bear");
                                                    ff.Owner = ParentWindow;
                                                    ff.ShowDialog();
                                                }
                                               

                                            }));
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LogWriter.LogWrite(ex.Message);
                                    }
                                });

                            }
                        }
                        else
                        {
                            FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["customLoginVcInfo"]["titlePasswordIsBlank"].ToString(), "Focus Bear");
                            ff.Owner = ParentWindow;
                            ff.ShowDialog();
                        }
                    }
                    else
                    {
                        FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["customLoginVcInfo"]["emailInvalidText"].ToString(), "Focus Bear");
                        ff.Owner = ParentWindow;
                        ff.ShowDialog();
                    }
                }
                else
                {
                    FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["customLoginVcInfo"]["titleEmailIsBlank"].ToString(), "Focus Bear");
                    ff.Owner = ParentWindow;
                    ff.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                
            }
            finally
            {
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
            TxtConfirmPassword.IsEnabled = bEnable;
            btnCreateAccount.IsEnabled = bEnable;
        }

        private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            //TxtPasswordVisible.Text = TxtPassword.Password;
  
            //TxtPassword.GetType().GetMethod("Select", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(TxtPassword, new object[] { TxtPassword.Password.Length, TxtPassword.Password.Length });
            //TxtPassword.Focus();
            if(IsPasswordValid())
            {
                btnCreateAccount.IsEnabled = true;

            }
            else
            {
                btnCreateAccount.IsEnabled = false;
            }
        }

        private void TxtPasswordVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            TxtPassword.Password = TxtPasswordVisible.Text;
            TxtPasswordVisible.Focus();
        }

        private void TxtConfirmPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
             
        }
        private void TxtConfirmPasswordVisible_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        bool IsPasswordValid()
        {
            bool isPasswordFirstRuleValid = false;
            if (TxtPassword.Password.Length >= 8)
            {
                ImgPassword8Chars.Visibility = Visibility.Visible;
                isPasswordFirstRuleValid = true;
            }
            else
            {
                ImgPassword8Chars.Visibility = Visibility.Collapsed;
            }
            int passwordRuleAppliedCount = 0;

            Regex regex = new Regex(@".*[A-Z]+.*");
            if(regex.IsMatch(TxtPassword.Password))
            {
                passwordRuleAppliedCount += 1;
                ImgPasswordUpperCase.Visibility = Visibility.Visible;
            }
            else
            {
                ImgPasswordUpperCase.Visibility = Visibility.Collapsed;
            }  
            
            Regex regex1 = new Regex(@".*[a-z]+.*");
            if(regex1.IsMatch(TxtPassword.Password))
            {
                passwordRuleAppliedCount += 1;
                ImgPasswordLowerCase.Visibility = Visibility.Visible;
            }
            else
            {
                ImgPasswordLowerCase.Visibility = Visibility.Collapsed;
            }

            Regex regex2 = new Regex(@".*[0-9]+.*");
            if(regex2.IsMatch(TxtPassword.Password))
            {
                passwordRuleAppliedCount += 1;
                ImgPasswordNumbers.Visibility = Visibility.Visible;
            }
            else
            {
                ImgPasswordNumbers.Visibility = Visibility.Collapsed;
            }
           
            Regex regex3 = new Regex(@".*[!&^%$#@()/]+.*");
            if(regex3.IsMatch(TxtPassword.Password))
            {
                passwordRuleAppliedCount += 1;
                ImgPasswordSpecialChars.Visibility = Visibility.Visible;
            }
            else
            {
                ImgPasswordSpecialChars.Visibility = Visibility.Collapsed;
            }


            if (passwordRuleAppliedCount >= 3)
            {
                ImgPassword3Types.Visibility = Visibility.Visible;
                if (isPasswordFirstRuleValid)
                {
                    ImgPasswordValid.Visibility = Visibility.Visible;
                    return true;
                }
                else
                {
                    ImgPasswordValid.Visibility = Visibility.Collapsed;
                    return false;
                }
            }
            else
            {
                ImgPassword3Types.Visibility = Visibility.Collapsed;
                ImgPasswordValid.Visibility = Visibility.Collapsed;
                return false;
           }

        }


        private void EyePassword_MouseUp(object sender, MouseButtonEventArgs e)
        {
            bEyeOnPasswordBox = !bEyeOnPasswordBox;
            if (bEyeOnPasswordBox)
            {
                string imagePath = ("pack://application:,,,/Focus Bear;component/images/eye-orange.png");
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(imagePath);
                image.EndInit();
                EyePassword.Source = image;
                TxtPasswordVisible.Text = TxtPassword.Password;
                TxtPasswordVisible.Visibility = Visibility.Visible;
                TxtPassword.Visibility = Visibility.Collapsed;

                try
                {
                    EyePassword.ToolTip = appSettings.ConfigFileStrings["customLoginVcInfo"]["tooltipHidePassword"].ToString();
                }
                catch (Exception ex)
                {
                    EyePassword.ToolTip = "Hide Password"; //Fore Time Being //todo win texts
                    LogWriter.LogWrite("EyePassword_MouseUp text ex = " + ex.Message);
                }
            }
            else
            {
                string imagePath = ("pack://application:,,,/Focus Bear;component/images/eye-dark.png");
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(imagePath);
                image.EndInit();
                EyePassword.Source = image;

                TxtPasswordVisible.Visibility = Visibility.Collapsed;
                TxtPassword.Visibility = Visibility.Visible;

                try
                {
                    EyePassword.ToolTip = appSettings.ConfigFileStrings["customLoginVcInfo"]["tooltipShowPassword"].ToString();
                }
                catch (Exception ex)
                {
                    EyePassword.ToolTip = "Show Password"; //for time being //todo win texts
                    LogWriter.LogWrite("EyePassword_MouseUp text ex = " + ex.Message);
                }
            }
        }

        private void EyeConfirmPassword_MouseUp(object sender, MouseButtonEventArgs e)
        { 
        }

        private string GenerateSecurePassword()
        {
            string letters = "abcdefghijklmnopqrstuvwxyz";
            string uppercaseLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            string numbers = "0123456789";
            string specialCharacters = "!@#$%^&*()_-+={}[]\\|:;\"'<>,.?/~`";
            int passwordLength = 8; // change this to set the length of the password

            Random random = new Random();
            string password = "";

            // add one upper case letter
            password += uppercaseLetters[random.Next(0, uppercaseLetters.Length)];

            // add one lower case letter
            password += letters[random.Next(0, letters.Length)];

            // add one special character
            password += specialCharacters[random.Next(0, specialCharacters.Length)];

            // add one number
            password += numbers[random.Next(0, numbers.Length)];

            // add remaining characters randomly
            for (int i = 0; i < passwordLength - 4; i++)
            {
                string allChars = letters + uppercaseLetters + numbers; //+ specialCharacters
                password += allChars[random.Next(0, allChars.Length)];
            }

            // shuffle the password to make it more secure
            password = new string(password.ToCharArray().OrderBy(x => random.Next()).ToArray());

            return password;
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
                Description.Foreground = new SolidColorBrush(Colors.Black);
                lblEmail.Foreground = new SolidColorBrush(Colors.Black);
                lblPassword.Foreground = new SolidColorBrush(Colors.Black);
            }
            else
            {
                this.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
                Description.Foreground = new SolidColorBrush(Colors.LightGray);
                lblEmail.Foreground = new SolidColorBrush(Colors.LightGray);
                lblPassword.Foreground = new SolidColorBrush(Colors.LightGray);
            }
        }
        

    }
}
