using System;

namespace FocusBear.Core.Startup
{
    /// <summary>
    /// What to do when the access token can't be renewed. Straight after sign-in to Windows the network is often not
    /// up yet; a failed renew used to sign the user out of Focus Bear on the spot. Now that the logon task starts the
    /// app at once instead of four minutes later (<see cref="StartupTaskPolicy.LogonDelay"/>), that would happen on
    /// most restarts, so an offline failure waits for the network and tries again.
    /// </summary>
    public static class TokenRenewRetry
    {
        /// <summary>How long to wait for a network before giving up and asking the user to sign in again.</summary>
        public static readonly TimeSpan MaxWaitForNetwork = TimeSpan.FromMinutes(5);

        /// <summary>How often to look for the network while waiting.</summary>
        public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

        /// <summary>
        /// True to wait and retry the renew; false to treat the failure as final (sign out). Only an offline failure
        /// is retried: one while online means the server refused the token.
        /// </summary>
        public static bool ShouldRetry(bool online, TimeSpan waited) => !online && waited < MaxWaitForNetwork;
    }
}
