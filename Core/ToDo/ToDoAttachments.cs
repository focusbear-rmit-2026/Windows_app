using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>One file on a task. The bytes live in R2; <see cref="DownloadUrl"/> is short-lived and the server's.</summary>
    public sealed class ToDoAttachment
    {
        /// <summary>20 MB — the server's MAX_ATTACHMENT_SIZE_BYTES.</summary>
        public const int MaximumBytes = 20 * 1024 * 1024;

        /// <summary>The server's MAX_TASK_ATTACHMENTS.</summary>
        public const int MaximumPerTask = 20;

        public string Id { get; set; }
        public string TaskId { get; set; }
        public string FileName { get; set; }
        public string FileKey { get; set; }
        public string ContentType { get; set; }
        public long FileSize { get; set; }
        public string DownloadUrl { get; set; }
        public string CreatedAt { get; set; }

        public bool IsImage => (ContentType ?? "").StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary><c>POST …/attachments/generate-upload-url</c>: where to PUT the bytes, and the key to record them under.</summary>
    public sealed class ToDoAttachmentUploadTicket
    {
        public ToDoAttachmentUploadTicket(string uploadUrl, string fileKey)
        {
            UploadUrl = uploadUrl;
            FileKey = fileKey;
        }

        public string UploadUrl { get; }
        public string FileKey { get; }
    }

    public enum ToDoAttachmentUploadCheck
    {
        Ready,

        /// <summary>No task to attach to yet, or an upload already running — the UI already shows it.</summary>
        Unavailable,
        LimitReached,
        Unreadable,
        TooLarge,

        /// <summary>
        /// A zero-byte file. The record's <c>file_size</c> must be at least 1, so the upload would put the
        /// bytes in storage and then be refused — a generic "could not upload" after the work was done.
        /// </summary>
        Empty,
    }

    /// <summary>
    /// Task attachments (the Mac's <c>ToDoAttachments.swift</c>): three steps — a presigned URL, a PUT of the
    /// bytes straight to R2 with no Authorization header (the signature is the auth), then the record. The
    /// server verifies the object and takes the real size from storage; the checks here only turn a
    /// certain rejection into a clear message.
    /// </summary>
    public static class ToDoAttachments
    {
        public const string FallbackContentType = "application/octet-stream";

        private static readonly IDictionary<string, string> ContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "png", "image/png" }, { "jpg", "image/jpeg" }, { "jpeg", "image/jpeg" }, { "gif", "image/gif" },
            { "heic", "image/heic" }, { "webp", "image/webp" }, { "tiff", "image/tiff" }, { "bmp", "image/bmp" },
            { "pdf", "application/pdf" }, { "txt", "text/plain" },
        };

        /// <summary>What the picker offers: the images and documents the preview can render.</summary>
        public static readonly IList<string> PickableExtensions = new[] { "png", "jpg", "jpeg", "gif", "webp", "bmp", "pdf", "txt" };

        /// <summary>The type the upload is signed with — a wrong one is baked into the stored object.</summary>
        public static string ContentType(string extension) =>
            ContentTypes.TryGetValue((extension ?? "").TrimStart('.'), out var type) ? type : FallbackContentType;

        public static ToDoAttachmentUploadCheck Evaluate(bool hasTask, bool isUploading, int existingCount, long? byteCount)
        {
            if (!hasTask || isUploading) return ToDoAttachmentUploadCheck.Unavailable;
            if (existingCount >= ToDoAttachment.MaximumPerTask) return ToDoAttachmentUploadCheck.LimitReached;
            if (!byteCount.HasValue) return ToDoAttachmentUploadCheck.Unreadable;
            if (byteCount.Value <= 0) return ToDoAttachmentUploadCheck.Empty;
            if (byteCount.Value > ToDoAttachment.MaximumBytes) return ToDoAttachmentUploadCheck.TooLarge;
            return ToDoAttachmentUploadCheck.Ready;
        }

        /// <summary>"1.2 MB" / "340 KB" / "12 bytes".</summary>
        public static string SizeLabel(long bytes)
        {
            bytes = Math.Max(0, bytes);
            if (bytes < 1024) return bytes.ToString(CultureInfo.InvariantCulture) + " bytes";
            if (bytes < 1024 * 1024) return Math.Round(bytes / 1024.0).ToString(CultureInfo.InvariantCulture) + " KB";
            return (bytes / (1024.0 * 1024.0)).ToString("0.#", CultureInfo.InvariantCulture) + " MB";
        }

        public static IList<ToDoAttachment> ReadAttachments(string json)
        {
            var token = ToDoJson.Parse(json);
            var rows = token as JArray ?? (token as JObject)?["data"] as JArray;
            return rows?.OfType<JObject>().Select(Read).Where(a => a != null).ToList();
        }

        public static ToDoAttachment ReadAttachment(string json) => Read(ToDoJson.Parse(json) as JObject);

        public static ToDoAttachmentUploadTicket ReadTicket(string json)
        {
            var row = ToDoJson.Parse(json) as JObject;
            string url = Text(row?["uploadUrl"]) ?? Text(row?["upload_url"]);
            string key = Text(row?["fileKey"]) ?? Text(row?["file_key"]);
            return string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key) ? null : new ToDoAttachmentUploadTicket(url, key);
        }

        private static ToDoAttachment Read(JObject row)
        {
            string id = Text(row?["id"]);
            if (string.IsNullOrEmpty(id)) return null;
            long size = 0;
            var sizeToken = row["file_size"];
            if (sizeToken != null && (sizeToken.Type == JTokenType.Integer || sizeToken.Type == JTokenType.Float)) size = (long)(double)sizeToken;
            else if (sizeToken != null && sizeToken.Type == JTokenType.String) long.TryParse((string)sizeToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out size);
            return new ToDoAttachment
            {
                Id = id,
                TaskId = Text(row["task_id"]),
                FileName = Text(row["file_name"]) ?? "",
                FileKey = Text(row["file_key"]),
                ContentType = Text(row["content_type"]) ?? FallbackContentType,
                FileSize = size,
                DownloadUrl = Text(row["download_url"]),
                CreatedAt = Text(row["created_at"]),
            };
        }

        private static string Text(JToken token) => token != null && token.Type == JTokenType.String ? (string)token : null;
    }
}
