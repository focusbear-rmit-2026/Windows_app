using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FocusBear.Core.Relevance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.Core.Tests.Relevance
{
    [TestClass]
    public class RelevanceModelInstallerTests
    {
        private static readonly byte[] Model = Encoding.UTF8.GetBytes("model bytes");
        private static readonly byte[] Vocabulary = Encoding.UTF8.GetBytes("vocab bytes");
        private string _dir;

        [TestInitialize]
        public void Make() => _dir = Path.Combine(Path.GetTempPath(), "fb-relevance-" + Guid.NewGuid().ToString("N"));

        [TestCleanup]
        public void Remove()
        {
            try { Directory.Delete(_dir, true); } catch (IOException) { }
        }

        [TestMethod]
        public async Task DownloadsEveryMissingFileAndVerifiesIt()
        {
            var served = new Dictionary<string, byte[]> { ["a.onnx"] = Model, ["vocab.txt"] = Vocabulary };
            var installer = Installer(served, out List<string> requested);

            Assert.IsFalse(installer.IsInstalled());
            Assert.IsTrue(await installer.EnsureInstalledAsync(CancellationToken.None));
            Assert.IsTrue(installer.IsInstalled());
            CollectionAssert.AreEqual(new[] { "a.onnx", "vocab.txt" }, requested);
            CollectionAssert.AreEqual(Model, File.ReadAllBytes(Path.Combine(_dir, "a.onnx")));

            requested.Clear();
            Assert.IsTrue(await installer.EnsureInstalledAsync(CancellationToken.None));
            Assert.AreEqual(0, requested.Count, "verified files aren't fetched again");
        }

        [TestMethod]
        public async Task AFileWithTheWrongHashIsNeverPutInPlace()
        {
            var served = new Dictionary<string, byte[]> { ["a.onnx"] = Encoding.UTF8.GetBytes("tampered!!!"), ["vocab.txt"] = Vocabulary };
            var installer = Installer(served, out _);

            Assert.IsFalse(await installer.EnsureInstalledAsync(CancellationToken.None));
            Assert.IsFalse(File.Exists(Path.Combine(_dir, "a.onnx")));
            Assert.AreEqual(0, Directory.GetFiles(_dir).Length, "no partial download is left behind");
            Assert.IsFalse(installer.IsInstalled());
        }

        [TestMethod]
        public async Task AFailedDownloadLeavesNothingAndCanBeRetried()
        {
            var served = new Dictionary<string, byte[]> { ["vocab.txt"] = Vocabulary };
            var installer = Installer(served, out _);

            Assert.IsFalse(await installer.EnsureInstalledAsync(CancellationToken.None));
            Assert.AreEqual(0, Directory.GetFiles(_dir).Length);

            served["a.onnx"] = Model;
            Assert.IsTrue(await installer.EnsureInstalledAsync(CancellationToken.None));
        }

        [TestMethod]
        public async Task ACorruptedInstalledFileIsReplaced()
        {
            var served = new Dictionary<string, byte[]> { ["a.onnx"] = Model, ["vocab.txt"] = Vocabulary };
            var installer = Installer(served, out _);
            Assert.IsTrue(await installer.EnsureInstalledAsync(CancellationToken.None));

            File.WriteAllBytes(Path.Combine(_dir, "a.onnx"), Encoding.UTF8.GetBytes("model bytez"));
            Assert.IsFalse(installer.IsInstalled(), "same size, different hash");
            Assert.IsTrue(await installer.EnsureInstalledAsync(CancellationToken.None));
            CollectionAssert.AreEqual(Model, File.ReadAllBytes(Path.Combine(_dir, "a.onnx")));
        }

        [TestMethod]
        public void TheCompiledInFilesAreTheSharedV21Model()
        {
            Assert.AreEqual(7, RelevanceModelFiles.All.Count);
            Assert.IsTrue(RelevanceModelFiles.All.All(file => file.Sha256.Length == 64 && file.Bytes > 0));
            Assert.AreEqual("https://pub-a0f25f0d306d4d63bbfc52aa624ae389.r2.dev/v2.1/minilm.onnx",
                RelevanceModelFiles.DownloadUrl(RelevanceModelFiles.All[0]).ToString());
        }

        private RelevanceModelInstaller Installer(Dictionary<string, byte[]> served, out List<string> requested)
        {
            var files = new[] { Expected("a.onnx", Model), Expected("vocab.txt", Vocabulary) };
            var calls = new List<string>();
            requested = calls;
            return new RelevanceModelInstaller(_dir, files, (url, stream, token) =>
            {
                string name = url.Segments.Last();
                calls.Add(name);
                if (!served.TryGetValue(name, out byte[] body)) throw new IOException("404 " + name);
                return stream.WriteAsync(body, 0, body.Length, token);
            }, file => new Uri("https://example.test/" + file.Name));
        }

        private static RelevanceModelFile Expected(string name, byte[] body)
        {
            using (SHA256 sha = SHA256.Create())
            {
                string hash = BitConverter.ToString(sha.ComputeHash(body)).Replace("-", "").ToLowerInvariant();
                return new RelevanceModelFile(name, hash, body.Length);
            }
        }
    }
}
