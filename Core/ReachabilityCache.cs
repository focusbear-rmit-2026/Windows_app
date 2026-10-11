using System;

namespace FocusBear.Core
{
    /// <summary>
    /// How long the app trusts its last "can I reach the API?" probe. Every API call used to probe first: an extra
    /// GET to /api on a new HttpClient, after enumerating every network adapter on the calling (often UI) thread.
    /// A reachable answer is reused for <see cref="ReachableFor"/>; an unreachable one only for
    /// <see cref="UnreachableFor"/>, so a blip (the 4 s probe timing out, 26 Sep 17:16, which emptied the Day Plan's
    /// meetings) is retried almost at once rather than failing every call for half a minute.
    /// </summary>
    public sealed class ReachabilityCache
    {
        public static readonly TimeSpan ReachableFor = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan UnreachableFor = TimeSpan.FromSeconds(3);

        private readonly object _lock = new object();
        private bool _known;
        private bool _reachable;
        private DateTime _atUtc;

        /// <summary>The cached answer, or null when there is none or it is too old to use.</summary>
        public bool? Cached(DateTime nowUtc)
        {
            lock (_lock)
            {
                if (!_known) return null;
                TimeSpan age = nowUtc - _atUtc;
                if (age < TimeSpan.Zero) return null;   // the clock went back: ask again
                return age < (_reachable ? ReachableFor : UnreachableFor) ? _reachable : (bool?)null;
            }
        }

        public void Record(bool reachable, DateTime nowUtc)
        {
            lock (_lock)
            {
                _known = true;
                _reachable = reachable;
                _atUtc = nowUtc;
            }
        }

        /// <summary>A request that failed on the network proves the cached "reachable" wrong: probe on the next call.</summary>
        public void Forget()
        {
            lock (_lock) _known = false;
        }
    }
}
