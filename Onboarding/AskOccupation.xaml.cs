using Focus_Bear.API;
using Focus_Bear.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Runtime.CompilerServices;
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
    /// Interaction logic for AskOccupation.xaml
    /// </summary>
    public partial class AskOccupation : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private FocusBearMainWindow _parentWindow;

        public AskOccupation()
        {
        }
        public AskOccupation(FocusBearMainWindow parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["askOccupationVcInfo"]["subTitle"].ToString(); //"What type of work do you do?";
                Description.Text = appSettings.ConfigFileStrings["askOccupationVcInfo"]["descriptionNew"].ToString(); //"I’ll suggest which websites to block and which ones are productive.";

                Bubble1.Text = appSettings.ConfigFileStrings["askOccupationVcInfo"]["bubbleText"].ToString(); //"Tell me your profession and I’ll find them all!";

                btnThatsMeText.Text = appSettings.ConfigFileStrings["askOccupationVcInfo"]["buttonContinue"].ToString(); //"That’s me!";
                btnSkipText.Text = appSettings.ConfigFileStrings["welcomeInfo"]["buttonSkipText"].ToString();//"Skip";

                TxtDetails.Visibility = Visibility.Collapsed;

                RadioButton1.Content = appSettings.ConfigFileStrings["askOccupationVcInfo"]["occupationStudent"].ToString();
                RadioButton2.Content = appSettings.ConfigFileStrings["askOccupationVcInfo"]["occupationBusinessOwner"].ToString();
                RadioButton3.Content = appSettings.ConfigFileStrings["askOccupationVcInfo"]["occupationEmployee"].ToString();
                RadioButton4.Content = appSettings.ConfigFileStrings["askOccupationVcInfo"]["occupationFreelancer"].ToString();
                RadioButton5.Content = appSettings.ConfigFileStrings["askOccupationVcInfo"]["occupationOther"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AskOccupation() EX - " + ex.Message);
            }
            Page_Theme();
            App.ThemeChanged += Page_Theme;

        }
        private void Page_Theme()
        {
            app.ApplyThemeToPage(this);
        }


        private async void btnThatsMe_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            TxtDetails.IsEnabled = false;
            btnThatsMe.IsEnabled = false;
            btnSkip.IsEnabled = false;
            RadioButton1.IsEnabled = false;
            RadioButton2.IsEnabled = false;
            RadioButton3.IsEnabled = false;
            RadioButton4.IsEnabled = false;
            RadioButton5.IsEnabled = false;

            FocusBearAPIClient client = new FocusBearAPIClient();
            // Each radio stands for its own occupation (every branch used to test the first radio).
            int selected = RadioButton1.IsChecked == true ? 1 : RadioButton2.IsChecked == true ? 2 : RadioButton3.IsChecked == true ? 3
                         : RadioButton4.IsChecked == true ? 4 : 5;
            string sParam = FocusBear.Core.Onboarding.OnboardingChoices.OccupationQuery(TxtDetails.Text, selected);
            var Response = await client.GenerateOccupationRelevantSites(sParam);
            int mRes = int.Parse(Response.ToString());
            if (mRes == 1)
            {   
                if(appSettings.sGeneratedOccupationRelevantSites.Length > 0)
                {
                    try
                    {
                        var userSettings = JObject.Parse(appSettings.sGeneratedOccupationRelevantSites);

                        AddUrlsFromJsonArray(userSettings, "user_relevant_sites", appSettings.AllowedURLsWhileOnboarding, true);
                        AddUrlsFromJsonArray(userSettings, "user_typical_distractions", appSettings.DistractingURLsWhileOnboarding, true);
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("AskOccupation() EX - " + ex.Message);
                    }
                }
                     
            }

            TxtDetails.IsEnabled = true;
            btnThatsMe.IsEnabled = true;
            btnSkip.IsEnabled = true;
            RadioButton1.IsEnabled = true;
            RadioButton2.IsEnabled = true;
            RadioButton3.IsEnabled = true;
            RadioButton4.IsEnabled = true;
            RadioButton5.IsEnabled = true;

            Mouse.OverrideCursor = null;

            appSettings.CapturePostHog("occupation-selected");

            // Blocked URLs/Apps now come before Allowed URLs (flow reorder to match mac).
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._BLOCKED_URLS_);
        }
        private void AddUrlsFromJsonArray(JObject json, string key, IList<DistractingURL> targetList, bool isAllowed)
        {
            if (json?[key] is JArray array && array.Count > 0)
            {
                foreach (var token in array)
                {
                    var url = token?.ToString();
                    // Going Back and answering again must not list the same suggestion twice.
                    if (!string.IsNullOrWhiteSpace(url) && !FocusBear.Core.Onboarding.OnboardingChoices.Offers(targetList.Select(u => u.sURL), url))
                    {
                        targetList.Add(new DistractingURL(url, isAllowed));
                    }
                }
            }
        }
        private void btnSkip_Click(object sender, RoutedEventArgs e)
        {
            appSettings.CapturePostHog("ask-occupation-skipped");
            // Blocked URLs/Apps now come before Allowed URLs (flow reorder to match mac).
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._BLOCKED_URLS_);
        }
        private void RadioButton1_Checked(object sender, RoutedEventArgs e) //student
        {
            btnThatsMe.IsEnabled = true;
            TxtDetails.Visibility = Visibility.Visible;
            TxtDetails.Text = "";
            TxtDetails.Tag = "✏️ " + appSettings.ConfigFileStrings["askOccupationVcInfo"]["placeHolderStudy"].ToString();
            //"✏️ What do you study?";
        }
        private void RadioButton2_Checked(object sender, RoutedEventArgs e) //business owner
        {
            btnThatsMe.IsEnabled = true;
            TxtDetails.Visibility = Visibility.Visible;
            TxtDetails.Text = "";
            TxtDetails.Tag = "✏️ " + appSettings.ConfigFileStrings["askOccupationVcInfo"]["placeHolderBusinessOwner"].ToString(); //"✏️ What kind of business do you run?";
        }

        private void RadioButton3_Checked(object sender, RoutedEventArgs e) //employee
        {
            btnThatsMe.IsEnabled = true;
            TxtDetails.Visibility = Visibility.Visible;
            TxtDetails.Text = "";
            TxtDetails.Tag = "✏️ " + appSettings.ConfigFileStrings["askOccupationVcInfo"]["placeHolderEmployee"].ToString(); //"✏️ What do you do for work?";
        }

        private void RadioButton4_Checked(object sender, RoutedEventArgs e)
        {
            btnThatsMe.IsEnabled = true;
            TxtDetails.Visibility = Visibility.Visible;
            TxtDetails.Text = "";
            TxtDetails.Tag = "✏️ " + appSettings.ConfigFileStrings["askOccupationVcInfo"]["placeHolderFreelancer"].ToString(); //"✏️ What kind of work do you do as a freelancer?";
        }
        private void RadioButton5_Checked(object sender, RoutedEventArgs e)
        {
            btnThatsMe.IsEnabled = true;
            TxtDetails.Visibility = Visibility.Visible;
            TxtDetails.Text = "";
            TxtDetails.Tag = "✏️ " + appSettings.ConfigFileStrings["askOccupationVcInfo"]["placeHolderOther"].ToString(); //"✏️ What do you spend most of your time on?";
        }
        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                appSettings.CapturePostHog("ask-occupation-screen-shown");
            }
        }

    }
}
