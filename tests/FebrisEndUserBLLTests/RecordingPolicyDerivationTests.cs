// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Febris.ModelLibrary.LauncherModels;
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.Models.UserModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.LogicLayer.Logic.LauncherLogic;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using XM = Febris.ModelLibrary.Models.XApiModels;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// ROADMAP 22: the node DERIVES whether a launch is recorded from the educator's per-cohort
    /// policy, and the client does not get a vote.
    ///
    /// <para>
    /// WHY THESE EXIST. Before this change the record decision was read off
    /// <c>input.RecordSession</c>, and NOTHING anywhere asserted when a video attachment is or is
    /// not produced -- <c>RecordSession</c> appeared zero times under tests/, so the attachment
    /// branch was never executed by any test. Two separate defects lived in that gap: neither
    /// shipped client ever populated the bool (so the branch was dead and no launch was ever
    /// recorded), and had one populated it, the person being recorded would have been the one
    /// deciding, from a device that proves nothing about the learner it names.
    /// </para>
    ///
    /// <para>
    /// THE SECURITY-SHAPED ASSERTIONS are <see cref="ClientAskingToRecord_IsIgnored_WithoutAPolicy"/>
    /// and <see cref="ClientRefusingToRecord_CannotSuppress_ThePolicy"/>. A launch request proves
    /// only the DEVICE (the middleware-validated hardware JWT); its ActorId is client-asserted and
    /// checked against nothing. Those two pin that the wire flag moves nothing in either
    /// direction, which is the property that makes the ActorId's untrustworthiness survivable.
    /// </para>
    ///
    /// <para>
    /// Union semantics (owner ruling 2026-08-24): a launch is recorded when EITHER the device's
    /// linked cohorts or the learner's memberships carry the policy. Both arms are pinned
    /// separately, because a union that silently only ever consults one arm passes a
    /// both-arms-set test while being half broken.
    /// </para>
    /// </summary>
    public class RecordingPolicyDerivationTests
    {
        private static DataDbContext BuildDataContext(string dbName)
        {
            DbContextOptions<DataDbContext> options = new DbContextOptionsBuilder<DataDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new DataDbContext(options);
        }

        /// <summary>
        /// The launch chain with REAL cohort wiring: the hardware-linked-cohort and cohort-member
        /// queries run against the InMemory context so the derivation actually resolves, rather
        /// than being mocked into whatever answer the test wants. Only the xAPI lookups, the user
        /// directory (which lives in a different DbContext) and the analytics/statement
        /// collaborators are mocks.
        /// </summary>
        private static LauncherLogic BuildLauncherLogic(
            DataDbContext context,
            Hardware requestHardware,
            Guid actorUuid,
            LocalApplicationUser learner = null)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Items["Hardware"] = requestHardware;
            var accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(httpContext);

            var actorQueries = new Mock<Febris.UserNode.DataAccessLayer.Queries.XAPIQueries.IActorQueries>();
            actorQueries.Setup(a => a.Get(It.IsAny<Guid>()))
                .ReturnsAsync(new XM.Actor() { UUID = actorUuid });

            var objectQueries = new Mock<Febris.UserNode.DataAccessLayer.Queries.XApiQueries.IObjectQueries>();
            objectQueries.Setup(o => o.Get(It.IsAny<long>()))
                .ReturnsAsync(new XM.Object() { UUID = Guid.NewGuid(), ObjectType = "Activity" });

            var verbQueries = new Mock<Febris.UserNode.DataAccessLayer.Queries.XApiQueries.IVerbQueries>();
            verbQueries.Setup(v => v.Get(It.IsAny<Uri>()))
                .ReturnsAsync(new XM.Verb() { Id = new Uri("https://febr.is/Verb/Details/Initialized") });

            // The user directory is on ApplicationDbContext, not this one, so the actor-reverse
            // hop is the one mock in the derivation path. Null learner means "no account claims
            // that actor", which is the shape a test-user or an unlinked actor produces.
            var userQueries = new Mock<Febris.UserNode.DataAccessLayer.Queries.UserQueries.IUserQueries>();
            userQueries.Setup(u => u.GetByActor(It.IsAny<Guid>()))
                .ReturnsAsync(learner);

            return new LauncherLogic(
                accessor.Object,
                new HardwareQueries(context),
                new HardwareLinkedModuleQueries(context),
                new Mock<IMessageBoardQueries>().Object,
                userQueries.Object,
                new ModuleQueries(context),
                new ModuleLinkedObjectQueries(context),
                actorQueries.Object,
                objectQueries.Object,
                verbQueries.Object,
                new HardwareLinkedCohortQueries(context),
                new Mock<PrimaryLogicLayer.Logic.XApiLogic.IStatementLogic>().Object,
                new CohortMemberQueries(context),
                new Mock<ITestUserQueries>().Object,
                new Mock<Logic.AnalyticsLogic.IModuleUsageAnalyticsLogic>().Object,
                new Mock<Logic.DataLogic.IRecordingLogic>().Object,
                null);
        }

        /// <summary>An entitled device and a module it may launch: the precondition for every
        /// case below, since the record decision is only reached after the entitlement gate.</summary>
        private static (Module Module, LocalHardware Hardware) SeedEntitledLaunch(DataDbContext context)
        {
            var module = new Module()
            {
                UUID = Guid.NewGuid(),
                Name = "Lockout Tagout",
                ModuleClassification = new ModuleClassification() { UUID = Guid.NewGuid(), Name = "Training" }
            };
            context.Module.Add(module);
            context.ModuleLinkedObject.Add(new ModuleLinkedObject()
            {
                UUID = Guid.NewGuid(),
                Module = module,
                ModuleUUID = module.UUID,
                ObjectId = 5,
                ObjectUUID = Guid.NewGuid()
            });

            var hardware = new LocalHardware() { UUID = Guid.NewGuid() };
            context.Hardware.Add(hardware);
            context.SaveChanges();

            context.HardwareLinkedModule.Add(new LocalHardwareLinkedModule()
            {
                UUID = Guid.NewGuid(),
                Hardware = hardware,
                HardwareUUID = hardware.UUID,
                ModuleId = module.Id,
                ModuleUUID = module.UUID
            });
            context.SaveChanges();
            return (module, hardware);
        }

        private static Cohort SeedCohort(DataDbContext context, bool recordSessions, bool archived = false)
        {
            var cohort = new Cohort()
            {
                UUID = Guid.NewGuid(),
                Name = "Spring Term",
                RecordSessions = recordSessions,
                Archive = archived
            };
            context.Cohort.Add(cohort);
            context.SaveChanges();
            return cohort;
        }

        private static void LinkDeviceToCohort(DataDbContext context, LocalHardware hardware, Cohort cohort)
        {
            context.HardwareLinkedCohort.Add(new HardwareLinkedCohort()
            {
                UUID = Guid.NewGuid(),
                Hardware = hardware,
                HardwareUUID = hardware.UUID,
                Cohort = cohort,
                CohortUUID = cohort.UUID
            });
            context.SaveChanges();
        }

        private static void AddLearnerToCohort(DataDbContext context, Guid userId, Cohort cohort)
        {
            context.CohortMember.Add(new CohortMember()
            {
                UUID = Guid.NewGuid(),
                UserId = userId,
                Cohort = cohort,
                CohortUUID = cohort.UUID
            });
            context.SaveChanges();
        }

        private static StatementInitalizationRequestViewModel Request(
            Module module, Guid actorUuid, bool clientAsksToRecord = false)
        {
            return new StatementInitalizationRequestViewModel()
            {
                ActorId = actorUuid,
                ModuleId = module.UUID,
                RecordSession = clientAsksToRecord
            };
        }

        /// <summary>The video attachment is the instruction to record: ContentType video/mp4.</summary>
        private static bool Records(StatementInitalizationResponseViewModel response)
        {
            List<XM.Attachment> attachments = response?.Statement?.Attachments;
            return attachments != null
                && attachments.Exists(a => a != null && a.ContentType == "video/mp4");
        }

        [Fact]
        public async Task NoCohortPolicy_DoesNotRecord()
        {
            using DataDbContext context = BuildDataContext(nameof(NoCohortPolicy_DoesNotRecord));
            (Module module, LocalHardware hardware) = SeedEntitledLaunch(context);
            Guid actor = Guid.NewGuid();
            LauncherLogic logic = BuildLauncherLogic(context, new Hardware() { Id = hardware.Id }, actor);

            StatementInitalizationResponseViewModel response =
                await logic.InitalizeStatement(Request(module, actor));

            Records(response).Should().BeFalse(
                "a launch with no educator policy behind it must not be recorded -- recording learner session video is opt-in");
        }

        [Fact]
        public async Task DeviceLinkedCohortWithPolicy_Records()
        {
            using DataDbContext context = BuildDataContext(nameof(DeviceLinkedCohortWithPolicy_Records));
            (Module module, LocalHardware hardware) = SeedEntitledLaunch(context);
            LinkDeviceToCohort(context, hardware, SeedCohort(context, recordSessions: true));
            Guid actor = Guid.NewGuid();
            LauncherLogic logic = BuildLauncherLogic(context, new Hardware() { Id = hardware.Id }, actor);

            StatementInitalizationResponseViewModel response =
                await logic.InitalizeStatement(Request(module, actor));

            Records(response).Should().BeTrue(
                "the station is linked to a cohort the educator set to record");
        }

        [Fact]
        public async Task LearnersCohortWithPolicy_Records_EvenWhenTheDeviceHasNoCohort()
        {
            using DataDbContext context = BuildDataContext(nameof(LearnersCohortWithPolicy_Records_EvenWhenTheDeviceHasNoCohort));
            (Module module, LocalHardware hardware) = SeedEntitledLaunch(context);
            Guid actor = Guid.NewGuid();
            var learner = new LocalApplicationUser() { Id = Guid.NewGuid(), Actor = actor };
            AddLearnerToCohort(context, learner.Id, SeedCohort(context, recordSessions: true));

            // The device is deliberately linked to NOTHING: this is the union's second arm on its
            // own, so a derivation that only ever consulted the device would fail here.
            LauncherLogic logic = BuildLauncherLogic(context, new Hardware() { Id = hardware.Id }, actor, learner);

            StatementInitalizationResponseViewModel response =
                await logic.InitalizeStatement(Request(module, actor));

            Records(response).Should().BeTrue(
                "the learner belongs to a cohort the educator set to record, whatever station they are on");
        }

        [Fact]
        public async Task ArchivedCohortWithPolicy_DoesNotRecord()
        {
            using DataDbContext context = BuildDataContext(nameof(ArchivedCohortWithPolicy_DoesNotRecord));
            (Module module, LocalHardware hardware) = SeedEntitledLaunch(context);
            LinkDeviceToCohort(context, hardware, SeedCohort(context, recordSessions: true, archived: true));
            Guid actor = Guid.NewGuid();
            LauncherLogic logic = BuildLauncherLogic(context, new Hardware() { Id = hardware.Id }, actor);

            StatementInitalizationResponseViewModel response =
                await logic.InitalizeStatement(Request(module, actor));

            Records(response).Should().BeFalse(
                "a retired cohort must not keep recording live sessions -- its policy is not visible in the UI that shows live ones");
        }

        [Fact]
        public async Task ClientAskingToRecord_IsIgnored_WithoutAPolicy()
        {
            using DataDbContext context = BuildDataContext(nameof(ClientAskingToRecord_IsIgnored_WithoutAPolicy));
            (Module module, LocalHardware hardware) = SeedEntitledLaunch(context);
            Guid actor = Guid.NewGuid();
            LauncherLogic logic = BuildLauncherLogic(context, new Hardware() { Id = hardware.Id }, actor);

            StatementInitalizationResponseViewModel response =
                await logic.InitalizeStatement(Request(module, actor, clientAsksToRecord: true));

            Records(response).Should().BeFalse(
                "a client cannot switch recording ON by asserting a flag -- the node decides from the educator's policy");
        }

        [Fact]
        public async Task ClientRefusingToRecord_CannotSuppress_ThePolicy()
        {
            using DataDbContext context = BuildDataContext(nameof(ClientRefusingToRecord_CannotSuppress_ThePolicy));
            (Module module, LocalHardware hardware) = SeedEntitledLaunch(context);
            LinkDeviceToCohort(context, hardware, SeedCohort(context, recordSessions: true));
            Guid actor = Guid.NewGuid();
            LauncherLogic logic = BuildLauncherLogic(context, new Hardware() { Id = hardware.Id }, actor);

            // clientAsksToRecord: false is what a device wanting to dodge recording would send,
            // and is also what BOTH shipped clients send today by never populating the field.
            StatementInitalizationResponseViewModel response =
                await logic.InitalizeStatement(Request(module, actor, clientAsksToRecord: false));

            Records(response).Should().BeTrue(
                "the person being recorded does not get to veto the educator's decision from the device");
        }

        [Fact]
        public async Task TheRecordedAttachment_CarriesTheRecordingName_NotTheWordVideo()
        {
            // Pins the shape the CLIENT side depends on, and the reason WI-12's PC gate could
            // never fire: Display carries the recording NAME (the video ownership chain keys on
            // it), so a client testing Display == "video" tests something that is never true.
            // ContentType is the field that answers "is this a recording instruction".
            using DataDbContext context = BuildDataContext(nameof(TheRecordedAttachment_CarriesTheRecordingName_NotTheWordVideo));
            (Module module, LocalHardware hardware) = SeedEntitledLaunch(context);
            LinkDeviceToCohort(context, hardware, SeedCohort(context, recordSessions: true));
            Guid actor = Guid.NewGuid();
            LauncherLogic logic = BuildLauncherLogic(context, new Hardware() { Id = hardware.Id }, actor);

            StatementInitalizationResponseViewModel response =
                await logic.InitalizeStatement(Request(module, actor));

            XM.Attachment attachment = response.Statement.Attachments[0];
            attachment.ContentType.Should().Be("video/mp4");
            attachment.Display.Should().NotBeNull();
            attachment.Display.Values.Should().NotContain(
                v => v != null && v.ToLower() == "video",
                "Display carries the recording name, which is why a client gate testing it for the literal \"video\" never fired");
        }
    }
}
