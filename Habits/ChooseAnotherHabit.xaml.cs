using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
                    string sActivityName = appSettings.arrAllEveningHabits[i].name;
                    string sID = appSettings.arrAllEveningHabits[i].id;
                    string sNo = string.Format("{0}.", (nSerialNo).ToString());
                    double nDurationSeconds = appSettings.arrAllMorningHabits[i].durationSeconds;
                    var ActivityItem = new HabitListSelectable { id = sID, SNumber = sNo, ActivityName = sActivityName, nDuration = nDurationSeconds };
                    FilteredList.Add(ActivityItem);
                    nSerialNo++;
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
                sButtonText = sButtonText.Replace("<TIME>", appSettings.SecondsToTextHMS((int)selectedItem.nDuration));
                sButtonText = sButtonText.Replace("<NAME>", selectedItem.ActivityName);
                btnStartTXT.Text = sButtonText;
            }
        }
    }
}
