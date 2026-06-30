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
            // Parse and reformat as we know server side it 24 hour format
            DateTime timeStart = DateTime.ParseExact(_editableCopy.StartTimeValue, "HH:mm:ss", CultureInfo.InvariantCulture);
            string time12 = timeStart.ToString("hh:mm:ss tt"); // "12:00:00 PM" as we show AM/PM in UI

            // Split into hour/min/sec if needed
            string[] parts = time12.Split(':');
            string hour = parts[0];               // "12"
            string minute = parts[1];             // "00"
            string second = parts[2].Split(' ')[0]; // "00"
            string ampm = timeStart.ToString("tt");    // "PM"

            StartTimePicker.NUDHTextBox.Text = hour;
            StartTimePicker.NUDMTextBox.Text = minute;
            StartTimePicker.NUDAMPTextBox.Text = ampm;

            DateTime timeEnd = DateTime.ParseExact(_editableCopy.EndTimeValue, "HH:mm:ss", null);
            string timeEnd12 = timeEnd.ToString("hh:mm:ss tt"); // "12:00:00 PM" as we show AM/PM in UI

            // Split into hour/min/sec if needed
            string[] partsEnd = timeEnd12.Split(':');
            string hourEnd = partsEnd[0];               // "12"
            string minuteEnd = partsEnd[1];             // "00"
            string secondEnd = partsEnd[2].Split(' ')[0]; // "00"
            string ampmEnd = timeEnd.ToString("tt");    // "PM"

            EndTimePicker.NUDHTextBox.Text = hourEnd;
            EndTimePicker.NUDMTextBox.Text = minuteEnd;
            EndTimePicker.NUDAMPTextBox.Text = ampmEnd;

            // Bind to UI
            DataContext = _editableCopy;

            //Initialize Texts

            try
            {
                BlockName.Text = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["titleScheduleName"].ToString();
                Title.Text = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["title"].ToString();
                 
                BlockingScheduleName.Text = "TO add what comesfrom array or new";
                From.Text = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["titleFromTime"].ToString();
                Till.Text = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["titleTillTime"].ToString();

                BlockList.Text = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["titleFocusMode"].ToString();
                EditBlockListText.Text = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["buttonEditBlockList"].ToString();

                Mon.Content = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["dayMon"].ToString();
                Tue.Content = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["dayTue"].ToString();
                Wed.Content = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["dayWed"].ToString();
                Thu.Content = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["dayThu"].ToString();
                Fri.Content = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["dayFri"].ToString();
                Sat.Content = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["daySat"].ToString();
                Sun.Content = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["daySun"].ToString();

                StrictnessLevel.Text = appSettings.ConfigFileStrings["focusInfo"]["startingFocusModeInfo"]["titleStrictnessLevel"].ToString();
                //Change strictness level text based on slider value
                //SliderStrictnessLevel.Value = 50;  // assuming x:Name="mySlider"

                Chill.Text = appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["titleNormalMode"].ToString();
                WaitForIt.Text = appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["titleStrictMode"].ToString();
                IronFocus.Text = appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["titleSuperStrictMode"].ToString();

                btnCancelText.Text = appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString(); //"Cancel";
                btnDeleteText.Text = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["buttonDelete"].ToString();
                btnSaveChangesText.Text = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["buttonSave"].ToString();

              

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

                // Validate the time inputs BEFORE mutating _instance. Both From and
                // Till must parse, otherwise show a clear message and bail out without
                // leaving the schedule half-saved (e.g. id assigned).
                if (!StartTimePicker.TryGetTime24(out string timeStart24)
                    || !EndTimePicker.TryGetTime24(out string timeEnd24))
                {
                    FBMessageBox msg = new FBMessageBox(appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["alertInvalidTime"].ToString());
                    msg.Owner = _ParentWindow;
                    msg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    _ParentWindow.GridShade.Visibility = Visibility.Visible;
                    msg.ShowDialog();
                    _ParentWindow.GridShade.Visibility = Visibility.Collapsed;
                    return;
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

        private void SliderStrictnessLevel_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        { 
            //Change strictness level text based on slider value
            double newValue = e.NewValue;

            // Use the value as needed
            Debug.WriteLine($"Slider value changed to: {newValue}");

            SliderStrictnessLevel.ToolTip = "";
            //"descriptionNormalMode": "Stop focusing anytime, no questions asked.",
            //"toolTipNormalMode": "Best for gentle focus days. No barriers to pausing or stopping a session.",
            //"titleStrictMode": "Wait for it…",
            //"descriptionStrictMode": "15-second delay before you can stop",
            //"toolTipStrictMode": "Adds a 15s countdown before you can pause/stop. Great for catching impulsive decisions.",
            //"titleSuperStrictMode": "Iron Focus",
            //"descriptionSuperStrictMode": "Once you start, you can't stop",
            //"toolTipSuperStrictMode": "Once you start, there’s no backing out. Best for deep, distraction-free sessions."

            if (SliderStrictnessLevel.Value == 0)
            {
                StrictnessLevel.Text = appSettings.ConfigFileStrings["focusInfo"]["startingFocusModeInfo"]["titleStrictnessLevel"].ToString() +
                                        " " + appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["titleNormalMode"].ToString() +
                                        " (" + appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["descriptionNormalMode"].ToString() + ")";
                StrictnessLevelInfo.ToolTip = appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["descriptionNormalMode"].ToString();
            }
            else if (SliderStrictnessLevel.Value == 50)
            {
                StrictnessLevel.Text = appSettings.ConfigFileStrings["focusInfo"]["startingFocusModeInfo"]["titleStrictnessLevel"].ToString() +
                                        " " + appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["titleStrictMode"].ToString() +
                                        " (" + appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["descriptionStrictMode"].ToString() + ")";
                StrictnessLevelInfo.ToolTip = appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["toolTipStrictMode"].ToString();
            }
            else if (SliderStrictnessLevel.Value == 100)
            {
                StrictnessLevel.Text = appSettings.ConfigFileStrings["focusInfo"]["startingFocusModeInfo"]["titleStrictnessLevel"].ToString() +
                                        " " + appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["titleSuperStrictMode"].ToString() +
                                        " (" + appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["descriptionSuperStrictMode"].ToString() + ")";
                StrictnessLevelInfo.ToolTip = appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["toolTipSuperStrictMode"].ToString();

                SliderStrictnessLevel.ToolTip = appSettings.ConfigFileStrings["simplePomodoroVcInfo"]["toolTipSuperStrictMode"].ToString();
               
                var slider = (Slider)sender;

                // Create a tooltip only once
               var _strictTooltip = new ToolTip
                {
                    Content = SliderStrictnessLevel.ToolTip,
                    PlacementTarget = slider,
                    Placement = PlacementMode.Relative,
                    StaysOpen = false
                };

                
                if (_strictTooltip != null)
                {

                    if (slider.Value == slider.Maximum)
                    {
                        // Position near the right edge (end of slider)
                        double xOffset = slider.ActualWidth - 30; // tweak 30 to align visually
                        double yOffset = -60; // slightly above the slider

                        _strictTooltip.HorizontalOffset = xOffset;
                        _strictTooltip.VerticalOffset = yOffset;

                        _strictTooltip.PlacementTarget = slider;
                        _strictTooltip.IsOpen = true;
                        // Auto close after 2 seconds
                        Task.Delay(5000).ContinueWith(_ =>
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                _strictTooltip.IsOpen = false;
                            });
                        });
                    }
                    else
                    {
                        _strictTooltip.IsOpen = false;
                    }
                }

            }

        }

    }
}
