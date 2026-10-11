using System;
using System.Collections.Generic;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// The "&lt;app&gt; isn't allowed in the &lt;focus&gt; focus mode." notice shown when a focus session
    /// hard-blocks an app, and its "Oops I actually need it" button (Mac <c>FocusBlockNotificationManager</c>).
    /// </summary>
    public static class AppBlockNotice
    {
        /// <summary>
        /// Whether the notice offers "Oops I actually need it". Same rule as the Mac and as the blocked web
        /// page, which only shows its "Oops, I actually need this" in cuddly mode (<c>cuddly_bear_mode=true</c>):
        /// the button re-presents the gentle block popup (allow / explain / set intention), and in strict
        /// (grizzly) mode there is no such popup, so offering it would be an escape hatch strict mode withholds.
        /// A teacher class session has its own "Request unlock" button instead.
        /// </summary>
        public static bool OffersNeedItButton(bool cuddlyMode, bool teacherSessionActive)
        {
            return cuddlyMode && !teacherSessionActive;
        }

        /// <summary>
        /// "Oops I actually need it": drop the app from this session's blocked list (case-insensitively, every
        /// copy), so the next time it comes to the front it is treated as fresh and goes through the gentle
        /// block popup again, with whatever gates that popup applies. Returns whether anything was removed.
        /// </summary>
        public static bool ForgetSessionBlock(List<string> sessionBlockedApps, string appName)
        {
            if (sessionBlockedApps == null || string.IsNullOrEmpty(appName)) return false;
            return sessionBlockedApps.RemoveAll(a => string.Equals(a, appName, StringComparison.OrdinalIgnoreCase)) > 0;
        }
    }
}
