using System;
using System.Collections.Generic;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// Resolves the exe name behind a window event's process id for the app's WinEvent hook, which fires many times a
    /// second from every process. Answers (names and failures alike) are remembered per process id, so events that
    /// alternate between a few apps don't each pay for a lookup, and a window that outlives its process (the lookup
    /// throws "Process has exited") is not retried and logged on every event: it wrote about 170 log lines a minute,
    /// 90,000 on 26 Sep. A name is trusted for <see cref="NameLifetime"/> and a failure for <see cref="FailureRetry"/>,
    /// in case Windows reuses the id for a new process.
    /// </summary>
    public sealed class ProcessNameCache
    {
        public static readonly TimeSpan FailureRetry = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan NameLifetime = TimeSpan.FromMinutes(2);
        public const int MaxEntries = 256;

        private readonly Func<int, string> _resolve;
        private readonly Func<int, DateTime?> _startTime;
        private readonly Action<string> _log;
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();

        private struct Entry
        {
            public string Name;   // null for a failed lookup
            public DateTime AtUtc;
            public DateTime? Started;   // the process's start time when resolved, if known
        }

        /// <param name="resolve">Returns the exe name ("chrome.exe") for a process id; may throw.</param>
        /// <param name="log">Where a failed lookup is reported, once per process id per retry period.</param>
        /// <param name="startTime">
        /// The process's start time for an id (null when unknown), checked on every hit: a name was trusted by id alone
        /// for <see cref="NameLifetime"/>, so an id Windows reused for a browser was still taken for the exited app.
        /// </param>
        public ProcessNameCache(Func<int, string> resolve, Action<string> log, Func<int, DateTime?> startTime = null)
        {
            _resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
            _log = log ?? (_ => { });
            _startTime = startTime;
        }

        private DateTime? StartOf(int pid)
        {
            if (_startTime == null) return null;
            try { return _startTime(pid); } catch (Exception) { return null; }
        }

        /// <summary>The exe name for <paramref name="pid"/>, or "" when there is none.</summary>
        public string NameFor(int pid, DateTime nowUtc)
        {
            if (pid == 0) return string.Empty;
            DateTime? started = StartOf(pid);
            if (_entries.TryGetValue(pid, out Entry known))
            {
                TimeSpan age = nowUtc - known.AtUtc;
                bool sameProcess = known.Started == null || started == null || known.Started == started;
                if (known.Name != null && age < NameLifetime && sameProcess) return known.Name;
                if (known.Name == null && age < FailureRetry) return string.Empty;
            }

            if (_entries.Count >= MaxEntries) _entries.Clear();
            try
            {
                string name = _resolve(pid) ?? string.Empty;
                _entries[pid] = new Entry { Name = name, AtUtc = nowUtc, Started = started };
                return name;
            }
            catch (Exception ex)
            {
                bool loggedAlready = _entries.TryGetValue(pid, out Entry previous) && previous.Name == null;
                if (!loggedAlready) _log(ex.Message);
                _entries[pid] = new Entry { Name = null, AtUtc = nowUtc };
                return string.Empty;
            }
        }
    }
}
