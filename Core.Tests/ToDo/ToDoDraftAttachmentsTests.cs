using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FocusBear.Core.ToDo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.ToDo
{
    [TestClass]
    public class ToDoDraftAttachmentsTests
    {
        private static ToDoDraftAttachment File(string name) => new ToDoDraftAttachment(name, "image/png", new byte[] { 1 });

        [TestMethod]
        public async Task FailedTaskSaveNeverUploadsAndKeepsFiles()
        {
            var queue = new ToDoDraftAttachments();
            queue.Add(File("first.png"), 0);
            var uploads = 0;
            Assert.IsFalse(await queue.SaveAsync(() => Task.FromResult(false), file => { uploads++; return Task.FromResult(true); }));
            Assert.AreEqual(0, uploads);
            Assert.AreEqual(1, queue.Files.Count);
            Assert.IsFalse(queue.HasSavedTask);
        }

        [TestMethod]
        public async Task RetryKeepsStableTaskIdentityAndOnlyRetriesUnconfirmedFiles()
        {
            var task = ToDoDraft.NewTask("Task", new DateTime(2026, 10, 2));
            var queue = new ToDoDraftAttachments();
            var first = File("first.png");
            var second = File("second.png");
            queue.Add(first, 0);
            queue.Add(second, 0);
            var savedIds = new List<string>();
            Func<Task<bool>> save = () => { savedIds.Add(task.Id); return Task.FromResult(true); };
            Assert.IsFalse(await queue.SaveAsync(save, file => Task.FromResult(file == first)));
            Assert.IsTrue(queue.HasSavedTask);
            Assert.AreSame(second, queue.Files[0]);
            Assert.IsTrue(await queue.SaveAsync(save, file => { Assert.AreSame(second, file); return Task.FromResult(true); }));
            CollectionAssert.AreEqual(new[] { task.Id, task.Id }, savedIds);
            Assert.AreEqual(0, queue.Files.Count);
        }

        [TestMethod]
        public async Task SavingBlocksDuplicateSaveAndQueueMutation()
        {
            var queue = new ToDoDraftAttachments();
            var file = File("first.png");
            queue.Add(file, 0);
            var pending = new TaskCompletionSource<bool>();
            var save = queue.SaveAsync(() => pending.Task, item => Task.FromResult(true));
            Assert.IsFalse(await queue.SaveAsync(() => Task.FromResult(true), item => Task.FromResult(true)));
            queue.Remove(file);
            Assert.AreEqual(1, queue.Files.Count);
            Assert.AreEqual(ToDoAttachmentUploadCheck.Unavailable, queue.Add(File("second.png"), 0));
            pending.SetResult(true);
            Assert.IsTrue(await save);
            Assert.IsFalse(queue.IsSaving);
        }

        [TestMethod]
        public async Task UploadExceptionReleasesSaveLockAndRetainsCheckpoint()
        {
            var queue = new ToDoDraftAttachments();
            var file = File("first.png");
            queue.Add(file, 0);
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => queue.SaveAsync(
                () => Task.FromResult(true), item =>
                {
                    item.FileKey = "task/object";
                    item.RegistrationUnconfirmed = true;
                    throw new InvalidOperationException();
                }));
            Assert.IsFalse(queue.IsSaving);
            Assert.AreSame(file, queue.Files[0]);
            Assert.IsTrue(file.RegistrationUnconfirmed);
            Assert.AreEqual("task/object", file.FileKey);
        }

        [TestMethod]
        public async Task LostRegistrationResponseReconcilesWithoutAnotherUploadOrRegistration()
        {
            var file = File("first.png");
            var tickets = 0;
            var puts = 0;
            var posts = 0;
            IList<ToDoAttachment> lookup = null;
            Func<Task<ToDoAttachment>> upload = () => ToDoDraftAttachmentUpload.UploadAsync(file,
                () => { tickets++; return Task.FromResult(new ToDoAttachmentUploadTicket("url", "key")); },
                ticket => { puts++; return Task.FromResult(true); },
                () => { posts++; return Task.FromResult(new ToDoAttachmentRegistrationResult(null, false)); },
                () => Task.FromResult(lookup));
            Assert.IsNull(await upload());
            Assert.IsTrue(file.RegistrationUnconfirmed);
            Assert.IsNull(await upload()); // Failed GET must not mean an empty list.
            lookup = new List<ToDoAttachment>();
            Assert.IsNull(await upload()); // An absent row might still be committing.
            var attachment = new ToDoAttachment { FileKey = "key" };
            lookup.Add(attachment);
            Assert.AreSame(attachment, await upload());
            Assert.AreEqual(1, tickets);
            Assert.AreEqual(1, puts);
            Assert.AreEqual(1, posts);
        }

        [TestMethod]
        public async Task DefinitiveRegistrationRejectionAllowsRetry()
        {
            var file = File("first.png");
            var posts = 0;
            Func<Task<ToDoAttachment>> upload = () => ToDoDraftAttachmentUpload.UploadAsync(file,
                () => Task.FromResult(new ToDoAttachmentUploadTicket("url", "key")),
                ticket => Task.FromResult(true),
                () => { posts++; return Task.FromResult(new ToDoAttachmentRegistrationResult(null, true)); },
                () => throw new AssertFailedException("A definitive rejection needs no reconciliation."));
            Assert.IsNull(await upload());
            Assert.IsFalse(file.RegistrationUnconfirmed);
            Assert.IsNull(await upload());
            Assert.AreEqual(2, posts);
        }

        [TestMethod]
        public async Task ConfirmedFilesStillCountTowardTheLimitAfterPartialFailure()
        {
            var queue = new ToDoDraftAttachments();
            for (int index = 0; index < ToDoAttachment.MaximumPerTask; index++) queue.Add(File(index.ToString()), 0);
            var calls = 0;
            Assert.IsFalse(await queue.SaveAsync(() => Task.FromResult(true), file => Task.FromResult(++calls < ToDoAttachment.MaximumPerTask)));
            Assert.AreEqual(ToDoAttachment.MaximumPerTask - 1, queue.ConfirmedUploadCount);
            Assert.AreEqual(1, queue.Files.Count);
            Assert.AreEqual(ToDoAttachmentUploadCheck.LimitReached, queue.Add(File("extra"), 0));
        }

        [TestMethod]
        public void FilesAreValidatedBeforeStaging()
        {
            var queue = new ToDoDraftAttachments();
            Assert.AreEqual(ToDoAttachmentUploadCheck.LimitReached, queue.Add(File("first.png"), ToDoAttachment.MaximumPerTask));
            Assert.AreEqual(ToDoAttachmentUploadCheck.Empty, queue.Add(new ToDoDraftAttachment("empty", "image/png", new byte[0]), 0));
            Assert.AreEqual(0, queue.Files.Count);
        }
    }
}
