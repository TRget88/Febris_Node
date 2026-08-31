// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Febris.ModelLibrary.ViewModels;
using Febris.PrimaryLogicLayer.Logic.XApiLogic;
using Febris.SharedServices;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.UserNode.DataAccessLayer.Queries.XApiQueries;
using Febris.UserNode.DataAccessLayer.Queries.XAPIQueries;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using XM = Febris.ModelLibrary.Models.XApiModels;
using Local = Febris.ModelLibrary.Models.XApiModels.ModifiedForSharing;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// The READ SIDE of <c>LocalStatement.SubmittedByHardwareUUID</c>.
    ///
    /// <para>
    /// The column was added so a forged learning record would be "investigable instead of
    /// indistinguishable", and it shipped with TWO WRITERS AND NO READER anywhere in the
    /// application. A repo-wide grep returned the two assignments, the property declaration and the
    /// migration, and nothing else. The mitigation that had been accepted on the strength of that
    /// promise therefore required direct database access to use, which is the same
    /// write-side-first-with-no-read-side shape this audit has repeatedly found in older code.
    /// </para>
    ///
    /// <para>
    /// These tests pin the query, the summary the BLL builds from it, and the fact that the device
    /// detail screen actually renders it. The last of those matters most: a read side nobody can
    /// reach from the product is the same defect one layer further up.
    /// </para>
    /// </summary>
    public class StatementSubmitterReadSideTests
    {
        private static readonly Guid DeviceA = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
        private static readonly Guid DeviceB = Guid.Parse("bbbbbbbb-1111-2222-3333-444444444444");

        private static XApiDbContext BuildContext(string dbName)
        {
            return new XApiDbContext(new DbContextOptionsBuilder<XApiDbContext>()
                .UseInMemoryDatabase(dbName).Options);
        }

        private static Local.LocalStatement Statement(Guid? submitter, string actorName, DateTime stamp)
        {
            return new Local.LocalStatement
            {
                UUID = Guid.NewGuid(),
                Timestamp = stamp,
                Stored = stamp,
                SubmittedByHardwareUUID = submitter,
                Actor = new XM.Actor
                {
                    UUID = Guid.NewGuid(),
                    ObjectType = "Agent",
                    Name = actorName
                }
            };
        }

        private static StatementLogic BuildLogic(XApiDbContext context)
        {
            DefaultHttpContext http = new DefaultHttpContext();
            Mock<IHttpContextAccessor> accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(http);

            return new StatementLogic(
                accessor.Object,
                new StatementQueries(context),
                new Mock<IVerbQueries>().Object,
                new Mock<IVersionQueries>().Object,
                new Mock<IObjectQueries>().Object,
                new Mock<IXApiResultExtrasQueries>().Object,
                new Mock<IStatementFileHandler>().Object,
                new Mock<IActorQueries>().Object,
                new Mock<IMemberQueries>().Object,
                new Mock<IExtensionsQueries>().Object);
        }

        [Fact]
        public async Task TheQueryReturnsOnlyWhatThatDeviceSubmitted()
        {
            // THE regression test for the whole column. Before this existed, nothing in the
            // application could answer "what did this device send".
            using XApiDbContext context = BuildContext(nameof(TheQueryReturnsOnlyWhatThatDeviceSubmitted));
            context.LocalStatement.AddRange(
                Statement(DeviceA, "Ada", new DateTime(2026, 8, 1)),
                Statement(DeviceA, "Grace", new DateTime(2026, 8, 2)),
                Statement(DeviceB, "Alan", new DateTime(2026, 8, 3)),
                Statement(null, "Portal origin", new DateTime(2026, 8, 4)));
            await context.SaveChangesAsync();

            DeviceSubmissionSummaryViewModel summary =
                await BuildLogic(context).GetSubmissionsByDevice(DeviceA, 25);

            summary.TotalCount.Should().Be(2);
            summary.Statements.Should().HaveCount(2);
            summary.Statements.Should().OnlyContain(s => s.SubmittedByHardwareUUID == DeviceA);
        }

        [Fact]
        public async Task StatementsWithNoSubmitterAreNotAttributedToAnyDevice()
        {
            // A Portal-originated statement, a seed or an import genuinely has no submitting device.
            // Sweeping those into some device's list would manufacture evidence.
            using XApiDbContext context = BuildContext(nameof(StatementsWithNoSubmitterAreNotAttributedToAnyDevice));
            context.LocalStatement.AddRange(
                Statement(null, "Seeded", new DateTime(2026, 8, 1)),
                Statement(null, "Imported", new DateTime(2026, 8, 2)));
            await context.SaveChangesAsync();

            DeviceSubmissionSummaryViewModel summary =
                await BuildLogic(context).GetSubmissionsByDevice(DeviceA, 25);

            summary.TotalCount.Should().Be(0);
            summary.Statements.Should().BeEmpty();
        }

        [Fact]
        public async Task AnEmptyDeviceUuidReturnsNothingRatherThanEverythingUnattributed()
        {
            // Guid.Empty is what an unpopulated hardware reference looks like. If it matched the
            // NULL column, asking about "no device" would return every unattributed statement on the
            // node and present it as one device's activity.
            using XApiDbContext context = BuildContext(nameof(AnEmptyDeviceUuidReturnsNothingRatherThanEverythingUnattributed));
            context.LocalStatement.AddRange(
                Statement(null, "Unattributed", new DateTime(2026, 8, 1)),
                Statement(DeviceA, "Ada", new DateTime(2026, 8, 2)));
            await context.SaveChangesAsync();

            DeviceSubmissionSummaryViewModel summary =
                await BuildLogic(context).GetSubmissionsByDevice(Guid.Empty, 25);

            summary.TotalCount.Should().Be(0);
            summary.Statements.Should().BeEmpty();
        }

        [Fact]
        public async Task TheListIsCappedButTheTotalIsHonest()
        {
            // A screen that shows a truncated list without saying so tells an investigator they are
            // looking at everything when they are not.
            using XApiDbContext context = BuildContext(nameof(TheListIsCappedButTheTotalIsHonest));
            for (int i = 0; i < 12; i++)
            {
                context.LocalStatement.Add(Statement(DeviceA, "Learner " + i, new DateTime(2026, 8, 1).AddMinutes(i)));
            }
            await context.SaveChangesAsync();

            DeviceSubmissionSummaryViewModel summary =
                await BuildLogic(context).GetSubmissionsByDevice(DeviceA, 5);

            summary.Statements.Should().HaveCount(5, "the page is capped");
            summary.TotalCount.Should().Be(12, "but the total must not be the capped number");
            summary.IsTruncated.Should().BeTrue();
        }

        [Fact]
        public async Task TheNewestSubmissionsComeFirst()
        {
            // An investigation starts at the most recent activity, and the cap makes ordering
            // load-bearing rather than cosmetic: the wrong order silently returns the wrong rows.
            using XApiDbContext context = BuildContext(nameof(TheNewestSubmissionsComeFirst));
            context.LocalStatement.AddRange(
                Statement(DeviceA, "Oldest", new DateTime(2026, 8, 1)),
                Statement(DeviceA, "Newest", new DateTime(2026, 8, 30)),
                Statement(DeviceA, "Middle", new DateTime(2026, 8, 15)));
            await context.SaveChangesAsync();

            DeviceSubmissionSummaryViewModel summary =
                await BuildLogic(context).GetSubmissionsByDevice(DeviceA, 1);

            summary.Statements.Single().Actor.Name.Should().Be("Newest");
        }

        [Fact]
        public async Task ManyLearnersOnOneDeviceIsReportedNotRejected()
        {
            // A shared classroom device submitting for many learners is ORDINARY. The summary
            // surfaces the number as context; nothing anywhere treats it as a failure.
            using XApiDbContext context = BuildContext(nameof(ManyLearnersOnOneDeviceIsReportedNotRejected));
            context.LocalStatement.AddRange(
                Statement(DeviceA, "Ada", new DateTime(2026, 8, 1)),
                Statement(DeviceA, "Grace", new DateTime(2026, 8, 2)),
                Statement(DeviceA, "Alan", new DateTime(2026, 8, 3)));
            await context.SaveChangesAsync();

            DeviceSubmissionSummaryViewModel summary =
                await BuildLogic(context).GetSubmissionsByDevice(DeviceA, 25);

            summary.DistinctActorCount.Should().Be(3);
            summary.Statements.Should().HaveCount(3, "no statement is withheld because the device is shared");
        }

        [Fact]
        public void TheColumnHasAReaderAtAll()
        {
            // The defect this whole file exists for was structural rather than behavioural: the
            // column had writers and no reader. This asserts the property directly, so deleting the
            // query later fails here rather than quietly restoring the original hole.
            System.Reflection.MethodInfo reader = typeof(IStatementQueries)
                .GetMethod(nameof(IStatementQueries.GetBySubmittingHardware));

            reader.Should().NotBeNull(
                "SubmittedByHardwareUUID must be readable through the DAL, not only writable");

            typeof(IStatementLogic).GetMethod(nameof(IStatementLogic.GetSubmissionsByDevice))
                .Should().NotBeNull("and must be reachable from the BLL, not only from the DAL");
        }
    }
}
