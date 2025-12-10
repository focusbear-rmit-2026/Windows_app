using Focus_Bear.Models;
using Focus_Bear.Utils;
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

namespace Focus_Bear.Preferences.SettingsTab
{
    /// <summary>
    /// Interaction logic for UI.xaml
    /// </summary>
    public partial class UI : UserControl
    {
        PreferencesWindow _ParentWindow = null;

        AppSettings appSettings = AppSettings.GetInstance;
        private bool bDarkModeVisible = true;
        public UI()
        {
            InitializeComponent();
        }
        public UI(PreferencesWindow ParentWindow)
        {
            InitializeComponent();
            _ParentWindow = ParentWindow;

            try
            {
                FullScreen.Text = appSettings.ConfigFileStrings["preferencesInfo"]["checkBoxFullScreenEnabledText"].ToString();
                FullScreenDesc.Text = appSettings.ConfigFileStrings["tooltips"]["ui_ActivitiesFullScreen"].ToString();

                FullScreenCheckbox.IsChecked = appSettings.bPlayActivitiesInFullScreenMode;

                //Fonts related
                SelectFont.Text = appSettings.ConfigFileStrings["preferencesInfo"]["titleFont"].ToString();
                SelectFontDesc.Text = appSettings.ConfigFileStrings["tooltips"]["accessibility_Font"].ToString();
                foreach (string sFont in appSettings.arrFontFamily)
                {
                    Fonts.Items.Add(sFont);
                }

                Fonts.SelectionChanged -= Fonts_SelectionChanged;
                Fonts.SelectedItem = appSettings.sSelectedAppFont;
                Fonts.SelectionChanged += Fonts_SelectionChanged;

                //Language Related
                SelectLanguage.Text = appSettings.ConfigFileStrings["preferencesInfo"]["titleSelectLanguage"].ToString();
                SelectLanguageDesc.Text = appSettings.ConfigFileStrings["tooltips"]["ui_SelectLanguage"].ToString();

                Language.Items.Add(appSettings.ConfigFileStrings["languageInfo"]["languageEnglish"].ToString());
                Language.Items.Add(appSettings.ConfigFileStrings["languageInfo"]["languageSpanish"].ToString());

                if (appSettings.sLangCode == "en")
                {
                    Language.SelectedIndex = 0;
                }
                else if (appSettings.sLangCode == "es")
                {
                    Language.SelectedIndex = 1;
                }

                //Dark mode realted
                DarkMode.Visibility = bDarkModeVisible ? Visibility.Visible : Visibility.Collapsed;
                SelectDarkMode.Visibility = bDarkModeVisible ? Visibility.Visible : Visibility.Collapsed;

                SelectDarkMode.Text = appSettings.ConfigFileStrings["preferencesInfo"]["titleDarkMode"].ToString();
                SelectDarkModeDesc.Text = appSettings.ConfigFileStrings["tooltips"]["ui_DarkMode"].ToString();

                DarkMode.Items.Add(appSettings.ConfigFileStrings["preferencesInfo"]["appAppearanceOptions"][2].ToString());
                DarkMode.Items.Add(appSettings.ConfigFileStrings["preferencesInfo"]["appAppearanceOptions"][1].ToString());
                DarkMode.Items.Add(appSettings.ConfigFileStrings["preferencesInfo"]["appAppearanceOptions"][0].ToString());

                if (appSettings.sTheme == "Light" && appSettings.bAutoTheme == false)
                {
                    DarkMode.SelectedIndex = 0;
                }
                else if (appSettings.sTheme == "Dark" && appSettings.bAutoTheme == false)
                {
                    DarkMode.SelectedIndex = 1;
                }
                else if (appSettings.bAutoTheme == true)
                {
                    DarkMode.SelectedIndex = 2;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Settings UI Page text setting ex -" + ex.Message);
            }
        }

        private void FullScreenCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.bPlayActivitiesInFullScreenMode = true;
        }

        private void FullScreenCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bPlayActivitiesInFullScreenMode = false;
        }

        private void Fonts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            appSettings.sSelectedAppFont = Fonts.SelectedItem.ToString();
            RegistryManager.SetApplicationFont();
            FBMessageBox msgBox = new FBMessageBox(appSettings.ConfigFileStrings["preferencesInfo"]["alertChangeLanguageInfoText"].ToString());
            msgBox.Owner = _ParentWindow;
            msgBox.ShowDialog();
            appSettings.bRestartAppNeeded = true;
            Application.Current.Shutdown(0);
        }

        private void Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int nSelectedIndex = 0;

            if (appSettings.sLangCode == "en")
            {
                nSelectedIndex = 0;
            }
            else if (appSettings.sLangCode == "es")
            {
                nSelectedIndex = 1;
            }

            if (Language.SelectedIndex != nSelectedIndex)
            {
                string ss = appSettings.ConfigFileStrings["preferencesInfo"]["alertChangeLanguageMessageText"].ToString();// "alertChangeLanguageMessageText":"Are you sure you want to change the language to <LANGUAGE>?",
                ss = ss.Replace("<LANGUAGE>", Language.SelectedItem.ToString());
                FBMessageBox ff = new FBMessageBox(ss, "Focus Bear", MessageBoxButton.YesNo);
                ff.Owner = _ParentWindow;
                ff.ShowDialog();
                if (ff.btnClicked == MessageBoxResult.No)
                {
                    Language.SelectedIndex = nSelectedIndex;
                    return;
                }
                else
                {
                    //we will need to write the lang code and restart the app.
                    if (Language.SelectedIndex == 0)//en
                    {
                        appSettings.sLangCode = "en";
                    }
                    else if (Language.SelectedIndex == 1)//es
                    {
                        appSettings.sLangCode = "es";
                    }
                    RegistryManager.SetLangCode();
                    appSettings.bRestartAppNeeded = true;
                    Application.Current.Shutdown(0);
                }

            }
        }

        private void DarkMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DarkMode.SelectedItem != null)
            {
                string selectedTheme = appSettings.sTheme;

                if (DarkMode.SelectedIndex == 0)
                {
                    selectedTheme = "Light";
                    appSettings.sTheme = "Light";
                    appSettings.bAutoTheme = false;
                }
                else if (DarkMode.SelectedIndex == 1)
                {
                    selectedTheme = "Dark";
                    appSettings.sTheme = "Dark";
                    appSettings.bAutoTheme = false;
                }
                else if (DarkMode.SelectedIndex == 2)
                {
                    ////auto theme
                    bool isLightTheme = Global.IsLightTheme();
                    selectedTheme = isLightTheme ? "Light" : "Dark";
                    appSettings.sTheme = selectedTheme;
                    appSettings.bAutoTheme = true;
                }
                RegistryManager.SetThemeMode();
                RegistryManager.SetAutoThemeMode();
                ((App)Application.Current).ChangeTheme(selectedTheme);
            }
        }
    }
}
