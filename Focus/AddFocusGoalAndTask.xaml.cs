using Focus_Bear.API;
using Focus_Bear.SetIntention;
using Focus_Bear.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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
using static Focus_Bear.Focus.GentleBlock;

namespace Focus_Bear.Focus
{
    /// <summary>
    /// Interaction logic for AddFocusGoalAndTask.xaml
    /// </summary>
    public partial class AddFocusGoalAndTask : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;
        public string sFocusGoal;
        public AddFocusGoalAndTask()
        {

        }
        public AddFocusGoalAndTask(bool bNoGoalNoTask = false)
        {
            InitializeComponent();

            try
            {
                FocusGoal.Text = appSettings.ConfigFileStrings["popUpAddNewFocusGoalVcInfo"]["titleGoal"].ToString();
                NewTask.Text = appSettings.ConfigFileStrings["popUpAddNewFocusGoalVcInfo"]["titleNewTask"].ToString();

                PreviousTask.Text = appSettings.ConfigFileStrings["popUpAddNewFocusGoalVcInfo"]["titlePreviousTask"].ToString();
                MarkPreviousTaskCompleteText.Text = appSettings.ConfigFileStrings["popUpAddNewFocusGoalVcInfo"]["buttonMarkCurrentTaskAsCompleted"].ToString();

                btnSaveText.Text = appSettings.ConfigFileStrings["popUpAddNewFocusGoalVcInfo"]["buttonSaveText"].ToString();

                if (bNoGoalNoTask)
                {
                    Heading.Text = appSettings.ConfigFileStrings["popUpBlockConfirmVcInfo"]["pickTaskText"].ToString();
                    PreviousTask.Visibility = Visibility.Collapsed;
                    PreviousTaskValue.Visibility = Visibility.Collapsed;
                    MarkPreviousTaskComplete.Visibility = Visibility.Collapsed;
                    
                }
                else
                {
                    Heading.Text = appSettings.ConfigFileStrings["popUpAddNewFocusGoalVcInfo"]["title"].ToString();
                    PreviousTask.Visibility = Visibility.Visible;
                    PreviousTaskValue.Visibility = Visibility.Visible;
                    MarkPreviousTaskComplete.Visibility = Visibility.Visible;

                    PreviousTaskValue.Text = appSettings.sCurrentTaskPlayingInToDoPlayer;
                    TxtIntention.Text = appSettings.sRunningFocusModeIntention; //place it
                    TxtIntention.Focus();
                    TxtIntention.SelectAll();
                }
                if (appSettings.sAllTasks.Length > 0)
                {
                    //File the tasks combo box
                    JObject obj = JObject.Parse(appSettings.sAllTasks);

                    JArray tasks = (JArray)obj["data"];

                    var taskList = tasks
                        .Select(t => new TaskItem
                        {
                            Id = t["id"]?.ToString(),
                            Name = t["title"]?.ToString()
                        })
                        .ToList();

                    ComboBoxTasks.ItemsSource = taskList;
                    ComboBoxTasks.DisplayMemberPath = "Name";
                }
                //"fetchingTaskText": "Fetching tasks...",
                //"SavingTaskText": "Saving task...",
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AddFocusGoalAndTask texts ex - " + ex.Message);
            }
        }

        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            appSettings.sRunningFocusModeIntention = TxtIntention.Text;

            TaskItem selectedTask = ComboBoxTasks.SelectedItem as TaskItem;

            string selectedName;
            string selectedId;
            if (selectedTask != null)
            {
                // Existing item selected
                selectedName = selectedTask.Name;
                selectedId = selectedTask.Id;

                if (appSettings.sAllTasks.Length > 0)
                {
                    JObject obj = JObject.Parse(appSettings.sAllTasks);

                    JArray tasks = (JArray)obj["data"];

                    JObject matchedTask = tasks.FirstOrDefault(t => t["id"]?.ToString() == selectedId) as JObject;

                    if (matchedTask != null)
                    {
                        //Just updating the local copy meanwhile we get notification from the webview
                        appSettings.sCurrentTaskPlayingInToDoPlayer = selectedName;

                        // Found exact task
                        //Let's put it in the sCurrentTaskCreated and use the same in AddAndPlayTaskInCurrentList
                        appSettings.sCurrentTaskCreated = matchedTask.ToString();

                        //Add in current tasks list and play it in todoplayer
                        bool bMarkCurrentTaskAsComplete = MarkPreviousTaskComplete.IsChecked.Value;

                        await Dispatcher.BeginInvoke((Action)(() =>
                        {
                            if (((App)(Application.Current)).wndFocusBearMainWindow.PageToDoPlayer != null)
                            {
                                ((App)(Application.Current)).wndFocusBearMainWindow.PageToDoPlayer.AddAndPlayTaskInCurrentList(bMarkCurrentTaskAsComplete);
                                ((App)(Application.Current)).wndFocusBearMainWindow.PageToDoPlayer.UpdateIntentionForToDoPlayer();
                            }
                        }));
                    }
                }

            }
            else
            {
                // User typed new value
                selectedName = ComboBoxTasks.Text;
                selectedId = null;
                if (selectedName.Length > 0)
                {
                    FocusBearAPIClient client = new FocusBearAPIClient();
                    var m = await client.AddTask(selectedName);
                    if (m == 1)
                    {
                        bool bMarkCurrentTaskAsComplete = MarkPreviousTaskComplete.IsChecked.Value;
                        //Add in current tasks list and play it in todoplayer
                        if (appSettings.sCurrentTaskCreated.Length > 0)
                        {
                            //Just updating the local copy meanwhile we get notification from the webview
                            appSettings.sCurrentTaskPlayingInToDoPlayer = selectedName;

                            await Dispatcher.BeginInvoke((Action)(() =>
                            {
                                if (((App)(Application.Current)).wndFocusBearMainWindow.PageToDoPlayer != null)
                                {
                                    ((App)(Application.Current)).wndFocusBearMainWindow.PageToDoPlayer.AddAndPlayTaskInCurrentList(bMarkCurrentTaskAsComplete);
                                    ((App)(Application.Current)).wndFocusBearMainWindow.PageToDoPlayer.UpdateIntentionForToDoPlayer();
                                }
                            }));
                        }
                    }
                }
            }

            Mouse.OverrideCursor = null;
            Close();
        }
    }
}
