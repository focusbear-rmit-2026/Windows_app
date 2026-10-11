using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>Task details written on the web, shown on a native screen.</summary>
    [TestClass]
    public class ToDoHtmlTests
    {
        [TestMethod]
        public void TagsAreStrippedAndEntitiesDecoded()
        {
            Assert.AreEqual("Hello world", ToDoHtml.PlainText("<p>Hello&nbsp;<b>world</b></p>"));
        }

        [TestMethod]
        public void BlockBreaksBecomeNewlines()
        {
            Assert.AreEqual("one\ntwo", ToDoHtml.PlainText("<p>one</p><p>two</p>").Trim());
            Assert.AreEqual("one\ntwo", ToDoHtml.PlainText("one<br/>two"));
            Assert.AreEqual("one\ntwo", ToDoHtml.PlainText("one<BR>two"), "the editor's casing is not ours to assume");
        }

        [TestMethod]
        public void TextWithNoMarkupIsLeftAloneApartFromItsEntities()
        {
            Assert.AreEqual("plain text", ToDoHtml.PlainText("plain text"));
            Assert.AreEqual("Tom & Jerry", ToDoHtml.PlainText("Tom &amp; Jerry"));
            Assert.AreEqual("  kept  ", ToDoHtml.PlainText("  kept  "),
                            "without markup there is nothing to tidy, so the user's own spacing stands");
        }

        [TestMethod]
        public void AnEscapedTagSurvivesTheStripper()
        {
            // The entity is decoded AFTER the tags are stripped, so an angle bracket the user typed is
            // not mistaken for markup and deleted.
            Assert.AreEqual("a < b", ToDoHtml.PlainText("<p>a &lt; b</p>"));
        }

        [TestMethod]
        public void AnAbsentDetailsFieldIsEmptyText()
        {
            Assert.AreEqual(string.Empty, ToDoHtml.PlainText(null));
            Assert.AreEqual(string.Empty, ToDoHtml.PlainText(string.Empty));
        }
    }
}
