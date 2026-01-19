using Focus_Bear.Models;
using Focus_Bear.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Focus_Bear.FocusMusic
{
    public static class FocusMusicManager
    {
        private static AppSettings appSettings = AppSettings.GetInstance;

        public static async void ReadAndParseFocusMusicTracks()
        {
            LogWriter.LogWrite("ReadAndParseFocusMusicTracks()");
            try
            {
                //Check is settings are there
                if (appSettings.sRawFocusMusicTracks == null || appSettings.sRawFocusMusicTracks.Length == 0)
                {
                    LogWriter.LogWrite("sRawFocusMusicTracks == null - Read previously saved data from registry");
                    //Read previously saved in registry
                    RegistryManager.GetSavedFocusMusicTracks();
                }
                if (appSettings.sRawFocusMusicTracks.Length > 0)
                {

                    //Parse the JSON string
                    try
                    {
                        JArray MusicTracks = JArray.Parse(appSettings.sRawFocusMusicTracks);

                        if (MusicTracks != null && MusicTracks.Count() > 0)
                        {
                            foreach (JObject item in MusicTracks)
                            {


                                try
                                {
                                    string id = item.ContainsKey("id") ? item.GetValue("id").ToString() : "";
                                    string name = item.ContainsKey("name") ? item.GetValue("name").ToString() : "";
                                    string downloadUrl = item.ContainsKey("download_url") ? item.GetValue("download_url").ToString() : "";
                                    string thumbnailDownloadUrl = item.ContainsKey("thumbnail_download_url") ? item.GetValue("thumbnail_download_url").ToString() : "";
                                    string duration = item.ContainsKey("duration") ? item.GetValue("duration").ToString() : "";
                                    string durationFormatted = appSettings.SecondsToTextHMS(Convert.ToInt32(duration));

                                    var track = new FocusMusicTrack(id, name, downloadUrl, thumbnailDownloadUrl, duration, durationFormatted);
                                    track.DownloadButtonText = appSettings.ConfigFileStrings["allTracksVcInfo"]["downloadButtonText"].ToString();
                                    appSettings.arrFocusMusicTracks.Add(track);
                                }
                                catch (Exception ex)
                                {
                                    LogWriter.LogWrite("ReadAndParseUserDetailsJson() Reading item in BlockingSchedules failed ex = " + ex.Message);
                                    continue; //Even if one focus mode have failed we will read the next one
                                }
                            }

                        }

                    }
                    catch (Exception ex)
                    {
                        LogWriter.LogWrite("ReadAndParseBlockingSchedules() ex - " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ReadAndParseBlockingSchedules() top ex -" + ex.Message);
            }

            await PopulateAndDownloadImagesAsync();
        }

        public static async Task PopulateAndDownloadImagesAsync()
        {
            string imageFolder = Path.Combine(  Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                                        "Focus Bear", "FocusMusic", "Images");

            Directory.CreateDirectory(imageFolder);

            foreach (var track in appSettings.arrFocusMusicTracks)
            {
                if (string.IsNullOrWhiteSpace(track.ServerImageUrl))
                    continue;

                string fileName = $"{track.Id}_image{Path.GetExtension(track.ServerImageUrl).Split('?')[0]}";
                string fullPath = Path.Combine(imageFolder, fileName);

                if (!File.Exists(fullPath))
                {
                    try
                    {
                        HttpCommunication http = new HttpCommunication();
                        if (await http.Download(track.ServerImageUrl, fullPath, false))
                        {
                            LogWriter.LogWrite(track.ServerImageUrl + "Download Success");
                        }

                    }
                    catch
                    {
                        // handle/log error, keep LocalImagePath null so UI can show placeholder
                        LogWriter.LogWrite(track.ServerImageUrl + "Download Failed.");
                        continue;
                    }
                }

                track.LocalImagePath = fullPath;
                track.IsImageDownloaded = true;
            }
        }
    }
}
