/*
 * Copyright (c) 2018-2019 Wise Tech Labs Private Limited, All Rights Reserved.
 *
 * File: AppUpdater.cs
 * Project: SMART System Repair
 * User: Gaurav
 *
 * This document is the property of Wise Tech Labs Private Limited
 * It is considered confidential and proprietary.
 *
 * This document may not be reproduced or transmitted in any form, 
 * in whole or in part, without the express written permission of 
 * Wise Tech Labs Private Limited
 *
 */

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
                return false;
            }
        }

        private bool ReadUpdateDetails(string updateINIPath)
        {
            try
            {
                INIParser objIniParser = new INIParser(updateINIPath);
                string section = "application";
                string versionInsideINIFile = objIniParser.ReadValue(section, "program_version");

                _DownloadUpdateFileName = "FocusBearSetup.exe";
                _DownloadSize = Convert.ToInt32(objIniParser.ReadValue(section, "program_size"));
                _DownloadUpdateURL = objIniParser.ReadValue(section, "program_url");
                //ForceUpdate = Convert.ToBoolean(Convert.ToInt16(objIniParser.ReadValue(section, "force_update")));

                Version updateVersion = null;
                Version.TryParse(versionInsideINIFile, out updateVersion);
                if (updateVersion != null && appSettings.AppVersion != null && updateVersion > appSettings.AppVersion)
                {
                    Debug.WriteLine("Update available");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
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

            //if (!ForceUpdate)
            //{
            //    http.WebClientDownloadProgressHandler -= Http_DownloadProgressChanged;
            //    http.AsyncCompletedEventArgsHandler -= Http_AsyncCompletedEventArgsHandler;
            //}

            Debug.WriteLine("Update Downloaded successfully");
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
