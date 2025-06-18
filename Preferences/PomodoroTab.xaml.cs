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

        public PomodoroTab(PreferencesWindow _parentWindow)
        {
            InitializeComponent();
            ParentWindow = _parentWindow;

            PomodoroMinutes.Text = appSettings.PomodoroMinutes;
            BreakMinutes.Text = appSettings.PomodoroBreakMinutes;

            PomodoroMinutes.GotFocus += RemovePlaceholder;
            PomodoroMinutes.LostFocus += SetPlaceholder;
            BreakMinutes.GotFocus += RemovePlaceholder;
            BreakMinutes.LostFocus += SetPlaceholder;

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

        private void RemovePlaceholder(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Tag?.ToString() == "placeholder")
            {
                textBox.Text = "";
                textBox.Foreground = Brushes.Black;
                textBox.Tag = null;
            }
        }

        private void SetPlaceholder(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && string.IsNullOrWhiteSpace(textBox.Text))
            {
                if (textBox == PomodoroMinutes)
                {
                    textBox.Text = "25";
                    textBox.Foreground = Brushes.Gray;
                    textBox.Tag = "placeholder";
                    appSettings.PomodoroMinutes = "25";
                }
                else
                {
                    textBox.Text = "5";
                    textBox.Foreground = Brushes.Gray;
                    textBox.Tag = "placeholder";
                    appSettings.PomodoroBreakMinutes = "5";
                }
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
                    string sText = appSettings.ConfigFileStrings["preferencesInfo"]["hardCorePomodoroEnabledText"].ToString();
                    sText = sText.Replace("<FOCUSTYPE>", appSettings.AutomaticSuperPomodoroFocusedName);
                    sText = sText.Replace("<COUNT>", appSettings.AutomaticNumberOfSuperPomodorosToRun.ToString());

                    string sDays = string.Join(", ", appSettings.AutomaticSuperPomodoroSelectedDaysArray);
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

        private void Minutes_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("^[0-9]*$");
            e.Handled = !regex.IsMatch((sender as TextBox).Text.Insert((sender as TextBox).SelectionStart, e.Text));
        }

        private void Minutes_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private bool ValidatePomodoroMinutes(bool showErrors = true)
        {
            string minutesText = PomodoroMinutes.Text;

            if (string.IsNullOrWhiteSpace(minutesText))
            {
                if (showErrors)
                {
                    ShowErrorMessage(appSettings.ConfigFileStrings["preferencesInfo"]["workDurationEmptyErrorText"].ToString());
                }
                return false;
            }

            if (int.TryParse(minutesText, out int minutes))
            {
                if (minutes <= 0)
                {
                    if (showErrors)
                    {
                        ShowErrorMessage(appSettings.ConfigFileStrings["preferencesInfo"]["workDurationZeroErrorText"].ToString());
                    }
                    return false;
                }
                appSettings.PomodoroMinutes = minutesText;
                return true;
            }

            if (showErrors)
            {
                ShowErrorMessage("Please enter a valid number.");
            }
            return false;
        }

        private bool ValidateBreakMinutes(bool showErrors = true)
        {
            string minutesText = BreakMinutes.Text;

            if (string.IsNullOrWhiteSpace(minutesText))
            {
                if (showErrors)
                {
                    ShowErrorMessage(appSettings.ConfigFileStrings["preferencesInfo"]["breakDurationEmptyErrorText"].ToString());
                }
                return false;
            }

            if (int.TryParse(minutesText, out int minutes))
            {
                if (minutes <= 0)
                {
                    if (showErrors)
                    {
                        ShowErrorMessage(appSettings.ConfigFileStrings["preferencesInfo"]["breakDurationZeroErrorText"].ToString());
                    }
                    return false;
                }
                appSettings.PomodoroBreakMinutes = minutesText;
                return true;
            }

            if (showErrors)
            {
                ShowErrorMessage("Please enter a valid number.");
            }
            return false;
        }

        private void ShowErrorMessage(string message)
        {
            FBMessageBox msgbox = new FBMessageBox(message);
            msgbox.Owner = ParentWindow;
            ParentWindow.GridShade.Visibility = Visibility.Visible;
            msgbox.ShowDialog();
            ParentWindow.GridShade.Visibility = Visibility.Collapsed;
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PomodoroMinutes.Text))
            {
                PomodoroMinutes.Text = "25";
                appSettings.PomodoroMinutes = "25";
            }

            if (string.IsNullOrWhiteSpace(BreakMinutes.Text))
            {
                BreakMinutes.Text = "5";
                appSettings.PomodoroBreakMinutes = "5";
            }

            bool pomodoroValid = ValidatePomodoroMinutes(true);
            bool breakValid = ValidateBreakMinutes(true);

            if (pomodoroValid && breakValid)
            {
                ShowSuccessMessage("Settings saved successfully!");
            }
        }

        private void ShowSuccessMessage(string message)
        {
            FBMessageBox msgbox = new FBMessageBox(message);
            msgbox.Owner = ParentWindow;
            msgbox.ShowDialog();
        }

        private void PomodoroMinutes_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(PomodoroMinutes.Text))
            {
                ValidatePomodoroMinutes(true);
            }
            SetPlaceholder(sender, e);
        }

        private void BreakMinutes_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(BreakMinutes.Text))
            {
                ValidateBreakMinutes(true);
            }
            SetPlaceholder(sender, e);
        }
    }
}