using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FocusBear.Core.Auth
{
    /// <summary>
    /// The rules around renewing the Auth0 access token, apart from Auth0 itself.
    ///
    /// Every API call checks the token first and renews it when it has expired, and at launch, or when a sweep
    /// runs, a dozen calls do so in the same second. Each used to start its OWN refresh with the same refresh
    /// token: with refresh-token rotation that is exactly what Auth0's reuse detection revokes (every token in
    /// the family, i.e. a sign-out), and without rotation the answers race to overwrite each other.
    /// </summary>
    public sealed class TokenRefreshGate
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, Task<bool>> _inFlight = new Dictionary<string, Task<bool>>(StringComparer.Ordinal);

        /// <summary>
        /// Runs <paramref name="refresh"/> for <paramref name="refreshToken"/>, or joins the refresh of the same
        /// token already under way. A finished refresh is forgotten, so the next expiry refreshes again.
        /// </summary>
        public Task<bool> RunAsync(string refreshToken, Func<Task<bool>> refresh)
        {
            if (refresh == null) throw new ArgumentNullException(nameof(refresh));
            string key = refreshToken ?? string.Empty;
            lock (_lock)
            {
                if (_inFlight.TryGetValue(key, out Task<bool> running)) return running;
                Task<bool> started = RunAndForget(key, refresh);
                // A refresh that completed synchronously has already removed itself; do not re-add it.
                if (!started.IsCompleted) _inFlight[key] = started;
                return started;
            }
        }

        /// <summary>
        /// A refresh that must be NEW (after a purchase, to pick up the claims it changed): it waits for any refresh
        /// under way - joining that one returned an answer fetched before the purchase - then refreshes with the
        /// refresh token as it stands then (the one before may have rotated it).
        /// </summary>
        public async Task<bool> RunForcedAsync(Func<string> currentRefreshToken, Func<string, Task<bool>> refresh)
        {
            if (currentRefreshToken == null) throw new ArgumentNullException(nameof(currentRefreshToken));
            if (refresh == null) throw new ArgumentNullException(nameof(refresh));
            Task[] running;
            lock (_lock) running = new List<Task>(_inFlight.Values).ToArray();
            if (running.Length > 0)
            {
                try { await Task.WhenAll(running).ConfigureAwait(false); }
                catch (Exception) { /* its failure is its callers' answer; this one refreshes anyway */ }
            }
            string token = currentRefreshToken();
            return await RunAsync(token, () => refresh(token)).ConfigureAwait(false);
        }

        private async Task<bool> RunAndForget(string key, Func<Task<bool>> refresh)
        {
            try
            {
                return await refresh().ConfigureAwait(false);
            }
            finally
            {
                lock (_lock) _inFlight.Remove(key);
            }
        }

        /// <summary>
        /// The refresh token to keep after a successful refresh. Auth0 only sends a new one when rotation is on;
        /// when the answer has none, the one that was used is still the valid one. Storing the empty answer left
        /// the app with no refresh token at all ("Missing required parameter: refresh_token") and every later
        /// renewal failed.
        /// </summary>
        public static string RefreshTokenToKeep(string previous, string received)
        {
            return string.IsNullOrWhiteSpace(received) ? previous : received;
        }
    }
}
