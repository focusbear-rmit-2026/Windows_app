using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace FocusBear.Core.Reports
{
    /// <summary>
    /// The zip a problem report sends: every file in the logs folder under <c>Logs/</c>, the screenshots under
    /// <c>Screenshots/</c>.
    ///
    /// It used to be built with <c>CreateEntryFromFile</c>, which opens each file allowing readers only: the log
    /// being written at that moment either could not be opened (the report failed with "problem uploading the
    /// logs") or shut the app's own log writes out while the zip was built. And one screenshot deleted since it
    /// was picked failed the whole report. Files are now read sharing write access, and a file that cannot be
    /// read is left out and named in the result instead.
    /// </summary>
    public static class LogBundle
    {
        /// <summary>Writes the bundle to <paramref name="zipPath"/>; returns the paths that could not be read.</summary>
        public static IList<string> Write(string zipPath, IEnumerable<string> logFiles, IEnumerable<string> screenshots)
        {
            var skipped = new List<string>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var zipStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                foreach (string file in logFiles ?? Enumerable.Empty<string>()) Add(archive, file, "Logs/", names, skipped);
                foreach (string file in screenshots ?? Enumerable.Empty<string>()) Add(archive, file, "Screenshots/", names, skipped);
            }
            return skipped;
        }

        private static void Add(ZipArchive archive, string path, string folder, ISet<string> names, IList<string> skipped)
        {
            if (string.IsNullOrEmpty(path)) return;
            FileStream source;
            try { source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                skipped.Add(path);
                return;
            }
            using (source)
            {
                // Two screenshots called "Screenshot 1.png" from different folders are both kept.
                string name = UniqueName(folder, Path.GetFileName(path), names);
                var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
                using (var target = entry.Open()) source.CopyTo(target);
            }
        }

        private static string UniqueName(string folder, string fileName, ISet<string> names)
        {
            string candidate = folder + fileName;
            string stem = Path.GetFileNameWithoutExtension(fileName), extension = Path.GetExtension(fileName);
            for (int n = 2; !names.Add(candidate); n++) candidate = folder + stem + " (" + n + ")" + extension;
            return candidate;
        }
    }
}
