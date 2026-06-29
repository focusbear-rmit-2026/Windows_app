using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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

namespace Focus_Bear.Preferences.SettingsTab
{
    /// <summary>
    /// Interaction logic for Strictness.xaml
    /// </summary>
    public partial class Strictness : UserControl
    {
        FocusBearMainWindow _ParentWindow = null;

        AppSettings appSettings = AppSettings.GetInstance;
        public Strictness()
        {
           
        }
        public Strictness(FocusBearMainWindow pp)
        {
            InitializeComponent();
            _ParentWindow = pp;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["settingsNew"]["strictnessSettingTitle"].ToString();
                btnPasswordText.Text = appSettings.ConfigFileStrings["settingsNew"]["passwordSettingTitle"].ToString();
                btnFocusModeText.Text = appSettings.ConfigFileStrings["settingsNew"]["focusModeSettingTitle"].ToString();
                btnDistractingSitesText.Text = appSettings.ConfigFileStrings["blockedUrlsViewInfo"]["title"].ToString();
                btnAlwaysAllowedAppsText.Text = appSettings.ConfigFileStrings["alwaysAllowedAppsViewInfo"]["title"].ToString(); 
                btnAlwaysBlockedAppsText.Text = appSettings.ConfigFileStrings["blockedAppsViewInfo"]["title"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Strictness text setting ex - " + ex.Message);
            }

        }

        private void btnPassword_Click(object sender, RoutedEventArgs e)
        {
            if (appSettings.isPasswordSet)
            {
                if (!appSettings.bPasswordRequireInChangeSettings)
                {
                    AskPassword pwdWnd = new AskPassword();
                    pwdWnd.Owner = _ParentWindow;
                    _ParentWindow.GridShade.Visibility = Visibility.Visible;

                    bool? bResult = pwdWnd.ShowDialog();

                    _ParentWindow.GridShade.Visibility = Visibility.Collapsed;

                    if (bResult.Value)
                    {
                        //Password is correct
                    }
                    else
                    {
                        e.Handled = true;
                        return;
                    }
                }
            }

            // Hide main menu
            MainMenuPanel.Visibility = Visibility.Collapsed;
            SubViewContainer.Visibility = Visibility.Visible;

            SubViewHeading.Text = appSettings.ConfigFileStrings["settingsNew"]["passwordSettingTitle"].ToString();
            // Load the DistractingSites class here
            ContentArea.Content = new Password(_ParentWindow);
        }

        private void btnFocusMode_Click(object sender, RoutedEventArgs e)
        {
            // Hide main menu
            MainMenuPanel.Visibility = Visibility.Collapsed;
            SubViewContainer.Visibility = Visibility.Visible;

            SubViewHeading.Text = appSettings.ConfigFileStrings["settingsNew"]["focusModeSettingTitle"].ToString();
            // Load the DistractingSites class here
            ContentArea.Content = new FocusModeSetting();
        }

        private void btnDistractingSites_Click(object sender, RoutedEventArgs e)
        {
            // Hide main menu
            MainMenuPanel.Visibility = Visibility.Collapsed;
            SubViewContainer.Visibility = Visibility.Visible;

            SubViewHeading.Text = appSettings.ConfigFileStrings["blockedUrlsViewInfo"]["title"].ToString();
            // Load the DistractingSites class here
            ContentArea.Content = new AlwaysBlockedURLs(_ParentWindow);
        }

        private void btnAlwaysAllowedApps_Click(object sender, RoutedEventArgs e)
        {
            // Hide main menu
            MainMenuPanel.Visibility = Visibility.Collapsed;
            SubViewContainer.Visibility = Visibility.Visible;

            SubViewHeading.Text = appSettings.ConfigFileStrings["alwaysAllowedAppsViewInfo"]["title"].ToString();
            // Load the AlwaysAllowedApps class here
            ContentArea.Content = new AlwaysAllowedApps(_ParentWindow);
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            // Hide subview, show main menu
            SubViewContainer.Visibility = Visibility.Collapsed;
            MainMenuPanel.Visibility = Visibility.Visible;

            // Optionally clear the content
            ContentArea.Content = null;
        }

        private void btnAlwaysBlockedApps_Click(object sender, RoutedEventArgs e)
        {
            // Hide main menu
            MainMenuPanel.Visibility = Visibility.Collapsed;
            SubViewContainer.Visibility = Visibility.Visible;

            SubViewHeading.Text = appSettings.ConfigFileStrings["blockedAppsViewInfo"]["title"].ToString(); 
            // Load the AlwaysAllowedApps class here
            ContentArea.Content = new AlwaysBlockedApps(_ParentWindow);
        }
    }
}
