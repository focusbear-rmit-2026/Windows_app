using System.Linq;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    [TestClass]
    public class ToDoAttachmentsTests
    {
        [TestMethod]
        public void TheUploadIsSignedWithTheFilesRealType()
        {
            Assert.AreEqual("image/png", ToDoAttachments.ContentType("PNG"));
            Assert.AreEqual("image/jpeg", ToDoAttachments.ContentType(".jpg"));
            Assert.AreEqual("application/pdf", ToDoAttachments.ContentType("pdf"));
            Assert.AreEqual("application/octet-stream", ToDoAttachments.ContentType("exe"));
        }

        [TestMethod]
        public void TheChecksTurnACertainRejectionIntoAMessage()
        {
            Assert.AreEqual(ToDoAttachmentUploadCheck.Unavailable, ToDoAttachments.Evaluate(false, false, 0, 10));
            Assert.AreEqual(ToDoAttachmentUploadCheck.Unavailable, ToDoAttachments.Evaluate(true, true, 0, 10));
            Assert.AreEqual(ToDoAttachmentUploadCheck.LimitReached, ToDoAttachments.Evaluate(true, false, 20, 10));
            Assert.AreEqual(ToDoAttachmentUploadCheck.Unreadable, ToDoAttachments.Evaluate(true, false, 0, null));
            Assert.AreEqual(ToDoAttachmentUploadCheck.TooLarge, ToDoAttachments.Evaluate(true, false, 0, ToDoAttachment.MaximumBytes + 1));
            Assert.AreEqual(ToDoAttachmentUploadCheck.Ready, ToDoAttachments.Evaluate(true, false, 19, ToDoAttachment.MaximumBytes));
        }

        [TestMethod]
        public void AnEmptyFileIsRefusedBeforeItIsUploaded()
        {
            // CreateTaskAttachmentDto.file_size is @Min(1): an empty file went to storage and was then refused.
            Assert.AreEqual(ToDoAttachmentUploadCheck.Empty, ToDoAttachments.Evaluate(true, false, 0, 0));
            Assert.AreEqual(ToDoAttachmentUploadCheck.Ready, ToDoAttachments.Evaluate(true, false, 0, 1));
        }

        [TestMethod]
        public void AttachmentsTicketsAndSizesAreRead()
        {
            var list = ToDoAttachments.ReadAttachments(@"[{ ""id"": ""a1"", ""file_name"": ""shot.png"", ""file_key"": ""k"", ""content_type"": ""image/png"", ""file_size"": ""2048"", ""download_url"": ""https://r2/x"" }, { ""file_name"": ""no id"" }]");
            var file = list.Single();
            Assert.AreEqual(2048, file.FileSize);
            Assert.IsTrue(file.IsImage);
            var ticket = ToDoAttachments.ReadTicket(@"{ ""uploadUrl"": ""https://r2/put"", ""fileKey"": ""tasks/t1/abc.png"" }");
            Assert.AreEqual("tasks/t1/abc.png", ticket.FileKey);
            Assert.IsNull(ToDoAttachments.ReadTicket(@"{ ""uploadUrl"": """" }"));
            Assert.AreEqual("12 bytes", ToDoAttachments.SizeLabel(12));
            Assert.AreEqual("2 KB", ToDoAttachments.SizeLabel(2048));
            Assert.AreEqual("1.5 MB", ToDoAttachments.SizeLabel(1572864));
        }
    }
}
