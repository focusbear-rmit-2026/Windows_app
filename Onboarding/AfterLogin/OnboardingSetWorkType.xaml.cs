using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace Focus_Bear.Onboarding.AfterLogin
{
    /// <summary>
    /// Interaction logic for OnboardingSetWorkType.xaml
    /// </summary>
    public partial class OnboardingSetWorkType : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        private OnboardingSetupAndTipsWindow _parentWindow;
        public OnboardingSetWorkType()
        {

        }
        public OnboardingSetWorkType(OnboardingSetupAndTipsWindow parentWindow)
        {
            InitializeComponent();
            _parentWindow = parentWindow;

            LogWriter.LogWrite("OnboardingSetWorkType screen");

            TypeOfWork.Text = appSettings.ConfigFileStrings["preloadFocusModesViewInfo"]["title"].ToString();

            /*
              "preloadFocusModesViewInfo":{
                "title":"What type of work do you mainly do?",
                "titleSelectWorkType":"Select work type",
                "titleProgramming":"Programming",
                "titleGraphicDesign":"Graphic Design",
                "titleDataAnalysis":"Data Analysis",
                "titleSomethingElse":"Something else",
                "buttonContinueText":"Continue",
                "workText":"Work"
            }, 
            */

            ComboBoxWorkType.Items.Add(appSettings.ConfigFileStrings["preloadFocusModesViewInfo"]["titleSelectWorkType"].ToString());
            ComboBoxWorkType.Items.Add(appSettings.ConfigFileStrings["preloadFocusModesViewInfo"]["titleProgramming"].ToString());
            ComboBoxWorkType.Items.Add(appSettings.ConfigFileStrings["preloadFocusModesViewInfo"]["titleGraphicDesign"].ToString());
            ComboBoxWorkType.Items.Add(appSettings.ConfigFileStrings["preloadFocusModesViewInfo"]["titleDataAnalysis"].ToString());
            ComboBoxWorkType.Items.Add(appSettings.ConfigFileStrings["preloadFocusModesViewInfo"]["titleSomethingElse"].ToString());

            btnContinueTXT.Text = appSettings.ConfigFileStrings["preloadFocusModesViewInfo"]["buttonContinueText"].ToString();

            btnContinue.IsEnabled = false;
            SetupFocusMode.Visibility = Visibility.Collapsed;
        }

        private void ComboBoxWorkType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnContinue.IsEnabled = false;

            if (ComboBoxWorkType.SelectedIndex == 4) //Something Else Selected
            {
                TxtSomethingElse.Visibility = Visibility.Visible;
                TxtSomethingElse.Focus();

                SetupFocusMode.Visibility = Visibility.Collapsed;
            }
            else
            {
                TxtSomethingElse.Text = "";
                TxtSomethingElse.Visibility = Visibility.Collapsed;
                if (ComboBoxWorkType.SelectedIndex != 0)
                {
                    btnContinue.IsEnabled = true;
                    SetupFocusMode.Visibility = Visibility.Visible;
                    string ss = appSettings.ConfigFileStrings["preloadFocusModesViewInfo"]["gotItText"].ToString();
                    ss = ss.Replace("<WORK_TYPE>", ComboBoxWorkType.SelectedItem.ToString());
                    SetupFocusMode.Text = ss;
                }
                else
                {
                    btnContinue.IsEnabled = false;
                    SetupFocusMode.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void TxtSomethingElse_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtSomethingElse.Text.Length > 0)
            {
                btnContinue.IsEnabled = true;
                SetupFocusMode.Visibility = Visibility.Visible;
                string ss = appSettings.ConfigFileStrings["preloadFocusModesViewInfo"]["gotItText"].ToString();
                ss = ss.Replace("<WORK_TYPE>", TxtSomethingElse.Text);
                SetupFocusMode.Text = ss;
            }
            else
            {
                btnContinue.IsEnabled = false;
                SetupFocusMode.Visibility = Visibility.Collapsed;
            }
        }

        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string sFocusName = "";
                if (ComboBoxWorkType.SelectedIndex == 4) //Something Else Selected
                {
                    sFocusName = TxtSomethingElse.Text;
                }
                else
                {
                    sFocusName = ComboBoxWorkType.SelectedItem.ToString();
                }

                /*
                 * case 1://Programming
            dic = ["name":"\(titleProgramming) \(workText)", "color": getColorString(color: NSColor(red: 208/255, green: 150/255, blue: 60/255, alpha: 1)), "allowed_apps": ["/Applications/Visual Studio Code.app", "/Applications/Xcode.app", "/Applications/Utilities/Terminal.app", "/Applications/Google Chrome.app", "/Applications/iTerm2.app", "/Applications/Docker.app", "/Applications/Firefox.app", "/Applications/Insomnia.app", "/Applications/VirtualBox.app", "/Applications/DBeaver.app", "/Applications/Safari.app"], "allowed_urls": ["https://stackoverflow.com","localhost"], "isOn": false, "isAllowedAllApps":false, "jobType":jobTypeProgramming, "isAllowedListApproach":true, "blockUrlsArray": []]//#453, "isBlockedAllUrls":false, "isAllowedAllUrls":false

        case 2://Graphic Design
            dic = ["name":"\(titleGraphicDesign) \(workText)", "color": getColorString(color: NSColor(red: 208/255, green: 150/255, blue: 60/255, alpha: 1)), "allowed_apps": ["/Applications/Figma.app", "/Applications/Adobe Illustrator.app", "/Applications/Adobe Photoshop.app", "/Applications/Google Chrome.app", "/Applications/Safari.app"], "allowed_urls": ["https://www.figma.com"], "isOn": false, "isAllowedAllApps":false, "jobType":jobTypeGraphicDesign, "isAllowedListApproach":true, "blockUrlsArray": []]//#453, "isBlockedAllUrls":false, "isAllowedAllUrls":false
            
        case 3://Data Analysis
            dic = ["name":"\(titleDataAnalysis) \(workText)", "color": getColorString(color: NSColor(red: 208/255, green: 150/255, blue: 60/255, alpha: 1)), "allowed_apps": ["/Applications/Microsoft Excel.app", "/Applications/Google Chrome.app", "/Applications/Safari.app"], "allowed_urls": ["https://www.office.com"], "isOn": false, "isAllowedAllApps":false, "jobType":jobTypeDataAnalysis, "isAllowedListApproach":true, "blockUrlsArray": []]//#453 , "isBlockedAllUrls":false , "isAllowedAllUrls":false
        
        case 4:
            dic = ["name":"\(textFieldSomethingElse.stringValue) \(workText)", "color": getColorString(color: NSColor(red: 208/255, green: 150/255, blue: 60/255, alpha: 1)), "allowed_apps": ["/Applications/Microsoft Excel.app", "/Applications/Microsoft Word.app", "/Applications/Google Chrome.app", "/System/Applications/Calendar.app", "/Applications/Safari.app"], "allowed_urls": ["https://www.google.com"], "isOn": false, "isAllowedAllApps":false, "jobType":jobTypeSomethingElse, "isAllowedListApproach":true, "blockUrlsArray": []]//#453// lsit approach check krni padeegi  sabiki, "isAllowedAllUrls":false , "isBlockedAllUrls":false

                 */
                ObservableCollection<AppDetails> arrAllowedAppsWin = new ObservableCollection<AppDetails>();
                ObservableCollection<string> arrAllowedUrls = new ObservableCollection<string>();
               

                if (ComboBoxWorkType.SelectedIndex == 1)//"titleProgramming":"Programming",
                {
                    arrAllowedAppsWin.Add(new AppDetails("Visual Studio Code", "code.exe"));
                    arrAllowedAppsWin.Add(new AppDetails("Microsoft Visual Studio", "devenv.exe"));
                    arrAllowedAppsWin.Add(new AppDetails("Slack", "slack.exe"));
                    arrAllowedAppsWin.Add(new AppDetails("Microsoft Edge", "msedge.exe"));
                    arrAllowedAppsWin.Add(new AppDetails("Mozilla Firefox", "firefox.exe"));
                    arrAllowedAppsWin.Add(new AppDetails("Google Chrome", "chrome.exe"));

                    arrAllowedUrls.Add("https://stackoverflow.com");
                    arrAllowedUrls.Add("localhost");
                }
                else if (ComboBoxWorkType.SelectedIndex == 2)// "titleGraphicDesign":"Graphic Design",
                {
                     
                    arrAllowedAppsWin.Add(new AppDetails("Figma", "figma.exe"));
                    arrAllowedAppsWin.Add(new AppDetails("Adobe Illustrator", "illustrator.exe"));
                    arrAllowedAppsWin.Add(new AppDetails("Adobe Photoshop", "photoshop.exe"));
                    arrAllowedAppsWin.Add(new AppDetails("Microsoft Edge", "msedge.exe"));
                    arrAllowedAppsWin.Add(new AppDetails("Mozilla Firefox", "firefox.exe"));
                    arrAllowedAppsWin.Add(new AppDetails("Google Chrome", "chrome.exe"));

                    arrAllowedUrls.Add("https://www.figma.com");
                    
                }
                else if (ComboBoxWorkType.SelectedIndex == 3)// "titleDataAnalysis":"Data Analysis",
                {
                    arrAllowedAppsWin.Add(new AppDetails("Microsoft Excel", "excel.exe"));
                    arrAllowedAppsWin.Add(new AppDetails("Microsoft Edge", "msedge.exe"));
                    arrAllowedAppsWin.Add(new AppDetails("Mozilla Firefox", "firefox.exe"));
                    arrAllowedAppsWin.Add(new AppDetails("Google Chrome", "chrome.exe"));

                    arrAllowedUrls.Add("https://www.office.com");
                }
                else if (ComboBoxWorkType.SelectedIndex == 4)//  "titleSomethingElse":"Something else",
                {
                    arrAllowedAppsWin.Add(new AppDetails("Microsoft Excel", "excel.exe"));
                    arrAllowedAppsWin.Add(new AppDetails("Microsoft Word", "winword.exe"));
                    arrAllowedAppsWin.Add(new AppDetails("Microsoft Edge", "msedge.exe"));
                    arrAllowedAppsWin.Add(new AppDetails("Mozilla Firefox", "firefox.exe"));
                    arrAllowedAppsWin.Add(new AppDetails("Google Chrome", "chrome.exe"));

                    arrAllowedUrls.Add("https://www.google.com");
                }

                appSettings.FocusModeNameToCreateWhileOnBoarding = new FocusMode(System.Guid.NewGuid().ToString(), sFocusName, arrAllowedUrls, new ObservableCollection<string>(), arrAllowedAppsWin, "#A3FFA1", false, false, true, false, false, false);

                _parentWindow.ShowFocusModeAppsSetup();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Onboarding set work type btnContinue_Click EX -" + ex.Message);
            }
        }
    }
}
