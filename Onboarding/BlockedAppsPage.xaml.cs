using Focus_Bear.Habits;
using Focus_Bear.Models;
using Focus_Bear.Preferences;
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
    /// Interaction logic for BlockedAppsPage.xaml
    /// </summary>
    public partial class BlockedAppsPage : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;
        private FocusBearMainWindow _parentWindow;
        public BlockedAppsPage()
        {
        }
        public BlockedAppsPage(FocusBearMainWindow parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["onboardingBlockedAppsVcInfo"]["title"].ToString(); //"Choose apps to block";
                Bubble1.Text = appSettings.ConfigFileStrings["onboardingBlockedAppsVcInfo"]["bubbleText"].ToString(); //"Don’t worry, we don’t track which apps you’re using";

                btnAddText.Text = appSettings.ConfigFileStrings["onboardingBlockedAppsVcInfo"]["buttonAdd"].ToString(); //"Add";
                btnRemoveText.Text = appSettings.ConfigFileStrings["onboardingBlockedAppsVcInfo"]["buttonRemove"].ToString(); //"Remove";
                btnProceedText.Text = appSettings.ConfigFileStrings["onboardingBlockedAppsVcInfo"]["buttonProceed"].ToString(); //"Proceed";
                btnSkipText.Text = appSettings.ConfigFileStrings["onboardingAllowedUrlsVcInfo"]["buttonSkip"].ToString(); //"Skip"; //"Skip";

                ListBlockedApps.ItemsSource = appSettings.arrAlwaysBlockedApps;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("BlockedAppsPage() EX - " + ex.Message);
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
            // Allowed URLs now comes after Blocked URLs/Apps (flow reorder to match mac).
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._ALLOWED_URLS_);
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //_parentWindow.RepositionWindow();
                _parentWindow.GridShade.Visibility = Visibility.Visible;
                AddApp aa = new AddApp(_parentWindow, false);
                aa.Topmost = true;
                bool? DialogResult = aa.ShowDialog();

                _parentWindow.GridShade.Visibility = Visibility.Collapsed;

                if (DialogResult.Value == false)
                {
                    ShowAddAppManuallyWindow();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("arrAlwaysBlockedApps btnAddApp_Click ex - " + ex.Message);
            }
        }
        private void ShowAddAppManuallyWindow()
        {
            LogWriter.LogWrite("arrAlwaysBlockedApps ShowAddAppManuallyWindow()");

            try
            {
                _parentWindow.RepositionWindow();
                _parentWindow.GridShade.Visibility = Visibility.Visible;
                AddAppsManually aa = new AddAppsManually(_parentWindow);
                aa.Topmost = true;
                bool? DialogResult = aa.ShowDialog();

                _parentWindow.GridShade.Visibility = Visibility.Collapsed;

                string sApps = "";

                switch (DialogResult)
                {
                    case true:
                        sApps = aa.sAppsToAdd;
                        break;
                    case false:
                        return;
                    default:
                        return;
                }

                // Files picked via "Select File" — each stored with the exes from its install folder.
                foreach (var custom in aa.SelectedCustomApps)
                {
                    if (!appSettings.arrAlwaysBlockedApps.Any(i => i.AppName.ToLower() == custom.AppName.ToLower()))
                    {
                        appSettings.arrAlwaysBlockedApps.Add(custom);
                        appSettings.CapturePostHog("added-always-blocked-app-manually");
                    }
                }

                // Typed names — added alongside any selected files.
                if (!string.IsNullOrEmpty(sApps))
                {
                    string[] apps = sApps.Split(',');
                    bool bAppAdded = false;
                    foreach (string app in apps)
                    {
                        string trimmedApp = app.Trim().Trim('"');
                        if (!string.IsNullOrEmpty(trimmedApp))
                        {
                            string appName = System.IO.Path.GetFileNameWithoutExtension(trimmedApp);
                            string exeName = System.IO.Path.GetFileName(trimmedApp);
                            var listItems = appSettings.arrAlwaysBlockedApps.Where(i => i.AppName.ToLower() == appName.ToLower());
                            if (listItems.Count() == 0)
                            {
                                appSettings.arrAlwaysBlockedApps.Add(new AppDetails(appName, exeName));
                                bAppAdded = true;
                            }
                        }
                    }
                    if (bAppAdded)
                        appSettings.CapturePostHog("added-always-blocked-app-manually");
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("arrAlwaysBlockedApps ShowAddAppManuallyWindow ex - " + ex.Message);
            }
        }
        private void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = ListBlockedApps.SelectedItem as AppDetails;
                if (selectedItem != null)
                {
                    appSettings.arrAlwaysBlockedApps.Remove(selectedItem);
                    appSettings.CapturePostHog("remove-always-blocked-app");
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("arrAlwaysBlockedApps btnDeleteApp_Click ex - " + ex.Message);
            }
        }
        private void btnSkip_Click(object sender, RoutedEventArgs e)
        {
            // Allowed URLs now comes after Blocked URLs/Apps (flow reorder to match mac).
            _parentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._ALLOWED_URLS_);
        }

        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                appSettings.CapturePostHog("onboarding-blocked-apps-shown");
            }
        }
    }
}
