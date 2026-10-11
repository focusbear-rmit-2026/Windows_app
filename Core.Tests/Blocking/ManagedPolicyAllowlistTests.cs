using System;
using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Blocking
{
    /// <summary>
    /// The host / subdomain / path / port rules of the Mac's
    /// <c>ManagedPolicyEvaluator.matchesAllowlist</c>.
    ///
    /// This is the tight counterpart to <see cref="UrlValidationTests"/>: the same question ("does
    /// this URL match this entry?") answered by parsing the URL instead of searching it for a
    /// substring. Several cases below are deliberately the same URL/entry pair as a case there, so
    /// the two answers can be read side by side.
    /// </summary>
    [TestClass]
    public class ManagedPolicyAllowlistTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

        private static bool Matches(string url, params ManagedAllowlistEntry[] allowlist)
        {
            return ManagedPolicyAllowlist.Matches(allowlist, url, Now);
        }

        private static ManagedAllowlistEntry Entry(string url, bool includeSubdomains = false,
                                                   DateTimeOffset? expiresAt = null)
        {
            return new ManagedAllowlistEntry(url, includeSubdomains, expiresAt);
        }

        // MARK: - Host

        [TestMethod]
        public void AnExactHostMatches()
        {
            Assert.IsTrue(Matches("https://example.org/anything", Entry("https://example.org/")));
            Assert.IsTrue(Matches("https://EXAMPLE.ORG/", Entry("example.org")),
                "both sides are canonicalised, so case cannot separate them");
        }

        [TestMethod]
        public void ASubdomainMatchesOnlyWhenTheEntrySaysSo()
        {
            Assert.IsFalse(Matches("https://docs.example.org/", Entry("https://example.org/")));
            Assert.IsTrue(Matches("https://docs.example.org/", Entry("https://example.org/", includeSubdomains: true)));
            Assert.IsTrue(Matches("https://a.b.example.org/", Entry("https://example.org/", includeSubdomains: true)));
        }

        [TestMethod]
        public void ALookalikeHostNeverMatchesEvenWithSubdomainsOn()
        {
            // The dot in "." + host is the whole guard. Without it every one of these would pass as
            // "example.org", which is exactly the hole the substring matcher has.
            var entry = Entry("https://example.org/", includeSubdomains: true);
            Assert.IsFalse(Matches("https://notexample.org/", entry));
            Assert.IsFalse(Matches("https://example.org.evil.example/", entry));
            Assert.IsFalse(Matches("https://evil-example.org/", entry));

            // Contrast: the block-list matcher accepts the same lookalikes as "example.org".
            Assert.IsTrue(UrlValidation.IsUrlValid("https://notexample.org/", "example.org"));
        }

        [TestMethod]
        public void AnEntryMentionedOnlyInThePathOfAnotherSiteDoesNotMatch()
        {
            // The failure mode an allow list cannot afford, and the one the substring matcher has.
            Assert.IsFalse(Matches("https://evil.example/example.org", Entry("https://example.org/")));
            Assert.IsFalse(Matches("https://evil.example/?u=example.org", Entry("https://example.org/")));
            Assert.IsTrue(UrlValidation.IsUrlValid("https://evil.example/example.org", "example.org"),
                "the same URL passes the substring matcher — the reason both exist");
        }

        [TestMethod]
        public void AUrlThatCannotBeCanonicalisedMatchesNothing()
        {
            // Failure is always towards blocking: an unreadable URL is not allowed by default.
            Assert.IsFalse(Matches("https://user:pw@example.org/", Entry("https://example.org/")));
            Assert.IsFalse(Matches("https://example.org\\evil", Entry("https://example.org/")));
            Assert.IsFalse(Matches("ftp://example.org/", Entry("https://example.org/")));
        }

        [TestMethod]
        public void AnEntryThatCannotBeCanonicalisedAllowsNothing()
        {
            Assert.IsFalse(Matches("https://example.org/", Entry("https://user:pw@example.org/")));
            Assert.IsFalse(Matches("https://example.org/", Entry("not a url at all")));
        }

        // MARK: - Port

        [TestMethod]
        public void PortsMustAgree()
        {
            Assert.IsFalse(Matches("https://example.org:8443/", Entry("https://example.org/")));
            Assert.IsTrue(Matches("https://example.org:8443/", Entry("https://example.org:8443/")));
            Assert.IsTrue(Matches("https://example.org/", Entry("https://example.org:443/")),
                "a default port and an absent one are the same port");
        }

        // MARK: - Path

        [TestMethod]
        public void AnEntryWithNoPathAllowsTheWholeHost()
        {
            Assert.IsTrue(Matches("https://example.org/anything/at/all", Entry("https://example.org/")));
            Assert.IsTrue(Matches("https://example.org/anything", Entry("example.org")));
        }

        [TestMethod]
        public void APathEntryMatchesOnSegmentBoundariesOnly()
        {
            var entry = Entry("https://example.org/docs");
            Assert.IsTrue(Matches("https://example.org/docs", entry));
            Assert.IsTrue(Matches("https://example.org/docs/guide", entry));
            Assert.IsFalse(Matches("https://example.org/docsecret", entry),
                "a prefix that stops mid-segment is a different path");
            Assert.IsFalse(Matches("https://example.org/", entry));
            Assert.IsFalse(Matches("https://example.org/other", entry));
        }

        [TestMethod]
        public void ATrailingSlashOnTheEntryChangesNothing()
        {
            Assert.IsTrue(Matches("https://example.org/docs/guide", Entry("https://example.org/docs/")));
            Assert.IsFalse(Matches("https://example.org/docsecret", Entry("https://example.org/docs/")));
        }

        [TestMethod]
        public void DotSegmentsCannotClimbOutOfAnAllowedPath()
        {
            var entry = Entry("https://example.org/docs");
            Assert.IsFalse(Matches("https://example.org/docs/../secret", entry));
            Assert.IsFalse(Matches("https://example.org/docs/%2e%2e/secret", entry),
                "encoded or not, the traversal is resolved before the comparison");
        }

        [TestMethod]
        public void TheQueryIsNotPartOfThePathComparison()
        {
            Assert.IsTrue(Matches("https://example.org/docs?page=2", Entry("https://example.org/docs")));
        }

        // MARK: - Scheme and expiry

        [TestMethod]
        public void TheSchemeIsDeliberatelyNotCompared()
        {
            // Faithful to the Mac: an entry written as http allows the https page on the same host,
            // path and port. Noted rather than tightened, so both clients keep one answer.
            Assert.IsTrue(Matches("https://example.org/docs", Entry("http://example.org/docs")));
        }

        [TestMethod]
        public void AnExpiredEntryAllowsNothing()
        {
            Assert.IsTrue(Matches("https://example.org/", Entry("https://example.org/", expiresAt: Now.AddMinutes(1))));
            Assert.IsFalse(Matches("https://example.org/", Entry("https://example.org/", expiresAt: Now.AddMinutes(-1))));
            Assert.IsFalse(Matches("https://example.org/", Entry("https://example.org/", expiresAt: Now)),
                "expiry is exclusive: an entry that expires exactly now has expired");
        }

        [TestMethod]
        public void AnyMatchingEntryIsEnough()
        {
            Assert.IsTrue(Matches("https://example.org/docs",
                Entry("https://other.example/"), Entry("https://example.org/docs")));
        }

        [TestMethod]
        public void AnEmptyOrMissingAllowlistAllowsNothing()
        {
            Assert.IsFalse(Matches("https://example.org/"));
            Assert.IsFalse(ManagedPolicyAllowlist.Matches(null, "https://example.org/", Now));
        }
    }
}
