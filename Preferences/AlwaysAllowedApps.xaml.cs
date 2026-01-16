using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
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
using Microsoft.Win32;

namespace Focus_Bear.Preferences
{
    public partial class AlwaysAllowedApps : UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;

        FocusBearMainWindow _ParentWindow = null;

        public AlwaysAllowedApps()
        {
            
        }
        public AlwaysAllowedApps(FocusBearMainWindow pp)
        {
            InitializeComponent();
            _ParentWindow = pp;

            try
            {
                Heading.Text = "Always Allowed Apps";
                Description.Text = "These apps will never be blocked, regardless of focus mode, blocking schedules, or strictness settings. Use this for essential apps that you always need access to.";

                btnAddApp.ToolTip = "Add an app to the always allowed list";
                btnDeleteApp.ToolTip = "Remove the selected app from the always allowed list";

                btnAddAppTXT.Text = "Add App";
                btnDeleteAppTXT.Text = "Remove";
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AlwaysAllowedApps text setting ex - " + ex.Message);
            }

            ListAllowedApps.ItemsSource = appSettings.arrAlwaysAllowedApps;
            
        }

        private void btnAddApp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*";
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                openFileDialog.Title = "Select an application to always allow";

                if (openFileDialog.ShowDialog() == true)
                {
                    string appPath = openFileDialog.FileName;
                    string appName = System.IO.Path.GetFileNameWithoutExtension(appPath);

                    var listItems = appSettings.arrAlwaysAllowedApps.Where(i => System.IO.Path.GetFileNameWithoutExtension(i).ToLower() == appName.ToLower());
                    if (listItems.Count() == 0)
                    {
                        appSettings.arrAlwaysAllowedApps.Add(appPath);
                        appSettings.CapturePostHog("add-always-allowed-app");
                    }
                    else
                    {
                        string sErr = "This app is already in the always allowed list.";
                        
                        FBMessageBox ff = new FBMessageBox(sErr, "Focus Bear");
                        ff.Owner = _ParentWindow;
                        ff.ShowDialog();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("btnAddApp_Click in Always allowed apps List" + ex.Message);
            }
        }

        private void btnDeleteApp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = ListAllowedApps.SelectedItem as string;
                if (selectedItem != null)
                {
                    appSettings.arrAlwaysAllowedApps.Remove(selectedItem);
                    appSettings.CapturePostHog("remove-always-allowed-app");
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Always allowed apps btnDeleteApp_Click + " + ex.Message);
            }
        }
    }
    
}
