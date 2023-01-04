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
using System.Windows.Shapes;

namespace Focus_Bear.Preferences
{
    /// <summary>
    /// Interaction logic for FocusAdvanced.xaml
    /// </summary>
    public partial class FocusAdvanced : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        FocusMode selectedFocusMode = null;
        public FocusAdvanced()
        {
        }
        public FocusAdvanced(PreferencesWindow ParentWindow, FocusMode sFocusMode)
        {
            InitializeComponent();

            selectedFocusMode = sFocusMode;

            this.Owner = ParentWindow;

            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            /*
             *  "focusPreferencesAdvancedInfo":{
             *   "titleBlockedUrls":"Blocked URLs",
             *   "toolTipAdd":"Add",
                "toolTipRemove":"Remove",
                "instructionText":"The URLs here will be blocked during this focus mode. Useful if you want to allow some websites but block others. For example, you could have *.google.com as an allowed URL but block tv.google.com",
             * */

            Heading.Text = appSettings.ConfigFileStrings["focusPreferencesAdvancedInfo"]["titleBlockedUrls"].ToString();
            Description.Text = appSettings.ConfigFileStrings["focusPreferencesAdvancedInfo"]["instructionText"].ToString();

            btnAddURL.ToolTip = appSettings.ConfigFileStrings["focusPreferencesAdvancedInfo"]["toolTipAdd"].ToString();
            btnDeleteURL.ToolTip = appSettings.ConfigFileStrings["focusPreferencesAdvancedInfo"]["toolTipRemove"].ToString();
            
            btnOKTXT.Text = appSettings.ConfigFileStrings["common"]["buttonOkText"].ToString();

            ListBLockedURLs.ItemsSource = selectedFocusMode.arrBlockedUrls;
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnAddURL_Click(object sender, RoutedEventArgs e)
        {
            try
            {


                GridShadeAdvanced.Visibility = Visibility.Visible;
                AddWebUrl aa = new AddWebUrl(this,"",false,true);
                aa.Topmost = true;
                bool? DialogResult = aa.ShowDialog();

                GridShadeAdvanced.Visibility = Visibility.Collapsed;

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
                    // var selectedFocusMode = ListFocusModes.SelectedItem as FocusMode;

                    if (selectedFocusMode != null)
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
                            //"urlAlreadyExistsText":"<APPNAME> already exists in the list.",
                            string sErr = appSettings.ConfigFileStrings["focusPreferencesInfo"]["urlAlreadyExistsText"].ToString();
                            sErr = sErr.Replace("<APPNAME>", url);
                            MessageBox.Show(sErr, "");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("btnAddURL_Click in Advanced Focus URLs List" + ex.Message);
            }
        }

        private void btnDeleteURL_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = ListBLockedURLs.SelectedItem as string;
                if (selectedItem != null)
                {
                    if (selectedFocusMode != null)
                    {
                        selectedFocusMode.arrBlockedUrls.Remove(selectedItem);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnDeleteURL_Click + " + ex.Message);
            }

        }

        private void ListBLockedURLs_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var selectedItem = ListBLockedURLs.SelectedItem as string;

                if (selectedItem != null)
                {
                    GridShadeAdvanced.Visibility = Visibility.Visible;
                    AddWebUrl aa = new AddWebUrl(this, selectedItem, true, true);
                    aa.Topmost = true;
                    bool? DialogResult = aa.ShowDialog();

                    GridShadeAdvanced.Visibility = Visibility.Collapsed;

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
                           if (selectedFocusMode != null)
                        {
                            //check the appropriate radio boxes for URLs
                            
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
                                    //"urlAlreadyExistsText":"<APPNAME> already exists in the list.",
                                    string sErr = appSettings.ConfigFileStrings["focusPreferencesInfo"]["urlAlreadyExistsText"].ToString();
                                    sErr = sErr.Replace("<APPNAME>", url);
                                    MessageBox.Show(sErr, "");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ListBLockedURLs_MouseDoubleClick in Advanced Focus URLs List" + ex.Message);
            }

        }
    }
}
