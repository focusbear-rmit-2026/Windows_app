using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FocusBear.Core.Notes
{
    /// <summary>
    /// Converts note bodies between the HTML the web dashboard stores (CKEditor output: p, strong, em,
    /// u, s, a, ul/ol/li, h1–h6, br, code, pre) and a <see cref="NoteDocument"/>. Port of the Mac's
    /// <c>Notes/NoteHTML.swift</c>, so a note edited on either desktop renders the same on the other
    /// and on the web. Hand-rolled on both platforms for the same reason: a browser-grade HTML import
    /// is slow and exports bloated styled markup the web would not render consistently.
    /// </summary>
    public static class NoteHtml
    {
        /// <summary>Blank paragraphs round-trip as CKEditor's own representation.</summary>
        public const string EmptyParagraphHtml = "<p>&nbsp;</p>";
        public const string BulletMarker = "\u2022 ";

        private static readonly HashSet<string> BlockTags = new HashSet<string>(StringComparer.Ordinal)
        {
            "p", "div", "h1", "h2", "h3", "h4", "h5", "h6", "li", "blockquote", "pre", "ul", "ol", "hr",
            "tr", "table", "section", "article", "header", "footer"
        };

        /// <summary>
        /// Elements whose content is never note text. The web editor does not emit them; a body that
        /// carries one came from somewhere else, and its script is not something to show a user.
        /// </summary>
        private static readonly HashSet<string> SkippedContentTags = new HashSet<string>(StringComparer.Ordinal)
        {
            "script", "style"
        };

        // ------------------------------------------------------------------ import

        public static NoteDocument Parse(string html)
        {
            var builder = new Builder();
            foreach (var token in NoteHtmlTokenizer.Tokenize(html ?? "")) builder.Consume(token);
            return builder.Finish();
        }

        /// <summary>
        /// Plain text for list previews and comparisons: paragraphs become lines, list items carry their
        /// "• " / "1. " marker, entities are decoded. Matches the Mac's <c>plainText(fromHTML:)</c>.
        /// </summary>
        public static string PlainText(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            if (html.IndexOf('<') < 0) return NoteHtmlEntities.Decode(html).Trim();
            var lines = new List<string>();
            AppendPlainText(Parse(html).Blocks, lines);
            return string.Join("\n", lines).Trim();
        }

        private static void AppendPlainText(IEnumerable<NoteBlock> blocks, List<string> lines)
        {
            foreach (var block in blocks)
            {
                if (block is NoteParagraph paragraph)
                {
                    lines.Add(paragraph.Text);
                }
                else if (block is NoteList list)
                {
                    int number = 0;
                    foreach (var item in list.Items)
                    {
                        number++;
                        string marker = list.Ordered ? number.ToString(CultureInfo.InvariantCulture) + ". " : BulletMarker;
                        var itemLines = new List<string>();
                        AppendPlainText(item.Blocks, itemLines);
                        if (itemLines.Count == 0) itemLines.Add("");
                        // The marker belongs to the item's first line; a nested list inside it already
                        // brought its own markers.
                        bool firstIsNestedList = item.Blocks.Count > 0 && item.Blocks[0] is NoteList;
                        if (!firstIsNestedList) itemLines[0] = marker + itemLines[0];
                        lines.AddRange(itemLines);
                    }
                }
            }
        }

        private sealed class ListFrame
        {
            internal NoteList List;
            /// <summary>How many containers were open when the list opened; its items sit above this.</summary>
            internal int ContainerDepth;
        }

        private sealed class Builder
        {
            private readonly NoteDocument _document = new NoteDocument();
            private readonly List<List<NoteBlock>> _containers = new List<List<NoteBlock>>();
            private readonly List<ListFrame> _lists = new List<ListFrame>();
            private readonly List<string> _links = new List<string>();
            private NoteParagraph _paragraph;
            private bool _blockOpened;
            private bool _listItemJustOpened;
            private int _bold, _italic, _underline, _strike, _code, _preformatted, _skipped;
            private int _headingLevel;

            internal Builder()
            {
                _containers.Add(_document.Blocks);
            }

            private List<NoteBlock> Container => _containers[_containers.Count - 1];

            internal NoteDocument Finish()
            {
                FlushParagraph();
                return _document;
            }

            internal void Consume(NoteHtmlToken token)
            {
                switch (token.Kind)
                {
                    case NoteHtmlTokenKind.Text: AppendText(token.Text); break;
                    case NoteHtmlTokenKind.Open: Open(token.Name, token.Href); break;
                    case NoteHtmlTokenKind.Close: Close(token.Name); break;
                }
            }

            private void Open(string name, string href)
            {
                if (SkippedContentTags.Contains(name)) { _skipped++; return; }
                switch (name)
                {
                    case "b": case "strong": _bold++; break;
                    case "i": case "em": _italic++; break;
                    case "u": _underline++; break;
                    case "s": case "strike": case "del": _strike++; break;
                    case "code": _code++; break;
                    case "a": _links.Add(href ?? ""); break;
                    case "br": EnsureParagraph().Inlines.Add(new NoteLineBreak()); break;
                    case "ul":
                    case "ol":
                        OpenList(name == "ol");
                        break;
                    case "li":
                        OpenListItem();
                        break;
                    case "pre":
                        FlushParagraph(); _blockOpened = true; _preformatted++; break;
                    case "h1": case "h2": case "h3": case "h4": case "h5": case "h6":
                        FlushParagraph(); _blockOpened = true; _headingLevel = name[1] - '0'; break;
                    default:
                        if (BlockTags.Contains(name))
                        {
                            // A <p> directly inside a freshly opened <li> continues that item instead of
                            // ending it; CKEditor writes list items that way.
                            if (_listItemJustOpened) { _listItemJustOpened = false; return; }
                            FlushParagraph();
                            _blockOpened = true;
                        }
                        break;
                }
                if (name != "li") _listItemJustOpened = false;
            }

            private void Close(string name)
            {
                if (SkippedContentTags.Contains(name)) { _skipped = Math.Max(_skipped - 1, 0); return; }
                switch (name)
                {
                    case "b": case "strong": _bold = Math.Max(_bold - 1, 0); break;
                    case "i": case "em": _italic = Math.Max(_italic - 1, 0); break;
                    case "u": _underline = Math.Max(_underline - 1, 0); break;
                    case "s": case "strike": case "del": _strike = Math.Max(_strike - 1, 0); break;
                    case "code": _code = Math.Max(_code - 1, 0); break;
                    case "a": if (_links.Count > 0) _links.RemoveAt(_links.Count - 1); break;
                    case "ul":
                    case "ol":
                        FlushParagraph();
                        if (_lists.Count > 0)
                        {
                            PopContainersTo(_lists[_lists.Count - 1].ContainerDepth);
                            _lists.RemoveAt(_lists.Count - 1);
                        }
                        break;
                    case "li":
                        FlushParagraph();
                        if (_lists.Count > 0) PopContainersTo(_lists[_lists.Count - 1].ContainerDepth);
                        break;
                    case "pre":
                        FlushParagraph(); _preformatted = Math.Max(_preformatted - 1, 0); break;
                    case "h1": case "h2": case "h3": case "h4": case "h5": case "h6":
                        FlushParagraph(); _headingLevel = 0; break;
                    default:
                        if (BlockTags.Contains(name)) FlushParagraph();
                        break;
                }
                _listItemJustOpened = false;
            }

            private void OpenList(bool ordered)
            {
                FlushParagraph();
                var list = new NoteList { Ordered = ordered };
                if (_lists.Count > 0 && _containers.Count == _lists[_lists.Count - 1].ContainerDepth)
                {
                    // A list opened directly inside another, between its items — the shape the Mac
                    // exports (`<ul><li>a</li><ul><li>b</li></ul></ul>`) and CKEditor tolerates. It is a
                    // nested list of the previous item, not a sibling of the outer one.
                    var outer = _lists[_lists.Count - 1].List;
                    if (outer.Items.Count == 0) outer.Items.Add(new NoteListItem());
                    _containers.Add(outer.Items[outer.Items.Count - 1].Blocks);
                }
                Container.Add(list);
                _lists.Add(new ListFrame { List = list, ContainerDepth = _containers.Count });
            }

            private void OpenListItem()
            {
                FlushParagraph();
                if (_lists.Count == 0)
                {
                    var implicitList = new NoteList();
                    Container.Add(implicitList);
                    _lists.Add(new ListFrame { List = implicitList, ContainerDepth = _containers.Count });
                }
                var frame = _lists[_lists.Count - 1];
                // An unclosed previous <li> ends here.
                PopContainersTo(frame.ContainerDepth);
                var item = new NoteListItem();
                frame.List.Items.Add(item);
                _containers.Add(item.Blocks);
                _blockOpened = true;
                _listItemJustOpened = true;
            }

            private void PopContainersTo(int depth)
            {
                while (_containers.Count > Math.Max(depth, 1)) _containers.RemoveAt(_containers.Count - 1);
            }

            private NoteParagraph EnsureParagraph()
            {
                if (_paragraph == null) _paragraph = new NoteParagraph { HeadingLevel = _headingLevel };
                return _paragraph;
            }

            private void AppendText(string rawText)
            {
                if (_skipped > 0) return;
                string text = rawText;
                if (_preformatted == 0)
                {
                    text = CollapseWhitespace(text);
                    if (text.StartsWith(" ", StringComparison.Ordinal)
                        && (_paragraph == null || _paragraph.Inlines.Count == 0 || EndsWithSpaceOrBreak(_paragraph) || _listItemJustOpened))
                    {
                        text = text.Substring(1);
                    }
                    if (text.Length == 0) return;
                    _listItemJustOpened = false;
                    AddRun(text);
                    return;
                }

                // Preformatted text keeps its spacing; its newlines are line breaks within the block.
                string[] lines = text.Replace("\r\n", "\n").Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    if (i > 0) EnsureParagraph().Inlines.Add(new NoteLineBreak());
                    if (lines[i].Length > 0) AddRun(lines[i]);
                }
                _listItemJustOpened = false;
            }

            private void AddRun(string text)
            {
                var run = new NoteRun(text)
                {
                    Bold = _bold > 0,
                    Italic = _italic > 0,
                    Underline = _underline > 0,
                    Strikethrough = _strike > 0,
                    Code = _code > 0 || _preformatted > 0,
                    Link = _links.Count > 0 ? NoteLinks.OpenableUrl(_links[_links.Count - 1]) : null,
                };
                var inlines = EnsureParagraph().Inlines;
                if (inlines.Count > 0 && inlines[inlines.Count - 1] is NoteRun previous && previous.SameStyleAs(run))
                    previous.Text += text;
                else
                    inlines.Add(run);
            }

            private static bool EndsWithSpaceOrBreak(NoteParagraph paragraph)
            {
                var last = paragraph.Inlines[paragraph.Inlines.Count - 1];
                return last is NoteLineBreak || (last is NoteRun run && run.Text.EndsWith(" ", StringComparison.Ordinal));
            }

            private void FlushParagraph()
            {
                if (_paragraph != null) TrimTrailingSpace(_paragraph);
                bool hasContent = _paragraph != null && _paragraph.Inlines.Count > 0;
                if (hasContent) Container.Add(_paragraph);
                else if (_blockOpened) Container.Add(new NoteParagraph { HeadingLevel = _headingLevel });
                _paragraph = null;
                _blockOpened = false;
                _listItemJustOpened = false;
            }

            /// <summary>So "text " round-trips as "text".</summary>
            private static void TrimTrailingSpace(NoteParagraph paragraph)
            {
                while (paragraph.Inlines.Count > 0 && paragraph.Inlines[paragraph.Inlines.Count - 1] is NoteRun run)
                {
                    run.Text = run.Text.TrimEnd(' ');
                    if (run.Text.Length > 0) break;
                    paragraph.Inlines.RemoveAt(paragraph.Inlines.Count - 1);
                }
            }
        }

        private static string CollapseWhitespace(string text)
        {
            var output = new StringBuilder(text.Length);
            bool previousWasSpace = false;
            foreach (char c in text)
            {
                if (c == ' ' || c == '\n' || c == '\t' || c == '\r')
                {
                    if (!previousWasSpace) output.Append(' ');
                    previousWasSpace = true;
                }
                else
                {
                    output.Append(c);
                    previousWasSpace = false;
                }
            }
            return output.ToString();
        }

        // ------------------------------------------------------------------ export

        public static string Write(NoteDocument document)
        {
            var blocks = document.Blocks.ToList();
            // Drop trailing blank paragraphs: an editor ending in Enter would otherwise save a blank <p>.
            while (blocks.Count > 0 && blocks[blocks.Count - 1] is NoteParagraph last && last.IsBlank) blocks.RemoveAt(blocks.Count - 1);
            var output = new StringBuilder();
            foreach (var block in blocks) WriteBlock(block, output);
            return output.ToString();
        }

        private static void WriteBlock(NoteBlock block, StringBuilder output)
        {
            if (block is NoteParagraph paragraph)
            {
                if (paragraph.IsBlank) { output.Append(EmptyParagraphHtml); return; }
                if (paragraph.IsHeading)
                {
                    output.Append("<h").Append(paragraph.HeadingLevel).Append('>')
                        .Append(InlineHtml(paragraph.Inlines, inHeading: true))
                        .Append("</h").Append(paragraph.HeadingLevel).Append('>');
                }
                else
                {
                    output.Append("<p>").Append(InlineHtml(paragraph.Inlines, inHeading: false)).Append("</p>");
                }
                return;
            }
            if (block is NoteList list)
            {
                string tag = list.Ordered ? "ol" : "ul";
                output.Append('<').Append(tag).Append('>');
                foreach (var item in list.Items)
                {
                    output.Append("<li>");
                    bool wroteText = false;
                    foreach (var child in item.Blocks)
                    {
                        if (child is NoteParagraph itemParagraph)
                        {
                            // An item's text is written inline, as CKEditor does; a second paragraph in the
                            // same item continues on a new line rather than running into the first.
                            // An empty paragraph writes nothing, and must not count as text: it made the next one
                            // start with a <br>, which read back as a paragraph of its own - one more <br> on every
                            // save (found by fuzzing: "<li><ol></ol>text").
                            string inline = InlineHtml(itemParagraph.Inlines, inHeading: false);
                            if (inline.Length == 0) continue;
                            if (wroteText) output.Append("<br>");
                            output.Append(inline);
                            wroteText = true;
                        }
                        else
                        {
                            WriteBlock(child, output);
                            // A nested list is a break already: text after it starts its own line without a <br>
                            // (with one, each read-and-write added another).
                            wroteText = false;
                        }
                    }
                    output.Append("</li>");
                }
                output.Append("</").Append(tag).Append('>');
            }
        }

        /// <summary>
        /// Innermost first, matching the Mac's export: code, s, u, em, strong, then the link. Adjacent runs
        /// with the same styles are written as one: an editor splits text into many runs as it is typed
        /// into, and writing each separately would fragment the HTML a little more on every save.
        /// </summary>
        private static string InlineHtml(IEnumerable<NoteInline> inlines, bool inHeading)
        {
            var output = new StringBuilder();
            foreach (var inline in MergeRuns(inlines))
            {
                if (inline is NoteLineBreak) { output.Append("<br>"); continue; }
                var run = (NoteRun)inline;
                if (string.IsNullOrEmpty(run.Text)) continue;
                string fragment = Escape(run.Text);
                if (run.Code) fragment = "<code>" + fragment + "</code>";
                if (run.Strikethrough) fragment = "<s>" + fragment + "</s>";
                if (run.Underline) fragment = "<u>" + fragment + "</u>";
                if (run.Italic) fragment = "<em>" + fragment + "</em>";
                // A heading is bold by definition; saying so again would make every heading "strong".
                if (run.Bold && !inHeading) fragment = "<strong>" + fragment + "</strong>";
                string link = NoteLinks.OpenableUrl(run.Link);
                if (link != null) fragment = "<a href=\"" + Escape(link) + "\">" + fragment + "</a>";
                output.Append(fragment);
            }
            return output.ToString();
        }

        private static IEnumerable<NoteInline> MergeRuns(IEnumerable<NoteInline> inlines)
        {
            NoteRun pending = null;
            foreach (var inline in inlines)
            {
                var run = inline as NoteRun;
                if (run != null && string.IsNullOrEmpty(run.Text)) continue;
                if (run != null && pending != null && pending.SameStyleAs(run))
                {
                    pending = new NoteRun(pending.Text + run.Text)
                    {
                        Bold = run.Bold, Italic = run.Italic, Underline = run.Underline,
                        Strikethrough = run.Strikethrough, Code = run.Code, Link = run.Link,
                    };
                    continue;
                }
                if (pending != null) yield return pending;
                pending = run;
                if (run == null) yield return inline;
            }
            if (pending != null) yield return pending;
        }

        private static string Escape(string text) =>
            text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }

    public enum NoteHtmlTokenKind { Text, Open, Close }

    public sealed class NoteHtmlToken
    {
        public NoteHtmlTokenKind Kind { get; }
        /// <summary>Lower-cased tag name for Open/Close.</summary>
        public string Name { get; }
        /// <summary>Entity-decoded text for Text.</summary>
        public string Text { get; }
        /// <summary>The raw (decoded) href of an <c>&lt;a&gt;</c>; not yet vetted.</summary>
        public string Href { get; }

        public NoteHtmlToken(NoteHtmlTokenKind kind, string name, string text, string href)
        {
            Kind = kind;
            Name = name;
            Text = text;
            Href = href;
        }
    }

    /// <summary>Port of <c>NoteHTML.tokenize</c>.</summary>
    public static class NoteHtmlTokenizer
    {
        private static readonly Regex HrefPattern = new Regex("href\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)')", RegexOptions.IgnoreCase);

        public static List<NoteHtmlToken> Tokenize(string html)
        {
            var tokens = new List<NoteHtmlToken>();
            var text = new StringBuilder();
            int index = 0;

            void FlushText()
            {
                if (text.Length == 0) return;
                tokens.Add(new NoteHtmlToken(NoteHtmlTokenKind.Text, null, NoteHtmlEntities.Decode(text.ToString()), null));
                text.Clear();
            }

            while (index < html.Length)
            {
                char c = html[index];
                if (c != '<') { text.Append(c); index++; continue; }

                // Comments and doctype: skip to the closing delimiter.
                if (index + 1 < html.Length && html[index + 1] == '!')
                {
                    FlushText();
                    if (string.CompareOrdinal(html, index, "<!--", 0, 4) == 0)
                    {
                        int endComment = html.IndexOf("-->", index + 4, StringComparison.Ordinal);
                        index = endComment < 0 ? html.Length : endComment + 3;
                    }
                    else
                    {
                        int endDecl = html.IndexOf('>', index);
                        index = endDecl < 0 ? html.Length : endDecl + 1;
                    }
                    continue;
                }

                // A "<" only opens a tag when a name (or "/name") follows it. Without this, prose such as
                // "5 < 6 and 7 > 6" is read as a tag running to the next ">" and swallowed whole.
                if (!StartsTagName(html, index))
                {
                    text.Append(c);
                    index++;
                    continue;
                }

                int end = html.IndexOf('>', index + 1);
                if (end < 0)
                {
                    // Unterminated "<": literal.
                    text.Append(c);
                    index++;
                    continue;
                }

                FlushText();
                string inner = html.Substring(index + 1, end - index - 1).Trim();
                index = end + 1;
                bool isClosing = inner.StartsWith("/", StringComparison.Ordinal);
                if (isClosing) inner = inner.Substring(1);
                if (inner.EndsWith("/", StringComparison.Ordinal)) inner = inner.Substring(0, inner.Length - 1);
                int nameEnd = inner.IndexOfAny(new[] { ' ', '\n', '\t', '\r' });
                string name = (nameEnd < 0 ? inner : inner.Substring(0, nameEnd)).ToLowerInvariant();
                if (name.Length == 0) continue;
                if (isClosing)
                {
                    tokens.Add(new NoteHtmlToken(NoteHtmlTokenKind.Close, name, null, null));
                    continue;
                }
                string href = null;
                if (name == "a" && nameEnd >= 0)
                {
                    var match = HrefPattern.Match(inner.Substring(nameEnd));
                    if (match.Success)
                        href = NoteHtmlEntities.Decode(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
                }
                tokens.Add(new NoteHtmlToken(NoteHtmlTokenKind.Open, name, null, href));
            }
            FlushText();
            return tokens;
        }

        private static bool IsAsciiLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        private static bool StartsTagName(string html, int index)
        {
            if (index + 1 >= html.Length) return false;
            char next = html[index + 1];
            if (IsAsciiLetter(next)) return true;
            return next == '/' && index + 2 < html.Length && IsAsciiLetter(html[index + 2]);
        }
    }

    /// <summary>
    /// Port of <c>NoteHTML.decodeEntities</c>: the named entities the web editor emits, and numeric ones.
    /// Deliberately not a full HTML decoder — an unknown entity is left exactly as written, which is
    /// what the Mac shows for the same note.
    /// </summary>
    public static class NoteHtmlEntities
    {
        private static readonly Dictionary<string, string> Named = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "amp", "&" }, { "lt", "<" }, { "gt", ">" }, { "quot", "\"" }, { "apos", "'" }, { "nbsp", " " },
            { "ndash", "\u2013" }, { "mdash", "\u2014" }, { "hellip", "\u2026" }, { "copy", "\u00A9" },
            { "lsquo", "\u2018" }, { "rsquo", "\u2019" }, { "ldquo", "\u201C" }, { "rdquo", "\u201D" },
        };

        /// <summary>Longest entity name looked for after "&amp;", as on the Mac.</summary>
        private const int MaxEntityLength = 10;

        public static string Decode(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('&') < 0) return text ?? "";
            var output = new StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                int amp = text.IndexOf('&', i);
                if (amp < 0) { output.Append(text, i, text.Length - i); break; }
                output.Append(text, i, amp - i);
                int semicolon = text.IndexOf(';', amp + 1);
                if (semicolon < 0 || semicolon - amp - 1 > MaxEntityLength)
                {
                    output.Append('&');
                    i = amp + 1;
                    continue;
                }
                string entity = text.Substring(amp + 1, semicolon - amp - 1);
                string decoded = DecodeOne(entity);
                if (decoded == null)
                {
                    output.Append('&');
                    i = amp + 1;
                    continue;
                }
                output.Append(decoded);
                i = semicolon + 1;
            }
            return output.ToString();
        }

        private static string DecodeOne(string entity)
        {
            if (Named.TryGetValue(entity, out string named)) return named;
            if (entity.Length < 2 || entity[0] != '#') return null;
            bool isHex = entity[1] == 'x' || entity[1] == 'X';
            string digits = isHex ? entity.Substring(2) : entity.Substring(1);
            if (digits.Length == 0) return null;
            bool parsed = isHex
                ? int.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int value)
                : int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out value);
            if (!parsed || value < 0 || value > 0x10FFFF || (value >= 0xD800 && value <= 0xDFFF)) return null;
            return char.ConvertFromUtf32(value);
        }
    }

    /// <summary>
    /// Which hrefs may become clickable. A note body is content this app did not write — it syncs in
    /// from the web editor — so a <c>javascript:</c>, <c>file:</c> or <c>data:</c> href must never turn
    /// into something the user can activate. Those render as ordinary text: still legible, not actionable.
    /// Port of <c>NoteHTML.openableURL</c>.
    /// </summary>
    public static class NoteLinks
    {
        public static string OpenableUrl(string href)
        {
            if (href == null) return null;
            string trimmed = href.Trim();
            if (trimmed.Length == 0) return null;
            if (!FocusBear.Core.SafeUri.TryCreate(trimmed, UriKind.Absolute, out Uri uri)) return null;
            string scheme = uri.Scheme.ToLowerInvariant();
            return scheme == "http" || scheme == "https" ? trimmed : null;
        }
    }
}
