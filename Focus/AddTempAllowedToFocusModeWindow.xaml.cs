using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Focus_Bear.Focus
{
    /// <summary>
    /// Interaction logic for AddTempAllowedToFocusModeWindow.xaml
    /// </summary>
    public partial class AddTempAllowedToFocusModeWindow : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        App app = (App)Application.Current;

        public AddTempAllowedToFocusModeWindow()
        {
            LogWriter.LogWrite("AddTempAllowedToFocusModeWindow() shown ");
            InitializeComponent();

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }


            try
            {
                
                Heading.Text = appSettings.ConfigFileStrings["addFocusTempUrlsOrAppsVcInfo"]["title"].ToString();
                btnAddTXT.Text = appSettings.ConfigFileStrings["addFocusTempUrlsOrAppsVcInfo"]["buttonAllowText"].ToString();
                btnCancelTXT.Text = appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString();
                AppsTitle.Text = appSettings.ConfigFileStrings["toolBoxVcInfo"]["appsText"].ToString();
                URLSTitle.Text = appSettings.ConfigFileStrings["toolBoxVcInfo"]["urlsText"].ToString();
                KeywordsTitle.Text = appSettings.ConfigFileStrings["addFocusTempUrlsOrAppsVcInfo"]["titleKeywords"].ToString();
                Title = appSettings.ConfigFileStrings["addFocusTempUrlsOrAppsVcInfo"]["titleWindowText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AddTempAllowedToFocusModeWindow() EX = " + ex.Message);
            }

            ListApps.ItemsSource = appSettings.TemporaryAllowedAppsGentleBlock;
            ListURLs.ItemsSource = appSettings.TemporaryAllowedURLsGentleBlock;

            //Will will need to call the keywords API and then get the keyword to show in the list.
            ListKeywords.ItemsSource = appSettings.TemporaryAllowedKeywords;

            if (appSettings.TemporaryAllowedAppsGentleBlock.Count == 0)
            {
                AppsTitle.Visibility = Visibility.Collapsed;
                ListApps.Visibility = Visibility.Collapsed;
            }
            if(appSettings.TemporaryAllowedURLsGentleBlock.Count == 0)
            {
                URLSTitle.Visibility = Visibility.Collapsed;
                ListURLs.Visibility = Visibility.Collapsed;
            }
         
            // Manually alter window height and width
            this.SizeToContent = SizeToContent.Manual;

            // Automatically resize width relative to content
            this.SizeToContent = SizeToContent.Width;

            // Automatically resize height relative to content
            this.SizeToContent = SizeToContent.Height;

            // Automatically resize height and width relative to content
            this.SizeToContent = SizeToContent.WidthAndHeight;
            TitleBarThemeChange();
            App.ThemeChanged += TitleBarThemeChange;
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("AddTempAllowedToFocusModeWindow() btnAdd_Click ");

            var listItems = appSettings.arrFocusModes.Where(i => i.id.ToLower() == appSettings.sRunningFocusModeID.ToLower());
            if (listItems.Count() != 0)
            {
                FocusMode ff = listItems.First();

                try
                {
                    FocusBearAPIClient client = new FocusBearAPIClient();

                    foreach (DistractingURL dd in appSettings.TemporaryAllowedAppsGentleBlock)
                    {
                        if (dd.IsItemChecked)
                            ff.arrAllowedAppsWin.Add(new AppDetails(dd.sURL, dd.sURL));
                    }
                    foreach (DistractingURL dd in appSettings.TemporaryAllowedURLsGentleBlock)
                    {
                        if (dd.IsItemChecked)
                            ff.arrAllowedUrls.Add(dd.sURL);
                    }
                    foreach (DistractingURL dd in appSettings.TemporaryAllowedKeywords)
                    {
                        if (dd.IsItemChecked && dd.sURL.Length > 0)
                            ff.arrAllowedUrls.Add(dd.sURL);
                    }
                    //we will call the API to update all the focus modes.
                    LogWriter.LogWrite("attempt to UpdateAllFocusModes() API");
                    var response = client.UpdateAllFocusModes();

                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("btnAddDeleteAllowedURLs_Click EX = " + ex.Message);
                }
            }
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("AddTempAllowedToFocusModeWindow() btnCancel_Click ");
            Close();
        }

        private void btnAddKeyword_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("AddTempAllowedToFocusModeWindow() btnAddKeyword_Click ");
            appSettings.TemporaryAllowedKeywords.Add(new DistractingURL("",true));
            ListKeywords.SelectedItem = (appSettings.TemporaryAllowedKeywords.Count() - 1);
        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var source = (HwndSource)PresentationSource.FromVisual(this);
            app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
        }
        private void TitleBarThemeChange()
        {
            var source = (HwndSource)PresentationSource.FromVisual(this);
            if (source != null)
            {
                app.UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }
        }
        
    }
}
