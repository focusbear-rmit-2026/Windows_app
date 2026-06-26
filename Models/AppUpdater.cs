
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
        public bool bCheckingForFocusButton = false;
        public AppUpdater()
        {
            this._UpdateFolderPath = AppSettings.AppDataDirPath;
        }

        public async Task<bool> CheckForUpdate(bool bCheckingForFocusButton = false)
        {
            LogWriter.LogWrite("Task<bool> CheckForUpdate()");
            try
            {
                this.bCheckingForFocusButton = bCheckingForFocusButton;
                string iniFileURL = bCheckingForFocusButton ? AppSettings.FocusButtonUpdateIniFileURL : AppSettings.AppIniFileURL;
                string updateINIPath = bCheckingForFocusButton ? AppSettings.FocusButtonUpdateINIPath : AppSettings.AppUpdateINIPath;
                _DownloadUpdateFileName = bCheckingForFocusButton ? "FirmwareUpdate.zip" : "FocusBearSetup.exe";

                HttpCommunication http = new HttpCommunication();
                if (await http.Download(iniFileURL, updateINIPath, false))
                {
                    //If update is available the True will be returned;
                    return ReadUpdateDetails(updateINIPath, bCheckingForFocusButton);
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

        private bool ReadUpdateDetails(string updateINIPath, bool bCheckingForFocusButton = false)
        {
            LogWriter.LogWrite("ReadUpdateDetails()");

            try
            {
                INIParser objIniParser = new INIParser(updateINIPath);
                string section = "application";
                string versionInsideINIFile = objIniParser.ReadValue(section, "program_version");

                _DownloadUpdateURL = objIniParser.ReadValue(section, "program_url");

                Version updateVersion = null;
                Version.TryParse(versionInsideINIFile, out updateVersion);

                if (bCheckingForFocusButton)
                {
                    // Offer the firmware when the server has a build AND either the device's firmware
                    // version is unknown (a blank/un-flashed device, or one that didn't report a version)
                    // or the server build is newer. The unknown case is what lets a brand-new device with
                    // no firmware be flashed instead of being treated as already up to date.
                    if (updateVersion != null && (appSettings.FirmwareVersion == null || updateVersion > appSettings.FirmwareVersion))
                    {
                        LogWriter.LogWrite("Firmware Update available");
                        return true;
                    }
                }
                else
                {
                    if (updateVersion != null && appSettings.AppVersion != null && updateVersion > appSettings.AppVersion)
                    {
                        LogWriter.LogWrite("Application Update available");
                        return true;
                    }
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
