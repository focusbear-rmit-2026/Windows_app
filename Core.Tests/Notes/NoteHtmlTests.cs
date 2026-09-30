using System.Linq;
using FocusBear.Core.Notes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Notes
{
    /// <summary>
    /// The note-body contract shared with the Mac (<c>Notes/NoteHTML.swift</c>) and the web editor.
    /// The Mac has no suite of its own for NoteHTML; the tokenizer and link cases below are ported
    /// from <c>EventDescriptionHTMLTests.swift</c>, which exercises the same tokenizer, and the rest
    /// pin the behaviour its comments describe. The round-trip cases are the ones that matter: a note
    /// saved from Windows is read by the Mac and the web, and back.
    /// </summary>
    [TestClass]
    public class NoteHtmlTests
    {
        private static NoteParagraph OnlyParagraph(string html)
        {
            var doc = NoteHtml.Parse(html);
            Assert.AreEqual(1, doc.Blocks.Count, "expected one block");
            return (NoteParagraph)doc.Blocks[0];
        }

        private static NoteRun[] Runs(string html) =>
            NoteHtml.Parse(html).Blocks.OfType<NoteParagraph>().SelectMany(p => p.Inlines).OfType<NoteRun>().ToArray();

        // ------------------------------------------------------------------ round trip

        [DataTestMethod]
        [DataRow("<p>hello</p>")]
        [DataRow("<p><strong>bold</strong> and <em>italic</em></p>")]
        [DataRow("<p><u>under</u> <s>struck</s> <code>x = 1</code></p>")]
        [DataRow("<p>line one<br>line two</p>")]
        [DataRow("<h2>Heading</h2><p>body</p>")]
        [DataRow("<ul><li>one</li><li>two</li></ul>")]
        [DataRow("<ol><li>first</li><li>second</li></ol>")]
        [DataRow("<p>see <a href=\"https://focusbear.io\">the site</a></p>")]
        [DataRow("<p>a</p><p>&nbsp;</p><p>b</p>")]
        [DataRow("<ul><li>outer<ul><li>inner</li></ul></li></ul>")]
        [DataRow("<p>Tom &amp; Jerry &lt;3 &quot;quotes&quot;</p>")]
        public void WhatTheWebWritesComesBackUnchanged(string html)
        {
            Assert.AreEqual(html, NoteHtml.Write(NoteHtml.Parse(html)));
        }

        [TestMethod]
        public void BoldInsideAHeadingIsNotWrittenAsStrong()
        {
            // A heading is bold already; writing <strong> inside every heading would change the note.
            Assert.AreEqual("<h1>Title</h1>", NoteHtml.Write(NoteHtml.Parse("<h1><strong>Title</strong></h1>")));
        }

        [TestMethod]
        public void TrailingBlankParagraphsAreDropped()
        {
            Assert.AreEqual("<p>text</p>", NoteHtml.Write(NoteHtml.Parse("<p>text</p><p>&nbsp;</p><p></p>")));
        }

        [TestMethod]
        public void AnEmptyDocumentWritesNothing()
        {
            Assert.AreEqual("", NoteHtml.Write(new NoteDocument()));
            Assert.AreEqual("", NoteHtml.Write(NoteHtml.Parse("")));
        }

        [TestMethod]
        public void AdjacentRunsWithTheSameStyleAreOneRun()
        {
            // A WPF FlowDocument splits text into many runs as it is edited; the HTML must not.
            var doc = new NoteDocument(new NoteBlock[]
            {
                new NoteParagraph(new NoteRun("hel") { Bold = true }, new NoteRun("lo") { Bold = true }),
            });
            Assert.AreEqual("<p><strong>hello</strong></p>", NoteHtml.Write(doc), "one element, not one per run");
        }

        // ------------------------------------------------------------------ lists

        [TestMethod]
        public void AParagraphInsideAListItemContinuesTheItem()
        {
            // CKEditor writes <li><p>…</p></li>. That must not become a blank line plus the text.
            var list = (NoteList)NoteHtml.Parse("<ul><li><p>one</p></li><li><p>two</p></li></ul>").Blocks.Single();
            Assert.AreEqual(2, list.Items.Count);
            Assert.AreEqual(1, list.Items[0].Blocks.Count);
            Assert.AreEqual("one", ((NoteParagraph)list.Items[0].Blocks[0]).Text);
            Assert.AreEqual("<ul><li>one</li><li>two</li></ul>", NoteHtml.Write(NoteHtml.Parse("<ul><li><p>one</p></li><li><p>two</p></li></ul>")));
        }

        [TestMethod]
        public void TheMacsSiblingNestedListIsReadAsNested()
        {
            // The Mac exports depth by opening the inner list straight after </li>.
            var outer = (NoteList)NoteHtml.Parse("<ul><li>a</li><ul><li>b</li></ul><li>c</li></ul>").Blocks.Single();
            Assert.AreEqual(2, outer.Items.Count, "b is nested under a, and c is still an outer item");
            var nested = outer.Items[0].Blocks.OfType<NoteList>().Single();
            Assert.AreEqual("b", ((NoteParagraph)nested.Items.Single().Blocks.Single()).Text);
            Assert.AreEqual("c", ((NoteParagraph)outer.Items[1].Blocks.Single()).Text);
        }

        [TestMethod]
        public void AnUnclosedListItemEndsAtTheNextOne()
        {
            var list = (NoteList)NoteHtml.Parse("<ul><li>a<li>b</ul>").Blocks.Single();
            Assert.AreEqual(2, list.Items.Count);
        }

        [TestMethod]
        public void AStrayListItemGetsAnImplicitList()
        {
            var list = (NoteList)NoteHtml.Parse("<li>alone</li>").Blocks.Single();
            Assert.IsFalse(list.Ordered);
            Assert.AreEqual("alone", ((NoteParagraph)list.Items.Single().Blocks.Single()).Text);
        }

        [TestMethod]
        public void TwoParagraphsInOneItemStayOnSeparateLines()
        {
            var item = new NoteListItem();
            item.Blocks.Add(new NoteParagraph(new NoteRun("one")));
            item.Blocks.Add(new NoteParagraph(new NoteRun("two")));
            var list = new NoteList();
            list.Items.Add(item);
            Assert.AreEqual("<ul><li>one<br>two</li></ul>", NoteHtml.Write(new NoteDocument(new NoteBlock[] { list })));
        }

        // ------------------------------------------------------------------ whitespace

        [TestMethod]
        public void SourceWhitespaceCollapsesLikeABrowser()
        {
            Assert.AreEqual("a b", OnlyParagraph("<p>  a \n\t b  </p>").Text);
        }

        [TestMethod]
        public void SpacesBetweenStyledRunsSurvive()
        {
            Assert.AreEqual("plain bold plain", OnlyParagraph("<p>plain <b>bold</b> plain</p>").Text);
        }

        [TestMethod]
        public void PreformattedTextKeepsItsSpacingAndLines()
        {
            var p = OnlyParagraph("<pre>a  b\nc</pre>");
            Assert.AreEqual("a  b\nc", p.Text);
            Assert.IsTrue(p.Inlines.OfType<NoteRun>().All(r => r.Code));
        }

        [TestMethod]
        public void AnEmptyParagraphIsKeptAsABlankLine()
        {
            var blocks = NoteHtml.Parse("<p>a</p><p>&nbsp;</p><p>b</p>").Blocks;
            Assert.AreEqual(3, blocks.Count);
            Assert.IsTrue(((NoteParagraph)blocks[1]).IsBlank);
        }

        // ------------------------------------------------------------------ tokenizer (EventDescriptionHTMLTests)

        [TestMethod]
        public void ALessThanInProseIsText()
        {
            Assert.AreEqual("5 < 6 and 7 > 6", NoteHtml.PlainText("<p>5 < 6 and 7 > 6</p>"));
        }

        [TestMethod]
        public void AnUnterminatedTagIsLiteral()
        {
            // Nothing after it closes it. (In "<p>a <b</p>" the ">" of </p> would, on the Mac too.)
            Assert.AreEqual("x and <b", NoteHtml.PlainText("<b>x</b> and <b"));
        }

        [TestMethod]
        public void StrayClosingTagsAreIgnored()
        {
            Assert.AreEqual("text more", NoteHtml.PlainText("<p>text</b></a> more</p>"));
        }

        [TestMethod]
        public void UnknownTagsAreStrippedButTheirTextIsKept()
        {
            Assert.AreEqual("kept", NoteHtml.PlainText("<div><span class=\"x\">kept</span></div>"));
        }

        [TestMethod]
        public void ScriptAndStyleContentIsDropped()
        {
            Assert.AreEqual("ab", NoteHtml.PlainText("<p>a<script>alert(1)</script>b</p>"));
            Assert.AreEqual("ab", NoteHtml.PlainText("<p>a<style>.x{color:red}</style>b</p>"));
        }

        [TestMethod]
        public void CommentsAreDropped()
        {
            Assert.AreEqual("ab", NoteHtml.PlainText("<p>a<!-- hidden -->b</p>"));
        }

        [TestMethod]
        public void UnclosedBoldAppliesToTheRest()
        {
            var runs = Runs("<p>before <b>after</p>");
            CollectionAssert.AreEqual(new[] { "before ", "after" }, runs.Select(r => r.Text).ToArray());
            CollectionAssert.AreEqual(new[] { false, true }, runs.Select(r => r.Bold).ToArray());
        }

        [TestMethod]
        public void NestedBoldStaysBoldUntilTheOuterTagCloses()
        {
            var runs = Runs("<p><b>a<b>b</b>c</b>d</p>");
            Assert.AreEqual("abcd", string.Concat(runs.Select(r => r.Text)));
            Assert.IsTrue(runs.Where(r => r.Text != "d").All(r => r.Bold));
            Assert.IsFalse(runs.Single(r => r.Text == "d").Bold);
        }

        [TestMethod]
        public void TagNamesAreCaseInsensitive()
        {
            Assert.IsTrue(Runs("<P><STRONG>x</STRONG></P>").Single().Bold);
        }

        // ------------------------------------------------------------------ entities

        [DataTestMethod]
        [DataRow("Tom &amp; Jerry", "Tom & Jerry")]
        [DataRow("&lt;tag&gt;", "<tag>")]
        [DataRow("say &quot;hi&quot;", "say \"hi\"")]
        [DataRow("it&#39;s", "it's")]
        [DataRow("it&#x27;s", "it's")]
        [DataRow("a&nbsp;b", "a b")]
        [DataRow("wait&hellip;", "wait…")]
        [DataRow("100 &fake; ok", "100 &fake; ok")]
        [DataRow("AT&T", "AT&T")]
        [DataRow("&#128512;", "\U0001F600")]
        public void EntitiesDecodeAsOnTheMac(string encoded, string decoded)
        {
            Assert.AreEqual(decoded, NoteHtmlEntities.Decode(encoded));
        }

        [TestMethod]
        public void EscapedMarkupIsNotReparsedAsATag()
        {
            Assert.AreEqual("<b>not bold</b>", NoteHtml.PlainText("<p>&lt;b&gt;not bold&lt;/b&gt;</p>"));
            Assert.IsFalse(Runs("<p>&lt;b&gt;not bold&lt;/b&gt;</p>").Any(r => r.Bold));
        }

        [TestMethod]
        public void WritingEscapesWhatWouldOtherwiseBeMarkup()
        {
            var doc = new NoteDocument(new NoteBlock[] { new NoteParagraph(new NoteRun("<b> & \"q\"")) });
            Assert.AreEqual("<p>&lt;b&gt; &amp; &quot;q&quot;</p>", NoteHtml.Write(doc));
        }

        // ------------------------------------------------------------------ links

        [TestMethod]
        public void AnchorTextKeepsItsHref()
        {
            var runs = Runs("<p>join <a href=\"https://example.com/room\">here</a> now</p>");
            CollectionAssert.AreEqual(new[] { "join ", "here", " now" }, runs.Select(r => r.Text).ToArray());
            CollectionAssert.AreEqual(new[] { null, "https://example.com/room", null }, runs.Select(r => r.Link).ToArray());
        }

        [TestMethod]
        public void SingleQuotedHrefIsRead()
        {
            Assert.AreEqual("https://example.com", Runs("<p><a href='https://example.com'>x</a></p>").Single().Link);
        }

        [DataTestMethod]
        [DataRow("javascript:alert(1)")]
        [DataRow("JaVaScRiPt:alert(1)")]
        [DataRow("  javascript:alert(1)  ")]
        [DataRow("file:///etc/passwd")]
        [DataRow("data:text/html;base64,PHNjcmlwdD4=")]
        [DataRow("/relative/path")]
        [DataRow("")]
        public void UnsafeHrefsAreNotActionableButTheirTextStaysLegible(string href)
        {
            var run = Runs("<p><a href=\"" + href + "\">click me</a></p>").Single();
            Assert.IsNull(run.Link, "an unsafe href must never become a clickable link");
            Assert.AreEqual("click me", run.Text);
        }

        [TestMethod]
        public void AnUnsafeLinkIsNotWrittenBackOut()
        {
            // Even if something put one on a run, the writer vets it again.
            var doc = new NoteDocument(new NoteBlock[] { new NoteParagraph(new NoteRun("x") { Link = "javascript:alert(1)" }) });
            Assert.AreEqual("<p>x</p>", NoteHtml.Write(doc));
        }

        [TestMethod]
        public void OpenableUrlAcceptsOnlyHttpAndHttps()
        {
            Assert.AreEqual("http://example.com", NoteLinks.OpenableUrl("http://example.com"));
            Assert.AreEqual("https://example.com", NoteLinks.OpenableUrl(" https://example.com "));
            Assert.IsNull(NoteLinks.OpenableUrl("ftp://example.com"));
            Assert.IsNull(NoteLinks.OpenableUrl(null));
        }

        // ------------------------------------------------------------------ plain text

        [TestMethod]
        public void PlainTextPutsParagraphsOnLines()
        {
            Assert.AreEqual("one\ntwo", NoteHtml.PlainText("<p>one</p><p>two</p>"));
        }

        [TestMethod]
        public void PlainTextMarksListItems()
        {
            Assert.AreEqual("• a\n• b", NoteHtml.PlainText("<ul><li>a</li><li>b</li></ul>"));
            Assert.AreEqual("1. a\n2. b", NoteHtml.PlainText("<ol><li>a</li><li>b</li></ol>"));
        }

        [TestMethod]
        public void PlainTextOfTextWithoutTagsIsDecodedAndTrimmed()
        {
            Assert.AreEqual("a & b", NoteHtml.PlainText("  a &amp; b \n"));
        }

        [TestMethod]
        public void PlainTextOfAnEmptyBodyIsEmpty()
        {
            Assert.AreEqual("", NoteHtml.PlainText(null));
            Assert.AreEqual("", NoteHtml.PlainText("<p>&nbsp;</p>"));
        }

        [TestMethod]
        public void BreaksBecomeNewLines()
        {
            Assert.AreEqual("one\ntwo\nthree", NoteHtml.PlainText("<p>one<br>two<br/>three</p>"));
        }
    }
}
