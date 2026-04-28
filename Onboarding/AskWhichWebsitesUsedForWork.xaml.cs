using Focus_Bear.Habits;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
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
    /// Interaction logic for AskWhichWebsitesUsedForWork.xaml
    /// </summary>
    public partial class AskWhichWebsitesUsedForWork : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private FocusBearMainWindow _parentWindow;
        public AskWhichWebsitesUsedForWork()
        {
        }
        public AskWhichWebsitesUsedForWork(FocusBearMainWindow parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["onboardingAllowedUrlsVcInfo"]["title"].ToString(); //"What websites do you use for work?";
                Description.Text = appSettings.ConfigFileStrings["onboardingAllowedUrlsVcInfo"]["subtitle"].ToString(); //"We'll use these to kick off your focus sessions.";
                Choose.Text = appSettings.ConfigFileStrings["onboardingAllowedUrlsVcInfo"]["titleChooseFromList"].ToString(); //"Choose from suggested websites:";
                Bubble1.Text = appSettings.ConfigFileStrings["onboardingAllowedUrlsVcInfo"]["bubbleText"].ToString(); //"When you go to one of these sites, i'll start a focus session for you";

                btnThatsAllText.Text = appSettings.ConfigFileStrings["onboardingAllowedUrlsVcInfo"]["buttonContinue"].ToString(); //"That’s all";
                btnSkipText.Text = appSettings.ConfigFileStrings["onboardingAllowedUrlsVcInfo"]["buttonSkip"].ToString(); //"Skip";

                TxtDetails.Tag = "🔍 " + appSettings.ConfigFileStrings["onboardingAllowedUrlsVcInfo"]["textFieldPlaceholder"].ToString(); //"🔍 Add a URL or keyword";

                ListAllowedURLs.ItemsSource = appSettings.AllowedURLsWhileOnboarding;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AskWhichWebsitesUsedForWork() EX - " + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;

        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }

        private void btnThatsAlll_Click(object sender, RoutedEventArgs e)
        {
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._BLOCKED_URLS_);
        }

        private void btnSkip_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("onboarding-allowed-urls-skipped");
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._BLOCKED_URLS_);
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            var listItems = appSettings.AllowedURLsWhileOnboarding.Where(i => i.sURL.ToLower() == TxtDetails.Text.ToLower());
            if (listItems.Count() == 0)
            {
                appSettings.AllowedURLsWhileOnboarding.Add(new DistractingURL(TxtDetails.Text.ToLower(), true));
                ListAllowedURLs.ScrollIntoView(ListAllowedURLs.Items[ListAllowedURLs.Items.Count - 1]); //scroll to last
                TxtDetails.Text = "";
                TxtDetails.Focus();
                appSettings.CapturePostHog("new-allow-url-added");
            }
        }
        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                appSettings.CapturePostHog("onboarding-allowed-urls-shown");
            }
        }
    }
}
