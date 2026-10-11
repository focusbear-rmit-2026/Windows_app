using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using Focus_Bear.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// The seam between the note editor (a WPF RichTextBox) and Core's <c>NoteHtml</c>: what
    /// <see cref="NoteFlowDocument"/> reads out of a FlowDocument. Core proves the HTML rules; this proves
    /// the editor's own output reaches them intact. The documents are built the way the RichTextBox's
    /// EditingCommands build them — Bold/Italic/Underline spans rather than run properties, a bullet list
    /// as List/ListItem/Paragraph, indentation as a List inside a ListItem — because that is where a
    /// mapping that only handles the importer's own shapes would lose formatting the user typed.
    /// </summary>
    [TestClass]
    public class NoteFlowDocumentSeamTests
    {
        /// <summary>WPF content elements belong to an STA thread; the test host's is MTA.</summary>
        private static void OnSta(Action body)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try { body(); }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) throw new AssertFailedException(failure.Message, failure);
        }

        private static Paragraph P(params Inline[] inlines)
        {
            var paragraph = new Paragraph();
            paragraph.Inlines.AddRange(inlines);
            return paragraph;
        }

        private static string Html(params Block[] blocks)
        {
            var document = new FlowDocument();
            document.Blocks.AddRange(blocks);
            return NoteFlowDocument.ToHtml(document);
        }

        [DataTestMethod]
        [DataRow("<p><strong>bold</strong> and <em>italic</em> and <u>under</u></p>")]
        [DataRow("<h2>Heading</h2><p>body</p>")]
        [DataRow("<ul><li>one</li><li>two</li></ul>")]
        [DataRow("<ol><li>first</li><li>second</li></ol>")]
        [DataRow("<ul><li>outer<ul><li>inner</li></ul></li></ul>")]
        [DataRow("<p>line one<br>line two</p>")]
        [DataRow("<p>see <a href=\"https://focusbear.io\">the site</a></p>")]
        [DataRow("<p>a</p><p>&nbsp;</p><p>b</p>")]
        [DataRow("<p><code>x = 1</code> <s>gone</s></p>")]
        public void ANoteOpenedInTheEditorSavesBackUnchanged(string html)
        {
            // Opening a note and saving it untouched must not rewrite it.
            OnSta(() => Assert.AreEqual(html, NoteFlowDocument.ToHtml(NoteFlowDocument.FromHtml(html, 14))));
        }

        [TestMethod]
        public void FormattingAppliedWithTheToolbarIsSaved()
        {
            // ToggleBold/Italic/Underline wrap the selection in Bold/Italic/Underline spans.
            OnSta(() => Assert.AreEqual("<p>plain <strong>loud</strong> <em>lean</em> <u>under</u></p>",
                Html(P(new Run("plain "), new Bold(new Run("loud")), new Run(" "), new Italic(new Run("lean")), new Run(" "), new Underline(new Run("under"))))));
        }

        [TestMethod]
        public void NestedSpansKeepEveryStyle()
        {
            // Wrapped innermost-first as the Mac writes them: u, then em, then strong.
            OnSta(() => Assert.AreEqual("<p><strong><em><u>all</u></em></strong></p>",
                Html(P(new Bold(new Italic(new Underline(new Run("all"))))))));
        }

        [TestMethod]
        public void TextSplitAcrossRunsIsOneRunInTheHtml()
        {
            // Typing into the middle of a bold word leaves the RichTextBox holding several runs.
            OnSta(() => Assert.AreEqual("<p><strong>hello</strong></p>",
                Html(P(new Bold(new Run("hel")), new Bold(new Run("lo"))))));
        }

        [TestMethod]
        public void ABulletListMadeInTheEditorIsSavedAsAList()
        {
            OnSta(() =>
            {
                var list = new List { MarkerStyle = TextMarkerStyle.Disc };
                list.ListItems.Add(new ListItem(P(new Run("one"))));
                list.ListItems.Add(new ListItem(P(new Run("two"))));
                Assert.AreEqual("<ul><li>one</li><li>two</li></ul>", Html(list));
            });
        }

        [TestMethod]
        public void AnIndentedItemIsSavedAsANestedList()
        {
            // IncreaseIndentation puts a List inside the previous ListItem.
            OnSta(() =>
            {
                var inner = new List { MarkerStyle = TextMarkerStyle.Decimal };
                inner.ListItems.Add(new ListItem(P(new Run("inner"))));
                var outerItem = new ListItem(P(new Run("outer")));
                outerItem.Blocks.Add(inner);
                var outer = new List { MarkerStyle = TextMarkerStyle.Disc };
                outer.ListItems.Add(outerItem);
                Assert.AreEqual("<ul><li>outer<ol><li>inner</li></ol></li></ul>", Html(outer));
            });
        }

        [TestMethod]
        public void AHeadingKeepsItsLevelThroughTheEditor()
        {
            OnSta(() =>
            {
                var document = NoteFlowDocument.FromHtml("<h3>Plan</h3>", 14);
                var heading = (Paragraph)document.Blocks.FirstBlock;
                Assert.AreEqual(3, heading.Tag);
                Assert.AreEqual(FontWeights.Bold, heading.FontWeight);
                Assert.AreEqual("<h3>Plan</h3>", NoteFlowDocument.ToHtml(document));
            });
        }

        [TestMethod]
        public void AnUnsafeLinkInANoteIsNeverClickable()
        {
            OnSta(() =>
            {
                var document = NoteFlowDocument.FromHtml("<p><a href=\"javascript:alert(1)\">click me</a></p>", 14);
                var paragraph = (Paragraph)document.Blocks.FirstBlock;
                Assert.IsFalse(paragraph.Inlines.OfType<Hyperlink>().Any(), "no hyperlink for a javascript: href");
                Assert.AreEqual("click me", new TextRange(document.ContentStart, document.ContentEnd).Text.Trim());
            });
        }

        [TestMethod]
        public void AnUnsafeLinkPastedIntoTheEditorIsNotSaved()
        {
            OnSta(() =>
            {
                var link = new Hyperlink(new Run("x")) { NavigateUri = new Uri("file:///C:/Windows/System32/calc.exe") };
                Assert.AreEqual("<p>x</p>", Html(P(link)));
            });
        }

        [TestMethod]
        public void ASafeLinkIsClickableAndSaved()
        {
            OnSta(() =>
            {
                var document = NoteFlowDocument.FromHtml("<p><a href=\"https://focusbear.io/x\">site</a></p>", 14);
                var link = ((Paragraph)document.Blocks.FirstBlock).Inlines.OfType<Hyperlink>().Single();
                Assert.AreEqual("https://focusbear.io/x", link.NavigateUri.OriginalString);
            });
        }

        [TestMethod]
        public void AnEditorLeftEmptySavesNothing()
        {
            OnSta(() =>
            {
                var document = NoteFlowDocument.FromHtml("", 14);
                Assert.IsTrue(NoteFlowDocument.IsEmpty(document));
                Assert.AreEqual("", NoteFlowDocument.ToHtml(document));
            });
        }

        [TestMethod]
        public void PastedBlocksANoteCannotHoldAreDroppedNotFatal()
        {
            OnSta(() =>
            {
                var table = new Table();
                Assert.AreEqual("<p>kept</p>", Html(P(new Run("kept")), table, new BlockUIContainer()));
            });
        }

        [TestMethod]
        public void OpeningAWebNoteAndSavingItUntouched_KeepsWhatTheEditorCannotHold()
        {
            // The personal-note editor saved its own export on every Save, so a title-only edit rewrote the
            // body through the native document model and dropped the web's table, image and highlight.
            OnSta(() =>
            {
                const string stored = "<p>intro</p><table><tbody><tr><td>q1</td><td>q2</td></tr></tbody></table><p><img src=\"https://x/y.png\"><mark>hot</mark></p>";
                var document = NoteFlowDocument.FromHtml(stored, 14);
                string opening = NoteFlowDocument.ToHtml(document);
                Assert.AreNotEqual(stored, opening, "the fixture really is something the editor cannot round-trip");

                Assert.AreEqual(stored, FocusBear.Core.Notes.NoteUpsertBody.BodyToSave(NoteFlowDocument.ToHtml(document), opening, stored));
            });
        }
    }
}
