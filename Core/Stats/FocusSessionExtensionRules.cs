using System;

namespace FocusBear.Core.Stats
{
    /// <summary>The accountability buddy's answer about whether a stated task belongs to the session.</summary>
    public sealed class TaskIntentionAlignment
    {
        public TaskIntentionAlignment(bool aligns, string reason)
        {
            Aligns = aligns;
            Reason = reason ?? string.Empty;
        }

        public bool Aligns { get; }

        /// <summary>The server's own words for the conflict, shown to the user when it pushes back.</summary>
        public string Reason { get; }
    }

    public enum ExtendReasonAction
    {
        /// <summary>Grant the extra minutes now.</summary>
        ExtendNow,

        /// <summary>Show the reason and make the user choose again — revise, or extend deliberately.</summary>
        PushBack
    }

    /// <summary>What the extend-reason sheet does once the buddy has (or has not) judged the reason.</summary>
    public struct ExtendReasonVerdict : IEquatable<ExtendReasonVerdict>
    {
        private ExtendReasonVerdict(ExtendReasonAction action, string reason)
        {
            Action = action;
            Reason = reason;
        }

        public ExtendReasonAction Action { get; }

        /// <summary>The push-back message; empty for <see cref="ExtendReasonAction.ExtendNow"/>.</summary>
        public string Reason { get; }

        public static ExtendReasonVerdict ExtendNow()
        {
            return new ExtendReasonVerdict(ExtendReasonAction.ExtendNow, string.Empty);
        }

        public static ExtendReasonVerdict PushBack(string reason)
        {
            return new ExtendReasonVerdict(ExtendReasonAction.PushBack, reason);
        }

        public bool Equals(ExtendReasonVerdict other)
        {
            return Action == other.Action && Reason == other.Reason;
        }

        public override bool Equals(object obj)
        {
            return obj is ExtendReasonVerdict && Equals((ExtendReasonVerdict)obj);
        }

        public override int GetHashCode()
        {
            return (int)Action ^ (Reason ?? string.Empty).GetHashCode();
        }

        public override string ToString()
        {
            return Action == ExtendReasonAction.ExtendNow ? "ExtendNow" : "PushBack(" + Reason + ")";
        }
    }

    /// <summary>
    /// Why the accountability-buddy chat was opened, and how much of what it approves may be granted.
    /// Port of the Mac's <c>AccountabilityChatPurpose</c>.
    /// </summary>
    public sealed class AccountabilityChatPurpose
    {
        private readonly int _requestedMinutes;
        private readonly bool _isExtension;

        private AccountabilityChatPurpose(bool isExtension, int requestedMinutes)
        {
            _isExtension = isExtension;
            _requestedMinutes = requestedMinutes;
        }

        /// <summary>Postponing a routine habit: the buddy's approved duration stands as it is.</summary>
        public static readonly AccountabilityChatPurpose PostponeRoutine = new AccountabilityChatPurpose(false, 0);

        /// <summary>Extending a running focus session by the minutes the user asked for.</summary>
        public static AccountabilityChatPurpose ExtendSession(int minutes)
        {
            return new AccountabilityChatPurpose(true, minutes);
        }

        /// <summary>
        /// The minutes actually granted for an approval of <paramref name="approvedMinutes"/>.
        ///
        /// An extension is capped BOTH ways: never more than the user asked for (the chat reuses the
        /// postpone conversation, which is free to approve a longer stretch than this sheet offered)
        /// and never more than the buddy approved.
        /// </summary>
        public int GrantedMinutes(int approvedMinutes)
        {
            if (!_isExtension)
            {
                return approvedMinutes;
            }

            return Math.Min(Math.Max(0, _requestedMinutes), Math.Max(0, approvedMinutes));
        }
    }

    /// <summary>
    /// The pure rules behind extending a running focus session. Port of the testable half of the
    /// Mac's <c>FocusSessionExtension</c>: what the extra minutes are judged against, and what the
    /// sheet does with the buddy's verdict.
    ///
    /// The report behind it: a product owner typed "play games" into the extend sheet and was given
    /// the extra ten minutes — the screen captured the answer and asked nobody.
    /// </summary>
    public static class FocusSessionExtensionRules
    {
        /// <summary>Separator between the original focus intention and the reason given for extending.</summary>
        public const string IntentionReasonSeparator = " — ";

        /// <summary>
        /// The focus intention the extended session runs with.
        ///
        /// The AI relevance checks judge every app and site against the intention, so folding the
        /// stated reason into it is what makes the buddy's question do something: the extra minutes
        /// are judged against the reason the user gave for wanting them. A blank answer changes
        /// nothing, and a reason already covered by the intention is not appended twice — extending
        /// repeatedly for the same reason must not grow the string without bound.
        /// </summary>
        public static string IntentionAfterExtension(string previousIntention, string reason)
        {
            string trimmedReason = (reason ?? string.Empty).Trim();
            if (trimmedReason.Length == 0)
            {
                return previousIntention;
            }

            string trimmedPrevious = (previousIntention ?? string.Empty).Trim();
            if (trimmedPrevious.Length == 0)
            {
                return trimmedReason;
            }

            // Swift uses localizedCaseInsensitiveContains; CurrentCultureIgnoreCase is its counterpart
            // here — both are case-insensitive but diacritic-sensitive.
            if (trimmedPrevious.IndexOf(trimmedReason, StringComparison.CurrentCultureIgnoreCase) >= 0)
            {
                return trimmedPrevious;
            }

            return trimmedPrevious + IntentionReasonSeparator + trimmedReason;
        }

        /// <summary>
        /// Whether the stated reason is worth sending to the buddy at all. With no intention there is
        /// nothing to compare the answer against, and a skipped question still means skipped.
        /// </summary>
        public static bool ShouldCheckExtensionReason(string intention, string reason)
        {
            return (intention ?? string.Empty).Trim().Length > 0
                && (reason ?? string.Empty).Trim().Length > 0;
        }

        /// <summary>
        /// Turns the buddy's answer into what the sheet should do.
        ///
        /// FAILS OPEN. A null <paramref name="alignment"/> is every way the check can fail to produce
        /// an answer — unreachable server, timed-out request, a body that did not parse — and they all
        /// extend: the check is advisory, and being offline must never cost a user the minutes they
        /// asked for. Only an explicit <c>Aligns == false</c>, which the deliberately lenient
        /// classifier gives only for a clear conflict, pushes back, and even then it asks rather
        /// than forbids.
        /// </summary>
        public static ExtendReasonVerdict VerdictForExtensionReason(TaskIntentionAlignment alignment,
                                                                    string fallbackPushBackMessage)
        {
            if (alignment == null || alignment.Aligns)
            {
                return ExtendReasonVerdict.ExtendNow();
            }

            string message = alignment.Reason.Trim();
            return ExtendReasonVerdict.PushBack(message.Length == 0 ? fallbackPushBackMessage : message);
        }
    }
}
