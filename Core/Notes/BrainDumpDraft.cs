using System;
using System.IO;
using System.Text;

namespace FocusBear.Core.Notes
{
    /// <summary>
    /// The brain dump text not yet confirmed saved, kept on disk. A save that failed (offline) left the text only
    /// in the editor - retried every few seconds for as long as the app ran, even with the screen closed - and a
    /// quit lost it. It is written before each save, removed when one lands, and restored into the editor when the
    /// server's copy doesn't have it. Never throws.
    /// </summary>
    public sealed class BrainDumpDraft
    {
        private readonly string _path;

        public BrainDumpDraft(string path)
        {
            _path = path;
        }

        public void Write(string html)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                string temp = _path + ".tmp";
                File.WriteAllText(temp, html ?? "", new UTF8Encoding(false));
                if (File.Exists(_path)) File.Replace(temp, _path, null); else File.Move(temp, _path);
            }
            catch (Exception) { }
        }

        /// <summary>The kept text, or null when there is none.</summary>
        public string Read()
        {
            try { return File.Exists(_path) ? File.ReadAllText(_path, Encoding.UTF8) : null; }
            catch (Exception) { return null; }
        }

        public void Clear()
        {
            try { if (File.Exists(_path)) File.Delete(_path); }
            catch (Exception) { }
        }

        /// <summary>Whether a kept draft should replace the server's body in the editor.</summary>
        public static bool ShouldRestore(string draft, string serverBody) =>
            draft != null && !string.Equals(draft, serverBody ?? "", StringComparison.Ordinal);
    }
}
