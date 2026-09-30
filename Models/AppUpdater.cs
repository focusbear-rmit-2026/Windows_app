
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
        /// <summary>The last firmware check found no image for the connected device's board.</summary>
        public bool NoFirmwareForBoard;
        public AppUpdater()
        {
            this._UpdateFolderPath = AppSettings.AppDataDirPath;
        }

        public async Task<bool> CheckForUpdate(bool bCheckingForFocusButton = false)
        {
            LogWriter.LogWrite("Task<bool> CheckForUpdate()");
            // The E2E test build never downloads or installs an update (app or Focus Button firmware).
            if (!E2EBuild.MachineIntegrationAllowed) return false;
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

                //Missing components count as 0 (System.Version makes them -1, so 3.0.249.0 > 3.0.249 offered the
                //installed version again), and a stray "v" or whitespace doesn't hide an update.
                Version updateVersion = FocusBear.Core.Updates.VersionCompare.Parse(versionInsideINIFile);

                if (bCheckingForFocusButton)
                {
                    //Only an image built for this device's board is offered: the INI served the M5Dial build to
                    //every device, the VIEWE C3 included. A device that hasn't named its board (blank) may get the
                    //image, and the flash refuses it unless the image header matches the detected chip.
                    var release = FocusBear.Core.FocusButton.FirmwareBoard.ReleaseFor(
                        FocusBear.Core.FocusButton.FirmwareBoard.ParseIni(System.IO.File.ReadAllText(updateINIPath)),
                        appSettings.sFirmwareHardwareCode);
                    if (release == null)
                    {
                        LogWriter.LogWrite("Firmware update: no image for board '" + appSettings.sFirmwareHardwareCode + "' in the update INI");
                        NoFirmwareForBoard = true;
                        return false;
                    }
                    _DownloadUpdateURL = release.Url;
                    updateVersion = release.Version;

                    // Offer the firmware when the server has a build AND either the device's firmware
                    // version is unknown (a blank/un-flashed device, or one that didn't report a version)
                    // or the server build is newer. The unknown case is what lets a brand-new device with
                    // no firmware be flashed instead of being treated as already up to date.
                    if (FocusBear.Core.Updates.VersionCompare.IsNewer(updateVersion, appSettings.FirmwareVersion))
                    {
                        LogWriter.LogWrite("Firmware Update available");
                        return true;
                    }
                }
                else
                {
                    if (appSettings.AppVersion != null && FocusBear.Core.Updates.VersionCompare.IsNewer(updateVersion, appSettings.AppVersion))
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
