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

            webView.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["askOnboardingVideoVcInfo"]["subTitle"].ToString();
               

                btnContinueTxt.Text = appSettings.ConfigFileStrings["preloadFocusModesViewInfo"]["buttonContinueText"].ToString();

                this.Closing += new CancelEventHandler(OnWindowClosing);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingVideo  EX - " + ex.Message);
            }

           

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
                LogWriter.LogWrite("onboarding video SetWebView() EX - " + ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }

        private void CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                try
                {


                    //this.webView.NavigationStarting += NavigationStarting;
                    //this.webView.NavigationCompleted += NavigationCompleted;
                    webView.CoreWebView2.Settings.AreHostObjectsAllowed = true;
                    webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                    webView.CoreWebView2.Settings.IsScriptEnabled = true;
                    webView.CoreWebView2.Settings.IsWebMessageEnabled = true;

                    webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                    webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

                    // webView.WebMessageReceived += webView_WebMessageReceived;

                    try
                    {
                       
                        string videoUrl = appSettings.ConfigFileStrings["WindowsSpecific"]["welcomeVideoUrl"].ToString();

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
 
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("OnboardingVideo  EX - " + ex.Message);
                    }


                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("CoreWebView2InitializationCompleted()" + ex.Message);
                }
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            if (null != e)
                e.Handled = true;


            var source = PresentationSource.FromVisual(this);

            if (null != source && null != source.CompositionTarget)
            {
                var matrix = source.CompositionTarget.TransformToDevice;

                var dpiTransform = new ScaleTransform(1 / matrix.M11, 1 / matrix.M22);

                if (dpiTransform.CanFreeze)
                    dpiTransform.Freeze();

                LayoutTransform = dpiTransform;
                this.Width = this.Width * dpiTransform.ScaleX;
                this.Height = this.Height * dpiTransform.ScaleY;

                var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
                this.Left = (desktopWorkingArea.Width - this.Width) / 2;
                this.Top = (desktopWorkingArea.Height - this.Height) / 2;


            }
        }
    }
}
