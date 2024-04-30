using Focus_Bear.Utils;
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
using System.Windows.Shapes;

namespace Focus_Bear.Preferences
{
    /// <summary>
    /// Interaction logic for PomodoroTabAdvanced.xaml
    /// </summary>
    public partial class PomodoroTabAdvanced : Window
    {
        PreferencesWindow ParentWindow;
        AppSettings appSettings = AppSettings.GetInstance;
        public PomodoroTabAdvanced()
        {
            
        }

        public PomodoroTabAdvanced(PreferencesWindow _parentWindow)
        {
            InitializeComponent();
            ParentWindow = _parentWindow;
            this.Owner = ParentWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            try
            {
                UseSuperPomodoroCheckTxt.Text = appSettings.ConfigFileStrings["pomodoroPreferencesAdvancedInfo"]["checkBoxUseHardcorePomodoroText"].ToString();
                LockIn.Text = appSettings.ConfigFileStrings["pomodoroPreferencesAdvancedInfo"]["descriptionHardcorePomodoroText"].ToString();
                ChooseFocusMode.Text = appSettings.ConfigFileStrings["pomodoroPreferencesAdvancedInfo"]["chooseFocusText"].ToString();

                ComboBoxFocusModes.Text = appSettings.ConfigFileStrings["pomodoroPreferencesAdvancedInfo"]["checkBoxUseHardcorePomodoroText"].ToString();

                YouWontBeAbleToChange.Text = appSettings.ConfigFileStrings["pomodoroPreferencesAdvancedInfo"]["descriptionChooseFocusFirstText"].ToString();

                YouWillBeAbleToUseSite.Text = appSettings.ConfigFileStrings["pomodoroPreferencesAdvancedInfo"]["descriptionChooseFocusSecondText"].ToString();
                PomodoroCountText.Text = appSettings.ConfigFileStrings["pomodoroPreferencesAdvancedInfo"]["HowManyPomodoroText"].ToString();

                ScheduleDays.Text = appSettings.ConfigFileStrings["pomodoroPreferencesAdvancedInfo"]["selectDaysForPomodoroText"].ToString();

                Monday.Content = appSettings.ConfigFileStrings["common"]["mondayText"].ToString();
                Tuesday.Content = appSettings.ConfigFileStrings["common"]["tuesdayText"].ToString();
                Wednesday.Content = appSettings.ConfigFileStrings["common"]["wednesdayText"].ToString();
                Thursday.Content = appSettings.ConfigFileStrings["common"]["thursdayText"].ToString();
                Friday.Content = appSettings.ConfigFileStrings["common"]["fridayText"].ToString();
                Saturday.Content = appSettings.ConfigFileStrings["common"]["saturdayText"].ToString();
                Sunday.Content = appSettings.ConfigFileStrings["common"]["sundayText"].ToString();
               
                SkipMicroBreaks.Content = appSettings.ConfigFileStrings["pomodoroPreferencesAdvancedInfo"]["buttonSkipMicrobreaksText"].ToString();
               
                btnCancelTXT.Text = appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString();
                btnApplyTXT.Text = appSettings.ConfigFileStrings["pomodoroPreferencesAdvancedInfo"]["buttonApplyText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PomodoroTabAdvanced() texts ex - " + ex.Message);
            }

            try
            {
                for (int i = 0; i < appSettings.arrFocusModes.Count(); i++)
                {
                    if (!(appSettings.arrFocusModes[i].isMeetings || appSettings.arrFocusModes[i].isLockedFocus))
                        ComboBoxFocusModes.Items.Add(appSettings.arrFocusModes[i].name);
                }

                ComboBoxFocusModes.SelectedItem = appSettings.AutomaticSuperPomodoroFocusedName;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ComboBoxFocusModes.Items.Add() EX - " + ex.Message);
            }

            
            UseSuperPomodoroCheck.IsChecked = appSettings.bAutomaticUseSuperPomodoroModeSetting;
            UpdateControls(appSettings.bAutomaticUseSuperPomodoroModeSetting);

            ComboBoxNumberOfPomodoros.SelectedIndex = (appSettings.AutomaticNumberOfSuperPomodorosToRun - 2);

            SkipMicroBreaks.IsChecked = appSettings.bAutomaticSuperPomodoroSkipMicroBreaks;

            if(appSettings.AutomaticSuperPomodoroSelectedDaysArray != null && appSettings.AutomaticSuperPomodoroSelectedDaysArray.Count > 0)
            {
                //JArray array = (JArray)JsonConvert.DeserializeObject(appSettings.SuperPomodoroSelectedDaysArray);

                for (int i = 0; i < appSettings.AutomaticSuperPomodoroSelectedDaysArray.Count(); i++)
                {
                    string sName  = appSettings.AutomaticSuperPomodoroSelectedDaysArray[i].ToString();
                    
                    switch(sName)
                    {
                        case "Monday":
                            Monday.IsChecked = true;
                            break;
                        case "Tuesday":
                            Tuesday.IsChecked = true;
                            break;
                        case "Wednesday":
                            Wednesday.IsChecked = true;
                            break;
                        case "Thursday": 
                            Thursday.IsChecked = true;
                            break;
                        case "Friday":
                            Friday.IsChecked = true;
                            break;
                        case "Saturday":
                            Saturday.IsChecked = true;
                            break;
                        case "Sunday":
                            Sunday.IsChecked = true;
                            break;

                    }

                }
            }

            
        }
      
        private void UseSuperPomodoroCheck_Checked(object sender, RoutedEventArgs e)
        {
            UpdateControls(true);
        }

        private void UseSuperPomodoroCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateControls(false);
        }
        private void UpdateControls(bool bEnabled)
        {
            ComboBoxFocusModes.IsEnabled = bEnabled;
            ComboBoxNumberOfPomodoros.IsEnabled = bEnabled;
            Monday.IsEnabled = bEnabled;
            Tuesday.IsEnabled = bEnabled;
            Wednesday.IsEnabled = bEnabled;
            Thursday.IsEnabled = bEnabled;
            Friday.IsEnabled = bEnabled;
            Saturday.IsEnabled = bEnabled;
            Sunday.IsEnabled = bEnabled;
            SkipMicroBreaks.IsEnabled = bEnabled;
        }
       
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnApply_Click(object sender, RoutedEventArgs e)
        {
            bool bAnyDaySelected = false;

            //Prepare the days array
            appSettings.AutomaticSuperPomodoroSelectedDaysArray.Clear();

            if (Monday.IsChecked.Value)
            {
                appSettings.AutomaticSuperPomodoroSelectedDaysArray.Add("Monday");
                bAnyDaySelected = true;
            }

            if (Tuesday.IsChecked.Value)
            {
                appSettings.AutomaticSuperPomodoroSelectedDaysArray.Add("Tuesday");
                bAnyDaySelected = true;
            }

            if (Wednesday.IsChecked.Value)
            {
                appSettings.AutomaticSuperPomodoroSelectedDaysArray.Add("Wednesday");
                bAnyDaySelected = true;
            }

            if (Thursday.IsChecked.Value)
            {
                appSettings.AutomaticSuperPomodoroSelectedDaysArray.Add("Thursday");
                bAnyDaySelected = true;
            }

            if (Friday.IsChecked.Value)
            {
                appSettings.AutomaticSuperPomodoroSelectedDaysArray.Add("Friday");
                bAnyDaySelected = true;
            }

            if (Saturday.IsChecked.Value)
            {
                appSettings.AutomaticSuperPomodoroSelectedDaysArray.Add("Saturday");
                bAnyDaySelected = true;
            }

            if (Sunday.IsChecked.Value)
            {
                appSettings.AutomaticSuperPomodoroSelectedDaysArray.Add("Sunday");
                bAnyDaySelected = true;
            }

            if(UseSuperPomodoroCheck.IsChecked.Value && !bAnyDaySelected)
            {
                FBMessageBox msgbox = new FBMessageBox(appSettings.ConfigFileStrings["pomodoroPreferencesAdvancedInfo"]["selectAtleastOneDayText"].ToString());
                msgbox.Owner = this;
                msgbox.ShowDialog();
                
                return;
            }

            appSettings.bAutomaticUseSuperPomodoroModeSetting = UseSuperPomodoroCheck.IsChecked.Value;
            appSettings.AutomaticSuperPomodoroFocusedName = ComboBoxFocusModes.SelectedItem.ToString();
            appSettings.bAutomaticSuperPomodoroSkipMicroBreaks = SkipMicroBreaks.IsChecked.Value;

            ComboBoxItem typeItem = (ComboBoxItem)ComboBoxNumberOfPomodoros.SelectedItem;
            appSettings.AutomaticNumberOfSuperPomodorosToRun = int.Parse(typeItem.Content.ToString());

            Close();
        }
 
    }
}
