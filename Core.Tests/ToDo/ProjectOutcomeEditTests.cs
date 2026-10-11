using System.Globalization;
using System.Linq;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>The project outcome picker and chip: 1–5 on screen, 2–10 on the wire, legacy odd values kept.</summary>
    [TestClass]
    public class ProjectOutcomeEditTests
    {
        private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

        [TestMethod]
        public void ChipStep_IsTheMacsWholeNumberDivision_Clamped()
        {
            Assert.IsNull(ProjectOutcomeEdit.ChipStep(null));
            Assert.AreEqual(1, ProjectOutcomeEdit.ChipStep(1));
            Assert.AreEqual(1, ProjectOutcomeEdit.ChipStep(2));
            Assert.AreEqual(3, ProjectOutcomeEdit.ChipStep(6));
            Assert.AreEqual(3, ProjectOutcomeEdit.ChipStep(7));
            Assert.AreEqual(4, ProjectOutcomeEdit.ChipStep(8));
            Assert.AreEqual(5, ProjectOutcomeEdit.ChipStep(10));
            Assert.AreEqual(5, ProjectOutcomeEdit.ChipStep(14));
            Assert.AreEqual(1, ProjectOutcomeEdit.ChipStep(0));
        }

        [TestMethod]
        public void Options_AreNotSetThenFiveSteps_OnTheTenPointWire()
        {
            var options = ProjectOutcomeEdit.Options(8);
            Assert.AreEqual(6, options.Count);
            Assert.IsNull(options[0].Wire);
            CollectionAssert.AreEqual(new int?[] { 2, 4, 6, 8, 10 }, options.Skip(1).Select(o => o.Wire).ToArray());
            CollectionAssert.AreEqual(new int?[] { 1, 2, 3, 4, 5 }, options.Skip(1).Select(o => o.Step).ToArray());
            Assert.IsFalse(options.Any(o => o.IsLegacy));
            Assert.AreEqual(6, ProjectOutcomeEdit.Options(null).Count);
        }

        [TestMethod]
        public void Options_KeepALegacyOddValueAsItsOwnRow()
        {
            var options = ProjectOutcomeEdit.Options(7);
            Assert.AreEqual(7, options.Count);
            var legacy = options.Last();
            Assert.IsTrue(legacy.IsLegacy);
            Assert.AreEqual(7, legacy.Wire);
            Assert.IsNull(legacy.Step);
        }

        [TestMethod]
        public void ValueText_ShowsHalfSteps_Clamped()
        {
            Assert.AreEqual("3.5 / 5", ProjectOutcomeEdit.ValueText(7, English));
            Assert.AreEqual("4 / 5", ProjectOutcomeEdit.ValueText(8, English));
            Assert.AreEqual("1 / 5", ProjectOutcomeEdit.ValueText(1, English));
            Assert.AreEqual("5 / 5", ProjectOutcomeEdit.ValueText(11, English));
        }
    }
}
