using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
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
    /// Interaction logic for BlocksTab.xaml
    /// </summary>
    public partial class BlocksTab : UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;
        FocusBearMainWindow _ParentWindow = null;
        FocusPage PageFocusModes = null;
        bool bBlockingScheduleDetailsPageVisible = false;
        public BlocksTab()
        {
        }
        public BlocksTab(FocusBearMainWindow ParentWindow)
        {
            _ParentWindow = ParentWindow;

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
                Heading.Text = appSettings.ConfigFileStrings["leftMenuVcInfo"]["menuBlocksPreferences"].ToString();
                btnBlockListsText.Text = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["titleFocusMode"].ToString();
                btnBlockingScheduleText.Text = appSettings.ConfigFileStrings["settingsNew"]["blockingScheduleSettingTitle"].ToString();
                btnDistractingSitesText.Text = appSettings.ConfigFileStrings["blockedUrlsViewInfo"]["title"].ToString();
                
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("BlocksTab text setting ex - " + ex.Message);
            }

        }

        private void btnBlockingSchedule_Click(object sender, RoutedEventArgs e)
        {
            // Hide main menu
            MainMenuPanel.Visibility = Visibility.Collapsed;
            SubViewContainer.Visibility = Visibility.Visible;

            SubViewHeading.Text = appSettings.ConfigFileStrings["settingsNew"]["blockingScheduleSettingTitle"].ToString();
            // Load the BlockingSchedule class here
            ContentArea.Content = new BlockingSchedule(_ParentWindow);           
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

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
           

            if(bBlockingScheduleDetailsPageVisible)
            {
                //Show blocks list page
                ButtonAutomationPeer peer = new ButtonAutomationPeer(btnBlockingSchedule);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();

                bBlockingScheduleDetailsPageVisible = false;
                return;
            }
            // Hide subview, show main menu
            SubViewContainer.Visibility = Visibility.Collapsed;
            MainMenuPanel.Visibility = Visibility.Visible;

            // Optionally clear the content
            ContentArea.Content = null;
        }

        private void btnBlockLists_Click(object sender, RoutedEventArgs e)
        {
            // Hide main menu
            MainMenuPanel.Visibility = Visibility.Collapsed;
            SubViewContainer.Visibility = Visibility.Visible;

            SubViewHeading.Text = appSettings.ConfigFileStrings["settingsNew"]["titleBlockLists"].ToString();
            // Load yourFocus modes page here
            PageFocusModes = new FocusPage(_ParentWindow);
            ContentArea.Content = PageFocusModes;

        }

        public void ShowFocusMode(string sFocusMode = "")
        {
            //Show blocks list page
            ButtonAutomationPeer peer = new ButtonAutomationPeer(btnBlockLists);
            IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            invokeProv.Invoke();

            if (sFocusMode.Length > 0)
                PageFocusModes.SelectFocusMode(sFocusMode);
        }

        public void ShowBlockingScheduleDetails(CustomBlockingSchedule Block)
        {
            bBlockingScheduleDetailsPageVisible = true;
            ContentArea.Content = new BlockingScheduleDetail(_ParentWindow, Block);
        }

    }
}
