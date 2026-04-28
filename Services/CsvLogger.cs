using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Focus_Bear.Services
{
    public class CsvLogger : IDisposable
    {
        private readonly string _filePath;
        private readonly BlockingCollection<string> _queue;
        private readonly Thread _workerThread;
        private StreamWriter _writer;

        private volatile bool _isRunning = true;

        // Optional batching
        private const int BatchSize = 10;
        private const int FlushIntervalMs = 2000;
        private bool _bFocusProductivityStats = false;
        private bool _bUsageTimingsExplanation = false;
        public CsvLogger(string filePath, bool bFocusProductivityStats = false, bool bsageTimingsExplanation = false)
        {
            _filePath = filePath;
            this._bFocusProductivityStats = bFocusProductivityStats;
            this._bUsageTimingsExplanation = bsageTimingsExplanation;

            _queue = new BlockingCollection<string>(new ConcurrentQueue<string>());

            InitWriter();

            _workerThread = new Thread(ProcessQueue)
            {
                IsBackground = true,
                Name = "CSV Logger Thread"
            };

            _workerThread.Start();

        }

        // -------------------------
        // Init
        // -------------------------
        private void InitWriter()
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            bool fileExists = File.Exists(_filePath);
            bool isEmpty = !fileExists || new FileInfo(_filePath).Length == 0;

            _writer = new StreamWriter(_filePath, true, Encoding.UTF8)
            {
                AutoFlush = false
            };

            if (isEmpty)
            {
                string headers = "Timestamp,OverallFocusAlignmentScore,IdleTimeSeconds,CurrentFocusAlignmentScore,CurrentTabCount";
                if(_bFocusProductivityStats)
                {
                    headers = "Timestamp,OverallFocusAlignmentScore,IdleTimeSeconds,CurrentFocusAlignmentScore,CurrentTabCount";
                }
                else if(_bUsageTimingsExplanation)
                {
                    headers = "start_time,end_time,tab_url_app_name,duration_seconds";
                }

                _writer.WriteLine(headers);
                _writer.Flush(); // ensure header is written immediately
            }
        }
        // -------------------------
        // Public API
        // -------------------------
        public void Append(string line)
        {
            if (!_isRunning) return;

            _queue.Add(line);
        }

        // -------------------------
        // Worker Thread
        // -------------------------
        private void ProcessQueue()
        {
            var buffer = new StringBuilder();
            var lastFlush = DateTime.UtcNow;

            while (_isRunning || _queue.Count > 0)
            {
                try
                {
                    string item;

                    if (_queue.TryTake(out item, 500))
                    {
                        buffer.AppendLine(item);
                    }

                    bool shouldFlush =
                        buffer.Length > 0 &&
                        (buffer.Length >= BatchSize * 50 || // approx size
                         (DateTime.UtcNow - lastFlush).TotalMilliseconds >= FlushIntervalMs);

                    if (shouldFlush)
                    {
                        _writer.Write(buffer.ToString());
                        buffer.Clear();
                        _writer.Flush();

                        lastFlush = DateTime.UtcNow;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("CSV Logger error: " + ex.Message);
                }
            }

            // Final flush
            if (buffer.Length > 0)
            {
                _writer.Write(buffer.ToString());
                _writer.Flush();
            }
        }

        // -------------------------
        // Shutdown
        // -------------------------
        public void Dispose()
        {
            _isRunning = false;
            _queue.CompleteAdding();

            _workerThread.Join();

            _writer?.Flush();
            _writer?.Dispose();
        }
    }
}
