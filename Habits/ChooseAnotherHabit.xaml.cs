using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Focus_Bear.Habits
{
    /// <summary>
    /// Interaction logic for ChooseAnotherHabit.xaml
    /// </summary>
    public partial class ChooseAnotherHabit : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        public ObservableCollection<HabitListSelectable> FilteredList = new ObservableCollection<HabitListSelectable>();
        public string sIDHabitToDo = "";
        App app = (App)Application.Current;
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public ChooseAnotherHabit()
        {
        }

        public ChooseAnotherHabit(HabitWindow ParentWindow)
        {
            InitializeComponent();
            this.Owner = ParentWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }

            try
            {
                this.Title = appSettings.ConfigFileStrings["activityInfo"]["buttonChooseHabitText"].ToString();
                Heading.Text = appSettings.ConfigFileStrings["activityInfo"]["titleChooseHabitText"].ToString();
                btnStartTXT.Text = appSettings.ConfigFileStrings["activityInfo"]["buttonChooseHabitText"].ToString();
                CancelTXT.Text = appSettings.ConfigFileStrings["common"]["buttonCancelText"].ToString();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ChooseHabits initialization" + ex.Message);
            }

            InitializeHabitsList();

            if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
            {
                ListHabits.ItemsSource = FilteredList;
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
            {
                ListHabits.ItemsSource = FilteredList;
            }
        }

        public void InitializeHabitsList()
        {
            if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
            {
                int nSerialNo = 1;
                for (int i = appSettings.nHabitIndex + 1; i < appSettings.arrAllMorningHabits.Count; i++)
                {
                    string sActivityName = appSettings.arrAllMorningHabits[i].name;
                    string sID = appSettings.arrAllMorningHabits[i].id;
                    string sNo = string.Format("{0}.", (nSerialNo).ToString());
                    double nDurationSeconds = appSettings.arrAllMorningHabits[i].durationSeconds;
                    if (!appSettings.arrAllMorningHabits[i].useTimer)
                        nDurationSeconds = appSettings.nTimerValForNonTimerActivities;
                    var ActivityItem = new HabitListSelectable { id = sID, SNumber = sNo, ActivityName = sActivityName, nDuration = nDurationSeconds };
                    FilteredList.Add(ActivityItem);
                    nSerialNo++;
                }
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
            {
                int nSerialNo = 1;
                for (int i = appSettings.nHabitIndex + 1; i < appSettings.arrAllEveningHabits.Count; i++)
                {
                    bool bAddItem = false;
                    //We will also need to check the cutoff time and remove habits past cutoff time
                    if (appSettings.DidCutOffTimePassed())
                    {
                        if (appSettings.arrAllEveningHabits[i].bHighPriorityActivity)
                        {
                            if (appSettings.arrAllEveningHabits[i].sCutOffTime != "")
                            {
                                DateTime now = DateTime.Now;
                                TimeSpan tsTime = TimeSpan.Parse(appSettings.arrAllEveningHabits[i].sCutOffTime);
                                DateTime myAvtivityCutoffTime = new DateTime(now.Year, now.Month, now.Day, tsTime.Hours, tsTime.Minutes, 00);
                                if (now > myAvtivityCutoffTime)
                                {
                                    appSettings.arrAllEveningHabits.RemoveAt(i);
                                    i--;
                                }
                                else
                                {
                                    bAddItem = true;
                                }
                            }
                            else
                            {
                                bAddItem = true;
                            }
                        }
                        else
                        {
                            //remove from list of evening habits
                            appSettings.arrAllEveningHabits.RemoveAt(i);
                            i--;
                        }
                    }
                    else
                    {
                        bAddItem = true;
                    }
                    if(bAddItem)
                    {
                        string sActivityName = appSettings.arrAllEveningHabits[i].name;
                        string sID = appSettings.arrAllEveningHabits[i].id;
                        string sNo = string.Format("{0}.", (nSerialNo).ToString());
                        double nDurationSeconds = appSettings.arrAllEveningHabits[i].durationSeconds;
                        if (!appSettings.arrAllEveningHabits[i].useTimer)
                            nDurationSeconds = appSettings.nTimerValForNonTimerActivities;

                        var ActivityItem = new HabitListSelectable { id = sID, SNumber = sNo, ActivityName = sActivityName, nDuration = nDurationSeconds };
                        FilteredList.Add(ActivityItem);
                        nSerialNo++;
                    }
                    
                }
            }
        }
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (ListHabits.SelectedItem != null)
            {
                //
                DialogResult = true;
                int selectedIndex = ListHabits.SelectedIndex;
                HabitListSelectable selectedItem = ListHabits.SelectedItem as HabitListSelectable;
                if (selectedItem != null)  // Ensure an item is selected
                {
                    if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                    {
                        for (int i = 0; i < appSettings.arrAllMorningHabits.Count; i++)
                        {
                            if(selectedItem.id == appSettings.arrAllMorningHabits[i].id)
                            {
                                habit itemToMove = appSettings.arrAllMorningHabits[i];
                                // Remove the item from arrAllMorningHabits
                                appSettings.arrAllMorningHabits.Remove(itemToMove);
                                // Insert it at the front (index 0) of arrAllMorningHabits
                                appSettings.arrAllMorningHabits.Insert(appSettings.nHabitIndex + 1, itemToMove);
                                break;
                            }
                        }
                        
                       
                    }
                    else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                    {
                        for (int i = 0; i < appSettings.arrAllEveningHabits.Count; i++)
                        {
                            if (selectedItem.id == appSettings.arrAllEveningHabits[i].id)
                            {
                                habit itemToMove = appSettings.arrAllEveningHabits[i];
                                // Remove the item from arrAllMorningHabits
                                appSettings.arrAllEveningHabits.Remove(itemToMove);
                                // Insert it at the front (index 0) of arrAllMorningHabits
                                appSettings.arrAllEveningHabits.Insert(appSettings.nHabitIndex + 1, itemToMove);
                                break;
                            }
                        }
                    }
                }
            }
            Close();
        }

        private void ListHabits_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Check if an item is selected, chenage button text
            if (ListHabits.SelectedItem != null)
            {
                HabitListSelectable selectedItem = ListHabits.SelectedItem as HabitListSelectable;
                string sButtonText = appSettings.ConfigFileStrings["activityInfo"]["startActivityButtonText"].ToString();
                int nDurationSeconds = (int)selectedItem.nDuration;
                if (nDurationSeconds == appSettings.nTimerValForNonTimerActivities)//86400
                {
                    sButtonText = appSettings.ConfigFileStrings["activityInfo"]["startNonTimerActivityButtonText"].ToString();
                    sButtonText = sButtonText.Replace("<NAME>", selectedItem.ActivityName);
                }
                else
                {
                    sButtonText = sButtonText.Replace("<TIME>", appSettings.SecondsToTextHMS(nDurationSeconds));
                    sButtonText = sButtonText.Replace("<NAME>", selectedItem.ActivityName);
                }
                
                btnStartTXT.Text = sButtonText;
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var source = (HwndSource)PresentationSource.FromVisual(this);
            var isLightTheme = Global.IsLightTheme();
            if (appSettings.bAutoTheme)
            {
                if (source != null)
                {
                    app.UseImmersiveDarkMode(source.Handle, !isLightTheme);
                }
            }
            else
            {
                if (appSettings.sTheme == "Light")
                {
                    app.UseImmersiveDarkMode(source.Handle, false);
                }
                else if (appSettings.sTheme == "Dark")
                {
                    app.UseImmersiveDarkMode(source.Handle, true);
                }
            }
        }
    }
}
