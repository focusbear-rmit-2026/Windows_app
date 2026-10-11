using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.Focus
{
    /// <summary>
    /// Whether the 5 s call check (every microphone session, then the webcam consent registry) is worth doing.
    /// Both halves only report a call when the app using the microphone or camera is on the Meetings focus mode's
    /// allowed apps, so with the setting off, or no Meetings mode, or one that allows no apps, the check can never
    /// say yes: skip it (it enumerated the audio sessions and scanned dozens of registry keys every 5 s for nothing).
    /// </summary>
    public static class CallDetectionPolicy
    {
        /// <param name="meetingModeExeNames">The exe names the Meetings focus mode allows, or null when there is no such mode.</param>
        public static bool ShouldCheck(bool detectionEnabled, IEnumerable<string> meetingModeExeNames) =>
            detectionEnabled && meetingModeExeNames != null && meetingModeExeNames.Any(n => !string.IsNullOrWhiteSpace(n));
    }
}
