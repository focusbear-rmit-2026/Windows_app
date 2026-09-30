using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FocusBear.Core.Reports
{
    /// <summary>
    /// What to write to the log about a request or response body. The log is attached to every problem report,
    /// and it held whole bodies: the settings document, the buddy list (emails), the onboarding webview's
    /// access token, a password-reset email, focus-session intentions. These describe a body - its size, a
    /// short hash to tell two apart, its keys, which keys changed - without its contents.
    /// </summary>
    public static class LogSafe
    {
        private const int MaxChangedKeys = 20;
        private const int MaxDepth = 3;

        /// <summary>"1234 chars #1a2b3c4d", or "empty".</summary>
        public static string Describe(string body)
        {
            if (string.IsNullOrEmpty(body))
                return "empty";
            return body.Length + " chars #" + ShortHash(body);
        }

        /// <summary>The top-level shape: "object: a, b, c", "array of N", or "unreadable, N chars".</summary>
        public static string Keys(string json)
        {
            JToken token = TryParse(json);
            if (token is JObject obj)
                return "object: " + string.Join(", ", obj.Properties().Select(p => p.Name));
            if (token is JArray arr)
                return "array of " + arr.Count;
            return "unreadable, " + (json?.Length ?? 0) + " chars";
        }

        /// <summary>
        /// The dotted paths (up to three levels deep) that differ between two JSON documents, sorted, at most
        /// twenty. "first load" when there was no earlier document, "none" when nothing changed.
        /// </summary>
        public static string ChangedKeys(string before, string after)
        {
            if (string.IsNullOrEmpty(before))
                return "first load";
            JToken a = TryParse(before), b = TryParse(after);
            if (a == null || b == null)
                return "unreadable";

            var changed = new SortedSet<string>(StringComparer.Ordinal);
            Diff(a, b, "", 1, changed);
            if (changed.Count == 0)
                return "none";
            string shown = string.Join(", ", changed.Take(MaxChangedKeys));
            return changed.Count > MaxChangedKeys ? shown + " (+" + (changed.Count - MaxChangedKeys) + " more)" : shown;
        }

        private static void Diff(JToken a, JToken b, string path, int depth, ISet<string> changed)
        {
            if (JToken.DeepEquals(a, b))
                return;
            if (a is JObject oa && b is JObject ob && depth <= MaxDepth)
            {
                foreach (string name in oa.Properties().Select(p => p.Name).Union(ob.Properties().Select(p => p.Name)))
                {
                    string child = path.Length == 0 ? name : path + "." + name;
                    JToken va = oa[name], vb = ob[name];
                    if (va == null || vb == null)
                        changed.Add(child);
                    else
                        Diff(va, vb, child, depth + 1, changed);
                }
                return;
            }
            changed.Add(path.Length == 0 ? "(document)" : path);
        }

        private static JToken TryParse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;
            try
            {
                return JToken.Parse(json);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string ShortHash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                var sb = new StringBuilder(8);
                for (int i = 0; i < 4; i++)
                    sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
