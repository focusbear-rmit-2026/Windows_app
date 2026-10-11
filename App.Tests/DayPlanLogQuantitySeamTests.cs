using System;
using System.Threading;
using System.Windows.Controls;
using Focus_Bear.Habits.DayPlan;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The Day Plan's Log Quantity answer box, driven the way its PreviewTextInput / paste handlers drive it. Its
    /// plain answer box had no rule at all, and the answer binds to a double, so "6,85" logged 685.
    /// </summary>
    [TestClass]
    public class DayPlanLogQuantitySeamTests
    {
        private static void OnSta(Action body)
        {
            Exception failure = null;
            var t = new Thread(() => { try { body(); } catch (Exception ex) { failure = ex; } });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
            if (failure != null) throw new AssertFailedException(failure.Message, failure);
        }

        private static string TypeEach(TextBox box, string keys)
        {
            foreach (char c in keys) DayPlanLogQuantityWindow.Type(box, c.ToString());
            return box.Text;
        }

        [TestMethod]
        public void ACommaIsTheDecimalPoint()
        {
            OnSta(() => Assert.AreEqual("6.85", TypeEach(new TextBox(), "6,85")));
        }

        [TestMethod]
        public void LettersASecondPointAndSpacesAreRefused()
        {
            OnSta(() => Assert.AreEqual("1.25", TypeEach(new TextBox(), "1a.2 .5")));
        }

        [TestMethod]
        public void TypingOverASelectionReplacesIt()
        {
            OnSta(() =>
            {
                var box = new TextBox { Text = "10" };
                box.SelectAll();
                Assert.IsTrue(DayPlanLogQuantityWindow.Type(box, "7"));
                Assert.AreEqual("7", box.Text, "the old rule checked '107', not '7'");
            });
        }

        [TestMethod]
        public void APasteIsCheckedLikeTyping()
        {
            OnSta(() =>
            {
                var box = new TextBox { Text = "" };
                Assert.IsTrue(DayPlanLogQuantityWindow.Type(box, "3,5"));
                Assert.AreEqual("3.5", box.Text);
                Assert.IsFalse(DayPlanLogQuantityWindow.Type(box, "abc"));
                Assert.AreEqual("3.5", box.Text);
            });
        }
    }
}
