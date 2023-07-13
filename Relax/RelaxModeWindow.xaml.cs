using Focus_Bear.API;
using Focus_Bear.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Focus_Bear.Relax
{
    /// <summary>
    /// Interaction logic for RelaxModeWindow.xaml
    /// </summary>
    public partial class RelaxModeWindow : Window
    {
        AppSettings appSettings = AppSettings.GetInstance;

        public ObservableCollection<RelaxURL> RelaxURLs = new ObservableCollection<RelaxURL>();

        int nTimer = 300;  
        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Normal);

        bool bForceClose = false;
        public RelaxModeWindow()
        {
        }
        public RelaxModeWindow(int nTime)
        {
            InitializeComponent();

            nTimer = nTime;
            this.Topmost = true;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            try
            {
                Heading.Text = "Sites you saved for relax mode:";
                btnDeleteURLs.ToolTip = "Remove Selected URLs";
                btnMovetoNextTXT.Text = "I'm done! Move to next activity"; //todo Win texts
                TimeRemaining.Text = appSettings.ConfigFileStrings["brainDumpVcInfo"]["titleTimeRemainingText"].ToString() + " " + appSettings.SecondsToTimerText(nTimer);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("RelaxModeWindow() ex - " + ex.Message);
            }
            this.Cursor = Cursors.Wait;
            SetListView();

            
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
            CommandManager.InvalidateRequerySuggested();

        }

        private async void SetListView()
        {
            //WE will need to get the URLS
            FocusBearAPIClient client = new FocusBearAPIClient();
            var Response = await client.GetSavedWebsite();
            if (Response == 1)
            {
                if (appSettings.sSavedURLSForRelaxActivity != null && appSettings.sSavedURLSForRelaxActivity.Length > 0)
                {
                    JArray arrURLS = JArray.Parse(appSettings.sSavedURLSForRelaxActivity);
                    LogWriter.LogWrite(arrURLS.ToString());

                    foreach (JObject item in arrURLS)
                    {
                        string sID = item.ContainsKey("id") ? item.GetValue("id").ToString() : "";
                        string sURL = item.ContainsKey("url") ? item.GetValue("url").ToString() : "";
                        if(sURL.Length > 0)
                        {
                            RelaxURLs.Add(new RelaxURL(sID, sURL, false));
                        }
                    }

                }
            }
            this.Cursor = Cursors.Arrow;
            ListRelaxURLs.ItemsSource = RelaxURLs;

        }
        private void btnDeleteURLs_Click(object sender, RoutedEventArgs e)
        {
            for(int i=0; i< RelaxURLs.Count(); i++)
            {
                if(RelaxURLs[i].IsItemChecked)
                {
                    RelaxURLs.RemoveAt(i);
                }
            }
        }

        private void btnMovetoNext_Click(object sender, RoutedEventArgs e)
        {
            dispatcherTimer.Stop();

            FinishWork();
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
#if DEBUG
            int val = nTimer--;
#else
            int val = nTimer--;
#endif

            try
            {

                TimeRemaining.Text = appSettings.ConfigFileStrings["brainDumpVcInfo"]["titleTimeRemainingText"].ToString() + " " + appSettings.SecondsToTimerText(nTimer);

                CommandManager.InvalidateRequerySuggested();
 
                if (nTimer < 0 )
                {
                    TimeRemaining.Text = TimeRemaining.Text = appSettings.ConfigFileStrings["brainDumpVcInfo"]["titleTimeRemainingText"].ToString() + " 00:00";
                    dispatcherTimer.Stop();
                    FinishWork();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("relax urls Window = " + ex.Message);
            }

        }

        private void FinishWork()
        {
            if (((App)(Application.Current)).wndHabits != null)
            {
                //show the habit window
                ((App)(Application.Current)).wndHabits.Show();
                ((App)(Application.Current)).wndHabits.WindowState = WindowState.Maximized;
                //set timer to 0
                ((App)(Application.Current)).wndHabits.PageHabitActivityOnGoing.SetTimerToZeroToFinish(); ;

            }
            bForceClose = true;
            Close(); //Best to close here .
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!bForceClose)
            {
                e.Cancel = true;
                this.WindowState = WindowState.Minimized;
            }
        }

        private void ListRelaxURLs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedURL = ListRelaxURLs.SelectedItem as RelaxURL;
            if (selectedURL != null)
            {
                try
                {
                    Process.Start(selectedURL.sURL);
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("ListRelaxURLs_SelectionChanged  ex= " + ex.Message);
                }
            }
        }
    }
}
