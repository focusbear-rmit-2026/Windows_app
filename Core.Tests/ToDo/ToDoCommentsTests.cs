using System.Linq;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    [TestClass]
    public class ToDoCommentsTests
    {
        [TestMethod]
        public void CommentsAreReadFromTheEnvelope_AuthorFromTheUserRelation()
        {
            var comments = ToDoComments.ReadComments(@"{ ""data"": [
              { ""id"": ""c1"", ""task_id"": ""t1"", ""content"": ""Looks good"", ""user"": { ""id"": ""u2"", ""username"": ""sam"" }, ""created_at"": ""2026-09-24T10:00:00.000Z"" },
              { ""content"": ""no id"" }
            ] }");
            var comment = comments.Single();
            Assert.AreEqual("u2", comment.UserId);
            Assert.AreEqual("sam", comment.AuthorName);
            Assert.AreEqual("2026-09-24T10:00:00.000Z", comment.CreatedAt);
            Assert.IsNull(ToDoComments.ReadComments("nope"));
        }

        [TestMethod]
        public void EachCommentsReactionsAreReadFromTheCommentItself()
        {
            // TaskCommentResponseDto carries `reactions` inline; the editor used to fetch them again, one
            // request and one full redraw per comment.
            var comment = ToDoComments.ReadComments(@"{ ""data"": [
              { ""id"": ""c1"", ""task_id"": ""t1"", ""user_id"": ""u2"", ""content"": ""Nice"", ""reactions"": [
                  { ""id"": ""r1"", ""comment_id"": ""c1"", ""user_id"": ""u1"", ""emoji"": ""👍"" },
                  { ""id"": ""r2"", ""emoji"": ""🎉"", ""user"": { ""id"": ""u3"" } },
                  { ""id"": ""r3"" } ] },
              { ""id"": ""c2"", ""content"": ""none"" }
            ] }")[0];

            Assert.AreEqual(2, comment.Reactions.Count);
            Assert.IsTrue(ToDoComments.HasReacted(comment.Reactions, "👍", "u1"));
            Assert.IsTrue(ToDoComments.HasReacted(comment.Reactions, "🎉", "u3"), "the user relation stands in for a missing user_id");
            Assert.AreEqual("c1", comment.Reactions[1].CommentId);
            Assert.AreEqual("t1", comment.Reactions[1].TaskId);
        }

        [TestMethod]
        public void ACommentWithNoReactionsKeyHasAnEmptyList()
        {
            var comment = ToDoComments.ReadComments(@"[{ ""id"": ""c2"", ""content"": ""none"" }]").Single();
            Assert.AreEqual(0, comment.Reactions.Count);
        }

        [TestMethod]
        public void ReactionsWithoutAnEmojiAreDropped()
        {
            var reactions = ToDoComments.ReadReactions(@"[{ ""id"": ""r1"", ""user_id"": ""u1"", ""emoji"": ""👍"" }, { ""id"": ""r2"", ""emoji"": """" }]");
            Assert.AreEqual(1, reactions.Count);
        }

        [TestMethod]
        public void ATogglesAddsTheViewersReaction_AndASecondTakesItBack()
        {
            var start = ToDoComments.ReadReactions(@"[{ ""id"": ""r1"", ""user_id"": ""other"", ""emoji"": ""🎉"" }]");
            var added = ToDoComments.Toggled(start, "🎉", "me", "t1");
            Assert.AreEqual(2, ToDoComments.Count(added, "🎉"));
            Assert.IsTrue(ToDoComments.HasReacted(added, "🎉", "me"));
            var removed = ToDoComments.Toggled(added, "🎉", "me", "t1");
            Assert.AreEqual(1, ToDoComments.Count(removed, "🎉"), "someone else's reaction stays");
            Assert.IsFalse(ToDoComments.HasReacted(removed, "🎉", "me"));
            Assert.IsFalse(ToDoComments.HasReacted(removed, "🎉", ""), "nobody signed in reacts to nothing");
        }

        [TestMethod]
        public void ACommentWithoutAnAuthorIsTheViewersOwn()
        {
            Assert.IsTrue(ToDoComments.IsMine(new ToDoComment { UserId = null }, "me"));
            Assert.IsTrue(ToDoComments.IsMine(new ToDoComment { UserId = "me" }, "me"));
            Assert.IsFalse(ToDoComments.IsMine(new ToDoComment { UserId = "you" }, "me"));
        }
    }
}
