using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Blocking
{
    /// <summary>
    /// Ported from the Mac app's <c>validationTest.swift</c>, plus a block of security-shaped cases
    /// whose expectations were taken by running the real Swift <c>urlValidation</c> on macOS. Where a
    /// case documents a hole rather than a guard it says so — the point is that both clients have the
    /// SAME holes until they are fixed on both.
    /// </summary>
    [TestClass]
    public class UrlValidationTests
    {
        // MARK: - Wildcards in an entry (validationTest.swift)

        [TestMethod]
        public void StarsAnywhereInTheEntryAreWildcards()
        {
            Assert.IsTrue(UrlValidation.IsUrlValid("https://www.facebook.com", "*facebook.com"));
            Assert.IsTrue(UrlValidation.IsUrlValid("https://www.facebook.com", "facebook*"));
            Assert.IsTrue(UrlValidation.IsUrlValid("https://www.facebook.com", "*facebook*"));
            Assert.IsTrue(UrlValidation.IsUrlValid("https://www.facebook.com", "facebook"));
            Assert.IsTrue(UrlValidation.IsUrlValid("https://www.fammmcebllllook.c88m", "f*ce*ook.c*m"));
        }

        [TestMethod]
        public void QuestionMarkIsASingleCharacterWildcard()
        {
            Assert.IsTrue(UrlValidation.IsUrlValid("ab", "a?"));
        }

        [TestMethod]
        public void AnEmptyEntryMatchesEveryUrl()
        {
            // Documents a footgun rather than a feature: an empty allow/block row is wrapped to "**".
            Assert.IsTrue(UrlValidation.IsUrlValid("https://github.com", string.Empty));
        }

        // MARK: - Normalisation

        [TestMethod]
        public void SchemeAndWwwAreStrippedAndMatchingIsCaseInsensitive()
        {
            Assert.IsTrue(UrlValidation.IsUrlValid("https://www.facebook.com", "https://www.facebook.com"));
            Assert.IsTrue(UrlValidation.IsUrlValid("http://www.facebook.com", "facebook.com"));
            Assert.IsTrue(UrlValidation.IsUrlValid("HTTPS://WWW.FACEBOOK.COM", "facebook.com"));
            Assert.IsTrue(UrlValidation.IsUrlValid("https://www.facebook.com", "FACEBOOK.COM"));
            Assert.IsTrue(UrlValidation.IsUrlValid("https://www.facebook.com", "www.facebook.com"));
        }

        [TestMethod]
        public void PathsAndQueryStringsDoNotStopADomainMatching()
        {
            Assert.IsTrue(UrlValidation.IsUrlValid("https://www.facebook.com/feed", "facebook.com"));
            Assert.IsTrue(UrlValidation.IsUrlValid("https://facebook.com/?ref=1", "facebook.com"));
        }

        [TestMethod]
        public void AnUnrelatedUrlDoesNotMatch()
        {
            Assert.IsFalse(UrlValidation.IsUrlValid("https://github.com", "facebook.com"));
            Assert.IsFalse(UrlValidation.IsUrlValid(string.Empty, "facebook.com"));
        }

        // MARK: - Subdomains

        [TestMethod]
        public void ABareDomainAlsoCoversItsSubdomains()
        {
            // The shipped behaviour: blocking or allowing "zoho.com" also covers "desk.zoho.com".
            Assert.IsTrue(UrlValidation.IsUrlValid("https://desk.zoho.com", "zoho.com"));
            Assert.IsTrue(UrlValidation.IsUrlValid("https://desk.zoho.com", "*zoho.com"));
            Assert.IsTrue(UrlValidation.IsUrlValid("https://m.facebook.com", "facebook"));
        }

        [TestMethod]
        public void AnEntryWrittenWithALeadingStarAndDotRequiresTheDot()
        {
            Assert.IsTrue(UrlValidation.IsUrlValid("https://desk.zoho.com", "*.zoho.com"));
            Assert.IsFalse(UrlValidation.IsUrlValid("https://zoho.com", "*.zoho.com"),
                "\"*.zoho.com\" needs a leading label, so it misses the bare domain");
        }

        // MARK: - Path-scoped entries

        [TestMethod]
        public void APathScopedEntryMatchesThatPathAndBelow()
        {
            Assert.IsFalse(UrlValidation.IsUrlValid("github.com", "github.com/ram"));
            Assert.IsTrue(UrlValidation.IsUrlValid("github.com/ram", "github.com/ram"));
            Assert.IsTrue(UrlValidation.IsUrlValid("github.com/ram/shyam", "github.com/ram"));
        }

        // MARK: - x.com, Mac-App#1209

        [TestMethod]
        public void XComDoesNotMatchEveryHostEndingInIt()
        {
            // "x.com" as a bare substring used to block netflix.com, max.com and friends.
            Assert.IsTrue(UrlValidation.IsUrlValid("https://x.com", "x.com"));
            Assert.IsTrue(UrlValidation.IsUrlValid("https://www.x.com", "x.com"));
            Assert.IsFalse(UrlValidation.IsUrlValid("https://netflix.com", "x.com"));
            Assert.IsFalse(UrlValidation.IsUrlValid("https://max.com", "x.com"));
            Assert.IsFalse(UrlValidation.IsUrlValid("https://twitter.com", "x.com"));
        }

        [TestMethod]
        public void XComStillMatchesARealSubdomain()
        {
            Assert.IsTrue(UrlValidation.IsUrlValid("https://a.x.com", "x.com"),
                "a dot, not a letter, precedes the match");
            Assert.IsTrue(UrlValidation.IsUrlValid("https://sub.x.com/path", "x.com"));
        }

        [TestMethod]
        public void RemainingEdgesOfTheXComGuard()
        {
            // Nothing precedes the match in "x.company", so the guard never fires and it matches.
            Assert.IsTrue(UrlValidation.IsUrlValid("https://x.company", "x.com"));
            // Only the FIRST occurrence is examined: the "x" of "vox" rejects the URL before the
            // real x.com later in the path is ever looked at.
            Assert.IsFalse(UrlValidation.IsUrlValid("https://vox.com/x.com", "x.com"));
        }

        // MARK: - file:// entries

        [TestMethod]
        public void AFileEntryMatchesOnlyAUrlWithAScheme()
        {
            Assert.IsTrue(UrlValidation.IsUrlValid("file:///Users/a/photo.jpg", "file:///*"));
            Assert.IsFalse(UrlValidation.IsUrlValid("https://x.com", "file:///*"));
        }

        [TestMethod]
        public void AFileEntryIsTheOneCaseSensitiveComparison()
        {
            // The file branch matches the URL as typed, not the lower-cased copy, so an upper-case
            // scheme misses. Preserved deliberately: this is the Mac's behaviour, and quietly
            // case-folding it would allow a file the user never allowed.
            Assert.IsFalse(UrlValidation.IsUrlValid("FILE:///Users/A/P.JPG", "file:///*"));
        }

        // MARK: - What the substring matcher lets through (verified against the Swift)

        [TestMethod]
        public void SubstringMatchingOverBlocksLookalikeHosts()
        {
            // Not a guard — a consequence. "facebook.com" blocks hosts that merely contain it.
            Assert.IsTrue(UrlValidation.IsUrlValid("https://notfacebook.com", "facebook.com"));
            Assert.IsTrue(UrlValidation.IsUrlValid("https://facebook.com.evil.example", "facebook.com"));
        }

        [TestMethod]
        public void SubstringMatchingAlsoMatchesTheEntryInsideAPathOrQuery()
        {
            // This is the direction that matters: used as an ALLOW list, an entry for "github.com"
            // also allows an unrelated host that merely mentions it, because nothing here
            // distinguishes the authority from the rest of the URL. Pinned so that a future
            // tightening has to be a deliberate, matched change on both clients.
            Assert.IsTrue(UrlValidation.IsUrlValid("https://evil.example/github.com", "github.com"));
            Assert.IsTrue(UrlValidation.IsUrlValid("https://evil.example/?u=facebook.com", "facebook.com"));
            Assert.IsTrue(UrlValidation.IsUrlValid("https://github.com@evil.example/", "github.com"));
            Assert.IsTrue(UrlValidation.IsUrlValid("https://evil.example/www.github.com", "github.com"),
                "\"www.\" is stripped everywhere, not only at the front");
            Assert.IsTrue(UrlValidation.IsUrlValid("https://a.example/redirect?to=http://github.com", "github.com"),
                "the scheme is stripped everywhere too");
        }

        [TestMethod]
        public void APortDoesNotPreventADomainMatch()
        {
            Assert.IsTrue(UrlValidation.IsUrlValid("https://github.com:8443/x", "github.com"));
        }

        // MARK: - isUrlAlwaysBlocked, the anchored matcher (#1233)

        [TestMethod]
        public void AnAlwaysBlockedDomainEntryIsAnchoredAtTheStart()
        {
            Assert.IsTrue(UrlValidation.IsUrlAlwaysBlocked("https://facebook.com/feed", "facebook.com"));
            // The genuine disagreement with IsUrlValid: a subdomain does NOT match here.
            Assert.IsFalse(UrlValidation.IsUrlAlwaysBlocked("https://m.facebook.com", "facebook.com"));
            Assert.IsFalse(UrlValidation.IsUrlAlwaysBlocked("https://desk.zoho.com", "zoho.com"));
            Assert.IsFalse(UrlValidation.IsUrlAlwaysBlocked("https://notfacebook.com", "facebook.com"));
        }

        [TestMethod]
        public void AnAlwaysBlockedKeywordEntryIsNotAnchored()
        {
            // No dot, so it is treated as a keyword and matched anywhere.
            Assert.IsTrue(UrlValidation.IsUrlAlwaysBlocked("https://m.facebook.com/messages", "facebook"));
        }

        [TestMethod]
        public void AnchoringStillAdmitsALookalikeSuffix()
        {
            // Starting with the entry is not the same as BEING the entry's host.
            Assert.IsTrue(UrlValidation.IsUrlAlwaysBlocked("https://facebook.com.evil.example", "facebook.com"));
        }

        [TestMethod]
        public void AnEmbeddedCredentialDefeatsTheAlwaysBlockedList()
        {
            // "user:pw@facebook.com/" does not START with the entry, so the anchored matcher misses
            // it entirely — a blocked site reachable by typing a username in front of it. Pinned as
            // the Mac's behaviour; ManagedResource is the matcher that refuses such a URL outright.
            Assert.IsFalse(UrlValidation.IsUrlAlwaysBlocked("https://user:pw@facebook.com/", "facebook.com"));
            Assert.IsNull(ManagedResource.TryCreate("https://user:pw@facebook.com/"),
                "the strict matcher rejects the same URL rather than mis-reading it");
        }
    }
}
