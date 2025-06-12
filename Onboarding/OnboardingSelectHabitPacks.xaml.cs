using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Interop;

namespace Focus_Bear.Onboarding
{
    /// <summary>
    /// Interaction logic for OnboardingSelectHabitPacks.xaml
    /// </summary>
    public partial class OnboardingSelectHabitPacks : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private OnBoardingWindow _parentWindow;

        public OnboardingSelectHabitPacks()
        {
        }
        public OnboardingSelectHabitPacks(OnBoardingWindow parentWindow) : this()
        {
            LogWriter.LogWrite("OnboardingSelectHabitPacks(OnBoardingWindow parentWindow)");

            _parentWindow = parentWindow;

            InitializeComponent();

            SetWebView();

            webView.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
            webView.NavigationCompleted += CoreWebView2NavigationCompleted;

            try
            {
               
                if (appSettings.bGoalHealthyHabits)
                    Heading.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["titleSelectHabitPack"].ToString();
                else
                    Heading.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["titleSelectBreaks"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingSelectHabitPacks()" + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;
        }

        private async void SetWebView()
        {
            try
            {
                var userDataFolder = System.IO.Path.Combine(AppSettings.AppDataDirPath, "FBCustomWebView2");

                var task = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                if (webView != null)
                {
                    await webView.EnsureCoreWebView2Async(task);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingSelectHabitPacks SetWebView() EX - " + ex.Message);
 
            }
        }

        private void CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                try
                {
                    webView.CoreWebView2.Settings.AreHostObjectsAllowed = true;
                    webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                    webView.CoreWebView2.Settings.IsScriptEnabled = true;
                    webView.CoreWebView2.Settings.IsWebMessageEnabled = true;
                    webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                    webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                    webView.WebMessageReceived += webView_WebMessageReceived;

                    string sURL = "https://dashboard.focusbear.io/webview/routine-suggestion?bust_cache=" + DateTime.UtcNow.ToString();
                    webView.Source = new Uri(sURL, System.UriKind.Absolute);
 
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("OnboardingSelectHabitPacks CoreWebView2InitializationCompleted()" + ex.Message);
                }
            }
        }

        private void CoreWebView2NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                InitJsExec();
            }
        }

        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            webView.Dispose();
            _parentWindow.ShowOnboardingAskWorkTimeScreen();
        }
 

        private async void webView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                LogWriter.LogWrite("select habit packs webView_WebMessageReceived() - tasks got from webview");


                string sContent = e.WebMessageAsJson;
                if (sContent == "routine-suggestions-installed-successfully" || sContent == "\"routine-suggestions-installed-successfully\"")
                {
                    //Get User Settings
                    FocusBearAPIClient client = new FocusBearAPIClient();

                    //We should the settings and replace the time
                    await Task.Run(async () =>
                    {
                        CommandManager.InvalidateRequerySuggested();

                        LogWriter.LogWrite("attempt to GetUserSettings");
                        var userSettingsResponse = await client.GetUserSettings();
                        int x = int.Parse(userSettingsResponse.ToString());
                        if (x == 1)
                        {
                            LogWriter.LogWrite("GetUserSettings Success");
                            //User SEttings retrieved Successfully, Process them.
                            appSettings.ReadAndParseSettingsJson();
                        }
                        else
                        {
                            LogWriter.LogWrite("GetUserSettings Failed");
                            appSettings.ReadAndParseSettingsJson(); //This function handles if the settings are not derieved from Server
                        }

                        CommandManager.InvalidateRequerySuggested();

                        //One we have parsed the raw settings, we can get the duration for routines from the habits arrays
                        for (int i = 0; i < appSettings.arrGLOBALAllMorningHabits.Count; i++)
                        {
                            appSettings.nMorningRoutineDuration = appSettings.arrGLOBALAllMorningHabits[i].durationSeconds;
                        }
                        for (int i = 0; i < appSettings.arrGLOBALAllMorningHabits.Count; i++)
                        {
                            appSettings.nEveningRoutineDuration = appSettings.arrGLOBALAllMorningHabits[i].durationSeconds;
                        }
                    });

                    ButtonAutomationPeer peer = new ButtonAutomationPeer(btnContinue);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else if (sContent == "invoke-js-function") {
                    InitJsExec();
                } else //just navigating the steps
                {
                    JObject sContentJson = JObject.Parse(sContent);
                    if (sContentJson.ContainsKey("step"))
                    {
                        return; //nothing to do
                    }
                }

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("select habit packs webView_WebMessageReceived()" + ex.Message);
            }
        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }

        private void InitJsExec()
        {
            string sTheme = appSettings.sTheme == "Light" ? "LIGHT" : "DARK";
            string sStayFoucs = appSettings.bGoalStayFocused == true ? "Stay focused at work" : "";
            LogWriter.LogWrite("appSettings.sCustomGoalSelected: " + appSettings.sCustomGoalSelected);
            string LoadToken = string.Format("{{\"access_token\":\"{0}\",\"platform\":\"win\",lang:\"{1}\",\"font\":\"{2}\",\"theme\":\"{4}\",\"long_term_goals\":[\"{3}\"]}}", appSettings.userAccount.accessToken, appSettings.sLangCode, appSettings.sSelectedAppFont, appSettings.sCustomGoalSelected, sTheme);

            LogWriter.LogWrite("LoadToken: " + LoadToken);

            string LoadTokenFunc = string.Format("const waitForAppLoaded = setInterval(() => {{ if (window.loadMetaDataForRoutineSuggestion) {{ loadMetaDataForRoutineSuggestion({0}); clearInterval(waitForAppLoaded); }} }}, 100);", LoadToken);

            var sReturn = webView.ExecuteScriptAsync(LoadTokenFunc);
        }

    }
}
