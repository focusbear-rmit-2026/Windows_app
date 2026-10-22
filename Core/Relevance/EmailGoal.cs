using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FocusBear.Core.Relevance
{
    /// <summary>
    /// A mail inbox is the work itself when the goal is about email or support ("Reply to customer support emails").
    /// The training set has no mail pages at all, and its feed/messaging flag fires on "inbox", so the model blocked
    /// Gmail for exactly those goals. For such a goal a mail page is neither flagged as a feed nor blocked.
    /// </summary>
    public static class EmailGoal
    {
        private static readonly HashSet<string> GoalWords = new HashSet<string>(StringComparer.Ordinal)
        {
            "email", "emails", "e-mail", "e-mails", "mail", "inbox", "inboxes", "reply", "replies", "respond",
            "support", "customer", "customers",
        };

        private static readonly HashSet<string> MailHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mail.google.com", "outlook.live.com", "outlook.office.com", "outlook.office365.com", "mail.yahoo.com",
            "mail.proton.me", "app.fastmail.com",
        };

        private static readonly string[] MailTitleWords = { "gmail", "outlook", "inbox" };
        private static readonly Regex Word = new Regex(@"[\p{L}\p{N}]+(?:-[\p{L}\p{N}]+)*");

        public static bool IsEmailGoal(string goal) =>
            Word.Matches((goal ?? string.Empty).ToLowerInvariant()).Cast<Match>().Any(match => GoalWords.Contains(match.Value));

        public static bool IsMailPage(string url, string title)
        {
            Uri page = RelevanceInput.ParseWebUrl(url);
            if (page != null && MailHosts.Contains(page.Host)) return true;
            string lowered = (title ?? string.Empty).ToLowerInvariant();
            return MailTitleWords.Any(lowered.Contains);
        }

        /// <summary>True when the page is a mail inbox and the goal is about email, so it must be allowed.</summary>
        public static bool AllowsMailPage(string goal, string url, string title) =>
            IsMailPage(url, title) && IsEmailGoal(goal);
    }
}
