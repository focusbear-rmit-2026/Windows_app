using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Notes
{
    /// <summary>
    /// Reads a project note's body — a ProseMirror (TipTap) JSON document — without modelling ProseMirror.
    /// The body is written by a collaborative Yjs editor on the web, so this side only ever reads it:
    /// writing content through the REST endpoint would be invisible to that editor and clobbered by it.
    /// </summary>
    public static class ProseMirrorNote
    {
        /// <summary>Nodes after which the Mac's flattening starts a new line.</summary>
        private static readonly HashSet<string> LineEndingNodes = new HashSet<string>
        {
            "paragraph", "heading", "blockquote", "codeBlock", "taskListEmbed"
        };

        private static readonly Regex ExtraBlankLines = new Regex("\n{3,}");

        /// <summary>
        /// The body flattened to plain text, paragraph breaks kept — for previews and search. Port of the
        /// Mac's <c>JSONValue.plainText</c>, so both desktops preview a shared note alike â€” with one deliberate
        /// difference: a <c>hardBreak</c> is a new line here, where the Mac drops it and runs "Friday" into
        /// "site" as "Fridaysite".
        /// </summary>
        public static string PlainText(JToken content)
        {
            if (content == null || content.Type == JTokenType.Null) return "";
            var output = new StringBuilder();
            Walk(content, output);
            return ExtraBlankLines.Replace(output.ToString(), "\n\n").Trim();
        }

        private static void Walk(JToken node, StringBuilder output)
        {
            if (node is JObject obj)
            {
                if (obj["text"] is JValue text && text.Type == JTokenType.String) output.Append((string)text);
                if (obj["type"] is JValue breakType && (string)breakType == "hardBreak") output.Append('\n');
                if (obj["content"] is JArray children) foreach (var child in children) Walk(child, output);
                if (obj["type"] is JValue type && type.Type == JTokenType.String && LineEndingNodes.Contains((string)type)) output.Append('\n');
            }
            else if (node is JArray array)
            {
                foreach (var item in array) Walk(item, output);
            }
        }

        /// <summary>
        /// The body as a <see cref="NoteDocument"/>, for a native read-only view. Covers the nodes the web's
        /// TipTap editor writes; an unknown block contributes its text as a paragraph rather than vanishing.
        /// </summary>
        public static NoteDocument ToDocument(JToken content)
        {
            var document = new NoteDocument();
            if (content is JObject root) AppendBlocks(Children(root), document.Blocks);
            return document;
        }

        private static IEnumerable<JObject> Children(JObject node) =>
            (node["content"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>();

        private static string TypeOf(JObject node) => node["type"]?.Type == JTokenType.String ? (string)node["type"] : "";

        private static void AppendBlocks(IEnumerable<JObject> nodes, List<NoteBlock> blocks)
        {
            foreach (var node in nodes)
            {
                switch (TypeOf(node))
                {
                    case "paragraph":
                        blocks.Add(Paragraph(node, 0, code: false));
                        break;
                    case "heading":
                        int level = (node["attrs"] as JObject)?["level"]?.Type == JTokenType.Integer ? (int)node["attrs"]["level"] : 1;
                        blocks.Add(Paragraph(node, level < 1 || level > 6 ? 1 : level, code: false));
                        break;
                    case "codeBlock":
                        blocks.Add(Paragraph(node, 0, code: true));
                        break;
                    case "bulletList":
                    case "orderedList":
                    case "taskList":
                        blocks.Add(List(node, ordered: TypeOf(node) == "orderedList"));
                        break;
                    case "blockquote":
                        AppendBlocks(Children(node), blocks);
                        break;
                    case "horizontalRule":
                        blocks.Add(new NoteParagraph());
                        break;
                    case "text":
                    case "hardBreak":
                        // Inline content at block level: gather it into one paragraph.
                        var loose = new NoteParagraph();
                        AppendInline(node, loose.Inlines, code: false);
                        blocks.Add(loose);
                        break;
                    default:
                        if (Children(node).Any()) AppendBlocks(Children(node), blocks);
                        break;
                }
            }
        }

        private static NoteList List(JObject node, bool ordered)
        {
            var list = new NoteList { Ordered = ordered };
            foreach (var itemNode in Children(node))
            {
                var item = new NoteListItem();
                AppendBlocks(Children(itemNode), item.Blocks);
                if (TypeOf(itemNode) == "taskItem" && item.Blocks.FirstOrDefault() is NoteParagraph first)
                {
                    bool done = (itemNode["attrs"] as JObject)?["checked"]?.Type == JTokenType.Boolean && (bool)itemNode["attrs"]["checked"];
                    first.Inlines.Insert(0, new NoteRun(done ? "☑ " : "☐ "));
                }
                list.Items.Add(item);
            }
            return list;
        }

        private static NoteParagraph Paragraph(JObject node, int headingLevel, bool code)
        {
            var paragraph = new NoteParagraph { HeadingLevel = headingLevel };
            foreach (var child in Children(node)) AppendInline(child, paragraph.Inlines, code);
            return paragraph;
        }

        private static void AppendInline(JObject node, List<NoteInline> inlines, bool code)
        {
            string type = TypeOf(node);
            if (type == "hardBreak") { inlines.Add(new NoteLineBreak()); return; }
            if (type != "text") { foreach (var child in Children(node)) AppendInline(child, inlines, code); return; }
            string text = node["text"]?.Type == JTokenType.String ? (string)node["text"] : "";
            if (text.Length == 0) return;
            var run = new NoteRun(text) { Code = code };
            foreach (var mark in (node["marks"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
            {
                switch (TypeOf(mark))
                {
                    case "bold": run.Bold = true; break;
                    case "italic": run.Italic = true; break;
                    case "underline": run.Underline = true; break;
                    case "strike": run.Strikethrough = true; break;
                    case "code": run.Code = true; break;
                    case "link":
                        run.Link = NoteLinks.OpenableUrl((mark["attrs"] as JObject)?["href"]?.Type == JTokenType.String ? (string)mark["attrs"]["href"] : null);
                        break;
                }
            }
            if (code && text.Contains("\n"))
            {
                // A code block keeps its lines.
                var lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    if (i > 0) inlines.Add(new NoteLineBreak());
                    if (lines[i].Length > 0) inlines.Add(new NoteRun(lines[i]) { Code = true, Bold = run.Bold, Italic = run.Italic });
                }
                return;
            }
            inlines.Add(run);
        }
    }
}
