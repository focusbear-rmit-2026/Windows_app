using Focus_Bear.Utils;
using System;

namespace Focus_Bear.Services.VoiceCommand
{
    public enum VoiceCommandFailure
    {
        NotSignedIn,
        Offline,
        NothingHeard,
        TooLong,
        RateLimited,
        TimedOut,
        Failed,
    }

    // Raised by the backend voice flow so callers can tell the user WHY the command failed rather
    // than swallowing it: each reason maps to its own localised message.
    public class VoiceCommandException : Exception
    {
        public VoiceCommandFailure Reason { get; }

        public VoiceCommandException(VoiceCommandFailure reason, string detail, Exception inner = null)
            : base(detail, inner)
        {
            Reason = reason;
        }
    }

    // User-facing voice-command messages. They live in the menuView block of config.json /
    // config-es.json (shared with the Focus-Bear/assets config the app downloads at startup); the
    // English fallback covers a downloaded config that predates these keys.
    public static class VoiceCommandMessages
    {
        private const string ConfigBlock = "menuView";

        public const string SignInKey = "voiceErrorSignIn";
        public const string OfflineKey = "voiceErrorOffline";
        public const string RateLimitedKey = "voiceErrorRateLimited";
        public const string TooLongKey = "voiceErrorTooLong";
        public const string TimedOutKey = "voiceErrorTimedOut";
        public const string FailedKey = "voiceErrorFailed";
        public const string MicrophoneKey = "voiceErrorMicrophone";
        public const string NotCaughtKey = "voiceErrorNotCaught";
        public const string NotUnderstoodKey = "voiceErrorNotUnderstood";

        public static string ForFailure(VoiceCommandFailure reason)
        {
            switch (reason)
            {
                case VoiceCommandFailure.NotSignedIn:
                    return Get(SignInKey, "Sign in to Focus Bear to use voice commands.");
                case VoiceCommandFailure.Offline:
                    return Get(OfflineKey, "Couldn't reach Focus Bear. Check your internet connection and try again.");
                case VoiceCommandFailure.NothingHeard:
                    return NotCaught();
                case VoiceCommandFailure.TooLong:
                    return Get(TooLongKey, "That recording is too long. Keep voice commands short and try again.");
                case VoiceCommandFailure.RateLimited:
                    return Get(RateLimitedKey, "That's a lot of voice commands in a row. Wait a minute, then try again.");
                case VoiceCommandFailure.TimedOut:
                    return Get(TimedOutKey, "Your voice command is taking too long. Please try again.");
                default:
                    return Failed();
            }
        }

        public static string Failed()
        {
            return Get(FailedKey, "Couldn't process your voice command. Please try again.");
        }

        public static string Microphone()
        {
            return Get(MicrophoneKey, "Couldn't access the microphone.");
        }

        public static string NotCaught()
        {
            return Get(NotCaughtKey, "Sorry, I didn't catch that. Please try again.");
        }

        public static string NotUnderstood()
        {
            return Get(NotUnderstoodKey, "Sorry, I didn't understand that. Please try again.");
        }

        private static string Get(string key, string englishFallback)
        {
            return AppSettings.GetInstance.GetConfigStringSafe(englishFallback, ConfigBlock, key);
        }
    }
}
