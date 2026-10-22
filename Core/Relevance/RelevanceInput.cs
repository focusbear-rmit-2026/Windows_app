using System;
using System.Linq;

namespace FocusBear.Core.Relevance
{
    /// <summary>
    /// What the relevance model is asked about one page, built the way its training rows were:
    /// goal = focus intention + current task, context = page title + URL path words, title = the bare page title.
    /// </summary>
    public sealed class RelevanceInput
    {
        private const int MinPathWordLength = 3;

        private RelevanceInput(string goal, string context, string url, string title)
        {
            Goal = goal;
            Context = context;
            Url = url;
            Title = title;
        }

        public string Goal { get; }
        public string Context { get; }
        public string Url { get; }
        public string Title { get; }

        /// <summary>The input for a browser tab, or null when the URL isn't a web page or the title isn't ready.</summary>
        public static RelevanceInput ForBrowserTab(string focusIntention, string currentTask, string url, string windowTitle)
        {
            Uri page = ParseWebUrl(url);
            if (page == null || BrowserTitle.IsNotReady(page, windowTitle)) return null;
            string goal = ComposeGoal(focusIntention, currentTask);
            string videoTitle = BrowserTitle.CleanYouTubeTitle(page, windowTitle);
            if (videoTitle != null) return new RelevanceInput(goal, videoTitle, url.Trim(), videoTitle);
            string title = BrowserTitle.StripBrowserName(windowTitle);
            string context = (title + " " + PathWords(page)).Trim();
            return new RelevanceInput(goal, context, url.Trim(), title);
        }

        public static string ComposeGoal(string focusIntention, string currentTask) =>
            ((focusIntention ?? string.Empty) + " " + (currentTask ?? string.Empty)).Trim();

        /// <summary>The URL's path segments longer than two characters that aren't just digits.</summary>
        public static string PathWords(Uri url) =>
            string.Join(" ", url.AbsolutePath
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(part => part.Length >= MinPathWordLength && !part.All(char.IsDigit)));

        public static Uri ParseWebUrl(string url)
        {
            string raw = (url ?? string.Empty).Trim();
            if (raw.Length == 0) return null;
            if (!Uri.TryCreate(raw.Contains("://") ? raw : "https://" + raw, UriKind.Absolute, out Uri parsed)) return null;
            return parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps ? parsed : null;
        }
    }
}
