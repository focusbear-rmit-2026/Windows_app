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
                Heading.Text = appSettings.ConfigFileStrings["createFirstBlockingScheduleVcInfo"]["title"].ToString();
                Bubble1.Text = appSettings.ConfigFileStrings["createFirstBlockingScheduleVcInfo"]["bubbleText"].ToString(); //"For example, block distracting websites during work hours";
                btnAddBlockText.Text = appSettings.ConfigFileStrings["createFirstBlockingScheduleVcInfo"]["buttonAddBlock"].ToString(); //"Add Block";

                BlockName.Text = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["titleScheduleName"].ToString();
                Title.Text = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["title"].ToString();
                 
                BlockingScheduleName.Text = "";
                From.Text = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["titleFromTime"].ToString();
                Till.Text = appSettings.ConfigFileStrings["blockingScheduleEditorVcInfo"]["titleTillTime"].ToString();

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

                string sStartHours = StartTimePicker.NUDHTextBox.Text;
                string sStartMinutes = StartTimePicker.NUDMTextBox.Text;
                string sStartAMPM = StartTimePicker.NUDAMPTextBox.Text;

                string combinedStartTime = $"{sStartHours}:{sStartMinutes}:00 {sStartAMPM}";
                DateTime time = DateTime.ParseExact(combinedStartTime, "h:m:s tt", CultureInfo.InvariantCulture,DateTimeStyles.None);
                string timeStart24 = time.ToString("HH:mm:ss"); // e.g. "00:15:00"

                string sEndHours = EndTimePicker.NUDHTextBox.Text;
                string sEndMinutes = EndTimePicker.NUDMTextBox.Text;
                string sEndAMPM = EndTimePicker.NUDAMPTextBox.Text;

                string combinedEndTime = $"{sEndHours}:{sEndMinutes}:00 {sEndAMPM}";
                DateTime timeEnd = DateTime.ParseExact(combinedEndTime, "h:m:s tt", CultureInfo.InvariantCulture, DateTimeStyles.None);
                string timeEnd24 = timeEnd.ToString("HH:mm:ss"); // e.g. "00:15:00"

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
        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                appSettings.CapturePostHog("create-first-blocking-schedule-screen-shown");
            }
        }
    }
}
