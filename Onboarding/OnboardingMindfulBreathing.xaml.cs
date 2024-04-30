using Focus_Bear.Utils;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using System.Windows.Threading;

namespace Focus_Bear.Onboarding
{
    /// <summary>
    /// Interaction logic for OnboardingMindfulBreathing.xaml
    /// </summary>
    public partial class OnboardingMindfulBreathing : Page
    {
        private OnBoardingWindow _parentWindow = null;

        AppSettings appSettings = AppSettings.GetInstance;

        int nTimer = 30;
        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);
        bool bTimerRunning;
        public OnboardingMindfulBreathing()
        {
            bTimerRunning = false;
        }
        public OnboardingMindfulBreathing(OnBoardingWindow parentWindow) : this()
        {
            _parentWindow = parentWindow;

           
            InitializeComponent();

            SetWebView();
            webView.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;

            btnSkipThisHabitTXT.Text = appSettings.ConfigFileStrings["welcomeInfo"]["buttonSkipText"].ToString();
          
            Heading.Text = appSettings.OnboardingActivityName; 

            nTimer = appSettings.OnboardingActivityDurationSeconds;

            appSettings.CapturePostHog("ongoing-onboarding-activity-shown");
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
                LogWriter.LogWrite("SetWebView()" + ex.Message);
                Debug.WriteLine(ex.Message);
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

                    string videoUrl = appSettings.OnboardingVideoUrl;
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
                    if (videoUrl.Contains("?"))
                    {
                        videoUrl = videoUrl + "&autoplay=1";
                    }
                    else
                    {
                        videoUrl = videoUrl + "?autoplay=1";
                    }

                    webView.Source = new System.Uri(videoUrl);

                    appSettings.CapturePostHog("play-youtube-video-in-activity");

                    dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
                    dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
                    bTimerRunning = true;
                    pause.Visibility = Visibility.Visible;
                    play.Visibility = Visibility.Collapsed;
                    dispatcherTimer.Start();
                    CommandManager.InvalidateRequerySuggested();

                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("CoreWebView2InitializationCompleted()" + ex.Message);
                }
            }
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
#if DEBUG
            int val = nTimer-=1;
#else
            int val = nTimer--;
#endif


            if (nTimer < 25) //Hide the top image after 5 seconds
            {
                TopIcon.Visibility = Visibility.Collapsed;
            }
            string msg = "00:" + val.ToString("D2");
            TxtTimer.Text = msg;

            CommandManager.InvalidateRequerySuggested();
            
            if(nTimer < 0)
            {
                TxtTimer.Text = "00:00";
                //Stop the video too
                webView.Source = new System.Uri("about:blank");
                try
                {
                    _parentWindow.ShowMindFullBreathingFinished();
                    dispatcherTimer.Stop();

                    appSettings.CapturePostHog("onboarding-activity-finished");
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite(ex.Message);
                }
            }
        }
        private void btnPausePlay_Click(object sender, RoutedEventArgs e)
        {
            if (bTimerRunning)
            {
                dispatcherTimer.Stop();
                bTimerRunning = false;
                pause.Visibility = Visibility.Collapsed;
                play.Visibility = Visibility.Visible;

                SkipThisHabitText.Visibility = Visibility.Visible; //Show the skip button
            }
            else
            {
                bTimerRunning = true;
                play.Visibility = Visibility.Collapsed;
                pause.Visibility = Visibility.Visible;
                dispatcherTimer.Start();

                SkipThisHabitText.Visibility = Visibility.Collapsed;//Hide the skip button
            }
        }

        private void btnSkipThisHabit_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("OnboardingMindfulBreathing - btnSkipThisHabit_Click");

            TxtTimer.Text = "00:00";
            appSettings.bOnboardingActivitySkipped = true;
            //Stop the video too
            webView.Source = new System.Uri("about:blank");
            try
            {
                _parentWindow.ShowMindFullBreathingFinished();
                dispatcherTimer.Stop();
                appSettings.CapturePostHog("onboarding-activity-skipped");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingMindfulBreathing btnSkipThisHabit_Click ex" + ex.Message);
            }
        }
    }
}
