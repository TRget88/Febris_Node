// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Febris.EnumLibrary;
using Febris.SharedServices;
using Febris.UserNode.LogicLayer.Logic.LauncherLogic;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// T6, the silent-loss unit. First test coverage this pipeline has ever had.
    ///
    /// <para>
    /// <b>The merge key was never released.</b> It was added and tested under
    /// <c>SplitVideoFileSystemPath + baseFileName</c> but removed under the bare
    /// <c>baseFileName</c>, so <c>List.Remove</c> never matched. After the first SUCCESSFUL merge
    /// of a given name, every later merge of that name was skipped for the lifetime of the process,
    /// while <c>ProcessVideoFiles</c> returned a hardcoded <c>true</c> and the node answered
    /// <c>200 {"Success":true}</c>. Recordings were silently never produced. No attacker required:
    /// ordinary successful use poisoned the name, and because nothing binds an upload to a device
    /// it was poisoned for every device on the node.
    /// </para>
    ///
    /// <para>
    /// <b>The merge was neither atomic nor safe to retry.</b> The final served path was opened
    /// <c>FileMode.Create</c> before any chunk was read, truncating a good recording, and the
    /// Portal serves that directory. Source parts were deleted inside the copy loop before the
    /// output was closed or checked, and a per-chunk <c>IOException</c> was swallowed so the loop
    /// carried on deleting while omitting a chunk.
    /// </para>
    ///
    /// <para>
    /// These tests drive the REAL <see cref="MergeFileManager"/> singleton, because key symmetry
    /// against it is the defect. It is process-wide static, so every test here uses a unique
    /// recording name to stay isolated.
    /// </para>
    /// </summary>
    [Collection("VideoFileSystem")]
    public class VideoMergeLifecycleTests : IDisposable
    {
        private readonly string _root;
        private readonly string _splitDir;
        private readonly string _recordingsDir;
        private readonly string _originalSplit;
        private readonly string _originalRecordings;

        public VideoMergeLifecycleTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "FebrisT6_" + Guid.NewGuid().ToString("N"));
            _splitDir = Path.Combine(_root, "SplitVideos") + Path.DirectorySeparatorChar;
            _recordingsDir = Path.Combine(_root, "recordings") + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(_splitDir);
            Directory.CreateDirectory(_recordingsDir);

            // The production code concatenates these statics directly, so they are the seam.
            _originalSplit = StaticDetails.SplitVideoFileSystemPath;
            _originalRecordings = StaticDetails.RecordingsFileSystemPath;
            StaticDetails.SplitVideoFileSystemPath = _splitDir;
            StaticDetails.RecordingsFileSystemPath = _recordingsDir;
        }

        public void Dispose()
        {
            StaticDetails.SplitVideoFileSystemPath = _originalSplit;
            StaticDetails.RecordingsFileSystemPath = _originalRecordings;
            try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
            catch (IOException) { /* a leaked handle must not fail the run */ }
        }

        /// <summary>
        /// Real-file-backed handler. Only <c>IsFileInUse</c> and <c>AddFileToMerge</c> matter to the
        /// key defect, and both delegate to the real singleton exactly as production does.
        /// </summary>
        private static IVideoFileHandler BuildHandler()
        {
            Mock<IVideoFileHandler> h = new Mock<IVideoFileHandler>();

            h.Setup(x => x.CreateFileDirectory(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string p, string _) => { Directory.CreateDirectory(p); return Task.CompletedTask; });

            h.Setup(x => x.CreationFileStream(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string p, string n) => Task.FromResult(File.Create(p + n)));

            h.Setup(x => x.FileExists(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string p, string n) => Task.FromResult(File.Exists(p + n)));

            h.Setup(x => x.FileDelete(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string p, string n) => { if (File.Exists(p + n)) File.Delete(p + n); return Task.CompletedTask; });

            h.Setup(x => x.DeleteSplitFiles(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string p, string n) => { if (File.Exists(p + n)) File.Delete(p + n); return Task.CompletedTask; });

            h.Setup(x => x.GetDirectoryFileList(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string p, string pattern) => Task.FromResult(
                    Directory.Exists(p)
                        ? Directory.GetFiles(p, pattern).Select(Path.GetFileName).ToArray()
                        : new string[0]));

            h.Setup(x => x.MergeFileStream(It.IsAny<string>(), It.IsAny<FileMode>()))
                .Returns((string f, FileMode m) => Task.FromResult(new FileStream(f, m)));

            // The two that must be real: production keys these against MergeFileManager, and
            // VideoUploadLogic releases on the same singleton directly.
            h.Setup(x => x.IsFileInUse(It.IsAny<string>()))
                .Returns((string k) => Task.FromResult(MergeFileManager.Instance.InUse(k)));
            h.Setup(x => x.AddFileToMerge(It.IsAny<string>()))
                .Returns((string k) => { MergeFileManager.Instance.AddFile(k); return Task.CompletedTask; });

            return h.Object;
        }

        private static VideoUploadLogic BuildLogic(IVideoFileHandler handler)
        {
            DefaultHttpContext http = new DefaultHttpContext();
            Mock<IHttpContextAccessor> accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(http);

            // These tests are about the MERGE, so the upload gate is stubbed open. It is stubbed
            // open EXPLICITLY rather than incidentally: the gate has its own suite
            // (RecordingUploadBindingTests) which drives the real RecordingLogic against exact-match
            // lookups. Splitting it this way is deliberate, because a permissive stub inside the
            // gate's OWN tests is precisely how the earlier entitlement defect passed review.
            Mock<Febris.UserNode.LogicLayer.Logic.DataLogic.IRecordingLogic> recordings =
                new Mock<Febris.UserNode.LogicLayer.Logic.DataLogic.IRecordingLogic>();
            recordings.Setup(r => r.MayAcceptPart(It.IsAny<string>(), It.IsAny<Guid>()))
                .ReturnsAsync(true);

            // Null config on purpose: the quota limits then take their compiled defaults, which is
            // the behaviour an unconfigured host gets. These tests are about the merge, and the
            // limits have their own suite.
            return new VideoUploadLogic(accessor.Object, handler, recordings.Object, null);
        }

        /// <summary>Builds a complete part set for one recording, in the naming convention the merge parses.</summary>
        private static IFormFileCollection PartsFor(string recordingName, params string[] contents)
        {
            FormFileCollection files = new FormFileCollection();
            for (int i = 0; i < contents.Length; i++)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(contents[i]);
                MemoryStream ms = new MemoryStream(bytes);
                string partName = recordingName + ".part_" + i + "." + contents.Length;
                files.Add(new FormFile(ms, 0, bytes.Length, "file", partName));
            }
            return files;
        }

        // ------------------------------------------------------------------

        [Fact]
        public async Task TheMergeKeyIsReleasedAfterASuccessfulMerge()
        {
            // THE defect, at its narrowest. Add and test used the full split path; release used the
            // bare filename, so the entry stayed forever.
            string name = "release-" + Guid.NewGuid().ToString("N") + ".mp4";

            bool ok = await BuildLogic(BuildHandler()).ProcessVideoFiles(PartsFor(name, "alpha", "beta"));
            ok.Should().BeTrue();

            MergeFileManager.Instance.InUse(StaticDetails.SplitVideoFileSystemPath + name)
                .Should().BeFalse("the key must be released under the SAME string it was added with");
            MergeFileManager.Instance.InUse(name)
                .Should().BeFalse("and it must not linger under the bare name either");
        }

        [Fact]
        public async Task TheSameRecordingNameCanBeMergedTwice()
        {
            // The consequence a user would actually hit. Before the fix the second upload was
            // silently skipped and still answered success, so the recording never appeared.
            string name = "twice-" + Guid.NewGuid().ToString("N") + ".mp4";

            (await BuildLogic(BuildHandler()).ProcessVideoFiles(PartsFor(name, "first", "run")))
                .Should().BeTrue();
            File.Exists(_recordingsDir + name).Should().BeTrue("the first merge must produce a recording");
            File.Delete(_recordingsDir + name);

            (await BuildLogic(BuildHandler()).ProcessVideoFiles(PartsFor(name, "second", "run")))
                .Should().BeTrue();
            File.Exists(_recordingsDir + name).Should().BeTrue(
                "a name that merged once must be mergeable again -- this is the silent data loss");
            File.ReadAllText(_recordingsDir + name).Should().Be("secondrun");
        }

        [Fact]
        public async Task AMergedRecordingHasItsPartsInOrderAndNoTempFileLeftBehind()
        {
            string name = "order-" + Guid.NewGuid().ToString("N") + ".mp4";

            await BuildLogic(BuildHandler()).ProcessVideoFiles(PartsFor(name, "AAA", "BBB", "CCC"));

            File.ReadAllText(_recordingsDir + name).Should().Be("AAABBBCCC", "chunks merge in part order");
            Directory.GetFiles(_recordingsDir, "*.merging").Should().BeEmpty(
                "the temp file must be renamed into place, never left behind");
            Directory.GetFiles(_splitDir).Should().BeEmpty("parts are removed once the recording is safe");
        }

        [Fact]
        public async Task AFailedMergeReportsFailureAndLeavesThePartsForRetry()
        {
            // Parts used to be deleted inside the copy loop, so a mid-merge failure destroyed the
            // only means of retrying AND was reported as success.
            string name = "fail-" + Guid.NewGuid().ToString("N") + ".mp4";

            Mock<IVideoFileHandler> h = Mock.Get(BuildHandler());
            // Fail when the merge tries to read a part back, after the parts are on disk.
            h.Setup(x => x.MergeFileStream(It.Is<string>(f => f.Contains(".part_")), It.IsAny<FileMode>()))
                .ThrowsAsync(new IOException("chunk unreadable"));

            bool ok = await BuildLogic(h.Object).ProcessVideoFiles(PartsFor(name, "alpha", "beta"));

            ok.Should().BeFalse("a merge that produced nothing must not be reported as success");
            Directory.GetFiles(_splitDir, name + ".part_*").Should().NotBeEmpty(
                "the parts must survive so the upload can be retried");
            File.Exists(_recordingsDir + name).Should().BeFalse(
                "no partial recording may be left at the path the Portal serves");
            Directory.GetFiles(_recordingsDir, "*.merging").Should().BeEmpty("the temp file is cleaned up");

            MergeFileManager.Instance.InUse(StaticDetails.SplitVideoFileSystemPath + name)
                .Should().BeFalse("the key must be released on the failure path too");
        }

        [Fact]
        public async Task AnIncompletePartSetIsNotAFailure()
        {
            // Every part except the last lands here. It must stay a success, or clients would treat
            // normal progress as an error.
            string name = "partial-" + Guid.NewGuid().ToString("N") + ".mp4";

            FormFileCollection oneOfThree = new FormFileCollection();
            byte[] bytes = Encoding.UTF8.GetBytes("only");
            oneOfThree.Add(new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name + ".part_0.3"));

            bool ok = await BuildLogic(BuildHandler()).ProcessVideoFiles(oneOfThree);

            ok.Should().BeTrue("an incomplete set is normal progress, not a failure");
            File.Exists(_recordingsDir + name).Should().BeFalse("nothing is assembled yet");
        }
    }
}
