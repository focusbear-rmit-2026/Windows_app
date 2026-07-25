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
        FocusBearMainWindow ParentWindow;

        public PomodoroTab(FocusBearMainWindow _parentWindow)
        {
            InitializeComponent();
            ParentWindow = _parentWindow;

            PomodoroMinutes.GotFocus += RemovePlaceholder;
            PomodoroMinutes.LostFocus += SetPlaceholder;
            WorkRatioInput.GotFocus += RemovePlaceholder;
            WorkRatioInput.LostFocus += SetPlaceholder;
            BreakRatioInput.GotFocus += RemovePlaceholder;
            BreakRatioInput.LostFocus += SetPlaceholder;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["preferencesInfo"]["pomodoroDescriptionText"].ToString();
                btnLearnMoreTXT.Text = appSettings.ConfigFileStrings["preferencesInfo"]["buttonLearnPomodoroTechniqueText"].ToString();
                PomodoroMode.Text = appSettings.ConfigFileStrings["preferencesInfo"]["checkBoxPomodoroText"].ToString();
                PomodoroModeDesc.Text = appSettings.ConfigFileStrings["tooltips"]["pomodoro_AfterMorning"].ToString();
                btnAdvancedTXT.Text = appSettings.ConfigFileStrings["preferencesInfo"]["buttonAdvancedText"].ToString();

                UpdateTextsAndButtons();

                PomodoroModeDuration.Text = appSettings.ConfigFileStrings["preferencesInfo"]["pomodoroModeDurationText"].ToString();
                MinutesTitle.Text = appSettings.ConfigFileStrings["preferencesInfo"]["minutesText"].ToString();
                WorkToBreakRatioLabel.Text = appSettings.ConfigFileStrings["focusPreferencesInfo"]["titleWorkToBreakRatio"].ToString();
                RatioToText.Text = appSettings.ConfigFileStrings["focusPreferencesInfo"]["toText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PomodoroTab texts ex = " + ex.Message);
            }
        }

        private void UpdateRatioDescription()
        {
            try
            {
                string desc = appSettings.ConfigFileStrings["focusPreferencesInfo"]["descriptionWorkToBreakRatio"].ToString();
                desc = desc.Replace("<WORKRATIO>", appSettings.PomodoroWorkToBreakRatio.ToString());
                desc = desc.Replace("<BREAKRATIO>", appSettings.PomodoroBreakRatio.ToString());
                WorkToBreakRatioDesc.Text = desc;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("PomodoroTab UpdateRatioDescription ex = " + ex.Message);
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
                else if (textBox == WorkRatioInput)
                {
                    textBox.Text = "5";
                    textBox.Foreground = Brushes.Gray;
                    textBox.Tag = "placeholder";
                    appSettings.PomodoroWorkToBreakRatio = 5;
                }
                else if (textBox == BreakRatioInput)
                {
                    textBox.Text = "1";
                    textBox.Foreground = Brushes.Gray;
                    textBox.Tag = "placeholder";
                    appSettings.PomodoroBreakRatio = 1;
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
            PomodoroMinutes.Text = appSettings.PomodoroMinutes;
            WorkRatioInput.Text = appSettings.PomodoroWorkToBreakRatio.ToString();
            BreakRatioInput.Text = appSettings.PomodoroBreakRatio.ToString();
            EnterPomodoroCheck.IsChecked = appSettings.bEnterPomodoroModeAfterMorningRoutine;

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

        private bool ValidateWorkToBreakRatio(bool showErrors = true)
        {
            if (string.IsNullOrWhiteSpace(WorkRatioInput.Text))
            {
                if (showErrors) ShowErrorMessage(appSettings.ConfigFileStrings["focusPreferencesInfo"]["workRatioEmptyErrorText"].ToString());
                return false;
            }
            if (string.IsNullOrWhiteSpace(BreakRatioInput.Text))
            {
                if (showErrors) ShowErrorMessage(appSettings.ConfigFileStrings["focusPreferencesInfo"]["breakRatioEmptyErrorText"].ToString());
                return false;
            }

            if (!int.TryParse(WorkRatioInput.Text, out int workRatio) || workRatio <= 0)
            {
                if (showErrors) ShowErrorMessage(appSettings.ConfigFileStrings["focusPreferencesInfo"]["workRatioInvalidErrorText"].ToString());
                return false;
            }
            if (!int.TryParse(BreakRatioInput.Text, out int breakRatio) || breakRatio <= 0)
            {
                if (showErrors) ShowErrorMessage(appSettings.ConfigFileStrings["focusPreferencesInfo"]["breakRatioInvalidErrorText"].ToString());
                return false;
            }

            appSettings.PomodoroWorkToBreakRatio = workRatio;
            appSettings.PomodoroBreakRatio = breakRatio;
            UpdateRatioDescription();
            return true;
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

            if (ValidatePomodoroMinutes(true))
            {
                LocalSettingsManager.PutLocalSettings();
                ShowSuccessMessage("Settings saved successfully!");
            }
        }

        private void SaveRatio_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(WorkRatioInput.Text))
            {
                WorkRatioInput.Text = "5";
                appSettings.PomodoroWorkToBreakRatio = 5;
            }
            if (string.IsNullOrWhiteSpace(BreakRatioInput.Text))
            {
                BreakRatioInput.Text = "1";
                appSettings.PomodoroBreakRatio = 1;
            }

            if (ValidateWorkToBreakRatio(true))
            {
                LocalSettingsManager.PutLocalSettings();
                ShowSuccessMessage("Settings saved successfully!");
            }
        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                PomodoroMinutes.Text = appSettings.PomodoroMinutes;
                PomodoroMinutes.Tag = null;
                PomodoroMinutes.Foreground = Brushes.Black;

                WorkRatioInput.Text = appSettings.PomodoroWorkToBreakRatio.ToString();
                WorkRatioInput.Tag = null;
                WorkRatioInput.Foreground = Brushes.Black;

                BreakRatioInput.Text = appSettings.PomodoroBreakRatio.ToString();
                BreakRatioInput.Tag = null;
                BreakRatioInput.Foreground = Brushes.Black;

                EnterPomodoroCheck.IsChecked = appSettings.bEnterPomodoroModeAfterMorningRoutine;
                UpdateTextsAndButtons();
                UpdateRatioDescription();
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

        private void WorkRatioInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(WorkRatioInput.Text))
                ValidateWorkToBreakRatio(false);
            SetPlaceholder(sender, e);
        }

        private void BreakRatioInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(BreakRatioInput.Text))
                ValidateWorkToBreakRatio(false);
            SetPlaceholder(sender, e);
        }
    }
}