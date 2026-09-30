using System;
using System.Globalization;
using System.Text;

namespace FocusBear.Core.Stats
{
    /// <summary>
    /// The end-of-session texts as <c>POST /focus-mode/:id/finish</c> accepts them. <c>FinishFocusModeDto</c> caps
    /// <c>achievements</c> and <c>distractions</c> at 255 characters, and the achievements go URL-encoded (as the
    /// Mac sends them), which can triple their length. A longer text was refused and the session never finished
    /// on the server; the Mac trims its encoded achievements for the same reason.
    /// </summary>
    public static class FocusSessionText
    {
        public const int MaxLength = 255;

        /// <summary>
        /// The longest leading part of <paramref name="raw"/> whose encoding fits <paramref name="max"/>, cut on a
        /// whole character (an emoji or an accented letter is never split), then encoded.
        /// </summary>
        public static string EncodedWithin(string raw, Func<string, string> encode, int max = MaxLength)
        {
            if (encode == null) throw new ArgumentNullException(nameof(encode));
            string text = raw ?? "";
            string whole = encode(text) ?? "";
            if (whole.Length <= max) return whole;
            // Encoding never shortens text, so nothing past the first max characters can fit.
            text = Within(text, max);
            var kept = new StringBuilder();
            string best = "";
            TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(text);
            while (elements.MoveNext())
            {
                kept.Append(elements.GetTextElement());
                string encoded = encode(kept.ToString()) ?? "";
                if (encoded.Length > max) break;
                best = encoded;
            }
            return best;
        }

        /// <summary>At most <paramref name="max"/> characters, cut on a whole character.</summary>
        public static string Within(string raw, int max = MaxLength)
        {
            string text = raw ?? "";
            if (text.Length <= max) return text;
            var kept = new StringBuilder();
            TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(text);
            while (elements.MoveNext())
            {
                string next = elements.GetTextElement();
                if (kept.Length + next.Length > max) break;
                kept.Append(next);
            }
            return kept.ToString();
        }
    }
}
