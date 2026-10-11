using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>`TaskReaction` / `CommentReaction` — one person's emoji on a task or on one of its comments.</summary>
    public sealed class ToDoReaction
    {
        public string Id { get; set; }
        public string TaskId { get; set; }
        public string CommentId { get; set; }
        public string UserId { get; set; }
        public string Emoji { get; set; }
    }

    /// <summary>`TaskComment`. Its reactions arrive from a separate call, as the web merges them.</summary>
    public sealed class ToDoComment
    {
        public string Id { get; set; }
        public string TaskId { get; set; }
        public string UserId { get; set; }
        public string Content { get; set; }
        public string AuthorName { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public IList<ToDoReaction> Reactions { get; set; } = new List<ToDoReaction>();
    }

    /// <summary>
    /// A task's comments and reactions (the web's <c>taskComments</c> / <c>taskReactions</c>, the Mac's
    /// <c>ToDoTaskDetailsStore</c>): reading what the API sends, and the rules the editor's toggles follow.
    /// </summary>
    public static class ToDoComments
    {
        /// <summary>The web's reaction palette, in its order.</summary>
        public static readonly IList<string> ReactionPalette = new[] { "👍", "🎉", "❤️", "🔥", "👀", "🚀" };

        public const int CommentsTake = 50;

        /// <summary>
        /// <c>GET /to-do/:id/comments</c>: <c>{ data: [...] }</c> (a bare array is read too). Each comment carries
        /// its reactions inline (<c>TaskCommentResponseDto.reactions</c>), so no per-comment fetch is needed.
        /// </summary>
        public static IList<ToDoComment> ReadComments(string json)
        {
            var token = ToDoJson.Parse(json);
            var rows = token as JArray ?? (token as JObject)?["data"] as JArray;
            if (rows == null) return null;
            return rows.OfType<JObject>().Where(row => Text(row["id"]) != null).Select(row => new ToDoComment
            {
                Id = Text(row["id"]),
                TaskId = Text(row["task_id"]),
                UserId = Text(row["user_id"]) ?? Text((row["user"] as JObject)?["id"]),
                Content = Text(row["content"]) ?? "",
                AuthorName = Text((row["user"] as JObject)?["username"]),
                CreatedAt = Text(row["created_at"]) ?? "",
                UpdatedAt = Text(row["updated_at"]),
                Reactions = ReadReactionRows(row["reactions"] as JArray, Text(row["task_id"]), Text(row["id"])),
            }).ToList();
        }

        /// <summary><c>GET /to-do/:id/reactions</c> or <c>…/comments/:id/reactions</c>: a bare array.</summary>
        public static IList<ToDoReaction> ReadReactions(string json)
        {
            var rows = ToDoJson.Parse(json) as JArray;
            if (rows == null) return null;
            return ReadReactionRows(rows, null, null);
        }

        private static IList<ToDoReaction> ReadReactionRows(JArray rows, string taskId, string commentId)
        {
            if (rows == null) return new List<ToDoReaction>();
            return rows.OfType<JObject>().Where(row => !string.IsNullOrEmpty(Text(row["emoji"]))).Select(row => new ToDoReaction
            {
                Id = Text(row["id"]),
                TaskId = Text(row["task_id"]) ?? taskId,
                CommentId = Text(row["comment_id"]) ?? commentId,
                UserId = Text(row["user_id"]) ?? Text((row["user"] as JObject)?["id"]),
                Emoji = Text(row["emoji"]),
            }).ToList();
        }

        public static int Count(IEnumerable<ToDoReaction> reactions, string emoji) =>
            (reactions ?? Enumerable.Empty<ToDoReaction>()).Count(reaction => reaction.Emoji == emoji);

        public static bool HasReacted(IEnumerable<ToDoReaction> reactions, string emoji, string userId) =>
            !string.IsNullOrEmpty(userId) && (reactions ?? Enumerable.Empty<ToDoReaction>()).Any(reaction => reaction.Emoji == emoji && reaction.UserId == userId);

        /// <summary>
        /// The list as it should look straight after a click (optimistic, like the web): the user's reaction
        /// removed when they had it, else one added. The server's answer then replaces this.
        /// </summary>
        public static IList<ToDoReaction> Toggled(IEnumerable<ToDoReaction> reactions, string emoji, string userId, string taskId, string commentId = null)
        {
            var list = (reactions ?? Enumerable.Empty<ToDoReaction>()).ToList();
            if (HasReacted(list, emoji, userId)) return list.Where(reaction => !(reaction.Emoji == emoji && reaction.UserId == userId)).ToList();
            list.Add(new ToDoReaction { Id = Guid.NewGuid().ToString(), TaskId = taskId, CommentId = commentId, UserId = userId, Emoji = emoji });
            return list;
        }

        /// <summary>A comment with no author on it is the viewer's own, as on the Mac (the API omits it on some rows).</summary>
        public static bool IsMine(ToDoComment comment, string userId) => (comment.UserId ?? userId) == userId;

        private static string Text(JToken token) => token != null && token.Type == JTokenType.String ? (string)token : null;
    }
}
