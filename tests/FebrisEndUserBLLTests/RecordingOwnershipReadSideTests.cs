// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Linq;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// The device-activity view: what a given device has minted.
    ///
    /// <para>
    /// AN OPERATIONS FEATURE, NOT A SECURITY CONTROL. "What has this device done" is a routine admin
    /// question -- a headset attributing to the wrong roster, a device that has stopped recording, a
    /// support call about a missing video. These tests pin the query, the summary and the fact that
    /// it reports rather than judges.
    /// </para>
    ///
    /// <para>
    /// THE FRAMING THIS REPLACES, RECORDED SO IT IS NOT REINTRODUCED. The audit called recording
    /// ownership "forgeable", because the actor arrives in the launch body
    /// (<c>LauncherLogic.cs:596</c>) and is written as the owner (<c>:767</c>) with no check against
    /// the calling device. That describes the code correctly and is NOT a defect: a classroom kiosk
    /// cannot prove which learner is standing at it, the device token proves the DEVICE and the
    /// launch carries the LEARNER, and one headset serves a whole class in sequence. The split is
    /// the design.
    /// </para>
    ///
    /// <para>
    /// The first version of this file went further than the audit and called the design an integrity
    /// and disclosure problem, on the grounds that a recording attributed to a learner becomes
    /// visible to their guardian. That is the feature working. Retracted. See the ownership ruling
    /// in <c>docs/BUGS.md</c>.
    /// </para>
    /// </summary>
    public class RecordingOwnershipReadSideTests
    {
        private static readonly Guid DeviceA = Guid.Parse("dddddddd-1111-2222-3333-444444444444");
        private static readonly Guid DeviceB = Guid.Parse("eeeeeeee-1111-2222-3333-444444444444");

        private static DataDbContext BuildContext(string dbName)
        {
            return new DataDbContext(new DbContextOptionsBuilder<DataDbContext>()
                .UseInMemoryDatabase(dbName).Options);
        }

        private static RecordingLogic BuildLogic(DataDbContext context)
        {
            Mock<IHttpContextAccessor> accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());

            return new RecordingLogic(
                accessor.Object,
                new RecordingQueries(context),
                new Mock<Febris.UserNode.DataAccessLayer.Queries.DataQueries.IParentLinkedStudentQueries>().Object);
        }

        private static Recording Rec(Guid device, Guid actor, DateTime stamp, string name)
        {
            return new Recording
            {
                UUID = Guid.NewGuid(),
                Name = name,
                ActorUUID = actor,
                HardwareUUID = device,
                TimeStamp = stamp,
                LastUpdateTimeStamp = stamp
            };
        }

        [Fact]
        public async Task TheQueryReturnsOnlyWhatThatDeviceMinted()
        {
            using DataDbContext context = BuildContext(nameof(TheQueryReturnsOnlyWhatThatDeviceMinted));
            context.Recording.AddRange(
                Rec(DeviceA, Guid.NewGuid(), new DateTime(2026, 8, 1), "a1"),
                Rec(DeviceA, Guid.NewGuid(), new DateTime(2026, 8, 2), "a2"),
                Rec(DeviceB, Guid.NewGuid(), new DateTime(2026, 8, 3), "b1"));
            await context.SaveChangesAsync();

            DeviceRecordingSummaryViewModel summary =
                await BuildLogic(context).GetRecordingsByDevice(DeviceA, 25);

            summary.TotalCount.Should().Be(2);
            summary.Recordings.Should().OnlyContain(r => r.HardwareUUID == DeviceA);
        }

        [Fact]
        public async Task AnEmptyDeviceUuidReturnsNothingRatherThanEverythingUnattributed()
        {
            // Register writes Guid.Empty when a launch had no hardware. Matching it would gather
            // every unattributed recording under one heading, which reads as real evidence.
            using DataDbContext context = BuildContext(nameof(AnEmptyDeviceUuidReturnsNothingRatherThanEverythingUnattributed));
            context.Recording.AddRange(
                Rec(Guid.Empty, Guid.NewGuid(), new DateTime(2026, 8, 1), "orphan"),
                Rec(DeviceA, Guid.NewGuid(), new DateTime(2026, 8, 2), "a1"));
            await context.SaveChangesAsync();

            DeviceRecordingSummaryViewModel summary =
                await BuildLogic(context).GetRecordingsByDevice(Guid.Empty, 25);

            summary.TotalCount.Should().Be(0);
            summary.Recordings.Should().BeEmpty();
        }

        [Fact]
        public async Task TheListIsCappedButTheTotalIsHonest()
        {
            using DataDbContext context = BuildContext(nameof(TheListIsCappedButTheTotalIsHonest));
            for (int i = 0; i < 9; i++)
            {
                context.Recording.Add(Rec(DeviceA, Guid.NewGuid(), new DateTime(2026, 8, 1).AddMinutes(i), "r" + i));
            }
            await context.SaveChangesAsync();

            DeviceRecordingSummaryViewModel summary =
                await BuildLogic(context).GetRecordingsByDevice(DeviceA, 4);

            summary.Recordings.Should().HaveCount(4);
            summary.TotalCount.Should().Be(9, "a truncated list must not be reported as the whole history");
            summary.IsTruncated.Should().BeTrue();
        }

        [Fact]
        public async Task TheNewestRecordingsComeFirst()
        {
            // Load-bearing rather than cosmetic: with a cap, the wrong order returns the wrong rows.
            using DataDbContext context = BuildContext(nameof(TheNewestRecordingsComeFirst));
            context.Recording.AddRange(
                Rec(DeviceA, Guid.NewGuid(), new DateTime(2026, 8, 1), "oldest"),
                Rec(DeviceA, Guid.NewGuid(), new DateTime(2026, 8, 30), "newest"),
                Rec(DeviceA, Guid.NewGuid(), new DateTime(2026, 8, 15), "middle"));
            await context.SaveChangesAsync();

            DeviceRecordingSummaryViewModel summary =
                await BuildLogic(context).GetRecordingsByDevice(DeviceA, 1);

            summary.Recordings.Single().Name.Should().Be("newest");
        }

        [Fact]
        public async Task ManyLearnersOnOneDeviceIsReportedNotRejected()
        {
            // A shared classroom device minting video for several learners is ORDINARY -- it is what
            // a class using one headset in sequence looks like. The count is context for a reader
            // and nothing anywhere treats it as a signal.
            using DataDbContext context = BuildContext(nameof(ManyLearnersOnOneDeviceIsReportedNotRejected));
            context.Recording.AddRange(
                Rec(DeviceA, Guid.NewGuid(), new DateTime(2026, 8, 1), "r1"),
                Rec(DeviceA, Guid.NewGuid(), new DateTime(2026, 8, 2), "r2"),
                Rec(DeviceA, Guid.NewGuid(), new DateTime(2026, 8, 3), "r3"));
            await context.SaveChangesAsync();

            DeviceRecordingSummaryViewModel summary =
                await BuildLogic(context).GetRecordingsByDevice(DeviceA, 25);

            summary.DistinctActorCount.Should().Be(3);
            summary.Recordings.Should().HaveCount(3, "no recording is withheld because the device is shared");
        }

        [Fact]
        public async Task NothingHereRefusesOrAltersARecording()
        {
            // The panel REPORTS, it does not judge. Any learner may legitimately appear against any
            // device, because the launch context decides the learner and a shared headset serves a
            // whole class. So the row is returned as stored, not flagged, hidden or rewritten.
            //
            // Worth keeping even though the "forgeable ownership" framing was retracted: a later
            // change that started filtering or flagging here would break shared devices, which is
            // the real risk in this area and points the opposite way from the original finding.
            using DataDbContext context = BuildContext(nameof(NothingHereRefusesOrAltersARecording));
            Guid anyLearner = Guid.NewGuid();
            context.Recording.Add(Rec(DeviceA, anyLearner, new DateTime(2026, 8, 1), "ordinary"));
            await context.SaveChangesAsync();

            DeviceRecordingSummaryViewModel summary =
                await BuildLogic(context).GetRecordingsByDevice(DeviceA, 25);

            summary.Recordings.Single().ActorUUID.Should().Be(anyLearner,
                "the claimed actor is reported as-is, because reporting it is the point");
            context.Recording.Single().ActorUUID.Should().Be(anyLearner,
                "and the stored row is not rewritten by reading it");
        }
    }
}
