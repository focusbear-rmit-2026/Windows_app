using Focus_Bear.API;
using Focus_Bear.Preferences.SettingsTab;
using Focus_Bear.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net.Mail;
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
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace Focus_Bear.Onboarding
{
    /// <summary>
    /// Interaction logic for SetupAccount.xaml
    /// </summary>
    public partial class SetupAccount : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private FocusBearMainWindow _parentWindow;
        public SetupAccount()
        {
        }
        public SetupAccount(FocusBearMainWindow parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Bubble1.Text = appSettings.ConfigFileStrings["setUpAccountVcInfo"]["bubbleLeftText"].ToString();  //"That way, we can work together on your computer and phone!";  
                Heading.Text = appSettings.ConfigFileStrings["setUpAccountVcInfo"]["titleText"].ToString();  //"Set up an account";
                Description.Text = appSettings.ConfigFileStrings["setUpAccountVcInfo"]["subtitleText"].ToString();  //"Stay focused across all your devices -no matter where you work.";

                btnStayAnonymousText.Text = appSettings.ConfigFileStrings["setUpAccountVcInfo"]["buttonStayAnonymous"].ToString();  //"I’ll stay anonymous";
                btnSignMeUpText.Text = appSettings.ConfigFileStrings["setUpAccountVcInfo"]["buttonSignUp"].ToString();  //"Sign me up, Scotty!";
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingNewSetupAccount() EX -" + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;

        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }

        
        private async void btnStayAnonymous_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnStayAnonymous_Click");


            appSettings.CapturePostHog("button-stay-anonymous-clicked");
            string sPassword = "Passw0rd!";
            string sEmail = AppSettings.AnonymousEmailPrefix + System.Guid.NewGuid().ToString() + "@focusbear.io";

            if(appSettings.bIsTestUser)
            {
                sEmail = "internaltest+" + System.Guid.NewGuid().ToString() + "@focusbear.io";
            }

            {
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                EnablControls(false);
                await Task.Run(async () =>
                {
                    try
                    {
                        var authenticator = new Authentication();
                        if (await authenticator.SignUp(sEmail, sPassword))
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
                                        if (await authenticator.Login(sEmail, sPassword))
                                        {
                                            await Dispatcher.BeginInvoke((Action)(() =>
                                            {
                                                LogWriter.LogWrite("SignUp Success, Login Success!!");

                                                Mouse.OverrideCursor = null;
                                                EnablControls(true);

                                                //Login Success
                                                appSettings.GetLoginAndTokenValidSettings();
                                                appSettings.CapturePostHog("login-homemade");

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
                                                ff.Owner = _parentWindow;
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
                                    ff.Owner = _parentWindow;
                                    ff.ShowDialog();
                                }
                                catch (Exception ex)
                                {
                                    Mouse.OverrideCursor = null;
                                    EnablControls(true);
                                    LogWriter.LogWrite(ex.Message);

                                    FBMessageBox ff = new FBMessageBox(authenticator.sErrorMessage, "Focus Bear");
                                    ff.Owner = _parentWindow;
                                    ff.ShowDialog();
                                }


                            }));
                        }
                    }
                    catch (Exception ex)
                    {
                        Mouse.OverrideCursor = null;
                        EnablControls(true);
                        LogWriter.LogWrite(ex.Message);
                    }
                });

            }
            Mouse.OverrideCursor = null;
        }

        private void EnablControls(bool bEnable)
        {
            btnStayAnonymous.IsEnabled = bEnable;
            btnSignMeUp.IsEnabled = bEnable;
            _parentWindow.PageOnboardingViewController.BtnBack.IsEnabled = bEnable;
        }
        private void btnSignMeUp_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnSignMeUp_Click");
            appSettings.CapturePostHog("button-sign-me-up-clicked");
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._PRIVACY_POLICY_);
        }
        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                appSettings.CapturePostHog("setup-account-screen-shown");
            }
        }
    }
}
