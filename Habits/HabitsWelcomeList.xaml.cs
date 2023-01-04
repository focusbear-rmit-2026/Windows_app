using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Focus_Bear.Habits
{
    /// <summary>
    /// Interaction logic for HabitsWelcomeList.xaml
    /// </summary>
    public partial class HabitsWelcomeList : Page
    {
        public class HabitList : INotifyPropertyChanged
        {
            public string SNumber { get; set; }
            public string ActivityName { get; set; }

            public HabitList()
            {
                //// NavItems = new ObservableCollection<StartupAppModel>();
            }
            public event PropertyChangedEventHandler PropertyChanged;
            private void OnChanged(string propertyName)
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
            }
        }

        private ObservableCollection<HabitList> _ObjListActivityModel = null;
        AppSettings appSettings = AppSettings.GetInstance;
        private HabitWindow _parentWindow;
        public HabitsWelcomeList()
        {
        }
        public HabitsWelcomeList(HabitWindow parentWindow) : this()
        {
            InitializeComponent();

            _parentWindow = parentWindow;
            /*
            "activityInfo":{
                "startMorningHabitTitleText":"Good Morning! Here are the habits to kick off an awesome day.",
                 "buttonBeginText": "Begin",
                "startEveningHabitTitleText":"Good Evening! Let’s go through your wind down routine.",

            */
            int nCount = 0;
            if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
            {
                Heading.Text = appSettings.ConfigFileStrings["activityInfo"]["startMorningHabitTitleText"].ToString();
                nCount = appSettings.arrAllMorningHabits.Count;

                string imagePath = ("pack://application:,,,/Focus Bear;component/images/gifs/Wakingup.gif");
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(imagePath);
                image.EndInit();
                ImageBehavior.SetAnimatedSource(TopIcon, image);

            }
            else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
            {
                Heading.Text = appSettings.ConfigFileStrings["activityInfo"]["startEveningHabitTitleText"].ToString();
                nCount = appSettings.arrAllEveningHabits.Count;
            }
            btnBeginTXT.Text = appSettings.ConfigFileStrings["activityInfo"]["buttonBeginText"].ToString();


            _ObjListActivityModel = new ObservableCollection<HabitList>();
            ListActivities.ItemsSource = _ObjListActivityModel;

            for(int i = 0; i < nCount; i++)
            {
                string sActivityName = "";
                if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS)
                {
                    sActivityName = appSettings.arrAllMorningHabits[i].name;
                }
                else if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS)
                {
                    sActivityName = appSettings.arrAllEveningHabits[i].name;
                }

                string sNo = string.Format("{0}.", (i+1).ToString());
                var ActivityItem = new HabitList { SNumber = sNo, ActivityName = sActivityName };
                _ObjListActivityModel.Add(ActivityItem);
            }

            Sounds.PlayRoutineStartSound();
        }

        private void btnBegin_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("Welcome Begin Clicked");
            try
            {
                _parentWindow.ShowExtraPersonalFocus();
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("WElcome btnBegin_Click" + ex.Message);
                Debug.WriteLine(ex.Message);
            }
        }
    }
}
