using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests
{
    /// <summary>
    /// Every expectation here was produced by running the same comparison in Swift 6 on macOS
    /// (2026-09-24), not derived from the documentation.
    /// </summary>
    [TestClass]
    public class SwiftStringComparerTests
    {
        private const string DecomposedE = "é";
        private const string PrecomposedE = "é";

        private static int Compare(string x, string y) => SwiftStringComparer.Instance.Compare(x, y);

        [TestMethod]
        public void CanonicallyEquivalentSpellingsAreEqual()
        {
            Assert.AreEqual(0, Compare(DecomposedE, PrecomposedE));
        }

        [TestMethod]
        public void AnAccentSortsTheSameHoweverItWasEncoded()
        {
            // Swift: "e\u{301}" < "f" is false, and "\u{E9}" < "f" is false.
            Assert.IsTrue(Compare("f", DecomposedE) < 0);
            Assert.IsTrue(Compare("f", PrecomposedE) < 0);
            Assert.IsTrue(string.CompareOrdinal(DecomposedE, "f") < 0,
                "the reason this type exists: ordinal puts the decomposed spelling on the other side of f");
        }

        [TestMethod]
        public void CharactersAboveTheBasicPlaneSortAboveIt()
        {
            // Swift: "\u{FF21}" < "\u{1F600}" is true. UTF-16 ordinal says the opposite, because the
            // emoji's high surrogate (0xD83D) is below 0xFF21.
            Assert.IsTrue(Compare("Ａ", "\U0001F600") < 0);
            Assert.IsTrue(string.CompareOrdinal("Ａ", "\U0001F600") > 0);
        }

        [TestMethod]
        public void MatchesASwiftSortOfTheSameList()
        {
            // Swift: ["f", "e\u{301}", "e", "ez"].sorted() == ["e", "ez", "f", "e\u{301}"]
            string[] sorted = new[] { "f", DecomposedE, "e", "ez" }.OrderBy(s => s, SwiftStringComparer.Instance).ToArray();

            CollectionAssert.AreEqual(new[] { "e", "ez", "f", DecomposedE }, sorted);
        }

        [TestMethod]
        public void IsNotACollation()
        {
            // Swift: "Z" < "a" is true. Matching the Mac matters more than looking alphabetical.
            Assert.IsTrue(Compare("Z", "a") < 0);
        }

        [TestMethod]
        public void AShorterPrefixSortsFirst()
        {
            Assert.IsTrue(Compare("ab", "abc") < 0);
            Assert.IsTrue(Compare("", "a") < 0);
            Assert.IsTrue(Compare("abc", "ab") > 0);
        }

        [TestMethod]
        public void NullSortsFirstAndALoneSurrogateDoesNotThrow()
        {
            Assert.IsTrue(Compare(null, "") < 0);
            Assert.AreEqual(0, Compare(null, null));
            Assert.IsTrue(Compare("\uD800", "a") > 0, "compared as-is rather than failing the whole sort");
        }
    }
}
