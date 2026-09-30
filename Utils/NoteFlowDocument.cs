using FocusBear.Core.Notes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Focus_Bear.Utils
{
    /// <summary>
    /// The WPF half of the note-body conversion: <see cref="NoteDocument"/> to and from the FlowDocument a
    /// RichTextBox edits. Everything about what the HTML means lives in <c>FocusBear.Core.Notes.NoteHtml</c>;
    /// this only maps structure onto WPF elements and back, so the rules stay testable without WPF.
    /// </summary>
    public static class NoteFlowDocument
    {
        private static readonly Dictionary<int, double> HeadingSizeMultipliers = new Dictionary<int, double> { { 1, 1.6 }, { 2, 1.35 }, { 3, 1.15 } };
        private static readonly FontFamily CodeFont = new FontFamily("Consolas");

        /// <summary>An HTML note body as a document ready for a RichTextBox (editable) or a FlowDocumentScrollViewer.</summary>
        public static FlowDocument FromHtml(string html, double baseFontSize)
        {
            return FromDocument(NoteHtml.Parse(html ?? ""), baseFontSize);
        }

        /// <summary>
        /// Put a note into an editor without replacing its FlowDocument. Assigning RichTextBox.Document leaves
        /// the box's UI Automation text provider on the old, empty document, so Narrator and NVDA read an
        /// edited note as blank (found by the E2E Notes tests). Moving the blocks keeps the same document.
        /// </summary>
        public static void Load(System.Windows.Controls.RichTextBox box, string html, double baseFontSize)
        {
            FlowDocument source = FromHtml(html, baseFontSize);
            FlowDocument target = box.Document;
            if (target == null)
            {
                box.Document = source;
                return;
            }
            // Loading must not be undoable (Ctrl+Z would empty the note); toggling undo also clears the
            // history, as a new document used to.
            bool undo = box.IsUndoEnabled;
            box.IsUndoEnabled = false;
            try
            {
                target.PagePadding = source.PagePadding;
                target.FontSize = source.FontSize;
                target.Blocks.Clear();
                foreach (Block block in source.Blocks.ToList())
                {
                    source.Blocks.Remove(block);
                    target.Blocks.Add(block);
                }
                box.CaretPosition = target.ContentStart;
            }
            finally { box.IsUndoEnabled = undo; }
        }

        /// <summary>The editor's content as HTML the web and the Mac read.</summary>
        public static string ToHtml(FlowDocument document)
        {
            return NoteHtml.Write(ToDocument(document));
        }

        public static FlowDocument FromDocument(NoteDocument note, double baseFontSize)
        {
            var document = new FlowDocument { PagePadding = new Thickness(0), FontSize = baseFontSize };
            AddBlocks(note.Blocks, document.Blocks, baseFontSize);
            if (document.Blocks.Count == 0) document.Blocks.Add(new Paragraph { Margin = new Thickness(0) });
            return document;
        }

        private static void AddBlocks(IEnumerable<NoteBlock> blocks, BlockCollection target, double baseFontSize)
        {
            foreach (var block in blocks)
            {
                var paragraph = block as NoteParagraph;
                if (paragraph != null) { target.Add(ToParagraph(paragraph, baseFontSize)); continue; }
                var list = block as NoteList;
                if (list == null) continue;
                var wpfList = new List
                {
                    MarkerStyle = list.Ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                    Margin = new Thickness(0),
                    Padding = new Thickness(baseFontSize * 1.5, 0, 0, 0),
                };
                foreach (var item in list.Items)
                {
                    var listItem = new ListItem();
                    AddBlocks(item.Blocks, listItem.Blocks, baseFontSize);
                    if (listItem.Blocks.Count == 0) listItem.Blocks.Add(new Paragraph { Margin = new Thickness(0) });
                    wpfList.ListItems.Add(listItem);
                }
                target.Add(wpfList);
            }
        }

        private static Paragraph ToParagraph(NoteParagraph source, double baseFontSize)
        {
            var paragraph = new Paragraph { Margin = new Thickness(0) };
            if (source.IsHeading)
            {
                // The level rides on Tag so it exports back as the same <hN>.
                paragraph.Tag = source.HeadingLevel;
                double multiplier;
                if (!HeadingSizeMultipliers.TryGetValue(source.HeadingLevel, out multiplier)) multiplier = 1.0;
                paragraph.FontSize = Math.Round(baseFontSize * multiplier);
                paragraph.FontWeight = FontWeights.Bold;
                paragraph.Margin = new Thickness(0, 4, 0, 2);
            }
            foreach (var inline in source.Inlines)
            {
                if (inline is NoteLineBreak) { paragraph.Inlines.Add(new LineBreak()); continue; }
                var run = (NoteRun)inline;
                var wpfRun = new Run(run.Text);
                if (run.Bold) wpfRun.FontWeight = FontWeights.Bold;
                if (run.Italic) wpfRun.FontStyle = FontStyles.Italic;
                if (run.Code) wpfRun.FontFamily = CodeFont;
                if (run.Underline || run.Strikethrough)
                {
                    var decorations = new TextDecorationCollection();
                    if (run.Underline) decorations.Add(TextDecorations.Underline);
                    if (run.Strikethrough) decorations.Add(TextDecorations.Strikethrough);
                    wpfRun.TextDecorations = decorations;
                }
                Uri uri;
                if (run.Link != null && FocusBear.Core.SafeUri.TryCreate(run.Link, UriKind.Absolute, out uri))
                {
                    var link = new Hyperlink(wpfRun) { NavigateUri = uri, ToolTip = run.Link };
                    link.RequestNavigate += OpenLink;
                    paragraph.Inlines.Add(link);
                }
                else
                {
                    paragraph.Inlines.Add(wpfRun);
                }
            }
            return paragraph;
        }

        /// <summary>
        /// Opens a note link in the browser. The href was vetted when the note was read (http/https only), and
        /// is checked again here because a link typed in the editor never went through that reader.
        /// </summary>
        private static void OpenLink(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            string url = NoteLinks.OpenableUrl(e.Uri != null ? e.Uri.OriginalString : null);
            if (url == null) return;
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { LogWriter.LogWrite("NoteFlowDocument open link ex - " + ex.Message); }
            e.Handled = true;
        }

        // ------------------------------------------------------------------ FlowDocument -> NoteDocument

        public static NoteDocument ToDocument(FlowDocument document)
        {
            var note = new NoteDocument();
            ReadBlocks(document.Blocks, note.Blocks);
            return note;
        }

        private static void ReadBlocks(IEnumerable<Block> blocks, List<NoteBlock> target)
        {
            foreach (var block in blocks)
            {
                var paragraph = block as Paragraph;
                if (paragraph != null) { target.Add(ReadParagraph(paragraph)); continue; }
                var list = block as List;
                if (list != null)
                {
                    var noteList = new NoteList { Ordered = IsOrdered(list.MarkerStyle) };
                    foreach (var item in list.ListItems)
                    {
                        var noteItem = new NoteListItem();
                        ReadBlocks(item.Blocks, noteItem.Blocks);
                        noteList.Items.Add(noteItem);
                    }
                    target.Add(noteList);
                    continue;
                }
                var section = block as Section;
                if (section != null) ReadBlocks(section.Blocks, target);
                // Tables, images and other pasted blocks have no place in a note body and are dropped,
                // as the web editor would drop them.
            }
        }

        private static bool IsOrdered(TextMarkerStyle style)
        {
            return style == TextMarkerStyle.Decimal || style == TextMarkerStyle.LowerLatin || style == TextMarkerStyle.UpperLatin
                || style == TextMarkerStyle.LowerRoman || style == TextMarkerStyle.UpperRoman;
        }

        private static NoteParagraph ReadParagraph(Paragraph paragraph)
        {
            var note = new NoteParagraph { HeadingLevel = paragraph.Tag is int ? (int)paragraph.Tag : 0 };
            ReadInlines(paragraph.Inlines, note.Inlines, false, false, null);
            return note;
        }

        /// <summary>
        /// Weight, style and font family are inherited in WPF, so a run already reports what its Bold/Italic
        /// ancestors applied. Text decorations are not inherited, so underline and strikethrough are carried
        /// down from each enclosing span by hand, as is the link.
        /// </summary>
        private static void ReadInlines(InlineCollection inlines, List<NoteInline> target, bool underline, bool strike, string link)
        {
            foreach (var inline in inlines)
            {
                if (inline is LineBreak) { target.Add(new NoteLineBreak()); continue; }
                // A Hyperlink draws its own underline (its default style); that says "link", not "the user
                // underlined this". Counting it would add <u> to every link on every save.
                bool runUnderline = underline || (!(inline is Hyperlink) && HasDecoration(inline.TextDecorations, TextDecorationLocation.Underline));
                bool runStrike = strike || HasDecoration(inline.TextDecorations, TextDecorationLocation.Strikethrough);
                var run = inline as Run;
                if (run != null)
                {
                    if (string.IsNullOrEmpty(run.Text)) continue;
                    target.Add(new NoteRun(run.Text)
                    {
                        Bold = run.FontWeight.ToOpenTypeWeight() >= FontWeights.Bold.ToOpenTypeWeight(),
                        Italic = run.FontStyle == FontStyles.Italic,
                        Underline = runUnderline,
                        Strikethrough = runStrike,
                        Code = run.FontFamily != null && run.FontFamily.Source.IndexOf("Consolas", StringComparison.OrdinalIgnoreCase) >= 0,
                        Link = link,
                    });
                    continue;
                }
                var span = inline as Span;
                if (span != null)
                {
                    var hyperlink = span as Hyperlink;
                    string spanLink = hyperlink != null && hyperlink.NavigateUri != null ? NoteLinks.OpenableUrl(hyperlink.NavigateUri.OriginalString) : link;
                    ReadInlines(span.Inlines, target, runUnderline || span is Underline, runStrike, spanLink);
                }
                // InlineUIContainer (a pasted image or control) carries no text and is dropped.
            }
        }

        private static bool HasDecoration(TextDecorationCollection decorations, TextDecorationLocation location)
        {
            return decorations != null && decorations.Any(d => d.Location == location);
        }

        /// <summary>Whether the editor holds any text at all (the placeholder shows when it does not).</summary>
        public static bool IsEmpty(FlowDocument document)
        {
            return string.IsNullOrWhiteSpace(new TextRange(document.ContentStart, document.ContentEnd).Text);
        }
    }
}
