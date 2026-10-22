using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace FocusBear.Core.Relevance
{
    /// <summary>
    /// Turns a browser window title into the page title the relevance model was trained on. Window titles end with
    /// the browser's name (" - Google Chrome"); none of the 4,484 training titles do, and leaving it in moves 24 of
    /// the 762 held-out rows to a different tier (88.1% → 86.8% correct).
    /// </summary>
    public static class BrowserTitle
    {
        // Edge puts the profile between the page and its name ("… - Personal - Microsoft​ Edge", with a zero-width
        // space inside the name). Only known profile words are removed, so a real " - Gmail" segment is kept.
        private static readonly Regex BrowserSuffix = new Regex(
            @"\s+[-–—]\s+(?:(?:Personal|Work|InPrivate|Profile\s+\d+)\s+[-–—]\s+)?" +
            @"(?:Google\s+Chrome|Microsoft​?\s*Edge|Mozilla\s+Firefox|Firefox(?:\s+Developer\s+Edition|\s+Nightly)?|" +
            @"Brave|Opera|Vivaldi|Chromium|Arc|Waterfox|LibreWolf|Zen\s+Browser)" +
            @"(?:\s+\((?:Incognito|InPrivate|Private\s+Browsing)\)|\s+Private\s+Browsing)?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex YouTubeSuffix = new Regex(@"\s+[-–—]\s+YouTube\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex NotificationCounter = new Regex(@"^\s*\(\d+\)\s*");
        private static readonly Regex Whitespace = new Regex(@"\s+");

        private const string YouTubeName = "YouTube";
        private static readonly string[] LoadingTitles = { "Untitled", "New Tab" };
        private static readonly string[] YouTubeHosts = { "youtube.com", "www.youtube.com", "m.youtube.com" };
        private static readonly string[] YouTubeContentPaths = { "/watch", "/results" };

        /// <summary>The title without the browser's name (and profile) at its end, trimmed.</summary>
        public static string StripBrowserName(string windowTitle) =>
            BrowserSuffix.Replace(windowTitle ?? string.Empty, string.Empty).Trim();

        public static bool IsYouTubeContentPage(Uri url) =>
            url != null &&
            YouTubeHosts.Any(host => string.Equals(url.Host, host, StringComparison.OrdinalIgnoreCase)) &&
            YouTubeContentPaths.Any(path => string.Equals(url.AbsolutePath, path, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// The video or search title of a YouTube watch/results page, without the browser name, " - YouTube" and the
        /// "(3)" notification counter; null when the page isn't one, or its title is still a placeholder or the URL.
        /// </summary>
        public static string CleanYouTubeTitle(Uri url, string windowTitle)
        {
            if (!IsYouTubeContentPage(url)) return null;
            string clean = YouTubeSuffix.Replace(StripBrowserName(windowTitle), string.Empty);
            clean = NotificationCounter.Replace(clean, string.Empty);
            clean = Whitespace.Replace(clean, " ").Trim();
            if (clean.Length == 0 || IsPlaceholder(clean) || IsTheUrl(clean, url)) return null;
            return clean;
        }

        /// <summary>True while the tab shows a loading/blank title, or just its own URL, instead of the page title.</summary>
        public static bool IsNotReady(Uri url, string windowTitle)
        {
            string title = NotificationCounter.Replace(StripBrowserName(windowTitle), string.Empty);
            title = YouTubeSuffix.Replace(title, string.Empty).Trim();
            if (title.Length == 0 || LoadingTitles.Any(p => title.StartsWith(p, StringComparison.OrdinalIgnoreCase))) return true;
            if (IsTheUrl(title, url)) return true;
            return IsYouTubeContentPage(url) && CleanYouTubeTitle(url, windowTitle) == null;
        }

        private static bool IsPlaceholder(string title) =>
            LoadingTitles.Concat(new[] { YouTubeName }).Any(p => string.Equals(title, p, StringComparison.OrdinalIgnoreCase));

        private static bool IsTheUrl(string title, Uri url)
        {
            if (url == null || title.Any(char.IsWhiteSpace)) return false;
            if (!Uri.TryCreate(title.Contains("://") ? title : "https://" + title, UriKind.Absolute, out Uri titleUrl)) return false;
            if (titleUrl.Scheme != Uri.UriSchemeHttp && titleUrl.Scheme != Uri.UriSchemeHttps) return false;
            // Ignore only the scheme; keep path/query case and values intact.
            return string.Equals(titleUrl.Authority, url.Authority, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(titleUrl.PathAndQuery, url.PathAndQuery, StringComparison.Ordinal) &&
                string.Equals(titleUrl.Fragment, url.Fragment, StringComparison.Ordinal);
        }
    }
}
