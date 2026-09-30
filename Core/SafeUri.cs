using System;

namespace FocusBear.Core
{
    /// <summary>
    /// Uri.TryCreate, which is meant never to throw, throws IndexOutOfRangeException on some inputs (found by
    /// fuzzing: "//" followed by an RTL mark). The URLs parsed here come from address bars, calendars and typed
    /// text, so an odd one must read as "not a URL", not end the caller.
    /// </summary>
    public static class SafeUri
    {
        public static bool TryCreate(string text, UriKind kind, out Uri uri)
        {
            uri = null;
            if (text == null) return false;
            try { return Uri.TryCreate(text, kind, out uri); }
            catch (Exception) { uri = null; return false; }
        }
    }
}
