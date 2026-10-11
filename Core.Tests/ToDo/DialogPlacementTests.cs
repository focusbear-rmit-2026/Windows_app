using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    [TestClass]
    public class DialogPlacementTests
    {
        // 1366x768 at 125%: a 1093x584 work area above a 30px taskbar.
        private static readonly DialogRect Small = new DialogRect(0, 0, 1093, 584);

        [TestMethod]
        public void ATallDialog_ShrinksToTheWorkArea_AndItsTopStaysOnScreen()
        {
            var owner = new DialogRect(100, 50, 900, 500);
            var fit = DialogPlacement.Fit(Small, owner, 720, 720);
            Assert.AreEqual(584 - 32, fit.Height);
            Assert.AreEqual(16, fit.Top, "top edge — the header with Save/Cancel — is on screen");
            Assert.IsTrue(fit.Bottom <= 584 - 16);
            Assert.AreEqual(720, fit.Width);
            Assert.AreEqual(100 + (900 - 720) / 2.0, fit.Left, "still centred on the owner across");
        }

        [TestMethod]
        public void AFittingDialog_IsCentredOnItsOwner()
        {
            var work = new DialogRect(0, 0, 1920, 1040);
            var owner = new DialogRect(200, 100, 1200, 800);
            var fit = DialogPlacement.Fit(work, owner, 720, 600);
            Assert.AreEqual(200 + 240, fit.Left);
            Assert.AreEqual(100 + 100, fit.Top);
            Assert.AreEqual(600, fit.Height);
        }

        [TestMethod]
        public void AnOwnerNearAnEdge_PushesTheDialogBackInside()
        {
            var work = new DialogRect(0, 0, 1920, 1040);
            var owner = new DialogRect(1700, 900, 400, 300);
            var fit = DialogPlacement.Fit(work, owner, 720, 600);
            Assert.AreEqual(1920 - 16 - 720, fit.Left);
            Assert.AreEqual(1040 - 16 - 600, fit.Top);
        }

        [TestMethod]
        public void ASecondMonitor_ClampsToItsOwnWorkArea()
        {
            var work = new DialogRect(1920, -200, 1280, 700);
            var fit = DialogPlacement.Fit(work, null, 720, 720);
            Assert.AreEqual(-200 + 16, fit.Top);
            Assert.AreEqual(700 - 32, fit.Height);
            Assert.AreEqual(1920 + (1280 - 720) / 2.0, fit.Left);
        }
    }
}
