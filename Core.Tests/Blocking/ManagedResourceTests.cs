using System;
using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Blocking
{
    /// <summary>
    /// The 21 cases of <c>FocusBearTests/Fixtures/managed-url-normalization.v1.json</c>, transcribed
    /// verbatim — inputs, expected canonical URLs, expected hosts AND expected SHA-256 hashes.
    ///
    /// That fixture is a shared contract: the Mac reads it, and the backend module has its own copy.
    /// It is inlined here rather than copied as a file because a data file would need a change to the
    /// test project, and the whole point of this library is that it builds untouched. Asserting the
    /// hashes as well as the strings is what makes this a real cross-platform check: a hash can only
    /// match if every byte of the canonical form does.
    /// </summary>
    [TestClass]
    public class ManagedResourceTests
    {
        private sealed class FixtureCase
        {
            public FixtureCase(string id, string input, string normalizedUrl, string host, string resourceHash)
            {
                Id = id;
                Input = input;
                NormalizedUrl = normalizedUrl;
                Host = host;
                ResourceHash = resourceHash;
            }

            public FixtureCase(string id, string input) : this(id, input, null, null, null) { }

            public string Id { get; }
            public string Input { get; }
            public string NormalizedUrl { get; }
            public string Host { get; }
            public string ResourceHash { get; }
            public bool Valid { get { return NormalizedUrl != null; } }
        }

        private static readonly FixtureCase[] Fixture =
        {
            new FixtureCase("bare-host", "EXAMPLE.org", "https://example.org/", "example.org",
                "8198d1bac40a1033653a78e48800cefc9e6b974ff075c66e5548b5c1e145a2b0"),
            new FixtureCase("trailing-dot-fragment", "https://EXAMPLE.org./a#private", "https://example.org/a", "example.org",
                "b5b10dd0429e69ac7cfaf0a20b6901c1265c50b55d1751bcb684ed96ef7b300a"),
            new FixtureCase("unreserved-percent", "https://example.org/%61/%7euser", "https://example.org/a/~user", "example.org",
                "dd63e0fc31a8eeeb46441433b2126f2586d7e762687d299e71cac2ded9780ad3"),
            new FixtureCase("reserved-percent", "https://example.org/a%2fb", "https://example.org/a%2Fb", "example.org",
                "d544ef85d17387d81ae021b97a9c8331363372747c8cd514d4d5f211ba7686b4"),
            new FixtureCase("dot-segments", "https://example.org/lesson/%2e%2e/game", "https://example.org/game", "example.org",
                "608f2de99e0a403b2c2deb9bb960a2ba7d2cb1da85f0c5526a0b94454d1791ab"),
            new FixtureCase("query-preserved", "https://example.org/a?token=secret#discard", "https://example.org/a?token=secret", "example.org",
                "928a683a812250fca2773c6c070e06988c1ecf25443252e7b591c6bc19d78046"),
            new FixtureCase("query-order-preserved", "https://example.org/?b=2&a=1", "https://example.org/?b=2&a=1", "example.org",
                "b2afbf3fed2b387f73543de7d334d2417b569ec33a028ceb08c914781b9bb634"),
            new FixtureCase("empty-query", "https://example.org/?", "https://example.org/", "example.org",
                "8198d1bac40a1033653a78e48800cefc9e6b974ff075c66e5548b5c1e145a2b0"),
            new FixtureCase("default-port", "https://example.org:443/a", "https://example.org/a", "example.org",
                "b5b10dd0429e69ac7cfaf0a20b6901c1265c50b55d1751bcb684ed96ef7b300a"),
            new FixtureCase("explicit-port", "https://example.org:8443/a", "https://example.org:8443/a", "example.org",
                "9b80290f5309859e46ab4ff5047b742d84c4851577ee5ca3f5b95bdf29d0cf4e"),
            new FixtureCase("idn", "https://bücher.example/", "https://xn--bcher-kva.example/", "xn--bcher-kva.example",
                "7971a8be6267ce24bde810901c82e8c4ada53555d5b75e746486b392c1e9eede"),
            new FixtureCase("ipv6", "https://[2001:0db8::1]/", "https://[2001:db8::1]/", "2001:db8::1",
                "a29c5e99701feded870a3e8098e7ec0e39fd2f374341502a33b1bc73704431f6"),
            new FixtureCase("ipv4-short", "https://127.1/", "https://127.0.0.1/", "127.0.0.1",
                "ddd0780b683530a71ff3a08afee3330e5e1aea6328d8d09d227fddab53779482"),
            new FixtureCase("credentials", "https://user:secret@example.org/"),
            new FixtureCase("unsupported-scheme", "ftp://example.org/file"),
            new FixtureCase("backslash", "https://example.org\\evil"),
            new FixtureCase("whitespace", "https://example.org/a b"),
            new FixtureCase("bad-percent", "https://example.org/%ZZ"),
            new FixtureCase("missing-host", "https:///"),
            new FixtureCase("control", "https://example.org/\n"),
            // The fixture spells this one out as 4096 "a"s after the prefix, for 4116 characters.
            new FixtureCase("too-long", "https://example.org/" + Repeat('a', 4096)),
        };

        private static string Repeat(char character, int count)
        {
            return new string(character, count);
        }

        [TestMethod]
        public void SharedUrlNormalizationAndResourceHashCases()
        {
            Assert.AreEqual(21, Fixture.Length, "the shared fixture has 21 cases");

            foreach (FixtureCase item in Fixture)
            {
                ManagedResource resource = ManagedResource.TryCreate(item.Input);
                Assert.AreEqual(item.Valid, resource != null, item.Id);
                if (!item.Valid) continue;

                Assert.AreEqual(item.NormalizedUrl, resource.NormalizedUrl, item.Id);
                Assert.AreEqual(item.Host, resource.Host, item.Id);
                Assert.AreEqual(item.ResourceHash, resource.Hash, item.Id);
            }
        }

        // MARK: - The length bound

        [TestMethod]
        public void TheLengthBoundIsInclusive()
        {
            string atLimit = "https://example.org/" + Repeat('a', ManagedResource.MaximumLength - 20);
            Assert.AreEqual(ManagedResource.MaximumLength, atLimit.Length);
            Assert.IsNotNull(ManagedResource.TryCreate(atLimit));
            Assert.IsNull(ManagedResource.TryCreate(atLimit + "a"));
        }

        // MARK: - Rejections, stated as what each one prevents

        [TestMethod]
        public void AnyEmbeddedCredentialIsRejectedNotParsed()
        {
            // The part before "@" is a username; a reader sees the wrong site. There is no safe
            // reading, so no reading is attempted — including the password-less form, which is the
            // one that actually gets used.
            Assert.IsNull(ManagedResource.TryCreate("https://user:secret@example.org/"));
            Assert.IsNull(ManagedResource.TryCreate("https://trusted.example@evil.example/"));
            Assert.IsNull(ManagedResource.TryCreate("https://@example.org/"));
        }

        [TestMethod]
        public void ControlCharactersSpacesAndBackslashesAreRejected()
        {
            // Browsers disagree about where these end the host, so any answer could be the wrong one.
            Assert.IsNull(ManagedResource.TryCreate("https://example.org/a b"));
            Assert.IsNull(ManagedResource.TryCreate("https://example.org/\n"));
            Assert.IsNull(ManagedResource.TryCreate("https://example.org/\t"));
            Assert.IsNull(ManagedResource.TryCreate("https://example.org/\0"));
            Assert.IsNull(ManagedResource.TryCreate("https://example.org/\u007f"));
            Assert.IsNull(ManagedResource.TryCreate("https://example.org\\evil"));
            Assert.IsNull(ManagedResource.TryCreate("https://example.org/a\\..\\b"));
        }

        [TestMethod]
        public void MalformedPercentEscapesInThePathAreRejected()
        {
            Assert.IsNull(ManagedResource.TryCreate("https://example.org/%ZZ"));
            Assert.IsNull(ManagedResource.TryCreate("https://example.org/%4"));
            Assert.IsNull(ManagedResource.TryCreate("https://example.org/%"));
            Assert.IsNull(ManagedResource.TryCreate("https://example.org/%4g"));
        }

        [TestMethod]
        public void OnlyHttpAndHttpsAreUnderstood()
        {
            Assert.IsNull(ManagedResource.TryCreate("ftp://example.org/file"));
            Assert.IsNull(ManagedResource.TryCreate("javascript://example.org/"));
            Assert.IsNull(ManagedResource.TryCreate("file:///Users/a/p.jpg"));
            Assert.IsNotNull(ManagedResource.TryCreate("HTTP://example.org/"));
        }

        [TestMethod]
        public void AMissingHostIsRejected()
        {
            Assert.IsNull(ManagedResource.TryCreate("https:///"));
            Assert.IsNull(ManagedResource.TryCreate("https://./"));
            Assert.IsNull(ManagedResource.TryCreate("https://:443/"));
        }

        // MARK: - Rewrites, each one a spelling that would otherwise dodge a comparison

        [TestMethod]
        public void ABareHostIsReadAsHttps()
        {
            Assert.AreEqual("https://example.org/", ManagedResource.TryCreate("example.org").NormalizedUrl);
            Assert.AreEqual("https://example.org/a", ManagedResource.TryCreate("example.org/a").NormalizedUrl);
        }

        [TestMethod]
        public void TheFragmentIsDroppedAndTheQueryIsKeptInOrder()
        {
            // The fragment never reaches the server, so it cannot select a page. The query routinely
            // does, so it is preserved exactly — including an order another client might have sorted.
            Assert.AreEqual("https://example.org/a?token=secret",
                ManagedResource.TryCreate("https://example.org/a?token=secret#discard").NormalizedUrl);
            Assert.AreEqual("https://example.org/?b=2&a=1",
                ManagedResource.TryCreate("https://example.org/?b=2&a=1").NormalizedUrl);
            Assert.AreEqual("https://example.org/a",
                ManagedResource.TryCreate("https://example.org/a#b?c").NormalizedUrl,
                "a \"?\" inside the fragment is not a query");
        }

        [TestMethod]
        public void DefaultPortsDisappearAndOthersDoNot()
        {
            Assert.IsNull(ManagedResource.TryCreate("https://example.org:443/a").Port);
            Assert.IsNull(ManagedResource.TryCreate("http://example.org:80/a").Port);
            Assert.AreEqual(8443, ManagedResource.TryCreate("https://example.org:8443/a").Port);
            Assert.AreEqual(443, ManagedResource.TryCreate("http://example.org:443/a").Port,
                "443 is not http's default, so it stays — the two are different services");
        }

        [TestMethod]
        public void EveryIpv4SpellingCollapsesToDottedQuad()
        {
            // Short, octal and hex forms all name the same address, so a policy written against one
            // cannot be walked around by typing another.
            Assert.AreEqual("127.0.0.1", ManagedResource.TryCreate("https://127.1/").Host);
            Assert.AreEqual("127.0.0.1", ManagedResource.TryCreate("https://2130706433/").Host);
            Assert.AreEqual("127.0.0.1", ManagedResource.TryCreate("https://0x7f.0.0.1/").Host);
            Assert.AreEqual("127.0.0.1", ManagedResource.TryCreate("https://0177.0.0.1/").Host);
            Assert.AreEqual("192.168.0.1", ManagedResource.TryCreate("https://192.168.1/").Host);
        }

        [TestMethod]
        public void AnOutOfRangeNumericHostIsRejectedRatherThanWrapped()
        {
            Assert.IsNull(ManagedResource.TryCreate("https://256.0.0.1/"));
            Assert.IsNull(ManagedResource.TryCreate("https://1.2.3.4.5/"));
            Assert.IsNull(ManagedResource.TryCreate("https://0x100000000/"));
        }

        [TestMethod]
        public void AHostnameThatMerelyContainsDigitsIsLeftAlone()
        {
            Assert.AreEqual("v2.example.org", ManagedResource.TryCreate("https://v2.example.org/").Host);
            Assert.AreEqual("example.org", ManagedResource.TryCreate("https://example.org/").Host);
        }

        [TestMethod]
        public void Ipv6IsCompressedAndKeepsItsBracketsOnlyInTheUrl()
        {
            ManagedResource resource = ManagedResource.TryCreate("https://[2001:0db8:0000::1]/");
            Assert.AreEqual("https://[2001:db8::1]/", resource.NormalizedUrl);
            Assert.AreEqual("2001:db8::1", resource.Host, "the host itself has no brackets");
            Assert.IsNull(ManagedResource.TryCreate("https://[not:an:address]/"));
            Assert.IsNull(ManagedResource.TryCreate("https://[2001:db8::1%eth0]/"),
                "a scope id is a local concept and cannot identify a remote host");
        }

        [TestMethod]
        public void DotSegmentsAreResolvedBeforeAnythingIsCompared()
        {
            Assert.AreEqual("https://example.org/game",
                ManagedResource.TryCreate("https://example.org/lesson/../game").NormalizedUrl);
            Assert.AreEqual("https://example.org/game",
                ManagedResource.TryCreate("https://example.org/lesson/%2e%2e/game").NormalizedUrl,
                "an encoded \"..\" is decoded first, so it cannot smuggle a traversal past the check");
            Assert.AreEqual("https://example.org/a",
                ManagedResource.TryCreate("https://example.org/./a").NormalizedUrl);
            Assert.AreEqual("https://example.org/",
                ManagedResource.TryCreate("https://example.org/../../..").NormalizedUrl,
                "climbing above the root stops at the root");
        }

        [TestMethod]
        public void AnEncodedSlashStaysEncodedAndIsNotASeparator()
        {
            // The reverse of the rule above, and just as important: "%2f" must NOT become a path
            // separator, or "a%2F..%2Fb" would resolve to something the server never serves.
            Assert.AreEqual("https://example.org/a%2Fb",
                ManagedResource.TryCreate("https://example.org/a%2fb").NormalizedUrl);
            Assert.AreEqual("https://example.org/a%2F..%2Fb",
                ManagedResource.TryCreate("https://example.org/a%2f..%2fb").NormalizedUrl);
        }

        [TestMethod]
        public void UnreservedEscapesAreDecodedAndReservedOnesAreUpperCased()
        {
            Assert.AreEqual("https://example.org/a/~user",
                ManagedResource.TryCreate("https://example.org/%61/%7euser").NormalizedUrl);
            Assert.AreEqual("https://example.org/a%20b",
                ManagedResource.TryCreate("https://example.org/a%20b").NormalizedUrl);
        }

        [TestMethod]
        public void TwoSpellingsOfOnePageProduceOneHash()
        {
            // The property the hash exists for.
            Assert.AreEqual(ManagedResource.TryCreate("EXAMPLE.org").Hash,
                            ManagedResource.TryCreate("https://example.org:443/").Hash);
            Assert.AreNotEqual(ManagedResource.TryCreate("https://example.org/a").Hash,
                               ManagedResource.TryCreate("https://example.org/b").Hash);
        }

        [TestMethod]
        public void NullIsNotAnError()
        {
            Assert.IsNull(ManagedResource.TryCreate(null));
        }
    }
}
