using Focus_Bear.Models;
using Focus_Bear.Preferences;
using Focus_Bear.Utils;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives; // for PlacementMode
using System.Windows.Data;
using System.Windows.Media;
using System.Globalization;
using System;
using Microsoft.IdentityModel.Tokens;
using System.Threading.Tasks;
using Focus_Bear.Services;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace Focus_Bear.Onboarding
{

    public partial class CreateBlockingSchedule : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;

        FocusBearMainWindow _ParentWindow = null;

        private CustomBlockingSchedule _instance;
        private CustomBlockingSchedule _editableCopy;

        public CreateBlockingSchedule(FocusBearMainWindow ParentWindow, CustomBlockingSchedule instance = null)
        {
            InitializeComponent();

            _ParentWindow = ParentWindow;

            if (instance == null)
            {
                _editableCopy = new CustomBlockingSchedule
                {
                    id = "", // will be assigned on save
                    name = "",
                    isOn = false,
                    // Default to the "Weekdays" scope (Mon-Fri), matching mac's onboarding default.
                    DaysOfWeek = new bool[] { true, true, true, true, true, false, false },

                    //make sure toa dd in 24 hours format which server accepts and convert to am/pm per UI
                    StartTimeValue = DateTime.Now.ToString("HH:mm:ss"),
                    EndTimeValue = DateTime.Now.AddHours(1).ToString("HH:mm:ss"),

                    SelectedFocusModeId = null,
                    BlockLevel = FOCUSMODESTRICTNESS.CHILL_MODE,

                    // Onboarding shows AI/skip-microbreaks directly (More Options expanded by default)
                    // and mac defaults both on there, unlike the in-app editor which defaults both off.
                    bAIBlockingEnabled = true,
                    bMicroBreaksEnabled = false,
                    Emoji = null,

                };
                _instance = _editableCopy;
            }

            //Set start time and end time in UI
            // Parse as we know server side it 24 hour format
            TimeSpan timeStart = DateTime.ParseExact(_editableCopy.StartTimeValue, "HH:mm:ss", CultureInfo.InvariantCulture).TimeOfDay;
            TimeSpan timeEnd = DateTime.ParseExact(_editableCopy.EndTimeValue, "HH:mm:ss", CultureInfo.InvariantCulture).TimeOfDay;
            ActiveTimeSlider.SetTimes(timeStart, timeEnd);

            // Bind to UI
            DataContext = _editableCopy;

            UpdateWeekdayPresetHighlight();
            UpdateStrictnessDescription(_editableCopy.BlockLevel);
            UpdateEmojiPickerText();

            //Initialize Texts

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["createFirstBlockingScheduleVcInfo"]["title"].ToString();
                Bubble1.Text = appSettings.ConfigFileStrings["createFirstBlockingScheduleVcInfo"]["bubbleText"].ToString(); //"For example, block distracting websites during work hours";
                btnAddBlockText.Text = appSettings.ConfigFileStrings["createFirstBlockingScheduleVcInfo"]["buttonAddBlock"].ToString(); //"Add Block";

                BlockName.Text = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["titleScheduleName"].ToString();
                Title.Text = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["title"].ToString();

                BlockingScheduleName.Text = "";

                // Single-letter day circles (mac shows "M T W T F S S"), same as the in-app editor -
                // reuses the existing localized dayMon/dayTue/... strings rather than new config keys.
                Mon.Content = FirstLetter(appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["dayMon"].ToString());
                Tue.Content = FirstLetter(appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["dayTue"].ToString());
                Wed.Content = FirstLetter(appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["dayWed"].ToString());
                Thu.Content = FirstLetter(appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["dayThu"].ToString());
                Fri.Content = FirstLetter(appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["dayFri"].ToString());
                Sat.Content = FirstLetter(appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["daySat"].ToString());
                Sun.Content = FirstLetter(appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["daySun"].ToString());

                // These keys aren't in the shared assets config yet - fall back to English rather than
                // let a missing key throw and abort the rest of this try block.
                BtnWeekdays.Content = GetConfigStringOrDefault("blockingScheduleEditorVcInfo", "titleWeekdays", "Weekdays");
                BtnWeekends.Content = GetConfigStringOrDefault("blockingScheduleEditorVcInfo", "titleWeekends", "Weekends");
                BtnEveryday.Content = GetConfigStringOrDefault("blockingScheduleEditorVcInfo", "titleEveryday", "Everyday");

                ActiveTimeTitle.Text = GetConfigStringOrDefault("blockingScheduleEditorVcInfo", "titleActiveTime", "Active time");

                StrictnessLevel.Text = appSettings.ConfigFileStrings["focusInfo"]["startingFocusModeInfo"]["titleStrictnessLevel"].ToString();

                RadioFlexible.Content = GetConfigStringOrDefault("blockingScheduleEditorVcInfo", "titleStrictnessFlexible", "Flexible");
                RadioModerate.Content = GetConfigStringOrDefault("blockingScheduleEditorVcInfo", "titleStrictnessModerate", "Moderate");
                RadioStrict.Content = GetConfigStringOrDefault("blockingScheduleEditorVcInfo", "titleStrictnessStrict", "Strict");

                MoreOptionsHeaderText.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["moreOptionsText"].ToString();

                SkipMicroBreaks.Content = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["buttonSkipMicrobreaks"].ToString();
                UseAIToDecide.Content = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["buttonEnableAi"].ToString();
                MicroBreaksInfo.ToolTip = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["buttonSkipMicrobreaks"].ToString(); //todo
                UseAIToDecideInfo.ToolTip = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["buttonEnableAi"].ToString(); //todo

                if(_instance.name.IsNullOrEmpty())
                {
                    BlockingScheduleName.Focus();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AddApp load text ex + " + ex.Message);
            }
        }

        private void btnAddBlock_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Validate inputs
                if (string.IsNullOrWhiteSpace(_editableCopy.name))
                {
                    FBMessageBox msg = new FBMessageBox(appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["alertEnterName"].ToString()); //"Please enter a name for your custom schedule before saving.",
                    msg.Owner = _ParentWindow;
                    msg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    _ParentWindow.GridShade.Visibility = Visibility.Visible;
                    msg.ShowDialog();
                    _ParentWindow.GridShade.Visibility = Visibility.Collapsed;
                    return;
                }

                //Check that at least one day is selected
                 bool anyDaySelected = _editableCopy.DaysOfWeek.Any(d => d == true);
                if (!anyDaySelected)
                {
                    FBMessageBox msg = new FBMessageBox(appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["alertSelectDay"].ToString());
                    msg.Owner = _ParentWindow;
                    msg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    _ParentWindow.GridShade.Visibility = Visibility.Visible;
                    msg.ShowDialog();
                    _ParentWindow.GridShade.Visibility = Visibility.Collapsed;
                    return;
                }

                string timeStart24 = ActiveTimeSlider.StartTime24;
                string timeEnd24 = ActiveTimeSlider.EndTime24;

                bool bAddingNew = false;
                // 2. If new schedule (no ID), assign one
                if (string.IsNullOrEmpty(_instance.id))
                {
                    _instance.id = Guid.NewGuid().ToString();
                    bAddingNew = true;
                }

                // 3. Copy all editable fields back to the original instance
                _instance.name = _editableCopy.name;
                _instance.isOn = _editableCopy.isOn;

                _instance.StartTimeValue = timeStart24;
                _instance.EndTimeValue = timeEnd24;

                _instance.SelectedFocusModeId = _editableCopy.SelectedFocusModeId;
                _instance.BlockLevel = _editableCopy.BlockLevel;

                _instance.bAIBlockingEnabled = _editableCopy.bAIBlockingEnabled;
                _instance.bMicroBreaksEnabled = _editableCopy.bMicroBreaksEnabled;
                _instance.Emoji = _editableCopy.Emoji;

                for (int i = 0; i < 7; i++)
                    _instance.DaysOfWeek[i] = _editableCopy.DaysOfWeek[i];

                // 4. Save or update the list
                if (bAddingNew)
                {
                    appSettings.arrCustomBlockingSchedules.Add(_instance);//Later we will add a Focus mode to this and upload it.
                   // BlockingScheduleManager.AddBlockingSchedule(_instance);
                }

                _ParentWindow.PageOnboardingViewController.Navigate(OnboardingViewController.ONBOARDINGSTATE._ALL_SET_);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnSaveChanges_Click ex - " + ex.Message);
            }
        }

        // Fired when the user checks one of the Flexible/Moderate/Strict radio buttons. The Tag
        // carries the FOCUSMODESTRICTNESS value so this doesn't depend on the binding having
        // already pushed the new value into _editableCopy.BlockLevel by the time this runs.
        private void StrictnessRadio_Checked(object sender, RoutedEventArgs e)
        {
            string tag = (sender as RadioButton)?.Tag as string;
            FOCUSMODESTRICTNESS level = FOCUSMODESTRICTNESS.CHILL_MODE;
            if (tag == "STRICT")
                level = FOCUSMODESTRICTNESS.STRICT;
            else if (tag == "IRON_FOCUS")
                level = FOCUSMODESTRICTNESS.IRON_FOCUS;

            UpdateStrictnessDescription(level);
        }

        private void UpdateStrictnessDescription(FOCUSMODESTRICTNESS level)
        {
            string titleKey, titleFallback, descriptionKey;
            switch (level)
            {
                case FOCUSMODESTRICTNESS.STRICT:
                    titleKey = "titleStrictnessModerate";
                    titleFallback = "Moderate";
                    descriptionKey = "descriptionStrictMode";
                    break;
                case FOCUSMODESTRICTNESS.IRON_FOCUS:
                    titleKey = "titleStrictnessStrict";
                    titleFallback = "Strict";
                    descriptionKey = "descriptionSuperStrictMode";
                    break;
                default:
                    titleKey = "titleStrictnessFlexible";
                    titleFallback = "Flexible";
                    descriptionKey = "descriptionNormalMode";
                    break;
            }

            string title = GetConfigStringOrDefault("blockingScheduleEditorVcInfo", titleKey, titleFallback);
            string description = GetConfigStringOrDefault("simplePomodoroVcInfo", descriptionKey, "");
            StrictnessDescription.Text = title + " — " + description;
        }

        private void BtnWeekdays_Click(object sender, RoutedEventArgs e)
        {
            SetDays(true, true, true, true, true, false, false);
        }

        private void BtnWeekends_Click(object sender, RoutedEventArgs e)
        {
            SetDays(false, false, false, false, false, true, true);
        }

        private void BtnEveryday_Click(object sender, RoutedEventArgs e)
        {
            SetDays(true, true, true, true, true, true, true);
        }

        private void SetDays(bool mon, bool tue, bool wed, bool thu, bool fri, bool sat, bool sun)
        {
            _editableCopy.Monday = mon;
            _editableCopy.Tuesday = tue;
            _editableCopy.Wednesday = wed;
            _editableCopy.Thursday = thu;
            _editableCopy.Friday = fri;
            _editableCopy.Saturday = sat;
            _editableCopy.Sunday = sun;

            UpdateWeekdayPresetHighlight();
        }

        // Fired whenever an individual day circle is toggled, so the Weekdays/Weekends/Everyday
        // pills re-highlight (or clear) to reflect the resulting day pattern.
        private void DayToggle_Changed(object sender, RoutedEventArgs e)
        {
            UpdateWeekdayPresetHighlight();
        }

        private void UpdateWeekdayPresetHighlight()
        {
            if (BtnWeekdays == null || _editableCopy?.DaysOfWeek == null)
                return;

            bool[] d = _editableCopy.DaysOfWeek;
            BtnWeekdays.IsChecked = d[0] && d[1] && d[2] && d[3] && d[4] && !d[5] && !d[6];
            BtnWeekends.IsChecked = !d[0] && !d[1] && !d[2] && !d[3] && !d[4] && d[5] && d[6];
            BtnEveryday.IsChecked = d.All(x => x);
        }

        private void BtnEmojiPicker_Click(object sender, RoutedEventArgs e)
        {
            BtnEmojiPicker.ContextMenu.PlacementTarget = BtnEmojiPicker;
            BtnEmojiPicker.ContextMenu.IsOpen = true;
        }

        private void EmojiMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem item = sender as MenuItem;
            if (item == null)
                return;

            // Every real emoji item carries its character in Tag (Header holds the emoji:TextBlock
            // used for display instead, since Emoji.Wpf renders it in color). "None" has no Tag.
            _editableCopy.Emoji = item.Tag as string;

            UpdateEmojiPickerText();
        }

        private void UpdateEmojiPickerText()
        {
            bool hasEmoji = !string.IsNullOrEmpty(_editableCopy.Emoji);
            EmojiPickerText.Text = hasEmoji ? _editableCopy.Emoji : "🙂";
            EmojiPickerText.Opacity = hasEmoji ? 1.0 : 0.5;
        }

        private string FirstLetter(string s)
        {
            return string.IsNullOrEmpty(s) ? s : s.Substring(0, 1).ToUpperInvariant();
        }

        // Safe config lookup that falls back to English text instead of throwing when a key isn't
        // present - some of the strings this screen uses aren't in the shared assets config yet.
        private string GetConfigStringOrDefault(string section, string key, string fallback)
        {
            try
            {
                var token = appSettings.ConfigFileStrings?[section]?[key];
                return token != null ? token.ToString() : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        // Scroll the newly-revealed More Options content into view. Deferred to Background priority
        // so it runs after the Expander's layout pass has actually grown ContentScrollViewer's extent -
        // calling ScrollToEnd() synchronously here would still see the pre-expand (shorter) extent.
        private void MoreOptionsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => ContentScrollViewer.ScrollToEnd()), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                appSettings.CapturePostHog("create-first-blocking-schedule-screen-shown");
            }
        }
    }
}
