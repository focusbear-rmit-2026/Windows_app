using System.Collections.Generic;
using System.Linq;
using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Blocking
{
    /// <summary>
    /// Ported from the Mac app's <c>BlockListCategoryTests.swift</c>: that a selection round-trips,
    /// that it encodes to the exact wire shape the backend expects, and that categories ride
    /// top-level on a focus-mode write rather than inside the <c>metadata</c> blob (which every save
    /// replaces wholesale, and which an older client would therefore wipe).
    /// </summary>
    [TestClass]
    public class BlockListCategoryTests
    {
        private static IDictionary<string, string> WireRow(string entry, string category)
        {
            return new Dictionary<string, string> { { "entry", entry }, { "category", category } };
        }

        private static void AssertWireEquals(IReadOnlyList<IDictionary<string, string>> actual,
                                             params string[] entryThenCategoryPairs)
        {
            Assert.AreEqual(entryThenCategoryPairs.Length / 2, actual.Count, "row count");
            for (int row = 0; row < actual.Count; row++)
            {
                Assert.AreEqual(entryThenCategoryPairs[row * 2], actual[row]["entry"]);
                Assert.AreEqual(entryThenCategoryPairs[row * 2 + 1], actual[row]["category"]);
                Assert.AreEqual(2, actual[row].Count, "a row carries exactly entry and category");
            }
        }

        // MARK: - Wire shape

        [TestMethod]
        public void EncodingProducesEntryAndCategoryPairsSortedByEntry()
        {
            IReadOnlyList<IDictionary<string, string>> encoded = BlockListCategories.EncodeCategories(
                new Dictionary<string, DistractionCategory>
                {
                    { "instagram.com", DistractionCategory.SocialMedia },
                    { "bbc.com", DistractionCategory.News },
                });

            // Sorted by entry, so an unrelated edit can't churn the payload.
            AssertWireEquals(encoded, "bbc.com", "news", "instagram.com", "social_media");
        }

        [TestMethod]
        public void EntriesSortAsTheMacSortsThem()
        {
            // Swift: ["f.com", "e\u{301}mail.example", "zoom.us"].sorted() puts the accented entry
            // LAST. Ordinal put it first, so a save from each client wrote a different payload.
            IReadOnlyList<IDictionary<string, string>> encoded = BlockListCategories.EncodeCategories(
                new Dictionary<string, DistractionCategory>
                {
                    { "e\u0301mail.example", DistractionCategory.News },
                    { "zoom.us", DistractionCategory.News },
                    { "f.com", DistractionCategory.News },
                });

            AssertWireEquals(encoded, "f.com", "news", "zoom.us", "news", "e\u0301mail.example", "news");
        }

        [TestMethod]
        public void CategoryWireValuesAreTheStableIdentifiers()
        {
            // These strings are persisted and sent to the backend; renaming one silently re-files
            // every entry a user has already categorised.
            Assert.AreEqual("social_media", DistractionCategories.WireValue(DistractionCategory.SocialMedia));
            Assert.AreEqual("news", DistractionCategories.WireValue(DistractionCategory.News));
            Assert.AreEqual("video", DistractionCategories.WireValue(DistractionCategory.Video));
            Assert.AreEqual("gaming", DistractionCategories.WireValue(DistractionCategory.Gaming));
            Assert.AreEqual("dating", DistractionCategories.WireValue(DistractionCategory.Dating));
            Assert.AreEqual("real_estate", DistractionCategories.WireValue(DistractionCategory.RealEstate));
            Assert.AreEqual("porn", DistractionCategories.WireValue(DistractionCategory.Porn));
            Assert.AreEqual("gambling", DistractionCategories.WireValue(DistractionCategory.Gambling));
            Assert.AreEqual("other", DistractionCategories.WireValue(DistractionCategory.Other));
        }

        // MARK: - Round trip

        [TestMethod]
        public void DecodingReadsTheStoredWireShape()
        {
            IDictionary<string, DistractionCategory> assigned = BlockListCategories.AssignedCategories(new[]
            {
                WireRow("instagram.com", "social_media"),
                WireRow("bbc.com", "news"),
            });

            Assert.AreEqual(DistractionCategory.SocialMedia, assigned["instagram.com"]);
            Assert.AreEqual(DistractionCategory.News, assigned["bbc.com"]);
        }

        [TestMethod]
        public void ASelectionRoundTripsThroughTheWireShape()
        {
            IReadOnlyList<IDictionary<string, string>> encoded = BlockListCategories.EncodeCategories(
                new Dictionary<string, DistractionCategory> { { "instagram.com", DistractionCategory.SocialMedia } });

            Assert.AreEqual(DistractionCategory.SocialMedia,
                BlockListCategories.AssignedCategories(encoded)["instagram.com"]);
        }

        [TestMethod]
        public void UnknownCategoryIdsAreDroppedRatherThanFailingTheWholeDecode()
        {
            // A newer client (or another platform) may file an entry under a category this build has
            // never heard of. That entry should degrade to uncategorised, not break the screen.
            IDictionary<string, DistractionCategory> assigned = BlockListCategories.AssignedCategories(new[]
            {
                WireRow("instagram.com", "social_media"),
                WireRow("example.com", "a_category_from_the_future"),
            });

            Assert.AreEqual(DistractionCategory.SocialMedia, assigned["instagram.com"]);
            Assert.IsFalse(assigned.ContainsKey("example.com"));
        }

        [TestMethod]
        public void MalformedRowsAreDroppedToo()
        {
            IDictionary<string, DistractionCategory> assigned = BlockListCategories.AssignedCategories(new[]
            {
                WireRow(string.Empty, "news"),
                new Dictionary<string, string> { { "category", "news" } },
                new Dictionary<string, string> { { "entry", "lonely.com" } },
                WireRow("kept.com", "news"),
            });

            Assert.AreEqual(1, assigned.Count);
            Assert.AreEqual(DistractionCategory.News, assigned["kept.com"]);
        }

        [TestMethod]
        public void AFocusModeWithNoCategoriesDecodesToAnEmptyMap()
        {
            Assert.AreEqual(0, BlockListCategories.AssignedCategories(null).Count);
            Assert.AreEqual(0, BlockListCategories.AssignedCategories(new IDictionary<string, string>[0]).Count);
        }

        // MARK: - Pruning

        [TestMethod]
        public void CategoriesForEntriesNoLongerInTheListArePruned()
        {
            // An older client can delete a blocked URL without knowing categories exist, stranding
            // the row. Pruning happens on the next write so the stored list can't only ever grow.
            var assigned = new Dictionary<string, DistractionCategory>
            {
                { "instagram.com", DistractionCategory.SocialMedia },
                { "deleted-elsewhere.com", DistractionCategory.News },
            };

            IDictionary<string, DistractionCategory> kept =
                BlockListCategories.PruneOrphanedCategories(assigned, new[] { "instagram.com" });

            Assert.AreEqual(DistractionCategory.SocialMedia, kept["instagram.com"]);
            Assert.IsFalse(kept.ContainsKey("deleted-elsewhere.com"));
        }

        [TestMethod]
        public void PruningLeavesAFullyLiveCategoryListAlone()
        {
            var assigned = new Dictionary<string, DistractionCategory>
            {
                { "instagram.com", DistractionCategory.SocialMedia },
            };

            IDictionary<string, DistractionCategory> kept =
                BlockListCategories.PruneOrphanedCategories(assigned, new[] { "instagram.com" });

            Assert.AreEqual(1, kept.Count);
            Assert.AreEqual(DistractionCategory.SocialMedia, kept["instagram.com"]);
        }

        // MARK: - Sync payload

        [TestMethod]
        public void CategoriesAreSentTopLevelAndStrippedFromMetadata()
        {
            // The PATCH replaces `metadata` wholesale on every save, so categories stored there would
            // be wiped by any client that doesn't know about them. They must ride top-level.
            IReadOnlyList<IDictionary<string, string>> stored = new[] { WireRow("instagram.com", "social_media") };
            var focusType = new Dictionary<string, object> { { "categories", stored } };
            var json = new Dictionary<string, object> { { "name", "Deep Work" } };
            var metadata = new Dictionary<string, object>(focusType);

            BlockListCategories.ApplyFocusModeCategories(focusType, json, metadata);

            Assert.AreSame(stored, json["categories"]);
            Assert.IsFalse(metadata.ContainsKey("categories"),
                "categories must not also ride inside the metadata blob");
        }

        [TestMethod]
        public void AFocusModeWithoutCategoriesSendsAnEmptyList()
        {
            var focusType = new Dictionary<string, object> { { "name", "Deep Work" } };
            var json = new Dictionary<string, object>();
            var metadata = new Dictionary<string, object>();

            BlockListCategories.ApplyFocusModeCategories(focusType, json, metadata);

            var sent = json["categories"] as IEnumerable<IDictionary<string, string>>;
            Assert.IsNotNull(sent, "an empty LIST, not a missing key — omitted means \"unchanged\" on the backend");
            Assert.AreEqual(0, sent.Count());
        }

        // MARK: - Catalog

        [TestMethod]
        public void SeedDomainsAreClassifiedIntoTheirOwnCategory()
        {
            Assert.AreEqual(DistractionCategory.SocialMedia, DistractionCategoryCatalog.InferredCategory("instagram.com"));
            Assert.AreEqual(DistractionCategory.News, DistractionCategoryCatalog.InferredCategory("www.bbc.com/news"));
            Assert.AreEqual(DistractionCategory.Video, DistractionCategoryCatalog.InferredCategory("netflix"));
            Assert.AreEqual(DistractionCategory.Dating, DistractionCategoryCatalog.InferredCategory("tinder.com"));
            Assert.AreEqual(DistractionCategory.RealEstate, DistractionCategoryCatalog.InferredCategory("zillow.com"));
        }

        [TestMethod]
        public void AnUnknownDomainIsNotForcedIntoACategory()
        {
            Assert.IsNull(DistractionCategoryCatalog.InferredCategory("some-internal-tool.example"));
        }

        [TestMethod]
        public void VeryShortEntriesAreNotMatchedBySubstring()
        {
            // "x" would otherwise match x.com and half the catalog.
            Assert.IsNull(DistractionCategoryCatalog.InferredCategory("x"));
            Assert.IsNull(DistractionCategoryCatalog.InferredCategory("xy"));
        }

        [TestMethod]
        public void AdultAndBettingCategoriesShipNoSeedDomains()
        {
            Assert.AreEqual(0, DistractionCategoryCatalog.SeedEntries(DistractionCategory.Porn).Count);
            Assert.AreEqual(0, DistractionCategoryCatalog.SeedEntries(DistractionCategory.Gambling).Count);
            Assert.AreEqual(0, DistractionCategoryCatalog.SeedEntries(DistractionCategory.Other).Count);
        }

        // MARK: - Taxonomy ordering

        [TestMethod]
        public void OnboardingShowsTheHistoricalSubsetOfTheSharedTaxonomy()
        {
            var onboarding = DistractionCategories.All.Where(DistractionCategories.AppearsInOnboarding).ToList();

            CollectionAssert.AreEquivalent(
                new[] { DistractionCategory.SocialMedia, DistractionCategory.News, DistractionCategory.Video,
                        DistractionCategory.Dating, DistractionCategory.RealEstate },
                onboarding);
            // The block-list screen offers strictly more, from the same enum — one taxonomy, not two.
            Assert.IsTrue(DistractionCategories.OrderedForFiltering.Count > onboarding.Count);
        }

        [TestMethod]
        public void OtherSortsLastAmongTheFilterChips()
        {
            IReadOnlyList<DistractionCategory> ordered = DistractionCategories.OrderedForFiltering;
            Assert.AreEqual(DistractionCategory.Other, ordered[ordered.Count - 1]);
            CollectionAssert.AreEquivalent(DistractionCategories.All.ToArray(), ordered.ToArray(),
                "every category must be reachable as a filter");
        }

        // MARK: - Card presentation

        [TestMethod]
        public void ALeadingEmojiIsLiftedOutOfTheBlockListName()
        {
            Assert.AreEqual("🤚", BlockListCategories.LeadingEmoji("🤚 Deep Work"));
            Assert.AreEqual("💻", BlockListCategories.LeadingEmoji("💻 No work in the evening"));
        }

        [TestMethod]
        public void ANameWithoutAnEmojiHasNone()
        {
            Assert.IsNull(BlockListCategories.LeadingEmoji("Deep Work"));
            // Digits carry an emoji property in Unicode; they must not be mistaken for one.
            Assert.IsNull(BlockListCategories.LeadingEmoji("1Password"));
            Assert.IsNull(BlockListCategories.LeadingEmoji(string.Empty));
            Assert.IsNull(BlockListCategories.LeadingEmoji(null));
        }

        [TestMethod]
        public void AMultiScalarEmojiIsKeptWhole()
        {
            // A flag is two regional indicators; returning half of one would render as a stray letter.
            Assert.AreEqual("🇦🇺", BlockListCategories.LeadingEmoji("🇦🇺 Work hours"));
        }
    }
}
