using System;

namespace FocusBear.Core.Updates
{
    /// <summary>
    /// Version comparisons for the app and Focus Button updates. System.Version treats a missing component as -1,
    /// so 1.6 &lt; 1.6.0 and 3.0.249 &lt; 3.0.249.0: here a missing component counts as 0.
    /// </summary>
    public static class VersionCompare
    {
        /// <summary><paramref name="candidate"/> is newer than <paramref name="current"/>; anything beats an unknown current version.</summary>
        public static bool IsNewer(Version candidate, Version current)
        {
            if (candidate == null) return false;
            if (current == null) return true;
            return Normalize(candidate) > Normalize(current);
        }

        /// <summary><paramref name="version"/> is at least <paramref name="minimum"/>; an unknown version is not.</summary>
        public static bool AtLeast(Version version, Version minimum)
        {
            if (version == null) return false;
            if (minimum == null) return true;
            return Normalize(version) >= Normalize(minimum);
        }

        /// <summary>A version from an INI or device field ("3.0.248.1", " v1.8.1\r"); null when it is not one.</summary>
        public static Version Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string t = text.Trim();
            if (t.StartsWith("v", StringComparison.OrdinalIgnoreCase)) t = t.Substring(1);
            return Version.TryParse(t, out Version v) ? v : null;
        }

        private static Version Normalize(Version v) =>
            new Version(Math.Max(0, v.Major), Math.Max(0, v.Minor), Math.Max(0, v.Build), Math.Max(0, v.Revision));
    }
}
