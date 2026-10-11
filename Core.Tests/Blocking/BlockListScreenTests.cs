using System.Linq;
using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Blocking
{
    [TestClass]
    public class BlockListScreenTests
    {
        private static BlockListModeFlags Custom(bool allowList = false, bool allAppsAllowed = true) =>
            new BlockListModeFlags { IsAllowedListApproach = allowList, IsAllowedAllApps = allAppsAllowed };

        // MARK: - Eligibility

        [TestMethod]
        public void BuiltInModesCannotBeDeletedOrRenamed()
        {
            foreach (var mode in new[]
            {
                new BlockListModeFlags { IsDefault = true }, new BlockListModeFlags { IsMeetings = true },
                new BlockListModeFlags { IsLockedFocus = true }, new BlockListModeFlags { IsRelaxFocus = true },
                new BlockListModeFlags { IsDeepWorkFocus = true },
            })
            {
                Assert.IsFalse(BlockListRules.CanDelete(mode));
                Assert.IsFalse(BlockListRules.CanRename(mode));
            }
            Assert.IsTrue(BlockListRules.CanDelete(Custom()));
            Assert.IsTrue(BlockListRules.CanRename(Custom()));
        }

        [TestMethod]
        public void OnlyTheDefaultAndLockedModesCannotBeDuplicated()
        {
            Assert.IsFalse(BlockListRules.CanDuplicate(new BlockListModeFlags { IsDefault = true }));
            Assert.IsFalse(BlockListRules.CanDuplicate(new BlockListModeFlags { IsLockedFocus = true }));
            Assert.IsTrue(BlockListRules.CanDuplicate(new BlockListModeFlags { IsMeetings = true }));
            Assert.IsTrue(BlockListRules.CanDuplicate(Custom()));
        }

        [TestMethod]
        public void LockedFocusIsNeverListed()
        {
            Assert.IsFalse(BlockListRules.ShowsInList(new BlockListModeFlags { IsLockedFocus = true }));
            Assert.IsTrue(BlockListRules.ShowsInList(new BlockListModeFlags { IsDefault = true }));
        }

        [TestMethod]
        public void AdvancedIsOnlyForAllowListModesThatAreNotDefault()
        {
            Assert.IsTrue(BlockListRules.AdvancedAvailable(Custom(allowList: true)));
            Assert.IsFalse(BlockListRules.AdvancedAvailable(Custom(allowList: false)));
            Assert.IsFalse(BlockListRules.AdvancedAvailable(new BlockListModeFlags { IsDefault = true, IsAllowedListApproach = true }));
        }

        [TestMethod]
        public void ApproachChoicesAreHiddenForTheDefaultAndRelaxModes()
        {
            Assert.IsFalse(BlockListRules.ShowsApproachChoices(new BlockListModeFlags { IsDefault = true }));
            Assert.IsFalse(BlockListRules.ShowsApproachChoices(new BlockListModeFlags { IsRelaxFocus = true }));
            Assert.IsTrue(BlockListRules.ShowsApproachChoices(new BlockListModeFlags { IsMeetings = true }));
        }

        // MARK: - Which lists a mode edits

        [TestMethod]
        public void TheDefaultModeBlocksItsAppsAndWebsitesWhateverItsFlagsSay()
        {
            var mode = new BlockListModeFlags { IsDefault = true, IsAllowedAllApps = true, IsAllowedListApproach = true };
            Assert.IsTrue(BlockListRules.ShowsApps(mode));
            Assert.IsFalse(BlockListRules.AppsAreAllowed(mode));
            Assert.IsFalse(BlockListRules.WebsitesAreAllowed(mode));
            Assert.AreEqual(BlockListNotice.AlwaysBlocked, BlockListRules.Notice(mode));
        }

        [TestMethod]
        public void AllowAllAppsMeansThereIsNoAppList()
        {
            Assert.IsFalse(BlockListRules.ShowsApps(Custom(allAppsAllowed: true)));
            Assert.IsTrue(BlockListRules.ShowsApps(Custom(allAppsAllowed: false)));
            Assert.IsTrue(BlockListRules.AppsAreAllowed(Custom(allAppsAllowed: false)));
        }

        [TestMethod]
        public void ARelaxModeHasNothingToEdit()
        {
            var relax = new BlockListModeFlags { IsRelaxFocus = true, IsAllowedAllApps = false };
            Assert.IsFalse(BlockListRules.CanEditEntries(relax));
            Assert.IsFalse(BlockListRules.ShowsApps(relax));
            Assert.AreEqual(BlockListNotice.AllWebsitesAllowed, BlockListRules.Notice(relax));
        }

        [TestMethod]
        public void TheNoticeNamesWhichHalvesAreAllowLists()
        {
            Assert.AreEqual(BlockListNotice.None, BlockListRules.Notice(Custom()));
            Assert.AreEqual(BlockListNotice.AllowListWebsites, BlockListRules.Notice(Custom(allowList: true)));
            Assert.AreEqual(BlockListNotice.AllowListApps, BlockListRules.Notice(Custom(allAppsAllowed: false)));
            Assert.AreEqual(BlockListNotice.AllowListEverything, BlockListRules.Notice(Custom(allowList: true, allAppsAllowed: false)));
        }

        [TestMethod]
        public void SuggestionsNeedACategoryAndAreNeverAddedToAnAllowList()
        {
            Assert.IsFalse(BlockListRules.OffersSuggestions(Custom(), null));
            Assert.IsTrue(BlockListRules.OffersSuggestions(Custom(), DistractionCategory.News));
            Assert.IsFalse(BlockListRules.OffersSuggestions(Custom(allowList: true), DistractionCategory.News));
            Assert.IsFalse(BlockListRules.OffersSuggestions(new BlockListModeFlags { IsRelaxFocus = true }, DistractionCategory.News));
            Assert.IsTrue(BlockListRules.OffersSuggestions(new BlockListModeFlags { IsDefault = true, IsAllowedListApproach = true }, DistractionCategory.News));
        }

        // MARK: - Names

        [TestMethod]
        public void RenameTrimsAndRejectsBlanks()
        {
            Assert.AreEqual(BlockListRenameOutcome.Empty, BlockListRules.ValidateRename("   ", "Study", new string[0], out _));
            Assert.AreEqual(BlockListRenameOutcome.Renamed, BlockListRules.ValidateRename("  Deep  ", "Study", new string[0], out string name));
            Assert.AreEqual("Deep", name);
        }

        [TestMethod]
        public void RenameCutsLongNamesToFortyFiveCharacters()
        {
            BlockListRules.ValidateRename(new string('a', 60), "Study", new string[0], out string name);
            Assert.AreEqual(BlockListRules.MaxRenameLength, name.Length);
        }

        [TestMethod]
        public void RenameRejectsAnotherListsNameIgnoringCase()
        {
            Assert.AreEqual(BlockListRenameOutcome.Duplicate, BlockListRules.ValidateRename("meetings", "Study", new[] { "Meetings" }, out _));
        }

        [TestMethod]
        public void RenamingToTheSameNameIsNotAnError()
        {
            Assert.AreEqual(BlockListRenameOutcome.Unchanged, BlockListRules.ValidateRename("Study ", "Study", new[] { "Meetings" }, out _));
            // Changing only the case of its own name is allowed: the list it "clashes" with is itself.
            Assert.AreEqual(BlockListRenameOutcome.Renamed, BlockListRules.ValidateRename("study", "Study", new[] { "Meetings" }, out _));
        }

        [TestMethod]
        public void DuplicateNamesCountUpPastTakenOnes()
        {
            Assert.AreEqual("Study 1", BlockListRules.DuplicateName("Study", new[] { "Study" }));
            Assert.AreEqual("Study 3", BlockListRules.DuplicateName("Study", new[] { "Study", "study 1", "Study 2" }));
        }

        [TestMethod]
        public void NewNamesAreCutToTwoHundredFiftyCharacters()
        {
            Assert.AreEqual(250, BlockListRules.NewBlockListName(new string('x', 300)).Length);
            Assert.AreEqual("Study", BlockListRules.NewBlockListName("Study"));
        }

        // MARK: - Adding websites

        [TestMethod]
        public void AddingAWebsiteTrimsAndRejectsBlanksAndExactDuplicates()
        {
            Assert.AreEqual(BlockListAddOutcome.Empty, BlockListRules.ValidateNewWebsite("  ", new string[0], out _));
            Assert.AreEqual(BlockListAddOutcome.Duplicate, BlockListRules.ValidateNewWebsite(" reddit.com ", new[] { "reddit.com" }, out _));
            Assert.AreEqual(BlockListAddOutcome.Added, BlockListRules.ValidateNewWebsite(" x.com", new[] { "reddit.com" }, out string value));
            Assert.AreEqual("x.com", value);
        }

        // MARK: - Entries

        [TestMethod]
        public void EntriesListWebsitesThenAppsWithInferredCategories()
        {
            var entries = BlockListEntries.Build(new[] { "www.instagram.com/explore", "example.org" }, false, new[] { "steam.exe" }, true);
            Assert.AreEqual(3, entries.Count);
            Assert.AreEqual(DistractionCategory.SocialMedia, entries[0].Category);
            Assert.AreEqual(DistractionCategory.Other, entries[1].Category);
            Assert.AreEqual(BlockListEntryType.App, entries[2].Type);
            Assert.IsFalse(entries[0].IsAllowed);
            Assert.IsTrue(entries[2].IsAllowed);
            Assert.AreEqual("W", entries[0].AvatarLetter);
        }

        [TestMethod]
        public void FilteringAppliesTheCategoryAndTheSearchTogether()
        {
            var entries = BlockListEntries.Build(new[] { "facebook.com", "reddit.com", "bbc.com", "work.example" }, false, new string[0], false);
            Assert.AreEqual(2, BlockListEntries.Visible(entries, DistractionCategory.SocialMedia, "").Count);
            Assert.AreEqual("reddit.com", BlockListEntries.Visible(entries, DistractionCategory.SocialMedia, " RED ").Single().Value);
            Assert.AreEqual(4, BlockListEntries.Visible(entries, null, null).Count);
            Assert.AreEqual(1, BlockListEntries.Count(entries, DistractionCategory.News));
            Assert.AreEqual(4, BlockListEntries.Count(entries, null));
        }

        [TestMethod]
        public void SuggestionsSkipWhatIsAlreadyListedAndStopAtFour()
        {
            var entries = BlockListEntries.Build(new[] { "Facebook.com" }, false, new string[0], false);
            var suggestions = BlockListEntries.Suggestions(entries, DistractionCategory.SocialMedia);
            CollectionAssert.AreEqual(new[] { "x.com", "instagram.com", "tiktok.com", "youtube.com" }, suggestions.ToArray());
            Assert.AreEqual(0, BlockListEntries.Suggestions(entries, null).Count);
            Assert.AreEqual(0, BlockListEntries.Suggestions(entries, DistractionCategory.Porn).Count);
        }

        [TestMethod]
        public void TheBadgeUsesTheSingularForOne()
        {
            Assert.AreEqual("1 block", BlockListEntries.BlocksBadge(1, "<COUNT> blocks", "1 block"));
            Assert.AreEqual("0 blocks", BlockListEntries.BlocksBadge(0, "<COUNT> blocks", "1 block"));
            Assert.AreEqual("12 blocks", BlockListEntries.BlocksBadge(12, "<COUNT> blocks", "1 block"));
        }
    }

    /// <summary>Ported from the Mac's <c>BlockListLayoutTests.swift</c>.</summary>
    [TestClass]
    public class BlockListLayoutTests
    {
        private const double Tolerance = 0.001;

        [TestMethod]
        public void TheNameColumnNeverCollapsesOrShrinksAsTheRowGrows()
        {
            double previous = 0;
            for (double row = 0; row <= 2400; row += 5)
            {
                double name = BlockListLayout.ColumnWidths(row).Name;
                Assert.IsTrue(name >= BlockListLayout.NameMinWidth - Tolerance, "collapsed at " + row);
                Assert.IsTrue(name >= previous - Tolerance, "shrank at " + row);
                previous = name;
            }
        }

        [TestMethod]
        public void ColumnsFitTheRowWheneverItMeetsTheMinimum()
        {
            for (double row = BlockListLayout.MinimumRowWidth; row <= 2400; row += 5)
                Assert.IsTrue(BlockListLayout.ColumnWidths(row).TotalRowWidth <= row + Tolerance, "overflowed " + row);
        }

        [TestMethod]
        public void ExtraWidthGoesToTheNameOnceTheOtherColumnsReachTheDesign()
        {
            var narrower = BlockListLayout.ColumnWidths(900);
            var wider = BlockListLayout.ColumnWidths(1100);
            Assert.AreEqual(BlockListLayout.CategoryIdealWidth, wider.Category, Tolerance);
            Assert.AreEqual(BlockListLayout.TypeIdealWidth, wider.Type, Tolerance);
            Assert.AreEqual(200, wider.Name - narrower.Name, Tolerance);
        }

        [TestMethod]
        public void CategoryAndTypeGiveUpWidthBeforeTheNameDoes()
        {
            var tight = BlockListLayout.ColumnWidths(BlockListLayout.MinimumRowWidth);
            Assert.AreEqual(BlockListLayout.NameMinWidth, tight.Name, Tolerance);
            Assert.AreEqual(BlockListLayout.CategoryMinWidth, tight.Category, Tolerance);
            Assert.AreEqual(BlockListLayout.TypeMinWidth, tight.Type, Tolerance);
        }

        [TestMethod]
        public void TheRailStaysBetweenItsMinimumAndDesignWidths()
        {
            for (double width = 200; width <= 2400; width += 10)
            {
                double rail = BlockListLayout.RailWidth(width);
                Assert.IsTrue(rail >= BlockListLayout.RailMinWidth - Tolerance && rail <= BlockListLayout.RailIdealWidth + Tolerance);
            }
            Assert.AreEqual(BlockListLayout.RailIdealWidth, BlockListLayout.RailWidth(1600), Tolerance);
        }

        [TestMethod]
        public void TheTitleScalesWithThePanelWithinItsBounds()
        {
            Assert.AreEqual(BlockListLayout.TitleMinFontSize, BlockListLayout.TitleFontSize(100), Tolerance);
            Assert.AreEqual(BlockListLayout.TitleMaxFontSize, BlockListLayout.TitleFontSize(5000), Tolerance);
            Assert.AreEqual(20, BlockListLayout.TitleFontSize(559.5), Tolerance);
        }

        [TestMethod]
        public void MetricsAccountForEveryPieceOfScreenChrome()
        {
            var metrics = BlockListLayout.Metrics(1000);
            double reassembled = BlockListLayout.OuterPadding * 2 + metrics.RailWidth + BlockListLayout.RailPanelGap + metrics.PanelWidth;
            Assert.AreEqual(1000, reassembled, Tolerance);
            Assert.AreEqual(metrics.PanelWidth - BlockListLayout.PanelInset * 2 - BlockListLayout.ScrollerWidth, metrics.RowWidth, Tolerance);
        }

        [TestMethod]
        public void ANarrowWindowStillLaysOutAFullRow()
        {
            foreach (double width in new double[] { 400, 500, 600, 680 })
            {
                var metrics = BlockListLayout.Metrics(width);
                Assert.IsTrue(metrics.LaidOutRowWidth >= BlockListLayout.MinimumRowWidth);
                Assert.IsTrue(metrics.Columns.TotalRowWidth <= metrics.LaidOutRowWidth + Tolerance);
            }
        }
    }
}
