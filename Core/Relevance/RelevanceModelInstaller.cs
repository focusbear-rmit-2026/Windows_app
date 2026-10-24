using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace FocusBear.Core.Relevance
{
    /// <summary>
    /// Puts the relevance model files in a folder and proves they are the expected bytes. Each missing or wrong file
    /// is downloaded to a temporary name beside it, checked for size and SHA-256, and only then moved into place, so
    /// an interrupted or tampered download never leaves a file the classifier would load.
    /// </summary>
    public sealed class RelevanceModelInstaller
    {
        private const string PartialSuffix = ".partial";

        private readonly string _directory;
        private readonly IReadOnlyList<RelevanceModelFile> _files;
        private readonly Func<Uri, Stream, CancellationToken, Task> _download;
        private readonly Func<RelevanceModelFile, Uri> _urlFor;

        /// <param name="download">Copies the body of the URL into the stream; throws when the server doesn't answer 2xx.</param>
        public RelevanceModelInstaller(string directory, IReadOnlyList<RelevanceModelFile> files,
            Func<Uri, Stream, CancellationToken, Task> download, Func<RelevanceModelFile, Uri> urlFor = null)
        {
            _directory = directory ?? throw new ArgumentNullException(nameof(directory));
            _files = files ?? throw new ArgumentNullException(nameof(files));
            _download = download ?? throw new ArgumentNullException(nameof(download));
            _urlFor = urlFor ?? RelevanceModelFiles.DownloadUrl;
        }

        public string Directory => _directory;

        /// <summary>True when every file is present with the expected size and hash.</summary>
        public bool IsInstalled()
        {
            foreach (RelevanceModelFile file in _files)
            {
                if (!Matches(PathOf(file), file)) return false;
            }
            return true;
        }

        /// <summary>
        /// Downloads whatever is missing or wrong. Returns true only when every file then verifies; on false nothing
        /// unverified has been put in place, and the next call tries again.
        /// </summary>
        public async Task<bool> EnsureInstalledAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            System.IO.Directory.CreateDirectory(_directory);
            foreach (RelevanceModelFile file in _files)
            {
                if (Matches(PathOf(file), file)) continue;
                if (!await DownloadVerifiedAsync(file, cancellationToken).ConfigureAwait(false)) return false;
            }
            return true;
        }

        private async Task<bool> DownloadVerifiedAsync(RelevanceModelFile file, CancellationToken cancellationToken)
        {
            string destination = PathOf(file);
            string partial = destination + "." + Guid.NewGuid().ToString("N") + PartialSuffix;
            try
            {
                using (var stream = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    await _download(_urlFor(file), stream, cancellationToken).ConfigureAwait(false);
                }
                if (!Matches(partial, file)) return false;
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(destination))
                    File.Replace(partial, destination, null);
                else
                    File.Move(partial, destination);
                return true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                return false;
            }
            finally
            {
                TryDelete(partial);
            }
        }

        private string PathOf(RelevanceModelFile file) => Path.Combine(_directory, file.Name);

        public static bool Matches(string path, RelevanceModelFile file)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length != file.Bytes) return false;
                return string.Equals(Sha256Of(path), file.Sha256, StringComparison.OrdinalIgnoreCase);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        public static string Sha256Of(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
