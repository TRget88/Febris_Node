// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Febris.UserNode.DataAccessLayer.Queries.AnalyticsQueries;
using Febris.UserNode.LogicLayer.Logic.AnalyticsLogic;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// T11 retention for the analytics databases.
    ///
    /// <para>
    /// These tables grew one row per event with no retention at all, holding per-request learner PII
    /// that is rendered to Org Admins. Unlike the video and account purgers, this one defaults ON,
    /// because here the defect is KEEPING the data: finding H-26 is the standing proof that this
    /// table retained forever is dangerous rather than untidy.
    /// </para>
    ///
    /// <para>
    /// The property these tests exist to protect is the SPLIT. Two of the three tables are deleted.
    /// The third, module launches, is only ever ANONYMISED, because LauncherLogic never persists the
    /// launch statement, so for a learner who launches and never completes, that row is the only
    /// record the node holds that they engaged with the module at all. Deleting it would destroy a
    /// student record, which is the exact failure this whole audit exists to prevent.
    /// </para>
    /// </summary>
    public class AnalyticsRetentionReaperTests
    {
        private sealed class Harness
        {
            public Mock<ILocalAnalyticsQueries> Local = new Mock<ILocalAnalyticsQueries>();
            public Mock<IModuleDownloadAnalyticsQueries> Downloads = new Mock<IModuleDownloadAnalyticsQueries>();
            public Mock<IModuleUsageAnalyticsQueries> Usage = new Mock<IModuleUsageAnalyticsQueries>();
            public AnalyticsRetentionReaper Reaper;
        }

        private static IConfiguration Config(params (string Key, string Value)[] pairs) =>
            new ConfigurationBuilder().AddInMemoryCollection(
                pairs.ToDictionary(p => p.Key, p => p.Value)).Build();

        /// <summary>
        /// Each delete/anonymise stub returns the requested count once and then zero, so the reaper's
        /// batch loop terminates the way it would against a real table.
        /// </summary>
        private static Harness Build(IConfiguration config, int localRows = 0, int downloadRows = 0, int usageRows = 0)
        {
            Harness h = new Harness();

            SetupBatches(h.Local, localRows);
            SetupBatches(h.Downloads, downloadRows);

            int usageLeft = usageRows;
            h.Usage.Setup(x => x.AnonymiseOlderThan(It.IsAny<DateTime>(), It.IsAny<int>()))
                .ReturnsAsync((DateTime _, int batch) =>
                {
                    int take = Math.Min(batch, usageLeft);
                    usageLeft -= take;
                    return take;
                });

            h.Reaper = new AnalyticsRetentionReaper(h.Local.Object, h.Downloads.Object, h.Usage.Object, config);
            return h;
        }

        private static void SetupBatches<T>(Mock<T> mock, int rows) where T : class
        {
            int left = rows;
            if (mock.Object is ILocalAnalyticsQueries)
            {
                (mock as Mock<ILocalAnalyticsQueries>).Setup(x => x.DeleteOlderThan(It.IsAny<DateTime>(), It.IsAny<int>()))
                    .ReturnsAsync((DateTime _, int batch) => { int take = Math.Min(batch, left); left -= take; return take; });
            }
            else if (mock.Object is IModuleDownloadAnalyticsQueries)
            {
                (mock as Mock<IModuleDownloadAnalyticsQueries>).Setup(x => x.DeleteOlderThan(It.IsAny<DateTime>(), It.IsAny<int>()))
                    .ReturnsAsync((DateTime _, int batch) => { int take = Math.Min(batch, left); left -= take; return take; });
            }
        }

        // ------------------------------------------------------------------
        // THE split: what is deleted and what is never deleted
        // ------------------------------------------------------------------

        [Fact]
        public async Task ModuleLaunchRowsAreNEVERDeleted()
        {
            // THE safety property. LauncherLogic does not persist the launch statement, so for an
            // incomplete session this row is the only evidence the learner engaged with the module.
            Harness h = Build(Config(), localRows: 10, downloadRows: 10, usageRows: 10);

            await h.Reaper.ReapExpiredRequestAnalyticsAsync(DateTime.UtcNow, CancellationToken.None);
            await h.Reaper.AnonymiseOldLaunchAnalyticsAsync(DateTime.UtcNow, CancellationToken.None);

            // IModuleUsageAnalyticsQueries has no delete surface at all, and the reaper must never
            // reach for one. Anonymise is the only thing it may do to a launch row.
            h.Usage.Verify(x => x.AnonymiseOlderThan(It.IsAny<DateTime>(), It.IsAny<int>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task RequestAndDownloadAnalyticsAreDeletedPastTheWindow()
        {
            Harness h = Build(Config(("AnalyticsRetention:PurgeAfterDays", "365")), localRows: 5, downloadRows: 3);

            int deleted = await h.Reaper.ReapExpiredRequestAnalyticsAsync(DateTime.UtcNow, CancellationToken.None);

            deleted.Should().Be(8);
            h.Local.Verify(x => x.DeleteOlderThan(It.IsAny<DateTime>(), It.IsAny<int>()), Times.AtLeastOnce);
            h.Downloads.Verify(x => x.DeleteOlderThan(It.IsAny<DateTime>(), It.IsAny<int>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task LaunchRowsAreAnonymisedRatherThanRemoved()
        {
            Harness h = Build(Config(("AnalyticsRetention:AnonymiseAfterDays", "90")), usageRows: 7);

            int changed = await h.Reaper.AnonymiseOldLaunchAnalyticsAsync(DateTime.UtcNow, CancellationToken.None);

            changed.Should().Be(7);
        }

        // ------------------------------------------------------------------
        // Defaults, which differ deliberately from the other two purgers
        // ------------------------------------------------------------------

        [Fact]
        public async Task ThePurgeIsONByDefault()
        {
            // Deliberately UNLIKE VideoRetention:PurgeAfterDays and AccountLifecycle:PurgeAfterDays,
            // which both fail safe to off because they guard learner records. This guards request
            // exhaust, where keeping it because nobody set a value is itself the defect, and both of
            // those knobs ship inert today, which is what a default-off policy actually achieves.
            Harness h = Build(Config(), localRows: 4);

            int deleted = await h.Reaper.ReapExpiredRequestAnalyticsAsync(DateTime.UtcNow, CancellationToken.None);

            deleted.Should().Be(4, "no configuration at all must still bound the table");
        }

        [Fact]
        public async Task TheDefaultWindowIsAYear()
        {
            DateTime now = new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);
            DateTime? asked = null;

            Harness h = Build(Config(), localRows: 1);
            h.Local.Setup(x => x.DeleteOlderThan(It.IsAny<DateTime>(), It.IsAny<int>()))
                .ReturnsAsync((DateTime cutoff, int _) => { asked = cutoff; return 0; });

            await h.Reaper.ReapExpiredRequestAnalyticsAsync(now, CancellationToken.None);

            asked.Should().Be(now.AddDays(-365));
        }

        [Fact]
        public async Task ThePurgeCanBeTurnedOffExplicitly()
        {
            // An operator who wants everything kept must be able to say so.
            Harness h = Build(Config(("AnalyticsRetention:PurgeAfterDays", "0")), localRows: 10);

            int deleted = await h.Reaper.ReapExpiredRequestAnalyticsAsync(DateTime.UtcNow, CancellationToken.None);

            deleted.Should().Be(0);
            h.Local.Verify(x => x.DeleteOlderThan(It.IsAny<DateTime>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task AnonymisationCanBeTurnedOffExplicitly()
        {
            Harness h = Build(Config(("AnalyticsRetention:AnonymiseAfterDays", "0")), usageRows: 10);

            int changed = await h.Reaper.AnonymiseOldLaunchAnalyticsAsync(DateTime.UtcNow, CancellationToken.None);

            changed.Should().Be(0);
            h.Usage.Verify(x => x.AnonymiseOlderThan(It.IsAny<DateTime>(), It.IsAny<int>()), Times.Never);
        }

        // ------------------------------------------------------------------
        // Batching and failure posture
        // ------------------------------------------------------------------

        [Fact]
        public async Task DeletionIsBatchedRatherThanOneLongTransaction()
        {
            // A single DELETE across a table collecting a row per request since installation is a
            // long transaction holding locks. More rows than one batch must mean more than one call.
            Harness h = Build(Config(), localRows: 2500);

            await h.Reaper.ReapExpiredRequestAnalyticsAsync(DateTime.UtcNow, CancellationToken.None);

            h.Local.Verify(x => x.DeleteOlderThan(It.IsAny<DateTime>(), It.IsAny<int>()),
                Times.AtLeast(3), "2500 rows cannot come out in one batch of 1000");
        }

        [Fact]
        public async Task AFailureOnOneTableDoesNotAbandonTheOther()
        {
            // The batch-abandonment lesson from UserLogic, applied here before it can bite.
            Harness h = Build(Config(), downloadRows: 5);
            h.Local.Setup(x => x.DeleteOlderThan(It.IsAny<DateTime>(), It.IsAny<int>()))
                .ThrowsAsync(new InvalidOperationException("table locked"));

            int deleted = await h.Reaper.ReapExpiredRequestAnalyticsAsync(DateTime.UtcNow, CancellationToken.None);

            deleted.Should().Be(5, "the download table must still be trimmed");
            h.Downloads.Verify(x => x.DeleteOlderThan(It.IsAny<DateTime>(), It.IsAny<int>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task AThrowingRunReportsZeroRatherThanCrashingTheHost()
        {
            Harness h = Build(Config());
            h.Usage.Setup(x => x.AnonymiseOlderThan(It.IsAny<DateTime>(), It.IsAny<int>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

            int changed = await h.Reaper.AnonymiseOldLaunchAnalyticsAsync(DateTime.UtcNow, CancellationToken.None);

            changed.Should().Be(0);
        }
    }
}
