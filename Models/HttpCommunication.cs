/*
 * Copyright (c) 2018-2019 Wise Tech Labs Private Limited, All Rights Reserved.
 *
 * File: HttpCommunication.cs
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
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
  
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
 

namespace Focus_Bear.Models
{
    class HttpCommunication : WebClient
    {
      ////  Configuration config = Configuration.GetInstance;

        //protected override WebRequest GetWebRequest(Uri address)
        //{
        //    var objWebRequest = base.GetWebRequest(address);
        //    objWebRequest.Timeout = 20;
        //    return objWebRequest;
        //}

        public event EventHandler<DownloadProgressChangedEventArgs> WebClientDownloadProgressHandler;
        public event EventHandler<AsyncCompletedEventArgs> AsyncCompletedEventArgsHandler;
        public event EventHandler<DownloadFileEventArgs> WebClientDownloadProgressHandlerTimeOut;
 

        public async Task<bool> Download(string URL, string Location, bool shouldReportProgress)
        {
            //Check if internet is connected
            try
            {
                if (shouldReportProgress)
                {
                    this.DownloadProgressChanged += Client_DownloadProgressChanged;
                    this.DownloadFileCompleted += Client_DownloadFileCompleted;
                }


                await DownloadFileTaskAsync(URL, Location);
                return true;


                //using (var client = new HttpClientDownloadWithProgress(URL, Location))
                //{
                //    client.ProgressChanged += Client_ProgressChanged;
                //    await client.StartDownload();

                //    if (File.Exists(AppInfo.AppDataDirPath + "\\dl\\tmp00.dat"))
                //    {
                //        if(new FileInfo(AppInfo.AppDataDirPath + "\\dl\\tmp00.dat").Length > 0)
                //        {
                //            return true;
                //        }
                //    }
                //}

            }
            catch (WebException ex)
            {
                LogWriter.LogWrite(ex.Message);
                if (ex != null)
                {
                    if (!string.IsNullOrEmpty(ex.Message))
                    {
                        Debug.WriteLine(ex.Message);
                    }
                }
            }
            return false;
        }

        private void Client_ProgressChanged(long? totalFileSize, long totalBytesDownloaded, double? progressPercentage)
        {
            WebClientDownloadProgressHandlerTimeOut?.Invoke(this, new DownloadFileEventArgs { BytesDownloaded = totalBytesDownloaded });
        }

        public async Task<string> DownloadStringData(string URL, string Location, bool shouldReportProgress)
        {
            //Check if internet is connected
            try
            {
                WebClient client = new WebClient();

                if (!shouldReportProgress)
                {
                    client.DownloadProgressChanged += Client_DownloadProgressChanged;
                    client.DownloadFileCompleted += Client_DownloadFileCompleted;
                }

                return await client.DownloadStringTaskAsync(URL);
            }
            catch (WebException ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite(ex.Message);
                Debug.WriteLine(ex.Message);
            }
            return "";
        }

        private void Client_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            AsyncCompletedEventArgsHandler?.Invoke(this, e);
        }

        private void Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            WebClientDownloadProgressHandler?.Invoke(this, e);
        }

    }

    public class DownloadFileEventArgs : EventArgs
    {
        public double BytesDownloaded;
    }
}
