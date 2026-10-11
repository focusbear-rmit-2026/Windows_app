using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    /// <summary>
    /// Reading the reason out of a rejected write, ported from the Mac's
    /// `testServerErrorMessageShapes` and `testServerErrorMessageReadsPlainTextRejections`.
    /// </summary>
    [TestClass]
    public class ToDoServerErrorTests
    {
        [TestMethod]
        public void AStringMessageIsTheReason()
        {
            Assert.AreEqual("Project with id x not found",
                            ToDoServerError.Message(@"{""statusCode"":400,""message"":""Project with id x not found""}"));
        }

        [TestMethod]
        public void AnArrayOfStringsIsJoined()
        {
            Assert.AreEqual("title should not be empty; due_date must be a Date instance",
                            ToDoServerError.Message(
                                @"{""message"":[""title should not be empty"",""due_date must be a Date instance""]}"));
        }

        [TestMethod]
        public void ValidationErrorsAreReadFromTheirConstraints()
        {
            const string body = @"{""message"":[
                {""property"":""project_id"",""constraints"":{""isUuid"":""project_id must be a UUID""}},
                {""property"":""tags"",""constraints"":{""isArray"":""tags must be an array""}}]}";

            Assert.AreEqual("project_id must be a UUID; tags must be an array", ToDoServerError.Message(body));
        }

        [TestMethod]
        public void OneValidationErrorsConstraintsReadInAStableOrder()
        {
            // A NestJS ValidationError can carry several constraints, and the object's key order is not
            // something a client should inherit — the same rejection must read the same way every time.
            const string body = @"{""message"":[{""constraints"":{""isString"":""b"",""isDefined"":""a""}}]}";

            Assert.AreEqual("a; b", ToDoServerError.Message(body));
        }

        [TestMethod]
        public void ABodyWithNoUsableReasonGivesNone()
        {
            Assert.IsNull(ToDoServerError.Message(@"{""message"":""""}"));
            Assert.IsNull(ToDoServerError.Message(@"{""error"":""Bad Request""}"));
            Assert.IsNull(ToDoServerError.Message(@"{""message"":[{""property"":""tags""}]}"),
                          "a validation error with no constraints says nothing readable");
            Assert.IsNull(ToDoServerError.Message(null));
        }

        [TestMethod]
        public void PlainTextRejectionsAreShownToo()
        {
            // The API's auth layer answers text/plain, so a 401 used to leave the toast with no reason
            // and the debug log with no evidence (report 2026-09-16_15-14-36).
            Assert.AreEqual("Token missing or corrupted!", ToDoServerError.Message("Token missing or corrupted!"));
            Assert.AreEqual("Unauthorized", ToDoServerError.Message("  Unauthorized\n"));
        }

        [TestMethod]
        public void WhitespaceMarkupAndOverlongBodiesAreNotReasons()
        {
            Assert.IsNull(ToDoServerError.Message("   "), "whitespace is not a reason");
            Assert.IsNull(ToDoServerError.Message("<html><body>502 Bad Gateway</body></html>"),
                          "a proxy's HTML page is not a reason to show a user");
            Assert.IsNull(ToDoServerError.Message(new string('x', 500)),
                          "an overlong body is not a reason to show a user");
        }

        [TestMethod]
        public void AJsonValueThatIsNotAnObjectFallsBackToTheTextItself()
        {
            // A bare status line is valid JSON but not a NestJS error body.
            Assert.AreEqual("502", ToDoServerError.Message("502"));
        }
    }
}
