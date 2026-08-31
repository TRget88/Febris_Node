// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Febris.SharedServices;
using Febris.UserNode.LogicLayer.Logic.LauncherLogic;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// ROADMAP 18: <c>VideoLimits:*</c> is honoured by the logic THE HOST ACTUALLY RESOLVES.
    ///
    /// <para>
    /// WHY. <see cref="VideoUploadLogic"/> has two constructors. The greedy one reads
    /// <c>VideoLimits:MaxPartBytes</c> and <c>VideoLimits:MaxPartsPerRecording</c>; the legacy
    /// one-argument one hardcodes the defaults. MS.DI picks the most-parameters constructor it can
    /// SATISFY, and the greedy one needs <see cref="IVideoFileHandler"/>, which nothing registered
    /// on the API host. So MS.DI silently fell back to the legacy constructor, the configured limits
    /// were read by no code path that ran, and the greedy constructor's own comment -- "without it
    /// the greedy ctor is unresolvable and MS.DI silently drops to the legacy ctor below, which is
    /// exactly how a gate gets bypassed" -- described the shipped behaviour.
    /// </para>
    ///
    /// <para>
    /// <c>VideoQuotaTests</c> never noticed because it calls the greedy constructor directly. A test
    /// that constructs the thing under test by hand cannot see what the container does. This one
    /// resolves <see cref="IVideoUploadLogic"/> through a <see cref="ServiceCollection"/> that
    /// mirrors the API host's registrations, and tells the two constructors apart by configuring a
    /// limit ABOVE the compiled default: a part that only the configured limit admits is accepted
    /// when the greedy constructor ran and refused when the legacy one did.
    /// </para>
    /// </summary>
    [Collection("VideoFileSystem")]
    public class VideoUploadLogicResolutionTests : IDisposable
    {
        private const long CompiledDefaultMaxPartBytes = 16L * 1024 * 1024;

        static VideoUploadLogicResolutionTests()
        {
            // Under a non-DEBUG build the DAL's AppConfiguration reads its connection strings from
            // StaticDetails.PassedBackConfig, which the HOST passes at boot and no test host does.
            // The legacy constructor this class deliberately resolves news up the static
            // DataDbContext, so the Release configuration -- the one CI runs -- died in the type
            // initializer with a NullReferenceException while Debug quietly read
            // appsettings.Development.json from disk and passed (found by the first local Release
            // mirror after this file landed, 2026-08-23). The values below never connect: building
            // DbContextOptions does not open a socket, they only need to parse.
            if (StaticDetails.PassedBackConfig == null)
            {
                const string parseOnly = "Host=localhost;Database=febris_never_connects;Username=t;Password=t";
                StaticDetails.PassedBackConfig = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                        ["ConnectionStrings:DataDBConnection"] = parseOnly,
                        ["ConnectionStrings:XAPIDBConnection"] = parseOnly,
                        ["ConnectionStrings:UserDBConnection"] = parseOnly,
                        ["ConnectionStrings:AnalyticsDBConnection"] = parseOnly,
                    })
                    .Build();
            }
        }

        private readonly string _root;
        private readonly string _splitDir;
        private readonly string _recordingsDir;
        private readonly string _originalSplit;
        private readonly string _originalRecordings;

        public VideoUploadLogicResolutionTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "FebrisResolve_" + Guid.NewGuid().ToString("N"));
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

        /// <summary>
        /// The API host's registrations that bear on resolving <see cref="IVideoUploadLogic"/>, in
        /// the host's own shape: <c>AddScoped&lt;IVideoUploadLogic, VideoUploadLogic&gt;</c>, an
        /// <c>IRecordingLogic</c>, an <c>IHttpContextAccessor</c>, and -- the fix -- an
        /// <see cref="IVideoFileHandler"/>. The accessor and recording logic are doubles so that
        /// the test exercises RESOLUTION and the quota, not a database.
        /// </summary>
        private ServiceProvider Host(IConfiguration config, bool registerFileHandler)
        {
            ServiceCollection services = new ServiceCollection();

            DefaultHttpContext http = new DefaultHttpContext();
            Mock<IHttpContextAccessor> accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(http);
            services.AddSingleton<IHttpContextAccessor>(accessor.Object);

            Mock<Febris.UserNode.LogicLayer.Logic.DataLogic.IRecordingLogic> recordings =
                new Mock<Febris.UserNode.LogicLayer.Logic.DataLogic.IRecordingLogic>();
            recordings.Setup(r => r.MayAcceptPart(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(true);
            services.AddScoped<Febris.UserNode.LogicLayer.Logic.DataLogic.IRecordingLogic>(_ => recordings.Object);

            services.AddSingleton<IConfiguration>(config);

            if (registerFileHandler)
            {
                // The registration the API host was missing. A mock rather than the real
                // VideoFileHandler so disk I/O stays inside the temp directory.
                services.AddSingleton<IVideoFileHandler>(FileHandlerOnTempDisk());
            }

            services.AddScoped<IVideoUploadLogic, VideoUploadLogic>();
            return services.BuildServiceProvider();
        }

        private static IVideoFileHandler FileHandlerOnTempDisk()
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

        private static IConfiguration Limits(long maxPartBytes) =>
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                ["VideoLimits:MaxPartBytes"] = maxPartBytes.ToString(),
                ["VideoLimits:MaxPartsPerRecording"] = "10",
            }).Build();

        private static IFormFileCollection OnePart(string name, int bytes)
        {
            // Declared count 99 so the single part never triggers the merge.
            FormFileCollection files = new FormFileCollection();
            byte[] payload = new byte[bytes];
            files.Add(new FormFile(new MemoryStream(payload), 0, payload.Length, "file", name + ".mp4.part_1.99"));
            return files;
        }

        [Fact]
        public async Task The_resolved_logic_honours_a_configured_limit_above_the_compiled_default()
        {
            // THE discriminator. 32 MiB configured, a 17 MiB part: the greedy constructor admits
            // it, the legacy constructor's hardcoded 16 MiB refuses it. Only a container that
            // picks the greedy constructor can pass.
            using ServiceProvider host = Host(Limits(32L * 1024 * 1024), registerFileHandler: true);
            using IServiceScope scope = host.CreateScope();
            IVideoUploadLogic logic = scope.ServiceProvider.GetRequiredService<IVideoUploadLogic>();

            string name = "resolved-" + Guid.NewGuid().ToString("N");
            bool ok = await logic.ProcessVideoFiles(OnePart(name, (int)(CompiledDefaultMaxPartBytes + 1024 * 1024)));

            ok.Should().BeTrue(
                "a 17 MiB part is within the CONFIGURED 32 MiB limit, so refusing it means the compiled 16 MiB default was used instead -- the container resolved the legacy constructor");
            Directory.GetFiles(_splitDir, name + "*").Should().HaveCount(1);
        }

        [Fact]
        public async Task The_resolved_logic_still_refuses_a_part_over_the_configured_limit()
        {
            // The positive test above cannot be satisfied by "accept everything". This pins the
            // other side: the resolved logic enforces the configured limit, not merely a larger one.
            using ServiceProvider host = Host(Limits(1024), registerFileHandler: true);
            using IServiceScope scope = host.CreateScope();
            IVideoUploadLogic logic = scope.ServiceProvider.GetRequiredService<IVideoUploadLogic>();

            string name = "refused-" + Guid.NewGuid().ToString("N");
            bool ok = await logic.ProcessVideoFiles(OnePart(name, 4096));

            ok.Should().BeFalse();
            Directory.GetFiles(_splitDir, name + "*").Should().BeEmpty();
        }

        [Fact]
        public void Without_the_file_handler_registered_the_container_picks_the_legacy_constructor()
        {
            // The mechanism of the original defect, pinned so the fix cannot be "removed as
            // redundant" later. With IVideoFileHandler absent, resolution does not fail -- it
            // SUCCEEDS via the one-argument constructor, which is precisely why nothing noticed.
            // The legacy constructor self-news real RecordingLogic and queries, so the resolved
            // instance is not exercised further here; constructing it is the whole observation.
            using ServiceProvider host = Host(Limits(32L * 1024 * 1024), registerFileHandler: false);
            using IServiceScope scope = host.CreateScope();

            Action resolve = () => scope.ServiceProvider.GetRequiredService<IVideoUploadLogic>();

            resolve.Should().NotThrow(
                "MS.DI falls back to the legacy constructor rather than failing, which is how the configured limits were silently ignored");
        }
    }
}
