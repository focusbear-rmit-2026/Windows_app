using System;
using System.Threading.Tasks;

namespace FocusBear.Core.Startup
{
    /// <summary>
    /// A request started early and handed, once, to the first caller that would have made it. At start-up the app
    /// builds its main window on the UI thread (2-10 s normally, 44 s on a busy machine, report 2026-09-28_21-45-38)
    /// before the sign-in chain makes its first call; starting those calls first lets the network wait overlap the
    /// window build instead of following it.
    ///
    /// The result is handed over at most once and only while fresh: a later caller (a refresh after a purchase, a
    /// sign-in hours later) always makes its own request.
    /// </summary>
    public sealed class OneShotPrefetch<T>
    {
        private readonly object _lock = new object();
        private readonly TimeSpan _maxAge;
        private Task<T> _task;
        private DateTime _startedUtc;

        public OneShotPrefetch(TimeSpan maxAge) { _maxAge = maxAge; }

        /// <summary>Start the request, unless one is already waiting to be taken.</summary>
        public void Start(Func<Task<T>> fetch, DateTime nowUtc)
        {
            if (fetch == null) return;
            lock (_lock)
            {
                if (_task != null) return;
                try { _task = fetch(); }
                catch (Exception) { _task = null; return; }
                _startedUtc = nowUtc;
            }
        }

        /// <summary>Drop a waiting request unused (the session it was made for ended, e.g. back to the sign-in screen).</summary>
        public void Discard()
        {
            lock (_lock) _task = null;
        }

        /// <summary>True while a started request is waiting to be taken.</summary>
        public bool IsWaiting { get { lock (_lock) return _task != null; } }

        /// <summary>
        /// The early request's result if one is waiting and still fresh, else <paramref name="fetchNow"/>. The early
        /// request is used up either way. One that failed falls back to a request made now.
        /// </summary>
        public async Task<T> Take(Func<Task<T>> fetchNow, DateTime nowUtc)
        {
            Task<T> early;
            lock (_lock)
            {
                early = _task;
                bool fresh = early != null && nowUtc - _startedUtc <= _maxAge;
                _task = null;
                if (!fresh) early = null;
            }
            if (early != null)
            {
                try { return await early.ConfigureAwait(true); }
                catch (Exception) { /* fall through to a request made now */ }
            }
            return await fetchNow().ConfigureAwait(true);
        }
    }
}
