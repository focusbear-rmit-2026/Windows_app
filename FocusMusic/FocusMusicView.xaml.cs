using Focus_Bear.Utils;
using Sentry.Protocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Path = System.IO.Path;

namespace Focus_Bear.FocusMusic
{
    /// <summary>
    /// Interaction logic for FocusMusic.xaml
    /// </summary>
    /// 
    public class PlayPauseTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (bool)value ? "Pause" : "Play";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && !b) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class DownloadButtonVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            bool isDownloaded = values[0] is bool b1 && b1;
            bool isDownloading = values[1] is bool b2 && b2;

            return (!isDownloaded && !isDownloading)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] types, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class PlayButtonVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            bool isDownloaded = values[0] is bool b1 && b1;
            bool isDownloading = values[1] is bool b2 && b2;

            return (isDownloaded && !isDownloading)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] types, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }


    public partial class FocusMusicView : UserControl
    {
        AppSettings appSettings = AppSettings.GetInstance;

        private MediaPlayer _player = new MediaPlayer();
        private DispatcherTimer _playbackTimer;
        private FocusMusicTrack _currentTrack;

        public FocusMusicView()
        {
            LogWriter.LogWrite("FocusMusicView() Initialized");

            InitializeComponent();

            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }

            //DataContext = this;
            ListTracks.ItemsSource = appSettings.arrFocusMusicTracks;

            _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _playbackTimer.Tick += PlaybackTimer_Tick;
            _playbackTimer.Start();  

            _player.MediaEnded += Player_MediaEnded;

            try
            {
                Heading.Text = appSettings.ConfigFileStrings["allTracksVcInfo"]["title"].ToString();
                Volume.Text = appSettings.ConfigFileStrings["allTracksVcInfo"]["titleVolumeText"].ToString();   
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("FocusMusicView texts Exception - " + ex.Message);
            }
        }

        private void Player_MediaEnded(object sender, EventArgs e)
        {
            if (_currentTrack == null) return;

            StopTrack(_currentTrack);
        }
        private void PlaybackTimer_Tick(object sender, EventArgs e)
        {
            var current = appSettings.arrFocusMusicTracks.FirstOrDefault(t => t.IsPlaying);
            if (current == null) return;
            if (!_currentTrack.IsPlaying) return;

            var elapsed = _currentTrack.PausedOffset + (DateTime.Now - _currentTrack.StartTime);
            _currentTrack.ElapsedFormatted = elapsed.ToString(@"mm\:ss");

        }

        private void ButtonPlayPause_Click(object sender, RoutedEventArgs e)
        {
            LogWriter.LogWrite("FocusMusicView() ButtonPlayPause_Click");

            var track = (sender as Button)?.DataContext as FocusMusicTrack;
            if (track == null) return;

            // User clicked a *different* track
            if (_currentTrack != track)
            {
                PlayNewTrack(track);
                return;
            }

            // same track → toggle
            if (track.IsPlaying)
                PauseTrack(track);
            else
                ResumeTrack(track);
        }

        private void PlayNewTrack(FocusMusicTrack newTrack)
        {
            // Stop old track
            if (_currentTrack != null)
                StopTrack(_currentTrack);

            _currentTrack = newTrack;

            // Reset state
            newTrack.PausedOffset = TimeSpan.Zero;
            newTrack.StartTime = DateTime.Now;

            newTrack.IsPlaying = true;
            newTrack.IsPaused = false;

            // Load the audio file (local path)
            if (!string.IsNullOrEmpty(newTrack.LocalAudioPath))
            {
                _player.Open(new Uri(newTrack.LocalAudioPath, UriKind.Absolute));
                _player.Position = TimeSpan.Zero;
                _player.Play();
            }
        }

        public void PauseTrack(FocusMusicTrack track)
        {
            if (track == null) return;

            track.IsPlaying = false;
            track.IsPaused = true;

            // Save the elapsed time so far
            track.PausedOffset += DateTime.Now - track.StartTime;

            // Pause the player
            _player.Pause();
        }


        public void ResumeTrack(FocusMusicTrack track)
        {
            if (track == null) return;

            track.StartTime = DateTime.Now;
            track.IsPlaying = true;
            track.IsPaused = false;

            // Resume from paused position
            _player.Position = track.PausedOffset;
            _player.Play();
        }


        private void StopTrack(FocusMusicTrack track)
        {
            if (track == null) return;

            track.IsPlaying = false;
            track.IsPaused = false;
            track.PausedOffset = TimeSpan.Zero;
            track.ElapsedFormatted = "00:00";

            _player.Stop();
        }



        private async void Download_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is FocusMusicTrack track)
            {
                await DownloadAudioWithProgressAsync(track);
            }
        }
     
        public async Task DownloadAudioWithProgressAsync(FocusMusicTrack track)
        {
            LogWriter.LogWrite("FocusMusicView() DownloadAudio invoked");

            if (track == null || track.IsAudioDownloaded || string.IsNullOrWhiteSpace(track.ServerAudioUrl))
                return;


            string trackFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                                        "Focus Bear", "FocusMusic", "Tracks");

            Directory.CreateDirectory(trackFolder);
            track.IsDownloading = true;
            track.DownloadProgress = 0;

            string fileName = $"{track.Id}_audio{Path.GetExtension(track.ServerAudioUrl).Split('?')[0]}";
            track.LocalAudioPath = Path.Combine(trackFolder, fileName);
            string tempFile = Path.Combine(trackFolder, "temp_" + fileName);

            // Ensure temp file doesn't exist
            if (File.Exists(tempFile))
                File.Delete(tempFile);

            track.IsDownloading = true;
            track.DownloadProgress = 0;
            long? totalBytes = null;
            try
            {
                using (var client = new HttpClient())
                using (var response = await client.GetAsync(track.ServerAudioUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    totalBytes = response.Content.Headers.ContentLength ?? -1;
                    var canReportProgress = totalBytes > 0;

                    using (var inputStream = await response.Content.ReadAsStreamAsync())
                    using (var outputStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write,FileShare.None))
                    {
                        byte[] buffer = new byte[81920];
                        int bytesRead;
                        long totalRead = 0;

                        while ((bytesRead = await inputStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await outputStream.WriteAsync(buffer, 0, bytesRead);
                            totalRead += bytesRead;

                            if (totalBytes > 0)
                            {
                                track.DownloadProgress = (int)((double)totalRead / totalBytes * 100);
                            }
                        }

                        // Force flush buffer
                        await outputStream.FlushAsync();
                    }
                }

            }
            catch (Exception ex)
            {
                track.IsDownloading = false;
                track.DownloadProgress = 0;
                track.LocalAudioPath = null;
                MessageBox.Show($"Error downloading file: {ex.Message}");
                return;
            }

            // Double-check downloaded file exists and matches size
            var fileInfo = new FileInfo(tempFile);
            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                MessageBox.Show("Download failed or corrupted file.");
                track.LocalAudioPath = null;
                track.IsDownloading = false;
                return;
            }

           
            if (totalBytes != null && totalBytes != fileInfo.Length)
            {
                MessageBox.Show("Incomplete download. Retrying...");
                track.LocalAudioPath = null;
                track.IsDownloading = false;
                return;
            }

            // Download completed safely
            track.IsDownloading = false;
            track.IsAudioDownloaded = true;
            await Task.Delay(50); // slight buffer

            // Move the temp file to final **after all streams are fully closed**
            File.Move(tempFile, track.LocalAudioPath);


        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _player.Volume = VolumeSlider.Value;
        }

        private void ChangeTheme(string theme)
        {

            var source = (HwndSource)PresentationSource.FromVisual(this);

            if (source != null)
            {
                ((App)(Application.Current)).UseImmersiveDarkMode(source.Handle, appSettings.sTheme != "Light");
            }
            if (theme == "Light")
            {
                this.Background = new SolidColorBrush(Colors.White);
            }
            else if (theme == "Dark")
            {
                this.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
            }
        }

        //private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        //{
        //    e.Cancel = true;
        //    Hide();
        //}
    }
}
