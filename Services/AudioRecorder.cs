using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using Focus_Bear.Utils;
using NAudio.Wave;
using static Focus_Bear.Utils.CallDetection;
using StoppedEventArgs = NAudio.Wave.StoppedEventArgs;

namespace Focus_Bear.Services
{
    public class AudioRecorder : IDisposable
    {
        AppSettings appSettings = AppSettings.GetInstance;

        private WaveInEvent _waveSource;
        private WaveFileWriter _waveFile;

        private TaskCompletionSource<bool> _stopRecordingTcs;
        public bool IsRecording
        {
            get { return _waveSource != null; }
        }

        public AudioRecorder()
        {
            string appFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Focus Bear");

            Directory.CreateDirectory(appFolder);

            appSettings.RecordingPath = Path.Combine(appFolder, "voicecmd.wav");
        }

        public void StartRecording()
        {
            if (IsRecording)
                return;

            // Delete previous recording
            if (File.Exists(appSettings.RecordingPath))
            {
                try
                {
                    File.Delete(appSettings.RecordingPath);
                }
                catch
                {
                    // File may still be locked
                }
            }

            _waveSource = new WaveInEvent();
            _waveSource.DeviceNumber = 0; // Default microphone
            _waveSource.WaveFormat = new WaveFormat(44100, 1);

            _waveSource.DataAvailable += OnDataAvailable;
            _waveSource.RecordingStopped += OnRecordingStopped;

            _waveFile = new WaveFileWriter(
                appSettings.RecordingPath,
                _waveSource.WaveFormat);

            _waveSource.StartRecording();
        }

        public async Task StopRecording()
        {
            if (!IsRecording)
                return;

            _stopRecordingTcs = new TaskCompletionSource<bool>();

            _waveSource.StopRecording();

            await _stopRecordingTcs.Task;
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (_waveFile == null)
                return;

            _waveFile.Write(e.Buffer, 0, e.BytesRecorded);
            _waveFile.Flush();
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            if (_waveSource != null)
            {
                _waveSource.DataAvailable -= OnDataAvailable;
                _waveSource.RecordingStopped -= OnRecordingStopped;

                _waveSource.Dispose();
                _waveSource = null;
            }

            if (_waveFile != null)
            {
                _waveFile.Dispose();
                _waveFile = null;
            }
            _stopRecordingTcs?.TrySetResult(true);
        }
    
        public void Dispose()
        {
            if (IsRecording)
            {
                try
                {
                    _waveSource.StopRecording();
                }
                catch
                {
                }
            }

            if (_waveSource != null)
            {
                _waveSource.Dispose();
                _waveSource = null;
            }

            if (_waveFile != null)
            {
                _waveFile.Dispose();
                _waveFile = null;
            }
        }
    }
}
