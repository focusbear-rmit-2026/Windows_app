using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FocusBear.Core.ToDo
{
    public sealed class ToDoDraftAttachment
    {
        public ToDoDraftAttachment(string fileName, string contentType, byte[] bytes)
        {
            FileName = fileName;
            ContentType = contentType;
            Bytes = bytes;
        }

        public string FileName { get; }
        public string ContentType { get; }
        public byte[] Bytes { get; }
        public string FileKey { get; set; }
        public bool RegistrationUnconfirmed { get; set; }
    }

    public sealed class ToDoAttachmentRegistrationResult
    {
        public ToDoAttachmentRegistrationResult(ToDoAttachment attachment, bool isDefinitiveFailure)
        {
            Attachment = attachment;
            IsDefinitiveFailure = isDefinitiveFailure;
        }
        public ToDoAttachment Attachment { get; }
        public bool IsDefinitiveFailure { get; }
    }

    public static class ToDoDraftAttachmentUpload
    {
        public static async Task<ToDoAttachment> UploadAsync(ToDoDraftAttachment file,
            Func<Task<ToDoAttachmentUploadTicket>> getTicket,
            Func<ToDoAttachmentUploadTicket, Task<bool>> uploadBytes,
            Func<Task<ToDoAttachmentRegistrationResult>> register,
            Func<Task<IList<ToDoAttachment>>> reconcile)
        {
            if (file.RegistrationUnconfirmed)
            {
                var existing = await reconcile();
                return existing?.FirstOrDefault(attachment => attachment.FileKey == file.FileKey);
            }
            var ticket = await getTicket();
            if (ticket == null || !await uploadBytes(ticket)) return null;
            file.FileKey = ticket.FileKey;
            // Set before the POST: an exception or lost response cannot permit another object upload.
            file.RegistrationUnconfirmed = true;
            var result = await register();
            if (result.Attachment != null || result.IsDefinitiveFailure) file.RegistrationUnconfirmed = false;
            return result.Attachment;
        }
    }

    /// <summary>One editor's queue. A failed task save never sends files; confirmed uploads leave the queue.</summary>
    public sealed class ToDoDraftAttachments
    {
        private readonly List<ToDoDraftAttachment> _files = new List<ToDoDraftAttachment>();
        public IReadOnlyList<ToDoDraftAttachment> Files => _files;
        public bool IsSaving { get; private set; }
        public bool HasSavedTask { get; private set; }
        public int ConfirmedUploadCount { get; private set; }

        public ToDoAttachmentUploadCheck Add(ToDoDraftAttachment file, int existingCount)
        {
            var check = ToDoAttachments.Evaluate(true, IsSaving, existingCount + ConfirmedUploadCount + _files.Count, file?.Bytes?.LongLength);
            if (check == ToDoAttachmentUploadCheck.Ready) _files.Add(file);
            return check;
        }

        public void Remove(ToDoDraftAttachment file)
        {
            if (!IsSaving) _files.Remove(file);
        }

        public async Task<bool> SaveAsync(Func<Task<bool>> saveTask, Func<ToDoDraftAttachment, Task<bool>> upload)
        {
            if (IsSaving) return false;
            IsSaving = true;
            try
            {
                // Re-save edits using the editor's stable task UUID, including after a lost response.
                if (!await saveTask()) return false;
                HasSavedTask = true;
                foreach (var file in _files.ToArray())
                {
                    if (!await upload(file)) return false;
                    _files.Remove(file);
                    ConfirmedUploadCount++;
                }
                return true;
            }
            finally { IsSaving = false; }
        }
    }
}
