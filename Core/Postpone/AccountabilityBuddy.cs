using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Postpone
{
    /// <summary>One turn of the postpone conversation.</summary>
    public sealed class AccountabilityMessage
    {
        public AccountabilityMessage(bool fromBuddy, string text)
        {
            FromBuddy = fromBuddy;
            Text = text ?? "";
        }

        public bool FromBuddy { get; }
        public string Text { get; }
    }

    /// <summary>
    /// The AI accountability buddy's answer (<c>POST /user/evaluate-focus-postponement</c>): approve or deny
    /// the postpone, the reply to show, the intention it distilled, and what the scoped session may use.
    /// </summary>
    public sealed class AccountabilityVerdict
    {
        public const int MinSessionMinutes = 5;
        public const int MaxSessionMinutes = 180;
        public const int DefaultSessionMinutes = 25;

        public bool Approved { get; set; }
        public string Message { get; set; } = "";
        public string Intention { get; set; }
        public IList<string> AllowedApps { get; set; } = new List<string>();
        public IList<string> AllowedUrls { get; set; } = new List<string>();
        public int DurationMinutes { get; set; }

        /// <summary>The server clamps too; this keeps a missing or odd value inside 5–180, default 25.</summary>
        public int ClampedDurationMinutes =>
            Math.Min(MaxSessionMinutes, Math.Max(MinSessionMinutes, DurationMinutes > 0 ? DurationMinutes : DefaultSessionMinutes));

        /// <summary>The session's goal: the buddy's distilled intention, else the user's first message.</summary>
        public string GoalOr(string firstUserMessage) => string.IsNullOrWhiteSpace(Intention) ? (firstUserMessage ?? "").Trim() : Intention.Trim();

        /// <summary>Null when the body is not a verdict at all (the caller shows the recovery path).</summary>
        public static AccountabilityVerdict Read(string json)
        {
            JObject row;
            try { row = JsonConvert.DeserializeObject<JToken>(json ?? "", new JsonSerializerSettings { DateParseHandling = DateParseHandling.None }) as JObject; }
            catch (JsonException) { return null; }
            if (row == null || row["verdict"]?.Type != JTokenType.String) return null;
            int minutes = 0;
            var duration = row["clampedDurationMinutes"];
            if (duration != null && (duration.Type == JTokenType.Integer || duration.Type == JTokenType.Float)) minutes = (int)Math.Round((double)duration);
            return new AccountabilityVerdict
            {
                Approved = string.Equals((string)row["verdict"], "approve", StringComparison.OrdinalIgnoreCase),
                Message = row["reason"]?.Type == JTokenType.String ? (string)row["reason"] : "",
                Intention = row["intention"]?.Type == JTokenType.String ? (string)row["intention"] : null,
                AllowedApps = Strings(row["allowedApps"]),
                AllowedUrls = Strings(row["allowedUrls"]),
                DurationMinutes = minutes,
            };
        }

        private static IList<string> Strings(JToken token) =>
            (token as JArray)?.Where(t => t.Type == JTokenType.String).Select(t => ((string)t).Trim()).Where(t => t.Length > 0).Distinct().ToList() ?? new List<string>();
    }

    /// <summary>The request body, with the backend DTO's limits applied here rather than as a 400.</summary>
    public static class AccountabilityRequest
    {
        public const int MessageLimit = 500;
        public const int ContextLimit = 200;
        public const int MaxContextItems = 50;

        public static JObject Build(IEnumerable<AccountabilityMessage> messages, IEnumerable<string> uncompletedHabits,
            IEnumerable<string> dayPlanTasks, string currentMeetingTitle, string language)
        {
            var body = new JObject
            {
                ["messages"] = new JArray((messages ?? Enumerable.Empty<AccountabilityMessage>())
                    .Where(m => m.Text.Trim().Length > 0)
                    .Select(m => new JObject { ["role"] = m.FromBuddy ? "assistant" : "user", ["content"] = Bounded(m.Text, MessageLimit) })),
                ["uncompletedHabits"] = Context(uncompletedHabits),
                ["dayPlanTasks"] = Context(dayPlanTasks),
                ["language"] = string.IsNullOrWhiteSpace(language) ? "English" : language,
            };
            if (!string.IsNullOrWhiteSpace(currentMeetingTitle)) body["currentMeetingTitle"] = Bounded(currentMeetingTitle, MessageLimit);
            return body;
        }

        /// <summary>Trimmed to <paramref name="limit"/> UTF-16 units without splitting a surrogate pair.</summary>
        public static string Bounded(string text, int limit)
        {
            text = (text ?? "").Trim();
            if (text.Length <= limit) return text;
            int cut = limit;
            if (char.IsHighSurrogate(text[cut - 1])) cut--;
            return text.Substring(0, cut);
        }

        private static JArray Context(IEnumerable<string> items) =>
            new JArray((items ?? Enumerable.Empty<string>()).Where(i => !string.IsNullOrWhiteSpace(i)).Take(MaxContextItems).Select(i => Bounded(i, ContextLimit)));

        /// <summary>"Meditate (~10 min)" — how a habit or task reaches the buddy.</summary>
        public static string Line(string title, int minutes, string project = null)
        {
            string line = (title ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(project)) line += " [#" + project.Trim() + "]";
            return line + " (~" + Math.Max(1, minutes).ToString(CultureInfo.InvariantCulture) + " min)";
        }
    }

    /// <summary>
    /// A conversation the buddy keeps denying the same way is the feature failing, not the user: after four
    /// buddy turns, or as soon as it repeats itself, the chat offers the same escape as a backend error.
    /// </summary>
    public static class AccountabilityLoopGuard
    {
        public const int MaxBuddyTurns = 4;

        public static string Normalised(string text)
        {
            var words = new List<string>();
            var word = new StringBuilder();
            foreach (char c in (text ?? "").ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) word.Append(c);
                else if (word.Length > 0) { words.Add(word.ToString()); word.Clear(); }
            }
            if (word.Length > 0) words.Add(word.ToString());
            return string.Join(" ", words);
        }

        public static bool IsStuck(IEnumerable<string> buddyMessages)
        {
            var meaningful = (buddyMessages ?? Enumerable.Empty<string>()).Select(Normalised).Where(m => m.Length > 0).ToList();
            if (meaningful.Count == 0) return false;
            if (meaningful.Count >= MaxBuddyTurns) return true;
            string latest = meaningful[meaningful.Count - 1];
            return meaningful.Take(meaningful.Count - 1).Contains(latest);
        }
    }

    /// <summary>
    /// Why the recovery card is showing, which decides what it says first (the Mac's
    /// <c>AccountabilityRecoveryReason</c>, report 2026-09-25_15-40-49). One generic card ("Your buddy didn't
    /// answer…") used to cover all three, so it told a user whose buddy had just denied them that it hadn't
    /// answered at all.
    /// </summary>
    public enum AccountabilityRecoveryReason
    {
        /// <summary>No internet connection, so the buddy can't be reached at all.</summary>
        Offline,
        /// <summary>The buddy's endpoint failed, timed out or answered with something unusable.</summary>
        Unavailable,
        /// <summary>The buddy answered, but the conversation is going in circles (<see cref="AccountabilityLoopGuard"/>).</summary>
        Stuck,
    }

    /// <summary>What one evaluate call came back with: a verdict, or why there isn't one.</summary>
    public sealed class AccountabilityReply
    {
        private AccountabilityReply(AccountabilityVerdict verdict, AccountabilityRecoveryReason failure)
        {
            Verdict = verdict;
            Failure = failure;
        }

        /// <summary>Null when the call failed; <see cref="Failure"/> then says how.</summary>
        public AccountabilityVerdict Verdict { get; }
        public AccountabilityRecoveryReason Failure { get; }

        public static AccountabilityReply Answered(AccountabilityVerdict verdict) =>
            verdict == null ? Failed(AccountabilityRecoveryReason.Unavailable) : new AccountabilityReply(verdict, AccountabilityRecoveryReason.Unavailable);

        public static AccountabilityReply Failed(AccountabilityRecoveryReason reason) => new AccountabilityReply(null, reason);

        /// <summary>
        /// Offline only when the connectivity check failed before the request; a request that was sent and then
        /// failed (a timeout, a 5xx, a body that isn't a verdict) is the server's problem, not the user's network.
        /// </summary>
        public static AccountabilityReply From(bool internetOn, bool succeeded, string body)
        {
            if (!internetOn) return Failed(AccountabilityRecoveryReason.Offline);
            return succeeded ? Answered(AccountabilityVerdict.Read(body)) : Failed(AccountabilityRecoveryReason.Unavailable);
        }
    }

    /// <summary>The recovery card's rules (the Mac's <c>recoveryBar</c> after PR #2649).</summary>
    public static class AccountabilityRecovery
    {
        /// <summary>Preselected, so the card is one hold away from done; the hold itself is unchanged.</summary>
        public const int DefaultMinutes = 15;

        /// <summary>
        /// A stuck conversation first asks, quietly, whether to step outside it ("Is the AI accountability buddy
        /// getting annoying? Yes"). An outage opens straight onto the explanation and the postpone controls,
        /// because there's no buddy left to keep talking to.
        /// </summary>
        public static bool StartsCollapsed(AccountabilityRecoveryReason reason) => reason == AccountabilityRecoveryReason.Stuck;

        /// <summary>A denial shows the card only once the conversation is stuck; an approval never does.</summary>
        public static AccountabilityRecoveryReason? ForVerdict(AccountabilityVerdict verdict, IEnumerable<string> buddyMessages) =>
            verdict != null && !verdict.Approved && AccountabilityLoopGuard.IsStuck(buddyMessages) ? AccountabilityRecoveryReason.Stuck : (AccountabilityRecoveryReason?)null;
    }

    /// <summary>
    /// The recovery path's press-and-hold: ten continuous seconds, restarted by any release or focus loss
    /// (the Mac's <c>AccountabilityPostponeRecovery</c>).
    /// </summary>
    public sealed class AccountabilityHold
    {
        public static readonly TimeSpan Required = TimeSpan.FromSeconds(10);
        public static readonly int[] DurationChoices = { 5, 10, 15, 25, 30, 45, 60, 90, 120, 180 };

        private DateTime? _since;

        public bool IsHolding => _since.HasValue;

        public void Press(DateTime now) { if (!_since.HasValue) _since = now; }

        public void Release() => _since = null;

        public double Progress(DateTime now) =>
            _since.HasValue ? Math.Min(1, Math.Max(0, (now - _since.Value).TotalMilliseconds / Required.TotalMilliseconds)) : 0;

        public int SecondsLeft(DateTime now) => (int)Math.Ceiling((1 - Progress(now)) * Required.TotalSeconds);

        public bool IsComplete(DateTime now) => Progress(now) >= 1;
    }
}
