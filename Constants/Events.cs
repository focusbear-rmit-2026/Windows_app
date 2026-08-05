

namespace Focus_Bear.Constants
{
    static class Events
    {
         public const string WindowClosing = "window_closing_event";

        // Emitted whenever the app clears the signed-in session (analytics#33). The `reason`
        // property lets analytics separate a deliberate sign-out from an involuntary session
        // loss (expired/rejected token), which was previously indistinguishable on Windows.
        public const string Logout = "logout";

        // Outcome of the onboarding "hold on a minute" interstitial (analytics#33). On Windows
        // this screen is a static gate before habit setup — the post-login load has already
        // finished by the time it shows — so only the "user proceeded" outcome can fire here
        // (there is no load to time out or error).
        public const string HoldOnScreenSucceeded = "holdon-screen-succeeded";

        // Allowed values for the Logout event's `reason` property.
        public static class LogoutReason
        {
            public const string UserInitiated = "user_initiated";
            public const string TokenExpired = "token_expired";
            public const string RefreshFailed = "refresh_failed";
            public const string AuthError = "auth_error";
        }

        // Allowed values for the `auth_state` property on view-signin-shown / view-signup-shown
        // (analytics#33) — separates guest→account conversion from involuntary re-auth.
        public static class AuthState
        {
            public const string Guest = "guest";
            public const string Registered = "registered";
        }
    }
}
