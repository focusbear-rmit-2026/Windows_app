using System;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// The pure rules behind the AI block check-in (the Mac's <c>AiBlockBuddyScreen</c>): how the caught
    /// item is labelled, how long an argued-for item may be used, and when an argument counts as relevant.
    /// </summary>
    public static class AiBlockBuddy
    {
        public const int UrlLabelMaxLength = 40;
        public const int MinAllowMinutes = 1;
        public const int MaxAllowMinutes = 120;
        public const int DefaultAllowMinutes = 15;

        /// <summary>After an argument, the AI's probability at or above which the item may be used for a while.</summary>
        public const double RelevantProbability = 0.5;

        /// <summary>
        /// A short readable URL: scheme, query and fragment dropped, at most 40 characters, cut at a path
        /// boundary with "…" — so a giant checkout link reads <c>checkout.stripe.com/c/pay/…</c>.
        /// </summary>
        public static string TruncatedUrl(string rawUrl)
        {
            string s = (rawUrl ?? "").Trim();
            foreach (string scheme in new[] { "https://", "http://" })
                if (s.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)) { s = s.Substring(scheme.Length); break; }
            int cut = s.IndexOfAny(new[] { '?', '#' });
            if (cut >= 0) s = s.Substring(0, cut);
            s = s.TrimEnd('/');
            if (s.Length <= UrlLabelMaxLength) return s;
            string clipped = s.Substring(0, UrlLabelMaxLength);
            int slash = clipped.LastIndexOf('/');
            return slash > 0 ? clipped.Substring(0, slash + 1) + "…" : clipped + "…";
        }

        /// <summary>An app keeps its name; a page is "Title (short.url/path)", or just the short URL without a title.</summary>
        public static string DisplayLabel(bool isApp, string appName, string url, string pageTitle)
        {
            if (isApp) return (appName ?? "").Trim();
            string shortUrl = TruncatedUrl(url);
            string title = (pageTitle ?? "").Trim();
            return title.Length == 0 ? shortUrl : title + " (" + shortUrl + ")";
        }

        public static int ClampAllowMinutes(int minutes) => Math.Min(MaxAllowMinutes, Math.Max(MinAllowMinutes, minutes));

        public static bool IsRelevant(double? probability) => probability.HasValue && probability.Value >= RelevantProbability;

        /// <summary>"82%" for the (i) detail; empty when there is no score.</summary>
        public static string ScoreLabel(double? probability) =>
            probability.HasValue && probability.Value >= 0 ? ((int)Math.Round(Math.Min(1, probability.Value) * 100)) + "%" : "";

        /// <summary>
        /// Grace after the check-in appears during which Focus Bear's own main window coming forward doesn't
        /// count as leaving (Mac <c>focusBearActivationGrace</c>): presenting the prompt can itself briefly
        /// bring the app forward.
        /// </summary>
        public static readonly TimeSpan OwnWindowGrace = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Whether the user has moved off the app or page the check-in is about, so it should go away - no block,
        /// no allow (Mac <c>AiBlockBuddyDismissObserver</c>). They have left when:
        /// <list type="bullet">
        /// <item>a page check-in's browser is no longer on that page;</item>
        /// <item>Focus Bear's own main window came to the front (not the check-in or its other windows), after
        /// <see cref="OwnWindowGrace"/>;</item>
        /// <item>a different app is in front than the one the check-in is about (the blocked app, or the page's
        /// browser).</item>
        /// </list>
        /// The shell (taskbar, Start, Snipping Tool...) and anything unreadable never count, so taking a screenshot
        /// of the check-in doesn't dismiss it.
        /// </summary>
        public static bool HasLeftItem(ForegroundKind foreground, string foregroundProcess, string itemProcess,
                                       bool isApp, bool stillOnPage, TimeSpan sinceShown)
        {
            if (!isApp && !stillOnPage) return true;
            switch (foreground)
            {
                case ForegroundKind.OwnMainWindow:
                    return sinceShown >= OwnWindowGrace;
                case ForegroundKind.OtherApp:
                    return !string.IsNullOrEmpty(itemProcess) && !string.IsNullOrEmpty(foregroundProcess)
                        && !string.Equals(foregroundProcess, itemProcess, StringComparison.OrdinalIgnoreCase);
                default:
                    return false;
            }
        }

        private static readonly string[] ShellProcesses =
        {
            "explorer", "shellexperiencehost", "startmenuexperiencehost", "searchhost", "searchui", "searchapp",
            "textinputhost", "shellhost", "lockapp", "snippingtool", "screenclippinghost", "applicationframehost",
            "csrss", "dwm", "idle", "openwith",
        };

        /// <summary>
        /// Windows' own shell processes - the taskbar, Start, search, the touch keyboard, Snipping Tool - which
        /// the user passes through without having left the blocked app.
        /// </summary>
        public static bool IsShellProcess(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return false;
            foreach (string shell in ShellProcesses)
            {
                if (string.Equals(shell, processName, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>
        /// The key a check-in is for: one per app or page. A new check-in for a different key replaces the one on
        /// screen instead of being dropped (or stacking); the same key leaves it alone.
        /// </summary>
        public static string ItemKey(bool isApp, string appName, string url) =>
            (isApp ? "app:" + (appName ?? "") : "url:" + (url ?? "")).ToLowerInvariant();
    }

    /// <summary>What is in front while a check-in is up.</summary>
    public enum ForegroundKind
    {
        /// <summary>Couldn't tell.</summary>
        Unknown,
        /// <summary>The check-in itself, its backdrop, or another Focus Bear popup or dialog.</summary>
        OwnPopup,
        /// <summary>Focus Bear's main window (Day Plan, the focus screen, full screen or not).</summary>
        OwnMainWindow,
        /// <summary>The taskbar, Start, search, Snipping Tool and other system windows.</summary>
        SystemShell,
        /// <summary>Any other app.</summary>
        OtherApp,
    }
}
