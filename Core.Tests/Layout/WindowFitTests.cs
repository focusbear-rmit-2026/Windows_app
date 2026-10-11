using FocusBear.Core.Layout;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Layout
{
    [TestClass]
    public class WindowFitTests
    {
        // 1920x1080 at 150% with a 48px taskbar: a 1280 x 688 DIP work area.
        private static readonly Box ShortScreen = new Box(0, 0, 1280, 688);

        [TestMethod]
        public void ATallDialogShrinksToTheWorkAreaAndStaysOnIt()
        {
            // The owner sits near the top: CenterOwner would put a 700-high dialog at y = -250.
            Box placed = WindowFit.Place(ShortScreen, 700, 700, new Box(100, 0, 900, 200));
            Assert.AreEqual(656, placed.Height);
            Assert.AreEqual(16, placed.Top);
            Assert.IsTrue(placed.Bottom <= 688 - 16);
            Assert.AreEqual(700, placed.Width);
            Assert.AreEqual(200, placed.Left);   // still centred on the owner horizontally
        }

        [TestMethod]
        public void AnOwnerHalfOffScreenStillLandsTheDialogInside()
        {
            Box placed = WindowFit.Place(ShortScreen, 700, 500, new Box(900, 400, 800, 600));
            Assert.IsTrue(placed.Right <= 1280 - 16, placed.ToString());
            Assert.IsTrue(placed.Bottom <= 688 - 16, placed.ToString());
        }

        [TestMethod]
        public void WithoutAnOwnerItCentresOnTheWorkAreaOfItsMonitor()
        {
            Box second = new Box(1280, 0, 1920, 1040);
            Box placed = WindowFit.Place(second, 700, 700);
            Assert.AreEqual(1280 + (1920 - 700) / 2, placed.Left);
            Assert.AreEqual((1040 - 700) / 2, placed.Top);
        }
    }
}
