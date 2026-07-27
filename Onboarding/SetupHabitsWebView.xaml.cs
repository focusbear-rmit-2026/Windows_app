using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Services;
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
    /// Interaction logic for SetupHabitsWebView.xaml
    /// </summary>
    public partial class SetupHabitsWebView : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private FocusBearMainWindow _parentWindow;

        public SetupHabitsWebView()
        {
        }
        public SetupHabitsWebView(FocusBearMainWindow parentWindow) : this()
        {
            LogWriter.LogWrite("SetupHabitsWebView() shown");

            _parentWindow = parentWindow;

            InitializeComponent();

            SetWebView();

            webView.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
            webView.NavigationCompleted += CoreWebView2NavigationCompleted;

            try
            {
                btnSkipText.Text = appSettings.ConfigFileStrings["welcomeInfo"]["buttonSkipText"].ToString();//"Skip"; 
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("SetupHabitsWebView()" + ex.Message);
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
                LogWriter.LogWrite("SetupHabitsWebView SetWebView() EX - " + ex.Message);
 
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

                    string sURL = "https://dashboard.focusbear.io/webview/routine-suggestion-revamp?bust_cache=" + DateTime.UtcNow.ToString() + appSettings.sHabitsParam;
                    webView.Source = new Uri(sURL, System.UriKind.Absolute);
 
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("SetupHabitsWebView CoreWebView2InitializationCompleted()" + ex.Message);
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
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._SET_DISTRACTION_BLOCKING_);
        }
 

        private async void webView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                LogWriter.LogWrite("select habit packs webView_WebMessageReceived() - tasks got from webview");

                // Path B - reactive token refresh. Handle + stop before the routine-install message.
                var req = await WebViewTokenSyncService.Instance.HandleTokenRequestAsync(e.WebMessageAsJson);
                if (req.IsTokenRequest)
                {
                    if (req.RefreshSucceeded)
                        InitJsExec(req.RequestId);
                    return;
                }

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

                        //Once we have parsed the raw settings, sum each routine's total duration from its own habits array.
                        appSettings.nMorningRoutineDuration = 0;
                        foreach (var morningHabit in appSettings.arrGLOBALAllMorningHabits)
                        {
                            appSettings.nMorningRoutineDuration += morningHabit.durationSeconds;
                        }

                        appSettings.nEveningRoutineDuration = 0;
                        foreach (var eveningHabit in appSettings.arrGLOBALAllEveningHabits)
                        {
                            appSettings.nEveningRoutineDuration += eveningHabit.durationSeconds;
                        }
                    });

                    ButtonAutomationPeer peer = new ButtonAutomationPeer(btnContinue);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else if (sContent == "invoke-js-function" || sContent == "\"invoke-js-function\"") 
                {
                    InitJsExec();
                }
                else if(sContent == "routine-suggestions-installation-failed" || sContent == "\"routine-suggestions-installation-failed\"")
                {
                    LogWriter.LogWrite("routine-suggestions-installation-failed recieved form Save Habits so moving to next screen");
                    //Not sure why the issue is coming but for now will proceed to next screens: 23 July 2025
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(btnContinue);
                    IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }
                else //just navigating the steps
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

        private void InitJsExec(string requestId = null)
        {
            string sTheme = appSettings.sTheme == "Light" ? "LIGHT" : "DARK";
            string sStayFocus = appSettings.bGoalStayFocused == true ? "Stay focused at work" : "";
            LogWriter.LogWrite("appSettings.sCustomGoalSelected: " + appSettings.sCustomGoalSelected);

            // Path B - echo the requestId back on a REQUEST_ACCESS_TOKEN reply (omitted on initial load).
            string sRequestId = string.IsNullOrEmpty(requestId) ? "" : string.Format(",\"requestId\":\"{0}\"", requestId);

            string LoadToken = string.Format("{{\"access_token\":\"{0}\",\"platform\":\"win\",lang:\"{1}\",\"font\":\"{2}\",\"theme\":\"{4}\",\"long_term_goals\":[\"{3}\"]{5}}}", appSettings.userAccount.accessToken, appSettings.sLangCode, appSettings.sSelectedAppFont, appSettings.sCustomGoalSelected, sTheme, sRequestId);

            LogWriter.LogWrite("LoadToken: " + LoadToken);

            string LoadTokenFunc = string.Format("const waitForAppLoaded = setInterval(() => {{ if (window.loadMetaDataForRoutineSuggestion) {{ loadMetaDataForRoutineSuggestion({0}); clearInterval(waitForAppLoaded); }} }}, 100);", LoadToken);

            webView.ExecuteScriptAsync(LoadTokenFunc);
        }

        private void btnSkip_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("set-up-habits-webview-skipped");
            webView.Dispose();
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._SET_DISTRACTION_BLOCKING_);
        }
        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                appSettings.CapturePostHog("install-habits-screen-shown");
            }
        }
    }
}
