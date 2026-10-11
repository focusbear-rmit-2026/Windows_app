using System;
using System.Collections.Generic;
using System.Globalization;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// Enough of the Unicode emoji properties to answer the one question the block-list card asks:
    /// does this name start with an emoji?
    ///
    /// Swift reads <c>Unicode.Scalar.Properties.isEmoji</c> / <c>isEmojiPresentation</c> straight out
    /// of ICU. .NET exposes no equivalent, so the Emoji_Presentation set is carried here as a table.
    /// THIS IS THE ONE PLACE IN THIS NAMESPACE THAT IS AN APPROXIMATION RATHER THAN A PORT: the table
    /// is a snapshot of Unicode 15.1 and will drift from whichever ICU the Mac's Foundation links
    /// against as new emoji are assigned. A newly-assigned emoji would fail to be recognised here
    /// while the Mac recognises it; the consequence is a card that draws its emoji inline instead of
    /// in its own tile, which is why an approximation is acceptable for this and for nothing else.
    /// </summary>
    internal static class EmojiPresentation
    {
        private const int ZeroWidthJoiner = 0x200D;
        private const int VariationSelector15 = 0xFE0E;
        private const int VariationSelector16 = 0xFE0F;
        private const int CombiningEnclosingKeycap = 0x20E3;
        private const int SkinToneFirst = 0x1F3FB;
        private const int SkinToneLast = 0x1F3FF;
        private const int RegionalIndicatorFirst = 0x1F1E6;
        private const int RegionalIndicatorLast = 0x1F1FF;

        /// <summary>
        /// Unicode 15.1 Emoji_Presentation=Yes, as inclusive code-point ranges. These are the code
        /// points that render as colour emoji with no variation selector.
        /// </summary>
        private static readonly int[][] EmojiPresentationRanges =
        {
            new[] { 0x231A, 0x231B }, new[] { 0x23E9, 0x23EC }, new[] { 0x23F0, 0x23F0 },
            new[] { 0x23F3, 0x23F3 }, new[] { 0x25FD, 0x25FE }, new[] { 0x2614, 0x2615 },
            new[] { 0x2648, 0x2653 }, new[] { 0x267F, 0x267F }, new[] { 0x2693, 0x2693 },
            new[] { 0x26A1, 0x26A1 }, new[] { 0x26AA, 0x26AB }, new[] { 0x26BD, 0x26BE },
            new[] { 0x26C4, 0x26C5 }, new[] { 0x26CE, 0x26CE }, new[] { 0x26D4, 0x26D4 },
            new[] { 0x26EA, 0x26EA }, new[] { 0x26F2, 0x26F3 }, new[] { 0x26F5, 0x26F5 },
            new[] { 0x26FA, 0x26FA }, new[] { 0x26FD, 0x26FD }, new[] { 0x2705, 0x2705 },
            new[] { 0x270A, 0x270B }, new[] { 0x2728, 0x2728 }, new[] { 0x274C, 0x274C },
            new[] { 0x274E, 0x274E }, new[] { 0x2753, 0x2755 }, new[] { 0x2757, 0x2757 },
            new[] { 0x2795, 0x2797 }, new[] { 0x27B0, 0x27B0 }, new[] { 0x27BF, 0x27BF },
            new[] { 0x2B1B, 0x2B1C }, new[] { 0x2B50, 0x2B50 }, new[] { 0x2B55, 0x2B55 },
            new[] { 0x1F004, 0x1F004 }, new[] { 0x1F0CF, 0x1F0CF }, new[] { 0x1F18E, 0x1F18E },
            new[] { 0x1F191, 0x1F19A }, new[] { 0x1F1E6, 0x1F1FF }, new[] { 0x1F201, 0x1F201 },
            new[] { 0x1F21A, 0x1F21A }, new[] { 0x1F22F, 0x1F22F }, new[] { 0x1F232, 0x1F236 },
            new[] { 0x1F238, 0x1F23A }, new[] { 0x1F250, 0x1F251 }, new[] { 0x1F300, 0x1F320 },
            new[] { 0x1F32D, 0x1F335 }, new[] { 0x1F337, 0x1F37C }, new[] { 0x1F37E, 0x1F393 },
            new[] { 0x1F3A0, 0x1F3CA }, new[] { 0x1F3CF, 0x1F3D3 }, new[] { 0x1F3E0, 0x1F3F0 },
            new[] { 0x1F3F4, 0x1F3F4 }, new[] { 0x1F3F8, 0x1F43E }, new[] { 0x1F440, 0x1F440 },
            new[] { 0x1F442, 0x1F4FC }, new[] { 0x1F4FF, 0x1F53D }, new[] { 0x1F54B, 0x1F54E },
            new[] { 0x1F550, 0x1F567 }, new[] { 0x1F57A, 0x1F57A }, new[] { 0x1F595, 0x1F596 },
            new[] { 0x1F5A4, 0x1F5A4 }, new[] { 0x1F5FB, 0x1F64F }, new[] { 0x1F680, 0x1F6C5 },
            new[] { 0x1F6CC, 0x1F6CC }, new[] { 0x1F6D0, 0x1F6D2 }, new[] { 0x1F6D5, 0x1F6D7 },
            new[] { 0x1F6DC, 0x1F6DF }, new[] { 0x1F6EB, 0x1F6EC }, new[] { 0x1F6F4, 0x1F6FC },
            new[] { 0x1F7E0, 0x1F7EB }, new[] { 0x1F7F0, 0x1F7F0 }, new[] { 0x1F90C, 0x1F93A },
            new[] { 0x1F93C, 0x1F945 }, new[] { 0x1F947, 0x1F9FF }, new[] { 0x1FA70, 0x1FA7C },
            new[] { 0x1FA80, 0x1FA88 }, new[] { 0x1FA90, 0x1FABD }, new[] { 0x1FABF, 0x1FAC5 },
            new[] { 0x1FACE, 0x1FADB }, new[] { 0x1FAE0, 0x1FAE8 }, new[] { 0x1FAF0, 0x1FAF8 },
        };

        internal static bool HasEmojiPresentation(int codePoint)
        {
            foreach (int[] range in EmojiPresentationRanges)
            {
                if (codePoint >= range[0] && codePoint <= range[1]) return true;
            }
            return false;
        }

        /// <summary>
        /// Anything that can carry emoji meaning, which is the wider <c>isEmoji</c> property. ASCII
        /// digits and <c>#</c>/<c>*</c> are deliberately included, exactly as Unicode has them — the
        /// callers are responsible for not mistaking "1Password" for an emoji, and they do that with
        /// the narrower presentation test above.
        /// </summary>
        internal static bool IsEmoji(int codePoint)
        {
            if (codePoint == '#' || codePoint == '*' || (codePoint >= '0' && codePoint <= '9')) return true;
            if (HasEmojiPresentation(codePoint)) return true;
            // Text-presentation emoji (©, ®, ☺, ✂ …) live among the symbol categories.
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(codePoint), 0);
            return category == UnicodeCategory.OtherSymbol || category == UnicodeCategory.MathSymbol;
        }

        /// <summary>
        /// The leading extended grapheme cluster and its code points.
        ///
        /// Built by hand rather than with <c>StringInfo</c>: netstandard2.0 and net472 use a
        /// pre-UAX#29-emoji text-element algorithm that splits ZWJ sequences and flag pairs, so the
        /// "more than one scalar" half of Swift's rule would never fire on the runtimes this library
        /// targets. Joining them here keeps the answer the same on every target.
        /// </summary>
        internal static IReadOnlyList<int> LeadingClusterCodePoints(string value)
        {
            var codePoints = new List<int>();
            if (string.IsNullOrEmpty(value)) return codePoints;

            int index = 0;
            codePoints.Add(ReadCodePoint(value, ref index));

            // A flag is exactly two regional indicators and takes no further extensions.
            if (codePoints[0] >= RegionalIndicatorFirst && codePoints[0] <= RegionalIndicatorLast)
            {
                if (index < value.Length)
                {
                    int next = PeekCodePoint(value, index);
                    if (next >= RegionalIndicatorFirst && next <= RegionalIndicatorLast)
                    {
                        codePoints.Add(ReadCodePoint(value, ref index));
                    }
                }
                return codePoints;
            }

            while (index < value.Length)
            {
                int next = PeekCodePoint(value, index);
                if (next == VariationSelector15 || next == VariationSelector16
                    || next == CombiningEnclosingKeycap
                    || (next >= SkinToneFirst && next <= SkinToneLast))
                {
                    codePoints.Add(ReadCodePoint(value, ref index));
                    continue;
                }
                if (next == ZeroWidthJoiner)
                {
                    int afterJoiner = index;
                    ReadCodePoint(value, ref afterJoiner);
                    if (afterJoiner >= value.Length) break;
                    codePoints.Add(ReadCodePoint(value, ref index));   // the joiner itself
                    codePoints.Add(ReadCodePoint(value, ref index));   // what it joins to
                    continue;
                }
                break;
            }
            return codePoints;
        }

        internal static string ClusterText(string value, IReadOnlyList<int> codePoints)
        {
            int length = 0;
            foreach (int codePoint in codePoints) length += codePoint > 0xFFFF ? 2 : 1;
            return value.Substring(0, length);
        }

        private static int PeekCodePoint(string value, int index)
        {
            int cursor = index;
            return ReadCodePoint(value, ref cursor);
        }

        private static int ReadCodePoint(string value, ref int index)
        {
            if (char.IsHighSurrogate(value[index]) && index + 1 < value.Length
                && char.IsLowSurrogate(value[index + 1]))
            {
                int codePoint = char.ConvertToUtf32(value[index], value[index + 1]);
                index += 2;
                return codePoint;
            }
            return value[index++];
        }
    }
}
