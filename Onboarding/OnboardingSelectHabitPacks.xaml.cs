using Focus_Bear.Utils;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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

namespace Focus_Bear.Onboarding
{
    /// <summary>
    /// Interaction logic for OnboardingSelectHabitPacks.xaml
    /// </summary>
    public partial class OnboardingSelectHabitPacks : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        private OnBoardingWindow _parentWindow;

        public OnboardingSelectHabitPacks()
        {
        }
        public OnboardingSelectHabitPacks(OnBoardingWindow parentWindow) : this()
        {
            LogWriter.LogWrite("OnboardingSelectHabitPacks(OnBoardingWindow parentWindow)");

            _parentWindow = parentWindow;

            InitializeComponent();

            SetWebView();

            webView.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;


            try
            {
                btnContinueTXT.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["buttonContinueText"].ToString();

                if (appSettings.bGoalHealthyHabits)
                    Heading.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["titleSelectHabitPack"].ToString();
                else
                    Heading.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["titleSelectBreaks"].ToString();
                //SelectHabits.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["titleSelectHabitPack"].ToString();
                //"titleSelectBreaks": "Choose your break activities to get started",
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingSelectHabitPacks()" + ex.Message);
                if (!appSettings.bGoalHealthyHabits)
                    Heading.Text = "Choose your break activities to get started"; //for time being
            }

            //ComboBoxPack
            //       HabitPackDesc
            //        MorningDuration
            //        EveningDuration
            //Check is we got some response from keywords api
            if (appSettings.sRawHabitPacks != null || (appSettings.sRawHabitPacks.Length != 0))
            {
                //Parse the JSON string
                try
                {

                    JArray array = (JArray)JsonConvert.DeserializeObject(appSettings.sRawHabitPacks); 
                 
                    for (int i = 0; i < array.Count(); i++ )
                    {
                        JObject PackSettings = JObject.Parse(array[i].ToString());
                        string sName = PackSettings.ContainsKey("pack_name") ? PackSettings["pack_name"].ToString() : "";
                        ComboBoxPack.Items.Add(sName);
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("ReadAndParseSettingsJson() EX - " + ex.Message);
                }
            }
 

        }

        private async void SetWebView()
        {
            try
            {
                var userDataFolder = System.IO.Path.Combine(AppSettings.AppDataDirPath, "FBCustomWebView2");

                var task = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                if (webView != null)
                {
                    await webView.EnsureCoreWebView2Async(task);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnboardingSelectHabitPacks SetWebView() EX - " + ex.Message);
 
            }
        }

        private void CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                try
                {


                    //this.webView.NavigationStarting += NavigationStarting;
                    //this.webView.NavigationCompleted += NavigationCompleted;
                    webView.CoreWebView2.Settings.AreHostObjectsAllowed = true;
                    webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                    webView.CoreWebView2.Settings.IsScriptEnabled = true;
                    webView.CoreWebView2.Settings.IsWebMessageEnabled = true;

                    webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                    webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

                    // webView.WebMessageReceived += webView_WebMessageReceived;

                    if (appSettings.sRawHabitPacks != null || (appSettings.sRawHabitPacks.Length != 0))
                    {
                        //Parse the JSON string
                        try
                        {
                            JArray array = (JArray)JsonConvert.DeserializeObject(appSettings.sRawHabitPacks);
                            for (int i = 0; i < array.Count(); i++)
                            {
                                if (ComboBoxPack.SelectedIndex == i)
                                {
                                    JObject PackSettings = JObject.Parse(array[i].ToString());
                                    appSettings.sHabitPackIdToInstall = PackSettings.ContainsKey("id") ? PackSettings["id"].ToString() : "";
                                    string sURL = string.Format("https://dashboard.focusbear.io/habit-packs/share/{0}?embedded=true", appSettings.sHabitPackIdToInstall);

                                    appSettings.sMorningRoutineDuration = PackSettings.ContainsKey("morning_routine_duration_seconds") ? PackSettings["morning_routine_duration_seconds"].ToString() : "";
                                    //MorningDuration.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["morningRoutineText"].ToString() + appSettings.SecondsToText(int.Parse(sMorningRoutineDuration)); 

                                    appSettings.sEveningRoutineDuration = PackSettings.ContainsKey("morning_routine_duration_seconds") ? PackSettings["morning_routine_duration_seconds"].ToString() : "";
                                    //EveningDuration.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["eveningRoutineText"].ToString() + appSettings.SecondsToText(int.Parse(sEveningRoutineDuration)); 

                                    webView.Source = new Uri(sURL, System.UriKind.Absolute);

                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogWriter.LogWrite("OnboardingSelectHabitPacks CoreWebView2InitializationCompleted() EX - " + ex.Message);
                        }
                    }



                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("OnboardingSelectHabitPacks CoreWebView2InitializationCompleted()" + ex.Message);
                }
            }
        }

        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            webView.Dispose();
            _parentWindow.ShowOnboardingAskWorkTimeScreen();
        }

        private void ComboBoxPack_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (appSettings.sRawHabitPacks != null || (appSettings.sRawHabitPacks.Length != 0))
            {
                //Parse the JSON string
                try
                {
                    JArray array = (JArray)JsonConvert.DeserializeObject(appSettings.sRawHabitPacks);
                    for (int i = 0; i < array.Count(); i++)
                    {
                        if (ComboBoxPack.SelectedIndex == i)
                        {
                            JObject PackSettings = JObject.Parse(array[i].ToString());
                            appSettings.sHabitPackIdToInstall = PackSettings.ContainsKey("id") ? PackSettings["id"].ToString() : "";
                            string sURL = string.Format("https://dashboard.focusbear.io/habit-packs/share/{0}?embedded=true", appSettings.sHabitPackIdToInstall);

                            appSettings.sMorningRoutineDuration = PackSettings.ContainsKey("morning_routine_duration_seconds") ? PackSettings["morning_routine_duration_seconds"].ToString() : "";
                            //MorningDuration.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["morningRoutineText"].ToString() + appSettings.SecondsToText(int.Parse(sMorningRoutineDuration)); 

                            appSettings.sEveningRoutineDuration = PackSettings.ContainsKey("morning_routine_duration_seconds") ? PackSettings["morning_routine_duration_seconds"].ToString() : "";
                            //EveningDuration.Text = appSettings.ConfigFileStrings["askAppWorkVcInfo"]["eveningRoutineText"].ToString() + appSettings.SecondsToText(int.Parse(sEveningRoutineDuration)); 

                            webView.Source = new Uri(sURL, System.UriKind.Absolute);

                        }
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("ReadAndParseSettingsJson() EX - " + ex.Message);
                }
            }

        }
    }
}
