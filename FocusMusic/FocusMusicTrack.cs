using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Focus_Bear.FocusMusic
{
    public class FocusMusicTrack : INotifyPropertyChanged
    {
        // identity / meta
        public string Id { get; set; }
        public string Name { get; set; }

        // remote (from server / API)
        public string ServerImageUrl { get; set; }
        public string ServerAudioUrl { get; set; }

        // local cached paths (after download)
        private string _localImagePath;
        public string LocalImagePath
        {
            get => _localImagePath;
            set { _localImagePath = value; OnPropertyChanged(nameof(LocalImagePath)); }
        }

        private string _localAudioPath;
        public string LocalAudioPath
        {
            get => _localAudioPath;
            set { _localAudioPath = value; OnPropertyChanged(nameof(LocalAudioPath)); }
        }

        // audio/duration
        public string Duration { get; set; }

        // state flags & progress
        private bool _isImageDownloaded;
        public bool IsImageDownloaded
        {
            get => _isImageDownloaded;
            set { _isImageDownloaded = value; OnPropertyChanged(nameof(IsImageDownloaded)); }
        }

        private bool _isAudioDownloaded;
        public bool IsAudioDownloaded
        {
            get => _isAudioDownloaded;
            set { _isAudioDownloaded = value; OnPropertyChanged(nameof(IsAudioDownloaded)); }
        }

        private bool _isDownloading;
        public bool IsDownloading
        {
            get => _isDownloading;
            set { _isDownloading = value; OnPropertyChanged(nameof(IsDownloading)); }
        }

        private int _downloadProgress;
        public int DownloadProgress
        {
            get => _downloadProgress;
            set { _downloadProgress = value; OnPropertyChanged(nameof(DownloadProgress)); }
        }

        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            set { _isPlaying = value; OnPropertyChanged(nameof(IsPlaying)); }
        }

        private bool _isPaused;
        public bool IsPaused
        {
            get => _isPaused;
            set { _isPaused = value; OnPropertyChanged(nameof(IsPaused)); }
        }

        private TimeSpan _pausedOffset = TimeSpan.Zero;
        public TimeSpan PausedOffset
        {
            get => _pausedOffset;
            set { _pausedOffset = value; OnPropertyChanged(nameof(PausedOffset)); }
        }

        private string _downloadButtonText;
        public string DownloadButtonText { get => _downloadButtonText; set { _downloadButtonText = value; OnPropertyChanged(nameof(DownloadButtonText)); } }

        public DateTime StartTime { get; set; }
        // convenience / formatted values
        public string DurationFormatted { get; set; } = "00:00"; 
       
        private string _elapsedFormatted;
        public string ElapsedFormatted // update while playing
        {
            get => _elapsedFormatted;
            set
            {
                if (_elapsedFormatted != value)
                {
                    _elapsedFormatted = value;
                    OnPropertyChanged(nameof(ElapsedFormatted));
                }
            }
        }

        // INotify
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        string imageFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                                        FocusBear.Core.AppInstance.DataFolderName, "FocusMusic", "Images");

        string trackFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                                        FocusBear.Core.AppInstance.DataFolderName, "FocusMusic", "Tracks");
        public FocusMusicTrack(string Id, string Name, string ServerAudioUrl, string ServerImageUrl, string Duration, string DurationFormatted)
        {
            this.Id = Id;
            this.Name = Name;
            this.ServerAudioUrl = ServerAudioUrl;
            this.ServerImageUrl = ServerImageUrl;
            this.Duration = Duration;
            this.DurationFormatted = DurationFormatted;
            Directory.CreateDirectory(imageFolder);
            Directory.CreateDirectory(trackFolder);

            string fileName = $"{Id}_audio{Path.GetExtension(ServerAudioUrl).Split('?')[0]}";
            string destPath = Path.Combine(trackFolder, fileName);
            if (File.Exists(destPath))
            {
                this.IsAudioDownloaded = true;
                this.LocalAudioPath = destPath;
            }
        }
    }

}
