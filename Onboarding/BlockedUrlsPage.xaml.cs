using Focus_Bear.Habits;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.Metrics;
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
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace Focus_Bear.Onboarding
{
    /// <summary>
    /// Interaction logic for BlockedUrlsPage.xaml
    /// </summary>
    public partial class BlockedUrlsPage : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private FocusBearMainWindow _parentWindow;
        public BlockedUrlsPage()
        {
        }
        public BlockedUrlsPage(FocusBearMainWindow parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["titleNew"].ToString(); //"Choose websites to block";
                Bubble1.Text = appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["bubbleText"].ToString(); //"Don’t worry, we don’t track which websites you’re using";

                btnProceedText.Text = appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["buttonProceedText"].ToString(); //"Proceed";

                TxtURL.Tag = "🔍 " + appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["textFieldPlaceholder"].ToString(); //"🔍 Enter URL or keyword to block";

                //Default Always Blocked URLS
                appSettings.arrDistractingURLs.Add(new DistractingURL("facebook.com", true));
                appSettings.arrDistractingURLs.Add(new DistractingURL("x.com", true));
                appSettings.arrDistractingURLs.Add(new DistractingURL("instagram.com", true));
                appSettings.arrDistractingURLs.Add(new DistractingURL("tiktok.com", true));
                appSettings.arrDistractingURLs.Add(new DistractingURL("bbc.com", true));
                appSettings.arrDistractingURLs.Add(new DistractingURL("cnn.com", true));
                appSettings.arrDistractingURLs.Add(new DistractingURL("reddit.com", true));
                appSettings.arrDistractingURLs.Add(new DistractingURL("youtube.com", true));

                appSettings.arrDistractingURLs.CollectionChanged += DistractingURLs_CollectionChanged;

                foreach (var item in appSettings.arrDistractingURLs)
                {
                    item.PropertyChanged += Item_PropertyChanged;
                }

                ListBLockedURLs.ItemsSource = appSettings.arrDistractingURLs;
                SetBlockedText();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("BlockedUrlsPage() EX - " + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;

        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }

        private void btnProceed_Click(object sender, RoutedEventArgs e)
        {
            //we need to add the URLS to always blocked urls
            for (int i = 0; i < appSettings.arrDistractingURLs.Count(); i++)
            {
                if (appSettings.arrDistractingURLs[i].IsItemChecked)
                    appSettings.arrAlwaysBlockedURLs.Add(appSettings.arrDistractingURLs[i].sURL);
                else
                    appSettings.bHasEditedBlockedURLs = true;
            }

            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._BLOCKED_APPS_);
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            var listItems = appSettings.arrDistractingURLs.Where(i => i.sURL.ToLower() == TxtURL.Text.ToLower());
            if (listItems.Count() == 0)
            {
                appSettings.arrDistractingURLs.Add(new DistractingURL(TxtURL.Text.ToLower(), true));
                ListBLockedURLs.ScrollIntoView(ListBLockedURLs.Items[ListBLockedURLs.Items.Count - 1]); //scroll to last
                TxtURL.Text = "";
                TxtURL.Focus();
                appSettings.bHasEditedBlockedURLs = true;
                appSettings.CapturePostHog("new-block-url-added");
            }
        }

        private void DistractingURLs_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (DistractingURL item in e.NewItems)
                {
            
                    item.PropertyChanged += Item_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (DistractingURL item in e.OldItems)
                {
                    item.PropertyChanged -= Item_PropertyChanged;
                }
            }
            SetBlockedText();
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DistractingURL.IsItemChecked))
            {
                var item = (DistractingURL)sender;
                SetBlockedText();
            }
        }

        private void SetBlockedText()
        {
            string sCount = appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["blockSitesCountText"].ToString();  // "<COUNT> websites selected (you can change this later)";  

            var listItems = appSettings.arrDistractingURLs.Where(i => i.IsItemChecked == true);
            if (listItems.Count() == 1)
            {
                sCount = appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["oneSiteBlockText"].ToString();  //"1 website selected (you can change this later)";
            }
            else
            {
                sCount = appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["blockSitesCountText"].ToString();  // "<COUNT> websites selected (you can change this later)";
                sCount = sCount.Replace("<COUNT>", listItems.Count().ToString());
            }

            CountBlockedSites.Text = sCount;
        }
        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                appSettings.CapturePostHog("onboarding-blocked-urls-shown");
            }
        }
    }
}
