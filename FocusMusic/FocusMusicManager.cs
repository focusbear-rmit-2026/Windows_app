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

        /// <summary>
        /// The track catalogue from <c>GET /tracks</c> (or the copy saved in the registry), into
        /// <see cref="AppSettings.arrFocusMusicTracks"/>. Runs from the post-login work on a pool thread, so the
        /// collection — bound to the Focus Music screen and read by the tray — is only touched on the UI thread,
        /// and each run REPLACES the catalogue rather than appending another copy of it.
        /// </summary>
        public static async void ReadAndParseFocusMusicTracks()
        {
            LogWriter.LogWrite("ReadAndParseFocusMusicTracks()");
            IList<FocusMusicTrack> fresh = null;
            try
            {
                //Check is settings are there
                if (appSettings.sRawFocusMusicTracks == null || appSettings.sRawFocusMusicTracks.Length == 0)
                {
                    LogWriter.LogWrite("sRawFocusMusicTracks == null - Read previously saved data from registry");
                    //Read previously saved in registry
                    RegistryManager.GetSavedFocusMusicTracks();
                }
                fresh = ParseTracks(appSettings.sRawFocusMusicTracks, DownloadButtonText());
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ReadAndParseFocusMusicTracks() top ex -" + ex.Message);
            }
            if (fresh == null) return;

            await OnUiThreadAsync(() => MergeInto(appSettings.arrFocusMusicTracks, fresh));
            await PopulateAndDownloadImagesAsync();
        }

        private static string DownloadButtonText()
        {
            try { return appSettings.ConfigFileStrings?["allTracksVcInfo"]?["downloadButtonText"]?.ToString(); }
            catch (Exception) { return null; }
        }

        /// <summary>
        /// Runs <paramref name="action"/> on the UI thread and completes after it. Awaited rather than a blocking
        /// Dispatcher.Invoke from the pool thread, which would deadlock a UI thread waiting on this work.
        /// </summary>
        internal static Task OnUiThreadAsync(Action action, System.Windows.Threading.Dispatcher dispatcher = null)
        {
            dispatcher = dispatcher ?? System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }
            return dispatcher.InvokeAsync(action).Task;
        }

        /// <summary>
        /// The tracks in <paramref name="raw"/>; null when it is not a JSON array. A row that cannot be read is
        /// skipped, never the whole list.
        /// </summary>
        internal static IList<FocusMusicTrack> ParseTracks(string raw, string downloadButtonText)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            JArray rows;
            try { rows = JArray.Parse(raw); }
            catch (Exception ex) { LogWriter.LogWrite("ReadAndParseFocusMusicTracks() parse ex - " + ex.Message); return null; }
            var tracks = new List<FocusMusicTrack>();
            var seen = new HashSet<string>();
            foreach (var item in rows.OfType<JObject>())
            {
                try
                {
                    string id = item.ContainsKey("id") ? item.GetValue("id").ToString() : "";
                    if (id.Length == 0 || !seen.Add(id)) continue;
                    string name = item.ContainsKey("name") ? item.GetValue("name").ToString() : "";
                    string downloadUrl = item.ContainsKey("download_url") ? item.GetValue("download_url").ToString() : "";
                    string thumbnailDownloadUrl = item.ContainsKey("thumbnail_download_url") ? item.GetValue("thumbnail_download_url").ToString() : "";
                    string duration = item.ContainsKey("duration") ? item.GetValue("duration").ToString() : "";
                    int.TryParse(duration, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int seconds);
                    var track = new FocusMusicTrack(id, name, downloadUrl, thumbnailDownloadUrl, duration, appSettings.SecondsToTextHMS(seconds));
                    if (downloadButtonText != null) track.DownloadButtonText = downloadButtonText;
                    tracks.Add(track);
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("ReadAndParseFocusMusicTracks() Reading item in arrFocusMusicTracks failed ex = " + ex.Message);
                }
            }
            return tracks;
        }

        /// <summary>
        /// Brings <paramref name="target"/> to <paramref name="fresh"/>: a track already there keeps its object —
        /// and with it the playing / paused state and the download in progress — and takes the new name and
        /// (short-lived) URLs; new tracks are added in order; a track the server no longer lists goes, unless it
        /// is the one playing.
        /// </summary>
        internal static void MergeInto(System.Collections.ObjectModel.ObservableCollection<FocusMusicTrack> target, IList<FocusMusicTrack> fresh)
        {
            var keep = new HashSet<string>(fresh.Select(t => t.Id));
            var byId = new Dictionary<string, FocusMusicTrack>();
            foreach (var track in target.ToList())
            {
                bool duplicate = track == null || track.Id == null || byId.ContainsKey(track.Id);
                bool gone = !duplicate && !keep.Contains(track.Id) && !track.IsPlaying && !track.IsPaused;
                if (duplicate || gone) { target.Remove(track); continue; }
                byId[track.Id] = track;
            }
            foreach (var track in fresh)
            {
                if (byId.TryGetValue(track.Id, out var existing))
                {
                    existing.Name = track.Name;
                    existing.ServerAudioUrl = track.ServerAudioUrl;
                    existing.ServerImageUrl = track.ServerImageUrl;
                    existing.Duration = track.Duration;
                    existing.DurationFormatted = track.DurationFormatted;
                    if (track.DownloadButtonText != null) existing.DownloadButtonText = track.DownloadButtonText;
                }
                else target.Add(track);
            }
        }

        public static async Task PopulateAndDownloadImagesAsync()
        {
            string imageFolder = Path.Combine(  Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                                        FocusBear.Core.AppInstance.DataFolderName, "FocusMusic", "Images");

            Directory.CreateDirectory(imageFolder);

            // A snapshot: the catalogue may be replaced while the downloads are awaited.
            foreach (var track in appSettings.arrFocusMusicTracks.ToList())
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
