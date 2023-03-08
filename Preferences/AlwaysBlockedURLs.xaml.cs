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
    public partial class AlwaysBlockedURLs : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;

        PreferencesWindow _ParentWindow = null;

        public AlwaysBlockedURLs()
        {
            
        }
        public AlwaysBlockedURLs(PreferencesWindow pp)
        {
            InitializeComponent();
            _ParentWindow = pp;
            /*
            "blockedUrlsViewInfo":{
                "title":"Always Blocked URLs",
                "invalidDomainText":"Adding * will block all urls. Please use another combination.",
                "subTitleText":"\"Block these URLs even if you're not in a focus block. You might want to put social media or news sites that you would rather not use at any time. You can always set up an evening routine activity where you explicitly allow access to Facebook so that you can still stay connected - just not during your morning routine/work day.\""
            },*/
            Heading.Text = appSettings.ConfigFileStrings["blockedUrlsViewInfo"]["title"].ToString();
            Description.Text = appSettings.ConfigFileStrings["blockedUrlsViewInfo"]["subTitleText"].ToString();

            btnAddURL.ToolTip = appSettings.ConfigFileStrings["focusPreferencesAdvancedInfo"]["toolTipAdd"].ToString();
            btnDeleteURL.ToolTip = appSettings.ConfigFileStrings["focusPreferencesAdvancedInfo"]["toolTipRemove"].ToString();

            ListBLockedURLs.ItemsSource = appSettings.arrAlwaysBlockedURLs;

        }

        private void btnAddURL_Click(object sender, RoutedEventArgs e)
        {
            try
            {


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
                    }
                    else
                    {
                        //"urlAlreadyExistsText":"<APPNAME> already exists in the list.",
                        string sErr = appSettings.ConfigFileStrings["focusPreferencesInfo"]["urlAlreadyExistsText"].ToString();
                        sErr = sErr.Replace("<APPNAME>", url);
                        ///MessageBox.Show(sErr, "");
                        FBMessageBox ff = new FBMessageBox(sErr, "Focus Bear");
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
                                    break;
                                }
                            }
                        }
                        else
                        {
                            //"urlAlreadyExistsText":"<APPNAME> already exists in the list.",
                            string sErr = appSettings.ConfigFileStrings["focusPreferencesInfo"]["urlAlreadyExistsText"].ToString();
                            sErr = sErr.Replace("<APPNAME>", url);
                            // MessageBox.Show(sErr, "");
                            FBMessageBox ff = new FBMessageBox(sErr, "Focus Bear");
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
    }
    
}
