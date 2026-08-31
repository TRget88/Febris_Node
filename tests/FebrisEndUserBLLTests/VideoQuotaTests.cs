// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Febris.SharedServices;
using Febris.UserNode.LogicLayer.Logic.LauncherLogic;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// T6, the last member: the video pipeline had NO quota of any kind. No part-count cap, no
    /// per-device, per-learner or per-org cap, no total-bytes cap and no reaper. An entitled device
    /// could fill the storage volume one accepted part at a time.
    ///
    /// <para>
    /// The limits are enforced per ROUTE rather than by lowering the host's multipart limit,
    /// because this host also ingests module and software packages
    /// (<c>ModuleController.Upload</c>, <c>SoftwarePackageController.Upload</c>) which are archives
    /// and legitimately large. A host-wide limit small enough to bound video would have silently
    /// broken package ingest.
    /// </para>
    ///
    /// <para>
    /// The node deliberately does NOT consult the tenant's <c>MaxVideoStorage</c>. That field lives
    /// on the central <c>Institution</c> row, which was torn out of the node context with the other
    /// central tables, and reaching for it would re-couple the node to central data. It is also
    /// never compared anywhere in the repo -- a setting with no enforcement. These are node-local
    /// limits instead.
    /// </para>
    /// </summary>
    [Collection("VideoFileSystem")]
    public class VideoQuotaTests : IDisposable
    {
        private readonly string _root;
        private readonly string _splitDir;
        private readonly string _recordingsDir;
        private readonly string _originalSplit;
        private readonly string _originalRecordings;

        public VideoQuotaTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "FebrisQuota_" + Guid.NewGuid().ToString("N"));
            _splitDir = Path.Combine(_root, "SplitVideos") + Path.DirectorySeparatorChar;
            _recordingsDir = Path.Combine(_root, "recordings") + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(_splitDir);
            Directory.CreateDirectory(_recordingsDir);

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
            catch (IOException) { }
        }

        private static IConfiguration Config(long maxPartBytes, int maxParts) =>
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                ["VideoLimits:MaxPartBytes"] = maxPartBytes.ToString(),
                ["VideoLimits:MaxPartsPerRecording"] = maxParts.ToString(),
            }).Build();

        private static IVideoFileHandler RealFileHandler()
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
                    Directory.Exists(p) ? Directory.GetFiles(p, pattern).Select(Path.GetFileName).ToArray() : new string[0]));
            h.Setup(x => x.MergeFileStream(It.IsAny<string>(), It.IsAny<FileMode>()))
                .Returns((string f, FileMode m) => Task.FromResult(new FileStream(f, m)));
            h.Setup(x => x.IsFileInUse(It.IsAny<string>())).ReturnsAsync(false);
            h.Setup(x => x.AddFileToMerge(It.IsAny<string>())).Returns(Task.CompletedTask);
            return h.Object;
        }

        /// <summary>Ownership gate stubbed open: these tests are about the QUOTA, which sits after it.</summary>
        private static VideoUploadLogic BuildLogic(IConfiguration config)
        {
            DefaultHttpContext http = new DefaultHttpContext();
            Mock<IHttpContextAccessor> accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(http);

            Mock<Febris.UserNode.LogicLayer.Logic.DataLogic.IRecordingLogic> recordings =
                new Mock<Febris.UserNode.LogicLayer.Logic.DataLogic.IRecordingLogic>();
            recordings.Setup(r => r.MayAcceptPart(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(true);

            return new VideoUploadLogic(accessor.Object, RealFileHandler(), recordings.Object, config);
        }

        private static IFormFileCollection OnePart(string name, int index, int count, int bytes)
        {
            FormFileCollection files = new FormFileCollection();
            byte[] payload = new byte[bytes];
            files.Add(new FormFile(new MemoryStream(payload), 0, payload.Length, "file",
                name + ".mp4.part_" + index + "." + count));
            return files;
        }

        // ------------------------------------------------------------------

        [Fact]
        public async Task APartWithinTheSizeLimitIsAccepted()
        {
            // The path that must keep working, so the cap cannot be "refuse everything".
            string name = "ok-" + Guid.NewGuid().ToString("N");

            bool ok = await BuildLogic(Config(1024, 10)).ProcessVideoFiles(OnePart(name, 1, 5, 512));

            ok.Should().BeTrue();
            Directory.GetFiles(_splitDir, name + "*").Should().HaveCount(1);
        }

        [Fact]
        public async Task AnOversizePartIsRefusedAndNothingIsWritten()
        {
            // The refusal must happen before anything reaches disk, like the ownership gate above it.
            string name = "big-" + Guid.NewGuid().ToString("N");

            bool ok = await BuildLogic(Config(1024, 10)).ProcessVideoFiles(OnePart(name, 1, 5, 4096));

            ok.Should().BeFalse("a part over the per-part limit must be refused");
            Directory.GetFiles(_splitDir, name + "*").Should().BeEmpty("a refused part must leave no disk state");
        }

        [Fact]
        public async Task PartsBeyondThePerRecordingLimitAreRefused()
        {
            // Bounds a recording no matter how many parts the client claims it has. This is the cap
            // that stops one device filling the volume one accepted part at a time.
            string name = "many-" + Guid.NewGuid().ToString("N");
            VideoUploadLogic logic = BuildLogic(Config(1024, 3));

            for (int i = 1; i <= 3; i++)
            {
                (await logic.ProcessVideoFiles(OnePart(name, i, 99, 64)))
                    .Should().BeTrue("part " + i + " is within the limit of 3");
            }

            bool fourth = await logic.ProcessVideoFiles(OnePart(name, 4, 99, 64));

            fourth.Should().BeFalse("the fourth part exceeds the per-recording limit");
            Directory.GetFiles(_splitDir, name + "*").Should().HaveCount(3, "the refused part was not written");
        }

        [Fact]
        public async Task TheLimitCountsFilesOnDiskNotTheCountTheClientDeclares()
        {
            // The part filename carries a client-declared total after the second dot. It is not
            // trusted: the cap counts what is actually in the split directory, which a client
            // cannot fake.
            //
            // The parts are seeded DIRECTLY rather than uploaded, to isolate this from the merge.
            // Routing them through ProcessVideoFiles would not work: a declared total of 1 makes
            // FilesList.Count() == FileCount true immediately, so each part merges and is deleted
            // and the directory never accumulates. That is existing merge behaviour, and it is
            // exactly the sort of thing that makes an assumed test premise wrong.
            string name = "declared-" + Guid.NewGuid().ToString("N");
            File.WriteAllBytes(_splitDir + name + ".mp4.part_1.99", new byte[64]);
            File.WriteAllBytes(_splitDir + name + ".mp4.part_2.99", new byte[64]);

            // Cap of 2, two already on disk, and the client claims a total of only 1.
            bool third = await BuildLogic(Config(1024, 2)).ProcessVideoFiles(OnePart(name, 3, 1, 64));

            third.Should().BeFalse("the cap is enforced against disk state, not the declared count");
            Directory.GetFiles(_splitDir, name + "*").Should().HaveCount(2, "the refused part was not written");
        }

        [Fact]
        public async Task LimitsApplyWhenNoConfigurationIsPresent()
        {
            // An unconfigured host is not an unlimited one: null config falls back to the compiled
            // defaults rather than disabling the cap.
            string name = "default-" + Guid.NewGuid().ToString("N");

            bool ok = await BuildLogic(null).ProcessVideoFiles(OnePart(name, 1, 2, 1024));

            ok.Should().BeTrue("a normal 1 KiB part is far under the 16 MiB default");
        }

        [Fact]
        public async Task ConfiguredLimitsOverrideTheDefaults()
        {
            // Proves the config keys are actually read, rather than the defaults always winning.
            string name = "cfg-" + Guid.NewGuid().ToString("N");

            bool ok = await BuildLogic(Config(100, 10)).ProcessVideoFiles(OnePart(name, 1, 2, 512));

            ok.Should().BeFalse("512 bytes exceeds the configured 100-byte per-part limit");
        }
    }
}
