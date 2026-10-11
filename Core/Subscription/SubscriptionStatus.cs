using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Subscription
{
    /// <summary>
    /// The subscription status from the backend (SubscriptionStatus model: activeEntitlements, expirations).
    /// The app read only activeEntitlements[0] against a paid list without team_admin or the 10/15 team sizes,
    /// so a paying user listed as ["trial","personal"] or ["team_admin",...] was treated as on trial; an empty
    /// list or a missing expiry threw half-way. Like the Mac (SubscriptionEntitlements.payingEntitlement), the
    /// first paying entitlement among all of them decides.
    /// </summary>
    public sealed class SubscriptionStatus
    {
        /// <summary>Entitlement.enum values that are paid access (everything but "trial").</summary>
        public static readonly IReadOnlyCollection<string> PaidEntitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "personal", "team_admin", "team_member", "team_owner", "team_size_5", "team_size_10", "team_size_15",
        };

        public bool HasActiveSubscription { get; private set; }
        public bool IsPurchased { get; private set; }
        /// <summary>Days left on a trial; null when not on a trial or unknown.</summary>
        public int? TrialDaysLeft { get; private set; }

        /// <summary>The status, or null when the body can't be read (the caller keeps what it knew).</summary>
        public static SubscriptionStatus Parse(string json)
        {
            JObject root;
            try
            {
                root = string.IsNullOrWhiteSpace(json) ? null : JToken.Parse(json) as JObject;
            }
            catch (JsonException)
            {
                return null;
            }
            if (root == null)
                return null;

            List<string> active = (root["activeEntitlements"] as JArray)?
                .Where(t => t.Type == JTokenType.String).Select(t => (string)t).ToList() ?? new List<string>();

            var status = new SubscriptionStatus { HasActiveSubscription = active.Count > 0 };
            if (!status.HasActiveSubscription)
                return status;

            status.IsPurchased = active.Any(e => PaidEntitlements.Contains(e));
            if (!status.IsPurchased && active.Contains("trial", StringComparer.OrdinalIgnoreCase))
                status.TrialDaysLeft = DaysLeft(root["expirations"]?["trial"]);
            return status;
        }

        private static int? DaysLeft(JToken expiry)
        {
            JToken days = expiry?["days_left"];
            if (days == null || days.Type == JTokenType.Null)
                return null;
            if (days.Type == JTokenType.Integer || days.Type == JTokenType.Float)
                return (int)Math.Floor((double)days);
            return int.TryParse((string)days, out int parsed) ? parsed : (int?)null;
        }
    }
}
