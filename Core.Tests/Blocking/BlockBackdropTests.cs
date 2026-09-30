using System;
using FocusBear.Core.Blocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Blocking
{
    [TestClass]
    public class BlockBackdropTests
    {
        [TestMethod]
        public void TheBackdropStaysWhileItsPopupIsUp()
        {
            Assert.IsFalse(BlockBackdrop.ShouldDismiss(true, true, TimeSpan.FromMinutes(30)));
            Assert.IsFalse(BlockBackdrop.ShouldDismiss(true, false, TimeSpan.Zero));
        }

        [TestMethod]
        public void ItGoesAsSoonAsThePopupHasGoneHoweverItWent()
        {
            Assert.IsTrue(BlockBackdrop.ShouldDismiss(false, true, TimeSpan.FromSeconds(1)));
        }

        [TestMethod]
        public void ItWaitsBrieflyForThePopupToAppearButNotForever()
        {
            Assert.IsFalse(BlockBackdrop.ShouldDismiss(false, false, TimeSpan.FromSeconds(2)), "the popup follows the backdrop");
            Assert.IsTrue(BlockBackdrop.ShouldDismiss(false, false, BlockBackdrop.PopupGrace), "no popup ever came");
        }

        // "AI blocking popups should appear on top of the blur" (block-blur-backdrop, 28 Sep).

        [TestMethod]
        public void NothingMovesWhileEveryPopupIsAboveTheBackdrop()
        {
            Assert.AreEqual(BackdropOrderFix.None, BlockBackdrop.OrderFix(popupBelowBackdrop: false, otherWindowBetween: false, attempts: 0));
        }

        [TestMethod]
        public void APopupUnderTheBackdropIsRaisedAboveIt()
        {
            // Raising the popup (not lowering the backdrop) works even when Focus Bear isn't the foreground app,
            // which is when a reused popup's Show() leaves it where it was: under the freshly shown backdrop.
            Assert.AreEqual(BackdropOrderFix.RaisePopups, BlockBackdrop.OrderFix(true, false, 0));
            Assert.AreEqual(BackdropOrderFix.RaisePopups, BlockBackdrop.OrderFix(true, true, BlockBackdrop.MaxOrderFixes - 1));
        }

        [TestMethod]
        public void APopupThatStaysUnderTheBackdropTakesTheBackdropDown()
        {
            // Never a popup frosted over and out of reach: past the retries the backdrop goes, the popup stays.
            Assert.AreEqual(BackdropOrderFix.HideBackdrop, BlockBackdrop.OrderFix(true, false, BlockBackdrop.MaxOrderFixes));
        }

        [TestMethod]
        public void AnotherWindowBetweenThePopupAndTheBackdropMovesTheBackdropUnderThePopup()
        {
            Assert.AreEqual(BackdropOrderFix.MoveBackdropUnderPopup, BlockBackdrop.OrderFix(false, true, 0));
            // ...but not forever: a window that keeps climbing back is left alone (the popup is still on top).
            Assert.AreEqual(BackdropOrderFix.None, BlockBackdrop.OrderFix(false, true, BlockBackdrop.MaxOrderFixes));
        }

        [TestMethod]
        public void TheWashMatchesTheMac()
        {
            Assert.AreEqual(0.82, BlockBackdrop.WashOpacity(blurShowing: false), 1e-9, "no blur: the Mac's wash has to hide the app alone");
            Assert.IsTrue(BlockBackdrop.WashOpacity(blurShowing: true) < BlockBackdrop.ScrimOpacity, "over acrylic the app may bleed through");
        }
    }
}
