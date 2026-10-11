using Focus_Bear.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Focus_Bear.Utils
{
    public static class UploadLogs
    {
        static AppSettings appSettings = AppSettings.GetInstance;
        public static async Task <bool> UploadLogsToServer(string sFeedback)
        {
            try
            {
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

                appSettings.sUploadURL = ""; //Reinitialize
                bool bUploadFailed = false;
                //NotifySuccess/xResponse == 2 means the call failed because there was no Internet connection
                int NotifySuccess = 1;
                FocusBearAPIClient client = new FocusBearAPIClient();
                int xResponse = await client.GetUploadLogsURL();
                if (xResponse == 1)
                {
                    bool UploadSuccess = await ZipAndUploadLogs();

                    if (UploadSuccess)
                    {
                        NotifySuccess = await client.NotifyUploadLogsSuccess(sFeedback, appSettings.sUploadURL);
                        //Notify API
                        if (NotifySuccess != 1)
                        {
                            bUploadFailed = true;
                        }
                    }
                    else
                    {
                        bUploadFailed = true;
                    }
                }
                else
                {
                    bUploadFailed = true;
                }

                Mouse.OverrideCursor = null;

                if (bUploadFailed)
                {
                    //"There was some problem in uploading the logs. Please try again."
                    string sErrorMessage = appSettings.ConfigFileStrings["helpVcInfo"]["logsUploadErrorText"].ToString();

                    if (xResponse == 2 || NotifySuccess == 2)
                    {
                        try
                        {
                            // "There was a problem with your Internet connection while uploading the logs. Please check your connection and try again."
                            sErrorMessage = appSettings.ConfigFileStrings["helpVcInfo"]["logsUploadErrorConnectionText"].ToString();
                        }
                        catch (Exception ex)
                        {
                            //Older config versions may lack the key - keep the generic message
                            LogWriter.LogWrite("UploadLogs.UploadLogsToServer - " + ex.Message);
                        }
                    }

                    FBMessageBox ff = new FBMessageBox(sErrorMessage, "Focus Bear");
                    //ff.Owner = _ParentWindow;
                    ff.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    ff.ShowDialog();
                    return false;
                }
                else
                {
                    //Logs successfully shared.
                    FBMessageBox ff = new FBMessageBox(appSettings.ConfigFileStrings["helpVcInfo"]["logsUploadDoneText"].ToString(), "Focus Bear");
                    //ff.Owner = _ParentWindow;
                    ff.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    ff.ShowDialog();

                    return true;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("HelpTab btnExportLogs_Click ex -" + ex.Message);
            }
            return false;
        }
        private static async Task<bool> ZipAndUploadLogs()
        {
            try
            {
                //Zip the logs
                string zipPath = System.IO.Path.Combine(AppSettings.AppDataDirPath, "FocusBearLogs.zip");

                if (File.Exists(zipPath))
                {
                    Global.DeleteFile(zipPath, false);
                }
               
                //Zip all the logs files, better the entire log folder
                string sourceDir = AppSettings.AppDataDirLogsPath;
                LogWriter.Flush();   // the buffered lines belong in the report

                // Off the UI thread (the logs run to tens of MB), sharing write access with the log being written, and
                // leaving out - rather than failing on - a screenshot that has gone since it was picked.
                var screenshots = appSettings.droppedFiles.Select(f => f.FilePath).ToList();
                var skipped = await Task.Run(() => FocusBear.Core.Reports.LogBundle.Write(zipPath, Directory.GetFiles(sourceDir), screenshots));
                foreach (string missing in skipped) LogWriter.LogWrite("ZipAndUploadLogs() left out unreadable " + Path.GetFileName(missing));

                if (File.Exists(zipPath))
                {
                    bool bUploadDone = await UploadZipFileAsync(zipPath);
                    if (bUploadDone)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ZipAndUploadLogs() ex -" + ex.Message);
            }
            return false;
        }

        public static async Task<bool> UploadZipFileAsync(string zipPath)
        {
            try
            {
                // A presigned PUT takes the zip itself, with the content type it was signed for (application/zip), as
                // the Mac sends it. The multipart form this used to send carried a different Content-Type, and stored
                // a form envelope rather than a zip.
                using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
                using (var fileStream = new FileStream(zipPath, FileMode.Open, FileAccess.Read))
                using (var content = new StreamContent(fileStream))
                {
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");

                    HttpResponseMessage response = await client.PutAsync(appSettings.sUploadURL, content);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Upload successful!");
                        return true;
                    }
                    else
                    {
                        LogWriter.LogWrite($"UploadZipFileAsync() FAILED - {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("UploadZipFileAsync() Ex - " + ex.Message);
            }
            return false;
        }
    }
}
