using Focus_Bear.API;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

            try
            {
               

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
                /* ComboBoxWorkType.Items.Add(appSettings.ConfigFileStrings["preloadFocusModesViewInfo"]["titleProgramming"].ToString());
                 ComboBoxWorkType.Items.Add(appSettings.ConfigFileStrings["preloadFocusModesViewInfo"]["titleGraphicDesign"].ToString());
                 ComboBoxWorkType.Items.Add(appSettings.ConfigFileStrings["preloadFocusModesViewInfo"]["titleDataAnalysis"].ToString());
                 ComboBoxWorkType.Items.Add(appSettings.ConfigFileStrings["preloadFocusModesViewInfo"]["titleSomethingElse"].ToString());
                 */

                if (appSettings.sFocusModeTemplates != null /*|| (appSettings.sFocusModeTemplates.Length != 0)*/)
                {
                    //Parse the JSON string
                    try
                    {

                        JArray array = (JArray)JsonConvert.DeserializeObject(appSettings.sFocusModeTemplates);

                        for (int i = 0; i < array.Count(); i++)
                        {
                            JObject PackSettings = JObject.Parse(array[i].ToString());
                            string sName = PackSettings.ContainsKey("name") ? PackSettings["name"].ToString() : "";
                            ComboBoxWorkType.Items.Add(sName);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("sFocusModeTemplates reading filling() EX - " + ex.Message);
                    }
                }
                //we need to add something else.
                ComboBoxWorkType.Items.Add(appSettings.ConfigFileStrings["preloadFocusModesViewInfo"]["titleSomethingElse"].ToString());

              
                
                btnContinueTXT.Text = appSettings.ConfigFileStrings["preloadFocusModesViewInfo"]["buttonContinueText"].ToString();
                Customize.Text = appSettings.ConfigFileStrings["preloadFocusModesViewInfo"]["description"].ToString();

                MaxChars.Content = appSettings.ConfigFileStrings["preloadFocusModesViewInfo"]["maxChars"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Onboarding set work type () EX -" + ex.Message);
            }
          
            SetupFocusMode.Visibility = Visibility.Collapsed;
        }

        private void ComboBoxWorkType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnContinue.IsEnabled = false;

            if (ComboBoxWorkType.SelectedIndex == (ComboBoxWorkType.Items.Count - 1)) //Something Else Selected
            {
                TxtSomethingElse.Visibility = Visibility.Visible;
                TxtSomethingElse.Focus();
                MaxChars.Visibility = Visibility.Visible;
                SetupFocusMode.Visibility = Visibility.Collapsed;
            }
            else
            {
                TxtSomethingElse.Text = "";
                TxtSomethingElse.Visibility = Visibility.Collapsed;
                MaxChars.Visibility = Visibility.Collapsed;
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
                ObservableCollection<AppDetails> arrAllowedAppsWin = new ObservableCollection<AppDetails>();
                ObservableCollection<string> arrAllowedUrls = new ObservableCollection<string>();

                string sFocusName = "";

                if (ComboBoxWorkType.SelectedIndex == (ComboBoxWorkType.Items.Count - 1)) //Something Else Selected
                {
                    sFocusName = TxtSomethingElse.Text;

                    arrAllowedAppsWin.Add(new AppDetails("Microsoft Excel", "excel.exe"));
                    arrAllowedAppsWin.Add(new AppDetails("Microsoft Word", "winword.exe"));
                    arrAllowedAppsWin.Add(new AppDetails("Microsoft Edge", "msedge.exe"));
                    arrAllowedAppsWin.Add(new AppDetails("Mozilla Firefox", "firefox.exe"));
                    arrAllowedAppsWin.Add(new AppDetails("Google Chrome", "chrome.exe"));

                    arrAllowedUrls.Add("https://www.google.com");
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
              
                List<string> arrAllowed_apps = new List<string>();

                if (ComboBoxWorkType.SelectedIndex > 0 && ComboBoxWorkType.SelectedIndex < (ComboBoxWorkType.Items.Count - 1))
                {
                    int nSelectedIndex = ComboBoxWorkType.SelectedIndex - 1;//we have an additional element in combo box "Select"

                    if (appSettings.sFocusModeTemplates != null || (appSettings.sFocusModeTemplates.Length != 0))
                    {
                        //Parse the JSON string
                        try
                        {

                            JArray array = (JArray)JsonConvert.DeserializeObject(appSettings.sFocusModeTemplates);

                            for (int i = 0; i < array.Count(); i++)
                            {
                                if (i == nSelectedIndex)
                                {
                                    JObject item = JObject.Parse(array[i].ToString());


                                    if (item.ContainsKey("allowed_apps"))
                                    {
                                        JArray arrayurlsArray = (JArray)item.GetValue("allowed_apps");
                                        arrAllowed_apps = arrayurlsArray.ToObject<List<string>>();
                                    }
                                    if (item.ContainsKey("allowed_urls"))
                                    {
                                        arrAllowedUrls = item.GetValue("allowed_urls").ToObject<ObservableCollection<string>>();
                                    }
                                    break;
                                }

                            }

                            //We now need to make the allowed urls in our format.
                            for (int m = 0; m < arrAllowed_apps.Count(); m++)
                            {
                                arrAllowedAppsWin.Add(new AppDetails(arrAllowed_apps[m], ""));
                            }
                        }
                        catch (Exception ex)
                        {
                            LogWriter.LogWrite("sFocusModeTemplates reading filling() EX - " + ex.Message);
                        }
                    }
                }


/**
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
    **/
                appSettings.FocusModeNameToCreateWhileOnBoarding = new FocusMode(System.Guid.NewGuid().ToString(), sFocusName, arrAllowedUrls, new ObservableCollection<string>(), arrAllowedAppsWin, "#A3FFA1", false, false, true, false, false, false);
                appSettings.AddFocusModeToArrayAndUploadtoAPI(appSettings.FocusModeNameToCreateWhileOnBoarding);

                RegistryManager.SetFocusModeSetupDone(true);

                _parentWindow.ShowAIConfirmScreen();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Onboarding set work type btnContinue_Click EX -" + ex.Message);
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ComboBoxWorkType.SelectedIndex = 0;
        }
    }
}
