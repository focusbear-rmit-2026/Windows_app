using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
    /// Interaction logic for PomodoroTab.xaml
    /// </summary>
    public partial class PomodoroTab : UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;
        PreferencesWindow ParentWindow;
        public PomodoroTab()
        {
        }
        public PomodoroTab(PreferencesWindow _parentWindow)
        {
            InitializeComponent();
            ParentWindow = _parentWindow;

            PomodoroMinutes.Text = appSettings.PomodoroMinutes;
            BreakMinutes.Text = appSettings.PomodoroBreakMinutes;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["preferencesInfo"]["pomodoroDescriptionText"].ToString();
                btnLearnMoreTXT.Text = appSettings.ConfigFileStrings["preferencesInfo"]["buttonLearnPomodoroTechniqueText"].ToString(); 
                PomodoroMode.Text = appSettings.ConfigFileStrings["preferencesInfo"]["titlePomodoro"].ToString();
                EnterPomodoroCheckTxt.Text = appSettings.ConfigFileStrings["preferencesInfo"]["checkBoxPomodoroText"].ToString();

                btnAdvancedTXT.Text = appSettings.ConfigFileStrings["preferencesInfo"]["buttonAdvancedText"].ToString();
                
                EnterPomodoroCheck.IsChecked = appSettings.bEnterPomodoroModeAfterMorningRoutine;
                UpdateTextsAndButtons();

                PomodoroModeDuration.Text = appSettings.ConfigFileStrings["preferencesInfo"]["pomodoroModeDurationText"].ToString(); 
                MinutesTitle.Text = appSettings.ConfigFileStrings["preferencesInfo"]["minutesText"].ToString();
                BreakDuration.Text = appSettings.ConfigFileStrings["preferencesInfo"]["breakDurationText"].ToString(); 
                BreakMinutesTitle.Text = appSettings.ConfigFileStrings["preferencesInfo"]["minutesText"].ToString(); 

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PomodoroTab texts ex = " + ex.Message);
            }
        }
        private void btnLearnMore_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("https://medium.com/@focusbearteam/studying-on-tomato-oclock-a-student-s-guide-to-using-the-pomodoro-technique-efficiently-9a037cc509be");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnLearnMore_Click ex = " + ex.Message);
            }
        }

        private void EnterPomodoroCheck_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.bEnterPomodoroModeAfterMorningRoutine = true;
            UpdateTextsAndButtons();
        }

        private void EnterPomodoroCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            appSettings.bEnterPomodoroModeAfterMorningRoutine = false;
            UpdateTextsAndButtons();
        }
        private void UpdateTextsAndButtons()
        {
            if (appSettings.bEnterPomodoroModeAfterMorningRoutine)
            {
                if (appSettings.bAutomaticUseSuperPomodoroModeSetting)
                {
                    //"hardCorePomodoroEnabledText": "Super Pomodoro mode enabled to focus on <FOCUSTYPE> for <COUNT> pomodoros on <DAYS>.",
                    string sText = appSettings.ConfigFileStrings["preferencesInfo"]["hardCorePomodoroEnabledText"].ToString();
                    sText = sText.Replace("<FOCUSTYPE>", appSettings.AutomaticSuperPomodoroFocusedName);
                    sText = sText.Replace("<COUNT>", appSettings.AutomaticNumberOfSuperPomodorosToRun.ToString());

                    string sDays = "";
                    for (int i = 0; i < appSettings.AutomaticSuperPomodoroSelectedDaysArray.Count(); i++)
                    {
                        string sName = appSettings.AutomaticSuperPomodoroSelectedDaysArray[i].ToString();
                        sDays = sDays + sName + ", ";
                    }
                    sDays = sDays.Trim();
                    //Remove last comma
                    sDays = sDays.Remove(sDays.Length - 1, 1);

                    sText = sText.Replace("<DAYS>", sDays);

                    SuperPomodoroStatus.Text = sText;
                }
                else
                {
                    SuperPomodoroStatus.Text = appSettings.ConfigFileStrings["preferencesInfo"]["hardCorePomodoroNotEnabledText"].ToString();
                }
                btnAdvanced.IsEnabled = true;
            }
            else
            {
                SuperPomodoroStatus.Text = appSettings.ConfigFileStrings["preferencesInfo"]["automaticPomodoroNotEnabledText"].ToString();
                btnAdvanced.IsEnabled = false;
            }
        }
        private void btnAdvanced_Click(object sender, RoutedEventArgs e)
        {
            PomodoroTabAdvanced wndAdvanced = new PomodoroTabAdvanced(ParentWindow);
            ParentWindow.GridShade.Visibility = Visibility.Visible;
            wndAdvanced.ShowDialog();
            UpdateTextsAndButtons();
            ParentWindow.GridShade.Visibility = Visibility.Collapsed;
        }

        private void Minutes_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {

            Regex regex = new Regex("^[0-9]*$");
            e.Handled = !regex.IsMatch((sender as TextBox).Text.Insert((sender as TextBox).SelectionStart, e.Text));
        }

        private void Minutes_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = false;
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }

        }

        private void Minutes_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if ((e.Key == Key.Delete) || (e.Key == Key.Back))
            {
                string text = PomodoroMinutes.Text;
                text = text.Trim();
                if (text.Length > 0)
                {
                    ValidatePomodoroMinutes();
                }
                else
                {
                    ValidatePomodoroMinutes();
                }
            }
        }

        private void PomodoroMinutes_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidatePomodoroMinutes();
        }

        void ValidatePomodoroMinutes()
        {
            string Minutestext = PomodoroMinutes.Text;

            if (Minutestext.Length > 0)
            {
                if(int.Parse(Minutestext) == 0)
                {
                    FBMessageBox msgbox = new FBMessageBox(appSettings.ConfigFileStrings["preferencesInfo"]["workDurationZeroErrorText"].ToString());
                    msgbox.Owner = ParentWindow;
                    ParentWindow.GridShade.Visibility = Visibility.Visible;
                    msgbox.ShowDialog();
                    ParentWindow.GridShade.Visibility = Visibility.Collapsed;
                    PomodoroMinutes.Text = "25";
                }
            }
            else
            {
                FBMessageBox msgbox = new FBMessageBox(appSettings.ConfigFileStrings["preferencesInfo"]["workDurationEmptyErrorText"].ToString());
                msgbox.Owner = ParentWindow;
                ParentWindow.GridShade.Visibility = Visibility.Visible;
                msgbox.ShowDialog();
                ParentWindow.GridShade.Visibility = Visibility.Collapsed;
                PomodoroMinutes.Text = "25";
            }

            appSettings.PomodoroMinutes = PomodoroMinutes.Text;//Assign to sync to backend
        }
        private void MinutesBreak_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {

            Regex regex = new Regex("^[0-9]*$");
            e.Handled = !regex.IsMatch((sender as TextBox).Text.Insert((sender as TextBox).SelectionStart, e.Text));
        }

        private void MinutesBreak_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = false;
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private void MinutesBreak_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if ((e.Key == Key.Delete) || (e.Key == Key.Back))
            {
                string text = BreakMinutes.Text;
                text = text.Trim();
                if (text.Length > 0)
                {
                    ValidateBreakMinutes();
                }
                else
                {
                    ValidateBreakMinutes();
                }
            }
        }

        private void MinutesBreak_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateBreakMinutes();
        }

        void ValidateBreakMinutes()
        {
            string Minutestext = BreakMinutes.Text;

            if (Minutestext.Length > 0)
            {
                if (int.Parse(Minutestext) == 0)
                {
                    FBMessageBox msgbox = new FBMessageBox(appSettings.ConfigFileStrings["preferencesInfo"]["breakDurationZeroErrorText"].ToString());
                    msgbox.Owner = ParentWindow;
                    ParentWindow.GridShade.Visibility = Visibility.Visible;
                    msgbox.ShowDialog();
                    ParentWindow.GridShade.Visibility = Visibility.Collapsed;
                    BreakMinutes.Text = "5";
                }
            }
            else
            {
                FBMessageBox msgbox = new FBMessageBox(appSettings.ConfigFileStrings["preferencesInfo"]["breakDurationEmptyErrorText"].ToString());
                msgbox.Owner = ParentWindow;
                ParentWindow.GridShade.Visibility = Visibility.Visible;
                msgbox.ShowDialog();
                ParentWindow.GridShade.Visibility = Visibility.Collapsed;
                BreakMinutes.Text = "5";
            }
            appSettings.PomodoroBreakMinutes = BreakMinutes.Text;//Assign to sync to backend
        }
    }
}
