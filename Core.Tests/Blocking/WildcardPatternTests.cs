using System;
using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Blocking
{
    /// <summary>
    /// The Mac's <c>wildcard(_:pattern:)</c> is <c>NSPredicate ... LIKE</c>, which has no Swift-side
    /// test of its own. Every expectation below was taken by running the real Foundation predicate on
    /// macOS rather than from the documentation, because the whole block-list depends on knowing
    /// exactly which characters are special and which are not.
    /// </summary>
    [TestClass]
    public class WildcardPatternTests
    {
        [TestMethod]
        public void StarMatchesAnyRunAndQuestionMarkMatchesExactlyOne()
        {
            Assert.IsTrue(WildcardPattern.Matches("abc", "a*c"));
            Assert.IsTrue(WildcardPattern.Matches("abc", "a?c"));
            Assert.IsTrue(WildcardPattern.Matches("ab", "a?"));
            Assert.IsFalse(WildcardPattern.Matches("abc", "a?"), "? is one character, not a suffix");
        }

        [TestMethod]
        public void TheWholeStringMustMatch()
        {
            Assert.IsFalse(WildcardPattern.Matches("xabcx", "abc"), "there is no implicit contains");
            Assert.IsTrue(WildcardPattern.Matches("xabcx", "*abc*"));
        }

        [TestMethod]
        public void BacktracksAcrossSeveralStars()
        {
            Assert.IsTrue(WildcardPattern.Matches("aXbYc", "a*b*c"));
            Assert.IsTrue(WildcardPattern.Matches("abcbc", "a*b*c"));
            Assert.IsFalse(WildcardPattern.Matches("ac", "a*b*c"));
        }

        [TestMethod]
        public void RegexAndSqlMetacharactersAreLiteral()
        {
            // A pattern is neither a regex nor a SQL LIKE, so a "." in a domain matches only a ".".
            Assert.IsTrue(WildcardPattern.Matches("a.c", "a.c"));
            Assert.IsFalse(WildcardPattern.Matches("abc", "a.c"));
            Assert.IsFalse(WildcardPattern.Matches("aaac", "a+c"));
            Assert.IsTrue(WildcardPattern.Matches("a+c", "a+c"));
            Assert.IsFalse(WildcardPattern.Matches("abc", "a[b]c"));
            Assert.IsTrue(WildcardPattern.Matches("a[b]c", "a[b]c"));
            Assert.IsFalse(WildcardPattern.Matches("abc", "a_c"), "_ is a SQL wildcard, not a LIKE one");
            Assert.IsTrue(WildcardPattern.Matches("100%", "100%"));
            Assert.IsTrue(WildcardPattern.Matches("a(c)", "a(c)"));
        }

        [TestMethod]
        public void BackslashEscapesTheNextCharacter()
        {
            Assert.IsTrue(WildcardPattern.Matches("a*c", "a\\*c"));
            Assert.IsFalse(WildcardPattern.Matches("abc", "a\\*c"));
            Assert.IsTrue(WildcardPattern.Matches("a?c", "a\\?c"));
            Assert.IsTrue(WildcardPattern.Matches("a\\c", "a\\\\c"));
            // Escaping an ordinary character is allowed and means the character itself.
            Assert.IsTrue(WildcardPattern.Matches("ab", "*a\\b*"));
        }

        [TestMethod]
        public void MatchingIsCaseAndDiacriticSensitive()
        {
            Assert.IsFalse(WildcardPattern.Matches("ABC", "abc"));
            Assert.IsFalse(WildcardPattern.Matches("abc", "ABC"));
            Assert.IsFalse(WildcardPattern.Matches("café", "cafe"));
        }

        [TestMethod]
        public void AnEmptyPatternMatchesNothingAtAll()
        {
            // Not even the empty string. The callers never pass one because they wrap entries in
            // stars first — which is why an empty block-list entry becomes "**" and matches
            // everything, the opposite outcome.
            Assert.IsFalse(WildcardPattern.Matches(string.Empty, string.Empty));
            Assert.IsFalse(WildcardPattern.Matches("a", string.Empty));
            Assert.IsTrue(WildcardPattern.Matches("anything", "**"));
            Assert.IsTrue(WildcardPattern.Matches(string.Empty, "**"));
        }

        [TestMethod]
        public void ATrailingUnescapedBackslashIsRejectedRatherThanIgnored()
        {
            // Foundation raises NSInvalidArgumentException here, which terminates the Mac app. It is
            // surfaced rather than swallowed so the two clients cannot disagree about such an entry;
            // the callers' trailing "*" is what keeps a user's entry from ever reaching this.
            Assert.ThrowsException<ArgumentException>(() => WildcardPattern.Matches("a", "a\\"));
        }
    }
}
