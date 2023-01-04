using Focus_Bear.Utils;
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
using System.Windows.Shapes;
using System.ComponentModel;
using Microsoft.Web.WebView2.Core;
using System.Diagnostics;

namespace Focus_Bear.Login
{
    /// <summary>
    /// Interaction logic for OnboardingVideo.xaml
    /// </summary>
    public partial class OnboardingVideo : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        public OnboardingVideo()
        {
            InitializeComponent();

            SetWebView();

            Heading.Text = appSettings.ConfigFileStrings["welcomeInfo"]["preloadFocusModesDoneText"].ToString();

            string videoUrl = appSettings.ConfigFileStrings["welcomeInfo"]["welcomeVideoUrl"].ToString();

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

            webView.Source = new System.Uri(videoUrl);

            btnContinueTxt.Text = appSettings.ConfigFileStrings["preloadFocusModesViewInfo"]["buttonContinueText"].ToString();

           this.Closing += new CancelEventHandler(OnWindowClosing);

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

        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public void OnWindowClosing(object sender, CancelEventArgs e)
        {
            webView.Source = new System.Uri("about:blank");
        }
    }
}
