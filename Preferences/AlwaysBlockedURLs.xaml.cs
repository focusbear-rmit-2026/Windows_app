using Focus_Bear.Utils;
using Focus_Bear.Utils;
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

namespace Focus_Bear.Preferences
{
    /// <summary>
    /// Interaction logic for AlwaysBlockedURLs.xaml
    /// </summary>
    public partial class AlwaysBlockedURLs : UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;

        FocusBearMainWindow _ParentWindow = null;

        public AlwaysBlockedURLs()
        {
            
        }
        public AlwaysBlockedURLs(FocusBearMainWindow pp)
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

            _ParentWindow = pp;

            try
            {

                Heading.Text = appSettings.ConfigFileStrings["blockedUrlsViewInfo"]["title"].ToString();
                Description.Text = appSettings.ConfigFileStrings["blockedUrlsViewInfo"]["subTitleText"].ToString();

                btnAddURL.ToolTip = appSettings.ConfigFileStrings["focusPreferencesAdvancedInfo"]["toolTipAdd"].ToString();
                btnDeleteURL.ToolTip = appSettings.ConfigFileStrings["focusPreferencesAdvancedInfo"]["toolTipRemove"].ToString();
                ImmediatelyBlockDistractingURLsText.Text = appSettings.ConfigFileStrings["blockedUrlsViewInfo"]["checkBoxText"].ToString();

                btnAddSiteTXT.Text = appSettings.ConfigFileStrings["onboardingBlockedUrlsInfo"]["buttonAddUrlText"].ToString();
                btnDeleteURLTXT.Text = appSettings.ConfigFileStrings["focusPreferencesInfo"]["toolTipRemove"].ToString();

                ImmediatelyBlockDistractingURLsText.ToolTip = appSettings.ConfigFileStrings["blockedUrlsViewInfo"]["checkBoxText"].ToString();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AlwaysBlockedURLs text setting ex - " + ex.Message);
            }

            ImmediatelyBlockDistractingURLs.IsChecked = appSettings.bImmediatelyBlockDistractingSites;

            ListBLockedURLs.ItemsSource = appSettings.arrAlwaysBlockedURLs;
            
        }

        /// <summary>
        /// Save the list at once. It was saved only when the main window was hidden, so an edit was lost if the app
        /// was quit from the tray, killed or crashed first (found by the E2E blocking tests).
        /// </summary>
        private static void SaveNow()
        {
            try { LocalSettingsManager.PutLocalSettings(); }
            catch (Exception ex) { LogWriter.LogWrite("AlwaysBlockedURLs save ex - " + ex.Message); }
        }

        private void btnAddURL_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (appSettings.isExpired)
                {
                    if (appSettings.arrAlwaysBlockedURLs.Count >= 3)
                    {
                        ((App)(Application.Current)).ShowUpgradeToProNag(appSettings.ConfigFileStrings["upgradeToProNagVcInfo"]["titleLimitedBlockUrls"].ToString());
                        return;
                    }
                }

                _ParentWindow.GridShade.Visibility = Visibility.Visible;
                AddWebUrl aa = new AddWebUrl(_ParentWindow, "", false, true);
                aa.Topmost = true;
                bool? DialogResult = aa.ShowDialog();

                _ParentWindow.GridShade.Visibility = Visibility.Collapsed;

                string url = "";

                switch (DialogResult)
                {
                    case true:
                        // Url added
                        url = aa.sURLToAdd;
                        break;
                    case false:
                        // User did not postponed the breaks..normal flow follows
                        break;
                    default:
                        // Indeterminate
                        break;
                }
                if (url.Length > 0)
                {
                  
                    //First check if it is already added
                    var listItems = appSettings.arrAlwaysBlockedURLs.Where(i => i == url);
                    if (listItems.Count() == 0)
                    {
                        appSettings.arrAlwaysBlockedURLs.Add(url);
                        appSettings.CapturePostHog("add-always-blocked-url");
                        appSettings.bHasEditedBlockedURLs = true;
                        SaveNow();
                    }
                    else
                    {
                        string sErr = appSettings.ConfigFileStrings["focusPreferencesInfo"]["urlAlreadyExistsText"].ToString();
                        sErr = sErr.Replace("<APPNAME>", url);
                       
                        FBMessageBox ff = new FBMessageBox(sErr, "Focus Bear");
                        ff.Owner = _ParentWindow;
                        ff.ShowDialog();
                    }
                     
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("btnAddURL_Click in Always blocked urls List" + ex.Message);
            }
        }

        private void btnDeleteURL_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = ListBLockedURLs.SelectedItem as string;
                if (selectedItem != null)
                {
                    appSettings.arrAlwaysBlockedURLs.Remove(selectedItem);
                    appSettings.CapturePostHog("remove-always-blocked-url");
                    appSettings.bHasEditedBlockedURLs = true;
                    SaveNow();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Always blocked urls btnDeleteURL_Click + " + ex.Message);
            }
        }

        private void ListBLockedURLs_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var selectedItem = ListBLockedURLs.SelectedItem as string;

                if (selectedItem != null)
                {
                    _ParentWindow.GridShade.Visibility = Visibility.Visible;
                    AddWebUrl aa = new AddWebUrl(_ParentWindow, selectedItem, true, true);
                    aa.Topmost = true;
                    bool? DialogResult = aa.ShowDialog();

                    _ParentWindow.GridShade.Visibility = Visibility.Collapsed;

                    string url = "";

                    switch (DialogResult)
                    {
                        case true:
                            // Url added
                            url = aa.sURLToAdd;
                            break;
                        case false:
                            return;
                            break;
                        default:
                            // Indeterminate
                            break;
                    }
                    if (url.Length > 0)
                    {
                        if (selectedItem == url) //No change was there in the url, return
                        {
                            return; //nothing to do
                        }


                        //First check if it is already added
                        var listItems = appSettings.arrAlwaysBlockedURLs.Where(i => i == url);
                        if (listItems.Count() == 0)
                        {
                            for (int m = 0; m < appSettings.arrAlwaysBlockedURLs.Count(); m++)
                            {
                                if (appSettings.arrAlwaysBlockedURLs[m] == selectedItem)//Item to edit
                                {
                                    appSettings.arrAlwaysBlockedURLs[m] = url;
                                    appSettings.bHasEditedBlockedURLs = true;
                                    SaveNow();
                                    break;
                                }
                            }
                        }
                        else
                        {
                            string sErr = appSettings.ConfigFileStrings["focusPreferencesInfo"]["urlAlreadyExistsText"].ToString();
                            sErr = sErr.Replace("<APPNAME>", url);
                            
                            FBMessageBox ff = new FBMessageBox(sErr, "Focus Bear");
                            ff.Owner = _ParentWindow;
                            ff.ShowDialog();
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ListBLockedURLs_MouseDoubleClick in ALways blocked URLs List" + ex.Message);
            }

        }

        private void ImmediatelyBlockDistractingURLs_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.bImmediatelyBlockDistractingSites = true;
            appSettings.CapturePostHog("settings-changed-immediately-block-url-enabled");
        }

        private void ImmediatelyBlockDistractingURLs_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bImmediatelyBlockDistractingSites = false;
            appSettings.CapturePostHog("settings-changed-immediately-block-url-disabled");
        }
    }
    
}
