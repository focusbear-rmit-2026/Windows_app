using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>
    /// The two places the to-do API is looser than its own contract, ported from the Mac's
    /// <c>LenientInt</c> and <c>@DefaultEmpty</c> (`testLenientDurationAndHexIcon`,
    /// `testPageKeepsRowsMissingTitleOrCreatedAt`).
    /// </summary>
    [TestClass]
    public class ToDoWireTests
    {
        [TestMethod]
        public void ADurationIsReadWhetherItArrivesAsANumberOrAString()
        {
            Assert.AreEqual(300, ToDoWire.LenientInt(300));
            Assert.AreEqual(300, ToDoWire.LenientInt("300"));
            Assert.AreEqual(300, ToDoWire.LenientInt(300.0));
            Assert.AreEqual(300, ToDoWire.LenientInt(300L));
        }

        [TestMethod]
        public void AFractionalValueTruncatesTowardsZeroAsSwiftDoes()
        {
            Assert.AreEqual(12, ToDoWire.LenientInt(12.9));
            Assert.AreEqual(12, ToDoWire.LenientInt("12.9"));
        }

        [TestMethod]
        public void AnUnreadableDurationIsZeroRatherThanACostToTheWholeRow()
        {
            Assert.AreEqual(0, ToDoWire.LenientInt(null));
            Assert.AreEqual(0, ToDoWire.LenientInt("not a number"));
            Assert.AreEqual(0, ToDoWire.LenientInt(true));
        }

        [TestMethod]
        public void ADurationIsReadTheSameWayWhateverTheMachinesLocaleCallsADecimalPoint()
        {
            // A German machine writes 12,9 for 12.9; the WIRE is always invariant, so the comma is not a
            // decimal point here and the value is unreadable rather than a hundred and twenty-nine.
            Assert.AreEqual(0, ToDoWire.LenientInt("12,9"));
        }

        [TestMethod]
        public void AStringTheApiForgotToSendReadsAsEmpty()
        {
            // `GET /to-do?sort_mode=DUE_DATE` has returned a completed row with no title, and
            // `PUT /to-do` answers a create with no `created_at`. One such row must not cost the page.
            Assert.AreEqual(string.Empty, ToDoWire.TextOrEmpty(null));
            Assert.AreEqual("Widgets", ToDoWire.TextOrEmpty("Widgets"));
        }
    }
}
