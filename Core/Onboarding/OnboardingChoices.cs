using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.Onboarding
{
    /// <summary>The onboarding screens' choices, made safe to repeat: the user can go Back and choose again.</summary>
    public static class OnboardingChoices
    {
        private static readonly string[] Occupations = { "student", "business owner", "employee", "freelancer", "other" };

        /// <summary>
        /// What "Ask occupation" sends to the suggested-sites generator: what the user typed, else the occupation
        /// of the chosen radio (1 student, 2 business owner, 3 employee, 4 freelancer, 5 other). Every branch used
        /// to test the first radio, so a business owner, employee or freelancer who typed nothing was sent "other".
        /// </summary>
        public static string OccupationQuery(string typed, int selectedRadio)
        {
            string text = (typed ?? "").Trim();
            if (text.Length > 0) return text;
            return selectedRadio >= 1 && selectedRadio <= Occupations.Length ? Occupations[selectedRadio - 1] : "other";
        }

        /// <summary>
        /// Brings <paramref name="target"/> in line with the ticks on a suggestion list: a ticked entry is there once,
        /// an unticked one that the list offers is not. Proceed used to append every ticked entry each time, so
        /// going Back and on again listed them twice, and unticking after going Back did not take them out.
        /// Entries the list does not offer (added elsewhere) are left alone. Compared ignoring case.
        /// </summary>
        public static void ApplyTicks(IList<string> target, IEnumerable<KeyValuePair<string, bool>> offered)
        {
            foreach (var entry in offered ?? Enumerable.Empty<KeyValuePair<string, bool>>())
            {
                if (string.IsNullOrWhiteSpace(entry.Key)) continue;
                var present = target.Where(t => string.Equals(t, entry.Key, StringComparison.OrdinalIgnoreCase)).ToList();
                if (entry.Value)
                {
                    if (present.Count == 0) target.Add(entry.Key);
                    foreach (var extra in present.Skip(1)) target.Remove(extra);
                }
                else foreach (var item in present) target.Remove(item);
            }
        }

        /// <summary>Whether a suggestion list already offers <paramref name="url"/> (ignoring case and surrounding space).</summary>
        public static bool Offers(IEnumerable<string> offered, string url)
        {
            string wanted = (url ?? "").Trim();
            return (offered ?? Enumerable.Empty<string>()).Any(o => string.Equals((o ?? "").Trim(), wanted, StringComparison.OrdinalIgnoreCase));
        }
    }
}
