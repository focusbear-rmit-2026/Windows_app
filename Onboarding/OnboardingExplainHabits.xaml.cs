using Focus_Bear.Utils;
using Microsoft.Web.WebView2.Core;
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
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Focus_Bear.Onboarding
{
    /// <summary>
    /// Interaction logic for OnboardingExplainHabits.xaml
    /// </summary>
    public partial class OnboardingExplainHabits : Page
    {
        private OnBoardingWindow _parentWindow = null;

        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        public OnboardingExplainHabits()
        {
             
        }

        public OnboardingExplainHabits(OnBoardingWindow parentWindow) : this()
        {
            InitializeComponent();

            InitializeComponent();

            _parentWindow = parentWindow;

            SetWebView();
            webView.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;

            btnGotItTXT.Text = appSettings.ConfigFileStrings["onboardingHabitExplainVcInfo"]["buttonContinueText"].ToString();
            Heading.Text = appSettings.ConfigFileStrings["onboardingHabitExplainVcInfo"]["title"].ToString();
            Description.Text = appSettings.ConfigFileStrings["onboardingHabitExplainVcInfo"]["subTitle"].ToString();
            Page_Theme();
            App.ThemeChanged += Page_Theme;
        }

        private void btnGotIt_Click(object sender, RoutedEventArgs e)
        {
            _parentWindow.ShowOnboardingAchieveWithFocusBearScreen();
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
                LogWriter.LogWrite("OnboardingExplainDeepFocus SetWebView()" + ex.Message);
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

                    string videoUrl = appSettings.ConfigFileStrings["onboardingHabitExplainVcInfo"]["video_url"].ToString();

                    if (videoUrl.Contains("watch?v="))
                    {
                        videoUrl = videoUrl.Replace("watch?v=", "embed/");
                    }
                    else
                    {
                        if (videoUrl.ToLower().Contains("https://youtu.be/"))
                        {
                            //Handle non permitted embedded videos
                            //https://youtu.be/lExuhKRS8vo
                            string lastItem = videoUrl.Split('/').Last();
                            if (lastItem.Length > 0)
                            {
                                videoUrl = "https://www.youtube.com/embed/" + lastItem;
                            }

                        }
                    }
                    webView.Source = new System.Uri(videoUrl);
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("CoreWebView2InitializationCompleted()" + ex.Message);
                }
            }
        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }
    }
}
