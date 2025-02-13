using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
    /// Interaction logic for ChooseHabits.xaml
    /// </summary>
    /// 
    
    public partial class ChooseHabits : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        public ObservableCollection<HabitListSelectable> FilteredList = new ObservableCollection<HabitListSelectable>();
        public ChooseHabits()
        {
        }
        public ChooseHabits(HabitWindow ParentWindow)
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
                this.Title = appSettings.ConfigFileStrings["chooseHabitsVCInfo"]["title"].ToString();
                Heading.Text = appSettings.ConfigFileStrings["chooseHabitsVCInfo"]["subTitle"].ToString();
                SelectDeselect.Content = appSettings.ConfigFileStrings["chooseHabitsVCInfo"]["checkBoxSelectAllText"].ToString();
                btnContinueText.Text = appSettings.ConfigFileStrings["chooseHabitsVCInfo"]["buttonContinueText"].ToString();
                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                {
                    CompletelySkipTXT.Text = appSettings.ConfigFileStrings["chooseHabitsVCInfo"]["buttonSkipMorningHabitsText"].ToString();
                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    CompletelySkipTXT.Text = appSettings.ConfigFileStrings["chooseHabitsVCInfo"]["buttonSkipEveningHabitsText"].ToString();
                }

                //To be updated real time
                TotalTime.Text = appSettings.ConfigFileStrings["chooseHabitsVCInfo"]["titleRequiredTime"].ToString();
                ExpectedTime.Text = appSettings.ConfigFileStrings["chooseHabitsVCInfo"]["titleCompletionTime"].ToString();

            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ChooseHabits initialization" + ex.Message);
            }
            InitializeHabitsList();
            FilteredList = appSettings._ObjHabitListSelectable;
            ListHabits.ItemsSource = FilteredList;
        }

        public void InitializeHabitsList()
        {
            appSettings._ObjHabitListSelectable.Clear();
            int nCount = 0;
            int nSelectedCount = 0;
            if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
            {
                nCount = appSettings.arrAllMorningHabitsFiltered.Count;
                nSelectedCount = appSettings.arrAllMorningHabits.Count;
            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
            {
                nCount = appSettings.arrAllEveningHabitsFiltered.Count;
                nSelectedCount = appSettings.arrAllEveningHabits.Count;
            }
            int nDurationSeconds = 0;
            for (int i = 0; i < nCount; i++)
            {
                string sActivityName = "";
                string sID = "";
                bool bSelected = false;
                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                {
                    sActivityName = appSettings.arrAllMorningHabitsFiltered[i].name;
                    sID = appSettings.arrAllMorningHabitsFiltered[i].id;
                    for (int j = 0; j < nSelectedCount; j++)
                    {
                        if (sID == appSettings.arrAllMorningHabits[j].id)
                        {
                            bSelected = true;
                            nDurationSeconds = appSettings.arrAllMorningHabits[j].durationSeconds;
                            break;
                        }
                    }
                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    sActivityName = appSettings.arrAllEveningHabitsFiltered[i].name;
                    sID = appSettings.arrAllEveningHabitsFiltered[i].id;
                    for (int j = 0; j < nSelectedCount; j++)
                    {
                        if (sID == appSettings.arrAllEveningHabits[j].id)
                        {
                            bSelected = true;
                            nDurationSeconds = appSettings.arrAllEveningHabits[j].durationSeconds;
                            break;
                        }
                    }
                }
                string sNo = string.Format("{0}.", (i + 1).ToString());
                var ActivityItem = new HabitListSelectable { id = sID, SNumber = sNo, ActivityName = sActivityName, bIsSelected = bSelected , nDuration = nDurationSeconds};
                ActivityItem.PropertyChanged += Habit_PropertyChanged;
                appSettings._ObjHabitListSelectable.Add(ActivityItem);
            }

            //TotalTime.Text = appSettings.ConfigFileStrings["chooseHabitsVCInfo"]["titleRequiredTime"].ToString() + appSettings.SecondsToTextHMS(nDurationSeconds);
            //ExpectedTime.Text = appSettings.ConfigFileStrings["chooseHabitsVCInfo"]["titleCompletionTime"].ToString() + DateTime.Now.AddSeconds(nDurationSeconds);
            SetContinueButtonState();
        }
        private void SetContinueButtonState()
        {
            // If any item is unchecked, uncheck "Select All"
            if (appSettings._ObjHabitListSelectable.Any(h => !h.bIsSelected))
            {
                SelectDeselect.Unchecked -= SelectDeselect_Unchecked;
                SelectDeselect.IsChecked = false;
                SelectDeselect.Unchecked += SelectDeselect_Unchecked;
            }
            // If all items are selected, check "Select All"
            else if (appSettings._ObjHabitListSelectable.All(h => h.bIsSelected))
            {
                SelectDeselect.Checked -= SelectDeselect_Checked;
                SelectDeselect.IsChecked = true;
                SelectDeselect.Checked += SelectDeselect_Checked;
            }

            if (appSettings._ObjHabitListSelectable.All(h => !h.bIsSelected))
            {
                btnContinue.IsEnabled = false;
            }
            else
            {
                btnContinue.IsEnabled = true;
            }

            //Update expected time value
            UpdatedExpectedTimeValues();
        }
        private void UpdatedExpectedTimeValues()
        {
            
            double nDurationSeconds = 0;
            for (int i = 0; i < appSettings._ObjHabitListSelectable.Count; i++)
            {
                if(appSettings._ObjHabitListSelectable[i].bIsSelected)
                {
                    nDurationSeconds = nDurationSeconds + appSettings._ObjHabitListSelectable[i].nDuration;
                }
                
            }

            TotalTime.Text = appSettings.ConfigFileStrings["chooseHabitsVCInfo"]["titleRequiredTime"].ToString() + appSettings.SecondsToTextHMS((int)nDurationSeconds);
            ExpectedTime.Text = appSettings.ConfigFileStrings["chooseHabitsVCInfo"]["titleCompletionTime"].ToString() + DateTime.Now.AddSeconds(nDurationSeconds);

        }
        // Event handler for individual item property changes
        private void Habit_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HabitListSelectable.bIsSelected))
            {
                // If any item is unchecked, uncheck "Select All"
                if (appSettings._ObjHabitListSelectable.Any(h => !h.bIsSelected))
                {
                    SelectDeselect.Unchecked -= SelectDeselect_Unchecked;
                    SelectDeselect.IsChecked = false;
                    SelectDeselect.Unchecked += SelectDeselect_Unchecked;
                }
                // If all items are selected, check "Select All"
                else if (appSettings._ObjHabitListSelectable.All(h => h.bIsSelected))
                {
                    SelectDeselect.Checked -= SelectDeselect_Checked;
                    SelectDeselect.IsChecked = true;
                    SelectDeselect.Checked += SelectDeselect_Checked;
                }

                SetContinueButtonState();
            }
        }

        private void SelectDeselect_Checked(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < appSettings._ObjHabitListSelectable.Count; i++)
            {
                appSettings._ObjHabitListSelectable[i].PropertyChanged -= Habit_PropertyChanged;
                appSettings._ObjHabitListSelectable[i].bIsSelected = true;
                appSettings._ObjHabitListSelectable[i].PropertyChanged += Habit_PropertyChanged;
            }
            SetContinueButtonState();
        }

        private void SelectDeselect_Unchecked(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < appSettings._ObjHabitListSelectable.Count; i++)
            {
                appSettings._ObjHabitListSelectable[i].PropertyChanged -= Habit_PropertyChanged;
                appSettings._ObjHabitListSelectable[i].bIsSelected = false;
                appSettings._ObjHabitListSelectable[i].PropertyChanged += Habit_PropertyChanged;
            }
            SetContinueButtonState();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Filter the list based on the search criteria
            var FilteredList = appSettings._ObjHabitListSelectable
                .Where(i => i.ActivityName.ToLower().Contains(SearchBox.Text.ToLower()))
                .ToList();  // Convert the filtered result to a List

            // Convert the List to an ObservableCollection
            ObservableCollection<HabitListSelectable> observableFilteredList =
                new ObservableCollection<HabitListSelectable>(FilteredList);

            // Set the filtered list as the ItemsSource
            ListHabits.ItemsSource = observableFilteredList;
        }

        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            // If any item is unchecked, uncheck "Select All"
            if (appSettings._ObjHabitListSelectable.Any(h => !h.bIsSelected))
            {
                //Filter out the ones that are not checked.
                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                {
                    // Create a list of items to remove (items where bIsSelected == false)
                    var itemsToRemove = appSettings._ObjHabitListSelectable.Where(h => !h.bIsSelected).ToList();
                    //Recopy the original filtered list
                    appSettings.arrAllMorningHabits = new List<habit>(appSettings.arrAllMorningHabitsFiltered);
                    foreach (var item in itemsToRemove)
                    {
                        for (int i = 0; i < appSettings.arrAllMorningHabits.Count; i++)
                        {
                            if (appSettings.arrAllMorningHabits[i].id == item.id)
                            {
                                appSettings.arrAllMorningHabits.RemoveAt(i);
                                break;
                            }
                        }
                    }
                    RegistryManager.SaveSelectedMorningHabitsForToday();
                    
                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    // Create a list of items to remove (items where bIsSelected == false)
                    var itemsToRemove = appSettings._ObjHabitListSelectable.Where(h => !h.bIsSelected).ToList();
                    //Recopy the original filtered list
                    appSettings.arrAllEveningHabits = new List<habit>(appSettings.arrAllEveningHabitsFiltered);

                    foreach (var item in itemsToRemove)
                    {
                        for (int i = 0; i < appSettings.arrAllEveningHabits.Count; i++)
                        {
                            if (appSettings.arrAllEveningHabits[i].id == item.id)
                            {
                                appSettings.arrAllEveningHabits.RemoveAt(i);
                            }
                        }
                    }
                    RegistryManager.SaveSelectedEveningHabitsForToday();
                }
            }
            else
            {
                //means everything is checked
                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                {
                    //Recopy the original filtered list
                    appSettings.arrAllMorningHabits = new List<habit>(appSettings.arrAllMorningHabitsFiltered);
                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    //Recopy the original filtered list
                    appSettings.arrAllEveningHabits = new List<habit>(appSettings.arrAllEveningHabitsFiltered);
                }
            }
            Close();
        }

        private void btnCompletelySkip_Click(object sender, RoutedEventArgs e)
        {
            //WE will set the morning habits shown date for today
            RegistryManager.SetMorningHabitsShownTillDate();
            DialogResult = false;
            Close();
        }
    }
}
