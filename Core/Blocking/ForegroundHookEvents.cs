using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// The window events the app's out-of-context WinEvent hook asks Windows for. The hook used to take the whole
    /// 0x8005-0x800E range, so every state, name and location change of every window on the machine (a browser tab's
    /// spinner, a clock, a moving mouse) was posted to the app's UI thread: about 125 a second on a busy desktop,
    /// of which 1% were the events anything acts on. Each one ran the handler (process lookups, and for a browser a
    /// cross-process accessibility call). Only three kinds are used: a new foreground window and a focus change (an
    /// app switch hides the set-intention popup; a browser's schedules a tab check) and a value change (the address
    /// bar's URL, for blocking).
    /// </summary>
    public static class ForegroundHookEvents
    {
        public const uint SystemForeground = 0x0003;
        public const uint ObjectFocus = 0x8005;
        public const uint ObjectStateChange = 0x800A;
        public const uint ObjectLocationChange = 0x800B;
        public const uint ObjectNameChange = 0x800C;
        public const uint ObjectValueChange = 0x800E;

        /// <summary>Each (min, max) pair is one SetWinEventHook call.</summary>
        public static readonly IReadOnlyList<KeyValuePair<uint, uint>> Ranges = new[]
        {
            new KeyValuePair<uint, uint>(SystemForeground, SystemForeground),
            new KeyValuePair<uint, uint>(ObjectFocus, ObjectFocus),
            new KeyValuePair<uint, uint>(ObjectValueChange, ObjectValueChange),
        };

        public static bool IsHooked(uint eventType) => Ranges.Any(r => eventType >= r.Key && eventType <= r.Value);

        /// <summary>Only a value change carries the address bar's URL.</summary>
        public static bool CarriesUrl(uint eventType) => eventType == ObjectValueChange;
    }
}
