// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.DataModels;
using Febris.SharedServices;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// T6 follow-on: the reaper. Video storage grew without bound -- the per-recording caps stop any
    /// ONE recording running away, but nothing ever reclaimed space across all of them, and nothing
    /// acted on free space.
    ///
    /// <para>
    /// Two jobs with deliberately different defaults, because they delete materially different
    /// things. <b>Abandoned parts</b> are fragments of uploads that never completed; they are not
    /// learner records, and the producers re-send a whole recording from part 1 rather than
    /// resuming, so a stale part is never what a retry needs. That job is ON by default at 7 days.
    /// <b>Finished recordings</b> ARE learner records, so that job is OFF unless
    /// <c>VideoRetention:PurgeAfterDays</c> is set -- the same fail-safe posture as
    /// <c>SoftDeletedUserPurger</c>, and for the same reason: deleting a person's records because
    /// nobody set a config value is not a defensible default.
    /// </para>
    /// </summary>
    [Collection("VideoFileSystem")]
    public class VideoRetentionReaperTests : IDisposable
    {
        private readonly string _root;
        private readonly string _splitDir;
        private readonly string _recordingsDir;
        private readonly string _originalSplit;
        private readonly string _originalRecordings;

        public VideoRetentionReaperTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "FebrisReap_" + Guid.NewGuid().ToString("N"));
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

        private static IConfiguration Config(params (string Key, string Value)[] pairs) =>
            new ConfigurationBuilder().AddInMemoryCollection(
                pairs.ToDictionary(p => p.Key, p => p.Value)).Build();

        private string WritePart(string name, int ageDays)
        {
            string path = _splitDir + name;
            File.WriteAllBytes(path, new byte[16]);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-ageDays));
            return path;
        }

        // ------------------------------------------------------------------
        // Abandoned parts: ON by default, and they are not learner records
        // ------------------------------------------------------------------

        [Fact]
        public async Task AbandonedPartsOlderThanTheAgeAreDeleted()
        {
            WritePart("old.mp4.part_1.5", ageDays: 30);
            VideoRetentionReaper reaper = new VideoRetentionReaper(
                new Mock<IRecordingQueries>().Object, Config(("VideoRetention:AbandonedPartDays", "7")));

            int deleted = await reaper.ReapAbandonedPartsAsync(DateTime.UtcNow, CancellationToken.None);

            deleted.Should().Be(1);
            Directory.GetFiles(_splitDir).Should().BeEmpty();
        }

        [Fact]
        public async Task RecentPartsAreLeftAlone()
        {
            // A recording still being retried must never be reaped out from under the producer.
            WritePart("fresh.mp4.part_1.5", ageDays: 1);
            VideoRetentionReaper reaper = new VideoRetentionReaper(
                new Mock<IRecordingQueries>().Object, Config(("VideoRetention:AbandonedPartDays", "7")));

            int deleted = await reaper.ReapAbandonedPartsAsync(DateTime.UtcNow, CancellationToken.None);

            deleted.Should().Be(0);
            Directory.GetFiles(_splitDir).Should().HaveCount(1);
        }

        [Fact]
        public async Task AgeIsMeasuredFromLastWriteSoARetriedPartSurvives()
        {
            // The producers re-send from part 1 rather than resuming, so a part being retried is
            // REWRITTEN. Measuring creation time would reap a recording that is actively uploading.
            string path = WritePart("retried.mp4.part_1.5", ageDays: 90);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);

            VideoRetentionReaper reaper = new VideoRetentionReaper(
                new Mock<IRecordingQueries>().Object, Config(("VideoRetention:AbandonedPartDays", "7")));

            (await reaper.ReapAbandonedPartsAsync(DateTime.UtcNow, CancellationToken.None)).Should().Be(0);
            File.Exists(path).Should().BeTrue("a part rewritten by a retry is not abandoned");
        }

        [Fact]
        public async Task NonPartFilesInTheSplitDirectoryAreNeverTouched()
        {
            // The sweep is scoped to *.part_* so anything else sharing the directory is safe.
            string stray = _splitDir + "not-a-part.txt";
            File.WriteAllText(stray, "keep me");
            File.SetLastWriteTimeUtc(stray, DateTime.UtcNow.AddDays(-365));

            VideoRetentionReaper reaper = new VideoRetentionReaper(
                new Mock<IRecordingQueries>().Object, Config(("VideoRetention:AbandonedPartDays", "7")));

            (await reaper.ReapAbandonedPartsAsync(DateTime.UtcNow, CancellationToken.None)).Should().Be(0);
            File.Exists(stray).Should().BeTrue();
        }

        [Fact]
        public async Task AbandonedPartReapingCanBeDisabled()
        {
            WritePart("old.mp4.part_1.5", ageDays: 30);
            VideoRetentionReaper reaper = new VideoRetentionReaper(
                new Mock<IRecordingQueries>().Object, Config(("VideoRetention:AbandonedPartDays", "0")));

            (await reaper.ReapAbandonedPartsAsync(DateTime.UtcNow, CancellationToken.None)).Should().Be(0);
            Directory.GetFiles(_splitDir).Should().HaveCount(1);
        }

        // ------------------------------------------------------------------
        // Finished recordings: OFF unless configured. These are learner records.
        // ------------------------------------------------------------------

        [Fact]
        public async Task RecordingsAreNEVERDeletedWhenRetentionIsUnset()
        {
            // THE safety property. An unconfigured node must not delete a single learner recording.
            Mock<IRecordingQueries> q = new Mock<IRecordingQueries>(MockBehavior.Strict);

            VideoRetentionReaper reaper = new VideoRetentionReaper(q.Object, Config());

            int deleted = await reaper.ReapExpiredRecordingsAsync(DateTime.UtcNow, CancellationToken.None);

            deleted.Should().Be(0);
            // Strict mock: any query call at all would throw. Retention off means it must not even ASK.
            q.Verify(x => x.GetOlderThan(It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task ANonPositiveRetentionAlsoDeletesNothing()
        {
            Mock<IRecordingQueries> q = new Mock<IRecordingQueries>(MockBehavior.Strict);
            VideoRetentionReaper reaper = new VideoRetentionReaper(
                q.Object, Config(("VideoRetention:PurgeAfterDays", "0")));

            (await reaper.ReapExpiredRecordingsAsync(DateTime.UtcNow, CancellationToken.None)).Should().Be(0);
            q.Verify(x => x.GetOlderThan(It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task ExpiredRecordingsAreDeletedWithTheirFileAndRow()
        {
            string name = "expired-" + Guid.NewGuid().ToString("N");
            File.WriteAllBytes(_recordingsDir + name + ".mp4", new byte[32]);

            Recording row = new Recording { Id = 7, Name = name, ActorUUID = Guid.NewGuid() };
            Mock<IRecordingQueries> q = new Mock<IRecordingQueries>();
            q.Setup(x => x.GetOlderThan(It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Recording> { row });
            q.Setup(x => x.Delete(7)).ReturnsAsync(true);

            VideoRetentionReaper reaper = new VideoRetentionReaper(
                q.Object, Config(("VideoRetention:PurgeAfterDays", "30")));

            int deleted = await reaper.ReapExpiredRecordingsAsync(DateTime.UtcNow, CancellationToken.None);

            deleted.Should().Be(1);
            File.Exists(_recordingsDir + name + ".mp4").Should().BeFalse("the file must go");
            q.Verify(x => x.Delete(7), Times.Once, "and so must the ownership row");
        }

        [Fact]
        public async Task TheCutoffIsDerivedFromTheConfiguredRetention()
        {
            DateTime now = new DateTime(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);
            DateTime? asked = null;

            Mock<IRecordingQueries> q = new Mock<IRecordingQueries>();
            q.Setup(x => x.GetOlderThan(It.IsAny<DateTime>()))
                .Callback<DateTime>(d => asked = d)
                .ReturnsAsync(new List<Recording>());

            VideoRetentionReaper reaper = new VideoRetentionReaper(
                q.Object, Config(("VideoRetention:PurgeAfterDays", "30")));

            await reaper.ReapExpiredRecordingsAsync(now, CancellationToken.None);

            asked.Should().Be(now.AddDays(-30), "the cutoff must reflect the configured retention");
        }

        [SkippableFact]
        public async Task AFileThatCannotBeDeletedDoesNotRemoveItsRow()
        {
            // Order matters: file first, row second. If the row went first and the file delete then
            // failed, the recording would be permanently unviewable but still occupying disk with
            // nothing left to identify it by.
            //
            // Staging a delete that FAILS is the platform-specific part, and the first version of
            // this test only knew the Windows half, which is why it could never pass on the ubuntu
            // runner CI actually uses. MakeUndeletable carries the portable arrangement and the
            // reason for it.
            string name = "locked-" + Guid.NewGuid().ToString("N");
            string file = _recordingsDir + name + ".mp4";
            File.WriteAllBytes(file, new byte[32]);

            Recording row = new Recording { Id = 9, Name = name, ActorUUID = Guid.NewGuid() };
            Mock<IRecordingQueries> q = new Mock<IRecordingQueries>();
            q.Setup(x => x.GetOlderThan(It.IsAny<DateTime>())).ReturnsAsync(new List<Recording> { row });
            q.Setup(x => x.Delete(9)).ReturnsAsync(true);

            VideoRetentionReaper reaper = new VideoRetentionReaper(
                q.Object, Config(("VideoRetention:PurgeAfterDays", "30")));

            using (MakeUndeletable(file))
            {
                int deleted = await reaper.ReapExpiredRecordingsAsync(DateTime.UtcNow, CancellationToken.None);
                deleted.Should().Be(0, "the file could not be deleted, so the recording was not reaped");
                File.Exists(file).Should().BeTrue("the arrangement is worthless if the file went anyway");
                q.Verify(x => x.Delete(9), Times.Never, "the row must outlive a file that is still there");
            }
        }

        // ------------------------------------------------------------------
        // CORRECTIONS found by adversarially reviewing this reaper's own defaults
        // ------------------------------------------------------------------

        [Fact]
        public async Task ACompletePartSetIsNeverReapedEvenWhenStale()
        {
            // THE correction. The first version matched on glob plus mtime alone and never asked
            // whether the set was COMPLETE, so a fully uploaded recording that merely never merged
            // was deleted as though it were an abandoned fragment.
            //
            // That is reachable, not theoretical: VideoUploadLogic answers 200 on a skipped merge
            // and on an incomplete set, MergeFile has no retry job, and the PC producer moves its
            // own copy out of the watched folder once it sees that 200. At the moment this ran, the
            // parts were the only copy of that learner's session in existence.
            WritePart("session-a.mp4.part_1.3", ageDays: 30);
            WritePart("session-a.mp4.part_2.3", ageDays: 30);
            WritePart("session-a.mp4.part_3.3", ageDays: 30);

            VideoRetentionReaper reaper = new VideoRetentionReaper(
                new Mock<IRecordingQueries>().Object, Config(("VideoRetention:AbandonedPartDays", "7")));

            int deleted = await reaper.ReapAbandonedPartsAsync(DateTime.UtcNow, CancellationToken.None);

            deleted.Should().Be(0, "a complete set is mergeable, not abandoned");
            Directory.GetFiles(_splitDir).Should().HaveCount(3, "all three parts must survive");
        }

        [Fact]
        public async Task AnIncompletePartSetIsStillReaped()
        {
            // The other side of the correction. Skipping complete sets must not turn the reaper off
            // entirely: a genuinely abandoned upload is still what this job exists to clean up.
            WritePart("session-b.mp4.part_1.3", ageDays: 30);
            WritePart("session-b.mp4.part_2.3", ageDays: 30);

            VideoRetentionReaper reaper = new VideoRetentionReaper(
                new Mock<IRecordingQueries>().Object, Config(("VideoRetention:AbandonedPartDays", "7")));

            int deleted = await reaper.ReapAbandonedPartsAsync(DateTime.UtcNow, CancellationToken.None);

            deleted.Should().Be(2, "two of three parts is an abandoned upload");
            Directory.GetFiles(_splitDir).Should().BeEmpty();
        }

        [Fact]
        public async Task APartWithAnUnparseableCountIsTreatedAsIncomplete()
        {
            // Unparseable debris must not become immortal by failing the completeness test open.
            WritePart("session-c.mp4.part_1.notanumber", ageDays: 30);

            VideoRetentionReaper reaper = new VideoRetentionReaper(
                new Mock<IRecordingQueries>().Object, Config(("VideoRetention:AbandonedPartDays", "7")));

            int deleted = await reaper.ReapAbandonedPartsAsync(DateTime.UtcNow, CancellationToken.None);

            deleted.Should().Be(1);
        }

        [Fact]
        public async Task AnExpiredRecordingWithNoStoredFileKeepsItsOwnershipRow()
        {
            // THE second correction. Recording.TimeStamp is stamped at module LAUNCH, not when a
            // video arrives, so a learner who launched and uploaded later than the retention window
            // had their ownership row deleted while no file had ever existed. MayAcceptPart then
            // refuses every part with "no recording was minted by this node", making that upload
            // permanently and silently impossible.
            Recording row = new Recording { Id = 11, Name = "never-uploaded-" + Guid.NewGuid().ToString("N"), ActorUUID = Guid.NewGuid() };
            Mock<IRecordingQueries> q = new Mock<IRecordingQueries>();
            q.Setup(x => x.GetOlderThan(It.IsAny<DateTime>())).ReturnsAsync(new List<Recording> { row });
            q.Setup(x => x.Delete(It.IsAny<long>())).ReturnsAsync(true);

            VideoRetentionReaper reaper = new VideoRetentionReaper(
                q.Object, Config(("VideoRetention:PurgeAfterDays", "30")));

            int deleted = await reaper.ReapExpiredRecordingsAsync(DateTime.UtcNow, CancellationToken.None);

            deleted.Should().Be(0, "there was no disk to reclaim");
            q.Verify(x => x.Delete(It.IsAny<long>()), Times.Never,
                "deleting the ownership row would block the learner's upload forever");
        }

        // ------------------------------------------------------------------
        // Staging a delete that fails, on both platforms
        // ------------------------------------------------------------------

        /// <summary>
        /// Makes <paramref name="file"/> undeletable for the lifetime of the returned handle.
        ///
        /// <para>
        /// Windows and POSIX disagree about what an open file is. On Windows a handle opened with
        /// <c>FileShare.None</c> blocks <c>File.Delete</c> outright. On Linux it does not: unlink
        /// removes the NAME and the open handle keeps working, so the Windows-only arrangement let
        /// the reap succeed and made this assertion unreachable on ubuntu-latest, which is where CI
        /// runs.
        /// </para>
        ///
        /// <para>
        /// Unlink is a write to the DIRECTORY rather than to the file, so the POSIX arrangement
        /// takes the write bit off the containing directory and leaves read and execute on, which
        /// keeps the reaper's own <c>File.Exists</c> working and fails it on the delete instead.
        /// Root ignores directory permissions entirely, so that arrangement is PROVED with a
        /// throwaway probe file and the test skips when it does not bite, rather than passing while
        /// asserting nothing.
        /// </para>
        /// </summary>
        private static IDisposable MakeUndeletable(string file)
        {
            if (OperatingSystem.IsWindows())
            {
                return new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None);
            }
            return new UnwritableDirectory(Path.GetDirectoryName(file));
        }

        /// <summary>
        /// Holds a directory at read-plus-execute so nothing inside it can be unlinked, and puts
        /// the original mode back on dispose.
        /// </summary>
        [UnsupportedOSPlatform("windows")]
        private sealed class UnwritableDirectory : IDisposable
        {
            private readonly DirectoryInfo _dir;
            private readonly UnixFileMode _original;
            private readonly string _probe;

            public UnwritableDirectory(string dir)
            {
                _dir = new DirectoryInfo(dir);
                _original = _dir.UnixFileMode;

                // The probe has to exist BEFORE the mode changes: File.Delete on a path that is not
                // there is a no-op rather than a throw, so an absent probe would read as "the mode
                // did not bite" and would skip a test that could have run.
                _probe = Path.Combine(dir, "undeletable-probe.tmp");
                File.WriteAllBytes(_probe, Array.Empty<byte>());

                _dir.UnixFileMode = UnixFileMode.UserRead | UnixFileMode.UserExecute;

                bool bites;
                try { File.Delete(_probe); bites = false; }
                catch (UnauthorizedAccessException) { bites = true; }
                catch (IOException) { bites = true; }

                if (!bites)
                {
                    Dispose();
                }

                Skip.If(!bites,
                    "this user can unlink from a directory it holds no write permission on (root), " +
                    "so a delete that fails cannot be staged here");
            }

            public void Dispose()
            {
                try { _dir.UnixFileMode = _original; } catch (IOException) { }
                try { File.Delete(_probe); } catch (IOException) { }
            }
        }
    }
}
