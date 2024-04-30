
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using SystemCare.Models;

namespace Focus_Bear.Models
{
    public class AppUpdater
    {
        AppSettings appSettings = AppSettings.GetInstance;

        public event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressHandler;
        public event EventHandler<AsyncCompletedEventArgs> AsyncCompletedEventArgsHandler;

         private string _UpdateFolderPath;
        public string _UpdateHash;
        private double _DownloadSize;
        private string _DownloadUpdateURL;
        private string _DownloadUpdateFileName;
        public bool ForceUpdate;

        public AppUpdater()
        {
            this._UpdateFolderPath = AppSettings.AppDataDirPath;
        }

        public async Task<bool> CheckForUpdate()
        {
            LogWriter.LogWrite("Task<bool> CheckForUpdate()");
            try
            {
                string iniFileURL = AppSettings.AppIniFileURL;
                string updateINIPath = AppSettings.AppUpdateINIPath;

                HttpCommunication http = new HttpCommunication();
                if (await http.Download(iniFileURL, updateINIPath, false))
                {
                    //If update is available the True will be returned;
                    return ReadUpdateDetails(updateINIPath);
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("1" + ex.Message);
                LogWriter.LogWrite("Task<bool> CheckForUpdate() Exception - " + ex.Message);
                return false;
            }
        }

        private bool ReadUpdateDetails(string updateINIPath)
        {
            LogWriter.LogWrite("ReadUpdateDetails()");

            try
            {
                INIParser objIniParser = new INIParser(updateINIPath);
                string section = "application";
                string versionInsideINIFile = objIniParser.ReadValue(section, "program_version");

                _DownloadUpdateFileName = "FocusBearSetup.exe";

                _DownloadUpdateURL = objIniParser.ReadValue(section, "program_url");

                Version updateVersion = null;
                Version.TryParse(versionInsideINIFile, out updateVersion);
                if (updateVersion != null && appSettings.AppVersion != null && updateVersion > appSettings.AppVersion)
                {
                    LogWriter.LogWrite("Update available");
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ReadUpdateDetails() EX - " + ex.Message);
            }

            return false;
        }

        public async Task<bool> DownloadUpdate()
        {
            HttpCommunication http = new HttpCommunication();
            if (!ForceUpdate)
            {
                http.WebClientDownloadProgressHandler += Http_DownloadProgressChanged;
            }


            http.AsyncCompletedEventArgsHandler += Http_AsyncCompletedEventArgsHandler;

            var res = await http.Download(_DownloadUpdateURL, _UpdateFolderPath + "\\" + _DownloadUpdateFileName, true);
            if (!res)
                return false;

            LogWriter.LogWrite("Update Downloaded successfully");  
            return true;
        }

        private void Http_AsyncCompletedEventArgsHandler(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            AsyncCompletedEventArgsHandler?.Invoke(this, e);
        }

        private void Http_DownloadProgressChanged(object sender, System.Net.DownloadProgressChangedEventArgs e)
        {
            DownloadProgressHandler?.Invoke(this, e);
        }
    }
}
