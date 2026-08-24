using Focus_Bear.Models;
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

namespace Focus_Bear.Preferences
{
    public class EnumToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return 0;

            return (int)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Enum.ToObject(typeof(FOCUSMODESTRICTNESS), (int)(double)value);
        }
    }
    // Binds a RadioButton's IsChecked to a specific enum value, given via ConverterParameter.
    // Used to present FOCUSMODESTRICTNESS as three mutually-exclusive radio options.
    public class EnumBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            return value.ToString() == parameter.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked && parameter != null)
                return Enum.Parse(typeof(FOCUSMODESTRICTNESS), parameter.ToString());

            return Binding.DoNothing;
        }
    }
    // Inverts a boolean for two-way bindings. Used for the "Skip microbreaks" checkbox, which is bound to
    // bMicroBreaksEnabled ("micro breaks enabled"): checking Skip must set the field to false.
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b) ? !b : value;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b) ? !b : value;
    }
    public class DaysOfWeekIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool[] days && int.TryParse(parameter?.ToString(), out int index))
            {
                return (index >= 0 && index < days.Length) ? days[index] : false;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked &&
                int.TryParse(parameter?.ToString(), out int index))
            {
                return new Tuple<int, bool>(index, isChecked); // return position + value
            }

            return Binding.DoNothing;
        }
    }

    public class BindingProxy : Freezable
    {
        public object Data
        {
            get { return GetValue(DataProperty); }
            set { SetValue(DataProperty, value); }
        }

        public static readonly DependencyProperty DataProperty =
            DependencyProperty.Register("Data", typeof(object), typeof(BindingProxy), new UIPropertyMetadata(null));

        protected override Freezable CreateInstanceCore()
        {
            return new BindingProxy();
        }
    }
    public partial class BlockingScheduleDetail : System.Windows.Controls.UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;

        FocusBearMainWindow _ParentWindow = null;

        private CustomBlockingSchedule _instance;
        private CustomBlockingSchedule _editableCopy;

        public BlockingScheduleDetail(FocusBearMainWindow ParentWindow, CustomBlockingSchedule instance)
        {
            InitializeComponent();

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }

            _ParentWindow = ParentWindow;

            if (instance == null)
            {
                btnDelete.Visibility = Visibility.Collapsed;

                _editableCopy = new CustomBlockingSchedule
                {
                    id = "", // will be assigned on save
                    name = "",
                    isOn = false,
                    DaysOfWeek = new bool[7],

                    //make sure toa dd in 24 hours format which server accepts and convert to am/pm per UI
                    StartTimeValue = DateTime.Now.ToString("HH:mm:ss"),
                    EndTimeValue = DateTime.Now.AddHours(1).ToString("HH:mm:ss"),

                    SelectedFocusModeId = null,
                    BlockLevel = FOCUSMODESTRICTNESS.CHILL_MODE,

                    bAIBlockingEnabled = false,
                    bMicroBreaksEnabled = false,
                   
                };
                _instance = _editableCopy;
            }
            else
            {
                btnDelete.Visibility = Visibility.Visible;

                _instance = instance;
                // Clone the instance for editing
                _editableCopy = new CustomBlockingSchedule
                {
                    id = instance.id,
                    name = instance.name,
                    DaysOfWeek = (bool[])instance.DaysOfWeek.Clone(),

                    StartTimeValue = instance.StartTimeValue,
                    EndTimeValue = instance.EndTimeValue,

                    SelectedFocusModeId = instance.SelectedFocusModeId,
                    BlockLevel = instance.BlockLevel,

                    bMicroBreaksEnabled = instance.bMicroBreaksEnabled,
                    bAIBlockingEnabled = instance.bAIBlockingEnabled

                };
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

            //Initialize Texts

            try
            {
                BlockName.Text = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["titleScheduleName"].ToString();
                Title.Text = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["title"].ToString();
                 
                BlockingScheduleName.Text = "TO add what comesfrom array or new";

                BlockList.Text = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["titleFocusMode"].ToString();
                EditBlockListText.Text = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["buttonEditBlockList"].ToString();

                // Single-letter day circles (mac shows "M T W T F S S", not 3-letter abbreviations).
                // Reuses the existing localized dayMon/dayTue/... strings rather than new config keys -
                // takes the first letter of whatever the current locale already has configured there.
                Mon.Content = FirstLetter(appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["dayMon"].ToString());
                Tue.Content = FirstLetter(appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["dayTue"].ToString());
                Wed.Content = FirstLetter(appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["dayWed"].ToString());
                Thu.Content = FirstLetter(appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["dayThu"].ToString());
                Fri.Content = FirstLetter(appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["dayFri"].ToString());
                Sat.Content = FirstLetter(appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["daySat"].ToString());
                Sun.Content = FirstLetter(appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["daySun"].ToString());

                // These keys aren't in the shared assets config yet (server-hosted config.json checked
                // 2026-08-24 has no titleWeekdays/titleActiveTime/titleStrictness* under
                // blockingScheduleEditorVcInfo) - fall back to English rather than let a missing key
                // throw and abort the rest of this try block (incl. AddFocusModesToComboBox() below).
                BtnWeekdays.Content = GetConfigStringOrDefault("blockingScheduleEditorVcInfo", "titleWeekdays", "Weekdays");
                BtnWeekends.Content = GetConfigStringOrDefault("blockingScheduleEditorVcInfo", "titleWeekends", "Weekends");
                BtnEveryday.Content = GetConfigStringOrDefault("blockingScheduleEditorVcInfo", "titleEveryday", "Everyday");

                ActiveTimeTitle.Text = GetConfigStringOrDefault("blockingScheduleEditorVcInfo", "titleActiveTime", "Active time");

                StrictnessLevel.Text = appSettings.ConfigFileStrings["focusInfo"]["startingFocusModeInfo"]["titleStrictnessLevel"].ToString();

                RadioFlexible.Content = GetConfigStringOrDefault("blockingScheduleEditorVcInfo", "titleStrictnessFlexible", "Flexible");
                RadioModerate.Content = GetConfigStringOrDefault("blockingScheduleEditorVcInfo", "titleStrictnessModerate", "Moderate");
                RadioStrict.Content = GetConfigStringOrDefault("blockingScheduleEditorVcInfo", "titleStrictnessStrict", "Strict");

                btnCancelText.Text = appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString(); //"Cancel";
                btnDeleteText.Text = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["buttonDelete"].ToString();
                btnSaveChangesText.Text = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["buttonSave"].ToString();

                MoreOptionsHeaderText.Text = appSettings.ConfigFileStrings["pomodoroInfo"]["moreOptionsText"].ToString();

                SkipMicroBreaks.Content = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["buttonSkipMicrobreaks"].ToString();
                UseAIToDecide.Content = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["buttonEnableAi"].ToString();
                MicroBreaksInfo.ToolTip = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["buttonSkipMicrobreaks"].ToString(); //todo
                UseAIToDecideInfo.ToolTip = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["buttonEnableAi"].ToString(); //todo

                AddFocusModesToComboBox();

                if(_instance.name.IsNullOrEmpty())
                {
                    BlockingScheduleName.Focus();
                }    
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AddApp load text ex + " + ex.Message);
            }

            // When editing an existing schedule, listen for server refreshes (the future Pusher
            // blocking-schedule-updated event). If this schedule was deleted on another device while
            // it's open here, drop back to the now-refreshed list instead of showing a ghost. #1114
            if (instance != null)
            {
                BlockingScheduleManager.SchedulesRefreshed += OnSchedulesRefreshed;
                Unloaded += (s, e) => BlockingScheduleManager.SchedulesRefreshed -= OnSchedulesRefreshed;
            }

            // Refresh the block-list combo whenever focus modes are re-fetched, so a block list added
            // on another device becomes selectable here without restarting (covers both new + edit). #1114
            BlockingScheduleManager.FocusModesRefreshed += OnFocusModesRefreshed;
            Unloaded += (s, e) => BlockingScheduleManager.FocusModesRefreshed -= OnFocusModesRefreshed;
        }

        // Fired (on the UI thread) after focus modes were re-fetched from the server. Rebuild the
        // block-list combo, preserving whatever the user currently has selected. #1114
        private void OnFocusModesRefreshed()
        {
            try
            {
                object keepSelected = ComboBoxFocusModes.SelectedValue;
                AddFocusModesToComboBox();
                if (keepSelected != null)
                    ComboBoxFocusModes.SelectedValue = keepSelected;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnFocusModesRefreshed ex - " + ex.Message);
            }
        }

        // Fired (on the UI thread) after a server refresh changed the schedule collection. If the
        // schedule being edited here no longer exists, it was deleted elsewhere — return to the list. #1114
        private void OnSchedulesRefreshed()
        {
            try
            {
                if (_instance == null || string.IsNullOrEmpty(_instance.id))
                    return;   // brand-new unsaved schedule — nothing to reconcile

                if (appSettings.arrCustomBlockingSchedules.Any(s => s.id == _instance.id))
                    return;   // still exists — leave the editor as-is

                LogWriter.LogWrite("BlockingScheduleDetail - schedule deleted elsewhere, returning to list");

                // Navigate back to the (refreshed) list the same way Cancel/Delete do.
                ButtonAutomationPeer peer = new ButtonAutomationPeer(_ParentWindow.PageBlocks.btnBlockingSchedule);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("OnSchedulesRefreshed ex - " + ex.Message);
            }
        }
        public void AddFocusModesToComboBox()
        {
            //Load the combox box with focus mode name, trigged by event when new mode added
            ComboBoxFocusModes.Items.Clear();

            // Filter out unwanted focus modes
            var focusModes = appSettings.arrFocusModes
                .Where(m => !(m.isMeetings || m.isLockedFocus))
                .ToList();

            // Set ItemsSource to the collection directly
            ComboBoxFocusModes.ItemsSource = focusModes;
            // ensure initial selection reflects the model id (string)
            ComboBoxFocusModes.SelectedValue = _instance.SelectedFocusModeId;
        }
        private void btnSaveChanges_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //        "alertEnterName": "Please enter a name for your custom schedule before saving.",
                //"alertSelectDay": "Please select at least one day for this schedule.",
                //"alertScheduleActive": "Schedule is Active",
                //"descriptionScheduleActive": "This schedule is currently running. Are you sure you want to delete it? Blocking will stop immediately.",
                //"buttonAddNewBlock": "Add new block"

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

                if (_editableCopy.SelectedFocusModeId == null || ComboBoxFocusModes.SelectedIndex == -1)
                {
                    
                    FBMessageBox msg = new FBMessageBox(appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["alertSelectBlockList"].ToString(), "Missing block list");
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

                //Check overlapping schedules
                if (string.IsNullOrEmpty(_editableCopy.id)) //new schedule
                {
                    if (BlockingScheduleManager.DoesNewScheduleOverlap(_editableCopy.DaysOfWeek, _editableCopy.StartTimeValue, _editableCopy.EndTimeValue))
                    {
                        //"This blocking schedule's time overlaps with an existing one. Do you want to save it anyway?"
                        FBMessageBox msg = new FBMessageBox(appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["subTitleScheduleOverlapAlert"].ToString(),
                            appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["titleScheduleOverlapAlert"].ToString(), 
                            MessageBoxButton.YesNo,
                            appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["buttonSaveBlockAnyway"].ToString(),
                            appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString());
                        msg.Owner = _ParentWindow;
                        msg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                        _ParentWindow.GridShade.Visibility = Visibility.Visible;
                        msg.ShowDialog();
                        _ParentWindow.GridShade.Visibility = Visibility.Collapsed;
                        if (msg.btnClicked == MessageBoxResult.No)
                        {
                            return;
                        }
                    }
                }
                else
                {
                    if (BlockingScheduleManager.HasOverlappingSchedules(_instance.id))
                    {
                        //"titleScheduleOverlapAlert": "Time Overlap Detected",
                        //"subTitleScheduleOverlapAlert": "This blocking schedule's time overlaps with an existing one. Do you want to save it anyway?",
                        //"buttonSaveBlockAnyway": "Save Anyway",
                        //"This blocking schedule's time overlaps with an existing one. Do you want to save it anyway?"
                        FBMessageBox msg = new FBMessageBox(appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["subTitleScheduleOverlapAlert"].ToString(),
                            appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["titleScheduleOverlapAlert"].ToString(),
                            MessageBoxButton.YesNo,
                            appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["buttonSaveBlockAnyway"].ToString(),
                            appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString());

                        msg.Owner = _ParentWindow;
                        msg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                        _ParentWindow.GridShade.Visibility = Visibility.Visible;
                        msg.ShowDialog();
                        _ParentWindow.GridShade.Visibility = Visibility.Collapsed;
                        if (msg.btnClicked == MessageBoxResult.No)
                        {
                            return;
                        }
                    }
                }

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
                

                for (int i = 0; i < 7; i++)
                    _instance.DaysOfWeek[i] = _editableCopy.DaysOfWeek[i];

                // 4. Save or update the list 
                if (bAddingNew)
                {
                    appSettings.arrCustomBlockingSchedules.Add(_instance);
                    BlockingScheduleManager.AddBlockingSchedule(_instance);
                }
                else
                {
                    BlockingScheduleManager.AddBlockingSchedule(_instance, true); //send true when updating a blocking schedule
                }
                
                ButtonAutomationPeer peer = new ButtonAutomationPeer(_ParentWindow.PageBlocks.btnBlockingSchedule);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnSaveChanges_Click ex - " + ex.Message);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            // Discard edits and reset editable copy
            if(_instance.id == null)
            {
                //just close and return
                return;
            }

            _editableCopy.id = _instance.id;
            _editableCopy.name = _instance.name;

            _editableCopy.Monday = _instance.Monday;
            _editableCopy.Tuesday = _instance.Tuesday;
            _editableCopy.Wednesday = _instance.Wednesday;
            _editableCopy.Thursday = _instance.Thursday;
            _editableCopy.Friday = _instance.Friday;
            _editableCopy.Saturday = _instance.Saturday;
            _editableCopy.Sunday = _instance.Sunday;

            _editableCopy.StartTimeValue = _instance.StartTimeValue;
            _editableCopy.EndTimeValue = _instance.EndTimeValue;

            _editableCopy.BlockLevel = _instance.BlockLevel;

            _editableCopy.SelectedFocusModeId = _instance.SelectedFocusModeId;

            _editableCopy.DaysOfWeek = (bool[])_instance.DaysOfWeek.Clone();

            _editableCopy.bAIBlockingEnabled = _instance.bAIBlockingEnabled;
            _editableCopy.bMicroBreaksEnabled = _instance.bMicroBreaksEnabled;

            ButtonAutomationPeer peer = new ButtonAutomationPeer(_ParentWindow.PageBlocks.btnBlockingSchedule);
            IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            invokeProv.Invoke();

        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Blocking Schedule  btnDelete_Click");
            try
            {
                if (_instance.isOn)
                {
                    //"alertScheduleActive": "Schedule is Active",
                    //"descriptionScheduleActive": "This schedule is currently running. Are you sure you want to delete it? Blocking will stop immediately.",
            
                    FBMessageBox msg = new FBMessageBox(appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["descriptionScheduleActive"].ToString(), appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["alertScheduleActive"].ToString(), MessageBoxButton.YesNo);
                    msg.Owner = _ParentWindow;
                    msg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    _ParentWindow.GridShade.Visibility = Visibility.Visible;
                    msg.ShowDialog();
                    _ParentWindow.GridShade.Visibility = Visibility.Collapsed;
                    if (msg.btnClicked == MessageBoxResult.No)
                    {
                        return;
                    }
                }

                appSettings.arrCustomBlockingSchedules.Remove(_instance);
                BlockingScheduleManager.DeleteBlockingSchedule(_instance.id);
                LogWriter.LogWrite("Blocking Schedule  Deleted with name - " + _instance.name);

                ButtonAutomationPeer peer = new ButtonAutomationPeer(_ParentWindow.PageBlocks.btnBlockingSchedule);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DeleteBlockingSchedule btnDelete_Click ex - " + ex.Message);
            }
           
        }
        private void btnEditBlockList_Click(object sender, RoutedEventArgs e)
        {
            _ParentWindow.ShowFocusMode();
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

            // titleFallback matches the same fallback text used for the radio button labels
            // themselves - the config key isn't in the shared assets config yet, so this is what
            // actually renders today. (Previously fell back to level.ToString(), e.g. "CHILL_MODE".)
            string title = GetConfigStringOrDefault("blockingScheduleEditorVcInfo", titleKey, titleFallback);
            string description = GetConfigStringOrDefault("simplePomodoroVcInfo", descriptionKey, "");
            StrictnessDescription.Text = title + " — " + description;
        }

        private string FirstLetter(string s)
        {
            return string.IsNullOrEmpty(s) ? s : s.Substring(0, 1).ToUpperInvariant();
        }

        // Safe config lookup that falls back to English text instead of throwing when a key isn't
        // present - some of the strings this screen uses (Flexible/Moderate/Strict, Active time,
        // Weekdays/Weekends/Everyday) aren't in the shared assets config yet, and this is also called
        // from a couple of places that aren't wrapped in the constructor's try/catch.
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

    }
}
