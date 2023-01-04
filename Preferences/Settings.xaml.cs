using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace Focus_Bear.Preferences
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class Settings : Page
    {
        AppSettings appSettings = AppSettings.GetInstance;
        public Settings()
        {
            InitializeComponent();
            /*  "preferencesInfo":{
             *  "titleAppMode":"Focus bear mode:",
             "checkBoxCuddlyBearText":"Cuddly Bear mode",
            "descriptionCuddlyBearText":"Cuddly Bear mode: kind and gentle - gives you the benefit of the doubt when you open a non-allowed URL. Lets you temporarily allow blocked URLs during a focus mode.",
            "checkBoxGrizzlyBearText":"Grizzly Bear mode",
            "descriptionGrizzlyBearText":"Grizzly Bear mode: super strict - immediately block URLs that aren't allowed and don't let yourself allow them until the focus mode is over.",
             "versionText": "Version:",
              "buttonExportLogsText":"Click here to export logs to share with Support Team.",
        */

            /* "menuInfo":{
              "menuItemExportStatisticsTitle":"Export Statistics",
              "menuItemCheckForUpdatesTitle":"Check for updates",
              */
            FocusBearMode.Text = appSettings.ConfigFileStrings["preferencesInfo"]["titleAppMode"].ToString();
            CuddlyMode.Content = appSettings.ConfigFileStrings["preferencesInfo"]["checkBoxCuddlyBearText"].ToString();
            GrizzlyMode.Content = appSettings.ConfigFileStrings["preferencesInfo"]["checkBoxGrizzlyBearText"].ToString();

            CuddlyModeDesc.Text = appSettings.ConfigFileStrings["preferencesInfo"]["descriptionCuddlyBearText"].ToString();
            GrizzlyModeDesc.Text = appSettings.ConfigFileStrings["preferencesInfo"]["descriptionGrizzlyBearText"].ToString();

            Version.Text = appSettings.ConfigFileStrings["preferencesInfo"]["versionText"].ToString() + appSettings.AppVersion.ToString();
            btnExportLogsTXT.Text = appSettings.ConfigFileStrings["preferencesInfo"]["buttonExportLogsText"].ToString();

            btnExportStatisticsTXT.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemExportStatisticsTitle"].ToString();
            btnCheckForUpdatesTXT.Text = appSettings.ConfigFileStrings["menuInfo"]["menuItemCheckForUpdatesTitle"].ToString();

            CuddlyMode.IsChecked = (appSettings.nFocudBearMode == 1) ? true : false;//1 is cuddly bear mode

        }

        private void btnExportStatistics_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                /*        "menuInfo":{
                         "menuItemExportStatisticsTitle":"Export Statistics",*/

                var folderBrowserDialog1 = new FolderBrowserDialog();
                folderBrowserDialog1.Description = appSettings.ConfigFileStrings["menuInfo"]["menuItemExportStatisticsTitle"].ToString();

                // Show the FolderBrowserDialog.
                DialogResult result = folderBrowserDialog1.ShowDialog();
                if (result == DialogResult.OK)
                {
                    string startPath = AppSettings.AppDataDirPath + "\\FocusBearStats.CSV";

                    string folderName = folderBrowserDialog1.SelectedPath;
                    string statsExportPath = folderName + "\\FocusBearStats.CSV";

                    if (File.Exists(statsExportPath))
                    {
                        Global.DeleteFile(statsExportPath, false);
                    }

                    File.Copy(startPath, statsExportPath);

                    appSettings.CapturePostHog("export-statistics");
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnExportLogs_Click ex -" + ex.Message);
            }

        }

        private void btnCheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            LogWriter.LogWrite("btnCheckForUpdates_Click "  );
            ((App)(Application.Current)).CheckForUpdate(true);
            Mouse.OverrideCursor = null;

        }

        private void btnExportLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                /*        "buttonPanelExportLogsText":"Export Logs",
                "panelExportLogsText":"Export logs to share with Focus Bear Support Team",
                "exportSuccessText":"Your logs are available to share in the FocusBearLogs.zip file in the <URL> directory",*/
                
                var folderBrowserDialog1 = new FolderBrowserDialog();
                folderBrowserDialog1.Description = appSettings.ConfigFileStrings["preferencesInfo"]["panelExportLogsText"].ToString();
                
                // Show the FolderBrowserDialog.
                DialogResult result = folderBrowserDialog1.ShowDialog();
                if (result == DialogResult.OK)
                {
                    string folderName = folderBrowserDialog1.SelectedPath;
                    string startPath = AppSettings.AppDataDirLogsPath;
                    string zipPath = folderName + "\\FocusBearLogs.zip";

                    if(File.Exists(zipPath))
                    {
                        Global.DeleteFile(zipPath, false);
                    }

                    ZipFile.CreateFromDirectory(startPath, zipPath, CompressionLevel.Fastest, false);
                    string ss = appSettings.ConfigFileStrings["preferencesInfo"]["exportSuccessText"].ToString();
                    MessageBox.Show(ss.Replace("<URL>", folderName), "Focus Bear");
                }
            
               
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("btnExportLogs_Click ex -" + ex.Message);
            }
        }

        private void GrizzlyMode_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.nFocudBearMode = 0;//0 is Grizzly bear mode
        }

        private void CuddlyMode_Checked(object sender, RoutedEventArgs e)
        {
            appSettings.nFocudBearMode = 1;//1 is cuddly bear mode
        }
    }
}
