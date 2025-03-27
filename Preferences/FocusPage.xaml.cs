using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
    /// Interaction logic for FocusPage.xaml
    /// </summary>
    /// 
    
    public partial class FocusPage : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;

        PreferencesWindow _ParentWindow = null;
        SolidColorBrush BackgroundColor = null;

        public FocusPage()
        {
        }
        public FocusPage(PreferencesWindow pp)
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
                Heading.Text = appSettings.ConfigFileStrings["focusPreferencesInfo"]["titleFocusType"].ToString();
                AllowAllApps.Content = appSettings.ConfigFileStrings["focusPreferencesInfo"]["titleAllowedAllApps"].ToString();
                AllowSelectedApps.Content = appSettings.ConfigFileStrings["focusPreferencesInfo"]["titleAllowedSelectedApps"].ToString();

                BlockEverything.Content = appSettings.ConfigFileStrings["focusPreferencesInfo"]["titleAllowUrlListApproch"].ToString();
                AllowEverything.Content = appSettings.ConfigFileStrings["focusPreferencesInfo"]["titleBlockUrlListApproch"].ToString();

                btnDeleteFocusModeTXT.Text = appSettings.ConfigFileStrings["focusPreferencesInfo"]["buttonDeleteFocusTypeText"].ToString();
                btnDuplicateFocusModeTXT.Text = appSettings.ConfigFileStrings["focusPreferencesInfo"]["duplicateText"].ToString();
                btnAdvancedTXT.Text = appSettings.ConfigFileStrings["focusPreferencesInfo"]["buttonAdvancedText"].ToString();
                btnAddFocusModeTXT.Text = appSettings.ConfigFileStrings["focusPreferencesInfo"]["buttonAddFocusTypeText"].ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("FocusPage() EX - " + ex.Message);
            }


            ListFocusModes.ItemsSource = appSettings.arrFocusModes;

            ListFocusModes.SelectedIndex = 0;

            //changeTheme();
        }

        // Use dynamic resource source = {dynamic plusImg} instead.
        //private void changeTheme()
        //{
        //    // Dynamic background color for dark mode in .cs
        //    BackgroundColor = (SolidColorBrush)FindResource("BackgroundColor");
        //    LogWriter.LogWrite("Current theme focus " + appSettings.sTheme);

        //    if (appSettings.sTheme == "Light")
        //    {
        //        btnAddImg.Source = new BitmapImage(new Uri("../Images/plus.png", UriKind.Relative));
        //        btnMinusImg.Source = new BitmapImage(new Uri("../Images/minus.png", UriKind.Relative));
        //        //btnAddUrlImg.Source = new BitmapImage(new Uri("../Images/plus.png", UriKind.Relative));
        //        //btnMinusUrlImg.Source = new BitmapImage(new Uri("../Images/minus.png", UriKind.Relative));
        //    }
        //    else if (appSettings.sTheme == "Dark")
        //    {
        //        btnAddImg.Source = new BitmapImage(new Uri("../Images/plus_dark.png", UriKind.Relative));
        //        btnMinusImg.Source = new BitmapImage(new Uri("../Images/minus_dark.png", UriKind.Relative));
        //        //btnAddUrlImg.Source = new BitmapImage(new Uri("../Images/plus_dark.png", UriKind.Relative));
        //        //btnMinusUrlImg.Source = new BitmapImage(new Uri("../Images/minus_dark.png", UriKind.Relative));

        //    }
        //}

        private void btnDeleteFocusMode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = ListFocusModes.SelectedItem as FocusMode;
                if (selectedItem != null)
                {
                    if (selectedItem.isDefault || selectedItem.isMeetings || selectedItem.isLockedFocus || selectedItem.isRelaxFocus || selectedItem.isDeepWorkFocus)
                    {
 
                        FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["focusPreferencesInfo"]["focusCanNotDeleteSubTitle"].ToString(), "Focus Bear");
                        ff.Owner = _ParentWindow;
                        ff.ShowDialog();
                        return;
                    }

                    if (selectedItem.isOn)
                    {
                        FBMessageBox ff1 = new FBMessageBox(appSettings.ConfigFileStrings["focusPreferencesInfo"]["focusModeOnText"].ToString(), "Focus Bear");
                        ff1.Owner = _ParentWindow;
                        ff1.ShowDialog();
                        return;
                    }

                    FBMessageBox ff2 = new FBMessageBox(appSettings.ConfigFileStrings["focusPreferencesInfo"]["deleteFocusTypeMessageText"].ToString(), "Focus Bear", MessageBoxButton.YesNo);
                    ff2.Owner = _ParentWindow;
                    ff2.ShowDialog();
                    if (ff2.btnClicked == MessageBoxResult.Yes)
                    {
                        appSettings.arrFocusModes.Remove(selectedItem);

                        appSettings.CapturePostHog("delete-focus-mode");

                        //call the API and remove from backend
                        FocusBearAPIClient client = new FocusBearAPIClient();

                        LogWriter.LogWrite("attempt to AddFocusModeToArrayAndUploadtoAPI");
                        var response = client.DeleteFocusMode(selectedItem.id);
    
                        //ALso select the new index
                        ListFocusModes.SelectedIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

        }

        private void btnDuplicateFocusMode_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnDuplicateFocusMode_Click");

            var selectedFocusMode = ListFocusModes.SelectedItem as FocusMode;
            if (selectedFocusMode != null)
            {
                // We will need to check same name
                
                string oldName = selectedFocusMode.name;
                int count = 1;
                string newName = oldName + " " + count.ToString();

                bool isNameAvailable = false;
                while( !isNameAvailable )
                {
                    var listItems = appSettings.arrFocusModes.Where(i => i.name.ToLower() == newName.ToLower());
                    if (listItems.Count() == 0)
                        isNameAvailable = true;
                    else
                    {
                        count++;
                        newName = oldName + " " + count.ToString();
                    }
                }
                appSettings.CapturePostHog("duplicate-focus-mode");
                //add to focus modes  array and Upload to api
                AddFocusModeToArrayAndUploadtoAPI(new FocusMode(System.Guid.NewGuid().ToString(), newName, selectedFocusMode.arrAllowedUrls, selectedFocusMode.arrBlockedUrls, selectedFocusMode.arrAllowedAppsWin, selectedFocusMode.color, false, selectedFocusMode.isAllowedAllApps, selectedFocusMode.isAllowedListApproach, false, false, false, false, false));
            }
        }
        void AddFocusModeToArrayAndUploadtoAPI(FocusMode ff)
        {
            LogWriter.LogWrite("AddFocusModeToArrayAndUploadtoAPI()");
            try
            {
                //add to focus modes  array
                appSettings.arrFocusModes.Add(ff);

                //Select the new item in the FOcus list.
                ListFocusModes.SelectedIndex = appSettings.arrFocusModes.Count() - 1;
                //Upload to api
                FocusBearAPIClient client = new FocusBearAPIClient();

                LogWriter.LogWrite("attempt to AddFocusModeToArrayAndUploadtoAPI");
                var userSettingsResponse = client.AddFocusMode(ff);

            }
            catch (Exception ex)
            {
                Debug.WriteLine("AddFocusModeToArrayAndUploadtoAPI()" + ex.Message);
            }
        }
        private void AllowAllApps_Checked(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("AllowAllApps_Checked");

            ListApps.IsEnabled = false;
            btnAddAllowedApp.IsEnabled = false;
            btnAddDeleteAllowedApp.IsEnabled = false;

            ListApps.Visibility = Visibility.Hidden;
            btnAddAllowedApp.Visibility = Visibility.Hidden;
            btnAddDeleteAllowedApp.Visibility = Visibility.Hidden;


            SelectedAppsListBG.Background = BackgroundColor;
            SelectedAppsListBGSeperator.Background = BackgroundColor;
            var selectedFocusMode = ListFocusModes.SelectedItem as FocusMode;
            if (selectedFocusMode != null)
            {
                selectedFocusMode.isAllowedAllApps = true;
            }
        }

        private void AllowSelectedApps_Checked(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("AllowSelectedApps_Checked");

            ListApps.IsEnabled = true;
            btnAddAllowedApp.IsEnabled = true;
            btnAddDeleteAllowedApp.IsEnabled = true;

            ListApps.Visibility = Visibility.Visible;
            btnAddAllowedApp.Visibility = Visibility.Visible;
            btnAddDeleteAllowedApp.Visibility = Visibility.Visible;

            SelectedAppsListBG.Background = BackgroundColor;
            SelectedAppsListBGSeperator.Background = BackgroundColor;

            var selectedFocusMode = ListFocusModes.SelectedItem as FocusMode;
            if (selectedFocusMode != null)
            {
                selectedFocusMode.isAllowedAllApps = false;
            }
        }

        private void BlockEverything_Checked(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("BlockEverything_Checked");

            HowDoYouWant.Text = appSettings.ConfigFileStrings["focusPreferencesInfo"]["allowUrlsQuestionText"].ToString();

            // Remove the handler
            //do not want to raise SelectionChanged event programmatically here
            var selectedFocusMode = ListFocusModes.SelectedItem as FocusMode;
            if (selectedFocusMode != null)
            {
                {
                    ListURLs.IsEnabled = true;
                    btnAddAllowedURLs.IsEnabled = true;
                    btnAddDeleteAllowedURLs.IsEnabled = true;

                    ListURLs.Visibility = Visibility.Visible;
                    btnAddAllowedURLs.Visibility = Visibility.Visible;
                    btnAddDeleteAllowedURLs.Visibility = Visibility.Visible;


                    SelectedURLsListBG.Background = BackgroundColor;
                    SelectedURLsListBGSeperator.Background = BackgroundColor;
                }

                selectedFocusMode.isAllowedListApproach = true;
              
                ListURLs.ItemsSource = selectedFocusMode.arrAllowedUrls;
            }

            btnAdvanced.IsEnabled = true; //Enable the advanced button to show the blocked list
        }

        private void AllowEverything_Checked(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("AllowEverything_Checked");

            HowDoYouWant.Text = appSettings.ConfigFileStrings["focusPreferencesInfo"]["blockUrlsQuestionText"].ToString();

            var selectedFocusMode = ListFocusModes.SelectedItem as FocusMode;
            if (selectedFocusMode != null)
            {
             
                {
                   
                    if (!selectedFocusMode.isRelaxFocus)
                    {
                        ListURLs.IsEnabled = true;
                        btnAddAllowedURLs.IsEnabled = true;
                        btnAddDeleteAllowedURLs.IsEnabled = true;

                        ListURLs.Visibility = Visibility.Visible;
                        btnAddAllowedURLs.Visibility = Visibility.Visible;
                        btnAddDeleteAllowedURLs.Visibility = Visibility.Visible;


                        SelectedURLsListBG.Background = BackgroundColor;
                        SelectedURLsListBGSeperator.Background = BackgroundColor;

                    }
                    else
                    {
                        HowDoYouWant.Text = "All websites are allowed";//todo win texts // appSettings.ConfigFileStrings["WindowsSpecific"]["allWebsitesAllowed"].ToString();


                        SelectedURLsListBG.Background = BackgroundColor;
                        SelectedURLsListBGSeperator.Background = BackgroundColor;
                    }
                }

                selectedFocusMode.isAllowedListApproach = false;
                ListURLs.ItemsSource = selectedFocusMode.arrBlockedUrls;
            }

            btnAdvanced.IsEnabled = false;//Disable the advanced button 

        }

        private void btnadvanced_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnadvanced_Click");

            var selectedFocusMode = ListFocusModes.SelectedItem as FocusMode;
            if (selectedFocusMode != null)
            {
                appSettings.CapturePostHog("open-advanced-focus-mode-preferences");

                _ParentWindow.RepositionWindow();
                _ParentWindow.GridShade.Visibility = Visibility.Visible;
                FocusAdvanced aa = new FocusAdvanced(_ParentWindow, selectedFocusMode);
                aa.Topmost = true;
                bool? DialogResult = aa.ShowDialog();

                _ParentWindow.GridShade.Visibility = Visibility.Collapsed;
            }
        }

        private void btnAddAllowedApp_Click(object sender, RoutedEventArgs e)
        {

            var selectedFocusMode = ListFocusModes.SelectedItem as FocusMode;
            if (selectedFocusMode != null)
            {
                _ParentWindow.RepositionWindow();
                _ParentWindow.GridShade.Visibility = Visibility.Visible;
                AddApp aa = new AddApp(_ParentWindow, selectedFocusMode);
                aa.Topmost = true;
                bool? DialogResult = aa.ShowDialog();

                _ParentWindow.GridShade.Visibility = Visibility.Collapsed;

                if(DialogResult.Value == false) //means the Add apps manually button is clicked
                {
                    ShowAddAppManuallyWindow();
                }
            }

            return;
            
        }

        private void ShowAddAppManuallyWindow()
        {
            LogWriter.LogWrite("ShowAddAppManuallyWindow()");


           _ParentWindow.RepositionWindow();
            _ParentWindow.GridShade.Visibility = Visibility.Visible;
            AddAppsManually aa = new AddAppsManually(_ParentWindow);
            aa.Topmost = true;
            bool? DialogResult = aa.ShowDialog();

            _ParentWindow.GridShade.Visibility = Visibility.Collapsed;

            string sApps = "";

            switch (DialogResult)
            {
                case true:
                    // Url added
                    sApps = aa.sAppsToAdd;
                    break;
                case false:
                    // User did not postponed the breaks..normal flow follows
                    break;
                default:
                    // Indeterminate
                    break;
            }

            try
            {
                if (sApps.Length > 0)
                {
                    //add to focus mode urls list

                    //First check if it is already added
                    var selectedFocusMode = ListFocusModes.SelectedItem as FocusMode;

                    if (selectedFocusMode != null)
                    {
                        //Seperate the text with ,
                        string[] data = sApps.Split(',');
                        if (data != null && data.Count() > 0)
                        {
                            bool bAppAdded = false;
                            foreach (string ss in data)
                            {
                                string app = ss.Trim().ToLower();

                                if (app.Length > 0 && app.Contains(".exe"))
                                {
                                    //First check if this is not already present in the apps
                                    var listItems = selectedFocusMode.arrAllowedAppsWin.Where(i => i.AppName == app);
                                    if (listItems.Count() == 0)
                                    {
                                        selectedFocusMode.arrAllowedAppsWin.Add(new AppDetails(app, app));
                                        bAppAdded = true;
                                    }
                                }
                            }
                            if(bAppAdded)
                                appSettings.CapturePostHog("add-allowed-app-manually-in-focus-mode");
                        }


                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ShowAddAppManuallyWindow ex - " + ex.Message);
            }
        }
        private void btnAddDeleteAllowedApp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = ListApps.SelectedItem as AppDetails;
                if (selectedItem != null)
                {
                    var selectedFocusMode = ListFocusModes.SelectedItem as FocusMode;

                    if (selectedFocusMode != null)
                    {
                        selectedFocusMode.arrAllowedAppsWin.Remove(selectedItem);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnAddDeleteAllowedApp_Click ex - " + ex.Message);
            }
        }

        private void btnAddAllowedURLs_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnAddAllowedURLs_Click");

            _ParentWindow.RepositionWindow();
            _ParentWindow.GridShade.Visibility = Visibility.Visible;
            AddWebUrl aa = null;

            var selectedFocusMode1 = ListFocusModes.SelectedItem as FocusMode;
            if (selectedFocusMode1 != null)
            {
                if(selectedFocusMode1.isAllowedListApproach)
                {
                    aa = new AddWebUrl(_ParentWindow);
                }
                else
                {
                    aa = new AddWebUrl(_ParentWindow,"",false,true);
                }
            }
            else
            {
                return;
            }

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
                //add to focus mode urls list

                //First check if it is already added
                var selectedFocusMode = ListFocusModes.SelectedItem as FocusMode;
                
                if (selectedFocusMode != null)
                {
                    //check the appropriate radio boxes for URLs
                    if (selectedFocusMode.isAllowedListApproach)
                    {
                        //First check if it is already added
                        var listItems = selectedFocusMode.arrAllowedUrls.Where(i => i == url);
                        if (listItems.Count() == 0)
                        {
                            selectedFocusMode.arrAllowedUrls.Add(url);
                            appSettings.CapturePostHog("add-allowed-url-in-focus-mode");
                        }
                        else
                        {
                            string sErr = appSettings.ConfigFileStrings["focusPreferencesInfo"]["urlAlreadyExistsText"].ToString();
                            sErr = sErr.Replace("<APPNAME>", url);
                           //// MessageBox.Show(sErr, "");
                            FBMessageBox ff = new FBMessageBox(sErr, "Focus Bear");
                            ff.Owner = _ParentWindow;
                            ff.ShowDialog();
                        }
                    }
                    else
                    {
                        //First check if it is already added
                        var listItems = selectedFocusMode.arrBlockedUrls.Where(i => i == url);
                        if (listItems.Count() == 0)
                        {
                            selectedFocusMode.arrBlockedUrls.Add(url);
                            appSettings.CapturePostHog("add-blocked-url-in-focus-mode");
                        }
                        else
                        {
                            string sErr = appSettings.ConfigFileStrings["focusPreferencesInfo"]["urlAlreadyExistsText"].ToString();
                            sErr = sErr.Replace("<APPNAME>", url);
                            ///MessageBox.Show(sErr, "");
                            FBMessageBox ff = new FBMessageBox(sErr, "Focus Bear");
                            ff.Owner = _ParentWindow;
                            ff.ShowDialog();
                        }
                    }
                }
            }
           
        }

        private void btnAddDeleteAllowedURLs_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnAddDeleteAllowedURLs_Click");
            try
            {
                var selectedItem = ListURLs.SelectedItem as string;
                if (selectedItem != null)
                {
                    var selectedFocusMode = ListFocusModes.SelectedItem as FocusMode;

                    if (selectedFocusMode != null)
                    {
                        if (selectedFocusMode.isAllowedListApproach)
                        {
                            selectedFocusMode.arrAllowedUrls.Remove(selectedItem);
                        }
                        else
                        {
                            selectedFocusMode.arrBlockedUrls.Remove(selectedItem);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnAddDeleteAllowedURLs_Click EX = " + ex.Message);
            }
        }

        private void btnAddFocusMode_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("btnAddFocusMode_Click");

            _ParentWindow.RepositionWindow();
            _ParentWindow.GridShade.Visibility = Visibility.Visible;
            AddFocusType aa = new AddFocusType(_ParentWindow);
            aa.Topmost = true;
            bool? DialogResult = aa.ShowDialog();

            _ParentWindow.GridShade.Visibility = Visibility.Collapsed;

            string sFocusName = "";

            switch (DialogResult)
            {
                case true:
                    // Url added
                    sFocusName = aa.sFocusNameToAdd;
                    break;
                case false:
                    // User did not postponed the breaks..normal flow follows
                    break;
                default:
                    // Indeterminate
                    break;
            }
            if (sFocusName.Length > 0)
            {
                if (sFocusName.Length > 250)
                {
                    sFocusName = sFocusName.Substring(0, 250);
                }

                appSettings.CapturePostHog("add-new-focus-mode");
                //Call the API to upload the newly cread focus mode
                //add to focus modes  array and Upload to api
                //#A3FFA1 is the default color
                AddFocusModeToArrayAndUploadtoAPI(new FocusMode(System.Guid.NewGuid().ToString(), sFocusName, new ObservableCollection<string>(), new ObservableCollection<string>(), new ObservableCollection<AppDetails>(), "#A3FFA1", false, true, true,   false, false, false, false, false));
            }

        }

        private void ListFocusModes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LogWriter.LogWrite("Focus mode tab in preferences - ListFocusModes_SelectionChanged");

            try
            {
                var selectedItem = ListFocusModes.SelectedItem as FocusMode;
               
                if (selectedItem != null)
                {
                    
                    TxtFocusName.Text = selectedItem.name;
                    if(selectedItem.isDefault || selectedItem.isMeetings || selectedItem.isLockedFocus || selectedItem.isRelaxFocus || selectedItem.isDeepWorkFocus)
                    {
                        TxtFocusName.IsEnabled = false;
                    }
                    else
                    {
                        TxtFocusName.IsEnabled = true;
                    }
                    if(selectedItem.isRelaxFocus)
                    {
                        AllowSelectedApps.Visibility = Visibility.Collapsed;

                        BlockEverything.Visibility = Visibility.Collapsed;
                        AllowEverything.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        AllowSelectedApps.Visibility = Visibility.Visible;
                        
                        BlockEverything.Visibility = Visibility.Visible;
                        AllowEverything.Visibility = Visibility.Visible;
                    }
                    //check the appropriate radio boxes for apps
                    if (selectedItem.isAllowedAllApps)
                    {
                        AllowAllApps.IsChecked = true;
                    }
                    else
                    {
                        AllowSelectedApps.IsChecked = true;
                    }
                    //check the appropriate radio boxes for URLs
                    if (selectedItem.isAllowedListApproach)
                    {
                        BlockEverything.IsChecked = true;
                      
                        ListURLs.ItemsSource = selectedItem.arrAllowedUrls;
                    }
                    else
                    {
                        AllowEverything.IsChecked = true;
                       
                        ListURLs.ItemsSource = selectedItem.arrBlockedUrls;
                    }

                    if (selectedItem.isRelaxFocus)
                    {
                        ListURLs.IsEnabled = false;
                        btnAddAllowedURLs.IsEnabled = false;
                        btnAddDeleteAllowedURLs.IsEnabled = false;

                        ListURLs.Visibility = Visibility.Collapsed;
                        btnAddAllowedURLs.Visibility = Visibility.Collapsed;
                        btnAddDeleteAllowedURLs.Visibility = Visibility.Collapsed;
                        
                    }
                    else
                    {
                        ListURLs.IsEnabled = true;
                        btnAddAllowedURLs.IsEnabled = true;
                        btnAddDeleteAllowedURLs.IsEnabled = true;

                        ListURLs.Visibility = Visibility.Visible;
                        btnAddAllowedURLs.Visibility = Visibility.Visible;
                        btnAddDeleteAllowedURLs.Visibility = Visibility.Visible;
                    }

                    //Fill the lists for apps and URLs
                    ListApps.ItemsSource = selectedItem.arrAllowedAppsWin;
 
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ListFocusModes_SelectionChanged in PReferences" + ex.Message);
            }

        }

        private void TxtFocusName_LostFocus(object sender, RoutedEventArgs e)
        {
            // e.Handled = true;
            string sText = TxtFocusName.Text;
            ValidateAndUpdateFocusName(sText.Trim());

        }

        private void TxtFocusName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string sText = TxtFocusName.Text;
                ValidateAndUpdateFocusName(sText.Trim());
            }
        }
        private void ValidateAndUpdateFocusName(string sNewName)
        {
           

            var selectedItem = ListFocusModes.SelectedItem as FocusMode;
            if (selectedItem != null)
            {
                if (sNewName.Length == 0) //no blanks allowed
                {
                    TxtFocusName.Text = selectedItem.name;
                    return;
                }
                if (sNewName.Length > 250) //no more than 250
                {
                    sNewName = sNewName.Substring(0,250);
                     
                }

                var listItems = appSettings.arrFocusModes.Where(i => i.name.ToLower() == sNewName.ToLower());
                if (listItems.Count() == 0)
                {
                    selectedItem.name = sNewName;

                    appSettings.CapturePostHog("change-focus-mode-name");
                }
                else
                {
                    FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["focusPreferencesInfo"]["errorSameFocusType"].ToString(), "Focus Bear");
                    ff.Owner = _ParentWindow;
                    ff.ShowDialog();
                    TxtFocusName.Text = selectedItem.name;
                }
            }

        }

        private void ListURLs_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var selectedItem = ListURLs.SelectedItem as string;

                if (selectedItem != null)
                {
                    _ParentWindow.RepositionWindow();
                    _ParentWindow.GridShade.Visibility = Visibility.Visible;

                    AddWebUrl aa = null;

                    var selectedFocusMode1 = ListFocusModes.SelectedItem as FocusMode;
                    if (selectedFocusMode1 != null)
                    {
                        if (selectedFocusMode1.isAllowedListApproach)
                        {
                            aa = new AddWebUrl(_ParentWindow, selectedItem, true);
                        }
                        else
                        {
                            aa = new AddWebUrl(_ParentWindow, selectedItem, true, true);
                        }
                    }
                    else
                    {
                        return;
                    }

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
                        //add to focus mode urls list

                        //First check if it is already added
                        var selectedFocusMode = ListFocusModes.SelectedItem as FocusMode;

                        if (selectedFocusMode != null)
                        {
                            //check the appropriate radio boxes for URLs
                            if (selectedFocusMode.isAllowedListApproach)
                            {
                                //First check if it is already added
                                var listItems = selectedFocusMode.arrAllowedUrls.Where(i => i == url);
                                if (listItems.Count() == 0)
                                {
                                    for (int m = 0; m < selectedFocusMode.arrAllowedUrls.Count(); m++)
                                    {
                                        if (selectedFocusMode.arrAllowedUrls[m] == selectedItem)//Item to edit
                                        {
                                            selectedFocusMode.arrAllowedUrls[m] = url;
                                            break;
                                        }
                                    }
                                }
                                else
                                {
     
                                    string sErr = appSettings.ConfigFileStrings["focusPreferencesInfo"]["urlAlreadyExistsText"].ToString();
                                    sErr = sErr.Replace("<APPNAME>", url);
                                    ///MessageBox.Show(sErr, "");
                                    FBMessageBox ff = new FBMessageBox(sErr, "Focus Bear");
                                    ff.Owner = _ParentWindow;
                                    ff.ShowDialog();
                                }
                            }
                            else
                            {
                                //First check if it is already added
                                var listItems = selectedFocusMode.arrBlockedUrls.Where(i => i == url);
                                if (listItems.Count() == 0)
                                {
                                    for (int m = 0; m < selectedFocusMode.arrBlockedUrls.Count(); m++)
                                    {
                                        if (selectedFocusMode.arrBlockedUrls[m] == selectedItem)//Item to edit
                                        {
                                            selectedFocusMode.arrBlockedUrls[m] = url;
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
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ListURLs_MouseDoubleClick in Focus URLs List" + ex.Message);
            }
        }

        public void SelectFocusMode(string sFocusMode)
        {
            var listItems = appSettings.arrFocusModes.Where(i => i.name.ToLower() == sFocusMode.ToLower());
            if (listItems.Count() != 0)
            {
                ListFocusModes.SelectedItem = listItems.First();
            }
        }
    }
}
