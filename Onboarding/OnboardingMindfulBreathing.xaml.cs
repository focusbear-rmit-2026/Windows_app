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
        DispatcherTimer dispatcherTimer = new DispatcherTimer();
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

            var Title = appSettings.ConfigFileStrings["welcomeInfo"]["introActivities"][0]["name"];
            Heading.Text = Title.ToString();

            int nDurationSeconds = (int) appSettings.ConfigFileStrings["welcomeInfo"]["introActivities"][0]["duration_seconds"];
            nTimer = nDurationSeconds;

            JArray array = (JArray)appSettings.ConfigFileStrings["welcomeInfo"]["introActivities"][0]["video_urls"];
            int n = array.Count;
            Random rnd = new Random();
            int nRandom = rnd.Next(0, n-1);
            string videoUrl = array[nRandom].ToString();


            //string videoUrl = "https://www.youtube.com/watch?v=HhMXYsk_Pvo";
            //string videoUrl = "https://youtu.be/lExuhKRS8vo";
            if (videoUrl.Contains("watch?v=") )
            {
                videoUrl = videoUrl.Replace("watch?v=", "embed/");
            }
            else
            {
                if (videoUrl.ToLower().Contains("https://youtu.be/")){
                    //Handle non permitted embedded videos
                    //https://youtu.be/lExuhKRS8vo
                    string lastItem = videoUrl.Split('/').Last();
                    if(lastItem.Length > 0)
                    {
                        videoUrl = "https://www.youtube.com/embed/" + lastItem;
                    }

                }
            }
            if (videoUrl.Contains("?")){
                videoUrl = videoUrl + "&autoplay=1";
            }
            else
            {
                videoUrl = videoUrl + "?autoplay=1";
            }
       
            webView.Source = new System.Uri(videoUrl);

            
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            bTimerRunning = true;
            pause.Visibility = Visibility.Visible;
            play.Visibility = Visibility.Collapsed;
            dispatcherTimer.Start();
            CommandManager.InvalidateRequerySuggested();
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

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
#if DEBUG
            int val = nTimer-=5;
#else
            int val = nTimer--;
#endif


            if (nTimer < 25) //Hide the top image after 5 seconds
            {
                TopIcon.Visibility = Visibility.Collapsed;
                btnPausePlay.Margin = new Thickness(0, 10, 0, 40);
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
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite(ex.Message);
                    //Debug.WriteLine(ex.Message);
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
            }
            else
            {
                bTimerRunning = true;
                play.Visibility = Visibility.Collapsed;
                pause.Visibility = Visibility.Visible;
                dispatcherTimer.Start();
            }
          //  _parentWindow.Close();
        }

      
    }
}
