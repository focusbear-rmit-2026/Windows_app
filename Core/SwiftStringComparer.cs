using System;
using System.Collections.Generic;
using System.Text;

namespace FocusBear.Core
{
    /// <summary>
    /// Orders strings the way Swift's <c>&lt;</c> on <c>String</c> does, for rules whose order both
    /// clients show the same user.
    ///
    /// Swift compares by Unicode canonical equivalence: both sides are brought to NFC and compared
    /// scalar by scalar. <see cref="StringComparer.Ordinal"/> differs twice. It compares whatever
    /// encoding the text arrived in, so a decomposed "é" (e + U+0301) sorts before "f" while a
    /// precomposed one sorts after it. And it compares UTF-16 code units, which put every character
    /// above U+FFFF (emoji, many CJK extensions) BELOW U+E000–U+FFFF instead of above them.
    ///
    /// Not a collation: "Z" still sorts before "a", exactly as on the Mac.
    /// </summary>
    public sealed class SwiftStringComparer : IComparer<string>
    {
        public static readonly SwiftStringComparer Instance = new SwiftStringComparer();

        private SwiftStringComparer() { }

        public int Compare(string x, string y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            return CompareScalars(ToNfc(x), ToNfc(y));
        }

        /// <summary>
        /// A string holding a lone surrogate cannot be normalized, and cannot exist in Swift at all —
        /// decoding repairs it. Comparing it as-is keeps the order total instead of throwing mid-sort.
        /// </summary>
        private static string ToNfc(string value)
        {
            try
            {
                return value.Normalize(NormalizationForm.FormC);
            }
            catch (ArgumentException)
            {
                return value;
            }
        }

        private static int CompareScalars(string x, string y)
        {
            int i = 0, j = 0;
            while (i < x.Length && j < y.Length)
            {
                int a = ScalarAt(x, ref i);
                int b = ScalarAt(y, ref j);
                if (a != b) return a < b ? -1 : 1;
            }

            if (i < x.Length) return 1;
            if (j < y.Length) return -1;
            return 0;
        }

        private static int ScalarAt(string value, ref int index)
        {
            if (char.IsSurrogatePair(value, index))
            {
                int scalar = char.ConvertToUtf32(value, index);
                index += 2;
                return scalar;
            }

            return value[index++];
        }
    }
}
