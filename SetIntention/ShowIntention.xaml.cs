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

namespace Focus_Bear.SetIntention
{
    /// <summary>
    /// Interaction logic for ShowIntention.xaml
    /// </summary>
    public partial class ShowIntention : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        string sActiveURL = "";
        IntPtr HWND = IntPtr.Zero;
        public string sFullURLForBlockedPage = "";
        public ShowIntention()
        {
            InitializeComponent();

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }

            this.Topmost = true;

            var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
            this.Left = (desktopWorkingArea.Right - this.Width);
            this.Top = (desktopWorkingArea.Bottom - this.Height);

            try
            {
                string sHeading = appSettings.ConfigFileStrings["intentionBlockedUrlsVcInfo"]["title"].ToString();
                sHeading = sHeading.Replace("<SITE>", sActiveURL);
                Heading.Text = sHeading;

                btnBlockItText.Text = appSettings.ConfigFileStrings["intentionBlockedUrlsVcInfo"]["buttonBlockText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowIntention text setting ex - " + ex.Message);
            }
        }

        public void Show_ShowIntentionWindow(IntPtr hWnd, string sURL, string sIntention )
        {
            HWND = hWnd;
            sFullURLForBlockedPage = sURL;
            sActiveURL = sURL;

            string sHeading = appSettings.ConfigFileStrings["intentionBlockedUrlsVcInfo"]["title"].ToString();
         
            if (sURL.Length > 0)
            {
                try
                {
                    Uri myUri = new Uri(sURL);
                    string host = myUri.Host; //fails if there is no http://
                    sActiveURL = host;
                }
                catch (Exception ex)
                {
                    /*
                    This constructor initializes a new instance of the UriBuilder class with the Fragment, Host, Path, Port, Query, Scheme, and Uri properties set as specified in uri.
                    If uri does not specify a scheme, the scheme defaults to "http:".
                    */
                    Uri myUri = new UriBuilder(sURL).Uri;
                    sActiveURL = myUri.Host;
                }
            }

            sHeading = sHeading.Replace("<SITE>", sActiveURL);
            this.Show(); //Will keep this window hidden will we get response from API

            Heading.Text = sHeading;
            TxtIntention.Text = sIntention;
        }

        private void btnBlockIt_Click(object sender, RoutedEventArgs e)
        {
            appSettings.TemporarryBlockedURLsWithSetIntention.Add(sActiveURL.ToLower());

            //Remove it from allowed urls list
            var allowedURLs = appSettings.TemporaryAllowedURLsWithSetIntention.Where(i => i.sURL.ToLower() == sActiveURL.ToLower());
            if (allowedURLs.Count() != 0)
            {
                //found in allowed url, remove it
                appSettings.TemporaryAllowedURLsWithSetIntention.Remove(allowedURLs.First());
            }
            
            Hide();
            ProcessBrowserURLs.suppressURLForFirefox(HWND, sFullURLForBlockedPage, "blocked"); //here we will send the full URL
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;

            //Remove it from allowed urls list
            var allowedURLs = appSettings.TemporaryAllowedURLsWithSetIntention.Where(i => i.sURL.ToLower() == sActiveURL.ToLower());
            if (allowedURLs.Count() != 0)
            {
                //found in allowed url, remove it
                allowedURLs.First().bIsShowIntentionWindowClosedForThisURL = true;
            }

            Hide();
        }
    }
}
