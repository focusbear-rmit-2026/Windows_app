using System.Text.RegularExpressions;

namespace FocusBear.Core.Schedule
{
    /// <summary>
    /// Typing into a Log Quantity answer. The box accepted digits and one "." and silently swallowed anything
    /// else - so someone whose decimal separator is a comma, typing "6,85", logged 685. A comma now means the
    /// decimal point (the app reads the value with the invariant culture), and typing over a selection replaces it.
    /// </summary>
    public static class QuantityTyping
    {
        private static readonly Regex Allowed = new Regex(@"^[0-9]*\.?[0-9]*$", RegexOptions.CultureInvariant);

        /// <summary>The box's text after <paramref name="typed"/>, or null when it would not be a number being typed.</summary>
        public static string Apply(string text, int selectionStart, int selectionLength, string typed)
        {
            string current = text ?? "";
            int start = System.Math.Max(0, System.Math.Min(selectionStart, current.Length));
            int length = System.Math.Max(0, System.Math.Min(selectionLength, current.Length - start));
            // An IME types full-width digits and punctuation ("４", "．", "，"): read them as the ASCII ones.
            var folded = new System.Text.StringBuilder();
            foreach (char c in typed ?? "")
            {
                if (c > '\u007f' && char.IsDigit(c)) folded.Append((char)('0' + (int)char.GetNumericValue(c)));
                else if (c == '\uFF0E') folded.Append('.');
                else if (c == '\uFF0C') folded.Append(',');
                else folded.Append(c);
            }
            string input = folded.ToString().Replace(',', '.');
            string next = current.Substring(0, start) + input + current.Substring(start + length);
            return Allowed.IsMatch(next) ? next : null;
        }
    }
}
