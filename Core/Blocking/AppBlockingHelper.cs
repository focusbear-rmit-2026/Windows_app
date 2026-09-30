using System;
using System.Collections.Generic;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// The Mac's <c>AppBlockingHelper</c>. App lists are stored as bundle PATHS
    /// ("/Applications/Slack.app") while the running app is known by NAME ("Slack"), so every
    /// membership test reduces a path to its last component minus the extension and compares
    /// case-insensitively.
    /// </summary>
    public static class AppBlockingHelper
    {
        public static bool IsAppInAlwaysAllowedList(string appName, IEnumerable<string> alwaysAllowedApps)
        {
            return ContainsApp(appName, alwaysAllowedApps);
        }

        public static bool IsAppInBlockedList(string appName, IEnumerable<string> blockedApps)
        {
            return ContainsApp(appName, blockedApps);
        }

        public static bool IsAppInAllowedList(string appName, IEnumerable<string> allowedApps)
        {
            return ContainsApp(appName, allowedApps);
        }

        /// <summary>
        /// The decision, in the Mac's order — the order IS the precedence:
        /// always-allowed beats everything, then the block list (so it beats "allow all apps in
        /// focus" and the per-mode allow list), then "allow all apps", then the built-in
        /// default-allowed names, then the per-mode allow list. Anything unlisted is blocked.
        /// </summary>
        public static bool ShouldBlockApp(string appName, IEnumerable<string> alwaysAllowedApps,
                                          IEnumerable<string> blockedApps, IEnumerable<string> allowedApps,
                                          bool isAllowedAllAppsInFocus, IEnumerable<string> defaultAllowedApps)
        {
            if (appName == null) throw new ArgumentNullException(nameof(appName));
            string normalizedAppName = appName.ToLowerInvariant();

            if (IsAppInAlwaysAllowedList(normalizedAppName, alwaysAllowedApps)) return false;
            if (IsAppInBlockedList(normalizedAppName, blockedApps)) return true;
            if (isAllowedAllAppsInFocus) return false;

            // Exact, case-SENSITIVE containment against the already-lowercased name — unlike every
            // other list here. An entry spelled "Finder" therefore never matches and the app is
            // blocked, so the default-allowed table must be written in lower case.
            if (defaultAllowedApps != null)
            {
                foreach (string entry in defaultAllowedApps)
                {
                    if (string.Equals(entry, normalizedAppName, StringComparison.Ordinal)) return false;
                }
            }

            if (IsAppInAllowedList(normalizedAppName, allowedApps)) return false;
            return true;
        }

        private static bool ContainsApp(string appName, IEnumerable<string> appPaths)
        {
            if (appName == null) throw new ArgumentNullException(nameof(appName));
            if (appPaths == null) return false;
            string normalizedAppName = appName.ToLowerInvariant();
            foreach (string path in appPaths)
            {
                if (path == null) continue;
                if (AppNameFromPath(path).ToLowerInvariant() == normalizedAppName) return true;
            }
            return false;
        }

        /// <summary>
        /// The Mac's <c>URL(fileURLWithPath:).deletingPathExtension().lastPathComponent</c>.
        ///
        /// Split on "/" only — these are macOS bundle paths, and using the host platform's separator
        /// would make the same block list behave differently on Windows. A name that is nothing but
        /// an extension (".hidden") keeps its dot, matching <c>deletingPathExtension</c>.
        /// </summary>
        internal static string AppNameFromPath(string path)
        {
            string trimmed = path.TrimEnd('/');
            int lastSlash = trimmed.LastIndexOf('/');
            string lastComponent = lastSlash >= 0 ? trimmed.Substring(lastSlash + 1) : trimmed;
            int lastDot = lastComponent.LastIndexOf('.');
            return lastDot > 0 ? lastComponent.Substring(0, lastDot) : lastComponent;
        }
    }
}
